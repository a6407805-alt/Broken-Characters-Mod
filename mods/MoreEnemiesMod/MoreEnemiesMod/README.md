# MoreEnemies

Multiplies how many enemies the game's Director spawns.

Config `BepInEx/config/com.user.moreenemies.cfg`, section General:

| Setting | Default | What it does |
|---|---|---|
| EnemyMultiplier | 2 | 1 = vanilla, up to 20. |
| ScaleRewards | true | Divide gold/XP per kill by the multiplier, so totals stay vanilla. |
| RaiseEnemyCap | true | Raise the 40-monster soft cap by the multiplier. |
| FasterSpawns | true | Shorten the Director's waits between spawns by the multiplier. |

Host-side: the host's setting decides the spawns.
