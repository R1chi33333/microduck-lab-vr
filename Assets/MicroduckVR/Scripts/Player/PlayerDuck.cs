using System.Collections.Generic;
using MicroduckVR.Core;
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
    public class PlayerDuck : MonoBehaviour, ILocalPlayer
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

        [Header("Third person")]
        public bool thirdPerson;                                   // left stick click / V toggles
        public float thirdBack = 0.42f, thirdUp = 0.2f;            // camera offset behind and above the duck (duck-scale m)
        public float thirdLag = 0.25f;                             // follow smoothing (s); the view never rotates by itself
        Vector3 thirdPos, thirdVel; bool thirdSnap = true; Vector2 headAim;

        // what the player has done (the tutorial watches these)
        public int SnapCount, ChirpCount, GrabCount, DropCount, ViewToggles; public float Walked, Turned;
        public float HeadAimAmount => headAim.magnitude;
        /// <summary>Turn the view (not the duck) to face a heading, in one step.</summary>
        public void FaceView(float yawRad) { if (cam != null) originYaw += Mathf.DeltaAngle(cam.transform.eulerAngles.y, yawRad * Mathf.Rad2Deg); }
        [Range(0f, 1f)] public float vignetteStrength = 0.6f;

        DuckSetup setup;
        DuckBody body;
        CharacterController mover;
        DuckInput input;
        PlayerSkates skates;
        public PlayerSkates Skates => skates;
        float yaw, yawRate, eyeHeight, eyeY, eyeYVel, vy;
        Vector3 vel, headBaseline;
        bool turnLatched;
        float originYaw, turnVel;
        Grabbable held; float lastYawDeg, regrabWait;
        bool beakWasOpen;
        float chirpT, bedT;

        public static PlayerDuck Instance { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }
        /// <summary>Lets the layers below the player (comic effects, the lawn) find this duck, in play mode and in edit mode.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void RegisterLocalPlayer() { LocalPlayer.Resolve = () => Instance != null ? Instance : null; }
        bool ILocalPlayer.HasBody => Body != null;
        Vector3 ILocalPlayer.BodyPos => Body.Pos;
        Camera ILocalPlayer.Cam => cam;
        bool ILocalPlayer.ThirdPerson => thirdPerson;
        /// <summary>Debug only: when set, the duck walks this world direction at full speed (used by editor tests).</summary>
        public static Vector3? DebugWalk;
        public static float? DebugBeak;                    // editor tests and filming: the beak trigger
        public static bool DebugChirp;
        public float Speed => new Vector2(vel.x, vel.z).magnitude;
        public DuckBody Body => body;
        public Grabbable Held => held;
        /// <summary>Let go of whatever is in the beak, right now.</summary>
        public void DropHeld() { if (held != null) { held.Release(Vector3.zero); held = null; DropCount++; body.JawStop = 0f; } }

        void Start()
        {
            Instance = this;
            setup = GetComponent<DuckSetup>();
            body = setup.Body; mover = setup.Mover;
            input = new DuckInput(); input.Enable();
            skates = GetComponent<PlayerSkates>(); if (skates == null) skates = gameObject.AddComponent<PlayerSkates>();
            skates.Init(body);
            var lp = GetComponent<MicroduckVR.Games.LaserPointer>(); if (lp == null) lp = gameObject.AddComponent<MicroduckVR.Games.LaserPointer>();
            lp.cam = cam; var bag = GetComponent<BagMenu>(); if (bag != null) lp.rightHand = bag.rightHand;
            if (GetComponent<TutorialCoach>() == null) gameObject.AddComponent<TutorialCoach>().cam = cam;
            // the bedroom mirror and the garage (they also put your saved look on)
            TMPro.TMP_FontAsset fa = null;
            if (bag != null && bag.font != null) fa = TMPro.TMP_FontAsset.CreateFontAsset(bag.font);
            MicroduckVR.Custom.MirrorStation.Ensure(this, fa);
            MicroduckVR.Custom.Garage.Ensure(this, fa);
            yaw = body.Yaw;
            eyeHeight = body.EyePosition.y - body.transform.position.y;
            eyeY = body.transform.position.y + eyeHeight;
            originYaw = yaw * Mathf.Rad2Deg;
            if (xrOrigin != null) xrOrigin.localScale = Vector3.one * worldScale;
            if (cam != null) cam.cullingMask &= ~(1 << Layers.PlayerHead);
            TrySetDeviceOrigin();
        }

        /// <summary>Move the duck (capsule and body) to a point; y is a hint, the duck lands on the surface below it.</summary>
        public void Teleport(Vector3 pos, float yawRad)
        {
            mover.enabled = false; mover.transform.position = pos; mover.enabled = true;
            vel = Vector3.zero; vy = 0f; yaw = yawRad;
            body.Teleport(new Vector3(pos.x, WorldQueries.GroundNear(pos.x, pos.z, pos.y), pos.z), yawRad);
            eyeY = body.BaseY + eyeHeight; eyeYVel = 0f; thirdSnap = true;
        }

        /// <summary>Become another duck: swap places, headings and looks with the duck most in front of you (or the nearest).</summary>
        public void SwapWithNearest()
        {
            MicroduckVR.Flock.DuckBrain best = null; float bs = -1e9f;
            var f = cam != null ? cam.transform.forward : GltfSpace.Forward(yaw); f.y = 0f; f.Normalize();
            foreach (var b in MicroduckVR.Flock.DuckBrain.All)
            {
                var to = b.Pos - body.Pos; to.y = 0f; float d = to.magnitude; if (d < 1e-3f) continue;
                float score = Vector3.Dot(to / d, f) * 2f - d;          // in front and close
                if (score > bs) { bs = score; best = b; }
            }
            if (best == null) return;
            var otherSetup = best.GetComponent<DuckSetup>();
            Vector3 myPos = body.Pos, hisPos = best.Pos; float myYaw = yaw, hisYaw = best.Body.Yaw;
            bool skating = skates.On; if (skating) skates.Toggle(yaw, Vector3.zero);        // the skates' own colours stay out of the reskin
            (setup.skin, otherSetup.skin) = (otherSetup.skin, setup.skin);
            MicroduckVR.Comic.ComicLook.ApplyToDuck(body.Model.GetChild(0), setup.skin);
            MicroduckVR.Comic.ComicLook.ApplyToDuck(best.Body.Model.GetChild(0), otherSetup.skin);
            best.Teleport(myPos, myYaw);
            Teleport(hisPos + Vector3.up * 0.02f, hisYaw);
            originYaw += Mathf.DeltaAngle(myYaw * Mathf.Rad2Deg, hisYaw * Mathf.Rad2Deg);   // keep looking the way the new duck faces
            thirdSnap = true;
            var au = GetComponent<MicroduckVR.Audio.DuckAudio>(); if (au != null) au.Chirp();
            body.PlayEmote(EmoteKind.Nod);
            if (skating) skates.Toggle(yaw, Vector3.zero);
        }

        public void SetThirdPerson(bool on)
        {
            thirdPerson = on; thirdSnap = true; ViewToggles++; headAim = Vector2.zero;
            if (cam != null) { if (on) cam.cullingMask |= 1 << Layers.PlayerHead; else cam.cullingMask &= ~(1 << Layers.PlayerHead); }
            if (vignette != null) vignette.Pulse(0.6f);
        }

        void OnDestroy() { input?.Disable(); if (Instance == this) Instance = null; }

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

            // ---- right stick: in third person it aims the duck's head; otherwise it turns (smooth or snap)
            Vector2 rs = input.Turn.ReadValue<Vector2>();
            float tx = rs.x;
            if (thirdPerson)
            {
                headAim = Vector2.MoveTowards(headAim, rs.magnitude > 0.15f ? rs : Vector2.zero, 4f * dt);
                if (input.Recenter.WasPressedThisFrame() && cam != null)
                {
                    // swing the view round behind the duck (a cut, not a glide)
                    originYaw += Mathf.DeltaAngle(cam.transform.eulerAngles.y, yaw * Mathf.Rad2Deg);
                    thirdSnap = true; if (vignette != null) vignette.Pulse(0.4f);
                }
            }
            else if (turnMode == TurnMode.Smooth)
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
                    originYaw += d; yaw += d * Mathf.Deg2Rad; SnapCount++;
                    if (vignette != null) vignette.Pulse(0.35f);
                    thirdSnap = true;                                  // third person: jump to the new side, no orbiting glide
                }
                else if (Mathf.Abs(tx) < 0.3f) turnLatched = false;
            }

            // ---- walk, relative to where the player looks
            Vector2 stick = input.Move.ReadValue<Vector2>();
            if (stick.magnitude < 0.15f) stick = Vector2.zero;
            if (MicroduckVR.Games.RaceGame.Frozen) stick = Vector2.zero;                 // 3, 2, 1...
            float hy = HeadYaw;
            Vector3 fwd = GltfSpace.Forward(hy), right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 want = (fwd * stick.y + right * stick.x) * (Gait.MaxSpeed * speedScale);
            if (want.magnitude > Gait.MaxSpeed * speedScale) want = want.normalized * Gait.MaxSpeed * speedScale;
            if (DebugWalk.HasValue) want = DebugWalk.Value.normalized * Gait.MaxSpeed * speedScale;
            if (MicroduckVR.Games.RaceGame.Active) want *= 1.9f;                                 // race pace (skates are faster still)
            var surf = SurfaceTag.At(mover.transform.position);
            SurfaceType surfType = surf != null ? surf.Kind : SurfaceType.Wood;
            float prevYaw = yaw;
            Vector3 before = mover.transform.position;
            if (skates.On)
            {
                // on skates: the stick says where to go (relative to the head); the skates carve towards it
                Vector3 stickW = fwd * stick.y + right * stick.x;
                if (stickW.magnitude > 1f) stickW.Normalize();
                if (DebugWalk.HasValue) stickW = DebugWalk.Value.normalized;
                vel = skates.Step(dt, stickW, surf != null ? surf.Friction : 0.7f, mover.transform.position, ref yaw);
            }
            else
            {
                if (surfType == SurfaceType.Water) want *= 0.55f;
                float grip = surf != null ? Mathf.Lerp(0.18f, 1f, Mathf.InverseLerp(0.15f, 0.7f, surf.Friction)) : 1f;   // tiles: you slide
                vel = Vector3.MoveTowards(vel, want, 0.9f * grip * dt);   // gentle acceleration (comfort)

                // ---- body facing: walk direction, or follow the head when it looks far away
                float targetYaw = yaw;
                if (vel.magnitude > 0.02f) targetYaw = Mathf.Atan2(vel.x, vel.z);
                else if (thirdPerson) { if (Mathf.Abs(headAim.x) > 0.85f) targetYaw = yaw + Mathf.Sign(headAim.x) * 0.6f; }   // full stick: the duck turns round
                else if (Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, hy * Mathf.Rad2Deg)) > bodyFollowDeg) targetYaw = hy;
                float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, targetYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                yaw += Mathf.Clamp(dy, -Gait.MaxYawRate * 1.4f * dt, Gait.MaxYawRate * 1.4f * dt);
            }
            yawRate = dt > 0f ? Mathf.DeltaAngle(prevYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt : 0f;

            // ---- move the capsule
            vy = mover.isGrounded ? -0.05f : vy - 9.81f * dt;
            Vector3 step = new Vector3(vel.x, vy, vel.z) * dt;
            mover.Move(step);
            if (skates.On) skates.AfterMove(step, mover.transform.position - before);
            Vector3 p = mover.transform.position;
            Vector3 realVel = new Vector3(vel.x, 0f, vel.z);
            Walked += realVel.magnitude * dt;
            Turned += Mathf.Abs(Mathf.DeltaAngle(lastYawDeg, yaw * Mathf.Rad2Deg)); lastYawDeg = yaw * Mathf.Rad2Deg;

            // ---- beak and grabbing
            float trig = BagMenu.Busy || MicroduckVR.Custom.WorldPanel.AnyAimed ? 0f : (DebugBeak ?? input.Beak.ReadValue<float>());   // the trigger works the bag while it is open
            bool open = trig > 0.35f;
            if (held == null)
            {
                body.JawTarget = trig * 0.6f;
                var near = Grabbable.Nearest(body.BeakGap(), 0.26f);                  // the head is well above the floor: look far enough down
                bool reach = open && near != null && near.Centre.y < p.y + 0.1f;
                body.ReachState.target = reach ? near.Centre : (Vector3?)null;
                body.ReachState.weight = reach ? 1f : 0f;
                regrabWait -= dt;
                if (beakWasOpen && !open && regrabWait <= 0f)        // not straight after a drop: a quick squeeze must not bite the same thing again
                {
                    var g = Grabbable.Nearest(body.BeakGap(), 0.03f);          // measured to the thing's surface, so big and off-centre things work too
                    if (g != null && g.TryHold(this)) { held = g; GrabCount++; body.JawStop = 0.12f; }
                }
            }
            else
            {
                body.ReachState.weight = 0f; body.ReachState.target = null;
                body.JawTarget = 0f;
                if (open && !beakWasOpen) { held.Release(realVel); held = null; DropCount++; regrabWait = 0.6f; body.JawStop = 0f; body.JawTarget = 0.5f; }
            }
            beakWasOpen = open;

            if (!BagMenu.Busy && (input.Chirp.WasPressedThisFrame() || DebugChirp)) { DebugChirp = false; chirpT = 0.25f; ChirpCount++; var au = GetComponent<MicroduckVR.Audio.DuckAudio>(); if (au != null) au.Chirp(); }
            if (chirpT > 0f) { chirpT -= dt; if (held == null) body.JawTarget = Mathf.Max(body.JawTarget, 0.55f); }
            if (input.Emote.WasPressedThisFrame() && !MicroduckVR.Custom.MirrorStation.InZone) body.PlayEmote(EmoteKind.Dance);      // at the mirror X opens the board
            if (!BagMenu.Busy && input.Skates.WasPressedThisFrame()) skates.Toggle(yaw, vel);
            if (input.View.WasPressedThisFrame()) SetThirdPerson(!thirdPerson);

            // ---- gaze from the head
            if (thirdPerson) body.LookAt.gazeDir = Quaternion.Euler(-headAim.y * 35f, yaw * Mathf.Rad2Deg + headAim.x * 75f, 0f) * Vector3.forward;
            else if (cam != null) body.LookAt.gazeDir = cam.transform.forward;

            // bed: boing boing (the body hops; the camera only follows a damped fraction of it)
            bedT = surfType == SurfaceType.Bed ? bedT + dt : 0f;
            body.Hop = surfType == SurfaceType.Bed ? Mathf.Abs(Mathf.Sin(bedT * 6.5f)) * 0.028f : Mathf.MoveTowards(body.Hop, 0f, 0.2f * dt);
            body.Step(dt, new Vector3(p.x, p.y, p.z), yaw, realVel, yawRate);

            if (held != null)
            {
                // held by its middle, just in front of the beak (not by its pivot, which may be at its foot)
                var hr = Quaternion.LookRotation(body.HeadDir(), Vector3.up);
                held.transform.rotation = hr;
                held.transform.position = body.BeakGap() + body.HeadDir() * Mathf.Min(held.Radius * 0.6f, 0.02f) - hr * Vector3.Scale(held.CentreLocal, held.transform.lossyScale);
            }

            // comic speed lines when skating fast (web: from 0.22 m/s, 2.5 per m/s, at most 0.8; full while boosting),
            // with the web's threshold scaled to the VR skates' lower top speed
            if (vignette != null)
                vignette.SpeedLines = skates.On ? Mathf.Max(Mathf.Clamp((realVel.magnitude - 0.18f * skates.comfortScale) * 2.5f, 0f, 0.8f), skates.Boosting ? 1f : 0f) : 0f;
            if (vignette != null)
                vignette.Target =vignetteStrength * Mathf.Clamp01(realVel.magnitude / (skates.On ? skates.TopSpeed : Gait.MaxSpeed) * (skates.On ? 0.9f : 0.8f) + (skates.Drifting ? 0.25f : 0f) + (skates.On ? 0f : Mathf.Abs(yawRate) * 0.25f) + Mathf.Abs(turnVel) / smoothTurnDegPerSec * 0.6f);
        }

        void LateUpdate()
        {
            if (xrOrigin == null || cam == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            xrOrigin.localScale = Vector3.one * worldScale;
            xrOrigin.rotation = Quaternion.Euler(0f, originYaw, 0f);

            // eye height follows the ground point with a damped spring: no bob, no bow, no fall tumble
            float target = mover.transform.position.y + eyeHeight + body.Hop * 0.3f + body.SkateW * SkatePair.Height;
            float k = 60f, c = 2f * Mathf.Sqrt(k);
            eyeYVel += (k * (target - eyeY) - c * eyeYVel) * dt;
            eyeY += eyeYVel * dt;
            if (Mathf.Abs(target - eyeY) > 0.3f) { eyeY = target; eyeYVel = 0f; }

            Vector3 eye = new Vector3(mover.transform.position.x, eyeY, mover.transform.position.z);
            if (thirdPerson)
            {
                // behind and above the duck, in the view's own heading (it only turns with snap turns), smoothly following
                Vector3 back = Quaternion.Euler(0f, originYaw, 0f) * new Vector3(0f, thirdUp, -thirdBack);
                Vector3 want = eye + back;
                // never inside a wall: pull in towards the duck's head if something is in the way
                Vector3 head = eye + Vector3.up * 0.02f, d = want - head;
                if (Physics.SphereCast(head, 0.03f, d.normalized, out var hit, d.magnitude, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore))
                    want = head + d.normalized * Mathf.Max(0.06f, hit.distance - 0.01f);
                if (thirdSnap) { thirdPos = want; thirdVel = Vector3.zero; thirdSnap = false; }
                else thirdPos = Vector3.SmoothDamp(thirdPos, want, ref thirdVel, thirdLag, Mathf.Infinity, dt);
                eye = thirdPos;
            }
            // Only a slowly drifting baseline of the head position goes into the origin. The tracked pose
            // itself (applied by the TrackedPoseDriver right before rendering) stays untouched on top of it,
            // so head motion is never a frame late: no judder when turning the head.
            Vector3 headLocal = cam.transform.localPosition;
            headBaseline = Vector3.Lerp(headBaseline, headLocal, 1f - Mathf.Exp(-dt / 1.5f));
            xrOrigin.position = eye - xrOrigin.rotation * (headBaseline * worldScale);
        }
    }
}
