// The game's newer Inventory methods (GiveItemPermanent etc.) don't exist in older GameLibs, so the classic ones
// are used on purpose; they still work. This hides the 'obsolete' warnings for them.
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using R2API;
using R2API.ContentManagement;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace WilburMod
{
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(R2APIContentManager.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class WilburPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.ahren.wilbur";
        public const string PluginName = "Wilbur";
        public const string PluginVersion = "2.5.0";
        public const string TokenPrefix = "AHREN_WILBUR_";

        public static GameObject WilburBodyPrefab;
        public static GameObject WilburDisplayPrefab;

        public static BuffDef DevourBuff;
        public static BuffDef FeedingBuff;
        public static BuffDef BurrowBuff;
        public static BuffDef SuperBuff;
        public static BuffDef TempItemBuff;

        // Primary
        public static ConfigEntry<float> BurpDamage;
        public static ConfigEntry<float> BurpRange;
        public static ConfigEntry<float> BurpHalfAngle;
        public static ConfigEntry<float> BurpWideHalfAngle;
        public static ConfigEntry<float> BurpForce;
        public static ConfigEntry<int> StunStacks;
        public static ConfigEntry<float> GasDps;
        public static ConfigEntry<float> GasSeconds;
        public static ConfigEntry<float> GasRadius;
        public static ConfigEntry<float> GasSpeed;
        public static ConfigEntry<float> GasRange;
        // Secondary
        public static ConfigEntry<int> DevourChains;
        public static ConfigEntry<float> ChainRange;
        public static ConfigEntry<int> ChainBonusEvery;
        public static ConfigEntry<float> DevourBite;
        public static ConfigEntry<float> DevourRange;
        public static ConfigEntry<float> ChainMaxDistance;
        public static ConfigEntry<float> BiteSeconds;
        public static ConfigEntry<float> DevourCooldown;
        public static ConfigEntry<int> DevourMaxStacks;
        public static ConfigEntry<float> HeadGrowth;
        public static ConfigEntry<float> StackHealth;
        public static ConfigEntry<float> StackArmor;
        public static ConfigEntry<float> FeedArmor;
        public static ConfigEntry<int> StacksPerKill;
        public static ConfigEntry<int> StacksPerEliteKill;
        public static ConfigEntry<int> StacksPerBossKill;
        public static ConfigEntry<int> StacksPerGobble;
        public static ConfigEntry<bool> LoseHalfOnDeath;
        public static ConfigEntry<float> ExecuteBase;
        public static ConfigEntry<float> ExecutePerStack;
        public static ConfigEntry<float> ExecuteMax;
        public static ConfigEntry<float> SwallowWholeBase;
        public static ConfigEntry<float> SwallowWholePerStack;
        public static ConfigEntry<float> DevourDamagePerStack;
        // Utility
        public static ConfigEntry<float> BurrowMaxSeconds;
        public static ConfigEntry<float> BurrowMinSeconds;
        public static ConfigEntry<float> BurrowSpeed;
        public static ConfigEntry<float> BurrowDamage;
        public static ConfigEntry<float> BurrowRadius;
        public static ConfigEntry<float> BurrowHealPerHit;
        public static ConfigEntry<float> BurrowCooldown;
        public static ConfigEntry<float> DamagePerStack;
        public static ConfigEntry<bool> PiggyBankEnabled;
        public static ConfigEntry<float> CoinSmallChance;
        public static ConfigEntry<float> CoinBigChance;
        public static ConfigEntry<float> CoinJackpotChance;
        public static ConfigEntry<int> CoinSmall;
        public static ConfigEntry<int> CoinBig;
        public static ConfigEntry<int> CoinJackpot;
        public static ConfigEntry<float> Multicast2;
        public static ConfigEntry<float> Multicast3;
        public static ConfigEntry<float> Multicast4;
        public static ConfigEntry<float> MulticastPerStack;
        public static ConfigEntry<float> DoubleWindow;
        public static ConfigEntry<float> SuperStartBlast;
        public static ConfigEntry<float> SuperEndBlast;
        public static ConfigEntry<float> SuperBlastRadius;
        public static ConfigEntry<float> SuperAuraDamage;
        public static ConfigEntry<float> SuperAuraRadius;
        public static ConfigEntry<bool> PhrasesEnabled;
        public static ConfigEntry<float> PhraseCooldown;
        public static ConfigEntry<float> RainbowDps;
        public static ConfigEntry<float> RainbowRange;
        public static ConfigEntry<float> RainbowWidth;
        public static ConfigEntry<float> RainbowDrainFraction;
        public static ConfigEntry<float> RainbowDrainMin;
        public static ConfigEntry<float> SuperDrainFraction;
        public static ConfigEntry<float> SuperDrainMin;
        public static ConfigEntry<bool> CameraZoom;
        public static ConfigEntry<float> CameraZoomStrength;
        public static ConfigEntry<int> ChainMaxBites;
        public static ConfigEntry<float> ChainMaxSeconds;
        public static ConfigEntry<bool> HitRolls;
        public static ConfigEntry<float> HitPayout;
        public static ConfigEntry<float> GasSeekRange;
        public static ConfigEntry<int> GasMaxPuffs;
        public static ConfigEntry<float> BallDps;
        public static ConfigEntry<float> BallBlast;
        public static ConfigEntry<float> BallPullRadius;
        public static ConfigEntry<float> BallPullSpeed;
        public static ConfigEntry<float> BallBlastRadius;
        public static ConfigEntry<float> BallVortexSeconds;
        public static ConfigEntry<float> BallMaxFlightSeconds;
        public static ConfigEntry<float> BallCooldown;
        public static ConfigEntry<float> MaxReach;
        public static ConfigEntry<float> CameraMaxZoom;
        public static ConfigEntry<float> ItemChanceSmall;
        public static ConfigEntry<float> ItemChanceBig;
        public static ConfigEntry<float> ItemChanceJackpot;
        public static ConfigEntry<float> JackpotRedChance;
        public static ConfigEntry<float> BigRedChance;
        public static ConfigEntry<float> HitItemScale;
        public static ConfigEntry<bool> CombosEnabled;
        public static ConfigEntry<float> ComboWindow;
        public static ConfigEntry<int> ComboSpinEvery;
        public static ConfigEntry<float> ComboItemScale;
        public static ConfigEntry<float> MultiKillWindow;
        public static ConfigEntry<int> MultiKillCount;
        public static ConfigEntry<bool> InteractSpin;
        public static ConfigEntry<int> GrowthSpinEvery;
        public static ConfigEntry<float> SpeedScale;
        public static ConfigEntry<float> MaxSpeedMult;
        public static ConfigEntry<float> JumpScale;
        public static ConfigEntry<float> StompMinSize;
        public static ConfigEntry<float> StompRadius;
        public static ConfigEntry<float> StompBossDps;
        public static ConfigEntry<float> QuakeRadius;
        public static ConfigEntry<float> QuakeDps;
        public static ConfigEntry<float> QuakeSlam;
        public static ConfigEntry<float> TempWhite;
        public static ConfigEntry<float> TempGreen;
        public static ConfigEntry<float> TempRed;
        public static ConfigEntry<int> MaxTempItems;
        public static ConfigEntry<int> JackpotItems;
        // Special
        public static ConfigEntry<float> SuperDuration;
        public static ConfigEntry<float> SuperSpeed;
        public static ConfigEntry<float> SuperRam;
        public static ConfigEntry<float> SuperExtend;
        public static ConfigEntry<float> SuperCooldown;
        // Model
        public static ConfigEntry<float> PigYOffset;
        public static ConfigEntry<float> GlowStrength;
        // Sound
        public static ConfigEntry<int> SoundVolume;
        public static ConfigEntry<bool> IdleOinks;
        public static ConfigEntry<float> IdleOinkMin;
        public static ConfigEntry<float> IdleOinkMax;
        public static ConfigEntry<float> ChompInterval;
        // Polish (v2.0)
        public static ConfigEntry<float> ScreenShake;
        public static ConfigEntry<bool> LootLabels;
        public static ConfigEntry<bool> LootLabelsOnlyWilbur;
        public static ConfigEntry<float> LootLabelRange;
        public static ConfigEntry<bool> LootBeams;
        public static ConfigEntry<float> ItemEffectScale;
        public static ConfigEntry<bool> PrizeIcons;
        public static ConfigEntry<bool> GameQuotes;
        public static ConfigEntry<bool> Announcer;
        public static ConfigEntry<bool> CandyPinata;
        public static ConfigEntry<float> DigestDelay;
        public static ConfigEntry<float> DigestPercent;
        public static ConfigEntry<float> DigestMin;
        public static ConfigEntry<float> CandyAmount;
        public static BepInEx.Logging.ManualLogSource Log;

        private string SoundFolder
        {
            get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "Sounds"); }
        }

        private string IconFolder
        {
            get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "Icons"); }
        }

        private void Awake()
        {
            Log = Logger;
            SetupConfig();
            WilburAudio.Load(SoundFolder, SoundVolume.Value);
            Application.quitting += WilburAudio.Unload;
            WilburIcons.Folder = IconFolder;
            gameObject.AddComponent<WilburLootLabels>();
            RegisterLanguage();
            CreateBuffs();
            CreateBodyAndDisplay();
            RegisterStatesAndSkills();
            RegisterSurvivor();

            // Passive: every interaction (chest, shrine, ...) spins the Lucky Piggy roulette.
            GlobalEventManager.OnInteractionsGlobal += OnInteraction;

            // Feast: every kill grows Wilbur (like Cho'Gath); stacks live on the master so they survive stages.
            GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeath;
            GlobalEventManager.onServerDamageDealt += OnDamageDealt;

            Logger.LogInfo(PluginName + " " + PluginVersion + " initialized. Size per stack: " + HeadGrowth.Value + ", max stacks: " + DevourMaxStacks.Value + " (should be 0.2 and 100 with the new defaults).");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Config.Reload();
                WilburAudio.Load(SoundFolder, SoundVolume.Value);
                Logger.LogInfo("Wilbur config and sounds reloaded!");
            }
        }

        // Objects that already paid out a spin (each chest, shrine or barrel only counts once).
        private static readonly HashSet<GameObject> SpunObjects = new HashSet<GameObject>();

        // Only real "opening" interactions count: chests, shrines, terminals, drones, barrels... Item exchanges
        // (printers, scrappers, cauldrons) and picking up equipment / lunar / void items don't, otherwise
        // swapping things back and forth would spin forever.
        private static bool CountsForSpin(GameObject obj)
        {
            if (!obj) return false;
            if (obj.GetComponent<GenericPickupController>() || obj.GetComponent<PickupPickerController>()) return false;
            if (obj.GetComponent<ScrapperController>()) return false;
            string n = obj.name.ToLowerInvariant();
            if (n.Contains("duplicator") || n.Contains("scrapper") || n.Contains("cauldron") || n.Contains("printer")) return false;

            PurchaseInteraction pi = obj.GetComponent<PurchaseInteraction>();
            if (pi)
            {
                switch (pi.costType)
                {
                    case CostTypeIndex.None:
                    case CostTypeIndex.Money:
                    case CostTypeIndex.PercentHealth:
                    case CostTypeIndex.LunarCoin:
                        return true;
                    default:
                        return false; // paid with items
                }
            }
            return obj.GetComponent<BarrelInteraction>();
        }

        private static void OnInteraction(Interactor interactor, IInteractable interactable, GameObject interactableObject)
        {
            if (!NetworkServer.active || !interactor) return;
            WilburStatsController ctrl = interactor.GetComponent<WilburStatsController>();
            if (!ctrl) return;
            if (!CountsForSpin(interactableObject)) return;
            SpunObjects.RemoveWhere(o => !o);
            if (!SpunObjects.Add(interactableObject)) return;
            Vector3 pos = interactableObject ? interactableObject.transform.position : interactor.transform.position;
            ctrl.RunRoulette(pos);
            if (InteractSpin.Value) ctrl.BonusSpin(pos, 1, ComboItemScale.Value, "LUCKY TOUCH!", new Color(0.4f, 0.9f, 1f));
        }

        // Wilbur getting hit: a hurt phrase now and then.
        private static void OnDamageDealt(DamageReport report)
        {
            if (report == null || !report.victimBody || report.damageDealt <= 0f) return;
            if (report.attackerBody == report.victimBody) return;
            if (!report.victimBody.GetComponent<WilburStatsController>()) return;
            WilburPhrases.Say(report.victimBody, WilburPhrases.Kind.Hurt, 0.5f);
        }

        private static void OnCharacterDeath(DamageReport report)
        {
            if (!NetworkServer.active || report == null) return;

            // Wilbur died: lose half the feast.
            if (report.victimBody && report.victimBody.GetComponent<WilburStatsController>() && report.victimMaster)
            {
                WilburFeast feast = report.victimMaster.GetComponent<WilburFeast>();
                if (feast && LoseHalfOnDeath.Value) feast.Stacks /= 2;
                return;
            }

            CharacterBody killer = report.attackerBody;
            if (!killer || !report.victimBody) return;
            WilburStatsController ctrl = killer.GetComponent<WilburStatsController>();
            if (!ctrl) return;

            CharacterBody victim = report.victimBody;
            int gain = StacksPerKill.Value;
            if (victim.isElite) gain = Mathf.Max(gain, StacksPerEliteKill.Value);
            if (victim.isBoss || victim.isChampion) gain = Mathf.Max(gain, StacksPerBossKill.Value);
            bool gobble = WilburGobble.WasGobble(victim.healthComponent)
                || (report.damageInfo != null && report.damageInfo.inflictor && report.damageInfo.inflictor == killer.gameObject
                    && killer.HasBuff(FeedingBuff));
            if (gobble) gain += StacksPerGobble.Value;

            ctrl.AddFeast(gain, gobble);
            ctrl.PiggyBank(gobble, victim.corePosition);
            ctrl.RegisterKill(victim.corePosition);
        }

        private void SetupConfig()
        {
            BurpDamage = Config.Bind("Primary - Oink Oink", "DamageCoeff", 3f, "Shockwave damage coefficient (3 = 300%).");
            BurpRange = Config.Bind("Primary - Oink Oink", "Range", 16f, "How far the shockwave cone reaches.");
            BurpHalfAngle = Config.Bind("Primary - Oink Oink", "ConeHalfAngle", 30f, "Half-angle of the shockwave cone in degrees.");
            BurpWideHalfAngle = Config.Bind("Primary - Oink Oink", "WideConeHalfAngle", 50f, "Half-angle once Wilbur has enough Devour stacks.");
            BurpForce = Config.Bind("Primary - Oink Oink", "PushForce", 1800f, "Knockback force of the shockwave.");
            StunStacks = Config.Bind("Primary - Oink Oink", "WideAndStunAtStacks", 15, "Devour stacks needed for the bigger, stunning burp.");
            GasDps = Config.Bind("Primary - Oink Oink", "GasDamagePerSecondCoeff_v4", 1f, "Gas cloud damage per second (1.2 = 120%). Clouds do not stack on the same enemy.");
            GasSeconds = Config.Bind("Primary - Oink Oink", "GasLingerSeconds", 4f, "How long the gas cloud lingers after it arrives.");
            GasRadius = Config.Bind("Primary - Oink Oink", "GasRadius", 4.5f, "Radius of the gas cloud.");
            GasSpeed = Config.Bind("Primary - Oink Oink", "GasTravelSpeed", 22f, "How fast the cloud travels to its target.");
            GasRange = Config.Bind("Primary - Oink Oink", "GasMaxRange", 32f, "Furthest the cloud will travel.");

            DevourBite = Config.Bind("Secondary - Slopp Devour", "BiteDamageCoeff", 2.5f, "Damage of each bite in the chain (2.5 = 250%). Small or weakened enemies are gobbled instead.");
            DevourChains = Config.Bind("Secondary - Slopp Devour", "ChainBites_v5", 6, "Most bites per Devour (hold the key to keep chaining). 0 = no limit.");
            ChainRange = Config.Bind("Secondary - Slopp Devour", "ChainRange", 14f, "How far Wilbur can spring to the next enemy (grows with size).");
            ChainBonusEvery = Config.Bind("Secondary - Slopp Devour", "ExtraBiteEveryStacks", 40, "One extra chain bite per this many Feast stacks (0 = off).");
            DevourRange = Config.Bind("Secondary - Slopp Devour", "Range_v5", 35f, "How far Wilbur can pounce at the first target (grows with size).");
            ChainMaxDistance = Config.Bind("Secondary - Slopp Devour", "ChainAreaRadius_v5", 25f, "The chain stays inside this radius around where the Devour started (grows with size).");
            BiteSeconds = Config.Bind("Secondary - Slopp Devour", "LatchSeconds", 0.3f, "How long Wilbur latches on and sucks each enemy's head before jumping on.");
            DevourCooldown = Config.Bind("Secondary - Slopp Devour", "Cooldown_v5", 2f, "Cooldown in seconds.");
            DevourMaxStacks = Config.Bind("Feast Stacks", "MaxStacks_v4", 100, "Maximum Feast stacks (applies to every Wilbur, in single player and multiplayer; the host's setting counts). 0 = no limit.");
            DamagePerStack = Config.Bind("Feast Stacks", "DamagePerStack_v4", 0.006f, "Base damage bonus per stack (0.01 = +1%). Bigger pig, bigger hits.");
            PiggyBankEnabled = Config.Bind("Piggy Bank", "Enabled", true, "Every kill rolls the Piggy Bank slot for gold.");
            CoinSmallChance = Config.Bind("Piggy Bank", "SmallCoinChance", 0.38f, "Chance per roll to win a small pile of gold.");
            CoinBigChance = Config.Bind("Piggy Bank", "BigCoinChance", 0.14f, "Chance per roll to win a big pile of gold.");
            CoinJackpotChance = Config.Bind("Piggy Bank", "JackpotChance", 0.03f, "Chance per roll to hit the Piggy Bank jackpot.");
            CoinSmall = Config.Bind("Piggy Bank", "SmallCoinBase", 3, "Small win in base gold (scales with difficulty like chest prices).");
            CoinBig = Config.Bind("Piggy Bank", "BigCoinBase", 8, "Big win in base gold.");
            CoinJackpot = Config.Bind("Piggy Bank", "JackpotBase", 25, "Jackpot in base gold.");
            Multicast2 = Config.Bind("Piggy Bank", "GobbleMulticast2Chance_v4", 0.3f, "Gobble kills: chance to roll twice (like Ogre Magi's Multicast).");
            Multicast3 = Config.Bind("Piggy Bank", "GobbleMulticast3Chance_v4", 0.15f, "After a x2, chance it becomes x3.");
            Multicast4 = Config.Bind("Piggy Bank", "GobbleMulticast4Chance_v4", 0.08f, "After a x3, chance it becomes x4.");
            MulticastPerStack = Config.Bind("Piggy Bank", "MulticastPerStack", 0.002f, "Each Feast stack adds this to every multicast chance (bigger pig, more rolls).");
            DoubleWindow = Config.Bind("Lucky Piggy", "JackpotDoubleWindowSeconds", 30f, "After a Jackpot, the next item you pick up within this many seconds is doubled.");
            PhrasesEnabled = Config.Bind("Phrases", "Enabled", true, "Cartoon pig phrases pop out of Wilbur when he attacks, gets hit or gobbles.");
            PhraseCooldown = Config.Bind("Phrases", "CooldownSeconds", 2.5f, "Shortest time between phrases.");
            RainbowDps = Config.Bind("Special - Super Piggy", "RainbowDamagePerSecondCoeff_v4", 8f, "Rainbow beam damage per second to everything it touches (15 = 1500%).");
            RainbowRange = Config.Bind("Special - Super Piggy", "RainbowRange_v4", 60f, "How far the rainbow beam reaches.");
            RainbowWidth = Config.Bind("Special - Super Piggy", "RainbowWidth", 1.2f, "Hit radius of the beam at normal size (grows with size).");
            RainbowDrainFraction = Config.Bind("Special - Super Piggy", "RainbowDeflatePerSecond", 0.05f, "Share of your Feast stacks lost per second while firing the rainbow (0.05 = 5%).");
            RainbowDrainMin = Config.Bind("Special - Super Piggy", "RainbowDeflateMinPerSecond", 2f, "Minimum stacks lost per second while firing the rainbow.");
            SuperDrainFraction = Config.Bind("Special - Super Piggy", "DeflatePerSecond", 0.02f, "Share of your Feast stacks lost per second just from being Super Piggy (0.02 = 2%).");
            SuperDrainMin = Config.Bind("Special - Super Piggy", "DeflateMinPerSecond", 0.5f, "Minimum stacks lost per second while Super Piggy.");
            CameraZoom = Config.Bind("Model", "CameraZoomsOutWithSize", true, "Pull the camera back as Wilbur grows.");
            CameraZoomStrength = Config.Bind("Model", "CameraZoomStrength", 0.6f, "How much the camera pulls back per unit of size (0.6 = at 3x size the camera is 2.2x further).");
            ChainMaxBites = Config.Bind("Secondary - Slopp Devour", "ChainHardCap", 0, "Absolute bite cap. 0 = none.");
            ChainMaxSeconds = Config.Bind("Secondary - Slopp Devour", "ChainMaxSeconds", 0f, "Longest one chain can last. 0 = no limit.");
            HitRolls = Config.Bind("Piggy Bank", "RollOnHits", true, "Devour bites, sonic wave hits and gassed enemies each spin the Piggy Bank.");
            HitPayout = Config.Bind("Piggy Bank", "HitPayoutMultiplier_v4", 0.3f, "Gold from hit spins compared to kill spins (0.5 = half).");
            GasSeekRange = Config.Bind("Primary - Oink Oink", "GasSeekRange", 30f, "The burp cloud splits into puffs that home in on enemies within this range.");
            GasMaxPuffs = Config.Bind("Primary - Oink Oink", "GasMaxPuffs", 8, "Most homing gas puffs per burp.");
            BallDps = Config.Bind("Special - Super Piggy", "RainbowBallDamagePerSecondCoeff_v4", 4f, "Rainbow ball vortex damage per second to everything it pulls (6 = 600%).");
            BallBlast = Config.Bind("Special - Super Piggy", "RainbowBallBlastCoeff_v4", 15f, "Rainbow ball final explosion damage (30 = 3000%).");
            BallPullRadius = Config.Bind("Special - Super Piggy", "RainbowBallPullRadius_v2", 25f, "How far the vortex sucks enemies in. The ball is as big as Wilbur and its reach grows with it.");
            BallPullSpeed = Config.Bind("Special - Super Piggy", "RainbowBallPullSpeed", 30f, "How hard the vortex pulls.");
            BallBlastRadius = Config.Bind("Special - Super Piggy", "RainbowBallBlastRadius_v2", 18f, "Explosion radius. Grows with the ball, which is as big as Wilbur.");
            BallVortexSeconds = Config.Bind("Special - Super Piggy", "RainbowBallVortexSeconds", 2.5f, "How long the vortex sucks before it explodes.");
            BallMaxFlightSeconds = Config.Bind("Special - Super Piggy", "RainbowBallFlightSeconds", 1.2f, "If the ball hits nothing, it turns into a vortex after this long.");
            BallCooldown = Config.Bind("Special - Super Piggy", "RainbowBallCooldown", 2.5f, "Seconds between rainbow balls while Super.");
            MaxReach = Config.Bind("Feast Stacks", "MaxEffectRadius_v4", 60f, "Cap on how big area attacks get as Wilbur grows, so a mountain-sized pig doesn't hit the whole map.");
            CameraMaxZoom = Config.Bind("Model", "CameraMaxZoom_v2", 45f, "Furthest the camera pulls back (45 = 45x normal distance).");
            ItemChanceSmall = Config.Bind("Piggy Bank", "ItemChanceSmallWin_v4", 0.06f, "Chance a small win also gives a temporary item (mostly white).");
            ItemChanceBig = Config.Bind("Piggy Bank", "ItemChanceBigWin_v4", 0.2f, "Chance a big win also gives a temporary item (white or green, sometimes red).");
            ItemChanceJackpot = Config.Bind("Piggy Bank", "ItemChanceJackpot_v4", 0.6f, "Chance a jackpot gives temporary items (1 = always).");
            JackpotRedChance = Config.Bind("Piggy Bank", "JackpotRedChance_v4", 0.2f, "When a jackpot gives an item, chance it is red.");
            BigRedChance = Config.Bind("Piggy Bank", "BigWinRedChance_v4", 0.04f, "When a big win gives an item, chance it is red.");
            HitItemScale = Config.Bind("Piggy Bank", "HitSpinItemMultiplier_v4", 0.25f, "Item chances on hit spins compared to kill spins.");
            CombosEnabled = Config.Bind("Piggy Bank", "Combos", true, "Hit enemies over and over without stopping to build a combo that pays bonus spins.");
            ComboWindow = Config.Bind("Piggy Bank", "ComboWindowSeconds", 3f, "The combo breaks if you go this long without hitting anything.");
            ComboSpinEvery = Config.Bind("Piggy Bank", "ComboSpinEveryHits_v4", 30, "A bonus spin every this many hits in a combo (better at 40+ and 100+).");
            ComboItemScale = Config.Bind("Piggy Bank", "BonusSpinItemMultiplier_v4", 1f, "Item chances on combo / multi-kill / interaction / growth spins.");
            MultiKillWindow = Config.Bind("Piggy Bank", "MultiKillWindowSeconds", 1.5f, "Kills this close together count as a multi-kill.");
            MultiKillCount = Config.Bind("Piggy Bank", "MultiKillCount", 3, "Kills needed for a multi-kill spin.");
            InteractSpin = Config.Bind("Piggy Bank", "SpinOnInteract", true, "Opening chests, barrels, shrines, terminals and the like also spins the Piggy Bank.");
            SpeedScale = Config.Bind("Feast Stacks", "SpeedScale_v4", 0.5f, "How much move speed grows with size (1 = speed x sqrt(size): 25x size runs 5x faster).");
            MaxSpeedMult = Config.Bind("Feast Stacks", "MaxSpeedMultiplier_v4", 2.5f, "Cap on the size speed bonus.");
            JumpScale = Config.Bind("Feast Stacks", "JumpScale", 0.5f, "How much jump height grows with size.");
            StompMinSize = Config.Bind("Feast Stacks", "StompStartsAtSize_v4", 15f, "Wilbur starts stomping enemies under him at this size (20 = 20x).");
            StompRadius = Config.Bind("Feast Stacks", "StompRadiusPerSize", 0.45f, "Stomp radius per unit of size (capped by MaxEffectRadius).");
            StompBossDps = Config.Bind("Feast Stacks", "StompBossDamagePerSecondCoeff_v4", 4f, "Bosses can't be squashed outright; they take this much per second under your feet (8 = 800%).");
            QuakeRadius = Config.Bind("Utility - Truffle Burrow", "EarthquakeRadius_v2", 20f, "Earthquake radius while burrowed (grows with size).");
            QuakeDps = Config.Bind("Utility - Truffle Burrow", "EarthquakeDamagePerSecondCoeff_v4", 2f, "Earthquake damage per second (3 = 300%).");
            QuakeSlam = Config.Bind("Utility - Truffle Burrow", "EarthquakeSlamSpeed", 40f, "How hard flying enemies get slammed to the ground.");
            TempWhite = Config.Bind("Piggy Bank", "WhitePrizeSeconds", 20f, "How long a white prize item lasts.");
            TempGreen = Config.Bind("Piggy Bank", "GreenPrizeSeconds", 30f, "How long a green prize item lasts.");
            TempRed = Config.Bind("Piggy Bank", "RedPrizeSeconds", 45f, "How long a red prize item lasts.");
            MaxTempItems = Config.Bind("Piggy Bank", "MaxPrizeItemsAtOnce_v4", 10, "Most temporary prize items you can hold at once.");
            JackpotItems = Config.Bind("Piggy Bank", "ItemsPerJackpot_v4", 1, "How many temporary items a jackpot gives.");
            GrowthSpinEvery = Config.Bind("Piggy Bank", "GrowthSpinEveryStacks", 25, "A boosted spin every time your Feast passes another multiple of this.");
            StacksPerKill = Config.Bind("Feast Stacks", "StacksPerKill_v4", 1, "Stacks gained for any kill.");
            StacksPerEliteKill = Config.Bind("Feast Stacks", "StacksPerEliteKill_v4", 3, "Stacks gained for killing an elite.");
            StacksPerBossKill = Config.Bind("Feast Stacks", "StacksPerBossKill_v4", 8, "Stacks gained for killing a boss or champion.");
            StacksPerGobble = Config.Bind("Feast Stacks", "BonusStacksForDevourKill_v4", 1, "Extra stacks when the kill comes from Slopp Devour.");
            LoseHalfOnDeath = Config.Bind("Feast Stacks", "LoseHalfOnDeath", true, "Lose half your stacks when you die.");
            HeadGrowth = Config.Bind("Feast Stacks", "SizePerStack_v4", 0.2f, "Size added per stack (0.2 = +20%; 50 stacks = 11x boss size, 100 stacks = 21x).");
            StackHealth = Config.Bind("Feast Stacks", "MaxHealthPerStack_v4", 0.01f, "Max health bonus per stack (0.02 = +2%).");
            StackArmor = Config.Bind("Feast Stacks", "ArmorPerStack_v4", 0.25f, "Armor per stack.");
            DevourDamagePerStack = Config.Bind("Feast Stacks", "DevourDamagePerStack_v4", 0.01f, "Slopp Devour damage bonus per stack (0.02 = +2%).");
            ExecuteBase = Config.Bind("Feast Stacks", "ExecuteThresholdBase", 0.3f, "Devour's bite executes non-bosses below this health fraction at 0 stacks.");
            ExecutePerStack = Config.Bind("Feast Stacks", "ExecuteThresholdPerStack_v4", 0.002f, "Execute threshold added per stack.");
            ExecuteMax = Config.Bind("Feast Stacks", "ExecuteThresholdMax_v4", 0.5f, "Highest the execute threshold can go.");
            SwallowWholeBase = Config.Bind("Feast Stacks", "SwallowWholeBase", 0.25f, "Non-bosses with max health below this fraction of YOUR max health are swallowed instantly.");
            SwallowWholePerStack = Config.Bind("Feast Stacks", "SwallowWholePerStack_v4", 0.005f, "Swallow-whole fraction added per stack.");
            FeedArmor = Config.Bind("Feast Stacks", "FeedingBonusArmor", 50f, "Bonus armor while Wilbur is feeding.");

            BurrowMaxSeconds = Config.Bind("Utility - Truffle Burrow", "MaxSeconds", 2f, "Longest you can stay underground. Let go of the key to pop up sooner.");
            BurrowMinSeconds = Config.Bind("Utility - Truffle Burrow", "MinSeconds", 0.3f, "Shortest time underground.");
            BurrowSpeed = Config.Bind("Utility - Truffle Burrow", "SpeedMultiplier", 1.8f, "Speed underground as a multiple of move speed.");
            BurrowDamage = Config.Bind("Utility - Truffle Burrow", "EruptDamageCoeff_v4", 3.5f, "Damage when bursting out of the ground (4 = 400%).");
            BurrowRadius = Config.Bind("Utility - Truffle Burrow", "EruptRadius", 6f, "Radius of the eruption at normal size (grows with Feast).");
            BurrowHealPerHit = Config.Bind("Utility - Truffle Burrow", "HealPerEnemyHit", 0.03f, "Max health healed per enemy hit by the eruption (0.03 = 3%).");
            BurrowCooldown = Config.Bind("Utility - Truffle Burrow", "Cooldown", 7f, "Cooldown in seconds.");

            SuperDuration = Config.Bind("Special - Super Piggy", "Duration_v4", 8f, "Longest Super Piggy lasts. Press the key again to end it early.");
            SuperSpeed = Config.Bind("Special - Super Piggy", "SpeedMultiplier", 2.5f, "Flight speed as a multiple of move speed.");
            SuperRam = Config.Bind("Special - Super Piggy", "RamDamageCoeff_v4", 6f, "Ramming damage coefficient (10 = 1000%). Small enemies are gobbled on contact.");
            SuperStartBlast = Config.Bind("Special - Super Piggy", "TakeoffBlastCoeff_v4", 3f, "Shockwave damage when Super Piggy starts (5 = 500%).");
            SuperEndBlast = Config.Bind("Special - Super Piggy", "HamSlamCoeff_v4", 5f, "HAM SLAM damage when Super Piggy ends (8 = 800%).");
            SuperBlastRadius = Config.Bind("Special - Super Piggy", "BlastRadius", 14f, "Radius of the takeoff blast (Ham Slam is 20% bigger). Grows with size.");
            SuperAuraDamage = Config.Bind("Special - Super Piggy", "AuraDamageCoeff_v4", 1f, "Golden aura damage every 0.5s while flying (1.5 = 150%).");
            SuperAuraRadius = Config.Bind("Special - Super Piggy", "AuraRadius", 7f, "Golden aura radius. Grows with size.");
            SuperExtend = Config.Bind("Special - Super Piggy", "ExtendSeconds", 3f, "Seconds added by opening a chest or finishing a feed.");
            SuperCooldown = Config.Bind("Special - Super Piggy", "Cooldown_v4", 30f, "Cooldown in seconds.");

            PigYOffset = Config.Bind("Model", "PigYOffset", 0f, "Raise or lower the pig model if he floats or sinks into the ground.");
            GlowStrength = Config.Bind("Model", "GlowStrength", 1f, "Multiplier for all of Wilbur's glow effects. Lower it if the screen gets washed out.");

            SoundVolume = Config.Bind("Sound", "Volume", 70, "Volume of Wilbur's sounds, 0-100. These play outside the game's volume sliders.");
            IdleOinks = Config.Bind("Sound", "IdleOinks", true, "Wilbur oinks every now and then on his own.");
            IdleOinkMin = Config.Bind("Sound", "IdleOinkMinSeconds", 6f, "Shortest time between idle oinks.");
            IdleOinkMax = Config.Bind("Sound", "IdleOinkMaxSeconds", 14f, "Longest time between idle oinks.");
            ChompInterval = Config.Bind("Sound", "ChompSoundInterval", 0.45f, "Seconds between chomp sounds while feeding.");

            ScreenShake = Config.Bind("Polish", "ScreenShake", 1f, "Screen shake strength for stomps, HAM SLAM, eruptions and rainbow booms (0 = off).");
            LootLabels = Config.Bind("Polish", "LootLabels", true, "Show floating names and prices over chests, shrines, shops, drones, items on the ground and the teleporter.");
            LootLabelsOnlyWilbur = Config.Bind("Polish", "LootLabelsOnlyAsWilbur", true, "Only show loot labels while you are playing Wilbur.");
            LootLabelRange = Config.Bind("Polish", "LootLabelRange", 220f, "How far away loot labels show (grows with Wilbur's size).");
            LootBeams = Config.Bind("Polish", "LootBeams", true, "A coloured light pillar over items on the ground, taller the bigger Wilbur is.");
            ItemEffectScale = Config.Bind("Polish", "ItemEffectScale", 1f, "How much item auras grow with Wilbur (Bustling Fungus ring, Frost Relic, buff visuals). 1 = match his size, 0 = off.");
            PrizeIcons = Config.Bind("Polish", "PrizeIcons", true, "Piggy Bank prizes pop out of Wilbur as the item's icon.");
            GameQuotes = Config.Bind("Polish", "GameQuotes", true, "Pig parodies of famous video game lines in Wilbur's phrases.");
            Announcer = Config.Bind("Polish", "Announcer", true, "Halo-style multi-kill calls, killing sprees and combo ranks.");
            DigestDelay = Config.Bind("Feast Stacks", "DigestDelaySeconds_v4", 5f, "Feast stacks start to slowly digest away after this many seconds without eating (kills, gobbles). 0 = never.");
            DigestPercent = Config.Bind("Feast Stacks", "DigestPercentPerSecond_v4", 0.015f, "Share of your stacks lost per second while digesting (0.015 = 1.5%/s).");
            DigestMin = Config.Bind("Feast Stacks", "DigestMinPerSecond_v4", 0.3f, "Minimum stacks lost per second while digesting.");
            CandyPinata = Config.Bind("Polish", "CandyPinata", true, "Wilbur bursts candy like a pinata when he gets hit and when he deflates.");
            CandyAmount = Config.Bind("Polish", "CandyAmount", 1f, "Multiplier for how much candy comes out (0.5 = half, 2 = double).");
        }

        private void RegisterLanguage()
        {
            LanguageAPI.Add(TokenPrefix + "NAME", "Wilbur");
            LanguageAPI.Add(TokenPrefix + "SUBTITLE", "Hungry Hero");
            LanguageAPI.Add(TokenPrefix + "DESCRIPTION", "Wilbur is a scrappy little pig with a big appetite and a bigger burp.<style=cSub>\r\n\r\n" +
                " ! > Oink Oink is a sonic burp followed by a lingering green gas cloud.\r\n" +
                " ! > Slopp Devour chains bites from enemy to enemy. Every kill makes him bigger and pays out gold.\r\n" +
                " ! > Every chest or shrine you open spins the Lucky Piggy roulette.\r\n" +
                " ! > Super Piggy turns Wilbur gold and lets him fly, invulnerable.</style>");
            LanguageAPI.Add(TokenPrefix + "SKIN_DEFAULT", "Default");
            LanguageAPI.Add(TokenPrefix + "BUFF_PRIZES", "Piggy Bank Prizes");
            LanguageAPI.Add(TokenPrefix + "OUTRO", "..and so he left, well fed and heavier than ever.");
            LanguageAPI.Add(TokenPrefix + "FAIL", "..and so he was served with an apple in his mouth.");

            LanguageAPI.Add(TokenPrefix + "PASSIVE_NAME", "Piggy Bank");
            LanguageAPI.Add(TokenPrefix + "PASSIVE_DESC", "<style=cIsUtility>Piggy Bank:</style> every kill rolls a slot for <style=cShrine>gold</style> and a chance at <style=cIsHealth>temporary items, even red ones</style>. Bonus spins for <style=cIsUtility>combos, multi-kills, opening anything and growing</style>; gobble kills can <style=cIsUtility>multicast</style> x2, x3 or x4. <style=cIsDamage>Feast:</style> every kill makes Wilbur bigger with no limit: +" + (DamagePerStack.Value * 100f).ToString("0") + "% damage, +" + (StackHealth.Value * 100f).ToString("0") + "% max health and +" + StackArmor.Value.ToString("0.#") + " armor per stack, up to " + DevourMaxStacks.Value + " stacks. <style=cDeath>Stop eating and the stacks slowly digest away.</style>. <style=cIsUtility>Lucky Piggy:</style> opening chests or shrines spins a slot: <style=cIsUtility>Jackpot</style> (next item x2, +0.5 Luck), <style=cIsUtility>Truffle Rush</style>, <style=cIsHealing>Pig Out</style> or <style=cDeath>Indigestion</style>.");

            LanguageAPI.Add(TokenPrefix + "PRIMARY_NAME", "Oink Oink");
            LanguageAPI.Add(TokenPrefix + "PRIMARY_DESC", "Burp a sonic shockwave in a cone for <style=cIsDamage>" + (BurpDamage.Value * 100f).ToString("0") + "% damage</style> that shoves enemies back. Then a <style=cIsDamage>green gas cloud</style> splits into puffs that <style=cIsDamage>home in on every enemy nearby</style> and cling to them for " + GasSeconds.Value.ToString("0") + "s at <style=cIsDamage>" + (GasDps.Value * 100f).ToString("0") + "% damage per second</style>. Every hit spins the <style=cIsUtility>Piggy Bank</style>. During Super Piggy, hold to fire a <style=cIsDamage>rainbow</style> instead.");

            LanguageAPI.Add(TokenPrefix + "SECONDARY_NAME", "Slopp Devour");
            LanguageAPI.Add(TokenPrefix + "SECONDARY_DESC", "Pounce onto an enemy's head and suck on it, then spring to the next one nearby while you hold the key (up to " + DevourChains.Value + " bites), biting for <style=cIsDamage>" + (DevourBite.Value * 100f).ToString("0") + "% damage</style>. Let go or jump to stop. Every bite spins the <style=cIsUtility>Piggy Bank</style>; small or weakened enemies are <style=cIsDamage>gobbled</style>. During Super Piggy, throws a <style=cIsDamage>rainbow ball</style> that sucks enemies in and explodes for " + (BallBlast.Value * 100f).ToString("0") + "%.");

            LanguageAPI.Add(TokenPrefix + "UTILITY_NAME", "Truffle Burrow");
            LanguageAPI.Add(TokenPrefix + "UTILITY_DESC", "Dig underground, <style=cIsUtility>invulnerable</style>, setting off an <style=cIsDamage>earthquake</style> that hits everything around you for " + (QuakeDps.Value * 100f).ToString("0") + "% per second and slams flying enemies to the ground, and tunnel at <style=cIsUtility>" + (BurrowSpeed.Value * 100f).ToString("0") + "% speed</style> for up to " + BurrowMaxSeconds.Value.ToString("0.#") + "s. Let go to burst out for <style=cIsDamage>" + (BurrowDamage.Value * 100f).ToString("0") + "% damage</style>, launching enemies and <style=cIsDamage>gobbling</style> small or weakened ones. Heal <style=cIsHealing>" + (BurrowHealPerHit.Value * 100f).ToString("0") + "%</style> per enemy hit.");

            LanguageAPI.Add(TokenPrefix + "SPECIAL_NAME", "Super Piggy");
            LanguageAPI.Add(TokenPrefix + "SPECIAL_DESC", "Turn radiant gold with a <style=cIsDamage>" + (SuperStartBlast.Value * 100f).ToString("0") + "% shockwave</style>. You are <style=cIsHealing>invulnerable</style> and fly. <style=cIsDamage>Left click</style>: continuous rainbow laser, " + (RainbowDps.Value * 100f).ToString("0") + "% per second. <style=cIsDamage>Right click</style>: PUKE a rainbow ball as big as you are that sucks enemies in and explodes for " + (BallBlast.Value * 100f).ToString("0") + "%. <style=cDeath>It costs ALL your Feast stacks.</style> <style=cDeath>You deflate while Super, faster while firing.</style> Press again to end it with a <style=cIsDamage>" + (SuperEndBlast.Value * 100f).ToString("0") + "% HAM SLAM</style>.");
        }

        private static BuffDef MakeBuff(string name, Color color, bool canStack, bool hidden, string icon = null)
        {
            BuffDef def = ScriptableObject.CreateInstance<BuffDef>();
            ((ScriptableObject)def).name = name;
            def.buffColor = color;
            def.canStack = canStack;
            def.isDebuff = false;
            def.isHidden = hidden;
            def.iconSprite = icon != null ? WilburIcons.Get(icon, MakeIcon(color, Color.white)) : MakeIcon(color, Color.white);
            ContentAddition.AddBuffDef(def);
            return def;
        }

        private void CreateBuffs()
        {
            DevourBuff = MakeBuff(TokenPrefix + "Feast", new Color(1f, 0.55f, 0.65f), true, false, "buff_feast");
            FeedingBuff = MakeBuff(TokenPrefix + "Feeding", new Color(0.9f, 0.3f, 0.3f), false, true);
            BurrowBuff = MakeBuff(TokenPrefix + "Burrow", new Color(0.5f, 0.35f, 0.2f), false, true);
            SuperBuff = MakeBuff(TokenPrefix + "Super", new Color(1f, 0.85f, 0.2f), false, false, "buff_super");
            TempItemBuff = MakeBuff(TokenPrefix + "Prizes", new Color(1f, 0.85f, 0.15f), true, false, "buff_prizes");
        }

        // Removes a primitive's collider safely even inside physics callbacks (DestroyImmediate is not allowed there).
        public static void StripCollider(GameObject go)
        {
            if (!go) return;
            Collider c = go.GetComponent<Collider>();
            if (!c) return;
            c.enabled = false;
            Object.Destroy(c);
        }

        public static Material MakeMat()
        {
            Shader sh = Shader.Find("Hopoo Games/Deferred/Standard");
            if (!sh) sh = Shader.Find("Standard");
            if (!sh) sh = Shader.Find("Sprites/Default");
            return new Material(sh);
        }

        // Glow is kept low on purpose: strong emissive colours blow the screen out to white through the game's bloom.
        public static void Tint(Material m, Color main, Color emission, float power)
        {
            float glow = GlowStrength != null ? Mathf.Max(0f, GlowStrength.Value) : 1f;
            power *= glow;
            m.color = main;
            if (m.HasProperty("_Color")) m.SetColor("_Color", main);
            if (m.HasProperty("_EmColor")) m.SetColor("_EmColor", emission);
            if (m.HasProperty("_EmPower")) m.SetFloat("_EmPower", power);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission * power);
        }

        private static GameObject MakePart(GameObject root, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = pos;
            part.transform.localScale = scale;
            part.transform.localEulerAngles = euler;
            part.GetComponent<Renderer>().sharedMaterial = mat;
            return part;
        }

        private static Material PigMat(Color main, Color glow, float power)
        {
            Material m = MakeMat();
            Tint(m, main, glow, power);
            return m;
        }

        // Wilbur himself: a chubby, big-headed cartoon pig built from primitives (generated from pig_spec.py).
        // Parts named "Pig_*" turn gold during Super Piggy. Feet are at y = 0, nose points along +Z.
        private static GameObject BuildPig()
        {
            GameObject root = new GameObject("WilburPig");
            Material pink = PigMat(new Color(1f, 0.67f, 0.75f), new Color(0.3f, 0.12f, 0.15f), 0.25f);
            Material belly = PigMat(new Color(1f, 0.8f, 0.85f), new Color(0.3f, 0.15f, 0.18f), 0.25f);
            Material snout = PigMat(new Color(0.97f, 0.52f, 0.64f), new Color(0.25f, 0.08f, 0.12f), 0.25f);
            Material earInner = PigMat(new Color(0.92f, 0.42f, 0.56f), new Color(0.2f, 0.06f, 0.1f), 0.2f);
            Material blush = PigMat(new Color(1f, 0.42f, 0.52f), new Color(0.35f, 0.06f, 0.1f), 0.25f);
            Material hoof = PigMat(new Color(0.3f, 0.18f, 0.14f), new Color(0f, 0f, 0f), 0f);
            Material eyeWhite = PigMat(new Color(0.97f, 0.97f, 0.97f), new Color(0.25f, 0.25f, 0.25f), 0.3f);
            Material pupil = PigMat(new Color(0.04f, 0.03f, 0.05f), new Color(0f, 0f, 0f), 0f);
            Material shine = PigMat(new Color(1f, 1f, 1f), new Color(1f, 1f, 1f), 0.5f);
            Material brow = PigMat(new Color(0.42f, 0.22f, 0.2f), new Color(0f, 0f, 0f), 0f);
            Material mouth = PigMat(new Color(0.35f, 0.08f, 0.12f), new Color(0f, 0f, 0f), 0f);
            Material tongue = PigMat(new Color(1f, 0.45f, 0.55f), new Color(0.3f, 0.05f, 0.1f), 0.2f);
            Material spark = PigMat(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.7f, 0.1f), 0.6f);
            Material snoutShine = PigMat(new Color(1f, 0.8f, 0.86f), new Color(0.4f, 0.2f, 0.25f), 0.3f);
            Material lid = PigMat(new Color(0.98f, 0.6f, 0.7f), new Color(0.3f, 0.1f, 0.14f), 0.25f);
            Material mouthIn = PigMat(new Color(0.45f, 0.08f, 0.14f), new Color(0f, 0f, 0f), 0f);
            Dictionary<string, GameObject> piv = new Dictionary<string, GameObject>();
            piv[""] = root;
            piv["HeadPivot"] = MakePivot(piv[""], "HeadPivot", new Vector3(0f, 0.84f, 0.5f), new Vector3(0f, 0f, 0f));
            piv["Eyes"] = MakePivot(piv["HeadPivot"], "Eyes", new Vector3(0f, 0.14f, 0.4f), new Vector3(0f, 0f, 0f));
            piv["Lids"] = MakePivot(piv["HeadPivot"], "Lids", new Vector3(0f, 0.14f, 0.4f), new Vector3(0f, 0f, 0f));
            piv["Brows"] = MakePivot(piv["HeadPivot"], "Brows", new Vector3(0f, 0.29f, 0.41f), new Vector3(0f, 0f, 0f));
            piv["EarL"] = MakePivot(piv["HeadPivot"], "EarL", new Vector3(-0.22f, 0.31f, 0.06f), new Vector3(25f, 0f, 30f));
            piv["EarR"] = MakePivot(piv["HeadPivot"], "EarR", new Vector3(0.22f, 0.31f, 0.06f), new Vector3(25f, 0f, -30f));
            piv["Pig_Tail"] = MakePivot(piv[""], "Pig_Tail", new Vector3(0f, 0.8f, -0.64f), new Vector3(0f, 0f, 0f));
            piv["LegFL"] = MakePivot(piv[""], "LegFL", new Vector3(-0.25f, 0.4f, 0.3f), new Vector3(0f, 0f, 0f));
            piv["LegFR"] = MakePivot(piv[""], "LegFR", new Vector3(0.25f, 0.4f, 0.3f), new Vector3(0f, 0f, 0f));
            piv["LegBL"] = MakePivot(piv[""], "LegBL", new Vector3(-0.25f, 0.4f, -0.3f), new Vector3(0f, 0f, 0f));
            piv["LegBR"] = MakePivot(piv[""], "LegBR", new Vector3(0.25f, 0.4f, -0.3f), new Vector3(0f, 0f, 0f));
            MakePart(piv[""], "Pig_Body", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0f), new Vector3(0.9f, 0.76f, 1.05f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_Rump", PrimitiveType.Sphere, new Vector3(0f, 0.64f, -0.26f), new Vector3(0.86f, 0.74f, 0.68f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_Chest", PrimitiveType.Sphere, new Vector3(0f, 0.63f, 0.24f), new Vector3(0.82f, 0.72f, 0.66f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_Belly", PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0.04f), new Vector3(0.76f, 0.5f, 0.88f), Vector3.zero, belly);
            MakePart(piv[""], "Pig_Neck", PrimitiveType.Sphere, new Vector3(0f, 0.74f, 0.42f), new Vector3(0.62f, 0.56f, 0.42f), Vector3.zero, pink);
            MakePart(piv[""], "Spot0", PrimitiveType.Sphere, new Vector3(0.12f, 0.98f, -0.22f), new Vector3(0.16f, 0.04f, 0.14f), new Vector3(0f, 25f, 0f), belly);
            MakePart(piv[""], "Spot1", PrimitiveType.Sphere, new Vector3(-0.16f, 0.95f, 0.02f), new Vector3(0.1f, 0.035f, 0.09f), Vector3.zero, belly);
            MakePart(piv[""], "Pig_HamL", PrimitiveType.Sphere, new Vector3(-0.27f, 0.5f, -0.3f), new Vector3(0.32f, 0.4f, 0.42f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_ShoulderL", PrimitiveType.Sphere, new Vector3(-0.27f, 0.52f, 0.3f), new Vector3(0.3f, 0.36f, 0.36f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_HamR", PrimitiveType.Sphere, new Vector3(0.27f, 0.5f, -0.3f), new Vector3(0.32f, 0.4f, 0.42f), Vector3.zero, pink);
            MakePart(piv[""], "Pig_ShoulderR", PrimitiveType.Sphere, new Vector3(0.27f, 0.52f, 0.3f), new Vector3(0.3f, 0.36f, 0.36f), Vector3.zero, pink);
            MakePart(piv["HeadPivot"], "Pig_Head", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.12f), new Vector3(0.74f, 0.68f, 0.66f), Vector3.zero, pink);
            MakePart(piv["HeadPivot"], "Pig_Jowls", PrimitiveType.Sphere, new Vector3(0f, -0.12f, 0.18f), new Vector3(0.6f, 0.38f, 0.52f), Vector3.zero, pink);
            MakePart(piv["HeadPivot"], "Pig_SnoutBridge", PrimitiveType.Sphere, new Vector3(0f, -0.02f, 0.34f), new Vector3(0.26f, 0.2f, 0.22f), Vector3.zero, pink);
            MakePart(piv["HeadPivot"], "Pig_Snout", PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0.44f), new Vector3(0.3f, 0.08f, 0.22f), new Vector3(90f, 0f, 0f), snout);
            MakePart(piv["HeadPivot"], "SnoutShine", PrimitiveType.Sphere, new Vector3(0.05f, 0.02f, 0.5f), new Vector3(0.08f, 0.04f, 0.03f), new Vector3(0f, 0f, 20f), snoutShine);
            MakePart(piv["HeadPivot"], "MouthOpen", PrimitiveType.Sphere, new Vector3(0f, -0.215f, 0.43f), new Vector3(0.16f, 0.12f, 0.06f), Vector3.zero, mouthIn);
            MakePart(piv["HeadPivot"], "NostrilL", PrimitiveType.Sphere, new Vector3(-0.055f, -0.05f, 0.525f), new Vector3(0.05f, 0.07f, 0.03f), Vector3.zero, pupil);
            MakePart(piv["HeadPivot"], "NostrilR", PrimitiveType.Sphere, new Vector3(0.055f, -0.05f, 0.525f), new Vector3(0.05f, 0.07f, 0.03f), Vector3.zero, pupil);
            MakePart(piv["HeadPivot"], "MouthL", PrimitiveType.Sphere, new Vector3(-0.07f, -0.185f, 0.435f), new Vector3(0.07f, 0.025f, 0.03f), new Vector3(0f, 0f, 28f), mouth);
            MakePart(piv["HeadPivot"], "MouthM", PrimitiveType.Sphere, new Vector3(0f, -0.205f, 0.45f), new Vector3(0.09f, 0.03f, 0.03f), Vector3.zero, mouth);
            MakePart(piv["HeadPivot"], "MouthR", PrimitiveType.Sphere, new Vector3(0.07f, -0.185f, 0.435f), new Vector3(0.07f, 0.025f, 0.03f), new Vector3(0f, 0f, -28f), mouth);
            MakePart(piv["HeadPivot"], "Tongue", PrimitiveType.Sphere, new Vector3(0.015f, -0.218f, 0.452f), new Vector3(0.05f, 0.03f, 0.025f), Vector3.zero, tongue);
            MakePart(piv["HeadPivot"], "BlushL", PrimitiveType.Sphere, new Vector3(-0.24f, -0.08f, 0.36f), new Vector3(0.14f, 0.08f, 0.05f), Vector3.zero, blush);
            MakePart(piv["HeadPivot"], "BlushR", PrimitiveType.Sphere, new Vector3(0.24f, -0.08f, 0.36f), new Vector3(0.14f, 0.08f, 0.05f), Vector3.zero, blush);
            MakePart(piv["Eyes"], "EyeWhiteL", PrimitiveType.Sphere, new Vector3(-0.14f, 0f, 0f), new Vector3(0.15f, 0.17f, 0.1f), Vector3.zero, eyeWhite);
            MakePart(piv["Eyes"], "PupilL", PrimitiveType.Sphere, new Vector3(-0.135f, -0.01f, 0.045f), new Vector3(0.085f, 0.1f, 0.05f), Vector3.zero, pupil);
            MakePart(piv["Eyes"], "ShineL", PrimitiveType.Sphere, new Vector3(-0.113f, 0.025f, 0.07f), new Vector3(0.035f, 0.035f, 0.015f), Vector3.zero, shine);
            MakePart(piv["Eyes"], "EyeWhiteR", PrimitiveType.Sphere, new Vector3(0.14f, 0f, 0f), new Vector3(0.15f, 0.17f, 0.1f), Vector3.zero, eyeWhite);
            MakePart(piv["Eyes"], "PupilR", PrimitiveType.Sphere, new Vector3(0.135f, -0.01f, 0.045f), new Vector3(0.085f, 0.1f, 0.05f), Vector3.zero, pupil);
            MakePart(piv["Eyes"], "ShineR", PrimitiveType.Sphere, new Vector3(0.157f, 0.025f, 0.07f), new Vector3(0.035f, 0.035f, 0.015f), Vector3.zero, shine);
            MakePart(piv["Lids"], "Pig_LidL", PrimitiveType.Sphere, new Vector3(-0.14f, 0.055f, 0.012f), new Vector3(0.165f, 0.08f, 0.11f), Vector3.zero, lid);
            MakePart(piv["Lids"], "Pig_LidR", PrimitiveType.Sphere, new Vector3(0.14f, 0.055f, 0.012f), new Vector3(0.165f, 0.08f, 0.11f), Vector3.zero, lid);
            MakePart(piv["Brows"], "BrowL", PrimitiveType.Sphere, new Vector3(-0.14f, 0f, 0f), new Vector3(0.14f, 0.032f, 0.04f), new Vector3(0f, 0f, 10f), brow);
            MakePart(piv["Brows"], "BrowR", PrimitiveType.Sphere, new Vector3(0.14f, 0f, 0f), new Vector3(0.14f, 0.032f, 0.04f), new Vector3(0f, 0f, -10f), brow);
            MakePart(piv["HeadPivot"], "Pig_Tuft0", PrimitiveType.Sphere, new Vector3(0f, 0.38f, 0.13f), new Vector3(0.06f, 0.15f, 0.06f), new Vector3(-20f, 0f, 0f), pink);
            MakePart(piv["HeadPivot"], "Pig_Tuft1", PrimitiveType.Sphere, new Vector3(-0.05f, 0.36f, 0.11f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(-15f, 0f, 28f), pink);
            MakePart(piv["HeadPivot"], "Pig_Tuft2", PrimitiveType.Sphere, new Vector3(0.05f, 0.36f, 0.11f), new Vector3(0.05f, 0.12f, 0.05f), new Vector3(-15f, 0f, -28f), pink);
            MakePart(piv["EarL"], "Pig_EarL", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.28f, 0.06f), Vector3.zero, pink);
            MakePart(piv["EarL"], "EarInnerL", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.022f), new Vector3(0.14f, 0.18f, 0.03f), Vector3.zero, earInner);
            MakePart(piv["EarR"], "Pig_EarR", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0f), new Vector3(0.24f, 0.28f, 0.06f), Vector3.zero, pink);
            MakePart(piv["EarR"], "EarInnerR", PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.022f), new Vector3(0.14f, 0.18f, 0.03f), Vector3.zero, earInner);
            MakePart(piv["Pig_Tail"], "Pig_Tail0", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.08f, 0.08f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail1", PrimitiveType.Sphere, new Vector3(0.0641f, 0.049f, -0.018f), new Vector3(0.075f, 0.075f, 0.075f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail2", PrimitiveType.Sphere, new Vector3(0.0589f, -0.01f, -0.036f), new Vector3(0.07f, 0.07f, 0.07f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail3", PrimitiveType.Sphere, new Vector3(0f, -0.026f, -0.054f), new Vector3(0.065f, 0.065f, 0.065f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail4", PrimitiveType.Sphere, new Vector3(-0.0485f, 0.02f, -0.072f), new Vector3(0.06f, 0.06f, 0.06f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail5", PrimitiveType.Sphere, new Vector3(-0.0433f, 0.085f, -0.09f), new Vector3(0.055f, 0.055f, 0.055f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail6", PrimitiveType.Sphere, new Vector3(-0f, 0.116f, -0.108f), new Vector3(0.05f, 0.05f, 0.05f), Vector3.zero, pink);
            MakePart(piv["Pig_Tail"], "Pig_Tail7", PrimitiveType.Sphere, new Vector3(0.0329f, 0.103f, -0.126f), new Vector3(0.045f, 0.045f, 0.045f), Vector3.zero, pink);
            MakePart(piv["LegFL"], "Pig_Leg0", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.2f, 0.17f, 0.2f), Vector3.zero, pink);
            MakePart(piv["LegFL"], "HoofA0", PrimitiveType.Sphere, new Vector3(-0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegFL"], "HoofB0", PrimitiveType.Sphere, new Vector3(0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegFR"], "Pig_Leg1", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.2f, 0.17f, 0.2f), Vector3.zero, pink);
            MakePart(piv["LegFR"], "HoofA1", PrimitiveType.Sphere, new Vector3(-0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegFR"], "HoofB1", PrimitiveType.Sphere, new Vector3(0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegBL"], "Pig_Leg2", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.2f, 0.17f, 0.2f), Vector3.zero, pink);
            MakePart(piv["LegBL"], "HoofA2", PrimitiveType.Sphere, new Vector3(-0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegBL"], "HoofB2", PrimitiveType.Sphere, new Vector3(0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegBR"], "Pig_Leg3", PrimitiveType.Cylinder, new Vector3(0f, -0.17f, 0f), new Vector3(0.2f, 0.17f, 0.2f), Vector3.zero, pink);
            MakePart(piv["LegBR"], "HoofA3", PrimitiveType.Sphere, new Vector3(-0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            MakePart(piv["LegBR"], "HoofB3", PrimitiveType.Sphere, new Vector3(0.048f, -0.355f, 0.02f), new Vector3(0.1f, 0.07f, 0.15f), Vector3.zero, hoof);
            for (int i = 0; i < 6; i++)
                MakePart(root, "Spark_" + i, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.1f, 0.1f, 0.1f), Vector3.zero, spark);
            return root;
        }

        private static GameObject MakePivot(GameObject parent, string name, Vector3 pos, Vector3 euler)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            return go;
        }

        private static Transform GetModel(GameObject rootPrefab)
        {
            ModelLocator locator = rootPrefab.GetComponent<ModelLocator>();
            return locator ? locator.modelTransform : rootPrefab.transform;
        }

        // Wilbur is the whole character, so the Commando mesh (and item displays) are removed. The skin controller
        // is removed too, because it would re-apply the Commando renderers.
        private static void HideHostModel(GameObject rootPrefab)
        {
            Transform model = GetModel(rootPrefab);
            if (!model) return;

            foreach (ModelSkinController skinController in model.GetComponentsInChildren<ModelSkinController>(true))
                Object.DestroyImmediate(skinController);

            CharacterModel cm = model.GetComponent<CharacterModel>();
            if (cm)
            {
                cm.baseRendererInfos = new CharacterModel.RendererInfo[0];
                cm.itemDisplayRuleSet = null;
            }

            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { r.enabled = false; continue; }
                Object.DestroyImmediate(r);
            }

            AddEmptySkin(model);
        }

        // The loadout screen expects at least one skin, so Wilbur gets an empty "Default" skin
        // (it changes nothing; the pig is drawn separately). Built by reflection to survive game updates.
        private static void AddEmptySkin(Transform model)
        {
            try
            {
                SkinDef skin = ScriptableObject.CreateInstance<SkinDef>();
                ((ScriptableObject)skin).name = "skinWilburDefault";
                FillEmptyArrays(skin, 0);
                SetMember(skin, "nameToken", TokenPrefix + "SKIN_DEFAULT");
                SetMember(skin, "icon", WilburIcons.Get("skin", MakeIcon(new Color(1f, 0.67f, 0.75f), Color.white)));
                SetMember(skin, "rootObject", model.gameObject);

                ModelSkinController msc = model.gameObject.AddComponent<ModelSkinController>();
                SetMember(msc, "skins", new SkinDef[] { skin });
            }
            catch (Exception e)
            {
                if (Log != null) Log.LogWarning("Wilbur could not add a default skin: " + e.Message);
            }
        }

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static void SetMember(object obj, string name, object value)
        {
            FieldInfo f = obj.GetType().GetField(name, AnyInstance);
            if (f != null && (value == null || f.FieldType.IsInstanceOfType(value))) { f.SetValue(obj, value); return; }
            PropertyInfo p = obj.GetType().GetProperty(name, AnyInstance);
            if (p != null && p.CanWrite && (value == null || p.PropertyType.IsInstanceOfType(value))) p.SetValue(obj, value, null);
        }

        private static void FillEmptyArrays(object obj, int depth)
        {
            if (obj == null || depth > 2) return;
            foreach (FieldInfo f in obj.GetType().GetFields(AnyInstance))
            {
                try
                {
                    object cur = f.GetValue(obj);
                    if (f.FieldType.IsArray)
                    {
                        if (cur == null) f.SetValue(obj, Array.CreateInstance(f.FieldType.GetElementType(), 0));
                    }
                    else if (typeof(ScriptableObject).IsAssignableFrom(f.FieldType) && !f.FieldType.IsAbstract
                        && f.Name.IndexOf("Params", StringComparison.OrdinalIgnoreCase) >= 0 && (cur == null || (cur is Object && !(Object)cur)))
                    {
                        ScriptableObject sub = ScriptableObject.CreateInstance(f.FieldType);
                        FillEmptyArrays(sub, depth + 1);
                        f.SetValue(obj, sub);
                    }
                }
                catch (Exception) { }
            }
        }

        private static void AttachPig(GameObject rootPrefab)
        {
            Transform model = GetModel(rootPrefab);
            if (!model) return;

            GameObject pig = BuildPig();
            pig.transform.SetParent(model, false);
            pig.AddComponent<WilburPigController>();
        }

        private void CreateBodyAndDisplay()
        {
            GameObject commandoBody = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody");
            WilburBodyPrefab = PrefabAPI.InstantiateClone(commandoBody, "WilburBody", true);

            CharacterBody body = WilburBodyPrefab.GetComponent<CharacterBody>();
            body.baseNameToken = TokenPrefix + "NAME";
            body.subtitleNameToken = TokenPrefix + "SUBTITLE";
            body.bodyColor = new Color(1f, 0.6f, 0.7f);
            body.baseMaxHealth = 150f;
            body.levelMaxHealth = 45f;
            body.baseRegen = 2f;
            body.levelRegen = 0.4f;
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseMoveSpeed = 7f;
            body.baseArmor = 15f;
            body.baseJumpCount = 1;
            Texture portrait = WilburIcons.Texture("portrait");
            if (portrait) body.portraitIcon = portrait;

            WilburBodyPrefab.AddComponent<WilburStatsController>();
            WilburBodyPrefab.AddComponent<WilburSuperServer>();
            WilburBodyPrefab.AddComponent<WilburCameraZoom>();
            HideHostModel(WilburBodyPrefab);
            AttachPig(WilburBodyPrefab);

            // Pig-height aim point.
            if (body.aimOriginTransform) body.aimOriginTransform.localPosition = new Vector3(0f, 1.0f, 0f);

            ContentAddition.AddBody(WilburBodyPrefab);

            GameObject commandoDisplay = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterDisplays/CommandoDisplay");
            WilburDisplayPrefab = PrefabAPI.InstantiateClone(commandoDisplay, "WilburDisplay", false);
            HideHostModel(WilburDisplayPrefab);
            AttachPig(WilburDisplayPrefab);
        }

        private static SkillDef MakeSkill<T>(string key, Type state, string machine, float cooldown, bool mustKeyPress, bool combat, InterruptPriority priority, Color bg, Color fg) where T : SkillDef
        {
            T def = ScriptableObject.CreateInstance<T>();
            def.skillName = TokenPrefix + key;
            ((ScriptableObject)def).name = TokenPrefix + key;
            def.skillNameToken = TokenPrefix + key + "_NAME";
            def.skillDescriptionToken = TokenPrefix + key + "_DESC";
            def.icon = MakeIcon(bg, fg);
            def.activationState = new SerializableEntityStateType(state);
            def.activationStateMachineName = machine;
            def.baseMaxStock = 1;
            def.baseRechargeInterval = cooldown;
            def.beginSkillCooldownOnSkillEnd = true;
            def.isCombatSkill = combat;
            def.mustKeyPress = mustKeyPress;
            def.interruptPriority = priority;
            ContentAddition.AddSkillDef(def);
            return def;
        }

        private void RegisterStatesAndSkills()
        {
            ContentAddition.AddEntityState<WilburBurpState>(out _);
            ContentAddition.AddEntityState<WilburDevourState>(out _);
            ContentAddition.AddEntityState<WilburBurrowState>(out _);
            ContentAddition.AddEntityState<WilburSuperState>(out _);
            ContentAddition.AddEntityState<WilburLaserState>(out _);
            ContentAddition.AddEntityState<WilburPukeState>(out _);


            SkillLocator skillLocator = WilburBodyPrefab.GetComponent<SkillLocator>();
            foreach (GenericSkill oldSkill in WilburBodyPrefab.GetComponentsInChildren<GenericSkill>(true))
                Object.DestroyImmediate(oldSkill);

            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = TokenPrefix + "PASSIVE_NAME";
            skillLocator.passiveSkill.skillDescriptionToken = TokenPrefix + "PASSIVE_DESC";
            skillLocator.passiveSkill.icon = WilburIcons.Get("passive", MakeIcon(new Color(1f, 0.75f, 0.2f), new Color(1f, 0.6f, 0.7f)));

            Color pinkBg = new Color(0.95f, 0.5f, 0.62f);
            SkillDef primary = MakeSkill<WilburPrimarySkillDef>("PRIMARY", typeof(WilburBurpState), "Weapon", 0f, false, true, InterruptPriority.Any, pinkBg, new Color(0.6f, 0.95f, 0.4f));
            SkillDef secondary = MakeSkill<WilburDevourSkillDef>("SECONDARY", typeof(WilburDevourState), "Weapon", DevourCooldown.Value, true, true, InterruptPriority.PrioritySkill, new Color(0.7f, 0.2f, 0.3f), new Color(1f, 0.8f, 0.8f));
            SkillDef utility = MakeSkill<SkillDef>("UTILITY", typeof(WilburBurrowState), "Body", BurrowCooldown.Value, true, false, InterruptPriority.PrioritySkill, new Color(0.45f, 0.3f, 0.15f), new Color(0.9f, 0.7f, 0.5f));
            SkillDef special = MakeSkill<SkillDef>("SPECIAL", typeof(WilburSuperState), "Body", SuperCooldown.Value, true, true, InterruptPriority.PrioritySkill, new Color(0.85f, 0.15f, 0.15f), new Color(1f, 0.9f, 0.3f));

            primary.icon = WilburIcons.Get("primary", primary.icon);
            secondary.icon = WilburIcons.Get("secondary", secondary.icon);
            utility.icon = WilburIcons.Get("utility", utility.icon);
            special.icon = WilburIcons.Get("special", special.icon);

            skillLocator.primary = CreateGenericSkillSlot(WilburBodyPrefab, "WilburPrimaryFamily", primary);
            skillLocator.secondary = CreateGenericSkillSlot(WilburBodyPrefab, "WilburSecondaryFamily", secondary);
            skillLocator.utility = CreateGenericSkillSlot(WilburBodyPrefab, "WilburUtilityFamily", utility);
            skillLocator.special = CreateGenericSkillSlot(WilburBodyPrefab, "WilburSpecialFamily", special);
        }

        private static GenericSkill CreateGenericSkillSlot(GameObject bodyPrefab, string familyName, params SkillDef[] skillDefs)
        {
            GenericSkill slot = bodyPrefab.AddComponent<GenericSkill>();
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            ((ScriptableObject)family).name = familyName;
            family.variants = new SkillFamily.Variant[skillDefs.Length];
            for (int v = 0; v < skillDefs.Length; v++)
            {
                family.variants[v] = new SkillFamily.Variant
                {
                    skillDef = skillDefs[v],
                    unlockableDef = null,
                    viewableNode = new ViewablesCatalog.Node(skillDefs[v].skillNameToken, false, null)
                };
            }

            ContentAddition.AddSkillFamily(family);

            FieldInfo field = typeof(GenericSkill).GetField("_skillFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) field.SetValue(slot, family);
            return slot;
        }

        private void RegisterSurvivor()
        {
            SurvivorDef survivorDef = ScriptableObject.CreateInstance<SurvivorDef>();
            survivorDef.cachedName = "WilburSurvivor";
            survivorDef.bodyPrefab = WilburBodyPrefab;
            survivorDef.displayPrefab = WilburDisplayPrefab;
            survivorDef.displayNameToken = TokenPrefix + "NAME";
            survivorDef.descriptionToken = TokenPrefix + "DESCRIPTION";
            survivorDef.outroFlavorToken = TokenPrefix + "OUTRO";
            survivorDef.mainEndingEscapeFailureFlavorToken = TokenPrefix + "FAIL";
            survivorDef.desiredSortPosition = 102f;
            survivorDef.primaryColor = new Color(1f, 0.6f, 0.7f);
            ContentAddition.AddSurvivorDef(survivorDef);
        }

        private static Sprite MakeIcon(Color bg, Color fg)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - size * 0.5f) / (size * 0.5f);
                    float dy = (y - size * 0.5f) / (size * 0.5f);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = Color.Lerp(fg, bg, Mathf.Clamp01(dist));
                    if (x < 3 || x >= size - 3 || y < 3 || y >= size - 3)
                        c = fg * 0.85f;
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    // While Super Piggy, right click belongs to the rainbow ball, so Devour is switched off.
    // Shared rules for gobbling: who can be eaten and how a gobble kill is dealt.
    public static class WilburGobble
    {
        private static readonly Dictionary<HealthComponent, float> Pending = new Dictionary<HealthComponent, float>();

        public static int Cap { get { return WilburPlugin.DevourMaxStacks.Value <= 0 ? int.MaxValue : WilburPlugin.DevourMaxStacks.Value; } }
        public static int Stacks(CharacterBody b) { return b ? b.GetBuffCount(WilburPlugin.DevourBuff) : 0; }
        public static float SizeMul(CharacterBody b) { return 1f + WilburPlugin.HeadGrowth.Value * Stacks(b); }

        public static float ExecuteThreshold(CharacterBody b)
        {
            return Mathf.Min(WilburPlugin.ExecuteMax.Value, WilburPlugin.ExecuteBase.Value + WilburPlugin.ExecutePerStack.Value * Stacks(b));
        }

        public static bool IsBoss(HealthComponent hc)
        {
            return hc && hc.body && (hc.body.isBoss || hc.body.isChampion);
        }

        // Non-bosses small enough compared to Wilbur get swallowed whole.
        public static bool CanSwallowWhole(CharacterBody eater, HealthComponent hc)
        {
            if (!eater || !hc || !hc.alive || IsBoss(hc)) return false;
            float frac = WilburPlugin.SwallowWholeBase.Value + WilburPlugin.SwallowWholePerStack.Value * Stacks(eater);
            return hc.fullCombinedHealth <= eater.maxHealth * frac;
        }

        // Swallow-whole size, or weakened below the execute line.
        public static bool CanEat(CharacterBody eater, HealthComponent hc)
        {
            if (!eater || !hc || !hc.alive || IsBoss(hc)) return false;
            return CanSwallowWhole(eater, hc) || hc.combinedHealthFraction < ExecuteThreshold(eater);
        }

        // Kill as a gobble: counts toward Feast with the gobble bonus. Server only.
        public static void Gobble(CharacterBody eater, HealthComponent hc, bool crit)
        {
            if (!NetworkServer.active || !eater || !hc || !hc.alive) return;
            Pending[hc] = Time.time;
            Vector3 pos = hc.body ? hc.body.corePosition : hc.transform.position;
            DamageInfo dmg = new DamageInfo
            {
                attacker = eater.gameObject,
                inflictor = eater.gameObject,
                damage = hc.fullCombinedHealth * 2f + 1f,
                crit = crit,
                position = pos,
                force = Vector3.zero,
                procCoefficient = 0.5f,
                damageColorIndex = DamageColorIndex.Default,
                damageType = DamageType.BypassArmor
            };
            hc.TakeDamage(dmg);
            GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
            WilburFx.SpawnSuck(pos, eater.corePosition);
            if (!hc.alive && hc.body) WilburVictimFx.Slurp(hc.body);
        }

        public static bool WasGobble(HealthComponent hc)
        {
            if (!hc) return false;
            float t;
            bool hit = Pending.TryGetValue(hc, out t) && Time.time - t < 2f;
            Pending.Remove(hc);
            if (Pending.Count > 64)
            {
                List<HealthComponent> old = new List<HealthComponent>();
                foreach (KeyValuePair<HealthComponent, float> kv in Pending)
                    if (!kv.Key || Time.time - kv.Value > 2f) old.Add(kv.Key);
                foreach (HealthComponent h in old) Pending.Remove(h);
            }
            return hit;
        }

        // Gobble every edible enemy near a point. Returns how many.
        public static int GobbleNearby(CharacterBody eater, Vector3 center, float radius, HealthComponent exclude, bool crit)
        {
            if (!NetworkServer.active || !eater) return 0;
            SphereSearch search = new SphereSearch { origin = center, radius = radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(eater.teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();
            int n = 0;
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || hc == exclude || !CanEat(eater, hc)) continue;
                Gobble(eater, hc, crit);
                n++;
            }
            return n;
        }
    }

    public class WilburDevourSkillDef : SkillDef
    {
        public override bool CanExecute(GenericSkill skillSlot)
        {
            if (!base.CanExecute(skillSlot)) return false;
            CharacterBody b = skillSlot.characterBody;
            return b && !b.HasBuff(WilburPlugin.SuperBuff);
        }
    }

    // While Super Piggy, left click belongs to the rainbow laser, so the burp is switched off.
    public class WilburPrimarySkillDef : SkillDef
    {
        public override bool CanExecute(GenericSkill skillSlot)
        {
            if (!base.CanExecute(skillSlot)) return false;
            CharacterBody b = skillSlot.characterBody;
            return b && !b.HasBuff(WilburPlugin.SuperBuff);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Wilbur's visuals: a four-legged pig with a procedural walk cycle, growing with Feast stacks, plus
    // hand-tuned procedural animations for every ability (burp, pounce, chomp, dive, pop, super spin...).
    // ------------------------------------------------------------------------------------------------
    public enum WilburAnim { None, Burp, Pounce, Chomp, Dive, Pop, SuperSpin, Throw, Hurt, Land, Stomp, Happy, Puke }

    public class WilburPigController : MonoBehaviour
    {
        private static Material _goldMat;

        private CharacterBody _body;
        private bool _hadBody;
        private Transform _headPivot;
        private Transform _tail;
        private Transform _lidL;
        private Transform _lidR;
        private Vector3 _lid0;
        private Vector3 _lidPosL;
        private Vector3 _lidPosR;
        private float _lastHealth = -1f;
        private float _lastFull;
        private float _hurtCd;
        private int _lastStacks = -1;
        private float _happyCd;
        private float _candyCd;
        private float _candySoundCd;
        private int _candyOwed;
        private Transform _mouthOpen;
        private Vector3 _mouthOpen0;
        private Transform _tongue;
        private Vector3 _tongue0;
        private Transform _snout;
        private Vector3 _snout0;
        private Transform _earL;
        private Transform _earR;
        private Quaternion _earL0;
        private Quaternion _earR0;
        private float _blink = 3f;
        private float _blinkT;
        private float _sniff = 5f;
        private float _sniffT;
        private readonly Transform[] _legs = new Transform[4];
        private readonly Transform[] _sparks = new Transform[6];
        private Renderer[] _pigRenderers;
        private Material[] _pigMats;
        private Renderer[] _allRenderers;
        private Vector3 _baseScale;
        private float _scaleMul = 1f;
        private float _phase;
        private float _pitch;
        private float _rearTimer;
        private float _idleOink = 5f;
        private bool _chewing;
        private bool _super;
        private bool _burrowed;
        private float _hideAt = -1f;
        private bool _wasGrounded = true;
        private float _fallSpeed;

        // One-shot animation
        private WilburAnim _anim;
        private float _animT;
        private float _animDur = 1f;

        public bool Lasering;

        // Devour latch: sucking on an enemy's head, turned sideways. SuckSide picks which way he tilts.
        public bool Sucking;
        public float SuckSide = 1f;
        private float _suckBlend;
        private Transform _jowls;
        private Vector3 _jowls0;
        private readonly Vector3[] _legPos0 = new Vector3[4];
        private float _gallop;
        private float _air;
        private Vector3 _lastPos;
        private Vector3 _netVel;
        private bool _hasLastPos;

        private void Awake()
        {
            _baseScale = transform.localScale;
            FindBody();

            _headPivot = transform.Find("HeadPivot");
            _tail = transform.Find("Pig_Tail");
            _lidL = transform.Find("HeadPivot/Lids/Pig_LidL");
            _lidR = transform.Find("HeadPivot/Lids/Pig_LidR");
            if (_lidL) { _lid0 = _lidL.localScale; _lidPosL = _lidL.localPosition; }
            if (_lidR) _lidPosR = _lidR.localPosition;
            _mouthOpen = transform.Find("HeadPivot/MouthOpen");
            if (_mouthOpen)
            {
                _mouthOpen0 = _mouthOpen.localScale;
                if (_mouthOpen0.sqrMagnitude < 0.0001f) _mouthOpen0 = new Vector3(0.16f, 0.12f, 0.06f);
                _mouthOpen.localScale = Vector3.zero;
            }
            _tongue = transform.Find("HeadPivot/Tongue");
            if (_tongue) _tongue0 = _tongue.localPosition;
            _snout = transform.Find("HeadPivot/Pig_Snout");
            if (_snout) _snout0 = _snout.localScale;
            _earL = transform.Find("HeadPivot/EarL");
            _earR = transform.Find("HeadPivot/EarR");
            if (_earL) _earL0 = _earL.localRotation;
            if (_earR) _earR0 = _earR.localRotation;
            _legs[0] = transform.Find("LegFL");
            _legs[1] = transform.Find("LegFR");
            _legs[2] = transform.Find("LegBL");
            _legs[3] = transform.Find("LegBR");
            for (int i = 0; i < 4; i++) if (_legs[i]) _legPos0[i] = _legs[i].localPosition;
            _jowls = transform.Find("HeadPivot/Pig_Jowls");
            if (_jowls) _jowls0 = _jowls.localScale;
            for (int i = 0; i < _sparks.Length; i++)
            {
                _sparks[i] = transform.Find("Spark_" + i);
                if (_sparks[i]) _sparks[i].gameObject.SetActive(false);
            }

            List<Renderer> pig = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith("Pig_")) pig.Add(r);
            _pigRenderers = pig.ToArray();
            _pigMats = new Material[_pigRenderers.Length];
            for (int i = 0; i < _pigRenderers.Length; i++) _pigMats[i] = _pigRenderers[i].sharedMaterial;
            _allRenderers = GetComponentsInChildren<Renderer>(true);
        }

        // In the character select screen there is no body: greet the player with a spin.
        private void Start()
        {
            if (!_body) Play(WilburAnim.SuperSpin);
        }

        private void SetHidden(bool hidden)
        {
            foreach (Renderer r in _allRenderers)
                if (r) r.enabled = !hidden;
            if (!hidden && _mouthOpen) _mouthOpen.localScale = Vector3.zero;
        }

        // The game detaches the model from the body at runtime, so look the body up through CharacterModel.
        private void FindBody()
        {
            if (_body) return;
            CharacterModel cm = GetComponentInParent<CharacterModel>();
            if (cm && cm.body) _body = cm.body;
            if (!_body) _body = GetComponentInParent<CharacterBody>();
            if (_body) _hadBody = true;
        }

        public static WilburPigController Find(CharacterBody body)
        {
            if (!body) return null;
            ModelLocator ml = body.modelLocator;
            if (ml && ml.modelTransform)
            {
                WilburPigController p = ml.modelTransform.GetComponentInChildren<WilburPigController>(true);
                if (p) return p;
            }
            return body.GetComponentInChildren<WilburPigController>(true);
        }

        public static void PlayOn(CharacterBody body, WilburAnim anim)
        {
            WilburPigController p = Find(body);
            if (p) p.Play(anim);
        }

        public Vector3 MouthPosition
        {
            get
            {
                if (_headPivot) return _headPivot.TransformPoint(new Vector3(0f, -0.05f, 0.55f));
                return transform.position + Vector3.up;
            }
        }

        public void Rear() { _rearTimer = 0.35f; }
        public void SetChewing(bool on) { _chewing = on; }

        public void Play(WilburAnim anim) { Play(anim, 1f); }

        public void Play(WilburAnim anim, float speed)
        {
            // Big moves aren't interrupted by small ones.
            if ((_anim == WilburAnim.SuperSpin || _anim == WilburAnim.Pop || _anim == WilburAnim.Dive) && _animT < _animDur
                && (anim == WilburAnim.Hurt || anim == WilburAnim.Happy)) return;
            _anim = anim;
            _animT = 0f;
            switch (anim)
            {
                case WilburAnim.Burp: _animDur = 0.6f; break;
                case WilburAnim.Pounce: _animDur = 0.4f; break;
                case WilburAnim.Chomp: _animDur = 0.2f; break;
                case WilburAnim.Dive: _animDur = 0.25f; break;
                case WilburAnim.Pop: _animDur = 0.55f; break;
                case WilburAnim.SuperSpin: _animDur = 0.75f; break;
                case WilburAnim.Throw: _animDur = 0.4f; break;
                case WilburAnim.Hurt: _animDur = 0.3f; break;
                case WilburAnim.Land: _animDur = 0.25f; break;
                case WilburAnim.Stomp: _animDur = 0.4f; break;
                case WilburAnim.Happy: _animDur = 0.4f; break;
                case WilburAnim.Puke: _animDur = 0.75f; break;
                default: _animDur = 0.01f; break;
            }
            _animDur /= Mathf.Clamp(speed, 0.25f, 4f);
        }

        private void ApplySuper(bool on)
        {
            _super = on;
            if (_goldMat == null)
            {
                // Deliberately dim: a strong emissive here washed the whole screen out to white.
                _goldMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_goldMat, new Color(0.95f, 0.68f, 0.08f), new Color(0.9f, 0.55f, 0.05f), 0.5f);
            }
            for (int i = 0; i < _pigRenderers.Length; i++)
                if (_pigRenderers[i]) _pigRenderers[i].sharedMaterial = on ? _goldMat : _pigMats[i];
            for (int i = 0; i < _sparks.Length; i++)
                if (_sparks[i]) _sparks[i].gameObject.SetActive(on);
            if (on)
            {
                WilburFx.SpawnPuffRing(transform.position + Vector3.up * 0.8f, new Color(1f, 0.8f, 0.2f));
                Play(WilburAnim.SuperSpin);
            }
        }

        private static float Ease(float k) { return 1f - (1f - k) * (1f - k); }
        private static float Bump(float k) { return Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI); }

        private void LateUpdate()
        {
            if (!_body) FindBody();
            if (_hadBody && !_body)
            {
                Destroy(gameObject);
                return;
            }

            float dt = Time.deltaTime;
            float t = Time.time;

            // Visual states follow the body's buffs, so they work on every machine.
            if (_body)
            {
                bool sup = _body.HasBuff(WilburPlugin.SuperBuff);
                if (sup != _super) ApplySuper(sup);

                bool under = _body.HasBuff(WilburPlugin.BurrowBuff);
                if (under && !_burrowed)
                {
                    _burrowed = true;
                    Play(WilburAnim.Dive);
                    _hideAt = t + 0.22f;
                }
                else if (!under && _burrowed)
                {
                    _burrowed = false;
                    _hideAt = -1f;
                    SetHidden(false);
                    Play(WilburAnim.Pop);
                }
                if (_burrowed && _hideAt > 0f && t >= _hideAt)
                {
                    _hideAt = -1f;
                    SetHidden(true);
                }
                _chewing = _body.HasBuff(WilburPlugin.FeedingBuff);

                // Flinch when hit (works on every machine: it just watches the health bar).
                HealthComponent hc = _body.healthComponent;
                if (hc)
                {
                    float hp = hc.combinedHealth;
                    float full = hc.fullCombinedHealth;
                    float drop = _lastHealth >= 0f ? _lastHealth - hp : 0f;
                    bool realHit = drop > 0f && full >= _lastFull - 0.5f && !_super && !_burrowed;
                    // Pinata: every hit knocks candy out of him (more for bigger hits).
                    if (realHit && drop > full * 0.005f)
                        _candyOwed += Mathf.Clamp(Mathf.RoundToInt(drop / Mathf.Max(1f, full) * 90f), 3, 30);
                    if (realHit && drop > full * 0.03f && t >= _hurtCd)
                    {
                        _hurtCd = t + 0.7f;
                        Play(WilburAnim.Hurt);
                    }
                    _lastHealth = hp;
                    _lastFull = full;
                }
            }

            int stacks = (_body && WilburPlugin.DevourBuff != null) ? _body.GetBuffCount(WilburPlugin.DevourBuff) : 0;
            // A happy little hop and tail wiggle when he grows.
            if (_lastStacks >= 0 && stacks > _lastStacks && t >= _happyCd && _anim == WilburAnim.None && !_burrowed)
            {
                _happyCd = t + 1.2f;
                Play(WilburAnim.Happy);
            }
            // Deflating spills candy too.
            if (_lastStacks >= 0 && stacks < _lastStacks && !_burrowed && (_super || _lastStacks - stacks >= 2))
                _candyOwed += Mathf.Clamp((_lastStacks - stacks) * 2, 2, 35);
            _lastStacks = stacks;
            if (_candyOwed > 0 && t >= _candyCd)
            {
                _candyCd = t + 0.12f;
                int n = Mathf.Min(_candyOwed, 40);
                _candyOwed = 0;
                WilburCandy.Burst(transform.position + Vector3.up * (0.75f * _scaleMul), n, _scaleMul, 1f);
                if (_body && t >= _candySoundCd)
                {
                    _candySoundCd = t + 0.3f;
                    WilburAudio.PlayFor(_body, "candy");
                }
            }
            float target = (1f + WilburPlugin.HeadGrowth.Value * stacks) * (_super ? 1.35f : 1f);
            _scaleMul = Mathf.Lerp(_scaleMul, target, 1f - Mathf.Exp(-6f * dt));

            // Other players' Wilburs have no real motor velocity or ground state on this machine (the game only
            // syncs their position), so measure the movement ourselves and probe the ground.
            Vector3 vel = Vector3.zero;
            bool local = _body && _body.characterMotor && _body.hasEffectiveAuthority;
            if (_body)
            {
                Vector3 p = _body.transform.position;
                if (_hasLastPos && dt > 0.0001f)
                    _netVel = Vector3.Lerp(_netVel, Vector3.ClampMagnitude((p - _lastPos) / dt, 300f), 1f - Mathf.Exp(-12f * dt));
                _lastPos = p;
                _hasLastPos = true;
                vel = local ? _body.characterMotor.velocity : _netVel;
            }
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float speed = flat.magnitude / Mathf.Max(1f, Mathf.Sqrt(_scaleMul));
            float moving = Mathf.Clamp01(speed / 4f);
            bool sprinting = _body && (_body.isSprinting || speed > 8.5f) && moving > 0.5f;
            _gallop = Mathf.MoveTowards(_gallop, sprinting ? 1f : 0f, dt * 3f);
            _phase += speed * Mathf.Lerp(1.7f, 1.15f, _gallop) * dt;

            // Landing squash (a giant pig lands with a STOMP).
            bool grounded;
            if (local) grounded = _body.characterMotor.isGrounded;
            else if (_body) grounded = Physics.Raycast(_body.footPosition + Vector3.up * 0.3f, Vector3.down, 0.8f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            else grounded = true;
            if (!grounded) _fallSpeed = Mathf.Min(_fallSpeed, vel.y);
            if (grounded && !_wasGrounded && _fallSpeed < -8f && !_super)
            {
                bool giant = _scaleMul >= WilburPlugin.StompMinSize.Value;
                Play(giant ? WilburAnim.Stomp : WilburAnim.Land);
                if (giant && _body)
                {
                    WilburAudio.PlayFor(_body, "stomp");
                    WilburShake.At(_body.footPosition, 3f, 0.45f, 60f + _scaleMul * 2f);
                    WilburFx.SpawnDirtBurst(_body.footPosition, 18, 10f + Mathf.Sqrt(_scaleMul) * 3f);
                }
            }
            if (grounded) _fallSpeed = 0f;
            _wasGrounded = grounded;

            // ---- pose accumulators
            float headRoll = 0f, suckPump = 0f;
            float headPitch = 0f, headYaw = 0f, rootSpin = 0f, rootPitch = 0f, rootRoll = 0f, yOff = 0f, mouth = 0f;
            Vector3 squash = Vector3.one;
            float legTuck = 0f;

            // ---- one-shot animations
            if (_anim != WilburAnim.None)
            {
                _animT += dt;
                float k = Mathf.Clamp01(_animT / _animDur);
                float e = 1f - k;
                switch (_anim)
                {
                    case WilburAnim.Burp:
                        if (k < 0.5f)
                        {
                            // Wind-up: rear back and puff the chest.
                            float a = Mathf.Sin(k / 0.5f * Mathf.PI * 0.5f);
                            headPitch -= 35f * a;
                            rootPitch -= 6f * a;
                            squash = new Vector3(1f + 0.1f * a, 1f + 0.14f * a, 1f - 0.06f * a);
                            mouth = 0.15f;
                        }
                        else
                        {
                            // Release: snap forward, mouth wide open, body recoils.
                            float r = 1f - (k - 0.5f) / 0.5f;
                            headPitch += 20f * r;
                            rootPitch += 8f * r;
                            squash = new Vector3(1f + 0.16f * r, 1f - 0.16f * r, 1f + 0.12f * r);
                            mouth = Mathf.Min(1f, r * 1.4f);
                        }
                        break;
                    case WilburAnim.Pounce:
                        squash = new Vector3(1f - 0.15f * e, 1f - 0.1f * e, 1f + 0.4f * e);
                        headPitch -= 12f * e;
                        legTuck = e;
                        mouth = Mathf.Clamp01(k * 2f);
                        break;
                    case WilburAnim.Chomp:
                        squash = new Vector3(1f + 0.18f * e, 1f - 0.18f * e, 1f - 0.08f * e);
                        headPitch += 28f * Bump(k);
                        mouth = k < 0.3f ? 1f : 0f;
                        break;
                    case WilburAnim.Dive:
                        rootPitch += 70f * Ease(k);
                        yOff -= 0.8f * k;
                        squash = new Vector3(1f, 1f - 0.3f * k, 1f + 0.25f * k);
                        legTuck = k;
                        break;
                    case WilburAnim.Pop:
                        rootSpin += 360f * Ease(k);
                        squash = new Vector3(1f - 0.2f * e, 1f + 0.4f * e, 1f - 0.2f * e);
                        yOff += 0.5f * Bump(k);
                        mouth = e;
                        legTuck = e;
                        break;
                    case WilburAnim.SuperSpin:
                        rootSpin += 720f * Ease(k);
                        float pop = 1f + 0.3f * Bump(k);
                        squash = new Vector3(pop, pop, pop);
                        yOff += 0.4f * Bump(k);
                        break;
                    case WilburAnim.Throw:
                        headPitch += k < 0.4f ? -40f * Bump(k / 0.8f) : 22f * (1f - (k - 0.4f) / 0.6f);
                        rootPitch += k < 0.4f ? -8f * Bump(k / 0.8f) : 8f * (1f - (k - 0.4f) / 0.6f);
                        mouth = k > 0.35f ? e : 0.3f;
                        break;
                    case WilburAnim.Hurt:
                        headYaw += Mathf.Sin(k * 40f) * 18f * e;
                        rootRoll += Mathf.Sin(k * 30f) * 6f * e;
                        squash = new Vector3(1f + 0.1f * e, 1f - 0.12f * e, 1f + 0.1f * e);
                        break;
                    case WilburAnim.Land:
                        squash = new Vector3(1f + 0.25f * e, 1f - 0.3f * e, 1f + 0.25f * e);
                        break;
                    case WilburAnim.Stomp:
                        squash = new Vector3(1f + 0.35f * e, 1f - 0.4f * e, 1f + 0.35f * e);
                        headPitch += 10f * e;
                        break;
                    case WilburAnim.Puke:
                        if (k < 0.37f)
                        {
                            // Two big heaves: belly pumps, head bobs down.
                            float h = Mathf.Abs(Mathf.Sin(k / 0.37f * Mathf.PI * 2f));
                            squash = new Vector3(1f + 0.14f * h, 1f - 0.1f * h, 1f + 0.06f * h);
                            headPitch += 22f * h;
                            rootPitch += 5f * h;
                            mouth = 0.3f * h;
                        }
                        else
                        {
                            // BLEURGH: head thrusts forward, jaw wide open, body stretches.
                            float r = 1f - (k - 0.37f) / 0.63f;
                            float hold = Mathf.Clamp01(r * 1.6f);
                            headPitch += 28f * hold;
                            headYaw += Mathf.Sin(k * 60f) * 4f * hold;
                            rootPitch += 10f * hold;
                            squash = new Vector3(1f - 0.08f * hold, 1f - 0.05f * hold, 1f + 0.2f * hold);
                            mouth = hold;
                        }
                        break;
                    case WilburAnim.Happy:
                        yOff += 0.3f * Bump(k);
                        rootRoll += Mathf.Sin(k * Mathf.PI * 2f) * 8f;
                        mouth = 0.5f * Bump(k);
                        break;
                }
                if (k >= 1f) _anim = WilburAnim.None;
            }

            // ---- continuous poses
            if (Lasering)
            {
                headPitch -= 12f;
                headYaw += Mathf.Sin(t * 47f) * 1.5f;
                rootRoll += Mathf.Sin(t * 53f) * 1.2f;
                mouth = 1f;
            }
            _suckBlend = Mathf.MoveTowards(_suckBlend, Sucking ? 1f : 0f, dt * 9f);
            if (_suckBlend > 0f)
            {
                // Latched onto a head, rolled sideways, cheeks pumping as he sucks.
                float sb = _suckBlend;
                float pump = Mathf.Sin(t * 26f);
                suckPump = (0.5f + 0.5f * pump) * sb;
                rootRoll += 55f * SuckSide * sb;
                headRoll += 22f * SuckSide * sb;
                headPitch += (12f + pump * 6f) * sb;
                headYaw += Mathf.Sin(t * 13f) * 5f * sb;
                mouth = Mathf.Max(mouth, (0.65f + 0.35f * pump) * sb);
                squash = new Vector3(squash.x * (1f + 0.06f * pump * sb), squash.y * (1f - 0.05f * pump * sb), squash.z);
            }
            if (_chewing && _suckBlend < 0.5f)
            {
                headPitch += Mathf.Sin(t * 30f) * 9f;
                mouth = Mathf.Max(mouth, Mathf.Abs(Mathf.Sin(t * 15f)));
            }
            if (_rearTimer > 0f)
            {
                _rearTimer -= dt;
                headPitch -= 30f * Mathf.Sin(Mathf.Clamp01(_rearTimer / 0.35f) * Mathf.PI);
                mouth = Mathf.Max(mouth, 0.4f);
            }

            // Gait: a bouncy trot (diagonal pairs) that blends into a bounding gallop when sprinting. Feet lift on
            // the forward swing, legs splay in the air, tuck for pounces and dives, superhero pose when flying.
            bool airborne = _body && !grounded && !_super;
            _air = Mathf.MoveTowards(_air, airborne ? 1f : 0f, dt * 6f);
            float[] trotOff = { 0f, Mathf.PI, Mathf.PI, 0f };
            float[] galOff = { 0f, 0.35f, Mathf.PI, Mathf.PI + 0.35f };
            float amp = _super ? 0f : moving * Mathf.Lerp(34f, 52f, _gallop);
            for (int i = 0; i < 4; i++)
            {
                if (!_legs[i]) continue;
                float ph = _phase + Mathf.Lerp(trotOff[i], galOff[i], _gallop);
                float a = _super ? (i < 2 ? -70f : 40f) : Mathf.Sin(ph) * amp;
                a = Mathf.Lerp(a, i < 2 ? -35f : 35f, _air * (1f - legTuck));
                a = Mathf.Lerp(a, i < 2 ? -60f : 60f, legTuck);
                _legs[i].localRotation = Quaternion.Euler(a, 0f, 0f);
                float lift = _super ? 0f : Mathf.Max(0f, -Mathf.Cos(ph)) * moving * Mathf.Lerp(0.07f, 0.11f, _gallop) * (1f - _air);
                _legs[i].localPosition = _legPos0[i] + Vector3.up * lift;
            }

            float gaitBob = 0f;
            if (!_super && moving > 0.01f && _suckBlend < 0.5f)
            {
                float trotBounce = (1f - Mathf.Cos(_phase * 2f)) * 0.5f;
                float galBounce = Mathf.Max(0f, Mathf.Sin(_phase + 1.2f));
                gaitBob = Mathf.Lerp(trotBounce * 0.045f, galBounce * 0.13f, _gallop) * moving * (1f - _air);
                rootRoll += Mathf.Sin(_phase) * 3.5f * moving * (1f - _gallop);
                rootPitch += (Mathf.Sin(_phase + 0.4f) * 9f * _gallop + 3f) * moving * (1f - _air);
                headPitch += (Mathf.Sin(_phase * 2f) * 4f * (1f - _gallop) - Mathf.Sin(_phase) * 7f * _gallop) * moving;
                float land = Mathf.Max(0f, -Mathf.Sin(_phase + 1.2f)) * _gallop * moving * (1f - _air);
                squash = new Vector3(squash.x * (1f + 0.04f * land), squash.y * (1f - 0.05f * land), squash.z * (1f + 0.05f * land));
            }

            // Every now and then, an oink (with a little head toss).
            if (_body && _body.hasEffectiveAuthority && WilburPlugin.IdleOinks.Value && !_super && !_burrowed)
            {
                _idleOink -= dt;
                if (_idleOink <= 0f)
                {
                    float lo = Mathf.Max(1f, WilburPlugin.IdleOinkMin.Value);
                    _idleOink = UnityEngine.Random.Range(lo, Mathf.Max(lo, WilburPlugin.IdleOinkMax.Value));
                    WilburAudio.Play("oink");
                    _rearTimer = 0.3f;
                }
            }

            // Idle sniff: the snout wiggles.
            _sniff -= dt;
            if (_sniff <= 0f)
            {
                _sniff = UnityEngine.Random.Range(4f, 8f);
                _sniffT = 0.6f;
            }
            if (_snout)
            {
                float w = 1f;
                if (_sniffT > 0f)
                {
                    _sniffT -= dt;
                    w = 1f + 0.18f * Mathf.Abs(Mathf.Sin(_sniffT * 30f));
                    headPitch += Mathf.Sin(_sniffT * 30f) * 3f;
                }
                _snout.localScale = new Vector3(_snout0.x * w, _snout0.y, _snout0.z * w);
            }

            if (_headPivot) _headPivot.localRotation = Quaternion.Euler(headPitch, headYaw, headRoll);
            if (_jowls) _jowls.localScale = new Vector3(_jowls0.x * (1f + 0.14f * suckPump), _jowls0.y, _jowls0.z * (1f + 0.06f * suckPump));
            if (_tail) _tail.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * (_anim == WilburAnim.Happy ? 25f : 8f)) * 25f);

            // Mouth opens for burps, chomps, lasers.
            if (_mouthOpen && !(_burrowed && _hideAt < 0f))
                _mouthOpen.localScale = _mouthOpen0 * Mathf.Clamp01(mouth);
            if (_tongue) _tongue.localPosition = _tongue0 + new Vector3(0f, -0.06f * mouth, 0.01f * mouth);

            // Ears flop with the trot and flap while flying.
            float flop = _super ? Mathf.Sin(t * 16f) * 18f : Mathf.Sin(_phase * 2f) * 12f * moving;
            if (_anim == WilburAnim.Hurt || _anim == WilburAnim.Happy) flop += Mathf.Sin(t * 30f) * 15f;
            if (_earL) _earL.localRotation = _earL0 * Quaternion.Euler(flop, 0f, 0f);
            if (_earR) _earR.localRotation = _earR0 * Quaternion.Euler(-flop, 0f, 0f);

            // Blink: the lids drop over the eyes.
            _blink -= dt;
            if (_blink <= 0f)
            {
                _blink = UnityEngine.Random.Range(2.5f, 5f);
                _blinkT = 0.13f;
            }
            float lid = 0f;
            if (_blinkT > 0f) { _blinkT -= dt; lid = 1f; }
            if (_anim == WilburAnim.Hurt || Lasering || _anim == WilburAnim.Chomp) lid = Mathf.Max(lid, 0.55f);
            if (_anim == WilburAnim.Happy) lid = Mathf.Max(lid, 0.4f);
            if (_suckBlend > 0.3f) lid = Mathf.Max(lid, 0.6f);
            Vector3 lidScale = new Vector3(_lid0.x, _lid0.y * Mathf.Lerp(1f, 2.4f, lid), _lid0.z * Mathf.Lerp(1f, 1.5f, lid));
            if (_lidL) { _lidL.localScale = lidScale; _lidL.localPosition = _lidPosL + Vector3.down * 0.045f * lid; }
            if (_lidR) { _lidR.localScale = lidScale; _lidR.localPosition = _lidPosR + Vector3.down * 0.045f * lid; }

            if (_super)
            {
                for (int i = 0; i < _sparks.Length; i++)
                {
                    if (!_sparks[i]) continue;
                    float ang = t * 3f + i * Mathf.PI * 2f / _sparks.Length;
                    _sparks[i].localPosition = new Vector3(Mathf.Cos(ang) * 0.95f, 0.7f + Mathf.Sin(t * 2f + i) * 0.2f, Mathf.Sin(ang) * 0.95f);
                }
            }

            // Flight pitch, bob, breathing.
            float pitchTarget = _super ? Mathf.Clamp(-vel.y * 2.5f, -40f, 40f) : 0f;
            _pitch = Mathf.Lerp(_pitch, pitchTarget, 1f - Mathf.Exp(-8f * dt));
            float bob = gaitBob;
            float breathe = 1f + Mathf.Sin(t * 2f) * 0.02f;

            transform.localPosition = new Vector3(0f, WilburPlugin.PigYOffset.Value + (bob + yOff) * _scaleMul, 0f);
            transform.localRotation = Quaternion.Euler(_pitch + rootPitch, rootSpin, rootRoll);
            Vector3 s = _baseScale * _scaleMul;
            transform.localScale = new Vector3(s.x * squash.x, s.y * squash.y * breathe, s.z * squash.z);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Stats from Devour stacks, stack decay, and the Lucky Piggy roulette.
    // ------------------------------------------------------------------------------------------------
    // Feast stack count stored on the master, so it survives stage changes like Cho'Gath's stacks.
    public class WilburFeast : MonoBehaviour
    {
        public int Stacks;
    }

    public class WilburStatsController : MonoBehaviour
    {
        private CharacterBody _body;
        private float _health0;
        private float _levelHealth0;
        private float _armor0;
        private float _damage0;
        private float _levelDamage0;
        private float _speed0;
        private float _jump0;
        private float _stompTimer;
        private float _stepTimer;
        private int _lastStacks = -1;
        private bool _lastFeeding;
        private bool _lastSlide;
        private float _radius0 = -1f;
        private float _stompFxCd;
        private float _lastGain;
        private float _digestAcc;
        private int _digestShown;

        // Slow digestion: stop eating and the Feast slowly melts away, so Wilbur has to keep feeding to stay big.
        // (Super Piggy has its own, faster deflation instead.)
        private void Digest(WilburFeast f)
        {
            float delay = WilburPlugin.DigestDelay.Value;
            if (delay <= 0f || f.Stacks <= 0 || _body.HasBuff(WilburPlugin.SuperBuff)) { _digestAcc = 0f; return; }
            if (Time.time - _lastGain < delay) return;
            _digestAcc += Mathf.Max(WilburPlugin.DigestMin.Value, f.Stacks * WilburPlugin.DigestPercent.Value) * Time.fixedDeltaTime;
            int n = Mathf.FloorToInt(_digestAcc);
            if (n <= 0) return;
            _digestAcc -= n;
            n = Mathf.Min(n, f.Stacks);
            f.Stacks -= n;
            _digestShown += n;
            if (_digestShown >= 5 || f.Stacks == 0)
            {
                WilburFloatingText.Show(_body.corePosition + Vector3.up * (1.2f + Mathf.Min(WilburGobble.SizeMul(_body), 12f) * 0.8f),
                    "digesting -" + _digestShown + "  (" + f.Stacks + ")", new Color(0.8f, 0.7f, 0.72f), 0.7f);
                _digestShown = 0;
            }
        }

        // Announcer state
        private int _spree;
        private int _grillChain;
        private float _grillLast;
        private int _grillShown;
        private float _grillShownAt;
        private bool _grillPending;

        private void Start()
        {
            _lastGain = Time.time;
            // A line when he drops into a new stage.
            if (_body && _body.hasEffectiveAuthority) Invoke("SpawnLine", 2f);
        }

        private void SpawnLine()
        {
            if (_body) WilburPhrases.Say(_body, WilburPhrases.Kind.Spawn, 1f);
        }

        // Item auras (Bustling Fungus ring, Frost Relic, buff visuals) size themselves from the body's radius,
        // so growing that radius with Wilbur makes them grow with him. Set by reflection (private setter).
        private static PropertyInfo _radiusProp;
        private static FieldInfo _radiusField;
        private static bool _radiusLooked;

        private void ApplyRadius(float size)
        {
            try
            {
                if (!_radiusLooked)
                {
                    _radiusLooked = true;
                    BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                    _radiusProp = typeof(CharacterBody).GetProperty("radius", bf);
                    if (_radiusProp == null || _radiusProp.GetSetMethod(true) == null)
                    {
                        _radiusProp = null;
                        _radiusField = typeof(CharacterBody).GetField("radius", bf) ?? typeof(CharacterBody).GetField("<radius>k__BackingField", bf);
                    }
                }
                if (_radius0 < 0f) _radius0 = _body.radius;
                float scale = WilburPlugin.ItemEffectScale.Value;
                float want = _radius0;
                if (scale > 0f) want = Mathf.Clamp(_radius0 + 0.55f * (size - 1f) * scale, _radius0, WilburPlugin.MaxReach.Value * 0.5f);
                if (Mathf.Abs(_body.radius - want) < 0.01f) return;
                if (_radiusProp != null) _radiusProp.GetSetMethod(true).Invoke(_body, new object[] { want });
                else if (_radiusField != null) _radiusField.SetValue(_body, want);
            }
            catch (Exception)
            {
            }
        }

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
            _health0 = _body.baseMaxHealth;
            _levelHealth0 = _body.levelMaxHealth;
            _armor0 = _body.baseArmor;
            _damage0 = _body.baseDamage;
            _levelDamage0 = _body.levelDamage;
            _speed0 = _body.baseMoveSpeed;
            _jump0 = _body.baseJumpPower;
        }

        private WilburFeast Feast
        {
            get
            {
                if (!_body || !_body.master) return null;
                WilburFeast f = _body.master.GetComponent<WilburFeast>();
                if (!f) f = _body.master.gameObject.AddComponent<WilburFeast>();
                return f;
            }
        }

        public void AddFeast(int amount, bool gobble)
        {
            WilburFeast f = Feast;
            if (!f || amount <= 0) return;
            int before = f.Stacks;
            f.Stacks = (int)Math.Min((long)WilburGobble.Cap, (long)f.Stacks + amount);
            int gained = f.Stacks - before;
            _lastGain = Time.time;
            if (gained <= 0) return;

            // Growing past each milestone pays a boosted spin.
            int every = Mathf.Max(1, WilburPlugin.GrowthSpinEvery.Value);
            if (f.Stacks / every > before / every)
            {
                BonusSpin(_body.corePosition, 2, WilburPlugin.ComboItemScale.Value, "GROWTH SPURT! " + (1f + WilburPlugin.HeadGrowth.Value * f.Stacks).ToString("0.#") + "x", new Color(1f, 0.5f, 0.7f));
                WilburPhrases.Say(_body, WilburPhrases.Kind.Grow, 1f);
            }

            string text = (gobble ? "GOBBLED!  +" : "+") + gained + "  (" + f.Stacks + ")  " + (1f + WilburPlugin.HeadGrowth.Value * f.Stacks).ToString("0.#") + "x";
            if (gobble)
            {
                WilburAudio.PlayFor(_body, "oink");
                WilburPhrases.Say(_body, WilburPhrases.Kind.Gobble, 0.6f);
            }
            WilburFloatingText.Show(_body.corePosition + Vector3.up * (1.6f + f.Stacks * 0.01f), text,
                gobble ? new Color(1f, 0.45f, 0.6f) : new Color(1f, 0.75f, 0.85f), gobble ? 1.2f : 0.8f);
        }

        private void FixedUpdate()
        {
            if (!_body || WilburPlugin.DevourBuff == null) return;

            // The server keeps the visible buff count equal to the stored feast (the buff replicates to clients).
            if (NetworkServer.active)
            {
                WilburFeast f = Feast;
                if (f)
                {
                    if (f.Stacks > WilburGobble.Cap) f.Stacks = WilburGobble.Cap;
                    Digest(f);
                    int have = _body.GetBuffCount(WilburPlugin.DevourBuff);
                    int want = Mathf.Clamp(f.Stacks, 0, WilburGobble.Cap);
                    int guard = 0;
                    while (have < want && guard++ < 200) { _body.AddBuff(WilburPlugin.DevourBuff); have++; }
                    while (have > want && guard++ < 200) { _body.RemoveBuff(WilburPlugin.DevourBuff); have--; }
                }
            }

            if (NetworkServer.active)
            {
                TickCombo();
                Stomp();
            }

            int stacks = _body.GetBuffCount(WilburPlugin.DevourBuff);
            bool feeding = _body.HasBuff(WilburPlugin.FeedingBuff);
            bool slide = false;

            if (stacks != _lastStacks || feeding != _lastFeeding || slide != _lastSlide)
            {
                _lastStacks = stacks;
                _lastFeeding = feeding;
                _lastSlide = slide;

                float mul = 1f + WilburPlugin.StackHealth.Value * stacks;
                _body.baseMaxHealth = _health0 * mul;
                _body.levelMaxHealth = _levelHealth0 * mul;
                _body.baseArmor = _armor0 + WilburPlugin.StackArmor.Value * stacks
                    + (feeding ? WilburPlugin.FeedArmor.Value : 0f);
                float dmul = 1f + WilburPlugin.DamagePerStack.Value * stacks;
                _body.baseDamage = _damage0 * dmul;
                _body.levelDamage = _levelDamage0 * dmul;

                // A giant pig covers ground: speed and jump grow with size.
                float size = 1f + WilburPlugin.HeadGrowth.Value * stacks;
                float root = Mathf.Sqrt(size) - 1f;
                _body.baseMoveSpeed = _speed0 * Mathf.Min(1f + WilburPlugin.SpeedScale.Value * root, Mathf.Max(1f, WilburPlugin.MaxSpeedMult.Value));
                _body.baseJumpPower = _jump0 * (1f + WilburPlugin.JumpScale.Value * root);
                _body.RecalculateStats();
                ApplyRadius(size);
            }
        }

        // Mountain pig: anything under his feet gets squashed (bosses just take a beating).
        private void Stomp()
        {
            float size = WilburGobble.SizeMul(_body);
            if (size < WilburPlugin.StompMinSize.Value || _body.HasBuff(WilburPlugin.BurrowBuff) || _body.HasBuff(WilburPlugin.SuperBuff)) return;
            _stompTimer -= Time.fixedDeltaTime;
            if (_stompTimer > 0f) return;
            _stompTimer = 0.25f;

            float r = Mathf.Min(size * WilburPlugin.StompRadius.Value, WilburPlugin.MaxReach.Value);
            Vector3 feet = _body.footPosition;
            SphereSearch search = new SphereSearch { origin = feet + Vector3.up * r * 0.5f, radius = r, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();

            bool crit = _body.RollCrit();
            int squashed = 0;
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || !hc.body) continue;
                float dy = hc.body.corePosition.y - feet.y;
                if (dy < -4f || dy > size * 1.2f) continue;

                if (!WilburGobble.IsBoss(hc))
                {
                    WilburGobble.Gobble(_body, hc, crit);
                    squashed++;
                }
                else
                {
                    DamageInfo dmg = new DamageInfo
                    {
                        attacker = _body.gameObject,
                        inflictor = _body.gameObject,
                        damage = _body.damage * WilburPlugin.StompBossDps.Value * 0.25f,
                        crit = crit,
                        position = hc.body.corePosition,
                        force = Vector3.down * 1500f,
                        procCoefficient = 0.5f,
                        damageColorIndex = DamageColorIndex.Default,
                        damageType = DamageType.Generic
                    };
                    hc.TakeDamage(dmg);
                    GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                }
                RegisterHit(hc.body.corePosition);
            }

            if (squashed > 0)
            {
                WilburFloatingText.ShowCartoon(feet + Vector3.up * 3f, squashed > 1 ? "SQUASH x" + squashed + "!" : "SQUASH!", new Color(1f, 0.5f, 0.6f), 1.3f);
                WilburFx.SpawnDirtBurst(feet, 16, 12f);
                WilburPhrases.Say(_body, WilburPhrases.Kind.Stomp, 0.5f);
                if (Time.time >= _stompFxCd)
                {
                    _stompFxCd = Time.time + 0.6f;
                    WilburAudio.PlayFor(_body, "stomp");
                    WilburPigController.PlayOn(_body, WilburAnim.Stomp);
                    WilburShake.At(feet, 2.5f, 0.4f, r * 3f);
                }
            }
        }

        // Earth-shaking footsteps once he's huge.
        private void Update()
        {
            UpdateDeflateText();
            if (!_body || !_body.characterMotor) return;
            float size = WilburGobble.SizeMul(_body);
            if (size < 10f || !_body.characterMotor.isGrounded) return;
            Vector3 v = _body.characterMotor.velocity;
            v.y = 0f;
            if (v.sqrMagnitude < 4f) return;
            _stepTimer -= Time.deltaTime;
            if (_stepTimer > 0f) return;
            _stepTimer = 0.35f;
            WilburFx.SpawnDirtBurst(_body.footPosition, 6, 4f + Mathf.Sqrt(size) * 2f);
        }

        private static int Scaled(int baseGold)
        {
            try { return Run.instance ? Run.instance.GetDifficultyScaledCost(baseGold) : baseGold; }
            catch (Exception) { return baseGold; }
        }

        // Piggy Bank: every kill rolls the slot for gold. Gobble kills can multicast (x2/x3/x4, Ogre Magi style),
        // and bigger Wilbur multicasts more often.
        public void PiggyBank(bool gobble, Vector3 pos)
        {
            if (!WilburPlugin.PiggyBankEnabled.Value || !_body || !_body.master) return;

            int rolls = 1;
            if (gobble)
            {
                float bonus = WilburPlugin.MulticastPerStack.Value * _body.GetBuffCount(WilburPlugin.DevourBuff);
                if (UnityEngine.Random.value < Mathf.Min(0.95f, WilburPlugin.Multicast2.Value + bonus))
                {
                    rolls = 2;
                    if (UnityEngine.Random.value < Mathf.Min(0.9f, WilburPlugin.Multicast3.Value + bonus))
                    {
                        rolls = 3;
                        if (UnityEngine.Random.value < Mathf.Min(0.85f, WilburPlugin.Multicast4.Value + bonus)) rolls = 4;
                    }
                }
            }

            int gold = 0;
            bool jackpot = false;
            float pJack = WilburPlugin.CoinJackpotChance.Value;
            float pBig = WilburPlugin.CoinBigChance.Value;
            float pSmall = WilburPlugin.CoinSmallChance.Value;
            for (int i = 0; i < rolls; i++)
            {
                float r = UnityEngine.Random.value;
                int tier = 0;
                if (r < pJack) { gold += Scaled(WilburPlugin.CoinJackpot.Value); jackpot = true; tier = 3; }
                else if (r < pJack + pBig) { gold += Scaled(WilburPlugin.CoinBig.Value); tier = 2; }
                else if (r < pJack + pBig + pSmall) { gold += Scaled(WilburPlugin.CoinSmall.Value); tier = 1; }
                // Every spin can also win an item; the better the spin, the better the odds and rarity.
                WilburPrizes.TryGive(_body, tier, 1f, pos);
            }

            Vector3 at = pos + Vector3.up * 1.2f;
            if (rolls > 1)
                WilburFloatingText.ShowCartoon(at + Vector3.up * 0.7f, "MULTICAST x" + rolls + "!", new Color(1f, 0.55f, 0.15f), 0.9f + 0.15f * rolls);

            if (gold > 0)
            {
                _body.master.GiveMoney((uint)gold);
                WilburFloatingText.Show(at, "$" + gold, new Color(1f, 0.85f, 0.15f), jackpot ? 1.4f : 1f);
                WilburAudio.PlayFor(_body, jackpot ? "slot_jackpot" : "coin");
                if (jackpot) WilburPhrases.Say(_body, WilburPhrases.Kind.Money, 1f);
            }
        }

        // ---------------- Piggy Bank bonus spins: combos, multi-kills, interactions, growth ----------------
        private int _combo;
        private float _comboTimer;
        private int _nextComboSpin;
        private readonly List<float> _recentKills = new List<float>();

        // A bonus spin. boost upgrades the result (+1 = small win becomes big, a miss becomes a small win...).
        public void BonusSpin(Vector3 pos, int boost, float itemScale, string label, Color labelColor)
        {
            if (!WilburPlugin.PiggyBankEnabled.Value || !_body || !_body.master || !NetworkServer.active) return;

            float r = UnityEngine.Random.value;
            float pJack = WilburPlugin.CoinJackpotChance.Value;
            float pBig = WilburPlugin.CoinBigChance.Value;
            float pSmall = WilburPlugin.CoinSmallChance.Value;
            int tier = r < pJack ? 3 : (r < pJack + pBig ? 2 : (r < pJack + pBig + pSmall ? 1 : 0));
            tier = Mathf.Clamp(tier + boost, 1, 3);

            int baseGold = tier == 3 ? WilburPlugin.CoinJackpot.Value : (tier == 2 ? WilburPlugin.CoinBig.Value : WilburPlugin.CoinSmall.Value);
            int gold = Mathf.Max(1, Scaled(baseGold));
            _body.master.GiveMoney((uint)gold);

            if (label != null) WilburFloatingText.ShowCartoon(pos + Vector3.up * 2.4f, label, labelColor, 1.3f);
            WilburFloatingText.Show(pos + Vector3.up * 1.4f, "$" + gold, new Color(1f, 0.85f, 0.15f), tier == 3 ? 1.4f : 1f);
            WilburSlotVisual.Spawn(_body, tier == 3 ? new Color(1f, 0.85f, 0.1f) : (tier == 2 ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.6f, 0.75f)),
                tier == 3 ? "JACKPOT!" : (tier == 2 ? "BIG WIN!" : "WIN!"), tier == 3 ? "slot_jackpot" : "slot_good");
            WilburPrizes.TryGive(_body, tier, itemScale, pos);
        }

        // Every hit feeds the combo; every ComboSpinEvery hits in a row pays out a bonus spin.
        public void RegisterHit(Vector3 pos)
        {
            if (!NetworkServer.active || !WilburPlugin.CombosEnabled.Value) return;
            if (_nextComboSpin <= 0) _nextComboSpin = WilburPlugin.ComboSpinEvery.Value;
            _combo++;
            _comboTimer = WilburPlugin.ComboWindow.Value;
            string rank = WilburPhrases.ComboRank(_combo);
            if (rank != null && _body)
                WilburPhrases.Announce(_body, rank, Color.HSVToRGB(Mathf.Repeat(_combo / 160f, 1f), 0.65f, 1f), 0.7f);
            if (_combo >= _nextComboSpin)
            {
                _nextComboSpin += Mathf.Max(1, WilburPlugin.ComboSpinEvery.Value);
                int boost = _combo >= 100 ? 2 : (_combo >= 40 ? 1 : 0);
                BonusSpin(_body ? _body.corePosition : pos, boost, WilburPlugin.ComboItemScale.Value, "COMBO x" + _combo + "!", new Color(1f, 0.55f, 0.15f));
            }
        }

        // Several kills close together = multi-kill spin.
        public void RegisterKill(Vector3 pos)
        {
            if (!NetworkServer.active) return;
            RegisterHit(pos);
            float now = Time.time;

            // Announcer: killing spree (this life) and Halo-style multi-kill chain.
            _spree++;
            string spree = WilburPhrases.SpreeName(_spree);
            if (spree != null && _body)
            {
                bool rage = WilburPhrases.IsRage(spree);
                Color sc = rage ? Color.red : (_spree >= 25 ? new Color(0.75f, 0.45f, 1f) : new Color(1f, 0.55f, 0.2f));
                WilburPhrases.Announce(_body, spree, WilburPhrases.Subtitle(spree), sc, rage ? 1.35f : 1f, rage);
                _grillShownAt = now;
            }
            if (now - _grillLast > 4f) { _grillChain = 0; _grillShown = 1; }
            _grillChain++;
            _grillLast = now;
            if (_grillChain >= 2) _grillPending = true;
            _recentKills.Add(now);
            _recentKills.RemoveAll(k => now - k > WilburPlugin.MultiKillWindow.Value);
            int n = _recentKills.Count;
            if (n >= WilburPlugin.MultiKillCount.Value)
            {
                _recentKills.Clear();
                int boost = n >= WilburPlugin.MultiKillCount.Value + 3 ? 2 : 1;
                BonusSpin(pos, boost, WilburPlugin.ComboItemScale.Value, "MULTI-KILL x" + n + "!", new Color(1f, 0.35f, 0.6f));
            }
        }

        private void TickCombo()
        {
            // Multi-kill calls are batched so a big AoE doesn't spam: only the best new level, twice a second at most.
            if (_grillPending && Time.time - _grillShownAt >= 0.45f)
            {
                _grillPending = false;
                int level = Mathf.Min(_grillChain, 6);
                if (level > _grillShown && _body)
                {
                    _grillShown = level;
                    _grillShownAt = Time.time;
                    string call = WilburPhrases.MultiKillName(level);
                    bool rage = level >= 6;
                    if (call != null)
                        WilburPhrases.Announce(_body, call, WilburPhrases.Subtitle(call), rage ? Color.red : new Color(1f, 0.82f, 0.2f), rage ? 1.35f : 0.85f + 0.05f * level, rage);
                }
            }

            if (_combo <= 0) return;
            _comboTimer -= Time.fixedDeltaTime;
            if (_comboTimer <= 0f)
            {
                _combo = 0;
                _nextComboSpin = WilburPlugin.ComboSpinEvery.Value;
            }
        }

        private float _deflateAcc;
        private int _deflateShown;
        private float _deflateShowTimer;
        private static float _lastCoinSound;

        // Lose Feast stacks (server). Shrinks the pig and shows "-N" every so often.
        public void Deflate(float amount)
        {
            WilburFeast f = Feast;
            if (!f || amount <= 0f || f.Stacks <= 0) return;
            _deflateAcc += amount;
            int n = Mathf.FloorToInt(_deflateAcc);
            if (n <= 0) return;
            _deflateAcc -= n;
            n = Mathf.Min(n, f.Stacks);
            f.Stacks -= n;
            _deflateShown += n;
        }

        private void UpdateDeflateText()
        {
            if (_deflateShown <= 0 || !_body) return;
            _deflateShowTimer -= Time.deltaTime;
            if (_deflateShowTimer > 0f) return;
            _deflateShowTimer = 0.7f;
            WilburFloatingText.Show(_body.corePosition + Vector3.up * (1.2f + Mathf.Min(WilburGobble.SizeMul(_body), 12f) * 0.8f),
                "-" + _deflateShown + "  (" + WilburGobble.Stacks(_body) + ")", new Color(1f, 0.6f, 0.75f), 0.9f);
            _deflateShown = 0;
            WilburAudio.PlayFor(_body, "deflate");
            WilburPhrases.Say(_body, WilburPhrases.Kind.Deflate, 0.25f);
        }

        // One Piggy Bank spin from a hit (Devour bite, sonic wave, gas). Pays less than a kill spin.
        public void PiggyBankHit(Vector3 pos)
        {
            RegisterHit(pos);
            if (!WilburPlugin.PiggyBankEnabled.Value || !WilburPlugin.HitRolls.Value || !_body || !_body.master) return;

            float r = UnityEngine.Random.value;
            float pJack = WilburPlugin.CoinJackpotChance.Value;
            float pBig = WilburPlugin.CoinBigChance.Value;
            float pSmall = WilburPlugin.CoinSmallChance.Value;
            int baseGold;
            bool jackpot = false;
            int tier;
            if (r < pJack) { baseGold = WilburPlugin.CoinJackpot.Value; jackpot = true; tier = 3; }
            else if (r < pJack + pBig) { baseGold = WilburPlugin.CoinBig.Value; tier = 2; }
            else if (r < pJack + pBig + pSmall) { baseGold = WilburPlugin.CoinSmall.Value; tier = 1; }
            else return;
            WilburPrizes.TryGive(_body, tier, WilburPlugin.HitItemScale.Value, pos);

            int gold = Mathf.Max(1, Mathf.RoundToInt(Scaled(baseGold) * WilburPlugin.HitPayout.Value));
            _body.master.GiveMoney((uint)gold);
            WilburFloatingText.Show(pos + Vector3.up * 1.4f, "$" + gold, new Color(1f, 0.85f, 0.15f), jackpot ? 1.3f : 0.75f);
            if (jackpot)
            {
                WilburAudio.PlayFor(_body, "slot_jackpot");
                WilburPhrases.Say(_body, WilburPhrases.Kind.Money, 1f);
            }
            else if (Time.time - _lastCoinSound > 0.12f)
            {
                _lastCoinSound = Time.time;
                WilburAudio.PlayFor(_body, "coin");
            }
        }

        public void RunRoulette(Vector3 interactPos)
        {
            if (!_body) return;

            // Opening anything also feeds the Super Piggy meter.
            WilburSuperServer sup = GetComponent<WilburSuperServer>();
            if (sup && sup.Active) sup.Extend(WilburPlugin.SuperExtend.Value);

            float r = UnityEngine.Random.value;
            int result = r < 0.2f ? 0 : (r < 0.45f ? 1 : (r < 0.75f ? 2 : 3));
            Color color;
            string text;

            switch (result)
            {
                case 0:
                {
                    color = new Color(1f, 0.85f, 0.1f);
                    float total = 0.5f;
                    if (_body.master)
                    {
                        WilburLuckBonus luck = _body.master.GetComponent<WilburLuckBonus>();
                        if (!luck) luck = _body.master.gameObject.AddComponent<WilburLuckBonus>();
                        luck.Add(0.5f);
                        total = luck.Bonus;
                    }
                    // The next item you pick up is doubled.
                    WilburItemDoubler.Arm(_body);
                    text = "JACKPOT!  NEXT ITEM x2  +0.5 LUCK  (stage +" + total.ToString("0.#") + ")";
                    break;
                }
                case 1:
                    color = new Color(1f, 0.55f, 0.7f);
                    if (sup && sup.Active)
                    {
                        sup.Extend(WilburPlugin.SuperExtend.Value);
                        text = "TRUFFLE RUSH!  +" + WilburPlugin.SuperExtend.Value.ToString("0") + "s SUPER PIGGY";
                    }
                    else
                    {
                        if (_body.skillLocator && _body.skillLocator.special)
                            _body.skillLocator.special.AddOneStock();
                        text = "TRUFFLE RUSH!  SUPER PIGGY READY";
                    }
                    break;
                case 2:
                {
                    color = new Color(0.3f, 0.95f, 0.35f);
                    int packs = SpawnSlop(interactPos);
                    text = "PIG OUT!  +" + packs + " HEALING SLOP";
                    break;
                }
                default:
                    color = new Color(0.85f, 0.35f, 0.2f);
                    text = "INDIGESTION...  (bad roll)";
                    Indigestion();
                    break;
            }

            string resultSound = result == 0 ? "slot_jackpot" : (result == 3 ? "slot_bad" : "slot_good");
            WilburAudio.PlayFor(_body, "slot_spin");
            WilburSlotVisual.Spawn(_body, color, text, resultSound);
            Chat.AddMessage("<style=cIsUtility>Lucky Piggy:</style> " + text);
        }

        private int SpawnSlop(Vector3 pos)
        {
            GameObject healPack = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/HealPack");
            if (!healPack) return 0;
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = pos + Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward * 2f + Vector3.up;
                GameObject pack = Object.Instantiate(healPack, p, Quaternion.identity);
                TeamFilter tf = pack.GetComponent<TeamFilter>();
                if (tf) tf.teamIndex = TeamIndex.Player;
                NetworkServer.Spawn(pack);
            }
            return 3;
        }

        // A backfiring burp: small shove opposite the aim direction.
        private void Indigestion()
        {
            WilburAudio.PlayFor(_body, "burp");
            if (_body.characterMotor && _body.inputBank)
            {
                Vector3 back = -_body.inputBank.aimDirection;
                back.y = 0f;
                _body.characterMotor.Motor.ForceUnground();
                _body.characterMotor.velocity += back.normalized * 14f + Vector3.up * 6f;
            }
            WilburFx.SpawnBurp(_body.corePosition + Vector3.up, _body.inputBank ? _body.inputBank.aimDirection : transform.forward, 25f);
        }
    }

    // Jackpot: the next item that lands in your inventory is given to you a second time.
    // (Copying the dropped pickup does not work on game 1.5, where the pickup data changed.)
    public class WilburItemDoubler : MonoBehaviour
    {
        private Inventory _inv;
        private CharacterMaster _master;
        private int[] _snapshot;
        private float _until;
        private int _charges;
        private bool _busy;
        private bool _hooked;

        public static void Arm(CharacterBody body)
        {
            if (!NetworkServer.active || !body || !body.master || !body.master.inventory) return;
            WilburItemDoubler d = body.master.GetComponent<WilburItemDoubler>();
            if (!d) d = body.master.gameObject.AddComponent<WilburItemDoubler>();
            d.ArmNow();
        }

        private void ArmNow()
        {
            _master = GetComponent<CharacterMaster>();
            _inv = _master ? _master.inventory : null;
            if (!_inv) return;
            _snapshot = Snapshot();
            _until = Time.time + WilburPlugin.DoubleWindow.Value;
            _charges++;
            if (!_hooked)
            {
                _inv.onInventoryChanged += OnChanged;
                _hooked = true;
            }
        }

        private int[] Snapshot()
        {
            int n = ItemCatalog.itemCount;
            int[] a = new int[n];
            for (int i = 0; i < n; i++) a[i] = _inv.GetItemCount((ItemIndex)i);
            return a;
        }

        private void OnChanged()
        {
            if (_busy || _charges <= 0 || !NetworkServer.active || !_inv || _snapshot == null) return;
            if (WilburPrizes.Granting)
            {
                // Piggy Bank prizes are temporary and never doubled.
                _snapshot = Snapshot();
                return;
            }
            if (Time.time > _until)
            {
                _charges = 0;
                _snapshot = null;
                return;
            }

            int n = Math.Min(_snapshot.Length, ItemCatalog.itemCount);
            for (int i = 0; i < n; i++)
            {
                ItemIndex idx = (ItemIndex)i;
                if (_inv.GetItemCount(idx) <= _snapshot[i]) continue;

                _busy = true;
                try
                {
                    _inv.GiveItem(idx, 1);
                }
                catch (Exception e)
                {
                    if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur Jackpot could not double the item: " + e.Message);
                }
                _busy = false;
                _charges--;
                _snapshot = _charges > 0 ? Snapshot() : null;

                CharacterBody body = _master ? _master.GetBody() : null;
                if (body)
                {
                    WilburFloatingText.ShowCartoon(body.corePosition + Vector3.up * 2.2f, "ITEM x2!", new Color(1f, 0.85f, 0.1f), 1.4f);
                    WilburAudio.PlayFor(body, "coin");
                }
                return;
            }

            // Nothing new (an item was used up, scrapped, etc.): re-baseline.
            _snapshot = Snapshot();
        }

        private void OnDestroy()
        {
            if (_hooked && _inv) _inv.onInventoryChanged -= OnChanged;
        }
    }

    // Cartoon pig phrases that pop out of Wilbur when he attacks, gets hit, gobbles or cashes in.
    public static class WilburPhrases
    {
        public enum Kind
        {
            Attack, Burp, Devour, Chain, Gobble, Burrow, Mud, Erupt, Super, Laser, Rainbow, HamSlam,
            Hurt, Money, Prize, Deflate, Stomp, Grow, Spawn
        }

        // Each kind has Wilbur's own lines and pig parodies of famous game lines (used when GameQuotes is on).
        private static readonly Dictionary<Kind, string[]> Own = new Dictionary<Kind, string[]>
        {
            { Kind.Attack, new[] { "OINK OINK!", "HOG WILD!", "SOOO-EEE!", "SNOUT ATTACK!", "PORK CHOP!", "HAM-BUSH!" } },
            { Kind.Burp, new[] { "OINK OINK!", "SOOO-EEE!", "EXCUSE ME!", "GREEN AND MEAN!", "WHO DEALT IT?" } },
            { Kind.Devour, new[] { "SNACK TIME!", "PIGGY'S HUNGRY!", "HAM-BUSH!", "TIME TO PIG OUT!", "SNOUT ATTACK!" } },
            { Kind.Chain, new[] { "ALL YOU CAN EAT!", "BUFFET MODE!", "MORE! MORE!", "NEXT COURSE!", "NOM NOM NOM!" } },
            { Kind.Gobble, new[] { "NOM NOM!", "DELICIOUS!", "SECONDS PLEASE!", "*BURP*", "YUM-OINK!", "GOBBLE GOBBLE!" } },
            { Kind.Burrow, new[] { "TRUFFLE HUNT!", "TUNNEL SNOUT!", "DIGGY DIGGY!", "BACK IN A JIFFY!" } },
            { Kind.Mud, new[] { "PIG IN MUD!", "MUD BATH!", "EARTHQUAKE!", "WALLOW TIME!", "DIRTY HOG!", "THE FLOOR IS MUD!" } },
            { Kind.Erupt, new[] { "SURPRISE!", "PEEK-A-BOAR!", "TRUFFLES!", "HERE'S PIGGY!" } },
            { Kind.Super, new[] { "WHEN PIGS FLY!", "SUPER SWINE!", "GOLDEN HOG!", "THIS LITTLE PIGGY FLIES!" } },
            { Kind.Laser, new[] { "RAINBOW HAM!", "PRISMATIC PORK!", "DOUBLE RAINBOW!", "SPARKLE SNOUT!" } },
            { Kind.Rainbow, new[] { "BLEEEURGH!", "RAINBOW YAWN!", "I REGRET NOTHING!", "SHOULDN'T HAVE HAD\nTHAT LAST SLOP...", "HOGS AND KISSES!" } },
            { Kind.HamSlam, new[] { "HAM SLAM!", "BELLY FLOP!", "CANNONBALL!", "SPLAT!" } },
            { Kind.Hurt, new[] { "NOT THE BACON!", "OUCH! MY HAM!", "SQUEAL!", "HEY! RUDE!", "OINK?!", "MY CURLY TAIL!", "THAT'S HOGWASH!", "I'M NOT A PIÑATA!", "HEY! THAT'S MY CANDY!" } },
            { Kind.Money, new[] { "KA-CHING!", "PIGGY BANK!", "OINK-OME!", "CASH PIG!", "JACKPORK!" } },
            { Kind.Prize, new[] { "OOH, SHINY!", "PRESENT FOR PIGGY!", "LOOT!", "FINDERS KEEPERS!" } },
            { Kind.Deflate, new[] { "PFFFFT!", "LOSING WEIGHT!", "WORTH IT!", "SHRINK-OINK!", "MORE SNACKS LATER!", "FREE CANDY!", "SWEETS FOR EVERYONE!" } },
            { Kind.Stomp, new[] { "STOMP!", "PANCAKE!", "WATCH YOUR STEP!", "SO TINY!", "FEE FI FO PORK!" } },
            { Kind.Grow, new[] { "GROWTH SPURT!", "MORE HAM, MORE POWER!", "BIGGER IS BETTER!", "XXXL PIG!" } },
            { Kind.Spawn, new[] { "OINK!", "WHO'S HUNGRY?", "LET'S PIG OUT!" } },
        };

        private static readonly Dictionary<Kind, string[]> Quotes = new Dictionary<Kind, string[]>
        {
            // Oink Oink: a sonic shout plus a gas cloud.
            { Kind.Burp, new[] { "FUS RO OINK!", "HADOU-OINK!", "FIRE IN THE HOG!", "NUCLEAR BURP DETECTED.", "YOU REQUIRE MORE\nPIG GAS!", "IT'S SUPER DIGESTIVE!", "WORT WORT WOINK!", "SMELL YA LATER!", "BREATH OF THE\nWILD BOAR!" } },
            // Slopp Devour: the pounce.
            { Kind.Devour, new[] { "GET OVER HERE, SNACK!", "A WILD SNACK APPEARED!", "FRESH MEAT!", "RIP AND CHEW!", "LEEEEROY HAMKINS!", "WOULD YOU KINDLY\nBE LUNCH?", "WAKA WAKA WAKA!" } },
            // Slopp Devour: long chains.
            { Kind.Chain, new[] { "GOTTA EAT 'EM ALL!", "C-C-C-COMBO BREAKFAST!", "FINISH HIM...\nWITH GRAVY!", "IT'S SUPER DELICIOUS!", "MORE SNACKS REQUIRED." } },
            // Swallowed whole.
            { Kind.Gobble, new[] { "FAT-ALITY!", "PRESS F TO\nPAY RESPECTS.", "THE CAKE WAS\nNOT A LIE!", "A SNACK HAS BEEN SLAIN.", "HEROES NEVER DIET!", "EN TARO A-DONUT!", "FLAWLESS DIGESTION!" } },
            // Truffle Burrow: going under.
            { Kind.Burrow, new[] { "BRAVO SIX, GOING MUD.", "WAKE ME WHEN\nYOU NEED HAM.", "I NEED A TRUFFLE.", "ZERG RUSH? HOG RUSH!", "USE THE BOOST\nTO DIG THROUGH!" } },
            // The earthquake (slams flyers down).
            { Kind.Mud, new[] { "ALL AIRCRAFT REPORT...\nTO THE MUD!", "GET DOWN, FLYBOY!", "SEISMIC SWINE DETECTED." } },
            // Bursting out.
            { Kind.Erupt, new[] { "KEPT YOU WAITING, HUH?", "SHORYU-HAM!", "!", "IT'S-A ME, HAM-IO!", "NUCLEAR LUNCH DETECTED." } },
            // Super Piggy takeoff.
            { Kind.Super, new[] { "DO A BACON ROLL!", "BATTLE-SWINE OPERATIONAL.", "PORK OVERWHELMING!", "YOU ARE NOT PREPARED...\nFOR PORK!", "GOTTA OINK FAST!", "HAIL TO THE OINK, BABY!" } },
            // Rainbow laser.
            { Kind.Laser, new[] { "KAME-HAME-HAM!", "IT'S HIGH NOON...\nSNACK TIME.", "ENEMY AC-130 HOG ABOVE!", "SPARTAN LASER,\nPIG EDITION!", "RAINBOW ROAD!" } },
            // Rainbow ball (vortex).
            { Kind.Rainbow, new[] { "HAM-ATO CANNON!", "NERF THIS!", "GRAVITON SLURP!", "PREDATOR PORK\nMISSILE INBOUND!", "SPIRIT BOMB OF BACON!" } },
            // HAM SLAM.
            { Kind.HamSlam, new[] { "HAM-MER DOWN!", "JUSTICE HAMS\nFROM ABOVE!", "TACTICAL HAM INCOMING!", "FALCON PORK!", "SIR. FINISHING\nTHIS FEAST.", "MY LARD FOR AIUR!" } },
            // Getting hit.
            { Kind.Hurt, new[] { "YOU WANNA PIECE\nOF ME, BOAR?", "PEPPY! GET THIS GUY\nOFF MY HAM!", "YOUR BACON IS\nUNDER ATTACK!", "TOOK A FORK\nTO THE HAM!", "SNOUT? SNOOOOUT!", "CAN'T LET YOU DO THAT,\nSTAR HOG!" } },
            // Piggy Bank jackpots.
            { Kind.Money, new[] { "SHOW ME THE MONEY!", "TIME IS MONEY, FRIEND!", "NOT ENOUGH MINERALS?\nNOT ANYMORE!", "RUPEES FOR PIGGY!" } },
            // Temporary item prizes.
            { Kind.Prize, new[] { "CARE PORKAGE INBOUND!", "IT'S DANGEROUS TO GO ALONE!\nTAKE THIS!", "ITEM GET!", "DA-NA-NA-NAAA!" } },
            // Shrinking while Super.
            { Kind.Deflate, new[] { "NOT ENOUGH LARD.", "I SHOULD GO...\nON A DIET.", "SHIELDS... I MEAN\nBELLY... LOW!" } },
            // Giant stomps.
            { Kind.Stomp, new[] { "GOOMBA STOMP!", "IT'S-A ME,\nSTOMP-IO!", "SHADOW OF THE\nCOLOSS-HOG!", "JUGGERHOG!" } },
            // Growing milestones.
            { Kind.Grow, new[] { "WHAT? WILBUR\nIS EVOLVING!", "BIG BOSS HOG.", "MEGA EVOL-OINK!", "LEVEL UP!" } },
            // A new stage.
            { Kind.Spawn, new[] { "PIG GOOD TO GO, SIR!", "IT'S-A ME, WILBUR!", "HEY! LISTEN! OINK!" } },
        };

        private static readonly Color[] Colors =
        {
            new Color(1f, 0.6f, 0.75f), new Color(1f, 0.9f, 0.3f), new Color(1f, 1f, 1f), new Color(0.6f, 1f, 0.6f)
        };

        private static readonly Dictionary<CharacterBody, float> Next = new Dictionary<CharacterBody, float>();
        private static readonly Dictionary<int, int> LastPick = new Dictionary<int, int>();

        private static bool Forced(Kind k)
        {
            return k == Kind.Super || k == Kind.Money || k == Kind.Laser || k == Kind.Rainbow || k == Kind.HamSlam
                || k == Kind.Grow || k == Kind.Spawn;
        }

        private static string Pick(Kind kind)
        {
            string[] own;
            string[] quotes;
            if (!Own.TryGetValue(kind, out own)) own = Own[Kind.Attack];
            bool useQuotes = WilburPlugin.GameQuotes == null || WilburPlugin.GameQuotes.Value;
            if (useQuotes && Quotes.TryGetValue(kind, out quotes) && quotes.Length > 0 && UnityEngine.Random.value < 0.65f)
                own = quotes;

            int i = UnityEngine.Random.Range(0, own.Length);
            int last;
            int key = (int)kind * 2 + (own.Length > 0 && Own.ContainsKey(kind) && own == Own[kind] ? 0 : 1);
            if (own.Length > 1 && LastPick.TryGetValue(key, out last) && last == i) i = (i + 1) % own.Length;
            LastPick[key] = i;
            return own[i];
        }

        public static void Say(CharacterBody body, Kind kind, float chance)
        {
            if (!body || WilburPlugin.PhrasesEnabled == null || !WilburPlugin.PhrasesEnabled.Value) return;
            bool forced = Forced(kind);
            float next;
            if (!forced && Next.TryGetValue(body, out next) && Time.time < next) return;
            if (UnityEngine.Random.value > chance) return;
            Next[body] = Time.time + WilburPlugin.PhraseCooldown.Value;

            string text = Pick(kind);
            Color c = kind == Kind.Rainbow || kind == Kind.Laser ? Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 1f)
                : kind == Kind.Super || kind == Kind.Money || kind == Kind.HamSlam || kind == Kind.Prize ? new Color(1f, 0.85f, 0.2f)
                : kind == Kind.Hurt ? new Color(1f, 0.45f, 0.45f)
                : kind == Kind.Burp ? new Color(0.6f, 1f, 0.45f)
                : kind == Kind.Burrow || kind == Kind.Mud || kind == Kind.Erupt ? new Color(0.95f, 0.75f, 0.5f)
                : Colors[UnityEngine.Random.Range(0, Colors.Length)];

            float size = WilburGobble.SizeMul(body);
            Vector3 side = Camera.main ? Camera.main.transform.right : Vector3.right;
            Vector3 pos = body.corePosition + Vector3.up * (1.4f * Mathf.Min(size, 12f) + 0.8f) + side * UnityEngine.Random.Range(-0.6f, 0.6f);
            WilburFloatingText.ShowCartoon(pos, text, c, text.Length > 22 ? 1.05f : 1.2f);

            if (Next.Count > 16)
            {
                List<CharacterBody> dead = new List<CharacterBody>();
                foreach (KeyValuePair<CharacterBody, float> kv in Next) if (!kv.Key) dead.Add(kv.Key);
                foreach (CharacterBody b in dead) Next.Remove(b);
            }
        }

        // Announcer call pinned to the top of the screen (multi-kills, sprees, combo ranks). Only the player
        // playing this Wilbur sees and hears it.
        public static void Announce(CharacterBody body, string text, Color color, float scale)
        {
            Announce(body, text, null, color, scale, false);
        }

        public static void Announce(CharacterBody body, string text, string subtitle, Color color, float scale, bool rage)
        {
            if (!body || !body.hasEffectiveAuthority || WilburPlugin.Announcer == null || !WilburPlugin.Announcer.Value) return;
            WilburAnnouncerText.Show(text, subtitle, color, scale, rage);
            WilburAudio.PlayFor(body, "combo");
            if (rage)
            {
                WilburAudio.PlayFor(body, "stomp");
                WilburShake.At(body.corePosition, 3f, 0.6f, 500f);
            }
        }

        // Multi-kills (kills within ~4 seconds of each other): pig puns on the classics.
        public static string MultiKillName(int kills)
        {
            switch (kills)
            {
                case 2: return "DOUBLE TROUGH-BLE!";
                case 3: return "HAM TRICK!";
                case 4: return "QUADRA-PORK!";
                case 5: return "PENTA-PORKER!";
            }
            return kills >= 6 ? "HAMPAGE!" : null;
        }

        // Killing sprees without dying: every one is a pun.
        public static string SpreeName(int kills)
        {
            switch (kills)
            {
                case 5: return "SQUEALING SPREE!";
                case 9: return "HOGLIKE!";
                case 10: return "FEEDING FRENZY!";
                case 15: return "HOG WILD!";
                case 20: return "HAMPAGE!";
                case 25: return "UNTOUCHABACON!";
                case 30: return "INVINCIBACON!";
                case 35: return "HAMTASTIC!";
                case 40: return "UNFRYGGENBELIEVABLE!";
                case 50: return "SLOTTERHOUSE!";
                case 75: return "PORKALYPSE NOW!";
                case 100: return "THE WHOLE HOG!";
                case 255: return "MAXIMUM PORKAGE!";
            }
            if (kills > 100 && kills % 50 == 0) return "BEYOND HOGLIKE!";
            return null;
        }

        // The rage calls get the red, shaking Dota treatment.
        public static bool IsRage(string call)
        {
            return call == "HAMPAGE!";
        }

        public static string Subtitle(string call)
        {
            return null;
        }

        // Devil May Cry style combo ranks.
        public static string ComboRank(int hits)
        {
            switch (hits)
            {
                case 10: return "D - DIRTY!";
                case 25: return "C - CRISPY!";
                case 50: return "B - BACON!";
                case 100: return "A - ALL YOU CAN EAT!";
                case 200: return "S - SIZZLING!";
                case 350: return "SS - SUPER SWINE!";
                case 500: return "SSS - SSSOOOO-EEE!";
            }
            return null;
        }
    }

    // +Luck from a Jackpot lasts until the next stage starts.
    public class WilburLuckBonus : MonoBehaviour
    {
        private CharacterMaster _master;
        private float _bonus;
        public float Bonus { get { return _bonus; } }

        private void Awake()
        {
            _master = GetComponent<CharacterMaster>();
        }

        private void OnEnable()
        {
            Stage.onStageStartGlobal += OnStageStart;
        }

        private void OnDisable()
        {
            Stage.onStageStartGlobal -= OnStageStart;
        }

        private void OnStageStart(Stage stage)
        {
            Clear();
        }

        public void Add(float amount)
        {
            if (!_master) return;
            _master.luck += amount;
            _bonus += amount;
        }

        private void Clear()
        {
            if (_master && _bonus != 0f)
            {
                _master.luck -= _bonus;
                _bonus = 0f;
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }

    // Server-side timer for Super Piggy so chests and feeds can extend it.
    public class WilburSuperServer : MonoBehaviour
    {
        private CharacterBody _body;
        private bool _invuln;
        public float Remaining;

        public bool Active { get { return Remaining > 0f; } }

        private void Awake()
        {
            _body = GetComponent<CharacterBody>();
        }

        public void Begin(float duration)
        {
            if (!NetworkServer.active || !_body) return;
            Remaining = duration;
            if (!_body.HasBuff(WilburPlugin.SuperBuff)) _body.AddBuff(WilburPlugin.SuperBuff);
            if (!_invuln)
            {
                _body.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
                _invuln = true;
            }
        }

        public void Extend(float seconds)
        {
            if (Remaining <= 0f) return;
            Remaining = Mathf.Min(Remaining + seconds, 30f);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || Remaining <= 0f) return;
            float dt = Time.fixedDeltaTime;
            Remaining -= dt;

            // Being Super Piggy slowly deflates you.
            WilburStatsController stats = _body ? _body.GetComponent<WilburStatsController>() : null;
            int stacks = WilburGobble.Stacks(_body);
            if (stats && stacks > 0)
                stats.Deflate(Mathf.Max(WilburPlugin.SuperDrainMin.Value, stacks * WilburPlugin.SuperDrainFraction.Value) * dt);

            if (Remaining <= 0f) End();
        }

        public void End()
        {
            Remaining = 0f;
            if (!_body) return;
            if (_body.HasBuff(WilburPlugin.SuperBuff)) _body.RemoveBuff(WilburPlugin.SuperBuff);
            if (_invuln)
            {
                if (_body.HasBuff(RoR2Content.Buffs.HiddenInvincibility)) _body.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
                _invuln = false;
            }
        }

        private void OnDestroy()
        {
            if (NetworkServer.active) End();
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Small visual helpers (local, no networking).
    // ------------------------------------------------------------------------------------------------
    public class WilburFlyingPuff : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Life = 0.4f;
        private float _t;
        private Vector3 _scale0;

        private void Start()
        {
            _scale0 = transform.localScale;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            transform.position += Velocity * Time.deltaTime;
            transform.localScale = _scale0 * Mathf.Max(0.01f, 1f - _t / Life);
            if (_t >= Life) Destroy(gameObject);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Custom sounds. RoR2 turns Unity audio off, so wav files are played through Windows' winmm (MCI).
    // Any wav in the Sounds folder whose name starts with a sound's key is used for it, e.g.
    // oink_1.wav, oink_gertrude.wav -> "oink". Several files for one key = random variety.
    // Limits: Windows only, ignores the game's volume sliders, not positional, only you hear them.
    // ------------------------------------------------------------------------------------------------
    public static class WilburAudio
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr winHandle);

        // Longest keys first so "slot_spin" is not mistaken for something shorter.
        private static readonly string[] Keys =
        {
            "slot_jackpot", "slot_good", "slot_bad", "slot_spin",
            "oinkoink", "oink", "burp", "gas", "squeal", "chomp", "gulp", "dig", "erupt", "coin", "rainbow", "deflate",
            "stomp", "quake", "prize", "combo", "whoosh", "candy", "puke", "slurp"
        };
        private const int Slots = 2;

        // key -> variants -> pool of MCI aliases (separate copies so the same sound can overlap itself)
        private static readonly Dictionary<string, List<string[]>> Sounds = new Dictionary<string, List<string[]>>();
        private static readonly Dictionary<string, int> NextSlot = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> LastVariant = new Dictionary<string, int>();
        private static readonly List<string> Opened = new List<string>();
        private static bool _broken;
        private static string _tempDir;
        private static int _loadCount;

        private static int Send(string cmd)
        {
            if (_broken) return -1;
            try
            {
                return mciSendString(cmd, null, 0, IntPtr.Zero);
            }
            catch (Exception e)
            {
                _broken = true;
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur sounds disabled (winmm unavailable): " + e.Message);
                return -1;
            }
        }

        public static void Unload()
        {
            foreach (string alias in Opened) Send("close " + alias);
            Opened.Clear();
            Sounds.Clear();
            NextSlot.Clear();
            LastVariant.Clear();
        }

        public static void Load(string folder, int volume)
        {
            Unload();
            if (_broken) return;
            if (!System.IO.Directory.Exists(folder))
            {
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur Sounds folder not found: " + folder);
                return;
            }

            _loadCount++;
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WilburSounds");
            try
            {
                System.IO.Directory.CreateDirectory(_tempDir);
                foreach (string old in System.IO.Directory.GetFiles(_tempDir, "*.wav"))
                {
                    try { System.IO.File.Delete(old); } catch { }
                }
            }
            catch (Exception e)
            {
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur could not prepare temp sound folder: " + e.Message);
                return;
            }

            // Perceptual volume: 0-100 mapped on a curve so 50 sounds about half as loud.
            float gain = Mathf.Pow(Mathf.Clamp(volume, 0, 100) / 100f, 2f);
            int loaded = 0;

            foreach (string file in System.IO.Directory.GetFiles(folder, "*.wav"))
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string key = null;
                foreach (string k in Keys)
                {
                    if (name.StartsWith(k)) { key = k; break; }
                }
                if (key == null) continue;

                byte[] scaled = ScaleWav(file, gain);
                if (scaled == null) continue;

                List<string[]> variants;
                if (!Sounds.TryGetValue(key, out variants))
                {
                    variants = new List<string[]>();
                    Sounds[key] = variants;
                }

                int v = variants.Count;
                string[] pool = new string[Slots];
                int ok = 0;
                for (int slot = 0; slot < Slots; slot++)
                {
                    string alias = "wil" + _loadCount + "_" + key + "_" + v + "_" + slot;
                    string dest = System.IO.Path.Combine(_tempDir, alias + ".wav");
                    try { System.IO.File.WriteAllBytes(dest, scaled); }
                    catch { continue; }
                    if (Send("open \"" + dest + "\" type waveaudio alias " + alias) == 0)
                    {
                        pool[ok++] = alias;
                        Opened.Add(alias);
                    }
                }
                if (ok == 0) continue;
                if (ok < Slots) Array.Resize(ref pool, ok);
                variants.Add(pool);
                loaded++;
            }

            if (WilburPlugin.Log != null) WilburPlugin.Log.LogInfo("Wilbur loaded " + loaded + " sound files from " + folder);
        }

        // Applies the volume by scaling 16-bit PCM samples (MCI ignores its own volume command).
        private static byte[] ScaleWav(string path, float gain)
        {
            byte[] data;
            try { data = System.IO.File.ReadAllBytes(path); }
            catch { return null; }
            if (data.Length < 44 || Encoding.ASCII.GetString(data, 0, 4) != "RIFF" || Encoding.ASCII.GetString(data, 8, 4) != "WAVE")
                return null;

            int pos = 12;
            short format = 0;
            short bits = 0;
            while (pos + 8 <= data.Length)
            {
                string id = Encoding.ASCII.GetString(data, pos, 4);
                int size = BitConverter.ToInt32(data, pos + 4);
                int body = pos + 8;
                if (size < 0) break;
                if (id == "fmt " && body + 16 <= data.Length)
                {
                    format = BitConverter.ToInt16(data, body);
                    bits = BitConverter.ToInt16(data, body + 14);
                }
                else if (id == "data")
                {
                    if (format == 1 && bits == 16)
                    {
                        int end = Math.Min(body + size, data.Length);
                        for (int i = body; i + 1 < end; i += 2)
                        {
                            int sample = (int)Math.Round(BitConverter.ToInt16(data, i) * gain);
                            if (sample > short.MaxValue) sample = short.MaxValue;
                            if (sample < short.MinValue) sample = short.MinValue;
                            data[i] = (byte)(sample & 0xFF);
                            data[i + 1] = (byte)((sample >> 8) & 0xFF);
                        }
                    }
                    break;
                }
                pos = body + size + (size & 1);
            }
            return data;
        }

        public static void Play(string key)
        {
            if (_broken) return;
            List<string[]> variants;
            if (!Sounds.TryGetValue(key, out variants) || variants.Count == 0) return;

            int v = UnityEngine.Random.Range(0, variants.Count);
            int last;
            if (variants.Count > 1 && LastVariant.TryGetValue(key, out last) && last == v)
                v = (v + 1) % variants.Count;
            LastVariant[key] = v;

            string[] pool = variants[v];
            string slotKey = key + v;
            int slot;
            NextSlot.TryGetValue(slotKey, out slot);
            NextSlot[slotKey] = (slot + 1) % pool.Length;
            Send("play " + pool[slot % pool.Length] + " from 0");
        }

        // Only the player controlling this Wilbur hears his sounds.
        public static void PlayFor(CharacterBody body, string key)
        {
            if (!body || !body.hasEffectiveAuthority) return;
            Play(key);
        }
    }

    // Big readable text in the world (results, numbers). Faces the camera, rises and fades.
    public class WilburFloatingText : MonoBehaviour
    {
        private static Font _font;
        private static bool _fontTried;
        private TextMesh _mesh;
        private Color _color;
        private float _t;
        private const float Life = 2.2f;

        private readonly List<TextMesh> _outlines = new List<TextMesh>();
        private bool _cartoon;
        private float _spin;

        private static bool EnsureFont()
        {
            if (!_fontTried)
            {
                _fontTried = true;
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
                if (!_font) { try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _font = null; } }
                if (!_font) { try { _font = Font.CreateDynamicFontFromOSFont("Arial", 64); } catch { _font = null; } }
            }
            return _font != null;
        }

        private static TextMesh MakeMesh(GameObject go, string text, Color color, float size, int order)
        {
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.035f * size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.sharedMaterial = _font.material;
                mr.sortingOrder = order;
            }
            return tm;
        }

        public static void Show(Vector3 pos, string text, Color color, float size)
        {
            try
            {
                if (!EnsureFont()) return;
                GameObject go = new GameObject("WilburText");
                go.transform.position = pos;
                WilburFloatingText ft = go.AddComponent<WilburFloatingText>();
                ft._mesh = MakeMesh(go, text, color, size, 20);
                ft._color = color;
            }
            catch (Exception)
            {
                // Text is only feedback; never let it break gameplay.
            }
        }

        // Cartoon style: thick black outline, pops in with a bounce and wobbles.
        public static void ShowCartoon(Vector3 pos, string text, Color color, float size)
        {
            try
            {
                if (!EnsureFont()) return;
                GameObject go = new GameObject("WilburBubble");
                go.transform.position = pos;
                WilburFloatingText ft = go.AddComponent<WilburFloatingText>();
                ft._mesh = MakeMesh(go, text, color, size, 22);
                ft._color = color;
                ft._cartoon = true;
                ft._spin = UnityEngine.Random.Range(-1f, 1f);

                float o = 0.012f * size;
                Vector2[] offs =
                {
                    new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
                    new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f)
                };
                foreach (Vector2 off in offs)
                {
                    GameObject child = new GameObject("Outline");
                    child.transform.SetParent(go.transform, false);
                    child.transform.localPosition = new Vector3(off.x * o, off.y * o, 0.01f);
                    ft._outlines.Add(MakeMesh(child, text, Color.black, size, 21));
                }
            }
            catch (Exception)
            {
            }
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_t >= Life)
            {
                Destroy(gameObject);
                return;
            }
            transform.position += Vector3.up * ((_cartoon ? 0.45f : 0.9f) * Time.deltaTime);
            Camera cam = Camera.main;
            Quaternion face = cam ? cam.transform.rotation : transform.rotation;
            float camScale = cam ? Mathf.Max(1f, Vector3.Distance(cam.transform.position, transform.position) / 14f) : 1f;
            transform.localScale = Vector3.one * camScale;
            if (_cartoon)
            {
                // Pop in (0 -> 1.3 -> 1) and wobble.
                float sc = _t < 0.12f ? Mathf.Lerp(0f, 1.3f, _t / 0.12f) : (_t < 0.24f ? Mathf.Lerp(1.3f, 1f, (_t - 0.12f) / 0.12f) : 1f);
                transform.localScale = Vector3.one * sc * camScale;
                face = face * Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 9f) * 7f * (1f - _t / Life) + _spin * 6f);
            }
            transform.rotation = face;
            float a = _t < Life - 0.5f ? 1f : Mathf.Clamp01((Life - _t) / 0.5f);
            if (_mesh) _mesh.color = new Color(_color.r, _color.g, _color.b, a);
            foreach (TextMesh o in _outlines) if (o) o.color = new Color(0f, 0f, 0f, a);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Custom icons: PNGs in the Icons folder next to the DLL (skills, buffs, portrait). Anything missing
    // falls back to the old generated icon, so a missing folder never breaks the mod.
    // ------------------------------------------------------------------------------------------------
    public static class WilburIcons
    {
        public static string Folder;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static bool _warned;

        public static Texture2D Texture(string name)
        {
            Texture2D tex;
            if (Textures.TryGetValue(name, out tex)) return tex;
            tex = null;
            try
            {
                string path = Folder != null ? System.IO.Path.Combine(Folder, name + ".png") : null;
                if (path != null && System.IO.File.Exists(path))
                {
                    tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, System.IO.File.ReadAllBytes(path)))
                    {
                        tex.name = "Wilbur_" + name;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        tex.filterMode = FilterMode.Bilinear;
                    }
                    else tex = null;
                }
                else if (!_warned)
                {
                    _warned = true;
                    if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur Icons folder not found or incomplete (" + Folder + "). Copy the Icons folder next to WilburMod.dll for the custom icons.");
                }
            }
            catch (Exception e)
            {
                tex = null;
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur could not load icon " + name + ": " + e.Message);
            }
            Textures[name] = tex;
            return tex;
        }

        public static Sprite Get(string name, Sprite fallback)
        {
            Sprite s;
            if (Sprites.TryGetValue(name, out s) && s) return s;
            Texture2D tex = Texture(name);
            if (!tex) return fallback;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "Wilbur_" + name;
            Sprites[name] = s;
            return s;
        }
    }

    // Screen shake through the game's own ShakeEmitter. Fields are set by reflection so a game update
    // can't break the build; if anything is missing it just doesn't shake.
    public static class WilburShake
    {
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static float _last;

        private static void Set(object o, string name, object value)
        {
            if (o == null) return;
            FieldInfo f = o.GetType().GetField(name, BF);
            if (f != null && f.FieldType.IsInstanceOfType(value)) f.SetValue(o, value);
        }

        public static void At(Vector3 pos, float amplitude, float duration, float radius)
        {
            float strength = WilburPlugin.ScreenShake != null ? WilburPlugin.ScreenShake.Value : 1f;
            if (strength <= 0f || amplitude <= 0f) return;
            // Many shakes in the same instant would stack into a blur; keep the biggest few.
            if (Time.time - _last < 0.05f && amplitude < 2f) return;
            _last = Time.time;
            try
            {
                GameObject go = new GameObject("WilburShake");
                go.transform.position = pos;
                go.SetActive(false);
                ShakeEmitter se = go.AddComponent<ShakeEmitter>();
                Set(se, "shakeOnStart", true);
                Set(se, "shakeOnEnable", false);
                Set(se, "duration", duration);
                Set(se, "radius", radius);
                Set(se, "scaleShakeRadiusWithLocalScale", false);
                Set(se, "amplitudeTimeDecay", true);
                FieldInfo wf = typeof(ShakeEmitter).GetField("wave", BF);
                if (wf != null)
                {
                    object wave = Activator.CreateInstance(wf.FieldType);
                    Set(wave, "amplitude", amplitude * strength);
                    Set(wave, "frequency", 18f);
                    Set(wave, "cycleOffset", UnityEngine.Random.value * 6f);
                    wf.SetValue(se, wave);
                }
                go.SetActive(true);
                Object.Destroy(go, duration + 0.5f);
            }
            catch (Exception)
            {
            }
        }
    }

    // A won prize pops out of Wilbur as the item's real icon on a glowing disc, then gets slurped up.
    public class WilburPrizeIcon : MonoBehaviour
    {
        private static Sprite _disc;
        private static Material _spriteMat;

        private CharacterBody _body;
        private Transform _icon;
        private Transform _glow;
        private SpriteRenderer _iconR;
        private SpriteRenderer _glowR;
        private Color _color;
        private float _t;
        private float _iconUnit = 1f;
        private float _glowUnit = 1f;
        private Vector3 _offset;
        private const float Life = 1.9f;

        private static Sprite Disc
        {
            get
            {
                if (_disc) return _disc;
                const int n = 64;
                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f - n * 0.5f) / (n * 0.5f);
                        float dy = (y + 0.5f - n * 0.5f) / (n * 0.5f);
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (d < 0.62f ? 1f : 0.85f);
                        float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.66f) * 18f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a + ring)));
                    }
                }
                tex.Apply();
                _disc = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return _disc;
            }
        }

        private static SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            if (!sr.sharedMaterial)
            {
                if (_spriteMat == null)
                {
                    Shader sh = Shader.Find("Sprites/Default");
                    if (sh) _spriteMat = new Material(sh);
                }
                if (_spriteMat) sr.sharedMaterial = _spriteMat;
            }
            sr.sortingOrder = order;
            return sr;
        }

        public static void Spawn(CharacterBody body, Sprite icon, Color color)
        {
            if (!body || !icon || WilburPlugin.PrizeIcons == null || !WilburPlugin.PrizeIcons.Value) return;
            try
            {
                GameObject root = new GameObject("WilburPrizeIcon");
                WilburPrizeIcon p = root.AddComponent<WilburPrizeIcon>();
                p._body = body;
                p._color = color;
                p._offset = new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0f, UnityEngine.Random.Range(-0.6f, 0.6f));
                p._glowR = MakeSprite(root.transform, "Glow", Disc, 30);
                p._glowR.color = new Color(color.r, color.g, color.b, 0.9f);
                p._glow = p._glowR.transform;
                p._glowUnit = 1f / Mathf.Max(0.01f, Disc.bounds.size.x);
                p._iconR = MakeSprite(root.transform, "Icon", icon, 31);
                p._icon = p._iconR.transform;
                p._icon.localPosition = new Vector3(0f, 0f, -0.02f);
                p._iconUnit = 1f / Mathf.Max(0.01f, icon.bounds.size.x);
                root.transform.position = body.corePosition;
            }
            catch (Exception)
            {
            }
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_t >= Life || !_body)
            {
                Destroy(gameObject);
                return;
            }

            float size = Mathf.Min(WilburGobble.SizeMul(_body), 30f);
            Vector3 head = _body.corePosition + Vector3.up * (0.6f + size * 0.55f);
            Vector3 top = head + Vector3.up * (1.6f + size * 0.25f) + _offset * Mathf.Sqrt(size);
            Vector3 pos;
            float scale;
            if (_t < 0.35f)
            {
                // Pop out of Wilbur with an overshoot.
                float k = _t / 0.35f;
                pos = Vector3.Lerp(head, top, 1f - (1f - k) * (1f - k));
                scale = Mathf.Lerp(0.2f, 1.35f, k);
            }
            else if (_t < Life - 0.45f)
            {
                // Hover and bob.
                float k = (_t - 0.35f) / 0.2f;
                pos = top + Vector3.up * Mathf.Sin(_t * 5f) * 0.12f;
                scale = Mathf.Lerp(1.35f, 1f, Mathf.Clamp01(k));
            }
            else
            {
                // Slurp: shrink back into the pig.
                float k = (_t - (Life - 0.45f)) / 0.45f;
                pos = Vector3.Lerp(top, head, k * k);
                scale = Mathf.Lerp(1f, 0.1f, k * k);
            }
            transform.position = pos;

            Camera cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;
            float camScale = cam ? Mathf.Max(1f, Vector3.Distance(cam.transform.position, pos) / 14f) : 1f;
            float world = 1.1f * scale * camScale;
            if (_icon) _icon.localScale = Vector3.one * world * _iconUnit;
            if (_glow)
            {
                _glow.localScale = Vector3.one * world * 1.9f * _glowUnit * (1f + Mathf.Sin(_t * 14f) * 0.06f);
                _glow.localRotation = Quaternion.Euler(0f, 0f, _t * 90f);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Loot labels: floating names (and prices) over chests, shrines, drones, shops, items on the ground and
    // the teleporter, plus a light pillar over dropped items, so a mountain-sized pig can still find loot.
    // Labels grow with distance (and Wilbur's size) so they stay readable.
    // ------------------------------------------------------------------------------------------------
    public class WilburLootLabels : MonoBehaviour
    {
        private class Label
        {
            public GameObject Target;
            public PurchaseInteraction Purchase;
            public GenericPickupController Pickup;
            public bool Teleporter;
            public GameObject Root;
            public TextMesh Text;
            public TextMesh Shadow;
            public Transform Beam;
            public Material BeamMat;
            public float Height;
            public string Last;
        }

        private static Font _font;
        private readonly Dictionary<GameObject, Label> _labels = new Dictionary<GameObject, Label>();
        private readonly List<GameObject> _dead = new List<GameObject>();
        private float _scan;

        private static CharacterBody LocalBody()
        {
            try
            {
                LocalUser u = LocalUserManager.GetFirstLocalUser();
                return u != null ? u.cachedBody : null;
            }
            catch (Exception) { return null; }
        }

        private void Update()
        {
            CharacterBody me = LocalBody();
            bool on = WilburPlugin.LootLabels != null && WilburPlugin.LootLabels.Value && me
                && (!WilburPlugin.LootLabelsOnlyWilbur.Value || me.GetComponent<WilburStatsController>());
            if (!on)
            {
                if (_labels.Count > 0) ClearAll();
                return;
            }

            _scan -= Time.deltaTime;
            if (_scan <= 0f)
            {
                _scan = 1f;
                Scan();
            }

            Camera cam = Camera.main;
            if (!cam) return;
            float size = WilburGobble.SizeMul(me);
            float range = WilburPlugin.LootLabelRange.Value * Mathf.Max(1f, Mathf.Sqrt(size));

            _dead.Clear();
            foreach (KeyValuePair<GameObject, Label> kv in _labels)
            {
                Label l = kv.Value;
                if (!kv.Key || !l.Root || !StillWanted(l)) { _dead.Add(kv.Key); continue; }

                Vector3 basePos = l.Target.transform.position;
                float dist = Vector3.Distance(cam.transform.position, basePos);
                bool visible = dist <= range;
                if (l.Root.activeSelf != visible) l.Root.SetActive(visible);
                if (!visible) continue;

                string text = BuildText(l);
                if (text != l.Last)
                {
                    l.Last = text;
                    if (l.Text) l.Text.text = text;
                    if (l.Shadow) l.Shadow.text = StripTags(text);
                }

                float camScale = Mathf.Clamp(dist / 16f, 0.6f, 40f);
                l.Root.transform.position = basePos + Vector3.up * (l.Height + 0.35f * camScale);
                l.Root.transform.rotation = cam.transform.rotation;
                l.Root.transform.localScale = Vector3.one * camScale;

                if (l.Beam)
                {
                    float h = Mathf.Min(6f + size * 0.6f, 80f);
                    float w = Mathf.Min(0.1f * Mathf.Max(1f, Mathf.Sqrt(size)) * Mathf.Max(1f, dist / 40f), 3f);
                    l.Beam.position = basePos + Vector3.up * (h * 0.5f);
                    l.Beam.rotation = Quaternion.identity;
                    l.Beam.localScale = new Vector3(w, h * 0.5f, w);
                }
            }
            foreach (GameObject g in _dead) Remove(g);
        }

        private static bool StillWanted(Label l)
        {
            if (l.Teleporter) return l.Target && l.Target.activeInHierarchy;
            if (l.Purchase) return l.Purchase.available && l.Target.activeInHierarchy;
            if (l.Pickup) return l.Target.activeInHierarchy;
            return false;
        }

        private void Scan()
        {
            try
            {
                foreach (PurchaseInteraction pi in Object.FindObjectsOfType<PurchaseInteraction>())
                {
                    if (!pi || !pi.available || _labels.ContainsKey(pi.gameObject)) continue;
                    Add(pi.gameObject, pi, null, false, ColorFor(pi));
                }
                foreach (GenericPickupController g in Object.FindObjectsOfType<GenericPickupController>())
                {
                    if (!g || _labels.ContainsKey(g.gameObject)) continue;
                    Add(g.gameObject, null, g, false, PickupColor(g));
                }
                TeleporterInteraction tp = TeleporterInteraction.instance;
                if (tp && !_labels.ContainsKey(tp.gameObject))
                    Add(tp.gameObject, null, null, true, new Color(1f, 0.35f, 0.3f));
            }
            catch (Exception)
            {
            }
        }

        private static Color ColorFor(PurchaseInteraction pi)
        {
            string n = pi.gameObject.name.ToLowerInvariant();
            if (n.Contains("chest") || n.Contains("lockbox") || n.Contains("cache")) return new Color(1f, 0.85f, 0.3f);
            if (n.Contains("shrine")) return new Color(0.45f, 0.95f, 1f);
            if (n.Contains("drone") || n.Contains("turret")) return new Color(0.6f, 0.8f, 1f);
            if (n.Contains("terminal") || n.Contains("shop") || n.Contains("printer") || n.Contains("duplicator")) return new Color(0.55f, 1f, 0.55f);
            if (n.Contains("lunar") || n.Contains("newt")) return new Color(0.5f, 0.75f, 1f);
            return new Color(0.92f, 0.92f, 0.92f);
        }

        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        // Read a field or property by name (works across game versions without touching obsolete members).
        private static object Member(object o, string name)
        {
            if (o == null) return null;
            Type t = o.GetType();
            FieldInfo f = t.GetField(name, BF);
            if (f != null) return f.GetValue(o);
            PropertyInfo p = t.GetProperty(name, BF);
            if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(o, null);
            return null;
        }

        // Newer game versions keep the pickup in a "pickupState" struct; older ones in "pickupIndex".
        private static PickupIndex IndexOf(GenericPickupController g)
        {
            foreach (string name in new[] { "pickupState", "pickup", "pickupIndex" })
            {
                try
                {
                    object v = Member(g, name);
                    if (v is PickupIndex && ((PickupIndex)v).isValid) return (PickupIndex)v;
                    object inner = Member(v, "pickupIndex");
                    if (inner is PickupIndex && ((PickupIndex)inner).isValid) return (PickupIndex)inner;
                }
                catch (Exception) { }
            }
            return PickupIndex.none;
        }

        private static PickupDef PickupDefOf(GenericPickupController g)
        {
            try
            {
                PickupIndex i = IndexOf(g);
                return i.isValid ? PickupCatalog.GetPickupDef(i) : null;
            }
            catch (Exception) { return null; }
        }

        private static Color PickupColor(GenericPickupController g)
        {
            PickupDef def = PickupDefOf(g);
            return def != null ? def.baseColor : Color.white;
        }

        private static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        private static string StripTags(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            bool tag = false;
            foreach (char ch in s)
            {
                if (ch == '<') { tag = true; continue; }
                if (ch == '>') { tag = false; continue; }
                if (!tag) sb.Append(ch);
            }
            return sb.ToString();
        }

        private static string CostText(PurchaseInteraction pi)
        {
            if (pi.cost <= 0) return null;
            switch (pi.costType)
            {
                case CostTypeIndex.Money: return "$" + pi.cost;
                case CostTypeIndex.PercentHealth: return pi.cost + "% HP";
                case CostTypeIndex.LunarCoin: return pi.cost + " LUNAR";
                case CostTypeIndex.WhiteItem: return pi.cost + " WHITE ITEM" + (pi.cost > 1 ? "S" : "");
                case CostTypeIndex.GreenItem: return pi.cost + " GREEN ITEM" + (pi.cost > 1 ? "S" : "");
                case CostTypeIndex.RedItem: return pi.cost + " RED ITEM" + (pi.cost > 1 ? "S" : "");
                case CostTypeIndex.BossItem: return pi.cost + " BOSS ITEM" + (pi.cost > 1 ? "S" : "");
                default: return null;
            }
        }

        private static string BuildText(Label l)
        {
            if (l.Teleporter) return "<color=#ff6655>TELEPORTER</color>";
            if (l.Purchase)
            {
                string name = null;
                try { name = l.Purchase.GetDisplayName(); } catch (Exception) { }
                if (string.IsNullOrEmpty(name)) name = l.Target.name.Replace("(Clone)", "");
                string cost = CostText(l.Purchase);
                string line = "<color=#" + Hex(ColorFor(l.Purchase)) + ">" + name.ToUpperInvariant() + "</color>";
                return cost != null ? line + "\n<color=#ffd84a>" + cost + "</color>" : line;
            }
            if (l.Pickup)
            {
                PickupDef def = PickupDefOf(l.Pickup);
                string name = null;
                try { name = l.Pickup.GetDisplayName(); } catch (Exception) { }
                if (string.IsNullOrEmpty(name)) name = def != null ? Language.GetString(def.nameToken) : "ITEM";
                Color c = def != null ? def.baseColor : Color.white;
                return "<color=#" + Hex(c) + ">" + name.ToUpperInvariant() + "</color>";
            }
            return "";
        }

        private void Add(GameObject target, PurchaseInteraction pi, GenericPickupController pickup, bool tp, Color color)
        {
            if (_font == null)
            {
                try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
                if (!_font) { try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _font = null; } }
                if (!_font) return;
            }

            Label l = new Label { Target = target, Purchase = pi, Pickup = pickup, Teleporter = tp };

            // Put the label just above the object's top.
            float top = 1.5f;
            try
            {
                Bounds b = new Bounds(target.transform.position, Vector3.zero);
                bool any = false;
                foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
                {
                    if (!r || r is ParticleSystemRenderer) continue;
                    if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; }
                }
                if (any) top = Mathf.Clamp(b.max.y - target.transform.position.y, 0.6f, tp ? 12f : 6f);
            }
            catch (Exception) { }
            l.Height = top;

            l.Root = new GameObject("WilburLootLabel");
            l.Shadow = MakeText(l.Root.transform, new Vector3(0.035f, -0.035f, 0.02f), Color.black, 40);
            l.Text = MakeText(l.Root.transform, Vector3.zero, Color.white, 41);

            if (pickup && WilburPlugin.LootBeams.Value)
            {
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "WilburLootBeam";
                WilburPlugin.StripCollider(beam);
                l.BeamMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(l.BeamMat, color, color, 0.7f);
                Renderer br = beam.GetComponent<Renderer>();
                br.sharedMaterial = l.BeamMat;
                br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                br.receiveShadows = false;
                l.Beam = beam.transform;
            }
            _labels[target] = l;
        }

        private static TextMesh MakeText(Transform parent, Vector3 offset, Color color, int order)
        {
            GameObject go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.characterSize = 0.035f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.richText = true;
            tm.color = color;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr)
            {
                mr.sharedMaterial = _font.material;
                mr.sortingOrder = order;
            }
            return tm;
        }

        private void Remove(GameObject key)
        {
            Label l;
            if (key == null)
            {
                // Destroyed targets: find by reference.
                List<GameObject> gone = new List<GameObject>();
                foreach (KeyValuePair<GameObject, Label> kv in _labels) if (!kv.Key) gone.Add(kv.Key);
                foreach (GameObject g in gone)
                {
                    if (_labels.TryGetValue(g, out l)) Kill(l);
                    _labels.Remove(g);
                }
                return;
            }
            if (_labels.TryGetValue(key, out l)) Kill(l);
            _labels.Remove(key);
        }

        private static void Kill(Label l)
        {
            if (l.Root) Object.Destroy(l.Root);
            if (l.Beam) Object.Destroy(l.Beam.gameObject);
            if (l.BeamMat) Object.Destroy(l.BeamMat);
        }

        private void ClearAll()
        {
            foreach (Label l in _labels.Values) Kill(l);
            _labels.Clear();
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Piñata pig: candy bursts out of Wilbur when he gets hit and when he deflates. Purely visual, runs
    // on every machine. Wrapped candies, lollipops and gumballs fly out, bounce on the ground and fade.
    // ------------------------------------------------------------------------------------------------
    public static class WilburCandy
    {
        private static readonly Color[] Colors =
        {
            new Color(1f, 0.25f, 0.35f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.92f, 0.2f), new Color(0.3f, 0.9f, 0.35f),
            new Color(0.25f, 0.65f, 1f), new Color(0.7f, 0.35f, 1f), new Color(1f, 0.5f, 0.8f)
        };
        private static Material[] _mats;
        private static Material _white;
        public static int Alive;
        private const int MaxAlive = 180;

        private static Material[] Mats
        {
            get
            {
                if (_mats == null)
                {
                    _mats = new Material[Colors.Length];
                    for (int i = 0; i < Colors.Length; i++)
                    {
                        _mats[i] = WilburPlugin.MakeMat();
                        WilburPlugin.Tint(_mats[i], Colors[i], Colors[i], 0.3f);
                    }
                    _white = WilburPlugin.MakeMat();
                    WilburPlugin.Tint(_white, new Color(0.97f, 0.97f, 0.95f), new Color(0.2f, 0.2f, 0.2f), 0.2f);
                }
                return _mats;
            }
        }

        private static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            WilburPlugin.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static GameObject MakeCandy()
        {
            Material[] mats = Mats;
            Material m = mats[UnityEngine.Random.Range(0, mats.Length)];
            GameObject root = new GameObject("WilburCandy");
            int kind = UnityEngine.Random.Range(0, 3);
            if (kind == 0)
            {
                // Wrapped candy: a sweet with twisted wrapper ends.
                Part(root.transform, PrimitiveType.Sphere, Vector3.zero, new Vector3(1f, 0.7f, 0.7f), Vector3.zero, m);
                Part(root.transform, PrimitiveType.Cube, new Vector3(-0.62f, 0f, 0f), new Vector3(0.35f, 0.55f, 0.08f), new Vector3(0f, 0f, 0f), m);
                Part(root.transform, PrimitiveType.Cube, new Vector3(0.62f, 0f, 0f), new Vector3(0.35f, 0.55f, 0.08f), new Vector3(0f, 0f, 0f), m);
            }
            else if (kind == 1)
            {
                // Lollipop.
                Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.9f, 0.08f, 0.9f), new Vector3(90f, 0f, 0f), m);
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.75f, 0f), new Vector3(0.1f, 0.4f, 0.1f), Vector3.zero, _white);
            }
            else
            {
                // Gumball.
                Part(root.transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.75f, Vector3.zero, m);
            }
            return root;
        }

        // count: how many pieces. size: Wilbur's size multiplier. power: launch speed.
        public static void Burst(Vector3 from, int count, float size, float power)
        {
            if (WilburPlugin.CandyPinata == null || !WilburPlugin.CandyPinata.Value) return;
            count = Mathf.RoundToInt(count * Mathf.Max(0f, WilburPlugin.CandyAmount.Value));
            count = Mathf.Min(count, MaxAlive - Alive);
            if (count <= 0) return;
            float s = Mathf.Sqrt(Mathf.Max(1f, size));
            try
            {
                for (int i = 0; i < count; i++)
                {
                    GameObject c = MakeCandy();
                    c.transform.position = from + UnityEngine.Random.insideUnitSphere * 0.3f * s;
                    c.transform.rotation = UnityEngine.Random.rotation;
                    c.transform.localScale = Vector3.one * 0.22f * s * UnityEngine.Random.Range(0.8f, 1.25f);
                    WilburCandyPiece p = c.AddComponent<WilburCandyPiece>();
                    Vector3 dir = UnityEngine.Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 0.8f + 0.6f;
                    p.Velocity = dir.normalized * UnityEngine.Random.Range(5f, 11f) * power * s;
                    p.Spin = UnityEngine.Random.onUnitSphere * UnityEngine.Random.Range(300f, 900f);
                    p.Radius = 0.12f * s;
                    p.Gravity = 22f * Mathf.Min(s, 4f);
                    p.Life = UnityEngine.Random.Range(2.5f, 4f);
                    Alive++;
                }
            }
            catch (Exception)
            {
            }
        }
    }

    public class WilburCandyPiece : MonoBehaviour
    {
        public Vector3 Velocity;
        public Vector3 Spin;
        public float Radius = 0.1f;
        public float Gravity = 22f;
        public float Life = 3f;
        public bool Counted = true;
        private float _t;
        private bool _resting;
        private Vector3 _scale0;

        private void Start()
        {
            _scale0 = transform.localScale;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_t >= Life)
            {
                Destroy(gameObject);
                return;
            }

            if (!_resting)
            {
                Velocity += Vector3.down * Gravity * dt;
                Vector3 step = Velocity * dt;
                RaycastHit hit;
                if (step.sqrMagnitude > 0f && Physics.Raycast(transform.position, step.normalized, out hit, step.magnitude + Radius, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    // Bounce, losing energy each time; settle when slow.
                    transform.position = hit.point + hit.normal * Radius;
                    Velocity = Vector3.Reflect(Velocity, hit.normal) * 0.45f;
                    Velocity -= Vector3.Project(Velocity, hit.normal) * 0.1f;
                    Spin *= 0.5f;
                    if (Velocity.magnitude < 1.5f)
                    {
                        _resting = true;
                        Velocity = Vector3.zero;
                    }
                }
                else
                {
                    transform.position += step;
                }
                transform.Rotate(Spin * dt, Space.World);
            }

            // Pop in, then shrink away at the end.
            float k = _t < 0.12f ? _t / 0.12f : (_t > Life - 0.6f ? (Life - _t) / 0.6f : 1f);
            if (_scale0 != Vector3.zero) transform.localScale = _scale0 * Mathf.Clamp01(k);
        }

        private void OnDestroy()
        {
            if (Counted) WilburCandy.Alive = Mathf.Max(0, WilburCandy.Alive - 1);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Clean glowing lines (LineRenderer) for shockwave rings and the rainbow ball's halos.
    // ------------------------------------------------------------------------------------------------
    public static class WilburLines
    {
        private static Material _mat;
        private static bool _tried;

        public static Material Mat
        {
            get
            {
                if (_mat || _tried) return _mat;
                _tried = true;
                foreach (string name in new[] { "Sprites/Default", "UI/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit" })
                {
                    Shader sh = Shader.Find(name);
                    if (sh) { _mat = new Material(sh); break; }
                }
                if (!_mat && WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur: no line shader found, rings fall back to solid shapes.");
                return _mat;
            }
        }

        // A circle in the transform's local XY plane. flat = lies in that plane (TransformZ), otherwise faces the camera.
        public static LineRenderer Circle(Transform parent, string name, int segments, float radius, float width, Color color, bool flat)
        {
            if (!Mat) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Mat;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.widthMultiplier = width;
            lr.startColor = color;
            lr.endColor = color;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (flat) lr.alignment = LineAlignment.TransformZ;
            SetCircle(lr, radius);
            return lr;
        }

        public static void SetCircle(LineRenderer lr, float radius)
        {
            if (!lr) return;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
        }
    }

    // A flat ring that races outward over the ground and fades (earthquake shockwave).
    public class WilburRingWave : MonoBehaviour
    {
        private LineRenderer _outer;
        private LineRenderer _inner;
        private float _r0;
        private float _r1;
        private float _t;
        private float _life;
        private Color _color;
        private float _width;

        public static void Spawn(Vector3 center, float r0, float r1, float life, Color color, float width)
        {
            RaycastHit hit;
            if (Physics.Raycast(center + Vector3.up * 3f, Vector3.down, out hit, 20f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                center = hit.point;
            GameObject go = new GameObject("WilburRingWave");
            go.transform.position = center + Vector3.up * 0.25f;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            WilburRingWave w = go.AddComponent<WilburRingWave>();
            w._outer = WilburLines.Circle(go.transform, "Outer", 64, r0, width, color, true);
            w._inner = WilburLines.Circle(go.transform, "Inner", 64, r0 * 0.9f, width * 0.5f, color * 0.8f, true);
            if (!w._outer) { Object.Destroy(go); return; }
            w._r0 = r0;
            w._r1 = r1;
            w._life = life;
            w._color = color;
            w._width = width;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / _life);
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            float e = 1f - (1f - k) * (1f - k);
            float r = Mathf.Lerp(_r0, _r1, e);
            Color c = new Color(_color.r, _color.g, _color.b, 1f - k);
            WilburLines.SetCircle(_outer, r);
            WilburLines.SetCircle(_inner, r * 0.88f);
            if (_outer) { _outer.startColor = c; _outer.endColor = c; _outer.widthMultiplier = _width * (1f - 0.6f * k); }
            if (_inner) { Color ci = c * 0.8f; ci.a = c.a * 0.7f; _inner.startColor = ci; _inner.endColor = ci; _inner.widthMultiplier = _width * 0.5f * (1f - 0.6f * k); }
        }
    }

    // The earthquake made visible: a shockwave racing out to the quake's edge, rocks thrown up all over the
    // area, and cracks splitting the ground.
    public static class WilburQuakeFx
    {
        private static Material _rockMat;
        private static Material _crackMat;

        public static void Pulse(Vector3 center, float radius, float size)
        {
            if (_rockMat == null)
            {
                _rockMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_rockMat, new Color(0.42f, 0.33f, 0.25f), Color.black, 0f);
                _crackMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_crackMat, new Color(0.08f, 0.05f, 0.03f), Color.black, 0f);
            }
            float s = Mathf.Sqrt(Mathf.Max(1f, size));

            WilburRingWave.Spawn(center, radius * 0.1f, radius, 0.7f, new Color(0.85f, 0.6f, 0.3f), 0.35f * s);

            // Rocks flung up across the whole quake area.
            int rocks = Mathf.Clamp(Mathf.RoundToInt(radius * 0.6f), 6, 24);
            for (int i = 0; i < rocks; i++)
            {
                if (WilburCandy.Alive >= 220) break;
                Vector2 c = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 p = center + new Vector3(c.x, 0.3f, c.y);
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(rock);
                rock.GetComponent<Renderer>().sharedMaterial = i % 3 == 0 ? _rockMat : WilburBurrowMound.Dirt[i % WilburBurrowMound.Dirt.Length];
                rock.transform.position = p;
                rock.transform.rotation = UnityEngine.Random.rotation;
                rock.transform.localScale = new Vector3(UnityEngine.Random.Range(0.3f, 0.7f), UnityEngine.Random.Range(0.25f, 0.5f), UnityEngine.Random.Range(0.3f, 0.7f)) * s;
                WilburCandyPiece piece = rock.AddComponent<WilburCandyPiece>();
                piece.Velocity = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(7f, 13f), UnityEngine.Random.Range(-2f, 2f)) * Mathf.Min(s, 3f);
                piece.Spin = UnityEngine.Random.onUnitSphere * 400f;
                piece.Radius = 0.2f * s;
                piece.Gravity = 28f * Mathf.Min(s, 3f);
                piece.Life = UnityEngine.Random.Range(1.2f, 2f);
                WilburCandy.Alive++;
            }

            // Cracks radiating out from Wilbur.
            int cracks = 7;
            float baseAng = UnityEngine.Random.value * 360f;
            for (int i = 0; i < cracks; i++)
            {
                float ang = baseAng + i * 360f / cracks + UnityEngine.Random.Range(-15f, 15f);
                float len = radius * UnityEngine.Random.Range(0.45f, 0.95f);
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                Vector3 mid = center + dir * len * 0.5f;
                RaycastHit hit;
                if (Physics.Raycast(mid + Vector3.up * 4f, Vector3.down, out hit, 25f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    mid = hit.point;
                GameObject crack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                WilburPlugin.StripCollider(crack);
                crack.GetComponent<Renderer>().sharedMaterial = _crackMat;
                crack.transform.position = mid + Vector3.up * 0.04f;
                crack.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, UnityEngine.Random.Range(-6f, 6f), 0f);
                crack.transform.localScale = new Vector3(0.18f * s, 0.06f, len);
                Object.Destroy(crack, 2.5f);
            }
        }
    }

    // Big announcer text pinned to the top of the screen (Halo / Dota style). One at a time: a new call replaces
    // the old one. "Rage" calls (RAMPAGE) are red, huge and shake.
    public class WilburAnnouncerText : MonoBehaviour
    {
        private static Font _font;
        private static WilburAnnouncerText _current;
        private TextMesh _main;
        private readonly List<TextMesh> _outline = new List<TextMesh>();
        private TextMesh _sub;
        private Color _color;
        private bool _rage;
        private float _t;
        private float _scale;
        private const float Life = 2.4f;

        private static TextMesh Mesh(Transform parent, Vector3 offset, string text, Color color, float size, int order)
        {
            GameObject go = new GameObject("T");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.fontSize = 72;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr) { mr.sharedMaterial = _font.material; mr.sortingOrder = order; }
            return tm;
        }

        public static void Show(string text, string subtitle, Color color, float scale, bool rage)
        {
            try
            {
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
                    if (!_font) { try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { _font = null; } }
                    if (!_font) return;
                }
                // Don't stomp on a fresh RAMPAGE with a smaller call.
                if (_current && _current._rage && !rage && _current._t < 1.4f) return;
                if (_current) Destroy(_current.gameObject);
                GameObject go = new GameObject("WilburAnnouncer");
                WilburAnnouncerText a = go.AddComponent<WilburAnnouncerText>();
                a._color = color;
                a._rage = rage;
                a._scale = scale;
                float size = 0.04f;
                float o = 0.007f;
                Vector2[] offs =
                {
                    new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
                    new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f)
                };
                foreach (Vector2 off in offs)
                    a._outline.Add(Mesh(go.transform, new Vector3(off.x * o, off.y * o, 0.005f), text, Color.black, size, 60));
                a._main = Mesh(go.transform, Vector3.zero, text, color, size, 61);
                if (!string.IsNullOrEmpty(subtitle))
                    a._sub = Mesh(go.transform, new Vector3(0f, -0.27f, 0f), subtitle, new Color(1f, 0.85f, 0.9f), size * 0.45f, 61);
                _current = a;
                a.LateUpdate();
            }
            catch (Exception)
            {
            }
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_t >= Life)
            {
                Destroy(gameObject);
                return;
            }
            Camera cam = Camera.main;
            if (!cam) return;

            // Slam in big, settle, then fade.
            float pop = _t < 0.1f ? Mathf.Lerp(2.2f, 0.9f, _t / 0.1f) : (_t < 0.2f ? Mathf.Lerp(0.9f, 1f, (_t - 0.1f) / 0.1f) : 1f);
            Vector3 jitter = Vector3.zero;
            float tilt = 0f;
            if (_rage)
            {
                // Dota RAMPAGE: violent shake that calms down a little over time.
                float amp = 0.035f * (1f - 0.5f * _t / Life);
                jitter = new Vector3(UnityEngine.Random.Range(-amp, amp), UnityEngine.Random.Range(-amp, amp), 0f);
                tilt = UnityEngine.Random.Range(-4f, 4f);
                pop *= 1f + Mathf.Abs(Mathf.Sin(_t * 22f)) * 0.06f;
            }
            else
            {
                tilt = Mathf.Sin(_t * 8f) * 2f * Mathf.Max(0f, 1f - _t);
            }

            Transform c = cam.transform;
            transform.position = c.position + c.forward * 3f + c.up * 0.62f + c.right * jitter.x + c.up * jitter.y;
            transform.rotation = c.rotation * Quaternion.Euler(0f, 0f, tilt);
            transform.localScale = Vector3.one * _scale * pop;

            float a = _t < Life - 0.5f ? 1f : Mathf.Clamp01((Life - _t) / 0.5f);
            Color col = _color;
            if (_rage) col = Color.Lerp(new Color(1f, 0.05f, 0.05f), new Color(1f, 0.45f, 0.2f), Mathf.Abs(Mathf.Sin(_t * 10f)) * 0.5f);
            if (_main) _main.color = new Color(col.r, col.g, col.b, a);
            if (_sub) _sub.color = new Color(1f, 0.85f, 0.9f, a);
            foreach (TextMesh o in _outline) if (o) o.color = new Color(0f, 0f, 0f, a);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // What happens to the things Wilbur eats: they squish while he sucks on them, and a gobbled enemy
    // shrinks away into nothing (slurped up) instead of just falling over.
    // ------------------------------------------------------------------------------------------------
    public class WilburVictimFx : MonoBehaviour
    {
        private Vector3 _scale0;
        private float _t;
        private float _life;
        private bool _slurp;
        private HealthComponent _hc;
        private bool _hidden;

        private static Transform ModelOf(CharacterBody body)
        {
            if (!body || !body.modelLocator) return null;
            return body.modelLocator.modelTransform;
        }

        private static WilburVictimFx Get(CharacterBody body)
        {
            Transform model = ModelOf(body);
            if (!model) return null;
            WilburVictimFx fx = model.GetComponent<WilburVictimFx>();
            if (!fx)
            {
                fx = model.gameObject.AddComponent<WilburVictimFx>();
                fx._scale0 = model.localScale;
            }
            fx._hc = body.healthComponent;
            return fx;
        }

        // Squish in rhythm with Wilbur's sucking.
        public static void Squish(CharacterBody body, float seconds)
        {
            try
            {
                WilburVictimFx fx = Get(body);
                if (!fx || fx._slurp) return;
                fx._t = 0f;
                fx._life = seconds;
            }
            catch (Exception) { }
        }

        // Gobbled: shrink to nothing.
        public static void Slurp(CharacterBody body)
        {
            try
            {
                WilburVictimFx fx = Get(body);
                if (!fx) return;
                fx._slurp = true;
                fx._t = 0f;
                fx._life = 0.3f;
            }
            catch (Exception) { }
        }

        private void LateUpdate()
        {
            _t += Time.deltaTime;
            if (_slurp)
            {
                float k = Mathf.Clamp01(_t / _life);
                if (k < 1f)
                {
                    // Stretch thin and shrink, like being sucked through a straw.
                    float sh = 1f - k * k;
                    transform.localScale = new Vector3(_scale0.x * sh * 0.7f, _scale0.y * Mathf.Lerp(1.3f, 0f, k), _scale0.z * sh * 0.7f);
                }
                else if (!_hidden)
                {
                    _hidden = true;
                    transform.localScale = _scale0 * 0.001f;
                    foreach (Renderer r in GetComponentsInChildren<Renderer>()) if (r) r.enabled = false;
                }
                // Safety: if it somehow survived, give it its body back.
                if (_t > 0.8f && _hc && _hc.alive) Restore();
                return;
            }

            if (_t >= _life)
            {
                Restore();
                return;
            }
            float p = Mathf.Sin(_t * 26f) * 0.13f * (1f - _t / _life);
            transform.localScale = new Vector3(_scale0.x * (1f + p), _scale0.y * (1f - p), _scale0.z * (1f + p));
        }

        private void Restore()
        {
            transform.localScale = _scale0;
            if (_hidden) foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) if (r) r.enabled = true;
            Destroy(this);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Truffle Burrow visuals: a lumpy, churning dirt mound with Wilbur's pink ears poking out, racing along
    // the ground like a cartoon gopher and leaving a line of dirt bumps behind it.
    // ------------------------------------------------------------------------------------------------
    public class WilburBurrowMound : MonoBehaviour
    {
        private static Material[] _dirt;
        private static Material _pink;
        private static Material _pinkInner;

        private CharacterBody _body;
        private readonly List<Transform> _lumps = new List<Transform>();
        private readonly List<Vector3> _lumpScale = new List<Vector3>();
        private readonly List<float> _lumpPhase = new List<float>();
        private Transform _earL;
        private Transform _earR;
        private Vector3 _lastTrail;
        private float _t;
        private bool _closing;
        private float _closeT;
        private float _size = 1f;

        public static Material[] Dirt
        {
            get
            {
                if (_dirt == null)
                {
                    Color[] tones = { new Color(0.5f, 0.34f, 0.19f), new Color(0.4f, 0.27f, 0.15f), new Color(0.6f, 0.43f, 0.26f), new Color(0.3f, 0.2f, 0.11f) };
                    _dirt = new Material[tones.Length];
                    for (int i = 0; i < tones.Length; i++)
                    {
                        _dirt[i] = WilburPlugin.MakeMat();
                        WilburPlugin.Tint(_dirt[i], tones[i], Color.black, 0f);
                    }
                    _pink = WilburPlugin.MakeMat();
                    WilburPlugin.Tint(_pink, new Color(1f, 0.67f, 0.75f), new Color(0.3f, 0.12f, 0.15f), 0.25f);
                    _pinkInner = WilburPlugin.MakeMat();
                    WilburPlugin.Tint(_pinkInner, new Color(0.92f, 0.42f, 0.56f), new Color(0.2f, 0.06f, 0.1f), 0.2f);
                }
                return _dirt;
            }
        }

        private static Transform Ball(Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            WilburPlugin.StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        public static WilburBurrowMound Spawn(CharacterBody body)
        {
            GameObject root = new GameObject("WilburBurrowMound");
            WilburBurrowMound m = root.AddComponent<WilburBurrowMound>();
            m._body = body;
            m.Build();
            m.Follow(0f);
            m._lastTrail = root.transform.position;
            return m;
        }

        private void AddLump(Vector3 pos, Vector3 scale)
        {
            Material[] d = Dirt;
            Transform t = Ball(transform, pos, scale, new Vector3(0f, UnityEngine.Random.Range(0f, 360f), 0f), d[UnityEngine.Random.Range(0, d.Length)]);
            _lumps.Add(t);
            _lumpScale.Add(scale);
            _lumpPhase.Add(UnityEngine.Random.value * 6.28f);
        }

        private void Build()
        {
            Material[] d = Dirt;
            AddLump(new Vector3(0f, 0.1f, 0f), new Vector3(1.15f, 0.62f, 1.3f));
            AddLump(new Vector3(0f, 0.06f, 0.5f), new Vector3(0.75f, 0.42f, 0.6f));
            AddLump(new Vector3(0f, 0.05f, -0.55f), new Vector3(0.85f, 0.38f, 0.75f));
            AddLump(new Vector3(-0.45f, 0.03f, 0.05f), new Vector3(0.55f, 0.32f, 0.75f));
            AddLump(new Vector3(0.45f, 0.03f, -0.05f), new Vector3(0.55f, 0.32f, 0.75f));
            // Little clumps and pebbles on top.
            for (int i = 0; i < 5; i++)
            {
                Vector2 c = UnityEngine.Random.insideUnitCircle * 0.35f;
                AddLump(new Vector3(c.x, 0.33f, c.y), Vector3.one * UnityEngine.Random.Range(0.12f, 0.22f));
            }

            // His ears poke out of the top of the mound.
            GameObject earL = new GameObject("EarL");
            earL.transform.SetParent(transform, false);
            earL.transform.localPosition = new Vector3(-0.17f, 0.36f, 0.22f);
            Ball(earL.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.24f, 0.05f), Vector3.zero, _pink);
            Ball(earL.transform, new Vector3(0f, 0.1f, 0.02f), new Vector3(0.1f, 0.15f, 0.03f), Vector3.zero, _pinkInner);
            _earL = earL.transform;
            GameObject earR = new GameObject("EarR");
            earR.transform.SetParent(transform, false);
            earR.transform.localPosition = new Vector3(0.17f, 0.36f, 0.22f);
            Ball(earR.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.24f, 0.05f), Vector3.zero, _pink);
            Ball(earR.transform, new Vector3(0f, 0.1f, 0.02f), new Vector3(0.1f, 0.15f, 0.03f), Vector3.zero, _pinkInner);
            _earR = earR.transform;
        }

        private void Follow(float dt)
        {
            if (!_body) return;
            Vector3 p = _body.footPosition;
            RaycastHit hit;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out hit, 4f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                p = hit.point;
            transform.position = p;
            Vector3 fwd = _body.characterDirection ? _body.characterDirection.forward : _body.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.01f)
            {
                Quaternion want = Quaternion.LookRotation(fwd.normalized);
                transform.rotation = dt > 0f ? Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-10f * dt)) : want;
            }
        }

        public void Close()
        {
            _closing = true;
            _closeT = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (!_body)
            {
                Destroy(gameObject);
                return;
            }
            _size = Mathf.Lerp(_size, WilburGobble.SizeMul(_body), 1f - Mathf.Exp(-6f * dt));
            Follow(dt);

            // Pop up with an overshoot; sink fast when closing.
            float pop;
            if (_closing)
            {
                _closeT += dt;
                pop = Mathf.Clamp01(1f - _closeT / 0.15f);
                if (pop <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                float k = Mathf.Clamp01(_t / 0.25f);
                pop = k < 1f ? Mathf.Sin(k * Mathf.PI * 0.5f) * (1f + 0.3f * Mathf.Sin(k * Mathf.PI)) : 1f;
            }
            transform.localScale = Vector3.one * _size * pop;

            // Churning: every lump bulges on its own rhythm, faster when moving.
            Vector3 v = _body.characterMotor ? _body.characterMotor.velocity : Vector3.zero;
            v.y = 0f;
            float churn = 1f + Mathf.Clamp01(v.magnitude / 8f);
            for (int i = 0; i < _lumps.Count; i++)
            {
                if (!_lumps[i]) continue;
                float w = Mathf.Sin(_t * 12f * churn + _lumpPhase[i]);
                Vector3 b = _lumpScale[i];
                _lumps[i].localScale = new Vector3(b.x * (1f - 0.06f * w), b.y * (1f + 0.14f * w), b.z * (1f - 0.06f * w));
            }

            // Ears peek and wiggle.
            float peek = 0.03f * Mathf.Sin(_t * 5f);
            if (_earL)
            {
                _earL.localRotation = Quaternion.Euler(10f, 0f, 25f + Mathf.Sin(_t * 9f) * 15f);
                _earL.localPosition = new Vector3(-0.17f, 0.36f + peek, 0.22f);
            }
            if (_earR)
            {
                _earR.localRotation = Quaternion.Euler(10f, 0f, -25f - Mathf.Sin(_t * 9f + 1f) * 15f);
                _earR.localPosition = new Vector3(0.17f, 0.36f + peek, 0.22f);
            }

            // A line of dirt bumps behind him, like a cartoon gopher tunnel.
            float spacing = Mathf.Max(1.2f, 0.9f * Mathf.Sqrt(_size));
            if (!_closing && (transform.position - _lastTrail).sqrMagnitude > spacing * spacing)
            {
                _lastTrail = transform.position;
                WilburTrailBump.Spawn(transform.position - transform.forward * 0.6f * _size, transform.rotation, _size);
                WilburFx.SpawnDirtBurst(transform.position + Vector3.up * 0.3f * _size, 2, 3f + Mathf.Sqrt(_size));
            }
        }
    }

    // One dirt bump of the tunnel trail: settles and sinks back into the ground.
    public class WilburTrailBump : MonoBehaviour
    {
        private float _t;
        private const float Life = 2.5f;
        private Vector3 _scale0;
        private Vector3 _pos0;

        public static void Spawn(Vector3 pos, Quaternion rot, float size)
        {
            GameObject root = new GameObject("WilburTrailBump");
            root.transform.position = pos;
            root.transform.rotation = rot * Quaternion.Euler(0f, UnityEngine.Random.Range(-15f, 15f), 0f);
            float s = Mathf.Sqrt(Mathf.Max(1f, size)) * 0.8f;
            root.transform.localScale = Vector3.one * s;
            Material[] d = WilburBurrowMound.Dirt;
            for (int i = 0; i < 2; i++)
            {
                GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(b);
                b.transform.SetParent(root.transform, false);
                b.transform.localPosition = new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), 0f, i * 0.35f - 0.15f);
                b.transform.localScale = new Vector3(0.7f, 0.32f, 0.6f) * UnityEngine.Random.Range(0.8f, 1.1f);
                Renderer r = b.GetComponent<Renderer>();
                r.sharedMaterial = d[UnityEngine.Random.Range(0, d.Length)];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            WilburTrailBump t = root.AddComponent<WilburTrailBump>();
            t._scale0 = root.transform.localScale;
            t._pos0 = pos;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= Life)
            {
                Destroy(gameObject);
                return;
            }
            float k = _t / Life;
            // Flatten and sink over its life.
            transform.localScale = new Vector3(_scale0.x, _scale0.y * (1f - k), _scale0.z);
            transform.position = _pos0 + Vector3.down * 0.15f * k * _scale0.y;
        }
    }

    public static class WilburFx
    {
        // A ring of coloured puffs bursting outward (used when Super Piggy starts).
        private static Material _ringMat;

        public static void SpawnPuffRing(Vector3 center, Color color)
        {
            if (_ringMat == null) _ringMat = WilburPlugin.MakeMat();
            Material mat = _ringMat;
            WilburPlugin.Tint(mat, color, color * 0.6f, 0.5f);
            for (int i = 0; i < 14; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(puff);
                puff.transform.position = center;
                puff.transform.localScale = Vector3.one * 0.3f;
                puff.GetComponent<Renderer>().sharedMaterial = mat;
                float ang = i * Mathf.PI * 2f / 14f;
                WilburFlyingPuff fp = puff.AddComponent<WilburFlyingPuff>();
                fp.Velocity = new Vector3(Mathf.Cos(ang), 0.3f, Mathf.Sin(ang)) * 9f;
                fp.Life = 0.45f;
            }
        }

        private static Material _burpMat;

        // The shockwave: a quick spray of pale puffs in the cone.
        public static void SpawnBurp(Vector3 origin, Vector3 dir, float halfAngle)
        {
            dir = dir.normalized;
            if (_burpMat == null)
            {
                _burpMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_burpMat, new Color(0.9f, 0.93f, 1f), new Color(0.5f, 0.6f, 0.8f), 0.3f);
            }

            for (int i = 0; i < 10; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(puff);
                puff.transform.position = origin;
                puff.transform.localScale = Vector3.one * 0.35f;
                puff.GetComponent<Renderer>().sharedMaterial = _burpMat;

                Vector3 axis = Vector3.Cross(dir, UnityEngine.Random.onUnitSphere);
                if (axis.sqrMagnitude < 0.001f) axis = Vector3.up;
                Vector3 d = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, halfAngle), axis.normalized) * dir;

                WilburFlyingPuff fp = puff.AddComponent<WilburFlyingPuff>();
                fp.Velocity = d * UnityEngine.Random.Range(18f, 30f);
                fp.Life = UnityEngine.Random.Range(0.3f, 0.5f);
            }
        }

        // Pink puffs flying from a gobbled enemy into Wilbur's mouth.
        private static Material _suckMat;

        public static void SpawnSuck(Vector3 from, Vector3 to)
        {
            if (_suckMat == null)
            {
                _suckMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_suckMat, new Color(1f, 0.55f, 0.7f), new Color(0.6f, 0.2f, 0.3f), 0.4f);
            }
            Material mat = _suckMat;
            Vector3 d = to - from;
            for (int i = 0; i < 6; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(puff);
                puff.transform.position = from + UnityEngine.Random.insideUnitSphere * 0.6f;
                puff.transform.localScale = Vector3.one * 0.35f;
                puff.GetComponent<Renderer>().sharedMaterial = mat;
                WilburFlyingPuff fp = puff.AddComponent<WilburFlyingPuff>();
                fp.Life = 0.35f;
                fp.Velocity = d / fp.Life;
            }
        }

        private static Material _mudMat;

        // A flat brown mud puddle on the ground that fades away.
        public static void SpawnMud(Vector3 pos, float radius, float life)
        {
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out hit, 30f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.05f;
            if (_mudMat == null)
            {
                _mudMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_mudMat, new Color(0.3f, 0.19f, 0.1f), new Color(0.05f, 0.03f, 0.01f), 0f);
            }
            GameObject mud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mud.name = "WilburMud";
            WilburPlugin.StripCollider(mud);
            mud.transform.position = pos;
            mud.transform.localScale = new Vector3(radius * 2f, 0.03f, radius * 2f);
            mud.GetComponent<Renderer>().sharedMaterial = _mudMat;
            Object.Destroy(mud, life);
        }

        private static Material[] _pukeMats;

        // The rainbow puke: a gush of rainbow blobs out of Wilbur's mouth.
        public static void SpawnPuke(Vector3 mouth, Vector3 dir, float size)
        {
            if (_pukeMats == null)
            {
                Color[] b = WilburRainbowBeam.Bands;
                _pukeMats = new Material[b.Length];
                for (int i = 0; i < b.Length; i++)
                {
                    _pukeMats[i] = WilburPlugin.MakeMat();
                    WilburPlugin.Tint(_pukeMats[i], b[i], b[i], 0.5f);
                }
            }
            float s = Mathf.Sqrt(Mathf.Max(1f, size));
            dir = dir.normalized;
            for (int i = 0; i < 26; i++)
            {
                GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                StripColliderSafe(blob);
                blob.transform.position = mouth + UnityEngine.Random.insideUnitSphere * 0.2f * s;
                blob.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.18f, 0.4f) * s;
                Renderer r = blob.GetComponent<Renderer>();
                r.sharedMaterial = _pukeMats[i % _pukeMats.Length];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                WilburCandyPiece p = blob.AddComponent<WilburCandyPiece>();
                p.Velocity = (dir + UnityEngine.Random.insideUnitSphere * 0.25f).normalized * UnityEngine.Random.Range(10f, 22f) * Mathf.Min(s, 4f);
                p.Spin = Vector3.zero;
                p.Radius = 0.1f * s;
                p.Gravity = 18f * Mathf.Min(s, 3f);
                p.Life = UnityEngine.Random.Range(0.8f, 1.4f);
                WilburCandy.Alive++;
            }
        }

        private static void StripColliderSafe(GameObject go) { WilburPlugin.StripCollider(go); }

        // Clods of dirt thrown up from the ground: lumpy, multi-tone, arcing with gravity and bouncing.
        public static void SpawnDirtBurst(Vector3 pos, int count, float speed)
        {
            Material[] mats = WilburBurrowMound.Dirt;
            for (int i = 0; i < count; i++)
            {
                GameObject clod = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(clod);
                clod.transform.position = pos + Vector3.up * 0.2f;
                clod.transform.rotation = UnityEngine.Random.rotation;
                float sz = UnityEngine.Random.Range(0.12f, 0.28f) * Mathf.Max(1f, speed / 10f);
                clod.transform.localScale = new Vector3(sz * UnityEngine.Random.Range(0.8f, 1.3f), sz * UnityEngine.Random.Range(0.6f, 1f), sz);
                Renderer r = clod.GetComponent<Renderer>();
                r.sharedMaterial = mats[UnityEngine.Random.Range(0, mats.Length)];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 dir = new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1f, 2f), UnityEngine.Random.Range(-1f, 1f));
                WilburCandyPiece p = clod.AddComponent<WilburCandyPiece>();
                p.Counted = false;
                p.Velocity = dir.normalized * speed * UnityEngine.Random.Range(0.6f, 1f);
                p.Spin = UnityEngine.Random.onUnitSphere * 500f;
                p.Radius = sz * 0.5f;
                p.Gravity = 25f;
                p.Life = UnityEngine.Random.Range(0.6f, 1.1f);
            }
        }
    }

    // Three spinning reels that settle on the colour of the roulette result.
    public class WilburSlotVisual : MonoBehaviour
    {
        private CharacterBody _body;
        private Color _result;
        private string _text;
        private string _sound;
        private bool _shown;
        private readonly Transform[] _reels = new Transform[3];
        private readonly Material[] _mats = new Material[3];
        private float _t;

        public static void Spawn(CharacterBody body, Color result, string text, string resultSound)
        {
            if (!body) return;
            GameObject go = new GameObject("WilburSlotMachine");
            WilburSlotVisual v = go.AddComponent<WilburSlotVisual>();
            v._body = body;
            v._result = result;
            v._text = text;
            v._sound = resultSound;
            for (int i = 0; i < 3; i++)
            {
                GameObject reel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                WilburPlugin.StripCollider(reel);
                reel.transform.SetParent(go.transform, false);
                reel.transform.localScale = Vector3.one * 0.3f;
                Material m = WilburPlugin.MakeMat();
                reel.GetComponent<Renderer>().sharedMaterial = m;
                v._reels[i] = reel.transform;
                v._mats[i] = m;
            }
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (!_body || _t > 1.9f)
            {
                Destroy(gameObject);
                return;
            }

            Camera cam = Camera.main;
            Vector3 right = cam ? cam.transform.right : Vector3.right;
            transform.position = _body.corePosition + Vector3.up * (2.0f + _body.GetBuffCount(WilburPlugin.DevourBuff) * 0.012f);

            for (int i = 0; i < 3; i++)
            {
                _reels[i].position = transform.position + right * ((i - 1) * 0.38f);
                if (_t < 1.0f)
                {
                    _reels[i].Rotate(540f * Time.deltaTime, 360f * Time.deltaTime, 0f);
                    Color c = Color.HSVToRGB(Mathf.Repeat(_t * 3f + i * 0.33f, 1f), 0.8f, 1f);
                    WilburPlugin.Tint(_mats[i], c, c, 0.4f);
                }
                else
                {
                    _reels[i].rotation = Quaternion.identity;
                    WilburPlugin.Tint(_mats[i], _result, _result, 0.6f);
                }
            }

            if (!_shown && _t >= 1.0f)
            {
                _shown = true;
                WilburAudio.PlayFor(_body, _sound);
                WilburFloatingText.Show(transform.position + Vector3.up * 0.6f, _text, _result, 1.1f);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i]) Destroy(_mats[i]);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // M1: Oink Oink. Part 1 is a shockwave cone; part 2 is a green gas cloud that travels and lingers.
    // ------------------------------------------------------------------------------------------------
    public class WilburBurpState : BaseSkillState
    {
        private float _duration;
        private float _fireTime;
        private bool _fired;
        private WilburPigController _pig;
        private bool _rainbow;

        public override void OnEnter()
        {
            base.OnEnter();

            // Safety: the Super state handles left click itself.
            if (characterBody.HasBuff(WilburPlugin.SuperBuff))
            {
                _rainbow = true;
                if (isAuthority) outer.SetNextStateToMain();
                return;
            }

            // Super Piggy burps twice as fast.
            float fast = characterBody.HasBuff(WilburPlugin.SuperBuff) ? 2f : 1f;
            _duration = 0.9f / attackSpeedStat / fast;
            _fireTime = 0.3f / attackSpeedStat / fast;
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Burp, 0.2f);
            StartAimMode(GetAimRay(), 2f, false);
            _pig = WilburPigController.Find(characterBody);
            if (_pig) _pig.Play(WilburAnim.Burp, attackSpeedStat * fast);
            WilburAudio.PlayFor(characterBody, "oinkoink");
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (_rainbow) return;
            if (!_fired && fixedAge >= _fireTime)
            {
                _fired = true;
                Burp();
            }
            if (fixedAge >= _duration && isAuthority)
                outer.SetNextStateToMain();
        }

        // Where the gas cloud ends up: the first thing the aim ray hits (an enemy or the world), or max range.
        private Vector3 FindGasEnd(Ray aim)
        {
            float max = WilburPlugin.GasRange.Value;
            Vector3 end = aim.origin + aim.direction * max;

            RaycastHit[] hits = Physics.RaycastAll(aim.origin, aim.direction, max, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit h in hits)
            {
                CharacterBody hitBody = h.collider.GetComponentInParent<CharacterBody>();
                if (hitBody == characterBody) continue;
                HurtBox hb = h.collider.GetComponent<HurtBox>();
                if (hb && hb.healthComponent && hb.healthComponent.body == characterBody) continue;
                end = h.point;
                break;
            }

            if ((end - aim.origin).magnitude < 3f) end = aim.origin + aim.direction * 3f;
            return end;
        }

        private void Burp()
        {
            Ray aim = GetAimRay();
            WilburAudio.PlayFor(characterBody, "gas");

            bool wide = characterBody.GetBuffCount(WilburPlugin.DevourBuff) >= WilburPlugin.StunStacks.Value;
            float half = wide ? WilburPlugin.BurpWideHalfAngle.Value : WilburPlugin.BurpHalfAngle.Value;
            bool crit = RollCrit();
            float power = characterBody.HasBuff(WilburPlugin.SuperBuff) ? 1.5f : 1f;

            Vector3 origin = _pig ? _pig.MouthPosition : aim.origin;

            // Part 1: the shockwave.
            WilburFx.SpawnBurp(origin, aim.direction, half);

            // Part 2: a green gas cloud that travels to where you are aiming and lingers.
            float radius = WilburPlugin.GasRadius.Value * (wide ? 1.3f : 1f);
            WilburGas.Spawn(origin, FindGasEnd(aim), gameObject, teamComponent.teamIndex,
                damageStat * WilburPlugin.GasDps.Value * 0.5f * power, crit, radius);

            if (!NetworkServer.active) return;
            WilburStatsController stats = characterBody.GetComponent<WilburStatsController>();

            SphereSearch search = new SphereSearch
            {
                origin = aim.origin,
                radius = WilburPlugin.BurpRange.Value,
                mask = LayerIndex.entityPrecise.mask
            };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();

            foreach (HurtBox box in search.GetHurtBoxes())
            {
                if (!box || !box.healthComponent || !box.healthComponent.alive) continue;
                CharacterBody victim = box.healthComponent.body;
                if (!victim) continue;

                Vector3 to = victim.corePosition - aim.origin;
                if (Vector3.Angle(aim.direction, to) > half) continue;

                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.BurpDamage.Value * power,
                    crit = crit,
                    position = victim.corePosition,
                    force = aim.direction * WilburPlugin.BurpForce.Value,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = wide ? DamageType.Stun1s : DamageType.Generic
                };
                box.healthComponent.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, box.healthComponent.gameObject);

                // Every enemy the sonic wave hits spins the Piggy Bank.
                if (stats) stats.PiggyBankHit(victim.corePosition);
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }
    }

    // Soft smoke built from a real particle system. It borrows the smoke texture from one of the game's
    // own dust effects, so it reads as a cloud instead of solid balls. Returns null if no material is found.
    public static class WilburParticles
    {
        private static Material _baseMat;
        private static bool _looked;
        private static readonly Dictionary<int, Material> Tinted = new Dictionary<int, Material>();

        private static Material BaseMaterial
        {
            get
            {
                if (_looked) return _baseMat;
                _looked = true;
                string[] paths =
                {
                    "Prefabs/Effects/ImpactEffects/CharacterLandImpact",
                    "Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick",
                    "Prefabs/Effects/OmniEffect/OmniExplosionVFX"
                };
                foreach (string path in paths)
                {
                    GameObject fx = null;
                    try { fx = LegacyResourcesAPI.Load<GameObject>(path); } catch { fx = null; }
                    if (!fx) continue;
                    foreach (ParticleSystemRenderer r in fx.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    {
                        if (r && r.sharedMaterial && r.renderMode == ParticleSystemRenderMode.Billboard)
                        {
                            _baseMat = r.sharedMaterial;
                            break;
                        }
                    }
                    if (_baseMat) break;
                }
                if (WilburPlugin.Log != null)
                    WilburPlugin.Log.LogInfo(_baseMat ? "Wilbur smoke uses particle material: " + _baseMat.name : "Wilbur smoke: no particle material found, using simple puffs.");
                return _baseMat;
            }
        }

        private static Material For(Color c)
        {
            Material b = BaseMaterial;
            if (!b) return null;
            int key = Mathf.RoundToInt(c.r * 255f) << 16 | Mathf.RoundToInt(c.g * 255f) << 8 | Mathf.RoundToInt(c.b * 255f);
            Material m;
            if (Tinted.TryGetValue(key, out m) && m) return m;
            m = new Material(b);
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            Tinted[key] = m;
            return m;
        }

        public static ParticleSystem MakeSmoke(Transform parent, Color color, float size, float rate, float life, float radius)
        {
            Material mat = For(color);
            if (!mat) return null;
            try
            {
                GameObject go = new GameObject("WilburSmoke");
                go.SetActive(false);
                go.transform.SetParent(parent, false);

                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = ps.main;
                main.duration = 1f;
                main.loop = true;
                main.playOnAwake = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.15f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.25f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.03f;
                main.maxParticles = 300;

                ParticleSystem.EmissionModule em = ps.emission;
                em.rateOverTime = rate;

                ParticleSystem.ShapeModule shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;

                ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1.4f)));

                ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
                col.enabled = true;
                Gradient g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);

                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

                ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sharedMaterial = mat;

                go.SetActive(true);
                ps.Play();
                return ps;
            }
            catch (Exception e)
            {
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogWarning("Wilbur smoke failed: " + e.Message);
                return null;
            }
        }

        // Stop emitting, let the existing smoke fade, then clean up.
        public static void Release(ParticleSystem ps, float after)
        {
            if (!ps) return;
            try
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ps.transform.SetParent(null, true);
                Object.Destroy(ps.gameObject, after);
            }
            catch (Exception) { }
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Oink Oink's gas. A cloud bursts out of Wilbur's mouth, then splits into puffs that home in on every
    // enemy nearby. Each puff latches onto its enemy and lingers, poisoning it and anything next to it.
    // With nobody around, the cloud just drifts to where you aimed and hangs there.
    // Every machine draws the gas; only the server deals damage.
    // ------------------------------------------------------------------------------------------------
    public class WilburGas : MonoBehaviour
    {
        private enum Mode { Burst, Seek, Linger }

        private static readonly Dictionary<HealthComponent, float> NextTick = new Dictionary<HealthComponent, float>();
        private static Material _fallbackMat;
        private static readonly Color GasColor = new Color(0.45f, 0.9f, 0.25f);

        private Mode _mode;
        private Vector3 _from;
        private Vector3 _to;
        private Vector3 _vel;
        private HealthComponent _target;
        private GameObject _owner;
        private CharacterBody _ownerBody;
        private TeamIndex _team;
        private float _tickDamage;
        private float _radius;
        private bool _crit;
        private float _t;
        private float _phaseT;
        private float _lingerLeft;
        private float _tick;
        private float _burstTime;
        private ParticleSystem _smoke;
        private Transform[] _puffs;
        private float[] _phases;
        private readonly HashSet<HealthComponent> _rolled = new HashSet<HealthComponent>();

        public static void Spawn(Vector3 mouth, Vector3 aimEnd, GameObject owner, TeamIndex team, float tickDamage, bool crit, float radius)
        {
            WilburGas g = Make(mouth, owner, team, tickDamage, crit, radius, Mode.Burst);
            Vector3 dir = aimEnd - mouth;
            float len = Mathf.Min(dir.magnitude, 5f);
            g._from = mouth;
            g._to = mouth + (dir.sqrMagnitude > 0.01f ? dir.normalized : owner.transform.forward) * len;
            g._burstTime = 0.25f;
            g._lingerLeft = WilburPlugin.GasSeconds.Value;
            g._aimEnd = aimEnd;
        }

        private Vector3 _aimEnd;

        private static WilburGas Make(Vector3 pos, GameObject owner, TeamIndex team, float tickDamage, bool crit, float radius, Mode mode)
        {
            GameObject go = new GameObject("WilburGas");
            go.transform.position = pos;
            WilburGas g = go.AddComponent<WilburGas>();
            g._owner = owner;
            g._ownerBody = owner ? owner.GetComponent<CharacterBody>() : null;
            g._team = team;
            g._tickDamage = tickDamage;
            g._crit = crit;
            g._radius = radius;
            g._mode = mode;

            float size = mode == Mode.Seek ? radius * 0.35f : radius * 0.55f;
            float rate = mode == Mode.Seek ? 40f : 30f;
            g._smoke = WilburParticles.MakeSmoke(go.transform, GasColor, size, rate, 1.2f, mode == Mode.Seek ? 0.2f : radius * 0.35f);
            if (!g._smoke) g.BuildFallbackPuffs(mode == Mode.Seek ? 4 : 7);
            return g;
        }

        private void BuildFallbackPuffs(int n)
        {
            if (_fallbackMat == null)
            {
                _fallbackMat = WilburPlugin.MakeMat();
                WilburPlugin.Tint(_fallbackMat, GasColor, new Color(0.2f, 0.6f, 0.08f), 0.4f);
            }
            _puffs = new Transform[n];
            _phases = new float[n];
            for (int i = 0; i < n; i++)
            {
                GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                WilburPlugin.StripCollider(puff);
                puff.transform.SetParent(transform, false);
                puff.GetComponent<Renderer>().sharedMaterial = _fallbackMat;
                _puffs[i] = puff.transform;
                _phases[i] = UnityEngine.Random.value * Mathf.PI * 2f;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _phaseT += dt;

            switch (_mode)
            {
                case Mode.Burst: UpdateBurst(); break;
                case Mode.Seek: UpdateSeek(dt); break;
                case Mode.Linger: UpdateLinger(dt); break;
            }

            if (_puffs != null)
            {
                float r = _mode == Mode.Seek ? _radius * 0.3f : _radius * (_mode == Mode.Burst ? 0.5f : 0.8f);
                float fade = _mode == Mode.Linger ? Mathf.Clamp01(_lingerLeft / 0.8f) : 1f;
                for (int i = 0; i < _puffs.Length; i++)
                {
                    if (!_puffs[i]) continue;
                    float p = _phases[i];
                    Vector3 off = new Vector3(Mathf.Sin(_t * 1.7f + p), Mathf.Sin(_t * 1.3f + p * 2f) * 0.5f, Mathf.Cos(_t * 1.5f + p)) * r * 0.6f;
                    _puffs[i].localPosition = off;
                    _puffs[i].localScale = Vector3.one * r * 0.7f * fade * (0.8f + 0.2f * Mathf.Sin(_t * 2f + p));
                }
            }
        }

        private void UpdateBurst()
        {
            float k = Mathf.Clamp01(_phaseT / _burstTime);
            transform.position = Vector3.Lerp(_from, _to, 1f - (1f - k) * (1f - k));
            if (k < 1f) return;

            // Split into homing puffs, one per nearby enemy.
            List<HealthComponent> targets = FindTargets(transform.position, WilburPlugin.GasSeekRange.Value, WilburPlugin.GasMaxPuffs.Value);
            if (targets.Count == 0)
            {
                // Nobody around: drift on to where you aimed and hang there.
                _mode = Mode.Linger;
                _phaseT = 0f;
                _from = transform.position;
                _to = _aimEnd;
                return;
            }

            Vector3 fwd = (_to - _from).sqrMagnitude > 0.01f ? (_to - _from).normalized : Vector3.forward;
            foreach (HealthComponent hc in targets)
            {
                WilburGas puff = Make(transform.position, _owner, _team, _tickDamage, _crit, _radius * 0.7f, Mode.Seek);
                puff._target = hc;
                puff._lingerLeft = WilburPlugin.GasSeconds.Value;
                Vector3 spread = UnityEngine.Random.onUnitSphere;
                spread.y = Mathf.Abs(spread.y) * 0.6f;
                puff._vel = (fwd * 0.5f + spread).normalized * 10f;
            }
            Finish(1.4f);
        }

        private List<HealthComponent> FindTargets(Vector3 center, float range, int max)
        {
            List<HealthComponent> list = new List<HealthComponent>();
            SphereSearch search = new SphereSearch { origin = center, radius = range, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_team));
            search.OrderCandidatesByDistance();
            search.FilterCandidatesByDistinctHurtBoxEntities();
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || list.Contains(hc)) continue;
                list.Add(hc);
                if (list.Count >= max) break;
            }
            return list;
        }

        private static Vector3 Where(HealthComponent hc)
        {
            if (!hc) return Vector3.zero;
            return hc.body ? hc.body.corePosition : hc.transform.position;
        }

        private void UpdateSeek(float dt)
        {
            if (!_target || !_target.alive)
            {
                List<HealthComponent> next = FindTargets(transform.position, 15f, 1);
                if (next.Count > 0) _target = next[0];
                else
                {
                    BeginLinger(null);
                    return;
                }
            }

            Vector3 goal = Where(_target);
            Vector3 to = goal - transform.position;
            if (to.magnitude < 1.3f || _phaseT > 4f)
            {
                BeginLinger(_target);
                return;
            }

            float speed = Mathf.Lerp(10f, WilburPlugin.GasSpeed.Value * 1.6f, Mathf.Clamp01(_phaseT / 0.6f));
            Vector3 desired = to.normalized * speed;
            _vel = Vector3.MoveTowards(_vel, desired, 70f * dt);
            transform.position += _vel * dt;
        }

        private void BeginLinger(HealthComponent on)
        {
            _mode = Mode.Linger;
            _phaseT = 0f;
            _target = on;
            _from = transform.position;
            _to = transform.position;
            _lingerLeft = WilburPlugin.GasSeconds.Value;
            if (_smoke)
            {
                ParticleSystem.EmissionModule em = _smoke.emission;
                em.rateOverTime = 26f;
                ParticleSystem.ShapeModule sh = _smoke.shape;
                sh.radius = _radius * 0.45f;
                ParticleSystem.MainModule main = _smoke.main;
                main.startSize = new ParticleSystem.MinMaxCurve(_radius * 0.5f, _radius * 0.9f);
            }
        }

        private void UpdateLinger(float dt)
        {
            if (_target && _target.alive)
            {
                transform.position = Where(_target);
            }
            else if (_from != _to)
            {
                float k = Mathf.Clamp01(_phaseT / 0.5f);
                transform.position = Vector3.Lerp(_from, _to, 1f - (1f - k) * (1f - k));
            }

            _lingerLeft -= dt;
            if (_lingerLeft <= 0f) Finish(1.5f);
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || _mode != Mode.Linger) return;
            _tick -= Time.fixedDeltaTime;
            if (_tick > 0f) return;
            _tick = 0.5f;

            SphereSearch search = new SphereSearch { origin = transform.position, radius = _radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_team));
            search.FilterCandidatesByDistinctHurtBoxEntities();

            WilburStatsController ctrl = _ownerBody ? _ownerBody.GetComponent<WilburStatsController>() : null;
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive) continue;

                // Overlapping clouds don't stack: one gas tick per enemy at a time.
                float next;
                if (NextTick.TryGetValue(hc, out next) && Time.time < next) continue;
                NextTick[hc] = Time.time + 0.45f;

                DamageInfo dmg = new DamageInfo
                {
                    attacker = _owner,
                    inflictor = gameObject,
                    damage = _tickDamage,
                    crit = _crit,
                    position = Where(hc),
                    force = Vector3.zero,
                    procCoefficient = 0.2f,
                    damageColorIndex = DamageColorIndex.Poison,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);

                // The burp spins the Piggy Bank once for every enemy it gasses.
                if (ctrl && _rolled.Add(hc)) ctrl.PiggyBankHit(Where(hc));
            }

            if (NextTick.Count > 128)
            {
                List<HealthComponent> dead = new List<HealthComponent>();
                foreach (KeyValuePair<HealthComponent, float> kv in NextTick)
                    if (!kv.Key) dead.Add(kv.Key);
                foreach (HealthComponent h in dead) NextTick.Remove(h);
            }
        }

        private void Finish(float smokeFade)
        {
            WilburParticles.Release(_smoke, smokeFade);
            _smoke = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_smoke) WilburParticles.Release(_smoke, 1.5f);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // M2: Slopp Devour, a chain attack. Wilbur pounces on an enemy, bites, then springs straight to the
    // next nearby enemy, again and again. Small or weakened enemies are gobbled (each gobble kill rolls
    // the Piggy Bank with a multicast chance). Bigger Wilbur chains further and more times.
    // ------------------------------------------------------------------------------------------------
    public class WilburDevourState : BaseSkillState
    {
        private enum Phase { Pounce, Bite, Hop }

        private const float PounceMax = 1.0f;
        private Vector3 _areaCenter;
        private Vector3 _moveVel;

        // The chain is contained: it never leaves this circle around where the Devour started.
        private float AreaRadius { get { return Mathf.Min(WilburPlugin.ChainMaxDistance.Value * Mathf.Sqrt(SizeMul), WilburPlugin.MaxReach.Value); } }

        private bool InArea(Vector3 p)
        {
            Vector3 d = p - _areaCenter;
            d.y *= 0.5f;
            return d.sqrMagnitude <= AreaRadius * AreaRadius;
        }
        private float BiteTime { get { return Mathf.Clamp(WilburPlugin.BiteSeconds.Value, 0.08f, 2f); } }
        private float _suckSide = 1f;
        private const float HopTime = 0.35f;

        private HurtBox _target;
        private HurtBoxReference _targetRef;
        private Phase _phase = Phase.Pounce;
        private float _phaseT;
        private bool _active;
        private bool _crit;
        private bool _bitThis;
        private int _bites;
        private Vector3 _lastTargetPos;
        private readonly HashSet<HealthComponent> _bitten = new HashSet<HealthComponent>();
        private WilburPigController _pig;
        private float _lastWhoosh;

        private float SizeMul { get { return WilburGobble.SizeMul(characterBody); } }

        private void PounceFx()
        {
            if (!_pig) _pig = WilburPigController.Find(characterBody);
            if (_pig) _pig.Play(WilburAnim.Pounce);
            if (Time.time - _lastWhoosh > 0.25f)
            {
                _lastWhoosh = Time.time;
                WilburAudio.PlayFor(characterBody, "whoosh");
            }
        }
        private float DamageMul { get { return 1f + WilburPlugin.DevourDamagePerStack.Value * WilburGobble.Stacks(characterBody); } }

        private int MaxBites
        {
            get
            {
                int limit = WilburPlugin.DevourChains.Value;
                if (limit <= 0) limit = int.MaxValue;
                int hard = WilburPlugin.ChainMaxBites.Value;
                if (hard > 0) limit = Mathf.Min(limit, hard);
                return limit;
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Safety: the Super state handles right click itself.
            if (characterBody.HasBuff(WilburPlugin.SuperBuff))
            {
                if (isAuthority) outer.SetNextStateToMain();
                return;
            }

            if (isAuthority)
            {
                _target = FindFirstTarget();
                if (_target) _targetRef = HurtBoxReference.FromHurtBox(_target);
            }
            else
            {
                _target = _targetRef.ResolveHurtBox();
            }

            if (!_target)
            {
                if (isAuthority) outer.SetNextStateToMain();
                return;
            }

            _active = true;
            _areaCenter = transform.position;
            _moveVel = characterMotor ? characterMotor.velocity : Vector3.zero;
            _crit = RollCrit();
            _lastTargetPos = TargetPoint();
            StartAimMode(GetAimRay(), 2f, false);
            WilburAudio.PlayFor(characterBody, "oink");
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Devour, 0.6f);
            PounceFx();

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.Motor.ForceUnground();
            }
            if (NetworkServer.active) characterBody.AddBuff(WilburPlugin.FeedingBuff);
        }

        private HurtBox FindFirstTarget()
        {
            Ray aim = GetAimRay();
            BullseyeSearch search = new BullseyeSearch
            {
                teamMaskFilter = TeamMask.GetEnemyTeams(teamComponent.teamIndex),
                filterByLoS = true,
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                sortMode = BullseyeSearch.SortMode.Angle,
                maxDistanceFilter = WilburPlugin.DevourRange.Value * Mathf.Sqrt(SizeMul),
                maxAngleFilter = 30f
            };
            search.RefreshCandidates();
            search.FilterOutGameObject(gameObject);
            return search.GetResults().FirstOrDefault();
        }

        // The next link in the chain: the closest living enemy near the last one that hasn't been bitten yet.
        private HurtBox FindNextTarget(Vector3 from)
        {
            SphereSearch search = new SphereSearch
            {
                origin = from,
                radius = WilburPlugin.ChainRange.Value * Mathf.Sqrt(SizeMul),
                mask = LayerIndex.entityPrecise.mask
            };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.OrderCandidatesByDistance();
            search.FilterCandidatesByDistinctHurtBoxEntities();

            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || _bitten.Contains(hc)) continue;
                if (!InArea(hc.body ? hc.body.corePosition : box.transform.position)) continue;
                if (hc.body && hc.body.mainHurtBox) return hc.body.mainHurtBox;
                return box;
            }

            // Nobody next to the last bite: the closest un-bitten enemy still inside the area.
            SphereSearch area = new SphereSearch { origin = _areaCenter, radius = AreaRadius, mask = LayerIndex.entityPrecise.mask };
            area.RefreshCandidates();
            area.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            area.FilterCandidatesByDistinctHurtBoxEntities();
            HurtBox best = null;
            float bestD = float.MaxValue;
            foreach (HurtBox box in area.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || _bitten.Contains(hc) || !hc.body) continue;
                float d = (hc.body.corePosition - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = hc.body.mainHurtBox ? hc.body.mainHurtBox : box; }
            }
            if (best) return best;

            // Everyone in the area has been bitten: keep biting the current one if it's still alive (bosses).
            if (_target && _target.healthComponent && _target.healthComponent.alive) return _target;
            return null;
        }

        // Where to latch on: the top of the enemy (its head).
        private static Vector3 HeadPoint(CharacterBody vb)
        {
            HurtBox hb = vb.mainHurtBox;
            if (hb && hb.collider)
            {
                Bounds b = hb.collider.bounds;
                return new Vector3(b.center.x, b.max.y - b.extents.y * 0.3f, b.center.z);
            }
            return vb.corePosition + Vector3.up * vb.radius * 0.5f;
        }

        // Hold Wilbur so his mouth sits on the enemy's head while he sucks.
        private void Latch()
        {
            if (!isAuthority || !characterMotor) return;
            CharacterBody vb = _target && _target.healthComponent ? _target.healthComponent.body : null;
            if (!_pig) _pig = WilburPigController.Find(characterBody);
            if (!vb || !_pig || SizeMul > 6f)
            {
                Move(Vector3.zero);
                return;
            }
            Vector3 desired = HeadPoint(vb) - (_pig.MouthPosition - transform.position);
            Vector3 delta = desired - transform.position;
            characterMotor.Motor.ForceUnground();
            _moveVel = Vector3.Lerp(_moveVel, Vector3.ClampMagnitude(delta / Time.fixedDeltaTime * 0.35f, 50f), 1f - Mathf.Exp(-20f * Time.fixedDeltaTime));
            characterMotor.velocity = _moveVel;
        }

        private Vector3 TargetPoint()
        {
            if (_target && _target.healthComponent && _target.healthComponent.body)
                return _target.healthComponent.body.corePosition;
            if (_target) return _target.transform.position;
            return _lastTargetPos;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(_targetRef);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            _targetRef = reader.ReadHurtBoxReference();
        }

        private void Move(Vector3 velocity)
        {
            if (!isAuthority || !characterMotor) return;
            characterMotor.Motor.ForceUnground();
            characterMotor.velocity = velocity;
        }

        private void Face(Vector3 dir)
        {
            dir.y = 0f;
            if (characterDirection && dir.sqrMagnitude > 0.01f) characterDirection.forward = dir.normalized;
        }

        private void GoTo(Phase next)
        {
            _phase = next;
            _phaseT = 0f;
            if (next != Phase.Hop) return;
            if (!_pig) _pig = WilburPigController.Find(characterBody);
            if (_pig) _pig.Play(WilburAnim.Happy);

            if (characterMotor)
            {
                characterMotor.useGravity = true;
                if (isAuthority)
                {
                    Vector3 away = transform.position - _lastTargetPos;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f) away = -transform.forward;
                    characterMotor.Motor.ForceUnground();
                    characterMotor.velocity = away.normalized * 8f + Vector3.up * 11f;
                }
            }
        }

        // Keep chaining only while the key is held, there are bites left and time remains.
        private bool WantsMore()
        {
            if (_bites >= MaxBites) return false;
            float maxT = WilburPlugin.ChainMaxSeconds.Value;
            if (maxT > 0f && fixedAge >= maxT) return false;
            if (isAuthority && !IsKeyDownAuthority()) return false;
            return true;
        }

        private void NextLinkOrHop()
        {
            if (WantsMore())
            {
                HurtBox next = FindNextTarget(_lastTargetPos);
                if (next)
                {
                    _target = next;
                    _bitThis = false;
                    GoTo(Phase.Pounce);
                    PounceFx();
                    return;
                }
            }
            GoTo(Phase.Hop);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!_active) return;

            float dt = Time.fixedDeltaTime;
            _phaseT += dt;
            _lastTargetPos = TargetPoint();
            if (!_pig) _pig = WilburPigController.Find(characterBody);
            if (_pig)
            {
                _pig.Sucking = _phase == Phase.Bite && SizeMul <= 6f;
                _pig.SuckSide = _suckSide;
            }

            bool alive = _target && _target.healthComponent && _target.healthComponent.alive;
            Vector3 toTarget = _lastTargetPos - transform.position;

            // Jump bails out of the chain at any time; the time cap stops runaway chains.
            if (_phase != Phase.Hop && isAuthority && inputBank && inputBank.jump.justPressed)
            {
                GoTo(Phase.Hop);
                return;
            }
            if (_phase == Phase.Pounce && WilburPlugin.ChainMaxSeconds.Value > 0f && fixedAge >= WilburPlugin.ChainMaxSeconds.Value + 0.5f)
            {
                GoTo(Phase.Hop);
                return;
            }

            switch (_phase)
            {
                case Phase.Pounce:
                    Face(toTarget);
                    if (!alive)
                        NextLinkOrHop();
                    else if (_phaseT > PounceMax && toTarget.magnitude > Mathf.Min(4f * SizeMul, 60f))
                        NextLinkOrHop();
                    else if (toTarget.magnitude < Mathf.Min(2.2f * SizeMul, 40f) || _phaseT > PounceMax)
                        GoTo(Phase.Bite);
                    else
                    {
                        // Smooth springy leap: eased speed, a small hop arc while far, no teleport-fast dashes.
                        float dist = toTarget.magnitude;
                        Vector3 want = toTarget.normalized * Mathf.Clamp(dist / 0.3f, 25f, 70f);
                        if (dist > 4f) want += Vector3.up * Mathf.Min(dist, 12f) * 0.6f;
                        _moveVel = Vector3.Lerp(_moveVel, want, 1f - Mathf.Exp(-14f * dt));
                        Move(_moveVel);
                    }
                    break;

                case Phase.Bite:
                    Face(toTarget);
                    Latch();
                    if (!_bitThis)
                    {
                        _bitThis = true;
                        _bites++;
                        _suckSide = -_suckSide;
                        WilburAudio.PlayFor(characterBody, "slurp");
                        CharacterBody victimBody = _target && _target.healthComponent ? _target.healthComponent.body : null;
                        if (victimBody) WilburVictimFx.Squish(victimBody, BiteTime + 0.1f);
                        if (_target && _target.healthComponent) _bitten.Add(_target.healthComponent);
                        WilburAudio.PlayFor(characterBody, "chomp");
                        if (!_pig) _pig = WilburPigController.Find(characterBody);
                        if (_pig) _pig.Play(WilburAnim.Chomp);
                        if (_bites == 4 || (_bites > 4 && _bites % 8 == 0))
                            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Chain, 1f);
                        if (NetworkServer.active) Bite();
                    }
                    if (_phaseT >= BiteTime) NextLinkOrHop();
                    break;

                case Phase.Hop:
                    if (_phaseT >= HopTime && isAuthority)
                        outer.SetNextStateToMain();
                    break;
            }
        }

        private void Bite()
        {
            HealthComponent hc = _target ? _target.healthComponent : null;
            if (!hc || !hc.alive) return;

            Vector3 at = hc.body ? hc.body.corePosition : _lastTargetPos;
            bool gobble = WilburGobble.CanEat(characterBody, hc);
            if (gobble)
            {
                WilburGobble.Gobble(characterBody, hc, _crit);
            }
            else
            {
                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.DevourBite.Value * DamageMul,
                    crit = _crit,
                    position = at,
                    force = Vector3.zero,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
            }

            // Every bite spins the Piggy Bank.
            WilburStatsController stats = characterBody.GetComponent<WilburStatsController>();
            if (stats) stats.PiggyBankHit(at);

            string word = gobble ? "GOBBLE" : "CHOMP";
            WilburFloatingText.Show(at + Vector3.up, _bites > 1 ? word + " x" + _bites + "!" : word + "!",
                gobble ? new Color(1f, 0.4f, 0.55f) : new Color(1f, 0.85f, 0.85f), 0.9f + 0.08f * _bites);

            WilburSuperServer sup = characterBody.GetComponent<WilburSuperServer>();
            if (sup && sup.Active) sup.Extend(WilburPlugin.SuperExtend.Value);
        }

        public override void OnExit()
        {
            if (_pig) _pig.Sucking = false;
            if (_active)
            {
                if (characterMotor) characterMotor.useGravity = true;
                if (NetworkServer.active && characterBody.HasBuff(WilburPlugin.FeedingBuff))
                    characterBody.RemoveBuff(WilburPlugin.FeedingBuff);
            }
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Shift: Truffle Burrow. Dig underground (invulnerable, steerable), then burst out under enemies,
    // launching them and gobbling the small ones. Hold the key to stay under, let go to erupt.
    // ------------------------------------------------------------------------------------------------
    public class WilburBurrowState : BaseSkillState
    {
        private static Material _dirtMat;

        private Vector3 _vel;
        private WilburBurrowMound _mound;
        private float _clod;
        private bool _invuln;

        public static Material DirtMat
        {
            get
            {
                if (_dirtMat == null)
                {
                    _dirtMat = WilburPlugin.MakeMat();
                    WilburPlugin.Tint(_dirtMat, new Color(0.36f, 0.24f, 0.13f), new Color(0.05f, 0.03f, 0.01f), 0f);
                }
                return _dirtMat;
            }
        }

        private float _quakeSound;
        private float _quakeShake;
        private float _quakePulse = 0.15f;

        // Each quake pulse knocks every grounded enemy in the area into the air (bosses only a little).
        private void QuakeBounce()
        {
            SphereSearch search = new SphereSearch { origin = characterBody.footPosition, radius = QuakeRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                CharacterBody v = box && box.healthComponent ? box.healthComponent.body : null;
                if (!v || !v.characterMotor || v.characterMotor.isFlying || !v.characterMotor.isGrounded) continue;
                float hop = v.isChampion ? 4f : 10f;
                v.characterMotor.Motor.ForceUnground();
                v.characterMotor.velocity = new Vector3(v.characterMotor.velocity.x * 0.3f, hop, v.characterMotor.velocity.z * 0.3f);
            }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            WilburAudio.PlayFor(characterBody, "dig");
            WilburAudio.PlayFor(characterBody, "whoosh");
            WilburFx.SpawnDirtBurst(characterBody.footPosition, 10, 7f);
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Burrow, 0.6f);
            _quakeSound = 0.25f;

            if (NetworkServer.active)
            {
                characterBody.AddBuff(WilburPlugin.BurrowBuff);
                characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility);
                _invuln = true;
            }

            _mound = WilburBurrowMound.Spawn(characterBody);

            if (characterMotor) _vel = new Vector3(characterMotor.velocity.x, 0f, characterMotor.velocity.z);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            float dt = Time.fixedDeltaTime;

            Vector3 move = inputBank ? inputBank.moveVector : Vector3.zero;
            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            Vector3 target = move * (moveSpeedStat * WilburPlugin.BurrowSpeed.Value);
            _vel = Vector3.Lerp(_vel, target, 1f - Mathf.Exp(-10f * dt));

            if (characterMotor && isAuthority)
                characterMotor.velocity = new Vector3(_vel.x, characterMotor.velocity.y, _vel.z);
            if (characterDirection && _vel.sqrMagnitude > 0.5f)
                characterDirection.forward = new Vector3(_vel.x, 0f, _vel.z).normalized;

            _clod -= dt;
            if (_clod <= 0f)
            {
                _clod = 0.15f;
                WilburFx.SpawnDirtBurst(characterBody.footPosition, 1, 5f);
            }

            Quake(dt);

            if (isAuthority)
            {
                bool held = IsKeyDownAuthority();
                if ((fixedAge >= WilburPlugin.BurrowMinSeconds.Value && !held) || fixedAge >= WilburPlugin.BurrowMaxSeconds.Value)
                    outer.SetNextStateToMain();
            }
        }

        // Earthquake while burrowed: damages everything around Wilbur, slams flying enemies to the ground
        // and roots them, and slows the ones on the ground. A pig in mud!
        private float _quakeTick;
        private float _mudTick;
        private int _quakeHits;

        private float QuakeRadius
        {
            get { return Mathf.Min(WilburPlugin.QuakeRadius.Value * Mathf.Sqrt(WilburGobble.SizeMul(characterBody)), WilburPlugin.MaxReach.Value); }
        }

        private void Quake(float dt)
        {
            // Rumble: sound loop + a gentle constant shake.
            _quakeSound -= dt;
            if (_quakeSound <= 0f)
            {
                _quakeSound = 1.45f;
                WilburAudio.PlayFor(characterBody, "quake");
            }
            _quakeShake -= dt;
            if (_quakeShake <= 0f)
            {
                _quakeShake = 0.35f;
                WilburShake.At(characterBody.footPosition, 1.4f, 0.5f, QuakeRadius * 2f);
            }

            // Big pulses: a shockwave ring to the quake's edge, rocks, cracks, and every enemy gets bounced.
            _quakePulse -= dt;
            if (_quakePulse <= 0f)
            {
                _quakePulse = 0.7f;
                WilburQuakeFx.Pulse(characterBody.footPosition, QuakeRadius, WilburGobble.SizeMul(characterBody));
                WilburShake.At(characterBody.footPosition, 2.6f, 0.4f, QuakeRadius * 2.5f);
                if (NetworkServer.active) QuakeBounce();
            }

            _mudTick -= dt;
            if (_mudTick <= 0f)
            {
                _mudTick = 0.3f;
                Vector2 off = UnityEngine.Random.insideUnitCircle * QuakeRadius * 0.7f;
                WilburFx.SpawnDirtBurst(characterBody.footPosition + new Vector3(off.x, 0f, off.y), 3, 8f);
            }

            if (!NetworkServer.active) return;
            _quakeTick -= dt;
            if (_quakeTick > 0f) return;
            _quakeTick = 0.25f;

            SphereSearch search = new SphereSearch { origin = characterBody.footPosition, radius = QuakeRadius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();

            bool crit = RollCrit();
            WilburStatsController stats = characterBody.GetComponent<WilburStatsController>();
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || !hc.body) continue;
                CharacterBody v = hc.body;

                bool flyer = !v.characterMotor || v.characterMotor.isFlying;
                if (flyer)
                {
                    // Slam flyers out of the sky and pin them.
                    if (v.rigidbody) v.rigidbody.velocity = Vector3.down * WilburPlugin.QuakeSlam.Value;
                    if (v.characterMotor) v.characterMotor.velocity = Vector3.down * WilburPlugin.QuakeSlam.Value;
                    v.AddTimedBuff(RoR2Content.Buffs.Entangle, 0.6f);
                }
                else
                {
                    v.AddTimedBuff(RoR2Content.Buffs.Slow50, 0.6f);
                }

                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.QuakeDps.Value * 0.25f,
                    crit = crit,
                    position = v.corePosition,
                    force = Vector3.down * 400f,
                    procCoefficient = 0.3f,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                if (stats) stats.RegisterHit(v.corePosition);
                _quakeHits++;
            }

            if (_quakeHits > 0 && fixedAge > 0.2f && UnityEngine.Random.value < 0.1f)
                WilburPhrases.Say(characterBody, WilburPhrases.Kind.Mud, 1f);
        }

        public override void Update()
        {
            base.Update();
            // The mound animates itself (WilburBurrowMound).
        }

        private void Erupt()
        {
            Vector3 c = characterBody.footPosition;
            float size = WilburGobble.SizeMul(characterBody);
            float radius = Mathf.Min(WilburPlugin.BurrowRadius.Value * (1f + (size - 1f) * 0.5f), WilburPlugin.MaxReach.Value);

            WilburFx.SpawnDirtBurst(c, 22, 14f * Mathf.Sqrt(size));
            WilburRingWave.Spawn(c, 0.5f * Mathf.Sqrt(size), radius, 0.5f, new Color(0.6f, 0.43f, 0.26f), 0.3f * Mathf.Sqrt(size));
            WilburAudio.PlayFor(characterBody, "erupt");
            WilburAudio.PlayFor(characterBody, "squeal");
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Erupt, 0.7f);
            WilburShake.At(c, 2.5f, 0.5f, radius * 2.5f);

            if (characterMotor && isAuthority)
            {
                characterMotor.Motor.ForceUnground();
                characterMotor.velocity = new Vector3(characterMotor.velocity.x, 12f, characterMotor.velocity.z);
            }

            if (!NetworkServer.active) return;

            SphereSearch search = new SphereSearch { origin = c + Vector3.up, radius = radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();

            bool crit = RollCrit();
            int hit = 0;
            int gobbled = 0;
            foreach (HurtBox box in search.GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive) continue;
                hit++;

                if (WilburGobble.CanEat(characterBody, hc))
                {
                    WilburGobble.Gobble(characterBody, hc, crit);
                    gobbled++;
                    continue;
                }

                Vector3 outward = box.transform.position - c;
                outward.y = 0f;
                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.BurrowDamage.Value,
                    crit = crit,
                    position = box.transform.position,
                    force = Vector3.up * 3200f + outward.normalized * 800f,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                WilburStatsController st = characterBody.GetComponent<WilburStatsController>();
                if (st) st.PiggyBankHit(box.transform.position);
            }

            if (hit > 0 && characterBody.healthComponent)
                characterBody.healthComponent.HealFraction(WilburPlugin.BurrowHealPerHit.Value * hit, default(ProcChainMask));

            if (gobbled > 0)
                WilburFloatingText.Show(c + Vector3.up * 2f, gobbled > 1 ? "GOBBLED x" + gobbled + "!" : "GOBBLED!", new Color(1f, 0.4f, 0.55f), 1.2f);
            else if (hit > 0)
                WilburFloatingText.Show(c + Vector3.up * 2f, "TRUFFLES!  +" + (WilburPlugin.BurrowHealPerHit.Value * hit * 100f).ToString("0") + "% HP", new Color(0.7f, 0.5f, 0.3f), 1f);
        }

        public override void OnExit()
        {
            if (characterBody && characterBody.healthComponent && characterBody.healthComponent.alive) Erupt();
            if (_mound) _mound.Close();
            if (NetworkServer.active)
            {
                if (characterBody.HasBuff(WilburPlugin.BurrowBuff)) characterBody.RemoveBuff(WilburPlugin.BurrowBuff);
                if (_invuln && characterBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility))
                    characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility);
            }
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Super Piggy's rainbow laser. Owned and driven by the Super state itself (no skill hand-off): while
    // Super, holding left click fires a continuous, piercing rainbow out of Wilbur's snout. It deflates
    // Wilbur while it fires.
    // ------------------------------------------------------------------------------------------------
    public class WilburRainbowBeam : MonoBehaviour
    {
        public static readonly Color[] Bands =
        {
            new Color(1f, 0.18f, 0.2f), new Color(1f, 0.55f, 0.12f), new Color(1f, 0.92f, 0.2f), new Color(0.3f, 0.95f, 0.35f),
            new Color(0.2f, 0.6f, 1f), new Color(0.35f, 0.3f, 0.95f), new Color(0.78f, 0.35f, 1f)
        };
        private static Material[] _mats;

        public static Material[] BandMaterials
        {
            get
            {
                if (_mats == null)
                {
                    _mats = new Material[Bands.Length];
                    for (int i = 0; i < Bands.Length; i++)
                    {
                        _mats[i] = WilburPlugin.MakeMat();
                        WilburPlugin.Tint(_mats[i], Bands[i], Bands[i], 0.9f);
                    }
                }
                return _mats;
            }
        }

        private CharacterBody _body;
        private WilburPigController _pig;
        private WilburStatsController _stats;
        private GameObject _root;
        private readonly Transform[] _strips = new Transform[7];
        private bool _firing;
        private float _tick;
        private float _sound;
        private float _sparkle;
        private Vector3 _end;

        public bool Firing { get { return _firing; } }

        public static WilburRainbowBeam Attach(CharacterBody body)
        {
            WilburRainbowBeam b = body.gameObject.GetComponent<WilburRainbowBeam>();
            if (!b) b = body.gameObject.AddComponent<WilburRainbowBeam>();
            b._body = body;
            b._pig = WilburPigController.Find(body);
            b._stats = body.GetComponent<WilburStatsController>();
            b.Build();
            return b;
        }

        private void Build()
        {
            if (_root) return;
            Material[] mats = BandMaterials;
            _root = new GameObject("WilburRainbowBeam");
            for (int i = 0; i < mats.Length; i++)
            {
                GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                strip.name = "Band" + i;
                WilburPlugin.StripCollider(strip);
                strip.transform.SetParent(_root.transform, false);
                strip.GetComponent<Renderer>().sharedMaterial = mats[i];
                _strips[i] = strip.transform;
            }
            _root.SetActive(false);
        }

        public void SetFiring(bool on)
        {
            if (on == _firing) return;
            _firing = on;
            if (_root) _root.SetActive(on);
            if (!_pig && _body) _pig = WilburPigController.Find(_body);
            if (_pig) _pig.Lasering = on;
            if (on)
            {
                _tick = 0f;
                _sound = 0f;
                if (_body)
                {
                    WilburPhrases.Say(_body, WilburPhrases.Kind.Laser, 0.8f);
                    WilburShake.At(_body.corePosition, 1f, 0.3f, 40f);
                }
            }
        }

        private float Size { get { return WilburGobble.SizeMul(_body); } }

        private Ray AimRay()
        {
            if (_body && _body.inputBank) return _body.inputBank.GetAimRay();
            return new Ray(transform.position, transform.forward);
        }

        private void FixedUpdate()
        {
            if (!_firing || !_body) return;
            float dt = Time.fixedDeltaTime;

            _tick -= dt;
            if (_tick <= 0f)
            {
                _tick = 0.1f;
                Fire();
            }

            _sound -= dt;
            if (_sound <= 0f)
            {
                _sound = 0.9f;
                WilburAudio.PlayFor(_body, "rainbow");
            }

            if (NetworkServer.active && _stats)
            {
                int stacks = WilburGobble.Stacks(_body);
                if (stacks > 0)
                    _stats.Deflate(Mathf.Max(WilburPlugin.RainbowDrainMin.Value, stacks * WilburPlugin.RainbowDrainFraction.Value) * dt);
            }
        }

        private void Fire()
        {
            Ray aim = AimRay();
            float range = WilburPlugin.RainbowRange.Value * Mathf.Max(1f, Mathf.Sqrt(Size));
            RaycastHit hit;
            _end = Physics.Raycast(aim, out hit, range, LayerIndex.world.mask, QueryTriggerInteraction.Ignore) ? hit.point : aim.GetPoint(range);

            if (!NetworkServer.active) return;

            float dist = Vector3.Distance(aim.origin, _end);
            float radius = Mathf.Min(WilburPlugin.RainbowWidth.Value * Mathf.Sqrt(Size), 15f);
            RaycastHit[] hits = Physics.SphereCastAll(aim.origin, radius, aim.direction, dist, LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Collide);
            HashSet<HealthComponent> done = new HashSet<HealthComponent>();
            bool crit = _body.RollCrit();
            TeamIndex team = _body.teamComponent ? _body.teamComponent.teamIndex : TeamIndex.Player;
            foreach (RaycastHit h in hits)
            {
                HurtBox hb = h.collider ? h.collider.GetComponent<HurtBox>() : null;
                HealthComponent hc = hb ? hb.healthComponent : null;
                if (!hc || !hc.alive || hc == _body.healthComponent) continue;
                if (hc.body && hc.body.teamComponent && hc.body.teamComponent.teamIndex == team) continue;
                if (!done.Add(hc)) continue;

                DamageInfo dmg = new DamageInfo
                {
                    attacker = _body.gameObject,
                    inflictor = _body.gameObject,
                    damage = _body.damage * WilburPlugin.RainbowDps.Value * 0.1f,
                    crit = crit,
                    position = hc.body ? hc.body.corePosition : h.point,
                    force = aim.direction * 300f,
                    procCoefficient = 0.3f,
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
            }
            if (done.Count > 0 && _stats) _stats.RegisterHit(_end);
        }

        private void LateUpdate()
        {
            if (!_firing || !_root || !_body) return;

            Vector3 from = _pig ? _pig.MouthPosition : AimRay().origin;
            if (_end == Vector3.zero) _end = AimRay().GetPoint(30f);
            Vector3 d = _end - from;
            float len = d.magnitude;
            if (len < 0.1f) return;

            Camera cam = Camera.main;
            Vector3 up = cam ? cam.transform.up : Vector3.up;
            Quaternion rot = Quaternion.LookRotation(d / len, up);
            float w = 0.13f * Mathf.Sqrt(Size);
            float t = Time.time;
            for (int i = 0; i < _strips.Length; i++)
            {
                if (!_strips[i]) continue;
                float wob = Mathf.Sin(t * 14f + i * 0.7f) * w * 0.15f;
                _strips[i].position = from + d * 0.5f + rot * new Vector3(wob, (3 - i) * w, 0f);
                _strips[i].rotation = rot;
                float pulse = 1f + Mathf.Sin(t * 20f + i) * 0.08f;
                _strips[i].localScale = new Vector3(w * 3f * pulse, w * pulse, len);
            }

            _sparkle -= Time.deltaTime;
            if (_sparkle <= 0f)
            {
                _sparkle = 0.15f;
                WilburFx.SpawnPuffRing(_end, Bands[UnityEngine.Random.Range(0, Bands.Length)]);
            }
        }

        private void OnDestroy()
        {
            if (_pig) _pig.Lasering = false;
            if (_root) Destroy(_root);
        }
    }

    // ------------------------------------------------------------------------------------------------
    // Super Piggy's secondary: a rainbow ball. It flies out, stops at the first thing it hits (or at max
    // range), becomes a vortex that sucks every enemy in and shreds them, then detonates and gobbles the
    // weak ones. Costs a chunk of Wilbur's size.
    // ------------------------------------------------------------------------------------------------
    public class WilburRainbowBall : MonoBehaviour
    {
        private enum Mode { Fly, Vortex }

        private Mode _mode = Mode.Fly;
        private CharacterBody _owner;
        private TeamIndex _team;
        private bool _crit;
        private float _damage;
        private Vector3 _dir;
        private float _speed;
        private float _t;
        private float _modeT;
        private float _tick;
        private float _suck;
        private float _radius;      // visual radius of the ball (as big as Wilbur)
        private float _pullRadius;
        private float _blastRadius;
        private float _scale = 1f;
        private Transform _core;
        private Material _coreMat;
        private Transform _halo;
        private Transform _orbitA;
        private Transform _orbitB;
        private readonly List<LineRenderer> _haloLines = new List<LineRenderer>();
        private TrailRenderer _trail;

        // size = Wilbur's size when he pukes it.
        public static void Spawn(Vector3 from, Ray aim, CharacterBody owner, bool crit, float size)
        {
            GameObject go = new GameObject("WilburRainbowBall");
            WilburRainbowBall b = go.AddComponent<WilburRainbowBall>();
            b._owner = owner;
            b._team = owner && owner.teamComponent ? owner.teamComponent.teamIndex : TeamIndex.Player;
            b._crit = crit;
            b._damage = owner ? owner.damage : 12f;
            b._radius = Mathf.Max(0.8f, 0.55f * size);
            b._pullRadius = Mathf.Min(WilburPlugin.BallPullRadius.Value + b._radius * 2.5f, WilburPlugin.MaxReach.Value);
            b._blastRadius = Mathf.Min(WilburPlugin.BallBlastRadius.Value + b._radius * 2f, WilburPlugin.MaxReach.Value);
            b._speed = 38f + b._radius * 1.5f;

            // Aim from the camera ray, start just in front of the mouth so a huge ball doesn't swallow Wilbur.
            Vector3 target = aim.GetPoint(80f + b._radius * 2f);
            RaycastHit hit;
            if (Physics.Raycast(aim, out hit, 300f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore)) target = hit.point;
            b._dir = (target - from).sqrMagnitude > 0.01f ? (target - from).normalized : aim.direction;
            go.transform.position = from + b._dir * b._radius * 0.6f;
            b.Build();
        }

        private static Gradient Rainbow(float alpha)
        {
            Gradient g = new Gradient();
            Color[] c = WilburRainbowBeam.Bands;
            GradientColorKey[] ck = new GradientColorKey[7];
            for (int i = 0; i < 7; i++) ck[i] = new GradientColorKey(c[i], i / 6f);
            g.SetKeys(ck, new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha, 1f) });
            return g;
        }

        private void Build()
        {
            float r = _radius;

            // Core: a smooth glowing pearl whose colour drifts through the rainbow.
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            WilburPlugin.StripCollider(core);
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * r * 2f;
            _coreMat = WilburPlugin.MakeMat();
            WilburPlugin.Tint(_coreMat, new Color(1f, 0.85f, 0.95f), new Color(1f, 0.7f, 0.9f), 0.6f);
            Renderer cr = core.GetComponent<Renderer>();
            cr.sharedMaterial = _coreMat;
            cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _core = core.transform;

            // Halo: seven rainbow bands around the ball, always facing the camera.
            GameObject halo = new GameObject("Halo");
            halo.transform.SetParent(transform, false);
            _halo = halo.transform;
            Color[] bands = WilburRainbowBeam.Bands;
            for (int i = 0; i < bands.Length; i++)
            {
                LineRenderer lr = WilburLines.Circle(_halo, "Band" + i, 48, r * (1.12f + i * 0.085f), r * 0.085f, bands[bands.Length - 1 - i], true);
                if (lr) _haloLines.Add(lr);
            }

            // Two tilted rainbow orbits spinning around it.
            GameObject a = new GameObject("OrbitA");
            a.transform.SetParent(transform, false);
            a.transform.localRotation = Quaternion.Euler(70f, 0f, 20f);
            _orbitA = a.transform;
            LineRenderer la = WilburLines.Circle(_orbitA, "Ring", 64, r * 1.9f, r * 0.07f, Color.white, true);
            if (la) la.colorGradient = Rainbow(0.95f);
            GameObject bb = new GameObject("OrbitB");
            bb.transform.SetParent(transform, false);
            bb.transform.localRotation = Quaternion.Euler(-60f, 40f, 0f);
            _orbitB = bb.transform;
            LineRenderer lb = WilburLines.Circle(_orbitB, "Ring", 64, r * 1.65f, r * 0.06f, Color.white, true);
            if (lb) lb.colorGradient = Rainbow(0.95f);

            // A rainbow trail behind it.
            if (WilburLines.Mat)
            {
                _trail = gameObject.AddComponent<TrailRenderer>();
                _trail.sharedMaterial = WilburLines.Mat;
                _trail.time = 0.45f;
                _trail.widthMultiplier = r * 1.6f;
                _trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                _trail.colorGradient = Rainbow(0.8f);
                _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _trail.minVertexDistance = Mathf.Max(0.1f, r * 0.2f);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            _modeT += dt;

            // Smooth colour drift on the core (no flicker).
            if (_coreMat)
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(_t * 0.35f, 1f), 0.35f, 1f);
                WilburPlugin.Tint(_coreMat, c, c, 0.6f);
            }

            float spin = _mode == Mode.Vortex ? 420f : 160f;
            if (_orbitA) _orbitA.Rotate(Vector3.up, spin * dt, Space.World);
            if (_orbitB) _orbitB.Rotate(Vector3.up, -spin * 1.3f * dt, Space.World);
            Camera cam = Camera.main;
            if (_halo && cam) _halo.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, _t * (_mode == Mode.Vortex ? 200f : 60f));

            float target = _mode == Mode.Vortex ? Mathf.Lerp(1f, 1.5f, Mathf.Clamp01(_modeT / 0.35f)) : Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(_t / 0.2f));
            _scale = Mathf.Lerp(_scale, target, 1f - Mathf.Exp(-12f * dt));
            transform.localScale = Vector3.one * _scale * (1f + Mathf.Sin(_t * (_mode == Mode.Vortex ? 18f : 9f)) * 0.04f);

            if (_mode == Mode.Fly)
            {
                Vector3 step = _dir * _speed * dt;
                RaycastHit hit;
                // Ignore the world for a moment so a giant ball isn't stopped by the ground under Wilbur.
                bool canHit = _t > 0.2f;
                if (canHit && Physics.SphereCast(transform.position, _radius * 0.6f, _dir, out hit, step.magnitude, LayerIndex.world.mask | LayerIndex.entityPrecise.mask, QueryTriggerInteraction.Ignore)
                    && !IsOwner(hit.collider))
                {
                    transform.position += _dir * hit.distance;
                    StartVortex();
                }
                else
                {
                    transform.position += step;
                    if (_modeT >= WilburPlugin.BallMaxFlightSeconds.Value) StartVortex();
                }
                return;
            }

            // Vortex: rainbow streaks spiralling into the middle.
            _suck -= dt;
            if (_suck <= 0f)
            {
                _suck = 0.04f;
                WilburStreak.Spawn(transform.position, _pullRadius * UnityEngine.Random.Range(0.6f, 1f), Mathf.Max(0.15f, _radius * 0.12f),
                    WilburRainbowBeam.Bands[UnityEngine.Random.Range(0, 7)]);
            }

            if (_modeT >= WilburPlugin.BallVortexSeconds.Value) Explode();
        }

        private bool IsOwner(Collider c)
        {
            if (!c || !_owner) return false;
            HurtBox hb = c.GetComponent<HurtBox>();
            if (hb && hb.healthComponent && hb.healthComponent == _owner.healthComponent) return true;
            CharacterBody b = c.GetComponentInParent<CharacterBody>();
            return b == _owner;
        }

        private void StartVortex()
        {
            _mode = Mode.Vortex;
            _modeT = 0f;
            if (_trail) _trail.emitting = false;
            WilburRingWave.Spawn(transform.position, _radius, _pullRadius, 0.5f, new Color(1f, 0.6f, 0.9f), Mathf.Max(0.3f, _radius * 0.15f));
            if (_owner)
            {
                WilburAudio.PlayFor(_owner, "gas");
                WilburAudio.PlayFor(_owner, "quake");
            }
        }

        private SphereSearch Enemies(float radius)
        {
            SphereSearch search = new SphereSearch { origin = transform.position, radius = radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(_team));
            search.FilterCandidatesByDistinctHurtBoxEntities();
            return search;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || _mode != Mode.Vortex) return;
            _tick -= Time.fixedDeltaTime;
            if (_tick > 0f) return;
            _tick = 0.25f;

            foreach (HurtBox box in Enemies(_pullRadius).GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive || !hc.body) continue;
                CharacterBody v = hc.body;

                // Suck them in (bosses resist a bit).
                Vector3 to = transform.position - v.corePosition;
                float pull = WilburPlugin.BallPullSpeed.Value * (v.isChampion ? 0.4f : 1f) * Mathf.Max(1f, Mathf.Sqrt(_radius));
                Vector3 vel = to.normalized * Mathf.Min(pull, to.magnitude * 6f);
                if (v.characterMotor)
                {
                    v.characterMotor.Motor.ForceUnground();
                    v.characterMotor.velocity = vel;
                }
                else if (v.rigidbody) v.rigidbody.velocity = vel;

                DamageInfo dmg = new DamageInfo
                {
                    attacker = _owner ? _owner.gameObject : null,
                    inflictor = gameObject,
                    damage = _damage * WilburPlugin.BallDps.Value * 0.25f,
                    crit = _crit,
                    position = v.corePosition,
                    force = Vector3.zero,
                    procCoefficient = 0.4f,
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                WilburStatsController st = _owner ? _owner.GetComponent<WilburStatsController>() : null;
                if (st) st.RegisterHit(v.corePosition);
            }
        }

        private void Explode()
        {
            Vector3 at = transform.position;
            float radius = _blastRadius;
            if (NetworkServer.active && _owner)
            {
                // The weak ones get gobbled (feeding Wilbur back up), the rest take the blast.
                WilburGobble.GobbleNearby(_owner, at, radius, null, _crit);
                BlastAttack blast = new BlastAttack
                {
                    attacker = _owner.gameObject,
                    inflictor = gameObject,
                    teamIndex = _team,
                    position = at,
                    radius = radius,
                    baseDamage = _damage * WilburPlugin.BallBlast.Value,
                    baseForce = 2000f,
                    bonusForce = Vector3.up * 1200f,
                    crit = _crit,
                    damageType = DamageType.Generic,
                    falloffModel = BlastAttack.FalloffModel.None,
                    procCoefficient = 1f
                };
                blast.Fire();
                GameObject boom = null;
                try { boom = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX"); } catch { }
                if (boom) EffectManager.SpawnEffect(boom, new EffectData { origin = at, scale = radius }, true);
            }
            Color[] bands = WilburRainbowBeam.Bands;
            for (int i = 0; i < bands.Length; i++)
                WilburRingWave.Spawn(at, _radius * 0.5f, radius * (1f - i * 0.06f), 0.6f + i * 0.04f, bands[i], Mathf.Max(0.3f, _radius * 0.12f));
            WilburFx.SpawnPuffRing(at, new Color(1f, 0.8f, 0.95f));
            WilburFloatingText.ShowCartoon(at + Vector3.up * (2f + _radius), "RAINBOW BOOM!", Color.HSVToRGB(UnityEngine.Random.value, 0.6f, 1f), 1.4f);
            if (_owner) WilburAudio.PlayFor(_owner, "erupt");
            WilburShake.At(at, 3f, 0.6f, radius * 3f);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_coreMat) Destroy(_coreMat);
        }
    }

    // A short rainbow streak spiralling into a point (the rainbow ball's suction).
    public class WilburStreak : MonoBehaviour
    {
        private LineRenderer _lr;
        private Vector3 _center;
        private float _r;
        private float _ang;
        private float _y;
        private float _t;
        private const float Life = 0.45f;
        private Color _color;

        public static void Spawn(Vector3 center, float radius, float width, Color color)
        {
            if (!WilburLines.Mat)
            {
                WilburFx.SpawnSuck(center + UnityEngine.Random.onUnitSphere * radius * 0.7f, center);
                return;
            }
            GameObject go = new GameObject("WilburStreak");
            WilburStreak s = go.AddComponent<WilburStreak>();
            s._lr = go.AddComponent<LineRenderer>();
            s._lr.sharedMaterial = WilburLines.Mat;
            s._lr.useWorldSpace = true;
            s._lr.positionCount = 2;
            s._lr.widthMultiplier = width;
            s._lr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
            s._lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            s._center = center;
            s._r = radius;
            s._ang = UnityEngine.Random.value * Mathf.PI * 2f;
            s._y = UnityEngine.Random.Range(-0.5f, 0.5f) * radius;
            s._color = color;
        }

        private Vector3 At(float k)
        {
            float r = _r * (1f - k);
            float a = _ang + k * 3f;
            return _center + new Vector3(Mathf.Cos(a) * r, _y * (1f - k), Mathf.Sin(a) * r);
        }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / Life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            _lr.SetPosition(0, At(Mathf.Max(0f, k - 0.18f)));
            _lr.SetPosition(1, At(k));
            Color c = new Color(_color.r, _color.g, _color.b, Mathf.Sin(k * Mathf.PI));
            _lr.startColor = c;
            _lr.endColor = c;
        }
    }

    // Piggy Bank prizes are temporary: each one is removed again after a while. Lives on the master, so the
    // timers keep running across stages. A buff on Wilbur shows how many prizes he is holding.
    public class WilburTempItems : MonoBehaviour
    {
        private struct Entry
        {
            public ItemIndex Item;
            public float Expires;
            public string Name;
        }

        private readonly List<Entry> _items = new List<Entry>();
        private CharacterMaster _master;
        private float _syncTimer;

        public int Count { get { return _items.Count; } }

        public static WilburTempItems For(CharacterMaster master)
        {
            if (!master) return null;
            WilburTempItems t = master.GetComponent<WilburTempItems>();
            if (!t) t = master.gameObject.AddComponent<WilburTempItems>();
            t._master = master;
            return t;
        }

        public bool Give(ItemIndex item, float seconds, string name)
        {
            if (!NetworkServer.active || !_master || !_master.inventory) return false;
            if (_items.Count >= WilburPlugin.MaxTempItems.Value) return false;
            WilburPrizes.Granting = true;
            try { _master.inventory.GiveItem(item, 1); }
            finally { WilburPrizes.Granting = false; }
            _items.Add(new Entry { Item = item, Expires = Time.time + seconds, Name = name });
            SyncBuff();
            return true;
        }

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !_master) return;
            bool changed = false;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (Time.time < _items[i].Expires) continue;
                Entry e = _items[i];
                _items.RemoveAt(i);
                changed = true;
                if (_master.inventory && _master.inventory.GetItemCount(e.Item) > 0)
                {
                    WilburPrizes.Granting = true;
                    try { _master.inventory.RemoveItem(e.Item, 1); }
                    finally { WilburPrizes.Granting = false; }
                }
                CharacterBody body = _master.GetBody();
                if (body) WilburFloatingText.Show(body.corePosition + Vector3.up * 2f, "expired: " + e.Name, new Color(0.7f, 0.7f, 0.75f), 0.8f);
            }

            _syncTimer -= Time.fixedDeltaTime;
            if (changed || _syncTimer <= 0f)
            {
                _syncTimer = 1f;
                SyncBuff();
            }
        }

        private void SyncBuff()
        {
            CharacterBody body = _master ? _master.GetBody() : null;
            if (!body || WilburPlugin.TempItemBuff == null) return;
            int have = body.GetBuffCount(WilburPlugin.TempItemBuff);
            int want = _items.Count;
            int guard = 0;
            while (have < want && guard++ < 100) { body.AddBuff(WilburPlugin.TempItemBuff); have++; }
            while (have > want && guard++ < 100) { body.RemoveBuff(WilburPlugin.TempItemBuff); have--; }
        }

        // If the run ends or the master is removed, take the prizes back.
        private void OnDestroy()
        {
            if (!NetworkServer.active || !_master || !_master.inventory) return;
            foreach (Entry e in _items)
                if (_master.inventory.GetItemCount(e.Item) > 0) _master.inventory.RemoveItem(e.Item, 1);
            _items.Clear();
        }
    }

    // Slot item prizes: the better the roll, the better the odds and the rarity. Server only.
    public static class WilburPrizes
    {
        // True while Wilbur is adding/removing prize items, so the Jackpot doubler ignores them.
        public static bool Granting;

        private static readonly Color White = new Color(0.95f, 0.95f, 0.95f);
        private static readonly Color Green = new Color(0.45f, 1f, 0.4f);
        private static readonly Color Red = new Color(1f, 0.35f, 0.3f);

        // tier: 1 small win, 2 big win, 3 jackpot. scale: lower for hit spins.
        public static void TryGive(CharacterBody body, int tier, float scale, Vector3 pos)
        {
            if (!NetworkServer.active || !body || !body.master || !body.master.inventory || tier <= 0) return;
            float chance = tier == 3 ? WilburPlugin.ItemChanceJackpot.Value : (tier == 2 ? WilburPlugin.ItemChanceBig.Value : WilburPlugin.ItemChanceSmall.Value);
            if (UnityEngine.Random.value >= chance * scale) return;

            int count = tier == 3 ? Mathf.Max(1, WilburPlugin.JackpotItems.Value) : 1;
            for (int i = 0; i < count; i++) GiveOne(body, tier, pos + Vector3.up * (0.8f * i));
        }

        private static void GiveOne(CharacterBody body, int tier, Vector3 pos)
        {
            // Rarity by roll quality.
            int rarity = 1;
            float r = UnityEngine.Random.value;
            if (tier == 3) rarity = r < WilburPlugin.JackpotRedChance.Value ? 3 : (r < WilburPlugin.JackpotRedChance.Value + 0.45f ? 2 : 1);
            else if (tier == 2) rarity = r < WilburPlugin.BigRedChance.Value ? 3 : (r < 0.35f ? 2 : 1);
            else rarity = r < 0.08f ? 2 : 1;

            PickupIndex pick = RandomFrom(rarity);
            if (!pick.isValid && rarity > 1) pick = RandomFrom(rarity - 1);
            if (!pick.isValid) return;

            PickupDef def = PickupCatalog.GetPickupDef(pick);
            if (def == null || def.itemIndex == ItemIndex.None) return;
            string name = def.itemIndex.ToString();
            try
            {
                ItemDef item = ItemCatalog.GetItemDef(def.itemIndex);
                if (item) name = Language.GetString(item.nameToken);
            }
            catch (Exception) { }

            float seconds = rarity == 3 ? WilburPlugin.TempRed.Value : (rarity == 2 ? WilburPlugin.TempGreen.Value : WilburPlugin.TempWhite.Value);
            WilburTempItems temp = WilburTempItems.For(body.master);
            if (!temp || !temp.Give(def.itemIndex, seconds, name)) return;

            Color c = rarity == 3 ? Red : (rarity == 2 ? Green : White);
            WilburFloatingText.ShowCartoon(pos + Vector3.up * 2.2f, "PRIZE! " + name.ToUpperInvariant() + " (" + seconds.ToString("0") + "s)", c, rarity == 3 ? 1.6f : 1.2f);
            Chat.AddMessage("<style=cIsUtility>Piggy Bank prize:</style> " + name + " for " + seconds.ToString("0") + "s");
            WilburAudio.PlayFor(body, "prize");
            if (rarity == 3) WilburAudio.PlayFor(body, "slot_jackpot");
            try
            {
                ItemDef item = ItemCatalog.GetItemDef(def.itemIndex);
                if (item) WilburPrizeIcon.Spawn(body, item.pickupIconSprite, c);
            }
            catch (Exception) { }
            WilburPhrases.Say(body, WilburPhrases.Kind.Prize, rarity == 3 ? 1f : 0.3f);
        }

        private static PickupIndex RandomFrom(int rarity)
        {
            Run run = Run.instance;
            if (!run) return PickupIndex.none;
            string field = rarity == 3 ? "availableTier3DropList" : (rarity == 2 ? "availableTier2DropList" : "availableTier1DropList");
            try
            {
                FieldInfo f = typeof(Run).GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                System.Collections.IList list = f != null ? f.GetValue(run) as System.Collections.IList : null;
                if (list == null || list.Count == 0) return PickupIndex.none;
                object o = list[UnityEngine.Random.Range(0, list.Count)];
                return o is PickupIndex ? (PickupIndex)o : PickupIndex.none;
            }
            catch (Exception)
            {
                return PickupIndex.none;
            }
        }
    }

    // Pulls the camera back as Wilbur grows so a giant pig still fits on screen. It works on a private
    // copy of the camera settings, through reflection, and silently does nothing if the game changed them.
    public class WilburCameraZoom : MonoBehaviour
    {
        private const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private CharacterBody _body;
        private object _params;
        private FieldInfo _dataField;
        private Vector3 _basePos;
        private float _basePivot;
        private bool _hasPivot;
        private bool _ok;
        private float _cur = 1f;
        private float _applied = 1f;

        private void Start()
        {
            _body = GetComponent<CharacterBody>();
            try
            {
                CameraTargetParams ctp = GetComponent<CameraTargetParams>();
                if (!ctp) return;
                FieldInfo pf = typeof(CameraTargetParams).GetField("cameraParams", BF);
                ScriptableObject orig = pf != null ? pf.GetValue(ctp) as ScriptableObject : null;
                if (!orig) return;
                ScriptableObject copy = Object.Instantiate(orig);
                _dataField = copy.GetType().GetField("data", BF);
                if (_dataField == null) return;

                object data = _dataField.GetValue(copy);
                object v = Get(Get(data, "idealLocalCameraPos"), "value");
                if (!(v is Vector3)) return;
                _basePos = (Vector3)v;
                object pv = Get(Get(data, "pivotVerticalOffset"), "value");
                _hasPivot = pv is float;
                if (_hasPivot) _basePivot = (float)pv;

                pf.SetValue(ctp, copy);
                _params = copy;
                _ok = true;
            }
            catch (Exception e)
            {
                if (WilburPlugin.Log != null) WilburPlugin.Log.LogInfo("Wilbur camera zoom unavailable: " + e.Message);
            }
        }

        private static object Get(object obj, string name)
        {
            if (obj == null) return null;
            FieldInfo f = obj.GetType().GetField(name, BF);
            return f != null ? f.GetValue(obj) : null;
        }

        private static void Set(object obj, string name, object value)
        {
            if (obj == null) return;
            FieldInfo f = obj.GetType().GetField(name, BF);
            if (f != null) f.SetValue(obj, value);
        }

        private void LateUpdate()
        {
            if (!_ok || !_body || !WilburPlugin.CameraZoom.Value) return;
            float size = WilburGobble.SizeMul(_body) * (_body.HasBuff(WilburPlugin.SuperBuff) ? 1.35f : 1f);
            float target = Mathf.Min(1f + (size - 1f) * WilburPlugin.CameraZoomStrength.Value, Mathf.Max(1f, WilburPlugin.CameraMaxZoom.Value));
            _cur = Mathf.Lerp(_cur, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
            if (Mathf.Abs(_cur - _applied) < 0.005f) return;
            _applied = _cur;

            try
            {
                object data = _dataField.GetValue(_params);
                object ideal = Get(data, "idealLocalCameraPos");
                Set(ideal, "value", _basePos * _cur);
                Set(data, "idealLocalCameraPos", ideal);
                if (_hasPivot)
                {
                    object piv = Get(data, "pivotVerticalOffset");
                    Set(piv, "value", _basePivot * (1f + (_cur - 1f) * 0.8f));
                    Set(data, "pivotVerticalOffset", piv);
                }
                _dataField.SetValue(_params, data);
            }
            catch (Exception)
            {
                _ok = false;
            }
        }
    }

    // ------------------------------------------------------------------------------------------------
    // R: Super Piggy
    // ------------------------------------------------------------------------------------------------
    public class WilburSuperState : BaseSkillState
    {
        private static GameObject _bigBoom;
        private static GameObject _smallBoom;
        private static bool _fxLoaded;

        private readonly Dictionary<HealthComponent, float> _ramCooldown = new Dictionary<HealthComponent, float>();
        private float _auraTick;
        private Vector3 _flyVel;

        private static void LoadFx()
        {
            if (_fxLoaded) return;
            _fxLoaded = true;
            try { _bigBoom = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX"); } catch { }
            try { _smallBoom = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick"); } catch { }
        }

        public override void OnEnter()
        {
            base.OnEnter();
            LoadFx();
            Util.PlaySound("Play_huntress_shift_mini_blink", gameObject);
            WilburAudio.PlayFor(characterBody, "squeal");
            WilburAudio.PlayFor(characterBody, "erupt");
            WilburAudio.PlayFor(characterBody, "whoosh");
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Super, 1f);
            WilburShake.At(characterBody.corePosition, 2f, 0.5f, 50f * Mathf.Sqrt(WilburGobble.SizeMul(characterBody)));

            if (characterMotor)
            {
                characterMotor.useGravity = false;
                characterMotor.Motor.ForceUnground();
                // Launch off the ground.
                if (isAuthority) characterMotor.velocity = new Vector3(characterMotor.velocity.x, 14f, characterMotor.velocity.z);
            }

            if (NetworkServer.active)
            {
                WilburSuperServer sup = characterBody.GetComponent<WilburSuperServer>();
                if (sup) sup.Begin(WilburPlugin.SuperDuration.Value);

                // Transformation shockwave.
                Blast(characterBody.corePosition, Mathf.Min(WilburPlugin.SuperBlastRadius.Value * WilburGobble.SizeMul(characterBody), WilburPlugin.MaxReach.Value),
                    damageStat * WilburPlugin.SuperStartBlast.Value);
            }
            WilburFloatingText.Show(characterBody.corePosition + Vector3.up * 2.5f, "SUPER PIGGY!", new Color(1f, 0.82f, 0.2f), 1.6f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            Fly();
            if (NetworkServer.active)
            {
                Ram();
                Aura();
            }

            HandleRainbows();

            // Press the key again to end it early.
            if (isAuthority && inputBank)
            {
                if (!inputBank.skill4.down) _released = true;
                else if (_released && fixedAge > 0.3f)
                {
                    outer.SetNextStateToMain();
                    return;
                }
            }

            // The server owns the timer; give the buff a moment to replicate before checking it.
            if (fixedAge > 0.6f && !characterBody.HasBuff(WilburPlugin.SuperBuff) && isAuthority)
                outer.SetNextStateToMain();
        }

        private bool _released;
        private float _ballCooldown;

        // Left click: rainbow laser while held. Right click: rainbow ball on its own short cooldown.
        // Left click: rainbow laser while held. Right click: puke a rainbow ball (own short cooldown).
        // Both run as states on the Weapon state machine, so every player (and the server) sees them.
        private void HandleRainbows()
        {
            if (!isAuthority || !inputBank) return;
            if (!_weapon) _weapon = EntityStateMachine.FindByCustomName(gameObject, "Weapon");
            if (!_weapon) return;

            _ballCooldown -= Time.fixedDeltaTime;
            bool puking = _weapon.state is WilburPukeState;
            if (inputBank.skill2.down && _ballCooldown <= 0f && !puking)
            {
                _ballCooldown = WilburPlugin.BallCooldown.Value;
                _weapon.SetNextState(new WilburPukeState
                {
                    AimDir = GetAimRay().direction,
                    Size = WilburGobble.SizeMul(characterBody)
                });
                return;
            }
            if (inputBank.skill1.down && !puking && !(_weapon.state is WilburLaserState))
                _weapon.SetNextState(new WilburLaserState());
        }

        private EntityStateMachine _weapon;

        // Camera-relative flight: move with WASD as normal, hold Jump to rise, hold Sprint to sink.
        private void Fly()
        {
            if (!characterMotor || !inputBank) return;
            float dt = Time.fixedDeltaTime;

            Vector3 move = inputBank.moveVector;
            move.y = 0f;
            float vert = 0f;
            if (inputBank.jump.down) vert += 1f;
            if (inputBank.sprint.down) vert -= 1f;

            Vector3 dir = move + Vector3.up * vert;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            Vector3 target = dir * (moveSpeedStat * WilburPlugin.SuperSpeed.Value);
            if (fixedAge < dt * 1.5f) _flyVel = characterMotor.velocity;
            _flyVel = Vector3.Lerp(_flyVel, target, 1f - Mathf.Exp(-7f * dt));

            characterMotor.Motor.ForceUnground();
            characterMotor.velocity = _flyVel;

            Vector3 face = inputBank.aimDirection;
            face.y = 0f;
            if (characterDirection && face.sqrMagnitude > 0.01f) characterDirection.forward = face.normalized;
        }

        private SphereSearch Enemies(Vector3 origin, float radius)
        {
            SphereSearch search = new SphereSearch { origin = origin, radius = radius, mask = LayerIndex.entityPrecise.mask };
            search.RefreshCandidates();
            search.FilterCandidatesByHurtBoxTeam(TeamMask.GetEnemyTeams(teamComponent.teamIndex));
            search.FilterCandidatesByDistinctHurtBoxEntities();
            return search;
        }

        // Smashing into enemies: huge hit, and anything small enough is gobbled on contact.
        private void Ram()
        {
            if (!characterMotor || characterMotor.velocity.magnitude < 4f) return;
            float size = WilburGobble.SizeMul(characterBody);
            bool crit = RollCrit();
            Vector3 dir = characterMotor.velocity.normalized;

            foreach (HurtBox box in Enemies(characterBody.corePosition, Mathf.Min(3.5f * size, WilburPlugin.MaxReach.Value * 0.5f)).GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive) continue;
                float next;
                if (_ramCooldown.TryGetValue(hc, out next) && Time.time < next) continue;
                _ramCooldown[hc] = Time.time + 0.4f;

                Vector3 at = hc.body ? hc.body.corePosition : box.transform.position;
                if (WilburGobble.CanEat(characterBody, hc))
                {
                    WilburGobble.Gobble(characterBody, hc, crit);
                    continue;
                }

                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.SuperRam.Value,
                    crit = crit,
                    position = at,
                    force = dir * 8000f + Vector3.up * 2000f,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                if (_smallBoom) EffectManager.SpawnEffect(_smallBoom, new EffectData { origin = at, scale = 3f }, true);
            }
        }

        private WilburStatsController _statsRef;

        private void Combo(Vector3 at)
        {
            if (!_statsRef) _statsRef = characterBody.GetComponent<WilburStatsController>();
            if (_statsRef) _statsRef.RegisterHit(at);
        }

        // A golden aura that keeps burning everything around you while you fly.
        private void Aura()
        {
            _auraTick -= Time.fixedDeltaTime;
            if (_auraTick > 0f) return;
            _auraTick = 0.5f;

            float size = WilburGobble.SizeMul(characterBody);
            bool crit = RollCrit();
            foreach (HurtBox box in Enemies(characterBody.corePosition, Mathf.Min(WilburPlugin.SuperAuraRadius.Value * size, WilburPlugin.MaxReach.Value)).GetHurtBoxes())
            {
                HealthComponent hc = box ? box.healthComponent : null;
                if (!hc || !hc.alive) continue;
                DamageInfo dmg = new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = damageStat * WilburPlugin.SuperAuraDamage.Value,
                    crit = crit,
                    position = hc.body ? hc.body.corePosition : box.transform.position,
                    force = Vector3.zero,
                    procCoefficient = 0.5f,
                    damageColorIndex = DamageColorIndex.Item,
                    damageType = DamageType.Generic
                };
                hc.TakeDamage(dmg);
                GlobalEventManager.instance.OnHitEnemy(dmg, hc.gameObject);
                Combo(dmg.position);
            }
        }

        private void Blast(Vector3 pos, float radius, float damage)
        {
            BlastAttack blast = new BlastAttack
            {
                attacker = gameObject,
                inflictor = gameObject,
                teamIndex = teamComponent.teamIndex,
                position = pos,
                radius = radius,
                baseDamage = damage,
                baseForce = 2500f,
                bonusForce = Vector3.up * 1500f,
                crit = RollCrit(),
                damageType = DamageType.Generic,
                falloffModel = BlastAttack.FalloffModel.None,
                procCoefficient = 1f
            };
            blast.Fire();
            if (_bigBoom) EffectManager.SpawnEffect(_bigBoom, new EffectData { origin = pos, scale = radius }, true);
            WilburFx.SpawnPuffRing(pos, new Color(1f, 0.8f, 0.2f));
        }

        public override void OnExit()
        {
            if (isAuthority && _weapon && (_weapon.state is WilburLaserState)) _weapon.SetNextStateToMain();
            if (characterMotor) characterMotor.useGravity = true;
            if (NetworkServer.active && characterBody)
            {
                WilburSuperServer sup = characterBody.GetComponent<WilburSuperServer>();
                if (sup) sup.End();
            }
            if (!characterBody || !characterBody.healthComponent || !characterBody.healthComponent.alive)
            {
                base.OnExit();
                return;
            }

            // Finale: HAM SLAM.
            LoadFx();
            WilburAudio.PlayFor(characterBody, "erupt");
            WilburAudio.PlayFor(characterBody, "stomp");
            WilburPigController.PlayOn(characterBody, WilburAnim.Stomp);
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.HamSlam, 1f);
            WilburShake.At(characterBody.footPosition, 4f, 0.7f, 80f * Mathf.Sqrt(WilburGobble.SizeMul(characterBody)));
            if (NetworkServer.active)
            {
                Vector3 at = characterBody.footPosition;
                Blast(at, Mathf.Min(WilburPlugin.SuperBlastRadius.Value * 1.2f * WilburGobble.SizeMul(characterBody), WilburPlugin.MaxReach.Value),
                    damageStat * WilburPlugin.SuperEndBlast.Value);
            }
            WilburFloatingText.ShowCartoon(characterBody.corePosition + Vector3.up * 2.5f, "HAM SLAM!", new Color(1f, 0.82f, 0.2f), 1.6f);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }

    // Super Piggy's rainbow laser as a networked state (Weapon machine): every machine sees the beam and
    // Wilbur's laser pose, and the server deals the damage.
    public class WilburLaserState : BaseState
    {
        private WilburRainbowBeam _beam;

        public override void OnEnter()
        {
            base.OnEnter();
            _beam = WilburRainbowBeam.Attach(characterBody);
            _beam.SetFiring(true);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!isAuthority) return;
            if (!inputBank || !inputBank.skill1.down || !characterBody.HasBuff(WilburPlugin.SuperBuff))
                outer.SetNextStateToMain();
        }

        public override void OnExit()
        {
            if (_beam) _beam.SetFiring(false);
            base.OnExit();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }
    }

    // Super Piggy's rainbow ball as a networked state: everyone sees the heave, the puke and the ball;
    // the server deals the damage and takes Wilbur's stacks.
    public class WilburPukeState : BaseState
    {
        public Vector3 AimDir;
        public float Size = 1f;
        private bool _fired;
        private const float FireAt = 0.28f;
        private const float Duration = 0.6f;

        public override void OnEnter()
        {
            base.OnEnter();
            if (isAuthority && AimDir == Vector3.zero)
            {
                AimDir = GetAimRay().direction;
                Size = WilburGobble.SizeMul(characterBody);
            }
            WilburPigController.PlayOn(characterBody, WilburAnim.Puke);
            WilburAudio.PlayFor(characterBody, "puke");
            WilburPhrases.Say(characterBody, WilburPhrases.Kind.Rainbow, 1f);
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (!_fired && fixedAge >= FireAt)
            {
                _fired = true;
                Fire();
            }
            if (isAuthority && fixedAge >= Duration) outer.SetNextStateToMain();
        }

        private void Fire()
        {
            WilburPigController pig = WilburPigController.Find(characterBody);
            Vector3 dir = AimDir.sqrMagnitude > 0.01f ? AimDir.normalized : characterBody.transform.forward;
            Vector3 from = pig ? pig.MouthPosition : characterBody.corePosition;
            Ray aim = new Ray(inputBank ? inputBank.aimOrigin : from, dir);
            WilburRainbowBall.Spawn(from, aim, characterBody, RollCrit(), Size);
            WilburFx.SpawnPuke(from, dir, Size);
            WilburAudio.PlayFor(characterBody, "rainbow");
            WilburAudio.PlayFor(characterBody, "whoosh");
            WilburShake.At(from, 2f, 0.35f, 40f + Size * 2f);

            if (NetworkServer.active)
            {
                WilburStatsController stats = characterBody.GetComponent<WilburStatsController>();
                int stacks = WilburGobble.Stacks(characterBody);
                if (stats && stacks > 0) stats.Deflate(stacks);
            }
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(AimDir);
            writer.Write(Size);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            AimDir = reader.ReadVector3();
            Size = reader.ReadSingle();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }

}
