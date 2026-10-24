// ============================================================================================================
//  Creator window: the QUICK UPLOAD box (pick or drag a friend's .zip/.dll, name the mod, check, publish).
//  The staging work is in QuickUpload.cs; after "Check files" the normal list + gold button take over.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace BrokenCharactersLauncher
{
    public partial class PublishWindow
    {
        private List<string> _quickPicked = new List<string>();
        private bool _quickMode;
        private List<string> _quickKnown = new List<string>(); // mods this person may update (for whole-plugins-folder uploads)

        private async void PickFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null || _busy) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick the mod (.zip, .dll and its files) - a whole BepInEx/plugins zip works too",
                Multiselect = true,
                Filter = "Mod files (*.zip;*.dll)|*.zip;*.dll|All files (*.*)|*.*"
            };
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads)) dialog.InitialDirectory = downloads;
            if (dialog.ShowDialog(this) != true) return;
            await SetQuickPicksAsync(dialog.FileNames);
        }

        private void Quick_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = _pub != null && !_busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private async void Quick_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await SetQuickPicksAsync(paths);
        }

        private async Task SetQuickPicksAsync(string[] paths)
        {
            if (_pub == null || _busy) return;
            _quickPicked = paths.ToList();
            QuickText.Text = "Picked: " + string.Join(", ", paths.Select(p => Path.GetFileName(p.TrimEnd('\\', '/'))));
            QuickNameRow.Visibility = Visibility.Visible;

            string guess = QuickUpload.GuessName(_quickPicked);
            try
            {
                List<string> mods = await _pub.PublishableModsAsync(_cts.Token);
                _quickKnown = mods;
                QuickName.ItemsSource = mods;
                guess = mods.FirstOrDefault(m => m.Equals(guess, StringComparison.OrdinalIgnoreCase)) ?? guess;
            }
            catch (Exception ex) when (ex is GitHubException || IsNetworkError(ex)) { } // the list is just a helper
            QuickName.Text = guess;
            StatusLine.Text = "Check the mod name (pick from the list to update one, or type a new name for a new mod), then press \"Check files\".";
        }

        private async void QuickCheck_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null || _busy || _quickPicked.Count == 0) return;
            string name = (QuickName.Text ?? "").Trim();
            if (_quickKnown.Count == 0)
            {
                try { _quickKnown = await _pub.PublishableModsAsync(_cts.Token); }
                catch (Exception ex) when (ex is GitHubException || IsNetworkError(ex)) { }
            }

            QuickUpload.Result staged;
            try { staged = await Task.Run(() => QuickUpload.Stage(_quickPicked, name, _quickKnown)); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                StatusLine.Text = ex is InvalidDataException ? "That zip looks broken. Download it again." : ex.Message;
                return;
            }
            _folder = staged.Folder;
            _quickMode = true;
            FolderText.Text = "Quick upload: " + (staged.Multi ? staged.Included.Count + " mods" : name);
            await RefreshAsync();
            if (staged.Skipped.Count > 0)
                StatusLine.Text += "  (Skipped " + staged.Skipped.Count + " that aren't yours or aren't in the pack: " + QuickUpload.Short(staged.Skipped) + ")";
        }
    }
}
