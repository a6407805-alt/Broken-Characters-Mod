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
