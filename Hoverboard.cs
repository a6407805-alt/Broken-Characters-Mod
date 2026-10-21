using System.Collections.Generic;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    /// <summary>
    /// The Pharaoh's hoverboard. Double-tap jump to whip it out (or put it away), and ride it with skateboard
    /// momentum: speed builds up and carries through turns, you carve instead of strafing, hills speed you up,
    /// you coast when you let go, and slamming into a wall costs you. Jump to ollie; the longer you're in the
    /// air the bigger the trick, and landing it gives you a speed boost (chain tricks for a combo).
    /// Put away automatically inside duels.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class DuelistHover : MonoBehaviour
    {
        public static BuffDef BoardBuff;            // hidden, synced: lets every player see who's on a board

        public bool Hovering;                       // the local rider's choice (authority)
        public const float RideHeight = 0.55f;

        // what the board visual and model need to know
        public float Speed;
        public Vector3 Heading = Vector3.forward;
        public float Bank, AirTime, FlipAngle, SpinYaw;
        public bool Riding;                         // touching the ground (not in the air)
        public string LastTrick;
        public float LastTrickTime = -10f;
        public int Combo;

        private CharacterBody _body;
        private float _lastTap = -10f, _airControl = -1f, _lastGroundTime;
        private bool _prevJump, _ollie, _trickDone;
        private Animator _anim;
        private HashSet<string> _params;
        private float _spinTarget;

        public static void Register()
        {
            BoardBuff = ScriptableObject.CreateInstance<BuffDef>();
            BoardBuff.name = "bdDuelistHoverboard";
            BoardBuff.isHidden = true;
            BoardBuff.canStack = false;
            BoardBuff.buffColor = new Color(1f, 0.82f, 0.35f);
            ContentAddition.AddBuffDef(BoardBuff);
        }

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            Hovering = DuelConfig.StartHovering.Value;
        }

        private bool LocalActive => Hovering && DuelConfig.Hover.Value && _body && !DuelDimensionState.ActiveDuelists.Contains(_body);

        /// <summary>On the board right now (true on every client).</summary>
        public bool IsHovering => _body && (_body.hasEffectiveAuthority ? LocalActive : (BoardBuff && _body.HasBuff(BoardBuff)));

        private bool _synced;
        private void SyncBuff()
        {
            bool want = LocalActive;
            if (want == _synced || !_body) return;
            _synced = want;
            DuelNet.SetBoard(_body, want);
        }

        private void Update()
        {
            if (!_body) return;
            if (_body.hasEffectiveAuthority && _body.inputBank)
            {
                bool jump = _body.inputBank.jump.down;
                if (jump && !_prevJump)
                {
                    if (Time.time - _lastTap < 0.3f)
                    {
                        Hovering = !Hovering;
                        _lastTap = -10f;
                        _ollie = false;
                        Util.PlaySound(Hovering ? "Play_huntress_shift_mini_blink" : "Play_UI_menuClick", gameObject);
                        Partners.Burst(_body.footPosition, new Color(1f, 0.82f, 0.35f), Hovering ? 3f : 1.5f);
                        if (Hovering) { Speed = Mathf.Max(Speed, FlatVel().magnitude); Heading = Flat(_body.characterDirection ? _body.characterDirection.forward : transform.forward); }
                    }
                    else
                    {
                        _lastTap = Time.time;
                        if (LocalActive && Riding) _ollie = true;
                    }
                }
                _prevJump = jump;
                SyncBuff();
            }

            // board stance: no running legs while riding (every client)
            if (IsHovering)
            {
                if (!_anim && _body.modelLocator && _body.modelLocator.modelTransform) _anim = _body.modelLocator.modelTransform.GetComponent<Animator>();
                if (_anim)
                {
                    if (_params == null)
                    {
                        _params = new HashSet<string>();
                        foreach (AnimatorControllerParameter ap in _anim.parameters) _params.Add(ap.name);
                    }
                    if (_params.Contains("isGrounded")) _anim.SetBool("isGrounded", true);
                    if (_params.Contains("isMoving")) _anim.SetBool("isMoving", false);
                    if (_params.Contains("isSprinting")) _anim.SetBool("isSprinting", false);
                    if (_params.Contains("walkSpeed")) _anim.SetFloat("walkSpeed", 0f);
                    if (_params.Contains("forwardSpeed")) _anim.SetFloat("forwardSpeed", 0f);
                    if (_params.Contains("rightSpeed")) _anim.SetFloat("rightSpeed", 0f);
                }
            }

            // remote riders: approximate heading/speed from the replicated movement for the board visual
            if (!_body.hasEffectiveAuthority && IsHovering)
            {
                Vector3 v = FlatVel();
                Speed = v.magnitude;
                if (Speed > 0.5f) Heading = Vector3.Slerp(Heading, v.normalized, Time.deltaTime * 8f);
            }

            // trick visuals: the board flips and the rider spins while in the air
            float dt = Time.deltaTime;
            if (IsHovering && !Riding && AirTime > 0.3f) FlipAngle += dt * 540f;
            else FlipAngle = Mathf.MoveTowards(FlipAngle, Mathf.Round(FlipAngle / 360f) * 360f, dt * 900f);
            SpinYaw = Mathf.MoveTowards(SpinYaw, _spinTarget, dt * 720f);
        }

        private Vector3 FlatVel()
        {
            Vector3 v = _body && _body.characterMotor ? _body.characterMotor.velocity : Vector3.zero;
            v.y = 0f;
            return v;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        private void FixedUpdate()
        {
            if (!_body || !_body.hasEffectiveAuthority || !_body.characterMotor || !_body.inputBank) return;
            CharacterMotor m = _body.characterMotor;
            if (_airControl < 0f) _airControl = m.airControl;
            if (!LocalActive)
            {
                m.airControl = _airControl;
                Riding = true;
                AirTime = 0f;
                return;
            }
            float dt = Time.fixedDeltaTime;
            m.airControl = 0f;
            if (m.isGrounded) m.Motor.ForceUnground();

            float move = _body.moveSpeed;
            float cruise = move * (_body.isSprinting ? 2.0f : 1.6f);
            float maxSpeed = move * 4.5f;

            // the ground under the board
            Vector3 foot = _body.footPosition;
            bool ground = Physics.Raycast(foot + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 0.5f + RideHeight + 0.9f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            float gap = ground ? foot.y - hit.point.y : 99f;
            Vector3 v = m.velocity;
            bool wasRiding = Riding;
            Riding = ground && gap < RideHeight + 0.35f && v.y < 3f;

            // collisions: if a wall ate our speed, the board loses it too (and bonks)
            float actual = new Vector3(v.x, 0f, v.z).magnitude;
            if (Speed > 4f && actual < Speed * 0.55f)
            {
                Speed = Mathf.Lerp(Speed, actual, 0.6f);
                if (Speed < 3f) Combo = 0;
            }

            // carving: the board turns toward where you steer, it doesn't strafe
            Vector3 input = _body.inputBank.moveVector;
            input.y = 0f;
            float push = Mathf.Min(1f, input.magnitude);
            Vector3 oldHeading = Heading;
            if (push > 0.1f)
            {
                Vector3 want = input.normalized;
                float ang = Vector3.Angle(Heading, want);
                float turn = (Riding ? Mathf.Lerp(260f, 130f, Speed / Mathf.Max(1f, maxSpeed)) : 110f) * Mathf.Deg2Rad;
                if (ang > 135f && Speed > 3f && Riding)
                    Speed = Mathf.Max(0f, Speed - move * 3f * dt);           // pulling back hard: brake (powerslide)
                else
                    Heading = Vector3.RotateTowards(Heading, want, turn * dt, 0f);
                if (Riding && Speed < cruise) Speed = Mathf.Min(cruise, Speed + move * 2.6f * push * dt);   // kick-push up to cruising speed
                if (Speed < 1f) Heading = Vector3.RotateTowards(Heading, want, 10f * dt, 0f);
            }
            Heading = Flat(Heading);
            float yawRate = Vector3.SignedAngle(oldHeading, Heading, Vector3.up) / dt;
            Bank = Mathf.Lerp(Bank, Mathf.Clamp(-yawRate * 0.12f, -28f, 28f) * Mathf.Clamp01(Speed / 6f), dt * 8f);

            // momentum: above cruising speed you keep it (slow bleed); let go and you coast
            if (Speed > cruise) Speed -= (Riding ? 1.6f : 0.4f) * dt;
            else if (push < 0.1f && Riding) Speed -= 1.4f * dt;
            // hills: downhill speeds you up, uphill slows you down
            if (Riding)
            {
                Vector3 slope = Vector3.ProjectOnPlane(Physics.gravity, hit.normal);
                Speed += Vector3.Dot(slope, Heading) * 0.9f * dt;
            }
            Speed = Mathf.Clamp(Speed, 0f, maxSpeed);

            // height: ride a little above the ground; ollies and gravity in the air
            float vy;
            if (_ollie && Riding)
            {
                _ollie = false;
                vy = 12f + Speed * 0.22f;
                Riding = false;
                AirTime = 0f;
                _trickDone = false;
                if (Time.time - _lastGroundTime > 1.6f) Combo = 0;
                Util.PlaySound("Play_huntress_shift_mini_blink", gameObject);
                Partners.Burst(foot, new Color(0.35f, 0.85f, 1f), 1.5f);
            }
            else if (Riding)
            {
                float target = hit.point.y + RideHeight + Mathf.Sin(Time.time * 3f) * 0.04f;
                vy = Mathf.Clamp((target - foot.y) * 12f, -14f, 14f);
            }
            else vy = v.y + Physics.gravity.y * 1.15f * dt;

            if (!Riding) AirTime += dt;
            else if (!wasRiding) Land();
            if (Riding) _lastGroundTime = Time.time;

            m.velocity = Heading * Speed + Vector3.up * vy;
        }

        private static readonly string[] Tricks =
        {
            "OLLIE", "KICKFLIP", "HEELFLIP", "VARIAL FLIP", "360 FLIP", "SPHINX SPIN 540", "PHARAOH FLIP 720", "PYRAMID SCHEME 900", "MILLENNIUM 1080"
        };

        /// <summary>Touchdown: big air pays out a trick and a speed boost; tricks chained quickly build a combo.</summary>
        private void Land()
        {
            float air = AirTime;
            AirTime = 0f;
            if (air < 0.35f || _trickDone) return;
            _trickDone = true;
            int tier = Mathf.Clamp(Mathf.FloorToInt((air - 0.35f) / 0.3f) + 1, 1, Tricks.Length - 1);
            if (air < 0.5f) tier = 0;
            Combo++;
            float boost = (2f + tier * 1.6f) * (1f + 0.25f * (Combo - 1));
            Speed = Mathf.Min(Speed + boost, _body.moveSpeed * 4.5f);
            LastTrick = Tricks[tier];
            LastTrickTime = Time.time;
            if (tier >= 3) _spinTarget += 360f * (tier >= 6 ? 2f : 1f);
            Util.PlaySound(tier >= 4 ? "Play_UI_item_pickup" : "Play_UI_menuClick", gameObject);
            Partners.Burst(_body.footPosition, new Color(1f, 0.82f, 0.35f), 2f + tier * 0.4f);
        }

        private void OnDisable()
        {
            if (_body && _body.characterMotor && _airControl >= 0f) _body.characterMotor.airControl = _airControl;
            if (_synced && _body) DuelNet.SetBoard(_body, false);
            _synced = false;
        }
    }

    /// <summary>
    /// The model side of riding: banks into carves, leans with speed, spins on big tricks and bobs with the board.
    /// (Walking: no lift, the feet stay on the ground.)
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class DuelistLevitate : MonoBehaviour
    {
        private Vector3 _base, _lastSet;
        private Quaternion _baseRot, _lastRot;
        private bool _has;
        private Vector3 _lean;
        private CharacterBody _body;
        private DuelistHover _hover;

        private void Start()
        {
            CharacterModel cm = GetComponent<CharacterModel>();
            _body = cm ? cm.body : null;
            _hover = _body ? _body.GetComponent<DuelistHover>() : null;
        }

        private void LateUpdate()
        {
            Transform t = transform;
            if (!_has || (t.localPosition - _lastSet).sqrMagnitude > 0.000001f) _base = t.localPosition;
            if (!_has || Quaternion.Angle(t.localRotation, _lastRot) > 0.01f) _baseRot = t.localRotation;
            _has = true;

            bool riding = _hover && _hover.IsHovering;
            _lastSet = _base;
            t.localPosition = _lastSet;

            Vector3 want = Vector3.zero;
            float spin = 0f;
            if (riding && _body && _body.characterMotor && t.parent)
            {
                Vector3 v = _body.characterMotor.velocity; v.y = 0f;
                Vector3 local = t.parent.InverseTransformDirection(v) / Mathf.Max(1f, _body.moveSpeed);
                // crouch-lean forward with speed, bank into the carve
                want = new Vector3(Mathf.Clamp(local.z, -1f, 3f) * 6f, 0f, -_hover.Bank * 0.8f - Mathf.Clamp(local.x, -1f, 1f) * 6f);
                spin = _hover.SpinYaw % 360f;
            }
            _lean = Vector3.Lerp(_lean, want, Time.deltaTime * 6f);
            _lastRot = _baseRot * Quaternion.Euler(_lean.x, spin, _lean.z);
            t.localRotation = _lastRot;
        }
    }

    /// <summary>
    /// The board itself: a sleek golden Extreme-Gear style deck with lapis inlays, glowing cyan underside and twin
    /// thrusters that stream light. Follows the rider on every client.
    /// </summary>
    public class DuelistBoardVisual : MonoBehaviour
    {
        private CharacterBody _body;
        private DuelistHover _hover;
        private GameObject _board;
        private Transform _deck;
        private ParticleSystem[] _jets;
        private Light _glow;
        private float _show;
        private Vector3 _pos;
        private static Mesh _deckMesh, _stripeMesh, _underMesh, _nozzleMesh, _finMesh;
        private static Material _gold, _lapis, _cyan, _dark, _jetMat;

        private void Start()
        {
            _body = GetComponent<CharacterBody>();
            _hover = GetComponent<DuelistHover>();
        }

        private void LateUpdate()
        {
            if (!_body || !_hover) return;
            bool on = _hover.IsHovering && _body.healthComponent && _body.healthComponent.alive;
            _show = Mathf.MoveTowards(_show, on ? 1f : 0f, Time.deltaTime * (on ? 6f : 4f));
            if (_show <= 0f)
            {
                if (_board && _board.activeSelf) _board.SetActive(false);
                return;
            }
            if (!_board) Build();
            if (!_board.activeSelf)
            {
                _board.SetActive(true);
                _pos = _body.footPosition;
            }

            float scale = Mathf.Max(0.6f, _body.radius / 0.5f) * 1.0f;
            Vector3 target = _body.footPosition - Vector3.up * 0.08f * scale;
            _pos = Vector3.Lerp(_pos, target, 1f - Mathf.Exp(-30f * Time.deltaTime));
            _board.transform.position = _pos;
            Quaternion rot = Quaternion.LookRotation(_hover.Heading, Vector3.up) * Quaternion.Euler(0f, 0f, _hover.Bank);
            // ground pitch: follow the slope under the board
            if (Physics.Raycast(_pos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2.5f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 fwd = Vector3.ProjectOnPlane(_hover.Heading, hit.normal).normalized;
                if (fwd.sqrMagnitude > 0.01f) rot = Quaternion.LookRotation(fwd, hit.normal) * Quaternion.Euler(0f, 0f, _hover.Bank);
            }
            _board.transform.rotation = rot;
            _board.transform.localScale = Vector3.one * scale * Mathf.SmoothStep(0f, 1f, _show);
            if (_deck) _deck.localRotation = Quaternion.Euler(0f, 0f, _hover.FlipAngle);     // kickflips

            float thrust = Mathf.Clamp01(_hover.Speed / Mathf.Max(1f, _body.moveSpeed * 2f));
            if (_jets != null)
                foreach (ParticleSystem ps in _jets)
                {
                    ParticleSystem.EmissionModule em = ps.emission;
                    em.rateOverTime = 15f + 70f * thrust;
                    ParticleSystem.MainModule main = ps.main;
                    main.startSpeed = 1.5f + 6f * thrust;
                }
            if (_glow) _glow.intensity = (1.4f + 1.2f * thrust + Mathf.Sin(Time.time * 9f) * 0.2f) * _show;
        }

        // ------------------------------------------------------------ building the board

        private void Build()
        {
            if (_deckMesh == null) MakeMeshes();
            _board = new GameObject("DuelistHoverboard");
            _deck = new GameObject("Deck").transform;
            _deck.SetParent(_board.transform, false);
            Part("Body", _deckMesh, _gold);
            Part("Stripe", _stripeMesh, _lapis);
            Part("Underglow", _underMesh, _cyan);
            Part("Fins", _finMesh, _gold);
            _jets = new ParticleSystem[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.11f : 0.11f;
                GameObject n = Part("Nozzle" + i, _nozzleMesh, _dark);
                n.transform.localPosition = new Vector3(x, 0.0f, -0.78f);
                _jets[i] = Jet(n.transform);
            }
            GameObject l = new GameObject("Glow");
            l.transform.SetParent(_board.transform, false);
            l.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            _glow = l.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.color = new Color(0.35f, 0.85f, 1f);
            _glow.range = 3.5f;
        }

        private GameObject Part(string name, Mesh mesh, Material mat)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(_deck, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return g;
        }

        private ParticleSystem Jet(Transform at)
        {
            GameObject go = new GameObject("Jet");
            go.transform.SetParent(at, false);
            go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(0.45f, 0.9f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 150;
            ParticleSystem.ShapeModule sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 8f;
            sh.radius = 0.03f;
            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.8f, 0.97f, 1f), 0f), new GradientColorKey(new Color(1f, 0.8f, 0.3f), 1f) },
                      new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            ParticleSystemRenderer pr = go.GetComponent<ParticleSystemRenderer>();
            if (_jetMat == null)
            {
                Shader s = DuelistVisuals.UnlitShader;
                _jetMat = s ? new Material(s) : DuelistVisuals.MakeMaterial(Color.white, new Color(0.4f, 0.9f, 1f));
                _jetMat.mainTexture = DuelVenom.SoftDot;
            }
            pr.sharedMaterial = _jetMat;
            ps.Play();
            return ps;
        }

        private static void MakeMaterials()
        {
            _gold = DuelistVisuals.MakeMaterial(new Color(0.95f, 0.72f, 0.22f), new Color(0.45f, 0.3f, 0.05f));
            _lapis = DuelistVisuals.MakeMaterial(new Color(0.1f, 0.22f, 0.7f), new Color(0.05f, 0.12f, 0.45f));
            _cyan = DuelistVisuals.MakeMaterial(new Color(0.4f, 0.9f, 1f), new Color(0.35f, 0.9f, 1f) * 2.5f);
            _dark = DuelistVisuals.MakeMaterial(new Color(0.18f, 0.14f, 0.1f), new Color(0.3f, 0.8f, 1f));
        }

        /// <summary>Half-width of the deck along its length (z from -0.8 tail to 0.9 nose): a pointed nose, squared tail.</summary>
        private static float HalfWidth(float z)
        {
            float u = Mathf.InverseLerp(-0.8f, 0.9f, z);
            float nose = Mathf.Pow(Mathf.Clamp01((1f - u) / 0.45f), 0.6f);       // narrows to a point at the front
            float tail = 0.82f + 0.18f * Mathf.Sin(Mathf.Clamp01(u / 0.25f) * Mathf.PI * 0.5f);
            return 0.26f * Mathf.Min(nose, tail);
        }

        private static void MakeMeshes()
        {
            MakeMaterials();
            _deckMesh = Loft(z => HalfWidth(z), z => 0.045f + 0.02f * Mathf.Sin(Mathf.InverseLerp(-0.8f, 0.9f, z) * Mathf.PI), 0f, 24, 14);
            // a lapis inlay running down the deck, an eye-of-the-sun stripe
            _stripeMesh = Loft(z => HalfWidth(z) * 0.3f, z => 0.012f, 0.055f, 24, 8, -0.6f, 0.7f);
            _underMesh = Loft(z => HalfWidth(z) * 0.75f, z => 0.01f, -0.055f, 24, 8, -0.7f, 0.75f);
            _nozzleMesh = Cylinder(0.06f, 0.16f, 12);
            _finMesh = Fins();
        }

        private delegate float Prof(float z);

        /// <summary>A lofted lens-shaped slab: elliptical cross-sections along z.</summary>
        private static Mesh Loft(Prof halfW, Prof halfH, float yOff, int rings, int seg, float z0 = -0.8f, float z1 = 0.9f)
        {
            List<Vector3> v = new List<Vector3>();
            List<int> tri = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float z = Mathf.Lerp(z0, z1, i / (float)rings);
                float w = Mathf.Max(0.004f, halfW(z)), h = halfH(z);
                for (int k = 0; k < seg; k++)
                {
                    float a = k / (float)seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * w, yOff + Mathf.Sin(a) * h, z));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int k = 0; k < seg; k++)
                {
                    int a = i * seg + k, b = i * seg + (k + 1) % seg, c = a + seg, d = b + seg;
                    tri.AddRange(new[] { a, c, b, b, c, d });
                }
            // caps
            int tailC = v.Count; v.Add(new Vector3(0f, yOff, z0));
            int noseC = v.Count; v.Add(new Vector3(0f, yOff, z1));
            for (int k = 0; k < seg; k++)
            {
                tri.AddRange(new[] { tailC, (k + 1) % seg, k });
                int o = rings * seg;
                tri.AddRange(new[] { noseC, o + k, o + (k + 1) % seg });
            }
            Mesh m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh Cylinder(float r, float len, int seg)
        {
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            for (int i = 0; i < 2; i++)
                for (int k = 0; k < seg; k++)
                {
                    float a = k / (float)seg * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(a) * r * (i == 0 ? 1.15f : 1f), Mathf.Sin(a) * r * (i == 0 ? 1.15f : 1f), i == 0 ? -len * 0.5f : len * 0.5f));
                }
            for (int k = 0; k < seg; k++)
            {
                int a = k, b = (k + 1) % seg, c = a + seg, d = b + seg;
                t.AddRange(new[] { a, b, c, b, d, c });
            }
            Mesh m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            return m;
        }

        private static Mesh Fins()
        {
            // two swept tail fins, like the wings on a scarab
            List<Vector3> v = new List<Vector3>();
            List<int> t = new List<int>();
            foreach (float sx in new[] { -1f, 1f })
            {
                int o = v.Count;
                v.Add(new Vector3(0.17f * sx, 0.02f, -0.45f));
                v.Add(new Vector3(0.36f * sx, 0.06f, -0.82f));
                v.Add(new Vector3(0.2f * sx, 0.02f, -0.75f));
                v.Add(new Vector3(0.17f * sx, -0.01f, -0.45f));
                v.Add(new Vector3(0.36f * sx, 0.03f, -0.82f));
                v.Add(new Vector3(0.2f * sx, -0.01f, -0.75f));
                if (sx > 0) t.AddRange(new[] { o, o + 2, o + 1, o + 3, o + 4, o + 5 });
                else t.AddRange(new[] { o, o + 1, o + 2, o + 3, o + 5, o + 4 });
            }
            Mesh m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            return m;
        }
    }

    /// <summary>Tony Hawk style trick callouts for the local rider.</summary>
    public class DuelistTrickHud : MonoBehaviour
    {
        private DuelistHover _hover;
        private GUIStyle _big, _small;

        private void Start() => _hover = GetComponent<DuelistHover>();

        private void OnGUI()
        {
            if (!_hover || !_hover.isActiveAndEnabled) return;
            CharacterBody b = GetComponent<CharacterBody>();
            if (!b || !b.hasEffectiveAuthority) return;
            float age = Time.time - _hover.LastTrickTime;
            if (age > 1.8f || string.IsNullOrEmpty(_hover.LastTrick)) return;
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.BoldAndItalic, alignment = TextAnchor.MiddleCenter, richText = true };
                _small = new GUIStyle(_big) { fontSize = 26 };
            }
            float s = Screen.height / 1080f;
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            float w = Screen.width / s;
            float pop = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f;
            float alpha = Mathf.Clamp01((1.8f - age) / 0.4f);
            float y = 700f - age * 20f;
            Rect r = new Rect(w * 0.5f - 500f, y, 1000f, 60f);
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), r.center);
            string trick = $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(1f, 0.85f, 0.35f, alpha))}>{_hover.LastTrick}!</color>";
            Outlined(r, trick, _big, alpha);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
            if (_hover.Combo > 1)
                Outlined(new Rect(w * 0.5f - 300f, y + 55f, 600f, 40f), $"<color=#{ColorUtility.ToHtmlStringRGBA(new Color(0.45f, 0.9f, 1f, alpha))}>COMBO x{_hover.Combo}  ·  SPEED BOOST</color>", _small, alpha);
            GUI.matrix = old;
        }

        private static void Outlined(Rect r, string text, GUIStyle st, float alpha)
        {
            string plain = System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", "");
            Color c = st.normal.textColor;
            st.normal.textColor = new Color(0f, 0f, 0f, 0.85f * alpha);
            for (int dx = -2; dx <= 2; dx += 2)
                for (int dy = -2; dy <= 2; dy += 2)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx, r.y + dy, r.width, r.height), plain, st);
            st.normal.textColor = c;
            GUI.Label(r, text, st);
        }
    }
}
