# Duelist mod installer: finds Risk of Rain 2 (Steam) and any r2modman / Thunderstore / Gale profiles,
# and copies the Duelist into each one's BepInEx\plugins folder. Needs BepInEx + R2API already installed.
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$modSrc = Join-Path $here 'Duelist'
$modName = 'Duelist'

function Say($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }

function Find-GameFolders {
    $found = @()
    $steamRoots = @()
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty $key -ErrorAction Stop
            if ($p.SteamPath) { $steamRoots += $p.SteamPath }
            if ($p.InstallPath) { $steamRoots += $p.InstallPath }
        } catch { }
    }
    $libs = @()
    foreach ($s in ($steamRoots | Select-Object -Unique)) {
        $s = $s -replace '/', '\'
        $libs += $s
        $vdf = Join-Path $s 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in (Select-String -Path $vdf -Pattern '"path"\s+"([^"]+)"' -AllMatches)) {
                foreach ($g in $m.Matches) { $libs += ($g.Groups[1].Value -replace '\\\\', '\') }
            }
        }
    }
    foreach ($l in ($libs | Select-Object -Unique)) {
        $game = Join-Path $l 'steamapps\common\Risk of Rain 2'
        if (Test-Path (Join-Path $game 'Risk of Rain 2.exe')) { $found += $game }
    }
    return $found | Select-Object -Unique
}

function Find-Profiles {
    $roots = @(
        (Join-Path $env:APPDATA 'r2modmanPlus-local\RiskOfRain2\profiles'),
        (Join-Path $env:APPDATA 'Thunderstore Mod Manager\DataFolder\RiskOfRain2\profiles'),
        (Join-Path $env:APPDATA 'com.kesomannen.gale\riskofrain2\profiles')
    )
    $found = @()
    foreach ($r in $roots) {
        if (Test-Path $r) { Get-ChildItem $r -Directory | ForEach-Object { if (Test-Path (Join-Path $_.FullName 'BepInEx')) { $found += $_.FullName } } }
    }
    return $found
}

Say ''
Say '  ====================================' 'Yellow'
Say ('  DUELIST ' + $(if ($Uninstall) { 'UNINSTALLER' } else { 'INSTALLER' })) 'Yellow'
Say '  ====================================' 'Yellow'
Say ''

if (-not $Uninstall -and -not (Test-Path (Join-Path $modSrc 'DuelistMod.dll'))) {
    Say 'The Duelist files are missing. Extract the whole zip first (right-click > Extract All), then run INSTALL.bat from the extracted folder.' 'Red'
    exit 1
}

$targets = @()
foreach ($g in Find-GameFolders) { $targets += [pscustomobject]@{ Name = "Steam game folder ($g)"; Root = $g } }
foreach ($p in Find-Profiles) { $targets += [pscustomobject]@{ Name = "Mod manager profile '$(Split-Path $p -Leaf)'"; Root = $p } }

if ($targets.Count -eq 0) {
    Say "Couldn't find Risk of Rain 2 automatically. Pick your Risk of Rain 2 folder (the one with 'Risk of Rain 2.exe')." 'Yellow'
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Select your Risk of Rain 2 folder'
    if ($dlg.ShowDialog() -eq 'OK') { $targets += [pscustomobject]@{ Name = $dlg.SelectedPath; Root = $dlg.SelectedPath } }
    else { Say 'Cancelled.' 'Red'; exit 1 }
}

$done = 0
foreach ($t in $targets) {
    $bep = Join-Path $t.Root 'BepInEx'
    $plugins = Join-Path $bep 'plugins'
    $dest = Join-Path $plugins $modName
    if ($Uninstall) {
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force; Say "  Removed from $($t.Name)" 'Green'; $done++ }
        continue
    }
    if (-not (Test-Path (Join-Path $bep 'core'))) {
        Say "  Skipped $($t.Name): BepInEx isn't installed there." 'DarkYellow'
        continue
    }
    $hasR2 = Get-ChildItem $plugins -Recurse -Filter 'R2API.ContentManagement.dll' -ErrorAction SilentlyContinue | Select-Object -First 1
    New-Item -ItemType Directory -Force -Path $plugins | Out-Null
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }      # replace any older Duelist
    # only one DuelistMod.dll may live under plugins: switch off any other copy (an old build, a dev folder)
    Get-ChildItem $plugins -Recurse -Filter 'DuelistMod.dll' -ErrorAction SilentlyContinue | ForEach-Object {
        Rename-Item $_.FullName ($_.Name + '.disabled') -Force
        Say "    switched off an extra copy: $($_.FullName)" 'DarkYellow'
    }
    Copy-Item $modSrc $dest -Recurse -Force
    Say "  Installed into $($t.Name)" 'Green'
    if (-not $hasR2) { Say '    (warning: R2API not found there - the Duelist needs R2API to load)' 'DarkYellow' }
    $done++
}

Say ''
if ($done -gt 0) {
    if ($Uninstall) { Say 'Done. The Duelist is removed.' 'Green' }
    else { Say "Done! Start Risk of Rain 2 and pick the Duelist. (Everyone in a multiplayer lobby needs the same version.)" 'Green' }
} else {
    Say $(if ($Uninstall) { 'Nothing to remove.' } else { 'Nothing was installed. Install BepInEx + R2API first (r2modman makes that easy), then run this again.' }) 'Red'
}
