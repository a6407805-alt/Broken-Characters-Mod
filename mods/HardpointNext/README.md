# Hardpoint NEXT

Vince's Hardpoint rebuilt the Armored Core 4 / For Answer way. Same mech, same skins; new movement and garage.
Source: `src/HardpointNext/` (decompiled from Hardpoint 1.0.3, then changed). Uses Hardpoint's `hardpointbundle`
from `plugins/Hardpoint/` (reused if Hardpoint already loaded it), so keep Hardpoint installed.

## What's new vs Hardpoint

- **Quick Boost (Shift):** no charges, only EN (15). Chain them. Press again right after one for a **second-stage
  quick boost** (1.7x distance, 1.6x EN). Cancels Overed Boost.
- **Primal Armor:** regenerating field, blocks up to 45% of each hit at full; hits drain it. HUD bar above AP.
  Overed Boost drains it; Assault Armor dumps all of it (full PA = 140% damage, empty = 60%).
- **Overed Boost (V):** 0.45 s spool-up, then 58 m/s where you look. Burns EN and PA. V again / Quick Boost /
  redline stops it. Jump = Boost Kick. HUD speedometer in km/h.
- **Hanger (F):** swap the right arm to the hanger weapon picked in the loadout (Shotgun or Linear Rifle).
- **Garage (loadout rows):** FRAME (Middle / Light / Heavy), BOOSTER (Standard / Quick / Overed), GENERATOR
  (Standard / Output / Capacity / Kojima), HANGER (Shotgun / Linear / None).
- **Tuning (T):** 1 FRS point per level, +5% per point (max 10 per stat): quick boost, overed boost, hover,
  generator output, generator capacity. Kept for the run.

All numbers: `BepInEx/config/com.ahren.HardpointNext.cfg` (sections N1-N5 are the new systems).

## Arsenal (v0.2)

- **Arms:** both M1 (right arm) and M2 (left arm) pick from the same list and fire on their own.
- **Back units:** R fires the right back, **G** fires the left back.
- **Weights (load):** Light 70, Middle 100, Heavy 140. Over the limit = overweight: -1.2% speed, quick boost
  distance and EN recovery per point over (max -60%). HUD shows `LOAD x / y`.

| Arm weapon | Wt | | Back unit | Wt |
|---|---|---|---|---|
| Burst Rifle | 12 | | Missile Salvo | 22 |
| Assault Rifle | 14 | | Grenade Cannon | 40 |
| SMG | 8 | | Laser Cannon | 32 |
| Shotgun | 16 | | Assault Armor | 18 |
| Linear Rifle | 20 | | | |
| Gatling | 34 | | | |
| Handgun | 8 | | | |
| Pulse Blade | 10 | | | |
| Pile Bunker | 24 | | | |

The hanger weapon counts toward the load too.
