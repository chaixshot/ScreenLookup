using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

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

        // --- Persistent Position Anchoring Fields ---
        private HmdMatrix34_t cachedAnchorTransform;
        private bool hasAnchorTransform = false;
        private float lastMetersPerPixel = 0f;

        // --- Absolute Virtual Canvas Positioning Buffers ---
        private int _cachedMinLeft = 0;
        private int _cachedMinTop = 0;
        private int _cachedCompositeHeight = 0;

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
                hasAnchorTransform = false; // Forces it to recalculate its anchor relative to where the head is looking right now
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

        /// <summary>
        /// Recalculates the position shift required to keep the main target window locked to a persistent
        /// world-space anchor point, while allowing the texture composition canvas to expand dynamically.
        /// </summary>
        private void UpdateOverlayTransform(float pixelShiftX, float pixelShiftY, float metersPerPixel)
        {
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            // Establish the locked position reference if it hasn't been cached yet
            if (!hasAnchorTransform)
            {
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
                float fZ = rawForwardZ / horizontalLength;

                float rX = -fZ;
                float rZ = fX;

                // Cache baseline orientation exactly 2 meters in front of user's face alignment
                cachedAnchorTransform = new HmdMatrix34_t()
                {
                    m0 = rX,
                    m1 = 0f,
                    m2 = -fX,
                    m3 = hmdMatrix.m3 + (fX * 2f),
                    m4 = 0f,
                    m5 = 1f,
                    m6 = 0f,
                    m7 = hmdMatrix.m7,
                    m8 = rZ,
                    m9 = 0f,
                    m10 = -fZ,
                    m11 = hmdMatrix.m11 + (fZ * 2f)
                };
                hasAnchorTransform = true;
            }

            // Map structural layout variance out into physical meters
            float vrShiftX = pixelShiftX * metersPerPixel;
            float vrShiftY = -pixelShiftY * metersPerPixel; // Reverse Y axis vector conversion directions

            // Compute frame offset drift transformations applied specifically to the layout direction vector components
            HmdMatrix34_t adjustedTransform = cachedAnchorTransform;

            adjustedTransform.m3 += (cachedAnchorTransform.m0 * vrShiftX);
            adjustedTransform.m7 += (cachedAnchorTransform.m4 * vrShiftX) + (cachedAnchorTransform.m5 * vrShiftY);
            adjustedTransform.m11 += (cachedAnchorTransform.m8 * vrShiftX);

            OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref adjustedTransform);
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
                        // OpenVR mouse events pass absolute coordinates native to the 
                        // composite dimensions set via SetOverlayMouseScale.
                        float vrX = vrEvent.data.mouse.x;
                        float vrY = vrEvent.data.mouse.y;

                        // SteamVR tracks 0,0 from the bottom-left of textures. 
                        // Invert the Y coordinates relative to our current composite height scale.
                        int screenX = _cachedMinLeft + (int)vrX;
                        int screenY = _cachedMinTop + (_cachedCompositeHeight - (int)vrY);

                        SetCursorPos(screenX, screenY);
                        isOverlayDirty = true;
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
                                doublePressCts = new CancellationTokenSource();

                                // Start an async timeout window without blocking the main thread
                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        await Task.Delay(300, doublePressCts.Token);
                                        isWaitingForSecondPress = false;
                                        targetWindow.Dispatcher.Invoke(() => App.captureWindow.HideWindow());
                                    }
                                    catch (TaskCanceledException) { }
                                });
                            }
                            else // Double press re-center
                            {
                                isWaitingForSecondPress = false;

                                doublePressCts?.Cancel();
                                doublePressCts = new CancellationTokenSource();

                                Task.Run(async () =>
                                {
                                    try
                                    {
                                        AppUtilities.PlaySound("ready.wav");
                                        await Task.Delay(1000, doublePressCts.Token);

                                        // Reset anchor token flags so that the render system re-snaps perspective down onto head projection coordinates
                                        hasAnchorTransform = false;
                                        isOverlayDirty = true;
                                    }
                                    catch (TaskCanceledException) { }
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

            // Gather all windows and determine the absolute minimum and maximum screen bounds
            bool isVisible = false;
            RECT mainRect = new();
            var popupWindows = new List<(IntPtr Handle, RECT Rect)>();

            targetWindow.Dispatcher.Invoke(() =>
            {
                isVisible = targetWindow.IsVisible;
                if (isVisible)
                {
                    GetWindowRect(targetHwnd, out mainRect);

                    // Find all active popup sources belonging to this dispatcher
                    foreach (PresentationSource source in PresentationSource.CurrentSources)
                    {
                        if (source is HwndSource hwndSource && hwndSource.Handle != targetHwnd &&
                            hwndSource.RootVisual is FrameworkElement element && element.IsVisible &&
                            element.GetType().Name == "PopupRoot")
                        {
                            if (GetWindowRect(hwndSource.Handle, out RECT pRect))
                            {
                                int pW = pRect.Right - pRect.Left;
                                int pH = pRect.Bottom - pRect.Top;
                                if (pW > 0 && pH > 0)
                                {
                                    popupWindows.Add((hwndSource.Handle, pRect));
                                }
                            }
                        }
                    }
                }
            });

            if (!isVisible) return;

            // Compute the composite virtual bounding box (Union of main window + all popups)
            int minLeft = mainRect.Left;
            int minTop = mainRect.Top;
            int maxRight = mainRect.Right;
            int maxBottom = mainRect.Bottom;

            foreach (var popup in popupWindows)
            {
                if (popup.Rect.Left < minLeft) minLeft = popup.Rect.Left;
                if (popup.Rect.Top < minTop) minTop = popup.Rect.Top;
                if (popup.Rect.Right > maxRight) maxRight = popup.Rect.Right;
                if (popup.Rect.Bottom > maxBottom) maxBottom = popup.Rect.Bottom;
            }

            int compositeWidth = maxRight - minLeft;
            int compositeHeight = maxBottom - minTop;

            if (compositeWidth <= 0 || compositeHeight <= 0) return;

            // Cache global layout positioning attributes cleanly across threads
            _cachedMinLeft = minLeft;
            _cachedMinTop = minTop;
            _cachedCompositeHeight = compositeHeight;

            // Track dirty flags if dimensions change due to an expanding flyout
            if (sharedCaptureBmp == null || sharedCaptureBmp.Width != compositeWidth || sharedCaptureBmp.Height != compositeHeight)
            {
                isOverlayDirty = true;
            }

            // --- ALL SCALE AND CORRECTION CALCULATIONS CALLED CONTINUOUSLY ---
            int mainWindowWidth = mainRect.Right - mainRect.Left;
            if (mainWindowWidth <= 0) mainWindowWidth = 1;

            float mainWindowTargetWidthInMeters = 2f;
            bool menuVisible = false;
            targetWindow.Dispatcher.Invoke(() => menuVisible = App.captureWindow.configMenu.IsVisible);
            if (menuVisible) mainWindowTargetWidthInMeters = 1f;

            // Establish exact spatial conversion density profile scaling structures 
            float metersPerPixel = mainWindowTargetWidthInMeters / mainWindowWidth;
            float widthInMeters = compositeWidth * metersPerPixel;

            overlay.SetOverlayWidthInMeters(overlayHandle, widthInMeters);

            // Map mouse coordinates scaling using the newly computed composite viewport size
            HmdVector2_t mouseScale = new() { v0 = (float)compositeWidth, v1 = (float)compositeHeight };
            overlay.SetOverlayMouseScale(overlayHandle, ref mouseScale);

            // Calculate current texture center drifts from the target anchor tracking midpoint bounds
            float mainWindowCenterPx = mainRect.Left + (mainWindowWidth / 2f);
            float compositeCenterPx = minLeft + (compositeWidth / 2f);
            float pixelShiftX = compositeCenterPx - mainWindowCenterPx;

            int mainWindowHeight = mainRect.Bottom - mainRect.Top;
            float mainWindowCenterPy = mainRect.Top + (mainWindowHeight / 2f);
            float compositeCenterPy = minTop + (compositeHeight / 2f);
            float pixelShiftY = compositeCenterPy - mainWindowCenterPy;

            // If layout frame is dirty or scale factor adjusted, sync runtime transformation positioning matrix modifications
            if (isOverlayDirty || lastMetersPerPixel != metersPerPixel)
            {
                UpdateOverlayTransform(pixelShiftX, pixelShiftY, metersPerPixel);
                lastMetersPerPixel = metersPerPixel;
            }

            if (!isOverlayDirty) return;

            try
            {
                // Manage persistent buffers based on the dynamic composite canvas size
                if (sharedCaptureBmp == null || sharedCaptureBmp.Width != compositeWidth || sharedCaptureBmp.Height != compositeHeight)
                {
                    sharedCaptureGraphics?.Dispose();
                    sharedCaptureBmp?.Dispose();

                    sharedCaptureBmp = new Bitmap(compositeWidth, compositeHeight, PixelFormat.Format32bppPArgb);
                    sharedCaptureGraphics = Graphics.FromImage(sharedCaptureBmp);
                }

                // Clear canvas with complete transparency to prepare for shifted overlay compositions
                sharedCaptureGraphics.Clear(Color.Transparent);

                // Capture and compose inside the UI Thread Context
                targetWindow.Dispatcher.Invoke(() =>
                {
                    // Draw main window relative to the composite virtual canvas origin (minLeft, minTop)
                    IntPtr hdc = sharedCaptureGraphics!.GetHdc();
                    PrintWindow(targetHwnd, hdc, 0x02);
                    sharedCaptureGraphics.ReleaseHdc(hdc);

                    // If the canvas is expanded upwards or leftwards, adjust the placement position of the main window screenshot
                    int mainOffsetX = mainRect.Left - minLeft;
                    int mainOffsetY = mainRect.Top - minTop;

                    if (mainOffsetX != 0 || mainOffsetY != 0)
                    {
                        // Shift the captured window onto its correct spot inside our larger virtual canvas
                        using (Bitmap mainTemp = new Bitmap(mainRect.Right - mainRect.Left, mainRect.Bottom - mainRect.Top, PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics gM = Graphics.FromImage(mainTemp))
                            {
                                IntPtr hdcM = gM.GetHdc();
                                PrintWindow(targetHwnd, hdcM, 0x02);
                                gM.ReleaseHdc(hdcM);
                            }
                            sharedCaptureGraphics.Clear(Color.Transparent);
                            sharedCaptureGraphics.DrawImage(mainTemp, mainOffsetX, mainOffsetY);
                        }
                    }

                    // Draw popups relative to the virtual canvas origin
                    foreach (var popup in popupWindows)
                    {
                        int pW = popup.Rect.Right - popup.Rect.Left;
                        int pH = popup.Rect.Bottom - popup.Rect.Top;

                        using (Bitmap popupBmp = new Bitmap(pW, pH, PixelFormat.Format32bppPArgb))
                        {
                            using (Graphics gP = Graphics.FromImage(popupBmp))
                            {
                                IntPtr hdcP = gP.GetHdc();
                                PrintWindow(popup.Handle, hdcP, 0x02);
                                gP.ReleaseHdc(hdcP);
                            }

                            // Flyout corner processing block
                            BitmapData pData = popupBmp.LockBits(new Rectangle(0, 0, pW, pH), ImageLockMode.ReadWrite, popupBmp.PixelFormat);
                            unsafe
                            {
                                int pRadius = 8;
                                for (int y = 0; y < pH; y++)
                                {
                                    byte* pRowPtr = (byte*)pData.Scan0 + (y * pData.Stride);
                                    for (int x = 0; x < pW; x++)
                                    {
                                        int offset = x * 4;
                                        bool insideCornerZone = false;
                                        int cx = 0, cy = 0;

                                        if (x < pRadius && y < pRadius) { insideCornerZone = true; cx = pRadius - 1; cy = pRadius - 1; }
                                        else if (x >= pW - pRadius && y < pRadius) { insideCornerZone = true; cx = pW - pRadius; cy = pRadius - 1; }
                                        else if (x < pRadius && y >= pH - pRadius) { insideCornerZone = true; cx = pRadius - 1; cy = pH - pRadius; }
                                        else if (x >= pW - pRadius && y >= pH - pRadius) { insideCornerZone = true; cx = pW - pRadius; cy = pH - pRadius; }

                                        if (insideCornerZone)
                                        {
                                            int dx = x - cx;
                                            int dy = y - cy;
                                            if ((dx * dx) + (dy * dy) > (pRadius * pRadius))
                                            {
                                                pRowPtr[offset + 0] = 0;
                                                pRowPtr[offset + 1] = 0;
                                                pRowPtr[offset + 2] = 0;
                                                pRowPtr[offset + 3] = 0;
                                                continue;
                                            }
                                        }

                                        if (pRowPtr[offset + 3] == 255 && pRowPtr[offset + 2] == 0 && pRowPtr[offset + 1] == 0 && pRowPtr[offset + 0] == 0)
                                        {
                                            pRowPtr[offset + 3] = 0;
                                        }
                                    }
                                }
                            }
                            popupBmp.UnlockBits(pData);

                            // Composite popups onto the master canvas based on the computed offset anchor
                            sharedCaptureGraphics.DrawImage(popupBmp, popup.Rect.Left - minLeft, popup.Rect.Top - minTop);
                        }
                    }
                });

                // Direct3D Hardware Copy Block
                lock (d3dLock)
                {
                    if (overlayTex == null || overlayTex.Description.Width != (uint)compositeWidth || overlayTex.Description.Height != (uint)compositeHeight)
                    {
                        overlayTex?.Dispose();
                        stagingTex?.Dispose();

                        Texture2DDescription desc = new()
                        {
                            Width = (uint)compositeWidth,
                            Height = (uint)compositeHeight,
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

                    MappedSubresource box = d3dContext!.Map(stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                    BitmapData bData = sharedCaptureBmp.LockBits(new Rectangle(0, 0, compositeWidth, compositeHeight), ImageLockMode.ReadOnly, sharedCaptureBmp.PixelFormat);

                    unsafe
                    {
                        int cornerRadius = 12;

                        for (int y = 0; y < compositeHeight; y++)
                        {
                            byte* sourceRowPtr = (byte*)bData.Scan0 + (y * bData.Stride);
                            byte* destRowPtr = (byte*)box.DataPointer + (y * box.RowPitch);

                            for (int x = 0; x < compositeWidth; x++)
                            {
                                int pixelOffset = x * 4;
                                bool insideCornerZone = false;
                                int cx = 0, cy = 0;

                                if (x < cornerRadius && y < cornerRadius) { insideCornerZone = true; cx = cornerRadius - 1; cy = cornerRadius - 1; }
                                else if (x >= compositeWidth - cornerRadius && y < cornerRadius) { insideCornerZone = true; cx = compositeWidth - cornerRadius; cy = cornerRadius - 1; }
                                else if (x < cornerRadius && y >= compositeHeight - cornerRadius) { insideCornerZone = true; cx = cornerRadius - 1; cy = compositeHeight - cornerRadius; }
                                else if (x >= compositeWidth - cornerRadius && y >= compositeHeight - cornerRadius) { insideCornerZone = true; cx = compositeWidth - cornerRadius; cy = compositeHeight - cornerRadius; }

                                if (insideCornerZone)
                                {
                                    int dx = x - cx;
                                    int dy = y - cy;
                                    if ((dx * dx) + (dy * dy) > (cornerRadius * cornerRadius))
                                    {
                                        destRowPtr[pixelOffset + 0] = 0;
                                        destRowPtr[pixelOffset + 1] = 0;
                                        destRowPtr[pixelOffset + 2] = 0;
                                        destRowPtr[pixelOffset + 3] = 0;
                                        continue;
                                    }
                                }

                                byte b = sourceRowPtr[pixelOffset + 0];
                                byte g = sourceRowPtr[pixelOffset + 1];
                                byte r = sourceRowPtr[pixelOffset + 2];
                                byte a = sourceRowPtr[pixelOffset + 3];

                                if (a == 255 && r == 0 && g == 0 && b == 0)
                                {
                                    destRowPtr[pixelOffset + 0] = 0;
                                    destRowPtr[pixelOffset + 1] = 0;
                                    destRowPtr[pixelOffset + 2] = 0;
                                    destRowPtr[pixelOffset + 3] = 0;
                                }
                                else
                                {
                                    destRowPtr[pixelOffset + 0] = (byte)(b * a / 255);
                                    destRowPtr[pixelOffset + 1] = (byte)(g * a / 255);
                                    destRowPtr[pixelOffset + 2] = (byte)(r * a / 255);
                                    destRowPtr[pixelOffset + 3] = a;
                                }
                            }
                        }
                    }

                    sharedCaptureBmp.UnlockBits(bData);
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