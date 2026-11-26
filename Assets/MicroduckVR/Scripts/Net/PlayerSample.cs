using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>One snapshot of a player's duck, as the other PC sees it.</summary>
    public struct PlayerSample
    {
        public Vector3 pos, gaze, reachTarget, laserFrom, laserPoint;
        public float yaw, vx, vz, yawRate, jaw, hop, lean, stroke, strokeSide, wobble, reachWeight;
        public bool skating, reach, laser;

        public static PlayerSample Lerp(PlayerSample a, PlayerSample b, float u)
        {
            var r = u < 0.5f ? a : b;                     // flags and on/off things from the nearer one
            r.pos = Vector3.Lerp(a.pos, b.pos, u);
            r.yaw = NetMath.LerpYaw(a.yaw, b.yaw, u);
            r.vx = Mathf.Lerp(a.vx, b.vx, u); r.vz = Mathf.Lerp(a.vz, b.vz, u);
            r.yawRate = Mathf.Lerp(a.yawRate, b.yawRate, u);
            r.gaze = Vector3.Slerp(a.gaze, b.gaze, u);
            r.jaw = Mathf.Lerp(a.jaw, b.jaw, u); r.hop = Mathf.Lerp(a.hop, b.hop, u);
            r.lean = Mathf.Lerp(a.lean, b.lean, u); r.stroke = b.stroke; r.wobble = Mathf.Lerp(a.wobble, b.wobble, u);
            r.reachWeight = Mathf.Lerp(a.reachWeight, b.reachWeight, u);
            if (a.reach && b.reach) r.reachTarget = Vector3.Lerp(a.reachTarget, b.reachTarget, u);
            if (a.laser && b.laser) { r.laserFrom = Vector3.Lerp(a.laserFrom, b.laserFrom, u); r.laserPoint = Vector3.Lerp(a.laserPoint, b.laserPoint, u); }
            return r;
        }
    }
}
