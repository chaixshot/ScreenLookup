using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace ScreenLookup.src.utils
{
    public class FrameShotService : IDisposable
    {
        public static FrameShotService? Instance { get; private set; }

        private const int FRAME_TEX_W = 1024;
        private const int FRAME_TEX_H = 1024;

        // State
        public bool IsConnected { get; private set; }
        public bool IsFraming { get; private set; }
        public string? LastError { get; private set; }
        public SteamOverlayService? SteamOverlay { get; set; }

        // Events
        public event Action<object>? OnStateUpdate;
        public event Action? OnVRQuit; // Retained if needed elsewhere in the app wrapper
        public event Action<Bitmap?, bool>? OnPhotoSaved;

        // Dependencies & Input pipeline
        private readonly VRInputService inputService;
        private CVRSystem? vrSystem;
        private ulong overlayHandle;
        private CancellationTokenSource? cts;
        private Task? processTask;
        private bool running;
        private readonly Action<string> log;

        // Internal Input Processing Mirrors
        private bool isButtonComboInRage;
        private bool isButtonComboPressed;
        private bool leftHeld;
        private bool rightHeld;
        private bool leftHeldPrev;
        private bool rightHeldPrev;

        // Geometry cache
        private Vector3 lastLeftPos;
        private Vector3 lastRightPos;
        private float lastFrameWidth;
        private float lastFrameHeight;
        private Quaternion lastHmdRot;

        // D3D11
        private ID3D11Device? d3dDevice;
        private ID3D11DeviceContext? d3dContext;
        private readonly object d3dLock = new();

        private ID3D11Texture2D? overlayTex;
        private ID3D11Texture2D? stagingTex;
        private ID3D11Texture2D? mirrorStaging;
        private ID3D11Texture2D? mirrorTexCached;
        private ID3D11ShaderResourceView? mirrorSrvObj;
        private EVREye currentMirrorEye = (EVREye)(-1);
        private IntPtr mirrorSrv = IntPtr.Zero;

        // Rendering resources
        private readonly byte[] rowBuffer = new byte[FRAME_TEX_W * 4];
        private Bitmap? frameBitmap;
        private int mirrorW;
        private int mirrorH;
        private Vector3 hmdRight, hmdUp, hmdFwd;


        public ID3D11Device? Device => d3dDevice;
        public ID3D11DeviceContext? Context => d3dContext;

        public FrameShotService(Action<string> log)
        {
            this.log = log;
            inputService = new VRInputService();
            Instance = this;
        }

        public bool Connect()
        {
            if (IsConnected)
                return true;

            try
            {
                EVRInitError err = EVRInitError.None;
                vrSystem = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
                if (err != EVRInitError.None)
                {
                    LastError = $"OpenVR init failed: {err}";
                    return false;
                }

                OpenVR.Overlay.CreateOverlay("ScreenLookup.FrameShot", "ScreenLookup FrameShot", ref overlayHandle);
                OpenVR.Overlay.SetOverlayAlpha(overlayHandle, 1.0f);

                D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None, [FeatureLevel.Level_11_0], out d3dDevice, out d3dContext);

                overlayTex = d3dDevice!.CreateTexture2D(new Texture2DDescription
                {
                    Width = FRAME_TEX_W,
                    Height = FRAME_TEX_H,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                });

                stagingTex = d3dDevice.CreateTexture2D(new Texture2DDescription
                {
                    Width = FRAME_TEX_W,
                    Height = FRAME_TEX_H,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    CPUAccessFlags = CpuAccessFlags.Write,
                });

                frameBitmap = new Bitmap(FRAME_TEX_W, FRAME_TEX_H, PixelFormat.Format32bppArgb);

                IsConnected = true;
                EmitState();
                StartThread();

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public void Disconnect()
        {
            if (!IsConnected)
                return;

            if (processTask != null && !processTask.IsCompleted && Task.CurrentId != processTask.Id) // Avoid deadlock if Disconnect is called from the polling thread (e.g. during a VR Quit event)
                processTask.Wait(TimeSpan.FromMilliseconds(500));

            StopThread();

            lock (d3dLock)
            {
                mirrorStaging?.Dispose();
                mirrorTexCached?.Dispose();
                mirrorSrvObj?.Dispose();
                mirrorStaging = null;

                if (mirrorSrv != IntPtr.Zero)
                    OpenVR.Compositor?.ReleaseMirrorTextureD3D11(mirrorSrv);

                stagingTex?.Dispose();
                overlayTex?.Dispose();
                mirrorSrv = IntPtr.Zero;
                currentMirrorEye = (EVREye)(-1);

                d3dContext?.Dispose();
                d3dDevice?.Dispose();
            }

            OpenVR.Shutdown();
            SteamOverlay?.Dispose();
            SteamOverlay = null;
            IsConnected = false;
            EmitState();
        }

        public void StartThread()
        {
            if (running)
                return;

            // Initialise action handles now that the manifest is registered (by SteamOverlayService).
            // Both services share the same OpenVR session, so no second SetActionManifestPath needed.
            inputService.InitActionHandles();

            cts = new CancellationTokenSource();
            running = true;
            processTask = ThreadAsync(cts.Token);
        }

        public void StopThread()
        {
            running = false;
            cts?.Cancel();
        }

        private async Task ThreadAsync(CancellationToken ct)
        {
            float refreshRate = VRInputService.GetHmdRefreshRate();
            int delay = (int)(1000 / refreshRate);

            while (!ct.IsCancellationRequested)
            {
                ProcessThread();
                await Task.Delay(delay, ct);
            }
        }

        private void ProcessThread()
        {
            CVRSystem system = OpenVR.System;
            if (system == null || !IsConnected)
                return;

            // Detect SteamVR quit events to handle external shutdown gracefully and prevent app-wide exit
            VREvent_t vrEvent = new();
            while (system.PollNextEvent(ref vrEvent, (uint)Marshal.SizeOf<VREvent_t>()))
            {
                if (vrEvent.eventType == (uint)EVREventType.VREvent_Quit)
                {
                    LastError = "SteamVR Disconnected";
                    Disconnect();
                    OnVRQuit?.Invoke();
                    return;
                }
            }

            if (SteamOverlay?.IsVisible == true)
            {
                if (IsFraming)
                {
                    OpenVR.Overlay.HideOverlay(overlayHandle);
                    IsFraming = false;
                }
                isButtonComboPressed = false;
                isButtonComboInRage = false;
                leftHeld = false;
                rightHeld = false;
                return;
            }

            inputService.UpdatePosesAndIndices();

            // Update IVRInput action state for this tick (grip + trigger reads below depend on this)
            inputService.UpdateActionState();

            leftHeldPrev = leftHeld;
            rightHeldPrev = rightHeld;
            leftHeld = inputService.IsActionHeld(inputService.GripLeftHandle);
            rightHeld = inputService.IsActionHeld(inputService.GripRightHandle);

            // Evaluate framing gestures and coordinate collection
            Vector3 leftCoords = Vector3.Zero;
            Vector3 rightCoords = Vector3.Zero;
            bool wasFraming = IsFraming;

            if (leftHeld && rightHeld)
            {
                if (!isButtonComboPressed)
                {
                    isButtonComboInRage = inputService.TryGetHandPositions(App.setting.ActivationRadius, out leftCoords, out rightCoords);
                    isButtonComboPressed = true;
                }
            }
            else
            {
                isButtonComboPressed = false;
                isButtonComboInRage = false;
            }

            // Update active state based on proximity range check
            IsFraming = isButtonComboInRage;

            // Execute UI updates, audio cues, and rendering behaviors
            if (IsFraming)
            {
                if (inputService.IsActionJustPressed(inputService.TriggerLeftHandle) || inputService.IsActionJustReleased(inputService.TriggerLeftHandle))
                    inputService.TriggerHapticPulse(inputService.LeftControllerIdx, 4000);
                else if (inputService.IsActionJustPressed(inputService.TriggerRightHandle))
                {
                    inputService.TriggerHapticPulseBoth(50000);
                    App.captureWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        App.captureWindow.OpenWindow();
                    }));
                }
                else if (!wasFraming)
                {
                    AppUtilities.PlaySound("ready.wav");
                    inputService.TriggerHapticPulseBoth(50000);
                    EnsureMirrorPipeline();
                }

                UpdateFrameAndRender(leftCoords, rightCoords);
            }
            else if (wasFraming)
            {
                OpenVR.Overlay.HideOverlay(overlayHandle);

                // Screenshot condition: User released RIGHT grip while continuing to hold LEFT grip
                if (rightHeldPrev && !rightHeld && leftHeld)
                {
                    AppUtilities.PlaySound("screenshot.wav");
                    inputService.TriggerHapticPulseBoth(50000);

                    App.captureWindow.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        // Cache trigger button state immediately before thread delays alter input metrics
                        bool leftTriggerHeld = inputService.IsActionHeld(inputService.TriggerLeftHandle);

                        await Task.Delay(100); // Allow OpenVR overlay a frame to hide completely

                        CaptureAndSave(leftTriggerHeld);
                    }));
                }
            }
        }

        private void UpdateFrameAndRender(Vector3 L_Coords, Vector3 R_Coords)
        {
            uint hmdIdx = OpenVR.k_unTrackedDeviceIndex_Hmd;
            TrackedDevicePose_t[]? poses = inputService.Poses;

            if (!poses[inputService.LeftControllerIdx].bPoseIsValid ||
                !poses[inputService.RightControllerIdx].bPoseIsValid ||
                !poses[hmdIdx].bPoseIsValid)
                return;

            if (L_Coords == Vector3.Zero && R_Coords == Vector3.Zero)
            {
                L_Coords = VRInputService.PosFromMatrix(poses[inputService.LeftControllerIdx].mDeviceToAbsoluteTracking);
                R_Coords = VRInputService.PosFromMatrix(poses[inputService.RightControllerIdx].mDeviceToAbsoluteTracking);
            }

            HmdMatrix34_t hmdM = poses[hmdIdx].mDeviceToAbsoluteTracking;
            Quaternion hmdRot = VRInputService.RotFromMatrix(hmdM);
            lastHmdRot = hmdRot;

            // Calculate how much the HMD is tilted relative to world up
            Vector3 hmdUpLive = Vector3.Transform(Vector3.UnitY, hmdRot);
            float tiltAmount = 1.0f - MathF.Max(0, Vector3.Dot(hmdUpLive, Vector3.UnitY));
            bool shouldTilt = App.setting.UseHmdRotations && (tiltAmount > App.setting.HmdRotationThreshold);

            Vector3 hmdFwdLive = Vector3.Transform(-Vector3.UnitZ, hmdRot);
            Vector3 hmdRightLive = Vector3.Transform(Vector3.UnitX, hmdRot);

            if (shouldTilt)
            {
                hmdFwd = hmdFwdLive;
                hmdRight = hmdRightLive;
                hmdUp = hmdUpLive;
            }
            else
            {
                hmdFwd = hmdFwdLive;
                Vector3 right = Vector3.Cross(hmdFwd, Vector3.UnitY);
                hmdRight = (right.LengthSquared() < 1e-6f) ? hmdRightLive : Vector3.Normalize(right); // Fallback to live right if Fwd is too close to vertical
                hmdUp = Vector3.Normalize(Vector3.Cross(hmdRight, hmdFwd));
            }

            L_Coords += (hmdRight * ((float)App.setting.FrameOffset / 100f)); // Adjust L_Coords and R_Coords positions to expand the frame slightly beyond controller center
            R_Coords -= (hmdRight * ((float)App.setting.FrameOffset / 100f)); // Left controller moves LEFT (negative right vector), Right controller moves RIGHT (positive right vector)

            Vector3 center = (L_Coords + R_Coords) * 0.5f;

            // Calculate dimensions based on HMD-aligned axes to support portrait/landscape
            float widthM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R_Coords - L_Coords, hmdRight)));
            float heightM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R_Coords - L_Coords, hmdUp)));

            lastLeftPos = L_Coords;
            lastRightPos = R_Coords;
            lastFrameWidth = widthM;
            lastFrameHeight = heightM;

            // Calculate draw dimensions based on the aspect ratio of the physical frame
            DrawFrameTexture(center, widthM, heightM, out float totalWidthM, out Vector3 overlayCenter);

            OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, totalWidthM);

            HmdMatrix34_t transform = new HmdMatrix34_t { m0 = hmdRight.X, m1 = hmdUp.X, m2 = -hmdFwd.X, m3 = overlayCenter.X, m4 = hmdRight.Y, m5 = hmdUp.Y, m6 = -hmdFwd.Y, m7 = overlayCenter.Y, m8 = hmdRight.Z, m9 = hmdUp.Z, m10 = -hmdFwd.Z, m11 = overlayCenter.Z };
            OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform);
            OpenVR.Overlay.ShowOverlay(overlayHandle);
        }

        private void DrawFrameTexture(Vector3 center, float widthM, float heightM, out float totalWidthM, out Vector3 overlayCenter)
        {
            totalWidthM = widthM;
            overlayCenter = center;

            if (frameBitmap == null || d3dContext == null || stagingTex == null || overlayTex == null)
                return;

            bool showHint = widthM <= 0.2f && heightM <= 0.2f;

            string[] hintLines =
            [
                "- Release Right Grip to capture, Left Grip to cancel.",
                "- Holding Left Trigger, then capture to show options.",
                "- Press Right Trigger to show the last result."
            ];

            const float fontSize = 0.007f;
            const float paddingLeft = 0.010f;
            const float paddingRight = 0.020f;
            const float paddingTop = 0.008f;
            const float paddingBottom = 0.012f;
            const float lineSpacing = 0.003f;
            const float marginY = 0.012f;
            const float cornerRadius = 0.004f;

            float boxWidthM = 0f, boxHeightM = 0f, hintRegionHM = 0f;

            if (showHint)
            {
                float maxLineAspect = 0f;
                using (Graphics gTemp = Graphics.FromImage(frameBitmap))
                {
                    gTemp.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    using Font refFont = new(App.setting.FontFace, 16f, FontStyle.Regular);
                    float refLineHeight = refFont.GetHeight(gTemp);
                    foreach (string line in hintLines)
                    {
                        float aspect = gTemp.MeasureString(line, refFont).Width / refLineHeight;
                        if (aspect > maxLineAspect) maxLineAspect = aspect;
                    }
                }

                boxWidthM = maxLineAspect * fontSize + paddingLeft + paddingRight;
                boxHeightM = hintLines.Length * fontSize + (hintLines.Length - 1) * lineSpacing + paddingTop + paddingBottom;
                hintRegionHM = boxHeightM + marginY;
                totalWidthM = Math.Max(widthM, boxWidthM);
            }

            float totalHeightM = heightM + hintRegionHM;
            float texScale = MathF.Min(MathF.Min((float)FRAME_TEX_W / totalWidthM, (float)FRAME_TEX_H / totalHeightM), 3000f);

            int totalPixelW = Math.Min(FRAME_TEX_W, (int)MathF.Ceiling(totalWidthM * texScale));
            int totalPixelH = Math.Min(FRAME_TEX_H, (int)MathF.Ceiling(totalHeightM * texScale));
            int drawW = Math.Max(16, (int)MathF.Round(widthM * texScale));
            int drawH = Math.Max(16, (int)MathF.Round(heightM * texScale));
            int frameX = 0, frameY = (int)MathF.Ceiling(hintRegionHM * texScale);

            using (Graphics g = Graphics.FromImage(frameBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                if (showHint)
                {
                    int boxPixelW = (int)MathF.Ceiling(boxWidthM * texScale);
                    int boxPixelH = (int)MathF.Ceiling(boxHeightM * texScale);
                    RectangleF boxRect = new(1f, 1f, boxPixelW - 2f, boxPixelH - 2f);

                    using GraphicsPath boxPath = CreateRoundedRectanglePath(boxRect, cornerRadius * texScale);
                    using SolidBrush bgBrush = new(Color.FromArgb(200, 20, 20, 28));
                    using Pen borderPen = new(Color.FromArgb(180, 218, 96, 255), Math.Max(1f, 0.0015f * texScale));
                    using SolidBrush textBrush = new(Color.FromArgb(245, 245, 245));
                    using Font font = new("Segoe UI", Math.Max(8f, fontSize * texScale * 0.75f), FontStyle.Regular);

                    g.FillPath(bgBrush, boxPath);
                    g.DrawPath(borderPen, boxPath);

                    float textX = 1f + paddingLeft * texScale;
                    float currentY = 1f + paddingTop * texScale;
                    float lineStep = (fontSize + lineSpacing) * texScale;

                    foreach (string line in hintLines)
                    {
                        g.DrawString(line, font, textBrush, textX, currentY);
                        currentY += lineStep;
                    }
                }

                float penThickness = Math.Max(1f, (4f / 1024f) * texScale);
                float inset = penThickness / 2f;
                using Pen pen = new(Color.FromArgb(255, 218, 96, 255), penThickness);
                g.DrawRectangle(pen, frameX + inset, frameY + inset, drawW - penThickness - 1, drawH - penThickness - 1);
            }

            Rectangle rect = new(0, 0, FRAME_TEX_W, FRAME_TEX_H);
            BitmapData? bData = null;
            try
            {
                bData = frameBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int rowBytes = FRAME_TEX_W * 4;
                int srcStride = bData.Stride;
                lock (d3dLock)
                {
                    MappedSubresource box =
                        d3dContext.Map(stagingTex, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        unsafe // Use unsafe context for direct memory copy
                        {
                            byte* srcBase = (byte*)bData.Scan0;
                            byte* dstBase = (byte*)box.DataPointer;
                            for (int y = 0; y < FRAME_TEX_H; y++)
                                Buffer.MemoryCopy(srcBase + (long)y * srcStride, dstBase + (long)y * box.RowPitch,
                                    rowBytes, rowBytes);
                        }
                    }
                    finally { d3dContext.Unmap(stagingTex, 0); }
                    d3dContext.CopyResource(overlayTex, stagingTex);
                }
            }
            finally { if (bData != null) frameBitmap.UnlockBits(bData); }

            float offsetX_meters = ((frameX + drawW / 2.0f) - totalPixelW / 2.0f) / texScale;
            float offsetY_meters = ((totalPixelH / 2.0f) - (frameY + drawH / 2.0f)) / texScale;
            overlayCenter = center - hmdRight * offsetX_meters - hmdUp * offsetY_meters;

            VRTextureBounds_t bounds = new()
            { uMin = 0f, vMin = 0f, uMax = (float)totalPixelW / FRAME_TEX_W, vMax = (float)totalPixelH / FRAME_TEX_H };
            OpenVR.Overlay.SetOverlayTextureBounds(overlayHandle, ref bounds);

            Texture_t vrTex = new() { handle = overlayTex!.NativePointer, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Auto };
            OpenVR.Overlay.SetOverlayTexture(overlayHandle, ref vrTex);
            lock (d3dLock) { d3dContext?.Flush(); }
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

        public void CaptureAndSave(bool leftTriggerHeld)
        {
            if (!EnsureMirrorPipeline())
                return;

            PointF[]? corners = ProjectFrameCorners(mirrorW, mirrorH);
            if (corners == null || corners.Length < 4) return;

            // Guard against invalid source dimensions
            if (lastFrameWidth <= 0 || lastFrameHeight <= 0) return;

            Bitmap? mirrorBmp = null;
            lock (d3dLock)
            {
                Format mirrorFormat = mirrorTexCached!.Description.Format;
                bool needsSwap = mirrorFormat == Format.R8G8B8A8_UNorm ||
                                 mirrorFormat == Format.R8G8B8A8_UNorm_SRgb ||
                                 mirrorFormat == Format.R8G8B8A8_Typeless;

                d3dContext!.CopyResource(mirrorStaging!, mirrorTexCached!);
                d3dContext.Flush();
                MappedSubresource box = d3dContext.Map(mirrorStaging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);

                try
                {
                    mirrorBmp = new Bitmap(mirrorW, mirrorH, PixelFormat.Format32bppArgb);
                    BitmapData bData = mirrorBmp.LockBits(new Rectangle(0, 0, mirrorW, mirrorH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                    unsafe
                    {
                        for (int y = 0; y < mirrorH; y++)
                        {
                            byte* srcPtr = (byte*)box.DataPointer + (y * box.RowPitch);
                            byte* dstPtr = (byte*)bData.Scan0 + (y * bData.Stride);

                            for (int x = 0; x < mirrorW; x++)
                            {
                                if (needsSwap)
                                {
                                    dstPtr[0] = srcPtr[2]; // B
                                    dstPtr[1] = srcPtr[1]; // G
                                    dstPtr[2] = srcPtr[0]; // R
                                }
                                else
                                {
                                    *(uint*)dstPtr = *(uint*)srcPtr;
                                }
                                dstPtr[3] = 255; // Alpha
                                srcPtr += 4;
                                dstPtr += 4;
                            }
                        }
                    }

                    mirrorBmp.UnlockBits(bData);
                }
                finally
                {
                    d3dContext.Unmap(mirrorStaging!, 0);
                }
            }

            // Validate width and height calculations
            float dist = Vector2.Distance(new Vector2(corners[0].X, corners[0].Y), new Vector2(corners[1].X, corners[1].Y));
            int outW = (int)MathF.Min(MathF.Max(dist, 8f), 16384f);
            int outH = (int)MathF.Min(MathF.Max(MathF.Round(outW * (lastFrameHeight / lastFrameWidth)), 8f), 16384f);

            // If the projected frame is too small (near edge-on view), the inverted
            // transform would require GDI+ to scale mirrorBmp by an extreme factor,
            // causing ExternalException (0x8000FFFF). Require a meaningful output size.
            if (dist < 8f)
            {
                mirrorBmp.Dispose();
                return;
            }

            using (Bitmap outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(outBmp))
                {
                    using Matrix mtx = new Matrix(new RectangleF(0, 0, outW, outH), new[] { corners[0], corners[1], corners[3] });
                    // Ensure matrix is invertible before applying to Graphics context
                    if (!mtx.IsInvertible)
                    {
                        mirrorBmp.Dispose();
                        return;
                    }

                    mtx.Invert();
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.Transform = mtx;
                    try
                    {
                        g.DrawImage(mirrorBmp, 0, 0);
                    }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        // GDI+ can still fail with extreme transform scales even when the
                        // matrix is technically invertible. Discard this frame silently.
                        mirrorBmp.Dispose();
                        return;
                    }
                }

                OnPhotoSaved?.Invoke((Bitmap)outBmp.Clone(), leftTriggerHeld);
            }

            mirrorBmp.Dispose();
        }

        private bool EnsureMirrorPipeline()
        {
            EVREye targetEye = App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left;
            if (mirrorStaging != null && currentMirrorEye == targetEye)
                return true;

            lock (d3dLock)
            {
                mirrorStaging?.Dispose();
                mirrorTexCached?.Dispose();
                mirrorSrvObj?.Dispose();
                if (mirrorSrv != IntPtr.Zero)
                    OpenVR.Compositor?.ReleaseMirrorTextureD3D11(mirrorSrv);

                IntPtr srv = IntPtr.Zero;
                if (OpenVR.Compositor.GetMirrorTextureD3D11(targetEye, d3dDevice!.NativePointer, ref srv) != EVRCompositorError.None)
                    return false;

                mirrorSrv = srv;
                mirrorSrvObj = new ID3D11ShaderResourceView(srv);
                mirrorTexCached = mirrorSrvObj.Resource.QueryInterface<ID3D11Texture2D>();
                currentMirrorEye = targetEye;
            }

            Texture2DDescription desc = mirrorTexCached.Description;
            mirrorW = (int)desc.Width; mirrorH = (int)desc.Height;

            mirrorStaging = d3dDevice.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)mirrorW,
                Height = (uint)mirrorH,
                MipLevels = 1,
                ArraySize = 1,
                Format = desc.Format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read
            });
            return true;
        }

        private PointF[]? ProjectFrameCorners(int mw, int mh)
        {
            CVRSystem system = OpenVR.System;
            if (system == null) return null;

            uint hmdIdx = OpenVR.k_unTrackedDeviceIndex_Hmd;
            TrackedDevicePose_t[]? poses = inputService.Poses;

            Matrix4x4 vp = VRInputService.ToMatrix4x4(system.GetEyeToHeadTransform(App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left)) * VRInputService.ToMatrix4x4(poses[hmdIdx].mDeviceToAbsoluteTracking);
            Matrix4x4.Invert(vp, out Matrix4x4 view);
            vp = view * VRInputService.ToMatrix4x4Proj(system.GetProjectionMatrix(App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left, 0.05f, 50f));

            Vector3 center = (lastLeftPos + lastRightPos) * 0.5f;
            float hw = lastFrameWidth * 0.5f, hh = lastFrameHeight * 0.5f;

            Vector3[] worldCorners = [center - hmdRight * hw + hmdUp * hh, center + hmdRight * hw + hmdUp * hh, center + hmdRight * hw - hmdUp * hh, center - hmdRight * hw - hmdUp * hh];
            PointF[]? pts = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                Vector4 clip = Vector4.Transform(new Vector4(worldCorners[i], 1f), vp);
                if (clip.W <= 0)
                    return null;
                pts[i] = new PointF((clip.X / clip.W * 0.5f + 0.5f) * mw, (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * mh);
            }
            return pts;
        }

        private void EmitState() => OnStateUpdate?.Invoke(new { connected = IsConnected, framing = IsFraming });

        public void Dispose()
        {
            Disconnect();
            frameBitmap?.Dispose();
        }
    }
}