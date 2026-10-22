// Special Forces asset bundle builder (Unity 2021.3.45f2 editor script - the game's exact version).
//
// Setup: put every FBX from this folder (StormValley, Hurricane, ControlTower, StormNest, BarrierWall, SupplyDepot,
// Lander, Floodlight) in Assets/SpecialForces/, and this file in Assets/Editor/. Then use the menu
//   Special Forces > Build Asset Bundles
// It writes two files to <project>/AssetBundles/: "sfstormvalley" (the map scene) and "specialforcesassets" (the
// hurricane and the props). Copy both next to SpecialForces.dll.
//
// What it does (same approach as the survivors' builders):
//  1. Import settings: every model Read/Write enabled (the mod builds mesh colliders and the game's particle effects
//     emit from meshes), materials imported from the FBX so each keeps its Blender name (the mod swaps them by name
//     for the game's own terrain/water materials or recolours them on the game's shader).
//  2. The hurricane: Generic rig (root bone "base"), named clips, an animator controller whose state and parameter
//     names match what the mod's boss states play (Body: Idle/Walk/Death; "FullBody, Override": Claw/Slam/Roar/Cast).
//  3. Prefabs mdlHurricane, mdlControlTower, mdlStormNest, mdlBarrierWall, mdlSupplyDepot, mdlLander, mdlFloodlight.
//  4. The scene sfStormValley: the map model at the origin, a dim storm light and dark ambient light. Everything
//     else (colliders, monster pathing, spawn points, the game's systems, rain, lightning) is added by the mod when
//     the scene loads.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpecialForcesBuilder
{
    const string Root = "Assets/SpecialForces";
    const string MapFbx = Root + "/StormValley.fbx";
    const string BossFbx = Root + "/Hurricane.fbx";
    const string ScenePath = Root + "/Scenes/sfStormValley.unity";
    const string SceneBundle = "sfstormvalley";
    const string AssetsBundle = "specialforcesassets";
    const string OutDir = "AssetBundles";

    static readonly string[] Props = { "ControlTower", "StormNest", "BarrierWall", "SupplyDepot", "Lander", "Floodlight" };

    // Keep in step with sf_boss_anim.py.
    static readonly HashSet<string> Looping = new HashSet<string> { "Idle", "Walk" };

    [MenuItem("Special Forces/Build Asset Bundles")]
    public static void BuildAll()
    {
        var needed = new List<string> { MapFbx, BossFbx };
        needed.AddRange(Props.Select(p => Root + "/" + p + ".fbx"));
        foreach (var path in needed)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                EditorUtility.DisplayDialog("Special Forces", "Couldn't find " + path +
                    ".\nCreate the folder Assets/SpecialForces and put all eight FBX files in it.", "OK");
                return;
            }
        }

        ConfigureStatic(MapFbx);
        foreach (var p in Props) ConfigureStatic(Root + "/" + p + ".fbx");
        ConfigureBoss();
        var clips = LoadClips();
        var controller = BuildBossController(clips);

        var bundled = new List<string>();
        bundled.Add(BuildPrefab(BossFbx, "mdlHurricane", controller));
        foreach (var p in Props) bundled.Add(BuildPrefab(Root + "/" + p + ".fbx", "mdl" + p, null));
        BuildScene();
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(OutDir);
        var builds = new[]
        {
            new AssetBundleBuild { assetBundleName = AssetsBundle, assetNames = bundled.ToArray() },
            new AssetBundleBuild { assetBundleName = SceneBundle, assetNames = new[] { ScenePath } },
        };
        BuildPipeline.BuildAssetBundles(OutDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        string dir = Path.GetFullPath(OutDir);
        Debug.Log("[SpecialForces] Built " + dir);
        EditorUtility.RevealInFinder(Path.Combine(dir, SceneBundle));
        EditorUtility.DisplayDialog("Special Forces",
            "Done!\n\nCopy these two files next to SpecialForces.dll in BepInEx/plugins/SpecialForces/:\n\n" +
            Path.Combine(dir, SceneBundle) + "\n" + Path.Combine(dir, AssetsBundle), "OK");
    }

    // ------------------------------------------------------------------ import

    static void ConfigureStatic(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.addCollider = false;                    // the mod adds colliders where they belong
        importer.indexFormat = ModelImporterIndexFormat.Auto;
        importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.SaveAndReimport();
    }

    static void ConfigureBoss()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(BossFbx);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = true;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;

        var clips = new List<ModelImporterClipAnimation>();
        foreach (var clip in importer.defaultClipAnimations)
        {
            string name = clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1);
            clip.name = name;
            clip.loopTime = Looping.Contains(name);
            clips.Add(clip);
        }
        importer.clipAnimations = clips.ToArray();
        importer.SaveAndReimport();
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(BossFbx).OfType<AnimationClip>())
            if (!c.name.StartsWith("__preview__")) clips[c.name] = c;
        Debug.Log("[SpecialForces] Hurricane clips: " + string.Join(", ", clips.Keys));
        return clips;
    }

    // -------------------------------------------------------------- controller

    static AnimatorController BuildBossController(Dictionary<string, AnimationClip> clips)
    {
        string path = Root + "/animHurricane.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters the game sets on every character (GenericCharacterMain); the mod's states set the rates.
        foreach (var b in new[] { "isMoving", "isGrounded", "isSprinting", "inCombat" })
            ac.AddParameter(new AnimatorControllerParameter { name = b, type = AnimatorControllerParameterType.Bool, defaultBool = b == "isGrounded" });
        foreach (var f in new[] { "forwardSpeed", "rightSpeed", "upSpeed", "walkSpeed", "aimPitchCycle", "aimYawCycle",
                                  "Claw.playbackRate", "Slam.playbackRate", "Roar.playbackRate", "Cast.playbackRate" })
            ac.AddParameter(new AnimatorControllerParameter { name = f, type = AnimatorControllerParameterType.Float, defaultFloat = f.EndsWith("Rate") ? 1f : 0f });

        var layers = ac.layers;
        layers[0].name = "Body";
        ac.layers = layers;
        var body = ac.layers[0].stateMachine;
        var idle = body.AddState("Idle");
        idle.motion = clips["Idle"];
        var walk = body.AddState("Walk");
        walk.motion = clips["Walk"];
        walk.speedParameterActive = true;
        walk.speedParameter = "walkSpeed";
        var death = body.AddState("Death");
        death.motion = clips["Death"];
        body.defaultState = idle;
        Transition(idle, walk, 0.25f, ("isMoving", true));
        Transition(walk, idle, 0.3f, ("isMoving", false));

        var full = new AnimatorStateMachine { name = "FullBody, Override", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(full, ac);
        ac.AddLayer(new AnimatorControllerLayer
        {
            name = "FullBody, Override", stateMachine = full, defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
        });
        var empty = full.AddState("BufferEmpty");
        full.defaultState = empty;
        foreach (var name in new[] { "Claw", "Slam", "Roar", "Cast" })
        {
            var s = full.AddState(name);
            s.motion = clips[name];
            s.speedParameterActive = true;
            s.speedParameter = name + ".playbackRate";
            var t = s.AddTransition(empty);
            t.hasExitTime = true;
            t.exitTime = 0.95f;
            t.duration = 0.15f;
        }
        EditorUtility.SetDirty(ac);
        return ac;
    }

    static void Transition(AnimatorState from, AnimatorState to, float duration, params (string param, bool value)[] conditions)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = duration;
        foreach (var c in conditions)
            t.AddCondition(c.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, c.param);
    }

    // ------------------------------------------------------------------ prefabs

    static string BuildPrefab(string fbx, string name, RuntimeAnimatorController controller)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name;
        var animator = instance.GetComponent<Animator>();
        if (controller != null)
        {
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else if (animator != null)
        {
            Object.DestroyImmediate(animator);
        }
        foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            r.updateWhenOffscreen = true;
        Directory.CreateDirectory(Root + "/Prefabs");
        string path = Root + "/Prefabs/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return path;
    }

    // -------------------------------------------------------------------- scene

    static void BuildScene()
    {
        Directory.CreateDirectory(Root + "/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var map = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MapFbx));
        PrefabUtility.UnpackPrefabInstance(map, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        map.name = "StormValley";
        map.transform.position = Vector3.zero;

        // Night: a low, cold moon behind the storm clouds (the user: "dark and under moonlight while raining").
        // The mod adds the game's night post-processing and night sky on top.
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = new Color(0.55f, 0.66f, 0.90f);
        moon.intensity = 0.38f;
        moon.shadows = LightShadows.Soft;
        moon.shadowStrength = 0.8f;
        moon.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
        var sun = moon;

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.10f, 0.13f, 0.20f);
        RenderSettings.ambientEquatorColor = new Color(0.07f, 0.09f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.05f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.07f, 0.09f, 0.13f);
        RenderSettings.fogDensity = 0.0042f;
        RenderSettings.skybox = null;
        RenderSettings.sun = sun;

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[SpecialForces] Saved scene " + ScenePath);
    }
}
