using System;
using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.Duck
{
    /// <summary>
    /// Articulated Microduck body (port of duck.js): joint posing from the rig's hinge data,
    /// 2-link leg IK with foot planting, procedural pigeon-toed gait, head look / stabilisation,
    /// beak reach (CCD), jaw, sit / crouch / skates / emotes, tip-over and get-up.
    /// Unity space; see GltfSpace for the mirroring rules used when porting formulas.
    /// Hierarchy built at Init: root (this transform, ground point + yaw) > tilt > model > rig (= body_trunk).
    /// </summary>
    public partial class DuckBody : MonoBehaviour
    {
        // ------------------------------------------------------------------ types
        class JointState
        {
            public Transform node;
            public Quaternion zero;
            public Vector3 axis;
            public float min, max, stand;
        }

        public class Foot
        {
            public Vector3 pos, from;
            public float yaw, fromYaw, fromGroundY, toY, pitch, s = 1f;
            public bool planted = true, justLanded;
        }

        public class Leg
        {
            public string side;
            public float s;                       // Unity side sign: left = -1, right = +1
            public string yawJ, rollJ, pitchJ, kneeJ, ankleJ;
            public Transform top, sole, ankle;
            public Vector3 fLocal;                // ankle-local forward
            public float kneeSign;
            public Vector3 neutral;               // root-local at yaw 0
            public Foot foot = new Foot();
            public float err;
        }

        /// <summary>Stage of a fall: tipping over, tumbling (thrown), lying, getting up.</summary>
        public enum FallPhase { Tip, Tumble, Lie, GetUp }

        public class Fall
        {
            public FallPhase phase;
            public Vector3 axis, pivot;
            public float angle, angVel, rest, t, lift, getFrom, lieTime = 1.1f;
            public Dictionary<string, float> from;
            public bool landed;
        }

        public class Emote { public string kind; public float t, dur; }

        public class Look { public Vector3? target; public float weight; public float pitchDown = 0.26f; public float yawBias; public Vector3? gazeDir; }
        public class Reach { public Vector3? target; public float weight, w, err, crouch = 0.034f, bow = 0.62f; public Dictionary<string, float> sol; }

        // ------------------------------------------------------------------ config + state
        public Func<float, float, float> Ground = (x, z) => 0f;
        public Action<BodySound, object> OnSound;
        public Action OnGotUp;
        /// <summary>An emote started (kind, duration) and a tip-over started (direction): the network session passes them on.</summary>
        public Action<string, float> OnEmote;
        public Action<Vector3> OnFall;

        public Transform Tilt { get; private set; }
        public Transform Model { get; private set; }
        public Transform Trunk { get; private set; }
        public Transform HeadNode { get; private set; }
        public readonly Dictionary<string, Transform> Nodes = new Dictionary<string, Transform>();
        public readonly Dictionary<string, Leg> Legs = new Dictionary<string, Leg>();
        public readonly Dictionary<string, float> Q = new Dictionary<string, float>();
        readonly Dictionary<string, JointState> joints = new Dictionary<string, JointState>();
        Dictionary<string, float> limp;

        Vector3 trunkBase;
        Vector3 headF, headLeft;
        public Transform MarkerUpper, MarkerLower, MarkerCam, MarkerTof, MarkerMouth;
        Renderer[] renderers = Array.Empty<Renderer>();
        public float StandHeight { get; private set; }
        public float SitDrop { get; private set; } = 0.05f;

        public Vector3 Pos;
        public float Yaw;                              // Unity convention, radians
        public Vector3 Vel;
        public float YawRate;
        public float BaseY, Phase, CycleHz = 2.2f;
        public bool Stepping;
        Vector3 trunkOff, prevVel;
        float lean, gw, idleTilt, time;
        public float Jaw, JawTarget, JawStop;
        public readonly Look LookAt = new Look();
        public readonly Reach ReachState = new Reach();
        public float CrouchExtra, PitchExtra;
        [System.NonSerialized] public Fall FallState;
        public bool EnabledIK = true;
        public int Iters = 3;
        public bool SitTarget, CrouchTarget;
        public float SitW, CrouchW;
        public bool Skating;
        public float SkateLift = 0.022f, SkateW, Stroke, StrokeSide = 1f, Wobble;
        public float SkateLean, LeanW;                 // body roll on skates (Unity roll convention)
        [System.NonSerialized] public Emote CurrentEmote;
        public float Hop, GazeYawOff, GazePitchOff, HappyWiggle;
        public bool Fallen => FallState != null;

        static readonly (string side, float s, string yaw, string roll, string pitch, string knee, string ankle, string sole)[] LegNodes =
        {
            ("left", -1f, "body_yaw2roll", "body_hip_l", "body_upper_leg_left", "body_leg", "body_ankle_left", "marker_left_foot"),
            ("right", 1f, "body_bearing_roll", "body_hip_l_2", "body_upper_leg_right", "body_leg_2", "body_ankle_right", "marker_right_foot"),
        };

        // ------------------------------------------------------------------ setup
        /// <summary>Wraps an instantiated rig (glTFast instance; its root is body_trunk) and reads the joints.</summary>
        public void Init(GameObject rigInstance, DuckJointMap map)
        {
            var savedPos = transform.position; var savedRot = transform.rotation;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Tilt = new GameObject("duck_tilt").transform; Tilt.SetParent(transform, false);
            Model = new GameObject("duck_model").transform; Model.SetParent(Tilt, false);
            rigInstance.transform.SetParent(Model, false);
            Trunk = rigInstance.transform;
            Trunk.localPosition = map.trunkTranslation;
            Trunk.localRotation = Quaternion.identity;
            CollectNodes(Trunk);
            Nodes["body_trunk"] = Trunk;
            trunkBase = map.trunkTranslation;
            Model.localPosition = new Vector3(-trunkBase.x, 0f, -trunkBase.z);
            StandHeight = trunkBase.y;

            foreach (var j in map.joints)
            {
                if (!Nodes.TryGetValue(j.node, out var node)) { Debug.LogWarning("[Microduck] rig node missing: " + j.node); continue; }
                joints[j.joint] = new JointState { node = node, zero = j.zero, axis = j.axis.normalized, min = j.min, max = j.max, stand = j.stand };
                Q[j.joint] = j.stand;
            }
            ApplyAll();

            foreach (var L in LegNodes)
            {
                string p = L.side + "_";
                var leg = new Leg
                {
                    side = L.side, s = L.s,
                    yawJ = p + "hip_yaw", rollJ = p + "hip_roll", pitchJ = p + "hip_pitch", kneeJ = p + "knee", ankleJ = p + "ankle",
                    top = Nodes[L.yaw], sole = Nodes[L.sole], ankle = Nodes[L.ankle],
                };
                leg.fLocal = Quaternion.Inverse(leg.ankle.rotation) * Vector3.forward;
                Vector3 H = Pivot(leg.pitchJ), K = Pivot(leg.kneeJ), A = Pivot(leg.ankleJ);
                float ks = Mathf.Sign(SignedAngle(H - K, A - K, Axis(leg.kneeJ)));
                leg.kneeSign = ks == 0f ? 1f : ks;
                Vector3 sole = leg.sole.position;
                leg.neutral = new Vector3(L.s * Gait.FootX, 0f, sole.z);
                leg.foot.pos = sole; leg.foot.from = sole;
                Legs[L.side] = leg;
            }

            HeadNode = Nodes["body_jaw_soft"];
            var hw = Quaternion.Inverse(HeadNode.rotation);
            headF = hw * Vector3.forward;
            headLeft = hw * Vector3.left;              // the duck's left is -X in Unity
            Nodes.TryGetValue("marker_beak_upper_tip", out MarkerUpper);
            Nodes.TryGetValue("marker_beak_lower_tip", out MarkerLower);
            Nodes.TryGetValue("marker_head_camera", out MarkerCam);
            Nodes.TryGetValue("marker_tof", out MarkerTof);
            Nodes.TryGetValue("marker_mouth_tip", out MarkerMouth);

            limp = new Dictionary<string, float>(Q);
            // limp pose (theta values are handedness-independent)
            var lp = new Dictionary<string, float> {
                { "left_hip_pitch", -0.05f }, { "left_knee", 0.85f }, { "left_ankle", 0.55f }, { "right_hip_pitch", 0.05f }, { "right_knee", -0.85f }, { "right_ankle", -0.55f },
                { "left_hip_roll", -0.2f }, { "right_hip_roll", 0.2f }, { "neck_pitch", 0.1f }, { "head_pitch", 0.7f }, { "head_roll", 0.15f }, { "jaw", 0.25f } };
            foreach (var kv in lp) limp[kv.Key] = kv.Value;

            renderers = Trunk.GetComponentsInChildren<Renderer>(true);
            float m = float.PositiveInfinity;
            foreach (Transform c in Trunk) { var r = c.GetComponent<Renderer>(); if (r != null) m = Mathf.Min(m, r.bounds.min.y); }
            SitDrop = float.IsInfinity(m) ? 0.05f : Mathf.Max(0.03f, m - 0.009f);

            transform.SetPositionAndRotation(savedPos, savedRot);
            Pos = new Vector3(savedPos.x, savedPos.y, savedPos.z);
            Yaw = savedRot.eulerAngles.y * Mathf.Deg2Rad;
            Teleport(Pos, Yaw);
        }

        void CollectNodes(Transform t)
        {
            if (!Nodes.ContainsKey(t.name)) Nodes[t.name] = t;
            foreach (Transform c in t) CollectNodes(c);
        }

        // ------------------------------------------------------------------ math helpers
        static float Clamp(float x, float a, float b) => Mathf.Max(a, Mathf.Min(b, x));
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Smooth(float t) => t * t * (3f - 2f * t);
        static float Smoother(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        static float Damp(float cur, float target, float lambda, float dt) => Lerp(cur, target, 1f - Mathf.Exp(-lambda * dt));
        static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - n * Vector3.Dot(v, n);
        /// <summary>Signed angle from a to b about n, both projected onto the plane normal to n.</summary>
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 n)
        {
            Vector3 ap = ProjectOnPlane(a, n), bp = ProjectOnPlane(b, n);
            return Mathf.Atan2(Vector3.Dot(n, Vector3.Cross(ap, bp)), Vector3.Dot(ap, bp));
        }
        static Quaternion YawRot(float yaw) => Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up);

        // ------------------------------------------------------------------ joints
        public bool HasJoint(string name) => joints.ContainsKey(name);
        public float JointStand(string name) => joints[name].stand;
        public void SetQ(string name, float theta) { if (joints.TryGetValue(name, out var j)) Q[name] = Clamp(theta, j.min, j.max); }
        public void ApplyJoint(string name)
        {
            if (!joints.TryGetValue(name, out var j)) return;
            j.node.localRotation = j.zero * Quaternion.AngleAxis(Q[name] * Mathf.Rad2Deg, j.axis);
        }
        public void ApplyAll() { foreach (var k in joints.Keys) ApplyJoint(k); }
        public Vector3 Axis(string name)
        {
            var j = joints[name];
            return (j.node.parent.rotation * j.zero * j.axis).normalized;
        }
        public Vector3 Pivot(string name) => joints[name].node.position;
        void Rot(string name, float delta) { SetQ(name, Q[name] + delta); ApplyJoint(name); }

        // ------------------------------------------------------------------ main update
        /// <summary>pos = ground point (y used only with yOverride); yaw Unity convention (radians); vel world.</summary>
        public void Step(float dt, Vector3 pos, float yaw, Vector3 vel, float yawRate, float? yOverride = null)
        {
            dt = Mathf.Min(dt, 1f / 20f);
            time += dt;
            Pos = pos; Yaw = yaw; Vel = vel; YawRate = yawRate;
            float speed = new Vector2(Vel.x, Vel.z).magnitude;
            float sp01 = Clamp(speed / Gait.MaxSpeed, 0f, 1f);

            float gC = Ground(Pos.x, Pos.z);
            Foot L = Legs["left"].foot, R = Legs["right"].foot;
            float gFeet = (Mathf.Min(L.pos.y, R.pos.y) + Mathf.Max(L.pos.y, R.pos.y)) * 0.5f;
            float gTarget = FallState != null ? gC : Mathf.Max(gC - 0.012f, Mathf.Min(gC + 0.012f, gFeet * 0.6f + gC * 0.4f));
            if (yOverride.HasValue) BaseY = yOverride.Value;
            else BaseY = Mathf.Abs(BaseY - gTarget) > 0.2f ? gTarget : Damp(BaseY, gTarget, 14f, dt);
            transform.SetPositionAndRotation(new Vector3(Pos.x, BaseY + Hop, Pos.z), YawRot(Yaw));

            if (FallState != null) { UpdateFall(dt); UpdateJaw(dt); return; }

            float footErr = FootError();
            float sitWas = SitW, crouchWas = CrouchW;
            SitW = Damp(SitW, SitTarget ? 1f : 0f, 3.5f, dt);
            CrouchW = Damp(CrouchW, CrouchTarget ? 1f : 0f, 7f, dt);
            if (OnSound != null) { if (sitWas < 0.8f && SitW >= 0.8f) OnSound(BodySound.Sit, null); if (crouchWas < 0.7f && CrouchW >= 0.7f) OnSound(BodySound.Crouch, null); }
            SkateW = Damp(SkateW, Skating ? 1f : 0f, 8f, dt);
            var em = EmoteOffsets(dt);
            bool wantStep = !Skating && SitW < 0.3f && (speed > 0.006f || Mathf.Abs(YawRate) > 0.08f || footErr > 0.012f);
            if (Skating) Stepping = false;
            float hz = Lerp(2.0f, 2.9f, sp01) + Mathf.Max(0f, speed - Gait.MaxSpeed) * 9f + Mathf.Min(Mathf.Abs(YawRate), 1.5f) * 0.25f;
            CycleHz = Damp(CycleHz, hz, 6f, dt);
            if (wantStep) Stepping = true;
            if (Stepping)
            {
                float prev = Phase;
                Phase = (Phase + CycleHz * dt) % 1f;
                if (!wantStep)
                {
                    float sw = Gait.Swing;
                    bool Crossed(float a, float b, float x) => a <= b ? (a < x && b >= x) : (a < x || b >= x);
                    if ((Crossed(prev, Phase, sw) || Crossed(prev, Phase, 0.5f + sw)) && FootError() < 0.012f) Stepping = false;
                }
            }

            // ---- trunk motion (root-local); x, yaw and roll mirrored from the web formulas
            float ph = Phase * Mathf.PI * 2f;
            float g = Stepping ? 1f : 0f;
            gw = Damp(gw, g * (0.35f + 0.65f * sp01) + g * Mathf.Min(Mathf.Abs(YawRate), 1f) * 0.3f, 8f, dt);
            Vector3 accel = (Vel - prevVel) / Mathf.Max(dt, 1e-3f); prevVel = Vel;
            Vector3 fwd = GltfSpace.Forward(Yaw);
            float aF = Clamp(Vector3.Dot(accel, fwd), -2f, 2f);
            lean = Damp(lean, sp01 * 0.07f + aF * 0.02f, 6f, dt);
            float sway = Mathf.Sin(ph) * 0.0045f * gw;
            float bob = (Mathf.Cos(ph * 2f) * 0.5f + 0.5f) * 0.0028f * gw;
            float breathe = Mathf.Sin(time * 1.7f) * 0.0006f;
            float sk = SkateW, glide = Mathf.Sin(Stroke * Mathf.PI * 2f);
            LeanW = Damp(LeanW, SkateLean, 9f, dt);
            float skateRoll = sk * (LeanW + glide * 0.05f * StrokeSide + Wobble * Mathf.Sin(time * 15f) * 0.2f);
            trunkOff = new Vector3(-(sway + sk * glide * 0.004f) + em.x,
                -Gait.Crouch * gw - bob + breathe - CrouchExtra - SitW * SitDrop - CrouchW * 0.028f + sk * SkateLift + em.y, 0f);
            float pitch = lean + PitchExtra + Mathf.Sin(ph * 2f) * 0.012f * gw - SitW * 0.16f + CrouchW * 0.1f + sk * 0.12f + em.pitch;
            float yawT = -(Mathf.Sin(ph) * 0.05f * gw) + em.yaw;
            float roll = Mathf.Sin(ph) * 0.045f * gw + skateRoll + em.roll;
            Trunk.localPosition = trunkBase + trunkOff;
            Trunk.localRotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, yawT * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);

            // ---- feet
            if (EnabledIK)
            {
                float sw = Gait.Swing;
                foreach (var side in new[] { "left", "right" })
                {
                    var leg = Legs[side]; var F = leg.foot;
                    float start = side == "left" ? 0f : 0.5f;
                    float s = ((Phase - start) % 1f + 1f) % 1f;
                    bool inSwing = Stepping && s < sw;
                    if (SkateW > 0.01f && !inSwing)
                    {
                        float push = Mathf.Approximately(StrokeSide, leg.s) ? Mathf.Max(0f, Mathf.Sin(Stroke * Mathf.PI * 2f)) : 0f;
                        Vector3 tgt = YawRot(Yaw) * (leg.neutral + new Vector3(leg.s * (0.004f + push * 0.016f), 0f, -push * 0.035f)) + Pos;
                        tgt.y = BaseY + SkateLift + push * 0.006f;
                        F.pos = Vector3.Lerp(F.pos, tgt, SkateW > 0.95f ? 1f : 0.25f); F.yaw = Yaw + leg.s * push * 0.35f; F.planted = true; F.pitch = 0f;
                    }
                    else if (SitW > 0.01f && !inSwing)
                    {
                        Vector3 tgt = YawRot(Yaw) * (leg.neutral + new Vector3(leg.s * 0.006f * SitW, 0f, 0.03f * SitW)) + Pos;
                        F.pos.x = Damp(F.pos.x, tgt.x, 6f, dt); F.pos.z = Damp(F.pos.z, tgt.z, 6f, dt);
                        F.pitch = -0.25f * SitW;
                    }
                    else if (inSwing)
                    {
                        float u = s / sw;
                        if (F.planted) { F.planted = false; F.from = F.pos; F.fromYaw = F.yaw; F.fromGroundY = F.pos.y; }
                        var T = Landing(leg, (1f - u) * sw / CycleHz, (1f - sw) / CycleHz, out float tYaw);
                        float e = Smoother(Mathf.Min(1f, u * 1.08f));
                        F.pos.x = Lerp(F.from.x, T.x, e); F.pos.z = Lerp(F.from.z, T.z, e);
                        float gy = Ground(T.x, T.z); if (gy < BaseY - 0.05f) gy = BaseY;
                        float gyc = Clamp(gy, BaseY - 0.03f, BaseY + 0.03f);
                        F.toY = gyc;
                        F.pos.y = Lerp(F.fromGroundY, gyc, e) + Gait.Lift * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.9f) * (0.6f + 0.4f * sp01);
                        F.yaw = F.fromYaw + Wrap(tYaw - F.fromYaw) * e;
                        F.pitch = Mathf.Sin(Mathf.PI * u) * 0.12f * sp01 - (u > 0.85f ? (u - 0.85f) * 0.4f : 0f);
                        F.s = u;
                    }
                    else
                    {
                        if (!F.planted) { F.planted = true; F.pos.y = F.toY; F.pitch = 0f; F.justLanded = true; OnSound?.Invoke(BodySound.Plant, side); }
                        F.pitch = 0f;
                        if (SkateW <= 0.01f && F.pos.y > BaseY + 0.008f && !Stepping) F.pos.y = Damp(F.pos.y, BaseY, 10f, dt);
                    }
                    SolveLeg(leg, F.pos, F.yaw, F.pitch);
                }
            }

            // ---- head
            idleTilt = Damp(idleTilt, Stepping ? 0f : Mathf.Sin(time * 0.37f) * 0.12f + HappyWiggle * Mathf.Sin(time * 18f) * 0.25f, 3f, dt);
            HappyWiggle = Mathf.Max(0f, HappyWiggle - dt * 0.8f);
            Head(dt, sp01 * 0.25f);
            ReachState.w = Damp(ReachState.w, ReachState.weight, 5f, dt);
            CrouchExtra = ReachState.w * ReachState.crouch; PitchExtra = ReachState.w * ReachState.bow;
            if (ReachState.w > 0.002f)
            {
                float k = Smooth(Clamp(ReachState.w * 1.3f, 0f, 1f));
                SetQ("head_yaw", Lerp(Q["head_yaw"], 0f, k)); SetQ("head_roll", Lerp(Q["head_roll"], 0f, k));
                ApplyJoint("head_yaw"); ApplyJoint("head_roll");
            }
            if (ReachState.w > 0.002f && ReachState.target.HasValue)
            {
                float bNeck = Q["neck_pitch"], bHead = Q["head_pitch"];
                if (ReachState.sol != null) { SetQ("neck_pitch", ReachState.sol["neck_pitch"]); SetQ("head_pitch", ReachState.sol["head_pitch"]); ApplyJoint("neck_pitch"); ApplyJoint("head_pitch"); }
                ReachCCD();
                ReachState.sol = new Dictionary<string, float> { { "neck_pitch", Q["neck_pitch"] }, { "head_pitch", Q["head_pitch"] } };
                float w = Smooth(Clamp(ReachState.w, 0f, 1f));
                SetQ("neck_pitch", Lerp(bNeck, ReachState.sol["neck_pitch"], w)); SetQ("head_pitch", Lerp(bHead, ReachState.sol["head_pitch"], w));
                ApplyJoint("neck_pitch"); ApplyJoint("head_pitch");
            }
            else ReachState.sol = null;
            UpdateJaw(dt);
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            Pos = pos; Yaw = yaw; BaseY = Ground(pos.x, pos.z); Vel = Vector3.zero; YawRate = 0f;
            FallState = null; Tilt.localPosition = Vector3.zero; Tilt.localRotation = Quaternion.identity;
            Model.localPosition = new Vector3(-trunkBase.x, 0f, -trunkBase.z);
            ResetFeet();
        }
    }
}
