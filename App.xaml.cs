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
        #region Native Interop
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private static uint taskbarCreatedMessage;
        #endregion

        #region Application Global State
        public static readonly string tempFolder = Path.Combine(Path.GetTempPath(), "ScreenLookup");
        public static readonly string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenLookup");

        public static Settings setting = null!;
        public static CaptureWindow captureWindow = null!;
        public static TrayIcon trayIcon = null!;
        public static MainWindow mainWindow = null!;

        public static SettingPage? settingPage;

        private static readonly HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
        private static Hotkey? hotkey;
        #endregion

        #region Application Lifecycle
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

            if (setting.AutoConnectStamVR)
                FrameShotPage.AutoConnectSteamVR();

            ToggleTopmost();

            // Register message hook for Explorer taskbar restart
            {
                taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

                var wih = new WindowInteropHelper(mainWindow);
                wih.EnsureHandle();

                HwndSource? source = HwndSource.FromHwnd(wih.Handle);
                source?.AddHook(HwndMessageHook);
            }

            base.OnStartup(e);
        }

        private static IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == taskbarCreatedMessage)
            {
                trayIcon?.Close();
                trayIcon = new TrayIcon();
                trayIcon.Show();

                SetupHoykey();
            }

            return IntPtr.Zero;
        }

        private void AppExit(object sender, ExitEventArgs e)
        {
            trayIcon?.Close();
            mainWindow?.Close();
            captureWindow?.Close();

            if (hotkey != null)
                hotkeyManager.TryRemoveHotkey(hotkey);
        }
        #endregion

        #region Public Application Actions
        public static void ToggleTopmost(bool? enabled = null)
        {
            if (enabled != null)
                setting.Topmost = enabled.Value;

            // Main Window
            if (mainWindow?.TopmostButton?.Icon is SymbolIcon mainIcon)
            {
                mainIcon.Filled = setting.Topmost;
                mainWindow.Topmost = setting.Topmost;
            }

            // Capture Window
            if (captureWindow?.TopmostButton?.Icon is SymbolIcon captureIcon)
            {
                captureIcon.Filled = setting.Topmost;
                captureWindow.Topmost = setting.Topmost;
            }
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
                captureWindow.DesktopCaptureScreen();
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
        #endregion
    }
}
