using System.Collections.Generic;
using EntityStates;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using RoR2.Orbs;
using UnityEngine;
using UnityEngine.Networking;

namespace Surveyor
{
    /// <summary>
    /// v0.7.1 minigun ammo (R swaps): LEAD fires fast and barely heats, INFERNO hits hard and burns but overheats fast,
    /// FROST stacks a slow that turns into ice and locks enemies in place, RAD is a sickness that jumps from enemy to enemy.
    /// The debuffs are real buffs (they replicate), so every player sees the ice and the sickness.
    /// </summary>
    public static class SvrAmmo
    {
        public const int Lead = 0, Inferno = 1, Frost = 2, Rad = 3;
        public static readonly string[] Names = { "LEAD", "INFERNO", "FROST", "RAD" };
        public static readonly string[] Descs =
        {
            "FAST FIRE · BARELY HEATS · SHOOT ALL DAY",
            "HUGE FIRE DAMAGE · BURNS · OVERHEATS FAST",
            "SLOWS MORE EVERY HIT · FREEZES THEM IN ICE",
            "SICKNESS THAT JUMPS FROM ENEMY TO ENEMY"
        };
        public static readonly Color[] Colors =
        {
            new Color(0.9f, 0.88f, 0.8f, 1f),
            new Color(1f, 0.45f, 0.1f, 1f),
            new Color(0.5f, 0.9f, 1f, 1f),
            new Color(0.55f, 1f, 0.25f, 1f)
        };
        public static readonly float[] RateMult = { 1.3f, 0.9f, 1f, 0.95f };
        public static readonly float[] DamageMult = { 1f, 1.9f, 0.85f, 0.8f };
        public static readonly float[] HeatPerSecond = { 0.05f, 0.38f, 0.1f, 0.12f };
        public static readonly float[] RangeMult = { 1.25f, 0.9f, 1f, 1f };

        public static int Current => SvrLidar.MinigunMode & 3;

        public static BuffDef FrostBuff, FrostLockBuff, RadBuff;
        private static GameObject _iceBurst;

        public const int FrostMaxStacks = 40, FrostLockStacks = 30, RadMaxStacks = 10;

        public static void Create()
        {
            FrostBuff = Buff("BCSurveyorFrost", new Color(0.55f, 0.9f, 1f), true);
            FrostLockBuff = Buff("BCSurveyorIceLock", new Color(0.8f, 0.97f, 1f), false);
            RadBuff = Buff("BCSurveyorRad", new Color(0.55f, 1f, 0.25f), true);
            RoR2Application.onLoad += () =>
            {
                SvrAmmoFx.Ensure();
                try
                {
                    if (RoR2Content.Buffs.Slow80) { FrostBuff.iconSprite = RoR2Content.Buffs.Slow80.iconSprite; FrostLockBuff.iconSprite = RoR2Content.Buffs.Slow80.iconSprite; }
                    if (RoR2Content.Buffs.Poisoned) RadBuff.iconSprite = RoR2Content.Buffs.Poisoned.iconSprite;
                }
                catch { }
                foreach (string p in new[] { "Prefabs/Effects/ImpactEffects/IceRingExplosion", "Prefabs/Effects/ImpactEffects/IceCullExplosion", "Prefabs/Effects/ImpactEffects/MageIceExplosion" })
                {
                    try { _iceBurst = LegacyResourcesAPI.Load<GameObject>(p); } catch { }
                    if (_iceBurst) break;
                }
            };
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            RoR2Application.onFixedUpdate += ServerTick;
            NetworkingAPI.RegisterMessageType<SvrAmmoHitMessage>();
        }

        private static BuffDef Buff(string name, Color c, bool stack)
        {
            BuffDef b = ScriptableObject.CreateInstance<BuffDef>();
            b.name = name;
            b.buffColor = c;
            b.canStack = stack;
            b.isDebuff = true;
            ContentAddition.AddBuffDef(b);
            return b;
        }

