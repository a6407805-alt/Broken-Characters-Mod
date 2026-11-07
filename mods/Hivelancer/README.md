# Hivelancer

A Petrichor V hive sentinel: a hovering wasp warrior with spiral drill-lances for forearms and a stinger it can fire.
Lore survivor, inspired by Beedrill but an original design. Skins: Hive, Ember, Void. Personal use.

## Venom (passive)

- Every hit adds **Venom**: 10% damage per stack per second for 6 s (up to 10 stacks; new stacks refresh it).
- At **6 stacks** the target is **Marked**: the next lance or stinger hit deals **+200%** (and uses the Mark).
- Hold **Jump** in the air to **hover**.

## Twineedle (primary)

- Alternating lance jabs, **140%** each (4.5 m reach).

## Pin Missile (secondary, 4 s)

- Curls its abdomen and fires **5 homing stingers x 160%**.

## Drill Dive (utility, 6 s)

- Corkscrew **18 m** where you aim, **450%** to everything in the path. **A kill resets the cooldown.**

## Swarm Call (special, 18 s) / Fell Stinger (alt, 10 s)

- Swarm Call: **6 hive drones** for 15 s, stinging for **100%** every second (and adding Venom).
- Fell Stinger: flip and dive-stab for **1200%**. Every kill: **+3% damage for the rest of the run** (up to 20 kills;
  shows as the Fell Stinger item).

## Battle HUD

- While you play Hivelancer (your screen only): a handheld-battle-style HUD (original art): the foe's box (Wild NAME,
  Lv, HP, PSN / MRK badges), your box with HP numbers and EXP, the move menu with cooldowns as "PP", the BAG item
  strip, money and time, a capture-ring reticle and the dialogue box ("HIVELANCER used TWINEEDLE!", "It's super
  effective!" when a Mark pops). Config: `8. HUD`.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.Hivelancer.cfg`); one Volume setting (75).

## Install

- Put the `Hivelancer` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Its model needs `hivelancerbundle` next to `Hivelancer.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX, PNG and the TTF font
  into `Assets/Hivelancer/`, `HivelancerBuilder.cs` into `Assets/Editor/`, then menu **Hivelancer > Build Asset Bundle** and copy the bundle
  next to the DLL. Without the bundle it still plays, as a recoloured Commando.
