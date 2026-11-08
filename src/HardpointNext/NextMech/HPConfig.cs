using BepInEx.Configuration;

namespace NextMech;

internal static class HPConfig
{
	public static ConfigEntry<float> BaseHealth;

	public static ConfigEntry<float> HealthPerLevel;

	public static ConfigEntry<float> BaseRegen;

	public static ConfigEntry<float> BaseDamage;

	public static ConfigEntry<float> DamagePerLevel;

	public static ConfigEntry<float> BaseArmor;

	public static ConfigEntry<float> MoveSpeed;

	public static ConfigEntry<float> SprintMultiplier;

	public static ConfigEntry<float> ModelScale;

	public static ConfigEntry<float> CameraDistance;

	public static ConfigEntry<float> CameraHeight;

	public static ConfigEntry<bool> AcMovement;

	public static ConfigEntry<float> EnCapacity;

	public static ConfigEntry<float> EnRegen;

	public static ConfigEntry<float> EnRegenDelay;

	public static ConfigEntry<float> HoverCost;

	public static ConfigEntry<float> HoverRise;

	public static ConfigEntry<float> RedlineRecover;

	public static ConfigEntry<float> StaggerThreshold;

	public static ConfigEntry<float> StaggerPerDamage;

	public static ConfigEntry<float> StaggerDecay;

	public static ConfigEntry<float> StaggerDecayDelay;

	public static ConfigEntry<float> StunTime;

	public static ConfigEntry<float> OverloadDuration;

	public static ConfigEntry<float> OverloadDamageBonus;

	public static ConfigEntry<float> RifleDamage;

	public static ConfigEntry<float> RifleShotGap;

	public static ConfigEntry<float> RifleBurstGap;

	public static ConfigEntry<float> RifleReload;

	public static ConfigEntry<float> RifleRange;

	public static ConfigEntry<float> RifleSpread;

	public static ConfigEntry<int> RifleBurst;

	public static ConfigEntry<int> RifleMagazine;

	public static ConfigEntry<float> BladeCooldown;

	public static ConfigEntry<float> BladeDamage;

	public static ConfigEntry<float> BladeLunge;

	public static ConfigEntry<float> BladeRadius;

	public static ConfigEntry<float> BladeComboWindow;

	public static ConfigEntry<float> QuickBoostRecharge;

	public static ConfigEntry<float> QuickBoostDistance;

	public static ConfigEntry<float> QuickBoostCost;

	public static ConfigEntry<int> QuickBoostCharges;

	public static ConfigEntry<float> AssaultCooldown;

	public static ConfigEntry<float> AssaultSpeed;

	public static ConfigEntry<float> AssaultCost;

	public static ConfigEntry<float> AssaultMaxTime;

	public static ConfigEntry<float> KickDamage;

	public static ConfigEntry<float> KickRadius;

	public static ConfigEntry<float> MissileCooldown;

	public static ConfigEntry<float> MissileDamage;

	public static ConfigEntry<float> MissileLockTime;

	public static ConfigEntry<float> MissileLockRange;

	public static ConfigEntry<float> MissileLockAngle;

	public static ConfigEntry<float> MissileMaxHold;

	public static ConfigEntry<float> MissileRadius;

	public static ConfigEntry<int> MissileCount;

	public static ConfigEntry<int> MissileMaxLocks;

	public static ConfigEntry<float> ArmorCooldown;

	public static ConfigEntry<float> ArmorDamage;

	public static ConfigEntry<float> ArmorRadius;

	public static ConfigEntry<float> ArmorChargeArmor;

	public static ConfigEntry<bool> HudEnabled;

	public static ConfigEntry<bool> HudHideVanilla;

	public static ConfigEntry<float> HudScale;

	public static ConfigEntry<bool> CustomSounds;

	public static ConfigEntry<bool> VoiceLines;

	public static ConfigEntry<float> Volume;

