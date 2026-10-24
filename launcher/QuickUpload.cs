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

        /// <summary>Copies the picked files/zips/folders into a fresh temp folder as mods/&lt;modName&gt;/. Returns that folder.</summary>
        public static string Stage(IReadOnlyList<string> picked, string modName)
        {
            try { if (Directory.Exists(StageRoot)) Directory.Delete(StageRoot, true); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { } // an old copy still open: harmless

            string root = Path.Combine(StageRoot, Guid.NewGuid().ToString("N"));
            string dest = Path.Combine(root, "mods", modName);
            Directory.CreateDirectory(dest);

            foreach (string p in picked)
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (Directory.Exists(p)) CopyTree(ModRoot(p), dest);
                else if (ext == ".zip")
                {
                    string unzip = Path.Combine(root, "unzip-" + Guid.NewGuid().ToString("N"));
                    ZipFile.ExtractToDirectory(p, unzip); // refuses entries that point outside the folder
                    CopyTree(ModRoot(unzip), dest);
                }
                else if (OtherArchives.Contains(ext))
                    throw new InvalidOperationException(Path.GetFileName(p) + " is a " + ext + " file. Right-click it and Extract (7-Zip or WinRAR), then pick what came out.");
                else File.Copy(p, Path.Combine(dest, Path.GetFileName(p)), true);
            }

            if (!Directory.EnumerateFiles(dest, "*", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("There were no files in what you picked.");
            return root;
        }

        /// <summary>Digs past wrapper folders ("MyMod-1.0\", "BepInEx\plugins\MyMod\") to the mod's own files.</summary>
        private static string ModRoot(string dir)
        {
            for (int i = 0; i < 8; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "BepInEx"))) { dir = Path.Combine(dir, "BepInEx"); continue; }
                if (Directory.Exists(Path.Combine(dir, "plugins"))) { dir = Path.Combine(dir, "plugins"); continue; }
                string[] files = Directory.GetFiles(dir);
                string[] dirs = Directory.GetDirectories(dir);
                if (files.Length == 0 && dirs.Length == 1) { dir = dirs[0]; continue; }
                if (files.Length == 0 && dirs.Length > 1)
                {
                    var withDlls = dirs.Where(d => Directory.EnumerateFiles(d, "*.dll", SearchOption.AllDirectories).Any())
                                       .Select(Path.GetFileName).ToList();
                    if (withDlls.Count > 1)
                        throw new InvalidOperationException("That has " + withDlls.Count + " mods inside (" + string.Join(", ", withDlls.Take(5)) +
                                                            (withDlls.Count > 5 ? ", ..." : "") + "). Upload one mod at a time: pick just that mod's folder or files.");
                }
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
