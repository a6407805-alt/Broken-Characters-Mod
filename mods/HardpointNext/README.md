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
