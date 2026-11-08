using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using EntityStates;
using RoR2;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;
using Path = System.IO.Path;

namespace BerserkFury
{
    /// <summary>
    /// Battle Legends-style comm chatter. The pilot (VEGA) trash-talks and reacts through the fight; some moments play a
    /// short exchange with mission COMMAND. Each line slides in a portrait window with a typed subtitle and a radio voice.
    /// Only the player flying Berserk Fury sees/hears it.
    ///
    /// Real clips: plugins\BerserkFury\Pilot\&lt;event&gt;\*.wav replace the pilot's line for that event (see README).
    /// </summary>
    public class BerserkFuryPilot : MonoBehaviour
    {
        // "V:" = pilot, "C:" = command. One entry = one exchange. All lines are original.
        static readonly Dictionary<string, string[][]> Script = new Dictionary<string, string[][]>
        {
            ["launch"] = new[] {
                new[] { "V:Berserk Fury, launching.", "C:Organoid link stable. You're cleared to engage.", "V:Finally." },
                new[] { "V:All systems green. Don't bore me." },
                new[] { "C:Vega, your sync rate is off the charts.", "V:Of course it is. Let's go, Fury." },
                new[] { "V:Another planet full of insects. Fine." },
            },
            ["battle"] = new[] {
                new[] { "C:Massive energy reading on the teleporter!", "V:Good. I was getting bored." },
                new[] { "V:So this is the welcoming party?" },
                new[] { "C:Multiple hostiles converging. Be careful.", "V:Careful is for people who lose." },
                new[] { "V:Come on. Show me something worth fighting." },
            },
            ["tpdone"] = new[] {
                new[] { "C:Teleporter charged. Nice work.", "V:That was barely a warm-up." },
                new[] { "V:Done already? Next." },
            },
            ["cpc"] = new[] {
                new[] { "V:Charged Particle Cannon. Fire!" },
                new[] { "V:Anchors down. Disappear!" },
                new[] { "V:Full power. Burn it all!" },
                new[] { "V:Get out of my sight!" },
            },
            ["drill"] = new[] { new[] { "V:Buster Claws, pierce!" }, new[] { "V:Too close." }, new[] { "V:Magneser drill!" } },
            ["boost"] = new[] { new[] { "V:Ion boosters, full thrust!" }, new[] { "V:Can you even keep up?" } },
            ["overdrive"] = new[] {
                new[] { "V:Organoid drive at maximum!", "C:Output's past the safety limit!", "V:Then stop watching and enjoy it." },
                new[] { "V:Now I'm serious." },
                new[] { "V:Particle energy, overflowing!" },
            },
            ["streak"] = new[] {
                new[] { "V:Is that the best you've got?" }, new[] { "V:Pathetic." }, new[] { "V:Line up. I'll take you all." },
                new[] { "V:Ha! Too slow!" }, new[] { "V:Fury, keep going. Don't let them breathe." },
            },
            ["bighit"] = new[] {
                new[] { "V:Tch! Lucky shot." }, new[] { "V:You'll pay for that." },
                new[] { "C:Heavy damage to the left flank!", "V:I felt it. Shut up." },
            },
            ["hurt"] = new[] {
                new[] { "C:Armor critical! Pull back!", "V:Not yet! Fury can still fight!" },
                new[] { "V:I won't lose. Not here." },
                new[] { "V:Is that all you've got?" },
            },
            ["kill"] = new[] { new[] { "V:Target down." }, new[] { "V:Next." }, new[] { "V:Too slow." }, new[] { "V:Stay down." } },
            ["boss"] = new[] {
                new[] { "V:Finally, a real opponent.", "C:Target neutralized!", "V:It was fun while it lasted." },
                new[] { "V:That one almost made me try." },
            },
            ["idle"] = new[] {
                new[] { "V:Where are the strong ones hiding?" },
                new[] { "C:Vega, check your heat levels.", "V:Fury's fine. Worry about them." },
                new[] { "V:This planet is full of weaklings." },
                new[] { "V:Stay sharp, Fury. Something's watching us." },
                new[] { "V:Hmph. I expected more." },
            },
            ["levelup"] = new[] { new[] { "C:Sync rate rising.", "V:Fury is learning." }, new[] { "V:We're getting stronger." } },
            ["ally"] = new[] { new[] { "C:We lost a teammate!", "V:Weak. I'll win this alone if I have to." }, new[] { "V:Get up. I'm not carrying you." } },
            ["death"] = new[] { new[] { "V:No... Fury...!", "C:Vega! Vega, respond!" }, new[] { "V:This... isn't over..." } },
            ["revive"] = new[] { new[] { "V:Did you miss me?" }, new[] { "C:Systems back online.", "V:Round two." } },
        };
        static readonly Dictionary<string, float> Cooldown = new Dictionary<string, float>
        {
            ["launch"] = 0f, ["battle"] = 30f, ["tpdone"] = 30f, ["cpc"] = 6f, ["drill"] = 14f, ["boost"] = 16f, ["overdrive"] = 20f,
            ["streak"] = 20f, ["bighit"] = 18f, ["hurt"] = 25f, ["kill"] = 14f, ["boss"] = 5f, ["idle"] = 0f, ["levelup"] = 12f,
            ["ally"] = 15f, ["death"] = 0f, ["revive"] = 0f,
        };
        static readonly Dictionary<string, float> Chance = new Dictionary<string, float>
        {
            ["drill"] = 0.35f, ["boost"] = 0.4f, ["kill"] = 0.3f, ["streak"] = 0.7f, ["bighit"] = 0.6f,
        };

