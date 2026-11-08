using System;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using R2API;

namespace BerserkFury
{
    public class BerserkFuryPassiveController : MonoBehaviour
    {
        public float ParticleCharge = 0f;
        public const float MaxCharge = 100f;
        public bool IsOverdrive => ParticleCharge >= 99.9f;
        public bool IsEShieldActive { get; private set; }
        public bool IsChargingSpecial;

        private CharacterBody _body;
        private HealthComponent _health;
        private GameObject _shieldVisual;
        private float _shieldAngle = 90f;
        private float _shieldRadius = 3.5f;

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _health = GetComponent<HealthComponent>();
            // No placeholder quad: the Buster Claws open and spin to show the E-Shield (BerserkFuryLocomotion).
        }

        private void Start()
        {
            GlobalEventManager.onServerDamageDealt += OnDamageDealt;
        }

        private void OnDestroy()
        {
            GlobalEventManager.onServerDamageDealt -= OnDamageDealt;
            if (_shieldVisual) Destroy(_shieldVisual);
        }

        private void OnDamageDealt(DamageReport report)
        {
            if (report == null || report.attacker != gameObject) return;

            // Build particle charge on hits
            float gain = 2.5f;
            if (report.victim && !report.victim.alive) gain += 5f;
            AddCharge(gain);
        }

        public void AddCharge(float amount)
        {
            ParticleCharge = Mathf.Clamp(ParticleCharge + amount, 0f, MaxCharge);
        }

        public void ConsumeCharge(float amount)
        {
            ParticleCharge = Mathf.Clamp(ParticleCharge - amount, 0f, MaxCharge);
        }

        private void Update()
        {
            if (!_body) return;

            // Decay charge slowly if not in combat
            if (ParticleCharge > 0f)
            {
                ParticleCharge = Mathf.Max(0f, ParticleCharge - (Time.deltaTime * 1.5f));
            }

            // E-Shield is active while sprinting OR while charging special
            bool shouldShield = _body.isSprinting || IsChargingSpecial;

            IsEShieldActive = shouldShield;
            if (_shieldVisual)
            {
                _shieldVisual.SetActive(shouldShield);
                if (shouldShield)
                {
                    _shieldVisual.transform.position = transform.position + Vector3.up * 1.2f + transform.forward * 1.5f;
                    _shieldVisual.transform.rotation = transform.rotation;
                }
            }

            // Projectile deflection when E-Shield is active
            if (NetworkServer.active && shouldShield)
            {
                DeflectIncomingProjectiles();
            }
        }

        private void DeflectIncomingProjectiles()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position + Vector3.up * 1.2f, _shieldRadius, LayerIndex.projectile.mask);
            foreach (Collider c in colliders)
            {
                if (!c) continue;
                var proj = c.GetComponentInParent<RoR2.Projectile.ProjectileController>();
                if (proj && proj.owner != gameObject)
                {
                    Vector3 toProj = (c.transform.position - transform.position).normalized;
                    if (Vector3.Angle(transform.forward, toProj) <= _shieldAngle * 0.5f)
                    {
                        // Deflect or vaporize hostile projectile
                        EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/LaserTurbineBombExplosion"),
                            new EffectData { origin = c.transform.position, scale = 0.5f }, true);
                        Destroy(proj.gameObject);
                    }
                }
            }
        }

        private void CreateShieldVisual()
        {
            _shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _shieldVisual.name = "BerserkFury_EShield";
            Destroy(_shieldVisual.GetComponent<Collider>());
            _shieldVisual.transform.localScale = new Vector3(3f, 3f, 1f);

            Material mat = BerserkFuryModel.SafeMaterial(null, new Color(0.2f, 0.8f, 1f), new Color(0f, 0.7f, 1f), 0.4f);
            mat.name = "matBerserkFuryEShield";

            var mr = _shieldVisual.GetComponent<MeshRenderer>();
            mr.material = mat;
            _shieldVisual.SetActive(false);
        }
    }
}
