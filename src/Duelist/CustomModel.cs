using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
using Path = System.IO.Path;

namespace DuelistMod
{
    /// <summary>
    /// Loads the Blender-made Duelist (duelist_model.bin, written by Blender/export_ror2.py) and puts it on
    /// the Commando skeleton: each body part is a rigid mesh parented to the matching Commando bone, so the
    /// Duelist plays all of Commando's animations. The original Commando renderers are hidden.
    /// No Unity editor or asset bundle needed. If the file is missing, the Commando model stays.
    /// </summary>
    public static class DuelistModel
    {
        public const string FileName = "duelist_model.bin";

        private class BoneInfo { public string Name; public Vector3 Head, Tail; }
        private class PartData { public string Bone; public Vector3[] Pos, Nrm; public int[] Pal; }

        private static bool _tried;
        private static bool _loggedBones;
        private static bool _loggedPlacement;
        private static bool _logLimbs = true;
        private static readonly Dictionary<string, BoneInfo> Bones = new Dictionary<string, BoneInfo>();
        private static List<PartData> _parts;
        private static Color[] _palColor;
        private static float[] _palEmit;
        private static Texture2D _tex, _emTex;

        // our bone -> Commando bone names to try (exact first, then "contains")
        private static readonly Dictionary<string, string[]> Candidates = new Dictionary<string, string[]>
        {
            { "hips", new[] { "pelvis", "base", "hips", "hip" } },
            { "spine", new[] { "stomach", "spine", "spine1", "abdomen" } },
            { "chest", new[] { "chest", "spine2", "upperchest" } },
            { "neck", new[] { "neck" } },
            { "head", new[] { "head" } },
            { "upper_arm.R", new[] { "upper_arm.r", "upperarm.r", "upperarm_r", "upper_arm_r" } },
            { "forearm.R", new[] { "lower_arm.r", "forearm.r", "lowerarm.r", "lower_arm_r" } },
            { "hand.R", new[] { "hand.r", "hand_r" } },
            { "upper_arm.L", new[] { "upper_arm.l", "upperarm.l", "upperarm_l", "upper_arm_l" } },
            { "forearm.L", new[] { "lower_arm.l", "forearm.l", "lowerarm.l", "lower_arm_l" } },
            { "hand.L", new[] { "hand.l", "hand_l" } },
            { "thigh.R", new[] { "thigh.r", "upper_leg.r", "upperleg.r", "thigh_r" } },
            { "shin.R", new[] { "calf.r", "shin.r", "lower_leg.r", "lowerleg.r", "calf_r" } },
            { "foot.R", new[] { "foot.r", "foot_r" } },
            { "thigh.L", new[] { "thigh.l", "upper_leg.l", "upperleg.l", "thigh_l" } },
            { "shin.L", new[] { "calf.l", "shin.l", "lower_leg.l", "lowerleg.l", "calf_l" } },
            { "foot.L", new[] { "foot.l", "foot_l" } },
        };

        // which of our bones each bone points at (for the bone's direction)
        private static readonly Dictionary<string, string> ChildOf = new Dictionary<string, string>
        {
            { "hips", "spine" }, { "spine", "chest" }, { "chest", "neck" }, { "neck", "head" },
            { "upper_arm.R", "forearm.R" }, { "forearm.R", "hand.R" },
            { "upper_arm.L", "forearm.L" }, { "forearm.L", "hand.L" },
            { "thigh.R", "shin.R" }, { "shin.R", "foot.R" },
            { "thigh.L", "shin.L" }, { "shin.L", "foot.L" },
        };

        // ------------------------------------------------------------------ loading

        private static string FindFile() => FindFile(FileName);

