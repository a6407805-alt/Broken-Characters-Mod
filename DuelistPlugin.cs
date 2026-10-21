using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EntityStates;
using HarmonyLib;
using R2API;
using R2API.ContentManagement;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    [BepInDependency(R2API.Networking.NetworkingAPI.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class DuelistPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.ahren.duelist";
        public const string PluginName = "Duelist";
        public const string PluginVersion = "1.1.0";
        public const string TokenPrefix = "AHREN_DUELIST_";

        public static ManualLogSource Log;
        public static GameObject BodyPrefab;
        public static GameObject DisplayPrefab;

        public static readonly Color MainColor = new Color(0.95f, 0.78f, 0.25f);

        // Two DuelistMod.dll files under plugins make the game run states from the copy whose
        // settings were never loaded: every duel/partner call then throws NullReferenceException.
        private static void WarnDuplicateCopies()
        {
            try
            {
                var copies = System.IO.Directory.GetFiles(BepInEx.Paths.PluginPath, "DuelistMod.dll", System.IO.SearchOption.AllDirectories);
                if (copies.Length <= 1) return;
                Log.LogError("Found " + copies.Length + " copies of DuelistMod.dll - delete all but one or duels and partners will break:\n  " + string.Join("\n  ", copies));
            }
            catch { }
        }

        private void Awake()
        {
            Log = Logger;
            DuelConfig.Bind(Config);
            WarnDuplicateCopies();
            DuelistTokens.Register();
            DuelNet.Register();
            DuelVenom.Register();
            DuelistHover.Register();

            CreateBody();
            CreateDisplay();
            CreateSkills();
            CreateSurvivor();
            CardRain.Create();
            Partners.Create();
            ApplyPatches();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Arena height {DuelConfig.ArenaHeight.Value}, deck max {DuelConfig.MaxDeckSize.Value}.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(DuelConfig.ReloadKey.Value))
            {
                Config.Reload();
                Log.LogInfo("Duelist config reloaded.");
                try { Chat.AddMessage("<color=#f2c640>Duelist config reloaded.</color>"); } catch { }
            }
        }

        // ---------------------------------------------------------------- body

        private void CreateBody()
        {
            GameObject commando = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            BodyPrefab = PrefabAPI.InstantiateClone(commando, "DuelistBody", true);

            CharacterBody body = BodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = TokenPrefix + "NAME";
            body.subtitleNameToken = TokenPrefix + "SUBTITLE";
            body.bodyColor = MainColor;
            body.baseMaxHealth = 100f;
            body.levelMaxHealth = 30f;
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseMoveSpeed = 7f;
            body.baseRegen = 1f;
            body.levelRegen = 0.2f;
            body.portraitIcon = DuelistIcons.MakeTexture(new Color(0.18f, 0.1f, 0.3f), MainColor, DuelistIcons.Symbol.Deck, 128);

            BodyPrefab.AddComponent<DuelistMarker>();
            BodyPrefab.AddComponent<PartnerManager>();
            BodyPrefab.AddComponent<DuelistHover>();
            BodyPrefab.AddComponent<DuelistBoardVisual>();
            BodyPrefab.AddComponent<DuelistTrickHud>();
            Transform bodyModel = DuelistVisuals.FindModel(BodyPrefab);
            if (bodyModel) { bodyModel.gameObject.AddComponent<DuelistCustomModel>(); bodyModel.gameObject.AddComponent<DuelistLevitate>(); if (DuelConfig.FlowingOutfit.Value) bodyModel.gameObject.AddComponent<DuelistOutfitFlow>(); }

            DuelistVisuals.AttachDeckProp(BodyPrefab);
            DuelistVisuals.AddTinter(BodyPrefab);

            ContentAddition.AddBody(BodyPrefab);
        }

        private void CreateDisplay()
        {
            GameObject commandoDisplay = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            DisplayPrefab = PrefabAPI.InstantiateClone(commandoDisplay, "DuelistDisplay", false);
            Transform displayModel = DuelistVisuals.FindModel(DisplayPrefab);
            if (displayModel) { displayModel.gameObject.AddComponent<DuelistCustomModel>(); displayModel.gameObject.AddComponent<DuelistLevitate>(); if (DuelConfig.FlowingOutfit.Value) displayModel.gameObject.AddComponent<DuelistOutfitFlow>(); }
            DuelistVisuals.AttachDeckProp(DisplayPrefab);
            DuelistVisuals.AddTinter(DisplayPrefab);
        }

        // ---------------------------------------------------------------- skills

        private void CreateSkills()
        {
            ContentAddition.AddEntityState<CardTossState>(out _);
            ContentAddition.AddEntityState<CardFanState>(out _);
            ContentAddition.AddEntityState<SleightOfHandState>(out _);
            ContentAddition.AddEntityState<DuelDimensionState>(out _);

            foreach (GenericSkill old in BodyPrefab.GetComponentsInChildren<GenericSkill>(true))
                Object.DestroyImmediate(old);

            SkillLocator locator = BodyPrefab.GetComponent<SkillLocator>();

            locator.passiveSkill.enabled = true;
            locator.passiveSkill.skillNameToken = TokenPrefix + "PASSIVE_NAME";
            locator.passiveSkill.skillDescriptionToken = TokenPrefix + "PASSIVE_DESC";
            locator.passiveSkill.icon = DuelistIcons.MakeSprite(new Color(0.25f, 0.12f, 0.35f), MainColor, DuelistIcons.Symbol.Deck);

            SkillDef primary = MakeSkill("PRIMARY", typeof(CardTossState), "Weapon", 0f, InterruptPriority.Any, false,
                DuelistIcons.MakeSprite(new Color(0.45f, 0.1f, 0.12f), new Color(1f, 0.95f, 0.85f), DuelistIcons.Symbol.Card));
            SkillDef secondary = MakeSkill("SECONDARY", typeof(CardFanState), "Weapon", 4f, InterruptPriority.Skill, true,
                DuelistIcons.MakeSprite(new Color(0.12f, 0.2f, 0.45f), new Color(1f, 0.95f, 0.85f), DuelistIcons.Symbol.Fan));
            SkillDef utility = MakeSkill("UTILITY", typeof(PartnerSwapState), "Weapon", DuelConfig.PartnerSwapCooldown.Value, InterruptPriority.Any, true,
                DuelistIcons.MakeSprite(new Color(0.1f, 0.35f, 0.25f), new Color(0.85f, 1f, 0.9f), DuelistIcons.Symbol.Arrow));
            SkillDef special = MakeSkill("SPECIAL", typeof(DuelDimensionState), "Body", DuelConfig.DuelCooldown.Value, InterruptPriority.PrioritySkill, true,
                DuelistIcons.MakeRelicSprite(new Color(0.3f, 0.08f, 0.45f), DuelistIcons.Symbol.RelicKey, 0)); // the Key opens the duel

            primary.isCombatSkill = true;
            secondary.isCombatSkill = true;
            utility.isCombatSkill = false;
            special.isCombatSkill = true;

            locator.primary = AddSlot("DuelistPrimaryFamily", primary);
            locator.secondary = AddSlot("DuelistSecondaryFamily", secondary);
            locator.utility = AddSlot("DuelistUtilityFamily", utility);
            locator.special = AddSlot("DuelistSpecialFamily", special);
        }

        private static SkillDef MakeSkill(string key, Type state, string machine, float cooldown, InterruptPriority priority, bool mustKeyPress, Sprite icon)
        {
            SkillDef def = ScriptableObject.CreateInstance<SkillDef>();
            def.skillName = TokenPrefix + key;
            ((ScriptableObject)def).name = TokenPrefix + key;
            def.skillNameToken = TokenPrefix + key + "_NAME";
            def.skillDescriptionToken = TokenPrefix + key + "_DESC";
            def.icon = icon;
            def.activationState = new SerializableEntityStateType(state);
            def.activationStateMachineName = machine;
            def.baseMaxStock = 1;
            def.baseRechargeInterval = cooldown;
            def.rechargeStock = 1;
            def.requiredStock = 1;
            def.stockToConsume = 1;
            def.beginSkillCooldownOnSkillEnd = true;
            def.canceledFromSprinting = false;
            def.cancelSprintingOnActivation = true;
            def.fullRestockOnAssign = true;
            def.mustKeyPress = mustKeyPress;
            def.interruptPriority = priority;
            ContentAddition.AddSkillDef(def);
            return def;
        }

        private static GenericSkill AddSlot(string familyName, SkillDef def)
        {
            GenericSkill slot = BodyPrefab.AddComponent<GenericSkill>();
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = familyName;
            family.variants = new[]
            {
                new SkillFamily.Variant
                {
                    skillDef = def,
                    unlockableDef = null,
                    viewableNode = new ViewablesCatalog.Node(def.skillNameToken, false, null)
                }
            };
            ContentAddition.AddSkillFamily(family);

            FieldInfo field = typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(slot, family);
            else Log.LogError("GenericSkill._skillFamily not found; skills will be empty.");
            return slot;
        }

        // ---------------------------------------------------------------- survivor

        private void CreateSurvivor()
        {
            SurvivorDef def = ScriptableObject.CreateInstance<SurvivorDef>();
            ((ScriptableObject)def).name = "DuelistSurvivor";
            def.bodyPrefab = BodyPrefab;
            def.displayPrefab = DisplayPrefab;
            def.displayNameToken = TokenPrefix + "NAME";
            def.descriptionToken = TokenPrefix + "DESCRIPTION";
            def.outroFlavorToken = TokenPrefix + "OUTRO";
            def.mainEndingEscapeFailureFlavorToken = TokenPrefix + "FAIL";
            def.primaryColor = MainColor;
            def.desiredSortPosition = 103f;
            ContentAddition.AddSurvivorDef(def);
        }

        // ---------------------------------------------------------------- patches

        private void ApplyPatches()
        {
            try
            {
                Harmony harmony = new Harmony(PluginGUID);
                try { DuelRegen.Apply(harmony); }
                catch (Exception e) { Log.LogWarning("No-regen patch failed: " + e.Message); }
                MethodInfo target = AccessTools.Method(typeof(MapZone), "TeleportBody");
                if (target != null)
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(typeof(DuelPatches).GetMethod(nameof(DuelPatches.MapZoneTeleportPrefix))));
                    Log.LogInfo("Patched MapZone.TeleportBody (arena is not treated as out of bounds).");
                }
                else
                {
                    Log.LogWarning("MapZone.TeleportBody not found; the arena might get bounced back by out-of-bounds zones. Lower ArenaHeight if so.");
                }
            }
            catch (Exception e)
            {
                Log.LogWarning("Harmony patch failed: " + e.Message);
            }
        }
    }

    /// <summary>Tags the Duelist body so other code can recognise it.</summary>
    public class DuelistMarker : MonoBehaviour, IOnIncomingDamageServerReceiver
    {
        private CharacterBody _body;

        private void Start()
        {
            if (!UnityEngine.Networking.NetworkServer.active) return;
            _body = GetComponent<CharacterBody>();
            if (_body && _body.master && !_body.master.GetComponent<DuelistDeck>())
                _body.master.gameObject.AddComponent<DuelistDeck>();

            // Featherpuff's guard: listen to incoming damage on the server.
            HealthComponent hc = _body ? _body.healthComponent : null;
            if (hc)
            {
                IOnIncomingDamageServerReceiver[] old = hc.onIncomingDamageReceivers ?? new IOnIncomingDamageServerReceiver[0];
                IOnIncomingDamageServerReceiver[] arr = new IOnIncomingDamageServerReceiver[old.Length + 1];
                old.CopyTo(arr, 0);
                arr[old.Length] = this;
                hc.onIncomingDamageReceivers = arr;
            }
        }

        /// <summary>Featherpuff takes the first big hit for you, then faints.</summary>
        public void OnIncomingDamageServer(DamageInfo damageInfo)
        {
            if (damageInfo == null || damageInfo.rejected || !_body || !_body.healthComponent) return;
            PartnerManager pm = _body.GetComponent<PartnerManager>();
            if (!pm || pm.Current != PartnerKind.Featherpuff) return;
            CharacterBody pb = pm.PartnerBody;
            if (!pb || !pb.healthComponent || !pb.healthComponent.alive) return;
            if (damageInfo.damage < _body.healthComponent.fullCombinedHealth * DuelConfig.FeatherpuffShieldThreshold.Value) return;

            damageInfo.rejected = true;
            Partners.Circle(pb.footPosition, new Color(1f, 0.95f, 0.85f), 0.6f);
            try { Chat.AddMessage("<color=#fff2d0>Featherpuff takes the hit for you!</color>"); } catch { }
            pb.healthComponent.Suicide(); // faints; ready again later
        }
    }

    public static class DuelPatches
    {
        public static bool MapZoneTeleportPrefix(object[] __args)
        {
            if (__args == null || __args.Length == 0) return true;
            CharacterBody body = __args[0] as CharacterBody;
            if (body && DuelDimensionState.ActiveDuelists.Contains(body)) return false;
            return true;
        }
    }

    public static class DuelConfig
    {
        public static ConfigEntry<KeyCode> ReloadKey;

        public static ConfigEntry<float> DuelCooldown;
        public static ConfigEntry<float> ArenaHeight;
        public static ConfigEntry<int> MaxRounds;
        public static ConfigEntry<int> StartingHand;
        public static ConfigEntry<int> DrawPerTurn;
        public static ConfigEntry<int> MaxHandSize;
        public static ConfigEntry<int> NormalSummonsPerTurn;
        public static ConfigEntry<float> EnemyTurnSpeed;
        public static ConfigEntry<float> MaxDuelSeconds;



        public static ConfigEntry<float> EnemyUnitAttack;
        public static ConfigEntry<float> BossAttackMult;
        public static ConfigEntry<int> BossActions;
        public static ConfigEntry<float> DirectorLifeFraction;

        public static ConfigEntry<float> MonsterAtkCoeff;
        public static ConfigEntry<float> MonsterHpFraction;
        public static ConfigEntry<float> ExtraDeckDroneWeight;
        public static ConfigEntry<float> SpellDamage;
        public static ConfigEntry<int> MaxDeckSize;
        public static ConfigEntry<int> MetaDeckSize;
        public static ConfigEntry<bool> DronesBecomeCards;
        public static ConfigEntry<bool> KeepItems;
        public static ConfigEntry<bool> PartnersInDeck;
        public static ConfigEntry<bool> ExtraToolkit;
        public static ConfigEntry<float> LinkBoost;
        public static ConfigEntry<float> DefenseDamage;
        public static ConfigEntry<float> KillGoldBonus;
        public static ConfigEntry<float> VictoryGoldBonus;
        public static ConfigEntry<float> VictoryJackpotChests;
        public static ConfigEntry<int> VictoryLegendary;
        public static ConfigEntry<int> VictoryUncommon;
        public static ConfigEntry<bool> VictoryLevelUp;
        public static ConfigEntry<float> VictoryBuffSeconds;
        public static ConfigEntry<bool> VenomAura;
        public static ConfigEntry<float> DuelVenomSeconds;
        public static ConfigEntry<bool> Controller;
        public static ConfigEntry<float> CardTossSpell;
        public static ConfigEntry<float> HealCardFraction;

        public static ConfigEntry<float> PartnerFaintTime;
        public static ConfigEntry<float> PartnerAutoDamage;
        public static ConfigEntry<float> PartnerAutoInterval;
        public static ConfigEntry<float> PartnerSwapCooldown;
        public static ConfigEntry<float> FeatherpuffShieldThreshold;
        public static ConfigEntry<float> PartnerSize;
        public static ConfigEntry<bool> UseCustomModel;
        public static ConfigEntry<bool> ModelOutline;
        public static ConfigEntry<float> ModelGlow;
        public static ConfigEntry<float> ModelScale;

        public static ConfigEntry<bool> AlliesJoinDuel;
        public static ConfigEntry<float> AllyAtkCoeff;

        public static ConfigEntry<bool> CardRain;
        public static ConfigEntry<int> RainPerHit;

        public static ConfigEntry<bool> TabletopCamera;
        public static ConfigEntry<float> CameraHeight;
        public static ConfigEntry<float> CameraBack;
        public static ConfigEntry<float> CameraLookAt;
        public static ConfigEntry<float> CameraFov;

        public static ConfigEntry<float> TossDamage;
        public static ConfigEntry<float> FanDamage;
        public static ConfigEntry<int> FanCount;

        // Yu-Gi-Oh! rules
        public static ConfigEntry<float> EnemyAtkScale;
        public static ConfigEntry<float> EnemyDefScale;
        public static ConfigEntry<float> PhaseDelay;
        public static ConfigEntry<float> ChainWindow;
        public static ConfigEntry<bool> FirstTurnNoBattle;
        public static ConfigEntry<int> EndPhaseHandLimit;
        public static ConfigEntry<float> EnemyResponseChance;
        public static ConfigEntry<bool> CritInBattle;
        public static ConfigEntry<bool> Hover;
        public static ConfigEntry<bool> StartHovering;
        public static ConfigEntry<bool> FlowingOutfit;
        public static ConfigEntry<bool> NoRegenInDuel;

        // looks and sound
        public static ConfigEntry<bool> UsePlaymat;
        public static ConfigEntry<float> PlaymatGlow;
        public static ConfigEntry<float> SoundVolume;
        public static readonly System.Collections.Generic.Dictionary<DuelSound, ConfigEntry<string>> GameSounds = new System.Collections.Generic.Dictionary<DuelSound, ConfigEntry<string>>();

        public static void Bind(ConfigFile cfg)
        {
            ReloadKey = cfg.Bind("General", "ReloadKey", KeyCode.F8, "Reloads this config in game.");

            DuelCooldown = cfg.Bind("Duel", "Cooldown", 30f, "Special cooldown in seconds, starting when the duel ends. Needs a restart.");
            ArenaHeight = cfg.Bind("Duel", "ArenaHeight", 250f, "How far above you the duel arena is built.");
            MaxRounds = cfg.Bind("Duel", "MaxRounds_v2", 5, "Rounds before the duel dimension collapses and sends you back.");
            StartingHand = cfg.Bind("Duel", "StartingHand", 5, "Cards drawn when the duel begins.");
            DrawPerTurn = cfg.Bind("Duel", "DrawPerTurn", 2, "Cards drawn at the start of each later turn.");
            MaxHandSize = cfg.Bind("Duel", "MaxHandSize", 8, "Hand size limit.");
            NormalSummonsPerTurn = cfg.Bind("Duel", "NormalSummonsPerTurn", 1, "Monsters you can summon from your hand each turn (fusions from the Extra Deck don't count).");
            MaxDuelSeconds = cfg.Bind("Duel", "MaxDuelSeconds_v2", 900f, "Safety limit: the duel ends after this many seconds no matter what (phases and chains make duels longer).");
            EnemyTurnSpeed = cfg.Bind("Duel", "EnemyActionDelay", 0.9f, "Seconds between enemy attacks, so each hit can be read.");



            EnemyUnitAttack = cfg.Bind("Enemies", "UnitAttack", 1.0f, "Each enemy card attacks once per turn for its real damage x this.");
            BossAttackMult = cfg.Bind("Enemies", "CommanderAttackMult", 1.5f, "A boss commander's attacks are this much stronger.");
            BossActions = cfg.Bind("Enemies", "CommanderAttacks", 2, "Attacks a boss commander makes each enemy turn.");
            DirectorLifeFraction = cfg.Bind("Enemies", "DirectorLifeFraction", 0.3f, "With no boss, the Director's life points = this x the total max HP of the enemies at the start of the duel. Direct attacks lower it; zero wins the duel.");
            MonsterAtkCoeff = cfg.Bind("Cards", "MonsterAtk", 2.5f, "Monster ATK as a multiple of your damage, before tier and stack bonuses. ATK is damage per enemy unit at full range.");
            MonsterHpFraction = cfg.Bind("Cards", "MonsterHp", 0.3f, "Monster HP as a fraction of your max HP, before tier and stack bonuses.");
            ExtraDeckDroneWeight = cfg.Bind("Cards", "ExtraDeckDroneWeight", 2.5f, "Robots at least this strong (base HP / 150; Gunner Drone = 1, TC-280 ≈ 3+) are Extra Deck fusions instead of monsters.");
            SpellDamage = cfg.Bind("Cards", "SpellDamage", 2.5f, "Damage of a white damage-item spell (2.5 = 250%); green x1.6, red/boss hit every species.");
            MaxDeckSize = cfg.Bind("Deck", "MaxDeckSize", 60, "Main deck limit. Items, equipment and drones picked up past this stay normal.");
            MetaDeckSize = cfg.Bind("Deck", "RecommendedDeckSize", 40, "Shown as the target deck size.");
            PartnersInDeck = cfg.Bind("Deck", "PartnersInDeck", true, "Your 5 partners (Featherpuff, Tock, Hollow Idol, Jinx, Gloop) are monster cards in your duel deck, each with its own effect.");
            DronesBecomeCards = cfg.Bind("Deck", "DronesBecomeCards", true, "Drones and robots you buy or repair leave the field and become monster cards.");
            KeepItems = cfg.Bind("Deck", "KeepItems", true, "Items and equipment you pick up stay in your inventory and keep their normal effects; each new copy ALSO becomes a card. Off = the old way (items are taken out and only become cards).");
            ExtraToolkit = cfg.Bind("Summoning", "ExtraToolkit", true, "Every Extra Deck starts with Scrap Chimera (Fusion), Tuned Colossus (Synchro), Overlay Sentinel (XYZ) and Link Arbiter (Link).");
            LinkBoost = cfg.Bind("Summoning", "LinkBoostPerRating", 0.15f, "ATK bonus a Link monster gives your other monsters, per Link rating.");
            DefenseDamage = cfg.Bind("Summoning", "DefenseDamageTaken", 0.5f, "Damage a monster in defense position takes (0.5 = half).");
            KillGoldBonus = cfg.Bind("Gold", "KillGoldBonus", 1.0f, "Extra gold per enemy killed in a duel, as a multiple of its normal gold (1.0 = double gold).");
            VictoryGoldBonus = cfg.Bind("Gold", "VictoryGoldBonus", 0.5f, "Winning the duel pays this fraction of your duel bonus again.");
            VictoryJackpotChests = cfg.Bind("Victory", "JackpotChests", 4f, "Winning a duel pays a gold jackpot worth this many small chests (scales with difficulty).");
            VictoryLegendary = cfg.Bind("Victory", "LegendaryDrops", 1, "Legendary (red) items that drop when you win a duel. Beating a boss or the Director adds one more.");
            VictoryUncommon = cfg.Bind("Victory", "UncommonDrops", 2, "Uncommon (green) items that drop when you win a duel.");
            VictoryLevelUp = cfg.Bind("Victory", "LevelUp", true, "Winning a duel gives your team an instant level up.");
            VictoryBuffSeconds = cfg.Bind("Victory", "GlorySeconds", 30f, "After a win: full heal plus Victor's Glory (big move and attack speed) for this many seconds. 0 = off.");
            VenomAura = cfg.Bind("Visuals", "DuelVenom", true, "Anything hurt by a duel gets a purple venom aura on the map (everyone sees it) and purple damage numbers.");
            DuelVenomSeconds = cfg.Bind("Visuals", "DuelVenomSeconds", 10f, "How long the purple venom aura lasts after the last duel hit.");
            Controller = cfg.Bind("Controls", "ControllerCursor", true, "Play duels with a gamepad: left stick moves a cursor, D-pad jumps between buttons, A selects.");
            CardTossSpell = cfg.Bind("Cards", "CardTossSpellDamage", 2.0f, "Starter Card Toss spell damage (2.0 = 200%).");
            HealCardFraction = cfg.Bind("Cards", "ItemHealFraction", 0.12f, "Base max-HP fraction healed by an item heal spell.");

            PartnerFaintTime = cfg.Bind("Partners", "FaintTime", 20f, "Seconds a knocked-out partner needs before it can be summoned again (Gloop reforms in 5).");
            PartnerAutoDamage = cfg.Bind("Partners", "AutoAttackDamage_v2", 1.0f, "Multiplier on every partner's automatic attack (each partner has its own base, about 110-140% of your damage).");
            PartnerAutoInterval = cfg.Bind("Partners", "AutoAttackInterval_v2", 0.9f, "Seconds between the partner's automatic attacks.");
            PartnerSwapCooldown = cfg.Bind("Partners", "SwapCooldown", 1f, "Shift cooldown. Needs a restart.");
            FeatherpuffShieldThreshold = cfg.Bind("Partners", "FeatherpuffShieldThreshold", 0.2f, "Featherpuff blocks the first hit that would take at least this fraction of your max health, then faints.");
            UseCustomModel = cfg.Bind("Model", "UseCustomModel", true, "Use the Blender-made Duelist (duelist_model.bin next to the dll). Off = Commando model.");
            ModelOutline = cfg.Bind("Model", "InkOutline_v2", false, "Black anime outline around the custom model (experimental).");
            ModelGlow = cfg.Bind("Model", "GlowStrength", 2.5f, "How bright the glowing parts (eyes, gems, runes) are.");
            ModelScale = cfg.Bind("Model", "Scale", 1.0f, "Extra size multiplier for the custom model.");
            PartnerSize = cfg.Bind("Partners", "Size", 0.75f, "How big partners look (1.0 = about the size of a drone, was 1.5).");
            AlliesJoinDuel = cfg.Bind("Allies", "AlliesJoinDuel", true, "Your drones, turrets and other minions are pulled into the duel as monster cards (grouped by type) and returned afterwards.");
            AllyAtkCoeff = cfg.Bind("Allies", "AllyAtk", 2.0f, "Ally card ATK = sum of the allies' damage x this.");

            CardRain = cfg.Bind("Card Rain", "Enabled", true, "Cards fall from the sky onto the enemies you hit in the duel, visible to everyone in the game.");
            RainPerHit = cfg.Bind("Card Rain", "CardsPerHit", 10, "Most falling cards per hit (one per damaged unit, up to this).");

            TabletopCamera = cfg.Bind("Camera", "TabletopCamera", true, "Lock the camera above the board during the duel and hide the normal HUD.");
            CameraHeight = cfg.Bind("Camera", "Height_v2", 24f, "Camera height above the board.");
            CameraBack = cfg.Bind("Camera", "Back_v2", 8f, "How far behind you the camera sits.");
            CameraLookAt = cfg.Bind("Camera", "LookAt_v2", 12f, "How far down the board the camera looks.");
            CameraFov = cfg.Bind("Camera", "Fov_v2", 58f, "Field of view during the duel.");

            TossDamage = cfg.Bind("Skills", "CardTossDamage", 1.5f, "Primary damage (1.5 = 150%).");
            FanDamage = cfg.Bind("Skills", "CardFanDamage", 1.0f, "Secondary damage per card.");
            FanCount = cfg.Bind("Skills", "CardFanCount", 5, "Secondary card count.");

            EnemyAtkScale = cfg.Bind("Rules", "EnemyAtkScale", 2.5f, "Enemy ATK = their real damage compared to yours, x this, on the card game's scale (a Beetle is about 1000 ATK).");
            EnemyDefScale = cfg.Bind("Rules", "EnemyDefScale", 0.75f, "Enemy DEF from their health compared to yours, x this (a Beetle is about 1150 DEF).");
            PhaseDelay = cfg.Bind("Rules", "PhaseDelay", 0.9f, "Seconds the automatic phases (Draw, Standby, End, enemy phases) stay on screen.");
            ChainWindow = cfg.Bind("Rules", "ChainWindowSeconds", 8f, "How long you get to respond with a set card before the chain resolves without you.");
            FirstTurnNoBattle = cfg.Bind("Rules", "FirstTurnNoBattlePhase", true, "Like the card game: the player who goes first can't conduct a Battle Phase on turn 1.");
            EndPhaseHandLimit = cfg.Bind("Rules", "EndPhaseHandLimit", 6, "In your End Phase you discard down to this many cards (the card game uses 6).");
            EnemyResponseChance = cfg.Bind("Rules", "EnemyResponseChance", 0.6f, "Chance an enemy elite card chains a response when it's allowed to.");
            CritInBattle = cfg.Bind("Rules", "CritsDoubleAtk", true, "Your crit chance still matters: a critical attack doubles the attacker's ATK for that battle.");
            Hover = cfg.Bind("Skills", "Hover", true, "Allow the hoverboard: double-tap jump to whip it out or put it away. On the board, jump to ollie; big air = tricks = speed boosts.");
            StartHovering = cfg.Bind("Skills", "StartHovering", false, "Start each stage already on the hoverboard instead of walking.");
            FlowingOutfit = cfg.Bind("Model", "FlowingOutfit", true, "The Duelist's coat tails, apron and chains flow and trail as he moves (the hem most, the coat body a little). Needs a restart.");
            NoRegenInDuel = cfg.Bind("Rules", "NoRegenDuringDuels", true, "During a duel nobody regenerates: your LP, the enemy's LP, enemy monsters, and your turrets and drones. Only heal cards restore LP.");

            UsePlaymat = cfg.Bind("Board", "Playmat", true, "Lay the printed duel mat (duel_mat.jpg next to the dll) on the board.");
            PlaymatGlow = cfg.Bind("Board", "PlaymatGlow", 1.4f, "How brightly the mat's zone lines glow.");
            SoundVolume = cfg.Bind("Sounds", "CustomVolume", 0.8f, "Volume for your own .wav files in the sounds folder (named draw.wav, summon.wav, attack.wav…).");
            foreach (DuelSound snd in (DuelSound[])System.Enum.GetValues(typeof(DuelSound)))
                GameSounds[snd] = cfg.Bind("Sounds", snd.ToString(), DuelSfx.DefaultEvent(snd), $"Game sound played for '{snd}' when there's no sounds/{snd.ToString().ToLowerInvariant()}.wav. Empty = silent.");
        }
    }

    public static class DuelistTokens
    {
        public static void Register()
        {
            string p = DuelistPlugin.TokenPrefix;
            LanguageAPI.Add(p + "NAME", "Duelist");
            LanguageAPI.Add(p + "SUBTITLE", "Keeper of the Last Deck");
            LanguageAPI.Add(p + "DESCRIPTION",
                "The Duelist turns everything they find into cards, then drags whole species into a duel they can't refuse.<color=#CCD3E0>\n\n" +
                "< ! > Items keep working as normal, and every one you pick up also becomes a card in your duel deck.\n\n" +
                "< ! > In the duel, summon monsters, set traps and fuse red-item monsters from your Extra Deck.\n\n" +
                "< ! > Every enemy on the map is its own card, drawn from their deck nearest-first.\n\n" +
                "< ! > Big swarms hit hard. Thin them out before ending your turn.\n\n" +
                "< ! > Enemies close to you when the duel starts take the most damage.</color>\n");
            LanguageAPI.Add(p + "OUTRO", "..and so they left, the last hand still unplayed.");
            LanguageAPI.Add(p + "FAIL", "..and so they vanished, their deck scattered across the stars.");

            LanguageAPI.Add(p + "PASSIVE_NAME", "Deckbuilder");
            LanguageAPI.Add(p + "PASSIVE_DESC", "Every item, equipment and robot you pick up also becomes a <style=cIsDamage>card</style> for your duel deck, and items keep working as normal. White, green and red items are <style=cIsHealing>spells</style> (healing items are Quick-Play); equipment and void items are <style=cIsUtility>traps</style>, lunar items Counter Traps; drones you buy are <style=cIsDamage>monsters</style>. Deck limit 60, 40 recommended. Double-tap jump to ride a <style=cIsUtility>hoverboard</style> with skateboard momentum: carve, ollie, and land big-air tricks for speed boosts. Double-tap again to walk.");

            LanguageAPI.Add(p + "PRIMARY_NAME", "Card Toss");
            LanguageAPI.Add(p + "PRIMARY_DESC", "Flick a razor card that <style=cIsUtility>tracks</style> the enemy nearest your crosshair for <style=cIsDamage>150% damage</style>. Every third throw is the <style=cIsDamage>Trump Card</style>: a huge golden card that pierces the line for <style=cIsDamage>375%</style>.");

            LanguageAPI.Add(p + "SECONDARY_NAME", "Pharaoh's Judgment");
            LanguageAPI.Add(p + "SECONDARY_DESC", "<style=cIsUtility>Pharaoh's Judgment</style>: four golden cards rise around you, then each hunts down a different enemy for <style=cIsDamage>175% damage</style>.");

            LanguageAPI.Add(p + "UTILITY_NAME", "Call Partner");
            LanguageAPI.Add(p + "UTILITY_DESC", "<style=cIsUtility>Special Summon</style> the next partner or robot card from your deck (or none). Robots fight on their own. It fights beside you; while it's out, <style=cIsDamage>M1, M2 and R</style> are its abilities. <style=cIsUtility>1</style>: attack your target. <style=cIsUtility>2</style>: come back.");

            LanguageAPI.Add(p + "SPECIAL_NAME", "Duel Dimension");
            LanguageAPI.Add(p + "SPECIAL_DESC", "Pull everyone on the map into a <style=cIsUtility>Yu-Gi-Oh!-style duel</style>: Draw, Main, Battle and End phases, <style=cIsDamage>ATK vs DEF</style> battles, chains with Spell Speeds. Every enemy is a monster card; destroy it in the duel and it dies for real. Their commander's health is their Life Points.");
        }
    }
}
