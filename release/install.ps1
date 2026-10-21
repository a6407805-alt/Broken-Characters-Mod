# Broken Characters Mod installer - no mod manager needed.
# Finds Risk of Rain 2 (Steam), installs BepInEx and the shared libraries straight into the game folder
# (downloaded from Thunderstore, pinned to the exact versions we play with), then the Duelist and the
# group's other mods. Re-running it only fetches what is missing or out of date.
param([switch]$Uninstall, [string]$GameDir, [switch]$SkipExtras, [string]$ExtrasKey, [string]$LauncherPath, [switch]$SetSteamPlay, [switch]$UnsetSteamPlay, [switch]$SteamPlayOnly, [switch]$ClearLaunchOptions, [string]$SteamRoot)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'      # makes Invoke-WebRequest much faster
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
try { Start-Transcript (Join-Path $here 'install_log.txt') -Force | Out-Null } catch { }
# files downloaded from the internet are 'blocked' by Windows; unblock the pack so nothing silently refuses to run
try { Get-ChildItem $here -Recurse -File | Unblock-File -ErrorAction SilentlyContinue } catch { }
trap {
    Write-Host ''
    Write-Host "Something went wrong: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host 'Send install_log.txt (in this folder) to whoever gave you the pack.' -ForegroundColor Red
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}
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

$tmp = Join-Path $env:TEMP 'BrokenCharactersDeps'
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

# ------------------------------------------------------------------ friends-only extras
# Things we can't put in the public pack live in a private repo; an "extras key" (a read-only token for it)
# unlocks them. Asked for once, then remembered in %APPDATA%\BrokenCharacters.
$extrasRepo = 'a6407805-alt/BrokenCharacters-Extras'
$appDir = Join-Path $env:APPDATA 'BrokenCharacters'
$extrasKeyFile = Join-Path $appDir 'extras-key.txt'
$extrasSkipFile = Join-Path $appDir 'extras-skip.txt'

function Get-Extras {
    if ($SkipExtras) { return $null }
    $k = $ExtrasKey
    if (-not $k -and (Test-Path $extrasKeyFile)) { $k = (Get-Content $extrasKeyFile -Raw).Trim() }
    $asked = $false
    if (-not $k) {
        if (Test-Path $extrasSkipFile) { Say '  (Friends-only extras skipped. Run "Add Extras.bat" to add them.)' 'DarkGray'; return $null }
        Say ''
        Say '  FRIENDS-ONLY EXTRAS: paste your extras key (right-click to paste) and press Enter.' 'Cyan'
        Say '  No key? Just press Enter to skip; the pack works without them.' 'Cyan'
        $k = (Read-Host '  Extras key').Trim()
        $asked = $true
        if (-not $k) {
            New-Item -ItemType Directory -Force -Path $appDir | Out-Null
            Set-Content -Path $extrasSkipFile -Value 'skip'
            Say '  Skipped. Run "Add Extras.bat" any time to add them.' 'DarkGray'
            return $null
        }
    }
    try {
        $h = @{ Authorization = "Bearer $k"; 'User-Agent' = 'BrokenCharacters'; Accept = 'application/vnd.github+json' }
        $rel = Invoke-RestMethod "https://api.github.com/repos/$extrasRepo/releases/tags/latest" -Headers $h
        $a = $rel.assets | Where-Object { $_.name -eq 'Extras.zip' } | Select-Object -First 1
        if (-not $a) { throw 'no Extras.zip in the latest extras release' }
        $dir = Join-Path $env:TEMP 'BrokenCharactersExtras'
        if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $zipPath = Join-Path $dir 'Extras.zip'
        $req = [System.Net.HttpWebRequest]::Create($a.url)
        $req.AllowAutoRedirect = $false; $req.UserAgent = 'BrokenCharacters'; $req.Accept = 'application/octet-stream'
        $req.Headers.Add('Authorization', "Bearer $k")
        $resp = $req.GetResponse(); $loc = $resp.Headers['Location']; $resp.Close()
        Invoke-WebRequest -UseBasicParsing $loc -OutFile $zipPath
        $out = Join-Path $dir 'files'
        Expand-Archive $zipPath $out -Force
        New-Item -ItemType Directory -Force -Path $appDir | Out-Null
        Set-Content -Path $extrasKeyFile -Value $k -NoNewline
        if (Test-Path $extrasSkipFile) { Remove-Item $extrasSkipFile -Force }
        if ($asked) { Say '  Extras key saved: you won''t be asked again.' 'Green' }
        return $out
    } catch {
        Say "  Couldn't get the friends-only extras: $($_.Exception.Message)" 'DarkYellow'
        Say '  (Check the key, or run "Add Extras.bat" later to enter it again.) The rest of the pack still installs.' 'DarkYellow'
        return $null
    }
}

