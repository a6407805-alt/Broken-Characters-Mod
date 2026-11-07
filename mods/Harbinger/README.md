# Harbinger, the Demolitionist

An original UES demolitions specialist: a sealed dome with a red star, a long blast coat and charges from belt to boots.
Skins: Standard (rust + teal), Hazard (black + yellow), Frost (white + ice blue).

## Countercharge (passive)

- When an enemy hits you, **20%** chance a belt micro-charge goes off on the attacker: **250%** in **4 m**.
  Never harms you (at most one every 0.3 s).

## Tactical Lob (primary)

- A grenade in a short arc every **0.8 s** (hold to keep throwing; attack speed makes it faster).
- It bursts on an enemy, or **0.3 s** after landing: **300%** in **4 m**. It arcs onto the crosshair.

## Corrosive Charge (secondary, 6 s)

- A **sticky** charge that clings to an enemy or a surface. **1.5 s** later: **600%** to what it's stuck to (on a surface:
  enemies within 2.5 m) and a corrosion pulse: **-30 armor** for **6 s** to everything within **6 m**.

## Linebreaker Dash (utility, 5 s)

- Dash **10 m** (the way you're moving). A **landmine** is buried where you started: armed at once, it goes off when an enemy
  steps on it: **500%** in **6 m**. Up to 6 mines, each lasts 30 s.

## Atomic Burst (special, 14 s)

- Wind up and hurl a heavy charge. **1.5 s** after the throw: **1500%** in **14 m**, then **6 mini-bombs** fly out in a ring
  (about 10 m) and burst **1 s** later for **400%** in **6 m** each.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.Harbinger.cfg`); one Volume setting (75).

## Install

- Put the `Harbinger` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `harbingerbundle` next to `Harbinger.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX and
  PNG into `Assets/Harbinger/`, `HarbingerBuilder.cs` into `Assets/Editor/`, then menu **Harbinger > Build Asset Bundle** and copy
  the bundle next to the DLL. Without the bundle he still plays, as a recoloured Commando.
