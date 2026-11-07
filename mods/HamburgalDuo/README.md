# Hot HamburgalWife & GrimVince

A married couple in their Halloween costumes (she's the Hamburglar, he's Grimace), lost on Petrichor V after their annual
McDonald's run to Orlando. They play as one survivor that moves together, like a two-person team: one leads, the other
helps. Skins: Halloween and Shamrock Shake (his outfit goes green). Meme survivor for personal use (real people,
McDonald's characters).

## Hot Swap (passive, key F)

- **F** switches who leads. Each has **their own four skills and cooldowns** (the bar shows the leader's).
- Swapping to **HamburgalWife**: **+50% movement speed** for 10 s. To **GrimVince**: **+100% health regen** for 10 s.
- The partner who isn't leading **uses their primary** on the nearest enemy within **17 m** (a burger or a fart cloud,
  once a second). Swapping has a 1 s cooldown and can't be used during the dances or the gorilla.

## HamburgalWife

- **Lucky I Paid** (primary, hold): burger tosses for **100% / 150% / 200%**. Every burger hit **always procs your
  on-hit items** (ATG, ukulele, sticky bombs...). "You're lucky I paid for these."
- **Caped** (secondary, 6 s): a cape swirl at everything in a **15 m cone**: **stunned for 10 s** and their **armor
  drops to 0 for 10 s** (no damage). "Nice to meet ya."
- **Rumba** (utility, 10 s): they dance the rumba for **8 s**: **+50% movement speed** and everything within 7 m is
  **set on fire** (60% every 0.5 s). "Rumba Tyme!"
- **Drop It Like It's Hot** (special, 40 s): GrimVince grows to **2x** and spins her by the hips for **14 s** (you can
  drift around): **700% + stun** every second to everything within 6 m, while she throws **hot burgers** all around
  (**450%**, ignite, 5 a second). Then both shout "Drop it!" and he slams her down: **11,000% in 25 m**.

## GrimVince

- **Flatulence** (primary, hold): he turns around and farts, three poses: **200% / 300% / 600%** in melee range
  (4.5 m). "That's a stinky one."
- **Hash Toss** (secondary): a hash brown boomerang that pierces: **400% out, 800% back**. **No cooldown, one out at a
  time** (Backup Magazines give more). When he catches it he eats it: **heal 20% + 5% per enemy it hit**.
- **Takes Two to Tango** (utility, 6 s after it ends): **GrimVince can't sprint.** Instead they tango forward on their
  own (steer with the camera, jump still works) to a tango song. A beat bar shows the timing: **tap Sprint on the
  beat** (every second beat, ~1 s). Each good tap hits everything within **45 m** for **100% (+25% per beat in a
  row)**, adds **+12% speed** (no cap) and they strike a pose. A missed or early tap, or losing **10% of your health**,
  ends it.
- **You Got Good Head on Your Shoulders** (special, 80 s, **starts every stage on cooldown**): **25 s** as a giant purple
  gorilla with her on his shoulder firing the **Grimace shake cannon** at the nearest enemy within 15 m (**369% a
  shot**, ~6 a second). **Left click**: two swipes, **700%** each. **Right click** (4 s): chest beat and roar, **400% +
  2 s stun within 30 m**. +50 armor. At the end a McDonald's tower rises under them with biplanes circling; he roars on
  the roof, leaps off and lands for **40,000% to every enemy within 150 m** (no fall damage).

All numbers are in the config (`BepInEx/config/com.VincentMacaluso.HamburgalDuo.cfg`): the Hot Swap key, every damage
number, the tango timing window, cinematic camera on/off, and one Volume setting (75). Voice lines and sounds are Windows
only (like the other survivors).

## Install

- Put the `HamburgalDuo` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- Their models need `hamburgalduobundle` next to `HamburgalDuo.dll`, built in Unity 2021.3.45f2 from the `-Unity.zip`:
  copy every FBX and PNG into `Assets/HamburgalDuo/`, `HamburgalDuoBuilder.cs` into `Assets/Editor/`, then menu
  **HamburgalDuo > Build Asset Bundle** and copy the bundle next to the DLL. Without the bundle they still play, as a
  recoloured Commando (props show as plain shapes).
