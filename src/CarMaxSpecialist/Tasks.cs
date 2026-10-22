using System;
using System.Collections.Generic;
using System.Reflection;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Random = UnityEngine.Random;

namespace CarMaxSpecialist
{
    /// <summary>
    /// Busywork: task windows pop up on his screen like ads and stay there (blocking the view and nagging stress)
    /// until he finishes their little minigame. How often they come depends on his stress:
    ///   calm (0-40)      rare, a couple at a time
    ///   busy (41-74)     frequent, they pile up on each other
    ///   overloaded (75+) bursts of several windows, the screen fills up
    ///   100              MANAGEMENT AUDIT: the screen locks down and windows keep coming until the backlog is cleared
    /// Windows: part number, RO sign-off, write the story, invoice, plus the ERO repair order (RO #29751) and the
    /// appointment scheduler (date twice, scrambled-vowel name, twitchy promise time, fake progress bar).
    /// Keyboard only, newest window takes the keys. Only the Specialist's own player sees them.
    /// </summary>
    public class CmxTasks : MonoBehaviour
    {
        private enum Kind { PartNumber, RepairOrder, Story, Invoice, Ero, Appt, Error, Inventory, PartMatch }

        private class Task
        {
            public Kind Kind;
            public Rect Rect;
            public float Born, Shake, Flash;
            public string Title, Header;
            // part number / invoice
            public string Answer = "", Typed = "";
            // repair order sign-off
            public string[] Lines;
            public int[] Arrows;
            public int Step;
            // story
            public Story Story;
            public string[] Options;
            public int Correct;
            // ERO
            public string Says, Chosen = "";
            public int NeedPay;
            // appointment
            public string[] Dates;
            public int Cursor, Picked = -1, AlphaIdx;
            public char[] Alpha;
            public string TargetName, Name = "", Msg = "";
            public float Pos = 420f, TargetMin, Hold, ErrCool, ProgT, Prog;
            public bool Released;
            public bool[] Checks = new bool[3];
            public int Qty;
            public int[] InvShelf = new int[2], InvSys = new int[2];
            public string[] InvParts = new string[2];
            public string Bin, Car, Fmt;
            public int[] Order;
            public char[] Letters = new char[3];
            public int LetterOk;
        }

        private class Story
        {
            public string Complaint, Cause, Correction;
            public string[] WrongCauses, WrongCorrections;
        }

        private static readonly Story[] Stories =
        {
            new Story { Complaint = "Brakes squeal when stopping", Cause = "Front pads worn to 2mm", Correction = "Replaced front pads, resurfaced rotors",
                WrongCauses = new[] { "Radio volume too high", "Low tire pressure" }, WrongCorrections = new[] { "Topped off washer fluid", "Rotated tires" } },
            new Story { Complaint = "Check engine light is on", Cause = "Loose gas cap (code P0457)", Correction = "Tightened gas cap, cleared codes",
                WrongCauses = new[] { "Worn wiper blades", "Dirty cabin filter" }, WrongCorrections = new[] { "Replaced wiper blades", "Washed the vehicle" } },
            new Story { Complaint = "Car pulls to the right", Cause = "Front alignment out of spec", Correction = "Performed 4-wheel alignment",
                WrongCauses = new[] { "Weak battery", "Oil is low" }, WrongCorrections = new[] { "Replaced battery", "Changed oil" } },
            new Story { Complaint = "A/C blows warm air", Cause = "Refrigerant leak at condenser", Correction = "Replaced condenser, recharged A/C",
                WrongCauses = new[] { "Headlight bulb out", "Tires worn" }, WrongCorrections = new[] { "Replaced headlight bulb", "Mounted 4 new tires" } },
            new Story { Complaint = "Clicking noise when turning", Cause = "Torn boot, worn left CV axle", Correction = "Replaced left CV axle",
                WrongCauses = new[] { "Loose floor mat", "Coolant low" }, WrongCorrections = new[] { "Secured floor mat", "Topped off coolant" } },
            new Story { Complaint = "Battery light flickers", Cause = "Alternator output low (12.1V)", Correction = "Replaced alternator, tested charging",
                WrongCauses = new[] { "Tire tread low", "Dirty air filter" }, WrongCorrections = new[] { "Rotated tires", "Replaced air filter" } },
            new Story { Complaint = "Car shakes at highway speed", Cause = "Front wheels out of balance", Correction = "Balanced all 4 wheels",
                WrongCauses = new[] { "Sunroof seal dry", "Key fob battery low" }, WrongCorrections = new[] { "Lubed sunroof seal", "Replaced key fob battery" } }
        };

        private static readonly string[] RoLines =
        {
            "Oil & filter change", "Tire rotation", "Multi-point inspection", "Cabin air filter", "Wiper blades", "Brake fluid flush",
            "Battery test", "Coolant top-off", "Road test", "Wash & vacuum", "Recall check", "Alignment check"
        };

        // ERO: what the customer says, and the shorthand the tech types for it.
        private static readonly string[] EroSays =
        {
            "Customer says the check engine light is on and it stalls at red lights.",
            "Customer says the brakes squeal every time they stop.",
            "Customer says the A/C only blows warm air.",
            "Customer says there is a vibration at highway speed.",
            "Customer says there is a clunk from the front end over bumps."
        };

        private static readonly string[] EroLines =
        {
            "CK ENG LT ON, STALLS AT IDLE. CK AND ADV.",
            "BRAKES SQUEAL ON STOPPING. INSPECT F/R.",
            "A/C BLOWS WARM. DIAGNOSE AND REPAIR.",
            "VIBRATION AT HWY SPEED. CK TIRES/BALANCE.",
            "CLUNK F/END OVER BUMPS. CK SUSPENSION."
        };

        private static readonly string[] PayCodes = { "RET", "WAR", "TWG", "NSA" };

        private static readonly string[] PayHints =
        {
            "Customer is paying out of pocket.",
            "Covered by manufacturer warranty.",
            "Dealer goodwill, no charge to customer.",
            "No-show adjustment, internal credit."
        };

        private static readonly string[] InvNames =
        {
            "Oil filter", "Brake pad set", "Spark plug", "Wiper blade", "Cabin air filter", "Serpentine belt", "Headlight bulb", "Brake rotor", "Air filter", "Battery"
        };

        private static readonly string[] CarNames =
        {
            "2019 Dodge Charger", "2021 Ram 1500", "2018 Jeep Wrangler", "2020 Ford F-150", "2017 Ford Mustang", "2019 Chevrolet Silverado",
            "2016 GMC Sierra", "2020 Toyota Camry", "2018 Honda Accord", "2019 BMW 330i", "2018 Audi A4", "2020 Volkswagen Jetta"
        };
        // which part-number style each car uses (index into MakeHints)
        private static readonly int[] CarMake = { 0, 0, 0, 1, 1, 2, 2, 3, 4, 5, 6, 6 };

        private static readonly string[] MakeHints =
        {
            "MOPAR (Dodge, Ram, Jeep, Chrysler): 8 digits, then a letter pair like AA or AB. Example 68258471AA",
            "FORD: letters and a digit, then Z, a dash, digits, a dash, a letter. Example FL3Z-6731-A",
            "GM (Chevy, GMC): 8 digits and no letters. Example 12677234",
            "TOYOTA: 5 digits, a dash, then 5 mixed characters. Example 04152-YZZA1",
            "HONDA: 5 digits, 3 letters, then A and 2 digits, all dashed. Example 15400-PLM-A02",
            "BMW: 11 digits in groups. Example 11 42 7 953 129",
            "VW / AUDI: 3 characters, then two groups of 3 digits, then a letter. Example 8V0 615 301 A"
        };

        private static string Dig(int n) { string r = ""; for (int i = 0; i < n; i++) r += Random.Range(0, 10); return r; }
        private static string Let(int n) { string r = ""; for (int i = 0; i < n; i++) r += (char)('A' + Random.Range(0, 26)); return r; }
        private static string Pick(string set) { return set[Random.Range(0, set.Length)].ToString(); }

        private static string PartNo(int make)
        {
            switch (make)
            {
                case 0: return Dig(8) + "A" + Pick("ABCD");
                case 1: return Pick("EFGH") + Pick("LOPTU") + Random.Range(1, 9) + "Z-" + Dig(Random.Range(4, 6)) + "-" + Let(1);
                case 2: return Dig(8);
                case 3: return Dig(5) + "-" + Pick("0123456789") + Let(1) + Dig(2) + Pick("0123456789ABCDEFGH");
                case 4: return Dig(5) + "-" + Let(3) + "-A" + Dig(2);
                case 5: return Dig(2) + " " + Dig(2) + " " + Dig(1) + " " + Dig(3) + " " + Dig(3);
                default: return Dig(1) + Let(1) + "0 " + Dig(3) + " " + Dig(3) + " " + Let(1);
            }
        }

