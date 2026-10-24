# The Surveyor

A blind, heavily armored engineer stranded on Petrichor V. The world is black. She sees only what her scanner paints,
and something shimmering is following her. **Early version (0.1.0), untested in game.**

Everything about the LiDAR and the Shimmer Demon happens on the Surveyor's own computer. Nobody else sees it, and there
is nothing to sync, so it works in multiplayer without anyone else needing to do anything. Original art and vanilla
sound events only (no copyrighted files).

## The suit (3D model, v0.4: built in Blender)
A heavy, armoured NASA-style EVA astronaut (white / orange / blue) with a big two-handed rotary minigun, on the Commando skeleton (every
Commando animation works). Round bubble helmet with a flip-up visor (Peek), a realistic face inside with heavy-lidded blind eyes and a comm cap,
a life-support backpack with a spinning LiDAR dome, and minigun barrels that spin with the spool.
**Weight (v0.4):** the left hand holds the foregrip, the gun lags behind turns, she leans back under the load (more when spooled), hips sway
and sink with each step, the muzzle kicks and the torso shudders while firing, the camera thuds on steps and landings, and she accelerates
and turns slower. Config `HeavyMotion_v1`, `StepShake_v1`, `UseCustomModel_v1`, `ModelScale_v1`.
Source: `src/Surveyor/Model/blender_surveyor.py` (run it in Blender; it writes `Documents/SurveyorModel/surveyor_model.bin` and previews;
`gzip -9` that into `mods/Surveyor/surveyor_model.bin.gz`).

## LiDAR Vision (passive)
- The screen is black (config `DarkWorld`). An always-on scanner paints the world as a decaying cloud of dots:
  **red = near, yellow = mid, green = far**, walls a bit brighter than floor. Enemies are bright red.
- Minigun hits, the Lance (a streak of dots along the beam) and the Shockwave (a 360 degree burst, then enemies stay tagged for 5 s) paint it fast.
- v0.2: the cloud is **packed tight** (up to 40000 dots, drawn as a pixel buffer, rays spiral over a wide cone so it fills in evenly) and dots last 9 s. Config `DotResolutionDivider_v1` = chunkiness (1 finest, slowest).
- Bottom-left: four status boxes (SEE, HEAR, TOUCH, BAL). Middle of the screen: a SPOOL bar while the barrels turn.

## Keys (v0.4.2)
- **Q / E**: switch scan mode at any time: CONE (spiral ahead), SWEEP (lighthouse beam around you), WIDE (bubble all around, short range), FOCUS (narrow long dense beam), ECHO (short cone + enemies within 45 m ping through walls). The Overdrive skill pushes the current mode. Config `ScanPrevKey_v1` / `ScanNextKey_v1` (Q and E are also the game's equipment and interact keys).
- **F**: Peek (visor off), whatever is in the utility slot. Config `PeekKey_v1`.

## Skills
| Slot | Skill | What it does |
|---|---|---|
| M1 | **Rotary Survey Gun** | Hold to spool for 1.1 s, then 2.5 up to 13 shots per second at 32% damage each (about 400% per second at full spool). -25% move speed while spooled. |
| M2 | **Surveyor Lance** | 0.5 s charge, then a piercing 600% shot with big knockback. 6 s cooldown, no holding to spam. Recoils you back unless anchored. |
| Shift | **Hydraulic Slide** | Slide forward (0.45 s). **Keep holding** to plant an **Anchor** for up to 3 s: rooted, spools 3x faster, no spool slowdown, +40% minigun damage (+25% Lance), steadier aim. 7 s cooldown after it ends. |
| R | **Acoustic Shockwave** | 500% damage blast in 30 m, 20 s cooldown. Lights up all geometry around you and throws the Shimmer Demon away. |
| Shift (alt) | **Peek** | Rip the visor off for 4.5 s. The real world comes back and you see far beyond the scanner, but it is **blurry, blown out and jarring** (depth-of-field blur, bloom, colour fringing, lens bulge, white wash) and the dots fade. 16 s cooldown. Replaces Hydraulic Slide in the loadout. |
| R (alt) | **LiDAR Overdrive** | Pick a mode with **V** (config `ModeKey`), then use the skill. **SWEEP**: a tall scan line spins 360 degrees (3.2 s). **BURST**: the whole sphere at once. **FOCUS**: a very dense beam out to 170 m (4 s). **ECHO**: every enemy within 85 m lit through walls for 8 s, and the Shimmer Demon is shown through walls too. 14 s cooldown. Replaces Acoustic Shockwave in the loadout. |

Stats: 150 HP (+45 per level), 20 armor, 6.8 move speed, 12 damage.

## The Shimmer Demon
- v0.2: it is now a filled, **animated** body (rigged: walk, prowl hunch, galloping hunt with arms thrown forward, flailing stagger, afterimage trail) instead of a still outline.
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
Shimmer Demon, Lost senses, Peek and Overdrive. Set `DarkWorld = false` to see the normal game with dots on top, and `Enabled = false` under Shimmer Demon to turn the stalker off.