        CharacterBody _body;
        HealthComponent _hp;
        BerserkFuryPassiveController _passive;
        EntityStateMachine _weapon, _bodyMachine;
        Type _lastWeapon, _lastBody;
        bool _wasOverdrive, _wasHurt;
        int _lastLevel;
        float _lastAny = -99f, _nextIdle;
        readonly Dictionary<string, float> _lastAt = new Dictionary<string, float>();
        readonly Queue<float> _hits = new Queue<float>();
        static Stage _lastStage;

        bool Mine => _body && _body.hasEffectiveAuthority && _body.isPlayerControlled;

        void Start()
        {
            _body = GetComponent<CharacterBody>();
            _hp = GetComponent<HealthComponent>();
            _passive = GetComponent<BerserkFuryPassiveController>();
            _lastLevel = _body ? Mathf.FloorToInt(_body.level) : 1;
            _nextIdle = Time.unscaledTime + Random.Range(40f, 70f);
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            GlobalEventManager.onServerDamageDealt += OnDamage;
            TeleporterInteraction.onTeleporterBeginChargingGlobal += OnTpStart;
            TeleporterInteraction.onTeleporterChargedGlobal += OnTpDone;
            // second spawn in the same stage = a revive
            bool revived = Stage.instance && _lastStage == Stage.instance;
            _lastStage = Stage.instance;
            Invoke(revived ? nameof(SayRevive) : nameof(SayLaunch), 2.5f);
        }

        void OnDestroy()
        {
            GlobalEventManager.onCharacterDeathGlobal -= OnDeath;
            GlobalEventManager.onServerDamageDealt -= OnDamage;
            TeleporterInteraction.onTeleporterBeginChargingGlobal -= OnTpStart;
            TeleporterInteraction.onTeleporterChargedGlobal -= OnTpDone;
        }

        void SayLaunch() => Say("launch", true);
        void SayRevive() => Say("revive", true);
        void OnTpStart(TeleporterInteraction t) => Say("battle", true);
        void OnTpDone(TeleporterInteraction t) => Say("tpdone", true);

        void OnDeath(DamageReport r)
        {
            if (r == null || !_body) return;
            if (r.victimBody == _body) { Say("death", true); return; }
            if (r.victimBody && r.victimBody.isPlayerControlled) { Say("ally", true); return; }
            if (r.attackerBody != _body) return;
            if (r.victimIsBoss || (r.victimBody && r.victimBody.isChampion)) Say("boss", true);
            else if (r.victimIsElite) Say("kill");
        }

