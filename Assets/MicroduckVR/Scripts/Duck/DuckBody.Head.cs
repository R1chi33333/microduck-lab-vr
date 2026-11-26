using MicroduckVR.Core;
using UnityEngine;

namespace MicroduckVR.Duck
{
    public partial class DuckBody
    {
        // ------------------------------------------------------------------ head
        /// <summary>Where the duck's eyes are (the camera marker, else the head).</summary>
        public Vector3 EyePosition => MarkerCam != null ? MarkerCam.position : HeadNode.position;

        void Head(float dt, float walkSpeed)
        {
            Vector3 gaze;
            if (LookAt.gazeDir.HasValue)
            {
                gaze = LookAt.gazeDir.Value.normalized;             // VR: the player's head drives the duck's gaze
            }
            else if (LookAt.target.HasValue && LookAt.weight > 0.01f)
            {
                gaze = (LookAt.target.Value - EyePosition).normalized;
                Vector3 fwd = GltfSpace.Forward(Yaw);
                Vector3 bas = (fwd * Mathf.Cos(LookAt.pitchDown) + new Vector3(0f, -Mathf.Sin(LookAt.pitchDown), 0f)).normalized;
                gaze = Vector3.Lerp(gaze, bas, 1f - LookAt.weight).normalized;
            }
            else
            {
                float yawB = Clamp(YawRate * 0.28f, -0.6f, 0.6f) + LookAt.yawBias + GazeYawOff;
                float y = Yaw + yawB;
                float p = LookAt.pitchDown + walkSpeed * 0.6f + GazePitchOff - SitW * 0.25f;
                gaze = new Vector3(Mathf.Sin(y) * Mathf.Cos(p), -Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
            }
            float nod = Stepping ? Mathf.Sin(Phase * Mathf.PI * 4f) * 0.03f : 0f;
            SetQ(JointNames.NeckPitch, joints[JointNames.NeckPitch].stand - walkSpeed * 0.9f + nod); ApplyJoint(JointNames.NeckPitch);
            for (int it = 0; it < Mathf.Min(2, Iters); it++)
            {
                Vector3 f = HeadNode.rotation * headF;
                float dYaw = SignedAngle(f, gaze, Axis(JointNames.HeadYaw));
                SetQ(JointNames.HeadYaw, Clamp(Q[JointNames.HeadYaw] + dYaw, -1.0f, 1.0f)); ApplyJoint(JointNames.HeadYaw);
                f = HeadNode.rotation * headF;
                Rot(JointNames.HeadPitch, SignedAngle(f, gaze, Axis(JointNames.HeadPitch)));
                // level the head (roll), with a little tilt when idle
                Vector3 r = HeadNode.rotation * headLeft;
                f = HeadNode.rotation * headF;
                Vector3 rd = Vector3.Cross(f, Vector3.up).normalized;   // desired "left" in Unity
                rd = Quaternion.AngleAxis(-idleTilt * Mathf.Rad2Deg, f) * rd;
                Rot(JointNames.HeadRoll, SignedAngle(r, rd, Axis(JointNames.HeadRoll)));
            }
        }

        /// <summary>The point between the two beak halves, where a held toy sits.</summary>
        public Vector3 BeakGap() => MarkerUpper != null && MarkerLower != null ? (MarkerUpper.position + MarkerLower.position) * 0.5f : HeadNode.position;
        /// <summary>How far the beak is open (m).</summary>
        public float BeakOpening() => MarkerUpper != null && MarkerLower != null ? Vector3.Distance(MarkerUpper.position, MarkerLower.position) : 0f;
        /// <summary>The direction the head faces.</summary>
        public Vector3 HeadDir() => (HeadNode.rotation * headF).normalized;

        void ReachCCD()
        {
            if (!ReachState.target.HasValue) return;
            Vector3 T = ReachState.target.Value;
            for (int it = 0; it < 8; it++)
            {
                foreach (var jn in new[] { JointNames.HeadPitch, JointNames.NeckPitch })
                {
                    Vector3 E = BeakGap(), P = Pivot(jn);
                    float d = SignedAngle(E - P, T - P, Axis(jn));
                    Rot(jn, d * 0.8f);
                }
            }
            ReachState.err = Vector3.Distance(BeakGap(), T);
        }

        void UpdateJaw(float dt)
        {
            float tgt = Mathf.Max(JawTarget, JawStop);
            const float rate = 3.2f;
            Jaw += Clamp(tgt - Jaw, -rate * dt, rate * dt);
            SetQ(JointNames.Jaw, Jaw); ApplyJoint(JointNames.Jaw);
        }
    }
}
