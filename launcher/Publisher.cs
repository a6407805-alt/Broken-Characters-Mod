// ============================================================================================================
//  Publisher - the engine behind the launcher's "Creator" window.
//
//  A mod creator picks the folder where their mod lives (a copy of the repo: it has src\ and mods\ inside) and
//  presses PUBLISH. This class:
//    1. checks the creator key (a GitHub token that can write to this one repo) and finds out who it belongs to,
//    2. lists the mods in the folder that changed (every creator may update every mod; creators.json only notes who added one),
//    3. compares the local files with what is online and lists what changed,
//    4. uploads ONLY the changed files that belong to the chosen mods, as one commit on top of the newest main.
//       Nothing else in the repo can be touched, and nothing is ever deleted, so one person's publish can't
//       overwrite another person's mod. If main moved while uploading, it simply tries again on top of it.
//    5. watches the build server and reports whether the mod built and is live (or the exact errors).
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BrokenCharactersLauncher
{
    /// <summary>A GitHub call that came back with an error status.</summary>
    public sealed class GitHubException : Exception
    {
        public int Status { get; }
        public GitHubException(int status, string message) : base(message) { Status = status; }
    }

    /// <summary>One local file that belongs to a mod, and whether it differs from the online copy.</summary>
    public sealed class PublishFile
    {
        public string RemotePath { get; init; } = "";
        public string LocalPath { get; init; } = "";
        public long Size { get; init; }
        public bool Changed { get; init; }
        public bool ExistsOnline { get; init; }
        public string? BlobSha { get; set; }
    }

    /// <summary>One mod in the Creator window's list.</summary>
    public sealed class ModPlan : INotifyPropertyChanged
    {
        public string Name { get; init; } = "";
        public List<PublishFile> Files { get; init; } = new List<PublishFile>();
        public bool IsNewMod { get; init; }

        /// <summary>Set when someone else changed this mod online after the creator did.</summary>
        public string Conflict { get; init; } = "";

        public int ChangedCount => Files.Count(f => f.Changed);
        public bool HasConflict => Conflict.Length > 0;
        public System.Windows.Visibility ConflictVisibility => HasConflict ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        private bool _selected;
        public bool IsSelected { get => _selected; set { _selected = value; Raise(nameof(IsSelected)); } }

        private bool _overwrite;
        /// <summary>The creator ticked "publish anyway" for a mod that someone else changed online.</summary>
        public bool OverwriteOk { get => _overwrite; set { _overwrite = value; Raise(nameof(OverwriteOk)); } }

        /// <summary>Will pressing PUBLISH include this mod?</summary>
        public bool WillPublish => IsSelected && ChangedCount > 0 && (!HasConflict || OverwriteOk);

        public string StatusText =>
            ChangedCount == 0 ? "Up to date" :
            (IsNewMod ? "New mod: " : "") + ChangedCount + (ChangedCount == 1 ? " file changed" : " files changed");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>What the build server said about a published commit.</summary>
    public sealed class BuildReport
    {
        public bool Finished { get; init; }
        public bool Success { get; set; }
        public List<string> Lines { get; init; } = new List<string>();
        public List<string> Errors { get; init; } = new List<string>();
    }

    public sealed class Publisher
    {
        public const string Repo = "a6407805-alt/Broken-Characters-Mod";
        private const string Branch = "main";
        private const long MaxFileBytes = 90L * 1024 * 1024; // GitHub refuses files over 100 MB

        // The creator key lives next to the extras key (same style: a small text file in the user's own profile).
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters");
        public static readonly string KeyFile = Path.Combine(ConfigDir, "creator-key.txt");
        public static readonly string FolderFile = Path.Combine(ConfigDir, "creator-folder.txt");

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrokenCharactersLauncher", "1.0"));
            return client;
        }

        private readonly string _token;
        public string Login { get; private set; } = "";
        public bool IsAdmin => Login.Equals(Repo.Split('/')[0], StringComparison.OrdinalIgnoreCase);

        /// <summary>The owner's rule: the pack is ONE shared mod, so every creator may update every mod (Vince updates
        /// existing mods). creators.json now only records who added a mod first; it no longer locks anyone out.</summary>
        public bool CanEditAll => true;

        public Publisher(string token) { _token = token.Trim(); }

        public static string? ReadSavedKey()
        {
            try { return File.Exists(KeyFile) ? File.ReadAllText(KeyFile).Trim() : null; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        public static void SaveKey(string key)
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(KeyFile, key.Trim());
        }

        public static void ForgetKey()
        {
            try { if (File.Exists(KeyFile)) File.Delete(KeyFile); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        public static string? ReadSavedFolder()
        {
            try { return File.Exists(FolderFile) ? File.ReadAllText(FolderFile).Trim() : null; }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return null; }
        }

        public static void SaveFolder(string folder)
        {
            try { Directory.CreateDirectory(ConfigDir); File.WriteAllText(FolderFile, folder); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        // ------------------------------------------------------------------ GitHub plumbing

        private async Task<JsonNode?> CallAsync(HttpMethod method, string path, JsonNode? body = null, CancellationToken ct = default)
        {
            string url = path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : "https://api.github.com/repos/" + Repo + path;
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (body != null) request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            string text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
                throw new GitHubException((int)response.StatusCode, Friendly((int)response.StatusCode, text));
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }

        /// <summary>
        /// Uploads a file (a jukebox MP3) to the repo's "jukebox" release (made on first use), so it never bloats git.
        /// Returns the public download address. An existing file with the same name is replaced.
        /// </summary>
        public async Task<string> UploadReleaseFileAsync(string tag, string assetName, string localPath, string contentType, CancellationToken ct = default)
        {
            JsonNode? release;
            try { release = await CallAsync(HttpMethod.Get, "/releases/tags/" + tag, null, ct); }
            catch (GitHubException ex) when (ex.Status == 404)
            {
                release = await CallAsync(HttpMethod.Post, "/releases", new JsonObject
                {
                    ["tag_name"] = tag,
                    ["target_commitish"] = Branch,
                    ["name"] = "Jukebox songs",
                    ["body"] = "MP3s shared from the launcher's JUKEBOX. Not a game build.",
                    ["prerelease"] = true,
                    ["make_latest"] = "false"
                }, ct);
            }
            long id = release!["id"]!.GetValue<long>();
            foreach (JsonNode? a in release["assets"]?.AsArray() ?? new JsonArray())
                if (a?["name"]?.GetValue<string>() == assetName)
                    await CallAsync(HttpMethod.Delete, "/releases/assets/" + a["id"]!.GetValue<long>(), null, ct);

            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://uploads.github.com/repos/" + Repo + "/releases/" + id + "/assets?name=" + Uri.EscapeDataString(assetName));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            await using FileStream fs = File.OpenRead(localPath);
            request.Content = new StreamContent(fs);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(10));
            using HttpResponseMessage response = await Http.SendAsync(request, cts.Token);
            string text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode) throw new GitHubException((int)response.StatusCode, Friendly((int)response.StatusCode, text));
            return JsonNode.Parse(text)!["browser_download_url"]!.GetValue<string>();
        }

        private static string Friendly(int status, string text)
        {
            if (status == 401) return "That creator key doesn't work (it may have expired). Ask for a new one.";
            if (status == 403 || status == 404) return "This key isn't allowed to publish to the pack. Ask for a new one.";
            string detail = text;
            try { detail = JsonNode.Parse(text)?["message"]?.GetValue<string>() ?? text; } catch (JsonException) { }
            return "GitHub said no (" + status + "): " + detail;
        }

        /// <summary>Checks the key and learns whose it is.</summary>
        public async Task<string> SignInAsync(CancellationToken ct = default)
        {
            JsonNode? me = await CallAsync(HttpMethod.Get, "https://api.github.com/user", null, ct);
            Login = me?["login"]?.GetValue<string>() ?? "";
            if (Login.Length == 0) throw new GitHubException(401, Friendly(401, ""));
            // The key must also be able to see (and write to) the repo, otherwise find out now, not after uploading.
            JsonNode? repo = await CallAsync(HttpMethod.Get, "", null, ct);
            JsonNode? pushNode = repo?["permissions"]?["push"];
            bool canPush = pushNode == null || pushNode.GetValue<bool>(); // if GitHub doesn't say, the first upload will
            if (!canPush) throw new GitHubException(403, Friendly(403, ""));
            return Login;
        }

        /// <summary>
        /// Reads one text file from main, lets <paramref name="change"/> rewrite it (null = file doesn't exist yet), and
        /// saves it back as one commit. Used by the Jukebox window for launcher/jukebox.json. The commit message gets
        /// "[skip ci]" so adding a song doesn't rebuild the whole pack.
        /// </summary>
        public async Task UpdateTextFileAsync(string repoPath, Func<string?, string> change, string message, CancellationToken ct = default)
        {
            string? sha = null, current = null;
            try
            {
                JsonNode? node = await CallAsync(HttpMethod.Get, "/contents/" + repoPath + "?ref=" + Branch, null, ct);
                sha = node?["sha"]?.GetValue<string>();
                string? b64 = node?["content"]?.GetValue<string>();
                if (b64 != null) current = Encoding.UTF8.GetString(Convert.FromBase64String(b64.Replace("\n", "").Replace("\r", "")));
            }
            catch (GitHubException ex) when (ex.Status == 404) { }

            var body = new JsonObject
            {
                ["message"] = message + " [skip ci]",
                ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(change(current))),
                ["branch"] = Branch,
            };
            if (sha != null) body["sha"] = sha;
            await CallAsync(HttpMethod.Put, "/contents/" + repoPath, body, ct);
        }

        private async Task<(string HeadSha, string TreeSha)> GetHeadAsync(CancellationToken ct)
        {
            JsonNode? reference = await CallAsync(HttpMethod.Get, "/git/ref/heads/" + Branch, null, ct);
            string head = reference!["object"]!["sha"]!.GetValue<string>();
            JsonNode? commit = await CallAsync(HttpMethod.Get, "/git/commits/" + head, null, ct);
            return (head, commit!["tree"]!["sha"]!.GetValue<string>());
        }

        private async Task<JsonNode> ReadCreatorsAsync(string refName, CancellationToken ct)
        {
            try
            {
                JsonNode? file = await CallAsync(HttpMethod.Get, "/contents/launcher/creators.json?ref=" + refName, null, ct);
                string b64 = (file?["content"]?.GetValue<string>() ?? "").Replace("\n", "");
                string json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
                return JsonNode.Parse(json) ?? new JsonObject();
            }
            catch (GitHubException ex) when (ex.Status == 404) { return new JsonObject(); }
        }

        private static List<string> OwnedMods(JsonNode creators, string login)
        {
            var owned = new List<string>();
            if (creators["creators"] is JsonObject map)
                foreach (var kv in map)
                    if (kv.Key.Equals(login, StringComparison.OrdinalIgnoreCase) && kv.Value is JsonArray arr)
                        foreach (JsonNode? n in arr) if (n != null) owned.Add(n.GetValue<string>());
            return owned;
        }

        /// <summary>The existing mods this person may update (admins: every mod in mods/), for the quick-upload name box.</summary>
        public async Task<List<string>> PublishableModsAsync(CancellationToken ct = default)
        {
            (string head, string treeSha) = await GetHeadAsync(ct);
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CanEditAll || IsAdmin)
            {
                JsonNode? tree = await CallAsync(HttpMethod.Get, "/git/trees/" + treeSha + "?recursive=1", null, ct);
                foreach (JsonNode? item in tree?["tree"]?.AsArray() ?? new JsonArray())
                {
                    string[] parts = (item?["path"]?.GetValue<string>() ?? "").Split('/');
                    if (item?["type"]?.GetValue<string>() == "tree" && parts.Length == 2 && parts[0] == "mods") names.Add(parts[1]);
                }
            }
            else
                foreach (string mod in OwnedMods(await ReadCreatorsAsync(head, ct), Login))
                    if (mod != "*") names.Add(mod);
            return names.ToList();
        }

        // ------------------------------------------------------------------ scanning

        private static readonly string[] SkipFolders = { "bin", "obj", ".vs", ".git", ".idea", "node_modules" };
        private static readonly string[] SkipExtensions = { ".user", ".suo", ".tmp", ".bak" };

        /// <summary>The files that belong to one mod: its source, shipped files, Unity sources, card and pictures.</summary>
        private static IEnumerable<(string Remote, string Local)> ModFiles(string folder, string mod, bool hasSource)
        {
            foreach (string area in new[] { "src", "mods", "unity" })
            {
                string dir = Path.Combine(folder, area, mod);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
                    string[] parts = rel.Split('/');
                    if (parts.Take(parts.Length - 1).Any(p => SkipFolders.Contains(p, StringComparer.OrdinalIgnoreCase))) continue;
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (SkipExtensions.Contains(ext)) continue;
                    // The build server compiles src\<Mod> and puts the fresh dll in mods\<Mod>. A copied dll would
                    // only go stale (and overwrite the fresh one), so mods built from source never upload dlls.
                    if (area == "mods" && hasSource && parts.Length == 1 &&
                        (ext == ".dll" || ext == ".pdb" || file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))) continue;
                    yield return (area + "/" + mod + "/" + rel, file);
                }
            }

            string card = Path.Combine(folder, "launcher", "cards", mod + ".json");
            if (File.Exists(card)) yield return ("launcher/cards/" + mod + ".json", card);
            string portrait = Path.Combine(folder, "launcher", "Assets", "Roster", mod + ".png");
            if (File.Exists(portrait)) yield return ("launcher/Assets/Roster/" + mod + ".png", portrait);
            string tiles = Path.Combine(folder, "launcher", "Assets", "Tiles");
            if (Directory.Exists(tiles))
                foreach (string tile in Directory.EnumerateFiles(tiles, mod + "*.png"))
                    yield return ("launcher/Assets/Tiles/" + Path.GetFileName(tile), tile);
        }

        /// <summary>The same fingerprint git gives a file, so we can tell "changed" from "same" without uploading.</summary>
        private static string GitBlobSha(string path)
        {
            using FileStream fs = File.OpenRead(path);
            using var sha = SHA1.Create();
            byte[] header = Encoding.ASCII.GetBytes("blob " + fs.Length + "\0");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            var buffer = new byte[81920];
            int n;
            while ((n = fs.Read(buffer, 0, buffer.Length)) > 0) sha.TransformBlock(buffer, 0, n, null, 0);
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }

        /// <summary>Looks at the chosen folder and returns the mods this person may publish, with what changed.</summary>
        public async Task<List<ModPlan>> ScanAsync(string folder, CancellationToken ct = default)
        {
            if (!Directory.Exists(Path.Combine(folder, "src")) && !Directory.Exists(Path.Combine(folder, "mods")))
                throw new InvalidOperationException("That folder has no \"src\" or \"mods\" folder inside. Pick the folder your Claude keeps the mod in.");

            (string head, string treeSha) = await GetHeadAsync(ct);
            JsonNode? tree = await CallAsync(HttpMethod.Get, "/git/trees/" + treeSha + "?recursive=1", null, ct);
            var remote = new Dictionary<string, string>(StringComparer.Ordinal);
            var remoteMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var remoteSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JsonNode? item in tree!["tree"]!.AsArray())
            {
                if (item == null || item["type"]?.GetValue<string>() != "blob") continue;
                string path = item["path"]!.GetValue<string>();
                remote[path] = item["sha"]!.GetValue<string>();
                string[] parts = path.Split('/');
                if (parts.Length >= 3 && (parts[0] == "src" || parts[0] == "mods")) remoteMods.Add(parts[1]);
                if (parts.Length >= 3 && parts[0] == "src") remoteSource.Add(parts[1]);
            }

            JsonNode creators = await ReadCreatorsAsync(head, ct);
            List<string> owned = OwnedMods(creators, Login);

            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string area in new[] { "src", "mods" })
            {
                string dir = Path.Combine(folder, area);
                if (Directory.Exists(dir))
                    foreach (string sub in Directory.GetDirectories(dir)) names.Add(Path.GetFileName(sub));
            }

            var plans = new List<ModPlan>();
            foreach (string mod in names)
            {
                bool isNew = !remoteMods.Contains(mod);
                bool allowed = CanEditAll || IsAdmin || isNew || owned.Contains("*") || owned.Contains(mod, StringComparer.OrdinalIgnoreCase);
                if (!allowed) continue; // someone else's mod: not even shown, so it can't be touched by accident

                bool hasSource = Directory.Exists(Path.Combine(folder, "src", mod)) || remoteSource.Contains(mod);
                var files = await Task.Run(() =>
                {
                    var list = new List<PublishFile>();
                    foreach ((string rp, string lp) in ModFiles(folder, mod, hasSource))
                    {
                        long size = new FileInfo(lp).Length;
                        if (size > MaxFileBytes) continue;
                        bool online = remote.TryGetValue(rp, out string? sha);
                        bool changed = !online || sha != GitBlobSha(lp);
                        list.Add(new PublishFile { RemotePath = rp, LocalPath = lp, Size = size, Changed = changed, ExistsOnline = online });
                    }
                    return list;
                }, ct);

                if ((CanEditAll || IsAdmin) && !files.Any(f => f.Changed)) continue; // everyone has every mod: only list the ones with changes

                string conflict = "";
                int changedCount = files.Count(f => f.Changed);
                if (!isNew && changedCount > 0) conflict = await FindConflictAsync(files, ct);

                plans.Add(new ModPlan { Name = mod, Files = files, IsNewMod = isNew, Conflict = conflict, IsSelected = changedCount > 0 });
            }
            return plans;
        }

        /// <summary>
        /// "Would publishing wipe out someone else's change?" For the files that differ from the online copy (and exist
        /// online), looks at who changed each one last. If it wasn't this person, their local copy may be out of date,
        /// so the window asks first. Checks at most 8 files (code files first) to stay quick.
        /// </summary>
        private async Task<string> FindConflictAsync(List<PublishFile> files, CancellationToken ct)
        {
            try
            {
                IEnumerable<PublishFile> suspects = files.Where(f => f.Changed && f.ExistsOnline)
                    .OrderBy(f => f.RemotePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .Take(8);
                foreach (PublishFile file in suspects)
                {
                    JsonNode? list = await CallAsync(HttpMethod.Get,
                        "/commits?sha=" + Branch + "&per_page=1&path=" + Uri.EscapeDataString(file.RemotePath), null, ct);
                    if (list is not JsonArray arr || arr.Count == 0) continue;
                    JsonNode? c = arr[0];
                    string who = c?["author"]?["login"]?.GetValue<string>() ?? "";
                    if (who.Length == 0 || who.Equals(Login, StringComparison.OrdinalIgnoreCase)) continue;
                    string when = c?["commit"]?["author"]?["date"]?.GetValue<string>() ?? "";
                    string day = DateTimeOffset.TryParse(when, out DateTimeOffset d) ? d.LocalDateTime.ToString("MMM d") : "recently";
                    return "Someone else (" + who + ") changed this mod online on " + day + ". Ask your Claude to \"pull the latest\", then press Refresh.";
                }
            }
            catch (GitHubException) { } // the check is a safety net; if it can't run, don't block
            return "";
        }

        // ------------------------------------------------------------------ publishing

        /// <summary>Uploads the chosen mods' changed files as one commit on top of the newest main. Returns the commit's SHA.</summary>
        public async Task<string> PublishAsync(IReadOnlyList<ModPlan> plans, string message, IProgress<(int Percent, string Text)> progress, CancellationToken ct = default)
        {
            List<PublishFile> files = plans.SelectMany(p => p.Files.Where(f => f.Changed)).ToList();
            if (files.Count == 0) throw new InvalidOperationException("Nothing to publish.");

            int done = 0;
            foreach (PublishFile f in files)
            {
                progress.Report((done * 85 / files.Count, "Uploading " + (done + 1) + " of " + files.Count + ": " + Path.GetFileName(f.LocalPath)));
                var blob = new JsonObject
                {
                    ["content"] = Convert.ToBase64String(await File.ReadAllBytesAsync(f.LocalPath, ct)),
                    ["encoding"] = "base64"
                };
                JsonNode? made = await CallAsync(HttpMethod.Post, "/git/blobs", blob, ct);
                f.BlobSha = made!["sha"]!.GetValue<string>();
                done++;
            }

            progress.Report((90, "Saving your changes to the pack..."));
            var newMods = plans.Where(p => p.IsNewMod && !IsAdmin).Select(p => p.Name).ToList();

            for (int attempt = 1; ; attempt++)
            {
                (string head, string baseTree) = await GetHeadAsync(ct);
                var entries = new JsonArray();
                foreach (PublishFile f in files)
                    entries.Add(new JsonObject { ["path"] = f.RemotePath, ["mode"] = "100644", ["type"] = "blob", ["sha"] = f.BlobSha });

                if (newMods.Count > 0)
                {
                    // A brand-new mod is registered to its creator in the same commit.
                    JsonNode creators = await ReadCreatorsAsync(head, ct);
                    if (creators["creators"] is not JsonObject map) { map = new JsonObject(); creators["creators"] = map; }
                    string key = map.Select(kv => kv.Key).FirstOrDefault(k => k.Equals(Login, StringComparison.OrdinalIgnoreCase)) ?? Login;
                    if (map[key] is not JsonArray list) { list = new JsonArray(); map[key] = list; }
                    foreach (string mod in newMods)
                        if (!list.Any(n => n != null && n.GetValue<string>().Equals(mod, StringComparison.OrdinalIgnoreCase))) list.Add(mod);
                    string json = creators.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
                    entries.Add(new JsonObject { ["path"] = "launcher/creators.json", ["mode"] = "100644", ["type"] = "blob", ["content"] = json });
                }

                JsonNode? tree = await CallAsync(HttpMethod.Post, "/git/trees", new JsonObject { ["base_tree"] = baseTree, ["tree"] = entries }, ct);
                var parents = new JsonArray();
                parents.Add(head);
                JsonNode? commit = await CallAsync(HttpMethod.Post, "/git/commits", new JsonObject
                {
                    ["message"] = message,
                    ["tree"] = tree!["sha"]!.GetValue<string>(),
                    ["parents"] = parents
                }, ct);
                string sha = commit!["sha"]!.GetValue<string>();

                try
                {
                    await CallAsync(new HttpMethod("PATCH"), "/git/refs/heads/" + Branch, new JsonObject { ["sha"] = sha, ["force"] = false }, ct);
                    progress.Report((100, "Published."));
                    return sha;
                }
                catch (GitHubException ex) when ((ex.Status == 422 || ex.Status == 409) && attempt < 4)
                {
                    // Someone pushed at the same moment. Our files don't overlap theirs, so just build on top of theirs.
                    progress.Report((92, "Someone else just published. Trying again on top of it..."));
                    await Task.Delay(1500, ct);
                }
            }
        }

        // ------------------------------------------------------------------ watching the build

        /// <summary>Polls the build server until the commit's build finishes, then explains the result in plain words.</summary>
        public async Task<BuildReport> WatchBuildAsync(string commitSha, IReadOnlyList<ModPlan> plans, IProgress<string> progress, CancellationToken ct = default)
        {
            DateTime started = DateTime.UtcNow;
            JsonNode? run = null;
            while (DateTime.UtcNow - started < TimeSpan.FromMinutes(25))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    JsonNode? runs = await CallAsync(HttpMethod.Get, "/actions/runs?head_sha=" + commitSha + "&event=push&per_page=5", null, ct);
                    run = runs?["workflow_runs"]?.AsArray().FirstOrDefault();
                }
                catch (GitHubException) { run = null; }

                int minutes = (int)(DateTime.UtcNow - started).TotalMinutes;
                string status = run?["status"]?.GetValue<string>() ?? "";
                if (status == "completed") break;
                progress.Report(run == null ? "Waiting for the build to start..." : "Building your mod on the build server... (" + minutes + " min)");
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }

            if (run == null || run["status"]?.GetValue<string>() != "completed")
                return new BuildReport { Finished = false, Lines = { "Still building after 25 minutes. Check back later: your files ARE published." } };

            bool success = run["conclusion"]?.GetValue<string>() == "success";
            var annotations = new List<(string Level, string Message, string Path)>();
            try
            {
                string runId = run["id"]!.GetValue<long>().ToString();
                JsonNode? jobs = await CallAsync(HttpMethod.Get, "/actions/runs/" + runId + "/jobs?per_page=30", null, ct);
                foreach (JsonNode? job in jobs?["jobs"]?.AsArray() ?? new JsonArray())
                {
                    if (job == null) continue;
                    string id = job["id"]!.GetValue<long>().ToString();
                    JsonNode? list = await CallAsync(HttpMethod.Get, "/check-runs/" + id + "/annotations?per_page=100", null, ct);
                    foreach (JsonNode? a in list?.AsArray() ?? new JsonArray())
                        if (a != null)
                            annotations.Add((a["annotation_level"]?.GetValue<string>() ?? "",
                                             a["message"]?.GetValue<string>() ?? "",
                                             a["path"]?.GetValue<string>() ?? ""));
                }
            }
            catch (GitHubException) { } // annotations are a bonus

            var report = new BuildReport { Finished = true };
            bool allBuilt = true;
            foreach (ModPlan plan in plans)
            {
                bool hasSource = plan.Files.Any(f => f.RemotePath.StartsWith("src/", StringComparison.Ordinal));
                if (!hasSource) continue;
                bool built = annotations.Any(a => a.Message.Contains(plan.Name + " built from source", StringComparison.OrdinalIgnoreCase));
                bool failed = annotations.Any(a => a.Message.Contains(plan.Name + " failed to build", StringComparison.OrdinalIgnoreCase));
                if (built && !failed) report.Lines.Add("✔ " + plan.Name + " built OK.");
                else
                {
                    allBuilt = false;
                    report.Lines.Add("✘ " + plan.Name + " did NOT build, so the pack still has the old version.");
                    foreach (var err in annotations.Where(x => x.Level == "failure" && (x.Path.Contains("/" + plan.Name + "/") || x.Message.Contains(plan.Name))).Take(8))
                        report.Errors.Add(err.Message.Trim());
                }
            }

            if (success && allBuilt) { report.Lines.Add("✔ Live! Players get it the next time they press Update."); }
            else
            {
                report.Lines.Add(success ? "The pack was published, but a mod above didn't build." : "✘ The pack's build failed.");
                if (report.Errors.Count == 0)
                    foreach (var err in annotations.Where(x => x.Level == "failure").Take(8)) report.Errors.Add(err.Message.Trim());
                report.Lines.Add("Press \"Copy errors for Claude\", paste them to your Claude, and it will fix it.");
            }
            report.Success = success && allBuilt;
            return report;
        }
    }
}
