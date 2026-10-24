// ============================================================================================================
//  The Creator window ("PUBLISH MY MOD"). Three steps for the mod creator: choose the mod folder, tick the
//  mod(s), press the big button. All the GitHub work lives in Publisher.cs; this file is just the screen.
//
//  "--screenshot-publish <png>" (used by the build server) shows the window with made-up demo data,
//  saves a picture of it and closes, so the layout can be checked without signing in.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    public partial class PublishWindow : Window
    {
        private readonly string? _screenshotPath;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Publisher? _pub;
        private List<ModPlan> _plans = new List<ModPlan>();
        private string _folder = "";
        private bool _busy;
        private BuildReport? _report;

        public PublishWindow(string? screenshotPath = null)
        {
            InitializeComponent();
            _screenshotPath = screenshotPath;
        }

        // ================================================================== start

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_screenshotPath != null) { await ShowDemoAsync(); return; }

            string? key = Publisher.ReadSavedKey();
            if (string.IsNullOrEmpty(key)) ShowKeyPanel(null);
            else await SignInAsync(key);
        }

        private void ShowKeyPanel(string? error)
        {
            MainPanel.Visibility = Visibility.Collapsed;
            KeyPanel.Visibility = Visibility.Visible;
            KeyError.Text = error ?? "";
            KeyError.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
            KeyBox.Focus();
        }

        private async void SaveKey_Click(object sender, RoutedEventArgs e)
        {
            string key = KeyBox.Password.Trim();
            if (key.Length == 0) return;
            SaveKeyButton.IsEnabled = false;
            try
            {
                Publisher.SaveKey(key);
                KeyBox.Clear();
                await SignInAsync(key);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowKeyPanel("Couldn't save the key: " + ex.Message);
            }
            finally { SaveKeyButton.IsEnabled = true; }
        }

        private async Task SignInAsync(string key)
        {
            try
            {
                var pub = new Publisher(key);
                await pub.SignInAsync(_cts.Token);
                _pub = pub;
            }
            catch (GitHubException ex)
            {
                if (ex.Status == 401 || ex.Status == 403 || ex.Status == 404) Publisher.ForgetKey();
                ShowKeyPanel(ex.Message);
                return;
            }
            catch (Exception ex) when (IsNetworkError(ex))
            {
                ShowKeyPanel("Couldn't reach GitHub. Check your internet and open this window again.");
                return;
            }

            KeyPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            HelloText.Text = "Signed in as " + _pub.Login;

            string? saved = Publisher.ReadSavedFolder();
            if (!string.IsNullOrEmpty(saved) && Directory.Exists(saved))
            {
                _folder = saved;
                FolderText.Text = saved;
                await RefreshAsync();
            }
            else StatusLine.Text = "Press \"Choose folder...\" and pick the folder your mod is in.";
            UpdatePublishButton();
        }

        private void ChangeKey_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            Publisher.ForgetKey();
            _pub = null;
            ShowKeyPanel(null);
        }

        // ================================================================== folder and scan

        private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Pick the folder your mod is in (it has \"src\" or \"mods\" inside)"
            };
            if (_folder.Length > 0 && Directory.Exists(_folder)) dialog.InitialDirectory = _folder;
            if (dialog.ShowDialog(this) != true) return;
            _folder = dialog.FolderName;
            _quickMode = false;
            FolderText.Text = _folder;
            Publisher.SaveFolder(_folder);
            await RefreshAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async Task RefreshAsync()
        {
            if (_pub == null || _folder.Length == 0 || _busy) return;
            SetBusy(true);
            Bar.IsIndeterminate = true;
            ResultText.Text = "";
            CopyErrorsButton.Visibility = Visibility.Collapsed;
            StatusLine.Text = "Checking your files against the pack...";
            try
            {
                _plans = await _pub.ScanAsync(_folder, _cts.Token);
                foreach (ModPlan plan in _plans) plan.PropertyChanged += (_, __) => UpdatePublishButton();
                ModList.ItemsSource = _plans;
                if (_plans.Count == 0)
                {
                    EmptyText.Text = _quickMode
                        ? "Nothing to upload. Either those files are already in the pack, it's someone else's mod, or the mod is built from source (its dll comes from the build server, so send the source instead)."
                        : "No mods of yours were found in this folder, or nothing has changed. If this is the wrong folder, press \"Choose folder...\".";
                    EmptyText.Visibility = Visibility.Visible;
                }
                else EmptyText.Visibility = Visibility.Collapsed;
                StatusLine.Text = _plans.Any(p => p.ChangedCount > 0) ? "Ready. Tick your mod and press the gold button." : "Everything is up to date. Nothing to publish.";
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch (Exception ex) when (ex is GitHubException || ex is InvalidOperationException || ex is IOException || IsNetworkError(ex))
            {
                StatusLine.Text = ex is GitHubException || ex is InvalidOperationException ? ex.Message : "Couldn't check your files: " + ex.Message;
            }
            finally
            {
                Bar.IsIndeterminate = false;
                Bar.Value = 0;
                SetBusy(false);
            }
        }

        // ================================================================== publish

        private void UpdatePublishButton()
        {
            int files = _plans.Where(p => p.WillPublish).Sum(p => p.ChangedCount);
            PublishButton.Content = files == 0 ? "NOTHING TO PUBLISH" : "PUBLISH " + files + (files == 1 ? " FILE" : " FILES");
            PublishButton.IsEnabled = !_busy && files > 0;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            ChooseFolderButton.IsEnabled = !busy;
            RefreshButton.IsEnabled = !busy;
            ChangeKeyButton.IsEnabled = !busy;
            PickFilesButton.IsEnabled = !busy;
            QuickCheckButton.IsEnabled = !busy;
            ModList.IsEnabled = !busy;
            NoteBox.IsEnabled = !busy;
            UpdatePublishButton();
        }

        private async void Publish_Click(object sender, RoutedEventArgs e)
        {
            if (_pub == null) return;
            List<ModPlan> chosen = _plans.Where(p => p.WillPublish).ToList();
            if (chosen.Count == 0) return;
            int files = chosen.Sum(p => p.ChangedCount);
            string names = string.Join(", ", chosen.Select(p => p.Name));

            if (MessageBox.Show(this,
                    "Publish " + files + (files == 1 ? " file" : " files") + " for " + names + "?\n\nEveryone gets the new version the next time they press Update.",
                    "Publish", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            SetBusy(true);
            ResultText.Text = "";
            CopyErrorsButton.Visibility = Visibility.Collapsed;
            string note = NoteBox.Text.Trim();
            string message = names + ": " + (note.Length > 0 ? note : "update") +
                             "\n\nPublished from the Broken Characters launcher by " + _pub.Login + ".";
            var uploadProgress = new Progress<(int Percent, string Text)>(p => { Bar.IsIndeterminate = false; Bar.Value = p.Percent; StatusLine.Text = p.Text; });

            try
            {
                string sha = await _pub.PublishAsync(chosen, message, uploadProgress, _cts.Token);
                NoteBox.Clear();
                Bar.IsIndeterminate = true;
                StatusLine.Text = "Uploaded! Now the build server is compiling it (about 5 to 8 minutes). You can leave this window open.";
                _report = await _pub.WatchBuildAsync(sha, chosen, new Progress<string>(t => StatusLine.Text = t), _cts.Token);

                Bar.IsIndeterminate = false;
                Bar.Value = _report.Finished ? 100 : 0;
                StatusLine.Text = _report.Success ? "All done." : (_report.Finished ? "Published, but something needs fixing." : "Published.");
                ResultText.Foreground = new SolidColorBrush(_report.Success ? Color.FromRgb(0x8F, 0xE3, 0x9A) : Color.FromRgb(0xFF, 0x9A, 0x8C));
                ResultText.Text = string.Join("\n", _report.Lines);
                if (_report.Errors.Count > 0) CopyErrorsButton.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is GitHubException || ex is IOException || ex is InvalidOperationException || IsNetworkError(ex))
            {
                Bar.IsIndeterminate = false;
                Bar.Value = 0;
                StatusLine.Text = "";
                ResultText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x8C));
                ResultText.Text = "Couldn't publish: " + ex.Message + "\nNothing was changed online. Try again in a minute.";
            }
            finally { SetBusy(false); }

            if (_report is { Success: true }) await RefreshAsync(); // mods now show "Up to date"
        }

        private void CopyErrors_Click(object sender, RoutedEventArgs e)
        {
            if (_report == null) return;
            var text = new StringBuilder("I published my mod from the Broken Characters launcher and the build said:\n");
            foreach (string line in _report.Lines) text.AppendLine(line);
            foreach (string err in _report.Errors) text.AppendLine("- " + err);
            try { Clipboard.SetText(text.ToString()); CopyErrorsButton.Content = "Copied! Paste it to your Claude"; }
            catch (System.Runtime.InteropServices.ExternalException) { CopyErrorsButton.Content = "Couldn't copy, try again"; }
        }

        // ================================================================== screenshot demo

        private async Task ShowDemoAsync()
        {
            KeyPanel.Visibility = Visibility.Collapsed;
            MainPanel.Visibility = Visibility.Visible;
            HelloText.Text = "Signed in as DeAndre6969";
            FolderText.Text = @"C:\Users\Vince\Documents\Broken-Characters-Mod";
            _plans = new List<ModPlan>
            {
                new ModPlan { Name = "SpecialForces", Files = DemoFiles(7), IsSelected = true },
                new ModPlan { Name = "StormreachProps", IsNewMod = true, Files = DemoFiles(9), IsSelected = true },
                new ModPlan { Name = "SpecialForcesSounds", Files = DemoFiles(2), IsSelected = true,
                              Conflict = "Someone else (a6407805-alt) changed this mod online on Oct 21. Ask your Claude to \"pull the latest\", then press Refresh." }
            };
            ModList.ItemsSource = _plans;
            NoteBox.Text = "Fixed the boss knockback";
            StatusLine.Text = "Ready. Tick your mod and press the gold button.";
            UpdatePublishButton();
            await Task.Delay(1500);
            SaveScreenshot(_screenshotPath!);
            Application.Current.Shutdown();
        }

        private static List<PublishFile> DemoFiles(int count) =>
            Enumerable.Range(0, count).Select(i => new PublishFile { RemotePath = "demo/" + i, Changed = true }).ToList();

        private void SaveScreenshot(string path)
        {
            if (Content is not FrameworkElement root) return;
            root.UpdateLayout();
            var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
            png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
            using FileStream fs = File.Create(path);
            png.Save(fs);
        }

        // ================================================================== window chrome

        private static bool IsNetworkError(Exception ex) =>
            ex is HttpRequestException || ex is TaskCanceledException || ex is IOException;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy && _screenshotPath == null &&
                MessageBox.Show(this, "A publish is still running. Close anyway? (Files already uploaded stay published.)", "Broken Characters Mod",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }
            _cts.Cancel();
        }
    }
}
