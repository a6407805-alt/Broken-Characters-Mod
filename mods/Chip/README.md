# Chip

A skinny, lanky grocery bagger. He can't afford anything... so he takes out a mortgage.

**Personal use only.** He's based on a real friend's photo and works at Publix (their name is on his bag and spawn line): play it with your friends, don't upload it to Thunderstore.

## Penniless Peasant (passive)

- **He can't pick up items or equipment, and never keeps any gold.** Items on the ground stay there for the others.
- **It's Called a Mortgage:** the bar at the bottom of the screen shows the levels coming up. Every level he reaches gives a loot box:
  - even levels: **5 white items**, odd levels (from 3): **5 green items**;
  - every 7 levels: a **yellow (boss) item**, every 11 levels: a **red item**, given right away.
- Press **F** to open a box. When the reel stops: **F** takes the items, **G** spins again for the next rarity up (white -> green -> red)...
  with a **35% chance it busts** into lunar (blue), void (pink) or debuff items (Tonic Affliction).

## Ja-Ja-Jimmy (primary, 3 s)

- A stuttering sonic shout in a cone. Every "ja" is a pulse that hits for **20%**, then the shout hits for **20% / 50% / 80% / 500%**: the longer he stutters, the more pulses (1, 2, 4 or 8) and the harder the shout (the big one is rare).

## Bagging (secondary, 8 s)

- Swipes a big plastic bag: **every enemy in front of him goes in** (not bosses). Each has a 10% chance to give him a white or green item.
- **Hold** to keep them in the bag and aim; **let go** to throw them at what you're aiming at. They hit for 300% x their size x how fast they flew (random), to themselves and everything they land on.
- **While the bag is full he zooms around super fast.**

## Mega Cart (utility, 30 s after he gets out)

- A giant shopping cart with a CIWS gatling on it, **first person** with a CIWS camera overlay, **+500 armor**.
- **Hold Primary** to fire: **500 rounds**, 60% each. Ends when the ammo runs out or after 25 s.

## I Didn't Get You Anything? (special, 45 s)

- "I didn't get you anything?" A massive Christmas tree grows out of the ground. Its **20 miniguns fire presents** at enemies for 20 s
  (**300% for every 5 presents**), and it **heals him and his team** nearby.
- **10% chance** it catches fire and blows up: **5000% damage to every enemy and teammate within 50 m**. Run.

## Install

- Put the `Chip` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model and props need `chipbundle` next to `Chip.dll` (built in Unity from the `-Unity.zip`).
  Without it he's a tinted Commando and the props are boxes.
- Every player needs the mod. Settings: `BepInEx/config/com.VincentMacaluso.Chip.cfg` (one Volume, default 75; the loot-box keys).
