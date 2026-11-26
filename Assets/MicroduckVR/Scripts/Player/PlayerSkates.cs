using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Player
{
    /// <summary>
    /// Roller skates for the player's duck (port of the skate model in agent.js): momentum, push strokes, braking,
    /// grip that depends on the floor, a drift (hold left grip while turning: lower grip, faster turn, the tail slides
    /// out; let go after a moment for a slingshot) and a boost (left trigger: a short burst, then a cooldown).
    /// Comic effects (the web's skate FX, drawn by ComicFX): smoke puffs, zig-zag bolts, speed streaks, sparks, glints,
    /// a burst when a fast run comes to a stop, and skid marks on the floor. Through your own eyes a share of the smoke
    /// and bolts curls forwards into the bottom of the view, since everything at the wheels is out of sight below you.
    ///
    /// VR comfort: you steer by pointing the stick where you want to go relative to where you look (the skates turn
    /// towards it); the view itself never turns, rolls or leans with the duck. Speeds are capped (comfortScale) and the
    /// vignette tightens with speed.
    /// </summary>
    public class PlayerSkates : MonoBehaviour
    {
        // web SKATE constants (duck-scale metres and seconds), read from the tuning; its defaults are the web numbers
        /// <summary>Optional feel settings. Left empty, Init looks for Resources/SkateTuning, then uses SkateTuning.Defaults.</summary>
        public SkateTuning tuning;
        SkateTuning resolved;                                       // picked once: about 20 reads a frame skip the Unity null check
        SkateTuning Tuning { get { if (resolved is null) resolved = tuning != null ? tuning : SkateTuning.Defaults; return resolved; } }
        float Accel => Tuning.accel; float MaxSpeed => Tuning.maxSpeed; float Friction => Tuning.friction; float Brake => Tuning.brake;
        float Grip => Tuning.grip; float Turn => Tuning.turn; float StrokeHz => Tuning.strokeHz;
        float BoostMult => Tuning.boostMult; float BoostTime => Tuning.boostTime; float BoostCooldown => Tuning.boostCooldown;
        float BoostKick => Tuning.boostKick; float BoostAccel => Tuning.boostAccel;
        float DriftGrip => Tuning.driftGrip; float DriftTurnMult => Tuning.driftTurnMult; float DriftFlick => Tuning.driftFlick;
        float DriftMinSpeed => Tuning.driftMinSpeed; float SlingAfter => Tuning.slingAfter; float SlingKick => Tuning.slingKick;
        float SlingTime => Tuning.slingTime;
        float CrashSpeed => Tuning.crashSpeed;

        [Range(0.5f, 1f)] public float comfortScale = 0.85f;      // caps top speed and boost in VR

        /// <summary>The skates are on.</summary>
        public bool On { get; private set; }
        /// <summary>Editor tests: the drift and boost inputs.</summary>
        public static float? DebugDrift, DebugBoost;
        float DriftIn => DebugDrift ?? driftA.ReadValue<float>();
        float BoostIn => DebugBoost ?? boostA.ReadValue<float>();
        /// <summary>Speed on the floor (m/s).</summary>
        public float Speed => sv.magnitude;
        /// <summary>Top speed after the comfort cap.</summary>
        public float TopSpeed => MaxSpeed * comfortScale;
        /// <summary>The drift button is held while turning.</summary>
        public bool Drifting => drifting;
        /// <summary>A boost or slingshot is running.</summary>
        public bool Boosting => boostT > 0f;
        /// <summary>Skating velocity.</summary>
        public Vector3 Velocity => sv;

        DuckBody body;
        SkatePair pair;
        InputAction driftA, boostA;
        Vector3 sv; float w, boostT, boostCd, driftT, slip;
        bool drifting;

        // effects (toon.js skateFX state): smoothed acceleration, turn rate, the last run's peak speed, emission accumulators
        PlayerDuck pd;
        bool fxLive, stopped = true;
        Vector3 fxPos; float fxSp, fxYaw, fxAcc, peak, peakT = 9f, skrrtCd;
        float smokeAcc, boltAcc, streakAcc, fpSmokeAcc, fpBoltAcc;
        readonly Vector3?[] lastSkid = new Vector3?[2];
        AudioSource rollSrc, sfxSrc; AudioClip boostClip, pushClip, clackClip;

        /// <summary>Sets the skates up for a body: the pair, the drift and boost inputs and the sounds.</summary>
        public void Init(DuckBody b)
        {
            body = b;
            if (tuning == null) tuning = Resources.Load<SkateTuning>(SkateTuning.ResourceName);
            pair = new SkatePair();
            driftA = new InputAction("Drift", InputActionType.Value, expectedControlType: "Axis");
            driftA.AddBinding(InputPaths.LeftGrip); driftA.AddBinding(InputPaths.KeyLeftShift);
            boostA = new InputAction("Boost", InputActionType.Value, expectedControlType: "Axis");
            boostA.AddBinding(InputPaths.LeftTrigger); boostA.AddBinding(InputPaths.KeyLeftCtrl);
            driftA.Enable(); boostA.Enable();
            pd = GetComponent<PlayerDuck>();
            ComicFX.Get();
            rollSrc = gameObject.AddComponent<AudioSource>(); rollSrc.clip = Noise(1f, 0.25f, true); rollSrc.loop = true; rollSrc.volume = 0f; rollSrc.spatialBlend = 0f; rollSrc.Play();
            sfxSrc = gameObject.AddComponent<AudioSource>(); sfxSrc.spatialBlend = 0f;
            boostClip = Sweep(300f, 1400f, 0.35f); pushClip = Noise(0.12f, 0.6f, false); clackClip = Sweep(900f, 500f, 0.04f);
        }

        void OnDestroy() { driftA?.Disable(); boostA?.Disable(); }

        // ------------------------------------------------------------------ equip
        /// <summary>Puts the skates on or takes them off (not while fallen); going on keeps the walking velocity.</summary>
        public void Toggle(float yaw, Vector3 walkVel)
        {
            if (body.Fallen) return;
            On = !On;
            if (On) { pair.Attach(body); body.Skating = true; body.SkateLift = SkatePair.Height; sv = walkVel; w = 0f; }
            else { pair.Detach(); body.Skating = false; body.SkateLean = 0f; body.Wobble = 0f; drifting = false; boostT = 0f; sv = Vector3.zero; }
            fxLive = false; lastSkid[0] = lastSkid[1] = null;
            sfxSrc.PlayOneShot(clackClip, 0.8f);
        }

        // ------------------------------------------------------------------ physics
        static Vector3 Fwd(float yaw) => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));

        /// <summary>One step. stick: wanted direction in the world (length 0..1, already relative to the head).
        /// friction: the floor's (wood 0.7, rug 1, tiles 0.15). Updates yaw; returns the velocity to move by.</summary>
        public Vector3 Step(float dt, Vector3 stick, float friction, Vector3 pos, ref float yaw)
        {
            float fric = Mathf.Clamp(friction / 0.7f, 0.18f, 1.45f);
            boostCd = Mathf.Max(0f, boostCd - dt); boostT = Mathf.Max(0f, boostT - dt);
            bool boosting = boostT > 0f;
            if (body.Fallen) stick = Vector3.zero;

            // the command, from where the stick points relative to the skates' heading
            float m = Mathf.Clamp01(stick.magnitude), push = 0f, wCmd = 0f; bool braking = false;
            if (m > 0.15f)
            {
                float dy = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, Mathf.Atan2(stick.x, stick.z) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float c = Mathf.Cos(dy);
                if (c < -0.6f && sv.magnitude > 0.03f) braking = true;          // stick pulled against the heading: brake
                else { push = m * Mathf.Max(0f, c); wCmd = Mathf.Clamp(dy * 2.2f, -1f, 1f) * m; }
            }
            if (!body.Fallen && BoostIn > 0.6f && boostCd <= 0f) Boost(false, yaw);

            float sp0 = sv.magnitude;
            bool wantDrift = DriftIn > 0.5f && Mathf.Abs(wCmd) > 0.1f && sp0 > DriftMinSpeed * Mathf.Min(1f, fric);
            if (wantDrift && !drifting) { drifting = true; driftT = 0f; yaw += Mathf.Sign(wCmd) * DriftFlick; }
            if (drifting && !wantDrift) { drifting = false; if (driftT > SlingAfter && !body.Fallen) Boost(true, yaw); }
            if (drifting) driftT += dt;

            var f = Fwd(yaw);
            if (push > 0.02f)
            {
                body.Stroke = (body.Stroke + StrokeHz * dt) % 1f;
                if (body.Stroke < StrokeHz * dt) { body.StrokeSide *= -1f; sfxSrc.PlayOneShot(pushClip, 0.25f + 0.35f * push); KickPuff(); }
                float phase = Mathf.Max(0f, Mathf.Sin(body.Stroke * Mathf.PI * 2f));
                sv += f * Accel * push * (0.4f + phase) * dt;
            }
            else body.Stroke = Mathf.Max(0f, body.Stroke - dt);
            if (boosting) sv += f * BoostAccel * dt;
            if (braking) sv *= Mathf.Exp(-Brake * 2.2f * dt);
            sv *= Mathf.Exp(-Friction * fric * dt);
            float top = TopSpeed * (boosting ? Mathf.Lerp(1f, BoostMult, comfortScale) : 1f);
            float sp = sv.magnitude;
            if (sp > top) { float k = boosting ? top / sp : Mathf.Max(top / sp, Mathf.Exp(-2.5f * dt)); sv *= k; sp = sv.magnitude; }

            // steer; the velocity only slowly follows the heading -> drift
            float turn = Turn * (drifting ? DriftTurnMult : 1f);
            w = Mathf.MoveTowards(w, wCmd * turn, (drifting ? 12f : 8f) * dt);
            yaw += w * dt;
            var f2 = Fwd(yaw);
            float along = Vector3.Dot(sv, f2);
            var target = f2 * (along >= 0f ? sp : -sp);
            float grip = (drifting ? DriftGrip : Grip) * fric;
            sv = Vector3.Lerp(sv, target, 1f - Mathf.Exp(-grip * dt));

            // ramps pull it downhill
            const float e = 0.03f;
            float gx = (WorldQueries.GroundNear(pos.x + e, pos.z, pos.y) - WorldQueries.GroundNear(pos.x - e, pos.z, pos.y)) / (2f * e);
            float gz = (WorldQueries.GroundNear(pos.x, pos.z + e, pos.y) - WorldQueries.GroundNear(pos.x, pos.z - e, pos.y)) / (2f * e);
            float slope = Mathf.Abs(gx) < 0.6f && Mathf.Abs(gz) < 0.6f ? Mathf.Sqrt(gx * gx + gz * gz) : 0f;
            if (slope > 0f) { sv.x -= gx * 2.2f * dt; sv.z -= gz * 2.2f * dt; }

            // lean into turns, counter-lean while drifting (the body only; never the view)
            slip = Mathf.Abs(sv.x * f2.z - sv.z * f2.x);
            float spk = Mathf.Clamp(sp / 0.35f, 0f, 1.4f);
            body.SkateLean = drifting ? Mathf.Clamp(w * 0.16f * spk, -0.42f, 0.42f) : Mathf.Clamp(-w * 0.11f * spk, -0.3f, 0.3f);
            float wob = Mathf.Clamp01((sp - MaxSpeed * 1.05f) / 0.2f) * 0.35f + slope * 1.2f;
            body.Wobble = Mathf.Lerp(body.Wobble, Mathf.Clamp(wob, 0f, 0.6f), 1f - Mathf.Exp(-3f * dt));

            pair.Spin(Vector3.Dot(sv, f2) * dt);
            Effects(dt, yaw);
            return sv;
        }

        void Boost(bool sling, float yaw)
        {
            if (!sling) { boostCd = BoostTime + BoostCooldown; boostT = BoostTime; }
            else boostT = Mathf.Max(boostT, SlingTime);
            sv += Fwd(yaw) * (sling ? SlingKick : BoostKick) * comfortScale;
            sfxSrc.PlayOneShot(boostClip, sling ? 0.5f : 0.8f);
            BoostBurst(yaw, sling);
        }

        /// <summary>After the capsule moved: a wall stops the skates; a hard hit (only when boosting) knocks the duck over.</summary>
        public void AfterMove(Vector3 intended, Vector3 got)
        {
            float want = new Vector2(intended.x, intended.z).magnitude; if (want < 1e-5f) return;
            float blocked = 1f - new Vector2(got.x, got.z).magnitude / want;
            if (blocked < 0.5f) return;
            if (sv.magnitude > CrashSpeed * comfortScale && !body.Fallen)
            {
                // bounce off and sit down backwards: tipping forwards put the duck's head through the wall it had just hit
                body.StartTip(-new Vector3(intended.x, 0f, intended.z).normalized);
                sfxSrc.PlayOneShot(clackClip, 1f); CrashBurst(new Vector3(intended.x, 0f, intended.z).normalized);
                sv = Vector3.zero;
            }
            else sv *= 0.25f;
            drifting = false; boostT = 0f;
        }

        // ------------------------------------------------------------------ comic effects (port of toon.js skateFX)
        // Rates are per second with accumulators, so they do not depend on the frame rate. The speed thresholds are the
        // web's scaled by comfortScale: the VR skates top out lower, and the effects should start at the same share of it.
        const float FpScale = 1.15f;        // effects seen through your own eyes are drawn a little bigger

        void Effects(float dt, float yaw)
        {
            float sp = sv.magnitude;
            rollSrc.volume = Mathf.Lerp(rollSrc.volume, On ? Mathf.Clamp01(sp / 0.42f) * 0.32f : 0f, 1f - Mathf.Exp(-8f * dt));
            rollSrc.pitch = 0.6f + sp * 1.4f + (drifting ? 0.25f : 0f);
            if (dt <= 0f) return;
            if (body.Fallen) { fxLive = false; lastSkid[0] = lastSkid[1] = null; return; }

            // teleports and respawns: start afresh instead of reading a huge acceleration
            var here = transform.position;
            if (!fxLive || new Vector2(here.x - fxPos.x, here.z - fxPos.z).sqrMagnitude > 0.04f)
            { fxSp = sp; fxYaw = yaw; fxAcc = 0f; lastSkid[0] = lastSkid[1] = null; fxLive = true; }
            fxPos = here;
            float acc = (sp - fxSp) / dt; fxAcc += (acc - fxAcc) * Mathf.Min(1f, dt * 12f); fxSp = sp;
            float yr = Mathf.DeltaAngle(fxYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt; fxYaw = yaw;
            if (sp > peak || peakT > 2.2f) { peak = sp; peakT = 0f; }
            peakT += dt; skrrtCd -= dt;

            float q = comfortScale;
            var f = Fwd(yaw); var back = -f; var right = new Vector3(f.z, 0f, -f.x);
            Vector3 w0 = pair.Left.position, w1 = pair.Right.position;
            float y = Mathf.Min(w0.y, w1.y); w0.y = y; w1.y = y;
            // the web reads a drift from the slide and the turn rate; the drift button counts too
            bool drift = (sp > 0.12f * q && (slip > 0.045f || Mathf.Abs(yr) > 1.5f)) || (drifting && sp > DriftMinSpeed);
            bool braking = sp > 0.06f && fxAcc < -0.22f;
            bool pushing = fxAcc > 0.1f && sp > 0.04f;
            bool burst = (fxAcc > 0.2f && sp > 0.2f * q) || (Mathf.Abs(yr) > 1.8f && sp > 0.15f * q) || drift;

            // skid marks
            bool skidOn = drift || (braking && sp > 0.1f);
            Skid(0, w0, skidOn); Skid(1, w1, skidOn);

            // smoke: push-offs, acceleration, hard braking, drifting
            smokeAcc += (drift ? 26f : braking ? 20f : pushing ? 12f : sp > 0.3f * q ? 7f : 0f) * dt;
            while (smokeAcc >= 1f) { smokeAcc -= 1f; ComicFX.Smoke(Random.value < 0.5f ? w0 : w1, back, drift ? 1.25f : 1f); }

            // zig-zag energy bolts: speed bursts and sharp turns, flying off backwards and up
            boltAcc += (burst ? 9f : sp > 0.34f * q ? 3f : 0f) * dt;
            while (boltAcc >= 1f) { boltAcc -= 1f; BackBolt(here, y, back, right, Random.value < 0.5f ? 1f : -1f); }

            // speed streaks behind the wheels
            streakAcc += (sp > 0.25f * q ? (sp - 0.2f * q) * 110f : 0f) * dt;
            while (streakAcc >= 1f) { streakAcc -= 1f; ComicFX.Streak((Random.value < 0.5f ? w0 : w1) + back * 0.05f + Vector3.up * (0.01f + Random.value * 0.02f), f); }

            // sparks while braking hard; on the drift button also a few off the wheels
            if (braking && Random.value < dt * 25f) ComicFX.Spark(Random.value < 0.5f ? w0 : w1);
            if (drifting && sp > DriftMinSpeed && Random.value < dt * 10f) ComicFX.Spark(Random.value < 0.5f ? w0 : w1);

            // through your own eyes the wheels are out of sight below you: a share of the smoke and bolts comes off
            // the outside of the turn and curls forwards into the bottom of the view
            bool fp = FirstPerson(out var cam);
            Vector3 vf = f;
            if (fp)
            {
                vf = cam.forward; vf.y = 0f; vf = vf.sqrMagnitude > 1e-4f ? vf.normalized : f;
                var vr = new Vector3(vf.z, 0f, -vf.x);
                float turn = Mathf.Abs(yr) > 0.3f ? Mathf.Sign(yr) : (Random.value < 0.5f ? 1f : -1f);
                var outward = -vr * turn;          // yaw rising = turning right, so the outside is on the left
                fpSmokeAcc += (drift ? 10f : braking ? 6f : 0f) * dt;
                while (fpSmokeAcc >= 1f) { fpSmokeAcc -= 1f; FpPuff(cam, y, vf, outward, drift ? 1.25f : 1f); }
                fpBoltAcc += (burst ? 4f : 0f) * dt;
                while (fpBoltAcc >= 1f) { fpBoltAcc -= 1f; FpBolt(cam, y, vf, outward); }
            }
            else fpSmokeAcc = fpBoltAcc = 0f;

            // a fast run that comes to a stop: sparks, glints and a puff thrown forwards
            if (sp < 0.08f && peak > 0.2f * q && peakT < 2.2f && !stopped)
            {
                bool hard = peak > 0.26f * q;
                stopped = true; peak = 0f;
                for (int i = 0; i < 8; i++) ComicFX.Spark(i % 2 == 1 ? w0 : w1, 1.2f);
                for (int i = 0; i < 4; i++) ComicFX.Glint(new Vector3(here.x, y + 0.03f, here.z), 1.3f);
                for (int i = 0; i < 5; i++) ComicFX.Smoke(i % 2 == 1 ? w0 : w1, f, 1.1f);
                if (fp)
                {
                    var ahead = new Vector3(cam.position.x, y, cam.position.z) + vf * 0.15f;
                    for (int i = 0; i < 3; i++) ComicFX.Glint(ahead + Vector3.up * 0.02f, 1.3f * FpScale);
                    for (int i = 0; i < 4; i++) ComicFX.Spark(ahead, FpScale);
                }
                // the web flashes the screen here; in VR a sound word instead (never a flash)
                if (hard && skrrtCd <= 0f && pd != null && pd.cam != null) { skrrtCd = 2.5f; ComicEvents.Own(pd, "SKRRT!", ComicColors.Orange, 0.9f); }
            }
            if (sp > 0.15f * q) stopped = false;
        }

        bool FirstPerson(out Transform cam)
        {
            cam = pd != null && pd.cam != null && !pd.thirdPerson && ComicPop.Viewer == null ? pd.cam.transform : null;
            return cam != null;
        }

        void BackBolt(Vector3 here, float y, Vector3 back, Vector3 right, float side)
        {
            var d = (back + right * (side * 0.5f) + Vector3.up * (0.15f + Random.value * 0.3f)).normalized;
            ComicFX.Bolt(new Vector3(here.x, y + 0.05f + Random.value * 0.08f, here.z) - right * (side * 0.05f) + back * 0.03f, d);
        }

        /// <summary>A puff for your own eyes: starts a little ahead of your feet on the outside of the turn, keeps pace
        /// with you for a moment (it carries your velocity), rises and slides outwards, then drag lets it fall behind.</summary>
        void FpPuff(Transform cam, float y, Vector3 vf, Vector3 outward, float scale)
        {
            var foot = new Vector3(cam.position.x, y, cam.position.z);
            var p = foot + vf * Random.Range(0.12f, 0.16f) + outward * Random.Range(0.03f, 0.06f);
            var carry = sv + vf * 0.25f + outward * Random.Range(0.1f, 0.2f) + Vector3.up * Random.Range(0.1f, 0.16f);
            ComicFX.Smoke(p, outward, scale * FpScale, carry);
        }

        /// <summary>A bolt for your own eyes, low in the view on the outside of the turn, travelling with you.</summary>
        void FpBolt(Transform cam, float y, Vector3 vf, Vector3 outward)
        {
            var foot = new Vector3(cam.position.x, y, cam.position.z);
            var p = foot + vf * Random.Range(0.14f, 0.18f) + outward * Random.Range(0.04f, 0.08f) + Vector3.up * Random.Range(0.05f, 0.09f);
            var d = (outward * 0.8f + vf * 0.2f + Vector3.up * Random.Range(0.2f, 0.5f)).normalized;
            ComicFX.Bolt(p, d, FpScale, sv);
        }

        /// <summary>A push stroke kicks up a small puff.</summary>
        void KickPuff()
        {
            if (!On) return;
            var w = Random.value < 0.5f ? pair.Left.position : pair.Right.position;
            ComicFX.Smoke(w, -Fwd(body.Yaw), 0.9f);
        }

        /// <summary>Boost (or a drift slingshot): a cloud off both wheels and a handful of bolts.</summary>
        void BoostBurst(float yaw, bool sling)
        {
            if (!On) return;
            var f = Fwd(yaw); var back = -f; var right = new Vector3(f.z, 0f, -f.x);
            Vector3 w0 = pair.Left.position, w1 = pair.Right.position, here = transform.position;
            float y = Mathf.Min(w0.y, w1.y); w0.y = y; w1.y = y;
            for (int i = 0; i < (sling ? 4 : 6); i++) ComicFX.Smoke(i % 2 == 0 ? w0 : w1, back, 1.2f);
            for (int i = 0; i < (sling ? 2 : 4); i++) BackBolt(here, y, back, right, i % 2 == 0 ? 1f : -1f);
            if (FirstPerson(out var cam))
            {
                var vf = cam.forward; vf.y = 0f; vf = vf.sqrMagnitude > 1e-4f ? vf.normalized : f;
                var vr = new Vector3(vf.z, 0f, -vf.x);
                for (int i = 0; i < (sling ? 2 : 4); i++) FpPuff(cam, y, vf, i % 2 == 0 ? vr : -vr, 1.2f);
                for (int i = 0; i < 2; i++) FpBolt(cam, y, vf, i % 2 == 0 ? vr : -vr);
            }
        }

        /// <summary>A hard hit on a wall: sparks and glints where the skates met it.</summary>
        void CrashBurst(Vector3 into)
        {
            Vector3 w0 = pair.Left.position, w1 = pair.Right.position, here = transform.position;
            float y = Mathf.Min(w0.y, w1.y); w0.y = y; w1.y = y;
            var hit = new Vector3(here.x, y + 0.03f, here.z) + into * 0.06f;
            for (int i = 0; i < 10; i++) ComicFX.Spark(hit, 1.2f);
            for (int i = 0; i < 3; i++) ComicFX.Glint(hit, 1.2f);
            for (int i = 0; i < 4; i++) ComicFX.Smoke(i % 2 == 0 ? w0 : w1, -into, 1.1f);
        }

        /// <summary>Lay a skid stroke from this wheel's last floor point to its new one (on the floor, never across a step).</summary>
        void Skid(int i, Vector3 wheel, bool on)
        {
            if (!on || !On) { lastSkid[i] = null; return; }
            var p = wheel; p.y = WorldQueries.GroundNear(p.x, p.z, p.y - 0.03f) + 0.0015f;
            if (lastSkid[i] is Vector3 a)
            {
                float len = new Vector2(p.x - a.x, p.z - a.z).magnitude;
                if (len < 0.008f) return;
                if (len < 0.08f && Mathf.Abs(p.y - a.y) < 0.01f) ComicFX.Skid(a, p, 0.012f + 0.006f * Random.value);
            }
            lastSkid[i] = p;
        }

        // ------------------------------------------------------------------ procedural sounds
        static AudioClip Noise(float len, float vol, bool loop)
        {
            int n = (int)(44100 * len); var d = new float[n]; var r = new System.Random(9); float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                lp += ((float)r.NextDouble() * 2f - 1f - lp) * 0.08f;          // rumbly low-passed noise
                float env = loop ? 1f : Mathf.Sin(Mathf.PI * i / n);
                d[i] = lp * 3f * vol * env;
            }
            if (loop) for (int i = 0; i < 400; i++) { float k = i / 400f; d[n - 400 + i] = Mathf.Lerp(d[n - 400 + i], d[i], k); }
            var c = AudioClip.Create("skate noise", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        static AudioClip Sweep(float f0, float f1, float len)
        {
            int n = (int)(44100 * len); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++) { float u = i / (float)n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u * u) / 44100f; d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sin(ph * 2.01f) * 0.2f) * (1f - u) * Mathf.Min(1f, u * 20f) * 0.6f; }
            var c = AudioClip.Create("sweep", n, 1, 44100, false); c.SetData(d, 0); return c;
        }
    }
}
