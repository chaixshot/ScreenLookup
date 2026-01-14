using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace ScreenLookup
{
    public partial class App : Application
    {
        public static readonly string tempFolder = Path.Combine(Path.GetTempPath(), "ScreenLookup");
        public static readonly string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLookup");
        public static readonly Settings? setting = new();
        public static readonly TrayIcon? trayIcon = new();
        public static readonly MainWindow? mainWindow = new();
        public static readonly CaptureWindow? captureWindow = new();
        public static bool Ready = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            trayIcon.Show();

            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(appDataFolder);

            if (setting.StartInBackground)
                Notification.Show("ScreenLookup running in the background");
            else
            {
                mainWindow.Show();
                mainWindow.Activate();
            }


            Ready = true;
            ToggleTopmost();

            base.OnStartup(e);
        }

        public static void ToggleTopmost(bool? enabled = null)
        {
            if (enabled != null)
                App.setting.Topmost = (bool)enabled;

            // Main Window
            Button mainButton = mainWindow.TopmostButton;
            SymbolIcon mainIcon = (SymbolIcon)mainButton?.Icon;
            mainIcon.Filled = App.setting.Topmost;
            mainWindow.Topmost = App.setting.Topmost;

            // Capture Window
            Button captureButton = captureWindow.TopmostButton;
            SymbolIcon captureIcon = (SymbolIcon)captureButton?.Icon;
            captureIcon.Filled = App.setting.Topmost;
            captureWindow.Topmost = App.setting.Topmost;
        }

        private void AppExit(object sender, ExitEventArgs e)
        {
            trayIcon?.Close();
            mainWindow?.Close();
            captureWindow?.Close();
        }
    }
}
