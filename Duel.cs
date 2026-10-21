using System;
using System.Collections.Generic;
using System.Reflection;
using RoR2;
using RoR2.CharacterAI;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

namespace DuelistMod
{
    /// <summary>Risk of Rain 2's elite types.</summary>
    public enum Rarity { Common, Rare, SuperRare, UltraRare, SecretRare }

    public enum DuelElite { None, Blazing, Glacial, Overloading, Malachite, Celestine, Mending, Voidtouched, Perfected, Gilded, Twisted, Other }

    public class DuelMember
    {
        public CharacterBody Body;
        public HealthComponent Health;
        public float Weight; // damage multiplier (always 1 now)

        public bool Alive => Health && Health.alive;
    }

    /// <summary>One enemy monster card = one real monster on the map, with ATK and DEF on the card game's scale.</summary>
    public class Representative
    {
        public BodyIndex Index;
        public string Name;
        public Texture Icon;
        public Color Color;
        public bool IsBoss;
        public readonly List<DuelMember> Members = new List<DuelMember>();

        public int AliveCount;
        public float TotalHealth;
        public float FullHealth;
        public float StartHealth;
        public int Actions;

        public float LastLoss;
        public float LastHitTime = -10f;
        public float LungeTime = -10f;   // holo-chess: lunges across the board when it attacks
        public Vector3 LungeOffset = new Vector3(0f, 0f, -3.2f);
        // Risk of Rain 2's own elite system: the elite type this monster has on the map
        public string EliteName;          // "Blazing", "Glacial"… as the game names it (null if not elite)
        public DuelElite Elite = DuelElite.None;
        public Color EliteColor = Color.white;
        public Texture EliteIcon;         // its aspect's icon
        public float Distance;          // from you when it was added: nearest monsters are drawn first

        // ---- the card game's numbers
        public int BaseAtk, BaseDef;    // from its real damage and health (see DuelStats)
        public int AtkMod, DefMod;      // spell effects until the End Phase
        public bool DefensePos;         // enemy monsters can sit in defense position too
        public bool Destroyed;          // destroyed in the duel (by battle or a card), even if the real one somehow lives

        public GameObject Hologram;     // the stand (or the commander object)
        public Transform ModelRoot;     // the live clone's holder, shaken when hit
        public Vector3 ModelBase;
        public Vector3 BoardPosition;

        public int ZoneIndex = -1;      // enemy monster zone; -1 = waiting in the enemy deck (or gone)
        public bool IsCommander;
        public bool OnField => ZoneIndex >= 0 || IsCommander;

        public bool Defeated => Destroyed || AliveCount <= 0;
        public float DefenseFactor => 1f;
        public string Label => Name;

        public int Atk => Mathf.Max(0, (IsCommander ? DuelStats.Round50(BaseAtk * DuelConfig.BossAttackMult.Value) : BaseAtk) + AtkMod);
        public int Def => Mathf.Max(0, BaseDef + DefMod);

        /// <summary>Card-game Level from its strength (a Beetle is Level 4), bosses higher.</summary>
        public int Level => Mathf.Clamp(2 + Mathf.Max(BaseAtk, BaseDef) / 500 + (IsBoss ? 2 : 0), 1, 12);

        /// <summary>How rare the monster is: elites are Rare, tougher elites Super Rare, bosses Ultra Rare, elite bosses Secret Rare.</summary>
        public Rarity Rarity
        {
            get
            {
                bool strongElite = Elite == DuelElite.Malachite || Elite == DuelElite.Celestine || Elite == DuelElite.Voidtouched
                                || Elite == DuelElite.Perfected || Elite == DuelElite.Twisted;
                if (IsBoss) return Elite != DuelElite.None ? Rarity.SecretRare : Rarity.UltraRare;
                if (strongElite) return Rarity.SuperRare;
                if (Elite != DuelElite.None) return Rarity.Rare;
                return Rarity.Common;
            }
        }

        public void Refresh()
        {
            int n = 0;
            float hp = 0f, full = 0f;
            foreach (DuelMember m in Members)
            {
                if (!m.Alive) continue;
                n++;
                hp += m.Health.combinedHealth;
                full += m.Health.fullCombinedHealth;
            }
            AliveCount = n;
            TotalHealth = hp;
            if (full > 0f) FullHealth = full;
            if (hp > StartHealth) StartHealth = hp;
            Actions = n <= 0 ? 0 : IsCommander ? Mathf.Max(1, DuelConfig.BossActions.Value) : 1;
        }

        /// <summary>ATK from its real damage, DEF from its real health, both against yours.</summary>
        public void ComputeStats()
        {
            float dmg = 0f, hp = 0f;
            foreach (DuelMember m in Members)
            {
                if (!m.Alive) continue;
                if (m.Body) dmg = Mathf.Max(dmg, m.Body.damage);
                hp = Mathf.Max(hp, m.Health.fullCombinedHealth);
            }
            BaseAtk = DuelStats.EnemyAtk(dmg * DuelConfig.EnemyUnitAttack.Value);
            BaseDef = DuelStats.EnemyDef(hp);
        }

        public DuelMember RandomAliveMember()
        {
            List<DuelMember> alive = Members.FindAll(m => m.Alive);
            return alive.Count == 0 ? null : alive[UnityEngine.Random.Range(0, alive.Count)];
        }
    }

    /// <summary>
    /// A monster on your field: either a card you summoned, or a group of real allies (drones,
    /// turrets…) pulled into the duel, whose ATK and DEF come from the allies themselves.
    /// </summary>
    public class FieldMonster
    {
        public Card Card;
        public int Slot;
        public float Hp;                // allies only: their real combined health
        public float MaxHp;
        public bool HasAttacked;
        public GameObject CardObject;
        public GameObject Statue;
        public float LastHitTime = -10f;
        public float LungeTime = -10f;
        public Vector3 LungeOffset = new Vector3(0f, 0f, 3.2f);
        public List<CharacterBody> Allies;

        public bool Defense;            // defense position: can't attack; battles use its DEF
        public bool FaceDown;           // set: hidden in defense until flipped or attacked
        public bool SummonedThisTurn;
        public bool PositionChanged;
        public bool Revived;            // came back from the graveyard: banished if it leaves again
        public List<Card> Materials;    // XYZ materials attached under it
        public bool AllThisTurn;        // XYZ detach: this turn it attacks every enemy monster
        public float AtkBonus;          // Hollow Idol: +25% per enemy it destroyed
        public int TempAtk;             // effects until the End Phase
        public int FrozenRound = -1;    // Glacial: can't attack during this round

        public bool IsAlly => Allies != null;

        public int AllyAlive
        {
            get
            {
                int n = 0;
                if (Allies != null) foreach (CharacterBody b in Allies) if (b && b.healthComponent && b.healthComponent.alive) n++;
                return n;
            }
        }

        public bool Dead => IsAlly && AllyAlive == 0;

        public int AllyAtkPts()
        {
            float sum = 0f;
            if (Allies != null) foreach (CharacterBody b in Allies) if (b && b.healthComponent && b.healthComponent.alive) sum += b.damage;
            return DuelStats.AllyAtk(sum);
        }

        public int AllyDefPts()
        {
            float sum = 0f;
            if (Allies != null) foreach (CharacterBody b in Allies) if (b && b.healthComponent && b.healthComponent.alive) sum += b.healthComponent.fullCombinedHealth;
            return DuelStats.AllyDef(sum);
        }

        public string Title => IsAlly ? $"{Card.Name} x{AllyAlive}" : Card.Name;

        public void RefreshAlly()
        {
            if (!IsAlly) return;
            float hp = 0f, max = 0f;
            foreach (CharacterBody b in Allies)
            {
                if (!b || !b.healthComponent || !b.healthComponent.alive) continue;
                hp += b.healthComponent.combinedHealth;
                max += b.healthComponent.fullCombinedHealth;
            }
            Hp = hp;
            MaxHp = Mathf.Max(max, 1f);
        }
    }

    /// <summary>One face-up enemy card on their spell/trap row: an elite aspect present on their field.</summary>
    public class EnemyAura
    {
        public string Name;
        public int Count;
        public Color Color;
        public Texture Art;
        public DuelElite Elite;
        public int Slot;
    }

    public class SetCard
    {
        public Card Card;
        public int Charges;
        public int SetTurn;             // can't be activated during the turn it was set
    }

    /// <summary>One link of a chain: a card or effect waiting to resolve.</summary>
    public class ChainLink
    {
        public int Number;
        public string Name;
        public string Text;
        public int Speed;
        public bool Mine;
        public Color Color = Color.white;
        public Action Resolve;
        public bool Negated;
        public GameObject Visual;       // the card standing up on the field while it waits
        public string AuraName;         // enemy elite card that made it
    }

    /// <summary>An attack being declared: who attacks what.</summary>
    public class BattleInfo
    {
        public bool Mine;               // your monster attacks
        public FieldMonster Attacker;
        public Representative Rep;      // enemy attacker, or your target
        public List<Representative> Targets;
        public int Slot = -1;           // enemy attack: your monster zone it targets (-1 = you directly)
        public bool Direct;
        public bool Negated;
        public bool Crit;
    }

    /// <summary>Result of damage dealt to a monster's real body, used for kills and gold.</summary>
    public struct HitResult
    {
        public float Total;
        public int Hits;
        public int Kills;
        public float MaxEach;
        public float MinEach;
    }

    public class Popup
    {
        public string Text;
        public string Sub;
        public Color Color;
        public float Born;
        public bool OnScreen;   // true: centre of the screen; false: pinned to World
        public Vector3 World;
        public bool Big;
    }

    public class DuelSession
    {
        /// <summary>The card game's turn structure, for you and for the enemy.</summary>
        public enum Phase { Draw, Standby, Main1, Battle, Main2, End, EnemyDraw, EnemyStandby, EnemyMain, EnemyBattle, EnemyEnd, Finished }
        public const int Zones = 5;

        public readonly CharacterBody Player;
        public readonly List<Representative> Reps = new List<Representative>();
        public readonly FieldMonster[] Monsters = new FieldMonster[Zones];
        public readonly SetCard[] Backrow = new SetCard[Zones];
        public readonly List<Card> DrawPile = new List<Card>();
        public readonly List<Card> Hand = new List<Card>();
        public readonly List<Card> Graveyard = new List<Card>();
        public readonly List<Card> ExtraDeck = new List<Card>();
        public readonly List<Card> Banished = new List<Card>();
        public readonly List<string> Log = new List<string>();
        public readonly List<Popup> Popups = new List<Popup>();
        public readonly DuelCamera Camera = new DuelCamera();
        public DuelBoard Board;

        public Phase CurrentPhase = Phase.Draw;
        public float PhaseChangedAt = -10f;
        public int Round = 1;
        public int SummonsLeft;
        public int SelectedRep;
        public string Banner = "";
        public float BannerTime = -10f;

        public bool Finished => CurrentPhase == Phase.Finished;

        // ---- outcome and rewards
        public enum Outcome { None, Victory, Defeat }
        public Outcome Result = Outcome.None;
        public string ResultReason = "";
        public int KillCount;
        public uint BaseGold;      // the normal gold those kills are worth
        public uint BonusGold;     // duel bonus paid on top, as kills happen
        public uint VictoryGold;   // paid at the end if you win
        public uint JackpotGold;   // the victory jackpot (chests' worth of gold)
        public uint BountyGold;    // paid per enemy monster defeated
        public int BountyWhite, BountyGreen, BountyRed;   // items owed, dropped when the duel ends
        public int LootLegendary, LootUncommon;
        public bool BossBeaten;
        public int DeckTotal;      // main deck size this duel

        public bool IsPlayerPhase => CurrentPhase <= Phase.End;
        public bool IsEnemyPhase => CurrentPhase >= Phase.EnemyDraw && CurrentPhase <= Phase.EnemyEnd;
        /// <summary>You can act: it's your turn, nothing is resolving and the duel isn't over.</summary>
        public bool IsPlayerTurn => IsPlayerPhase && _finishTimer < 0f && !ChainBusy;
        public bool IsMainPhase => IsPlayerTurn && (CurrentPhase == Phase.Main1 || CurrentPhase == Phase.Main2);
        public bool IsBattlePhase => IsPlayerTurn && CurrentPhase == Phase.Battle;

        public int TributableCount { get { int n = 0; foreach (FieldMonster m in Monsters) if (m != null && !m.IsAlly) n++; return n; } }

        // ---- the enemy's side of the field
        public Representative Commander;
        public readonly List<EnemyAura> EnemyAuras = new List<EnemyAura>();
        private readonly HashSet<CharacterBody> _known = new HashSet<CharacterBody>();
        private readonly Dictionary<string, int> _auraUsedRound = new Dictionary<string, int>();
        private readonly HashSet<string> _auraDestroyed = new HashSet<string>();

        /// <summary>Units in species that haven't reached a monster zone yet.</summary>
        public int EnemyDeckCount { get { int n = 0; foreach (Representative r in Reps) if (!r.OnField && !r.Defeated) n += r.AliveCount; return n; } }
        /// <summary>Every enemy monster destroyed since the duel began.</summary>
        public int EnemyGraveCount { get { int n = 0; foreach (Representative r in Reps) if (r.Defeated && !r.IsCommander) n++; return n; } }
        /// <summary>The teleporter boss that hasn't been summoned yet.</summary>
        public int EnemyExtraCount
        {
            get
            {
                try { return Commander == null && TeleporterInteraction.instance && !TeleporterInteraction.instance.isCharged ? 1 : 0; }
                catch { return 0; }
            }
        }
        public bool EnemyMonstersOnField { get { foreach (Representative r in Reps) if (r.ZoneIndex >= 0 && !r.Defeated) return true; return false; } }

        /// <summary>The Director's life points (only when there's no commander).</summary>
        public float DirectorLife, DirectorLifeMax;
        public bool DirectorExposed => Commander == null && DirectorLife > 0f && !EnemyMonstersOnField && Result == Outcome.None;

        /// <summary>Monsters on enemy zones can be attacked (Celestine hides behind the others).</summary>
        public bool Targetable(Representative r) => r != null && !r.Defeated && r.ZoneIndex >= 0 && !Cloaked(r);

        /// <summary>Your Life Points: 8000 = full health.</summary>
        public int PlayerLP => Player && Player.healthComponent ? DuelStats.LifePoints(Player.healthComponent.combinedHealth, Player.healthComponent.fullCombinedHealth) : 0;
        /// <summary>The enemy's Life Points: the commander's health on the same scale, or the Director's.</summary>
        public int EnemyLP => Commander != null ? (Commander.Defeated ? 0 : DuelStats.LifePoints(Commander.TotalHealth, Commander.FullHealth)) : Mathf.CeilToInt(DirectorLife);

        private Vector3 _returnPosition;
        private Vector3 _arenaSpot;
        private Vector3 _forward;

        private class AllyHome
        {
            public CharacterBody Body;
            public Vector3 Position;
            public BaseAI[] AI;
            public Vector3 Offset;
            public FieldMonster Group;
        }
        private readonly List<AllyHome> _allies = new List<AllyHome>();

        private readonly Queue<Representative> _enemyQueue = new Queue<Representative>();
        private float _enemyTimer;
        private float _phaseTimer;
        private float _finishTimer = -1f;
        private int _turnId;

        private static GameObject _hitVfx;
        private static GameObject _bigVfx;

        private static readonly Color AllyFrame = new Color(0.25f, 0.55f, 0.85f);
        private static readonly Color SpellColor = new Color(0.25f, 0.85f, 0.75f);
        private static readonly Color TrapColor = new Color(0.85f, 0.45f, 0.95f);

        public DuelSession(CharacterBody player)
        {
            Player = player;
        }

        public static string PhaseName(Phase p)
        {
            switch (p)
            {
                case Phase.Draw: return "DRAW PHASE";
                case Phase.Standby: return "STANDBY PHASE";
                case Phase.Main1: return "MAIN PHASE 1";
                case Phase.Battle: return "BATTLE PHASE";
                case Phase.Main2: return "MAIN PHASE 2";
                case Phase.End: return "END PHASE";
                case Phase.EnemyDraw: return "ENEMY DRAW PHASE";
                case Phase.EnemyStandby: return "ENEMY STANDBY PHASE";
                case Phase.EnemyMain: return "ENEMY MAIN PHASE";
                case Phase.EnemyBattle: return "ENEMY BATTLE PHASE";
                case Phase.EnemyEnd: return "ENEMY END PHASE";
            }
            return "";
        }

        // ------------------------------------------------------------ setup

        public bool Begin()
        {
            if (!Player || !Player.healthComponent) return false;

            SkillAssets.Load();
            if (!_hitVfx) _hitVfx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");
            if (!_bigVfx) _bigVfx = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFX");
            DuelStats.Calibrate(Player);

            _returnPosition = Player.footPosition;
            Vector3 aim = Player.inputBank ? Player.inputBank.aimDirection : Player.transform.forward;
            aim.y = 0f;
            _forward = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector3.forward;

            DuelistPlugin.Log.LogInfo("Duel setup: gathering enemies…");
            GatherRepresentatives();
            if (Reps.Count == 0)
            {
                try { Chat.AddMessage("<color=#f2c640>No enemies to duel.</color>"); } catch { }
                return false;
            }

            _arenaSpot = _returnPosition + Vector3.up * DuelConfig.ArenaHeight.Value;
            DuelistPlugin.Log.LogInfo($"Duel setup: {Reps.Count} monsters, building board…");
            Board = DuelBoard.Build(_arenaSpot, _forward);
            DuelistPlugin.Log.LogInfo("Duel setup: board built.");
            Step("enemy field", SetupEnemyField);

            if (_bigVfx) EffectManager.SpawnEffect(_bigVfx, new EffectData { origin = Player.corePosition, scale = 6f }, true);
            Teleport(Player, _arenaSpot);
            if (Player.characterDirection) Player.characterDirection.forward = _forward;

            Step("tabletop camera", () => { Camera.Aim(Board.transform); Camera.Attach(Player); });
            Step("allies", GatherAllies);

            DuelistPlugin.Log.LogInfo("Duel setup: building decks…");
            List<Card> main, extra;
            DuelistDeck deckComp = Player.master ? Player.master.GetComponent<DuelistDeck>() : null;
            if (deckComp) deckComp.BuildDecks(out main, out extra);
            else { main = Card.StarterDeck(); extra = DuelConfig.ExtraToolkit.Value ? Card.ExtraToolkit() : new List<Card>(); }

            DeckTotal = main.Count;
            Shuffle(main);
            DrawPile.AddRange(main);
            ExtraDeck.AddRange(extra);
            Draw(DuelConfig.StartingHand.Value, false);

            int units = 0;
            foreach (Representative r in Reps) units += r.AliveCount;
            AddLog($"DUEL! {units} enemy monster(s). Both duelists start at {DuelStats.StartLP} LP." + (_allies.Count > 0 ? $" {_allies.Count} allies join you." : ""));
            ShowBanner("DUEL!");
            Util.PlaySound("Play_UI_teleporter_activate", Player.gameObject);
            EnterPhase(Phase.Draw);
            return true;
        }

