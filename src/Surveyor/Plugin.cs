using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EntityStates;
using R2API;
using R2API.ContentManagement;
using R2API.Networking;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Surveyor
{
    /// <summary>
    /// The Surveyor: a blind, heavily armored engineer. The world is black; she sees through a decaying LiDAR point
    /// cloud. A rotary minigun that needs to spool, a piercing lance, a hydraulic slide that plants an anchor and an
    /// acoustic shockwave. A Shimmer Demon stalks her and steals her suit's senses. Everything about the LiDAR and
    /// the Demon is local to her own client (nothing to sync, nobody else can see it).
    /// </summary>
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SvrPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.surveyor";
        public const string PluginName = "The Surveyor";
        public const string PluginVersion = "0.7.5";
        public const string T = "BC_SURVEYOR_";

        public static ManualLogSource Log;
        public static SvrPlugin Instance;
        public static GameObject BodyPrefab, DisplayPrefab;
        public static SkillDef MinigunSkillDef, LanceSkillDef;

        // ---------------------------------------------------------------- config
        public static ConfigEntry<float> SpoolSeconds, MinRate, MaxRate, BulletDamage, SpreadMin, SpreadMax, MinigunRange, MoveSlow;
        public static ConfigEntry<float> LanceDamage, LanceCooldown, LanceCharge, LanceForce;
        public static ConfigEntry<float> SlideCooldown, SlideSeconds, SlideSpeed, AnchorSeconds, AnchorDamageBonus;
        public static ConfigEntry<float> PulseDamage, PulseRadius, PulseCooldown, PulseForce;
        public static ConfigEntry<bool> DarkWorld;
        public static ConfigEntry<int> MaxDots, RaysPerFrame, DotDivider;
        public static ConfigEntry<float> ScanRange, DotLife;
        public static ConfigEntry<float> PeekSeconds, PeekCooldown, OverdriveCooldown, SweepSeconds, FocusSeconds, EchoSeconds;
        public static ConfigEntry<string> ScanNextKey, PeekKey, RepulsorKey, MinionKey, CashInKey;
        public static ConfigEntry<bool> UseCustomModel;
        public static ConfigEntry<float> ModelScale, HeavyStepShake;
        public static ConfigEntry<bool> HeavyMotion;
        public static ConfigEntry<float> BlackHoleCooldown, BlackHoleSeconds, BlackHoleRadius, BlackHoleDamage, BlackHoleTickDamage, XrayRange;
        public static ConfigEntry<float> MoonGravity, MoonMaxFall, LeapUp, LeapForward, LeapCooldown, PulseRevealRange, PulseRevealSeconds;
        public static ConfigEntry<float> BlackHoleSpeed, BlackHoleCharge, StalkerArmor, StalkerPerPlayer;
        public static ConfigEntry<bool> StalkerEnabled, StalkerOnlyWithSurveyor;
        public static ConfigEntry<float> StalkerDelayMin, StalkerDelayMax, StalkerHealthMult, StalkerScale, StalkerDarkness;
        public static ConfigEntry<bool> DemonEnabled;
        public static ConfigEntry<float> DemonFirstSpawn, DemonProwlSpeed, DemonHuntSpeed, DemonRespawn, DemonContactRange;
        public static ConfigEntry<float> MinigunPushSpool, LanceStagger, PulseStagger, MinigunStagger;
        public static ConfigEntry<int> MaxSensesLost;
        public static ConfigEntry<float> SightLossMult, BalanceSlow, BalanceSpool, HearingLossVolume;
        public static ConfigEntry<string> HearingRtpc;

        private string Folder(string sub) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), sub);

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            SetupConfig();
            SvrIcons.Folder = Folder("Icons");

            RegisterLanguage();
            CreateBody();
            SvrBlackHole.Create();
            SvrAmmo.Create();
            CreateSkills();
            RegisterSurvivor();
            SvrStalker.Create();
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            SceneManager.activeSceneChanged += OnSceneChanged;
            Run.onRunStartGlobal += OnRunStart;
            SvrAudio.ResetVolume();

            Logger.LogInfo(PluginName + " " + PluginVersion + " online.");
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            Run.onRunStartGlobal -= OnRunStart;
            SvrAudio.ResetVolume();
        }

        private static void OnRunStart(Run r)
        {
            SvrUpgrades.ResetRun();
        }

        private static void OnSceneChanged(Scene a, Scene b)
        {
            SvrSenses.ResetStage();
            SvrAudio.ResetVolume();
        }

        private void SetupConfig()
        {
            SpoolSeconds = Config.Bind("1. Minigun", "SpoolSeconds", 1.1f, "Seconds from first pull to the full rate of fire (before attack speed).");
            MinRate = Config.Bind("1. Minigun", "MinShotsPerSecond", 2.5f, "Rate of fire as soon as the barrels turn.");
            MaxRate = Config.Bind("1. Minigun", "MaxShotsPerSecond", 13f, "Rate of fire fully spooled.");
            BulletDamage = Config.Bind("1. Minigun", "BulletDamage", 0.32f, "Damage per bullet (0.32 = 32%). Fully spooled that is about 400% per second.");
            SpreadMin = Config.Bind("1. Minigun", "SpreadFullySpooled", 1.1f, "Spread in degrees at full spool.");
            SpreadMax = Config.Bind("1. Minigun", "SpreadJustStarted", 4.5f, "Spread in degrees right after the barrels start turning.");
            MinigunRange = Config.Bind("1. Minigun", "Range", 90f, "Bullet range in metres.");
            MoveSlow = Config.Bind("1. Minigun", "MoveSlowWhileSpooled", 0.25f, "Move speed lost while the barrels are spooled (0.25 = -25%). Anchoring removes it.");

            LanceDamage = Config.Bind("2. Lance", "Damage", 6f, "Surveyor Lance damage (6 = 600%). Pierces everything.");
            LanceCooldown = Config.Bind("2. Lance", "Cooldown", 6f, "Seconds between lances (it cannot be held and spammed).");
            LanceCharge = Config.Bind("2. Lance", "ChargeSeconds", 0.5f, "Wind-up before the lance fires.");
            LanceForce = Config.Bind("2. Lance", "Force", 3500f, "Knockback on enemies.");

            SlideCooldown = Config.Bind("3. Slide and Anchor", "Cooldown", 7f, "Seconds. Starts when the slide or anchor ends.");
            SlideSeconds = Config.Bind("3. Slide and Anchor", "SlideSeconds", 0.45f, "Length of the slide.");
            SlideSpeed = Config.Bind("3. Slide and Anchor", "SlideSpeedMultiplier", 3f, "Slide speed at the start (times move speed). It slows down to normal.");
            AnchorSeconds = Config.Bind("3. Slide and Anchor", "AnchorSeconds", 3f, "Hold Shift through the slide to plant the anchor for up to this long.");
            AnchorDamageBonus = Config.Bind("3. Slide and Anchor", "AnchorDamageBonus", 0.4f, "Minigun damage while anchored (0.4 = +40%). The Lance gets +25%.");

            PulseDamage = Config.Bind("4. LiDAR Pulse", "Damage_v2", 4f, "R LiDAR Pulse damage (4 = 400%).");
            PulseRadius = Config.Bind("4. LiDAR Pulse", "Radius_v2", 22f, "R damage radius in metres.");
            PulseCooldown = Config.Bind("4. LiDAR Pulse", "Cooldown_v2", 12f, "R cooldown (seconds).");
            PulseForce = Config.Bind("4. Acoustic Shockwave", "Force", 2500f, "Knockback on enemies.");

            DarkWorld = Config.Bind("5. LiDAR", "DarkWorld", true, "The world is black and you only see the LiDAR dots. Turn off to see the normal game with the dots on top (handy for testing).");
            MaxDots = Config.Bind("5. LiDAR", "MaxDots_v3", 65000, "Most dots kept at once (the point cloud is packed; lower this on a slow PC).");
            RaysPerFrame = Config.Bind("5. LiDAR", "ScanRaysPerFrame_v3", 320, "Rays cast per frame by the always-on scanner (they spiral over a wide cone so the cloud fills in evenly).");
            ScanRange = Config.Bind("5. LiDAR", "ScanRange_v2", 95f, "Scanner range in metres.");
            DotLife = Config.Bind("5. LiDAR", "DotSeconds_v2", 9f, "How long a dot stays before it fades.");
            DotDivider = Config.Bind("5. LiDAR", "DotResolutionDivider_v1", 2, "The cloud is drawn at screen size divided by this (2 = every dot is a 2x2 block at 1080p; 1 = finest, slowest; 3 = chunkier).");

            PeekSeconds = Config.Bind("8. Peek and Overdrive", "PeekSeconds_v1", 10.0f, "How long the visor stays off.");
            PeekCooldown = Config.Bind("8. Peek and Overdrive", "PeekCooldown_v1", 12.0f, "Seconds. Starts when you use it.");
            OverdriveCooldown = Config.Bind("8. Peek and Overdrive", "OverdriveCooldown_v1", 14f, "Seconds.");
            SweepSeconds = Config.Bind("8. Peek and Overdrive", "SweepSeconds_v1", 3.2f, "SWEEP mode: seconds for the scan line to spin 360 degrees.");
            FocusSeconds = Config.Bind("8. Peek and Overdrive", "FocusSeconds_v1", 4f, "FOCUS mode: seconds of the long dense beam.");
            EchoSeconds = Config.Bind("8. Peek and Overdrive", "EchoSeconds_v1", 8f, "ECHO mode: seconds enemies (and the Shimmer Demon) are lit through walls.");
            UseCustomModel = Config.Bind("9. Model", "UseCustomModel_v1", true, "Use the Surveyor's own 3D model (surveyor_model.bin.gz next to the dll). Off = Commando with a green tint.");
            ModelScale = Config.Bind("9. Model", "ModelScale_v1", 1.08f, "Size of the armoured suit on the Commando skeleton (1 = fitted to the skeleton).");
            HeavyMotion = Config.Bind("9. Model", "HeavyMotion_v1", true, "Heavy-gunner body language: both hands on the gun, gun lag, lean, sway, recoil.");
            HeavyStepShake = Config.Bind("9. Model", "StepShake_v1", 1f, "Camera thud on each heavy step and landing (0 = off).");
            BlackHoleCooldown = Config.Bind("10. Black Hole", "Cooldown_v1", 10f, "Seconds between Black Hole orbs (M2 in BLACK HOLE mode).");
            BlackHoleSeconds = Config.Bind("10. Black Hole", "Seconds_v2", 4.5f, "How long the black hole flies (pulling everything along its path) before it implodes.");
            BlackHoleSpeed = Config.Bind("10. Black Hole", "Speed_v2", 14f, "Metres per second. It flies in a straight line at this steady speed.");
            BlackHoleCharge = Config.Bind("10. Black Hole", "ChargeSeconds_v2", 0.6f, "Wind-up before it fires.");
            BlackHoleRadius = Config.Bind("10. Black Hole", "Radius_v1", 16f, "Metres. Enemies and projectiles inside are pulled to the centre.");
            BlackHoleDamage = Config.Bind("10. Black Hole", "ImplosionDamage_v1", 8f, "Implosion damage (8 = 800%), with a stun.");
            BlackHoleTickDamage = Config.Bind("10. Black Hole", "TickDamage_v1", 0.4f, "Damage every half second near the event horizon (0.4 = 40%).");
            XrayRange = Config.Bind("5. LiDAR", "ChestXrayRange_v1", 14f, "Metres. Closer than this, chests show a wireframe box, a tier nameplate and what is inside.");
            MoonGravity = Config.Bind("11. Moon", "Gravity_v1", 0.4f, "Gravity while airborne (1 = normal, 0.4 = moon). No fall damage.");
            MoonMaxFall = Config.Bind("11. Moon", "MaxFallSpeed_v1", 22f, "Fastest she can fall (m/s).");
            LeapUp = Config.Bind("11. Moon", "BoostUp_v2", 6f, "Thruster Boost (Shift): upward kick (m/s).");
            LeapForward = Config.Bind("11. Moon", "BoostSpeed_v2", 40f, "Thruster Boost (Shift): burst speed (m/s). It bleeds off over about a second.");
            LeapCooldown = Config.Bind("11. Moon", "BoostCooldown_v2", 4f, "Thruster Boost cooldown (seconds).");
            PulseRevealRange = Config.Bind("12. LiDAR Pulse", "RevealRange_v1", 80f, "R: enemies this close are marked for the whole team (the Shimmer Stalker at any range).");
            PulseRevealSeconds = Config.Bind("12. LiDAR Pulse", "RevealSeconds_v1", 8f, "R: how long the marks last.");
            StalkerEnabled = Config.Bind("13. Shimmer Stalker", "Enabled_v1", true, "Night-stalker event (host's setting decides).");
            StalkerOnlyWithSurveyor = Config.Bind("13. Shimmer Stalker", "OnlyWithSurveyor_v1", true, "Only when someone in the run plays the Surveyor.");
            StalkerDelayMin = Config.Bind("13. Shimmer Stalker", "DelayMin_v1", 150f, "Seconds into a stage, earliest arrival.");
            StalkerDelayMax = Config.Bind("13. Shimmer Stalker", "DelayMax_v1", 240f, "Seconds into a stage, latest arrival.");
            StalkerHealthMult = Config.Bind("13. Shimmer Stalker", "HealthMult_v2", 12f, "Health x Imp Overlord. A raid boss: it takes the whole team.");
            StalkerPerPlayer = Config.Bind("13. Shimmer Stalker", "HealthPerExtraPlayer_v1", 2f, "Extra health per extra player (2 = +200% each).");
            StalkerArmor = Config.Bind("13. Shimmer Stalker", "Armor_v1", 40f, "Extra armor (40 = takes about 30% less damage).");
            StalkerScale = Config.Bind("13. Shimmer Stalker", "Size_v2", 1.35f, "Size x Imp Overlord.");
            StalkerDarkness = Config.Bind("13. Shimmer Stalker", "LightsLeft_v1", 0.35f, "How much stage light is left while it lives (1 = no dimming).");
            ScanNextKey = Config.Bind("8. Peek and Overdrive", "ScanModeKey_v2", "Q", "Tap to cycle scan modes: TOPOGRAPHY -> THERMAL -> SALVAGE -> STRUCTURAL. (E stays free for the game's Interact key.)");
            PeekKey = Config.Bind("8. Peek and Overdrive", "PeekKey_v1", "F", "Peek (visor off) on its own key, whatever skill is in the utility slot.");
            RepulsorKey = Config.Bind("6. Shimmer Demon", "RepulsorBlowKey_v1", "V", "Reverse-Flow Repulsor ('Blow' function): blast away swarms and reflect projectiles.");
            MinionKey = Config.Bind("6. Shimmer Demon", "RedeployMinionKey_v1", "H", "Redeploy a captured spirit from your Containment Cell as a friendly 30s Spectral Hologram Guardian.");
            CashInKey = Config.Bind("6. Shimmer Demon", "CashInTeleporterKey_v1", "B", "Cash in a captured spirit at the stage Teleporter for Gold, Item Scrap/Droplet, and Healing.");

            DemonEnabled = Config.Bind("6. Shimmer Demon", "Enabled", true, "The Shimmer Demon stalks the Surveyor (only she can see it).");
            DemonFirstSpawn = Config.Bind("6. Shimmer Demon", "FirstSpawnSeconds", 80f, "Seconds into a stage before it appears.");
            DemonProwlSpeed = Config.Bind("6. Shimmer Demon", "ProwlSpeed", 3.5f, "Metres per second while it circles.");
            DemonHuntSpeed = Config.Bind("6. Shimmer Demon", "HuntSprintSpeed", 12f, "Metres per second in a hunting sprint (it is slower than a sprinting Surveyor).");
            DemonRespawn = Config.Bind("6. Shimmer Demon", "RespawnSeconds", 50f, "Seconds it stays gone after it feeds.");
            DemonContactRange = Config.Bind("6. Shimmer Demon", "ContactRange", 2.4f, "Metres. Inside this it steals a sense.");
            MaxSensesLost = Config.Bind("6. Shimmer Demon", "MaxSensesLostPerStage", 4, "How many of the 4 senses it can take in one stage.");
            MinigunPushSpool = Config.Bind("6. Shimmer Demon", "MinigunPushAtSpool", 0.85f, "Minigun spool needed to push it (0.6 while anchored). Below this the bullets pass through it.");
            MinigunStagger = Config.Bind("6. Shimmer Demon", "MinigunStaggerSeconds", 0.9f, "Stagger after 4 spooled hits within 0.8 s (each hit also pushes it back 0.9 m).");
            LanceStagger = Config.Bind("6. Shimmer Demon", "LanceStaggerSeconds", 2.5f, "Stagger from a lance hit (also thrown back 10 m).");
            PulseStagger = Config.Bind("6. Shimmer Demon", "ShockwaveStaggerSeconds", 3f, "Stagger from the shockwave (thrown back up to 22 m).");

            SightLossMult = Config.Bind("7. Lost senses", "SightScanMultiplier", 0.5f, "Scan range and density once Sight is stolen.");
            BalanceSlow = Config.Bind("7. Lost senses", "BalanceMoveSlow", 0.1f, "Move speed lost once Balance is stolen.");
            BalanceSpool = Config.Bind("7. Lost senses", "BalanceSpoolMultiplier", 1.3f, "Spool time multiplier once Balance is stolen (also more spread).");
            HearingLossVolume = Config.Bind("7. Lost senses", "HearingVolume", 0.15f, "Game sound volume once Hearing is stolen (0.15 = 15%).");
            HearingRtpc = Config.Bind("7. Lost senses", "HearingRtpcName", "Volume_SFX", "Wwise parameter used to muffle the game when Hearing is stolen.");
        }

        private static void Stats(CharacterBody b, RecalculateStatsAPI.StatHookEventArgs a)
        {
            if (!b) return;
            SvrSpool sp = b.GetComponent<SvrSpool>();
            if (!sp || !b.hasEffectiveAuthority) return;
            float slow = 0f;
            if (sp.Spool > 0.3f && sp.AnchorLeft <= 0f && !SvrUpgrades.Has(SvrUpgrades.GYRO_HARNESS)) slow += MoveSlow.Value;
            if (SvrSenses.Touch) slow += 0.05f;
            if (SvrSenses.Balance) slow += BalanceSlow.Value;
            if (slow > 0f) a.moveSpeedMultAdd -= slow;
            if (SvrUpgrades.Has(SvrUpgrades.COMPRESSION_PRESS)) a.armorAdd += 25f;
        }

        // ---------------------------------------------------------------- language

        private void RegisterLanguage()
        {
            LanguageAPI.Add(T + "NAME", "The Surveyor");
            LanguageAPI.Add(T + "SUBTITLE", "Blind Engineer");
            LanguageAPI.Add(T + "DESCRIPTION",
                "The Surveyor is a blind, heavily armored engineer in a white, orange and blue EVA rig. The world is black: she sees what her LiDAR scanner paints, and she moves like she is on the moon.<color=#CCD3E0>\r\n\r\n" +
                "< ! > Press F to switch LiDAR off and look at the real world, F again to switch it back on.\r\n\r\n" +
                "< ! > LiDAR views: 1 = every monster on the map, 2 = every item, 3 = objectives. Each fires a coloured pulse across the whole stage.\r\n\r\n" +
                "< ! > R swaps ammo: LEAD (fast, cool), INFERNO (huge damage, overheats), FROST (stacks into ice), RAD (a sickness that spreads).\r\n\r\n" +
                "< ! > A few minutes into each stage the lights fail and the Shimmer Stalker comes for whoever strays. It takes the whole team to kill.</color>");
            LanguageAPI.Add(T + "OUTRO", "..and so she left, still hearing the scanner ping.");
            LanguageAPI.Add(T + "FAIL", "..and so she vanished, and the dots went dark.");
            for (int i = 0; i < SvrModel.SkinKeys.Length; i++) LanguageAPI.Add(T + "SKIN_" + SvrModel.SkinKeys[i], SvrModel.SkinNames[i]);

            LanguageAPI.Add(T + "PASSIVE_NAME", "LiDAR Vision / Moon Walker");
            LanguageAPI.Add(T + "PASSIVE_DESC",
                "The screen is dark: an always-on scanner paints the world as a <style=cIsUtility>point cloud</style> with a <style=cIsUtility>radar</style> above your skills. " +
                "<style=cIsUtility>Q</style> cycles LiDAR modes. Press <style=cIsUtility>1 / 2 / 3</style> to pulse the whole map for <style=cIsDamage>monsters</style>, <style=cIsUtility>items</style> or <style=cIsUtility>objectives</style>. F shows the real world. " +
                "<style=cIsUtility>Low gravity</style>: high, slow jumps and <style=cIsUtility>no fall damage</style>.");
            LanguageAPI.Add(T + "PRIMARY_NAME", "Rotary Survey Minigun");
            LanguageAPI.Add(T + "PRIMARY_DESC", "Hold to <style=cIsUtility>spool up</style> and fire a stream of rounds. The longer you hold, the faster it fires. Fire too long and it <style=cIsHealth>overheats</style> (how fast depends on the ammo).");
            LanguageAPI.Add(T + "BLACKHOLE_NAME", "Black Hole");
            LanguageAPI.Add(T + "BLACKHOLE_DESC", "Charge, then fire a singularity that flies straight ahead at a steady speed, <style=cIsUtility>sucking in every enemy along its path</style> for " + BlackHoleSeconds.Value.ToString("0.#") +
                "s, then <style=cIsDamage>implodes for " + (BlackHoleDamage.Value * 100f).ToString("0") + "% damage</style> and <style=cIsUtility>stuns</style>.");
            LanguageAPI.Add(T + "MOONLEAP_NAME", "Thruster Boost");
            LanguageAPI.Add(T + "MOONLEAP_DESC", "Slam every <style=cIsUtility>backpack and boot thruster</style> at once: a sudden <style=cIsUtility>burst of speed</style> where you are moving (or looking). Works in the air.");
            LanguageAPI.Add(T + "AMMOSWAP_NAME", "Ammo Swap");
            LanguageAPI.Add(T + "AMMOSWAP_DESC", "Swap the minigun ammo. <style=cIsUtility>LEAD</style>: fires <style=cIsDamage>4x faster</style> and barely heats. The heavy rounds hit far harder and carry an effect: " +
                "<style=cIsDamage>INFERNO</style> burns (overheats fast), <style=cIsUtility>FROST</style> stacks a slow until <style=cIsUtility>ice locks them in place</style>, " +
                "<style=cIsHealing>RAD</style> stacks radiation that slows and weakens, then <style=cIsDamage>melts down</style>, stunning the whole crowd and spreading.");
        }

        // ---------------------------------------------------------------- body

        public static Transform Model(GameObject root)
        {
            ModelLocator ml = root ? root.GetComponent<ModelLocator>() : null;
            if (ml && ml.modelTransform) return ml.modelTransform;
            CharacterModel cm = root ? root.GetComponentInChildren<CharacterModel>(true) : null;
            return cm ? cm.transform : null;
        }

        private void CreateBody()
        {
            GameObject source = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            BodyPrefab = PrefabAPI.InstantiateClone(source, "BCSurveyorBody", true);

            CharacterBody body = BodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = T + "NAME";
            body.subtitleNameToken = T + "SUBTITLE";
            body.bodyColor = new Color(0.55f, 0.9f, 0.25f);
            body.baseMaxHealth = 150f;
            body.levelMaxHealth = 45f;
            body.baseRegen = 1f;
            body.levelRegen = 0.2f;
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseArmor = 20f;
            body.baseMoveSpeed = 7f;
            body.baseAcceleration = 80f;           // crisp, responsive movement (matching Commando 80)
            body.baseJumpPower = 12f;              // moon gravity: this still jumps ~2x higher, slow and floaty
            body.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;
            CharacterMotor motor = BodyPrefab.GetComponent<CharacterMotor>();
            if (motor) motor.airControl = 0.4f;
            Texture portrait = SvrIcons.Texture("portrait");
            if (portrait) body.portraitIcon = portrait;

            BodyPrefab.AddComponent<SvrSpool>();
            BodyPrefab.AddComponent<SvrLidar>();
            BodyPrefab.AddComponent<SvrMoonGravity>();
            BodyPrefab.AddComponent<SvrThrusters>();
            BodyPrefab.AddComponent<SvrFSlot>();
            Transform model = Model(BodyPrefab);
            if (model) { model.gameObject.AddComponent<SvrLook>(); model.gameObject.AddComponent<SvrCustomModel>(); AddSkin(model); }
            ContentAddition.AddBody(BodyPrefab);

            GameObject display = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            DisplayPrefab = PrefabAPI.InstantiateClone(display, "BCSurveyorDisplay", false);
            CharacterModel dcm = DisplayPrefab.GetComponentInChildren<CharacterModel>(true);
            Transform dm = dcm ? dcm.transform : DisplayPrefab.transform;
            dm.gameObject.AddComponent<SvrLook>();
            dm.gameObject.AddComponent<SvrCustomModel>();
        }

        // The loadout screen wants a skin entry; Commando's own would show Commando's names, so she gets one entry
        // that keeps the model's default materials (SvrLook tints them).
        private static void AddSkin(Transform model)
        {
            try
            {
                ModelSkinController old = model.GetComponent<ModelSkinController>();
                SkinDef baseSkin = null;
                FieldInfo sf = typeof(ModelSkinController).GetField("skins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (old && sf != null)
                {
                    SkinDef[] skins = sf.GetValue(old) as SkinDef[];
                    if (skins != null && skins.Length > 0) baseSkin = skins[0];
                }
                if (!baseSkin) return;
                // one skin per real EVA suit (the suit is repainted from its palette; see SvrModel.SkinRoles)
                SkinDef[] mine = new SkinDef[SvrModel.SkinKeys.Length];
                for (int i = 0; i < mine.Length; i++)
                {
                    SkinDef skin = Object.Instantiate(baseSkin);
                    ((ScriptableObject)skin).name = "skinBCSurveyor" + SvrModel.SkinKeys[i];
                    SetMember(skin, "nameToken", T + "SKIN_" + SvrModel.SkinKeys[i]);
                    Sprite icon = SvrIcons.Get("skin_" + SvrModel.SkinKeys[i].ToLowerInvariant(), SvrIcons.Get("skin", null));
                    if (icon) SetMember(skin, "icon", icon);
                    mine[i] = skin;
                }
                sf.SetValue(old, mine);
            }
            catch (Exception e) { Log.LogWarning("Skin setup skipped: " + e.Message); }
        }

        public static void SetMember(object obj, string name, object value)
        {
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo f = obj.GetType().GetField(name, any);
            if (f != null && (value == null || f.FieldType.IsInstanceOfType(value))) { f.SetValue(obj, value); return; }
            PropertyInfo p = obj.GetType().GetProperty(name, any);
            if (p != null && p.CanWrite && (value == null || p.PropertyType.IsInstanceOfType(value))) p.SetValue(obj, value, null);
        }

        // ---------------------------------------------------------------- skills

        private static SkillDef MakeSkill(string key, Type state, string machine, float cooldown, bool mustKeyPress, bool combat, InterruptPriority prio, string icon, bool onEnd)
        {
            SkillDef d = ScriptableObject.CreateInstance<SkillDef>();
            d.skillName = T + key;
            ((ScriptableObject)d).name = T + key;
            d.skillNameToken = T + key + "_NAME";
            d.skillDescriptionToken = T + key + "_DESC";
            d.icon = SvrIcons.Get(icon, null);
            d.activationState = new SerializableEntityStateType(state);
            d.activationStateMachineName = machine;
            d.baseMaxStock = 1;
            d.rechargeStock = 1;
            d.requiredStock = 1;
            d.stockToConsume = 1;
            d.baseRechargeInterval = cooldown;
            d.beginSkillCooldownOnSkillEnd = onEnd;
            d.isCombatSkill = combat;
            d.mustKeyPress = mustKeyPress;
            d.interruptPriority = prio;
            d.canceledFromSprinting = false;
            d.cancelSprintingOnActivation = combat;
            ContentAddition.AddSkillDef(d);
            return d;
        }

        private void CreateSkills()
        {
            ContentAddition.AddEntityState<SvrMinigunState>(out _);
            ContentAddition.AddEntityState<SvrVacuumState>(out _);
            ContentAddition.AddEntityState<SvrLanceState>(out _);
            ContentAddition.AddEntityState<SvrSlideState>(out _);
            ContentAddition.AddEntityState<SvrAnchorState>(out _);
            ContentAddition.AddEntityState<SvrSwitchWeaponState>(out _);
            ContentAddition.AddEntityState<SvrPeekState>(out _);
            ContentAddition.AddEntityState<SvrOverdriveState>(out _);
            ContentAddition.AddEntityState<SvrBlackHoleState>(out _);
            ContentAddition.AddEntityState<SvrMoonLeapState>(out _);
            ContentAddition.AddEntityState<SvrLidarPulseState>(out _);
            ContentAddition.AddEntityState<SvrAmmoSwapState>(out _);

            SkillLocator sl = BodyPrefab.GetComponent<SkillLocator>();
            foreach (GenericSkill old in BodyPrefab.GetComponentsInChildren<GenericSkill>(true)) Object.DestroyImmediate(old);
            sl.passiveSkill.enabled = true;
            sl.passiveSkill.skillNameToken = T + "PASSIVE_NAME";
            sl.passiveSkill.skillDescriptionToken = T + "PASSIVE_DESC";
            sl.passiveSkill.icon = SvrIcons.Get("passive", null);

            MinigunSkillDef = MakeSkill("PRIMARY", typeof(SvrMinigunState), "Weapon", 0f, false, true, InterruptPriority.Any, "primary", false);
            SkillDef blackHole = MakeSkill("BLACKHOLE", typeof(SvrBlackHoleState), "Weapon", BlackHoleCooldown.Value, true, true, InterruptPriority.Skill, "blackhole", false);
            SkillDef leap = MakeSkill("MOONLEAP", typeof(SvrMoonLeapState), "Body", LeapCooldown.Value, true, true, InterruptPriority.PrioritySkill, "moonleap", false);
            leap.cancelSprintingOnActivation = false;
            SkillDef ammo = MakeSkill("AMMOSWAP", typeof(SvrAmmoSwapState), "Weapon", 0.4f, true, false, InterruptPriority.Any, "ammo", false);

            sl.primary = Slot("SurveyorPrimary", MinigunSkillDef);
            sl.secondary = Slot("SurveyorSecondary", blackHole);
            sl.utility = Slot("SurveyorUtility", leap);
            sl.special = Slot("SurveyorSpecial", ammo);
        }

        private static GenericSkill Slot(string family, params SkillDef[] defs)
        {
            GenericSkill slot = BodyPrefab.AddComponent<GenericSkill>();
            SkillFamily fam = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)fam).name = family;
            fam.variants = new SkillFamily.Variant[defs.Length];
            for (int i = 0; i < defs.Length; i++)
                fam.variants[i] = new SkillFamily.Variant { skillDef = defs[i], unlockableDef = null, viewableNode = new ViewablesCatalog.Node(defs[i].skillNameToken, false, null) };
            ContentAddition.AddSkillFamily(fam);
            FieldInfo f = typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(slot, fam);
            return slot;
        }

        private void RegisterSurvivor()
        {
            SurvivorDef s = ScriptableObject.CreateInstance<SurvivorDef>();
            s.cachedName = "Surveyor";
            s.bodyPrefab = BodyPrefab;
            s.displayPrefab = DisplayPrefab;
            s.displayNameToken = T + "NAME";
            s.descriptionToken = T + "DESCRIPTION";
            s.outroFlavorToken = T + "OUTRO";
            s.mainEndingEscapeFailureFlavorToken = T + "FAIL";
            s.desiredSortPosition = 105f;
            s.primaryColor = new Color(0.55f, 0.9f, 0.25f);
            ContentAddition.AddSurvivorDef(s);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) Config.Reload();
        }
    }

    // ================================================================ icons

    public static class SvrIcons
    {
        public static string Folder;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        public static Texture2D Texture(string name)
        {
            if (Textures.TryGetValue(name, out Texture2D tex)) return tex;
            tex = null;
            try
            {
                string path = Folder != null ? System.IO.Path.Combine(Folder, name + ".png") : null;
                if (path != null && System.IO.File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path))) { tex.name = "Surveyor_" + name; tex.wrapMode = TextureWrapMode.Clamp; }
                    else tex = null;
                }
            }
            catch (Exception e) { SvrPlugin.Log?.LogWarning("Icon " + name + ": " + e.Message); tex = null; }
            Textures[name] = tex;
            return tex;
        }

        public static Sprite Get(string name, Sprite fallback)
        {
            if (Sprites.TryGetValue(name, out Sprite s) && s) return s;
            Texture2D tex = Texture(name);
            if (!tex) return fallback;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "Surveyor_" + name;
            Sprites[name] = s;
            return s;
        }
    }

    // ================================================================ look: tint the Commando model dark steel with a green-yellow glow

    public class SvrLook : MonoBehaviour
    {
        private static readonly Dictionary<Material, Material> Tinted = new Dictionary<Material, Material>();
        private static readonly HashSet<Material> Mine = new HashSet<Material>();
        private CharacterModel _cm;
        private float _next;

        private void Start() { _cm = GetComponent<CharacterModel>(); }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            if (!_cm || _cm.baseRendererInfos == null) return;
            for (int i = 0; i < _cm.baseRendererInfos.Length; i++)
            {
                CharacterModel.RendererInfo ri = _cm.baseRendererInfos[i];
                if (!ri.renderer) continue;
                Material src = ri.defaultMaterial;
                if (!src || Mine.Contains(src)) continue;
                if (!Tinted.TryGetValue(src, out Material t) || !t)
                {
                    t = new Material(src);
                    if (t.HasProperty("_Color")) t.SetColor("_Color", new Color(0.46f, 0.52f, 0.42f));
                    if (t.HasProperty("_EmColor")) t.SetColor("_EmColor", new Color(0.62f, 0.9f, 0.22f));
                    if (t.HasProperty("_EmPower")) t.SetFloat("_EmPower", 0.3f);
                    Tinted[src] = t;
                    Mine.Add(t);
                }
                ri.defaultMaterial = t;
                _cm.baseRendererInfos[i] = ri;
                ri.renderer.sharedMaterial = t;
            }
        }
    }
}
