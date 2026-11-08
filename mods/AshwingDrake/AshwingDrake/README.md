# Ashwing Drake

A frost dragon survivor that fights in two forms. Every skill changes with the form.

| Skill | Grounded | Aerial |
|---|---|---|
| **Primary** | **Bite**: 350%. | **Snatch**: hold to mark a small enemy, release and the drake dives for it by itself and carries it off (300%, crushing it for 200%/s); press again to drop it for 500%. Bosses are too heavy. |
| **Secondary** | **Ice Breath**: draws in the cold, then bellows a billowing frost mist for 8 s (about 100 m), 400% per second, slowing, freezing every 6th tick. | Same, breathed down from the air. |
| **Utility** | **Corkscrew Ascent**: spiral 80 m up into aerial form. | **Corkscrew Dive**: spiral down to the ground, landing in a 300% frost shockwave. |
| **Special** | **Hidden Power**: become a massive ice drake for the rest of the stage (+40% damage, +50% health, +40 armor, ice pulses, Frost Bite and Glacial Breath), but you can't fly again until the next stage. | **Ice Meteor**: the camera goes into space as the drake drives a giant ice meteor onto your target: 10,000% to every enemy on the map. |

The special has a 3-minute cooldown, shared by both forms. It starts each stage charging, and Hidden Power ends when the stage does.

**Flight is real flight**: you always fly forward and turn towards your aim. Dive to gain speed, climb to trade it for height, hold forward to flap, back to brake, jump to flap upwards. Too slow and you stall (the nose drops). Touch down slowly to land. No fall damage.

The drake is huge: 40 m tall (about 110 m long), and Hidden Power doubles that to 80 m (config "Drake Height (m, 1.3)"). The camera sits over its shoulder (config section "1b. Camera (1.6)").

Every number is in `BepInEx/config/com.VincentMacaluso.AshwingDrake.cfg`.

## Changes

- 1.8.4: one "Volume (3)" setting (75) for every custom sound; the per-clip volumes are gone (quiet ones like the rifle are louder now).
- 1.8.3: custom sounds louder again (Master Volume 75, between the original and the 50 before).
- 1.8.2: item-choice menus (command essences, void potentials, scrappers) stay open for the drake.
- 1.8.1: all custom sounds quieter by default (new "Master Volume" setting, 50).
- 1.8.0: much tougher: 1000 base health, +300 per level (was 220, +66).
- 1.7.1: Snatch only picks up creatures (it could grab a Trial Gong, which threw an error).
- 1.7.0: the drake can use the teleporter, chests, shrines and pick-ups from much further away (scaled to its size; config "Interaction Reach (x height)").
- 1.6.0: camera placed from the drake's real in-game size (measured from its bones), so it sits right behind its back; Bite hits around the actual jaws and the ground under the head.
- 1.5.0: third-person camera right behind the drake's back (the camera height setting now really applies).
- 1.4.0: camera much closer, close behind the drake's back like a regular survivor's.
- 1.3.0: twice the size (40 m), over-the-shoulder camera, Ice Breath now comes out of the mouth and goes to the crosshair, calmer flight (slower, gentler turns).
- 1.2.0: 20 m drake, real flight, hold-to-mark Snatch with an automatic dive, 8 s Skyrim-style frost breath.

## Install

`AshwingDrake.dll`, `ashwingdrakebundle` (built in Unity, see the Unity folder) and the `Sounds` folder go in
`BepInEx/plugins/AshwingDrake/`. Sounds are Windows only.

## Credits

Dragon model and its original 6 animations: **Ashwing Drake Lite by Literock Studios** (https://literock-studios.itch.io/ashwing-drake),
used under its free licence (use in games and mods allowed; the model files themselves may not be re-shared).
New animations, ice meteor and crystals, sounds and code made for this mod.
