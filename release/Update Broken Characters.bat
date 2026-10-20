@echo off
title Update the Broken Characters Collection
rem Downloads the newest Broken Characters Collection from GitHub and installs it. Close Risk of Rain 2 first.
set "REPO=a6407805-alt/DuelistMod"
tasklist /FI "IMAGENAME eq Risk of Rain 2.exe" 2>NUL | find /I "Risk of Rain 2.exe" >NUL
if not errorlevel 1 (
    echo Risk of Rain 2 is running. Close it, then press any key.
    pause >NUL
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop'; $t=Join-Path $env:TEMP 'BrokenCharactersUpdate'; if (Test-Path $t) { Remove-Item $t -Recurse -Force }; New-Item -ItemType Directory $t | Out-Null; Write-Host 'Downloading the latest Broken Characters Collection...' -ForegroundColor Yellow; [Net.ServicePointManager]::SecurityProtocol='Tls12'; Invoke-WebRequest -UseBasicParsing 'https://github.com/%REPO%/releases/latest/download/Broken-Characters-Collection.zip' -OutFile (Join-Path $t 'd.zip'); Expand-Archive (Join-Path $t 'd.zip') $t -Force; & (Join-Path $t 'install.ps1')"
echo.
pause
