// Infested General asset bundle builder (Unity 2021.3.45f2 editor script - the game's exact version).
//
// Setup: put the four FBX files (InfestedGeneral.fbx, Baneling.fbx, AutoTurret.fbx, CommandCenter.fbx) in
// Assets/InfestedGeneral/ and this file in Assets/Editor/. Then use the menu
//   Infested General > Build Asset Bundle
// The bundle is written to <project>/AssetBundles/infestedgeneralbundle. Copy it next to InfestedGeneral.dll.
//
// What it does (same approach as the Reaper builder):
//  1. Import settings: the General as a Generic rig with named clips; the props as static meshes.
//  2. Simple materials (the mod swaps them for the game's own shader at runtime).
//  3. Animator controllers: in-game (layers Body, "Gesture, Override" upper body, "FullBody, Override")
//     and lobby. State and parameter names match what the game and the mod's skills use.
//  4. Prefabs mdlInfestedGeneral, mdlInfestedGeneralDisplay, mdlBaneling, mdlAutoTurret, mdlCommandCenter,
//     all in one asset bundle.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class InfestedGeneralBuilder
{
    const string Root = "Assets/InfestedGeneral";
    const string GeneralFbx = Root + "/InfestedGeneral.fbx";
    const string BundleName = "infestedgeneralbundle";
    const string OutDir = "AssetBundles";

    static readonly (string fbx, string prefab)[] Props =
    {
        (Root + "/Baneling.fbx", "mdlBaneling"),
        (Root + "/AutoTurret.fbx", "mdlAutoTurret"),
        (Root + "/CommandCenter.fbx", "mdlCommandCenter"),
    };

    static readonly HashSet<string> Looping = new HashSet<string>
    {
        "Idle", "Run", "RunBack", "RunLeft", "RunRight", "Sprint", "Ascend", "Descend", "Lobby",
    };

    // material name -> (colour, emission). Keep in step with MATERIALS in Model/ig_rig.py and ig_props.py.
    static readonly Dictionary<string, (Color color, float emission)> MaterialColors = new Dictionary<string, (Color, float)>
    {
        { "matIGArmor", (new Color(0.09f, 0.17f, 0.52f), 0f) },
        { "matIGArmorDark", (new Color(0.04f, 0.06f, 0.14f), 0f) },
        { "matIGFlesh", (new Color(0.36f, 0.15f, 0.11f), 0f) },
        { "matIGFleshDark", (new Color(0.16f, 0.05f, 0.05f), 0f) },
        { "matIGBone", (new Color(0.86f, 0.78f, 0.62f), 0f) },
        { "matIGRifle", (new Color(0.10f, 0.10f, 0.12f), 0f) },
        { "matIGBayonet", (new Color(0.55f, 0.58f, 0.62f), 0f) },
        { "matIGGlow", (new Color(0.62f, 0.35f, 1.0f), 4f) },
        { "matIGRifleLight", (new Color(1.0f, 0.78f, 0.3f), 4f) },
        { "matBanelingSac", (new Color(0.42f, 0.95f, 0.12f), 1.5f) },
        { "matBanelingShell", (new Color(0.14f, 0.07f, 0.11f), 0f) },
        { "matBanelingBone", (new Color(0.82f, 0.76f, 0.66f), 0f) },
        { "matTurretMetal", (new Color(0.72f, 0.74f, 0.78f), 0f) },
        { "matTurretBlue", (new Color(0.1f, 0.2f, 0.5f), 0f) },
        { "matTurretDark", (new Color(0.07f, 0.07f, 0.08f), 0f) },
        { "matTurretLight", (new Color(0.4f, 0.8f, 1.0f), 4f) },
        { "matCCRed", (new Color(0.48f, 0.05f, 0.05f), 0f) },
        { "matCCDark", (new Color(0.09f, 0.09f, 0.1f), 0f) },
        { "matCCMetal", (new Color(0.45f, 0.47f, 0.5f), 0f) },
        { "matCCHazard", (new Color(0.85f, 0.62f, 0.08f), 0f) },
        { "matCCLight", (new Color(1.0f, 0.78f, 0.35f), 4f) },
        { "matCCFlesh", (new Color(0.36f, 0.15f, 0.11f), 0f) },
    };

    [MenuItem("Infested General/Build Asset Bundle")]
    public static void BuildAll()
    {
        foreach (var path in new[] { GeneralFbx }.Concat(Props.Select(p => p.fbx)))
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                EditorUtility.DisplayDialog("Infested General", "Couldn't find " + path +
                    ".\nCreate the folder Assets/InfestedGeneral and put the four FBX files in it.", "OK");
                return;
            }
        }

        ConfigureGeneralImport();
        foreach (var prop in Props) ConfigurePropImport(prop.fbx);
        var clips = LoadClips();
        var materials = CreateMaterials();
        var mask = CreateUpperBodyMask();
        var controller = BuildController(clips, mask);
        var displayController = BuildDisplayController(clips);
        BuildPrefab(GeneralFbx, "mdlInfestedGeneral", controller, materials);
        BuildPrefab(GeneralFbx, "mdlInfestedGeneralDisplay", displayController, materials);
        foreach (var prop in Props) BuildPrefab(prop.fbx, prop.prefab, null, materials);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(OutDir);
        BuildPipeline.BuildAssetBundles(OutDir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        string bundle = Path.GetFullPath(Path.Combine(OutDir, BundleName));
        Debug.Log("[InfestedGeneral] Built " + bundle);
        EditorUtility.RevealInFinder(bundle);
        EditorUtility.DisplayDialog("Infested General",
            "Done!\n\nCopy this file next to InfestedGeneral.dll in BepInEx/plugins/InfestedGeneral/:\n\n" + bundle, "OK");
    }

    // ------------------------------------------------------------------ import

    static void ConfigureGeneralImport()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(GeneralFbx);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = true; // the game's particle effects (spawn, elite auras) emit from the mesh
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
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

    static void ConfigurePropImport(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = true; // the game's particle effects (spawn, elite auras) emit from the mesh
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.SaveAndReimport();
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(GeneralFbx)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name, c => c);
        Debug.Log("[InfestedGeneral] Clips: " + string.Join(", ", clips.Keys));
        return clips;
    }

    // --------------------------------------------------------------- materials

    static Dictionary<string, Material> CreateMaterials()
    {
        Directory.CreateDirectory(Root + "/Materials");
        var result = new Dictionary<string, Material>();
        foreach (var pair in MaterialColors)
        {
            string path = Root + "/Materials/" + pair.Key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = pair.Value.color;
            if (pair.Value.emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", pair.Value.color * pair.Value.emission);
            }
            EditorUtility.SetDirty(mat);
            result[pair.Key] = mat;
        }
        return result;
    }

    // -------------------------------------------------------------------- mask

    static AvatarMask CreateUpperBodyMask()
    {
        string path = Root + "/maskUpperBody.mask";
        AssetDatabase.DeleteAsset(path);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(GeneralFbx);
        var mask = new AvatarMask();
        mask.AddTransformPath(model.transform, true);
        for (int i = 0; i < mask.transformCount; i++)
        {
            string p = mask.GetTransformPath(i);
            mask.SetTransformActive(i, p.Contains("/stomach"));
        }
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }

    // -------------------------------------------------------------- controllers

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask upperBody)
    {
        string path = Root + "/animInfestedGeneral.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        AddBool(ac, "isMoving", false);
        AddBool(ac, "isGrounded", true);
        AddBool(ac, "isSprinting", false);
        AddFloat(ac, "forwardSpeed", 0f);
        AddFloat(ac, "rightSpeed", 0f);
        AddFloat(ac, "upSpeed", 0f);
        foreach (var rate in new[] { "FireRifle.playbackRate", "Reload.playbackRate", "Throw.playbackRate" })
            AddFloat(ac, rate, 1f);

        // ---- Body (base layer): movement
        var layers = ac.layers;
        layers[0].name = "Body";
        ac.layers = layers;
        var body = ac.layers[0].stateMachine;

        var grounded = ac.CreateBlendTreeInController("Grounded", out BlendTree moveTree, 0);
        moveTree.blendType = BlendTreeType.FreeformDirectional2D;
        moveTree.blendParameter = "rightSpeed";
        moveTree.blendParameterY = "forwardSpeed";
        moveTree.AddChild(clips["Idle"], new Vector2(0f, 0f));
        moveTree.AddChild(clips["Run"], new Vector2(0f, 1f));
        moveTree.AddChild(clips["RunBack"], new Vector2(0f, -1f));
        moveTree.AddChild(clips["RunLeft"], new Vector2(-1f, 0f));
        moveTree.AddChild(clips["RunRight"], new Vector2(1f, 0f));

        var air = ac.CreateBlendTreeInController("Air", out BlendTree airTree, 0);
        airTree.blendType = BlendTreeType.Simple1D;
        airTree.blendParameter = "upSpeed";
        airTree.useAutomaticThresholds = false;
        airTree.AddChild(clips["Descend"], -3f);
        airTree.AddChild(clips["Ascend"], 3f);

        var sprint = body.AddState("Sprint");
        sprint.motion = clips["Sprint"];
        var jump = body.AddState("Jump");
        jump.motion = clips["Jump"];
        var bonusJump = body.AddState("BonusJump");
        bonusJump.motion = clips["Jump"];
        var death = body.AddState("Death");
        death.motion = clips["Death"];
        body.defaultState = grounded;

        Transition(grounded, sprint, 0.15f, ("isSprinting", true), ("isMoving", true));
        Transition(sprint, grounded, 0.15f, ("isSprinting", false));
        Transition(sprint, grounded, 0.15f, ("isMoving", false));
        Transition(grounded, air, 0.2f, ("isGrounded", false));
        Transition(sprint, air, 0.2f, ("isGrounded", false));
        Transition(air, grounded, 0.15f, ("isGrounded", true));
        foreach (var j in new[] { jump, bonusJump })
        {
            var t = j.AddTransition(air);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.15f;
        }

        // ---- Gesture, Override: upper body (shooting, reloading and throwing while moving)
        var gesture = AddLayer(ac, "Gesture, Override", upperBody);
        var gestureEmpty = gesture.AddState("BufferEmpty");
        gesture.defaultState = gestureEmpty;
        ActionState(gesture, gestureEmpty, clips["FireRifle"], "FireRifle", "FireRifle.playbackRate", true);
        ActionState(gesture, gestureEmpty, clips["Reload"], "Reload", "Reload.playbackRate", true);
        ActionState(gesture, gestureEmpty, clips["ThrowBaneling"], "ThrowBaneling", "Throw.playbackRate", true);

        // ---- FullBody, Override: whole-body skills
        var full = AddLayer(ac, "FullBody, Override", null);
        var fullEmpty = full.AddState("BufferEmpty");
        full.defaultState = fullEmpty;
        ActionState(full, fullEmpty, clips["DropTurrets"], "DropTurrets", null, true);
        ActionState(full, fullEmpty, clips["CallCommandCenter"], "CallCommandCenter", null, true);

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorController BuildDisplayController(Dictionary<string, AnimationClip> clips)
    {
        string path = Root + "/animInfestedGeneralDisplay.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ac.layers[0].stateMachine;
        var lobby = sm.AddState("Lobby");
        lobby.motion = clips["Lobby"];
        sm.defaultState = lobby;
        EditorUtility.SetDirty(ac);
        return ac;
    }

    static void AddBool(AnimatorController ac, string name, bool value) =>
        ac.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Bool, defaultBool = value });

    static void AddFloat(AnimatorController ac, string name, float value) =>
        ac.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = value });

    static AnimatorStateMachine AddLayer(AnimatorController ac, string name, AvatarMask mask)
    {
        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, ac);
        ac.AddLayer(new AnimatorControllerLayer
        {
            name = name,
            stateMachine = sm,
            defaultWeight = 1f,
            blendingMode = AnimatorLayerBlendingMode.Override,
            avatarMask = mask,
        });
        return sm;
    }

    // A one-shot action state that returns to BufferEmpty when it finishes.
    static void ActionState(AnimatorStateMachine sm, AnimatorState empty, AnimationClip clip, string name, string speedParam, bool returnWhenDone)
    {
        var state = sm.AddState(name);
        state.motion = clip;
        if (speedParam != null)
        {
            state.speedParameterActive = true;
            state.speedParameter = speedParam;
        }
        if (returnWhenDone)
        {
            var t = state.AddTransition(empty);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.12f;
        }
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

    // Material for a renderer: "IGArmor" -> matIGArmor, "TurretMetal.Head" -> matTurretMetal.
    static Material MaterialFor(string rendererName, Dictionary<string, Material> materials)
    {
        string baseName = rendererName.Split('.')[0];
        return materials.TryGetValue("mat" + baseName, out var mat) ? mat : null;
    }

    static void BuildPrefab(string fbx, string name, RuntimeAnimatorController controller, Dictionary<string, Material> materials)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = name;

        if (controller != null)
        {
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        else
        {
            var animator = instance.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
        }

        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            var mat = MaterialFor(r.name, materials);
            if (mat != null) r.sharedMaterial = mat;
            else Debug.LogWarning("[InfestedGeneral] No material for renderer " + r.name);
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }

        string path = Root + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(BundleName, "");
    }
}
