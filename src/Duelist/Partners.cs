using System;
using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Orbs;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    public enum PartnerKind { None = 0, Featherpuff = 1, Tock = 2, HollowIdol = 3, Jinx = 4, Gloop = 5 }

    public enum PartnerAbility
    {
        FeatherGlaive, Ward, ForesightFlurry,
        TeslaTick, SlowTime, TimeRoulette,
        SoulGaze, Absorb, HollowPull,
        CardJuggle, MindTaunt, Encore,
        Glob, StickyPuddle, Reform
    }

    public class PartnerAbilityInfo
    {
        public PartnerAbility Id;
        public string Name;
        public string Desc;
        public float Cooldown;
    }

    public class PartnerInfo
    {
        public PartnerKind Kind;
        public string Name;
        public string Relic;                    // the golden relic it carries
        public DuelistIcons.Symbol RelicSymbol;
        public Color Color;                     // glow / effects
        public Color IconBg;                    // icon background (picked for contrast with gold)
        public LightningOrb.LightningType Orb;  // how its shots look
        public float AutoCoeff;                 // auto-attack damage
        public int AutoBounces;
        public bool AutoSlows;
        public PartnerAbilityInfo[] Abilities;  // M1, M2, R
    }

    /// <summary>
    /// Partners: one at a time, Special Summoned from the deck with Shift, floating beside you and
    /// auto-battling. While one is out, M1/M2/R are its abilities; 1 sends it at your aim target,
    /// 2 calls it back. Each carries a golden relic (original designs on public-domain Egyptian symbols).
    /// </summary>
    public static class Partners
    {
        public static readonly PartnerInfo[] All =
        {
            new PartnerInfo
            {
                Kind = PartnerKind.Featherpuff, Name = "Featherpuff", Relic = "Relic Necklace (sees what's coming)",
                RelicSymbol = DuelistIcons.Symbol.RelicNecklace, Color = new Color(1f, 0.95f, 0.85f), IconBg = new Color(0.3f, 0.5f, 0.85f),
                Orb = LightningOrb.LightningType.HuntressGlaive, AutoCoeff = 1.2f, AutoBounces = 2,
                Abilities = new[]
                {
                    new PartnerAbilityInfo { Id = PartnerAbility.FeatherGlaive, Name = "Feather Glaive", Desc = "Featherpuff flings a feather glaive for <style=cIsDamage>260% damage</style> that <style=cIsDamage>bounces 4 times</style>.", Cooldown = 0.45f },
                    new PartnerAbilityInfo { Id = PartnerAbility.Ward, Name = "Downy Ward", Desc = "Gain a <style=cIsHealing>barrier</style> worth 40% of your max health.", Cooldown = 8f },
                    new PartnerAbilityInfo { Id = PartnerAbility.ForesightFlurry, Name = "Foresight Flurry", Desc = "Become <style=cIsUtility>invulnerable</style> for 3s while 8 feather glaives hunt enemies around you for <style=cIsDamage>200% each</style>.", Cooldown = 20f },
                }
            },
            new PartnerInfo
            {
                Kind = PartnerKind.Tock, Name = "Tock", Relic = "Relic Ring (bends time)",
                RelicSymbol = DuelistIcons.Symbol.RelicRing, Color = new Color(0.95f, 0.75f, 0.3f), IconBg = new Color(0.1f, 0.35f, 0.45f),
                Orb = LightningOrb.LightningType.Tesla, AutoCoeff = 1.2f, AutoBounces = 1,
                Abilities = new[]
                {
                    new PartnerAbilityInfo { Id = PartnerAbility.TeslaTick, Name = "Tesla Tick", Desc = "Tock fires a bolt for <style=cIsDamage>280% damage</style> that <style=cIsDamage>chains 3 times</style>.", Cooldown = 0.5f },
                    new PartnerAbilityInfo { Id = PartnerAbility.SlowTime, Name = "Slow Time", Desc = "Everything within 20m of Tock is <style=cIsUtility>slowed for 6s</style> and takes <style=cIsDamage>350% damage</style>.", Cooldown = 8f },
                    new PartnerAbilityInfo { Id = PartnerAbility.TimeRoulette, Name = "Time Roulette", Desc = "Flip a coin. <style=cIsDamage>Heads</style>: freeze everything within 24m and deal 1600%. <style=cDeath>Tails</style>: you lose 5% health, but Tock still blasts 400%.", Cooldown = 16f },
                }
            },
            new PartnerInfo
            {
                Kind = PartnerKind.HollowIdol, Name = "Hollow Idol", Relic = "Relic Eye (seals souls)",
                RelicSymbol = DuelistIcons.Symbol.RelicEye, Color = new Color(0.65f, 0.3f, 1f), IconBg = new Color(0.35f, 0.12f, 0.55f),
                Orb = LightningOrb.LightningType.RazorWire, AutoCoeff = 1.4f, AutoBounces = 0,
                Abilities = new[]
                {
                    new PartnerAbilityInfo { Id = PartnerAbility.SoulGaze, Name = "Soul Gaze", Desc = "The Idol's stare hits for <style=cIsDamage>300% damage</style> and jumps once.", Cooldown = 0.55f },
                    new PartnerAbilityInfo { Id = PartnerAbility.Absorb, Name = "Absorb", Desc = "Rip at the enemy you're aiming at for <style=cIsDamage>900%</style>. If it dies, its soul is sealed: <style=cIsDamage>+25% partner damage</style> (up to 8) and you <style=cIsHealing>heal 15%</style>.", Cooldown = 9f },
                    new PartnerAbilityInfo { Id = PartnerAbility.HollowPull, Name = "Hollow Pull", Desc = "<style=cIsUtility>Pull</style> every enemy within 28m to the Idol and deal <style=cIsDamage>700%</style>.", Cooldown = 15f },
                }
            },
            new PartnerInfo
            {
                Kind = PartnerKind.Jinx, Name = "Jinx", Relic = "Relic Rod (bends minds)",
                RelicSymbol = DuelistIcons.Symbol.RelicRod, Color = new Color(1f, 0.25f, 0.3f), IconBg = new Color(0.55f, 0.06f, 0.1f),
                Orb = LightningOrb.LightningType.Ukulele, AutoCoeff = 1.1f, AutoBounces = 2,
                Abilities = new[]
                {
                    new PartnerAbilityInfo { Id = PartnerAbility.CardJuggle, Name = "Card Juggle", Desc = "Jinx throws 4 sparks for <style=cIsDamage>4x130% damage</style>, each bouncing twice. Nearby enemies can't help targeting Jinx.", Cooldown = 0.6f },
                    new PartnerAbilityInfo { Id = PartnerAbility.MindTaunt, Name = "Mind Taunt", Desc = "Every enemy within 40m <style=cIsUtility>targets Jinx</style> for 6s. Jinx gains armor and bursts for <style=cIsDamage>300%</style>.", Cooldown = 10f },
                    new PartnerAbilityInfo { Id = PartnerAbility.Encore, Name = "Encore", Desc = "Jinx explodes for <style=cIsDamage>1300%</style> in 16m, comes back at full health and taunts everything nearby.", Cooldown = 18f },
                }
            },
            new PartnerInfo
            {
                Kind = PartnerKind.Gloop, Name = "Gloop", Relic = "Relic Scale (fuses and splits)",
                RelicSymbol = DuelistIcons.Symbol.RelicScale, Color = new Color(0.4f, 1f, 0.45f), IconBg = new Color(0.08f, 0.4f, 0.15f),
                Orb = LightningOrb.LightningType.TreePoisonDart, AutoCoeff = 1.2f, AutoBounces = 0, AutoSlows = true,
                Abilities = new[]
                {
                    new PartnerAbilityInfo { Id = PartnerAbility.Glob, Name = "Glob", Desc = "Gloop spits for <style=cIsDamage>280% damage</style> that <style=cIsUtility>slows</style> and jumps once. Gloop reforms in seconds when knocked out.", Cooldown = 0.45f },
                    new PartnerAbilityInfo { Id = PartnerAbility.StickyPuddle, Name = "Sticky Puddle", Desc = "Splash where you're aiming: <style=cIsDamage>600%</style> and <style=cIsUtility>slow</style> in 13m.", Cooldown = 8f },
                    new PartnerAbilityInfo { Id = PartnerAbility.Reform, Name = "Reform", Desc = "<style=cIsHealing>Heal 40%</style>, fully restore Gloop, and Gloop bursts for <style=cIsDamage>500%</style> around itself.", Cooldown = 18f },
                }
            },
        };

        public static readonly Dictionary<PartnerKind, GameObject> MasterPrefabs = new Dictionary<PartnerKind, GameObject>();
        public static readonly Dictionary<PartnerKind, SkillDef[]> Skills = new Dictionary<PartnerKind, SkillDef[]>();
        public static readonly Dictionary<SkillDef, PartnerAbility> AbilityOf = new Dictionary<SkillDef, PartnerAbility>();
        public static GameObject CircleEffect;

        public static PartnerInfo Info(PartnerKind k)
        {
            foreach (PartnerInfo i in All) if (i.Kind == k) return i;
            return null;
        }

        public static void Create()
        {
            ContentAddition.AddEntityState<PartnerAbilityState>(out _);
            ContentAddition.AddEntityState<PartnerSwapState>(out _);
            CreateCircle();

            GameObject droneBody = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/Drone1Body");
            GameObject droneMaster = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/Drone1Master");
            if (!droneBody || !droneMaster)
            {
                DuelistPlugin.Log.LogError("Partners: Drone1 prefabs not found, partners disabled.");
                return;
            }

            foreach (PartnerInfo info in All)
            {
                try
                {
                    string key = info.Kind.ToString();
                    string token = DuelistPlugin.TokenPrefix + "PARTNER_" + key.ToUpper();
                    LanguageAPI.Add(token + "_NAME", info.Name);

                    // Body: a flying drone underneath (movement, networking), our own look on top.
                    GameObject body = PrefabAPI.InstantiateClone(droneBody, "Duelist" + key + "Body", true);
                    CharacterBody cb = body.GetComponent<CharacterBody>();
                    cb.baseNameToken = token + "_NAME";
                    cb.bodyFlags &= ~CharacterBody.BodyFlags.Mechanical; // not a robot: never turned into a card
                    cb.baseMaxHealth = 700f;
                    cb.levelMaxHealth = 210f;
                    cb.baseRegen = 6f;
                    cb.levelRegen = 1.2f;
                    cb.baseArmor = 30f;
                    cb.baseMoveSpeed = 24f;
                    cb.bodyColor = info.Color;
                    cb.portraitIcon = DuelistIcons.RelicTexture(info.IconBg, info.RelicSymbol);
                    body.AddComponent<PartnerTag>().Kind = info.Kind;
                    body.AddComponent<PartnerVisual>().Kind = info.Kind;
                    ContentAddition.AddBody(body);

                    // Master: no AI; the PartnerManager on the Duelist drives it.
                    GameObject master = PrefabAPI.InstantiateClone(droneMaster, "Duelist" + key + "Master", true);
                    foreach (AISkillDriver d in master.GetComponents<AISkillDriver>()) Object.DestroyImmediate(d);
                    foreach (BaseAI ai in master.GetComponents<BaseAI>()) Object.DestroyImmediate(ai);
                    master.GetComponent<CharacterMaster>().bodyPrefab = body;
                    ContentAddition.AddMaster(master);
                    MasterPrefabs[info.Kind] = master;

                    // M1 / M2 / R skills that replace the Duelist's while this partner is out.
                    SkillDef[] defs = new SkillDef[3];
                    for (int i = 0; i < 3; i++)
                    {
                        PartnerAbilityInfo a = info.Abilities[i];
                        string at = token + "_" + a.Id.ToString().ToUpper();
                        LanguageAPI.Add(at + "_NAME", a.Name);
                        LanguageAPI.Add(at + "_DESC", a.Desc + $" <style=cStack>({info.Relic})</style>");

                        SkillDef def = ScriptableObject.CreateInstance<SkillDef>();
                        def.skillName = at;
                        ((ScriptableObject)def).name = at;
                        def.skillNameToken = at + "_NAME";
                        def.skillDescriptionToken = at + "_DESC";
                        def.icon = DuelistIcons.MakeRelicSprite(info.IconBg, info.RelicSymbol, i + 1);
                        def.activationState = new SerializableEntityStateType(typeof(PartnerAbilityState));
                        def.activationStateMachineName = "Weapon";
                        def.baseMaxStock = 1;
                        def.baseRechargeInterval = a.Cooldown;
                        def.rechargeStock = 1;
                        def.requiredStock = 1;
                        def.stockToConsume = 1;
                        def.beginSkillCooldownOnSkillEnd = i > 0;
                        def.canceledFromSprinting = false;
                        def.cancelSprintingOnActivation = i > 0;
                        def.fullRestockOnAssign = true;
                        def.mustKeyPress = i > 0;
                        def.isCombatSkill = true;
                        def.interruptPriority = i == 0 ? InterruptPriority.Any : InterruptPriority.Skill;
                        ContentAddition.AddSkillDef(def);
                        defs[i] = def;
                        AbilityOf[def] = a.Id;
                    }
                    Skills[info.Kind] = defs;
                }
                catch (Exception e)
                {
                    DuelistPlugin.Log.LogError($"Partner {info.Name} failed to build:\n{e}");
                }
            }
            DuelistPlugin.Log.LogInfo($"Partners ready: {MasterPrefabs.Count}/{All.Length}.");
        }

        private static void CreateCircle()
        {
            try
            {
                GameObject holder = new GameObject("DuelistCircleHolder");
                holder.SetActive(false);
                GameObject tmp = new GameObject("DuelistSummonCircle");
                tmp.transform.SetParent(holder.transform, false);
                tmp.AddComponent<EffectComponent>();
                VFXAttributes vfx = tmp.AddComponent<VFXAttributes>();
                vfx.vfxPriority = VFXAttributes.VFXPriority.Always;
                vfx.vfxIntensity = VFXAttributes.VFXIntensity.Low;
                tmp.AddComponent<SummonCircleEffect>();
                CircleEffect = PrefabAPI.InstantiateClone(tmp, "DuelistSummonCircle", false);
                Object.Destroy(holder);
                ContentAddition.AddEffect(CircleEffect);
            }
            catch (Exception e)
            {
                CircleEffect = null;
                DuelistPlugin.Log.LogError("Summoning circle effect failed:\n" + e);
            }
        }

        /// <summary>Summoning circle at a spot, seen by everyone. scale 1 = summon, smaller = dismiss.</summary>
        public static void Circle(Vector3 at, Color color, float scale) => SpawnFx(at, color, scale, 0f);

        /// <summary>A colored shockwave ring of the given radius, seen by everyone.</summary>
        public static void Burst(Vector3 at, Color color, float radius) => SpawnFx(at, color, radius, 1f);

        /// <summary>Anime hit: a flash with speed lines bursting out of the point.</summary>
        public static void Impact(Vector3 at, Color color, float size, bool transmit = true) => SpawnFx(at, color, size, 2f, transmit: transmit);

        /// <summary>A beam of light from one point to another that flares and thins away.</summary>
        public static void Beam(Vector3 from, Vector3 to, Color color, float width, bool transmit = true) => SpawnFx(to, color, width, 3f, from, transmit: transmit);

        /// <summary>A spinning magic circle of cards facing along normal (charge-ups, shields, coins).</summary>
        public static void Glyph(Vector3 at, Vector3 normal, Color color, float size, bool transmit = true)
            => SpawnFx(at, color, size, 4f, rot: Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up), transmit: transmit);

        /// <summary>A towering column of light with cards spiralling up (ultimates).</summary>
        public static void Pillar(Vector3 at, Color color, float size, bool transmit = true) => SpawnFx(at, color, size, 5f, transmit: transmit);

        private static void SpawnFx(Vector3 at, Color color, float scale, float mode, Vector3? start = null, Quaternion? rot = null, bool transmit = true)
        {
            if (!CircleEffect) return;
            try
            {
                EffectData d = new EffectData
                {
                    origin = at,
                    scale = scale,
                    genericFloat = mode,
                    color = (Color32)color
                };
                if (start.HasValue) d.start = start.Value;
                if (rot.HasValue) d.rotation = rot.Value;
                EffectManager.SpawnEffect(CircleEffect, d, transmit);
            }
            catch { }
        }
    }

    /// <summary>Marks a partner body (so it's never converted to a card).</summary>
    public class PartnerTag : MonoBehaviour
    {
        public PartnerKind Kind;
    }

    // ===================================================================== the Duelist's side

    /// <summary>
    /// Lives on the Duelist body. Server/host side: summons and dismisses the partner, keeps it beside
    /// you, auto-attacks, handles keys 1/2, swaps M1/M2/R to the partner's abilities.
    /// </summary>
    public class PartnerManager : MonoBehaviour
    {
        public PartnerKind Current = PartnerKind.None;
        public BodyIndex CurrentDrone = BodyIndex.None; // a robot card summoned from the deck (its own AI)
        public int AbsorbStacks;
        public float TauntUntil;

        private CharacterBody _body;
        private CharacterMaster _master;
        private readonly float[] _faintUntil = new float[6];
        private readonly Dictionary<BodyIndex, float> _droneFaint = new Dictionary<BodyIndex, float>();
        private bool _reformGloop, _bodySeen;
        private HurtBox _focus;
        private float _autoTimer, _tauntTick, _lungeUntil;
        private Vector3 _lungeDir;
        private PartnerKind _overrideKind = PartnerKind.None;

        public CharacterBody PartnerBody => _master ? _master.GetBody() : null;
        public PartnerInfo Info => Partners.Info(Current);

        public static PartnerManager Of(CharacterBody b) => b ? b.GetComponent<PartnerManager>() : null;

        private bool InDuel => _body && DuelDimensionState.ActiveDuelists.Contains(_body);

        /// <summary>Partner damage multiplier: the Idol grows with every soul it seals.</summary>
        public float DamageMult => 1f + (Current == PartnerKind.HollowIdol ? 0.25f * AbsorbStacks : 0f);

        private void Awake() => _body = GetComponent<CharacterBody>();

        private void OnDestroy()
        {
            if (NetworkServer.active) Dismiss(false);
        }

        /// <summary>Makes the partner dart toward what it's attacking for a moment (position is networked).</summary>
        public void Lunge(Vector3 target)
        {
            CharacterBody pb = PartnerBody;
            if (!pb) return;
            Vector3 d = target - pb.corePosition;
            if (d.sqrMagnitude < 0.01f) return;
            _lungeDir = d.normalized;
            _lungeUntil = Time.time + 0.2f;
        }

        // ------------------------------------------------------------ swapping

        private struct Option
        {
            public PartnerKind Kind;
            public BodyIndex Drone;
        }

        /// <summary>Everything Shift can call out: none, the 5 partners, then each robot card in your deck.</summary>
        private List<Option> Options()
        {
            List<Option> list = new List<Option> { new Option { Kind = PartnerKind.None, Drone = BodyIndex.None } };
            foreach (PartnerInfo p in Partners.All)
                if (Partners.MasterPrefabs.ContainsKey(p.Kind)) list.Add(new Option { Kind = p.Kind, Drone = BodyIndex.None });
            DuelistDeck deck = _body && _body.master ? _body.master.GetComponent<DuelistDeck>() : null;
            if (deck)
            {
                List<BodyIndex> drones = new List<BodyIndex>(deck.DroneCards.Keys);
                drones.Sort((a, b) => ((int)a).CompareTo((int)b));
                foreach (BodyIndex d in drones)
                    if (deck.DroneCards[d] > 0 && DroneMaster(d)) list.Add(new Option { Kind = PartnerKind.None, Drone = d });
            }
            return list;
        }

        private bool Ready(Option o)
        {
            if (o.Drone != BodyIndex.None) return !_droneFaint.TryGetValue(o.Drone, out float t) || Time.time >= t;
            return o.Kind == PartnerKind.None || Time.time >= _faintUntil[(int)o.Kind];
        }

        /// <summary>Shift: None → partners → your robot cards → None, skipping fainted ones.</summary>
        public void Cycle()
        {
            if (!NetworkServer.active || !_body) return;
            List<Option> opts = Options();
            int cur = opts.FindIndex(o => o.Kind == Current && o.Drone == CurrentDrone);
            if (cur < 0) cur = 0;

            Option next = opts[0];
            for (int step = 1; step <= opts.Count; step++)
            {
                Option o = opts[(cur + step) % opts.Count];
                if (Ready(o)) { next = o; break; }
            }

            Dismiss(true);
            if (next.Drone != BodyIndex.None) SummonDrone(next.Drone);
            else if (next.Kind != PartnerKind.None) Summon(next.Kind);
            else Say("<color=#f2c640>No partner.</color> Your own skills are back (R: Duel Dimension).");
        }

        /// <summary>The vanilla AI master that drives a robot body (so it fights on its own).</summary>
        private static GameObject DroneMaster(BodyIndex body)
        {
            foreach (CharacterMaster m in MasterCatalog.allAiMasters)
            {
                if (!m || !m.bodyPrefab) continue;
                CharacterBody cb = m.bodyPrefab.GetComponent<CharacterBody>();
                if (cb && cb.bodyIndex == body) return m.gameObject;
            }
            return null;
        }

        /// <summary>Special Summons one of your robot cards as the real robot, under its own AI.</summary>
        private void SummonDrone(BodyIndex body)
        {
            GameObject prefab = DroneMaster(body);
            if (!prefab) return;
            Card card = Card.FromDrone(body);
            string name = card != null ? card.Name : prefab.name;
            Vector3 spot = _body.footPosition + Side() * 2.5f;
            Partners.Circle(spot, new Color(0.95f, 0.75f, 0.3f), 1f);
            try
            {
                CharacterMaster m = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = spot + Vector3.up * 1.5f,
                    rotation = _body.transform.rotation,
                    summonerBodyObject = _body.gameObject,
                    ignoreTeamMemberLimit = true,
                    useAmbientLevel = true
                }.Perform();
                if (!m) { Say("Summon failed."); return; }
                CharacterBody b = m.GetBody();
                if (b && !b.GetComponent<PartnerTag>()) b.gameObject.AddComponent<PartnerTag>(); // don't turn it back into a card
                _master = m;
                _bodySeen = false;
                CurrentDrone = body;
                Current = PartnerKind.None;
                Say($"<color=#f2c640>The Duelist special summons {name} from the deck!</color> <color=#ffffffaa>It fights on its own · your skills stay yours · Shift: swap</color>");
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogError("Robot summon failed:\n" + e);
            }
        }

        private void Summon(PartnerKind kind)
        {
            if (!Partners.MasterPrefabs.TryGetValue(kind, out GameObject prefab)) return;
            PartnerInfo info = Partners.Info(kind);
            Vector3 spot = _body.footPosition + Side() * 2.2f;
            Partners.Circle(spot, info.Color, 1f);

            try
            {
                CharacterMaster m = new MasterSummon
                {
                    masterPrefab = prefab,
                    position = spot + Vector3.up * 1.2f,
                    rotation = _body.transform.rotation,
                    summonerBodyObject = _body.gameObject,
                    ignoreTeamMemberLimit = true,
                    useAmbientLevel = true
                }.Perform();
                if (!m) { Say("Summon failed."); return; }
                _master = m;
                _bodySeen = false;
                Current = kind;
                _focus = null;
                ApplyOverrides(kind);
                Say($"<color=#f2c640>The Duelist special summons {info.Name} from the deck!</color> <color=#ffffffaa>1: attack target · 2: come back · Shift: swap</color>");
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogError("Partner summon failed:\n" + e);
            }
        }

        /// <summary>Sends the partner back to the deck (small circle).</summary>
        public void Dismiss(bool effect)
        {
            RemoveOverrides();
            CharacterBody pb = PartnerBody;
            if (pb && effect)
            {
                PartnerInfo info = Info;
                Partners.Circle(pb.footPosition, info != null ? info.Color : Color.white, 0.6f);
            }
            try
            {
                if (pb) NetworkServer.Destroy(pb.gameObject);
                if (_master) NetworkServer.Destroy(_master.gameObject);
            }
            catch { }
            _master = null;
            Current = PartnerKind.None;
            CurrentDrone = BodyIndex.None;
            _focus = null;
        }

        private void Faint()
        {
            if (CurrentDrone != BodyIndex.None)
            {
                BodyIndex d = CurrentDrone;
                Card card = Card.FromDrone(d);
                _droneFaint[d] = Time.time + DuelConfig.PartnerFaintTime.Value;
                try { if (_master) NetworkServer.Destroy(_master.gameObject); } catch { }
                _master = null;
                CurrentDrone = BodyIndex.None;
                Say($"<color=#ff9a7a>{(card != null ? card.Name : "Your robot")} was destroyed!</color> Its card is back in your deck; ready again in {DuelConfig.PartnerFaintTime.Value:0}s.");
                return;
            }
            PartnerKind k = Current;
            PartnerInfo info = Partners.Info(k);
            RemoveOverrides();
            try { if (_master) NetworkServer.Destroy(_master.gameObject); } catch { }
            _master = null;
            Current = PartnerKind.None;
            if (k == PartnerKind.Gloop)
            {
                _faintUntil[(int)k] = Time.time + 5f;
                _reformGloop = true;
                Say("<color=#80ff90>Gloop splats... and starts to reform.</color>");
            }
            else
            {
                _faintUntil[(int)k] = Time.time + DuelConfig.PartnerFaintTime.Value;
                Say($"<color=#ff9a7a>{info?.Name} fainted!</color> It'll be ready again in {DuelConfig.PartnerFaintTime.Value:0}s.");
            }
        }

        // ------------------------------------------------------------ M1 / M2 / R overrides

        private void ApplyOverrides(PartnerKind kind)
        {
            RemoveOverrides();
            if (!Partners.Skills.TryGetValue(kind, out SkillDef[] defs) || !_body.skillLocator) return;
            SkillLocator sl = _body.skillLocator;
            try
            {
                if (sl.primary) sl.primary.SetSkillOverride(this, defs[0], GenericSkill.SkillOverridePriority.Replacement);
                if (sl.secondary) sl.secondary.SetSkillOverride(this, defs[1], GenericSkill.SkillOverridePriority.Replacement);
                if (sl.special) sl.special.SetSkillOverride(this, defs[2], GenericSkill.SkillOverridePriority.Replacement);
                _overrideKind = kind;
            }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Partner skill override failed: " + e.Message); }
        }

        private void RemoveOverrides()
        {
            if (_overrideKind == PartnerKind.None || !_body || !_body.skillLocator) { _overrideKind = PartnerKind.None; return; }
            if (Partners.Skills.TryGetValue(_overrideKind, out SkillDef[] defs))
            {
                SkillLocator sl = _body.skillLocator;
                try
                {
                    if (sl.primary) sl.primary.UnsetSkillOverride(this, defs[0], GenericSkill.SkillOverridePriority.Replacement);
                    if (sl.secondary) sl.secondary.UnsetSkillOverride(this, defs[1], GenericSkill.SkillOverridePriority.Replacement);
                    if (sl.special) sl.special.UnsetSkillOverride(this, defs[2], GenericSkill.SkillOverridePriority.Replacement);
                }
                catch { }
            }
            _overrideKind = PartnerKind.None;
        }

        // ------------------------------------------------------------ every frame

        private void Update()
        {
            if (!_body || !_body.hasEffectiveAuthority || InDuel || Current == PartnerKind.None) return;
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                _focus = AimedEnemy(90f);
                if (!NetworkServer.active) DuelNet.PartnerFocus(_body, _focus);
                if (_focus) Say($"<color=#f2c640>{Info?.Name ?? "Partner"}, attack!</color>");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                _focus = null;
                if (!NetworkServer.active) DuelNet.PartnerFocus(_body, null);
                Say($"<color=#f2c640>{Info?.Name ?? "Partner"}, come back!</color>");
            }
        }

        private PartnerKind _sentKind = PartnerKind.None;
        private BodyIndex _sentDrone = BodyIndex.None;

        public void SetFocus(HurtBox target) => _focus = target;

        /// <summary>Client side: the host says which partner this Duelist has out; switch M1/M2/R to match.</summary>
        public void ApplyRemoteState(PartnerKind kind, BodyIndex drone)
        {
            Current = kind;
            CurrentDrone = drone;
            if (!_body || !_body.hasEffectiveAuthority) return;
            if (kind != PartnerKind.None) ApplyOverrides(kind); else RemoveOverrides();
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !_body) return;
            if (Current != _sentKind || CurrentDrone != _sentDrone)
            {
                _sentKind = Current;
                _sentDrone = CurrentDrone;
                if (!_body.hasEffectiveAuthority) DuelNet.SendPartnerState(this, Current, CurrentDrone);
            }

            if (_reformGloop && Current == PartnerKind.None && CurrentDrone == BodyIndex.None && Time.time >= _faintUntil[(int)PartnerKind.Gloop])
            {
                _reformGloop = false;
                Summon(PartnerKind.Gloop);
            }
            if (Current == PartnerKind.None && CurrentDrone == BodyIndex.None) return;

            CharacterBody pb = PartnerBody;
            if (pb) _bodySeen = true;
            if (!_master || (pb && pb.healthComponent && !pb.healthComponent.alive) || (!pb && _bodySeen))
            {
                Faint();
                return;
            }
            if (!pb) return;   // still spawning
            if (InDuel) return; // the duel pins allies on the board
            if (CurrentDrone != BodyIndex.None) return; // robots fight with their own AI

            Follow(pb);
            AutoAttack(pb);
            if (Current == PartnerKind.Jinx) TauntTick(pb, Time.time < TauntUntil ? 40f : 14f);
        }

        private Vector3 Side()
        {
            Vector3 aim = _body.inputBank ? _body.inputBank.aimDirection : _body.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.01f) aim = _body.transform.forward;
            return -Vector3.Cross(Vector3.up, aim.normalized).normalized; // your left side
        }

        private void Follow(CharacterBody pb)
        {
            Vector3 desired;
            if (_focus && _focus.healthComponent && _focus.healthComponent.alive)
            {
                Vector3 tp = _focus.healthComponent.body ? _focus.healthComponent.body.corePosition : _focus.transform.position;
                Vector3 away = pb.corePosition - tp;
                away.y = 0f;
                desired = tp + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 6f + Vector3.up * 3.5f;
            }
            else
            {
                _focus = null;
                desired = _body.corePosition + Side() * 2.2f + Vector3.up * 1.4f;
            }
            if (Time.time < _lungeUntil) desired += _lungeDir * 3f;

            Vector3 to = desired - pb.corePosition;
            float dist = to.magnitude;
            if (dist > 60f)
            {
                try { TeleportHelper.TeleportBody(pb, desired); } catch { }
                return;
            }

            Vector3 vel = Vector3.ClampMagnitude(to * 7f, Mathf.Max(34f, _body.moveSpeed * 2.6f));
            if (pb.inputBank) pb.inputBank.moveVector = dist > 0.3f ? to / Mathf.Max(dist, 1f) : Vector3.zero;
            if (pb.rigidbody) pb.rigidbody.velocity = vel;
            else if (pb.characterMotor) pb.characterMotor.velocity = vel;

            Vector3 look = _focus ? (_focus.transform.position - pb.corePosition) : (_body.inputBank ? _body.inputBank.aimDirection : _body.transform.forward);
            if (pb.inputBank) pb.inputBank.aimDirection = look.normalized;
            if (pb.rigidbody && look.sqrMagnitude > 0.01f)
                pb.rigidbody.MoveRotation(Quaternion.Slerp(pb.rigidbody.rotation, Quaternion.LookRotation(new Vector3(look.x, 0f, look.z).normalized), 0.2f));
        }

        private void AutoAttack(CharacterBody pb)
        {
            _autoTimer -= Time.fixedDeltaTime;
            if (_autoTimer > 0f) return;
            _autoTimer = DuelConfig.PartnerAutoInterval.Value;

            HurtBox target = _focus ? _focus : PartnerFx.Nearest(_body, pb.corePosition, 32f);
            if (!target) return;
            PartnerInfo info = Info;
            PartnerFx.Orb(_body, pb, target, info.AutoCoeff * DuelConfig.PartnerAutoDamage.Value * DamageMult, info.Orb, info.AutoBounces, info.AutoSlows, 0.6f);
            Lunge(target.transform.position);
        }

        private void TauntTick(CharacterBody pb, float radius)
        {
            _tauntTick -= Time.fixedDeltaTime;
            if (_tauntTick > 0f) return;
            _tauntTick = 0.5f;
            foreach (HurtBox hb in PartnerFx.Enemies(_body, pb.corePosition, radius))
            {
                CharacterBody eb = hb.healthComponent ? hb.healthComponent.body : null;
                if (!eb || !eb.master || eb.isChampion) continue;
                foreach (BaseAI ai in eb.master.aiComponents)
                {
                    try
                    {
                        if (ai == null || ai.currentEnemy == null) continue;
                        ai.currentEnemy.gameObject = pb.gameObject;
                        ai.currentEnemy.bestHurtBox = pb.mainHurtBox;
                    }
                    catch { }
                }
            }
        }

        private HurtBox AimedEnemy(float range)
        {
            if (!_body.inputBank) return null;
            Ray aim = _body.inputBank.GetAimRay();
            BullseyeSearch s = new BullseyeSearch
            {
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                maxDistanceFilter = range,
                maxAngleFilter = 12f,
                teamMaskFilter = TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Angle,
                filterByLoS = true
            };
            s.RefreshCandidates();
            foreach (HurtBox hb in s.GetResults()) if (hb && hb.healthComponent && hb.healthComponent.alive) return hb;
            return null;
        }

        private static void Say(string msg)
        {
            try { Chat.AddMessage(msg); } catch { }
        }

        // ------------------------------------------------------------ small partner label

        private void OnGUI()
        {
            if (!_body || !_body.hasEffectiveAuthority || InDuel) return;
            string text;
            if (CurrentDrone != BodyIndex.None)
            {
                CharacterBody pb = PartnerBody;
                Card card = Card.FromDrone(CurrentDrone);
                string hp = pb && pb.healthComponent ? $"{pb.healthComponent.combinedHealth:N0}/{pb.healthComponent.fullCombinedHealth:N0}" : "…";
                text = $"<b>Summoned:</b> <color=#f2c640>{(card != null ? card.Name : "robot")}</color>  HP {hp}  <color=#ffffff88>(fights on its own)</color>\n<color=#ffffff88>Shift swap · your M1/M2/R are your own</color>";
            }
            else if (Current == PartnerKind.None) text = "<b>Partner:</b> none  <color=#ffffff88>(Shift to summon)</color>";
            else
            {
                CharacterBody pb = PartnerBody;
                string hp = pb && pb.healthComponent ? $"{pb.healthComponent.combinedHealth:N0}/{pb.healthComponent.fullCombinedHealth:N0}" : "…";
                string extra = Current == PartnerKind.HollowIdol && AbsorbStacks > 0 ? $"  <color=#c080ff>{AbsorbStacks} souls (+{AbsorbStacks * 25}%)</color>" : "";
                text = $"<b>Partner:</b> <color=#f2c640>{Info.Name}</color>  HP {hp}{extra}" + (_focus ? "  <color=#ff9a7a>attacking</color>" : "") +
                       "\n<color=#ffffff88>1 attack · 2 come back · M1/M2/R abilities · Shift swap</color>";
            }
            float s = Screen.height / 1080f;
            GUIStyle st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * s), richText = true };
            GUI.Label(new Rect(20f * s, Screen.height - 230f * s, 640f * s, 60f * s), text, st);
        }
    }

    // ===================================================================== skills

    /// <summary>Shift: swap to the next partner.</summary>
    public class PartnerSwapState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (NetworkServer.active) PartnerManager.Of(characterBody)?.Cycle();
            Util.PlaySound("Play_UI_menuClick", gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.15f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    /// <summary>One state for all 15 partner abilities; which one comes from the skill that started it.</summary>
    public class PartnerAbilityState : BaseSkillState
    {
        private float _duration = 0.3f;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = 0.3f / Mathf.Max(0.1f, attackSpeedStat);
            SkillDef def = activatorSkillSlot ? activatorSkillSlot.skillDef : null;
            if (def == null || !Partners.AbilityOf.TryGetValue(def, out PartnerAbility ability)) return;
            PartnerManager pm = PartnerManager.Of(characterBody);
            if (isAuthority) PartnerFx.AnnounceLocal(pm, ability);
            if (!NetworkServer.active)
            {
                // playing as a client: the partner lives on the host, so ask the host to use the ability
                if (!isAuthority) return;
                StartAimMode(GetAimRay(), 2f, false);
                PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
                DuelNet.UsePartnerAbility(characterBody, ability, GetAimRay());
                return;
            }
            CharacterBody pb = pm ? pm.PartnerBody : null;
            if (!pb) return;
            StartAimMode(GetAimRay(), 2f, false);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right"); // the Duelist gestures a command
            try { PartnerFx.Run(ability, characterBody, pm, pb, GetAimRay()); }
            catch (Exception e) { DuelistPlugin.Log.LogWarning($"Partner ability {ability} failed: {e.Message}"); }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    // ===================================================================== what the abilities do

    public static class PartnerFx
    {
        private static GameObject _blastVfx;

        public static List<HurtBox> Enemies(CharacterBody owner, Vector3 at, float radius)
        {
            SphereSearch s = new SphereSearch { origin = at, radius = radius, mask = LayerIndex.entityPrecise.mask };
            s.RefreshCandidates();
            s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex));
            s.OrderCandidatesByDistance();
            s.FilterCandidatesByDistinctHurtBoxEntities();
            List<HurtBox> list = new List<HurtBox>();
            foreach (HurtBox hb in s.GetHurtBoxes()) if (hb && hb.healthComponent && hb.healthComponent.alive) list.Add(hb);
            return list;
        }

        public static HurtBox Nearest(CharacterBody owner, Vector3 at, float radius)
        {
            List<HurtBox> l = Enemies(owner, at, radius);
            return l.Count > 0 ? l[0] : null;
        }

        /// <summary>A visible travelling orb (glaive, tesla bolt, dart…) from the partner to a target, bouncing on.</summary>
        public static void Orb(CharacterBody owner, CharacterBody pb, HurtBox target, float coeff, LightningOrb.LightningType type, int bounces, bool slow, float proc = 1f)
        {
            if (!target || !OrbManager.instance) return;
            LightningOrb orb = new LightningOrb
            {
                origin = pb.corePosition,
                target = target,
                attacker = owner.gameObject,
                inflictor = pb.gameObject,
                teamIndex = owner.teamComponent.teamIndex,
                damageValue = owner.damage * coeff,
                isCrit = Util.CheckRoll(owner.crit, owner.master),
                procCoefficient = proc,
                bouncesRemaining = bounces,
                range = 25f,
                damageCoefficientPerBounce = 1f,
                lightningType = type,
                damageColorIndex = DamageColorIndex.Default,
                bouncedObjects = new List<HealthComponent>()
            };
            if (slow) orb.damageType = DamageType.SlowOnHit;
            OrbManager.instance.AddOrb(orb);
        }

        public static void Blast(CharacterBody owner, Vector3 at, float radius, float coeff, Color color, bool freeze = false, bool slow = false)
        {
            if (!_blastVfx) _blastVfx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
            BlastAttack ba = new BlastAttack
            {
                attacker = owner.gameObject,
                inflictor = owner.gameObject,
                teamIndex = owner.teamComponent.teamIndex,
                position = at,
                radius = radius,
                baseDamage = owner.damage * coeff,
                baseForce = 800f,
                crit = Util.CheckRoll(owner.crit, owner.master),
                falloffModel = BlastAttack.FalloffModel.None,
                procCoefficient = 1f
            };
            if (freeze) ba.damageType = DamageType.Freeze2s;
            else if (slow) ba.damageType = DamageType.SlowOnHit;
            ba.Fire();
            if (_blastVfx) EffectManager.SpawnEffect(_blastVfx, new EffectData { origin = at, scale = radius * 0.6f }, true);
            Partners.Burst(at, color, radius);
        }

        private static Vector3 AimPoint(Ray aim)
        {
            if (Physics.Raycast(aim, out RaycastHit hit, 200f, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
                return hit.point;
            return aim.origin + aim.direction * 60f;
        }

        /// <summary>The enemy you're aiming at (or nearest the aim point).</summary>
        private static HurtBox AimTarget(CharacterBody owner, Ray aim)
        {
            BullseyeSearch s = new BullseyeSearch
            {
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                maxDistanceFilter = 120f,
                maxAngleFilter = 10f,
                teamMaskFilter = TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Angle,
                filterByLoS = true
            };
            s.RefreshCandidates();
            foreach (HurtBox hb in s.GetResults()) if (hb && hb.healthComponent && hb.healthComponent.alive) return hb;
            return Nearest(owner, AimPoint(aim), 10f);
        }

        /// <summary>
        /// The attack-name call: big moves (M2, R) flash the anime cut-in on your screen with the partner's name.
        /// </summary>
        public static void AnnounceLocal(PartnerManager pm, PartnerAbility a)
        {
            PartnerInfo info = pm ? pm.Info : null;
            if (info == null || info.Abilities == null) return;
            int slot = Array.FindIndex(info.Abilities, x => x.Id == a);
            if (slot <= 0) return;
            AnimeCutIn.Show(info.Name.ToUpper(), info.Abilities[slot].Name.ToUpper() + "!", info.Color, slot == 2);
        }

        private static void Hit(CharacterBody owner, CharacterBody pb, HurtBox t, float coeff, Color col, DamageType type = DamageType.Generic, DamageColorIndex dc = DamageColorIndex.Default)
        {
            if (!t || !t.healthComponent || !t.healthComponent.alive) return;
            DamageInfo di = new DamageInfo
            {
                attacker = owner.gameObject,
                inflictor = pb ? pb.gameObject : owner.gameObject,
                damage = owner.damage * coeff,
                crit = Util.CheckRoll(owner.crit, owner.master),
                position = t.transform.position,
                force = Vector3.zero,
                procCoefficient = 1f,
                damageColorIndex = dc,
                procChainMask = default(ProcChainMask)
            };
            di.damageType = type;
            t.healthComponent.TakeDamage(di);
            GlobalEventManager.instance?.OnHitEnemy(di, t.healthComponent.gameObject);
            GlobalEventManager.instance?.OnHitAll(di, t.healthComponent.gameObject);
            Partners.Impact(t.transform.position, col, 1.4f);
        }

        private static void Later(CharacterBody owner, float delay, Action act) => PartnerCombo.After(owner ? owner.gameObject : null, delay, act);

        public static void Run(PartnerAbility a, CharacterBody owner, PartnerManager pm, CharacterBody pb, Ray aim)
        {
            PartnerInfo info = pm.Info;
            Color col = info != null ? info.Color : Color.white;
            Color white = Color.white;
            float mult = pm.DamageMult;
            HealthComponent hc = owner.healthComponent;
            Vector3 from = pb.corePosition;
            HurtBox target = AimTarget(owner, aim);
            if (target) pm.Lunge(target.transform.position);
            Vector3 toward = target ? (target.transform.position - from).normalized : aim.direction;

            switch (a)
            {
                // ---- Featherpuff: wind, feathers, light
                case PartnerAbility.FeatherGlaive:
                    Orb(owner, pb, target, 2.6f * mult, LightningOrb.LightningType.HuntressGlaive, 4, false);
                    Later(owner, 0.12f, () => Orb(owner, pb, AimTarget(owner, aim) ?? target, 0.9f * mult, LightningOrb.LightningType.HuntressGlaive, 2, false, 0.5f));
                    break;
                case PartnerAbility.Ward:
                    Partners.Glyph(owner.footPosition + Vector3.up * 0.1f, Vector3.up, col, 2.2f);
                    Partners.Pillar(owner.footPosition, white, 1.2f);
                    hc.AddBarrier(hc.fullCombinedHealth * 0.4f);
                    Partners.Burst(owner.footPosition, col, 6f);
                    break;
                case PartnerAbility.ForesightFlurry:
                {
                    owner.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, 3f);
                    Partners.Pillar(owner.footPosition, col, 2.2f);
                    Partners.Glyph(owner.footPosition + Vector3.up * 0.1f, Vector3.up, col, 3.5f);
                    for (int i = 0; i < 8; i++)
                    {
                        int k = i;
                        Later(owner, 0.12f * i, () =>
                        {
                            if (!pb) return;
                            List<HurtBox> near = Enemies(owner, owner.corePosition, 38f);
                            if (near.Count == 0) return;
                            HurtBox t = near[k % near.Count];
                            Partners.Beam(pb.corePosition, t.transform.position, col, 0.25f);
                            Orb(owner, pb, t, 2f * mult, LightningOrb.LightningType.HuntressGlaive, 2, false);
                        });
                    }
                    Later(owner, 1.1f, () => { Partners.Burst(owner.footPosition, white, 14f); Partners.Impact(owner.corePosition + Vector3.up, col, 3f); });
                    break;
                }

                // ---- Tock: clockwork lightning
                case PartnerAbility.TeslaTick:
                    if (target) Partners.Beam(from, target.transform.position, col, 0.35f);
                    Orb(owner, pb, target, 2.8f * mult, LightningOrb.LightningType.Tesla, 3, false);
                    if (target) Partners.Impact(target.transform.position, col, 1.2f);
                    break;
                case PartnerAbility.SlowTime:
                    Partners.Glyph(pb.footPosition + Vector3.up * 0.2f, Vector3.up, col, 6f);
                    Later(owner, 0.25f, () =>
                    {
                        if (!pb) return;
                        foreach (HurtBox hb in Enemies(owner, pb.corePosition, 20f))
                        {
                            if (hb.healthComponent.body) hb.healthComponent.body.AddTimedBuff(RoR2Content.Buffs.Slow60, 6f);
                            Partners.Beam(pb.corePosition, hb.transform.position, col, 0.12f);
                        }
                        Blast(owner, pb.corePosition, 20f, 3.5f * mult, col);
                    });
                    break;
                case PartnerAbility.TimeRoulette:
                {
                    bool heads = UnityEngine.Random.value < 0.5f;
                    // the coin spins above Tock first, then the result
                    for (int i = 0; i < 4; i++)
                    {
                        int k = i;
                        Later(owner, 0.15f * i, () => { if (pb) Partners.Glyph(pb.corePosition + Vector3.up * 2.2f, Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward, k % 2 == 0 ? new Color(1f, 0.85f, 0.35f) : col, 0.9f); });
                    }
                    Later(owner, 0.7f, () =>
                    {
                        if (!pb) return;
                        Vector3 at = pb.corePosition;
                        if (heads)
                        {
                            try { Chat.AddMessage("<color=#f2c640>Time Roulette: HEADS!</color> Time stops."); } catch { }
                            Partners.Pillar(pb.footPosition, new Color(1f, 0.85f, 0.35f), 3f);
                            Partners.Glyph(pb.footPosition + Vector3.up * 0.2f, Vector3.up, col, 9f);
                            foreach (HurtBox hb in Enemies(owner, at, 24f)) Partners.Beam(at, hb.transform.position, new Color(1f, 0.85f, 0.35f), 0.2f);
                            Blast(owner, at, 24f, 16f * mult, col, freeze: true);
                        }
                        else
                        {
                            try { Chat.AddMessage("<color=#ff7777>Time Roulette: tails...</color>"); } catch { }
                            hc.TakeDamage(new DamageInfo
                            {
                                damage = hc.fullCombinedHealth * 0.05f,
                                position = owner.corePosition,
                                procCoefficient = 0f,
                                damageColorIndex = DamageColorIndex.Default,
                                procChainMask = default(ProcChainMask)
                            });
                            Partners.Impact(owner.corePosition, new Color(1f, 0.3f, 0.3f), 1.5f);
                            Blast(owner, at, 12f, 4f * mult, col);
                        }
                    });
                    break;
                }

                // ---- Hollow Idol: violet soul beams
                case PartnerAbility.SoulGaze:
                    if (target)
                    {
                        Partners.Beam(from, target.transform.position, col, 0.3f);
                        Hit(owner, pb, target, 3f * mult, col);
                        HurtBox next = Nearest(owner, target.transform.position + Vector3.up * 0.01f, 18f);
                        List<HurtBox> near = Enemies(owner, target.transform.position, 18f);
                        HurtBox jump = near.Find(h => h != target);
                        if (jump) Later(owner, 0.1f, () => { if (target && jump) { Partners.Beam(target.transform.position, jump.transform.position, col, 0.2f); Hit(owner, pb, jump, 2f * mult, col); } });
                    }
                    break;
                case PartnerAbility.Absorb:
                {
                    if (!target) break;
                    HealthComponent th = target.healthComponent;
                    for (int i = 0; i < 3; i++)
                    {
                        int k = i;
                        Later(owner, 0.1f * i, () => { if (target && pb) Partners.Beam(pb.corePosition, target.transform.position, k == 2 ? Color.white : col, 0.5f - k * 0.12f); });
                    }
                    Later(owner, 0.3f, () =>
                    {
                        if (!target || !th) return;
                        Hit(owner, pb, target, 9f * mult, col, DamageType.Generic, DamageColorIndex.Void);
                        Partners.Impact(target.transform.position, Color.white, 2.6f);
                        if (!th.alive)
                        {
                            pm.AbsorbStacks = Mathf.Min(8, pm.AbsorbStacks + 1);
                            hc.HealFraction(0.15f, default(ProcChainMask));
                            if (pb) Partners.Beam(target.transform.position, pb.corePosition, Color.white, 0.6f);   // the soul flies home
                            Partners.Burst(owner.footPosition, col, 4f);
                            try { Chat.AddMessage($"<color=#c080ff>The Hollow Idol seals its soul! (+{pm.AbsorbStacks * 25}% damage)</color>"); } catch { }
                        }
                    });
                    break;
                }
                case PartnerAbility.HollowPull:
                    Partners.Glyph(from, Vector3.up, col, 5f);
                    Partners.Pillar(pb.footPosition, col, 2.4f);
                    foreach (HurtBox hb in Enemies(owner, from, 28f))
                    {
                        CharacterBody eb = hb.healthComponent.body;
                        Partners.Beam(from, hb.transform.position, col, 0.15f);
                        if (!eb || eb.isChampion) continue;
                        Vector3 pull = (from - eb.corePosition).normalized * 30f + Vector3.up * 6f;
                        if (eb.characterMotor) { eb.characterMotor.Motor.ForceUnground(); eb.characterMotor.velocity = pull; }
                        else if (eb.rigidbody) eb.rigidbody.velocity = pull;
                    }
                    Later(owner, 0.55f, () =>
                    {
                        if (!pb) return;
                        Partners.Impact(pb.corePosition, Color.white, 4f);
                        Blast(owner, pb.corePosition, 14f, 7f * mult, col);
                    });
                    break;

                // ---- Jinx: a card magician's show
                case PartnerAbility.CardJuggle:
                {
                    for (int i = 0; i < 4; i++)
                    {
                        int k = i;
                        Later(owner, 0.07f * i, () =>
                        {
                            if (!pb) return;
                            List<HurtBox> near = Enemies(owner, target ? target.transform.position : pb.corePosition, 25f);
                            HurtBox t = near.Count > 0 ? near[k % near.Count] : target;
                            Orb(owner, pb, t, 1.3f * mult, LightningOrb.LightningType.Ukulele, 2, false, 0.7f);
                        });
                    }
                    break;
                }
                case PartnerAbility.MindTaunt:
                    pm.TauntUntil = Time.time + 6f;
                    pb.AddTimedBuff(RoR2Content.Buffs.ArmorBoost, 6f);
                    foreach (HurtBox hb in Enemies(owner, from, 40f)) Partners.Beam(from, hb.transform.position, new Color(1f, 0.4f, 0.8f), 0.08f);
                    Blast(owner, from, 10f, 3f * mult, col);
                    break;
                case PartnerAbility.Encore:
                    Partners.Pillar(pb.footPosition, col, 2.6f);
                    Partners.Glyph(pb.footPosition + Vector3.up * 0.2f, Vector3.up, col, 7f);
                    Later(owner, 0.45f, () =>
                    {
                        if (!pb) return;
                        Partners.Impact(pb.corePosition, Color.white, 5f);
                        Blast(owner, pb.corePosition, 16f, 13f * mult, col);
                        pb.healthComponent.HealFraction(1f, default(ProcChainMask));
                        pm.TauntUntil = Time.time + 4f;
                        Partners.Circle(pb.footPosition, col, 1f);
                    });
                    break;

                // ---- Gloop: splashy slime
                case PartnerAbility.Glob:
                    Partners.Impact(from + toward * 0.5f, col, 0.8f);
                    Orb(owner, pb, target, 2.8f * mult, LightningOrb.LightningType.TreePoisonDart, 1, true);
                    break;
                case PartnerAbility.StickyPuddle:
                {
                    Vector3 at = AimPoint(aim);
                    Partners.Beam(from, at, col, 0.45f);
                    Later(owner, 0.15f, () =>
                    {
                        Partners.Glyph(at + Vector3.up * 0.15f, Vector3.up, col, 5f);
                        Partners.Impact(at, col, 3f);
                        Blast(owner, at, 13f, 6f * mult, col, slow: true);
                    });
                    break;
                }
                case PartnerAbility.Reform:
                    Partners.Pillar(owner.footPosition, col, 2f);
                    Partners.Glyph(owner.footPosition + Vector3.up * 0.1f, Vector3.up, col, 4f);
                    hc.HealFraction(0.4f, default(ProcChainMask));
                    pb.healthComponent.HealFraction(1f, default(ProcChainMask));
                    Later(owner, 0.3f, () => { if (pb) { Partners.Impact(pb.corePosition, col, 3f); Blast(owner, pb.corePosition, 10f, 5f * mult, col); } });
                    break;
            }
        }
    }

    /// <summary>Runs the follow-up hits of a partner combo on the host, a beat apart.</summary>
    public class PartnerCombo : MonoBehaviour
    {
        private readonly List<KeyValuePair<float, Action>> _queue = new List<KeyValuePair<float, Action>>();

        public static void After(GameObject host, float delay, Action act)
        {
            if (act == null) return;
            if (!host || delay <= 0f) { try { act(); } catch (Exception e) { DuelistPlugin.Log.LogWarning("Partner combo step failed: " + e.Message); } return; }
            PartnerCombo c = host.GetComponent<PartnerCombo>() ?? host.AddComponent<PartnerCombo>();
            c._queue.Add(new KeyValuePair<float, Action>(Time.time + delay, act));
        }

        private void Update()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                if (Time.time < _queue[i].Key) continue;
                Action a = _queue[i].Value;
                _queue.RemoveAt(i);
                try { a(); } catch (Exception e) { DuelistPlugin.Log.LogWarning("Partner combo step failed: " + e.Message); }
            }
        }
    }

    /// <summary>
    /// The anime attack-name call: a slanted band sweeps across your screen with the partner's name and
    /// the move, speed lines streaking behind it. Only on the screen of the player who used it.
    /// </summary>
    public class AnimeCutIn : MonoBehaviour
    {
        private static AnimeCutIn _inst;
        private string _who, _move;
        private Color _color;
        private bool _ultimate;
        private float _born = -10f;
        private static Texture2D _white;
        private GUIStyle _big, _small;
        private readonly float[] _lines = new float[18];

        public static void Show(string who, string move, Color color, bool ultimate)
        {
            if (!_inst)
            {
                GameObject go = new GameObject("DuelistCutIn");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<AnimeCutIn>();
            }
            _inst._who = who;
            _inst._move = move;
            _inst._color = color;
            _inst._ultimate = ultimate;
            _inst._born = Time.unscaledTime;
            for (int i = 0; i < _inst._lines.Length; i++) _inst._lines[i] = UnityEngine.Random.value;
        }

        private void OnGUI()
        {
            float age = Time.unscaledTime - _born;
            float life = _ultimate ? 1.5f : 0.9f;
            if (age < 0f || age > life || string.IsNullOrEmpty(_move)) return;
            if (!_white) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = false };
                _small = new GUIStyle(_big) { fontStyle = FontStyle.Bold };
            }
            float sw = Screen.width, sh = Screen.height;
            float k = sh / 1080f;
            float inT = Mathf.Clamp01(age / 0.14f), outT = Mathf.Clamp01((age - (life - 0.25f)) / 0.25f);
            float slide = (1f - (1f - inT) * (1f - inT)) - outT;
            float x = Mathf.Lerp(sw, 0f, slide) + outT * -sw * 0.3f;
            float h = (_ultimate ? 190f : 120f) * k;
            float y = sh * (_ultimate ? 0.3f : 0.22f);
            float a = 1f - outT;

            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(-6f, new Vector2(sw * 0.5f, y + h * 0.5f));
            Color oc = GUI.color;
            // band
            GUI.color = new Color(0.02f, 0.01f, 0.05f, 0.88f * a);
            GUI.DrawTexture(new Rect(x - sw * 0.2f, y, sw * 1.4f, h), _white);
            GUI.color = new Color(_color.r, _color.g, _color.b, a);
            GUI.DrawTexture(new Rect(x - sw * 0.2f, y, sw * 1.4f, 6f * k), _white);
            GUI.DrawTexture(new Rect(x - sw * 0.2f, y + h - 6f * k, sw * 1.4f, 6f * k), _white);
            // speed lines streaking left
            for (int i = 0; i < _lines.Length; i++)
            {
                float ly = y + 10f * k + _lines[i] * (h - 20f * k);
                float len = (120f + _lines[(i + 5) % _lines.Length] * 380f) * k;
                float lx = ((sw - (age * 2600f * k + _lines[(i + 3) % _lines.Length] * sw)) % (sw * 1.4f) + sw * 1.4f) % (sw * 1.4f) - sw * 0.2f;
                GUI.color = new Color(1f, 1f, 1f, 0.22f * a);
                GUI.DrawTexture(new Rect(lx, ly, len, 2f * k), _white);
            }
            // text
            _small.fontSize = Mathf.RoundToInt((_ultimate ? 30f : 24f) * k);
            _big.fontSize = Mathf.RoundToInt((_ultimate ? 86f : 58f) * k);
            Rect tr = new Rect(x, y, sw, h);
            GUI.color = new Color(1f, 1f, 1f, a);
            _small.normal.textColor = new Color(_color.r, _color.g, _color.b, a);
            GUI.Label(new Rect(tr.x, tr.y + 4f * k, tr.width, h * 0.3f), _who, _small);
            _big.normal.textColor = new Color(0f, 0f, 0f, 0.85f * a);
            GUI.Label(new Rect(tr.x + 4f * k, tr.y + h * 0.26f + 4f * k, tr.width, h * 0.7f), _move, _big);
            _big.normal.textColor = Color.Lerp(Color.white, _color, 0.25f) * new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(tr.x, tr.y + h * 0.26f, tr.width, h * 0.7f), _move, _big);
            GUI.color = oc;
            GUI.matrix = old;
        }
    }

    // ===================================================================== visuals

    /// <summary>
    /// Builds each partner's look from shapes on every machine: a bigger body, a glow light, a motion
    /// trail and its golden relic orbiting it. Hides the drone underneath and animates everything.
    /// </summary>
    public class PartnerVisual : MonoBehaviour
    {
        public PartnerKind Kind;
        public bool OnBoard;            // shown on a duel monster zone: no movement trail

        private Transform _root, _body, _relic, _orbit;
        private CharacterModel _model;
        private readonly List<Transform> _spinners = new List<Transform>();
        private readonly List<Transform> _orbiters = new List<Transform>();
        private Transform _wingL, _wingR, _wingL2, _wingR2;
        private float _seed;
        private Color _color = Color.white;

        private void Start()
        {
            _seed = UnityEngine.Random.value * 10f;
            PartnerInfo info = Partners.Info(Kind);
            if (info != null) _color = info.Color;
            ModelLocator ml = GetComponent<ModelLocator>();
            Transform model = ml && ml.modelTransform ? ml.modelTransform : transform;
            _model = model.GetComponent<CharacterModel>();
            HideDrone(model);

            _root = new GameObject("PartnerLook").transform;
            _root.SetParent(model, false);
            _root.localScale = Vector3.one * Mathf.Clamp(DuelConfig.PartnerSize.Value, 0.2f, 3f);
            _body = new GameObject("Body").transform;
            _body.SetParent(_root, false);
            try
            {
                if (PartnerModels.TryBuild(Kind, _body, out Dictionary<string, Transform> parts)) UseModelParts(parts);
                else
                {
                    Build();
                    BuildRelic(info);
                }
                AddGlow();
            }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Partner look failed: " + e.Message); }
        }

        /// <summary>Hooks the Blender model's named parts up to the existing animations.</summary>
        private void UseModelParts(Dictionary<string, Transform> parts)
        {
            parts.TryGetValue("wingL", out _wingL);
            parts.TryGetValue("wingR", out _wingR);
            parts.TryGetValue("wingL2", out _wingL2);
            parts.TryGetValue("wingR2", out _wingR2);
            if (parts.TryGetValue("Hands", out Transform hands)) _spinners.Add(hands);   // first spinner: the fast one
            foreach (KeyValuePair<string, Transform> kv in parts)
            {
                if (kv.Key.StartsWith("Gear") || kv.Key == "Halo") _spinners.Add(kv.Value);
                else if (kv.Key.StartsWith("Orbit")) _orbiters.Add(kv.Value);
            }
        }

        private void HideDrone(Transform model)
        {
            if (_model && _model.baseRendererInfos != null && _model.baseRendererInfos.Length > 0)
                _model.baseRendererInfos = new CharacterModel.RendererInfo[0];
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                if (!_root || !r.transform.IsChildOf(_root)) r.enabled = false;
        }

        private void LateUpdate()
        {
            if (_model && _model.baseRendererInfos != null && _model.baseRendererInfos.Length > 0) HideDrone(_model.transform);
        }

        private GameObject P(PrimitiveType t, Vector3 pos, Vector3 scale, Color c, float glow = 0.3f, Transform parent = null, Vector3? euler = null)
        {
            GameObject g = DuelistVisuals.Primitive(t, "Part", parent ? parent : _body, pos, scale, DuelistVisuals.MakeMaterial(c, c * glow));
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            return g;
        }

        private Transform Pivot(string name, Vector3 pos, Transform parent = null)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent ? parent : _body, false);
            t.localPosition = pos;
            return t;
        }

        private void Build()
        {
            Color black = new Color(0.04f, 0.04f, 0.06f);
            Color gold = new Color(1f, 0.82f, 0.35f);
            switch (Kind)
            {
                case PartnerKind.Featherpuff:
                {
                    Color fluff = new Color(0.98f, 0.95f, 0.9f);
                    // A fluffy cloud of overlapping puffs.
                    P(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.75f, fluff, 0.5f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.3f, Mathf.Sin(a * 2f) * 0.08f, Mathf.Sin(a) * 0.3f), Vector3.one * 0.38f, fluff, 0.45f);
                    }
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.32f, 0f), Vector3.one * 0.35f, fluff, 0.5f);
                    // Big eyes with shine, a little beak, a golden halo.
                    for (int s = -1; s <= 1; s += 2)
                    {
                        P(PrimitiveType.Sphere, new Vector3(0.16f * s, 0.08f, 0.36f), new Vector3(0.17f, 0.2f, 0.1f), black, 0f);
                        P(PrimitiveType.Sphere, new Vector3(0.13f * s + 0.03f, 0.14f, 0.41f), Vector3.one * 0.05f, Color.white, 1.5f);
                    }
                    P(PrimitiveType.Sphere, new Vector3(0f, -0.06f, 0.42f), new Vector3(0.1f, 0.06f, 0.08f), new Color(1f, 0.6f, 0.25f), 0.5f);
                    Transform halo = Pivot("Halo", new Vector3(0f, 0.62f, 0f));
                    P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.45f, 0.012f, 0.45f), gold, 1.5f, halo);
                    _spinners.Add(halo);
                    // Two pairs of wings (big + small), white with gold tips.
                    _wingL = Pivot("WingL", new Vector3(-0.38f, 0.12f, -0.1f));
                    _wingR = Pivot("WingR", new Vector3(0.38f, 0.12f, -0.1f));
                    _wingL2 = Pivot("WingL2", new Vector3(-0.32f, -0.12f, -0.1f));
                    _wingR2 = Pivot("WingR2", new Vector3(0.32f, -0.12f, -0.1f));
                    foreach (Transform w in new[] { _wingL, _wingR, _wingL2, _wingR2 })
                    {
                        float side = w.localPosition.x < 0f ? -1f : 1f;
                        bool big = w == _wingL || w == _wingR;
                        float len = big ? 0.75f : 0.45f;
                        for (int f = 0; f < 3; f++)
                            P(PrimitiveType.Cube, new Vector3(side * len * 0.5f, 0.04f * f, -0.08f * f), new Vector3(len - 0.12f * f, 0.035f, 0.26f), Color.white, 0.8f, w, new Vector3(0f, 0f, side * 8f * f));
                        P(PrimitiveType.Cube, new Vector3(side * len, 0f, 0f), new Vector3(0.12f, 0.04f, 0.22f), gold, 1f, w);
                    }
                    break;
                }
                case PartnerKind.Tock:
                {
                    Color robe = new Color(0.25f, 0.15f, 0.45f);
                    // Robed caster: cone robe, clock-face head, big hat with a brim, a clock staff, floating gears.
                    P(PrimitiveType.Cylinder, new Vector3(0f, -0.35f, 0f), new Vector3(0.6f, 0.3f, 0.6f), robe, 0.25f);
                    P(PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(0.42f, 0.2f, 0.42f), robe * 1.2f, 0.25f);
                    P(PrimitiveType.Cylinder, new Vector3(0f, -0.62f, 0f), new Vector3(0.72f, 0.03f, 0.72f), gold, 0.8f);
                    GameObject face = P(PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0f), new Vector3(0.52f, 0.06f, 0.52f), new Color(0.8f, 0.6f, 0.25f), 0.5f, null, new Vector3(90f, 0f, 0f));
                    P(PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0.035f), new Vector3(0.44f, 0.02f, 0.44f), new Color(0.97f, 0.93f, 0.82f), 0.7f, null, new Vector3(90f, 0f, 0f));
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        P(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.19f, 0.32f + Mathf.Sin(a) * 0.19f, 0.06f), Vector3.one * (i % 3 == 0 ? 0.035f : 0.02f), black, 0f);
                    }
                    for (int i = 0; i < 2; i++)
                    {
                        Transform hand = Pivot("Hand" + i, new Vector3(0f, 0.32f, 0.07f));
                        P(PrimitiveType.Cube, new Vector3(0f, i == 0 ? 0.09f : 0.06f, 0f), new Vector3(0.025f, i == 0 ? 0.18f : 0.12f, 0.015f), black, 0f, hand);
                        _spinners.Add(hand);
                    }
                    P(PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.7f, 0.02f, 0.7f), robe, 0.3f);
                    P(PrimitiveType.Capsule, new Vector3(0f, 0.82f, -0.04f), new Vector3(0.32f, 0.28f, 0.32f), robe, 0.3f, null, new Vector3(-12f, 0f, 0f));
                    P(PrimitiveType.Sphere, new Vector3(0f, 1.08f, -0.1f), Vector3.one * 0.1f, gold, 1.5f);
                    // Staff with a glowing clock-orb.
                    P(PrimitiveType.Cylinder, new Vector3(0.42f, -0.05f, 0.1f), new Vector3(0.04f, 0.6f, 0.04f), new Color(0.35f, 0.22f, 0.1f), 0.2f);
                    P(PrimitiveType.Sphere, new Vector3(0.42f, 0.6f, 0.1f), Vector3.one * 0.18f, _color, 2f);
                    // Two gears orbiting.
                    for (int g = 0; g < 2; g++)
                    {
                        Transform gear = Pivot("Gear" + g, new Vector3(g == 0 ? -0.55f : 0.55f, g == 0 ? 0.35f : -0.25f, -0.2f));
                        P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.28f, 0.03f, 0.28f), gold, 0.8f, gear, new Vector3(90f, 0f, 0f));
                        for (int t = 0; t < 8; t++)
                        {
                            float a = t / 8f * Mathf.PI * 2f;
                            P(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.16f, Mathf.Sin(a) * 0.16f, 0f), new Vector3(0.06f, 0.06f, 0.04f), gold, 0.8f, gear, new Vector3(0f, 0f, a * Mathf.Rad2Deg));
                        }
                        _spinners.Add(gear);
                    }
                    break;
                }
                case PartnerKind.HollowIdol:
                {
                    Color stone = new Color(0.14f, 0.11f, 0.2f);
                    // A huge glowing eye in a cracked stone frame, shards and runes orbiting.
                    P(PrimitiveType.Cube, Vector3.zero, new Vector3(0.95f, 0.75f, 0.4f), stone, 0.15f);
                    P(PrimitiveType.Cube, new Vector3(0f, 0.48f, 0f), new Vector3(1.1f, 0.14f, 0.45f), stone * 1.3f, 0.15f);
                    P(PrimitiveType.Cube, new Vector3(0f, -0.46f, 0f), new Vector3(0.8f, 0.14f, 0.45f), stone * 1.3f, 0.15f);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.18f), new Vector3(0.75f, 0.42f, 0.3f), new Color(0.95f, 0.9f, 1f), 0.6f);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.3f), Vector3.one * 0.3f, _color, 2.5f);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.42f), new Vector3(0.1f, 0.2f, 0.06f), black, 0f);
                    P(PrimitiveType.Cube, new Vector3(0f, -0.3f, 0.22f), new Vector3(0.04f, 0.25f, 0.04f), _color, 2f);
                    Transform shards = Pivot("Shards", Vector3.zero);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        P(PrimitiveType.Cube, new Vector3(Mathf.Cos(a) * 0.85f, Mathf.Sin(a * 2f) * 0.15f, Mathf.Sin(a) * 0.85f), new Vector3(0.12f, 0.32f, 0.12f), stone * 1.6f, 0.4f, shards, new Vector3(30f * i, 45f, 20f));
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a + 0.5f) * 0.7f, 0.25f, Mathf.Sin(a + 0.5f) * 0.7f), Vector3.one * 0.07f, _color, 3f, shards);
                    }
                    _orbiters.Add(shards);
                    break;
                }
                case PartnerKind.Jinx:
                {
                    Color red = new Color(0.8f, 0.08f, 0.14f), dark = new Color(0.1f, 0.08f, 0.14f);
                    // Jester: two-tone body, ruff collar, masked face, three-pointed hat with bells, juggling balls.
                    P(PrimitiveType.Capsule, new Vector3(-0.1f, -0.3f, 0f), new Vector3(0.28f, 0.38f, 0.38f), red, 0.25f);
                    P(PrimitiveType.Capsule, new Vector3(0.1f, -0.3f, 0f), new Vector3(0.28f, 0.38f, 0.38f), dark, 0.25f);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.22f, 0.05f, Mathf.Sin(a) * 0.22f), new Vector3(0.14f, 0.07f, 0.14f), Color.white, 0.5f);
                    }
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.4f, new Color(0.97f, 0.97f, 0.97f), 0.4f);
                    P(PrimitiveType.Cube, new Vector3(0f, 0.34f, 0.17f), new Vector3(0.32f, 0.09f, 0.04f), dark, 0.2f);
                    for (int s = -1; s <= 1; s += 2) P(PrimitiveType.Sphere, new Vector3(0.08f * s, 0.34f, 0.2f), Vector3.one * 0.05f, _color, 3f);
                    P(PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.19f), new Vector3(0.16f, 0.025f, 0.02f), red, 1f); // grin
                    for (int i = -1; i <= 1; i++)
                    {
                        Color c = i == 0 ? dark : (i < 0 ? red : new Color(0.95f, 0.75f, 0.2f));
                        P(PrimitiveType.Capsule, new Vector3(0.24f * i, 0.62f, i == 0 ? -0.08f : 0f), new Vector3(0.12f, 0.24f, 0.12f), c, 0.3f, null, new Vector3(i == 0 ? -25f : 0f, 0f, -40f * i));
                        P(PrimitiveType.Sphere, new Vector3(0.42f * i, i == 0 ? 0.88f : 0.8f, i == 0 ? -0.2f : 0f), Vector3.one * 0.1f, new Color(1f, 0.85f, 0.25f), 1.2f);
                    }
                    // Arms out, juggling three glowing balls.
                    P(PrimitiveType.Capsule, new Vector3(-0.32f, -0.12f, 0.08f), new Vector3(0.09f, 0.2f, 0.09f), red, 0.25f, null, new Vector3(0f, 0f, 60f));
                    P(PrimitiveType.Capsule, new Vector3(0.32f, -0.12f, 0.08f), new Vector3(0.09f, 0.2f, 0.09f), dark, 0.25f, null, new Vector3(0f, 0f, -60f));
                    Transform juggle = Pivot("Juggle", new Vector3(0f, 0.15f, 0.35f));
                    Color[] balls = { _color, new Color(1f, 0.85f, 0.25f), new Color(0.6f, 0.3f, 1f) };
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i / 3f * Mathf.PI * 2f;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.38f, Mathf.Sin(a) * 0.38f, 0f), Vector3.one * 0.12f, balls[i], 2.5f, juggle);
                    }
                    _spinners.Add(juggle);
                    break;
                }
                default: // Gloop
                {
                    Color slime = new Color(0.25f, 0.85f, 0.3f);
                    // A big slime with a glowing core, drips, and little satellite blobs.
                    P(PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(1f, 0.75f, 1f), slime, 0.6f);
                    P(PrimitiveType.Sphere, new Vector3(0f, -0.25f, 0f), new Vector3(1.15f, 0.35f, 1.15f), slime * 0.85f, 0.5f);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0f), Vector3.one * 0.35f, new Color(0.85f, 1f, 0.5f), 3f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f + 0.3f;
                        P(PrimitiveType.Capsule, new Vector3(Mathf.Cos(a) * 0.45f, -0.38f, Mathf.Sin(a) * 0.45f), new Vector3(0.12f, 0.12f, 0.12f), slime, 0.6f);
                    }
                    for (int s = -1; s <= 1; s += 2)
                    {
                        P(PrimitiveType.Sphere, new Vector3(0.17f * s, 0.16f, 0.38f), Vector3.one * 0.2f, Color.white, 0.4f);
                        P(PrimitiveType.Sphere, new Vector3(0.17f * s, 0.16f, 0.47f), Vector3.one * 0.09f, black, 0f);
                    }
                    P(PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.45f), new Vector3(0.18f, 0.03f, 0.03f), black, 0f, null, new Vector3(0f, 0f, 0f)); // smile
                    Transform blobs = Pivot("Blobs", Vector3.zero);
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i / 3f * Mathf.PI * 2f;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.8f, 0.1f, Mathf.Sin(a) * 0.8f), Vector3.one * 0.22f, slime, 0.8f, blobs);
                    }
                    _orbiters.Add(blobs);
                    break;
                }
            }
        }

        /// <summary>The partner's golden relic, orbiting slowly around it.</summary>
        private void BuildRelic(PartnerInfo info)
        {
            if (info == null) return;
            _orbit = Pivot("RelicOrbit", Vector3.zero, _root);
            _relic = Pivot("Relic", new Vector3(0.95f, 0.55f, 0f), _orbit);
            _relic.localScale = Vector3.one * 0.32f;
            Color gold = new Color(1f, 0.82f, 0.32f), bright = new Color(1f, 0.95f, 0.65f);
            Transform r = _relic;
            switch (info.RelicSymbol)
            {
                case DuelistIcons.Symbol.RelicNecklace:
                    for (int i = 0; i <= 8; i++)
                    {
                        float a = Mathf.PI + i / 8f * Mathf.PI;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.6f + 0.3f, 0f), Vector3.one * 0.2f, gold, 1f, r);
                    }
                    P(PrimitiveType.Cube, new Vector3(0f, -0.55f, 0f), new Vector3(0.25f, 0.25f, 0.08f), bright, 1.5f, r, new Vector3(0f, 0f, 45f));
                    break;
                case DuelistIcons.Symbol.RelicRing:
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        P(PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.6f, Mathf.Sin(a) * 0.6f, 0f), Vector3.one * 0.17f, gold, 1f, r);
                    }
                    P(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.3f, bright, 1.5f, r);
                    for (int i = 0; i < 4; i++)
                        P(PrimitiveType.Cube, Vector3.zero, new Vector3(0.9f, 0.05f, 0.05f), gold, 1f, r, new Vector3(0f, 0f, i * 45f));
                    break;
                case DuelistIcons.Symbol.RelicEye:
                    P(PrimitiveType.Sphere, Vector3.zero, new Vector3(1f, 0.5f, 0.2f), gold, 1f, r);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0f, 0.08f), Vector3.one * 0.3f, bright, 1.5f, r);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0f, 0.16f), Vector3.one * 0.12f, new Color(0.1f, 0.05f, 0.15f), 0f, r);
                    P(PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1f, 0.08f, 0.08f), gold, 1f, r);
                    P(PrimitiveType.Cube, new Vector3(0.05f, -0.45f, 0f), new Vector3(0.07f, 0.4f, 0.07f), gold, 1f, r);
                    break;
                case DuelistIcons.Symbol.RelicRod:
                    P(PrimitiveType.Cylinder, new Vector3(0f, -0.2f, 0f), new Vector3(0.1f, 0.65f, 0.1f), gold, 1f, r, new Vector3(0f, 0f, 20f));
                    P(PrimitiveType.Sphere, new Vector3(-0.2f, 0.48f, 0f), Vector3.one * 0.3f, bright, 1.5f, r);
                    P(PrimitiveType.Cube, new Vector3(0.05f, 0.5f, 0f), new Vector3(0.45f, 0.08f, 0.06f), gold, 1f, r, new Vector3(0f, 0f, -25f));
                    P(PrimitiveType.Cube, new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.45f, 0.08f, 0.06f), gold, 1f, r, new Vector3(0f, 0f, 25f));
                    break;
                case DuelistIcons.Symbol.RelicScale:
                    P(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.08f, 0.6f, 0.08f), gold, 1f, r);
                    P(PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(1.2f, 0.07f, 0.07f), gold, 1f, r);
                    P(PrimitiveType.Sphere, new Vector3(0f, 0.65f, 0f), Vector3.one * 0.15f, bright, 1.5f, r);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        P(PrimitiveType.Cylinder, new Vector3(0.55f * s, 0.2f, 0f), new Vector3(0.02f, 0.3f, 0.02f), gold, 1f, r);
                        P(PrimitiveType.Cylinder, new Vector3(0.55f * s, -0.1f, 0f), new Vector3(0.45f, 0.03f, 0.45f), gold, 1f, r);
                    }
                    P(PrimitiveType.Cube, new Vector3(0f, -0.6f, 0f), new Vector3(0.6f, 0.07f, 0.2f), gold, 1f, r);
                    break;
            }
        }

        /// <summary>A colored point light and a motion trail, so it reads at a distance.</summary>
        private void AddGlow()
        {
            Light l = _root.gameObject.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = _color;
            l.range = 6f * Mathf.Clamp(DuelConfig.PartnerSize.Value, 0.4f, 2f);
            l.intensity = 2f;

            if (OnBoard) { l.range *= 2.5f; l.intensity = 3f; return; }
            Shader unlit = DuelistVisuals.UnlitShader;
            if (!unlit) return;
            TrailRenderer tr = _body.gameObject.AddComponent<TrailRenderer>();
            tr.material = new Material(unlit);
            tr.time = 0.35f;
            tr.startWidth = 0.35f * Mathf.Clamp(DuelConfig.PartnerSize.Value, 0.3f, 2f);
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.1f;
            tr.startColor = new Color(_color.r, _color.g, _color.b, 0.8f);
            tr.endColor = new Color(_color.r, _color.g, _color.b, 0f);
        }

        private void Update()
        {
            if (!_root) return;
            float t = Time.time + _seed;
            _body.localPosition = new Vector3(0f, Mathf.Sin(t * 2.5f) * 0.08f, 0f);
            float flap = Mathf.Sin(t * 16f);
            if (_wingL) _wingL.localRotation = Quaternion.Euler(0f, 0f, flap * 40f);
            if (_wingR) _wingR.localRotation = Quaternion.Euler(0f, 0f, -flap * 40f);
            if (_wingL2) _wingL2.localRotation = Quaternion.Euler(0f, 0f, -flap * 25f);
            if (_wingR2) _wingR2.localRotation = Quaternion.Euler(0f, 0f, flap * 25f);

            for (int i = 0; i < _spinners.Count; i++)
            {
                Transform s = _spinners[i];
                switch (Kind)
                {
                    case PartnerKind.Tock:
                        s.localRotation = s.name.StartsWith("Gear") ? Quaternion.Euler(0f, 0f, t * (i % 2 == 0 ? 90f : -120f))
                                                                     : Quaternion.Euler(0f, 0f, -t * (i == 0 ? 360f : 30f));
                        break;
                    case PartnerKind.Jinx: s.localRotation = Quaternion.Euler(0f, 0f, t * 300f); break;
                    default: s.localRotation = Quaternion.Euler(0f, t * 90f, 0f); break;
                }
            }
            foreach (Transform o in _orbiters) o.localRotation = Quaternion.Euler(Mathf.Sin(t) * 10f, t * 50f, 0f);
            if (_orbit) _orbit.localRotation = Quaternion.Euler(0f, t * 70f, 0f);
            if (_relic) _relic.localRotation = Quaternion.Euler(0f, -t * 70f + 180f, Mathf.Sin(t * 2f) * 10f); // keeps facing out

            if (Kind == PartnerKind.Gloop)
            {
                float w = Mathf.Sin(t * 5f) * 0.09f;
                _body.localScale = new Vector3(1f + w, 1f - w, 1f + w);
            }
            if (Kind == PartnerKind.HollowIdol) _body.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.8f) * 15f, 0f);
        }
    }

    /// <summary>
    /// Every partner/duelist effect in one networked prefab, tinted with effectData.color, picked by genericFloat:
    ///  0 summoning circle · 1 shockwave · 2 anime impact (flash + speed lines) · 3 beam (start → origin)
    ///  4 glyph (spinning card circle facing effectData.rotation's up) · 5 pillar of light with rising cards.
    /// </summary>
    public class SummonCircleEffect : MonoBehaviour
    {
        private float _life = 1.4f;
        private Transform _ring, _ring2, _pillar, _disc, _core;
        private readonly List<Transform> _spikes = new List<Transform>();
        private readonly List<Vector3> _dirs = new List<Vector3>();
        private float _t, _scale = 1f, _len;
        private int _mode;

        private void Start()
        {
            Color color = new Color(1f, 0.85f, 0.35f);
            Vector3 start = transform.position;
            Quaternion rot = Quaternion.identity;
            EffectComponent ec = GetComponent<EffectComponent>();
            if (ec && ec.effectData != null)
            {
                _scale = Mathf.Max(0.05f, ec.effectData.scale);
                _mode = Mathf.RoundToInt(ec.effectData.genericFloat);
                Color32 c = ec.effectData.color;
                if (c.a > 0) color = c;
                start = ec.effectData.start;
                rot = ec.effectData.rotation;
            }

            Material glow = DuelistVisuals.MakeMaterial(color, color * 2.5f);
            Material white = DuelistVisuals.MakeMaterial(Color.white, Color.white * 2.2f);
            Material gold = DuelistVisuals.MakeMaterial(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.8f, 0.2f) * 2f);

            switch (_mode)
            {
                case 1:
                {
                    _life = 0.5f;
                    _disc = DuelistVisuals.Primitive(PrimitiveType.Sphere, "Flash", transform, Vector3.zero, Vector3.one * 0.1f, glow).transform;
                    _ring = new GameObject("Wave").transform;
                    _ring.SetParent(transform, false);
                    for (int i = 0; i < 20; i++)
                    {
                        float a = i / 20f * Mathf.PI * 2f;
                        GameObject chip = DuelistVisuals.Primitive(PrimitiveType.Cube, "Chip", _ring, new Vector3(Mathf.Cos(a), 0.1f, Mathf.Sin(a)), new Vector3(0.12f, 0.03f, 0.2f), i % 2 == 0 ? glow : gold);
                        chip.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    }
                    return;
                }
                case 2:
                {
                    _life = 0.34f;
                    Material hot = DuelistVisuals.MakeMaterial(Color.Lerp(color, Color.white, 0.45f), Color.Lerp(color, Color.white, 0.45f) * 2f);
                    _core = DuelistVisuals.Primitive(PrimitiveType.Sphere, "Flash", transform, Vector3.zero, Vector3.one * 0.1f, hot).transform;
                    for (int i = 0; i < 9; i++)
                    {
                        Vector3 d = UnityEngine.Random.onUnitSphere;
                        GameObject sp = DuelistVisuals.Primitive(PrimitiveType.Cube, "SpeedLine", transform, Vector3.zero, new Vector3(0.05f, 0.05f, 1f), glow);
                        sp.transform.localRotation = Quaternion.LookRotation(d);
                        _spikes.Add(sp.transform);
                        _dirs.Add(d);
                    }
                    return;
                }
                case 3:
                {
                    _life = 0.42f;
                    Vector3 d = transform.position - start;
                    _len = d.magnitude;
                    if (_len < 0.05f) { _life = 0f; return; }
                    transform.position = (transform.position + start) * 0.5f;
                    transform.rotation = Quaternion.FromToRotation(Vector3.up, d / _len);
                    _pillar = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Beam", transform, Vector3.zero, new Vector3(_scale, _len * 0.5f, _scale), glow).transform;
                    _core = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "BeamCore", transform, Vector3.zero, new Vector3(_scale * 0.4f, _len * 0.5f, _scale * 0.4f), white).transform;
                    _disc = DuelistVisuals.Primitive(PrimitiveType.Sphere, "Tip", transform, new Vector3(0f, _len * 0.5f, 0f), Vector3.one * _scale * 2.5f, white).transform;
                    return;
                }
                case 4:
                {
                    _life = 0.75f;
                    transform.rotation = rot;
                    _disc = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Disc", transform, Vector3.zero, new Vector3(0.1f, 0.005f, 0.1f), glow).transform;
                    _ring = new GameObject("Ring").transform;
                    _ring.SetParent(transform, false);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        GameObject card = DuelistVisuals.Primitive(PrimitiveType.Cube, "Card", _ring, new Vector3(Mathf.Cos(a), 0.02f, Mathf.Sin(a)), new Vector3(0.22f, 0.015f, 0.32f), i % 2 == 0 ? gold : glow);
                        card.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                    }
                    _ring2 = new GameObject("Runes").transform;
                    _ring2.SetParent(transform, false);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        GameObject r = DuelistVisuals.Primitive(PrimitiveType.Cube, "Rune", _ring2, new Vector3(Mathf.Cos(a) * 0.62f, 0.03f, Mathf.Sin(a) * 0.62f), new Vector3(0.5f, 0.012f, 0.05f), white);
                        r.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                    }
                    transform.localScale = Vector3.one * 0.01f;
                    return;
                }
                case 5:
                {
                    _life = 1.1f;
                    _pillar = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Pillar", transform, new Vector3(0f, 4f, 0f), new Vector3(1f, 4f, 1f), glow).transform;
                    _core = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "PillarCore", transform, new Vector3(0f, 4f, 0f), new Vector3(0.4f, 4f, 0.4f), white).transform;
                    _ring = new GameObject("Rising").transform;
                    _ring.SetParent(transform, false);
                    for (int i = 0; i < 10; i++)
                    {
                        float a = i / 10f * Mathf.PI * 2f;
                        GameObject card = DuelistVisuals.Primitive(PrimitiveType.Cube, "Card", _ring, new Vector3(Mathf.Cos(a) * 1.3f, i * 0.6f, Mathf.Sin(a) * 1.3f), new Vector3(0.35f, 0.5f, 0.02f), i % 2 == 0 ? gold : glow);
                        card.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f);
                    }
                    transform.localScale = Vector3.one * _scale;
                    return;
                }
            }

            _disc = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Disc", transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.01f, 0.1f), glow).transform;
            _ring = new GameObject("Ring").transform;
            _ring.SetParent(transform, false);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f;
                GameObject card = DuelistVisuals.Primitive(PrimitiveType.Cube, "Card", _ring, new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)), new Vector3(0.35f, 0.02f, 0.5f), i % 2 == 0 ? gold : glow);
                card.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            }
            _pillar = DuelistVisuals.Primitive(PrimitiveType.Cylinder, "Pillar", transform, new Vector3(0f, 2f, 0f), new Vector3(0.6f, 2f, 0.6f), glow).transform;
            transform.localScale = Vector3.one * _scale * 2f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _life > 0f ? Mathf.Clamp01(_t / _life) : 1f;
            if (_t >= _life) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;

            switch (_mode)
            {
                case 1:
                {
                    float r = Mathf.Lerp(0.5f, _scale, 1f - (1f - k) * (1f - k));
                    if (_ring) { _ring.localScale = Vector3.one * r; _ring.Rotate(Vector3.up, 200f * dt, Space.Self); }
                    if (_disc) _disc.localScale = Vector3.one * Mathf.Lerp(_scale * 0.4f, 0.05f, k);
                    return;
                }
                case 2:
                {
                    float pop = k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f;
                    if (_core) _core.localScale = Vector3.one * _scale * 0.7f * pop;
                    if (_disc) _disc.localScale = Vector3.one * _scale * 1.3f * Mathf.Sqrt(Mathf.Max(0f, pop));
                    float reach = _scale * (0.4f + 2.2f * (1f - (1f - k) * (1f - k)));
                    for (int i = 0; i < _spikes.Count; i++)
                    {
                        if (!_spikes[i]) continue;
                        float len = _scale * 1.6f * (1f - k);
                        _spikes[i].localPosition = _dirs[i] * (reach - len * 0.5f);
                        _spikes[i].localScale = new Vector3(0.06f * _scale * (1f - k) + 0.01f, 0.06f * _scale * (1f - k) + 0.01f, Mathf.Max(0.01f, len));
                    }
                    return;
                }
                case 3:
                {
                    float w = k < 0.15f ? Mathf.Lerp(0.3f, 1.3f, k / 0.15f) : Mathf.Lerp(1.3f, 0f, (k - 0.15f) / 0.85f);
                    if (_pillar) _pillar.localScale = new Vector3(_scale * w, _len * 0.5f, _scale * w);
                    if (_core) _core.localScale = new Vector3(_scale * 0.4f * w, _len * 0.5f, _scale * 0.4f * w);
                    if (_disc) _disc.localScale = Vector3.one * _scale * 2.5f * w;
                    return;
                }
                case 4:
                {
                    float open = k < 0.2f ? Mathf.SmoothStep(0f, 1f, k / 0.2f) : k > 0.8f ? 1f - (k - 0.8f) / 0.2f : 1f;
                    transform.localScale = Vector3.one * _scale * Mathf.Max(0.01f, open);
                    if (_ring) _ring.Rotate(Vector3.up, 260f * dt, Space.Self);
                    if (_ring2) _ring2.Rotate(Vector3.up, -180f * dt, Space.Self);
                    if (_disc) _disc.localScale = new Vector3(2.2f, 0.005f, 2.2f);
                    return;
                }
                case 5:
                {
                    float p = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                    if (_pillar) _pillar.localScale = new Vector3(1.2f * p, 4f, 1.2f * p);
                    if (_core) _core.localScale = new Vector3(0.5f * p, 4f, 0.5f * p);
                    if (_ring) { _ring.Rotate(Vector3.up, 300f * dt, Space.Self); _ring.localPosition = new Vector3(0f, k * 4f, 0f); _ring.localScale = Vector3.one * Mathf.Max(0.01f, 1f - k * 0.7f); }
                    return;
                }
            }

            float open0 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t / 0.35f));
            if (_ring) { _ring.localScale = Vector3.one * open0; _ring.Rotate(Vector3.up, 240f * dt, Space.Self); }
            if (_disc) _disc.localScale = new Vector3(2.2f * open0, 0.01f, 2.2f * open0);
            if (_pillar)
            {
                float p = k < 0.3f ? k / 0.3f : 1f - (k - 0.3f) / 0.7f;
                _pillar.localScale = new Vector3(0.6f * p, 2f + 2f * k, 0.6f * p);
                _pillar.localPosition = new Vector3(0f, 2f + 2f * k, 0f);
            }
            if (k > 0.75f) transform.localScale = Vector3.one * _scale * 2f * (1f - (k - 0.75f) / 0.25f);
        }
    }
}