        /// <summary>A data file next to the dll, or up to a few folders above it.</summary>
        public static string FindFile(string fileName)
        {
            string dir = Path.GetDirectoryName(typeof(DuelistModel).Assembly.Location);
            for (int up = 0; up < 5 && !string.IsNullOrEmpty(dir); up++)
            {
                string p = Path.Combine(dir, fileName);
                if (File.Exists(p)) return p;
                if (File.Exists(p + ".gz")) return p + ".gz";      // compressed copy (release builds)
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        public static bool Load()
        {
            if (_tried) return _parts != null;
            _tried = true;
            string path = FindFile();
            if (path == null)
            {
                DuelistPlugin.Log.LogInfo($"No {FileName} found near the plugin; using the Commando model.");
                return false;
            }
            try
            {
                using (BinaryReader br = new BinaryReader(OpenData(path)))
                {
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "DUEL") throw new Exception("not a Duelist model file");
                    br.ReadInt32(); // version
                    int nb = br.ReadInt32();
                    for (int i = 0; i < nb; i++)
                    {
                        BoneInfo b = new BoneInfo { Name = ReadStr(br), Head = ReadV3(br), Tail = ReadV3(br) };
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
                        PartData p = new PartData { Bone = ReadStr(br) };
                        int n = br.ReadInt32();
                        p.Pos = new Vector3[n];
                        p.Nrm = new Vector3[n];
                        p.Pal = new int[n];
                        for (int j = 0; j < n; j++)
                        {
                            p.Pos[j] = ReadV3(br);
                            p.Nrm[j] = ReadV3(br);
                            p.Pal[j] = br.ReadInt32();
                        }
                        _parts.Add(p);
                    }
                }
                BuildTextures();
                DuelistPlugin.Log.LogInfo($"Loaded custom Duelist model: {_parts.Count} parts, {_palColor.Length} colours ({path}).");
                return true;
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogError("Couldn't read " + path + ": " + e);
                _parts = null;
                return false;
            }
        }

        /// <summary>Opens a model file; .gz files are decompressed on the fly.</summary>
        public static Stream OpenData(string path)
        {
            Stream fs = File.OpenRead(path);
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                return new BufferedStream(new GZipStream(fs, CompressionMode.Decompress), 1 << 16);
            return new BufferedStream(fs, 1 << 16);
        }

        private static string ReadStr(BinaryReader br) => Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
        private static Vector3 ReadV3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

        private static void BuildTextures()
        {
            int n = Mathf.Max(1, _palColor.Length);
            _tex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DuelistPalette" };
            _emTex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "DuelistPaletteEm" };
            for (int i = 0; i < _palColor.Length; i++)
            {
                _tex.SetPixel(i, 0, _palColor[i]);
                _emTex.SetPixel(i, 0, _palEmit[i] > 0f ? _palColor[i] : Color.black);
            }
            _tex.Apply(false, true);
            _emTex.Apply(false, true);
        }

        // ------------------------------------------------------------------ applying

