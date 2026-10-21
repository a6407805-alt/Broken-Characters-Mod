// Ashwing Drake asset bundle builder (Unity 2021.3.45f2 editor script - the game's exact version).
//
// Setup: put AshwingDrake.fbx, IceMeteor.fbx, IceCrystals.fbx and the Textures folder in Assets/AshwingDrake/,
// and this file in Assets/Editor/. Then use the menu
//   Ashwing Drake > Build Asset Bundle
// The bundle is written to <project>/AssetBundles/ashwingdrakebundle. Copy it next to AshwingDrake.dll.
//
// What it does (same approach as the Nidus / Infested General builders):
//  1. Import settings: the drake as a Generic rig (root bone "root") with named clips; the props as static meshes.
//  2. Materials: the drake's textured material (base colour + normal map from the Lite pack) and plain ice
//     materials (the mod swaps them for the game's own shader at runtime).
//  3. Animator controllers: in-game (layers Body, "Gesture, Override" = neck/head/jaw, "FullBody, Override")
//     and lobby. State and parameter names match what the game and the mod's skills use.
//  4. Prefabs mdlAshwingDrake, mdlAshwingDrakeDisplay, mdlIceMeteor, mdlIceCrystals in one asset bundle.
//
// The drake model is Literock Studios' Ashwing Drake Lite: free to use and modify inside a game or mod, but the
// model files themselves may not be re-shared (see Source/LICENSE.txt). Shipping the built bundle inside the mod
// is allowed; don't post this Unity folder or the FBX publicly.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AshwingDrakeBuilder
{
    const string Root = "Assets/AshwingDrake";
    const string DrakeFbx = Root + "/AshwingDrake.fbx";
    const string BundleName = "ashwingdrakebundle";
    const string OutDir = "AssetBundles";

    static readonly (string fbx, string prefab)[] Props =
    {
        (Root + "/IceMeteor.fbx", "mdlIceMeteor"),
        (Root + "/IceCrystals.fbx", "mdlIceCrystals"),
    };

    // Keep in step with LOOPING in Model/ad_anim.py.
    static readonly HashSet<string> Looping = new HashSet<string>
    {
        "Idle", "Walk", "Fly", "Breath", "FlyBreath", "FlyCarry", "Dive", "MeteorPush", "Glide",
    };

    // Plain materials for the props: name -> (colour, emission). Keep in step with COLORS in Model/ad_props.py.
    static readonly Dictionary<string, (Color color, float emission)> IceColors = new Dictionary<string, (Color, float)>
    {
        { "matIceRock", (new Color(0.55f, 0.70f, 0.85f), 0f) },
        { "matIceCore", (new Color(0.55f, 0.90f, 1.00f), 4f) },
        { "matIceCrystal", (new Color(0.70f, 0.92f, 1.00f), 1.5f) },
    };

    // Renderer name -> its material slots, in the FBX's slot order.
    static readonly Dictionary<string, string[]> RendererMaterials = new Dictionary<string, string[]>
    {
        { "AshwingBody", new[] { "matAshwingDrake" } },
        { "IceMeteor", new[] { "matIceRock", "matIceCore" } },
        { "IceCrystals", new[] { "matIceCrystal" } },
    };

    [MenuItem("Ashwing Drake/Build Asset Bundle")]
    public static void BuildAll()
    {
        foreach (var path in new[] { DrakeFbx }.Concat(Props.Select(p => p.fbx)))
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                EditorUtility.DisplayDialog("Ashwing Drake", "Couldn't find " + path +
                    ".\nCreate the folder Assets/AshwingDrake and put the three FBX files and the Textures folder in it.", "OK");
                return;
            }
        }

        ConfigureTextures();
        ConfigureDrakeImport();
        foreach (var prop in Props) ConfigurePropImport(prop.fbx);
        var clips = LoadClips();
        var materials = CreateMaterials();
        var neckMask = CreateMask("maskNeck", p => p.Contains("/neck_01"));
        var controller = BuildController(clips, neckMask);
        var displayController = BuildDisplayController(clips);
        BuildPrefab(DrakeFbx, "mdlAshwingDrake", controller, materials);
        BuildPrefab(DrakeFbx, "mdlAshwingDrakeDisplay", displayController, materials);
        foreach (var prop in Props) BuildPrefab(prop.fbx, prop.prefab, null, materials);
        AssetDatabase.SaveAssets();

        Directory.CreateDirectory(OutDir);
        BuildPipeline.BuildAssetBundles(OutDir, BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
        string bundle = Path.GetFullPath(Path.Combine(OutDir, BundleName));
        Debug.Log("[AshwingDrake] Built " + bundle);
        EditorUtility.RevealInFinder(bundle);
        EditorUtility.DisplayDialog("Ashwing Drake",
            "Done!\n\nCopy this file next to AshwingDrake.dll in BepInEx/plugins/AshwingDrake/:\n\n" + bundle, "OK");
    }

    // ------------------------------------------------------------------ import

    static void ConfigureTextures()
    {
        foreach (var name in new[] { "Ashwing_BaseColor", "Ashwing_Normal" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "/Textures/" + name + ".png");
            if (importer == null) { Debug.LogWarning("[AshwingDrake] Missing texture " + name); continue; }
            importer.textureType = name.EndsWith("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !name.EndsWith("Normal");
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }
    }

    static void ConfigureDrakeImport()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(DrakeFbx);
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.isReadable = true; // the game's particle effects (spawn, elite auras) emit from the mesh
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // No motion node: the clips move the "root" bone (bob, death slump) and that motion must stay on the bone.
        importer.optimizeGameObjects = false;
        importer.importAnimation = true;
        importer.resampleCurves = true;
        importer.animationCompression = ModelImporterAnimationCompression.Off;

        var clips = new List<ModelImporterClipAnimation>();
        foreach (var clip in importer.defaultClipAnimations)
        {
            // Takes come out as "AshwingRig|AshwingRig|Idle" (and "Key|..." morph takes, which are skipped).
            if (clip.takeName.StartsWith("Key|")) continue;
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
        importer.isReadable = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.SaveAndReimport();
    }

    static Dictionary<string, AnimationClip> LoadClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var c in AssetDatabase.LoadAllAssetsAtPath(DrakeFbx).OfType<AnimationClip>())
            if (!c.name.StartsWith("__preview__")) clips[c.name] = c;
        Debug.Log("[AshwingDrake] Clips: " + string.Join(", ", clips.Keys));
        return clips;
    }

    // --------------------------------------------------------------- materials

    static Dictionary<string, Material> CreateMaterials()
    {
        Directory.CreateDirectory(Root + "/Materials");
        var result = new Dictionary<string, Material>();

        var drake = LoadOrCreate("matAshwingDrake");
        drake.color = Color.white;
        drake.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Ashwing_BaseColor.png"));
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Ashwing_Normal.png");
        if (normal != null)
        {
            drake.SetTexture("_BumpMap", normal);
            drake.EnableKeyword("_NORMALMAP");
        }
        drake.SetFloat("_Metallic", 0.1f);
        drake.SetFloat("_Glossiness", 0.35f);
        EditorUtility.SetDirty(drake);
        result["matAshwingDrake"] = drake;

        foreach (var pair in IceColors)
        {
            var mat = LoadOrCreate(pair.Key);
            mat.color = pair.Value.color;
            mat.SetFloat("_Glossiness", 0.8f);
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

    static Material LoadOrCreate(string name)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    // -------------------------------------------------------------------- masks

    static AvatarMask CreateMask(string name, System.Func<string, bool> active)
    {
        string path = Root + "/" + name + ".mask";
        AssetDatabase.DeleteAsset(path);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(DrakeFbx);
        var mask = new AvatarMask();
        mask.AddTransformPath(model.transform, true);
        for (int i = 0; i < mask.transformCount; i++)
            mask.SetTransformActive(i, active(mask.GetTransformPath(i)));
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }

    // -------------------------------------------------------------- controllers

    static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask neck)
    {
        string path = Root + "/animAshwingDrake.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);

        // The game sets these every frame (GenericCharacterMain); the mod sets isAerial and isCarrying.
        AddBool(ac, "isMoving", false);
        AddBool(ac, "isGrounded", true);
        AddBool(ac, "isSprinting", false);
        AddBool(ac, "isAerial", false);
        AddBool(ac, "isCarrying", false);
        AddFloat(ac, "forwardSpeed", 0f);
        AddFloat(ac, "rightSpeed", 0f);
        AddFloat(ac, "upSpeed", 0f);
        foreach (var rate in new[] { "Bite.playbackRate", "Grab.playbackRate", "Corkscrew.playbackRate", "Roar.playbackRate" })
            AddFloat(ac, rate, 1f);

        // ---- Body (base layer): movement on the ground and in the air
        var layers = ac.layers;
        layers[0].name = "Body";
        ac.layers = layers;
        var body = ac.layers[0].stateMachine;

        var grounded = ac.CreateBlendTreeInController("Grounded", out BlendTree moveTree, 0);
        moveTree.blendType = BlendTreeType.FreeformDirectional2D;
        moveTree.blendParameter = "rightSpeed";
        moveTree.blendParameterY = "forwardSpeed";
        moveTree.AddChild(clips["Idle"], new Vector2(0f, 0f));
        foreach (var dir in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            moveTree.AddChild(clips["Walk"], dir);

        var sprint = body.AddState("Sprint");
        sprint.motion = clips["Walk"];
        sprint.speed = 1.7f;
        var glide = body.AddState("Glide");      // ground mode, off the ground (falling, after a jump)
        glide.motion = clips["Glide"];
        var jump = body.AddState("Jump");
        jump.motion = clips["Launch"];
        var bonusJump = body.AddState("BonusJump");
        bonusJump.motion = clips["Launch"];
        var fly = body.AddState("Fly");          // aerial mode
        fly.motion = clips["Fly"];
        var carry = body.AddState("FlyCarry");   // aerial mode, holding prey
        carry.motion = clips["FlyCarry"];
        var death = body.AddState("Death");
        death.motion = clips["Death"];
        body.defaultState = grounded;

        Transition(grounded, sprint, 0.15f, ("isSprinting", true), ("isMoving", true));
        Transition(sprint, grounded, 0.15f, ("isSprinting", false));
        Transition(sprint, grounded, 0.15f, ("isMoving", false));
        Transition(grounded, glide, 0.25f, ("isGrounded", false), ("isAerial", false));
        Transition(sprint, glide, 0.25f, ("isGrounded", false), ("isAerial", false));
        Transition(glide, grounded, 0.2f, ("isGrounded", true));
        foreach (var j in new[] { jump, bonusJump })
        {
            var t = j.AddTransition(glide);
            t.hasExitTime = true;
            t.exitTime = 0.85f;
            t.duration = 0.2f;
        }
        foreach (var s in new[] { grounded, sprint, glide, jump, bonusJump })
            Transition(s, fly, 0.3f, ("isAerial", true));
        Transition(fly, carry, 0.2f, ("isCarrying", true));
        Transition(carry, fly, 0.25f, ("isCarrying", false));
        Transition(fly, grounded, 0.3f, ("isAerial", false), ("isGrounded", true));
        Transition(fly, glide, 0.3f, ("isAerial", false), ("isGrounded", false));
        Transition(carry, glide, 0.3f, ("isAerial", false));

        // ---- Gesture, Override: neck, head and jaw only (bite and breath while the legs keep moving)
        var gesture = AddLayer(ac, "Gesture, Override", neck);
        var gestureEmpty = gesture.AddState("BufferEmpty");
        gesture.defaultState = gestureEmpty;
        ActionState(gesture, gestureEmpty, clips["Bite"], "Bite", "Bite.playbackRate", true);
        ActionState(gesture, gestureEmpty, clips["Breath"], "Breath", null, false);       // held; the skill ends it
        ActionState(gesture, gestureEmpty, clips["FlyBreath"], "FlyBreath", null, false); // held; the skill ends it

        // ---- FullBody, Override: whole-body moves
        var full = AddLayer(ac, "FullBody, Override", null);
        var fullEmpty = full.AddState("BufferEmpty");
        full.defaultState = fullEmpty;
        ActionState(full, fullEmpty, clips["FlyGrab"], "FlyGrab", "Grab.playbackRate", true);
        ActionState(full, fullEmpty, clips["Launch"], "CorkscrewUp", "Corkscrew.playbackRate", false);
        ActionState(full, fullEmpty, clips["Dive"], "CorkscrewDown", "Corkscrew.playbackRate", false);
        ActionState(full, fullEmpty, clips["MeteorPush"], "MeteorPush", null, false);
        ActionState(full, fullEmpty, clips["Roar"], "Roar", "Roar.playbackRate", true);

        EditorUtility.SetDirty(ac);
        return ac;
    }

    static AnimatorController BuildDisplayController(Dictionary<string, AnimationClip> clips)
    {
        string path = Root + "/animAshwingDrakeDisplay.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm = ac.layers[0].stateMachine;
        var roar = sm.AddState("Roar");   // roars once when picked, then idles
        roar.motion = clips["Roar"];
        var idle = sm.AddState("Idle");
        idle.motion = clips["Idle"];
        sm.defaultState = roar;
        var t = roar.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.duration = 0.25f;
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

    // An action state; one-shots return to BufferEmpty when they finish, held ones wait for the skill.
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

    static void BuildPrefab(string fbx, string name, RuntimeAnimatorController controller, Dictionary<string, Material> materials)
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

        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            string key = r.name.Split('.')[0];
            if (RendererMaterials.TryGetValue(key, out var slots))
                r.sharedMaterials = slots.Select(s => materials[s]).ToArray();
            else
                Debug.LogWarning("[AshwingDrake] No material for renderer " + r.name);
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
        }

        string path = Root + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant(BundleName, "");
    }
}
