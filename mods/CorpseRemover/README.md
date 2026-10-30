# Corpse Remover

Opened chests disappear **10 seconds** after you open them, so used "corpses" don't pile up on the map.

- It covers every chest type: small, large, legendary, equipment barrels, lunar pods and so on.
- With the Artifact of Delusion on, chests stay, because that artifact needs them.
- Only the **host** needs it. Players without the mod can still join.

**Replaces Vinces' Quality of Life:** remove `VincesQualityOfLife` from r2modman first, or both would remove chests.

## Settings

`BepInEx/config/com.VincentMacaluso.CorpseRemover.cfg` (or r2modman's Config editor):

| Setting | Default | What it does |
|---|---|---|
| Opened Chests / Enabled | true | Turns it on or off. |
| Opened Chests / Seconds | 10 | Seconds before an opened chest disappears. |

## Changelog

- **1.0.0**: renamed from Vinces' Quality of Life 1.0.4, keeping only the chest removal. The extra chests and the Command picker descriptions are gone.
