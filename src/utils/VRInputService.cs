using System.Numerics;
using System.Runtime.InteropServices;
using Valve.VR;

namespace ScreenLookup.src.utils
{
    public class VRInputService
    {
        private readonly TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];

        public uint LeftControllerIdx { get; private set; } = OpenVR.k_unTrackedDeviceIndexInvalid;
        public uint RightControllerIdx { get; private set; } = OpenVR.k_unTrackedDeviceIndexInvalid;

        public TrackedDevicePose_t[] Poses => poses;

        public uint GripButtonId { get; set; } = (uint)EVRButtonId.k_EButton_Grip;
        public uint TriggerButtonId { get; set; } = (uint)EVRButtonId.k_EButton_SteamVR_Trigger;
        public uint AButtonId { get; set; } = (uint)EVRButtonId.k_EButton_IndexController_A;
        public uint BButtonId { get; set; } = (uint)EVRButtonId.k_EButton_IndexController_B;

        public void UpdatePosesAndIndices()
        {
            var system = OpenVR.System;
            if (system == null) return;

            system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0.0f, poses);
            LeftControllerIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            RightControllerIdx = system.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
        }

        public bool IsButtonHeld(uint controllerIdx, uint buttonId)
        {
            var system = OpenVR.System;
            if (controllerIdx == OpenVR.k_unTrackedDeviceIndexInvalid || system == null)
                return false;

            var state = new VRControllerState_t();
            return system.GetControllerState(controllerIdx, ref state, (uint)Marshal.SizeOf<VRControllerState_t>()) &&
                   (state.ulButtonPressed & (1UL << (int)buttonId)) != 0;
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
        public static Matrix4x4 ToMatrix4x4Proj(in HmdMatrix44_t m) => new(m.m0, m.m4, m.m8, m.m12, m.m1, m.m5, m.m9, m.m13, m.m2, m.m6, m.m10, m.m14, m.m3, m.m7, m.m11, m.m15);
    }
}