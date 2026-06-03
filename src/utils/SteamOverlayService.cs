using System.Drawing.Imaging;
using System.IO;
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
        // Win32 API imports for injecting mouse inputs into OS space via VR controllers
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

        private ulong dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
        private ulong thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
        private bool isInitialized = false;

        // Direct3D11 Native Interfaces
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

        // High-Performance Pipeline Caching
        private bool isOverlayDirty = true;
        private byte[]? rowBuffer;

        public bool IsInitialized => isInitialized;

        public SteamOverlayService(Window window)
        {
            SetWindow(window);

            if (Initialize())
                StartPolling();
        }

        public void SetWindow(Window window)
        {
            targetWindow = window ?? throw new ArgumentNullException(nameof(window));
            targetHwnd = new WindowInteropHelper(targetWindow).Handle;

            targetWindow.LayoutUpdated += (s, e) =>
            {
                isOverlayDirty = true;
            };
        }

        /// <summary>
        /// Instantiates OpenVR Subsystems and establishes GPU Direct3D contexts.
        /// </summary>
        private bool Initialize()
        {
            if (OpenVR.System == null)
            {
                var initError = EVRInitError.None;
                OpenVR.Init(ref initError, EVRApplicationType.VRApplication_Overlay);

                if (initError != EVRInitError.None)
                {
                    System.Diagnostics.Debug.WriteLine($"[SteamVR] Initialization failed: {initError}");
                    return false;
                }
            }

            var overlay = OpenVR.Overlay;
            if (overlay == null) return false;

            // Spin up Direct3D11 Device Context
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out d3dDevice, out d3dContext);

            var overlayError = overlay.CreateDashboardOverlay("ScreenLookup.Overlay", "ScreenLookup", ref dashboardHandle, ref thumbnailHandle);
            if (overlayError != EVROverlayError.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Dashboard overlay creation failed: {overlayError}");
                return false;
            }

            string thumbnailPath = Path.Combine(Environment.CurrentDirectory, "src\\images", "applicationIcon.png");
            if (!string.IsNullOrEmpty(thumbnailPath) && System.IO.File.Exists(thumbnailPath))
                overlay.SetOverlayFromFile(thumbnailHandle, thumbnailPath);

            // Handle alpha transparency cleanly (WPF targets Premultiplied Alpha layout outputs)
            overlay.SetOverlayFlag(dashboardHandle, VROverlayFlags.IsPremultiplied, true);

            isInitialized = true;
            return true;
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

                // Steady loop delay capping execution around 60Hz intervals
                await Task.Delay(16, ct);
            }
        }
        #endregion

        /// <summary>
        /// Map OpenVR Pointer updates down into Windows system input coordinate space variables.
        /// </summary>
        public void ProcessInput()
        {
            if (!isInitialized || dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;
            if (!targetWindow.IsVisible) return;

            VREvent_t vrEvent = new();
            uint eventSize = (uint)Marshal.SizeOf<VREvent_t>();

            while (OpenVR.Overlay.PollNextOverlayEvent(dashboardHandle, ref vrEvent, eventSize))
            {
                switch ((EVREventType)vrEvent.eventType)
                {
                    case EVREventType.VREvent_MouseMove:
                        double vrX = vrEvent.data.mouse.x;
                        double vrY = targetWindow.ActualHeight - vrEvent.data.mouse.y;

                        targetWindow.Dispatcher.Invoke(() =>
                        {
                            if (targetWindow.IsVisible)
                            {
                                Point screenPoint = targetWindow.PointToScreen(new Point(vrX, vrY));

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

        /// <summary>
        /// Handles UI element frame capture and submission logic. Drops layout rendering cost when window is hidden.
        /// </summary>
        public void RenderFrame()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null || !isInitialized || dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            if (targetWindow.IsVisible)
            {
                try
                {
                    if (!isOverlayDirty) return; // High-Speed short-circuit execution bail path if no updates are needed

                    GetWindowRect(targetHwnd, out var rect);
                    double uiWidth = rect.Right - rect.Left;
                    double uiHeight = rect.Bottom - rect.Top;

                    if (uiWidth <= 0 || uiHeight <= 0) return;

                    using var captureBmp = new System.Drawing.Bitmap((int)uiWidth, (int)uiHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var g = System.Drawing.Graphics.FromImage(captureBmp))
                    {
                        IntPtr hdc = g.GetHdc();
                        // 0x02 = PW_RENDERFULLCONTENT, introduced in Win 8.1 to capture hardware-accelerated windows
                        PrintWindow(targetHwnd, hdc, 0x02);
                        g.ReleaseHdc(hdc);

                        // Composite WPF Popups (Flyouts) which are separate top-level windows and not captured by PrintWindow on the main HWND
                        try
                        {
                            foreach (PresentationSource source in PresentationSource.CurrentSources)
                            {
                                if (source is HwndSource hwndSource && hwndSource.Handle != targetHwnd &&
                                    hwndSource.RootVisual is FrameworkElement element && element.IsVisible &&
                                    element.GetType().Name == "PopupRoot")
                                {
                                    GetWindowRect(hwndSource.Handle, out var pRect);
                                    int pW = pRect.Right - pRect.Left;
                                    int pH = pRect.Bottom - pRect.Top;

                                    if (pW > 0 && pH > 0)
                                    {
                                        using var popupBmp = new System.Drawing.Bitmap(pW, pH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                                        using (var gP = System.Drawing.Graphics.FromImage(popupBmp))
                                        {
                                            IntPtr hdcP = gP.GetHdc();
                                            PrintWindow(hwndSource.Handle, hdcP, 0x02);
                                            gP.ReleaseHdc(hdcP);
                                        }
                                        // Draw the popup onto the main capture at the correct relative offset
                                        g.DrawImage(popupBmp, pRect.Left - rect.Left, pRect.Top - rect.Top);
                                    }
                                }
                            }
                        }
                        catch (Exception) { /* Ignore transient errors if sources collection changes during iteration */ }
                    }

                    float resolution = 2000;
                    float aspectRatio = (float)(uiWidth / uiHeight);

                    double scale = Math.Min(resolution / uiWidth, resolution / uiHeight);
                    int canvasWidth = (int)Math.Round(uiWidth * scale);
                    int canvasHeight = (int)Math.Round(uiHeight * scale);

                    float startWidth = 5f;
                    if (App.captureWindow.configMenu.IsVisible)
                        startWidth = 1.5f;
                    else if (uiWidth >= uiHeight)
                        startWidth = 4f;

                    float baseWidthInMeters = startWidth / (16.0f / 9.0f);
                    overlay.SetOverlayWidthInMeters(dashboardHandle, baseWidthInMeters * aspectRatio);

                    HmdVector2_t mouseScale = new() { v0 = (float)uiWidth, v1 = (float)uiHeight };
                    overlay.SetOverlayMouseScale(dashboardHandle, ref mouseScale);

                    lock (d3dLock)
                    {
                        if (overlayTex == null || overlayTex.Description.Width != (uint)canvasWidth || overlayTex.Description.Height != (uint)canvasHeight)
                        {
                            overlayTex?.Dispose();
                            stagingTex?.Dispose();

                            var desc = new Texture2DDescription
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

                        var box = d3dContext!.Map(stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);

                        // Process GDI Bitmap into DirectX Texture
                        using var scaledBmp = new System.Drawing.Bitmap(canvasWidth, canvasHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        using (var g = System.Drawing.Graphics.FromImage(scaledBmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.DrawImage(captureBmp, 0, 0, canvasWidth, canvasHeight);
                        }

                        var bData = scaledBmp.LockBits(new System.Drawing.Rectangle(0, 0, canvasWidth, canvasHeight), ImageLockMode.ReadOnly, scaledBmp.PixelFormat);
                        for (int y = 0; y < canvasHeight; y++)
                        {
                            Marshal.Copy(bData.Scan0 + y * bData.Stride, rowBuffer!, 0, canvasWidth * 4);
                            Marshal.Copy(rowBuffer!, 0, box.DataPointer + (nint)((long)y * box.RowPitch), canvasWidth * 4);
                        }
                        scaledBmp.UnlockBits(bData);

                        d3dContext.Unmap(stagingTex!, 0);
                        d3dContext.CopyResource(overlayTex!, stagingTex!);

                        var tex = new Texture_t
                        {
                            handle = overlayTex!.NativePointer,
                            eType = ETextureType.DirectX,
                            eColorSpace = EColorSpace.Auto
                        };

                        overlay.SetOverlayTexture(dashboardHandle, ref tex);

                        isOverlayDirty = false;
                    }


                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SteamVR] Frame rendering crashed: {ex.Message}");
                }
            }
            else
                overlay.ClearOverlayTexture(dashboardHandle);
        }

        public void Dispose()
        {
            if (isInitialized)
            {
                var overlay = OpenVR.Overlay;
                if (overlay != null)
                {
                    if (dashboardHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(dashboardHandle);

                    if (thumbnailHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(thumbnailHandle);
                }

                StopPolling();

                overlayTex?.Dispose();
                stagingTex?.Dispose();
                d3dContext?.Dispose();
                d3dDevice?.Dispose();

                dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
                thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
                isInitialized = false;
            }
        }
    }
}