using System;
using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using RoR2;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Surveyor
{
    // ================================================================ senses the Demon can steal (local to the Surveyor's client)

    public static class SvrSenses
    {
        public static bool Hearing, Sight, Touch, Balance;   // true = stolen
        public static int Version;                           // bumped on every change so the body recalculates its stats

        public static int Lost { get { return (Hearing ? 1 : 0) + (Sight ? 1 : 0) + (Touch ? 1 : 0) + (Balance ? 1 : 0); } }
        public static float SightMult { get { return Sight ? SvrPlugin.SightLossMult.Value : 1f; } }
        public static float SpoolMult { get { return Balance ? SvrPlugin.BalanceSpool.Value : 1f; } }
        public static float SpreadMult { get { return (Balance ? 1.8f : 1f) * (Touch ? 1.3f : 1f); } }

        /// <summary>Takes one sense that is still there. Returns its name, or null if she has nothing left to lose.</summary>
        public static string TakeRandom()
        {
            List<string> left = new List<string>();
            if (!Hearing) left.Add("HEARING");
            if (!Sight) left.Add("SIGHT");
            if (!Touch) left.Add("TOUCH");
            if (!Balance) left.Add("BALANCE");
            if (left.Count == 0) return null;
            string pick = left[Random.Range(0, left.Count)];
            if (pick == "HEARING") Hearing = true;
            else if (pick == "SIGHT") Sight = true;
            else if (pick == "TOUCH") Touch = true;
            else Balance = true;
            Version++;
            if (Hearing) SvrAudio.Muffle(true);
            return pick;
        }

        public static void ResetStage()
        {
            bool any = Hearing || Sight || Touch || Balance;
            Hearing = false; Sight = false; Touch = false; Balance = false;
            if (any) Version++;
            SvrAudio.Muffle(false);
        }
    }

    /// <summary>Lowers the game's sound through the same Wwise parameter the Jukebox uses for music.</summary>
    public static class SvrAudio
    {
        private static MethodInfo _set;
        private static bool _looked;
        private static bool _applied;

        private static MethodInfo SetRtpc()
        {
            if (_looked) return _set;
            _looked = true;
            try
            {
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = a.GetType("AkSoundEngine", false);
                    if (t == null) continue;
                    _set = t.GetMethod("SetRTPCValue", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(float) }, null);
                    if (_set != null) break;
                }
            }
            catch (Exception e) { SvrPlugin.Log?.LogWarning("Surveyor: no Wwise parameter found: " + e.Message); }
            return _set;
        }

        private static float PlayerVolume()
        {
            try
            {
                object console = RoR2.Console.instance;
                MethodInfo find = console?.GetType().GetMethod("FindConVar", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                object conVar = find?.Invoke(console, new object[] { "volume_sfx" });
                string text = conVar?.GetType().GetMethod("GetString")?.Invoke(conVar, null) as string;
                if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            }
            catch { }
            return 50f;
        }

        public static void Muffle(bool on)
        {
            try
            {
                if (!on && !_applied) return;
                float v = PlayerVolume() * (on ? SvrPlugin.HearingLossVolume.Value : 1f);
                MethodInfo m = SetRtpc();
                if (m != null) m.Invoke(null, new object[] { SvrPlugin.HearingRtpc.Value, v });
                _applied = on;
            }
            catch (Exception e) { SvrPlugin.Log?.LogWarning("Surveyor: hearing change failed: " + e.Message); }
        }
    }

    // ================================================================ the Shimmer Demon (a client-side stalker: no body, no health, only she sees it)

    public class SvrDemon
    {
        public enum Mode { Dormant, Prowl, Stalk, Hunt, Stagger, Gone }

        public Mode State = Mode.Dormant;
        public Vector3 Pos;
        public float Yaw;

        private float _stageTime, _modeTimer, _flank, _stagger, _chain, _chainT, _goneTimer;
        private float _phase;
        private Vector3 _center;

        // Animation state: the gait clock, how fast it is really moving, how long it has been staggered.
        private float _gait, _speedNow, _stagT, _lungeT;
        private Vector3 _lastPos;
        private static readonly Vector3[] Sph = BuildSphere();

        public bool Active { get { return State != Mode.Dormant && State != Mode.Gone; } }
        public Vector3 Center { get { return _center; } }

        // ---------------------------------------------------------------- the body: a rigged, filled horned humanoid made of points

        private static Vector3[] BuildSphere()
        {
            System.Random rnd = new System.Random(7);
            Vector3[] a = new Vector3[160];
            for (int i = 0; i < a.Length; i++)
            {
                Vector3 v = new Vector3((float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1));
                a[i] = v.sqrMagnitude < 0.0001f ? Vector3.up : v.normalized;
            }
            return a;
        }

        /// <summary>Direction pointing down from a joint, swung forward by deg degrees (180 = straight up).</summary>
        private static Vector3 Down(float deg, float outward)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector3(outward, -Mathf.Cos(r), Mathf.Sin(r)).normalized;
        }

        /// <summary>A filled tube of points from a to b (a spiral around the axis, so it reads as a solid limb, not a line).</summary>
        private static void Tube(List<Vector3> into, Vector3 a, Vector3 b, float r0, float r1, int n, float twist)
        {
            Vector3 d = b - a;
            Vector3 dn = d.sqrMagnitude < 0.0001f ? Vector3.up : d.normalized;
            Vector3 p1 = Vector3.Cross(dn, Vector3.right);
            if (p1.sqrMagnitude < 0.01f) p1 = Vector3.Cross(dn, Vector3.forward);
            p1.Normalize();
            Vector3 p2 = Vector3.Cross(dn, p1);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 0.5f) / n;
                float ang = i * 2.4f + twist;
                float r = Mathf.Lerp(r0, r1, t);
                into.Add(a + d * t + (p1 * Mathf.Cos(ang) + p2 * Mathf.Sin(ang)) * r);
            }
        }

        private readonly List<Vector3> _local = new List<Vector3>(512);

        /// <summary>Poses the rig for this moment: walking, prowling, galloping when it hunts, flailing when staggered.</summary>
        private void BuildPose(float t)
        {
            List<Vector3> p = _local;
            p.Clear();
            bool hunt = State == Mode.Hunt, stag = State == Mode.Stagger, prowl = State == Mode.Prowl;
            float run = Mathf.Clamp01(_speedNow / 4f);                       // 0 standing, 1 moving
            float amp = stag ? 8f : (hunt ? 58f : (prowl ? 24f : 34f)) * run;
            float armAmp = stag ? 0f : (hunt ? 62f : 18f) * run;
            float lean = hunt ? 38f : (prowl ? 16f : 24f);
            float bob = (hunt ? 0.13f : 0.05f) * run * Mathf.Abs(Mathf.Sin(_gait)) + 0.015f * Mathf.Sin(t * 1.3f);
            float headPitch = lean * 0.4f + Mathf.Sin(t * 0.9f) * 6f;
            float twist = Mathf.Sin(_gait) * (hunt ? 16f : 9f) * run;
            float armHang = 8f;
            float kneeBase = hunt ? 28f : 16f;

            if (stag)
            {
                float k = Mathf.Clamp01(_stagT / 0.35f);
                lean = Mathf.Lerp(lean, -28f, k) + Mathf.Sin(t * 26f) * 7f;
                headPitch = -50f + Mathf.Sin(t * 31f) * 14f;
                armHang = 150f + Mathf.Sin(t * 22f) * 25f;
                kneeBase = 50f;
                twist = Mathf.Sin(t * 19f) * 22f;
                bob = -0.12f;
            }
            else if (_lungeT > 0f)
            {
                armHang = 70f;
                lean += 14f;
            }

            Vector3 pelvis = new Vector3(0f, 1.02f + bob, 0f);
            Vector3 chest = pelvis + new Vector3(0f, Mathf.Cos(lean * Mathf.Deg2Rad), Mathf.Sin(lean * Mathf.Deg2Rad)) * 0.95f;
            Quaternion tw = Quaternion.Euler(0f, twist, 0f);

            // torso: a fat tube (ribcage wider than the waist) plus a hump of spine
            Tube(p, pelvis, chest, 0.30f, 0.44f, 120, 0f);
            Tube(p, pelvis + new Vector3(0f, 0.2f, -0.12f), chest + new Vector3(0f, 0.1f, -0.2f), 0.1f, 0.14f, 14, 1f);

            // head, neck and horns
            Vector3 headDir = new Vector3(0f, Mathf.Cos(headPitch * Mathf.Deg2Rad), Mathf.Sin(headPitch * Mathf.Deg2Rad));
            Vector3 head = chest + headDir * 0.42f + new Vector3(Mathf.Sin(t * 0.7f) * 0.05f, 0f, 0f);
            Tube(p, chest, head, 0.16f, 0.12f, 12, 0f);
            for (int i = 0; i < 44; i++) p.Add(head + Sph[i] * 0.27f);
            Quaternion hq = Quaternion.Euler(headPitch, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
                for (int s = 0; s < 12; s++)
                {
                    float u = s / 11f;
                    p.Add(head + hq * new Vector3(side * (0.2f + 0.3f * Mathf.Sin(u * 2.2f)), 0.12f + u * 0.62f, 0.02f - u * 0.1f));
                }
            p.Add(head + hq * new Vector3(-0.1f, 0.04f, 0.24f));            // eyes
            p.Add(head + hq * new Vector3(0.1f, 0.04f, 0.24f));

            // arms, swinging against the legs; the hunt throws them forward
            for (int side = -1; side <= 1; side += 2)
            {
                float ph = _gait + (side > 0 ? 0f : Mathf.PI);
                Vector3 sh = chest + tw * new Vector3(side * 0.52f, -0.08f, 0f);
                float a = armHang - Mathf.Sin(ph) * armAmp;
                if (stag) a = armHang + side * 15f;
                Vector3 elbow = sh + Down(a, side * 0.14f) * 0.62f;
                float e = a + (hunt ? 50f : 28f) + (stag ? 20f : 0f);
                Vector3 edir = Down(e, side * 0.05f);
                Vector3 hand = elbow + edir * 0.66f;
                Tube(p, sh, elbow, 0.12f, 0.09f, 24, side);
                Tube(p, elbow, hand, 0.09f, 0.07f, 24, side);
                for (int c = 0; c < 4; c++)                                  // claws
                    p.Add(hand + edir * (0.12f + c * 0.07f) + new Vector3(side * (c - 1.5f) * 0.05f, 0f, 0f));
                for (int c = 0; c < 6; c++) p.Add(sh + Sph[60 + c + (side > 0 ? 0 : 6)] * 0.17f);   // shoulder plate
            }

            // legs: thigh, knee that bends as the foot lifts, shin, foot
            for (int side = -1; side <= 1; side += 2)
            {
                float ph = _gait + (side > 0 ? Mathf.PI : 0f);
                Vector3 hip = pelvis + new Vector3(side * 0.2f, -0.02f, 0f);
                float a = Mathf.Sin(ph) * amp;
                Vector3 knee = hip + Down(a, side * 0.02f) * 0.55f;
                float lift = Mathf.Max(0f, Mathf.Cos(ph)) * (stag ? 10f : 38f) * run;
                float b = a - kneeBase - lift;
                Vector3 foot = knee + Down(b, 0f) * 0.55f;
                Tube(p, hip, knee, 0.15f, 0.1f, 26, side);
                Tube(p, knee, foot, 0.1f, 0.07f, 26, side);
                p.Add(foot + new Vector3(side * 0.04f, 0f, 0.1f));
                p.Add(foot + new Vector3(side * 0.04f, 0f, 0.2f));
                p.Add(foot + new Vector3(side * 0.1f, 0f, 0.12f));
            }
        }

        /// <summary>Its points in world space for this frame (posed, breathing, shaking when staggered).</summary>
        public void FillWorld(List<Vector3> into, float t)
        {
            into.Clear();
            BuildPose(t);
            Quaternion q = Quaternion.Euler(0f, Yaw, 0f);
            float shake = State == Mode.Stagger ? 0.07f : 0.012f;
            for (int i = 0; i < _local.Count; i++)
            {
                Vector3 w = _local[i] * 1.15f;
                w.x += (Random.value - 0.5f) * shake;
                w.z += (Random.value - 0.5f) * shake;
                into.Add(Pos + q * w);
            }
        }

        private void Animate(float dt)
        {
            float moved = (Pos - _lastPos).magnitude / Mathf.Max(dt, 0.0001f);
            _speedNow = Mathf.Lerp(_speedNow, Mathf.Min(moved, 30f), 1f - Mathf.Exp(-8f * dt));
            _lastPos = Pos;
            _gait += dt * (2.2f + _speedNow * (State == Mode.Hunt ? 0.75f : 1.1f));
            if (State == Mode.Stagger) _stagT += dt; else _stagT = 0f;
            if (_lungeT > 0f) _lungeT -= dt;
        }

        // ---------------------------------------------------------------- queries

        public float Proximity(Vector3 p)
        {
            if (!Active) return 0f;
            float d = Vector3.Distance(p, Pos);
            return Mathf.Clamp01(1f - (d - 6f) / 39f);
        }

        public bool InVoid(Vector3 p, float r)
        {
            return Active && (p - _center).sqrMagnitude < r * r;
        }

        private bool Near(Ray aim, float maxDist)
        {
            Vector3 toC = _center - aim.origin;
            float t = Vector3.Dot(toC, aim.direction);
            if (t < 0f || t > maxDist) return false;
            return (toC - aim.direction * t).magnitude < 1.8f;
        }

        // ---------------------------------------------------------------- what pushes it back (it cannot be hurt by her)

        public void Knock(Vector3 from, float meters, float stagger)
        {
            if (!Active) return;
            Vector3 away = Pos - from;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            Pos += away.normalized * meters;
            if (stagger > 0f) { _stagger = Mathf.Max(_stagger, stagger); State = Mode.Stagger; }
        }

        public void MinigunHit(Ray aim, float spool, bool anchored)
        {
            if (!Active) return;
            float need = anchored ? Mathf.Min(0.6f, SvrPlugin.MinigunPushSpool.Value) : SvrPlugin.MinigunPushSpool.Value;
            if (spool < need || !Near(aim, 60f)) return;
            Knock(aim.origin, 0.9f, 0f);
            _chain += 1f;
            _chainT = 0.8f;
            if (_chain >= 4f) { _chain = 0f; Knock(aim.origin, 0f, SvrPlugin.MinigunStagger.Value); }
        }

        public void LanceHit(Ray aim)
        {
            if (Active && Near(aim, 150f)) Knock(aim.origin, 10f, SvrPlugin.LanceStagger.Value);
        }

        public void Pulse(Vector3 at, float radius)
        {
            if (!Active) return;
            Vector3 d = Pos - at;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < radius) Knock(at, Mathf.Lerp(22f, 8f, dist / radius), SvrPlugin.PulseStagger.Value);
        }

        // ---------------------------------------------------------------- the hunt

        public SvrDemon()
        {
            _phase = Random.value * 6.28f;
        }

        private static float GroundY(Vector3 p, float fallback)
        {
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out h, 120f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) return h.point.y;
            return fallback;
        }

        private void Spawn(Vector3 player)
        {
            float a = Random.value * 6.28f;
            Vector3 p = player + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(45f, 60f);
            p.y = GroundY(p, player.y);
            Pos = p;
            _lastPos = p;
            State = Mode.Prowl;
            _modeTimer = Random.Range(18f, 30f);
            _chain = 0f;
            _center = Pos + Vector3.up * 1.4f;
        }

        public void Tick(float dt, CharacterBody body)
        {
            if (!SvrPlugin.DemonEnabled.Value) { State = Mode.Dormant; return; }
            _stageTime += dt;
            if (_chainT > 0f) { _chainT -= dt; if (_chainT <= 0f) _chain = 0f; }
            Vector3 pp = body.footPosition;

            if (State == Mode.Dormant)
            {
                if (_stageTime >= SvrPlugin.DemonFirstSpawn.Value) Spawn(pp);
                return;
            }
            if (State == Mode.Gone)
            {
                _goneTimer -= dt;
                if (_goneTimer <= 0f && SvrSenses.Lost < SvrPlugin.MaxSensesLost.Value) Spawn(pp);
                return;
            }

            Vector3 to = pp - Pos;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float fd = flat.magnitude;
            Vector3 dir = fd > 0.01f ? flat / fd : Vector3.forward;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            _flank += dt;
            float speed = 0f;
            Vector3 wish = Vector3.zero;

            switch (State)
            {
                case Mode.Prowl:
                    speed = SvrPlugin.DemonProwlSpeed.Value;
                    wish = dir * 0.25f + side * (Mathf.Sin(_phase + _flank * 0.15f) >= 0f ? 0.9f : -0.9f);
                    if (fd < 30f) wish -= dir * 0.7f;
                    _modeTimer -= dt;
                    if (_modeTimer <= 0f) { State = Mode.Stalk; _modeTimer = Random.Range(12f, 20f); }
                    break;
                case Mode.Stalk:
                    speed = SvrPlugin.DemonProwlSpeed.Value * 1.5f;
                    wish = dir + side * Mathf.Sin(_flank * 1.3f) * 0.7f;
                    _modeTimer -= dt;
                    if ((fd < 24f && Random.value < dt * 0.35f) || _modeTimer <= 0f) { State = Mode.Hunt; _modeTimer = 3f; }
                    break;
                case Mode.Hunt:
                    speed = SvrPlugin.DemonHuntSpeed.Value;
                    wish = dir + side * Mathf.Sin(_flank * 5f) * 0.25f;
                    _modeTimer -= dt;
                    if (_modeTimer <= 0f) { State = Mode.Stalk; _modeTimer = Random.Range(10f, 16f); }
                    break;
                case Mode.Stagger:
                    _stagger -= dt;
                    if (_stagger <= 0f) { State = Mode.Stalk; _modeTimer = 8f; }
                    break;
            }

            if (speed > 0f && wish.sqrMagnitude > 0.0001f) Pos += wish.normalized * speed * dt;

            // It phases through walls and climbs: follows the ground, and rises to her level when it is close.
            float gy = GroundY(Pos, Pos.y);
            float targetY = gy;
            if (fd < 12f) targetY = Mathf.Max(gy, Mathf.Min(pp.y, gy + 30f));
            Pos.y = Mathf.MoveTowards(Pos.y, targetY, 10f * dt);
            _center = Pos + Vector3.up * 1.4f;
            if (State == Mode.Hunt && fd < 7f) _lungeT = 0.4f;
            Animate(dt);
            if (fd > 0.1f && State != Mode.Stagger) Yaw = Mathf.LerpAngle(Yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 6f * dt);

            if (State != Mode.Stagger && Vector3.Distance(body.corePosition, _center) < SvrPlugin.DemonContactRange.Value + 0.6f) Feed();
        }

        private void Feed()
        {
            string sense = SvrSenses.TakeRandom();
            if (sense != null)
            {
                try { Chat.AddMessage("<color=#ff4a4a>It touched you. Your suit loses " + sense + ".</color>"); } catch { }
            }
            State = Mode.Gone;
            _goneTimer = SvrPlugin.DemonRespawn.Value;
            if (SvrSenses.Lost >= SvrPlugin.MaxSensesLost.Value) _goneTimer = 99999f;
        }
    }

    // ================================================================ the LiDAR: a decaying point cloud drawn over a black world

    public class SvrLidar : MonoBehaviour
    {
        private struct Dot
        {
            public Vector3 P;
            public float T, Life;
            public Color C;
            public bool Big;
        }

        // Scan modes, cycled with Q / E. They change the always-on scanner, and the Overdrive skill pushes the current one.
        public static readonly string[] OdNames = { "CONE", "SWEEP", "WIDE", "FOCUS", "ECHO" };
        private static readonly int[] OdKind = { 1, 0, 1, 2, 3 };    // which overdrive each mode becomes: 0 sweep, 1 burst, 2 focus, 3 echo
        public static int OdMode;
        private float _scanYaw, _echoPing, _peekReady, _modeFlash;
        private bool _loggedPeekFx;

        private Dot[] _dots;
        private int _head, _count;
        private CharacterBody _body;
        private InputBankTest _input;
        private bool _local;
        private SvrDemon _demon;
        private readonly List<Vector3> _demonPoints = new List<Vector3>(512);
        private readonly List<Vector3> _trailA = new List<Vector3>(512);
        private readonly List<Vector3> _trailB = new List<Vector3>(512);
        private float _trailT;

        private int _pulseLeft;
        private Vector3 _pulseAt;
        private float _pulseRange;
        private readonly List<HealthComponent> _tracked = new List<HealthComponent>();
        private readonly List<float> _trackedUntil = new List<float>();

        // Overdrive (the LiDAR pushed past its limits, in one of four modes)
        private int _odActive = -1;
        private float _odUntil, _odStart, _sweepYaw, _echoNext, _revealUntil;
        private int _spin;

        // Peek (visor off: real vision, blurry and blown out)
        private float _peekUntil, _peekStart, _peekK, _lastPpWeight = -1f;
        private Camera _peekCam;
        private bool _warped;
        private float _warpFov;
        private Quaternion _warpRot;
        private Component _ppVol, _ppLayer;                // Post-Process Volume + Layer, built by reflection (the game's NuGet package has no reference to it)
        private bool _ppFailed, _origFinalBlit = true;
        private SvrCameraBlur _camBlur;

        private Camera _dimCam;
        private int _origMask;
        private CameraClearFlags _origFlags;
        private Color _origBg;
        private bool _dimmed;
        private float _nextAudio;
        private GUIStyle _label;

        // The point cloud is drawn into a small pixel buffer (one draw call for tens of thousands of dots).
        private Texture2D _buf;
        private Color32[] _pix;
        private int _bufW, _bufH;

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _input = GetComponent<InputBankTest>();
            _dots = new Dot[Mathf.Max(1024, SvrPlugin.MaxDots.Value)];
            _demon = new SvrDemon();
        }

        private void OnEnable()
        {
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }

        private void OnDestroy() { Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender; RestoreCamera(); DropPeekFx(); if (_buf) Destroy(_buf); }
        private void OnDisable() { Camera.onPreCull -= PreCull; Camera.onPostRender -= PostRender; RestoreCamera(); DropPeekFx(); }

        public bool Peeking { get { return Time.time < _peekUntil; } }
        public bool OverdriveOn { get { return _odActive >= 0 && Time.time < _odUntil; } }
        // ---------------------------------------------------------------- helpers

        private bool IsLocal()
        {
            try
            {
                LocalUser lu = LocalUserManager.GetFirstLocalUser();
                return lu != null && lu.cachedBody == _body;
            }
            catch { return false; }
        }

        private Camera Cam()
        {
            foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
                if (rig && rig.sceneCam && rig.localUserViewer != null) return rig.sceneCam;
            return Camera.main;
        }

        private bool Alive { get { return _body && _body.healthComponent && _body.healthComponent.alive; } }

        private static Color Depth(float dist, float range, Vector3 normal)
        {
            float t = Mathf.Clamp01(dist / (range * 0.7f));
            Color red = new Color(1f, 0.15f, 0.1f), yellow = new Color(1f, 0.9f, 0.1f), green = new Color(0.2f, 1f, 0.25f);
            Color c = t < 0.5f ? Color.Lerp(red, yellow, t * 2f) : Color.Lerp(yellow, green, (t - 0.5f) * 2f);
            c.a = 0.55f + 0.45f * (1f - Mathf.Abs(normal.y));   // walls and edges read brighter than flat floor
            return c;
        }

        private void AddDot(Vector3 p, Color c, float life, bool big)
        {
            if (_demon.InVoid(p, 3f))
            {
                // The demon's presence eats the data: most dots dissolve, a few scatter as dim static.
                if (Random.value < 0.8f) return;
                p += Random.insideUnitSphere * 2f;
                c = new Color(0.9f, 0.25f, 0.8f, 0.45f);
            }
            _dots[_head] = new Dot { P = p, T = Time.time, Life = life, C = c, Big = big };
            _head = (_head + 1) % _dots.Length;
            if (_count < _dots.Length) _count++;
        }

        private void Cast(Vector3 origin, Vector3 dir, float range, float life, bool big)
        {
            RaycastHit hit;
            if (!Physics.Raycast(origin, dir, out hit, range, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore)) return;
            bool enemy = false;
            HurtBox hb = hit.collider ? hit.collider.GetComponent<HurtBox>() : null;
            if (hb)
            {
                if (hb.healthComponent && hb.healthComponent.body == _body) return;
                CharacterBody other = hb.healthComponent ? hb.healthComponent.body : null;
                enemy = other && other.teamComponent && _body.teamComponent && other.teamComponent.teamIndex != _body.teamComponent.teamIndex;
            }
            Color c = enemy ? new Color(1f, 0.2f, 0.12f, 1f) : Depth(hit.distance, range, hit.normal);
            AddDot(hit.point, c, life, big || enemy);
        }

        // ---------------------------------------------------------------- called by her skills

        public void Shot(Ray aim, float spool, bool anchored)
        {
            RaycastHit hit;
            if (Physics.Raycast(aim, out hit, SvrPlugin.MinigunRange.Value, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore))
                for (int i = 0; i < 9; i++)
                    AddDot(hit.point + Random.insideUnitSphere * 0.7f, new Color(1f, Random.Range(0.25f, 0.8f), 0.1f, 1f), 2.2f, true);
            _demon.MinigunHit(aim, spool, anchored);
        }

        public void LanceShot(Ray aim)
        {
            RaycastHit hit;
            float len = 150f;
            if (Physics.Raycast(aim, out hit, 150f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) len = hit.distance;
            for (float d = 3f; d < len; d += 3.5f)
                AddDot(aim.origin + aim.direction * d + Random.insideUnitSphere * 0.25f, new Color(1f, 0.9f, 0.2f, 1f), 3f, true);
            if (len < 150f) for (int i = 0; i < 24; i++) AddDot(hit.point + Random.insideUnitSphere * 1.2f, new Color(1f, 0.5f, 0.1f, 1f), 3.5f, true);
            _demon.LanceHit(aim);
        }

        public void Pulse(Vector3 at, float radius, CharacterBody owner)
        {
            _pulseAt = at;
            _pulseRange = radius * 1.2f;
            _pulseLeft = Mathf.RoundToInt(1500f * SvrSenses.SightMult);
            _demon.Pulse(at, radius);
            try
            {
                SphereSearch s = new SphereSearch { origin = at, radius = radius, mask = LayerIndex.entityPrecise.mask };
                s.RefreshCandidates();
                s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(owner.teamComponent.teamIndex));
                s.FilterCandidatesByDistinctHurtBoxEntities();
                foreach (HurtBox hb in s.GetHurtBoxes())
                    if (hb && hb.healthComponent && hb.healthComponent.alive && _tracked.Count < 30)
                    {
                        _tracked.Add(hb.healthComponent);
                        _trackedUntil.Add(Time.time + 5f);
                    }
            }
            catch { }
        }

        public void Overdrive(int mode)
        {
            int kind = OdKind[Mathf.Clamp(mode, 0, OdKind.Length - 1)];
            _odActive = kind;
            _odStart = Time.time;
            Vector3 at = _body.corePosition + Vector3.up * 0.8f;
            switch (kind)
            {
                case 0:     // SWEEP: a tall scan line spins all the way around her
                    _odUntil = Time.time + SvrPlugin.SweepSeconds.Value;
                    Vector3 f = _input ? _input.aimDirection : transform.forward;
                    _sweepYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                    break;
                case 1:     // BURST: the whole sphere around her at once
                    _odUntil = Time.time + 0.7f;
                    _pulseAt = at;
                    _pulseRange = 70f * SvrSenses.SightMult;
                    _pulseLeft = Mathf.RoundToInt(5200f * SvrSenses.SightMult);
                    break;
                case 2:     // FOCUS: one very tight, very dense, very long beam
                    _odUntil = Time.time + SvrPlugin.FocusSeconds.Value;
                    break;
                default:    // ECHO: every enemy lit up through the walls, and the Shimmer Demon with them
                    _odUntil = Time.time + SvrPlugin.EchoSeconds.Value;
                    _echoNext = 0f;
                    _revealUntil = _odUntil;
                    break;
            }
        }

        public void StartPeek(float seconds)
        {
            _peekStart = Time.time;
            _peekUntil = Time.time + seconds * Mathf.Max(0.5f, SvrSenses.SightMult);
            _peekK = Mathf.Max(_peekK, 0.25f);
        }

        // ---------------------------------------------------------------- per frame

        private static Vector3 Cone(Vector3 aim, float degrees, float theta)
        {
            Quaternion rot = Quaternion.LookRotation(aim);
            Vector3 d = Quaternion.Euler(-degrees * Mathf.Sin(theta), degrees * Mathf.Cos(theta), 0f) * Vector3.forward;
            return rot * d;
        }

        private void Update()
        {
            if (!_body) return;
            _local = IsLocal();
            if (!_local) { RestoreCamera(); return; }
            if (!Alive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            KeyCode next, prev, peek;
            int n0 = OdNames.Length;
            if (TryKey(SvrPlugin.ScanNextKey.Value, out next) && Input.GetKeyDown(next)) { OdMode = (OdMode + 1) % n0; _modeFlash = Time.time; Util.PlaySound("Play_UI_menuClick", gameObject); Scan(0.08f); }
            if (TryKey(SvrPlugin.ScanPrevKey.Value, out prev) && Input.GetKeyDown(prev)) { OdMode = (OdMode + n0 - 1) % n0; _modeFlash = Time.time; Util.PlaySound("Play_UI_menuClick", gameObject); Scan(0.08f); }
            // Peek on its own key, whatever is in the utility slot. It runs the Peek state on the Body machine,
            // which is networked, so everyone sees the visor flip.
            if (TryKey(SvrPlugin.PeekKey.Value, out peek) && Input.GetKeyDown(peek) && Time.time >= _peekReady && !Peeking)
            {
                EntityStateMachine esm = EntityStateMachine.FindByCustomName(gameObject, "Body");
                if (esm)
                {
                    esm.SetInterruptState(new SvrPeekState(), InterruptPriority.Skill);
                    _peekReady = Time.time + SvrPlugin.PeekCooldown.Value;
                }
            }

            _demon.Tick(dt, _body);
            Scan(dt);
            OverdriveTick(dt);

            if (_pulseLeft > 0)
            {
                int n = Mathf.Min(_pulseLeft, 650);
                for (int i = 0; i < n; i++) Cast(_pulseAt + Vector3.up * 0.8f, Random.onUnitSphere, _pulseRange, SvrPlugin.DotLife.Value * 1.6f, true);
                _pulseLeft -= n;
            }

            int per = (_odActive == 3 && OverdriveOn) ? 9 : 3;
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                HealthComponent hc = _tracked[i];
                if (!hc || !hc.alive || !hc.body || Time.time > _trackedUntil[i]) { _tracked.RemoveAt(i); _trackedUntil.RemoveAt(i); continue; }
                for (int k = 0; k < per; k++)
                    AddDot(hc.body.corePosition + Random.insideUnitSphere * Mathf.Max(0.5f, hc.body.radius), new Color(1f, 0.15f, 0.1f, 1f), 0.4f, true);
            }

            if (SvrSenses.Hearing && Time.unscaledTime > _nextAudio) { _nextAudio = Time.unscaledTime + 2f; SvrAudio.Muffle(true); }
        }

        private static bool TryKey(string name, out KeyCode k)
        {
            k = KeyCode.None;
            try { return !string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out k) && k != KeyCode.None; }
            catch { return false; }
        }

        // The always-on scanner. Rays fan out in a spiral over a wide cone, so the cloud fills in evenly and packs tight.
        private void Scan(float dt)
        {
            if (!_input) return;
            float prox = _demon.Proximity(_body.corePosition);
            if (prox > 0.2f && Random.value < prox * 0.85f) return;      // the suit's scanner stutters and drops frames near it
            float sight = SvrSenses.SightMult;
            float frames = Mathf.Clamp(dt * 60f, 0.5f, 3f);
            int rays = Mathf.RoundToInt(SvrPlugin.RaysPerFrame.Value * frames * sight * (1f - 0.5f * prox));
            float range = SvrPlugin.ScanRange.Value * sight;
            Vector3 o = _input.aimOrigin;
            Vector3 aim = _input.aimDirection;
            float life = SvrPlugin.DotLife.Value * (sight < 1f ? 0.7f : 1f);
            switch (OdMode)
            {
                case 1:     // SWEEP: a lighthouse beam that circles her, plus a little ahead
                    _scanYaw += 230f * dt;
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        if (i % 4 == 0) { float f0 = ((_spin % 257) + Random.value) / 257f; Cast(o, Cone(aim, 30f * Mathf.Sqrt(f0), _spin * 2.39996f), range, life, false); continue; }
                        float yaw = _scanYaw + Random.Range(-2.5f, 2.5f);
                        float pitch = Mathf.Lerp(-55f, 65f, Random.value);
                        Cast(o, Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward, range * 1.1f, life * 1.3f, false);
                    }
                    break;
                case 2:     // WIDE: an even bubble all around her, shorter range
                    for (int i = 0; i < rays; i++)
                    {
                        Vector3 d = Random.onUnitSphere;
                        d.y *= 0.6f;
                        Cast(o, d.normalized, range * 0.55f, life, false);
                    }
                    break;
                case 3:     // FOCUS: a narrow, dense, long beam
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        float f1 = ((_spin % 257) + Random.value) / 257f;
                        Cast(o, Cone(aim, 13f * Mathf.Sqrt(f1), _spin * 2.39996f), range * 1.8f, life * 1.2f, false);
                    }
                    break;
                case 4:     // ECHO: a shorter cone, and nearby enemies ping through walls every second
                    for (int i = 0; i < (rays * 3) / 4; i++)
                    {
                        _spin++;
                        float f2 = ((_spin % 257) + Random.value) / 257f;
                        Cast(o, Cone(aim, 45f * Mathf.Sqrt(f2), _spin * 2.39996f), range * 0.7f, life, false);
                    }
                    if (Time.time >= _echoPing)
                    {
                        _echoPing = Time.time + 1.1f;
                        try
                        {
                            SphereSearch s = new SphereSearch { origin = _body.corePosition, radius = 45f * sight, mask = LayerIndex.entityPrecise.mask };
                            s.RefreshCandidates();
                            s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex));
                            s.FilterCandidatesByDistinctHurtBoxEntities();
                            foreach (HurtBox hb in s.GetHurtBoxes())
                                if (hb && hb.healthComponent && hb.healthComponent.alive && !_tracked.Contains(hb.healthComponent) && _tracked.Count < 30)
                                {
                                    _tracked.Add(hb.healthComponent);
                                    _trackedUntil.Add(Time.time + 0.45f);
                                }
                        }
                        catch { }
                    }
                    break;
                default:    // CONE: the spiral cone ahead, a little all around her
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        if (Random.value < 0.18f) { Cast(o, Random.onUnitSphere, range * 0.35f, life, false); continue; }
                        float f = ((_spin % 257) + Random.value) / 257f;
                        Cast(o, Cone(aim, 52f * Mathf.Sqrt(f), _spin * 2.39996f), range, life, false);
                    }
                    break;
            }
        }

        private void OverdriveTick(float dt)
        {
            if (_odActive < 0) return;
            if (Time.time >= _odUntil) { _odActive = -1; return; }
            float sight = SvrSenses.SightMult;
            Vector3 o = _input ? _input.aimOrigin : _body.corePosition;
            Vector3 aim = _input ? _input.aimDirection : transform.forward;
            float life = SvrPlugin.DotLife.Value * 1.8f;
            switch (_odActive)
            {
                case 0:
                {
                    float dur = Mathf.Max(0.5f, _odUntil - _odStart);
                    float step = 360f / dur * dt;
                    int slices = Mathf.Max(1, Mathf.CeilToInt(step / 1.1f));
                    for (int s = 0; s < slices; s++)
                    {
                        _sweepYaw += step / slices;
                        for (int i = 0; i < 52; i++)
                        {
                            float pitch = Mathf.Lerp(-60f, 70f, (i + Random.value) / 52f);
                            Cast(o, Quaternion.Euler(-pitch, _sweepYaw, 0f) * Vector3.forward, 100f * sight, life, false);
                        }
                    }
                    break;
                }
                case 2:
                {
                    int n = Mathf.RoundToInt(520f * Mathf.Clamp(dt * 60f, 0.5f, 3f) * sight);
                    for (int i = 0; i < n; i++)
                        Cast(o, Cone(aim, 6.5f * Mathf.Sqrt(Random.value), Random.value * 6.283f), 170f * sight, life * 1.2f, false);
                    break;
                }
                case 3:
                {
                    if (Time.time < _echoNext) break;
                    _echoNext = Time.time + 0.4f;
                    try
                    {
                        SphereSearch s = new SphereSearch { origin = _body.corePosition, radius = 85f, mask = LayerIndex.entityPrecise.mask };
                        s.RefreshCandidates();
                        s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex));
                        s.FilterCandidatesByDistinctHurtBoxEntities();
                        foreach (HurtBox hb in s.GetHurtBoxes())
                            if (hb && hb.healthComponent && hb.healthComponent.alive && !_tracked.Contains(hb.healthComponent) && _tracked.Count < 40)
                            {
                                _tracked.Add(hb.healthComponent);
                                _trackedUntil.Add(_odUntil);
                            }
                    }
                    catch { }
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- camera: dark world, or the real world while peeking

        private void LateUpdate()
        {
            if (!_local || !_body) return;
            if (!Alive) { RestoreCamera(); DropPeekFx(); _peekK = 0f; return; }

            // Peek intensity: snaps on, holds, then fades back to the dark over the last second.
            float now = Time.time;
            float target = 0f;
            if (Peeking) target = Mathf.Clamp01(0.25f + (now - _peekStart) / 0.18f) * Mathf.Clamp01((_peekUntil - now) / 0.9f + 0.25f);
            else if (now < _peekUntil + 0.8f) target = 0f;
            _peekK = Mathf.MoveTowards(_peekK, target, Time.deltaTime * 6f);

            Camera cam = Cam();
            if (!cam) return;
            _peekCam = cam;

            if (Peeking || _peekK > 0.01f)
            {
                if (Peeking) RestoreCamera();             // the real world is back
                ApplyPeekFx(cam, _peekK);
                if (Peeking) return;
            }
            else
            {
                ApplyPeekFx(cam, 0f);
            }

            if (!SvrPlugin.DarkWorld.Value) { RestoreCamera(); return; }
            if (!_dimmed || _dimCam != cam)
            {
                RestoreCamera();
                _dimCam = cam;
                _origMask = cam.cullingMask;
                _origFlags = cam.clearFlags;
                _origBg = cam.backgroundColor;
                _dimmed = true;
            }
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        private void RestoreCamera()
        {
            if (_dimmed && _dimCam)
            {
                _dimCam.cullingMask = _origMask;
                _dimCam.clearFlags = _origFlags;
                _dimCam.backgroundColor = _origBg;
            }
            _dimmed = false;
            _dimCam = null;
        }

        // Peek post-processing: heavy blur (multi-pass camera downsample + depth of field), blown-out bloom and exposure,
        // colour fringing, a bulging lens. Done by reflection so the build does not need the post-processing assembly.
        private static Type PT(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType("UnityEngine.Rendering.PostProcessing." + name, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        private static ScriptableObject Fx(string type, params object[] nameValue)
        {
            Type t = PT(type);
            if (t == null) throw new Exception(type + " not found");
            ScriptableObject o = ScriptableObject.CreateInstance(t);
            FieldInfo act = t.GetField("active", BindingFlags.Instance | BindingFlags.Public);
            if (act != null) act.SetValue(o, true);
            Param(o, "enabled", true);
            for (int i = 0; i + 1 < nameValue.Length; i += 2) Param(o, (string)nameValue[i], nameValue[i + 1]);
            return o;
        }

        private static void Param(object effect, string field, object value)
        {
            FieldInfo f = effect.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public);
            if (f == null) return;
            object p = f.GetValue(effect);
            if (p == null) return;
            foreach (MethodInfo m in p.GetType().GetMethods())
                if (m.Name == "Override" && m.GetParameters().Length == 1)
                {
                    object v = value;
                    Type want = m.GetParameters()[0].ParameterType;
                    if (want.IsEnum && value is string) v = Enum.Parse(want, (string)value);
                    m.Invoke(p, new[] { v });
                    return;
                }
        }

        private void SetPpWeight(float w)
        {
            if (Mathf.Abs(w - _lastPpWeight) < 0.002f) return;
            _lastPpWeight = w;
            if (_ppVol)
            {
                try
                {
                    FieldInfo f = _ppVol.GetType().GetField("weight", BindingFlags.Instance | BindingFlags.Public);
                    if (f != null) f.SetValue(_ppVol, w);
                    MethodInfo disp = _ppVol.GetType().GetMethod("DispatchVolumeSettingsChangedEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (disp != null) disp.Invoke(_ppVol, null);
                }
                catch { }
            }
            if (_ppLayer)
            {
                try
                {
                    Type lt = _ppLayer.GetType();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    lt.GetField("finalBlitToCameraTarget", flags)?.SetValue(_ppLayer, w <= 0.01f ? _origFinalBlit : false);
                    lt.GetField("globalVolumesDirty", flags)?.SetValue(_ppLayer, true);
                    lt.GetField("volumeSettingsChanged", flags)?.SetValue(_ppLayer, true);
                    lt.GetField("m_SettingsUpdateNeeded", flags)?.SetValue(_ppLayer, true);
                }
                catch { }
            }
        }

        private void ApplyPeekFx(Camera cam, float w)
        {
            if (cam)
            {
                if (!_camBlur || _camBlur.gameObject != cam.gameObject)
                    _camBlur = cam.GetComponent<SvrCameraBlur>() ?? cam.gameObject.AddComponent<SvrCameraBlur>();
                _camBlur.Weight = w;
                _camBlur.enabled = w > 0.01f;
            }
            if (w <= 0.01f && !_ppVol) return;
            if (_ppFailed) return;
            try
            {
                if (!_ppVol && w > 0.01f)
                {
                    Type layerType = PT("PostProcessLayer");
                    Component layer = layerType != null ? cam.GetComponent(layerType) : null;
                    if (!layer) { _ppFailed = true; SvrPlugin.Log.LogWarning("Peek: the camera has no post-processing layer, using camera downsample blur + wash."); return; }
                    _ppLayer = layer;
                    FieldInfo fb = layerType.GetField("finalBlitToCameraTarget", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fb != null) _origFinalBlit = (bool)fb.GetValue(layer);
                    object vl = layerType.GetField("volumeLayer").GetValue(layer);
                    int mask = ((LayerMask)vl).value, lay = 0;
                    for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) { lay = i; break; }

                    // Created inactive so OnEnable() runs with isGlobal = true (RoR2's PostProcessVolume.OnEnable puts
                    // volumes with isGlobal = false into a Burst localVolumes job that requires a collider).
                    GameObject go = new GameObject("SvrPeekVolume");
                    go.SetActive(false);
                    go.layer = lay;
                    Type volType = PT("PostProcessVolume");
                    Component vol = go.AddComponent(volType);
                    volType.GetField("isGlobal").SetValue(vol, true);
                    volType.GetField("priority").SetValue(vol, 300f);
                    volType.GetField("weight").SetValue(vol, 0f);
                    object profile = volType.GetProperty("profile").GetValue(vol, null);
                    MethodInfo add = profile.GetType().GetMethod("AddSettings", new[] { PT("PostProcessEffectSettings") });
                    ScriptableObject[] list =
                    {
                        Fx("Bloom", "intensity", 28f, "threshold", 0.25f, "diffusion", 10f),
                        Fx("DepthOfField", "focusDistance", 0.25f, "aperture", 0.05f, "focalLength", 200f, "kernelSize", "VeryLarge"),
                        Fx("ChromaticAberration", "intensity", 1f),
                        Fx("ColorGrading", "postExposure", 2.2f, "saturation", -35f, "contrast", -25f),
                        Fx("LensDistortion", "intensity", -45f)
                    };
                    foreach (ScriptableObject s in list) add.Invoke(profile, new object[] { s });
                    go.SetActive(true);
                    _ppVol = vol;
                    _lastPpWeight = -1f;
                }
                SetPpWeight(w);
                if (!_loggedPeekFx && w > 0.01f) { _loggedPeekFx = true; SvrPlugin.Log.LogInfo("Peek: post-processing and camera blur are on."); }
            }
            catch (Exception e)
            {
                _ppFailed = true;
                SvrPlugin.Log.LogWarning("Peek post-processing unavailable, using camera blur and screen wash: " + e.Message);
            }
        }

        private void DropPeekFx()
        {
            SetPpWeight(0f);
            try { if (_camBlur) { _camBlur.Weight = 0f; _camBlur.enabled = false; } } catch { }
            try { if (_ppVol) Destroy(_ppVol.gameObject); } catch { }
            _ppVol = null;
            _ppLayer = null;
            _lastPpWeight = -1f;
        }

        // The lens pulses and rolls while the visor is off (applied right before the camera renders, undone right after).
        private void PreCull(Camera c)
        {
            if (!_local || c != _peekCam || _peekK < 0.02f) return;
            try
            {
                _warpFov = c.fieldOfView;
                _warpRot = c.transform.rotation;
                c.fieldOfView = _warpFov * (1f + 0.06f * _peekK * Mathf.Sin(Time.time * 7f));
                c.transform.rotation = _warpRot * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 11f) * 2.2f * _peekK);
                _warped = true;
            }
            catch { }
        }

        private void PostRender(Camera c)
        {
            if (!_warped || c != _peekCam) return;
            try { c.fieldOfView = _warpFov; c.transform.rotation = _warpRot; } catch { }
            _warped = false;
        }

        // ---------------------------------------------------------------- drawing (IMGUI, so no shaders or materials are needed)

        private void OnGUI()
        {
            if (!_local || !_body || !Alive) return;
            if (Event.current.type != EventType.Repaint) return;
            Camera cam = Cam();
            if (!cam) return;
            float scale = Screen.height / 1080f;
            Color old = GUI.color;
            DrawDots(cam, scale);
            DrawDemon(cam, scale);
            DrawPeekWash(scale);
            DrawHud(scale);
            GUI.color = old;
        }

        private void DrawDots(Camera cam, float scale)
        {
            int div = Mathf.Clamp(SvrPlugin.DotDivider.Value, 1, 4);
            int W = Mathf.Max(64, Screen.width / div), H = Mathf.Max(64, Screen.height / div);
            if (_buf == null || _bufW != W || _bufH != H)
            {
                if (_buf) Destroy(_buf);
                _buf = new Texture2D(W, H, TextureFormat.RGBA32, false);
                _buf.filterMode = FilterMode.Point;
                _buf.wrapMode = TextureWrapMode.Clamp;
                _pix = new Color32[W * H];
                _bufW = W; _bufH = H;
            }
            Array.Clear(_pix, 0, _pix.Length);

            Matrix4x4 m = cam.projectionMatrix * cam.worldToCameraMatrix;
            float now = Time.time;
            int len = _dots.Length;
            bool voidOn = _demon.Active;
            for (int k = _count - 1; k >= 0; k--)          // oldest first, so the newest dots end up on top
            {
                int idx = ((_head - 1 - k) % len + len) % len;
                Dot d = _dots[idx];
                float age = now - d.T;
                if (age >= d.Life) continue;
                Vector3 p = d.P;
                float w = m.m30 * p.x + m.m31 * p.y + m.m32 * p.z + m.m33;
                if (w <= 0.3f) continue;
                float nx = (m.m00 * p.x + m.m01 * p.y + m.m02 * p.z + m.m03) / w;
                float ny = (m.m10 * p.x + m.m11 * p.y + m.m12 * p.z + m.m13) / w;
                if (nx < -1f || nx > 1f || ny < -1f || ny > 1f) continue;
                if (voidOn && _demon.InVoid(p, 2.2f)) continue;
                float fade = 1f - age / d.Life;
                float a = d.C.a * fade;
                Color32 c = new Color32((byte)(d.C.r * 255f), (byte)(d.C.g * 255f), (byte)(d.C.b * 255f), (byte)(Mathf.Clamp01(a) * 255f));
                int ix = (int)((nx * 0.5f + 0.5f) * W), iy = (int)((ny * 0.5f + 0.5f) * H);
                int sz = (d.Big || w < 9f) ? 2 : 1;
                for (int yy = 0; yy < sz; yy++)
                {
                    int y = iy + yy;
                    if (y >= H) break;
                    for (int xx = 0; xx < sz; xx++)
                    {
                        int x = ix + xx;
                        if (x >= W) break;
                        _pix[y * W + x] = c;
                    }
                }
            }
            _buf.SetPixels32(_pix);
            _buf.Apply(false);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.3f, _peekK));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _buf, ScaleMode.StretchToFill, true);
        }

        private void DrawDemon(Camera cam, float scale)
        {
            if (!_demon.Active) return;
            bool revealed = Time.time < _revealUntil;
            bool blocked = !revealed && Physics.Linecast(cam.transform.position, _demon.Center, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            float vis = blocked ? 0.18f : 0.95f;
            _demon.FillWorld(_demonPoints, Time.time);

            // afterimages: where it was a moment ago, so its movement smears
            _trailT -= Time.deltaTime;
            if (_trailT <= 0f)
            {
                _trailT = 0.07f;
                _trailB.Clear(); _trailB.AddRange(_trailA);
                _trailA.Clear(); _trailA.AddRange(_demonPoints);
            }
            Texture2D white = Texture2D.whiteTexture;
            DrawTrail(cam, _trailB, 0.10f * vis, scale, white);
            DrawTrail(cam, _trailA, 0.20f * vis, scale, white);

            float split = (2.5f + Random.value * 1.5f) * scale;
            for (int i = 0; i < _demonPoints.Count; i++)
            {
                if (Random.value < 0.06f) continue;                                // flicker
                Vector3 sp = cam.WorldToScreenPoint(_demonPoints[i]);
                if (sp.z <= 0.3f) continue;
                float size = Mathf.Clamp(380f / sp.z, 2.4f, 9f) * scale;
                float dx = split;
                if (Mathf.Sin(sp.y * 0.045f + Time.time * 38f) > 0.975f) dx += (Random.value - 0.5f) * 40f * scale;   // glitch bands
                float y = Screen.height - sp.y - size * 0.5f;
                GUI.color = new Color(1f, 0.15f, 0.2f, vis);
                GUI.DrawTexture(new Rect(sp.x - dx - size * 0.5f, y, size, size), white);
                GUI.color = new Color(0.2f, 0.45f, 1f, vis);
                GUI.DrawTexture(new Rect(sp.x + dx - size * 0.5f, y, size, size), white);
                GUI.color = new Color(0.9f, 0.97f, 1f, vis);
                GUI.DrawTexture(new Rect(sp.x - size * 0.5f, y, size, size), white);
            }
        }

        private static void DrawTrail(Camera cam, List<Vector3> pts, float alpha, float scale, Texture2D white)
        {
            GUI.color = new Color(1f, 0.2f, 0.25f, alpha);
            for (int i = 0; i < pts.Count; i += 2)
            {
                Vector3 sp = cam.WorldToScreenPoint(pts[i]);
                if (sp.z <= 0.3f) continue;
                float size = Mathf.Clamp(300f / sp.z, 2f, 7f) * scale;
                GUI.DrawTexture(new Rect(sp.x - size * 0.5f, Screen.height - sp.y - size * 0.5f, size, size), white);
            }
        }

        // Blown-out visor-off look: white wash, light bleeding in from the edges, streaks, and a burn-in after it ends.
        private void DrawPeekWash(float scale)
        {
            float now = Time.time;
            float k = _peekK;
            float after = (!Peeking && now < _peekUntil + 0.8f) ? 1f - (now - _peekUntil) / 0.8f : 0f;
            if (k < 0.02f && after <= 0f) return;
            Texture2D white = Texture2D.whiteTexture;
            float flick = 0.88f + 0.12f * Mathf.Sin(now * 23f) + (Random.value < 0.04f ? 0.25f : 0f);
            float wash = (0.22f * k + 0.45f * after) * flick;
            GUI.color = new Color(1f, 0.97f, 0.9f, Mathf.Clamp01(wash));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);
            for (int i = 0; i < 7; i++)
            {
                float inset = i * 0.035f;
                float a = (0.16f - i * 0.02f) * k * flick;
                if (a <= 0f) continue;
                GUI.color = new Color(1f, 0.98f, 0.92f, a);
                float bx = Screen.width * inset, by = Screen.height * inset;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, by + 6f), white);
                GUI.DrawTexture(new Rect(0f, Screen.height - by - 6f, Screen.width, by + 6f), white);
                GUI.DrawTexture(new Rect(0f, 0f, bx + 6f, Screen.height), white);
                GUI.DrawTexture(new Rect(Screen.width - bx - 6f, 0f, bx + 6f, Screen.height), white);
            }
            for (int i = 0; i < 6; i++)
            {
                GUI.color = new Color(1f, 1f, 1f, Random.Range(0.1f, 0.35f) * k);
                float bw = Random.Range(0.25f, 1f) * Screen.width;
                GUI.DrawTexture(new Rect(Random.value * (Screen.width - bw), Random.value * Screen.height, bw, Random.Range(2f, 16f) * scale), white);
            }
        }

        private static readonly Rect[] Regions =
        {
            new Rect(0.55f, 0.86f, 0.44f, 0.13f),   // skills
            new Rect(0.02f, 0.86f, 0.30f, 0.12f),   // health
            new Rect(0.465f, 0.465f, 0.07f, 0.07f), // crosshair
            new Rect(0.02f, 0.78f, 0.20f, 0.06f)    // level / gold
        };
        private static readonly float[] RegionFrom = { 0.30f, 0.50f, 0.70f, 0.20f };

        private void DrawHud(float scale)
        {
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.fontStyle = FontStyle.Bold;
                _label.alignment = TextAnchor.MiddleLeft;
            }
            Texture2D white = Texture2D.whiteTexture;
            int fs = Mathf.RoundToInt(15f * scale);
            _label.fontSize = Mathf.Max(10, fs);

            // Sensor status strip (the four senses) and the spool gauge.
            float x = Screen.width * 0.03f, y = Screen.height * 0.745f;
            DrawSense("SEE", SvrSenses.Sight, ref x, y, scale);
            DrawSense("HEAR", SvrSenses.Hearing, ref x, y, scale);
            DrawSense("TOUCH", SvrSenses.Touch, ref x, y, scale);
            DrawSense("BAL", SvrSenses.Balance, ref x, y, scale);

            SvrSpool sp = _body.GetComponent<SvrSpool>();
            if (sp && (sp.Spool > 0.01f || sp.AnchorLeft > 0f))
            {
                float w = 170f * scale, h = 8f * scale;
                float bx = Screen.width * 0.5f - w * 0.5f, by = Screen.height * 0.64f;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(bx - 2f, by - 2f, w + 4f, h + 4f), white);
                GUI.color = sp.AnchorLeft > 0f ? new Color(1f, 0.85f, 0.2f, 0.95f) : new Color(0.55f, 1f, 0.3f, 0.95f);
                GUI.DrawTexture(new Rect(bx, by, w * sp.Spool, h), white);
                GUI.color = new Color(0.8f, 1f, 0.7f, 0.9f);
                GUI.Label(new Rect(bx, by + h + 2f, w, 20f * scale), sp.AnchorLeft > 0f ? "SPOOL  ANCHORED" : "SPOOL", _label);
            }

            // Overdrive mode readout (only when the skill is equipped) and the visor timer.
            SkillLocator skl = _body.skillLocator;
            bool hasOd = skl && skl.special && skl.special.skillDef && skl.special.skillDef.skillName == SvrPlugin.T + "OVERDRIVE";
            {
                // scan mode readout (always): name, five pips, the keys; flashes when changed
                bool flash = Time.time - _modeFlash < 0.6f;
                float sx = Screen.width * 0.03f, sy = Screen.height * 0.712f;
                GUI.color = flash ? new Color(1f, 1f, 1f, 1f) : (OverdriveOn ? new Color(1f, 0.9f, 0.3f, 0.95f) : new Color(0.6f, 1f, 0.4f, 0.95f));
                string keys = "[" + SvrPlugin.ScanPrevKey.Value.ToUpperInvariant() + "/" + SvrPlugin.ScanNextKey.Value.ToUpperInvariant() + "]";
                GUI.Label(new Rect(sx, sy, 420f * scale, 24f * scale), "SCAN: " + OdNames[OdMode % OdNames.Length] + "  " + keys + (hasOd ? "   R = OVERDRIVE" : ""), _label);
                for (int i = 0; i < OdNames.Length; i++)
                {
                    GUI.color = i == OdMode ? new Color(0.6f, 1f, 0.4f, 0.95f) : new Color(0.6f, 1f, 0.4f, 0.25f);
                    GUI.DrawTexture(new Rect(sx + i * 16f * scale, sy - 8f * scale, 12f * scale, 5f * scale), white);
                }
                float pr = _peekReady - Time.time;
                GUI.color = new Color(0.9f, 0.95f, 1f, 0.85f);
                GUI.Label(new Rect(sx, sy + 20f * scale, 420f * scale, 24f * scale), "PEEK [" + SvrPlugin.PeekKey.Value.ToUpperInvariant() + "]" + (pr > 0f ? "  " + pr.ToString("0") + "s" : "  ready"), _label);
            }
            if (Peeking)
            {
                float left = Mathf.Clamp01((_peekUntil - Time.time) / Mathf.Max(0.1f, _peekUntil - _peekStart));
                float pw = 220f * scale;
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - pw * 0.5f - 2f, Screen.height * 0.12f - 2f, pw + 4f, 10f * scale + 4f), white);
                GUI.color = new Color(1f, 0.95f, 0.8f, 0.95f);
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - pw * 0.5f, Screen.height * 0.12f, pw * left, 10f * scale), white);
                GUI.Label(new Rect(Screen.width * 0.5f - pw * 0.5f, Screen.height * 0.12f + 14f * scale, pw, 24f * scale), "VISOR OFF", _label);
            }

            // Suit failure near the demon: the HUD parts flicker with static and drop offline one after another.
            float prox = _demon.Proximity(_body.corePosition);
            if (prox <= 0.1f) return;
            GUI.color = new Color(1f, 0.35f, 0.3f, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            if (prox > 0.25f) GUI.Label(new Rect(Screen.width * 0.40f, Screen.height * 0.07f, Screen.width * 0.2f, 24f * scale), "SIGNAL INTERFERENCE", _label);
            for (int r = 0; r < Regions.Length; r++)
            {
                float chance = Mathf.InverseLerp(RegionFrom[r], 1f, prox);
                if (chance <= 0f || Random.value > chance * 0.85f) continue;
                Rect fr = Regions[r];
                Rect box = new Rect(fr.x * Screen.width, fr.y * Screen.height, fr.width * Screen.width, fr.height * Screen.height);
                if (chance > 0.55f && Random.value < chance * 0.5f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.9f);
                    GUI.DrawTexture(box, white);
                    GUI.color = new Color(1f, 0.3f, 0.25f, 0.9f);
                    GUI.Label(new Rect(box.x + 6f, box.y, box.width, box.height), "OFFLINE", _label);
                }
                int bars = 10 + Mathf.RoundToInt(chance * 14f);
                for (int i = 0; i < bars; i++)
                {
                    float g = Random.Range(0.3f, 1f);
                    GUI.color = new Color(g, g, g, Random.Range(0.25f, 0.8f));
                    float bw = Random.Range(0.05f, 0.4f) * box.width, bh = Random.Range(1f, 5f) * scale;
                    GUI.DrawTexture(new Rect(box.x + Random.value * (box.width - bw), box.y + Random.value * box.height, bw, bh), white);
                }
            }
        }

        private void DrawSense(string name, bool lost, ref float x, float y, float scale)
        {
            float w = (name.Length * 11f + 22f) * scale;
            Texture2D white = Texture2D.whiteTexture;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(x, y, w, 24f * scale), white);
            GUI.color = lost ? new Color(1f, 0.3f, 0.25f, 0.95f) : new Color(0.6f, 1f, 0.4f, 0.95f);
            GUI.Label(new Rect(x + 6f * scale, y, w, 24f * scale), lost ? name + " X" : name, _label);
            x += w + 6f * scale;
        }
    }

    /// <summary>
    /// Attached to the active scene camera while Peek (visor off) is active.
    /// Runs a multi-pass bilinear pyramid downsample + upsample in OnRenderImage so the 3D world is heavily blurred
    /// even if DepthOfField or PostProcessing is disabled or skipped.
    /// </summary>
    public class SvrCameraBlur : MonoBehaviour
    {
        public float Weight;

        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (Weight <= 0.01f || !src)
            {
                Graphics.Blit(src, dst);
                return;
            }
            float k = Mathf.Clamp01(Weight);
            int w = Mathf.Max(64, src.width), h = Mathf.Max(64, src.height);
            int w1 = Mathf.Max(32, Mathf.RoundToInt(w / Mathf.Lerp(1.2f, 2.5f, k))), h1 = Mathf.Max(32, Mathf.RoundToInt(h / Mathf.Lerp(1.2f, 2.5f, k)));
            int w2 = Mathf.Max(16, Mathf.RoundToInt(w / Mathf.Lerp(1.5f, 6.0f, k))), h2 = Mathf.Max(16, Mathf.RoundToInt(h / Mathf.Lerp(1.5f, 6.0f, k)));
            int w3 = Mathf.Max(8, Mathf.RoundToInt(w / Mathf.Lerp(2.0f, 14.0f, k))), h3 = Mathf.Max(8, Mathf.RoundToInt(h / Mathf.Lerp(2.0f, 14.0f, k)));
            int w4 = Mathf.Max(4, Mathf.RoundToInt(w / Mathf.Lerp(2.5f, 26.0f, k))), h4 = Mathf.Max(4, Mathf.RoundToInt(h / Mathf.Lerp(2.5f, 26.0f, k)));
            RenderTextureFormat fmt = src.format;
            RenderTexture rt1 = RenderTexture.GetTemporary(w1, h1, 0, fmt);
            RenderTexture rt2 = RenderTexture.GetTemporary(w2, h2, 0, fmt);
            RenderTexture rt3 = RenderTexture.GetTemporary(w3, h3, 0, fmt);
            RenderTexture rt4 = RenderTexture.GetTemporary(w4, h4, 0, fmt);
            rt1.filterMode = FilterMode.Bilinear;
            rt2.filterMode = FilterMode.Bilinear;
            rt3.filterMode = FilterMode.Bilinear;
            rt4.filterMode = FilterMode.Bilinear;
            Graphics.Blit(src, rt1);
            Graphics.Blit(rt1, rt2);
            Graphics.Blit(rt2, rt3);
            Graphics.Blit(rt3, rt4);
            Graphics.Blit(rt4, rt3);
            Graphics.Blit(rt3, rt2);
            Graphics.Blit(rt2, rt1);
            Graphics.Blit(rt1, dst);
            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
            RenderTexture.ReleaseTemporary(rt3);
            RenderTexture.ReleaseTemporary(rt4);
        }
    }
}
