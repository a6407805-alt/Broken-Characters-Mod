using System;
using System.Linq;
using System.Reflection;
using EntityStates;
using GravitosNext.Components;
using GravitosNext.SkillStates;
using GravitosNext.Skills;
using R2API;
using RoR2;
using RoR2.Projectile;
using RoR2.Skills;
using UnityEngine;

namespace GravitosNext;

internal static class GravitosContent
{
	public const string Prefix = "BCM_GRAVNEXT_";

	public static GameObject BodyPrefab;

	public static GameObject DisplayPrefab;

	public static GameObject MasterPrefab;

	public static GameObject BlackHolePrefab;

	public static SurvivorDef Survivor;

	private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	public static void Create()
	{
		if (!GravitosAssets.EngiBody)
		{
			GravitosPlugin.Log.LogError("EngiBody could not be loaded; Gravitos will not be added.");
			return;
		}
		RegisterStates();
		CreateBody();
		CreateDisplay();
		CreateBlackHole();
		CreateSkills();
		CreateMaster();
		CreateSurvivor();
		Tokens.Add();
	}

	private static void RegisterStates()
	{
		ContentAddition.AddEntityState<SelectState>(out var wasAdded);
		ContentAddition.AddEntityState<ChargeGravitonLance>(out wasAdded);
		ContentAddition.AddEntityState<FireGravitonLance>(out wasAdded);
		ContentAddition.AddEntityState<PullState>(out wasAdded);
		ContentAddition.AddEntityState<WildRideState>(out wasAdded);
		ContentAddition.AddEntityState<OrbitalIonCannon>(out wasAdded);
		ContentAddition.AddEntityState<GravitosCharacterMain>(out wasAdded);
	}

