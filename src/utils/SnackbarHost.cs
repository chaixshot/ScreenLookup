using System.Windows;
using Wpf.Ui.Controls;

namespace ScreenLookup.src.utils
{
    public enum SnackbarType
    {
        Warning,
        Error,
        Success,
        Info
    }

    /// <summary>
    /// Utility class for displaying WPF-UI snackbars across windows.
    /// </summary>
    internal static class SnackbarHost
    {
        public static Snackbar? SnackbarMain;
        public static Snackbar? SnackbarCapture;

        public static void Show(
            string title = "",
            string message = "",
            SnackbarType type = SnackbarType.Info,
            int timeout = 5,
            int width = 500,
            bool showMainWindow = false,
            bool closeButton = true)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Show(title, message, type, timeout, width, showMainWindow, closeButton);
                }));
                return;
            }

            ControlAppearance appearance;
            SymbolIcon icon;

            switch (type)
            {
                case SnackbarType.Warning:
                    appearance = ControlAppearance.Caution;
                    icon = new SymbolIcon(SymbolRegular.Alert24);
                    break;
                case SnackbarType.Success:
                    appearance = ControlAppearance.Success;
                    icon = new SymbolIcon(SymbolRegular.CheckmarkCircle24);
                    break;
                case SnackbarType.Error:
                    appearance = ControlAppearance.Danger;
                    icon = new SymbolIcon(SymbolRegular.DismissCircle24);
                    break;
                default:
                    appearance = ControlAppearance.Secondary;
                    icon = new SymbolIcon(SymbolRegular.Info24);
                    break;
            }

            // Create a new Snackbar for both windows if needed
            SnackbarMain ??= new Snackbar(App.mainWindow?.snackbarHost);
            SnackbarCapture ??= new Snackbar(App.captureWindow?.snackbarHost);

            if (showMainWindow && App.mainWindow != null)
            {
                if (!App.mainWindow.IsVisible || !App.mainWindow.IsActive)
                    App.mainWindow.ShowFromTray();
            }

            // Configure and show on Main Window
            if (SnackbarMain != null)
            {
                SnackbarMain.SetCurrentValue(Snackbar.TitleProperty, title);
                SnackbarMain.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
                SnackbarMain.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
                SnackbarMain.SetCurrentValue(Snackbar.IconProperty, icon);
                SnackbarMain.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));
                SnackbarMain.MinWidth = width;
                SnackbarMain.IsCloseButtonEnabled = closeButton;
                SnackbarMain.Show(true);
            }

            // Configure and show on Capture Window
            if (SnackbarCapture != null)
            {
                SnackbarCapture.SetCurrentValue(Snackbar.TitleProperty, title);
                SnackbarCapture.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
                SnackbarCapture.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
                SnackbarCapture.SetCurrentValue(Snackbar.IconProperty, icon);
                SnackbarCapture.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));
                SnackbarCapture.MinWidth = width;
                SnackbarCapture.IsCloseButtonEnabled = closeButton;
                SnackbarCapture.Show(true);
            }
        }
    }
}
