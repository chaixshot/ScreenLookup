using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    internal class SnackbarHost
    {

        public static void Show(string title, string message, bool isError = false)
        {
            var SnackbarHost = (App.Current.MainWindow as MainWindow)?.SnackbarHost;
            var snackbar = new Wpf.Ui.Controls.Snackbar(SnackbarHost)
            {
                Title = title,
                Content = message,
                Appearance = isError ? ControlAppearance.Danger : ControlAppearance.Light,
                Timeout = TimeSpan.FromSeconds(2)
            };

            snackbar.Show();
        }
    }
}
