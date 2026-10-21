@echo off
title Uninstall the Broken Characters Mod
cd /d "%~dp0"
if not exist "%~dp0install.ps1" (
  echo.
  echo   This is running from INSIDE the zip, so the installer can't see its files.
  echo   Close this, right-click Broken-Characters-Mod.zip ^> Extract All, open the new folder
  echo   and double-click INSTALL.bat in there.
  echo.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -Uninstall
echo.
echo   (A log of this run was saved as install_log.txt in this folder.)
pause
