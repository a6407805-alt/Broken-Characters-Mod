using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Orbs;
using RoR2.Projectile;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;

namespace Surveyor
{
    // ================================================================ LiDAR-Exclusive Hidden Map Upgrades
    // Hidden in service panels, crates, and drop pods across maps — only discoverable & interactable via the Surveyor's LiDAR scan.

    public static class SvrUpgrades
    {
        // 6 Lockable Field Modules (scattered across maps in hidden LiDAR caches) + 2 Rig Enhancements
        public const int MINI_CRYO = 0;        // Minigun Mode 1: Cryo-Quench Ammo (chills targets & cools barrel heat)
        public const int MINI_INCENDIARY = 1;  // Minigun Mode 2: Incendiary Thermite Ammo (ignites with burning DoT)
        public const int MINI_PLASMA = 2;      // Minigun Mode 3: Overclocked Plasma-Arc (chain lightning shocks)
        public const int VAC_VORTEX = 3;       // Vacuum Mode 1: Singularity Vortex (pulls swarms & pests to nozzle)
        public const int VAC_CRYO = 4;         // Vacuum Mode 2: Cryo-Exhaust Mist (freezes cone & leeches barrier)
        public const int VAC_REPULSE = 5;      // Vacuum Mode 3: Reverse Repulsor (deflects projs & blasts foes back)
        public const int GYRO_HARNESS = 6;     // Exo-Rig: Gyroscopic Shoulder Harness (no move slow while firing)
        public const int AETHER_LEECH = 7;     // Field Rig: Aether-Leech Field (siphons energy into EVA suit barrier)
        public const int COUNT = 8;

        // Backward compatibility aliases
        public const int CRYO_JACKET = MINI_CRYO;
        public const int DU_SLUG = MINI_INCENDIARY;
        public const int PLASMA_ACCEL = MINI_PLASMA;
        public const int SINGULARITY_CORE = VAC_VORTEX;
        public const int REPULSOR_BLOW = VAC_REPULSE;
        public const int COMPRESSION_PRESS = AETHER_LEECH;

        public static readonly string[] Names =
        {
            "CRYO-QUENCH COOLING MODULE",
            "INCENDIARY THERMITE MODULE",
            "OVERCLOCKED PLASMA MODULE",
            "SINGULARITY VORTEX MODULE",
            "CRYO-EXHAUST FREEZE MODULE",
            "REVERSE REPULSOR MODULE",
            "GYROSCOPIC SHOULDER RIG",
            "AETHER-LEECH ENERGY SIPHON"
        };

        public static readonly string[] ShortNames =
        {
            "CRYO AMMO",
            "INCENDIARY",
            "PLASMA-ARC",
            "VORTEX VAC",
            "CRYO EXHAUST",
            "REPULSOR",
            "GYRO RIG",
            "AETHER SIPHON"
        };

        public static readonly string[] Descs =
        {
            "Unlocks Minigun Mode [CRYO] · Chills targets & cools barrel heat",
            "Unlocks Minigun Mode [INCENDIARY] · High-temp ignite & burning damage",
            "Unlocks Minigun Mode [PLASMA] · High-voltage chain lightning arcs",
            "Unlocks Vacuum Mode [VORTEX] · Singularity vortex reels swarms into nozzle",
            "Unlocks Vacuum Mode [CRYO] · Sub-zero exhaust flash-freezes enemies & feeds barrier",
            "Unlocks Vacuum Mode [REPULSOR] · Reverse blast wave deflects projectiles & knocks back swarms",
            "Zero movement slow penalty while firing & -50% recoil climb",
            "Firing & vacuuming siphons barrier directly into EVA suit"
        };

        private static readonly bool[] _unlocked = new bool[COUNT];
        public static int UnlockedCount { get; private set; }

        public static bool Has(int id) => id >= 0 && id < COUNT && _unlocked[id];

        public static bool IsMinigunUnlocked(int mode)
        {
            mode = (mode % 4 + 4) % 4;
            if (mode == 0) return true; // IRIDIUM AP is default starting weapon mode
            if (mode == 1) return Has(MINI_CRYO);
            if (mode == 2) return Has(MINI_INCENDIARY);
            if (mode == 3) return Has(MINI_PLASMA);
            return false;
        }

