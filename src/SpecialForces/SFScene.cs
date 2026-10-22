using System;
using System.Collections.Generic;
using RoR2;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Turns the bundle's plain geometry into a playable stage when sfStormValley loads (on every machine):
    // game materials, colliders, monster pathing, the game's stage components (SceneInfo + ClassicStageInfo, the
    // director that places chests), player spawn points, night sky + post-processing, an out-of-bounds catch,
    // then the storm, the lander cutscene, the HUD and (host only) the Special Forces director.
    internal static class SFScene
    {
        public static readonly Dictionary<string, Vector3> Markers = new Dictionary<string, Vector3>();
        public static bool Active { get; private set; }
        public static Vector3 TowerPosition => Markers.TryGetValue("SF_Tower", out var p) ? p : Vector3.zero;
        public static Vector3 LanderPad => Markers.TryGetValue("SF_LanderPad", out var p) ? p : TowerPosition;

        public static void Init()
        {
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                bool ours = scene.name == SFAssets.SceneName;
                if (!ours)
                {
                    if (Active) SFAudio.SetRain(false);
                    Active = false;
                    return;
                }
                try { Setup(scene); }
                catch (Exception e) { SpecialForcesPlugin.Log.LogError("Stormreach Valley setup failed: " + e); }
            };
        }

        private static void Setup(Scene scene)
        {
            Active = true;
            Markers.Clear();
            GameObject map = null;
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name.StartsWith("StormValley")) map = go;
            if (!map)
            {
                SpecialForcesPlugin.Log.LogError("Stormreach Valley: map object not found in the scene.");
                return;
            }

            EnsureGlobalEvents();

            // ---- markers, materials, colliders
            var bounds = new Bounds();
            bool hasBounds = false;
            int colliders = 0;
            foreach (Transform t in map.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("SF_")) Markers[t.name] = t.position;
                var renderer = t.GetComponent<MeshRenderer>();
                if (!renderer) continue;
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = SFAssets.MapMaterial(mats[i]);
                renderer.sharedMaterials = mats;
                bool solid = t.name.StartsWith("Terrain_") || t.name.StartsWith("Boulders_") || t.name.StartsWith("Trunks_") || t.name.StartsWith("Outpost_");
                if (solid)
                {
                    var filter = t.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                    {
                        var mc = t.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = filter.sharedMesh;
                        t.gameObject.layer = LayerIndex.world.intVal;
                        // 1.0.1: without a surface type the game's landing and impact code threw (OnLanded x174,
                        // projectiles not exploding on the ground).
                        string surface = t.name.StartsWith("Trunks_") ? Keys.SdWood : t.name.StartsWith("Boulders_") ? Keys.SdStone
                                       : t.name.StartsWith("Outpost_") ? Keys.SdStone : Keys.SdDirt;
                        SetSurface(t.gameObject, surface);
                        colliders++;
                    }
                }
                else
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (t.name.StartsWith("Terrain_"))
                {
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            Physics.SyncTransforms();
            SpecialForcesPlugin.Log.LogInfo($"Stormreach Valley: {Markers.Count} markers, {colliders} colliders, map {bounds.size.x:0} x {bounds.size.z:0} m.");

            // ---- pathing + the game's stage components
            NodeGraph ground = SFNodeGraph.BuildGround(bounds, Mathf.Max(4f, SFConfig.NodeSpacing.Value));
            NodeGraph air = SFNodeGraph.BuildAir(bounds, 28f);

            var infoObject = new GameObject("SceneInfo");
            infoObject.SetActive(false);
            var info = infoObject.AddComponent<SceneInfo>();
            SFUtil.SetField(info, "groundNodesAsset", ground);
            SFUtil.SetField(info, "airNodesAsset", air);
            var stageInfo = infoObject.AddComponent<ClassicStageInfo>();
            stageInfo.interactableCategories = SFUtil.Load<DirectorCardCategorySelection>(Keys.InteractablesFoggySwamp, "Wetland Aspect interactables")
                                               ?? SFUtil.Load<DirectorCardCategorySelection>(Keys.InteractablesRootJungle, "Sundered Grove interactables");
            SFUtil.SetField(stageInfo, "monsterCategories", SFUtil.Load<DirectorCardCategorySelection>(Keys.MonsterLists[2], "Sundered Grove monsters"));
            stageInfo.sceneDirectorInteractibleCredits = SFConfig.InteractableCredits.Value;
            stageInfo.sceneDirectorMonsterCredits = 0;               // no starting monsters: nests and waves bring them
            stageInfo.possibleMonsterFamilies = new ClassicStageInfo.MonsterFamily[0];
            stageInfo.bonusInteractibleCreditObjects = new ClassicStageInfo.BonusInteractibleCreditObject[0];
            infoObject.SetActive(true);
            // Workaround: SceneInfo copies its graphs in Awake; make sure the live properties point at them too.
            if (info.groundNodes == null) SFUtil.SetField(info, "<groundNodes>k__BackingField", ground);
            if (info.airNodes == null) SFUtil.SetField(info, "<airNodes>k__BackingField", air);

            var directorObject = new GameObject("Director");
            directorObject.SetActive(false);
            directorObject.AddComponent<DirectorCore>();
            var sceneDirector = directorObject.AddComponent<SceneDirector>();
            sceneDirector.teleporterSpawnCard = null;             // no teleporter: this stage never ends
            sceneDirector.expRewardCoefficient = 1f;
            directorObject.SetActive(true);

            // ---- player spawn points: beside the lander pad (where the ramp comes down)
            for (int k = 1; k <= 12; k++)
            {
                if (!Markers.TryGetValue($"SF_PlayerSpawn_{k:00}", out Vector3 p)) continue;
                Vector3 look = TowerPosition - p;
                look.y = 0;
                SpawnPoint.AddSpawnPoint(SFUtil.Ground(p) + Vector3.up * 0.2f, look.sqrMagnitude > 1 ? Quaternion.LookRotation(look) : Quaternion.identity);
            }

            // ---- out of bounds: anything that falls through the world is put back (the game's MapZone)
            var oob = new GameObject("OutOfBounds");
            oob.layer = LayerIndex.collideWithCharacterHullOnly.intVal;
            var box = oob.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(bounds.size.x + 400f, 40f, bounds.size.z + 400f);
            oob.transform.position = new Vector3(bounds.center.x, bounds.min.y - 45f, bounds.center.z);
            var zone = oob.AddComponent<MapZone>();
            zone.zoneType = MapZone.ZoneType.OutOfBounds;
            zone.triggerType = MapZone.TriggerType.TriggerEnter;

            // ---- night: the game's night sky and post-processing (Verdant Falls at night + Sundered Grove's rain)
            Material sky = SFUtil.Load<Material>(Keys.NightSkybox, "night skybox") ?? SFUtil.Load<Material>(Keys.FrozenNightSkybox, "night skybox (fallback)");
            if (sky) RenderSettings.skybox = sky;
            AddPostProcess("PPNight", Keys.PPNight, 1f, 2f) ;
            AddPostProcess("PPRain", Keys.PPRain, 0.45f, 1f);

            // ---- everything else
            new GameObject("SFWeather").AddComponent<SFWeather>();
            SFDecor.Build(map);
            SFHud.Begin();
            SFCutscene.Begin();
            if (NetworkServer.active) new GameObject("SFDirector").AddComponent<SFDirector>();
        }

        // PostProcessVolume isn't in the reference libs: added by name (Unity's post-processing package).
        private static void AddPostProcess(string name, string key, float weight, float priority)
        {
            ScriptableObject profile = SFUtil.Load<ScriptableObject>(key, name);
            if (!profile) return;
            Type volumeType = Type.GetType("UnityEngine.Rendering.PostProcessing.PostProcessVolume, Unity.Postprocessing.Runtime");
            if (volumeType == null)
            {
                SpecialForcesPlugin.Log.LogWarning("PostProcessVolume type not found; no night colour grading.");
                return;
            }
            var go = new GameObject(name) { layer = LayerIndex.postProcess.intVal };
            Component volume = go.AddComponent(volumeType);
            SFUtil.SetField(volume, "isGlobal", true);
            SFUtil.SetField(volume, "weight", weight);
            SFUtil.SetField(volume, "priority", priority);
            SFUtil.SetField(volume, "sharedProfile", profile);
        }

        // 1.0.2's log: 3000+ NullReferenceExceptions in BlastAttack/BulletAttack/OverlapAttack/projectile hits,
        // CharacterMotor.OnLanded and Interactor.PerformInteraction. Every vanilla stage has a GlobalEventManager in
        // the scene (on-hit procs, ignite and other damage types, landing, interactions, death events); a bundle scene
        // made outside the game's tools has none, so damage landed but everything after it threw: no procs, no burn,
        // missing tracers. One is made here, with its settings copied from the game's GameManager prefab when it has one.
        private static void EnsureGlobalEvents()
        {
            if (GlobalEventManager.instance)
            {
                SpecialForcesPlugin.Log.LogInfo("GlobalEventManager already present.");
                return;
            }
            GlobalEventManager source = null;
            GameObject prefab = SFUtil.Load<GameObject>(Keys.GameManager, "GameManager");
            if (prefab)
            {
                var names = new List<string>();
                foreach (var c in prefab.GetComponentsInChildren<Component>(true)) if (c) names.Add(c.GetType().Name);
                SpecialForcesPlugin.Log.LogInfo("GameManager prefab: " + string.Join(", ", names));
                source = prefab.GetComponentInChildren<GlobalEventManager>(true);
            }
            var go = new GameObject("GlobalEventManager");
            go.SetActive(false);
            var manager = go.AddComponent<GlobalEventManager>();
            int copied = 0;
            if (source)
            {
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                                             System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
                foreach (var field in typeof(GlobalEventManager).GetFields(flags))
                {
                    if (field.IsInitOnly) continue;
                    try { field.SetValue(manager, field.GetValue(source)); copied++; }
                    catch { }
                }
            }
            go.SetActive(true);
            SpecialForcesPlugin.Log.LogInfo($"GlobalEventManager added to the scene ({copied} settings copied, instance {(bool)GlobalEventManager.instance}).");
        }

        public static void SetSurface(GameObject go, string key)
        {
            var def = SFUtil.Load<SurfaceDef>(key, "surface type");
            if (!def) return;
            var provider = go.GetComponent<SurfaceDefProvider>();
            if (!provider) provider = go.AddComponent<SurfaceDefProvider>();
            provider.surfaceDef = def;
        }

        public static Vector3 Marker(string name, Vector3 fallback) => Markers.TryGetValue(name, out var p) ? p : fallback;

        public static IEnumerable<KeyValuePair<string, Vector3>> MarkersStartingWith(string prefix)
        {
            foreach (var pair in Markers)
                if (pair.Key.StartsWith(prefix)) yield return pair;
        }
    }
}
