# Bard, the Resonant Harp

A performer android from the UES Safe Travels' recreation deck: a striped hat with a pink feather, four glowing lenses,
bell sleeves, and a big harp at her side with three floating plectrums. Skins: Standard (powder blue + navy + rust),
Encore (cream + crimson + gold), Nocturne (charcoal + violet + silver).

## Still Resonance (passive)

- Stand still without using skills for **1 s** and you gain **+10% crit chance every 0.4 s**, up to **+100%**.
  The stacks stay when you move; your next **critical hit** uses them all up.

## Melodic Shots (primary)

- Hold to pluck a note every **0.4 s**: **120%**, it **seeks the nearest enemy** within 40 degrees of your aim (45 m)
  and curves onto it. Notes in a row on the same target deal **+10% each**, up to **+100%**.

## Motivation (secondary, 5 s)

- An uplifting chord: you and allies within **15 m** get **+40% attack speed** and **+40% movement speed** for **4 s**.

## Tag Note (utility, 7 s)

- A tethering note (60 m). On an **enemy**: you **blink** beside them and burst for **400%** in 5 m.
  On an **ally**: you **blink** beside them and **heal them for 25%** of their health.

## Speechless Performance (special, 18 s)

- A slow, sad song: every enemy within **35 m** is **Silenced** for **3 s**: held in place and unable to use skills
  (anything that can be stunned is stopped mid-attack). **Bosses too**, for **2.5 s**.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.ResonantBard.cfg`); one Volume setting (75).

## Install

- Put the `Bard` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Her model needs `bardbundle` next to `Bard.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX and
  PNG into `Assets/Bard/`, `BardBuilder.cs` into `Assets/Editor/`, then menu **Bard > Build Asset Bundle** and copy the
  bundle next to the DLL. Without the bundle she still plays, as a recoloured Commando.
