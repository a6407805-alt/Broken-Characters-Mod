using System;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BerserkFury
{
    // ================================================================ Primary: Buster Blaster
    public class BusterBlasterState : BaseSkillState
    {
        private static bool _alternateSide;
        public static float BaseDuration = 0.38f;
        public static float DamageCoeff = 3.2f;

        private float _duration;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = BaseDuration / Mathf.Max(attackSpeedStat, 0.5f);
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);

            _alternateSide = !_alternateSide;
            Vector3 sideOffset = characterBody.gameObject.transform.right * (_alternateSide ? 0.9f : -0.9f) + Vector3.up * 1.5f;

            if (isAuthority)
            {
                FireBusterShot(aim, sideOffset);
                CheckMeleeClaw(aim);
            }

            if (!(isAuthority && BerserkFurySfx.Play("m1"))) Util.PlaySound("Play_golem_laser_fire", gameObject);
        }

        private void FireBusterShot(Ray aim, Vector3 offset)
        {
            Vector3 origin = aim.origin + offset;
            BulletAttack attack = new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = origin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = 0.4f,
                bulletCount = 1,
                damage = damageStat * DamageCoeff,
                force = 600f,
                tracerEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerToolbotRebar"),
                hitEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/ImpactToolbotRebar"),
                isCrit = RollCrit(),
                damageType = DamageType.Generic
            };
            attack.Fire();

            // Gain particle charge via passive
            var passive = GetComponent<BerserkFuryPassiveController>();
            if (passive) passive.AddCharge(2f);
        }

        private void CheckMeleeClaw(Ray aim)
        {
            // Close range Strike Laser Claw melee strike
            BlastAttack melee = new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = TeamComponent.GetObjectTeam(gameObject),
                baseDamage = damageStat * 1.5f,
                baseForce = 400f,
                position = transform.position + transform.forward * 2.2f + Vector3.up * 1.0f,
                radius = 3.5f,
                falloffModel = BlastAttack.FalloffModel.None,
                crit = RollCrit(),
                damageType = DamageType.BleedOnHit
            };
            melee.Fire();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= _duration && isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Skill;
    }

    // ================================================================ Secondary: Magneser Drill
    public class MagneserDrillState : BaseSkillState
    {
        public static float BaseDuration = 0.75f;
        private float _duration;
        private float _tickTimer;
        private const float TickInterval = 0.10f;

        public override void OnEnter()
        {
            base.OnEnter();
            _duration = BaseDuration;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);

            if (characterMotor)
            {
                characterMotor.velocity = aim.direction * 32f;
            }

            if (!(isAuthority && BerserkFurySfx.Play("drill"))) { Util.PlaySound("Play_loader_m2_charge", gameObject); Util.PlaySound("Play_loader_m1_swing", gameObject); }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            _tickTimer += Time.fixedDeltaTime;
            if (_tickTimer >= TickInterval && isAuthority)
            {
                _tickTimer -= TickInterval;
                DoDrillTick();
            }

            if (fixedAge >= _duration && isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }

        private void DoDrillTick()
        {
            BlastAttack drill = new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = TeamComponent.GetObjectTeam(gameObject),
                baseDamage = damageStat * 0.85f,
                baseForce = 250f,
                position = transform.position + transform.forward * 2.5f + Vector3.up * 1.0f,
                radius = 4.5f,
                falloffModel = BlastAttack.FalloffModel.None,
                crit = RollCrit(),
                damageType = DamageType.Generic
            };
            drill.Fire();

            var passive = GetComponent<BerserkFuryPassiveController>();
            if (passive) passive.AddCharge(1.5f);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // ================================================================ Utility: Ion Thruster Surge
    public class IonThrusterSurgeState : BaseSkillState
    {
        public static float Duration = 0.55f;
        private Vector3 _dashDirection;

        public override void OnEnter()
        {
            base.OnEnter();
            Ray aim = GetAimRay();
            _dashDirection = aim.direction;
            StartAimMode(aim, 2f, false);

            if (characterMotor)
            {
                characterMotor.velocity = _dashDirection * 45f;
            }

            if (!(isAuthority && BerserkFurySfx.Play("boost"))) { Util.PlaySound("Play_commando_shift", gameObject); Util.PlaySound("Play_mage_shift_wall_explode", gameObject); }

            if (NetworkServer.active)
            {
                characterBody.AddTimedBuff(RoR2Content.Buffs.ArmorBoost, Duration + 1f);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (characterMotor)
            {
                characterMotor.velocity = Vector3.Lerp(characterMotor.velocity, _dashDirection * 35f, Time.fixedDeltaTime * 4f);
            }

            if (fixedAge >= Duration && isAuthority)
            {
                // Concussive shockwave on dash completion
                BlastAttack shockwave = new BlastAttack
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    teamIndex = TeamComponent.GetObjectTeam(gameObject),
                    baseDamage = damageStat * 4.5f,
                    baseForce = 1200f,
                    position = transform.position,
                    radius = 7f,
                    falloffModel = BlastAttack.FalloffModel.Linear,
                    crit = RollCrit(),
                    damageType = DamageType.Stun1s
                };
                shockwave.Fire();

                outer.SetNextStateToMain();
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Pain;
    }

    // ================================================================ Special: Charged Particle Cannon (CPC)
    public class ChargedParticleCannonState : BaseSkillState
    {
        public static float ChargeDuration = 1.0f;
        public static float FiringDuration = 2.4f;
        private bool _isFiring;
        private float _fireTimer;
        private const float FireTickInterval = 0.12f;

        public override void OnEnter()
        {
            base.OnEnter();
            Ray aim = GetAimRay();
            StartAimMode(aim, 4f, false);

            // Anchor into ground
            if (characterMotor && characterMotor.isGrounded)
            {
                characterMotor.velocity = Vector3.zero;
            }

            var passive = GetComponent<BerserkFuryPassiveController>();
            if (passive) passive.IsChargingSpecial = true;

            if (!(isAuthority && BerserkFurySfx.Play("cpc_charge"))) Util.PlaySound("Play_golem_laser_charge", gameObject);
        }

        public override void OnExit()
        {
            var passive = GetComponent<BerserkFuryPassiveController>();
            if (passive) passive.IsChargingSpecial = false;
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            // Steer aim smoothly
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);

            // Restrict motion while anchored
            if (characterMotor && characterMotor.isGrounded)
            {
                characterMotor.velocity = Vector3.zero;
            }

            if (!_isFiring && fixedAge >= ChargeDuration)
            {
                _isFiring = true;
                if (!(isAuthority && BerserkFurySfx.Play("cpc_fire"))) Util.PlaySound("Play_titan_laser_fire", gameObject);
            }

            if (_isFiring)
            {
                _fireTimer += Time.fixedDeltaTime;
                if (_fireTimer >= FireTickInterval && isAuthority)
                {
                    _fireTimer -= FireTickInterval;
                    FireParticleBeamTick(aim);
                }

                if (fixedAge >= ChargeDuration + FiringDuration && isAuthority)
                {
                    // Terminal detonation burst
                    BlastAttack terminal = new BlastAttack
                    {
                        attacker = gameObject,
                        inflictor = gameObject,
                        teamIndex = TeamComponent.GetObjectTeam(gameObject),
                        baseDamage = damageStat * 6f,
                        baseForce = 1500f,
                        position = aim.origin + aim.direction * 30f,
                        radius = 12f,
                        falloffModel = BlastAttack.FalloffModel.None,
                        crit = true,
                        damageType = DamageType.IgniteOnHit
                    };
                    terminal.Fire();

                    outer.SetNextStateToMain();
                }
            }
        }

        private void FireParticleBeamTick(Ray aim)
        {
            Vector3 mouthOrigin = transform.position + Vector3.up * 1.6f + transform.forward * 1.8f;
            BulletAttack beam = new BulletAttack
            {
                owner = gameObject,
                weapon = gameObject,
                origin = mouthOrigin,
                aimVector = aim.direction,
                minSpread = 0f,
                maxSpread = 0.1f,
                bulletCount = 1,
                damage = damageStat * 1.6f,
                force = 800f,
                radius = 1.2f,
                stopperMask = LayerIndex.world.mask,
                tracerEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/Tracers/TracerGolem"),
                hitEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/ImpactToolbotRebar"),
                isCrit = RollCrit(),
                damageType = DamageType.BypassArmor
            };
            beam.Fire();

            var passive = GetComponent<BerserkFuryPassiveController>();
            if (passive) passive.AddCharge(3f);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Death;
    }
}
