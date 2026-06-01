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

            // Use static instances to persist state across page navigation
            if (FrameShot == null)
            {
                FrameShot = new FrameShotService(msg => System.Diagnostics.Debug.WriteLine($"[FrameShot] {msg}"));

                // Hook events
                FrameShot.OnStateUpdate += (state) =>
                {
                    Dispatcher.Invoke(UpdateStatusUI);
                };

                FrameShot.OnPhotoSaved += (image) =>
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            App.captureWindow.StartCaptureVR(image);
                        }));
                    });
                };
            }

            // Initialize UI values
            ActivationRadius.Value = App.setting.activationRadius;
            HmdRotCheck.IsChecked = App.setting.useHmdRotations;
            UpdateStatusUI();
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
            if (FrameShot == null) return;

            if (FrameShot.IsConnected)
            {
                // If already connected, disconnect the overlay
                FrameShot.Disconnect();

                UpdateStatusUI();

                return;
            }
            else
            {
                Connect();
            }
        }

        private void Connect()
        {
            if (FrameShot == null) return;

            if (FrameShot.Connect())
            {
                FrameShot.StartPolling();
                UpdateStatusUI();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"SteamVR Connection Failed:\n{FrameShot.LastError}",
                    "FrameShot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
    }
}