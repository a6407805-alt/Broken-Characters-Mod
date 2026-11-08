using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace NextMech;

internal static class GhostFix
{
	private delegate bool TryFindOrig(ProjectileController controller, out GameObject ghost, out AssetReferenceT<GameObject> reference);

	private static Hook hook;

	public static void Use(GameObject projectilePrefab, GameObject ghost)
	{
		ProjectileController component = projectilePrefab.GetComponent<ProjectileController>();
		if (!component)
		{
			return;
		}
		component.ghostPrefab = ghost;
		FieldInfo[] fields = typeof(ProjectileController).GetFields((BindingFlags)52);
		foreach (FieldInfo val in fields)
		{
			if (typeof(AssetReference).IsAssignableFrom(val.FieldType))
			{
				try
				{
					string text = ((val.GetValue((object)component) is AssetReference assetReference) ? assetReference.AssetGUID : "");
					val.SetValue((object)component, Activator.CreateInstance(val.FieldType, ""));
					HPPlugin.Log.LogInfo(projectilePrefab.name + ": cleared ProjectileController." + ((MemberInfo)val).Name + " (was '" + text + "') so our ghost is used.");
				}
				catch (Exception ex)
				{
					HPPlugin.Log.LogWarning(projectilePrefab.name + ": could not clear ProjectileController." + ((MemberInfo)val).Name + ": " + ex.Message);
				}
			}
		}
		(projectilePrefab.GetComponent<CustomGhost>() ?? projectilePrefab.AddComponent<CustomGhost>()).ghost = ghost;
	}

	public static void Init()
	{
		try
		{
			MethodInfo method = typeof(ProjectileGhostReplacementManager).GetMethod("TryFindProjectileGhostPrefab", (BindingFlags)56);
			if (method == (MethodInfo)null)
			{
				HPPlugin.Log.LogInfo("TryFindProjectileGhostPrefab not found (older game); ghostPrefab is used directly.");
				return;
			}
			hook = new Hook((MethodBase)(object)method, typeof(GhostFix).GetMethod("TryFind", (BindingFlags)40));
			HPPlugin.Log.LogInfo("Hooked ProjectileGhostReplacementManager.TryFindProjectileGhostPrefab for custom projectile models.");
		}
		catch (Exception ex)
		{
			HPPlugin.Log.LogWarning("Could not hook the projectile ghost lookup: " + ex.Message);
		}
	}

	private static bool TryFind(TryFindOrig orig, ProjectileController controller, out GameObject ghost, out AssetReferenceT<GameObject> reference)
	{
		CustomGhost customGhost = (controller ? controller.GetComponent<CustomGhost>() : null);
		if ((bool)customGhost && (bool)customGhost.ghost)
		{
			ghost = customGhost.ghost;
			reference = new AssetReferenceT<GameObject>("");
			return true;
		}
		return orig(controller, out ghost, out reference);
	}
}
