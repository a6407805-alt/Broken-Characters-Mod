using System;
using System.IO;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NextMech;

internal static class HPAssets
{
	public static GameObject CommandoBody;

	public static GameObject CommandoDisplay;

	public static GameObject SpitProjectile;

	public static GameObject Tracer;

	public static GameObject MuzzleFlash;

	public static GameObject HitSpark;

	public static GameObject Explosion;

	public static GameObject BigExplosion;

	public static GameObject SlashHit;

	public static GameObject Blink;

	public static GameObject Spark;

	public static GameObject Slam;

	public static Sprite PassiveIcon;

	public static Sprite PrimaryIcon;

	public static Sprite SecondaryIcon;

	public static Sprite UtilityIcon;

	public static Sprite Utility2Icon;

	public static Sprite SpecialIcon;

	public static Sprite Special2Icon;

	public static Sprite OverloadIcon;

	public static Sprite ArmorIcon;

	public static Texture2D Portrait;

	public static void Init()
	{
		CommandoBody = Load<GameObject>(RoR2_Base_Commando.CommandoBody_prefab);
		CommandoDisplay = Load<GameObject>(RoR2_Base_Commando.CommandoDisplay_prefab);
		SpitProjectile = Load<GameObject>(RoR2_Base_Croco.CrocoSpit_prefab);
		Tracer = Load<GameObject>(RoR2_Base_Commando.TracerCommandoDefault_prefab);
		MuzzleFlash = Load<GameObject>(RoR2_Base_Common_VFX.Muzzleflash1_prefab);
		HitSpark = Load<GameObject>(RoR2_Base_Commando.HitsparkCommando_prefab);
		Explosion = Load<GameObject>(RoR2_Base_Common_VFX.OmniExplosionVFX_prefab);
		BigExplosion = Load<GameObject>(RoR2_Base_Common_VFX.ExplosionVFX_prefab);
		SlashHit = Load<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXSlash_prefab);
		Blink = Load<GameObject>(RoR2_Base_Huntress.HuntressBlinkEffect_prefab);
		Spark = Load<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFX_prefab);
		Slam = Load<GameObject>(RoR2_Base_Loader.LoaderGroundSlam_prefab);
		PassiveIcon = IconFactory.Fallback(new Color(0.45f, 1f, 1f));
		PrimaryIcon = IconFactory.Fallback(new Color(0.8f, 0.77f, 0.7f));
		SecondaryIcon = IconFactory.Fallback(new Color(0.45f, 0.9f, 1f));
		UtilityIcon = IconFactory.Fallback(new Color(0.31f, 0.35f, 0.39f));
		Utility2Icon = IconFactory.Fallback(new Color(0.93f, 0.45f, 0.1f));
		SpecialIcon = IconFactory.Fallback(new Color(0.85f, 0.14f, 0.08f));
		Special2Icon = IconFactory.Fallback(new Color(0.45f, 1f, 1f));
		OverloadIcon = IconFactory.Fallback(new Color(1f, 0.75f, 0.25f));
		ArmorIcon = IconFactory.Fallback(new Color(0.45f, 1f, 1f));
		Portrait = IconFactory.FallbackTexture(new Color(0.8f, 0.77f, 0.7f));
	}

	public static void LoadIcons(string pluginDirectory)
	{
		PassiveIcon = Icon("passive", PassiveIcon);
		PrimaryIcon = Icon("primary", PrimaryIcon);
		SecondaryIcon = Icon("secondary", SecondaryIcon);
		UtilityIcon = Icon("utility", UtilityIcon);
		Utility2Icon = Icon("utility2", Utility2Icon);
		SpecialIcon = Icon("special", SpecialIcon);
		Special2Icon = Icon("special2", Special2Icon);
		Texture2D texture2D = Texture("portrait");
		if ((bool)texture2D)
		{
			Portrait = texture2D;
		}
		Sprite Icon(string name, Sprite fallback)
		{
			Texture2D texture2D3 = Texture(name);
			if (!texture2D3)
			{
				return fallback;
			}
			return Sprite.Create(texture2D3, new Rect(0f, 0f, texture2D3.width, texture2D3.height), new Vector2(0.5f, 0.5f));
		}
		Texture2D Texture(string name)
		{
			try
			{
				string text = Path.Combine(pluginDirectory, "Icons", name + ".png");
				if (!File.Exists(text))
				{
					return null;
				}
				Texture2D texture2D2 = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
				return texture2D2.LoadImage(File.ReadAllBytes(text)) ? texture2D2 : null;
			}
			catch (Exception ex)
			{
				HPPlugin.Log.LogWarning("Icon " + name + ": " + ex.Message);
				return null;
			}
		}
	}

	public static T Load<T>(string key) where T : UnityEngine.Object
	{
		try
		{
			T val = Addressables.LoadAssetAsync<T>(key).WaitForCompletion();
			if (!val)
			{
				HPPlugin.Log.LogWarning("Asset not found: " + key);
			}
			return val;
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Failed to load " + key + ": " + ex.Message);
			return null;
		}
	}
}
