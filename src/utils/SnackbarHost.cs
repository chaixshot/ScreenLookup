using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    /// Utility class for displaying WPF-UI snackbars across windows with optional copy button.
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
            bool closeButton = true,
            bool showCopyButton = true)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Show(title, message, type, timeout, width, showMainWindow, closeButton, showCopyButton);
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
                SnackbarMain.SetCurrentValue(Snackbar.TitleProperty, CreateSnackbarHeader(title, message, showCopyButton));
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
                SnackbarCapture.SetCurrentValue(Snackbar.TitleProperty, CreateSnackbarHeader(title, message, showCopyButton));
                SnackbarCapture.SetCurrentValue(System.Windows.Controls.ContentControl.ContentProperty, message);
                SnackbarCapture.SetCurrentValue(Snackbar.AppearanceProperty, appearance);
                SnackbarCapture.SetCurrentValue(Snackbar.IconProperty, icon);
                SnackbarCapture.SetCurrentValue(Snackbar.TimeoutProperty, TimeSpan.FromSeconds(timeout));
                SnackbarCapture.MinWidth = width;
                SnackbarCapture.IsCloseButtonEnabled = closeButton;
                SnackbarCapture.Show(true);
            }
        }

        private static object CreateSnackbarHeader(string title, string message, bool showCopyButton)
        {
            if (!showCopyButton || (string.IsNullOrEmpty(message) && string.IsNullOrEmpty(title)))
            {
                return title;
            }

            DockPanel dock = new() { LastChildFill = true };

            Wpf.Ui.Controls.TextBlock titleText = new()
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            SymbolIcon copyIcon = new(SymbolRegular.Copy16);
            copyIcon.SetBinding(Control.ForegroundProperty, new Binding("Foreground") { Source = titleText });

            Wpf.Ui.Controls.Button copyBtn = new()
            {
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Icon = copyIcon,
                ToolTip = "Copy content to clipboard",
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Appearance = ControlAppearance.Transparent
            };

            copyBtn.SetBinding(Control.ForegroundProperty, new Binding("Foreground") { Source = titleText });

            string textToCopy = string.IsNullOrEmpty(title) ? message : (string.IsNullOrEmpty(message) ? title : $"{title}\n{message}");

            copyBtn.Click += async (s, e) =>
            {
                try
                {
                    Clipboard.SetText(textToCopy);
                    SymbolIcon checkIcon = new(SymbolRegular.Checkmark16);
                    checkIcon.SetBinding(Control.ForegroundProperty, new Binding("Foreground") { Source = titleText });
                    copyBtn.Icon = checkIcon;
                    copyBtn.ToolTip = "Copied!";
                    await System.Threading.Tasks.Task.Delay(1500);
                    SymbolIcon resetIcon = new(SymbolRegular.Copy16);
                    resetIcon.SetBinding(Control.ForegroundProperty, new Binding("Foreground") { Source = titleText });
                    copyBtn.Icon = resetIcon;
                    copyBtn.ToolTip = "Copy content to clipboard";
                }
                catch { }
            };

            DockPanel.SetDock(copyBtn, Dock.Right);
            dock.Children.Add(copyBtn);
            dock.Children.Add(titleText);

            return dock;
        }
    }
}
