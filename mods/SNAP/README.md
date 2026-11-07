# SNA-P "Shutterbug"

An investigator bot meant to capture and replicate crime scenes. Her holoprinters make holographic copies of anything
she takes a picture of. A camera head on a jointed neck, a spinning-top body on a hover thruster, two floating limbs
that walk like bird feet and lift up as hands to frame a shot, and three holoprinters floating round her.
Skins: Default (teal-grey, warm light) and Mastery **SNA-Z** (greys, white light, a red ribbon bow).

## Holoprinters (passive)

- **3** printers float round her. Each free printer fires a laser. A hologram or printer turret takes a printer away
  until it is destroyed or the stage ends, then the printer flies home.

## Laser Pointer (primary)

- One small laser from every **free** printer, **80%** each, a volley every 0.55 s. With every printer out, her lens
  fires one.

## Flash (secondary, 7 s)

- **Stunning.** A blinding light in a 25 m, 60 degree cone: **200%** and a **2 s stun**. Flashed **flying enemies sink
  to the ground** (5 m/s for 4 s).

## Rewind Footage (utility, 8 s)

- **Heavy.** She plays her last few seconds of movement **backwards at 2x speed**. A tap rewinds 1.5 s of footage;
  **hold** to keep rewinding, up to 4 s. Enemies in the way take **250%**, more the faster she moves.

## PRTSCR (special, 30 s)

- **Hold** to raise the camera: your screen shows the **viewfinder** (dark surround, frame, focus points that light up
  over enemies, a green LCD readout: health battery, Flash ready, enemies in frame, distance, how many this picture
  would kill, printers free, and a green dot when a kill is coming). **Let go** (or tap) to take the picture.
- **800%** to every enemy inside the frame (80 m). If it kills, a **friendly hologram** of the enemy rises (a random
  one if several die), projected by one of her printers. Holograms copy her Damage and Utility items and don't last
  between stages. **At most 3**; a fourth replaces the oldest. 30 s cooldown, hit or miss.

## CLPBRD (alt special, 30 s)

- "Create a hologram of the man on the moon." The same picture; if a **projectile** (friendly or hostile) is in the
  frame, a printer **copies it** and hovers beside her, firing it at the nearest enemy every 2 s for **250%**. Same
  limit of 3, each takes a printer.

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.ShutterbugSNAP.cfg`); one Volume setting (75).

## Install

- Put the `SNAP` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Her model needs `shutterbugsnapbundle` next to `SNAP.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`: copy
  every FBX, PNG and TTF into `Assets/SNAP/`, `SNAPBuilder.cs` into `Assets/Editor/`, then menu **SNAP > Build Asset
  Bundle** and copy the bundle next to the DLL. Without the bundle she still plays, as a recoloured Commando.
- The viewfinder's 7-segment font is DSEG (SIL Open Font License, `DSEG-LICENSE.txt`).
