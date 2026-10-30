# Captain Phil

A legendary fire captain in a heavy armoured turnout suit. Fire doesn't burn him: it works for him.

## Turnout Gear (passive)

- **Burns can't hurt him.**
- Every enemy his other skills kill (axe, its bleed and burn, the hose, the truck) takes **0.5 s off Punishment's cooldown**.

## Axe You A Question? (primary)

- A 6-hit fire axe combo: slash, backhand, overhead chop, a twirl into a rising cut, a full 360 spin, and an overhead slam into the ground.
- **300% damage** every hit. Every hit makes enemies **bleed** and **burn**.

## Hose em Down (secondary, 20 s)

- He leaps up into a fire-rescue helicopter, which circles the spot for **15 s** like the chopper gunner.
- **Hold Primary** to fire the door-mounted water cannon: 100% per shot, 12 shots a second, goes through every enemy in the jet.
- He can't be hurt up there. When it ends he drops back down and the helicopter flies off.

## FireTruck (utility, 20 s after he gets out)

- He drives a massive fire engine for **15 s**, sirens on, with +100 armor.
- W/S to drive and brake. The truck steers where you look (A/D steer too). Sprint or Jump is the handbrake.
- **Ram enemies for 600% damage.** The two water turrets on the deck shoot the nearest enemies on their own (60% each, about 7 shots a second).

## Punishment (special, 100 s)

- The screen goes dark. A flash of light. Captain Phil stands there holding a **massive pair of jeans**.
- He swings them twice horizontally and twice diagonally (**5000%** each, all around him), then once straight down (**15000%**).
- He finishes in a cool stance, jeans over his shoulder, as a fire explosion goes off in front of him.
- He can't be hurt while it plays (about 6 s).

## Install

- Put the `CaptainPhil` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model and props need `captainphilbundle` next to `CaptainPhil.dll` (built in Unity from the `-Unity.zip`).
  Without it he's a tinted Commando and the helicopter and truck are boxes.
- Every player needs the mod. Settings: `BepInEx/config/com.VincentMacaluso.CaptainPhil.cfg` (one Volume, default 75).
