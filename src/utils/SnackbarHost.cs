using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    internal class SnackbarHost
    {
        public static Snackbar? snackbarMain;
        public static Snackbar? snackbarCapture;
        public static MainWindow? mainWindow = (MainWindow)App.Current.MainWindow;

        public static void Show(string title = "", string message = "", string type = "info", int timeout = 5, int width = 500, string windows = "main", bool closeButton = true)
        {
            ControlAppearance appearance;
            SymbolIcon icon;
            Snackbar? snackbar;

            if (type == "warning")
            {
                appearance = ControlAppearance.Caution;
                icon = new SymbolIcon(SymbolRegular.Alert24);
            }
            else if (type == "success")
            {
                appearance = ControlAppearance.Success;
                icon = new SymbolIcon(SymbolRegular.CheckmarkCircle24);
            }
            else if (type == "error")
            {
                appearance = ControlAppearance.Danger;
                icon = new SymbolIcon(SymbolRegular.DismissCircle24);
            }
            else
            {
                appearance = ControlAppearance.Secondary;
                icon = new SymbolIcon(SymbolRegular.Info24);
            }

            if (windows == "main")
            {
                snackbarMain ??= new Snackbar(mainWindow?.snackbarHost);
                snackbar = snackbarMain;
            }
            else
            {
                snackbarCapture ??= new Snackbar((mainWindow?.GetCaptureWindow().snackbarHost));
                snackbar = snackbarCapture;
            }

            snackbar.SetCurrentValue(Snackbar.TitleProperty, title);
            snackbar.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
            snackbar.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
            snackbar.SetCurrentValue(Snackbar.IconProperty, icon);
            snackbar.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));

            snackbar.Width = width;
            snackbar.IsCloseButtonEnabled = closeButton;

            snackbar.Show(true);
        }
    }
}