        void OnDamage(DamageReport r)
        {
            if (r == null || !_body || r.damageInfo == null) return;
            if (r.attackerBody == _body)
            {
                float now = Time.unscaledTime;
                _hits.Enqueue(now);
                while (_hits.Count > 0 && now - _hits.Peek() > 2.5f) _hits.Dequeue();
                if (_hits.Count >= 12) { _hits.Clear(); Say("streak"); }
            }
            else if (r.victimBody == _body && _hp && r.damageDealt > _hp.fullCombinedHealth * 0.18f) Say("bighit");
        }

        void Update()
        {
            if (!Mine) return;

            if (!_weapon) _weapon = EntityStateMachine.FindByCustomName(gameObject, "Weapon");
            if (!_bodyMachine) _bodyMachine = EntityStateMachine.FindByCustomName(gameObject, "Body");
            Type w = _weapon && _weapon.state != null ? _weapon.state.GetType() : null;
            Type b = _bodyMachine && _bodyMachine.state != null ? _bodyMachine.state.GetType() : null;
            if (w != _lastWeapon && w != null)
            {
                if (w == typeof(ChargedParticleCannonState)) Say("cpc", true);
                else if (w == typeof(MagneserDrillState)) Say("drill");
            }
            if (b != _lastBody && b == typeof(IonThrusterSurgeState)) Say("boost");
            _lastWeapon = w; _lastBody = b;

            bool od = _passive && _passive.IsOverdrive;
            if (od && !_wasOverdrive) { BerserkFurySfx.Play("overdrive"); Say("overdrive", true); }
            _wasOverdrive = od;

            bool hurt = _hp && _hp.alive && _hp.combinedHealthFraction < 0.3f;
            if (hurt && !_wasHurt) Say("hurt", true);
            _wasHurt = hurt;

            int lvl = Mathf.FloorToInt(_body.level);
            if (lvl > _lastLevel) Say("levelup");
            _lastLevel = lvl;

            // banter during fights when nothing else has been said for a while
            float now = Time.unscaledTime;
            if (now > _nextIdle)
            {
                _nextIdle = now + Random.Range(45f, 80f);
                if (now - _lastAny > 25f && _body.outOfCombatStopwatch < 4f) Say("idle", true);
            }
        }

        public void Say(string evt, bool important = false)
        {
            if (!Mine || !Script.TryGetValue(evt, out string[][] options)) return;
            float now = Time.unscaledTime;
            if (_lastAt.TryGetValue(evt, out float last) && now - last < Cooldown[evt]) return;
            if (!important && now - _lastAny < 6f) return;
            if (BerserkFuryComms.Busy && !important) return;
            if (Chance.TryGetValue(evt, out float ch) && Random.value > ch) return;
            _lastAt[evt] = now; _lastAny = now;
            BerserkFuryComms.Play(evt, options[Random.Range(0, options.Length)], evt == "cpc" || evt == "death");
        }
    }

    /// <summary>The comm window + line queue. One per game (survives the body dying so last words finish).</summary>
    public class BerserkFuryComms : MonoBehaviour
    {
        public static string PilotName = "VEGA", CommandName = "COMMAND", ZoidName = "BERSERK FURY";

        static BerserkFuryComms _inst;
        readonly Queue<string> _queue = new Queue<string>();
        string _evt, _text = "";
        bool _pilot = true, _firstOfExchange;
        float _shownAt = -99f, _holdUntil = -99f;
        static Texture2D _portraitV, _portraitC, _white;
        static GUIStyle _name, _sub, _line;

        public static bool Busy => _inst && (_inst._queue.Count > 0 || Time.unscaledTime < _inst._holdUntil);

        public static void Play(string evt, string[] lines, bool interrupt)
        {
            if (!_inst)
            {
                GameObject go = new GameObject("BerserkFuryComms");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<BerserkFuryComms>();
            }
            if (interrupt) { _inst._queue.Clear(); _inst._holdUntil = 0f; }
            else if (_inst._queue.Count > 2) return;
            _inst._evt = evt;
            _inst._firstOfExchange = true;
            foreach (string l in lines) _inst._queue.Enqueue(l);
        }

