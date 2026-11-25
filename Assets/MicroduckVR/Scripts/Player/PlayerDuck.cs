using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;
using UnityEngine.XR;

namespace MicroduckVR.Player
{
    /// <summary>
    /// The player IS this duck. The HMD drives the duck's gaze; the left stick walks (relative to where
    /// you look) and the procedural legs step; smooth or snap turn on the right stick; right trigger opens the beak,
    /// letting go on a toy grabs it, squeezing again drops it.
    ///
    /// Comfort: the camera is NOT attached to the head bones. It rides a "virtual eye" above the duck's
    /// ground point at a fixed eye height, with damped vertical motion; real head movement adds on top,
    /// scaled down with the world, for natural parallax. Bows, nods, bobbing and falls never move the view.
    /// The XR Origin is scaled down (default 0.15) so the stereo baseline shrinks and the house reads giant.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [RequireComponent(typeof(DuckSetup))]
    public class PlayerDuck : MonoBehaviour
    {
        [Header("Rig")]
        public Transform xrOrigin;
        public Camera cam;
        public ComfortVignette vignette;

        [Header("Feel")]
        [Range(0.08f, 0.3f)] public float worldScale = 0.15f;     // XR Origin scale: smaller = bigger world
        public enum TurnMode { Smooth, Snap }
        public TurnMode turnMode = TurnMode.Snap;        // snap is the comfortable default; smooth is optional
        public float smoothTurnDegPerSec = 110f;
        public float snapDegrees = 30f;
        public float speedScale = 1f;
        public float bodyFollowDeg = 55f;                          // body turns to face where you look past this
        [Range(0f, 1f)] public float vignetteStrength = 0.6f;

        DuckSetup setup;
        DuckBody body;
        CharacterController mover;
        DuckInput input;
        float yaw, yawRate, eyeHeight, eyeY, eyeYVel, vy;
        Vector3 vel, headBaseline;
        bool turnLatched;
        float originYaw, turnVel;
        Grabbable held;
        bool beakWasOpen;
        float chirpT;

        public DuckBody Body => body;
        public Grabbable Held => held;

        void Start()
        {
            setup = GetComponent<DuckSetup>();
            body = setup.Body; mover = setup.Mover;
            input = new DuckInput(); input.Enable();
            yaw = body.Yaw;
            eyeHeight = body.EyePosition.y - body.transform.position.y;
            eyeY = body.transform.position.y + eyeHeight;
            originYaw = yaw * Mathf.Rad2Deg;
            if (xrOrigin != null) xrOrigin.localScale = Vector3.one * worldScale;
            if (cam != null) cam.cullingMask &= ~(1 << DuckSetup.LayerPlayerHead);
            TrySetDeviceOrigin();
        }

        void OnDestroy() { input?.Disable(); }

