using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLookup.src.pages
{
    public partial class FrameShotPage : Page
    {
        private static FrameShotService _frameShot;

        public FrameShotPage()
        {
            InitializeComponent();

            // Use static instances to persist state across page navigation
            if (_frameShot == null)
            {
                _frameShot = new FrameShotService(msg => System.Diagnostics.Debug.WriteLine($"[FrameShot] {msg}"));

                // Hook events
                _frameShot.OnStateUpdate += (state) =>
                {
                    Dispatcher.Invoke(UpdateStatusUI);
                };

                _frameShot.OnPhotoSaved += (image) =>
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
            RadiusSlider.Value = _frameShot.ActivationRadius;
            HmdRotCheck.IsChecked = _frameShot.UseHmdRotations;
            UpdateStatusUI();
        }

        private void UpdateStatusUI()
        {
            if (_frameShot == null) return;

            if (_frameShot.IsConnected)
            {
                StatusDot.Fill = Brushes.Green;
                StatusText.Text = _frameShot.IsFraming ? "Framing..." : "Connected";
                StatusButton.Content = "Disconnect";
            }
            else
            {
                StatusDot.Fill = Brushes.Red;
                StatusText.Text = _frameShot.LastError ?? "Not Connected";
                StatusButton.Content = "Connect to SteamVR";
            }
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_frameShot == null) return;

            if (_frameShot.IsConnected)
            {
                // If already connected, disconnect the overlay
                _frameShot.Disconnect();

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
            if (_frameShot == null) return;

            if (_frameShot.Connect())
            {
                // Link the overlay service with the D3D device created by FrameShot
                var device = _frameShot.GetDevice();

                _frameShot.StartPolling();
                UpdateStatusUI();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"SteamVR Connection Failed:\n{_frameShot.LastError}",
                    "FrameShot Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RadiusSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_frameShot != null)
            {
                _frameShot.ActivationRadius = (float)e.NewValue;
            }
        }

        private void HmdRotCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_frameShot != null && HmdRotCheck != null)
            {
                _frameShot.UseHmdRotations = HmdRotCheck.IsChecked ?? false;
            }
        }
    }
}