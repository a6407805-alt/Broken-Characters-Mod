using System;
using System.Collections.Generic;
using System.Linq;
using EntityStates;
using NextMech.Components;
using NextMech.SkillStates;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using UnityEngine;

namespace NextMech;

internal static class HPContent
{
	public const string Prefix = "BCM_NEXT_";

	public static readonly Color BodyColor = new Color(0.35f, 0.9f, 1f);

	public static GameObject BodyPrefab;

	public static GameObject DisplayPrefab;

	public static GameObject SoundCueEffect;

	public static GameObject MissileProjectile;

	public static BuffDef StaggerBuff;

	public static BuffDef OverloadBuff;

	public static BuffDef ArmorBuff;

	public static SurvivorDef Survivor;

	public static void Create()
	{
		if (!HPAssets.CommandoBody)
		{
			HPPlugin.Log.LogError("Commando could not be loaded; Hardpoint will not be added.");
			return;
		}
		Type[] array = new Type[]
		{
			typeof(HPMain),
			typeof(FireRifle),
			typeof(ReloadRifle),
			typeof(PulseBlade),
			typeof(QuickBoost),
			typeof(AssaultBoost),
			typeof(BoostKick),
			typeof(MissileLock),
			typeof(MissileFire),
			typeof(AssaultArmor),
			typeof(OveredBoost),
			typeof(FireShotgun),
			typeof(FireLinear)
		};
		for (int i = 0; i < array.Length; i++)
		{
			ContentAddition.AddEntityState(array[i], out var _);
		}
		Try("survivor body", CreateBody);
		if ((bool)BodyPrefab)
		{
			Try("character select model", CreateDisplay);
			Try("buffs", CreateBuffs);
			Try("skills", CreateSkills);
			Try("survivor", CreateSurvivor);
			Try("effects", CreateEffects);
			Try("missiles", CreateMissile);
			RecalculateStatsAPI.GetStatCoefficients += Stats;
			RecalculateStatsAPI.GetStatCoefficients += Garage.Stats;
		}
	}

	public static void Try(string what, Action build)
	{
		try
		{
			build();
		}
		catch (Exception arg)
		{
			HPPlugin.Log.LogError($"Failed to build the {what}: {arg}");
		}
	}

