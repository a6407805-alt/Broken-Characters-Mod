# Juggernaut

An original UES heavy assault frame, 9 m tall: the slowest, toughest thing on the field. Lore survivor. Skins:
Garrison (olive), Dustwalker (sand + rust), Contact Light (UES white + blue). Personal use.

## Body parts (passive, your rules)

- Its **1200 health is split over its body**: torso 400, head 160, each arm 160, each leg 160 (they grow with level and
  items; the health bar is their total).
- Every hit damages the part it lands on. A destroyed part's later hits go to the torso.
- It keeps fighting with **one leg** gone (-40% speed, no sprint) or **both arms** gone (their lasers are offline).
- **Head, torso or both legs at 0 = it dies.**
- **Arm Guard (hold F):** arms crossed in front: hits from the front on the head or torso land on the arms instead,
  50% less.
- Healing goes to the most damaged parts; destroyed parts come back with Coolant Flush or at the next stage.

## Heat and point defence (passive)

- Every weapon builds heat (it bleeds 14/s). At 100% it **shuts down** for 2.5 s.
- The shoulder turret shoots down an enemy projectile every 0.5 s within 25 m.

## Skills

- **Laser Bank (primary):** 2 chest lasers + 1 per working arm, 90% each.
- **Heavy Autocannon (secondary, 5 s):** one shell, 1400% to what it hits + 400% in 6 m.
  Alt **Rocket Pod (4 s):** 6 rockets x 300%.
- **Hydraulic Ram (utility, 8 s):** 15 m shoulder charge, 800% + stun to everything in the way.
  Alt **Coolant Flush (12 s):** dumps all heat, repairs every part 25% (destroyed parts come back).
- **Missile Rack (special, 10 s):** hold to lock up to 4 targets, release: 20 missiles x 200% from above.
  Alt **Alpha Strike (16 s):** everything at once at the crosshair, +70 heat.

## Cockpit HUD

While you play it (your screen only): the body-part diagram with each part's health bar, structure, level and items,
heat and throttle columns, the weapon table (OFFLINE rows for lost arms), compass, radar, reticle with range, target
box and missile locks. It hides the vanilla health, skill and item bars (config `8. HUD`).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.Juggernaut.cfg`); one Volume setting (75).

## Install

- Put the `Juggernaut` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Its model needs `juggernautbundle` next to `Juggernaut.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy
  every FBX, PNG and both TTF fonts into `Assets/Juggernaut/`, `JuggernautBuilder.cs` into `Assets/Editor/`, then menu
  **Juggernaut > Build Asset Bundle** and copy the bundle next to the DLL. Without the bundle it still plays, as a
  recoloured Commando.
