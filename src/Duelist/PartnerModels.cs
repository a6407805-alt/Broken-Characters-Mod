using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Path = System.IO.Path;

namespace DuelistMod
{
    /// <summary>
    /// The partners' Blender-made models (partner_&lt;Name&gt;.bin, written by Blender/build_partners.py).
    /// Each file holds named parts with pivots (body, wingL, wingR, Gear…, Hands, Halo, Orbit…) so
    /// PartnerVisual can animate them. Missing file = the old primitive look.
    /// </summary>
    public static class PartnerModels
    {
        private class Part { public string Name; public Vector3 Pivot; public Vector3[] Pos, Nrm; public int[] Pal; public Mesh Mesh; }
        private class Data { public readonly List<Part> Parts = new List<Part>(); public Material Mat; }

        private static readonly Dictionary<string, Data> Cache = new Dictionary<string, Data>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        public static bool TryBuild(PartnerKind kind, Transform parent, out Dictionary<string, Transform> parts)
            => TryBuildFile($"partner_{kind}.bin", parent, out parts);

        /// <summary>Builds any parts model file (partner_*.bin, extra_*.bin) under parent, one child per named part.</summary>
        public static bool TryBuildFile(string file, Transform parent, out Dictionary<string, Transform> parts)
        {
            parts = null;
            Data d = Load(file);
            if (d == null) return false;
            parts = new Dictionary<string, Transform>();
            int layer = parent.gameObject.layer;
            foreach (Part p in d.Parts)
            {
                GameObject go = new GameObject(p.Name) { layer = layer };
                go.transform.SetParent(parent, false);
                go.transform.localPosition = p.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = p.Mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = d.Mat;
                mr.shadowCastingMode = ShadowCastingMode.On;
                parts[p.Name] = go.transform;
            }
            return true;
        }

        private static Data Load(string file)
        {
            string kind = Path.GetFileNameWithoutExtension(file);
            if (Cache.TryGetValue(file, out Data cached)) return cached;
            if (Missing.Contains(file)) return null;
            string path = DuelistModel.FindFile(file);
            if (path == null) { Missing.Add(file); DuelistPlugin.Log.LogInfo($"No {file}: using the fallback look."); return null; }
            try
            {
                Data d = new Data();
                Color[] pal;
                float[] emit;
                Dictionary<string, Vector3> pivots = new Dictionary<string, Vector3>();
                using (BinaryReader br = new BinaryReader(DuelistModel.OpenData(path)))
                {
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "DUEL") throw new Exception("not a Duelist model file");
                    br.ReadInt32();
                    int nb = br.ReadInt32();
                    for (int i = 0; i < nb; i++)
                    {
                        string name = Str(br);
                        Vector3 head = V3(br);
                        V3(br);
                        pivots[name] = head;
                    }
                    int np = br.ReadInt32();
                    pal = new Color[np];
                    emit = new float[np];
                    for (int i = 0; i < np; i++)
                    {
                        pal[i] = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1f);
                        emit[i] = br.ReadSingle();
                    }
                    int nparts = br.ReadInt32();
                    for (int i = 0; i < nparts; i++)
                    {
                        Part p = new Part { Name = Str(br) };
                        int n = br.ReadInt32();
                        p.Pos = new Vector3[n];
                        p.Nrm = new Vector3[n];
                        p.Pal = new int[n];
                        for (int j = 0; j < n; j++) { p.Pos[j] = V3(br); p.Nrm[j] = V3(br); p.Pal[j] = br.ReadInt32(); }
                        p.Pivot = pivots.TryGetValue(p.Name, out Vector3 pv) ? pv : Vector3.zero;
                        d.Parts.Add(p);
                    }
                }

                int w = Mathf.Max(1, pal.Length);
                Texture2D tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PartnerPalette" };
                Texture2D em = new Texture2D(w, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "PartnerPaletteEm" };
                for (int i = 0; i < pal.Length; i++)
                {
                    tex.SetPixel(i, 0, pal[i]);
                    em.SetPixel(i, 0, emit[i] > 0f ? pal[i] * Mathf.Clamp(emit[i] * 0.5f, 0.5f, 2f) : Color.black);
                }
                tex.Apply(false, true);
                em.Apply(false, true);
                d.Mat = DuelistModel.PaletteMaterial(null, tex, em);

