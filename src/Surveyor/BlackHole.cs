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

            // straight line, steady speed, no slowing down: it drags everything along its path
            float speed = SvrPlugin.BlackHoleSpeed.Value;
            ProjectileSimple ps = Prefab.GetComponent<ProjectileSimple>();
            if (ps)
            {
                Set(ps, "desiredForwardSpeed", speed);
                Set(ps, "velocity", speed);
                Set(ps, "lifetime", life + 0.3f);
                Set(ps, "enableVelocityOverLifetime", false);
                Set(ps, "updateAfterFiring", true);
            }
            Rigidbody rb = Prefab.GetComponent<Rigidbody>();
            if (rb) { rb.useGravity = false; rb.drag = 0f; }
            foreach (Collider c in Prefab.GetComponentsInChildren<Collider>(true))
                if (c && !c.isTrigger) c.isTrigger = true;     // flies through enemies instead of bumping to a stop
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

    /// <summary>M2: charge the Black Hole (a dark orb grows in the gun), then fire it.</summary>
    public class SvrBlackHoleState : BaseSkillState
    {
        private float _charge;
        private bool _fired;
        private GameObject _orb;
        private Light _light;
        private static Material _orbMat;

        public override void OnEnter()
        {
            base.OnEnter();
            _charge = SvrPlugin.BlackHoleCharge.Value / Mathf.Max(0.5f, attackSpeedStat);
            Util.PlaySound("Play_voidman_m2_chargeUp", gameObject);
            Util.PlaySound("Play_mage_m2_charge", gameObject);
            PlayAnimation("Gesture Additive, Right", "FirePistol, Right");
            if (isAuthority) StartAimMode(GetAimRay(), 2f, false);
            try { MakeOrb(); } catch { }
        }

        private void MakeOrb()
        {
            if (!_orbMat)
            {
                Shader hg = Shader.Find("Hopoo Games/Deferred/Standard");
                if (!hg) return;
                _orbMat = new Material(hg) { name = "SvrBlackHoleCharge" };
                _orbMat.SetColor("_Color", new Color(0.02f, 0f, 0.05f));
                if (_orbMat.HasProperty("_EmColor")) _orbMat.SetColor("_EmColor", new Color(0.55f, 0.15f, 1f));
                if (_orbMat.HasProperty("_EmTex")) _orbMat.SetTexture("_EmTex", Texture2D.whiteTexture);
                if (_orbMat.HasProperty("_EmPower")) _orbMat.SetFloat("_EmPower", 0.6f);
            }
            _orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _orb.name = "SvrBlackHoleCharge";
            Object.Destroy(_orb.GetComponent<Collider>());
            _orb.GetComponent<Renderer>().sharedMaterial = _orbMat;
            _orb.transform.localScale = Vector3.zero;
            GameObject lg = new GameObject("SvrBlackHoleChargeLight");
            lg.transform.SetParent(_orb.transform, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.6f, 0.2f, 1f);
            _light.range = 6f;
        }

        public override void Update()
        {
            base.Update();
            if (!_orb) return;
            Ray aim = GetAimRay();
            float k = Mathf.Clamp01(age / Mathf.Max(0.05f, _charge));
            _orb.transform.position = aim.origin + aim.direction * 1.6f + Vector3.down * 0.15f;
            float wob = 1f + 0.12f * Mathf.Sin(age * 40f);
            _orb.transform.localScale = Vector3.one * (0.55f * k * wob);
            if (_light) _light.intensity = 4f * k;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!_fired && fixedAge >= _charge)
            {
                _fired = true;
                if (_orb) { Object.Destroy(_orb); _orb = null; }
                Util.PlaySound("Play_voidman_shift_pop", gameObject);
                Util.PlaySound("Play_voidman_m2_shoot", gameObject);
                if (isAuthority) Fire();
            }
            if (_fired && fixedAge >= _charge + 0.25f && isAuthority) outer.SetNextStateToMain();
        }

        private void Fire()
        {
            Ray aim = GetAimRay();
            AddRecoil(-3f, -4f, -0.8f, 0.8f);
            if (SvrBlackHole.Prefab && ProjectileManager.instance)
            {
                ProjectileManager.instance.FireProjectile(new FireProjectileInfo
                {
                    projectilePrefab = SvrBlackHole.Prefab,
                    position = aim.origin + aim.direction * 1.6f,
                    rotation = Util.QuaternionSafeLookRotation(aim.direction),
                    owner = gameObject,
                    damage = damageStat * SvrPlugin.BlackHoleDamage.Value,
                    force = 0f,
                    crit = RollCrit(),
                    speedOverride = SvrPlugin.BlackHoleSpeed.Value,
                    damageTypeOverride = DamageType.Stun1s
                });
            }
            else
            {
                SvrLidar lidar = GetComponent<SvrLidar>();
                if (lidar) lidar.TryFireSingularity(aim, characterBody);
            }
        }

        public override void OnExit()
        {
            if (_orb) Object.Destroy(_orb);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return _fired ? InterruptPriority.Any : InterruptPriority.PrioritySkill; }
    }
}
