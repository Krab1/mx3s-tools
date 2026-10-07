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
    readonly ToolStripMenuItem _updateItem = new("Install update") { Visible = false, Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
    BatteryReading? _last;
    DateTime _lastAt = DateTime.Now;
    Form? _popup;
    Release? _pending;

    public event Action? RefreshRequested;

    public Tray(Settings cfg)
    {
        _cfg = cfg;
        var menu = new ContextMenuStrip();
        menu.Items.AddRange([
            _status, _updateItem, new ToolStripSeparator(),
            Item("Refresh now", () => RefreshRequested?.Invoke()),
            Item("Check for updates", async () => await CheckUpdatesAsync(manual: true)),
            Item("Settings…", OpenSettings),
            _pause, new ToolStripSeparator(),
            Item("Exit", Application.Exit),
        ]);
        _ni.ContextMenuStrip = menu;                         // right click
        _ni.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) ShowPopup(); };
        _updateItem.Click += async (_, _) => await InstallAsync();
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

    /// <summary>manual = user clicked "Check for updates": always answer, and go straight to the install prompt.</summary>
    public async Task CheckUpdatesAsync(bool manual)
    {
        var (r, reachable) = await Updater.CheckAsync();
        if (r is null)
        {
            if (manual) MessageBox.Show(reachable ? $"You're on the latest version ({Updater.Current})."
                : "Couldn't reach the update server (offline, or updates are no longer published). The current version keeps working.", "MX Battery");
            return;                                                          // automatic checks stay silent either way
        }
        if (!manual && r.Version.ToString() == _cfg.SkippedVersion) return;
        bool isNew = _pending?.Version != r.Version;
        _pending = r;
        _updateItem.Text = $"Install update {r.Version}…";
        _updateItem.Visible = true;
        if (manual) await InstallAsync();
        else if (isNew)
            try { Toasts.ShowText("MX Battery update available", $"Version {r.Version} is ready. Right-click the tray icon to install."); } catch { }
    }

    async Task InstallAsync()
    {
        if (_pending is not { } r) return;
        var a = MessageBox.Show($"Install MX Battery {r.Version}? The app will restart.\n\nYes = install now\nNo = skip this version\nCancel = decide later",
            "MX Battery update", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (a == DialogResult.No)
        {
            _cfg.SkippedVersion = r.Version.ToString(); _cfg.Save();
            _updateItem.Visible = false; _pending = null;
            return;
        }
        if (a != DialogResult.Yes) return;
        try { await Updater.ApplyAsync(r); Application.Exit(); }
        catch (Exception e) { MessageBox.Show("Update failed, the current version keeps running.\n\n" + e.Message, "MX Battery"); }
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
