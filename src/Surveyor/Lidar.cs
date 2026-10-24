using System;
using System.Collections.Generic;
using System.Reflection;
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
        private static readonly List<Vector3> Local = BuildLocal();

        public bool Active { get { return State != Mode.Dormant && State != Mode.Gone; } }
        public Vector3 Center { get { return _center; } }

        // ---------------------------------------------------------------- the body: a horned humanoid made of points

        private static List<Vector3> BuildLocal()
        {
            List<Vector3> l = new List<Vector3>();
            System.Random rnd = new System.Random(7);
            Func<Vector3> sphere = () =>
            {
                Vector3 v = new Vector3((float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1), (float)(rnd.NextDouble() * 2 - 1));
                return v.sqrMagnitude < 0.0001f ? Vector3.up : v.normalized;
            };
            for (int i = 0; i < 26; i++) l.Add(new Vector3(0f, 2.4f, 0.04f) + sphere() * 0.27f);                                  // head
            for (int side = -1; side <= 1; side += 2)
                for (int s = 0; s < 11; s++)                                                                                       // horns
                {
                    float u = s / 10f;
                    l.Add(new Vector3(side * (0.2f + 0.3f * Mathf.Sin(u * 2.2f)), 2.55f + u * 0.62f, 0.04f - u * 0.1f));
                }
            for (int i = 0; i < 12; i++) l.Add(new Vector3(Mathf.Lerp(-0.62f, 0.62f, i / 11f), 2.05f, 0f));                         // shoulders
            for (int i = 0; i < 50; i++) { Vector3 v = sphere(); l.Add(new Vector3(0f, 1.5f, 0f) + new Vector3(v.x * 0.4f, v.y * 0.62f, v.z * 0.24f)); } // torso
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 14; i++)                                                                                       // arms
                {
                    float u = i / 13f;
                    l.Add(new Vector3(side * Mathf.Lerp(0.62f, 0.8f, u), Mathf.Lerp(2.05f, 0.75f, u), Mathf.Lerp(0f, 0.32f, Mathf.Sin(u * 1.5f))));
                }
                for (int c = 0; c < 4; c++)                                                                                        // claws
                    l.Add(new Vector3(side * (0.8f + c * 0.04f), 0.72f - c * 0.07f, 0.34f + c * 0.04f));
                for (int i = 0; i < 14; i++)                                                                                       // legs
                {
                    float u = i / 13f;
                    l.Add(new Vector3(side * Mathf.Lerp(0.2f, 0.27f, u), Mathf.Lerp(1.0f, 0f, u), Mathf.Sin(u * Mathf.PI) * 0.18f));
                }
            }
            return l;
        }

        /// <summary>Its points in world space for this frame (wobbling, breathing, shaking when staggered).</summary>
        public void FillWorld(List<Vector3> into, float t)
        {
            into.Clear();
            Quaternion q = Quaternion.Euler(0f, Yaw, 0f);
            float shake = State == Mode.Stagger ? 0.09f : 0f;
            float breathe = 1f + 0.02f * Mathf.Sin(t * 1.1f);
            for (int i = 0; i < Local.Count; i++)
            {
                Vector3 w = Local[i] * 1.15f;
                w.x += Mathf.Sin(t * 1.8f + w.y * 2f) * 0.04f;
                w.y *= breathe;
                if (shake > 0f) { w.x += (Random.value - 0.5f) * shake; w.z += (Random.value - 0.5f) * shake; }
                into.Add(Pos + q * w);
            }
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

        private Dot[] _dots;
        private int _head, _count;
        private CharacterBody _body;
        private InputBankTest _input;
        private bool _local;
        private SvrDemon _demon;
        private readonly List<Vector3> _demonPoints = new List<Vector3>(256);

        private int _pulseLeft;
        private Vector3 _pulseAt;
        private float _pulseRange;
        private readonly List<HealthComponent> _tracked = new List<HealthComponent>();
        private readonly List<float> _trackedUntil = new List<float>();

        private Camera _dimCam;
        private int _origMask;
        private CameraClearFlags _origFlags;
        private Color _origBg;
        private bool _dimmed;
        private float _nextAudio;
        private GUIStyle _label;

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _input = GetComponent<InputBankTest>();
            _dots = new Dot[Mathf.Max(256, SvrPlugin.MaxDots.Value)];
            _demon = new SvrDemon();
        }

        private void OnDestroy() { RestoreCamera(); }
        private void OnDisable() { RestoreCamera(); }

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

        // ---------------------------------------------------------------- per frame

        private void Update()
        {
            if (!_body) return;
            _local = IsLocal();
            if (!_local) { RestoreCamera(); return; }
            if (!Alive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _demon.Tick(dt, _body);
            Scan();

            if (_pulseLeft > 0)
            {
                int n = Mathf.Min(_pulseLeft, 380);
                for (int i = 0; i < n; i++) Cast(_pulseAt + Vector3.up * 0.8f, Random.onUnitSphere, _pulseRange, SvrPlugin.DotLife.Value * 1.6f, true);
                _pulseLeft -= n;
            }

            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                HealthComponent hc = _tracked[i];
                if (!hc || !hc.alive || !hc.body || Time.time > _trackedUntil[i]) { _tracked.RemoveAt(i); _trackedUntil.RemoveAt(i); continue; }
                for (int k = 0; k < 3; k++)
                    AddDot(hc.body.corePosition + Random.insideUnitSphere * Mathf.Max(0.5f, hc.body.radius), new Color(1f, 0.15f, 0.1f, 1f), 0.4f, true);
            }

            if (SvrSenses.Hearing && Time.unscaledTime > _nextAudio) { _nextAudio = Time.unscaledTime + 2f; SvrAudio.Muffle(true); }
        }

        private void Scan()
        {
            if (!_input) return;
            float prox = _demon.Proximity(_body.corePosition);
            if (prox > 0.2f && Random.value < prox * 0.85f) return;      // the suit's scanner stutters and drops frames near it
            float sight = SvrSenses.SightMult;
            int rays = Mathf.RoundToInt(SvrPlugin.RaysPerFrame.Value * sight * (1f - 0.5f * prox));
            float range = SvrPlugin.ScanRange.Value * sight;
            Vector3 o = _input.aimOrigin;
            Vector3 aim = _input.aimDirection;
            float life = SvrPlugin.DotLife.Value * (sight < 1f ? 0.7f : 1f);
            for (int i = 0; i < rays; i++)
            {
                if (Random.value < 0.15f) Cast(o, Random.onUnitSphere, range * 0.35f, life, false);   // a little all around her
                else Cast(o, (aim + Random.insideUnitSphere * 0.75f).normalized, range, life, false);
            }
        }

        private void LateUpdate()
        {
            if (!_local || !_body) return;
            if (!SvrPlugin.DarkWorld.Value || !Alive) { RestoreCamera(); return; }
            Camera cam = Cam();
            if (!cam) return;
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
            DrawHud(scale);
            GUI.color = old;
        }

        private void DrawDots(Camera cam, float scale)
        {
            Texture2D white = Texture2D.whiteTexture;
            float now = Time.time;
            int len = _dots.Length;
            int drawn = 0;
            for (int k = 0; k < _count; k++)
            {
                int idx = ((_head - 1 - k) % len + len) % len;
                Dot d = _dots[idx];
                float age = now - d.T;
                if (age >= d.Life) continue;
                Vector3 sp = cam.WorldToScreenPoint(d.P);
                if (sp.z <= 0.3f || sp.x < -8f || sp.x > Screen.width + 8f || sp.y < -8f || sp.y > Screen.height + 8f) continue;
                if (_demon.InVoid(d.P, 2.2f)) continue;
                float fade = 1f - age / d.Life;
                float size = Mathf.Lerp(3.6f, 1.6f, Mathf.Clamp01(sp.z / 55f)) * scale * (SvrPlugin.DotSize.Value / 3f) * (d.Big ? 1.5f : 1f);
                GUI.color = new Color(d.C.r, d.C.g, d.C.b, d.C.a * fade);
                GUI.DrawTexture(new Rect(sp.x - size * 0.5f, Screen.height - sp.y - size * 0.5f, size, size), white);
                if (++drawn >= 4000) break;
            }
        }

        private void DrawDemon(Camera cam, float scale)
        {
            if (!_demon.Active) return;
            bool blocked = Physics.Linecast(cam.transform.position, _demon.Center, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            float vis = blocked ? 0.18f : 0.9f;
            _demon.FillWorld(_demonPoints, Time.time);
            Texture2D white = Texture2D.whiteTexture;
            float split = (2.5f + Random.value * 1.5f) * scale;
            for (int i = 0; i < _demonPoints.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(_demonPoints[i]);
                if (sp.z <= 0.3f) continue;
                float size = Mathf.Clamp(320f / sp.z, 2.2f, 8f) * scale;
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
}
