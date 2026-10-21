@echo off
title Restore Steam's Play button
cd /d "%~dp0"
echo.
echo   Puts Risk of Rain 2's Play button in Steam back to normal (it stops opening the launcher)
echo   and removes the desktop shortcut. Your mods stay installed. Close Steam first.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -ClearLaunchOptions
echo.
pause
