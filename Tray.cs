using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MxBattery;

/// <summary>Tray icon (theme-aware), right-click menu, left-click flyout, toasts.</summary>
public sealed class Tray : IDisposable
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    readonly Settings _cfg;
    readonly NotifyIcon _ni = new() { Visible = true };
    readonly ToolStripMenuItem _status = new("Checking…") { Enabled = false };
    readonly ToolStripMenuItem _pause = new("Pause notifications") { CheckOnClick = true };
    BatteryReading? _last;
    DateTime _lastAt = DateTime.Now;
    Form? _popup;

    public event Action? RefreshRequested;

    public Tray(Settings cfg)
    {
        _cfg = cfg;
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _status, new ToolStripSeparator(),
            Item("Refresh now", () => RefreshRequested?.Invoke()),
            Item("Settings…", OpenSettings),
            _pause, new ToolStripSeparator(),
            Item("Exit", Application.Exit),
        ]);
        _ni.ContextMenuStrip = menu;                         // right click
        _ni.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
        SystemEvents.UserPreferenceChanged += OnTheme;       // light/dark taskbar switch
        OnReading(null);
    }

    static ToolStripMenuItem Item(string text, Action a)
    {
        var i = new ToolStripMenuItem(text);
        i.Click += (_, _) => a();
        return i;
    }

    void OnTheme(object? s, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color) UpdateIcon();
    }

    public void OnReading(BatteryReading? r)
    {
        _last = r; _lastAt = DateTime.Now;
        _status.Text = Describe(r);
        var tip = "MX Master 3S: " + Describe(r);
        _ni.Text = tip[..Math.Min(63, tip.Length)];          // NotifyIcon limit: 63 chars
        UpdateIcon();
    }

    void UpdateIcon()
    {
        int s = SystemInformation.SmallIconSize.Width;
        using var bmp = Icons.Tray(s, _last?.Percent, _last?.Charging == true, Icons.TaskbarIsLight(), _cfg.IconStyle, _cfg);
        var h = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h).Clone();
        DestroyIcon(h);
        var old = _ni.Icon;
        _ni.Icon = icon;
        old?.Dispose();
    }

    public void OnAlert(Alert a, BatteryReading r)
    {
        if (_pause.Checked) return;
        var (kind, title, text) = a switch
        {
            Alert.Critical => (Kind.Critical, "Mouse battery critical", $"{r.Percent}% left — charge now."),
            Alert.Full => (Kind.Full, "Mouse fully charged", "MX Master 3S is at 100%."),
            _ => (Kind.Low, "Mouse battery low", $"MX Master 3S is at {r.Percent}%."),
        };
        try { Toasts.Show(kind, title, text); }
        catch { _ni.ShowBalloonTip(8000, title, text, kind == Kind.Full ? ToolTipIcon.Info : ToolTipIcon.Warning); }   // toast API unavailable
    }

    static string Describe(BatteryReading? r) => r is null
        ? "Not connected"
        : $"{r.Percent}%{(r.Charging == true ? " (charging)" : "")} via {r.Via}";

    void OpenSettings()
    {
        using var f = new SettingsForm(_cfg);
        if (f.ShowDialog() == DialogResult.OK) { OnReading(_last); RefreshRequested?.Invoke(); }
    }

    void ShowPopup()
    {
        _popup?.Close();
        _popup = new StatusPopup(_last, _lastAt, _cfg);
        _popup.Show();
        _popup.Activate();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnTheme;
        _ni.Visible = false;
        _ni.Icon?.Dispose();
        _ni.Dispose();
    }
}
