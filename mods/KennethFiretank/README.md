# Kenneth the Firetank

Big Kenny: a tank in Ike-style armour (cape, tunic tails, pauldrons) with a huge golden greatsword, a buzz cut bleached
on top and thin wire glasses. Three armour skins: Firetank (crimson + steel), Radiant (blue + silver), Obsidian (black
steel + ember). Personal use only (a real person's likeness, GMC/Yukon, a Smash Bros homage).

## Bye! (passive)

- Every **4th** skill hit he lands on the same enemy, he shouts **"Bye!!"**: **500%** and it's **launched away with
  extreme force**. Every skill counts except Without a Scratch.

## Ike Combo (primary)

- A 4-hit greatsword combo: **200% / 300% / 400% / 500%**. He locks onto the enemy nearest your aim (within **30 m**) and
  **every swing flies him to it** (in the air too; he hangs while he swings). Hold to keep chaining.
- Alt: **Eruption** (2 s): hold to charge with the blade planted (up to 2 s), release: **600-1800%** + burning in 8-14 m.

## Chicago is a State / Gasoline is Clear (secondary, 16 s)

- He tells you one of two facts, 50/50. **Chicago is a State**: **+500% attack speed** for 8 s.
  **Gasoline is Clear**: for 8 s **every hit spawns a Kjaro's fire tornado**.
- Alt: **Great Aether** (8 s): throws the sword up, leaps after it carrying enemies (4x 150%), catches it and slams down
  (**800%** in 10 m).
- Alt: **Smash Bros All Night** (6 s): Ike's Counter. 1 s with the sword raised; the next enemy hit is blocked and he
  answers with **800%** to the attacker and everyone within 8 m (counts as 2 hits toward Bye!).

## Quick Draw (utility, 6 s)

- Hold up to **3 s**, then a long straight dash (12 m uncharged, 45 m fully charged), cutting everyone on the way
  for 300-900%.

## Without a Scratch (special, 60 s)

- A cutscene: he walks to his white 2-door GMC Yukon, gets in ("No seatbelts in the back."), pulls off, clips a curb
  and it starts flipping.
- Then **18 s** in the tumbling Yukon (steer with the move keys; a chase camera follows). He can't be hurt in there.
  Everything it rolls into takes **1000%** and **sticks to it** like a Katamari ball.
- At the end it explodes: **5000%** and a **Bye!** on everything within **40 m**. "Without a scratch."

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.KennethFiretank.cfg`); one Volume setting (75).

## Install

- Put the `KennethFiretank` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model and the Yukon need `kennethfiretankbundle` next to `KennethFiretank.dll`, built in Unity 2021.3.45f2 from
  the `-Unity.zip`: copy every FBX and PNG into `Assets/KennethFiretank/`, `KennethFiretankBuilder.cs` into
  `Assets/Editor/`, then menu **KennethFiretank > Build Asset Bundle**.
