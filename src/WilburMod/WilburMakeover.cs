using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WilburMod
{
    /// <summary>
    /// Wilbur's makeover: swaps the primitive spheres and cylinders of his body for smooth sculpted meshes
    /// (made by model/build_wilbur.py and embedded in the dll as wilbur_model.bin). Every mesh replaces a part
    /// of the same name in the same frame, so all of his animation, the blinking, the sniffing and the gold
    /// Super Piggy look keep working. If anything is missing it quietly keeps the old primitive pig.
    /// </summary>
    public static class WilburMakeover
    {
        private class Entry
        {
            public string Name;
            public int Frame;           // 0 = baked in the parent's frame, 1 = baked in the part's own frame
            public string[] Removes;
            public Mesh Mesh;
        }

        private static List<Entry> _entries;
        private static bool _loaded;

        private static List<Entry> Load()
        {
            if (_loaded) return _entries;
            _loaded = true;
            try
            {
                Stream s = typeof(WilburMakeover).Assembly.GetManifestResourceStream("WilburMod.wilbur_model.bin");
                if (s == null) { WilburPlugin.Log?.LogWarning("Wilbur makeover model not found in the dll; using the classic pig."); return null; }
                using (BinaryReader r = new BinaryReader(s))
                {
                    if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "WPIG") return null;
                    r.ReadInt32();
                    int n = r.ReadInt32();
                    List<Entry> list = new List<Entry>();
                    Dictionary<string, Mesh> shared = new Dictionary<string, Mesh>();
                    for (int i = 0; i < n; i++)
                    {
                        Entry e = new Entry { Name = Str(r), Frame = r.ReadInt32() };
                        e.Removes = new string[r.ReadInt32()];
                        for (int k = 0; k < e.Removes.Length; k++) e.Removes[k] = Str(r);
                        int vc = r.ReadInt32();
                        Vector3[] v = new Vector3[vc], nr = new Vector3[vc];
                        for (int k = 0; k < vc; k++) v[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        for (int k = 0; k < vc; k++) nr[k] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        int ic = r.ReadInt32();
                        int[] idx = new int[ic];
                        for (int k = 0; k < ic; k++) idx[k] = r.ReadInt32();

                        // the four legs (and hooves) are the same shape: share one mesh
                        string key = vc + ":" + ic + ":" + v[0];
                        if (!shared.TryGetValue(key, out Mesh m))
                        {
                            m = new Mesh { name = "Wilbur_" + e.Name };
                            if (vc > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                            m.vertices = v;
                            m.normals = nr;
                            m.triangles = idx;
                            m.RecalculateBounds();
                            shared[key] = m;
                        }
                        e.Mesh = m;
                        list.Add(e);
                    }
                    _entries = list;
                    WilburPlugin.Log?.LogInfo($"Wilbur makeover: {list.Count} sculpted parts loaded.");
                }
            }
            catch (Exception ex)
            {
                WilburPlugin.Log?.LogWarning("Wilbur makeover failed to load, using the classic pig: " + ex.Message);
                _entries = null;
            }
            return _entries;
        }

        private static string Str(BinaryReader r) => Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));

        private static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                Transform f = Find(c, name);
                if (f) return f;
            }
            return null;
        }

        /// <summary>Called at the end of BuildPig, before his controller caches anything.</summary>
        public static void Apply(GameObject pig)
        {
            if (WilburPlugin.SculptedModel != null && !WilburPlugin.SculptedModel.Value) return;
            List<Entry> list = Load();
            if (list == null) return;
            foreach (Entry e in list)
            {
                Transform t = Find(pig.transform, e.Name);
                MeshFilter mf = t ? t.GetComponent<MeshFilter>() : null;
                if (!mf) continue;
                mf.sharedMesh = e.Mesh;
                if (e.Frame == 0)
                {
                    t.localPosition = Vector3.zero;
                    t.localRotation = Quaternion.identity;
                }
                t.localScale = Vector3.one;
                foreach (string rem in e.Removes)
                {
                    Transform r = Find(pig.transform, rem);
                    if (r) Object.DestroyImmediate(r.gameObject);
                }
            }
        }
    }

    /// <summary>
    /// Jiggle physics: his belly and flanks wobble on springs when he starts, stops, lands or turns; his jowls
    /// bounce, his ears flop and his tail boings. Runs after his animation each frame and only adds on top of it.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class WilburJiggle : MonoBehaviour
    {
        private class SoftMesh
        {
            public Mesh Mesh;
            public Vector3[] Base, Work;
            public float[] W;
        }

        private readonly List<SoftMesh> _soft = new List<SoftMesh>();
        private Transform _head, _jowls, _earL, _earR, _tail;
        private Vector3 _jowlsPos;
        private bool _ready;

        private Vector3 _lastPos, _lastVel, _acc;
        private Vector3 _lastHeadPos, _lastHeadVel, _headAcc;
        private Vector3 _body, _bodyVel;       // belly spring (pig-local units)
        private Vector3 _cheek, _cheekVel;     // jowl spring (head-local)
        private float _ear, _earVel, _earSide, _earSideVel, _tailA, _tailVel, _tailB, _tailBVel;
        private bool _hasLast;

        private void Start()
        {
            _head = transform.Find("HeadPivot");
            _jowls = transform.Find("HeadPivot/Pig_Jowls");
            _earL = transform.Find("HeadPivot/EarL");
            _earR = transform.Find("HeadPivot/EarR");
            _tail = transform.Find("Pig_Tail");
            if (_jowls) _jowlsPos = _jowls.localPosition;

            foreach (string n in new[] { "Pig_Body", "Pig_Belly" })
            {
                Transform t = transform.Find(n);
                MeshFilter mf = t ? t.GetComponent<MeshFilter>() : null;
                if (!mf || !mf.sharedMesh || mf.sharedMesh.vertexCount < 200 || !mf.sharedMesh.isReadable) continue;   // only the sculpted body
                Mesh m = Instantiate(mf.sharedMesh);
                m.MarkDynamic();
                mf.sharedMesh = m;
                SoftMesh s = new SoftMesh { Mesh = m, Base = m.vertices };
                s.Work = (Vector3[])s.Base.Clone();
                s.W = new float[s.Base.Length];
                for (int i = 0; i < s.Base.Length; i++)
                {
                    Vector3 v = s.Base[i];
                    // the spine stays put; the belly, flanks and rump hang loose
                    float low = Mathf.Clamp01((0.98f - v.y) / 0.62f);
                    float side = 0.55f + 0.45f * Mathf.Clamp01(Mathf.Abs(v.x) / 0.45f);
                    s.W[i] = low * low * side;
                }
                _soft.Add(s);
            }
            _ready = true;
        }

        private static void Spring(ref Vector3 x, ref Vector3 v, Vector3 force, float k, float damp, float dt, float max)
        {
            v += (force - k * x - damp * v) * dt;
            x += v * dt;
            if (x.magnitude > max) { x = x.normalized * max; v *= 0.5f; }
        }

        private static void Spring(ref float x, ref float v, float force, float k, float damp, float dt, float max)
        {
            v += (force - k * x - damp * v) * dt;
            x += v * dt;
            if (Mathf.Abs(x) > max) { x = Mathf.Sign(x) * max; v *= 0.5f; }
        }

        private void LateUpdate()
        {
            if (!_ready || (WilburPlugin.Jiggle != null && !WilburPlugin.Jiggle.Value)) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            float amount = WilburPlugin.JiggleAmount != null ? Mathf.Max(0f, WilburPlugin.JiggleAmount.Value) : 1f;
            float scale = Mathf.Max(0.1f, transform.lossyScale.y);

            // how the pig is being shoved around (world acceleration, smoothed, in his own units)
            Vector3 pos = transform.position;
            Vector3 headPos = _head ? _head.position : pos;
            if (!_hasLast) { _lastPos = pos; _lastHeadPos = headPos; _hasLast = true; }
            if ((pos - _lastPos).sqrMagnitude > 25f * scale * scale) { _lastPos = pos; _lastHeadPos = headPos; _lastVel = _lastHeadVel = Vector3.zero; }   // teleported
            Vector3 vel = (pos - _lastPos) / dt;
            Vector3 hvel = (headPos - _lastHeadPos) / dt;
            _acc = Vector3.Lerp(_acc, (vel - _lastVel) / dt, 1f - Mathf.Exp(-20f * dt));
            _headAcc = Vector3.Lerp(_headAcc, (hvel - _lastHeadVel) / dt, 1f - Mathf.Exp(-20f * dt));
            _lastPos = pos; _lastVel = vel; _lastHeadPos = headPos; _lastHeadVel = hvel;

            Vector3 accLocal = transform.InverseTransformDirection(_acc) / scale * amount;
            Vector3 headLocal = (_head ? _head.InverseTransformDirection(_headAcc) : accLocal) / scale * amount;

            // belly: a soft, slightly underdamped spring (the wobble), pushed opposite to the acceleration
            Spring(ref _body, ref _bodyVel, -accLocal * 0.0045f, 120f, 7f, dt, 0.11f);
            foreach (SoftMesh s in _soft)
            {
                for (int i = 0; i < s.Base.Length; i++)
                {
                    float w = s.W[i];
                    Vector3 b = s.Base[i];
                    // sideways and fore-aft sway, plus a vertical squash that bulges the belly outward
                    s.Work[i] = new Vector3(b.x + _body.x * w + b.x * -_body.y * 0.6f * w,
                                            b.y + _body.y * w,
                                            b.z + _body.z * w);
                }
                s.Mesh.vertices = s.Work;
            }

            // jowls bounce, ears flop, tail boings
            Spring(ref _cheek, ref _cheekVel, -headLocal * 0.0035f, 170f, 8f, dt, 0.05f);
            if (_jowls) _jowls.localPosition = _jowlsPos + _cheek;

            Spring(ref _ear, ref _earVel, -headLocal.y * 0.9f, 90f, 5f, dt, 35f);
            Spring(ref _earSide, ref _earSideVel, -headLocal.x * 0.7f, 90f, 5f, dt, 25f);
            if (_earL) _earL.localRotation *= Quaternion.Euler(_ear, 0f, _earSide);
            if (_earR) _earR.localRotation *= Quaternion.Euler(-_ear, 0f, _earSide);

            Spring(ref _tailA, ref _tailVel, accLocal.x * 1.6f, 140f, 4f, dt, 45f);
            Spring(ref _tailB, ref _tailBVel, -accLocal.y * 1.2f, 140f, 4f, dt, 35f);
            if (_tail) _tail.localRotation *= Quaternion.Euler(_tailB, 0f, _tailA);
        }
    }

    /// <summary>
    /// A ground crack for the earthquake: a jagged, tapering split that follows the ground, unzips outward
    /// from Wilbur, then closes back up. A dark core with a dusty rim, so it reads as cracked earth.
    /// </summary>
    public class WilburGroundCrack : MonoBehaviour
    {
        private const int N = 12;
        private readonly Vector3[] _pts = new Vector3[N], _nrm = new Vector3[N], _side = new Vector3[N];
        private float _width, _life, _t;
        private Mesh _core, _rim;
        private static Material _coreMat, _rimMat;

        public static void Spawn(Vector3 center, Vector3 dir, float length, float width, float life)
        {
            if (_coreMat == null)
            {
                _coreMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_coreMat, new Color(0.05f, 0.03f, 0.025f), new Color(0.2f, 0.06f, 0f), 0.15f);
                _rimMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_rimMat, new Color(0.62f, 0.5f, 0.36f), Color.black, 0f);
            }
            GameObject go = new GameObject("WilburCrack");
            WilburGroundCrack c = go.AddComponent<WilburGroundCrack>();
            c._width = width;
            c._life = life;
            c.Build(center, dir.normalized, length);
        }

        private void Build(Vector3 center, Vector3 dir, float length)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float jag = 0f;
            for (int i = 0; i < N; i++)
            {
                float f = i / (float)(N - 1);
                // zig-zag: each step kinks left or right, more jagged further out
                jag += (i % 2 == 0 ? 1f : -1f) * UnityEngine.Random.Range(0.3f, 1f) * _width * 1.6f * (0.4f + f);
                jag *= 0.6f;
                Vector3 p = center + dir * (f * length + 0.4f) + side * jag;
                Vector3 n = Vector3.up;
                if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 12f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    p = hit.point;
                    n = hit.normal;
                }
                _pts[i] = p;
                _nrm[i] = n;
            }
            for (int i = 0; i < N; i++)
            {
                Vector3 along = (i < N - 1 ? _pts[i + 1] - _pts[i] : _pts[i] - _pts[i - 1]);
                _side[i] = Vector3.Cross(_nrm[i], along).normalized;
            }
            _core = MakeRenderer("Core", _coreMat);
            _rim = MakeRenderer("Rim", _rimMat);
            UpdateMeshes(0f);
        }

        private Mesh MakeRenderer(string name, Material mat)
        {
            GameObject g = new GameObject(name);
            g.transform.SetParent(transform, false);
            Mesh m = new Mesh();
            m.MarkDynamic();
            g.AddComponent<MeshFilter>().sharedMesh = m;
            MeshRenderer r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return m;
        }

        private void UpdateMeshes(float t)
        {
            // unzip outward over the first 0.25 s, close up over the last 0.6 s
            float reveal = Mathf.Clamp01(t / 0.25f) * (N - 1);
            float close = Mathf.Clamp01((_life - t) / 0.6f);
            Fill(_rim, reveal, close, 1.9f, 0.025f);
            Fill(_core, reveal, close, 1f, 0.045f);
        }

        private void Fill(Mesh m, float reveal, float close, float widthMul, float lift)
        {
            Vector3[] v = new Vector3[N * 2];
            Vector3[] nr = new Vector3[N * 2];
            for (int i = 0; i < N; i++)
            {
                float f = i / (float)(N - 1);
                float open = Mathf.Clamp01(reveal - i + 1f);
                float half = _width * widthMul * 0.5f * Mathf.Pow(1f - f, 0.6f) * open * close;   // widest at Wilbur, a hairline at the tip
                Vector3 p = _pts[i] + _nrm[i] * lift;
                v[i * 2] = p - _side[i] * half;
                v[i * 2 + 1] = p + _side[i] * half;
                nr[i * 2] = nr[i * 2 + 1] = _nrm[i];
            }
            int[] tri = new int[(N - 1) * 6];
            for (int i = 0; i < N - 1; i++)
            {
                int a = i * 2;
                tri[i * 6 + 0] = a; tri[i * 6 + 1] = a + 2; tri[i * 6 + 2] = a + 1;
                tri[i * 6 + 3] = a + 1; tri[i * 6 + 4] = a + 2; tri[i * 6 + 5] = a + 3;
            }
            m.Clear();
            m.vertices = v;
            m.normals = nr;
            m.triangles = tri;
            m.RecalculateBounds();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _life) { Destroy(gameObject); return; }
            UpdateMeshes(_t);
        }
    }
}
