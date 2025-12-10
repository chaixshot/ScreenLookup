using HotkeyUtility;
using Microsoft.Win32;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using ScreenLookup.src.utils;
using ScreenLookup.src.windows;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using FormWindowState = System.Windows.Forms.FormWindowState;

namespace ScreenLookup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public static CaptureWindow captureWindow;
        private Hotkey hotkey;

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            WindowStateRestore(this, "Main");

            captureWindow = GetCaptureWindow();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                RootNavigation.Navigate(typeof(SettingPage));

                if (Setting.StartInBackground)
                    HideToTray();

                ToggleTopmost(Setting.Topmost);
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Setting.MinimizeToTray)
                e.Cancel = true;
            else
                GetCaptureWindow().Close();

            var isShuttingDownProp = typeof(Application).GetProperty("IsShuttingDown", BindingFlags.Static | BindingFlags.NonPublic);
            dynamic isShuttingDown = isShuttingDownProp.GetValue(Application.Current, null);
            if (!isShuttingDown)
                HideToTray();

            base.OnClosing(e);
        }

        public CaptureWindow GetCaptureWindow()
        {
            if (captureWindow != null)
                return captureWindow;
            captureWindow = new CaptureWindow();
            captureWindow.Activate();
            return captureWindow;
        }

        public void SetupHoykey()
        {
            HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
            ShortcutKeySet shortcutKey = Setting.ShortcutKey;
            ModifierKeys modifierKey = ModifierKeys.None;
            trayCapture.Header = "Lookup".PadRight(20);
            foreach (ModifierKeys key in shortcutKey.Modifiers)
            {
                modifierKey |= key;
                trayCapture.Header += $"{key}+";
            }
            trayCapture.Header += shortcutKey.NonModifierKey.ToString();

            if (hotkey != null)
                hotkeyManager.TryRemoveHotkey(hotkey);

            hotkey = new(shortcutKey.NonModifierKey, modifierKey, (s, e) =>
            {
                GetCaptureWindow().StartCaptureScreen();
            });

            try
            {
                hotkeyManager.TryAddHotkey(hotkey);
            }
            catch
            {
                Notification.Show("Lookup Shortcut. The shortcut is already in use for other application");
            }
        }

        private void HideToTray()
        {
            if (Setting.MinimizeToTray)
            {
                try
                {
                    Notification.Show("ScreenLookup running in the background");
                }
                catch (Exception)
                {
                }
                this.WindowState = (WindowState)FormWindowState.Minimized;
                this.Hide();
            }
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = (WindowState)FormWindowState.Normal;
        }

        public void ToggleTopmost(bool enabled)
        {
            Setting.Topmost = enabled;

            var button = TopmostButton as Button;
            var symbolIcon = button?.Icon as SymbolIcon;
            symbolIcon.Filled = enabled;
            this.Topmost = enabled;
        }

        // ----------------- Window Persistence State -----------------
        private void WindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == (WindowState)FormWindowState.Minimized)
                HideToTray();
        }

        private void MainWindow_BoundsChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            WindowStateSave(window, "Main");
        }

        private void WindowStateSave(Window windows, string windowsType)
        {
            if (windows != null)
            {
                Setting.RegWindowBounds.SetValue("Bounds", windows.RestoreBounds.ToString());
            }
        }

        private void WindowStateRestore(Window windows, string windowsType)
        {
            if (windows != null)
            {
                RegistryKey key = Setting.RegWindowBounds;
                if (key.GetValue("Bounds") != null)
                {
                    Rect bounds = Rect.Parse(key.GetValue("Bounds").ToString());
                    if (!bounds.IsEmpty)
                    {
                        windows.Top = bounds.Top;
                        windows.Left = bounds.Left;

                        // Restore the size only for a manually sized
                        if (windows.SizeToContent == SizeToContent.Manual)
                        {
                            windows.Width = bounds.Width;
                            windows.Height = bounds.Height;
                        }
                    }
                }
            }
        }

        // ----------------- Tray Icon -----------------
        private void NotifyIcon_LeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            e.Handled = true;
            GetCaptureWindow().StartCaptureScreen();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowFromTray();
        }

        private void CaptureWindowItem_Click(object sender, RoutedEventArgs e)
        {
            CaptureWindow CaptureWindow = new CaptureWindow();
            CaptureWindow.Show();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTopmost(!Setting.Topmost);
        }
    }
}