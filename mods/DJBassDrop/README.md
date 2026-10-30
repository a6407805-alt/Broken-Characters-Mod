# DJ Bass Drop

A club DJ with a violet high-top, huge glowing headphones, an LED equaliser visor, a hot-pink puffer jacket and a
floating deck with spinning platters. Everything he does grooves at 120 BPM.

## 120 BPM (passive)

- A beat every **0.5 s**. A ring around the crosshair closes in on each beat (pink when you're on it).
- His hits within **0.12 s** of a beat deal **double damage**.

## Scratch (primary)

- Every scratch sends a ring of sound **40 m** down the crosshair: **140%**, **through every enemy and wall**,
  4 per second (so every other one is on the beat). Hold it.

## Beat Drop (secondary, 6 s)

- He leaps, hangs, then slams down fist first: **600%** in **10 m**, knocking enemies up. **On the beat the radius
  doubles** (and the hit doubles like every on-beat hit). No fall damage.

## Speaker Stack (utility, 12 s)

- An **8 m wall** of four speaker stacks lands in front of him for **12 s**. It **blocks enemies and their projectiles**
  and pumps everything within 6 m in front of it for **100%** on every beat.
- On every beat each stack also **shoots a Scratch wave** straight ahead (**4 waves**, 140% each, 40 m, through
  everything).

## The Drop (special, 40 s)

- Finger to the sky. The build-up: for **2 s** the screen pulses and **every enemy on the stage that can be frozen
  freezes**. Then the bass drops: **1000% to every enemy on the stage**.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.DJBassDrop.cfg`).

## Install

- Put the `DJBassDrop` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model and the speakers need `djbassdropbundle` next to `DJBassDrop.dll`, built in Unity from the `-Unity.zip`.