	private static void CreateBody()
	{
		BodyPrefab = GravitosAssets.EngiBody.InstantiateClone("GravitosNextBody", registerNetwork: true);
		CharacterBody component = BodyPrefab.GetComponent<CharacterBody>();
		component.baseNameToken = "BCM_GRAVNEXT_NAME";
		component.subtitleNameToken = "BCM_GRAVNEXT_SUBTITLE";
		component.portraitIcon = GravitosAssets.Portrait;
		component.bodyColor = new Color(0.55f, 0.35f, 1f);
		component.baseMaxHealth = GravitosConfig.BaseHealth.Value;
		component.levelMaxHealth = 0f;
		component.baseMaxShield = GravitosConfig.BaseShield.Value;
		component.levelMaxShield = GravitosConfig.ShieldPerLevel.Value;
		component.baseRegen = 0.5f;
		component.levelRegen = 0.1f;
		component.baseDamage = GravitosConfig.BaseDamage.Value;
		component.levelDamage = GravitosConfig.DamagePerLevel.Value;
		component.baseArmor = GravitosConfig.BaseArmor.Value;
		component.levelArmor = 0f;
		component.baseMoveSpeed = GravitosConfig.MoveSpeed.Value;
		EnsureStateMachine(BodyPrefab, "Weapon");
		EnsureStateMachine(BodyPrefab, "Weapon2");
		EntityStateMachine entityStateMachine = BodyPrefab.GetComponents<EntityStateMachine>().FirstOrDefault((EntityStateMachine m) => m.customName == "Body");
		if ((bool)entityStateMachine)
		{
			entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(GravitosCharacterMain));
		}
		else
		{
			GravitosPlugin.Log.LogError("Body state machine not found; levitation and glide disabled.");
		}
		BodyPrefab.AddComponent<GravitosController>();
		BodyPrefab.AddComponent<GravitosTracker>();
		ModelLocator component2 = BodyPrefab.GetComponent<ModelLocator>();
		if ((bool)component2 && (bool)component2.modelTransform)
		{
			component2.modelTransform.gameObject.AddComponent<ModelPartHider>();
			component2.modelTransform.gameObject.AddComponent<ModelHover>();
			component2.modelTransform.gameObject.AddComponent<LodestarCustomModel>();
		}
		ContentAddition.AddBody(BodyPrefab);
	}

	private static void CreateDisplay()
	{
		if ((bool)GravitosAssets.EngiDisplay)
		{
			DisplayPrefab = GravitosAssets.EngiDisplay.InstantiateClone("GravitosNextDisplay", registerNetwork: false);
			CharacterModel componentInChildren = DisplayPrefab.GetComponentInChildren<CharacterModel>(includeInactive: true);
			GameObject obj = (componentInChildren ? componentInChildren.gameObject : DisplayPrefab);
			obj.AddComponent<ModelPartHider>();
			obj.AddComponent<ModelHover>();
			obj.AddComponent<LodestarCustomModel>();
		}
	}

	private static void CreateBlackHole()
	{
		if (!GravitosAssets.GravSphere)
		{
			GravitosPlugin.Log.LogError("GravSphere could not be loaded; Wild Ride will do nothing.");
			return;
		}
		BlackHolePrefab = GravitosAssets.GravSphere.InstantiateClone("GravitosNextWildRideProjectile", registerNetwork: true);
		RadialForce[] componentsInChildren = BlackHolePrefab.GetComponentsInChildren<RadialForce>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren[i]);
		}
		DestroyOnTimer[] componentsInChildren2 = BlackHolePrefab.GetComponentsInChildren<DestroyOnTimer>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(componentsInChildren2[i]);
		}
		ProjectileSimple component = BlackHolePrefab.GetComponent<ProjectileSimple>();
		if ((bool)component)
		{
			component.desiredForwardSpeed = 0f;
			component.lifetime = GravitosConfig.WildRideDuration.Value + 5f;
		}
		if (!BlackHolePrefab.GetComponent<ProjectileDamage>())
		{
			BlackHolePrefab.AddComponent<ProjectileDamage>();
		}
		BlackHolePrefab.AddComponent<GravitosBlackHole>();
		ContentAddition.AddProjectile(BlackHolePrefab);
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
			skillNameToken = "BCM_GRAVNEXT_PASSIVE_NAME",
			skillDescriptionToken = "BCM_GRAVNEXT_PASSIVE_DESCRIPTION",
			icon = GravitosAssets.PassiveIcon
		};
		GravitosTrackingSkillDef gravitosTrackingSkillDef = ScriptableObject.CreateInstance<GravitosTrackingSkillDef>();
		Configure(gravitosTrackingSkillDef, "Select", "PRIMARY", GravitosAssets.PrimaryIcon, typeof(SelectState), "Weapon", 0f, InterruptPriority.Any);
		gravitosTrackingSkillDef.requiredStock = 0;
		gravitosTrackingSkillDef.stockToConsume = 0;
		gravitosTrackingSkillDef.mustKeyPress = false;
		SkillDef skillDef = ScriptableObject.CreateInstance<SkillDef>();
		float cooldown = GravitosConfig.LanceChargeTime.Value + GravitosConfig.LanceDuration.Value + GravitosConfig.LanceCooldown.Value;
		Configure(skillDef, "GravitonLance", "SECONDARY", GravitosAssets.SecondaryIcon, typeof(ChargeGravitonLance), "Weapon", cooldown, InterruptPriority.Skill);
		SkillDef skillDef2 = ScriptableObject.CreateInstance<SkillDef>();
		float cooldown2 = 1.5f + GravitosConfig.IonDuration.Value + 2f + GravitosConfig.IonCooldown.Value;
		Configure(skillDef2, "OrbitalIonCannon", "SECONDARY_ION", GravitosAssets.IonCannonIcon, typeof(OrbitalIonCannon), "Weapon", cooldown2, InterruptPriority.Skill);
		ShieldScaledSkillDef shieldScaledSkillDef = ScriptableObject.CreateInstance<ShieldScaledSkillDef>();
		Configure(shieldScaledSkillDef, "Pull", "UTILITY", GravitosAssets.UtilityIcon, typeof(PullState), "Weapon2", GravitosConfig.PullCooldown.Value, InterruptPriority.PrioritySkill);
		shieldScaledSkillDef.cancelSprintingOnActivation = false;
		shieldScaledSkillDef.isCombatSkill = false;
		SkillDef skillDef3 = ScriptableObject.CreateInstance<SkillDef>();
		Configure(skillDef3, "WildRide", "SPECIAL", GravitosAssets.SpecialIcon, typeof(WildRideState), "Weapon", GravitosConfig.WildRideCooldown.Value, InterruptPriority.PrioritySkill);
		component.primary = AddSkillSlot(BodyPrefab, "Primary", gravitosTrackingSkillDef);
		component.secondary = AddSkillSlot(BodyPrefab, "Secondary", skillDef, skillDef2);
		component.utility = AddSkillSlot(BodyPrefab, "Utility", shieldScaledSkillDef);
		component.special = AddSkillSlot(BodyPrefab, "Special", skillDef3);
	}

	private static void Configure(SkillDef def, string name, string tokenSlot, Sprite icon, Type state, string machine, float cooldown, InterruptPriority priority)
	{
		def.skillName = "GravitosNext" + name;
		((UnityEngine.Object)def).name = def.skillName;
		def.skillNameToken = "BCM_GRAVNEXT_" + tokenSlot + "_NAME";
		def.skillDescriptionToken = "BCM_GRAVNEXT_" + tokenSlot + "_DESCRIPTION";
		def.icon = icon;
		def.activationState = new SerializableEntityStateType(state);
		def.activationStateMachineName = machine;
		def.interruptPriority = priority;
		def.baseRechargeInterval = cooldown;
		def.baseMaxStock = 1;
		def.rechargeStock = 1;
		def.requiredStock = 1;
		def.stockToConsume = 1;
		def.beginSkillCooldownOnSkillEnd = false;
		def.fullRestockOnAssign = true;
		def.resetCooldownTimerOnUse = false;
		def.isCombatSkill = true;
		def.mustKeyPress = true;
		def.cancelSprintingOnActivation = true;
		def.canceledFromSprinting = false;
		ContentAddition.AddSkillDef(def);
	}

	private static GenericSkill AddSkillSlot(GameObject prefab, string slotName, params SkillDef[] defs)
	{
		SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
		((UnityEngine.Object)skillFamily).name = "GravitosNext" + slotName + "Family";
		skillFamily.variants = defs.Select(delegate(SkillDef def)
		{
			SkillFamily.Variant result = default(SkillFamily.Variant);
			result.skillDef = def;
			result.viewableNode = new ViewablesCatalog.Node(def.skillNameToken, isFolder: false);
			return result;
		}).ToArray();
		ContentAddition.AddSkillFamily(skillFamily);
		GenericSkill genericSkill = prefab.AddComponent<GenericSkill>();
		genericSkill.skillName = slotName;
		SetPrivateField(genericSkill, "_skillFamily", skillFamily);
		return genericSkill;
	}

	private static EntityStateMachine EnsureStateMachine(GameObject prefab, string name)
	{
		EntityStateMachine entityStateMachine = prefab.GetComponents<EntityStateMachine>().FirstOrDefault((EntityStateMachine m) => m.customName == name);
		if ((bool)entityStateMachine)
		{
			return entityStateMachine;
		}
		entityStateMachine = prefab.AddComponent<EntityStateMachine>();
		entityStateMachine.customName = name;
		entityStateMachine.initialStateType = new SerializableEntityStateType(typeof(Idle));
		entityStateMachine.mainStateType = new SerializableEntityStateType(typeof(Idle));
		NetworkStateMachine component = prefab.GetComponent<NetworkStateMachine>();
		if ((bool)component)
		{
			EntityStateMachine[] source = GetPrivateField<EntityStateMachine[]>(component, "stateMachines") ?? new EntityStateMachine[0];
			SetPrivateField(component, "stateMachines", source.Append(entityStateMachine).ToArray());
		}
		CharacterDeathBehavior component2 = prefab.GetComponent<CharacterDeathBehavior>();
		if ((bool)component2)
		{
			component2.idleStateMachine = (component2.idleStateMachine ?? new EntityStateMachine[0]).Append(entityStateMachine).ToArray();
		}
		return entityStateMachine;
	}

	private static void SetPrivateField(object target, string field, object value)
	{
		FieldInfo field2 = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (field2 == null)
		{
			GravitosPlugin.Log.LogError("Field " + target.GetType().Name + "." + field + " not found.");
		}
		else
		{
			field2.SetValue(target, value);
		}
	}

	private static T GetPrivateField<T>(object target, string field) where T : class
	{
		return target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target) as T;
	}

	private static void CreateMaster()
	{
		if ((bool)GravitosAssets.EngiMonsterMaster)
		{
			MasterPrefab = GravitosAssets.EngiMonsterMaster.InstantiateClone("GravitosNextMonsterMaster", registerNetwork: true);
			MasterPrefab.GetComponent<CharacterMaster>().bodyPrefab = BodyPrefab;
			ContentAddition.AddMaster(MasterPrefab);
		}
	}

	private static void CreateSurvivor()
	{
		Survivor = ScriptableObject.CreateInstance<SurvivorDef>();
		((UnityEngine.Object)Survivor).name = "GravitosNext";
		Survivor.cachedName = "GravitosNext";
		Survivor.bodyPrefab = BodyPrefab;
		Survivor.displayPrefab = (DisplayPrefab ? DisplayPrefab : GravitosAssets.EngiDisplay);
		Survivor.primaryColor = new Color(0.55f, 0.35f, 1f);
		Survivor.displayNameToken = "BCM_GRAVNEXT_NAME";
		Survivor.descriptionToken = "BCM_GRAVNEXT_DESCRIPTION";
		Survivor.outroFlavorToken = "BCM_GRAVNEXT_OUTRO_FLAVOR";
		Survivor.mainEndingEscapeFailureFlavorToken = "BCM_GRAVNEXT_OUTRO_FAILURE";
		Survivor.desiredSortPosition = 100f;
		ContentAddition.AddSurvivorDef(Survivor);
	}
}
