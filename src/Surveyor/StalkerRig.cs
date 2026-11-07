using System.Collections.Generic;
using System.Reflection;
using EntityStates;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace Surveyor
{
    /// <summary>
    /// The Shimmer Stalker's own skeleton (v0.7.2). The Blender model rides a rig of its own bones, animated in code on
    /// every client: a stalking walk, twitching head that tracks its prey, breathing and claw flexes, an all-fours crawl
    /// once it is enraged, and full-body attacks (overhead claw slam, spike hurl, pounce, shriek). Its hitboxes are capsules
    /// on those bones, so they match what you see. The Imp Overlord underneath only does the walking and thinking.
    /// </summary>
    public class SvrStalkerRig : MonoBehaviour
    {
        public enum Act { None, Slam, Throw, Pounce, Shriek, Spawn, Death }

        private CharacterBody _body;
        private Transform _root;
        public Transform Head { get; private set; }
        private readonly Dictionary<string, Transform> _j = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> _rest = new Dictionary<string, Vector3>();
        private float _s = 1f;
        private Vector3 _lastPos, _vel;
        private float _phase, _crawlK, _t, _lastStepPhase;
        private Act _act;
        private float _actT, _actDur, _actW, _lastEvent;
        private float _jaw, _jawTarget, _nextSnap, _lastLand = -10f;
        private Vector3 _twitch, _twitchTarget;
        private float _nextTwitch, _nextStateScan;
        private bool _built;
        private EntityStateMachine[] _esms;

        private static readonly string[] Order =
        {
            "hips", "spine", "chest", "neck", "head", "jaw",
            "upper_arm.R", "forearm.R", "hand.R", "upper_arm.L", "forearm.L", "hand.L",
            "thigh.R", "shin.R", "foot.R", "thigh.L", "shin.L", "foot.L"
        };
        private static readonly Dictionary<string, string> Parent = new Dictionary<string, string>
        {
            { "hips", null }, { "spine", "hips" }, { "chest", "spine" }, { "neck", "chest" }, { "head", "neck" }, { "jaw", "head" },
            { "upper_arm.R", "chest" }, { "forearm.R", "upper_arm.R" }, { "hand.R", "forearm.R" },
            { "upper_arm.L", "chest" }, { "forearm.L", "upper_arm.L" }, { "hand.L", "forearm.L" },
            { "thigh.R", "hips" }, { "shin.R", "thigh.R" }, { "foot.R", "shin.R" },
            { "thigh.L", "hips" }, { "shin.L", "thigh.L" }, { "foot.L", "shin.L" },
        };

        // ---------------------------------------------------------------- build

        public static SvrStalkerRig Build(GameObject bodyGo, Transform model)
        {
            if (!SvrStalkerModel.Get(out var bones, out var meshes, out Material mat, out float ourHeight)) return null;
            SvrStalkerRig rig = bodyGo.AddComponent<SvrStalkerRig>();
            try { rig.Setup(model, bones, meshes, mat, ourHeight); }
            catch (System.Exception e) { SvrPlugin.Log.LogError("Stalker rig: " + e); Destroy(rig); return null; }
            return rig;
        }

        private void Setup(Transform model, Dictionary<string, Vector3[]> bones, List<KeyValuePair<string, Mesh>> meshes, Material mat, float ourHeight)
        {
            _body = GetComponent<CharacterBody>();
            Renderer[] originals = model.GetComponentsInChildren<Renderer>(true);

            // as tall as the Imp Overlord, times the config size
            Bounds b = new Bounds(model.position, Vector3.zero);
            bool any = false;
            foreach (Renderer r in originals)
                if (r is SkinnedMeshRenderer || r is MeshRenderer) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float h = any ? Mathf.Clamp(b.size.y, 2f, 20f) : 6f;
            _s = h / ourHeight * SvrPlugin.StalkerScale.Value;

            int layer = model.gameObject.layer;
            Transform holder = model.parent ? model.parent : bodyGo.transform;
            _root = new GameObject("StalkerRig") { layer = layer }.transform;
            _root.SetParent(holder, false);
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one * _s;
            _root.position = _body.footPosition;

            Dictionary<string, Vector3> head = new Dictionary<string, Vector3>();
            foreach (string n in Order)
            {
                Vector3 hp;
                if (n == "jaw") hp = bones.TryGetValue("head@jaw", out var jb) ? jb[0] : (bones.TryGetValue("head", out var hb2) ? hb2[0] : Vector3.up * 2.6f);
                else hp = bones.TryGetValue(n, out var bb) ? bb[0] : Vector3.zero;
                head[n] = hp;
                Transform parent = Parent[n] == null ? _root : _j[Parent[n]];
                Vector3 parentHead = Parent[n] == null ? Vector3.zero : head[Parent[n]];
                Transform t = new GameObject("StalkerBone_" + n) { layer = layer }.transform;
                t.SetParent(parent, false);
                t.localPosition = hp - parentHead;
                _j[n] = t;
                _rest[n] = t.localPosition;
            }
            Head = _j["head"];

            foreach (var kv in meshes)
            {
                Transform parent = _j.TryGetValue(kv.Key, out Transform jt) ? jt : _root;
                GameObject go = new GameObject("StalkerMesh_" + kv.Key) { layer = layer };
                go.transform.SetParent(parent, false);
                if (!_j.ContainsKey(kv.Key)) go.transform.localPosition = Vector3.zero;
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
            }

            foreach (Renderer r in originals) if (r) r.forceRenderingOff = true;
            try
            {
                CharacterModel cm = model.GetComponent<CharacterModel>();
                if (cm)
                {
                    HashSet<Renderer> orig = new HashSet<Renderer>(originals);
                    var list = new List<CharacterModel.RendererInfo>();
                    if (cm.baseRendererInfos != null)
                        foreach (var ri in cm.baseRendererInfos) if (ri.renderer && !orig.Contains(ri.renderer)) list.Add(ri);
                    foreach (MeshRenderer mr in _root.GetComponentsInChildren<MeshRenderer>(true))
                        list.Add(new CharacterModel.RendererInfo { renderer = mr, defaultMaterial = mr.sharedMaterial, defaultShadowCastingMode = ShadowCastingMode.On, ignoreOverlays = false });
                    cm.baseRendererInfos = list.ToArray();
                }
            }
            catch { }

            try { BuildHurtBoxes(model, bones, head); }
            catch (System.Exception e) { SvrPlugin.Log.LogWarning("Stalker hitboxes kept the Imp Overlord's: " + e.Message); }

            _lastPos = _body.footPosition;
            _esms = GetComponents<EntityStateMachine>();
            _built = true;
            SvrPlugin.Log.LogInfo($"Shimmer Stalker rig built: {meshes.Count} parts, size x{_s:0.##}.");
            Trigger(Act.Spawn, 2.2f);
        }

        // ---------------------------------------------------------------- hitboxes on the rig's own bones

        private void BuildHurtBoxes(Transform model, Dictionary<string, Vector3[]> bones, Dictionary<string, Vector3> head)
        {
            HurtBoxGroup group = model.GetComponent<HurtBoxGroup>();
            HealthComponent hc = _body.healthComponent;
            if (!group || !hc) return;
            // (bone, from, to, radius) in our model's units
            var caps = new List<(string bone, Vector3 a, Vector3 b, float r, bool main, bool weak)>();
            void Bone(string n, float r, bool main = false) { if (bones.TryGetValue(n, out var bb)) caps.Add((n, bb[0], NextHead(n, bones, bb[1]), r, main, false)); }
            Bone("hips", 0.24f);
            Bone("spine", 0.2f);
            Bone("chest", 0.34f, true);
            Bone("neck", 0.12f);
            if (bones.TryGetValue("head", out var hb))
            {
                Vector3 hc0 = new Vector3(0f, hb[0].y + 0.1f, hb[0].z + 0.22f);
                caps.Add(("head", hc0 - new Vector3(0f, 0.02f, 0.3f), hc0 + new Vector3(0f, -0.05f, 0.34f), 0.17f, false, true));   // weak point
            }
            foreach (string sd in new[] { ".R", ".L" })
            {
                Bone("upper_arm" + sd, 0.11f);
                Bone("forearm" + sd, 0.09f);
                if (bones.TryGetValue("hand" + sd, out var hd)) caps.Add(("hand" + sd, hd[0], hd[0] + (hd[1] - hd[0]).normalized * 0.7f, 0.12f, false, false));
                Bone("thigh" + sd, 0.14f);
                Bone("shin" + sd, 0.1f);
            }

            List<HurtBox> made = new List<HurtBox>();
            HurtBox main = null;
            int layer = LayerIndex.entityPrecise.intVal;
            foreach (var c in caps)
            {
                Transform bone = _j[c.bone];
                GameObject go = new GameObject("StalkerHurtBox_" + c.bone) { layer = layer };
                go.SetActive(false);
                go.transform.SetParent(bone, false);
                Vector3 from = c.a - head[c.bone], to = c.b - head[c.bone];
                Vector3 d = to - from;
                go.transform.localPosition = (from + to) * 0.5f;
                go.transform.localRotation = d.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(d.normalized, Vector3.up) : Quaternion.identity;
                CapsuleCollider col = go.AddComponent<CapsuleCollider>();
                col.direction = 2;
                col.radius = c.r;
                col.height = d.magnitude + c.r * 2f;
                col.isTrigger = false;
                HurtBox hbx = go.AddComponent<HurtBox>();
                hbx.healthComponent = hc;
                hbx.hurtBoxGroup = group;
                hbx.damageModifier = c.weak ? HurtBox.DamageModifier.SniperTarget : HurtBox.DamageModifier.Normal;
                hbx.isBullseye = c.main;
                hbx.isSniperTarget = c.weak;
                hbx.indexInGroup = (short)made.Count;
                try { typeof(HurtBox).GetProperty("collider", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetSetMethod(true)?.Invoke(hbx, new object[] { col }); } catch { }
                try { SetTeam(hbx); } catch { }
                go.SetActive(true);
                made.Add(hbx);
                if (c.main) main = hbx;
            }
            if (made.Count == 0) return;
            // retire the Imp Overlord's (now invisible) hitboxes
            if (group.hurtBoxes != null)
                foreach (HurtBox old in group.hurtBoxes)
                    if (old && !made.Contains(old)) { Collider oc = old.GetComponent<Collider>(); if (oc) oc.enabled = false; old.enabled = false; }
            group.hurtBoxes = made.ToArray();
            group.mainHurtBox = main ? main : made[0];
            group.bullseyeCount = 1;
        }

        private static void SetTeam(HurtBox hb)
        {
            FieldInfo f = typeof(HurtBox).GetField("teamIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { f.SetValue(hb, TeamIndex.Monster); return; }
            PropertyInfo p = typeof(HurtBox).GetProperty("teamIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            p?.GetSetMethod(true)?.Invoke(hb, new object[] { TeamIndex.Monster });
        }

        private static Vector3 NextHead(string n, Dictionary<string, Vector3[]> bones, Vector3 tail)
        {
            string child = n switch
            {
                "hips" => "spine", "spine" => "chest", "chest" => "neck", "neck" => "head",
                "upper_arm.R" => "forearm.R", "forearm.R" => "hand.R", "upper_arm.L" => "forearm.L", "forearm.L" => "hand.L",
                "thigh.R" => "shin.R", "shin.R" => "foot.R", "thigh.L" => "shin.L", "shin.L" => "foot.L",
                _ => null
            };
            return child != null && bones.TryGetValue(child, out var cb) ? cb[0] : tail;
        }

        // ---------------------------------------------------------------- actions

        public void Trigger(Act a, float duration)
        {
            if (a == _act && _actT < 0.3f) return;           // the same event can arrive twice on the host
            _act = a;
            _actT = 0f;
            _actDur = Mathf.Max(0.3f, duration);
            _lastEvent = Time.time;
            switch (a)
            {
                case Act.Shriek:
                case Act.Spawn:
                    _jawTarget = 80f;
                    Util.PlaySound("Play_imp_overlord_spawn", gameObject);
                    Util.PlaySound("Play_voidRaid_fog_affectPlayer", gameObject);
                    break;
                case Act.Slam: _jawTarget = 55f; Util.PlaySound("Play_imp_overlord_attack2_tell", gameObject); break;
                case Act.Throw: _jawTarget = 40f; break;
                case Act.Pounce: _jawTarget = 70f; Util.PlaySound("Play_moonBrother_phaseJump_jumpAway", gameObject); Util.PlaySound("Play_imp_overlord_idle_VO", gameObject); break;
                case Act.Death: _jawTarget = 85f; break;
            }
        }

        public void OnEvent(byte kind)
        {
            switch (kind)
            {
                case 1: Trigger(Act.Pounce, 1.2f); break;
                case 2:
                    if (Time.time - _lastLand < 0.4f) break;
                    _lastLand = Time.time;
                    Util.PlaySound("Play_moonBrother_phaseJump_land_impact", gameObject);
                    if (_act == Act.Pounce) _actT = _actDur;
                    ShakeNear(1.2f, 24f);
                    break;
                case 3: Trigger(Act.Shriek, 1.8f); ShakeNear(1.6f, 40f); break;
            }
        }

        private void ShakeNear(float amp, float range)
        {
            try
            {
                LocalUser lu = LocalUserManager.GetFirstLocalUser();
                if (lu == null || !lu.cachedBody) return;
                float d = Vector3.Distance(lu.cachedBody.corePosition, _body.corePosition);
                if (d > range) return;
                ShakeEmitter.CreateSimpleShakeEmitter(_body.corePosition, new Wave { amplitude = amp * (1f - d / range) * 3f, frequency = 18f }, 0.6f, range, true);
            }
            catch { }
        }

        private void ScanStates()
        {
            if (_esms == null) return;
            foreach (EntityStateMachine m in _esms)
            {
                if (!m || m.state == null) continue;
                string n = m.state.GetType().Name;
                if (n.Contains("GroundPound") || n.Contains("Slam"))
                {
                    if (_act != Act.Slam) Trigger(Act.Slam, Duration(m.state, 1.6f));
                }
                else if (n.Contains("Spike") || n.Contains("Throw") || n.Contains("Fire"))
                {
                    if (_act != Act.Throw) Trigger(Act.Throw, Duration(m.state, 1.4f));
                }
                else if (n.Contains("Death") && _act != Act.Death) Trigger(Act.Death, 3f);
            }
        }

        private static float Duration(EntityState st, float fallback)
        {
            for (System.Type t = st.GetType(); t != null && t != typeof(object); t = t.BaseType)
            {
                FieldInfo f = t.GetField("duration", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null && f.FieldType == typeof(float)) { float v = (float)f.GetValue(st); if (v > 0.2f) return v; }
                f = t.GetField("baseDuration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null && f.FieldType == typeof(float)) { float v = (float)f.GetValue(null); if (v > 0.2f) return v; }
            }
            return fallback;
        }

        // ---------------------------------------------------------------- animation (every client)

        private void LateUpdate()
        {
            if (!_built || !_body || !_root) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _t += dt;

            if (Time.time >= _nextStateScan) { _nextStateScan = Time.time + 0.1f; ScanStates(); }
            if (_act != Act.None)
            {
                _actT += dt;
                if (_actT >= _actDur && _act != Act.Death) _act = Act.None;
            }
            _actW = Mathf.MoveTowards(_actW, _act != Act.None ? 1f : 0f, dt * 6f);

            Vector3 pos = _body.footPosition;
            Vector3 v = (pos - _lastPos) / dt;
            _lastPos = pos;
            if (v.sqrMagnitude > 2500f) v = Vector3.zero;     // a teleport, not a step
            _vel = Vector3.Lerp(_vel, v, 1f - Mathf.Exp(-8f * dt));
            Vector3 flat = new Vector3(_vel.x, 0f, _vel.z);
            float speed = flat.magnitude;
            bool airborne = Mathf.Abs(_vel.y) > 3f && !Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, 1.6f + _s * 0.2f, LayerIndex.world.mask);

            bool rage = _body.healthComponent && _body.healthComponent.combinedHealthFraction < 0.5f;
            _crawlK = Mathf.MoveTowards(_crawlK, rage ? 1f : 0f, dt * 0.8f);
            float moveK = Mathf.Clamp01(speed / (6f + 3f * _crawlK));
            float stride = (2.1f + 0.8f * _crawlK) * _s;
            _phase += speed * dt / Mathf.Max(0.5f, stride) * Mathf.PI;
            float ph = _phase;
            float sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);

            // footsteps (heavy, and hands too once it crawls)
            float stepPhase = Mathf.Floor(ph / Mathf.PI);
            if (stepPhase != _lastStepPhase)
            {
                _lastStepPhase = stepPhase;
                if (moveK > 0.25f && !airborne) Util.PlaySound(_crawlK > 0.5f ? "Play_grandparent_handStep" : "Play_parent_step", gameObject);
            }

            // twitches: the head snaps to odd angles and back
            if (Time.time >= _nextTwitch)
            {
                _nextTwitch = Time.time + Random.Range(0.6f, 2.8f);
                _twitchTarget = Random.value < 0.6f ? new Vector3(Random.Range(-12f, 12f), Random.Range(-28f, 28f), Random.Range(-30f, 30f)) : Vector3.zero;
                if (Random.value < 0.15f) Util.PlaySound("Play_gravekeeper_idle_twitch", gameObject);
            }
            _twitch = Vector3.Lerp(_twitch, _twitchTarget, 1f - Mathf.Exp(-30f * dt));
            _twitchTarget = Vector3.Lerp(_twitchTarget, Vector3.zero, dt * 1.2f);

            // look at the prey
            float lookYaw = 0f, lookPitch = 0f;
            CharacterBody prey = NearestPlayer();
            if (prey)
            {
                Vector3 d = _root.InverseTransformDirection(prey.corePosition - (Head ? Head.position : _body.corePosition));
                lookYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -70f, 70f);
                lookPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -35f, 35f);
            }

            float breathe = Mathf.Sin(_t * 1.3f);
            float idle = 1f - moveK;
            float c = _crawlK;

            // ---- base pose: stalking walk / idle, blended into the all-fours crawl
            Vector3 hips = new Vector3(60f * c, sn * 6f * moveK, sn * 5f * moveK * (1f - c));
            Vector3 spine = new Vector3(6f * moveK + 8f * c + breathe * 1.5f * idle, -sn * 4f * moveK, 0f);
            Vector3 chest = new Vector3(breathe * 2.5f * idle + 6f * c, sn * 6f * moveK, -sn * 3f * moveK);
            Vector3 neck = new Vector3(-40f * c + lookPitch * 0.4f, lookYaw * 0.4f, 0f);
            Vector3 headR = new Vector3(lookPitch * 0.6f - 18f * c, lookYaw * 0.6f, 0f) + _twitch;

            float legSwing = (28f - 6f * c) * moveK;
            Vector3 thighR = new Vector3(-sn * legSwing - 60f * c - 18f * c, 0f, 0f);
            Vector3 thighL = new Vector3(sn * legSwing - 60f * c - 18f * c, 0f, 0f);
            Vector3 shinR = new Vector3(Mathf.Max(0f, -cs) * 35f * moveK + 28f * c, 0f, 0f);
            Vector3 shinL = new Vector3(Mathf.Max(0f, cs) * 35f * moveK + 28f * c, 0f, 0f);
            Vector3 footR = new Vector3(-shinR.x * 0.6f, 0f, 0f), footL = new Vector3(-shinL.x * 0.6f, 0f, 0f);

            // arms: swing against the legs upright; in the crawl they are front legs (diagonal gait)
            float armSwing = Mathf.Lerp(20f, 34f, c) * moveK;
            Vector3 uaR = new Vector3(Mathf.Lerp(sn, sn, c) * armSwing - 20f * c + Mathf.Sin(_t * 0.7f) * 3f * idle, 0f, 4f + 6f * idle);
            Vector3 uaL = new Vector3(-sn * armSwing - 20f * c + Mathf.Sin(_t * 0.7f + 1f) * 3f * idle, 0f, -4f - 6f * idle);
            if (c > 0.01f)
            {
                uaR.x = Mathf.Lerp(uaR.x, sn * armSwing - 25f, c);
                uaL.x = Mathf.Lerp(uaL.x, -sn * armSwing - 25f, c);
            }
            Vector3 faR = new Vector3(-12f - Mathf.Abs(sn) * 12f * moveK - 10f * c, 0f, 0f);
            Vector3 faL = new Vector3(-12f - Mathf.Abs(cs) * 12f * moveK - 10f * c, 0f, 0f);
            Vector3 hR = new Vector3(Mathf.Sin(_t * 0.9f) * 10f * idle, 0f, 0f), hL = new Vector3(Mathf.Sin(_t * 0.9f + 2f) * 10f * idle, 0f, 0f);
            float rootDrop = (Mathf.Abs(cs) * 0.07f * moveK - 0.32f * c) * _s;

            // ---- attacks and events, layered over the base pose
            if (_actW > 0.001f)
            {
                float w = _actW;
                float u = Mathf.Clamp01(_actT / _actDur);
                switch (_act)
                {
                    case Act.Slam:
                    {
                        // rear up with both scythes overhead, then smash them down
                        float up = Smooth(0f, 0.5f, u) * (1f - Smooth(0.52f, 0.62f, u));
                        float down = Smooth(0.52f, 0.62f, u) * (1f - Smooth(0.85f, 1f, u));
                        spine += new Vector3(-28f * up + 45f * down, 0f, 0f) * w;
                        chest += new Vector3(-12f * up + 15f * down, 0f, 0f) * w;
                        uaR += new Vector3(-160f * up - 60f * down, 0f, 18f * up) * w;
                        uaL += new Vector3(-160f * up - 60f * down, 0f, -18f * up) * w;
                        faR += new Vector3(-35f * up, 0f, 0f) * w;
                        faL += new Vector3(-35f * up, 0f, 0f) * w;
                        rootDrop -= 0.25f * _s * down * w;
                        if (u > 0.55f && u < 0.6f) { _jawTarget = 70f; }
                        break;
                    }
                    case Act.Throw:
                    {
                        // hurl spikes, one arm after the other: wind back, whip forward
                        float cyc = (_actT % 0.5f) / 0.5f;
                        bool right = ((int)(_actT / 0.5f) & 1) == 0;
                        float wind = Smooth(0f, 0.45f, cyc) * (1f - Smooth(0.45f, 0.6f, cyc));
                        float whip = Smooth(0.45f, 0.6f, cyc) * (1f - Smooth(0.8f, 1f, cyc));
                        Vector3 arm = new Vector3(70f * wind - 120f * whip, 0f, 0f) * w;
                        if (right) { uaR += arm + new Vector3(0f, 0f, 25f * wind) * w; faR += new Vector3(-40f * wind, 0f, 0f) * w; }
                        else { uaL += arm + new Vector3(0f, 0f, -25f * wind) * w; faL += new Vector3(-40f * wind, 0f, 0f) * w; }
                        chest += new Vector3(0f, (right ? -1f : 1f) * (22f * wind - 30f * whip), 0f) * w;
                        break;
                    }
                    case Act.Pounce:
                    {
                        // airborne: claws thrown forward, legs trailing, maw wide
                        float k = airborne ? 1f : 1f - Smooth(0.6f, 1f, u);
                        spine += new Vector3(-15f, 0f, 0f) * k * w;
                        uaR += new Vector3(-110f, 0f, 20f) * k * w;
                        uaL += new Vector3(-110f, 0f, -20f) * k * w;
                        faR += new Vector3(-20f, 0f, 0f) * k * w;
                        faL += new Vector3(-20f, 0f, 0f) * k * w;
                        thighR += new Vector3(55f, 0f, 0f) * k * w;
                        thighL += new Vector3(55f, 0f, 0f) * k * w;
                        neck += new Vector3(-20f, 0f, 0f) * k * w;
                        break;
                    }
                    case Act.Shriek:
                    case Act.Spawn:
                    {
                        // head thrown back, arms flung wide, the whole body shaking
                        float k = Smooth(0f, 0.2f, u) * (1f - Smooth(0.8f, 1f, u));
                        Vector3 shake = new Vector3(Random.Range(-4f, 4f), Random.Range(-4f, 4f), Random.Range(-4f, 4f)) * k;
                        chest += (new Vector3(-24f, 0f, 0f) + shake) * w * k;
                        neck += new Vector3(-35f, 0f, 0f) * w * k;
                        headR += (new Vector3(-30f, 0f, 0f) + shake * 2f) * w * k;
                        uaR += new Vector3(-30f, 0f, 70f) * w * k;
                        uaL += new Vector3(-30f, 0f, -70f) * w * k;
                        faR += new Vector3(-30f, 0f, 0f) * w * k;
                        faL += new Vector3(-30f, 0f, 0f) * w * k;
                        if (u < 0.85f) _jawTarget = 85f;
                        break;
                    }
                    case Act.Death:
                    {
                        float k = Smooth(0f, 1f, u);
                        hips += new Vector3(80f, 0f, 20f) * k;
                        thighR += new Vector3(-80f, 0f, 0f) * k; thighL += new Vector3(-60f, 0f, 0f) * k;
                        uaR += new Vector3(-40f, 0f, 30f) * k; uaL += new Vector3(-50f, 0f, -20f) * k;
                        rootDrop -= 0.9f * _s * k;
                        break;
                    }
                }
            }

            // jaw: hangs open, snaps now and then, chatters in a rage
            if (Time.time >= _nextSnap)
            {
                _nextSnap = Time.time + Random.Range(rage ? 0.5f : 1.8f, rage ? 1.4f : 4.5f);
                _jawTarget = Mathf.Max(_jawTarget, Random.Range(14f, rage ? 45f : 32f));
            }
            _jawTarget = Mathf.MoveTowards(_jawTarget, 4f, dt * 40f);
            _jaw = Mathf.Lerp(_jaw, _jawTarget, 1f - Mathf.Exp(-16f * dt));
            float chatter = rage ? Mathf.Sin(_t * 40f) * 4f : 0f;

            _root.position = pos + Vector3.up * rootDrop;
            Set("hips", hips); Set("spine", spine); Set("chest", chest); Set("neck", neck); Set("head", headR);
            Set("jaw", new Vector3(_jaw + chatter, 0f, 0f));
            Set("upper_arm.R", uaR); Set("forearm.R", faR); Set("hand.R", hR);
            Set("upper_arm.L", uaL); Set("forearm.L", faL); Set("hand.L", hL);
            Set("thigh.R", thighR); Set("shin.R", shinR); Set("foot.R", footR);
            Set("thigh.L", thighL); Set("shin.L", shinL); Set("foot.L", footL);
        }

        private void Set(string n, Vector3 e)
        {
            if (_j.TryGetValue(n, out Transform t)) t.localRotation = Quaternion.Euler(e);
        }

        private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / Mathf.Max(1e-4f, b - a)); return t * t * (3f - 2f * t); }

        private float _nextPrey;
        private CharacterBody _prey;

        private CharacterBody NearestPlayer()
        {
            if (Time.time < _nextPrey && _prey) return _prey;
            _nextPrey = Time.time + 0.5f;
            _prey = null;
            float best = 120f * 120f;
            foreach (CharacterBody p in SvrStalker.AlivePlayers())
            {
                float d = (p.corePosition - _body.corePosition).sqrMagnitude;
                if (d < best) { best = d; _prey = p; }
            }
            return _prey;
        }

        private void OnDestroy()
        {
            if (_root) Destroy(_root.gameObject);
        }
    }

    /// <summary>Host -> everyone: the Stalker pounced (1), landed (2) or shrieked (3), so every client animates it.</summary>
    public class SvrStalkerEventMessage : INetMessage
    {
        public GameObject Body;
        public byte Kind;
        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Kind); }
        public void Deserialize(NetworkReader r) { Body = r.ReadGameObject(); Kind = r.ReadByte(); }
        public void OnReceived()
        {
            if (!Body) return;
            SvrStalkerRig rig = Body.GetComponent<SvrStalkerRig>();
            if (rig) rig.OnEvent(Kind);
        }

        public static void Broadcast(GameObject body, byte kind)
        {
            SvrStalkerRig rig = body ? body.GetComponent<SvrStalkerRig>() : null;
            if (rig) rig.OnEvent(kind);                    // the host's own copy right away
            try { new SvrStalkerEventMessage { Body = body, Kind = kind }.Send(NetworkDestination.Clients); } catch { }
        }
    }
}
