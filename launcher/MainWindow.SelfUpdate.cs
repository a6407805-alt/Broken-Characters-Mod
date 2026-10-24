// ============================================================================================================
//  Broken Characters Launcher - self-update
//
//  The launcher exe used to never replace itself, so players had to download it by hand. Now, every start
//  (by hand, from the desktop shortcut, or from Steam's Play button) and every REFRESH:
//    1. compares this exe's size with Broken-Characters-Mod.exe on the latest release
//       (the build server republishes the exe on every push; its size changes when the launcher's code changes)
//    2. if they differ: downloads the new exe next to this one as "<exe>.new" and checks its size
//    3. renames the running exe to "<exe>.old" (Windows allows renaming a running program), moves the new one
//       into its place, and restarts it with the same arguments plus "--updated"
//    4. the restarted launcher skips step 1 ("--updated", so it can never loop), deletes "<exe>.old" when it can,
//       and carries on with the normal pack check
//  Anything going wrong (offline, rate limit, antivirus lock, a bad download) just skips the update: the old
//  launcher keeps working and tries again next time. Debug builds and preview/screenshot runs never self-update.
// ============================================================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BrokenCharactersLauncher
{
    public partial class MainWindow
    {
        private const string LauncherDownloadUrl = "https://github.com/" + Repo + "/releases/latest/download/" + LauncherAssetName;

        /// <summary>Set when PLAY started a self-update: once the pack is current, the restarted launcher starts the game.</summary>
        private static bool PlayAfterUpdate => Array.Exists(Args, a => a == "--play");

        /// <summary>Set on the restart right after a self-update, so the new launcher doesn't check again.</summary>
        private static bool JustUpdated => Array.Exists(Args, a => a == "--updated");

        /// <summary>
        /// Replaces this launcher with the one on the latest release if they differ, then restarts it.
        /// Returns true when the launcher is restarting (the caller should stop what it was doing).
        /// </summary>
        /// <param name="fromRefresh">REFRESH or PLAY pressed: check even if this run was itself started by a self-update.</param>
        /// <param name="thenPlay">PLAY pressed: the restarted launcher updates the pack and then starts the game by itself.</param>
        private async Task<bool> TrySelfUpdateAsync(bool fromRefresh, bool thenPlay = false)
        {
#if DEBUG
            await Task.CompletedTask;
            return false; // never replace a developer's own build
#else
            if (PreviewMode || (JustUpdated && !fromRefresh)) return false;

            string? me = Environment.ProcessPath;
            // Any "Broken-Characters-*.exe": also "Broken-Characters-Mod (1).exe" from a second download and the old
            // "Broken-Characters-Launcher.exe". Other names (a developer's own build) are never replaced.
            string myName = me == null ? "" : Path.GetFileName(me);
            if (me == null || !myName.StartsWith("Broken-Characters-", StringComparison.OrdinalIgnoreCase)
                || !myName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            // The dev launcher (Broken-Characters-Mod-dev.exe, from the dev pre-release) is newer than "latest":
            // never swap it for the friends' launcher, or dev-only features vanish the moment it opens.
            if (myName.IndexOf("-dev", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            string newPath = me + ".new";
            string oldPath = me + ".old";
            TryDelete(oldPath); // left over from the last self-update (fails quietly if that old copy is still closing)
            TryDelete(newPath); // left over from a download that didn't finish

            // ---- 1. Is there a different launcher on the release?
            ReleaseInfo release;
            try
            {
                release = await GetLatestReleaseAsync();
                _remote = release;
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is InvalidOperationException || ex is System.Text.Json.JsonException
                                       || ex is System.Collections.Generic.KeyNotFoundException)
            {
                Debug.WriteLine("Launcher update check skipped: " + ex.Message);
                return false;
            }

            long mine;
            try { mine = new FileInfo(me).Length; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return false; }

            if (release.LauncherSize <= 0 || release.LauncherSize == mine) return false;

            // ---- 2. Download it next to this exe (same folder, so the swap is a simple rename)
            _busy = true;
            PlayButton.IsEnabled = false;
            SetStatus("Updating the launcher...");
            SetProgress(0, indeterminate: false);
            try
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)))
                using (HttpResponseMessage response = await Http.GetAsync(LauncherDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                {
                    response.EnsureSuccessStatusCode();
                    await using Stream source = await response.Content.ReadAsStreamAsync(cts.Token);
                    await using var target = new FileStream(newPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    byte[] buffer = new byte[81920];
                    long total = 0;
                    int lastPercent = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                        total += read;
                        int percent = (int)Math.Min(100, total * 100 / release.LauncherSize);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            SetProgress(percent, indeterminate: false);
                        }
                    }
                }

                // A cut-off or swapped download is never installed.
                if (new FileInfo(newPath).Length != release.LauncherSize)
                {
                    TryDelete(newPath);
                    return Skip("The launcher download didn't finish. It will try again next time.");
                }

                // ---- 3. Swap: running exe -> .old, new exe -> its place (put the old one back if that fails)
                File.Move(me, oldPath);
                try
                {
                    File.Move(newPath, me);
                }
                catch (Exception) when (TryRestore(oldPath, me))
                {
                    throw; // only reached if the old exe was put back; the outer catch reports it
                }
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is UnauthorizedAccessException)
            {
                Debug.WriteLine("Launcher update skipped: " + ex.Message);
                TryDelete(newPath);
                return Skip("Couldn't update the launcher right now. It will try again next time.");
            }

            // ---- 4. Restart the new launcher with the same arguments (keeps Steam's game command)
            try
            {
                var psi = new ProcessStartInfo(me) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(me) ?? "" };
                psi.ArgumentList.Add("--updated");
                if (thenPlay) psi.ArgumentList.Add("--play");
                for (int i = 1; i < Args.Length; i++)
                {
                    if (Args[i] != "--updated" && Args[i] != "--play") psi.ArgumentList.Add(Args[i]);
                }
                Process.Start(psi);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // The new exe is in place; this window just keeps running the old code until it's closed.
                Debug.WriteLine("Restart after launcher update failed: " + ex.Message);
                return Skip("Launcher updated. Close and reopen it to use the new version.");
            }

            Application.Current.Shutdown();
            return true;
#endif
        }

        /// <summary>Ends a skipped self-update: frees the buttons and leaves a short note (the pack check runs next).</summary>
        private bool Skip(string message)
        {
            _busy = false;
            SetStatus(message);
            SetProgress(0, indeterminate: false);
            return false;
        }

        /// <summary>Puts the old exe back after a failed swap. Always true so it can sit in an exception filter.</summary>
        private static bool TryRestore(string oldPath, string me)
        {
            try { if (!File.Exists(me) && File.Exists(oldPath)) File.Move(oldPath, me); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Debug.WriteLine("Restore failed: " + ex.Message); }
            return true;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Debug.WriteLine("Couldn't delete " + path + ": " + ex.Message); }
        }
    }
}
