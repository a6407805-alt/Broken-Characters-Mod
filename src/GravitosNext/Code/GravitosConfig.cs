using BepInEx.Configuration;

namespace GravitosNext;

internal static class GravitosConfig
{
	public static ConfigEntry<float> BaseHealth;

	public static ConfigEntry<float> BaseShield;

	public static ConfigEntry<float> ShieldPerLevel;

	public static ConfigEntry<float> BaseDamage;

	public static ConfigEntry<float> DamagePerLevel;

	public static ConfigEntry<float> BaseArmor;

	public static ConfigEntry<float> MoveSpeed;

	public static ConfigEntry<float> PassiveRadius;

	public static ConfigEntry<float> PassiveSlamDamage;

	public static ConfigEntry<float> PassiveCooldown;

	public static ConfigEntry<float> PassiveLiftDuration;

	public static ConfigEntry<float> ProjectileSlowFactor;

	public static ConfigEntry<bool> ShowBubble;

	public static ConfigEntry<float> SelectDamage;

	public static ConfigEntry<float> SelectDuration;

	public static ConfigEntry<float> SelectRange;

	public static ConfigEntry<float> SelectAngle;

	public static ConfigEntry<bool> SelectSmallOnly;

	public static ConfigEntry<float> LanceChargeTime;

	public static ConfigEntry<float> LanceDuration;

	public static ConfigEntry<float> LanceDamagePerSecond;

	public static ConfigEntry<float> LanceTickInterval;

	public static ConfigEntry<float> LanceProcCoefficient;

	public static ConfigEntry<float> LanceBeamRadius;

	public static ConfigEntry<float> LanceSplashRadius;

	public static ConfigEntry<float> LanceTurnRate;

	public static ConfigEntry<float> LanceChargeMoveSpeed;

	public static ConfigEntry<float> LanceFireMoveSpeed;

	public static ConfigEntry<float> LanceCooldown;

	public static ConfigEntry<float> LanceRange;

	public static ConfigEntry<float> LanceChainFraction;

	public static ConfigEntry<int> LanceChainBounces;

	public static ConfigEntry<float> LanceChainRange;

	public static ConfigEntry<float> LanceSlamDamage;

	public static ConfigEntry<float> IonCooldown;

	public static ConfigEntry<float> IonDuration;

	public static ConfigEntry<float> IonDamagePerSecond;

	public static ConfigEntry<float> IonBeamRadius;

	public static ConfigEntry<float> IonBeamSpread;

	public static ConfigEntry<float> IonBeamMoveSpeed;

	public static ConfigEntry<float> IonSpinSpeed;

	public static ConfigEntry<float> IonBeamHeight;

	public static ConfigEntry<float> IonAltitude;

	public static ConfigEntry<float> IonFlySpeed;

	public static ConfigEntry<float> IonCameraDistance;

	public static ConfigEntry<float> PullCooldown;

	public static ConfigEntry<float> PullMinCooldown;

	public static ConfigEntry<float> PullShieldReference;

	public static ConfigEntry<float> PullRange;

	public static ConfigEntry<float> PullConeAngle;

	public static ConfigEntry<float> PullMinFalloff;

	public static ConfigEntry<float> PullBossMultiplier;

	public static ConfigEntry<float> PullShieldBaseline;

	public static ConfigEntry<float> PullShieldPerDoubling;

	public static ConfigEntry<float> PullMaxStrength;

	public static ConfigEntry<bool> PullItems;

	public static ConfigEntry<bool> PullChests;

	public static ConfigEntry<bool> PullBarrels;

	public static ConfigEntry<float> WildRideCooldown;

	public static ConfigEntry<float> WildRideDuration;

	public static ConfigEntry<float> WildRideDamagePerSecond;

	public static ConfigEntry<float> WildRideExplosionDamage;

	public static ConfigEntry<float> WildRidePullRadius;

	public static ConfigEntry<float> WildRideDamageRadius;

