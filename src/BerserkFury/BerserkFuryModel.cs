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

        private static Material CreateMaterial(Texture2D mainTex)
        {
            Shader hgStandard = Shader.Find("Hopoo Games/Deferred/Standard");
            if (!hgStandard) hgStandard = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard");

            Material mat;
            if (hgStandard)
            {
                mat = new Material(hgStandard);
            }
            else
            {
                // Fall back to borrowing Commando's material
                GameObject commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
                Renderer cr = commando ? commando.GetComponentInChildren<Renderer>(true) : null;
                mat = cr && cr.sharedMaterial ? new Material(cr.sharedMaterial) : new Material(Shader.Find("Standard"));
            }

            mat.name = "matBerserkFury";
            mat.mainTexture = mainTex;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
            if (mat.HasProperty("_SpecularStrength")) mat.SetFloat("_SpecularStrength", 0.65f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.5f);
            // No _EmTex means the whole mesh emits, which blew the model out to flat magenta. Emission off.
            if (mat.HasProperty("_EmTex")) mat.SetTexture("_EmTex", null);
            if (mat.HasProperty("_EmColor")) mat.SetColor("_EmColor", Color.black);
            if (mat.HasProperty("_EmPower")) mat.SetFloat("_EmPower", 0f);
            if (mat.HasProperty("_RampInfo")) mat.SetFloat("_RampInfo", 1f);
            return mat;
        }

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

            MeshFilter filter = visualObj.AddComponent<MeshFilter>();
            filter.sharedMesh = _cachedMesh;

            MeshRenderer renderer = visualObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _cachedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            // Register into CharacterModel renderer list for RoR2 lighting & item overlays
            var list = new List<CharacterModel.RendererInfo>(model.baseRendererInfos ?? new CharacterModel.RendererInfo[0]);
            list.Add(new CharacterModel.RendererInfo
            {
                renderer = renderer,
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
            hider.Keep = renderer;
        }
    }

    /// <summary>Hides the cloned Commando meshes every frame; item displays (Display*) stay visible.</summary>
    public class BerserkFuryHideBase : MonoBehaviour
    {
        public Renderer Keep;
        private CharacterModel _model;

        private void LateUpdate()
        {
            if (!_model) _model = GetComponent<CharacterModel>();
            if (!_model || _model.baseRendererInfos == null) return;
            if (!Keep)
            {
                Transform v = transform.Find("BerserkFuryVisual");
                if (v) Keep = v.GetComponent<Renderer>();
            }
            foreach (var info in _model.baseRendererInfos)
            {
                Renderer r = info.renderer;
                if (!r || r == Keep || r.name.StartsWith("Display")) continue;
                if (!r.forceRenderingOff) r.forceRenderingOff = true;
            }
        }
    }

    /// <summary>
    /// Animates Berserk Fury with dynamic T-Rex gait, weapon recoil, and idle breathing.
    /// </summary>
    public class BerserkFuryLocomotion : MonoBehaviour
    {
        private CharacterBody _body;
        private float _walkCycle;
        private Vector3 _basePos;
        private Quaternion _baseRot;

        private void Start()
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _body = GetComponentInParent<CharacterBody>();
        }

        private void Update()
        {
            if (!_body)
            {
                _body = GetComponentInParent<CharacterBody>();
                // In character display preview
                if (!_body)
                {
                    float idle = Mathf.Sin(Time.time * 1.5f) * 0.03f;
                    transform.localPosition = _basePos + new Vector3(0, idle, 0);
                    return;
                }
            }

            Vector3 velocity = _body.characterMotor ? _body.characterMotor.velocity : Vector3.zero;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;

            if (horizontalSpeed > 0.5f)
            {
                // Walking / running T-Rex dinosaur gait (head bobbing, slight tail swaying)
                float freq = Mathf.Min(horizontalSpeed * 0.7f, 6f);
                _walkCycle += Time.deltaTime * freq;

                float bobY = Mathf.Abs(Mathf.Sin(_walkCycle)) * 0.08f;
                float swayZ = Mathf.Sin(_walkCycle * 0.5f) * 2.5f;
                float tiltPitch = Mathf.Sin(_walkCycle) * 1.8f;

                transform.localPosition = _basePos + new Vector3(0, bobY, 0);
                transform.localRotation = _baseRot * Quaternion.Euler(tiltPitch, 0, swayZ);
            }
            else
            {
                // Idle breathing
                float breath = Mathf.Sin(Time.time * 1.8f) * 0.025f;
                float pitch = Mathf.Sin(Time.time * 1.2f) * 0.8f;
                transform.localPosition = _basePos + new Vector3(0, breath, 0);
                transform.localRotation = _baseRot * Quaternion.Euler(pitch, 0, 0);
            }
        }
    }
}
