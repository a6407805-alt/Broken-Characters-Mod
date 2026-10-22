# CarMax Auto Repair Specialist

An hourly, overworked associate. He's paid by the hour no matter what, gets yelled at by customers, orders his own
parts, has lazy coworkers and impossible deadlines. A sister character to the Certified Audi Technician.

**Personal use only.** CarMax's name is CarMax's trademark: play it with friends, don't upload it to Thunderstore.

## Hourly (Non-Exempt) (passive)

- **Paycheck every hour** (20 s): gold, no matter how you're doing. After **8 hours**: **Overtime**, time and a half and
  +15% attack speed, but stress only goes up.
- **Stress** (meter right under your health bar, and a buff): customers yelling at you (taking damage) adds it, overtime grinds it up,
  and it calms down after 6 s without being hit. Every point is **+0.5% damage**.
- **Burnout** at 100: he slams the desk and freezes for 3 s, plus a random debuff (cripple, slow, no healing, written
  up, paycheck garnished or HR training).
- **Busywork pop-ups:** task windows pop up on your screen like ads and stay there, blocking your view, until you
  finish their minigame. Each open one adds stress every second; finishing one takes off 12 stress and pays a little.
  They come faster the more stressed you are (and on overtime). Up to 3 at once; the newest (yellow border) takes your keys.
  - **Parts Counter:** type the 9-digit part number with the number keys.
  - **RO Sign Off:** press the arrow keys shown for each line, in order.
  - **Write the Story:** pick the right cause, then the right correction (1, 2 or 3).
  - **Invoice:** total labor and parts and type it in.
- The timed combat deadlines from the first version can be turned back on in the config (`CombatDeadlines`).

## Skills

| Skill | What it does |
|---|---|
| **Completed Repair Orders** (primary) | Flings a completed repair order that pierces, 110%. Every 3rd is an RO Stack: three big ones at once. |
| **Inventory Audit** (secondary) | Barcode-scans a cone for 150%. Scanned enemies are inventory for 10 s: -30 armor, and killing one sells the unit for gold. |
| **Order Your Own Parts** (utility) | Dash; a parts pallet drops where you stood (500%). 30% chance it's backordered: 4 s late, but twice as hard. |
| **Lazy Coworkers** (special) | Two coworkers for 15 s. Each has a 40% chance to be on break (more stress). Press again to Do It Yourself: send them home for +50% damage and +30% attack speed. |

## Notes

- He wears the Audi Technician's model, recolored CarMax blue (needs the AudiTechnician mod; otherwise a blue base body).
- His sounds play on Windows only. Volume and every number are in `BepInEx/config/com.brokencharacters.carmaxspecialist.cfg`.
- Built from source in `src/CarMaxSpecialist/` on every push.

## Stress-driven popups & Management Audit
Task windows now scale with stress: 0-40 rare, 41-74 frequent and overlapping, 75-99 malware-style bursts.
At 100 stress a **Management Audit** locks the screen and keeps windows coming until the backlog is cleared (no Burnout).
New windows: **ERO repair order** (RO #29751, -35 stress) and the **appointment scheduler** (-10 stress; date twice,
scrambled-vowel name, twitchy promise time that throws error popups, fake progress bar that needs the compliance boxes).
Keyboard only: number keys and arrows. Tunable in the "3. Tasks" config section (`*_v2` keys).

## Controls update
- **M2** is thrown from his hand now (lug nuts, tools, jack and stands, then a car that grows as it flies, and the building right behind it).
- **R** clears every pop-up off the screen (and ends a Management Audit), lays off every enemy within ~45 m (bosses take heavy damage) and calls in the coworkers.
- **Appointment scheduler** is all number keys: press a number to choose, **NUM ENTER** to confirm. Date picked twice from a list (the order changes), the name one letter at a time (1/2/3), Promise Time with **hold 4 / hold 6** then NUM ENTER, then the compliance boxes (1, 2, 3).
- He wears the Audi Technician's model again (recolored CarMax blue) when that mod is installed; otherwise Commando's. Blue and gold throughout.

## New mini-games, speech, scrolling
- **Parts Inventory Audit:** count the boxes on the shelf, set SYSTEM QTY to match (Up/Down, + / -, or press the number), NUM ENTER. Two parts per window.
- **Parts Lookup:** pick the part number that fits the car. Each brand has its own format (Mopar ends in AA/AB, GM is 8 digits, Ford has dashes with a Z, ...), and the window gives you a hint.
- Invoices are simple addition now (labor + parts).
- **Mouse wheel** cycles through open windows.
- **3D text to speech** (Windows voice, Windows only): his lines, banners and each window's prompt are spoken from whoever said them, so friends nearby hear it (louder when close, panned left/right, a little pitch bend when someone flies past fast). Config section 5: `Enable3DTTS_v3`, `TTSVolume_v3`, `TTSRate_v3`, `TTSMinDistance_v3`, `TTSMaxDistance_v3`, `TTSDopplerLevel_v3`.

## Easier pop-ups (v3 defaults)
No pop-ups below 50 stress. 50-80: one window every ~28 s. 81-99: up to two, one every ~15 s. At 100 the audit only needs 3 windows. Finishing any window removes 45 stress and gives 3 s of Energized plus a small barrier. Open windows no longer add stress. Getting hit adds 40% less stress. Old `_v2` settings are ignored; delete the config file to reset everything.
