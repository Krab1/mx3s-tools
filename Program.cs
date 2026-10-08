using System.Runtime.InteropServices;

namespace MxBattery;

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    /// <summary>Diagnostics: MxBattery.exe --probe out.txt  (traces one read of each source into the file)</summary>
    static int Probe(string path)
    {
        File.WriteAllText(path, Diagnostics.Run(Settings.Load()));
        return 0;
    }

    /// <summary>Quiet check shortly after start, then daily. Runs on the UI context so the tray can be touched directly.</summary>
    static async Task CheckUpdatesLoop(Tray tray, Settings cfg, CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
            while (!ct.IsCancellationRequested)
            {
                if (cfg.CheckForUpdates) await tray.CheckUpdatesAsync(manual: false);
                await Task.Delay(TimeSpan.FromHours(24), ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--selftest")) { AttachConsole(-1); return SelfTest.Run(); }

        if (args.Length == 2 && args[0] == "--probe") return Probe(args[1]);
        if (args.Length == 2 && args[0] == "--icons") return IconSheet.Run(args[1]);
        if (args.Length == 2 && args[0] == "--popup")                         // diagnostics: render popup states to a PNG
        {
            ApplicationConfiguration.Initialize();
            var c = new Settings();
            BatteryReading?[] rs = [new(95, false, Via.Bolt), new(72, true, Via.Bolt), new(25, null, Via.Bluetooth), new(8, false, Via.Bolt), null];
            using var sheet = new Bitmap(5 * 700 + 10, 3 * 2 * 330 + 10);
            using var gg = Graphics.FromImage(sheet);
            gg.Clear(Color.Gray);
            int row = 0;
            foreach (var scale in new[] { 1.0f, 1.5f, 2.0f })                 // 100% / 150% / 200% display scaling, dark + light
                foreach (var light in new[] { false, true })
                {
                    int x = 5, rowH = 0;
                    for (int i = 0; i < rs.Length; i++)
                    {
                        using var f = new StatusPopup(rs[i], DateTime.Now, c, scale, light, rs[i] is null ? "Bolt: no Logi Bolt receiver found.\nBluetooth: \"MX Master 3S\" is paired but not connected." : null);
                        f.Show(); f.Opacity = 0;
                        using var b = new Bitmap(f.Width, f.Height);
                        f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                        gg.DrawImage(b, x, 5 + row * 330); x += f.Width + 8; rowH = Math.Max(rowH, f.Height);
                    }
                    row++;
                }
            sheet.Save(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "--settings")                      // diagnostics: render the settings window at 100/150/200% to a PNG
        {
            ApplicationConfiguration.Initialize();
            Application.SetColorMode(SystemColorMode.System);
            Settings.NoPersist = true;
            var shots = new List<Bitmap>();
            foreach (var k in new[] { 1.0f, 1.5f, 2.0f })
            {
                using var f = new SettingsForm(new Settings(), k);
                f.Show(); Application.DoEvents(); Application.DoEvents();
                var b = new Bitmap(f.Width, f.Height);
                f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height));
                shots.Add(b);
            }
            using var sheet = new Bitmap(shots.Sum(b => b.Width) + 40, shots.Max(b => b.Height) + 20);
            using (var g = Graphics.FromImage(sheet)) { g.Clear(Color.Gray); int x = 10; foreach (var b in shots) { g.DrawImage(b, x, 10); x += b.Width + 10; } }
            sheet.Save(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "--toast")                         // diagnostics: fire one toast, e.g. --toast low
        {
            var k = Enum.Parse<Kind>(args[1], true);
            Toasts.Show(k, k == Kind.Full ? "Mouse fully charged" : k == Kind.Critical ? "Mouse battery critical" : "Mouse battery low", "MX Master 3S is at 28%.");
            Thread.Sleep(1500);
            return 0;
        }

        if (args.Length == 1 && args[0] == "--update-test")                   // diagnostics: check + apply an update with no prompts
        {
            var (rel, _) = Updater.CheckAsync().GetAwaiter().GetResult();
            if (rel is null) return 2;
            Updater.ApplyAsync(rel).GetAwaiter().GetResult();
            return 0;
        }
        if (args.Length == 2 && args[0] == "--wait-pid")                      // launched by the updater: let the old instance exit first
        {
            try { System.Diagnostics.Process.GetProcessById(int.Parse(args[1])).WaitForExit(15000); } catch { }
        }

        bool mock = args.Contains("--mock");                                  // preview: fake battery states, nothing persisted
        if (mock) Settings.NoPersist = true;
        using var single = new Mutex(true, mock ? @"Local\MxBattery.mock" : @"Local\MxBattery", out bool first);
        if (!first) return 0;
        if (!mock) { Updater.CleanUp(); Settings.EnsureStartMenuShortcut(); }

        ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001   // dark mode support is experimental in WinForms
        Application.SetColorMode(SystemColorMode.System);                    // menus + settings follow the Windows theme
#pragma warning restore WFO5001
        var cfg = mock ? new Settings() : Settings.Load();
        if (!mock) Settings.SetAutostart(cfg.StartWithWindows);              // keep the Run key in sync with the setting

        // Composition root: the only place that knows the concrete sources.
        var fake = new MockSource();
        IBatterySource source = mock ? fake : new CompositeSource(
            new HidppSource(() => cfg.DeviceFilter),
            new BluetoothSource(() => cfg.DeviceFilter),
            () => cfg.Preferred);

        using var tray = new Tray(cfg);
        var monitor = new Monitor(source, cfg, tray.OnReading, tray.OnAlert);
        tray.RefreshRequested += monitor.RefreshNow;
        tray.IssueProvider = () => source.Issue;
        using var cts = new CancellationTokenSource();
        _ = monitor.RunAsync(cts.Token);
        if (mock)
        {
            tray.SetTooltipPrefix("[MOCK] ");
            foreach (var (label, r) in MockSource.States.Reverse())
                tray.AddMenu("Mock: " + label, () => { fake.Current = r; monitor.RefreshNow(); });
            tray.AddMenu("Mock: toast - fully charged", () => Toasts.Show(Kind.Full, "Mouse fully charged", "MX Master 3S is at 100%."));
            tray.AddMenu("Mock: toast - critical", () => Toasts.Show(Kind.Critical, "Mouse battery critical", "8% left — charge now."));
            tray.AddMenu("Mock: toast - low", () => Toasts.Show(Kind.Low, "Mouse battery low", "MX Master 3S is at 28%."));
        }
        else _ = CheckUpdatesLoop(tray, cfg, cts.Token);
        Application.Run();                                  // process exits next; no need to cancel the loop
        return 0;
    }
}

/// <summary>Smallest check that fails if the alert or source-selection logic breaks: MxBattery.exe --selftest</summary>
static class SelfTest
{
    sealed class Fake(BatteryReading? r) : IBatterySource
    {
        public int Calls;
        public Task<BatteryReading?> ReadAsync(CancellationToken ct) { Calls++; return Task.FromResult(r); }
    }

    static void Check(bool ok, string what) { if (!ok) throw new Exception("FAIL: " + what); }

    public static int Run()
    {
        try
        {
            var s = new Settings { LowPercent = 30, CriticalPercent = 10, Hysteresis = 5, ReminderMinutes = 0 };
            var a = new AlertState();
            var t = DateTime.Now;
            BatteryReading R(int p, bool? c = false) => new(p, c, Via.Bolt);

            Check(a.Next(R(50), s, t) == Alert.None, "above low");
            Check(a.Next(R(30), s, t) == Alert.Low, "low fires at threshold");
            Check(a.Next(R(28), s, t) == Alert.None, "low fires once");
            Check(a.Next(null, s, t) == Alert.None, "unreachable is silent");
            Check(a.Next(R(10), s, t) == Alert.Critical, "critical fires");
            Check(a.Next(R(9), s, t) == Alert.None, "critical fires once");
            Check(a.Next(R(33), s, t) == Alert.None && a.Next(R(30), s, t) == Alert.None, "inside hysteresis does not re-arm");
            Check(a.Next(R(36), s, t) == Alert.None && a.Next(R(30), s, t) == Alert.Low, "re-arms above low+hysteresis");
            Check(a.Next(R(20, true), s, t) == Alert.None && a.Next(R(25), s, t) == Alert.Low, "charging re-arms");

            s.ReminderMinutes = 10;
            Check(a.Next(R(25), s, t.AddMinutes(5)) == Alert.None, "reminder too early");
            Check(a.Next(R(25), s, t.AddMinutes(11)) == Alert.Reminder, "reminder fires");

            Check(Settings.Migrate(new Settings { IconStyle = IconStyle.Number, Schema = 0 }).IconStyle == IconStyle.Mouse, "old settings file moves to the new icon style");
            Check(Settings.Migrate(new Settings { IconStyle = IconStyle.Number, Schema = 2 }).IconStyle == IconStyle.Number, "a deliberate Number choice survives");

            var bolt = new Fake(R(40)); var bt = new Fake(new(60, null, Via.Bluetooth));
            var pref = Preferred.Auto;
            var c = new CompositeSource(bolt, bt, () => pref);
            Check(c.ReadAsync(default).Result!.Via == Via.Bolt && bt.Calls == 0, "auto: bolt first, bt untouched");
            pref = Preferred.Bluetooth;
            Check(c.ReadAsync(default).Result!.Via == Via.Bluetooth, "forced bluetooth");
            var c2 = new CompositeSource(new Fake(null), bt, () => Preferred.Auto);
            Check(c2.ReadAsync(default).Result!.Via == Via.Bluetooth, "auto: falls back to bluetooth");

            Console.WriteLine("selftest OK");
            return 0;
        }
        catch (Exception e) { Console.WriteLine(e.Message); return 1; }
    }
}
