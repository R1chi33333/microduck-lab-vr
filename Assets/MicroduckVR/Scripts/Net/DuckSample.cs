using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>One snapshot of an AI duck.</summary>
    public struct DuckSample
    {
        /// <summary>Where the duck is, and where its head and beak aim.</summary>
        public Vector3 pos, lookTarget, reachTarget;
        /// <summary>Heading, velocity, turn rate, jaw, hop, happy wiggle and the look and reach weights.</summary>
        public float yaw, vx, vz, yawRate, jaw, hop, happy, lookWeight, reachWeight;
        /// <summary>Sitting, crouching, looking at something, reaching for something.</summary>
        public bool sit, crouch, look, reach;

        /// <summary>A blend of two snapshots (u = 0 gives a, 1 gives b).</summary>
        public static DuckSample Lerp(DuckSample a, DuckSample b, float u)
        {
            var r = u < 0.5f ? a : b;
            r.pos = Vector3.Lerp(a.pos, b.pos, u);
            r.yaw = NetMath.LerpYaw(a.yaw, b.yaw, u);
            r.vx = Mathf.Lerp(a.vx, b.vx, u); r.vz = Mathf.Lerp(a.vz, b.vz, u);
            r.yawRate = Mathf.Lerp(a.yawRate, b.yawRate, u);
            r.jaw = Mathf.Lerp(a.jaw, b.jaw, u); r.hop = Mathf.Lerp(a.hop, b.hop, u); r.happy = Mathf.Lerp(a.happy, b.happy, u);
            r.lookWeight = Mathf.Lerp(a.lookWeight, b.lookWeight, u); r.reachWeight = Mathf.Lerp(a.reachWeight, b.reachWeight, u);
            if (a.look && b.look) r.lookTarget = Vector3.Lerp(a.lookTarget, b.lookTarget, u);
            if (a.reach && b.reach) r.reachTarget = Vector3.Lerp(a.reachTarget, b.reachTarget, u);
            return r;
        }
    }
}
