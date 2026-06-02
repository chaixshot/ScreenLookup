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
        // Win32 imports for simulating hardware mouse inputs from VR controller space
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;

        private ulong _dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
        private ulong _thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
        private bool _isInitialized = false;

        private ID3D11Device? _d3dDevice;
        private ID3D11DeviceContext? _d3dContext;
        private ID3D11Texture2D? _overlayTex;
        private ID3D11Texture2D? _stagingTex;
        private readonly object _d3dLock = new();

        private readonly Window parentWindow;
        private readonly FrameworkElement uiElement;

        private CancellationTokenSource? cts;
        private Task? pollTask;
        private bool running;

        // --- PERFORMANCE CACHE FIELDS ---
        private RenderTargetBitmap? _cachedRenderTarget;
        private int _cachedWidth = 0;
        private int _cachedHeight = 0;
        private bool _isOverlayDirty = true; // State-driven rendering flag

        public bool IsInitialized => _isInitialized;

        public SteamOverlayService(Window parentWindow, FrameworkElement uiElement)
        {
            this.parentWindow = parentWindow ?? throw new ArgumentNullException(nameof(parentWindow));
            this.uiElement = uiElement ?? throw new ArgumentNullException(nameof(uiElement));

            // HIGH PERFORMANCE: Only redraw the overlay when WPF layout updates, sizes change, or animations fire.
            this.uiElement.LayoutUpdated += (s, e) => _isOverlayDirty = true;
            this.uiElement.SizeChanged += (s, e) => _isOverlayDirty = true;

            if (Initialize(Path.Combine(Environment.CurrentDirectory, "src\\images", "applicationIcon.png")))
            {
                StartPolling();
            }
        }

        /// <summary>
        /// Connects to SteamVR and spins up the dashboard overlay surfaces.
        /// </summary>
        private bool Initialize(string thumbnailPath = null)
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

            // Initialize Direct3D11 for GPU-side texture submission
            D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out _d3dDevice, out _d3dContext);

            var overlayError = overlay.CreateDashboardOverlay("com.company.wpf.overlay", "WPF SteamVR Dashboard", ref _dashboardHandle, ref _thumbnailHandle);
            if (overlayError != EVROverlayError.None)
            {
                System.Diagnostics.Debug.WriteLine($"[SteamVR] Dashboard overlay creation failed: {overlayError}");
                return false;
            }

            if (!string.IsNullOrEmpty(thumbnailPath) && System.IO.File.Exists(thumbnailPath))
            {
                overlay.SetOverlayFromFile(_thumbnailHandle, thumbnailPath);
            }

            // Ensure alpha transparency is handled correctly (WPF uses premultiplied alpha)
            overlay.SetOverlayFlag(_dashboardHandle, VROverlayFlags.IsPremultiplied, true);

            _isInitialized = true;
            return true;
        }

        #region Polling Loop
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
                // Process laser pointer tracking and inputs
                ProcessInput();

                // Draw the current visual frames out to VR space (Only runs if dirty flag is true)
                parentWindow.Dispatcher.Invoke(RenderFrame);

                // PERFORMANCE: Stepping back to 16ms (~60Hz) reduces layout cycle thrashing significantly.
                await Task.Delay(16, ct);
            }
        }
        #endregion

        /// <summary>
        /// Translates OpenVR hardware controller actions directly into UI coordinates and Win32 inputs.
        /// </summary>
        public void ProcessInput()
        {
            if (!_isInitialized || _dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;

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
                                _isOverlayDirty = true; // Mark dirty to capture visual hovers instantly
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
        /// Captures the targeted WPF element layouts and pushes the rasterized buffer handle directly to SteamVR.
        /// </summary>
        public void RenderFrame()
        {
            var overlay = OpenVR.Overlay;
            if (overlay == null || !_isInitialized || _dashboardHandle == OpenVR.k_ulOverlayHandleInvalid) return;

            // PERFORMANCE CAP: If nothing in the UI layout updated, completely bypass rendering overhead.
            if (!_isOverlayDirty) return;

            try
            {
                double uiWidth = uiElement.ActualWidth;
                double uiHeight = uiElement.ActualHeight;

                if (uiWidth <= 0 || uiHeight <= 0) return;

                // PERFORMANCE LIMIT: Lowering maximum scaling bounds from 3000 to 1280.
                // This eliminates the bottleneck of processing bloated pixel byte buffers.
                float resolution = 1280;
                float aspectRatio = (float)(uiWidth / uiHeight);

                double scale = Math.Min(resolution / uiWidth, resolution / uiHeight);
                int canvasWidth = (int)Math.Round(uiWidth * scale);
                int canvasHeight = (int)Math.Round(uiHeight * scale);

                float startWidth = 5f;

                if (uiElement == App.captureWindow.configMenu)
                    startWidth = 1.5f;
                else if (uiWidth >= uiHeight)
                    startWidth = 4f;

                // Dynamically adjust SteamVR physical width for landscape 
                float baseDashboardHeightMeters = startWidth / (16.0f / 9.0f);
                overlay.SetOverlayWidthInMeters(_dashboardHandle, baseDashboardHeightMeters * aspectRatio);

                HmdVector2_t mouseScale = new() { v0 = (float)uiWidth, v1 = (float)uiHeight };
                overlay.SetOverlayMouseScale(_dashboardHandle, ref mouseScale);

                // OPTIMIZATION: Reuse the RenderTargetBitmap instance instead of reallocating it per frame
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

                    // Draw the main UI content
                    drawingContext.DrawRectangle(new VisualBrush(uiElement), null, new Rect(0, 0, uiWidth, uiHeight));

                    // Composite Popups (Only include this loop if your layout actively uses WPF Popups/ComboBox flyouts)
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
                    catch (InvalidOperationException) { /* Handle elements detaching mid-iteration */ }

                    // Composite Adorners (Shadows, validations, focus boxes)
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
                    // Ensure textures are allocated and match layout bounds
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

                    // Map the Direct3D staging surface pointer buffer
                    var box = _d3dContext!.Map(_stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                    int bufferSize = (int)(box.RowPitch * canvasHeight);

                    // OPTIMIZATION: Dump pixels straight from RenderTargetBitmap into the Direct3D Staging Buffer Address!
                    // Removes intermediate arrays and WriteableBitmap blits entirely.
                    _cachedRenderTarget.CopyPixels(
                        new Int32Rect(0, 0, canvasWidth, canvasHeight),
                        box.DataPointer,
                        bufferSize,
                        (int)box.RowPitch
                    );

                    _d3dContext.Unmap(_stagingTex!, 0);
                    _d3dContext.CopyResource(_overlayTex!, _stagingTex!);

                    // Submit texture handle directly to the OpenVR Overlay
                    var tex = new Texture_t
                    {
                        handle = _overlayTex!.NativePointer,
                        eType = ETextureType.DirectX,
                        eColorSpace = EColorSpace.Auto
                    };
                    overlay.SetOverlayTexture(_dashboardHandle, ref tex);
                }

                // Reset the state tracker at the end of a successful render cycle
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
                _d3dContext?.Dispose();
                _d3dDevice?.Dispose();

                _dashboardHandle = OpenVR.k_ulOverlayHandleInvalid;
                _thumbnailHandle = OpenVR.k_ulOverlayHandleInvalid;
                _isInitialized = false;
            }
        }
    }
}