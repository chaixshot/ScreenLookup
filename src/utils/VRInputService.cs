using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Valve.VR;

namespace ScreenLookup.src.utils
{
    /// <summary>
    /// Service responsible for VR controller tracking, IVRInput action management, and spatial math transforms.
    /// </summary>
    public class VRInputService
    {
        #region Constants & Properties
        private readonly TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        public uint LeftControllerIdx { get; private set; } = OpenVR.k_unTrackedDeviceIndexInvalid;
        public uint RightControllerIdx { get; private set; } = OpenVR.k_unTrackedDeviceIndexInvalid;

        // High priority value to block scene application inputs
        public const int HighPriorityBlock = OpenVR.k_nActionSetOverlayGlobalPriorityMin + 2;
        public bool BlockGameInput { get; set; } = false;

        public TrackedDevicePose_t[] Poses => poses;

        public uint GripButtonId { get; set; } = (uint)EVRButtonId.k_EButton_Grip;
        public uint TriggerButtonId { get; set; } = (uint)EVRButtonId.k_EButton_SteamVR_Trigger;
        public uint AButtonId { get; set; } = (uint)EVRButtonId.k_EButton_IndexController_A;
        public uint BButtonId { get; set; } = (uint)EVRButtonId.k_EButton_IndexController_B;
        #endregion

        #region Action Handles
        public ulong ActionSetHandle { get; private set; } = 0;
        public ulong GripLeftHandle { get; private set; } = 0;
        public ulong GripRightHandle { get; private set; } = 0;
        public ulong TriggerLeftHandle { get; private set; } = 0;
        public ulong TriggerRightHandle { get; private set; } = 0;
        public ulong CloseHandle { get; private set; } = 0;
        public ulong RecenterHandle { get; private set; } = 0;
        public ulong PointerLeftHandle { get; private set; } = 0;
        public ulong PointerRightHandle { get; private set; } = 0;
        public ulong ThumbstickLeftHandle { get; private set; } = 0;
        public ulong ThumbstickRightHandle { get; private set; } = 0;
        #endregion

        #region Haptic
        private class HapticTracker
        {
            public CancellationTokenSource? Cts;
        }

        private static readonly ConditionalWeakTable<object, HapticTracker> ActiveRumbles = new();
        private static readonly object LeftHandKey = new();
        private static readonly object RightHandKey = new();
        #endregion


        #region Action Registration & Polling
        /// <summary>
        /// Call once after OpenVR.Input.SetActionManifestPath() to cache all action handles.
        /// </summary>
        public void InitActionHandles()
        {
            string manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "vr", "actions.json");
            if (File.Exists(manifestPath))
                OpenVR.Input?.SetActionManifestPath(manifestPath);
            else
                SnackbarHost.Show("VRInput", $"actions.json not found at: {manifestPath}", type: SnackbarType.Error);

            var input = OpenVR.Input;
            if (input == null) return;

            ulong set = 0;
            input.GetActionSetHandle("/actions/screenlookup", ref set);
            ActionSetHandle = set;