        /// <summary>Runs an optional setup step; a failure is logged and the duel goes on without it.</summary>
        private void Step(string name, Action action)
        {
            try { action(); DuelistPlugin.Log.LogInfo($"Duel setup: {name} ok."); }
            catch (Exception e) { DuelistPlugin.Log.LogError($"Duel setup step '{name}' failed (continuing without it):\n{e}"); }
        }

        private IEnumerable<CharacterBody> EnemyBodies()
        {
            TeamIndex myTeam = Player.teamComponent ? Player.teamComponent.teamIndex : TeamIndex.Player;
            TeamMask enemies = TeamMask.GetEnemyTeams(myTeam);
            List<CharacterBody> list = new List<CharacterBody>();
            for (TeamIndex t = 0; t < TeamIndex.Count; t++)
            {
                if (!enemies.HasTeam(t)) continue;
                foreach (TeamComponent tc in TeamComponent.GetTeamMembers(t))
                {
                    CharacterBody b = tc ? tc.body : null;
                    if (b && b.healthComponent && b.healthComponent.alive) list.Add(b);
                }
            }
            return list;
        }

        /// <summary>Adds a monster as its own card. Null if already known.</summary>
        private Representative AddEnemyBody(CharacterBody b)
        {
            if (!b || _known.Contains(b)) return null;
            _known.Add(b);

            Representative rep = new Representative
            {
                Index = b.bodyIndex,
                Name = b.GetDisplayName(), // includes the elite prefix, e.g. "Blazing Beetle"
                Icon = b.portraitIcon,
                IsBoss = b.isChampion,
                Color = Color.HSVToRGB(((int)b.bodyIndex * 0.618034f) % 1f, 0.55f, 0.9f),
                Distance = Vector3.Distance(_returnPosition, b.corePosition)
            };
            if (string.IsNullOrEmpty(rep.Name)) rep.Name = SafeName(b);
            if (TryElite(b, out string elite, out Color ec, out Texture eIcon))
            {
                rep.EliteName = elite;
                rep.EliteColor = ec;
                rep.EliteIcon = eIcon;
                rep.Elite = EliteOf(b, elite);
            }
            rep.Members.Add(new DuelMember { Body = b, Health = b.healthComponent, Weight = 1f });
            rep.Refresh();
            rep.ComputeStats();
            Reps.Add(rep);
            return rep;
        }

        private void GatherRepresentatives()
        {
            foreach (CharacterBody b in EnemyBodies()) AddEnemyBody(b);
            foreach (Representative r in Reps) r.Refresh();
            Reps.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }

        // ------------------------------------------------------------ the enemy's field

        private void SetupEnemyField()
        {
            Representative boss = null;
            foreach (Representative r in Reps)
                if (r.IsBoss && (boss == null || r.TotalHealth > boss.TotalHealth)) boss = r;
            if (boss == null && Reps.Count >= 1)
            {
                // no boss: the most powerful monster on the map takes command (toughest single unit, then hardest hitter)
                foreach (Representative r in Reps)
                    if (boss == null || Power(r) > Power(boss)) boss = r;
            }
            SetCommander(boss, false);
            if (Commander == null) DirectorLifeMax = DirectorLife = DuelStats.StartLP;
            FillEnemyZones(false);
            RefreshAuras();

            // explain the elite types on the field once
            HashSet<DuelElite> seen = new HashSet<DuelElite>();
            foreach (Representative r in Reps)
                if (r.EliteName != null && seen.Add(r.Elite) && r.Elite != DuelElite.Other)
                    AddLog($"<color=#{ColorUtility.ToHtmlStringRGB(r.EliteColor)}>{r.EliteName}</color>: {EliteText(r.Elite)}");
        }

        /// <summary>0 → 1 → 0 over 0.6 s: a hop toward the target and back when a piece attacks.</summary>
        private static float Lunge(float t) => t < 0f || t > 0.6f ? 0f : Mathf.Sin(t / 0.6f * Mathf.PI);

        /// <summary>Which of the game's elite types this is (from its EliteDef and its in-game name).</summary>
        private static DuelElite EliteOf(CharacterBody b, string name)
        {
            string key = (name ?? "").ToLowerInvariant();
            try
            {
                EquipmentDef ed = EquipmentCatalog.GetEquipmentDef(InventoryCompat.CurrentEquipment(b.inventory));
                if (ed && ed.passiveBuffDef && ed.passiveBuffDef.eliteDef) key += " " + ed.passiveBuffDef.eliteDef.name.ToLowerInvariant();
            }
            catch { }
            if (key.Contains("blaz") || key.Contains("fire")) return DuelElite.Blazing;
            if (key.Contains("glac") || key.Contains("ice")) return DuelElite.Glacial;
            if (key.Contains("overload") || key.Contains("lightning")) return DuelElite.Overloading;
            if (key.Contains("malach") || key.Contains("poison")) return DuelElite.Malachite;
            if (key.Contains("celest") || key.Contains("haunt")) return DuelElite.Celestine;
            if (key.Contains("perfect") || key.Contains("lunar")) return DuelElite.Perfected;
            if (key.Contains("mend") || key.Contains("earth")) return DuelElite.Mending;
            if (key.Contains("void")) return DuelElite.Voidtouched;
            if (key.Contains("gild") || key.Contains("gold")) return DuelElite.Gilded;
            if (key.Contains("twist") || key.Contains("bead")) return DuelElite.Twisted;
            return DuelElite.Other;
        }

        /// <summary>What an elite monster does in battle (mirrors what it does on the map).</summary>
        public static string EliteText(DuelElite e)
        {
            switch (e)
            {
                case DuelElite.Blazing: return "After it attacks, you burn: 300 damage in your next Standby Phase.";
                case DuelElite.Glacial: return "A monster that battles it and survives is frozen: it can't attack in your next turn.";
                case DuelElite.Overloading: return "When it attacks, its lightning inflicts 400 damage to you.";
                case DuelElite.Malachite: return "When it attacks, its poison stops you gaining LP in your next turn.";
                case DuelElite.Celestine: return "Cloaked: can't be attacked while another enemy monster is on the field.";
                case DuelElite.Mending: return "In the enemy's End Phase, the enemy gains 500 LP.";
                case DuelElite.Voidtouched: return "Piercing: attacking a Defense monster with more ATK than its DEF inflicts the difference to you.";
                case DuelElite.Perfected: return "Lunar: unaffected by your trap cards.";
                case DuelElite.Gilded: return "Battle damage it inflicts also steals your gold.";
                case DuelElite.Twisted: return "When it destroys a monster by battle, you take 300 damage for each monster you control.";
            }
            return "";
        }

        /// <summary>What the elite's face-up card on their spell/trap row can chain once per round.</summary>
        public static string AuraText(DuelElite e)
        {
            switch (e)
            {
                case DuelElite.Blazing: return "FLAME BURST (SS2): when you declare an attack, inflict 500 damage to you.";
                case DuelElite.Glacial: return "ICE PRISON (SS2): when you declare an attack, negate it; the attacker can't attack next turn.";
                case DuelElite.Overloading: return "STATIC SHOCK (SS2): when you declare an attack, the attacker loses 700 ATK until the End Phase.";
                case DuelElite.Malachite: return "TOXIC BLOOM (SS2): when you declare an attack, the attacker loses 500 ATK and you can't gain LP next turn.";
                case DuelElite.Celestine: return "PHASE VEIL (SS2): negate an attack, or the activation of your spell or trap.";
                case DuelElite.Mending: return "REGROWTH (SS2): when you declare an attack, the enemy gains 700 LP.";
                case DuelElite.Voidtouched: return "VOID COLLAPSE (SS2): when you declare an attack, destroy the attacker if its ATK is 2000 or less.";
                case DuelElite.Perfected: return "LUNAR SEAL (Counter, SS3): negate the activation of your spell or trap.";
                case DuelElite.Gilded: return "GOLD TOLL (SS2): when you declare an attack, you lose 10% of your gold.";
                case DuelElite.Twisted: return "TWIST (SS2): when you declare an attack, the attacker's ATK is halved until the End Phase.";
            }
            return "Its power is already in their monsters' ATK and DEF.";
        }

        private bool Cloaked(Representative r)
        {
            if (r == null || r.Elite != DuelElite.Celestine) return false;
            foreach (Representative o in Reps)
                if (o != r && !o.Defeated && o.ZoneIndex >= 0 && o.Elite != DuelElite.Celestine) return true;
            return false;
        }

        private void ElitePop(Representative rep, Vector3 at, string title, string sub)
        {
            Pop(at + Vector3.up * 2.2f, title, sub, rep.EliteColor, false);
        }

        private static float Power(Representative r)
        {
            float best = 0f;
            foreach (DuelMember m in r.Members) if (m.Alive) best = Mathf.Max(best, m.Health.fullCombinedHealth);
            return best * Mathf.Max(1f, r.BaseAtk);
        }

        /// <summary>The boss stands behind their field like you stand behind yours. Their LP is its health.</summary>
        private void SetCommander(Representative boss, bool announce)
        {
            if (Commander != null) return;
            if (boss != null)
            {
                boss.IsCommander = true;
                boss.DefensePos = false;
                if (boss.Hologram) { Board.RemoveStand(boss.Hologram); boss.Hologram = null; }
                boss.ZoneIndex = -1;
                Commander = boss;
                boss.Refresh();
            }
            Board.PlaceCommander(boss, boss != null ? Reps.IndexOf(boss) : -1);
            if (boss != null)
            {
                boss.ModelRoot = boss.Hologram ? boss.Hologram.transform.Find("Model") : null;
                if (boss.ModelRoot) boss.ModelBase = boss.ModelRoot.localPosition;
                if (announce)
                {
                    ShowBanner($"{boss.Name.ToUpper()} TAKES COMMAND!");
                    AddLog($"{boss.Name} arrives and takes command of the enemy side. Its health is now their LP.");
                    if (_bigVfx) EffectManager.SpawnEffect(_bigVfx, new EffectData { origin = Board.CommanderPos + Vector3.up * 2f, scale = 6f }, true);
                    DuelFx.Pillar(Board.transform, Board.CommanderPos, new Color(1f, 0.35f, 0.3f), 14f, 2.4f);
                    DuelSfx.Play(DuelSound.SpecialSummon, Player.gameObject);
                }
            }
        }

        /// <summary>The enemy's strongest face-up attack-position ATK you'd have to beat.</summary>
        private int MyBestAtk()
        {
            int best = 0;
            foreach (FieldMonster m in Monsters)
                if (m != null && !m.FaceDown && !m.Defense) best = Mathf.Max(best, MonsterAtk(m));
            return best;
        }

        /// <summary>Enemy AI: attack position unless your field would beat it and its DEF is the better wall.</summary>
        private bool ChooseDefense(Representative r)
        {
            if (r.IsCommander) return false;
            int mine = MyBestAtk();
            return mine > r.Atk && r.Def > r.Atk;
        }

        /// <summary>Monsters waiting in the enemy deck are summoned onto free enemy monster zones, nearest first.</summary>
        private int FillEnemyZones(bool announce)
        {
            bool[] used = new bool[Zones];
            foreach (Representative r in Reps) if (r.ZoneIndex >= 0) used[r.ZoneIndex] = true;
            List<Representative> waiting = Reps.FindAll(r => !r.OnField && !r.Defeated);
            waiting.Sort((a, b) => a.Distance.CompareTo(b.Distance)); // the closest monsters are drawn first

            int placed = 0;
            foreach (Representative w in waiting)
            {
                int zone = Array.IndexOf(used, false);
                if (zone < 0) break;
                used[zone] = true;
                w.ZoneIndex = zone;
                w.DefensePos = ChooseDefense(w);
                w.Hologram = Board.PlaceRep(w, Reps.IndexOf(w), zone);
                w.ModelRoot = w.Hologram ? w.Hologram.transform.Find("Model") : null;
                if (w.ModelRoot) w.ModelBase = w.ModelRoot.localPosition;
                Board.SetRepPosition(w, false);
                placed++;
                if (announce)
                {
                    DuelFx.RiseUp(w.Hologram, Board.transform, w.EliteName != null ? w.EliteColor : new Color(1f, 0.5f, 0.4f));
                    Pop(w.BoardPosition + Vector3.up * 4f, w.DefensePos ? "SUMMON · DEF" : "SUMMON", $"{w.Label}  {w.Atk}/{w.Def}", new Color(1f, 0.55f, 0.45f), true);
                    AddLog($"The enemy summons {w.Label} (ATK {w.Atk} / DEF {w.Def}) in {(w.DefensePos ? "defense" : "attack")} position.");
                }
            }
            if (announce && placed > 0) DuelSfx.Play(DuelSound.Summon, Player.gameObject);
            return placed;
        }

        /// <summary>Enemy Draw Phase: monsters that spawned on the map since last time join their deck.</summary>
        private void EnemyDrawPhase()
        {
            int fresh = 0;
            foreach (CharacterBody b in EnemyBodies())
                if (AddEnemyBody(b) != null) fresh++;
            foreach (Representative r in Reps) r.Refresh();

            if (Commander == null)
            {
                Representative boss = null;
                foreach (Representative r in Reps)
                    if (r.IsBoss && !r.Defeated && (boss == null || r.TotalHealth > boss.TotalHealth)) boss = r;
                if (boss != null) SetCommander(boss, true);
            }
            // Deck-out for the enemy: nothing left to draw (no monsters waiting, none newly spawned) = they lose.
            if (DuelConfig.DeckOut.Value && fresh == 0 && EnemyDeckCount == 0 && Round >= 2 && Result == Outcome.None)
            {
                AddLog("<color=#80ff90>The enemy has to draw, but its deck is empty. The enemy is DECKED OUT!</color>");
                ShowBanner("ENEMY DECKED OUT!");
                SetResult(Outcome.Victory, "The enemy had no monsters left to draw: decked out!");
                return;
            }
            if (fresh > 0)
            {
                AddLog($"Enemy Draw Phase: {fresh} monster(s) spawned on the map and join their deck.");
                Pop(Board.World(DuelBoard.EnemyDeckLocal) + Vector3.up * 2f, $"+{fresh}", "drawn", new Color(1f, 0.6f, 0.5f), false);
                DuelSfx.Play(DuelSound.Draw, Player.gameObject);
            }
        }

        /// <summary>Enemy Main Phase: summon onto empty zones, then set positions.</summary>
        private void EnemyMainPhase()
        {
            int placed = FillEnemyZones(true);
            foreach (Representative r in Reps)
            {
                if (r.ZoneIndex < 0 || r.Defeated || r.Hologram == null) continue;
                bool def = ChooseDefense(r);
                if (def == r.DefensePos) continue;
                r.DefensePos = def;
                Board.SetRepPosition(r, true);
                AddLog($"{r.Label} switches to {(def ? "defense" : "attack")} position.");
                Pop(r.BoardPosition + Vector3.up * 3f, def ? "DEFENSE" : "ATTACK", r.Label, new Color(0.8f, 0.9f, 1f), false);
                DuelSfx.Play(DuelSound.Position, Player.gameObject);
            }
            RefreshAuras();
            if (placed == 0) AddLog("Enemy Main Phase: no new summons.");
        }

        /// <summary>Elite aspects on their field become face-up cards on the enemy spell/trap row.</summary>
        private void RefreshAuras()
        {
            Dictionary<string, EnemyAura> map = new Dictionary<string, EnemyAura>();
            foreach (Representative r in Reps)
            {
                if (!r.OnField || r.Defeated) continue;
                foreach (DuelMember m in r.Members)
                {
                    if (!m.Alive || !TryElite(m.Body, out string name, out Color color, out Texture art)) continue;
                    if (!map.TryGetValue(name, out EnemyAura a)) map[name] = a = new EnemyAura { Name = name, Color = color, Art = art, Elite = r.Elite };
                    a.Count++;
                }
            }
            EnemyAuras.Clear();
            EnemyAuras.AddRange(map.Values);
            EnemyAuras.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (EnemyAuras.Count > Zones) EnemyAuras.RemoveRange(Zones, EnemyAuras.Count - Zones);
            for (int i = 0; i < Zones; i++)
            {
                bool on = i < EnemyAuras.Count;
                if (on) EnemyAuras[i].Slot = i;
                Board.SetEnemyBack(i, on ? EnemyAuras[i].Art : null, on ? EnemyAuras[i].Color : Color.white, on);
            }
        }

        /// <summary>Whether an enemy elite card can still chain this round.</summary>
        public bool AuraReady(EnemyAura a) => a != null && !_auraDestroyed.Contains(a.Name) && (!_auraUsedRound.TryGetValue(a.Name, out int r) || r != Round);
        public bool AuraDestroyed(EnemyAura a) => a != null && _auraDestroyed.Contains(a.Name);

