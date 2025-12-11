using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.Windows;

namespace ScreenLookup
{
    public partial class App : Application
    {
        public static Settings? setting = new();
        public static TrayIcon? trayIcon;
        public static MainWindow? mainWindow = new();
        public static CaptureWindow? captureWindow = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            trayIcon = new();
            trayIcon.Show();

            if (setting.StartInBackground)
                Notification.Show("ScreenLookup running in the background");
            else
            {
                mainWindow.Show();
            }

            base.OnStartup(e);
        }

        public static CaptureWindow GetCaptureWindow()
        {
            if (captureWindow != null)
                return captureWindow;
            captureWindow = new CaptureWindow();
            captureWindow.Activate();
            return captureWindow;
        }

        private void AppExit(object sender, ExitEventArgs e)
        {
            trayIcon?.Close();
            mainWindow?.Close();
            captureWindow?.Close();
        }
    }
}
