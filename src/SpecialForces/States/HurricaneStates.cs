using System.Collections.Generic;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace SpecialForces.States
{
    // The Hurricane's moves. Sizes scale with its height (config "Height", 24 m by default).
    internal static class HurricaneFx
    {
        public static float H => SFConfig.BossHeight.Value;

        public static void Boom(Vector3 at, float scale)
        {
            GameObject boom = SFUtil.Load<GameObject>(Keys.OmniExplosion, "OmniExplosionVFX");
            if (boom) EffectManager.SpawnEffect(boom, new EffectData { origin = at, scale = scale }, true);
        }

        public static void Lightning(Vector3 ground)
        {
            if (SFEffects.Bolt) EffectManager.SpawnEffect(SFEffects.Bolt, new EffectData { origin = ground, start = ground + new Vector3(Random.Range(-20f, 20f), 160f, Random.Range(-20f, 20f)) }, true);
            GameObject impact = SFUtil.Load<GameObject>(Keys.LightningStrikeImpact, "LightningStrikeImpact");
            if (impact) EffectManager.SpawnEffect(impact, new EffectData { origin = ground, scale = 6f }, true);
        }
    }

    public class HurricaneSpawn : BaseState
    {
        private const float Duration = 2.5f;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "Roar", "Roar.playbackRate", Duration);
            SFAudio.PlayNear(SFAudio.Clip.BossRoar, transform.position, 300f);
            if (NetworkServer.active) characterBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, Duration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Death;
    }

    // Thunder Claw: a sweeping swipe in front (600%).
    public class HurricaneClaw : BaseSkillState
    {
        private float duration;
        private bool hit;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = 1.4f / attackSpeedStat;
            PlayAnimation("FullBody, Override", "Claw", "Claw.playbackRate", duration);
            SFAudio.PlayNear(SFAudio.Clip.BossGrowl, transform.position, 150f);
            if (characterDirection && inputBank) characterDirection.forward = inputBank.aimDirection;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!hit && fixedAge >= duration * 0.45f)
            {
                hit = true;
                Vector3 forward = characterDirection ? characterDirection.forward : transform.forward;
                Vector3 center = characterBody.footPosition + forward * HurricaneFx.H * 0.55f + Vector3.up * HurricaneFx.H * 0.15f;
                if (NetworkServer.active)
                {
                    new BlastAttack
                    {
                        attacker = gameObject, inflictor = gameObject, teamIndex = GetTeam(), position = center,
                        radius = HurricaneFx.H * 0.5f, baseDamage = damageStat * 6f, baseForce = 2500f, bonusForce = forward * 2500f + Vector3.up * 800f,
                        crit = RollCrit(), procCoefficient = 1f, falloffModel = BlastAttack.FalloffModel.SweetSpot,
                        attackerFiltering = AttackerFiltering.NeverHitSelf,
                    }.Fire();
                    HurricaneFx.Boom(center, HurricaneFx.H * 0.4f);
                }
                SFAudio.PlayNear(SFAudio.Clip.BossSlam, center, 150f);
            }
            if (fixedAge >= duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // Lightning Barrage: 12 strikes (400%) on the players, allies and buildings around it, each telegraphed by a
    // ring on the ground a second before it lands.
    public class HurricaneBarrage : BaseSkillState
    {
        private const float Duration = 2.2f, Telegraph = 1.1f, Radius = 7f;
        private const int Strikes = 12;
        private int fired;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "Cast", "Cast.playbackRate", Duration);
            SFAudio.PlayNear(SFAudio.Clip.BossRoar, transform.position, 250f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active)
            {
                while (fired < Strikes && fixedAge >= 0.3f + fired * (Duration - 0.5f) / Strikes)
                {
                    fired++;
                    Vector3? target = PickTarget();
                    if (target.HasValue) StrikeScheduler.Schedule(target.Value, Telegraph, Radius, damageStat * 4f, gameObject, GetTeam(), RollCrit());
                }
            }
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        private Vector3? PickTarget()
        {
            var candidates = new List<CharacterBody>();
            foreach (var tc in TeamComponent.GetTeamMembers(TeamIndex.Player))
                if (tc && tc.body && (tc.body.corePosition - characterBody.corePosition).sqrMagnitude < 170f * 170f) candidates.Add(tc.body);
            if (candidates.Count == 0) return null;
            var b = candidates[Random.Range(0, candidates.Count)];
            Vector2 jitter = Random.insideUnitCircle * 5f;
            return SFUtil.Ground(b.footPosition + new Vector3(jitter.x, 0, jitter.y), 10f);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // Server: a strike lands after its telegraph even if the boss has moved on.
    public class StrikeScheduler : MonoBehaviour
    {
        private Vector3 at;
        private float delay, radius, damage;
        private GameObject attacker;
        private TeamIndex team;
        private bool crit;

        public static void Schedule(Vector3 at, float delay, float radius, float damage, GameObject attacker, TeamIndex team, bool crit)
        {
            if (SFEffects.Warning) EffectManager.SpawnEffect(SFEffects.Warning, new EffectData { origin = at, scale = radius }, true);
            var s = new GameObject("SFStrike").AddComponent<StrikeScheduler>();
            s.at = at; s.delay = delay; s.radius = radius; s.damage = damage; s.attacker = attacker; s.team = team; s.crit = crit;
        }

        private void FixedUpdate()
        {
            delay -= Time.fixedDeltaTime;
            if (delay > 0f) return;
            HurricaneFx.Lightning(at);
            new BlastAttack
            {
                attacker = attacker, inflictor = attacker, teamIndex = team, position = at + Vector3.up, radius = radius,
                baseDamage = damage, baseForce = 600f, crit = crit, procCoefficient = 0.5f, falloffModel = BlastAttack.FalloffModel.None,
                damageType = DamageType.Shock5s, attackerFiltering = AttackerFiltering.NeverHitSelf,
            }.Fire();
            Destroy(gameObject);
        }
    }

    // Cyclone: three tornadoes spread in front, crawling forward for 10 s, pulling and battering (150%/s).
    public class HurricaneCyclone : BaseSkillState
    {
        private const float Duration = 2f;
        private bool launched;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "Cast", "Cast.playbackRate", Duration);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!launched && fixedAge >= Duration * 0.4f)
            {
                launched = true;
                if (NetworkServer.active && SFBoss.CyclonePrefab)
                {
                    Vector3 forward = inputBank ? inputBank.aimDirection : transform.forward;
                    forward.y = 0;
                    forward.Normalize();
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector3 dir = Quaternion.Euler(0, i * 25f, 0) * forward;
                        Vector3 start = SFUtil.Ground(characterBody.footPosition + dir * HurricaneFx.H * 0.6f, 20f);
                        var go = Object.Instantiate(SFBoss.CyclonePrefab, start, Quaternion.identity);
                        var c = go.GetComponent<Cyclone>();
                        c.owner = gameObject;
                        c.team = GetTeam();
                        c.damage = damageStat * 1.5f * 0.5f;    // per 0.5 s tick: 150%/s
                        c.direction = dir;
                        NetworkServer.Spawn(go);
                    }
                }
            }
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // Storm Roar: rears up and slams down; a 40 m shockwave (300%) that throws everything back.
    public class HurricaneRoar : BaseSkillState
    {
        private const float Duration = 1.8f;
        private bool slammed;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "Slam", "Slam.playbackRate", Duration / attackSpeedStat);
            SFAudio.PlayNear(SFAudio.Clip.BossRoar, transform.position, 300f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!slammed && fixedAge >= Duration / attackSpeedStat * 0.55f)
            {
                slammed = true;
                Vector3 at = characterBody.footPosition;
                float radius = HurricaneFx.H * 40f / 24f;
                if (NetworkServer.active)
                {
                    new BlastAttack
                    {
                        attacker = gameObject, inflictor = gameObject, teamIndex = GetTeam(), position = at + Vector3.up * 2f, radius = radius,
                        baseDamage = damageStat * 3f, baseForce = 5000f, bonusForce = Vector3.up * 1500f, crit = RollCrit(),
                        procCoefficient = 1f, falloffModel = BlastAttack.FalloffModel.Linear, attackerFiltering = AttackerFiltering.NeverHitSelf,
                    }.Fire();
                    HurricaneFx.Boom(at, radius * 0.5f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        HurricaneFx.Lightning(SFUtil.Ground(at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius * 0.7f, 20f));
                    }
                }
                SFAudio.PlayNear(SFAudio.Clip.BossSlam, at, 300f);
            }
            if (fixedAge >= Duration / attackSpeedStat && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.PrioritySkill;
    }

    // Squall (75 / 50 / 25% health): roars and calls a lair boss plus a pack of elites out of the storm.
    public class HurricaneSquall : BaseState
    {
        private const float Duration = 2.5f;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "Roar", "Roar.playbackRate", Duration);
            SFAudio.PlayNear(SFAudio.Clip.BossRoar, transform.position, 400f);
            if (!NetworkServer.active) return;
            characterBody.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, Duration);
            SFSquall.Call(characterBody.footPosition);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= Duration && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Death;
    }

    public class HurricaneDeath : BaseState
    {
        private const float Duration = 7f;

        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("FullBody, Override", "BufferEmpty");
            PlayAnimation("Body", "Death");
            SFAudio.PlayNear(SFAudio.Clip.BossRoar, transform.position, 600f);
            if (NetworkServer.active)
            {
                Vector3 at = characterBody ? characterBody.footPosition : transform.position;
                for (int i = 0; i < 8; i++)
                    HurricaneFx.Lightning(SFUtil.Ground(at + Random.insideUnitSphere * HurricaneFx.H, 30f));
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (NetworkServer.active && fixedAge >= Duration) EntityState.Destroy(gameObject);
        }

        public override InterruptPriority GetMinimumInterruptPriority() => InterruptPriority.Death;
    }
}
