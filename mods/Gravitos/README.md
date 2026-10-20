# Gravitos

A gravity and electricity survivor built on the Engineer, minus the backpack arms. He **levitates** instead of walking and **glides** when you hold jump in the air. He has only **10 health**, but his **energy shield** starts at 300 and grows with level.

| Slot | Skill | What it does |
|---|---|---|
| Passive | **Event Horizon** | A 15m bubble surrounds you. Every 2s it lifts enemies inside and slams them into the ground for **150%** damage. Enemy projectiles inside are slowed. |
| Primary | **Select** | Lock onto an enemy. Small ones (beetles, lemurians…) are lifted and dropped into the ground for **300%**. Bigger ones are crushed in place. |
| Secondary | **Graviton Lance** | Charge for 4s, then fire a huge purple beam for 8s: **300%/s** to everything in it and where it lands. Hits chain-shock nearby enemies, and small enemies are picked up and slammed (+150%). You move slower while charging and firing. |
| Secondary (alt) | **Orbital Ion Cannon** | Fly into the sky and become **four vertical Graviton beams** for 25s (**250%/s** each). The camera pulls far back; aim to sweep the beams across the ground like a glassing beam, WASD to fly. Same chain-shock and slam traits. **Invulnerable** until you land. 55s cooldown. |
| Utility | **PULL** | Pulls enemies, item pickups and chests in a cone up to 50m toward you. Farther targets are pulled less. Pull strength **doubles for every 50 max shield above 300**, and the 15s cooldown shortens as your max shield grows. |
| Special | **Wild Ride** | Opens a black hole that sucks in everything nearby for **150%/s** for 10s, then explodes for **3000%**. **It pulls and damages teammates too.** 40s cooldown. |

Every number is configurable in `BepInEx/config/com.VincentMacaluso.Gravitos.cfg`.

## Notes
- **PULL cooldown** = `15s × √(300 / max shield)`, never below 4s. At 300 shield it's 15s; at 1200 it's 7.5s.
- **Wild Ride and teammates:** set `Affects Teammates = false` or lower `Teammate Damage Multiplier` if your group isn't into friendly fire.
- **Backpack arms:** they're hidden by name. If they still show, open `LogOutput.log`, find the "Gravitos model hierarchy" dump, and put the arm bone names into `Hidden Bone Keywords`.
- Everyone in a multiplayer lobby needs the mod.
