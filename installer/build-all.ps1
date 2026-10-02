<#
  Our Happy Home - builds release packages for every platform.
  Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

  Usage:  pwsh installer/build-all.ps1 [-Version 1.0.0] [-Platforms win,linux,mac]
  Output: artifacts/
#>
param(
    [string]$Version = "1.0.0",
    [string[]]$Platforms = @("win", "linux", "mac")
)
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

if ($Platforms -contains "win") {
    & "$here/windows/build-windows.ps1" -Version $Version
}

$bash = Get-Command bash -ErrorAction SilentlyContinue
$gitBash = Join-Path $env:ProgramFiles "Git/bin/bash.exe"
if (-not $bash -and (Test-Path $gitBash)) {
    Set-Alias bash $gitBash
    $bash = $true
}

if ($Platforms -contains "linux") {
    if ($bash) { bash "$here/linux/build-linux.sh" $Version } else { Write-Warning "bash not found: skipping Linux package" }
}

if ($Platforms -contains "mac") {
    if ($bash) { bash "$here/macos/build-macos.sh" $Version } else { Write-Warning "bash not found: skipping macOS package" }
}

Write-Host "Done. Packages are in $(Resolve-Path "$here/../artifacts")" -ForegroundColor Green