        private static readonly string[] ApptNames = { "OMAR", "ABEL", "IVAN", "ELLA", "ERIN", "OLGA" };

        private static readonly string[] ApptSteps =
        {
            "SELECT APPOINTMENT DATE", "CONFIRM APPOINTMENT DATE", "ENTER CUSTOMER NAME", "SET PROMISE TIME", "SUBMITTING APPOINTMENT"
        };

        private static readonly string[] ApptChecks =
        {
            "I certify the Promise Time is acceptable.",
            "I acknowledge dates are subject to change.",
            "I have read the Terms of Service (page 1 of 47)."
        };

        private static readonly string[] ArrowNames = { "UP", "RIGHT", "DOWN", "LEFT" };
        private static readonly string[] ArrowGlyph = { "↑", "→", "↓", "←" };
        private static readonly KeyCode[] ArrowKeys = { KeyCode.UpArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.LeftArrow };

        private class Hit
        {
            public Task Owner;
            public Rect Rect;
            public int Digit = -1, Arrow = -1, Hold = -1;
            public char Letter;
        }

        public static int Open;
        public static bool ClearRequest;
        // mouse mode (toggle key): click the same things the keys press
        private readonly List<Hit> _hits = new List<Hit>();
        private bool _mouse, _cursorOpened;
        private object _eventSystem;
        private float _sc = 1f;
        private static int _clickDigit = -1, _clickArrow = -1, _heldArrow = -1;
        private static char _clickLetter;
        private readonly List<Task> _tasks = new List<Task>();
        private CharacterBody _body;
        private float _nextSpawn = 20f;
        private int _sentKey = -1;
        private bool _audit, _auditEnded;
        private int _backlog;
        private float _postAudit, _toastT;
        private string _toast = "";
        private Texture2D _white;
        private GUIStyle _label, _center, _mono, _big;

        public static bool Enabled => CmxPlugin.Tasks == null || CmxPlugin.Tasks.Value;

        private void Update()
        {
            CharacterBody me = null;
            try { me = LocalUserManager.GetFirstLocalUser()?.cachedBody; } catch { }
            CmxShift shift = me ? me.GetComponent<CmxShift>() : null;
            bool alive = shift && me.healthComponent && me.healthComponent.alive;
            if (me != _body) { _tasks.Clear(); _body = me; _nextSpawn = 20f; _audit = false; _backlog = 0; }
            if (!alive || !Enabled) { if (_tasks.Count > 0) _tasks.Clear(); _audit = false; SetMouse(false); Report(shift); return; }
            if (Time.timeScale <= 0f) return;

            float dt = Time.deltaTime;
            if (_toastT > 0f) _toastT -= dt;
            if (_postAudit > 0f) _postAudit -= dt;

            if (Input.GetKeyDown(CmxPlugin.MouseKey.Value) && (_tasks.Count > 0 || _mouse)) SetMouse(!_mouse);
            if (_mouse && _tasks.Count == 0) SetMouse(false);
            if (_mouse) MouseClicks(); else _heldArrow = -1;

            // 100 stress = management audit (the host waits a moment for us to report it before it falls back to Burnout).
            if (!_audit && CmxPlugin.AuditLockdown.Value && _postAudit <= 0f && shift.Stress >= 99.5f) StartAudit();
            if (_audit && _backlog <= 0) EndAudit();

            if (ClearRequest)
            {
                // R: the coworkers take everything off the screen (and the audit with it)
                ClearRequest = false;
                _tasks.Clear();
                if (_audit) { _backlog = 0; EndAudit(); }
                _nextSpawn = Mathf.Max(_nextSpawn, 8f);
            }

            _nextSpawn -= dt;
            if (_nextSpawn <= 0f) SpawnTick(shift);

            // mouse wheel cycles through the windows
            float sw = Input.mouseScrollDelta.y;
            if (Mathf.Abs(sw) > 0.01f && _tasks.Count > 1)
            {
                if (sw > 0f) { Task f = _tasks[0]; _tasks.RemoveAt(0); _tasks.Add(f); }
                else { Task l = _tasks[_tasks.Count - 1]; _tasks.RemoveAt(_tasks.Count - 1); _tasks.Insert(0, l); }
                CmxAudio.Play("typing");
            }

            if (_tasks.Count > 0) HandleInput(_tasks[_tasks.Count - 1], shift, dt);
            SpeakTop();
            _clickDigit = -1; _clickArrow = -1; _clickLetter = '\0';
            Report(shift);
        }

        // ------------------------------------------------------------ text to speech

        private Task _lastTop;
        private int _lastKey = -1;
        private string _lastMsg = "";

        private static string Spaced(string s)
        {
            string r = "";
            for (int i = 0; i < s.Length; i++) r += s[i] + " ";
            return r.Trim();
        }

        private static int SpokenKey(Task t)
        {
            switch (t.Kind)
            {
                case Kind.Appt: return t.Step * 100 + t.Name.Length;
                case Kind.Ero: case Kind.Story: case Kind.Inventory: return t.Step;
                default: return 0;
            }
        }

        private string Spoken(Task t)
        {
            switch (t.Kind)
            {
                case Kind.PartNumber: return "Parts counter. Type the part number: " + Spaced(t.Answer);
                case Kind.RepairOrder:
                {
                    string r = "Sign off the repair order. Press";
                    for (int i = 0; i < t.Arrows.Length; i++) r += " " + ArrowNames[t.Arrows[i]].ToLower() + ",";
                    return r;
                }
                case Kind.Story: return t.Step == 0 ? "Warranty claim. Customer states " + t.Story.Complaint + ". Pick the cause." : "Now pick the correction.";
                case Kind.Invoice: return "Invoice. " + t.Header.Replace("$", "").Replace("+", "plus").Replace("=", "equals").Replace("?", "what");
                case Kind.Ero:
                    return t.Step == 0 ? "Repair order entry. " + t.Says + " Pick the matching complaint." : t.Step == 1 ? "Pick the pay type. " + PayHints[t.NeedPay] : "Stamp it. Press num enter.";
                case Kind.Appt:
                    switch (t.Step)
                    {
                        case 0: return "Service scheduler. Pick the appointment date. Press a number, then num enter.";
                        case 1: return "Pick the same date again. The order has changed.";
                        case 2: return t.Name.Length == 0 ? "Type the customer's name: " + Spaced(t.TargetName) + ". First letter " + t.TargetName[0] + "." : "Next letter " + t.TargetName[t.Name.Length] + ".";
                        case 3: return "Set the promise time to " + FormatTime((int)t.TargetMin) + ". Hold 4 for earlier, 6 for later, then num enter.";
                        default: return "Submitting. Wait for the bar, then tick all three compliance boxes.";
                    }
                case Kind.Inventory: return "Inventory audit. " + t.InvParts[t.Step] + ". Count the boxes on the shelf and match the system quantity.";
                case Kind.PartMatch: return "Parts lookup. " + t.Car + ". Which part number fits? " + t.Fmt.Split(new[] { " Example" }, StringSplitOptions.None)[0];
                case Kind.Error: return "Error. Promise time exceeds bay capacity. Press zero.";
            }
            return t.Title;
        }

        private void SpeakTop()
        {
            if (_tasks.Count == 0) { _lastTop = null; _lastKey = -1; return; }
            Task top = _tasks[_tasks.Count - 1];
            int k = SpokenKey(top);
            if (top != _lastTop || k != _lastKey)
            {
                _lastTop = top; _lastKey = k;
                _lastMsg = top.Msg ?? "";
                CmxTts.Say(Spoken(top));
            }
            else if (!string.IsNullOrEmpty(top.Msg) && top.Msg != _lastMsg)
            {
                _lastMsg = top.Msg;
                CmxTts.Say(top.Msg);
            }
            else if (string.IsNullOrEmpty(top.Msg)) _lastMsg = "";
        }

        // ------------------------------------------------------------ mouse mode

        private void OnDisable() { SetMouse(false); }

