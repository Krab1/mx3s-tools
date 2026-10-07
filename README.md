# MxBattery

Windows tray app that shows the battery level of a Logitech MX Master 3S and warns when it is low.

- **Sources:** Logi Bolt receiver (HID++ 2.0, exact % + charging) or Bluetooth (battery property Windows publishes). Auto mode tries Bolt first, then Bluetooth. Unifying receivers are not supported.
- **Tray:** left click = status popup, right click = menu (refresh, settings, pause notifications, exit).
- **Alerts:** one toast per crossing of the low (30%) and critical (10%) thresholds, re-armed by hysteresis or charging. All options are in Settings (`%AppData%\MxBattery\settings.json`).
- **Not a Windows Service:** services have no desktop, so this is a tray app that starts at login (Run key, toggle in Settings).

## Build / run

```
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Diagnostics: `MxBattery.exe --selftest` (logic checks, exit code 0 = pass), `--probe out.txt` (trace one read of each source),
`--icons dir` (render the icon preview sheet and `app.ico`).

Status: Bolt and Bluetooth reads verified on an MX Master 3S. The generated icons (`Icons.cs`) are not yet wired into the tray.
