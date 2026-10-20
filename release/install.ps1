# Duelist pack installer - no mod manager needed.
# Finds Risk of Rain 2 (Steam), installs BepInEx and the shared libraries straight into the game folder
# (downloaded from Thunderstore, pinned to the exact versions we play with), then the Duelist and the
# group's other mods. Re-running it only fetches what is missing or out of date.
param([switch]$Uninstall, [string]$GameDir)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'      # makes Invoke-WebRequest much faster
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$modSrc = Join-Path $here 'Duelist'
$extraSrc = Join-Path $here 'Mods'
$modName = 'Duelist'

# Everyone gets exactly these versions, so the whole lobby matches.
$BepInExPack = @{ Ns = 'bbepis'; Name = 'BepInExPack'; Ver = '5.4.2122' }
$Libraries = @(
    @{ Ns = 'RiskofThunder'; Name = 'RoR2BepInExPack';           Ver = '1.45.0' },
    @{ Ns = 'RiskofThunder'; Name = 'HookGenPatcher';            Ver = '1.2.9'  },
    @{ Ns = 'RiskofThunder'; Name = 'FixPluginTypesSerialization'; Ver = '1.1.0' },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_Core';                Ver = '5.3.0'  },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_ContentManagement';   Ver = '1.0.11' },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_Language';            Ver = '1.1.0'  },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_Networking';          Ver = '1.0.4'  },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_Prefab';              Ver = '1.1.1'  },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_Items';               Ver = '1.0.8'  },
    @{ Ns = 'RiskofThunder'; Name = 'R2API_RecalculateStats';    Ver = '1.6.6'  },
    @{ Ns = 'score';         Name = 'MiscFixes';                 Ver = '1.6.1'  },
    @{ Ns = 'pseudopulse';   Name = 'SeekersPatcher';            Ver = '1.4.3'  }
)

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
    return @($found | Select-Object -Unique)
}

$tmp = Join-Path $env:TEMP 'DuelistDeps'
function Get-Package($pkg) {
    # downloads one Thunderstore package and returns the folder it was extracted to
    $id = "$($pkg.Ns)-$($pkg.Name)-$($pkg.Ver)"
    $zip = Join-Path $tmp "$id.zip"
    $dir = Join-Path $tmp $id
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $tmp | Out-Null
        Say "    downloading $id ..." 'DarkGray'
        Invoke-WebRequest -UseBasicParsing "https://thunderstore.io/package/download/$($pkg.Ns)/$($pkg.Name)/$($pkg.Ver)/" -OutFile $zip
        Expand-Archive $zip $dir -Force
    }
    return $dir
}

function Installed-Version($folder) {
    $m = Join-Path $folder 'manifest.json'
    if (-not (Test-Path $m)) { return $null }
    try { return (Get-Content $m -Raw | ConvertFrom-Json).version_number } catch { return $null }
}

function Install-BepInEx($game) {
    $core = Join-Path $game 'BepInEx\core\BepInEx.dll'
    if ((Test-Path $core) -and (Test-Path (Join-Path $game 'winhttp.dll'))) { return }
    Say '  Setting up BepInEx (the mod loader)...' 'Yellow'
    $dir = Get-Package $BepInExPack
    $inner = Join-Path $dir 'BepInExPack'
    if (-not (Test-Path $inner)) { $inner = $dir }
    Copy-Item (Join-Path $inner '*') $game -Recurse -Force
}

function Place-Items($game, $from, $plug, $patch) {
    foreach ($item in Get-ChildItem $from) {
        switch ($item.Name.ToLower()) {
            'bepinex'  { if ($item.PSIsContainer) { Place-Items $game $item.FullName $plug $patch } else { Copy-Item $item.FullName $plug -Force } }
            'plugins'  { Copy-Item (Join-Path $item.FullName '*') $plug -Recurse -Force }
            'patchers' { New-Item -ItemType Directory -Force -Path $patch | Out-Null; Copy-Item (Join-Path $item.FullName '*') $patch -Recurse -Force }
            'config'   { $c = Join-Path $game 'BepInEx\config'; New-Item -ItemType Directory -Force -Path $c | Out-Null
                         Get-ChildItem $item.FullName | ForEach-Object { $dst = Join-Path $c $_.Name; if (-not (Test-Path $dst)) { Copy-Item $_.FullName $dst -Recurse } } }
            'core'     { Copy-Item (Join-Path $item.FullName '*') (Join-Path $game 'BepInEx\core') -Recurse -Force }
            default    { Copy-Item $item.FullName $plug -Recurse -Force }
        }
    }
}

