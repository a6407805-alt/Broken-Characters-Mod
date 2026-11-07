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

        /// <summary>Builds the monster on this Imp Overlord model. Returns the jaw hinge (or null), and false if nothing was built.</summary>
        public static bool Apply(Transform model, out Transform jaw)
        {
            jaw = null;
            if (!model || !Load()) return false;
            if (!_mat) _mat = SvrModel.PaletteMaterial(null, _tex, _emTex);
            if (!_mat) return false;

            Renderer[] originals = model.GetComponentsInChildren<Renderer>(true);

            // bind pose of every skinned bone (the rest pose, whatever frame the body spawned in)
            Dictionary<Transform, Matrix4x4> bind = new Dictionary<Transform, Matrix4x4>();
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.sharedMesh || smr.bones == null) continue;
                Matrix4x4[] bp = smr.sharedMesh.bindposes;
                Matrix4x4 rootM = smr.transform.localToWorldMatrix;
                for (int i = 0; i < smr.bones.Length && i < bp.Length; i++)
                    if (smr.bones[i] && !bind.ContainsKey(smr.bones[i])) bind[smr.bones[i]] = rootM * bp[i].inverse;
            }
            Matrix4x4 toLocal = model.worldToLocalMatrix;
            Func<Transform, Matrix4x4> BM = t => bind.TryGetValue(t, out Matrix4x4 m) ? m : t.localToWorldMatrix;
            Func<Transform, Vector3> LP = t => toLocal.MultiplyPoint3x4(BM(t).GetColumn(3));     // bone position in model space

            List<Transform> bones = new List<Transform>(bind.Keys);
            if (bones.Count == 0) foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) bones.Add(t);

            Dictionary<string, Transform> game = new Dictionary<string, Transform>();
            foreach (var kv in Candidates)
            {
                Transform t = Find(bones, kv.Value);
                if (t) game[kv.Key] = t;
            }
            if (!_logged)
            {
                _logged = true;
                StringBuilder sb = new StringBuilder("Stalker bone map: ");
                foreach (var kv in Candidates) sb.Append(kv.Key).Append("->").Append(game.TryGetValue(kv.Key, out Transform t) ? t.name : "-").Append("  ");
                SvrPlugin.Log.LogInfo(sb.ToString());
                if (game.Count < 12)
                {
                    StringBuilder nb = new StringBuilder("Imp Overlord bones: ");
                    foreach (Transform t in bones) nb.Append(t.name).Append(", ");
                    SvrPlugin.Log.LogInfo(nb.ToString());
                }
            }

            // size: our height (ground to head) over the skeleton's
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Transform t in bones) { float y = LP(t).y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
            float gameHead = game.TryGetValue("head", out Transform gh) ? LP(gh).y : maxY;
            float ourHead = Bones.TryGetValue("head", out BoneInfo oh) ? oh.Head.y : 2.6f;
            float ground = Mathf.Min(minY, 0f);
            float s = Mathf.Clamp((gameHead - ground) / Mathf.Max(0.1f, ourHead), 0.2f, 20f);
            Vector3 centre = game.TryGetValue("hips", out Transform ghip) ? LP(ghip) : Vector3.zero;
            Vector3 offset = new Vector3(centre.x, ground, centre.z) - (Bones.TryGetValue("hips", out BoneInfo ohip) ? new Vector3(ohip.Head.x, 0f, ohip.Head.z) * s : Vector3.zero);
            Func<Vector3, Vector3> Fit = p => p * s + offset;     // our space -> model space (before per-bone fixes)

            int layer = model.gameObject.layer;
            int built = 0;
            foreach (PartData part in _parts)
            {
                string boneName = part.Bone, tag = null;
                int at = boneName.IndexOf('@');
                if (at > 0) { tag = boneName.Substring(at + 1); boneName = boneName.Substring(0, at); }
                Bones.TryGetValue(boneName, out BoneInfo ob);
                Vector3 ourJoint = ob != null ? Fit(ob.Head) : Fit(Centroid(part));

                Transform gb;
                if (!game.TryGetValue(boneName, out gb)) gb = Nearest(bones, LP, ourJoint);
                if (!gb) gb = model;
                Vector3 gameJoint = gb == model ? ourJoint : LP(gb);

                // limbs: turn the piece to follow the game's bone direction; everything: move its joint onto the bone
                Quaternion rot = Quaternion.identity;
                if (ob != null && ChildOf.TryGetValue(boneName, out string child) && game.TryGetValue(child, out Transform gc) && Bones.TryGetValue(child, out BoneInfo oc))
                {
                    Vector3 ourDir = (oc.Head - ob.Head).normalized;
                    Vector3 gameDir = (LP(gc) - gameJoint).normalized;
                    if (ourDir.sqrMagnitude > 0.5f && gameDir.sqrMagnitude > 0.5f) rot = Quaternion.FromToRotation(ourDir, gameDir);
                }
                bool torso = boneName == "hips" || boneName == "spine" || boneName == "chest" || boneName == "neck" || boneName == "head";
                if (torso) gameJoint = ourJoint;                   // the trunk keeps its own hunched shape

                Matrix4x4 place = Matrix4x4.TRS(gameJoint, rot, Vector3.one) * Matrix4x4.TRS(-ourJoint, Quaternion.identity, Vector3.one);
                Transform parent = gb;
                GameObject holder = new GameObject("StalkerPart_" + part.Bone) { layer = layer };
                Matrix4x4 local = BM(parent).inverse * model.localToWorldMatrix;
                holder.transform.SetParent(parent, false);
                holder.transform.localPosition = local.GetColumn(3);
                holder.transform.localRotation = local.rotation;
                holder.transform.localScale = local.lossyScale;

                Transform meshParent = holder.transform;
                Vector3 pivot = Vector3.zero;
                if (tag != null && Bones.TryGetValue(part.Bone, out BoneInfo pb))
                {
                    // a hinge at the pivot (the jaw) the Fx can turn
                    pivot = place.MultiplyPoint3x4(Fit(pb.Head));
                    GameObject hinge = new GameObject("StalkerHinge_" + tag) { layer = layer };
                    hinge.transform.SetParent(holder.transform, false);
                    hinge.transform.localPosition = pivot;
                    meshParent = hinge.transform;
                    if (tag == "jaw") jaw = hinge.transform;
                }
                AddMesh(meshParent, part, p => place.MultiplyPoint3x4(Fit(p)) - pivot, rot, layer);
                built++;
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
                    foreach (MeshRenderer mr in model.GetComponentsInChildren<MeshRenderer>(true))
                        if (mr && mr.name.StartsWith("StalkerMesh"))
                            list.Add(new CharacterModel.RendererInfo { renderer = mr, defaultMaterial = mr.sharedMaterial, defaultShadowCastingMode = ShadowCastingMode.On, ignoreOverlays = false });
                    cm.baseRendererInfos = list.ToArray();
                }
            }
            catch { }
            SvrPlugin.Log.LogInfo($"Shimmer Stalker model placed: {built} parts, scale {s:0.##}.");
            return built > 0;
        }

        private static void AddMesh(Transform parent, PartData part, Func<Vector3, Vector3> xf, Quaternion rot, int layer)
        {
            int n = part.Pos.Length;
            Vector3[] v = new Vector3[n];
            Vector3[] nr = new Vector3[n];
            Vector2[] uv = new Vector2[n];
            int[] tris = new int[n];
            float w = Mathf.Max(1, _palColor.Length);
            for (int i = 0; i < n; i++)
            {
                v[i] = xf(part.Pos[i]);
                nr[i] = rot * part.Nrm[i];
                uv[i] = new Vector2((part.Pal[i] + 0.5f) / w, 0.5f);
                int k = i % 3;
                tris[i] = k == 1 ? i + 1 : (k == 2 ? i - 1 : i);
            }
            Mesh mesh = new Mesh { name = "Stalker_" + part.Bone };
            if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = nr;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            GameObject go = new GameObject("StalkerMesh_" + part.Bone) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
        }

        private static Vector3 Centroid(PartData p)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < p.Pos.Length; i++) c += p.Pos[i];
            return p.Pos.Length > 0 ? c / p.Pos.Length : Vector3.zero;
        }

        private static Transform Find(List<Transform> all, string[] names)
        {
            foreach (string n in names)
                foreach (Transform t in all)
                    if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)) return t;
            foreach (string n in names)
                foreach (Transform t in all)
                {
                    string tn = t.name.ToLowerInvariant();
                    if (tn.EndsWith(n) && !tn.Contains("end") && !tn.Contains("ik") && !tn.Contains("twist")) return t;
                }
            return null;
        }

        private static Transform Nearest(List<Transform> bones, Func<Transform, Vector3> lp, Vector3 p)
        {
            Transform best = null;
            float bd = float.MaxValue;
            foreach (Transform t in bones)
            {
                float d = (lp(t) - p).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }
    }
}