        private static void Stats(CharacterBody b, RecalculateStatsAPI.StatHookEventArgs a)
        {
            if (!b || FrostBuff == null) return;
            int f = b.GetBuffCount(FrostBuff);
            if (f > 0)
            {
                a.moveSpeedReductionMultAdd += f * 0.07f;          // more ice, slower
                a.attackSpeedMultAdd -= Mathf.Min(0.6f, f * 0.015f);
            }
            if (b.HasBuff(FrostLockBuff))
            {
                a.moveSpeedRootCount += 1;                          // locked in ice
                a.attackSpeedMultAdd -= 0.5f;
            }
        }

        // ---------------------------------------------------------------- on hit (the shooter tells the host)

        public static void SendHit(HealthComponent hc, GameObject attacker, float bulletDamage, int ammo)
        {
            if (!hc || ammo == Lead) return;
            if (NetworkServer.active) { OnHit(hc, attacker, bulletDamage, ammo); return; }
            try { new SvrAmmoHitMessage { Victim = hc.gameObject, Attacker = attacker, Damage = bulletDamage, Ammo = ammo }.Send(NetworkDestination.Server); } catch { }
        }

        public static void OnHit(HealthComponent hc, GameObject attacker, float bulletDamage, int ammo)
        {
            if (!NetworkServer.active || !hc || !hc.alive || !hc.body) return;
            CharacterBody v = hc.body;
            switch (ammo)
            {
                case Inferno:
                    InflictDotInfo burn = new InflictDotInfo
                    {
                        victimObject = hc.gameObject,
                        attackerObject = attacker,
                        totalDamage = bulletDamage * 1.2f,
                        dotIndex = DotController.DotIndex.Burn,
                        damageMultiplier = 1f
                    };
                    DotController.InflictDot(ref burn);
                    break;
                case Frost:
                    int stacks = v.GetBuffCount(FrostBuff);
                    if (stacks < FrostMaxStacks) v.AddTimedBuff(FrostBuff, 4f);
                    stacks++;
                    bool locked = v.HasBuff(FrostLockBuff);
                    if (!locked && (stacks >= FrostLockStacks || Random.value < stacks * 0.012f))
                        Lock(v, 1.6f + stacks * 0.05f);
                    break;
                case Rad:
                    Infect(hc, attacker, bulletDamage * 0.3f, 2, 0, 6f);
                    break;
            }
        }

        private static void Lock(CharacterBody v, float seconds)
        {
            v.AddTimedBuff(FrostLockBuff, seconds);
            try
            {
                SetStateOnHurt ss = v.GetComponent<SetStateOnHurt>();
                if (ss && ss.canBeFrozen) ss.SetFrozen(seconds);
            }
            catch { }
            if (_iceBurst)
            {
                try { EffectManager.SpawnEffect(_iceBurst, new EffectData { origin = v.corePosition, scale = Mathf.Max(1.5f, v.radius * 2f) }, true); } catch { }
            }
            Util.PlaySound("Play_mage_shift_wall_build", v.gameObject);
        }

        // ---------------------------------------------------------------- radiation (server)

        private class RadInfo
        {
            public HealthComponent Hc;
            public GameObject Attacker;
            public float TickDamage, Until, NextTick, NextJump;
            public int Gen;
        }

        private static readonly Dictionary<HealthComponent, RadInfo> _rad = new Dictionary<HealthComponent, RadInfo>();
        private static readonly List<HealthComponent> _dead = new List<HealthComponent>();

        private static void Infect(HealthComponent hc, GameObject attacker, float tick, int addStacks, int gen, float seconds)
        {
            if (!hc || !hc.alive || !hc.body) return;
            float now = Time.fixedTime;
            if (!_rad.TryGetValue(hc, out RadInfo r))
            {
                r = new RadInfo { Hc = hc, NextTick = now + 0.5f, NextJump = now + 0.9f, Gen = gen };
                _rad[hc] = r;
            }
            r.Attacker = attacker;
            r.TickDamage = Mathf.Max(r.TickDamage, tick);
            r.Until = Mathf.Max(r.Until, now + seconds);
            r.Gen = Mathf.Min(r.Gen, gen);
            for (int i = 0; i < addStacks && hc.body.GetBuffCount(RadBuff) < RadMaxStacks; i++)
                hc.body.AddTimedBuff(RadBuff, seconds);
        }

