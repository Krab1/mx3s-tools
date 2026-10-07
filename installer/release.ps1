# Build a release package: .\installer\release.ps1 -Version 1.0.2 [-Publish]
# Produces dist\MxBattery-<v>.zip and dist\MxBattery-<v>.zip.sha256 (the two assets the in-app updater looks for).
# With -Publish (needs the GitHub CLI `gh`, logged in) it also tags v<v> and creates the GitHub release.
param([Parameter(Mandatory)][string]$Version, [switch]$Publish)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot)

(Get-Content MxBattery.csproj -Raw) -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>" | Set-Content MxBattery.csproj -NoNewline
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if ($LASTEXITCODE) { throw 'publish failed' }

$pkg = "dist\MxBattery"; $zip = "dist\MxBattery-$Version.zip"
Remove-Item dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $pkg | Out-Null
Copy-Item publish\MxBattery.exe, installer\install.cmd, installer\uninstall.cmd $pkg
'Run install.cmd to install (per-user, no admin). Run uninstall.cmd from %LOCALAPPDATA%\Programs\MxBattery to remove. Or just run MxBattery.exe without installing.' | Set-Content "$pkg\README.txt"
Compress-Archive $pkg $zip
"$((Get-FileHash $zip -Algorithm SHA256).Hash.ToLower())  MxBattery-$Version.zip" | Set-Content "$zip.sha256"

$p = Start-Process "$pkg\MxBattery.exe" --selftest -Wait -PassThru
if ($p.ExitCode) { throw 'selftest failed' }
"Built $zip ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB), selftest OK"

if ($Publish) {
    gh release create "v$Version" $zip "$zip.sha256" --title "MX Battery $Version" --generate-notes
} else {
    "Next: commit, push, then create a GitHub release tagged v$Version and upload both files in dist\ (or rerun with -Publish once `gh` is installed)."
}
