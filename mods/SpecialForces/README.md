# Special Forces

A new game mode (Main menu > **Alternate game modes > Special Forces**), inspired by StarCraft II's Special Forces
Elite. Up to **12 players** drop into **Stormreach Valley**, a 1 km rainy night valley, by dropship, to defend the
**control tower**, the only thing holding back a living storm. Hold the outpost against waves that never stop and
keep getting stronger, build defences, raid **Storm Nests** for items, and push north to kill **the Hurricane**.

- **Win:** kill the Hurricane (2.5 million health and up). **Lose:** the control tower is destroyed.
- **Time is endless.** Waves start at 2:00 and come faster and bigger; every 5th wave brings bosses.
- **Storm Nests** (8, tiers 1-4 going north) drop items and gold, and grow back after 8 minutes. Two storm lairs
  hold bosses that drop a red and a boss item.
- Chests, shrines and printers are the game's own (from Wetland Aspect), topped up every 6 minutes.
- Dead players redeploy at the outpost after 30 s while the tower stands.

## Building (B)

Inside 90 m of the control tower. Pick with **1-5**, rotate with **R**, place with **F**:

| | | Base cost |
|---|---|---|
| 1 | Auto Turret: an Engineer turret that copies your items | $120 |
| 2 | Barrier Wall | $25 |
| 3 | Supply Depot: pays you every 5 s | $100 |
| 4 | Defender Squad: 3 marines guarding the tower | $80 |
| 5 | Escort Squad: 2 marines that follow you (build an army) | $60 |

Costs scale with difficulty like chest prices. Per-player limits in the config.

## The Hurricane

A storm-cloud beast with lightning in its veins. Thunder Claw, Lightning Barrage (telegraphed strikes on everyone and
everything near it), Cyclone (tornadoes that drag you in), Storm Roar (a shockwave that throws you back), and a
Squall that calls bosses and elites at 75/50/25% health. Enraged below 25%.

## Install

1. Install this mod (r2modman: Import local mod).
2. Build the map and models in **Unity 2021.3.45f2** with the Unity zip's `SpecialForcesBuilder.cs` (Special Forces >
   Build Asset Bundles) and copy `sfstormvalley` and `specialforcesassets` next to `SpecialForces.dll`.
3. Everyone in the lobby needs the same version.

Settings: `BepInEx/config/com.VincentMacaluso.SpecialForces.cfg` (every number: tower, waves, nests, boss, building,
storm, sounds). Custom sounds play on Windows only.

## Changelog

- **1.0.4**
  - Kills give experience and gold again, using the same amounts as the game's own spawns. The "Kill Rewards" config setting multiplies them.
- **1.0.3**
  - On-hit effects, ignite and burn, bullet trails, item procs, chest and shrine interactions and landing now work on Stormreach Valley. The map was missing the game's global event handler.
  - Money and levels are kept through death and respawn.
  - Nest guards and lair bosses no longer get the strongest elites at the start.
  - The squad now shows in the dropship.
- **1.0.2**
  - The Hurricane now appears only when a player reaches the Storm Plain. The wake radius is in the config.
  - The control tower, the structures and the boss now stand upright.
  - The lander flies level, its bay is open and lit, and the interior shot shows the squad. This needs the new Unity zip.
  - Map materials whose shader is missing are skipped, which removes the pink surfaces.
  - Map and structure colliders now have surface types, which fixes projectiles and the landing errors.
- **1.0.1**: the game mode registers (1.0.0 failed to start).
