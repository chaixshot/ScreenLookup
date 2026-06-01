using System.Drawing;
using System.Drawing.Imaging;
using System.Media;
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

        // Config
        public uint LeftButtonId { get; set; } = (uint)EVRButtonId.k_EButton_Grip;
        public uint RightButtonId { get; set; } = (uint)EVRButtonId.k_EButton_Grip;

        // State
        public bool IsConnected { get; private set; }
        public bool IsFraming { get; private set; }
        public string? LastError { get; private set; }

        // Events
        public event Action<object>? OnStateUpdate;
        public event Action? OnVRQuit;
        public event Action<Bitmap?> OnPhotoSaved;

        // OpenVR
        private CVRSystem? _vrSystem;
        private ulong _overlayHandle;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _running;
        private bool _disposed;
        private readonly Action<string> _log;

        // Controller tracking
        private uint _leftIdx = OpenVR.k_unTrackedDeviceIndexInvalid;
        private uint _rightIdx = OpenVR.k_unTrackedDeviceIndexInvalid;
        private readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        // Button state
        private bool _leftHeld;
        private bool _rightHeld;
        private bool _leftHeldPrev;
        private bool _rightHeldPrev;

        private Vector3 _lastLeftPos;
        private Vector3 _lastRightPos;
        private float _lastFrameWidth;
        private float _lastFrameHeight;

        // D3D11
        private ID3D11Device? _d3dDevice;
        private ID3D11DeviceContext? _d3dContext;
        public ID3D11Device? Device => _d3dDevice;
        public ID3D11DeviceContext? Context => _d3dContext;

        public ID3D11Device? GetDevice() => _d3dDevice;

        private readonly object _d3dLock = new();
        private ID3D11Texture2D? _overlayTex;
        private ID3D11Texture2D? _stagingTex;
        private ID3D11Texture2D? _mirrorStaging;
        private ID3D11ShaderResourceView? _mirrorSrvObj;
        private const int FRAME_TEX_W = 1024;
        private const int FRAME_TEX_H = 1024;
        private byte[] _rowBuffer = new byte[FRAME_TEX_W * 4];
        private Bitmap? _frameBitmap;

        private IntPtr _mirrorSrv = IntPtr.Zero;
        private ID3D11Texture2D? _mirrorTexCached;
        private int _mirrorW;
        private int _mirrorH;

        public FrameShotService(Action<string> log)
        {
            _log = log;
            Instance = this;
        }

        public bool Connect()
        {
            if (IsConnected) return true;
            try
            {
                var err = EVRInitError.None;
                _vrSystem = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Overlay);
                if (err != EVRInitError.None)
                {
                    LastError = $"OpenVR init failed: {err}";
                    return false;
                }

                OpenVR.Overlay.CreateOverlay("screenlookup.frameshot", "ScreenLookup FrameShot", ref _overlayHandle);
                OpenVR.Overlay.SetOverlayAlpha(_overlayHandle, 1.0f);

                D3D11.D3D11CreateDevice(null, DriverType.Hardware, DeviceCreationFlags.None,
                    [FeatureLevel.Level_11_0], out _d3dDevice, out _d3dContext);

                _overlayTex = _d3dDevice!.CreateTexture2D(new Texture2DDescription
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
                _stagingTex = _d3dDevice.CreateTexture2D(new Texture2DDescription
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
                _frameBitmap = new Bitmap(FRAME_TEX_W, FRAME_TEX_H, PixelFormat.Format32bppArgb);

                IsConnected = true;
                EmitState();
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        public void Disconnect()
        {
            StopPolling();
            if (!IsConnected) return;
            lock (_d3dLock)
            {
                _mirrorStaging?.Dispose(); _mirrorTexCached?.Dispose(); _mirrorSrvObj?.Dispose(); _mirrorStaging = null;
                if (_mirrorSrv != IntPtr.Zero) OpenVR.Compositor?.ReleaseMirrorTextureD3D11(_mirrorSrv);
                _stagingTex?.Dispose(); _overlayTex?.Dispose(); _mirrorSrv = IntPtr.Zero;
                _d3dContext?.Dispose(); _d3dDevice?.Dispose();
            }
            OpenVR.Shutdown();
            IsConnected = false;
        }

        public void StartPolling()
        {
            if (_running) return;
            _cts = new CancellationTokenSource();
            _running = true;
            _pollTask = PollLoopAsync(_cts.Token);
        }

        public void StopPolling() { _running = false; _cts?.Cancel(); }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ProcessFrame();
                await Task.Delay(11, ct);
            }
        }

        private void ProcessFrame()
        {
            if (_vrSystem == null) return;
            _vrSystem.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0, _poses);

            _leftIdx = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            _rightIdx = _vrSystem.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

            _leftHeldPrev = _leftHeld;
            _rightHeldPrev = _rightHeld;
            _leftHeld = IsButtonHeld(_leftIdx, LeftButtonId);
            _rightHeld = IsButtonHeld(_rightIdx, RightButtonId);

            Vector3 L = Vector3.Zero, R = Vector3.Zero;
            bool wasFraming = IsFraming;
            bool isButtonCombo = _leftHeld && _rightHeld;

            if (isButtonCombo)
                IsFraming = wasFraming || AreHandsWithinActivationRadius(out L, out R);
            else
                IsFraming = false;

            if (IsFraming)
                UpdateFrameAndRender(L, R);
            else if (wasFraming)
            {
                OpenVR.Overlay.HideOverlay(_overlayHandle);
                if (_rightHeldPrev && !_rightHeld && _leftHeld)
                {
                    SystemSounds.Asterisk.Play();
                    Task.Run(CaptureAndSave);
                }
                else
                {
                    SystemSounds.Hand.Play();
                }
            }
        }

        private bool IsButtonHeld(uint idx, uint buttonId)
        {
            if (idx == OpenVR.k_unTrackedDeviceIndexInvalid) return false;
            var s = new VRControllerState_t();
            return _vrSystem!.GetControllerState(idx, ref s, (uint)Marshal.SizeOf<VRControllerState_t>()) && (s.ulButtonPressed & (1UL << (int)buttonId)) != 0;
        }

        private bool AreHandsWithinActivationRadius(out Vector3 L, out Vector3 R)
        {
            L = R = Vector3.Zero;
            if (_leftIdx == OpenVR.k_unTrackedDeviceIndexInvalid || _rightIdx == OpenVR.k_unTrackedDeviceIndexInvalid) return false;
            if (!_poses[_leftIdx].bPoseIsValid || !_poses[_rightIdx].bPoseIsValid) return false;

            L = PosFromMatrix(_poses[_leftIdx].mDeviceToAbsoluteTracking);
            R = PosFromMatrix(_poses[_rightIdx].mDeviceToAbsoluteTracking);
            return (R - L).Length() <= App.setting.ActivationRadius / 100f;
        }

        private void UpdateFrameAndRender(Vector3 L, Vector3 R)
        {
            uint hmdIdx = (uint)OpenVR.k_unTrackedDeviceIndex_Hmd;
            if (!_poses[_leftIdx].bPoseIsValid || !_poses[_rightIdx].bPoseIsValid || !_poses[hmdIdx].bPoseIsValid) return;

            if (L == Vector3.Zero && R == Vector3.Zero)
            {
                L = PosFromMatrix(_poses[_leftIdx].mDeviceToAbsoluteTracking);
                R = PosFromMatrix(_poses[_rightIdx].mDeviceToAbsoluteTracking);
            }

            var hmdM = _poses[hmdIdx].mDeviceToAbsoluteTracking;
            var hmdRot = RotFromMatrix(hmdM);

            Vector3 hmdFwd = Vector3.Transform(-Vector3.UnitZ, hmdRot);
            Vector3 hmdRight = Vector3.Normalize(Vector3.Cross(hmdFwd, Vector3.UnitY));
            Vector3 hmdUp = Vector3.Normalize(Vector3.Cross(hmdRight, hmdFwd));
            Vector3 center = (L + R) * 0.5f;

            float widthM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R - L, hmdRight)));
            float heightM = MathF.Max(0.02f, MathF.Abs(Vector3.Dot(R - L, hmdUp)));

            _lastLeftPos = L; _lastRightPos = R;
            _lastFrameWidth = widthM; _lastFrameHeight = heightM;

            int drawW = FRAME_TEX_W;
            int drawH = (int)MathF.Round(FRAME_TEX_W * (heightM / widthM));
            if (drawH > FRAME_TEX_H) { drawH = FRAME_TEX_H; drawW = (int)MathF.Round(FRAME_TEX_H / (heightM / widthM)); }

            DrawFrameTexture(drawW, drawH);
            OpenVR.Overlay.SetOverlayWidthInMeters(_overlayHandle, widthM);
            var transform = new HmdMatrix34_t { m0 = hmdRight.X, m1 = hmdUp.X, m2 = -hmdFwd.X, m3 = center.X, m4 = hmdRight.Y, m5 = hmdUp.Y, m6 = -hmdFwd.Y, m7 = center.Y, m8 = hmdRight.Z, m9 = hmdUp.Z, m10 = -hmdFwd.Z, m11 = center.Z };
            OpenVR.Overlay.SetOverlayTransformAbsolute(_overlayHandle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref transform);
            OpenVR.Overlay.ShowOverlay(_overlayHandle);
        }

        private void DrawFrameTexture(int drawW, int drawH)
        {
            using (var g = Graphics.FromImage(_frameBitmap!))
            {
                g.Clear(System.Drawing.Color.Transparent);
                using var pen = new Pen(System.Drawing.Color.FromArgb(255, 130, 210, 255), 8f);
                g.DrawRectangle(pen, 4, 4, drawW - 9, drawH - 9);
            }

            var rect = new System.Drawing.Rectangle(0, 0, FRAME_TEX_W, FRAME_TEX_H);
            var bData = _frameBitmap!.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            lock (_d3dLock)
            {
                var box = _d3dContext!.Map(_stagingTex!, 0, MapMode.Write, Vortice.Direct3D11.MapFlags.None);
                for (int y = 0; y < FRAME_TEX_H; y++)
                {
                    Marshal.Copy(bData.Scan0 + y * bData.Stride, _rowBuffer, 0, FRAME_TEX_W * 4);
                    Marshal.Copy(_rowBuffer, 0, box.DataPointer + (nint)((long)y * box.RowPitch), FRAME_TEX_W * 4);
                }
                _d3dContext.Unmap(_stagingTex!, 0);
                _d3dContext.CopyResource(_overlayTex!, _stagingTex!);
            }
            _frameBitmap.UnlockBits(bData);
            var bounds = new VRTextureBounds_t { uMin = 0f, vMin = 0f, uMax = (float)drawW / FRAME_TEX_W, vMax = (float)drawH / FRAME_TEX_H };
            OpenVR.Overlay.SetOverlayTextureBounds(_overlayHandle, ref bounds);
            var vrTex = new Texture_t { handle = _overlayTex!.NativePointer, eType = ETextureType.DirectX, eColorSpace = EColorSpace.Auto };
            OpenVR.Overlay.SetOverlayTexture(_overlayHandle, ref vrTex);
        }

        public void CaptureAndSave()
        {
            if (!EnsureMirrorPipeline()) return;

            var corners = ProjectFrameCorners(_mirrorW, _mirrorH);
            if (corners == null) return;

            Bitmap? mirrorBmp = null;
            lock (_d3dLock)
            {
                if (_rowBuffer.Length < _mirrorW * 4) _rowBuffer = new byte[_mirrorW * 4];

                _d3dContext!.CopyResource(_mirrorStaging!, _mirrorTexCached!);
                var box = _d3dContext.Map(_mirrorStaging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                mirrorBmp = new Bitmap(_mirrorW, _mirrorH, PixelFormat.Format32bppArgb);
                var bData = mirrorBmp.LockBits(new System.Drawing.Rectangle(0, 0, _mirrorW, _mirrorH), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                for (int y = 0; y < _mirrorH; y++)
                {
                    Marshal.Copy(box.DataPointer + (nint)((long)y * box.RowPitch), _rowBuffer, 0, _mirrorW * 4);
                    Marshal.Copy(_rowBuffer, 0, bData.Scan0 + y * bData.Stride, _mirrorW * 4);
                }
                mirrorBmp.UnlockBits(bData);
                _d3dContext.Unmap(_mirrorStaging!, 0);
            }

            int outW = (int)MathF.Max(2, Vector2.Distance(new Vector2(corners[0].X, corners[0].Y), new Vector2(corners[1].X, corners[1].Y)));
            int outH = (int)MathF.Round(outW * (_lastFrameHeight / _lastFrameWidth));
            using (var outBmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(outBmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    var mtx = new System.Drawing.Drawing2D.Matrix(new System.Drawing.RectangleF(0, 0, outW, outH), [corners[0], corners[1], corners[3]]);
                    mtx.Invert(); g.Transform = mtx;
                    g.DrawImage(mirrorBmp, 0, 0);
                }
                OnPhotoSaved?.Invoke((Bitmap)outBmp.Clone());
            }
            mirrorBmp.Dispose();
        }

        private bool EnsureMirrorPipeline()
        {
            if (_mirrorStaging != null) return true;
            var srv = IntPtr.Zero;
            if (OpenVR.Compositor.GetMirrorTextureD3D11(EVREye.Eye_Left, _d3dDevice!.NativePointer, ref srv) != EVRCompositorError.None) return false;
            _mirrorSrv = srv;
            _mirrorSrvObj = new ID3D11ShaderResourceView(srv);
            _mirrorTexCached = _mirrorSrvObj.Resource.QueryInterface<ID3D11Texture2D>();
            var desc = _mirrorTexCached.Description;
            _mirrorW = (int)desc.Width; _mirrorH = (int)desc.Height;
            _mirrorStaging = _d3dDevice.CreateTexture2D(new Texture2DDescription { Width = (uint)_mirrorW, Height = (uint)_mirrorH, MipLevels = 1, ArraySize = 1, Format = desc.Format, SampleDescription = new SampleDescription(1, 0), Usage = ResourceUsage.Staging, CPUAccessFlags = CpuAccessFlags.Read });
            return true;
        }

        private PointF[]? ProjectFrameCorners(int mw, int mh)
        {
            uint hmdIdx = (uint)OpenVR.k_unTrackedDeviceIndex_Hmd;
            var hmdM = _poses[hmdIdx].mDeviceToAbsoluteTracking;
            var hmdRot = RotFromMatrix(hmdM);
            var hmdPos = PosFromMatrix(hmdM);
            var vp = ToMatrix4x4(_vrSystem!.GetEyeToHeadTransform(EVREye.Eye_Left)) * ToMatrix4x4(hmdM);
            Matrix4x4.Invert(vp, out var view);
            vp = view * ToMatrix4x4Proj(_vrSystem.GetProjectionMatrix(EVREye.Eye_Left, 0.05f, 50f));

            Vector3 hmdFwd = Vector3.Transform(-Vector3.UnitZ, hmdRot);
            Vector3 hmdRight = Vector3.Normalize(Vector3.Cross(hmdFwd, Vector3.UnitY));
            Vector3 hmdUp = Vector3.Normalize(Vector3.Cross(hmdRight, hmdFwd));
            Vector3 center = (_lastLeftPos + _lastRightPos) * 0.5f;
            float hw = _lastFrameWidth * 0.5f, hh = _lastFrameHeight * 0.5f;

            Vector3[] worldCorners = { center - hmdRight * hw + hmdUp * hh, center + hmdRight * hw + hmdUp * hh, center + hmdRight * hw - hmdUp * hh, center - hmdRight * hw - hmdUp * hh };
            var pts = new PointF[4];
            for (int i = 0; i < 4; i++)
            {
                var clip = Vector4.Transform(new Vector4(worldCorners[i], 1f), vp);
                if (clip.W <= 0) return null;
                pts[i] = new PointF((clip.X / clip.W * 0.5f + 0.5f) * mw, (1f - (clip.Y / clip.W * 0.5f + 0.5f)) * mh);
            }
            return pts;
        }

        private static Vector3 PosFromMatrix(in HmdMatrix34_t m) => new(m.m3, m.m7, m.m11);
        private static Quaternion RotFromMatrix(in HmdMatrix34_t m)
        {
            float tr = m.m0 + m.m5 + m.m10;
            if (tr > 0f) { float s = MathF.Sqrt(tr + 1f) * 2f; return Quaternion.Normalize(new Quaternion((m.m9 - m.m6) / s, (m.m2 - m.m8) / s, (m.m4 - m.m1) / s, 0.25f * s)); }
            return Quaternion.Identity;
        }
        private static Matrix4x4 ToMatrix4x4(in HmdMatrix34_t m) => new(m.m0, m.m4, m.m8, 0, m.m1, m.m5, m.m9, 0, m.m2, m.m6, m.m10, 0, m.m3, m.m7, m.m11, 1);
        private static Matrix4x4 ToMatrix4x4Proj(in HmdMatrix44_t m) => new(m.m0, m.m4, m.m8, m.m12, m.m1, m.m5, m.m9, m.m13, m.m2, m.m6, m.m10, m.m14, m.m3, m.m7, m.m11, m.m15);
        private void EmitState() => OnStateUpdate?.Invoke(new { connected = IsConnected, framing = IsFraming });
        public void Dispose() { Disconnect(); _frameBitmap?.Dispose(); }
    }
}