        private void SetMouse(bool on)
        {
            _mouse = on;
            if (!on) { _hits.Clear(); _heldArrow = -1; }
            if (on == _cursorOpened) return;
            try
            {
                BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                if (_eventSystem == null)
                {
                    LocalUser lu = LocalUserManager.GetFirstLocalUser();
                    if (lu != null)
                        _eventSystem = lu.GetType().GetProperty("eventSystem", bf)?.GetValue(lu) ?? lu.GetType().GetField("eventSystem", bf)?.GetValue(lu);
                }
                if (_eventSystem == null) { _mouse = false; return; }
                System.Type t = _eventSystem.GetType();
                PropertyInfo p = t.GetProperty("cursorOpenerCount", bf);
                FieldInfo fi = p == null ? t.GetField("cursorOpenerCount", bf) : null;
                int delta = on ? 1 : -1;
                if (p != null && p.CanWrite) p.SetValue(_eventSystem, Mathf.Max(0, (int)p.GetValue(_eventSystem) + delta));
                else if (fi != null) fi.SetValue(_eventSystem, Mathf.Max(0, (int)fi.GetValue(_eventSystem) + delta));
                else { CmxPlugin.Log.LogWarning("cursorOpenerCount not found; mouse mode unavailable, use the keyboard."); _mouse = false; return; }
                _cursorOpened = on;
            }
            catch (System.Exception e)
            {
                _mouse = false;
                CmxPlugin.Log.LogWarning("Cursor toggle failed: " + e.Message);
            }
        }

        private void MouseClicks()
        {
            Vector2 mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            bool down = Input.GetMouseButtonDown(0), held = Input.GetMouseButton(0);
            _heldArrow = -1;
            for (int i = _hits.Count - 1; i >= 0; i--)
            {
                Hit h = _hits[i];
                if (!h.Rect.Contains(mp)) continue;
                if (_tasks.Count > 0 && h.Owner != _tasks[_tasks.Count - 1])
                {
                    if (down && _tasks.Remove(h.Owner)) _tasks.Add(h.Owner); // click a buried window to bring it up
                    return;
                }
                if (h.Hold >= 0 && held) _heldArrow = h.Hold;
                if (down) { _clickDigit = h.Digit; _clickArrow = h.Arrow; _clickLetter = h.Letter; }
                return;
            }
        }

        private static bool ArrowPressed(int a) { return Input.GetKeyDown(ArrowKeys[a]) || _clickArrow == a; }
        private static bool ArrowHeld(int a) { return Input.GetKey(ArrowKeys[a]) || _heldArrow == a; }

        // ------------------------------------------------------------ stress tiers + audit

        private int Tier(float stress)
        {
            if (_audit) return 3;
            int p = Mathf.FloorToInt(stress);
            if (p >= 100) return 3;
            if (p >= Mathf.RoundToInt(CmxPlugin.HighFrom.Value)) return 2;
            if (p >= Mathf.RoundToInt(CmxPlugin.MediumFrom.Value)) return 1;
            return 0;
        }

        private int TicketCount()
        {
            int n = 0;
            for (int i = 0; i < _tasks.Count; i++) if (_tasks[i].Kind != Kind.Error) n++;
            return n;
        }

        private void SpawnTick(CmxShift shift)
        {
            int tier = Tier(shift.Stress);
            int cap, burst = 1;
            float gap;
            switch (tier)
            {
                case 0:
                    cap = CmxPlugin.LowCap.Value;
                    gap = CmxPlugin.LowEvery.Value * Random.Range(0.75f, 1.25f);
                    break;
                case 1:
                    cap = CmxPlugin.MedCap.Value;
                    gap = Random.Range(CmxPlugin.MedEveryMin.Value, Mathf.Max(CmxPlugin.MedEveryMin.Value, CmxPlugin.MedEveryMax.Value));
                    break;
                case 2:
                    cap = CmxPlugin.HighCap.Value;
                    burst = Random.Range(1, Mathf.Max(1, CmxPlugin.HighBurst.Value) + 1);
                    gap = Random.Range(CmxPlugin.HighEveryMin.Value, Mathf.Max(CmxPlugin.HighEveryMin.Value, CmxPlugin.HighEveryMax.Value));
                    break;
                default:
                    cap = Mathf.Max(CmxPlugin.HighCap.Value, 14);
                    gap = CmxPlugin.AuditEvery.Value;
                    break;
            }
            _nextSpawn = Mathf.Max(0.3f, gap * (shift.Overtime && tier < 3 ? 0.7f : 1f));

            for (int i = 0; i < burst; i++)
            {
                int open = TicketCount();
                if (open >= cap) break;
                if (_audit && open >= _backlog) break; // never more windows than you can clear
                Spawn(tier);
            }
        }

        private void StartAudit()
        {
            _audit = true;
            CmxTts.Say("Management audit. Clear the backlog.");
            _backlog = Mathf.Max(1, CmxPlugin.AuditBacklog.Value);
            _nextSpawn = 0.3f;
            CmxAudio.Play("slam");
        }

        private void EndAudit()
        {
            _audit = false;
            _auditEnded = true; // reported once so the host resets stress
            _postAudit = 3f;
            _nextSpawn = 6f;
            _toast = "AUDIT PASSED";
            _toastT = 2.5f;
            for (int i = _tasks.Count - 1; i >= 0; i--) if (_tasks[i].Kind != Kind.Error) _tasks.RemoveAt(i);
            CmxAudio.Play("cash");
        }

        // ------------------------------------------------------------ spawning

        private Kind PickKind()
        {
            float r = Random.value;
            if (r < CmxPlugin.ApptChance.Value) return Kind.Appt;
            if (r < CmxPlugin.ApptChance.Value + CmxPlugin.EroChance.Value) return Kind.Ero;
            if (r < CmxPlugin.ApptChance.Value + CmxPlugin.EroChance.Value + CmxPlugin.InvChance.Value) return Kind.Inventory;
            if (r < CmxPlugin.ApptChance.Value + CmxPlugin.EroChance.Value + CmxPlugin.InvChance.Value + CmxPlugin.MatchChance.Value) return Kind.PartMatch;
            return (Kind)Random.Range(0, 4);
        }

        private Rect PickRect(float w, float h, int tier)
        {
            float x = Random.Range(0.04f, 0.96f) * (Screen.width - w);
            float y = Random.Range(0.08f, 0.85f) * (Screen.height - h);
            // Under pressure, new windows love to land on the last one.
            if (tier >= 1 && _tasks.Count > 0 && Random.value < 0.35f)
            {
                Rect last = _tasks[_tasks.Count - 1].Rect;
                x = Mathf.Clamp(last.x + Random.Range(-90f, 90f), 0f, Mathf.Max(0f, Screen.width - w));
                y = Mathf.Clamp(last.y + Random.Range(-70f, 70f), 0f, Mathf.Max(0f, Screen.height - h));
            }
            return new Rect(x, y, w, h);
        }

        private void Spawn(int tier)
        {
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);
            Kind kind = PickKind();
            float w = 460f * sc, h = 270f * sc;
            if (kind == Kind.PartNumber || kind == Kind.Invoice) h = 310f * sc;
            if (kind == Kind.Ero) { w = 640f * sc; h = 440f * sc; }
            else if (kind == Kind.Appt) { w = 620f * sc; h = 400f * sc; }
            else if (kind == Kind.Inventory) { w = 580f * sc; h = 380f * sc; }
            else if (kind == Kind.PartMatch) { w = 560f * sc; h = 320f * sc; }

