using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using Path = System.IO.Path;

namespace BerserkFury
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(R2API.PrefabAPI.PluginGUID)]
    [BepInDependency(R2API.LanguageAPI.PluginGUID)]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID)]
    public class BerserkFuryPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.bcm.berserkfury";
        public const string PluginName = "BerserkFury";
        public const string PluginVersion = "0.2.1";
        public const string TokenPrefix = "BCM_BERSERKFURY_";

        public static ManualLogSource Log;
        public static BerserkFuryPlugin Instance;

        public static GameObject BodyPrefab;
        public static GameObject DisplayPrefab;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // Pre-load model & texture
            BerserkFuryModel.EnsureLoaded();

            RegisterLanguage();
            CreateBody();
            CreateSkills();
            RegisterSurvivor();

            RecalculateStatsAPI.GetStatCoefficients += ApplyStats;

            Log.LogInfo($"{PluginName} v{PluginVersion} initialized! Zoids Battle Legends ready.");
        }

        private void ApplyStats(CharacterBody body, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!body || !body.gameObject) return;
            var passive = body.GetComponent<BerserkFuryPassiveController>();
            if (!passive) return;

            // E-Shield armor bonus
            if (passive.IsEShieldActive)
            {
                args.armorAdd += 50f;
            }

            // Organoid overdrive stats
            if (passive.IsOverdrive)
            {
                args.attackSpeedMultAdd += 0.25f;
                args.moveSpeedMultAdd += 0.20f;
                args.armorAdd += 25f;
            }
        }

        private void CreateBody()
        {
            GameObject commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            BodyPrefab = PrefabAPI.InstantiateClone(commando, "BerserkFuryBody", true);

            CharacterBody body = BodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = TokenPrefix + "NAME";
            body.subtitleNameToken = TokenPrefix + "SUBTITLE";
            body.bodyColor = new Color(0.6f, 0.2f, 0.85f);
            body.baseMaxHealth = 160f;
            body.levelMaxHealth = 48f;
            body.baseRegen = 1.5f;
            body.levelRegen = 0.3f;
            body.baseDamage = 14f;
            body.levelDamage = 2.8f;
            body.baseArmor = 20f;
            body.baseMoveSpeed = 7.5f;
            body.baseAcceleration = 32f;   // heavy: slower to start and stop (Commando = 80)

            Texture portraitTex = LoadTexture("portrait");
            if (portraitTex) body.portraitIcon = portraitTex;

            // Camera: this Zoid is ~3x Commando's size, so pull the camera back and up.
            try
            {
                CameraTargetParams ctp = BodyPrefab.GetComponent<CameraTargetParams>();
                if (ctp && ctp.cameraParams)
                {
                    CharacterCameraParams ccp = UnityEngine.Object.Instantiate(ctp.cameraParams);
                    ccp.name = "ccpBerserkFury";
                    ccp.data.idealLocalCameraPos = new Vector3(0f, 1.2f, -13f);
                    ccp.data.pivotVerticalOffset = 1.6f;
                    ctp.cameraParams = ccp;
                }
            }
            catch (System.Exception e) { Log.LogWarning("Camera params: " + e.Message); }

            // Attach passive controller
            BodyPrefab.AddComponent<BerserkFuryPassiveController>();
            BodyPrefab.AddComponent<BerserkFuryPilot>();

            // Attach Berserk Fury 3D model
            Transform modelTransform = GetModelTransform(BodyPrefab);
            if (modelTransform)
            {
                BerserkFuryModel.Attach(modelTransform.gameObject);
            }

            ContentAddition.AddBody(BodyPrefab);

            // Display prefab for Character Select Lobby
            GameObject commandoDisplay = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            DisplayPrefab = PrefabAPI.InstantiateClone(commandoDisplay, "BerserkFuryDisplay", false);

            Transform displayModel = GetModelTransform(DisplayPrefab);
            if (displayModel)
            {
                BerserkFuryModel.Attach(displayModel.gameObject);
            }
        }

        private Transform GetModelTransform(GameObject root)
        {
            ModelLocator ml = root ? root.GetComponent<ModelLocator>() : null;
            if (ml && ml.modelTransform) return ml.modelTransform;
            CharacterModel cm = root ? root.GetComponentInChildren<CharacterModel>(true) : null;
            return cm ? cm.transform : (root ? root.transform : null);
        }

        private void CreateSkills()
        {
            ContentAddition.AddEntityState<BusterBlasterState>(out _);
            ContentAddition.AddEntityState<MagneserDrillState>(out _);
            ContentAddition.AddEntityState<IonThrusterSurgeState>(out _);
            ContentAddition.AddEntityState<ChargedParticleCannonState>(out _);

            SkillLocator sl = BodyPrefab.GetComponent<SkillLocator>();
            foreach (GenericSkill old in BodyPrefab.GetComponentsInChildren<GenericSkill>(true))
            {
                DestroyImmediate(old);
            }

            // Passive
            sl.passiveSkill.enabled = true;
            sl.passiveSkill.skillNameToken = TokenPrefix + "PASSIVE_NAME";
            sl.passiveSkill.skillDescriptionToken = TokenPrefix + "PASSIVE_DESC";
            sl.passiveSkill.icon = LoadSprite("passive");

            // Primary, Secondary, Utility, Special
            SkillDef primary = MakeSkill("PRIMARY", typeof(BusterBlasterState), "Weapon", 0f, false, true, InterruptPriority.Any, "primary");
            SkillDef secondary = MakeSkill("SECONDARY", typeof(MagneserDrillState), "Weapon", 4.0f, false, true, InterruptPriority.Skill, "secondary");
            SkillDef utility = MakeSkill("UTILITY", typeof(IonThrusterSurgeState), "Body", 5.0f, true, false, InterruptPriority.PrioritySkill, "utility");
            SkillDef special = MakeSkill("SPECIAL", typeof(ChargedParticleCannonState), "Weapon", 10.0f, true, true, InterruptPriority.Death, "special");

            utility.cancelSprintingOnActivation = false;

            sl.primary = RegisterSlot("BerserkFuryPrimary", primary);
            sl.secondary = RegisterSlot("BerserkFurySecondary", secondary);
            sl.utility = RegisterSlot("BerserkFuryUtility", utility);
            sl.special = RegisterSlot("BerserkFurySpecial", special);
        }

        private SkillDef MakeSkill(string key, Type state, string machine, float cooldown, bool mustKeyPress, bool combat, InterruptPriority prio, string iconName)
        {
            SkillDef d = ScriptableObject.CreateInstance<SkillDef>();
            d.skillName = TokenPrefix + key;
            ((ScriptableObject)d).name = TokenPrefix + key;
            d.skillNameToken = TokenPrefix + key + "_NAME";
            d.skillDescriptionToken = TokenPrefix + key + "_DESC";
            d.icon = LoadSprite(iconName);
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

        private GenericSkill RegisterSlot(string familyName, SkillDef def)
        {
            GenericSkill slot = BodyPrefab.AddComponent<GenericSkill>();
            SkillFamily fam = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)fam).name = familyName;
            fam.variants = new[] { new SkillFamily.Variant { skillDef = def, unlockableDef = null, viewableNode = new ViewablesCatalog.Node(def.skillNameToken, false, null) } };
            ContentAddition.AddSkillFamily(fam);

            FieldInfo f = typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(slot, fam);
            return slot;
        }

        private void RegisterSurvivor()
        {
            SurvivorDef s = ScriptableObject.CreateInstance<SurvivorDef>();
            s.cachedName = "BerserkFury";
            s.bodyPrefab = BodyPrefab;
            s.displayPrefab = DisplayPrefab;
            s.displayNameToken = TokenPrefix + "NAME";
            s.descriptionToken = TokenPrefix + "DESCRIPTION";
            s.outroFlavorToken = TokenPrefix + "OUTRO";
            s.mainEndingEscapeFailureFlavorToken = TokenPrefix + "FAIL";
            s.desiredSortPosition = 106f;
            s.primaryColor = new Color(0.6f, 0.2f, 0.85f);
            ContentAddition.AddSurvivorDef(s);
        }

        private static Sprite LoadSprite(string name)
        {
            Texture2D tex = LoadTexture(name);
            if (!tex) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Texture2D LoadTexture(string name)
        {
            string iconPath = BerserkFuryModel.FindFile(Path.Combine("Icons", name + ".png"));
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath)) return null;

            byte[] data = File.ReadAllBytes(iconPath);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.LoadImage(data);
            return tex;
        }

        private void RegisterLanguage()
        {
            LanguageAPI.Add(TokenPrefix + "NAME", "Berserk Fury");
            LanguageAPI.Add(TokenPrefix + "SUBTITLE", "The Ultimate Tyrannosaur Zoid");
            LanguageAPI.Add(TokenPrefix + "DESCRIPTION",
                "<color=#9C27B0>Berserk Fury</color> is an apex combat Zoid straight from <color=#00E5FF>Zoids: Battle Legends</color>.\n\n" +
                "<color=#E040FB>[PASSIVE] Organoid Drive & E-Shield:</color> Dealing damage accumulates <color=#E040FB>Particle Energy</color>. At 100%, enter Overdrive for +25% attack speed and +20% move speed. Sprinting deploys an <color=#00E5FF>E-Shield</color> deflecting incoming projectiles.\n" +
                "<color=#E040FB>[M1] Twin Buster Blaster:</color> Fire alternating particle beams for <style=cIsDamage>320% damage</style>. Close-range strikes inflict bleed.\n" +
                "<color=#E040FB>[M2] Magneser Drill Thrust:</color> Thrust spinning drill claws forward, shredding enemies for <style=cIsDamage>7x85% damage</style>.\n" +
                "<color=#E040FB>[SHIFT] Ion Thruster Surge:</color> Engage high-output rocket thrusters, rocketing forward and releasing a <style=cIsDamage>450% concussive blast</style>.\n" +
                "<color=#E040FB>[R] Charged Particle Cannon (CPC):</color> Anchor into the ground and discharge a screen-clearing particle mega-beam for <style=cIsDamage>3000%+ total damage</style>.");

            LanguageAPI.Add(TokenPrefix + "OUTRO", "...and so the Ultimate Zoid roared into the cosmos, unchallenged.");
            LanguageAPI.Add(TokenPrefix + "FAIL", "...and so the reactor core went dark upon the desolate soil.");

            LanguageAPI.Add(TokenPrefix + "PASSIVE_NAME", "Organoid Drive & E-Shield");
            LanguageAPI.Add(TokenPrefix + "PASSIVE_DESC", "Hits build <style=cIsDamage>Particle Charge</style>. At 100%, gain +25% attack speed and +20% movement speed. Sprinting projects a frontal <style=cIsUtility>E-Shield</style> that grants <style=cIsHealing>+50 Armor</style> and deflects hostile projectiles.");

            LanguageAPI.Add(TokenPrefix + "PRIMARY_NAME", "Twin Buster Blaster");
            LanguageAPI.Add(TokenPrefix + "PRIMARY_DESC", "Fire high-velocity particle beam rounds from Buster Claws for <style=cIsDamage>320% damage</style>. Close foes are slashed for <style=cIsDamage>150% damage</style> and bleed.");

            LanguageAPI.Add(TokenPrefix + "SECONDARY_NAME", "Magneser Drill Thrust");
            LanguageAPI.Add(TokenPrefix + "SECONDARY_DESC", "Lunge forward with dual spinning magneser drills, piercing enemies for <style=cIsDamage>7x85% damage</style>.");

            LanguageAPI.Add(TokenPrefix + "UTILITY_NAME", "Ion Thruster Surge");
            LanguageAPI.Add(TokenPrefix + "UTILITY_DESC", "Rocket forward with high-output ion thrusters. Detonates a <style=cIsDamage>450% stun shockwave</style> on completion.");

            LanguageAPI.Add(TokenPrefix + "SPECIAL_NAME", "Charged Particle Cannon (CPC)");
            LanguageAPI.Add(TokenPrefix + "SPECIAL_DESC", "Anchor into the ground and charge a massive particle vortex, then fire an immense continuous mega-beam dealing <style=cIsDamage>3000%+ total damage</style>.");
        }
    }
}