        public static bool IsVacuumUnlocked(int mode)
        {
            mode = (mode % 4 + 4) % 4;
            if (mode == 0) return true; // POLTERGUST / SUCTION is default starting vacuum mode
            if (mode == 1) return Has(VAC_VORTEX);
            if (mode == 2) return Has(VAC_CRYO);
            if (mode == 3) return Has(VAC_REPULSE);
            return false;
        }

        public static void Unlock(int id)
        {
            if (id < 0 || id >= COUNT || _unlocked[id]) return;
            _unlocked[id] = true;
            UnlockedCount++;
            SvrSenses.Version++;
        }

        public static void ResetRun()
        {
            for (int i = 0; i < COUNT; i++) _unlocked[i] = false;
            UnlockedCount = 0;
            SvrSenses.Version++;
        }
    }

    // ================================================================ spool & industrial heat gauge (state shared between presses of M1)

    /// <summary>How spun up the barrels are (0 to 1), industrial barrel heat (0 to 1), and how long the anchor still holds. Lives on the body.</summary>
    public class SvrSpool : MonoBehaviour
    {
        public float Spool;
        public float Heat;
        public bool Overheated;
        public float AnchorLeft;
        /// <summary>Recoil the model shows (SvrHeavy consumes it).</summary>
        public float KickPulse;
        private float _idle;
        private bool _slowed;
        private int _senseVersion = -1;
        private CharacterBody _body;

        private void Awake() { _body = GetComponent<CharacterBody>(); }

        /// <summary>The minigun calls this every tick it fires, so the barrels do not wind down; heat depends on the ammo.</summary>
        public void Held()
        {
            _idle = 0f;
            float heatRate = SvrAmmo.HeatPerSecond[SvrAmmo.Current];
            if (AnchorLeft > 0f) heatRate *= 0.65f;
            Heat = Mathf.Clamp01(Heat + Time.fixedDeltaTime * heatRate);
            if (Heat >= 0.999f && !Overheated)
            {
                Overheated = true;
                Util.PlaySound("Play_item_proc_igniteOnKill", gameObject);
            }
        }

        /// <summary>Barrels still turning but not firing (overheated, venting).</summary>
        public void Spinning() { _idle = 0f; }

