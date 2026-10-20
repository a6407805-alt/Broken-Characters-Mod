# EffectBudgetFix

Risk of Rain 2 has an effects budget: when a lot of effects are alive at once, or the frame rate drops, it quietly skips
new low and medium priority effects (explosions, hit sparks, monster deaths). With huge enemy counts (for example
MoreEnemies at x20) the budget is spent almost all the time, so most explosions never show and monsters whose death
is only an effect (Lesser Wisps) simply vanish.

This mod lets every effect through, and every 30 s logs how many the budget would have hidden.

Config (`BepInEx/config/com.VincentMacaluso.EffectBudgetFix.cfg`):
- **Always Show Effects** (true): turn off to get the game's culling back (better frame rate with huge enemy counts).
- **Log Skipped Effects** (true): the 30-second log line.

## Install

`EffectBudgetFix.dll` in `BepInEx/plugins/EffectBudgetFix/`. Client-side; nothing else needed.
