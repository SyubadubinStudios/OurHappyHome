<#
  Our Happy Home - per-user installer for the portable package.
  Copies the game to %LOCALAPPDATA%\Programs\OurHappyHome, adds Start menu and desktop
  shortcuts and an "Apps & features" entry. No administrator rights are needed.
#>
param([switch]$NoDesktopShortcut)
$ErrorActionPreference = "Stop"
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path $env:LOCALAPPDATA "Programs\OurHappyHome"
$exe = Join-Path $target "OurHappyHome.exe"

Write-Host "Installing Our Happy Home to $target ..." -ForegroundColor Cyan
Get-Process OurHappyHome -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $target) { Remove-Item -Recurse -Force $target }
New-Item -ItemType Directory -Force $target | Out-Null
Get-ChildItem $source | Where-Object { $_.Name -notin @("install.ps1", "install.cmd") } | Copy-Item -Destination $target -Recurse

$shell = New-Object -ComObject WScript.Shell
function New-Shortcut([string]$path) {
    $s = $shell.CreateShortcut($path)
    $s.TargetPath = $exe
    $s.WorkingDirectory = $target
    $s.IconLocation = "$exe,0"
    $s.Description = "Our Happy Home - Syubadubin Studios"
    $s.Save()
}

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
New-Shortcut (Join-Path $startMenu "Our Happy Home.lnk")
if (-not $NoDesktopShortcut) {
    New-Shortcut (Join-Path ([Environment]::GetFolderPath("Desktop")) "Our Happy Home.lnk")
}

$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\OurHappyHome"
New-Item -Force $key | Out-Null
Set-ItemProperty $key DisplayName "Our Happy Home"
Set-ItemProperty $key Publisher "Syubadubin Studios"
Set-ItemProperty $key DisplayIcon $exe
Set-ItemProperty $key InstallLocation $target
Set-ItemProperty $key DisplayVersion ((Get-Item $exe).VersionInfo.ProductVersion)
Set-ItemProperty $key UninstallString "powershell -NoProfile -ExecutionPolicy Bypass -File `"$target\uninstall.ps1`""
Set-ItemProperty $key NoModify 1
Set-ItemProperty $key NoRepair 1

Write-Host "Done! Start 'Our Happy Home' from the Start menu or the desktop." -ForegroundColor Green
