# Updates the Broken Characters Collection from GitHub, then installs it.
# Works with a public or a PRIVATE repo: for a private repo it asks once for the "pack key" (a read-only
# GitHub token the pack's owner shares with friends) and remembers it in %APPDATA%\BrokenCharacters.
param([string]$Key, [switch]$DownloadOnly, [string]$OutDir)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repo = 'a6407805-alt/DuelistMod'
$asset = 'Broken-Characters-Collection.zip'
$keyDir = Join-Path $env:APPDATA 'BrokenCharacters'
$keyFile = Join-Path $keyDir 'pack-key.txt'

function Say($t, $c = 'Gray') { Write-Host $t -ForegroundColor $c }

$work = if ($OutDir) { $OutDir } else { Join-Path $env:TEMP 'BrokenCharactersUpdate' }
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null
$zip = Join-Path $work 'pack.zip'

function Download-Public {
    try { Invoke-WebRequest -UseBasicParsing "https://github.com/$repo/releases/latest/download/$asset" -OutFile $zip; return $true }
    catch { return $false }
}

function Download-WithKey($k) {
    $h = @{ Authorization = "Bearer $k"; 'User-Agent' = 'BrokenCharacters'; Accept = 'application/vnd.github+json' }
    $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/tags/latest" -Headers $h
    $a = $rel.assets | Where-Object { $_.name -eq $asset } | Select-Object -First 1
    if (-not $a) { throw "The latest release has no $asset." }
    # GitHub answers with a redirect to the file; follow it WITHOUT the key (the file host rejects it)
    $req = [System.Net.HttpWebRequest]::Create($a.url)
    $req.AllowAutoRedirect = $false
    $req.UserAgent = 'BrokenCharacters'
    $req.Accept = 'application/octet-stream'
    $req.Headers.Add('Authorization', "Bearer $k")
    $resp = $req.GetResponse()
    $loc = $resp.Headers['Location']
    if ($loc) {
        $resp.Close()
        Invoke-WebRequest -UseBasicParsing $loc -OutFile $zip
    } else {
        $fs = [System.IO.File]::Create($zip)
        try { $resp.GetResponseStream().CopyTo($fs) } finally { $fs.Close(); $resp.Close() }
    }
    return $true
}

Say ''
Say '  Downloading the latest Broken Characters Collection...' 'Yellow'
if (-not $Key -and (Test-Path $keyFile)) { $Key = (Get-Content $keyFile -Raw).Trim() }

$ok = $false
if ($Key) {
    try { $ok = Download-WithKey $Key }
    catch { Say "  The saved pack key didn't work ($($_.Exception.Message))." 'DarkYellow'; $Key = $null }
}
if (-not $ok) { $ok = Download-Public }
if (-not $ok) {
    # private repo and no working key yet: ask once
    Say ''
    Say '  This pack is private. Paste the pack key you were given (right-click to paste), then press Enter.' 'Cyan'
    $Key = (Read-Host '  Pack key').Trim()
    if (-not $Key) { Say '  No key entered. Ask whoever runs the pack for the key.' 'Red'; exit 1 }
    try { $ok = Download-WithKey $Key }
    catch { Say "  That key didn't work: $($_.Exception.Message)" 'Red'; exit 1 }
    New-Item -ItemType Directory -Force -Path $keyDir | Out-Null
    Set-Content -Path $keyFile -Value $Key -NoNewline
    Say '  Key saved: you won''t be asked again.' 'Green'
}

Say "  Downloaded $([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB." 'Green'
if ($DownloadOnly) { exit 0 }

$pack = Join-Path $work 'pack'
Expand-Archive $zip $pack -Force
& (Join-Path $pack 'install.ps1')
