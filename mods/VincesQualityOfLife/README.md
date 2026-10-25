# Vinces' Quality of Life

Small quality-of-life changes:

- **More chests:** **+10% chests per player**, on top of the game's own scaling (the game already adds about 50% per extra player).
  - That's +10% solo, +20% with 2 players and +40% with 4.
  - Compared with the plain game solo, that's 1.1x; with 2 players, 1.8x; with 4 players, 3.5x.
  - It multiplies the stage's interactable budget, so shrines, drones and printers increase along with chests.
- **Opened chests disappear:** an opened chest vanishes **10 seconds** after you open it.
  - It covers every chest type: small, large, legendary, equipment barrels, lunar pods and so on.
  - With the Artifact of Delusion on, chests stay, because that artifact needs them.
- **Item descriptions in pickers:** with the **Artifact of Command** (and other item pickers like the scrapper), hover over an item to see its name and full description.

## Who needs it

- The **host** needs it for the chest changes.
- **Every player** who wants the descriptions needs it; those are client side.
- Players without the mod can still join.

## Settings

`BepInEx/config/com.VincentMacaluso.VincesQualityOfLife.cfg` (or r2modman's Config editor):

| Setting | Default | What it does |
|---|---|---|
| More Chests / Enabled | true | Turns the extra chests on or off. |
| More Chests / Extra Chests Per Player | 0.1 | Extra chests per player on top of the game's scaling (0.1 = +10% per player). |
| Opened Chests / Enabled | true | Turns removal of opened chests on or off. |
| Opened Chests / Seconds | 10 | Seconds before an opened chest disappears. |
| Item Picker / Show Descriptions | true | Item descriptions on hover in pickers. |

## Changelog

- **1.0.2**: +10% chests per player on top of the game's own scaling (no longer multiplies it).
- **1.0.1**: chests are 1.1x per player (was 1.5x).
- **1.0.0**: first release.
