# Certified Audi Technician

A random-chance survivor. He lives and dies by his **Flat Rate** hours.

**Personal use only.** Audi's name, rings and models are Audi's trademarks: play it with friends, don't upload it to Thunderstore.

## Flat Rate (passive)

- Damage and kills put **Hours** on his clock (readout under the health bar), and every ability adds a bonus.
- Each stage is randomly **In Season** (hours x1.4) or **Out Of Season** (hours x0.65).
- Every **7 abilities** he punches in at a computer. He can't be hurt while it plays.
  - **40+ hours:** a random buff, a fist in the air and "Hell yeah!".
  - **Under 40:** a random debuff. He slams the desk, says "Oh fuck" and cries.
  - The clock then starts again at 0.
- **Buffs:**
  - an item drop (white, green or red)
  - fly around the map for 30 s
  - huge regen
  - +100% attack speed
  - +80% move speed
  - +100% damage
  - 10 s untouchable
  - +100% crit
  - every cooldown refreshed
  - gold
  - lightning on every enemy nearby
  - full heal + barrier
  - an equipment drop
- **Debuffs:**
  - lose an item
  - -50% damage
  - -40% move speed
  - -40% attack speed
  - burning
  - lose half your gold
  - launched into the air
  - every skill on cooldown
  - -60 armor
  - lose 40% health
  - rooted for 5 s
  - -40% max health

## Skills

| Skill | What it does |
|---|---|
| **LOF** (primary) | Pours oil around him and lights it: 300% within 15 m, sets enemies on fire, and the oil keeps burning in a wide ring for 15 s. +3 hours. 2 s cooldown. |
| **Replace Wiper Blades** (secondary) | Throws 2 wiper blades. Each bounces between enemies up to 15 times (hops up to 65 m) for 220% each. A blade stops when no enemy is within 65 m of the last one. +2.5 hours. |
| **AWD Fluid Change** (utility) | Hops in a Q3 and drives in circles for 12 s, ramming enemies (300%). A/D picks the direction, W moves the circle. +8 hours. |
| **40k or Bust** (special) | Puts an A5 on the lift and services it, hitting everything nearby for 500% every second. Press the 4 W/A/S/D keys shown in time for **+25 hours**. Miss one and the A5 falls off the lift onto his tool chest ("Fuck, not again!"): no more 40k this stage. 18 s cooldown. |

## Install

1. Install the zip in r2modman (Settings > Import local mod). It also needs R2API Items and RecalculateStats.
2. **The 3D models:** build the bundle once in Unity **2021.3.45f2**.
   - New 3D project. Make `Assets/AudiTechnician/` and put the 9 FBX files from `AudiTechnician-1.0.0-Unity.zip` in it.
   - Make `Assets/Editor/` and put `AudiTechnicianBuilder.cs` in it.
   - Menu **Audi Technician > Build Asset Bundle**.
   - Copy `AssetBundles/auditechnicianbundle` next to `AudiTechnician.dll`.
   - Without it, he's a red Commando and the props are boxes (everything still works).
3. Sounds play on Windows only. There's one **Volume** setting (default 75) in `BepInEx/config/com.VincentMacaluso.AudiTechnician.cfg`. Every other number in the kit is in that file too.