        public static void Apply(Transform model)
        {
            if (!model || !DuelConfig.UseCustomModel.Value || !Load()) return;

            Renderer[] originals = model.GetComponentsInChildren<Renderer>(true);
            int layer = model.gameObject.layer;
            foreach (Renderer r in originals)
                if (r is SkinnedMeshRenderer) { layer = r.gameObject.layer; break; }
            Material baseMat = StandardMaterial(model);
            Material mat = MakeMaterial(baseMat);
            Material outline = DuelConfig.ModelOutline.Value ? MakeOutline(baseMat) : null;

            // map our bones onto the game skeleton
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            Dictionary<string, Transform> game = new Dictionary<string, Transform>();
            foreach (KeyValuePair<string, string[]> kv in Candidates)
            {
                Transform t = FindBone(all, kv.Value);
                if (t) game[kv.Key] = t;
            }
            if (!_loggedBones)
            {
                _loggedBones = true;
                StringBuilder sb = new StringBuilder("Duelist model bone map: ");
                foreach (KeyValuePair<string, string[]> kv in Candidates)
                    sb.Append(kv.Key).Append("->").Append(game.TryGetValue(kv.Key, out Transform t) ? t.name : "(none)").Append("  ");
                DuelistPlugin.Log.LogInfo(sb.ToString());
                if (game.Count < 10)
                {
                    StringBuilder names = new StringBuilder("Model transforms: ");
                    foreach (Transform t in all) names.Append(t.name).Append(", ");
                    DuelistPlugin.Log.LogInfo(names.ToString());
                }
            }

            BuildBindPose(model);

            float scale = 1f;
            if (game.TryGetValue("head", out Transform gh) && game.TryGetValue("hips", out Transform gp)
                && Bones.TryGetValue("head", out BoneInfo oh) && Bones.TryGetValue("hips", out BoneInfo op))
            {
                float ours = Vector3.Distance(oh.Head, op.Head);
                float theirs = Vector3.Distance(BP(gh), BP(gp));
                if (ours > 0.01f && theirs > 0.01f) scale = Mathf.Clamp(theirs / ours, 0.3f, 3f);
            }
            // Commando's shoulders sit relatively higher than the Duelist's: size the body so the shoulders line up
            if (game.TryGetValue("hips", out Transform sh0) && Bones.TryGetValue("hips", out BoneInfo oh0)
                && game.TryGetValue("upper_arm.R", out Transform shR) && game.TryGetValue("upper_arm.L", out Transform shL)
                && Bones.TryGetValue("upper_arm.R", out BoneInfo oR) && Bones.TryGetValue("upper_arm.L", out BoneInfo oL))
            {
                float theirs = ((BP(shR) + BP(shL)) * 0.5f - BP(sh0)).magnitude;
                float ours = ((oR.Head + oL.Head) * 0.5f - oh0.Head).magnitude;
                if (ours > 0.01f && theirs > 0.01f) scale = Mathf.Clamp(theirs / ours, 0.3f, 3f);
            }
            scale *= DuelConfig.ModelScale.Value;

            // torso + head are placed as one rigid piece from the hips, so the head sits on the Duelist's own neck
            // (Commando's neck is a different length); each piece still follows its own bone afterwards
            Quaternion bodyRot = Quaternion.LookRotation(model.forward, model.up);
            Transform gHips = null;
            BoneInfo oHips = null;
            bool haveHips = game.TryGetValue("hips", out gHips) && Bones.TryGetValue("hips", out oHips);

            foreach (PartData part in _parts)
            {
                Transform parent = model;
                Vector3 origin = Vector3.zero;
                Vector3 stretchDir = Vector3.zero;
                float stretch = 1f;
                Quaternion rot = bodyRot;
                Vector3 anchor = model.position;

                if (haveHips && TorsoBones.Contains(part.Bone))
                {
                    origin = oHips.Head;
                    anchor = BP(gHips);
                    parent = game.TryGetValue(part.Bone, out Transform tb) ? tb : gHips;
                }
                else if (game.TryGetValue(part.Bone, out Transform gb) && Bones.TryGetValue(part.Bone, out BoneInfo ob)
                    && GameDir(part.Bone, gb, game, model, out Vector3 gdir))
                {
                    Vector3 odir = ob.Tail - ob.Head;
                    if (odir.sqrMagnitude > 1e-6f)
                    {
                        odir.Normalize();
                        Quaternion ours = Quaternion.LookRotation(odir, RefAxis(part.Bone, odir, Vector3.forward, Vector3.up, Vector3.right));
                        Quaternion theirs = Quaternion.LookRotation(gdir, RefAxis(part.Bone, gdir, model.forward, model.up, model.right));
                        rot = theirs * Quaternion.Inverse(ours);
                        parent = gb;
                        origin = ob.Head;
                        anchor = BP(gb);
                        // stretch the piece along its bone so it reaches the next bone (Commando's limbs are longer)
                        if (ChildOf.TryGetValue(part.Bone, out string childName) && game.TryGetValue(childName, out Transform gChild)
                            && Bones.TryGetValue(childName, out BoneInfo oChild))
                        {
                            float gameLen = (BP(gChild) - BP(gb)).magnitude;
                            float ourLen = (oChild.Head - ob.Head).magnitude * scale;
                            if (ourLen > 0.001f) stretch = Mathf.Clamp(gameLen / ourLen, 0.5f, 3f);
                            stretchDir = odir;
                        }
                    }
                }

                if (_logLimbs && parent != model)
                {
                    Vector3 ob2 = Bones.TryGetValue(part.Bone, out BoneInfo bi) ? (bi.Tail - bi.Head).normalized : Vector3.zero;
                    DuelistPlugin.Log.LogInfo($"  part {part.Bone} -> {parent.name}: anchor {anchor} (bone now {parent.position}), " +
                        $"our dir {ob2} becomes {(rot * ob2)}, stretch {stretch:0.00}, torso {TorsoBones.Contains(part.Bone)}");
                }
                // placement is worked out in the skeleton's bind pose (always the same), not whatever
                // animation frame the model spawned in, then stored relative to the bone
                GameObject go = new GameObject("DuelistPart_" + part.Bone) { layer = layer };
                Matrix4x4 local = BM(parent).inverse * Matrix4x4.TRS(anchor, rot, Vector3.one * scale);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local.GetColumn(3);
                go.transform.localRotation = local.rotation;
                go.transform.localScale = local.lossyScale;
                AddMesh(go, part, origin, 0f, mat, ShadowCastingMode.On, false, stretchDir, stretch);
                if (outline)
                {
                    GameObject og = new GameObject("Outline") { layer = layer };
                    og.transform.SetParent(go.transform, false);
                    AddMesh(og, part, origin, 0.0045f, outline, ShadowCastingMode.Off, true, stretchDir, stretch);
                }
            }

            foreach (Renderer r in originals)       // hide Commando (item displays added later still show)
                if (r) r.forceRenderingOff = true;

            _logLimbs = false;
            if (!_loggedPlacement)
            {
                _loggedPlacement = true;
                Bounds b = new Bounds(model.position, Vector3.zero);
                int count = 0;
                foreach (MeshRenderer mr in model.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.name.StartsWith("DuelistPart_")) { b.Encapsulate(mr.bounds); count++; }
                DuelistPlugin.Log.LogInfo($"Duelist model placed: {count} parts, scale {scale:0.###}, model at {model.position}, " +
                    $"parts span {b.size} centred {b.center}, shader {(mat.shader ? mat.shader.name : "none")}, outline {(outline ? "on" : "off")}.");
            }
        }

