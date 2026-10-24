// ============================================================================================================
//  Jukebox songs for the launcher. The Jukebox mod (src/Jukebox) plays whatever is in
//  %APPDATA%\BrokenCharacters\Jukebox\ and reads playlist.json there; this file fills that folder.
//
//  Two lists:
//    - the group list: launcher/jukebox.json on main (Suno links only, no audio). Anyone with a creator key can
//      add or remove songs; every launcher downloads them.
//    - my list: local-songs.json in the Jukebox folder, for songs only this player wants.
//  Audio comes straight from Suno's CDN (cdn1.suno.ai/<id>.mp3), so no music is stored in the public repo.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BrokenCharactersLauncher
{
    public sealed class JukeboxSong
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("artist")] public string Artist { get; set; } = "";
        [JsonPropertyName("audio")] public string? Audio { get; set; }
        [JsonPropertyName("added")] public string? Added { get; set; }
        [JsonPropertyName("addedBy")] public string? AddedBy { get; set; }

        /// <summary>Set for an MP3/WAV the player dropped into the Jukebox folder themselves (not in any list).</summary>
        [JsonIgnore] public string? FilePath { get; set; }
    }

    public sealed class JukeboxFile
    {
        [JsonPropertyName("songs")] public List<JukeboxSong> Songs { get; set; } = new List<JukeboxSong>();
        /// <summary>Songs someone pressed Remove on: every PC deletes its copy, and nobody re-shares them.</summary>
        [JsonPropertyName("removed")] public List<string> Removed { get; set; } = new List<string>();
    }

    public static class JukeboxLibrary
    {
        public const string RepoPath = "launcher/jukebox.json";
        private const string SharedUrl = "https://raw.githubusercontent.com/" + Publisher.Repo + "/main/" + RepoPath;

        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters", "Jukebox");
        private static readonly string LocalFile = Path.Combine(Folder, "local-songs.json");
        private static readonly string SharedCache = Path.Combine(Folder, "group-songs.json");
        private static readonly string PlaylistFile = Path.Combine(Folder, "playlist.json");

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        private static readonly HttpClient Http = CreateHttp();
        private static readonly SemaphoreSlim SyncLock = new SemaphoreSlim(1, 1);

        private static HttpClient CreateHttp()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            // Suno's site and CDN can refuse non-browser clients, so ask like a browser does.
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", BrowserAgent);
            return c;
        }

        private const string BrowserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";

        /// <summary>An id for a shared MP3 file (from its file name): lowercase letters, digits and dashes.</summary>
        public static string FileSongId(string name)
        {
            string id = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            while (id.StartsWith("mp3-", StringComparison.Ordinal)) id = id.Substring(4); // already a shared file's id
            return "mp3-" + (id.Length == 0 ? Guid.NewGuid().ToString("N").Substring(0, 8) : id.Length > 60 ? id.Substring(0, 60) : id);
        }

        /// <summary>The jukebox comes with the friends-only extras: only PCs with an extras key download the songs.</summary>
        public static bool ExtrasEnabled => File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters", "extras-key.txt"));

        public static string SongPath(string id) => Path.Combine(Folder, id + ".mp3");
        public static bool IsDownloaded(string id) => File.Exists(SongPath(id));

        // ------------------------------------------------------------------ lists

        public static JukeboxFile Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new JukeboxFile();
            try { return JsonSerializer.Deserialize<JukeboxFile>(json, Json) ?? new JukeboxFile(); }
            catch (JsonException) { return new JukeboxFile(); }
        }

        public static string Serialize(JukeboxFile file) => JsonSerializer.Serialize(file, Json);

        public static JukeboxFile LoadLocal() => File.Exists(LocalFile) ? Parse(File.ReadAllText(LocalFile)) : new JukeboxFile();

        public static void SaveLocal(JukeboxFile file)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(LocalFile, Serialize(file));
        }

        /// <summary>The group list from GitHub (raw file, no API limit); falls back to the last copy we saw.</summary>
        public static async Task<JukeboxFile> FetchGroupAsync(CancellationToken ct = default)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                // The GitHub API gives the list as it is right now. The raw file (the fallback, when the API's hourly
                // limit is used up) can be several minutes old, which once made just-shared songs look unshared.
                string? text = null;
                try
                {
                    using var api = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Publisher.Repo + "/contents/" + RepoPath + "?ref=main");
                    api.Headers.TryAddWithoutValidation("Accept", "application/vnd.github.raw");
                    using HttpResponseMessage ar = await Http.SendAsync(api, cts.Token);
                    if (ar.StatusCode == System.Net.HttpStatusCode.NotFound) return new JukeboxFile();
                    if (ar.IsSuccessStatusCode) text = await ar.Content.ReadAsStringAsync(cts.Token);
                }
                catch (HttpRequestException) { }
                if (text == null)
                {
                    using HttpResponseMessage r = await Http.GetAsync(SharedUrl + "?t=" + DateTime.UtcNow.Ticks, cts.Token);
                    if (r.StatusCode == System.Net.HttpStatusCode.NotFound) return new JukeboxFile();
                    r.EnsureSuccessStatusCode();
                    text = await r.Content.ReadAsStringAsync(cts.Token);
                }
                Directory.CreateDirectory(Folder);
                File.WriteAllText(SharedCache, text);
                return Parse(text);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException)
            {
                return File.Exists(SharedCache) ? Parse(File.ReadAllText(SharedCache)) : new JukeboxFile();
            }
        }

        // ------------------------------------------------------------------ Suno links

        private static readonly Regex Uuid = new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase);

        /// <summary>
        /// Turns a Suno link (suno.com/song/&lt;id&gt;, a short suno.com/s/... link, or a bare id) into a song with its
        /// title and artist read from the page's preview tags. Throws with a friendly message if it isn't a song.
        /// </summary>
        public static async Task<JukeboxSong> ResolveAsync(string link, CancellationToken ct = default)
        {
            link = link.Trim();
            Match bare = Uuid.Match(link);
            var song = new JukeboxSong { Added = DateTime.UtcNow.ToString("o") };
            string? html = null;

            if (link.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(link, UriKind.Absolute, out Uri? uri) || !uri.Host.EndsWith("suno.com", StringComparison.OrdinalIgnoreCase)
                    && !uri.Host.EndsWith("suno.ai", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("That isn't a Suno link. Copy the song's Share link from Suno.");
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(15));
                    using var req = new HttpRequestMessage(HttpMethod.Get, uri);
                    using HttpResponseMessage r = await Http.SendAsync(req, cts.Token); // follows short-link redirects
                    html = await r.Content.ReadAsStringAsync(cts.Token);
                    Match fromUrl = Uuid.Match(r.RequestMessage?.RequestUri?.ToString() ?? "");
                    if (fromUrl.Success) bare = fromUrl;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException) { }
            }

            string? audio = html != null ? Meta(html, "og:audio") ?? Meta(html, "og:audio:url") : null;
            if (!bare.Success && audio != null) bare = Uuid.Match(audio);
            if (!bare.Success && html != null) bare = Uuid.Match(html);
            if (!bare.Success) throw new InvalidOperationException("Couldn't find a song in that link. Copy the song's Share link from Suno.");

            song.Id = bare.Value.ToLowerInvariant();
            song.Audio = audio;
            string? title = html != null ? Meta(html, "og:title") : null;
            if (!string.IsNullOrWhiteSpace(title))
            {
                title = System.Net.WebUtility.HtmlDecode(title).Replace(" | Suno", "").Trim();
                int by = title.LastIndexOf(" by ", StringComparison.OrdinalIgnoreCase);
                if (by > 0) { song.Artist = title.Substring(by + 4).Trim(); title = title.Substring(0, by).Trim(); }
                song.Title = title;
            }
            if (song.Title.Length == 0) song.Title = "Suno song " + song.Id.Substring(0, 8);
            return song;
        }

        private static string? Meta(string html, string property)
        {
            var m = Regex.Match(html, "<meta[^>]+(?:property|name)=\"" + Regex.Escape(property) + "\"[^>]*content=\"([^\"]*)\"", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(html, "<meta[^>]+content=\"([^\"]*)\"[^>]*(?:property|name)=\"" + Regex.Escape(property) + "\"", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        // ------------------------------------------------------------------ download + playlist

        public static async Task DownloadAsync(JukeboxSong song, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Folder);
            string dest = SongPath(song.Id), part = dest + ".part";
            var urls = new List<string>();
            // Shared songs live on GitHub (the jukebox release): download from there first, Suno only as a backup.
            bool onGitHub = song.Audio != null && song.Audio.Contains("github.com/", StringComparison.OrdinalIgnoreCase);
            if (onGitHub) urls.Add(song.Audio!);
            bool fromSuno = !song.Id.StartsWith("mp3-", StringComparison.Ordinal) && !onGitHub;
            if (!song.Id.StartsWith("mp3-", StringComparison.Ordinal)) urls.Add("https://cdn1.suno.ai/" + song.Id + ".mp3");
            if (!string.IsNullOrEmpty(song.Audio) && !urls.Contains(song.Audio)) urls.Add(song.Audio);
            if (fromSuno)
            try
            {
                // The song page names the current audio address; read it fresh in case the CDN moved.
                JukeboxSong fresh = await ResolveAsync("https://suno.com/song/" + song.Id, ct);
                if (!string.IsNullOrEmpty(fresh.Audio) && !urls.Contains(fresh.Audio)) urls.Add(fresh.Audio);
            }
            catch (InvalidOperationException) { }
            if (fromSuno) urls.Add("https://cdn2.suno.ai/" + song.Id + ".mp3");
            Exception? last = null;
            foreach (string url in urls)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.TryAddWithoutValidation("Referer", "https://suno.com/");
                    using HttpResponseMessage r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                    r.EnsureSuccessStatusCode();
                    await using (var outFile = File.Create(part))
                        await r.Content.CopyToAsync(outFile, ct);
                    if (new FileInfo(part).Length < 10_000) throw new IOException("the download was empty");
                    File.Move(part, dest, true);
                    return;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is IOException || ex is TaskCanceledException)
                {
                    last = ex;
                    try { if (File.Exists(part)) File.Delete(part); } catch (IOException) { }
                }
            }
            throw new IOException("Couldn't download \"" + song.Title + "\" from Suno (" + last?.Message + "). Is the song public?");
        }

        /// <summary>The group list + my list, without duplicates (group first).</summary>
        public static List<JukeboxSong> Merge(JukeboxFile group, JukeboxFile local)
        {
            var all = new List<JukeboxSong>();
            foreach (JukeboxSong s in group.Songs.Concat(local.Songs))
                if (s.Id.Length > 0 && !all.Any(o => o.Id == s.Id)) all.Add(s);
            return all;
        }

        /// <summary>Writes playlist.json in the format the mod reads (Unity JsonUtility: songs[] of id/title/artist/file).</summary>
        public static void WritePlaylist(IEnumerable<JukeboxSong> songs)
        {
            Directory.CreateDirectory(Folder);
            var list = songs.Where(s => IsDownloaded(s.Id))
                            .Select(s => new Dictionary<string, string> { ["id"] = s.Id, ["title"] = s.Title, ["artist"] = s.Artist, ["file"] = s.Id + ".mp3" })
                            .ToList();
            File.WriteAllText(PlaylistFile, JsonSerializer.Serialize(new Dictionary<string, object> { ["songs"] = list }, Json));
        }

        /// <summary>
        /// Fetches the group list, downloads every song this PC is missing and rewrites playlist.json.
        /// Reports progress lines; returns how many songs failed. Safe to call at launcher start (quiet on errors).
        /// </summary>
        public static async Task<int> SyncAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        {
            await SyncLock.WaitAsync(ct);
            try
            {
                if (!ExtrasEnabled)
                {
                    WritePlaylist(Array.Empty<JukeboxSong>());
                    progress?.Report("The jukebox comes with the extras. Enter the extras key or password in the EXTRAS box (bottom left) first.");
                    return 0;
                }
                JukeboxFile group = await FetchGroupAsync(ct);
                foreach (string gone in group.Removed) DeleteSongFile(gone); // someone pressed Remove: drop our copy too
                JukeboxFile mine = LoadLocal();
                if (mine.Songs.RemoveAll(s => group.Removed.Contains(s.Id)) > 0) SaveLocal(mine);
                List<JukeboxSong> all = Merge(group, mine).Where(s => !group.Removed.Contains(s.Id)).ToList();
                List<JukeboxSong> missing = all.Where(s => !IsDownloaded(s.Id)).ToList();
                int failed = 0;
                string firstError = "";
                for (int i = 0; i < missing.Count; i++)
                {
                    progress?.Report("Downloading " + (i + 1) + " of " + missing.Count + ": " + missing[i].Title);
                    try { await DownloadAsync(missing[i], ct); }
                    catch (IOException ex) { failed++; if (firstError.Length == 0) firstError = ex.Message; progress?.Report(ex.Message); }
                }
                WritePlaylist(all);
                progress?.Report(failed == 0 ? all.Count + " song(s) ready for the game."
                                             : all.Count - failed + " song(s) ready, " + failed + " couldn't download. " + firstError);
                return failed;
            }
            finally { SyncLock.Release(); }
        }

        /// <summary>MP3/WAV files dropped into the Jukebox folder by hand (named after the file, like the mod does).</summary>
        public static List<JukeboxSong> LooseFiles(IEnumerable<JukeboxSong> known)
        {
            var ids = new HashSet<string>(known.Select(k => k.Id), StringComparer.OrdinalIgnoreCase);
            var list = new List<JukeboxSong>();
            if (!Directory.Exists(Folder)) return list;
            foreach (string f in Directory.GetFiles(Folder).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".mp3" && ext != ".wav") continue;
                string id = Path.GetFileNameWithoutExtension(f);
                if (ids.Contains(id)) continue;
                list.Add(new JukeboxSong { Id = id, Title = id.Replace('_', ' '), FilePath = f });
            }
            return list;
        }

        public static void DeleteSongFile(string id)
        {
            try { if (File.Exists(SongPath(id))) File.Delete(SongPath(id)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }
    }
}
