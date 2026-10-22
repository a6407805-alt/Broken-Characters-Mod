using System;
using System.Collections.Generic;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace CarMaxSpecialist
{
    public enum Deadline : byte { None, Kills, Elite, Damage, Purchase, NoHit, Scan }

    /// <summary>
    /// His shift. The host runs it (pay, stress, deadlines, coworkers) and sends the numbers to everyone for the
    /// timecard; stress is also a buff stack so the damage bonus works everywhere.
    /// </summary>
    public class CmxShift : MonoBehaviour
    {
        // synced to clients
        public float Stress, Hours, Progress, Target, TimeLeft, NextIn;
        public bool Overtime;
        public Deadline Kind;
        public int Coworkers, OnBreak;
        public int OpenTasks; // host: task windows open on the owner's screen
        public bool AuditActive; // host: the owner's screen is in a management audit
        private float _maxTime;

        // local only
        public int EmailCount;
        public float LastEmail;
        private readonly int[] _step = new int[2];
        private readonly float[] _stepT = new float[2];

        /// <summary>Which hit of the 4-hit combo is next (light = M1, heavy = M2); starts over after a pause.</summary>
        public int NextStep(bool heavy)
        {
            int h = heavy ? 1 : 0;
            float now = Time.time;
            if (now - _stepT[h] > (heavy ? 2.2f : 1.3f)) _step[h] = 0;
            int s = _step[h];
            _step[h] = (s + 1) % 4;
            _stepT[h] = now + (s == 3 ? 0.4f : 0f);
            return s;
        }

        private CharacterBody _body;
        private int _hoursDone;
        private float _clock, _lastHurt = -99f, _syncTimer, _lastYell = -99f, _lastPhone = -99f, _lastBreakLine;
        private float _deadlineStart;
        private readonly List<CharacterMaster> _crew = new List<CharacterMaster>();
        private readonly HashSet<CharacterMaster> _lazy = new HashSet<CharacterMaster>();
        private float _crewEnd;
        private bool _started;

        public CharacterBody Body => _body;

        private void Awake() { _body = GetComponent<CharacterBody>(); }

        private void Start()
        {
            NextIn = 25f;
        }

        // ------------------------------------------------------------ host loop

        private void FixedUpdate()
        {
            if (!NetworkServer.active || !_body || !_body.healthComponent || !_body.healthComponent.alive) return;
            float dt = Time.fixedDeltaTime;
            if (!_started)
            {
                _started = true;
                CmxNet.Say(_body, gameObject, Lines.Pick(Lines.ClockIn), Style.Memo, "clock");
            }

            // The clock: paid by the hour, no matter what.
            float hour = Mathf.Max(1f, CmxPlugin.HourSeconds.Value);
            _clock += dt;
            while (_clock >= hour) { _clock -= hour; PayHour(); }
            Hours = _hoursDone + _clock / hour;

            // Stress: calms down when nobody's yelling, except on overtime, which only grinds it up.
            if (Overtime) AddStress(0.4f * dt);
            else if (Time.time - _lastHurt > 6f) AddStress(-CmxPlugin.StressDecay.Value * dt);

            if (OpenTasks > 0) AddStress(OpenTasks * CmxPlugin.TaskNag.Value * dt);
            if (CmxPlugin.CombatDeadlines.Value || Kind != Deadline.None) UpdateDeadline(dt);
            UpdateCrew();

            int stacks = Mathf.Clamp(Mathf.RoundToInt(Stress), 0, 100);
            if (_body.GetBuffCount(CmxPlugin.StressBuff) != stacks) _body.SetBuffCount(CmxPlugin.StressBuff.buffIndex, stacks);
            if (AuditActive) { Stress = 100f; _maxTime = 0f; }
            else if (Stress >= 100f)
            {
                // Give the owner's screen a moment to report the audit before it falls back to Burnout.
                _maxTime += dt;
                if (!CmxPlugin.AuditLockdown.Value || !CmxTasks.Enabled || _maxTime > 2.5f) { _maxTime = 0f; Burnout(); }
            }
            else _maxTime = 0f;

            _syncTimer -= dt;
            if (_syncTimer <= 0f) { _syncTimer = 0.2f; CmxNet.SendState(this); }
        }

        private int Scaled(float gold)
        {
            int g = Mathf.Max(1, Mathf.RoundToInt(gold));
            return Run.instance ? Run.instance.GetDifficultyScaledCost(g) : g;
        }

        private void Pay(int amount)
        {
            if (amount > 0 && _body.master) _body.master.GiveMoney((uint)amount);
        }

        private void PayHour()
        {
            _hoursDone++;
            int pay = Scaled(CmxPlugin.PayPerHour.Value * (Overtime ? CmxPlugin.OvertimePay.Value : 1f));
            Pay(pay);
            AddStress(Overtime ? 6f : 2f);
            CmxNet.Say(_body, gameObject, "<color=#7CFF7C>+$" + pay + "</color>  <size=80%>hour " + _hoursDone + (Overtime ? " (OT)" : "") + "</size>", Style.Small, "cash");

            if (!Overtime && _hoursDone >= Mathf.RoundToInt(CmxPlugin.OvertimeAfter.Value))
            {
                Overtime = true;
                _body.AddBuff(CmxPlugin.OvertimeBuff);
                AddStress(10f);
                CmxNet.Say(_body, gameObject, Lines.Pick(Lines.OvertimeStart), Style.Memo, "printer");
            }
            else if (Random.value < 0.35f)
                CmxNet.Say(_body, gameObject, Lines.Pick(Overtime ? Lines.OvertimeGrind : Lines.Corporate), Style.Bubble, null);
        }

        public void AddStress(float amount)
        {
            Stress = Mathf.Clamp(Stress + amount, 0f, 100f);
        }

        private void Burnout()
        {
            Stress = 45f;
            CmxNet.Say(_body, gameObject, "<color=#FF5050>BURNOUT</color>\n<size=70%>" + Lines.Pick(Lines.Burnout) + "</size>", Style.Banner, "slam", burnout: true);

            switch (Random.Range(0, 6))
            {
                case 0: _body.AddTimedBuff(RoR2Content.Buffs.Cripple, 6f); Tell("Pulled a muscle lifting a transmission. (Cripple)"); break;
                case 1: _body.AddTimedBuff(RoR2Content.Buffs.Slow50, 6f); Tell("Running on 3 hours of sleep. (Slow)"); break;
                case 2: _body.AddTimedBuff(RoR2Content.Buffs.HealingDisabled, 6f); Tell("No time for lunch. (No healing)"); break;
                case 3: _body.AddTimedBuff(CmxPlugin.WriteUpBuff, 10f); Tell("Written up for attitude. (-25% damage)"); break;
                case 4:
                    if (_body.master && _body.master.money > 0)
                    {
                        uint lost = _body.master.money / 3u;
                        _body.master.money -= lost;
                        Tell("Paycheck garnished: -$" + lost + ".");
                    }
                    else _body.AddTimedBuff(RoR2Content.Buffs.Slow50, 6f);
                    break;
                default: _body.AddTimedBuff(RoR2Content.Buffs.Slow80, 4f); Tell("Mandatory HR training. (Slowed)"); break;
            }
        }

        private void Tell(string text) => CmxNet.Say(_body, gameObject, text, Style.Bubble, null);

        // ------------------------------------------------------------ deadlines

        private void UpdateDeadline(float dt)
        {
            if (Kind == Deadline.None)
            {
                NextIn -= dt;
                if (NextIn <= 0f) StartDeadline();
                return;
            }
            TimeLeft -= dt;
            if (Kind == Deadline.NoHit) Progress = Mathf.Min(Target, Progress + dt);
            if (Progress >= Target) { MeetDeadline(); return; }
            if (TimeLeft <= 0f) MissDeadline();
        }

        private static bool AnyEliteAlive()
        {
            foreach (CharacterBody b in CharacterBody.readOnlyInstancesList)
                if (b && b.isElite && b.teamComponent && b.teamComponent.teamIndex == TeamIndex.Monster && b.healthComponent && b.healthComponent.alive) return true;
            return false;
        }

        private void StartDeadline()
        {
            List<Deadline> pool = new List<Deadline> { Deadline.Kills, Deadline.Damage, Deadline.NoHit, Deadline.Scan, Deadline.Kills };
            if (AnyEliteAlive()) { pool.Add(Deadline.Elite); pool.Add(Deadline.Elite); }
            if (_body.master && _body.master.money >= (uint)Scaled(25f)) pool.Add(Deadline.Purchase);
            Kind = pool[Random.Range(0, pool.Count)];
            Progress = 0f;
            int stage = Run.instance ? Run.instance.stageClearCount : 0;
            switch (Kind)
            {
                case Deadline.Kills: Target = 6 + Mathf.Min(6, stage); TimeLeft = 40f; break;
                case Deadline.Elite: Target = 1; TimeLeft = 45f; break;
                case Deadline.Damage: Target = Mathf.Round(_body.damage * 25f / 10f) * 10f; TimeLeft = 35f; break;
                case Deadline.Purchase: Target = 1; TimeLeft = 50f; break;
                case Deadline.NoHit: Target = 15f; TimeLeft = 16f; break;
                case Deadline.Scan: Target = 6; TimeLeft = 30f; break;
            }
            _deadlineStart = Time.time;
            CmxNet.Say(_body, gameObject, "<color=#FFD040>NEW DEADLINE</color>\n<size=70%>" + Describe(Kind, Target) + "</size>", Style.Banner, "memo");
        }

        public static string Describe(Deadline k, float target)
        {
            switch (k)
            {
                case Deadline.Kills: return "Reconcile inventory: move " + target.ToString("0") + " units (kills)";
                case Deadline.Elite: return "Customer escalation: deal with an elite customer";
                case Deadline.Damage: return "Month-end close: process " + target.ToString("0") + " in damage";
                case Deadline.Purchase: return "Approve a PO: open a chest or buy something";
                case Deadline.NoHit: return "Customer satisfaction survey: don't get yelled at for 15s";
                case Deadline.Scan: return "Cycle count: scan " + target.ToString("0") + " units with Inventory Audit";
            }
            return "";
        }

        private void MeetDeadline()
        {
            Kind = Deadline.None;
            NextIn = Random.Range(CmxPlugin.DeadlineMin.Value, CmxPlugin.DeadlineMax.Value);
            AddStress(-CmxPlugin.DeadlineStressRelief.Value);
            int bonus = Scaled(CmxPlugin.PayPerHour.Value * 2.5f);
            Pay(bonus);
            _body.AddTimedBuff(CmxPlugin.PizzaBuff, 8f);

            float r = Random.value;
            List<PickupIndex> list = null;
            if (Run.instance)
            {
                if (r < 0.12f) list = Run.instance.availableTier2DropList;
                else if (r < 0.5f) list = Run.instance.availableTier1DropList;
            }
            if (list != null && list.Count > 0)
            {
                PickupIndex p = Run.instance.treasureRng.NextElementUniform(list);
                try { PickupDropletController.CreatePickupDroplet(p, _body.corePosition + Vector3.up * 1.5f, Vector3.up * 15f + _body.gameObject.transform.forward * 4f); }
                catch (Exception e) { CmxPlugin.Log.LogWarning("Deadline reward drop failed: " + e.Message); }
            }
            CmxNet.Say(_body, gameObject, "<color=#7CFF7C>DEADLINE MET</color>\n<size=70%>" + Lines.Pick(Lines.Praise) + "  +$" + bonus + "</size>", Style.Banner, "pizza");
        }

        private void MissDeadline()
        {
            Kind = Deadline.None;
            NextIn = Random.Range(CmxPlugin.DeadlineMin.Value, CmxPlugin.DeadlineMax.Value);
            AddStress(CmxPlugin.DeadlineFailStress.Value);
            if (Random.value < 0.5f) _body.AddTimedBuff(CmxPlugin.WriteUpBuff, 10f);
            CmxNet.Say(_body, gameObject, "<color=#FF6060>DEADLINE MISSED</color>\n<size=70%>" + Lines.Pick(Lines.Scold) + "</size>", Style.Banner, "fail");
        }

        // ------------------------------------------------------------ events (host)

        public void OnHurt(DamageReport r)
        {
            float full = Mathf.Max(1f, _body.healthComponent.fullCombinedHealth);
            AddStress(Mathf.Clamp(r.damageDealt / full * 80f, 1.5f, 20f) * CmxPlugin.StressPerHit.Value);
            _lastHurt = Time.time;
            if (Kind == Deadline.NoHit) Progress = 0f;

            if (Time.time - _lastYell > 3f && Random.value < 0.4f)
            {
                _lastYell = Time.time;
                GameObject at = r.attackerBody ? r.attackerBody.gameObject : gameObject;
                CmxNet.Say(_body, at, Lines.Pick(Lines.Customers), Style.Customer, Time.time - _lastPhone > 8f ? "phone" : null);
                if (Time.time - _lastPhone > 8f) _lastPhone = Time.time;
            }
        }

        public void OnKill(DamageReport r)
        {
            if (Kind == Deadline.Kills) Progress += 1f;
            if (Kind == Deadline.Elite && r.victimBody && (r.victimBody.isElite || r.victimBody.isBoss)) Progress += 1f;
        }

        public void OnDamage(float amount)
        {
            if (Kind == Deadline.Damage) Progress += amount;
        }

        public void OnPurchase()
        {
            if (Kind == Deadline.Purchase) Progress += 1f;
        }

        public void SetAudit(byte state)
        {
            bool was = AuditActive;
            AuditActive = state == 1;
            if (state == 2 && was)
            {
                Stress = 45f;
                CmxNet.Say(_body, gameObject, "<color=#FFC928>AUDIT PASSED</color>\n<size=70%>Backlog cleared. Back to work.</size>", Style.Banner, null);
            }
        }

        public void ServerTaskDone(float relief = -1f)
        {
            AddStress(-(relief >= 0f ? relief : CmxPlugin.TaskRelief.Value));
            if (CmxPlugin.CompletionReward.Value)
            {
                _body.AddTimedBuff(RoR2Content.Buffs.Energized, 3f);
                if (_body.healthComponent) _body.healthComponent.AddBarrier(_body.maxHealth * 0.10f);
            }
            int gold = Scaled(CmxPlugin.PayPerHour.Value * 0.6f);
            Pay(gold);
            CmxNet.Say(_body, gameObject, "<color=#FFC928>Task closed +$" + gold + "</color>", Style.Small, null);
        }

        public void OnScanned(int count)
        {
            if (Kind == Deadline.Scan) Progress += count;
        }

        public void OnUnitSold(CharacterBody victim)
        {
            int gold = Scaled(2f + (victim && victim.isElite ? 4f : 0f));
            Pay(gold);
            Vector3 at = victim ? victim.corePosition : _body.corePosition;
            CmxNet.Say(_body, null, "<color=#FFD040>UNIT SOLD</color> <color=#7CFF7C>+$" + gold + "</color>", Style.Small, "cash", pos: at);
        }

        // ------------------------------------------------------------ coworkers (host)

        public void ServerCoworkers()
        {
            Clean();
            if (_crew.Count > 0)
            {
                foreach (CharacterMaster m in _crew) if (m) { try { m.TrueKill(); } catch { } }
                _crew.Clear();
                _lazy.Clear();
                _body.AddTimedBuff(CmxPlugin.DiyBuff, 8f);
                AddStress(CmxPlugin.DiyStress.Value);
                CmxNet.Say(_body, gameObject, Lines.Pick(Lines.Diy), Style.Bubble, "sigh");
                return;
            }

            Layoffs();
            GameObject prefab = Coworker.Prefab();
            if (!prefab) { CmxNet.Say(_body, gameObject, "Nobody's answering the phone.", Style.Bubble, null); return; }
            Vector3 right = Vector3.Cross(Vector3.up, _body.gameObject.transform.forward).normalized;
            List<string> names = new List<string>(Lines.Names);
            for (int i = 0; i < 2; i++)
            {
                Vector3 spot = _body.footPosition + right * (i == 0 ? -2.5f : 2.5f) + Vector3.up * 1.2f;
                try
                {
                    CharacterMaster m = new MasterSummon
                    {
                        masterPrefab = prefab,
                        position = spot,
                        rotation = _body.gameObject.transform.rotation,
                        summonerBodyObject = _body.gameObject,
                        ignoreTeamMemberLimit = true,
                        useAmbientLevel = true
                    }.Perform();
                    if (!m) continue;
                    _crew.Add(m);
                    int n = Random.Range(0, names.Count);
                    string name = names[n];
                    names.RemoveAt(n);
                    m.gameObject.AddComponent<Coworker>().Name = name;
                    bool lazy = Random.value < CmxPlugin.LazyChance.Value;
                    if (lazy)
                    {
                        _lazy.Add(m);
                        foreach (RoR2.CharacterAI.BaseAI ai in m.GetComponents<RoR2.CharacterAI.BaseAI>()) ai.enabled = false;
                        AddStress(6f);
                    }
                    CharacterBody cb = m.GetBody();
                    if (cb) CmxNet.Say(_body, cb.gameObject, "<b>" + name + ":</b> " + Lines.Pick(lazy ? Lines.Lazy : Lines.Helpful), lazy ? Style.Customer : Style.Bubble, null);
                }
                catch (Exception e) { CmxPlugin.Log.LogWarning("Coworker summon failed: " + e.Message); }
            }
            _crewEnd = Time.time + CmxPlugin.CoworkerSeconds.Value;
            _lastBreakLine = Time.time;
            CmxNet.Say(_body, gameObject, Lines.Pick(Lines.CallIn), Style.Bubble, "phone");
        }

        /// <summary>The coworkers clear out the floor: every enemy nearby is let go (bosses just take a big hit).</summary>
        private void Layoffs()
        {
            if (!_body.teamComponent) return;
            BullseyeSearch s = new BullseyeSearch
            {
                searchOrigin = _body.corePosition,
                searchDirection = Vector3.forward,
                maxDistanceFilter = CmxPlugin.LayoffRange.Value,
                maxAngleFilter = 180f,
                teamMaskFilter = TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Distance,
                filterByLoS = false
            };
            s.RefreshCandidates();
            s.FilterOutGameObject(gameObject);
            HashSet<HealthComponent> done = new HashSet<HealthComponent>();
            int count = 0;
            foreach (HurtBox hb in s.GetResults())
            {
                HealthComponent hc = hb ? hb.healthComponent : null;
                if (!hc || !hc.alive || !done.Add(hc)) continue;
                CharacterBody v = hc.body;
                bool boss = v && v.isChampion;
                hc.TakeDamage(new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = boss ? _body.damage * CmxPlugin.LayoffDamage.Value : hc.fullCombinedHealth + 1f,
                    crit = false,
                    position = hb.transform.position,
                    force = Vector3.zero,
                    procCoefficient = 0f,
                    damageType = boss ? DamageType.Generic : (DamageType.BypassArmor | DamageType.BypassBlock),
                    damageColorIndex = DamageColorIndex.Item
                });
                if (count < 6 && v) CmxNet.Say(_body, v.gameObject, "<color=#FFC928>LAID OFF</color>", Style.Small, null);
                if (++count >= 60) break;
            }
        }

        private void Clean()
        {
            _crew.RemoveAll(m => !m || !m.hasBody && m.IsDeadAndOutOfLivesServer());
            _lazy.RemoveWhere(m => !m || !_crew.Contains(m));
        }

        private void UpdateCrew()
        {
            if (_crew.Count == 0) { Coworkers = OnBreak = 0; return; }
            Clean();
            if (Time.time >= _crewEnd)
            {
                foreach (CharacterMaster m in _crew)
                {
                    if (!m) continue;
                    CharacterBody cb = m.GetBody();
                    if (cb) CmxNet.Say(_body, null, "<b>" + (m.GetComponent<Coworker>()?.Name ?? "Coworker") + ":</b> " + Lines.Pick(Lines.ClockOut), Style.Small, null, pos: cb.corePosition);
                    try { m.TrueKill(); } catch { }
                }
                _crew.Clear();
                _lazy.Clear();
            }
            else if (_lazy.Count > 0 && Time.time - _lastBreakLine > 4.5f)
            {
                _lastBreakLine = Time.time;
                AddStress(2f);
                foreach (CharacterMaster m in _lazy)
                {
                    CharacterBody cb = m ? m.GetBody() : null;
                    if (cb) { CmxNet.Say(_body, cb.gameObject, "<b>" + (m.GetComponent<Coworker>()?.Name ?? "Coworker") + ":</b> " + Lines.Pick(Lines.Lazy), Style.Customer, null); break; }
                }
            }
            Coworkers = _crew.Count;
            OnBreak = _lazy.Count;
        }

        // ------------------------------------------------------------ audit (host)

        public void ServerAudit(Ray aim)
        {
            if (!_body.teamComponent) return;
            BullseyeSearch s = new BullseyeSearch
            {
                searchOrigin = aim.origin,
                searchDirection = aim.direction,
                maxDistanceFilter = CmxPlugin.AuditRange.Value,
                maxAngleFilter = 32f,
                teamMaskFilter = TeamMask.GetEnemyTeams(_body.teamComponent.teamIndex),
                sortMode = BullseyeSearch.SortMode.Distance,
                filterByLoS = true
            };
            s.RefreshCandidates();
            s.FilterOutGameObject(gameObject);
            HashSet<HealthComponent> done = new HashSet<HealthComponent>();
            int count = 0;
            bool crit = Util.CheckRoll(_body.crit, _body.master);
            foreach (HurtBox hb in s.GetResults())
            {
                HealthComponent h = hb ? hb.healthComponent : null;
                if (!h || !h.alive || !done.Add(h)) continue;
                CharacterBody v = h.body;
                if (v)
                {
                    v.AddTimedBuff(CmxPlugin.InventoryBuff, CmxPlugin.AuditSeconds.Value);
                    Tagger[v] = this;
                }
                h.TakeDamage(new DamageInfo
                {
                    attacker = gameObject,
                    inflictor = gameObject,
                    damage = _body.damage * CmxPlugin.AuditDamage.Value,
                    crit = crit,
                    position = hb.transform.position,
                    force = Vector3.zero,
                    procCoefficient = 0.7f,
                    damageColorIndex = DamageColorIndex.Item
                });
                if (count < 5 && v) CmxNet.Say(_body, v.gameObject, "<color=#FFD040>STOCK #" + Random.Range(10000, 99999) + "</color>", Style.Small, null);
                count++;
                if (count >= 12) break;
            }
            if (count > 0) OnScanned(count);
        }

        // ------------------------------------------------------------ global hooks

        public static readonly Dictionary<CharacterBody, CmxShift> Tagger = new Dictionary<CharacterBody, CmxShift>();

        public static void Hook()
        {
            GlobalEventManager.onServerDamageDealt += r =>
            {
                try
                {
                    if (r == null) return;
                    CmxShift victim = r.victimBody ? r.victimBody.GetComponent<CmxShift>() : null;
                    if (victim && r.damageDealt > 0f && r.attackerBody != r.victimBody) victim.OnHurt(r);
                    CmxShift attacker = r.attackerBody ? r.attackerBody.GetComponent<CmxShift>() : null;
                    if (attacker) attacker.OnDamage(r.damageDealt);
                }
                catch (Exception e) { CmxPlugin.Log.LogWarning("Shift damage hook: " + e.Message); }
            };
            GlobalEventManager.onCharacterDeathGlobal += r =>
            {
                try
                {
                    if (r == null) return;
                    CmxShift attacker = r.attackerBody ? r.attackerBody.GetComponent<CmxShift>() : null;
                    if (attacker) attacker.OnKill(r);
                    if (r.victimBody && Tagger.TryGetValue(r.victimBody, out CmxShift tagger))
                    {
                        Tagger.Remove(r.victimBody);
                        if (tagger && r.victimBody.HasBuff(CmxPlugin.InventoryBuff)) tagger.OnUnitSold(r.victimBody);
                    }
                    if (Tagger.Count > 200)
                    {
                        List<CharacterBody> dead = new List<CharacterBody>();
                        foreach (var kv in Tagger) if (!kv.Key || !kv.Value) dead.Add(kv.Key);
                        foreach (CharacterBody b in dead) Tagger.Remove(b);
                    }
                }
                catch (Exception e) { CmxPlugin.Log.LogWarning("Shift death hook: " + e.Message); }
            };
            GlobalEventManager.OnInteractionsGlobal += (interactor, interactable, obj) =>
            {
                try
                {
                    if (!NetworkServer.active || !interactor || !obj) return;
                    CmxShift s = interactor.GetComponent<CmxShift>();
                    if (s && obj.GetComponent<PurchaseInteraction>()) s.OnPurchase();
                }
                catch (Exception e) { CmxPlugin.Log.LogWarning("Shift interaction hook: " + e.Message); }
            };
        }
    }

    /// <summary>Marks a summoned coworker (keeps its name for the speech bubbles).</summary>
    public class Coworker : MonoBehaviour
    {
        public string Name;
        private static GameObject _prefab;
        private static bool _looked;

        public static GameObject Prefab()
        {
            if (_looked) return _prefab;
            _looked = true;
            foreach (string n in new[] { "CommandoMonsterMaster", "EngiMonsterMaster", "MercMonsterMaster", "Drone1Master" })
            {
                try { _prefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/" + n); } catch { _prefab = null; }
                if (_prefab) { CmxPlugin.Log.LogInfo("Coworkers use " + n); break; }
            }
            return _prefab;
        }
    }

    // ================================================================ networking

    public enum Style : byte { Bubble, Customer, Small, Memo, Banner }

    public static class CmxNet
    {
        public static void Register()
        {
            NetworkingAPI.RegisterMessageType<CmxStateMessage>();
            NetworkingAPI.RegisterMessageType<CmxSayMessage>();
            NetworkingAPI.RegisterMessageType<CmxTaskMessage>();
            NetworkingAPI.RegisterMessageType<CmxSpeakMessage>();
        }

        public static void TaskDone(CmxShift s, float relief = -1f)
        {
            if (!s) return;
            if (NetworkServer.active) { s.ServerTaskDone(relief); return; }
            try { new CmxTaskMessage { Body = Id(s.gameObject), Done = true, Relief = relief }.Send(NetworkDestination.Server); }
            catch (Exception e) { CmxPlugin.Log.LogDebug("Task sync: " + e.Message); }
        }

        public static void TaskOpen(CmxShift s, int open, byte audit = 0)
        {
            if (!s) return;
            if (NetworkServer.active) { s.OpenTasks = open; s.SetAudit(audit); return; }
            try { new CmxTaskMessage { Body = Id(s.gameObject), Done = false, Open = (byte)Mathf.Clamp(open, 0, 255), Audit = audit }.Send(NetworkDestination.Server); }
            catch (Exception e) { CmxPlugin.Log.LogDebug("Task sync: " + e.Message); }
        }

        private static NetworkInstanceId Id(GameObject go)
        {
            NetworkIdentity ni = go ? go.GetComponent<NetworkIdentity>() : null;
            return ni ? ni.netId : NetworkInstanceId.Invalid;
        }

        public static void SendState(CmxShift s)
        {
            if (!NetworkServer.active || !s) return;
            try
            {
                new CmxStateMessage
                {
                    Body = Id(s.gameObject), Stress = s.Stress, Hours = s.Hours, Overtime = s.Overtime, Kind = (byte)s.Kind,
                    Progress = s.Progress, Target = s.Target, TimeLeft = s.TimeLeft, NextIn = s.NextIn,
                    Coworkers = (byte)s.Coworkers, OnBreak = (byte)s.OnBreak
                }.Send(NetworkDestination.Clients);
            }
            catch (Exception e) { CmxPlugin.Log.LogDebug("State sync: " + e.Message); }
        }

        /// <summary>Host: show a line for everyone (and handle it here right away).</summary>
        public static void Say(CharacterBody owner, GameObject anchor, string text, Style style, string sound, bool burnout = false, Vector3 pos = default(Vector3))
        {
            if (!NetworkServer.active) return;
            CmxSayMessage m = new CmxSayMessage
            {
                Owner = Id(owner ? owner.gameObject : null), Anchor = Id(anchor), Pos = anchor ? anchor.transform.position : pos,
                Text = text ?? "", Style = (byte)style, Sound = sound ?? "", Burnout = burnout
            };
            m.Handle();
            try { m.Send(NetworkDestination.Clients); }
            catch (Exception e) { CmxPlugin.Log.LogDebug("Say sync: " + e.Message); }
        }
    }

    public class CmxStateMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public float Stress, Hours, Progress, Target, TimeLeft, NextIn;
        public bool Overtime;
        public byte Kind, Coworkers, OnBreak;

        public void Serialize(NetworkWriter w)
        {
            w.Write(Body); w.Write(Stress); w.Write(Hours); w.Write(Progress); w.Write(Target); w.Write(TimeLeft); w.Write(NextIn);
            w.Write(Overtime); w.Write(Kind); w.Write(Coworkers); w.Write(OnBreak);
        }

        public void Deserialize(NetworkReader r)
        {
            Body = r.ReadNetworkId(); Stress = r.ReadSingle(); Hours = r.ReadSingle(); Progress = r.ReadSingle(); Target = r.ReadSingle();
            TimeLeft = r.ReadSingle(); NextIn = r.ReadSingle(); Overtime = r.ReadBoolean(); Kind = r.ReadByte(); Coworkers = r.ReadByte(); OnBreak = r.ReadByte();
        }

        public void OnReceived()
        {
            if (NetworkServer.active) return; // the host already has the real numbers
            GameObject go = Util.FindNetworkObject(Body);
            CmxShift s = go ? go.GetComponent<CmxShift>() : null;
            if (!s) return;
            s.Stress = Stress; s.Hours = Hours; s.Progress = Progress; s.Target = Target; s.TimeLeft = TimeLeft; s.NextIn = NextIn;
            s.Overtime = Overtime; s.Kind = (Deadline)Kind; s.Coworkers = Coworkers; s.OnBreak = OnBreak;
        }
    }

    public class CmxSayMessage : INetMessage
    {
        public NetworkInstanceId Owner, Anchor;
        public Vector3 Pos;
        public string Text, Sound;
        public byte Style;
        public bool Burnout;

        public void Serialize(NetworkWriter w)
        {
            w.Write(Owner); w.Write(Anchor); w.Write(Pos); w.Write(Text ?? ""); w.Write(Sound ?? ""); w.Write(Style); w.Write(Burnout);
        }

        public void Deserialize(NetworkReader r)
        {
            Owner = r.ReadNetworkId(); Anchor = r.ReadNetworkId(); Pos = r.ReadVector3(); Text = r.ReadString(); Sound = r.ReadString();
            Style = r.ReadByte(); Burnout = r.ReadBoolean();
        }

        public void OnReceived()
        {
            if (NetworkServer.active) return; // the host showed it when it was said
            Handle();
        }

        public void Handle()
        {
            try
            {
                GameObject owner = Owner == NetworkInstanceId.Invalid ? null : Util.FindNetworkObject(Owner);
                GameObject anchor = Anchor == NetworkInstanceId.Invalid ? null : Util.FindNetworkObject(Anchor);
                CharacterBody ob = owner ? owner.GetComponent<CharacterBody>() : null;
                bool mine = ob && ob.hasEffectiveAuthority;
                CarMaxSpecialist.Style st = (CarMaxSpecialist.Style)Style;

                if (st == CarMaxSpecialist.Style.Banner || st == CarMaxSpecialist.Style.Memo)
                {
                    if (mine) CmxHud.Banner(Text, st == CarMaxSpecialist.Style.Banner ? 3.5f : 2.5f);
                    else CmxHud.Bubble(anchor ? anchor.transform : null, Pos, Text, CarMaxSpecialist.Style.Bubble);
                }
                else CmxHud.Bubble(anchor ? anchor.transform : null, Pos, Text, st);

                if (mine && !string.IsNullOrEmpty(Sound)) CmxAudio.Play(Sound);
                if (mine && Burnout) CmxBurnoutState.Begin(ob);
            }
            catch (Exception e) { CmxPlugin.Log.LogDebug("Say: " + e.Message); }
        }
    }
}
