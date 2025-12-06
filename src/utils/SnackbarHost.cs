using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    internal class SnackbarHost
    {

        public static void Show(string title, string message, string type)
        {
            ControlAppearance appearance = ControlAppearance.Secondary;

            if (type == "warning")
                appearance = ControlAppearance.Caution;
            else if (type == "success")
                appearance = ControlAppearance.Success;
            else if (type == "error")
                appearance = ControlAppearance.Danger;

            var SnackbarHost = (App.Current.MainWindow as MainWindow)?.SnackbarHost;
            var snackbar = new Snackbar(SnackbarHost)
            {
                Title = title,
                Content = message,
                Appearance = appearance,
                Timeout = TimeSpan.FromSeconds(3),
            };

            snackbar.Show();
        }
    }
}
