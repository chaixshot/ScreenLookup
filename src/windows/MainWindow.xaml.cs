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
            WindowStateRestore();

            Loaded += (s, e) =>
            {
                ApplicationThemeManager.ApplySystemTheme();
                SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);

                if (App.setting.FirstRun)
                    this.RootNavigation.Navigate(typeof(InfoPage));
                else
                {
                    this.RootNavigation.Navigate(typeof(SettingPage));
                    AppUtilities.ChackForUpdate();
                }
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
            WindowStateSave();
        }

        private void WindowStateSave()
        {
            App.setting.RegWindowBounds.SetValue("Bounds", this.RestoreBounds.ToString());
            App.setting.RegWindowBounds.SetValue("State", this.WindowState.ToString());
        }

        private void WindowStateRestore()
        {
            RegistryKey key = App.setting.RegWindowBounds;

            if (key.GetValue("Bounds") != null)
            {
                Rect bounds = Rect.Parse(key.GetValue("Bounds").ToString());
                if (!bounds.IsEmpty)
                {
                    this.Top = bounds.Top;
                    this.Left = bounds.Left;

                    // Restore the size only for a manually sized
                    if (this.SizeToContent == SizeToContent.Manual)
                    {
                        this.Width = bounds.Width;
                        this.Height = bounds.Height;
                    }
                }
            }

            if (key.GetValue("State") != null)
            {
                string state = key.GetValue("State").ToString();
                this.WindowState = state == "Maximized" ? WindowState.Maximized : WindowState.Normal;
            }
        }
        #endregion
    }
}