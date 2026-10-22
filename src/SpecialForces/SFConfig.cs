using BepInEx.Configuration;

namespace SpecialForces
{
    // Every number is here (BepInEx/config/com.VincentMacaluso.SpecialForces.cfg). Changing a default later: rename
    // the key, or old config files keep the old value.
    internal static class SFConfig
    {
        // 1. Lobby
        public static ConfigEntry<int> MaxPlayers;
        // 2. Control tower and the outpost
        public static ConfigEntry<float> TowerHealth, TowerHealthPerExtraPlayer, TowerArmor, TowerRegen, BuildRadius;
        public static ConfigEntry<float> RespawnSeconds;
        // 3. Waves
        public static ConfigEntry<float> FirstWaveSeconds, WaveInterval, WaveIntervalStep, WaveIntervalMin, WaveCredits,
            WaveCreditsPerWave, WaveCreditsPerExtraPlayer, EliteChanceBase, EliteChancePerWave, EliteChanceMax, TowerAggroRange;
        public static ConfigEntry<int> MiniBossEveryNWaves, Tier2ElitesFromWave, MaxWaveMonsters;
        public static ConfigEntry<float> KillRewards;
        // 4. Storm Nests and lairs
        public static ConfigEntry<float> NestHealthPerTier, NestRegrowMinutes, LairRegrowMinutes, NestGuardRespawnSeconds, NestGoldPerTier;
        // 5. Chests
        public static ConfigEntry<int> InteractableCredits, InteractableRefillCredits;
        public static ConfigEntry<float> InteractableRefillMinutes;
        // 6. Hurricane
        public static ConfigEntry<float> BossHealth, BossHealthPerLevel, BossArmor, BossDamage, BossDamagePerLevel, BossHeight,
            BossHealthPerExtraPlayer, BossLeash, BossMoveSpeed, BossWakeRadius;
        // 7. Building
        public static ConfigEntry<string> BuildKey, PlaceKey, RotateKey;
        public static ConfigEntry<int> TurretCost, WallCost, DepotCost, DefenderCost, EscortCost;
        public static ConfigEntry<int> TurretLimit, WallLimit, DepotLimit, DefenderLimit, EscortLimit;
        public static ConfigEntry<float> WallHealth, DepotIncome, DepotIncomeSeconds;
        // 8. Storm and cutscene
        public static ConfigEntry<bool> Cutscene, RadioAltVoice, HeavyRain;
        public static ConfigEntry<float> RainDensity, LightningMinSeconds, LightningMaxSeconds;
        // 9. Sounds
        public static ConfigEntry<bool> CustomSounds;
        public static ConfigEntry<float> MasterVolume, VoiceVolume, StormVolume, BossVolume;
        // 10. Map
        public static ConfigEntry<float> NodeSpacing;
        public static ConfigEntry<bool> UseGameMaterials;