        private void FixedUpdate()
        {
            if (!_body || !_body.hasEffectiveAuthority) return;
            float dt = Time.fixedDeltaTime;
            _idle += dt;
            if (AnchorLeft > 0f) AnchorLeft = Mathf.Max(0f, AnchorLeft - dt);
            if (_idle > 0.15f && Spool > 0f)
                Spool = Mathf.MoveTowards(Spool, 0f, dt / (SvrPlugin.SpoolSeconds.Value * 1.4f));
            // cools whenever it isn't firing; venting after an overheat is faster, and it fires again below 30%
            if (_idle > 0.15f || Overheated)
                Heat = Mathf.MoveTowards(Heat, 0f, dt * (Overheated ? 0.5f : 0.35f));
            if (Overheated && Heat < 0.3f) Overheated = false;

            bool slowed = Spool > 0.3f && AnchorLeft <= 0f && !SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS);
            if (slowed != _slowed || _senseVersion != SvrSenses.Version)
            {
                _slowed = slowed;
                _senseVersion = SvrSenses.Version;
                _body.RecalculateStats();
            }
        }
    }

    // ================================================================ M1: Rotary Survey Gun

    public class SvrMinigunState : BaseSkillState
    {
        private SvrSpool _spool;
        private SvrLidar _lidar;
        private float _timer;
        private int _shotCounter;
        private GameObject _tracer, _hit;

        public override void OnEnter()
        {
            base.OnEnter();
            _spool = GetComponent<SvrSpool>();
            _lidar = GetComponent<SvrLidar>();
            try { _tracer = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoDefault"); } catch { _tracer = null; }
            try { _hit = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/HitsparkCommando"); } catch { _hit = null; }
            LoadModeFx();
        }

        // Per-ammo tracers and impacts (first prefab that exists wins; Commando's are the fallback)
        private static readonly GameObject[] ModeTracer = new GameObject[4], ModeHit = new GameObject[4];
        private static bool _modeFxLoaded;

        private static GameObject LoadFirst(params string[] paths)
        {
            foreach (string p in paths)
            {
                try { GameObject g = LegacyResourcesAPI.Load<GameObject>(p); if (g) return g; } catch { }
            }
            return null;
        }

        private static void LoadModeFx()
        {
            if (_modeFxLoaded) return;
            _modeFxLoaded = true;
            ModeTracer[0] = LoadFirst("Prefabs/Effects/Tracers/TracerBandit2Rifle", "Prefabs/Effects/Tracers/TracerCommandoShotgun");
            ModeHit[0] = LoadFirst("Prefabs/Effects/ImpactEffects/HitsparkBandit2", "Prefabs/Effects/ImpactEffects/HitsparkBandit", "Prefabs/Effects/ImpactEffects/HitsparkCommandoShotgun");
            ModeTracer[1] = LoadFirst("Prefabs/Effects/Tracers/TracerMageFire", "Prefabs/Effects/Tracers/TracerClayBruiserMinigun");
            ModeHit[1] = LoadFirst("Prefabs/Effects/ImpactEffects/IgniteExplosionVFX", "Prefabs/Effects/ImpactEffects/MissileExplosionVFX");
            ModeTracer[2] = LoadFirst("Prefabs/Effects/Tracers/TracerMageIceLarge", "Prefabs/Effects/Tracers/TracerMageIceLaser", "Prefabs/Effects/Tracers/TracerHuntressSnipe");
            ModeHit[2] = LoadFirst("Prefabs/Effects/ImpactEffects/IceRingImpact", "Prefabs/Effects/ImpactEffects/MageIceExplosion", "Prefabs/Effects/ImpactEffects/IceCullExplosion");
            ModeTracer[3] = LoadFirst("Prefabs/Effects/Tracers/TracerCrocoSpit", "Prefabs/Effects/Tracers/TracerVoidSurvivorBeam", "Prefabs/Effects/Tracers/TracerMageLightning");
            ModeHit[3] = LoadFirst("Prefabs/Effects/ImpactEffects/CrocoDiseaseImpactEffect", "Prefabs/Effects/ImpactEffects/PoisonNovaProc", "Prefabs/Effects/ImpactEffects/ImpactMageLightning");
            int found = 0;
            for (int i = 0; i < 4; i++) { if (ModeTracer[i]) found++; if (ModeHit[i]) found++; }
            SvrPlugin.Log.LogInfo("Minigun ammo effects: " + found + "/8 prefabs found (missing ones use Commando's).");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            if (!_spool || !inputBank || !inputBank.skill1.down) { outer.SetNextStateToMain(); return; }

            _spool.Spinning();
            bool anchored = _spool.AnchorLeft > 0f;
            float spoolTime = SvrPlugin.SpoolSeconds.Value / Mathf.Clamp(attackSpeedStat, 0.5f, 3f) * SvrSenses.SpoolMult;
            if (anchored) spoolTime *= 0.33f;
            _spool.Spool = Mathf.MoveTowards(_spool.Spool, 1f, Time.fixedDeltaTime / spoolTime);

            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            if (_spool.Spool < 0.2f) return;
            if (_spool.Overheated) return;      // venting: barrels spin, no rounds
            _spool.Held();                       // heat builds every tick it fires (how fast depends on the ammo)

            _timer -= Time.fixedDeltaTime;
            if (_timer > 0f) return;
            int ammo = SvrAmmo.Current;
            float rate = Mathf.Lerp(SvrPlugin.MinRate.Value, SvrPlugin.MaxRate.Value, Mathf.InverseLerp(0.2f, 1f, _spool.Spool)) * Mathf.Clamp(attackSpeedStat, 1f, 2f) * SvrAmmo.RateMult[ammo];
            _timer += 1f / Mathf.Max(0.5f, rate);
            if (_timer < 0f) _timer = 0f;
            Shoot(aim, anchored, ammo);
        }

        public override void OnExit()
        {
            Util.PlaySound("Stop_clayBruiser_attack1_shootLoop", gameObject);
            base.OnExit();
        }

        // what other players hear: the gun firing at a steady clip while the state runs on their side
        private float _remoteTimer;

        public override void Update()
        {
            base.Update();
            if (isAuthority || fixedAge < 0.35f) return;
            _remoteTimer -= Time.deltaTime;
            if (_remoteTimer > 0f) return;
            _remoteTimer = 1f / 11f;
            Util.PlaySound("Play_clayBruiser_attack1_shoot_bullet", gameObject);
        }

        // each ammo has its own voice on top of the heavy base rattle
        // sound design: a heavy minigun body (Clay Templar's rotary cannon: wind up, firing loop, a round per shot, wind down)
        // with a voice per ammo on top. Every one of these lives in the game's Global sound bank, so it always plays.
        private static readonly string[][] AmmoSounds =
        {
            new[] { "Play_commando_M1", null, null },                                                              // LEAD: dry, fast rattle
            new[] { "Play_lemurian_fireball_shoot", "Play_fireballsOnHit_shoot", null }, // INFERNO: roaring fire
            new[] { "Play_lunar_wisp_attack1_shoot_bullet", "Play_item_proc_iceRingSpear", null },                   // FROST: crystal crack
            new[] { "Play_voidBarnacle_m1_shoot", "Play_affix_void_bug_infect", null }                              // RAD: wet, sick hiss
        };
        private bool _loopOn;

        private void Shoot(Ray aim, bool anchored, int ammo)
        {
            _shotCounter++;
            bool hasGyro = SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS);

            float spread = Mathf.Lerp(SvrPlugin.SpreadMax.Value, SvrPlugin.SpreadMin.Value, _spool.Spool) * SvrSenses.SpreadMult * (anchored ? 0.5f : (hasGyro ? 0.72f : 1f));
            float dmg = damageStat * SvrPlugin.BulletDamage.Value * (anchored ? 1f + SvrPlugin.AnchorDamageBonus.Value : 1f) * SvrAmmo.DamageMult[ammo];
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");

            // sound: the base rattle every round, the ammo's own layer every 2nd / 3rd round
            Util.PlaySound("Play_clayBruiser_attack1_shoot_bullet", gameObject);
            string[] snd = AmmoSounds[ammo];
            bool heavy = ammo != SvrAmmo.Lead;          // heavy rounds are 4x slower, so every one gets its full voice
            if ((heavy || _shotCounter % 3 == 0) && snd[0] != null) Util.PlaySound(snd[0], gameObject);
            if (heavy && _shotCounter % 2 == 0 && snd[1] != null) Util.PlaySound(snd[1], gameObject);

            characterBody.AddSpreadBloom(hasGyro ? 0.05f : 0.09f);
            float rk = (anchored ? 0.45f : 1f) * (hasGyro ? 0.5f : 1f) * (ammo == SvrAmmo.Inferno ? 1.4f : 1f);
            AddRecoil(-0.35f * rk, -0.75f * rk, -0.25f * rk, 0.25f * rk);
            _spool.KickPulse += ammo == SvrAmmo.Inferno ? 0.4f : 0.22f;

            DamageTypeCombo dt = DamageType.Generic;
            if (ammo == SvrAmmo.Inferno) dt = DamageType.IgniteOnHit;
            if (ammo == SvrAmmo.Frost) dt = DamageType.SlowOnHit;
            float range = SvrPlugin.MinigunRange.Value * SvrAmmo.RangeMult[ammo];
            bool crit = RollCrit();
            GameObject me = gameObject;

            BulletAttack ba = new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = spread,
                damage = dmg,
                force = ammo == SvrAmmo.Inferno ? 260f : 120f,
                tracerEffectPrefab = ModeTracer[ammo] ? ModeTracer[ammo] : _tracer,
                muzzleName = "MuzzleRight",
                // the big ammo impacts every other round (13 rounds a second of explosions would bury the screen)
                hitEffectPrefab = (ModeHit[ammo] && _shotCounter % 2 == 0) ? ModeHit[ammo] : _hit,
                isCrit = crit,
                radius = 0.3f,
                smartCollision = true,
                maxDistance = range,
                procCoefficient = ammo == SvrAmmo.Lead ? 0.8f : 0.7f,
                damageType = dt
            };
            if (ammo != SvrAmmo.Lead)
            {
                ba.hitCallback = (BulletAttack b, ref BulletAttack.BulletHit hit) =>
                {
                    bool r = BulletAttack.defaultHitCallback(b, ref hit);
                    try
                    {
                        HurtBox hb = hit.hitHurtBox;
                        if (hb && hb.healthComponent && hb.healthComponent.body != characterBody)
                            SvrAmmo.SendHit(hb.healthComponent, me, dmg, ammo);
                    }
                    catch { }
                    return r;
                };
            }
            ba.Fire();

            if (_lidar) _lidar.Shot(aim, _spool.Spool, anchored);
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Any; }
    }

    // ================================================================ M2: Industrial Poltergust Vacuum Cleaner

    public class SvrVacuumState : BaseSkillState
    {
        private SvrLidar _lidar;
        private float _soundTimer;
        private bool _firedOrb;

        public override void OnEnter()
        {
            base.OnEnter();
            _lidar = GetComponent<SvrLidar>();
            // BLACK HOLE mode: pressing M2 fires a singularity orb (on its own cooldown) instead of channelling
            if (isAuthority && _lidar && SvrLidar.VacuumMode % 4 == 1 && _lidar.TryFireSingularity(GetAimRay(), characterBody))
            {
                _firedOrb = true;
                PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
                AddRecoil(-1.5f, -2.5f, -0.5f, 0.5f);
                return;
            }
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_UI_menuClick", gameObject);         // one soft servo hum to start, then throttled (below)
            _soundTimer = 1.6f;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            Ray aim = GetAimRay();
            StartAimMode(aim, 1.5f, false);

            if (_firedOrb)
            {
                if (isAuthority && fixedAge >= 0.35f && (!inputBank || !inputBank.skill2.down)) outer.SetNextStateToMain();
                return;
            }

            if (_lidar)
            {
                _lidar.VacuumHold(aim, Time.fixedDeltaTime, characterBody);
            }

            _soundTimer -= Time.fixedDeltaTime;
            if (_soundTimer <= 0f)
            {
                _soundTimer = 1.6f;
                Util.PlaySound("Play_UI_menuClick", gameObject);
            }

            if (isAuthority)
            {
                if (!inputBank || !inputBank.skill2.down)
                {
                    outer.SetNextStateToMain();
                }
            }
        }

        public override void OnExit()
        {
            if (_lidar != null && _lidar.Demon != null)
            {
                _lidar.Demon.TetherActive = false;
            }
            base.OnExit();
        }

        // PrioritySkill, not Skill: with Skill, holding M2 let a fresh copy of this state interrupt itself every frame
        // (OnEnter/OnExit 60 times a second, each playing a sound) = the screech.
        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    // ================================================================ Alt M2: Surveyor Lance

    public class SvrLanceState : BaseSkillState
    {
        private float _charge;
        private bool _fired;
        private SvrSpool _spool;

        public override void OnEnter()
        {
            base.OnEnter();
            _spool = GetComponent<SvrSpool>();
            _charge = SvrPlugin.LanceCharge.Value / Mathf.Clamp(attackSpeedStat, 0.5f, 3f);
            Util.PlaySound("Play_drone_repair", gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!_fired && fixedAge >= _charge)
            {
                _fired = true;
                if (isAuthority) Fire();
            }
            if (fixedAge >= _charge + 0.35f && isAuthority) outer.SetNextStateToMain();
        }

        private void Fire()
        {
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            bool anchored = _spool && _spool.AnchorLeft > 0f;
            float dmg = damageStat * SvrPlugin.LanceDamage.Value * (anchored ? 1.25f : 1f);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_huntress_m1_shoot", gameObject);

            GameObject tracer = null, hit = null;
            try { tracer = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerCommandoDefault"); } catch { }
            try { hit = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/HitsparkCommando"); } catch { }

            new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = 0f,
                damage = dmg,
                force = SvrPlugin.LanceForce.Value,
                tracerEffectPrefab = tracer,
                muzzleName = "MuzzleRight",
                hitEffectPrefab = hit,
                isCrit = RollCrit(),
                radius = 0.9f,
                smartCollision = true,
                maxDistance = 150f,
                procCoefficient = 1f,
                stopperMask = LayerIndex.world.mask
            }.Fire();

            SvrLidar lidar = GetComponent<SvrLidar>();
            if (lidar) lidar.LanceShot(aim);
            characterBody.AddSpreadBloom(1f);
            if (!anchored && characterMotor) characterMotor.ApplyForce(-aim.direction * 700f, false, false);
            if (_spool) _spool.Spool = Mathf.Max(_spool.Spool, 0.4f);
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    // ================================================================ Shift: Hydraulic Slide, then hold to Anchor

    public class SvrSlideState : BaseSkillState
    {
        private Vector3 _dir;
        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = SvrPlugin.SlideSeconds.Value;
            Vector3 move = inputBank ? inputBank.moveVector : Vector3.zero;
            Vector3 flat = GetAimRay().direction;
            flat.y = 0f;
            if (move.sqrMagnitude > 0.01f) _dir = move.normalized;
            else _dir = flat.sqrMagnitude < 0.01f ? transform.forward : flat.normalized;
            Util.PlaySound("Play_env_geyser_launch", gameObject);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (characterMotor && isAuthority)
            {
                float t = Mathf.Clamp01(fixedAge / _duration);
                float speed = moveSpeedStat * Mathf.Lerp(SvrPlugin.SlideSpeed.Value, 1f, t);
                Vector3 v = characterMotor.velocity;
                characterMotor.velocity = new Vector3(_dir.x * speed, v.y, _dir.z * speed);
            }
            if (fixedAge >= _duration && isAuthority)
            {
                if (inputBank && inputBank.skill3.down)
                {
                    outer.SetNextState(new SvrAnchorState());
                }
                else
                {
                    if (characterMotor)
                    {
                        Vector3 v = characterMotor.velocity;
                        characterMotor.velocity = new Vector3(_dir.x * moveSpeedStat, v.y, _dir.z * moveSpeedStat);
                    }
                    outer.SetNextStateToMain();
                }
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    public class SvrAnchorState : BaseSkillState
    {
        private SvrSpool _spool;

        public override void OnEnter()
        {
            base.OnEnter();
            _spool = GetComponent<SvrSpool>();
            Util.PlaySound("Play_drone_repair", gameObject);
            if (_spool) _spool.AnchorLeft = SvrPlugin.AnchorSeconds.Value;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            float left = Mathf.Max(0f, SvrPlugin.AnchorSeconds.Value - fixedAge);
            if (_spool) _spool.AnchorLeft = left;
            if (characterMotor && isAuthority)
            {
                Vector3 v = characterMotor.velocity;
                characterMotor.velocity = new Vector3(0f, v.y, 0f);
            }
            if (isAuthority && (left <= 0f || (fixedAge > 0.3f && inputBank && !inputBank.skill3.down))) outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            if (_spool) _spool.AnchorLeft = 0f;
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    // ================================================================ R: Tactical Weapon Switch (Rotary Minigun <-> Piercing Rail Lance)

    public class SvrSwitchWeaponState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority && skillLocator && skillLocator.primary != null && SvrPlugin.MinigunSkillDef != null)
            {
                skillLocator.primary.SetSkillOverride(gameObject, SvrPlugin.MinigunSkillDef, GenericSkill.SkillOverridePriority.Upgrade);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    // ================================================================ Utility alt: Peek (visor off)

    public class SvrPeekState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            SvrLidar lidar = GetComponent<SvrLidar>();
            if (lidar)
            {
                if (lidar.Peeking)
                {
                    lidar.EndPeek();
                }
                else
                {
                    lidar.StartPeek();
                    Util.PlaySound("Play_UI_menuClick", gameObject);
                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.15f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Skill; }
    }

    // ================================================================ Special: LiDAR Overdrive Pulse (360-degree full stage reveal)

    public class SvrOverdriveState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority)
            {
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.TriggerOverdrivePulse();
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.25f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Skill; }
    }
}
