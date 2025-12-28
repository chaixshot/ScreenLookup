using Microsoft.Win32;
using ScreenLookup.src.pages;
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
            InitializeComponent();
            WindowStateRestore(this, "Main");

            Loaded += (s, e) =>
            {
                ApplicationThemeManager.ApplySystemTheme();
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);

                this.RootNavigation.Navigate(typeof(SettingPage));
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
            this.Activate();
            this.WindowState = (WindowState)FormWindowState.Normal;
        }

        private void OnNavigationSelectionChanged(NavigationView navigationView, RoutedEventArgs args)
        {
            headerText.Text = navigationView.SelectedItem.TargetPageTag.ToString();
        }

        #region button
        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            App.ToggleTopmost(!App.setting.Topmost);
        }

        private void MinimizeButton_Clicked(TitleBar sender, RoutedEventArgs args)
        {
            HideToTray();
        }

        private void CloseButton_Clicked(TitleBar sender, RoutedEventArgs args)
        {
            HideToTray();
        }
        #endregion

        #region Window Persistence State
        private void MainWindow_BoundsChanged(object sender, EventArgs e)
        {
            Window window = (Window)sender;
            WindowStateSave(window, "Main");
        }

        private static void WindowStateSave(Window windows, string windowsType)
        {
            if (windows != null)
            {
                App.setting.RegWindowBounds.SetValue("Bounds", windows.RestoreBounds.ToString());
            }
        }

        private static void WindowStateRestore(Window windows, string windowsType)
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
        #endregion
    }
}