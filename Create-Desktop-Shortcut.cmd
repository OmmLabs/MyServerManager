@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Install-DesktopShortcut.ps1"
if errorlevel 1 pause
