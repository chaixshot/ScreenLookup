using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLookup.src.pages
{
    public partial class FrameShotPage : Page
    {
        private static FrameShotService? FrameShot;
        public static SteamOverlayService? SteamOverlay;

        public FrameShotPage()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                // Initialize UI values
                ActivationRadius.Value = App.setting.ActivationRadius;
                HmdRotCheck.IsChecked = App.setting.UseHmdRotations;
                UseRightEye.IsChecked = App.setting.UseRightEye;
            };
        }

        private void UpdateStatusUI()
        {
            if (FrameShot?.IsConnected == true)
            {
                StatusDot.Fill = Brushes.Green;
                StatusText.Text = FrameShot.IsFraming ? "Framing..." : "Connected";
                StatusButton.Content = "Disconnect";
            }
            else
            {
                StatusDot.Fill = Brushes.Red;
                StatusText.Text = FrameShot?.LastError ?? "Not Connected";
                StatusButton.Content = "Connect to SteamVR";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (FrameShot?.IsConnected == true)
            {
                TryDisconnect();
            }
            else
                TryConnect();
        }

        private void TryConnect()
        {
            // Use static instances to persist state across page navigation
            if (FrameShot == null)
            {
                FrameShot = new FrameShotService(msg => System.Diagnostics.Debug.WriteLine($"[FrameShot] {msg}"));

                // Hook events
                FrameShot.OnStateUpdate += (state) =>
                {
                    Dispatcher.Invoke(UpdateStatusUI);
                };

                FrameShot.OnPhotoSaved += (image, triggerHeld) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            App.captureWindow.StartCaptureScreen(image, triggerHeld);
                        }));
                    });
                };
            }

            if (FrameShot.Connect())
            {
                FrameShot.StartPolling();

                SteamOverlay = new SteamOverlayService(
                    parentWindow: App.captureWindow,
                    uiElement: App.captureWindow.configMenu
                );

                UpdateStatusUI();
            }
            else
                SnackbarHost.Show("FrameShot Error", $"SteamVR Connection Failed:\n{FrameShot.LastError}", type: SnackbarType.Error);
        }

        private void TryDisconnect()
        {
            if (FrameShot?.IsConnected == true)
            {
                FrameShot.Disconnect();
                FrameShot.Dispose();
                FrameShot = null;

                SteamOverlay?.Dispose();
                SteamOverlay = null;

                UpdateStatusUI();
            }
        }

        private void ActivationRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.ActivationRadius = (int)e.NewValue;
        }

        private void HmdRotCheck_Changed(object sender, RoutedEventArgs e)
        {
            App.setting.UseHmdRotations = HmdRotCheck.IsChecked == true;
        }

        private void UseRightEye_Changed(object sender, RoutedEventArgs e)
        {
            if (FrameShot?.IsConnected == true)
                TryDisconnect();

            App.setting.UseRightEye = UseRightEye.IsChecked == true;
        }
    }
}