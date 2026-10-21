#!/usr/bin/env bash
# Packages the built mod the same way "Make Release.bat" does, for GitHub's build servers.
set -euo pipefail
mod="$(cd "$(dirname "$0")/.." && pwd)"
dll="$(find "$mod/bin/Release" -name DuelistMod.dll | head -1)"
stage="$mod/release/stage"
rm -rf "$stage" "$mod/Broken-Characters-Collection.zip"
mkdir -p "$stage/Duelist"
cp "$dll" "$stage/Duelist/"
for f in "$mod"/*.bin; do gzip -9 -c "$f" > "$stage/Duelist/$(basename "$f").gz"; done
for f in duel_mat.jpg duel_mat_glow.png; do [ -f "$mod/$f" ] && cp "$mod/$f" "$stage/Duelist/"; done
[ -d "$mod/sounds" ] && cp -r "$mod/sounds" "$stage/Duelist/sounds"
# the other mods we play with (each goes into its own BepInEx\plugins\<Mod> folder)
[ -d "$mod/mods" ] && cp -r "$mod/mods" "$stage/Mods"
for f in INSTALL.bat UNINSTALL.bat install.ps1 README.txt manifest.json icon.png "Update Broken Characters.bat" update.ps1 "Add Extras.bat"; do
  [ -f "$mod/release/$f" ] && cp "$mod/release/$f" "$stage/"
done
(cd "$stage" && zip -qr9 "$mod/Broken-Characters-Collection.zip" .)
echo "Packaged $(du -h "$mod/Broken-Characters-Collection.zip" | cut -f1) -> Broken-Characters-Collection.zip"
