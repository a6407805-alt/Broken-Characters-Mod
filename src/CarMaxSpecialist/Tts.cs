using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using R2API.Networking;
using R2API.Networking.Interfaces;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace CarMaxSpecialist
{
    /// <summary>
    /// Spoken lines in 3D. The Windows voice (SAPI, through one hidden PowerShell) renders each line to a temp .wav, and
    /// winmm plays it with volume and left/right pan worked out from where the speaker is relative to the camera
    /// (Unity's own audio is off in RoR2, so no AudioSource). Fast fly-bys bend the pitch a little (Doppler).
    /// Bubbles are said on every client already, so each client voices them itself; pop-up prompts go through a
    /// network message so everybody hears the Specialist's windows.
    /// </summary>
    public static class CmxTts
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
        private static extern int Mci(string command, StringBuilder ret, int len, IntPtr hwnd);

        private class Req { public Transform At; public Vector3 Pos; }

        private class Voice
        {
            public string Alias, File;
            public Transform At;
            public Vector3 Pos;
            public float Born;
            public int LastL = -1, LastR = -1;
        }

        private static Process _proc;
        private static bool _broken, _started, _splitBroken;
        private static string _dir;
        private static int _n;
        private static string _last = "";
        private static float _lastAt = -10f;
        private static readonly Dictionary<string, Req> Pending = new Dictionary<string, Req>();
        private static readonly ConcurrentQueue<string> Done = new ConcurrentQueue<string>();
        private static readonly List<Voice> Playing = new List<Voice>();
        private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Junk = new Regex("[#*_\\[\\]{}|~^<>$`]", RegexOptions.Compiled);

        public static bool On => !_broken && Application.platform == RuntimePlatform.WindowsPlayer && CmxPlugin.Tts != null && CmxPlugin.Tts.Value;

        // ------------------------------------------------------------ public API

        /// <summary>The owner speaks (pop-ups, banners): everyone nearby hears it from his body.</summary>
        public static void Say(string text)
        {
            CharacterBody me = null;
            try { me = LocalUserManager.GetFirstLocalUser()?.cachedBody; } catch { }
            if (me) SayNet(me, text);
        }

        /// <summary>A speech bubble: voiced from where its anchor is, on this client only.</summary>
        public static void SayFrom(Transform anchor, Vector3 pos, string text)
        {
            Request(anchor, pos, text);
        }

        public static void SayNet(CharacterBody body, string text)
        {
            if (!On || !body || string.IsNullOrEmpty(text)) return;
            string t = Clean(text);
            if (t == null) return;
            NetworkIdentity ni = body.GetComponent<NetworkIdentity>();
            if (!ni) { Request(body.gameObject.transform, body.corePosition, t); return; }
            try
            {
                bool host = NetworkServer.active;
                new CmxSpeakMessage { Body = ni.netId, Text = t, Relayed = (byte)(host ? 1 : 0) }.Send(host ? NetworkDestination.Clients : NetworkDestination.Server);
            }
            catch (Exception e) { CmxPlugin.Log.LogDebug("TTS send: " + e.Message); Request(body.gameObject.transform, body.corePosition, t); }
        }

        public static void PlayRemote(NetworkInstanceId id, string text)
        {
            GameObject go = id == NetworkInstanceId.Invalid ? null : Util.FindNetworkObject(id);
            if (!go) return;
            Request(go.transform, go.transform.position + Vector3.up * 1.5f, text);
        }

        public static string Clean(string text)
        {
            string t = Junk.Replace(Tags.Replace(text ?? "", ""), " ").Replace("\r", " ").Replace("\n", ". ").Trim();
            if (t.Length < 2) return null;
            return t.Length > 200 ? t.Substring(0, 200) : t;
        }

        // ------------------------------------------------------------ synthesis

        private static void Start()
        {
            _started = true;
            try
            {
                _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CarMaxTts");
                System.IO.Directory.CreateDirectory(_dir);
                foreach (string f in System.IO.Directory.GetFiles(_dir, "*.wav")) { try { System.IO.File.Delete(f); } catch { } }

                // The text arrives base64-encoded, so nothing a player says can be run as a command.
                string script =
                    "Add-Type -AssemblyName System.Speech\\n" +
                    "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer\\n" +
                    "while (($l = [Console]::In.ReadLine()) -ne $null) {\\n" +
                    "  $p = $l.Split('|')\\n" +
                    "  if ($p.Length -ne 3) { continue }\\n" +
                    "  try {\\n" +
                    "    $s.Rate = [int]$p[1]\\n" +
                    "    $s.SetOutputToWaveFile($p[0])\\n" +
                    "    $s.Speak([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p[2])))\\n" +
                    "    $s.SetOutputToNull()\\n" +
                    "    [Console]::Out.WriteLine('OK|' + $p[0])\\n" +
                    "  } catch { [Console]::Out.WriteLine('ERR|' + $p[0]) }\\n" +
                    "  [Console]::Out.Flush()\\n" +
                    "}";
                script = script.Replace("\\n", "\n");
                string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " + encoded)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                _proc = Process.Start(psi);
                _proc.OutputDataReceived += (s, e) => { if (e.Data != null) Done.Enqueue(e.Data); };
                _proc.BeginOutputReadLine();
                Application.quitting += Stop;
            }
            catch (Exception e)
            {
                _broken = true;
                CmxPlugin.Log.LogWarning("Text to speech unavailable: " + e.Message);
            }
        }

        public static void Stop()
        {
            try { if (_proc != null && !_proc.HasExited) _proc.Kill(); } catch { }
            _proc = null;
            foreach (Voice v in Playing) Close(v);
            Playing.Clear();
        }

        private static Vector3 ListenerPos()
        {
            Camera c = Camera.main;
            if (c) return c.transform.position;
            try { CharacterBody b = LocalUserManager.GetFirstLocalUser()?.cachedBody; if (b) return b.corePosition; } catch { }
            return Vector3.zero;
        }

        private static void Request(Transform at, Vector3 pos, string text)
        {
            if (!On || string.IsNullOrEmpty(text)) return;
            try
            {
                string t = Clean(text);
                if (t == null) return;
                if (at) pos = at.position;
                float max = Mathf.Max(5f, CmxPlugin.TtsMax.Value);
                if (Vector3.Distance(ListenerPos(), pos) > max) return; // too far to hear anyway
                float now = Time.unscaledTime;
                if (t == _last && now - _lastAt < 2f) return;
                _last = t;
                _lastAt = now;
                if (!_started) Start();
                if (_broken || _proc == null || _proc.HasExited) { _broken = true; return; }
                if (Pending.Count >= 4) return; // already behind, drop it
                string path = System.IO.Path.Combine(_dir, "v" + (_n++) + ".wav");
                Pending[path] = new Req { At = at, Pos = pos };
                int rate = Mathf.Clamp(CmxPlugin.TtsRate.Value, -10, 10);
                _proc.StandardInput.WriteLine(path + "|" + rate + "|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(t)));
                _proc.StandardInput.Flush();
            }
            catch (Exception e)
            {
                _broken = true;
                CmxPlugin.Log.LogWarning("Text to speech stopped: " + e.Message);
            }
        }

        // ------------------------------------------------------------ playback (called every frame from the plugin)

        public static void Pump()
        {
            if (!_started || _broken) return;
            try
            {
                while (Done.TryDequeue(out string line))
                {
                    int bar = line.IndexOf('|');
                    if (bar < 0) continue;
                    string ok = line.Substring(0, bar), path = line.Substring(bar + 1);
                    Pending.TryGetValue(path, out Req req);
                    Pending.Remove(path);
                    if (ok == "OK" && req != null) Begin(path, req);
                    else { try { System.IO.File.Delete(path); } catch { } }
                }

                for (int i = Playing.Count - 1; i >= 0; i--)
                {
                    Voice v = Playing[i];
                    StringBuilder sb = new StringBuilder(64);
                    Mci("status " + v.Alias + " mode", sb, 64, IntPtr.Zero);
                    if (sb.ToString() == "stopped" || Time.unscaledTime - v.Born > 30f) { Close(v); Playing.RemoveAt(i); continue; }
                    Mix(v);
                }
            }
            catch (Exception e)
            {
                _broken = true;
                CmxPlugin.Log.LogWarning("Text to speech stopped: " + e.Message);
            }
        }

        private static Vector3 Velocity(GameObject go)
        {
            if (!go) return Vector3.zero;
            CharacterBody b = go.GetComponentInParent<CharacterBody>();
            CharacterMotor m = b ? b.characterMotor : null;
            if (m) return m.velocity;
            Rigidbody rb = go.GetComponentInParent<Rigidbody>();
            return rb ? rb.velocity : Vector3.zero;
        }

        private static void Begin(string path, Req req)
        {
            // one voice per speaker: a new line cuts the old one
            for (int i = Playing.Count - 1; i >= 0; i--)
                if (req.At && Playing[i].At == req.At) { Close(Playing[i]); Playing.RemoveAt(i); }

            // Doppler: closing speed bends the pitch by rewriting the sample rate in the wav header
            float level = CmxPlugin.TtsDoppler.Value;
            if (level > 0.01f && req.At)
            {
                try
                {
                    Vector3 pos = req.At.position, listener = ListenerPos();
                    Vector3 toL = (listener - pos);
                    if (toL.sqrMagnitude > 1f)
                    {
                        Vector3 lv = Vector3.zero;
                        CharacterBody me = LocalUserManager.GetFirstLocalUser()?.cachedBody;
                        if (me && me.characterMotor) lv = me.characterMotor.velocity;
                        float closing = Vector3.Dot(Velocity(req.At.gameObject) - lv, toL.normalized);
                        float factor = Mathf.Clamp(1f + level * closing / 343f, 0.6f, 1.6f);
                        if (Mathf.Abs(factor - 1f) > 0.02f)
                        {
                            byte[] wav = System.IO.File.ReadAllBytes(path);
                            if (wav.Length > 44)
                            {
                                int sr = BitConverter.ToInt32(wav, 24), br = BitConverter.ToInt32(wav, 28);
                                Array.Copy(BitConverter.GetBytes(Mathf.RoundToInt(sr * factor)), 0, wav, 24, 4);
                                Array.Copy(BitConverter.GetBytes(Mathf.RoundToInt(br * factor)), 0, wav, 28, 4);
                                System.IO.File.WriteAllBytes(path, wav);
                            }
                        }
                    }
                }
                catch { }
            }

            string alias = "cmxv" + (_n++);
            if (Mci("open \"" + path + "\" type waveaudio alias " + alias, null, 0, IntPtr.Zero) != 0) { try { System.IO.File.Delete(path); } catch { } return; }
            Voice v = new Voice { Alias = alias, File = path, At = req.At, Pos = req.Pos, Born = Time.unscaledTime };
            Mix(v);
            Mci("play " + alias, null, 0, IntPtr.Zero);
            Playing.Add(v);
        }

        /// <summary>Volume by distance (full inside TtsMin, silent past TtsMax) and pan by which side of the camera it is on.</summary>
        private static void Mix(Voice v)
        {
            Vector3 pos = v.At ? v.At.position : v.Pos;
            Vector3 listener = ListenerPos();
            Vector3 d = pos - listener;
            float dist = d.magnitude;
            float min = Mathf.Max(1f, CmxPlugin.TtsMin.Value), max = Mathf.Max(min + 1f, CmxPlugin.TtsMax.Value);
            float vol = dist <= min ? 1f : Mathf.Clamp01(1f - (dist - min) / (max - min));
            vol *= Mathf.Clamp01(CmxPlugin.TtsVolume.Value);
            float pan = 0f;
            Camera c = Camera.main;
            if (c && dist > 0.5f) pan = Mathf.Clamp(Vector3.Dot(d / dist, c.transform.right), -1f, 1f) * Mathf.Clamp01(dist / 3f);
            int l = Mathf.RoundToInt(1000f * vol * (1f - Mathf.Max(0f, pan) * 0.9f));
            int r = Mathf.RoundToInt(1000f * vol * (1f - Mathf.Max(0f, -pan) * 0.9f));
            if (Mathf.Abs(l - v.LastL) < 8 && Mathf.Abs(r - v.LastR) < 8) return;
            v.LastL = l; v.LastR = r;
            if (!_splitBroken)
            {
                int a = Mci("setaudio " + v.Alias + " left volume to " + l, null, 0, IntPtr.Zero);
                int b = Mci("setaudio " + v.Alias + " right volume to " + r, null, 0, IntPtr.Zero);
                if (a == 0 && b == 0) return;
                _splitBroken = true;
            }
            Mci("setaudio " + v.Alias + " volume to " + Mathf.Max(l, r), null, 0, IntPtr.Zero);
        }

        private static void Close(Voice v)
        {
            try
            {
                Mci("stop " + v.Alias, null, 0, IntPtr.Zero);
                Mci("close " + v.Alias, null, 0, IntPtr.Zero);
                System.IO.File.Delete(v.File);
            }
            catch { }
        }
    }

    /// <summary>A line the Specialist's player says: client → host → everyone, who each voice it in 3D from his body.</summary>
    public class CmxSpeakMessage : INetMessage
    {
        public NetworkInstanceId Body;
        public string Text;
        public byte Relayed; // 0 on the way to the host, 1 once the host has sent it on

        public void Serialize(NetworkWriter w) { w.Write(Body); w.Write(Text ?? ""); w.Write(Relayed); }
        public void Deserialize(NetworkReader r) { Body = r.ReadNetworkId(); Text = r.ReadString(); Relayed = r.ReadByte(); }

        public void OnReceived()
        {
            if (Relayed == 0)
            {
                if (!NetworkServer.active) return;
                Relayed = 1;
                try { Text = CmxTts.Clean(Text); if (Text != null) this.Send(NetworkDestination.Clients); } catch { }
                return;
            }
            CmxTts.PlayRemote(Body, Text);
        }
    }
}
