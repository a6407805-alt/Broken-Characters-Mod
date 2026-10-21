using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace DuelistMod
{
    public enum CardType { Monster, Spell, Trap, Extra }

    public enum CardEffect
    {
        None,
        DamageOne,  // spell: damage the targeted species
        DamageAll,  // spell: damage every species on the field
        Heal,       // spell
        Draw,       // spell
        Negate,     // trap: cancels an enemy attack
        Reflect,    // trap: cancels an attack and hits the attacking species with it
        Counter,    // counter trap (Spell Speed 3): negates an enemy effect's activation
        Ritual,     // spell: Ritual Summon a Ritual monster from your hand
        Revive      // spell: Special Summon a monster from your graveyard
    }

    /// <summary>How an Extra Deck monster is summoned.</summary>
    public enum ExtraKind { None, Fusion, Synchro, Xyz, Link }

    /// <summary>
    /// Where cards come from:
    ///   Spells   — white, green, red and boss items (damage items hit, healing items heal, utility items draw)
    ///   Traps    — equipment, lunar (blue) items, void (pink) items
    ///   Monsters — drones and other robots you buy or repair; the biggest ones are Extra Deck fusions
    /// Every copy is its own card. Main deck max 60 (40 recommended).
    /// Monster ATK is real damage per enemy unit at full range, HP is real HP, read live from your stats.
    /// </summary>
    public class Card
    {
        public string Name;
        public CardType Type;
        public CardEffect Effect = CardEffect.None;
        public int Level;
        public float AtkCoeff;      // x player damage
        public float HpFraction;    // x player max HP
        public float Power;         // spell damage coeff / heal fraction / draw count
        public int Charges = 1;     // trap uses
        public bool HitsAll;        // monster attacks hit every species
        public Texture Art;
        public ItemIndex Item = ItemIndex.None;
        public EquipmentIndex Equip = EquipmentIndex.None;
        public BodyIndex Body = BodyIndex.None; // robots: the model shown on your zone
        public bool IsTuner;                    // Synchro material
        public bool IsRitual;                   // Ritual monster: only via a Ritual spell
        public ExtraKind Extra = ExtraKind.None;
        public bool Generic;                    // Extra toolkit monster: stats come from its materials
        public int Rank;                        // XYZ
        public int LinkRating;                  // Link
        public string Source = "";              // "White item", "Equipment", "Drone"…
        public PartnerKind Partner = PartnerKind.None; // one of your 5 partners: 3D model + its own effect

        public int TributesNeeded
        {
            get
            {
                if (Type == CardType.Extra || IsRitual) return 0; // summoned their own way
                if (Type != CardType.Monster) return 0;
                return Level >= 7 ? 2 : Level >= 5 ? 1 : 0;
            }
        }

        public bool NeedsTarget => Effect == CardEffect.DamageOne;

        // ---- Yu-Gi-Oh! numbers: every monster has ATK and DEF on the card game's scale (multiples of 50)
        /// <summary>ATK on the card game's scale. 1000 ATK = a monster as strong as a Gunner Drone.</summary>
        public int AtkPts => Generic && AtkCoeff <= 0f ? 0 : DuelStats.Round50(1000f * AtkCoeff / Mathf.Max(0.01f, DuelConfig.MonsterAtkCoeff.Value));
        /// <summary>DEF on the card game's scale.</summary>
        public int DefPts => Generic && HpFraction <= 0f ? 0 : DuelStats.Round50(1000f * HpFraction / Mathf.Max(0.01f, DuelConfig.MonsterHpFraction.Value));

        /// <summary>Spell Speed: 1 normal spells, 2 quick-play spells and traps, 3 counter traps. 0 for monsters.</summary>
        public int SpellSpeed
        {
            get
            {
                if (Type == CardType.Spell) return IsQuickPlay ? 2 : 1;
                if (Type == CardType.Trap) return Effect == CardEffect.Counter ? 3 : 2;
                return 0;
            }
        }
        /// <summary>Healing items are Quick-Play spells: usable in your Battle Phase, or set and used on the enemy's turn.</summary>
        public bool IsQuickPlay => Type == CardType.Spell && Effect == CardEffect.Heal;
        public bool IsCounterTrap => Type == CardType.Trap && Effect == CardEffect.Counter;
        /// <summary>Spell power on the card game's scale: how much ATK/DEF a damage spell strips, or LP it burns.</summary>
        public int SpellPts => DuelStats.Round50(300f * Power);

        /// <summary>Level used for Synchro/XYZ/Ritual math. XYZ and Link monsters have none.</summary>
        public int MaterialLevel => Extra == ExtraKind.Xyz || Extra == ExtraKind.Link ? 0 : Level;

        public float Atk(CharacterBody p) => p ? p.damage * AtkCoeff : 0f;
        public float MaxHp(CharacterBody p) => p && p.healthComponent ? p.healthComponent.fullHealth * HpFraction : 1f;

        public Color Frame
        {
            get
            {
                switch (Type)
                {
                    case CardType.Monster: return IsRitual ? new Color(0.25f, 0.42f, 0.8f) : new Color(0.78f, 0.52f, 0.24f);
                    case CardType.Spell: return new Color(0.12f, 0.58f, 0.52f);
                    case CardType.Trap: return new Color(0.68f, 0.22f, 0.52f);
                    default:
                        switch (Extra)
                        {
                            case ExtraKind.Synchro: return new Color(0.82f, 0.82f, 0.85f);
                            case ExtraKind.Xyz: return new Color(0.14f, 0.14f, 0.16f);
                            case ExtraKind.Link: return new Color(0.15f, 0.35f, 0.75f);
                            default: return new Color(0.52f, 0.32f, 0.78f);
                        }
                }
            }
        }

        public string TypeLine
        {
            get
            {
                switch (Type)
                {
                    case CardType.Monster:
                        if (IsRitual) return $"RITUAL MONSTER · Lv {Level}";
                        return $"MONSTER · Lv {Level}" + (IsTuner ? " · TUNER" : "") + (TributesNeeded > 0 ? $" · {TributesNeeded} tribute" : "");
                    case CardType.Spell: return Effect == CardEffect.Ritual ? "RITUAL SPELL" : IsQuickPlay ? "QUICK-PLAY SPELL · SS2" : "NORMAL SPELL · SS1";
                    case CardType.Trap: return IsCounterTrap ? "COUNTER TRAP · SS3" : "NORMAL TRAP · SS2";
                    default:
                        switch (Extra)
                        {
                            case ExtraKind.Synchro: return Level > 0 ? $"SYNCHRO · Lv {Level}" : "SYNCHRO · EXTRA DECK";
                            case ExtraKind.Xyz: return Rank > 0 ? $"XYZ · Rank {Rank}" : "XYZ · EXTRA DECK";
                            case ExtraKind.Link: return LinkRating > 0 ? $"LINK-{LinkRating}" : "LINK · EXTRA DECK";
                            default: return "FUSION · EXTRA DECK";
                        }
                }
            }
        }

        public string Describe(CharacterBody p)
        {
            if (Partner != PartnerKind.None) return PartnerText(Partner);
            switch (Effect)
            {
                case CardEffect.DamageOne: return $"Target 1 enemy monster: it loses {SpellPts} ATK and DEF until the End Phase. If its DEF hits 0, destroy it. No monsters? Inflict {SpellPts} damage to the enemy's LP.";
                case CardEffect.DamageAll: return $"Every enemy monster loses {SpellPts} ATK and DEF until the End Phase; any whose DEF hits 0 is destroyed. No monsters? Inflict {SpellPts} damage to the enemy's LP.";
                case CardEffect.Heal: return $"Quick-Play: gain {Mathf.RoundToInt(Power * 8000f / 50f) * 50} LP ({Power * 100f:0}% of your max health). Can be set and chained on the enemy's turn.";
                case CardEffect.Draw: return $"Draw {Mathf.RoundToInt(Power)} card(s).";
                case CardEffect.Negate: return "When an enemy monster declares an attack: negate the attack, then end the enemy's Battle Phase.";
                case CardEffect.Reflect: return "When an enemy monster declares an attack: destroy that monster. (Against the commander: inflict half its ATK to the enemy's LP instead.)";
                case CardEffect.Counter: return "Counter Trap. When the enemy activates an effect: negate the activation, and that card can't be used again this duel.";
                case CardEffect.Ritual: return "Ritual Summon a Ritual monster from your hand by tributing monsters whose Levels total its Level or more.";
                case CardEffect.Revive: return "Special Summon a monster from your graveyard. If it leaves the field again, it's banished.";
            }
            if (IsRitual) return $"Ritual Summon only (Ritual spell + tributes totaling Lv {Level}).";
            if (Type == CardType.Extra) return Requirement + (HitsAll ? " Attacks EVERY enemy monster, one battle after another." : "") + ExtraText;
            if (HitsAll) return "Attacks every enemy monster, one battle after another.";
            return "A monster. Battles are settled by ATK and DEF.";
        }

        /// <summary>What it takes to summon this Extra Deck monster.</summary>
        public string Requirement
        {
            get
            {
                switch (Extra)
                {
                    case ExtraKind.Synchro: return "1 Tuner + 1 or more non-Tuners. Level = their total.";
                    case ExtraKind.Xyz: return "2 or more monsters of the same Level. Rank = that Level.";
                    case ExtraKind.Link: return "2 or more monsters. Link rating = how many.";
                    default: return "2 monsters.";
                }
            }
        }

        private string ExtraText
        {
            get
            {
                switch (Extra)
                {
                    case ExtraKind.Xyz: return " Detach a material: this turn it attacks every enemy monster.";
                    case ExtraKind.Link: return $" Your other monsters gain +{DuelConfig.LinkBoost.Value * 100f:0}% ATK per Link rating. Link monsters have no DEF and can't change to defense.";
                    default: return "";
                }
            }
        }

        /// <summary>The four toolkit monsters every Extra Deck starts with.</summary>
        public static List<Card> ExtraToolkit()
        {
            return new List<Card>
            {
                new Card { Name = "Scrap Chimera", Type = CardType.Extra, Extra = ExtraKind.Fusion, Generic = true, Source = "Toolkit" },
                new Card { Name = "Tuned Colossus", Type = CardType.Extra, Extra = ExtraKind.Synchro, Generic = true, HitsAll = true, Source = "Toolkit" },
                new Card { Name = "Overlay Sentinel", Type = CardType.Extra, Extra = ExtraKind.Xyz, Generic = true, Source = "Toolkit" },
                new Card { Name = "Link Arbiter", Type = CardType.Extra, Extra = ExtraKind.Link, Generic = true, Source = "Toolkit" },
            };
        }

        /// <summary>
        /// A summoned copy of an Extra Deck card. Toolkit monsters take their stats from the materials:
        /// ATK/DEF are the materials' totals times a multiplier, and the look comes from the first material.
        /// </summary>
        public Card Summoned(List<Card> materials, int totalAtk, int totalDef, CharacterBody p)
        {
            Card c = (Card)MemberwiseClone();
            int levels = 0;
            foreach (Card m in materials) levels += m.MaterialLevel;
            switch (Extra)
            {
                case ExtraKind.Synchro: c.Level = levels; break;
                case ExtraKind.Xyz: c.Rank = materials.Count > 0 ? materials[0].MaterialLevel : 0; c.Level = 0; break;
                case ExtraKind.Link: c.LinkRating = materials.Count; c.Level = 0; break;
                default: if (c.Level == 0) c.Level = Mathf.Max(5, levels); break;
            }
            if (Generic && p)
            {
                float atkMult, hpMult;
                switch (Extra)
                {
                    case ExtraKind.Synchro: atkMult = 1.3f; hpMult = 1f; break;
                    case ExtraKind.Xyz: atkMult = 1f; hpMult = 1.3f; break;
                    case ExtraKind.Link: atkMult = 0.7f; hpMult = 0.7f; break;
                    default: atkMult = 1.2f; hpMult = 0.9f; break;
                }
                // back from card-game points to the coefficients the card stores
                c.AtkCoeff = totalAtk * atkMult / 1000f * DuelConfig.MonsterAtkCoeff.Value;
                c.HpFraction = totalDef * hpMult / 1000f * DuelConfig.MonsterHpFraction.Value;
                if (Extra == ExtraKind.Link) c.HpFraction = 0f;
                Card look = materials.Find(m => m.Body != BodyIndex.None || m.Item != ItemIndex.None);
                if (look != null) { c.Body = look.Body; c.Item = look.Item; if (!c.Art) c.Art = look.Art; }
            }
            return c;
        }

        // ------------------------------------------------------------ starter cards

        public static List<Card> StarterDeck()
        {
            return new List<Card>
            {
                Tuner(),
                new Card { Name = "Card Toss", Type = CardType.Spell, Effect = CardEffect.DamageOne, Power = DuelConfig.CardTossSpell.Value, Source = "Starter" },
                new Card { Name = "Patch Up", Type = CardType.Spell, Effect = CardEffect.Heal, Power = 0.15f, Source = "Starter" },
                new Card { Name = "Pot of Plenty", Type = CardType.Spell, Effect = CardEffect.Draw, Power = 2f, Source = "Starter" },
                new Card { Name = "Brace", Type = CardType.Trap, Effect = CardEffect.Negate, Source = "Starter" },
                new Card { Name = "Teleporter Rite", Type = CardType.Spell, Effect = CardEffect.Ritual, Source = "Starter" },
                new Card { Name = "Second Chance", Type = CardType.Spell, Effect = CardEffect.Revive, Source = "Starter" },
            };
        }

        public const int StarterCount = 7;

        // ---- partner monsters (the 5 companions from running around), each with its own effect

        public static int PartnerCount => DuelConfig.PartnersInDeck.Value ? 5 : 0;

        public static List<Card> PartnerCards()
        {
            List<Card> list = new List<Card>();
            if (!DuelConfig.PartnersInDeck.Value) return list;
            list.Add(PartnerCard(PartnerKind.Featherpuff, 3, 0.5f, 1.1f));
            list.Add(PartnerCard(PartnerKind.Tock, 4, 0.8f, 0.8f));
            list.Add(PartnerCard(PartnerKind.HollowIdol, 5, 1.4f, 1.1f));
            list.Add(PartnerCard(PartnerKind.Jinx, 4, 0.9f, 0.9f));
            list.Add(PartnerCard(PartnerKind.Gloop, 3, 0.6f, 1.3f));
            return list;
        }

        private static Card PartnerCard(PartnerKind k, int level, float atk, float hp)
        {
            PartnerInfo info = Partners.Info(k);
            Card c = new Card
            {
                Name = info != null ? info.Name : k.ToString(),
                Type = CardType.Monster, Level = level, Partner = k, Source = "Partner",
                AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * atk,
                HpFraction = DuelConfig.MonsterHpFraction.Value * hp
            };
            try { if (info != null) c.Art = DuelistIcons.RelicTexture(info.IconBg, info.RelicSymbol); } catch { }
            return c;
        }

        public static string PartnerText(PartnerKind k)
        {
            switch (k)
            {
                case PartnerKind.Featherpuff: return "FEATHER WARD: while face-up, the first enemy attack each enemy turn is negated.";
                case PartnerKind.Tock: return "TIME ROULETTE: when summoned, flip a coin. Heads: time stops, the enemy skips its next Battle Phase. Tails: draw 2 cards.";
                case PartnerKind.HollowIdol: return "SOUL ABSORB: each time it destroys an enemy monster by battle, it permanently gains 25% ATK.";
                case PartnerKind.Jinx: return "MIND TAUNT: enemy monsters that attack must attack Jinx. ENCORE: when Jinx is destroyed, Special Summon your strongest monster from the graveyard.";
                case PartnerKind.Gloop: return "REFORM: once per duel, when destroyed, it re-forms in your next Standby Phase.";
            }
            return "";
        }

        private static Card Tuner() => new Card
        {
            Name = "Drifting Spark", Type = CardType.Monster, Level = 2, IsTuner = true, Source = "Starter",
            AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * 0.4f, HpFraction = DuelConfig.MonsterHpFraction.Value * 0.5f
        };

        private static Card Token() => new Card
        {
            Name = "Drifting Survivor", Type = CardType.Monster, Level = 3, Source = "Starter",
            AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * 0.6f, HpFraction = DuelConfig.MonsterHpFraction.Value * 0.7f
        };

        // ------------------------------------------------------------ items → spells / traps

        /// <summary>White/green/red/boss items are spells; lunar and void items are traps.</summary>
        public static Card FromItem(ItemIndex index)
        {
            ItemDef def = ItemCatalog.GetItemDef(index);
            if (!def) return null;
            ItemTier tier = def.tier;
            string name = def.name;
            try { name = Language.GetString(def.nameToken); } catch { }
            Card c = new Card { Name = name, Art = def.pickupIconTexture, Item = index };

            switch (tier)
            {
                case ItemTier.Lunar:
                    c.Type = CardType.Trap; c.Effect = CardEffect.Counter; c.Source = "Lunar item";
                    return c;
                case ItemTier.VoidTier1:
                case ItemTier.VoidTier2:
                case ItemTier.VoidTier3:
                case ItemTier.VoidBoss:
                    c.Type = CardType.Trap; c.Effect = CardEffect.Negate; c.Source = "Void item";
                    return c;
            }

            if (def.name == "ExtraLife")
            {
                c.Type = CardType.Spell; c.Effect = CardEffect.Revive; c.Source = "Red item";
                return c;
            }
            if (tier == ItemTier.Boss)
            {
                // Boss items are Ritual monsters: summoned with a Ritual spell and Lv 8 of tributes.
                c.Type = CardType.Monster; c.IsRitual = true; c.Level = 8; c.Source = "Boss item";
                c.AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * 2.5f;
                c.HpFraction = DuelConfig.MonsterHpFraction.Value * 2.5f;
                return c;
            }

            c.Type = CardType.Spell;
            float tw = TierWeight(tier);
            c.Source = tier == ItemTier.Tier1 ? "White item" : tier == ItemTier.Tier2 ? "Green item" : "Red item";
            if (def.ContainsTag(ItemTag.Healing))
            {
                c.Effect = CardEffect.Heal;
                c.Power = Mathf.Min(1f, DuelConfig.HealCardFraction.Value * tw);
            }
            else if (def.ContainsTag(ItemTag.Utility))
            {
                c.Effect = CardEffect.Draw;
                c.Power = tier == ItemTier.Tier1 ? 1f : tier == ItemTier.Tier2 ? 2f : 3f;
            }
            else if (tier == ItemTier.Tier3)
            {
                c.Effect = CardEffect.DamageAll;
                c.Power = DuelConfig.SpellDamage.Value * 0.6f * tw;
            }
            else
            {
                c.Effect = CardEffect.DamageOne;
                c.Power = DuelConfig.SpellDamage.Value * tw;
            }
            return c;
        }

        /// <summary>Equipment is a trap: it springs on an enemy attack and hits them back.</summary>
        public static Card FromEquipment(EquipmentIndex index)
        {
            EquipmentDef def = EquipmentCatalog.GetEquipmentDef(index);
            if (!def) return null;
            string name = def.name;
            try { name = Language.GetString(def.nameToken); } catch { }
            return new Card
            {
                Name = name, Art = def.pickupIconTexture, Equip = index, Type = CardType.Trap,
                Effect = def.isLunar ? CardEffect.Counter : CardEffect.Reflect,
                Source = def.isLunar ? "Lunar equipment" : "Equipment"
            };
        }

        /// <summary>Drones and robots are monsters. The toughest ones (TC-280 and up) are Extra Deck fusions.</summary>
        public static Card FromDrone(BodyIndex body)
        {
            GameObject prefab = BodyCatalog.GetBodyPrefab(body);
            CharacterBody cb = prefab ? prefab.GetComponent<CharacterBody>() : null;
            if (!cb) return null;
            string name = prefab.name;
            try { name = Language.GetString(cb.baseNameToken); } catch { }

            float w = DroneWeight(cb);
            Card c = new Card
            {
                Name = name, Art = cb.portraitIcon, Body = body, Source = "Drone",
                AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * w,
                HpFraction = DuelConfig.MonsterHpFraction.Value * w
            };
            ExtraKind rare = RareDroneKind(prefab.name, w);
            if (rare != ExtraKind.None)
            {
                // Rare robots are Extra Deck monsters, each its own kind of summon.
                c.Type = CardType.Extra;
                c.Extra = rare;
                c.Source = "Rare drone";
                float boost = Mathf.Max(1.4f, w);
                c.AtkCoeff = DuelConfig.MonsterAtkCoeff.Value * boost;
                c.HpFraction = DuelConfig.MonsterHpFraction.Value * boost;
                switch (rare)
                {
                    case ExtraKind.Fusion: c.Level = 8; c.HitsAll = true; break;
                    case ExtraKind.Synchro: c.Level = 7; break;
                    case ExtraKind.Xyz: c.Level = 0; break;
                    case ExtraKind.Link: c.Level = 0; c.HpFraction = 0f; break;
                }
            }
            else
            {
                c.Type = CardType.Monster;
                c.Level = w >= 1.8f ? 6 : w >= 1.2f ? 4 : 3;
                // Support robots tune the others: Healing and Emergency drones are Tuners.
                string pn = prefab.name;
                c.IsTuner = pn.StartsWith("Drone2") || pn.Contains("Emergency");
            }
            return c;
        }

        /// <summary>How strong a robot is, from its base health (a Gunner Drone is about 1).</summary>
        public static float DroneWeight(CharacterBody cb) => Mathf.Clamp(cb.baseMaxHealth / 150f, 0.8f, 3.5f);

        public static bool IsExtraDrone(BodyIndex body)
        {
            GameObject prefab = BodyCatalog.GetBodyPrefab(body);
            CharacterBody cb = prefab ? prefab.GetComponent<CharacterBody>() : null;
            return cb && RareDroneKind(prefab.name, DroneWeight(cb)) != ExtraKind.None;
        }

        /// <summary>
        /// Drone rarity. Common robots (Gunner Drone, Healing Drone, Gunner Turret) are main-deck monsters;
        /// rare ones are Extra Deck monsters: Incinerator = Synchro, Missile = Xyz, Emergency/Equipment = Link,
        /// TC-280 (and anything big) = Fusion. Unknown modded robots go by toughness.
        /// </summary>
        public static ExtraKind RareDroneKind(string prefabName, float weight)
        {
            if (!DuelConfig.RareDronesExtra.Value) return weight >= DuelConfig.ExtraDeckDroneWeight.Value ? ExtraKind.Fusion : ExtraKind.None;
            string n = prefabName ?? "";
            if (n.StartsWith("Drone1") || n.StartsWith("Drone2") || n.StartsWith("Turret1")) return ExtraKind.None;
            if (n.Contains("MegaDrone")) return ExtraKind.Fusion;
            if (n.Contains("FlameDrone")) return ExtraKind.Synchro;
            if (n.Contains("MissileDrone")) return ExtraKind.Xyz;
            if (n.Contains("EmergencyDrone") || n.Contains("EquipmentDrone")) return ExtraKind.Link;
            if (weight >= DuelConfig.ExtraDeckDroneWeight.Value) return ExtraKind.Fusion;
            if (weight >= 1.3f) return ExtraKind.Synchro;
            return ExtraKind.None;
        }

        public static float TierWeight(ItemTier tier)
        {
            switch (tier)
            {
                case ItemTier.Tier1: return 1f;
                case ItemTier.Tier2: return 1.6f;
                case ItemTier.Tier3: return 3f;
                case ItemTier.Boss: return 2.5f;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Lives on the Duelist's CharacterMaster (survives stages). Server side, host only for now.
    /// Records every item and equipment you pick up as a card (they stay in your inventory and keep
    /// working, unless KeepItems is off), and buys drones off the field as monster cards.
    /// </summary>
    public class DuelistDeck : MonoBehaviour
    {
        public readonly Dictionary<ItemIndex, int> ItemCards = new Dictionary<ItemIndex, int>();
        public readonly Dictionary<EquipmentIndex, int> EquipCards = new Dictionary<EquipmentIndex, int>();
        public readonly Dictionary<BodyIndex, int> DroneCards = new Dictionary<BodyIndex, int>();

        private CharacterMaster _master;
        private Inventory _inventory;
        private bool _dirty = true;
        private bool _warnedClient, _warnedFull;
        private bool _syncDirty, _requested;

        /// <summary>Client side: the host sent this Duelist's deck.</summary>
        public void ApplySync(List<(int, int)> items, List<(int, int)> equips, List<(int, int)> drones)
        {
            ItemCards.Clear();
            EquipCards.Clear();
            DroneCards.Clear();
            foreach ((int k, int v) in items) ItemCards[(ItemIndex)k] = v;
            foreach ((int k, int v) in equips) EquipCards[(EquipmentIndex)k] = v;
            foreach ((int k, int v) in drones) DroneCards[(BodyIndex)k] = v;
        }
        private float _droneScan;

        /// <summary>Cards in the main deck (Extra Deck robots don't count, like in Yu-Gi-Oh).</summary>
        public int MainDeckSize
        {
            get
            {
                int n = Card.StarterCount + Card.PartnerCount;
                foreach (int v in ItemCards.Values) n += v;
                foreach (int v in EquipCards.Values) n += v;
                foreach (KeyValuePair<BodyIndex, int> kv in DroneCards) if (!Card.IsExtraDrone(kv.Key)) n += kv.Value;
                return n;
            }
        }

        private bool DeckFull => MainDeckSize >= DuelConfig.MaxDeckSize.Value;
        private string DeckTag => $"(deck {MainDeckSize}/{DuelConfig.MaxDeckSize.Value})";

        private void Start()
        {
            _master = GetComponent<CharacterMaster>();
            _inventory = _master ? _master.inventory : GetComponent<Inventory>();
            if (_inventory) _inventory.onInventoryChanged += OnInventoryChanged;
        }

        private void OnDestroy()
        {
            if (_inventory) _inventory.onInventoryChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged() => _dirty = true;

        private bool Active(out CharacterBody body)
        {
            body = _master ? _master.GetBody() : null;
            return NetworkServer.active && body && body.GetComponent<DuelistMarker>();   // the host converts for every Duelist
        }

        private void FixedUpdate()
        {
            if (!_inventory || !_master) return;
            if (NetworkServer.active && _syncDirty) { _syncDirty = false; DuelNet.SyncDeck(this); }
            if (!NetworkServer.active && !_requested && DuelNet.Id(this) != UnityEngine.Networking.NetworkInstanceId.Invalid)
            {
                _requested = true;
                DuelNet.RequestDeck(_master);
            }

            if (_dirty)
            {
                if (_master.GetBody()) // stay dirty until the body exists again
                {
                    _dirty = false;
                    if (Active(out _)) { ConvertItems(); ConvertEquipment(); }
                }
            }

            _droneScan -= Time.fixedDeltaTime;
            if (_droneScan <= 0f)
            {
                _droneScan = 0.5f;
                if (DuelConfig.DronesBecomeCards.Value && Active(out CharacterBody body)) ConvertDrones(body);
            }
        }

        private void Say(string msg)
        {
            DuelNet.Tell(_master ? _master.GetBody() : null, msg);   // shown to whoever owns this Duelist
        }

        private void WarnFull(string what)
        {
            if (_warnedFull) return;
            _warnedFull = true;
            Say($"<color=#ff9a7a>Deck full ({DuelConfig.MaxDeckSize.Value}).</color> {what} and anything else you pick up now works normally.");
        }

        private readonly Dictionary<ItemIndex, int> _itemSeen = new Dictionary<ItemIndex, int>();
        private EquipmentIndex _lastEquip = EquipmentIndex.None;

        /// <summary>
        /// Every new copy of an item you pick up also becomes a card. With KeepItems on (default) the item
        /// stays in your inventory and works as usual; off, it's taken out like before.
        /// </summary>
        private void ConvertItems()
        {
            bool keep = DuelConfig.KeepItems.Value;
            List<ItemIndex> order = new List<ItemIndex>(_inventory.itemAcquisitionOrder);
            foreach (ItemIndex index in order)
            {
                ItemDef def = ItemCatalog.GetItemDef(index);
                if (!def || def.hidden || def.tier == ItemTier.NoTier || (!keep && !def.canRemove)) continue;

                int count = InventoryCompat.Count(_inventory, index);
                _itemSeen.TryGetValue(index, out int seen);
                int fresh = keep ? count - seen : count;
                _itemSeen[index] = count;
                if (fresh <= 0) continue;

                int room = DuelConfig.MaxDeckSize.Value - MainDeckSize;
                if (room <= 0) { WarnFull(Language.GetString(def.nameToken)); continue; }
                int take = Mathf.Min(fresh, room);
                if (!keep && !InventoryCompat.Remove(_inventory, index, take)) continue;

                ItemCards.TryGetValue(index, out int existing);
                ItemCards[index] = existing + take;
                _syncDirty = true;

                Card preview = Card.FromItem(index);
                string name = preview != null ? preview.Name : def.name;
                string kind = preview != null ? preview.Type.ToString().ToLower() : "card";
                Say($"<color=#f2c640>{name}</color> → +{(take > 1 ? take + " " : "")}{kind} card{(take > 1 ? "s" : "")} {DeckTag}");
                if (take < fresh) WarnFull(name);
            }
            // items that left the inventory (printers, cauldrons, cleansing) don't take cards away
            List<ItemIndex> known = new List<ItemIndex>(_itemSeen.Keys);
            foreach (ItemIndex k in known)
                if (!order.Contains(k)) _itemSeen[k] = 0;
        }

        private void ConvertEquipment()
        {
            bool keep = DuelConfig.KeepItems.Value;
            EquipmentIndex eq = InventoryCompat.CurrentEquipment(_inventory);
            if (keep && eq == _lastEquip) return;          // nothing new in the slot
            _lastEquip = eq;
            if (eq == EquipmentIndex.None) return;
            EquipmentDef def = EquipmentCatalog.GetEquipmentDef(eq);
            if (!def || !def.canDrop) return; // elite aspects etc. stay
            if (DeckFull) { WarnFull(Language.GetString(def.nameToken)); return; }
            if (!keep && !InventoryCompat.ClearEquipment(_inventory)) return;

            EquipCards.TryGetValue(eq, out int existing);
            EquipCards[eq] = existing + 1;
            _syncDirty = true;
            Card preview = Card.FromEquipment(eq);
            Say($"<color=#f2c640>{(preview != null ? preview.Name : def.name)}</color> → +trap card {DeckTag}." + (keep ? "" : " Your equipment slot is free again."));
        }

        /// <summary>Drones and turrets (including Gunner Turrets, which aren't flagged mechanical).</summary>
        private static bool IsRobot(CharacterBody b)
        {
            if ((b.bodyFlags & CharacterBody.BodyFlags.Mechanical) != 0) return true;
            string n = b.name;
            return n.Contains("Turret") || n.Contains("Drone");
        }

        /// <summary>Robots and turrets you buy or repair leave the field and join your deck as monsters.</summary>
        private void ConvertDrones(CharacterBody owner)
        {
            List<CharacterMaster> grab = new List<CharacterMaster>();
            foreach (CharacterMaster m in CharacterMaster.readOnlyInstancesList)
            {
                if (!m || m == _master) continue;
                MinionOwnership mo = m.minionOwnership;
                if (!mo || mo.ownerMaster != _master) continue;
                CharacterBody b = m.GetBody();
                if (!b || !b.healthComponent || !b.healthComponent.alive) continue;
                if (!IsRobot(b)) continue;
                if (b.GetComponent<PartnerTag>()) continue; // partners are never turned into cards
                if (DuelDimensionState.ActiveDuelists.Contains(owner)) return; // never mid-duel
                grab.Add(m);
            }

            foreach (CharacterMaster m in grab)
            {
                CharacterBody b = m.GetBody();
                bool extra = Card.IsExtraDrone(b.bodyIndex);
                if (!extra && DeckFull) { WarnFull(b.GetDisplayName()); continue; }

                DroneCards.TryGetValue(b.bodyIndex, out int existing);
                DroneCards[b.bodyIndex] = existing + 1;
                _syncDirty = true;
                Card preview = Card.FromDrone(b.bodyIndex);
                string name = preview != null ? preview.Name : b.GetDisplayName();
                string kind = extra && preview != null ? $"<color=#c77dff>RARE · Extra Deck {preview.Extra.ToString().ToUpper()}</color>" : "monster card";
                Say($"<color=#f2c640>{name}</color> → {kind} {DeckTag}");

                try
                {
                    GameObject bodyObj = b.gameObject;
                    GameObject masterObj = m.gameObject;
                    NetworkServer.Destroy(bodyObj);
                    NetworkServer.Destroy(masterObj);
                }
                catch (Exception e) { DuelistPlugin.Log.LogWarning("Couldn't remove drone: " + e.Message); }
            }
        }

        /// <summary>Main deck (one card per copy) and Extra Deck (the biggest robots) for one duel.</summary>
        public void BuildDecks(out List<Card> main, out List<Card> extra)
        {
            main = Card.StarterDeck();
            main.AddRange(Card.PartnerCards());
            extra = DuelConfig.ExtraToolkit.Value ? Card.ExtraToolkit() : new List<Card>();
            foreach (KeyValuePair<ItemIndex, int> kv in ItemCards)
                for (int i = 0; i < kv.Value; i++) { Card c = Card.FromItem(kv.Key); if (c != null) main.Add(c); }
            foreach (KeyValuePair<EquipmentIndex, int> kv in EquipCards)
                for (int i = 0; i < kv.Value; i++) { Card c = Card.FromEquipment(kv.Key); if (c != null) main.Add(c); }
            foreach (KeyValuePair<BodyIndex, int> kv in DroneCards)
                for (int i = 0; i < kv.Value; i++)
                {
                    Card c = Card.FromDrone(kv.Key);
                    if (c == null) continue;
                    if (c.Type == CardType.Extra) extra.Add(c); else main.Add(c);
                }
        }
    }

    /// <summary>
    /// Game 1.5 reworked inventories; resolve the item count/remove methods by name so the mod
    /// works whether the "Permanent" variants exist or not.
    /// </summary>
    public static class InventoryCompat
    {
        private static MethodInfo _count;
        private static MethodInfo _remove;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            Type[] countSig = { typeof(ItemIndex) };
            Type[] removeSig = { typeof(ItemIndex), typeof(int) };

            _count = Find("GetItemCountPermanent", countSig) ?? Find("GetItemCount", countSig);
            _remove = Find("RemoveItemPermanent", removeSig) ?? Find("RemoveItem", removeSig);

            DuelistPlugin.Log.LogInfo($"Inventory API: count={Describe(_count)}, remove={Describe(_remove)}");
        }

        private static MethodInfo Find(string name, Type[] leading)
        {
            foreach (MethodInfo m in typeof(Inventory).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m.Name != name) continue;
                ParameterInfo[] ps = m.GetParameters();
                if (ps.Length < leading.Length) continue;
                bool ok = true;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (i < leading.Length) { if (ps[i].ParameterType != leading[i]) { ok = false; break; } }
                    else if (!ps[i].HasDefaultValue) { ok = false; break; }
                }
                if (ok) return m;
            }
            return null;
        }

        private static string Describe(MethodInfo m) => m == null ? "MISSING" : m.Name + "(" + m.GetParameters().Length + " args)";

        private static object[] Args(MethodInfo m, params object[] leading)
        {
            ParameterInfo[] ps = m.GetParameters();
            object[] args = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
                args[i] = i < leading.Length ? leading[i] : ps[i].DefaultValue;
            return args;
        }

        public static int Count(Inventory inv, ItemIndex index)
        {
            Resolve();
            if (_count == null) return 0;
            try { return Convert.ToInt32(_count.Invoke(inv, Args(_count, index))); }
            catch { return 0; }
        }

        public static bool Remove(Inventory inv, ItemIndex index, int amount)
        {
            Resolve();
            if (_remove == null) return false;
            try { _remove.Invoke(inv, Args(_remove, index, amount)); return true; }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("RemoveItem failed: " + e.Message);
                return false;
            }
        }
        // ---- equipment (one active slot; read and cleared by name so 1.5 signature changes don't break the build)
        private static PropertyInfo _curEquip;
        private static MethodInfo _setEquip;
        private static bool _equipResolved;

        private static void ResolveEquip()
        {
            if (_equipResolved) return;
            _equipResolved = true;
            _curEquip = typeof(Inventory).GetProperty("currentEquipmentIndex", BindingFlags.Instance | BindingFlags.Public);
            _setEquip = Find("SetEquipmentIndex", new[] { typeof(EquipmentIndex) });
            DuelistPlugin.Log.LogInfo($"Equipment API: current={(_curEquip != null ? "ok" : "MISSING")}, set={Describe(_setEquip)}");
        }

        public static EquipmentIndex CurrentEquipment(Inventory inv)
        {
            ResolveEquip();
            if (_curEquip == null || !inv) return EquipmentIndex.None;
            try { return (EquipmentIndex)_curEquip.GetValue(inv); }
            catch { return EquipmentIndex.None; }
        }

        public static bool ClearEquipment(Inventory inv)
        {
            ResolveEquip();
            if (_setEquip == null || !inv) return false;
            try { _setEquip.Invoke(inv, Args(_setEquip, EquipmentIndex.None)); return true; }
            catch (Exception e) { DuelistPlugin.Log.LogWarning("Clearing equipment failed: " + e.Message); return false; }
        }
    }
}
