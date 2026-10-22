# Locust Horde

The Locust Horde from Gears of War, as a third faction on the planet. Turn on the **Artifact of the Locust Horde**
in the lobby and Emergence Holes start tearing open across the stage. Locust squads climb out of them in waves
every 20 seconds and make war on everything: you, your allies and the planet's monsters, who fight back.

## Emergence Holes

- Each stage there's a 60% chance an E-Hole bursts open 25-60 s in, 40-90 m from a player (rumble, the ground
  tears, red smoke): a crater 20 m across, always on flat open ground.
- After that the **invasion** rolls every minute: 25% at first, +5% for every minute you stay on the stage (50% at
  5 minutes, certain from 15). Up to 3 open at once (never on top of each other), each open until destroyed; destroy one and another can take
  its place. Camping a stage draws the Horde.
- A wave climbs out every 20 s: small on Drizzle and early in the run, growing with the difficulty and with time
  (up to 14 per squad, 40 Locusts alive at most).
- When one opens, **"The Horde Arrives..."** pops up in the middle of the screen, and **"Destroy E-Hole!"** joins
  your objectives (top right).
- **Seal it by destroying it.** It's very tough: 6000 health (+1800 per level) and 60 armor. Monsters attack
  it too, but only chip at it (25% damage) - sealing it is your job: it bursts out **one random green (uncommon) item per player**, plus gold. It stays open until
  you destroy it.

## Locusts

| Locust | What it does |
|---|---|
| **Drone** | The Horde's foot soldier: a hulking, hunched 7-ft brute with a cracked snake-skin hide, a snarling mouth of jagged teeth and long claws. 240 HP (+72 per level), 20 armor, walks at a Lemurian's pace. Shoulders its **Hammerburst** and fires a 3-round burst (75% per round) - deadly up close, wild at range - reloads after 4 bursts, and smashes anything that gets close with the **rifle butt** (250%). Roars with its arms flung wide when it picks a fight, laughs over its kills, flinches when hit. Railgunner's scope finds its head. |

More Locusts will join the roster.

## How Drones fight

Drones are close-range powerhouses that don't charge alone:
- **Waiting for backup:** a Drone walks to cover 20-45 m from its target (low cover in front, a line to shoot over
  the top), near other Locusts, and fires from there - where its Hammerburst is inaccurate.
- **The charge:** once **7 Locusts are within 15 m** of a Drone, the pack roars its attack line and rushes you at
  a sprint, roaring again as it comes. Charging Drones hit 50% harder and fire much tighter bursts up close (the
  bonus fades with distance: far away they're still wild).
- Get within 10 m of a Drone and it fights you head-on, backup or not.

All of it is in the config section "7. Tactics" ("Use Tactics" off = they always charge, like before 1.3.0).

## Sounds

Gears of War sounds in the `Sounds` folder next to the DLL (Windows only, played the same way as the other
mods' custom sounds, so the game's volume sliders don't affect them). Each sound is louder or quieter and more
muffled depending on how far away it is. Volumes are in the config (Master Volume 50 by default).

## Testing

Host with the artifact on: press **F8** to open an E-Hole near you right away (config "Debug Open E-Hole Key").

## Files

- `LocustHorde.dll`, `Sounds/`, and `locusthordebundle` (the Drone and E-Hole models; built in Unity from the
  Unity zip). Without the bundle, Drones are grey Commandos and E-Holes rough stand-ins.
- Every number is in `BepInEx/config/com.VincentMacaluso.LocustHorde.cfg`.

## Changes

- 1.7.0: sealing an E-Hole drops green items instead of reds (config "Seal Item Tier": White, Green, Red or Boss;
  "Seal Items Per Player").

- 1.6.0: Drones tougher (240 HP +72/level, 20 armor) and slightly more accurate (1 degree up close, 8 far); while
  charging they roar the attack line, deal 50% more damage and fire tighter up close (config "Rush ..." in
  "7. Tactics"). Up to 3 E-Holes at once, and they no longer open on top of each other.

- 1.5.0: the invasion: E-Holes keep rolling every minute all stage, likelier the longer you stay (up to 2 at once,
  each open until destroyed). Config "Invasion Roll Interval (1.5)", "Invasion Chance (1.5)", "Invasion Chance Per Minute (1.5)".

- 1.4.0: E-Holes lie flat (they stood on edge, half sunk in the ground), are 20 m across (config "E-Hole Diameter"),
  only open on flat open ground (config "E-Hole Flat Ground Tolerance"), and stay until destroyed (the 5-minute
  lifetime is off by default, "E-Hole Lifetime (1.4)"). Waves climb out across the whole pit.

- 1.3.0: Drones are 7 ft tall (config "Drone Height"), walk at a Lemurian's pace, and only sprint when charging.
  The Hammerburst no longer laser-beams: the aim locks when a burst starts and the spread grows with distance
  (1.5 degrees up close, 10 degrees from 40 m). New tactics: Drones hold in cover until 7 are together, then rush.
  E-Holes send a wave every 20 s. Fixed: the Drone's hurtboxes failed to build (an error in the log), which also
  skipped part of its model setup. Same asset bundle as 1.1.0.

- 1.2.0: "The Horde Arrives..." title when an E-Hole opens; "Destroy E-Hole!" objective; destroying an E-Hole drops
  one random legendary per player (config "Legendaries Per Player"). Same asset bundle as 1.1.0.

- 1.1.0: the Drone rebuilt to match Gears of War 3: hulking body, snarling face, clawed hands, Locust armour with
  a shoulder pauldron and ribbed tubes, snake-skin hide; the Hammerburst rebuilt from the classic design; new
  monster animations (twitchy idle, lumbering run, roadie run, violent reload, rifle-butt smash, roar, laugh,
  flinch, death); E-Holes much tougher (6000 HP, 60 armor, monsters do 25%); sealing pays 10 Drones' worth.
  Rebuild the asset bundle in Unity from the new Unity zip.
- 1.0.0: first version: the artifact, Emergence Holes, the Locust Drone and its Hammerburst.
