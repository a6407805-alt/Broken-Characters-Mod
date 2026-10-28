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
        private async Task ShowOwnerReviewAsync()
        {
            if (_stagingKey == null) return;
            ReviewBellButton.Visibility = Visibility.Visible;
            try
            {
                var all = await StagingSync.CheckAllStagingAsync(_stagingKey);
                SetBell(all.Count);
                if (all.Count > 0) SetStatus(all.Count + " mod update(s) from creators waiting for you: click the bell.");
            }
            catch (Exception ex) { Debug.WriteLine("Staging check skipped: " + ex.Message); }
        }

        private void SetBell(int count)
        {
            ReviewBellButton.Tag = count > 0 ? count.ToString() : null;
            ReviewBellButton.ToolTip = count > 0 ? count + " mod update(s) waiting for your approval" : "No mod updates waiting";
        }

        private void ReviewBell_Click(object sender, RoutedEventArgs e)
        {
            if (_stagingKey == null) return;
            var w = new ReviewWindow(_stagingKey) { Owner = this };
            w.ShowDialog();
            SetBell(w.Remaining);
            if (w.Approved) SetStatus("Update approved! The build server is publishing it (about 5 to 8 minutes).");
        }
    }
}
