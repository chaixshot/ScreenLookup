using Microsoft.Win32;
using ScreenLookup.src.utils;
using System.ComponentModel;
using System.Windows;
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

                ToggleTopmost(App.setting.Topmost);
            };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (App.setting.MinimizeToTray)
                e.Cancel = true;
            else
                App.Current.Shutdown();

            base.OnClosing(e);
        }

        // Title bar
        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTopmost(!App.setting.Topmost);
        }

        private void MinimizeButton_Clicked(TitleBar sender, RoutedEventArgs args)
        {
            HideToTray();
        }

        private void CloseButton_Clicked(TitleBar sender, RoutedEventArgs args)
        {
            HideToTray();
        }

        private void HideToTray()
        {
            if (App.setting.MinimizeToTray)
            {
                try
                {
                    Notification.Show("ScreenLookup running in the background");
                }
                catch { }
                this.WindowState = (WindowState)FormWindowState.Minimized;
                this.Hide();
            }
        }

        public void ShowFromTray()
        {
            this.Show();
            this.WindowState = (WindowState)FormWindowState.Normal;
        }

        public void ToggleTopmost(bool enabled)
        {
            App.setting.Topmost = enabled;

            var button = TopmostButton as Button;
            var symbolIcon = button?.Icon as SymbolIcon;
            symbolIcon.Filled = enabled;
            this.Topmost = enabled;
        }

        // ----------------- Window Persistence State -----------------
        private void MainWindow_BoundsChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            WindowStateSave(window, "Main");
        }

        private void WindowStateSave(Window windows, string windowsType)
        {
            if (windows != null)
            {
                App.setting.RegWindowBounds.SetValue("Bounds", windows.RestoreBounds.ToString());
            }
        }

        private void WindowStateRestore(Window windows, string windowsType)
        {
            if (windows != null)
            {
                RegistryKey key = App.setting.RegWindowBounds;
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