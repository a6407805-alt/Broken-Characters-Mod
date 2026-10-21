@echo off
title Update the Broken Characters Mod
rem Downloads the newest pack from GitHub and installs it. Close Risk of Rain 2 first.
tasklist /FI "IMAGENAME eq Risk of Rain 2.exe" 2>NUL | find /I "Risk of Rain 2.exe" >NUL
if not errorlevel 1 (
    echo Risk of Rain 2 is running. Close it, then press any key.
    pause >NUL
)
if not exist "%~dp0update.ps1" (
    echo update.ps1 is missing: extract the whole zip, then run this from the extracted folder.
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0update.ps1"
echo.
pause
