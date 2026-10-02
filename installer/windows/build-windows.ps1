<#
  Builds the Windows packages of Our Happy Home:
    artifacts/OurHappyHome-<version>-win-x64-portable.zip   (unzip & run, or run install.cmd)
    artifacts/OurHappyHome-<version>-win-x64-setup.exe       (when Inno Setup 6 is installed)
#>
param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = (Resolve-Path "$here/../..").Path
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish/win-x64"
New-Item -ItemType Directory -Force $artifacts | Out-Null
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }

Write-Host "Publishing Our Happy Home $Version for win-x64..." -ForegroundColor Cyan
dotnet publish "$root/src/OurHappyHome" -c Release -r win-x64 --self-contained `
    -p:Version=$Version -p:DebugType=None -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Get-ChildItem $publish -Filter *.pdb | Remove-Item -Force

# Portable zip with a small per-user installer.
$stagingRoot = Join-Path $artifacts "staging-win"
$staging = Join-Path $stagingRoot "OurHappyHome"
if (Test-Path $stagingRoot) { Remove-Item -Recurse -Force $stagingRoot }
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item "$publish/*" $staging -Recurse
Copy-Item "$here/install.ps1", "$here/uninstall.ps1", "$here/install.cmd" $staging
Copy-Item "$root/README.md" $staging
$zip = Join-Path $artifacts "OurHappyHome-$Version-win-x64-portable.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $staging -DestinationPath $zip
Remove-Item -Recurse -Force $stagingRoot
Write-Host "Portable package: $zip" -ForegroundColor Green

# Optional: classic setup.exe with Inno Setup 6.
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    & $iscc "/DAppVersion=$Version" "/DSourceDir=$publish" "/DOutputDir=$artifacts" "/DRootDir=$root" "$here/OurHappyHome.iss"
    Write-Host "Setup: $artifacts/OurHappyHome-$Version-win-x64-setup.exe" -ForegroundColor Green
}
else {
    Write-Host "Inno Setup 6 not found: skipped setup.exe (https://jrsoftware.org/isinfo.php)" -ForegroundColor Yellow
}
