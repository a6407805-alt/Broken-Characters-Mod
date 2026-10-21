using System;
using System.Collections.Generic;
using RoR2;
using UnityEngine;

namespace DuelistMod
{
    /// <summary>
    /// Makes the Duelist's own outfit flow: the coat tails, apron and hanging chains swing and trail as he
    /// moves, the lower parts lagging and swinging more than the upper ones, the coat body only stirring.
    /// The garment hangs from the waist like a short chain of pendulums (each link lags the one above it),
    /// pushed back by the air he moves through, pulled back to rest by a soft spring, with a light ripple
    /// running round the hem. Deforms the model's own coat meshes; purely visual, on every client.
    /// </summary>
    [DefaultExecutionOrder(10002)]
    public class DuelistOutfitFlow : MonoBehaviour
    {
        private class Garment
        {
            public Transform Tr;
            public Mesh Mesh;
            public Vector3[] Base, Work;
            public int[] Idx;          // vertices that move
            public float[] T;          // 0 at the waist .. 1 at the hem (negative: above the waist)
            public float[] Ang;        // angle round the body, for the ripple
        }

        private const int Links = 4;
        private const float SkirtLen = 0.8f;          // model units from the waist to the hem
        private const float UpperLen = 0.55f;         // waist to the shoulders
        private readonly List<Garment> _garments = new List<Garment>();
        private readonly Vector3[] _p = new Vector3[Links + 1], _prev = new Vector3[Links + 1], _off = new Vector3[Links + 1], _local = new Vector3[Links + 1];
        private Transform _pivot;
        private float _wait, _seg;
        private bool _ready;
        private Vector3 _lastPivot, _lastFwd;
        private float _turn;

        private bool Setup()
        {
            int moving = 0;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "DuelistPart_spine" && t.name != "DuelistPart_hips") continue;
                MeshFilter mf = t.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                Garment g = new Garment { Tr = t, Mesh = mf.sharedMesh };
                g.Base = g.Mesh.vertices;
                g.Work = (Vector3[])g.Base.Clone();
                List<int> idx = new List<int>();
                List<float> ts = new List<float>(), angs = new List<float>();
                for (int i = 0; i < g.Base.Length; i++)
                {
                    Vector3 v = g.Base[i];
                    float y = v.y + 0.02f;                   // mesh origin sits at the hips joint, about the waistline
                    float tt;
                    if (y < 0f) tt = Mathf.Clamp01(-y / SkirtLen);
                    else if (t.name == "DuelistPart_spine" && y < UpperLen) tt = -(1f - y / UpperLen);   // coat body: a gentle stir
                    else continue;
                    if (Mathf.Abs(tt) < 0.02f) continue;
                    idx.Add(i);
                    ts.Add(tt);
                    angs.Add(Mathf.Atan2(v.x, v.z));
                }
                if (idx.Count == 0) continue;
                g.Idx = idx.ToArray();
                g.T = ts.ToArray();
                g.Ang = angs.ToArray();
                g.Mesh.MarkDynamic();
                _garments.Add(g);
                moving += idx.Count;
                if (t.name == "DuelistPart_hips" || !_pivot) _pivot = t;
            }
            if (_garments.Count == 0 || !_pivot) return false;
            float scale = Mathf.Abs(_pivot.lossyScale.y);
            _seg = SkirtLen * scale / Links;
            Rest();
            DuelistPlugin.Log.LogInfo($"Outfit flow: {_garments.Count} garment meshes, {moving} moving vertices.");
            return true;
        }

        private void Rest()
        {
            Vector3 top = _pivot.position;
            for (int k = 0; k <= Links; k++) _p[k] = _prev[k] = top + Vector3.down * _seg * k;
            _lastPivot = top;
            _lastFwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        }

        private void LateUpdate()
        {
            if (!_ready)
            {
                _wait += Time.deltaTime;
                if (_wait < 0.5f) return;            // the custom model is built a moment after spawn
                _wait = 0f;
                if (!Setup()) return;
                _ready = true;
            }
            if (!_pivot) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);
            Vector3 top = _pivot.position;
            if ((top - _lastPivot).sqrMagnitude > 16f) Rest();       // teleported
            Vector3 vel = (top - _lastPivot) / dt;
            _lastPivot = top;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (fwd.sqrMagnitude > 0.001f)
            {
                fwd.Normalize();
                float yaw = Vector3.SignedAngle(_lastFwd, fwd, Vector3.up) / dt;
                _turn = Mathf.Lerp(_turn, Mathf.Clamp(yaw, -400f, 400f), dt * 6f);
                _lastFwd = fwd;
            }

            // the pendulum chain: each link trails the one above it, so the hem lags most
            _p[0] = top;
            float h = dt * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                for (int k = 1; k <= Links; k++)
                {
                    Vector3 rest = top + Vector3.down * _seg * k;
                    Vector3 v = (_p[k] - _prev[k]) * 0.9f;
                    Vector3 air = -vel * (0.9f + 0.5f * k);                           // the air he moves through
                    Vector3 acc = Vector3.down * 9.8f + air;
                    _prev[k] = _p[k];
                    _p[k] += v + acc * h * h;
                    // a soft spring back toward hanging straight: stiffer near the waist
                    _p[k] = Vector3.Lerp(_p[k], rest, Mathf.Clamp01(h * (11f - 2f * k)));
                }
                for (int it = 0; it < 3; it++)
                    for (int k = 1; k <= Links; k++)
                    {
                        Vector3 d = _p[k] - _p[k - 1];
                        float len = d.magnitude;
                        if (len > 1e-5f) _p[k] = _p[k - 1] + d * (_seg / len);
                        // never swing up past horizontal: it's cloth, not a flag
                        float maxUp = _p[k - 1].y - _seg * 0.15f;
                        if (_p[k].y > maxUp) _p[k].y = maxUp;
                    }
            }
            for (int k = 0; k <= Links; k++) _off[k] = _p[k] - (top + Vector3.down * _seg * k);

            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            float ripple = Mathf.Clamp01(speed / 12f) * 0.035f + Mathf.Abs(_turn) / 400f * 0.03f + 0.006f;
            float time = Time.time;
            foreach (Garment g in _garments)
            {
                if (!g.Tr) continue;
                for (int k = 0; k <= Links; k++) _local[k] = g.Tr.InverseTransformVector(_off[k]);
                Vector3 upperStir = _local[1] * 0.18f;
                for (int n = 0; n < g.Idx.Length; n++)
                {
                    int i = g.Idx[n];
                    float t = g.T[n];
                    Vector3 o;
                    float amp;
                    if (t >= 0f)
                    {
                        float f = t * Links;
                        int lo = Mathf.Min(Links - 1, (int)f);
                        o = Vector3.Lerp(_local[lo], _local[lo + 1], f - lo) * Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 3f));
                        amp = t * t;
                    }
                    else
                    {
                        o = upperStir * (-t);
                        amp = -t * 0.15f;
                    }
                    // a wave running round the hem, quicker when he's moving or turning
                    float a = g.Ang[n];
                    float wave = Mathf.Sin(time * 6.5f + a * 3f + t * 5f) * ripple * amp;
                    g.Work[i] = g.Base[i] + o + new Vector3(Mathf.Sin(a) * wave, wave * 0.4f, Mathf.Cos(a) * wave);
                }
                g.Mesh.vertices = g.Work;
                g.Mesh.RecalculateBounds();
            }
        }

        private void OnDestroy()
        {
            foreach (Garment g in _garments)
                if (g.Mesh && g.Base != null) g.Mesh.vertices = g.Base;
        }
    }
}