        static void TrySetDeviceOrigin()
        {
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            foreach (var s in subs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
        }

        float HeadYaw => cam != null ? cam.transform.eulerAngles.y * Mathf.Deg2Rad : yaw;

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // ---- turning: smooth (default) or snap; the view pivots around the eye
            float tx = input.Turn.ReadValue<Vector2>().x;
            if (turnMode == TurnMode.Smooth)
            {
                float wantTurn = Mathf.Abs(tx) > 0.2f ? Mathf.Sign(tx) * Mathf.InverseLerp(0.2f, 1f, Mathf.Abs(tx)) * smoothTurnDegPerSec : 0f;
                turnVel = Mathf.MoveTowards(turnVel, wantTurn, smoothTurnDegPerSec * 4f * dt);   // eased start and stop
                float d = turnVel * dt;
                originYaw += d; yaw += d * Mathf.Deg2Rad;
            }
            else
            {
                if (!turnLatched && Mathf.Abs(tx) > 0.7f)
                {
                    turnLatched = true;
                    float d = Mathf.Sign(tx) * snapDegrees;
                    originYaw += d; yaw += d * Mathf.Deg2Rad;
                    if (vignette != null) vignette.Pulse(0.35f);
                }
                else if (Mathf.Abs(tx) < 0.3f) turnLatched = false;
            }

            // ---- walk, relative to where the player looks
            Vector2 stick = input.Move.ReadValue<Vector2>();
            if (stick.magnitude < 0.15f) stick = Vector2.zero;
            float hy = HeadYaw;
            Vector3 fwd = GltfSpace.Forward(hy), right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 want = (fwd * stick.y + right * stick.x) * (Gait.MaxSpeed * speedScale);
            if (want.magnitude > Gait.MaxSpeed * speedScale) want = want.normalized * Gait.MaxSpeed * speedScale;
            vel = Vector3.MoveTowards(vel, want, 0.9f * dt);           // gentle acceleration (comfort)

            // ---- body facing: walk direction, or follow the head when it looks far away
            float prevYaw = yaw;
            float targetYaw = yaw;
            if (vel.magnitude > 0.02f) targetYaw = Mathf.Atan2(vel.x, vel.z);
            else if (Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, hy * Mathf.Rad2Deg)) > bodyFollowDeg) targetYaw = hy;
            float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            yaw += Mathf.Clamp(dy, -Gait.MaxYawRate * 1.4f * dt, Gait.MaxYawRate * 1.4f * dt);
            yawRate = dt > 0f ? Mathf.DeltaAngle(prevYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt : 0f;

            // ---- move the capsule
            vy = mover.isGrounded ? -0.05f : vy - 9.81f * dt;
            mover.Move(new Vector3(vel.x, vy, vel.z) * dt);
            Vector3 p = mover.transform.position;
            Vector3 realVel = new Vector3(vel.x, 0f, vel.z);

            // ---- beak and grabbing
            float trig = input.Beak.ReadValue<float>();
            bool open = trig > 0.35f;
            if (held == null)
            {
                body.JawTarget = trig * 0.6f;
                var near = Grabbable.Nearest(body.BeakGap(), 0.16f);
                bool reach = open && near != null && near.transform.position.y < p.y + 0.08f;
                body.ReachState.target = reach ? near.transform.position : (Vector3?)null;
                body.ReachState.weight = reach ? 1f : 0f;
                if (beakWasOpen && !open)
                {
                    var g = Grabbable.Nearest(body.BeakGap(), 0.035f);
                    if (g != null && g.TryHold(this)) { held = g; body.JawStop = 0.12f; }
                }
            }
            else
            {
                body.ReachState.weight = 0f; body.ReachState.target = null;
                body.JawTarget = 0f;
                if (open && !beakWasOpen) { held.Release(realVel); held = null; body.JawStop = 0f; body.JawTarget = 0.5f; }
            }
            beakWasOpen = open;

            if (input.Chirp.WasPressedThisFrame()) chirpT = 0.25f;
            if (chirpT > 0f) { chirpT -= dt; if (held == null) body.JawTarget = Mathf.Max(body.JawTarget, 0.55f); }
            if (input.Emote.WasPressedThisFrame()) body.PlayEmote("dance");

            // ---- gaze from the head
            if (cam != null) body.LookAt.gazeDir = cam.transform.forward;

            body.Step(dt, new Vector3(p.x, p.y, p.z), yaw, realVel, yawRate);

            if (held != null)
            {
                held.transform.position = body.BeakGap();
                held.transform.rotation = Quaternion.LookRotation(body.HeadDir(), Vector3.up);
            }

            if (vignette != null)
                vignette.Target = vignetteStrength * Mathf.Clamp01(realVel.magnitude / Gait.MaxSpeed * 0.8f + Mathf.Abs(yawRate) * 0.25f + Mathf.Abs(turnVel) / smoothTurnDegPerSec * 0.6f);
        }

        void LateUpdate()
        {
            if (xrOrigin == null || cam == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            xrOrigin.localScale = Vector3.one * worldScale;
            xrOrigin.rotation = Quaternion.Euler(0f, originYaw, 0f);

            // eye height follows the ground point with a damped spring: no bob, no bow, no fall tumble
            float target = mover.transform.position.y + eyeHeight;
            float k = 60f, c = 2f * Mathf.Sqrt(k);
            eyeYVel += (k * (target - eyeY) - c * eyeYVel) * dt;
            eyeY += eyeYVel * dt;
            if (Mathf.Abs(target - eyeY) > 0.3f) { eyeY = target; eyeYVel = 0f; }

            Vector3 eye = new Vector3(mover.transform.position.x, eyeY, mover.transform.position.z);
            // Only a slowly drifting baseline of the head position goes into the origin. The tracked pose
            // itself (applied by the TrackedPoseDriver right before rendering) stays untouched on top of it,
            // so head motion is never a frame late: no judder when turning the head.
            Vector3 headLocal = cam.transform.localPosition;
            headBaseline = Vector3.Lerp(headBaseline, headLocal, 1f - Mathf.Exp(-dt / 1.5f));
            xrOrigin.position = eye - xrOrigin.rotation * (headBaseline * worldScale);
        }
    }
}