	private static void CreateBody()
	{
		BodyPrefab = HPAssets.CommandoBody.InstantiateClone("HardpointNextBody", registerNetwork: true);
		CharacterBody component = BodyPrefab.GetComponent<CharacterBody>();
		component.baseNameToken = "BCM_NEXT_NAME";
		component.subtitleNameToken = "BCM_NEXT_SUBTITLE";
		component.portraitIcon = HPAssets.Portrait;
		component.bodyColor = BodyColor;
		component.baseMaxHealth = HPConfig.BaseHealth.Value;
		component.levelMaxHealth = HPConfig.HealthPerLevel.Value;
		component.baseRegen = HPConfig.BaseRegen.Value;
		component.levelRegen = HPConfig.BaseRegen.Value * 0.2f;
		component.baseDamage = HPConfig.BaseDamage.Value;
		component.levelDamage = HPConfig.DamagePerLevel.Value;
		component.baseArmor = HPConfig.BaseArmor.Value;
		component.levelArmor = 0f;
		component.baseMoveSpeed = HPConfig.MoveSpeed.Value;
		component.sprintingSpeedMultiplier = HPConfig.SprintMultiplier.Value;
		if (HPConfig.AcMovement.Value)
		{
			component.bodyFlags |= CharacterBody.BodyFlags.SprintAnyDirection;
		}
		if (CustomModel.Active)
		{
			try
			{
				CustomModel.ApplyToBody(BodyPrefab);
				AddMuzzle();
			}
			catch (Exception ex)
			{
				HPPlugin.Log.LogError("Custom model failed on Hardpoint: " + ex);
			}
		}
		else
		{
			HPUtil.TintModel(BodyPrefab, new Color(0.9f, 0.85f, 0.75f));
		}
		EntityStateMachine entityStateMachine = HPUtil.FindMachine(BodyPrefab, "Body");
		if ((bool)entityStateMachine)
		{
			entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(HPMain));
		}
		AddMachine("Boost");
		AddMachine("Pods");
		CameraTargetParams component2 = BodyPrefab.GetComponent<CameraTargetParams>();
		if ((bool)component2 && (bool)component2.cameraParams)
		{
			CharacterCameraParams characterCameraParams = UnityEngine.Object.Instantiate(component2.cameraParams);
			characterCameraParams.name = "ccpNext";
			characterCameraParams.data.idealLocalCameraPos = new Vector3(0f, 0f, 0f - HPConfig.CameraDistance.Value);
			characterCameraParams.data.pivotVerticalOffset = 0f;
			component2.cameraParams = characterCameraParams;
			Transform transform = new GameObject("HardpointNextCameraPivot").transform;
			transform.SetParent(BodyPrefab.transform, worldPositionStays: false);
			transform.localPosition = Vector3.up * (HPConfig.CameraHeight.Value * HPConfig.ModelScale.Value - 0.9f);
			component2.cameraPivotTransform = transform;
			HPUtil.SetField(component2, "dontRaycastToPivot", true);
		}
		BodyPrefab.AddComponent<SpawnReport>();
		BodyPrefab.AddComponent<HPVoice>();
		BodyPrefab.AddComponent<HPStatus>();
		BodyPrefab.AddComponent<NextPilot>();
		BodyPrefab.AddComponent<PaField>();
		ContentAddition.AddBody(BodyPrefab);
	}

	private static void AddMuzzle()
	{
		Transform modelTransform = BodyPrefab.GetComponent<ModelLocator>().modelTransform;
		ChildLocator childLocator = (modelTransform ? modelTransform.GetComponent<ChildLocator>() : null);
		Transform transform = (childLocator ? childLocator.FindChild("Rifle") : null);
		if ((bool)transform)
		{
			Transform transform2 = new GameObject("MuzzleRifle").transform;
			transform2.SetParent(transform, worldPositionStays: false);
			transform2.localPosition = new Vector3(0f, 1.38f, 0.12f);
			ChildLocator.NameTransformPair[] array = HPUtil.GetField<ChildLocator.NameTransformPair[]>(childLocator, "transformPairs") ?? new ChildLocator.NameTransformPair[0];
			HPUtil.SetField(childLocator, "transformPairs", Enumerable.ToArray<ChildLocator.NameTransformPair>(Enumerable.Concat<ChildLocator.NameTransformPair>((IEnumerable<ChildLocator.NameTransformPair>)array, (IEnumerable<ChildLocator.NameTransformPair>)new ChildLocator.NameTransformPair[1]
			{
				new ChildLocator.NameTransformPair
				{
					name = "MuzzleRifle",
					transform = transform2
				}
			})));
		}
	}

	private static void AddMachine(string name)
	{
		EntityStateMachine entityStateMachine = BodyPrefab.AddComponent<EntityStateMachine>();
		entityStateMachine.customName = name;
		entityStateMachine.initialStateType = new SerializableEntityStateType(typeof(Idle));
		entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(Idle));
		NetworkStateMachine component = BodyPrefab.GetComponent<NetworkStateMachine>();
		EntityStateMachine[] array = (component ? HPUtil.GetField<EntityStateMachine[]>(component, "stateMachines") : null);
		if (array == null)
		{
			HPPlugin.Log.LogWarning("NetworkStateMachine.stateMachines not found: the " + name + " machine won't sync in multiplayer.");
			return;
		}
		HPUtil.SetField(component, "stateMachines", Enumerable.ToArray<EntityStateMachine>(Enumerable.Concat<EntityStateMachine>((IEnumerable<EntityStateMachine>)array, (IEnumerable<EntityStateMachine>)new EntityStateMachine[1] { entityStateMachine })));
	}

	private static void CreateDisplay()
	{
		if (CustomModel.Active)
		{
			try
			{
				DisplayPrefab = CustomModel.CreateDisplay();
			}
			catch (Exception ex)
			{
				HPPlugin.Log.LogError("Custom display model failed: " + ex);
			}
		}
		if (!DisplayPrefab)
		{
			DisplayPrefab = HPAssets.CommandoDisplay.InstantiateClone("HardpointNextDisplay", registerNetwork: false);
			CharacterModel componentInChildren = DisplayPrefab.GetComponentInChildren<CharacterModel>();
			(componentInChildren ? componentInChildren.gameObject : DisplayPrefab).AddComponent<ColorTint>().tint = new Color(0.9f, 0.85f, 0.75f);
		}
		DisplayPrefab.AddComponent<SelectVoice>();
	}

	private static BuffDef Buff(string name, Sprite icon, Color color, bool stack, bool debuff, bool hidden)
	{
		BuffDef buffDef = ScriptableObject.CreateInstance<BuffDef>();
		buffDef.name = name;
		buffDef.iconSprite = icon;
		buffDef.buffColor = color;
		buffDef.canStack = stack;
		buffDef.isDebuff = debuff;
		buffDef.isHidden = hidden;
		ContentAddition.AddBuffDef(buffDef);
		return buffDef;
	}

	private static void CreateBuffs()
	{
		StaggerBuff = Buff("HardpointNextStagger", HPAssets.OverloadIcon, new Color(1f, 0.75f, 0.25f), stack: true, debuff: false, hidden: true);
		OverloadBuff = Buff("HardpointNextACSOverload", HPAssets.OverloadIcon, new Color(1f, 0.6f, 0.15f), stack: false, debuff: true, hidden: false);
		ArmorBuff = Buff("HardpointNextAssaultArmorCharge", HPAssets.ArmorIcon, new Color(0.45f, 1f, 1f), stack: false, debuff: false, hidden: false);
	}

	private static void Stats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
	{
		if ((bool)body && (bool)ArmorBuff && body.HasBuff(ArmorBuff))
		{
			args.armorAdd += HPConfig.ArmorChargeArmor.Value;
		}
	}

	private static void CreateSkills()
	{
		GenericSkill[] components = BodyPrefab.GetComponents<GenericSkill>();
		for (int i = 0; i < components.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(components[i]);
		}
		SkillLocator component = BodyPrefab.GetComponent<SkillLocator>();
		component.passiveSkill = new SkillLocator.PassiveSkill
		{
			enabled = true,
			skillNameToken = "BCM_NEXT_PASSIVE_NAME",
			skillDescriptionToken = "BCM_NEXT_PASSIVE_DESCRIPTION",
			icon = HPAssets.PassiveIcon
		};
		SkillDef skillDef = MakeSkill<SkillDef>("BurstRifle", "PRIMARY", HPAssets.PrimaryIcon, typeof(FireRifle), 0f, "Weapon", InterruptPriority.Any);
		skillDef.mustKeyPress = false;
		skillDef.cancelSprintingOnActivation = false;
		SkillDef skillDef2 = MakeSkill<SkillDef>("PulseBlade", "SECONDARY", HPAssets.SecondaryIcon, typeof(PulseBlade), HPConfig.BladeCooldown.Value, "Body", InterruptPriority.Skill);
		ENSkillDef eNSkillDef = MakeSkill<ENSkillDef>("QuickBoost", "UTILITY", HPAssets.UtilityIcon, typeof(QuickBoost), NextConfig.QbInterval.Value, "Boost", InterruptPriority.Skill);
		eNSkillDef.baseMaxStock = 1;
		eNSkillDef.enCost = NextConfig.QbCost.Value;
		eNSkillDef.cancelSprintingOnActivation = false;
		eNSkillDef.isCombatSkill = false;
		ENSkillDef eNSkillDef2 = MakeSkill<ENSkillDef>("AssaultBoost", "UTILITY2", HPAssets.Utility2Icon, typeof(AssaultBoost), HPConfig.AssaultCooldown.Value, "Boost", InterruptPriority.Skill);
		eNSkillDef2.enCost = 15f;
		eNSkillDef2.cancelSprintingOnActivation = false;
		eNSkillDef2.isCombatSkill = false;
		SkillDef skillDef3 = MakeSkill<SkillDef>("MissileSalvo", "SPECIAL", HPAssets.SpecialIcon, typeof(MissileLock), HPConfig.MissileCooldown.Value, "Pods", InterruptPriority.Skill);
		skillDef3.cancelSprintingOnActivation = false;
		SkillDef skillDef4 = MakeSkill<SkillDef>("AssaultArmor", "SPECIAL2", HPAssets.Special2Icon, typeof(AssaultArmor), HPConfig.ArmorCooldown.Value, "Body", InterruptPriority.PrioritySkill);
		component.primary = AddSkillSlot(BodyPrefab, "Primary", skillDef);
		component.secondary = AddSkillSlot(BodyPrefab, "Secondary", skillDef2);
		component.utility = AddSkillSlot(BodyPrefab, "Utility", eNSkillDef, eNSkillDef2);
		component.special = AddSkillSlot(BodyPrefab, "Special", skillDef3, skillDef4);
		// Hanger weapons (swapped onto the right arm with the Hanger key) and the garage rows.
		SkillDef shotgun = MakeSkill<SkillDef>("HangerShotgun", "HANGER_SHOTGUN", HPAssets.PrimaryIcon, typeof(FireShotgun), 0f, "Weapon", InterruptPriority.Any);
		shotgun.mustKeyPress = false;
		shotgun.cancelSprintingOnActivation = false;
		SkillDef linear = MakeSkill<SkillDef>("HangerLinear", "HANGER_LINEAR", HPAssets.PrimaryIcon, typeof(FireLinear), 0f, "Weapon", InterruptPriority.Any);
		linear.mustKeyPress = false;
		linear.cancelSprintingOnActivation = false;
		Garage.Hangers = new SkillDef[] { shotgun, linear };
		Garage.CreateSlots(BodyPrefab);
		BodyPrefab.AddComponent<NextParts>();
	}

	public static T MakeSkill<T>(string name, string tokenSlot, Sprite icon, Type state, float cooldown, string machine, InterruptPriority priority) where T : SkillDef
	{
		T val = ScriptableObject.CreateInstance<T>();
		val.skillName = "HardpointNext" + name;
		((UnityEngine.Object)val).name = val.skillName;
		val.skillNameToken = "BCM_NEXT_" + tokenSlot + "_NAME";
		val.skillDescriptionToken = "BCM_NEXT_" + tokenSlot + "_DESCRIPTION";
		val.icon = icon;
		val.activationState = new SerializableEntityStateType(state);
		val.activationStateMachineName = machine;
		val.interruptPriority = priority;
		val.baseRechargeInterval = cooldown;
		val.baseMaxStock = 1;
		val.rechargeStock = 1;
		val.requiredStock = 1;
		val.stockToConsume = 1;
		val.fullRestockOnAssign = true;
		val.isCombatSkill = true;
		val.mustKeyPress = true;
		val.cancelSprintingOnActivation = true;
		val.canceledFromSprinting = false;
		ContentAddition.AddSkillDef(val);
		return val;
	}

	public static GenericSkill AddSkillSlot(GameObject prefab, string slotName, params SkillDef[] defs)
	{
		SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
		((UnityEngine.Object)skillFamily).name = "HardpointNext" + slotName + "Family";
		skillFamily.variants = Enumerable.ToArray<SkillFamily.Variant>(Enumerable.Select<SkillDef, SkillFamily.Variant>((IEnumerable<SkillDef>)defs, (Func<SkillDef, SkillFamily.Variant>)delegate(SkillDef def)
		{
			SkillFamily.Variant result = default(SkillFamily.Variant);
			result.skillDef = def;
			result.viewableNode = new ViewablesCatalog.Node(def.skillNameToken, isFolder: false);
			return result;
		}));
		ContentAddition.AddSkillFamily(skillFamily);
		GenericSkill genericSkill = prefab.AddComponent<GenericSkill>();
		genericSkill.skillName = slotName;
		HPUtil.SetField(genericSkill, "_skillFamily", skillFamily);
		return genericSkill;
	}

	private static void CreateSurvivor()
	{
		Survivor = ScriptableObject.CreateInstance<SurvivorDef>();
		((UnityEngine.Object)Survivor).name = "HardpointNext";
		Survivor.cachedName = "HardpointNext";
		Survivor.bodyPrefab = BodyPrefab;
		Survivor.displayPrefab = DisplayPrefab;
		Survivor.primaryColor = BodyColor;
		Survivor.displayNameToken = "BCM_NEXT_NAME";
		Survivor.descriptionToken = "BCM_NEXT_DESCRIPTION";
		Survivor.outroFlavorToken = "BCM_NEXT_OUTRO_FLAVOR";
		Survivor.mainEndingEscapeFailureFlavorToken = "BCM_NEXT_OUTRO_FAILURE";
		Survivor.desiredSortPosition = 117f;
		ContentAddition.AddSurvivorDef(Survivor);
	}

	private static void CreateEffects()
	{
		SoundCueEffect = MakeEffect("HardpointNextSoundCue", 0.5f, delegate(GameObject root)
		{
			root.AddComponent<SoundCue>();
		});
	}

	public static void Cue(CustomAudio.Clip clip, Vector3 position, float range)
	{
		if ((bool)SoundCueEffect)
		{
			EffectManager.SpawnEffect(SoundCueEffect, new EffectData
			{
				origin = position,
				genericUInt = (uint)clip,
				genericFloat = range
			}, transmit: true);
		}
	}

	public static GameObject MakeEffect(string name, float lifetime, Action<GameObject> build)
	{
		GameObject gameObject = new GameObject(name);
		EffectComponent effectComponent = gameObject.AddComponent<EffectComponent>();
		effectComponent.applyScale = false;
		effectComponent.soundName = "";
		VFXAttributes vFXAttributes = gameObject.AddComponent<VFXAttributes>();
		vFXAttributes.vfxPriority = VFXAttributes.VFXPriority.Always;
		vFXAttributes.vfxIntensity = VFXAttributes.VFXIntensity.Low;
		gameObject.AddComponent<DestroyOnTimer>().duration = lifetime;
		build(gameObject);
		GameObject gameObject2 = gameObject.InstantiateClone(name, registerNetwork: false);
		UnityEngine.Object.DestroyImmediate(gameObject);
		ContentAddition.AddEffect(gameObject2);
		return gameObject2;
	}

	private static void CreateMissile()
	{
		MissileProjectile = BareProjectile("HardpointNextMissile");
		if ((bool)MissileProjectile)
		{
			if (!MissileProjectile.GetComponent<ProjectileTargetComponent>())
			{
				MissileProjectile.AddComponent<ProjectileTargetComponent>();
			}
			MissileProjectile.AddComponent<MissileFlight>();
			GameObject gameObject = new GameObject("HardpointNextMissileGhost");
			gameObject.AddComponent<ProjectileGhostController>();
			GameObject gameObject2 = CustomModel.Prop("mdlHardpointMissile");
			if ((bool)gameObject2)
			{
				CustomModel.AttachProp(gameObject.transform, gameObject2, 1.3f);
			}
			else
			{
				StandIn(gameObject.transform, new Vector3(0.14f, 0.14f, 0.9f));
			}
			gameObject.AddComponent<MissileTrail>();
			GhostFix.Use(MissileProjectile, FinishPrefab(gameObject, "HardpointNextMissileGhost"));
			ContentAddition.AddProjectile(MissileProjectile);
		}
	}

	private static GameObject BareProjectile(string name)
	{
		if (!HPAssets.SpitProjectile)
		{
			return null;
		}
		GameObject gameObject = HPAssets.SpitProjectile.InstantiateClone(name, registerNetwork: true);
		ProjectileExplosion[] componentsInChildren = gameObject.GetComponentsInChildren<ProjectileExplosion>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren[i]);
		}
		MonoBehaviour[] componentsInChildren2 = gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		foreach (MonoBehaviour monoBehaviour in componentsInChildren2)
		{
			if (monoBehaviour is IProjectileImpactBehavior)
			{
				UnityEngine.Object.DestroyImmediate(monoBehaviour);
			}
		}
		ProjectileStickOnImpact[] componentsInChildren3 = gameObject.GetComponentsInChildren<ProjectileStickOnImpact>(includeInactive: true);
		for (int i = 0; i < componentsInChildren3.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren3[i]);
		}
		ProjectileSimple[] componentsInChildren4 = gameObject.GetComponentsInChildren<ProjectileSimple>(includeInactive: true);
		for (int i = 0; i < componentsInChildren4.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren4[i]);
		}
		ParticleSystem[] componentsInChildren5 = gameObject.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
		for (int i = 0; i < componentsInChildren5.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren5[i]);
		}
		Renderer[] componentsInChildren6 = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren6.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren6[i]);
		}
		Light[] componentsInChildren7 = gameObject.GetComponentsInChildren<Light>(includeInactive: true);
		for (int i = 0; i < componentsInChildren7.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren7[i]);
		}
		Collider[] componentsInChildren8 = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren8.Length; i++)
		{
			componentsInChildren8[i].enabled = false;
		}
		Rigidbody component = gameObject.GetComponent<Rigidbody>();
		if ((bool)component)
		{
			component.useGravity = false;
			component.isKinematic = true;
			component.detectCollisions = false;
		}
		if (!gameObject.GetComponent<ProjectileNetworkTransform>())
		{
			gameObject.AddComponent<ProjectileNetworkTransform>();
		}
		if (!gameObject.GetComponent<ProjectileDamage>())
		{
			gameObject.AddComponent<ProjectileDamage>();
		}
		return gameObject;
	}

	private static void StandIn(Transform parent, Vector3 size)
	{
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		UnityEngine.Object.DestroyImmediate(gameObject.GetComponent<Collider>());
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		gameObject.transform.localScale = size;
	}

	private static GameObject FinishPrefab(GameObject sceneObject, string name)
	{
		GameObject gameObject = sceneObject.InstantiateClone(name, registerNetwork: false);
		CustomModel.TrackRenderers(gameObject);
		UnityEngine.Object.DestroyImmediate(sceneObject);
		return gameObject;
	}
}
