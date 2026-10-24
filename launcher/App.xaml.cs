using System;
using System.Windows;

namespace BrokenCharactersLauncher
{
    /// <summary>
    /// Application entry point. Normally opens <see cref="MainWindow"/>; "--screenshot-publish &lt;png&gt;" (build server only)
    /// opens the Creator window with demo data instead, saves a picture of it and exits.
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            int i = Array.IndexOf(e.Args, "--screenshot-publish");
            if (i >= 0 && i + 1 < e.Args.Length)
            {
                new PublishWindow(e.Args[i + 1]).Show();
                return;
            }
            int j = Array.IndexOf(e.Args, "--screenshot-jukebox");
            if (j >= 0 && j + 1 < e.Args.Length)
            {
                new JukeboxWindow(e.Args[j + 1]).Show();
                return;
            }
            new MainWindow().Show();
        }
    }
}
