# Alchemist

Razzil Darkbrew and his ogre from Dota 2, played like Dota. Numbers are Dota's (patch 7.41), and his abilities
**rank up as he levels**, like skilling a Dota hero:
- Basic abilities reach ranks 1/2/3/4 at levels 1/3/5/7.
- Chemical Rage reaches ranks 1/2/3 at levels 1/6/12.

**Personal use only.** The model, animations and voice are Valve's (from your own Dota files): play it with friends,
never upload it to Thunderstore.

## 1.1.2

- The camera sits above his head (like Billy Bigsuits'), so he no longer blocks the crosshair. "Camera Height" in the config.

## 1.1.0

- Dota's real textures, ability icons and portrait (from your files).
- **Skins:** Default plus Dota's five Alchemist sets: Waaagh!, Jungle Chief, Minotaur Champion, Yeti Rider and
  New Chieftain.
- **2x bigger:** 5.2 m tall, with a longer reach and the camera further back.
- **Acid Spray 5x bigger:** 46.5-66.5 m radius, cast from up to 40 m.

## Passive: Corrosive Weaponry / Greevil's Greed

- **Corrosive Weaponry:** each hit puts 2 stacks of Corroded on the enemy.
  - Each stack slows it and cuts its damage by 2.5/3/3.5/4%.
  - The stacks last 4 s and every new stack refreshes them.
  - Max 7/10/13/16 stacks.
- **Greevil's Greed:** every kill pays bonus gold, more for each kill within 40 s of the last.
  - Dota pays 2 gold, +2 per kill in the streak, up to 18. Here that is +5% up to +45% of the enemy's own gold.
  - The gold buff shows the streak.

## Primary: Attack

- The ogre's two swords: 320% per swing (9 m reach), about one swing a second.
- In Chemical Rage he uses Dota's rage attack set, much faster.

## Secondary: Unstable Concoction (17 s)

- **Press to start brewing.** Razzil shakes the flask while the ogre keeps fighting, and he moves 4/8/12/16% faster.
- **Press again to throw it** at the enemy under the crosshair (it homes; with nobody targeted it flies where you aim).
- The longer it brews, the harder it hits. Full at 5 s:
  - 500/730/970/1200% damage.
  - A stun of up to 1.7/2.2/2.7/3.2 s.
  - Corroded stacks, 1 per second brewed.
- A timer under the crosshair shows the brew. **At 5.5 s it blows up on him:** it stuns him and hits everything around
  him. His own hit can't kill him.

## Utility: Acid Spray (22-19 s), or Berserk Potion (35 s)

- **Acid Spray:** acid over the spot you aim at (up to 40 m away) for 15 s, 46.5/53.5/60/66.5 m radius.
  - Enemies in it take 83/100/117/133% damage per second.
  - They lose 15/20/25/30 armor.
- **Berserk Potion** (his Aghanim's Shard, chosen in the loadout):
  - Throws a potion at the ally under the crosshair, or drinks it if nobody is there.
  - Removes debuffs.
  - For 15 s: +50% attack speed, +10% move speed, 2.5% max health per second.

## Special: Chemical Rage (60 s)

- The ogre rages for 30 s:
  - Removes debuffs.
  - Base attack time 1.2/1.1/1.0 instead of 1.7.
  - +7/10/14% move speed.
  - Regenerates 4/5/6% of max health per second.

## Install

- Put the `Alchemist` folder in `BepInEx/plugins` (r2modman: import the zip as a local mod).
- His model needs `alchemistbundle` next to `Alchemist.dll`, built in Unity from the `-Unity.zip` (FBX + Textures
  folder + builder).
- Every player needs the mod. Settings: `BepInEx/config/com.VincentMacaluso.Alchemist.cfg` (one Volume, default 75).
