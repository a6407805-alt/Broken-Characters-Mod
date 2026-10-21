using System;
using System.Reflection;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace DuelistMod
{
    /// <summary>
    /// While a duel is on, nothing in it regenerates: the duelists' LP (health), every enemy monster
    /// (and so the enemy's LP), and the duelists' allies, turrets and drones. Health regen, healing items
    /// and shield recharge are all held back. Only the duel's own cards (heal spells, Mending, Regrowth)
    /// can restore LP; they set Allow while they heal. Runs on the host, where health is decided.
    /// </summary>
    public static class DuelRegen
    {
        /// <summary>True while one of the duel's own heals is being applied.</summary>
        public static bool Allow;

        private static PropertyInfo _shieldProp;

        /// <summary>Is this body part of a duel right now?</summary>
        public static bool Frozen(HealthComponent hc)
        {
            if (!DuelConfig.NoRegenInDuel.Value || DuelDimensionState.ActiveDuelists.Count == 0 || !hc || !hc.body) return false;
            CharacterBody b = hc.body;
            if (DuelDimensionState.ActiveDuelists.Contains(b)) return true;
            TeamIndex team = b.teamComponent ? b.teamComponent.teamIndex : TeamIndex.None;
            if (team == TeamIndex.Player)
            {
                // a duelist's minions: turrets, drones, partners
                CharacterMaster m = b.master;
                CharacterMaster owner = m && m.minionOwnership ? m.minionOwnership.ownerMaster : null;
                CharacterBody ob = owner ? owner.GetBody() : null;
                return ob && DuelDimensionState.ActiveDuelists.Contains(ob);
            }
            // every enemy on the map is a card in the duel
            return team != TeamIndex.None && team != TeamIndex.Neutral;
        }

        /// <summary>Wraps one of the duel's own heals so it goes through.</summary>
        public static void Allowed(Action heal)
        {
            bool old = Allow;
            Allow = true;
            try { heal(); }
            finally { Allow = old; }
        }

        public static void Apply(Harmony harmony)
        {
            MethodInfo heal = AccessTools.Method(typeof(HealthComponent), "Heal", new[] { typeof(float), typeof(ProcChainMask), typeof(bool) });
            if (heal != null)
            {
                harmony.Patch(heal, prefix: new HarmonyMethod(typeof(DuelRegen).GetMethod(nameof(HealPrefix))));
                DuelistPlugin.Log.LogInfo("Patched HealthComponent.Heal (no regen during duels).");
            }
            else DuelistPlugin.Log.LogWarning("HealthComponent.Heal not found: health will regenerate in duels.");

            MethodInfo tick = AccessTools.Method(typeof(HealthComponent), "ServerFixedUpdate");
            _shieldProp = AccessTools.Property(typeof(HealthComponent), "Networkshield");
            if (tick != null && _shieldProp != null && _shieldProp.CanWrite)
            {
                harmony.Patch(tick, prefix: new HarmonyMethod(typeof(DuelRegen).GetMethod(nameof(TickPrefix))),
                                    postfix: new HarmonyMethod(typeof(DuelRegen).GetMethod(nameof(TickPostfix))));
                DuelistPlugin.Log.LogInfo("Patched shield recharge (no shield regen during duels).");
            }
            else DuelistPlugin.Log.LogWarning("Shield recharge hook not found: shields will still recharge in duels.");
        }

        public static bool HealPrefix(HealthComponent __instance, ref float __result)
        {
            if (Allow || !Frozen(__instance)) return true;
            __result = 0f;
            return false;
        }

        public static void TickPrefix(HealthComponent __instance, out float __state)
        {
            __state = __instance ? __instance.shield : 0f;
        }

        public static void TickPostfix(HealthComponent __instance, float __state)
        {
            try
            {
                if (Allow || !__instance || __instance.shield <= __state + 0.001f || !Frozen(__instance)) return;
                _shieldProp.SetValue(__instance, __state);
            }
            catch { }
        }
    }
}