	public static void Init(ConfigFile cfg)
	{
		BaseHealth = cfg.Bind("1. NEXT", "Base Health", 260f, "AP. A heavy frame (Commando has 110).");
		HealthPerLevel = cfg.Bind("1. NEXT", "Health Per Level", 78f, "");
		BaseRegen = cfg.Bind("1. NEXT", "Base Regen", 1.5f, "");
		BaseDamage = cfg.Bind("1. NEXT", "Base Damage", 13f, "");
		DamagePerLevel = cfg.Bind("1. NEXT", "Damage Per Level", 2.6f, "");
		BaseArmor = cfg.Bind("1. NEXT", "Base Armor", 25f, "");
		MoveSpeed = cfg.Bind("1. NEXT", "Move Speed (NEXT)", 9f, "A NEXT is faster than Hardpoint (7.5).");
		SprintMultiplier = cfg.Bind("1. NEXT", "Ground Boost Multiplier", 1.6f, "Sprinting is the ground boost: this many times the walk speed (Commando: 1.45).");
		AcMovement = cfg.Bind("1. NEXT", "AC6 Movement", defaultValue: true, "(user, Nov 6) His back always faces the camera and the ground boost (sprint) works in every direction, firing included, like Armored Core 6. False = the old Commando-style movement.");
		ModelScale = cfg.Bind("1. NEXT", "Model Scale", 1f, "1 = 3 m tall.");
		CameraDistance = cfg.Bind("1. NEXT", "Camera Distance", 13f, "Metres behind him.");
		CameraHeight = cfg.Bind("1. NEXT", "Camera Height", 3.1f, "Metres above his feet that the camera turns around.");
		EnCapacity = cfg.Bind("2. EN (energy)", "Capacity", 100f, "Boosting and hovering spend EN.");
		EnRegen = cfg.Bind("2. EN (energy)", "Recovery", 45f, "EN per second once he stops spending.");
		EnRegenDelay = cfg.Bind("2. EN (energy)", "Recovery Delay", 0.6f, "Seconds after the last spend before EN recovers.");
		HoverCost = cfg.Bind("2. EN (energy)", "Hover Cost", 28f, "EN per second while hovering (hold Jump in the air).");
		HoverRise = cfg.Bind("2. EN (energy)", "Hover Rise", 4.5f, "Metres per second upward while hovering.");
		RedlineRecover = cfg.Bind("2. EN (energy)", "Redline Recovery", 0.4f, "After running dry (redline) EN must refill to this fraction before he can boost again.");
		StaggerThreshold = cfg.Bind("3. Stagger (ACS)", "Threshold", 100f, "Stagger an enemy builds before it breaks (ACS overload).");
		StaggerPerDamage = cfg.Bind("3. Stagger (ACS)", "Stagger Per 100% Damage", 6f, "Stagger added per 100% of his damage dealt (a 600% blade hit adds 36).");
		StaggerDecay = cfg.Bind("3. Stagger (ACS)", "Decay", 10f, "Stagger lost per second once it hasn't been hit for a while.");
		StaggerDecayDelay = cfg.Bind("3. Stagger (ACS)", "Decay Delay", 2f, "Seconds without his hits before stagger decays.");
		StunTime = cfg.Bind("3. Stagger (ACS)", "Stun Time", 2.5f, "Seconds an overloaded enemy is stunned (bosses can't be stunned).");
		OverloadDuration = cfg.Bind("3. Stagger (ACS)", "Overload Duration", 3f, "Seconds an overloaded enemy takes extra damage.");
		OverloadDamageBonus = cfg.Bind("3. Stagger (ACS)", "Overload Damage Bonus", 0.5f, "+50% damage taken from everyone while overloaded.");
		RifleDamage = cfg.Bind("4. Primary - Burst Rifle", "Damage", 0.9f, "Per round (90%).");
		RifleBurst = cfg.Bind("4. Primary - Burst Rifle", "Rounds Per Burst", 3, "");
		RifleShotGap = cfg.Bind("4. Primary - Burst Rifle", "Round Interval", 0.07f, "Seconds between rounds in a burst.");
		RifleBurstGap = cfg.Bind("4. Primary - Burst Rifle", "Burst Interval", 0.38f, "Seconds after a burst before the next (attack speed shortens it).");
		RifleMagazine = cfg.Bind("4. Primary - Burst Rifle", "Magazine", 30, "Rounds before an automatic reload.");
		RifleReload = cfg.Bind("4. Primary - Burst Rifle", "Reload Time", 1.3f, "");
		RifleRange = cfg.Bind("4. Primary - Burst Rifle", "Range", 220f, "");
		RifleSpread = cfg.Bind("4. Primary - Burst Rifle", "Spread", 0.7f, "Degrees.");
		BladeCooldown = cfg.Bind("5. Secondary - Pulse Blade", "Cooldown", 4f, "");
		BladeDamage = cfg.Bind("5. Secondary - Pulse Blade", "Damage", 6f, "Per slash (600%).");
		BladeLunge = cfg.Bind("5. Secondary - Pulse Blade", "Lunge", 8f, "Metres he lunges with each slash.");
		BladeRadius = cfg.Bind("5. Secondary - Pulse Blade", "Reach", 4.8f, "Metres in front of him the slash hits.");
		BladeComboWindow = cfg.Bind("5. Secondary - Pulse Blade", "Second Slash Window", 1.2f, "Press again within this many seconds for the backhand (no extra cooldown).");
		QuickBoostCharges = cfg.Bind("6. Utility - Quick Boost", "Charges", 3, "");
		QuickBoostRecharge = cfg.Bind("6. Utility - Quick Boost", "Recharge", 1.2f, "Seconds per charge.");
		QuickBoostDistance = cfg.Bind("6. Utility - Quick Boost", "Distance", 9f, "Metres per boost, in the direction you're moving (forward if standing still).");
		QuickBoostCost = cfg.Bind("6. Utility - Quick Boost", "EN Cost", 22f, "");
		AssaultCooldown = cfg.Bind("7. Utility (alt) - Assault Boost", "Cooldown", 6f, "");
		AssaultSpeed = cfg.Bind("7. Utility (alt) - Assault Boost", "Speed", 30f, "Metres per second, where you aim.");
		AssaultCost = cfg.Bind("7. Utility (alt) - Assault Boost", "EN Cost", 24f, "EN per second.");
		AssaultMaxTime = cfg.Bind("7. Utility (alt) - Assault Boost", "Max Time", 5f, "Seconds (press Utility again to stop early).");
		KickDamage = cfg.Bind("7. Utility (alt) - Assault Boost", "Boost Kick Damage", 8f, "Press Jump while assault boosting: a boost kick (800%).");
		KickRadius = cfg.Bind("7. Utility (alt) - Assault Boost", "Boost Kick Reach", 3.6f, "");
		MissileCooldown = cfg.Bind("8. Special - Missile Salvo", "Cooldown", 10f, "");
		MissileDamage = cfg.Bind("8. Special - Missile Salvo", "Damage", 2.5f, "Per missile (250%).");
		MissileCount = cfg.Bind("8. Special - Missile Salvo", "Missiles", 12, "");
		MissileMaxLocks = cfg.Bind("8. Special - Missile Salvo", "Max Locks", 6, "Hold Special to lock targets; release to fire.");
		MissileLockTime = cfg.Bind("8. Special - Missile Salvo", "Lock Time", 0.25f, "Seconds per lock.");
		MissileLockRange = cfg.Bind("8. Special - Missile Salvo", "Lock Range", 90f, "");
		MissileLockAngle = cfg.Bind("8. Special - Missile Salvo", "Lock Cone", 30f, "Degrees from the crosshair.");
		MissileMaxHold = cfg.Bind("8. Special - Missile Salvo", "Max Hold", 2f, "Seconds: it fires on its own after this.");
		MissileRadius = cfg.Bind("8. Special - Missile Salvo", "Blast Radius", 4f, "");
		ArmorCooldown = cfg.Bind("9. Special (alt) - Assault Armor", "Cooldown", 30f, "");
		ArmorDamage = cfg.Bind("9. Special (alt) - Assault Armor", "Damage", 25f, "2500%.");
		ArmorRadius = cfg.Bind("9. Special (alt) - Assault Armor", "Radius", 15f, "");
		ArmorChargeArmor = cfg.Bind("9. Special (alt) - Assault Armor", "Armor While Charging", 100f, "");
		HudEnabled = cfg.Bind("10. HUD", "AC Style HUD", defaultValue: true, "(user) His own HUD and reticle while you play him (only for him).");
		HudHideVanilla = cfg.Bind("10. HUD", "Hide Vanilla HUD Parts", defaultValue: true, "Hide the normal health bar, skill bar and item bar while his HUD is up.");
		HudScale = cfg.Bind("10. HUD", "Scale", 1f, "");
		CustomSounds = cfg.Bind("11. Sounds", "Custom Sounds", defaultValue: true, "Windows only.");
		VoiceLines = cfg.Bind("11. Sounds", "Voice Lines", defaultValue: true, "The combat AI's voice.");
		Volume = cfg.Bind("11. Sounds", "Volume", 75f, "0-100 (75 = the level of the other survivors).");
	}
}
