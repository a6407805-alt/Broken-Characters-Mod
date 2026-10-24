// ============================================================================================================
//  The GUIDE window: install/update/publish instructions shown inside the launcher itself (no browser needed).
//  Opened from the GUIDE button in the main window's title bar. "Open as PDF in browser" is an optional fallback
//  to the full docs/Broken Characters - Install Guide.pdf committed to the repo, for anyone who wants to print
//  or share it.
// ============================================================================================================

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace BrokenCharactersLauncher
{
    public partial class GuideWindow : Window
    {
        public GuideWindow()
        {
            InitializeComponent();
        }

        // ------------------------------------------------------------------ extras key unlock (KeyVault.cs)

        private async void Unlock_Click(object sender, RoutedEventArgs e) => await UnlockAsync();

        private async void GuidePasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await UnlockAsync();
        }

        private async System.Threading.Tasks.Task UnlockAsync()
        {
            string pw = GuidePasswordBox.Password;
            if (pw.Length == 0) return;
            UnlockButton.IsEnabled = false;
            UnlockStatus.Visibility = Visibility.Visible;
            UnlockStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xC9, 0xD6, 0xEA));
            UnlockStatus.Text = "Unlocking...";
            (string? key, string? error) = await KeyVault.UnlockFromRepoAsync(KeyVault.ExtrasPath, pw);
            UnlockButton.IsEnabled = true;
            if (key == null)
            {
                UnlockStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x9A, 0x8C));
                UnlockStatus.Text = error;
                RevealRow.Visibility = Visibility.Collapsed;
                return;
            }
            RevealBox.Text = key;
            RevealRow.Visibility = Visibility.Visible;
            UnlockStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8F, 0xE3, 0x9A));
            UnlockStatus.Text = "Unlocked. Press Copy, then paste it into the EXTRAS box (bottom-left of the launcher) and press Save.";
        }

        private void CopyKey_Click(object sender, RoutedEventArgs e)
        {
            try { Clipboard.SetText(RevealBox.Text); CopyKeyButton.Content = "Copied!"; }
            catch (System.Runtime.InteropServices.COMException) { RevealBox.SelectAll(); RevealBox.Focus(); } // clipboard busy: select it for Ctrl+C
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "https://github.com/a6407805-alt/Broken-Characters-Mod/blob/main/docs/Broken%20Characters%20-%20Install%20Guide.pdf")
                { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception ex) { Debug.WriteLine("Could not open the guide PDF: " + ex.Message); }
        }
    }
}
