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

namespace BerserkFury
{
    /// <summary>
    /// Battle Legends-style pilot comm window: a portrait box slides in from the left with the pilot's name and a typed
    /// subtitle while the line plays. Only the player flying Berserk Fury sees/hears it.
    ///
    /// Voices: drop your own clips in plugins\BerserkFury\Pilot\&lt;event&gt;\*.wav (events: launch, cpc, drill, boost,
    /// overdrive, hurt, kill, boss, levelup) and a portrait at Pilot\portrait.png. Without clips the line is spoken by
    /// the Windows voice with a radio filter.
    /// </summary>
    public class BerserkFuryPilot : MonoBehaviour
    {
        // -------------------------------------------------------------- lines (original, game-style)
        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            ["launch"] = new[] { "Berserk Fury, launching.", "Organoid link stable. Let's begin.", "All systems green. Don't bore me." },
            ["cpc"] = new[] { "Charged Particle Cannon. Fire!", "Anchors down. Disappear!", "Full power. Burn it all!" },
            ["drill"] = new[] { "Buster Claws, pierce!", "Magneser drill!", "Too close." },
            ["boost"] = new[] { "Ion boosters, full thrust!", "Can you keep up?" },
            ["overdrive"] = new[] { "Organoid drive at maximum!", "Now I'm serious.", "Particle energy, overflowing!" },
            ["hurt"] = new[] { "Armor critical. Not yet!", "Is that all you've got?", "You'll pay for that." },
            ["kill"] = new[] { "Target down.", "Too slow.", "Next." },
            ["boss"] = new[] { "Finally, a real opponent.", "That one was worth fighting." },
            ["levelup"] = new[] { "Sync rate rising.", "Berserk Fury is adapting." },
        };
        static readonly Dictionary<string, float> Cooldown = new Dictionary<string, float>
        {
            ["launch"] = 0f, ["cpc"] = 6f, ["drill"] = 14f, ["boost"] = 16f, ["overdrive"] = 20f,
            ["hurt"] = 25f, ["kill"] = 12f, ["boss"] = 5f, ["levelup"] = 10f,
        };
        static readonly Dictionary<string, float> Chance = new Dictionary<string, float>
        {
            ["drill"] = 0.35f, ["boost"] = 0.4f, ["kill"] = 0.3f,
        };

        public static string PilotName = "VEGA";
        public static string ZoidName = "BERSERK FURY";

        CharacterBody _body;
        HealthComponent _hp;
        BerserkFuryPassiveController _passive;
        EntityStateMachine _weapon, _bodyMachine;
        Type _lastWeapon, _lastBody;
        bool _wasOverdrive, _wasHurt;
        int _lastLevel;
        float _lastAny = -99f;
        readonly Dictionary<string, float> _lastAt = new Dictionary<string, float>();

        // window
        string _text = "";
        float _shownAt = -99f, _holdUntil = -99f;
        static Texture2D _portrait, _white;
        static GUIStyle _name, _sub, _line;

        bool Mine => _body && _body.hasEffectiveAuthority && _body.isPlayerControlled;

        void Start()
        {
            _body = GetComponent<CharacterBody>();
            _hp = GetComponent<HealthComponent>();
            _passive = GetComponent<BerserkFuryPassiveController>();
            _lastLevel = _body ? Mathf.FloorToInt(_body.level) : 1;
            GlobalEventManager.onCharacterDeathGlobal += OnDeath;
            Invoke(nameof(SayLaunch), 2.5f);
        }

        void OnDestroy() { GlobalEventManager.onCharacterDeathGlobal -= OnDeath; }

        void SayLaunch() => Say("launch", true);

        void OnDeath(DamageReport r)
        {
            if (r == null || !_body || r.attackerBody != _body) return;
            if (r.victimIsBoss || (r.victimBody && r.victimBody.isChampion)) Say("boss", true);
            else if (r.victimIsElite) Say("kill");
        }

        void Update()
        {
            if (!Mine) return;
            BerserkFuryVoice.Pump();

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
            if (od && !_wasOverdrive) Say("overdrive", true);
            _wasOverdrive = od;

            bool hurt = _hp && _hp.alive && _hp.combinedHealthFraction < 0.3f;
            if (hurt && !_wasHurt) Say("hurt", true);
            _wasHurt = hurt;

            int lvl = Mathf.FloorToInt(_body.level);
            if (lvl > _lastLevel) Say("levelup");
            _lastLevel = lvl;
        }

        public void Say(string evt, bool important = false)
        {
            if (!Mine || !Lines.TryGetValue(evt, out string[] lines)) return;
            float now = Time.unscaledTime;
            if (_lastAt.TryGetValue(evt, out float last) && now - last < Cooldown[evt]) return;
            if (!important && now - _lastAny < 5f) return;                       // don't chatter
            if (Chance.TryGetValue(evt, out float ch) && Random.value > ch) return;
            _lastAt[evt] = now; _lastAny = now;

            string text = lines[Random.Range(0, lines.Length)];
            float secs = BerserkFuryVoice.Play(evt, text);
            _text = text;
            _shownAt = now;
            _holdUntil = now + Mathf.Max(2.6f, secs + 0.6f, text.Length * 0.07f + 1.2f);
        }

