using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace Surveyor
{
    /// <summary>
    /// Puts the Blender-made Shimmer Stalker (stalker_model.bin, built by Model/blender_stalker.py) on the Imp Overlord
    /// skeleton. The model is sized to the skeleton, each piece rides the matching bone (limbs are turned to follow the
    /// game's bones), anything that can't be matched rides the nearest bone, and the jaw gets its own hinge.
    /// If the file is missing, the darkened Imp Overlord stays.
    /// </summary>
    public static class SvrStalkerModel
    {
        public const string FileName = "stalker_model.bin";

        private class BoneInfo { public string Name; public Vector3 Head, Tail; }
        private class PartData { public string Bone; public Vector3[] Pos, Nrm; public int[] Pal; }

        private static bool _tried, _logged;
        private static readonly Dictionary<string, BoneInfo> Bones = new Dictionary<string, BoneInfo>();
        private static List<PartData> _parts;
        private static Color[] _palColor;
        private static float[] _palEmit;
        private static Texture2D _tex, _emTex;
        private static Material _mat;

        private static readonly Dictionary<string, string[]> Candidates = new Dictionary<string, string[]>
        {
            { "hips", new[] { "pelvis", "hips", "hip" } },
            { "spine", new[] { "stomach", "spine", "spine1", "abdomen", "spine.001" } },
            { "chest", new[] { "chest", "spine2", "upperchest", "spine.002" } },
            { "neck", new[] { "neck" } },
            { "head", new[] { "head" } },
            { "upper_arm.R", new[] { "upperarm.r", "upper_arm.r", "upperarm_r", "upper_arm_r", "arm.r" } },
            { "forearm.R", new[] { "lowerarm.r", "lower_arm.r", "forearm.r", "lowerarm_r", "lower_arm_r" } },
            { "hand.R", new[] { "hand.r", "hand_r" } },
            { "upper_arm.L", new[] { "upperarm.l", "upper_arm.l", "upperarm_l", "upper_arm_l", "arm.l" } },
            { "forearm.L", new[] { "lowerarm.l", "lower_arm.l", "forearm.l", "lowerarm_l", "lower_arm_l" } },
            { "hand.L", new[] { "hand.l", "hand_l" } },
            { "thigh.R", new[] { "thigh.r", "upperleg.r", "upper_leg.r", "thigh_r" } },
            { "shin.R", new[] { "calf.r", "shin.r", "lowerleg.r", "lower_leg.r", "calf_r" } },
            { "foot.R", new[] { "foot.r", "foot_r" } },
            { "thigh.L", new[] { "thigh.l", "upperleg.l", "upper_leg.l", "thigh_l" } },
            { "shin.L", new[] { "calf.l", "shin.l", "lowerleg.l", "lower_leg.l", "calf_l" } },
            { "foot.L", new[] { "foot.l", "foot_l" } },
        };

        private static readonly Dictionary<string, string> ChildOf = new Dictionary<string, string>
        {
            { "upper_arm.R", "forearm.R" }, { "forearm.R", "hand.R" },
            { "upper_arm.L", "forearm.L" }, { "forearm.L", "hand.L" },
            { "thigh.R", "shin.R" }, { "shin.R", "foot.R" },
            { "thigh.L", "shin.L" }, { "shin.L", "foot.L" },
        };

        private static bool Load()
        {
            if (_tried) return _parts != null;
            _tried = true;
            string path = SvrModel.FindFile(FileName);
            if (path == null) { SvrPlugin.Log.LogInfo("No " + FileName + " found; the Stalker keeps the Imp Overlord body."); return false; }
            try
            {
                using (BinaryReader br = new BinaryReader(SvrModel.OpenData(path)))
                {
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "DUEL") throw new Exception("not a model file");
                    br.ReadInt32();
                    int nb = br.ReadInt32();
                    for (int i = 0; i < nb; i++)
                    {
                        BoneInfo b = new BoneInfo { Name = Str(br), Head = V3(br), Tail = V3(br) };
                        Bones[b.Name] = b;
                    }
                    int np = br.ReadInt32();
                    _palColor = new Color[np];
                    _palEmit = new float[np];
                    for (int i = 0; i < np; i++)
                    {
                        _palColor[i] = new Color(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), 1f);
                        _palEmit[i] = br.ReadSingle();
                    }
                    int nparts = br.ReadInt32();
                    _parts = new List<PartData>(nparts);
                    for (int i = 0; i < nparts; i++)
                    {
                        PartData p = new PartData { Bone = Str(br) };
                        int n = br.ReadInt32();
                        p.Pos = new Vector3[n]; p.Nrm = new Vector3[n]; p.Pal = new int[n];
                        for (int j = 0; j < n; j++) { p.Pos[j] = V3(br); p.Nrm[j] = V3(br); p.Pal[j] = br.ReadInt32(); }
                        _parts.Add(p);
                    }
                }
                int w = Mathf.Max(1, _palColor.Length);
                _tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "StalkerPalette" };
                _emTex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "StalkerPaletteEm" };
                for (int i = 0; i < _palColor.Length; i++)
                {
                    _tex.SetPixel(i, 0, _palColor[i]);
                    _emTex.SetPixel(i, 0, _palEmit[i] > 0f ? _palColor[i] * Mathf.Min(2f, _palEmit[i] * 0.6f) : Color.black);
                }
                _tex.Apply(false, true);
                _emTex.Apply(false, true);
                SvrPlugin.Log.LogInfo($"Loaded the Shimmer Stalker model: {_parts.Count} parts.");
                return true;
            }
            catch (Exception e)
            {
                SvrPlugin.Log.LogError("Couldn't read " + path + ": " + e);
                _parts = null;
                return false;
            }
        }

        private static string Str(BinaryReader br) => Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
        private static Vector3 V3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        /// <summary>Everything the rig needs from the file.</summary>
        public static bool Get(out Dictionary<string, Vector3[]> bones, out List<KeyValuePair<string, Mesh>> meshes, out Material mat, out float height)
        {
            bones = null; meshes = null; mat = null; height = 3f;
            if (!Load()) return false;
            if (!_mat) _mat = SvrModel.PaletteMaterial(null, _tex, _emTex);
            if (!_mat) return false;
            mat = _mat;
            bones = new Dictionary<string, Vector3[]>();
            foreach (var kv in Bones) bones[kv.Key] = new[] { kv.Value.Head, kv.Value.Tail };
            meshes = new List<KeyValuePair<string, Mesh>>();
            float top = 0f;
            foreach (PartData part in _parts)
            {
                string bone = part.Bone == "head@jaw" ? "jaw" : part.Bone;
                Vector3 origin = part.Bone == "head@jaw" && Bones.TryGetValue("head@jaw", out BoneInfo jb) ? jb.Head
                    : (Bones.TryGetValue(bone, out BoneInfo ob) ? ob.Head : Vector3.zero);
                meshes.Add(new KeyValuePair<string, Mesh>(bone, BuildMesh(part, origin)));
                for (int i = 0; i < part.Pos.Length; i++) top = Mathf.Max(top, part.Pos[i].y);
            }
            height = Mathf.Max(1f, top);
            return true;
        }

        private static Mesh BuildMesh(PartData part, Vector3 origin)
        {
            int n = part.Pos.Length;
            Vector3[] v = new Vector3[n];
            Vector2[] uv = new Vector2[n];
            int[] tris = new int[n];
            float w = Mathf.Max(1, _palColor.Length);
            for (int i = 0; i < n; i++)
            {
                v[i] = part.Pos[i] - origin;
                uv[i] = new Vector2((part.Pal[i] + 0.5f) / w, 0.5f);
                int k = i % 3;
                tris[i] = k == 1 ? i + 1 : (k == 2 ? i - 1 : i);
            }
            Mesh mesh = new Mesh { name = "Stalker_" + part.Bone };
            if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = part.Nrm;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

    }
}
