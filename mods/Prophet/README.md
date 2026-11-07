# Prophet

A Lemurian elder who dreamed of Providence's death before it happened, hid his clan in the high caves and prepared them
for the war with Mithrix. Now he walks beside the new crew with the Staff of Providence, and his faith turns broken,
frightened creatures into allies. Skins: Default (orange, blue scarf, sage armour), Cult (maroon, red scarf, gold scaled
armour, blue body paint), Stormcaller (deep blue, leather).

## Faith (passive)

- Enemies given Faith by **Afterlight** rise again as **Faithful** allies with a gold Providence halo and a gold ring
  at their feet: **+50% attack speed, +50% damage, +100% health, +30% movement speed**.
- They **follow you to every stage**, like drones, and level with your team. Elites keep their elite power. A
  Faithful that dies is gone for good. **Up to 6** (a seventh replaces the oldest). Stragglers more than 60 m away are
  brought back to you.
- **Never bosses** (and, by default, not boss-type monsters such as Stone Titans met as normal spawns: config).

## Epiphany (primary)

- Staff sweep **220%**, back sweep **220%**, overhead slam **380%** (4 m). Every hit has a **5% chance to stun**
  (2 s; luck counts).

## Enlightenment (secondary, 4 s)

- Hold to charge a fireball (full in 1.5 s): **300% to 900%** in a **4 to 7 m** blast, **ignites**. A **full charge
  stuns for 4 s**.

## Afterlight (utility, 10 s)

- **Greyed out** until a **stunned** enemy under **70% health** is within 30 m.
- **Hold** Utility: a gold reticle locks onto that enemy (the one nearest your crosshair), you plant your feet and pray
  for **2.5 s** while the target is held stunned; then it receives **Faith**. Let go early (or lose the target) and it
  cancels with the cooldown refunded.

## Defiance (special, 30 s)

- Every ally (players, drones, Faithful) gains a **barrier of 25% of their base health**. For **15 s**, **every hit an
  ally lands heals you 1 HP**. Providence's cloaked spirit rises behind you.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.LemurianProphet.cfg`); one Volume setting (75).

## Install

- Put the `Prophet` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `lemurianprophetbundle` next to `Prophet.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`:
  copy every FBX and PNG into `Assets/Prophet/`, `ProphetBuilder.cs` into `Assets/Editor/`, then menu **Prophet > Build
  Asset Bundle** and copy the bundle next to the DLL. Without the bundle he still plays, as a recoloured Commando.
