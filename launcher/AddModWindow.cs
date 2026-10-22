// ============================================================================================================
//  Broken Characters Launcher - ADD MY MOD window
//
//  Lets a friend send their mod files to the owner without knowing git:
//    1. type the mod's name
//    2. pick files (or a whole folder)
//    3. press SEND
//
//  Two ways SEND works:
//    EASY MODE (default, no setup): copies the files into a tidy folder on the Desktop (mods/<Mod>/..., src/<Mod>/...),
//      opens that folder and GitHub's own upload page. The friend drags the folders into the page and presses Commit.
//    ONE-CLICK MODE (optional): if a GitHub token was saved once, SEND uploads everything straight to a new branch
//      "submit/<Mod>-<time>" through the GitHub API and opens a pull request. Nothing reaches players until the
//      owner presses "Squash and merge".
//
//  Where files go (same rules as CONTRIBUTING.md):
//    .cs and .csproj  ->  src/<Mod>/...      (the build server compiles every src/*/*.csproj)
//    everything else  ->  mods/<Mod>/...     (icons, sounds, asset bundles, a prebuilt dll)
//
//  The token is kept in %APPDATA%\BrokenCharacters\github-token.txt, next to the extras key. It is never logged,
//  never sent anywhere except api.github.com, and never put into the launcher itself.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace BrokenCharactersLauncher
{
    public sealed class AddModWindow : Window
    {
        // ------------------------------------------------------------------ settings

        private const string Repo = "a6407805-alt/Broken-Characters-Mod";
        private const string BaseBranch = "main";

        /// <summary>GitHub's blob API takes up to 100 MB; stay well under it.</summary>
        private const long MaxFileBytes = 90L * 1024 * 1024;

        /// <summary>Classic token page with the one scope we need (public_repo) already ticked.</summary>
        private const string TokenPageUrl = "https://github.com/settings/tokens/new?scopes=public_repo&description=Broken%20Characters%20launcher";

        private static readonly string TokenDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BrokenCharacters");
        private static readonly string TokenFile = Path.Combine(TokenDir, "github-token.txt");

        private static readonly HttpClient Api = CreateApiClient();

        private static HttpClient CreateApiClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BrokenCharactersLauncher", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            return client;
        }

        // ------------------------------------------------------------------ colours (same palette as the launcher)

        private static Brush Solid(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private static readonly Brush PanelBg = Solid(0x10, 0x14, 0x20);
        private static readonly Brush PanelLight = Solid(0x1E, 0x25, 0x38);
        private static readonly Brush Accent = Solid(0xFF, 0xC9, 0x28);
        private static readonly Brush Dim = Solid(0x9F, 0xB3, 0xD1);
        private static readonly Brush TextWhite = Solid(0xE6, 0xEA, 0xF2);
        private static readonly Brush DarkText = Solid(0x1A, 0x12, 0x05);
        private static readonly Brush Good = Solid(0x6B, 0xE0, 0x8A);
        private static readonly Brush Bad = Solid(0xFF, 0x7A, 0x7A);

        // ------------------------------------------------------------------ state

        /// <summary>Picked files: path inside the mod folder (forward slashes) -> file on this PC.</summary>
        private readonly SortedDictionary<string, string> _files = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private int _skipped;
        private bool _busy;

        private readonly TextBox _name;
        private readonly TextBlock _fileList;
        private readonly Button _send;
        private readonly TextBlock _status;
        private readonly TextBlock _modeText;
        private readonly StackPanel _setupPanel;
        private readonly PasswordBox _tokenBox;

        // ------------------------------------------------------------------ window

        public AddModWindow()
        {
            Title = "Add my mod";
            Width = 600;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = PanelBg;
            FontFamily = new FontFamily("Segoe UI");

            var root = new StackPanel { Margin = new Thickness(24) };

            root.Children.Add(MakeText("ADD MY MOD", 22, Accent, FontWeights.Black, new Thickness(0)));
            root.Children.Add(MakeText("Send your mod to the owner in 3 steps. Nothing is downloaded or installed.",
                13, Dim, FontWeights.Normal, new Thickness(0, 4, 0, 16)));

            // Step 1: name
            root.Children.Add(MakeText("1.  Name of your mod (letters and numbers, no spaces)", 13, TextWhite, FontWeights.SemiBold, new Thickness(0, 0, 0, 4)));
            _name = new TextBox { FontSize = 14, Padding = new Thickness(8, 6, 8, 6), MaxLength = 40 };
            _name.TextChanged += (s, e) => RefreshList();
            root.Children.Add(_name);

            // Step 2: files
            root.Children.Add(MakeText("2.  Pick your files", 13, TextWhite, FontWeights.SemiBold, new Thickness(0, 16, 0, 6)));
            var pickRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button pickFiles = MakeButton("CHOOSE FILES", false);
            pickFiles.Click += (s, e) => ChooseFiles();
            Button pickFolder = MakeButton("CHOOSE A FOLDER", false);
            pickFolder.Click += (s, e) => ChooseFolder();
            Button clear = MakeButton("CLEAR", false);
            clear.Click += (s, e) => { _files.Clear(); _skipped = 0; RefreshList(); };
            pickRow.Children.Add(pickFiles);
            pickRow.Children.Add(pickFolder);
            pickRow.Children.Add(clear);
            root.Children.Add(pickRow);

            _fileList = MakeText("No files picked yet.", 12, Dim, FontWeights.Normal, new Thickness(0));
            root.Children.Add(new Border
            {
                Background = PanelLight,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new ScrollViewer
                {
                    MaxHeight = 130,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = _fileList
                }
            });

            // Step 3: send
            root.Children.Add(MakeText("3.  Send it", 13, TextWhite, FontWeights.SemiBold, new Thickness(0, 16, 0, 6)));
            _send = MakeButton("SEND MY MOD", true);
            _send.Click += Send_Click;
            root.Children.Add(_send);

            _status = MakeText("", 13, TextWhite, FontWeights.Normal, new Thickness(0, 12, 0, 0));
            root.Children.Add(_status);

            // Mode + optional one-click setup
            _modeText = MakeText("", 12, Dim, FontWeights.Normal, new Thickness(0, 16, 0, 6));
            root.Children.Add(_modeText);

            Button setupToggle = MakeButton("ONE-CLICK SETUP (OPTIONAL)", false);
            root.Children.Add(setupToggle);

            _setupPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 0) };
            _setupPanel.Children.Add(MakeText(
                "Do this once:\n" +
                "1. Ask the owner to add you to the GitHub repo.\n" +
                "2. Press OPEN TOKEN PAGE, scroll down and press Generate token.\n" +
                "3. Copy the token (it starts with ghp_), paste it below and press SAVE.",
                12, Dim, FontWeights.Normal, new Thickness(0, 0, 0, 8)));
            var tokenRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button openToken = MakeButton("OPEN TOKEN PAGE", false);
            openToken.Click += (s, e) => OpenWithShell(TokenPageUrl);
            tokenRow.Children.Add(openToken);
            _setupPanel.Children.Add(tokenRow);
            _tokenBox = new PasswordBox { FontSize = 13, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 8, 0, 8) };
            _setupPanel.Children.Add(_tokenBox);
            Button saveToken = MakeButton("SAVE TOKEN", true);
            saveToken.Click += (s, e) => SaveToken();
            _setupPanel.Children.Add(saveToken);
            root.Children.Add(_setupPanel);

            setupToggle.Click += (s, e) =>
            {
                _setupPanel.Visibility = _setupPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            };

            Content = root;

            UpdateMode();
            RefreshList();
        }

        // ------------------------------------------------------------------ small UI helpers

        private static TextBlock MakeText(string text, double size, Brush color, FontWeight weight, Thickness margin)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = color,
                FontWeight = weight,
                Margin = margin,
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static Button MakeButton(string text, bool primary)
        {
            return new Button
            {
                Content = text,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = primary ? DarkText : Accent,
                Background = primary ? Accent : PanelLight,
                BorderBrush = Accent,
                BorderThickness = new Thickness(1)
            };
        }

        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.Foreground = error ? Bad : Good;
        }

        private void UpdateMode()
        {
            _modeText.Text = ReadToken() != null
                ? "One-click send is ON: SEND uploads straight to GitHub and the owner approves it there."
                : "Easy mode: SEND opens a folder and the GitHub upload page, and you drag the files in.";
        }

        private void UpdateSendEnabled()
        {
            _send.IsEnabled = !_busy && CleanName(_name.Text).Length >= 2 && _files.Count > 0;
        }

        private static void OpenWithShell(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Debug.WriteLine("Could not open " + target + ": " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ picking files

        private static string CleanName(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c < 128 && char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Where a picked file lands in the repo: source code -> src/, everything else -> mods/.</summary>
        private static string RepoPathFor(string mod, string relative)
        {
            string rel = relative.Replace('\\', '/').TrimStart('/');
            string ext = Path.GetExtension(rel).ToLowerInvariant();
            string top = (ext == ".cs" || ext == ".csproj") ? "src/" : "mods/";
            return top + mod + "/" + rel;
        }

        private void AddFile(string source, string relative)
        {
            string rel = relative.Replace('\\', '/');

            // Build output, hidden folders (.git, .vs) and hidden files never belong in the repo.
            foreach (string part in rel.Split('/'))
            {
                if (part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)
                    || part.StartsWith(".", StringComparison.Ordinal))
                {
                    _skipped++;
                    return;
                }
            }

            long size;
            try { size = new FileInfo(source).Length; }
            catch (IOException) { _skipped++; return; }
            if (size > MaxFileBytes) { _skipped++; return; }

            _files[rel] = source;
        }

        private void ChooseFiles()
        {
            var dialog = new OpenFileDialog { Multiselect = true, CheckFileExists = true, Title = "Pick your mod files" };
            if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames) AddFile(path, Path.GetFileName(path));
            RefreshList();
        }

        private void ChooseFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Pick your mod folder" };
            if (dialog.ShowDialog(this) != true) return;
            string root = dialog.FolderName;
            try
            {
                foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    AddFile(path, Path.GetRelativePath(root, path));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                SetStatus("Could not read that folder: " + ex.Message, true);
            }
            RefreshList();
        }

        private void RefreshList()
        {
            string mod = CleanName(_name.Text);
            if (mod.Length == 0) mod = "YourMod";

            if (_files.Count == 0)
            {
                _fileList.Text = _skipped > 0
                    ? "No files picked (" + _skipped + " skipped: build folders, hidden or huge files)."
                    : "No files picked yet.";
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append(_files.Count).Append(" file(s) ready");
                if (_skipped > 0) sb.Append("   (").Append(_skipped).Append(" skipped: build folders, hidden or huge files)");
                sb.AppendLine();
                int shown = 0;
                foreach (string rel in _files.Keys)
                {
                    if (shown >= 30)
                    {
                        sb.Append("... and ").Append(_files.Count - 30).Append(" more");
                        break;
                    }
                    sb.AppendLine(RepoPathFor(mod, rel));
                    shown++;
                }
                _fileList.Text = sb.ToString().TrimEnd();
            }
            UpdateSendEnabled();
        }

        // ------------------------------------------------------------------ the token

        private static string? ReadToken()
        {
            try
            {
                if (!File.Exists(TokenFile)) return null;
                string token = File.ReadAllText(TokenFile).Trim();
                return token.Length > 10 ? token : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private void SaveToken()
        {
            string token = _tokenBox.Password.Trim();
            if (!token.StartsWith("ghp_", StringComparison.Ordinal) && !token.StartsWith("github_pat_", StringComparison.Ordinal))
            {
                SetStatus("That doesn't look like a GitHub token. It starts with ghp_", true);
                return;
            }
            try
            {
                Directory.CreateDirectory(TokenDir);
                File.WriteAllText(TokenFile, token);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                SetStatus("Could not save the token: " + ex.Message, true);
                return;
            }
            _tokenBox.Clear();
            _setupPanel.Visibility = Visibility.Collapsed;
            UpdateMode();
            SetStatus("Saved. SEND now uploads for you.", false);
        }

        // ------------------------------------------------------------------ SEND

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;

            string mod = CleanName(_name.Text);
            if (mod.Length < 2)
            {
                SetStatus("Type a name for your mod first (letters and numbers only).", true);
                return;
            }
            if (_files.Count == 0)
            {
                SetStatus("Pick at least one file first.", true);
                return;
            }

            _busy = true;
            UpdateSendEnabled();
            try
            {
                string? token = ReadToken();
                if (token != null) await SendOneClickAsync(mod, token);
                else StageForWebUpload(mod);
            }
            catch (Exception ex)
            {
                SetStatus("Could not send: " + ex.Message, true);
            }
            finally
            {
                _busy = false;
                UpdateSendEnabled();
            }
        }

        // ---------------------------------------------- easy mode: Desktop folder + GitHub's own upload page

        private void StageForWebUpload(string mod)
        {
            string stage = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Send to Broken Characters",
                mod + " " + DateTime.Now.ToString("yyyyMMdd-HHmm"));

            foreach (KeyValuePair<string, string> file in _files)
            {
                string dest = Path.Combine(stage, RepoPathFor(mod, file.Key).Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file.Value, dest, true);
            }

            OpenWithShell(stage);
            OpenWithShell("https://github.com/" + Repo + "/upload/" + BaseBranch);

            SetStatus(
                "Done! A folder and a GitHub page just opened.\n" +
                "1. Drag the folders from the folder window into the GitHub page.\n" +
                "2. Scroll down, pick \"Create a new branch\", press Commit changes.\n" +
                "3. Tell the owner. They approve it.", false);
        }

        // ---------------------------------------------- one-click mode: GitHub API -> branch -> pull request

        private async Task SendOneClickAsync(string mod, string token)
        {
            string api = "https://api.github.com/repos/" + Repo;

            SetStatus("Connecting to GitHub...", false);
            JsonElement head = await CallAsync(HttpMethod.Get, api + "/git/ref/heads/" + BaseBranch, token, null);
            string headSha = head.GetProperty("object").GetProperty("sha").GetString()
                ?? throw new InvalidOperationException("GitHub did not return the latest version.");
            JsonElement headCommit = await CallAsync(HttpMethod.Get, api + "/git/commits/" + headSha, token, null);
            string baseTree = headCommit.GetProperty("tree").GetProperty("sha").GetString()
                ?? throw new InvalidOperationException("GitHub did not return the file list.");

            var entries = new List<object>();
            int done = 0;
            foreach (KeyValuePair<string, string> file in _files)
            {
                done++;
                SetStatus("Uploading " + done + " of " + _files.Count + ": " + Path.GetFileName(file.Value), false);
                string source = file.Value;
                string base64 = await Task.Run(() => Convert.ToBase64String(File.ReadAllBytes(source)));
                JsonElement blob = await CallAsync(HttpMethod.Post, api + "/git/blobs", token,
                    new { content = base64, encoding = "base64" });
                entries.Add(new
                {
                    path = RepoPathFor(mod, file.Key),
                    mode = "100644",
                    type = "blob",
                    sha = blob.GetProperty("sha").GetString()
                });
            }

            SetStatus("Saving to GitHub...", false);
            JsonElement tree = await CallAsync(HttpMethod.Post, api + "/git/trees", token,
                new { base_tree = baseTree, tree = entries });
            string treeSha = tree.GetProperty("sha").GetString()
                ?? throw new InvalidOperationException("GitHub did not save the files.");

            JsonElement commit = await CallAsync(HttpMethod.Post, api + "/git/commits", token,
                new { message = "Add " + mod + " (sent from the launcher)", tree = treeSha, parents = new[] { headSha } });
            string commitSha = commit.GetProperty("sha").GetString()
                ?? throw new InvalidOperationException("GitHub did not save the commit.");

            string branch = "submit/" + mod + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            await CallAsync(HttpMethod.Post, api + "/git/refs", token,
                new { @ref = "refs/heads/" + branch, sha = commitSha });

            SetStatus("Asking the owner to approve it...", false);
            await CallAsync(HttpMethod.Post, api + "/pulls", token, new
            {
                title = "Add " + mod,
                head = branch,
                @base = BaseBranch,
                body = "Sent from the launcher's ADD MY MOD button. " + _files.Count + " file(s): src/ for code, mods/ for everything else."
            });

            SetStatus("Sent! The owner just has to approve it on GitHub.", false);
        }

        private static async Task<JsonElement> CallAsync(HttpMethod method, string url, string token, object? body)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await Api.SendAsync(request);
            string text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(Explain(response.StatusCode, text));
            }

            using JsonDocument doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }

        private static string Explain(HttpStatusCode code, string body)
        {
            string detail = "";
            try
            {
                using JsonDocument doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("message", out JsonElement message)) detail = message.GetString() ?? "";
            }
            catch (JsonException)
            {
                // not JSON: just show the status
            }

            switch ((int)code)
            {
                case 401:
                    return "GitHub did not accept your token. Make a new one and save it again.";
                case 403:
                case 404:
                    return "GitHub said no. Ask the owner to add you to the repo, and check the token has public_repo. " + detail;
                default:
                    return "GitHub error " + (int)code + ". " + detail;
            }
        }
    }
}
