# VEND-R

A UES Contact Light commissary vending machine that walked off the wreck on salvaged reverse-jointed loader legs, with
two cargo arms, a cord tail and a screen face. It is very pleased to serve you. Lore survivor. Skins: UES Refresh,
Cola, Midnight. Personal use.

## Refreshments (passive)

- Every **4th skill** it drops a free drink anyone can pick up (20 s): **Cola** +30% attack speed, **Energy** +30% move
  speed, **Water** heals 15%, **Diet** +20 armor (8 s each).

## Coin Return (primary)

- Flick **5 coins x 80%**. Every coin that hits **ricochets** to another enemy within 15 m for 50%.

## Shake Well (secondary, 5 s)

- Shake a can and lob it: it bursts on an enemy (or 0.5 s after landing) for **550%** in 7 m and knocks enemies up.

## Tip Over (utility, 7 s)

- Ignore the sticker: fall forward and slide **15 m** on its face, **700%** and a stun to everything in the way.

## Bulk Order (special, 12 s) / Exact Change (alt, 10 s)

- Bulk Order: jams, then fires **12 cans x 300%** (5 m bursts); a quarter of them are free drinks instead.
- Exact Change: spends **10% of your gold** on one huge coin: **1000%** + **2000% per small chest's worth** spent
  (up to 6000%).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.VendR.cfg`); one Volume setting (75).

## Install

- Put the `VendR` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Its model needs `vendrbundle` next to `VendR.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy every FBX and PNG
  into `Assets/VendR/`, `VendRBuilder.cs` into `Assets/Editor/`, then menu **VendR > Build Asset Bundle** and copy the bundle
  next to the DLL. Without the bundle it still plays, as a recoloured Commando.
