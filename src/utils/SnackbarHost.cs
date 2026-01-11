using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    internal class SnackbarHost
    {
        public static Snackbar? snackbarMain;
        public static Snackbar? snackbarCapture;
        public static MainWindow? mainWindow = App.mainWindow;

        public static void Show(string title = "", string message = "", string type = "info", int timeout = 5, int width = 500, bool showMainWindow = false, bool closeButton = true)
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

            // Create a new Snackbar for both windows
            snackbarMain ??= new Snackbar(mainWindow?.snackbarHost);
            snackbarCapture ??= new Snackbar((App.captureWindow.snackbarHost));

            if (showMainWindow)
            {
                if (!App.mainWindow.IsVisible || !App.mainWindow.IsActive)
                    App.mainWindow.ShowFromTray();
            }

            // Main Window
            snackbarMain.SetCurrentValue(Snackbar.TitleProperty, title);
            snackbarMain.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
            snackbarMain.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
            snackbarMain.SetCurrentValue(Snackbar.IconProperty, icon);
            snackbarMain.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));
            snackbarMain.MinWidth = width;
            snackbarMain.IsCloseButtonEnabled = closeButton;
            snackbarMain.Show(true);

            //Capture Window
            snackbarCapture.SetCurrentValue(Snackbar.TitleProperty, title);
            snackbarCapture.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
            snackbarCapture.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
            snackbarCapture.SetCurrentValue(Snackbar.IconProperty, icon);
            snackbarCapture.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));
            snackbarCapture.MinWidth = width;
            snackbarCapture.IsCloseButtonEnabled = closeButton;
            snackbarCapture.Show(true);
        }
    }
}
