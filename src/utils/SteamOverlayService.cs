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
        private IntPtr targetHwnd = IntPtr.Zero;

        private CancellationTokenSource? cts;
        private Task? pollTask;
        private bool running;
        private readonly VRInputService inputService;

        private bool isOverlayDirty = true;

        // Persistent reusable buffers to eliminate GC churn completely
        private Bitmap? sharedCaptureBmp;
        private Graphics? sharedCaptureGraphics;

        public bool IsInitialized => isInitialized;

        public SteamOverlayService()
        {
            if (Initialize())
            {
                inputService = new VRInputService();

                SetWindow();
                SetVisible(false);
                StartPolling();

                targetWindow.LayoutUpdated += (s, e) =>
                {
                    isOverlayDirty = true;
                };

                targetWindow.IsVisibleChanged += (s, e) =>
                {
                    if (targetWindow.IsVisible)
                        SetVisible(true);
                    else
                        SetVisible(false);
                };
            }
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

            overlay.SetOverlayInputMethod(overlayHandle, VROverlayInputMethod.Mouse);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.ShowTouchPadScrollWheel, true);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, true);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.IsPremultiplied, true);

            return isInitialized = true;
        }

        private void SetVisible(bool visible)
        {
            CVROverlay overlay = OpenVR.Overlay;
            if (overlay == null) return;

            if (visible)
            {
                isOverlayDirty = true;
                UpdateOverlayTransform(0f, 0f, 2f);
                SetWindow();
                overlay.ShowOverlay(overlayHandle);
            }
            else
            {
                overlay.HideOverlay(overlayHandle);
            }
        }

        private void SetWindow()
        {
            targetWindow = App.captureWindow;
            targetHwnd = new WindowInteropHelper(targetWindow).Handle;
        }

        private void UpdateOverlayTransform(float offsetX, float offsetY, float offsetZ)
        {
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses);

            TrackedDevicePose_t hmdPose = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];
            if (!hmdPose.bPoseIsValid) return;

            HmdMatrix34_t hmdMatrix = hmdPose.mDeviceToAbsoluteTracking;

            float rawForwardX = -hmdMatrix.m2;
            float rawForwardZ = -hmdMatrix.m10;
            float horizontalLength = (float)Math.Sqrt(rawForwardX * rawForwardX + rawForwardZ * rawForwardZ);

            if (horizontalLength < 0.001f)
            {
                rawForwardX = 0f;
                rawForwardZ = -1f;
                horizontalLength = 1f;
            }

            float fX = rawForwardX / horizontalLength;
            float fY = 0f;
            float fZ = rawForwardZ / horizontalLength;

            float rX = -fZ;
            float rY = 0f;
            float rZ = fX;

            float uX = 0f;
            float uY = 1f;
            float uZ = 0f;

            float posX = hmdMatrix.m3 + (rX * offsetX) + (uX * offsetY) + (fX * offsetZ);
            float posY = hmdMatrix.m7 + (rY * offsetX) + (uY * offsetY) + (fY * offsetZ);
            float posZ = hmdMatrix.m11 + (rZ * offsetX) + (uZ * offsetY) + (fZ * offsetZ);

            HmdMatrix34_t transform = new()
            {
                m0 = rX,
                m4 = rY,
                m8 = rZ,
                m1 = uX,
                m5 = uY,
                m9 = uZ,
                m2 = -fX,
                m6 = -fY,
                m10 = -fZ,
                m3 = posX,
                m7 = posY,
                m11 = posZ
            };

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
                RenderFrame();

                await Task.Delay(16, ct); // Targets roughly ~60Hz
            }
        }
        #endregion

        private bool isWaitingForSecondPress = false;
        private CancellationTokenSource? doublePressCts;
        private void ProcessInput()
        {
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            VREvent_t vrEvent = new();
            uint eventSize = (uint)Marshal.SizeOf<VREvent_t>();

            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                uint button = vrEvent.data.controller.button;

                switch ((EVREventType)vrEvent.eventType)
                {
                    case EVREventType.VREvent_MouseMove:
                        targetWindow.Dispatcher.Invoke(() =>
                        {
                            if (targetWindow.IsVisible)
                            {
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

                    case EVREventType.VREvent_ButtonPress:
                        if (button == inputService.AButtonId)
                        {
                            if (!isWaitingForSecondPress) // HideWindow
                            {
                                isWaitingForSecondPress = true;

                                doublePressCts?.Cancel();
                                doublePressCts = new System.Threading.CancellationTokenSource();

                                // Start an async timeout window without blocking the main thread
                                Task.Run(async () =>
                                {
                                    await Task.Delay(300, doublePressCts.Token);

                                    // If we reach here, the timeout expired without a second press
                                    isWaitingForSecondPress = false;

                                    targetWindow.Dispatcher.Invoke(() => App.captureWindow.HideWindow());
                                });
                            }
                            else // Double press re-center
                            {
                                isWaitingForSecondPress = false;

                                doublePressCts?.Cancel();
                                doublePressCts = new System.Threading.CancellationTokenSource();

                                Task.Run(async () =>
                                {
                                    AppUtilities.PlaySound("ready.wav");
                                    await Task.Delay(1000, doublePressCts.Token);

                                    targetWindow.Dispatcher.Invoke(() => UpdateOverlayTransform(0f, 0f, 2f));
                                });
                            }
                        }
                        break;
                }
            }
        }

        private void RenderFrame()
        {
            CVROverlay overlay = OpenVR.Overlay;
            if (overlay == null || !isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            // Query necessary configuration markers from the UI Thread quickly
            int uiWidth = 0, uiHeight = 0;
            bool isVisible = false;
            RECT rect = new();

            targetWindow.Dispatcher.Invoke(() =>
            {
                isVisible = targetWindow.IsVisible;
                if (isVisible)
                {
                    GetWindowRect(targetHwnd, out rect);
                    uiWidth = rect.Right - rect.Left;
                    uiHeight = rect.Bottom - rect.Top;
                }
            });

            if (!isVisible || uiWidth <= 0 || uiHeight <= 0) return;
            if (!isOverlayDirty) return;

            try
            {
                // Manage persistent, zero-allocation buffers
                if (sharedCaptureBmp == null || sharedCaptureBmp.Width != uiWidth || sharedCaptureBmp.Height != uiHeight)
                {
                    sharedCaptureGraphics?.Dispose();
                    sharedCaptureBmp?.Dispose();

                    sharedCaptureBmp = new Bitmap(uiWidth, uiHeight, PixelFormat.Format32bppPArgb);
                    sharedCaptureGraphics = Graphics.FromImage(sharedCaptureBmp);
                    isOverlayDirty = true;
                }

                // Capture screen coordinates inside the required window dispatcher thread loop
                targetWindow.Dispatcher.Invoke(() =>
                {
                    IntPtr hdc = sharedCaptureGraphics!.GetHdc();
                    PrintWindow(targetHwnd, hdc, 0x02);
                    sharedCaptureGraphics.ReleaseHdc(hdc);

                    // Composite Popups manually onto the main bitmap payload context
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
                                sharedCaptureGraphics.DrawImage(popupBmp, pRect.Left - rect.Left, pRect.Top - rect.Top);
                            }
                        }
                    }
                });

                // Window meter calculation transformations logic
                float widthInMeters = 2f;
                if (uiHeight > uiWidth)
                    widthInMeters *= ((float)uiWidth / uiHeight);

                bool menuVisible = false;
                targetWindow.Dispatcher.Invoke(() => menuVisible = App.captureWindow.configMenu.IsVisible);
                if (menuVisible) widthInMeters = 1f;

                overlay.SetOverlayWidthInMeters(overlayHandle, widthInMeters);

                HmdVector2_t mouseScale = new() { v0 = (float)uiWidth, v1 = (float)uiHeight };
                overlay.SetOverlayMouseScale(overlayHandle, ref mouseScale);

                // Map resource context directly on the asynchronous task thread
                lock (d3dLock)
                {
                    if (overlayTex == null || overlayTex.Description.Width != (uint)uiWidth || overlayTex.Description.Height != (uint)uiHeight)
                    {
                        overlayTex?.Dispose();
                        stagingTex?.Dispose();

                        Texture2DDescription desc = new()
                        {
                            Width = (uint)uiWidth,
                            Height = (uint)uiHeight,
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
                    }

                    // Map the staging texture memory address 
                    MappedSubresource box = d3dContext!.Map(stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                    BitmapData bData = sharedCaptureBmp.LockBits(new Rectangle(0, 0, uiWidth, uiHeight), ImageLockMode.ReadOnly, sharedCaptureBmp.PixelFormat);

                    long linesize = uiWidth * 4;

                    // High-performance pointer arithmetic memory copy block
                    unsafe
                    {
                        for (int y = 0; y < uiHeight; y++)
                        {
                            byte* sourceRowPtr = (byte*)bData.Scan0 + (y * bData.Stride);
                            byte* destRowPtr = (byte*)box.DataPointer + (y * box.RowPitch);

                            Buffer.MemoryCopy(sourceRowPtr, destRowPtr, linesize, linesize);
                        }
                    }

                    sharedCaptureBmp.UnlockBits(bData);
                    d3dContext.Unmap(stagingTex!, 0);

                    // Hardware accelerated update copy
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
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Frame rendering encountered error: {ex.Message}");
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
                        overlay.DestroyOverlay(overlayHandle);

                    sharedCaptureGraphics?.Dispose();
                    sharedCaptureBmp?.Dispose();

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