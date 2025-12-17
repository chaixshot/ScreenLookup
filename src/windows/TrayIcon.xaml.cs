using HotkeyUtility;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ScreenLookup.src.windows
{
    /// <summary>
    /// Interaction logic for TrayIcon.xaml
    /// </summary>
    public partial class TrayIcon : Window
    {
        HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
        private Hotkey hotkey;

        public TrayIcon()
        {
            InitializeComponent();
            SetupHoykey();

            Loaded += (s, e) =>
            {
                this.Hide();
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (hotkey != null)
                hotkeyManager.TryRemoveHotkey(hotkey);

            base.OnClosing(e);
        }

        public void SetupHoykey()
        {

            ShortcutKeySet shortcutKey = App.setting.ShortcutKey;
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
                App.captureWindow.StartCaptureScreen();
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


        // Tray context menu
        private void Tray_LeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
        {
            e.Handled = true;
            App.captureWindow.StartCaptureScreen();
        }

        private void TrayItemSettings_Click(object sender, RoutedEventArgs e)
        {
            App.mainWindow.ShowFromTray();
            App.mainWindow.RootNavigation.Navigate(typeof(SettingPage));
        }

        private void TrayItemHistory_Click(object sender, RoutedEventArgs e)
        {
            App.mainWindow.ShowFromTray();
            App.mainWindow.RootNavigation.Navigate(typeof(HistoryPage));
        }

        private void TrayItemSaved_Click(object sender, RoutedEventArgs e)
        {
            App.mainWindow.ShowFromTray();
            App.mainWindow.RootNavigation.Navigate(typeof(SavedPage));
        }

        private void TrayItemCapture_Click(object sender, RoutedEventArgs e)
        {
            App.captureWindow.StartCaptureScreen();
        }

        private void TrayItemExit_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }
    }
}
