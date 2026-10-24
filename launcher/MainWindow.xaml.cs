// ============================================================================================================
//  Broken Characters Launcher - MainWindow
//
//  What it does, in order:
//    1. Reads the installed version from %LOCALAPPDATA%\BrokenCharacters\Launcher\version.txt
//    2. Asks the GitHub API for the latest release of the mod pack (and the recent commits for the changelog)
//    3. If GitHub has a newer build: downloads Broken-Characters-Mod.zip with a live progress bar,
//       extracts it (overwriting the old copy) and runs the pack's own install.ps1, which puts the mods into
//       the Risk of Rain 2 folder and fetches the libraries (BepInEx, R2API, ...) and the friends-only extras
//    4. Writes the new version to version.txt and lights up PLAY
//    5. PLAY launches Risk of Rain 2 through Steam and closes the launcher
//
//  Why the version is NOT the release tag: the build server republishes every build under the same tag,
//  "latest", so the tag never changes. Each release is created from one commit, and the release's
//  target_commitish is that commit's SHA, which is unique per build, so that is what version.txt stores.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    public partial class MainWindow : Window
    {
        // ------------------------------------------------------------------ settings

        /// <summary>The GitHub repository the pack is published from (owner/name).</summary>
        private const string Repo = "a6407805-alt/Broken-Characters-Mod";

        /// <summary>The release asset that holds the whole pack.</summary>
        private const string AssetName = "Broken-Characters-Mod.zip";

        /// <summary>The pack's earlier name, still accepted if a release only has that one.</summary>
        private const string LegacyAssetName = "Broken-Characters-Collection.zip";

        /// <summary>The launcher program itself, as published on the release.</summary>
        private const string LauncherAssetName = "Broken-Characters-Mod.exe";

        /// <summary>Steam's URL for Risk of Rain 2 (app id 632360).</summary>
        private const string SteamLaunchUri = "steam://rungameid/632360";

        // Where the launcher keeps its own files: %LOCALAPPDATA%\BrokenCharacters\Launcher
        private static readonly string LauncherDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrokenCharacters", "Launcher");
        private static readonly string VersionFile = Path.Combine(LauncherDir, "version.txt");
        private static readonly string PackDir = Path.Combine(LauncherDir, "pack");
        private static readonly string DownloadFile = Path.Combine(LauncherDir, "download.zip.part");

        // The installer (install.ps1) keeps the extras key here; the launcher shares the same files.
        private static readonly string ExtrasDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters");
        private static readonly string ExtrasKeyFile = Path.Combine(ExtrasDir, "extras-key.txt");
        private static readonly string ExtrasSkipFile = Path.Combine(ExtrasDir, "extras-skip.txt");

        /// <summary>
        /// One HttpClient for the launcher's whole life (creating one per request can exhaust sockets).
        /// GitHub's API rejects requests without a User-Agent, so one is always sent. Timeouts are handled
        /// per request with CancellationTokens, so the client itself never times out a long download.
        /// </summary>
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true // release downloads redirect to GitHub's file storage
            };
            var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrokenCharactersLauncher", "1.0"));
            return client;
        }

        /// <summary>What the GitHub API tells us about the latest release.</summary>
        private sealed record ReleaseInfo(string Version, string DownloadUrl, long Size, DateTimeOffset Published, long LauncherSize = 0);

        /// <summary>The release we found on this check (kept so "Save" on the extras key can reinstall it).</summary>
        private ReleaseInfo? _remote;

        /// <summary>True while a check or update is running, so buttons can't start a second one.</summary>
        private bool _busy;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ================================================================== startup

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Dev tools (devtools/BCMDevTools) leave a marker when they load; only then is TEST LEVEL shown.
            TestLevelButton.Visibility = File.Exists(DevToolsMarkerFile) ? Visibility.Visible : Visibility.Collapsed;

            // The dev tools' RELAUNCH button restarts the game through Steam, which (with Launch Options) opens us
            // again: go straight back into the game without updating, so the test arena loads as fast as possible.
            if (!PreviewMode && File.Exists(QuickTestFlagFile) && SteamGameCommand(out _, out _)) { LaunchGame(); return; }

            // Offer the extras key box only if the player has never entered one.
            ShowExtrasState(File.Exists(ExtrasKeyFile));

            // Fill the roster, maps and modes from the built-in roster.json right away, then refresh them
            // from GitHub in the background (new characters show up without a new launcher exe).
            LoadBuiltInRoster();
            Task roster = RefreshRosterFromGitHubAsync();
            if (!PreviewMode) _ = SyncJukeboxQuietlyAsync();

            // Preview mode ("--preview"): show the launcher without checking, downloading or installing anything.
            // Safe to run on a dev PC. "--screenshot <file.png>" (used by the build server) also saves a picture
            // of the window once the art has loaded, then closes.
            if (PreviewMode)
            {
                _ = LoadChangelogAsync();
                ShowInstalledVersion("preview");
                Ready("Installation Up to Date!");
                if (ScreenshotPath != null)
                {
                    await roster;
                    await Task.Delay(TimeSpan.FromSeconds(4)); // let the online images finish downloading
                    SaveScreenshot(ScreenshotPath);
                    Application.Current.Shutdown();
                }
                return;
            }

            // The changelog loads on its own; if it fails the launcher still works.
            _ = LoadChangelogAsync();

            // A newer launcher on the release replaces this one and restarts it (also when Steam's Play button opened us).
            if (await TrySelfUpdateAsync(fromRefresh: false)) return;

            await CheckAndUpdateAsync();
            try { await SetUpSteamPlayAsync(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.ComponentModel.Win32Exception)
            { Debug.WriteLine("Steam Play setup skipped: " + ex.Message); }

            // PLAY was pressed and the launcher restarted to update itself: the pack is now checked too, so go.
            if (PlayAfterUpdate && PlayButton.IsEnabled) LaunchGame();
        }

        /// <summary>The main flow: compare versions, update if needed, then enable PLAY.</summary>
        private async Task CheckAndUpdateAsync(bool forceInstall = false)
        {
            if (_busy) return;
            _busy = true;
            RetryButton.Visibility = Visibility.Collapsed;
            PlayButton.IsEnabled = false; // never playable while we might be changing files
            SetProgress(0, indeterminate: true);

            try
            {
                string? local = ReadLocalVersion();
                ShowInstalledVersion(local);
                SetStatus("Checking for updates...");

                // ---- 1. Ask GitHub what the newest build is
                try
                {
                    _remote = await GetLatestReleaseAsync();
                }
                catch (Exception ex) when (IsNetworkError(ex))
                {
                    // Offline or GitHub is down. If something is installed, let them play it anyway.
                    if (local != null)
                    {
                        Ready("Can't reach GitHub right now. Playing the version you have.");
                        return;
                    }
                    Fail("Can't reach GitHub to download the pack. Check your internet and press Retry.", ex);
                    return;
                }

                // ---- 2. Compare
                bool installed = local != null && File.Exists(Path.Combine(PackDir, "install.ps1"));
                if (!forceInstall && installed && string.Equals(local, _remote.Version, StringComparison.OrdinalIgnoreCase))
                {
                    Ready("Installation Up to Date!");
                    return;
                }

                // ---- 3. Update (download, extract, install), unless the pack is current and only the extras changed
                if (!installed || !string.Equals(local, _remote.Version, StringComparison.OrdinalIgnoreCase))
                    await DownloadAndExtractAsync(_remote);
                await RunInstallerAsync();

                // ---- 4. Remember what we installed
                WriteLocalVersion(_remote.Version);
                ShowInstalledVersion(_remote.Version);
                Ready("Installation Up to Date!");
            }
            catch (Exception ex)
            {
                Fail("Update failed: " + ex.Message, ex);
            }
            finally
            {
                _busy = false;
            }
        }

        // ================================================================== version checking

        /// <summary>Reads version.txt; null if the pack has never been installed by the launcher.</summary>
        private static string? ReadLocalVersion()
        {
            try
            {
                if (!File.Exists(VersionFile)) return null;
                string v = File.ReadAllText(VersionFile).Trim();
                return v.Length > 0 ? v : null;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static void WriteLocalVersion(string version)
        {
            Directory.CreateDirectory(LauncherDir);
            File.WriteAllText(VersionFile, version);
        }

        /// <summary>
        /// GET /repos/{repo}/releases/latest. The version is the commit the release was built from
        /// (target_commitish), and the download URL is the pack asset's browser_download_url.
        /// </summary>
        private static async Task<ReleaseInfo> GetLatestReleaseAsync()
        {
            // A build that is publishing can briefly have no release; wait and ask again instead of failing.
            for (int attempt = 1; ; attempt++)
            {
                try { return await GetLatestReleaseOnceAsync(); }
                catch (ReleaseMissingException) when (attempt < 4) { await Task.Delay(TimeSpan.FromSeconds(5)); }
            }
        }

        private sealed class ReleaseMissingException : InvalidOperationException
        {
            public ReleaseMissingException(string message) : base(message) { }
        }

        private static async Task<ReleaseInfo> GetLatestReleaseOnceAsync()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/releases/latest");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            if (response.StatusCode == HttpStatusCode.Forbidden || (int)response.StatusCode == 429)
                throw new HttpRequestException("GitHub is rate-limiting this PC (60 checks an hour). Try again in a few minutes.");
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new ReleaseMissingException("No release found for " + Repo + ".");
            response.EnsureSuccessStatusCode();

            await using Stream body = await response.Content.ReadAsStreamAsync(cts.Token);
            using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);
            JsonElement root = json.RootElement;

            // The commit SHA identifies the build. Fall back to the publish date if it's ever missing.
            string version = root.TryGetProperty("target_commitish", out JsonElement sha) && sha.GetString() is { Length: > 0 } s
                ? s
                : root.GetProperty("published_at").GetString() ?? "unknown";
            DateTimeOffset published = root.TryGetProperty("published_at", out JsonElement p) && p.TryGetDateTimeOffset(out DateTimeOffset d)
                ? d : DateTimeOffset.MinValue;

            // The launcher program itself is published too; its size tells the REFRESH button if this copy is out of date.
            long launcherSize = 0;
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == LauncherAssetName) launcherSize = asset.GetProperty("size").GetInt64();
            }

            foreach (string wanted in new[] { AssetName, LegacyAssetName })
            foreach (JsonElement asset in root.GetProperty("assets").EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() != wanted) continue;
                return new ReleaseInfo(
                    version,
                    asset.GetProperty("browser_download_url").GetString() ?? throw new InvalidOperationException("The release has no download link."),
                    asset.GetProperty("size").GetInt64(),
                    published,
                    launcherSize);
            }
            throw new InvalidOperationException($"The latest release doesn't contain {AssetName}.");
        }

        // ================================================================== changelog

        /// <summary>
        /// Shows the last 12 commit messages as the changelog. The release notes themselves are the same boilerplate
        /// every build, so the commit history is where the real "what's new" lives.
        /// </summary>
        private async Task LoadChangelogAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repo}/commits?per_page=12");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
                response.EnsureSuccessStatusCode();

                await using Stream body = await response.Content.ReadAsStreamAsync(cts.Token);
                using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);

                var sb = new StringBuilder();
                foreach (JsonElement c in json.RootElement.EnumerateArray())
                {
                    JsonElement commit = c.GetProperty("commit");
                    string message = commit.GetProperty("message").GetString() ?? "";
                    string firstLine = message.Split('\n')[0].Trim();
                    if (firstLine.Length == 0 || firstLine.StartsWith("Merge ", StringComparison.Ordinal)) continue;

                    string date = commit.GetProperty("author").TryGetProperty("date", out JsonElement de) && de.TryGetDateTimeOffset(out DateTimeOffset dt)
                        ? dt.ToLocalTime().ToString("MMM d") : "";
                    sb.Append("•  ").Append(firstLine);
                    if (date.Length > 0) sb.Append("   (").Append(date).Append(')');
                    sb.AppendLine().AppendLine();
                }
                ChangelogText.Text = sb.Length > 0 ? sb.ToString().TrimEnd() : "No changes listed.";
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is JsonException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                ChangelogText.Text = "Couldn't load the changelog (offline?). The launcher still works.";
            }
        }

        // ================================================================== downloading & extracting

        /// <summary>
        /// Downloads the pack zip to a temporary ".part" file while updating the progress bar, then extracts it into
        /// the launcher's pack folder. The download only replaces the old pack once it has fully arrived, so a dropped
        /// connection never leaves a half-written pack behind.
        /// </summary>
        private async Task DownloadAndExtractAsync(ReleaseInfo release)
        {
            Directory.CreateDirectory(LauncherDir);
            SetStatus("Downloading the latest build...");
            SetProgress(0, indeterminate: false);

            // Progress<T> runs its callback on the UI thread (it captures the dispatcher's context),
            // so it can touch the progress bar and text directly.
            var progress = new Progress<(long Done, long Total)>(p =>
            {
                double percent = p.Total > 0 ? p.Done * 100.0 / p.Total : 0;
                DownloadProgress.Value = percent;
                ProgressText.Text = $"{p.Done / 1048576.0:0.0} / {p.Total / 1048576.0:0.0} MB";
            });

            await DownloadFileAsync(release.DownloadUrl, DownloadFile, release.Size, progress);

            // ---- Extract
            SetStatus("Unpacking...");
            SetProgress(0, indeterminate: true);
            ProgressText.Text = "";
            await Task.Run(() =>
            {
                // Start from an empty folder so files removed from the pack don't linger and get reinstalled.
                if (Directory.Exists(PackDir)) Directory.Delete(PackDir, recursive: true);
                Directory.CreateDirectory(PackDir);
                // overwriteFiles: true replaces anything already there. ExtractToDirectory also refuses entries
                // that try to escape the folder (e.g. "..\..\file"), so a bad zip can't write elsewhere.
                ZipFile.ExtractToDirectory(DownloadFile, PackDir, overwriteFiles: true);
                File.Delete(DownloadFile);
            });
        }

        /// <summary>
        /// Streams a URL to a file in 80 KB chunks, reporting (bytes done, total bytes) as it goes.
        /// A "stall" timeout cancels if no data arrives for 30 seconds, without limiting how long a slow download may take.
        /// </summary>
        private static async Task DownloadFileAsync(string url, string destination, long expectedSize, IProgress<(long, long)> progress)
        {
            using var stall = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            // ResponseHeadersRead: start reading as soon as headers arrive instead of buffering 70 MB in memory.
            using HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, stall.Token);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? expectedSize;

            await using Stream source = await response.Content.ReadAsStreamAsync(stall.Token);
            await using (var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long done = 0;
                long lastReport = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), stall.Token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), stall.Token);
                    done += read;
                    stall.CancelAfter(TimeSpan.FromSeconds(30)); // data arrived: reset the stall timer

                    // Report about every 256 KB so the UI isn't flooded with updates.
                    if (done - lastReport >= 262144 || done == total)
                    {
                        lastReport = done;
                        progress.Report((done, total));
                    }
                }
                progress.Report((done, total));

                if (total > 0 && done != total)
                    throw new IOException($"The download was cut short ({done:N0} of {total:N0} bytes). Press Retry.");
            }
        }

        // ================================================================== installing into the game

        /// <summary>
        /// Runs the pack's own install.ps1 (the same one INSTALL.bat uses). It finds the Risk of Rain 2 folder through
        /// Steam, puts BepInEx and the libraries in place, copies the mods into BepInEx\plugins and adds the
        /// friends-only extras if an extras key is saved. Its output lines are shown as the status text.
        /// </summary>
        private async Task RunInstallerAsync()
        {
            string script = Path.Combine(PackDir, "install.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("The pack has no install.ps1.", script);

            SetStatus("Installing into Risk of Rain 2...");
            SetProgress(0, indeterminate: true);

            // Without a saved key or a "skip" choice, install.ps1 would stop and ask for one in a console nobody can
            // see. The launcher has its own key box instead, so in that case the extras are skipped for now.
            bool askedAboutExtras = File.Exists(ExtrasKeyFile) || File.Exists(ExtrasSkipFile);
            string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"" + (askedAboutExtras ? "" : " -SkipExtras");

            var psi = new ProcessStartInfo("powershell.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,            // no console window; we show progress ourselves
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = PackDir
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var errors = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data)) return;
                string line = e.Data.Trim();
                Dispatcher.InvokeAsync(() => SetStatus(line)); // events arrive on a background thread
            };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (errors) errors.AppendLine(e.Data); };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new InvalidOperationException("Windows PowerShell couldn't be started: " + ex.Message, ex);
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                string detail = errors.ToString().Trim();
                throw new InvalidOperationException("the installer stopped (code " + process.ExitCode + ")" +
                    (detail.Length > 0 ? ": " + detail.Split('\n')[0] : ". See install_log.txt in " + PackDir));
            }
        }

        // ================================================================== PLAY

        /// <summary>
        /// Hands the steam:// link to Windows, which passes it to Steam, then closes the launcher.
        /// UseShellExecute must be true: on .NET 8, Process.Start with a URL and the default false fails.
        /// </summary>
        /// <summary>
        /// PLAY: first update the launcher itself (it restarts and carries on by itself), then make sure the pack is
        /// current, then start the game.
        /// </summary>
        private async void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            PlayButton.IsEnabled = false;
            if (await TrySelfUpdateAsync(fromRefresh: true, thenPlay: true)) return; // restarting; the new launcher plays
            await CheckAndUpdateAsync();
            if (PlayButton.IsEnabled) LaunchGame(); // stays off only if nothing is installed and the update failed
        }

        private void LaunchGame()
        {
            try
            {
                // Opened by Steam's Play button (Launch Options: "<launcher>" %command%): Steam passed the game's exe,
                // so start that directly. Using steam:// here would make Steam open the launcher again, forever.
                if (SteamGameCommand(out string exe, out List<string> gameArgs))
                {
                    var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) ?? "" };
                    foreach (string a in gameArgs) psi.ArgumentList.Add(a);
                    Process.Start(psi);
                }
                else Process.Start(new ProcessStartInfo(SteamLaunchUri) { UseShellExecute = true }); // opened by hand
                Application.Current.Shutdown();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                MessageBox.Show(this, "Couldn't start the game: " + ex.Message + "\n\nIs Steam installed?",
                    "Broken Characters Mod", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>The "Risk of Rain 2.exe ..." command Steam appends when it opens the launcher through Launch Options.</summary>
        private static bool SteamGameCommand(out string exe, out List<string> rest)
        {
            exe = ""; rest = new List<string>();
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 1; i < a.Length; i++)
            {
                if (!a[i].EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(a[i])) continue;
                exe = a[i];
                for (int j = i + 1; j < a.Length; j++) rest.Add(a[j]);
                return true;
            }
            return false;
        }

        // ================================================================== Steam Play button (opt-in) + desktop shortcut

        /// <summary>A fixed home for the launcher, so Steam's Launch Options and the shortcut never point at Downloads.</summary>
        private static readonly string StableExe = Path.Combine(LauncherDir, "Broken-Characters-Mod.exe");
        private static readonly string SteamPlayFile = Path.Combine(ExtrasDir, "steam-play.txt"); // on / off

        private static string EnsureStableCopy()
        {
            try
            {
                string? me = Environment.ProcessPath;
                if (me == null || string.Equals(Path.GetFullPath(me), Path.GetFullPath(StableExe), StringComparison.OrdinalIgnoreCase)) return StableExe;
                Directory.CreateDirectory(LauncherDir);
                var src = new FileInfo(me);
                var dst = new FileInfo(StableExe);
                if (!dst.Exists || dst.Length != src.Length || dst.LastWriteTimeUtc < src.LastWriteTimeUtc) File.Copy(me, StableExe, overwrite: true);
                return StableExe;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Environment.ProcessPath ?? StableExe;
            }
        }

        /// <summary>
        /// First run: make the desktop shortcut and show the toggle's saved state. The Steam change itself only happens
        /// when the player flips the STEAM PLAY toggle (it's their Steam account, so it's opt-in).
        /// </summary>
        private async Task SetUpSteamPlayAsync()
        {
            string saved = File.Exists(SteamPlayFile) ? File.ReadAllText(SteamPlayFile).Trim() : "";
            SteamPlayToggle.IsChecked = saved == "on" || saved == "done";
            if (!File.Exists(Path.Combine(PackDir, "install.ps1"))) return;
            await RunSteamPlayScriptAsync("");   // shortcut only (it's skipped if one already exists)
        }

        private async Task<string> RunSteamPlayScriptAsync(string extra)
        {
            string script = Path.Combine(PackDir, "install.ps1");
            string args = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -SteamPlayOnly -LauncherPath \"{EnsureStableCopy()}\" {extra}";
            var psi = new ProcessStartInfo("powershell.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using Process? p = Process.Start(psi);
            if (p == null) return "";
            string output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return output;
        }

        private static string? SteamExe()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                string? exe = key?.GetValue("SteamExe") as string;
                return exe != null && File.Exists(exe) ? exe : null;
            }
            catch (System.Security.SecurityException) { return null; }
        }

        private static bool SteamRunning() => Process.GetProcessesByName("steam").Length > 0;

        /// <summary>
        /// The STEAM PLAY toggle. Steam keeps its settings in memory and rewrites the file when it closes, so a change
        /// made while Steam is open would be lost: the launcher closes Steam, saves the change, and opens Steam again.
        /// </summary>
        private async void SteamPlayToggle_Click(object sender, RoutedEventArgs e)
        {
            bool turnOn = SteamPlayToggle.IsChecked == true;
            void Revert() => SteamPlayToggle.IsChecked = !turnOn;

            if (SteamGameCommand(out _, out _))
            {
                MessageBox.Show(this, "Steam opened this launcher, so Steam can't be closed right now.\n\nOpen the launcher from its desktop shortcut to change this.",
                    "Broken Characters Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                Revert(); return;
            }
            if (!File.Exists(Path.Combine(PackDir, "install.ps1")))
            {
                MessageBox.Show(this, "Let the launcher finish installing first, then try again.", "Broken Characters Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                Revert(); return;
            }

            string? steam = SteamExe();
            bool restart = false;
            if (SteamRunning())
            {
                if (MessageBox.Show(this, "Steam has to close for a moment to save this (it would undo the change otherwise).\n\nClose Steam, save, and open it again?",
                        "Broken Characters Mod", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) { Revert(); return; }
                SteamPlayToggle.IsEnabled = false;
                SetStatus("Closing Steam...");
                try { if (steam != null) Process.Start(steam, "-shutdown"); else Process.Start(new ProcessStartInfo("steam://exit") { UseShellExecute = true }); }
                catch (System.ComponentModel.Win32Exception) { }
                for (int i = 0; i < 60 && SteamRunning(); i++) await Task.Delay(500);
                if (SteamRunning())
                {
                    SetStatus("Steam didn't close. Close it yourself (Steam menu > Exit) and flip the switch again.");
                    SteamPlayToggle.IsEnabled = true; Revert(); return;
                }
                restart = true;
            }

            SteamPlayToggle.IsEnabled = false;
            SetStatus(turnOn ? "Pointing Steam's Play button at the launcher..." : "Putting Steam's Play button back to normal...");
            string output = await RunSteamPlayScriptAsync(turnOn ? "-SetSteamPlay" : "-UnsetSteamPlay");
            bool ok = output.Contains("STEAMPLAY_ON") || output.Contains("STEAMPLAY_OFF") || output.Contains("already set") || (!turnOn && !output.Contains("STEAM_OPEN"));

            if (output.Contains("STEAMPLAY_KEPT"))
            {
                MessageBox.Show(this, "You already have your own Launch Options for Risk of Rain 2 in Steam, so they were left alone.\n\n" +
                    "To use this anyway: Steam > Risk of Rain 2 > Properties > clear Launch Options, then flip the switch again.",
                    "Broken Characters Mod", MessageBoxButton.OK, MessageBoxImage.Information);
                ok = false;
            }
            if (ok)
            {
                Directory.CreateDirectory(ExtrasDir);
                File.WriteAllText(SteamPlayFile, turnOn ? "on" : "off");
                SetStatus(turnOn ? "Done: Steam's Play button now opens this launcher." : "Done: Steam's Play button starts the game normally again.");
            }
            else
            {
                Revert();
                if (!output.Contains("STEAMPLAY_KEPT"))
                    SetStatus("Couldn't change Steam's settings. " + (output.Contains("STEAM_OPEN") ? "Steam is still open." : "Has Risk of Rain 2 been played on this Steam account?"));
            }
            if (restart && steam != null) { try { Process.Start(steam); } catch (System.ComponentModel.Win32Exception) { } }
            SteamPlayToggle.IsEnabled = true;
        }

        // ================================================================== creator tools

        /// <summary>
        /// REFRESH button: reloads the character/map cards and patch notes, updates this launcher program itself if a
        /// newer one is on the release (see MainWindow.SelfUpdate.cs), then checks GitHub for a newer pack and installs it.
        /// </summary>
        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || PreviewMode) return;
            RefreshButton.IsEnabled = false;
            try
            {
                Task roster = RefreshRosterFromGitHubAsync();
                Task changelog = LoadChangelogAsync();
                _ = SyncJukeboxQuietlyAsync();
                if (await TrySelfUpdateAsync(fromRefresh: true)) return; // restarting into the new launcher
                await CheckAndUpdateAsync();
                await roster;
                await changelog;
            }
            finally
            {
                RefreshButton.IsEnabled = true;
            }
        }

        // Shared with devtools/BCMDevTools (same folder as the extras key): the mod writes the marker when it loads
        // and reads + deletes the flag at startup to jump straight into the test arena.
        private static readonly string DevToolsMarkerFile = Path.Combine(ExtrasDir, "devtools-installed.txt");
        private static readonly string QuickTestFlagFile = Path.Combine(ExtrasDir, "quicktest.flag");

        /// <summary>TEST LEVEL (dev only): ask the dev tools for a quick test, then start the game without updating.</summary>
        private void TestLevel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(ExtrasDir);
                File.WriteAllText(QuickTestFlagFile, DateTime.Now.ToString("o"));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Couldn't start the test level: " + ex.Message, "Broken Characters Mod",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LaunchGame();
        }

        /// <summary>The right-edge tab: opens the install &amp; update guide (docs/, committed next to the launcher) in the browser.</summary>
        private void GuideTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "https://github.com/" + Repo + "/blob/main/docs/Broken%20Characters%20-%20Install%20Guide.pdf")
                { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex) { Debug.WriteLine("Could not open the guide: " + ex.Message); }
        }

        /// <summary>Opens the "publish my mod" window (see PublishWindow.xaml.cs / Publisher.cs).</summary>
        private void Creator_Click(object sender, RoutedEventArgs e)
        {
            new PublishWindow { Owner = this }.ShowDialog();
        }

        private void Jukebox_Click(object sender, RoutedEventArgs e)
        {
            new JukeboxWindow { Owner = this }.ShowDialog();
        }

        /// <summary>Downloads any new group songs for the Jukebox mod in the background; never blocks or fails startup.</summary>
        private static async Task SyncJukeboxQuietlyAsync()
        {
            try { await JukeboxLibrary.SyncAsync(); }
            catch (Exception ex) { Debug.WriteLine("Jukebox sync failed: " + ex.Message); }
        }

        // ================================================================== extras key

        /// <summary>Saves the friends-only extras key where install.ps1 looks for it, then reinstalls to add the extras.</summary>
        private async void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            string key = ExtrasKeyBox.Password.Trim();
            if (key.Length == 0) return;
            try
            {
                Directory.CreateDirectory(ExtrasDir);
                File.WriteAllText(ExtrasKeyFile, key);
                if (File.Exists(ExtrasSkipFile)) File.Delete(ExtrasSkipFile);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Fail("Couldn't save the key: " + ex.Message, ex);
                return;
            }
            ExtrasKeyBox.Clear();
            ShowExtrasState(true);
            await CheckAndUpdateAsync(forceInstall: true); // reruns install.ps1, which now downloads the extras
        }

        // ================================================================== roster, maps & game modes

        /// <summary>Shows either the extras key box or the "extras are active" line in the bottom-left panel.</summary>
        private void ShowExtrasState(bool keySaved)
        {
            ExtrasPanel.Visibility = keySaved ? Visibility.Collapsed : Visibility.Visible;
            ExtrasActivePanel.Visibility = keySaved ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>Reads the roster.json compiled into the exe (works offline).</summary>
        private void LoadBuiltInRoster()
        {
            try
            {
                var resource = Application.GetResourceStream(new Uri("pack://application:,,,/roster.json"));
                if (resource == null) return;
                using var reader = new StreamReader(resource.Stream);
                ApplyRoster(RosterData.Parse(reader.ReadToEnd(), path => new Uri("pack://application:,,,/" + path.TrimStart('/'))));
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                Debug.WriteLine("Built-in roster failed: " + ex);
            }
        }

        /// <summary>
        /// Downloads the newest roster.json straight from the repo (raw file, not the API, so it doesn't count against
        /// GitHub's 60-requests-an-hour limit). Its images are loaded from the repo too. Any failure keeps the built-in list.
        /// </summary>
        private async Task RefreshRosterFromGitHubAsync()
        {
            const string rawBase = "https://raw.githubusercontent.com/" + Repo + "/main/launcher/";
            // The build server merges every creator's card (launcher/cards/<Mod>.json) into one roster.json and attaches
            // it to the latest release. That merged copy is tried first; the plain file in the repo is the fallback.
            const string mergedRoster = "https://github.com/" + Repo + "/releases/latest/download/roster.json";
            try
            {
                string? json = null;
                try
                {
                    using var first = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    json = await Http.GetStringAsync(mergedRoster, first.Token);
                }
                catch (Exception ex) when (IsNetworkError(ex)) { Debug.WriteLine("Merged roster unavailable: " + ex.Message); }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                json ??= await Http.GetStringAsync(rawBase + "roster.json", cts.Token);
                RosterData remote = RosterData.Parse(json, path => new Uri(rawBase + path.TrimStart('/')));
                if (remote.Characters.Count > 0) ApplyRoster(remote);
            }
            catch (Exception ex) when (IsNetworkError(ex) || ex is JsonException)
            {
                Debug.WriteLine("Online roster unavailable, keeping the built-in one: " + ex.Message);
            }
        }

        private void ApplyRoster(RosterData data)
        {
            RosterList.ItemsSource = data.Characters;
            MapsList.ItemsSource = data.Maps;
            ModesList.ItemsSource = data.Modes;
            RosterCountText.Text = data.Characters.Count + " playable";
            RosterScroll.ScrollToHorizontalOffset(0); // start at the left, where the newest characters are
            UpdateRosterArrows();
        }

        // ------------------------------------------------------------------ roster scrolling

        /// <summary>Width one card takes in the row (176 wide, -4 margin each side).</summary>
        private const double CardStep = 168;

        private double _scrollFrom, _scrollTo;
        private DateTime _scrollStart;
        private bool _scrollAnimating;

        /// <summary>
        /// Smoothly scrolls the roster to a horizontal offset. ScrollViewer's HorizontalOffset can't be animated with a
        /// Storyboard (it's read-only), so each rendered frame moves it along an ease-out curve instead.
        /// </summary>
        private void SmoothScrollTo(double target)
        {
            _scrollFrom = RosterScroll.HorizontalOffset;
            _scrollTo = Math.Max(0, Math.Min(target, RosterScroll.ScrollableWidth));
            _scrollStart = DateTime.UtcNow;
            if (_scrollAnimating) return;
            _scrollAnimating = true;
            CompositionTarget.Rendering += OnScrollFrame;
        }

        private void OnScrollFrame(object? sender, EventArgs e)
        {
            double t = Math.Min(1.0, (DateTime.UtcNow - _scrollStart).TotalSeconds / 0.35);
            double eased = 1 - Math.Pow(1 - t, 3); // ease-out cubic: fast start, gentle stop
            RosterScroll.ScrollToHorizontalOffset(_scrollFrom + (_scrollTo - _scrollFrom) * eased);
            if (t < 1.0) return;
            CompositionTarget.Rendering -= OnScrollFrame;
            _scrollAnimating = false;
        }

        /// <summary>Where the roster is headed (the animation target while scrolling, otherwise where it is).</summary>
        private double RosterTarget => _scrollAnimating ? _scrollTo : RosterScroll.HorizontalOffset;

        /// <summary>How far one arrow click moves: as many whole cards as fit on screen, at least one.</summary>
        private double RosterPage => Math.Max(1, Math.Floor((RosterScroll.ViewportWidth - 80) / CardStep)) * CardStep;

        private void RosterLeft_Click(object sender, RoutedEventArgs e) => SmoothScrollTo(RosterTarget - RosterPage);

        private void RosterRight_Click(object sender, RoutedEventArgs e) => SmoothScrollTo(RosterTarget + RosterPage);

        /// <summary>The mouse wheel scrolls the roster sideways (one notch = about one card).</summary>
        private void Roster_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            SmoothScrollTo(RosterTarget - e.Delta / 120.0 * CardStep);
            e.Handled = true;
        }

        /// <summary>Hides each arrow when there's nothing more to scroll in its direction.</summary>
        private void Roster_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateRosterArrows();

        private void UpdateRosterArrows()
        {
            RosterLeft.IsEnabled = RosterScroll.HorizontalOffset > 1;
            RosterRight.IsEnabled = RosterScroll.HorizontalOffset < RosterScroll.ScrollableWidth - 1;
        }

        // Click-and-drag scrolling, like a touch list. A drag only starts after the mouse moves a few pixels.
        private Point _dragStart;
        private double _dragOffset;
        private bool _dragPending, _dragging;

        private void Roster_DragStart(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(RosterScroll);
            _dragOffset = RosterScroll.HorizontalOffset;
            _dragPending = true;
        }

        private void Roster_DragMove(object sender, MouseEventArgs e)
        {
            if (!_dragPending || e.LeftButton != MouseButtonState.Pressed) { _dragPending = false; return; }
            double dx = e.GetPosition(RosterScroll).X - _dragStart.X;
            if (!_dragging && Math.Abs(dx) > 6)
            {
                _dragging = true;
                RosterScroll.CaptureMouse();
                RosterScroll.Cursor = Cursors.SizeWE;
            }
            if (_dragging) RosterScroll.ScrollToHorizontalOffset(_dragOffset - dx);
        }

        private void Roster_DragEnd(object sender, MouseButtonEventArgs e)
        {
            _dragPending = false;
            if (!_dragging) return;
            _dragging = false;
            RosterScroll.ReleaseMouseCapture();
            RosterScroll.Cursor = null;
            e.Handled = true; // a drag isn't a click
        }

        /// <summary>The wheel scrolls the map and mode rows sideways too.</summary>
        private void TileRow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        // ================================================================== preview / screenshot

        private static readonly string[] Args = Environment.GetCommandLineArgs();
        private static bool PreviewMode => Array.Exists(Args, a => a == "--preview" || a == "--screenshot");

        private static string? ScreenshotPath
        {
            get
            {
                int i = Array.IndexOf(Args, "--screenshot");
                return i >= 0 && i + 1 < Args.Length ? Args[i + 1] : null;
            }
        }

        /// <summary>Renders the window's content to a PNG (the same pixels you'd see on screen).</summary>
        private void SaveScreenshot(string path)
        {
            if (Content is not FrameworkElement root) return;
            root.UpdateLayout();
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using FileStream fs = File.Create(path);
            png.Save(fs);
        }

        // ================================================================== UI helpers

        private void Ready(string message)
        {
            SetStatus(message);
            SetProgress(100, indeterminate: false);
            ProgressText.Text = "";
            PlayButton.IsEnabled = true;  // light it up
            PlayButton.Focus();           // Enter / Space now plays
        }

        private void Fail(string message, Exception ex)
        {
            Debug.WriteLine(ex);
            SetStatus(message);
            SetProgress(0, indeterminate: false);
            ProgressText.Text = "";
            RetryButton.Visibility = Visibility.Visible;
            // If an older build is installed, it can still be played.
            PlayButton.IsEnabled = ReadLocalVersion() != null;
        }

        private void SetStatus(string text) => StatusText.Text = text;

        private void SetProgress(double percent, bool indeterminate)
        {
            DownloadProgress.IsIndeterminate = indeterminate;
            DownloadProgress.Value = percent;
        }

        private void ShowInstalledVersion(string? version)
        {
            VersionText.Text = version == null
                ? "Installed: nothing yet"
                : "Installed build: " + (version.Length > 7 ? version.Substring(0, 7) : version);
        }

        /// <summary>Errors that mean "the network didn't work", as opposed to bugs.</summary>
        private static bool IsNetworkError(Exception ex) =>
            ex is HttpRequestException || ex is TaskCanceledException || ex is OperationCanceledException || ex is IOException;

        private async void Retry_Click(object sender, RoutedEventArgs e) => await CheckAndUpdateAsync();

        // ---- borderless window chrome

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (_busy && MessageBox.Show(this, "An update is running. Close anyway?", "Broken Characters Mod",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Application.Current.Shutdown();
        }
    }
}