                foreach (Part p in d.Parts)
                {
                    int n = p.Pos.Length;
                    Vector3[] v = new Vector3[n];
                    Vector2[] uv = new Vector2[n];
                    int[] tris = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        v[i] = p.Pos[i] - p.Pivot;
                        uv[i] = new Vector2((p.Pal[i] + 0.5f) / w, 0.5f);
                        tris[i] = i;
                    }
                    Mesh mesh = new Mesh { name = $"{kind}_{p.Name}" };
                    if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.vertices = v;
                    mesh.normals = p.Nrm;
                    mesh.uv = uv;
                    mesh.triangles = tris;
                    mesh.RecalculateBounds();
                    p.Mesh = mesh;
                    p.Pos = null; p.Nrm = null; p.Pal = null;
                }
                Cache[file] = d;
                DuelistPlugin.Log.LogInfo($"Loaded model {kind}: {d.Parts.Count} parts.");
                return d;
            }
            catch (Exception e)
            {
                Missing.Add(file);
                DuelistPlugin.Log.LogError($"Couldn't read {path}: {e}");
                return null;
            }
        }

        private static string Str(BinaryReader br) => Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
        private static Vector3 V3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    }

    /// <summary>
    /// Models for the Extra Deck toolkit monsters (extra_*.bin, written by Blender/build_extras.py):
    /// Scrap Chimera (Fusion), Tuned Colossus (Synchro), Overlay Sentinel (XYZ), Link Arbiter (Link).
    /// </summary>
    public static class ExtraModels
    {
        public static string FileFor(Card c)
        {
            if (c == null || c.Type != CardType.Extra || !c.Generic) return null;
            switch (c.Extra)
            {
                case ExtraKind.Synchro: return "extra_TunedColossus.bin";
                case ExtraKind.Xyz: return "extra_OverlaySentinel.bin";
                case ExtraKind.Link: return "extra_LinkArbiter.bin";
                default: return "extra_ScrapChimera.bin";
            }
        }

        /// <summary>Puts the monster's model on its zone (about 2.7 tall), animated. False = no model file.</summary>
        public static bool TryBuild(Card c, Transform parent, float height = 2.7f)
        {
            string file = FileFor(c);
            if (file == null) return false;
            GameObject holder = new GameObject("ExtraModel") { layer = parent.gameObject.layer };
            holder.transform.SetParent(parent, false);
            if (!PartnerModels.TryBuildFile(file, holder.transform, out Dictionary<string, Transform> parts))
            {
                UnityEngine.Object.Destroy(holder);
                return false;
            }
            holder.transform.localScale = Vector3.one * (height / 3.2f);
            holder.AddComponent<ExtraModelAnim>().Parts = parts;
            return true;
        }
    }

    /// <summary>Turns the SpinX/SpinY/SpinZ parts and bobs the Float parts of an Extra Deck model.</summary>
    public class ExtraModelAnim : MonoBehaviour
    {
        public Dictionary<string, Transform> Parts;
        private readonly Dictionary<Transform, Vector3> _base = new Dictionary<Transform, Vector3>();

        private void Update()
        {
            if (Parts == null) return;
            float dt = Time.deltaTime, t = Time.time;
            int k = 0;
            foreach (KeyValuePair<string, Transform> kv in Parts)
            {
                Transform tr = kv.Value;
                if (!tr) continue;
                string n = kv.Key;
                float dir = (k++ % 2 == 0) ? 1f : -1f;
                if (n.StartsWith("SpinY")) tr.Rotate(Vector3.up, 45f * dir * dt, Space.Self);
                else if (n.StartsWith("SpinX")) tr.Rotate(Vector3.right, 220f * dt, Space.Self);
                else if (n.StartsWith("SpinZ")) tr.Rotate(Vector3.forward, 28f * dt, Space.Self);
                else if (n.StartsWith("Float"))
                {
                    if (!_base.TryGetValue(tr, out Vector3 b)) _base[tr] = b = tr.localPosition;
                    tr.localPosition = b + Vector3.up * Mathf.Sin(t * 2.1f) * 0.07f;
                }
            }
        }
    }
}
