using EntityStates;
using RoR2;
using UnityEngine;

namespace Surveyor
{
    // v0.7.0: the Surveyor walks like she is on the moon (low gravity, no fall damage), Shift is a thruster Moon Leap, and
    // Space-Engineers-style blue thruster flames fire from the backpack and boots (everyone sees them).

    /// <summary>Low gravity on her own client: cancels part of gravity while airborne and caps the fall speed.</summary>
    public class SvrMoonGravity : MonoBehaviour
    {
        private CharacterBody _body;
        private CharacterMotor _motor;

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _motor = GetComponent<CharacterMotor>();
        }

        private void FixedUpdate()
        {
            if (!_body || !_motor || !_body.hasEffectiveAuthority || _motor.isGrounded) return;
            float dt = Time.fixedDeltaTime;
            float g = Mathf.Clamp(SvrPlugin.MoonGravity.Value, 0.05f, 1f);
            Vector3 v = _motor.velocity;
            v.y += -Physics.gravity.y * (1f - g) * dt;
            float cap = SvrPlugin.MoonMaxFall.Value;
            if (v.y < -cap) v.y = Mathf.MoveTowards(v.y, -cap, 40f * dt);
            _motor.velocity = v;
        }
    }

    /// <summary>
    /// Shift: Thruster Boost. Every thruster fires at once: a sudden hard shove where you are moving (or looking), held
    /// for a split second so it really kicks, then the speed bleeds off. Works in the air.
    /// </summary>
    public class SvrMoonLeapState : BaseSkillState
    {
        private const float PushTime = 0.22f;
        private Vector3 _dir;

        public override void OnEnter()
        {
            base.OnEnter();
            SvrThrusters.Burst(gameObject, 1.4f);                 // runs on every client: everyone sees the burn
            Util.PlaySound("Play_moonBrother_dash", gameObject);
            Util.PlaySound("Play_lunar_golem_attack1_launch", gameObject);
            Util.PlaySound("Play_env_geyser_launch", gameObject);
            if (!isAuthority || !characterMotor) return;
            Vector3 aim = GetAimRay().direction;
            Vector3 dir = inputBank ? inputBank.moveVector : Vector3.zero;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) { dir = aim; dir.y = 0f; }
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            dir.Normalize();
            // looking up or down while boosting tilts the burst a little
            dir = (dir + Vector3.up * Mathf.Clamp(aim.y, -0.35f, 0.5f)).normalized;
            _dir = dir;
            try { characterMotor.Motor.ForceUnground(); } catch { }
            characterMotor.velocity = dir * SvrPlugin.LeapForward.Value + Vector3.up * SvrPlugin.LeapUp.Value;
            AddRecoil(1.5f, 2.5f, -0.5f, 0.5f);                   // the camera snaps back with the kick
            if (characterBody) characterBody.isSprinting = true;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            if (fixedAge < PushTime && characterMotor)
            {
                // keep the thrust on for a moment so air drag and ground friction can't eat the kick
                Vector3 v = characterMotor.velocity;
                Vector3 want = _dir * SvrPlugin.LeapForward.Value;
                Vector3 flat = Vector3.ProjectOnPlane(v, Vector3.up);
                Vector3 wantFlat = Vector3.ProjectOnPlane(want, Vector3.up);
                if (Vector3.Dot(flat, wantFlat.normalized) < wantFlat.magnitude)
                    flat = Vector3.MoveTowards(flat, wantFlat, 200f * Time.fixedDeltaTime);
                characterMotor.velocity = new Vector3(flat.x, Mathf.Max(v.y, want.y + SvrPlugin.LeapUp.Value * 0.5f), flat.z);
            }
            if (fixedAge >= 0.3f) outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority() { return InterruptPriority.PrioritySkill; }
    }

    /// <summary>
    /// Blue thruster flames (Space Engineers style: hot white core, blue plume, flicker) on the backpack and boots, plus a
    /// blue glow light. They burn hard on Moon Leap, softly while drifting down, and puff on landing. Visible to all.
    /// </summary>
    public class SvrThrusters : MonoBehaviour
    {
        private Transform _model, _chest, _footL, _footR;
        private CharacterBody _body;
        private CharacterMotor _motor;
        private readonly Transform[] _outer = new Transform[4], _inner = new Transform[4];
        private Light _light;
        private float _burst, _thrust;
        private Vector3 _lastPos;
        private float _vy;
        private bool _built, _wasAir;
        private static Mesh _cone;
        private static Material _outerMat, _innerMat;

        public static void Burst(GameObject body, float amount)
        {
            if (!body) return;
            SvrThrusters t = body.GetComponent<SvrThrusters>();
            if (t) t._burst = Mathf.Max(t._burst, amount);
        }

        private void Start()
        {
            _body = GetComponent<CharacterBody>();
            _motor = GetComponent<CharacterMotor>();
            _lastPos = transform.position;
        }

        private bool Build()
        {
            _model = SvrPlugin.Model(gameObject);
            if (!_model) return false;
            foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "chest") _chest = t;
                else if (n == "foot.l") _footL = t;
                else if (n == "foot.r") _footR = t;
            }
            if (!_chest) return false;
            if (!_cone) _cone = MakeCone(14);
            if (!_outerMat) _outerMat = Glow(new Color(0.25f, 0.6f, 1f), 6f);
            if (!_innerMat) _innerMat = Glow(new Color(0.85f, 0.95f, 1f), 10f);
            for (int i = 0; i < 4; i++)
            {
                _outer[i] = Flame("SvrThrusterPlume" + i, _outerMat);
                _inner[i] = Flame("SvrThrusterCore" + i, _innerMat);
            }
            GameObject lg = new GameObject("SvrThrusterLight");
            lg.transform.SetParent(_model, false);
            _light = lg.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = new Color(0.35f, 0.65f, 1f);
            _light.range = 5f;
            _light.intensity = 0f;
            _built = true;
            return true;
        }

        private Transform Flame(string name, Material m)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(_model, false);
            g.AddComponent<MeshFilter>().sharedMesh = _cone;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            g.SetActive(false);
            return g.transform;
        }

        /// <summary>
        /// Flame material: a plain unlit, see-through colour (forward pass). The old deferred material with a very high
        /// glow drew wrong over the sky and blew out the bloom, which smeared across the screen and the HUD.
        /// </summary>
        private static Material Glow(Color c, float power)
        {
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (!sh) return null;
            Material m = new Material(sh) { name = "SvrThrusterFlame" };
            float a = power > 8f ? 0.9f : 0.55f;
            m.color = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), a);
            m.renderQueue = 3100;
            return m;
        }

        /// <summary>A cone: base radius 1 at z = 0, tip at z = 1.</summary>
        private static Mesh MakeCone(int seg)
        {
            Vector3[] v = new Vector3[seg + 2];
            int[] tri = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                v[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
            v[seg] = new Vector3(0f, 0f, 1f);
            v[seg + 1] = Vector3.zero;
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                tri[i * 6] = i; tri[i * 6 + 1] = seg; tri[i * 6 + 2] = j;
                tri[i * 6 + 3] = j; tri[i * 6 + 4] = seg + 1; tri[i * 6 + 5] = i;
            }
            Mesh m = new Mesh { name = "SvrThrusterCone", vertices = v, triangles = tri };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || !_body) return;
            if (!_built && !Build()) return;
            if (!_model) { _built = false; return; }

            // vertical speed from the motion itself, so remote players' flames match what everyone sees
            Vector3 p = transform.position;
            _vy = Mathf.Lerp(_vy, (p.y - _lastPos.y) / dt, 1f - Mathf.Exp(-10f * dt));
            _lastPos = p;
            bool grounded = _motor ? _motor.isGrounded : Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, 0.7f, LayerIndex.world.mask);
            if (_wasAir && grounded && _vy < -4f) _burst = Mathf.Max(_burst, 0.45f);   // landing puff
            _wasAir = !grounded;

            float want = 0f;
            if (!grounded)
            {
                if (_vy > 2f) want = 0.45f;                 // climbing
                else if (_vy < -1.5f) want = 0.3f;          // dampeners slowing the fall
                else want = 0.18f;
            }
            _burst = Mathf.MoveTowards(_burst, 0f, dt * 1.3f);
            float target = Mathf.Max(want, _burst);
            _thrust = Mathf.MoveTowards(_thrust, target, dt * 6f);

            bool on = _thrust > 0.03f;
            float s = Mathf.Abs(_model.lossyScale.x);
            Vector3 up = _model.up, fwd = _model.forward, right = _model.right;
            for (int i = 0; i < 4; i++)
            {
                Transform o = _outer[i], c = _inner[i];
                if (!o || !c) continue;
                Transform anchor = i < 2 ? _chest : (i == 2 ? _footL : _footR);
                if (!on || !anchor || !_outerMat) { o.gameObject.SetActive(false); c.gameObject.SetActive(false); continue; }
                Vector3 at, dir;
                if (i < 2)
                {
                    at = anchor.position - fwd * (0.4f * s) - up * (0.28f * s) + right * ((i == 0 ? -0.13f : 0.13f) * s);
                    dir = (-up * 0.85f - fwd * 0.5f).normalized;
                }
                else
                {
                    at = anchor.position - up * (0.06f * s);
                    dir = -up;
                }
                float flick = 0.8f + 0.4f * Mathf.PerlinNoise(Time.time * 22f, i * 3.1f);
                float len = (i < 2 ? 0.95f : 0.6f) * _thrust * flick * s;
                float rad = (i < 2 ? 0.075f : 0.06f) * s * (0.7f + 0.3f * _thrust);
                o.gameObject.SetActive(true);
                c.gameObject.SetActive(true);
                o.SetPositionAndRotation(at, Quaternion.LookRotation(dir, fwd));
                o.localScale = new Vector3(rad, rad, len) / Mathf.Max(0.01f, s);
                c.SetPositionAndRotation(at + dir * 0.01f, o.rotation);
                c.localScale = new Vector3(rad * 0.5f, rad * 0.5f, len * 0.55f) / Mathf.Max(0.01f, s);
            }
            if (_light)
            {
                _light.transform.position = _chest ? _chest.position - fwd * (0.45f * s) - up * (0.5f * s) : transform.position;
                _light.intensity = on ? 1.6f * _thrust * (0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 15f, 3.3f)) : 0f;
            }
        }
    }
}
