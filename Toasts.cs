using System.Drawing.Imaging;
using Microsoft.Toolkit.Uwp.Notifications;

namespace MxBattery;

/// <summary>Native Windows toasts carrying the generated notification badge.</summary>
static class Toasts
{
    const string ArtVersion = "v1";    // bump when the badge artwork changes so the cached PNGs are regenerated

    static string BadgeFile(Kind k)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MxBattery");
        var path = Path.Combine(dir, $"badge-{ArtVersion}-{k}.png");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(dir);
            using var b = Icons.Badge(96, k);
            b.Save(path, ImageFormat.Png);
        }
        return path;
    }

    public static void Show(Kind kind, string title, string body) =>
        new ToastContentBuilder()
            .AddAppLogoOverride(new Uri(BadgeFile(kind)), ToastGenericAppLogoCrop.None)
            .AddText(title)
            .AddText(body)
            .Show();
}
