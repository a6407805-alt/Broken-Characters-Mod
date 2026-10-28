// ============================================================================================================
//  SILENT STAGING + OWNER APPROVAL.
//
//  Creators (anyone with a creator key who isn't the owner) just drop their mod files into
//  Risk of Rain 2\BepInEx\plugins\<Mod>\. When their launcher opens, it compares EVERY file in their plugins
//  folder (pack mods + new mod folders, not libraries) with main and copies the differences onto their own branch:
//  staging/<github-name>. The commit notes their installed pack version, so the owner is warned when it was old.
//  That branch is "main + their changes". It never touches main, so nothing reaches friends yet.
//
//  The owner's launcher looks for staging/* branches that are ahead of main and shows the gold review bar:
//    Inspect   - the list of changed files
//    Approve   - copies those files onto the NEWEST main as one commit (the build server then publishes it)
//    Revert    - resets the staging branch back to main (the creator's draft is dropped)
//
//  Everything here is best effort: a creator never sees an error from it (it only writes to Debug).
//  Same rule as the Creator window: mods built from source (src/<Mod>) never upload their dll/pdb/deps.json,
//  because the build server makes those.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BrokenCharactersLauncher
{
    public sealed class StagingStatus
    {
        public string Branch { get; init; } = "";       // e.g. staging/deandre6969
        public string Creator { get; init; } = "";      // whose branch it is
        public string Author { get; init; } = "";       // who made the last commit
        public string PackVersion { get; init; } = "";  // the pack version the creator had installed when they sent it
        public DateTimeOffset Date { get; init; }
        public List<(string Path, string Status, string? Sha)> Files { get; init; } = new List<(string, string, string?)>();
        public IEnumerable<string> Mods => Files.Select(f => f.Path.Split('/')).Where(p => p.Length > 1).Select(p => p[1]).Distinct();
    }

    public static class StagingSync
    {
        private const string Repo = Publisher.Repo;
        private const string Main = "main";
        private const string Prefix = "staging/";

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrokenCharactersLauncher", "1.0"));
            return c;
        }

        public static string? ReadCreatorKey() => Publisher.ReadSavedKey();

        /// <summary>%APPDATA%\BrokenCharacters\staging-log.txt: what silent staging did (it never shows errors on screen).</summary>
        public static readonly string LogFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters", "staging-log.txt");

        public static void Log(string line)
        {
            Debug.WriteLine("Staging: " + line);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);
                var lines = File.Exists(LogFile) ? File.ReadAllLines(LogFile).TakeLast(300).ToList() : new List<string>();
                lines.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line);
                File.WriteAllLines(LogFile, lines);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        private static JsonArray Parents(string sha) { var a = new JsonArray(); a.Add(sha); return a; }

        // ------------------------------------------------------------------ GitHub helpers

        private static async Task<JsonNode?> CallAsync(string key, HttpMethod method, string path, JsonNode? body = null, CancellationToken ct = default)
        {
            string url = path.StartsWith("http", StringComparison.Ordinal) ? path : "https://api.github.com/repos/" + Repo + path;
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage r = await Http.SendAsync(req, ct);
            string text = await r.Content.ReadAsStringAsync(ct);
            if (!r.IsSuccessStatusCode) throw new GitHubException((int)r.StatusCode, "GitHub said " + (int)r.StatusCode + ": " + text);
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        private static async Task<string?> RefShaAsync(string key, string branch, CancellationToken ct)
        {
            try { return (await CallAsync(key, HttpMethod.Get, "/git/ref/heads/" + branch, null, ct))?["object"]?["sha"]?.GetValue<string>(); }
            catch (GitHubException ex) when (ex.Status == 404) { return null; }
        }

        private static async Task<string> TreeOfAsync(string key, string commitSha, CancellationToken ct) =>
            (await CallAsync(key, HttpMethod.Get, "/git/commits/" + commitSha, null, ct))!["tree"]!["sha"]!.GetValue<string>();

        private static async Task SetRefAsync(string key, string branch, string sha, bool force, CancellationToken ct)
        {
            if (await RefShaAsync(key, branch, ct) == null)
                await CallAsync(key, HttpMethod.Post, "/git/refs", new JsonObject { ["ref"] = "refs/heads/" + branch, ["sha"] = sha }, ct);
            else
                await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + branch, new JsonObject { ["sha"] = sha, ["force"] = force }, ct);
        }

        /// <summary>Whose key is this, and is it the owner (the repo's account)?</summary>
        public static async Task<(string Login, bool IsOwner)> WhoAmIAsync(string key, CancellationToken ct = default)
        {
            string login = (await CallAsync(key, HttpMethod.Get, "https://api.github.com/user", null, ct))?["login"]?.GetValue<string>() ?? "";
            return (login, login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsOwner(string login) => login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase);

        // ------------------------------------------------------------------ owner side

        /// <summary>The first staging/* branch that has changes main doesn't have yet (null if none).</summary>
        public static async Task<StagingStatus?> CheckStagingBranchAsync(string key, CancellationToken ct = default) =>
            (await CheckAllStagingAsync(key, ct)).FirstOrDefault();

        /// <summary>Every staging/* branch (one per creator) that has changes main doesn't have yet.</summary>
        public static async Task<List<StagingStatus>> CheckAllStagingAsync(string key, CancellationToken ct = default)
        {
            var found = new List<StagingStatus>();
            JsonNode? refs = await CallAsync(key, HttpMethod.Get, "/git/matching-refs/heads/" + Prefix, null, ct);
            foreach (JsonNode? r in refs?.AsArray() ?? new JsonArray())
            {
                string branch = (r?["ref"]?.GetValue<string>() ?? "").Replace("refs/heads/", "");
                if (branch.Length == 0) continue;
                JsonNode? cmp = await CallAsync(key, HttpMethod.Get, "/compare/" + Main + "..." + Uri.EscapeDataString(branch).Replace("%2F", "/"), null, ct);
                if ((cmp?["ahead_by"]?.GetValue<int>() ?? 0) == 0) continue;
                var files = new List<(string, string, string?)>();
                foreach (JsonNode? f in cmp?["files"]?.AsArray() ?? new JsonArray())
                    if (f != null)
                        files.Add((f["filename"]!.GetValue<string>(), f["status"]?.GetValue<string>() ?? "modified", f["sha"]?.GetValue<string>()));
                if (files.Count == 0) continue;
                JsonNode? last = cmp?["commits"]?.AsArray().LastOrDefault();
                string when = last?["commit"]?["author"]?["date"]?.GetValue<string>() ?? "";
                found.Add(new StagingStatus
                {
                    Branch = branch,
                    Creator = branch.Substring(Prefix.Length),
                    PackVersion = System.Text.RegularExpressions.Regex.Match(last?["commit"]?["message"]?.GetValue<string>() ?? "", @"pack=([0-9a-f]{7,40})").Groups[1].Value,
                    Author = last?["author"]?["login"]?.GetValue<string>() ?? last?["commit"]?["author"]?["name"]?.GetValue<string>() ?? branch.Substring(Prefix.Length),
                    Date = DateTimeOffset.TryParse(when, out DateTimeOffset d) ? d : DateTimeOffset.MinValue,
                    Files = files
                });
            }
            return found;
        }

        /// <summary>
        /// Puts the staged files on top of the NEWEST main as one commit (so nothing pushed to main in the meantime is
        /// lost), moves main to it (the build server builds and publishes), then resets the staging branch to main.
        /// </summary>
        public static async Task<string> ApproveAndPublishAsync(string key, StagingStatus staged, CancellationToken ct = default)
        {
            for (int attempt = 1; ; attempt++)
            {
                string head = (await RefShaAsync(key, Main, ct))!;
                string baseTree = await TreeOfAsync(key, head, ct);
                var entries = new JsonArray();
                foreach ((string path, string status, string? sha) in staged.Files)
                {
                    if (status == "removed") entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = null });
                    else if (sha != null) entries.Add(new JsonObject { ["path"] = path, ["mode"] = "100644", ["type"] = "blob", ["sha"] = sha });
                }
                JsonNode? tree = await CallAsync(key, HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                string mods = string.Join(", ", staged.Mods);
                JsonNode? commit = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = "Merge " + staged.Creator + " mod update" + (mods.Length > 0 ? " (" + mods + ")" : "") + " (approved by owner)",
                    ["tree"] = tree!["sha"]!.GetValue<string>(),
                    ["parents"] = Parents(head)
                }, ct);
                string sha2 = commit!["sha"]!.GetValue<string>();
                try
                {
                    await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + Main, new JsonObject { ["sha"] = sha2, ["force"] = false }, ct);
                }
                catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 4) { await Task.Delay(1500, ct); continue; }
                await SetRefAsync(key, staged.Branch, sha2, true, ct); // staging now equals main again
                return sha2;
            }
        }

        /// <summary>The commit the published pack (release "latest") was built from.</summary>
        public static async Task<string> LatestReleaseShaAsync(string key, CancellationToken ct = default)
        {
            try { return (await CallAsync(key, HttpMethod.Get, "/releases/latest", null, ct))?["target_commitish"]?.GetValue<string>() ?? ""; }
            catch (GitHubException) { return ""; }
        }

        /// <summary>Drops the creator's draft: the staging branch is reset to main's current commit.</summary>
        public static async Task RevertStagingAsync(string key, StagingStatus staged, CancellationToken ct = default)
        {
            string head = (await RefShaAsync(key, Main, ct))!;
            await SetRefAsync(key, staged.Branch, head, true, ct);
        }

        // ------------------------------------------------------------------ creator side

        /// <summary>Risk of Rain 2 folders Steam knows about (same search as install.ps1).</summary>
        public static List<string> FindGameFolders()
        {
            var roots = new List<string>();
            foreach ((Microsoft.Win32.RegistryKey hive, string sub) in new[]
            {
                (Microsoft.Win32.Registry.CurrentUser, @"Software\Valve\Steam"),
                (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
                (Microsoft.Win32.Registry.LocalMachine, @"SOFTWARE\Valve\Steam"),
            })
            {
                try
                {
                    using var k = hive.OpenSubKey(sub);
                    foreach (string name in new[] { "SteamPath", "InstallPath" })
                        if (k?.GetValue(name) is string v && v.Length > 0) roots.Add(v.Replace('/', '\\'));
                }
                catch (Exception ex) when (ex is System.Security.SecurityException || ex is UnauthorizedAccessException || ex is IOException) { }
            }
            var libs = new List<string>(roots);
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch (IOException) { }
            }
            return libs.Distinct(StringComparer.OrdinalIgnoreCase)
                       .Select(l => Path.Combine(l, "steamapps", "common", "Risk of Rain 2"))
                       .Where(g => File.Exists(Path.Combine(g, "Risk of Rain 2.exe")))
                       .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Scans BepInEx\plugins\&lt;Mod&gt;\ for every mod in the pack (or just <paramref name="onlyMod"/>), picks the files
        /// changed since the last pack install that differ from main, and writes "main + those files" to
        /// staging/&lt;login&gt;. Returns how many files are staged (0 = nothing to do). Never throws.
        /// </summary>
        public static async Task<int> SilentStageLocalModAsync(string key, string login, string? installedVersion, DateTime installedUtc,
                                                              string? onlyMod = null, CancellationToken ct = default, bool autoPublish = false,
                                                              IProgress<string>? progress = null)
        {
            int pending = 0; // files found to send; if sending then fails, the caller must NOT update the pack (it would wipe them)
            try
            {
                string? game = FindGameFolders().FirstOrDefault();
                if (game == null) { Log("No Risk of Rain 2 folder found through Steam."); return 0; }
                string plugins = Path.Combine(game, "BepInEx", "plugins");
                if (!Directory.Exists(plugins)) { Log("No plugins folder at " + plugins); return 0; }
                Log("Comparing " + plugins + " with GitHub as " + login + " (pack " + (installedVersion ?? "none") + ")");

                string head = (await RefShaAsync(key, Main, ct))!;
                // Auto-publish safety: if this PC's pack is older than the published one, only files edited AFTER the
                // last install count. Otherwise an old copy of the pack would overwrite newer work for everyone.
                string release = autoPublish ? await LatestReleaseShaAsync(key, ct) : "";
                bool packIsCurrent = !autoPublish || (installedVersion != null && release.Length > 0 &&
                                     release.StartsWith(installedVersion, StringComparison.OrdinalIgnoreCase));
                if (!packIsCurrent) Log("Pack on this PC is older than the published one: only files edited after the last install count.");
                string baseTree = await TreeOfAsync(key, head, ct);
                JsonNode? tree = await CallAsync(key, HttpMethod.Get, "/git/trees/" + baseTree + "?recursive=1", null, ct);
                var onMain = new Dictionary<string, string>(StringComparer.Ordinal);
                var packMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var fromSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var dllOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // OatsBrother.dll -> OatsBrother
                foreach (JsonNode? item in tree?["tree"]?.AsArray() ?? new JsonArray())
                {
                    if (item?["type"]?.GetValue<string>() != "blob") continue;
                    string p = item["path"]!.GetValue<string>();
                    onMain[p] = item["sha"]!.GetValue<string>();
                    string[] parts = p.Split('/');
                    if (parts.Length >= 3 && parts[0] == "mods") packMods.Add(parts[1]);
                    if (parts.Length >= 3 && parts[0] == "src") fromSource.Add(parts[1]);
                    if (parts.Length == 3 && parts[0] == "mods" && parts[2].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        dllOwner.TryAdd(parts[2], parts[1]);
                }

                // Local files that are newer than the pack install and not what main has.
                var changed = new List<(string Remote, string Local)>();
                int checkedFiles = 0;
                progress?.Report("Checking your mods against the pack...");
                await Task.Run(() =>
                {
                foreach (string modDir in Directory.GetDirectories(plugins))
                {
                    string folder = Path.GetFileName(modDir);
                    // Every pack mod, plus brand-new mod folders (not libraries like R2API / RiskofThunder-*).
                    string? mod = packMods.FirstOrDefault(m => m.Equals(folder, StringComparison.OrdinalIgnoreCase));
                    // A folder named differently from the pack (plugins\Brother holds OatsBrother.dll): match it by its dll,
                    // or it would be published as a second copy of the same mod.
                    if (mod == null)
                        foreach (string dll in Directory.GetFiles(modDir, "*.dll"))
                            if (dllOwner.TryGetValue(Path.GetFileName(dll), out string? owner)) { mod = owner; break; }
                    if (mod == null)
                    {
                        bool library = QuickUpload.IsReservedName(folder) || folder.Contains('-') || folder.StartsWith("MMHOOK", StringComparison.OrdinalIgnoreCase)
                                       || !Directory.EnumerateFiles(modDir, "*.dll", SearchOption.AllDirectories).Any();
                        if (library || !QuickUpload.IsValidName(folder)) continue;
                        mod = folder;
                    }
                    if (mod.Equals("Duelist", StringComparison.OrdinalIgnoreCase) || mod.Equals("DuelistMod", StringComparison.OrdinalIgnoreCase)) continue;
                    if (onlyMod != null && !mod.Equals(onlyMod, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string file in Directory.EnumerateFiles(modDir, "*", SearchOption.AllDirectories))
                    {
                        var info = new FileInfo(file);
                        // Owner's rule: compare EVERYTHING in the plugins folder with GitHub (the owner reviews it all).
                        if (info.Length > 90L * 1024 * 1024) { Log("  too big for GitHub (over 90 MB), not sent: " + file); continue; }
                        string rel = Path.GetRelativePath(modDir, file).Replace('\\', '/');
                        string ext = info.Extension.ToLowerInvariant();
                        if (ext == ".cfg" || ext == ".log" || ext == ".tmp" || ext == ".bak") continue;
                        bool topLevel = !rel.Contains('/');
                        if (fromSource.Contains(mod) && topLevel &&
                            (ext == ".dll" || ext == ".pdb" || file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))) continue;
                        string remote = "mods/" + mod + "/" + rel;
                        // Stale guard only for files GitHub already has (an old copy must not overwrite newer work).
                        // Files GitHub doesn't have at all (a new mod's bundle, icons...) always go up.
                        if (!packIsCurrent && onMain.ContainsKey(remote) && info.LastWriteTimeUtc <= installedUtc.AddMinutes(1)) continue;
                        if (++checkedFiles % 25 == 0) progress?.Report("Checking your mods against the pack... " + checkedFiles + " files checked, " + changed.Count + " different");
                        if (onMain.TryGetValue(remote, out string? sha) && sha == Publisher.GitBlobSha(file)) continue;
                        changed.Add((remote, file));
                    }
                }
                }, ct);
                pending = changed.Count;
                Log("Checked " + checkedFiles + " file(s), " + changed.Count + " differ from GitHub.");
                if (changed.Count == 0) { Log("Nothing differs from GitHub. Nothing sent."); return 0; }
                foreach ((string remote, _) in changed.Take(50)) Log("  differs: " + remote);

                // "main + my changes", rebuilt fresh every time so the branch never drifts from main.
                string branch = Prefix + login.ToLowerInvariant();
                var entries = new JsonArray();
                long totalBytes = changed.Sum(c => new FileInfo(c.Local).Length), sentBytes = 0;
                int n = 0;
                foreach ((string remote, string local) in changed)
                {
                    long size = new FileInfo(local).Length;
                    progress?.Report("Uploading your mod changes " + (++n) + " of " + changed.Count + ": " + Path.GetFileName(local) +
                                     " (" + (sentBytes / 1048576.0).ToString("0.0") + " of " + (totalBytes / 1048576.0).ToString("0.0") + " MB)");
                    sentBytes += size;
                    JsonNode? blob = await CallAsync(key, HttpMethod.Post, "/git/blobs", new JsonObject
                    {
                        ["content"] = Convert.ToBase64String(await File.ReadAllBytesAsync(local, ct)),
                        ["encoding"] = "base64"
                    }, ct);
                    entries.Add(new JsonObject { ["path"] = remote, ["mode"] = "100644", ["type"] = "blob", ["sha"] = blob!["sha"]!.GetValue<string>() });
                }
                JsonNode? newTree = await CallAsync(key, HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                string newTreeSha = newTree!["sha"]!.GetValue<string>();

                if (autoPublish)
                {
                    // No approval: straight onto main as one commit. The build server builds and publishes it for everyone.
                    var modNames = changed.Select(c => c.Remote.Split('/')[1]).Distinct().ToList();
                    for (int attempt = 1; ; attempt++)
                    {
                        JsonNode? auto = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                        {
                            ["message"] = "Auto-update from " + login + ": " + changed.Count + " file(s) in " + string.Join(", ", modNames) +
                                          "\n\nSent automatically by " + login + "'s launcher (from BepInEx\\plugins). pack=" + (installedVersion ?? "none"),
                            ["tree"] = newTreeSha,
                            ["parents"] = Parents(head)
                        }, ct);
                        try
                        {
                            await CallAsync(key, new HttpMethod("PATCH"), "/git/refs/heads/" + Main,
                                new JsonObject { ["sha"] = auto!["sha"]!.GetValue<string>(), ["force"] = false }, ct);
                            Log("Published " + changed.Count + " file(s) to everyone (" + string.Join(", ", modNames) + "). The build takes about 5 to 8 minutes.");
                            return changed.Count;
                        }
                        catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 4)
                        {
                            // Someone pushed at the same moment: rebuild the same files on top of their commit.
                            await Task.Delay(1500, ct);
                            head = (await RefShaAsync(key, Main, ct))!;
                            baseTree = await TreeOfAsync(key, head, ct);
                            newTree = await CallAsync(key, HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                            newTreeSha = newTree!["sha"]!.GetValue<string>();
                        }
                    }
                }

                // Same files as what's already staged? Leave the branch alone (no new commit every launch).
                string? current = await RefShaAsync(key, branch, ct);
                if (current != null && await TreeOfAsync(key, current, ct) == newTreeSha) { Log("Same " + changed.Count + " file(s) already sent, still waiting for the owner."); return changed.Count; }

                var mods = changed.Select(c => c.Remote.Split('/')[1]).Distinct();
                JsonNode? commit = await CallAsync(key, HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = "Staging: " + login + " changed " + changed.Count + " file(s) in " + string.Join(", ", mods) +
                                  "\n\npack=" + (installedVersion ?? "none") + " [skip ci]",
                    ["tree"] = newTreeSha,
                    ["parents"] = Parents(head)
                }, ct);
                await SetRefAsync(key, branch, commit!["sha"]!.GetValue<string>(), true, ct);
                Log("Sent " + changed.Count + " file(s) to " + branch + " for the owner to review.");
                return changed.Count;
            }
            catch (Exception ex) // silent on purpose: a creator never sees staging errors
            {
                Log("Skipped (error): " + ex.Message + (pending > 0 ? "  (" + pending + " file(s) NOT sent)" : ""));
                return pending > 0 ? -pending : 0;
            }
        }
    }
}