        // -------------------------------------------------------------- comm window
        void OnGUI()
        {
            if (!Mine) return;
            float now = Time.unscaledTime;
            if (now > _holdUntil + 0.35f || string.IsNullOrEmpty(_text)) return;
            EnsureStyles();

            float k = Screen.height / 1080f;
            float w = 560f * k, h = 150f * k;
            float slideIn = Mathf.Clamp01((now - _shownAt) / 0.22f), slideOut = Mathf.Clamp01((now - _holdUntil) / 0.3f);
            float ease = 1f - Mathf.Pow(1f - slideIn, 3f);
            float x = Mathf.Lerp(-w, 24f * k, ease) - slideOut * (w + 30f * k);
            float y = Screen.height * 0.36f;
            Color old = GUI.color;

            // frame: dark panel, magenta trim, scanlines
            Fill(new Rect(x, y, w, h), new Color(0.05f, 0.02f, 0.08f, 0.88f));
            Fill(new Rect(x, y, w, 3f * k), new Color(0.95f, 0.2f, 0.85f, 1f));
            Fill(new Rect(x, y + h - 3f * k, w, 3f * k), new Color(0.95f, 0.2f, 0.85f, 1f));
            Fill(new Rect(x, y, 6f * k, h), new Color(0.95f, 0.2f, 0.85f, 1f));

            // portrait with static flicker on open
            float ps = h - 22f * k;
            Rect pr = new Rect(x + 16f * k, y + 11f * k, ps, ps);
            Fill(pr, new Color(0f, 0f, 0f, 1f));
            float flicker = now - _shownAt < 0.3f ? Random.Range(0.3f, 1f) : 1f;
            GUI.color = new Color(1f, 1f, 1f, flicker);
            GUI.DrawTexture(pr, Portrait(), ScaleMode.ScaleAndCrop);
            GUI.color = old;
            for (float sy = pr.y; sy < pr.yMax; sy += 4f * k) Fill(new Rect(pr.x, sy, pr.width, 1f * k), new Color(0f, 0f, 0f, 0.25f));
            // "talking" bar under the portrait
            float talk = now < _holdUntil - 0.4f ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(now * 17f) * Mathf.Sin(now * 7.3f)) : 0f;
            Fill(new Rect(pr.x, pr.yMax - 5f * k, pr.width * talk, 4f * k), new Color(0.3f, 0.95f, 1f, 0.9f));

            // name + typed subtitle
            float tx = pr.xMax + 16f * k, tw = w - (tx - x) - 14f * k;
            _name.fontSize = Mathf.RoundToInt(26 * k); _sub.fontSize = Mathf.RoundToInt(15 * k); _line.fontSize = Mathf.RoundToInt(24 * k);
            GUI.Label(new Rect(tx, y + 10f * k, tw, 32f * k), PilotName, _name);
            GUI.Label(new Rect(tx, y + 40f * k, tw, 20f * k), ZoidName + "  ·  COMM", _sub);
            int chars = Mathf.Clamp(Mathf.FloorToInt((now - _shownAt - 0.15f) * 45f), 0, _text.Length);
            GUI.Label(new Rect(tx, y + 64f * k, tw, h - 70f * k), _text.Substring(0, chars), _line);
            GUI.color = old;
        }

        static void Fill(Rect r, Color c)
        {
            if (!_white) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
            Color old = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = old;
        }

        static void EnsureStyles()
        {
            if (_name != null) return;
            _name = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.35f, 0.9f) } };
            _sub = new GUIStyle(GUI.skin.label) { normal = { textColor = new Color(0.55f, 0.9f, 1f) } };
            _line = new GUIStyle(GUI.skin.label) { wordWrap = true, normal = { textColor = Color.white } };
        }

        /// <summary>Pilot\portrait.png if present, else a generated comm silhouette.</summary>
        static Texture2D Portrait()
        {
            if (_portrait) return _portrait;
            try
            {
                string p = BerserkFuryModel.FindFile(Path.Combine("Pilot", "portrait.png"));
                if (p != null)
                {
                    _portrait = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    _portrait.LoadImage(File.ReadAllBytes(p));
                    return _portrait;
                }
            }
            catch { }
            const int S = 128;
            _portrait = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x - S / 2f) / S, v = (float)y / S;
                    Color c = Color.Lerp(new Color(0.12f, 0.03f, 0.18f), new Color(0.35f, 0.06f, 0.4f), v);
                    bool head = (u * u) / 0.034f + ((v - 0.58f) * (v - 0.58f)) / 0.05f < 1f;          // helmet
                    bool shoulders = v < 0.36f && Mathf.Abs(u) < 0.42f - v * 0.6f + 0.18f;
                    if (head || shoulders) c = new Color(0.06f, 0.05f, 0.09f);
                    if (head && v > 0.55f && v < 0.63f && Mathf.Abs(u) < 0.15f) c = new Color(1f, 0.15f, 0.35f);  // visor
                    if (head && Mathf.Abs(v - 0.74f) < 0.01f) c = new Color(0.9f, 0.3f, 0.95f);              // helmet trim
                    _portrait.SetPixel(x, y, c);
                }
            _portrait.Apply();
            return _portrait;
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
        public static float Play(string evt, string text)
        {
            try
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
            Speak(text);
            return text.Length * 0.065f + 0.5f;
        }

        static void Speak(string text)
        {
            if (_broken || Application.platform != RuntimePlatform.WindowsPlayer) return;
            try
            {
                if (!_started) StartTts();
                if (_proc == null || _proc.HasExited) { _broken = true; return; }
                string path = Path.Combine(_dir, "p" + (_n++) + ".wav");
                // cold, clipped delivery: a little faster and lower
                string ssml = "<speak version=\"1.0\" xml:lang=\"en-US\"><prosody rate=\"+8%\" pitch=\"-12%\">" +
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
