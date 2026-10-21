@echo off
title Add the friends-only extras
rem Asks for the extras key again (if you skipped it, or got a new one), then reinstalls with the extras.
if exist "%APPDATA%\BrokenCharacters\extras-skip.txt" del "%APPDATA%\BrokenCharacters\extras-skip.txt"
if exist "%APPDATA%\BrokenCharacters\extras-key.txt" del "%APPDATA%\BrokenCharacters\extras-key.txt"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
echo.
pause
