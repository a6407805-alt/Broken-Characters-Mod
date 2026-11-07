# Sentry, the Nature Sentinel

A big, gentle custodial mech from the Contact Light's hydroponics bay: a jar of soil and flowers on his head, turtle-shell
shoulders with plants growing out of them, and the ground of Petrichor V on his side. Skins: Standard (pale teal),
Moss (green + bark), Granite (stone grey). 180 health (+54 a level), 20 armor.

## Stonehide (passive)

- Melee hits (an attacker within 7 m, not a projectile) deal **50% less damage**, and **20%** of them **stun** the
  attacker for **1.5 s**.

## Earthen Shot (primary)

- Every **1 s** (attack speed makes it faster) he tears a stone out of the ground and launches it straight ahead:
  **350%** in a **3 m** blast with heavy knockback (60 m range).

## Earth Wall (secondary, 8 s)

- A **7 m** stone wall rises at the target (up to 25 m away), facing you, and **throws enemies away from both faces**.
  It blocks movement and shots for **3 s**, then **shatters for 400%** in **8 m**. Raise it behind a group to throw
  them toward you.

## Gaia Shell (utility, 12 s)

- **3 s** in a rock cocoon: you **can't move**, you're **immune to all damage**, and you **heal 40%** of your health.
  When it breaks: **+60 armor** for **5 s**.

## Nature's Vortex (special, 16 s)

- A wind vortex at the target (40 m) **pulls every enemy within 25 m into its eye** for **3 s**, bosses too, and deals
  **100% every 0.5 s** to everything in the eye (8 m). You get **+60% attack speed** for **6 s**.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.NatureSentry.cfg`); one Volume setting (75).

## Install

- Put the `Sentry` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `sentrybundle` next to `Sentry.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX
  and PNG into `Assets/Sentry/`, `SentryBuilder.cs` into `Assets/Editor/`, then menu **Sentry > Build Asset Bundle** and
  copy the bundle next to the DLL. Without the bundle he still plays, as a recoloured Commando.
