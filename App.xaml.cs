using HotkeyUtility;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace ScreenLookup
{
    public partial class App : Application
    {
        public static readonly string tempFolder = Path.Combine(Path.GetTempPath(), "ScreenLookup");
        public static readonly string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLookup");

        public static Settings setting;
        public static CaptureWindow captureWindow;
        public static TrayIcon trayIcon;
        public static MainWindow mainWindow;

        public static SettingPage? settingPage;

        private static readonly HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
        private static Hotkey? hotkey;

        protected override void OnStartup(StartupEventArgs e)
        {
            Directory.CreateDirectory(tempFolder);
            Directory.CreateDirectory(appDataFolder);

            setting = new();

            trayIcon = new();
            trayIcon.Show();

            mainWindow = new();
            captureWindow = new();

            setting.Load();

            if (setting.StartInBackground)
                Notification.Show("ScreenLookup running in the background");
            else
            {
                mainWindow.Show();
                mainWindow.Activate();
            }

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

        public static void SetupHoykey()
        {

            ShortcutKeySet shortcutKey = setting.ShortcutKey;
            ModifierKeys modifierKey = ModifierKeys.None;
            trayIcon.trayCapture.Header = "Lookup".PadRight(20);
            foreach (ModifierKeys key in shortcutKey.Modifiers)
            {
                modifierKey |= key;
                trayIcon.trayCapture.Header += $"{key}+";
            }
            trayIcon.trayCapture.Header += shortcutKey.NonModifierKey.ToString();

            if (hotkey != null)
                hotkeyManager.TryRemoveHotkey(hotkey);

            hotkey = new(shortcutKey.NonModifierKey, modifierKey, (s, e) =>
            {
                captureWindow.StartCaptureScreen();
            });

            try
            {
                hotkeyManager.TryAddHotkey(hotkey);
            }
            catch
            {
                SnackbarHost.Show("Lookup Shortcut", "Another application is already using the Lookup Shortcut.", "error", 99999, showMainWindow: true);
            }
        }

        private void AppExit(object sender, ExitEventArgs e)
        {
            trayIcon?.Close();
            mainWindow?.Close();
            captureWindow?.Close();

            if (hotkey != null)
                hotkeyManager.TryRemoveHotkey(hotkey);
        }
    }
}
