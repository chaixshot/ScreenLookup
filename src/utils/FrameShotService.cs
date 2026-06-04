using System.Drawing;
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

        // Events
        public event Action<object>? OnStateUpdate;
        public event Action? OnVRQuit; // Retained if needed elsewhere in the app wrapper
        public event Action<Bitmap?, bool>? OnPhotoSaved;

        // Dependencies & Input pipeline
        private readonly VRInputService inputService;
        private CVRSystem? vrSystem;
        private ulong overlayHandle;
        private CancellationTokenSource? cts;
        private Task? pollTask;
        private bool running;
        private readonly Action<string> log;

        // Internal Input Processing Mirrors
        private bool leftHeld;
        private bool rightHeld;
        private bool leftTriggerHeld;
        private bool rightTriggerHeld;
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
                var err = EVRInitError.None;
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

            StopPolling();

            if (pollTask != null && !pollTask.IsCompleted)
                pollTask.Wait(TimeSpan.FromMilliseconds(500));

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
            IsConnected = false;
        }

        public void StartPolling()
        {
            if (running)
                return;

            cts = new CancellationTokenSource();
            running = true;
            pollTask = PollLoopAsync(cts.Token);
        }

        public void StopPolling()
        {
            running = false;
            cts?.Cancel();
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            float refreshRate = VRInputService.GetHmdRefreshRate();
            int delay = (int)(1000 / refreshRate);

            while (!ct.IsCancellationRequested)
            {
                ProcessFrame();
                await Task.Delay(delay, ct);
            }
        }

        private void ProcessFrame()
        {
            var system = OpenVR.System;
            if (system == null || !IsConnected)
                return;

            // Gather inputs via sub-service
            inputService.UpdatePosesAndIndices();

            leftHeldPrev = leftHeld;
            rightHeldPrev = rightHeld;

            leftHeld = inputService.IsButtonHeld(inputService.LeftControllerIdx, inputService.GripButtonId);
            rightHeld = inputService.IsButtonHeld(inputService.RightControllerIdx, inputService.GripButtonId);
            leftTriggerHeld = inputService.IsButtonHeld(inputService.LeftControllerIdx, inputService.TriggerButtonId);
            rightTriggerHeld = inputService.IsButtonHeld(inputService.RightControllerIdx, inputService.TriggerButtonId);

            Vector3 L = Vector3.Zero, R = Vector3.Zero;
            bool wasFraming = IsFraming;
            bool isButtonCombo = leftHeld && rightHeld;

            if (isButtonCombo)
            {
                bool isInRange = inputService.TryGetHandPositions(App.setting.ActivationRadius, out L, out R);

                if (!wasFraming && isInRange)
                {
                    AppUtilities.PlaySound("ready.wav");
                    App.captureWindow.HideWindow();
                }

                IsFraming = wasFraming || isInRange;
            }
            else
                IsFraming = false;

            if (IsFraming)
                UpdateFrameAndRender(L, R);
            else if (wasFraming)
            {
                OpenVR.Overlay.HideOverlay(overlayHandle);

                if (rightHeldPrev && !rightHeld && leftHeld)
                {
                    AppUtilities.PlaySound("screenshot.wav");

                    App.captureWindow.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        await Task.Delay(100);
                        CaptureAndSave(leftTriggerHeld || rightTriggerHeld);
                    }));
                }
            }
        }

        private void UpdateFrameAndRender(Vector3 L, Vector3 R)
        {
            uint hmdIdx = OpenVR.k_unTrackedDeviceIndex_Hmd;
            var poses = inputService.Poses;

            if (!poses[inputService.LeftControllerIdx].bPoseIsValid ||
                !poses[inputService.RightControllerIdx].bPoseIsValid ||
                !poses[hmdIdx].bPoseIsValid)
                return;

            if (L == Vector3.Zero && R == Vector3.Zero)
            {
                L = VRInputService.PosFromMatrix(poses[inputService.LeftControllerIdx].mDeviceToAbsoluteTracking);
                R = VRInputService.PosFromMatrix(poses[inputService.RightControllerIdx].mDeviceToAbsoluteTracking);
            }

            var hmdM = poses[hmdIdx].mDeviceToAbsoluteTracking;
            var hmdRot = VRInputService.RotFromMatrix(hmdM);
            lastHmdRot = hmdRot;

            Vector3 hmdFwd = Vector3.Transform(-Vector3.UnitZ, hmdRot);
            Vector3 hmdRight = Vector3.Normalize(Vector3.Cross(hmdFwd, Vector3.UnitY));
            Vector3 hmdUp = Vector3.Normalize(Vector3.Cross(hmdRight, hmdFwd));
            Vector3 center = (L + R) * 0.5f;

            float widthM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R - L, hmdRight)));
            float heightM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R - L, hmdUp)));

            lastLeftPos = L;
            lastRightPos = R;
            lastFrameWidth = widthM;
            lastFrameHeight = heightM;

            int drawW = FRAME_TEX_W;
            int drawH = (int)MathF.Round(FRAME_TEX_W * (heightM / widthM));

            if (drawH > FRAME_TEX_H)
            {
                drawH = FRAME_TEX_H;
                drawW = (int)MathF.Round(FRAME_TEX_H / (heightM / widthM));
            }

            DrawFrameTexture(drawW, drawH);
            OpenVR.Overlay.SetOverlayWidthInMeters(overlayHandle, widthM);

            var transform = new HmdMatrix34_t { m0 = hmdRight.X, m1 = hmdUp.X, m2 = -hmdFwd.X, m3 = center.X, m4 = hmdRight.Y, m5 = hmdUp.Y, m6 = -hmdFwd.Y, m7 = center.Y, m8 = hmdRight.Z, m9 = hmdUp.Z, m10 = -hmdFwd.Z, m11 = center.Z };
            OpenVR.Overlay.SetOverlayTransformAbsolute(overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform);
            OpenVR.Overlay.ShowOverlay(overlayHandle);
        }

        private void DrawFrameTexture(int drawW, int drawH)
        {
            using (var g = Graphics.FromImage(frameBitmap!))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var pen = new Pen(System.Drawing.Color.FromArgb(255, 130, 210, 255), 8f);
                g.DrawRectangle(pen, 4, 4, drawW - 9, drawH - 9);
            }

            var rect = new Rectangle(0, 0, FRAME_TEX_W, FRAME_TEX_H);
            var bData = frameBitmap!.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            lock (d3dLock)
            {
                var box = d3dContext!.Map(stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                for (int y = 0; y < FRAME_TEX_H; y++)
                {
                    Marshal.Copy(bData.Scan0 + y * bData.Stride, rowBuffer, 0, FRAME_TEX_W * 4);
                    Marshal.Copy(rowBuffer, 0, box.DataPointer + (nint)((long)y * box.RowPitch), FRAME_TEX_W * 4);
                }
                d3dContext.Unmap(stagingTex!, 0);
                d3dContext.CopyResource(overlayTex!, stagingTex!);
            }
            frameBitmap.UnlockBits(bData);

            var bounds = new VRTextureBounds_t { uMin = 0f, vMin = 0f, uMax = (float)drawW / FRAME_TEX_W, vMax = (float)drawH / FRAME_TEX_H };
            OpenVR.Overlay.SetOverlayTextureBounds(overlayHandle, ref bounds);

            var vrTex = new Texture_t { handle = overlayTex!.NativePointer, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Auto };
            OpenVR.Overlay.SetOverlayTexture(overlayHandle, ref vrTex);
        }

        public void CaptureAndSave(bool isTriggerHeld)
        {
            if (!EnsureMirrorPipeline())
                return;

            var corners = ProjectFrameCorners(mirrorW, mirrorH);
            if (corners == null) return;

            Bitmap? mirrorBmp = null;
            lock (d3dLock)
            {
                byte[] localRowBuffer = new byte[mirrorW * 4];
                Format mirrorFormat = mirrorTexCached!.Description.Format;
                bool needsSwap = mirrorFormat == Format.R8G8B8A8_UNorm ||
                                 mirrorFormat == Format.R8G8B8A8_UNorm_SRgb ||
                                 mirrorFormat == Format.R8G8B8A8_Typeless;

                d3dContext!.CopyResource(mirrorStaging!, mirrorTexCached!);
                var box = d3dContext.Map(mirrorStaging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                mirrorBmp = new Bitmap(mirrorW, mirrorH, PixelFormat.Format32bppArgb);
                var bData = mirrorBmp.LockBits(new Rectangle(0, 0, mirrorW, mirrorH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                for (int y = 0; y < mirrorH; y++)
                {
                    Marshal.Copy(box.DataPointer + (nint)((long)y * box.RowPitch), localRowBuffer, 0, mirrorW * 4);

                    for (int x = 0; x < mirrorW; x++)
                    {
                        int i = x * 4;
                        if (needsSwap)
                        {
                            byte r = localRowBuffer[i];
                            localRowBuffer[i] = localRowBuffer[i + 2];
                            localRowBuffer[i + 2] = r;
                        }
                        localRowBuffer[i + 3] = 255;
                    }

                    Marshal.Copy(localRowBuffer, 0, bData.Scan0 + y * bData.Stride, mirrorW * 4);
                }

                mirrorBmp.UnlockBits(bData);
                d3dContext.Unmap(mirrorStaging!, 0);
            }

            int outW = (int)MathF.Max(2, Vector2.Distance(new Vector2(corners[0].X, corners[0].Y), new Vector2(corners[1].X, corners[1].Y)));
            int outH = (int)MathF.Round(outW * (lastFrameHeight / lastFrameWidth));

            using (var outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(outBmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    var mtx = new System.Drawing.Drawing2D.Matrix(new RectangleF(0, 0, outW, outH), [corners[0], corners[1], corners[3]]);
                    mtx.Invert(); g.Transform = mtx;
                    g.DrawImage(mirrorBmp, 0, 0);
                }

                OnPhotoSaved?.Invoke((Bitmap)outBmp.Clone(), isTriggerHeld);
            }
            mirrorBmp.Dispose();
        }

        private bool EnsureMirrorPipeline()
        {
            var targetEye = App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left;
            if (mirrorStaging != null && currentMirrorEye == targetEye)
                return true;

            lock (d3dLock)
            {
                mirrorStaging?.Dispose();
                mirrorTexCached?.Dispose();
                mirrorSrvObj?.Dispose();
                if (mirrorSrv != IntPtr.Zero)
                    OpenVR.Compositor?.ReleaseMirrorTextureD3D11(mirrorSrv);

                var srv = IntPtr.Zero;
                if (OpenVR.Compositor.GetMirrorTextureD3D11(targetEye, d3dDevice!.NativePointer, ref srv) != EVRCompositorError.None)
                    return false;

                mirrorSrv = srv;
                mirrorSrvObj = new ID3D11ShaderResourceView(srv);
                mirrorTexCached = mirrorSrvObj.Resource.QueryInterface<ID3D11Texture2D>();
                currentMirrorEye = targetEye;
            }

            var desc = mirrorTexCached.Description;
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
            var system = OpenVR.System;
            if (system == null) return null;

            uint hmdIdx = OpenVR.k_unTrackedDeviceIndex_Hmd;
            var poses = inputService.Poses;

            var vp = VRInputService.ToMatrix4x4(system.GetEyeToHeadTransform(App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left)) * VRInputService.ToMatrix4x4(poses[hmdIdx].mDeviceToAbsoluteTracking);
            Matrix4x4.Invert(vp, out var view);
            vp = view * VRInputService.ToMatrix4x4Proj(system.GetProjectionMatrix(App.setting.UseRightEye ? EVREye.Eye_Right : EVREye.Eye_Left, 0.05f, 50f));

            Vector3 hmdFwd = Vector3.Transform(-Vector3.UnitZ, lastHmdRot);
            Vector3 hmdRight = Vector3.Normalize(Vector3.Cross(hmdFwd, Vector3.UnitY));
            Vector3 hmdUp = Vector3.Normalize(Vector3.Cross(hmdRight, hmdFwd));
            Vector3 center = (lastLeftPos + lastRightPos) * 0.5f;
            float hw = lastFrameWidth * 0.5f, hh = lastFrameHeight * 0.5f;

            Vector3[] worldCorners = { center - hmdRight * hw + hmdUp * hh, center + hmdRight * hw + hmdUp * hh, center + hmdRight * hw - hmdUp * hh, center - hmdRight * hw - hmdUp * hh };
            var pts = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                var clip = Vector4.Transform(new Vector4(worldCorners[i], 1f), vp);
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