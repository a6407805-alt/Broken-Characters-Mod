# Nidus

The Infested warframe from Warframe, with his own model, animations and three skins (Default, Nidus Prime, Verminous).

| Skill | What it does |
|---|---|
| **Mutation / Undying** (passive) | Abilities grow Mutation stacks (up to 100); his tendrils grow in at 3 / 5 / 7 / 10 stacks. A killing blow with 15+ stacks spends them instead: 5 s invulnerable and healed to 50%. |
| **Virulence** | Slam the ground: a 16 m line of fungal spikes for 250% (+4% per Mutation stack). Every 5 enemies hit grows a stack. Tendrils lash up at flying enemies up to 12 m above the line. Bursts Ravenous maggots it touches. |
| **Larva** | Throw a larva that pulls in and holds enemies within 12 m for 5 s (flyers too), crushing them for 120% damage per second. Held enemies that die may grow a stack. |
| **Parasitic Link** | Link to an ally (both deal +30% damage) or an enemy (it takes 50% of the damage you take) for 60 s. Virulence also erupts from your link. Costs 1 stack if you have one. |
| **Ravenous** | Costs 3 stacks. Infested ground that heals you and allies and releases maggots that feed on enemies; hit them with Virulence to burst them. |

Every number is in `BepInEx/config/com.VincentMacaluso.NidusSurvivor.cfg`.

The custom model is in the `nidusbundle` file next to the DLL. Without it, he appears as a tinted Commando.
## Sounds

His own sound effects (synthesized from scratch) are in the `Sounds` folder next to the DLL: Virulence, the Larva throw and
grab, Parasitic Link, Ravenous, maggots appearing and bursting, Undying, and a growl when you pick him. They play through
Windows directly (volume is in the config, the game's sliders don't affect them). Replace any `.wav` with your own if you
like. Without them (or off Windows) he uses fitting creature sounds from the game itself.

## Changes

- 1.0.9: one "Volume (3)" setting (75) for every custom sound; the per-clip volumes are gone (quiet ones like the rifle are louder now).
- 1.0.8: custom sounds louder again (Master Volume 75, between the original and the 50 before).
- 1.0.7: Parasitic Link paralyzes a linked enemy (bosses are held in place); Ravenous ground is 5x bigger (30 m).
- 1.0.6: all custom sounds quieter by default (new "Master Volume" setting, 50).