        private static bool TryElite(CharacterBody b, out string name, out Color color, out Texture art)
        {
            name = null; color = Color.white; art = null;
            try
            {
                if (!b || !b.isElite || !b.inventory) return false;
                EquipmentDef ed = EquipmentCatalog.GetEquipmentDef(InventoryCompat.CurrentEquipment(b.inventory));
                if (!ed || !ed.passiveBuffDef || !ed.passiveBuffDef.eliteDef) return false;
                EliteDef el = ed.passiveBuffDef.eliteDef;
                name = Language.GetString(el.modifierToken).Replace("{0}", "").Trim();
                if (string.IsNullOrEmpty(name)) name = el.name;
                color = el.color;
                art = ed.pickupIconTexture;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Pulls your minions onto the board, one monster zone per type.</summary>
        private void GatherAllies()
        {
            if (!DuelConfig.AlliesJoinDuel.Value || !Player.master || !NetworkServer.active) return;   // allies are host-only
            Dictionary<BodyIndex, FieldMonster> groups = new Dictionary<BodyIndex, FieldMonster>();

            foreach (CharacterMaster m in CharacterMaster.readOnlyInstancesList)
            {
                if (!m || m == Player.master) continue;
                MinionOwnership mo = m.minionOwnership;
                if (!mo || mo.ownerMaster != Player.master) continue;
                CharacterBody b = m.GetBody();
                if (!b || !b.healthComponent || !b.healthComponent.alive) continue;

                if (!groups.TryGetValue(b.bodyIndex, out FieldMonster fm))
                {
                    int slot = FreeSlot(Monsters);
                    if (slot < 0) continue; // more ally types than zones: the rest stay on the map
                    fm = new FieldMonster
                    {
                        Slot = slot,
                        Allies = new List<CharacterBody>(),
                        Card = new Card { Name = SafeName(b), Type = CardType.Monster, Art = b.portraitIcon }
                    };
                    fm.CardObject = Board.LayCard(DuelBoard.MonsterLocal(slot), AllyFrame, b.portraitIcon, "AllyCard" + slot, 2.3f, 3.1f);
                    Monsters[slot] = fm;
                    groups[b.bodyIndex] = fm;
                }

                int k = fm.Allies.Count;
                fm.Allies.Add(b);
                float ang = k * 2.4f;
                AllyHome home = new AllyHome
                {
                    Body = b,
                    Position = b.footPosition,
                    AI = m.GetComponents<BaseAI>(),
                    Offset = k == 0 ? Vector3.zero : new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Mathf.Min(1.1f, 0.5f + 0.15f * k),
                    Group = fm
                };
                foreach (BaseAI ai in home.AI) if (ai) ai.enabled = false;
                _allies.Add(home);
                Teleport(b, AllySpot(home));
            }

            foreach (FieldMonster fm in groups.Values)
            {
                fm.RefreshAlly();
                AddLog($"Ally joins: {fm.Title}, ATK {MonsterAtk(fm)} / DEF {MonsterDef(fm)}.");
            }
        }

        private Vector3 AllySpot(AllyHome h) => Board.World(DuelBoard.MonsterLocal(h.Group.Slot) + new Vector3(0f, 0.2f, -0.3f)) + Board.transform.rotation * h.Offset;

        /// <summary>Allies whose card is destroyed in battle are knocked out of the duel, back onto the map.</summary>
        private void ReleaseAllies(FieldMonster fm)
        {
            for (int i = _allies.Count - 1; i >= 0; i--)
            {
                AllyHome h = _allies[i];
                if (h.Group != fm) continue;
                if (h.AI != null) foreach (BaseAI ai in h.AI) if (ai) ai.enabled = true;
                if (h.Body && h.Body.healthComponent && h.Body.healthComponent.alive)
                {
                    Teleport(h.Body, h.Position);
                    if (NetworkServer.active) h.Body.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, 1.5f);
                }
                _allies.RemoveAt(i);
            }
        }

        private static string SafeName(CharacterBody b)
        {
            try
            {
                string s = Language.GetString(b.baseNameToken);
                if (!string.IsNullOrEmpty(s)) return s;
            }
            catch { }
            return b.name.Replace("(Clone)", "");
        }

        // ------------------------------------------------------------ partner monster effects

        private bool _featherWardUsed;
        private int _playerBurn;            // Blazing: LP you lose in your next Standby Phase
        private int _healBlockedRound = -1; // Malachite
        private bool _timeStop;
        private bool _gloopReformUsed;
        private Card _gloopPending;

        private FieldMonster FaceUpPartner(PartnerKind k)
        {
            foreach (FieldMonster m in Monsters)
                if (m != null && !m.IsAlly && !m.FaceDown && m.Card.Partner == k) return m;
            return null;
        }

        private void PartnerFx(FieldMonster fm, string title, string sub)
        {
            PartnerInfo info = Partners.Info(fm.Card.Partner);
            Color c = info != null ? info.Color : new Color(1f, 0.85f, 0.35f);
            ShowBanner($"{fm.Card.Name.ToUpper()}: {title}");
            if (!Board) return;
            Vector3 at = Board.MonsterPos(fm.Slot);
            Pop(at + Vector3.up * 3f, title, sub, c, true);
            try { Partners.Burst(at + Vector3.up * 0.2f, c, 3.5f); } catch { }
            DuelSfx.Play(DuelSound.Activate, Player.gameObject);
        }

        /// <summary>Runs a partner's "when summoned" effect (Tock's Time Roulette).</summary>
        private void OnPartnerSummoned(FieldMonster fm)
        {
            if (fm == null || fm.IsAlly || fm.FaceDown) return;
            try { if (Board) Partners.Circle(Board.MonsterPos(fm.Slot) + Vector3.up * 0.15f, Partners.Info(fm.Card.Partner)?.Color ?? Color.white, 2.2f); } catch { }
            if (fm.Card.Partner != PartnerKind.Tock) return;
            bool heads = UnityEngine.Random.value < 0.5f;
            if (heads)
            {
                _timeStop = true;
                PartnerFx(fm, "TIME STOP!", "Heads: the enemy skips its next Battle Phase");
                AddLog("Time Roulette: HEADS. Time stops: the enemy skips its next Battle Phase.");
            }
            else
            {
                PartnerFx(fm, "TIME ROULETTE", "Tails: draw 2");
                AddLog("Time Roulette: TAILS. You draw 2 cards.");
                Draw(2);
            }
        }

        // ------------------------------------------------------------ per frame

        public void Tick(float dt)
        {
            if (Finished) return;

            if (!Player || !Player.healthComponent || !Player.healthComponent.alive)
            {
                if (Result == Outcome.None) { Result = Outcome.Defeat; ResultReason = "Your Life Points hit zero."; }
                CurrentPhase = Phase.Finished;
                return;
            }

            PinPlayer();
            PinAllies();
            if (Board) { try { Camera.Aim(Board.transform); } catch { } } // picks up F8 config changes

            float now = Time.time;
            foreach (Representative r in Reps)
            {
                r.Refresh();
                if (r.Defeated && r.Hologram && (r.Destroyed || now - r.LastHitTime > 0.5f))
                {
                    Color c = r.EliteName != null ? r.EliteColor : new Color(1f, 0.45f, 0.35f);
                    if (r.IsCommander)
                    {
                        ShowBanner($"COMMANDER {r.Name.ToUpper()} DEFEATED!");
                        AddLog($"The enemy commander, {r.Name}, falls.");
                        DuelFx.Shatter(r.Hologram, c, 3f);
                    }
                    else
                    {
                        DuelFx.Shatter(r.Hologram, c, 1.8f);
                        AddLog($"{r.Name} goes to the enemy graveyard.");
                    }
                    r.Hologram = null;
                    r.ModelRoot = null;
                    if (!r.IsCommander) r.ZoneIndex = -1;
                    continue;
                }
                if (r.ModelRoot)
                {
                    float shake = Mathf.Clamp01(1f - (now - r.LastHitTime) / 0.4f);
                    float lunge = Lunge(now - r.LungeTime);
                    r.ModelRoot.localPosition = r.ModelBase + new Vector3(Mathf.Sin(now * 70f) * 0.25f * shake, lunge * 0.8f, 0f) + r.LungeOffset * lunge;
                }
            }

            for (int i = 0; i < Zones; i++)
            {
                FieldMonster m = Monsters[i];
                if (m == null) continue;
                if (m.Statue)
                {
                    float lunge = Lunge(now - m.LungeTime);
                    float shake = Mathf.Clamp01(1f - (now - m.LastHitTime) / 0.4f);
                    if (lunge > 0f || m.LungeTime > 0f || shake > 0f)
                        m.Statue.transform.localPosition = new Vector3(Mathf.Sin(now * 70f) * 0.2f * shake, 0.05f + lunge * 0.8f, -0.2f) + m.LungeOffset * lunge;
                    m.Statue.transform.localRotation = Quaternion.identity;   // face the enemy side like a chess piece
                }
                if (m.IsAlly)
                {
                    m.RefreshAlly();
                    if (m.Dead)
                    {
                        AddLog($"Your ally {m.Card.Name} has fallen.");
                        Pop(Board.MonsterPos(i), "DESTROYED", m.Card.Name, new Color(1f, 0.3f, 0.3f), true);
                        RemoveMonster(i, true);
                    }
                }
            }

            if (Board) Board.SetPiles(DrawPile.Count, ExtraDeck.Count, Graveyard.Count, EnemyDeckCount, EnemyExtraCount, EnemyGraveCount, Banished.Count);
            Popups.RemoveAll(p => Time.unscaledTime - p.Born > 2.2f);

            if (_finishTimer >= 0f)
            {
                _finishTimer -= dt;
                if (_finishTimer <= 0f) CurrentPhase = Phase.Finished;
                return;
            }

            // Victory: their LP hits zero, or their field and deck are both empty.
            if (Commander != null && Commander.Defeated)
            {
                SetResult(Outcome.Victory, $"The enemy's LP hit zero: {Commander.Name} has fallen!");
                return;
            }
            if (Commander == null && DirectorLifeMax > 0f && DirectorLife <= 0f)
            {
                SetResult(Outcome.Victory, "The Director's LP hit zero!");
                return;
            }
            if (!EnemyMonstersOnField && EnemyDeckCount == 0 && (Commander == null || Commander.Defeated))
            {
                SetResult(Outcome.Victory, "Every enemy monster is gone!");
                return;
            }

            if (ChainBusy) { TickChain(dt); return; }
            TickPhases(dt);
        }

        private void PinPlayer()
        {
            if (Player.characterMotor) Player.characterMotor.velocity = Vector3.zero;
            if ((Player.footPosition - _arenaSpot).sqrMagnitude > 9f) Teleport(Player, _arenaSpot);
        }

        private void PinAllies()
        {
            foreach (AllyHome h in _allies)
            {
                CharacterBody b = h.Body;
                if (!b || !b.healthComponent || !b.healthComponent.alive || h.Group.CardObject == null) continue;
                if (b.characterMotor) b.characterMotor.velocity = Vector3.zero;
                if (b.rigidbody) b.rigidbody.velocity = Vector3.zero;
                Vector3 spot = AllySpot(h);
                if ((b.footPosition - spot).sqrMagnitude > 2.5f) Teleport(b, spot);
            }
        }

        // ------------------------------------------------------------ phases

        /// <summary>Moves to a phase and runs what happens at its start.</summary>
        private void EnterPhase(Phase p)
        {
            if (Result != Outcome.None) return;
            CurrentPhase = p;
            PhaseChangedAt = Time.unscaledTime;
            _phaseTimer = Mathf.Max(0.2f, DuelConfig.PhaseDelay.Value);
            DuelSfx.Play(DuelSound.Phase, Player.gameObject);

            switch (p)
            {
                case Phase.Draw:
                    _turnId++;
                    foreach (FieldMonster m in Monsters)
                    {
                        if (m == null) continue;
                        m.HasAttacked = false;
                        m.SummonedThisTurn = false;
                        m.PositionChanged = false;
                        m.AllThisTurn = false;
                    }
                    SummonsLeft = DuelConfig.NormalSummonsPerTurn.Value;
                    _auraUsedRound.Clear();
                    if (Round > 1)
                    {
                        int before = Hand.Count;
                        Draw(DuelConfig.DrawPerTurn.Value);
                        int got = Hand.Count - before;
                        AddLog($"Turn {Round}. Draw Phase: you draw {got}.");
                        if (got > 0) DuelSfx.Play(DuelSound.Draw, Player.gameObject);
                    }
                    else AddLog("Turn 1. You go first: no card is drawn on the first turn.");
                    break;

                case Phase.Standby:
                    PlayerStandby();
                    foreach (FieldMonster lm in Monsters)
                        if (lm != null && !lm.FaceDown && lm.Card.Extra == ExtraKind.Link) { Step("link banner", () => DuelEcho.LinkBanner(Player)); break; }
                    break;

                case Phase.Main1:
                    if (Round == 1 && DuelConfig.FirstTurnNoBattle.Value)
                        PopScreen("Turn 1: the player going first can't conduct a Battle Phase", new Color(1f, 0.9f, 0.6f));
                    break;

                case Phase.Battle:
                    AddLog("Battle Phase: click a ready monster to attack your target.");
                    break;

                case Phase.Main2:
                    break;

                case Phase.End:
                    PlayerEndPhase();
                    break;

                case Phase.EnemyDraw:
                    _turnId++;
                    Step("enemy draw phase", EnemyDrawPhase);
                    break;

                case Phase.EnemyStandby:
                    break;

                case Phase.EnemyMain:
                    Step("enemy main phase", EnemyMainPhase);
                    _phaseTimer += 0.4f;
                    break;

                case Phase.EnemyBattle:
                    _enemyQueue.Clear();
                    _featherWardUsed = false;
                    if (_timeStop)
                    {
                        _timeStop = false;
                        ShowBanner("TIME STOP: the enemy cannot attack!");
                        AddLog("Time stop: the enemy skips its Battle Phase.");
                        break;
                    }
                    int maxActions = 0;
                    foreach (Representative r in Reps) if (r.OnField && !r.Defeated && !r.DefensePos) maxActions = Mathf.Max(maxActions, r.Actions);
                    for (int a = 0; a < maxActions; a++)
                        foreach (Representative r in Reps)
                            if (r.OnField && !r.Defeated && !r.DefensePos && r.Actions > a) _enemyQueue.Enqueue(r);
                    _enemyTimer = 0.8f;
                    AddLog(_enemyQueue.Count > 0 ? $"Enemy Battle Phase: {_enemyQueue.Count} possible attack(s)." : "Enemy Battle Phase: nothing in attack position.");
                    break;

                case Phase.EnemyEnd:
                    EliteEnemyEnd();
                    ClearTempMods();
                    break;
            }
        }

        private void TickPhases(float dt)
        {
            switch (CurrentPhase)
            {
                case Phase.Main1:
                case Phase.Battle:
                case Phase.Main2:
                    return; // waiting for you

                case Phase.EnemyBattle:
                    _enemyTimer -= dt;
                    if (_enemyTimer > 0f) return;
                    if (_enemyQueue.Count > 0)
                    {
                        EnemyAction(_enemyQueue.Dequeue());
                        _enemyTimer = DuelConfig.EnemyTurnSpeed.Value;
                        return;
                    }
                    EnterPhase(Phase.EnemyEnd);
                    return;
            }

            _phaseTimer -= dt;
            if (_phaseTimer > 0f) return;
            switch (CurrentPhase)
            {
                case Phase.Draw: EnterPhase(Phase.Standby); break;
                case Phase.Standby: EnterPhase(Phase.Main1); break;
                case Phase.End: EnterPhase(Phase.EnemyDraw); break;
                case Phase.EnemyDraw: EnterPhase(Phase.EnemyStandby); break;
                case Phase.EnemyStandby: EnterPhase(Phase.EnemyMain); break;
                case Phase.EnemyMain: EnterPhase(Phase.EnemyBattle); break;
                case Phase.EnemyEnd: StartNextRound(); break;
            }
        }

        /// <summary>What "next phase" does from where you are (for the button label).</summary>
        public string NextPhaseLabel()
        {
            switch (CurrentPhase)
            {
                case Phase.Main1: return Round == 1 && DuelConfig.FirstTurnNoBattle.Value ? "END PHASE" : "BATTLE PHASE";
                case Phase.Battle: return "MAIN PHASE 2";
                case Phase.Main2: return "END PHASE";
            }
            return null;
        }

        /// <summary>Main 1 → Battle → Main 2 → End.</summary>
        public void NextPhase()
        {
            if (!IsPlayerTurn || Selecting != null) return;
            switch (CurrentPhase)
            {
                case Phase.Main1:
                    if (Round == 1 && DuelConfig.FirstTurnNoBattle.Value) { EnterPhase(Phase.End); return; }
                    EnterPhase(Phase.Battle);
                    return;
                case Phase.Battle: EnterPhase(Phase.Main2); return;
                case Phase.Main2: EnterPhase(Phase.End); return;
            }
        }

        /// <summary>Straight to your End Phase.</summary>
        public void EndTurn()
        {
            if (!IsPlayerTurn || Selecting != null) return;
            if (CurrentPhase == Phase.Main1 || CurrentPhase == Phase.Battle || CurrentPhase == Phase.Main2) EnterPhase(Phase.End);
        }

        /// <summary>Your Standby Phase: burns tick, Gloop re-forms.</summary>
        private void PlayerStandby()
        {
            if (_gloopPending != null)
            {
                Card g = _gloopPending;
                _gloopPending = null;
                if (FreeSlot(Monsters) >= 0)
                {
                    PlaceMonster(g, "Reform", false, false, false, null);
                    AddLog("Standby Phase: Gloop re-forms!");
                }
                else Graveyard.Add(g);
            }
            if (_playerBurn > 0)
            {
                int b = _playerBurn;
                _playerBurn = 0;
                AddLog($"Standby Phase: you burn for {b}.");
                DamagePlayerLP(b, null, "BURN");
            }
        }

        /// <summary>Your End Phase: discard down to the hand limit, effects that last "until the End Phase" wear off.</summary>
        private void PlayerEndPhase()
        {
            int limit = Mathf.Max(1, DuelConfig.EndPhaseHandLimit.Value);
            if (Hand.Count > limit)
            {
                List<Card> order = new List<Card>(Hand);
                order.Sort((a, b) => Value(a).CompareTo(Value(b)));
                List<string> names = new List<string>();
                for (int i = 0; i < order.Count && Hand.Count > limit; i++)
                {
                    Hand.Remove(order[i]);
                    Graveyard.Add(order[i]);
                    names.Add(order[i].Name);
                }
                AddLog($"End Phase: hand limit {limit}, you discard {string.Join(", ", names)}.");
                PopScreen($"Hand limit: discarded {names.Count}", new Color(1f, 0.8f, 0.6f));
            }
            ClearTempMods();
        }

        private static int Value(Card c)
        {
            switch (c.Type)
            {
                case CardType.Monster: return c.AtkPts + c.DefPts / 2;
                case CardType.Trap: return c.IsCounterTrap ? 1800 : 1500;
                case CardType.Spell: return c.Effect == CardEffect.Ritual || c.Effect == CardEffect.Revive ? 2000 : 1200 + c.SpellPts;
            }
            return 1000;
        }

        private void ClearTempMods()
        {
            foreach (FieldMonster m in Monsters) if (m != null) m.TempAtk = 0;
            foreach (Representative r in Reps) { r.AtkMod = 0; r.DefMod = 0; }
        }

        private void StartNextRound()
        {
            if (DuelConfig.MaxRounds.Value > 0 && Round >= DuelConfig.MaxRounds.Value)
            {
                SetResult(Outcome.Defeat, $"Time ran out after {Round} turns: the duel dimension collapses.");
                return;
            }
            Round++;
            ShowBanner($"TURN {Round} · YOUR TURN");
            EnterPhase(Phase.Draw);
        }

        // ------------------------------------------------------------ chains

        public enum ChainEvent { PlayerAttack, EnemyAttack, PlayerCard }
        private enum ChainStep { None, WaitPlayer, Resolving }

        public readonly List<ChainLink> Chain = new List<ChainLink>();
        private ChainStep _chainStep;
        private ChainEvent _chainEvent;
        private Action _afterChain;
        private float _chainTimer;
        private int _resolveIndex;
        private BattleInfo _battle;

        public bool ChainBusy => _chainStep != ChainStep.None;
        public bool AwaitingResponse => _chainStep == ChainStep.WaitPlayer;
        public bool ChainResolving => _chainStep == ChainStep.Resolving;
        public float ChainTimeLeft => _chainTimer;
        /// <summary>What you'd be responding to, in words.</summary>
        public string ChainContext { get; private set; } = "";

        private int RequiredSpeed => Chain.Count == 0 ? 2 : Mathf.Max(2, Chain[Chain.Count - 1].Speed);

        /// <summary>
        /// Starts a chain. With no first link (an attack declaration) the defending side gets the first
        /// response window. Each new link gives the other side a window; a pass resolves the chain,
        /// last link first. Spell Speed 2 can answer 1 or 2; only Spell Speed 3 answers 3.
        /// </summary>
        private void StartChain(ChainEvent ev, ChainLink first, Action after)
        {
            Chain.Clear();
            _chainEvent = ev;
            _afterChain = after;
            if (first != null) AddLink(first);
            bool playerWindow = first != null ? !first.Mine : ev == ChainEvent.EnemyAttack;
            OpenWindow(playerWindow);
        }

        private void AddLink(ChainLink l)
        {
            l.Number = Chain.Count + 1;
            Chain.Add(l);
            DuelSfx.Play(DuelSound.Chain, Player.gameObject);
            if (Chain.Count > 1) ShowBanner($"CHAIN {l.Number}: {l.Name}");
            AddLog($"Chain Link {l.Number}: <color=#{ColorUtility.ToHtmlStringRGB(l.Color)}>{l.Name}</color> (Spell Speed {l.Speed})" + (l.Mine ? "" : " by the enemy") + ".");
        }

        private void OpenWindow(bool player)
        {
            if (Result != Outcome.None) { BeginResolve(); return; }
            if (player)
            {
                if (PlayerResponses().Count == 0) { BeginResolve(); return; }
                ChainLink last = Chain.Count > 0 ? Chain[Chain.Count - 1] : null;
                ChainContext = last != null ? $"{last.Name}: {last.Text}"
                             : _battle != null && !_battle.Mine ? $"{_battle.Rep.Label} (ATK {_battle.Rep.Atk}) declares an attack on " + (_battle.Slot >= 0 && Monsters[_battle.Slot] != null ? (Monsters[_battle.Slot].FaceDown ? "your face-down monster" : Monsters[_battle.Slot].Title) : "you directly")
                             : "the enemy's move";
                _chainStep = ChainStep.WaitPlayer;
                _chainTimer = Mathf.Max(1.5f, DuelConfig.ChainWindow.Value);
                return;
            }
            ChainLink e = EnemyResponse();
            if (e != null) { AddLink(e); OpenWindow(true); }
            else BeginResolve();
        }

        private void BeginResolve()
        {
            _chainStep = ChainStep.Resolving;
            _resolveIndex = Chain.Count - 1;
            _chainTimer = Chain.Count > 0 ? 0.45f : 0f;
        }

        private void TickChain(float dt)
        {
            _chainTimer -= dt;
            if (_chainStep == ChainStep.WaitPlayer)
            {
                if (_chainTimer <= 0f) PassChain();
                return;
            }
            if (_chainTimer > 0f) return;
            if (_resolveIndex >= 0)
            {
                ChainLink l = Chain[_resolveIndex--];
                ResolveLink(l);
                _chainTimer = 0.65f;
                return;
            }
            Action after = _afterChain;
            _afterChain = null;
            Chain.Clear();
            _chainStep = ChainStep.None;
            try { after?.Invoke(); }
            catch (Exception e) { DuelistPlugin.Log.LogError("After-chain step failed:\n" + e); }
        }

        private void ResolveLink(ChainLink l)
        {
            if (l.Negated)
            {
                AddLog($"Chain Link {l.Number}, {l.Name}, is negated.");
                if (l.Visual) Pop(l.Visual.transform.position + Vector3.up * 2f, "NEGATED", l.Name, new Color(0.7f, 0.7f, 0.8f), true);
                DuelSfx.Play(DuelSound.Negate, Player.gameObject);
            }
            else
            {
                if (Chain.Count > 1) AddLog($"Chain Link {l.Number} resolves: {l.Name}.");
                try { l.Resolve?.Invoke(); }
                catch (Exception e) { DuelistPlugin.Log.LogError($"Chain link {l.Name} failed:\n" + e); }
            }
            if (l.Visual) DuelFx.Shatter(l.Visual, l.Color, 1.2f);
        }

        /// <summary>Your set cards that may respond right now (zone indexes).</summary>
        public List<int> PlayerResponses()
        {
            List<int> list = new List<int>();
            int need = RequiredSpeed;
            ChainLink last = Chain.Count > 0 ? Chain[Chain.Count - 1] : null;
            for (int i = 0; i < Zones; i++)
            {
                SetCard s = Backrow[i];
                if (s == null || s.SetTurn >= _turnId) continue;   // not the turn it was set
                Card c = s.Card;
                if (c.SpellSpeed < need) continue;
                if (c.IsQuickPlay) { list.Add(i); continue; }
                switch (c.Effect)
                {
                    case CardEffect.Negate:
                    case CardEffect.Reflect:
                        if (last == null && _chainEvent == ChainEvent.EnemyAttack && _battle != null && !_battle.Mine) list.Add(i);
                        break;
                    case CardEffect.Counter:
                        if (last != null && !last.Mine) list.Add(i);
                        break;
                }
            }
            return list;
        }

        /// <summary>Why a set card can't be activated now (for tooltips), or null.</summary>
        public string SetCardReason(int slot)
        {
            SetCard s = slot >= 0 && slot < Zones ? Backrow[slot] : null;
            if (s == null) return "Empty";
            if (s.SetTurn >= _turnId) return "Can't be activated the turn it was set";
            if (AwaitingResponse) return PlayerResponses().Contains(slot) ? null : $"Needs Spell Speed {RequiredSpeed}+ and a matching moment";
            if (s.Card.IsQuickPlay) return IsPlayerTurn ? null : "Wait for your turn or a chain";
            switch (s.Card.Effect)
            {
                case CardEffect.Negate:
                case CardEffect.Reflect: return "Springs when an enemy monster declares an attack";
                case CardEffect.Counter: return "Springs when the enemy activates an effect";
            }
            return "Can't be activated now";
        }

        /// <summary>You respond with a set card (click it on the board or in the chain prompt).</summary>
        public void RespondWith(int slot)
        {
            if (!AwaitingResponse || !PlayerResponses().Contains(slot)) return;
            ChainLink l = ActivateSetCard(slot);
            if (l == null) return;
            AddLink(l);
            OpenWindow(false);
        }

        public void PassChain()
        {
            if (!AwaitingResponse) return;
            AddLog("You don't respond.");
            BeginResolve();
        }

        /// <summary>A set Quick-Play spell activated on your own turn, outside a chain.</summary>
        public void ActivateSetSpell(int slot)
        {
            if (AwaitingResponse) { RespondWith(slot); return; }
            if (!IsPlayerTurn) return;
            SetCard s = slot >= 0 && slot < Zones ? Backrow[slot] : null;
            if (s == null) return;
            if (Refuse(SetCardReason(slot))) return;
            ChainLink l = ActivateSetCard(slot);
            if (l != null) StartChain(ChainEvent.PlayerCard, l, null);
        }

        /// <summary>Flips a set card face-up into a chain link.</summary>
        private ChainLink ActivateSetCard(int slot)
        {
            SetCard s = Backrow[slot];
            if (s == null) return null;
            Card c = s.Card;
            s.Charges--;
            GameObject visual = null;
            if (s.Charges <= 0)
            {
                Backrow[slot] = null;
                visual = Board ? Board.TakeBackCard(slot) : null;
                Graveyard.Add(c);
            }
            Color col = c.Type == CardType.Spell ? SpellColor : TrapColor;
            if (Board)
            {
                if (visual) DuelFx.Activate(visual, Board.transform, col);
                else DuelFx.Pillar(Board.transform, Board.BackPos(slot), col, 5f, 1f);
            }
            DuelSfx.Play(DuelSound.Activate, Player.gameObject);
            ShowBanner($"{(c.Type == CardType.Spell ? "QUICK-PLAY" : c.IsCounterTrap ? "COUNTER TRAP" : "TRAP")}: {c.Name}!");
            if (Board) Pop(Board.BackPos(slot) + Vector3.up * 1.5f, c.IsCounterTrap ? "COUNTER TRAP!" : c.Type == CardType.Spell ? "QUICK-PLAY!" : "TRAP!", c.Name, col, true);

            ChainLink l = new ChainLink { Name = c.Name, Speed = c.SpellSpeed, Mine = true, Color = col, Visual = visual, Text = c.Describe(Player) };
            BattleInfo battle = _battle;
            ChainLink target = Chain.Count > 0 ? Chain[Chain.Count - 1] : null;
            switch (c.Effect)
            {
                case CardEffect.Negate:
                    l.Resolve = () =>
                    {
                        if (battle == null) return;
                        if (battle.Rep != null && battle.Rep.Elite == DuelElite.Perfected) { ElitePop(battle.Rep, battle.Rep.BoardPosition, "UNAFFECTED", "Perfected ignores traps"); return; }
                        battle.Negated = true;
                        _enemyQueue.Clear();
                        AddLog($"{c.Name}: the attack is negated and the enemy's Battle Phase ends.");
                        Pop(battle.Rep != null ? battle.Rep.BoardPosition + Vector3.up * 4f : Player.corePosition, "ATTACK NEGATED", "Battle Phase over", new Color(0.7f, 0.85f, 1f), true);
                        DuelSfx.Play(DuelSound.Negate, Player.gameObject);
                    };
                    break;
                case CardEffect.Reflect:
                    l.Resolve = () =>
                    {
                        if (battle == null || battle.Rep == null || battle.Rep.Defeated) return;
                        Representative rep = battle.Rep;
                        if (rep.Elite == DuelElite.Perfected) { ElitePop(rep, rep.BoardPosition, "UNAFFECTED", "Perfected ignores traps"); return; }
                        battle.Negated = true;
                        if (Board) Tracer(Board.World(new Vector3(0f, 1.5f, 3.2f)), rep.BoardPosition + Vector3.up * 2f);
                        if (rep.IsCommander)
                        {
                            int dmg = DuelStats.Round50(rep.Atk * 0.5f);
                            AddLog($"{c.Name}: the commander's attack rebounds for {dmg} damage.");
                            DamageEnemyLP(dmg, c.Name);
                        }
                        else
                        {
                            AddLog($"{c.Name} destroys the attacking {rep.Label}!");
                            DestroyRep(rep, c.Name, c.Item);
                        }
                    };
                    break;
                case CardEffect.Counter:
                    l.Resolve = () =>
                    {
                        if (target == null) return;
                        target.Negated = true;
                        if (target.AuraName != null) _auraDestroyed.Add(target.AuraName);
                        AddLog($"{c.Name} negates {target.Name}" + (target.AuraName != null ? " and seals that card for the rest of the duel." : "."));
                    };
                    break;
                default:
                    l.Resolve = () => ResolveSpell(c, null);
                    break;
            }
            return l;
        }

        /// <summary>The enemy AI: may chain one of its elite cards (once per round each).</summary>
        private ChainLink EnemyResponse()
        {
            ChainLink last = Chain.Count > 0 ? Chain[Chain.Count - 1] : null;
            if (last != null && !last.Mine) return null;   // it doesn't chain onto itself
            int need = RequiredSpeed;
            List<EnemyAura> ready = EnemyAuras.FindAll(a => AuraReady(a));
            for (int i = ready.Count - 1; i > 0; i--) { int j = UnityEngine.Random.Range(0, i + 1); EnemyAura t = ready[i]; ready[i] = ready[j]; ready[j] = t; }

            foreach (EnemyAura a in ready)
            {
                ChainLink l = AuraLink(a, last, need);
                if (l == null) continue;
                if (UnityEngine.Random.value > Mathf.Clamp01(DuelConfig.EnemyResponseChance.Value)) continue;
                _auraUsedRound[a.Name] = Round;
                l.AuraName = a.Name;
                l.Mine = false;
                l.Color = Color.Lerp(a.Color, Color.white, 0.2f);
                l.Visual = null;
                GameObject card = Board ? Board.EnemyBackCard(a.Slot) : null;
                if (card)
                {
                    DuelTween.From(card, Vector3.up * 0.6f, 1.15f, new Vector3(60f, 0f, 0f), 0.3f);
                    DuelFx.Pillar(Board.transform, card.transform.position, a.Color, 6f, 1.1f);
                }
                DuelSfx.Play(DuelSound.Activate, Player.gameObject);
                Pop((card ? card.transform.position : Board.CommanderPos) + Vector3.up * 2f, l.Name.ToUpper() + "!", $"{a.Name} card · Spell Speed {l.Speed}", a.Color, true);
                ShowBanner($"ENEMY CHAINS {a.Name.ToUpper()}: {l.Name}!");
                return l;
            }
            return null;
        }

        /// <summary>The response an enemy elite card offers in this situation, or null.</summary>
        private ChainLink AuraLink(EnemyAura a, ChainLink last, int need)
        {
            BattleInfo b = _battle;
            FieldMonster atk = b != null && b.Mine ? b.Attacker : null;
            bool attackDeclared = last == null && _chainEvent == ChainEvent.PlayerAttack && atk != null;

            if (last != null && last.Mine)
            {
                // answering your card
                if (a.Elite == DuelElite.Perfected && need <= 3)
                    return new ChainLink { Name = "Lunar Seal", Speed = 3, Text = "negate the activation of your card", Resolve = () => { last.Negated = true; AddLog($"Lunar Seal negates your {last.Name}."); } };
                if (a.Elite == DuelElite.Celestine && need <= 2)
                    return new ChainLink { Name = "Phase Veil", Speed = 2, Text = "negate the activation of your card", Resolve = () => { last.Negated = true; AddLog($"Phase Veil negates your {last.Name}."); } };
                return null;
            }
            if (!attackDeclared || need > 2) return null;

            switch (a.Elite)
            {
                case DuelElite.Blazing:
                    return new ChainLink { Name = "Flame Burst", Speed = 2, Text = "inflict 500 damage to you", Resolve = () => DamagePlayerLP(500, null, "Flame Burst") };
                case DuelElite.Glacial:
                    return new ChainLink { Name = "Ice Prison", Speed = 2, Text = "negate the attack and freeze the attacker", Resolve = () =>
                    {
                        b.Negated = true;
                        if (atk != null) atk.FrozenRound = Round + 1;
                        AddLog($"Ice Prison: {atk?.Title}'s attack is negated and it's frozen next turn.");
                        if (Board && atk != null) ElitePopAt(Board.MonsterPos(atk.Slot), a, "FROZEN", "can't attack next turn");
                    } };
                case DuelElite.Overloading:
                    return new ChainLink { Name = "Static Shock", Speed = 2, Text = "the attacker loses 700 ATK", Resolve = () => Weaken(atk, 700, a, "Static Shock") };
                case DuelElite.Malachite:
                    return new ChainLink { Name = "Toxic Bloom", Speed = 2, Text = "the attacker loses 500 ATK, no LP gain next turn", Resolve = () => { Weaken(atk, 500, a, "Toxic Bloom"); _healBlockedRound = Round + 1; } };
                case DuelElite.Celestine:
                    return new ChainLink { Name = "Phase Veil", Speed = 2, Text = "negate the attack", Resolve = () => { b.Negated = true; AddLog($"Phase Veil: {atk?.Title}'s attack passes through nothing."); } };
                case DuelElite.Mending:
                    return new ChainLink { Name = "Regrowth", Speed = 2, Text = "the enemy gains 700 LP", Resolve = () => HealEnemyLP(700, "Regrowth") };
                case DuelElite.Voidtouched:
                    if (atk == null || MonsterAtk(atk) > 2000) return null;
                    return new ChainLink { Name = "Void Collapse", Speed = 2, Text = "destroy the attacker (ATK 2000 or less)", Resolve = () =>
                    {
                        if (atk == null || Monsters[atk.Slot] != atk || MonsterAtk(atk) > 2000) return;
                        b.Negated = true;
                        AddLog($"Void Collapse destroys {atk.Title}!");
                        DestroyMine(atk.Slot, "Void Collapse");
                    } };
                case DuelElite.Gilded:
                    return new ChainLink { Name = "Gold Toll", Speed = 2, Text = "you lose 10% of your gold", Resolve = () =>
                    {
                        if (!Player.master) return;
                        uint money = Player.master.money;
                        uint toll = (uint)Mathf.CeilToInt(money * 0.1f);
                        if (toll > money) toll = money;
                        DuelNet.TakeMoney(Player.master, toll);
                        AddLog($"Gold Toll: you pay ${toll}.");
                        Pop(Player.corePosition + Vector3.up * 2f, $"-${toll}", "Gold Toll", a.Color, true);
                    } };
                case DuelElite.Twisted:
                    return new ChainLink { Name = "Twist", Speed = 2, Text = "the attacker's ATK is halved", Resolve = () => { if (atk != null) Weaken(atk, MonsterAtk(atk) / 2, a, "Twist"); } };
            }
            return null;
        }

        private void ElitePopAt(Vector3 at, EnemyAura a, string title, string sub) => Pop(at + Vector3.up * 2.2f, title, sub, a.Color, false);

        private void Weaken(FieldMonster fm, int amount, EnemyAura a, string source)
        {
            if (fm == null || Monsters[fm.Slot] != fm) return;
            int before = MonsterAtk(fm);
            fm.TempAtk -= amount;
            AddLog($"{source}: {fm.Title} loses {amount} ATK ({before} → {MonsterAtk(fm)}) until the End Phase.");
            if (Board) ElitePopAt(Board.MonsterPos(fm.Slot), a, $"-{amount} ATK", source);
        }

        // ------------------------------------------------------------ targeting

        public void SelectRep(int index)
        {
            if (index < 0 || index >= Reps.Count) return;
            Representative r = Reps[index];
            if (Targetable(r)) SelectedRep = index;
            else if (r.IsCommander && !r.Defeated) PopScreen(EnemyMonstersOnField ? "Destroy their monsters first; then your attacks hit their LP directly" : "No monsters left: your attacks hit their LP directly", new Color(1f, 0.8f, 0.6f));
            else if (Cloaked(r)) PopScreen("Celestine monsters can't be attacked while another enemy monster is on the field", new Color(0.8f, 0.9f, 1f));
        }

        public void CycleTarget(int dir)
        {
            if (Reps.Count == 0) return;
            for (int i = 1; i <= Reps.Count; i++)
            {
                int idx = ((SelectedRep + dir * i) % Reps.Count + Reps.Count) % Reps.Count;
                if (Targetable(Reps[idx])) { SelectedRep = idx; return; }
            }
        }

        public Representative CurrentTarget()
        {
            if (SelectedRep >= 0 && SelectedRep < Reps.Count && Targetable(Reps[SelectedRep])) return Reps[SelectedRep];
            CycleTarget(1);
            if (SelectedRep >= 0 && SelectedRep < Reps.Count && Targetable(Reps[SelectedRep])) return Reps[SelectedRep];
            return null;
        }

        // ------------------------------------------------------------ ATK and DEF

        public int MonsterCountAll { get { int n = 0; foreach (FieldMonster m in Monsters) if (m != null) n++; return n; } }

        /// <summary>A monster's ATK right now: its own, boosted by face-up Link monsters and its effects.</summary>
        public int MonsterAtk(FieldMonster fm)
        {
            if (fm == null) return 0;
            float baseAtk = fm.IsAlly ? fm.AllyAtkPts() : fm.Card.AtkPts;
            float boost = 0f;
            foreach (FieldMonster m in Monsters)
                if (m != null && m != fm && !m.FaceDown && !m.IsAlly && m.Card.Extra == ExtraKind.Link)
                    boost += DuelConfig.LinkBoost.Value * m.Card.LinkRating;
            return Mathf.Max(0, DuelStats.Round50(baseAtk * (1f + boost) * (1f + fm.AtkBonus)) + fm.TempAtk);
        }

        public int MonsterDef(FieldMonster fm)
        {
            if (fm == null) return 0;
            if (fm.IsAlly) return fm.AllyDefPts();
            if (fm.Card.Extra == ExtraKind.Link) return 0;
            return fm.Card.DefPts;
        }

        private List<FieldMonster> Tributable(bool faceUpOnly)
        {
            List<FieldMonster> list = new List<FieldMonster>();
            foreach (FieldMonster m in Monsters)
                if (m != null && !m.IsAlly && (!faceUpOnly || !m.FaceDown)) list.Add(m);
            return list;
        }

        // ---- which plays a hand card allows right now (null = allowed)

        private string MainOnly()
        {
            if (!IsPlayerPhase) return "Not your turn";
            if (ChainBusy) return "A chain is resolving";
            if (!IsMainPhase) return CurrentPhase == Phase.Battle ? "Only in a Main Phase (you're in the Battle Phase)" : "Only in your Main Phase";
            return null;
        }

        public string CanNormalSummon(Card c)
        {
            string m = MainOnly(); if (m != null) return m;
            if (c.Type != CardType.Monster) return "Not a monster";
            if (c.IsRitual) return "Ritual monster: play a Ritual spell";
            if (c.TributesNeeded > 0) return $"Lv {c.Level} needs a Tribute Summon";
            if (SummonsLeft <= 0) return "Normal Summon already used this turn";
            if (FreeSlot(Monsters) < 0) return "Monster zones full";
            return null;
        }

        public string CanSet(Card c) => CanNormalSummon(c);

        public string CanTributeSummon(Card c)
        {
            string m = MainOnly(); if (m != null) return m;
            if (c.Type != CardType.Monster || c.IsRitual || c.TributesNeeded == 0) return "No tribute needed";
            if (SummonsLeft <= 0) return "Normal Summon already used this turn";
            if (Tributable(false).Count < c.TributesNeeded) return $"Needs {c.TributesNeeded} card monster(s) on your field (not allies)";
            return null;
        }

        public string CanSpecialFromHand(Card c)
        {
            string m = MainOnly(); if (m != null) return m;
            if (c.Type != CardType.Monster || c.IsRitual || c.Level > 4) return "Only Lv 4 or lower";
            if (MonsterCountAll > 0) return "Only while you control no monsters";
            if (FreeSlot(Monsters) < 0) return "Monster zones full";
            return null;
        }

        private int TributableLevels()
        {
            int sum = 0;
            foreach (FieldMonster m in Tributable(false)) sum += m.Card.MaterialLevel;
            return sum;
        }

        private Card FirstRitualMonsterInHand() => Hand.Find(h => h.Type == CardType.Monster && h.IsRitual);

        private string RitualSpellReason()
        {
            Card rm = FirstRitualMonsterInHand();
            if (rm == null) return "Needs a Ritual monster (boss item) in your hand";
            if (TributableLevels() < rm.Level) return $"Needs monsters totaling Lv {rm.Level} on your field (you have {TributableLevels()})";
            return null;
        }

        private string ReviveReason()
        {
            if (!Graveyard.Exists(g => g.Type == CardType.Monster || g.Type == CardType.Extra)) return "No monsters in your graveyard";
            if (FreeSlot(Monsters) < 0) return "Monster zones full";
            return null;
        }

        /// <summary>Whether an Extra Deck monster could be summoned from what's on your field now.</summary>
        public string ExtraReason(Card c)
        {
            string m = MainOnly(); if (m != null) return m;
            List<FieldMonster> up = Tributable(true);
            switch (c.Extra)
            {
                case ExtraKind.Synchro:
                    if (!up.Exists(x => x.Card.IsTuner && x.Card.MaterialLevel > 0)) return "Needs a face-up Tuner";
                    if (!up.Exists(x => !x.Card.IsTuner && x.Card.MaterialLevel > 0)) return "Needs a face-up non-Tuner";
                    return null;
                case ExtraKind.Xyz:
                {
                    Dictionary<int, int> byLevel = new Dictionary<int, int>();
                    foreach (FieldMonster x in up)
                    {
                        int l = x.Card.MaterialLevel;
                        if (l <= 0) continue;
                        byLevel.TryGetValue(l, out int n);
                        byLevel[l] = n + 1;
                        if (n + 1 >= 2) return null;
                    }
                    return "Needs 2 face-up monsters of the same Level";
                }
                default:
                    return up.Count < 2 ? "Needs 2 face-up card monsters (allies can't be used)" : null;
            }
        }

        /// <summary>Why a hand card can't be played at all right now (dims it in the hand), or null.</summary>
        public string BlockReason(Card c)
        {
            switch (c.Type)
            {
                case CardType.Monster:
                    if (c.IsRitual)
                    {
                        string m = MainOnly(); if (m != null) return m;
                        return Hand.Exists(h => h.Effect == CardEffect.Ritual) ? (TributableLevels() >= c.Level ? null : $"Needs monsters totaling Lv {c.Level}") : "Needs a Ritual spell in your hand";
                    }
                    if (CanNormalSummon(c) == null || CanTributeSummon(c) == null || CanSpecialFromHand(c) == null) return null;
                    return c.TributesNeeded > 0 ? CanTributeSummon(c) : CanNormalSummon(c);
                case CardType.Trap:
                {
                    string m = MainOnly(); if (m != null) return m;
                    return FreeSlot(Backrow) < 0 ? "Spell/Trap zones full" : null;
                }
                case CardType.Spell:
                {
                    if (c.IsQuickPlay)
                    {
                        if (!IsPlayerTurn) return IsPlayerPhase ? "A chain is resolving" : "Set it on your turn to use it on theirs";
                        if (CurrentPhase != Phase.Main1 && CurrentPhase != Phase.Battle && CurrentPhase != Phase.Main2) return "Wait for your Main or Battle Phase";
                        return null;
                    }
                    string m = MainOnly(); if (m != null) return m;
                    if (c.Effect == CardEffect.Ritual) return RitualSpellReason();
                    if (c.Effect == CardEffect.Revive) return ReviveReason();
                    return null;
                }
                default:
                    return ExtraReason(c);
            }
        }

        private bool Refuse(string reason)
        {
            if (reason == null) return false;
            PopScreen(reason, new Color(1f, 0.6f, 0.6f));
            return true;
        }

        // ---- summoning from the hand

        public void NormalSummon(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (Refuse(CanNormalSummon(c))) return;
            Hand.RemoveAt(handIndex);
            SummonsLeft--;
            PlaceMonster(c, "Normal Summon", false, false, false, null);
        }

        public void SetMonster(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (Refuse(CanSet(c))) return;
            Hand.RemoveAt(handIndex);
            SummonsLeft--;
            PlaceMonster(c, "Set", true, true, false, null);
        }

        public void SpecialFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (Refuse(CanSpecialFromHand(c))) return;
            Hand.RemoveAt(handIndex);
            PlaceMonster(c, "Special Summon", false, false, false, null);
        }

