using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace MxBattery;

public enum IconStyle { Number, Glyph }

public sealed class Settings
{
    public int LowPercent { get; set; } = 30;
    public int CriticalPercent { get; set; } = 10;
    public int Hysteresis { get; set; } = 5;
    public int PollMinutes { get; set; } = 5;
    public int ReminderMinutes { get; set; } = 0;          // 0 = off
    public bool NotifyFull { get; set; } = false;
    public bool StartWithWindows { get; set; } = true;
    public IconStyle IconStyle { get; set; } = IconStyle.Number;
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

    public void Save()
    {
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
