using System;
using System.Collections.Generic;
using System.IO;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;
using Path = System.IO.Path;

namespace BerserkFury
{
    public static class BerserkFuryModel
    {
        public const float ModelScale = 0.20f;
        private static Mesh _cachedMesh;
        private static Material _cachedMaterial;
        private static Texture2D _cachedTexture;
        private static bool _loadFailed;

        public static string FindFile(string fileName)
        {
            string dir = Path.GetDirectoryName(typeof(BerserkFuryModel).Assembly.Location);
            for (int i = 0; i < 5 && !string.IsNullOrEmpty(dir); i++)
            {
                string candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate)) return candidate;
                string inModDir = Path.Combine(dir, "mods", "BerserkFury", fileName);
                if (File.Exists(inModDir)) return inModDir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        public static void EnsureLoaded()
        {
            if (_cachedMesh != null && _cachedMaterial != null) return;
            if (_loadFailed) return;

            try
            {
                string objPath = FindFile("BerserkFury.obj");
                string pngPath = FindFile("BerserkFury.png");

                if (string.IsNullOrEmpty(objPath) || !File.Exists(objPath))
                {
                    BerserkFuryPlugin.Log.LogError("BerserkFury.obj not found!");
                    _loadFailed = true;
                    return;
                }

                if (string.IsNullOrEmpty(pngPath) || !File.Exists(pngPath))
                {
                    BerserkFuryPlugin.Log.LogError("BerserkFury.png not found!");
                    _loadFailed = true;
                    return;
                }

                _cachedTexture = LoadTexture(pngPath);
                _cachedMaterial = CreateMaterial(_cachedTexture);
                _cachedMesh = LoadObj(objPath, ModelScale);
                BerserkFuryRig.Build(objPath, ModelScale);

                BerserkFuryPlugin.Log.LogInfo($"Successfully loaded Berserk Fury 3D model ({_cachedMesh.vertexCount} verts, {_cachedMesh.triangles.Length / 3} tris).");
            }
            catch (Exception ex)
            {
                BerserkFuryPlugin.Log.LogError("Error loading Berserk Fury model: " + ex);
                _loadFailed = true;
            }
        }

        private static Texture2D LoadTexture(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = "texBerserkFury";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.LoadImage(bytes);
            return tex;
        }

        private static Material CreateMaterial(Texture2D mainTex) => SafeMaterial(mainTex, Color.white, Color.black, 0f);

