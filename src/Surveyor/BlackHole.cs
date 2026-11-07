using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

namespace Surveyor
{
    /// <summary>
    /// M2 Black Hole (v0.7.0): a real networked projectile cloned from the game's own gravity sphere (Primordial Cube),
    /// so its pull works on every enemy for every player. It flies out, slows to a stop, pulls everything in, then
    /// implodes for big damage and a stun. Field names are set by reflection so game updates can't break the build.
    /// </summary>
    public static class SvrBlackHole
    {
        public static GameObject Prefab;

        public static void Create()
        {
            GameObject src = null;
            try { src = LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/GravSphere"); } catch { }
            if (!src)
            {
                SvrPlugin.Log.LogWarning("Black Hole: GravSphere not found; M2 falls back to the local singularity.");
                return;
            }
            Prefab = PrefabAPI.InstantiateClone(src, "BCSurveyorBlackHole", true);
            float life = SvrPlugin.BlackHoleSeconds.Value;

            ProjectileSimple ps = Prefab.GetComponent<ProjectileSimple>();
            if (ps)
            {
                Set(ps, "desiredForwardSpeed", 34f);
                Set(ps, "velocity", 34f);
                Set(ps, "lifetime", life + 0.3f);
                Set(ps, "enableVelocityOverLifetime", true);
                Set(ps, "velocityOverLifetime", new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.12f, 0.3f), new Keyframe(0.25f, 0f), new Keyframe(1f, 0f)));
            }
            RadialForce rf = Prefab.GetComponent<RadialForce>();
            if (rf)
            {
                Set(rf, "radius", SvrPlugin.BlackHoleRadius.Value);
                object fm = Get(rf, "forceMagnitude");
                if (fm is float f && Mathf.Abs(f) > 1f) Set(rf, "forceMagnitude", -Mathf.Abs(f) * 1.25f);
            }
            ProjectileImpactExplosion pie = Prefab.GetComponent<ProjectileImpactExplosion>();
            if (!pie) pie = Prefab.AddComponent<ProjectileImpactExplosion>();
            Set(pie, "lifetime", life);
            Set(pie, "destroyOnEnemy", false);
            Set(pie, "destroyOnWorld", false);
            Set(pie, "detonateOnEnemy", false);
            Set(pie, "impactOnWorld", false);
            Set(pie, "blastRadius", SvrPlugin.BlackHoleRadius.Value * 0.75f);
            Set(pie, "blastDamageCoefficient", 1f);
            Set(pie, "blastProcCoefficient", 1f);
            Set(pie, "falloffModel", BlastAttack.FalloffModel.None);
            GameObject boom = null;
            try { boom = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX"); } catch { }
            if (boom) { Set(pie, "explosionEffect", boom); Set(pie, "impactEffect", boom); }
            Prefab.AddComponent<SvrBlackHoleFx>();
            ContentAddition.AddProjectile(Prefab);
        }

        private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void Set(object o, string name, object v)
        {
            if (o == null) return;
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (f != null && f.FieldType.IsInstanceOfType(v)) { try { f.SetValue(o, v); } catch { } return; }
                PropertyInfo p = t.GetProperty(name, Any | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(v)) { try { p.SetValue(o, v, null); } catch { } return; }
            }
        }

        private static object Get(object o, string name)
        {
            for (System.Type t = o.GetType(); t != null; t = t.BaseType)
            {
                FieldInfo f = t.GetField(name, Any | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(o);
            }
            return null;
        }
    }

    /// <summary>Lets the Surveyor's LiDAR find live black holes (it paints a spiral of dots into them).</summary>
    public class SvrBlackHoleFx : MonoBehaviour
    {
        public static readonly List<Transform> Live = new List<Transform>();
        private void OnEnable() { Live.Add(transform); }
        private void OnDisable() { Live.Remove(transform); }
    }

    /// <summary>M2: fire the Black Hole.</summary>
    public class SvrBlackHoleState : BaseSkillState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            Util.PlaySound("Play_voidman_shift_pop", gameObject);
            if (!isAuthority) return;
            Ray aim = GetAimRay();
            StartAimMode(aim, 2f, false);
            AddRecoil(-2f, -3f, -0.6f, 0.6f);
            if (SvrBlackHole.Prefab && ProjectileManager.instance)
            {
                ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                {
                    projectilePrefab = SvrBlackHole.Prefab,
                    position = aim.origin + aim.direction * 1.5f,
                    rotation = Util.QuaternionSafeLookRotation(aim.direction),
                    owner = gameObject,
                    damage = damageStat * SvrPlugin.BlackHoleDamage.Value,
                    force = 0f,
                    crit = RollCrit(),
                    damageTypeOverride = DamageType.Stun1s
                });
            }
            else
            {
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.TryFireSingularity(aim, characterBody);
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= 0.35f && isAuthority) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }
}
