using HotkeyUtility;
using Microsoft.Win32;
using ScreenLookup.src.models;
using ScreenLookup.src.pages;
using ScreenLookup.src.windows;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;

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
                {
                    HideToTray();
                }

                WindowStateRestore(this, "Main");
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // setting cancel to true will cancel the close request
            // so the application is not closed
            e.Cancel = true;

            HideToTray();

            base.OnClosing(e);
        }

        private void HideToTray()
        {
            this.WindowState = (WindowState)FormWindowState.Minimized;
            NotifyIcon.Visibility = Visibility.Visible;

            Forms.NotifyIcon notifyIcon = new Forms.NotifyIcon();
            notifyIcon.Icon = new Icon("applicationIcon.ico");
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(500, "ScreenLookup", "ScreenLookup started in the background", Forms.ToolTipIcon.Info);
            notifyIcon.Visible = false;
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = (WindowState)FormWindowState.Normal;
        }

        // ----------------- Window Persistence State -----------------
        private void WindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == (WindowState)FormWindowState.Minimized)
            {
                this.Hide();
            }
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

        private void NotifyIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!NotifyIcon.IsVisible)
                NotifyIcon.Visibility = Visibility.Visible;
        }
    }
}