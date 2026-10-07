using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using Microsoft.Win32;

namespace MxBattery;

public enum Kind { Low, Critical, Full }

/// <summary>All icon artwork, drawn in code: app icon, theme-aware tray icon, notification badge.</summary>
public static class Icons
{
    // Windows 11 palette: brighter accents on dark, deeper ones on light for contrast.
    static readonly Color Blue1 = Color.FromArgb(0x3B, 0x8C, 0xFF), Blue2 = Color.FromArgb(0x1D, 0x4E, 0xD8);
    public static readonly Color Amber = Color.FromArgb(0xF5, 0xA5, 0x24), Red = Color.FromArgb(0xE5, 0x48, 0x4D), Green = Color.FromArgb(0x30, 0xA4, 0x6C);

    public static bool TaskbarIsLight()
    {
        try { return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) is 1; }
        catch { return false; }
    }

    // ---------- App icon: mouse silhouette used as a battery gauge ----------
    public static Bitmap App(int s, float level = 0.62f) => Draw(s, 4, g =>
    {
        float u = s * 4;
        var tile = Rounded(new(0, 0, u, u), u * 0.23f);
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, u, u), Blue1, Blue2, 55f)) g.FillPath(b, tile);
        using (var sheen = new LinearGradientBrush(new RectangleF(0, 0, u, u * 0.5f), Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
        { g.SetClip(tile); g.FillRectangle(sheen, 0, 0, u, u * 0.5f); g.ResetClip(); }

        var body = Rounded(new(u * 0.30f, u * 0.13f, u * 0.40f, u * 0.74f), u * 0.20f);
        using (var shadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
        using (var m = new Matrix()) { m.Translate(0, u * 0.015f); var sp = (GraphicsPath)body.Clone(); sp.Transform(m); g.FillPath(shadow, sp); }
        g.FillPath(Brushes.White, body);

        float top = u * 0.13f + u * 0.74f * (1 - level);                       // battery level, filled from the bottom
        g.SetClip(body);
        using (var fill = new SolidBrush(Color.FromArgb(0x2E, 0xD5, 0x73))) g.FillRectangle(fill, 0, top, u, u);
        g.ResetClip();

        using var line = new Pen(Color.FromArgb(200, Blue2), Math.Max(2, u * 0.022f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(line, u * 0.5f, u * 0.13f, u * 0.5f, u * 0.38f);             // button split
        g.DrawLine(line, u * 0.30f, u * 0.38f, u * 0.70f, u * 0.38f);
        using var wheel = new SolidBrush(Blue2);
        g.FillPath(wheel, Rounded(new(u * 0.468f, u * 0.19f, u * 0.064f, u * 0.13f), u * 0.032f));
    });

    // ---------- Tray: transparent, theme foreground, accent only when it matters ----------
    public static Bitmap Tray(int s, int? pct, bool charging, bool light, IconStyle style, Settings cfg)
    {
        var fg = light ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.White;
        Color accent = pct is null ? Color.FromArgb(150, fg)
            : pct <= cfg.CriticalPercent ? (light ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xFF, 0x6B, 0x6B))
            : pct <= cfg.LowPercent ? (light ? Color.FromArgb(0xB8, 0x6E, 0x00) : Color.FromArgb(0xFC, 0xC2, 0x3A))
            : charging ? (light ? Color.FromArgb(0x0F, 0x7B, 0x0F) : Color.FromArgb(0x6C, 0xCB, 0x5F)) : fg;
        return Draw(s, 1, g =>
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (style == IconStyle.Number)
            {
                var text = pct is null ? "–" : pct.ToString()!;
                using var font = new Font("Segoe UI", text.Length > 2 ? s * 0.58f : s * 0.74f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip };
                using var b = new SolidBrush(accent);
                g.DrawString(text, font, b, new RectangleF(0, 0, s, s), sf);   // charging = green digits, no extra mark
            }
            else
            {
                float w = s * 0.80f, h = s * 0.46f, x = s * 0.05f, y = (s - h) / 2f, t = Math.Max(1, s / 14f);
                using (var pen = new Pen(Color.FromArgb(pct is null ? 150 : 220, fg), t))
                    g.DrawPath(pen, Rounded(new(x + t / 2, y + t / 2, w - t, h - t), h * 0.22f));
                using (var nub = new SolidBrush(Color.FromArgb(200, fg))) g.FillRectangle(nub, x + w, y + h * 0.3f, Math.Max(1, s * 0.06f), h * 0.4f);
                if (pct is not null)
                {
                    float pad = t * 1.6f, iw = (w - pad * 2) * Math.Clamp(pct.Value, 0, 100) / 100f;
                    using var b = new SolidBrush(accent);
                    g.FillPath(b, Rounded(new(x + pad, y + pad, Math.Max(iw, 1), h - pad * 2), h * 0.1f));
                    if (charging) Bolt(g, new(x + w * 0.3f, y + h * 0.12f, w * 0.4f, h * 0.76f), light ? Color.White : Color.FromArgb(30, 30, 30), true);
                }
            }
        });
    }

    // ---------- Notification badge: coloured tile, white battery, state-specific mark ----------
    public static Bitmap Badge(int s, Kind kind) => Draw(s, 4, g =>
    {
        float u = s * 4;
        var c = kind switch { Kind.Critical => Red, Kind.Low => Amber, _ => Green };
        var tile = Rounded(new(0, 0, u, u), u * 0.23f);
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, u, u), Lighten(c, 0.18f), c, 60f)) g.FillPath(b, tile);

        float w = u * 0.64f, h = u * 0.36f, x = (u - w) / 2f - u * 0.02f, y = (u - h) / 2f, t = u * 0.045f;
        using (var pen = new Pen(Color.White, t)) g.DrawPath(pen, Rounded(new(x + t / 2, y + t / 2, w - t, h - t), h * 0.2f));
        g.FillRectangle(Brushes.White, x + w + u * 0.01f, y + h * 0.3f, u * 0.04f, h * 0.4f);
        float level = kind switch { Kind.Critical => 0.12f, Kind.Low => 0.28f, _ => 1f };
        float pad = t * 1.7f;
        g.FillPath(Brushes.White, Rounded(new(x + pad, y + pad, (w - pad * 2) * level, h - pad * 2), h * 0.08f));
        if (kind == Kind.Full) Bolt(g, new(x + w * 0.32f, y + h * 0.1f, w * 0.36f, h * 0.8f), c, true);
    });

    // ---------- helpers ----------
    static Bitmap Draw(int s, int ss, Action<Graphics> paint)
    {
        using var big = new Bitmap(s * ss, s * ss, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(big))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);
            paint(g);
        }
        if (ss == 1) return (Bitmap)big.Clone();
        var small = new Bitmap(s, s, PixelFormat.Format32bppPArgb);
        using var g2 = Graphics.FromImage(small);
        g2.InterpolationMode = InterpolationMode.HighQualityBicubic; g2.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g2.DrawImage(big, 0, 0, s, s);
        return small;
    }

    static GraphicsPath Rounded(RectangleF r, float rad)
    {
        rad = Math.Min(rad, Math.Min(r.Width, r.Height) / 2f); float d = rad * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static void Bolt(Graphics g, RectangleF r, Color c, bool outline = false)
    {
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);
        var pts = new[] { P(.62f, 0), P(.12f, .56f), P(.46f, .56f), P(.34f, 1), P(.9f, .38f), P(.54f, .38f) };
        using var b = new SolidBrush(c);
        g.FillPolygon(b, pts);
    }

    static Color Lighten(Color c, float f) => Color.FromArgb(c.R + (int)((255 - c.R) * f), c.G + (int)((255 - c.G) * f), c.B + (int)((255 - c.B) * f));

    /// <summary>Multi-size .ico with PNG frames (Vista+).</summary>
    public static void WriteIco(string path, int[] sizes, Func<int, Bitmap> render)
    {
        var frames = sizes.Select(sz => { using var b = render(sz); using var ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); return ms.ToArray(); }).ToArray();
        using var w = new BinaryWriter(File.Create(path));
        w.Write((short)0); w.Write((short)1); w.Write((short)frames.Length);
        int off = 6 + 16 * frames.Length;
        for (int i = 0; i < frames.Length; i++)
        {
            int sz = sizes[i];
            w.Write((byte)(sz >= 256 ? 0 : sz)); w.Write((byte)(sz >= 256 ? 0 : sz)); w.Write((byte)0); w.Write((byte)0);
            w.Write((short)1); w.Write((short)32); w.Write(frames[i].Length); w.Write(off);
            off += frames[i].Length;
        }
        foreach (var f in frames) w.Write(f);
    }
}
