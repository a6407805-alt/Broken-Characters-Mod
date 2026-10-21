#!/usr/bin/env bash
# Packages the Broken Characters Mod zip on GitHub's build servers.
set -euo pipefail
mod="$(cd "$(dirname "$0")/.." && pwd)"
duelist="$mod/src/Duelist"   # the Duelist character (its own folder in the pack)
dll="$(find "$duelist/bin/Release" -name DuelistMod.dll | head -1)"
stage="$mod/release/stage"
rm -rf "$stage" "$mod/Broken-Characters-Mod.zip"
mkdir -p "$stage/Duelist"
cp "$dll" "$stage/Duelist/"
for f in "$duelist"/*.bin; do gzip -9 -c "$f" > "$stage/Duelist/$(basename "$f").gz"; done
for f in duel_mat.jpg duel_mat_glow.png; do [ -f "$duelist/$f" ] && cp "$duelist/$f" "$stage/Duelist/"; done
[ -d "$duelist/sounds" ] && cp -r "$duelist/sounds" "$stage/Duelist/sounds"
# the other mods we play with (each goes into its own BepInEx\plugins\<Mod> folder)
[ -d "$mod/mods" ] && cp -r "$mod/mods" "$stage/Mods"
for f in INSTALL.bat UNINSTALL.bat "Restore Steam Play.bat" install.ps1 README.txt manifest.json icon.png "Update Broken Characters.bat" update.ps1 "Add Extras.bat"; do
  [ -f "$mod/release/$f" ] && cp "$mod/release/$f" "$stage/"
done
(cd "$stage" && zip -qr9 "$mod/Broken-Characters-Mod.zip" .)
echo "Packaged $(du -h "$mod/Broken-Characters-Mod.zip" | cut -f1) -> Broken-Characters-Mod.zip"
