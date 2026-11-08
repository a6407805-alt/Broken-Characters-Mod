using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace BerserkFury
{
    /// <summary>
    /// Splits the static OBJ into rigid parts (the model is 231 separate pieces) and gives each group a pivot,
    /// so the Zoid can walk: hips + knees, a 3-segment tail, head, arms and the two Buster Claw units.
    /// Model space (after import): +Z = forward (head), +Y = up, feet at Y = 0.
    /// </summary>
    public static class BerserkFuryRig
    {
        public class Part
        {
            public string Name, Parent;
            public Vector3 Pivot;   // model space
            public Mesh Mesh;
            public Vector3 Dir;     // prongs: direction the tip opens towards (model space, XY)
        }

        // Parent before child.
        static readonly string[][] Hierarchy =
        {
            new[] { "body", "" },
            new[] { "thighL", "body" }, new[] { "shinL", "thighL" },
            new[] { "thighR", "body" }, new[] { "shinR", "thighR" },
            new[] { "tail1", "body" }, new[] { "tail2", "tail1" }, new[] { "tail3", "tail2" },
            new[] { "head", "body" },
            new[] { "armL", "body" }, new[] { "armR", "body" },
            new[] { "mountL", "body" }, new[] { "clawL", "mountL" },
            new[] { "prongTopL", "clawL" }, new[] { "prongOutL", "clawL" }, new[] { "prongInL", "clawL" },
            new[] { "mountR", "body" }, new[] { "clawR", "mountR" },
            new[] { "prongTopR", "clawR" }, new[] { "prongOutR", "clawR" }, new[] { "prongInR", "clawR" },
        };

        public static List<Part> Parts;

        static string Label(Vector3 c)
        {
            float ax = Mathf.Abs(c.x);
            string side = c.x < 0f ? "L" : "R";
            // Buster Claw units: boom + hub ("mount"), palm ("claw"), three prongs
            if (c.y > 2.0f && c.z > 0.69f && ax > 0.5f) return (c.y > 2.4f ? "prongTop" : ax > 0.85f ? "prongOut" : "prongIn") + side;
            if (c.y > 2.0f && ax >= 0.6f && c.z > -0.1f && c.z <= 0.69f) return "claw" + side;
            if (c.y > 1.7f && ax > 0.25f && ax < 0.7f && c.z > -1.35f && c.z < 0.5f) return "mount" + side;
            if (c.z < -0.75f && ax < 0.35f && c.y < 1.7f) return c.z > -1.45f ? "tail1" : (c.z > -2.05f ? "tail2" : "tail3");
            if (ax < 0.3f && c.z > 0.7f && c.y > 1.2f) return "head";
            if (ax >= 0.2f && c.z > 0.5f && c.y < 1.3f) return "arm" + side;
            if (ax >= 0.3f && c.y < 0.95f && c.z > -0.85f && c.z < 0.75f) return "shin" + side;
            if (ax >= 0.3f && c.y < 1.75f && c.z > -0.85f && c.z < 0.35f) return "thigh" + side;
            return "body";
        }

        public static void Build(string objPath, float scale)
        {
            var pos = new List<Vector3>();
            var uv = new List<Vector2>();
            var nrm = new List<Vector3>();
            var faces = new List<int[]>();   // per corner: pos, uv, nrm (1-based, 0 = none)
            var ci = CultureInfo.InvariantCulture;

            foreach (string raw in File.ReadAllLines(objPath))
            {
                string line = raw.Trim();
                if (line.StartsWith("v "))
                {
                    string[] p = line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float x = float.Parse(p[0], ci), y = float.Parse(p[1], ci), z = float.Parse(p[2], ci);
                    pos.Add(new Vector3(-z * scale, y * scale, x * scale));
                }
                else if (line.StartsWith("vt "))
                {
                    string[] p = line.Substring(3).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    uv.Add(new Vector2(float.Parse(p[0], ci), float.Parse(p[1], ci)));
                }
                else if (line.StartsWith("vn "))
                {
                    string[] p = line.Substring(3).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float x = float.Parse(p[0], ci), y = float.Parse(p[1], ci), z = float.Parse(p[2], ci);
                    nrm.Add(new Vector3(-z, y, x).normalized);
                }
                else if (line.StartsWith("f "))
                {
                    string[] p = line.Substring(2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int[] idx = new int[p.Length * 3];
                    for (int i = 0; i < p.Length; i++)
                    {
                        string[] s = p[i].Split('/');
                        idx[i * 3] = int.Parse(s[0]);
                        idx[i * 3 + 1] = s.Length > 1 && s[1].Length > 0 ? int.Parse(s[1]) : 0;
                        idx[i * 3 + 2] = s.Length > 2 && s[2].Length > 0 ? int.Parse(s[2]) : 0;
                    }
                    // fan triangulate
                    for (int i = 1; i < p.Length - 1; i++)
                        faces.Add(new[] { idx[0], idx[1], idx[2], idx[i * 3], idx[i * 3 + 1], idx[i * 3 + 2], idx[(i + 1) * 3], idx[(i + 1) * 3 + 1], idx[(i + 1) * 3 + 2] });
                }
            }

            // Connected pieces (shared position indices)
            int[] uf = new int[pos.Count + 1];
            for (int i = 0; i < uf.Length; i++) uf[i] = i;
            int Find(int a) { while (uf[a] != a) { uf[a] = uf[uf[a]]; a = uf[a]; } return a; }
            foreach (int[] f in faces) { uf[Find(f[3])] = Find(f[0]); uf[Find(f[6])] = Find(f[0]); }

            var pieceVerts = new Dictionary<int, HashSet<int>>();
            foreach (int[] f in faces)
            {
                int root = Find(f[0]);
                if (!pieceVerts.TryGetValue(root, out var set)) pieceVerts[root] = set = new HashSet<int>();
                set.Add(f[0]); set.Add(f[3]); set.Add(f[6]);
            }
            var pieceLabel = new Dictionary<int, string>();
            foreach (var kv in pieceVerts)
            {
                Vector3 c = Vector3.zero;
                foreach (int v in kv.Value) c += pos[v - 1];
                pieceLabel[kv.Key] = Label(c / kv.Value.Count);
            }

            // Group triangles by label
            var groups = new Dictionary<string, List<int[]>>();
            foreach (int[] f in faces)
            {
                string l = pieceLabel[Find(f[0])];
                if (!groups.TryGetValue(l, out var list)) groups[l] = list = new List<int[]>();
                list.Add(f);
            }

            // Bounds per group -> pivots
            var bounds = new Dictionary<string, Bounds>();
            foreach (var g in groups)
            {
                Bounds b = new Bounds(pos[g.Value[0][0] - 1], Vector3.zero);
                foreach (int[] f in g.Value) { b.Encapsulate(pos[f[0] - 1]); b.Encapsulate(pos[f[3] - 1]); b.Encapsulate(pos[f[6] - 1]); }
                bounds[g.Key] = b;
            }

            Vector3 PivotFor(string n)
            {
                if (!bounds.TryGetValue(n, out Bounds b)) return Vector3.zero;
                Vector3 c = b.center;
                switch (n)
                {
                    case "body": return new Vector3(0f, 1.3f, -0.2f);
                    case "thighL": case "thighR": return new Vector3(c.x, b.max.y - 0.15f, c.z);
                    case "shinL": case "shinR": return new Vector3(c.x, b.max.y - 0.05f, c.z - 0.1f);
                    case "tail1": case "tail2": case "tail3": return new Vector3(0f, c.y, b.max.z);
                    case "head": return new Vector3(0f, b.min.y + 0.2f, b.min.z + 0.1f);
                    case "armL": case "armR": return new Vector3(c.x, b.max.y, b.min.z);
                    case "mountL": case "mountR": return new Vector3(Mathf.Sign(c.x) * 0.48f, 1.9f, -1.15f);
                    case "clawL": case "clawR": return new Vector3(c.x, c.y, b.min.z);
                    case "prongTopL": case "prongOutL": case "prongInL":
                    case "prongTopR": case "prongOutR": case "prongInR": return new Vector3(c.x, c.y, b.min.z);
                }
                return c;
            }

            Parts = new List<Part>();
            foreach (string[] h in Hierarchy)
            {
                if (!groups.TryGetValue(h[0], out var tris)) continue;
                string parent = h[1];
                while (parent != "" && !groups.ContainsKey(parent)) parent = Array.Find(Hierarchy, x => x[0] == parent)[1];
                Vector3 pivot = PivotFor(h[0]);
                Vector3 dir = Vector3.zero;
                if (h[0].StartsWith("prong") && bounds.TryGetValue("claw" + h[0].Substring(h[0].Length - 1), out Bounds palm))
                {
                    Vector3 d = pivot - palm.center; d.z = 0f;
                    dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.up;
                }
                Parts.Add(new Part { Name = h[0], Parent = parent, Pivot = pivot, Dir = dir, Mesh = MakeMesh(h[0], tris, pivot, pos, uv, nrm) });
            }
            BerserkFuryPlugin.Log.LogInfo("Rig: " + string.Join(", ", Parts.ConvertAll(p => p.Name + "(" + p.Mesh.triangles.Length / 3 + ")")));
        }

        static Mesh MakeMesh(string name, List<int[]> tris, Vector3 pivot, List<Vector3> pos, List<Vector2> uv, List<Vector3> nrm)
        {
            var map = new Dictionary<long, int>();
            var P = new List<Vector3>(); var U = new List<Vector2>(); var N = new List<Vector3>(); var T = new List<int>();
            bool hasN = true;
            foreach (int[] f in tris)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vp = f[k * 3], vt = f[k * 3 + 1], vn = f[k * 3 + 2];
                    long key = ((long)vp << 40) | ((long)vt << 20) | (long)vn;
                    if (!map.TryGetValue(key, out int id))
                    {
                        id = P.Count; map[key] = id;
                        P.Add(pos[vp - 1] - pivot);
                        U.Add(vt > 0 && vt <= uv.Count ? uv[vt - 1] : Vector2.zero);
                        if (vn > 0 && vn <= nrm.Count && nrm[vn - 1].sqrMagnitude > 0.5f) N.Add(nrm[vn - 1]); else { N.Add(Vector3.up); hasN = false; }
                    }
                    T.Add(id);
                }
            }
            Mesh m = new Mesh { name = "meshBerserkFury_" + name };
            if (P.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(P); m.SetUVs(0, U); m.SetNormals(N); m.SetTriangles(T, 0);
            if (!hasN) m.RecalculateNormals();
            m.RecalculateBounds();
            m.RecalculateTangents();
            Vector4[] tan = m.tangents;
            Vector3[] nn = m.normals;
            for (int i = 0; i < tan.Length; i++)
            {
                Vector4 t = tan[i];
                if (float.IsNaN(t.x) || float.IsNaN(t.y) || float.IsNaN(t.z) || new Vector3(t.x, t.y, t.z).sqrMagnitude < 1e-8f)
                {
                    Vector3 axis = Mathf.Abs(nn[i].y) < 0.9f ? Vector3.up : Vector3.right;
                    Vector3 tt = Vector3.Cross(axis, nn[i]).normalized;
                    tan[i] = new Vector4(tt.x, tt.y, tt.z, 1f);
                }
            }
            m.tangents = tan;
            return m;
        }

        /// <summary>Builds the part hierarchy under root and returns its renderers.</summary>
        public static List<Renderer> Instantiate(Transform root, Material mat)
        {
            var renderers = new List<Renderer>();
            var made = new Dictionary<string, Transform>();
            var pivots = new Dictionary<string, Vector3>();
            foreach (Part p in Parts)
            {
                GameObject go = new GameObject("BF_" + p.Name);
                Transform parent = p.Parent != "" && made.TryGetValue(p.Parent, out Transform pt) ? pt : root;
                Vector3 parentPivot = p.Parent != "" && pivots.TryGetValue(p.Parent, out Vector3 pp) ? pp : Vector3.zero;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = p.Pivot - parentPivot;
                go.transform.localRotation = Quaternion.identity;
                go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                mr.receiveShadows = true;
                renderers.Add(mr);
                if (p.Dir != Vector3.zero) go.AddComponent<BerserkFuryProng>().Dir = p.Dir;
                made[p.Name] = go.transform;
                pivots[p.Name] = p.Pivot;
            }
            return renderers;
        }
    }

    /// <summary>Remembers which way a Buster Claw prong opens.</summary>
    public class BerserkFuryProng : MonoBehaviour { public Vector3 Dir; }

    /// <summary>
    /// Heavy T-rex gait for the rigged Zoid: stride-locked leg swing with knee lift, body drop + roll on each
    /// footfall, lagging tail, counter-bobbing head, stomp dust + camera shake. Poses for each skill.
    /// </summary>
    public class BerserkFuryLocomotion : MonoBehaviour
    {
        const float StrideLength = 3.4f;   // metres per full cycle (two steps)

        CharacterBody _body;
        EntityStateMachine _weapon, _body2;
        Transform _bodyT, _thighL, _thighR, _shinL, _shinR, _t1, _t2, _t3, _head, _armL, _armR, _mountL, _mountR, _clawL, _clawR;
        BerserkFuryProng[] _prongsL, _prongsR;
        float _spinL, _spinR, _open, _shield, _drillSpin;
        Vector3 _bodyRest;
        float _phase, _move, _lastYaw, _turn, _brace, _lunge, _surge, _recoil;
        bool _plantedL, _plantedR;
        static GameObject _dust;

        Transform F(string n) { Transform t = transform.Find(n); return t ? t : FindDeep(transform, n); }
        static Transform FindDeep(Transform t, string n)
        {
            foreach (Transform c in t) { if (c.name == n) return c; Transform r = FindDeep(c, n); if (r) return r; }
            return null;
        }

        void Start()
        {
            _bodyT = F("BF_body"); _thighL = F("BF_thighL"); _thighR = F("BF_thighR"); _shinL = F("BF_shinL"); _shinR = F("BF_shinR");
            _t1 = F("BF_tail1"); _t2 = F("BF_tail2"); _t3 = F("BF_tail3"); _head = F("BF_head");
            _armL = F("BF_armL"); _armR = F("BF_armR"); _mountL = F("BF_mountL"); _mountR = F("BF_mountR");
            _clawL = F("BF_clawL"); _clawR = F("BF_clawR");
            _prongsL = _clawL ? _clawL.GetComponentsInChildren<BerserkFuryProng>() : new BerserkFuryProng[0];
            _prongsR = _clawR ? _clawR.GetComponentsInChildren<BerserkFuryProng>() : new BerserkFuryProng[0];
            if (_bodyT) _bodyRest = _bodyT.localPosition;
            _body = GetComponentInParent<CharacterBody>();
            if (!_dust) { try { _dust = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/CharacterLandImpact"); } catch { } }
        }

        static void Rot(Transform t, float pitch, float yaw, float roll) { if (t) t.localRotation = Quaternion.Euler(pitch, yaw, roll); }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float time = Time.time;
            if (!_body) _body = GetComponentInParent<CharacterBody>();

            float speed = 0f, yawRate = 0f;
            bool grounded = true, sprinting = false;
            string state = "";
            if (_body)
            {
                Vector3 v = _body.characterMotor ? _body.characterMotor.velocity : Vector3.zero;
                speed = new Vector2(v.x, v.z).magnitude;
                grounded = !_body.characterMotor || _body.characterMotor.isGrounded;
                sprinting = _body.isSprinting;
                float yaw = _body.gameObject.transform.eulerAngles.y;
                if (_body.characterDirection && _body.characterDirection.targetTransform) yaw = _body.characterDirection.targetTransform.eulerAngles.y;
                yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / Mathf.Max(dt, 0.001f);
                _lastYaw = yaw;
                if (!_weapon) _weapon = EntityStateMachine.FindByCustomName(_body.gameObject, "Weapon");
                if (!_body2) _body2 = EntityStateMachine.FindByCustomName(_body.gameObject, "Body");
                if (_weapon && _weapon.state != null && !(_weapon.state is Idle)) state = _weapon.state.GetType().Name;
                if (_body2 && _body2.state != null && _body2.state.GetType().Name.Contains("Thruster")) state = _body2.state.GetType().Name;
            }

            // Smoothed blends
            float targetMove = grounded ? Mathf.Clamp01(speed / 4f) : 0f;
            _move = Mathf.MoveTowards(_move, targetMove, dt * 3f);
            _turn = Mathf.Lerp(_turn, Mathf.Clamp(yawRate / 180f, -1f, 1f), dt * 4f);
            _brace = Mathf.MoveTowards(_brace, state.Contains("ParticleCannon") ? 1f : 0f, dt * 3f);
            _lunge = Mathf.MoveTowards(_lunge, state.Contains("Drill") ? 1f : 0f, dt * 6f);
            _surge = Mathf.MoveTowards(_surge, state.Contains("Thruster") ? 1f : 0f, dt * 5f);
            if (state.Contains("Blaster")) _recoil = Mathf.Max(_recoil, 0.6f + 0.4f * Mathf.Sin(time * 25f));
            _recoil = Mathf.MoveTowards(_recoil, 0f, dt * 3f);

            // Stride-locked phase (feet don't skate)
            float prev = _phase;
            if (grounded) _phase += speed / StrideLength * Mathf.PI * 2f * dt;
            else _phase = Mathf.Lerp(_phase, Mathf.Round(_phase / Mathf.PI) * Mathf.PI, dt * 4f);
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase);
            float m = _move * (1f - _brace);
            float stepAmp = sprinting ? 38f : 28f;

            // Legs: forward swing = negative pitch; knee lifts while the leg swings forward
            float legL = -s * stepAmp * m, legR = s * stepAmp * m;
            float kneeL = Mathf.Max(0f, c) * 50f * m, kneeR = Mathf.Max(0f, -c) * 50f * m;
            // Brace (CPC): wide crouch
            legL += _brace * -12f; legR += _brace * -12f; kneeL += _brace * 25f; kneeR += _brace * 25f;
            if (!grounded) { legL = -20f; legR = 10f; kneeL = 35f; kneeR = 20f; }
            Rot(_thighL, legL, 0f, _brace * -8f); Rot(_thighR, legR, 0f, _brace * 8f);
            Rot(_shinL, kneeL, 0f, 0f); Rot(_shinR, kneeR, 0f, 0f);

            // Footfalls: a foot plants when its leg reaches the front of the stride
            bool plantL = s > 0.97f, plantR = s < -0.97f;
            if (grounded && m > 0.4f)
            {
                if (plantL && !_plantedL) Stomp(_shinL);
                if (plantR && !_plantedR) Stomp(_shinR);
            }
            _plantedL = plantL; _plantedR = plantR;

            // Body: drops on each footfall, rolls over the planted leg, leans into speed and turns
            float impact = Mathf.Pow(Mathf.Abs(s), 6f);
            float breathe = Mathf.Sin(time * 1.6f) * 0.025f * (1f - m);
            float bob = -impact * 0.16f * m + breathe - _brace * 0.35f + _surge * -0.1f;
            if (_bodyT)
            {
                _bodyT.localPosition = _bodyRest + new Vector3(0f, bob, _recoil * -0.12f);
                float pitch = m * (sprinting ? 12f : 6f) + _surge * 22f - _brace * 4f + _lunge * 10f + impact * 3f * m;
                Rot(_bodyT, pitch, 0f, s * 5f * m - _turn * 8f);
            }

            // Tail: lagging wave, straightens and lifts when sprinting/thrusting, drops to the ground to anchor the CPC
            float lag1 = Mathf.Sin(_phase - 0.8f), lag2 = Mathf.Sin(_phase - 1.6f), lag3 = Mathf.Sin(_phase - 2.4f);
            float idle = Mathf.Sin(time * 1.1f);
            float tailLift = -(m * 4f + _surge * 10f) + _brace * 18f;
            Rot(_t1, tailLift + impact * 4f * m, lag1 * 7f * m + idle * 3f * (1 - m) + _turn * 12f, 0f);
            Rot(_t2, tailLift * 0.6f + _brace * 6f, lag2 * 10f * m + Mathf.Sin(time * 1.1f - 0.6f) * 5f * (1 - m) + _turn * 10f, 0f);
            Rot(_t3, tailLift * 0.4f + _brace * 6f, lag3 * 14f * m + Mathf.Sin(time * 1.1f - 1.2f) * 7f * (1 - m) + _turn * 8f, 0f);

            // Head: counter-bob, looks around at idle, raises to aim the CPC
            float look = Mathf.Sin(time * 0.6f) * 8f * (1 - m) * (1 - _brace);
            Rot(_head, impact * -6f * m - _brace * 14f - _lunge * 6f - (sprinting ? 6f : 0f), look - _turn * 10f, 0f);

            // Arms: swing opposite to the legs; jab forward for the drill
            Rot(_armL, -legL * 0.4f - _lunge * 55f, 0f, 0f);
            Rot(_armR, -legR * 0.4f - _lunge * 55f, 0f, 0f);

            // ---- Buster Claws: boom swing, palm aim + spin, three prongs open/close ----
            _shield = Mathf.MoveTowards(_shield, sprinting && grounded && _brace < 0.1f ? 1f : 0f, dt * 4f);
            bool firing = state.Contains("Blaster");
            float openTarget = 0.12f + Mathf.Sin(time * 1.3f) * 0.05f;               // idle: breathing
            if (firing) openTarget = 0.55f;
            if (_shield > 0.5f) openTarget = 0.75f;
            if (_brace > 0.5f) openTarget = 1f;
            if (_lunge > 0.3f || _surge > 0.3f) openTarget = -0.15f;                // clamp shut to drill / streamline
            _open = Mathf.MoveTowards(_open, openTarget, dt * 3.5f);

            // Spin: drills spin hard, shield spins slowly, CPC turns slowly
            float spinRate = _lunge * 1500f + _shield * 220f + _brace * 90f;
            _spinL += spinRate * dt; _spinR -= spinRate * dt;
            if (spinRate < 1f) { _spinL = Mathf.MoveTowardsAngle(_spinL, 0f, dt * 180f); _spinR = Mathf.MoveTowardsAngle(_spinR, 0f, dt * 180f); }

            // Booms: + pitch = claws dip forward/down; yaw = swing out
            float boomPitch = impact * 6f * m - (sprinting ? 4f : 0f)
                              + _lunge * 22f            // drill: thrust forward-down
                              - _surge * 28f            // thrusters: claws fold up and back
                              + _brace * 10f            // CPC: level out
                              + _shield * 6f;
            float boomYaw = _brace * 32f + _shield * 12f - _lunge * 10f + Mathf.Sin(time * 0.9f) * 2f * (1 - m);
            float kickL = firing && Mathf.Sin(time * 12f) > 0f ? _recoil : 0f;
            float kickR = firing && Mathf.Sin(time * 12f) <= 0f ? _recoil : 0f;
            Rot(_mountL, boomPitch - kickL * 10f + lag1 * 2f * m, -boomYaw, 0f);
            Rot(_mountR, boomPitch - kickR * 10f - lag1 * 2f * m, boomYaw, 0f);
            Rot(_clawL, -kickL * 14f - _brace * 6f, -_shield * 8f, _spinL);
            Rot(_clawR, -kickR * 14f - _brace * 6f, _shield * 8f, _spinR);
            float openDeg = _open * 38f;
            foreach (BerserkFuryProng pr in _prongsL) OpenProng(pr, openDeg);
            foreach (BerserkFuryProng pr in _prongsR) OpenProng(pr, openDeg);
        }

        static void OpenProng(BerserkFuryProng pr, float deg)
        {
            if (!pr) return;
            Vector3 axis = Vector3.Cross(pr.Dir, Vector3.forward);
            pr.transform.localRotation = Quaternion.AngleAxis(-deg, axis);
        }

        void Stomp(Transform foot)
        {
            if (!foot) return;
            Vector3 p = foot.position;
            p.y = transform.position.y;
            try
            {
                if (_dust) EffectManager.SimpleEffect(_dust, p, Quaternion.identity, false);
                ShakeEmitter.CreateSimpleShakeEmitter(p, new Wave { amplitude = 0.5f, frequency = 20f, cycleOffset = 0f }, 0.2f, 16f, true);
                Util.PlaySound("Play_char_land", gameObject);
            }
            catch { }
        }
    }
}
