using System.Collections.Generic;
using R2API;
using RoR2;
using RoR2.Audio;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    /// <summary>How an attack from the duel lands on the real monsters out in the world.</summary>
    public enum EchoKind
    {
        Spirit,     // your monsters: a golden spirit-lance from the sky
        Barrage,    // drones: a hail of gunfire from above
        Meteor,     // Fusion: a meteor slams down
        Lightning,  // Synchro: a lightning bolt
        Missiles,   // Xyz: a missile volley
        Chain,      // Link: arcs chain from target to target
        Rune,       // spells and traps: a pillar of arcane light
        Judgment    // direct hits to the enemy's LP: the Pharaoh's judgment from the heavens
    }

    /// <summary>
    /// The duel echoes into the real world, so everyone on the map sees and hears it:
    /// every hit rains down on the real monsters (each kind of monster its own way), summons empower your
    /// teammates, destroying monsters heals them, traps slow the real enemies, and Link monsters fly a war banner.
    /// Effects are networked (EffectManager); buffs and heals go through the host.
    /// </summary>
    public static class DuelEcho
    {
        private static GameObject _lightning, _meteor, _omni, _omniQuick;
        private static NetworkSoundEventDef _sBoom, _sZap, _sPop, _sChime, _sChain;
        private static int _chainFrame = -1;
        private static Vector3 _chainLast;

        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);

        public static void Register()
        {
            _sBoom = Sound("Play_captain_shift_impact");
            _sZap = Sound("Play_loader_R_shock");
            _sPop = Sound("Play_engi_M1_explo");
            _sChime = Sound("Play_UI_item_pickup");
            _sChain = Sound("Play_item_proc_chain_lightning");
        }

        private static NetworkSoundEventDef Sound(string evt)
        {
            NetworkSoundEventDef d = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            d.eventName = evt;
            ContentAddition.AddNetworkSoundEventDef(d);
            return d;
        }

        private static void Play(NetworkSoundEventDef d, Vector3 at)
        {
            try { if (d && d.index != NetworkSoundEventIndex.Invalid) EffectManager.SimpleSoundEffect(d.index, at, true); }
            catch { }
        }

        private static void Assets()
        {
            if (_omni) return;
            _lightning = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/LightningStrikeImpact");
            _meteor = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/MeteorStrikeImpact");
            _omni = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
            _omniQuick = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
        }

        private static void Fx(GameObject prefab, Vector3 at, float scale, Color color = default(Color))
        {
            if (!prefab) prefab = _omni;
            if (!prefab) return;
            try
            {
                EffectData d = new EffectData { origin = at, scale = scale };
                if (color != default(Color)) d.color = color;
                EffectManager.SpawnEffect(prefab, d, true);
            }
            catch { }
        }

        public static bool On => DuelConfig.WorldEcho.Value;

        /// <summary>The kind of echo a card makes.</summary>
        public static EchoKind KindOf(Card c, out Color color)
        {
            color = Gold;
            if (c == null) return EchoKind.Spirit;
            if (c.Type == CardType.Spell) { color = new Color(0.3f, 0.95f, 0.6f); return EchoKind.Rune; }
            if (c.Type == CardType.Trap) { color = new Color(0.8f, 0.35f, 1f); return EchoKind.Rune; }
            switch (c.Extra)
            {
                case ExtraKind.Fusion: color = new Color(1f, 0.5f, 0.15f); return EchoKind.Meteor;
                case ExtraKind.Synchro: color = new Color(0.65f, 0.9f, 1f); return EchoKind.Lightning;
                case ExtraKind.Xyz: color = new Color(1f, 0.35f, 0.3f); return EchoKind.Missiles;
                case ExtraKind.Link: color = new Color(0.3f, 0.55f, 1f); return EchoKind.Chain;
            }
            if (c.Source == "Drone") { color = new Color(1f, 0.75f, 0.4f); return EchoKind.Barrage; }
            return EchoKind.Spirit;
        }

        /// <summary>One hit from the duel landing on one real monster (or the real commander / player).</summary>
        public static void Strike(Vector3 at, EchoKind kind, Color color, float size, bool kill)
        {
            if (!On) return;
            Assets();
            size = Mathf.Clamp(size, 0.6f, 4f);
            Vector3 sky = at + Vector3.up * 45f + Random.insideUnitSphere * 6f;
            switch (kind)
            {
                case EchoKind.Spirit:
                    Partners.Beam(sky, at, color, 0.6f * size);
                    Partners.Impact(at, color, 1.6f * size);
                    Fx(_omniQuick, at, 2f * size);
                    Play(_sPop, at);
                    break;
                case EchoKind.Barrage:
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 hit = at + Random.insideUnitSphere * 1.2f;
                        if (SkillAssets.Tracer)
                            try { EffectManager.SpawnEffect(SkillAssets.Tracer, new EffectData { origin = hit, start = sky + Random.insideUnitSphere * 8f }, true); } catch { }
                    }
                    Fx(_omniQuick, at, 1.8f * size);
                    Play(_sPop, at);
                    break;
                case EchoKind.Meteor:
                    Partners.Beam(sky + Vector3.up * 20f, at, color, 1.6f * size);
                    Fx(_meteor, at, 4f * size);
                    Partners.Pillar(at, color, 1.5f * size);
                    Play(_sBoom, at);
                    break;
                case EchoKind.Lightning:
                    Fx(_lightning, at, 2f * size);
                    Partners.Beam(at + Vector3.up * 60f, at, color, 0.35f * size);
                    Partners.Impact(at, color, 1.4f * size);
                    Play(_sZap, at);
                    break;
                case EchoKind.Missiles:
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = at + Random.insideUnitSphere * 2f;
                        Partners.Beam(at + new Vector3(Random.Range(-25f, 25f), 30f, Random.Range(-25f, 25f)), p, color, 0.3f * size);
                        Fx(_omniQuick, p, 2.2f * size);
                    }
                    Play(_sPop, at);
                    break;
                case EchoKind.Chain:
                    // arcs link this target to the last one hit this frame
                    if (_chainFrame == Time.frameCount && (_chainLast - at).sqrMagnitude < 60f * 60f) Partners.Beam(_chainLast, at, color, 0.4f * size);
                    else Partners.Beam(at + Vector3.up * 40f, at, color, 0.4f * size);
                    _chainFrame = Time.frameCount;
                    _chainLast = at;
                    Fx(_lightning, at, 1.2f * size);
                    Partners.Impact(at, color, 1.2f * size);
                    Play(_sChain, at);
                    break;
                case EchoKind.Rune:
                    Partners.Pillar(at, color, 1.4f * size);
                    Partners.Impact(at, color, 1.3f * size);
                    Fx(_omniQuick, at, 1.5f * size);
                    Play(_sZap, at);
                    break;
                case EchoKind.Judgment:
                    Partners.Beam(at + Vector3.up * 80f, at, Gold, 2.2f * size);
                    Partners.Pillar(at, Gold, 2.4f * size);
                    Fx(_meteor, at, 5f * size);
                    Fx(_omni, at, 6f * size);
                    Play(_sBoom, at);
                    break;
            }
            if (kill) Partners.Burst(at, color, 2.5f * size);
        }

        // ------------------------------------------------------------ effects on the rest of the team

        public static List<CharacterBody> Teammates(CharacterBody except)
        {
            List<CharacterBody> list = new List<CharacterBody>();
            foreach (PlayerCharacterMasterController pc in PlayerCharacterMasterController.instances)
            {
                CharacterBody b = pc && pc.master ? pc.master.GetBody() : null;
                if (!b || b == except || !b.healthComponent || !b.healthComponent.alive) continue;
                list.Add(b);
            }
            return list;
        }

        /// <summary>A monster is summoned in the duel: teammates on the map are blessed (and Extra Deck summons shake the world).</summary>
        public static void Summoned(CharacterBody duelist, Card c, IEnumerable<CharacterBody> realEnemies)
        {
            if (!On || c == null) return;
            KindOf(c, out Color color);
            bool extra = c.Type == CardType.Extra;
            foreach (CharacterBody t in Teammates(duelist))
            {
                Partners.Pillar(t.footPosition, color, extra ? 1.6f : 1f);
                DuelNet.AddBuff(t, RoR2Content.Buffs.WarCryBuff, extra ? 10f : 5f);
                if (extra) DuelNet.Barrier(t, 0.2f);
            }
            if (extra && realEnemies != null)
            {
                int n = 0;
                foreach (CharacterBody e in realEnemies)
                {
                    if (!e || n++ >= 12) continue;
                    Strike(e.corePosition, KindOf(c, out _), color, 0.8f, false);
                    DuelNet.AddBuff(e, RoR2Content.Buffs.Slow80, 4f);
                }
            }
            Play(_sChime, duelist ? duelist.corePosition : Vector3.zero);
        }

        /// <summary>An enemy monster is destroyed: its soul flows to your teammates and heals them.</summary>
        public static void SoulHarvest(CharacterBody duelist, Vector3 from)
        {
            if (!On) return;
            foreach (CharacterBody t in Teammates(duelist))
            {
                Partners.Beam(from + Vector3.up, t.corePosition, new Color(0.45f, 1f, 0.55f), 0.25f);
                DuelNet.HealFraction(t, 0.05f);
            }
        }

        /// <summary>A heal spell: your teammates on the map are healed too.</summary>
        public static void HealTeam(CharacterBody duelist, float fraction)
        {
            if (!On) return;
            foreach (CharacterBody t in Teammates(duelist))
            {
                Partners.Pillar(t.footPosition, new Color(0.4f, 1f, 0.5f), 1.2f);
                DuelNet.HealFraction(t, fraction);
            }
        }

        /// <summary>A trap springs: the real enemies are bound in place.</summary>
        public static void Bind(IEnumerable<CharacterBody> realEnemies, Color color)
        {
            if (!On || realEnemies == null) return;
            int n = 0;
            foreach (CharacterBody e in realEnemies)
            {
                if (!e || n++ >= 16) continue;
                Partners.Glyph(e.footPosition + Vector3.up * 0.1f, Vector3.up, color, Mathf.Max(1.5f, e.radius * 2f));
                DuelNet.AddBuff(e, RoR2Content.Buffs.Slow80, 5f);
            }
        }

        /// <summary>While a Link monster is on your field, teammates fight under its war banner.</summary>
        public static void LinkBanner(CharacterBody duelist)
        {
            if (!On) return;
            foreach (CharacterBody t in Teammates(duelist))
            {
                DuelNet.AddBuff(t, RoR2Content.Buffs.Warbanner, 14f);
                Partners.Impact(t.corePosition, new Color(0.3f, 0.55f, 1f), 1f);
            }
        }
    }
}
