using System;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace GravitosNext;

internal static class GravitosAssets
{
	public static GameObject EngiBody;

	public static GameObject EngiDisplay;

	public static GameObject EngiMonsterMaster;

	public static GameObject GravSphere;

	public static GameObject TrackingIndicator;

	public static GameObject BeamTracer;

	public static GameObject SelectTracer;

	public static GameObject MuzzleFlash;

	public static GameObject LightningImpact;

	public static GameObject SlamEffect;

	public static GameObject PulseEffect;

	public static GameObject ExplosionEffect;

	public static Material BubbleMaterial;

	public static Material BeamOuterMaterial;

	public static Material BeamCoreMaterial;

	public static GameObject LanceChargeEffect;

	public static GameObject LanceImpactEffect;

	public static Sprite PassiveIcon;

	public static Sprite PrimaryIcon;

	public static Sprite SecondaryIcon;

	public static Sprite IonCannonIcon;

	public static Sprite UtilityIcon;

	public static Sprite SpecialIcon;

	public static Texture2D Portrait;

	public static void Init()
	{
		EngiBody = Load<GameObject>(RoR2_Base_Engi.EngiBody_prefab);
		EngiDisplay = Load<GameObject>(RoR2_Base_Engi.EngiDisplay_prefab);
		EngiMonsterMaster = Load<GameObject>(RoR2_Base_Engi.EngiMonsterMaster_prefab);
		GravSphere = Load<GameObject>(RoR2_Base_Blackhole.GravSphere_prefab);
		TrackingIndicator = Load<GameObject>(RoR2_Base_Huntress.HuntressTrackingIndicator_prefab);
		BeamTracer = Load<GameObject>(RoR2_DLC1_Railgunner.TracerRailgunLight_prefab);
		SelectTracer = Load<GameObject>(RoR2_Base_Golem.TracerGolem_prefab);
		MuzzleFlash = Load<GameObject>(RoR2_Base_Mage.MuzzleflashMageLightningLarge_prefab);
		LightningImpact = Load<GameObject>(RoR2_Base_LightningStrikeOnHit.SimpleLightningStrikeImpact_prefab);
		SlamEffect = Load<GameObject>(RoR2_Base_Loader.LoaderGroundSlam_prefab);
		PulseEffect = Load<GameObject>(RoR2_Base_BeetleGuard.BeetleGuardGroundSlam_prefab);
		ExplosionEffect = Load<GameObject>(RoR2_Base_Vagrant.VagrantNovaExplosion_prefab);
		BubbleMaterial = Load<Material>(RoR2_DLC1_Common_Void.matNullifierBlackholeZoneAreaIndicator_mat);
		BeamOuterMaterial = Load<Material>(RoR2_DLC1_VoidRaidCrab.matBeamInnerLine_mat);
		BeamCoreMaterial = Load<Material>(RoR2_DLC1_VoidSurvivor.matVoidSurvivorBeamCorrupted_mat);
		LanceChargeEffect = Load<GameObject>(RoR2_DLC1_VoidSurvivor.VoidSurvivorBeamChargeCorrupt_prefab);
		LanceImpactEffect = Load<GameObject>(RoR2_DLC1_VoidSurvivor.VoidSurvivorBeamImpactCorrupt_prefab);
		PassiveIcon = IconFactory.Passive();
		PrimaryIcon = IconFactory.Primary();
		SecondaryIcon = IconFactory.Secondary();
		IonCannonIcon = IconFactory.IonCannon();
		UtilityIcon = IconFactory.Utility();
		SpecialIcon = IconFactory.Special();
		Portrait = IconFactory.PortraitTexture();
	}

	public static T Load<T>(string key) where T : UnityEngine.Object
	{
		try
		{
			T val = Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
			if (!val)
			{
				GravitosPlugin.Log.LogWarning("Asset not found: " + key);
			}
			return val;
		}
		catch (Exception ex)
		{
			GravitosPlugin.Log.LogWarning("Failed to load " + key + ": " + ex.Message);
			return null;
		}
	}
}
