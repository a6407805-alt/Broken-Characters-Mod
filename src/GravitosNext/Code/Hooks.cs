using On.RoR2;
using RoR2;

namespace GravitosNext;

internal static class Hooks
{
	public static void Init()
	{
		On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
	}

	private static void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, RoR2.CharacterBody self)
	{
		orig(self);
		if ((bool)self && self.baseNameToken == "BCM_GRAVNEXT_NAME" && (bool)self.skillLocator && (bool)self.skillLocator.utility)
		{
			PrivateAccess.RecalculateFinalRechargeInterval(self.skillLocator.utility);
		}
	}
}
