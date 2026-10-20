# The Duelist — a Yu-Gi-Oh!-style survivor for Risk of Rain 2

## Play it (friends start here)
1. You need Risk of Rain 2 with **BepInEx** and **R2API** (any r2modman setup has them).
2. Go to **Releases → latest** on this page and download **Duelist-Install.zip**.
3. Right-click the zip → **Extract All**, then double-click **INSTALL.bat**.
4. To update later, double-click **Update Duelist.bat** (it came in the zip). Close the game first.

Everyone in a multiplayer lobby needs the same build, so update together.

## How builds work
Every push to `main` is built by GitHub Actions (`.github/workflows/build.yml`), packaged by
`release/package.sh`, and published as the **latest** release. Nobody needs .NET installed to play.
To build locally instead: `dotnet build -c Release` in this folder.

---

# Duelist (v0.9.8, experimental)

## New in 0.9.8: your partners are duel monsters
Featherpuff, Tock, Hollow Idol, Jinx and Gloop are monster cards in your duel deck, one copy each (config `Deck → PartnersInDeck`).
When summoned they appear in 3D on your monster zone with a summoning circle.
- **Featherpuff** (Lv 3) · FEATHER WARD: while face-up, the first enemy attack each enemy turn is negated.
- **Tock** (Lv 4) · TIME ROULETTE: on summon, flip a coin. Heads: the enemy skips its next turn. Tails: draw 2.
- **Hollow Idol** (Lv 5, 1 tribute) · SOUL ABSORB: +25% ATK permanently for every enemy it destroys.
- **Jinx** (Lv 4) · MIND TAUNT: enemies must attack Jinx. ENCORE: when destroyed, your strongest graveyard monster comes back.
- **Gloop** (Lv 3) · REFORM: once per duel, when destroyed it re-forms at the start of your next turn with half HP.

## New in 0.9.7: the custom Duelist model
- `duelist_model.bin` (in this folder) is the Egyptian anime Duelist made in Blender.
- The mod loads it at startup, hides the Commando mesh and attaches each body part to the matching Commando bone, so he uses all of Commando's animations.
- No Unity or asset bundles needed. If the file is missing, you get the Commando look.
- Config section `Model`: `UseCustomModel`, `InkOutline`, `GlowStrength`, `Scale`.
- To change the model: in Blender run `Blender/build_duelist.py`, then `Blender/export_ror2.py`. Copy `Documents/DuelistModels/duelist_model.bin` into this folder and rebuild.
- Parts are rigid (each piece follows one bone), so elbows and knees bend like a puppet's. The coat follows the spine.

## New in 0.9.6
- **Smaller partners.** They're about half their old size (config `Partners → Size`, default 0.75).
- **Robot cards join the Shift cycle.** After the 5 partners, Shift goes through each drone or robot card in your deck.
  - Each one is Special Summoned as the real robot, running its normal AI, and fights on its own.
  - While a robot is out, M1/M2/R stay as your own skills, so R still opens the Duel.
  - Shift sends it back to the deck.
  - If it's destroyed, it faints for `FaintTime` seconds.

TCG survivor for Risk of Rain 2. Commando clone, tinted purple/gold, holding a deck.

## Build and install

