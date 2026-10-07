@echo off
taskkill /im MxBattery.exe /f >nul 2>&1
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v MxBattery /f >nul 2>&1
reg delete "HKCU\Software\Classes\AppUserModelId\%LOCALAPPDATA:\=/%/Programs/MxBattery/MxBattery.exe" /f >nul 2>&1
powershell -NoProfile -Command "Remove-Item ([Environment]::GetFolderPath('Programs')+'\MX Battery.lnk') -ErrorAction SilentlyContinue"
echo MX Battery removed. Settings are kept in %%APPDATA%%\MxBattery (delete that folder to remove them).
rem this script lives in the install folder, so a detached shell deletes the folder after we exit
start "" /min powershell -NoProfile -Command "Start-Sleep 2; Remove-Item -Recurse -Force '%~dp0.'"
