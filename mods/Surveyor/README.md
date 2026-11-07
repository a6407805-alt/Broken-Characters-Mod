# The Surveyor (v0.7.0)

A blind, heavily armored engineer in a white, orange and blue EVA rig. The world is black: she sees what her LiDAR paints,
and she moves like she is on the moon.

## Kit
| Key | Skill | What it does |
|---|---|---|
| Passive | LiDAR Vision / Moon Walker | The screen is a point cloud. Chests, the teleporter, shrines, printers, drones and portals are marked through walls (labels when close) and shown on a radar map on the left. Low gravity: high, slow jumps, no fall damage. |
| F | LiDAR on/off | Lift the visor to see the real world (blurry), press again to switch the LiDAR back on. |
| M1 | Rotary Survey Minigun | Hold to spool up; the longer you hold, the faster it fires. |
| M2 | Black Hole | Fires a singularity that drifts to a stop, pulls every enemy in for 4 s, then implodes (800%) and stuns. 10 s cooldown. |
| Shift | Moon Leap | Backpack and boot thrusters (blue Space-Engineers-style flames everyone sees) launch her high and far. 5 s cooldown. |
| R | LiDAR Pulse | 400% damage + stun close by, floods the LiDAR, and marks every enemy within 80 m for the whole team through walls for 8 s. Always finds the Shimmer Stalker. 12 s cooldown. |

## The Shimmer Stalker (night-stalker event)
A real enemy everyone sees and fights. A few minutes into a stage (when someone plays the Surveyor) the lights fail and a
tall, pitch-black shadow with glowing red eyes arrives near whoever strayed from the group. It keeps hunting the most
isolated player. Close to it your screen fills with static and a heartbeat vignette. Below half health it drops low and
charges much faster. Kill it as a team: it drops one red item per player. Its boss bar shows for everyone.

## Config (BepInEx/config)
Sections 11 Moon (gravity, leap), 12 LiDAR Pulse (reveal range/time), 13 Shimmer Stalker (enabled, only with Surveyor,
arrival delay, health, size, how dark it gets), 10 Black Hole, 9 Model.
