using ScreenLookup.src.utils;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for InfoPage.xaml - Displays application version and global shortcut information.
    /// </summary>
    public partial class InfoPage : Page
    {
        public InfoPage()
        {
            InitializeComponent();

            VersionTextblock.Content = $"Version {AppUtilities.GetAppVersion()}";
            captureShortcut.KeySet = App.setting.ShortcutKey;
        }
    }
}
