using System.Collections.Generic;
using RoR2;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Game asset addresses (GUID keys, from RoR2BepInExPack's GameAssetPathsBetter; copied as strings).
    internal static class Keys
    {
        // Map dressing: the game's own terrain materials (they project their textures in world space, so the
        // procedural map needs no UVs). Night versions where the game has them (Verdant Falls at night).
        public const string RJTerrain = "8b6dda6fb880bcd4b9cfeec9d5bd37ad";          // Sundered Grove moss ground
        public const string RJTerrain2 = "9d2d15b5f71fb4d41aa851725a6d34dd";
        public const string RJCliffMossy = "ae026323688197945882f253eac3c49b";       // matCliff1VeryMossy_1
        public const string RJRock = "7cfdfae968633e74ca4e2a34e9fb33b7";
        public const string RJMossTriplanar = "45d7e8116d45aec4fb8d458e7ac093ad";
        public const string FSFloor = "1be3d9dc017cd1841a3fd74cb7b1595e";            // Wetland Aspect
        public const string FSWalls = "b96d694aca13c4f449405aa2b93441d7";
        public const string FSBoulder = "e80aa11cb66f182438ed64cf46dab11c";
        public const string FSTrunk = "096c79c669fd9784a8963c685232f5fc";
        public const string FSWater = "798511fdf8a5bf944b92fdf31070e30e";
        public const string GPWater = "69816177cbdfc554981361fd1eb2a1b1";
        public const string GPHeavyMoss = "41d18bb05d8fda342ade26d7b2629560";
        public const string WaterfallNight = "9a13b103327b652498b7653d0baf286d";     // matTLSlowWaterFallsNight
        public const string WaterfallDay = "b05b6c0e5e2b5c845a158556efe2e8d1";       // matBHWaterfall
        public const string NightSkybox = "6ecc58d8ab36af947903deeef599bf18";        // matTLNightSkybox
        public const string FrozenNightSkybox = "eca37cefe7584b44eadac026a891c210";
        public const string PPNight = "7d950bfc72076b74eb8b1ab81291be7a";            // ppSceneTLnight
        public const string PPRain = "90ba705aee1aeed43a874d672cb92f00";             // ppSceneRootJungleRain
        public const string PPFrozenNight = "c659dfd4e70d4b6428eb6ea30e8dd62d";
        // Surface types (landing dust, bullet and projectile impacts look them up on every world collider).
        public const string SdDirt = "9fdb3ecd5817ab544bea11316aea9735";
        public const string SdStone = "ccc7d1350e12f9741895a3bbe5f9de7a";
        public const string SdWood = "cc37b32f6b38af1439d87d707d943e29";
        public const string SdMetal = "4216e0d484464544ba4c7ffcf3ba9be1";
        public const string GameManager = "73df4e285bd098f41b83debbc943c919";
        // Effects
        public const string MatRain = "33311ebf8d660824eb226daef7067628";
        public const string MatLightning = "d9db5964ae054504e85d4a8a0c87a89e";       // matLightningLongBlue
        public const string MatSmoke = "95e3c887512b3174abce787f6059649e";
        public const string LightningStrikeImpact = "44c21b3826aa50e4c96f4102e65aafa0";
        public const string SimpleLightningImpact = "f0a5c0463f73e674d856be6e279c21db";
        public const string OmniExplosion = "389f7fab9bc92ef478636537a01a2f00";
        public const string LightningNova = "ba7b2996c5ebad14c9ce66b61ca908b6";
        // Bodies, masters, the run
        public const string ClassicRun = "1639ae59e29c54d4e9b0d31f8fdd112b";
        public const string TitanBody = "a1b26b36b3837504ca9796db5b641bf9";
        public const string TitanMaster = "0dd68b3ffd763b546a260f42738c48fb";
        public const string EngiTurretMaster = "ab6d27c997dd03546bcb6f3176059eba";
        public const string EngiTurretBody = "04508f474b420d546b5d55a9a18a9698";
        public const string CommandoMonsterMaster = "f146e1c7699e35b43b70e119b46875e8";
        // Director card lists (monsters for waves and nests come from these stages; chests from Wetland Aspect).
        public const string InteractablesFoggySwamp = "daa08a015ff94e548a3e820fab2b8e88";
        public const string InteractablesRootJungle = "2de6dfd3a81b9f94abe41c3af37492ee";
        public static readonly string[] MonsterLists =
        {
            "3beb73a0dd48c0e46b2f00b14afc43ee", // golemplains DLC1
            "224d8b87a4e763e4b9c09e6165005fc1", // foggyswamp DLC
            "c344b5a71b36e2a44965f385eb53c94d", // rootjungle DLC1
            "5d747cc6309988949b05aa2fad78e1b6", // wispgraveyard
            "012ffe080c5a69c43b6d11d83c9fa10e", // goolake DLC1
            "c10d601a4ac9fce48876f58c1aa0b63f", // skymeadow DLC1
            "086ef1ec1e1798b4ba70954fa1ddf6e6", // frozenwall DLC1
            "ea8366fe7b361f04e88ac59597d519e7", // shipgraveyard DLC1
            "694e2469e67dbf44b9814315a06963ef", // sulfurpools DLC1
            "7477bbb9aa6f7ad4ab4d763abd17634b", // lakes DLC2
        };
    }

    // The two asset bundles (built in Unity with SpecialForcesBuilder.cs) and material handling.
    internal static class SFAssets
    {
        public const string AssetsBundleFile = "specialforcesassets";
        public const string SceneBundleFile = "sfstormvalley";
        public const string SceneName = "sfStormValley";

        public static GameObject Hurricane, Tower, Nest, Wall, Depot, Lander, Floodlight;
        public static bool SceneReady { get; private set; }
        private static AssetBundle sceneBundle;

        private static Shader gameShader;
        private static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

        // Emissive materials (name -> glow strength); keep in step with sf_props.py / sf_boss.py.
        private static readonly Dictionary<string, float> Glow = new Dictionary<string, float>
        {
            { "matTowerPanel", 1.2f }, { "matTowerGlow", 4f }, { "matNestCrystal", 3f }, { "matDepotLight", 3f },
            { "matLanderWindow", 1.5f }, { "matLanderGlow", 4f }, { "matLanderLight", 3f }, { "matFloodLamp", 5f },
            { "matHurricaneLightning", 7f }, { "matHurricaneEyes", 10f },
        };

        public static void Init(string dir)
        {
            string path = System.IO.Path.Combine(dir, AssetsBundleFile);
            if (System.IO.File.Exists(path))
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                if (bundle)
                {
                    Hurricane = bundle.LoadAsset<GameObject>("mdlHurricane");
                    Tower = bundle.LoadAsset<GameObject>("mdlControlTower");
                    Nest = bundle.LoadAsset<GameObject>("mdlStormNest");
                    Wall = bundle.LoadAsset<GameObject>("mdlBarrierWall");
                    Depot = bundle.LoadAsset<GameObject>("mdlSupplyDepot");
                    Lander = bundle.LoadAsset<GameObject>("mdlLander");
                    Floodlight = bundle.LoadAsset<GameObject>("mdlFloodlight");
                }
            }
            else SpecialForcesPlugin.Log.LogWarning($"No '{AssetsBundleFile}' next to the DLL: build it in Unity with SpecialForcesBuilder.cs.");

            string scenePath = System.IO.Path.Combine(dir, SceneBundleFile);
            if (System.IO.File.Exists(scenePath))
            {
                sceneBundle = AssetBundle.LoadFromFile(scenePath);
                SceneReady = sceneBundle && sceneBundle.isStreamedSceneAssetBundle;
            }
            else SpecialForcesPlugin.Log.LogWarning($"No '{SceneBundleFile}' next to the DLL: the Special Forces map is missing.");

            SpecialForcesPlugin.Log.LogInfo($"Special Forces assets: map {SceneReady}, hurricane {(bool)Hurricane}, tower {(bool)Tower}, " +
                                            $"nest {(bool)Nest}, wall {(bool)Wall}, depot {(bool)Depot}, lander {(bool)Lander}, floodlight {(bool)Floodlight}.");
            RoR2Application.onLoad += () =>
            {
                gameShader = Shader.Find("Hopoo Games/Deferred/Standard");
                if (!gameShader) gameShader = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard");
                if (!gameShader) SpecialForcesPlugin.Log.LogWarning("Game shader not found; models keep Unity's Standard shader.");
                ConvertPending();
            };
        }

        public static string BaseName(Material m)
        {
            string n = m.name;
            int cut = n.IndexOf(" (");
            if (cut > 0) n = n.Substring(0, cut);
            cut = n.IndexOf('.');
            if (cut > 0) n = n.Substring(0, cut);
            return n;
        }

        // A Standard-shader material from the bundle -> a copy on the game's shader (same colour, glow by name).
        public static Material Convert(Material source)
        {
            if (!source || !gameShader || source.shader == gameShader) return source;
            if (converted.TryGetValue(source, out var done) && done) return done;
            var mat = new Material(gameShader) { name = source.name + "_hg" };
            Color color = source.HasProperty("_Color") ? source.color : Color.white;
            mat.SetColor("_Color", color);
            Texture main = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (main) mat.SetTexture("_MainTex", main);
            Glow.TryGetValue(BaseName(source), out float glow);
            mat.SetColor("_EmColor", glow > 0 ? color : Color.black);
            mat.SetFloat("_EmPower", glow);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_SpecularStrength")) mat.SetFloat("_SpecularStrength", 0.25f);
            mat.SetFloat("_LimbRemovalOn", 0f);
            mat.DisableKeyword("LIMBREMOVAL");
            converted[source] = mat;
            return mat;
        }

        // Bundle models built into prefabs at startup: converted once the game's shader exists.
        private static readonly List<GameObject> pending = new List<GameObject>();

        public static void RegisterForConversion(GameObject go)
        {
            if (gameShader) ConvertRenderers(go);
            else pending.Add(go);
        }

        public static void ConvertPending()
        {
            foreach (var go in pending) if (go) ConvertRenderers(go);
            pending.Clear();
        }

        public static void ConvertRenderers(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = Convert(mats[i]);
                r.sharedMaterials = mats;
            }
        }

        // A plain visual copy of a bundle prefab (no gameplay), on the game's shader.
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f, Transform parent = null)
        {
            if (!prefab) return null;
            GameObject go = Object.Instantiate(prefab, position, rotation * prefab.transform.rotation, parent);
            go.transform.localScale = prefab.transform.localScale * scale;
            ConvertRenderers(go);
            return go;
        }

        // ---------------------------------------------------------------- map materials

        // Material slot (Blender name) -> the game's material, with an optional tint (tinting is done on a copy).
        private static readonly Dictionary<string, (string key, string fallback, Color tint)> MapMaterials =
            new Dictionary<string, (string, string, Color)>
            {
                { "matForestFloor", (Keys.RJTerrain, Keys.FSFloor, Color.white) },
                { "matMossRock", (Keys.RJTerrain2, Keys.GPHeavyMoss, Color.white) },
                { "matCliff", (Keys.FSWalls, Keys.GPHeavyMoss, Color.white) },
                { "matRiverbed", (Keys.FSFloor, Keys.RJRock, Color.white) },
                { "matPlainGrass", (Keys.GPHeavyMoss, Keys.RJTerrain2, new Color(1.35f, 1.05f, 0.55f)) },  // golden grass
                { "matOutpostGround", (Keys.RJRock, Keys.FSFloor, new Color(0.85f, 0.85f, 0.85f)) },
                { "matWater", (Keys.FSWater, Keys.GPWater, Color.white) },
                { "matWaterfall", (Keys.WaterfallNight, Keys.WaterfallDay, Color.white) },
                { "matTreeTrunk", (Keys.FSTrunk, Keys.RJRock, Color.white) },
                { "matCanopy", (Keys.GPHeavyMoss, Keys.FSBoulder, new Color(0.75f, 0.95f, 0.75f)) },
                { "matBoulder", (Keys.FSBoulder, Keys.RJRock, Color.white) },
            };

        private static readonly Dictionary<string, Material> mapCache = new Dictionary<string, Material>();

        public static Material MapMaterial(Material slot)
        {
            string name = BaseName(slot);
            if (!SFConfig.UseGameMaterials.Value || !MapMaterials.TryGetValue(name, out var entry)) return Convert(slot);
            if (mapCache.TryGetValue(name, out Material m)) return m ? m : Convert(slot);
            // 1.0.1: some stage materials (Sundered Grove's cliffs and moss) came without their shader and drew
            // magenta. Only use a game material whose shader actually works here.
            Material game = Usable(SFUtil.Load<Material>(entry.key, name));
            if (!game) game = Usable(SFUtil.Load<Material>(entry.fallback, name + " (fallback)"));
            if (game && entry.tint != Color.white)
            {
                game = new Material(game) { name = game.name + "_sf" };
                if (game.HasProperty("_Color")) game.SetColor("_Color", game.GetColor("_Color") * entry.tint);
            }
            mapCache[name] = game;
            SpecialForcesPlugin.Log.LogInfo($"Map material {name}: {(game ? game.name + " (" + game.shader.name + ")" : "plain colour")}");
            return game ? game : Convert(slot);
        }

        private static Material Usable(Material m)
        {
            if (!m || !m.shader) return null;
            if (!m.shader.isSupported || m.shader.name.Contains("InternalError") || m.shader.name.Contains("Hidden/"))
            {
                SpecialForcesPlugin.Log.LogWarning($"Game material {m.name} has no working shader ({m.shader.name}); skipped.");
                return null;
            }
            return m;
        }
    }
}
