using ScreenLookup.src.pages;
using System.Windows;

namespace ScreenLookup.src.windows
{
    /// <summary>
    /// Interaction logic for TrayIcon.xaml
    /// </summary>
    public partial class TrayIcon : Window
    {
        public TrayIcon()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                this.Hide();
            };
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
