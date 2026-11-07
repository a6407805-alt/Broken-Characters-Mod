# GunBlade, the Tactical Conduit

Your own survivor from Risk of Rain Returns, rebuilt for Risk of Rain 2: an android soldier with a gunsword and a long
cloak. Skins: Standard (white + maroon), Ashen (gunmetal + black), Dawn (cream + teal). Sounds from your Returns mod.

## Bloodrush (passive)

- An enemy dying within **20 m** has a **35%** chance (+10% per stack) to give **+12% move speed** for 4 s, up to 5 stacks.

## Burstfire Surge (primary)

- **4-round bursts, 55% per round.** Tight standing still, loose while moving or in the air. Hold for steady bursts.

## Kinetic Shield Wall (secondary, 10 s)

- A **6 x 4 m energy wall** in front of you for **6 s**. Enemy attacks coming through it (bullets, projectiles, melee from
  the other side) hit the wall instead; enemies can't walk through; **your shots pass**. It holds **120%** of your max
  health, then shatters.

## Bayonet Dash (utility, 6 s)

- A **12 m lunge**: the first enemy is run through for **400%** and **stunned 1.2 s**. A hit gives back **60%** of the cooldown.

## Flow State (special, 25 s)

- **8 s** in blade stance: **+60 armor**, enemies within **25 m** turn on you, and the primary becomes a blade combo:
  **220% / 220% / 400%**. Every kill adds **1.5 s** (up to 16 s). It ends early once you've lost **25%** of your health.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.GunBlade.cfg`); one Volume setting (75).

## Install

- Put the `GunBlade` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `gunbladebundle` next to `GunBlade.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX and PNG
  into `Assets/GunBlade/`, `GunBladeBuilder.cs` into `Assets/Editor/`, then menu **GunBlade > Build Asset Bundle** and copy the
  bundle next to the DLL. Without the bundle he still plays, as a recoloured Commando.
