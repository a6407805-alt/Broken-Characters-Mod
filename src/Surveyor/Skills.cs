using System.Collections.Generic;
using EntityStates;
using RoR2;
using RoR2.Orbs;
using RoR2.Projectile;
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
        public const int SINGULARITY_CORE = 4;  // Super Vac: Singularity Trap Core (trap pulls in nearby enemies & flying pests)
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
            "SINGULARITY TRAP CORE",
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
            "Ghost-Trap pulls nearby enemies & flying pests inward",
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
            bool hasDu = SvrUpgrades.Has(SvrUpgrades.DU_SLUG);
            bool hasCryo = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET);
            bool hasPlasma = SvrUpgrades.Has(SvrUpgrades.PLASMA_ACCEL);
            bool hasGyro = SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS);
            bool hasLeech = SvrUpgrades.Has(SvrUpgrades.AETHER_LEECH);

            float spread = Mathf.Lerp(SvrPlugin.SpreadMax.Value, SvrPlugin.SpreadMin.Value, _spool.Spool) * SvrSenses.SpreadMult * (anchored ? 0.5f : (hasGyro ? 0.72f : 1f));
            float dmgMult = (anchored ? 1f + SvrPlugin.AnchorDamageBonus.Value : 1f) * (hasDu ? 1.75f : 1f);
            float dmg = damageStat * SvrPlugin.BulletDamage.Value * dmgMult;
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound(hasDu ? "Play_commando_M2" : "Play_commando_M1", gameObject);
            characterBody.AddSpreadBloom(hasGyro ? 0.06f : 0.12f);
            float rk = (anchored ? 0.45f : 1f) * (hasGyro ? 0.5f : 1f);     // heavy gun: the camera kicks with every round
            AddRecoil(-0.35f * rk, -0.75f * rk, -0.25f * rk, 0.25f * rk);
            _spool.KickPulse += hasDu ? 0.32f : 0.22f;

            DamageTypeCombo dtCombo = DamageType.Generic;
            if (hasCryo) dtCombo = DamageType.SlowOnHit;
            if (hasPlasma && (_shotCounter % 4 == 0)) dtCombo = DamageType.Shock5s;

            new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = spread,
                damage = dmg,
                force = hasDu ? 280f : 120f,
                tracerEffectPrefab = _tracer,
                muzzleName = "MuzzleRight",
                hitEffectPrefab = _hit,
                isCrit = RollCrit(),
                radius = hasDu ? 0.42f : 0.25f,
                smartCollision = true,
                maxDistance = SvrPlugin.MinigunRange.Value,
                procCoefficient = hasDu ? 0.9f : 0.7f,
                damageType = dtCombo
            }.Fire();

            // Depleted-Uranium Slug & Cryo-Jacket & Plasma-Accelerator on-hit effects
            if (hasDu || hasCryo || hasPlasma || hasLeech)
            {
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
                                if (hasPlasma && _spool.Spool > 0.6f && (_shotCounter % 3 == 0))
                                {
                                    LightningOrb orb = new LightningOrb
                                    {
                                        origin = hit.point,
                                        damageValue = dmg * 0.85f,
                                        isCrit = RollCrit(),
                                        bouncesRemaining = 2,
                                        teamIndex = TeamComponent.GetObjectTeam(gameObject),
                                        attacker = gameObject,
                                        procCoefficient = 0.5f,
                                        lightningType = LightningOrb.LightningType.Ukulele,
                                        range = 18f
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
            }

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
            Util.PlaySound("Play_drone_repair", gameObject);
            _soundTimer = 0.25f;
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
                _soundTimer = 0.35f;
                Util.PlaySound("Play_drone_repair", gameObject);
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
                if (inputBank && inputBank.skill3.down) outer.SetNextState(new SvrAnchorState());
                else outer.SetNextStateToMain();
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

    // ================================================================ R: Acoustic Shockwave

    public class SvrPulseState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_commando_R", gameObject);
            if (!isAuthority) return;

            float radius = SvrPlugin.PulseRadius.Value;
            Vector3 at = characterBody.corePosition;
            new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                baseDamage = damageStat * SvrPlugin.PulseDamage.Value,
                baseForce = SvrPlugin.PulseForce.Value,
                position = at,
                radius = radius,
                falloffModel = BlastAttack.FalloffModel.None,
                crit = RollCrit(),
                procCoefficient = 1f,
                teamIndex = TeamComponent.GetObjectTeam(gameObject)
            }.Fire();

            try
            {
                GameObject fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
                if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = at, scale = radius * 0.4f }, false);
            }
            catch { }

            SvrLidar lidar = GetComponent<SvrLidar>();
            if (lidar) lidar.Pulse(at, radius, characterBody);
            SvrSpool sp = GetComponent<SvrSpool>();
            if (sp) sp.Spool = Mathf.Max(sp.Spool, 0.5f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.5f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    // ================================================================ Utility alt: Peek (visor off)

    public class SvrPeekState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_commando_R", gameObject);
            SvrRigPart.OpenVisor(gameObject, SvrPlugin.PeekSeconds.Value);      // the visor flips up on every client
            if (isAuthority)
            {
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.StartPeek(SvrPlugin.PeekSeconds.Value);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.35f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Skill; }
    }

    // ================================================================ Special alt: LiDAR Overdrive (mode chosen with the mode key)

    public class SvrOverdriveState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_drone_repair", gameObject);
            if (isAuthority)
            {
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.Overdrive(SvrLidar.OdMode);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.3f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Skill; }
    }
}
