using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NextMech.Components;
using R2API;
using RoR2;
using UnityEngine;
using UnityEngine.Rendering;

namespace NextMech;

internal static class CustomModel
{
	private class Reference
	{
		public Component owner;

		public FieldInfo field;

		public string childName;

		public string targetName;
	}

	private class SkinVariant
	{
		public string suffix;

		public string token;

		public string texture;

		public Color iconA;

		public Color iconB;
	}

	public const string BundleFile = "hardpointbundle";

	public static GameObject Main;

	public static GameObject MainDisplay;

	public static readonly Dictionary<string, GameObject> Props = new Dictionary<string, GameObject>();

	public static readonly Dictionary<string, Font> Fonts = new Dictionary<string, Font>();

	public static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

	private static readonly List<CharacterModel> toConvert = new List<CharacterModel>();

	private static readonly List<Renderer> propRenderers = new List<Renderer>();

	private static readonly List<ValueTuple<CharacterModel, ScriptableObject, int>> skinParams = new List<ValueTuple<CharacterModel, ScriptableObject, int>>();

	private static readonly Dictionary<string, string> BodyBones = new Dictionary<string, string>
	{
		{ "Base", "root" },
		{ "Pelvis", "hips" },
		{ "Stomach", "spine" },
		{ "Chest", "chest" },
		{ "Neck", "head" },
		{ "Head", "head" },
		{ "HeadCenter", "head" },
		{ "HandL", "hand.l" },
		{ "HandR", "hand.r" },
		{ "UpperArmL", "arm.l" },
		{ "UpperArmR", "arm.r" },
		{ "LowerArmL", "fore.l" },
		{ "LowerArmR", "fore.r" },
		{ "ThighL", "thigh.l" },
		{ "ThighR", "thigh.r" },
		{ "CalfL", "shin.l" },
		{ "CalfR", "shin.r" },
		{ "FootL", "foot.l" },
		{ "FootR", "foot.r" },
		{ "Rifle", "rifle" },
		{ "Blade", "blade" },
		{ "Booster", "booster" },
		{ "PodL", "pod.l" },
		{ "PodR", "pod.r" }
	};

	private static readonly FieldInfo PairsField = typeof(ChildLocator).GetField("transformPairs", (BindingFlags)52);

	private static readonly SkinVariant[] Variants = new SkinVariant[3]
	{
		new SkinVariant
		{
			suffix = "Standard",
			token = "SKIN_STANDARD",
			iconA = new Color(0.8f, 0.77f, 0.7f),
			iconB = new Color(0.93f, 0.45f, 0.1f)
		},
		new SkinVariant
		{
			suffix = "Rustbelt",
			token = "SKIN_RUSTBELT",
			texture = "HardpointBodyRustbelt",
			iconA = new Color(0.72f, 0.6f, 0.42f),
			iconB = new Color(0.72f, 0.15f, 0.08f)
		},
		new SkinVariant
		{
			suffix = "Nightfall",
			token = "SKIN_NIGHTFALL",
			texture = "HardpointBodyNightfall",
			iconA = new Color(0.13f, 0.13f, 0.14f),
			iconB = new Color(0.78f, 0.08f, 0.1f)
		}
	};

	private static readonly Dictionary<ValueTuple<Material, int>, Material> skinMaterials = new Dictionary<ValueTuple<Material, int>, Material>();

	private static readonly Dictionary<ValueTuple<Material, string>, Material> retextured = new Dictionary<ValueTuple<Material, string>, Material>();

	private static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();

	public static bool Active { get; private set; }

	public static Shader GameShader { get; private set; }

	public static string[] SkinTokens
	{
		get
		{
			string[] array = new string[Variants.Length];
			for (int i = 0; i < Variants.Length; i++)
			{
				array[i] = Variants[i].token;
			}
			return array;
		}
	}

