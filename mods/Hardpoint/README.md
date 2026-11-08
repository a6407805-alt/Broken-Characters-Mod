# Hardpoint

A UES heavy combat frame, Armored Core 6 style: a 4 m bipedal mech with a burst rifle on the right arm, a pulse blade on
the left, shoulder missile pods and a big booster. Lore survivor. Three skins: UES Standard, Rustbelt, Nightfall.
Personal use.

## EN and ACS (passive)

- **EN** (100): boosting and hovering spend it; it refills at 45/s after 0.6 s. Run dry and it's **redline** until it's
  back to 40%. Hold **Jump** in the air to **hover** (28 EN/s). Sprinting is the ground boost (1.6x).
- **AC6 movement** (1.0.2, config): his core always faces where the camera looks, and the ground boost works in
  **every direction** (forward, back, sideways skating), firing the rifle and missiles included.
- **ACS stagger**: his hits build stagger (6 per 100% damage). At **100** the target **overloads**: stunned 2.5 s
  (bosses aren't) and takes **+50% damage** from everyone for 3 s.

## Burst Rifle (primary)

- 3-round bursts, **90%** per round, 50-round magazine with an automatic 1.3 s reload.

## Pulse Blade (secondary, 4 s)

- Lunge 8 m and slash for **600%** with heavy stagger. Press again within 1.2 s for the backhand (free).

## Quick Boost (utility) / Assault Boost (alt)

- Quick Boost: **3 charges** (1.2 s each), a 9 m sidestep the way you're moving, 22 EN.
- Assault Boost (6 s): fly where you aim at **30 m/s** while still firing (24 EN/s, up to 5 s, press again to stop).
  Press **Jump** for a **Boost Kick: 800%**.

## Missile Salvo (special, 10 s) / Assault Armor (alt, 30 s)

- Missile Salvo: hold to lock up to **8 targets** (0.25 s each, 90 m), release for **2 homing missiles per lock (at least 12) x 250%**.
- Assault Armor: charge (+100 armor), then a pulse explosion: **2500%** within **15 m**. Breaks stagger instantly.

## AC HUD

- While you play Hardpoint (your screen only): an Armored Core 6-style HUD: lock reticle with the target's ACS arcs,
  target bar, AP + level, item strip, EN gauge with quick-boost pips, four weapon slots, stage, timer and money. It hides
  the vanilla health, skill and item bars (config: `8. HUD`).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.Hardpoint.cfg`); one Volume setting (75).

## Install

- Put the `Hardpoint` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `hardpointbundle` next to `Hardpoint.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX, PNG and both TTF fonts
  into `Assets/Hardpoint/`, `HardpointBuilder.cs` into `Assets/Editor/`, then menu **Hardpoint > Build Asset Bundle** and copy the bundle
  next to the DLL. Without the bundle he still plays, as a recoloured Commando.
