// ============================================================================================================
//  Playing on Linux through Wine/Proton. WPF's see-through windows draw as a black box under Wine, and Wine has no
//  real Windows PowerShell or Steam registry. This file turns those off / finds the Linux Steam library instead.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    internal static class WineCompat
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module, string name);

        /// <summary>True under Wine/Proton (Wine's ntdll exports wine_get_version; real Windows doesn't).
        /// BCM_SAFE_MODE=1 forces it on Windows too, for testing.</summary>
        public static readonly bool IsWine = Detect();

        private static bool Detect()
        {
            if (Environment.GetEnvironmentVariable("BCM_SAFE_MODE") == "1") return true;
            try { IntPtr nt = GetModuleHandle("ntdll.dll"); return nt != IntPtr.Zero && GetProcAddress(nt, "wine_get_version") != IntPtr.Zero; }
            catch { return false; }
        }

        /// <summary>Called once at startup: Wine's graphics layer can't run WPF's GPU path, so draw in software.</summary>
        public static void ApplyToApp()
        {
            if (IsWine) RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        /// <summary>Called in every window's constructor (before Show): no see-through window under Wine, give it a solid
        /// dark background instead, so the window isn't a black rectangle.</summary>
        public static void Apply(Window w)
        {
            if (!IsWine) return;
            w.AllowsTransparency = false;
            if (w.Background is SolidColorBrush b && b.Color.A < 255) w.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x12, 0x20));
        }

        /// <summary>Risk of Rain 2 in the Linux Steam libraries (seen from Wine through drive Z:).</summary>
        public static List<string> LinuxGameFolders()
        {
            var found = new List<string>();
            if (!IsWine) return found;
            var homes = new List<string>();
            string? user = Environment.GetEnvironmentVariable("USER") ?? Environment.GetEnvironmentVariable("LOGNAME") ?? Environment.UserName;
            string? home = Environment.GetEnvironmentVariable("HOME"); // Wine passes the Linux HOME through
            if (!string.IsNullOrEmpty(home) && home.StartsWith("/")) homes.Add(home);
            if (!string.IsNullOrEmpty(user)) homes.Add("/home/" + user);
            try { if (Directory.Exists(@"Z:\home")) homes.AddRange(Directory.GetDirectories(@"Z:\home").Select(d => "/home/" + Path.GetFileName(d))); } catch { }

            var roots = new List<string>();
            foreach (string h in homes.Distinct())
                foreach (string rel in new[] { "/.steam/steam", "/.local/share/Steam", "/.var/app/com.valvesoftware.Steam/.local/share/Steam", "/snap/steam/common/.local/share/Steam" })
                    roots.Add(h + rel);

            var libs = new List<string>(roots);
            foreach (string r in roots)
            {
                try
                {
                    string vdf = ToWine(r + "/steamapps/libraryfolders.vdf");
                    if (File.Exists(vdf))
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value);
                }
                catch { }
            }
            foreach (string l in libs)
            {
                try
                {
                    string g = ToWine(l + "/steamapps/common/Risk of Rain 2");
                    if (File.Exists(Path.Combine(g, "Risk of Rain 2.exe"))) found.Add(Path.GetFullPath(g));
                }
                catch { }
            }
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>A Linux path as Wine sees it: /home/x → Z:\home\x. Windows-looking paths are left alone.</summary>
        public static string ToWine(string p) => p.StartsWith("/") ? "Z:" + p.Replace('/', '\\') : p;

        /// <summary>PowerShell to run install.ps1 with. Wine's own powershell.exe is an empty stub, so prefer PowerShell 7
        /// (winetricks powershell installs it).</summary>
        public static string PowerShellExe()
        {
            if (!IsWine) return "powershell.exe";
            foreach (string p in new[] { @"C:\Program Files\PowerShell\7\pwsh.exe", @"C:\Program Files (x86)\PowerShell\7\pwsh.exe" })
                if (File.Exists(p)) return p;
            return "powershell.exe";
        }
    }
}
