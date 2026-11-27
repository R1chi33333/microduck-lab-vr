using System.Collections;
using System.Collections.Generic;
using System.IO;
using MicroduckVR.Core;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// The playthrough: one long, unbroken session filmed through the game's own (third-person) camera, played by a
    /// "virtual player" that holds the controllers like a person does. Its head never stops moving a little, turns
    /// with a person's speed and settle, glances at things; its thumbs push the sticks part-way and let go gently;
    /// it reads each tutorial card before acting, hovers over a few tiles before choosing, sometimes misses a bite
    /// and tries again. Every button press is written to prompts.txt so the edit can show it on screen.
    /// </summary>
    public static partial class Film
    {
        [MenuItem("Microduck/Film/Playthrough")]
        public static void Playthrough() => Begin("play", PlayShots(), false, new Quality { ss = 2, bitrate = 18000000 });
        [MenuItem("Microduck/Film/Playthrough (preview stills)")]
        public static void PlaythroughPreview() => Begin("play", PlayShots(), true, new Quality { w = 640, h = 360 });

        /// <summary>Set by a shot's script when it has finished (the shot then ends early).</summary>
        static bool shotOver;

        static List<Shot> PlayShots() => new List<Shot>
        {
            new Shot { name = "play", dur = 420f, pre = 1.5f, fp = true, stillEvery = 2f, caption = "Playthrough",
                start = () => VpStart(Session()), tick = VpTick },
        };

        // ================================================================== the virtual player
        static System.Random vrng;
        static float vpT;                                    // game seconds since the script began
        static readonly Stack<IEnumerator> co = new Stack<IEnumerator>();
        static StreamWriter prompts;

        // head: where the person looks (world yaw and pitch, degrees), with a person's dynamics
        static float hYaw, hPitch, hYawV, hPitchV, wantYaw, wantPitch = 10f; static Vector3? lookAt; static bool lookWhereGoing = true;
        static float headStiff = 6f;                         // rad/s: how briskly the head goes where it wants
        static float snapCool; static float? pendingTurn;
        // left thumb
        static Vector2 stickL, stickLV; static Vector3? moveGoal; static float moveSpeed = 1f, moveStop = 0.08f, stuckT, sideStepT; static float sideSign = 1f;
        // right hand (pointer): a point in the world the hand aims at, moving like a wrist
        static Vector3 aim, aimV; static Vector3? aimGoal; static bool aimOn;
        static string lastCardSeen = "";

        static void VpStart(IEnumerator script)
        {
            vrng = new System.Random(7); vpT = 0f; shotOver = false; co.Clear(); co.Push(script);
            prompts?.Dispose(); prompts = new StreamWriter(Path.Combine(dir, "prompts.txt"));
            var f = P.cam.transform.forward; hYaw = wantYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; hPitch = wantPitch = 10f; hYawV = hPitchV = 0f;
            stickL = stickLV = Vector2.zero; moveGoal = null; lookAt = null; lookWhereGoing = true; aimOn = false; aimGoal = null; snapCool = 0f;
            GameHud.Hidden = false;
        }

        static void VpTick(float t)
        {
            GameHud.Hidden = false;
            if (t < 0f) { DriveHead(Dt); FilmFromGame(); return; }
            vpT += Dt;
            // run the script up to its next wait
            while (co.Count > 0)
            {
                var top = co.Peek();
                if (!top.MoveNext()) { co.Pop(); continue; }
                if (top.Current is IEnumerator inner) { co.Push(inner); continue; }
                break;
            }
            if (co.Count == 0 && !shotOver) { shotOver = true; prompts?.Flush(); }
            DriveStick(Dt); DriveHead(Dt); DrivePointer(Dt);
            FilmFromGame();
        }

        /// <summary>The film camera is the game's own camera (what the player sees), at a video-friendly width.</summary>
        static void FilmFromGame()
        {
            cam.transform.SetPositionAndRotation(P.cam.transform.position, P.cam.transform.rotation);
            cam.fieldOfView = 76f; cam.cullingMask = P.cam.cullingMask;
        }

        static float Noise(float speed, float seed) => Mathf.PerlinNoise(vpT * speed + seed, seed * 1.7f) * 2f - 1f;

        static void DriveHead(float dt)
        {
            // what the person wants to look at
            if (lookAt.HasValue)
            {
                var from = P.cam.transform.position; var d = lookAt.Value - from; var h = new Vector2(d.x, d.z).magnitude;
                wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; wantPitch = Mathf.Clamp(Mathf.Atan2(-d.y, Mathf.Max(0.05f, h)) * Mathf.Rad2Deg, -35f, 55f);
            }
            else if (lookWhereGoing && moveGoal.HasValue && stickL.magnitude > 0.3f)
            {
                var d = moveGoal.Value - Pos; d.y = 0f;
                if (d.magnitude > 0.15f) wantYaw = Mathf.MoveTowardsAngle(wantYaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 90f * dt);
                wantPitch = Mathf.MoveTowards(wantPitch, 12f, 20f * dt);
            }
            // a critically damped spring with a top speed, like a neck; plus the small drift a head never stops making
            float k = headStiff * headStiff, c = 2f * headStiff;
            float ey = Mathf.DeltaAngle(hYaw, wantYaw);
            hYawV += (k * ey - c * hYawV) * dt; hYawV = Mathf.Clamp(hYawV, -150f, 150f); hYaw += hYawV * dt;
            hPitchV += (k * (wantPitch - hPitch) - c * hPitchV) * dt; hPitchV = Mathf.Clamp(hPitchV, -90f, 90f); hPitch += hPitchV * dt;
            float ny = Noise(0.23f, 1.3f) * 2.2f + Noise(1.9f, 4.1f) * 0.35f, np = Noise(0.19f, 7.7f) * 1.6f + Noise(2.3f, 2.9f) * 0.25f;

            // a long turn of the head is finished with the right stick, a snap at a time, like VR players do
            float local = Mathf.DeltaAngle(P.ViewYaw, hYaw + ny);
            snapCool -= dt;
            PlayerDuck.DebugTurn = null;
            float? turn = pendingTurn; pendingTurn = null; bool byHand = turn.HasValue;
            if (!turn.HasValue && Mathf.Abs(local) > 62f && snapCool <= 0f && Mathf.Abs(Mathf.DeltaAngle(P.ViewYaw, wantYaw)) > 55f)
            {
                turn = Mathf.Sign(local); PromptOnce("rstick", "R-STICK", "TURN", 1.0f);
            }
            if (turn.HasValue)
            {
                // the stick turns the whole play space: the head turns with it (it keeps its place on the shoulders)
                PlayerDuck.DebugTurn = turn.Value; snapCool = 0.42f;
                float step = Mathf.Sign(turn.Value) * P.snapDegrees; hYaw += step;
                if (byHand && !lookAt.HasValue) wantYaw += step;      // a deliberate turn: what they look at turns too
                local = Mathf.DeltaAngle(P.ViewYaw + step, hYaw + ny);
            }
            local = Mathf.Clamp(local, -85f, 85f);
            PlayerDuck.DebugHead = Quaternion.Euler(hPitch + np, local, 0f);
        }

        static void DriveStick(float dt)
        {
            Vector2 want = Vector2.zero;
            if (moveGoal.HasValue)
            {
                var d = moveGoal.Value - Pos; d.y = 0f; float dist = d.magnitude;
                if (dist > moveStop)
                {
                    float ang = Mathf.Atan2(d.x, d.z) + Noise(0.5f, 3.3f) * 0.09f;
                    // stuck against something: step to one side for a moment, the way a person wiggles round a chair leg
                    if (stickL.magnitude > 0.5f && P.Speed < 0.025f) stuckT += dt; else stuckT = Mathf.Max(0f, stuckT - dt);
                    if (stuckT > 0.5f) { sideStepT = 0.9f; stuckT = 0f; sideSign = vrng.NextDouble() < 0.5 ? -1f : 1f; }
                    if (sideStepT > 0f) { sideStepT -= dt; ang += sideSign * 1.2f; }
                    var w = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                    float hy = P.cam.transform.eulerAngles.y * Mathf.Deg2Rad;
                    var fwd = new Vector3(Mathf.Sin(hy), 0f, Mathf.Cos(hy)); var right = new Vector3(fwd.z, 0f, -fwd.x);
                    float mag = moveSpeed * (0.9f + 0.1f * Noise(0.7f, 5.5f)) * Mathf.Clamp(dist / 0.3f, 0.45f, 1f);
                    want = new Vector2(Vector3.Dot(w, right), Vector3.Dot(w, fwd)) * mag;
                }
            }
            stickL = Vector2.SmoothDamp(stickL, want, ref stickLV, want == Vector2.zero ? 0.07f : 0.11f, 20f, dt);
            if (stickL.magnitude < 0.02f && want == Vector2.zero) stickL = Vector2.zero;
            PlayerDuck.DebugMove = stickL;
        }

        static void DrivePointer(float dt)
        {
            if (!aimOn) { Bag.SetDebugAim(null); WorldPanel.DebugPoint = null; return; }
            if (aimGoal.HasValue) aim = Vector3.SmoothDamp(aim, aimGoal.Value, ref aimV, 0.13f, 3f, dt);
            float tremor = 0.0025f;
            var p = aim + new Vector3(Noise(3.1f, 8.8f), Noise(2.7f, 6.6f), Noise(2.9f, 9.9f)) * tremor;
            var eye = P.cam.transform.position;
            if (BagMenu.Busy) { Bag.SetDebugAim(new Ray(eye, p - eye)); WorldPanel.DebugPoint = null; }
            else { Bag.SetDebugAim(null); WorldPanel.DebugPoint = p; }
        }

        // ------------------------------------------------------------------ verbs the script uses
        static IEnumerator Wait(float s) { float end = vpT + s; while (vpT < end) yield return null; }
        static IEnumerator Think(float a, float b) => Wait(a + (float)vrng.NextDouble() * (b - a));
        static IEnumerator Until(System.Func<bool> c, float timeout = 30f) { float end = vpT + timeout; while (!c() && vpT < end) yield return null; }

        /// <summary>Walk to a point, the head mostly looking the way it goes (stick released at the end).</summary>
        static IEnumerator GoTo(Vector3 p, float speed = 1f, float stop = 0.08f, float timeout = 25f)
        {
            moveGoal = p; moveSpeed = speed; moveStop = stop; float end = vpT + timeout;
            while (vpT < end) { var d = p - Pos; d.y = 0f; if (d.magnitude <= stop) break; yield return null; }
            moveGoal = null;
        }
        /// <summary>Walk a route through the doors (HouseLayout.Route), turning the corners like a person.</summary>
        static IEnumerator Travel(Vector3 to, float speed = 1f)
        {
            foreach (var w in HouseLayout.Route(Pos, to)) yield return GoTo(w, speed, 0.22f);
            yield return GoTo(to, speed, 0.1f);
        }
        static void Gaze(Vector3? p) { lookAt = p; }
        static IEnumerator Glance(Vector3 p, float hold) { var old = lookAt; lookAt = p; yield return Wait(hold); lookAt = old; }
        static IEnumerator LookAround(float dur, float spanDeg = 70f)
        {
            float baseYaw = hYaw, end = vpT + dur; lookAt = null; lookWhereGoing = false;
            while (vpT < end)
            {
                wantYaw = baseYaw + ((float)vrng.NextDouble() * 2f - 1f) * spanDeg * 0.5f; wantPitch = 4f + (float)vrng.NextDouble() * 18f;
                yield return Think(0.8f, 1.6f);
            }
            lookWhereGoing = true;
        }

        static void Prompt(string key, string label, float dur = 1.4f)
        {
            if (prompts == null || preview) return;
            prompts.WriteLine($"{outT:F2}|{key}|{label}|{dur:F2}"); prompts.Flush();
        }
        static readonly HashSet<string> promptedOnce = new HashSet<string>();
        static void PromptOnce(string id, string key, string label, float dur) { if (promptedOnce.Add(id)) Prompt(key, label, dur); }
        static void Chapter(string name) { if (prompts != null && !preview) { prompts.WriteLine($"{outT:F2}|CHAPTER|{name}|0"); prompts.Flush(); } Debug.Log($"[Microduck] play: {name} at {vpT:F1}s"); }

        /// <summary>Move the pointing hand onto a point and keep it there.</summary>
        static IEnumerator PointAt(Vector3 p, float settle = 0.45f)
        {
            if (!aimOn) { aimOn = true; var eye = P.cam.transform.position; aim = eye + P.cam.transform.forward * Vector3.Distance(eye, p); aimV = Vector3.zero; }
            // a slight overshoot past the target, then onto it
            var over = p + (p - aim) * 0.08f;
            aimGoal = over; yield return Wait(settle * 0.55f);
            aimGoal = p; yield return Wait(settle * 0.45f);
        }
        static void HandDown() { aimOn = false; aimGoal = null; }

        /// <summary>Hover the bag tiles one after another, then pick the last.</summary>
        static IEnumerator BrowseBag(params string[] tiles)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                yield return PointAt(Bag.TileWorld(tiles[i]), 0.35f + (float)vrng.NextDouble() * 0.2f);
                yield return Think(i == tiles.Length - 1 ? 0.35f : 0.25f, i == tiles.Length - 1 ? 0.7f : 0.6f);
            }
            Prompt("RT", "CHOOSE"); Bag.DebugPress(); yield return null;
        }

        static IEnumerator PressPanel(WorldPanel p, string id, float think = 0.5f)
        {
            WorldPanel.Button b = null; foreach (var x in p.buttons) if (x.id == id) b = x;
            if (b == null) yield break;
            yield return PointAt(b.t.position, 0.4f + (float)vrng.NextDouble() * 0.25f);
            yield return Think(think * 0.6f, think * 1.3f);
            Prompt("RT", "SELECT", 0.9f); WorldPanel.DebugPress = true; yield return null;
        }

        /// <summary>Walk up to a thing, open the beak (it reaches down by itself), let go to bite. The first try is too
        /// early when asked, the way people often bite at nothing the first time; then a little shuffle closer.</summary>
        static IEnumerator GrabThing(Grabbable g, bool missFirst)
        {
            if (g == null) yield break;
            Gaze(g.Centre);
            yield return GoTo(g.Centre, 0.55f, 0.16f, 8f);
            yield return Think(0.25f, 0.5f);
            for (int attempt = 0; attempt < 4 && P.Held == null; attempt++)
            {
                PlayerDuck.DebugBeak = 1f; if (attempt == 0) Prompt("RT", "HOLD: OPEN BEAK", 1.6f);
                if (missFirst && attempt == 0)
                {
                    yield return Wait(0.35f); PlayerDuck.DebugBeak = 0f; Prompt("RT", "LET GO: BITE", 0.9f);
                    yield return Wait(0.7f); continue;                                  // chomp on air
                }
                float until = vpT + 1.6f;
                while (vpT < until && Grabbable.Nearest(P.Body.BeakGap(), 0.02f) != g) yield return null;
                if (Grabbable.Nearest(P.Body.BeakGap(), 0.02f) == g)
                {
                    yield return Think(0.1f, 0.22f); PlayerDuck.DebugBeak = 0f; Prompt("RT", "LET GO: BITE", 1.0f); yield return Wait(0.4f);
                }
                else { PlayerDuck.DebugBeak = 0f; yield return Think(0.3f, 0.5f); yield return GoTo(g.Centre, 0.35f, 0.1f, 1.5f); }
            }
            PlayerDuck.DebugBeak = 0f; Gaze(null);
        }

        static IEnumerator ReadCard(string startsWith, float min = 0.9f, float max = 1.6f)
        {
            yield return Until(() => TutorialCoach.DebugTitle.StartsWith(startsWith), 20f);
            lastCardSeen = startsWith; Debug.Log($"[Microduck] play: card {TutorialCoach.DebugTitle} at {vpT:F1}s");
            yield return Think(min, max);               // read it
        }

        static Grabbable NearestToy(string label, Vector3 near, float within)
        {
            Grabbable best = null; float bd = within;
            foreach (var g in Grabbable.All) { if (label != null && g.label != label) continue; float d = Vector3.Distance(g.Centre, near); if (d < bd) { bd = d; best = g; } }
            return best;
        }

        // ================================================================== the session
        // Places (from the plan view, Microduck/Film MapShot): the mirror is on the bedroom's south wall; the bedroom has
        // open floor at x 4.2..5.2, z -1.4..0; its door to the hallway is at (5.0, 2.56); the hallway's clear lane is
        // z 2.95 from x 4.6 to -1.0; the living room door is at (0, 2.56), the football rug at x 1..2.5, z -0.6..2.2;
        // the patio door at (0, 3.88), the garden pond at (-1.5, 5.4).
        static IEnumerator SnapTurn(float sign) { pendingTurn = sign; yield return null; yield return null; }

        static IEnumerator Session()
        {
            // ---------------- a new player: the tutorial starts at the mirror
            Chapter("mirror");
            TutorialCoach.Restart();
            yield return Wait(0.6f);
            var mir = MirrorStation.Instance;
            var mc = MirrorStation.Centre;
            var mirrorC = new Vector3(mc.x, Pos.y + 0.13f, mc.z);                     // where the duck's reflection is
            Gaze(mirrorC);
            if (!mir.BoardOpen) { yield return Think(0.8f, 1.2f); Prompt("X", "MAKE YOUR DUCK"); mir.OpenBoard(true); yield return Wait(0.5f); }
            yield return Think(1.2f, 1.8f);                                          // oh, that's me
            yield return Glance(mir.Board.transform.position, 1.0f);
            yield return PressPanel(mir.Board, "skin_sky", 0.7f);
            yield return Glance(mirrorC, 1.0f);
            yield return PressPanel(mir.Board, "skin_graphite", 0.5f);
            yield return Glance(mirrorC, 0.8f);                                     // hmm, no
            yield return PressPanel(mir.Board, "skin_sky", 0.4f);
            yield return PressPanel(mir.Board, "part_head", 0.5f);
            yield return PressPanel(mir.Board, "paint_lavender", 0.6f);
            yield return Glance(mirrorC, 1.2f);
            yield return PressPanel(mir.Board, "voice_2", 0.5f);
            yield return Think(0.5f, 0.8f);
            yield return PressPanel(mir.Board, "random", 0.4f);
            yield return Glance(mirrorC, 1.0f);
            yield return PressPanel(mir.Board, "random", 0.3f);
            yield return Glance(mirrorC, 1.3f);                                     // that one's good
            yield return PressPanel(mir.Board, "save", 0.6f);
            HandDown(); Gaze(null);
            yield return Wait(0.8f);

            // ---------------- tutorial: getting around (in the open part of the bedroom)
            Chapter("tutorial");
            yield return ReadCard("LOOKING GOOD", 0.6f, 1.0f);
            yield return ReadCard("WADDLE", 1.0f, 1.5f);
            wantYaw = 0f; yield return Wait(0.6f);                                  // turn the head away from the mirror...
            yield return SnapTurn(1f); yield return Wait(0.35f); yield return SnapTurn(1f); yield return Wait(0.35f);
            yield return SnapTurn(1f); yield return Wait(0.35f);
            PromptOnce("lstick", "L-STICK", "WALK", 1.6f);
            yield return GoTo(new Vector3(4.55f, 0f, -1.1f), 0.8f, 0.1f, 6f);
            yield return GoTo(new Vector3(4.9f, 0f, -0.55f), 0.7f, 0.1f, 6f);
            yield return ReadCard("TURN", 0.6f, 1.0f);
            Prompt("R-STICK", "TURN", 1.2f); yield return SnapTurn(-1f); yield return Wait(0.5f); yield return SnapTurn(-1f);
            wantYaw = P.ViewYaw; yield return Wait(0.8f);
            yield return Glance(Pos + new Vector3(0.8f, 0.5f, 0.6f), 0.9f);          // the giant toy box

            yield return ReadCard("OPEN YOUR BEAK", 0.8f, 1.3f);
            Prompt("RT", "HOLD: OPEN BEAK", 1.5f); PlayerDuck.DebugBeak = 1f; yield return Wait(1.3f); PlayerDuck.DebugBeak = 0f;
            yield return ReadCard("GRAB IT", 0.8f, 1.4f);
            yield return GrabThing(NearestToy("rubber duck", Pos, 1.2f) ?? NearestToy(null, Pos, 0.8f), true);
            yield return ReadCard("DROP IT", 0.5f, 0.9f);
            yield return GoTo(new Vector3(4.6f, 0f, -0.3f), 0.7f, 0.08f, 5f);
            Prompt("RT", "DROP", 1.0f); PlayerDuck.DebugBeak = 1f; yield return Wait(0.25f); PlayerDuck.DebugBeak = 0f;
            yield return ReadCard("QUACK", 0.4f, 0.8f);
            Prompt("A", "QUACK"); PlayerDuck.DebugChirp = true; yield return Think(0.5f, 0.8f);
            Prompt("A", "QUACK", 0.8f); PlayerDuck.DebugChirp = true; yield return Wait(0.9f);

            // ---------------- the toy bag
            Chapter("bag");
            yield return ReadCard("TOY BAG", 0.5f, 0.9f);
            Prompt("Y", "TOY BAG"); Bag.Open(); yield return Wait(0.5f);
            yield return ReadCard("PICK A TOY", 0.4f, 0.8f);
            yield return BrowseBag("rubber_duck_pink", "toy_car", "wicker_ball", "strawberry", "toy_car");
            yield return ReadCard("PUT IT DOWN", 0.4f, 0.8f);
            var f0 = GltfSpace.Forward(hYaw * Mathf.Deg2Rad);
            var spot = Ground(Pos.x + f0.x * 0.4f, Pos.z + f0.z * 0.4f) + Vector3.up * 0.01f;
            yield return PointAt(spot, 0.6f); yield return Think(0.3f, 0.6f);
            Prompt("RT", "PLACE"); Bag.DebugPress(); yield return Wait(0.6f);
            yield return PointAt(spot + new Vector3(0.12f, 0f, 0.06f), 0.4f); Bag.DebugPress(); yield return Wait(0.5f);
            yield return ReadCard("CLOSE THE BAG", 0.3f, 0.6f);
            HandDown(); Prompt("Y", "CLOSE"); Bag.Close(); yield return Wait(0.8f);
            yield return Glance(spot, 1.2f);

            // ---------------- skates: out to the long hallway
            Chapter("skates");
            yield return ReadCard("ROLLER SKATES", 0.6f, 1.0f);
            Prompt("B", "SKATES ON"); Skates(true); yield return Wait(0.9f);
            yield return ReadCard("ROLL!", 0.4f, 0.7f);
            PromptOnce("lstick_skate", "L-STICK", "SKATE", 1.4f);
            yield return GoTo(new Vector3(5.0f, 0f, 1.6f), 0.7f, 0.25f, 8f);
            yield return GoTo(new Vector3(5.0f, 0f, 2.9f), 0.7f, 0.25f, 5f);
            yield return GoTo(new Vector3(3.2f, 0f, 2.95f), 1f, 0.3f, 6f);
            yield return ReadCard("BOOST", 0.2f, 0.4f);
            Prompt("LT", "BOOST"); PlayerSkates.DebugBoost = 1f; yield return Wait(0.35f); PlayerSkates.DebugBoost = null;
            yield return GoTo(new Vector3(-0.4f, 0f, 2.95f), 1f, 0.35f, 7f);
            yield return ReadCard("DRIFT", 0.2f, 0.4f);
            Prompt("L-GRIP", "HOLD + STEER: DRIFT", 1.8f); PlayerSkates.DebugDrift = 1f;
            yield return GoTo(new Vector3(-1.3f, 0f, 3.5f), 1f, 0.3f, 2.5f);
            yield return GoTo(new Vector3(0.2f, 0f, 3.3f), 1f, 0.35f, 2.5f);
            PlayerSkates.DebugDrift = null;
            yield return GoTo(new Vector3(1.4f, 0f, 3.1f), 0.6f, 0.3f, 5f);
            yield return ReadCard("SKATES OFF", 0.3f, 0.6f);
            Prompt("B", "SKATES OFF"); Skates(false); yield return Wait(0.8f);

            // ---------------- laser dot: the flock is somewhere near
            Chapter("laser");
            yield return ReadCard("LASER DOT", 0.6f, 1.0f);
            DuckBrain near = null; float nd = 9f; foreach (var b in DuckBrain.All) { float d = Vector3.Distance(b.Pos, Pos); if (d < nd) { nd = d; near = b; } }
            var dotC = near != null && nd < 2.5f ? Vector3.Lerp(Pos, near.Pos, 0.6f) : Pos + GltfSpace.Forward(hYaw * Mathf.Deg2Rad) * 0.55f;
            dotC.y = WorldQueries.GroundNear(dotC.x, dotC.z, 0.3f);
            Prompt("R-GRIP", "HOLD: LASER", 2.2f);
            float lEnd = vpT + 6.5f;
            while (vpT < lEnd)
            {
                float a = (vpT - lEnd) * 0.9f;
                var p = dotC + new Vector3(Mathf.Cos(a) * 0.2f + Noise(0.6f, 2f) * 0.05f, 0f, Mathf.Sin(a * 1.3f) * 0.12f);
                LaserPointer.DebugPoint = p; Gaze(p); yield return null;
            }
            LaserPointer.DebugPoint = null; Gaze(null);
            yield return Wait(0.8f);

            // ---------------- the duck's-eye view
            Chapter("first person");
            yield return ReadCard("DUCK'S-EYE VIEW", 0.6f, 1.0f);
            Prompt("L-STICK CLICK", "DUCK'S-EYE VIEW", 1.8f); P.SetThirdPerson(false); yield return Wait(0.8f);
            yield return ReadCard("EVERYTHING IS GIANT", 0.3f, 0.5f);
            headStiff = 4f;
            wantPitch = -32f; wantYaw = hYaw - 30f; yield return Wait(1.7f);           // up at the lamps and the ceiling
            wantYaw = hYaw + 75f; wantPitch = -20f; yield return Wait(1.8f);
            wantPitch = 6f; yield return Wait(1.0f);
            headStiff = 6f;
            yield return ReadCard("BACK OUT", 0.3f, 0.6f);
            Prompt("L-STICK CLICK", "BACK OUT", 1.4f); P.SetThirdPerson(true); yield return Wait(0.6f);
            yield return ReadCard("YOU'RE READY", 1.5f, 2.2f);
            yield return Until(() => TutorialCoach.DebugTitle == "(none)", 8f);

            // ---------------- free play: football in the living room
            Chapter("football");
            yield return GoTo(new Vector3(0.6f, 0f, 2.95f), 0.9f, 0.2f, 6f);
            yield return GoTo(new Vector3(0.1f, 0f, 2.3f), 0.9f, 0.2f, 6f);
            yield return GoTo(new Vector3(0.6f, 0f, 1.4f), 0.9f, 0.2f, 6f);
            Prompt("Y", "TOY BAG"); Bag.Open(); yield return Wait(0.6f);
            yield return BrowseBag("dance", "race", "football");
            HandDown(); yield return Wait(1.2f);
            float fEnd = vpT + 15f;
            var fg = FootballGame.Instance;
            while (vpT < fEnd && fg != null && FootballGame.Active)
            {
                // come at the ball from the far side of the goal it should go into, then run through it
                var ball = fg.BallPos; var goal = fg.GoalCentre(1); var toGoal = goal - ball; toGoal.y = 0f; toGoal.Normalize();
                var behind = ball - toGoal * 0.12f;
                bool lined = Vector3.Dot((Pos - ball).normalized, -toGoal) > 0.6f;
                moveGoal = lined ? ball + toGoal * 0.2f : behind;
                moveSpeed = 1f; moveStop = 0.02f; Gaze(ball);
                yield return null;
            }
            moveGoal = null; Gaze(null);
            if (FootballGame.Active) FootballGame.Instance.StopGame();
            yield return Wait(0.6f);

            // ---------------- the race round the whole house
            Chapter("race");
            Prompt("Y", "TOY BAG"); Bag.Open(); yield return Wait(0.6f);
            yield return BrowseBag("football", "race");
            HandDown();
            yield return Until(() => !RaceGame.Frozen, 6f);
            PromptOnce("race_go", "L-STICK", "RUN!", 1.2f);
            float rEnd = vpT + 22f;
            while (vpT < rEnd && RaceGame.Active)
            {
                var tg = RaceGame.Ensure().PlayerTarget(); if (!tg.HasValue) break;
                moveGoal = tg.Value; moveSpeed = 1f; moveStop = 0.02f; yield return null;
            }
            moveGoal = null;
            if (RaceGame.Active) RaceGame.Ensure().StopRace();

            // ---------------- outside: the garden, the pond, more ducks, a dance
            Chapter("garden");
            yield return Travel(new Vector3(0.1f, 0f, 4.4f), 1f);
            yield return LookAround(2.4f, 90f);
            yield return GoTo(new Vector3(-0.9f, 0f, 5.1f), 0.9f, 0.15f, 8f);          // into the pond: splash
            yield return Glance(Pos + Vector3.down * 0.05f + GltfSpace.Forward(P.Body.Yaw) * 0.2f, 1.0f);
            yield return GoTo(new Vector3(-0.3f, 0f, 6.1f), 0.9f, 0.15f, 8f);
            Prompt("Y", "TOY BAG"); Bag.Open(); yield return Wait(0.5f);
            yield return BrowseBag("swap", "addduck");
            yield return Think(0.4f, 0.7f);
            Bag.Open(); yield return Wait(0.4f); yield return BrowseBag("addduck"); yield return Wait(0.5f);
            HandDown();
            wantYaw = hYaw + 160f; yield return Wait(1.6f);
            Prompt("X", "DANCE"); P.Body.PlayEmote(EmoteKind.Dance); yield return Wait(0.4f);
            foreach (var b in DuckBrain.All) if (b.Body != null && Vector3.Distance(b.Pos, Pos) < 2.5f) b.Body.PlayEmote(EmoteKind.Dance);
            yield return Wait(2.8f);
            Prompt("A", "QUACK", 1.0f); PlayerDuck.DebugChirp = true; yield return Wait(2.0f);
            Chapter("end");
            yield return Wait(1.0f);
        }


        /// <summary>A plan view of the house (Logs/film/map.png, x -7.5..7.5, z -3..8), for laying out routes.</summary>
        public static void MapShot()
        {
            var go = new GameObject("map cam"); var c = go.AddComponent<Camera>(); c.enabled = false; c.orthographic = true;
            c.orthographicSize = 5.5f; c.aspect = 15f / 11f; c.transform.SetPositionAndRotation(new Vector3(0f, 2.4f, 2.5f), Quaternion.Euler(90f, 0f, 0f));
            c.nearClipPlane = 0.01f; c.farClipPlane = 10f; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = Color.black;
            var rtm = new RenderTexture(1500, 1100, 24) { antiAliasing = 4 }; c.targetTexture = rtm; c.Render();
            var t2 = new Texture2D(1500, 1100, TextureFormat.RGB24, false); RenderTexture.active = rtm; t2.ReadPixels(new Rect(0, 0, 1500, 1100), 0, 0); t2.Apply(); RenderTexture.active = null;
            Directory.CreateDirectory("Logs/film"); File.WriteAllBytes("Logs/film/map.png", t2.EncodeToPNG());
            c.targetTexture = null; Object.DestroyImmediate(rtm); Object.DestroyImmediate(t2); Object.DestroyImmediate(go);
            Debug.Log("[Microduck] map: Logs/film/map.png");
        }
    }
}