        public static void Init(ConfigFile c)
        {
            MaxPlayers = c.Bind("1. Lobby", "Max Players", 12,
                "Players allowed in a lobby (the game's own limit is 4). Applies to every lobby while the mod is installed.");

            TowerHealth = c.Bind("2. Control Tower", "Health", 15000f, "Control tower health at level 1 (+30% per level, like monsters).");
            TowerHealthPerExtraPlayer = c.Bind("2. Control Tower", "Health Per Extra Player", 0.25f, "Extra tower health per player beyond the first (0.25 = +25%).");
            TowerArmor = c.Bind("2. Control Tower", "Armor", 50f, "Tower armour.");
            TowerRegen = c.Bind("2. Control Tower", "Regen Percent", 1f, "Health regenerated per second (% of max) after 10 s without damage.");
            BuildRadius = c.Bind("2. Control Tower", "Build Radius", 90f, "Buildings can only be placed this close to the tower (m).");
            RespawnSeconds = c.Bind("2. Control Tower", "Respawn Seconds", 30f, "Dead players respawn at the outpost after this long, while the tower stands (0 = never).");

            FirstWaveSeconds = c.Bind("3. Waves", "First Wave Seconds", 120f, "Time before the first wave.");
            WaveInterval = c.Bind("3. Waves", "Interval", 90f, "Seconds between waves at the start.");
            WaveIntervalStep = c.Bind("3. Waves", "Interval Step", 2f, "Each wave comes this many seconds sooner than the last.");
            WaveIntervalMin = c.Bind("3. Waves", "Interval Min", 40f, "Shortest time between waves.");
            WaveCredits = c.Bind("3. Waves", "Credits", 140f, "Spawn budget of the first wave (a Beetle costs 8, a Lemurian 11, a Stone Golem 40).");
            WaveCreditsPerWave = c.Bind("3. Waves", "Credits Per Wave", 0.14f, "Budget growth per wave (0.14 = +14% of the first wave's budget per wave). Difficulty over time multiplies it too.");
            WaveCreditsPerExtraPlayer = c.Bind("3. Waves", "Credits Per Extra Player", 0.35f, "Budget growth per player beyond the first.");
            EliteChanceBase = c.Bind("3. Waves", "Elite Chance", 0.10f, "Chance each wave monster is an elite at wave 1.");
            EliteChancePerWave = c.Bind("3. Waves", "Elite Chance Per Wave", 0.03f, "Elite chance added per wave.");
            EliteChanceMax = c.Bind("3. Waves", "Elite Chance Max", 0.70f, "Highest elite chance.");
            MiniBossEveryNWaves = c.Bind("3. Waves", "Mini-boss Every N Waves", 5, "Every Nth wave brings bosses (more of them as waves go on).");
            KillRewards = c.Bind("3. Waves", "Kill Rewards", 1f, "Experience and gold per kill, x the game's own amount (what the combat director gives).");
            Tier2ElitesFromWave = c.Bind("3. Waves", "Tier 2 Elites From Wave", 25, "From this wave on, elites can be the game's tier 2 kinds (malachite, celestine).");
            MaxWaveMonsters = c.Bind("3. Waves", "Max Living Wave Monsters", 120, "No new wave monsters spawn while this many are alive (performance with 12 players).");
            TowerAggroRange = c.Bind("3. Waves", "Distraction Range", 25f, "Wave monsters head for the tower unless a player, ally or building is this close (m).");

            NestHealthPerTier = c.Bind("4. Storm Nests", "Health Per Tier", 2500f, "Storm Nest health per tier (tier 1-4) at level 1, +30% per level.");
            NestRegrowMinutes = c.Bind("4. Storm Nests", "Regrow Minutes", 8f, "A destroyed nest grows back after this long (farming).");
            LairRegrowMinutes = c.Bind("4. Storm Nests", "Lair Regrow Minutes", 12f, "A killed lair boss is replaced after this long.");
            NestGuardRespawnSeconds = c.Bind("4. Storm Nests", "Guard Respawn Seconds", 20f, "A nest spawns a new guard this often while players are near and it has fewer than its full guard.");
            NestGoldPerTier = c.Bind("4. Storm Nests", "Gold Per Tier", 60f, "Gold for each player when a nest dies (x tier, scaled by difficulty like chests).");

            InteractableCredits = c.Bind("5. Chests", "Credits", 1000, "Chests, shrines, printers etc. placed at the start (a normal stage gets ~220).");
            InteractableRefillCredits = c.Bind("5. Chests", "Refill Credits", 160, "More interactables added every refill.");
            InteractableRefillMinutes = c.Bind("5. Chests", "Refill Minutes", 6f, "How often interactables are topped up.");

            BossHealth = c.Bind("6. Hurricane", "Health", 2500000f, "The Hurricane's health at level 1.");
            BossHealthPerLevel = c.Bind("6. Hurricane", "Health Per Level", 750000f, "Health added per level (monsters level up with time).");
            BossHealthPerExtraPlayer = c.Bind("6. Hurricane", "Health Per Extra Player", 0.3f, "Extra health per player beyond the first (0.3 = +30%).");
            BossArmor = c.Bind("6. Hurricane", "Armor", 40f, "Armour.");
            BossDamage = c.Bind("6. Hurricane", "Damage", 40f, "Base damage (skills deal multiples of it).");
            BossDamagePerLevel = c.Bind("6. Hurricane", "Damage Per Level", 8f, "Damage added per level.");
            BossHeight = c.Bind("6. Hurricane", "Height", 24f, "Size: height at the shoulders in metres.");
            BossWakeRadius = c.Bind("6. Hurricane", "Wake Radius", 130f, "The Hurricane appears when a player comes this close to its lair (m).");
            BossLeash = c.Bind("6. Hurricane", "Leash", 180f, "It never strays further than this from its lair (m).");
            BossMoveSpeed = c.Bind("6. Hurricane", "Move Speed", 9f, "Walking speed.");

            BuildKey = c.Bind("7. Building", "Build Key", "B", "Opens/closes build mode (Unity key name).");
            PlaceKey = c.Bind("7. Building", "Place Key", "F", "Places the selected building.");
            RotateKey = c.Bind("7. Building", "Rotate Key", "R", "Turns the building 45 degrees.");
            TurretCost = c.Bind("7. Building", "Turret Cost", 120, "Auto Turret (an Engineer turret that copies your items), base cost.");
            WallCost = c.Bind("7. Building", "Wall Cost", 25, "Barrier Wall base cost.");
            DepotCost = c.Bind("7. Building", "Supply Depot Cost", 100, "Supply Depot base cost.");
            DefenderCost = c.Bind("7. Building", "Defender Squad Cost", 80, "3 marines guarding the tower, base cost.");
            EscortCost = c.Bind("7. Building", "Escort Squad Cost", 60, "2 marines following you, base cost.");
            TurretLimit = c.Bind("7. Building", "Turret Limit", 6, "Per player.");
            WallLimit = c.Bind("7. Building", "Wall Limit", 30, "Per player.");
            DepotLimit = c.Bind("7. Building", "Supply Depot Limit", 4, "Per player.");
            DefenderLimit = c.Bind("7. Building", "Defender Squad Limit", 4, "Per player.");
            EscortLimit = c.Bind("7. Building", "Escort Squad Limit", 8, "Per player.");
            WallHealth = c.Bind("7. Building", "Wall Health", 2000f, "Barrier Wall health at level 1.");
            DepotIncome = c.Bind("7. Building", "Supply Depot Income", 3f, "Money each depot pays its builder per tick (scaled by difficulty like chest costs).");
            DepotIncomeSeconds = c.Bind("7. Building", "Supply Depot Tick Seconds", 5f, "Seconds between payments.");

            Cutscene = c.Bind("8. Storm", "Lander Cutscene", true, "Play the dropship cutscene at the start of a run.");
            RadioAltVoice = c.Bind("8. Storm", "Alternate Radio Voice", false, "Use the British voice for the radio (radio_alt.wav).");
            HeavyRain = c.Bind("8. Storm", "Heavier Rain Outside The Shield", true, "Rain and fog get heavier away from the control tower.");
            RainDensity = c.Bind("8. Storm", "Rain Density", 1f, "Rain particle multiplier (lower for performance).");
            LightningMinSeconds = c.Bind("8. Storm", "Lightning Min Seconds", 4f, "Shortest time between lightning strikes in the distance.");
            LightningMaxSeconds = c.Bind("8. Storm", "Lightning Max Seconds", 12f, "Longest time between lightning strikes.");

            CustomSounds = c.Bind("9. Sounds", "Custom Sounds", true, "Play the mod's wav sounds (Windows only).");
            MasterVolume = c.Bind("9. Sounds", "Master Volume", 50f, "Volume of every custom sound (0-100).");
            VoiceVolume = c.Bind("9. Sounds", "Radio Volume", 90f, "The radio voice.");
            StormVolume = c.Bind("9. Sounds", "Storm Volume", 55f, "Rain, wind and thunder.");
            BossVolume = c.Bind("9. Sounds", "Boss Volume", 80f, "The Hurricane's roars and attacks.");

            NodeSpacing = c.Bind("10. Map", "Monster Path Spacing", 10f, "Spacing of the monster pathing grid built when the map loads (m). Smaller = better paths, slower loading.");
            UseGameMaterials = c.Bind("10. Map", "Use Game Terrain Materials", true, "Dress the map with the game's own moss, rock and water materials. Off = plain colours.");
        }
    }
}