        private static void ServerTick()
        {
            if (!NetworkServer.active || _rad.Count == 0) return;
            float now = Time.fixedTime;
            _dead.Clear();
            var snapshot = new List<RadInfo>(_rad.Values);
            foreach (RadInfo r in snapshot)
            {
                HealthComponent hc = r.Hc;
                if (!hc || !hc.alive || !hc.body || now > r.Until) { _dead.Add(hc); continue; }
                int stacks = Mathf.Max(1, hc.body.GetBuffCount(RadBuff));
                if (now >= r.NextTick)
                {
                    r.NextTick = now + 0.5f;
                    try
                    {
                        DamageInfo di = new DamageInfo
                        {
                            attacker = r.Attacker,
                            inflictor = r.Attacker,
                            damage = r.TickDamage * stacks,
                            position = hc.body.corePosition,
                            crit = false,
                            procCoefficient = 0f,
                            damageColorIndex = DamageColorIndex.Poison,
                            damageType = DamageType.DoT
                        };
                        hc.TakeDamage(di);
                    }
                    catch { }
                }
                // the sickness jumps: to the nearest healthy enemy close by (a few generations deep)
                if (now >= r.NextJump && r.Gen < 4)
                {
                    r.NextJump = now + 1.1f;
                    HealthComponent next = Nearest(hc, 16f);
                    if (next)
                    {
                        try
                        {
                            LightningOrb orb = new LightningOrb
                            {
                                origin = hc.body.corePosition,
                                damageValue = r.TickDamage,
                                isCrit = false,
                                bouncesRemaining = 0,
                                teamIndex = r.Attacker ? TeamComponent.GetObjectTeam(r.Attacker) : TeamIndex.Player,
                                attacker = r.Attacker,
                                procCoefficient = 0f,
                                lightningType = LightningOrb.LightningType.CrocoDisease,
                                target = next.body.mainHurtBox
                            };
                            if (orb.target) OrbManager.instance.AddOrb(orb);
                        }
                        catch { }
                        Infect(next, r.Attacker, r.TickDamage, Mathf.Max(1, stacks / 2), r.Gen + 1, 5f);
                    }
                }
            }
            foreach (HealthComponent d in _dead) _rad.Remove(d);
        }

