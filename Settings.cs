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
    public string SkippedVersion { get; set; } = "";
    public IconStyle IconStyle { get; set; } = IconStyle.Mouse;
    public Preferred Preferred { get; set; } = Preferred.Auto;
    public string DeviceFilter { get; set; } = "MX Master 3S";

    static readonly string PathName = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MxBattery", "settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathName), Json) ?? new(); }
        catch { return new(); }                              // missing or corrupt file: defaults
    }

    public static bool NoPersist;                           // mock mode: never write settings or the autostart key

    public void Save()
    {
        if (NoPersist) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(this, Json));
        SetAutostart(StartWithWindows);
    }

    public static void SetAutostart(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
        if (on) k?.SetValue("MxBattery", $"\"{Environment.ProcessPath}\"");
        else k?.DeleteValue("MxBattery", false);
    }
}