1. `dotnet build -c Release` in this folder.
2. Copy `bin\Release\netstandard2.1\DuelistMod.dll` to `BepInEx\plugins\DuelistMod\` in the r2modman profile.
3. Profile needs R2API_Prefab, R2API_Language, R2API_ContentManagement (same as the other survivors).
4. Config: `BepInEx\config\com.ahren.duelist.cfg`. F8 reloads it in game.

## Kit

| Slot | Skill | Notes |
|---|---|---|
| Passive | Deckbuilder | Items are removed from your inventory and become cards (see below). Copies stack into a stronger card. |
| M1 | Card Toss | 150% hitscan. |
| M2 | Fan the Deck | 5 x 100% spread, 4s. |
| Shift | Call Partner | Cycles None → Featherpuff → Tock → Hollow Idol → Jinx → Gloop → None. Each one is Special Summoned from your deck through a summoning circle. |
| R | Duel Dimension | Teleports you to a duel board 250m up. 30s cooldown, starts when the duel ends. |

## Where cards come from (v0.6)

| You pick up… | Card | Effect |
|---|---|---|
| White / green item (damage or other) | **Spell** | Damage the targeted species (250% / 400%). |
| Red / boss item (damage or other) | **Spell** | Damage every species on the field. |
| Healing item (any of those tiers) | **Spell** | Restore HP. |
| Utility item (any of those tiers) | **Spell** | Draw 1 / 2 / 3 cards. |
| Equipment | **Trap** | Springs on an attack and hits the attacker back. The equipment slot stays free, so you can keep collecting. |
| Lunar (blue) item or lunar equipment | **Trap** | Halves enemy attacks for the turn. |
| Void (pink) item | **Trap** | Negates an enemy attack. |
| Drone / robot you buy or repair | **Monster** | It leaves the map and joins your deck; summoned, it stands on your zone as its real model. TC-280 and other big robots are **Extra Deck** fusions. |

- **Every copy is its own card.** Main deck max **60**, **40** recommended. Past 60, pickups stay normal items. Extra Deck doesn't count.
- Starter cards (always in): 2 Drifting Survivor monsters, Card Toss, Patch Up, Pot of Plenty, Brace.

## Partners (v0.9)

One partner at a time floats beside you and auto-attacks nearby enemies. It stays until you swap it (Shift) or it faints.
While a partner is out, **M1 / M2 / R are its abilities**; with **None** you get your normal kit (R = Duel Dimension).
**1** = attack what you're aiming at · **2** = come back to your side. It joins your duels as an ally card.

Each partner carries a golden relic (orbiting it, and on all its skill icons; dots mark M1 / M2 / R). The Duel Dimension icon is the Relic Key.

| Partner | Relic | M1 | M2 | R | Trait / auto-attack |
|---|---|---|---|---|---|
| **Featherpuff** | Necklace | Feather Glaive 260%, bounces 4x | 40% barrier | Foresight Flurry: 3s invulnerable + 8 homing glaives (200%) | Blocks the first big hit for you · glaive 120% x2 bounces |
| **Tock** | Ring | Tesla Tick 280%, chains 3x | Slow Time: 20m, slow 6s, 350% | Time Roulette: heads = freeze + 1600% in 24m, tails = -5% HP but still 400% | Tesla 120% |
| **Hollow Idol** | Eye | Soul Gaze 300%, jumps once | Absorb 900% on your target; kills seal a soul (+25% dmg up to 8, heal 15%) | Hollow Pull: 28m pull + 700% | 140% |
| **Jinx** | Rod | Card Juggle 4x130%, bounce 2x | Mind Taunt: 40m taunt 6s, armor, 300% | Encore: 1300% in 16m, full heal, taunt | Taunts nearby enemies · spark 110% x2 bounces |
| **Gloop** | Scale | Glob 280% + slow, jumps once | Sticky Puddle 600% + slow, 13m | Reform: heal 40%, Gloop full, 500% burst | Reforms in 5s · slowing dart 120% |

Looks: 1.5x bigger, detailed shape models, glow light, motion trail, the relic orbiting. Attacks are visible orbs (glaives, tesla bolts, sparks, darts) and colored shockwave rings, all seen by everyone.

Settings: `[Partners]` faint time, AutoAttackDamage_v2 / AutoAttackInterval_v2, swap cooldown, Featherpuff's threshold.

## Enemies (v0.8): one card per monster

- Every enemy on the map is its **own card** with its real HP and damage. Nothing is grouped by species any more.
- They sit in the **enemy deck** and are drawn **nearest first** onto their 5 monster zones; empty zones refill at the start of each enemy turn (new spawns go into the deck too).
- Each enemy card attacks once per turn for its real damage. A boss commander attacks twice, 1.5x as hard.
- **No boss?** The **Director** has life points (30% of the total HP of the enemies at the start). Empty its zones during your turn and your monster attacks and damage spells hit it **directly**. Zero life = Victory.
- Settings: `[Enemies]` UnitAttack, CommanderAttackMult, CommanderAttacks, DirectorLifeFraction.

## Summoning (v0.7)

Click a monster in your hand for a menu; right-click (or click a monster that can't attack) on your field for its options.

| Summon | Rule |
|---|---|
| **Normal Summon** | Lv 1–4 from hand. 1 per turn, shared with Set and Tribute. |
| **Set** | Face-down defense. Can't attack, takes half damage, flips face-up when attacked. **Flip Summon** it on a later turn. |
| **Tribute Summon** | Lv 5–6: 1 tribute, Lv 7+: 2. You pick the tributes on the board, then Confirm. |
| **Special Summon from hand** | Lv 4 or lower, only while you control no monsters. Doesn't use your normal summon. |
| **Ritual Summon** | Boss items are Ritual monsters (Lv 8). Play a Ritual spell (starter "Teleporter Rite") and pick tributes totaling Lv 8+. |
| **Fusion** | 2 monsters → big-robot fusions or the toolkit **Scrap Chimera**. |
| **Synchro** | Exactly 1 Tuner + non-Tuners → **Tuned Colossus**, Level = total, hits every species. Tuners: Healing/Emergency drones, starter "Drifting Spark". |
| **XYZ** | 2+ monsters of the same Level → **Overlay Sentinel**, Rank = that Level. Materials stay attached; detach one to make its attack hit every species. |
| **Link** | 2+ monsters → **Link Arbiter**, Link-N. Boosts your other monsters' ATK by 15% per rating. Can't go to defense. |
| **Summon from GY** | Revive spell (starter "Second Chance", Dio's Best Friend): pick a monster from your graveyard. |
| **Banishment** | A revived monster that leaves the field again is banished — gone for the duel. Graveyard and Banished piles can be clicked to view. |

- **Positions**: attack / defense (once per turn, not the turn it was summoned). Defense takes half damage and can't attack.
- Toolkit Extra monsters take their ATK/HP from the materials used (and borrow the first material's look).
- Extra materials must be face-up card monsters (not allies). Tributes can be face-down.

## Gold and results (v0.6)

- Every enemy killed in a duel pays its normal gold plus a **duel bonus** (default +100%), shown on the hit popup.
- **Victory**: the enemy commander falls, or their field and deck are both empty. Pays an extra 50% of your duel bonus.
- **Defeat**: you die, you retreat, or the rounds run out.
- A result screen shows win/lose, why, kills and gold earned; chat repeats it after you return.

## The duel (v0.3: played on the board)

- The camera locks above the arena (tabletop view) and the normal HUD hides.
- **Everything is on the board**: enemy species stand at the far end, your 5 monster zones and 5 spell/trap zones are in front of you, Extra Deck on the left, Deck and Graveyard on the right. Small labels are pinned to each one.
- **Click the 3D objects**: an enemy stand to target it (gold ring), your monster to attack, the Extra Deck to open fusions. A white ring shows what you're hovering, and a tooltip next to the mouse gives exact numbers and an attack preview.
- Only the hand (bottom), life (top right), a 4-line log (top left, L for more) and End Turn / Retreat stay flat on screen.
- Hits pop big numbers over the thing that was hit, with tracers from attacker to target.
- v0.4: each enemy species is a **live clone** of the monster (scripts stripped, idle animation playing) standing on its stand, facing you. Help is a collapsible panel ([H] or the HELP button under the log); the log shows 3 lines (click it or press L to expand). Board glow toned way down.

**v0.5: the enemy plays like you do.**
- Their side mirrors yours: 5 enemy monster zones, a spell/trap row, Deck, Graveyard and Extra Deck.
- **Commander**: the boss stands behind their field (big live model, its own life bar). It can only be attacked once their monster zones are empty ("EXPOSED"). No boss? **The Director** commands instead and can't be attacked.
- **Enemy deck = spawns**: at the start of each enemy turn, monsters that spawned on the map are drawn onto their side (new species fill empty zones; more of an existing species join it). Species beyond 5 wait in the deck.
- **Enemy spell/trap row**: elite aspects on their field (Blazing, Overloading…) show as face-up cards.
- **Enemy graveyard**: enemy units killed since the duel started. **Enemy Extra Deck**: the teleporter boss that hasn't appeared yet; when it spawns it takes command.
- **Models**: the clones now keep the parts that apply the monster's skin, so beetles, wisps and bosses should show properly.
- **Card rain (for your friends)**: every hit in the duel drops cards out of the sky onto the enemies it hit on the map (with the card's item art), sent to every player. `[Card Rain]` in the config: on/off and cards per hit.

**Allies join the duel.** Your drones, turrets and other minions are pulled up onto your monster zones, one zone per type (3 Gunner Drones = one "Gunner Drone x3" card). Their AI pauses, they attack when you click them (ATK = their combined damage x 2), and enemy attacks on that card hit the real allies. Survivors are sent back to where they were. Allies can't be tributed.

**Rules**: 1 normal summon per turn, any number of spells/traps, each monster attacks once. Hitting a species hits every unit on the map, full within 25m of where you started, down to 15% at 120m. Enemy turn: each species attacks 1 + 1 per 10 units times; your monsters are hit first, then you; traps spring on attacks. 5 rounds.

Keys: 1-8 play hand card, F1-F5 attack with zone, Tab/E/Q target, X Extra Deck, L log, R end turn, Backspace retreat.

Camera feels off? `[Camera]` in the config (Height_v2, Back_v2, LookAt_v2, Fov_v2 — re-framed for the bigger board) reloads live with F8. `TabletopCamera = false` keeps the normal camera.

## Known limits

- **Host / single player only.** A client Duelist keeps normal items and the special does nothing.
- Items are converted the moment you pick them up, so printers and scrappers can't use them.
- Enemies keep acting on the map while you duel; units that spawn mid-duel aren't on the board.
- Not compile-checked (no .NET in the cloud session). Inventory, cursor and damage-number calls go through reflection so 1.5 API changes log a warning instead of breaking the build.
- MaxRounds moved to `MaxRounds_v2` (default 5) so the old value doesn't stick.

## What to look for in the first test

1. Does the build succeed? Send any errors.
2. In `LogOutput.log`: the `Inventory API:` line, and `Patched MapZone.TeleportBody`.
3. Pick up an item: chat should say it became a card, and it should vanish from the HUD.
4. Press R near a group: are you standing on the board, do the statues show, does the cursor appear, can you click cards?
5. Does damage actually land on the map (watch the swarm thin out when you return)?
6. Tuning feel: is a big swarm's enemy turn lethal enough / too lethal? All numbers are in the config.
7. v0.9: Shift cycles partners with a summoning circle? M1/M2/R switch to its abilities and back on None? 1/2 commands? Log line `Partners ready: 5/5`.
8. v0.8: are enemies one per card, drawn nearest-first? Can you hit the Director directly once its zones are empty?
8. v0.7: try each summon type from the menus; does material picking + Confirm work? Do face-down monsters flip when attacked? Does a revived monster get banished?
8. v0.6: do items/equipment/drones turn into the right cards (chat says which, with deck count)? Log `Equipment API:` line. Does the result screen show with gold?
8. v0.5: do monsters/boss models show (log: `Clone of X: … skin=yes`)? Does `Card rain effect registered.` appear, and do friends see cards fall? Do new spawns get drawn at the enemy turn?
8. v0.4: do the enemy clones show up? The log prints `Clone of <Name>: N renderers` or why it fell back.
9. v0.3: does the tabletop camera kick in and the HUD hide? Can you click the stands/zones? Do drones/turrets appear on your zones and go back afterwards?
