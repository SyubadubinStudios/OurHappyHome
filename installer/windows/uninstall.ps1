<#
  Our Happy Home - removes the per-user installation.
  Saves and album photos in %APPDATA%\OurHappyHome are kept unless -RemoveSaves is given.
#>
param([switch]$RemoveSaves)
$ErrorActionPreference = "SilentlyContinue"
$target = Join-Path $env:LOCALAPPDATA "Programs\OurHappyHome"
Get-Process OurHappyHome | Stop-Process -Force
Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Our Happy Home.lnk")
Remove-Item (Join-Path ([Environment]::GetFolderPath("Desktop")) "Our Happy Home.lnk")
Remove-Item -Recurse "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\OurHappyHome"
if ($RemoveSaves) {
    Remove-Item -Recurse -Force (Join-Path $env:APPDATA "OurHappyHome")
}

# This script lives inside the folder it deletes: finish from a detached shell.
Start-Process powershell -WindowStyle Hidden -ArgumentList "-NoProfile -Command Start-Sleep 1; Remove-Item -Recurse -Force '$target'"
Write-Host "Our Happy Home was uninstalled." -ForegroundColor Green
