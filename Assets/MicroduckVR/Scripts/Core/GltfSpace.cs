using UnityEngine;

namespace MicroduckVR.Core
{
    /// <summary>
    /// Conversions between glTF space (right-handed, +Y up, +Z forward; the web version's space)
    /// and Unity space (left-handed). glTFast mirrors X on import, so we do the same:
    ///   position / direction (x, y, z)        -> (-x, y, z)
    ///   rotation quaternion (x, y, z, w)       -> (x, -y, -z, w)
    ///   rotation axis (x, y, z), same angle    -> (x, -y, -z)
    /// Rotation axes transform like a quaternion's vector part, so a hinge
    /// q = zero_quat * axisAngle(axis, theta) keeps the same theta and ranges after conversion.
    ///
    /// Yaw: Unity code uses Unity's own convention, forward(yaw) = (sin yaw, 0, cos yaw) with
    /// Quaternion.Euler(0, yaw, 0). A web yaw maps to the negated Unity yaw. Offsets along X, and
    /// yaw or roll offsets, flip sign when porting a web formula; pitch offsets do not.
    /// The duck's side sign is the sign of that side's X in Unity: left = -1, right = +1
    /// (the web code uses left = +1). With that sign, web formulas of the form
    /// "yaw - side * toeIn" or "side * footX" port unchanged.
    /// </summary>
    public static class GltfSpace
    {
        /// <summary>A glTF position or direction in Unity space (X mirrored).</summary>
        public static Vector3 Position(float x, float y, float z) => new Vector3(-x, y, z);
        /// <summary>A glTF position or direction from a JSON array.</summary>
        public static Vector3 Position(double[] v) => Position((float)v[0], (float)v[1], (float)v[2]);

        /// <summary>A glTF rotation in Unity space.</summary>
        public static Quaternion Rotation(float x, float y, float z, float w) => new Quaternion(x, -y, -z, w);
        /// <summary>A glTF rotation from a JSON array (x, y, z, w).</summary>
        public static Quaternion Rotation(double[] q) => Rotation((float)q[0], (float)q[1], (float)q[2], (float)q[3]);

        /// <summary>A glTF hinge axis in Unity space (same angle and range).</summary>
        public static Vector3 Axis(float x, float y, float z) => new Vector3(x, -y, -z);
        /// <summary>A glTF hinge axis from a JSON array.</summary>
        public static Vector3 Axis(double[] a) => Axis((float)a[0], (float)a[1], (float)a[2]);

        /// <summary>Unity-space forward for a Unity-convention yaw (radians).</summary>
        public static Vector3 Forward(float yaw) => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }
}
