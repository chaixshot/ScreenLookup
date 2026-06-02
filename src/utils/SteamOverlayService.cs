using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ScreenLookup.src.utils
{
    public class SteamOverlayService : IDisposable
    {
        // Win32 API imports for injecting mouse inputs into OS space via VR controllers
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private ulong _dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
        private ulong _thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
        private bool _isInitialized = false;

        // Direct3D11 Native Interfaces
        private ID3D11Device? _d3dDevice;
        private ID3D11DeviceContext? _d3dContext;
        private ID3D11Texture2D? _overlayTex;
        private ID3D11Texture2D? _stagingTex;
        private ID3D11Texture2D? _transparentFallbackTex; // 16x16 fully transparent texture

        private readonly object _d3dLock = new();

        private Window parentWindow;
        private FrameworkElement uiElement;

        private CancellationTokenSource? cts;
        private Task? pollTask;
        private bool running;

        // High-Performance Pipeline Caching 
        private RenderTargetBitmap? _cachedRenderTarget;
        private int _cachedWidth = 0;
        private int _cachedHeight = 0;
        private bool _isOverlayDirty = true;
        private bool _wasWindowVisibleLastFrame = true;

        public bool IsInitialized => _isInitialized;

        public SteamOverlayService(Window parentWindow, FrameworkElement uiElement)
        {
            SetWindow(parentWindow, uiElement);

            if (Initialize())
                StartPolling();
        }

        public void SetWindow(Window parentWindow, FrameworkElement uiElement)
        {
            this.parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            this.uiElement = uiElement ?? throw new ArgumentNullException(nameof(uiElement));

            // Mark dirty only when explicit layout structural updates or sizing switches pulse
            this.uiElement.LayoutUpdated += (s, e) => _isOverlayDirty = true;
            this.uiElement.SizeChanged += (s, e) => _isOverlayDirty = true;
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
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out _d3dDevice, out _d3dContext);

            var overlayError = overlay.CreateDashboardOverlay("ScreenLookup.Overlay", "ScreenLookup", ref _dashboardHandle, ref _thumbnailHandle);
            if (overlayError != EVROverlayError.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Dashboard overlay creation failed: {overlayError}");
                return false;
            }

            string thumbnailPath = Path.Combine(Environment.CurrentDirectory, "src\\images", "applicationIcon.png");
            if (!string.IsNullOrEmpty(thumbnailPath) && System.IO.File.Exists(thumbnailPath))
                overlay.SetOverlayFromFile(_thumbnailHandle, thumbnailPath);

            // Handle alpha transparency cleanly (WPF targets Premultiplied Alpha layout outputs)
            overlay.SetOverlayFlag(_dashboardHandle, VROverlayFlags.IsPremultiplied, true);

            // Generate the stable fallback transparent texture
            InitializeTransparentFallbackTexture();

            _isInitialized = true;
            return true;
        }

        /// <summary>
        /// Generates a fully transparent 16x16 pixel texture on the GPU.
        /// </summary>
        private unsafe void InitializeTransparentFallbackTexture()
        {
            if (_d3dDevice == null || _transparentFallbackTex != null) return;

            const int side = 16;
            var desc = new Texture2DDescription
            {
                Width = side,
                Height = side,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.R8G8B8A8_UNorm, // SteamVR preferred standard pixel format
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource
            };

            // Fully Transparent Color Array Allocation (R:0, G:0, B:0, A:0)
            uint[] pixels = new uint[side * side];
            uint transparentColorValue = 0x00000000;
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = transparentColorValue;
            }

            fixed (uint* pPixels = pixels)
            {
                SubresourceData initData = new()
                {
                    DataPointer = (nint)pPixels,
                    RowPitch = side * 4,
                    SlicePitch = side * side * 4
                };

                _transparentFallbackTex = _d3dDevice.CreateTexture2D(desc, [initData]);
            }
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
                parentWindow.Dispatcher.Invoke(RenderFrame);

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
            if (!_isInitialized || _dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;
            if (!App.captureWindow.IsVisible) return;

            VREvent_t vrEvent = new();
            uint eventSize = (uint)Marshal.SizeOf<VREvent_t>();

            while (OpenVR.Overlay.PollNextOverlayEvent(_dashboardHandle, ref vrEvent, eventSize))
            {
                switch ((EVREventType)vrEvent.eventType)
                {
                    case EVREventType.VREvent_MouseMove:
                        double vrX = vrEvent.data.mouse.x;
                        double vrY = uiElement.ActualHeight - vrEvent.data.mouse.y;

                        parentWindow.Dispatcher.Invoke(() =>
                        {
                            if (uiElement.IsVisible)
                            {
                                Point screenPoint = uiElement.PointToScreen(new Point(vrX, vrY));
                                SetCursorPos((int)screenPoint.X, (int)screenPoint.Y);
                                _isOverlayDirty = true;
                            }
                        });
                        break;

                    case EVREventType.VREvent_MouseButtonDown:
                        if (vrEvent.data.mouse.button == (uint)EVRMouseButton.Left)
                        {
                            parentWindow.Dispatcher.Invoke(() => mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0));
                            _isOverlayDirty = true;
                        }
                        break;

                    case EVREventType.VREvent_MouseButtonUp:
                        if (vrEvent.data.mouse.button == (uint)EVRMouseButton.Left)
                        {
                            parentWindow.Dispatcher.Invoke(() => mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0));
                            _isOverlayDirty = true;
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
            if (overlay == null || !_isInitialized || _dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            bool isCurrentlyVisible = App.captureWindow.IsVisible;

            // Force a render pass on visibility transitions
            if (isCurrentlyVisible != _wasWindowVisibleLastFrame)
            {
                _isOverlayDirty = true;
                _wasWindowVisibleLastFrame = isCurrentlyVisible;
            }

            // High-Speed short-circuit execution bail path if frame updates are clean
            if (!_isOverlayDirty) return;

            try
            {
                if (isCurrentlyVisible)
                {
                    double uiWidth = uiElement.ActualWidth;
                    double uiHeight = uiElement.ActualHeight;

                    if (uiWidth <= 0 || uiHeight <= 0) return;

                    float resolution = 2000;
                    float aspectRatio = (float)(uiWidth / uiHeight);

                    double scale = Math.Min(resolution / uiWidth, resolution / uiHeight);
                    int canvasWidth = (int)Math.Round(uiWidth * scale);
                    int canvasHeight = (int)Math.Round(uiHeight * scale);

                    float startWidth = 5f;
                    if (uiElement == App.captureWindow.configMenu)
                        startWidth = 1.5f;
                    else if (uiWidth >= uiHeight)
                        startWidth = 4f;

                    float baseDashboardHeightMeters = startWidth / (16.0f / 9.0f);
                    overlay.SetOverlayWidthInMeters(_dashboardHandle, baseDashboardHeightMeters * aspectRatio);

                    HmdVector2_t mouseScale = new() { v0 = (float)uiWidth, v1 = (float)uiHeight };
                    overlay.SetOverlayMouseScale(_dashboardHandle, ref mouseScale);

                    // Re-instantiate layout render cache context only on dimensional variance changes
                    if (_cachedRenderTarget == null || _cachedWidth != canvasWidth || _cachedHeight != canvasHeight)
                    {
                        _cachedRenderTarget = new RenderTargetBitmap(canvasWidth, canvasHeight, 96, 96, PixelFormats.Pbgra32);
                        _cachedWidth = canvasWidth;
                        _cachedHeight = canvasHeight;
                    }

                    DrawingVisual drawingVisual = new();
                    using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                    {
                        double scaleX = (double)canvasWidth / uiWidth;
                        double scaleY = (double)canvasHeight / uiHeight;
                        double finalScale = Math.Min(scaleX, scaleY);

                        double renderWidth = uiWidth * finalScale;
                        double renderHeight = uiHeight * finalScale;

                        double offsetX = (canvasWidth - renderWidth) / 2.0;
                        double offsetY = (canvasHeight - renderHeight) / 2.0;

                        drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
                        drawingContext.PushTransform(new ScaleTransform(finalScale, finalScale));

                        drawingContext.DrawRectangle(new VisualBrush(uiElement), null, new Rect(0, 0, uiWidth, uiHeight));

                        // Composite Popup overlays safely
                        try
                        {
                            Point uiScreenPos = uiElement.PointToScreen(new Point(0, 0));
                            foreach (PresentationSource source in PresentationSource.CurrentSources)
                            {
                                Visual root = source.RootVisual;
                                if (root != null && root.GetType().Name == "PopupRoot" && root is FrameworkElement element && element.IsVisible)
                                {
                                    Point popupScreenPos = element.PointToScreen(new Point(0, 0));
                                    double relX = popupScreenPos.X - uiScreenPos.X;
                                    double relY = popupScreenPos.Y - uiScreenPos.Y;

                                    drawingContext.DrawRectangle(new VisualBrush(root), null, new Rect(relX, relY, element.ActualWidth, element.ActualHeight));
                                }
                            }
                        }
                        catch (InvalidOperationException) { }

                        // Composite UI layout AdornerLayers
                        AdornerLayer adornerLayer = AdornerLayer.GetAdornerLayer(uiElement);
                        if (adornerLayer != null)
                        {
                            Point offset = uiElement.TranslatePoint(new Point(0, 0), adornerLayer);
                            VisualBrush adornerBrush = new VisualBrush(adornerLayer)
                            {
                                Viewbox = new Rect(offset.X, offset.Y, uiWidth, uiHeight),
                                ViewboxUnits = BrushMappingMode.Absolute
                            };
                            drawingContext.DrawRectangle(adornerBrush, null, new Rect(0, 0, uiWidth, uiHeight));
                        }
                    }

                    _cachedRenderTarget.Render(drawingVisual);

                    lock (_d3dLock)
                    {
                        if (_overlayTex == null || _overlayTex.Description.Width != (uint)canvasWidth || _overlayTex.Description.Height != (uint)canvasHeight)
                        {
                            _overlayTex?.Dispose();
                            _stagingTex?.Dispose();

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
                            _overlayTex = _d3dDevice!.CreateTexture2D(desc);

                            desc.Usage = ResourceUsage.Staging;
                            desc.BindFlags = BindFlags.None;
                            desc.CPUAccessFlags = CpuAccessFlags.Write;
                            _stagingTex = _d3dDevice.CreateTexture2D(desc);
                        }

                        var box = _d3dContext!.Map(_stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                        int bufferSize = (int)(box.RowPitch * canvasHeight);

                        // Direct zero-copy blitting straight into native mapped memory address pointer blocks
                        _cachedRenderTarget.CopyPixels(
                            new Int32Rect(0, 0, canvasWidth, canvasHeight),
                            box.DataPointer,
                            bufferSize,
                            (int)box.RowPitch
                        );

                        _d3dContext.Unmap(_stagingTex!, 0);
                        _d3dContext.CopyResource(_overlayTex!, _stagingTex!);

                        var tex = new Texture_t
                        {
                            handle = _overlayTex!.NativePointer,
                            eType = ETextureType.DirectX,
                            eColorSpace = EColorSpace.Auto
                        };
                        overlay.SetOverlayTexture(_dashboardHandle, ref tex);
                    }
                }
                else
                {
                    // WINDOW HIDDEN FALLBACK PATH: Submit the native fully transparent texture
                    lock (_d3dLock)
                    {
                        if (_transparentFallbackTex != null)
                        {
                            // Reset dimensions slightly to prevent compositor rendering scale anomalies
                            overlay.SetOverlayWidthInMeters(_dashboardHandle, 2.5f);
                            HmdVector2_t mouseScale = new() { v0 = 16f, v1 = 16f };
                            overlay.SetOverlayMouseScale(_dashboardHandle, ref mouseScale);

                            var tex = new Texture_t
                            {
                                handle = _transparentFallbackTex.NativePointer,
                                eType = ETextureType.DirectX,
                                eColorSpace = EColorSpace.Auto
                            };
                            overlay.SetOverlayTexture(_dashboardHandle, ref tex);
                        }
                    }
                }

                _isOverlayDirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Frame rendering crashed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                var overlay = OpenVR.Overlay;
                if (overlay != null)
                {
                    if (_dashboardHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(_dashboardHandle);

                    if (_thumbnailHandle != OpenVR.k_ulOverlayHandleInvalid)
                        overlay.DestroyOverlay(_thumbnailHandle);
                }

                StopPolling();

                _overlayTex?.Dispose();
                _stagingTex?.Dispose();
                _transparentFallbackTex?.Dispose(); // Release the static asset context
                _d3dContext?.Dispose();
                _d3dDevice?.Dispose();

                _dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
                _thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
                _isInitialized = false;
            }
        }
    }
}