        void Update()
        {
            BerserkFuryVoice.Pump();
            float now = Time.unscaledTime;
            if (now < _holdUntil || _queue.Count == 0) return;
            string l = _queue.Dequeue();
            _pilot = !l.StartsWith("C:");
            string text = l.Length > 2 && l[1] == ':' ? l.Substring(2) : l;
            // the event's custom clip (if any) replaces the pilot's first line
            string clipEvt = _pilot && _firstOfExchange ? _evt : null;
            _firstOfExchange = false;
            if (_shownAt < now - 1f) BerserkFurySfx.Play("comm");
            float secs = BerserkFuryVoice.Play(clipEvt, text, _pilot);
            bool continuing = now - _holdUntil < 0.5f;
            _text = text;
            if (!continuing) _shownAt = now;
            _lineAt = now;
            _holdUntil = now + Mathf.Max(1.8f, secs + 0.35f, text.Length * 0.06f + 0.9f);
        }

        float _lineAt;

        void OnGUI()
        {
            float now = Time.unscaledTime;
            if (string.IsNullOrEmpty(_text) || (now > _holdUntil + 0.35f && _queue.Count == 0)) return;
            EnsureStyles();

            float k = Screen.height / 1080f;
            float w = 560f * k, h = 150f * k;
            float slideIn = Mathf.Clamp01((now - _shownAt) / 0.22f);
            float slideOut = _queue.Count == 0 ? Mathf.Clamp01((now - _holdUntil) / 0.3f) : 0f;
            float ease = 1f - Mathf.Pow(1f - slideIn, 3f);
            float x = Mathf.Lerp(-w, 24f * k, ease) - slideOut * (w + 30f * k);
            float y = Screen.height * 0.36f;
            Color trim = _pilot ? new Color(0.95f, 0.2f, 0.85f) : new Color(0.25f, 0.85f, 1f);
            Color old = GUI.color;

            Fill(new Rect(x, y, w, h), new Color(0.05f, 0.02f, 0.08f, 0.88f));
            Fill(new Rect(x, y, w, 3f * k), trim);
            Fill(new Rect(x, y + h - 3f * k, w, 3f * k), trim);
            Fill(new Rect(x, y, 6f * k, h), trim);

            float ps = h - 22f * k;
            Rect pr = new Rect(x + 16f * k, y + 11f * k, ps, ps);
            Fill(pr, Color.black);
            float flicker = now - _lineAt < 0.25f ? Random.Range(0.3f, 1f) : 1f;
            GUI.color = new Color(1f, 1f, 1f, flicker);
            GUI.DrawTexture(pr, _pilot ? PortraitV() : PortraitC(), ScaleMode.ScaleAndCrop);
            GUI.color = old;
            for (float sy = pr.y; sy < pr.yMax; sy += 4f * k) Fill(new Rect(pr.x, sy, pr.width, 1f * k), new Color(0f, 0f, 0f, 0.25f));
            float talk = now < _holdUntil - 0.3f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(now * 17f) * Mathf.Sin(now * 7.3f)) : 0f;
            Fill(new Rect(pr.x, pr.yMax - 5f * k, pr.width * talk, 4f * k), new Color(trim.r, trim.g, trim.b, 0.9f));

            float tx = pr.xMax + 16f * k, tw = w - (tx - x) - 14f * k;
            _name.fontSize = Mathf.RoundToInt(26 * k); _sub.fontSize = Mathf.RoundToInt(15 * k); _line.fontSize = Mathf.RoundToInt(24 * k);
            _name.normal.textColor = trim;
            GUI.Label(new Rect(tx, y + 10f * k, tw, 32f * k), _pilot ? PilotName : CommandName, _name);
            GUI.Label(new Rect(tx, y + 40f * k, tw, 20f * k), (_pilot ? ZoidName : "MISSION CONTROL") + "  ·  COMM", _sub);
            int chars = Mathf.Clamp(Mathf.FloorToInt((now - _lineAt - 0.1f) * 45f), 0, _text.Length);
            GUI.Label(new Rect(tx, y + 64f * k, tw, h - 70f * k), _text.Substring(0, chars), _line);
        }

