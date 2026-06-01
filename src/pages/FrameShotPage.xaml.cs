using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLookup.src.pages
{
    public partial class FrameShotPage : Page
    {
        private static FrameShotService? FrameShot;

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
            if (FrameShot == null) return;

            if (FrameShot.IsConnected)
            {
                StatusDot.Fill = Brushes.Green;
                StatusText.Text = FrameShot.IsFraming ? "Framing..." : "Connected";
                StatusButton.Content = "Disconnect";
            }
            else
            {
                StatusDot.Fill = Brushes.Red;
                StatusText.Text = FrameShot.LastError ?? "Not Connected";
                StatusButton.Content = "Connect to SteamVR";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (FrameShot != null && FrameShot.IsConnected)
            {
                // If already connected, disconnect the overlay
                FrameShot.Disconnect();

                UpdateStatusUI();

                return;
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
                            App.captureWindow.StartCaptureVR(image, triggerHeld);
                        }));
                    });
                };
            }

            if (FrameShot.Connect())
            {
                FrameShot.StartPolling();
                UpdateStatusUI();
            }
            else
                SnackbarHost.Show("FrameShot Error", $"SteamVR Connection Failed:\n{FrameShot.LastError}", type: SnackbarType.Error);
        }

        private void ActivationRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (FrameShot != null)
                App.setting.ActivationRadius = (int)e.NewValue;
        }

        private void HmdRotCheck_Changed(object sender, RoutedEventArgs e)
        {
            App.setting.UseHmdRotations = HmdRotCheck.IsChecked == true;
        }

        private void UseRightEye_Changed(object sender, RoutedEventArgs e)
        {
            App.setting.UseRightEye = UseRightEye.IsChecked == true;
        }
    }
}