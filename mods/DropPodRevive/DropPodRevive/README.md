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
