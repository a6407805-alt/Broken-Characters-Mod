using R2API;

namespace NextMech;

internal static class Tokens
{
	public static void Add()
	{
		LanguageAPI.Add("BCM_NEXT_NAME", "Hardpoint NEXT");
		LanguageAPI.Add("BCM_NEXT_SUBTITLE", "For Answer");
		LanguageAPI.Add("BCM_NEXT_DESCRIPTION", "Hardpoint, rebuilt as a For Answer NEXT: faster, lighter on its feet, wrapped in Primal Armor and tuned in the garage.<color=#CCD3E0>\n\n< ! > Quick Boost has no charges: chain it as long as EN lasts. Tap it twice for a second-stage quick boost.\n\n< ! > Primal Armor weakens every hit and regenerates. Overed Boost and Assault Armor burn it.\n\n< ! > V: Overed Boost. F: swap to your hanger weapon. T: spend FRS tuning points (one per level).\n\n< ! > Pick FRAME, BOOSTER, GENERATOR and HANGER parts in the loadout.</color>\n");
		LanguageAPI.Add("BCM_NEXT_LORE", "<style=cMono>UES SAFE TRAVELS // SECURITY CONTRACT 4471-C\nASSET: COMBAT FRAME HP-07 \"HARDPOINT\"\nPILOT: [REDACTED]    FEE: PAID IN FULL</style>\n\nThe UES does not own combat frames. It rents them, from outfits that do not answer the phone, and it rents the pilots with them. Clause 9 of contract 4471-C: the pilot travels in cold sleep inside the cockpit and is not to be woken by crew. Clause 12: the frame decides what is hostile.\n\nThe captain of the Safe Travels initialled both clauses without reading them. The frame was loaded crated and dark, between a pallet of medical gel and a crate that would not stop humming.\n\n<style=cMono>Cockpit audio, recovered. Petrichor V, night one:</style>\n> Main system... activated.\n> Pilot vitals: stable. Pilot response: none.\n> Hostiles: everywhere.\n> Engaging combat mode.\n\n<style=cMono>Night four:</style>\n> Generator output: nominal. Ammunition: forty percent.\n> Pilot response: none.\n> Recommend pilot remain asleep. The view is not good.\n\n<style=cMono>Night eleven:</style>\n> Contact: survivor, UES crew. Survivor asks if anyone is inside.\n> Answer withheld.\n> Survivor asks why the frame keeps fighting.\n> The contract is not complete.\n\nNobody has seen the pilot. A crewman who climbed onto the frame's shoulder to signal a dropship swears he heard someone breathing behind the armour, slow and even, like someone dreaming of somewhere else.\n\nThe frame let him climb down. Then it turned toward the gunfire, and went to work.");
		LanguageAPI.Add("BCM_NEXT_OUTRO_FLAVOR", "..and so it left, the contract fulfilled.");
		LanguageAPI.Add("BCM_NEXT_OUTRO_FAILURE", "..and so it went silent, its AP spent.");
		LanguageAPI.Add("BCM_NEXT_SKIN_STANDARD", "UES Standard");
		LanguageAPI.Add("BCM_NEXT_SKIN_RUSTBELT", "Rustbelt");
		LanguageAPI.Add("BCM_NEXT_SKIN_NIGHTFALL", "Nightfall");
		LanguageAPI.Add("BCM_NEXT_PASSIVE_NAME", "Primal Armor, EN and ACS");
		LanguageAPI.Add("BCM_NEXT_PASSIVE_DESCRIPTION", $"<style=cIsUtility>Primal Armor:</style> a regenerating field that blocks up to <style=cIsUtility>{NextConfig.PaReduction.Value * 100f:0}%</style> of each hit; hits wear it down. <style=cIsUtility>EN:</style> boosts and hovering spend energy; empty = <style=cIsHealth>redline</style>. <style=cIsDamage>ACS:</style> your hits build stagger; break it to stun and take <style=cIsDamage>+{Pct(HPConfig.OverloadDamageBonus.Value)}</style> damage.");
		LanguageAPI.Add("BCM_NEXT_PRIMARY_NAME", "Burst Rifle");
		LanguageAPI.Add("BCM_NEXT_PRIMARY_DESCRIPTION", $"Fire {HPConfig.RifleBurst.Value}-round bursts for <style=cIsDamage>{Pct(HPConfig.RifleDamage.Value)}</style> per round. " + $"{HPConfig.RifleMagazine.Value}-round magazine, reloads on its own.");
		LanguageAPI.Add("BCM_NEXT_SECONDARY_NAME", "Pulse Blade");
		LanguageAPI.Add("BCM_NEXT_SECONDARY_DESCRIPTION", "Ignite the blade and lunge: a wide slash for <style=cIsDamage>" + Pct(HPConfig.BladeDamage.Value) + "</style> with heavy stagger. Press again quickly for the <style=cIsDamage>backhand</style>.");
		LanguageAPI.Add("BCM_NEXT_UTILITY_NAME", "Quick Boost");
		LanguageAPI.Add("BCM_NEXT_UTILITY_DESCRIPTION", $"Dash <style=cIsUtility>{HPConfig.QuickBoostDistance.Value:0} m</style> the way you're moving. <style=cIsUtility>No charges</style>: chain them while EN lasts ({NextConfig.QbCost.Value:0} EN). Press again right after for a <style=cIsUtility>second stage</style> ({NextConfig.SecondStageDistance.Value:0.#}x distance). Cancels Overed Boost.");
		LanguageAPI.Add("BCM_NEXT_UTILITY2_NAME", "Assault Boost");
		LanguageAPI.Add("BCM_NEXT_UTILITY2_DESCRIPTION", $"Fly where you aim at <style=cIsUtility>{HPConfig.AssaultSpeed.Value:0} m/s</style> (keep firing). " + "Press Jump for a <style=cIsDamage>Boost Kick: " + Pct(HPConfig.KickDamage.Value) + "</style>. Spends EN; press again to stop.");
		LanguageAPI.Add("BCM_NEXT_SPECIAL_NAME", "Missile Salvo");
		LanguageAPI.Add("BCM_NEXT_SPECIAL_DESCRIPTION", $"Hold to lock up to <style=cIsUtility>{HPConfig.MissileMaxLocks.Value} targets</style>, release to fire " + $"<style=cIsDamage>{HPConfig.MissileCount.Value} homing missiles</style> for <style=cIsDamage>{Pct(HPConfig.MissileDamage.Value)}</style> each.");
		LanguageAPI.Add("BCM_NEXT_SPECIAL2_NAME", "Assault Armor");
		LanguageAPI.Add("BCM_NEXT_SPECIAL2_DESCRIPTION", $"Charge (<style=cIsUtility>+{HPConfig.ArmorChargeArmor.Value:0} armor</style>), then release a pulse explosion: " + $"<style=cIsDamage>{Pct(HPConfig.ArmorDamage.Value)}</style> to everything within {HPConfig.ArmorRadius.Value:0} m. Breaks stagger instantly.");
		LanguageAPI.Add("BCM_NEXT_HANGER_SHOTGUN_NAME", "Hanger: Shotgun");
		LanguageAPI.Add("BCM_NEXT_HANGER_SHOTGUN_DESCRIPTION", $"{NextConfig.ShotgunPellets.Value} pellets x <style=cIsDamage>{Pct(NextConfig.ShotgunDamage.Value)}</style>. Builds stagger fast up close.");
		LanguageAPI.Add("BCM_NEXT_HANGER_LINEAR_NAME", "Hanger: Linear Rifle");
		LanguageAPI.Add("BCM_NEXT_HANGER_LINEAR_DESCRIPTION", $"One piercing round for <style=cIsDamage>{Pct(NextConfig.LinearDamage.Value)}</style>.");
		LanguageAPI.Add("BCM_NEXT_GARAGE_FRAME", "Frame");
		LanguageAPI.Add("BCM_NEXT_GARAGE_BOOSTER", "Booster");
		LanguageAPI.Add("BCM_NEXT_GARAGE_GENERATOR", "Generator");
		LanguageAPI.Add("BCM_NEXT_GARAGE_HANGER", "Hanger");
		Part("FRAME", "MIDDLE", "Middleweight Frame", "Balanced. Hardpoint's numbers.");
		Part("FRAME", "LIGHT", "Lightweight Frame", "<style=cIsUtility>+20% speed</style>, longer and cheaper quick boosts. <style=cIsHealth>-30% AP</style>, <style=cIsHealth>-40% PA</style>. Glass cannon.");
		Part("FRAME", "HEAVY", "Heavyweight Frame", "<style=cIsHealth>+35% AP</style>, <style=cIsUtility>+25 armor</style>, <style=cIsUtility>+50% PA</style>. -15% speed, quick boosts shorter and pricier.");
		Part("BOOSTER", "STANDARD", "Standard Boosters", "Balanced quick and overed boost.");
		Part("BOOSTER", "QUICK", "Quick Boost Type", "<style=cIsUtility>+25% quick boost distance</style> (+15% EN). Slower overed boost.");
		Part("BOOSTER", "OVERED", "Overed Boost Type", "<style=cIsUtility>+35% overed boost speed</style>, -20% its EN. Shorter quick boosts.");
		Part("GENERATOR", "STANDARD", "Standard Generator", "Balanced EN.");
		Part("GENERATOR", "OUTPUT", "High-Output Generator", "<style=cIsUtility>+40% EN recovery</style>, -20% capacity.");
		Part("GENERATOR", "CAPACITY", "High-Capacity Generator", "<style=cIsUtility>+50% EN capacity</style>, -15% recovery.");
		Part("GENERATOR", "KOJIMA", "Kojima Generator", "<style=cIsUtility>+60% PA regen</style>, +20% PA. Less EN.");
		Part("HANGER", "SHOTGUN", "Hanger: Shotgun", "F swaps the right arm to a shotgun.");
		Part("HANGER", "LINEAR", "Hanger: Linear Rifle", "F swaps the right arm to a piercing linear rifle.");
		Part("HANGER", "NONE", "No Hanger", "Burst Rifle only.");
		static void Part(string row, string v, string name, string desc)
		{
			LanguageAPI.Add("BCM_NEXT_" + row + "_" + v + "_NAME", name);
			LanguageAPI.Add("BCM_NEXT_" + row + "_" + v + "_DESCRIPTION", desc);
		}
		static string Pct(float v)
		{
			return (v * 100f).ToString("0") + "%";
		}
	}
}
