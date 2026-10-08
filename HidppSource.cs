using HidSharp;
using System.Text;

namespace MxBattery;

/// <summary>
/// Reads the battery over HID++ 2.0 through a Logi Bolt receiver.
/// UNVERIFIED against hardware: written from the HID++ 2.0 spec (IRoot 0x0000, DeviceName 0x0005,
/// Unified Battery 0x1004, Battery Status 0x1000). Verify with the receiver plugged in.
/// </summary>
public sealed class HidppSource(Func<string> nameFilter) : IBatterySource
{
    const int Vid = 0x046D, BoltPid = 0xC548;
    const byte SwId = 0xA;            // our id in the low nibble; lets us ignore Options+ replies
    const int Timeout = 400, PingWait = 1500;

    public static Action<string>? Log;     // --probe tracing only

    HidStream? _s;
    int _dev;                          // receiver slot 1..6 holding the mouse, 0 = unknown
    int _feat;                         // battery feature index, 0 = unknown
    bool _unified;                     // true: 0x1004, false: 0x1000
    string? _issue;

    public string? Issue => _issue;

    public Task<BatteryReading?> ReadAsync(CancellationToken ct) => Task.Run(Read, ct);

    BatteryReading? Read()
    {
        try
        {
            if (_s is null && !Open()) return null;
            if (_dev == 0 && !FindDevice()) return null;
            if (_feat == 0 && !FindBattery()) return null;
            var r = _unified ? ReadUnified() : ReadStatus();
            if (r is null) { _issue = $"Bolt: the mouse (receiver slot {_dev}) did not answer. It may be asleep, move it."; _dev = _feat = 0; }   // rediscover next poll
            else _issue = null;
            return r;
        }
        catch (Exception e) when (e is IOException or TimeoutException or ObjectDisposedException)
        { _issue = "Bolt: lost contact with the receiver (" + e.GetType().Name + ")."; Reset(); return null; }                            // receiver unplugged or write stalled
    }

    void Reset() { _s?.Dispose(); _s = null; _dev = 0; _feat = 0; }

    bool Open()
    {
        // Windows exposes one HID device per collection; the long-report (0x11, 20 bytes) one is ours.
        var all = DeviceList.Local.GetHidDevices(Vid, BoltPid).ToList();
        foreach (var h in all)
            Log?.Invoke($"hid {h.DevicePath} in={h.GetMaxInputReportLength()} out={h.GetMaxOutputReportLength()}");
        if (all.Count == 0) { _issue = "Bolt: no Logi Bolt receiver found."; return false; }
        var dev = all.FirstOrDefault(d => d.GetMaxOutputReportLength() == 20 && d.GetMaxInputReportLength() == 20);
        if (dev is null) { _issue = "Bolt: receiver found, but its HID++ interface was not recognised."; return false; }
        if (!dev.TryOpen(out var s)) { _issue = "Bolt: receiver found, but it could not be opened. Another app may be holding it."; return false; }
        _s = s;
        return true;
    }

    bool FindDevice()
    {
        var filter = nameFilter();
        var answered = Ping();
        var seen = new List<string>();
        if (answered.Count == 0) { _issue = "Bolt: receiver found, but no paired device answered. Is the mouse awake and on the Bolt channel?"; return false; }
        foreach (int d in answered)
        {
            var nameIdx = Req(d, 0, 0, 0x00, 0x05)?[4] ?? 0;                // IRoot.GetFeature(DeviceName)
            if (nameIdx == 0) continue;
            int count = Req(d, nameIdx, 0)?[4] ?? 0;
            var sb = new StringBuilder();
            while (sb.Length < count && Req(d, nameIdx, 1, (byte)sb.Length) is { } r)
            {
                int before = sb.Length;
                for (int i = 4; i < r.Length && r[i] != 0 && sb.Length < count; i++) sb.Append((char)r[i]);
                if (sb.Length == before) break;
            }
            seen.Add(sb.Length > 0 ? sb.ToString() : $"slot {d}");
            if (sb.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)) { _dev = d; return true; }
        }
        _issue = $"Bolt: {answered.Count} device(s) answered, none named \"{filter}\" (saw: {string.Join(", ", seen)}).";
        return false;
    }

    bool FindBattery()
    {
        foreach (var (id, unified) in new[] { (0x1004, true), (0x1000, false) })
        {
            var idx = Req(_dev, 0, 0, (byte)(id >> 8), (byte)id)?[4] ?? 0;
            if (idx != 0) { _feat = idx; _unified = unified; return true; }
        }
        _issue = $"Bolt: the mouse (slot {_dev}) answered, but exposes no battery feature.";
        _dev = 0;
        return false;
    }

    BatteryReading? ReadUnified()
    {
        var r = Req(_dev, _feat, 1);                                         // GetStatus
        if (r is null) return null;
        int pct = r[4];
        if (pct == 0)                                                        // level flags only: critical/low/good/full
            pct = r[5] switch { 8 => 100, 4 => 50, 2 => 15, 1 => 5, _ => 0 };
        return new(pct, r[6] is 1 or 2 or 3, Via.Bolt);                      // 1,2 charging, 3 charge complete
    }

    BatteryReading? ReadStatus()
    {
        var r = Req(_dev, _feat, 0);                                         // GetBatteryLevelStatus
        return r is null ? null : new(r[4], r[6] is >= 1 and <= 4, Via.Bolt);
    }

    /// <summary>Ping all 6 receiver slots at once; a sleeping mouse can take ~1 s to wake and answer.</summary>
    List<int> Ping()
    {
        for (int d = 1; d <= 6; d++) Send(d, 0, 1, 0, 0, 0x5A);
        var alive = new List<int>();
        long end = Environment.TickCount64 + PingWait;
        while (alive.Count < 6)
        {
            long left = end - Environment.TickCount64;
            if (left <= 0) break;
            _s!.ReadTimeout = (int)left;
            byte[] r;
            try { r = _s.Read(); } catch (TimeoutException) { break; }
            Log?.Invoke("< " + Convert.ToHexString(r[..Math.Min(r.Length, 10)]));
            if (r.Length >= 7 && r[1] is >= 1 and <= 6 && r[2] == 0 && r[3] == (1 << 4 | SwId) && !alive.Contains(r[1])) alive.Add(r[1]);
        }
        return alive;
    }

    byte Send(int dev, int feat, int fn, params byte[] p)
    {
        var b = new byte[20];
        b[0] = 0x11; b[1] = (byte)dev; b[2] = (byte)feat; b[3] = (byte)(fn << 4 | SwId);
        p.CopyTo(b, 4);
        _s!.Write(b);
        Log?.Invoke("> " + Convert.ToHexString(b[..8]));
        return b[3];
    }

    /// <summary>Send a long request, return the matching reply, or null on error/timeout.</summary>
    byte[]? Req(int dev, int feat, int fn, params byte[] p)
    {
        byte sw = Send(dev, feat, fn, p);
        long end = Environment.TickCount64 + Timeout;
        while (true)
        {
            long left = end - Environment.TickCount64;
            if (left <= 0) return null;
            _s!.ReadTimeout = (int)left;
            byte[] r;
            try { r = _s.Read(); } catch (TimeoutException) { return null; }
            Log?.Invoke("< " + Convert.ToHexString(r[..Math.Min(r.Length, 10)]));
            if (r.Length < 7 || r[1] != dev) continue;
            if (r[2] == feat && r[3] == sw) return r;
            if (r[2] == 0xFF && r[3] == feat && r[4] == sw) return null;   // HID++ 2.0 error reply
        }
    }
}

