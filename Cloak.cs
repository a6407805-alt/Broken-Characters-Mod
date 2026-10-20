using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuelistMod
{
    /// <summary>
    /// A sorcerer's cloak hanging from the Duelist's shoulders, simulated as cloth: it streams out behind
    /// him when he glides, swings when he turns, ripples in the air and falls back when he stops.
    /// Verlet points pinned along the shoulders, held together by distance constraints, kept off the
    /// body by a capsule. Deep violet outside, gold-trimmed hem, crimson lining. Purely visual, per client.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    public class DuelistCloak : MonoBehaviour
    {
        private const int Cols = 9, Rows = 12;
        private Transform _chest, _hips, _armL, _armR, _neck;
        private Vector3[] _p, _prev;
        private float _restW, _restH;
        private Mesh _mesh;
        private Vector3[] _verts;
        private bool _ready;
        private Vector3 _lastPin;
        private float _wait;
        private CharacterBody _body;
        private static Material _mat;

        private void Start()
        {
            CharacterModel cm = GetComponent<CharacterModel>();
            _body = cm ? cm.body : null;
        }

        private static Transform Find(Transform root, params string[] names)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                foreach (string n in names)
                    if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        private bool Setup()
        {
            _chest = Find(transform, "chest", "spine2", "upperchest");
            _hips = Find(transform, "pelvis", "base", "hips");
            _armL = Find(transform, "upper_arm.l", "upperarm.l", "upperarm_l");
            _armR = Find(transform, "upper_arm.r", "upperarm.r", "upperarm_r");
            _neck = Find(transform, "neck", "head");
            if (!_chest || !_hips || !_armL || !_armR) return false;

            float shoulder = Vector3.Distance(_armL.position, _armR.position);
            float torso = Vector3.Distance(_chest.position, _hips.position);
            if (shoulder < 0.05f || torso < 0.05f) return false;
            _restW = shoulder * 1.05f / (Cols - 1);
            _restH = torso * 2.35f / (Rows - 1);

            _p = new Vector3[Cols * Rows];
            _prev = new Vector3[Cols * Rows];
            Pins(out Vector3[] pins, out Vector3 back, out Vector3 down);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    Vector3 pos = pins[c] + down * (_restH * r) + back * (0.02f * r);
                    _p[r * Cols + c] = _prev[r * Cols + c] = pos;
                }
            _lastPin = pins[Cols / 2];
            BuildMesh();
            return true;
        }

        /// <summary>The pinned top row: across the shoulders, curved round the back of the neck.</summary>
        private void Pins(out Vector3[] pins, out Vector3 back, out Vector3 down)
        {
            Vector3 fwd = transform.forward;
            if (_chest) { Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up); if (f.sqrMagnitude > 0.01f) fwd = f.normalized; }
            back = -fwd;
            down = Vector3.down;
            float scale = Vector3.Distance(_armL.position, _armR.position);
            pins = new Vector3[Cols];
            Vector3 l = _armL.position, r = _armR.position;
            Vector3 top = _chest.position + (_neck ? (_neck.position - _chest.position) * 0.45f : Vector3.up * scale * 0.2f);
            for (int c = 0; c < Cols; c++)
            {
                float u = c / (float)(Cols - 1);
                Vector3 across = Vector3.Lerp(l, r, u);
                float middle = 1f - Mathf.Abs(u * 2f - 1f);                 // 0 at the shoulders, 1 behind the neck
                Vector3 p = new Vector3(across.x, Mathf.Lerp(across.y, top.y, 0.6f) + scale * 0.05f, across.z);
                p += back * (scale * (0.18f + 0.08f * middle));
                pins[c] = p;
            }
        }

        private void BuildMesh()
        {
            int n = Cols * Rows;
            _verts = new Vector3[n * 2];
            Vector2[] uv = new Vector2[n * 2];
            List<int> tris = new List<int>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    bool trim = r >= Rows - 2 || c == 0 || c == Cols - 1;
                    uv[i] = new Vector2(trim ? 0.5f : 0.1667f, 0.5f);          // outside: violet, gold at the hem and edges
                    uv[n + i] = new Vector2(trim ? 0.5f : 0.8333f, 0.5f);      // lining: crimson
                }
            for (int r = 0; r < Rows - 1; r++)
                for (int c = 0; c < Cols - 1; c++)
                {
                    int a = r * Cols + c, b = a + 1, d = a + Cols, e = d + 1;
                    tris.AddRange(new[] { a, d, b, b, d, e });                                  // outside
                    tris.AddRange(new[] { n + a, n + b, n + d, n + b, n + e, n + d });           // lining (reversed)
                }
            _mesh = new Mesh { name = "DuelistCloak" };
            _mesh.MarkDynamic();
            _mesh.vertices = _verts;
            _mesh.uv = uv;
            _mesh.triangles = tris.ToArray();
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);

            if (!_mat)
            {
                Texture2D tex = new Texture2D(3, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CloakPalette" };
                tex.SetPixel(0, 0, new Color(0.2f, 0.07f, 0.32f));
                tex.SetPixel(1, 0, new Color(0.95f, 0.74f, 0.28f));
                tex.SetPixel(2, 0, new Color(0.5f, 0.06f, 0.1f));
                tex.Apply(false, true);
                Texture2D em = new Texture2D(3, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CloakGlow" };
                em.SetPixel(0, 0, new Color(0.05f, 0.01f, 0.09f));
                em.SetPixel(1, 0, new Color(0.6f, 0.42f, 0.1f));
                em.SetPixel(2, 0, Color.black);
                em.Apply(false, true);
                _mat = DuelistModel.PaletteMaterial(null, tex, em);
                if (_mat.HasProperty("_EmPower")) _mat.SetFloat("_EmPower", 1f);
                _mat.name = "DuelistCloakMat";
            }

            GameObject go = new GameObject("Cloak") { layer = gameObject.layer };
            go.transform.SetParent(null, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            _render = go;
        }

        private GameObject _render;

        private void OnDestroy()
        {
            if (_render) Destroy(_render);
        }

        private void OnDisable()
        {
            if (_render) _render.SetActive(false);
        }

        private void OnEnable()
        {
            if (_render) _render.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                _wait += Time.deltaTime;
                if (_wait < 0.5f) return;                       // let the custom model and skeleton settle
                _wait = 0f;
                if (!Setup()) return;
                _ready = true;
            }
            if (!_chest || !_armL || !_armR || !_hips) return;
            if (_render) _render.transform.position = Vector3.zero;

            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.04f);
            Pins(out Vector3[] pins, out Vector3 back, out Vector3 down);
            if ((pins[Cols / 2] - _lastPin).sqrMagnitude > 25f) Reset(pins, back, down);   // teleported
            _lastPin = pins[Cols / 2];

            Vector3 vel = Vector3.zero;
            if (_body && _body.characterMotor) vel = _body.characterMotor.velocity;
            else if (_body && _body.rigidbody) vel = _body.rigidbody.velocity;
            float t = Time.time;
            int steps = 2;
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                for (int r = 0; r < Rows; r++)
                    for (int c = 0; c < Cols; c++)
                    {
                        int i = r * Cols + c;
                        if (r == 0) { _prev[i] = _p[i]; _p[i] = pins[c]; continue; }
                        Vector3 cur = _p[i];
                        Vector3 v = (cur - _prev[i]) * 0.965f;
                        // air pushes the cloth against the way he moves, plus a breathing ripple
                        float depth = r / (float)(Rows - 1);
                        Vector3 air = -vel * (1.6f + 1.4f * depth)
                                    + back * (1.2f + Mathf.Sin(t * 2.3f + c * 0.7f + r * 0.4f) * 1.4f)
                                    + Vector3.up * Mathf.Sin(t * 3.1f + c * 1.3f) * 0.8f * depth;
                        Vector3 acc = Vector3.down * 9.8f + air;
                        _prev[i] = cur;
                        _p[i] = cur + v + acc * h * h;
                    }
                for (int it = 0; it < 4; it++)
                {
                    for (int r = 0; r < Rows; r++)
                        for (int c = 0; c < Cols; c++)
                        {
                            int i = r * Cols + c;
                            if (c < Cols - 1) Link(i, i + 1, _restW * (1f + 0.15f * r / Rows), r == 0);   // hem flares a little
                            if (r < Rows - 1) Link(i, i + Cols, _restH, r == 0);
                            if (r < Rows - 1 && c < Cols - 1) Link(i, i + Cols + 1, Mathf.Sqrt(_restW * _restW + _restH * _restH), r == 0);
                            if (r < Rows - 1 && c > 0) Link(i, i + Cols - 1, Mathf.Sqrt(_restW * _restW + _restH * _restH), r == 0);
                        }
                    Collide(back);
                }
            }

            int n = Cols * Rows;
            for (int i = 0; i < n; i++) { Vector3 nn = Normal(i, back); _verts[i] = _p[i] + nn * 0.006f; _verts[n + i] = _p[i] - nn * 0.006f; }
            _mesh.vertices = _verts;
            _mesh.RecalculateNormals();
        }

        private Vector3 Normal(int i, Vector3 back)
        {
            int r = i / Cols, c = i % Cols;
            Vector3 a = _p[r * Cols + Mathf.Min(c + 1, Cols - 1)] - _p[r * Cols + Mathf.Max(c - 1, 0)];
            Vector3 b = _p[Mathf.Min(r + 1, Rows - 1) * Cols + c] - _p[Mathf.Max(r - 1, 0) * Cols + c];
            Vector3 nrm = Vector3.Cross(a, b);
            if (nrm.sqrMagnitude < 1e-10f) return back;
            nrm.Normalize();
            return Vector3.Dot(nrm, back) < 0f ? -nrm : nrm;     // points away from his back
        }

        private void Link(int a, int b, float rest, bool pinnedA)
        {
            Vector3 d = _p[b] - _p[a];
            float len = d.magnitude;
            if (len < 1e-5f) return;
            float diff = (len - rest) / len;
            if (pinnedA) { _p[b] -= d * diff; return; }       // top row doesn't move
            _p[a] += d * (0.5f * diff);
            _p[b] -= d * (0.5f * diff);
        }

        /// <summary>Keeps the cloth outside the body: a capsule from the chest to below the hips, and behind the back.</summary>
        private void Collide(Vector3 back)
        {
            Vector3 a = _chest.position, b = _hips.position + (_hips.position - _chest.position) * 0.9f;
            float rad = Vector3.Distance(_armL.position, _armR.position) * 0.42f;
            for (int i = Cols; i < _p.Length; i++)
            {
                Vector3 p = _p[i];
                Vector3 ab = b - a;
                float k = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                Vector3 closest = a + ab * k;
                Vector3 off = p - closest;
                float dist = off.magnitude;
                if (dist < rad)
                {
                    Vector3 dir = dist > 1e-4f ? off / dist : back;
                    if (Vector3.Dot(dir, back) < -0.2f) dir = (dir + back * 1.5f).normalized;   // never pass through to the front
                    p = closest + dir * rad;
                }
                _p[i] = p;
            }
        }

        private void Reset(Vector3[] pins, Vector3 back, Vector3 down)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int i = r * Cols + c;
                    _p[i] = _prev[i] = pins[c] + down * (_restH * r) + back * (0.02f * r);
                }
        }
    }
}
