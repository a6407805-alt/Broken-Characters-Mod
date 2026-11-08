# Infested General

A marine general taken by the swarm, who still commands like one.

| Skill | What it does |
|---|---|
| **Growth** (passive) | Every kill you land yourself permanently raises all your stats: 0.25% for small enemies, up to 2.5% for bosses. Enemies have a 30% chance to drop a purple Infestation globe. |
| **C-14 Impaler Gauss Rifle** | Full-auto, 60% per round, 10 rounds a second, 100-round magazine, then a gun-slamming reload. |
| **Baneling Toss** | Throw a baneling that bursts for 350% and poisons. 4s cooldown. |
| **Cover the Place in Auto-Turrets** | Drop 4 permanent auto-turrets that fire rapidly for low damage (8 max). |
| **Infested Command Center** | Costs 30 Infestation, once per stage. A Planetary Fortress flies in, bombs enemies on the way, and lands. It fires gauss rounds, throws banelings, and deploys up to 10 rolling banelings and 10 Infested Marines. It stays until destroyed. |

## Sounds

StarCraft clips in the `Sounds` folder next to the DLL (Windows only; one volume setting in the config,
since the game's volume sliders don't affect them):

| Clip | When |
|---|---|
| `tmafir00` | Gauss Rifle bursts and auto-turret fire |
| `blastgn2` | Command Center firing |
| `tghfir00` | Auto-turrets deployed |
| `zbgyes03` | Picked on character select, and calling in the Command Center |
| `zbgwht01` | Spotting a boss-type enemy |
| `zbgwht00` | Now and then when he's back to full health after being hurt |
| `zzepss00` | A baneling appears, and now and then while rolling banelings hunt |
| `zzedth00` | A baneling bursts |

Every number is in `BepInEx/config/com.VincentMacaluso.InfestedGeneral.cfg`.

The custom model is in the `infestedgeneralbundle` file next to the DLL. Without it, he appears as a tinted Commando.

## Changes

- 1.1.11: one "Volume (3)" setting (75) for every custom sound; the per-clip volumes are gone (quiet ones like the rifle are louder now).
- 1.1.10: auto-turrets about Commando's size (x1.5), Command Center 1.5x bigger, custom sounds louder again (Master Volume 75).
- 1.1.9: all custom sounds quieter by default (new "Master Volume" setting, 50).