        private static Material _standard;

        /// <summary>
        /// A material using Hopoo's normal character shader (Deferred/Standard). The first renderer on a model can be
        /// an effect (distortion, glow), which would make the Duelist see-through, so only accept the standard shader.
        /// </summary>
        private static Material StandardMaterial(Transform model)
        {
            if (_standard) return _standard;
            Material found = null;
            CharacterModel cm = model.GetComponent<CharacterModel>();
            if (cm && cm.baseRendererInfos != null)
                foreach (CharacterModel.RendererInfo ri in cm.baseRendererInfos)
                    if (IsStandard(ri.defaultMaterial)) { found = ri.defaultMaterial; break; }
            if (!found)
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    if (IsStandard(r.sharedMaterial)) { found = r.sharedMaterial; break; }
            if (!found)
            {
                GameObject commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
                if (commando)
                    foreach (Renderer r in commando.GetComponentsInChildren<Renderer>(true))
                        if (IsStandard(r.sharedMaterial)) { found = r.sharedMaterial; break; }
            }
            if (!found)
            {
                // skins load asynchronously, so at spawn there may be no material yet: build one from the shader itself
                Shader hg = DuelistVisuals.SafeFindShader("Hopoo Games/Deferred/Standard");
                if (hg) found = new Material(hg) { name = "DuelistHGStandard" };
            }
            if (found) _standard = found;
            else DuelistPlugin.Log.LogWarning("Duelist model: no Hopoo standard shader found, using unlit.");
            return found;
        }

        private static bool IsStandard(Material m) =>
            m && m.shader && m.shader.name.Contains("Standard") && !m.shader.name.Contains("FX");

        private static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1f : v;

        private static readonly HashSet<string> TorsoBones = new HashSet<string> { "hips", "spine", "chest", "neck", "head" };

        /// <param name="insideOut">true for the outline shell: only its inner side draws, leaving a dark rim</param>
        private static void AddMesh(GameObject go, PartData part, Vector3 origin, float push, Material m, ShadowCastingMode shadows, bool insideOut,
            Vector3 stretchDir, float stretch)
        {
            int n = part.Pos.Length;
            Vector3[] v = new Vector3[n];
            Vector2[] uv = new Vector2[n];
            int[] tris = new int[n];
            float w = Mathf.Max(1, _palColor.Length);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = part.Pos[i] - origin;
                if (stretch != 1f && stretchDir != Vector3.zero)
                    p += stretchDir * (Vector3.Dot(p, stretchDir) * (stretch - 1f));
                v[i] = p + part.Nrm[i] * push;
                uv[i] = new Vector2((part.Pal[i] + 0.5f) / w, 0.5f);
                // export wrote each triangle as (a, c, b); the axis mirror already flips handedness,
                // so swap back to (a, b, c) or Unity culls the outside and shows the model inside-out
                int k = i % 3;
                tris[i] = insideOut ? i : (k == 1 ? i + 1 : (k == 2 ? i - 1 : i));
            }
            Mesh mesh = new Mesh { name = "Duelist_" + part.Bone };
            if (n > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = v;
            mesh.normals = part.Nrm;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows;
        }

