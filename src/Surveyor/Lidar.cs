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

        /// <summary>Restores one stolen sense (when the Shimmer Demon is vacuum-captured in the Ghost-Trap). Returns its name, or null if none were lost.</summary>
        public static string RestoreOne()
        {
            List<string> stolen = new List<string>();
            if (Touch) stolen.Add("TOUCH");
            if (Sight) stolen.Add("SIGHT");
            if (Hearing) stolen.Add("HEARING");
            if (Balance) stolen.Add("BALANCE");
            if (stolen.Count == 0) return null;
            string pick = stolen[Random.Range(0, stolen.Count)];
            if (pick == "HEARING") { Hearing = false; SvrAudio.Muffle(false); }
            else if (pick == "SIGHT") Sight = false;
            else if (pick == "TOUCH") Touch = false;
            else Balance = false;
            Version++;
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

        // Ghostbusters / Luigi's Mansion containment trap + proton tether + wrestling + payoff state
        public bool TrapActive;
        public Vector3 TrapPos;
        public float TrapUntil, TrapReadyAt;
        public float CaptureProgress; // 0 .. 1
        public bool TetherActive;
        public int StrainStacks;      // 0 .. 10 stacking "Containment Strain" debuff
        public float StrainDecay;
        public int PullDir;           // -1 = pulling Left (steer D), +1 = pulling Right (steer A), 0 = surging Back (steer S)
        public float PullSwitchTimer;
        public bool CounterSteering;
        public HealthComponent LockedEnemy; // optional Elite/Boss enemy locked by the Proton Tether
        public int StoredSpirits;
        public bool MinionActive;
        public float MinionUntil, MinionFireTimer;
        public Vector3 MinionPos;
        public string LastRestoredSense;
        public float LastRestoredUntil;

        public int MaxStoredSpirits { get { return SvrUpgrades.Has(SvrUpgrades.COMPRESSION_PRESS) ? 2 : 1; } }
        public Vector3 TetherTargetPos
        {
            get
            {
                if (LockedEnemy && LockedEnemy.alive && LockedEnemy.body) return LockedEnemy.body.corePosition;
                return _center;
            }
        }

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

        /// <summary>Its points in world space for this frame (posed, breathing, shaking when staggered, shrinking when vacuum-trapped).</summary>
        public void FillWorld(List<Vector3> into, float t)
        {
            into.Clear();
            BuildPose(t);
            Quaternion q = Quaternion.Euler(0f, Yaw, 0f);
            float shake = (State == Mode.Stagger || TetherActive) ? 0.08f : 0.012f;
            float shrink = TrapActive && CaptureProgress > 0.15f ? Mathf.Lerp(1.15f, 0.28f, Mathf.SmoothStep(0f, 1f, CaptureProgress)) : 1.15f;
            for (int i = 0; i < _local.Count; i++)
            {
                Vector3 w = _local[i] * shrink;
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
            if (!Active) return;
            if (TrapActive && Near(aim, 150f))
            {
                // When a Ghost-Trap is open, a Lance hit reels the Demon straight toward the trap and adds capture progress!
                Vector3 toTrap = TrapPos - Pos;
                toTrap.y = 0f;
                if (toTrap.sqrMagnitude > 0.01f) Pos += toTrap.normalized * Mathf.Min(12f, toTrap.magnitude * 0.65f);
                _stagger = Mathf.Max(_stagger, SvrPlugin.LanceStagger.Value);
                State = Mode.Stagger;
                CaptureProgress = Mathf.Clamp01(CaptureProgress + 0.35f);
                return;
            }
            if (Near(aim, 150f)) Knock(aim.origin, 10f, SvrPlugin.LanceStagger.Value);
        }

        public void DeployTrap(CharacterBody body, InputBankTest input)
        {
            // Active-Reload Compression Slam: if a trap is already active and CaptureProgress is in the Gold Zone [0.64 .. 0.88],
            // pressing [G] slams the Spectral Compression Press shut immediately for bonus healing & gold!
            if (TrapActive && CaptureProgress >= 0.64f && CaptureProgress <= 0.89f && (Active || (LockedEnemy && LockedEnemy.alive)))
            {
                if (body.healthComponent) body.healthComponent.HealFraction(0.35f, default);
                if (body.master) body.master.GiveMoney((uint)Mathf.RoundToInt(45f * (Run.instance ? Run.instance.difficultyCoefficient : 1f)));
                CaptureDemon(body, true);
                return;
            }

            Vector3 fwd = input ? input.aimDirection : body.gameObject.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 p = body.footPosition + fwd * 8.5f;
            p.y = GroundY(p, body.footPosition.y);
            TrapPos = p;
            TrapActive = true;
            TrapUntil = Time.time + SvrPlugin.TrapDuration.Value;
            TrapReadyAt = Time.time + SvrPlugin.TrapCooldown.Value;
            CaptureProgress = 0f;
            TetherActive = false;
            LockedEnemy = null;
            PullDir = Random.value < 0.5f ? -1 : 1;
            PullSwitchTimer = 1.1f;
            Util.PlaySound("Play_engi_M2_throw", body.gameObject);

            // If the Demon isn't currently on the field (and we have a stolen sense to recover, or want to bust it),
            // the Ghost-Trap's ectoplasmic lure immediately draws the Demon out nearby!
            if (!Active)
            {
                float a = Random.value * 6.28f;
                Vector3 sp = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(16f, 24f);
                sp.y = GroundY(sp, body.footPosition.y);
                Pos = sp;
                _lastPos = sp;
                State = Mode.Stalk;
                _modeTimer = 14f;
                _chain = 0f;
                _center = Pos + Vector3.up * 1.4f;
            }
        }

        public bool RedeployMinion(CharacterBody body)
        {
            if (StoredSpirits <= 0 || !body) return false;
            StoredSpirits--;
            MinionActive = true;
            MinionUntil = Time.time + 30f;
            MinionPos = body.corePosition + body.gameObject.transform.forward * 2.8f + Vector3.up * 1.2f;
            MinionFireTimer = 0.2f;
            Util.PlaySound("Play_UI_obj_casinoChest_open", body.gameObject);
            LastRestoredSense = "SPECTRAL GUARDIAN DEPLOYED (30s)";
            LastRestoredUntil = Time.time + 3.0f;
            return true;
        }

        public bool CashInAtTeleporter(CharacterBody body, Vector3 tpPos)
        {
            if (StoredSpirits <= 0 || !body) return false;
            StoredSpirits--;
            Util.PlaySound("Play_UI_shrineSuccess", body.gameObject);
            float diff = Run.instance ? Mathf.Max(1f, Run.instance.difficultyCoefficient) : 1f;
            uint gold = (uint)Mathf.RoundToInt(125f * diff);
            if (body.master) body.master.GiveMoney(gold);
            if (body.healthComponent) body.healthComponent.HealFraction(0.45f, default);

            try
            {
                if (UnityEngine.Networking.NetworkServer.active && Run.instance && Run.instance.availableTier1DropList != null && Run.instance.availableTier1DropList.Count > 0)
                {
                    List<PickupIndex> pool = (Random.value < 0.35f && Run.instance.availableTier2DropList.Count > 0)
                        ? Run.instance.availableTier2DropList
                        : Run.instance.availableTier1DropList;
                    PickupIndex drop = pool[Random.Range(0, pool.Count)];
                    PickupDropletController.CreatePickupDroplet(drop, tpPos + Vector3.up * 2.2f, Vector3.up * 14f + Random.insideUnitSphere * 3f);
                }
            }
            catch { }

            LastRestoredSense = "TELEPORTER CASH-IN (+$" + gold + " & LOOT)";
            LastRestoredUntil = Time.time + 3.5f;
            return true;
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

        public void Tick(float dt, CharacterBody body, InputBankTest input)
        {
            if (TrapActive && Time.time >= TrapUntil)
            {
                TrapActive = false;
                TetherActive = false;
                LockedEnemy = null;
                CaptureProgress = 0f;
            }

            // Friendly Spectral / Holographic Guardian minion (deployed with [H] for 30s)
            if (MinionActive)
            {
                if (Time.time >= MinionUntil)
                {
                    MinionActive = false;
                }
                else
                {
                    float orbitAng = Time.time * 1.4f;
                    Vector3 goal = body.corePosition + new Vector3(Mathf.Cos(orbitAng) * 3.4f, 1.4f + Mathf.Sin(Time.time * 3f) * 0.25f, Mathf.Sin(orbitAng) * 3.4f);
                    MinionPos = Vector3.Lerp(MinionPos, goal, 1f - Mathf.Exp(-6f * dt));
                    MinionFireTimer -= dt;
                    if (MinionFireTimer <= 0f)
                    {
                        MinionFireTimer = 0.65f;
                        try
                        {
                            SphereSearch ss = new SphereSearch { origin = MinionPos, radius = 38f, mask = LayerIndex.entityPrecise.mask };
                            ss.RefreshCandidates();
                            ss.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex));
                            ss.FilterCandidatesByDistinctHurtBoxEntities();
                            foreach (HurtBox hb in ss.GetHurtBoxes())
                            {
                                if (hb && hb.healthComponent && hb.healthComponent.alive)
                                {
                                    RoR2.Orbs.LightningOrb orb = new RoR2.Orbs.LightningOrb
                                    {
                                        origin = MinionPos + Vector3.up * 0.8f,
                                        target = hb,
                                        attacker = body.gameObject,
                                        damageValue = body.damage * 2.8f,
                                        isCrit = body.RollCrit(),
                                        teamIndex = body.teamComponent.teamIndex,
                                        lightningType = RoR2.Orbs.LightningOrb.LightningType.Ukulele,
                                        procCoefficient = 0.7f
                                    };
                                    RoR2.Orbs.OrbManager.instance.AddOrb(orb);
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            // Singularity Trap Core upgrade: deployed Ghost-Trap pulls nearby enemies and flying pests inward
            if (TrapActive && SvrUpgrades.Has(SvrUpgrades.SINGULARITY_CORE))
            {
                try
                {
                    SphereSearch ss = new SphereSearch { origin = TrapPos, radius = 15f, mask = LayerIndex.entityPrecise.mask };
                    ss.RefreshCandidates();
                    ss.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(body.teamComponent.teamIndex));
                    ss.FilterCandidatesByDistinctHurtBoxEntities();
                    foreach (HurtBox hb in ss.GetHurtBoxes())
                    {
                        CharacterBody eb = hb && hb.healthComponent ? hb.healthComponent.body : null;
                        if (!eb || eb == body) continue;
                        Vector3 pull = (TrapPos + Vector3.up * 0.8f) - eb.corePosition;
                        if (pull.sqrMagnitude > 1f)
                        {
                            if (eb.characterMotor) eb.characterMotor.rootMotion += pull.normalized * (11f * dt);
                            else if (eb.rigidbody) eb.rigidbody.velocity = Vector3.Lerp(eb.rigidbody.velocity, pull.normalized * 10f, 5f * dt);
                        }
                    }
                }
                catch { }
            }

            // Passive barrier trickle while holding a spirit with Spectral Compression Press
            if (StoredSpirits > 0 && SvrUpgrades.Has(SvrUpgrades.COMPRESSION_PRESS) && body.healthComponent)
            {
                body.healthComponent.AddBarrier(body.maxBarrier * 0.015f * dt);
            }

            if (!SvrPlugin.DemonEnabled.Value) { State = Mode.Dormant; TetherActive = false; return; }
            _stageTime += dt;
            if (_chainT > 0f) { _chainT -= dt; if (_chainT <= 0f) _chain = 0f; }
            Vector3 pp = body.footPosition;

            if (State == Mode.Dormant)
            {
                TetherActive = false;
                if (_stageTime >= SvrPlugin.DemonFirstSpawn.Value) Spawn(pp);
                return;
            }
            if (State == Mode.Gone)
            {
                TetherActive = false;
                _goneTimer -= dt;
                if (_goneTimer <= 0f && SvrSenses.Lost < SvrPlugin.MaxSensesLost.Value) Spawn(pp);
                return;
            }

            // Ghostbusters Proton-Tether & Luigi's Mansion Vacuum Trap + WASD Wrestling Match logic:
            TetherActive = false;
            CounterSteering = false;
            float trapDist = TrapActive ? Vector3.Distance(new Vector3(Pos.x, TrapPos.y, Pos.z), TrapPos) : 999f;
            if (input != null)
            {
                Ray aim = new Ray(input.aimOrigin, input.aimDirection);
                Vector3 toDemon = _center - aim.origin;
                float dDemon = toDemon.magnitude;
                float angle = dDemon > 0.1f ? Vector3.Angle(aim.direction, toDemon) : 0f;
                bool firing = input.skill1.down || input.skill2.down;
                if (firing && dDemon < 65f && angle < 25f)
                {
                    StrainDecay = 1.6f;
                    if (Random.value < dt * 4.5f && StrainStacks < 10) StrainStacks++;
                    if (TrapActive)
                    {
                        TetherActive = true;
                        LockedEnemy = null;
                        State = Mode.Stagger;
                        _stagger = Mathf.Max(_stagger, 0.45f);
                        if (SvrUpgrades.Has(SvrUpgrades.AETHER_LEECH) && body.healthComponent)
                            body.healthComponent.AddBarrier(body.maxBarrier * 0.045f * dt);
                    }
                }
                else if (TrapActive && firing)
                {
                    // Check if aiming at a weakened Elite / Boss near the trap
                    try
                    {
                        BullseyeSearch bs = new BullseyeSearch
                        {
                            searchOrigin = aim.origin,
                            searchDirection = aim.direction,
                            maxDistanceFilter = 55f,
                            maxAngleFilter = 22f,
                            teamMaskFilter = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex),
                            filterByLoS = false,
                            sortMode = BullseyeSearch.SortMode.Angle
                        };
                        bs.RefreshCandidates();
                        foreach (HurtBox hb in bs.GetResults())
                        {
                            HealthComponent hc = hb ? hb.healthComponent : null;
                            if (hc && hc.alive && hc.body && (hc.body.isElite || hc.body.isBoss || hc.body.isChampion) && hc.combinedHealthFraction < 0.65f)
                            {
                                LockedEnemy = hc;
                                TetherActive = true;
                                StrainDecay = 1.6f;
                                if (Random.value < dt * 4.5f && StrainStacks < 10) StrainStacks++;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            if (StrainDecay > 0f)
            {
                StrainDecay -= dt;
                if (StrainDecay <= 0f) StrainStacks = 0;
            }

            // Wrestling Match: target bucks Left (-1), Right (+1), or Back (0); player counter-steers with WASD!
            if (TrapActive && TetherActive)
            {
                PullSwitchTimer -= dt;
                if (PullSwitchTimer <= 0f)
                {
                    PullSwitchTimer = Random.Range(0.95f, 1.45f);
                    int r = Random.Range(0, 3);
                    PullDir = r == 0 ? -1 : (r == 1 ? 1 : 0);
                }
                if (PullDir < 0) CounterSteering = Input.GetKey(KeyCode.D);
                else if (PullDir > 0) CounterSteering = Input.GetKey(KeyCode.A);
                else CounterSteering = Input.GetKey(KeyCode.S);
            }

            if (TrapActive && (TetherActive || trapDist < 9.5f))
            {
                Vector3 targetPos = (LockedEnemy && LockedEnemy.alive && LockedEnemy.body) ? LockedEnemy.body.footPosition : Pos;
                Vector3 toTrap = TrapPos - targetPos;
                Vector3 flatTrap = new Vector3(toTrap.x, 0f, toTrap.z);
                float td = flatTrap.magnitude;

                // Spiral vortex pull into the trap (boosted by Containment Strain stacks and WASD Counter-Steering)
                float strainMult = 1f + StrainStacks * 0.06f;
                float steerMult = CounterSteering ? 1.85f : 1f;
                Vector3 pullDir = td > 0.05f ? flatTrap / td : Vector3.zero;
                Vector3 swirl = new Vector3(-pullDir.z, 0f, pullDir.x);
                float pullSpeed = ((TetherActive ? 13.5f : 5.5f) + CaptureProgress * 7f) * strainMult * (CounterSteering ? 1.35f : 1f);

                if (LockedEnemy && LockedEnemy.alive && LockedEnemy.body)
                {
                    if (LockedEnemy.body.characterMotor)
                        LockedEnemy.body.characterMotor.rootMotion += (pullDir + swirl * 0.25f) * (pullSpeed * 0.8f * dt);
                }
                else
                {
                    Pos += (pullDir + swirl * (TetherActive ? 0.38f : 0.2f)) * pullSpeed * dt;
                    Yaw += (TetherActive ? 540f : 220f) * dt;
                    Pos.y = Mathf.MoveTowards(Pos.y, TrapPos.y + (1f - CaptureProgress) * 0.6f, 12f * dt);
                    _center = Pos + Vector3.up * Mathf.Lerp(1.4f, 0.35f, CaptureProgress);
                    Animate(dt);
                }

                float pullDur = Mathf.Max(0.8f, SvrPlugin.TrapPullSeconds.Value);
                float rate = (TetherActive ? 1f : 0.35f) * (td < 4.5f ? 1.35f : 0.85f) * strainMult * steerMult / pullDur;
                CaptureProgress = Mathf.Clamp01(CaptureProgress + rate * dt);

                if (CaptureProgress >= 1f || (td < 1.35f && CaptureProgress > 0.65f))
                {
                    CaptureDemon(body, false);
                }
                return;
            }
            else if (TrapActive && CaptureProgress > 0f)
            {
                CaptureProgress = Mathf.Max(0f, CaptureProgress - dt * 0.25f);
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

            float strainSlow = Mathf.Lerp(1f, 0.45f, StrainStacks / 10f);
            if (speed > 0f && wish.sqrMagnitude > 0.0001f) Pos += wish.normalized * (speed * strainSlow * dt);

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

        private void CaptureDemon(CharacterBody body, bool compressionBonus)
        {
            TrapActive = false;
            TetherActive = false;
            CaptureProgress = 0f;
            StrainStacks = 0;

            if (LockedEnemy && LockedEnemy.alive)
            {
                try
                {
                    if (UnityEngine.Networking.NetworkServer.active)
                        LockedEnemy.Suicide(body.gameObject, body.gameObject, DamageType.VoidDeath);
                }
                catch { }
                LockedEnemy = null;
            }
            else
            {
                State = Mode.Gone;
                _goneTimer = SvrPlugin.DemonRespawn.Value * 1.25f;
            }

            StoredSpirits = Mathf.Min(MaxStoredSpirits, StoredSpirits + 1);
            Util.PlaySound("Play_UI_obj_casinoChest_open", body.gameObject);
            try
            {
                GameObject fx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
                if (fx) EffectManager.SpawnEffect(fx, new EffectData { origin = TrapPos + Vector3.up * 0.5f, scale = 5f }, false);
            }
            catch { }

            string restored = SvrSenses.RestoreOne();
            string slamTag = compressionBonus ? " [PERFECT COMPRESSION SLAM!]" : "";
            LastRestoredSense = (restored ?? "SPIRIT") + slamTag + " · [H] GUARDIAN / [B] CASH-IN";
            LastRestoredUntil = Time.time + 4.2f;
            if (restored != null)
            {
                try { Chat.AddMessage("<color=#58ff8a>ENTITY CONTAINED!" + slamTag + " Restored lost sense: " + restored + ". Press [H] to deploy Guardian or [B] at Teleporter to cash in!</color>"); } catch { }
            }
            else
            {
                try { Chat.AddMessage("<color=#58ff8a>ENTITY CONTAINED!" + slamTag + " Stored in Containment Cell (" + StoredSpirits + "/" + MaxStoredSpirits + "). Press [H] for Guardian or [B] at Teleporter!</color>"); } catch { }
            }
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

    // ================================================================ 1. THE HOLOGRAM DISCOVERY LOOP (SvrLiDARScanTarget)
    // Tagged onto containers, chests, shrines, shops, dropped items, teleporters, and hidden upgrade caches.
    // Hidden/dormant by default; when an expanding LiDAR pulse wave or scan beam washes over it, it materializes
    // as a glowing 3D wireframe hologram of its exact 3D shape (with zero boring floating 2D brackets or text boxes).

    public class SvrLiDARScanTarget : MonoBehaviour
    {
        public class WirePart
        {
            public Transform Tr;
            public Vector3[] EdgeA;
            public Vector3[] EdgeB;
            public Vector3[] Verts;
        }

        public int Kind;          // 0 = chest/shop, 1 = shrine/drone, 2 = barrel, 3 = item/pickup, 4 = teleporter, 5 = hidden tech cache
        public int SubShape;      // 0=smallChest, 1=largeChest, 2=bloodShrine, 3=chanceShrine, 4=mountainShrine, 5=printer, 6=shop, 7=barrel, 8=item, 9=teleporter, 10=dropPod, 11=hexCrate, 12=servicePanel, 13=drone
        public int UpgradeId = -1;
        public string Label = "";
        public Color HoloColor = new Color(0.28f, 0.96f, 1f, 1f);
        public Vector3 LocalCenter = new Vector3(0f, 0.7f, 0f);
        public Vector3 Extents = new Vector3(0.8f, 0.8f, 0.8f);

        public float RevealUntil = -10f;
        public float LastWaveHit = -10f;
        public float PeakIntensity;
        public bool Built;
        public float NextBuildAttempt;
        public readonly List<WirePart> Parts = new List<WirePart>(6);

        private Renderer[] _renderers;
        private Material[] _origMats;
        private Material _holoMat;
        private bool _matsSwapped;
        private static Shader _hgShader;

        public bool IsRevealed => Time.time < RevealUntil;

        public float CurrentAlpha(float now)
        {
            if (now >= RevealUntil) return 0f;
            float sinceHit = now - LastWaveHit;
            float rampIn = Mathf.Clamp01(sinceHit / 0.18f);
            float rem = RevealUntil - now;
            float fadeOut = Mathf.Clamp01(rem / 1.4f);
            return rampIn * fadeOut * Mathf.Clamp01(PeakIntensity);
        }

        public void OnWaveWash(float duration, float intensity)
        {
            float now = Time.time;
            if (now - LastWaveHit > 0.35f) LastWaveHit = now;
            RevealUntil = Mathf.Max(RevealUntil, now + duration);
            PeakIntensity = Mathf.Max(PeakIntensity, Mathf.Clamp01(intensity));
            if (!Built && now >= NextBuildAttempt) BuildWireframe();
            ApplyHologramMaterial(true);
        }

        public void ApplyHologramMaterial(bool active)
        {
            try
            {
                if (_renderers == null)
                {
                    _renderers = GetComponentsInChildren<Renderer>(false);
                    if (_renderers != null && _renderers.Length > 0)
                    {
                        _origMats = new Material[_renderers.Length];
                        for (int i = 0; i < _renderers.Length; i++)
                            if (_renderers[i]) _origMats[i] = _renderers[i].sharedMaterial;
                    }
                }
                if (_renderers == null || _renderers.Length == 0) return;

                if (active && !_matsSwapped)
                {
                    if (!_holoMat)
                    {
                        if (!_hgShader) _hgShader = Shader.Find("Hopoo Games/Deferred/Standard");
                        if (_hgShader)
                        {
                            _holoMat = new Material(_hgShader) { name = "SvrHoloTargetMat" };
                            if (_holoMat.HasProperty("_Color")) _holoMat.SetColor("_Color", HoloColor * 0.35f);
                            if (_holoMat.HasProperty("_EmColor")) _holoMat.SetColor("_EmColor", HoloColor);
                            if (_holoMat.HasProperty("_EmPower")) _holoMat.SetFloat("_EmPower", 3.2f);
                            if (_holoMat.HasProperty("_FresnelBoost")) _holoMat.SetFloat("_FresnelBoost", 4.5f);
                        }
                    }
                    if (_holoMat)
                    {
                        if (_holoMat.HasProperty("_EmColor")) _holoMat.SetColor("_EmColor", HoloColor);
                        for (int i = 0; i < _renderers.Length; i++)
                        {
                            Renderer r = _renderers[i];
                            if (!r || (r is ParticleSystemRenderer)) continue;
                            r.sharedMaterial = _holoMat;
                        }
                        _matsSwapped = true;
                    }
                }
                else if (!active && _matsSwapped)
                {
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        if (_renderers[i] && _origMats != null && i < _origMats.Length && _origMats[i])
                            _renderers[i].sharedMaterial = _origMats[i];
                    }
                    _matsSwapped = false;
                }
            }
            catch { }
        }

        private void OnDisable()
        {
            if (_matsSwapped) ApplyHologramMaterial(false);
        }

        public void BuildWireframe()
        {
            NextBuildAttempt = Time.time + 0.5f;
            Parts.Clear();

            try
            {
                // 1) Extract real 3D mesh edges & vertices from MeshFilters / SkinnedMeshRenderers / PickupDisplay
                Transform searchRoot = transform;
                if (Kind == 3)
                {
                    PickupDisplay pd = GetComponentInChildren<PickupDisplay>();
                    if (pd)
                    {
                        searchRoot = pd.transform;
                        if (pd.modelRenderers != null)
                        {
                            for (int i = 0; i < pd.modelRenderers.Count; i++)
                            {
                                Renderer r = pd.modelRenderers[i];
                                if (!r) continue;
                                MeshFilter mf = r.GetComponent<MeshFilter>();
                                Mesh m = mf ? mf.sharedMesh : (r is SkinnedMeshRenderer smr ? smr.sharedMesh : null);
                                if (m) AddMeshWirePart(r.transform, m);
                            }
                        }
                    }
                }

                if (Parts.Count == 0 && Kind != 5)
                {
                    MeshFilter[] mfs = searchRoot.GetComponentsInChildren<MeshFilter>(false);
                    for (int i = 0; i < mfs.Length && Parts.Count < 6; i++)
                    {
                        MeshFilter mf = mfs[i];
                        if (!mf || !mf.sharedMesh) continue;
                        string gn = mf.gameObject.name.ToLowerInvariant();
                        if (gn.Contains("area") || gn.Contains("indicator") || gn.Contains("wave") || gn.Contains("beam") || gn.Contains("glow") || gn.Contains("flash"))
                            continue;
                        AddMeshWirePart(mf.transform, mf.sharedMesh);
                    }
                    SkinnedMeshRenderer[] smrs = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                    for (int i = 0; i < smrs.Length && Parts.Count < 6; i++)
                    {
                        SkinnedMeshRenderer smr = smrs[i];
                        if (!smr || !smr.sharedMesh) continue;
                        AddMeshWirePart(smr.transform, smr.sharedMesh);
                    }
                }

                // 2) Add signature 3D architectural wireframe hologram lines so every chest, blood shrine, chance shrine,
                // printer, multishop, drop pod, hex crate, and service panel has an unmistakable 3D wireframe hologram shape!
                List<Vector3> archA = new List<Vector3>(96);
                List<Vector3> archB = new List<Vector3>(96);
                List<Vector3> archV = new List<Vector3>(48);
                BuildArchitecturalWireframe(SubShape, Extents, archA, archB, archV);
                if (archA.Count > 0)
                {
                    Parts.Add(new WirePart
                    {
                        Tr = transform,
                        EdgeA = archA.ToArray(),
                        EdgeB = archB.ToArray(),
                        Verts = archV.ToArray()
                    });
                }

                if (Parts.Count > 0) Built = true;
            }
            catch { }
        }

        private void AddMeshWirePart(Transform tr, Mesh m)
        {
            if (!tr || !m) return;
            SvrLidar.CachedMeshData cmd = SvrLidar.GetCachedMesh(m);
            if (cmd != null && cmd.EdgeA != null && cmd.EdgeA.Length > 0)
            {
                Parts.Add(new WirePart
                {
                    Tr = tr,
                    EdgeA = cmd.EdgeA,
                    EdgeB = cmd.EdgeB,
                    Verts = cmd.Verts
                });
            }
        }

        private static void AddLine(List<Vector3> a, List<Vector3> b, List<Vector3> v, Vector3 p0, Vector3 p1)
        {
            a.Add(p0);
            b.Add(p1);
            if (v.Count < 64) v.Add(p0);
        }

        private static void AddBoxWire(List<Vector3> a, List<Vector3> b, List<Vector3> v, Vector3 center, Vector3 half)
        {
            Vector3 p000 = center + new Vector3(-half.x, -half.y, -half.z);
            Vector3 p100 = center + new Vector3(half.x, -half.y, -half.z);
            Vector3 p101 = center + new Vector3(half.x, -half.y, half.z);
            Vector3 p001 = center + new Vector3(-half.x, -half.y, half.z);
            Vector3 p010 = center + new Vector3(-half.x, half.y, -half.z);
            Vector3 p110 = center + new Vector3(half.x, half.y, -half.z);
            Vector3 p111 = center + new Vector3(half.x, half.y, half.z);
            Vector3 p011 = center + new Vector3(-half.x, half.y, half.z);

            AddLine(a, b, v, p000, p100); AddLine(a, b, v, p100, p101); AddLine(a, b, v, p101, p001); AddLine(a, b, v, p001, p000);
            AddLine(a, b, v, p010, p110); AddLine(a, b, v, p110, p111); AddLine(a, b, v, p111, p011); AddLine(a, b, v, p011, p010);
            AddLine(a, b, v, p000, p010); AddLine(a, b, v, p100, p110); AddLine(a, b, v, p101, p111); AddLine(a, b, v, p001, p011);
        }

        private static void AddRingWire(List<Vector3> a, List<Vector3> b, List<Vector3> v, Vector3 center, float rx, float rz, int segs)
        {
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= segs; i++)
            {
                float ang = (i % segs) * (6.2831853f / segs);
                Vector3 cur = center + new Vector3(Mathf.Cos(ang) * rx, 0f, Mathf.Sin(ang) * rz);
                if (i > 0) AddLine(a, b, v, prev, cur);
                prev = cur;
            }
        }

        private static void BuildArchitecturalWireframe(int subShape, Vector3 ext, List<Vector3> a, List<Vector3> b, List<Vector3> v)
        {
            float sx = Mathf.Clamp(ext.x, 0.45f, 2.2f);
            float sy = Mathf.Clamp(ext.y, 0.45f, 2.4f);
            float sz = Mathf.Clamp(ext.z, 0.45f, 2.2f);

            switch (subShape)
            {
                case 0: // SMALL CHEST / LOCKBOX: beveled lower coffer + curved barrel-vault lid + center lock clasp
                case 1: // LARGE / LEGENDARY CHEST: wider reinforced coffer + dual structural ribs + lock plate
                {
                    float w = subShape == 1 ? Mathf.Max(0.95f, sx * 1.15f) : Mathf.Max(0.68f, sx * 0.85f);
                    float h = subShape == 1 ? Mathf.Max(0.85f, sy * 0.9f) : Mathf.Max(0.62f, sy * 0.75f);
                    float d = Mathf.Max(0.48f, sz * 0.68f);
                    // Base chest box
                    AddBoxWire(a, b, v, new Vector3(0f, h * 0.35f, 0f), new Vector3(w, h * 0.35f, d));
                    // Curved vaulted chest lid (arch segments across X)
                    int ribs = subShape == 1 ? 4 : 3;
                    for (int r = 0; r < ribs; r++)
                    {
                        float x = Mathf.Lerp(-w, w, r / (float)(ribs - 1));
                        Vector3 prev = Vector3.zero;
                        for (int s = 0; s <= 6; s++)
                        {
                            float t = s / 6f;
                            float ang = Mathf.Lerp(0f, 3.14159f, t);
                            Vector3 pt = new Vector3(x, h * 0.7f + Mathf.Sin(ang) * (h * 0.38f), -Mathf.Cos(ang) * d);
                            if (s > 0) AddLine(a, b, v, prev, pt);
                            prev = pt;
                        }
                    }
                    // Crown ridge + front lock clasp
                    AddLine(a, b, v, new Vector3(-w, h * 1.08f, 0f), new Vector3(w, h * 1.08f, 0f));
                    AddBoxWire(a, b, v, new Vector3(0f, h * 0.68f, d * 1.03f), new Vector3(w * 0.18f, h * 0.14f, 0.04f));
                    break;
                }
                case 2: // BLOOD SHRINE: stepped sacrificial altar + twin curved demon horns + central blood chalice
                {
                    AddRingWire(a, b, v, new Vector3(0f, 0.1f, 0f), 1.05f, 1.05f, 8);
                    AddBoxWire(a, b, v, new Vector3(0f, 0.65f, 0f), new Vector3(0.55f, 0.55f, 0.55f));
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 prev = new Vector3(side * 0.45f, 0.9f, 0f);
                        for (int s = 1; s <= 6; s++)
                        {
                            float u = s / 6f;
                            Vector3 cur = new Vector3(side * (0.45f + Mathf.Sin(u * 2.2f) * 0.65f), 0.9f + u * 1.45f, Mathf.Cos(u * 1.8f) * 0.2f);
                            AddLine(a, b, v, prev, cur);
                            prev = cur;
                        }
                    }
                    break;
                }
                case 3: // CHANCE / COMBAT / NEWT SHRINE: octagonal shrine pillar + upper floating diamond idol
                case 4: // MOUNTAIN SHRINE: three angled boss claw prongs around a central obelisk
                {
                    AddRingWire(a, b, v, new Vector3(0f, 0.12f, 0f), 0.95f, 0.95f, 8);
                    AddRingWire(a, b, v, new Vector3(0f, 1.15f, 0f), 0.55f, 0.55f, 6);
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * (6.2831853f / 6f);
                        AddLine(a, b, v,
                            new Vector3(Mathf.Cos(ang) * 0.85f, 0.12f, Mathf.Sin(ang) * 0.85f),
                            new Vector3(Mathf.Cos(ang) * 0.55f, 1.15f, Mathf.Sin(ang) * 0.55f));
                    }
                    // Floating diamond / claw crown at top
                    Vector3 top = new Vector3(0f, 2.25f, 0f);
                    Vector3 bot = new Vector3(0f, 1.25f, 0f);
                    for (int k = 0; k < 4; k++)
                    {
                        float ang = k * 1.570796f;
                        Vector3 mid = new Vector3(Mathf.Cos(ang) * 0.5f, 1.75f, Mathf.Sin(ang) * 0.5f);
                        AddLine(a, b, v, bot, mid);
                        AddLine(a, b, v, mid, top);
                    }
                    break;
                }
                case 5: // 3D PRINTER / SCRAPPER: industrial fabricator box + angled display hood + item output tray
                {
                    AddBoxWire(a, b, v, new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.55f, 0.65f));
                    AddBoxWire(a, b, v, new Vector3(-0.25f, 1.35f, -0.1f), new Vector3(0.48f, 0.32f, 0.42f));
                    AddRingWire(a, b, v, new Vector3(0.35f, 1.15f, 0.2f), 0.35f, 0.35f, 8);
                    break;
                }
                case 6: // MULTISHOP TERMINAL: tall hexagonal kiosk + 3 display bays
                {
                    AddRingWire(a, b, v, new Vector3(0f, 0.1f, 0f), 1.1f, 1.1f, 6);
                    AddRingWire(a, b, v, new Vector3(0f, 2.1f, 0f), 0.9f, 0.9f, 6);
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = k * (6.2831853f / 3f);
                        Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                        AddBoxWire(a, b, v, dir * 0.65f + new Vector3(0f, 1.15f, 0f), new Vector3(0.35f, 0.65f, 0.35f));
                    }
                    break;
                }
                case 10: // HIDDEN TECH CACHE — DROP POD: angled orbital capsule + 3 landing fins + service hatch
                {
                    AddRingWire(a, b, v, new Vector3(0f, 0.15f, 0f), 0.85f, 0.85f, 8);
                    AddRingWire(a, b, v, new Vector3(0f, 1.15f, 0f), 0.72f, 0.72f, 8);
                    Vector3 nose = new Vector3(0f, 2.05f, 0f);
                    for (int k = 0; k < 8; k++)
                    {
                        float ang = k * (6.2831853f / 8f);
                        Vector3 bPt = new Vector3(Mathf.Cos(ang) * 0.85f, 0.15f, Mathf.Sin(ang) * 0.85f);
                        Vector3 mPt = new Vector3(Mathf.Cos(ang) * 0.72f, 1.15f, Mathf.Sin(ang) * 0.72f);
                        AddLine(a, b, v, bPt, mPt);
                        AddLine(a, b, v, mPt, nose);
                        if (k % 2 == 0)
                        {
                            Vector3 fin = new Vector3(Mathf.Cos(ang) * 1.25f, 0.05f, Mathf.Sin(ang) * 1.25f);
                            AddLine(a, b, v, mPt, fin);
                            AddLine(a, b, v, bPt, fin);
                        }
                    }
                    break;
                }
                case 11: // HIDDEN TECH CACHE — HEX SUPPLY CRATE: heavy octagonal tech crate + cross-braced lid
                {
                    AddRingWire(a, b, v, new Vector3(0f, 0.08f, 0f), 0.82f, 0.82f, 6);
                    AddRingWire(a, b, v, new Vector3(0f, 0.98f, 0f), 0.82f, 0.82f, 6);
                    for (int k = 0; k < 6; k++)
                    {
                        float ang = k * (6.2831853f / 6f);
                        Vector3 p0 = new Vector3(Mathf.Cos(ang) * 0.82f, 0.08f, Mathf.Sin(ang) * 0.82f);
                        Vector3 p1 = new Vector3(Mathf.Cos(ang) * 0.82f, 0.98f, Mathf.Sin(ang) * 0.82f);
                        AddLine(a, b, v, p0, p1);
                        AddLine(a, b, v, p1, new Vector3(0f, 1.18f, 0f));
                    }
                    break;
                }
                case 12: // HIDDEN TECH CACHE — SERVICE PANEL: upright engineering terminal + conduit pipes + holo-screen
                {
                    AddBoxWire(a, b, v, new Vector3(0f, 0.85f, 0f), new Vector3(0.58f, 0.82f, 0.22f));
                    AddBoxWire(a, b, v, new Vector3(0f, 1.05f, 0.24f), new Vector3(0.42f, 0.36f, 0.04f));
                    AddLine(a, b, v, new Vector3(-0.35f, 1.05f, 0.28f), new Vector3(0.35f, 1.05f, 0.28f));
                    AddLine(a, b, v, new Vector3(-0.48f, 1.67f, 0f), new Vector3(-0.48f, 2.25f, 0f));
                    break;
                }
                default: // BARREL / DRONE / ITEM PEDESTAL
                {
                    if (subShape == 8) // ITEM: sleek ground holo-pedestal ring
                    {
                        AddRingWire(a, b, v, new Vector3(0f, 0.06f, 0f), 0.42f, 0.42f, 8);
                    }
                    else
                    {
                        AddRingWire(a, b, v, new Vector3(0f, 0.08f, 0f), 0.52f, 0.52f, 8);
                        AddRingWire(a, b, v, new Vector3(0f, 1.05f, 0f), 0.48f, 0.48f, 8);
                        for (int k = 0; k < 4; k++)
                        {
                            float ang = k * 1.570796f;
                            AddLine(a, b, v,
                                new Vector3(Mathf.Cos(ang) * 0.52f, 0.08f, Mathf.Sin(ang) * 0.52f),
                                new Vector3(Mathf.Cos(ang) * 0.48f, 1.05f, Mathf.Sin(ang) * 0.48f));
                        }
                    }
                    break;
                }
            }
        }
    }

    // ================================================================ 2. FULLY ANIMATED SKELETAL LIDAR RIG (Characters, Monsters & Bosses)
    // Hooks into RoR2's active skeletal transforms (Transform bones, HurtBox limb volumes, and SkinnedMeshRenderer skinning matrices)
    // to render a real-time animated point-cloud & 3D wireframe mesh with zero delay through walls and particle smoke.

    public class SvrSkeletalRig
    {
        public struct BoneLink
        {
            public Transform A;
            public Transform B;
            public float Radius;
        }

        public struct LimbVolume
        {
            public Transform Tr;
            public Vector3 LocalCenter;
            public float Radius;
            public float HalfHeight;
            public float Height;
            public int Axis; // 0=X, 1=Y, 2=Z
        }

        public struct RigidAttachment
        {
            public Transform Tr;
            public Vector3[] EdgeA;
            public Vector3[] EdgeB;
        }

        public CharacterBody Body;
        public Transform ModelRoot;
        public SkinnedMeshRenderer PrimarySmr;
        public readonly List<BoneLink> Bones = new List<BoneLink>(32);
        public readonly List<LimbVolume> Limbs = new List<LimbVolume>(20);
        public readonly List<Transform> JointNodes = new List<Transform>(24);
        public readonly List<SkinnedMeshRenderer> SkinnedMeshes = new List<SkinnedMeshRenderer>(4);
        public readonly List<RigidAttachment> Attachments = new List<RigidAttachment>(6);
        public readonly List<Vector3> BakedWorldVerts = new List<Vector3>(96);
        public float ScannedUntil;
        public float LastScanHit;
        public float NextBakeTime;
        public bool Initialized;

        public static SvrSkeletalRig Build(CharacterBody body)
        {
            if (!body) return null;
            SvrSkeletalRig rig = new SvrSkeletalRig
            {
                Body = body,
                Initialized = true
            };

            ModelLocator ml = body.modelLocator;
            rig.ModelRoot = ml ? ml.modelTransform : null;
            if (!rig.ModelRoot) return rig;

            try
            {
                // 1) Hook active skeletal Transform bones from SkinnedMeshRenderers
                SkinnedMeshRenderer[] smrs = rig.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                HashSet<Transform> boneSet = new HashSet<Transform>();
                for (int s = 0; s < smrs.Length; s++)
                {
                    SkinnedMeshRenderer smr = smrs[s];
                    if (!smr) continue;
                    rig.SkinnedMeshes.Add(smr);
                    if (!rig.PrimarySmr || (smr.bones != null && smr.bones.Length > (rig.PrimarySmr.bones != null ? rig.PrimarySmr.bones.Length : 0)))
                        rig.PrimarySmr = smr;
                    Transform[] bones = smr.bones;
                    if (bones == null) continue;
                    for (int i = 0; i < bones.Length; i++)
                    {
                        Transform b = bones[i];
                        if (!b) continue;
                        string bn = b.name.ToLowerInvariant();
                        if (bn.Contains("ik") || bn.Contains("root") || bn.Contains("base") || bn.Contains(" camera") || bn.Contains("aim"))
                            continue;
                        boneSet.Add(b);
                    }
                }

                // Also include any rigid bone parts (for robotic enemies or Surveyor allies/clones)
                if (boneSet.Count == 0)
                {
                    Transform[] all = rig.ModelRoot.GetComponentsInChildren<Transform>(false);
                    for (int i = 0; i < all.Length && boneSet.Count < 36; i++)
                    {
                        Transform t = all[i];
                        if (t && t != rig.ModelRoot) boneSet.Add(t);
                    }
                }

                float bodyScale = Mathf.Clamp(body.radius, 0.45f, 3.5f);
                foreach (Transform b in boneSet)
                {
                    if (!b) continue;
                    if (rig.JointNodes.Count < 28) rig.JointNodes.Add(b);
                    // Walk up up to 2 parents to find the nearest parent bone in the rig
                    Transform p = b.parent;
                    if (p && !boneSet.Contains(p) && p.parent && boneSet.Contains(p.parent))
                        p = p.parent;
                    if (p && p != rig.ModelRoot && boneSet.Contains(p) && rig.Bones.Count < 42)
                    {
                        float len = Vector3.Distance(b.position, p.position);
                        if (len > 0.04f && len < bodyScale * 3.5f)
                        {
                            float limbRad = Mathf.Clamp(len * 0.24f, 0.05f, bodyScale * 0.42f);
                            rig.Bones.Add(new BoneLink { A = p, B = b, Radius = limbRad });
                        }
                    }
                }

                // 2) Hook HurtBox anatomical limb colliders (parented directly to active character/monster/boss bones)
                HurtBoxGroup hbg = rig.ModelRoot.GetComponent<HurtBoxGroup>();
                HurtBox[] hbs = (hbg && hbg.hurtBoxes != null && hbg.hurtBoxes.Length > 0)
                    ? hbg.hurtBoxes
                    : rig.ModelRoot.GetComponentsInChildren<HurtBox>(false);
                if (hbs != null)
                {
                    for (int i = 0; i < hbs.Length && rig.Limbs.Count < 22; i++)
                    {
                        HurtBox hb = hbs[i];
                        if (!hb) continue;
                        Collider col = hb.GetComponent<Collider>();
                        if (!col) continue;
                        if (col is CapsuleCollider cap)
                        {
                            float r = Mathf.Max(0.06f, cap.radius);
                            float hh = Mathf.Max(0.05f, cap.height * 0.5f - r);
                            rig.Limbs.Add(new LimbVolume
                            {
                                Tr = cap.transform,
                                LocalCenter = cap.center,
                                Radius = r,
                                HalfHeight = hh,
                                Height = Mathf.Max(r * 2f, cap.height),
                                Axis = cap.direction
                            });
                        }
                        else if (col is SphereCollider sph)
                        {
                            float r = Mathf.Max(0.08f, sph.radius);
                            rig.Limbs.Add(new LimbVolume
                            {
                                Tr = sph.transform,
                                LocalCenter = sph.center,
                                Radius = r,
                                HalfHeight = 0f,
                                Height = r * 2f,
                                Axis = 1
                            });
                        }
                        else if (col is BoxCollider box)
                        {
                            Vector3 hs = box.size * 0.5f;
                            float r = Mathf.Max(0.08f, Mathf.Min(hs.x, hs.z));
                            rig.Limbs.Add(new LimbVolume
                            {
                                Tr = box.transform,
                                LocalCenter = box.center,
                                Radius = r,
                                HalfHeight = Mathf.Max(0.06f, hs.y * 0.7f),
                                Height = Mathf.Max(r * 2f, box.size.y),
                                Axis = 1
                            });
                        }
                    }
                }

                // 3) Hook rigid weapon / armor MeshFilters attached to the character's animated bones
                MeshFilter[] mfs = rig.ModelRoot.GetComponentsInChildren<MeshFilter>(false);
                for (int i = 0; i < mfs.Length && rig.Attachments.Count < 6; i++)
                {
                    MeshFilter mf = mfs[i];
                    if (!mf || !mf.sharedMesh) continue;
                    string gn = mf.gameObject.name.ToLowerInvariant();
                    if (gn.Contains("area") || gn.Contains("indicator") || gn.Contains("flash") || gn.Contains("glow"))
                        continue;
                    SvrLidar.CachedMeshData cmd = SvrLidar.GetCachedMesh(mf.sharedMesh);
                    if (cmd != null && cmd.EdgeA != null && cmd.EdgeA.Length > 0)
                    {
                        rig.Attachments.Add(new RigidAttachment
                        {
                            Tr = mf.transform,
                            EdgeA = cmd.EdgeA,
                            EdgeB = cmd.EdgeB
                        });
                    }
                }
            }
            catch { }

            return rig;
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

        // Scan modes, cycled sequentially by tapping Q (Topography -> Thermal -> Salvage -> Structural).
        public static readonly string[] OdNames = { "TOPOGRAPHY", "THERMAL", "SALVAGE", "STRUCTURAL" };
        public static readonly string[] OdDescs =
        {
            "WIDE TERRAIN CONTOUR FLOOD + PROXIMITY SKIRT",
            "SEISMIC WALL-SONAR — ENEMIES, BOSSES & DEMON",
            "360 LOOT & HOLOGRAM HUNTER — CHESTS, ITEMS & TECH",
            "TWIN 360 RADAR WALLS + LONG-RANGE SPOTLIGHT"
        };
        private static readonly int[] OdKind = { 1, 3, 1, 0 };    // which overdrive each mode becomes: 0 sweep, 1 burst, 2 focus, 3 echo
        public static int OdMode;
        private float _scanYaw, _sweepRing, _echoPing, _focusLockNext, _peekReady, _modeFlash;
        private bool _loggedPeekFx, _peekNeedBurst;
        private float _repulsorReadyAt;

        // LiDAR-Exclusive Hidden Map Upgrades (Service Panels, Crates, Drop Pods — only visible & interactable via Surveyor LiDAR)
        private struct FieldTechCache
        {
            public GameObject Go;
            public SvrLiDARScanTarget Target;
            public Vector3 Pos;
            public int UpgradeId;
            public int ShapeKind; // 0 = Drop Pod, 1 = Hex Crate, 2 = Service Panel
            public bool Collected;
        }
        private readonly List<FieldTechCache> _techCaches = new List<FieldTechCache>(4);
        private bool _techSpawned;
        private int _nearCacheIdx = -1;
        private string _upgradeToast = "";
        private float _upgradeToastUntil = -10f;

        // Expanding LiDAR Pulse Waves (triggers the Hologram Discovery Loop & Skeletal Rig Sweep as the wave washes over targets)
        private struct LidarWave
        {
            public Vector3 Origin;
            public float Radius;
            public float MaxRadius;
            public float Speed;
            public Color Col;
        }
        private readonly List<LidarWave> _waves = new List<LidarWave>(8);
        private float _nextAutoWave;

        // Real-time Animated Skeletal & Skinned-Mesh Rigs for characters, players, monsters, and bosses
        private readonly Dictionary<int, SvrSkeletalRig> _skeletalRigs = new Dictionary<int, SvrSkeletalRig>(64);
        private Mesh _sharedBakeMesh;

        // Tracked stage gameplay objects (chests, shrines, shops, barrels, dropped items, teleporter)
        public class CachedMeshData
        {
            public Vector3[] Verts;
            public Vector3[] EdgeA;
            public Vector3[] EdgeB;
        }
        private class MeshPart
        {
            public Transform Tr;
            public CachedMeshData Data;
        }
        private class PoiOutline
        {
            public float NextCheck;
            public bool Complete;
            public readonly List<MeshPart> Parts = new List<MeshPart>(4);
        }
        private struct Poi
        {
            public Transform Tr;
            public Vector3 Pos;
            public Vector3 Extents;
            public Color Col;
            public string Tag;
            public int Kind; // 0 = chest/shop, 1 = shrine/drone, 2 = barrel, 3 = item/pickup, 4 = teleporter
            public int SubShape;
            public PoiOutline Outline;
            public SvrLiDARScanTarget Target;
        }
        private readonly List<Poi> _pois = new List<Poi>(64);
        private static readonly Dictionary<int, CachedMeshData> _meshCache = new Dictionary<int, CachedMeshData>(128);
        private readonly Dictionary<int, PoiOutline> _poiOutlines = new Dictionary<int, PoiOutline>(128);
        private readonly List<Vector2> _hullPts = new List<Vector2>(48);
        private readonly List<Vector2> _hullOut = new List<Vector2>(24);
        private float _nextPoiRefresh, _nextPoiPaint;
        private static FieldInfo _fiPurchaseAvail, _fiChestOpened, _fiBarrelOpened, _fiShopPurchased, _fiGpcPickup;
        private static bool _fiResolved;

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
        private float _origFarClip;
        private bool _origFog;
        private FogMode _origFogMode;
        private float _origFogDensity, _origFogStart, _origFogEnd;
        private Color _origFogColor, _origAmbientLight;
        private float _origAmbientIntensity;
        private Material _origSkyboxMat;
        private Behaviour _dimPpLayer;
        private bool _dimPpOrigEnabled;
        private Camera _dimSkyboxCam;
        private bool _dimSkyboxOrigEnabled;
        private readonly List<Behaviour> _suppressedStageVolumes = new List<Behaviour>(16);
        private float _nextFogSweepTime;
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
            _sharedBakeMesh = new Mesh { name = "SvrLidarBakeMesh" };
        }

        private void OnEnable()
        {
            Camera.onPreCull += PreCull;
            Camera.onPostRender += PostRender;
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= PreCull;
            Camera.onPostRender -= PostRender;
            RestoreCamera();
            DropPeekFx();
            if (_buf) Destroy(_buf);
            if (_sharedBakeMesh) Destroy(_sharedBakeMesh);
            for (int i = 0; i < _techCaches.Count; i++)
                if (_techCaches[i].Go) Destroy(_techCaches[i].Go);
        }
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

        // High-contrast tactical LiDAR palette: warm orange/amber at close range & sharp vertical walls -> bright electric cyan at mid range -> crisp deep azure at long range (zero muddy green/yellow haze).
        private static Color Depth(float dist, float range, Vector3 normal)
        {
            float t = Mathf.Clamp01(dist / Mathf.Max(10f, range * 0.85f));
            Color nearOrange = new Color(1f, 0.52f, 0.14f);
            Color midCyan = new Color(0.18f, 0.94f, 1f);
            Color farAzure = new Color(0.14f, 0.56f, 0.96f);
            Color c = t < 0.36f
                ? Color.Lerp(nearOrange, midCyan, t / 0.36f)
                : Color.Lerp(midCyan, farAzure, (t - 0.36f) / 0.64f);
            float wallFactor = 1f - Mathf.Abs(normal.y);
            if (wallFactor > 0.55f && t < 0.55f)
                c = Color.Lerp(c, new Color(1f, 0.68f, 0.22f), (wallFactor - 0.55f) * 0.65f);
            c.a = 0.62f + 0.38f * wallFactor;   // walls and edges read brighter than flat floor
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

        private static void EnsureFields()
        {
            if (_fiResolved) return;
            _fiResolved = true;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            try { _fiPurchaseAvail = typeof(PurchaseInteraction).GetField("available", flags); } catch { }
            try { _fiChestOpened = typeof(ChestBehavior).GetField("isChestOpened", flags); } catch { }
            try { _fiBarrelOpened = typeof(BarrelInteraction).GetField("opened", flags); } catch { }
            try { _fiShopPurchased = typeof(ShopTerminalBehavior).GetField("hasBeenPurchased", flags); } catch { }
            try { _fiGpcPickup = typeof(GenericPickupController).GetField("pickupIndex", flags); } catch { }
        }

        private static void ResolvePickupDisplay(GameObject go, ref string tag, ref Color col)
        {
            if (!go) return;
            try
            {
                PickupIndex idx = PickupIndex.none;
                PickupDisplay pd = go.GetComponentInChildren<PickupDisplay>();
                if (pd) idx = pd.GetPickupIndex();
                if (idx == PickupIndex.none)
                {
                    EnsureFields();
                    GenericPickupController gpc = go.GetComponent<GenericPickupController>();
                    if (gpc && _fiGpcPickup != null)
                    {
                        object v = _fiGpcPickup.GetValue(gpc);
                        if (v is PickupIndex pi) idx = pi;
                    }
                }
                if (idx != PickupIndex.none)
                {
                    PickupDef def = PickupCatalog.GetPickupDef(idx);
                    if (def != null)
                    {
                        if (!string.IsNullOrEmpty(def.nameToken))
                        {
                            string loc = Language.GetString(def.nameToken);
                            if (!string.IsNullOrEmpty(loc) && loc != def.nameToken)
                                tag = loc.ToUpperInvariant();
                        }
                        Color bc = def.baseColor;
                        float maxC = Mathf.Max(bc.r, Mathf.Max(bc.g, bc.b));
                        if (maxC > 0.05f)
                        {
                            // Boost brightness/saturation slightly so white/green/red/yellow/lunar/void tiers pop on black LiDAR
                            col = Color.Lerp(bc * (1f / Mathf.Max(0.45f, maxC)), Color.white, 0.18f);
                            col.a = 1f;
                        }
                    }
                }
            }
            catch { }
        }

        public static CachedMeshData GetCachedMesh(Mesh m)
        {
            if (!m) return null;
            int id = m.GetInstanceID();
            CachedMeshData data;
            if (_meshCache.TryGetValue(id, out data)) return data;

            data = new CachedMeshData();
            List<Vector3> vList = new List<Vector3>(180);
            List<Vector3> eaList = new List<Vector3>(120);
            List<Vector3> ebList = new List<Vector3>(120);

            try
            {
                if (m.isReadable)
                {
                    Vector3[] verts = m.vertices;
                    if (verts != null && verts.Length > 0)
                    {
                        int vStep = Mathf.Max(1, verts.Length / 150);
                        for (int i = 0; i < verts.Length && vList.Count < 160; i += vStep)
                            vList.Add(verts[i]);

                        int[] tris = m.triangles;
                        if (tris != null && tris.Length >= 3)
                        {
                            int triCount = tris.Length / 3;
                            int tStep = Mathf.Max(1, triCount / 90);
                            for (int t = 0; t < triCount && eaList.Count < 110; t += tStep)
                            {
                                int b = t * 3;
                                Vector3 v0 = verts[tris[b]];
                                Vector3 v1 = verts[tris[b + 1]];
                                Vector3 v2 = verts[tris[b + 2]];
                                // Pick the longest edge of this triangle to emphasize structural silhouette lines
                                float d01 = (v0 - v1).sqrMagnitude;
                                float d12 = (v1 - v2).sqrMagnitude;
                                float d20 = (v2 - v0).sqrMagnitude;
                                if (d01 >= d12 && d01 >= d20) { eaList.Add(v0); ebList.Add(v1); }
                                else if (d12 >= d01 && d12 >= d20) { eaList.Add(v1); ebList.Add(v2); }
                                else { eaList.Add(v2); ebList.Add(v0); }
                            }
                        }
                    }
                }
            }
            catch { }

            // If mesh.isReadable is false on a cooked asset-bundle mesh, raycast against an isolated MeshCollider
            // from a 160-direction Fibonacci sphere to capture the exact 3D triangle surface contour of the item/chest!
            if (vList.Count < 12)
            {
                GameObject probe = null;
                try
                {
                    Bounds b = m.bounds;
                    float rad = Mathf.Max(0.15f, b.extents.magnitude * 1.45f);
                    probe = new GameObject("SvrMeshProbe");
                    probe.transform.position = new Vector3(0f, -9500f, 0f);
                    MeshCollider mc = probe.AddComponent<MeshCollider>();
                    mc.sharedMesh = m;

                    Vector3 worldCenter = probe.transform.position + b.center;
                    Vector3 prevHit = Vector3.zero;
                    bool hasPrev = false;
                    const int rays = 160;
                    for (int i = 0; i < rays; i++)
                    {
                        float y = 1f - 2f * (i + 0.5f) / rays;
                        float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                        float phi = i * 2.3999632f;
                        Vector3 dir = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
                        Ray ray = new Ray(worldCenter + dir * rad, -dir);
                        RaycastHit hit;
                        if (mc.Raycast(ray, out hit, rad * 2.1f))
                        {
                            Vector3 localPt = hit.point - probe.transform.position;
                            vList.Add(localPt);
                            if (hasPrev && (localPt - prevHit).sqrMagnitude < (b.extents.sqrMagnitude * 0.45f) && eaList.Count < 110)
                            {
                                eaList.Add(prevHit);
                                ebList.Add(localPt);
                            }
                            prevHit = localPt;
                            hasPrev = true;
                        }
                        else
                        {
                            hasPrev = false;
                        }
                    }
                }
                catch { }
                finally
                {
                    if (probe) DestroyImmediate(probe);
                }
            }

            // Final fallback: trace the mesh's local sub-bounds silhouette (octagonal prism + contour ribs) so it is never a plain cube
            if (vList.Count < 8)
            {
                Bounds b = m.bounds;
                Vector3 c = b.center;
                Vector3 e = Vector3.Max(b.extents, new Vector3(0.08f, 0.08f, 0.08f));
                for (int ring = 0; ring < 5; ring++)
                {
                    float yf = Mathf.Lerp(-0.92f, 0.92f, ring / 4f);
                    float pinch = 1f - 0.22f * Mathf.Abs(yf);
                    Vector3 prev = Vector3.zero;
                    for (int k = 0; k <= 12; k++)
                    {
                        float ang = (k % 12) * (6.2831853f / 12f);
                        Vector3 pt = c + new Vector3(Mathf.Cos(ang) * e.x * pinch, yf * e.y, Mathf.Sin(ang) * e.z * pinch);
                        if (k < 12) vList.Add(pt);
                        if (k > 0) { eaList.Add(prev); ebList.Add(pt); }
                        prev = pt;
                    }
                }
            }

            data.Verts = vList.ToArray();
            data.EdgeA = eaList.ToArray();
            data.EdgeB = ebList.ToArray();
            _meshCache[id] = data;
            return data;
        }

        private PoiOutline GetOrBuildOutline(Transform root, int kind)
        {
            if (!root) return null;
            int id = root.GetInstanceID();
            PoiOutline outline;
            if (_poiOutlines.TryGetValue(id, out outline))
            {
                if (outline.Complete || Time.time < outline.NextCheck) return outline;
            }
            else
            {
                outline = new PoiOutline();
                _poiOutlines[id] = outline;
            }

            outline.Parts.Clear();
            outline.NextCheck = Time.time + 0.35f;

            try
            {
                // For dropped items, prefer the PickupDisplay's actual 3D item model hierarchy (so we don't sample outer trigger spheres)
                Transform searchRoot = root;
                if (kind == 3)
                {
                    PickupDisplay pd = root.GetComponentInChildren<PickupDisplay>();
                    if (pd)
                    {
                        List<Renderer> mrs = pd.modelRenderers;
                        if (mrs != null && mrs.Count > 0)
                        {
                            for (int i = 0; i < mrs.Count; i++)
                            {
                                Renderer r = mrs[i];
                                if (!r) continue;
                                MeshFilter mf = r.GetComponent<MeshFilter>();
                                Mesh mesh = mf ? mf.sharedMesh : null;
                                if (!mesh && r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                                if (mesh)
                                {
                                    CachedMeshData cmd = GetCachedMesh(mesh);
                                    if (cmd != null && cmd.Verts.Length > 0)
                                        outline.Parts.Add(new MeshPart { Tr = r.transform, Data = cmd });
                                }
                            }
                        }
                        searchRoot = pd.transform;
                    }
                }

                if (outline.Parts.Count == 0)
                {
                    MeshFilter[] mfs = searchRoot.GetComponentsInChildren<MeshFilter>(false);
                    for (int i = 0; i < mfs.Length && outline.Parts.Count < 8; i++)
                    {
                        MeshFilter mf = mfs[i];
                        if (!mf || !mf.sharedMesh) continue;
                        string gn = mf.gameObject.name.ToLowerInvariant();
                        // Skip particle quad / area indicator / highlight proxy meshes
                        if (gn.Contains("area") || gn.Contains("indicator") || gn.Contains("wave") || gn.Contains("beam") || gn.Contains("glow") || gn.Contains("flash"))
                            continue;
                        Renderer mr = mf.GetComponent<Renderer>();
                        if (mr && !mr.enabled && kind != 3) continue;
                        CachedMeshData cmd = GetCachedMesh(mf.sharedMesh);
                        if (cmd != null && cmd.Verts.Length > 0)
                            outline.Parts.Add(new MeshPart { Tr = mf.transform, Data = cmd });
                    }

                    SkinnedMeshRenderer[] smrs = searchRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                    for (int i = 0; i < smrs.Length && outline.Parts.Count < 8; i++)
                    {
                        SkinnedMeshRenderer smr = smrs[i];
                        if (!smr || !smr.sharedMesh) continue;
                        CachedMeshData cmd = GetCachedMesh(smr.sharedMesh);
                        if (cmd != null && cmd.Verts.Length > 0)
                            outline.Parts.Add(new MeshPart { Tr = smr.transform, Data = cmd });
                    }
                }

                // If the object uses non-mesh colliders (e.g. compound colliders on a shrine/interactable), sample its collider surface directly
                if (outline.Parts.Count == 0 && kind != 3)
                {
                    Collider[] cols = root.GetComponentsInChildren<Collider>(false);
                    if (cols != null && cols.Length > 0)
                    {
                        List<Vector3> vList = new List<Vector3>(96);
                        List<Vector3> eaList = new List<Vector3>(64);
                        List<Vector3> ebList = new List<Vector3>(64);
                        for (int cIdx = 0; cIdx < cols.Length && cIdx < 4; cIdx++)
                        {
                            Collider col = cols[cIdx];
                            if (!col || col.isTrigger) continue;
                            Bounds b = col.bounds;
                            float rad = Mathf.Max(0.2f, b.extents.magnitude * 1.4f);
                            Vector3 prev = Vector3.zero;
                            bool hasPrev = false;
                            for (int i = 0; i < 64; i++)
                            {
                                float y = 1f - 2f * (i + 0.5f) / 64f;
                                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                                float phi = i * 2.3999632f;
                                Vector3 dir = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r);
                                RaycastHit hit;
                                if (col.Raycast(new Ray(b.center + dir * rad, -dir), out hit, rad * 2f))
                                {
                                    Vector3 lp = root.InverseTransformPoint(hit.point);
                                    vList.Add(lp);
                                    if (hasPrev) { eaList.Add(prev); ebList.Add(lp); }
                                    prev = lp;
                                    hasPrev = true;
                                }
                                else hasPrev = false;
                            }
                        }
                        if (vList.Count > 0)
                        {
                            outline.Parts.Add(new MeshPart
                            {
                                Tr = root,
                                Data = new CachedMeshData { Verts = vList.ToArray(), EdgeA = eaList.ToArray(), EdgeB = ebList.ToArray() }
                            });
                        }
                    }
                }

                if (outline.Parts.Count > 0) outline.Complete = true;
            }
            catch { }

            return outline;
        }

        private static bool IsPurchaseOpen(PurchaseInteraction pi)
        {
            if (!pi || !pi.gameObject.activeInHierarchy) return false;
            EnsureFields();
            try
            {
                if (_fiPurchaseAvail != null && !(bool)_fiPurchaseAvail.GetValue(pi)) return false;
                ChestBehavior cb = pi.GetComponent<ChestBehavior>();
                if (cb && _fiChestOpened != null && (bool)_fiChestOpened.GetValue(cb)) return false;
                ShopTerminalBehavior stb = pi.GetComponent<ShopTerminalBehavior>();
                if (stb && _fiShopPurchased != null && (bool)_fiShopPurchased.GetValue(stb)) return false;
            }
            catch { }
            return true;
        }

        private static bool IsBarrelOpen(BarrelInteraction bi)
        {
            if (!bi || !bi.gameObject.activeInHierarchy) return false;
            EnsureFields();
            try
            {
                if (_fiBarrelOpened != null && (bool)_fiBarrelOpened.GetValue(bi)) return false;
            }
            catch { }
            return true;
        }

        private static void ClassifyInteractable(GameObject go, out string tag, out Color col, out int kind, out int subShape)
        {
            string n = (go ? go.name : "").ToLowerInvariant();
            if (n.Contains("teleporter") || n.Contains(" primordial"))
            {
                tag = "TELEPORTER";
                col = new Color(1f, 0.3f, 0.95f, 1f);
                kind = 4;
                subShape = 9;
                return;
            }
            if (n.Contains("shrine") || n.Contains("altar") || n.Contains("halcyon"))
            {
                if (n.Contains("blood"))
                {
                    tag = "BLOOD SHRINE";
                    col = new Color(1f, 0.25f, 0.32f, 1f);
                    kind = 1;
                    subShape = 2;
                }
                else if (n.Contains("boss") || n.Contains("mountain"))
                {
                    tag = "MOUNTAIN SHRINE";
                    col = new Color(0.28f, 0.96f, 1f, 1f);
                    kind = 1;
                    subShape = 4;
                }
                else
                {
                    tag = n.Contains("newt") ? "NEWT ALTAR" : (n.Contains("chance") ? "CHANCE SHRINE" : (n.Contains("combat") ? "COMBAT SHRINE" : "SHRINE"));
                    col = new Color(0.25f, 0.92f, 1f, 1f);
                    kind = 1;
                    subShape = 3;
                }
                return;
            }
            if (n.Contains("drone") || n.Contains("turret") || n.Contains("emergency"))
            {
                tag = n.Contains("turret") ? "TURRET" : "DRONE";
                col = new Color(0.35f, 1f, 0.72f, 1f);
                kind = 1;
                subShape = 13;
                return;
            }
            if (n.Contains("duplicator") || n.Contains("printer"))
            {
                tag = "PRINTER";
                col = new Color(1f, 0.86f, 0.15f, 1f);
                kind = 0;
                subShape = 5;
                return;
            }
            if (n.Contains("tripleshop") || n.Contains("multishop") || n.Contains("terminal") || n.Contains("bazaar") || n.Contains("cauldron"))
            {
                tag = "SHOP";
                col = new Color(1f, 0.88f, 0.22f, 1f);
                kind = 0;
                subShape = 6;
                return;
            }
            if (n.Contains("scrapper"))
            {
                tag = "SCRAPPER";
                col = new Color(0.9f, 0.82f, 0.35f, 1f);
                kind = 0;
                subShape = 5;
                return;
            }
            if (n.Contains("lunar"))
            {
                tag = "LUNAR POD";
                col = new Color(0.45f, 0.78f, 1f, 1f);
                kind = 0;
                subShape = 3;
                return;
            }
            if (n.Contains("void"))
            {
                tag = "VOID CRADLE";
                col = new Color(0.88f, 0.38f, 1f, 1f);
                kind = 0;
                subShape = 3;
                return;
            }
            if (n.Contains("goldchest") || n.Contains("legendary"))
            {
                tag = "LEGENDARY CHEST";
                col = new Color(1f, 0.92f, 0.25f, 1f);
                kind = 0;
                subShape = 1;
                return;
            }
            if (n.Contains("chest2") || n.Contains("large"))
            {
                tag = "LARGE CHEST";
                col = new Color(1f, 0.88f, 0.18f, 1f);
                kind = 0;
                subShape = 1;
                return;
            }
            if (n.Contains("equipment"))
            {
                tag = "EQUIP BARREL";
                col = new Color(1f, 0.62f, 0.18f, 1f);
                kind = 0;
                subShape = 7;
                return;
            }
            if (n.Contains("lockbox"))
            {
                tag = "LOCKBOX";
                col = new Color(1f, 0.82f, 0.28f, 1f);
                kind = 0;
                subShape = 0;
                return;
            }
            tag = "CHEST";
            col = new Color(1f, 0.86f, 0.15f, 1f);
            kind = 0;
            subShape = 0;
        }

        private static SvrLiDARScanTarget EnsureScanTarget(GameObject go, int kind, int subShape, string label, Color col, Vector3 localCenter, Vector3 extents, int upgradeId = -1)
        {
            if (!go) return null;
            SvrLiDARScanTarget t = go.GetComponent<SvrLiDARScanTarget>();
            if (!t) t = go.AddComponent<SvrLiDARScanTarget>();
            t.Kind = kind;
            t.SubShape = subShape;
            t.Label = label;
            t.HoloColor = col;
            t.LocalCenter = localCenter;
            t.Extents = extents;
            t.UpgradeId = upgradeId;
            if (!t.Built && Time.time >= t.NextBuildAttempt) t.BuildWireframe();
            return t;
        }

        private void SpawnPulseWave(Vector3 origin, float maxRadius, float speed, Color col)
        {
            if (_waves.Count >= 6) _waves.RemoveAt(0);
            _waves.Add(new LidarWave
            {
                Origin = origin,
                Radius = 1.2f,
                MaxRadius = maxRadius,
                Speed = speed,
                Col = col
            });
        }

        private SvrSkeletalRig GetOrBuildSkeletalRig(CharacterBody b)
        {
            if (!b) return null;
            int id = b.GetInstanceID();
            SvrSkeletalRig rig;
            if (!_skeletalRigs.TryGetValue(id, out rig) || rig == null || rig.Body != b)
            {
                rig = SvrSkeletalRig.Build(b);
                if (rig != null) _skeletalRigs[id] = rig;
            }
            return rig;
        }

        // Maps active character/monster/boss skeletal animations & skinned meshes onto the real-time point-cloud + wireframe rig
        private void SweepBody(CharacterBody b, float revealSeconds, int pointBudget = 16)
        {
            if (!b || !b.healthComponent || !b.healthComponent.alive) return;
            SvrSkeletalRig rig = GetOrBuildSkeletalRig(b);
            if (rig == null) return;
            float now = Time.time;
            rig.ScannedUntil = Mathf.Max(rig.ScannedUntil, now + revealSeconds);
            rig.LastScanHit = now;

            bool isFriendly = b.teamComponent && _body && _body.teamComponent && b.teamComponent.teamIndex == _body.teamComponent.teamIndex;
            bool isBoss = b.isBoss || b.isChampion;
            Color boneCol = isFriendly
                ? new Color(0.28f, 1f, 0.88f, 1f)
                : (isBoss ? new Color(1f, 0.42f, 0.1f, 1f) : new Color(1f, 0.18f, 0.14f, 1f));
            Color edgeCol = Color.Lerp(boneCol, Color.white, 0.3f);
            float life = 0.24f;

            // 1) Emit real-time animated points along active Transform bone links
            if (rig.Bones.Count > 0)
            {
                int bCnt = Mathf.Min(pointBudget, rig.Bones.Count * 2);
                for (int i = 0; i < bCnt; i++)
                {
                    SvrSkeletalRig.BoneLink bl = rig.Bones[(i + _spin) % rig.Bones.Count];
                    if (!bl.A || !bl.B) continue;
                    Vector3 pA = bl.A.position;
                    Vector3 pB = bl.B.position;
                    Vector3 seg = pB - pA;
                    float u = Random.value;
                    Vector3 basePt = pA + seg * u;
                    // Radial surface point around the animated bone cylinder
                    Vector3 upRef = Mathf.Abs(seg.normalized.y) > 0.9f ? Vector3.right : Vector3.up;
                    Vector3 perp1 = Vector3.Cross(seg.normalized, upRef).normalized;
                    Vector3 perp2 = Vector3.Cross(seg.normalized, perp1).normalized;
                    float ang = Random.value * 6.2831853f;
                    Vector3 surfPt = basePt + (perp1 * Mathf.Cos(ang) + perp2 * Mathf.Sin(ang)) * bl.Radius;
                    AddDot(surfPt, (i & 1) == 0 ? edgeCol : boneCol, life, true);
                }
            }
            // 2) Emit real-time animated points on HurtBox limb capsules/spheres/boxes parented to moving bones
            else if (rig.Limbs.Count > 0)
            {
                int lCnt = Mathf.Min(pointBudget, rig.Limbs.Count * 2);
                for (int i = 0; i < lCnt; i++)
                {
                    SvrSkeletalRig.LimbVolume lv = rig.Limbs[(i + _spin) % rig.Limbs.Count];
                    if (!lv.Tr) continue;
                    Vector3 ls = lv.Tr.lossyScale;
                    float maxS = Mathf.Max(0.1f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
                    Vector3 center = lv.Tr.TransformPoint(lv.LocalCenter);
                    float r = lv.Radius * maxS;
                    float h = lv.Height * maxS * 0.5f;
                    Vector3 axis = lv.Axis == 0 ? lv.Tr.right : (lv.Axis == 2 ? lv.Tr.forward : lv.Tr.up);
                    float ang = Random.value * 6.2831853f;
                    Vector3 p1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                    Vector3 p2 = Vector3.Cross(axis, p1).normalized;
                    Vector3 surfPt = center + axis * Random.Range(-h, h) + (p1 * Mathf.Cos(ang) + p2 * Mathf.Sin(ang)) * r;
                    AddDot(surfPt, (i & 1) == 0 ? edgeCol : boneCol, life, true);
                }
            }

            // 3) If the character/enemy has a readable SkinnedMeshRenderer, bake its live skinning matrix pose right now
            if (_sharedBakeMesh && rig.SkinnedMeshes.Count > 0 && now >= rig.NextBakeTime)
            {
                rig.NextBakeTime = now + 0.045f; // ~22 Hz live skinning bake
                try
                {
                    SkinnedMeshRenderer smr = rig.SkinnedMeshes[0];
                    if (smr && smr.sharedMesh && smr.sharedMesh.isReadable)
                    {
                        _sharedBakeMesh.Clear();
                        smr.BakeMesh(_sharedBakeMesh);
                        Vector3[] bVerts = _sharedBakeMesh.vertices;
                        if (bVerts != null && bVerts.Length > 0)
                        {
                            rig.BakedWorldVerts.Clear();
                            Transform smrTr = smr.transform;
                            int step = Mathf.Max(1, bVerts.Length / 96);
                            for (int v = 0; v < bVerts.Length && rig.BakedWorldVerts.Count < 96; v += step)
                            {
                                Vector3 wp = smrTr.TransformPoint(bVerts[v]);
                                rig.BakedWorldVerts.Add(wp);
                                if ((v / step) % 4 == 0) AddDot(wp, edgeCol, life, true);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void Cast(Vector3 origin, Vector3 dir, float range, float life, bool big)
        {
            RaycastHit hit;
            int mask = LayerIndex.world.mask | LayerIndex.entityPrecise.mask | LayerIndex.defaultLayer.mask;
            if (!Physics.Raycast(origin, dir, out hit, range, mask, QueryTriggerInteraction.Ignore)) return;
            bool enemy = false;
            bool special = false;
            Color c = default;

            Collider col = hit.collider;
            HurtBox hb = col ? col.GetComponent<HurtBox>() : null;
            if (hb)
            {
                if (hb.healthComponent && hb.healthComponent.body == _body) return;
                CharacterBody other = hb.healthComponent ? hb.healthComponent.body : null;
                if (other)
                {
                    enemy = other.teamComponent && _body.teamComponent && other.teamComponent.teamIndex != _body.teamComponent.teamIndex;
                    // Real-time Skeletal & Skinned-Mesh Sweep when a LiDAR ray hits any player, ally, monster, or boss!
                    SweepBody(other, 1.8f, 6);
                }
            }
            if (enemy)
            {
                c = new Color(1f, 0.2f, 0.12f, 1f);
            }
            else if (col)
            {
                // Skip invisible interaction sphere/box colliders around dropped pickups so only their true 3D wireframe hologram is drawn
                if (col.GetComponentInParent<GenericPickupController>() || col.GetComponentInParent<PickupPickerController>())
                    return;

                SvrLiDARScanTarget scanTarget = col.GetComponentInParent<SvrLiDARScanTarget>();
                if (scanTarget)
                {
                    scanTarget.OnWaveWash(3.8f, 1f);
                    c = scanTarget.HoloColor;
                    special = true;
                }
                else
                {
                    TeleporterInteraction tp = col.GetComponentInParent<TeleporterInteraction>();
                    if (tp)
                    {
                        c = Random.value < 0.5f ? new Color(1f, 0.28f, 0.95f, 1f) : new Color(0.2f, 0.95f, 1f, 1f);
                        special = true;
                    }
                    else
                    {
                        PurchaseInteraction pi = col.GetComponentInParent<PurchaseInteraction>();
                        if (pi && IsPurchaseOpen(pi))
                        {
                            string tag; int kind, subShape;
                            ClassifyInteractable(pi.gameObject, out tag, out c, out kind, out subShape);
                            SvrLiDARScanTarget st = EnsureScanTarget(pi.gameObject, kind, subShape, tag, c, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
                            if (st) st.OnWaveWash(3.8f, 1f);
                            special = true;
                        }
                        else
                        {
                            BarrelInteraction bi = col.GetComponentInParent<BarrelInteraction>();
                            if (bi && IsBarrelOpen(bi))
                            {
                                c = new Color(1f, 0.72f, 0.2f, 1f);
                                SvrLiDARScanTarget st = EnsureScanTarget(bi.gameObject, 2, 7, "BARREL", c, new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 0.7f, 0.55f));
                                if (st) st.OnWaveWash(3.5f, 0.95f);
                                special = true;
                            }
                        }
                    }
                }
                if (!special) c = Depth(hit.distance, range, hit.normal);
            }
            else
            {
                c = Depth(hit.distance, range, hit.normal);
            }
            AddDot(hit.point, c, special ? life * 1.35f : life, big || enemy || special);
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
            SuppressStageFogAndColorGrading(true);
            SpawnPulseWave(at, radius * 1.35f, Mathf.Max(36f, radius * 1.6f), new Color(0.22f, 0.96f, 1f, 1f));
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
                        if (hb.healthComponent.body) SweepBody(hb.healthComponent.body, 5f, 22);
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
            SuppressStageFogAndColorGrading(true);
            SpawnPulseWave(at, 95f * SvrSenses.SightMult, 65f, mode == 1 ? new Color(1f, 0.45f, 0.16f, 1f) : new Color(0.22f, 0.95f, 1f, 1f));
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
            _peekNeedBurst = true;
            // Do not jump _peekK: let LateUpdate smoothly ramp _peekK from 0 -> 1 so the dots fade out smoothly.
        }

        // ---------------------------------------------------------------- per frame

        private static Vector3 Cone(Vector3 aim, float degrees, float theta)
        {
            Quaternion rot = Quaternion.LookRotation(aim);
            Vector3 d = Quaternion.Euler(-degrees * Mathf.Sin(theta), degrees * Mathf.Cos(theta), 0f) * Vector3.forward;
            return rot * d;
        }

        private void TriggerModeBurst()
        {
            _modeFlash = Time.time;
            Util.PlaySound("Play_UI_menuClick", gameObject);
            Util.PlaySound("Play_UI_obj_casinoChest_open", gameObject);
            if (OdMode == 1) _revealUntil = Mathf.Max(_revealUntil, Time.time + 2.0f);
            SuppressStageFogAndColorGrading(true);
            _nextPoiRefresh = 0f;
            _nextPoiPaint = 0f;
            Scan(0.32f);
            ScanInteractables(true);
            if (_body)
            {
                _pulseAt = _body.corePosition;
                _pulseRange = SvrPlugin.ScanRange.Value * SvrSenses.SightMult * (OdMode == 2 ? 1.45f : (OdMode == 3 ? 1.8f : 1.2f));
                _pulseLeft = Mathf.Max(_pulseLeft, Mathf.RoundToInt(950f * SvrSenses.SightMult));
                Color waveCol = OdMode == 1 ? new Color(1f, 0.45f, 0.16f, 1f) : new Color(0.22f, 0.96f, 1f, 1f);
                SpawnPulseWave(_body.corePosition, _pulseRange * 1.15f, 52f, waveCol);
            }
        }

        private void SpawnFieldTechCaches()
        {
            if (_techSpawned || !_body) return;
            _techSpawned = true;
            for (int i = 0; i < _techCaches.Count; i++)
                if (_techCaches[i].Go) Destroy(_techCaches[i].Go);
            _techCaches.Clear();

            List<int> pool = new List<int>(SvrUpgrades.COUNT);
            for (int i = 0; i < SvrUpgrades.COUNT; i++)
                if (!SvrUpgrades.Has(i)) pool.Add(i);
            if (pool.Count == 0)
            {
                for (int i = 0; i < SvrUpgrades.COUNT; i++) pool.Add(i);
            }

            Vector3 origin = _body.footPosition;
            TeleporterInteraction tp = FindObjectOfType<TeleporterInteraction>();
            Vector3 tpPos = tp ? tp.transform.position : origin + Vector3.forward * 55f;

            int count = Mathf.Min(3, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pickIdx = Random.Range(0, pool.Count);
                int upgId = pool[pickIdx];
                pool.RemoveAt(pickIdx);

                float ang = (i / (float)count) * 6.28318f + Random.Range(-0.45f, 0.45f);
                float dist = Random.Range(24f, 62f);
                Vector3 anchor = (i == 0) ? Vector3.Lerp(origin, tpPos, 0.45f) : origin;
                Vector3 candidate = anchor + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
                RaycastHit hit;
                if (Physics.Raycast(candidate + Vector3.up * 45f, Vector3.down, out hit, 140f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    candidate = hit.point;
                else
                    candidate.y = origin.y;

                int shapeKind = i % 3; // 0 = Drop Pod, 1 = Hex Crate, 2 = Service Panel
                GameObject cacheGo = new GameObject("SvrHiddenTechCache_" + i);
                cacheGo.transform.position = candidate;
                SphereCollider sc = cacheGo.AddComponent<SphereCollider>();
                sc.radius = 1.15f;
                sc.isTrigger = true;

                Vector3 ext = shapeKind == 0 ? new Vector3(0.95f, 1.35f, 0.95f) : (shapeKind == 1 ? new Vector3(0.95f, 0.85f, 0.95f) : new Vector3(0.85f, 1.25f, 0.55f));
                Color hCol = upgId < 4 ? new Color(1f, 0.86f, 0.22f, 1f) : new Color(0.28f, 1f, 0.92f, 1f);
                SvrLiDARScanTarget target = EnsureScanTarget(
                    cacheGo,
                    5,
                    10 + shapeKind,
                    SvrUpgrades.ShortNames[upgId],
                    hCol,
                    new Vector3(0f, ext.y, 0f),
                    ext,
                    upgId);

                _techCaches.Add(new FieldTechCache
                {
                    Go = cacheGo,
                    Target = target,
                    Pos = candidate,
                    UpgradeId = upgId,
                    ShapeKind = shapeKind,
                    Collected = false
                });
            }
        }

        private void FireRepulsorBlow()
        {
            _repulsorReadyAt = Time.time + (SvrUpgrades.Has(SvrUpgrades.REPULSOR_BLOW) ? 4.5f : 7.5f);
            Util.PlaySound("Play_commando_R", gameObject);
            Vector3 o = _input ? _input.aimOrigin : _body.corePosition;
            Vector3 aim = _input ? _input.aimDirection : transform.forward;

            // Paint a high-pressure cyan/white repulsor vortex cone in the LiDAR point cloud
            for (int i = 0; i < 180; i++)
            {
                float d = Random.Range(2f, 26f);
                Vector3 dir = Cone(aim, 38f, i * 2.39996f);
                AddDot(o + dir * d, (i & 1) == 0 ? new Color(0.35f, 0.96f, 1f, 1f) : new Color(0.9f, 1f, 1f, 1f), 1.6f, true);
            }

            // Knock back swarms & reset bosses in the forward cone
            try
            {
                BullseyeSearch bs = new BullseyeSearch
                {
                    searchOrigin = o,
                    searchDirection = aim,
                    maxDistanceFilter = 28f,
                    maxAngleFilter = 44f,
                    teamMaskFilter = TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex),
                    filterByLoS = false
                };
                bs.RefreshCandidates();
                foreach (HurtBox hb in bs.GetResults())
                {
                    HealthComponent hc = hb ? hb.healthComponent : null;
                    CharacterBody eb = hc ? hc.body : null;
                    if (!eb || eb == _body) continue;
                    Vector3 push = (eb.corePosition - o).normalized + Vector3.up * 0.25f;
                    float f = SvrUpgrades.Has(SvrUpgrades.REPULSOR_BLOW) ? 3800f : 2400f;
                    if (eb.characterMotor) eb.characterMotor.ApplyForce(push * f, true, false);
                    else if (eb.rigidbody) eb.rigidbody.AddForce(push * f, ForceMode.Impulse);
                    SweepBody(eb, 3.2f, 18);
                }
            }
            catch { }

            // Deflect / vaporize enemy projectiles in the forward repulsor blast
            try
            {
                RoR2.Projectile.ProjectileController[] projs = FindObjectsOfType<RoR2.Projectile.ProjectileController>();
                for (int i = 0; i < projs.Length; i++)
                {
                    RoR2.Projectile.ProjectileController pc = projs[i];
                    if (!pc || !pc.gameObject.activeInHierarchy) continue;
                    if (pc.teamFilter && pc.teamFilter.teamIndex == _body.teamComponent.teamIndex) continue;
                    Vector3 toP = pc.transform.position - o;
                    if (toP.magnitude < 24f && Vector3.Angle(aim, toP) < 48f)
                    {
                        for (int k = 0; k < 12; k++)
                            AddDot(pc.transform.position + Random.insideUnitSphere * 0.4f, new Color(0.4f, 1f, 1f, 1f), 1.4f, true);
                        Destroy(pc.gameObject);
                    }
                }
            }
            catch { }

            _demon.Pulse(o, 26f);
        }

        private void TickPulseWaves(float dt)
        {
            float now = Time.time;
            float sight = SvrSenses.SightMult;
            // Periodic expanding LiDAR discovery pulse wave so nearby chests, shrines, hidden caches, and moving enemies materialize when the wave washes over them
            if (now >= _nextAutoWave && _peekK < 0.65f)
            {
                float interval = OdMode == 1 ? 1.8f : (OdMode == 2 ? 2.4f : (OdMode == 3 ? 2.2f : 3.0f));
                _nextAutoWave = now + interval;
                float maxR = SvrPlugin.ScanRange.Value * sight * (OdMode == 2 ? 1.65f : (OdMode == 1 ? 1.45f : 1.2f));
                Color wc = OdMode == 1 ? new Color(1f, 0.45f, 0.16f, 1f) : new Color(0.22f, 0.96f, 1f, 1f);
                SpawnPulseWave(_body.corePosition, maxR, Mathf.Max(34f, maxR * 0.85f), wc);
            }

            Vector3 aimOrigin = _input ? _input.aimOrigin : _body.corePosition;
            Vector3 aimDir = _input ? _input.aimDirection : transform.forward;
            float baseRange = SvrPlugin.ScanRange.Value * sight;

            // Advance active expanding waves and wash over SvrLiDARScanTarget holograms + CharacterBody skeletal rigs
            for (int w = _waves.Count - 1; w >= 0; w--)
            {
                LidarWave lw = _waves[w];
                float prevR = lw.Radius;
                lw.Radius += lw.Speed * dt;
                _waves[w] = lw;

                float bandMin = prevR - 2.5f;
                float bandMax = lw.Radius + 2.5f;

                // Wash over stage interactables (chests, shrines, shops, items, teleporter)
                for (int i = 0; i < _pois.Count; i++)
                {
                    Poi p = _pois[i];
                    if (!p.Target || !p.Tr) continue;
                    float d = Vector3.Distance(lw.Origin, p.Pos);
                    if (d >= bandMin && d <= bandMax)
                        p.Target.OnWaveWash(5.2f, 1f);
                }

                // Wash over hidden LiDAR tech caches
                for (int cIdx = 0; cIdx < _techCaches.Count; cIdx++)
                {
                    FieldTechCache fc = _techCaches[cIdx];
                    if (fc.Collected || !fc.Target) continue;
                    float d = Vector3.Distance(lw.Origin, fc.Pos);
                    if (d >= bandMin && d <= bandMax)
                        fc.Target.OnWaveWash(5.8f, 1f);
                }

                // Wash over active characters, players, allies, monsters, and bosses
                try
                {
                    var bodies = CharacterBody.readOnlyInstancesList;
                    for (int bIdx = 0; bIdx < bodies.Count; bIdx++)
                    {
                        CharacterBody cb = bodies[bIdx];
                        if (!cb || cb == _body || !cb.healthComponent || !cb.healthComponent.alive) continue;
                        float d = Vector3.Distance(lw.Origin, cb.corePosition);
                        if (d >= bandMin && d <= bandMax)
                            SweepBody(cb, 2.8f, 18);
                    }
                }
                catch { }

                if (lw.Radius >= lw.MaxRadius) _waves.RemoveAt(w);
            }

            // Also continuously sweep any character/enemy/player inside the active scanner cone/dome so their animations update at 60 FPS with zero delay
            try
            {
                var bodies = CharacterBody.readOnlyInstancesList;
                float coneRange = baseRange * (OdMode == 3 ? 2.1f : (OdMode == 1 || OdMode == 2 ? 1.6f : 1.3f));
                float maxAngle = OdMode == 0 ? 68f : 180f;
                for (int bIdx = 0; bIdx < bodies.Count; bIdx++)
                {
                    CharacterBody cb = bodies[bIdx];
                    if (!cb || cb == _body || !cb.healthComponent || !cb.healthComponent.alive) continue;
                    Vector3 toBody = cb.corePosition - aimOrigin;
                    float dist = toBody.magnitude;
                    if (dist > coneRange) continue;
                    if (maxAngle < 179f && dist > 12f && Vector3.Angle(aimDir, toBody) > maxAngle) continue;
                    SweepBody(cb, 0.45f, 8);
                }
            }
            catch { }

            // Also wash any SvrLiDARScanTarget directly inside the forward scanner beam or close proximity
            for (int i = 0; i < _pois.Count; i++)
            {
                Poi p = _pois[i];
                if (!p.Target || !p.Tr) continue;
                Vector3 toP = p.Pos - aimOrigin;
                float d = toP.magnitude;
                if (d < 14f || (d < baseRange * 1.25f && Vector3.Angle(aimDir, toP) < 55f))
                    p.Target.OnWaveWash(2.6f, 0.92f);
            }
            for (int cIdx = 0; cIdx < _techCaches.Count; cIdx++)
            {
                FieldTechCache fc = _techCaches[cIdx];
                if (fc.Collected || !fc.Target) continue;
                Vector3 toC = fc.Pos - aimOrigin;
                float d = toC.magnitude;
                if (d < 16f || (d < baseRange * 1.3f && Vector3.Angle(aimDir, toC) < 55f))
                    fc.Target.OnWaveWash(3.2f, 1f);
            }
        }

        private void Update()
        {
            if (!_body) return;
            _local = IsLocal();
            if (!_local) { RestoreCamera(); return; }
            if (!Alive) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!_techSpawned && Time.timeSinceLevelLoad > 2.5f) SpawnFieldTechCaches();

            // Tapping Q sequentially rolls through the 4 modes (TOPOGRAPHY -> THERMAL -> SALVAGE -> STRUCTURAL) with a snappy audio cue
            KeyCode next, peek, trapKey, repKey, minKey, cashKey;
            int n0 = OdNames.Length;
            if (TryKey(SvrPlugin.ScanNextKey.Value, out next) && Input.GetKeyDown(next))
            {
                OdMode = (OdMode + 1) % n0;
                TriggerModeBurst();
            }

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
            // Ghost-Trap [G]: throws out a Ghostbusters / Luigi's Mansion containment trap on the floor ahead (or triggers Active-Reload Compression Slam).
            if (TryKey(SvrPlugin.TrapKey.Value, out trapKey) && Input.GetKeyDown(trapKey) && (Time.time >= _demon.TrapReadyAt || _demon.TrapActive))
            {
                _demon.DeployTrap(_body, _input);
                if (_demon.TrapActive)
                {
                    _revealUntil = Mathf.Max(_revealUntil, Time.time + SvrPlugin.TrapDuration.Value);
                    for (int i = 0; i < 120; i++)
                    {
                        float ang = (i / 120f) * 6.28318f;
                        Vector3 rp = _demon.TrapPos + new Vector3(Mathf.Cos(ang) * 1.4f, 0.1f + Random.value * 0.4f, Mathf.Sin(ang) * 1.4f);
                        AddDot(rp, (i & 1) == 0 ? new Color(1f, 0.88f, 0.12f, 1f) : new Color(0.2f, 1f, 0.55f, 1f), 1.8f, true);
                    }
                }
            }
            // Reverse-Flow Repulsor ("Blow" Function) [V]: blasts away swarms & reflects projectiles
            if (TryKey(SvrPlugin.RepulsorKey.Value, out repKey) && Input.GetKeyDown(repKey) && Time.time >= _repulsorReadyAt)
            {
                FireRepulsorBlow();
            }
            // Redeploy Captured Spirit as 30s Friendly Spectral Hologram Guardian [H]
            if (TryKey(SvrPlugin.MinionKey.Value, out minKey) && Input.GetKeyDown(minKey) && _demon.StoredSpirits > 0)
            {
                _demon.RedeployMinion(_body);
            }
            // Cash in Captured Spirit at the Teleporter [B]
            if (TryKey(SvrPlugin.CashInKey.Value, out cashKey) && Input.GetKeyDown(cashKey) && _demon.StoredSpirits > 0)
            {
                TeleporterInteraction tp = FindObjectOfType<TeleporterInteraction>();
                if (tp && Vector3.Distance(_body.corePosition, tp.transform.position) < 12f)
                {
                    _demon.CashInAtTeleporter(_body, tp.transform.position);
                }
            }

            // Check & paint LiDAR-Exclusive Hidden Tech Caches (via their SvrLiDARScanTarget 3D wireframe hologram)
            _nearCacheIdx = -1;
            for (int cIdx = 0; cIdx < _techCaches.Count; cIdx++)
            {
                FieldTechCache fc = _techCaches[cIdx];
                if (fc.Collected) continue;
                float dCache = Vector3.Distance(_body.corePosition, fc.Pos);
                if (dCache < 4.8f) _nearCacheIdx = cIdx;
                if (fc.Target && fc.Target.IsRevealed && _peekK < 0.65f)
                {
                    PaintTargetWireframe(fc.Target, 8, 0.42f);
                }
            }
            if (_nearCacheIdx >= 0 && Input.GetKeyDown(KeyCode.E))
            {
                FieldTechCache fc = _techCaches[_nearCacheIdx];
                fc.Collected = true;
                if (fc.Go) fc.Go.SetActive(false);
                _techCaches[_nearCacheIdx] = fc;
                SvrUpgrades.Unlock(fc.UpgradeId);
                Util.PlaySound("Play_UI_shrineSuccess", gameObject);
                _upgradeToast = "INSTALLED: " + SvrUpgrades.Names[fc.UpgradeId] + " — " + SvrUpgrades.Descs[fc.UpgradeId];
                _upgradeToastUntil = Time.time + 4.8f;
                for (int i = 0; i < 180; i++)
                {
                    AddDot(fc.Pos + Random.onUnitSphere * Random.Range(0.3f, 3.5f), (i & 1) == 0 ? new Color(1f, 0.86f, 0.2f, 1f) : new Color(0.25f, 1f, 0.9f, 1f), 2.2f, true);
                }
            }

            float prevRestored = _demon.LastRestoredUntil;
            _demon.Tick(dt, _body, _input);
            if (_demon.LastRestoredUntil > prevRestored)
            {
                // Celebratory containment burst when the Demon is captured and a lost sense is restored!
                for (int i = 0; i < 240; i++)
                {
                    Vector3 d = Random.onUnitSphere;
                    d.y = Mathf.Abs(d.y);
                    AddDot(_demon.TrapPos + d * Random.Range(0.2f, 4.5f), (i & 1) == 0 ? new Color(0.25f, 1f, 0.55f, 1f) : new Color(0.3f, 0.95f, 1f, 1f), 2.6f, true);
                }
            }

            // Paint the Friendly Spectral Hologram Guardian in the 3D point cloud while active
            if (_demon.MinionActive && _peekK < 0.65f)
            {
                Vector3 mp = _demon.MinionPos;
                for (int i = 0; i < 14; i++)
                {
                    float h = Random.Range(-0.9f, 0.9f);
                    float r = 0.38f * (1f - 0.35f * Mathf.Abs(h));
                    float ang = Time.time * 12f + i * 0.45f;
                    Color mc = (i & 1) == 0 ? new Color(0.25f, 1f, 0.72f, 1f) : new Color(0.35f, 0.9f, 1f, 1f);
                    AddDot(mp + new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r), mc, 0.22f, true);
                }
            }

            // Paint the active Ghost-Trap box & upward containment cone in the 3D point cloud
            if (_demon.TrapActive && _peekK < 0.65f)
            {
                Vector3 tp = _demon.TrapPos;
                // Hazard-striped Ghost-Trap casing + open angled doors on the floor
                for (int i = 0; i < 6; i++)
                {
                    float bx = Random.Range(-0.55f, 0.55f);
                    float bz = Random.Range(-0.35f, 0.35f);
                    bool yellow = (((int)((bx + 1f) * 6f) + (int)((bz + 1f) * 6f)) & 1) == 0;
                    AddDot(tp + new Vector3(bx, 0.12f, bz), yellow ? new Color(1f, 0.86f, 0.1f, 1f) : new Color(1f, 0.25f, 0.1f, 1f), 0.35f, true);
                }
                // Upward emerald/cyan Ghostbusters containment cone + spiraling Poltergust suction rings
                for (int i = 0; i < 8; i++)
                {
                    float h = Random.Range(0.15f, 4.8f);
                    float r = 0.25f + h * 0.38f;
                    float ang = Time.time * 9f + h * 2.2f + i * 0.785f;
                    Vector3 cp = tp + new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r);
                    Color cc = _demon.TetherActive
                        ? ((i & 1) == 0 ? new Color(0.2f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.12f, 1f))
                        : new Color(0.25f, 1f, 0.78f, 0.9f);
                    AddDot(cp, cc, 0.28f, true);
                }
            }

            // Pause ray scanning while the visor is fully up in Peek mode, then fire a fresh burst as the visor lowers.
            if (_peekK < 0.65f)
            {
                if (!Peeking && _peekNeedBurst)
                {
                    _peekNeedBurst = false;
                    Scan(0.25f);
                    ScanInteractables(true);
                }
                Scan(dt);
                ScanInteractables(false);
                TickPulseWaves(dt);
            }
            OverdriveTick(dt);

            if (_pulseLeft > 0 && _peekK < 0.65f)
            {
                int n = Mathf.Min(_pulseLeft, 850);
                for (int i = 0; i < n; i++) Cast(_pulseAt + Vector3.up * 0.8f, Random.onUnitSphere, _pulseRange, SvrPlugin.DotLife.Value * 1.6f, true);
                _pulseLeft -= n;
            }

            int per = (_odActive == 3 && OverdriveOn) ? 14 : (OdMode == 1 ? 9 : 6);
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                HealthComponent hc = _tracked[i];
                if (!hc || !hc.alive || !hc.body || Time.time > _trackedUntil[i]) { _tracked.RemoveAt(i); _trackedUntil.RemoveAt(i); continue; }
                SweepBody(hc.body, 0.6f, per);
            }

            if (SvrSenses.Hearing && Time.unscaledTime > _nextAudio) { _nextAudio = Time.unscaledTime + 2f; SvrAudio.Muffle(true); }
        }

        private static bool TryKey(string name, out KeyCode k)
        {
            k = KeyCode.None;
            try { return !string.IsNullOrEmpty(name) && Enum.TryParse(name.Trim(), true, out k) && k != KeyCode.None; }
            catch { return false; }
        }

        // Refresh and tag chests, shrines, shops, barrels, dropped items, and the Teleporter with SvrLiDARScanTarget.
        private void RefreshInteractables()
        {
            _pois.Clear();
            Vector3 me = _body.corePosition;
            float maxDist = SvrPlugin.ScanRange.Value * SvrSenses.SightMult * (OdMode == 2 || OdMode == 1 ? 2.2f : 1.45f);
            float maxDistSqr = maxDist * maxDist;

            try
            {
                TeleporterInteraction[] tps = FindObjectsOfType<TeleporterInteraction>();
                for (int i = 0; i < tps.Length; i++)
                {
                    TeleporterInteraction tp = tps[i];
                    if (!tp || !tp.gameObject.activeInHierarchy) continue;
                    Transform tr = tp.gameObject.transform;
                    Vector3 ext = new Vector3(3.2f, 2.2f, 3.2f);
                    Color col = new Color(1f, 0.28f, 0.95f, 1f);
                    SvrLiDARScanTarget st = EnsureScanTarget(tp.gameObject, 4, 9, "TELEPORTER", col, new Vector3(0f, 1.2f, 0f), ext);
                    if (st) st.OnWaveWash(4f, 1f); // Teleporter stays continually resonant
                    _pois.Add(new Poi
                    {
                        Tr = tr,
                        Pos = tr.position + Vector3.up * 1.2f,
                        Extents = ext,
                        Col = col,
                        Tag = "TELEPORTER",
                        Kind = 4,
                        SubShape = 9,
                        Outline = GetOrBuildOutline(tr, 4),
                        Target = st
                    });
                }
            }
            catch { }

            try
            {
                GenericPickupController[] gpcs = FindObjectsOfType<GenericPickupController>();
                for (int i = 0; i < gpcs.Length; i++)
                {
                    GenericPickupController gpc = gpcs[i];
                    if (!gpc || !gpc.gameObject.activeInHierarchy) continue;
                    Transform tr = gpc.gameObject.transform;
                    if ((tr.position - me).sqrMagnitude > maxDistSqr * 1.3f) continue;
                    string tag = "ITEM";
                    Color col = new Color(0.32f, 1f, 0.94f, 1f);
                    ResolvePickupDisplay(gpc.gameObject, ref tag, ref col);
                    Vector3 ext = new Vector3(0.55f, 0.55f, 0.55f);
                    SvrLiDARScanTarget st = EnsureScanTarget(gpc.gameObject, 3, 8, tag, col, new Vector3(0f, 0.45f, 0f), ext);
                    _pois.Add(new Poi
                    {
                        Tr = tr,
                        Pos = tr.position + Vector3.up * 0.4f,
                        Extents = ext,
                        Col = col,
                        Tag = tag,
                        Kind = 3,
                        SubShape = 8,
                        Outline = GetOrBuildOutline(tr, 3),
                        Target = st
                    });
                }
            }
            catch { }

            try
            {
                PickupPickerController[] ppcs = FindObjectsOfType<PickupPickerController>();
                for (int i = 0; i < ppcs.Length; i++)
                {
                    PickupPickerController ppc = ppcs[i];
                    if (!ppc || !ppc.gameObject.activeInHierarchy) continue;
                    Transform tr = ppc.gameObject.transform;
                    if ((tr.position - me).sqrMagnitude > maxDistSqr * 1.3f) continue;
                    string tag = "COMMAND ESSENCE";
                    Color col = new Color(0.35f, 1f, 0.95f, 1f);
                    ResolvePickupDisplay(ppc.gameObject, ref tag, ref col);
                    Vector3 ext = new Vector3(0.6f, 0.6f, 0.6f);
                    SvrLiDARScanTarget st = EnsureScanTarget(ppc.gameObject, 3, 8, tag, col, new Vector3(0f, 0.5f, 0f), ext);
                    _pois.Add(new Poi
                    {
                        Tr = tr,
                        Pos = tr.position + Vector3.up * 0.5f,
                        Extents = ext,
                        Col = col,
                        Tag = tag,
                        Kind = 3,
                        SubShape = 8,
                        Outline = GetOrBuildOutline(tr, 3),
                        Target = st
                    });
                }
            }
            catch { }

            try
            {
                PurchaseInteraction[] pis = FindObjectsOfType<PurchaseInteraction>();
                for (int i = 0; i < pis.Length; i++)
                {
                    PurchaseInteraction pi = pis[i];
                    if (!IsPurchaseOpen(pi))
                    {
                        SvrLiDARScanTarget oldSt = pi ? pi.GetComponent<SvrLiDARScanTarget>() : null;
                        if (oldSt) oldSt.ApplyHologramMaterial(false);
                        continue;
                    }
                    if (pi.GetComponentInParent<TeleporterInteraction>()) continue;
                    Transform tr = pi.gameObject.transform;
                    if ((tr.position - me).sqrMagnitude > maxDistSqr) continue;
                    string tag; Color col; int kind, subShape;
                    ClassifyInteractable(pi.gameObject, out tag, out col, out kind, out subShape);
                    if (tag == "PRINTER" || tag == "SHOP")
                    {
                        string itemTag = null;
                        Color itemCol = col;
                        ResolvePickupDisplay(pi.gameObject, ref itemTag, ref itemCol);
                        if (!string.IsNullOrEmpty(itemTag)) { tag = tag + ": " + itemTag; col = itemCol; }
                    }
                    Vector3 ext = kind == 1 ? new Vector3(1.0f, 1.45f, 1.0f) : new Vector3(0.95f, 0.78f, 0.72f);
                    Collider c = pi.GetComponentInChildren<Collider>();
                    if (c && !c.isTrigger) ext = Vector3.Max(new Vector3(0.55f, 0.55f, 0.55f), Vector3.Min(c.bounds.extents, new Vector3(2.2f, 2.6f, 2.2f)));
                    Vector3 worldCenter = c ? c.bounds.center : tr.position + Vector3.up * ext.y;
                    Vector3 localCenter = tr.InverseTransformPoint(worldCenter);
                    SvrLiDARScanTarget st = EnsureScanTarget(pi.gameObject, kind, subShape, tag, col, localCenter, ext);
                    _pois.Add(new Poi
                    {
                        Tr = tr,
                        Pos = worldCenter,
                        Extents = ext,
                        Col = col,
                        Tag = tag,
                        Kind = kind,
                        SubShape = subShape,
                        Outline = GetOrBuildOutline(tr, kind),
                        Target = st
                    });
                }
            }
            catch { }

            try
            {
                BarrelInteraction[] bis = FindObjectsOfType<BarrelInteraction>();
                for (int i = 0; i < bis.Length; i++)
                {
                    BarrelInteraction bi = bis[i];
                    if (!IsBarrelOpen(bi))
                    {
                        SvrLiDARScanTarget oldSt = bi ? bi.GetComponent<SvrLiDARScanTarget>() : null;
                        if (oldSt) oldSt.ApplyHologramMaterial(false);
                        continue;
                    }
                    Transform tr = bi.gameObject.transform;
                    if ((tr.position - me).sqrMagnitude > maxDistSqr * 0.75f) continue;
                    Vector3 ext = new Vector3(0.55f, 0.7f, 0.55f);
                    Color col = new Color(1f, 0.72f, 0.2f, 1f);
                    SvrLiDARScanTarget st = EnsureScanTarget(bi.gameObject, 2, 7, "BARREL", col, new Vector3(0f, 0.65f, 0f), ext);
                    _pois.Add(new Poi
                    {
                        Tr = tr,
                        Pos = tr.position + Vector3.up * 0.65f,
                        Extents = ext,
                        Col = col,
                        Tag = "BARREL",
                        Kind = 2,
                        SubShape = 7,
                        Outline = GetOrBuildOutline(tr, 2),
                        Target = st
                    });
                }
            }
            catch { }
        }

        private void ScanInteractables(bool burst)
        {
            float now = Time.time;
            if (now >= _nextPoiRefresh)
            {
                _nextPoiRefresh = now + 0.35f;
                RefreshInteractables();
            }
            if (!burst && now < _nextPoiPaint) return;
            _nextPoiPaint = now + (OdMode == 2 ? 0.042f : 0.068f);

            Vector3 me = _body.corePosition;
            float baseRange = SvrPlugin.ScanRange.Value * SvrSenses.SightMult;
            float maxRange = baseRange * (OdMode == 2 ? 2.0f : (OdMode == 1 ? 1.55f : 1.35f));
            int mult = (burst ? 3 : 1) * (OdMode == 2 ? 2 : 1);
            float life = SvrPlugin.DotLife.Value * 0.58f;

            for (int i = 0; i < _pois.Count; i++)
            {
                Poi p = _pois[i];
                if (!p.Tr || !p.Tr.gameObject.activeInHierarchy) continue;
                if (burst && p.Target) p.Target.OnWaveWash(5.0f, 1f);

                Vector3 pos = p.Pos;
                float dist = Vector3.Distance(me, pos);

                if (p.Kind == 4) // TELEPORTER: always paint out to 240m with base ring + actual 3D teleporter prongs + tall vertical beacon spire
                {
                    if (dist > 240f) continue;
                    int ringPts = 14 * mult;
                    for (int r = 0; r < ringPts; r++)
                    {
                        float ang = (r / (float)ringPts) * 6.28318f + now * 1.4f;
                        float rad = 3.2f + 0.4f * Mathf.Sin(now * 3f + r);
                        Vector3 rp = p.Tr.position + new Vector3(Mathf.Cos(ang) * rad, 0.25f + Random.value * 0.6f, Mathf.Sin(ang) * rad);
                        Color tc = (r & 1) == 0 ? new Color(1f, 0.28f, 0.95f, 1f) : new Color(0.2f, 0.95f, 1f, 1f);
                        AddDot(rp, tc, life * 1.2f, true);
                    }
                    int spirePts = 10 * mult;
                    for (int s = 0; s < spirePts; s++)
                    {
                        float h = Random.Range(0.5f, 16f);
                        Vector3 sp = p.Tr.position + new Vector3(Random.Range(-0.45f, 0.45f), h, Random.Range(-0.45f, 0.45f));
                        AddDot(sp, new Color(1f, 0.35f, 0.98f, 1f), life, true);
                    }
                    if (p.Target) PaintTargetWireframe(p.Target, 12 * mult, life);
                    continue;
                }

                if (dist > maxRange) continue;

                // Hologram Discovery Loop: only materialize the 3D wireframe hologram once a LiDAR pulse wave or scan beam has washed over it!
                if (p.Target && !p.Target.IsRevealed)
                {
                    p.Target.ApplyHologramMaterial(false);
                    continue;
                }

                int sampleBudget = (p.Kind == 3 ? 16 : (p.Kind == 2 ? 9 : 14)) * mult;
                if (p.Target && p.Target.Parts.Count > 0)
                {
                    PaintTargetWireframe(p.Target, sampleBudget, life);
                }
                else if (p.Outline != null && p.Outline.Parts.Count > 0)
                {
                    PaintMeshOutline(p.Outline, p.Col, sampleBudget, life);
                }
            }
        }

        private void PaintTargetWireframe(SvrLiDARScanTarget target, int count, float life)
        {
            if (!target || target.Parts.Count == 0) return;
            int parts = target.Parts.Count;
            Color baseCol = target.HoloColor;
            Color edgeCol = Color.Lerp(baseCol, Color.white, 0.35f);
            for (int n = 0; n < count; n++)
            {
                SvrLiDARScanTarget.WirePart wp = target.Parts[n % parts];
                if (!wp.Tr || wp.EdgeA == null || wp.EdgeA.Length == 0) continue;
                int eIdx = Random.Range(0, wp.EdgeA.Length);
                Vector3 localPt = Vector3.Lerp(wp.EdgeA[eIdx], wp.EdgeB[eIdx], Random.value);
                Vector3 worldPt = wp.Tr.TransformPoint(localPt);
                AddDot(worldPt, (n & 1) == 0 ? edgeCol : baseCol, life, true);
            }
        }

        private void PaintMeshOutline(PoiOutline outline, Color baseCol, int count, float life)
        {
            int parts = outline.Parts.Count;
            if (parts == 0) return;
            Color edgeCol = Color.Lerp(baseCol, Color.white, 0.28f);
            for (int n = 0; n < count; n++)
            {
                MeshPart mp = outline.Parts[n % parts];
                if (!mp.Tr || mp.Data == null) continue;
                CachedMeshData d = mp.Data;
                Vector3 localPt;
                bool isEdge = d.EdgeA != null && d.EdgeA.Length > 0 && (n % 3 != 0);
                if (isEdge)
                {
                    int eIdx = Random.Range(0, d.EdgeA.Length);
                    localPt = Vector3.Lerp(d.EdgeA[eIdx], d.EdgeB[eIdx], Random.value);
                }
                else if (d.Verts != null && d.Verts.Length > 0)
                {
                    localPt = d.Verts[Random.Range(0, d.Verts.Length)];
                }
                else continue;

                Vector3 worldPt = mp.Tr.TransformPoint(localPt);
                AddDot(worldPt, isEdge ? edgeCol : baseCol, life, true);
            }
        }

        // The always-on scanner. Each mode cycled by Q (0=TOPOGRAPHY, 1=THERMAL, 2=SALVAGE, 3=STRUCTURAL) has a distinct geometry and range profile.
        private void Scan(float dt)
        {
            if (!_input) return;
            float prox = _demon.Proximity(_body.corePosition);
            if (prox > 0.2f && Random.value < prox * 0.85f) return;      // the suit's scanner stutters and drops frames near it
            float sight = SvrSenses.SightMult;
            float frames = Mathf.Clamp(dt * 60f, 0.5f, 4f);
            int baseRays = Mathf.RoundToInt(SvrPlugin.RaysPerFrame.Value * frames * sight * (1f - 0.5f * prox));
            float range = SvrPlugin.ScanRange.Value * sight;
            Vector3 o = _input.aimOrigin;
            Vector3 aim = _input.aimDirection;
            float life = SvrPlugin.DotLife.Value * (sight < 1f ? 0.7f : 1f);
            switch (OdMode % 4)
            {
                case 1:     // THERMAL: Wide cone + seismic ground rings + 115m wall-penetrating enemy/boss/demon sonar
                {
                    int rays = Mathf.RoundToInt(baseRays * 1.5f);
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        if (i % 4 == 0)
                        {
                            // Seismic ground/wall ring
                            float yaw = (_spin * 9.7f) % 360f;
                            float pitch = Random.Range(-35f, 25f);
                            Cast(o, Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward, range * 1.2f, life, false);
                            continue;
                        }
                        float f2 = ((_spin % 257) + Random.value) / 257f;
                        Cast(o, Cone(aim, 56f * Mathf.Sqrt(f2), _spin * 2.39996f), range * 1.15f, life, false);
                    }
                    if (Time.time >= _echoPing)
                    {
                        _echoPing = Time.time + 0.50f;
                        _revealUntil = Mathf.Max(_revealUntil, Time.time + 0.75f); // THERMAL also reveals the Shimmer Demon through walls
                        try
                        {
                            SphereSearch s = new SphereSearch { origin = _body.corePosition, radius = 115f * sight, mask = LayerIndex.entityPrecise.mask };
                            s.RefreshCandidates();
                            s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex));
                            s.FilterCandidatesByDistinctHurtBoxEntities();
                            foreach (HurtBox hb in s.GetHurtBoxes())
                                if (hb && hb.healthComponent && hb.healthComponent.alive && !_tracked.Contains(hb.healthComponent) && _tracked.Count < 45)
                                {
                                    _tracked.Add(hb.healthComponent);
                                    _trackedUntil.Add(Time.time + 0.75f);
                                }
                        }
                        catch { }
                    }
                    break;
                }
                case 2:     // SALVAGE: Massive 360 omni-dome + concentric latitude bands + long-range loot & hologram hunter
                {
                    int rays = Mathf.RoundToInt(baseRays * 1.65f);
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        if (i % 3 == 0)
                        {
                            // Structured 360 horizontal & dome bands so walls, rooms, and chests read crisply all around her
                            float bandYaw = (_spin * 7.13f) % 360f;
                            float bandPitch = Mathf.Lerp(-50f, 55f, ((_spin / 36) % 12) / 11f) + Random.Range(-3f, 3f);
                            Cast(o, Quaternion.Euler(-bandPitch, bandYaw, 0f) * Vector3.forward, range * 1.3f, life * 1.15f, false);
                        }
                        else
                        {
                            Vector3 d = Random.onUnitSphere;
                            d.y *= 0.72f;
                            Cast(o, d.normalized, range * 1.2f, life, false);
                        }
                    }
                    break;
                }
                case 3:     // STRUCTURAL: Twin opposing 360 radar walls + expanding sonar floor ring + long-range spotlight & target lock
                {
                    int rays = Mathf.RoundToInt(baseRays * 1.7f);
                    _scanYaw += 310f * dt;
                    _sweepRing = (_sweepRing + dt * 0.85f) % 1f;
                    float ringYawBase = Time.time * 90f;
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        int mod = i % 5;
                        if (mod == 0 || mod == 2)
                        {
                            // Ultra-long forward structural spotlight (2.2x range)
                            float f1 = ((_spin % 257) + Random.value) / 257f;
                            float spread = (mod == 0) ? 16f * Mathf.Sqrt(f1) : 36f * Mathf.Sqrt(f1);
                            float r = (mod == 0) ? range * 2.2f : range * 1.5f;
                            Cast(o, Cone(aim, spread, _spin * 2.39996f), r, life * 1.35f, i % 6 == 0);
                        }
                        else if (mod == 1)
                        {
                            // Expanding horizontal sonar floor/wall ring sweeping outward
                            float rYaw = ringYawBase + (i / (float)rays) * 360f + Random.Range(-4f, 4f);
                            float rPitch = Mathf.Lerp(-48f, 18f, _sweepRing) + Random.Range(-3f, 3f);
                            Cast(o, Quaternion.Euler(-rPitch, rYaw, 0f) * Vector3.forward, range * 1.55f, life * 1.25f, true);
                        }
                        else
                        {
                            // Twin opposing vertical radar walls (front wall + rear wall 180 deg apart)
                            float wallOffset = (i & 1) == 0 ? 0f : 180f;
                            float yaw = _scanYaw + wallOffset + Random.Range(-3.5f, 3.5f);
                            float pitch = Mathf.Lerp(-65f, 72f, Random.value);
                            Cast(o, Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward, range * 1.6f, life * 1.35f, false);
                        }
                    }
                    if (Time.time >= _focusLockNext)
                    {
                        _focusLockNext = Time.time + 0.35f;
                        try
                        {
                            BullseyeSearch bs = new BullseyeSearch
                            {
                                searchOrigin = o,
                                searchDirection = aim,
                                maxDistanceFilter = range * 2.2f,
                                maxAngleFilter = 22f,
                                teamMaskFilter = TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex),
                                filterByLoS = false,
                                sortMode = BullseyeSearch.SortMode.Angle
                            };
                            bs.RefreshCandidates();
                            foreach (HurtBox hb in bs.GetResults())
                                if (hb && hb.healthComponent && hb.healthComponent.alive && !_tracked.Contains(hb.healthComponent) && _tracked.Count < 35)
                                {
                                    _tracked.Add(hb.healthComponent);
                                    _trackedUntil.Add(Time.time + 0.65f);
                                }
                        }
                        catch { }
                    }
                    break;
                }
                default:    // TOPOGRAPHY: Wide 65-degree high-density terrain contour flood ahead + 360 proximity skirt
                {
                    int rays = Mathf.RoundToInt(baseRays * 1.5f);
                    for (int i = 0; i < rays; i++)
                    {
                        _spin++;
                        if (i % 4 == 0)
                        {
                            // 360 close-range room/foot skirt so walls and ledges beside her are always painted
                            Vector3 d = Random.onUnitSphere;
                            d.y *= 0.65f;
                            Cast(o, d.normalized, range * 0.65f, life, false);
                            continue;
                        }
                        float f = ((_spin % 257) + Random.value) / 257f;
                        Cast(o, Cone(aim, 65f * Mathf.Sqrt(f), _spin * 2.39996f), range * 1.3f, life, false);
                    }
                    break;
                }
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
                        for (int i = 0; i < 64; i++)
                        {
                            float pitch = Mathf.Lerp(-65f, 72f, (i + Random.value) / 64f);
                            Cast(o, Quaternion.Euler(-pitch, _sweepYaw, 0f) * Vector3.forward, 130f * sight, life, false);
                        }
                    }
                    break;
                }
                case 2:
                {
                    int n = Mathf.RoundToInt(680f * Mathf.Clamp(dt * 60f, 0.5f, 3f) * sight);
                    for (int i = 0; i < n; i++)
                        Cast(o, Cone(aim, 8.5f * Mathf.Sqrt(Random.value), Random.value * 6.283f), 220f * sight, life * 1.3f, true);
                    break;
                }
                case 3:
                {
                    if (Time.time < _echoNext) break;
                    _echoNext = Time.time + 0.35f;
                    try
                    {
                        SphereSearch s = new SphereSearch { origin = _body.corePosition, radius = 140f, mask = LayerIndex.entityPrecise.mask };
                        s.RefreshCandidates();
                        s.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex));
                        s.FilterCandidatesByDistinctHurtBoxEntities();
                        foreach (HurtBox hb in s.GetHurtBoxes())
                            if (hb && hb.healthComponent && hb.healthComponent.alive && !_tracked.Contains(hb.healthComponent) && _tracked.Count < 50)
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

            // Peek intensity (_peekK): smoothly ramps 0 -> 1 over ~0.35s as the visor lifts, holds at 1 while peeking,
            // and smoothly ramps 1 -> 0 over ~0.4s as the visor comes back down.
            float now = Time.time;
            float target = 0f;
            if (Peeking)
            {
                float rem = _peekUntil - now;
                target = rem > 0.35f ? 1f : Mathf.Clamp01(rem / 0.35f);
            }
            float speed = Peeking ? 2.85f : 2.5f;
            _peekK = Mathf.MoveTowards(_peekK, target, Time.deltaTime * speed);

            Camera cam = Cam();
            if (!cam) return;
            _peekCam = cam;

            if (Peeking || _peekK > 0.02f)
            {
                RestoreCamera();                          // keep the real world visible throughout the entire transition
                ApplyPeekFx(cam, _peekK);
                return;
            }
            else
            {
                ApplyPeekFx(cam, 0f);
            }

            if (!SvrPlugin.DarkWorld.Value) { RestoreDim(); SuppressStageFogAndColorGrading(false); return; }
            SuppressStageFogAndColorGrading(false);
            if (!_dimmed || _dimCam != cam)
            {
                RestoreDim();
                _dimCam = cam;
                _origMask = cam.cullingMask;
                _origFlags = cam.clearFlags;
                _origBg = cam.backgroundColor;

                // Disable PostProcessLayer on the dimmed camera so RoR2's RampFog shader does NOT render a flat gray
                // ground/horizon fog plane across the empty depth buffer when cullingMask == 0!
                try
                {
                    Type layerType = PT("PostProcessLayer");
                    if (layerType != null)
                    {
                        _dimPpLayer = cam.GetComponent(layerType) as Behaviour;
                        if (_dimPpLayer)
                        {
                            _dimPpOrigEnabled = _dimPpLayer.enabled;
                            _dimPpLayer.enabled = false;
                        }
                    }
                }
                catch { }

                // Also disable skyboxCam on the active CameraRigController while in DarkWorld mode
                try
                {
                    foreach (CameraRigController rig in CameraRigController.readOnlyInstancesList)
                    {
                        if (rig && rig.sceneCam == cam && rig.skyboxCam)
                        {
                            _dimSkyboxCam = rig.skyboxCam;
                            _dimSkyboxOrigEnabled = _dimSkyboxCam.enabled;
                            _dimSkyboxCam.enabled = false;
                            break;
                        }
                    }
                }
                catch { }

                _dimmed = true;
            }
            cam.cullingMask = 0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            if (_dimPpLayer && _dimPpLayer.enabled) _dimPpLayer.enabled = false;
            if (_dimSkyboxCam && _dimSkyboxCam.enabled) _dimSkyboxCam.enabled = false;
        }

        private void RestoreCamera()
        {
            RestoreDim();
            RestoreStageFog();
        }

        // Scan-mode accent colors for the HUD (high-contrast cyan / orange / amber / azure — no green).
        private static Color ModeAccent(int mode)
        {
            switch (mode % 4)
            {
                case 1: return new Color(1f, 0.5f, 0.16f, 0.95f);    // THERMAL: hot orange
                case 2: return new Color(1f, 0.78f, 0.25f, 0.95f);   // SALVAGE: amber
                case 3: return new Color(0.4f, 0.72f, 1f, 0.95f);    // STRUCTURAL: azure
                default: return new Color(0.22f, 0.95f, 1f, 0.95f);  // TOPOGRAPHY: electric cyan
            }
        }

        private bool _fogSuppressed;
        private Camera _fogCam;
        private static Type _tPpVolume, _tWeather, _tHookLighting;
        private static bool _fogTypesResolved;

        private static Type FindTypeByName(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = a.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        // While the LiDAR visor is down, banish the stage's fog, mist and color grading (Wetland Aspect / Distant Roost green haze):
        // scene PostProcessVolumes (RampFog, ColorGrading), SceneWeatherController, HookLightingIntoPostProcessVolume,
        // Unity RenderSettings fog/ambient, and push the camera far clip out so distant wireframes never fade into horizon fog.
        private void SuppressStageFogAndColorGrading(bool forceSweep)
        {
            if (!_local || Peeking || _peekK > 0.02f) return;
            Camera cam = Cam();
            if (!cam) return;

            try
            {
                if (!_fogSuppressed)
                {
                    _origFog = RenderSettings.fog;
                    _origFogMode = RenderSettings.fogMode;
                    _origFogDensity = RenderSettings.fogDensity;
                    _origFogStart = RenderSettings.fogStartDistance;
                    _origFogEnd = RenderSettings.fogEndDistance;
                    _origFogColor = RenderSettings.fogColor;
                    _origAmbientLight = RenderSettings.ambientLight;
                    _origAmbientIntensity = RenderSettings.ambientIntensity;
                    _origSkyboxMat = RenderSettings.skybox;
                    _fogCam = cam;
                    _origFarClip = cam.farClipPlane;
                    _fogSuppressed = true;
                    forceSweep = true;
                }
                else if (_fogCam != cam)
                {
                    if (_fogCam) _fogCam.farClipPlane = _origFarClip;
                    _fogCam = cam;
                    _origFarClip = cam.farClipPlane;
                }
                ApplyFogOverrides(cam);
            }
            catch { }

            if (!forceSweep && Time.unscaledTime < _nextFogSweepTime) return;
            _nextFogSweepTime = Time.unscaledTime + 1.5f;

            if (!_fogTypesResolved)
            {
                _fogTypesResolved = true;
                _tPpVolume = PT("PostProcessVolume");
                _tWeather = FindTypeByName("RoR2.SceneWeatherController");
                _tHookLighting = FindTypeByName("RoR2.HookLightingIntoPostProcessVolume");
            }
            SuppressAllOfType(_tPpVolume, true);
            SuppressAllOfType(_tWeather, false);
            SuppressAllOfType(_tHookLighting, false);
        }

        private void SuppressAllOfType(Type t, bool isVolume)
        {
            if (t == null) return;
            try
            {
                UnityEngine.Object[] found = FindObjectsOfType(t);
                for (int i = 0; i < found.Length; i++)
                {
                    Behaviour b = found[i] as Behaviour;
                    if (!b || !b.enabled) continue;
                    if (isVolume && (b.gameObject.name == "SvrPeekVolume" || (_ppVol && b == _ppVol))) continue;
                    b.enabled = false;
                    if (!_suppressedStageVolumes.Contains(b)) _suppressedStageVolumes.Add(b);
                }
            }
            catch { }
        }

        private void ApplyFogOverrides(Camera cam)
        {
            RenderSettings.fog = false;
            RenderSettings.fogDensity = 0f;
            RenderSettings.fogStartDistance = 10000f;
            RenderSettings.fogEndDistance = 20000f;
            RenderSettings.fogColor = Color.black;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            if (cam) cam.farClipPlane = Mathf.Max(_origFarClip, 6000f);
        }

        private void RestoreStageFog()
        {
            if (!_fogSuppressed) return;
            _fogSuppressed = false;
            try
            {
                RenderSettings.fog = _origFog;
                RenderSettings.fogMode = _origFogMode;
                RenderSettings.fogDensity = _origFogDensity;
                RenderSettings.fogStartDistance = _origFogStart;
                RenderSettings.fogEndDistance = _origFogEnd;
                RenderSettings.fogColor = _origFogColor;
                RenderSettings.ambientLight = _origAmbientLight;
                RenderSettings.ambientIntensity = _origAmbientIntensity;
                if (_origSkyboxMat) RenderSettings.skybox = _origSkyboxMat;
            }
            catch { }
            try { if (_fogCam) _fogCam.farClipPlane = _origFarClip; } catch { }
            _fogCam = null;
            for (int i = 0; i < _suppressedStageVolumes.Count; i++)
            {
                try { if (_suppressedStageVolumes[i]) _suppressedStageVolumes[i].enabled = true; } catch { }
            }
            _suppressedStageVolumes.Clear();
            _nextFogSweepTime = 0f;
        }

        private void RestoreDim()
        {
            if (_dimmed && _dimCam)
            {
                _dimCam.cullingMask = _origMask;
                _dimCam.clearFlags = _origFlags;
                _dimCam.backgroundColor = _origBg;
            }
            if (_dimPpLayer)
            {
                try { _dimPpLayer.enabled = _dimPpOrigEnabled; } catch { }
                _dimPpLayer = null;
            }
            if (_dimSkyboxCam)
            {
                try { _dimSkyboxCam.enabled = _dimSkyboxOrigEnabled; } catch { }
                _dimSkyboxCam = null;
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
            // Keep the stage fog / horizon fade banished right before the LiDAR camera renders (RoR2 can rewrite RenderSettings mid-frame).
            if (_local && _fogSuppressed && c == _fogCam && _peekK < 0.02f)
            {
                try { ApplyFogOverrides(c); } catch { }
            }
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

            // Ensure a pure, solid black canvas backdrop when DarkWorld is active and visor is down (squashes any camera/UI gray plane)
            if (SvrPlugin.DarkWorld.Value && _peekK < 0.02f)
            {
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }

            // Smoothly fade all LiDAR dots, 3D holograms, animated skeletons, and demon points from 100% -> 0% as Peek lifts the visor,
            // and smoothly fade them back from 0% -> 100% as the visor lowers.
            float dotAlpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(_peekK * 1.15f));
            DrawDots(cam, scale, dotAlpha);
            DrawPulseWaves(cam, scale, dotAlpha);
            DrawHologramTargets(cam, scale, dotAlpha);
            DrawAnimatedSkeletons(cam, scale, dotAlpha);
            DrawDemon(cam, scale, dotAlpha);
            DrawPeekWash(scale);
            DrawHud(scale);
            GUI.color = old;
        }

        private void DrawDots(Camera cam, float scale, float dotAlpha)
        {
            if (dotAlpha <= 0.005f) return;

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
                float a = d.C.a * fade * dotAlpha;
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
            GUI.color = new Color(1f, 1f, 1f, dotAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _buf, ScaleMode.StretchToFill, true);
        }

        private static void DrawScreenLine(Vector2 a, Vector2 b, float thick, Texture2D tex)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.75f) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(1.6f, thick * 0.7f)), 1, 64);
            float half = thick * 0.5f;
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                GUI.DrawTexture(new Rect(p.x - half, p.y - half, thick, thick), tex);
            }
        }

        private static void DrawWorldEdge(Camera cam, Vector3 worldA, Vector3 worldB, float thick, Texture2D tex)
        {
            Vector3 sa = cam.WorldToScreenPoint(worldA);
            Vector3 sb = cam.WorldToScreenPoint(worldB);
            if (sa.z <= 0.3f || sb.z <= 0.3f) return;
            Vector2 a = new Vector2(sa.x, Screen.height - sa.y);
            Vector2 b = new Vector2(sb.x, Screen.height - sb.y);
            if ((a.x < -80f && b.x < -80f) || (a.x > Screen.width + 80f && b.x > Screen.width + 80f) ||
                (a.y < -80f && b.y < -80f) || (a.y > Screen.height + 80f && b.y > Screen.height + 80f))
                return;
            DrawScreenLine(a, b, thick, tex);
        }

        // Draws the expanding 3D LiDAR discovery pulse waves sweeping across the terrain
        private void DrawPulseWaves(Camera cam, float scale, float dotAlpha)
        {
            if (dotAlpha <= 0.005f || _waves.Count == 0) return;
            Texture2D white = Texture2D.whiteTexture;
            const int segs = 36;
            for (int w = 0; w < _waves.Count; w++)
            {
                LidarWave lw = _waves[w];
                float u = Mathf.Clamp01(lw.Radius / Mathf.Max(1f, lw.MaxRadius));
                float a = (1f - u * u) * 0.78f * dotAlpha;
                if (a <= 0.02f) continue;

                GUI.color = new Color(lw.Col.r, lw.Col.g, lw.Col.b, a);
                float thick = Mathf.Lerp(2.6f, 1.2f, u) * scale;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= segs; i++)
                {
                    float ang = (i % segs) * (6.2831853f / segs);
                    Vector3 cur = lw.Origin + new Vector3(Mathf.Cos(ang) * lw.Radius, 0.15f, Mathf.Sin(ang) * lw.Radius);
                    if (i > 0) DrawWorldEdge(cam, prev, cur, thick, white);
                    prev = cur;
                }
            }
        }

        // ================================================================ 2. THE HOLOGRAM DISCOVERY LOOP RENDERER
        // Renders each SvrLiDARScanTarget that has been washed by a LiDAR pulse wave as a glowing 3D wireframe hologram
        // of its exact shape (small chest, large chest, blood shrine, chance shrine, mountain shrine, 3D printer, multishop,
        // barrel, item pedestal, drop pod, hex crate, service panel, teleporter) — with NO floating 2D brackets or text popup boxes from afar!
        private void DrawHologramTargets(Camera cam, float scale, float dotAlpha)
        {
            if (dotAlpha <= 0.005f) return;
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.fontStyle = FontStyle.Bold;
                _label.alignment = TextAnchor.MiddleLeft;
            }
            int origFs = _label.fontSize;
            _label.fontSize = Mathf.Max(9, Mathf.RoundToInt(12f * scale));
            Texture2D white = Texture2D.whiteTexture;

            float now = Time.time;
            Vector3 me = _body.corePosition;
            float baseRange = SvrPlugin.ScanRange.Value * SvrSenses.SightMult;
            float maxDrawRange = baseRange * (OdMode == 2 ? 1.95f : (OdMode == 1 ? 1.5f : 1.3f));
            int drawn = 0;

            for (int i = 0; i < _pois.Count; i++)
            {
                Poi p = _pois[i];
                if (!p.Tr || !p.Tr.gameObject.activeInHierarchy || !p.Target) continue;
                float dist = Vector3.Distance(me, p.Pos);
                float maxD = p.Kind == 4 ? 220f : (p.Kind == 2 ? maxDrawRange * 0.7f : maxDrawRange);
                if (dist > maxD) continue;
                if (p.Kind != 4 && !p.Target.IsRevealed) continue;
                if (p.Kind != 4 && drawn >= 22) continue;

                Vector3 sp = cam.WorldToScreenPoint(p.Pos);
                if (sp.z <= 0.4f) continue;

                float holoAlpha = (p.Kind == 4 ? 0.92f : p.Target.CurrentAlpha(now)) * Mathf.Clamp01(1.18f - dist / maxD) * dotAlpha;
                if (holoAlpha <= 0.03f) continue;

                drawn++;
                RenderSingleHologram(cam, p.Target, p.Pos, dist, holoAlpha, scale, now, white);
            }

            // Also render revealed LiDAR-Exclusive Hidden Tech Caches (Drop Pods, Hex Crates, Service Panels) as 3D wireframe holograms
            for (int cIdx = 0; cIdx < _techCaches.Count; cIdx++)
            {
                FieldTechCache fc = _techCaches[cIdx];
                if (fc.Collected || !fc.Target || !fc.Target.IsRevealed) continue;
                float dist = Vector3.Distance(me, fc.Pos);
                if (dist > maxDrawRange * 1.35f) continue;
                Vector3 sp = cam.WorldToScreenPoint(fc.Pos + Vector3.up * 0.8f);
                if (sp.z <= 0.4f) continue;

                float holoAlpha = fc.Target.CurrentAlpha(now) * Mathf.Clamp01(1.25f - dist / (maxDrawRange * 1.35f)) * dotAlpha;
                if (holoAlpha <= 0.03f) continue;

                RenderSingleHologram(cam, fc.Target, fc.Pos + Vector3.up * 0.8f, dist, holoAlpha, scale, now, white);
            }

            _label.fontSize = origFs;
        }

        private void RenderSingleHologram(Camera cam, SvrLiDARScanTarget target, Vector3 worldCenter, float dist, float alpha, float scale, float now, Texture2D white)
        {
            if (!target.Built && now >= target.NextBuildAttempt) target.BuildWireframe();
            if (target.Parts.Count == 0) return;

            Color baseCol = new Color(target.HoloColor.r, target.HoloColor.g, target.HoloColor.b, alpha * 0.88f);
            Color coreGlow = Color.Lerp(baseCol, new Color(1f, 1f, 1f, alpha), 0.42f);
            float lineThick = Mathf.Clamp(Mathf.Lerp(2.2f, 1.15f, dist / 65f) * scale, 1.0f, 2.8f * scale);

            // Moving horizontal 3D holographic scan-plane sweeping vertically through the object's 3D wireframe
            float scanCycle = (now * 1.65f + (target.GetInstanceID() & 15) * 0.17f) % 1f;
            float scanLocalY = Mathf.Lerp(0f, target.Extents.y * 2.0f, scanCycle);

            // 1) Draw all 3D wireframe edges of the object's exact 3D geometry & architectural hologram
            int partsCount = target.Parts.Count;
            for (int pIdx = 0; pIdx < partsCount; pIdx++)
            {
                SvrLiDARScanTarget.WirePart wp = target.Parts[pIdx];
                if (!wp.Tr || wp.EdgeA == null || wp.EdgeB == null) continue;
                int eLen = wp.EdgeA.Length;
                int step = dist > 55f ? 2 : 1;
                for (int e = 0; e < eLen; e += step)
                {
                    Vector3 la = wp.EdgeA[e];
                    Vector3 lb = wp.EdgeB[e];
                    Vector3 wa = wp.Tr.TransformPoint(la);
                    Vector3 wb = wp.Tr.TransformPoint(lb);
                    float midY = 0.5f * (la.y + lb.y);
                    bool nearScanPlane = Mathf.Abs(midY - scanLocalY) < target.Extents.y * 0.28f;
                    GUI.color = nearScanPlane ? coreGlow : baseCol;
                    DrawWorldEdge(cam, wa, wb, nearScanPlane ? lineThick * 1.3f : lineThick, white);
                }

                // Draw glowing 3D vertex nodes on close-to-mid-range holograms
                if (dist < 38f && wp.Verts != null && wp.Verts.Length > 0)
                {
                    GUI.color = coreGlow;
                    float nodeSz = Mathf.Clamp(3.2f * scale, 2f, 4.5f);
                    int vStep = Mathf.Max(1, wp.Verts.Length / 18);
                    for (int v = 0; v < wp.Verts.Length; v += vStep)
                    {
                        Vector3 sv = cam.WorldToScreenPoint(wp.Tr.TransformPoint(wp.Verts[v]));
                        if (sv.z > 0.35f)
                            GUI.DrawTexture(new Rect(sv.x - nodeSz * 0.5f, Screen.height - sv.y - nodeSz * 0.5f, nodeSz, nodeSz), white);
                    }
                }
            }

            // 2) Draw the moving 3D horizontal scan-plane ring slicing through the hologram
            if (dist < 65f && target.transform)
            {
                GUI.color = new Color(coreGlow.r, coreGlow.g, coreGlow.b, alpha * 0.55f);
                Vector3 prevRing = Vector3.zero;
                const int ringSegs = 12;
                float rx = target.Extents.x * 0.92f;
                float rz = target.Extents.z * 0.92f;
                for (int r = 0; r <= ringSegs; r++)
                {
                    float ang = (r % ringSegs) * (6.2831853f / ringSegs);
                    Vector3 localPt = new Vector3(Mathf.Cos(ang) * rx, scanLocalY, Mathf.Sin(ang) * rz);
                    Vector3 worldPt = target.transform.TransformPoint(localPt);
                    if (r > 0) DrawWorldEdge(cam, prevRing, worldPt, Mathf.Max(1f, lineThick * 0.85f), white);
                    prevRing = worldPt;
                }
            }

            // 3) Only when standing right next to the hologram (< 5.5m) or looking directly at it at close range (< 11m within 9 deg),
            // render a clean, box-free holographic text projection (no opaque/gray UI background box!).
            bool closeInspect = dist < 5.5f;
            if (!closeInspect && dist < 11f && _input)
            {
                closeInspect = Vector3.Angle(_input.aimDirection, worldCenter - _input.aimOrigin) < 9f;
            }
            if (closeInspect && !string.IsNullOrEmpty(target.Label))
            {
                Vector3 topSp = cam.WorldToScreenPoint(worldCenter + Vector3.up * (target.Extents.y * 0.85f + 0.35f));
                if (topSp.z > 0.35f)
                {
                    string lbl = target.Label + " · " + Mathf.RoundToInt(dist) + "m";
                    float tx = topSp.x - (lbl.Length * 3.6f * scale);
                    float ty = Screen.height - topSp.y - 16f * scale;
                    GUI.color = coreGlow;
                    GUI.Label(new Rect(tx, ty, 320f * scale, 22f * scale), lbl, _label);
                }
            }
        }

        // ================================================================ 1. THE FULLY ANIMATED LIDAR PATCH RENDERER
        // Renders active skeletal transforms (Transform bones), anatomical HurtBox limb cages, rigid weapon meshes,
        // and live SkinnedMeshRenderer skinning poses in real time for characters, players, monsters, and bosses!
        // Visible through walls and particle smoke with zero delay.
        private void DrawAnimatedSkeletons(Camera cam, float scale, float dotAlpha)
        {
            if (dotAlpha <= 0.005f || _skeletalRigs.Count == 0) return;
            Texture2D white = Texture2D.whiteTexture;
            float now = Time.time;
            Vector3 me = _body.corePosition;
            float maxRange = SvrPlugin.ScanRange.Value * SvrSenses.SightMult * (OdMode == 3 ? 2.2f : (OdMode == 1 ? 1.85f : 1.45f));

            int drawnRigs = 0;
            foreach (KeyValuePair<int, SvrSkeletalRig> kv in _skeletalRigs)
            {
                SvrSkeletalRig rig = kv.Value;
                if (rig == null || !rig.Body || !rig.Body.healthComponent || !rig.Body.healthComponent.alive) continue;
                if (rig.Body == _body) continue;
                if (now >= rig.ScannedUntil) continue;

                float dist = Vector3.Distance(me, rig.Body.corePosition);
                if (dist > maxRange) continue;
                if (drawnRigs >= 24) break;

                Vector3 rootSp = cam.WorldToScreenPoint(rig.Body.corePosition);
                if (rootSp.z <= 0.3f) continue;

                float rem = rig.ScannedUntil - now;
                float fade = Mathf.Clamp01(rem / 0.35f) * Mathf.Clamp01(1.15f - dist / maxRange) * dotAlpha;
                if (fade <= 0.03f) continue;

                drawnRigs++;
                bool isFriendly = rig.Body.teamComponent && _body.teamComponent && rig.Body.teamComponent.teamIndex == _body.teamComponent.teamIndex;
                bool isBoss = rig.Body.isBoss || rig.Body.isChampion;

                Color wireCol = isFriendly
                    ? new Color(0.25f, 1f, 0.88f, fade * 0.92f)
                    : (isBoss ? new Color(1f, 0.45f, 0.12f, fade * 0.95f) : new Color(1f, 0.22f, 0.18f, fade * 0.92f));
                Color jointCol = Color.Lerp(wireCol, new Color(1f, 1f, 1f, fade), 0.45f);
                Color cageCol = new Color(wireCol.r, wireCol.g, wireCol.b, fade * 0.58f);
                float boneThick = Mathf.Clamp(Mathf.Lerp(2.4f, 1.2f, dist / 60f) * scale, 1.1f, 2.8f * scale);
                float cageThick = Mathf.Max(1.0f, boneThick * 0.72f);

                // A) Draw real-time active Transform bone links + glowing joint nodes (updates at 60 FPS with zero lag!)
                if (rig.Bones.Count > 0)
                {
                    GUI.color = wireCol;
                    for (int b = 0; b < rig.Bones.Count; b++)
                    {
                        SvrSkeletalRig.BoneLink bl = rig.Bones[b];
                        if (!bl.A || !bl.B) continue;
                        DrawWorldEdge(cam, bl.A.position, bl.B.position, boneThick, white);
                    }

                    if (dist < 48f && rig.JointNodes.Count > 0)
                    {
                        GUI.color = jointCol;
                        float jSz = Mathf.Clamp(3.4f * scale, 2f, 5f);
                        for (int j = 0; j < rig.JointNodes.Count; j++)
                        {
                            Transform jt = rig.JointNodes[j];
                            if (!jt) continue;
                            Vector3 jsp = cam.WorldToScreenPoint(jt.position);
                            if (jsp.z > 0.3f)
                                GUI.DrawTexture(new Rect(jsp.x - jSz * 0.5f, Screen.height - jsp.y - jSz * 0.5f, jSz, jSz), white);
                        }
                    }
                }

                // B) Draw 3D anatomical limb wireframe cages (HurtBox capsules/spheres/boxes parented to moving bones)
                if (rig.Limbs.Count > 0 && dist < 58f)
                {
                    GUI.color = rig.Bones.Count > 0 ? cageCol : wireCol;
                    int lStep = dist > 32f ? 2 : 1;
                    for (int l = 0; l < rig.Limbs.Count; l += lStep)
                    {
                        SvrSkeletalRig.LimbVolume lv = rig.Limbs[l];
                        if (!lv.Tr) continue;
                        Vector3 ls = lv.Tr.lossyScale;
                        float maxS = Mathf.Max(0.1f, Mathf.Max(Mathf.Abs(ls.x), Mathf.Max(Mathf.Abs(ls.y), Mathf.Abs(ls.z))));
                        Vector3 center = lv.Tr.TransformPoint(lv.LocalCenter);
                        float r = Mathf.Clamp(lv.Radius * maxS, 0.08f, 3.2f);
                        float halfH = Mathf.Clamp(lv.Height * maxS * 0.5f, r, 4.5f);

                        Vector3 axis = lv.Axis == 0 ? lv.Tr.right : (lv.Axis == 2 ? lv.Tr.forward : lv.Tr.up);
                        Vector3 p1 = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized * r;
                        Vector3 p2 = Vector3.Cross(axis, p1.normalized).normalized * r;

                        Vector3 topC = center + axis * Mathf.Max(0f, halfH - r * 0.5f);
                        Vector3 botC = center - axis * Mathf.Max(0f, halfH - r * 0.5f);

                        // 4 longitudinal wireframe ribs along the moving limb + 2 end-cap rings
                        DrawWorldEdge(cam, botC + p1, topC + p1, cageThick, white);
                        DrawWorldEdge(cam, botC - p1, topC - p1, cageThick, white);
                        DrawWorldEdge(cam, botC + p2, topC + p2, cageThick, white);
                        DrawWorldEdge(cam, botC - p2, topC - p2, cageThick, white);

                        // Equatorial cross-ring around the limb
                        DrawWorldEdge(cam, center + p1, center + p2, cageThick, white);
                        DrawWorldEdge(cam, center + p2, center - p1, cageThick, white);
                        DrawWorldEdge(cam, center - p1, center - p2, cageThick, white);
                        DrawWorldEdge(cam, center - p2, center + p1, cageThick, white);
                    }
                }

                // C) Draw rigid weapon / shield / mechanical attachments parented to the character's animated hands/bones
                if (rig.Attachments.Count > 0 && dist < 45f)
                {
                    GUI.color = jointCol;
                    for (int a = 0; a < rig.Attachments.Count; a++)
                    {
                        SvrSkeletalRig.RigidAttachment att = rig.Attachments[a];
                        if (!att.Tr || att.EdgeA == null || att.EdgeB == null) continue;
                        int eLen = att.EdgeA.Length;
                        for (int e = 0; e < eLen; e++)
                            DrawWorldEdge(cam, att.Tr.TransformPoint(att.EdgeA[e]), att.Tr.TransformPoint(att.EdgeB[e]), cageThick, white);
                    }
                }

                // D) Draw live baked skinning vertices if available
                if (rig.BakedWorldVerts.Count > 1 && dist < 42f)
                {
                    GUI.color = jointCol;
                    float pSz = Mathf.Clamp(2.6f * scale, 1.8f, 4f);
                    for (int v = 0; v < rig.BakedWorldVerts.Count; v++)
                    {
                        Vector3 bsp = cam.WorldToScreenPoint(rig.BakedWorldVerts[v]);
                        if (bsp.z > 0.3f)
                            GUI.DrawTexture(new Rect(bsp.x - pSz * 0.5f, Screen.height - bsp.y - pSz * 0.5f, pSz, pSz), white);
                    }
                }
            }
        }

        private void DrawDemon(Camera cam, float scale, float dotAlpha)
        {
            if (dotAlpha <= 0.005f) return;
            Texture2D white = Texture2D.whiteTexture;

            // Draw the Ghost-Trap HUD marker and Proton/Poltergust capture streams when the trap is deployed
            if (_demon.TrapActive)
            {
                Vector3 tsp = cam.WorldToScreenPoint(_demon.TrapPos + Vector3.up * 0.35f);
                if (tsp.z > 0.35f)
                {
                    float tx = tsp.x, ty = Screen.height - tsp.y;
                    float rad = 11f * scale;
                    GUI.color = new Color(1f, 0.88f, 0.15f, 0.92f * dotAlpha);
                    DrawScreenLine(new Vector2(tx - rad, ty), new Vector2(tx, ty - rad), 2f * scale, white);
                    DrawScreenLine(new Vector2(tx, ty - rad), new Vector2(tx + rad, ty), 2f * scale, white);
                    DrawScreenLine(new Vector2(tx + rad, ty), new Vector2(tx, ty + rad), 2f * scale, white);
                    DrawScreenLine(new Vector2(tx, ty + rad), new Vector2(tx - rad, ty), 2f * scale, white);
                    if (_label != null)
                    {
                        string tTxt = _demon.TetherActive
                            ? "CONTAINING DEMON " + Mathf.RoundToInt(_demon.CaptureProgress * 100f) + "%"
                            : "GHOST TRAP [OPEN]";
                        GUI.color = _demon.TetherActive ? new Color(0.25f, 1f, 0.55f, 0.95f * dotAlpha) : new Color(1f, 0.9f, 0.2f, 0.9f * dotAlpha);
                        GUI.Label(new Rect(tx + rad + 6f * scale, ty - 10f * scale, 220f * scale, 20f * scale), tTxt, _label);
                    }
                }
            }

            // Draw Friendly Spectral Hologram Guardian HUD tag if active
            if (_demon.MinionActive)
            {
                Vector3 msp = cam.WorldToScreenPoint(_demon.MinionPos + Vector3.up * 1.15f);
                if (msp.z > 0.35f && _label != null)
                {
                    float mx = msp.x, my = Screen.height - msp.y;
                    float rem = Mathf.Max(0f, _demon.MinionUntil - Time.time);
                    GUI.color = new Color(0.28f, 1f, 0.82f, 0.92f * dotAlpha);
                    GUI.Label(new Rect(mx - 85f * scale, my - 12f * scale, 220f * scale, 20f * scale), "SPECTRAL GUARDIAN · " + rem.ToString("0") + "s", _label);
                }
            }

            bool hasLockedEnemy = _demon.LockedEnemy && _demon.LockedEnemy.alive && _demon.LockedEnemy.body;
            if (!_demon.Active && !hasLockedEnemy) return;
            bool revealed = Time.time < _revealUntil || _demon.TrapActive;
            bool blocked = !revealed && _demon.Active && Physics.Linecast(cam.transform.position, _demon.Center, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            float vis = (blocked ? 0.18f : 0.95f) * dotAlpha;
            if (_demon.Active) _demon.FillWorld(_demonPoints, Time.time);
            else _demonPoints.Clear();

            // Ghostbusters Proton Stream + Luigi's Mansion Poltergust Suction Vortex while capturing!
            if (_demon.TrapActive && (_demon.TetherActive || _demon.CaptureProgress > 0.02f))
            {
                Vector3 muzzleWorld = _body.corePosition + cam.transform.right * 0.48f - cam.transform.up * 0.18f + cam.transform.forward * 0.55f;
                Vector3 demonCenter = _demon.TetherTargetPos;
                Vector3 trapWorld = _demon.TrapPos + Vector3.up * 0.25f;

                // 1) Ghostbusters Proton Stream: crackling orange/red core beam + spiraling electric blue helix from gun to Demon
                Vector3 prevCore = Vector3.zero, prevHelix = Vector3.zero;
                bool hasPrevStream = false;
                const int streamSegs = 22;
                for (int s = 0; s <= streamSegs; s++)
                {
                    float u = s / (float)streamSegs;
                    float env = Mathf.Sin(u * 3.14159f);
                    Vector3 basePt = Vector3.Lerp(muzzleWorld, demonCenter, u);
                    Vector3 jitter = new Vector3(
                        Mathf.Sin(Time.time * 42f + s * 1.9f),
                        Mathf.Cos(Time.time * 37f + s * 2.3f),
                        Mathf.Sin(Time.time * 49f + s * 1.4f)) * (0.22f * env);
                    Vector3 coreWorld = basePt + jitter;

                    float hAng = Time.time * 28f - u * 18f;
                    Vector3 helixOffset = (cam.transform.right * Mathf.Cos(hAng) + cam.transform.up * Mathf.Sin(hAng)) * (0.34f * env);
                    Vector3 helixWorld = basePt + helixOffset;

                    Vector3 sc = cam.WorldToScreenPoint(coreWorld);
                    Vector3 sh = cam.WorldToScreenPoint(helixWorld);
                    if (sc.z > 0.3f && sh.z > 0.3f)
                    {
                        Vector2 c2 = new Vector2(sc.x, Screen.height - sc.y);
                        Vector2 h2 = new Vector2(sh.x, Screen.height - sh.y);
                        if (hasPrevStream)
                        {
                            GUI.color = new Color(1f, 0.32f, 0.08f, 0.95f * dotAlpha);
                            DrawScreenLine(prevCore, c2, 3.2f * scale, white);
                            GUI.color = new Color(1f, 0.92f, 0.55f, 0.9f * dotAlpha);
                            DrawScreenLine(prevCore, c2, 1.4f * scale, white);
                            GUI.color = new Color(0.2f, 0.82f, 1f, 0.92f * dotAlpha);
                            DrawScreenLine(prevHelix, h2, 2.2f * scale, white);
                        }
                        prevCore = c2;
                        prevHelix = h2;
                        hasPrevStream = true;
                    }
                    else hasPrevStream = false;
                }

                // 2) Luigi's Mansion Poltergust Suction Vortex: spiraling ectoplasm-green funnel pulling Demon into the Ghost-Trap
                Vector3 prevVortex = Vector3.zero;
                bool hasPrevVortex = false;
                const int vortexSegs = 26;
                for (int v = 0; v <= vortexSegs; v++)
                {
                    float u = v / (float)vortexSegs; // 0 = Demon, 1 = Trap
                    float funnelRad = Mathf.Lerp(1.15f, 0.12f, u);
                    float vAng = Time.time * 32f + u * 22f;
                    Vector3 vWorld = Vector3.Lerp(demonCenter, trapWorld, u)
                        + new Vector3(Mathf.Cos(vAng) * funnelRad, Mathf.Sin(u * 3.14159f) * 0.45f, Mathf.Sin(vAng) * funnelRad);
                    Vector3 sv = cam.WorldToScreenPoint(vWorld);
                    if (sv.z > 0.3f)
                    {
                        Vector2 v2 = new Vector2(sv.x, Screen.height - sv.y);
                        if (hasPrevVortex)
                        {
                            GUI.color = (v & 1) == 0
                                ? new Color(0.22f, 1f, 0.52f, 0.92f * dotAlpha)
                                : new Color(0.3f, 0.95f, 1f, 0.88f * dotAlpha);
                            DrawScreenLine(prevVortex, v2, 2.4f * scale, white);
                        }
                        prevVortex = v2;
                        hasPrevVortex = true;
                    }
                    else hasPrevVortex = false;
                }
            }

            // afterimages: where it was a moment ago, so its movement smears
            _trailT -= Time.deltaTime;
            if (_trailT <= 0f)
            {
                _trailT = 0.07f;
                _trailB.Clear(); _trailB.AddRange(_trailA);
                _trailA.Clear(); _trailA.AddRange(_demonPoints);
            }
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
                GUI.color = _demon.TetherActive ? new Color(0.35f, 1f, 0.65f, vis) : new Color(0.9f, 0.97f, 1f, vis);
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

        // Blown-out visor-off look: frosted static/glitch blind overlay, white wash, chromatic edge refraction, and smooth dark fade when closing.
        private void DrawPeekWash(float scale)
        {
            float now = Time.time;
            float k = _peekK;
            if (k < 0.01f) return;
            Texture2D white = Texture2D.whiteTexture;

            // While the visor is closing back down (!Peeking && k > 0), smoothly darken the screen to black
            // as the LiDAR dots smoothly fade back in, so there is never a hard cut.
            if (!Peeking && SvrPlugin.DarkWorld.Value)
            {
                float darkCurtain = Mathf.SmoothStep(0f, 0.95f, 1f - k);
                GUI.color = new Color(0f, 0f, 0f, darkCurtain);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);
            }

            float flick = 0.88f + 0.12f * Mathf.Sin(now * 23f) + (Random.value < 0.04f ? 0.25f : 0f);
            float wash = 0.26f * k * flick;
            GUI.color = new Color(0.95f, 0.98f, 1f, Mathf.Clamp01(wash));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);

            // Frosted helmet-glass vignette + chromatic edge refraction
            for (int i = 0; i < 7; i++)
            {
                float inset = i * 0.035f;
                float a = (0.16f - i * 0.02f) * k * flick;
                if (a <= 0f) continue;
                GUI.color = (i == 0) ? new Color(0.75f, 0.94f, 1f, a * 1.2f) : new Color(1f, 0.98f, 0.92f, a);
                float bx = Screen.width * inset, by = Screen.height * inset;
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, by + 6f), white);
                GUI.DrawTexture(new Rect(0f, Screen.height - by - 6f, Screen.width, by + 6f), white);
                GUI.DrawTexture(new Rect(0f, 0f, bx + 6f, Screen.height), white);
                GUI.DrawTexture(new Rect(Screen.width - bx - 6f, 0f, bx + 6f, Screen.height), white);
            }
            // Frosted static & horizontal scanline glitch bars
            for (int i = 0; i < 9; i++)
            {
                bool cyanTint = (i & 1) == 0;
                GUI.color = cyanTint
                    ? new Color(0.75f, 0.96f, 1f, Random.Range(0.08f, 0.28f) * k)
                    : new Color(1f, 0.92f, 0.88f, Random.Range(0.1f, 0.32f) * k);
                float bw = Random.Range(0.25f, 1f) * Screen.width;
                GUI.DrawTexture(new Rect(Random.value * (Screen.width - bw), Random.value * Screen.height, bw, Random.Range(1.5f, 14f) * scale), white);
            }
        }

        // Draws one of the four tactical LiDAR scanner mode icons (0=TOPOGRAPHY, 1=THERMAL, 2=SALVAGE, 3=STRUCTURAL)
        private static void DrawModeIcon(Vector2 center, float r, int mode, Color col, float scale, Texture2D white)
        {
            GUI.color = col;
            float t = Mathf.Max(1.5f, 2f * scale);
            // Map scan mode -> icon glyph: TOPOGRAPHY = flood wedge, THERMAL = sonar chevrons, SALVAGE = omni diamond, STRUCTURAL = radar rotor
            int glyph;
            switch (mode % 4) { case 1: glyph = 4; break; case 2: glyph = 2; break; case 3: glyph = 1; break; default: glyph = 0; break; }
            switch (glyph)
            {
                case 0: // CONE: wide 65-deg flood wedge + base dot
                    DrawScreenLine(center + new Vector2(0f, r * 0.7f), center + new Vector2(-r * 0.85f, -r * 0.65f), t, white);
                    DrawScreenLine(center + new Vector2(0f, r * 0.7f), center + new Vector2(r * 0.85f, -r * 0.65f), t, white);
                    DrawScreenLine(center + new Vector2(-r * 0.85f, -r * 0.65f), center + new Vector2(0f, -r * 0.9f), t, white);
                    DrawScreenLine(center + new Vector2(0f, -r * 0.9f), center + new Vector2(r * 0.85f, -r * 0.65f), t, white);
                    break;
                case 1: // SWEEP: twin opposing radar rotor blades + ring
                    DrawScreenLine(center + new Vector2(-r * 0.85f, -r * 0.85f), center + new Vector2(r * 0.85f, r * 0.85f), t, white);
                    DrawScreenLine(center + new Vector2(-r * 0.85f, r * 0.85f), center + new Vector2(r * 0.85f, -r * 0.85f), t * 0.75f, white);
                    GUI.DrawTexture(new Rect(center.x - 2.5f * scale, center.y - 2.5f * scale, 5f * scale, 5f * scale), white);
                    break;
                case 2: // WIDE: 360 omni-dome diamond + inner core
                    DrawScreenLine(center + new Vector2(0f, -r), center + new Vector2(r, 0f), t, white);
                    DrawScreenLine(center + new Vector2(r, 0f), center + new Vector2(0f, r), t, white);
                    DrawScreenLine(center + new Vector2(0f, r), center + new Vector2(-r, 0f), t, white);
                    DrawScreenLine(center + new Vector2(-r, 0f), center + new Vector2(0f, -r), t, white);
                    GUI.DrawTexture(new Rect(center.x - 3f * scale, center.y - 3f * scale, 6f * scale, 6f * scale), white);
                    break;
                case 3: // FOCUS: sniper crosshair + tight vertical beam
                    DrawScreenLine(center + new Vector2(0f, -r), center + new Vector2(0f, r), t, white);
                    DrawScreenLine(center + new Vector2(-r, 0f), center + new Vector2(r, 0f), t, white);
                    GUI.DrawTexture(new Rect(center.x - r * 0.45f, center.y - r * 0.45f, r * 0.9f, r * 0.9f), white);
                    break;
                default: // ECHO: seismic concentric sonar chevrons
                    for (int w = 0; w < 3; w++)
                    {
                        float rw = r * (0.4f + w * 0.3f);
                        DrawScreenLine(center + new Vector2(-rw, rw * 0.3f), center + new Vector2(0f, -rw * 0.7f), t, white);
                        DrawScreenLine(center + new Vector2(0f, -rw * 0.7f), center + new Vector2(rw, rw * 0.3f), t, white);
                    }
                    break;
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

            // Sensor status strip (the four senses) + Containment Cell Spirit Charge counter
            float x = Screen.width * 0.03f, y = Screen.height * 0.745f;
            DrawSense("SEE", SvrSenses.Sight, ref x, y, scale);
            DrawSense("HEAR", SvrSenses.Hearing, ref x, y, scale);
            DrawSense("TOUCH", SvrSenses.Touch, ref x, y, scale);
            DrawSense("BAL", SvrSenses.Balance, ref x, y, scale);
            if (_demon.StoredSpirits > 0 || _demon.MinionActive)
            {
                string cellTxt = _demon.MinionActive
                    ? "GUARDIAN ACTIVE (" + Mathf.CeilToInt(_demon.MinionUntil - Time.time) + "s)"
                    : "CELL: " + _demon.StoredSpirits + "/" + _demon.MaxStoredSpirits + " [H] GUARDIAN · [B] CASH-IN";
                float cw = (cellTxt.Length * 8.2f + 16f) * scale;
                GUI.color = new Color(0.02f, 0.06f, 0.08f, 0.75f);
                GUI.DrawTexture(new Rect(x, y, cw, 24f * scale), white);
                GUI.color = new Color(0.28f, 1f, 0.85f, 0.96f);
                GUI.DrawTexture(new Rect(x, y, 3f * scale, 24f * scale), white);
                GUI.Label(new Rect(x + 7f * scale, y, cw, 24f * scale), cellTxt, _label);
            }

            // Segmented Industrial Minigun Heat & Spool Gauge
            SvrSpool sp = _body.GetComponent<SvrSpool>();
            if (sp && (sp.Spool > 0.01f || sp.Heat > 0.02f || sp.AnchorLeft > 0f))
            {
                float w = 210f * scale, h = 10f * scale;
                float bx = Screen.width * 0.5f - w * 0.5f, by = Screen.height * 0.64f;
                GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.72f);
                GUI.DrawTexture(new Rect(bx - 4f * scale, by - 4f * scale, w + 8f * scale, h + 28f * scale), white);

                const int segs = 12;
                float segGap = 2.5f * scale;
                float segW = (w - (segs - 1) * segGap) / segs;
                int litSpool = Mathf.RoundToInt(sp.Spool * segs);
                for (int sIdx = 0; sIdx < segs; sIdx++)
                {
                    Rect sr = new Rect(bx + sIdx * (segW + segGap), by, segW, h);
                    bool lit = sIdx < litSpool;
                    Color segCol = sp.Overheated
                        ? new Color(1f, 0.25f, 0.18f, 0.95f)
                        : (sp.AnchorLeft > 0f
                            ? new Color(1f, 0.86f, 0.2f, 0.95f)
                            : Color.Lerp(new Color(0.45f, 1f, 0.35f, 0.95f), new Color(1f, 0.52f, 0.15f, 0.95f), sp.Heat));
                    GUI.color = lit ? segCol : new Color(0.15f, 0.2f, 0.24f, 0.65f);
                    GUI.DrawTexture(sr, white);
                }

                // Thin thermal load bar right below the segmented spool gauge
                GUI.color = SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) ? new Color(0.35f, 0.95f, 1f, 0.9f) : new Color(1f, 0.38f, 0.12f, 0.9f);
                GUI.DrawTexture(new Rect(bx, by + h + 2f * scale, w * Mathf.Clamp01(sp.Heat), 2.5f * scale), white);

                string spoolTag = sp.Overheated ? "THERMAL OVERLOAD!" : (sp.AnchorLeft > 0f ? "SPOOL · ANCHORED" : (SvrUpgrades.Has(SvrUpgrades.CRYO_JACKET) ? "SPOOL · CRYO-COOLED" : "SPOOL · HEAT " + Mathf.RoundToInt(sp.Heat * 100f) + "%"));
                GUI.color = sp.Overheated ? new Color(1f, 0.35f, 0.25f, 0.98f) : new Color(0.82f, 1f, 0.75f, 0.92f);
                GUI.Label(new Rect(bx, by + h + 4f * scale, w + 40f * scale, 20f * scale), spoolTag, _label);
            }

            // Installed LiDAR-Exclusive Hidden Tech Upgrade badges
            if (SvrUpgrades.UnlockedCount > 0)
            {
                float ux = Screen.width * 0.03f, uy = Screen.height * 0.782f;
                for (int u = 0; u < SvrUpgrades.COUNT; u++)
                {
                    if (!SvrUpgrades.Has(u)) continue;
                    string uName = SvrUpgrades.ShortNames[u];
                    float uw = (uName.Length * 7.8f + 12f) * scale;
                    GUI.color = new Color(0.03f, 0.06f, 0.09f, 0.75f);
                    GUI.DrawTexture(new Rect(ux, uy, uw, 18f * scale), white);
                    Color uCol = u < 4 ? new Color(1f, 0.84f, 0.25f, 0.95f) : new Color(0.3f, 0.96f, 1f, 0.95f);
                    GUI.color = uCol;
                    GUI.DrawTexture(new Rect(ux, uy, 2.5f * scale, 18f * scale), white);
                    GUI.Label(new Rect(ux + 5f * scale, uy - 2f * scale, uw, 20f * scale), uName, _label);
                    ux += uw + 5f * scale;
                }
            }

            // Overdrive mode readout (only when the skill is equipped) and the visor timer.
            SkillLocator skl = _body.skillLocator;
            bool hasOd = skl && skl.special && skl.special.skillDef && skl.special.skillDef.skillName == SvrPlugin.T + "OVERDRIVE";
            {
                // scan mode readout (always): tactical mode icon, name, subtitle, four pips, the Q key; flashes when changed
                int mIdx = OdMode % OdNames.Length;
                float sinceSwitch = Time.time - _modeFlash;
                bool flash = sinceSwitch < 0.75f;
                float sx = Screen.width * 0.03f, sy = Screen.height * 0.692f;
                Color accent = ModeAccent(mIdx);
                Color modeCol = flash ? new Color(1f, 1f, 1f, 1f) : (OverdriveOn ? new Color(1f, 0.62f, 0.2f, 0.95f) : accent);
                DrawModeIcon(new Vector2(sx + 10f * scale, sy + 12f * scale), 9f * scale, mIdx, modeCol, scale, white);
                GUI.color = modeCol;
                string keys = "[" + SvrPlugin.ScanNextKey.Value.ToUpperInvariant() + " = NEXT MODE]";
                GUI.Label(new Rect(sx + 26f * scale, sy, 520f * scale, 24f * scale), "SCAN: " + OdNames[mIdx] + "  " + keys + (hasOd ? "   R = OVERDRIVE" : ""), _label);
                GUI.color = new Color(0.78f, 0.93f, 1f, 0.85f);
                GUI.Label(new Rect(sx + 26f * scale, sy + 18f * scale, 520f * scale, 22f * scale), OdDescs[mIdx], _label);
                for (int i = 0; i < OdNames.Length; i++)
                {
                    Color pc = ModeAccent(i);
                    GUI.color = i == mIdx ? new Color(pc.r, pc.g, pc.b, 0.95f) : new Color(pc.r, pc.g, pc.b, 0.22f);
                    GUI.DrawTexture(new Rect(sx + i * 18f * scale, sy - 8f * scale, 14f * scale, 5f * scale), white);
                }
                float pr = _peekReady - Time.time;
                GUI.color = new Color(0.9f, 0.95f, 1f, 0.85f);
                GUI.Label(new Rect(sx, sy + 36f * scale, 160f * scale, 24f * scale), "PEEK [" + SvrPlugin.PeekKey.Value.ToUpperInvariant() + "]" + (pr > 0f ? "  " + pr.ToString("0") + "s" : "  ready"), _label);

                float rr = _repulsorReadyAt - Time.time;
                GUI.color = new Color(0.45f, 0.96f, 1f, 0.88f);
                GUI.Label(new Rect(sx + 135f * scale, sy + 36f * scale, 165f * scale, 24f * scale), "BLOW [" + SvrPlugin.RepulsorKey.Value.ToUpperInvariant() + "]" + (rr > 0f ? "  " + rr.ToString("0") + "s" : "  ready"), _label);

                float tr = _demon.TrapReadyAt - Time.time;
                string trapKeyStr = SvrPlugin.TrapKey.Value.ToUpperInvariant();
                if (_demon.TrapActive)
                {
                    GUI.color = new Color(0.25f, 1f, 0.55f, 0.95f);
                    GUI.Label(new Rect(sx + 275f * scale, sy + 36f * scale, 380f * scale, 24f * scale), "TRAP [" + trapKeyStr + "]  OPEN — AIM & FIRE + WASD STEER!", _label);
                }
                else
                {
                    GUI.color = SvrSenses.Lost > 0 ? new Color(1f, 0.88f, 0.25f, 0.95f) : new Color(0.82f, 0.95f, 1f, 0.82f);
                    string trapSub = tr > 0f ? ("  " + tr.ToString("0") + "s") : (SvrSenses.Lost > 0 ? "  READY (CAPTURE DEMON -> RESTORE SENSE)" : "  ready");
                    GUI.Label(new Rect(sx + 275f * scale, sy + 36f * scale, 420f * scale, 24f * scale), "TRAP [" + trapKeyStr + "]" + trapSub, _label);
                }

                // Top-center banner when tapping Q switches scanner mode
                if (sinceSwitch < 1.35f)
                {
                    float bAlpha = Mathf.Clamp01(1.35f - sinceSwitch);
                    float bw = 480f * scale, bh = 46f * scale;
                    float bx = Screen.width * 0.5f - bw * 0.5f, by = Screen.height * 0.16f;
                    GUI.color = new Color(0f, 0f, 0f, 0.75f * bAlpha);
                    GUI.DrawTexture(new Rect(bx, by, bw, bh), white);
                    GUI.color = new Color(accent.r, accent.g, accent.b, 0.96f * bAlpha);
                    GUI.DrawTexture(new Rect(bx, by, bw, 2.5f * scale), white);
                    DrawModeIcon(new Vector2(bx + 20f * scale, by + 23f * scale), 11f * scale, mIdx, GUI.color, scale, white);
                    GUI.Label(new Rect(bx + 38f * scale, by + 4f * scale, bw - 44f * scale, 22f * scale), "SCANNER MODE: " + OdNames[mIdx] + "  (" + (mIdx + 1) + "/" + OdNames.Length + ")", _label);
                    GUI.color = new Color(0.82f, 0.95f, 1f, 0.9f * bAlpha);
                    GUI.Label(new Rect(bx + 38f * scale, by + 22f * scale, bw - 44f * scale, 22f * scale), OdDescs[mIdx], _label);
                }

                // Prompt when standing next to a LiDAR-Exclusive Hidden Tech Cache
                if (_nearCacheIdx >= 0 && _nearCacheIdx < _techCaches.Count && !_techCaches[_nearCacheIdx].Collected)
                {
                    int upgId = _techCaches[_nearCacheIdx].UpgradeId;
                    float pw = 540f * scale, ph = 46f * scale;
                    float px = Screen.width * 0.5f - pw * 0.5f, py = Screen.height * 0.56f;
                    GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.88f);
                    GUI.DrawTexture(new Rect(px, py, pw, ph), white);
                    GUI.color = new Color(1f, 0.86f, 0.2f, 0.98f);
                    GUI.DrawTexture(new Rect(px, py, pw, 2.5f * scale), white);
                    GUI.Label(new Rect(px + 14f * scale, py + 4f * scale, pw - 24f * scale, 22f * scale), "PRESS [E] TO INSTALL HIDDEN LIDAR TECH: " + SvrUpgrades.Names[upgId], _label);
                    GUI.color = new Color(0.35f, 0.98f, 1f, 0.92f);
                    GUI.Label(new Rect(px + 14f * scale, py + 23f * scale, pw - 24f * scale, 20f * scale), SvrUpgrades.Descs[upgId], _label);
                }
                else if (Time.time < _upgradeToastUntil)
                {
                    float uAlpha = Mathf.Clamp01(_upgradeToastUntil - Time.time);
                    float pw = 560f * scale, ph = 36f * scale;
                    float px = Screen.width * 0.5f - pw * 0.5f, py = Screen.height * 0.28f;
                    GUI.color = new Color(0.02f, 0.05f, 0.08f, 0.85f * uAlpha);
                    GUI.DrawTexture(new Rect(px, py, pw, ph), white);
                    GUI.color = new Color(1f, 0.86f, 0.2f, 0.98f * uAlpha);
                    GUI.DrawTexture(new Rect(px, py, pw, 2.5f * scale), white);
                    GUI.Label(new Rect(px + 12f * scale, py + 7f * scale, pw - 20f * scale, 22f * scale), _upgradeToast, _label);
                }

                // Ghostbusters / Luigi's Mansion Containment & Wrestling Match HUD bar while Ghost-Trap is active
                if (_demon.TrapActive)
                {
                    float cw = 520f * scale, ch = 66f * scale;
                    float cx = Screen.width * 0.5f - cw * 0.5f, cy = Screen.height * 0.22f;
                    GUI.color = new Color(0.02f, 0.04f, 0.06f, 0.86f);
                    GUI.DrawTexture(new Rect(cx, cy, cw, ch), white);
                    Color topBarCol = _demon.CounterSteering
                        ? new Color(0.25f, 1f, 0.55f, 1f)
                        : (_demon.TetherActive ? new Color(0.3f, 0.95f, 1f, 0.98f) : new Color(1f, 0.86f, 0.15f, 0.95f));
                    GUI.color = topBarCol;
                    GUI.DrawTexture(new Rect(cx, cy, cw, 2.5f * scale), white);

                    string strainTxt = _demon.StrainStacks > 0 ? (" · STRAIN x" + _demon.StrainStacks) : "";
                    string hdr = _demon.TetherActive
                        ? "PROTON TETHER LOCKED (" + Mathf.RoundToInt(_demon.CaptureProgress * 100f) + "%)" + strainTxt
                        : "GHOST TRAP OPEN — AIM AT DEMON/BOSS & HOLD FIRE TO CAPTURE!";
                    GUI.Label(new Rect(cx + 12f * scale, cy + 4f * scale, cw - 24f * scale, 20f * scale), hdr, _label);

                    if (_demon.TetherActive)
                    {
                        string steerPrompt = _demon.PullDir < 0
                            ? "<<< TARGET PULLING LEFT — COUNTER-STEER [D]!"
                            : (_demon.PullDir > 0
                                ? "TARGET PULLING RIGHT — COUNTER-STEER [A]! >>>"
                                : "^^^ TARGET SURGING BACK — PULL BACK [S]! ^^^");
                        if (_demon.CounterSteering) steerPrompt = "WRESTLING BONUS 2.2x! REELING TARGET IN FAST!";
                        GUI.color = _demon.CounterSteering ? new Color(0.25f, 1f, 0.55f, 1f) : new Color(1f, 0.78f, 0.25f, 0.95f);
                        GUI.Label(new Rect(cx + 12f * scale, cy + 23f * scale, cw - 24f * scale, 20f * scale), steerPrompt, _label);
                    }

                    float barW = cw - 24f * scale, barH = 12f * scale;
                    float barX = cx + 12f * scale, barY = cy + 46f * scale;
                    GUI.color = new Color(0.12f, 0.16f, 0.2f, 0.9f);
                    GUI.DrawTexture(new Rect(barX, barY, barW, barH), white);

                    // Gold Active-Reload Compression Slam Zone [64% .. 89%]
                    GUI.color = new Color(1f, 0.86f, 0.18f, 0.45f);
                    GUI.DrawTexture(new Rect(barX + barW * 0.64f, barY - 2f * scale, barW * 0.25f, barH + 4f * scale), white);

                    GUI.color = _demon.CounterSteering ? new Color(0.25f, 1f, 0.55f, 0.98f) : (_demon.TetherActive ? new Color(0.3f, 0.92f, 1f, 0.95f) : new Color(1f, 0.45f, 0.12f, 0.85f));
                    GUI.DrawTexture(new Rect(barX, barY, barW * Mathf.Clamp01(_demon.CaptureProgress), barH), white);

                    if (_demon.CaptureProgress >= 0.64f && _demon.CaptureProgress <= 0.89f)
                    {
                        GUI.color = new Color(1f, 0.95f, 0.35f, 1f);
                        GUI.Label(new Rect(barX + barW * 0.62f, barY - 18f * scale, 180f * scale, 18f * scale), "[G] SLAM!", _label);
                    }
                }
                else if (Time.time < _demon.LastRestoredUntil)
                {
                    float rAlpha = Mathf.Clamp01(_demon.LastRestoredUntil - Time.time);
                    float cw = 520f * scale, ch = 44f * scale;
                    float cx = Screen.width * 0.5f - cw * 0.5f, cy = Screen.height * 0.22f;
                    GUI.color = new Color(0.02f, 0.06f, 0.04f, 0.82f * rAlpha);
                    GUI.DrawTexture(new Rect(cx, cy, cw, ch), white);
                    GUI.color = new Color(0.25f, 1f, 0.55f, 0.98f * rAlpha);
                    GUI.DrawTexture(new Rect(cx, cy, cw, 3f * scale), white);
                    string msg = !string.IsNullOrEmpty(_demon.LastRestoredSense)
                        ? ("GHOST CONTAINED — " + _demon.LastRestoredSense)
                        : "GHOST CONTAINED — ALL SENSES INTACT!";
                    GUI.Label(new Rect(cx + 14f * scale, cy + 10f * scale, cw - 24f * scale, 24f * scale), msg, _label);
                }
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

            // Suit interference near the demon: subtle crimson/cyan vector glitch ticks on peripheral HUD (no solid/gray boxes).
            float prox = _demon.Proximity(_body.corePosition);
            if (prox <= 0.1f) return;
            GUI.color = new Color(1f, 0.35f, 0.3f, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            if (prox > 0.25f) GUI.Label(new Rect(Screen.width * 0.40f, Screen.height * 0.07f, Screen.width * 0.2f, 24f * scale), "SIGNAL INTERFERENCE", _label);
            for (int r = 0; r < Regions.Length; r++)
            {
                if (r == 2) continue; // never draw interference boxes over the center crosshair
                float chance = Mathf.InverseLerp(RegionFrom[r], 1f, prox);
                if (chance <= 0f || Random.value > chance * 0.65f) continue;
                Rect fr = Regions[r];
                Rect box = new Rect(fr.x * Screen.width, fr.y * Screen.height, fr.width * Screen.width, fr.height * Screen.height);
                int bars = 4 + Mathf.RoundToInt(chance * 6f);
                for (int i = 0; i < bars; i++)
                {
                    GUI.color = (i & 1) == 0
                        ? new Color(1f, 0.22f, 0.2f, Random.Range(0.25f, 0.65f))
                        : new Color(0.2f, 0.88f, 1f, Random.Range(0.2f, 0.55f));
                    float bw = Random.Range(0.05f, 0.28f) * box.width, bh = Random.Range(1f, 2.5f) * scale;
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
