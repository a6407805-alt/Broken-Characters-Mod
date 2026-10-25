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

        /// <summary>The minigun calls this every tick it is held, so the barrels do not wind down.</summary>
        public void Held()
        {
            _idle = 0f;
            bool cryoCool = (SvrLidar.MinigunMode == 1) || SvrUpgrades.Has(SvrUpgrades.MINI_CRYO);
            float heatRate = cryoCool ? 0.035f : 0.11f;
            if (AnchorLeft > 0f) heatRate *= 0.65f;
            Heat = Mathf.Clamp01(Heat + Time.fixedDeltaTime * heatRate);
            if (Heat >= 0.99f && !cryoCool)
                Overheated = true;
        }

        private void FixedUpdate()
        {
            if (!_body || !_body.hasEffectiveAuthority) return;
            float dt = Time.fixedDeltaTime;
            _idle += dt;
            if (AnchorLeft > 0f) AnchorLeft = Mathf.Max(0f, AnchorLeft - dt);
            if (_idle > 0.15f)
            {
                bool cryoCool = (SvrLidar.MinigunMode == 1) || SvrUpgrades.Has(SvrUpgrades.MINI_CRYO);
                float windDown = cryoCool ? 2.4f : 1.4f;
                if (Spool > 0f)
                    Spool = Mathf.MoveTowards(Spool, 0f, dt / (SvrPlugin.SpoolSeconds.Value * windDown));
                float coolRate = cryoCool ? 0.55f : 0.28f;
                Heat = Mathf.MoveTowards(Heat, 0f, dt * coolRate);
                if (Overheated && Heat < 0.35f) Overheated = false;
            }

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
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            if (!_spool || !inputBank || !inputBank.skill1.down) { outer.SetNextStateToMain(); return; }

            _spool.Held();
            bool anchored = _spool.AnchorLeft > 0f;
            float spoolTime = SvrPlugin.SpoolSeconds.Value / Mathf.Clamp(attackSpeedStat, 0.5f, 3f) * SvrSenses.SpoolMult;
            if (anchored) spoolTime *= 0.33f;
            _spool.Spool = Mathf.MoveTowards(_spool.Spool, 1f, Time.fixedDeltaTime / spoolTime);

            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            if (_spool.Spool < 0.2f) return;

            _timer -= Time.fixedDeltaTime;
            if (_timer > 0f) return;
            float rate = Mathf.Lerp(SvrPlugin.MinRate.Value, SvrPlugin.MaxRate.Value, Mathf.InverseLerp(0.2f, 1f, _spool.Spool)) * Mathf.Clamp(attackSpeedStat, 1f, 2f);
            if (SvrLidar.MinigunMode == 0) rate *= 0.88f; // Iridium AP heavy fire rate
            if (_spool.Overheated) rate *= 0.55f;
            _timer += 1f / Mathf.Max(0.5f, rate);
            if (_timer < 0f) _timer = 0f;
            Shoot(aim, anchored);
        }

        private void Shoot(Ray aim, bool anchored)
        {
            _shotCounter++;
            int mMode = SvrLidar.MinigunMode % 4;
            bool isIridium = (mMode == 0);
            bool isCryo = (mMode == 1);
            bool isIncendiary = (mMode == 2);
            bool isPlasma = (mMode == 3);

            bool hasGyro = SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS);
            bool hasLeech = SvrUpgrades.Has(SvrUpgrades.AETHER_LEECH);

            float spread = Mathf.Lerp(SvrPlugin.SpreadMax.Value, SvrPlugin.SpreadMin.Value, _spool.Spool) * SvrSenses.SpreadMult * (anchored ? 0.5f : (hasGyro ? 0.72f : 1f));
            float dmgMult = (anchored ? 1f + SvrPlugin.AnchorDamageBonus.Value : 1f) * (isIridium ? 1.6f : (isIncendiary ? 1.15f : 1f));
            float dmg = damageStat * SvrPlugin.BulletDamage.Value * dmgMult;
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");

            string shootSound = isCryo ? "Play_MULT_m1_smg_shoot" : (isIridium ? "Play_commando_M2" : (isIncendiary ? "Play_bandit2_m1_shoot" : "Play_commando_M1"));
            Util.PlaySound(shootSound, gameObject);
            characterBody.AddSpreadBloom(hasGyro ? 0.05f : 0.09f);
            float rk = (anchored ? 0.45f : 1f) * (hasGyro ? 0.5f : 1f);     // heavy gun: the camera kicks with every round
            AddRecoil(-0.35f * rk, -0.75f * rk, -0.25f * rk, 0.25f * rk);
            _spool.KickPulse += isIridium ? 0.35f : 0.22f;

            // Cryo cooling: cools down minigun heat on each shot
            if (isCryo)
            {
                _spool.Heat = Mathf.Max(0f, _spool.Heat - 0.045f);
                _spool.Overheated = false;
            }

            DamageTypeCombo dtCombo = DamageType.Generic;
            if (isCryo) dtCombo = DamageType.SlowOnHit;
            if (isIncendiary) dtCombo = DamageType.IgniteOnHit;
            if (isPlasma && (_shotCounter % 3 == 0)) dtCombo = DamageType.Shock5s;

            new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = spread,
                damage = dmg,
                force = isIridium ? 280f : 120f,
                tracerEffectPrefab = _tracer,
                muzzleName = "MuzzleRight",
                hitEffectPrefab = _hit,
                isCrit = RollCrit(),
                radius = isIridium ? 0.45f : 0.25f,
                smartCollision = true,
                maxDistance = SvrPlugin.MinigunRange.Value,
                procCoefficient = isIridium ? 0.9f : 0.7f,
                damageType = dtCombo
            }.Fire();

            // Depleted-Uranium Slug & Cryo-Jacket & Plasma-Accelerator on-hit effects
            try
            {
                RaycastHit hit;
                if (Physics.Raycast(aim.origin, aim.direction, out hit, SvrPlugin.MinigunRange.Value, LayerIndex.entityPrecise.mask | LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    HurtBox hb = hit.collider ? hit.collider.GetComponent<HurtBox>() : null;
                    HealthComponent hc = hb ? hb.healthComponent : null;
                    if (hc && hc.alive && hc.body && hc.body != characterBody)
                    {
                        if (hasLeech && characterBody.healthComponent)
                            characterBody.healthComponent.AddBarrier(Mathf.Max(1.5f, characterBody.maxBarrier * 0.012f));

                        if (NetworkServer.active)
                        {
                            if (isIridium) hc.body.AddTimedBuff(RoR2Content.Buffs.Pulverized, 4f);
                            if (isCryo) hc.body.AddTimedBuff(RoR2Content.Buffs.Slow80, 3.5f);
                            if (isIncendiary)
                            {
                                InflictDotInfo info = new InflictDotInfo
                                {
                                    victimObject = hc.gameObject,
                                    attackerObject = gameObject,
                                    totalDamage = dmg,
                                    dotIndex = DotController.DotIndex.Burn,
                                    damageMultiplier = 1f
                                };
                                DotController.InflictDot(ref info);
                            }
                            if (isPlasma && _spool.Spool > 0.5f && (_shotCounter % 2 == 0))
                            {
                                LightningOrb orb = new LightningOrb
                                {
                                    origin = hit.point,
                                    damageValue = dmg * 0.85f,
                                    isCrit = RollCrit(),
                                    bouncesRemaining = 3,
                                    teamIndex = TeamComponent.GetObjectTeam(gameObject),
                                    attacker = gameObject,
                                    procCoefficient = 0.5f,
                                    lightningType = LightningOrb.LightningType.Ukulele,
                                    range = 20f
                                };
                                orb.bouncedObjects = new List<HealthComponent> { hc };
                                HurtBox nextTarget = orb.PickNextTarget(hit.point);
                                if (nextTarget)
                                {
                                    orb.target = nextTarget;
                                    OrbManager.instance.AddOrb(orb);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (_lidar) _lidar.Shot(aim, _spool.Spool, anchored);
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Any; }
    }

    // ================================================================ M2: Industrial Poltergust Vacuum Cleaner

    public class SvrVacuumState : BaseSkillState
    {
        private SvrLidar _lidar;
        private float _soundTimer;

        public override void OnEnter()
        {
            base.OnEnter();
            _lidar = GetComponent<SvrLidar>();
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_engi_M2_throw", gameObject);
            _soundTimer = 0.22f;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            Ray aim = GetAimRay();
            StartAimMode(aim, 1.5f, false);

            if (_lidar)
            {
                _lidar.VacuumHold(aim, Time.fixedDeltaTime, characterBody);
            }

            _soundTimer -= Time.fixedDeltaTime;
            if (_soundTimer <= 0f)
            {
                _soundTimer = 0.32f;
                Util.PlaySound("Play_commando_R", gameObject);
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
            Util.PlaySound("Play_drone_repair", gameObject);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Skill; }
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
            Util.PlaySound("Play_commando_shift", gameObject);
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
                    Util.PlaySound("Play_commando_R", gameObject);
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
