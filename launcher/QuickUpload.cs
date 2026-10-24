// ============================================================================================================
//  QUICK UPLOAD (Creator window): a friend sends a mod over MEGA/Discord as a .zip or a .dll. Instead of laying
//  it out by hand, pick the files (or drag them in), type the mod name, and this copies them into a temp folder
//  shaped like the repo (mods/<Mod>/...). The normal Creator scan/publish then takes over, so ownership rules,
//  "what changed", conflict checks and the build report all work the same.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace BrokenCharactersLauncher
{
    public static class QuickUpload
    {
        public static readonly string StageRoot = Path.Combine(Path.GetTempPath(), "BrokenCharacters-QuickUpload");
        private static readonly string[] OtherArchives = { ".rar", ".7z", ".tar", ".gz", ".tgz" };

        public static bool IsValidName(string name) => Regex.IsMatch(name, @"^[A-Za-z0-9][A-Za-z0-9_.\-]{0,63}$");

        /// <summary>Best guess at the mod's name: the dll's name, else the zip/folder name, minus a version ("-1.0.5").</summary>
        public static string GuessName(IReadOnlyList<string> picked)
        {
            string? pick = picked.FirstOrDefault(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) ?? picked.FirstOrDefault();
            if (pick == null) return "";
            string name = Directory.Exists(pick) ? Path.GetFileName(pick.TrimEnd('\\', '/')) : Path.GetFileNameWithoutExtension(pick);
            name = Regex.Replace(name, @"[\s_\-]*v?\d+(\.\d+)+$", "");   // MyMod-1.0.5 -> MyMod
            Match ts = Regex.Match(name, @"^[A-Za-z0-9]+-([A-Za-z][A-Za-z0-9_]+)$");   // Author-MyMod (Thunderstore) -> MyMod
            if (ts.Success && !pick.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) name = ts.Groups[1].Value;
            return Regex.Replace(name, @"[^A-Za-z0-9_.\-]", "");
        }

        /// <summary>What Stage made: the folder to scan, and (for a whole plugins folder) which mods went in or were skipped.</summary>
        public sealed class Result
        {
            public string Folder { get; init; } = "";
            public List<string> Included { get; } = new List<string>();
            public List<string> Skipped { get; } = new List<string>();
            public bool Multi => Included.Count > 1 || Skipped.Count > 0;
        }

        /// <summary>
        /// Copies the picked files/zips/folders into a fresh temp folder shaped like the repo (mods/&lt;Mod&gt;/...).
        /// One mod: everything goes under <paramref name="modName"/>. A whole plugins/BepInEx folder (many mods): each
        /// mod folder that is already in the pack (and that this person may update, <paramref name="knownMods"/>) goes
        /// under its own name; everything else (R2API, other people's mods...) is skipped.
        /// </summary>
        public static Result Stage(IReadOnlyList<string> picked, string modName, IReadOnlyCollection<string> knownMods)
        {
            try { if (Directory.Exists(StageRoot)) Directory.Delete(StageRoot, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { } // an old copy still open: harmless

            string root = Path.Combine(StageRoot, Guid.NewGuid().ToString("N"));
            var result = new Result { Folder = root };
            Directory.CreateDirectory(Path.Combine(root, "mods"));

            foreach (string p in picked)
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                string? source = null;
                if (Directory.Exists(p)) source = ModRoot(p);
                else if (ext == ".zip")
                {
                    string unzip = Path.Combine(root, "unzip-" + Guid.NewGuid().ToString("N"));
                    ZipFile.ExtractToDirectory(p, unzip); // refuses entries that point outside the folder
                    source = ModRoot(unzip);
                }
                else if (OtherArchives.Contains(ext))
                    throw new InvalidOperationException(Path.GetFileName(p) + " is a " + ext + " file. Right-click it and Extract (7-Zip or WinRAR), then pick what came out.");

                if (source != null && IsManyMods(source)) StageMany(source, root, knownMods, result);
                else
                {
                    if (!IsValidName(modName)) throw new InvalidOperationException("Type the mod name first (letters, numbers, - _ . only).");
                    if (IsReservedName(modName))
                        throw new InvalidOperationException("\"" + modName + "\" is the game's mod loader, not a mod. Type the mod's real name (like SpecialForces), or pick the BepInEx\\plugins folder's zip so each mod is found by name.");
                    string dest = Path.Combine(root, "mods", modName);
                    Directory.CreateDirectory(dest);
                    if (source != null) CopyTree(source, dest);
                    else File.Copy(p, Path.Combine(dest, Path.GetFileName(p)), true);
                    if (!result.Included.Contains(modName, StringComparer.OrdinalIgnoreCase)) result.Included.Add(modName);
                }
            }

            if (!Directory.EnumerateFiles(Path.Combine(root, "mods"), "*", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException(result.Skipped.Count > 0
                    ? "None of those mods are ones you can update in the pack (skipped: " + Short(result.Skipped) + ")."
                    : "There were no files in what you picked.");
            return result;
        }

        /// <summary>A folder holding several mods (like BepInEx\plugins): more than one subfolder with a dll in it.</summary>
        private static bool IsManyMods(string dir) =>
            Path.GetFileName(dir.TrimEnd('\\', '/')).Equals("plugins", StringComparison.OrdinalIgnoreCase) ||
            Directory.GetDirectories(dir).Count(d => Directory.EnumerateFiles(d, "*.dll", SearchOption.AllDirectories).Any()) > 1;

        /// <summary>Folder names that are the game's mod loader or shared libraries, never one of our mods.</summary>
        private static readonly string[] NotModNames = { "BepInEx", "plugins", "patchers", "config", "core", "monomod", "cache",
            "R2API", "HookGenPatcher", "MMHOOK", "ScriptEngine", "Risk of Rain 2", "Risk_of_Rain_2_Data" };

        public static bool IsReservedName(string name) =>
            NotModNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            name.StartsWith("RiskofThunder-", StringComparison.OrdinalIgnoreCase) || name.StartsWith("bbepis-", StringComparison.OrdinalIgnoreCase);

        private static void StageMany(string dir, string root, IReadOnlyCollection<string> knownMods, Result result)
        {
            string? Known(string name) => knownMods.FirstOrDefault(m => m.Equals(name, StringComparison.OrdinalIgnoreCase));
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                string? mod = Known(name);
                if (mod == null) { result.Skipped.Add(name); continue; }
                CopyTree(sub, Path.Combine(root, "mods", mod));
                if (!result.Included.Contains(mod)) result.Included.Add(mod);
            }
            foreach (string file in Directory.GetFiles(dir, "*.dll")) // loose dlls right in plugins\
            {
                string? mod = Known(Path.GetFileNameWithoutExtension(file));
                if (mod == null) { result.Skipped.Add(Path.GetFileName(file)); continue; }
                Directory.CreateDirectory(Path.Combine(root, "mods", mod));
                File.Copy(file, Path.Combine(root, "mods", mod, Path.GetFileName(file)), true);
                if (!result.Included.Contains(mod)) result.Included.Add(mod);
            }
        }

        public static string Short(List<string> names) => string.Join(", ", names.Take(6)) + (names.Count > 6 ? " and " + (names.Count - 6) + " more" : "");

        /// <summary>Digs past wrapper folders ("MyMod-1.0\", "BepInEx\plugins\MyMod\") to the mod's own files, or to the
        /// plugins folder itself when it holds many mods.</summary>
        private static string ModRoot(string dir)
        {
            for (int i = 0; i < 8; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "BepInEx"))) { dir = Path.Combine(dir, "BepInEx"); continue; }
                if (Directory.Exists(Path.Combine(dir, "plugins"))) { dir = Path.Combine(dir, "plugins"); continue; }
                string[] files = Directory.GetFiles(dir);
                string[] dirs = Directory.GetDirectories(dir);
                if (files.Length == 0 && dirs.Length == 1) { dir = dirs[0]; continue; }
                break;
            }
            return dir;
        }

        private static void CopyTree(string from, string to)
        {
            foreach (string file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }
    }
}
