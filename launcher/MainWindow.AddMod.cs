using System.Windows;

namespace BrokenCharactersLauncher
{
    // The ADD MY MOD button in the title bar. All the work is in AddModWindow.cs.
    public partial class MainWindow
    {
        private void AddMod_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddModWindow { Owner = this };
            window.ShowDialog();
        }
    }
}
