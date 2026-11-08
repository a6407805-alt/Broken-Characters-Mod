# Billy Bigsuits

Billy Bigsuits, service director of Audi West Palm Beach, has come to the planet to establish his **Good-Will**.

**Personal use only.** Audi's name, rings and models are Audi's trademarks: play it with friends, don't upload it to Thunderstore.

## Skills

| Skill | What it does |
|---|---|
| **Good-Will** (passive) | Enemies within 20 m are **Feared**: they run away and can't attack for up to 10 s while they're near him. Each enemy can only be Feared once every 30 s (bosses too, so no cheese). |
| **4 Rings** (primary) | A 4-hit combo with the chrome rings: three wide slashes (260%), then he leaps about 8 m, spins and flips, and slams down (600% around where he lands). Hold to keep swinging. |
| **Q5 Toss / Slam** (secondary) | **Tap:** a Q5 drops from the sky into his hands and he throws it. It tumbles through enemies (300%), tossing them around, and crashes (500%). 7 s. **Hold:** two Q5s hover around him and he slams them down in front of him (900%). 15 s. |
| **R8** (utility) | Get in the R8 and drive until the gas runs out (fuel gauge on screen). W accelerates, S brakes and reverses, the car steers where you look (A/D steer too), **hold Sprint (Ctrl) or Jump to drift**. Ramming hurts more the faster you go. +60 armor. |
| **Wrath of Palm Beach** (special) | Close-up: he fixes his tie, says "All Will Fall" and points at the sky. The flying dealership **Audi WPB** comes in and bombs every enemy around with Q5s for 20 s, then leaves. 50 s cooldown (20 s of bombing + 30 s). |

He talks while he fights and moves: "Good-Will", "Warranty", "List Price", "Submit".

## Install

1. Install the zip in r2modman (Settings > Import local mod), or copy the `BillyBigsuits` folder to `BepInEx/plugins/`.
2. **The 3D models** (Billy, the Q5, the R8, the carrier): build the bundle once in Unity **2021.3.45f2**:
   - New 3D project. Make `Assets/BillyBigsuits/` and put `Billy.fbx`, `Q5.fbx`, `R8.fbx`, `Carrier.fbx`, `Rings.fbx` in it
     (from `BillyBigsuits-1.0.0-Unity.zip`).
   - Make `Assets/Editor/` and put `BillyBigsuitsBuilder.cs` in it.
   - Menu **Billy Bigsuits > Build Asset Bundle**. Copy `AssetBundles/billybigsuitsbundle` next to `BillyBigsuits.dll`.
   - Without it, Billy is a blue Commando and the cars are grey boxes (everything still works).
3. Sounds play on Windows only. One **Volume** setting (default 75) in `BepInEx/config/com.VincentMacaluso.BillyBigsuits.cfg`,
   where every other number of the kit can be changed too.
