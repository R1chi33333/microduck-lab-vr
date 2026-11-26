namespace MicroduckVR.Duck
{
    /// <summary>Joint names of the duck rig (DuckJointMap, from the GLB) and the rig nodes the code looks up by name.</summary>
    public static class JointNames
    {
        // ---- joints
        public const string NeckPitch = "neck_pitch", HeadPitch = "head_pitch", HeadYaw = "head_yaw", HeadRoll = "head_roll", Jaw = "jaw";
        public const string LeftHipYaw = "left_hip_yaw", LeftHipRoll = "left_hip_roll", LeftHipPitch = "left_hip_pitch", LeftKnee = "left_knee", LeftAnkle = "left_ankle";
        public const string RightHipYaw = "right_hip_yaw", RightHipRoll = "right_hip_roll", RightHipPitch = "right_hip_pitch", RightKnee = "right_knee", RightAnkle = "right_ankle";

        // ---- rig nodes
        public const string TrunkNode = "body_trunk", HeadNode = "body_jaw_soft";
        /// <summary>The top node of each leg (the hip yaw).</summary>
        public const string LeftLegRoot = "body_yaw2roll", RightLegRoot = "body_bearing_roll";
    }
}