            Task t = new Task { Rect = PickRect(w, h, tier), Born = Time.unscaledTime, Kind = kind };
            switch (t.Kind)
            {
                case Kind.PartNumber:
                    t.Title = "Parts Counter - Special Order";
                    t.Header = "No one's at the parts counter. Order it yourself.";
                    for (int i = 0; i < 9; i++) t.Answer += Random.Range(0, 10).ToString();
                    break;
                case Kind.RepairOrder:
                    t.Title = "RO #" + Random.Range(10000, 99999) + " - Sign Off";
                    t.Header = "Sign off every line before the customer arrives.";
                    t.Lines = new string[4];
                    t.Arrows = new int[4];
                    List<string> pool = new List<string>(RoLines);
                    for (int i = 0; i < 4; i++)
                    {
                        int n = Random.Range(0, pool.Count);
                        t.Lines[i] = pool[n];
                        pool.RemoveAt(n);
                        t.Arrows[i] = Random.Range(0, 4);
                    }
                    break;
                case Kind.Story:
                    t.Title = "Warranty Claim - Write the Story";
                    t.Story = Stories[Random.Range(0, Stories.Length)];
                    t.Header = "Customer states: \"" + t.Story.Complaint + "\"";
                    Shuffle(t, t.Story.Cause, t.Story.WrongCauses);
                    break;
                case Kind.Invoice:
                    t.Title = "Invoice - Customer Waiting";
                    int labor = Random.Range(2, 10) * 10, parts = Random.Range(1, 9) * 10;
                    t.Header = "Labor $" + labor + "  +  Parts $" + parts + "  =  ?";
                    t.Answer = (labor + parts).ToString();
                    break;
                case Kind.Inventory:
                {
                    t.Title = "Parts Inventory Audit - Cycle Count";
                    t.Header = "Count the boxes on the shelf, then make SYSTEM QTY match.";
                    t.Bin = "B-" + Random.Range(10, 60);
                    int p1 = Random.Range(0, InvNames.Length), p2 = (p1 + 1 + Random.Range(0, InvNames.Length - 1)) % InvNames.Length;
                    t.InvParts[0] = InvNames[p1]; t.InvParts[1] = InvNames[p2];
                    for (int i = 0; i < 2; i++)
                    {
                        int shelf = Random.Range(2, 10);
                        int sys = Mathf.Clamp(shelf + (Random.value < 0.5f ? -1 : 1) * Random.Range(1, 4), 0, 12);
                        if (sys == shelf) sys = shelf + 1;
                        t.InvShelf[i] = shelf; t.InvSys[i] = sys;
                    }
                    t.Qty = t.InvSys[0];
                    break;
                }
                case Kind.PartMatch:
                {
                    t.Title = "Parts Lookup - Fit the Right Part";
                    int ci = Random.Range(0, CarNames.Length);
                    int make = CarMake[ci];
                    int m1 = (make + 1 + Random.Range(0, 6)) % 7, m2 = m1;
                    while (m2 == make || m2 == m1) m2 = Random.Range(0, 7);
                    t.Car = CarNames[ci];
                    t.Header = t.Car + "  -  " + InvNames[Random.Range(0, InvNames.Length)] + "\nWhich part number fits this vehicle?";
                    t.Fmt = MakeHints[make];
                    Shuffle(t, PartNo(make), new[] { PartNo(m1), PartNo(m2) });
                    break;
                }
                case Kind.Ero:
                {
                    t.Title = "ERO - Repair Order Entry   [RO #29751]";
                    t.Header = "RO #29751     USER: BECKA003     SC: ABADIA, OMAR";
                    int ci = Random.Range(0, EroSays.Length);
                    int a = (ci + 1 + Random.Range(0, EroLines.Length - 1)) % EroLines.Length;
                    int b = a;
                    while (b == a || b == ci) b = Random.Range(0, EroLines.Length);
                    t.Says = EroSays[ci];
                    t.NeedPay = Random.Range(0, PayCodes.Length);
                    Shuffle(t, EroLines[ci], new[] { EroLines[a], EroLines[b] });
                    break;
                }
                case Kind.Appt:
                {
                    t.Title = "Service Scheduler - New Appointment";
                    t.Dates = new string[9];
                    for (int i = 0; i < 9; i++)
                        t.Dates[i] = DateTime.Today.AddDays(i + 1).ToString("ddd MM/dd", System.Globalization.CultureInfo.InvariantCulture);
                    t.Order = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
                    t.Cursor = -1;
                    t.TargetName = ApptNames[Random.Range(0, ApptNames.Length)];
                    t.TargetMin = 540 + 5 * Random.Range(0, 85); // 9:00 AM to 4:00 PM
                    break;
                }
            }
            _tasks.Add(t);
            CmxAudio.Play("memo");
        }

