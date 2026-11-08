# Reaper – a Risk of Rain 2 survivor (BepInEx + R2API)

A fast, fragile scythe survivor built **without Unity**: it clones Mercenary's body and rig, hides
his sword, attaches a scythe generated in code, tints him dark with a ghostly overlay, and replaces
his skills.

| Slot | Skill | What it does |
|---|---|---|
| Passive | Reap / Ghostly | Reaped enemies die at 30% health. 25% chance for enemy hits to pass through you |
| Primary | Harvest | 3 swings of 150%, then a 4th that lunges forward and hits all around for 350% + Bleed |
| Secondary | Grim Descent | Launch up, spin, dive into a 700% ground slam (2s cd) |
| Utility | Shadow Step | Hold to mark an enemy, release to teleport behind it: 400% + Reap (1s cd) |
| Special | Sow | Blink between every enemy within 100m, return, 1250% + Reap + Bleed to all (30s cd) |

Stats: 80 HP (+24/lvl), 12 damage (+2.4/lvl), 9 move speed (Merc: 7), 0 armor.

## Build

`dotnet build -c Release` in this folder. Output: `bin/Release/netstandard2.1/Reaper.dll`.

## Install

Put `Reaper.dll` in `BepInEx/plugins/Reaper/`. The r2modman profile needs R2API_Prefab,
R2API_Language and R2API_ContentManagement (same as Emberwarden).

## Config (`BepInEx/config/com.example.reaper.cfg`)

- `[Reap]` `ExecuteThreshold`, `Duration`, `AffectsBosses`
- `[Ghostly]` `DodgeChance`
- `[Colors]` `BodyTint`, `AccentColor`, `AccentGlow`, `GhostOverlay`, `GhostTint`
- `[Model]` `HideParts`, `ScytheAttachPoint`, `ScythePosition`, `ScytheRotation`, `ScytheScale`,
  `ScytheHandleColor`, `ScytheBladeColor`, `ReloadKey`

**Live tuning:** edit the config file while the game runs, then press **F8** (`ReloadKey`) to reload
it. Colours, hidden parts and the scythe placement update immediately, no restart.

At startup the log prints a **Merc model report**: every renderer (names for `HideParts`), the
child locator names and hand-like bones (names for `ScytheAttachPoint`), and the state machines.

## Notes

Effects, sounds and animation IDs are read from Merc's (and Loader's / Vagrant's) own skill classes
at runtime (`MercAssets.cs`), so there are no guessed asset paths there. Skill numbers are static
fields at the top of each state in `SkillStates.cs`.
