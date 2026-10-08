using System.Text;
using HidSharp;

namespace MxBattery;

/// <summary>One-shot report for debugging "Not connected": each source read once, with the raw HID++ trace.</summary>
static class Diagnostics
{
    public static string Run(Settings cfg)
    {
        var sw = new StringWriter();
        var w = TextWriter.Synchronized(sw);
        w.WriteLine($"MxBattery {Updater.Current}  {Environment.OSVersion}  {DateTime.Now}");
        w.WriteLine($"filter=\"{cfg.DeviceFilter}\" preferred={cfg.Preferred} iconStyle={cfg.IconStyle}");

        w.WriteLine("-- Logitech HID devices");
        try
        {
            foreach (var d in DeviceList.Local.GetHidDevices(0x046D))
                w.WriteLine($"PID_{d.ProductID:X4} in={d.GetMaxInputReportLength()} out={d.GetMaxOutputReportLength()} {d.DevicePath}");
        }
        catch (Exception e) { w.WriteLine("enumeration failed: " + e.Message); }

        var old = HidppSource.Log;
        HidppSource.Log = w.WriteLine;
        try
        {
            foreach (IBatterySource s in new IBatterySource[] { new HidppSource(() => cfg.DeviceFilter), new BluetoothSource(() => cfg.DeviceFilter) })
            {
                w.WriteLine($"== {s.GetType().Name}");
                try
                {
                    var r = s.ReadAsync(default).GetAwaiter().GetResult();
                    w.WriteLine("result: " + (r?.ToString() ?? "null"));
                    w.WriteLine("issue: " + (s.Issue ?? "-"));
                }
                catch (Exception e) { w.WriteLine("EXCEPTION " + e); }
            }
        }
        finally { HidppSource.Log = old; }
        return sw.ToString();
    }
}
