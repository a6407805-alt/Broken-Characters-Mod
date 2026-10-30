# Pudge

The Butcher from Dota 2, played like Dota. The numbers come from your own Dota files
(`npc_dota_hero_pudge.txt`, newer than patch 7.41). His abilities **rank up as he levels**, like skilling a Dota hero:
- Meat Hook, Rot and Meat Shield reach ranks 1/2/3/4 at levels 1/3/5/7.
- Dismember reaches ranks 1/2/3 at levels 1/6/12.

**Personal use only.** The model, animations and art are Valve's (from your own Dota files): play it with friends,
never upload it to Thunderstore.

**Needs the Unity bundle.** Build `pudgebundle` with the Unity zip and put it next to `Pudge.dll`.

## Looks

- 8 ft (2.44 m) tall (1.0.2), with the camera pulled back to match. "Height" in the config.
- 1.0.3: the camera sits above his head, like Billy's, so he doesn't block the crosshair ("Camera Height" in the config).
- The upload had only his bare body: his default hook, cleaver and belt weren't in it. So every look is a full Dota
  item set, with Dota's own textures:
  - Dapper Disguise (the default)
  - Lord of Decay, Bogatyr, Walrus Slaughterer, Deep Sea Abomination, Forbidden Regrowth, Frozen Pig Face, Hungry Clown

## Passive: Flesh Heap

- When an **elite or boss** dies within 12 m of him, or he kills one anywhere, he gets a permanent stack.
  - In Dota, these are enemy heroes dying within 450.
- Each stack is +2 strength: +13 max health, +0.5 damage, +0.1 regen, and more Dismember damage.
- The stacks last the whole run. The Flesh Heap buff shows how many he has.

## Primary: Attack

- Cleaver and hook in turn: 400% per swing, 4.5 m reach.
- One swing every 1.2 s (Dota's slow 1.7 base attack time).

## Secondary: Meat Hook (18/16/14/12 s)

- 0.3 s wind-up, then the hook flies up to 34.7 m (Dota: 1300) at 42.7 m/s. It catches the **first unit** it meets
  and drags it back to him.
- Enemies take **500/733/967/1200% pure damage**, which ignores armor.
- **Normal monsters die** when they reach him. In Dota the hook kills creeps outright.
  - Elites and bosses take the damage and get dragged.
- Allies that aren't players, such as drones, come along unharmed.
- He stands still while the hook goes out. Walls send it back empty.

## Utility: Rot (toggle)

- Press to turn it on, press again to turn it off. No cooldown.
- A green cloud with a ring at its edge, 6.7 m around him. Every 0.2 s, enemies inside:
  - take 100/200/300/400% damage per second;
  - are slowed 14/20/26/32%.
- **It hurts him too**, like Dota: 3.4/5.6/7.2/8.5% of his max health per second.
  - Here it stops at 1 health by default. The config has "Can Kill Him".
- You can toggle it during Dismember or while the hook is out.

## Extra key: Meat Shield (F, 20/19/18/17 s)

- RoR2 has four skill slots and Pudge has five abilities, so Dota's Flesh Heap active has its own key: **F**.
  - Change the key in the config.
- Its icon and cooldown show above the skill bar.
- For 4/5/6/7 s, every hit he takes is reduced by 27/47/67/87% of his damage. That covers any damage from any source,
  his own Rot included.

## Special: Dismember (30/25/20 s)

- Needs an enemy within reach: Huntress's reticle marks the one under the crosshair.
- 0.3 s wind-up, then a 2.75 s channel. The enemy is disabled and pulled in.
- 6 bites for 267/333/400% damage per second, plus strength x 0.3/0.6/0.9.
- **He heals for all of it.**
- Bosses can't be disabled here, but they still take the damage.
- Jump to stop early.

## Sounds

- 1.0.1: his own Dota voice lines and ability sounds, from your files:
  - the hook's throw, impact and retract;
  - the Rot loop;
  - Dismember's chewing and blood;
  - Meat Shield's swallow;
  - his attacks and death;
  - voice lines for spawn, attacks, kills, hooks (hit and miss), Rot, Dismember, Flesh Heap, level ups, pain and death.
- Windows only. Anything missing falls back to vanilla sounds.
- One **Volume** setting, default 75.

## Config

Everything is in `BepInEx/config/com.VincentMacaluso.Pudge.cfg`: stats, every ability number, the rank levels, the
Meat Shield key, Height, and the hook options (kills normal monsters, drags bosses, hooks allies).
