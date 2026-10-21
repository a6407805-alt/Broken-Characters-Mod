# Updates the Broken Characters Mod: downloads the newest pack from GitHub, then runs its installer
# (which also fetches the friends-only extras if you have an extras key).
param([string]$Key, [switch]$DownloadOnly, [string]$OutDir)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repo = 'a6407805-alt/Broken-Characters-Mod'
# The pack's current name first, then its earlier name (a release from before the rename only has that one).
$assets = 'Broken-Characters-Mod.zip', 'Broken-Characters-Collection.zip'
function Say($t, $c = 'Gray') { Write-Host $t -ForegroundColor $c }

$work = if ($OutDir) { $OutDir } else { Join-Path $env:TEMP 'BrokenCharactersUpdate' }
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $work | Out-Null
$zip = Join-Path $work 'pack.zip'

Say ''
Say '  Downloading the latest Broken Characters Mod...' 'Yellow'
if ($Key) {
    # (used by the build server, which tests the download with its own token)
    $h = @{ Authorization = "Bearer $Key"; 'User-Agent' = 'BrokenCharacters'; Accept = 'application/vnd.github+json' }
    $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/tags/latest" -Headers $h
    $a = $null
    foreach ($n in $assets) { if (-not $a) { $a = $rel.assets | Where-Object { $_.name -eq $n } | Select-Object -First 1 } }
    if (-not $a) { throw 'The latest release has no pack zip.' }
    $req = [System.Net.HttpWebRequest]::Create($a.url)
    $req.AllowAutoRedirect = $false; $req.UserAgent = 'BrokenCharacters'; $req.Accept = 'application/octet-stream'
    $req.Headers.Add('Authorization', "Bearer $Key")
    $resp = $req.GetResponse(); $loc = $resp.Headers['Location']; $resp.Close()
    Invoke-WebRequest -UseBasicParsing $loc -OutFile $zip
} else {
    $ok = $false
    foreach ($n in $assets) {
        try { Invoke-WebRequest -UseBasicParsing "https://github.com/$repo/releases/latest/download/$n" -OutFile $zip; $ok = $true; break }
        catch { if ($n -eq $assets[-1]) { throw } }
    }
}
Say "  Downloaded $([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB." 'Green'
if ($DownloadOnly) { exit 0 }

$pack = Join-Path $work 'pack'
Expand-Archive $zip $pack -Force
& (Join-Path $pack 'install.ps1')
