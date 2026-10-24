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
        public bool CanShare { get; init; } // an "ONLY ME" Suno song + this PC has a creator key
        public Visibility ShareVisibility => CanShare && !InGroup && !Loose ? Visibility.Visible : Visibility.Collapsed;
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
            ShareBox.IsChecked = canShare;
            ShareBox.IsEnabled = canShare;
            if (!canShare) ShareText.Text = "Only for me (adding for the whole group needs a creator key: CREATOR button)";

            Status("Loading the song list...");
            _group = await JukeboxLibrary.FetchGroupAsync(_cts.Token);
            Rebuild();
            await SyncAsync();
        }

        private void Rebuild()
        {
            JukeboxFile local = JukeboxLibrary.LoadLocal();
            bool canShare = !string.IsNullOrEmpty(_creatorKey);
            _rows.Clear();
            foreach (JukeboxSong s in _group.Songs)
                _rows.Add(new JukeboxRow { Song = s, InGroup = true, CanRemove = canShare });
            foreach (JukeboxSong s in local.Songs.Where(l => !_group.Songs.Any(g => g.Id == l.Id)))
                _rows.Add(new JukeboxRow { Song = s, InGroup = false, CanRemove = true, CanShare = canShare });
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

                bool forGroup = ShareBox.IsChecked == true && !string.IsNullOrEmpty(_creatorKey);
                if (forGroup)
                {
                    Status("Adding \"" + song.Title + "\" for the group...");
                    var pub = new Publisher(_creatorKey!);
                    try { song.AddedBy = await pub.SignInAsync(_cts.Token); } catch (GitHubException) { }
                    await pub.UpdateTextFileAsync(JukeboxLibrary.RepoPath, current =>
                    {
                        JukeboxFile f = JukeboxLibrary.Parse(current);
                        if (!f.Songs.Any(s => s.Id == song.Id)) f.Songs.Add(song);
                        _group = f;
                        return JukeboxLibrary.Serialize(f);
                    }, "Jukebox: add " + song.Title, _cts.Token);
                }
                else
                {
                    JukeboxFile local = JukeboxLibrary.LoadLocal();
                    local.Songs.Add(song);
                    JukeboxLibrary.SaveLocal(local);
                }

                Status("Downloading \"" + song.Title + "\"...");
                await JukeboxLibrary.DownloadAsync(song, _cts.Token);
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

        /// <summary>"Share": moves one of my ONLY ME songs into the group list, so every friend's launcher downloads it.</summary>
        private async void Share_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || string.IsNullOrEmpty(_creatorKey) || (sender as FrameworkElement)?.DataContext is not JukeboxRow row) return;
            SetBusy(true);
            try
            {
                Status("Sharing \"" + row.Title + "\" with the group...");
                var pub = new Publisher(_creatorKey!);
                JukeboxSong song = row.Song;
                try { song.AddedBy = await pub.SignInAsync(_cts.Token); } catch (GitHubException) { }
                await pub.UpdateTextFileAsync(JukeboxLibrary.RepoPath, current =>
                {
                    JukeboxFile f = JukeboxLibrary.Parse(current);
                    if (!f.Songs.Any(s => s.Id == song.Id)) f.Songs.Add(song);
                    _group = f;
                    return JukeboxLibrary.Serialize(f);
                }, "Jukebox: add " + song.Title, _cts.Token);
                JukeboxFile local = JukeboxLibrary.LoadLocal();
                local.Songs.RemoveAll(s => s.Id == song.Id);
                JukeboxLibrary.SaveLocal(local);
                Rebuild();
                Status("Shared \"" + row.Title + "\". Friends get it next time they open the launcher.");
            }
            catch (GitHubException ex) { Status(ex.Message); }
            catch (System.Net.Http.HttpRequestException ex) { Status("No connection to GitHub: " + ex.Message); }
            catch (IOException ex) { Status(ex.Message); }
            catch (OperationCanceledException) { }
            finally { SetBusy(false); }
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || (sender as FrameworkElement)?.DataContext is not JukeboxRow row) return;
            if (MessageBox.Show(this, "Remove \"" + row.Title + "\"" + (row.InGroup ? " for everyone?" : "?"), "Jukebox",
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
            AddButton.IsEnabled = !busy;
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
