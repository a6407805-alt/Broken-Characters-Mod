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

        private async void PickFiles_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null || _busy) return;
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pick the mod (the .zip, or the .dll and its files)",
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
            if (!QuickUpload.IsValidName(name))
            {
                StatusLine.Text = "Mod names are letters, numbers, - _ . only, no spaces (like SpecialForces).";
                return;
            }

            try { _folder = await Task.Run(() => QuickUpload.Stage(_quickPicked, name)); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                StatusLine.Text = ex is InvalidDataException ? "That zip looks broken. Download it again." : ex.Message;
                return;
            }
            _quickMode = true;
            FolderText.Text = "Quick upload: " + name;
            await RefreshAsync();
        }
    }
}