        static void Fill(Rect r, Color c)
        {
            if (!_white) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = old;
        }

        static void EnsureStyles()
        {
            if (_name != null) return;
            _name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _sub = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.55f, 0.9f, 1f) } };
            _line = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = Color.white } };
        }

        static Texture2D LoadPng(string name)
        {
            try
            {
                string p = BerserkFuryModel.FindFile(Path.Combine("Pilot", name));
                if (p == null) return null;
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                t.LoadImage(File.ReadAllBytes(p));
                return t;
            }
            catch { return null; }
        }

        static Texture2D PortraitV() => _portraitV ? _portraitV : (_portraitV = LoadPng("portrait.png") ?? Silhouette(new Color(0.35f, 0.06f, 0.4f), new Color(1f, 0.15f, 0.35f), false));
        static Texture2D PortraitC() => _portraitC ? _portraitC : (_portraitC = LoadPng("command.png") ?? Silhouette(new Color(0.05f, 0.2f, 0.32f), new Color(0.3f, 0.9f, 1f), true));

        /// <summary>Generated comm silhouette: helmeted pilot (visor) or operator (headset).</summary>
        static Texture2D Silhouette(Color bg, Color accent, bool headset)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x - S / 2f) / S, v = (float)y / S;
                    Color c = Color.Lerp(bg * 0.4f, bg, v);
                    bool head = (u * u) / 0.03f + ((v - 0.58f) * (v - 0.58f)) / 0.045f < 1f;
                    bool shoulders = v < 0.36f && Mathf.Abs(u) < 0.6f - v * 0.6f;
                    if (head || shoulders) c = new Color(0.06f, 0.05f, 0.09f);
                    if (!headset && head && v > 0.55f && v < 0.63f && Mathf.Abs(u) < 0.15f) c = accent;           // visor slit
                    if (headset && Mathf.Abs(Mathf.Sqrt(u * u + (v - 0.6f) * (v - 0.6f)) - 0.2f) < 0.012f && v > 0.6f) c = accent; // headband
                    if (headset && v > 0.47f && v < 0.6f && Mathf.Abs(Mathf.Abs(u) - 0.19f) < 0.03f) c = accent;   // ear cups
                    if (headset && v > 0.46f && v < 0.48f && u > 0.0f && u < 0.19f) c = accent;                    // mic
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }
    }

    /// <summary>Plays a pilot clip (custom .wav if present, else Windows TTS through a radio filter) with winmm.</summary>
    public static class BerserkFuryVoice
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        static extern int Mci(string command, StringBuilder ret, int len, IntPtr hwnd);

        static Process _proc;
        static bool _broken, _started;
        static string _dir;
        static int _n;
        static string _playing;
        static readonly ConcurrentQueue<string> Done = new ConcurrentQueue<string>();

        /// <summary>Starts the line; returns its length in seconds if known (custom clip), else an estimate.</summary>
        public static float Play(string evt, string text, bool pilot = true)
        {
            if (evt != null) try
            {
                string folder = BerserkFuryModel.FindFile(Path.Combine("Pilot", evt));
                if (folder == null)
                {
                    string root = BerserkFuryModel.FindFile("BerserkFury.obj");
                    if (root != null) folder = Path.Combine(Path.GetDirectoryName(root), "Pilot", evt);
                }
                if (folder != null && Directory.Exists(folder))
                {
                    var clips = new List<string>(Directory.GetFiles(folder, "*.wav"));
                    clips.AddRange(Directory.GetFiles(folder, "*.mp3"));
                    if (clips.Count > 0) { string f = clips[Random.Range(0, clips.Count)]; Open(f, f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)); return 3f; }
                }
            }
            catch (Exception e) { BerserkFuryPlugin.Log.LogDebug("Pilot clip: " + e.Message); }
            Speak(text, pilot);
            return text.Length * 0.065f + 0.5f;
        }

        static void Speak(string text, bool pilot)
        {
            if (_broken || Application.platform != RuntimePlatform.WindowsPlayer) return;
            try
            {
                if (!_started) StartTts();
                if (_proc == null || _proc.HasExited) { _broken = true; return; }
                string path = Path.Combine(_dir, "p" + (_n++) + ".wav");
                // cold, clipped delivery: a little faster and lower
                string ssml = "<speak version=\"1.0\" xml:lang=\"en-US\"><prosody rate=\"" + (pilot ? "+8%" : "+2%") + "\" pitch=\"" + (pilot ? "-12%" : "+14%") + "\">" +
                              System.Security.SecurityElement.Escape(text) + "</prosody></speak>";
                _proc.StandardInput.WriteLine(path + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(ssml)));
                _proc.StandardInput.Flush();
            }
            catch (Exception e) { _broken = true; BerserkFuryPlugin.Log.LogWarning("Pilot voice off: " + e.Message); }
        }

        static void StartTts()
        {
            _started = true;
            _dir = Path.Combine(Path.GetTempPath(), "BerserkFuryPilot");
            Directory.CreateDirectory(_dir);
            foreach (string f in Directory.GetFiles(_dir, "*.wav")) { try { File.Delete(f); } catch { } }
            // Text arrives base64-encoded, so nothing in it can run as a command. Prefers a male voice if installed.
            string script =
                "Add-Type -AssemblyName System.Speech\n" +
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer\n" +
                "try { $m = $s.GetInstalledVoices() | ? { $_.VoiceInfo.Gender -eq 'Male' } | select -First 1; if ($m) { $s.SelectVoice($m.VoiceInfo.Name) } } catch {}\n" +
                "while (($l = [Console]::In.ReadLine()) -ne $null) {\n" +
                "  $p = $l.Split('|'); if ($p.Length -ne 2) { continue }\n" +
                "  try { $s.SetOutputToWaveFile($p[0]); $s.SpeakSsml([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p[1]))); $s.SetOutputToNull(); [Console]::Out.WriteLine('OK|' + $p[0]) }\n" +
                "  catch { [Console]::Out.WriteLine('ERR|' + $p[0]) }\n" +
                "  [Console]::Out.Flush()\n" +
                "}";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + encoded)
            { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, CreateNoWindow = true };
            _proc = Process.Start(psi);
            _proc.OutputDataReceived += (s, e) => { if (e.Data != null) Done.Enqueue(e.Data); };
            _proc.BeginOutputReadLine();
            Application.quitting += () => { try { if (_proc != null && !_proc.HasExited) _proc.Kill(); } catch { } };
        }

        public static void Pump()
        {
            while (Done.TryDequeue(out string line))
            {
                int bar = line.IndexOf('|');
                if (bar < 0) continue;
                string path = line.Substring(bar + 1);
                if (line.StartsWith("OK")) { try { Radio(path); } catch { } Open(path, false); }
            }
        }

        static void Open(string path, bool mp3)
        {
            try
            {
                if (_playing != null) { Mci("stop " + _playing, null, 0, IntPtr.Zero); Mci("close " + _playing, null, 0, IntPtr.Zero); }
                string alias = "bfpilot" + (_n++);
                if (Mci("open \"" + path + "\" type " + (mp3 ? "mpegvideo" : "waveaudio") + " alias " + alias, null, 0, IntPtr.Zero) != 0) return;
                Mci("play " + alias, null, 0, IntPtr.Zero);
                _playing = alias;
            }
            catch { }
        }

        /// <summary>Comm-radio filter on a 16-bit PCM wav: band-pass (~300-3400 Hz), soft clip, faint hiss + squelch clicks.</summary>
        static void Radio(string path)
        {
            byte[] wav = File.ReadAllBytes(path);
            if (wav.Length < 64 || Encoding.ASCII.GetString(wav, 0, 4) != "RIFF") return;
            int ch = BitConverter.ToInt16(wav, 22), rate = BitConverter.ToInt32(wav, 24), bits = BitConverter.ToInt16(wav, 34);
            if (bits != 16) return;
            int pos = 12, dataAt = -1, dataLen = 0;
            while (pos + 8 <= wav.Length)
            {
                string id = Encoding.ASCII.GetString(wav, pos, 4); int len = BitConverter.ToInt32(wav, pos + 4);
                if (id == "data") { dataAt = pos + 8; dataLen = Math.Min(len, wav.Length - dataAt); break; }
                pos += 8 + len + (len & 1);
            }
            if (dataAt < 0) return;
            int n = dataLen / 2;
            float dtv = 1f / rate;
            float hpA = 1f / (1f + dtv * 2f * Mathf.PI * 300f), lpA = dtv / (dtv + 1f / (2f * Mathf.PI * 3400f));
            float prevIn = 0, prevHp = 0, lp = 0;
            var rng = new System.Random(n);
            for (int i = 0; i < n; i++)
            {
                int o = dataAt + i * 2;
                float s = BitConverter.ToInt16(wav, o) / 32768f;
                float hp = hpA * (prevHp + s - prevIn); prevIn = s; prevHp = hp;
                lp += lpA * (hp - lp);
                float y = (float)Math.Tanh(lp * 2.2f) * 0.8f;
                y += (float)(rng.NextDouble() - 0.5) * 0.012f;
                int frame = i / ch;
                if (frame < rate / 40 || frame > n / ch - rate / 40) y += (float)(rng.NextDouble() - 0.5) * 0.25f;   // squelch
                short v = (short)Mathf.Clamp(y * 32767f, -32768f, 32767f);
                wav[o] = (byte)(v & 0xff); wav[o + 1] = (byte)((v >> 8) & 0xff);
            }
            File.WriteAllBytes(path, wav);
        }
    }
}

