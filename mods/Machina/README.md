# Machina, the Apex Construct

Your own survivor from Risk of Rain Returns, rebuilt for Risk of Rain 2: a slim war-frame on four back arms, two that
walk and leap, two that shoot. Skins: Standard (pink + plum), Gunmetal, Ivory. Sounds from your Returns mod.

## Hydraulic Leap (passive)

- Jumps **40% higher** and moves **30% faster in the air**.

## Auto-Carbine (primary)

- Hold to fire: **90%** per shot, **6 shots a second**, a tight spread.

## Ripper Discs (secondary, 3 charges, 3 s each)

- A **piercing** saw disc: **240%** to everything it cuts, and they **bleed**.

## Wall Mount (utility, 7 s)

- Lunge to a wall within **9 m** and clamp onto it for **4 s**: **+40 armor**, aim and fire freely. **Jump to kick off.**
  Losing **30%** of your health shakes you loose. No wall in front: a short hop.

## Scatter Lancer (special, 12 s)

- A focused laser from the dome: **1200%** to the first enemy, then it scatters into **4-6 arcs of 350%** to enemies
  within **20 m** of it (no line of sight needed).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.Machina.cfg`); one Volume setting (75).

## Install

- Put the `Machina` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Her model needs `machinabundle` next to `Machina.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX and PNG
  into `Assets/Machina/`, `MachinaBuilder.cs` into `Assets/Editor/`, then menu **Machina > Build Asset Bundle** and copy the
  bundle next to the DLL. Without the bundle she still plays, as a recoloured Commando.
