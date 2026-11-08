using R2API;

namespace NextMech;

internal static class Tokens
{
	public static void Add()
	{
		LanguageAPI.Add("BCM_NEXT_NAME", "Hardpoint NEXT");
		LanguageAPI.Add("BCM_NEXT_SUBTITLE", "For Answer");
		LanguageAPI.Add("BCM_NEXT_DESCRIPTION", "Hardpoint, rebuilt as a For Answer NEXT: faster, lighter on its feet, wrapped in Primal Armor and tuned in the garage.<color=#CCD3E0>\n\n< ! > Quick Boost has no charges: chain it as long as EN lasts. Tap it twice for a second-stage quick boost.\n\n< ! > Primal Armor weakens every hit and regenerates. Overed Boost and Assault Armor burn it.\n\n< ! > V: Overed Boost. F: swap to your hanger weapon. T: spend FRS tuning points (one per level).\n\n< ! > Any arm weapon on M1 or M2, two back units (R and G). Frame sets the max load: go over and you are overweight (slower).\n\n< ! > Pick FRAME, BOOSTER, GENERATOR and HANGER parts in the loadout.</color>\n");
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
		Part("FRAME", "MIDDLE", "Middleweight Frame", $"Balanced. Load {NextConfig.LoadMiddle.Value}.");
		Part("FRAME", "LIGHT", "Lightweight Frame", $"<style=cIsUtility>+20% speed</style>, longer and cheaper quick boosts. <style=cIsHealth>-30% AP</style>, <style=cIsHealth>-40% PA</style>. Load {NextConfig.LoadLight.Value}. Glass cannon.");
		Part("FRAME", "HEAVY", "Heavyweight Frame", $"<style=cIsHealth>+35% AP</style>, <style=cIsUtility>+25 armor</style>, <style=cIsUtility>+50% PA</style>. -15% speed, quick boosts shorter and pricier. Load {NextConfig.LoadHeavy.Value}.");
		Part("BOOSTER", "STANDARD", "Standard Boosters", "Balanced quick and overed boost.");
		Part("BOOSTER", "QUICK", "Quick Boost Type", "<style=cIsUtility>+25% quick boost distance</style> (+15% EN). Slower overed boost.");
		Part("BOOSTER", "OVERED", "Overed Boost Type", "<style=cIsUtility>+35% overed boost speed</style>, -20% its EN. Shorter quick boosts.");
		Part("GENERATOR", "STANDARD", "Standard Generator", "Balanced EN.");
		Part("GENERATOR", "OUTPUT", "High-Output Generator", "<style=cIsUtility>+40% EN recovery</style>, -20% capacity.");
		Part("GENERATOR", "CAPACITY", "High-Capacity Generator", "<style=cIsUtility>+50% EN capacity</style>, -15% recovery.");
		Part("GENERATOR", "KOJIMA", "Kojima Generator", "<style=cIsUtility>+60% PA regen</style>, +20% PA. Less EN.");
		Part("HANGER", "SHOTGUN", "Hanger: Shotgun", "Wt 16. F swaps the right arm to a shotgun.");
		Part("HANGER", "LINEAR", "Hanger: Linear Rifle", "Wt 20. F swaps the right arm to a linear rifle.");
		Part("HANGER", "NONE", "No Hanger", "Burst Rifle only.");
		LanguageAPI.Add("BCM_NEXT_GARAGE_LBACK", "Left Back");
		W("BURSTRIFLE", "Burst Rifle", $"Wt 12. {HPConfig.RifleBurst.Value}-round bursts, <style=cIsDamage>{Pct(HPConfig.RifleDamage.Value)}</style> per round. {HPConfig.RifleMagazine.Value}-round magazine.");
		W("ASSAULTRIFLE", "Assault Rifle", $"Wt 14. Full-auto, <style=cIsDamage>{Pct(NextConfig.AssaultRifleDamage.Value)}</style> per round.");
		W("SMG", "SMG", $"Wt 8. Very fast, <style=cIsDamage>{Pct(NextConfig.SmgDamage.Value)}</style> per round, short range.");
		W("SHOTGUN", "Shotgun", $"Wt 16. {NextConfig.ShotgunPellets.Value} pellets x <style=cIsDamage>{Pct(NextConfig.ShotgunDamage.Value)}</style>. Big stagger up close.");
		W("LINEAR", "Linear Rifle", $"Wt 20. One piercing round, <style=cIsDamage>{Pct(NextConfig.LinearDamage.Value)}</style>.");
		W("GATLING", "Gatling Gun", $"Wt 34. Spins up while held, <style=cIsDamage>{Pct(NextConfig.GatlingDamage.Value)}</style> per round.");
		W("HANDGUN", "Handgun", $"Wt 8. Heavy pistol, <style=cIsDamage>{Pct(NextConfig.HandgunDamage.Value)}</style>, strong knockback.");
		W("PULSEBLADE", "Pulse Blade", $"Wt 10. Lunge and slash for <style=cIsDamage>{Pct(HPConfig.BladeDamage.Value)}</style>; press again for the backhand.");
		W("PILEBUNKER", "Pile Bunker", $"Wt 24. Lunge and drive a stake for <style=cIsDamage>{Pct(NextConfig.PileBunkerDamage.Value)}</style>. <style=cIsDamage>Double</style> on an ACS-overloaded target.");
		W("MISSILES", "Missile Salvo", $"Wt 22. Hold to lock up to {HPConfig.MissileMaxLocks.Value} targets, release for {HPConfig.MissileCount.Value} homing missiles x <style=cIsDamage>{Pct(HPConfig.MissileDamage.Value)}</style>.");
		W("GRENADE", "Grenade Cannon", $"Wt 40. A shell for <style=cIsDamage>{Pct(NextConfig.GrenadeDamage.Value)}</style> in {NextConfig.GrenadeRadius.Value:0} m. Braces (stops you) unless on a Heavy frame.");
		W("LASERCANNON", "Laser Cannon", $"Wt 32. Charge, then a piercing beam for <style=cIsDamage>{Pct(NextConfig.LaserDamage.Value)}</style>. Costs {NextConfig.LaserEnCost.Value:0} EN.");
		W("ASSAULTARMOR", "Assault Armor", $"Wt 18. Charge, then a pulse blast for <style=cIsDamage>{Pct(HPConfig.ArmorDamage.Value)}</style> in {HPConfig.ArmorRadius.Value:0} m. Dumps your Primal Armor.");
		static void W(string id, string name, string desc)
		{
			LanguageAPI.Add("BCM_NEXT_W_" + id + "_NAME", name);
			LanguageAPI.Add("BCM_NEXT_W_" + id + "_DESCRIPTION", desc);
		}
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
