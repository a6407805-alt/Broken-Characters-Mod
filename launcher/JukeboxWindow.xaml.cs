// ============================================================================================================
//  The JUKEBOX window: paste a Suno link to add a song, see every song and whether it's downloaded, listen to
//  one, remove one. The work lives in JukeboxLibrary.cs; adding to the group list uses the creator key
//  (Publisher.UpdateTextFileAsync on launcher/jukebox.json).
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    public sealed class JukeboxRow : INotifyPropertyChanged
    {
        public JukeboxSong Song { get; init; } = new JukeboxSong();
        public bool InGroup { get; init; }
        public bool CanRemove { get; init; }
        public string Title => Song.Title;
        public string Artist => Song.Artist;
        public bool Loose => Song.FilePath != null;
        public string Where => Loose ? "IN FOLDER" : InGroup ? "GROUP" : "ONLY ME";
        public string FilePath => Song.FilePath ?? JukeboxLibrary.SongPath(Song.Id);
        public Visibility RemoveVisibility => CanRemove ? Visibility.Visible : Visibility.Collapsed;
        public bool CanPlay => File.Exists(FilePath);
        public string Status => Loose ? "ready" : CanPlay ? "downloaded" : "not downloaded yet";

        private bool _playing;
        public bool Playing { get => _playing; set { _playing = value; Changed(nameof(PlayText)); } }
        public string PlayText => _playing ? "■" : "▶";

        public void Refresh() { Changed(nameof(Status)); Changed(nameof(CanPlay)); }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class JukeboxWindow : Window
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ObservableCollection<JukeboxRow> _rows = new ObservableCollection<JukeboxRow>();
        private readonly MediaPlayer _player = new MediaPlayer();
        private JukeboxRow? _playingRow;
        private JukeboxFile _group = new JukeboxFile();
        private string? _creatorKey;
        private bool _busy;

        private readonly string? _screenshotPath;

        public JukeboxWindow(string? screenshotPath = null)
        {
            InitializeComponent();
            _screenshotPath = screenshotPath;
            SongList.ItemsSource = _rows;
            _player.MediaEnded += (_, _) => StopPreview();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_screenshotPath != null) { await ShowDemoAsync(); return; }
            _creatorKey = Publisher.ReadSavedKey();
            bool canShare = !string.IsNullOrEmpty(_creatorKey);
            ShareText.Text = !JukeboxLibrary.ExtrasEnabled
                ? "The jukebox comes with the extras: enter the extras key or password in the EXTRAS box (bottom left of the launcher) first."
                : canShare
                ? "Every song you add goes on everyone's jukebox (it's uploaded to GitHub, then every PC with the extras downloads it)."
                : "You get everyone's songs automatically. To ADD songs, sign in once in CREATOR (then reopen this).";
            AddButton.IsEnabled = canShare;

            Status("Loading the song list...");
            _group = await JukeboxLibrary.FetchGroupAsync(_cts.Token);
            Rebuild();
            await ShareMineAsync();
            await SyncAsync();
        }

        private void Rebuild()
        {
            JukeboxFile local = JukeboxLibrary.LoadLocal();
            bool canShare = !string.IsNullOrEmpty(_creatorKey);
            _rows.Clear();
            foreach (JukeboxSong s in _group.Songs)
                _rows.Add(new JukeboxRow { Song = s, InGroup = true, CanRemove = canShare });
            foreach (JukeboxSong s in local.Songs.Where(l => !_group.Songs.Any(g => g.Id == l.Id) && !_group.Removed.Contains(l.Id)))
                _rows.Add(new JukeboxRow { Song = s, InGroup = false, CanRemove = true });
            foreach (JukeboxSong s in JukeboxLibrary.LooseFiles(_rows.Select(r => r.Song).ToList()))
                _rows.Add(new JukeboxRow { Song = s, InGroup = false, CanRemove = true });
            EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task SyncAsync()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var progress = new Progress<string>(line => { Status(line); foreach (var r in _rows) r.Refresh(); });
                await JukeboxLibrary.SyncAsync(progress, _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Status("Couldn't save the songs: " + ex.Message); }
            finally
            {
                foreach (var r in _rows) r.Refresh();
                SetBusy(false);
            }
        }

        // ================================================================== add / remove

        private void LinkBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Add_Click(sender, e);
        }

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            string link = LinkBox.Text.Trim();
            if (link.Length == 0 || _busy) return;
            SetBusy(true);
            try
            {
                Status("Looking up the song on Suno...");
                JukeboxSong song = await JukeboxLibrary.ResolveAsync(link, _cts.Token);
                if (_rows.Any(r => r.Song.Id == song.Id)) { Status("\"" + song.Title + "\" is already in the jukebox."); return; }

                bool forGroup = !string.IsNullOrEmpty(_creatorKey); // songs are always for everyone
                if (!forGroup) { Status("To add songs, sign in once in CREATOR, then reopen the jukebox."); return; }

                // 1. get the mp3 from Suno once, 2. put it on GitHub, 3. list it: friends then download from GitHub.
                Status("Downloading \"" + song.Title + "\" from Suno...");
                await JukeboxLibrary.DownloadAsync(song, _cts.Token);
                var pub = new Publisher(_creatorKey!);
                try { song.AddedBy = await pub.SignInAsync(_cts.Token); } catch (GitHubException) { }
                Status("Uploading \"" + song.Title + "\" to GitHub for everyone...");
                song.Audio = await pub.UploadReleaseFileAsync("jukebox", song.Id + ".mp3", JukeboxLibrary.SongPath(song.Id), "audio/mpeg", _cts.Token);
                await pub.UpdateTextFileAsync(JukeboxLibrary.RepoPath, current =>
                {
                    JukeboxFile f = JukeboxLibrary.Parse(current);
                    f.Removed.Remove(song.Id);
                    if (!f.Songs.Any(s => s.Id == song.Id)) f.Songs.Add(song);
                    _group = f;
                    return JukeboxLibrary.Serialize(f);
                }, "Jukebox: add " + song.Title, _cts.Token);

                LinkBox.Clear();
                Rebuild();
                JukeboxLibrary.WritePlaylist(JukeboxLibrary.Merge(_group, JukeboxLibrary.LoadLocal()));
                Status("Added \"" + song.Title + "\"" + (forGroup ? " for everyone. Friends get it next time they open the launcher." : "."));
            }
            catch (InvalidOperationException ex) { Status(ex.Message); }
            catch (GitHubException ex) { Status(ex.Message); }
            catch (System.Net.Http.HttpRequestException ex) { Status("No connection to GitHub: " + ex.Message); }
            catch (IOException ex) { Rebuild(); Status(ex.Message); }
            catch (OperationCanceledException) { }
            finally { SetBusy(false); }
        }

        /// <summary>
        /// No "only me" songs: anything this PC has that the group doesn't (old only-me Suno songs, MP3s dropped in the
        /// folder) gets shared. MP3s are uploaded to the repo's "jukebox" release; the list just points at them.
        /// Needs a creator key; without one the songs stay on this PC and the status line says why.
        /// </summary>
        private async Task ShareMineAsync()
        {
            JukeboxFile local = JukeboxLibrary.LoadLocal();
            List<JukeboxSong> localOnly = local.Songs.Where(l => !_group.Songs.Any(g => g.Id == l.Id) && !_group.Removed.Contains(l.Id)).ToList();
            List<JukeboxSong> loose = JukeboxLibrary.LooseFiles(_group.Songs.Concat(local.Songs).ToList())
                .Where(f => f.FilePath!.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)).ToList();
            if (localOnly.Count + loose.Count == 0 || _busy) return;
            int shareable = localOnly.Count + loose.Count(f => !_group.Songs.Any(g => g.Id == JukeboxLibrary.FileSongId(f.Id)) && !_group.Removed.Contains(JukeboxLibrary.FileSongId(f.Id)));
            if (string.IsNullOrEmpty(_creatorKey))
            {
                if (shareable == 0) return;
                Status(shareable + " song(s) here aren't shared yet. Sign in once in CREATOR to share them with everyone.");
                return;
            }

            SetBusy(true);
            try
            {
                var pub = new Publisher(_creatorKey!);
                string who = "";
                try { who = await pub.SignInAsync(_cts.Token); } catch (GitHubException) { }
                var add = new List<JukeboxSong>();
                foreach (JukeboxSong s in localOnly) { s.AddedBy ??= who; add.Add(s); }
                foreach (JukeboxSong f in loose)
                {
                    string id = JukeboxLibrary.FileSongId(f.Id);
                    string target = JukeboxLibrary.SongPath(id);
                    if (_group.Removed.Contains(id)) { File.Delete(f.FilePath!); continue; } // removed for everyone: don't bring it back
                    if (_group.Songs.Any(g => g.Id == id) || add.Any(a => a.Id == id))
                    {
                        // Already shared (a renamed copy of a group song): just tidy the file, don't upload it again.
                        if (!File.Exists(target)) File.Move(f.FilePath!, target);
                        else if (!string.Equals(f.FilePath, target, StringComparison.OrdinalIgnoreCase)) File.Delete(f.FilePath!);
                        continue;
                    }
                    Status("Uploading \"" + f.Title + "\" for everyone...");
                    string url = await pub.UploadReleaseFileAsync("jukebox", id + ".mp3", f.FilePath!, "audio/mpeg", _cts.Token);
                    string dest = JukeboxLibrary.SongPath(id);
                    if (!string.Equals(f.FilePath, dest, StringComparison.OrdinalIgnoreCase)) File.Move(f.FilePath!, dest, true);
                    add.Add(new JukeboxSong { Id = id, Title = f.Title, Artist = who, Audio = url, AddedBy = who, Added = DateTime.UtcNow.ToString("o") });
                }
                if (add.Count > 0)
                await pub.UpdateTextFileAsync(JukeboxLibrary.RepoPath, current =>
                {
                    JukeboxFile g = JukeboxLibrary.Parse(current);
                    foreach (JukeboxSong s in add) if (!g.Songs.Any(o => o.Id == s.Id)) g.Songs.Add(s);
                    _group = g;
                    return JukeboxLibrary.Serialize(g);
                }, "Jukebox: share " + add.Count + " song(s)", _cts.Token);
                local.Songs.RemoveAll(s => add.Any(a => a.Id == s.Id));
                JukeboxLibrary.SaveLocal(local);
                if (add.Count > 0) Status("Shared " + add.Count + " song(s) with everyone.");
            }
            catch (GitHubException ex) { Status("Couldn't share: " + ex.Message); }
            catch (System.Net.Http.HttpRequestException ex) { Status("No connection to GitHub: " + ex.Message); }
            catch (IOException ex) { Status(ex.Message); }
            catch (OperationCanceledException) { }
            finally { SetBusy(false); Rebuild(); }
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || (sender as FrameworkElement)?.DataContext is not JukeboxRow row) return;
            if (MessageBox.Show(this, "Remove \"" + row.Title + "\" from everyone's jukebox?", "Jukebox",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (_playingRow == row) StopPreview();
            if (row.Loose)
            {
                try { File.Delete(row.FilePath); Status("Removed \"" + row.Title + "\"."); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { Status("Couldn't remove it: " + ex.Message); }
                Rebuild();
                return;
            }
            SetBusy(true);
            try
            {
                if (row.InGroup)
                {
                    var pub = new Publisher(_creatorKey!);
                    await pub.UpdateTextFileAsync(JukeboxLibrary.RepoPath, current =>
                    {
                        JukeboxFile f = JukeboxLibrary.Parse(current);
                        f.Songs.RemoveAll(s => s.Id == row.Song.Id);
                        if (!f.Removed.Contains(row.Song.Id)) f.Removed.Add(row.Song.Id); // every PC deletes its copy
                        _group = f;
                        return JukeboxLibrary.Serialize(f);
                    }, "Jukebox: remove " + row.Title, _cts.Token);
                }
                JukeboxFile local = JukeboxLibrary.LoadLocal();
                local.Songs.RemoveAll(s => s.Id == row.Song.Id);
                JukeboxLibrary.SaveLocal(local);
                JukeboxLibrary.DeleteSongFile(row.Song.Id);
                Rebuild();
                JukeboxLibrary.WritePlaylist(JukeboxLibrary.Merge(_group, local));
                Status("Removed \"" + row.Title + "\".");
            }
            catch (GitHubException ex) { Status(ex.Message); }
            catch (System.Net.Http.HttpRequestException ex) { Status("No connection to GitHub: " + ex.Message); }
            catch (IOException ex) { Status(ex.Message); }
            catch (OperationCanceledException) { }
            finally { SetBusy(false); }
        }

        // ================================================================== listen

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not JukeboxRow row) return;
            bool wasThis = _playingRow == row;
            StopPreview();
            if (wasThis || !row.CanPlay) return;
            _player.Open(new Uri(row.FilePath));
            _player.Volume = 0.6;
            _player.Play();
            _playingRow = row;
            row.Playing = true;
        }

        private void StopPreview()
        {
            _player.Stop();
            _player.Close();
            if (_playingRow != null) _playingRow.Playing = false;
            _playingRow = null;
        }

        // ================================================================== build-server screenshot

        /// <summary>"--screenshot-jukebox &lt;png&gt;": made-up songs, no network, saves a picture and closes.</summary>
        private async Task ShowDemoAsync()
        {
            ShareBox.IsChecked = true;
            LinkBox.Text = "https://suno.com/s/aB3xYz9QkLmN";
            var demo = new[]
            {
                ("Neon Rain", "Jay", true), ("Last Teleporter", "Jay", true), ("Providence Was Right", "Jay", true),
                ("Lunar Coins (Remix)", "Jay", false), ("Monsoon Mode", "Vincent", true),
            };
            foreach (var (title, artist, group) in demo)
                _rows.Add(new JukeboxRow { Song = new JukeboxSong { Id = Guid.NewGuid().ToString(), Title = title, Artist = artist }, InGroup = group, CanRemove = true });
            _rows[0].Playing = true;
            Status("Added \"Monsoon Mode\" for everyone. Friends get it next time they open the launcher.");
            await Task.Delay(1500);
            try
            {
                if (Content is FrameworkElement root)
                {
                    root.UpdateLayout();
                    var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        (int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(root);
                    var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
                    using FileStream fs = File.Create(_screenshotPath!);
                    png.Save(fs);
                }
            }
            finally { Application.Current.Shutdown(); }
        }

        // ================================================================== buttons

        private async void Sync_Click(object sender, RoutedEventArgs e)
        {
            _group = await JukeboxLibrary.FetchGroupAsync(_cts.Token);
            Rebuild();
            await ShareMineAsync();
            await SyncAsync();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(JukeboxLibrary.Folder);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + JukeboxLibrary.Folder + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is IOException || ex is Win32Exception) { Status(ex.Message); }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            AddButton.IsEnabled = !busy && !string.IsNullOrEmpty(_creatorKey);
            SyncButton.IsEnabled = !busy;
        }

        private void Status(string text) => StatusLine.Text = text;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            StopPreview();
            _cts.Cancel();
        }
    }
}
