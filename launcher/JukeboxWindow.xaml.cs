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
        public string Where => InGroup ? "GROUP" : "ONLY ME";
        public Visibility RemoveVisibility => CanRemove ? Visibility.Visible : Visibility.Collapsed;
        public bool CanPlay => JukeboxLibrary.IsDownloaded(Song.Id);
        public string Status => CanPlay ? "downloaded" : "not downloaded yet";

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

        public JukeboxWindow()
        {
            InitializeComponent();
            SongList.ItemsSource = _rows;
            _player.MediaEnded += (_, _) => StopPreview();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
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

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            if (_busy || (sender as FrameworkElement)?.DataContext is not JukeboxRow row) return;
            if (MessageBox.Show(this, "Remove \"" + row.Title + "\"" + (row.InGroup ? " for everyone?" : "?"), "Jukebox",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (_playingRow == row) StopPreview();
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
            if (wasThis || !JukeboxLibrary.IsDownloaded(row.Song.Id)) return;
            _player.Open(new Uri(JukeboxLibrary.SongPath(row.Song.Id)));
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
