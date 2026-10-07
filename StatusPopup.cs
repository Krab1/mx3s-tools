using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MxBattery;

/// <summary>Left-click flyout: themed, rounded, custom painted. Closes when it loses focus.</summary>
sealed class StatusPopup : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    readonly BatteryReading? _r;
    readonly DateTime _at;
    readonly Settings _cfg;
    readonly bool _light = Icons.TaskbarIsLight();

    public StatusPopup(BatteryReading? r, DateTime at, Settings cfg)
    {
        _r = r; _at = at; _cfg = cfg;
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual; ClientSize = new(300, 150);
        BackColor = _light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(43, 43, 43);
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new(wa.Right - Width - 12, wa.Bottom - Height - 12);
        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ClassStyle |= 0x20000; return cp; }       // CS_DROPSHADOW
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int round = 2;                                                                  // DWMWCP_ROUND (Windows 11)
        DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var fg = _light ? Color.FromArgb(26, 26, 26) : Color.White;
        var dim = _light ? Color.FromArgb(100, 100, 100) : Color.FromArgb(180, 180, 180);
        var track = _light ? Color.FromArgb(225, 225, 225) : Color.FromArgb(70, 70, 70);
        var accent = Icons.Status(_r?.Percent, _light, _cfg);
        using (var p = new Pen(_light ? Color.FromArgb(215, 215, 215) : Color.FromArgb(75, 75, 75))) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

        using var title = new Font("Segoe UI Semibold", 10f); using var small = new Font("Segoe UI", 9f);
        using var big = new Font("Segoe UI Semibold", 30f, FontStyle.Regular, GraphicsUnit.Point);
        using var fgB = new SolidBrush(fg); using var dimB = new SolidBrush(dim); using var accB = new SolidBrush(accent);

        using (var app = Icons.App(18)) g.DrawImage(app, 16, 14, 18, 18);
        g.DrawString("MX Master 3S", title, fgB, 40, 13);
        if (_r is not null)
        {
            var via = _r.Via.ToString();
            var sz = g.MeasureString(via, small);
            g.DrawString(via, small, dimB, Width - 16 - sz.Width, 15);
        }

        if (_r is null)
        {
            g.DrawString("Not connected", new Font("Segoe UI Semibold", 16f), dimB, 16, 50);
            g.DrawString("Wake the mouse or check the connection.", small, dimB, 16, 88);
        }
        else
        {
            g.DrawString($"{_r.Percent}%", big, accB, 12, 36);
            var state = _r.Charging switch { true => "Charging", false => "On battery", _ => "" };
            if (state.Length > 0) g.DrawString(state, small, dimB, 130, 62);

            const int bx = 16, by = 100, bh = 10; int bw = Width - 32;
            using (var path = Pill(bx, by, bw, bh)) { using var tb = new SolidBrush(track); g.FillPath(tb, path); }
            int fw = Math.Max(bh, bw * Math.Clamp(_r.Percent, 0, 100) / 100);
            using (var path = Pill(bx, by, fw, bh)) g.FillPath(accB, path);
            using var tick = new Pen(dim, 1);
            foreach (var t in new[] { _cfg.CriticalPercent, _cfg.LowPercent })
                g.DrawLine(tick, bx + bw * t / 100, by + bh + 2, bx + bw * t / 100, by + bh + 6);
        }
        g.DrawString($"Checked {_at:t}  ·  alerts at {_cfg.LowPercent}% / {_cfg.CriticalPercent}%", small, dimB, 16, 122);
    }

    static GraphicsPath Pill(float x, float y, float w, float h)
    {
        var p = new GraphicsPath(); float d = h;
        p.AddArc(x, y, d, d, 90, 180); p.AddArc(x + w - d, y, d, d, 270, 180); p.CloseFigure();
        return p;
    }
}