	public static void Init(string pluginDirectory)
	{
		RoR2Application.onLoad = (Action)Delegate.Combine(RoR2Application.onLoad, new Action(ConvertMaterials));
		// NEXT shares Vince's Hardpoint bundle: next to this dll, else in plugins\Hardpoint. Unity can't load the same
		// bundle twice, so if Hardpoint already loaded it we reuse that copy.
		string text = System.IO.Path.Combine(pluginDirectory, "hardpointbundle");
		if (!File.Exists(text))
		{
			text = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pluginDirectory) ?? "", "Hardpoint", "hardpointbundle");
		}
		AssetBundle bundle = null;
		foreach (AssetBundle loaded in AssetBundle.GetAllLoadedAssetBundles())
		{
			if ((bool)loaded && loaded.name == "hardpointbundle")
			{
				bundle = loaded;
				HPPlugin.Log.LogInfo("Reusing the hardpointbundle Hardpoint already loaded.");
				break;
			}
		}
		if (!bundle)
		{
			if (!File.Exists(text))
			{
				HPPlugin.Log.LogWarning("No 'hardpointbundle' found: NEXT is a tinted Commando.");
				return;
			}
			bundle = AssetBundle.LoadFromFile(text);
		}
		if (!bundle)
		{
			HPPlugin.Log.LogWarning("Could not load " + text);
			return;
		}
		Main = Load("mdlHardpoint");
		MainDisplay = Load("mdlHardpointDisplay");
		string[] array = new string[1] { "mdlHardpointMissile" };
		foreach (string text2 in array)
		{
			GameObject gameObject = Load(text2);
			if ((bool)gameObject)
			{
				Props[text2] = gameObject;
			}
		}
		array = new string[2] { "HardpointBodyRustbelt", "HardpointBodyNightfall" };
		foreach (string text3 in array)
		{
			Texture2D texture2D = bundle.LoadAsset<Texture2D>(text3);
			if ((bool)texture2D)
			{
				Textures[text3] = texture2D;
			}
		}
		array = new string[2] { "Rajdhani", "ShareTechMono" };
		foreach (string text4 in array)
		{
			Font font = bundle.LoadAsset<Font>(text4);
			if ((bool)font)
			{
				Fonts[text4] = font;
			}
		}
		Active = (bool)Main && (bool)MainDisplay;
		HPPlugin.Log.LogInfo($"Custom models: hardpoint {(bool)Main}, display {(bool)MainDisplay}, props {Props.Count}/1, " + $"textures {Textures.Count}/2, fonts {Fonts.Count}/2.");
		foreach (GameObject value in Props.Values)
		{
			TrackRenderers(value);
		}
		GameObject Load(string name)
		{
			return bundle.LoadAsset<GameObject>(name);
		}
	}

	public static GameObject Prop(string name)
	{
		if (!Props.TryGetValue(name, out var value))
		{
			return null;
		}
		return value;
	}

	public static Font GetFont(string name)
	{
		if (!Fonts.TryGetValue(name, out var value))
		{
			return null;
		}
		return value;
	}

	public static void ApplyToBody(GameObject bodyPrefab)
	{
		CharacterBody component = bodyPrefab.GetComponent<CharacterBody>();
		ModelLocator component2 = bodyPrefab.GetComponent<ModelLocator>();
		Transform modelTransform = component2.modelTransform;
		Transform parent = (component2.modelBaseTransform ? component2.modelBaseTransform : modelTransform.parent);
		CharacterModel component3 = modelTransform.GetComponent<CharacterModel>();
		ItemDisplayRuleSet itemDisplayRuleSet = (component3 ? component3.itemDisplayRuleSet : null);
		List<ValueTuple<string, Vector3, Quaternion>> oldChildren = OldChildren(modelTransform);
		Vector3 localPosition = modelTransform.localPosition;
		List<Reference> references = FindReferencesInto(bodyPrefab, modelTransform);
		UnityEngine.Object.DestroyImmediate(modelTransform.gameObject);
		GameObject gameObject = UnityEngine.Object.Instantiate(Main, parent, worldPositionStays: false);
		gameObject.name = "mdlHardpoint";
		gameObject.transform.localPosition = localPosition;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one * HPConfig.ModelScale.Value;
		component2.modelTransform = gameObject.transform;
		SetupCharacterModel(gameObject, component, null).itemDisplayRuleSet = itemDisplayRuleSet;
		Dictionary<string, Transform> dictionary = Bones(gameObject);
		List<ChildLocator.NameTransformPair> pairs = new List<ChildLocator.NameTransformPair>();
		HashSet<string> added = new HashSet<string>();
		foreach (KeyValuePair<string, string> bodyBone in BodyBones)
		{
			if (dictionary.TryGetValue(bodyBone.Value, out var value))
			{
				Add(pairs, added, bodyBone.Key, value);
			}
		}
		AddOldChildren(gameObject, oldChildren, pairs, added);
		ChildLocator locator = SetLocator(gameObject, pairs);
		SetupHurtBox(gameObject, bodyPrefab, new Vector3(0f, 1.45f, 0f), 0.7f, 2.9f, 1);
		AddSkins(gameObject, "skinNext");
		CharacterDirection component4 = bodyPrefab.GetComponent<CharacterDirection>();
		if ((bool)component4)
		{
			component4.modelAnimator = gameObject.GetComponent<Animator>();
		}
		Redirect(references, gameObject, locator);
		gameObject.AddComponent<DeathAnimation>();
	}

	public static GameObject CreateDisplay()
	{
		GameObject gameObject = MainDisplay.InstantiateClone("HardpointNextDisplayModel", registerNetwork: false);
		gameObject.transform.localScale = Vector3.one * HPConfig.ModelScale.Value;
		SetupCharacterModel(gameObject, null, null);
		AddSkins(gameObject, "skinNextDisplay");
		return gameObject;
	}

	public static Transform AttachProp(Transform parent, GameObject prop, float worldScale)
	{
		Quaternion rotation = prop.transform.rotation;
		GameObject gameObject = UnityEngine.Object.Instantiate(prop, parent, worldPositionStays: false);
		gameObject.transform.position = parent.position;
		gameObject.transform.rotation = parent.rotation * rotation;
		float num = Mathf.Max(0.0001f, parent.lossyScale.x);
		gameObject.transform.localScale = prop.transform.localScale * (worldScale / num);
		if (!GameShader)
		{
			Renderer[] componentsInChildren = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer item in componentsInChildren)
			{
				propRenderers.Add(item);
			}
		}
		ConvertNow(gameObject);
		return gameObject.transform;
	}

	public static void TrackRenderers(GameObject prefab)
	{
		Renderer[] componentsInChildren = prefab.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer item in componentsInChildren)
		{
			propRenderers.Add(item);
		}
	}

	public static Transform FindDeep(Transform root, string name)
	{
		if (root.name == name)
		{
			return root;
		}
		foreach (Transform item in root)
		{
			Transform transform = FindDeep(item, name);
			if ((bool)transform)
			{
				return transform;
			}
		}
		return null;
	}

	private static Dictionary<string, Transform> Bones(GameObject model)
	{
		Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>();
		Transform[] componentsInChildren = model.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!dictionary.ContainsKey(transform.name))
			{
				dictionary[transform.name] = transform;
			}
		}
		return dictionary;
	}

	private static ChildLocator.NameTransformPair[] GetPairs(ChildLocator locator)
	{
		if (!locator || !(PairsField != (FieldInfo)null))
		{
			return new ChildLocator.NameTransformPair[0];
		}
		return ((ChildLocator.NameTransformPair[])PairsField.GetValue((object)locator)) ?? new ChildLocator.NameTransformPair[0];
	}

	private static ChildLocator SetLocator(GameObject model, List<ChildLocator.NameTransformPair> pairs)
	{
		ChildLocator childLocator = model.AddComponent<ChildLocator>();
		if (PairsField == (FieldInfo)null)
		{
			HPPlugin.Log.LogError("ChildLocator.transformPairs not found.");
		}
		else
		{
			PairsField.SetValue((object)childLocator, (object)pairs.ToArray());
		}
		return childLocator;
	}

	private static void Add(List<ChildLocator.NameTransformPair> pairs, HashSet<string> added, string name, Transform t)
	{
		if ((bool)t && added.Add(name))
		{
			pairs.Add(new ChildLocator.NameTransformPair
			{
				name = name,
				transform = t
			});
		}
	}

	private static List<ValueTuple<string, Vector3, Quaternion>> OldChildren(Transform oldModel)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		List<ValueTuple<string, Vector3, Quaternion>> list = new List<ValueTuple<string, Vector3, Quaternion>>();
		ChildLocator.NameTransformPair[] pairs = GetPairs(oldModel.GetComponent<ChildLocator>());
		for (int i = 0; i < pairs.Length; i++)
		{
			ChildLocator.NameTransformPair nameTransformPair = pairs[i];
			if ((bool)nameTransformPair.transform)
			{
				list.Add(new ValueTuple<string, Vector3, Quaternion>(nameTransformPair.name, oldModel.InverseTransformPoint(nameTransformPair.transform.position), Quaternion.Inverse(oldModel.rotation) * nameTransformPair.transform.rotation));
			}
		}
		return list;
	}

	private static void AddOldChildren(GameObject model, List<ValueTuple<string, Vector3, Quaternion>> oldChildren, List<ChildLocator.NameTransformPair> pairs, HashSet<string> added)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		foreach (ValueTuple<string, Vector3, Quaternion> oldChild in oldChildren)
		{
			if (!added.Contains(oldChild.Item1))
			{
				Transform transform = new GameObject(oldChild.Item1).transform;
				transform.SetParent(model.transform, worldPositionStays: false);
				transform.localPosition = oldChild.Item2;
				transform.localRotation = oldChild.Item3;
				Add(pairs, added, oldChild.Item1, transform);
			}
		}
	}

	private static Transform Anchor(Transform parent, string name, Vector3 point, bool worldPosition)
	{
		Transform transform = new GameObject(name).transform;
		transform.SetParent(parent, worldPositionStays: false);
		if (worldPosition)
		{
			transform.position = point;
		}
		else
		{
			transform.localPosition = point;
		}
		return transform;
	}

	private static List<Reference> FindReferencesInto(GameObject bodyPrefab, Transform oldModel)
	{
		Dictionary<Transform, string> dictionary = new Dictionary<Transform, string>();
		ChildLocator.NameTransformPair[] pairs = GetPairs(oldModel.GetComponent<ChildLocator>());
		for (int i = 0; i < pairs.Length; i++)
		{
			ChildLocator.NameTransformPair nameTransformPair = pairs[i];
			if ((bool)nameTransformPair.transform && !dictionary.ContainsKey(nameTransformPair.transform))
			{
				dictionary[nameTransformPair.transform] = nameTransformPair.name;
			}
		}
		List<Reference> list = new List<Reference>();
		Component[] componentsInChildren = bodyPrefab.GetComponentsInChildren<Component>(includeInactive: true);
		foreach (Component component in componentsInChildren)
		{
			if (!component || component.transform.IsChildOf(oldModel))
			{
				continue;
			}
			Type type = component.GetType();
			while (type != null && type != typeof(MonoBehaviour) && type != typeof(Component))
			{
				FieldInfo[] fields = type.GetFields((BindingFlags)54);
				foreach (FieldInfo val in fields)
				{
					if (typeof(UnityEngine.Object).IsAssignableFrom(val.FieldType))
					{
						object value;
						try
						{
							value = val.GetValue((object)component);
						}
						catch
						{
							continue;
						}
						Transform transform = null;
						if (value is Component component2 && (bool)component2)
						{
							transform = component2.transform;
						}
						else if (value is GameObject gameObject && (bool)gameObject)
						{
							transform = gameObject.transform;
						}
						if ((bool)transform && transform.IsChildOf(oldModel) && (!(component is ModelLocator) || !(((MemberInfo)val).Name == "_modelTransform")))
						{
							dictionary.TryGetValue(transform, out var value2);
							list.Add(new Reference
							{
								owner = component,
								field = val,
								childName = value2,
								targetName = transform.name
							});
						}
					}
				}
				type = type.BaseType;
			}
		}
		return list;
	}

	private static void Redirect(List<Reference> references, GameObject model, ChildLocator locator)
	{
		foreach (Reference reference in references)
		{
			Transform transform = null;
			if (!string.IsNullOrEmpty(reference.childName))
			{
				transform = locator.FindChild(reference.childName);
			}
			if (!transform)
			{
				transform = locator.FindChild(reference.targetName);
			}
			if (!transform)
			{
				transform = model.transform;
			}
			object obj = null;
			Type fieldType = reference.field.FieldType;
			if (fieldType == typeof(Transform))
			{
				obj = transform;
			}
			else if (fieldType == typeof(GameObject))
			{
				obj = transform.gameObject;
			}
			else if (typeof(Component).IsAssignableFrom(fieldType))
			{
				obj = (transform.GetComponent(fieldType) ? transform.GetComponent(fieldType) : model.GetComponentInChildren(fieldType, includeInactive: true));
			}
			try
			{
				reference.field.SetValue((object)reference.owner, obj);
				HPPlugin.Log.LogInfo("Redirected " + ((MemberInfo)reference.owner.GetType()).Name + "." + ((MemberInfo)reference.field).Name + " (" + reference.targetName + ") -> " + ((obj is UnityEngine.Object @object && (bool)@object) ? @object.name : "null"));
			}
			catch (Exception ex)
			{
				HPPlugin.Log.LogWarning("Could not redirect " + ((MemberInfo)reference.owner.GetType()).Name + "." + ((MemberInfo)reference.field).Name + ": " + ex.Message);
			}
		}
	}

	private static CharacterModel SetupCharacterModel(GameObject model, CharacterBody body, string ignoreOverlays)
	{
		CharacterModel characterModel = model.AddComponent<CharacterModel>();
		characterModel.body = body;
		characterModel.autoPopulateLightInfos = true;
		List<CharacterModel.RendererInfo> list = new List<CharacterModel.RendererInfo>();
		SkinnedMeshRenderer[] componentsInChildren = model.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			list.Add(new CharacterModel.RendererInfo
			{
				renderer = skinnedMeshRenderer,
				defaultMaterial = skinnedMeshRenderer.sharedMaterial,
				defaultShadowCastingMode = ShadowCastingMode.On,
				ignoreOverlays = (skinnedMeshRenderer.name == ignoreOverlays)
			});
		}
		characterModel.baseRendererInfos = list.ToArray();
		toConvert.Add(characterModel);
		return characterModel;
	}

	private static void SetupHurtBox(GameObject model, GameObject bodyPrefab, Vector3 center, float radius, float height, int direction)
	{
		GameObject gameObject = new GameObject("MainHurtbox");
		gameObject.transform.SetParent(model.transform, worldPositionStays: false);
		gameObject.transform.localPosition = center;
		gameObject.layer = LayerIndex.entityPrecise.intVal;
		CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
		capsuleCollider.radius = radius;
		capsuleCollider.height = height;
		capsuleCollider.direction = direction;
		HurtBox hurtBox = gameObject.AddComponent<HurtBox>();
		hurtBox.healthComponent = bodyPrefab.GetComponent<HealthComponent>();
		hurtBox.isBullseye = true;
		hurtBox.isSniperTarget = true;
		hurtBox.damageModifier = HurtBox.DamageModifier.Normal;
		HurtBoxGroup hurtBoxGroup = model.AddComponent<HurtBoxGroup>();
		hurtBoxGroup.hurtBoxes = new HurtBox[1] { hurtBox };
		hurtBoxGroup.mainHurtBox = hurtBox;
		hurtBoxGroup.bullseyeCount = 1;
		HPUtil.SetField(hurtBox, "hurtBoxGroup", hurtBoxGroup);
		HPUtil.SetField(hurtBox, "indexInGroup", (short)0);
	}

	private static void AddSkins(GameObject model, string skinName)
	{
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		CharacterModel component = model.GetComponent<CharacterModel>();
		Type type = typeof(SkinDef).Assembly.GetType("RoR2.SkinDefParams");
		FieldInfo field = typeof(SkinDef).GetField("skinDefParams", (BindingFlags)52);
		SkinDef[] array = new SkinDef[Variants.Length];
		for (int i = 0; i < Variants.Length; i++)
		{
			SkinDef skinDef = ScriptableObject.CreateInstance<SkinDef>();
			skinDef.name = skinName + Variants[i].suffix;
			skinDef.nameToken = "BCM_NEXT_" + Variants[i].token;
			skinDef.rootObject = model;
			skinDef.icon = IconFactory.SkinIcon(Variants[i].iconA, Variants[i].iconB);
			ScriptableObject scriptableObject = skinDef;
			if (type != null && field != (FieldInfo)null)
			{
				scriptableObject = ScriptableObject.CreateInstance(type);
				scriptableObject.name = skinDef.name + "Params";
				field.SetValue((object)skinDef, (object)scriptableObject);
			}
			SetRendererInfos(scriptableObject, Recolor(component.baseRendererInfos, i));
			skinParams.Add(new ValueTuple<CharacterModel, ScriptableObject, int>(component, scriptableObject, i));
			array[i] = skinDef;
		}
		model.AddComponent<ModelSkinController>().skins = array;
	}

	private static CharacterModel.RendererInfo[] Recolor(CharacterModel.RendererInfo[] infos, int variant)
	{
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		CharacterModel.RendererInfo[] array = (CharacterModel.RendererInfo[])infos.Clone();
		SkinVariant skinVariant = Variants[variant];
		if (skinVariant.texture == null || !Textures.TryGetValue(skinVariant.texture, out var value))
		{
			return array;
		}
		for (int i = 0; i < array.Length; i++)
		{
			Material defaultMaterial = array[i].defaultMaterial;
			if ((bool)defaultMaterial && (bool)array[i].renderer)
			{
				if (!skinMaterials.TryGetValue(new ValueTuple<Material, int>(defaultMaterial, variant), out var value2) || !value2)
				{
					value2 = new Material(defaultMaterial)
					{
						name = defaultMaterial.name + "_" + skinVariant.suffix
					};
					value2.SetTexture("_MainTex", value);
					skinMaterials[new ValueTuple<Material, int>(defaultMaterial, variant)] = value2;
				}
				array[i].defaultMaterial = value2;
			}
		}
		return array;
	}

	public static void Retexture(Transform prop, string texture)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		if (!prop || !Textures.TryGetValue(texture, out var value))
		{
			return;
		}
		Renderer[] componentsInChildren = prop.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material sharedMaterial = renderer.sharedMaterial;
			if ((bool)sharedMaterial)
			{
				if (!retextured.TryGetValue(new ValueTuple<Material, string>(sharedMaterial, texture), out var value2) || !value2)
				{
					value2 = new Material(sharedMaterial)
					{
						name = sharedMaterial.name + "_" + texture
					};
					value2.SetTexture("_MainTex", value);
					retextured[new ValueTuple<Material, string>(sharedMaterial, texture)] = value2;
				}
				renderer.sharedMaterial = value2;
			}
		}
	}

	private static void SetRendererInfos(ScriptableObject holder, CharacterModel.RendererInfo[] infos)
	{
		FieldInfo field = holder.GetType().GetField("rendererInfos", (BindingFlags)52);
		if (field != (FieldInfo)null)
		{
			field.SetValue((object)holder, (object)(CharacterModel.RendererInfo[])infos.Clone());
		}
	}

	private static void ConvertMaterials()
	{
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		GameShader = Shader.Find("Hopoo Games/Deferred/Standard");
		if (!GameShader)
		{
			GameShader = LegacyResourcesAPI.Load<Shader>("Shaders/Deferred/HGStandard");
		}
		if (!GameShader)
		{
			HPPlugin.Log.LogWarning("Game shader not found; the custom models keep Unity's Standard shader.");
			return;
		}
		foreach (CharacterModel item4 in toConvert)
		{
			if (!item4)
			{
				continue;
			}
			CharacterModel.RendererInfo[] baseRendererInfos = item4.baseRendererInfos;
			for (int i = 0; i < baseRendererInfos.Length; i++)
			{
				baseRendererInfos[i].defaultMaterial = Get(baseRendererInfos[i].defaultMaterial);
				if ((bool)baseRendererInfos[i].renderer)
				{
					baseRendererInfos[i].renderer.sharedMaterial = baseRendererInfos[i].defaultMaterial;
				}
			}
		}
		foreach (Renderer propRenderer in propRenderers)
		{
			if ((bool)propRenderer)
			{
				propRenderer.sharedMaterial = Get(propRenderer.sharedMaterial);
			}
		}
		foreach (ValueTuple<CharacterModel, ScriptableObject, int> skinParam in skinParams)
		{
			CharacterModel item = skinParam.Item1;
			ScriptableObject item2 = skinParam.Item2;
			int item3 = skinParam.Item3;
			if ((bool)item)
			{
				SetRendererInfos(item2, Recolor(item.baseRendererInfos, item3));
			}
		}
		HPPlugin.Log.LogInfo("Converted " + converted.Count + " custom model materials to the game's shader.");
	}

	private static void ConvertNow(GameObject instance)
	{
		if (!GameShader)
		{
			return;
		}
		Renderer[] componentsInChildren = instance.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if ((bool)renderer && !(renderer is ParticleSystemRenderer))
			{
				renderer.sharedMaterial = Get(renderer.sharedMaterial);
			}
		}
	}

	private static Material Get(Material source)
	{
		if (!source || !GameShader || source.shader == GameShader)
		{
			return source;
		}
		if (!converted.TryGetValue(source, out var value))
		{
			value = (converted[source] = Convert(source));
		}
		return value;
	}

	private static Material Convert(Material source)
	{
		Material material = new Material(GameShader)
		{
			name = source.name + "_hg"
		};
		material.SetColor("_Color", source.color);
		if ((bool)source.mainTexture)
		{
			material.SetTexture("_MainTex", source.mainTexture);
			material.SetTextureScale("_MainTex", source.mainTextureScale);
			material.SetTextureOffset("_MainTex", source.mainTextureOffset);
		}
		bool flag = source.IsKeywordEnabled("_EMISSION");
		material.SetColor("_EmColor", flag ? source.GetColor("_EmissionColor") : Color.black);
		if (flag && source.HasProperty("_EmissionMap") && (bool)source.GetTexture("_EmissionMap"))
		{
			material.SetTexture("_EmTex", source.GetTexture("_EmissionMap"));
		}
		material.SetFloat("_EmPower", flag ? 1.5f : 0f);
		if (source.HasProperty("_Metallic") && source.GetFloat("_Metallic") > 0.5f && material.HasProperty("_Smoothness"))
		{
			material.SetFloat("_Smoothness", 0.7f);
		}
		material.SetFloat("_LimbRemovalOn", 0f);
		material.DisableKeyword("LIMBREMOVAL");
		return material;
	}

	public static Material Glow(Color color, Color emission, float power, bool twoSided = false)
	{
		Material material = new Material(GameShader ? GameShader : Shader.Find("Standard"));
		material.SetColor("_Color", color);
		if (material.HasProperty("_EmColor"))
		{
			material.SetColor("_EmColor", emission);
		}
		if (material.HasProperty("_EmPower"))
		{
			material.SetFloat("_EmPower", power);
		}
		if (twoSided && material.HasProperty("_Cull"))
		{
			material.SetFloat("_Cull", 0f);
		}
		return material;
	}
}
