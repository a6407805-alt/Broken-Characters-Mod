# Builds the Duelist and packages it into Duelist-Install.zip on your Desktop, ready for Discord.
$ErrorActionPreference = 'Stop'
$mod = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $mod
# only one DuelistMod.dll may live under plugins
foreach ($f in 'DuelistMod.dll','DuelistMod.pdb','DuelistMod.deps.json') { Remove-Item (Join-Path $mod $f) -Force -ErrorAction SilentlyContinue }
if (Test-Path (Join-Path $mod 'obj')) { Remove-Item (Join-Path $mod 'obj') -Recurse -Force }
Write-Host 'Building...' -ForegroundColor Yellow
dotnet build -c Release -v q -nologo
if ($LASTEXITCODE -ne 0) { Write-Host 'BUILD FAILED - nothing was packaged.' -ForegroundColor Red; exit 1 }
if (Test-Path (Join-Path $mod 'obj')) { Remove-Item (Join-Path $mod 'obj') -Recurse -Force }

$dll = Get-ChildItem (Join-Path $mod 'bin\Release') -Recurse -Filter 'DuelistMod.dll' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$stage = Join-Path $env:TEMP 'DuelistRelease'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$plug = Join-Path $stage 'Duelist'
New-Item -ItemType Directory -Force -Path $plug | Out-Null
Copy-Item $dll.FullName $plug

# models, gzip-compressed (the mod reads .bin.gz directly)
Add-Type -AssemblyName System.IO.Compression
foreach ($bin in Get-ChildItem $mod -Filter '*.bin') {
    $out = Join-Path $plug ($bin.Name + '.gz')
    $in = [IO.File]::OpenRead($bin.FullName)
    $fs = [IO.File]::Create($out)
    $gz = New-Object IO.Compression.GZipStream($fs, [IO.Compression.CompressionLevel]::Optimal)
    $in.CopyTo($gz); $gz.Dispose(); $fs.Dispose(); $in.Dispose()
}
foreach ($gzf in Get-ChildItem $mod -Filter '*.bin.gz') {
    if (-not (Test-Path (Join-Path $plug $gzf.Name))) { Copy-Item $gzf.FullName $plug }
}

# the playmat and any custom sounds
foreach ($f in 'duel_mat.jpg', 'duel_mat_glow.png') {
    if (Test-Path (Join-Path $mod $f)) { Copy-Item (Join-Path $mod $f) $plug }
}
$snd = Join-Path $mod 'sounds'
if (Test-Path $snd) { Copy-Item $snd (Join-Path $plug 'sounds') -Recurse }

$rel = Join-Path $mod 'release'
foreach ($f in 'INSTALL.bat', 'UNINSTALL.bat', 'install.ps1', 'README.txt', 'manifest.json', 'icon.png') {
    if (Test-Path (Join-Path $rel $f)) { Copy-Item (Join-Path $rel $f) $stage }
}

$zip = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Duelist-Install.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ''
Write-Host "READY: Duelist-Install.zip on your Desktop ($mb MB). Drag it into Discord." -ForegroundColor Green
if ($mb -gt 10) { Write-Host 'Note: over 10 MB - free Discord accounts cannot upload it.' -ForegroundColor DarkYellow }
