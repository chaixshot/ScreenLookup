using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ScreenLookup.src.models
{
    class Notification
    {
        public static void Show(string text = "Notification", int timeout = 1000)
        {
            NotifyIcon notifyIcon = new NotifyIcon();
            notifyIcon.Icon = new Icon("applicationIcon.ico");
            notifyIcon.Visible = true;
            notifyIcon.ShowBalloonTip(timeout, "ScreenLookup", text, ToolTipIcon.Info);
            notifyIcon.Dispose();
        }
    }
}
