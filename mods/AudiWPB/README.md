# Audi West Palm Beach

A new stage on **Billy Bigsuits' flying dealership aircraft carrier**, the one from his Wrath of Palm Beach ultimate,
built full size and hollowed out. It floats high above the clouds, and you play on top of it and inside it.

**Personal use only.** Audi's and CDK's names are trademarks: play it with friends, don't upload it to Thunderstore.

## How to get there

- Turn on the **Artifact of Audi West Palm Beach** in the lobby. With it off, nothing from this mod appears.
- From **stage 2** on, each stage has a **55%** chance to have a **CDK Login** terminal somewhere, like a Newt altar.
- Use the terminal (it's free) to log in. A **portal to Audi West Palm Beach** opens next to it.
- After the carrier's teleporter, the run continues to the stage it was heading for. The carrier counts as a stage.

## The stage

- **Flight deck:** Billy's carrier at x9 (290 m), with lane markings, rows of parked Q5s and R8s, the palm trees, jet
  blast shields and tyre stacks. The **central computer** stands on the open deck beside the showroom, and the
  teleporter is always near it.
- **Showroom:** the Audi West Palm Beach island. Walk in through the glass front to find display cars on glowing
  turntables, sales desks with CDK screens, and a mezzanine.
- **Hangar deck / Service Drive:** reached by ramps down from the deck. It has car lifts with cars up on them, tool
  chests, the parts department and the service reception. Bay doors on both sides open onto balconies over the clouds.
- If you fall off, you're put back on board.

## Monsters

| | |
|---|---|
| **Audi Service Team** | Employees Billy worked to exhaustion. Five outfits: technician, technician in a beanie, red-blouse service advisor, navy-suit manager and blue-shirt foreman. They smash with **wrenches**, throw **brake rotors** like frisbees, and fling fans of **repair orders** that slow you. Tough: 350 base health (+105 per level). They live on the carrier. **Once you beat the carrier's teleporter, they spawn on every stage for the rest of the run.** |
| **Russian CDK Virus** (boss) | An 18 m mech with a monitor for a head that hijacked the central computer. It is always the carrier's teleporter boss, and extremely tough: 60,000 base health (+18,000 per level). **Firewall Stomp** sends a red shockwave along the deck (jump it). **Ransomware Beam** is a sweeping red laser from its screen. **Pop-up Barrage** fires homing FATAL ERROR windows. Below half health it calls in **Mandatory Overtime**: Service Team reinforcements. |

Some vanilla sky monsters (Brass Contraptions, Greater Wisps, Solus Probes, Jellyfish) also fly around the carrier.

## Cyan items

A new rarity, **Cyan**. Every normal item chest on the carrier gives a Cyan item. Normal item chests on other stages
have a 4% chance to. Only while the artifact is on.

| Item | Effect |
|---|---|
| **06L115562B** (Audi Q5 oil filter) | +50% health regen per stack. Every 8 s, an 8% chance to set yourself on fire; the burn is stronger with each filter. |
| **Pirelli Scorpion Verde** (tyre) | +50% move speed per stack, up to 3. At 4 stacks every tyre gives +100% instead (+400%). Extra tyres past 4 do nothing. You lose grip and drift sideways; at 4 it's much worse and you can't hold a straight line. |
| **E-tron Battery** | +1000 shield per stack. You move 25% slower per stack. Every 2.5 s, lightning arcs from you to teammates within 15 m (about 3% of their health per battery; it never kills). |

## Install

1. Import the zip in r2modman (Settings > Import local mod).
2. **The stage and the models:** build the two bundles once in Unity **2021.3.45f2**, the same as for Billy:
   - Open your Unity project (Billy's is fine). Make a folder `Assets/AudiWPB/`. Put in all the files from
     `AudiWPB-1.1.0-Unity.zip` except `AudiWPBBuilder.cs`: 10 FBX files and 4 PNG files.
   - Put `AudiWPBBuilder.cs` in `Assets/Editor/`.
   - Use the menu **Audi WPB > Build Asset Bundles**. When it's done, copy `AssetBundles/audiwpbassets` and
     `AssetBundles/audiwpbscene` next to `AudiWPB.dll` (in the r2modman profile: `BepInEx/plugins/<...>-AudiWPB/`).
   - Without the bundles there's no stage, and the terminal doesn't appear.
3. Every number is in `BepInEx/config/com.VincentMacaluso.AudiWPB.cfg`. That includes the 55% chance, the starting
   stage, health and damage of everything, and whether the Service Team spreads.
