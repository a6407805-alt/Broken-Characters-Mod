using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CarMaxSpecialist
{
    /// <summary>
    /// Reads things out loud with the Windows voice (SAPI). One hidden PowerShell stays open and speaks whatever is
    /// written to it; if that fails (not Windows, no PowerShell) it quietly turns itself off.
    /// </summary>
    public static class CmxTts
    {
        private static Process _proc;
        private static bool _broken, _started;
        private static string _last = "";
        private static float _lastAt = -10f;
        private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Junk = new Regex("[#*_\\[\\]{}|~^<>$]", RegexOptions.Compiled);

        public static bool On => !_broken && CmxPlugin.Tts != null && CmxPlugin.Tts.Value;

        private static void Start()
        {
            _started = true;
            try
            {
                string script =
                    "Add-Type -AssemblyName System.Speech;" +
                    "$s=New-Object System.Speech.Synthesis.SpeechSynthesizer;" +
                    "$s.Rate=" + Mathf.Clamp(CmxPlugin.TtsRate.Value, -5, 8) + ";" +
                    "$s.Volume=" + Mathf.Clamp(CmxPlugin.TtsVolume.Value, 0, 100) + ";" +
                    "while(($l=[Console]::In.ReadLine()) -ne $null){ if($l.Length -gt 0){ try{ $s.SpeakAsyncCancelAll(); $null=$s.SpeakAsync($l) }catch{} } }";
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + script.Replace("\"", "'") + "\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };
                _proc = Process.Start(psi);
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
        }

        /// <summary>Speak a line (rich text is stripped). The newest line cuts off the one before it. Repeats within 2 seconds are skipped.</summary>
        public static void Say(string text)
        {
            if (!On || string.IsNullOrEmpty(text)) return;
            try
            {
                string t = Junk.Replace(Tags.Replace(text, ""), " ").Replace("\r", " ").Replace("\n", ". ").Trim();
                if (t.Length < 2) return;
                if (t.Length > 200) t = t.Substring(0, 200);
                float now = Time.unscaledTime;
                if (t == _last && now - _lastAt < 2f) return;
                _last = t;
                _lastAt = now;
                if (!_started) Start();
                if (_proc == null || _proc.HasExited) { if (_started && _proc != null) { _broken = true; } return; }
                _proc.StandardInput.WriteLine(t);
                _proc.StandardInput.Flush();
            }
            catch (Exception e)
            {
                _broken = true;
                CmxPlugin.Log.LogWarning("Text to speech stopped: " + e.Message);
            }
        }
    }
}
