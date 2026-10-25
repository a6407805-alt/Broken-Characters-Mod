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
        public const int DU_SLUG = 0;           // Minigun: Depleted-Uranium Slug Fed (slower fire, heavy AP slugs that shred armor/barriers)
        public const int CRYO_JACKET = 1;       // Minigun: Cryo-Quench Cooling Jacket (liquid-nitrogen feed, no overheat, frost slow on hit)
        public const int PLASMA_ACCEL = 2;      // Minigun: Overclocked Plasma-Accelerator (sustained fire arcs chain plasma lightning)
        public const int GYRO_HARNESS = 3;      // Minigun: Gyroscopic Shoulder Harness (no move slow while firing, 50% less recoil kick)
        public const int SINGULARITY_CORE = 4;  // Super Vac: Singularity Vortex (pulls in nearby enemies & flying pests into nozzle)
        public const int REPULSOR_BLOW = 5;     // Super Vac: Reverse-Flow Repulsor ("Blow" function [V] blasts swarms & reflects projectiles)
        public const int AETHER_LEECH = 6;      // Super Vac: Aether-Leech Field (siphons enemy shields/health into EVA suit barrier)
        public const int COMPRESSION_PRESS = 7; // Containment Cell: Spectral Compression Press (dual capacity, shield armor, active-reload bonus)
        public const int COUNT = 8;

        public static readonly string[] Names =
        {
            "DEPLETED-URANIUM SLUG FED",
            "CRYO-QUENCH COOLING JACKET",
            "OVERCLOCKED PLASMA-ACCELERATOR",
            "GYROSCOPIC SHOULDER HARNESS",
            "SINGULARITY VORTEX",
            "REVERSE-FLOW REPULSOR [V]",
            "AETHER-LEECH FIELD",
            "SPECTRAL COMPRESSION PRESS"
        };

        public static readonly string[] ShortNames =
        {
            "DU-SLUG",
            "CRYO-JACKET",
            "PLASMA-ARC",
            "GYRO-RIG",
            "SINGULARITY",
            "REPULSOR [V]",
            "AETHER-LEECH",
            "COMPRESSOR"
        };

        public static readonly string[] Descs =
        {
            "Heavy AP slugs (+75% dmg, shreds boss armor & barriers)",
            "Liquid-N2 cooling (eliminates overheat + chills enemies)",
            "Sustained spool arcs high-voltage plasma lightning",
            "Zero move penalty while firing & -50% recoil climb",
            "Vacuum pulls nearby enemies & flying pests into nozzle",
            "Press [V] to blast swarms back & vaporize projectiles",
            "Firing & tethering siphons energy into EVA suit barrier",
            "Holds 2 spirits, +25 armor, & Gold-Zone active reload"
        };

        private static readonly bool[] _unlocked = new bool[COUNT];
        public static int UnlockedCount { get; private set; }

        public static bool Has(int id) => id >= 0 && id < COUNT && _unlocked[id];

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
            float heatRate = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) ? 0.035f : 0.11f;
            if (AnchorLeft > 0f) heatRate *= 0.65f;
            Heat = Mathf.Clamp01(Heat + Time.fixedDeltaTime * heatRate);
            if (Heat >= 0.99f && !SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET))
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
                float windDown = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) ? 2.4f : 1.4f;
                if (Spool > 0f)
                    Spool = Mathf.MoveTowards(Spool, 0f, dt / (SvrPlugin.SpoolSeconds.Value * windDown));
                float coolRate = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) ? 0.55f : 0.28f;
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
            if (SvrUpgrades.Has(SvrUpgrades.DU_SLUG)) rate *= 0.78f;
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

            bool hasDu = SvrUpgrades.Has(SvrUpgrades.DU_SLUG) || isIridium;
            bool hasCryo = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) || isCryo;
            bool hasPlasma = SvrUpgrades.Has(SvrUpgrades.PLASMA_ACCEL) || isPlasma;
            bool hasGyro = SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS);
            bool hasLeech = SvrUpgrades.Has(SvrUpgrades.AETHER_LEECH);

            float spread = Mathf.Lerp(SvrPlugin.SpreadMax.Value, SvrPlugin.SpreadMin.Value, _spool.Spool) * SvrSenses.SpreadMult * (anchored ? 0.5f : (hasGyro ? 0.72f : 1f));
            float dmgMult = (anchored ? 1f + SvrPlugin.AnchorDamageBonus.Value : 1f) * (isIridium ? 1.6f : (isIncendiary ? 1.15f : 1f));
            float dmg = damageStat * SvrPlugin.BulletDamage.Value * dmgMult;
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");

            string shootSound = isCryo ? "Play_MULT_m1_smg_shoot" : (isIridium ? "Play_commando_M2" : "Play_commando_M1");
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
            if (hasCryo) dtCombo = DamageType.SlowOnHit;
            if (isIncendiary) dtCombo = DamageType.IgniteOnHit;
            if (hasPlasma && (_shotCounter % 3 == 0)) dtCombo = DamageType.Shock5s;

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
                            if (hasDu) hc.body.AddTimedBuff(RoR2Content.Buffs.Pulverized, 4f);
                            if (hasCryo) hc.body.AddTimedBuff(RoR2Content.Buffs.Slow80, 3.5f);
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
                            if (hasPlasma && _spool.Spool > 0.5f && (_shotCounter % 2 == 0))
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
            float duration = SvrPlugin.PeekSeconds != null ? SvrPlugin.PeekSeconds.Value : 5.0f;
            if (duration < 1f) duration = 5.0f;
            SvrLidar lidar = GetComponent<SvrLidar>();
            if (lidar)
            {
                if (lidar.Peeking)
                {
                    lidar.EndPeek();
                }
                else
                {
                    lidar.StartPeek(duration);
                    SvrRigPart.OpenVisor(gameObject, duration);
                    Util.PlaySound("Play_commando_R", gameObject);
                }
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.25f && isAuthority) outer.SetNextStateToMain();
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
