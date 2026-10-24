// ============================================================================================================
//  KEY PASSWORDS window (Creator window → "Passwords..."): locks a key with a password and saves the locked copy
//  in the repo (KeyVault.cs). Creators lock their own key; the owner can also lock a friend's key or the extras key.
// ============================================================================================================

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BrokenCharactersLauncher
{
    public partial class KeyPasswordWindow : Window
    {
        private readonly Publisher _pub;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public KeyPasswordWindow(Publisher pub)
        {
            _pub = pub;
            InitializeComponent();
            if (!pub.IsAdmin) { FriendItem.Visibility = Visibility.Collapsed; ExtrasItem.Visibility = Visibility.Collapsed; }
            Closed += (_, __) => _cts.Cancel();
        }

        private void WhichBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (KeyRow == null) return; // still loading
            KeyRow.Visibility = WhichBox.SelectedItem == MineItem ? Visibility.Collapsed : Visibility.Visible;
            KeyLabel.Text = WhichBox.SelectedItem == ExtrasItem ? "Paste the extras key" : "Paste your friend's creator key";
            StatusText.Text = "";
        }

        private async void Lock_Click(object sender, RoutedEventArgs e)
        {
            string pw = PasswordBox1.Password;
            if (pw.Length < KeyVault.MinPasswordLength) { Say("The password needs at least " + KeyVault.MinPasswordLength + " characters."); return; }
            if (pw != PasswordBox2.Password) { Say("The two passwords don't match."); return; }

            LockButton.IsEnabled = false;
            try
            {
                string path, what, secret;
                if (WhichBox.SelectedItem == MineItem)
                {
                    secret = Publisher.ReadSavedKey() ?? "";
                    if (secret.Length == 0) { Say("No creator key is saved on this PC."); return; }
                    path = KeyVault.CreatorPath(_pub.Login);
                    what = "creator key for " + _pub.Login;
                }
                else
                {
                    secret = KeyBox.Password.Trim();
                    if (!KeyVault.LooksLikeKey(secret)) { Say("That doesn't look like a key (they start with github_pat_ or ghp_)."); return; }
                    Say("Checking the key...", ok: true);
                    if (WhichBox.SelectedItem == ExtrasItem)
                    {
                        if (!await KeyVault.ExtrasKeyWorksAsync(secret, _cts.Token)) { Say("That key can't open the extras. Check it and try again."); return; }
                        path = KeyVault.ExtrasPath;
                        what = "extras key";
                    }
                    else
                    {
                        string login = await new Publisher(secret).SignInAsync(_cts.Token); // also checks it can publish
                        path = KeyVault.CreatorPath(login);
                        what = "creator key for " + login;
                    }
                }

                Say("Locking...", ok: true);
                string locked = await Task.Run(() => KeyVault.Lock(secret, pw), _cts.Token);
                await _pub.UpdateTextFileAsync(path, _ => locked, "Password-locked " + what, _cts.Token);
                PasswordBox1.Clear(); PasswordBox2.Clear(); KeyBox.Clear();
                Say("Done. The " + what + " is locked. People can now type the password instead of the key (give GitHub a minute to update).", ok: true);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch (GitHubException ex) { Say(ex.Message); }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException) { Say("Couldn't reach GitHub. Check your internet and try again."); }
            finally { LockButton.IsEnabled = true; }
        }

        private void Say(string text, bool ok = false)
        {
            StatusText.Foreground = new System.Windows.Media.SolidColorBrush(ok
                ? System.Windows.Media.Color.FromRgb(0x8F, 0xE3, 0x9A)
                : System.Windows.Media.Color.FromRgb(0xFF, 0x9A, 0x8C));
            StatusText.Text = text;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