Say ''
Say '  ====================================' 'Yellow'
function Get-SteamRoot {
    if ($SteamRoot) { return $SteamRoot }   # (for testing)
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam') {
        try {
            $p = Get-ItemProperty $key -ErrorAction Stop
            foreach ($v in $p.SteamPath, $p.InstallPath) { if ($v -and (Test-Path (Join-Path $v 'userdata'))) { return ($v -replace '/', '\') } }
        } catch { }
    }
    return $null
}

# --- tiny VDF reader: finds a key's {block} or "value" at the top level of a range, respecting quotes and nesting
$script:VdfStr = [regex]'\G"((?:[^"\\]|\\.)*)"'
function Skip-Ws($t, $i) { while ($i -lt $t.Length -and [char]::IsWhiteSpace($t[$i])) { $i++ }; return $i }
function Skip-Block($t, $i) {   # $t[$i] is '{'; returns the index just after its matching '}'
    $depth = 0
    while ($i -lt $t.Length) {
        $c = $t[$i]
        if ($c -eq '"') { $m = $script:VdfStr.Match($t, $i); $i += [Math]::Max(1, $m.Length); continue }
        if ($c -eq '{') { $depth++ } elseif ($c -eq '}') { $depth--; if ($depth -eq 0) { return $i + 1 } }
        $i++
    }
    return $t.Length
}
# returns @{ Kind='block'|'value'; Start; End; Value } for $key directly inside [$from, $to), or $null
function Find-VdfKey($t, [int]$from, [int]$to, [string]$key) {
    $i = $from
    while ($i -lt $to) {
        $i = Skip-Ws $t $i
        if ($i -ge $to) { break }
        if ($t[$i] -ne '"') { $i++; continue }
        $k = $script:VdfStr.Match($t, $i); if (-not $k.Success) { $i++; continue }
        $i = Skip-Ws $t ($i + $k.Length)
        if ($i -lt $to -and $t[$i] -eq '{') {
            $end = Skip-Block $t $i
            if ($k.Groups[1].Value -ieq $key) { return @{ Kind = 'block'; Start = $i + 1; End = $end - 1 } }
            $i = $end; continue
        }
        if ($i -lt $to -and $t[$i] -eq '"') {
            $v = $script:VdfStr.Match($t, $i)
            if ($k.Groups[1].Value -ieq $key) { return @{ Kind = 'value'; Start = $i; End = $i + $v.Length; Value = $v.Groups[1].Value } }
            $i += [Math]::Max(1, $v.Length); continue
        }
    }
    return $null
}

function Set-RoR2LaunchOptions([string]$value) {   # $value = '' clears ours
    $root = Get-SteamRoot
    if (-not $root) { Say '  STEAMPLAY: Steam not found.' 'DarkGray'; return }
    $vdfValue = if ($value) { ($value -replace '\\', '\\' -replace '"', '\"') } else { '' }
    $files = Get-ChildItem (Join-Path $root 'userdata') -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'config\localconfig.vdf' } | Where-Object { Test-Path $_ }
    if (-not $files) { Say '  STEAMPLAY: no Steam accounts found on this PC.' 'DarkGray'; return }
    if (Get-Process steam -ErrorAction SilentlyContinue) { Say '  STEAM_OPEN: close Steam first (it rewrites this file when it closes).' 'Yellow'; return }
    foreach ($f in $files) {
        $t = [IO.File]::ReadAllText($f)
        # Steam reads launch options only from UserLocalConfigStore > Software > Valve > Steam > apps > 632360
        $node = @{ Start = 0; End = $t.Length }
        $ok = $true
        foreach ($k in 'UserLocalConfigStore', 'Software', 'Valve', 'Steam', 'apps') {
            $n = Find-VdfKey $t $node.Start $node.End $k
            if (-not $n -or $n.Kind -ne 'block') { $ok = $false; break }
            $node = $n
        }
        if (-not $ok) { Say "  STEAMPLAY: $f has no apps section; skipped." 'DarkGray'; continue }
        $app = Find-VdfKey $t $node.Start $node.End '632360'
        $new = $null
        if (-not $app) {
            if (-not $value) { continue }
            $new = $t.Insert($node.Start, "`n`t`t`t`t`t`"632360`"`n`t`t`t`t`t{`n`t`t`t`t`t`t`"LaunchOptions`"`t`t`"$vdfValue`"`n`t`t`t`t`t}")
        } else {
            $lo = Find-VdfKey $t $app.Start $app.End 'LaunchOptions'
            if ($value) {
                if ($lo -and $lo.Value -eq $vdfValue) { Say '  STEAMPLAY: already set.' 'Green'; continue }
                if ($lo -and $lo.Value -and $lo.Value -notmatch 'Broken-Characters') {
                    Say "  STEAMPLAY_KEPT: you already have your own launch options ($($lo.Value)); not changed." 'Yellow'; continue
                }
                $new = if ($lo) { $t.Remove($lo.Start, $lo.End - $lo.Start).Insert($lo.Start, "`"$vdfValue`"") }
                       else { $t.Insert($app.Start, "`n`t`t`t`t`t`t`"LaunchOptions`"`t`t`"$vdfValue`"") }
            } else {
                if (-not $lo -or $lo.Value -notmatch 'Broken-Characters') { continue }
                $new = $t.Remove($lo.Start, $lo.End - $lo.Start).Insert($lo.Start, '""')
            }
        }
        Copy-Item $f "$f.bc-backup" -Force
        [IO.File]::WriteAllText($f, $new)
        Say $(if ($value) { "  STEAMPLAY_ON: Steam's Play button now opens the Broken Characters Mod launcher." } else { '  STEAMPLAY_OFF: Steam''s Play button starts the game normally again.' }) 'Green'
    }
}

function Set-DesktopShortcut([string]$target, [bool]$remove) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Broken Characters Mod.lnk'
    if ($remove) { if (Test-Path $lnk) { Remove-Item $lnk -Force; Say '  Removed the desktop shortcut.' 'Green' }; return }
    if (Test-Path $lnk) { return }
    try {
        $sc = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
        $sc.TargetPath = $target; $sc.WorkingDirectory = Split-Path $target; $sc.Description = 'Broken Characters Mod'
        $sc.Save(); Say '  Added a "Broken Characters Mod" shortcut to your desktop.' 'Green'
    } catch { }
}

Say ('  BROKEN CHARACTERS MOD ' + $(if ($Uninstall) { 'UNINSTALLER' } else { 'INSTALLER' })) 'Yellow'
if (($ClearLaunchOptions -or $SteamPlayOnly) -and -not $Uninstall) {
    # Just the Steam Play button / desktop shortcut, mods untouched:
    #   -ClearLaunchOptions        undo (Play back to normal, shortcut removed)
    #   -SteamPlayOnly -LauncherPath x               desktop shortcut only
    #   ... -SetSteamPlay / -UnsetSteamPlay           also turn Steam's Play -> launcher on / off (the launcher's toggle)
    if ($ClearLaunchOptions) { Set-RoR2LaunchOptions ''; Set-DesktopShortcut '' $true }
    else {
        if ($LauncherPath -and (Test-Path $LauncherPath)) { Set-DesktopShortcut $LauncherPath $false }
        if ($SetSteamPlay -and $LauncherPath) { Set-RoR2LaunchOptions "`"$LauncherPath`" %command%" }
        elseif ($UnsetSteamPlay) { Set-RoR2LaunchOptions '' }
    }
    try { Stop-Transcript | Out-Null } catch { }
    exit 0
}
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
    $owner = New-Object System.Windows.Forms.Form -Property @{ TopMost = $true; ShowInTaskbar = $false }   # keeps the picker in front
    if ($dlg.ShowDialog($owner) -eq 'OK' -and (Test-Path (Join-Path $dlg.SelectedPath 'Risk of Rain 2.exe'))) { $games = @($dlg.SelectedPath) }
    else { Say "Cancelled (or that folder doesn't have Risk of Rain 2.exe)." 'Red'; exit 1 }
}

$extrasDir = if ($Uninstall) { $null } else { Get-Extras }
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
    if ($extrasDir -and (Test-Path $extrasDir)) {
        foreach ($m in Get-ChildItem $extrasDir -Directory) {
            $mDest = Join-Path $plugins $m.Name
            New-Item -ItemType Directory -Force -Path $mDest | Out-Null
            Copy-Item (Join-Path $m.FullName '*') $mDest -Recurse -Force
        }
        Say '  Installed the friends-only extras.' 'Green'
    }
    $done++
}

# ------------------------------------------------------------------ Steam's Play button -> the launcher
# Launch Options live in each Steam account's localconfig.vdf (app 632360). Opt-in: only with -SetSteamPlay (the
# launcher passes it after the player clicks Yes) and -LauncherPath does the installer set them to  "<launcher>" %command%  so Steam's Play opens the launcher, which then
# starts the game exe Steam passed in. -Uninstall or -ClearLaunchOptions removes them again. Steam must be closed
# while the file changes, or Steam overwrites it; if it's open, nothing is touched and the launcher retries next time.
if ($Uninstall) {
    Set-RoR2LaunchOptions ''
    Set-DesktopShortcut '' $true
} elseif ($LauncherPath -and (Test-Path $LauncherPath)) {
    Set-DesktopShortcut $LauncherPath $false
    # Only when the player said yes in the launcher (opt-in): it's their Steam account.
    if ($SetSteamPlay) { Set-RoR2LaunchOptions "`"$LauncherPath`" %command%" }
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
try { Stop-Transcript | Out-Null } catch { }
