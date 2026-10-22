using System;
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
using Object = UnityEngine.Object;

namespace CarMaxSpecialist
{
    /// <summary>
    /// The CarMax Auto Repair Specialist: an hourly, overworked associate. He's paid by the hour no matter what,
    /// gets yelled at by customers, has to order his own parts, has lazy coworkers and impossible deadlines.
    /// </summary>
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInDependency(AudiGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class CmxPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.carmaxspecialist";
        public const string PluginName = "CarMax Auto Repair Specialist";
        public const string PluginVersion = "1.0.0";
        public const string T = "BC_CARMAX_";
        public const string AudiGuid = "com.VincentMacaluso.AudiTechnician";
        public static bool AudiModel;

        public static ManualLogSource Log;
        public static CmxPlugin Instance;
        public static GameObject BodyPrefab, DisplayPrefab;
        public static BuffDef StressBuff, OvertimeBuff, PizzaBuff, WriteUpBuff, DiyBuff, InventoryBuff;

        // ---------------------------------------------------------------- config
        public static ConfigEntry<float> HourSeconds, PayPerHour, OvertimeAfter, OvertimePay;
        public static ConfigEntry<float> StressPerHit, StressDecay, StressDamage, BurnoutSeconds;
        public static ConfigEntry<bool> Tasks, CombatDeadlines;
        public static ConfigEntry<int> MaxTasks, LowCap, MedCap, HighCap, HighBurst, AuditBacklog;
        public static ConfigEntry<bool> AuditLockdown;
        public static ConfigEntry<float> MediumFrom, HighFrom, LowEvery, MedEveryMin, MedEveryMax, HighEveryMin, HighEveryMax, AuditEvery;
        public static ConfigEntry<float> EroRelief, ApptRelief, EroChance, ApptChance, InvChance, MatchChance, SliderSensitivity, SliderTolerance;
        public static ConfigEntry<float> TaskEveryMin, TaskEveryMax, TaskRelief, TaskNag;
        public static ConfigEntry<float> DeadlineMin, DeadlineMax, DeadlineStressRelief, DeadlineFailStress;
        public static ConfigEntry<KeyCode> MouseKey;
        public static ConfigEntry<float> LayoffRange, LayoffDamage, HeavyDamage, EmailDamage, AuditDamage, AuditRange, AuditSeconds, PalletDamage, PalletRadius, BackorderChance;
        public static ConfigEntry<float> CoworkerSeconds, LazyChance, DiyStress;
        public static ConfigEntry<float> SecondaryCooldown, UtilityCooldown, SpecialCooldown;
        public static ConfigEntry<int> Volume;
        public static ConfigEntry<bool> Bubbles, Props, UseAudiModel, Tts, CompletionReward;
        public static ConfigEntry<int> TtsRate;
        public static ConfigEntry<float> TtsVolume, TtsMin, TtsMax, TtsDoppler;
        public static ConfigEntry<float> HudX, HudY;

        private string Folder(string sub) => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), sub);

        private void Awake()
        {
            Log = Logger;
            Instance = this;
            SetupConfig();
            CmxAudio.Load(Folder("Sounds"), Volume.Value);
            Application.quitting += CmxAudio.Unload;
            CmxIcons.Folder = Folder("Icons");
            gameObject.AddComponent<CmxHud>();
            gameObject.AddComponent<CmxTasks>();

            RegisterLanguage();
            CreateBuffs();
            CreateBody();
            CreateSkills();
            RegisterSurvivor();
            CmxNet.Register();
            CmxShift.Hook();
            RecalculateStatsAPI.GetStatCoefficients += Stats;

            Logger.LogInfo(PluginName + " " + PluginVersion + " clocked in.");
        }

        private void SetupConfig()
        {
            HourSeconds = Config.Bind("1. Hourly", "SecondsPerHour", 20f, "Real seconds in one paid 'hour'.");
            PayPerHour = Config.Bind("1. Hourly", "PayPerHour", 6f, "Gold per hour (scaled by the run's difficulty, like chests).");
            OvertimeAfter = Config.Bind("1. Hourly", "OvertimeAfterHours", 8f, "Hours in a stage before overtime starts.");
            OvertimePay = Config.Bind("1. Hourly", "OvertimePayMultiplier", 1.5f, "Time and a half.");
            StressPerHit = Config.Bind("2. Stress", "StressPerHit_v3", 0.6f, "Stress from getting yelled at, multiplier (bigger hits = more stress).");
            StressDecay = Config.Bind("2. Stress", "StressDecayPerSecond", 1.5f, "Stress lost per second after 6 s without being hit (not during overtime).");
            StressDamage = Config.Bind("2. Stress", "DamagePerStress", 0.005f, "Extra damage per point of stress (0.005 = +50% at 100).");
            BurnoutSeconds = Config.Bind("2. Stress", "BurnoutSeconds", 3f, "How long a Burnout freezes him.");
            Tasks = Config.Bind("3. Tasks", "TaskPopups", true, "Task windows pop up on your screen (part numbers, ROs, stories, invoices) until you finish them.");
            MaxTasks = Config.Bind("3. Tasks", "MaxOpenTasks", 3, "Most task windows open at once.");
            LowCap = Config.Bind("3. Tasks", "Calm_MaxOpen_v3", 0, "Below the Busy stress: no pop-ups at all (0).");
            LowEvery = Config.Bind("3. Tasks", "Calm_SecondsBetween_v2", 24f, "Stress 0-40: average gap between windows.");
            MediumFrom = Config.Bind("3. Tasks", "Busy_FromStress_v3", 50f, "Stress where pop-ups start (below this: none).");
            MedCap = Config.Bind("3. Tasks", "Busy_MaxOpen_v3", 1, "Busy: most windows open at once.");
            MedEveryMin = Config.Bind("3. Tasks", "Busy_SecondsBetweenMin_v3", 24f, "Busy: shortest gap.");
            MedEveryMax = Config.Bind("3. Tasks", "Busy_SecondsBetweenMax_v3", 32f, "Busy: longest gap.");
            HighFrom = Config.Bind("3. Tasks", "Overload_FromStress_v3", 81f, "Stress where windows come faster.");
            HighCap = Config.Bind("3. Tasks", "Overload_MaxOpen_v3", 2, "Overload: most windows open at once.");
            HighBurst = Config.Bind("3. Tasks", "Overload_BurstSize_v3", 1, "Overload: windows per burst.");
            HighEveryMin = Config.Bind("3. Tasks", "Overload_SecondsBetweenMin_v3", 12f, "Overload: shortest gap.");
            HighEveryMax = Config.Bind("3. Tasks", "Overload_SecondsBetweenMax_v3", 18f, "Overload: longest gap.");
            AuditLockdown = Config.Bind("3. Tasks", "ManagementAudit_v2", true, "At 100 stress a Management Audit locks the screen and keeps the windows coming until the backlog is cleared (instead of Burnout).");
            AuditBacklog = Config.Bind("3. Tasks", "AuditBacklog_v3", 3, "Windows to clear to pass the audit.");
            AuditEvery = Config.Bind("3. Tasks", "AuditSecondsBetween_v3", 1.5f, "Audit: gap between windows.");
            InvChance = Config.Bind("3. Tasks", "InventoryAuditChance_v2", 0.15f, "Chance a new window is a parts inventory audit (match the shelf count).");
            MatchChance = Config.Bind("3. Tasks", "PartMatchChance_v2", 0.15f, "Chance a new window is a part-number lookup (fit the right part to the car).");
            EroChance = Config.Bind("3. Tasks", "EroChance_v2", 0.2f, "Chance a new window is an ERO repair order.");
            ApptChance = Config.Bind("3. Tasks", "AppointmentChance_v2", 0.15f, "Chance a new window is the appointment scheduler.");
            EroRelief = Config.Bind("3. Tasks", "EroStressRelief_v3", 45f, "Stress removed by finishing an ERO.");
            ApptRelief = Config.Bind("3. Tasks", "AppointmentStressRelief_v3", 45f, "Stress removed by finishing an appointment.");
            SliderSensitivity = Config.Bind("3. Tasks", "PromiseTimeSensitivity_v2", 3f, "How twitchy the promise-time slider is.");
            SliderTolerance = Config.Bind("3. Tasks", "PromiseTimeToleranceMinutes_v2", 10f, "How close the promise time must be (minutes).");
            TaskEveryMin = Config.Bind("3. Tasks", "SecondsBetweenAtFullStress", 12f, "Gap between new task windows at 100 stress.");
            TaskEveryMax = Config.Bind("3. Tasks", "SecondsBetweenAtNoStress", 30f, "Gap between new task windows at 0 stress.");
            TaskRelief = Config.Bind("3. Tasks", "StressReliefPerTask_v3", 45f, "Stress removed per finished task.");
            CompletionReward = Config.Bind("3. Tasks", "CompletionReward_v3", true, "Finishing any window gives 3s of Energized and a small barrier.");
            TaskNag = Config.Bind("3. Tasks", "StressPerOpenTaskPerSecond_v3", 0f, "Stress each open task adds per second (0 = no snowball).");
            CombatDeadlines = Config.Bind("3. Tasks", "CombatDeadlines", false, "Also give timed combat deadlines (kill N, deal damage, ...) from the first version.");
            DeadlineMin = Config.Bind("3. Deadlines", "SecondsBetweenMin", 40f, "Shortest gap between deadlines.");
            DeadlineMax = Config.Bind("3. Deadlines", "SecondsBetweenMax", 55f, "Longest gap between deadlines.");
            DeadlineStressRelief = Config.Bind("3. Deadlines", "StressRelief", 35f, "Stress removed when a deadline is met.");
            DeadlineFailStress = Config.Bind("3. Deadlines", "FailStress", 25f, "Stress added when a deadline is missed.");
            EmailDamage = Config.Bind("4. Skills", "EmailDamage", 1.1f, "M1 light combo base damage (1.1 = 110%); the 4 hits are x0.6, x0.8, x1.2, x2.4 of this.");
            HeavyDamage = Config.Bind("4. Skills", "HeavyDamage_v2", 1.6f, "M2 heavy combo base damage (1.6 = 160%); the 4 hits are x1, x1.3, x1.8, x2.5, and the building adds x3.");
            MouseKey = Config.Bind("3. Tasks", "MouseToggleKey_v2", KeyCode.F7, "Press to free the mouse so you can click the task windows (press again to go back to aiming).");
            AuditDamage = Config.Bind("4. Skills", "AuditDamage", 1.5f, "Inventory Audit damage to each scanned enemy.");
            AuditRange = Config.Bind("4. Skills", "AuditRange", 40f, "Inventory Audit range.");
            AuditSeconds = Config.Bind("4. Skills", "InventoriedSeconds", 10f, "How long an enemy stays tagged as inventory.");
            PalletDamage = Config.Bind("4. Skills", "PalletDamage", 5f, "Parts pallet damage (doubled when backordered).");
            PalletRadius = Config.Bind("4. Skills", "PalletRadius", 9f, "Parts pallet blast radius (x1.4 when backordered).");
            BackorderChance = Config.Bind("4. Skills", "BackorderChance", 0.3f, "Chance the parts are backordered (late, but twice as big).");
            LayoffRange = Config.Bind("4. Skills", "LayoffRange_v2", 45f, "R: enemies within this range are let go (killed) when the coworkers arrive.");
            LayoffDamage = Config.Bind("4. Skills", "LayoffBossDamage_v2", 15f, "R: bosses can't be laid off, they take this much damage instead (15 = 1500%).");
            CoworkerSeconds = Config.Bind("4. Skills", "CoworkerSeconds", 15f, "How long coworkers stay on the clock.");
            LazyChance = Config.Bind("4. Skills", "LazyChance", 0.4f, "Chance each coworker is 'on break'.");
            DiyStress = Config.Bind("4. Skills", "DoItYourselfStress", 15f, "Stress from doing it yourself.");
            SecondaryCooldown = Config.Bind("4. Skills", "AuditCooldown", 6f, "");
            UtilityCooldown = Config.Bind("4. Skills", "PartsCooldown", 7f, "");
            SpecialCooldown = Config.Bind("4. Skills", "CoworkersCooldown", 20f, "");
            Volume = Config.Bind("5. Look and sound", "Volume", 70, "Sound volume 0-100 (his own sounds, Windows only).");
            Bubbles = Config.Bind("5. Look and sound", "SpeechBubbles", true, "Corporate speech bubbles over heads.");
            UseAudiModel = Config.Bind("5. Look and sound", "UseAudiTechnicianModel", true, "Wear the Audi Technician's model, recolored CarMax blue (needs the AudiTechnician mod).");
            Tts = Config.Bind("5. Look and sound", "Enable3DTTS_v3", true, "Spoken lines and pop-up prompts in 3D, heard by everyone nearby (Windows voice, Windows only).");
            TtsVolume = Config.Bind("5. Look and sound", "TTSVolume_v3", 1f, "Master volume for the voice (0 to 1).");
            TtsRate = Config.Bind("5. Look and sound", "TTSRate_v3", 2, "Speech speed (-10 to 10).");
            TtsMin = Config.Bind("5. Look and sound", "TTSMinDistance_v3", 4f, "Metres where the voice is still at full volume.");
            TtsMax = Config.Bind("5. Look and sound", "TTSMaxDistance_v3", 55f, "Metres where the voice fades out completely.");
            TtsDoppler = Config.Bind("5. Look and sound", "TTSDopplerLevel_v3", 2.5f, "Pitch bend when someone talks while flying past fast (0 = off, 5 = extreme).");
            Props = Config.Bind("5. Look and sound", "OfficeProps_v2", false, "Tie, lanyard and clipboard on top of the model.");
            HudX = Config.Bind("5. Look and sound", "TimecardX", 0.012f, "Timecard panel position (fraction of the screen).");
            HudY = Config.Bind("5. Look and sound", "TimecardY", 0.26f, "Timecard panel position (fraction of the screen).");
        }

        private static void Stats(CharacterBody b, RecalculateStatsAPI.StatHookEventArgs a)
        {
            if (!b) return;
            int stress = b.GetBuffCount(StressBuff);
            if (stress > 0) a.damageMultAdd += stress * StressDamage.Value;
            if (b.HasBuff(OvertimeBuff)) a.attackSpeedMultAdd += 0.15f;
            if (b.HasBuff(PizzaBuff)) { a.attackSpeedMultAdd += 0.4f; a.baseRegenAdd += 4f; }
            if (b.HasBuff(WriteUpBuff)) a.damageMultAdd -= 0.25f;
            if (b.HasBuff(DiyBuff)) { a.damageMultAdd += 0.5f; a.attackSpeedMultAdd += 0.3f; }
            if (b.HasBuff(InventoryBuff)) a.armorAdd -= 30f;
        }

        // ---------------------------------------------------------------- language

        private void RegisterLanguage()
        {
            LanguageAPI.Add(T + "NAME", "CarMax Auto Repair Specialist");
            LanguageAPI.Add(T + "SUBTITLE", "Non-Exempt Associate");
            LanguageAPI.Add(T + "DESCRIPTION",
                "The Auto Repair Specialist is paid by the hour, not by the job, and there is always more job.<color=#CCD3E0>\r\n\r\n" +
                "< ! > Your paycheck comes every hour no matter what. After 8 hours it's overtime: more pay, more stress.\r\n\r\n" +
                "< ! > Stress makes you hit harder, but at 100% you burn out. Clear your task pop-ups (number keys, arrow keys) to calm down.\r\n\r\n" +
                "< ! > M1 and M2 are combos: keep hitting to chain bigger props. Press F7 to free the mouse and click your pop-ups.\r\n\r\n" +
                "< ! > Your coworkers might help. They might also be on break.</color>");
            LanguageAPI.Add(T + "OUTRO", "..and so he left, still on the clock.");
            LanguageAPI.Add(T + "FAIL", "..and so he vanished, and nobody covered his shift.");
            LanguageAPI.Add(T + "SKIN_DEFAULT", "Business Casual");

            LanguageAPI.Add(T + "PASSIVE_NAME", "Hourly (Non-Exempt)");
            LanguageAPI.Add(T + "PASSIVE_DESC",
                "Every hour (" + HourSeconds.Value.ToString("0") + "s) pays a <style=cShrine>paycheck</style>, no matter how you're doing. After " + OvertimeAfter.Value.ToString("0") +
                " hours: <style=cIsUtility>Overtime</style>, x" + OvertimePay.Value.ToString("0.#") + " pay and +15% attack speed, but stress keeps climbing. " +
                "<style=cIsDamage>Stress</style> builds when customers yell at you (taking damage), and gives <style=cIsDamage>+" + (StressDamage.Value * 100f).ToString("0.#") + "% damage per point</style>. " +
                "<style=cDeath>At 100: Burnout.</style> <style=cIsUtility>Busywork</style> pops up on your screen like ads (punch in part numbers, sign off ROs, write the story, total invoices). Open tasks block your view and add stress; finishing one pays a little and <style=cIsHealing>clears your head</style>.");
            LanguageAPI.Add(T + "PRIMARY_NAME", "Paper Trail");
            LanguageAPI.Add(T + "PRIMARY_DESC", "Hold to chain a <style=cIsDamage>4-hit combo</style> of office supplies: <style=cIsDamage>paper</style>, then <style=cIsDamage>staplers, phones and keyboards</style>, then the <style=cIsDamage>whole computer</style>, then a <style=cIsDamage>filing cabinet</style> slam. Base <style=cIsDamage>" + (EmailDamage.Value * 100f).ToString("0") + "% damage</style>, hits get heavier.");
            LanguageAPI.Add(T + "SECONDARY_NAME", "Heavy Equipment");
            LanguageAPI.Add(T + "SECONDARY_DESC", "Hold to chain a slower <style=cIsDamage>4-hit area combo</style> from the shop floor: <style=cIsDamage>lug nuts</style>, <style=cIsDamage>wrenches and tools</style>, a <style=cIsDamage>jack and jack stands</style>, then <style=cIsDamage>the car</style>, and the <style=cIsDamage>building</style> right behind it. Base <style=cIsDamage>" + (HeavyDamage.Value * 100f).ToString("0") + "% damage</style>.");
            LanguageAPI.Add(T + "UTILITY_NAME", "Order Your Own Parts");
            LanguageAPI.Add(T + "UTILITY_DESC", "<style=cIsUtility>Dash</style>. A parts pallet drops where you stood for <style=cIsDamage>" + (PalletDamage.Value * 100f).ToString("0") + "% damage</style>. " + (BackorderChance.Value * 100f).ToString("0") + "% chance it's <style=cIsUtility>backordered</style>: it lands 4 seconds late, but twice as hard.");
            LanguageAPI.Add(T + "SPECIAL_NAME", "Lazy Coworkers");
            LanguageAPI.Add(T + "SPECIAL_DESC", "<style=cIsUtility>Clears every pop-up off your screen</style>, <style=cIsDamage>lays off every enemy nearby</style> (bosses take " + (LayoffDamage.Value * 100f).ToString("0") + "% damage), and calls in 2 coworkers for " + CoworkerSeconds.Value.ToString("0") + "s. Each has a " + (LazyChance.Value * 100f).ToString("0") + "% chance to be <style=cDeath>on break</style> (more stress). Press again while they're out to <style=cIsDamage>Do It Yourself</style>: send them home and gain <style=cIsDamage>+50% damage and +30% attack speed</style> for 8s, plus " + DiyStress.Value.ToString("0") + " stress.");

            LanguageAPI.Add(T + "BUFF_STRESS", "Stress");
        }

        // ---------------------------------------------------------------- buffs

        private static BuffDef Buff(string name, string icon, Color color, bool stack, bool debuff)
        {
            BuffDef d = ScriptableObject.CreateInstance<BuffDef>();
            ((ScriptableObject)d).name = "bdCarMax" + name;
            d.buffColor = color;
            d.canStack = stack;
            d.isDebuff = debuff;
            d.iconSprite = CmxIcons.Get(icon, null);
            ContentAddition.AddBuffDef(d);
            return d;
        }

        private void CreateBuffs()
        {
            StressBuff = Buff("Stress", "buff_stress", new Color(1f, 0.35f, 0.25f), true, false);
            OvertimeBuff = Buff("Overtime", "buff_overtime", new Color(1f, 0.8f, 0.15f), false, false);
            PizzaBuff = Buff("PizzaParty", "buff_pizza", new Color(1f, 0.6f, 0.2f), false, false);
            WriteUpBuff = Buff("WrittenUp", "buff_writeup", new Color(0.85f, 0.2f, 0.2f), false, true);
            DiyBuff = Buff("DoItYourself", "buff_diy", new Color(0.3f, 0.6f, 1f), false, false);
            InventoryBuff = Buff("Inventoried", "buff_inventory", new Color(1f, 0.82f, 0.15f), false, true);
        }

        // ---------------------------------------------------------------- body

        /// <summary>
        /// The Audi Technician's body (with his custom model) if that mod is installed, so the Specialist wears the
        /// same model in CarMax blue. Falls back to Commando.
        /// </summary>
        private static GameObject FindAudi(string name)
        {
            if (!UseAudiModel.Value) return null;
            try
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!go || go.name != name) continue;
                    if (name.EndsWith("Body") && !go.GetComponent<CharacterBody>()) continue;
                    if (go.scene.IsValid() && go.scene.isLoaded && go.activeInHierarchy) continue;
                    return go;
                }
            }
            catch (Exception e) { Log.LogWarning("Audi model lookup failed: " + e.Message); }
            return null;
        }

        // The Audi Technician's own scripts (his Flat Rate clock etc.) are removed from the copy.
        private static void StripAudiScripts(GameObject root)
        {
            int n = 0;
            for (int pass = 0; pass < 2; pass++)
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!mb) continue;
                    string ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("AudiTechnician")) { Object.DestroyImmediate(mb); n++; }
                }
            if (n > 0) Log.LogInfo("Removed " + n + " Audi Technician scripts from the Specialist's copy.");
        }

        private void CreateBody()
        {
            GameObject audi = FindAudi("AudiTechBody");
            GameObject source = audi ? audi : LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            AudiModel = audi;
            BodyPrefab = PrefabAPI.InstantiateClone(source, "CarMaxSpecialistBody", true);
            if (audi) StripAudiScripts(BodyPrefab);
            Log.LogInfo(audi ? "Using the Audi Technician's model (in CarMax blue)." : "Audi Technician not found; using Commando's model.");

            CharacterBody body = BodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = T + "NAME";
            body.subtitleNameToken = T + "SUBTITLE";
            body.bodyColor = new Color(0.2f, 0.45f, 0.85f);
            body.baseMaxHealth = 120f;
            body.levelMaxHealth = 36f;
            body.baseRegen = 1.5f;
            body.levelRegen = 0.3f;
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseArmor = 5f;
            Texture portrait = CmxIcons.Texture("portrait");
            if (portrait) body.portraitIcon = portrait;

            BodyPrefab.AddComponent<CmxShift>();
            Transform model = Model(BodyPrefab);
            if (model) { model.gameObject.AddComponent<CmxLook>(); AddSkin(model); }
            ContentAddition.AddBody(BodyPrefab);

            GameObject audiDisplay = audi ? FindAudi("AudiTechDisplay") : null;
            GameObject display = audiDisplay ? audiDisplay : LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            DisplayPrefab = PrefabAPI.InstantiateClone(display, "CarMaxSpecialistDisplay", false);
            if (audiDisplay) StripAudiScripts(DisplayPrefab);
            Transform dm = Model(DisplayPrefab) ?? DisplayPrefab.transform;
            CharacterModel dcm = DisplayPrefab.GetComponentInChildren<CharacterModel>(true);
            if (dcm) dm = dcm.transform;
            if (dm) dm.gameObject.AddComponent<CmxLook>();
        }

        public static Transform Model(GameObject root)
        {
            ModelLocator ml = root ? root.GetComponent<ModelLocator>() : null;
            if (ml && ml.modelTransform) return ml.modelTransform;
            CharacterModel cm = root ? root.GetComponentInChildren<CharacterModel>(true) : null;
            return cm ? cm.transform : null;
        }

        // The loadout screen wants a skin entry; Commando's own skins would show Commando names, so he gets one
        // "Business Casual" entry that keeps the model's default materials (CmxLook does the recolor).
        private static void AddSkin(Transform model)
        {
            try
            {
                ModelSkinController old = model.GetComponent<ModelSkinController>();
                SkinDef baseSkin = null;
                if (old)
                {
                    FieldInfo f = typeof(ModelSkinController).GetField("skins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    SkinDef[] skins = f != null ? f.GetValue(old) as SkinDef[] : null;
                    if (skins != null && skins.Length > 0) baseSkin = skins[0];
                }
                if (!baseSkin) return;
                SkinDef skin = Object.Instantiate(baseSkin);
                ((ScriptableObject)skin).name = "skinCarMaxDefault";
                SetMember(skin, "nameToken", T + "SKIN_DEFAULT");
                Sprite icon = CmxIcons.Get("skin", null);
                if (icon) SetMember(skin, "icon", icon);
                FieldInfo sf = typeof(ModelSkinController).GetField("skins", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sf != null) sf.SetValue(old, new SkinDef[] { skin });
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

        private static T MakeSkill<T>(string key, Type state, string machine, float cooldown, bool mustKeyPress, bool combat, InterruptPriority prio, string icon) where T : SkillDef
        {
            T d = ScriptableObject.CreateInstance<T>();
            d.skillName = CmxPlugin.T + key;
            ((ScriptableObject)d).name = CmxPlugin.T + key;
            d.skillNameToken = CmxPlugin.T + key + "_NAME";
            d.skillDescriptionToken = CmxPlugin.T + key + "_DESC";
            d.icon = CmxIcons.Get(icon, null);
            d.activationState = new SerializableEntityStateType(state);
            d.activationStateMachineName = machine;
            d.baseMaxStock = 1;
            d.rechargeStock = 1;
            d.requiredStock = 1;
            d.stockToConsume = 1;
            d.baseRechargeInterval = cooldown;
            d.beginSkillCooldownOnSkillEnd = false;
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
            ContentAddition.AddEntityState<CmxLightState>(out _);
            ContentAddition.AddEntityState<CmxHeavyState>(out _);
            ContentAddition.AddEntityState<CmxPartsState>(out _);
            ContentAddition.AddEntityState<CmxCoworkersState>(out _);
            ContentAddition.AddEntityState<CmxBurnoutState>(out _);

            SkillLocator sl = BodyPrefab.GetComponent<SkillLocator>();
            foreach (GenericSkill old in BodyPrefab.GetComponentsInChildren<GenericSkill>(true)) Object.DestroyImmediate(old);
            sl.passiveSkill.enabled = true;
            sl.passiveSkill.skillNameToken = T + "PASSIVE_NAME";
            sl.passiveSkill.skillDescriptionToken = T + "PASSIVE_DESC";
            sl.passiveSkill.icon = CmxIcons.Get("passive", null);

            SkillDef primary = MakeSkill<SkillDef>("PRIMARY", typeof(CmxLightState), "Weapon", 0f, false, true, InterruptPriority.Any, "primary");
            SkillDef secondary = MakeSkill<SkillDef>("SECONDARY", typeof(CmxHeavyState), "Weapon", 0f, false, true, InterruptPriority.Skill, "secondary");
            SkillDef utility = MakeSkill<SkillDef>("UTILITY", typeof(CmxPartsState), "Body", UtilityCooldown.Value, true, false, InterruptPriority.PrioritySkill, "utility");
            SkillDef special = MakeSkill<CmxCoworkerSkillDef>("SPECIAL", typeof(CmxCoworkersState), "Weapon", SpecialCooldown.Value, true, true, InterruptPriority.Skill, "special");
            utility.cancelSprintingOnActivation = false;

            sl.primary = Slot("CarMaxPrimary", primary);
            sl.secondary = Slot("CarMaxSecondary", secondary);
            sl.utility = Slot("CarMaxUtility", utility);
            sl.special = Slot("CarMaxSpecial", special);
        }

        private static GenericSkill Slot(string family, SkillDef def)
        {
            GenericSkill slot = BodyPrefab.AddComponent<GenericSkill>();
            SkillFamily fam = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)fam).name = family;
            fam.variants = new[] { new SkillFamily.Variant { skillDef = def, unlockableDef = null, viewableNode = new ViewablesCatalog.Node(def.skillNameToken, false, null) } };
            ContentAddition.AddSkillFamily(fam);
            FieldInfo f = typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(slot, fam);
            return slot;
        }

        private void RegisterSurvivor()
        {
            SurvivorDef s = ScriptableObject.CreateInstance<SurvivorDef>();
            s.cachedName = "CarMaxSpecialist";
            s.bodyPrefab = BodyPrefab;
            s.displayPrefab = DisplayPrefab;
            s.displayNameToken = T + "NAME";
            s.descriptionToken = T + "DESCRIPTION";
            s.outroFlavorToken = T + "OUTRO";
            s.mainEndingEscapeFailureFlavorToken = T + "FAIL";
            s.desiredSortPosition = 104f;
            s.primaryColor = new Color(0.2f, 0.45f, 0.85f);
            ContentAddition.AddSurvivorDef(s);
        }

        private void Update()
        {
            CmxTts.Pump();
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Config.Reload();
                CmxAudio.Load(Folder("Sounds"), Volume.Value);
            }
        }
    }
}
