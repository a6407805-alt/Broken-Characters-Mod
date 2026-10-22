using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ExpansionManagement;
using UnityEngine;
using UnityEngine.Networking;

namespace SpecialForces
{
    // The host's game: places the tower, the Storm Nests, the lair bosses and the Hurricane; sends waves at the
    // tower that grow over time; drops loot from nests and lairs and regrows them; tops up chests; respawns dead
    // players at the outpost; ends the run (tower destroyed = lost, Hurricane killed = won). Server only.
    public class SFDirector : MonoBehaviour
    {
        public static SFDirector Instance;

        private class Pool
        {
            public readonly List<CharacterSpawnCard> basic = new List<CharacterSpawnCard>();
            public readonly List<CharacterSpawnCard> mini = new List<CharacterSpawnCard>();
            public readonly List<CharacterSpawnCard> champions = new List<CharacterSpawnCard>();
        }

        private class Site
        {
            public string name;
            public Vector3 position;
            public int tier;
            public bool lair;
            public CharacterMaster structure;     // the nest (null for a lair)
            public CharacterMaster boss;          // the lair's boss
            public readonly List<CharacterMaster> guards = new List<CharacterMaster>();
            public float regrowAt = -1f;
            public float nextGuard;
            public bool alive;
        }

        private readonly Pool pool = new Pool();
        private readonly List<Site> sites = new List<Site>();
        private readonly List<CharacterMaster> waveMonsters = new List<CharacterMaster>();
        private readonly Dictionary<CharacterMaster, float> deadSince = new Dictionary<CharacterMaster, float>();
        private readonly Dictionary<CharacterMaster, uint> keptMoney = new Dictionary<CharacterMaster, uint>();
        private ulong keptExperience;
        private CharacterMaster tower;
        private CharacterMaster hurricane;
        private Vector3 hurricaneLair;
        private bool hurricaneAwake;
        private bool started, ended;
        private int wave;
        private float nextWave, nextAggro, nextState, nextRefill, nextDepotTick, startTime;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GlobalEventManager.onCharacterDeathGlobal -= OnDeath;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || ended || !Run.instance) return;
            if (!started)
            {
                if (!Stage.instance || !DirectorCore.instance) return;     // wait for the game's stage to begin
                Begin();
                return;
            }
            float now = Time.fixedTime;
            if (now >= nextWave) SendWave();
            if (now >= nextAggro) { nextAggro = now + 1f; Aggro(); }
            if (now >= nextState) { nextState = now + 1f; StateMessage.Send(wave, Mathf.Max(0, nextWave - now), sites.Count(s => !s.lair && s.alive), sites.Count(s => !s.lair)); }
            if (now >= nextRefill) { nextRefill = now + SFConfig.InteractableRefillMinutes.Value * 60f; if (now - startTime > 5f) Refill(); }
            if (now >= nextDepotTick) { nextDepotTick = now + Mathf.Max(1f, SFConfig.DepotIncomeSeconds.Value); SFBuilding.ServerDepotIncome(); }
            Sites(now);
            Respawns(now);
            if (!hurricaneAwake) WakeHurricane();
            KeepProgress();
        }

        // The user (1.0.2): "money and levels should not be reset on death". Remember each player's money while they
        // live and the team's experience; if either comes back lower after a death/respawn (another mod's revive,
        // a respawn path that rebuilds the player), put it back.
        private void KeepProgress()
        {
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                var m = pc.master;
                if (!m) continue;
                bool alive = m.GetBody();
                keptMoney.TryGetValue(m, out uint kept);
                bool returned = alive && wasDead.Remove(m);
                if (returned && m.money < kept)
                {
                    SpecialForcesPlugin.Log.LogInfo($"Kept {pc.networkUser?.userName}'s money through death: {m.money} -> {kept}.");
                    m.money = kept;
                }
                if (alive) keptMoney[m] = m.money;
                else wasDead.Add(m);
            }
            if (TeamManager.instance)
            {
                ulong xp = TeamManager.instance.GetTeamExperience(TeamIndex.Player);
                if (xp < keptExperience)
                {
                    SpecialForcesPlugin.Log.LogInfo($"Kept the team's experience through death: {xp} -> {keptExperience}.");
                    TeamManager.instance.GiveTeamExperience(TeamIndex.Player, keptExperience - xp);
                }
                else keptExperience = xp;
            }
        }
        private readonly HashSet<CharacterMaster> wasDead = new HashSet<CharacterMaster>();

        // 1.0.1 spawned the Hurricane (and its boss bar) at the start. It now waits in the storm until a player
        // reaches the Storm Plain.
        private void WakeHurricane()
        {
            float radius = SFConfig.BossWakeRadius.Value;
            foreach (var player in PlayerCharacterMasterController.instances)
            {
                var body = player.master ? player.master.GetBody() : null;
                if (!body || (body.footPosition - hurricaneLair).sqrMagnitude > radius * radius) continue;
                hurricaneAwake = true;
                hurricane = SFBoss.SpawnHurricane(hurricaneLair, OnHurricaneDefeated);
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "HURRICANE_AWAKE" });
                SpecialForcesPlugin.Log.LogInfo($"The Hurricane woke at wave {wave} ({(bool)hurricane}).");
                return;
            }
        }

        // ------------------------------------------------------------------ start

        private void Begin()
        {
            started = true;
            startTime = Time.fixedTime;
            BuildPool();
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;

            Vector3 towerPos = SFUtil.Ground(SFScene.TowerPosition, 20f);
            tower = SFStructures.Spawn(SFStructures.TowerMaster, towerPos, Quaternion.identity, TeamIndex.Player);
            if (tower)
            {
                int extra = Mathf.RoundToInt(SFConfig.TowerHealthPerExtraPlayer.Value * 10f * (SFUtil.PlayerCount - 1));
                if (extra > 0) tower.inventory.GiveItem(RoR2Content.Items.BoostHp, extra);
                var body = tower.GetBody();
                if (body) body.gameObject.AddComponent<TowerRegen>();
            }
            else SpecialForcesPlugin.Log.LogError("Control tower could not be placed.");

            foreach (var pair in SFScene.MarkersStartingWith("SF_Nest_T"))
            {
                int tier = pair.Key[9] - '0';
                sites.Add(new Site { name = pair.Key, position = SFUtil.Ground(pair.Value, 20f), tier = Mathf.Clamp(tier, 1, 4) });
            }
            int lairTier = 3;
            foreach (var pair in SFScene.MarkersStartingWith("SF_Lair_"))
                sites.Add(new Site { name = pair.Key, position = SFUtil.Ground(pair.Value, 20f), tier = lairTier++, lair = true });
            foreach (var site in sites) Grow(site);

            hurricaneLair = SFUtil.Ground(SFScene.Marker("SF_BossLair", SFScene.TowerPosition + Vector3.forward * 700f), 30f);

            foreach (var p in PlayerCharacterMasterController.instances) if (p.master) p.master.preventGameOver = true;
            nextWave = Time.fixedTime + SFConfig.FirstWaveSeconds.Value + SFCutscene.Length;
            nextRefill = Time.fixedTime + SFConfig.InteractableRefillMinutes.Value * 60f;
            SpecialForcesPlugin.Log.LogInfo($"Special Forces started: tower {(bool)tower}, {sites.Count(s => !s.lair)} nests, {sites.Count(s => s.lair)} lairs, " +
                                            $"hurricane waits at {hurricaneLair} (wakes within {SFConfig.BossWakeRadius.Value} m), monster pool {pool.basic.Count}/{pool.mini.Count}/{pool.champions.Count} (basic/mini/champion), {SFUtil.PlayerCount} players.");
        }

        // Monsters for waves and nests: every card from several vanilla stages' lists (expansion-checked), so the
        // mix matches what the game (and its DLCs) has. Monster mods that add to these lists are included too.
        private void BuildPool()
        {
            var seen = new HashSet<string>();
            foreach (string key in Keys.MonsterLists)
            {
                var dccs = SFUtil.Load<DirectorCardCategorySelection>(key, "monster list");
                if (!dccs) continue;
                foreach (var category in dccs.categories)
                {
                    List<CharacterSpawnCard> into = category.name.Contains("Champion") ? pool.champions
                        : category.name.Contains("Miniboss") ? pool.mini : category.name.Contains("Basic") ? pool.basic : null;
                    if (into == null || category.cards == null) continue;
                    foreach (var card in category.cards)
                    {
                        var csc = SFUtil.CardOf(card) as CharacterSpawnCard;
                        if (!csc || !csc.prefab || !seen.Add(csc.name)) continue;
                        if (!Available(csc.prefab)) continue;
                        into.Add(csc);
                    }
                }
            }
        }

        private static bool Available(GameObject masterPrefab)
        {
            foreach (var go in new[] { masterPrefab, masterPrefab.GetComponent<CharacterMaster>()?.bodyPrefab })
            {
                if (!go) continue;
                var req = go.GetComponent<ExpansionRequirementComponent>();
                if (req && req.requiredExpansion && !Run.instance.IsExpansionEnabled(req.requiredExpansion)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ waves

        private void SendWave()
        {
            wave++;
            float interval = Mathf.Max(SFConfig.WaveIntervalMin.Value, SFConfig.WaveInterval.Value - SFConfig.WaveIntervalStep.Value * (wave - 1));
            nextWave = Time.fixedTime + interval;
            waveMonsters.RemoveAll(m => !m || !m.GetBody());
            if (waveMonsters.Count >= SFConfig.MaxWaveMonsters.Value)
            {
                SpecialForcesPlugin.Log.LogInfo($"Wave {wave} held back: {waveMonsters.Count} wave monsters still alive.");
                return;
            }

            float credits = SFConfig.WaveCredits.Value * (1f + SFConfig.WaveCreditsPerWave.Value * (wave - 1)) *
                            (1f + SFConfig.WaveCreditsPerExtraPlayer.Value * (SFUtil.PlayerCount - 1)) *
                            Mathf.Max(1f, Run.instance.difficultyCoefficient * 0.5f);
            var gates = SFScene.MarkersStartingWith("SF_WaveGate_").Select(p => p.Value).ToList();
            if (gates.Count == 0) gates.Add(SFScene.Marker("SF_BossLair", SFScene.TowerPosition + Vector3.forward * 400f));
            Vector3 gate = SFUtil.Ground(gates[Random.Range(0, gates.Count)], 30f);
            float eliteChance = Mathf.Min(SFConfig.EliteChanceMax.Value, SFConfig.EliteChanceBase.Value + SFConfig.EliteChancePerWave.Value * (wave - 1));

            int spawned = 0;
            // Mini-boss waves: champions first, more of them later on (every 3rd wave after half an hour).
            int every = Time.fixedTime - startTime > 1800f ? Mathf.Max(2, SFConfig.MiniBossEveryNWaves.Value - 2) : SFConfig.MiniBossEveryNWaves.Value;
            if (every > 0 && wave % every == 0 && pool.champions.Count > 0)
            {
                int bosses = 1 + wave / 15;
                for (int i = 0; i < bosses; i++)
                {
                    var card = pool.champions[Random.Range(0, pool.champions.Count)];
                    if (SpawnWaveMonster(card, gate, eliteChance)) spawned++;
                    credits -= card.directorCreditCost * 0.5f;
                }
            }
            var choices = new List<CharacterSpawnCard>(pool.basic);
            if (wave >= 3) choices.AddRange(pool.mini);
            int guard = 0;
            while (credits > 0 && choices.Count > 0 && guard++ < 200 && waveMonsters.Count < SFConfig.MaxWaveMonsters.Value)
            {
                var affordable = choices.Where(c => c.directorCreditCost <= credits).ToList();
                if (affordable.Count == 0) break;
                // Prefer pricier monsters as waves grow (fewer, tougher bodies instead of a flood).
                affordable.Sort((a, b) => a.directorCreditCost.CompareTo(b.directorCreditCost));
                int pick = Mathf.Clamp(Mathf.FloorToInt(Mathf.Pow(Random.value, Mathf.Lerp(1.5f, 0.6f, Mathf.Clamp01(wave / 30f))) * affordable.Count), 0, affordable.Count - 1);
                var card = affordable[pick];
                credits -= card.directorCreditCost;
                if (SpawnWaveMonster(card, gate, eliteChance)) spawned++;
            }
            SpecialForcesPlugin.Log.LogInfo($"Wave {wave}: {spawned} monsters from {gate} (next in {interval:0} s, elite chance {eliteChance:P0}).");
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "WAVE_INCOMING", paramTokens = new[] { wave.ToString() } });
        }

        private bool SpawnWaveMonster(CharacterSpawnCard card, Vector3 at, float eliteChance)
        {
            CharacterMaster m = SpawnMonster(card, at, 30f, eliteChance, wave >= SFConfig.Tier2ElitesFromWave.Value);
            if (!m) return false;
            waveMonsters.Add(m);
            return true;
        }

        public static CharacterMaster SpawnMonster(CharacterSpawnCard card, Vector3 at, float spread, float eliteChance, bool tier2)
        {
            var rule = new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                position = at,
                minDistance = 0f,
                maxDistance = spread,
            };
            var request = new DirectorSpawnRequest(card, rule, Run.instance.spawnRng)
            {
                teamIndexOverride = TeamIndex.Monster,
                ignoreTeamMemberLimit = true,
            };
            GameObject spawned = DirectorCore.instance ? DirectorCore.instance.TrySpawnObject(request) : null;
            var master = spawned ? spawned.GetComponent<CharacterMaster>() : null;
            float costMultiplier = 1f;
            if (master && Random.value < eliteChance) costMultiplier = MakeElite(master, tier2);
            if (master)
            {
                try { SetRewards(master, card.directorCreditCost * costMultiplier); }
                catch (System.Exception e) { SpecialForcesPlugin.Log.LogWarning("Kill rewards not set: " + e.Message); }
            }
            return master;
        }

        // The user (1.0.3): "no xp or gold per kill". The combat director fills in a monster's DeathRewards when it
        // spawns one; spawning through DirectorCore directly (waves, guards, lairs) leaves them at 0. Same formula as
        // CombatDirector (cost x 0.2, gold twice the experience, scaled by the run's difficulty).
        private static void SetRewards(CharacterMaster master, float cost)
        {
            var body = master.GetBody();
            var rewards = body ? body.GetComponent<DeathRewards>() : null;
            if (!rewards) return;
            float value = cost * 0.2f * SFConfig.KillRewards.Value;
            float difficulty = Run.instance ? Run.instance.compensatedDifficultyCoefficient : 1f;
            rewards.spawnValue = (int)Mathf.Max(1f, value);
            rewards.expReward = (uint)Mathf.Max(1f, value * difficulty);
            rewards.goldReward = (uint)Mathf.Max(1f, value * 2f * difficulty);
        }

        // Same as the game's combat director: the elite's equipment plus health/damage boost items.
        public static float MakeElite(CharacterMaster master, bool allowTier2)
        {
            var tiers = SFUtil.GetStatic<CombatDirector.EliteTierDef[]>(typeof(CombatDirector), "eliteTiers");
            if (tiers == null) return 1f;
            var options = new List<(EliteDef def, float cost)>();
            foreach (var tier in tiers)
            {
                if (tier == null || tier.eliteTypes == null || tier.costMultiplier <= 1.01f) continue;
                bool second = tier.costMultiplier > 10f;
                if (second && !allowTier2) continue;
                foreach (var def in tier.eliteTypes)
                    if (def && def.eliteEquipmentDef && def.IsAvailable()) options.Add((def, tier.costMultiplier));
            }
            if (options.Count == 0) return 1f;
            var (elite, eliteCost) = options[Random.Range(0, options.Count)];
            master.inventory.SetEquipmentIndex(elite.eliteEquipmentDef.equipmentIndex);
            master.inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt((elite.healthBoostCoefficient - 1f) * 10f));
            master.inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt((elite.damageBoostCoefficient - 1f) * 10f));
            return eliteCost;
        }

        // Wave monsters march on the tower unless a player, ally or building is close: then they fight that.
        private void Aggro()
        {
            CharacterBody towerBody = tower ? tower.GetBody() : null;
            if (!towerBody) return;
            float range = SFConfig.TowerAggroRange.Value;
            var allies = TeamComponent.GetTeamMembers(TeamIndex.Player);
            foreach (var m in waveMonsters)
            {
                if (!m) continue;
                var body = m.GetBody();
                if (!body) continue;
                bool distracted = false;
                foreach (var ally in allies)
                {
                    if (ally && ally.body && ally.body != towerBody && (ally.body.corePosition - body.corePosition).sqrMagnitude < range * range)
                    {
                        distracted = true;
                        break;
                    }
                }
                if (distracted) continue;
                foreach (var ai in m.GetComponents<BaseAI>())
                {
                    if (!ai) continue;
                    ai.currentEnemy.gameObject = towerBody.gameObject;
                }
            }
        }

        // ------------------------------------------------------------------ nests and lairs

        private void Grow(Site site)
        {
            site.alive = true;
            site.regrowAt = -1f;
            site.guards.Clear();
            if (site.lair)
            {
                if (pool.champions.Count == 0) return;
                var card = pool.champions[Random.Range(0, pool.champions.Count)];
                site.boss = SpawnMonster(card, site.position, 10f, 0.5f, wave >= SFConfig.Tier2ElitesFromWave.Value);
                if (site.boss) site.boss.inventory.GiveItem(RoR2Content.Items.BoostHp, 10 * site.tier);   // a tougher-than-usual boss
                return;
            }
            site.structure = SFStructures.Spawn(SFStructures.NestMaster, site.position, Quaternion.Euler(0, Random.Range(0f, 360f), 0), TeamIndex.Monster);
            if (site.structure)
            {
                site.structure.inventory.GiveItem(RoR2Content.Items.BoostHp, (site.tier - 1) * 10);
                var marker = site.structure.GetBody()?.GetComponent<SFStructure>();
                if (marker) { marker.marker = site.name; marker.tier = site.tier; }
            }
            for (int i = 0; i < 3 + site.tier; i++) SpawnGuard(site);
        }

        private void SpawnGuard(Site site)
        {
            var list = site.tier >= 3 && pool.mini.Count > 0 && Random.value < 0.5f ? pool.mini : pool.basic;
            if (list.Count == 0) return;
            // Higher tiers favour pricier monsters.
            var sorted = list.OrderBy(c => c.directorCreditCost).ToList();
            int pick = Mathf.Clamp(Mathf.FloorToInt(Mathf.Lerp(0f, sorted.Count - 1, (site.tier - 1) / 3f) + Random.Range(-2, 3)), 0, sorted.Count - 1);
            // 1.0.2 was too deadly at the start ("enemies one-shotting me"): tier-4 nests could field Malachite and
            // Celestine elites in the first minute. Elites now follow the wave count like the waves do.
            float minutes = (Time.fixedTime - startTime) / 60f;
            float eliteChance = Mathf.Min(0.08f * site.tier, 0.02f * site.tier * (1f + minutes / 3f));
            var guard = SpawnMonster(sorted[pick], site.position, 18f, eliteChance, site.tier >= 4 && wave >= SFConfig.Tier2ElitesFromWave.Value);
            if (guard) site.guards.Add(guard);
        }

        private void Sites(float now)
        {
            foreach (var site in sites)
            {
                if (!site.alive)
                {
                    if (site.regrowAt > 0 && now >= site.regrowAt) Grow(site);
                    continue;
                }
                if (site.lair) continue;
                site.guards.RemoveAll(g => !g || !g.GetBody());
                if (now >= site.nextGuard && site.guards.Count < 3 + site.tier && PlayerNear(site.position, 90f))
                {
                    site.nextGuard = now + SFConfig.NestGuardRespawnSeconds.Value;
                    SpawnGuard(site);
                }
            }
        }

        private static bool PlayerNear(Vector3 p, float range)
        {
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                var b = pc.master ? pc.master.GetBody() : null;
                if (b && (b.corePosition - p).sqrMagnitude < range * range) return true;
            }
            return false;
        }

        private void OnDeath(DamageReport report)
        {
            if (!NetworkServer.active || ended || report == null) return;
            var victim = report.victimBody;
            if (!victim) return;
            var structure = victim.GetComponent<SFStructure>();
            if (structure && structure.kind == StructureKind.Tower)
            {
                Lose();
                return;
            }
            foreach (var site in sites)
            {
                if (!site.alive) continue;
                bool nestDied = !site.lair && site.structure && report.victimMaster == site.structure;
                bool bossDied = site.lair && site.boss && report.victimMaster == site.boss;
                if (!nestDied && !bossDied) continue;
                site.alive = false;
                site.regrowAt = Time.fixedTime + (site.lair ? SFConfig.LairRegrowMinutes.Value : SFConfig.NestRegrowMinutes.Value) * 60f;
                Loot(site, victim.corePosition);
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + (site.lair ? "LAIR_CLEARED" : "NEST_DESTROYED"), paramTokens = new[] { site.tier.ToString() } });
            }
        }

        private static void Loot(Site site, Vector3 at)
        {
            var run = Run.instance;
            var drops = new List<PickupIndex>();
            void Add(List<PickupIndex> list, int n) { for (int i = 0; i < n && list != null && list.Count > 0; i++) drops.Add(list[Random.Range(0, list.Count)]); }
            if (site.lair)
            {
                Add(run.availableTier3DropList, 1);
                Add(run.availableBossDropList.Count > 0 ? run.availableBossDropList : run.availableTier3DropList, 1);
            }
            else
            {
                switch (site.tier)
                {
                    case 1: Add(run.availableTier1DropList, 2); break;
                    case 2: Add(run.availableTier1DropList, 2); Add(run.availableTier2DropList, 1); break;
                    case 3: Add(run.availableTier2DropList, 2); if (Random.value < 0.25f) Add(run.availableTier3DropList, 1); break;
                    default: Add(run.availableTier3DropList, 1); Add(run.availableTier2DropList, 2); break;
                }
            }
            for (int i = 0; i < drops.Count; i++)
            {
                float a = i / (float)drops.Count * Mathf.PI * 2f;
                PickupDropletController.CreatePickupDroplet(drops[i], at + Vector3.up * 3f, new Vector3(Mathf.Cos(a) * 9f, 22f, Mathf.Sin(a) * 9f));
            }
            uint gold = (uint)run.GetDifficultyScaledCost(Mathf.RoundToInt(SFConfig.NestGoldPerTier.Value * site.tier * (site.lair ? 2 : 1)));
            foreach (var pc in PlayerCharacterMasterController.instances) if (pc.master) pc.master.GiveMoney(gold);
        }

        // ------------------------------------------------------------------ chests, respawns, the end

        private void Refill()
        {
            var director = FindObjectOfType<SceneDirector>();
            if (!director) return;
            director.monsterCredit = 0;
            var prop = typeof(SceneDirector).GetProperty("interactableCredit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (prop != null) prop.SetValue(director, SFConfig.InteractableRefillCredits.Value);
            try { SFUtil.Call(director, "PopulateScene"); }
            catch (System.Exception e) { SpecialForcesPlugin.Log.LogWarning("Chest refill failed: " + e.Message); }
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "REFILL" });
        }

        private void Respawns(float now)
        {
            if (SFConfig.RespawnSeconds.Value <= 0f || !tower || !tower.GetBody()) return;
            foreach (var pc in PlayerCharacterMasterController.instances)
            {
                var m = pc.master;
                if (!m) continue;
                m.preventGameOver = true;          // the run only ends with the tower (or the Hurricane)
                if (m.GetBody() || !m.IsDeadAndOutOfLivesServer()) { deadSince.Remove(m); continue; }
                if (!deadSince.TryGetValue(m, out float since)) { deadSince[m] = now; continue; }
                if (now - since < SFConfig.RespawnSeconds.Value) continue;
                deadSince.Remove(m);
                Vector3 p = SFUtil.Ground(SFScene.LanderPad + new Vector3(Random.Range(-12f, 12f), 0, Random.Range(-12f, 12f)), 20f);
                m.Respawn(p, Quaternion.identity, true);
                Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "RESPAWNED", paramTokens = new[] { pc.networkUser ? pc.networkUser.userName : "?" } });
            }
        }

        private void Lose()
        {
            if (ended) return;
            ended = true;
            SpecialForcesPlugin.Log.LogInfo($"The control tower fell at wave {wave}.");
            foreach (var pc in PlayerCharacterMasterController.instances) if (pc.master) pc.master.preventGameOver = false;
            Run.instance.BeginGameOver(RoR2Content.GameEndings.StandardLoss);
        }

        private void OnHurricaneDefeated()
        {
            if (ended) return;
            ended = true;
            SpecialForcesPlugin.Log.LogInfo($"The Hurricane was killed at wave {wave}.");
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = Tokens.Prefix + "HURRICANE_DOWN" });
            Run.instance.BeginGameOver(SFGameMode.Victory ? SFGameMode.Victory : RoR2Content.GameEndings.MainEnding);
        }

        // The Hurricane's Squall: a lair boss and a pack of elites called out of the storm around it.
        public void Squall(Vector3 at)
        {
            if (pool.champions.Count > 0) SpawnMonster(pool.champions[Random.Range(0, pool.champions.Count)], at, 40f, 1f, true);
            for (int i = 0; i < 4 && pool.mini.Count > 0; i++) SpawnMonster(pool.mini[Random.Range(0, pool.mini.Count)], at, 45f, 1f, wave >= SFConfig.Tier2ElitesFromWave.Value);
        }

        public CharacterBody TowerBody => tower ? tower.GetBody() : null;
        public int Wave => wave;
    }

    // Server: the tower heals itself after a while out of combat.
    public class TowerRegen : MonoBehaviour
    {
        private HealthComponent health;
        private float lastHealth, quietSince;

        private void Start()
        {
            health = GetComponent<HealthComponent>();
            lastHealth = health ? health.health : 0f;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !health || !health.alive) return;
            if (health.health < lastHealth - 0.5f) quietSince = Time.fixedTime;
            if (Time.fixedTime - quietSince > 10f && health.health < health.fullHealth)
                health.Heal(health.fullHealth * SFConfig.TowerRegen.Value / 100f * Time.fixedDeltaTime, default(ProcChainMask), false);
            lastHealth = health.health;
        }
    }
}

namespace SpecialForces
{
    internal static class SFSquall
    {
        public static void Call(Vector3 at) => SFDirector.Instance?.Squall(at);
    }
}
