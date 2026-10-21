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
    /// <summary>
    /// Busywork: task windows pop up on his screen like ads and stay there (blocking the view and nagging stress)
    /// until he finishes their little minigame: punch in a part number, sign off an RO, write the story
    /// (complaint / cause / correction) or total an invoice. Only the Specialist's own player sees them.
    /// </summary>
    public class CmxTasks : MonoBehaviour
    {
        private enum Kind { PartNumber, RepairOrder, Story, Invoice }

        private class Task
        {
            public Kind Kind;
            public Rect Rect;
            public float Born, Shake, Flash;
            public string Title, Header;
            // part number / invoice
            public string Answer = "", Typed = "";
            // repair order
            public string[] Lines;
            public int[] Arrows;
            public int Step;
            // story
            public Story Story;
            public string[] Options;
            public int Correct;
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

        private static readonly string[] ArrowNames = { "UP", "RIGHT", "DOWN", "LEFT" };
        private static readonly string[] ArrowGlyph = { "↑", "→", "↓", "←" };
        private static readonly KeyCode[] ArrowKeys = { KeyCode.UpArrow, KeyCode.RightArrow, KeyCode.DownArrow, KeyCode.LeftArrow };

        public static int Open;
        private readonly List<Task> _tasks = new List<Task>();
        private CharacterBody _body;
        private float _nextSpawn = 20f;
        private int _sentOpen = -1;
        private Texture2D _white;
        private GUIStyle _label, _center, _mono;

        public static bool Enabled => CmxPlugin.Tasks == null || CmxPlugin.Tasks.Value;

        private void Update()
        {
            CharacterBody me = null;
            try { me = LocalUserManager.GetFirstLocalUser()?.cachedBody; } catch { }
            CmxShift shift = me ? me.GetComponent<CmxShift>() : null;
            bool alive = shift && me.healthComponent && me.healthComponent.alive;
            if (me != _body) { _tasks.Clear(); _body = me; _nextSpawn = 20f; }
            if (!alive || !Enabled) { if (_tasks.Count > 0) _tasks.Clear(); Report(shift); return; }
            if (Time.timeScale <= 0f) return;

            // New windows come faster the more stressed (and overtimed) he is.
            _nextSpawn -= Time.deltaTime;
            int max = Mathf.Max(1, CmxPlugin.MaxTasks.Value);
            if (_nextSpawn <= 0f)
            {
                float k = Mathf.Clamp01(shift.Stress / 100f);
                _nextSpawn = Mathf.Lerp(CmxPlugin.TaskEveryMax.Value, CmxPlugin.TaskEveryMin.Value, k) * (shift.Overtime ? 0.7f : 1f) * Random.Range(0.85f, 1.15f);
                if (_tasks.Count < max) Spawn();
            }

            if (_tasks.Count > 0) HandleInput(_tasks[_tasks.Count - 1], shift);
            Report(shift);
        }

        // ------------------------------------------------------------ spawning

        private void Spawn()
        {
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);
            float w = 460f * sc, h = 270f * sc;
            Rect r = new Rect(Random.Range(0.04f, 0.96f) * (Screen.width - w), Random.Range(0.08f, 0.85f) * (Screen.height - h), w, h);
            Task t = new Task { Rect = r, Born = Time.unscaledTime, Kind = (Kind)Random.Range(0, 4) };
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
                    int hours = Random.Range(1, 5), rate = Random.Range(8, 13) * 10, parts = Random.Range(12, 90);
                    t.Header = "Labor " + hours + " hr x $" + rate + "  +  parts $" + parts + "  =  ?";
                    t.Answer = (hours * rate + parts).ToString();
                    break;
            }
            _tasks.Add(t);
            CmxAudio.Play("memo");
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
            for (int i = 0; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
            return -1;
        }

        private void Wrong(Task t, string sound = "fail")
        {
            t.Shake = Time.unscaledTime;
            CmxAudio.Play(sound);
        }

        private void HandleInput(Task t, CmxShift shift)
        {
            switch (t.Kind)
            {
                case Kind.PartNumber:
                case Kind.Invoice:
                {
                    if (Input.GetKeyDown(KeyCode.Backspace) && t.Typed.Length > 0) t.Typed = t.Typed.Substring(0, t.Typed.Length - 1);
                    int d = DigitDown();
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
                        if (!Input.GetKeyDown(ArrowKeys[a])) continue;
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
            }
        }

        private void Complete(Task t, CmxShift shift)
        {
            _tasks.Remove(t);
            CmxAudio.Play("cash");
            CmxNet.TaskDone(shift);
        }

        private void Report(CmxShift shift)
        {
            int open = _tasks.Count;
            Open = open;
            if (open == _sentOpen || !shift) return;
            _sentOpen = open;
            CmxNet.TaskOpen(shift, open);
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
            }
            _label.fontSize = Mathf.RoundToInt(17f * sc);
            _center.fontSize = Mathf.RoundToInt(17f * sc);
            _mono.fontSize = Mathf.RoundToInt(30f * sc);
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
            if (_tasks.Count == 0 || Event.current.type != EventType.Repaint) return;
            GUI.depth = -10;
            float sc = Mathf.Max(0.6f, Screen.height / 1080f);
            Styles(sc);
            for (int i = 0; i < _tasks.Count; i++) DrawTask(_tasks[i], sc, i == _tasks.Count - 1);
        }

        private void DrawTask(Task t, float sc, bool active)
        {
            float age = Time.unscaledTime - t.Born;
            float pop = Mathf.Min(1f, age / 0.12f);
            float shake = Time.unscaledTime - t.Shake < 0.3f ? Mathf.Sin(Time.unscaledTime * 90f) * 8f * sc : 0f;
            Rect r = t.Rect;
            r = new Rect(r.center.x - r.width * pop / 2f + shake, r.center.y - r.height * pop / 2f, r.width * pop, r.height * pop);
            if (pop < 1f) { Box(r, new Color(0.9f, 0.9f, 0.9f, 0.9f)); return; }

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
            GUI.Label(new Rect(inner.x + pad, y, w, 46f * sc), t.Header, _label);
            y += 44f * sc;

            switch (t.Kind)
            {
                case Kind.PartNumber:
                {
                    _mono.normal.textColor = new Color(0f, 0.23f, 0.46f);
                    GUI.Label(new Rect(inner.x + pad, y, w, 40f * sc), "Part # " + Group(t.Answer), _mono);
                    y += 46f * sc;
                    Box(new Rect(inner.x + pad, y, w, 40f * sc), Color.white);
                    _mono.normal.textColor = new Color(0.1f, 0.5f, 0.15f);
                    GUI.Label(new Rect(inner.x + pad, y, w, 40f * sc), Group(t.Typed) + (Blink() ? "_" : " "), _mono);
                    y += 48f * sc;
                    Hint(inner, "Type the part number with the number keys", sc);
                    break;
                }
                case Kind.Invoice:
                {
                    Box(new Rect(inner.x + pad, y + 8f * sc, w, 44f * sc), Color.white);
                    _mono.normal.textColor = new Color(0.1f, 0.5f, 0.15f);
                    GUI.Label(new Rect(inner.x + pad, y + 8f * sc, w, 44f * sc), "$ " + t.Typed + (Blink() ? "_" : " "), _mono);
                    Hint(inner, "Type the total with the number keys", sc);
                    break;
                }
                case Kind.RepairOrder:
                {
                    float lh = 26f * sc;
                    for (int i = 0; i < t.Lines.Length; i++)
                    {
                        bool done = i < t.Step, next = i == t.Step;
                        string mark = done ? "<color=#1A8A2A>[SIGNED]</color>" : next ? "<color=#003B75><b>[" + ArrowGlyph[t.Arrows[i]] + " " + ArrowNames[t.Arrows[i]] + "]</b></color>" : "<color=#888888>[" + ArrowGlyph[t.Arrows[i]] + " " + ArrowNames[t.Arrows[i]] + "]</color>";
                        string line = done ? "<color=#888888>" + t.Lines[i] + "</color>" : t.Lines[i];
                        GUI.Label(new Rect(inner.x + pad, y + i * lh, w * 0.62f, lh), (i + 1) + ". " + line, _label);
                        GUI.Label(new Rect(inner.x + pad + w * 0.6f, y + i * lh, w * 0.4f, lh), mark, _label);
                    }
                    Hint(inner, "Press the arrow keys in order", sc);
                    break;
                }
                case Kind.Story:
                {
                    GUI.Label(new Rect(inner.x + pad, y - 6f * sc, w, 24f * sc), "<b>" + (t.Step == 0 ? "CAUSE:" : "CORRECTION:") + "</b>", _label);
                    y += 20f * sc;
                    for (int i = 0; i < 3; i++)
                        GUI.Label(new Rect(inner.x + pad, y + i * 25f * sc, w, 25f * sc), "<b><color=#003B75>[" + (i + 1) + "]</color></b> " + t.Options[i], _label);
                    Hint(inner, "Press 1, 2 or 3", sc);
                    break;
                }
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

    /// <summary>Specialist → host: a task window was finished, or how many are open.</summary>
    public class CmxTaskMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public bool Done;
        public byte Open;

        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Done); w.Write(Open); }
        public void Deserialize(NetworkReader r) { Body = r.ReadNetworkId(); Done = r.ReadBoolean(); Open = r.ReadByte(); }

        public void OnReceived()
        {
            if (!NetworkServer.active) return;
            GameObject go = Util.FindNetworkObject(Body);
            CmxShift s = go ? go.GetComponent<CmxShift>() : null;
            if (!s) return;
            if (Done) s.ServerTaskDone();
            else s.OpenTasks = Open;
        }
    }
}
