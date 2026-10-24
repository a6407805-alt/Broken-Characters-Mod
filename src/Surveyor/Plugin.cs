using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EntityStates;
using R2API;
using R2API.ContentManagement;
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
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class SvrPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.surveyor";
        public const string PluginName = "The Surveyor";
        public const string PluginVersion = "0.4.1";
        public const string T = "BC_SURVEYOR_";

        public static ManualLogSource Log;
        public static SvrPlugin Instance;
        public static GameObject BodyPrefab, DisplayPrefab;

        // ---------------------------------------------------------------- config
        public static ConfigEntry<float> SpoolSeconds, MinRate, MaxRate, BulletDamage, SpreadMin, SpreadMax, MinigunRange, MoveSlow;
        public static ConfigEntry<float> LanceDamage, LanceCooldown, LanceCharge, LanceForce;
        public static ConfigEntry<float> SlideCooldown, SlideSeconds, SlideSpeed, AnchorSeconds, AnchorDamageBonus;
        public static ConfigEntry<float> PulseDamage, PulseRadius, PulseCooldown, PulseForce;
        public static ConfigEntry<bool> DarkWorld;
        public static ConfigEntry<int> MaxDots, RaysPerFrame, DotDivider;
        public static ConfigEntry<float> ScanRange, DotLife;
        public static ConfigEntry<float> PeekSeconds, PeekCooldown, OverdriveCooldown, SweepSeconds, FocusSeconds, EchoSeconds;
        public static ConfigEntry<string> ScanNextKey, ScanPrevKey, PeekKey;
        public static ConfigEntry<bool> UseCustomModel;
        public static ConfigEntry<float> ModelScale, HeavyStepShake;
        public static ConfigEntry<bool> HeavyMotion;
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
            CreateSkills();
            RegisterSurvivor();
            RecalculateStatsAPI.GetStatCoefficients += Stats;
            SceneManager.activeSceneChanged += OnSceneChanged;

            Logger.LogInfo(PluginName + " " + PluginVersion + " online.");
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
        }

        private static void OnSceneChanged(Scene a, Scene b)
        {
            SvrSenses.ResetStage();
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

            PulseDamage = Config.Bind("4. Acoustic Shockwave", "Damage", 5f, "Shockwave damage (5 = 500%).");
            PulseRadius = Config.Bind("4. Acoustic Shockwave", "Radius", 30f, "Radius in metres.");
            PulseCooldown = Config.Bind("4. Acoustic Shockwave", "Cooldown", 20f, "Seconds.");
            PulseForce = Config.Bind("4. Acoustic Shockwave", "Force", 2500f, "Knockback on enemies.");

            DarkWorld = Config.Bind("5. LiDAR", "DarkWorld", true, "The world is black and you only see the LiDAR dots. Turn off to see the normal game with the dots on top (handy for testing).");
            MaxDots = Config.Bind("5. LiDAR", "MaxDots_v2", 40000, "Most dots kept at once (the point cloud is packed; lower this on a slow PC).");
            RaysPerFrame = Config.Bind("5. LiDAR", "ScanRaysPerFrame_v2", 170, "Rays cast per frame by the always-on scanner (they spiral over a wide cone so the cloud fills in evenly).");
            ScanRange = Config.Bind("5. LiDAR", "ScanRange", 75f, "Scanner range in metres.");
            DotLife = Config.Bind("5. LiDAR", "DotSeconds_v2", 9f, "How long a dot stays before it fades.");
            DotDivider = Config.Bind("5. LiDAR", "DotResolutionDivider_v1", 2, "The cloud is drawn at screen size divided by this (2 = every dot is a 2x2 block at 1080p; 1 = finest, slowest; 3 = chunkier).");

            PeekSeconds = Config.Bind("8. Peek and Overdrive", "PeekSeconds_v1", 4.5f, "How long the visor stays off.");
            PeekCooldown = Config.Bind("8. Peek and Overdrive", "PeekCooldown_v1", 16f, "Seconds. Starts when you use it.");
            OverdriveCooldown = Config.Bind("8. Peek and Overdrive", "OverdriveCooldown_v1", 14f, "Seconds.");
            SweepSeconds = Config.Bind("8. Peek and Overdrive", "SweepSeconds_v1", 3.2f, "SWEEP mode: seconds for the scan line to spin 360 degrees.");
            FocusSeconds = Config.Bind("8. Peek and Overdrive", "FocusSeconds_v1", 4f, "FOCUS mode: seconds of the long dense beam.");
            EchoSeconds = Config.Bind("8. Peek and Overdrive", "EchoSeconds_v1", 8f, "ECHO mode: seconds enemies (and the Shimmer Demon) are lit through walls.");
            UseCustomModel = Config.Bind("9. Model", "UseCustomModel_v1", true, "Use the Surveyor's own 3D model (surveyor_model.bin.gz next to the dll). Off = Commando with a green tint.");
            ModelScale = Config.Bind("9. Model", "ModelScale_v1", 1.08f, "Size of the armoured suit on the Commando skeleton (1 = fitted to the skeleton).");
            HeavyMotion = Config.Bind("9. Model", "HeavyMotion_v1", true, "Heavy-gunner body language: both hands on the gun, gun lag, lean, sway, recoil.");
            HeavyStepShake = Config.Bind("9. Model", "StepShake_v1", 1f, "Camera thud on each heavy step and landing (0 = off).");
            ScanNextKey = Config.Bind("8. Peek and Overdrive", "ScanNextKey_v1", "E", "Next scan mode: CONE, SWEEP, WIDE, FOCUS, ECHO. (E is also the game's interact key; change it here if that clashes.)");
            ScanPrevKey = Config.Bind("8. Peek and Overdrive", "ScanPrevKey_v1", "Q", "Previous scan mode. (Q is also the game's equipment key.)");
            PeekKey = Config.Bind("8. Peek and Overdrive", "PeekKey_v1", "F", "Peek (visor off) on its own key, whatever skill is in the utility slot.");

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
            if (sp.Spool > 0.3f && sp.AnchorLeft <= 0f) slow += MoveSlow.Value;
            if (SvrSenses.Touch) slow += 0.05f;
            if (SvrSenses.Balance) slow += BalanceSlow.Value;
            if (slow > 0f) a.moveSpeedMultAdd -= slow;
        }

        // ---------------------------------------------------------------- language

        private void RegisterLanguage()
        {
            LanguageAPI.Add(T + "NAME", "The Surveyor");
            LanguageAPI.Add(T + "SUBTITLE", "Blind Engineer");
            LanguageAPI.Add(T + "DESCRIPTION",
                "The Surveyor is a blind, heavily armored engineer stranded on Petrichor V. The world is black. She sees only what her scanner paints.<color=#CCD3E0>\r\n\r\n" +
                "< ! > Your LiDAR draws the world in dots: red is close, yellow is mid, green is far. Shooting and shockwaves paint it fast. Press Q / E to switch how the scanner sweeps (Cone, Sweep, Wide, Focus, Echo). Press F to Peek: the visor lifts for a blurry, blown-out look at the real world.\r\n\r\n" +
                "< ! > The minigun needs to spool. Hold the trigger and it rises to full fire rate, but you move slower while it is spun up. Anchor to spool faster.\r\n\r\n" +
                "< ! > Something shimmering follows you. Only you can see it, and you cannot hurt it, but a fully spooled minigun, the Lance and the Shockwave push it back.\r\n\r\n" +
                "< ! > If it touches you it eats a sense: Hearing, Sight, Touch or Balance, until the stage ends.</color>");
            LanguageAPI.Add(T + "OUTRO", "..and so she left, still hearing the scanner ping.");
            LanguageAPI.Add(T + "FAIL", "..and so she vanished, and the dots went dark.");
            LanguageAPI.Add(T + "SKIN_DEFAULT", "Survey Plate");

            LanguageAPI.Add(T + "PASSIVE_NAME", "LiDAR Vision");
            LanguageAPI.Add(T + "PASSIVE_DESC",
                "The screen is dark. An always-on scanner paints the world as a <style=cIsUtility>decaying point cloud</style> (red near, yellow mid, green far). Bullets, the Lance and the Shockwave leave bright dots. " +
                "<style=cDeath>A Shimmer Demon stalks you</style> and only you can see it: close in, your suit's HUD and scanner start to fail, and if it touches you it steals a sense for the stage.");
            LanguageAPI.Add(T + "PRIMARY_NAME", "Rotary Survey Gun");
            LanguageAPI.Add(T + "PRIMARY_DESC", "Hold to spool the barrels up over <style=cIsUtility>" + SpoolSeconds.Value.ToString("0.0#") + "s</style>, then fire up to <style=cIsDamage>" + MaxRate.Value.ToString("0") + " shots per second</style> for <style=cIsDamage>" +
                (BulletDamage.Value * 100f).ToString("0") + "% damage</style> each. <style=cIsUtility>Slows you by " + (MoveSlow.Value * 100f).ToString("0") + "%</style> while spooled. A fully spooled burst can shove the Shimmer Demon back.");
            LanguageAPI.Add(T + "SECONDARY_NAME", "Surveyor Lance");
            LanguageAPI.Add(T + "SECONDARY_DESC", "Charge for " + LanceCharge.Value.ToString("0.0#") + "s, then fire a <style=cIsDamage>piercing lance for " + (LanceDamage.Value * 100f).ToString("0") + "% damage</style> that knocks enemies back and paints a line of LiDAR. <style=cIsUtility>Staggers the Shimmer Demon for " +
                LanceStagger.Value.ToString("0.#") + "s.</style> Recoil pushes you back unless you are anchored.");
            LanguageAPI.Add(T + "UTILITY_NAME", "Hydraulic Slide");
            LanguageAPI.Add(T + "UTILITY_DESC", "<style=cIsUtility>Slide forward.</style> Keep <style=cIsUtility>holding the button</style> and she plants an <style=cIsDamage>Anchor</style> for up to " + AnchorSeconds.Value.ToString("0") +
                "s: rooted, <style=cIsUtility>spools 3x faster</style>, no move penalty, <style=cIsDamage>+" + (AnchorDamageBonus.Value * 100f).ToString("0") + "% minigun damage</style> and a steadier aim.");
            LanguageAPI.Add(T + "SPECIAL_NAME", "Acoustic Shockwave");
            LanguageAPI.Add(T + "SPECIAL_DESC", "Slam a pulse out to <style=cIsUtility>" + PulseRadius.Value.ToString("0") + "m</style> for <style=cIsDamage>" + (PulseDamage.Value * 100f).ToString("0") +
                "% damage</style>. It <style=cIsUtility>lights up all the geometry around you</style>, tags enemies on your scanner for 5s and <style=cIsUtility>throws the Shimmer Demon away</style>.");
            LanguageAPI.Add(T + "PEEK_NAME", "Peek");
            LanguageAPI.Add(T + "PEEK_DESC", "Rip off the visor for <style=cIsUtility>" + PeekSeconds.Value.ToString("0.#") + "s</style>. The real world comes back and you see <style=cIsUtility>far beyond the scanner</style>, but it is <style=cDeath>blurry, blown out and jarring</style>, and the LiDAR fades. Replaces Hydraulic Slide.");
            LanguageAPI.Add(T + "OVERDRIVE_NAME", "LiDAR Overdrive");
            LanguageAPI.Add(T + "OVERDRIVE_DESC", "Push the scanner past its limits. Pick a scan mode with <style=cIsUtility>" + ScanPrevKey.Value.ToUpperInvariant() + "/" + ScanNextKey.Value.ToUpperInvariant() + "</style>, then use the skill to overdrive it: " +
                "<style=cIsUtility>SWEEP</style> spins a tall scan line around you, <style=cIsUtility>BURST</style> floods the whole sphere at once, <style=cIsUtility>FOCUS</style> fires a very dense long beam, " +
                "<style=cIsDamage>ECHO</style> lights up every enemy (and the Shimmer Demon) through walls. Replaces Acoustic Shockwave.");
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
            body.baseMoveSpeed = 6.8f;
            body.baseAcceleration = 48f;           // heavy: slower to get going and to stop (Commando 80)
            body.baseJumpPower = 13.5f;
            Texture portrait = SvrIcons.Texture("portrait");
            if (portrait) body.portraitIcon = portrait;

            BodyPrefab.AddComponent<SvrSpool>();
            BodyPrefab.AddComponent<SvrLidar>();
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
                SkinDef skin = Object.Instantiate(baseSkin);
                ((ScriptableObject)skin).name = "skinBCSurveyorDefault";
                SetMember(skin, "nameToken", T + "SKIN_DEFAULT");
                Sprite icon = SvrIcons.Get("skin", null);
                if (icon) SetMember(skin, "icon", icon);
                sf.SetValue(old, new SkinDef[] { skin });
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
            ContentAddition.AddEntityState<SvrLanceState>(out _);
            ContentAddition.AddEntityState<SvrSlideState>(out _);
            ContentAddition.AddEntityState<SvrAnchorState>(out _);
            ContentAddition.AddEntityState<SvrPulseState>(out _);
            ContentAddition.AddEntityState<SvrPeekState>(out _);
            ContentAddition.AddEntityState<SvrOverdriveState>(out _);

            SkillLocator sl = BodyPrefab.GetComponent<SkillLocator>();
            foreach (GenericSkill old in BodyPrefab.GetComponentsInChildren<GenericSkill>(true)) Object.DestroyImmediate(old);
            sl.passiveSkill.enabled = true;
            sl.passiveSkill.skillNameToken = T + "PASSIVE_NAME";
            sl.passiveSkill.skillDescriptionToken = T + "PASSIVE_DESC";
            sl.passiveSkill.icon = SvrIcons.Get("passive", null);

            SkillDef primary = MakeSkill("PRIMARY", typeof(SvrMinigunState), "Weapon", 0f, false, true, InterruptPriority.Any, "primary", false);
            SkillDef secondary = MakeSkill("SECONDARY", typeof(SvrLanceState), "Weapon", LanceCooldown.Value, true, true, InterruptPriority.Skill, "secondary", true);
            SkillDef utility = MakeSkill("UTILITY", typeof(SvrSlideState), "Body", SlideCooldown.Value, true, false, InterruptPriority.PrioritySkill, "utility", true);
            SkillDef special = MakeSkill("SPECIAL", typeof(SvrPulseState), "Weapon", PulseCooldown.Value, true, true, InterruptPriority.Skill, "special", true);
            utility.cancelSprintingOnActivation = false;
            SkillDef peek = MakeSkill("PEEK", typeof(SvrPeekState), "Body", PeekCooldown.Value, true, false, InterruptPriority.PrioritySkill, "peek", false);
            peek.cancelSprintingOnActivation = false;
            SkillDef overdrive = MakeSkill("OVERDRIVE", typeof(SvrOverdriveState), "Weapon", OverdriveCooldown.Value, true, false, InterruptPriority.Skill, "overdrive", false);

            sl.primary = Slot("SurveyorPrimary", primary);
            sl.secondary = Slot("SurveyorSecondary", secondary);
            sl.utility = Slot("SurveyorUtility", utility, peek);
            sl.special = Slot("SurveyorSpecial", special, overdrive);
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
