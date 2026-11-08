using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using EntityStates;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace GravitosNext
{
    /// <summary>
    /// Lodestar settings, plus the floating parts from lodestar_fx.bin: three iron rings around the torso
    /// (parented to the chest piece) and a cluster of shards at each palm (parented to the hand pieces).
    /// Both files share one palette, so the body material paints them too.
    /// </summary>
    public static partial class LodestarModel
    {
        public const string FxFileName = "lodestar_fx.bin";

        public static ConfigEntry<bool> UseCustomModel;
        public static ConfigEntry<bool> ModelOutline;
        public static ConfigEntry<float> ModelScale;
        public static ConfigEntry<float> ModelGlow;
        public static ConfigEntry<float> RingSpin;
        public static ConfigEntry<float> RingCastBoost;

        public static void Init(ConfigFile cfg)
        {
            UseCustomModel = cfg.Bind("8. Lodestar Model", "Use Lodestar Model", true, "Use the Blender-made Lodestar (lodestar_model.bin next to the dll). Off = Engineer model.");
            ModelOutline = cfg.Bind("8. Lodestar Model", "Outline", true, "Black cel-shade outline around the model.");
            ModelScale = cfg.Bind("8. Lodestar Model", "Scale", 1f, "Extra size multiplier for the model.");
            ModelGlow = cfg.Bind("8. Lodestar Model", "Glow", 1.2f, "Strength of the violet/cyan glow.");
            RingSpin = cfg.Bind("8. Lodestar Model", "Ring Spin", 1f, "How fast the iron rings orbit (1 = normal).");
            RingCastBoost = cfg.Bind("8. Lodestar Model", "Ring Cast Boost", 3f, "Ring and shard speed multiplier while a skill is being used.");
        }

        public static Shader SafeFindShader(string name)
        {
            try { return Shader.Find(name); }
            catch { return null; }
        }

        private class FxPart { public string Name; public Vector3 Pivot; public Vector3[] Pos, Nrm; public int[] Pal; public Mesh Mesh; }

        private static List<FxPart> _fx;
        private static bool _fxTried;

        private static List<FxPart> LoadFx()
        {
            if (_fxTried) return _fx;
            _fxTried = true;
            string path = FindFile(FxFileName);
            if (path == null) { GravitosPlugin.Log.LogInfo($"No {FxFileName}: Lodestar has no rings."); return null; }
            try
            {
                List<FxPart> list = new List<FxPart>();
                Dictionary<string, Vector3> pivots = new Dictionary<string, Vector3>();
                using (BinaryReader br = new BinaryReader(OpenData(path)))
                {
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "DUEL") throw new Exception("not a model file");
                    br.ReadInt32();
                    int nb = br.ReadInt32();
                    for (int i = 0; i < nb; i++)
                    {
                        string name = ReadStr(br);
                        pivots[name] = ReadV3(br);
                        ReadV3(br);
                    }
                    int np = br.ReadInt32();
                    br.ReadBytes(np * 16);               // same palette as the body file
                    int nparts = br.ReadInt32();
                    for (int i = 0; i < nparts; i++)
                    {
                        FxPart p = new FxPart { Name = ReadStr(br) };
                        int n = br.ReadInt32();
                        p.Pos = new Vector3[n];
                        p.Nrm = new Vector3[n];
                        p.Pal = new int[n];
                        for (int j = 0; j < n; j++) { p.Pos[j] = ReadV3(br); p.Nrm[j] = ReadV3(br); p.Pal[j] = br.ReadInt32(); }
                        p.Pivot = pivots.TryGetValue(p.Name, out Vector3 pv) ? pv : Vector3.zero;
                        list.Add(p);
                    }
                }
                float w = Mathf.Max(1, _palColor.Length);
                foreach (FxPart p in list)
                {
                    int n = p.Pos.Length;
                    Vector3[] v = new Vector3[n];
                    Vector2[] uv = new Vector2[n];
                    int[] tris = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        v[i] = p.Pos[i] - p.Pivot;
                        uv[i] = new Vector2((p.Pal[i] + 0.5f) / w, 0.5f);
                        int k = i % 3;                   // stored (a, c, b) like the body file
                        tris[i] = k == 1 ? i + 1 : (k == 2 ? i - 1 : i);
                    }
                    p.Mesh = new Mesh { name = "LodestarFx_" + p.Name };
                    if (n > 65000) p.Mesh.indexFormat = IndexFormat.UInt32;
                    p.Mesh.vertices = v;
                    p.Mesh.normals = p.Nrm;
                    p.Mesh.uv = uv;
                    p.Mesh.triangles = tris;
                    p.Mesh.RecalculateBounds();
                }
                _fx = list;
                GravitosPlugin.Log.LogInfo($"Loaded {FxFileName}: {list.Count} parts.");
            }
            catch (Exception e)
            {
                GravitosPlugin.Log.LogError("Couldn't read " + path + ": " + e);
                _fx = null;
            }
            return _fx;
        }

        // tilt of each ring (degrees) so the three cross like an orbit diagram
        private static readonly Vector3[] RingTilt = { new Vector3(18f, 0f, 8f), new Vector3(-24f, 40f, 14f), new Vector3(9f, 80f, -30f) };

        private static void AttachFx(Transform model, Dictionary<string, KeyValuePair<Transform, Vector3>> placed, int layer, Material mat)
        {
            List<FxPart> fx = LoadFx();
            if (fx == null) return;
            LodestarOrbit orbit = model.gameObject.GetComponent<LodestarOrbit>() ?? model.gameObject.AddComponent<LodestarOrbit>();
            foreach (FxPart p in fx)
            {
                string bone = p.Name.StartsWith("Ring") ? "chest" : (p.Name.EndsWith("L") ? "hand.L" : "hand.R");
                if (!placed.TryGetValue(bone, out KeyValuePair<Transform, Vector3> host)) continue;
                // holder sits at the pivot in the host piece's space (its local units are the model's own units)
                GameObject holder = new GameObject("LodestarFx_" + p.Name) { layer = layer };
                holder.transform.SetParent(host.Key, false);
                holder.transform.localPosition = p.Pivot - host.Value;
                GameObject spin = new GameObject("Spin") { layer = layer };
                spin.transform.SetParent(holder.transform, false);
                spin.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                MeshRenderer mr = spin.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                if (p.Name.StartsWith("Ring") && int.TryParse(p.Name.Substring(4), out int i) && i < RingTilt.Length)
                {
                    holder.transform.localRotation = Quaternion.Euler(RingTilt[i]);
                    orbit.Add(spin.transform, (i % 2 == 0 ? 1f : -1f) * (45f + 20f * i), true);
                }
                else orbit.Add(spin.transform, 120f, false);
            }
        }
    }

    /// <summary>Spins the rings and shards; faster while Gravitos is casting.</summary>
    public class LodestarOrbit : MonoBehaviour
    {
        private readonly List<Transform> _spins = new List<Transform>();
        private readonly List<float> _speeds = new List<float>();
        private readonly List<bool> _isRing = new List<bool>();
        private CharacterModel _cm;
        private EntityStateMachine _weapon, _weapon2;
        private float _boost = 1f;

        public void Add(Transform t, float degPerSec, bool ring)
        {
            _spins.Add(t);
            _speeds.Add(degPerSec);
            _isRing.Add(ring);
        }

        private void Awake() => _cm = GetComponent<CharacterModel>();

        private bool Casting()
        {
            CharacterBody body = _cm ? _cm.body : null;
            if (!body) return false;
            if (!_weapon) _weapon = EntityStateMachine.FindByCustomName(body.gameObject, "Weapon");
            if (!_weapon2) _weapon2 = EntityStateMachine.FindByCustomName(body.gameObject, "Weapon2");
            return (_weapon && _weapon.state != null && !(_weapon.state is Idle))
                || (_weapon2 && _weapon2.state != null && !(_weapon2.state is Idle));
        }

        private void LateUpdate()
        {
            float target = Casting() ? LodestarModel.RingCastBoost.Value : 1f;
            _boost = Mathf.MoveTowards(_boost, target, Time.deltaTime * 6f);
            float spin = LodestarModel.RingSpin.Value * _boost * Time.deltaTime;
            float t = Time.time;
            for (int i = 0; i < _spins.Count; i++)
            {
                Transform s = _spins[i];
                if (!s) continue;
                s.Rotate(0f, _speeds[i] * spin, 0f, Space.Self);
                if (!_isRing[i]) s.localPosition = new Vector3(0f, Mathf.Sin(t * 3f + i) * 0.015f, 0f);
            }
        }
    }
}