        private static Material MakeMaterial(Material baseMat) => PaletteMaterial(baseMat, _tex, _emTex);

        /// <summary>Hopoo's character shader painted from a palette texture (colours + glow), cel-shaded.</summary>
        public static Material PaletteMaterial(Material baseMat, Texture2D tex, Texture2D emTex)
        {
            if (!baseMat) baseMat = _standard;
            if (!baseMat)
            {
                Shader hg = DuelistVisuals.SafeFindShader("Hopoo Games/Deferred/Standard");
                if (hg) baseMat = _standard = new Material(hg) { name = "DuelistHGStandard" };
            }
            Material m = baseMat ? new Material(baseMat) : new Material(DuelistVisuals.UnlitShader);
            m.name = "DuelistModelMat";
            SetTex(m, "_MainTex", tex);
            SetColor(m, "_Color", Color.white);
            SetTex(m, "_NormalTex", null);
            SetFloat(m, "_NormalStrength", 0f);
            SetTex(m, "_EmTex", emTex);
            SetColor(m, "_EmColor", Color.white);
            SetFloat(m, "_EmPower", DuelConfig.ModelGlow.Value);
            SetFloat(m, "_RampInfo", 0f);             // Hopoo two-tone ramp = cel-shaded look
            SetFloat(m, "_SpecularStrength", 0.08f);
            SetFloat(m, "_SpecularExponent", 4f);
            SetFloat(m, "_FresnelBoost", 0f);
            SetFloat(m, "_FlowmapOn", 0f);
            SetFloat(m, "_LimbRemovalOn", 0f);
            SetFloat(m, "_DitherOn", 0f);
            MakeSolid(m);
            SetFloat(m, "_Cull", (float)CullMode.Off);      // draw both sides (safety net for any flipped faces)
            return m;
        }

        private static Material MakeOutline(Material baseMat)
        {
            if (!baseMat) return null;
            Material m = new Material(baseMat) { name = "DuelistOutlineMat" };
            SetTex(m, "_MainTex", Texture2D.whiteTexture);
            SetColor(m, "_Color", Color.black);
            SetTex(m, "_NormalTex", null);
            SetTex(m, "_EmTex", null);
            SetColor(m, "_EmColor", Color.black);
            SetFloat(m, "_EmPower", 0f);
            SetFloat(m, "_SpecularStrength", 0f);
            SetFloat(m, "_Cull", (float)CullMode.Back);    // the shell mesh itself is inside-out (see AddMesh)
            SetFloat(m, "_FlowmapOn", 0f);
            SetFloat(m, "_LimbRemovalOn", 0f);
            MakeSolid(m);
            return m;
        }

        /// <summary>
        /// Commando's renderers get their fade value from CharacterModel every frame; ours don't,
        /// so make sure the copied material is fully opaque on its own (no dither fade, no limb removal).
        /// </summary>
        private static void MakeSolid(Material m)
        {
            SetFloat(m, "_Fade", 1f);
            SetFloat(m, "_DitherOn", 0f);
            SetFloat(m, "_LimbRemovalOn", 0f);
            SetFloat(m, "_FEON", 0f);
            foreach (string k in new[] { "DITHER", "LIMBREMOVAL", "FORCE_SPEC", "FRESNEL_EMISSION" })
                m.DisableKeyword(k);
            m.renderQueue = 2000;
            Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(c.r, c.g, c.b, 1f));
        }

        private static void SetTex(Material m, string p, Texture t)
        {
            if (!m.HasProperty(p)) return;
            m.SetTexture(p, t);
            m.SetTextureScale(p, Vector2.one);
            m.SetTextureOffset(p, Vector2.zero);
        }
        private static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
        private static void SetFloat(Material m, string p, float f) { if (m.HasProperty(p)) m.SetFloat(p, f); }

