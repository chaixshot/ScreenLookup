using HotkeyUtility;
using HotkeyUtility.Input;  // Contains HotkeyEventArgs
using ScreenTranslator.src.windows;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ScreenTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public CaptureWindow? CaptureWindow { get; set; } = null;


        public MainWindow()
        {
            InitializeComponent();

            // Added hotkey
            Hotkey hotkey = new(Key.A, ModifierKeys.Shift | ModifierKeys.Control, Button_Capture);
            HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
            _ = hotkeyManager.TryAddHotkey(hotkey);

            // Now, pressing Shift + Control + A is no longer registered and Hotkey_Pressed
            // will no longer be triggered by that hotkey.
            // Instead, Hotkey_Pressed will now be triggered by pressing Alt + B.
            _ = hotkeyManager.TryReplaceHotkey(Key.A, ModifierKeys.Shift | ModifierKeys.Control, Key.Z, ModifierKeys.Alt);

        }

        private void Button_Capture(object sender, RoutedEventArgs e)
        {
            CaptureWindow = new CaptureWindow();
            CaptureWindow.Show();
        }

        private void Button_Tray(object sender, RoutedEventArgs e)
        {
        }
    }
}