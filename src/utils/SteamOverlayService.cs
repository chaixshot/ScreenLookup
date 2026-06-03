using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Point = System.Windows.Point;

namespace ScreenLookup.src.utils
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public class SteamOverlayService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private ulong overlayHandle = OpenVR.k_ulOverlayHandleInvalid;
        private bool isInitialized = false;

        private ID3D11Device? d3dDevice;
        private ID3D11DeviceContext? d3dContext;
        private ID3D11Texture2D? overlayTex;
        private ID3D11Texture2D? stagingTex;

        private readonly object d3dLock = new();
        private Window targetWindow;

        private CancellationTokenSource? cts;
        private Task? pollTask;
        private bool running;
        private IntPtr targetHwnd = IntPtr.Zero;

        private bool isOverlayDirty = true;
        private byte[]? rowBuffer;

        public bool IsInitialized => isInitialized;

        public SteamOverlayService()
        {
            // Initialize OpenVR context and register the overlay handle safely
            if (Initialize())
            {
                SetWindow();
                SetVisible(false);

                // Kick off background polling and graphics work
                StartPolling();
            }
        }

        public void SetVisible(bool visible)
        {
            CVROverlay overlay = OpenVR.Overlay;
            if (overlay == null) return;

            if (visible)
            {
                isOverlayDirty = true;
                UpdateOverlayTransform(0f, 0f, 2f);
                overlay.ShowOverlay(overlayHandle);
            }
            else
                overlay.HideOverlay(overlayHandle);
        }

        public void SetWindow()
        {
            targetWindow = App.captureWindow;
            targetHwnd = new WindowInteropHelper(targetWindow).Handle;

            targetWindow.LayoutUpdated += (s, e) =>
            {
                isOverlayDirty = true;
            };
        }

        private bool Initialize()
        {
            if (OpenVR.System == null)
            {
                EVRInitError initError = EVRInitError.None;
                OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Overlay);

                if (initError != EVRInitError.None)
                {
                    System.Diagnostics.Debug.WriteLine($"[SteamVR] Initialization failed: {initError}");
                    return false;
                }
            }

            CVROverlay overlay = OpenVR.Overlay;
            if (overlay == null) return false;

            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out d3dDevice, out d3dContext);

            EVROverlayError overlayError = overlay.CreateOverlay("ScreenLookup.WorldOverlay", "ScreenLookup Floating Window", ref overlayHandle);
            if (overlayError != EVROverlayError.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] World overlay creation failed: {overlayError}");
                return false;
            }

            // Establish core tracking pipeline behaviors
            overlay.SetOverlayInputMethod(overlayHandle, VROverlayInputMethod.Mouse);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.ShowTouchPadScrollWheel, true);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.IsPremultiplied, true);

            return isInitialized = true;
        }

        public void UpdateOverlayTransform(float offsetX, float offsetY, float offsetZ)
        {
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            // Get the current HMD (Headset) pose from OpenVR
            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses);

            // HMD is always device index 0
            TrackedDevicePose_t hmdPose = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];

            if (!hmdPose.bPoseIsValid) return; // Don't update if tracking is lost

            HmdMatrix34_t hmdMatrix = hmdPose.mDeviceToAbsoluteTracking;

            // Extract the raw looking direction vector from the HMD matrix
            float rawForwardX = -hmdMatrix.m2;
            float rawForwardZ = -hmdMatrix.m10;

            // Project onto the horizontal plane (X/Z) and normalize to remove pitch/roll
            float horizontalLength = (float)Math.Sqrt(rawForwardX * rawForwardX + rawForwardZ * rawForwardZ);

            // Safety check in case the user is looking perfectly straight up or down
            if (horizontalLength < 0.001f)
            {
                // Use default fallback forward vector if tracking vector collapses
                rawForwardX = 0f;
                rawForwardZ = -1f;
                horizontalLength = 1f;
            }

            // Cleaned horizontal forward direction
            float fX = rawForwardX / horizontalLength;
            float fY = 0f; // Force vertical forward movement to zero
            float fZ = rawForwardZ / horizontalLength;

            // Calculate a stable right vector perpendicular to our clean forward vector and the true world up (0, 1, 0)
            float rX = -fZ;
            float rY = 0f;
            float rZ = fX;

            // True world Up vector (Keeps the overlay vertically straight)
            float uX = 0f;
            float uY = 1f;
            float uZ = 0f;

            // Calculate the position in front of the HMD using our flattened coordinate system
            float posX = hmdMatrix.m3 + (rX * offsetX) + (uX * offsetY) + (fX * offsetZ);
            float posY = hmdMatrix.m7 + (rY * offsetX) + (uY * offsetY) + (fY * offsetZ); // Modifies height relative to world horizon
            float posZ = hmdMatrix.m11 + (rZ * offsetX) + (uZ * offsetY) + (fZ * offsetZ);

            // Build the final transform matrix using our cleaned horizontal alignment vectors
            HmdMatrix34_t transform = new()
            {
                // Right Vector
                m0 = rX,
                m4 = rY,
                m8 = rZ,

                // Up Vector (Locked straight up to the world ceiling)
                m1 = uX,
                m5 = uY,
                m9 = uZ,

                // Forward Vector
                m2 = -fX,  // OpenVR expects negated forward vector components for matrix calculation
                m6 = -fY,
                m10 = -fZ,

                // Apply the calculated absolute position
                m3 = posX,
                m7 = posY,
                m11 = posZ
            };

            // Send to OpenVR
            OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform);
        }

        #region Polling Architecture Loop
        private void StartPolling()
        {
            if (running) return;

            cts = new CancellationTokenSource();
            running = true;
            pollTask = PollLoopAsync(cts.Token);
        }

        private void StopPolling()
        {
            running = false;
            cts?.Cancel();
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ProcessInput();
                // Direct UI execution frame dispatch synchronization via UI Thread Dispatcher
                targetWindow.Dispatcher.Invoke(RenderFrame);

                await Task.Delay(16, ct); // Cap around ~60Hz
            }
        }
        #endregion

        public void ProcessInput()
        {
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            bool isVisible = false;
            targetWindow.Dispatcher.Invoke(() => isVisible = targetWindow.IsVisible);
            if (!isVisible) return;

            VREvent_t vrEvent = new();
            uint eventSize = (uint)Marshal.SizeOf<VREvent_t>();

            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                switch ((EVREventType)vrEvent.eventType)
                {
                    case EVREventType.VREvent_MouseMove:
                        targetWindow.Dispatcher.Invoke(() =>
                        {
                            if (isVisible)
                            {
                                // Flip coordinate space tracking back onto desktop layout directions
                                double correctedY = targetWindow.ActualHeight - vrEvent.data.mouse.y;
                                Point screenPoint = targetWindow.PointToScreen(new Point(vrEvent.data.mouse.x, correctedY));
                                SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
                                isOverlayDirty = true;
                            }
                        });
                        break;

                    case EVREventType.VREvent_MouseButtonDown:
                        if (vrEvent.data.mouse.button == (uint)EVRMouseButton.Left)
                        {
                            targetWindow.Dispatcher.Invoke(() => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0));
                            isOverlayDirty = true;
                        }
                        break;

                    case EVREventType.VREvent_MouseButtonUp:
                        if (vrEvent.data.mouse.button == (uint)EVRMouseButton.Left)
                        {
                            targetWindow.Dispatcher.Invoke(() => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0));
                            isOverlayDirty = true;
                        }
                        break;
                }
            }
        }

        public void RenderFrame()
        {
            CVROverlay overlay = OpenVR.Overlay;
            if (overlay == null || !isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            bool isVisible = false;
            targetWindow.Dispatcher.Invoke(() => isVisible = targetWindow.IsVisible);

            if (isVisible)
            {
                // If there are no dirty state adjustments pending, keep previous payload active inside compositor memory
                if (!isOverlayDirty) return;

                try
                {
                    double uiWidth = 0, uiHeight = 0;
                    RECT rect = new();

                    targetWindow.Dispatcher.Invoke(() =>
                    {
                        GetWindowRect(targetHwnd, out rect);
                        uiWidth = rect.Right - rect.Left;
                        uiHeight = rect.Bottom - rect.Top;
                    });

                    if (uiWidth <= 0 || uiHeight <= 0) return;

                    using Bitmap captureBmp = new((int)uiWidth, (int)uiHeight, PixelFormat.Format32bppPArgb);
                    using (Graphics g = Graphics.FromImage(captureBmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        targetWindow.Dispatcher.Invoke(() => PrintWindow(targetHwnd, hdc, 0x02));
                        g.ReleaseHdc(hdc);

                        // Window pop-up compositing iteration mapping logic
                        try
                        {
                            targetWindow.Dispatcher.Invoke(() =>
                            {
                                foreach (PresentationSource source in PresentationSource.CurrentSources)
                                {
                                    if (source is HwndSource hwndSource && hwndSource.Handle != targetHwnd &&
                                        hwndSource.RootVisual is FrameworkElement element && element.IsVisible &&
                                        element.GetType().Name == "PopupRoot")
                                    {
                                        GetWindowRect(hwndSource.Handle, out RECT pRect);
                                        int pW = pRect.Right - pRect.Left;
                                        int pH = pRect.Bottom - pRect.Top;

                                        if (pW > 0 && pH > 0)
                                        {
                                            using Bitmap popupBmp = new(pW, pH, PixelFormat.Format32bppPArgb);
                                            using (Graphics gP = Graphics.FromImage(popupBmp))
                                            {
                                                IntPtr hdcP = gP.GetHdc();
                                                PrintWindow(hwndSource.Handle, hdcP, 0x02);
                                                gP.ReleaseHdc(hdcP);
                                            }
                                            g.DrawImage(popupBmp, pRect.Left - rect.Left, pRect.Top - rect.Top);
                                        }
                                    }
                                }
                            });
                        }
                        catch { /* Thread protection guard against changing framework target visual trees */ }
                    }

                    int canvasWidth = (int)Math.Round(uiWidth);
                    int canvasHeight = (int)Math.Round(uiHeight);
                    float widthInMeters = 2f;

                    if (uiHeight > uiWidth)
                        widthInMeters *= ((float)(uiWidth / (float)uiHeight));

                    if (App.captureWindow.configMenu.IsVisible)
                        widthInMeters = 1f;

                    overlay.SetOverlayWidthInMeters(overlayHandle, widthInMeters);

                    HmdVector2_t mouseScale = new() { v0 = (float)uiWidth, v1 = (float)uiHeight };
                    overlay.SetOverlayMouseScale(overlayHandle, ref mouseScale);

                    lock (d3dLock)
                    {
                        if (overlayTex == null || overlayTex.Description.Width != (uint)canvasWidth || overlayTex.Description.Height != (uint)canvasHeight)
                        {
                            overlayTex?.Dispose();
                            stagingTex?.Dispose();

                            Texture2DDescription desc = new()
                            {
                                Width = (uint)canvasWidth,
                                Height = (uint)canvasHeight,
                                MipLevels = 1,
                                ArraySize = 1,
                                Format = Format.B8G8R8A8_UNorm,
                                SampleDescription = new SampleDescription(1, 0),
                                Usage = ResourceUsage.Default,
                                BindFlags = BindFlags.ShaderResource
                            };
                            overlayTex = d3dDevice!.CreateTexture2D(desc);

                            desc.Usage = ResourceUsage.Staging;
                            desc.BindFlags = BindFlags.None;
                            desc.CPUAccessFlags = CpuAccessFlags.Write;
                            stagingTex = d3dDevice.CreateTexture2D(desc);
                            rowBuffer = new byte[canvasWidth * 4];
                        }

                        MappedSubresource box = d3dContext!.Map(stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);

                        using Bitmap scaledBmp = new(canvasWidth, canvasHeight, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(scaledBmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(captureBmp, 0, 0, canvasWidth, canvasHeight);
                        }

                        BitmapData? bData = scaledBmp.LockBits(new Rectangle(0, 0, canvasWidth, canvasHeight), ImageLockMode.ReadOnly, scaledBmp.PixelFormat);
                        for (int y = 0; y < canvasHeight; y++)
                        {
                            Marshal.Copy(bData.Scan0 + y * bData.Stride, rowBuffer!, 0, canvasWidth * 4);
                            Marshal.Copy(rowBuffer!, 0, box.DataPointer + (nint)((long)y * box.RowPitch), canvasWidth * 4);
                        }
                        scaledBmp.UnlockBits(bData);

                        d3dContext.Unmap(stagingTex!, 0);
                        d3dContext.CopyResource(overlayTex!, stagingTex!);

                        Texture_t tex = new()
                        {
                            handle = overlayTex!.NativePointer,
                            eType = ETextureType.DirectX,
                            eColorSpace = EColorSpace.Auto
                        };

                        overlay.SetOverlayTexture(overlayHandle, ref tex);
                        isOverlayDirty = false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SteamVR] Frame rendering crashed: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (isInitialized)
            {
                StopPolling();

                lock (d3dLock)
                {
                    CVROverlay overlay = OpenVR.Overlay;
                    if (overlay != null && overlayHandle != OpenVR.k_ulOverlayHandleInvalid)
                    {
                        overlay.DestroyOverlay(overlayHandle);
                    }

                    overlayTex?.Dispose();
                    stagingTex?.Dispose();
                    d3dContext?.Dispose();
                    d3dDevice?.Dispose();
                }

                overlayHandle = OpenVR.k_ulOverlayHandleInvalid;
                isInitialized = false;
            }
        }
    }
}