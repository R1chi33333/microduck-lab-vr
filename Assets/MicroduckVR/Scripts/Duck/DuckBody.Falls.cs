using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.Duck
{
    public partial class DuckBody
    {
        // ------------------------------------------------------------------ falls
        public void StartTip(Vector3 dirWorld)
        {
            if (FallState != null) return;
            Vector3 d = Quaternion.Inverse(YawRot(Yaw)) * new Vector3(dirWorld.x, 0f, dirWorld.z).normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, d).normalized;
            bool sideways = Mathf.Abs(d.x) > Mathf.Abs(d.z);
            FallState = new Fall { phase = FallPhase.Tip, axis = axis, pivot = d * 0.045f, angle = 0.05f, angVel = 1.2f, rest = sideways ? 1.32f : (d.z > 0f ? 1.15f : 1.25f), from = new Dictionary<string, float>(Q) };
            Stepping = false;
            OnFall?.Invoke(dirWorld);
        }

        public void StartTumble(Vector3 dirWorld)
        {
            Vector3 d = Quaternion.Inverse(YawRot(Yaw)) * new Vector3(dirWorld.x, 0f, dirWorld.z).normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, d).normalized;
            FallState = new Fall { phase = FallPhase.Tumble, axis = axis, pivot = new Vector3(0f, 0.1f, 0f), angle = 0f, angVel = 4.5f, rest = Mathf.PI * 0.5f + 1.0f, from = new Dictionary<string, float>(Q) };
            Stepping = false;
        }

        public void LandTumble()
        {
            var f = FallState; if (f == null) return;
            float a = ((f.angle % (Mathf.PI * 2f)) + Mathf.PI * 2f) % (Mathf.PI * 2f);
            f.angle = a; f.phase = FallPhase.Tip; f.pivot = Vector3.zero; f.angVel = 0f; f.rest = a > Mathf.PI ? Mathf.PI * 2f - 1.3f : 1.3f;
            if (a > Mathf.PI) { f.angle = a - Mathf.PI * 2f; f.rest = -1.3f; }
            f.landed = true;
            OnSound?.Invoke(BodySound.FallHit, 1f);
        }

        void PoseFall()
        {
            var f = FallState;
            Tilt.localPosition = f.pivot;
            Tilt.localRotation = Quaternion.AngleAxis(f.angle * Mathf.Rad2Deg, f.axis);
            Model.localPosition = new Vector3(-trunkBase.x - f.pivot.x, -f.pivot.y + f.lift, -trunkBase.z - f.pivot.z);
        }

        float MinY()
        {
            float m = float.PositiveInfinity;
            foreach (var r in renderers) if (r != null && r.enabled && r.gameObject.activeInHierarchy) m = Mathf.Min(m, r.bounds.min.y);
            return m;
        }

        void UpdateFall(float dt)
        {
            var f = FallState; f.t += dt;
            float limpW = f.phase == FallPhase.GetUp ? 1f - Smooth(Clamp(f.t / 1.4f, 0f, 1f)) : Smooth(Clamp(f.t / 0.35f, 0f, 1f));
            if (f.phase == FallPhase.Tumble) f.angle += f.angVel * dt;
            else if (f.phase == FallPhase.Tip)
            {
                float sgn = Mathf.Sign(f.rest); if (sgn == 0f) sgn = 1f;
                float ang = Mathf.Abs(f.angle);
                f.angVel += Mathf.Sin(Mathf.Max(ang, 0.05f)) * 38f * dt * sgn;
                f.angle += f.angVel * dt;
                if (Mathf.Abs(f.angle) >= Mathf.Abs(f.rest))
                {
                    if (Mathf.Abs(f.angVel) > 1.2f) OnSound?.Invoke(BodySound.FallHit, Mathf.Min(1.2f, Mathf.Abs(f.angVel) / 6f));
                    f.angle = f.rest; f.angVel = -f.angVel * 0.22f;
                    if (Mathf.Abs(f.angVel) < 0.3f) { f.phase = FallPhase.Lie; f.t = 0f; f.angVel = 0f; }
                }
            }
            else if (f.phase == FallPhase.Lie)
            {
                if (f.t > f.lieTime) { f.phase = FallPhase.GetUp; f.t = 0f; f.getFrom = f.angle; OnSound?.Invoke(BodySound.GetUp, null); }
            }
            else if (f.phase == FallPhase.GetUp)
            {
                float u0 = Clamp(f.t / 1.6f, 0f, 1f);
                f.angle = f.getFrom * (1f - Smoother(u0));
                if (u0 >= 1f)
                {
                    FallState = null;
                    Tilt.localPosition = Vector3.zero; Tilt.localRotation = Quaternion.identity;
                    Model.localPosition = new Vector3(-trunkBase.x, 0f, -trunkBase.z);
                    Trunk.localPosition = trunkBase; Trunk.localRotation = Quaternion.identity;
                    ResetFeet();
                    OnGotUp?.Invoke();
                    return;
                }
            }
            if (f.phase == FallPhase.GetUp)
            {
                float u = Clamp(f.t / 1.6f, 0f, 1f), e = Smoother(u);
                float push = Mathf.Sin(Mathf.PI * Clamp(u * 1.25f - 0.1f, 0f, 1f));
                var P = new Dictionary<string, float> { { "left_knee", 0.9f }, { "right_knee", -0.9f }, { "left_hip_pitch", 0.45f }, { "right_hip_pitch", -0.45f }, { "left_ankle", 0.45f }, { "right_ankle", -0.45f } };
                foreach (var k in new List<string>(joints.Keys))
                {
                    if (k == "jaw") continue;
                    float v = Lerp(limp[k], joints[k].stand, e) + (P.TryGetValue(k, out var pk) ? pk : 0f) * push;
                    if (k == "head_roll") v += Mathf.Sin(f.t * 17f) * 0.22f * Clamp((u - 0.78f) * 5f, 0f, 1f) * (1f - u) * 4f;
                    SetQ(k, v); ApplyJoint(k);
                }
                float wIK = Smooth(Clamp((u - 0.25f) / 0.45f, 0f, 1f));
                if (wIK > 0f)
                {
                    f.lift = 0f; PoseFall(); Trunk.localPosition = trunkBase;
                    foreach (var leg in Legs.Values)
                    {
                        var names = new[] { leg.yawJ, leg.rollJ, leg.pitchJ, leg.kneeJ, leg.ankleJ };
                        var anim = new float[names.Length];
                        for (int i = 0; i < names.Length; i++) anim[i] = Q[names[i]];
                        Vector3 tgt = YawRot(Yaw) * leg.neutral + Pos; tgt.y = BaseY;
                        SolveLeg(leg, tgt, Yaw - leg.s * Gait.ToeIn);
                        for (int i = 0; i < names.Length; i++) { SetQ(names[i], Lerp(anim[i], Q[names[i]], wIK)); ApplyJoint(names[i]); }
                    }
                }
            }
            else
            {
                foreach (var k in new List<string>(joints.Keys))
                {
                    if (k == "jaw") continue;
                    SetQ(k, Lerp(f.from[k], limp[k], limpW)); ApplyJoint(k);
                }
            }
            Trunk.localPosition = Vector3.Lerp(Trunk.localPosition, trunkBase, 1f - Mathf.Exp(-10f * dt));
            Trunk.localRotation = Quaternion.Slerp(Trunk.localRotation, Quaternion.identity, 1f - Mathf.Exp(-10f * dt));
            if (f.phase == FallPhase.Tumble) { SetQ("left_hip_roll", -0.3f + Mathf.Sin(f.t * 20f) * 0.1f); ApplyJoint("left_hip_roll"); }
            f.lift = 0f; PoseFall();
            if (f.phase != FallPhase.Tumble)
            {
                float my = MinY(), gy = BaseY;
                if (my < gy) { f.lift = gy - my; PoseFall(); }
            }
        }
    }
}
