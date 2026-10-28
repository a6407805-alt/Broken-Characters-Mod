// ============================================================================================================
//  The owner's review window (bell next to the title): every creator's staged mod update, its files, and
//  Approve (publish to everyone) / Discard. The GitHub work is in StagingSync.cs.
// ============================================================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BrokenCharactersLauncher
{
    public partial class ReviewWindow : Window
    {
        private readonly string _key;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private bool _busy;
        private string _releaseSha = "";

        /// <summary>How many updates are still waiting when the window closes (the bell's badge).</summary>
        public int Remaining { get; private set; }

        /// <summary>True once something was approved (the main window then says a build is on its way).</summary>
        public bool Approved { get; private set; }

        private static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(0xFF, 0xC9, 0x28));
        private static readonly Brush Dim = new SolidColorBrush(Color.FromRgb(0xC9, 0xD6, 0xEA));
        private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

        public ReviewWindow(string key)
        {
            _key = key;
            InitializeComponent();
            Closed += (_, __) => _cts.Cancel();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadAsync();

        private async Task LoadAsync()
        {
            CardList.Children.Clear();
            StatusLine.Text = "Checking for mod updates...";
            List<StagingStatus> all;
            try { all = await StagingSync.CheckAllStagingAsync(_key, _cts.Token); }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                StatusLine.Text = "Couldn't check GitHub: " + ex.Message;
                return;
            }
            Remaining = all.Count;
            _releaseSha = await StagingSync.LatestReleaseShaAsync(_key, _cts.Token);
            StatusLine.Text = all.Count == 0 ? "Nothing waiting. When anyone drops new files into their game's plugins folder, they show up here."
                                             : all.Count + " update(s) waiting for you.";
            foreach (StagingStatus s in all) CardList.Children.Add(Card(s));
        }

        private UIElement Card(StagingStatus s)
        {
            var panel = new StackPanel();
            string mods = string.Join(", ", s.Mods);
            panel.Children.Add(new TextBlock
            {
                Text = s.Creator + (mods.Length > 0 ? "  ·  " + mods : ""),
                Foreground = Gold, FontSize = 17, FontWeight = FontWeights.Black, TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = s.Files.Count + " file(s)" + (s.Date > DateTimeOffset.MinValue ? "  ·  " + s.Date.LocalDateTime.ToString("MMM d, h:mm tt") : ""),
                Foreground = Dim, FontSize = 12, Margin = new Thickness(0, 2, 0, 8)
            });
            bool oldPack = s.PackVersion.Length > 0 && _releaseSha.Length > 0 &&
                           !_releaseSha.StartsWith(s.PackVersion, StringComparison.OrdinalIgnoreCase) && s.PackVersion != "none";
            if (oldPack)
                panel.Children.Add(new TextBlock
                {
                    Text = "\u26A0 " + s.Creator + "'s pack wasn't up to date when this was sent, so some of these may be OLDER copies " +
                           "than what's on GitHub. Check the list before approving.",
                    Foreground = Red, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8)
                });
            foreach (var f in s.Files.Take(30))
            {
                string mark = f.Status == "removed" ? "−" : f.Status == "added" ? "+" : "~";
                panel.Children.Add(new TextBlock
                {
                    Text = mark + "  " + f.Path,
                    Foreground = f.Status == "removed" ? Red : Brushes.White,
                    FontFamily = new FontFamily("Consolas"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            if (s.Files.Count > 30)
                panel.Children.Add(new TextBlock { Text = "... and " + (s.Files.Count - 30) + " more", Foreground = Dim, FontSize = 12 });

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button discard = MakeButton("↺ Discard", Brushes.Transparent, Red, Red);
            Button approve = MakeButton("✓ APPROVE & PUBLISH", Gold, new SolidColorBrush(Color.FromRgb(0x10, 0x14, 0x20)), Gold);
            discard.Margin = new Thickness(0, 0, 8, 0);
            discard.Click += async (_, __) => await DiscardAsync(s);
            approve.Click += async (_, __) => await ApproveAsync(s);
            buttons.Children.Add(discard);
            buttons.Children.Add(approve);
            panel.Children.Add(buttons);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xC9, 0x28)), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 10),
                Child = panel
            };
        }

        private static Button MakeButton(string text, Brush bg, Brush fg, Brush border)
        {
            var b = new Button { Content = text, Foreground = fg, FontWeight = FontWeights.Black, FontSize = 13, Padding = new Thickness(14, 6, 14, 6), Cursor = Cursors.Hand };
            var t = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.SetValue(Border.BackgroundProperty, bg);
            bd.SetValue(Border.BorderBrushProperty, border);
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            bd.SetValue(Border.PaddingProperty, new Thickness(14, 6, 14, 6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            t.VisualTree = bd;
            b.Template = t;
            return b;
        }

        private async Task ApproveAsync(StagingStatus s)
        {
            if (_busy) return;
            if (MessageBox.Show(this, "Publish " + s.Creator + "'s " + s.Files.Count + " file(s) to everyone?", "Approve",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _busy = true;
            StatusLine.Text = "Publishing " + s.Creator + "'s update...";
            try
            {
                await StagingSync.ApproveAndPublishAsync(_key, s, _cts.Token);
                Approved = true;
                await LoadAsync();
                StatusLine.Text = "Approved! The build server is building the pack now (about 5 to 8 minutes). " + StatusLine.Text;
            }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                StatusLine.Text = "Couldn't approve: " + ex.Message;
            }
            finally { _busy = false; }
        }

        private async Task DiscardAsync(StagingStatus s)
        {
            if (_busy) return;
            if (MessageBox.Show(this, "Throw away " + s.Creator + "'s draft? Friends keep the current version.", "Discard",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            _busy = true;
            try
            {
                await StagingSync.RevertStagingAsync(_key, s, _cts.Token);
                await LoadAsync();
            }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                StatusLine.Text = "Couldn't discard: " + ex.Message;
            }
            finally { _busy = false; }
        }

        // ------------------------------------------------------------------ friends' shared upload key

        private async void SaveSharedKey_Click(object sender, RoutedEventArgs e)
        {
            string shared = SharedKeyBox.Password.Trim();
            if (!KeyVault.LooksLikeKey(shared)) { SharedKeyText.Text = "That doesn't look like a GitHub key (they start with github_pat_ or ghp_)."; return; }
            string? extras = KeyVault.ReadExtrasKey();
            if (string.IsNullOrEmpty(extras)) { SharedKeyText.Text = "This PC needs the extras first (EXTRAS box): the upload key is locked with it."; return; }
            SaveSharedKeyButton.IsEnabled = false;
            try
            {
                SharedKeyText.Text = "Checking the key...";
                // It must be able to write to the pack (create staging branches), or friends' uploads would just fail.
                var probe = new Publisher(shared);
                await probe.SignInAsync(_cts.Token);
                string locked = await Task.Run(() => KeyVault.Lock(shared, extras), _cts.Token);
                await new Publisher(_key).UpdateTextFileAsync(KeyVault.StagingPath, _ => locked, "Friends' upload key (locked with the extras key)", _cts.Token);
                SharedKeyBox.Clear();
                SharedKeyText.Text = "Saved. Everyone with the extras now sends you their mod changes automatically (after they update the launcher).";
            }
            catch (Exception ex) when (ex is GitHubException || ex is System.Net.Http.HttpRequestException || ex is TaskCanceledException)
            {
                SharedKeyText.Text = "Couldn't save it: " + ex.Message;
            }
            finally { SaveSharedKeyButton.IsEnabled = true; }
        }

        private void MakeKey_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { }
            e.Handled = true;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