        private static HealthComponent Nearest(HealthComponent from, float range)
        {
            HealthComponent best = null;
            float bestD = range * range;
            TeamIndex team = from.body.teamComponent ? from.body.teamComponent.teamIndex : TeamIndex.Monster;
            Vector3 p = from.body.corePosition;
            var all = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterBody b = all[i];
                if (!b || !b.healthComponent || b.healthComponent == from || !b.healthComponent.alive || !b.teamComponent || b.teamComponent.teamIndex != team) continue;
                if (_rad.ContainsKey(b.healthComponent)) continue;
                float d = (b.corePosition - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b.healthComponent; }
            }
            return best;
        }
    }

    /// <summary>Client -> host: one of my rounds hit this enemy with this ammo.</summary>
    public class SvrAmmoHitMessage : INetMessage
    {
        public GameObject Victim, Attacker;
        public float Damage;
        public int Ammo;
        public void Serialize(NetworkWriter w) { w.Write(Victim); w.Write(Attacker); w.Write(Damage); w.Write((byte)Ammo); }
        public void Deserialize(NetworkReader r) { Victim = r.ReadGameObject(); Attacker = r.ReadGameObject(); Damage = r.ReadSingle(); Ammo = r.ReadByte(); }
        public void OnReceived()
        {
            if (!NetworkServer.active || !Victim) return;
            HealthComponent hc = Victim.GetComponent<HealthComponent>();
            if (hc) SvrAmmo.OnHit(hc, Attacker, Mathf.Clamp(Damage, 0f, 1e6f), Ammo & 3);
        }
    }

    /// <summary>R: swap the minigun ammo (LEAD -> INFERNO -> FROST -> RAD).</summary>
    public class SvrAmmoSwapState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_MULT_R_variant_activate", gameObject);
            Util.PlaySound("Play_bandit2_R_load", gameObject);
            if (isAuthority)
            {
                SvrLidar.MinigunMode = (SvrLidar.MinigunMode + 1) & 3;
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.FlashAmmo();
                SvrSpool sp = GetComponent<SvrSpool>();
                if (sp) sp.KickPulse += 0.6f;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.2f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Any; }
    }

    /// <summary>
    /// Every client: ice crystals grow around enemies as Frost stacks up (they close into a shell when locked), and
    /// Rad-sick enemies glow toxic green.
    /// </summary>
    public class SvrAmmoFx : MonoBehaviour
    {
        private static SvrAmmoFx _inst;
        private class Cluster { public GameObject Root; public Transform[] Shards; public Vector3[] Dirs; public float[] Size; public Light Glow; }
        private readonly Dictionary<CharacterBody, Cluster> _ice = new Dictionary<CharacterBody, Cluster>();
        private readonly Dictionary<CharacterBody, Light> _sick = new Dictionary<CharacterBody, Light>();
        private readonly List<CharacterBody> _gone = new List<CharacterBody>();
        private static Mesh _shard;
        private static Material _iceMat;
        private float _k;

        public static void Ensure()
        {
            if (_inst) return;
            GameObject g = new GameObject("SvrAmmoFx");
            DontDestroyOnLoad(g);
            _inst = g.AddComponent<SvrAmmoFx>();
        }

        private void Update()
        {
            if (SvrAmmo.FrostBuff == null) return;
            float dt = Time.deltaTime;
            _k += dt;
            var all = CharacterBody.readOnlyInstancesList;
            for (int i = 0; i < all.Count; i++)
            {
                CharacterBody b = all[i];
                if (!b) continue;
                int f = b.GetBuffCount(SvrAmmo.FrostBuff);
                bool locked = b.HasBuff(SvrAmmo.FrostLockBuff);
                if ((f > 0 || locked) && !_ice.ContainsKey(b)) _ice[b] = MakeIce(b);
                if (b.HasBuff(SvrAmmo.RadBuff) && !_sick.ContainsKey(b) && _sick.Count < 14) _sick[b] = MakeSick(b);
            }

            _gone.Clear();
            foreach (var kv in _ice)
            {
                CharacterBody b = kv.Key;
                Cluster c = kv.Value;
                int f = b ? b.GetBuffCount(SvrAmmo.FrostBuff) : 0;
                bool locked = b && b.HasBuff(SvrAmmo.FrostLockBuff);
                bool alive = b && b.healthComponent && b.healthComponent.alive;
                if (!alive || (f == 0 && !locked)) { _gone.Add(b); if (c.Root) Destroy(c.Root); continue; }
                float k = locked ? 1f : Mathf.Clamp01(f / (float)SvrAmmo.FrostLockStacks);
                float r = Mathf.Max(0.6f, b.radius);
                c.Root.transform.position = b.corePosition;
                for (int s = 0; s < c.Shards.Length; s++)
                {
                    Transform t = c.Shards[s];
                    bool show = locked || s < Mathf.CeilToInt(c.Shards.Length * k);
                    t.gameObject.SetActive(show);
                    if (!show) continue;
                    Vector3 d = c.Dirs[s];
                    float len = r * c.Size[s] * (locked ? 1.5f : (0.5f + 0.9f * k));
                    t.position = b.corePosition + d * r * (locked ? 0.35f : 0.75f);
                    t.rotation = Quaternion.LookRotation(d, Vector3.up);
                    t.localScale = new Vector3(len * 0.32f, len * 0.32f, len);
                }
                if (c.Glow) c.Glow.intensity = 0.6f + 1.4f * k;
            }
            foreach (CharacterBody b in _gone) _ice.Remove(b);

            _gone.Clear();
            foreach (var kv in _sick)
            {
                CharacterBody b = kv.Key;
                bool on = b && b.healthComponent && b.healthComponent.alive && b.HasBuff(SvrAmmo.RadBuff);
                if (!on) { _gone.Add(b); if (kv.Value) Destroy(kv.Value.gameObject); continue; }
                int n = b.GetBuffCount(SvrAmmo.RadBuff);
                kv.Value.transform.position = b.corePosition;
                kv.Value.intensity = (1f + n * 0.25f) * (0.7f + 0.3f * Mathf.Sin(_k * 9f + b.GetInstanceID()));
            }
            foreach (CharacterBody b in _gone) _sick.Remove(b);
        }

        private Cluster MakeIce(CharacterBody b)
        {
            if (!_shard) _shard = MakeShard();
            if (!_iceMat)
            {
                Shader hg = Shader.Find("Hopoo Games/Deferred/Standard");
                if (hg)
                {
                    _iceMat = new Material(hg) { name = "SvrIce" };
                    _iceMat.SetColor("_Color", new Color(0.72f, 0.92f, 1f));
                    if (_iceMat.HasProperty("_EmColor")) _iceMat.SetColor("_EmColor", new Color(0.35f, 0.7f, 1f));
                    if (_iceMat.HasProperty("_EmTex")) _iceMat.SetTexture("_EmTex", Texture2D.whiteTexture);
                    if (_iceMat.HasProperty("_EmPower")) _iceMat.SetFloat("_EmPower", 1.2f);
                    if (_iceMat.HasProperty("_SpecularStrength")) _iceMat.SetFloat("_SpecularStrength", 1f);
                }
            }
            Cluster c = new Cluster { Root = new GameObject("SvrIceCluster") };
            int n = 14;
            c.Shards = new Transform[n];
            c.Dirs = new Vector3[n];
            c.Size = new float[n];
            for (int i = 0; i < n; i++)
            {
                GameObject g = new GameObject("SvrIceShard");
                g.transform.SetParent(c.Root.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = _shard;
                MeshRenderer mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _iceMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 d = Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 0.8f + (i < 4 ? -0.4f : 0.1f);
                c.Dirs[i] = d.normalized;
                c.Size[i] = Random.Range(0.6f, 1.25f);
                c.Shards[i] = g.transform;
                g.SetActive(false);
            }
            GameObject lg = new GameObject("SvrIceGlow");
            lg.transform.SetParent(c.Root.transform, false);
            c.Glow = lg.AddComponent<Light>();
            c.Glow.type = LightType.Point;
            c.Glow.color = new Color(0.5f, 0.85f, 1f);
            c.Glow.range = Mathf.Max(4f, b.radius * 4f);
            return c;
        }

        private static Light MakeSick(CharacterBody b)
        {
            GameObject g = new GameObject("SvrRadGlow");
            Light l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.45f, 1f, 0.2f);
            l.range = Mathf.Max(4f, b.radius * 3.5f);
            return l;
        }

        /// <summary>An ice shard: a long hexagonal spike, base at z = 0, tip at z = 1.</summary>
        private static Mesh MakeShard()
        {
            const int n = 6;
            Vector3[] v = new Vector3[n * 2 + 2];
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                v[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.5f;
                v[n + i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.42f + Vector3.forward * 0.62f;
            }
            v[2 * n] = Vector3.forward;
            v[2 * n + 1] = Vector3.forward * -0.1f;
            List<int> t = new List<int>();
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t.AddRange(new[] { i, n + i, n + j, i, n + j, j });          // sides
                t.AddRange(new[] { n + i, 2 * n, n + j });                   // tip
                t.AddRange(new[] { j, 2 * n + 1, i });                       // base
            }
            Mesh m = new Mesh { name = "SvrIceShard", vertices = v, triangles = t.ToArray() };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
