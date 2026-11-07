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
        public enum Act { None, Slam, Throw, PounceWindup, Pounce, Land, ShriekWindup, Shriek, Spawn, Death }

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
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            float h = any ? Mathf.Clamp(b.size.y, 4f, 8f) : 6.5f;      // the Imp Overlord's body only (effects made it ~25 m tall)
            _s = h / ourHeight * SvrPlugin.StalkerScale.Value;

            int layer = model.gameObject.layer;
            Transform holder = model.parent ? model.parent : gameObject.transform;
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

        // ---------------------------------------------------------------- actions (every attack is telegraphed)

        public void Trigger(Act a, float duration)
        {
            if (a == _act && _actT < 0.3f) return;           // the same event can arrive twice on the host
            _act = a;
            _actT = 0f;
            _actDur = Mathf.Max(0.3f, duration);
            switch (a)
            {
                case Act.Spawn:
                    _jawTarget = 80f;
                    Util.PlaySound("Play_imp_overlord_spawn", gameObject);
                    break;
                case Act.Slam:
                    // the slam: a red ring shows where it lands while it rears up
                    _jawTarget = 55f;
                    Util.PlaySound("Play_imp_overlord_attack2_tell", gameObject);
                    SvrGroundRing.Show(_body.footPosition, 14f, duration * 0.55f, new Color(1f, 0.15f, 0.1f));
                    break;
                case Act.Throw: _jawTarget = 40f; Util.PlaySound("Play_imp_overlord_attack1_throw", gameObject); break;
                case Act.Death: _jawTarget = 85f; Util.PlaySound("Play_imp_overlord_death", gameObject); break;
            }
        }

        public void OnEvent(byte kind, Vector3 pos, float radius)
        {
            switch (kind)
            {
                case 4:   // pounce wind-up: crouches, hisses, and the landing spot glows
                    Trigger(Act.PounceWindup, 0.95f);
                    _jawTarget = 60f;
                    Util.PlaySound("Play_imp_overlord_attack2_tell", gameObject);
                    Util.PlaySound("Play_imp_overlord_idle_VO", gameObject);
                    SvrGroundRing.Show(pos, radius, 1.9f, new Color(1f, 0.1f, 0.05f));
                    break;
                case 1:   // leap
                    _act = Act.Pounce; _actT = 0f; _actDur = 1.4f;
                    _jawTarget = 75f;
                    Util.PlaySound("Play_moonBrother_phaseJump_jumpAway", gameObject);
                    break;
                case 2:   // landed
                    if (Time.time - _lastLand < 0.4f) break;
                    _lastLand = Time.time;
                    Util.PlaySound("Play_moonBrother_phaseJump_land_impact", gameObject);
                    if (_act == Act.Pounce) { _act = Act.Land; _actT = 0f; _actDur = 0.7f; }
                    ShakeNear(1.2f, 24f);
                    break;
                case 5:   // shriek wind-up: rears back and draws breath, a purple ring shows how far the scream reaches
                    Trigger(Act.ShriekWindup, 0.9f);
                    _jawTarget = 0f;
                    Util.PlaySound("Play_imp_overlord_idle_VO", gameObject);
                    SvrGroundRing.Show(_body.footPosition, radius, 0.9f, new Color(0.7f, 0.2f, 1f));
                    break;
                case 3:   // shriek
                    _act = Act.Shriek; _actT = 0f; _actDur = 1.6f;
                    _jawTarget = 90f;
                    Util.PlaySound("Play_imp_overlord_spawn", gameObject);
                    ShakeNear(1.6f, 40f);
                    break;
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
                else if (n.Contains("Spike") || n.Contains("Throw") || n.Contains("FireVoid"))
                {
                    if (_act != Act.Throw && _act != Act.Pounce && _act != Act.PounceWindup) Trigger(Act.Throw, Duration(m.state, 1.4f));
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

        // the all-fours stance, posed and checked in Blender: hands and hind feet both on the ground
        private static readonly Vector3 HipsQ = new Vector3(55f, 0f, 0f), SpineQ = new Vector3(6f, 0f, 0f), ChestQ = new Vector3(4f, 0f, 0f);
        private static readonly Vector3 NeckQ = new Vector3(-48f, 0f, 0f), HeadQ = new Vector3(-22f, 0f, 0f);
        private static readonly Vector3 ThighRQ = new Vector3(-65f, 0f, 8f), ThighLQ = new Vector3(-65f, 0f, -8f), ShinQ = new Vector3(30f, 0f, 0f), FootQ = new Vector3(-15f, 0f, 0f);
        private static readonly Vector3 UpperArmRQ = new Vector3(-100f, 0f, 18f), UpperArmLQ = new Vector3(-100f, 0f, -18f), ForearmQ = new Vector3(22f, 0f, 0f);
        private const float DropQ = -0.22f;

        private readonly Dictionary<string, Quaternion> _cur = new Dictionary<string, Quaternion>();
        private float _drop = DropQ;

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
                if (_actT >= _actDur && _act != Act.Death && _act != Act.PounceWindup) _act = Act.None;
                if (_act == Act.PounceWindup && _actT > 2.5f) _act = Act.None;      // the leap never came
            }

            Vector3 pos = _body.footPosition;
            Vector3 v = (pos - _lastPos) / dt;
            _lastPos = pos;
            if (v.sqrMagnitude > 2500f) v = Vector3.zero;     // a teleport, not a step
            _vel = Vector3.Lerp(_vel, v, 1f - Mathf.Exp(-8f * dt));
            float speed = new Vector3(_vel.x, 0f, _vel.z).magnitude;
            bool airborne = Mathf.Abs(_vel.y) > 3f && !Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, 1.6f + _s * 0.2f, LayerIndex.world.mask);
            bool rage = _body.healthComponent && _body.healthComponent.combinedHealthFraction < 0.5f;

            float moveK = Mathf.Clamp01(speed / 7f);
            float stride = 2.6f * _s;
            _phase += speed * dt / Mathf.Max(0.5f, stride) * Mathf.PI;
            float ph = _phase, sn = Mathf.Sin(ph), cs = Mathf.Cos(ph);
            float stepPhase = Mathf.Floor(ph / Mathf.PI);
            if (stepPhase != _lastStepPhase)
            {
                _lastStepPhase = stepPhase;
                if (moveK > 0.2f && !airborne) Util.PlaySound((stepPhase % 2f) == 0f ? "Play_grandparent_handStep" : "Play_parent_step", gameObject);
            }

            // head twitches (snap there, ease back), look at the prey
            if (Time.time >= _nextTwitch)
            {
                _nextTwitch = Time.time + Random.Range(0.8f, 3f);
                _twitchTarget = Random.value < 0.55f ? new Vector3(Random.Range(-10f, 10f), Random.Range(-24f, 24f), Random.Range(-28f, 28f)) : Vector3.zero;
                if (Random.value < 0.12f) Util.PlaySound("Play_gravekeeper_idle_twitch", gameObject);
            }
            _twitch = Vector3.Lerp(_twitch, _twitchTarget, 1f - Mathf.Exp(-25f * dt));
            _twitchTarget = Vector3.Lerp(_twitchTarget, Vector3.zero, dt * 1.5f);
            float lookYaw = 0f, lookPitch = 0f;
            CharacterBody prey = NearestPlayer();
            if (prey && Head)
            {
                Vector3 d = _root.InverseTransformDirection(prey.corePosition - Head.position);
                lookYaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -60f, 60f);
                lookPitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -30f, 30f);
            }

            float idle = 1f - moveK;
            float breathe = Mathf.Sin(_t * (rage ? 2.4f : 1.4f));
            float gait = 22f * moveK * (rage ? 1.15f : 1f);

            // ---- base: the prowl (diagonal gait: right hand with left foot)
            Vector3 hips = HipsQ + new Vector3(breathe * 1.5f * idle, sn * 5f * moveK, sn * 4f * moveK);
            Vector3 spine = SpineQ + new Vector3(breathe * 2f * idle, -sn * 5f * moveK, 0f);
            Vector3 chest = ChestQ + new Vector3(breathe * 2.5f * idle, sn * 6f * moveK, -sn * 3f * moveK);
            Vector3 neck = NeckQ + new Vector3(lookPitch * 0.4f, lookYaw * 0.45f, 0f);
            Vector3 headR = HeadQ + new Vector3(lookPitch * 0.6f, lookYaw * 0.55f, 0f) + _twitch;
            Vector3 thR = ThighRQ + new Vector3(-sn * gait, 0f, 0f), thL = ThighLQ + new Vector3(sn * gait, 0f, 0f);
            Vector3 shR = ShinQ + new Vector3(Mathf.Max(0f, cs) * 30f * moveK, 0f, 0f), shL = ShinQ + new Vector3(Mathf.Max(0f, -cs) * 30f * moveK, 0f, 0f);
            Vector3 ftR = FootQ - new Vector3(Mathf.Max(0f, cs) * 15f * moveK, 0f, 0f), ftL = FootQ - new Vector3(Mathf.Max(0f, -cs) * 15f * moveK, 0f, 0f);
            Vector3 uaR = UpperArmRQ + new Vector3(sn * gait, 0f, Mathf.Sin(_t * 0.6f) * 2f * idle), uaL = UpperArmLQ + new Vector3(-sn * gait, 0f, -Mathf.Sin(_t * 0.6f + 1f) * 2f * idle);
            Vector3 faR = ForearmQ + new Vector3(Mathf.Max(0f, -cs) * 28f * moveK, 0f, 0f), faL = ForearmQ + new Vector3(Mathf.Max(0f, cs) * 28f * moveK, 0f, 0f);
            Vector3 hR = new Vector3(Mathf.Sin(_t * 0.9f) * 8f * idle, 0f, 0f), hL = new Vector3(Mathf.Sin(_t * 0.9f + 2f) * 8f * idle, 0f, 0f);
            float drop = DropQ - Mathf.Abs(cs) * 0.05f * moveK;

            // ---- actions over the base: wind-up (readable), strike (fast), recover (smooth)
            float u = Mathf.Clamp01(_actT / Mathf.Max(0.01f, _actDur));
            switch (_act)
            {
                case Act.Slam:
                {
                    float up = Smooth(0f, 0.5f, u) * (1f - Smooth(0.52f, 0.6f, u));
                    float down = Smooth(0.52f, 0.6f, u) * (1f - Smooth(0.8f, 1f, u));
                    hips += new Vector3(-40f * up + 5f * down, 0f, 0f);
                    thR += new Vector3(35f * up, 0f, 0f); thL += new Vector3(35f * up, 0f, 0f);
                    chest += new Vector3(-15f * up + 10f * down, 0f, 0f);
                    neck += new Vector3(-10f * up + 20f * down, 0f, 0f);
                    uaR += new Vector3(-75f * up + 20f * down, 0f, 10f * up); uaL += new Vector3(-75f * up + 20f * down, 0f, -10f * up);
                    faR += new Vector3(-40f * up, 0f, 0f); faL += new Vector3(-40f * up, 0f, 0f);
                    drop += 0.35f * up - 0.15f * down;
                    if (u > 0.52f && u < 0.56f && !_slamShook) { _slamShook = true; _jawTarget = 75f; ShakeNear(1.4f, 30f); }
                    break;
                }
                case Act.Throw:
                {
                    // rises onto its hind legs a little and whips one scythe after the other
                    float rise = Smooth(0f, 0.2f, u) * (1f - Smooth(0.85f, 1f, u));
                    hips += new Vector3(-20f * rise, 0f, 0f);
                    thR += new Vector3(18f * rise, 0f, 0f); thL += new Vector3(18f * rise, 0f, 0f);
                    float cyc = (_actT % 0.5f) / 0.5f;
                    bool right = ((int)(_actT / 0.5f) & 1) == 0;
                    float wind = Smooth(0f, 0.45f, cyc) * (1f - Smooth(0.45f, 0.6f, cyc));
                    float whip = Smooth(0.45f, 0.6f, cyc) * (1f - Smooth(0.8f, 1f, cyc));
                    Vector3 arm = new Vector3(55f * wind - 70f * whip, 0f, 0f) * rise;
                    if (right) { uaR += arm; faR += new Vector3(-30f * wind, 0f, 0f) * rise; }
                    else { uaL += arm; faL += new Vector3(-30f * wind, 0f, 0f) * rise; }
                    chest += new Vector3(0f, (right ? -1f : 1f) * (20f * wind - 26f * whip), 0f) * rise;
                    break;
                }
                case Act.PounceWindup:
                {
                    // coils down low, head pinned on the prey, a shiver building
                    float k = Smooth(0f, 0.6f, u);
                    float shiver = Mathf.Sin(_t * 55f) * 2.5f * k;
                    hips += new Vector3(8f * k, 0f, shiver);
                    thR += new Vector3(-15f * k, 0f, 0f); thL += new Vector3(-15f * k, 0f, 0f);
                    shR += new Vector3(25f * k, 0f, 0f); shL += new Vector3(25f * k, 0f, 0f);
                    uaR += new Vector3(25f * k, 0f, 6f * k); uaL += new Vector3(25f * k, 0f, -6f * k);
                    faR += new Vector3(15f * k, 0f, 0f); faL += new Vector3(15f * k, 0f, 0f);
                    neck += new Vector3(-12f * k, 0f, shiver);
                    drop -= 0.35f * k;
                    break;
                }
                case Act.Pounce:
                {
                    // stretched out in the air, claws first
                    float k = airborne || u < 0.4f ? 1f : 1f - Smooth(0.4f, 1f, u);
                    hips += new Vector3(-12f, 0f, 0f) * k;
                    uaR += new Vector3(-35f, 0f, 14f) * k; uaL += new Vector3(-35f, 0f, -14f) * k;
                    faR += new Vector3(-30f, 0f, 0f) * k; faL += new Vector3(-30f, 0f, 0f) * k;
                    thR += new Vector3(60f, 0f, 0f) * k; thL += new Vector3(60f, 0f, 0f) * k;
                    shR += new Vector3(20f, 0f, 0f) * k; shL += new Vector3(20f, 0f, 0f) * k;
                    neck += new Vector3(-10f, 0f, 0f) * k;
                    break;
                }
                case Act.Land:
                {
                    // slams down, absorbs, rises back to the prowl
                    float k = 1f - Smooth(0.2f, 1f, u);
                    drop -= 0.4f * k;
                    hips += new Vector3(10f * k, 0f, 0f);
                    uaR += new Vector3(15f * k, 0f, 10f * k); uaL += new Vector3(15f * k, 0f, -10f * k);
                    shR += new Vector3(25f * k, 0f, 0f); shL += new Vector3(25f * k, 0f, 0f);
                    break;
                }
                case Act.ShriekWindup:
                {
                    // rears back onto its haunches, head drawn in, ribs swelling
                    float k = Smooth(0f, 1f, u);
                    hips += new Vector3(-35f * k, 0f, 0f);
                    thR += new Vector3(30f * k, 0f, 0f); thL += new Vector3(30f * k, 0f, 0f);
                    chest += new Vector3(-8f * k, 0f, 0f);
                    neck += new Vector3(15f * k, 0f, 0f);
                    uaR += new Vector3(-20f * k, 0f, 20f * k); uaL += new Vector3(-20f * k, 0f, -20f * k);
                    drop += 0.25f * k;
                    break;
                }
                case Act.Shriek:
                case Act.Spawn:
                {
                    // head thrown up, arms flung wide, the whole body shaking
                    float k = Smooth(0f, 0.12f, u) * (1f - Smooth(0.8f, 1f, u));
                    float sh = Mathf.Sin(_t * 47f) * 3f * k;
                    hips += new Vector3(-35f * k, 0f, 0f);
                    thR += new Vector3(30f * k, 0f, 0f); thL += new Vector3(30f * k, 0f, 0f);
                    chest += new Vector3(-20f * k + sh, sh, 0f);
                    neck += new Vector3(-45f * k, 0f, sh);
                    headR += new Vector3(-25f * k + sh * 2f, 0f, sh);
                    uaR += new Vector3(-10f * k, 0f, 55f * k); uaL += new Vector3(-10f * k, 0f, -55f * k);
                    faR += new Vector3(-25f * k, 0f, 0f); faL += new Vector3(-25f * k, 0f, 0f);
                    drop += 0.3f * k;
                    if (u < 0.8f) _jawTarget = 90f;
                    break;
                }
                case Act.Death:
                {
                    float k = Smooth(0f, 1f, u);
                    hips += new Vector3(25f * k, 0f, 35f * k);
                    uaR += new Vector3(60f * k, 0f, 30f * k); uaL += new Vector3(70f * k, 0f, -25f * k);
                    thR += new Vector3(30f * k, 0f, 0f);
                    neck += new Vector3(40f * k, 0f, 30f * k);
                    drop -= 0.5f * k;
                    _jawTarget = 70f;
                    break;
                }
            }
            if (_act != Act.Slam) _slamShook = false;

            // jaw: hangs open, snaps now and then, chatters in a rage
            if (Time.time >= _nextSnap && _act == Act.None)
            {
                _nextSnap = Time.time + Random.Range(rage ? 0.6f : 2f, rage ? 1.6f : 5f);
                _jawTarget = Mathf.Max(_jawTarget, Random.Range(14f, rage ? 42f : 30f));
            }
            _jawTarget = Mathf.MoveTowards(_jawTarget, 4f, dt * 35f);
            _jaw = Mathf.Lerp(_jaw, _jawTarget, 1f - Mathf.Exp(-14f * dt));
            float chatter = rage ? Mathf.Sin(_t * 40f) * 3f : 0f;

            // smooth everything (no snapping between poses), quicker during strikes
            float rate = (_act == Act.Slam && u > 0.5f && u < 0.65f) || _act == Act.Pounce ? 30f : 12f;
            float a = 1f - Mathf.Exp(-rate * dt);
            _drop = Mathf.Lerp(_drop, drop, a);
            _root.position = pos + Vector3.up * (_drop * _s);
            Set("hips", hips, a); Set("spine", spine, a); Set("chest", chest, a); Set("neck", neck, a); Set("head", headR, a);
            Set("jaw", new Vector3(_jaw + chatter, 0f, 0f), 1f);
            Set("upper_arm.R", uaR, a); Set("forearm.R", faR, a); Set("hand.R", hR, a);
            Set("upper_arm.L", uaL, a); Set("forearm.L", faL, a); Set("hand.L", hL, a);
            Set("thigh.R", thR, a); Set("shin.R", shR, a); Set("foot.R", ftR, a);
            Set("thigh.L", thL, a); Set("shin.L", shL, a); Set("foot.L", ftL, a);
        }

        private bool _slamShook;

        private void Set(string n, Vector3 e, float a)
        {
            if (!_j.TryGetValue(n, out Transform t)) return;
            Quaternion want = Quaternion.Euler(e);
            Quaternion q = _cur.TryGetValue(n, out Quaternion c) ? Quaternion.Slerp(c, want, a) : want;
            _cur[n] = q;
            t.localRotation = q;
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

    /// <summary>Host -> everyone: the Stalker's attack events (4 pounce wind-up + landing spot, 1 leap, 2 landed, 5 shriek wind-up, 3 shriek).</summary>
    public class SvrStalkerEventMessage : INetMessage
    {
        public GameObject Body;
        public byte Kind;
        public Vector3 Pos;
        public float Radius;
        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Kind); w.Write(Pos); w.Write(Radius); }
        public void Deserialize(NetworkReader r) { Body = r.ReadGameObject(); Kind = r.ReadByte(); Pos = r.ReadVector3(); Radius = r.ReadSingle(); }
        public void OnReceived()
        {
            if (!Body || NetworkServer.active) return;          // the host already played it
            SvrStalkerRig rig = Body.GetComponent<SvrStalkerRig>();
            if (rig) rig.OnEvent(Kind, Pos, Radius);
        }

        public static void Broadcast(GameObject body, byte kind, Vector3 pos = default, float radius = 0f)
        {
            SvrStalkerRig rig = body ? body.GetComponent<SvrStalkerRig>() : null;
            if (rig) rig.OnEvent(kind, pos, radius);
            try { new SvrStalkerEventMessage { Body = body, Kind = kind, Pos = pos, Radius = radius }.Send(NetworkDestination.Clients); } catch { }
        }
    }

    /// <summary>A glowing ring on the ground that marks where an attack will hit; its inner ring grows out to the edge as the hit gets near.</summary>
    public class SvrGroundRing : MonoBehaviour
    {
        private static Mesh _ring;
        private float _start, _life, _radius;
        private Transform _outer, _inner;
        private Material _mat;

        public static void Show(Vector3 at, float radius, float seconds, Color color)
        {
            try
            {
                RaycastHit h;
                if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out h, 20f, LayerIndex.world.mask)) at = h.point;
                GameObject g = new GameObject("SvrAttackRing");
                g.transform.position = at + Vector3.up * 0.08f;
                g.AddComponent<SvrGroundRing>().Setup(radius, seconds, color);
            }
            catch { }
        }

        private void Setup(float radius, float seconds, Color c)
        {
            _start = Time.time; _life = Mathf.Max(0.2f, seconds); _radius = Mathf.Max(1f, radius);
            if (!_ring) _ring = MakeRing(72, 0.94f);
            Shader hg = Shader.Find("Hopoo Games/Deferred/Standard");
            if (hg)
            {
                _mat = new Material(hg) { name = "SvrAttackRing" };
                _mat.SetColor("_Color", c);
                if (_mat.HasProperty("_EmColor")) _mat.SetColor("_EmColor", c);
                if (_mat.HasProperty("_EmTex")) _mat.SetTexture("_EmTex", Texture2D.whiteTexture);
                if (_mat.HasProperty("_EmPower")) _mat.SetFloat("_EmPower", 4f);
                if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", 0f);
            }
            _outer = Part("Outer");
            _inner = Part("Inner");
            Update();
        }

        private Transform Part(string n)
        {
            GameObject g = new GameObject(n);
            g.transform.SetParent(transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = _ring;
            MeshRenderer mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return g.transform;
        }

        private void Update()
        {
            if (!_outer) return;
            float k = (Time.time - _start) / _life;
            if (k >= 1f) { if (_mat) Destroy(_mat); Destroy(gameObject); return; }
            float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 20f);
            _outer.localScale = new Vector3(_radius * pulse, 1f, _radius * pulse);
            float ir = Mathf.Max(0.3f, _radius * k);
            _inner.localScale = new Vector3(ir, 1f, ir);
            if (_mat && _mat.HasProperty("_EmPower")) _mat.SetFloat("_EmPower", 2f + 6f * k);
        }

        /// <summary>A flat ring in the XZ plane: outer radius 1, inner radius `inner`.</summary>
        private static Mesh MakeRing(int seg, float inner)
        {
            Vector3[] v = new Vector3[seg * 2];
            int[] t = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d; v[i * 2 + 1] = d * inner;
                int j = (i + 1) % seg;
                t[i * 6] = i * 2; t[i * 6 + 1] = j * 2; t[i * 6 + 2] = i * 2 + 1;
                t[i * 6 + 3] = i * 2 + 1; t[i * 6 + 4] = j * 2; t[i * 6 + 5] = j * 2 + 1;
            }
            Mesh m = new Mesh { name = "SvrRing", vertices = v, triangles = t };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
