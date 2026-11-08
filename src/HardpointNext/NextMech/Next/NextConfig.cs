using BepInEx.Configuration;
using UnityEngine;

namespace NextMech;

// Settings for everything NEXT adds on top of Hardpoint (AC4 / For Answer systems).
public static class NextConfig
{
	public static ConfigEntry<float> QbCost;
	public static ConfigEntry<float> QbInterval;
	public static ConfigEntry<float> SecondStageWindow;
	public static ConfigEntry<float> SecondStageDistance;
	public static ConfigEntry<float> SecondStageCost;

	public static ConfigEntry<float> PaMax;
	public static ConfigEntry<float> PaReduction;
	public static ConfigEntry<float> PaRegen;
	public static ConfigEntry<float> PaRegenDelay;
	public static ConfigEntry<float> PaHitDrain;
	public static ConfigEntry<float> PaDamageDrain;

	public static ConfigEntry<KeyboardShortcut> ObKey;
	public static ConfigEntry<float> ObSpeed;
	public static ConfigEntry<float> ObCharge;
	public static ConfigEntry<float> ObStartCost;
	public static ConfigEntry<float> ObCost;
	public static ConfigEntry<float> ObPaDrain;
	public static ConfigEntry<float> SpeedometerScale;

	public static ConfigEntry<float> ShotgunDamage;
	public static ConfigEntry<int> ShotgunPellets;
	public static ConfigEntry<float> ShotgunInterval;
	public static ConfigEntry<float> LinearDamage;
	public static ConfigEntry<float> LinearInterval;

	public static ConfigEntry<KeyboardShortcut> LeftBackKey;
	public static ConfigEntry<int> BoosterChoice;
	public static ConfigEntry<int> GeneratorChoice;
	public static ConfigEntry<float> PulseGunDamage;
	public static ConfigEntry<float> PulseGunEn;
	public static ConfigEntry<float> BazookaDamage;
	public static ConfigEntry<float> BazookaRadius;
	public static ConfigEntry<float> OverweightPenalty;
	public static ConfigEntry<float> AssaultRifleDamage;
	public static ConfigEntry<float> SmgDamage;
	public static ConfigEntry<float> HandgunDamage;
	public static ConfigEntry<float> GatlingDamage;
	public static ConfigEntry<float> PileBunkerDamage;
	public static ConfigEntry<float> GrenadeDamage;
	public static ConfigEntry<float> GrenadeRadius;
	public static ConfigEntry<float> LaserDamage;
	public static ConfigEntry<float> LaserEnCost;
	public static ConfigEntry<int> LoadLight;
	public static ConfigEntry<int> LoadMiddle;
	public static ConfigEntry<int> LoadHeavy;

	public static ConfigEntry<float> AirGravity;
	public static ConfigEntry<float> FallCap;
	public static ConfigEntry<float> RiseSpeed;
	public static ConfigEntry<float> VerticalThrust;
	public static ConfigEntry<float> AirControl;
	public static ConfigEntry<float> AirBoostSpeed;
	public static ConfigEntry<float> AirBoostAccel;
	public static ConfigEntry<float> AirBoostCost;
	public static ConfigEntry<float> HeavyLandingSpeed;

	public static ConfigEntry<KeyboardShortcut> TuneKey;
	public static ConfigEntry<float> TunePointsPerLevel;
	public static ConfigEntry<float> TunePerPoint;
	public static ConfigEntry<int> TuneMaxPerStat;

