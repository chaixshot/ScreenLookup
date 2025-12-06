using HotkeyUtility;
using Microsoft.Win32;
using ScreenLookup.src.utils;
using ScreenLookup.src.pages;
using ScreenLookup.src.windows;
using System.ComponentModel;
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
        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            WindowStateRestore(this, "Main");

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                RootNavigation.Navigate(typeof(SettingPage));

                // Added hotkey
                Hotkey hotkey = new(Key.A, ModifierKeys.Shift | ModifierKeys.Control, (s, e) =>
                {
                    CaptureWindow CaptureWindow = new CaptureWindow();
                    CaptureWindow.Show();
                });
                HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
                _ = hotkeyManager.TryAddHotkey(hotkey);

                // Now, pressing Shift + Control + A is no longer registered and Hotkey_Pressed
                // will no longer be triggered by that hotkey.
                // Instead, Hotkey_Pressed will now be triggered by pressing Alt + B.
                _ = hotkeyManager.TryReplaceHotkey(Key.A, ModifierKeys.Shift | ModifierKeys.Control, Key.Z, ModifierKeys.Alt);

                if (Setting.StartInBackground)
                    HideToTray();

                ToggleTopmost(Setting.Topmost);
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (Setting.MinimizeToTray)
                e.Cancel = true;
                HideToTray();

            base.OnClosing(e);
        }

        private void HideToTray()
        {
            if (Setting.MinimizeToTray)
            {
                this.Hide();
                this.WindowState = (WindowState)FormWindowState.Minimized;
                Notification.Show("ScreenLookup started in the background", 500);
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
            ShowFromTray();
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