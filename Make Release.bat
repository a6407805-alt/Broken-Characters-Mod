@echo off
title Make Duelist Release
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0release\make_release.ps1"
echo.
pause
