@echo off
rem Our Happy Home - double-click installer (per user, no administrator rights needed).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
pause
