using System;
using System.Reflection;
using RoR2;

namespace GravitosNext;

internal static class PrivateAccess
{
	private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly MethodInfo recalculateRechargeInterval = typeof(GenericSkill).GetMethod("RecalculateFinalRechargeInterval", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

	private static BulletAttack.HitCallback defaultHitCallback;

	public static BulletAttack.HitCallback DefaultHitCallback
	{
		get
		{
			if (defaultHitCallback == null)
			{
				MethodInfo method = typeof(BulletAttack).GetMethod("DefaultHitCallbackImplementation", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (method != null)
				{
					defaultHitCallback = (BulletAttack.HitCallback)Delegate.CreateDelegate(typeof(BulletAttack.HitCallback), method);
				}
				else
				{
					GravitosPlugin.Log.LogError("BulletAttack.DefaultHitCallbackImplementation not found.");
				}
			}
			return defaultHitCallback;
		}
	}

	public static void RecalculateFinalRechargeInterval(GenericSkill skill)
	{
		if ((bool)skill && recalculateRechargeInterval != null)
		{
			recalculateRechargeInterval.Invoke(skill, null);
		}
	}
}
