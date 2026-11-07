# Prisoner, the Shackled Warden

An inmate from the Contact Light's sealed hold: a tall glass head full of amber lamps, shackles that were never meant to
come off, and the dead of Petrichor V following him. Skins: Standard (teal + forest green), Penitent (bone + charcoal,
red lamps), Spectral (violet + midnight, pale lamps).

## Symbiotic Strength (passive)

- Shackled Strike deals **+10% damage per active minion** (lesser dead or infected ally), up to **+50%**
  (4 lesser + 1 infected). The count shows as a buff.

## Shackled Strike (primary)

- A slow, heavy backhand every **1 s**: **220%** in an arc in front with **huge knockback** (bosses a third).
- An enemy with a **wall within 5 m behind it** gets slammed into it: **+300%** and a **1 s stun**.

## Raise Lesser (secondary, 6 s, 2 charges)

- A lesser dead rises in front of you: **240 health (+72 a level)**, fast, slashes and blinks like an Imp, hunts nearby
  enemies, **copies your items** (like Engineer turrets) and lasts until it is killed. **Up to 4**: a fifth releases the
  oldest. Stragglers more than 45 m away are pulled back to you.

## Chain of Command (utility, 12 s)

- Hurl a corrupted minion. An **ordinary enemy** it hits becomes **Infected**: it fights for you until it dies, keeping its
  health and skills (elites too). **One at a time**: a new one releases the old. **Bosses can't be infected**: they take
  **300%** instead.

## Obedience (special, 18 s)

- Every lesser dead and your infected ally is **healed to full** and gets **+50 armor, +100% attack speed and +50%
  movement speed** for **8 s**.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.ShackledPrisoner.cfg`); one Volume setting (75).

## Install

- Put the `Prisoner` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `prisonerbundle` next to `Prisoner.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every
  FBX and PNG into `Assets/Prisoner/`, `PrisonerBuilder.cs` into `Assets/Editor/`, then menu **Prisoner > Build Asset
  Bundle** and copy the bundle next to the DLL. Without the bundle he still plays, as a recoloured Commando (his lesser
  dead show as plain capsules).
