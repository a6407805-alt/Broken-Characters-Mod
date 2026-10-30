# The Toaster Knight

Model T-4000: a grumpy two-slot chrome toaster on hose arms and legs, with a red cape, a power-cord tail, a butter
knife and a waffle shield (with butter and syrup).

## Golden Brown (passive)

- Every hit he lands adds **4 heat**. After **3 s** without a hit he cools down (10 per second).
- At **100 heat** he is **on fire for 8 s**: every attack burns and he moves **+20%** faster. Then the heat resets.
- A heat bar sits under the crosshair (can be turned off).

## Toast Barrage (primary)

- The lever clacks down and both slots fire a slice of toast at the crosshair: **2 x 120%**, 2 pops a second. Hold it.

## Butter Knife (secondary, 4 s)

- A 6 m lunge and a stab for **400%**. Everything hit is **buttered: 50% slower for 3 s**.

## Pop-Up (utility, 5 s)

- He slams his own lever and **pops 18 m into the air** with a backflip (no fall damage on the way down).
- **6 slices of toast** rain down where he launched, **200%** each in a small splash.

## Bagel Setting (special, 14 s)

- He shakes and overheats for 1.6 s while a **giant bagel** comes down around him. When it lands, every enemy within
  **30 m** takes **800% and burns**, and he catches fire (heat to 100).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.ToasterKnight.cfg`).

## Install

- Put the `ToasterKnight` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model and props need `toasterknightbundle` next to `ToasterKnight.dll`, built in Unity from the `-Unity.zip`.
