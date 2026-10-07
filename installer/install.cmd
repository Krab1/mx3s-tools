@echo off
setlocal
set "DEST=%LOCALAPPDATA%\Programs\MxBattery"
echo Installing MX Battery to %DEST%
taskkill /im MxBattery.exe /f >nul 2>&1
mkdir "%DEST%" 2>nul
copy /y "%~dp0MxBattery.exe" "%DEST%\MxBattery.exe" >nul || (echo Copy failed & pause & exit /b 1)
copy /y "%~dp0uninstall.cmd" "%DEST%\uninstall.cmd" >nul
powershell -NoProfile -Command "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Programs')+'\MX Battery.lnk'); $s.TargetPath='%DEST%\MxBattery.exe'; $s.WorkingDirectory='%DEST%'; $s.Save()"
start "" "%DEST%\MxBattery.exe"
echo Done. MX Battery is running in the tray and will start with Windows (change in Settings).
timeout /t 4 >nul
