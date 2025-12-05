using ScreenLookup.src.windows;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;


namespace ScreenLookup
{
    public partial class App : Application
    {
        public Forms.NotifyIcon notifyIcon = new Forms.NotifyIcon();

        protected override void OnStartup(StartupEventArgs e)
        {
            //Tray
            notifyIcon.Icon = new Icon("applicationIcon.ico");
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(1000, "ScreenLookup", "ScreenLookup started in the background", Forms.ToolTipIcon.Info);

            notifyIcon.MouseClick += (s, e) =>
            {
                MainWindow.Show();
                MainWindow.WindowState = (WindowState)Forms.FormWindowState.Normal;
            };

            notifyIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Open", new Bitmap(1, 1), (s, e) =>
            {
                MainWindow.Show();
                MainWindow.WindowState = (WindowState)Forms.FormWindowState.Normal;
            });
            notifyIcon.ContextMenuStrip.Items.Add("Capture              Alt+Z", new Bitmap(1, 1), (s, e) =>
            {
                CaptureWindow CaptureWindow = new CaptureWindow();
                CaptureWindow.Show();
            });
            notifyIcon.ContextMenuStrip.Items.Add("Quit", new Bitmap(1, 1), (s, e) =>
            {
                MainWindow.Close();
            });

            base.OnStartup(e);
        }
    }

}
