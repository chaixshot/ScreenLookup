using HotkeyUtility;
using HotkeyUtility.Input;  // Contains HotkeyEventArgs
using ScreenTranslator.src.windows;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace ScreenTranslator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public CaptureWindow? CaptureWindow { get; set; } = null;
        public NotifyIcon notifyIcon = new NotifyIcon();

        public MainWindow()
        {
            InitializeComponent();
            MainWindowHide();

            // Added hotkey
            Hotkey hotkey = new(Key.A, ModifierKeys.Shift | ModifierKeys.Control, Button_Capture);
            HotkeyManager hotkeyManager = HotkeyManager.GetHotkeyManager();
            _ = hotkeyManager.TryAddHotkey(hotkey);

            // Now, pressing Shift + Control + A is no longer registered and Hotkey_Pressed
            // will no longer be triggered by that hotkey.
            // Instead, Hotkey_Pressed will now be triggered by pressing Alt + B.
            _ = hotkeyManager.TryReplaceHotkey(Key.A, ModifierKeys.Shift | ModifierKeys.Control, Key.Z, ModifierKeys.Alt);

            //Tray
            notifyIcon.Icon = new Icon(@"..\..\..\src\logo.ico");
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(1000, "ScreenLookup", "ScreenLookup started in the background", ToolTipIcon.Info);

            notifyIcon.MouseClick += NotifyIcon_Click;

            notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Open", new Bitmap(1, 1), (s, e) =>
            {
                MainWindowShow();
            });
            notifyIcon.ContextMenuStrip.Items.Add("Capture              Alt+Z", new Bitmap(1, 1), Button_Capture);
            notifyIcon.ContextMenuStrip.Items.Add("Quit", new Bitmap(1, 1), (s, e) =>
            {
                MainWindowHide();
            });
        }

        private void Button_Capture(object sender, EventArgs e)
        {
            CaptureWindow = new CaptureWindow();
            CaptureWindow.Show();
        }

        private void WindowStateChanged(object sender, EventArgs e)
        {
            if (WindowState == (WindowState)FormWindowState.Minimized)
            {
                MainWindowHide();
            }
        }

        private void NotifyIcon_Click(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                MainWindowShow();
            }

        }

        private void MainWindowShow()
        {

            this.Show();
            WindowState = (WindowState)FormWindowState.Normal;
        }

        private void MainWindowHide()
        {

            this.Hide();
        }
    }
}