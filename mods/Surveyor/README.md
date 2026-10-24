# The Surveyor

A blind, heavily armored engineer stranded on Petrichor V. The world is black. She sees only what her scanner paints,
and something shimmering is following her. **Early version (0.1.0), untested in game.**

Everything about the LiDAR and the Shimmer Demon happens on the Surveyor's own computer. Nobody else sees it, and there
is nothing to sync, so it works in multiplayer without anyone else needing to do anything. Original art and vanilla
sound events only (no copyrighted files).

## LiDAR Vision (passive)
- The screen is black (config `DarkWorld`). An always-on scanner paints the world as a decaying cloud of dots:
  **red = near, yellow = mid, green = far**, walls a bit brighter than floor. Enemies are bright red.
- Minigun hits, the Lance (a streak of dots along the beam) and the Shockwave (a 360 degree burst, then enemies stay tagged for 5 s) paint it fast.
- Bottom-left: four status boxes (SEE, HEAR, TOUCH, BAL). Middle of the screen: a SPOOL bar while the barrels turn.

## Skills
| Slot | Skill | What it does |
|---|---|---|
| M1 | **Rotary Survey Gun** | Hold to spool for 1.1 s, then 2.5 up to 13 shots per second at 32% damage each (about 400% per second at full spool). -25% move speed while spooled. |
| M2 | **Surveyor Lance** | 0.5 s charge, then a piercing 600% shot with big knockback. 6 s cooldown, no holding to spam. Recoils you back unless anchored. |
| Shift | **Hydraulic Slide** | Slide forward (0.45 s). **Keep holding** to plant an **Anchor** for up to 3 s: rooted, spools 3x faster, no spool slowdown, +40% minigun damage (+25% Lance), steadier aim. 7 s cooldown after it ends. |
| R | **Acoustic Shockwave** | 500% damage blast in 30 m, 20 s cooldown. Lights up all geometry around you and throws the Shimmer Demon away. |

Stats: 150 HP (+45 per level), 20 armor, 6.8 move speed, 12 damage.

## The Shimmer Demon
A horned humanoid drawn as a shifting outline in the point cloud (red/blue edge split, glitch bands). Where it walks, nearby dots
dissolve or scatter as static (the "void pocket"). It cannot be damaged by you: your shots only push it.
- **Spawns** 80 s into a stage, 45-60 m away. **Prowl** (circles at about 35 m) -> **Stalk** (closes in, weaving) -> **Hunt**
  (a 3 s sprint at 12 m/s, faster than you walk and slower than your slide). It phases through walls and climbs to your level.
- **Pushing it back:** minigun at 85% spool or more (60% anchored): each bullet that passes through it pushes it 0.9 m, 4 hits within
  0.8 s stagger it for 0.9 s. **Lance:** 10 m back and 2.5 s stagger. **Shockwave:** 8 to 22 m back (closer = further), 3 s stagger.
- **Proximity dread:** closer than 45 m your scanner stutters and drops frames, then your HUD parts (gold/level, skills, health, crosshair)
  fill with static and drop offline one by one.
- **Contact** (within 2.4 m) steals a random sense until the stage ends, then it vanishes for 50 s:
  - **Hearing:** game sound drops to 15% (through Wwise `Volume_SFX`).
  - **Sight:** scan range and dot density halve.
  - **Touch:** +30% bullet spread and -5% move speed.
  - **Balance:** -10% move speed, spool 30% slower, much wider spread.
  It takes at most 4 senses per stage (config).

Not in this version: environmental damage or boss crossfire killing the Demon, a real refraction shader, and the Demon hurting
or being seen by other players. It is a stalker you survive, not one you kill.

## Config
`BepInEx\config\com.brokencharacters.surveyor.cfg` (F8 reloads in game). Sections: Minigun, Lance, Slide and Anchor, Shockwave, LiDAR,
Shimmer Demon, Lost senses. Set `DarkWorld = false` to see the normal game with dots on top, and `Enabled = false` under Shimmer Demon to turn the stalker off.
