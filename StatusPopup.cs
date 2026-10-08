using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MxBattery;

/// <summary>
/// Left-click flyout: themed, rounded, custom painted. Closes when it loses focus.
/// Everything is laid out in pixels scaled by the display DPI and measured from the real fonts,
/// so it can't overlap or clip at 125/150/200% scaling.
/// </summary>
sealed class StatusPopup : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    readonly BatteryReading? _r;
    readonly DateTime _at;
    readonly Settings _cfg;
    readonly bool _light;
    readonly float _k;                       // DPI scale: 1.0 = 96 dpi
    readonly Font _title, _small, _big, _notConn;
    readonly Metrics _lay;

    record Metrics(float BigY, SizeF Big, float StateX, float StateY, float BarY, float FootY, int W, int H);

    /// <param name="scale">Test override for the DPI scale; null = this display's scale.</param>
    public StatusPopup(BatteryReading? r, DateTime at, Settings cfg, float? scale = null, bool? light = null)
    {
        _r = r; _at = at; _cfg = cfg;
        _light = light ?? Icons.TaskbarIsLight();
        _k = scale ?? DeviceDpi / 96f;
        _title = Px("Segoe UI Semibold", 14, FontStyle.Regular); _small = Px("Segoe UI", 12, FontStyle.Regular);
        _big = Px("Segoe UI Semibold", 40, FontStyle.Regular); _notConn = Px("Segoe UI Semibold", 22, FontStyle.Regular);

        _lay = Measure();
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; DoubleBuffered = true;
        StartPosition = FormStartPosition.Manual; AutoScaleMode = AutoScaleMode.None;
        ClientSize = new(_lay.W, _lay.H);
        BackColor = _light ? Color.FromArgb(249, 249, 249) : Color.FromArgb(43, 43, 43);
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new(wa.Right - Width - (int)S(12), wa.Bottom - Height - (int)S(12));
        Deactivate += (_, _) => Close();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    float S(float v) => v * _k;
    Font Px(string name, float px, FontStyle st) => new(name, px * _k, st, GraphicsUnit.Pixel);

    static readonly StringFormat Tight = new(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces };

    Metrics Measure()
    {
        using var bmp = new Bitmap(1, 1); using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        float pad = S(16), y = S(14);
        float titleH = g.MeasureString("MX", _title, 1000, Tight).Height;
        float bigY = y + titleH + S(6);
        var big = g.MeasureString(_r is null ? "Not connected" : $"{_r.Percent}%", _r is null ? _notConn : _big, 1000, Tight);
        float smallH = g.MeasureString("Ag", _small, 1000, Tight).Height;
        float barY = bigY + big.Height + S(8);
        int h = (int)Math.Ceiling(barY + S(10) + S(8) + S(16));            // bar, threshold ticks, bottom padding
        return new(bigY, big, pad + big.Width + S(10), bigY + big.Height - smallH - S(6), barY, 0, (int)Math.Ceiling(S(300)), h);
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
        using var fgB = new SolidBrush(fg); using var dimB = new SolidBrush(dim); using var accB = new SolidBrush(accent);

        float pad = S(16), ic = S(20);
        using (var app = Icons.App(Math.Max(16, (int)ic))) g.DrawImage(app, pad, S(14), ic, ic);
        g.DrawString("MX Master 3S", _title, fgB, pad + ic + S(8), S(14), Tight);
        if (_r is not null)
        {
            var via = _r.Via.ToString();
            g.DrawString(via, _small, dimB, Width - pad - g.MeasureString(via, _small, 1000, Tight).Width, S(16), Tight);
        }

        if (_r is null)
        {
            g.DrawString("Not connected", _notConn, dimB, pad, _lay.BigY, Tight);
            g.DrawString("Wake the mouse or check the connection.", _small, dimB, pad, _lay.BarY, Tight);
        }
        else
        {
            g.DrawString($"{_r.Percent}%", _big, accB, pad, _lay.BigY, Tight);
            var state = _r.Charging switch { true => "Charging", false => "On battery", _ => "" };
            if (state.Length > 0) g.DrawString(state, _small, dimB, _lay.StateX, _lay.StateY, Tight);

            float bx = pad, bw = Width - pad * 2, bh = S(10);
            using (var path = Pill(bx, _lay.BarY, bw, bh)) { using var tb = new SolidBrush(track); g.FillPath(tb, path); }
            float fw = Math.Max(bh, bw * Math.Clamp(_r.Percent, 0, 100) / 100f);
            using (var path = Pill(bx, _lay.BarY, fw, bh)) g.FillPath(accB, path);
            using var tick = new Pen(dim, Math.Max(1, S(1)));
            foreach (var t in new[] { _cfg.CriticalPercent, _cfg.LowPercent })
                g.DrawLine(tick, bx + bw * t / 100, _lay.BarY + bh + S(2), bx + bw * t / 100, _lay.BarY + bh + S(6));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _title.Dispose(); _small.Dispose(); _big.Dispose(); _notConn.Dispose(); }
        base.Dispose(disposing);
    }

    static GraphicsPath Pill(float x, float y, float w, float h)
    {
        var p = new GraphicsPath(); float d = h;
        p.AddArc(x, y, d, d, 90, 180); p.AddArc(x + w - d, y, d, d, 270, 180); p.CloseFigure();
        return p;
    }
}