function Install-Library($game, $pkg) {
    # same layout a mod manager uses: plugins\<Ns-Name>, patchers\<Ns-Name>, config merged
    $full = "$($pkg.Ns)-$($pkg.Name)"
    $plug = Join-Path $game "BepInEx\plugins\$full"
    $patch = Join-Path $game "BepInEx\patchers\$full"
    if ((Installed-Version $plug) -eq $pkg.Ver) { return $false }
    $dir = Get-Package $pkg
    foreach ($d in $plug, $patch) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
    New-Item -ItemType Directory -Force -Path $plug | Out-Null
    Place-Items $game $dir $plug $patch
    return $true
}

Say ''
Say '  ====================================' 'Yellow'
Say ('  DUELIST PACK ' + $(if ($Uninstall) { 'UNINSTALLER' } else { 'INSTALLER' })) 'Yellow'
Say '  ====================================' 'Yellow'
Say ''

if (-not $Uninstall -and -not (Test-Path (Join-Path $modSrc 'DuelistMod.dll'))) {
    Say 'The Duelist files are missing. Extract the whole zip first (right-click > Extract All), then run INSTALL.bat from the extracted folder.' 'Red'
    exit 1
}
if (Get-Process -Name 'Risk of Rain 2' -ErrorAction SilentlyContinue) {
    Say 'Risk of Rain 2 is running. Close it, then run this again.' 'Red'
    exit 1
}

$games = if ($GameDir) { @($GameDir) } else { Find-GameFolders }
if ($games.Count -eq 0) {
    Say "Couldn't find Risk of Rain 2 automatically. Pick your Risk of Rain 2 folder (the one with 'Risk of Rain 2.exe')." 'Yellow'
    Add-Type -AssemblyName System.Windows.Forms
    $dlg = New-Object System.Windows.Forms.FolderBrowserDialog
    $dlg.Description = 'Select your Risk of Rain 2 folder'
    if ($dlg.ShowDialog() -eq 'OK' -and (Test-Path (Join-Path $dlg.SelectedPath 'Risk of Rain 2.exe'))) { $games = @($dlg.SelectedPath) }
    else { Say "Cancelled (or that folder doesn't have Risk of Rain 2.exe)." 'Red'; exit 1 }
}

$done = 0
foreach ($game in $games) {
    Say "  Game folder: $game" 'Cyan'
    $plugins = Join-Path $game 'BepInEx\plugins'
    if ($Uninstall) {
        $dest = Join-Path $plugins $modName
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force; Say '  Removed the Duelist.' 'Green'; $done++ }
        Say "  (To go back to the plain game, use Steam > Risk of Rain 2 > Properties > Installed Files > Verify,"
        Say "   or delete winhttp.dll from the game folder. That switches BepInEx and every mod off.)"
        continue
    }
    try {
        Install-BepInEx $game
        New-Item -ItemType Directory -Force -Path $plugins | Out-Null
        $fresh = @()
        foreach ($lib in $Libraries) { if (Install-Library $game $lib) { $fresh += $lib.Name } }
        if ($fresh.Count -gt 0) { Say "  Libraries installed/updated: $($fresh -join ', ')" 'Green' }
        else { Say '  Libraries already up to date.' 'Green' }
    } catch {
        Say "  Couldn't download the libraries from thunderstore.io: $($_.Exception.Message)" 'Red'
        Say '  Check your internet connection and run this again.' 'Red'
        continue
    }

    $dest = Join-Path $plugins $modName
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    # only one DuelistMod.dll may live under plugins: switch off any other copy (an old build, a dev folder)
    Get-ChildItem $plugins -Recurse -Filter 'DuelistMod.dll' -ErrorAction SilentlyContinue | ForEach-Object {
        Rename-Item $_.FullName ($_.Name + '.disabled') -Force
        Say "    switched off an extra copy: $($_.FullName)" 'DarkYellow'
    }
    Copy-Item $modSrc $dest -Recurse -Force
    Say '  Installed the Duelist.' 'Green'

    if (Test-Path $extraSrc) {
        $mods = Get-ChildItem $extraSrc -Directory
        foreach ($m in $mods) {
            $mDest = Join-Path $plugins $m.Name
            New-Item -ItemType Directory -Force -Path $mDest | Out-Null
            Copy-Item (Join-Path $m.FullName '*') $mDest -Recurse -Force    # copy over the top, never wipe
        }
        Say "  Installed $($mods.Count) group mods: $(($mods | ForEach-Object Name) -join ', ')" 'Green'
    }
    $done++
}

Say ''
if ($done -gt 0) {
    if ($Uninstall) { Say 'Done.' 'Green' }
    else {
        Say 'Done! Launch Risk of Rain 2 from Steam like normal - mods load automatically.' 'Green'
        Say 'The first launch takes a minute longer while it sets things up.' 'Green'
        Say 'Multiplayer: everyone in the lobby needs the same version, so update together.' 'Green'
    }
} else {
    Say $(if ($Uninstall) { 'Nothing to remove.' } else { 'Nothing was installed (see the messages above).' }) 'Red'
}
