using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>Position and rotation of a toy.</summary>
    public struct NetPose
    {
        /// <summary>Position and rotation.</summary>
        public Vector3 pos; public Quaternion rot;
        /// <summary>A blend of two poses (u = 0 gives a, 1 gives b).</summary>
        public static NetPose Lerp(NetPose a, NetPose b, float u) => new NetPose { pos = Vector3.Lerp(a.pos, b.pos, u), rot = Quaternion.Slerp(a.rot, b.rot, u) };
    }
}
