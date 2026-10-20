# Phantom Assassin

Mortred, the Phantom Assassin from Dota 2, with her own model and Dota animations (the Manifold Paradox Arcana set).
Abilities follow Dota 2 patch 7.41; ranges, cooldowns and timings are scaled for Risk of Rain 2.

| Skill | What it does | Dota animation |
|---|---|---|
| **Passive: Coup de Grace & Immaterial** | Each attack has a 17% chance to grant Deadly Focus: the next attack is a guaranteed crit for 200% (300% at level 6, 400% at level 12). 25% chance to evade enemy hits, rising to 55%. | `attack_crit_v2` for the Coup de Grace |
| **Primary: Attack** | Blade strikes for 220% (0.9 s per strike). Cycles her four attacks; after Phantom Strike she uses the Arcana's strike attacks. | `attack1-4`, `phantom_strike1-3` |
| **Secondary: Stifling Dagger** | Homing dagger for 250%, slows 50% for 2.5 s, 34% chance for Deadly Focus. 2 s. | `cast1_stifling_dagger` |
| **Utility: Phantom Strike** | Teleport to the enemy or ally under your crosshair (40 m, marked with a reticle); striking an enemy gives +100% attack speed for 2.5 s. 2 charges, 12 s. | `cast2_phantomstrike` |
| **Special: Blur** | Vanish for 8 s (enemies lose track of you), +20% speed, enemy projectiles near you disjointed. Attacking reveals you. 25 s. | `cast_blur` |
| **Alt special: Fan of Knives** | Ring of knives (Aghanim's Scepter): 300% + 10% of each enemy's max health (3% for bosses) in 20 m; victims can't heal for 4 s. 14 s. | `cast_fan_of_knives` |

Also from Dota: her calm and aggressive (in combat) idles and runs, the hasted run while sprinting, her airborne flail,
death, and her hero-pick entrance and loadout idle on the character select screen.

Every number is in `BepInEx/config/com.VincentMacaluso.PhantomAssassin.cfg`.

## Changes

- 1.0.2: Stifling Dagger cooldown 6 s -> 2 s (more answer to flying enemies).
- 1.0.1: slower, smoother attacks (0.9 s per strike, was 0.6) and calmer run and cast animations; Mercenary's slash trail now shows.

## Install

`PhantomAssassin.dll` and `phantomassassinbundle` (built in Unity, see the Unity folder) go in
`BepInEx/plugins/PhantomAssassin/`. Sounds are the game's own (Mercenary and Bandit), so the game's volume sliders apply.

## Credits

Phantom Assassin, her model, textures and animations are Valve's (Dota 2), exported by the user for personal use.
This is a fan mod: keep it private (don't upload it to Thunderstore). Code, kit translation and icons made for this mod.
