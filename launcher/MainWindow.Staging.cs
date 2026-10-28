// ============================================================================================================
//  Main window: silent staging (creators) and the owner's gold review bar. The GitHub work is in StagingSync.cs.
// ============================================================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BrokenCharactersLauncher
{
    public partial class MainWindow
    {
        private StagingStatus? _staged;
        private string? _stagingKey;

        /// <summary>
        /// Creator (key saved, not the owner): quietly copy plugins edits to staging/&lt;name&gt; (waits at most 45 s, so an
        /// update right after can't wipe them first). Owner: look for staging branches waiting for review (background).
        /// No key: nothing happens.
        /// </summary>
        private async Task RunStagingAsync()
        {
            string? key = StagingSync.ReadCreatorKey();
            if (string.IsNullOrEmpty(key)) return;
            _stagingKey = key;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                (string login, bool owner) = await StagingSync.WhoAmIAsync(key, cts.Token);
                if (login.Length == 0) return;
                if (owner) { _ = ShowOwnerReviewAsync(); return; }

                DateTime installed = File.Exists(VersionFile) ? File.GetLastWriteTimeUtc(VersionFile) : DateTime.MinValue;
                int staged = await StagingSync.SilentStageLocalModAsync(key, login, ReadLocalVersion(), installed, null, cts.Token);
                if (staged > 0) Debug.WriteLine("Staged " + staged + " file(s) for the owner to review.");
            }
            catch (Exception ex) { Debug.WriteLine("Staging skipped: " + ex.Message); } // never bother anyone with this
        }

        private async Task ShowOwnerReviewAsync()
        {
            if (_stagingKey == null) return;
            try
            {
                _staged = await StagingSync.CheckStagingBranchAsync(_stagingKey);
                if (_staged == null) { OwnerSyncBanner.Visibility = Visibility.Collapsed; return; }
                string mods = string.Join(", ", _staged.Mods);
                OwnerSyncTitle.Text = _staged.Creator + " pushed a staging update";
                OwnerSyncDetails.Text = _staged.Files.Count + " file(s) changed" + (mods.Length > 0 ? " in " + mods : "") +
                    (_staged.Date > DateTimeOffset.MinValue ? " · " + _staged.Date.LocalDateTime.ToString("MMM d, h:mm tt") : "") +
                    " · Awaiting your approval";
                OwnerSyncBanner.Visibility = Visibility.Visible;
            }
            catch (Exception ex) { Debug.WriteLine("Staging check skipped: " + ex.Message); }
        }

        private void InspectStaging_Click(object sender, RoutedEventArgs e)
        {
            if (_staged == null) return;
            string list = string.Join("\n", _staged.Files.Take(40).Select(f => (f.Status == "removed" ? "−  " : f.Status == "added" ? "+  " : "~  ") + f.Path));
            if (_staged.Files.Count > 40) list += "\n... and " + (_staged.Files.Count - 40) + " more";
            MessageBox.Show(this, _staged.Creator + " changed these files (+ new, ~ changed, − removed):\n\n" + list,
                "Staging update from " + _staged.Creator, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ApproveStaging_Click(object sender, RoutedEventArgs e)
        {
            if (_staged == null || _stagingKey == null) return;
            if (MessageBox.Show(this, "Publish " + _staged.Creator + "'s " + _staged.Files.Count + " changed file(s) to everyone?\n\n" +
                    "The build server builds the pack and friends get it the next time they press PLAY.",
                    "Approve", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            SetStagingButtons(false);
            SetStatus("Pushing to main...");
            try
            {
                await StagingSync.ApproveAndPublishAsync(_stagingKey, _staged);
                OwnerSyncBanner.Visibility = Visibility.Collapsed;
                SetStatus("Update approved! GitHub Actions build started (about 5 to 8 minutes).");
                _staged = null;
                _ = ShowOwnerReviewAsync(); // another creator may be waiting too
            }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                SetStatus("Couldn't approve: " + ex.Message);
            }
            finally { SetStagingButtons(true); }
        }

        private async void RevertStaging_Click(object sender, RoutedEventArgs e)
        {
            if (_staged == null || _stagingKey == null) return;
            if (MessageBox.Show(this, "Discard " + _staged.Creator + "'s staging draft and keep the current stable version?",
                    "Revert", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            SetStagingButtons(false);
            try
            {
                await StagingSync.RevertStagingAsync(_stagingKey, _staged);
                OwnerSyncBanner.Visibility = Visibility.Collapsed;
                SetStatus("Discarded " + _staged.Creator + "'s draft. Nothing changed for friends.");
                _staged = null;
                _ = ShowOwnerReviewAsync();
            }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                SetStatus("Couldn't revert: " + ex.Message);
            }
            finally { SetStagingButtons(true); }
        }

        private void SetStagingButtons(bool on)
        {
            ApproveStagingButton.IsEnabled = on;
            RevertStagingButton.IsEnabled = on;
            InspectStagingButton.IsEnabled = on;
        }
    }
}
