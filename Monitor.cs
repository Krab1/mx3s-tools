namespace MxBattery;

public enum Alert { None, Low, Critical, Reminder, Full }

/// <summary>Decides when to notify: once per crossing, re-armed by hysteresis or charging.</summary>
public sealed class AlertState
{
    bool _low = true, _crit = true, _full = true;
    DateTime _last;

    public Alert Next(BatteryReading? r, Settings s, DateTime now)
    {
        if (r is null) return Alert.None;                    // unreachable: keep state so a channel switch can't re-fire
        int p = r.Percent;
        if (r.Charging == true)
        {
            _low = _crit = true;
            if (p < 100) _full = true;
            else if (_full) { _full = false; return s.NotifyFull ? Alert.Full : Alert.None; }
            return Alert.None;
        }
        _full = true;
        if (p > s.LowPercent + s.Hysteresis) _low = true;
        if (p > s.CriticalPercent + s.Hysteresis) _crit = true;
        if (p <= s.CriticalPercent && _crit) { _crit = _low = false; _last = now; return Alert.Critical; }
        if (p <= s.LowPercent && _low) { _low = false; _last = now; return Alert.Low; }
        if (p <= s.LowPercent && s.ReminderMinutes > 0 && now - _last >= TimeSpan.FromMinutes(s.ReminderMinutes))
        { _last = now; return Alert.Reminder; }
        return Alert.None;
    }
}

/// <summary>Polling loop. Start from the UI thread: callbacks run on its sync context.</summary>
public sealed class Monitor(IBatterySource source, Settings cfg, Action<BatteryReading?> onReading, Action<Alert, BatteryReading> onAlert)
{
    readonly SemaphoreSlim _wake = new(0);
    readonly AlertState _state = new();

    public void RefreshNow() { if (_wake.CurrentCount == 0) _wake.Release(); }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            BatteryReading? r = null;
            try { r = await source.ReadAsync(ct); } catch (Exception e) when (e is not OperationCanceledException) { }
            onReading(r);
            var a = _state.Next(r, cfg, DateTime.Now);
            if (a != Alert.None) onAlert(a, r!);
            var wait = r is null ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(Math.Max(1, cfg.PollMinutes));
            try { await _wake.WaitAsync(wait, ct); } catch (OperationCanceledException) { }
        }
    }
}