        private static Transform FindBone(Transform[] all, string[] names)
        {
            foreach (string n in names)
                foreach (Transform t in all)
                    if (string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase)) return t;
            foreach (string n in names)
                foreach (Transform t in all)
                {
                    string tn = t.name.ToLowerInvariant();
                    if (tn.EndsWith(n) && !tn.Contains("end") && !tn.Contains("ik") && !tn.Contains("duelist")) return t;
                }
            return null;
        }

        /// <summary>Direction a game bone points (toward the next mapped bone), world space.</summary>
        private static bool GameDir(string bone, Transform gb, Dictionary<string, Transform> game, Transform model, out Vector3 dir)
        {
            dir = Vector3.zero;
            if (ChildOf.TryGetValue(bone, out string child) && game.TryGetValue(child, out Transform gc))
                dir = BP(gc) - BP(gb);
            else if (bone.StartsWith("hand."))
            {
                string fore = "forearm." + bone.Substring(5);
                if (game.TryGetValue(fore, out Transform gf)) dir = BP(gb) - BP(gf);
            }
            else if (bone == "head")
            {
                Transform below = game.TryGetValue("neck", out Transform gn) ? gn : (game.TryGetValue("chest", out Transform gc2) ? gc2 : null);
                dir = below ? BP(gb) - BP(below) : model.up;
            }
            else if (bone.StartsWith("foot."))
            {
                dir = gb.childCount > 0 ? BP(gb.GetChild(0)) - BP(gb) : model.forward;
                if (dir.sqrMagnitude < 1e-6f) dir = model.forward;
            }
            if (dir.sqrMagnitude < 1e-8f) return false;
            dir.Normalize();
            return true;
        }

        // ---- bind pose: where each bone sits in the skeleton's rest pose (from the skinned mesh's bindposes)
        private static readonly Dictionary<Transform, Matrix4x4> Bind = new Dictionary<Transform, Matrix4x4>();

        private static void BuildBindPose(Transform model)
        {
            Bind.Clear();
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = smr.sharedMesh;
                Transform[] bones = smr.bones;
                if (!mesh || bones == null) continue;
                Matrix4x4[] bp = mesh.bindposes;
                if (bp == null) continue;
                Matrix4x4 rootM = smr.transform.localToWorldMatrix;
                for (int i = 0; i < bones.Length && i < bp.Length; i++)
                    if (bones[i] && !Bind.ContainsKey(bones[i])) Bind[bones[i]] = rootM * bp[i].inverse;
            }
            if (Bind.Count == 0) DuelistPlugin.Log.LogWarning("Duelist model: no bind pose found, using the current pose.");
            else DuelistPlugin.Log.LogInfo($"Duelist model: bind pose for {Bind.Count} bones.");
        }

        private static Matrix4x4 BM(Transform t) => Bind.TryGetValue(t, out Matrix4x4 m) ? m : t.localToWorldMatrix;
        private static Vector3 BP(Transform t) => Bind.TryGetValue(t, out Matrix4x4 m) ? (Vector3)m.GetColumn(3) : t.position;

        /// <summary>A roll reference that is never parallel to the bone (same rule on both skeletons).</summary>
        private static Vector3 RefAxis(string bone, Vector3 dir, Vector3 fwd, Vector3 up, Vector3 right)
        {
            Vector3 r;
            if (bone.StartsWith("upper_arm.") || bone.StartsWith("forearm.")) r = right;
            else if (bone.StartsWith("hand.") || bone.StartsWith("foot.")) r = up;
            else r = fwd;
            if (Mathf.Abs(Vector3.Dot(r.normalized, dir)) > 0.95f) r = Mathf.Abs(Vector3.Dot(up, dir)) > 0.9f ? fwd : up;
            return r;
        }
    }

    /// <summary>Put on the body's and the lobby display's model; swaps in the Duelist model once it spawns.</summary>
    public class DuelistCustomModel : MonoBehaviour
    {
        private bool _done;

        private System.Collections.IEnumerator Start()
        {
            if (_done) yield break;
            _done = true;
            // skins load asynchronously: wait (up to ~3 s) until the skinned mesh and its bones exist,
            // so the parts can be lined up with the skeleton's rest pose
            for (int i = 0; i < 180 && !SkeletonReady(); i++) yield return null;
            try { DuelistModel.Apply(transform); }
            catch (Exception e) { DuelistPlugin.Log.LogError("Custom model failed, keeping Commando: " + e); }
        }

        private bool SkeletonReady()
        {
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh && smr.bones != null && smr.bones.Length > 0 && smr.sharedMesh.bindposes.Length > 0) return true;
            return false;
        }
    }
}