        /// <summary>Keyboard / quick play: does the most natural thing with a hand card.</summary>
        public void PlayFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];

            if (c.Type == CardType.Monster)
            {
                if (CanNormalSummon(c) == null) NormalSummon(handIndex);
                else if (CanTributeSummon(c) == null) StartTribute(handIndex);
                else if (CanSpecialFromHand(c) == null) SpecialFromHand(handIndex);
                else if (c.IsRitual)
                {
                    int spell = Hand.FindIndex(h => h.Effect == CardEffect.Ritual);
                    if (spell >= 0) StartRitual(spell); else Refuse("Needs a Ritual spell in your hand");
                }
                else Refuse(BlockReason(c));
                return;
            }
            if (c.Effect == CardEffect.Ritual) { StartRitual(handIndex); return; }
            if (c.Effect == CardEffect.Revive)
            {
                // Quick play revives your strongest monster; the mouse lets you pick from the graveyard.
                int best = -1;
                for (int i = 0; i < Graveyard.Count; i++)
                {
                    Card g = Graveyard[i];
                    if (g.Type != CardType.Monster && g.Type != CardType.Extra) continue;
                    if (best < 0 || g.AtkPts > Graveyard[best].AtkPts) best = i;
                }
                if (Refuse(MainOnly())) return;
                if (best >= 0) Revive(c, best); else Refuse(ReviveReason());
                return;
            }
            if (c.Type == CardType.Trap) { SetSpellTrap(handIndex); return; }
            ActivateSpell(handIndex);
        }

        /// <summary>Sets a trap (or a Quick-Play spell) face-down in your spell/trap zone.</summary>
        public void SetSpellTrap(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (c.Type != CardType.Trap && c.Type != CardType.Spell) return;
            if (Refuse(MainOnly())) return;
            int slot = FreeSlot(Backrow);
            if (slot < 0) { Refuse("Spell/Trap zones full"); return; }
            Hand.RemoveAt(handIndex);
            Backrow[slot] = new SetCard { Card = c, Charges = Mathf.Max(1, c.Charges), SetTurn = _turnId };
            if (Board)
            {
                Board.SetBackCard(slot, true);
                DuelFx.SetIn(Board.BackCardObject(slot));
                Pop(Board.BackPos(slot), "SET", null, new Color(0.9f, 0.6f, 1f), false);
            }
            DuelSfx.Play(DuelSound.Set, Player.gameObject);
            AddLog($"You set a card face-down.");
            ShowBanner("CARD SET");
        }

        /// <summary>Activates a spell from your hand. It sits face-up on a spell/trap zone while the chain resolves.</summary>
        public void ActivateSpell(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (c.Type != CardType.Spell) return;
            if (Refuse(BlockReason(c))) return;
            if (c.NeedsTarget && EnemyMonstersOnField && CurrentTarget() == null) { Refuse("No target"); return; }

            Hand.RemoveAt(handIndex);
            Graveyard.Add(c);
            Representative target = c.NeedsTarget ? CurrentTarget() : null;
            GameObject visual = Board ? Board.ShowSpellOnZone(c) : null;
            if (visual) DuelFx.Activate(visual, Board.transform, SpellColor);
            DuelSfx.Play(DuelSound.Activate, Player.gameObject);
            ShowBanner($"{(c.IsQuickPlay ? "QUICK-PLAY SPELL" : "SPELL")}: {c.Name}");
            ChainLink l = new ChainLink
            {
                Name = c.Name, Speed = c.SpellSpeed, Mine = true, Color = SpellColor, Visual = visual,
                Text = c.Describe(Player), Resolve = () => ResolveSpell(c, target)
            };
            StartChain(ChainEvent.PlayerCard, l, null);
        }

        /// <summary>Summon from the graveyard. A revived monster is banished if it leaves the field again.</summary>
        public void Revive(Card spell, int graveIndex)
        {
            if (!IsPlayerTurn || spell == null || !Hand.Contains(spell)) return;
            if (Refuse(MainOnly())) return;
            if (graveIndex < 0 || graveIndex >= Graveyard.Count) return;
            Card target = Graveyard[graveIndex];
            if (target.Type != CardType.Monster && target.Type != CardType.Extra) { Refuse("That's not a monster"); return; }
            if (Refuse(ReviveReason())) return;

            Hand.Remove(spell);
            Graveyard.RemoveAt(graveIndex);
            Graveyard.Add(spell);
            AddLog($"{spell.Name}: {target.Name} returns from the graveyard.");
            DuelSfx.Play(DuelSound.Activate, Player.gameObject);
            PlaceMonster(target, "Summon from GY", false, false, true, null);
        }

        // ---- material selection: Tribute, Ritual and Extra Deck summons let you pick on the board

        public enum SelectMode { Tribute, Ritual, Extra }

        public class Selection
        {
            public SelectMode Mode;
            public Card Card;      // the monster being summoned
            public Card Spell;     // the Ritual spell, for Ritual summons
            public readonly List<int> Chosen = new List<int>();
        }

        public Selection Selecting;

        public void StartTribute(int handIndex)
        {
            if (handIndex < 0 || handIndex >= Hand.Count) return;
            Card c = Hand[handIndex];
            if (Refuse(CanTributeSummon(c))) return;
            Selecting = new Selection { Mode = SelectMode.Tribute, Card = c };
            ShowBanner($"TRIBUTE SUMMON: pick {c.TributesNeeded} monster(s)");
        }

        public void StartRitual(int spellHandIndex)
        {
            if (spellHandIndex < 0 || spellHandIndex >= Hand.Count) return;
            Card spell = Hand[spellHandIndex];
            if (Refuse(MainOnly())) return;
            if (Refuse(RitualSpellReason())) return;
            Selecting = new Selection { Mode = SelectMode.Ritual, Card = FirstRitualMonsterInHand(), Spell = spell };
            ShowBanner($"RITUAL SUMMON: pick tributes totaling Lv {Selecting.Card.Level}");
        }

        public void StartExtra(int extraIndex)
        {
            if (extraIndex < 0 || extraIndex >= ExtraDeck.Count) return;
            Card c = ExtraDeck[extraIndex];
            if (Refuse(ExtraReason(c))) return;
            Selecting = new Selection { Mode = SelectMode.Extra, Card = c };
            ShowBanner($"{ExtraVerb(c).ToUpper()}: pick materials");
        }

        public void CancelSelection() => Selecting = null;

        public bool IsCandidate(int slot)
        {
            if (Selecting == null || slot < 0 || slot >= Zones) return false;
            FieldMonster m = Monsters[slot];
            if (m == null || m.IsAlly) return false;
            if (Selecting.Mode == SelectMode.Extra)
            {
                if (m.FaceDown) return false;
                if ((Selecting.Card.Extra == ExtraKind.Synchro || Selecting.Card.Extra == ExtraKind.Xyz) && m.Card.MaterialLevel <= 0) return false;
            }
            if (Selecting.Mode == SelectMode.Ritual && m.Card.MaterialLevel <= 0) return false;
            return true;
        }

        public void ToggleMaterial(int slot)
        {
            if (!IsCandidate(slot)) { Refuse("Can't use that monster here"); return; }
            if (Selecting.Chosen.Contains(slot)) Selecting.Chosen.Remove(slot);
            else Selecting.Chosen.Add(slot);
        }

        /// <summary>What the selection needs and how far along it is. ok = ready to confirm.</summary>
        public string SelectionStatus(out bool ok)
        {
            ok = false;
            if (Selecting == null) return "";
            List<Card> mats = Selecting.Chosen.ConvertAll(i => Monsters[i].Card);
            int levels = 0, tuners = 0;
            foreach (Card m in mats) { levels += m.MaterialLevel; if (m.IsTuner) tuners++; }
            Card c = Selecting.Card;

            switch (Selecting.Mode)
            {
                case SelectMode.Tribute:
                    ok = mats.Count == c.TributesNeeded;
                    return $"Tribute Summon {c.Name}: pick {c.TributesNeeded} monster(s) to tribute ({mats.Count}/{c.TributesNeeded})";
                case SelectMode.Ritual:
                    ok = mats.Count > 0 && levels >= c.Level;
                    return $"Ritual Summon {c.Name} with {Selecting.Spell.Name}: tributes totaling Lv {c.Level}+ (Lv {levels} chosen)";
            }
            switch (c.Extra)
            {
                case ExtraKind.Synchro:
                    ok = tuners == 1 && mats.Count >= 2;
                    return $"Synchro Summon {c.Name}: exactly 1 Tuner + non-Tuners ({tuners} Tuner, {mats.Count - tuners} other) → Lv {levels}";
                case ExtraKind.Xyz:
                {
                    bool same = mats.Count > 0 && mats.TrueForAll(m => m.MaterialLevel == mats[0].MaterialLevel);
                    ok = mats.Count >= 2 && same;
                    return $"XYZ Summon {c.Name}: 2+ monsters of the same Level" + (mats.Count > 0 ? (same ? $" → Rank {mats[0].MaterialLevel} ({mats.Count} materials)" : " (Levels don't match)") : "");
                }
                case ExtraKind.Link:
                    ok = mats.Count >= 2;
                    return $"Link Summon {c.Name}: 2+ monsters → Link-{mats.Count}";
                default:
                    ok = mats.Count == 2;
                    return $"Fusion Summon {c.Name}: pick 2 monsters ({mats.Count}/2)";
            }
        }

        public void ConfirmSelection()
        {
            if (Selecting == null) return;
            SelectionStatus(out bool ok);
            if (!ok) { Refuse("The materials don't fit yet"); return; }

            Selection sel = Selecting;
            Selecting = null;
            List<int> slots = new List<int>(sel.Chosen);
            List<Card> mats = slots.ConvertAll(i => Monsters[i].Card);
            int totalAtk = 0, totalDef = 0;
            foreach (int i in slots) { totalAtk += Monsters[i].Card.AtkPts; totalDef += Monsters[i].Card.DefPts; }
            bool xyz = sel.Mode == SelectMode.Extra && sel.Card.Extra == ExtraKind.Xyz;
            string names = string.Join(" + ", mats.ConvertAll(m => m.Name));

            // XYZ materials stay attached under the new monster; everything else goes to the graveyard.
            foreach (int i in slots) RemoveMonster(i, false, !xyz);

            switch (sel.Mode)
            {
                case SelectMode.Tribute:
                    Hand.Remove(sel.Card);
                    SummonsLeft--;
                    AddLog($"Tributed {names}.");
                    PlaceMonster(sel.Card, "Tribute Summon", false, false, false, null);
                    break;
                case SelectMode.Ritual:
                    Hand.Remove(sel.Spell);
                    Graveyard.Add(sel.Spell);
                    Hand.Remove(sel.Card);
                    AddLog($"{sel.Spell.Name}: tributed {names}.");
                    PlaceMonster(sel.Card, "Ritual Summon", false, false, false, null);
                    break;
                default:
                    ExtraDeck.Remove(sel.Card);
                    Card summoned = sel.Card.Summoned(mats, totalAtk, totalDef, Player);
                    AddLog($"{ExtraVerb(sel.Card)}: {names} → {summoned.Name}.");
                    PlaceMonster(summoned, ExtraVerb(sel.Card), false, false, false, xyz ? mats : null);
                    break;
            }
        }

        public static string ExtraVerb(Card c)
        {
            switch (c.Extra)
            {
                case ExtraKind.Synchro: return "Synchro Summon";
                case ExtraKind.Xyz: return "XYZ Summon";
                case ExtraKind.Link: return "Link Summon";
                default: return "Fusion Summon";
            }
        }

        /// <summary>Puts a monster on your first free zone in the given position, with its summon animation.</summary>
        private void PlaceMonster(Card c, string verb, bool faceDown, bool defense, bool revived, List<Card> xyzMaterials)
        {
            int slot = FreeSlot(Monsters);
            if (slot < 0) { Graveyard.Add(c); return; }
            if (!faceDown) Step("summon echo", () => DuelEcho.Summoned(Player, c, RealEnemies()));

            FieldMonster fm = new FieldMonster
            {
                Card = c, Slot = slot,
                FaceDown = faceDown, Defense = defense || faceDown,
                SummonedThisTurn = true, Revived = revived,
                Materials = xyzMaterials != null ? new List<Card>(xyzMaterials) : null
            };
            Monsters[slot] = fm;
            RefreshMonsterVisual(slot);

            if (faceDown)
            {
                if (Board) DuelFx.SetIn(fm.CardObject);
                DuelSfx.Play(DuelSound.Set, Player.gameObject);
                AddLog($"You set a monster face-down in defense position.");
                ShowBanner("MONSTER SET");
                if (Board) Pop(Board.MonsterPos(slot), "SET", null, new Color(0.9f, 0.75f, 0.5f), false);
                return;
            }

            bool special = verb != "Normal Summon" && verb != "Tribute Summon";
            if (Board)
            {
                DuelFx.SummonIn(fm.CardObject, fm.Statue, Board.transform, c.Frame, special);
                Pop(Board.MonsterPos(slot) + Vector3.up * 0.5f, verb.ToUpper(), $"ATK {MonsterAtk(fm)} / DEF {MonsterDef(fm)}", new Color(1f, 0.85f, 0.35f), false);
                if (special && _bigVfx) EffectManager.SpawnEffect(_bigVfx, new EffectData { origin = Board.MonsterPos(slot) + Vector3.up, scale = 2.5f }, true);
            }
            DuelSfx.Play(special ? DuelSound.SpecialSummon : DuelSound.Summon, Player.gameObject);
            string extra = c.Extra == ExtraKind.Xyz ? $" Rank {c.Rank}, {fm.Materials?.Count ?? 0} materials." : c.Extra == ExtraKind.Link ? $" Link-{c.LinkRating}." : "";
            AddLog($"{verb}: {c.Name}, ATK {MonsterAtk(fm)} / DEF {MonsterDef(fm)}.{extra}");
            ShowBanner($"{verb.ToUpper()}: {c.Name}");
            if (c.Partner != PartnerKind.None) OnPartnerSummoned(fm);
        }

        /// <summary>Rebuilds a zone's card: face-down back, or face-up card + model (turned sideways in defense).</summary>
        private void RefreshMonsterVisual(int slot)
        {
            FieldMonster fm = Monsters[slot];
            if (fm == null || !Board || fm.IsAlly) return;
            if (fm.CardObject) Object.Destroy(fm.CardObject);
            fm.Statue = null;
            if (fm.FaceDown) fm.CardObject = Board.LayFaceDown(slot);
            else fm.CardObject = Board.SummonOnZone(slot, fm.Card, out fm.Statue);
            if (fm.CardObject && fm.Defense) fm.CardObject.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // ---- positions and XYZ materials

        public string CanFlip(FieldMonster fm)
        {
            string m = MainOnly(); if (m != null) return m;
            if (fm == null || !fm.FaceDown) return "Not face-down";
            if (fm.SummonedThisTurn) return "Can't Flip Summon the turn it was set";
            return null;
        }

        public string CanChangePosition(FieldMonster fm)
        {
            string m = MainOnly(); if (m != null) return m;
            if (fm == null || fm.IsAlly) return "Allies stay in attack position";
            if (fm.FaceDown) return "Flip Summon it first";
            if (fm.SummonedThisTurn) return "Can't change position the turn it was summoned";
            if (fm.PositionChanged) return "Already changed position this turn";
            if (fm.HasAttacked) return "It attacked this turn";
            if (!fm.Defense && fm.Card.Extra == ExtraKind.Link) return "Link monsters can't be in defense position";
            return null;
        }

        public string CanDetach(FieldMonster fm)
        {
            string m = MainOnly(); if (m != null) return m;
            if (fm == null || fm.Materials == null || fm.Materials.Count == 0) return "No materials";
            if (fm.AllThisTurn) return "Already active this turn";
            if (fm.HasAttacked) return "It attacked this turn";
            return null;
        }

        public void FlipSummon(int slot)
        {
            FieldMonster fm = slot >= 0 && slot < Zones ? Monsters[slot] : null;
            if (Refuse(CanFlip(fm))) return;
            fm.FaceDown = false;
            fm.Defense = false;
            fm.PositionChanged = true;
            RefreshMonsterVisual(slot);
            DuelFx.FlipIn(fm.CardObject);
            if (fm.Statue) DuelTween.From(fm.Statue, Vector3.zero, 0.01f, Vector3.zero, 0.4f, 0.2f, true);
            DuelSfx.Play(DuelSound.Flip, Player.gameObject);
            AddLog($"Flip Summon: {fm.Card.Name}, ATK {MonsterAtk(fm)} / DEF {MonsterDef(fm)}.");
            ShowBanner($"FLIP SUMMON: {fm.Card.Name}");
            if (Board) Pop(Board.MonsterPos(slot), "FLIP!", null, new Color(1f, 0.85f, 0.35f), false);
            if (fm.Card.Partner != PartnerKind.None) OnPartnerSummoned(fm);
        }

        public void ChangePosition(int slot)
        {
            FieldMonster fm = slot >= 0 && slot < Zones ? Monsters[slot] : null;
            if (Refuse(CanChangePosition(fm))) return;
            float fromYaw = fm.Defense ? 90f : 0f;
            fm.Defense = !fm.Defense;
            fm.PositionChanged = true;
            RefreshMonsterVisual(slot);
            DuelFx.Turn(fm.CardObject, fromYaw);
            DuelSfx.Play(DuelSound.Position, Player.gameObject);
            AddLog($"{fm.Card.Name} switches to {(fm.Defense ? $"defense position (DEF {MonsterDef(fm)})" : $"attack position (ATK {MonsterAtk(fm)})")}.");
            if (Board) Pop(Board.MonsterPos(slot), fm.Defense ? "DEFENSE" : "ATTACK", null, new Color(0.8f, 0.9f, 1f), false);
        }

        public void DetachMaterial(int slot)
        {
            FieldMonster fm = slot >= 0 && slot < Zones ? Monsters[slot] : null;
            if (Refuse(CanDetach(fm))) return;
            Card mat = fm.Materials[fm.Materials.Count - 1];
            fm.Materials.RemoveAt(fm.Materials.Count - 1);
            Graveyard.Add(mat);
            fm.AllThisTurn = true;
            AddLog($"{fm.Card.Name} detaches {mat.Name}: this turn it attacks every enemy monster.");
            DuelSfx.Play(DuelSound.Activate, Player.gameObject);
            if (Board) { Pop(Board.MonsterPos(slot), "DETACH", "attacks every monster", new Color(0.8f, 0.8f, 1f), false); DuelFx.Shockwave(Board.transform, Board.MonsterPos(slot), new Color(0.7f, 0.7f, 1f), 3f); }
        }

        // ------------------------------------------------------------ spells

        private void ResolveSpell(Card c, Representative chosen)
        {
            SetEcho(c);
            if (c.Type == CardType.Trap) Step("trap echo", () => DuelEcho.Bind(RealEnemies(), new Color(0.8f, 0.35f, 1f)));
            switch (c.Effect)
            {
                case CardEffect.DamageOne:
                {
                    int pts = c.SpellPts;
                    if (!EnemyMonstersOnField) { BurnEnemy(pts, c.Name); break; }
                    Representative target = chosen != null && Targetable(chosen) ? chosen : CurrentTarget();
                    if (target == null) { AddLog($"{c.Name} has no target left."); break; }
                    Tracer(Player.corePosition, target.BoardPosition + Vector3.up * 2f);
                    WeakenRep(target, pts, c.Name, c.Item);
                    break;
                }
                case CardEffect.DamageAll:
                {
                    int pts = c.SpellPts;
                    if (!EnemyMonstersOnField) { BurnEnemy(pts, c.Name); break; }
                    foreach (Representative r in Reps.ToArray())
                        if (r.ZoneIndex >= 0 && !r.Defeated) { Tracer(Player.corePosition, r.BoardPosition + Vector3.up * 2f); WeakenRep(r, pts, c.Name, c.Item); }
                    break;
                }
                case CardEffect.Heal:
                {
                    Step("heal echo", () => DuelEcho.HealTeam(Player, Mathf.Clamp(c.Power * 0.5f, 0.05f, 0.4f)));
                    if (_healBlockedRound == Round)
                    {
                        AddLog($"{c.Name}: Malachite poison stops you gaining LP this turn.");
                        Pop(Player.corePosition + Vector3.up * 1.5f, "NO LP GAIN", "Malachite poison", new Color(0.4f, 0.9f, 0.5f), true);
                        break;
                    }
                    HealthComponent hc = Player.healthComponent;
                    int lpBefore = PlayerLP;
                    float before = hc.health;
                    float healed;
                    if (NetworkServer.active)
                    {
                        DuelRegen.Allowed(() => hc.HealFraction(c.Power, default(ProcChainMask)));
                        healed = hc.health - before;
                    }
                    else
                    {
                        DuelNet.HealFraction(Player, c.Power);
                        healed = Mathf.Min(hc.fullHealth * c.Power, hc.fullHealth - before);
                    }
                    int gain = Mathf.Max(0, DuelStats.LifePoints(healed, hc.fullCombinedHealth));
                    AddLog($"{c.Name}: you gain {gain} LP ({lpBefore} → {Mathf.Min(DuelStats.StartLP, lpBefore + gain)}).");
                    Pop(Player.corePosition + Vector3.up * 1.5f, $"+{gain} LP", c.Name, new Color(0.4f, 1f, 0.4f), true);
                    break;
                }
                case CardEffect.Draw:
                {
                    int n = Mathf.RoundToInt(c.Power);
                    Draw(n);
                    DuelSfx.Play(DuelSound.Draw, Player.gameObject);
                    AddLog($"{c.Name}: drew {n}.");
                    break;
                }
            }
        }

        /// <summary>A damage spell on a monster: it loses ATK and DEF until the End Phase; 0 DEF destroys it.</summary>
        private void WeakenRep(Representative r, int pts, string source, ItemIndex item)
        {
            r.AtkMod -= pts;
            r.DefMod -= pts;
            r.LastHitTime = Time.time;
            if (Board) DuelFx.Shockwave(Board.transform, r.BoardPosition, SpellColor, 3f);
            if (r.Def <= 0)
            {
                AddLog($"{source}: {r.Label} loses {pts} ATK/DEF, its DEF hits 0 and it's destroyed!");
                DestroyRep(r, source, item);
            }
            else
            {
                AddLog($"{source}: {r.Label} loses {pts} ATK and DEF until the End Phase (now {r.Atk}/{r.Def}).");
                Pop(r.BoardPosition + Vector3.up * 4f, $"-{pts}", "ATK / DEF", SpellColor, true);
                DuelSfx.Play(DuelSound.Impact, Player.gameObject);
            }
        }

        // ------------------------------------------------------------ battle

        public string CanAttack(FieldMonster fm)
        {
            if (!IsPlayerPhase) return "Not your turn";
            if (ChainBusy) return "A chain is resolving";
            if (CurrentPhase != Phase.Battle)
                return Round == 1 && DuelConfig.FirstTurnNoBattle.Value ? "No Battle Phase on the first turn" : "Attacks happen in the Battle Phase";
            if (fm == null) return "No monster";
            if (fm.FaceDown) return "Face-down: Flip Summon it first";
            if (fm.Defense) return "In defense position";
            if (fm.HasAttacked) return "Already attacked this turn";
            if (fm.FrozenRound == Round) return "Frozen (Glacial)";
            return null;
        }

        /// <summary>Declares an attack. The enemy may chain; the damage step happens after the chain.</summary>
        public void AttackWith(int slot)
        {
            if (slot < 0 || slot >= Zones) return;
            FieldMonster fm = Monsters[slot];
            if (fm == null) return;
            string why = CanAttack(fm);
            if (why != null) { if (Board) Pop(Board.MonsterPos(slot), why, null, new Color(1f, 0.6f, 0.6f), false); return; }

            BattleInfo b = new BattleInfo { Mine = true, Attacker = fm };
            if (!EnemyMonstersOnField) b.Direct = true;
            else if (fm.Card.HitsAll || fm.AllThisTurn) b.Targets = Reps.FindAll(r => r.ZoneIndex >= 0 && !r.Defeated && !Cloaked(r));
            else
            {
                b.Rep = CurrentTarget();
                if (b.Rep == null) { Refuse("No target you can attack"); return; }
            }
            b.Crit = DuelConfig.CritInBattle.Value && RollCrit();
            fm.HasAttacked = true;

            Vector3 at = b.Direct ? (Board ? Board.CommanderPos : Player.corePosition) : (b.Rep != null ? b.Rep.BoardPosition : Board.World(new Vector3(0f, 0f, 15f)));
            AimLunge(fm, at);
            fm.LungeTime = Time.time;
            AttackTracers(fm, at);
            DuelSfx.Play(DuelSound.Attack, Player.gameObject);
            string what = b.Direct ? "attacks directly!" : b.Targets != null ? "attacks every enemy monster!" : $"attacks {b.Rep.Label}!";
            ShowBanner($"{fm.Title} {what}" + (b.Crit ? "  CRITICAL!" : ""));
            AddLog($"{fm.Title} (ATK {MonsterAtk(fm)}) {what}");

            _battle = b;
            StartChain(ChainEvent.PlayerAttack, null, () => { ResolvePlayerAttack(b); _battle = null; });
        }

        private void ResolvePlayerAttack(BattleInfo b)
        {
            FieldMonster fm = b.Attacker;
            if (b.Negated) { AddLog($"{fm.Title}'s attack is negated."); return; }
            if (fm == null || Monsters[fm.Slot] != fm) return;   // it left the field during the chain
            int atk = MonsterAtk(fm) * (b.Crit ? 2 : 1);
            if (b.Crit) Pop(Board ? Board.MonsterPos(fm.Slot) + Vector3.up * 3f : Player.corePosition, "CRITICAL!", $"ATK {atk}", new Color(1f, 0.35f, 0.25f), true);

            if (b.Direct || !EnemyMonstersOnField)
            {
                AddLog($"Direct attack: {atk} damage to the enemy's LP.");
                DuelSfx.Play(DuelSound.DirectHit, Player.gameObject);
                if (Board) DuelFx.Shockwave(Board.transform, Board.CommanderPos, new Color(1f, 0.9f, 0.4f), 6f);
                DamageEnemyLP(atk, fm.Title);
                return;
            }
            List<Representative> targets = b.Targets ?? new List<Representative> { b.Rep };
            foreach (Representative r in targets)
            {
                if (Monsters[fm.Slot] != fm) break;                // it was destroyed in an earlier battle
                if (r == null || r.Defeated || r.ZoneIndex < 0) continue;
                PlayerBattle(fm, r, atk);
            }
        }

        /// <summary>The damage step when your monster attacks one of theirs.</summary>
        // How hits from the current attack/spell look out in the real world (see DuelEcho).
        private EchoKind _echoKind = EchoKind.Spirit;
        private Color _echoColor = DuelEcho.Gold;

        private void SetEcho(Card c, bool ally = false)
        {
            _echoKind = ally ? EchoKind.Spirit : DuelEcho.KindOf(c, out _echoColor);
            if (ally) _echoColor = DuelEcho.Gold;
        }

        /// <summary>Every real monster still standing in this duel (for world echoes).</summary>
        private List<CharacterBody> RealEnemies()
        {
            List<CharacterBody> list = new List<CharacterBody>();
            foreach (Representative r in Reps)
                if (!r.Defeated) foreach (DuelMember m in r.Members) if (m.Alive && m.Body) list.Add(m.Body);
            return list;
        }

        /// <summary>An enemy monster is defeated: pay its bounty (gold now, items when the duel ends).</summary>
        private void PayBounty(Representative rep)
        {
            if (!DuelConfig.Bounties.Value || rep == null) return;
            DuelRewards.RollBounty(rep.Level, rep.Rarity, out uint gold, out int w, out int g, out int r);
            gold = (uint)Mathf.RoundToInt(gold * Mathf.Max(0f, DuelConfig.BountyGold.Value));
            if (gold > 0 && Player.master) DuelNet.GiveMoney(Player.master, gold);
            BountyGold += gold;
            BountyWhite += w; BountyGreen += g; BountyRed += r;
            string rar = DuelRewards.RarityName(rep.Rarity);
            string loot = (w + g + r) > 0 ? $" + {(r > 0 ? $"{r} red " : "")}{(g > 0 ? $"{g} green " : "")}{(w > 0 ? $"{w} white " : "")}item{(w + g + r > 1 ? "s" : "")}" : "";
            Pop(rep.BoardPosition + Vector3.up * 6.2f, $"+${gold}{loot}", $"Lv {rep.Level} · {rar} bounty", DuelRewards.RarityColor(rep.Rarity), rep.Rarity >= Rarity.SuperRare);
            AddLog($"Bounty for {rep.Label} (Lv {rep.Level}, {rar}): ${gold}{loot}.");
            if (r + g > 0) DuelSfx.Play(DuelSound.Activate, Player.gameObject);
        }

        private void PlayerBattle(FieldMonster fm, Representative r, int atk)
        {
            SetEcho(fm.Card, fm.IsAlly);
            r.LastHitTime = Time.time;
            DuelSfx.Play(DuelSound.Impact, Player.gameObject);
            if (Board) DuelFx.Shockwave(Board.transform, r.BoardPosition, new Color(1f, 0.85f, 0.4f), 3.5f);
            string me = $"{fm.Title} ({atk})";
            if (!r.DefensePos)
            {
                int e = r.Atk;
                if (atk > e)
                {
                    AddLog($"{me} vs {r.Label} (ATK {e}): {r.Label} is destroyed, {atk - e} damage to the enemy.");
                    DestroyRep(r, fm.Title, fm.IsAlly ? ItemIndex.None : fm.Card.Item);
                    OnDestroyedByBattle(fm);
                    DamageEnemyLP(atk - e, "battle damage");
                }
                else if (atk < e)
                {
                    AddLog($"{me} vs {r.Label} (ATK {e}): your monster is destroyed, you take {e - atk}.");
                    DestroyMine(fm.Slot, r.Label);
                    DamagePlayerLP(e - atk, r, "battle damage");
                }
                else
                {
                    AddLog($"{me} vs {r.Label} (ATK {e}): equal ATK, both are destroyed.");
                    if (atk > 0) { DestroyRep(r, fm.Title, ItemIndex.None); DestroyMine(fm.Slot, r.Label); }
                }
            }
            else
            {
                int d = r.Def;
                if (atk > d)
                {
                    AddLog($"{me} vs {r.Label} (DEF {d}): {r.Label} is destroyed. Defense position takes no damage.");
                    DestroyRep(r, fm.Title, fm.IsAlly ? ItemIndex.None : fm.Card.Item);
                    OnDestroyedByBattle(fm);
                }
                else if (atk < d)
                {
                    AddLog($"{me} vs {r.Label} (DEF {d}): it holds, and you take {d - atk}.");
                    Pop(r.BoardPosition + Vector3.up * 4f, "BLOCKED", $"DEF {d}", new Color(0.7f, 0.85f, 1f), true);
                    DamagePlayerLP(d - atk, r, "battle damage");
                }
                else
                {
                    AddLog($"{me} vs {r.Label} (DEF {d}): a standoff, nothing is destroyed.");
                    Pop(r.BoardPosition + Vector3.up * 4f, "STANDOFF", $"DEF {d}", new Color(0.8f, 0.8f, 0.9f), true);
                }
            }
            // Glacial: whatever battles it and survives is frozen
            if (r.Elite == DuelElite.Glacial && Monsters[fm.Slot] == fm)
            {
                fm.FrozenRound = Round + 1;
                if (Board) ElitePop(r, Board.MonsterPos(fm.Slot), "FROZEN", "can't attack next turn");
                AddLog($"{fm.Title} is frozen by {r.Label}: it can't attack next turn.");
            }
        }

        private void OnDestroyedByBattle(FieldMonster fm)
        {
            if (fm.IsAlly || fm.Card.Partner != PartnerKind.HollowIdol || Monsters[fm.Slot] != fm) return;
            fm.AtkBonus += 0.25f;
            PartnerFx(fm, "SOUL ABSORB", $"+25% ATK (now {MonsterAtk(fm)})");
            AddLog($"Hollow Idol absorbs a soul: ATK is now {MonsterAtk(fm)}.");
        }

        /// <summary>Points a piece's lunge at its target (board-local offset, about half the way there).</summary>
        private void AimLunge(FieldMonster fm, Vector3 targetWorld)
        {
            if (!Board || fm == null) return;
            Vector3 d = Board.transform.InverseTransformPoint(targetWorld) - DuelBoard.MonsterLocal(fm.Slot);
            d.y = 0f;
            fm.LungeOffset = Vector3.ClampMagnitude(d * 0.5f, 6f);
        }

        private void AimLunge(Representative r, Vector3 targetWorld)
        {
            if (!Board || r == null) return;
            Vector3 from = r.IsCommander ? DuelBoard.CommanderLocal : DuelBoard.EnemyMonsterLocal(Mathf.Max(0, r.ZoneIndex));
            Vector3 d = Board.transform.InverseTransformPoint(targetWorld) - from;
            d.y = 0f;
            r.LungeOffset = Vector3.ClampMagnitude(d * 0.5f, 6f);
        }

        private void AttackTracers(FieldMonster fm, Vector3 end)
        {
            end += Vector3.up * 2f;
            if (fm.IsAlly)
            {
                foreach (CharacterBody b in fm.Allies)
                    if (b && b.healthComponent && b.healthComponent.alive) Tracer(b.corePosition, end);
            }
            else if (Board) Tracer(Board.MonsterPos(fm.Slot) + Vector3.up * 1.2f, end);
        }

        private static void Tracer(Vector3 from, Vector3 to)
        {
            if (!SkillAssets.Tracer) return;
            try { EffectManager.SpawnEffect(SkillAssets.Tracer, new EffectData { origin = to, start = from }, true); }
            catch { }
        }

        /// <summary>What would happen if this monster attacked that one (for tooltips).</summary>
        public string PredictBattle(FieldMonster fm, Representative r)
        {
            if (fm == null) return "";
            int atk = MonsterAtk(fm);
            if (r == null || !EnemyMonstersOnField) return $"Direct attack: <b>{atk}</b> damage to the enemy's LP ({EnemyLP} → {Mathf.Max(0, EnemyLP - atk)}).";
            if (!r.DefensePos)
            {
                int e = r.Atk;
                if (atk > e) return $"vs {r.Label} <color=#ff9a7a>ATK {e}</color>: <color=#80ff90>destroy it</color> and deal <b>{atk - e}</b> to their LP.";
                if (atk < e) return $"vs {r.Label} <color=#ff9a7a>ATK {e}</color>: <color=#ff8080>your monster is destroyed</color>, you take <b>{e - atk}</b>.";
                return $"vs {r.Label} <color=#ff9a7a>ATK {e}</color>: equal, <color=#ffcc80>both destroyed</color>.";
            }
            int d = r.Def;
            if (atk > d) return $"vs {r.Label} <color=#a0c8ff>DEF {d}</color>: <color=#80ff90>destroy it</color> (no damage through defense).";
            if (atk < d) return $"vs {r.Label} <color=#a0c8ff>DEF {d}</color>: it holds, <color=#ff8080>you take {d - atk}</color>.";
            return $"vs {r.Label} <color=#a0c8ff>DEF {d}</color>: standoff, nothing happens.";
        }

        // ------------------------------------------------------------ life points and destruction

        /// <summary>Battle or effect damage to you, in LP (8000 = your full health).</summary>
        private void DamagePlayerLP(int pts, Representative source, string what)
        {
            if (pts <= 0) return;
            HealthComponent hc = Player.healthComponent;
            float real = DuelStats.RealFromLP(pts, hc.fullCombinedHealth);
            GameObject attacker = null;
            if (source != null) { DuelMember m = source.RandomAliveMember(); attacker = m != null && m.Body ? m.Body.gameObject : null; }
            DamageInfo info = new DamageInfo
            {
                attacker = attacker,
                inflictor = attacker,
                damage = real,
                crit = false,
                position = Player.corePosition,
                force = Vector3.zero,
                procCoefficient = 0f,
                damageColorIndex = DuelVenom.NumberColor,
                procChainMask = default(ProcChainMask)
            };
            if (NetworkServer.active) { hc.TakeDamage(info); DuelVenom.Mark(hc); }
            else DuelNet.HurtBody(Player, real, attacker);
            if (_hitVfx) EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = Player.corePosition, scale = 2f }, true);
            if (Board) DuelFx.Shockwave(Board.transform, Player.footPosition, new Color(1f, 0.3f, 0.3f), 4f);
            Pop(Player.corePosition + Vector3.up * 1.5f, $"-{pts} LP", what, new Color(1f, 0.3f, 0.3f), true);
            DuelSfx.Play(DuelSound.DirectHit, Player.gameObject);
            if (source != null && source.Elite == DuelElite.Gilded && Player.master && what == "battle damage")
            {
                uint money = Player.master.money;
                uint steal = (uint)Mathf.RoundToInt(real * 0.5f);
                if (steal > money) steal = money;
                DuelNet.TakeMoney(Player.master, steal);
                ElitePop(source, Player.corePosition, $"-${steal}", "Gilded steals your gold");
                AddLog($"{source.Label} steals ${steal}.");
            }
        }

        /// <summary>Your monster is destroyed: shatter, then graveyard (or banished / allies sent home).</summary>
        private void DestroyMine(int slot, string by)
        {
            FieldMonster fm = slot >= 0 && slot < Zones ? Monsters[slot] : null;
            if (fm == null) return;
            fm.LastHitTime = Time.time;
            if (Board) Pop(Board.MonsterPos(slot) + Vector3.up * 2f, "DESTROYED", fm.Title, new Color(1f, 0.3f, 0.3f), true);
            DuelSfx.Play(DuelSound.Destroy, Player.gameObject);
            RemoveMonster(slot, true);
        }

        /// <summary>An enemy monster is destroyed: the real monster on the map dies with it.</summary>
        private void DestroyRep(Representative rep, string source, ItemIndex item)
        {
            if (rep == null || rep.Destroyed || rep.IsCommander) return;
            DuelMember first = rep.Members.Find(x => x.Alive && x.Body);
            Vector3 soulFrom = first != null ? first.Body.corePosition : Vector3.zero;
            if (first != null) Step("soul harvest", () => DuelEcho.SoulHarvest(Player, soulFrom));
            float most = 0f;
            foreach (DuelMember m in rep.Members) if (m.Alive) most = Mathf.Max(most, m.Health.combinedHealth);
            HitSpecies(rep, most * 10f + 100f, source, false, item, true);
            rep.Destroyed = true;
            rep.LastHitTime = Time.time;
            Pop(rep.BoardPosition + Vector3.up * 4.4f, "DESTROYED", rep.Label, new Color(1f, 0.45f, 0.3f), true);
            DuelSfx.Play(DuelSound.Destroy, Player.gameObject);
            if (_hitVfx) EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = rep.BoardPosition + Vector3.up * 2f, scale = 3f }, true);
            Step("bounty", () => PayBounty(rep));
        }

        /// <summary>Damage to the enemy's LP: the commander's real health (8000 LP = full), or the Director's.</summary>
        private void DamageEnemyLP(int pts, string source)
        {
            if (pts <= 0) return;
            Vector3 at = Board ? Board.CommanderPos + Vector3.up * 3f : Player.corePosition;
            if (Commander != null && !Commander.Defeated)
            {
                int before = EnemyLP;
                float real = DuelStats.RealFromLP(pts, Commander.FullHealth);
                EchoKind was = _echoKind;
                _echoKind = EchoKind.Judgment;
                HitSpecies(Commander, real, source, false, ItemIndex.None, true);
                _echoKind = was;
                Commander.LastHitTime = Time.time;
                Pop(at + Vector3.up * 2f, $"-{pts} LP", Commander.Name, new Color(1f, 0.9f, 0.4f), true);
                AddLog($"The enemy takes {pts} ({before} → {Mathf.Max(0, before - pts)} LP).");
            }
            else if (Commander == null && DirectorLifeMax > 0f)
            {
                DirectorLife = Mathf.Max(0f, DirectorLife - pts);
                Pop(at + Vector3.up * 2f, $"-{pts} LP", "The Director", new Color(1f, 0.9f, 0.4f), true);
                AddLog($"The Director takes {pts} ({DirectorLife:0} LP left).");
            }
            if (Board) DuelFx.Shockwave(Board.transform, Board.CommanderPos, new Color(1f, 0.85f, 0.35f), 5f);
            if (_hitVfx) EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = at, scale = 4f }, true);
            DuelSfx.Play(DuelSound.DirectHit, Player.gameObject);
        }

        /// <summary>Burn: a damage spell with no monsters to hit goes straight to their LP.</summary>
        private void BurnEnemy(int pts, string source)
        {
            AddLog($"{source}: no enemy monsters, so {pts} damage goes straight to their LP.");
            Tracer(Player.corePosition, Board ? Board.CommanderPos + Vector3.up * 3f : Player.corePosition);
            DamageEnemyLP(pts, source);
        }

        private void HealEnemyLP(int pts, string source)
        {
            if (pts <= 0) return;
            if (Commander != null && !Commander.Defeated)
            {
                float real = DuelStats.RealFromLP(pts, Commander.FullHealth);
                foreach (DuelMember m in Commander.Members) if (m.Alive) DuelNet.Heal(m.Health, real);
            }
            else if (Commander == null && DirectorLifeMax > 0f) DirectorLife = Mathf.Min(DirectorLifeMax, DirectorLife + pts);
            else return;
            Vector3 at = Board ? Board.CommanderPos + Vector3.up * 5f : Player.corePosition;
            Pop(at, $"+{pts} LP", source, new Color(0.45f, 1f, 0.45f), true);
            AddLog($"{source}: the enemy gains {pts} LP.");
        }

        // ------------------------------------------------------------ real damage to monsters on the map

        private HitResult HitSpecies(Representative rep, float perUnit, string source, bool crit, ItemIndex item = ItemIndex.None, bool quiet = false)
        {
            HitResult r = new HitResult { MinEach = float.MaxValue };
            int vfx = 0;
            int rain = 0;
            uint hitGold = 0;
            int rainMax = DuelConfig.CardRain.Value && CardRain.EffectPrefab ? Mathf.Max(0, DuelConfig.RainPerHit.Value) : 0;

            foreach (DuelMember m in rep.Members)
            {
                if (!m.Alive) continue;
                float d = perUnit * m.Weight * rep.DefenseFactor;
                if (d <= 0f) continue;

                DamageInfo info = new DamageInfo
                {
                    attacker = Player.gameObject,
                    inflictor = Player.gameObject,
                    damage = d,
                    crit = crit,
                    position = m.Body ? m.Body.corePosition : m.Health.transform.position,
                    force = Vector3.zero,
                    procCoefficient = 0f,
                    damageColorIndex = DuelVenom.NumberColor,
                    procChainMask = default(ProcChainMask)
                };
                DeathRewards rewards = m.Body ? m.Body.GetComponent<DeathRewards>() : null;
                uint worth = rewards ? rewards.goldReward : 0u;
                bool killed;
                if (NetworkServer.active) { m.Health.TakeDamage(info); killed = !m.Health.alive; if (!killed) DuelVenom.Mark(m.Health); }
                else
                {
                    // playing as a client: ask the host to deal it (it pays the kill bonus too); predict the kill locally
                    killed = d >= m.Health.combinedHealth;
                    DuelNet.DamageEnemy(Player, m.Health, d, crit, Mathf.Max(0f, DuelConfig.KillGoldBonus.Value));
                }
                r.Total += d;
                r.Hits++;
                r.MaxEach = Mathf.Max(r.MaxEach, d);
                r.MinEach = Mathf.Min(r.MinEach, d);
                if (killed)
                {
                    r.Kills++;
                    KillCount++;
                    BaseGold += worth;
                    uint bonus = (uint)Mathf.RoundToInt(worth * Mathf.Max(0f, DuelConfig.KillGoldBonus.Value));
                    if (bonus > 0 && Player.master) { if (NetworkServer.active) Player.master.GiveMoney(bonus); BonusGold += bonus; hitGold += bonus; }
                }

                // The hit lands on the real monster out in the world.
                if (r.Hits <= 6) Step("world echo", () => DuelEcho.Strike(info.position, _echoKind, _echoColor, 1f + (killed ? 0.4f : 0f) + (crit ? 0.3f : 0f), killed));

                // What your friends see on the map: a card falls on each unit you hit (up to the cap).
                if (rain < rainMax) { rain++; CardRain.Drop(m.Body ? m.Body.footPosition : info.position, item); }
                else if (rainMax == 0 && vfx < 6 && _hitVfx)
                {
                    vfx++;
                    EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = info.position, scale = 1.5f }, true);
                }
            }
            if (r.Hits == 0) r.MinEach = 0f;

            rep.LastLoss = r.Total;
            rep.LastHitTime = Time.time;
            rep.Refresh();
            if (quiet)
            {
                if (hitGold > 0) Pop(rep.BoardPosition + Vector3.up * 5.4f, $"+${hitGold}", "duel bonus", new Color(1f, 0.85f, 0.3f), false);
                return r;
            }

            Vector3 p = rep.BoardPosition + Vector3.up * 2f;
            if (_hitVfx) EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = p, scale = 3f }, true);
            WorldNumbers.Spawn(r.Total, p, crit);
            AddLog($"{source} → {rep.Name}: {r.Total:N0} damage" + (r.Kills > 0 ? $", <color=#ff8866>{r.Kills} destroyed</color>" : "") + ".");
            return r;
        }

        private bool RollCrit() => Util.CheckRoll(Player.crit, Player.master);

        // ------------------------------------------------------------ enemy battle phase

        /// <summary>The enemy AI picks what to attack: something it beats, or you directly when your field is empty.</summary>
        private int ChooseEnemyTarget(Representative rep, out bool hold)
        {
            hold = false;
            int a = rep.Atk;
            List<int> slots = new List<int>();
            for (int i = 0; i < Zones; i++) if (Monsters[i] != null && !Monsters[i].Dead) slots.Add(i);
            if (slots.Count == 0) return -1;
            FieldMonster jinx = FaceUpPartner(PartnerKind.Jinx);
            if (jinx != null) slots = new List<int> { jinx.Slot };   // Mind Taunt

            int best = -1;
            float bestScore = 0f;
            foreach (int s in slots)
            {
                FieldMonster fm = Monsters[s];
                float score;
                if (fm.FaceDown) score = a >= 1500 ? 300f : 0f;
                else if (!fm.Defense)
                {
                    int m = MonsterAtk(fm);
                    score = a > m ? 1000f + (a - m) + m * 0.5f : 0f;
                }
                else
                {
                    int d = MonsterDef(fm);
                    score = a > d ? 600f + d * 0.3f + (rep.Elite == DuelElite.Voidtouched ? a - d : 0) : 0f;
                }
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best < 0) hold = true;
            return best;
        }

        private void EnemyAction(Representative rep)
        {
            if (rep.Defeated || !rep.OnField || rep.DefensePos) return;
            if (rep.ZoneIndex < 0 && !rep.IsCommander) return;

            int slot = ChooseEnemyTarget(rep, out bool hold);
            if (hold)
            {
                AddLog($"{rep.Label} (ATK {rep.Atk}) holds back: it can't win a battle on your field.");
                Pop(rep.BoardPosition + Vector3.up * 3.5f, "HOLDS", "no good attack", new Color(0.8f, 0.8f, 0.9f), false);
                return;
            }
            string targetName = slot >= 0 ? (Monsters[slot].FaceDown ? "your face-down monster" : Monsters[slot].Title) : "you directly";
            Vector3 targetPos = slot >= 0 && Board ? Board.MonsterPos(slot) : Player.corePosition;

            AimLunge(rep, targetPos);
            rep.LungeTime = Time.time;
            Tracer(rep.BoardPosition + Vector3.up * 2f, targetPos + Vector3.up);
            DuelSfx.Play(DuelSound.Attack, Player.gameObject);
            ShowBanner($"{rep.Label} (ATK {rep.Atk}) attacks {targetName}!");
            AddLog($"{rep.Label} (ATK {rep.Atk}) declares an attack on {targetName}.");

            FieldMonster puff = _featherWardUsed ? null : FaceUpPartner(PartnerKind.Featherpuff);
            if (puff != null)
            {
                _featherWardUsed = true;
                PartnerFx(puff, "FEATHER WARD", $"{rep.Label}'s attack is negated");
                AddLog($"Featherpuff's Feather Ward negates {rep.Label}'s attack.");
                Pop(targetPos + Vector3.up * 1.5f, "NEGATED", "Feather Ward", new Color(1f, 0.95f, 0.85f), true);
                return;
            }

            BattleInfo b = new BattleInfo { Mine = false, Rep = rep, Slot = slot, Direct = slot < 0 };
            _battle = b;
            StartChain(ChainEvent.EnemyAttack, null, () => { ResolveEnemyAttack(b); _battle = null; });
        }

        private void ResolveEnemyAttack(BattleInfo b)
        {
            Representative rep = b.Rep;
            if (b.Negated) { AddLog($"{rep.Label}'s attack is negated."); return; }
            if (rep.Defeated || !rep.OnField) return;
            int a = rep.Atk;

            if (b.Direct)
            {
                if (MonsterCountAll > 0) { AddLog($"{rep.Label}'s attack fizzles: the field changed."); return; }
                AddLog($"{rep.Label} hits you directly for {a}.");
                DamagePlayerLP(a, rep, "battle damage");
                AfterEnemyAttack(rep, -1, false);
                return;
            }

            FieldMonster fm = Monsters[b.Slot];
            if (fm == null) { AddLog($"{rep.Label}'s target is gone; the attack ends."); return; }
            fm.LastHitTime = Time.time;
            DuelSfx.Play(DuelSound.Impact, Player.gameObject);
            if (Board) DuelFx.Shockwave(Board.transform, Board.MonsterPos(b.Slot), new Color(1f, 0.4f, 0.35f), 3.5f);
            if (fm.FaceDown)
            {
                fm.FaceDown = false; // attacking a set monster flips it face-up (still in defense)
                RefreshMonsterVisual(b.Slot);
                DuelFx.FlipIn(fm.CardObject);
                DuelSfx.Play(DuelSound.Flip, Player.gameObject);
                AddLog($"The face-down card is flipped: {fm.Card.Name} (DEF {MonsterDef(fm)}).");
                if (Board) Pop(Board.MonsterPos(b.Slot) + Vector3.up * 3f, "FLIPPED", fm.Card.Name, new Color(1f, 0.85f, 0.5f), false);
            }

            bool destroyedMine = false;
            if (!fm.Defense)
            {
                int m = MonsterAtk(fm);
                if (a > m)
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (ATK {m}): your monster is destroyed, you take {a - m}.");
                    DestroyMine(b.Slot, rep.Label);
                    destroyedMine = true;
                    DamagePlayerLP(a - m, rep, "battle damage");
                }
                else if (a < m)
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (ATK {m}): {fm.Title} wins" + (rep.IsCommander ? "" : $", {rep.Label} is destroyed") + $", the enemy takes {m - a}.");
                    if (!rep.IsCommander) { DestroyRep(rep, fm.Title, ItemIndex.None); OnDestroyedByBattle(fm); }
                    DamageEnemyLP(m - a, "battle damage");
                }
                else
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (ATK {m}): equal, both are destroyed.");
                    if (a > 0)
                    {
                        if (!rep.IsCommander) DestroyRep(rep, fm.Title, ItemIndex.None);
                        DestroyMine(b.Slot, rep.Label);
                        destroyedMine = true;
                    }
                }
            }
            else
            {
                int d = MonsterDef(fm);
                if (a > d)
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (DEF {d}): your monster is destroyed." + (rep.Elite == DuelElite.Voidtouched ? $" Piercing: you take {a - d}." : ""));
                    DestroyMine(b.Slot, rep.Label);
                    destroyedMine = true;
                    if (rep.Elite == DuelElite.Voidtouched) DamagePlayerLP(a - d, rep, "piercing damage");
                }
                else if (a < d)
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (DEF {d}): your wall holds, the enemy takes {d - a}.");
                    if (Board) Pop(Board.MonsterPos(b.Slot) + Vector3.up * 3f, "BLOCKED", $"DEF {d}", new Color(0.7f, 0.85f, 1f), true);
                    DamageEnemyLP(d - a, "battle damage");
                }
                else
                {
                    AddLog($"{rep.Label} ({a}) vs {fm.Title} (DEF {d}): a standoff.");
                    if (Board) Pop(Board.MonsterPos(b.Slot) + Vector3.up * 3f, "STANDOFF", $"DEF {d}", new Color(0.8f, 0.8f, 0.9f), true);
                }
            }
            if (!destroyedMine && rep.Elite == DuelElite.Glacial && Monsters[b.Slot] == fm)
            {
                fm.FrozenRound = Round + 1;
                if (Board) ElitePop(rep, Board.MonsterPos(b.Slot), "FROZEN", "can't attack next turn");
                AddLog($"{fm.Title} is frozen solid and can't attack next turn.");
            }
            AfterEnemyAttack(rep, b.Slot, destroyedMine);
        }

        /// <summary>Elite effects that follow an enemy attack.</summary>
        private void AfterEnemyAttack(Representative rep, int slot, bool destroyedMine)
        {
            switch (rep.Elite)
            {
                case DuelElite.Blazing:
                    _playerBurn += 300;
                    ElitePop(rep, Player.corePosition, "BURNING", "300 in your Standby Phase");
                    break;
                case DuelElite.Overloading:
                    AddLog($"{rep.Label}'s lightning arcs to you.");
                    DamagePlayerLP(400, rep, "Overloading");
                    break;
                case DuelElite.Malachite:
                    _healBlockedRound = Round + 1;
                    AddLog("Malachite poison: you can't gain LP in your next turn.");
                    ElitePop(rep, Player.corePosition, "POISONED", "no LP gain next turn");
                    break;
                case DuelElite.Twisted:
                    if (destroyedMine && MonsterCountAll > 0)
                    {
                        int dmg = 300 * MonsterCountAll;
                        AddLog($"Twisted: {MonsterCountAll} monster(s) on your field, you take {dmg}.");
                        DamagePlayerLP(dmg, rep, "Twisted");
                    }
                    break;
            }
        }

        /// <summary>Enemy End Phase: Mending elites restore their LP.</summary>
        private void EliteEnemyEnd()
        {
            foreach (Representative healer in Reps)
            {
                if (healer.Elite != DuelElite.Mending || healer.Defeated || !healer.OnField) continue;
                HealEnemyLP(500, healer.Label + " (Mending)");
                if (Board) ElitePop(healer, healer.BoardPosition, "MENDING", "+500 LP");
            }
        }

        private void ClearBack(int i)
        {
            Backrow[i] = null;
            if (Board) Board.SetBackCard(i, false);
        }

        /// <summary>
        /// Takes a monster off the field. It goes to the graveyard, or is banished if it had been revived.
        /// toGrave = false keeps the card out of both (it's becoming an XYZ material). Its own XYZ materials
        /// go to the graveyard with it. Ally cards send their allies back to the map.
        /// </summary>
        private void RemoveMonster(int slot, bool destroyed, bool toGrave = true)
        {
            FieldMonster fm = Monsters[slot];
            if (fm == null) return;
            bool gloopReform = destroyed && toGrave && !fm.IsAlly && fm.Card.Partner == PartnerKind.Gloop && !_gloopReformUsed;
            if (gloopReform)
            {
                _gloopReformUsed = true;
                _gloopPending = fm.Card;
                toGrave = false;                         // it isn't gone: it re-forms next turn
                PartnerFx(fm, "REFORM", "it will re-form in your next Standby Phase");
                AddLog("Gloop splatters... and starts to re-form. It returns in your next Standby Phase.");
            }
            bool jinxEncore = destroyed && !fm.IsAlly && fm.Card.Partner == PartnerKind.Jinx;
            if (fm.CardObject)
            {
                if (toGrave || destroyed || gloopReform) DuelFx.Shatter(fm.CardObject, fm.IsAlly ? AllyFrame : fm.Card.Frame, 1.6f);
                else Object.Destroy(fm.CardObject);
            }
            fm.CardObject = null;
            fm.Statue = null;
            if (destroyed && _hitVfx && Board) EffectManager.SpawnEffect(_hitVfx, new EffectData { origin = Board.MonsterPos(slot) + Vector3.up, scale = 3f }, true);
            if (fm.IsAlly)
            {
                if (destroyed && !fm.Dead)
                {
                    AddLog($"Your {fm.Card.Name} card is destroyed: those allies are knocked out of the duel and return to the map.");
                    ReleaseAllies(fm);
                }
            }
            else
            {
                if (fm.Materials != null) Graveyard.AddRange(fm.Materials);
                if (toGrave)
                {
                    if (fm.Revived)
                    {
                        Banished.Add(fm.Card);
                        AddLog($"{fm.Card.Name} is banished.");
                        if (Board) Pop(Board.MonsterPos(slot), "BANISHED", fm.Card.Name, new Color(0.75f, 0.75f, 0.85f), false);
                    }
                    else Graveyard.Add(fm.Card);
                }
            }
            Monsters[slot] = null;

            if (jinxEncore)
            {
                Card best = null;
                foreach (Card g in Graveyard)
                    if (g.Type == CardType.Monster && g.Partner != PartnerKind.Jinx && (best == null || g.AtkPts > best.AtkPts)) best = g;
                if (best != null)
                {
                    Graveyard.Remove(best);
                    ShowBanner("JINX: ENCORE!");
                    AddLog($"Jinx's Encore: {best.Name} returns from the graveyard.");
                    PlaceMonster(best, "Encore Summon", false, false, true, null);
                }
                else AddLog("Jinx's Encore: no monster in the graveyard to bring back.");
            }
        }

        public void Retreat()
        {
            if (Finished || _finishTimer >= 0f) return;
            SetResult(Outcome.Defeat, "You surrendered and left the duel.", 2.5f);
        }

        /// <summary>Ends the duel with a result; the result screen shows while the timer runs.</summary>
        private void SetResult(Outcome outcome, string reason, float showFor = 4f)
        {
            if (Result != Outcome.None) return;
            Result = outcome;
            ResultReason = reason;
            if (outcome == Outcome.Victory)
            {
                VictoryGold = (uint)Mathf.RoundToInt(BonusGold * Mathf.Max(0f, DuelConfig.VictoryGoldBonus.Value));
                JackpotGold = DuelRewards.Jackpot();
                if (VictoryGold + JackpotGold > 0 && Player.master) DuelNet.GiveMoney(Player.master, VictoryGold + JackpotGold);
                BossBeaten = Commander == null;      // beating the Director itself counts as a boss
                if (Commander != null) foreach (DuelMember m in Commander.Members) if (m.Body && m.Body.isChampion) BossBeaten = true;
                LootLegendary = DuelRewards.Legendaries(BossBeaten);
                LootUncommon = DuelRewards.Uncommons;
            }
            DuelSfx.Play(outcome == Outcome.Victory ? DuelSound.Victory : DuelSound.Defeat, Player.gameObject);
            AddLog((outcome == Outcome.Victory ? "VICTORY. " : "DEFEAT. ") + reason + $" {KillCount} destroyed, ${BaseGold + BonusGold + VictoryGold + JackpotGold} gold.");
            ShowBanner(outcome == Outcome.Victory ? "VICTORY!" : "DEFEAT");
            _finishTimer = showFor;
        }

        // ------------------------------------------------------------ cards

        private void Draw(int count, bool canDeckOut = true)
        {
            for (int i = 0; i < count && Hand.Count < DuelConfig.MaxHandSize.Value; i++)
            {
                if (DrawPile.Count == 0)
                {
                    if (DuelConfig.DeckOut.Value)
                    {
                        // Yu-Gi-Oh! rules: you must draw and you can't: you lose.
                        if (canDeckOut)
                        {
                            AddLog("<color=#ff6666>You have to draw, but your deck is empty. DECKED OUT!</color>");
                            ShowBanner("DECKED OUT!");
                            SetResult(Outcome.Defeat, "You had no cards left to draw: decked out!");
                        }
                        return;
                    }
                    if (Graveyard.Count == 0) return;
                    DrawPile.AddRange(Graveyard);
                    Graveyard.Clear();
                    Shuffle(DrawPile);
                    AddLog("Graveyard shuffled back into the deck.");
                }
                Hand.Add(DrawPile[DrawPile.Count - 1]);
                DrawPile.RemoveAt(DrawPile.Count - 1);
            }
        }

        private static int FreeSlot<T>(T[] zones) where T : class
        {
            for (int i = 0; i < zones.Length; i++) if (zones[i] == null) return i;
            return -1;
        }

        private static void Shuffle(List<Card> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Card tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // ------------------------------------------------------------ feedback

        public void Pop(Vector3 world, string text, string sub, Color color, bool big)
        {
            Popups.Add(new Popup { World = world, Text = text, Sub = sub, Color = color, Big = big, Born = Time.unscaledTime });
        }

        public void PopScreen(string text, Color color)
        {
            Popups.Add(new Popup { OnScreen = true, Text = text, Color = color, Born = Time.unscaledTime });
        }

        private void ShowBanner(string text)
        {
            Banner = text;
            BannerTime = Time.unscaledTime;
        }

        public void AddLog(string line)
        {
            Log.Add(line);
            if (Log.Count > 80) Log.RemoveAt(0);
            DuelistPlugin.Log.LogInfo("[Duel] " + System.Text.RegularExpressions.Regex.Replace(line, "<.*?>", ""));
        }

        // ------------------------------------------------------------ teardown

        public void End()
        {
            CurrentPhase = Phase.Finished;
            Step("restore camera", Camera.Detach);
            if (Result == Outcome.None) { Result = Outcome.Defeat; ResultReason = "The duel ended early."; }
            try
            {
                uint total = BaseGold + BonusGold + VictoryGold + JackpotGold + BountyGold;
                string head = Result == Outcome.Victory ? "<color=#f2c640>DUEL WON!</color>" : "<color=#ff6666>DUEL LOST.</color>";
                Chat.AddMessage($"{head} {ResultReason} {KillCount} enemies destroyed, <color=#f2c640>${total}</color> gold earned (${BonusGold + VictoryGold + JackpotGold + BountyGold} duel bonus)."
                    + (Result == Outcome.Victory ? $" <color=#c77dff>Victory spoils: {LootLegendary} legendary + {LootUncommon} uncommon item{(LootUncommon == 1 ? "" : "s")}" + (DuelConfig.VictoryLevelUp.Value ? ", LEVEL UP" : "") + "!</color>" : ""));
            }
            catch { }

            // Send surviving allies back where they were and wake their AI up.
            Step("return allies", () =>
            {
                foreach (AllyHome h in _allies)
                {
                    if (h.AI != null) foreach (BaseAI ai in h.AI) if (ai) ai.enabled = true;
                    if (h.Body && h.Body.healthComponent && h.Body.healthComponent.alive)
                    {
                        Teleport(h.Body, h.Position);
                        if (NetworkServer.active) h.Body.AddTimedBuff(RoR2Content.Buffs.HiddenInvincibility, 1.5f);
                    }
                }
                _allies.Clear();
            });

            if (Board) Object.Destroy(Board.gameObject);
            Board = null;

            if (Player && Player.healthComponent && Player.healthComponent.alive)
            {
                Teleport(Player, _returnPosition);
                DuelNet.Invincible(Player, 1.5f);
                if (_bigVfx) EffectManager.SpawnEffect(_bigVfx, new EffectData { origin = _returnPosition + Vector3.up, scale = 6f }, true);
                if (BountyWhite + BountyGreen + BountyRed > 0)
                    Step("bounty items", () => DuelNet.BountyLoot(Player, BountyWhite, BountyGreen, BountyRed));
                if (Result == Outcome.Victory)
                {
                    Step("victory spoils", () => DuelNet.VictoryLoot(Player, BossBeaten));
                    if (_bigVfx) for (int i = 0; i < 3; i++) EffectManager.SpawnEffect(_bigVfx, new EffectData { origin = _returnPosition + Vector3.up * (2f + i * 2.5f), scale = 9f + i * 3f, color = new Color(1f, 0.85f, 0.35f) }, true);
                    Util.PlaySound("Play_UI_item_pickup", Player.gameObject);
                }
            }
        }

        private static void Teleport(CharacterBody body, Vector3 foot)
        {
            if (!body) return;
            try
            {
                TeleportHelper.TeleportBody(body, foot);
            }
            catch (Exception e)
            {
                DuelistPlugin.Log.LogWarning("TeleportBody failed, moving directly: " + e.Message);
                if (body.characterMotor) body.characterMotor.Motor.SetPosition(foot + Vector3.up);
                else body.transform.position = foot;
            }
            if (body.characterMotor) body.characterMotor.velocity = Vector3.zero;
            if (body.rigidbody) body.rigidbody.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// The game's floating damage numbers, spawned over the arena statues so hits are visible in the
    /// world too. Resolved by reflection because the method's parameters vary between versions.
    /// </summary>
    public static class WorldNumbers
    {
        private static bool _resolved;
        private static MethodInfo _spawn;
        private static Func<object> _instance;

        public static void Spawn(float amount, Vector3 pos, bool crit)
        {
            try
            {
                if (!_resolved)
                {
                    _resolved = true;
                    Type t = typeof(CharacterBody).Assembly.GetType("RoR2.DamageNumberManager");
                    if (t != null)
                    {
                        BindingFlags sf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                        PropertyInfo ip = t.GetProperty("instance", sf);
                        FieldInfo inf = ip == null ? t.GetField("instance", sf) : null;
                        if (ip != null) _instance = () => ip.GetValue(null);
                        else if (inf != null) _instance = () => inf.GetValue(null);

                        foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public))
                        {
                            if (m.Name != "SpawnDamageNumber") continue;
                            ParameterInfo[] ps = m.GetParameters();
                            if (ps.Length >= 2 && ps[0].ParameterType == typeof(float) && ps[1].ParameterType == typeof(Vector3)) { _spawn = m; break; }
                        }
                    }
                    DuelistPlugin.Log.LogInfo("Damage numbers over statues: " + (_spawn != null && _instance != null ? "on" : "unavailable"));
                }
                if (_spawn == null || _instance == null) return;
                object inst = _instance();
                if (inst == null) return;

                ParameterInfo[] p = _spawn.GetParameters();
                object[] args = new object[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    Type pt = p[i].ParameterType;
                    if (i == 0) args[i] = amount;
                    else if (pt == typeof(Vector3)) args[i] = pos;
                    else if (pt == typeof(bool)) args[i] = crit;
                    else if (pt == typeof(TeamIndex)) args[i] = TeamIndex.Monster;
                    else if (pt == typeof(DamageColorIndex)) args[i] = DamageColorIndex.Default;
                    else if (p[i].HasDefaultValue) args[i] = p[i].DefaultValue;
                    else args[i] = pt.IsValueType ? Activator.CreateInstance(pt) : null;
                }
                _spawn.Invoke(inst, args);
            }
            catch { }
        }
    }
}
