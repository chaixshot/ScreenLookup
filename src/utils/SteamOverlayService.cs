using ScreenLookup.src.windows;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ScreenLookup.src.utils
{
    #region Native Win32 Structures
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }
    #endregion

    /// <summary>
    /// Service managing the floating 3D SteamVR overlay for ScreenLookup window rendering, laser pointers, and
    /// controller ray-casting.
    /// </summary>
    public class SteamOverlayService : IDisposable
    {
        #region Native Win32 Imports
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public short bmPlanes;
            public short bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, out BITMAP lpObject);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BITMAPINFOHEADER pbmi, uint iUsage, out IntPtr ppvBits, IntPtr hSection, uint dwOffset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        #endregion

        #region Private Fields
        private ulong overlayHandle = OpenVR.k_ulOverlayHandleInvalid;
        private ulong laserOverlayHandle = OpenVR.k_ulOverlayHandleInvalid;
        private bool isInitialized = false;
        private bool isVisible = false;

        private ID3D11Device? d3dDevice;
        private ID3D11DeviceContext? d3dContext;
        private ID3D11Texture2D? overlayTex;
        private ID3D11Texture2D? stagingTex;

        private ID3D11Texture2D? laserD3dTex;
        private ID3D11Texture2D? laserStagingTex;
        private int lastLaserTexHeight = 0;
        private float lastLaserLength = 0f;
        private float lastUpdateLengthTime = 0f;

        private readonly Lock d3dLock = new();
        private CaptureWindow targetWindow = null!;
        private IntPtr targetHwnd = IntPtr.Zero;

        private CancellationTokenSource? cts;
        private Task? processTask;
        private bool running;
        private readonly VRInputService inputService;

        private bool isOverlayDirty = true;

        // Persistent reusable buffers
        private Bitmap? sharedCaptureBmp;
        private Graphics? sharedCaptureGraphics;

        private Bitmap? _cachedMainTemp;
        private Graphics? _cachedMainTempGraphics;
        private readonly Dictionary<IntPtr, (Bitmap Bmp, Graphics Gfx)> _cachedPopupResources = [];

        // Position Anchoring
        private HmdMatrix34_t cachedAnchorTransform;
        private bool hasAnchorTransform = false;
        private float lastMetersPerPixel = 0f;
        private float cachedAnchorDistance = 0f;
        private float cachedAnchorHigh = 0f;

        // Virtual Canvas Positioning
        private int _cachedMinLeft = 0;
        private int _cachedMinTop = 0;
        private int _cachedCompositeHeight = 0;

        private enum ControllerHand { Right, Left }
        private ControllerHand activeHand = ControllerHand.Right;
        private uint ActiveControllerIdx => activeHand == ControllerHand.Right ? inputService.RightControllerIdx : inputService.LeftControllerIdx;

        private bool wasTriggerHeld = false;
        private bool hasPointerIntersection = false;
        private int pointerLocalX = 0;
        private int pointerLocalY = 0;

        private int clickPulseStartTime = -1000;
        private int clickPulseX = 0;
        private int clickPulseY = 0;

        private int lastScreenX = 0;
        private int lastScreenY = 0;

        private bool isGrabbingOverlay = false;
        private uint grabbingControllerIdx = OpenVR.k_unTrackedDeviceIndexInvalid;
        private Vector3 lastGrabControllerPos;
        private Vector3 currentOverlayPos;

        public bool IsInitialized => isInitialized;
        public bool IsVisible => isVisible;
        #endregion

        #region Constructor & Initialization
        public SteamOverlayService()
        {
            inputService = new VRInputService();

            if (Initialize())
            {
                inputService.InitActionHandles();

                SetWindow();
                SetVisible(false);
                StartThread();

                if (targetWindow != null)
                {
                    targetWindow.LayoutUpdated += (s, e) =>
                    {
                        targetWindow.Dispatcher.BeginInvoke(new Action(async () =>
                        {
                            await Task.Delay(100);
                            isOverlayDirty = true;
                        }));
                    };

                    targetWindow.IsVisibleChanged += (s, e) =>
                    {
                        SetVisible(targetWindow.IsVisible);
                    };
                }
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

            CVROverlay? overlay = OpenVR.Overlay;
            if (overlay == null) return false;

            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out d3dDevice, out d3dContext);

            EVROverlayError overlayError = overlay.CreateOverlay("ScreenLookup.WorldOverlay", "ScreenLookup Floating Window", ref overlayHandle);
            if (overlayError != EVROverlayError.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] World overlay creation failed: {overlayError}");
                return false;
            }

            overlay.SetOverlayInputMethod(overlayHandle, VROverlayInputMethod.None);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.SendVRSmoothScrollEvents, false);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.MakeOverlaysInteractiveIfVisible, false);
            overlay.SetOverlayFlag(overlayHandle, VROverlayFlags.IsPremultiplied, true);

            EVROverlayError laserOverlayError = overlay.CreateOverlay("ScreenLookup.LaserOverlay", "ScreenLookup Laser Pointer", ref laserOverlayHandle);
            if (laserOverlayError == EVROverlayError.None)
            {
                overlay.SetOverlayInputMethod(laserOverlayHandle, VROverlayInputMethod.None);
                overlay.SetOverlayFlag(laserOverlayHandle, VROverlayFlags.IsPremultiplied, true);
                overlay.SetOverlayWidthInMeters(laserOverlayHandle, 0.0025f);
            }

            return isInitialized = true;
        }

        private void SetVisible(bool visible)
        {
            CVROverlay? overlay = OpenVR.Overlay;
            if (overlay == null) return;

            if (visible)
            {
                isOverlayDirty = true;
                hasAnchorTransform = false;
                SetWindow();

                inputService.BlockGameInput = true;
                overlay.ShowOverlay(overlayHandle);
            }
            else
            {
                inputService.BlockGameInput = false;
                overlay.HideOverlay(overlayHandle);
                HideLaserOverlay();
            }

            isVisible = visible;
        }

        private void SetWindow()
        {
            targetWindow = App.captureWindow;
            if (targetWindow != null)
                targetHwnd = new WindowInteropHelper(targetWindow).Handle;
        }
        #endregion

        #region Polling Loop & Actions
        private void StartThread()
        {
            if (running) return;

            cts = new CancellationTokenSource();
            running = true;
            processTask = ThreadLoopAsync(cts.Token);
        }

        private void StopThread()
        {
            running = false;
            cts?.Cancel();
        }

        private async Task ThreadLoopAsync(CancellationToken ct)
        {
            float refreshRate = VRInputService.GetHmdRefreshRate();
            int delay = (int)(1000 / (refreshRate > 0 ? refreshRate : 90f));

            while (!ct.IsCancellationRequested)
            {
                RenderFrame();
                ProcessInput();

                await Task.Delay(delay, ct);
            }
        }

        private bool isRecentering = false;
        private CancellationTokenSource? doublePressCts;

        private void PerformRecenter()
        {
            if (isRecentering) return;

            doublePressCts?.Cancel();
            doublePressCts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                inputService.TriggerHapticPulse(ActiveControllerIdx, 3000);
                await Task.Delay(100, doublePressCts.Token);
                inputService.TriggerHapticPulse(ActiveControllerIdx, 3000);
            });

            Task.Run(async () =>
            {
                try
                {
                    isRecentering = true;
                    isOverlayDirty = true;
                    AppUtilities.PlaySound("recenter.wav");
                    await Task.Delay(1000, doublePressCts.Token);

                    isRecentering = false;
                    hasAnchorTransform = false;
                    isOverlayDirty = true;
                }
                catch (TaskCanceledException)
                {
                    isRecentering = false;
                }
            });
        }

        private void PerformClose()
        {
            targetWindow?.Dispatcher.Invoke(() => targetWindow.CloseWindow());
            inputService.TriggerHapticPulse(ActiveControllerIdx, 3000);
        }
        #endregion

        #region Input & Raycast Processing
        private void ProcessInput()
        {
            CVROverlay? overlay = OpenVR.Overlay;

            if (!isVisible) return;
            if (overlay == null) return;
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;
            if (sharedCaptureBmp == null) return;

            try
            {
                inputService.UpdatePosesAndIndices();
                inputService.UpdateActionState();

                ulong secondaryTrigger = (activeHand == ControllerHand.Right)
                    ? inputService.TriggerLeftHandle
                    : inputService.TriggerRightHandle;

                if (inputService.IsActionJustPressed(secondaryTrigger))
                    activeHand = (activeHand == ControllerHand.Right) ? ControllerHand.Left : ControllerHand.Right;

                bool isIntersecting = false;


                // Raycaster
                if (ActiveControllerIdx != OpenVR.k_unTrackedDeviceIndexInvalid && inputService.Poses[ActiveControllerIdx].bPoseIsValid)
                {
                    HmdMatrix34_t pose = inputService.Poses[ActiveControllerIdx].mDeviceToAbsoluteTracking;
                    ulong pointerHandle = (activeHand == ControllerHand.Right)
                        ? inputService.PointerRightHandle
                        : inputService.PointerLeftHandle;

                    if (inputService.GetPointerRay(pointerHandle, pose, out HmdVector3_t source, out HmdVector3_t direction))
                    {
                        hasPointerIntersection = false;
                        isIntersecting = false;

                        VROverlayIntersectionParams_t params_ = new()
                        {
                            eOrigin = ETrackingUniverseOrigin.TrackingUniverseStanding,
                            vSource = source,
                            vDirection = direction
                        };
                        VROverlayIntersectionResults_t results_ = new();

                        Vector3 sourcePos = new(source.v0, source.v1, source.v2);
                        Vector3 rayDir = Vector3.Normalize(new Vector3(direction.v0, direction.v1, direction.v2));
                        Vector3 hitPos = sourcePos + rayDir * 1.5f;

                        if (sharedCaptureBmp != null && sharedCaptureBmp.Width > 0 && sharedCaptureBmp.Height > 0 && overlay.ComputeOverlayIntersection(overlayHandle, ref params_, ref results_))
                        {
                            int compW = sharedCaptureBmp.Width;
                            int compH = sharedCaptureBmp.Height;

                            int localX = (int)(results_.vUVs.v0 * compW);
                            int localY = (int)((1f - results_.vUVs.v1) * compH);

                            int screenX = _cachedMinLeft + localX;
                            int screenY = _cachedMinTop + localY;

                            lastScreenX = screenX;
                            lastScreenY = screenY;

                            pointerLocalX = localX;
                            pointerLocalY = localY;
                            hasPointerIntersection = true;
                            isIntersecting = true;
                            isOverlayDirty = true;

                            hitPos = new(results_.vPoint.v0, results_.vPoint.v1, results_.vPoint.v2);
                        }

                        UpdateLaserOverlay(sourcePos, hitPos);
                    }
                }

                // Input Grip Button
                if (isGrabbingOverlay)
                {
                    ulong currentGrabGrip = (grabbingControllerIdx == inputService.RightControllerIdx)
                        ? inputService.GripRightHandle
                        : inputService.GripLeftHandle;

                    if (inputService.IsActionHeld(currentGrabGrip) &&
                        grabbingControllerIdx != OpenVR.k_unTrackedDeviceIndexInvalid &&
                        inputService.Poses[grabbingControllerIdx].bPoseIsValid)
                    {
                        Vector3 currentControllerPos = VRInputService.PosFromMatrix(inputService.Poses[grabbingControllerIdx].mDeviceToAbsoluteTracking);
                        Vector3 frameDelta = currentControllerPos - lastGrabControllerPos;
                        lastGrabControllerPos = currentControllerPos;

                        currentOverlayPos += frameDelta * 4f;

                        TrackedDevicePose_t[] hmdPoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                        OpenVR.System?.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, hmdPoses);
                        TrackedDevicePose_t hmdPose = hmdPoses[OpenVR.k_unTrackedDeviceIndex_Hmd];

                        Vector3 hmdPos = hmdPose.bPoseIsValid
                            ? VRInputService.PosFromMatrix(hmdPose.mDeviceToAbsoluteTracking)
                            : new Vector3(0f, currentOverlayPos.Y, 0f);

                        Vector3 dir = hmdPos - currentOverlayPos;
                        if (dir.LengthSquared() < 0.0001f)
                            dir = new Vector3(0f, 0f, -1f);
                        Vector3 fwd = Vector3.Normalize(dir);
                        Vector3 worldUp = new(0f, 1f, 0f);
                        Vector3 right = Vector3.Cross(worldUp, fwd);
                        if (right.LengthSquared() < 0.0001f)
                            right = new Vector3(1f, 0f, 0f);
                        else
                            right = Vector3.Normalize(right);
                        Vector3 up = Vector3.Normalize(Vector3.Cross(fwd, right));

                        HmdMatrix34_t newTransform = new()
                        {
                            m0 = right.X,
                            m1 = up.X,
                            m2 = fwd.X,
                            m3 = currentOverlayPos.X,
                            m4 = right.Y,
                            m5 = up.Y,
                            m6 = fwd.Y,
                            m7 = currentOverlayPos.Y,
                            m8 = right.Z,
                            m9 = up.Z,
                            m10 = fwd.Z,
                            m11 = currentOverlayPos.Z
                        };

                        overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref newTransform);

                        cachedAnchorTransform = newTransform;
                        hasAnchorTransform = true;
                        isOverlayDirty = true;
                    }
                    else
                    {
                        isGrabbingOverlay = false;
                        grabbingControllerIdx = OpenVR.k_unTrackedDeviceIndexInvalid;
                    }
                }
                else if (hasPointerIntersection)
                {
                    uint[] gripControllers = [inputService.RightControllerIdx, inputService.LeftControllerIdx];
                    foreach (uint ctrlIdx in gripControllers)
                    {
                        if (ctrlIdx == OpenVR.k_unTrackedDeviceIndexInvalid) continue;
                        if (ActiveControllerIdx != ctrlIdx) continue;
                        if (!inputService.Poses[ctrlIdx].bPoseIsValid) continue;

                        ulong gripHandle = (ctrlIdx == inputService.RightControllerIdx)
                            ? inputService.GripRightHandle
                            : inputService.GripLeftHandle;

                        if (inputService.IsActionJustPressed(gripHandle))
                        {
                            isGrabbingOverlay = true;
                            grabbingControllerIdx = ctrlIdx;

                            lastGrabControllerPos = VRInputService.PosFromMatrix(inputService.Poses[ctrlIdx].mDeviceToAbsoluteTracking);
                            currentOverlayPos = VRInputService.PosFromMatrix(cachedAnchorTransform);
                            break;
                        }
                    }
                }

                // Input Trigger Button
                if (isIntersecting && !isGrabbingOverlay)
                {
                    ulong primaryTrigger = (activeHand == ControllerHand.Right)
                        ? inputService.TriggerRightHandle
                        : inputService.TriggerLeftHandle;
                    bool isTriggerHeld = inputService.IsActionHeld(primaryTrigger);

                    if (isTriggerHeld && !wasTriggerHeld)
                    {
                        clickPulseStartTime = Environment.TickCount;
                        clickPulseX = pointerLocalX;
                        clickPulseY = pointerLocalY;

                        int clickX = lastScreenX;
                        int clickY = lastScreenY;

                        targetWindow?.Dispatcher.InvokeAsync(async () =>
                        {
                            if (!IsTargetWindowFronted())
                            {
                                SetForegroundWindow(targetHwnd);
                                await Task.Delay(50);
                            }
                            SetCursorPos(clickX, clickY);
                            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                            isOverlayDirty = true;
                        });
                    }
                    else if (!isTriggerHeld && wasTriggerHeld)
                    {
                        targetWindow?.Dispatcher.InvokeAsync(async () =>
                        {
                            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                            isOverlayDirty = true;
                        });
                    }
                    wasTriggerHeld = isTriggerHeld;
                }
                else if (wasTriggerHeld)
                {
                    targetWindow?.Dispatcher.InvokeAsync(() =>
                    {
                        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                        isOverlayDirty = true;
                    });
                    wasTriggerHeld = false;
                }

                // Input B/Y Button
                if (inputService.IsActionJustReleased(inputService.RecenterHandle))
                    PerformRecenter();

                // Input A/X Button
                if (inputService.IsActionJustReleased(inputService.CloseHandle))
                    PerformClose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Input processing error: {ex.Message}");
            }
        }

        private void UpdateLaserOverlay(Vector3 source, Vector3 hitPoint)
        {
            CVROverlay? overlay = OpenVR.Overlay;
            if (overlay == null || laserOverlayHandle == OpenVR.k_ulOverlayHandleInvalid || d3dDevice == null || d3dContext == null) return;

            Vector3 dir = hitPoint - source;
            float totalDist = dir.Length();
            if (totalDist < 0.1f) return;

            Vector3 normDir = Vector3.Normalize(dir);
            float tipOffset = 0.2f;
            if (totalDist <= tipOffset) return;

            Vector3 startPos = source + (normDir * tipOffset);
            Vector3 actualLaserDir = hitPoint - startPos;
            float laserLength = actualLaserDir.Length();
            if (laserLength < 0.01f) return;

            Vector3 mid = (startPos + hitPoint) * 0.5f;

            Vector3 up = Vector3.Normalize(actualLaserDir);
            Vector3 right = MathF.Abs(up.Y) < 0.99f
                ? Vector3.Normalize(Vector3.Cross(up, Vector3.UnitY))
                : Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX));
            Vector3 fwd = Vector3.Normalize(Vector3.Cross(right, up));

            HmdMatrix34_t laserTransform = new()
            {
                m0 = right.X,
                m1 = up.X,
                m2 = fwd.X,
                m3 = mid.X,
                m4 = right.Y,
                m5 = up.Y,
                m6 = fwd.Y,
                m7 = mid.Y,
                m8 = right.Z,
                m9 = up.Z,
                m10 = fwd.Z,
                m11 = mid.Z
            };

            overlay.SetOverlayTransformAbsolute(laserOverlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref laserTransform);
            overlay.SetOverlayWidthInMeters(laserOverlayHandle, 0.0025f);

            int targetHeight = Math.Max(10, (int)(laserLength * 500f));
            float distDelta = MathF.Abs(laserLength - lastLaserLength);
            float currentTime = (float)Environment.TickCount64 / 1000f;

            lock (d3dLock)
            {
                if (laserD3dTex == null ||
                   (lastLaserTexHeight != targetHeight && distDelta > 0.02f && (currentTime - lastUpdateLengthTime) >= 0.1f))
                {
                    laserD3dTex?.Dispose();
                    laserStagingTex?.Dispose();

                    Texture2DDescription desc = new()
                    {
                        Width = 1,
                        Height = (uint)targetHeight,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.ShaderResource
                    };
                    laserD3dTex = d3dDevice.CreateTexture2D(desc);

                    desc.Usage = ResourceUsage.Staging;
                    desc.BindFlags = BindFlags.None;
                    desc.CPUAccessFlags = CpuAccessFlags.Write;
                    laserStagingTex = d3dDevice.CreateTexture2D(desc);

                    lastLaserTexHeight = targetHeight;
                    lastLaserLength = laserLength;
                    lastUpdateLengthTime = currentTime;

                    MappedSubresource map = d3dContext.Map(laserStagingTex, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                    unsafe
                    {
                        byte r = 218, g = 96, b = 255;
                        int fadeLen = Math.Min(100, targetHeight);

                        for (int y = 0; y < targetHeight; y++)
                        {
                            byte alpha = 255;
                            int distFromEnd = (targetHeight - 1) - y;
                            if (distFromEnd < fadeLen)
                            {
                                float fadeRatio = (float)distFromEnd / fadeLen;
                                alpha = (byte)(255 * fadeRatio);
                            }

                            byte pA = alpha;
                            byte pB = (byte)((b * pA + 127) / 255);
                            byte pG = (byte)((g * pA + 127) / 255);
                            byte pR = (byte)((r * pA + 127) / 255);

                            byte* pixelPtr = (byte*)map.DataPointer + (y * map.RowPitch);
                            pixelPtr[0] = pB;
                            pixelPtr[1] = pG;
                            pixelPtr[2] = pR;
                            pixelPtr[3] = pA;
                        }
                    }
                    d3dContext.Unmap(laserStagingTex, 0);
                    d3dContext.CopyResource(laserD3dTex, laserStagingTex);

                    Texture_t tex = new()
                    {
                        handle = laserD3dTex.NativePointer,
                        eType = ETextureType.DirectX,
                        eColorSpace = EColorSpace.Auto
                    };
                    overlay.SetOverlayTexture(laserOverlayHandle, ref tex);
                }
            }

            overlay.ShowOverlay(laserOverlayHandle);
        }

        private void HideLaserOverlay()
        {
            CVROverlay? overlay = OpenVR.Overlay;
            if (overlay != null && laserOverlayHandle != OpenVR.k_ulOverlayHandleInvalid)
                overlay.HideOverlay(laserOverlayHandle);
        }
        #endregion

        #region Direct3D Overlay Composition
        private void UpdateOverlayTransform(float pixelShiftX, float pixelShiftY, float metersPerPixel)
        {
            CVROverlay? overlay = OpenVR.Overlay;
            float currentTargetDistance = App.setting.OverlayDistance;
            float currentTargetHigh = App.setting.OverlayHigh;

            if (overlay == null) return;
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            if (hasAnchorTransform && (!cachedAnchorDistance.Equals(currentTargetDistance) || !cachedAnchorHigh.Equals(currentTargetHigh)))
            {
                TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                OpenVR.System?.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses);
                TrackedDevicePose_t hmdPose = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];

                if (hmdPose.bPoseIsValid)
                {
                    HmdMatrix34_t hmdMatrix = hmdPose.mDeviceToAbsoluteTracking;
                    float forwardX = -cachedAnchorTransform.m2;
                    float forwardZ = -cachedAnchorTransform.m10;

                    cachedAnchorTransform.m3 = hmdMatrix.m3 + (forwardX * currentTargetDistance);
                    cachedAnchorTransform.m7 = hmdMatrix.m7 + (currentTargetHigh);
                    cachedAnchorTransform.m11 = hmdMatrix.m11 + (forwardZ * currentTargetDistance);

                    cachedAnchorDistance = currentTargetDistance;
                    cachedAnchorHigh = currentTargetHigh;
                }
            }

            if (!hasAnchorTransform)
            {
                TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
                OpenVR.System?.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, poses);

                TrackedDevicePose_t hmdPose = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];
                if (!hmdPose.bPoseIsValid) return;

                HmdMatrix34_t hmdMatrix = hmdPose.mDeviceToAbsoluteTracking;

                float rawForwardX = -hmdMatrix.m2;
                float rawForwardZ = -hmdMatrix.m10;
                float horizontalLength = MathF.Sqrt(rawForwardX * rawForwardX + rawForwardZ * rawForwardZ);

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

                cachedAnchorTransform = new HmdMatrix34_t
                {
                    m0 = rX,
                    m1 = 0f,
                    m2 = -fX,
                    m3 = hmdMatrix.m3 + (fX * currentTargetDistance),
                    m4 = 0f,
                    m5 = 1f,
                    m6 = 0f,
                    m7 = hmdMatrix.m7 + (currentTargetHigh),
                    m8 = rZ,
                    m9 = 0f,
                    m10 = -fZ,
                    m11 = hmdMatrix.m11 + (fZ * currentTargetDistance)
                };
                cachedAnchorDistance = currentTargetDistance;
                cachedAnchorHigh = currentTargetHigh;
                hasAnchorTransform = true;
            }

            float vrShiftX = pixelShiftX * metersPerPixel;
            float vrShiftY = pixelShiftY * metersPerPixel;

            HmdMatrix34_t adjustedTransform = cachedAnchorTransform;
            adjustedTransform.m3 += (cachedAnchorTransform.m0 * vrShiftX);
            adjustedTransform.m7 += (cachedAnchorTransform.m4 * vrShiftX) + (cachedAnchorTransform.m5 * vrShiftY);
            adjustedTransform.m11 += (cachedAnchorTransform.m8 * vrShiftX);

            overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref adjustedTransform);

            float baseWidthInMeters = App.setting.OverlayScale;
            if (targetWindow.configMenu.IsVisible)
                baseWidthInMeters = App.setting.OverlayScale / 2f;

            float curve = baseWidthInMeters / 6f * currentTargetDistance * (App.setting.OverlayCurve / 100f);
            overlay.SetOverlayCurvature(overlayHandle, curve);
        }

        private void RenderFrame()
        {
            if (!isVisible) return;
            if (OpenVR.Overlay == null) return;
            if (!isInitialized || overlayHandle == OpenVR.k_ulOverlayHandleInvalid || targetWindow == null) return;

            CVROverlay? overlay = OpenVR.Overlay;
            RECT mainRect = new();
            List<(IntPtr Handle, RECT Rect)> popupWindows = GetActivePopupWindows();

            GetWindowRect(targetHwnd, out mainRect);

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

            _cachedMinLeft = minLeft;
            _cachedMinTop = minTop;
            _cachedCompositeHeight = compositeHeight;

            if (sharedCaptureBmp == null || sharedCaptureBmp.Width != compositeWidth || sharedCaptureBmp.Height != compositeHeight)
                isOverlayDirty = true;

            int mainWindowWidth = mainRect.Right - mainRect.Left;
            if (mainWindowWidth <= 0) mainWindowWidth = 1;

            float targetWidthInMeters = App.setting.OverlayScale;
            if (targetWindow.configMenu.IsVisible)
                targetWidthInMeters = App.setting.OverlayScale / 2f;

            float metersPerPixel = targetWidthInMeters / mainWindowWidth;
            float widthInMeters = compositeWidth * metersPerPixel;

            overlay.SetOverlayWidthInMeters(overlayHandle, widthInMeters);

            HmdVector2_t mouseScale = new() { v0 = (float)compositeWidth, v1 = (float)compositeHeight };
            overlay.SetOverlayMouseScale(overlayHandle, ref mouseScale);

            float leftGrowth = mainRect.Left - minLeft;
            float rightGrowth = maxRight - mainRect.Right;
            float topGrowth = mainRect.Top - minTop;
            float bottomGrowth = maxBottom - mainRect.Bottom;

            float pixelShiftX = (rightGrowth - leftGrowth) / 2f;
            float pixelShiftY = (topGrowth - bottomGrowth) / 2f;

            if (isOverlayDirty || lastMetersPerPixel != metersPerPixel || !cachedAnchorDistance.Equals(App.setting.OverlayDistance) || !cachedAnchorHigh.Equals(App.setting.OverlayHigh))
            {
                UpdateOverlayTransform(pixelShiftX, pixelShiftY, metersPerPixel);
                lastMetersPerPixel = metersPerPixel;
            }

            if (!isOverlayDirty) return;

            try
            {
                if (sharedCaptureBmp == null || sharedCaptureBmp.Width != compositeWidth || sharedCaptureBmp.Height != compositeHeight)
                {
                    sharedCaptureGraphics?.Dispose();
                    sharedCaptureBmp?.Dispose();

                    sharedCaptureBmp = new Bitmap(compositeWidth, compositeHeight, PixelFormat.Format32bppPArgb);
                    sharedCaptureGraphics = Graphics.FromImage(sharedCaptureBmp);
                }

                sharedCaptureGraphics.Clear(Color.Transparent);

                targetWindow.Dispatcher.Invoke(() =>
                {
                    IntPtr hdc = sharedCaptureGraphics!.GetHdc();
                    PrintWindow(targetHwnd, hdc, 0x02);
                    sharedCaptureGraphics.ReleaseHdc(hdc);

                    int mainOffsetX = mainRect.Left - minLeft;
                    int mainOffsetY = mainRect.Top - minTop;
                    int mainW = mainRect.Right - mainRect.Left;
                    int mainH = mainRect.Bottom - mainRect.Top;

                    if (mainOffsetX != 0 || mainOffsetY != 0)
                    {
                        if (_cachedMainTemp == null || _cachedMainTemp.Width != mainW || _cachedMainTemp.Height != mainH)
                        {
                            _cachedMainTempGraphics?.Dispose();
                            _cachedMainTemp?.Dispose();
                            _cachedMainTemp = new Bitmap(mainW, mainH, PixelFormat.Format32bppPArgb);
                            _cachedMainTempGraphics = Graphics.FromImage(_cachedMainTemp);
                        }

                        IntPtr hdcM = _cachedMainTempGraphics!.GetHdc();
                        PrintWindow(targetHwnd, hdcM, 0x02);
                        _cachedMainTempGraphics.ReleaseHdc(hdcM);

                        sharedCaptureGraphics.Clear(Color.Transparent);
                        sharedCaptureGraphics.DrawImage(_cachedMainTemp, mainOffsetX, mainOffsetY);
                    }

                    foreach (var popup in popupWindows)
                    {
                        int pW = Math.Max(1, popup.Rect.Right - popup.Rect.Left);
                        int pH = Math.Max(1, popup.Rect.Bottom - popup.Rect.Top);

                        if (!_cachedPopupResources.TryGetValue(popup.Handle, out var res) || res.Bmp.Width != pW || res.Bmp.Height != pH)
                        {
                            res.Gfx?.Dispose();
                            res.Bmp?.Dispose();
                            var bmp = new Bitmap(pW, pH, PixelFormat.Format32bppPArgb);
                            var gfx = Graphics.FromImage(bmp);
                            res = (bmp, gfx);
                            _cachedPopupResources[popup.Handle] = res;
                        }

                        var popupBmp = res.Bmp;
                        var gP = res.Gfx;

                        IntPtr hdcP = gP.GetHdc();
                        PrintWindow(popup.Handle, hdcP, 0x02);
                        gP.ReleaseHdc(hdcP);

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
                        sharedCaptureGraphics.DrawImage(popupBmp, popup.Rect.Left - minLeft, popup.Rect.Top - minTop);
                    }
                });

                RenderHint(compositeWidth);
                RenderRecentering(compositeWidth, compositeHeight);
                RenderCursor(compositeWidth, compositeHeight);
                RenderClickAnimation();
                RenderCaptureOverlay(compositeWidth, compositeHeight);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Frame rendering encountered error: {ex.Message}");
            }
        }

        private bool IsTargetWindowFronted()
        {
            IntPtr foregroundHwnd = GetForegroundWindow();
            return foregroundHwnd == targetHwnd;
        }

        private List<(IntPtr Handle, RECT Rect)> GetActivePopupWindows()
        {
            List<(IntPtr Handle, RECT Rect)> popupWindows = [];

            // Find all active popup/flyout sources belonging to this dispatcher
            targetWindow?.Dispatcher.Invoke(() =>
            {
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
            });

            return popupWindows;
        }

        private void RenderCursor(int compositeWidth, int compositeHeight)
        {
            // Render Cursor (Extracted via DIB Section to cleanly handle 32-bit alpha and drop shadow)
            CURSORINFO ci = new() { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (hasPointerIntersection && GetCursorInfo(out ci) && (ci.flags & 1) != 0 && ci.hCursor != IntPtr.Zero)
            {
                if (GetIconInfo(ci.hCursor, out ICONINFO iconInfo))
                {
                    int cursorX = pointerLocalX - iconInfo.xHotspot;
                    int cursorY = pointerLocalY - iconInfo.yHotspot;

                    bool isMonochrome = iconInfo.hbmColor == IntPtr.Zero;
                    if (!isMonochrome)
                    {
                        if (GetObject(iconInfo.hbmColor, Marshal.SizeOf<BITMAP>(), out BITMAP bm) > 0)
                        {
                            int w = bm.bmWidth;
                            int h = bm.bmHeight;

                            IntPtr hdcScreen = GetDC(IntPtr.Zero);
                            IntPtr hdcMem = CreateCompatibleDC(hdcScreen);

                            BITMAPINFOHEADER bmi = new()
                            {
                                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                                biWidth = w,
                                biHeight = -h, // Top-down DIB
                                biPlanes = 1,
                                biBitCount = 32,
                                biCompression = 0 // BI_RGB
                            };

                            IntPtr hbmDib = CreateDIBSection(hdcMem, ref bmi, 0, out IntPtr bitsPtr, IntPtr.Zero, 0);
                            IntPtr hOld = SelectObject(hdcMem, hbmDib);

                            DrawIconEx(hdcMem, 0, 0, ci.hCursor, w, h, 0, IntPtr.Zero, 0x0003);

                            unsafe
                            {
                                byte* srcPixels = (byte*)bitsPtr;
                                for (int y = 0; y < h; y++)
                                {
                                    int targetY = cursorY + y;
                                    if (targetY < 0 || targetY >= compositeHeight) continue;

                                    byte* srcRow = srcPixels + (y * bm.bmWidthBytes);
                                    for (int x = 0; x < w; x++)
                                    {
                                        int targetX = cursorX + x;
                                        if (targetX < 0 || targetX >= compositeWidth) continue;

                                        int srcOffset = x * 4;
                                        byte b = srcRow[srcOffset + 0];
                                        byte g = srcRow[srcOffset + 1];
                                        byte r = srcRow[srcOffset + 2];
                                        byte a = srcRow[srcOffset + 3];

                                        if (a == 0) continue;

                                        // Filter out cursor drop shadow (semi-transparent black fringe)
                                        if (a < 180 && r < 50 && g < 50 && b < 50) continue;

                                        sharedCaptureBmp!.SetPixel(targetX, targetY, Color.FromArgb(a, r, g, b));
                                    }
                                }
                            }

                            SelectObject(hdcMem, hOld);
                            DeleteObject(hbmDib);
                            DeleteDC(hdcMem);
                            ReleaseDC(IntPtr.Zero, hdcScreen);
                        }
                    }
                    else
                    {
                        IntPtr hdc = sharedCaptureGraphics!.GetHdc();
                        DrawIconEx(hdc, cursorX, cursorY, ci.hCursor, 0, 0, 0, IntPtr.Zero, 0x0003);
                        sharedCaptureGraphics.ReleaseHdc(hdc);
                    }

                    if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                    if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
                }
            }
            else if (hasPointerIntersection)
            {
                using SolidBrush dotBrush = new(Color.FromArgb(220, 255, 255, 255));
                using Pen dotBorder = new(Color.FromArgb(255, 218, 96, 255), 2f);
                sharedCaptureGraphics!.SmoothingMode = SmoothingMode.AntiAlias;
                sharedCaptureGraphics.FillEllipse(dotBrush, pointerLocalX - 4, pointerLocalY - 4, 8, 8);
                sharedCaptureGraphics.DrawEllipse(dotBorder, pointerLocalX - 4, pointerLocalY - 4, 8, 8);
            }
        }

        private void RenderRecentering(int compositeWidth, int compositeHeight)
        {
            if (!isRecentering) return;

            string recenterText = "Recentering...";
            using Font font = new(App.setting.FontFace ?? "Segoe UI", 16f, System.Drawing.FontStyle.Bold);
            SizeF sz = sharedCaptureGraphics!.MeasureString(recenterText, font);
            float padX = 24f, padY = 14f;
            float boxW = sz.Width + padX * 2f;
            float boxH = sz.Height + padY * 2f;
            float boxX = (compositeWidth - boxW) / 2f;
            float boxY = (compositeHeight - boxH) / 2f;

            RectangleF boxRect = new(boxX, boxY, boxW, boxH);
            using GraphicsPath boxPath = CreateRoundedRectanglePath(boxRect, 8f);
            using SolidBrush bgBrush = new(Color.FromArgb(220, 20, 20, 28));
            using Pen borderPen = new(Color.FromArgb(255, 218, 96, 255), 2f);
            using SolidBrush textBrush = new(Color.FromArgb(245, 245, 245));

            sharedCaptureGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            sharedCaptureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            sharedCaptureGraphics.FillPath(bgBrush, boxPath);
            sharedCaptureGraphics.DrawPath(borderPen, boxPath);
            sharedCaptureGraphics.DrawString(recenterText, font, textBrush, boxX + padX, boxY + padY);
        }

        private void RenderClickAnimation()
        {
            // Render Click Pulse Ripple Effect
            int elapsedPulse = Environment.TickCount - clickPulseStartTime;
            if (elapsedPulse is >= 0 and < 300)
            {
                isOverlayDirty = true;
                float progress = elapsedPulse / 300f;
                float radius = 8f + progress * 24f;
                int alpha = (int)(255 * (1f - progress));

                using Pen pulsePen = new(Color.FromArgb(alpha, 218, 96, 255), 2.5f * (1f - progress) + 1f);
                sharedCaptureGraphics!.SmoothingMode = SmoothingMode.AntiAlias;
                sharedCaptureGraphics.DrawEllipse(pulsePen, clickPulseX - radius, clickPulseY - radius, radius * 2f, radius * 2f);
            }
        }

        private void RenderHint(int compositeWidth)
        {
            // Render Hint Box
            string[] hintLines =
            [
                "- Press A/X to close overlay, B/Y to recenter.",
                "- Grab to move the overlay.",
            ];

            using Font font = new(App.setting.FontFace ?? "Segoe UI", 10f, System.Drawing.FontStyle.Regular);
            float paddingLeft = 8f, paddingRight = 0f;
            float paddingTop = 6f, paddingBottom = 0f;
            float lineSpacing = 3f;
            float maxLineW = 0f, totalTextH = 0f;

            for (int i = 0; i < hintLines.Length; i++)
            {
                SizeF sz = sharedCaptureGraphics!.MeasureString(hintLines[i], font);
                if (sz.Width > maxLineW) maxLineW = sz.Width;
                totalTextH += sz.Height;
                if (i < hintLines.Length - 1) totalTextH += lineSpacing;
            }

            float boxW = maxLineW + paddingLeft + paddingRight;
            float boxH = totalTextH + paddingTop + paddingBottom;
            float boxX = Math.Max(0f, compositeWidth - boxW - 2f);
            float boxY = 2f;

            RectangleF boxRect = new(boxX, boxY, boxW, boxH);
            using GraphicsPath boxPath = CreateRoundedRectanglePath(boxRect, 6f);
            using SolidBrush bgBrush = new(Color.FromArgb(200, 20, 20, 28));
            using Pen borderPen = new(Color.FromArgb(180, 218, 96, 255), 1.5f);
            using SolidBrush textBrush = new(Color.FromArgb(245, 245, 245));

            sharedCaptureGraphics.SmoothingMode = SmoothingMode.AntiAlias;
            sharedCaptureGraphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            sharedCaptureGraphics.FillPath(bgBrush, boxPath);
            sharedCaptureGraphics.DrawPath(borderPen, boxPath);

            float textX = boxX + paddingLeft;
            float currentY = boxY + paddingTop;
            float fontHeight = font.GetHeight(sharedCaptureGraphics);

            foreach (string line in hintLines)
            {
                sharedCaptureGraphics.DrawString(line, font, textBrush, textX, currentY);
                currentY += fontHeight + lineSpacing;
            }
        }

        private void RenderCaptureOverlay(int compositeWidth, int compositeHeight)
        {
            CVROverlay? overlay = OpenVR.Overlay;
            if (overlay == null || overlayHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            // Hardware Texture Copy
            lock (d3dLock)
            {
                ID3D11Texture2D? oldOverlayTex = null;
                ID3D11Texture2D? oldStagingTex = null;

                if (overlayTex == null || overlayTex.Description.Width != (uint)compositeWidth || overlayTex.Description.Height != (uint)compositeHeight)
                {
                    oldOverlayTex = overlayTex;
                    oldStagingTex = stagingTex;

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
                BitmapData bData = sharedCaptureBmp!.LockBits(new Rectangle(0, 0, compositeWidth, compositeHeight), ImageLockMode.ReadOnly, sharedCaptureBmp.PixelFormat);

                unsafe
                {
                    int cornerRadius = 12;
                    int sqRadius = cornerRadius * cornerRadius;
                    int rightEdge = compositeWidth - cornerRadius;
                    int bottomEdge = compositeHeight - cornerRadius;

                    for (int y = 0; y < compositeHeight; y++)
                    {
                        byte* sourceRowPtr = (byte*)bData.Scan0 + (y * bData.Stride);
                        byte* destRowPtr = (byte*)box.DataPointer + (y * box.RowPitch);
                        bool isVerticalCorner = y < cornerRadius || y >= bottomEdge;

                        for (int x = 0; x < compositeWidth; x++)
                        {
                            int pixelOffset = x * 4;

                            if (isVerticalCorner && (x < cornerRadius || x >= rightEdge))
                            {
                                int cx = x < cornerRadius ? cornerRadius - 1 : compositeWidth - cornerRadius;
                                int cy = y < cornerRadius ? cornerRadius - 1 : compositeHeight - cornerRadius;
                                int dx = x - cx;
                                int dy = y - cy;
                                if ((dx * dx) + (dy * dy) > sqRadius)
                                {
                                    *(uint*)(destRowPtr + pixelOffset) = 0;
                                    continue;
                                }
                            }

                            byte a = sourceRowPtr[pixelOffset + 3];
                            if (a == 0)
                            {
                                *(uint*)(destRowPtr + pixelOffset) = 0;
                                continue;
                            }

                            byte b = sourceRowPtr[pixelOffset + 0];
                            byte g = sourceRowPtr[pixelOffset + 1];
                            byte r = sourceRowPtr[pixelOffset + 2];

                            destRowPtr[pixelOffset + 0] = (byte)((b * a + 127) / 255);
                            destRowPtr[pixelOffset + 1] = (byte)((g * a + 127) / 255);
                            destRowPtr[pixelOffset + 2] = (byte)((r * a + 127) / 255);
                            destRowPtr[pixelOffset + 3] = a;
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
                d3dContext.Flush();

                oldOverlayTex?.Dispose();
                oldStagingTex?.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float r)
        {
            GraphicsPath path = new();
            float d = r * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (isInitialized)
            {
                StopThread();

                lock (d3dLock)
                {
                    CVROverlay? overlay = OpenVR.Overlay;

                    if (overlay != null && overlayHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(overlayHandle);

                    if (overlay != null && laserOverlayHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(laserOverlayHandle);

                    sharedCaptureGraphics?.Dispose();
                    sharedCaptureBmp?.Dispose();

                    overlayTex?.Dispose();
                    stagingTex?.Dispose();

                    laserD3dTex?.Dispose();
                    laserStagingTex?.Dispose();

                    d3dContext?.Dispose();
                    d3dDevice?.Dispose();
                }

                overlayHandle = OpenVR.k_ulOverlayHandleInvalid;
                isInitialized = false;
            }
        }
        #endregion
    }
}