	public static ConfigEntry<float> WildRideExplosionRadius;

	public static ConfigEntry<float> WildRidePullSpeed;

	public static ConfigEntry<float> WildRideCastRange;

	public static ConfigEntry<bool> WildRideHitsTeammates;

	public static ConfigEntry<float> WildRideTeammateDamageMultiplier;

	public static ConfigEntry<bool> WildRideAffectsSelf;

	public static ConfigEntry<float> HoverHeight;

	public static ConfigEntry<bool> HoverIdleLegs;

	public static ConfigEntry<float> GlideFallSpeed;

	public static ConfigEntry<float> GlideBraking;

	public static ConfigEntry<string> HiddenPartKeywords;

	public static ConfigEntry<bool> DumpModelHierarchy;

	public static void Init(ConfigFile cfg)
	{
		BaseHealth = cfg.Bind("1. Stats", "Base Health", 10f, "Max health. Does not grow with level.");
		BaseShield = cfg.Bind("1. Stats", "Base Energy Shield", 300f, "Energy shield at level 1.");
		ShieldPerLevel = cfg.Bind("1. Stats", "Energy Shield Per Level", 90f, "Energy shield gained per level.");
		BaseDamage = cfg.Bind("1. Stats", "Base Damage", 12f, "");
		DamagePerLevel = cfg.Bind("1. Stats", "Damage Per Level", 2.4f, "");
		BaseArmor = cfg.Bind("1. Stats", "Base Armor", 0f, "");
		MoveSpeed = cfg.Bind("1. Stats", "Move Speed", 7f, "");
		PassiveRadius = cfg.Bind("2. Passive - Event Horizon", "Radius", 15f, "Bubble radius in meters.");
		PassiveSlamDamage = cfg.Bind("2. Passive - Event Horizon", "Slam Damage", 1.5f, "Damage coefficient of the bubble slam.");
		PassiveCooldown = cfg.Bind("2. Passive - Event Horizon", "Slam Cooldown", 2f, "Seconds between bubble slams.");
		PassiveLiftDuration = cfg.Bind("2. Passive - Event Horizon", "Lift Duration", 0.35f, "Seconds enemies are lifted into the air before being slammed down (0 = slam in place). Bosses are never lifted.");
		ProjectileSlowFactor = cfg.Bind("2. Passive - Event Horizon", "Projectile Speed Multiplier", 0.35f, "Enemy projectiles entering the bubble keep this fraction of their speed.");
		ShowBubble = cfg.Bind("2. Passive - Event Horizon", "Show Bubble", defaultValue: true, "Draw the bubble around Gravitos.");
		SelectDamage = cfg.Bind("3. Primary - Select", "Damage", 3f, "");
		SelectDuration = cfg.Bind("3. Primary - Select", "Duration", 0.6f, "Seconds per cast at 1.0 attack speed.");
		SelectRange = cfg.Bind("3. Primary - Select", "Range", 60f, "");
		SelectAngle = cfg.Bind("3. Primary - Select", "Targeting Angle", 15f, "Half-angle of the targeting cone.");
		SelectSmallOnly = cfg.Bind("3. Primary - Select", "Small Enemies Only", defaultValue: false, "If true, Select can only target small enemies. If false, large enemies are crushed in place without being lifted.");
		LanceChargeTime = cfg.Bind("4. Secondary - Graviton Lance Beam", "Charge Time", 4f, "Seconds of wind-up before the beam fires.");
		LanceDuration = cfg.Bind("4. Secondary - Graviton Lance Beam", "Beam Duration", 8f, "Seconds the beam lasts.");
		LanceDamagePerSecond = cfg.Bind("4. Secondary - Graviton Lance Beam", "Damage Per Second", 3f, "Damage coefficient per second to everything in the beam (3 = 300%/s).");
		LanceTickInterval = cfg.Bind("4. Secondary - Graviton Lance Beam", "Tick Interval", 0.25f, "Seconds between damage ticks at 1.0 attack speed. Attack speed makes it tick faster.");
		LanceProcCoefficient = cfg.Bind("4. Secondary - Graviton Lance Beam", "Proc Coefficient", 0.5f, "Per tick.");
		LanceBeamRadius = cfg.Bind("4. Secondary - Graviton Lance Beam", "Beam Radius", 2.5f, "Thickness of the beam (hit radius and visual).");
		LanceSplashRadius = cfg.Bind("4. Secondary - Graviton Lance Beam", "Impact Splash Radius", 5f, "Area damaged where the beam lands.");
		LanceTurnRate = cfg.Bind("4. Secondary - Graviton Lance Beam", "Turn Rate", 120f, "Degrees per second the beam can turn to follow your aim.");
		LanceChargeMoveSpeed = cfg.Bind("4. Secondary - Graviton Lance Beam", "Charge Move Speed", 0.8f, "Move speed multiplier while charging.");
		LanceFireMoveSpeed = cfg.Bind("4. Secondary - Graviton Lance Beam", "Beam Move Speed", 0.6f, "Move speed multiplier while firing.");
		LanceCooldown = cfg.Bind("4. Secondary - Graviton Lance Beam", "Cooldown", 8f, "Starts after the beam ends.");
		LanceRange = cfg.Bind("4. Secondary - Graviton Lance Beam", "Range", 100f, "");
		LanceChainFraction = cfg.Bind("4. Secondary - Graviton Lance Beam", "Chain Damage Fraction", 0.4f, "Each chain-shock arc deals this fraction of one beam tick.");
		LanceChainBounces = cfg.Bind("4. Secondary - Graviton Lance Beam", "Chain Bounces", 3, "");
		LanceChainRange = cfg.Bind("4. Secondary - Graviton Lance Beam", "Chain Range", 20f, "");
		LanceSlamDamage = cfg.Bind("4. Secondary - Graviton Lance Beam", "Slam Damage", 1.5f, "Damage when a small enemy hit by the beam is picked up and slammed (at most every 2s per enemy).");
		HoverHeight = cfg.Bind("8. Movement", "Hover Height", 0.6f, "How far above the ground Gravitos floats (visual).");
		HoverIdleLegs = cfg.Bind("8. Movement", "Float Without Walking", defaultValue: true, "Keep the legs still while moving so he drifts instead of walking.");
		GlideFallSpeed = cfg.Bind("8. Movement", "Glide Fall Speed", 4f, "Max fall speed while holding jump in the air.");
		GlideBraking = cfg.Bind("8. Movement", "Glide Braking", 80f, "How quickly a fall slows to glide speed (m/s per second).");
		IonCooldown = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Cooldown", 55f, "Starts after Gravitos lands again.");
		IonDuration = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Duration", 25f, "Seconds the four beams last.");
		IonDamagePerSecond = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Damage Per Second", 2.5f, "Per beam, to everything in it and where it lands. An enemy standing in several beams is still hit once per tick.");
		IonBeamRadius = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Beam Radius", 3f, "");
		IonBeamSpread = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Beam Spread", 7f, "Distance of each beam from the center of the formation.");
		IonBeamMoveSpeed = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Beam Move Speed", 30f, "How fast (m/s) the formation chases your crosshair.");
		IonSpinSpeed = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Spin Speed", 25f, "Degrees per second the formation rotates.");
		IonBeamHeight = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Beam Height", 150f, "How high above the ground the beams start.");
		IonAltitude = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Flight Altitude", 40f, "How high Gravitos flies.");
		IonFlySpeed = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Flight Speed", 14f, "WASD speed while in the sky.");
		IonCameraDistance = cfg.Bind("4b. Secondary - Orbital Ion Cannon", "Camera Distance", 70f, "How far the camera pulls back.");
		PullCooldown = cfg.Bind("5. Utility - PULL", "Cooldown", 15f, "Cooldown at the reference energy shield value.");
		PullMinCooldown = cfg.Bind("5. Utility - PULL", "Min Cooldown", 4f, "Cooldown cannot drop below this no matter how much shield you have.");
		PullShieldReference = cfg.Bind("5. Utility - PULL", "Shield Reference", 300f, "Cooldown = Cooldown * sqrt(Reference / max shield). More max shield = faster PULL.");
		PullRange = cfg.Bind("5. Utility - PULL", "Range", 50f, "");
		PullConeAngle = cfg.Bind("5. Utility - PULL", "Cone Angle", 30f, "Half-angle of the pull cone.");
		PullMinFalloff = cfg.Bind("5. Utility - PULL", "Min Pull Strength", 0.25f, "Fraction of the distance closed for targets at max range (targets right in front are pulled all the way).");
		PullBossMultiplier = cfg.Bind("5. Utility - PULL", "Boss Pull Multiplier", 0.35f, "Bosses and champions are pulled this much as hard.");
		PullShieldBaseline = cfg.Bind("5. Utility - PULL", "Shield Strength Baseline", 300f, "Max shield at which PULL has normal (1x) strength.");
		PullShieldPerDoubling = cfg.Bind("5. Utility - PULL", "Shield Per Doubling", 50f, "Pull strength doubles for every this much max shield above the baseline (and halves for every this much below).");
		PullMaxStrength = cfg.Bind("5. Utility - PULL", "Max Strength Multiplier", 64f, "Cap on the shield strength multiplier.");
		PullItems = cfg.Bind("5. Utility - PULL", "Pull Item Pickups", defaultValue: true, "");
		PullChests = cfg.Bind("5. Utility - PULL", "Pull Chests", defaultValue: true, "Pull chests / boxes.");
		PullBarrels = cfg.Bind("5. Utility - PULL", "Pull Barrels", defaultValue: false, "Also pull money barrels.");
		WildRideCooldown = cfg.Bind("6. Special - Wild Ride", "Cooldown", 40f, "");
		WildRideDuration = cfg.Bind("6. Special - Wild Ride", "Duration", 10f, "");
		WildRideDamagePerSecond = cfg.Bind("6. Special - Wild Ride", "Damage Per Second", 1.5f, "");
		WildRideExplosionDamage = cfg.Bind("6. Special - Wild Ride", "Explosion Damage", 30f, "");
		WildRidePullRadius = cfg.Bind("6. Special - Wild Ride", "Pull Radius", 30f, "");
		WildRideDamageRadius = cfg.Bind("6. Special - Wild Ride", "Damage Radius", 15f, "");
		WildRideExplosionRadius = cfg.Bind("6. Special - Wild Ride", "Explosion Radius", 25f, "");
		WildRidePullSpeed = cfg.Bind("6. Special - Wild Ride", "Pull Speed", 18f, "");
		WildRideCastRange = cfg.Bind("6. Special - Wild Ride", "Cast Range", 60f, "");
		WildRideHitsTeammates = cfg.Bind("6. Special - Wild Ride", "Affects Teammates", defaultValue: true, "Wild Ride pulls and damages teammates.");
		WildRideTeammateDamageMultiplier = cfg.Bind("6. Special - Wild Ride", "Teammate Damage Multiplier", 1f, "Damage multiplier against teammates.");
		WildRideAffectsSelf = cfg.Bind("6. Special - Wild Ride", "Affects Gravitos", defaultValue: false, "Wild Ride also pulls and damages Gravitos himself.");
		HiddenPartKeywords = cfg.Bind("7. Model", "Hidden Bone Keywords", "cannonjoint1", "Comma-separated. Any bone or renderer whose name contains one of these (case and punctuation ignored) is hidden. Used to remove Engineer's backpack arms.");
		DumpModelHierarchy = cfg.Bind("7. Model", "Dump Model Hierarchy", defaultValue: true, "Log every bone and renderer name of the model once per launch, so you can pick keywords above.");
	}
}
