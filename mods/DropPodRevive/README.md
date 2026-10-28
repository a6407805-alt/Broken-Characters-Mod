# DropPodRevive

Lives for every player, with drop pod respawns.

- Start with 3 lives (max 3). Every stage gives +1, never above the max.
- Die with a life left: after 4 s, if nothing else revived you (Dio's, Pluripotent Larva, Seeker's Tranquility),
  a life is spent and you come back in a drop pod.
- Die with 0 lives: you're out until the next stage, then arrive by drop pod.
- Special stages (Bazaar, Gilded Coast, Void Fields, the Moon...) never use pods.
- A "Lives: N/3" counter on the HUD (clients see theirs when R2API Networking is installed).

Config `BepInEx/config/local.droppodrevive.cfg`: StartingLives, MaxLives, LivesPerStage, ReviveDelaySeconds,
RespawnLocation (StageSpawn / DeathPosition), PodlessScenes, HUD OffsetX / OffsetY.

## Changelog

- **0.1.2**: the single-player fix, properly this time. The game decides "everyone is dead" the moment you die; 0.1.1 still turned the protection on just after that, so the run ended a few seconds after the pod brought you back. Now it's on before the game checks. As a backstop, a normal game over is also cancelled while any player is alive or waiting to respawn.
- **0.1.1**: fixed the run ending in single player when you died with lives left. The current game spawns your new body a moment after the respawn, and 0.1.0 dropped the run's "don't end" protection before that, so solo runs ended. Now the protection stays on until you're back.
- **0.1.0**: first version.
