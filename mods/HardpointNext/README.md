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
- **Frame (loadout row):** Middle / Light / Heavy. **Booster / Generator:** picked on the T screen.
- **ACSIS screen (T):** booster, generator, and tuning: 1 FRS point per level, +5% per point (max 10 per stat): quick boost, overed boost, hover,
  generator output, generator capacity. Kept for the run.

All numbers: `BepInEx/config/com.ahren.HardpointNext.cfg` (sections N1-N5 are the new systems).

## Arsenal (v0.2.1)

- **Arms:** M1 (right) and M2 (left) use the same list and fire on their own: Burst Rifle 12, Assault Rifle 14,
  SMG 8, Shotgun 16, Linear Rifle 20, Pulse Gun 10 (EN, no ammo), Bazooka 30, Gatling 34 (slows you while firing).
- **Back units:** R = right back, G = left back: Missile Salvo 22, Grenade Cannon 40, Laser Cannon 32, Assault Armor 18.
- **Max load:** Light 90, Middle 120, Heavy 160. Over it: -0.8% speed, quick boost distance and EN recovery per
  point (max -50%). HUD and T screen show `LOAD x / y`.

## Feel (v0.2.2)

- **Floaty, not flying:** hold Jump in the air = vertical booster (builds up speed, keeps momentum when you let go).
  Coming down is slow (45% gravity, capped fall). With no input you drift; hold Sprint in the air to glide and steer
  (costs EN). Big landings thud. Booster flame, glow and trail on the back while boosting. Config: `N8`.
- **Guns:** Assault Rifle blooms the longer you hold; SMG is a hose with wild spread; Shotgun kicks hard and shoves
  you back in the air; Linear Rifle charges (hold, release); Pulse Gun fires visible energy bolts; Bazooka shoves you
  back; Gatling spins up with gold tracers and slows you.