        private void SpawnError()
        {
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);
            float w = 440f * sc, h = 190f * sc;
            Task t = new Task
            {
                Kind = Kind.Error, Born = Time.unscaledTime, Title = "Error", Header = "ERROR 0x8007000E",
                Rect = new Rect(Screen.width * 0.5f - w * 0.5f + Random.Range(-120f, 120f) * sc, Screen.height * 0.5f - h * 0.5f + Random.Range(-90f, 90f) * sc, w, h)
            };
            _tasks.Add(t);
            CmxAudio.Play("fail");
        }

        private static void Shuffle(Task t, string right, string[] wrong)
        {
            List<string> o = new List<string> { right, wrong[0], wrong[1] };
            for (int i = o.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); string s = o[i]; o[i] = o[j]; o[j] = s; }
            t.Options = o.ToArray();
            t.Correct = o.IndexOf(right);
        }

        // ------------------------------------------------------------ input (top window only)

        private static int DigitDown()
        {
            if (_clickDigit >= 0) { int c = _clickDigit; _clickDigit = -1; return c; }
            if (Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Return)) return 11;
            for (int i = 0; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
            return -1;
        }

        private void Wrong(Task t, string sound = "fail")
        {
            t.Shake = Time.unscaledTime;
            CmxAudio.Play(sound);
        }

        private void HandleInput(Task t, CmxShift shift, float dt)
        {
            switch (t.Kind)
            {
                case Kind.PartNumber:
                case Kind.Invoice:
                {
                    if (Input.GetKeyDown(KeyCode.Backspace) && t.Typed.Length > 0) t.Typed = t.Typed.Substring(0, t.Typed.Length - 1);
                    int d = DigitDown();
                    if (d == 10) { if (t.Typed.Length > 0) t.Typed = t.Typed.Substring(0, t.Typed.Length - 1); break; }
                    if (d < 0) break;
                    t.Typed += d.ToString();
                    CmxAudio.Play("typing");
                    if (t.Kind == Kind.PartNumber && !t.Answer.StartsWith(t.Typed)) { t.Typed = ""; Wrong(t); }
                    else if (t.Typed.Length >= t.Answer.Length)
                    {
                        if (t.Typed == t.Answer) Complete(t, shift);
                        else { t.Typed = ""; Wrong(t); }
                    }
                    break;
                }
                case Kind.RepairOrder:
                    for (int a = 0; a < 4; a++)
                    {
                        if (!ArrowPressed(a)) continue;
                        if (a == t.Arrows[t.Step]) { t.Step++; CmxAudio.Play("stamp"); if (t.Step >= t.Arrows.Length) Complete(t, shift); }
                        else { t.Step = 0; Wrong(t); }
                        break;
                    }
                    break;
                case Kind.Story:
                {
                    int d = DigitDown();
                    if (d < 1 || d > 3) break;
                    if (d - 1 == t.Correct)
                    {
                        CmxAudio.Play("typing");
                        if (t.Step == 0) { t.Step = 1; Shuffle(t, t.Story.Correction, t.Story.WrongCorrections); }
                        else Complete(t, shift);
                    }
                    else
                    {
                        Wrong(t);
                        t.Step = 0;
                        Shuffle(t, t.Story.Cause, t.Story.WrongCauses);
                    }
                    break;
                }
                case Kind.Ero:
                    EroInput(t, shift);
                    break;
                case Kind.Inventory:
                {
                    int d = DigitDown();
                    if (ArrowPressed(0) || Input.GetKeyDown(KeyCode.KeypadPlus)) { t.Qty = Mathf.Min(20, t.Qty + 1); CmxAudio.Play("typing"); }
                    else if (ArrowPressed(2) || Input.GetKeyDown(KeyCode.KeypadMinus)) { t.Qty = Mathf.Max(0, t.Qty - 1); CmxAudio.Play("typing"); }
                    if (d >= 0 && d <= 9) { t.Qty = d; t.Msg = ""; CmxAudio.Play("typing"); break; }
                    if (d != 11) break;
                    if (t.Qty == t.InvShelf[t.Step])
                    {
                        CmxAudio.Play("stamp");
                        if (t.Step == 0) { t.Step = 1; t.Qty = t.InvSys[1]; t.Msg = ""; }
                        else Complete(t, shift);
                    }
                    else { t.Msg = "QTY DOES NOT MATCH THE SHELF."; Wrong(t); }
                    break;
                }
                case Kind.PartMatch:
                {
                    int d = DigitDown();
                    if (d < 1 || d > 3) break;
                    if (d - 1 == t.Correct) { CmxAudio.Play("stamp"); Complete(t, shift); }
                    else Wrong(t);
                    break;
                }
                case Kind.Appt:
                    ApptInput(t, shift, dt);
                    break;
                case Kind.Error:
                    { int e = DigitDown(); if (e == 0 || e == 11) { _tasks.Remove(t); CmxAudio.Play("typing"); } }
                    break;
            }
        }

        // ERO: 1) pick the complaint, 2) pick the pay type, 3) stamp LARGE COMPLETED.
        private void EroInput(Task t, CmxShift shift)
        {
            int d = DigitDown();
            if (d < 0) return;
            if (t.Step == 0)
            {
                if (d < 1 || d > 3) return;
                if (d - 1 == t.Correct) { t.Chosen = t.Options[t.Correct]; t.Step = 1; CmxAudio.Play("typing"); }
                else Wrong(t);
            }
            else if (t.Step == 1)
            {
                if (d < 1 || d > 4) return;
                if (d - 1 == t.NeedPay) { t.Step = 2; CmxAudio.Play("typing"); }
                else Wrong(t);
            }
            else if (d == 0 || d == 11)
            {
                CmxAudio.Play("stamp");
                Complete(t, shift, CmxPlugin.EroRelief.Value);
            }
            else Wrong(t);
        }

        private static int Wrap(int v, int n) { return ((v % n) + n) % n; }

        private void ApptInput(Task t, CmxShift shift, float dt)
        {
            int d = DigitDown();
            switch (t.Step)
            {
                case 0:
                case 1:
                    if (d >= 1 && d <= 9) { t.Cursor = d - 1; t.Msg = ""; CmxAudio.Play("typing"); break; }
                    if (d != 11) break;
                    if (t.Cursor < 0) { t.Msg = "PRESS A NUMBER FIRST."; Wrong(t); break; }
                    {
                        int chosen = t.Order[t.Cursor];
                        if (t.Step == 0)
                        {
                            t.Picked = chosen;
                            for (int i = 8; i > 0; i--) { int j = Random.Range(0, i + 1); int tmp = t.Order[i]; t.Order[i] = t.Order[j]; t.Order[j] = tmp; }
                            t.Cursor = -1; t.Step = 1; t.Msg = ""; CmxAudio.Play("stamp");
                        }
                        else if (chosen == t.Picked) { t.Cursor = -1; t.Step = 2; t.Msg = ""; NewLetters(t); CmxAudio.Play("stamp"); }
                        else
                        {
                            t.Picked = -1; t.Cursor = -1; t.Step = 0;
                            t.Order = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
                            t.Msg = "DATES DID NOT MATCH. START OVER."; Wrong(t);
                        }
                    }
                    break;
                case 2:
                    if (d < 1 || d > 3) break;
                    if (d - 1 == t.LetterOk)
                    {
                        t.Name += t.Letters[d - 1];
                        t.Msg = "";
                        CmxAudio.Play("typing");
                        if (t.Name.Length >= t.TargetName.Length) t.Step = 3; else NewLetters(t);
                    }
                    else { t.Name = ""; t.Msg = "WRONG LETTER. START THE NAME OVER."; Wrong(t); NewLetters(t); }
                    break;
                case 3:
                {
                    // Very twitchy: the longer you hold, the faster it goes. Overshoot and the scheduler throws an error.
                    t.ErrCool -= dt;
                    int dir = HeldLater() ? 1 : HeldEarlier() ? -1 : 0;
                    if (dir != 0)
                    {
                        t.Hold += dt;
                        float speed = Mathf.Lerp(40f, 420f, Mathf.Clamp01(t.Hold)) * CmxPlugin.SliderSensitivity.Value;
                        t.Pos = Mathf.Clamp(t.Pos + dir * speed * dt, 420f, 1080f);
                    }
                    else t.Hold = 0f;

                    if (t.Pos > t.TargetMin + 20f && t.ErrCool <= 0f)
                    {
                        t.ErrCool = 1.5f;
                        t.Pos = 420f;
                        t.Hold = 0f;
                        SpawnError();
                        return;
                    }
                    if (d != 11) break;
                    int minutes = 5 * Mathf.RoundToInt(t.Pos / 5f);
                    if (Mathf.Abs(minutes - t.TargetMin) <= Mathf.Max(5f, CmxPlugin.SliderTolerance.Value)) { t.Step = 4; t.Msg = ""; CmxAudio.Play("stamp"); }
                    else { t.Msg = "NOT ACCEPTABLE. TARGET IS " + FormatTime((int)t.TargetMin) + "."; Wrong(t); }
                    break;
                }
                case 4:
                {
                    // 10 seconds to 99%, then it stalls until every compliance box is ticked.
                    t.ProgT += dt;
                    float p = Mathf.Clamp01(t.ProgT / 10f);
                    if (!t.Released)
                    {
                        t.Prog = 0.99f * p;
                        if (p >= 1f)
                        {
                            if (d >= 1 && d <= 3) { t.Checks[d - 1] = !t.Checks[d - 1]; CmxAudio.Play("typing"); }
                            if (t.Checks[0] && t.Checks[1] && t.Checks[2]) t.Released = true;
                        }
                    }
                    else
                    {
                        t.Prog = Mathf.MoveTowards(t.Prog, 1f, dt * 1.6f);
                        if (t.Prog >= 1f) Complete(t, shift, CmxPlugin.ApptRelief.Value);
                    }
                    break;
                }
            }
        }

        private static bool HeldLater() { return ArrowHeld(1) || Input.GetKey(KeyCode.Keypad6) || Input.GetKey(KeyCode.Alpha6); }
        private static bool HeldEarlier() { return ArrowHeld(3) || Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.Alpha4); }

        // The next letter of the name: three keys, one is right (decoys match it: vowels hide among vowels).
        private static void NewLetters(Task t)
        {
            char target = t.TargetName[t.Name.Length];
            string pool = "AEIOU".IndexOf(target) >= 0 ? "AEIOU" : "BCDFGHJKLMNPRSTVWXYZ";
            List<char> o = new List<char> { target };
            while (o.Count < 3) { char c = pool[Random.Range(0, pool.Length)]; if (!o.Contains(c)) o.Add(c); }
            for (int i = 2; i > 0; i--) { int j = Random.Range(0, i + 1); char c = o[i]; o[i] = o[j]; o[j] = c; }
            for (int i = 0; i < 3; i++) t.Letters[i] = o[i];
            t.LetterOk = o.IndexOf(target);
        }

        private static string FormatTime(int minutes)
        {
            int h = minutes / 60, m = minutes % 60;
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return h12 + ":" + m.ToString("00") + (h >= 12 ? " PM" : " AM");
        }

        private void Complete(Task t, CmxShift shift, float relief = -1f)
        {
            _tasks.Remove(t);
            CmxAudio.Play("cash");
            if (_audit) _backlog = Mathf.Max(0, _backlog - 1);
            CmxNet.TaskDone(shift, relief);
        }

        private void Report(CmxShift shift)
        {
            int open = _tasks.Count;
            Open = open;
            if (!shift) return;
            byte audit = (byte)(_audit ? 1 : _auditEnded ? 2 : 0);
            int key = open * 4 + audit;
            if (key == _sentKey) return;
            _sentKey = key;
            _auditEnded = false;
            CmxNet.TaskOpen(shift, open, audit);
        }

        // ------------------------------------------------------------ drawing

        private void Styles(float sc)
        {
            if (!_white)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
                _label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft };
                _center = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter };
                _mono = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                _big = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            _sc = sc;
            _label.fontSize = Mathf.RoundToInt(17f * sc);
            _center.fontSize = Mathf.RoundToInt(17f * sc);
            _mono.fontSize = Mathf.RoundToInt(30f * sc);
            _big.fontSize = Mathf.RoundToInt(96f * sc);
        }

        private void Box(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            bool overlay = _audit || _toastT > 0f;
            if (_tasks.Count == 0 && !overlay) return;
            GUI.depth = -10;
            _hits.Clear();
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);
            Styles(sc);
            if (_audit) DrawAudit(sc);
            else if (_toastT > 0f)
            {
                _big.normal.textColor = new Color(1f, 0.79f, 0.15f);
                GUI.Label(new Rect(0f, Screen.height * 0.25f, Screen.width, 140f * sc), _toast, _big);
            }
            for (int i = 0; i < _tasks.Count; i++) DrawTask(_tasks[i], sc, i == _tasks.Count - 1);
            if (_tasks.Count > 0) DrawMouseHint(sc);
        }

        private void DrawMouseHint(float sc)
        {
            float w = 300f * sc, h = 28f * sc;
            Rect r = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - h - 10f * sc, w, h);
            Box(r, new Color(0f, 0f, 0f, 0.75f));
            _center.normal.textColor = _mouse ? new Color(0.5f, 1f, 0.55f) : Color.white;
            int old = _center.fontSize;
            _center.fontSize = Mathf.RoundToInt(15f * sc);
            GUI.Label(r, "[" + CmxPlugin.MouseKey.Value + "]  MOUSE " + (_mouse ? "ON  (click the windows)" : "OFF"), _center);
            _center.fontSize = old;
        }

        private void AddHit(Task t, Rect r, int digit = -1, int arrow = -1, int hold = -1, char letter = '\0')
        {
            if (!_mouse) return;
            _hits.Add(new Hit { Owner = t, Rect = r, Digit = digit, Arrow = arrow, Hold = hold, Letter = letter });
        }

        private static bool Over(Rect r) { return r.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)); }

        /// <summary>A clickable line of text: hover highlight, then the caller draws its label on top.</summary>
        private void Opt(Task t, Rect r, int digit)
        {
            if (!_mouse) return;
            AddHit(t, r, digit);
            if (Over(r)) Box(r, new Color(0.65f, 0.82f, 1f, 0.7f));
        }

        private void Btn(Task t, Rect r, string text, int digit = -1, int arrow = -1, int hold = -1, char letter = '\0', bool always = false)
        {
            if (!_mouse && !always) return;
            AddHit(t, r, digit, arrow, hold, letter);
            bool over = Over(r);
            bool press = over && Input.GetMouseButton(0);
            Box(r, new Color(0.04f, 0.06f, 0.1f));
            Box(new Rect(r.x + 1.5f * _sc, r.y + 1.5f * _sc, r.width - 3f * _sc, r.height - 3f * _sc),
                press ? new Color(0.05f, 0.17f, 0.35f) : over ? new Color(0.25f, 0.5f, 0.9f) : new Color(0f, 0.28f, 0.55f));
            int old = _center.fontSize;
            _center.fontSize = Mathf.RoundToInt(16f * _sc);
            _center.normal.textColor = Color.white;
            GUI.Label(r, "<b>" + text + "</b>", _center);
            _center.fontSize = old;
            _center.normal.textColor = new Color(0.1f, 0.12f, 0.18f);
        }

        private void Numpad(Task t, Rect inner, float sc)
        {
            if (!_mouse) return;
            float gap = 4f * sc, pad = 12f * sc, bh = 26f * sc;
            float bw = (inner.width - pad * 2f - gap * 5f) / 6f;
            float y0 = inner.yMax - 26f * sc - 2f * bh - gap - 4f * sc;
            for (int i = 0; i < 11; i++)
            {
                int row = i / 6, col = i % 6;
                int digit = i < 9 ? i + 1 : i == 9 ? 0 : 10;
                Btn(t, new Rect(inner.x + pad + col * (bw + gap), y0 + row * (bh + gap), bw, bh), i == 10 ? "<" : digit.ToString(), digit);
            }
        }

        private void DrawAudit(float sc)
        {
            float pulse = 0.6f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f);
            Box(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.14f, 0f, 0f, pulse));
            _big.normal.textColor = new Color(1f, 0.2f, 0.2f);
            GUI.Label(new Rect(0f, Screen.height * 0.28f, Screen.width, 140f * sc), "MANAGEMENT AUDIT", _big);
            _center.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.2f, Screen.height * 0.28f + 130f * sc, Screen.width * 0.6f, 70f * sc),
                "ALL REPAIR ORDERS MUST BE RECONCILED BEFORE THE SHIFT RESUMES.\nBACKLOG: " + _backlog + " REMAINING", _center);
        }

        private string HeaderOf(Task t)
        {
            if (t.Kind == Kind.Appt) return "STEP " + (t.Step + 1) + " OF 5   -   " + ApptSteps[t.Step];
            return t.Header;
        }

        private void DrawTask(Task t, float sc, bool active)
        {
            float age = Time.unscaledTime - t.Born;
            float pop = Mathf.Min(1f, age / 0.12f);
            float shake = Time.unscaledTime - t.Shake < 0.3f ? Mathf.Sin(Time.unscaledTime * 90f) * 8f * sc : 0f;
            Rect r = t.Rect;
            r = new Rect(r.center.x - r.width * pop / 2f + shake, r.center.y - r.height * pop / 2f, r.width * pop, r.height * pop);
            if (pop < 1f) { Box(r, new Color(0.9f, 0.9f, 0.9f, 0.9f)); return; }
            AddHit(t, r);

            float bar = 30f * sc, pad = 12f * sc;
            Box(new Rect(r.x + 6f * sc, r.y + 6f * sc, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
            Box(r, active ? new Color(1f, 0.79f, 0.15f) : new Color(0.55f, 0.55f, 0.55f));
            Rect inner = new Rect(r.x + 3f * sc, r.y + 3f * sc, r.width - 6f * sc, r.height - 6f * sc);
            Box(inner, new Color(0.94f, 0.94f, 0.92f, 0.97f));
            Box(new Rect(inner.x, inner.y, inner.width, bar), active ? new Color(0f, 0.23f, 0.46f) : new Color(0.35f, 0.42f, 0.52f));
            _label.normal.textColor = Color.white;
            GUI.Label(new Rect(inner.x + pad * 0.6f, inner.y + 4f * sc, inner.width - 60f * sc, bar), "<b>" + t.Title + "</b>", _label);
            Box(new Rect(inner.xMax - bar, inner.y + 4f * sc, bar - 6f * sc, bar - 8f * sc), new Color(0.75f, 0.2f, 0.2f));
            _center.normal.textColor = Color.white;
            GUI.Label(new Rect(inner.xMax - bar, inner.y + 2f * sc, bar - 6f * sc, bar - 4f * sc), "<b>x</b>", _center);

            float y = inner.y + bar + pad * 0.6f;
            float w = inner.width - pad * 2f;
            _label.normal.textColor = new Color(0.1f, 0.12f, 0.18f);
            _center.normal.textColor = new Color(0.1f, 0.12f, 0.18f);
            GUI.Label(new Rect(inner.x + pad, y, w, 46f * sc), HeaderOf(t), _label);
            y += 44f * sc;

            switch (t.Kind)
            {
                case Kind.PartNumber:
                {
                    _mono.normal.textColor = new Color(0f, 0.23f, 0.46f);
                    GUI.Label(new Rect(inner.x + pad, y, w, 40f * sc), "Part # " + Group(t.Answer), _mono);
                    y += 46f * sc;
                    Box(new Rect(inner.x + pad, y, w, 40f * sc), Color.white);
                    _mono.normal.textColor = new Color(0.05f, 0.25f, 0.65f);
                    GUI.Label(new Rect(inner.x + pad, y, w, 40f * sc), Group(t.Typed) + (Blink() ? "_" : " "), _mono);
                    y += 48f * sc;
                    Numpad(t, inner, sc);
                    Hint(inner, "Type the part number with the number keys", sc);
                    break;
                }
                case Kind.Invoice:
                {
                    Box(new Rect(inner.x + pad, y + 8f * sc, w, 44f * sc), Color.white);
                    _mono.normal.textColor = new Color(0.05f, 0.25f, 0.65f);
                    GUI.Label(new Rect(inner.x + pad, y + 8f * sc, w, 44f * sc), "$ " + t.Typed + (Blink() ? "_" : " "), _mono);
                    Numpad(t, inner, sc);
                    Hint(inner, "Type the total with the number keys", sc);
                    break;
                }
                case Kind.RepairOrder:
                {
                    float lh = 26f * sc;
                    for (int i = 0; i < t.Lines.Length; i++)
                    {
                        bool done = i < t.Step, next = i == t.Step;
                        string mark = done ? "<color=#B8860B>[SIGNED]</color>" : next ? "<color=#003B75><b>[" + ArrowGlyph[t.Arrows[i]] + " " + ArrowNames[t.Arrows[i]] + "]</b></color>" : "<color=#888888>[" + ArrowGlyph[t.Arrows[i]] + " " + ArrowNames[t.Arrows[i]] + "]</color>";
                        string line = done ? "<color=#888888>" + t.Lines[i] + "</color>" : t.Lines[i];
                        GUI.Label(new Rect(inner.x + pad, y + i * lh, w * 0.62f, lh), (i + 1) + ". " + line, _label);
                        GUI.Label(new Rect(inner.x + pad + w * 0.6f, y + i * lh, w * 0.4f, lh), mark, _label);
                    }
                    for (int a = 0; a < 4; a++)
                        Btn(t, new Rect(inner.x + inner.width * 0.5f - 2f * 74f * sc + a * 74f * sc + 2f * sc, inner.yMax - 26f * sc - 36f * sc, 70f * sc, 32f * sc), ArrowGlyph[a], arrow: a);
                    Hint(inner, "Press the arrow keys in order", sc);
                    break;
                }
                case Kind.Story:
                {
                    GUI.Label(new Rect(inner.x + pad, y - 6f * sc, w, 24f * sc), "<b>" + (t.Step == 0 ? "CAUSE:" : "CORRECTION:") + "</b>", _label);
                    y += 20f * sc;
                    for (int i = 0; i < 3; i++)
                    {
                        Rect o = new Rect(inner.x + pad, y + i * 25f * sc, w, 25f * sc);
                        Opt(t, o, i + 1);
                        GUI.Label(o, "<b><color=#003B75>[" + (i + 1) + "]</color></b> " + t.Options[i], _label);
                    }
                    Hint(inner, "Press 1, 2 or 3", sc);
                    break;
                }
                case Kind.Ero: DrawEro(t, inner, y, w, pad, sc); break;
                case Kind.Inventory: DrawInventory(t, inner, y, w, pad, sc); break;
                case Kind.PartMatch:
                {
                    float lh = 28f * sc;
                    for (int i = 0; i < 3; i++)
                    {
                        Rect o = new Rect(inner.x + pad, y + i * lh, w, lh);
                        Opt(t, o, i + 1);
                        GUI.Label(o, "<b><color=#003B75>[" + (i + 1) + "]</color></b>   " + t.Options[i], _label);
                    }
                    GUI.Label(new Rect(inner.x + pad, y + 3f * lh + 8f * sc, w, 70f * sc), "<color=#B8860B><b>HINT</b></color>  " + t.Fmt, _label);
                    Hint(inner, "Press 1, 2 or 3", sc);
                    break;
                }
                case Kind.Appt: DrawAppt(t, inner, y, w, pad, sc); break;
                case Kind.Error:
                {
                    Box(new Rect(inner.x + pad, y - 6f * sc, 44f * sc, 44f * sc), new Color(0.8f, 0.1f, 0.1f));
                    _mono.normal.textColor = Color.white;
                    GUI.Label(new Rect(inner.x + pad, y - 6f * sc, 44f * sc, 44f * sc), "X", _mono);
                    GUI.Label(new Rect(inner.x + pad + 60f * sc, y - 6f * sc, w - 60f * sc, 60f * sc), "PROMISE TIME EXCEEDS BAY CAPACITY.\nSELECT AN EARLIER TIME.", _label);
                    Btn(t, new Rect(inner.center.x - 50f * sc, inner.yMax - 26f * sc - 38f * sc, 100f * sc, 32f * sc), "OK", 0);
                    Hint(inner, "Press 0 for OK", sc);
                    break;
                }
            }
        }

        private void DrawEro(Task t, Rect inner, float y, float w, float pad, float sc)
        {
            float lh = 24f * sc, x = inner.x + pad;
            GUI.Label(new Rect(x, y, w, lh), "<b>BILL TO:</b> " + PayHints[t.NeedPay], _label);
            y += lh;
            GUI.Label(new Rect(x, y, w, lh * 2f), t.Says, _label);
            y += lh * 2f;
            GUI.Label(new Rect(x, y, w, lh), "<b>CUSTOMER COMPLAINT</b>", _label);
            y += lh;
            Box(new Rect(x, y, w, lh + 6f * sc), Color.white);
            GUI.Label(new Rect(x + 4f * sc, y + 2f * sc, w - 8f * sc, lh + 4f * sc),
                t.Chosen.Length > 0 ? t.Chosen : "<color=#888888>(press 1-3 to enter the complaint)</color>", _label);
            y += lh + 14f * sc;

            if (t.Step == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    Rect o = new Rect(x, y + i * lh, w, lh);
                    Opt(t, o, i + 1);
                    GUI.Label(o, "<b><color=#003B75>[" + (i + 1) + "]</color></b> " + t.Options[i], _label);
                }
            }
            else if (_mouse)
            {
                GUI.Label(new Rect(x, y, w, lh), "<b>PAY TYPE:</b>", _label);
                float bw = (w - 3f * 6f * sc) / 4f;
                for (int i = 0; i < PayCodes.Length; i++)
                    Btn(t, new Rect(x + i * (bw + 6f * sc), y + lh, bw, 30f * sc), (t.Step == 2 && i == t.NeedPay ? "* " : "") + PayCodes[i], i + 1);
            }
            else
            {
                string pay = "<b>PAY TYPE:</b>   ";
                for (int i = 0; i < PayCodes.Length; i++)
                {
                    bool chosen = t.Step == 2 && i == t.NeedPay;
                    pay += (chosen ? "<color=#B8860B><b>" : "<color=#003B75>") + "[" + (i + 1) + "] " + PayCodes[i] + (chosen ? "</b></color>" : "</color>") + "    ";
                }
                GUI.Label(new Rect(x, y, w, lh), pay, _label);
            }

            // the big green stamp
            Rect stamp = new Rect(inner.x + pad + w * 0.5f - 160f * sc, inner.yMax - 26f * sc - 74f * sc, 320f * sc, 66f * sc);
            bool ready = t.Step == 2;
            AddHit(t, stamp, 11);
            Box(new Rect(stamp.x - 3f * sc, stamp.y - 3f * sc, stamp.width + 6f * sc, stamp.height + 6f * sc), new Color(0.05f, 0.25f, 0.08f));
            Box(stamp, ready ? new Color(0.1f, 0.65f, 0.2f) : new Color(0.55f, 0.6f, 0.55f));
            int old = _mono.fontSize;
            _mono.fontSize = Mathf.RoundToInt(28f * sc);
            _mono.normal.textColor = Color.white;
            GUI.Label(stamp, "LARGE COMPLETED", _mono);
            _mono.fontSize = old;
            Hint(inner, t.Step == 0 ? "Press 1, 2 or 3" : t.Step == 1 ? "Press 1-4 for the pay type the bill-to calls for" : "Press NUM ENTER to stamp it", sc);
        }

        private void DrawInventory(Task t, Rect inner, float y, float w, float pad, float sc)
        {
            float x = inner.x + pad, lh = 24f * sc;
            int shelf = t.InvShelf[t.Step];
            GUI.Label(new Rect(x, y - 6f * sc, w, lh), "<b>" + t.InvParts[t.Step] + "</b>   <color=#888888>bin " + t.Bin + "   (count " + (t.Step + 1) + " of 2)</color>", _label);
            y += lh - 4f * sc;
            // the shelf: one box per unit actually on it
            Rect shelfR = new Rect(x, y, w * 0.55f, 120f * sc);
            Box(shelfR, new Color(0.16f, 0.2f, 0.3f));
            float cell = 40f * sc;
            for (int i = 0; i < shelf; i++)
            {
                Rect c = new Rect(shelfR.x + 10f * sc + (i % 5) * (cell + 6f * sc), shelfR.y + 10f * sc + (i / 5) * (cell + 6f * sc), cell, cell);
                Box(c, new Color(0.04f, 0.06f, 0.1f));
                Box(new Rect(c.x + 3f * sc, c.y + 3f * sc, c.width - 6f * sc, c.height - 6f * sc), new Color(1f, 0.79f, 0.15f));
            }
            // system quantity
            float rx = shelfR.xMax + 14f * sc, rw = w - shelfR.width - 14f * sc;
            GUI.Label(new Rect(rx, y, rw, lh), "<b>SYSTEM QTY</b>", _label);
            Box(new Rect(rx, y + lh, rw, 50f * sc), Color.white);
            _mono.normal.textColor = new Color(0.05f, 0.25f, 0.65f);
            GUI.Label(new Rect(rx, y + lh, rw, 50f * sc), t.Qty.ToString(), _mono);
            float bw = (rw - 6f * sc) / 2f;
            Btn(t, new Rect(rx, y + lh + 56f * sc, bw, 30f * sc), "-", arrow: 2);
            Btn(t, new Rect(rx + bw + 6f * sc, y + lh + 56f * sc, bw, 30f * sc), "+", arrow: 0);
            Btn(t, new Rect(rx, y + lh + 90f * sc, rw, 30f * sc), "NUM ENTER", 11);
            GUI.Label(new Rect(x, shelfR.yMax + 6f * sc, shelfR.width, lh * 3f), "Change SYSTEM QTY with <b>Up / Down</b>, <b>+ / -</b> or just <b>press the number</b>, then <b>NUM ENTER</b>.", _label);
            if (t.Msg.Length > 0)
            {
                _label.normal.textColor = new Color(0.7f, 0.05f, 0.05f);
                GUI.Label(new Rect(x, inner.yMax - 52f * sc, w, lh), "<b>" + t.Msg + "</b>", _label);
                _label.normal.textColor = new Color(0.1f, 0.12f, 0.18f);
            }
            Hint(inner, "Match the number on the shelf", sc);
        }

        private void DrawAppt(Task t, Rect inner, float y, float w, float pad, float sc)
        {
            float lh = 24f * sc, x = inner.x + pad;
            switch (t.Step)
            {
                case 0:
                case 1:
                {
                    GUI.Label(new Rect(x, y, w, lh * 2f), t.Step == 0 ? "<b>Pick the day the customer will bring the vehicle in.</b>\nPress its number, then NUM ENTER." : "<b>Policy 7.3.1: pick the same date a second time.</b>\nThe order has changed. Press its number, then NUM ENTER.", _label);
                    y += lh * 2f + 8f * sc;
                    float cg = 6f * sc, cw = (w - cg * 2f) / 3f, ch = 34f * sc;
                    for (int i = 0; i < 9; i++)
                    {
                        Rect c = new Rect(x + (i % 3) * (cw + cg), y + (i / 3) * (ch + cg), cw, ch);
                        bool sel = t.Cursor == i;
                        AddHit(t, c, i + 1);
                        Box(c, new Color(0.04f, 0.06f, 0.1f));
                        Box(new Rect(c.x + 2f * sc, c.y + 2f * sc, c.width - 4f * sc, c.height - 4f * sc), sel ? new Color(1f, 0.79f, 0.15f) : _mouse && Over(c) ? new Color(0.85f, 0.9f, 1f) : Color.white);
                        _center.normal.textColor = new Color(0f, 0.23f, 0.46f);
                        GUI.Label(c, "<b>[" + (i + 1) + "]  " + t.Dates[t.Order[i]] + "</b>", _center);
                    }
                    Btn(t, new Rect(x + w * 0.5f - 90f * sc, y + 3f * (ch + cg) + 2f * sc, 180f * sc, 32f * sc), "NUM ENTER", 11);
                    Hint(inner, "Number = choose    NUM ENTER = confirm", sc);
                    break;
                }
                case 2:
                {
                    GUI.Label(new Rect(x, y, w, lh), "Type the customer's name: <b>" + t.TargetName + "</b>", _label);
                    y += lh + 8f * sc;
                    Box(new Rect(x, y, w, 50f * sc), Color.white);
                    _mono.normal.textColor = new Color(0.05f, 0.25f, 0.65f);
                    GUI.Label(new Rect(x, y, w, 50f * sc), t.Name + (Blink() ? "_" : " "), _mono);
                    y += 58f * sc;
                    GUI.Label(new Rect(x, y, w, lh), "Next letter: <b>" + t.TargetName[t.Name.Length] + "</b>   (press its number, no Enter)", _label);
                    y += lh + 6f * sc;
                    float kg = 8f * sc, kw = (w - kg * 2f) / 3f;
                    for (int i = 0; i < 3; i++)
                        Btn(t, new Rect(x + i * (kw + kg), y, kw, 52f * sc), "[" + (i + 1) + "]   " + t.Letters[i], i + 1, always: true);
                    Hint(inner, "Press 1, 2 or 3", sc);
                    break;
                }
                case 3:
                {
                    GUI.Label(new Rect(x, y, w, lh), "Customer needs the vehicle back by  <b>" + FormatTime((int)t.TargetMin) + "</b>", _label);
                    y += lh + 6f * sc;
                    _mono.normal.textColor = new Color(0.05f, 0.2f, 0.7f);
                    GUI.Label(new Rect(x, y, w, 46f * sc), FormatTime(5 * Mathf.RoundToInt(t.Pos / 5f)), _mono);
                    y += 58f * sc;
                    float frac = Mathf.Clamp01((t.Pos - 420f) / 660f);
                    Box(new Rect(x, y, w, 14f * sc), new Color(0.35f, 0.35f, 0.35f));
                    Box(new Rect(x + w * frac - 9f * sc, y - 12f * sc, 18f * sc, 38f * sc), new Color(1f, 0.79f, 0.15f));
                    GUI.Label(new Rect(x, y + 28f * sc, w, lh * 2f), "<b>HOLD 4</b> = earlier     <b>HOLD 6</b> = later     <b>NUM ENTER</b> = set\nVery sensitive: don't go past the time!", _label);
                    {
                        float bw = (w - 12f * sc) / 3f, by = y + 28f * sc + lh * 2f + 4f * sc;
                        Btn(t, new Rect(x, by, bw, 34f * sc), "<  HOLD", hold: 3);
                        Btn(t, new Rect(x + bw + 6f * sc, by, bw, 34f * sc), "NUM ENTER", 11);
                        Btn(t, new Rect(x + 2f * (bw + 6f * sc), by, bw, 34f * sc), "HOLD  >", hold: 1);
                    }
                    Hint(inner, "Hold 4 / 6    NUM ENTER = set the time", sc);
                    break;
                }
                default:
                {
                    GUI.Label(new Rect(x, y, w, lh), "Posting appointment to the dealer management system...", _label);
                    y += lh + 6f * sc;
                    Box(new Rect(x, y, w, 30f * sc), new Color(0.45f, 0.45f, 0.45f));
                    Box(new Rect(x + 2f * sc, y + 2f * sc, (w - 4f * sc) * t.Prog, 26f * sc), new Color(1f, 0.79f, 0.15f));
                    GUI.Label(new Rect(x, y, w, 30f * sc), "<color=white><b>" + Mathf.FloorToInt(t.Prog * 100f) + "%</b></color>", _center);
                    y += 38f * sc;
                    float p = t.ProgT / 10f;
                    if (p < 1f)
                        GUI.Label(new Rect(x, y, w, lh), p < 0.25f ? "Contacting scheduling server..." : p < 0.5f ? "Validating customer record..." : p < 0.75f ? "Reserving service bay..." : "Finalizing...", _label);
                    else
                    {
                        GUI.Label(new Rect(x, y, w, lh), "<b>COMPLIANCE VERIFICATION REQUIRED</b>", _label);
                        for (int i = 0; i < 3; i++)
                        {
                            Rect o = new Rect(x, y + (i + 1) * lh, w, lh);
                            Opt(t, o, i + 1);
                            GUI.Label(o, "<b><color=#003B75>[" + (i + 1) + "]</color></b> [" + (t.Checks[i] ? "X" : " ") + "] " + ApptChecks[i], _label);
                        }
                    }
                    Hint(inner, p < 1f ? "Please wait..." : "Press 1, 2 and 3 to tick every box", sc);
                    break;
                }
            }
            if (t.Msg.Length > 0)
            {
                _label.normal.textColor = new Color(0.7f, 0.05f, 0.05f);
                GUI.Label(new Rect(x, inner.yMax - 54f * sc, w, lh), "<b>" + t.Msg + "</b>", _label);
                _label.normal.textColor = new Color(0.1f, 0.12f, 0.18f);
            }
        }

        private void Hint(Rect inner, string text, float sc)
        {
            _center.normal.textColor = new Color(0.4f, 0.42f, 0.48f);
            int old = _center.fontSize;
            _center.fontSize = Mathf.RoundToInt(14f * sc);
            GUI.Label(new Rect(inner.x, inner.yMax - 26f * sc, inner.width, 24f * sc), text, _center);
            _center.fontSize = old;
        }

        private static bool Blink() => Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.5f;

        private static string Group(string digits)
        {
            string s = "";
            for (int i = 0; i < digits.Length; i++) { if (i == 4 || i == 7) s += "-"; s += digits[i]; }
            return s;
        }
    }

    /// <summary>Specialist → host: a task window was finished (with its stress relief), or how many are open and the audit state.</summary>
    public class CmxTaskMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public bool Done;
        public byte Open;
        public float Relief = -1f;
        public byte Audit; // 0 none, 1 audit running, 2 audit just cleared

        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Done); w.Write(Open); w.Write(Relief); w.Write(Audit); }
        public void Deserialize(NetworkReader r) { Body = r.ReadNetworkId(); Done = r.ReadBoolean(); Open = r.ReadByte(); Relief = r.ReadSingle(); Audit = r.ReadByte(); }

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            GameObject go = Util.FindNetworkObject(Body);
            CmxShift s = go ? go.GetComponent<CmxShift>() : null;
            if (!s) return;
            if (Done) s.ServerTaskDone(Relief);
            else { s.OpenTasks = Open; s.SetAudit(Audit); }
        }
    }
}