            ulong h = 0;
            input.GetActionHandle("/actions/screenlookup/in/grip_left", ref h); GripLeftHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/grip_right", ref h); GripRightHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/trigger_left", ref h); TriggerLeftHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/trigger_right", ref h); TriggerRightHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/close", ref h); CloseHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/recenter", ref h); RecenterHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/pointer_left", ref h); PointerLeftHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/pointer_right", ref h); PointerRightHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/thumbstick_left", ref h); ThumbstickLeftHandle = h; h = 0;
            input.GetActionHandle("/actions/screenlookup/in/thumbstick_right", ref h); ThumbstickRightHandle = h;
        }

        /// <summary>
        /// Retrieves the pointer ray (origin and direction) for a controller using the SteamVR pointer pose action if
        /// available, or falling back to applying standard SteamVR pointer pitch (-38°) to raw controller tracking
        /// data.
        /// </summary>
        public bool GetPointerRay(ulong pointerActionHandle, in HmdMatrix34_t rawPose, out HmdVector3_t source, out HmdVector3_t direction, ulong restrictToDevice = OpenVR.k_ulInvalidInputValueHandle)
        {
            const float cos45 = 0.7071068f; // cos(45°)
            const float sin45 = 0.7071068f; // sin(45°)

            var input = OpenVR.Input;
            if (input != null && pointerActionHandle != 0)
            {
                InputPoseActionData_t poseData = new();

                // Try querying pose data relative to current frame
                EVRInputError err = input.GetPoseActionDataRelativeToNow(
                    pointerActionHandle,
                    ETrackingUniverseOrigin.TrackingUniverseStanding,
                    0.0f,
                    ref poseData,
                    (uint)Marshal.SizeOf<InputPoseActionData_t>(),
                    restrictToDevice);

                // Fallback to next frame prediction if current frame query fails
                if (err != EVRInputError.None || !poseData.bActive || !poseData.pose.bPoseIsValid)
                {
                    err = input.GetPoseActionDataForNextFrame(
                        pointerActionHandle,
                        ETrackingUniverseOrigin.TrackingUniverseStanding,
                        ref poseData,
                        (uint)Marshal.SizeOf<InputPoseActionData_t>(),
                        restrictToDevice);
                }

                if (err == EVRInputError.None && poseData.bActive && poseData.pose.bPoseIsValid)
                {
                    HmdMatrix34_t m = poseData.pose.mDeviceToAbsoluteTracking;
                    Vector3 dir = Vector3.Normalize(new Vector3(-m.m2, -m.m6, -m.m10));
                    Vector3 up = Vector3.Normalize(new Vector3(m.m1, m.m5, m.m9));
                    dir = Vector3.Normalize(cos45 * dir - sin45 * up); // Pitch down by 45°

                    Vector3 rawPos = new(m.m3, m.m7, m.m11);
                    Vector3 tipPos = rawPos + dir * 0.06f + up * 0.015f;

                    source = new HmdVector3_t { v0 = tipPos.X, v1 = tipPos.Y, v2 = tipPos.Z };
                    direction = new HmdVector3_t { v0 = dir.X, v1 = dir.Y, v2 = dir.Z };
                    return true;
                }
            }

            // Fallback: Apply 45° pointer pitch rotation relative to controller handle
            Vector3 rawFwd = Vector3.Normalize(new Vector3(-rawPose.m2, -rawPose.m6, -rawPose.m10));
            Vector3 rawUp = Vector3.Normalize(new Vector3(rawPose.m1, rawPose.m5, rawPose.m9));

            Vector3 pitchedDir = Vector3.Normalize(cos45 * rawFwd - sin45 * rawUp);
            Vector3 controllerPos = new(rawPose.m3, rawPose.m7, rawPose.m11);
            Vector3 laserTipPos = controllerPos + pitchedDir * 0.06f + rawUp * 0.015f;

            source = new HmdVector3_t { v0 = laserTipPos.X, v1 = laserTipPos.Y, v2 = laserTipPos.Z };
            direction = new HmdVector3_t { v0 = pitchedDir.X, v1 = pitchedDir.Y, v2 = pitchedDir.Z };
            return true;
        }

        private readonly Dictionary<ulong, (bool Current, bool Previous)> actionStates = [];

        /// <summary>
        /// Clears tracked action states to prevent pre-held buttons from triggering edge-detection events upon state activation.
        /// </summary>
        public void ResetActionStates()
        {
            actionStates.Clear();
        }

        /// <summary>
        /// Updates the IVRInput action set state for this frame.
        /// </summary>
        public void UpdateActionState()
        {
            if (ActionSetHandle == 0) return;
            var input = OpenVR.Input;
            if (input == null) return;

            var set = new VRActiveActionSet_t
            {
                ulActionSet = ActionSetHandle,
                nPriority = BlockGameInput ? HighPriorityBlock : 100
            };

            input.UpdateActionState([set], (uint)Marshal.SizeOf<VRActiveActionSet_t>());

            ulong[] handles = [GripLeftHandle, GripRightHandle, TriggerLeftHandle, TriggerRightHandle, CloseHandle, RecenterHandle];
            foreach (var h in handles)
            {
                if (h == 0) continue;
                var data = new InputDigitalActionData_t();
                input.GetDigitalActionData(h, ref data, (uint)Marshal.SizeOf<InputDigitalActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);

                bool current = data.bState;
                if (actionStates.TryGetValue(h, out var state))
                    actionStates[h] = (current, state.Current);
                else
                    actionStates[h] = (current, current);
            }
        }

        public bool IsActionHeld(ulong actionHandle)
        {
            if (actionHandle == 0) return false;
            return actionStates.TryGetValue(actionHandle, out var state) && state.Current;
        }

        public bool IsActionJustPressed(ulong actionHandle)
        {
            if (actionHandle == 0) return false;
            return actionStates.TryGetValue(actionHandle, out var state) && state.Current && !state.Previous;
        }

        public bool IsActionJustReleased(ulong actionHandle)
        {
            if (actionHandle == 0) return false;
            return actionStates.TryGetValue(actionHandle, out var state) && !state.Current && state.Previous;
        }

        public void UpdatePosesAndIndices()
        {
            var system = OpenVR.System;
            if (system == null) return;

            system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0.0f, poses);
            LeftControllerIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            RightControllerIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
        }

        public bool TryGetHandPositions(float activationRadiusCm, out Vector3 leftPos, out Vector3 rightPos)
        {
            leftPos = rightPos = Vector3.Zero;

            if (LeftControllerIdx == OpenVR.k_unTrackedDeviceIndexInvalid || RightControllerIdx == OpenVR.k_unTrackedDeviceIndexInvalid)
                return false;

            if (!poses[LeftControllerIdx].bPoseIsValid || !poses[RightControllerIdx].bPoseIsValid)
                return false;

            leftPos = PosFromMatrix(poses[LeftControllerIdx].mDeviceToAbsoluteTracking);
            rightPos = PosFromMatrix(poses[RightControllerIdx].mDeviceToAbsoluteTracking);

            return (rightPos - leftPos).Length() <= activationRadiusCm / 100f;
        }
        #endregion

        /// <summary>
        /// Sends a haptic rumble with explicit control over frequency and strength for the specified controller index.
        /// </summary>
        /// <param name="controllerIdx">The OpenVR device index of the controller.</param>
        /// <param name="duration">Total length of the vibration in seconds.</param>
        /// <param name="frequency">Frequency in Hz (Recommended: 40Hz to 320Hz).</param>
        /// <param name="strength">Strength from 0.0 to 1.0.</param>
        public static void Rumble(uint controllerIdx, float duration, float frequency, float strength)
        {
            if (controllerIdx == OpenVR.k_unTrackedDeviceIndexInvalid) return;

            var system = OpenVR.System;
            uint leftIdx = system?.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand) ?? OpenVR.k_unTrackedDeviceIndexInvalid;
            object handKey = (controllerIdx == leftIdx) ? LeftHandKey : RightHandKey;

            // Stop existing rumble task for this hand if active
            if (ActiveRumbles.TryGetValue(handKey, out HapticTracker? tracker))
            {
                tracker?.Cts?.Cancel();
                tracker?.Cts?.Dispose();
            }
            else
            {
                tracker = new HapticTracker();
                ActiveRumbles.Add(handKey, tracker);
            }

            tracker.Cts = new CancellationTokenSource();
            var token = tracker.Cts.Token;

            Task.Run(async () =>
            {
                try
                {
                    ushort pulseWidth = (ushort)(Math.Clamp(strength, 0f, 1f) * 6000f); // Map strength (0.0 - 1.0) to OpenVR microseconds (0 - 6000)

                    if (duration == 0f) // Play once and exit loop immediately for short durations.
                        OpenVR.System?.TriggerHapticPulse(controllerIdx, 0U, pulseWidth);
                    else
                    {
                        float elapsed = 0f;
                        float targetFrequency = Math.Clamp(frequency, 10f, 500f);  // Frequency math: Time (seconds) between each pulse = 1 / Frequency
                        float timeBetweenPulses = 1f / targetFrequency;
                        int delayMs = (int)(timeBetweenPulses * 1000f);

                        while (elapsed < duration && !token.IsCancellationRequested)
                        {
                            OpenVR.System?.TriggerHapticPulse(controllerIdx, 0U, pulseWidth);
                            await Task.Delay(Math.Max(1, delayMs), token);
                            elapsed += timeBetweenPulses;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Expected when cancelled
                }
                finally
                {
                    tracker?.Cts = null;
                }
            }, token);
        }

        /// <summary>
        /// Sends haptic rumbles to both left and right controllers with explicit control over frequency and strength.
        /// </summary>
        /// <param name="duration">Total length of the vibration in seconds.</param>
        /// <param name="frequency">Frequency in Hz (Recommended: 40Hz to 320Hz).</param>
        /// <param name="strength">Strength from 0.0 to 1.0.</param>
        public static void RumbleBoth(float duration, float frequency, float strength)
        {
            var system = OpenVR.System;
            if (system == null) return;

            uint leftIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            uint rightIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);

            Rumble(leftIdx, duration, frequency, strength);
            Rumble(rightIdx, duration, frequency, strength);
        }

        public float GetThumbstickY(ulong actionHandle)
        {
            if (actionHandle == 0) return 0f;
            var input = OpenVR.Input;
            if (input == null) return 0f;
            InputAnalogActionData_t data = new();
            EVRInputError err = input.GetAnalogActionData(actionHandle, ref data, (uint)Marshal.SizeOf<InputAnalogActionData_t>(), OpenVR.k_ulInvalidInputValueHandle);
            if (err == EVRInputError.None && data.bActive)
            {
                return data.y;
            }
            return 0f;
        }

        public static Vector3 PosFromMatrix(in HmdMatrix34_t m) => new(m.m3, m.m7, m.m11);

        public static Quaternion RotFromMatrix(in HmdMatrix34_t m)
        {
            float tr = m.m0 + m.m5 + m.m10;
            if (tr > 0f)
            {
                float s = MathF.Sqrt(tr + 1f) * 2f;
                return Quaternion.Normalize(new Quaternion((m.m9 - m.m6) / s, (m.m2 - m.m8) / s, (m.m4 - m.m1) / s, 0.25f * s));
            }
            return Quaternion.Identity;
        }

        public static float GetHmdRefreshRate()
        {
            var system = OpenVR.System;
            if (system == null) return 0f;

            var error = ETrackedPropertyError.TrackedProp_Success;
            float frequency = system.GetFloatTrackedDeviceProperty(
                OpenVR.k_unTrackedDeviceIndex_Hmd,
                ETrackedDeviceProperty.Prop_DisplayFrequency_Float,
                ref error);

            return error == ETrackedPropertyError.TrackedProp_Success ? frequency : 0f;
        }

        public static Matrix4x4 ToMatrix4x4(in HmdMatrix34_t m) => new(m.m0, m.m4, m.m8, 0, m.m1, m.m5, m.m9, 0, m.m2, m.m6, m.m10, 0, m.m3, m.m7, m.m11, 1);
        public static HmdMatrix34_t ToHmdMatrix34(in Matrix4x4 m) => new()
        {
            m0 = m.M11,
            m1 = m.M21,
            m2 = m.M31,
            m3 = m.M41,
            m4 = m.M12,
            m5 = m.M22,
            m6 = m.M32,
            m7 = m.M42,
            m8 = m.M13,
            m9 = m.M23,
            m10 = m.M33,
            m11 = m.M43
        };
        public static Matrix4x4 ToMatrix4x4Proj(in HmdMatrix44_t m) => new(m.m0, m.m4, m.m8, m.m12, m.m1, m.m5, m.m9, m.m13, m.m2, m.m6, m.m10, m.m14, m.m3, m.m7, m.m11, m.m15);
    }
}