	public static void Init(ConfigFile cfg)
	{
		QbCost = cfg.Bind("N1. Quick Boost (AC4)", "EN Cost", 15f, "EN per quick boost. No charges: chain them as long as EN lasts.");
		QbInterval = cfg.Bind("N1. Quick Boost (AC4)", "Interval", 0.22f, "Seconds between quick boosts.");
		SecondStageWindow = cfg.Bind("N1. Quick Boost (AC4)", "Second Stage Window", 0.4f, "Press Utility again within this many seconds for a second-stage quick boost.");
		SecondStageDistance = cfg.Bind("N1. Quick Boost (AC4)", "Second Stage Distance", 1.7f, "Distance multiplier of a second-stage quick boost.");
		SecondStageCost = cfg.Bind("N1. Quick Boost (AC4)", "Second Stage Cost", 1.6f, "EN multiplier of a second-stage quick boost.");

		PaMax = cfg.Bind("N2. Primal Armor", "Max PA", 100f, "Primal Armor points (Mid frame, standard generator).");
		PaReduction = cfg.Bind("N2. Primal Armor", "Damage Reduction At Full", 0.45f, "Damage blocked by full PA (0.45 = 45%). Scales down as PA drops.");
		PaRegen = cfg.Bind("N2. Primal Armor", "Regen", 14f, "PA per second.");
		PaRegenDelay = cfg.Bind("N2. Primal Armor", "Regen Delay", 1.2f, "Seconds after a hit before PA regenerates.");
		PaHitDrain = cfg.Bind("N2. Primal Armor", "Drain Per Hit", 3f, "PA lost on every hit.");
		PaDamageDrain = cfg.Bind("N2. Primal Armor", "Drain Per Damage", 120f, "PA lost per 100% of max AP taken (scaled by the damage).");

		ObKey = cfg.Bind("N3. Overed Boost", "Key", new KeyboardShortcut(KeyCode.V), "Press to light the overed boost, press again (or Quick Boost) to stop.");
		ObSpeed = cfg.Bind("N3. Overed Boost", "Speed", 58f, "Metres per second.");
		ObCharge = cfg.Bind("N3. Overed Boost", "Charge Time", 0.45f, "Seconds of spool-up before it launches.");
		ObStartCost = cfg.Bind("N3. Overed Boost", "Ignition Cost", 15f, "EN spent when it ignites.");
		ObCost = cfg.Bind("N3. Overed Boost", "EN Cost", 22f, "EN per second while boosting.");
		ObPaDrain = cfg.Bind("N3. Overed Boost", "PA Drain", 10f, "Primal Armor lost per second while boosting (like For Answer).");
		SpeedometerScale = cfg.Bind("N3. Overed Boost", "Speedometer Scale", 1f, "1 = real km/h.");

		ShotgunDamage = cfg.Bind("N6. Arsenal", "Shotgun Damage", 0.6f, "Per pellet (60%).");
		ShotgunPellets = cfg.Bind("N6. Arsenal", "Shotgun Pellets", 8, "");
		ShotgunInterval = cfg.Bind("N6. Arsenal", "Shotgun Interval", 0.85f, "Seconds between shots.");
		LinearDamage = cfg.Bind("N6. Arsenal", "Linear Rifle Damage", 3f, "Per shot (300%), pierces.");
		LinearInterval = cfg.Bind("N6. Arsenal", "Linear Rifle Interval", 0.7f, "Seconds between shots.");

		LeftBackKey = cfg.Bind("N6. Arsenal", "Left Back Key", new KeyboardShortcut(KeyCode.G), "Fires the left back unit (R fires the right one).");
		LoadLight = cfg.Bind("N6. Arsenal", "Max Load Light", 90, "Max weapon load on the Light frame.");
		LoadMiddle = cfg.Bind("N6. Arsenal", "Max Load Middle", 120, "Max weapon load on the Middle frame.");
		LoadHeavy = cfg.Bind("N6. Arsenal", "Max Load Heavy", 160, "Max weapon load on the Heavy frame.");
		OverweightPenalty = cfg.Bind("N6. Arsenal", "Overweight Penalty Per Point", 0.008f, "Per point over the load: -0.8% speed, quick boost distance and EN recovery (max -50%).");
		PulseGunDamage = cfg.Bind("N6. Arsenal", "Pulse Gun Damage", 0.85f, "Energy weapon: no ammo.");
		PulseGunEn = cfg.Bind("N6. Arsenal", "Pulse Gun EN Per Shot", 3f, "");
		BazookaDamage = cfg.Bind("N6. Arsenal", "Bazooka Damage", 8f, "");
		BazookaRadius = cfg.Bind("N6. Arsenal", "Bazooka Radius", 6f, "");
		BoosterChoice = cfg.Bind("N7. Assembly (set on the T screen)", "Booster", 0, "0 Standard, 1 Quick Boost type, 2 Overed Boost type.");
		GeneratorChoice = cfg.Bind("N7. Assembly (set on the T screen)", "Generator", 0, "0 Standard, 1 High-Output, 2 High-Capacity.");
		AssaultRifleDamage = cfg.Bind("N6. Arsenal", "Assault Rifle Damage", 0.75f, "");
		SmgDamage = cfg.Bind("N6. Arsenal", "SMG Damage", 0.4f, "");
		HandgunDamage = cfg.Bind("N6. Arsenal", "Handgun Damage", 1.8f, "");
		GatlingDamage = cfg.Bind("N6. Arsenal", "Gatling Damage", 0.5f, "");
		PileBunkerDamage = cfg.Bind("N6. Arsenal", "Pile Bunker Damage", 14f, "Doubled on an ACS-overloaded target.");
		GrenadeDamage = cfg.Bind("N6. Arsenal", "Grenade Cannon Damage", 12f, "");
		GrenadeRadius = cfg.Bind("N6. Arsenal", "Grenade Cannon Radius", 10f, "");
		LaserDamage = cfg.Bind("N6. Arsenal", "Laser Cannon Damage", 20f, "");
		LaserEnCost = cfg.Bind("N6. Arsenal", "Laser Cannon EN Cost", 30f, "");
		AirGravity = cfg.Bind("N8. Floaty Movement (AC4)", "Air Gravity", 0.45f, "Fraction of normal gravity in the air (1 = vanilla).");
		FallCap = cfg.Bind("N8. Floaty Movement (AC4)", "Max Fall Speed", 16f, "Metres per second.");
		RiseSpeed = cfg.Bind("N8. Floaty Movement (AC4)", "Vertical Boost Speed", 11f, "Top climb speed holding Jump in the air.");
		VerticalThrust = cfg.Bind("N8. Floaty Movement (AC4)", "Vertical Boost Thrust", 38f, "How fast it reaches that speed. Let go and you keep the momentum.");
		AirControl = cfg.Bind("N8. Floaty Movement (AC4)", "Air Control", 0.12f, "Steering without boosting (vanilla 0.25). Lower = more drift.");
		AirBoostSpeed = cfg.Bind("N8. Floaty Movement (AC4)", "Air Boost Speed", 1.35f, "Hold Sprint in the air: glide at this many times your speed.");
		AirBoostAccel = cfg.Bind("N8. Floaty Movement (AC4)", "Air Boost Steering", 30f, "");
		AirBoostCost = cfg.Bind("N8. Floaty Movement (AC4)", "Air Boost EN Cost", 12f, "EN per second.");
		HeavyLandingSpeed = cfg.Bind("N8. Floaty Movement (AC4)", "Heavy Landing Speed", 12f, "Landing faster than this thuds.");
		TuneKey = cfg.Bind("N5. Tuning", "Key", new KeyboardShortcut(KeyCode.T), "Open the tuning screen (FRS points).");
		TunePointsPerLevel = cfg.Bind("N5. Tuning", "Points Per Level", 1f, "");
		TunePerPoint = cfg.Bind("N5. Tuning", "Boost Per Point", 0.05f, "+5% per point.");
		TuneMaxPerStat = cfg.Bind("N5. Tuning", "Max Points Per Stat", 10, "");
	}
}
