using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace MxBattery;

/// <summary>Design review: MxBattery.exe --icons outdir  renders a preview sheet plus the .ico/.png assets.</summary>
static class IconSheet
{
    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var cfg = new Settings();
        Icons.WriteIco(Path.Combine(dir, "app.ico"), [16, 24, 32, 48, 64, 128, 256], s => Icons.App(s));

        const int W = 1180, H = 1160;
        using var sheet = new Bitmap(W, H);
        using var g = Graphics.FromImage(sheet);
        g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Color.FromArgb(32, 32, 32));
        using var h1 = new Font("Segoe UI Semibold", 15); using var small = new Font("Segoe UI", 9.5f);
        void Text(string t, float x, float y, Font f, Color c) { using var b = new SolidBrush(c); g.DrawString(t, f, b, x, y); }
        void Draw(Bitmap bmp, int x, int y, int w, int h, bool smooth)
        {
            g.InterpolationMode = smooth ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = smooth ? PixelOffsetMode.HighQuality : PixelOffsetMode.Half;
            g.DrawImage(bmp, new Rectangle(x, y, w, h));
        }

        // 1. App icon on light and dark surfaces
        Text("App icon  (taskbar, exe, alt-tab, installer)", 24, 14, h1, Color.White);
        int[] sizes = [192, 96, 64, 48, 32, 16];
        foreach (var (bg, x0, fg) in new[] { (Color.FromArgb(243, 243, 243), 24, Color.FromArgb(60, 60, 60)), (Color.FromArgb(28, 28, 28), 596, Color.FromArgb(200, 200, 200)) })
        {
            using (var b = new SolidBrush(bg)) g.FillRectangle(b, x0, 50, 560, 250);
            int x = x0 + 16;
            foreach (var s in sizes)
            {
                using var bmp = Icons.App(s);
                Draw(bmp, x, 50 + 16 + (192 - s) / 2, s, s, true);
                Text(s.ToString(), x, 50 + 16 + 192 + 8, small, fg);
                x += s + 16;
            }
        }

        // 2. Tray icons on taskbar strips: actual size (100%), 200%, and a 4x magnified view of the 16 px bitmap
        Text("Tray icon  (follows the Windows taskbar theme; actual size, 200%, 4x zoom of the 16 px bitmap)", 24, 322, h1, Color.White);
        (string label, int? pct, bool chg)[] states = [("90%", 90, false), ("25% low", 25, false), ("8% critical", 8, false), ("72% charging", 72, true), ("100%", 100, false), ("not connected", null, false)];
        int row = 0;
        foreach (var style in new[] { IconStyle.Number, IconStyle.Glyph })
            foreach (var light in new[] { false, true })
            {
                int y = 362 + row++ * 112;
                var bg = light ? Color.FromArgb(243, 243, 243) : Color.FromArgb(32, 32, 32);
                var fgText = light ? Color.FromArgb(60, 60, 60) : Color.FromArgb(200, 200, 200);
                using (var b = new SolidBrush(bg)) g.FillRectangle(b, 24, y, 1132, 100);
                using (var p = new Pen(light ? Color.FromArgb(215, 215, 215) : Color.FromArgb(75, 75, 75))) g.DrawRectangle(p, 24, y, 1132, 100);
                Text($"{style}  ·  {(light ? "light" : "dark")} taskbar", 36, y + 6, small, fgText);
                int x = 40;
                foreach (var (label, pct, chg) in states)
                {
                    using var a = Icons.Tray(16, pct, chg, light, style, cfg);
                    using var z = Icons.Tray(32, pct, chg, light, style, cfg);
                    Draw(a, x, y + 40, 16, 16, false);
                    Draw(z, x + 30, y + 32, 32, 32, false);
                    Draw(a, x + 76, y + 26, 64, 64, false);
                    Text(label, x, y + 78, small, fgText);
                    x += 184;
                }
            }

        // 3. Notification badges inside toast mock-ups (dark row, light row)
        int ty = 362 + 4 * 112 + 12;
        Text("Notification icon  (toast: low / critical / fully charged)", 24, ty, h1, Color.White);
        (Kind k, string title, string body)[] toasts = [(Kind.Low, "Mouse battery low", "MX Master 3S is at 28%."), (Kind.Critical, "Mouse battery critical", "8% left — charge now."), (Kind.Full, "Mouse fully charged", "MX Master 3S is at 100%.")];
        int r2 = 0;
        foreach (var light in new[] { false, true })
        {
            int cy = ty + 40 + r2++ * 88, cx = 24;
            using var strip = new SolidBrush(light ? Color.FromArgb(230, 230, 230) : Color.FromArgb(20, 20, 20));
            g.FillRectangle(strip, 24, cy - 6, 1132, 82);
            foreach (var (k, title, body) in toasts)
            {
                var card = light ? Color.FromArgb(251, 251, 251) : Color.FromArgb(44, 44, 44);
                var fg = light ? Color.FromArgb(26, 26, 26) : Color.White;
                var fg2 = light ? Color.FromArgb(96, 96, 96) : Color.FromArgb(190, 190, 190);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (var b = new SolidBrush(card)) g.FillPath(b, RoundedRect(cx + 8, cy, 352, 70, 8));
                using (var p = new Pen(light ? Color.FromArgb(220, 220, 220) : Color.FromArgb(70, 70, 70))) g.DrawPath(p, RoundedRect(cx + 8, cy, 352, 70, 8));
                using var badge = Icons.Badge(48, k);
                Draw(badge, cx + 20, cy + 11, 48, 48, true);
                using var tf = new Font("Segoe UI Semibold", 10.5f); using var bf = new Font("Segoe UI", 9.5f);
                Text(title, cx + 80, cy + 13, tf, fg); Text(body, cx + 80, cy + 37, bf, fg2);
                cx += 376;
            }
        }

        sheet.Save(Path.Combine(dir, "preview.png"), ImageFormat.Png);
        using (var b = Icons.App(256)) b.Save(Path.Combine(dir, "app-256.png"), ImageFormat.Png);
        return 0;
    }

    static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath(); float d = r * 2;
        p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90); p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}
