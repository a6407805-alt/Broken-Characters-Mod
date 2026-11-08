using R2API;

namespace GravitosNext;

internal static class Tokens
{
	private static string Pct(float coefficient)
	{
		return $"<style=cIsDamage>{coefficient * 100f:0}% damage</style>";
	}

	public static void Add()
	{
		LanguageAPI.Add("BCM_GRAVNEXT_NAME", "Gravitos NEXT");
		LanguageAPI.Add("BCM_GRAVNEXT_SUBTITLE", "The Lodestar");
		LanguageAPI.Add("BCM_GRAVNEXT_LORE", "<style=cMono>UES SAFE TRAVELS // GRAVITY DECK\nMEDICAL ADMISSION: ENGINEER [SURNAME ILLEGIBLE], \"GRAVITOS\"</style>\n\nThe patient was inside the artificial gravity ring when the Safe Travels broke up. The ring did not stop spinning. Neither did he.\n\nInjuries: too many to list. Vital signs: present, but only inside the field. His suit projects a shield that holds him together; without it, the attending medic estimates he would last \"about ten hit points\". The medic has been asked not to describe patients in those terms. The medic has declined.\n\nSide effects observed during recovery:\n- Loose objects drift toward his bed.\n- The coffee in the ward pours sideways.\n- The lights flicker whenever he is angry. The lights flicker often.\n\n<style=cMono>Field report, Petrichor V, day 9:</style>\nHe folds gravity around himself now, on purpose. Anything that comes near him is pulled in and slammed down, and lightning follows the fold lines. He calls it the event horizon. He has started calling himself after it, too.\n\n<style=cMono>Field report, day 10:</style>\nHe rode the singularity across the valley. He says it was fine. He says it was fine every time.\n\nWe have asked him to warn us first.");
		LanguageAPI.Add("BCM_GRAVNEXT_OUTRO_FLAVOR", "..and so he left, the planet's pull a little weaker than before.");
		LanguageAPI.Add("BCM_GRAVNEXT_OUTRO_FAILURE", "..and so he vanished, folded into a singularity of his own making.");
		LanguageAPI.Add("BCM_GRAVNEXT_DESCRIPTION", "Gravitos bends gravity and lightning to his will, but his body is fragile; his <style=cIsUtility>energy shield</style> is his life.<color=#CCD3E0>\n\n< ! > With only 10 health, stay out of danger long enough for your shield to recharge.\n\n< ! > Event Horizon slams nearby enemies on its own. Fight close to make the most of it.\n\n< ! > Shield items make PULL recharge faster.\n\n< ! > Wild Ride does not care whose side you are on. Warn your friends.</color>\n");
		LanguageAPI.Add("BCM_GRAVNEXT_PASSIVE_NAME", "Event Horizon");
		LanguageAPI.Add("BCM_GRAVNEXT_PASSIVE_DESCRIPTION", $"A <style=cIsUtility>{GravitosConfig.PassiveRadius.Value:0}m</style> gravity bubble surrounds you. Every <style=cIsUtility>{GravitosConfig.PassiveCooldown.Value:0.#}s</style> it <style=cIsUtility>lifts</style> enemies inside and <style=cIsDamage>slams</style> them into the ground for {Pct(GravitosConfig.PassiveSlamDamage.Value)}. " + $"Enemy projectiles inside are <style=cIsUtility>slowed</style>. You <style=cIsUtility>levitate</style>, and holding jump in the air lets you <style=cIsUtility>glide</style>. You have <style=cIsHealth>{GravitosConfig.BaseHealth.Value:0} health</style> but start with <style=cIsUtility>{GravitosConfig.BaseShield.Value:0} energy shield</style> that grows with level.");
		LanguageAPI.Add("BCM_GRAVNEXT_PRIMARY_NAME", "Select");
		LanguageAPI.Add("BCM_GRAVNEXT_PRIMARY_DESCRIPTION", "Lock onto an enemy. Small enemies are <style=cIsUtility>lifted</style> and <style=cIsDamage>dropped</style> into the ground for " + Pct(GravitosConfig.SelectDamage.Value) + "; larger ones are crushed in place.");
		LanguageAPI.Add("BCM_GRAVNEXT_SECONDARY_NAME", "Graviton Lance");
		LanguageAPI.Add("BCM_GRAVNEXT_SECONDARY_DESCRIPTION", $"Charge for <style=cIsUtility>{GravitosConfig.LanceChargeTime.Value:0}s</style>, then unleash a huge purple beam for <style=cIsUtility>{GravitosConfig.LanceDuration.Value:0}s</style> dealing {Pct(GravitosConfig.LanceDamagePerSecond.Value)} per second to everything in it and where it lands. " + $"Enemies hit <style=cIsDamage>chain-shock</style> to {GravitosConfig.LanceChainBounces.Value} others, and small enemies are <style=cIsUtility>picked up and slammed</style> for {Pct(GravitosConfig.LanceSlamDamage.Value)}. You move slower while using it.");
		LanguageAPI.Add("BCM_GRAVNEXT_SECONDARY_ION_NAME", "Orbital Ion Cannon");
		LanguageAPI.Add("BCM_GRAVNEXT_SECONDARY_ION_DESCRIPTION", $"Fly into the sky and become <style=cIsDamage>four Graviton beams</style> for <style=cIsUtility>{GravitosConfig.IonDuration.Value:0}s</style>, each dealing {Pct(GravitosConfig.IonDamagePerSecond.Value)} per second. " + "The camera pulls back; <style=cIsUtility>aim to sweep the beams</style> across the ground and use movement keys to fly. Hits chain-shock and slam small enemies. <style=cIsUtility>Invulnerable</style> until you land.");
		LanguageAPI.Add("BCM_GRAVNEXT_UTILITY_NAME", "PULL");
		LanguageAPI.Add("BCM_GRAVNEXT_UTILITY_DESCRIPTION", $"<style=cIsUtility>Pull</style> enemies, item pickups and chests in a cone up to <style=cIsUtility>{GravitosConfig.PullRange.Value:0}m</style> toward you. Farther targets are pulled less. " + $"<style=cIsUtility>Pull strength doubles for every {GravitosConfig.PullShieldPerDoubling.Value:0} max shield above {GravitosConfig.PullShieldBaseline.Value:0}</style>, and the cooldown shortens as your shield grows.");
		LanguageAPI.Add("BCM_GRAVNEXT_SPECIAL_NAME", "Wild Ride");
		LanguageAPI.Add("BCM_GRAVNEXT_SPECIAL_DESCRIPTION", $"Open a <style=cIsUtility>black hole</style> that sucks in everything nearby for {Pct(GravitosConfig.WildRideDamagePerSecond.Value)} per second for {GravitosConfig.WildRideDuration.Value:0}s, then <style=cIsDamage>explodes</style> for {Pct(GravitosConfig.WildRideExplosionDamage.Value)}. " + "<style=cDeath>Pulls and damages teammates too.</style>");
	}
}