namespace BerserkFury
{
    /// <summary>
    /// Optional custom sound effects: plugins\BerserkFury\Sfx\&lt;name&gt;.wav (m1, drill, boost, cpc_charge, cpc_fire,
    /// overdrive, comm). If the file is there it plays (winmm, this PC only) instead of the vanilla sound.
    /// </summary>
    public static class BerserkFurySfx
    {
        [System.Runtime.InteropServices.DllImport("winmm.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, EntryPoint = "mciSendStringW")]
        static extern int Mci(string command, System.Text.StringBuilder ret, int len, System.IntPtr hwnd);

        static readonly System.Collections.Generic.Dictionary<string, string> Found = new System.Collections.Generic.Dictionary<string, string>();
        static int _n;
        static readonly string[] Pool = new string[8];

        public static float Volume = 0.8f;

        static string FileFor(string name)
        {
            if (Found.TryGetValue(name, out string f)) return f;
            f = null;
            try
            {
                string obj = BerserkFuryModel.FindFile("BerserkFury.obj");
                if (obj != null)
                {
                    string dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(obj), "Sfx");
                    foreach (string ext in new[] { ".wav", ".mp3" })
                    {
                        string p = System.IO.Path.Combine(dir, name + ext);
                        if (System.IO.File.Exists(p)) { f = p; break; }
                    }
                }
            }
            catch { }
            Found[name] = f;
            return f;
        }

        /// <summary>True if a custom sound played (so the caller skips its vanilla sound).</summary>
        public static bool Play(string name)
        {
            if (UnityEngine.Application.platform != UnityEngine.RuntimePlatform.WindowsPlayer) return false;
            string f = FileFor(name);
            if (f == null) return false;
            try
            {
                int slot = _n++ % Pool.Length;
                if (Pool[slot] != null) { Mci("close " + Pool[slot], null, 0, System.IntPtr.Zero); }
                string alias = "bfsfx" + _n;
                bool mp3 = f.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase);
                if (Mci("open \"" + f + "\" type " + (mp3 ? "mpegvideo" : "waveaudio") + " alias " + alias, null, 0, System.IntPtr.Zero) != 0) return false;
                if (mp3) Mci("setaudio " + alias + " volume to " + (int)(Volume * 1000), null, 0, System.IntPtr.Zero);
                Mci("play " + alias, null, 0, System.IntPtr.Zero);
                Pool[slot] = alias;
                return true;
            }
            catch { return false; }
        }
    }
}
