# CarMax Auto Repair Specialist

An hourly, overworked associate. He's paid by the hour no matter what, gets yelled at by customers, orders his own
parts, has lazy coworkers and impossible deadlines. A sister character to the Certified Audi Technician.

**Personal use only.** CarMax's name is CarMax's trademark: play it with friends, don't upload it to Thunderstore.

## Hourly (Non-Exempt) (passive)

- **Paycheck every hour** (20 s): gold, no matter how you're doing. After **8 hours**: **Overtime**, time and a half and
  +15% attack speed, but stress only goes up.
- **Stress** (timecard on the left, and a buff): customers yelling at you (taking damage) adds it, overtime grinds it up,
  and it calms down after 6 s without being hit. Every point is **+0.5% damage**.
- **Burnout** at 100: he slams the desk and freezes for 3 s, plus a random debuff (cripple, slow, no healing, written
  up, paycheck garnished or HR training).
- **Deadlines** every 40-55 s: move N units (kills), deal with an elite customer, month-end close (damage), approve a PO
  (open a chest), customer satisfaction survey (don't get hit for 15 s) or a cycle count (scan units with Inventory Audit).
  - **Met:** bonus pay, -35 stress, a pizza party (+40% attack speed, regen) and maybe an item.
  - **Missed:** +25 stress and maybe written up (-25% damage).

## Skills

| Skill | What it does |
|---|---|
| **Per My Last Email** (primary) | A piercing email, 110%. Every 3rd is Reply All: three at once. |
| **Inventory Audit** (secondary) | Barcode-scans a cone for 150%. Scanned enemies are inventory for 10 s: -30 armor, and killing one sells the unit for gold. |
| **Order Your Own Parts** (utility) | Dash; a parts pallet drops where you stood (500%). 30% chance it's backordered: 4 s late, but twice as hard. |
| **Lazy Coworkers** (special) | Two coworkers for 15 s. Each has a 40% chance to be on break (more stress). Press again to Do It Yourself: send them home for +50% damage and +30% attack speed. |

## Notes

- No custom model yet: he's a recolored base body in a blue work shirt with a tie, lanyard and clipboard.
- His sounds play on Windows only. Volume and every number are in `BepInEx/config/com.brokencharacters.carmaxspecialist.cfg`.
- Built from source in `src/CarMaxSpecialist/` on every push.