        public static Material SafeMaterial(Texture mainTex, Color color, Color em, float emPower)
        {
            // Start from Commando's real material (all Hopoo properties sane) instead of a blank HG Standard:
            // blank defaults (spec exponent 0, fresnel, flowmap) produced NaN/over-bright pixels and bloom whited out the screen.
            Material baseMat = null;
            try
            {
                GameObject commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
                CharacterModel cm = commando ? commando.GetComponentInChildren<CharacterModel>(true) : null;
                if (cm && cm.baseRendererInfos != null)
                    foreach (var info in cm.baseRendererInfos)
                        if (info.defaultMaterial && info.defaultMaterial.shader && info.defaultMaterial.shader.name.Contains("Hopoo")) { baseMat = info.defaultMaterial; break; }
            }
            catch (Exception e) { BerserkFuryPlugin.Log.LogWarning("Commando material: " + e.Message); }

            Material mat;
            if (baseMat) mat = new Material(baseMat);
            else
            {
                Shader hg = null;
                try { hg = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard"); } catch { }
                mat = new Material(hg ? hg : Shader.Find("Hopoo Games/Deferred/Standard"));
            }

            mat.name = "matBerserkFury";
            SetTex(mat, "_MainTex", mainTex ? mainTex : Texture2D.whiteTexture);
            SetColor(mat, "_Color", color);
            SetTex(mat, "_NormalTex", null);
            SetFloat(mat, "_NormalStrength", 0f);
            SetTex(mat, "_EmTex", null);
            SetColor(mat, "_EmColor", em);
            SetFloat(mat, "_EmPower", emPower);
            SetFloat(mat, "_RampInfo", 0f);
            SetFloat(mat, "_SpecularStrength", 0.1f);
            SetFloat(mat, "_SpecularExponent", 4f);
            SetFloat(mat, "_FresnelBoost", 0f);
            SetFloat(mat, "_FlowmapOn", 0f);
            SetFloat(mat, "_LimbRemovalOn", 0f);
            SetFloat(mat, "_DitherOn", 0f);
            SetFloat(mat, "_Fade", 1f);
            SetFloat(mat, "_FEON", 0f);
            foreach (string k in new[] { "DITHER", "LIMBREMOVAL", "FORCE_SPEC", "FRESNEL_EMISSION" }) mat.DisableKeyword(k);
            mat.renderQueue = 2000;
            SetFloat(mat, "_Cull", (float)CullMode.Off);
            return mat;
        }

        private static void SetTex(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }
        private static void SetFloat(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        private static void SetColor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }

        private struct VertexKey : IEquatable<VertexKey>
        {
            public int V, Vt, Vn;
            public VertexKey(int v, int vt, int vn) { V = v; Vt = vt; Vn = vn; }
            public bool Equals(VertexKey other) => V == other.V && Vt == other.Vt && Vn == other.Vn;
            public override bool Equals(object obj) => obj is VertexKey vk && Equals(vk);
            public override int GetHashCode() => (V * 397 ^ Vt) * 397 ^ Vn;
        }

        private static Mesh LoadObj(string path, float scale)
        {
            List<Vector3> rawPositions = new List<Vector3>();
            List<Vector2> rawUVs = new List<Vector2>();
            List<Vector3> rawNormals = new List<Vector3>();

            List<Vector3> outPositions = new List<Vector3>();
            List<Vector2> outUVs = new List<Vector2>();
            List<Vector3> outNormals = new List<Vector3>();
            List<int> outTriangles = new List<int>();

            Dictionary<VertexKey, int> vertexLookup = new Dictionary<VertexKey, int>();

            string[] lines = File.ReadAllLines(path);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.Length < 2 || line.StartsWith("#")) continue;

                if (line.StartsWith("v "))
                {
                    string[] p = line.Substring(2).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3 && float.TryParse(p[0], out float x) && float.TryParse(p[1], out float y) && float.TryParse(p[2], out float z))
                    {
                        rawPositions.Add(new Vector3(x, y, z));
                    }
                }
                else if (line.StartsWith("vt "))
                {
                    string[] p = line.Substring(3).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 2 && float.TryParse(p[0], out float u) && float.TryParse(p[1], out float v))
                    {
                        rawUVs.Add(new Vector2(u, v));
                    }
                }
                else if (line.StartsWith("vn "))
                {
                    string[] p = line.Substring(3).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3 && float.TryParse(p[0], out float nx) && float.TryParse(p[1], out float ny) && float.TryParse(p[2], out float nz))
                    {
                        rawNormals.Add(new Vector3(nx, ny, nz));
                    }
                }
                else if (line.StartsWith("f "))
                {
                    string[] parts = line.Substring(2).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3) continue;

                    // Fan triangulation if quad/polygon
                    int[] faceIndices = new int[parts.Length];
                    for (int i = 0; i < parts.Length; i++)
                    {
                        faceIndices[i] = ParseOrGetVertex(parts[i], rawPositions, rawUVs, rawNormals, outPositions, outUVs, outNormals, vertexLookup, scale);
                    }

                    for (int i = 1; i < parts.Length - 1; i++)
                    {
                        outTriangles.Add(faceIndices[0]);
                        outTriangles.Add(faceIndices[i]);
                        outTriangles.Add(faceIndices[i + 1]);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "meshBerserkFury";
            mesh.indexFormat = outPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outPositions);
            if (outUVs.Count == outPositions.Count) mesh.SetUVs(0, outUVs);
            if (outNormals.Count == outPositions.Count) mesh.SetNormals(outNormals);
            mesh.SetTriangles(outTriangles, 0);
            mesh.RecalculateBounds();
            if (outNormals.Count != outPositions.Count) mesh.RecalculateNormals();
            // HG Standard is a deferred, normal-mapped shader: without tangents it writes NaN pixels and bloom
            // spreads them into a white-out over the whole screen.
            mesh.RecalculateTangents();
            // Zero-area UV triangles give NaN tangents; NaN in the G-buffer turns the whole screen white through bloom.
            Vector4[] tan = mesh.tangents;
            Vector3[] nrm = mesh.normals;
            int fixedCount = 0;
            for (int i = 0; i < tan.Length; i++)
            {
                Vector4 t = tan[i];
                if (float.IsNaN(t.x) || float.IsNaN(t.y) || float.IsNaN(t.z) || float.IsNaN(t.w) || new Vector3(t.x, t.y, t.z).sqrMagnitude < 1e-8f)
                {
                    Vector3 n = i < nrm.Length ? nrm[i] : Vector3.up;
                    Vector3 axis = Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right;
                    Vector3 tt = Vector3.Cross(axis, n).normalized;
                    tan[i] = new Vector4(tt.x, tt.y, tt.z, 1f);
                    fixedCount++;
                }
            }
            for (int i = 0; i < nrm.Length; i++)
                if (float.IsNaN(nrm[i].x) || nrm[i].sqrMagnitude < 1e-8f) { nrm[i] = Vector3.up; fixedCount++; }
            mesh.normals = nrm;
            mesh.tangents = tan;
            if (fixedCount > 0) BerserkFuryPlugin.Log.LogInfo($"Mesh: repaired {fixedCount} bad normals/tangents");
            return mesh;
        }

        private static int ParseOrGetVertex(string part, List<Vector3> rawP, List<Vector2> rawUV, List<Vector3> rawN,
            List<Vector3> outP, List<Vector2> outUV, List<Vector3> outN, Dictionary<VertexKey, int> lookup, float scale)
        {
            string[] sub = part.Split('/');
            int vIdx = int.Parse(sub[0]);
            int vtIdx = sub.Length > 1 && sub[1].Length > 0 ? int.Parse(sub[1]) : 0;
            int vnIdx = sub.Length > 2 && sub[2].Length > 0 ? int.Parse(sub[2]) : 0;

            VertexKey key = new VertexKey(vIdx, vtIdx, vnIdx);
            if (lookup.TryGetValue(key, out int existing)) return existing;

            // 1-based indexing in OBJ
            Vector3 rawPos = rawP[vIdx - 1];
            // Coordinate mapping: +X (Blender forward) -> +Z (Unity forward)
            // +Y (Blender up) -> +Y (Unity up)
            // +Z (Blender left) -> -X (Unity right)
            Vector3 unityPos = new Vector3(-rawPos.z * scale, rawPos.y * scale, rawPos.x * scale);

            Vector2 unityUV = (vtIdx > 0 && vtIdx <= rawUV.Count) ? rawUV[vtIdx - 1] : Vector2.zero;

            Vector3 unityNorm = Vector3.up;
            if (vnIdx > 0 && vnIdx <= rawN.Count)
            {
                Vector3 rawNorm = rawN[vnIdx - 1];
                unityNorm = new Vector3(-rawNorm.z, rawNorm.y, rawNorm.x).normalized;
            }

            int newIndex = outP.Count;
            outP.Add(unityPos);
            outUV.Add(unityUV);
            outN.Add(unityNorm);
            lookup[key] = newIndex;
            return newIndex;
        }

        public static void Attach(GameObject characterModelRoot)
        {
            EnsureLoaded();
            if (_cachedMesh == null || _cachedMaterial == null || !characterModelRoot) return;

            CharacterModel model = characterModelRoot.GetComponent<CharacterModel>();
            if (!model) return;

            // Hide original cloned body meshes
            foreach (Renderer r in characterModelRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r) r.forceRenderingOff = true;
            }

            // Create container for Berserk Fury model
            Transform existing = characterModelRoot.transform.Find("BerserkFuryVisual");
            if (existing) UnityEngine.Object.Destroy(existing.gameObject);

            GameObject visualObj = new GameObject("BerserkFuryVisual");
            visualObj.transform.SetParent(characterModelRoot.transform, false);
            visualObj.transform.localPosition = Vector3.zero;
            visualObj.transform.localRotation = Quaternion.identity;
            visualObj.transform.localScale = Vector3.one;

            List<Renderer> parts = BerserkFuryRig.Instantiate(visualObj.transform, _cachedMaterial);

            // Register into CharacterModel renderer list for RoR2 lighting & item overlays
            var list = new List<CharacterModel.RendererInfo>(model.baseRendererInfos ?? new CharacterModel.RendererInfo[0]);
            foreach (Renderer r in parts)
                list.Add(new CharacterModel.RendererInfo
                {
                    renderer = r,
                    defaultMaterial = _cachedMaterial,
                    defaultShadowCastingMode = ShadowCastingMode.On,
                    ignoreOverlays = false
                });
            model.baseRendererInfos = list.ToArray();

            // Add dynamic idle / locomotion anim component
            visualObj.AddComponent<BerserkFuryLocomotion>();

            // forceRenderingOff is not saved on prefabs, so Commando reappeared in game. Keep him hidden at runtime.
            BerserkFuryHideBase hider = characterModelRoot.GetComponent<BerserkFuryHideBase>();
            if (!hider) hider = characterModelRoot.AddComponent<BerserkFuryHideBase>();
            hider.Visual = visualObj.transform;
        }
    }

    /// <summary>Hides the cloned Commando meshes every frame and keeps every Berserk Fury part listed + visible.</summary>
    public class BerserkFuryHideBase : MonoBehaviour
    {
        public Transform Visual;
        private CharacterModel _model;
        private bool _logged;

        private void LateUpdate()
        {
            if (!_model) _model = GetComponent<CharacterModel>();
            if (!_model || _model.baseRendererInfos == null) return;
            if (!Visual)
            {
                Visual = transform.Find("BerserkFuryVisual");
                if (!Visual) { BerserkFuryModel.Attach(gameObject); Visual = transform.Find("BerserkFuryVisual"); }
                if (!Visual) return;
            }

            Renderer[] mine = Visual.GetComponentsInChildren<Renderer>(true);
            var listed = new HashSet<Renderer>();
            foreach (var info in _model.baseRendererInfos) if (info.renderer) listed.Add(info.renderer);
            List<CharacterModel.RendererInfo> add = null;
            foreach (Renderer r in mine)
            {
                if (!r.enabled) r.enabled = true;
                if (r.forceRenderingOff) r.forceRenderingOff = false;
                if (!listed.Contains(r))
                {
                    if (add == null) add = new List<CharacterModel.RendererInfo>(_model.baseRendererInfos);
                    add.Add(new CharacterModel.RendererInfo { renderer = r, defaultMaterial = r.sharedMaterial, defaultShadowCastingMode = ShadowCastingMode.On, ignoreOverlays = false });
                }
            }
            if (add != null) _model.baseRendererInfos = add.ToArray();
            if (!_logged) { _logged = true; BerserkFuryPlugin.Log.LogInfo($"[Visual] {name}: {mine.Length} parts, re-listed={(add != null)}"); }

            foreach (var info in _model.baseRendererInfos)
            {
                Renderer r = info.renderer;
                if (!r || r.name.StartsWith("Display") || r.transform.IsChildOf(Visual)) continue;
                if (!r.forceRenderingOff) r.forceRenderingOff = true;
            }
        }
    }
}
