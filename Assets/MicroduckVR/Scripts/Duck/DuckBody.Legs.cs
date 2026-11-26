using UnityEngine;

namespace MicroduckVR.Duck
{
    public partial class DuckBody
    {
        // ------------------------------------------------------------------ leg IK
        /// <summary>Leg IK: poses a leg so its sole reaches the target with the given foot yaw and pitch.</summary>
        public void SolveLeg(Leg leg, Vector3 target, float footYaw, float footPitch = 0f)
        {
            Vector3 fdesired = new Vector3(Mathf.Sin(footYaw) * Mathf.Cos(footPitch), -Mathf.Sin(footPitch), Mathf.Cos(footYaw) * Mathf.Cos(footPitch));
            for (int it = 0; it < Iters; it++)
            {
                // 1. hip yaw: foot heading
                Vector3 f = leg.ankle.rotation * leg.fLocal;
                Rot(leg.yawJ, SignedAngle(f, fdesired, Axis(leg.yawJ)));
                // 2. hip roll: bring the sole into the target's plane
                Vector3 R = Pivot(leg.rollJ), S = leg.sole.position;
                Rot(leg.rollJ, SignedAngle(S - R, target - R, Axis(leg.rollJ)));
                // 3. knee: hip-ankle distance
                Vector3 H = Pivot(leg.pitchJ), K = Pivot(leg.kneeJ), A = Pivot(leg.ankleJ);
                S = leg.sole.position;
                Vector3 At = target + (A - S);
                Vector3 wk = Axis(leg.kneeJ);
                float l1 = ProjectOnPlane(K - H, wk).magnitude;
                float l2 = ProjectOnPlane(A - K, wk).magnitude;
                float d = ProjectOnPlane(At - H, wk).magnitude;
                d = Clamp(d, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
                float cur = SignedAngle(H - K, A - K, wk);
                float want = leg.kneeSign * Mathf.Acos(Clamp((l1 * l1 + l2 * l2 - d * d) / (2f * l1 * l2), -1f, 1f));
                Rot(leg.kneeJ, want - cur);
                // 4. hip pitch: swing the leg onto the target
                A = Pivot(leg.ankleJ); H = Pivot(leg.pitchJ);
                Rot(leg.pitchJ, SignedAngle(A - H, At - H, Axis(leg.pitchJ)));
                // 5. ankle: sole flat
                f = leg.ankle.rotation * leg.fLocal;
                Rot(leg.ankleJ, SignedAngle(f, fdesired, Axis(leg.ankleJ)));
            }
            leg.err = Vector3.Distance(leg.sole.position, target);
        }

        float FootError()
        {
            float e = 0f;
            foreach (var leg in Legs.Values)
            {
                Vector3 n = YawRot(Yaw) * leg.neutral + Pos;
                e = Mathf.Max(e, Mathf.Max(new Vector2(n.x - leg.foot.pos.x, n.z - leg.foot.pos.z).magnitude,
                    Mathf.Abs(Wrap(leg.foot.yaw - (Yaw - leg.s * Gait.ToeIn))) * 0.06f));
            }
            return e;
        }

        Vector3 Landing(Leg leg, float tRemain, float tStance, out float toeYaw)
        {
            float t = tRemain + tStance * 0.5f;
            float yawP = Yaw + YawRate * t;
            Vector3 p = YawRot(yawP) * leg.neutral;
            p.x += Pos.x + Vel.x * t; p.z += Pos.z + Vel.z * t;
            toeYaw = yawP - leg.s * Gait.ToeIn;
            return p;
        }

        /// <summary>Snap the feet to neutral (after teleport / get-up).</summary>
        public void ResetFeet()
        {
            transform.SetPositionAndRotation(new Vector3(Pos.x, BaseY, Pos.z), YawRot(Yaw));
            foreach (var leg in Legs.Values)
            {
                Vector3 n = YawRot(Yaw) * leg.neutral + Pos;
                n.y = Ground(n.x, n.z); if (n.y < BaseY - 0.03f) n.y = BaseY;
                leg.foot.pos = n; leg.foot.yaw = Yaw - leg.s * Gait.ToeIn; leg.foot.planted = true; leg.foot.pitch = 0f;
            }
            Phase = 0f; Stepping = false; prevVel = Vector3.zero;
        }
    }
}
