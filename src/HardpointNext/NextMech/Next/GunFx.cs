using System;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;

namespace NextMech;

// Vanilla tracers / muzzle flashes / hit sparks so every gun looks and hits differently.
public static class GunFx
{
	public static GameObject TracerNails, TracerShotgun, TracerRailLight, TracerRailSuper, TracerGold, TracerSmoke;
	public static GameObject HitShotgun, HitRail, HitBarrage, HitFMJ;
	public static GameObject FlashRail, FlashSmoke, FlashFMJ, FlashLightning, FlashGold;
	public static GameObject Bolt;

	public static void Init()
	{
		TracerNails = L(RoR2_Base_Toolbot.TracerToolbotNails_prefab);
		TracerShotgun = L(RoR2_Base_Captain.TracerCaptainShotgun_prefab) ?? L(RoR2_Base_Commando.TracerCommandoShotgun_prefab);
		TracerRailLight = L(RoR2_DLC1_Railgunner.TracerRailgunLight_prefab);
		TracerRailSuper = L(RoR2_DLC1_Railgunner.TracerRailgunSuper_prefab);
		TracerGold = L(RoR2_Base_GoldGat.TracerGoldGat_prefab);
		TracerSmoke = L(RoR2_Base_Common_VFX.TracerSmokeLine_prefab);
		HitShotgun = L(RoR2_Base_Commando.HitsparkCommandoShotgun_prefab);
		HitRail = L(RoR2_DLC1_Railgunner.ImpactRailgun_prefab);
		HitBarrage = L(RoR2_DLC1_Railgunner.HitsparkRailgunnerPistol_prefab);
		HitFMJ = L(RoR2_Base_Captain.HitsparkCaptainShotgun_prefab);
		FlashRail = L(RoR2_DLC1_Railgunner.MuzzleflashRailgun_prefab);
		FlashSmoke = L(RoR2_Base_Common_VFX.MuzzleflashSmokeRing_prefab);
		FlashFMJ = L(RoR2_Base_Commando.MuzzleflashFMJ_prefab);
		FlashLightning = L(RoR2_Base_Mage.MuzzleflashMageLightning_prefab);
		FlashGold = L(RoR2_Base_GoldGat.MuzzleflashGoldGat_prefab);
		Bolt = L(RoR2_Base_Mage.MageLightningboltBasic_prefab);
	}

	private static GameObject L(string key)
	{
		try { return HPAssets.Load<GameObject>(key); }
		catch (Exception e)
		{
			HPPlugin.Log.LogWarning("GunFx asset missing: " + e.Message);
			return null;
		}
	}

	public static GameObject Or(GameObject a, GameObject b) => a ? a : b;
}
