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

namespace ScreenLookup
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public NotifyIcon notifyIcon = new NotifyIcon();

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

            Loaded += (s, e) =>
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
                RootNavigation.Navigate(typeof(SettingPage));

                //this.Hide();

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
            };

            WindowStateRestore(this, "Main");
            IsPaneOpen = Setting.RegSetting.GetValue("IsPaneOpen") == null || Setting.RegSetting.GetValue("IsPaneOpen").ToString() == "True";
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool IsPaneOpen
        {
            get { return Setting.IsPaneOpen; }
            set
            {
                Setting.IsPaneOpen = value;
                OnPropertyChanged();
            }
        }

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
    }
}