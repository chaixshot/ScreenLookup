using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.IO;
using System.Windows;

namespace ScreenLookup
{
    public partial class App : Application
    {
        public static readonly string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLookup");
        public static readonly Settings? setting = new();
        public static readonly TrayIcon? trayIcon = new();
        public static readonly MainWindow? mainWindow = new();
        public static readonly CaptureWindow? captureWindow = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            trayIcon.Show();

            Directory.CreateDirectory(appDataFolder);

            if (setting.StartInBackground)
                Notification.Show("ScreenLookup running in the background");
            else
            {
                mainWindow.Show();
            }

            base.OnStartup(e);
        }

        private void AppExit(object sender, ExitEventArgs e)
        {
            trayIcon?.Close();
            mainWindow?.Close();
            captureWindow?.Close();
        }
    }
}
