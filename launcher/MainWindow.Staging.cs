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
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    public partial class MainWindow
    {
        private string? _stagingKey;
        /// <summary>Changed mod files that couldn't be sent this time: the pack update is skipped so they aren't wiped.</summary>
        private int _modsNotSent;

        /// <summary>
        /// Creator (key saved, not the owner): quietly copy plugins edits to staging/&lt;name&gt; (waits at most 45 s, so an
        /// update right after can't wipe them first). Owner: look for staging branches waiting for review (background).
        /// No key: nothing happens.
        /// </summary>
        private async Task RunStagingAsync()
        {
            string? key = StagingSync.ReadCreatorKey();
            if (string.IsNullOrEmpty(key)) { await StageWithSharedKeyAsync(); return; }
            _stagingKey = key;
            try
            {
                // Big mod files can take a few minutes to upload; 45 s used to cut the upload off silently.
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                (string login, bool owner) = await StagingSync.WhoAmIAsync(key, cts.Token);
                if (login.Length == 0) return;
                if (owner) { _ = ShowOwnerReviewAsync(); return; }

                DateTime installed = File.Exists(VersionFile) ? File.GetLastWriteTimeUtc(VersionFile) : DateTime.MinValue;
                var progress = new Progress<string>(SetStatus);
                // Owner's rule: creators' changes go straight to everyone, no approval.
                int staged = await StagingSync.SilentStageLocalModAsync(key, login, ReadLocalVersion(), installed, null, cts.Token, autoPublish: true, progress: progress);
                if (staged > 0) SetStatus("Published " + staged + " changed file(s) to everyone. The new pack is ready in about 5 to 8 minutes.");
                if (staged < 0) _modsNotSent = -staged; // keep the pack as it is, or the update would delete them
            }
            catch (Exception ex) { StagingSync.Log("Skipped: " + ex.Message); } // never bother anyone with this
        }

        /// <summary>
        /// Everyone else (no creator key): if this PC has the extras, unlock the friends' shared upload key with the
        /// extras key and stage under staging/&lt;windows user name&gt;. Silent, like the creator path.
        /// </summary>
        private async Task StageWithSharedKeyAsync()
        {
            try
            {
                string? extras = KeyVault.ReadExtrasKey();
                if (string.IsNullOrEmpty(extras)) { StagingSync.Log("No creator key and no extras: nothing to send with."); return; }
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                (string? shared, _) = await KeyVault.UnlockFromRepoAsync(KeyVault.StagingPath, extras, cts.Token);
                if (string.IsNullOrEmpty(shared)) { StagingSync.Log("No creator key, and no friends' upload key set up yet."); return; } // owner hasn't set one up
                string name = System.Text.RegularExpressions.Regex.Replace(Environment.UserName.ToLowerInvariant(), "[^a-z0-9-]+", "-").Trim('-');
                if (name.Length == 0) name = Environment.MachineName.ToLowerInvariant();
                DateTime installed = File.Exists(VersionFile) ? File.GetLastWriteTimeUtc(VersionFile) : DateTime.MinValue;
                int staged = await StagingSync.SilentStageLocalModAsync(shared, name, ReadLocalVersion(), installed, null, cts.Token);
                if (staged > 0) SetStatus("Sent " + staged + " changed file(s) to the pack owner for review.");
            }
            catch (Exception ex) { StagingSync.Log("Shared key skipped: " + ex.Message); }
        }

        /// <summary>Owner only: show the bell, with a red count when creators' updates are waiting.</summary>
        private System.Windows.Threading.DispatcherTimer? _bellTimer;

        /// <summary>Owner: show the bell and check every 2 minutes. It lights up yellow when creators uploaded something
        /// since the owner last opened it, or when an update waits for approval.</summary>
        private async Task ShowOwnerReviewAsync()
        {
            if (_stagingKey == null) return;
            ReviewBellButton.Visibility = Visibility.Visible;
            if (_bellTimer == null)
            {
                _bellTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
                _bellTimer.Tick += async (_, __) => await RefreshBellAsync();
                _bellTimer.Start();
            }
            await RefreshBellAsync();
        }

        private async Task RefreshBellAsync()
        {
            if (_stagingKey == null) return;
            try
            {
                var waiting = await StagingSync.CheckAllStagingAsync(_stagingKey);
                DateTimeOffset seen = StagingSync.BellSeen();
                var uploads = await StagingSync.RecentUploadsAsync(_stagingKey, 30);
                int fresh = uploads.Count(u => u.FromCreator && u.Date > seen && u.Build != "part");
                SetBell(waiting.Count, fresh, uploads.Any(u => u.Date > seen && (u.Build == "building" || u.Build == "queued")));
                if (waiting.Count > 0) SetStatus(waiting.Count + " mod update(s) waiting for you: click the bell.");
                else if (fresh > 0) SetStatus(fresh + " new mod upload(s) from creators: click the yellow bell to see them.");
            }
            catch (Exception ex) { Debug.WriteLine("Bell check skipped: " + ex.Message); }
        }

        private void SetBell(int waiting, int fresh = 0, bool building = false)
        {
            int count = waiting + fresh;
            bool lit = count > 0 || building;
            ReviewBellButton.Tag = count > 0 ? count.ToString() : null;
            ReviewBellButton.Background = new SolidColorBrush(lit ? Color.FromRgb(0xFF, 0xC9, 0x28) : Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            ReviewBellButton.Foreground = new SolidColorBrush(lit ? Color.FromRgb(0x10, 0x14, 0x20) : Colors.White);
            ReviewBellButton.ToolTip = waiting > 0 ? waiting + " update(s) waiting for your approval"
                                     : fresh > 0 ? fresh + " new upload(s) from creators"
                                     : building ? "An upload is building right now"
                                     : "Mod uploads and updates";
        }

        private void ReviewBell_Click(object sender, RoutedEventArgs e)
        {
            if (_stagingKey == null) return;
            var w = new ReviewWindow(_stagingKey) { Owner = this };
            StagingSync.MarkBellSeen(); // opening it counts as seeing the new uploads
            w.ShowDialog();
            SetBell(w.Remaining);
            _ = RefreshBellAsync();
            if (w.Approved) SetStatus("Update approved! The build server is publishing it (about 5 to 8 minutes).");
        }
    }
}
