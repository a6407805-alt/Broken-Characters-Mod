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

        private static void Add(List<PickupIndex> into, List<PickupIndex> from)
        {
            if (from == null || from.Count == 0) return;
            into.Add(Run.instance.treasureRng.NextElementUniform(from));
        }
    }
}
