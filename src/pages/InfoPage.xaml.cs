using ScreenLookup.src.utils;
using System.Windows.Controls;

namespace ScreenLookup.src.pages
{
    /// <summary>
    /// Interaction logic for InfoPage.xaml
    /// </summary>
    public partial class InfoPage : Page
    {
        public InfoPage()
        {
            InitializeComponent();

            VersionTextblock.Text = $"Version {AppUtilities.GetAppVersion()}";
        }
    }
}
