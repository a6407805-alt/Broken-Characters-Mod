using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    /// <summary>
    /// Winning a duel is hard, so it pays out big (all on the host, so everyone sees it):
    /// a shower of item drops (guaranteed legendary + uncommons, one more if you beat a boss),
    /// an instant level up, a full heal and "Victor's Glory" (a burst of speed and attack speed).
    /// The gold jackpot is paid through DuelNet.GiveMoney by the duel itself.
    /// </summary>
    public static class DuelRewards
    {
        /// <summary>Gold jackpot for a win: worth this many small chests at the current difficulty.</summary>
        public static uint Jackpot()
        {
            if (!Run.instance) return 0;
            return (uint)Mathf.Max(0, Run.instance.GetDifficultyScaledCost(25) * Mathf.Max(0f, DuelConfig.VictoryJackpotChests.Value));
        }

        public static int Legendaries(bool boss) => Mathf.Max(0, DuelConfig.VictoryLegendary.Value) + (boss ? 1 : 0);
        public static int Uncommons => Mathf.Max(0, DuelConfig.VictoryUncommon.Value);

        /// <summary>Host only: drops, level, heal and buff for the winner.</summary>
        public static void GrantOnServer(CharacterBody winner, bool boss)
        {
            if (!NetworkServer.active || !winner || !Run.instance) return;
            Vector3 at = winner.corePosition + Vector3.up * 1.5f;

            List<PickupIndex> drops = new List<PickupIndex>();
            for (int i = 0; i < Legendaries(boss); i++) Add(drops, Run.instance.availableTier3DropList);
            for (int i = 0; i < Uncommons; i++) Add(drops, Run.instance.availableTier2DropList);
            for (int i = 0; i < drops.Count; i++)
            {
                // a fountain of loot around the winner, like a boss kill
                float ang = (360f / drops.Count) * i * Mathf.Deg2Rad;
                Vector3 vel = new Vector3(Mathf.Cos(ang) * 7f, 20f, Mathf.Sin(ang) * 7f);
                try { PickupDropletController.CreatePickupDroplet(drops[i], at, vel); }
                catch (System.Exception e) { DuelistPlugin.Log.LogWarning("Victory drop failed: " + e.Message); }
            }

            if (DuelConfig.VictoryLevelUp.Value && TeamManager.instance)
            {
                try
                {
                    TeamIndex team = winner.teamComponent ? winner.teamComponent.teamIndex : TeamIndex.Player;
                    ulong now = TeamManager.instance.GetTeamExperience(team);
                    ulong next = TeamManager.instance.GetTeamNextLevelExperience(team);
                    if (next > now) TeamManager.instance.GiveTeamExperience(team, next - now);
                }
                catch (System.Exception e) { DuelistPlugin.Log.LogWarning("Victory level up failed: " + e.Message); }
            }

            if (winner.healthComponent) DuelRegen.Allowed(() => winner.healthComponent.HealFraction(1f, default(ProcChainMask)));
            float glory = Mathf.Max(0f, DuelConfig.VictoryBuffSeconds.Value);
            if (glory > 0f) winner.AddTimedBuff(RoR2Content.Buffs.WarCryBuff, glory);
        }

        // ------------------------------------------------------------ bounties for each monster defeated

        public static string RarityName(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "Rare";
                case Rarity.SuperRare: return "Super Rare";
                case Rarity.UltraRare: return "Ultra Rare";
                case Rarity.SecretRare: return "Secret Rare";
                default: return "Common";
            }
        }

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.45f, 0.75f, 1f);
                case Rarity.SuperRare: return new Color(0.8f, 0.5f, 1f);
                case Rarity.UltraRare: return new Color(1f, 0.82f, 0.3f);
                case Rarity.SecretRare: return new Color(1f, 0.45f, 0.75f);
                default: return new Color(0.95f, 0.9f, 0.75f);
            }
        }

        /// <summary>
        /// A monster's bounty. Gold scales with its Level and rarity (and the run's difficulty, like chests);
        /// items: Common Lv 1-12 has a small chance at a white, Rare likely a white or green, Super Rare a green,
        /// Ultra Rare a green and maybe a red, Secret Rare a red.
        /// </summary>
        public static void RollBounty(int level, Rarity rarity, out uint gold, out int white, out int green, out int red)
        {
            float rarMult = new[] { 1f, 1.6f, 2.5f, 4f, 6f }[(int)rarity];
            int baseCost = Run.instance ? Run.instance.GetDifficultyScaledCost(Mathf.Max(1, level) * 3) : level * 3;
            gold = (uint)Mathf.RoundToInt(baseCost * rarMult);
            white = green = red = 0;
            float roll = UnityEngine.Random.value;
            switch (rarity)
            {
                case Rarity.Common:
                    if (roll < 0.06f + level * 0.025f) white = 1;
                    break;
                case Rarity.Rare:
                    if (roll < 0.3f + level * 0.02f) green = 1; else white = 1;
                    break;
                case Rarity.SuperRare:
                    green = 1;
                    if (UnityEngine.Random.value < 0.1f + level * 0.02f) red = 1;
                    break;
                case Rarity.UltraRare:
                    green = 1;
                    if (UnityEngine.Random.value < 0.35f + level * 0.03f) red = 1;
                    break;
                case Rarity.SecretRare:
                    red = 1; green = 1;
                    break;
            }
        }

        /// <summary>Host only: the bounty items burst out at the duelist's feet.</summary>
        public static void DropBounty(CharacterBody winner, int white, int green, int red)
        {
            if (!NetworkServer.active || !winner || !Run.instance) return;
            List<PickupIndex> drops = new List<PickupIndex>();
            for (int i = 0; i < red; i++) Add(drops, Run.instance.availableTier3DropList);
            for (int i = 0; i < green; i++) Add(drops, Run.instance.availableTier2DropList);
            for (int i = 0; i < white; i++) Add(drops, Run.instance.availableTier1DropList);
            Vector3 at = winner.corePosition + Vector3.up * 1.5f;
            for (int i = 0; i < drops.Count; i++)
            {
                float ang = (360f / drops.Count) * i * Mathf.Deg2Rad + 0.4f;
                Vector3 vel = new Vector3(Mathf.Cos(ang) * 6f, 18f, Mathf.Sin(ang) * 6f);
                try { PickupDropletController.CreatePickupDroplet(drops[i], at, vel); }
                catch (System.Exception e) { DuelistPlugin.Log.LogWarning("Bounty drop failed: " + e.Message); }
            }
        }

        private static void Add(List<PickupIndex> into, List<PickupIndex> from)
        {
            if (from == null || from.Count == 0) return;
            into.Add(Run.instance.treasureRng.NextElementUniform(from));
        }
    }
}
