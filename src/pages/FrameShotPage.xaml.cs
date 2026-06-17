using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScreenLookup.src.pages
{
    public partial class FrameShotPage : Page
    {
        private static FrameShotService? FrameShot;
        private static SteamOverlayService? SteamOverlay;

        public FrameShotPage()
        {
            InitializeComponent();

            // Initialize UI values
            {
                AutoConnectStamVR.IsChecked = App.setting.AutoConnectStamVR;

                ActivationRadius.Value = App.setting.ActivationRadius;
                UseRightEye.IsChecked = App.setting.UseRightEye;
                FrameOffset.Value = App.setting.FrameOffset;

                OverlayEnable.IsChecked = App.setting.OverlayEnable;
                OverlayHigh.Value = App.setting.OverlayHigh;
                OverlayDistance.Value = App.setting.OverlayDistance;
                OverlayScale.Value = App.setting.OverlayScale;
                OverlayScrollSpeed.Value = App.setting.OverlayScrollSpeed;
                OverlayCurve.Value = App.setting.OverlayCurve;

                UseHmdRotations.IsChecked = App.setting.UseHmdRotations;
                HmdRotationThreshold.Value = App.setting.HmdRotationThreshold;
            }

            Loaded += (s, e) =>
            {
                UpdateStatusUI();

                FrameShot?.OnStateUpdate += FrameShot_OnStateUpdate;
            };

            Unloaded += (s, e) =>
            {
                FrameShot?.OnStateUpdate -= FrameShot_OnStateUpdate;
            };
        }

        private static void InitializeFrameShot()
        {
            if (FrameShot != null) return;

            FrameShot = new FrameShotService(msg => System.Diagnostics.Debug.WriteLine($"[FrameShot] {msg}"));
            FrameShot.OnPhotoSaved += (image, triggerHeld) =>
            {
                App.captureWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    App.captureWindow.StartCaptureScreen(image, triggerHeld);
                }));
            };
        }

        public static void AutoConnectSteamVR()
        {
            Task.Run(async () =>
            {
                while (App.setting.AutoConnectStamVR)
                {
                    if (FrameShot == null || !FrameShot.IsConnected)
                        TryConnect();

                    await Task.Delay(5000);
                }
            });
        }

        private void FrameShot_OnStateUpdate(object state)
        {
            StatusButton.IsEnabled = false;
            Dispatcher.Invoke(UpdateStatusUI);
        }

        private void UpdateStatusUI()
        {
            if (FrameShot?.IsConnected == true)
            {
                StatusDot.Fill = Brushes.Green;
                StatusText.Text = "Connected";
                StatusButton.Content = "Disconnect";
            }
            else
            {
                StatusDot.Fill = Brushes.Red;
                StatusText.Text = FrameShot?.LastError ?? "Not Connected";
                StatusButton.Content = "Connect to SteamVR";

                FrameShot?.Dispose();
                SteamOverlay?.Dispose();
            }

            StatusButton.IsEnabled = true;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            StatusButton.IsEnabled = false;

            if (FrameShot?.IsConnected == true)
                TryDisconnect();
            else
                TryConnect();
        }

        private static void TryConnect()
        {
            App.captureWindow.Dispatcher.BeginInvoke(new Action(async () =>
            {
                InitializeFrameShot();

                await Task.Delay(100);

                if (FrameShot!.Connect())
                {
                    if (App.setting.OverlayEnable)
                        SteamOverlay = new SteamOverlayService();
                }
                else
                    SnackbarHost.Show("FrameShot Error", $"SteamVR Connection Failed:\n{FrameShot.LastError}", type: SnackbarType.Error);
            }));
        }

        private static void TryDisconnect()
        {
            App.captureWindow.Dispatcher.BeginInvoke(new Action(async () =>
            {
                await Task.Delay(100);

                if (FrameShot?.IsConnected == true)
                {
                    FrameShot.Disconnect();
                }
            }));
        }

        private void AutoConnectStamVR_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                App.setting.AutoConnectStamVR = AutoConnectStamVR.IsChecked == true;
        }


        //?? General Settings
        private void ActivationRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.ActivationRadius = (int)e.NewValue;
        }

        private void UseRightEye_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                if (FrameShot?.IsConnected == true)
                    TryDisconnect();

                App.setting.UseRightEye = UseRightEye.IsChecked == true;
            }
        }

        private void FrameOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.FrameOffset = (int)e.NewValue;
        }


        //?? Overlay Settings
        private void OverlayEnable_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                if (FrameShot?.IsConnected == true)
                    TryDisconnect();

                App.setting.OverlayEnable = OverlayEnable.IsChecked == true;
            }
        }

        private void OverlayHigh_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.OverlayHigh = (float)e.NewValue;
        }

        private void OverlayDistance_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.OverlayDistance = (float)e.NewValue;
        }

        private void OverlayScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.OverlayScale = (float)e.NewValue;
        }

        private void OverlayScrollSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.OverlayScrollSpeed = (int)e.NewValue;
        }

        private void OverlayCurve_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.OverlayCurve = (int)e.NewValue;
        }

        //?? Rotation Settings
        private void HmdRotCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                App.setting.UseHmdRotations = UseHmdRotations.IsChecked == true;
        }

        private void HmdRotationThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
                App.setting.HmdRotationThreshold = (float)e.NewValue;
        }
    }
}