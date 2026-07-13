using HotkeyUtility;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Controls;

namespace ScreenLookup
{
    public partial class App : Application
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private static uint taskbarCreatedMessage;

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

            if (!setting.StartInBackground)
            {
                mainWindow.Show();
                mainWindow.Activate();
            }

            ToggleTopmost();

            // Handle for explorer.exe restart
            {
                taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated"); // Register the message and hook into the MainWindow's message pump

                // MainWindow handle to listen for the OS broadcast
                var wih = new WindowInteropHelper(mainWindow);
                wih.EnsureHandle(); // Ensures the HWND exists even if hidden

                HwndSource source = HwndSource.FromHwnd(wih.Handle);
                source.AddHook(HwndMessageHook);
            }

            base.OnStartup(e);
        }

        // The message loop handler
        private static IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Check if the message matches the "TaskbarCreated" ID assigned by Windows
            if (msg == taskbarCreatedMessage)
            {
                trayIcon?.Close();
                trayIcon = new TrayIcon();
                trayIcon.Show();

                SetupHoykey(); // Refresh the shortcut string on the new tray item header
            }

            return IntPtr.Zero;
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
                SnackbarHost.Show("Lookup Shortcut", "Another application is already using the Lookup Shortcut.", SnackbarType.Error, timeout: 99999, showMainWindow: true);
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
