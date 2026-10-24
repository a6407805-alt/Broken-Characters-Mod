using EntityStates;
using RoR2;
using UnityEngine;

namespace Surveyor
{
    // Damage is always dealt from the owner's own client (isAuthority); the Surveyor has no gold, items, buffs or summons,
    // so nothing here needs the host. The LiDAR and the Shimmer Demon are local to her client.

    // ================================================================ spool (state shared between presses of M1)

    /// <summary>How spun up the barrels are (0 to 1) and how long the anchor still holds. Lives on the body.</summary>
    public class SvrSpool : MonoBehaviour
    {
        public float Spool;
        public float AnchorLeft;
        private float _idle;
        private bool _slowed;
        private int _senseVersion = -1;
        private CharacterBody _body;

        private void Awake() { _body = GetComponent<CharacterBody>(); }

        /// <summary>The minigun calls this every tick it is held, so the barrels do not wind down.</summary>
        public void Held() { _idle = 0f; }

        private void FixedUpdate()
        {
            if (!_body || !_body.hasEffectiveAuthority) return;
            float dt = Time.fixedDeltaTime;
            _idle += dt;
            if (AnchorLeft > 0f) AnchorLeft = Mathf.Max(0f, AnchorLeft - dt);
            if (_idle > 0.15f && Spool > 0f)
                Spool = Mathf.MoveTowards(Spool, 0f, dt / (SvrPlugin.SpoolSeconds.Value * 1.4f));

            bool slowed = Spool > 0.3f && AnchorLeft <= 0f;
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
            _timer += 1f / Mathf.Max(0.5f, rate);
            if (_timer < 0f) _timer = 0f;
            Shoot(aim, anchored);
        }

        private void Shoot(Ray aim, bool anchored)
        {
            float spread = Mathf.Lerp(SvrPlugin.SpreadMax.Value, SvrPlugin.SpreadMin.Value, _spool.Spool) * SvrSenses.SpreadMult * (anchored ? 0.5f : 1f);
            float dmg = damageStat * SvrPlugin.BulletDamage.Value * (anchored ? 1f + SvrPlugin.AnchorDamageBonus.Value : 1f);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_commando_M1", gameObject);
            characterBody.AddSpreadBloom(0.12f);

            new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = aim.origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = spread,
                damage = dmg,
                force = 120f,
                tracerEffectPrefab = _tracer,
                muzzleName = "MuzzleRight",
                hitEffectPrefab = _hit,
                isCrit = RollCrit(),
                radius = 0.25f,
                smartCollision = true,
                maxDistance = SvrPlugin.MinigunRange.Value,
                procCoefficient = 0.7f
            }.Fire();

            if (_lidar) _lidar.Shot(aim, _spool.Spool, anchored);
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.Any; }
    }

    // ================================================================ M2: Surveyor Lance

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
