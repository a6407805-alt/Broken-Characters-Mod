using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using R2API;
using RoR2;
using UnityEngine;

namespace CharacterStatus
{
    /// <summary>
    /// Adds a status line to every survivor's description on the character select screen:
    /// green = complete, yellow = in progress, red = early / untested. The list is status.json next to the dll
    /// (generated from the launcher's roster by release/sync_status.py). Every character loads either way; this
    /// only tells friends what to expect.
    /// </summary>
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class StatusPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.brokencharacters.characterstatus";
        public const string PluginName = "Character Status";
        public const string PluginVersion = "1.1.0";
        private const string Prefix = "BC_STATUS_DESC_";

        [Serializable] private class Entry { public string name; public string status; public string version; public string note; public string[] match; }
        [Serializable] private class FileData { public Entry[] characters; }

        private static ManualLogSource _log;
        private static Entry[] _entries;
        private static readonly Dictionary<SurvivorDef, string> Original = new Dictionary<SurvivorDef, string>();
        private static readonly HashSet<string> Reported = new HashSet<string>();

        private void Awake()
        {
            _log = Logger;
            try
            {
                string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "status.json");
                if (System.IO.File.Exists(path))
                {
                    FileData data = JsonUtility.FromJson<FileData>(System.IO.File.ReadAllText(path));
                    _entries = data != null ? data.characters : null;
                }
            }
            catch (Exception e) { Logger.LogWarning("status.json could not be read: " + e.Message); }

            if (_entries == null || _entries.Length == 0) { Logger.LogWarning("No status.json found; no status lines will be added."); return; }
            RoR2Application.onLoad += Apply;
            Language.onCurrentLanguageChanged += Apply;
            Logger.LogInfo("Character Status: " + _entries.Length + " entries.");
        }

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s.ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        private static Entry Find(SurvivorDef sd)
        {
            string hay = Norm(sd.cachedName) + "|" + Norm(sd.bodyPrefab ? sd.bodyPrefab.name : "") + "|" + Norm(Language.GetString(sd.displayNameToken));
            foreach (Entry e in _entries)
            {
                if (e == null || e.match == null) continue;
                foreach (string m in e.match)
                    if (!string.IsNullOrEmpty(m) && hay.Contains(Norm(m))) return e;
            }
            return null;
        }

        private static string StatusLine(Entry e)
        {
            if (e == null) return null;
            string ver = !string.IsNullOrEmpty(e.version) ? " (v" + e.version.TrimStart('v', 'V') + ")" : "";
            string note = !string.IsNullOrEmpty(e.note) ? " — " + e.note : "";
            switch ((e.status ?? "").ToLowerInvariant())
            {
                case "green":
                    return "<color=#4CD964><b>STATUS: COMPLETE" + ver + "</b></color> Finished and working." + note;
                case "active":
                case "working":
                    return "<color=#00E5FF><b>STATUS: ACTIVELY WORKING" + ver + "</b></color> Currently in active development" + (note.Length > 0 ? note + "." : ".");
                case "yellow":
                    return "<color=#FFD22E><b>STATUS: IN PROGRESS" + ver + "</b></color> Playable, but expect rough edges and changes" + (note.Length > 0 ? note + "." : ".");
                case "red":
                    return "<color=#FF4D4D><b>STATUS: EARLY" + ver + "</b></color> Early or untested. It may be unfinished or broken" + (note.Length > 0 ? note + "." : ".");
                default:
                    return null;
            }
        }

        private static void SetString(string token, string text)
        {
            try { LanguageAPI.Add(token, text); } catch (Exception e) { _log.LogWarning("LanguageAPI.Add failed: " + e.Message); }
            try
            {
                object lang = typeof(Language).GetProperty("currentLanguage", BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                MethodInfo set = lang?.GetType().GetMethod("SetStringByToken", BindingFlags.Public | BindingFlags.Instance);
                if (set != null) set.Invoke(lang, new object[] { token, text });
            }
            catch (Exception e) { _log.LogWarning("Setting the current language string failed: " + e.Message); }
        }

        private static void Apply()
        {
            if (_entries == null) return;
            try
            {
                foreach (SurvivorDef sd in SurvivorCatalog.allSurvivorDefs)
                {
                    if (!sd) continue;
                    string orig;
                    if (!Original.TryGetValue(sd, out orig))
                    {
                        orig = sd.descriptionToken;
                        if (string.IsNullOrEmpty(orig) || orig.StartsWith(Prefix)) continue;
                        Original[sd] = orig;
                    }
                    string baseText = Language.GetString(orig);
                    if (string.IsNullOrEmpty(baseText) || baseText == orig) continue;

                    string key = (sd.bodyPrefab ? sd.bodyPrefab.name : sd.cachedName) ?? "unknown";
                    Entry e = Find(sd);
                    string line = StatusLine(e);
                    if (line == null)
                    {
                        if (Reported.Add(key)) _log.LogInfo("No status for survivor '" + key + "' (" + Language.GetString(sd.displayNameToken) + ").");
                        continue;
                    }
                    string token = Prefix + key;
                    SetString(token, baseText + "\r\n\r\n" + line);
                    sd.descriptionToken = token;
                    if (Reported.Add(key)) _log.LogInfo("'" + key + "' is " + e.status + " (" + e.name + ").");
                }
            }
            catch (Exception e) { _log.LogWarning("Applying statuses failed: " + e); }
        }
    }
}
