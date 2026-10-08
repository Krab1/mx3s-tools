using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MxBattery;

public enum IconStyle { Mouse, Number, Glyph }

public sealed class Settings
{
    public int LowPercent { get; set; } = 30;
    public int CriticalPercent { get; set; } = 10;
    public int Hysteresis { get; set; } = 5;
    public int GreenFrom { get; set; } = 70;               // icon/popup colour: green at or above this
    public int YellowFrom { get; set; } = 40;               // yellow at or above this, red below
    public int PollMinutes { get; set; } = 5;
    public int ReminderMinutes { get; set; } = 0;          // 0 = off
    public bool NotifyFull { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public int Schema { get; set; }                        // settings-file version; <2 files get the new default icon style once
    public string SkippedVersion { get; set; } = "";
    public IconStyle IconStyle { get; set; } = IconStyle.Mouse;
    public Preferred Preferred { get; set; } = Preferred.Auto;
    public string DeviceFilter { get; set; } = "MX Master 3S";

    static readonly string PathName = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MxBattery", "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Settings Load()
    {
        try
        {
            return Migrate(JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName), Json) ?? new());
        }
        catch { return new() { Schema = 2 }; }               // missing or corrupt file: defaults
    }

    public static bool NoPersist;                           // mock mode: never write settings or the autostart key

    /// <summary>Files written before 1.0.3 say IconStyle=Number, which was only the old default, so move them to the new one once.</summary>
    public static Settings Migrate(Settings s)
    {
        if (s.Schema < 2) { s.IconStyle = IconStyle.Mouse; s.Schema = 2; }
        return s;
    }

    public void Save()
    {
        if (NoPersist) return;
        Schema = 2;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(this, Json));
        SetAutostart(StartWithWindows);
    }

    /// <summary>Make sure Start menu has "MX Battery" pointing at this exe, so a closed app can be relaunched from Search/Start.</summary>
    public static void EnsureStartMenuShortcut()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null) return;
            var lnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MX Battery.lnk");
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
            dynamic s = shell.CreateShortcut(lnk);                               // loads the existing one if present
            if (string.Equals((string)s.TargetPath, exe, StringComparison.OrdinalIgnoreCase)) return;
            s.TargetPath = exe; s.WorkingDirectory = System.IO.Path.GetDirectoryName(exe); s.Description = "MX Master battery monitor";
            s.Save();
        }
        catch { }                                                                // cosmetic: never block startup
    }

    public static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (on) k?.SetValue("MxBattery", $"\"{Environment.ProcessPath}\"");
        else k?.DeleteValue("MxBattery", false);
    }
}
