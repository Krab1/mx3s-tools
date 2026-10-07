using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MxBattery;

/// <summary>Tray icon, right-click menu, left-click status popup, toasts.</summary>
public sealed class Tray : IDisposable
{
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);

    static readonly Font PopupFont = new("Segoe UI", 10);
    readonly Settings _cfg;
    readonly NotifyIcon _ni = new() { Visible = true };
    readonly ToolStripMenuItem _status = new("Checking…") { Enabled = false };
    readonly ToolStripMenuItem _pause = new("Pause notifications") { CheckOnClick = true };
    BatteryReading? _last;
    DateTime _lastAt;
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
        OnReading(null);
    }

    static ToolStripMenuItem Item(string text, Action a)
    {
        var i = new ToolStripMenuItem(text);
        i.Click += (_, _) => a();
        return i;
    }

    public void OnReading(BatteryReading? r)
    {
        _last = r; _lastAt = DateTime.Now;
        _status.Text = Describe(r);
        var tip = "MX Master 3S: " + Describe(r);
        _ni.Text = tip[..Math.Min(63, tip.Length)];          // NotifyIcon limit: 63 chars
        var old = _ni.Icon;
        _ni.Icon = Render(r);
        old?.Dispose();
    }

    public void OnAlert(Alert a, BatteryReading r)
    {
        if (_pause.Checked) return;
        var (title, text) = a switch
        {
            Alert.Critical => ("Mouse battery critical", $"{r.Percent}% left — charge now."),
            Alert.Full => ("Mouse fully charged", "100%"),
            _ => ("Mouse battery low", $"{r.Percent}% left."),
        };
        _ni.ShowBalloonTip(8000, title, text, a == Alert.Full ? ToolTipIcon.Info : ToolTipIcon.Warning);
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
        var f = _popup = new Form
        {
            FormBorderStyle = FormBorderStyle.FixedToolWindow, ShowInTaskbar = false, TopMost = true,
            StartPosition = FormStartPosition.Manual, Text = "MX Master 3S", ClientSize = new(240, 110),
        };
        var wa = Screen.PrimaryScreen!.WorkingArea;
        f.Location = new(wa.Right - f.Width - 8, wa.Bottom - f.Height - 8);
        f.Controls.Add(new Label
        {
            Dock = DockStyle.Fill, Padding = new(12), Font = PopupFont,
            Text = $"{Describe(_last)}\r\n\r\nChecked {_lastAt:T}\r\nAlert below {_cfg.LowPercent}% (critical {_cfg.CriticalPercent}%)",
        });
        f.Deactivate += (_, _) => f.Close();
        f.Show();
        f.Activate();
    }

    Icon Render(BatteryReading? r)
    {
        int s = SystemInformation.SmallIconSize.Width;
        using var bmp = new Bitmap(s, s);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var c = r is null ? Color.Gray
                : r.Percent <= _cfg.CriticalPercent ? Color.Red
                : r.Percent <= _cfg.LowPercent ? Color.Orange : Color.LimeGreen;
            using var brush = new SolidBrush(c);
            if (_cfg.IconStyle == IconStyle.Glyph)
            {
                float w = s * 0.5f, x = (s - w) / 2f, top = s * 0.12f, h = s * 0.8f;
                using var pen = new Pen(c, Math.Max(1, s / 12f));
                g.DrawRectangle(pen, x, top, w, h);
                float fill = h * (r?.Percent ?? 0) / 100f;
                g.FillRectangle(brush, x, top + h - fill, w, fill);
            }
            else
            {
                var text = r is null ? "?" : r.Percent.ToString();
                using var font = new Font("Segoe UI", text.Length > 2 ? s * 0.42f : s * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel);
                var sz = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (s - sz.Width) / 2f, (s - sz.Height) / 2f);
            }
        }
        var h2 = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(h2).Clone();
        DestroyIcon(h2);
        return icon;
    }

    public void Dispose()
    {
        _ni.Visible = false;
        _ni.Icon?.Dispose();
        _ni.Dispose();
    }
}
