using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// The trailer, cut to the 120 BPM score (1 beat = 0.5 s, every shot length is a whole number of beats):
    ///   cold open 0-6 | title sting 6-8 (graphic) | intro 8-24 | escalation 24-48 | mirror breather 48-56 |
    ///   climax 56-72 (stop-time hits at 70 / 71 / 71.5) | end card 72-78 (graphic) | button gag 78-81 (sting at 79.5).
    /// The two graphics are made outside Unity, so this list holds the 41 filmed shots in running order.
    ///
    /// Camera rules: the duck stands 27 cm tall (eyes at 24.5 cm). The lens lives below its eye line - 10 cm off the
    /// floor indoors, 16 cm on the lawn so it clears the grass tips - and close enough that the duck is a third to a
    /// half of the frame; wide lenses close in for scale and speed, longer ones for faces. It tracks sideways or
    /// stands still, and never rolls or shakes. Every payoff gets at least a second on screen, and four of them get
    /// slow motion (drift, block tower, goal, puddle).
    ///
    /// Sound words: while filming, the game's own thrower (ComicEvents) is parked, because it puts the words where
    /// the player's eyes are; the shots throw the same words (same ComicPop) beside the duck's head for the film
    /// camera instead. It is switched back on with the last frame.
    /// </summary>
    public static partial class Film
    {
        // ------------------------------------------------------------------ places
        static readonly Vector3 Lawn = new Vector3(1.3f, 0f, 5.7f);          // clear lawn, no flowers
        static readonly Vector3 PartyAt = new Vector3(1.3f, 0f, 5.5f);
        static readonly Vector3 DriftC = new Vector3(-5.0f, 0f, -1.0f);      // kitchen tiles
        static readonly Vector3 LaserAt = new Vector3(-1.0f, 0f, 1.0f);
        const float HallZ = 3.22f, TowerX = -0.62f, TowerZ = 1.05f, BagYaw = 60f, BagPitch = 35f, PuddleEdgeX = -0.61f;
        const int TowerLevels = 7;
        // the stages (x, z): loose toys lying on the one a shot plays on are put away for that shot
        static readonly Rect FloorArea = Rect.MinMaxRect(-2.9f, 0.25f, 0.3f, 2.3f), TableArea = Rect.MinMaxRect(-1f, -0.8f, 1f, 1.6f),
                             HallArea = Rect.MinMaxRect(-7f, 2.62f, 7f, 3.82f), PitchArea = Rect.MinMaxRect(1f, -0.9f, 2.9f, 2.4f),
                             KitchenArea = Rect.MinMaxRect(-7f, -2.5f, -3.12f, 2.5f), BedroomArea = Rect.MinMaxRect(3.12f, -2.5f, 7f, 2.5f),
                             LawnArea = Rect.MinMaxRect(-3.5f, 3.94f, 3.5f, 7.6f);
        static readonly Vector2[] bagSpots = { new Vector2(0.27f, -0.10f), new Vector2(0.30f, 0.07f), new Vector2(0.41f, -0.02f), new Vector2(0.43f, 0.15f) };

        // ------------------------------------------------------------------ film-only state
        static string buildLook = "cream";                                   // the look the shots being listed are filmed in
        static DuckCustom wear = new DuckCustom();
        static readonly List<GameObject> filmToys = new List<GameObject>();
        static HashSet<ToyTag> toysBefore;
        static readonly List<GameObject> parked = new List<GameObject>();
        static readonly List<Rigidbody> tower = new List<Rigidbody>();
        static Grabbable biteToy;
        static Transform halo;
        static ComicEvents words;                                            // the game's own sound-word thrower, parked while filming
        static Vector3 trainC, trainU, trainT; static float trainR = 0.55f, wallX = 6.9f;
        /// <summary>Radius of the film-only red disc laid under the game's own laser dot (which is 1.6 cm) so it reads on video. 0 = off.</summary>
        static float HaloRadius = 0.045f;
        /// <summary>Extra shove for the block tower at the moment of impact (m/s). 0 = the game's physics only.</summary>
        static float SmashKick = 0.25f;
        static float hitAt = 99f, splashT;

        // ------------------------------------------------------------------ helpers
        /// <summary>One shot. Before its own setup, toys from earlier shots are cleared and the duck gets the look of this part of the film.</summary>
        static Shot Cut(string name, float dur, float pre, System.Action begin, System.Action<float> run, bool fp = false, string caption = null)
        {
            string lk = buildLook;
            return new Shot
            {
                name = name, dur = dur, pre = pre, fp = fp, caption = caption,
                start = () => { Fresh(lk); begin?.Invoke(); },
                tick = t =>
                {
                    // the garage puts the saved look back a frame after the duck leaves it: put ours on again
                    At(t, 0.08f - pre, "rewear", () => wear.ApplyTo(P.gameObject, false));
                    // the duck looks where the player's view points: keep that view on the duck's own heading, or it skates
                    // and races with its head turned to wherever the last shot left the view
                    if (!fp) P.FaceView(P.Body.Yaw);
                    run(t);
                    // the last frame of the last shot: the game throws its own sound words again, the house gets its toys back
                    if (si == shots.Count - 1 && outT >= shots[si].dur - 1.5f / Q.fps) { Unpark(); if (words != null) words.enabled = true; }
                }
            };
        }

        static void Fresh(string lk)
        {
            Clean(); Unpark(); HideDot(); GameHud.Hidden = true; biteToy = null; BagMenu.HideBeam = false;
            foreach (var pop in Object.FindObjectsByType<ComicPop>(FindObjectsSortMode.None)) Object.Destroy(pop.gameObject);   // no sound word from the last shot hanging in this one
            if (words == null) words = Object.FindAnyObjectByType<ComicEvents>();
            if (words != null) words.enabled = false;
            Wear(lk);
        }

        /// <summary>Paint the player's duck (a colourway, and optionally other colourways per part) without touching the saved look.</summary>
        static void Wear(string skin, string head = "", string body = "", string legs = "", string feet = "")
        {
            wear = new DuckCustom { skin = skin, head = head, body = body, legs = legs, feet = feet };
            wear.ApplyTo(P.gameObject, false);
        }

        /// <summary>A comic sound word (the game's own ComicPop) at a place in the world; it turns to face the film camera.</summary>
        static void Pop(string word, Vector3 at, string hex = "#FF5D73", float size = 1.5f, float life = 1f)
        {
            var vp = cam.WorldToViewportPoint(at);
            if (vp.z > 0.05f)
            {
                float tan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                var hv = cam.WorldToViewportPoint(P.Body.EyePosition);
                bool headIn = hv.z > 0.05f && hv.x > 0.02f && hv.x < 0.98f && hv.y > 0.05f && hv.y < 0.98f;
                // nearer the lens than the player's duck, so never hidden behind it - and looking the same size
                if (headIn) { float z = Mathf.Min(vp.z, Mathf.Max(0.15f, hv.z - 0.12f)); size *= z / vp.z; vp.z = z; }
                float fh = 2f * vp.z * tan, fw = fh * cam.aspect;
                float pw = (word.Length * 0.012f + 0.03f) * size * 1.45f / fw, ph = 0.036f * size * 1.75f / fh;     // half the burst, as a share of the frame
                if (headIn)
                {
                    // not on the duck's head either: beside it on the side it was meant for, else above it, else on the other side
                    float hw = 0.08f / (2f * hv.z * tan * cam.aspect), hh = 0.045f / (2f * hv.z * tan);
                    if (Mathf.Abs(vp.x - hv.x) < pw * 0.85f + hw && Mathf.Abs(vp.y - hv.y) < ph + hh)
                    {
                        float need = pw * 0.85f + hw, side = vp.x >= hv.x ? 1f : -1f, up = hv.y + ph + hh + 0.02f;
                        bool Fits(float x) => x >= pw + 0.03f && x <= 0.97f - pw;
                        if (Fits(hv.x + side * need)) vp.x = hv.x + side * need;
                        else if (up <= 0.96f - ph) vp.y = up;
                        else if (Fits(hv.x - side * need)) vp.x = hv.x - side * need;
                        else vp.x = hv.x + side * need;
                    }
                }
                // and all of it inside the frame (the bottom strip belongs to the captions)
                vp.x = pw < 0.47f ? Mathf.Clamp(vp.x, pw + 0.03f, 0.97f - pw) : 0.5f;
                vp.y = Mathf.Clamp(vp.y, Mathf.Min(ph + 0.2f, 0.5f), Mathf.Max(0.96f - ph, 0.5f));
                at = cam.ViewportToWorldPoint(vp);
            }
            ComicPop.Spawn(word, at, hex, size, life);
            var go = GameObject.Find("Pop " + word); if (go != null) go.name = "Film pop";
        }
        /// <summary>A point pulled inside the frame (sound words must not be cut off by its edge).</summary>
        static Vector3 Keep(Vector3 at)
        {
            var vp = cam.WorldToViewportPoint(at);
            if (vp.z <= 0.05f) return at;
            vp.x = Mathf.Clamp(vp.x, 0.22f, 0.76f); vp.y = Mathf.Clamp(vp.y, 0.24f, 0.77f);
            return cam.ViewportToWorldPoint(vp);
        }
        /// <summary>A word from the player's duck: beside its head as the lens sees it, and a little nearer the lens, so it is never hidden behind the head.</summary>
        static void Say(string word, string hex, float side = 1f, float size = 1f, float up = 0.3f, float life = 0.9f)
        {
            var toCam = cam.transform.position - Pos; toCam.y = 0f; toCam = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.zero;
            Pop(word, Pos + cam.transform.right * (0.2f * side) + V(0f, up, 0f) + toCam * 0.1f, hex, Mathf.Max(size, 1.15f), life);
        }
        static void NewDuck()
        {
            var b = FlockSpawner.Add();
            var old = GameObject.Find("Pop QUACK!"); if (old != null) { old.name = "dropped pop"; Object.Destroy(old); }
            if (b != null) Pop("QUACK!", b.transform.position + V(0f, 0.38f, 0f), "#FFC43D", 1.2f, 0.9f);
        }

        static GameObject Toy(string kind, Vector3 at, float yawDeg)
        {
            var go = ToyCatalog.Spawn(kind, at, yawDeg);
            if (go != null) filmToys.Add(go);
            return go;
        }
        /// <summary>Remember the toys that are out now; whatever the bag adds after this is cleared with the film's own toys.</summary>
        static void MarkToys() { toysBefore = new HashSet<ToyTag>(Object.FindObjectsByType<ToyTag>(FindObjectsSortMode.None)); }
        static void Clean()
        {
            foreach (var g in filmToys) if (g != null) { g.SetActive(false); Object.Destroy(g); }
            filmToys.Clear(); tower.Clear();
            if (toysBefore == null) return;
            foreach (var tt in Object.FindObjectsByType<ToyTag>(FindObjectsSortMode.None)) if (!toysBefore.Contains(tt)) { tt.gameObject.SetActive(false); Object.Destroy(tt.gameObject); }
            toysBefore = null;
        }

        /// <summary>Put away the small loose toys lying on the floor of a stage (they would trip the skates, sit in front of the lens
        /// or be bitten by mistake); they come back when the next shot is set up. Big props (teddy, beach ball) and things up on
        /// furniture stay where they are, and so does 'keep', e.g. the match ball.</summary>
        static void Sweep(Rect area, Rigidbody keep = null)
        {
            foreach (var rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (rb == keep || rb.isKinematic || filmToys.Contains(rb.gameObject)) continue;
                int layer = rb.gameObject.layer;
                if (layer != DuckSetup.LayerToy && layer != DuckSetup.LayerProp) continue;
                if (rb.position.y > 0.35f || !area.Contains(new Vector2(rb.position.x, rb.position.z))) continue;
                var col = rb.GetComponentInChildren<Collider>();
                if (col != null && col.bounds.extents.magnitude > 0.12f) continue;
                rb.gameObject.SetActive(false); parked.Add(rb.gameObject);
            }
        }
        static void Unpark() { foreach (var g in parked) if (g != null) g.SetActive(true); parked.Clear(); WorldPanel.HideAll = false; }
        /// <summary>Flowers stand taller than a duck: take the ones near a lawn stage out for one shot, so none blocks the lens.</summary>
        static void Weed(Vector3 centre, float radius)
        {
            foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!tr.name.StartsWith("Flower") || !tr.gameObject.activeSelf) continue;
                var d = tr.position - centre; d.y = 0f;
                if (d.magnitude < radius) { tr.gameObject.SetActive(false); parked.Add(tr.gameObject); }
            }
        }

        /// <summary>A toy to bite, put on the floor.</summary>
        static void Bait(string kind, float x, float z)
        {
            var go = Toy(kind, Ground(x, z), -90f);
            biteToy = go != null ? go.GetComponent<Grabbable>() : null;
        }
        /// <summary>Waddle up to the bait with the beak open and bite it; the bite cannot land before openAt + 0.6 s. True once held.</summary>
        static bool Chomp(float t, float openAt)
        {
            if (P.Held != null) { PlayerDuck.DebugBeak = 0f; return true; }
            var thing = biteToy != null ? biteToy.Centre : Pos + Fwd(P.Body.Yaw) * 0.1f;
            if (t < openAt)
            {
                PlayerDuck.DebugBeak = 1f;
                var d = thing - Pos; d.y = 0f;
                if (d.magnitude > 0.1f) PlayerDuck.DebugWalk = d.normalized; else Stop();
                return false;
            }
            return Grab(thing);
        }

        /// <summary>A 2 x 2 tower of foam cubes (bag toys), asleep until something hits it.</summary>
        static void Tower(float x, float z, int levels)
        {
            var vis = ToyCatalog.BuildVisual("foam_cube"); if (vis == null) return;
            var sz = ToyKit.LocalBounds(vis.transform).size; vis.SetActive(false); Object.Destroy(vis);
            var at = Ground(x, z);
            for (int k = 0; k < levels; k++)
                for (int i = 0; i < 4; i++)
                {
                    var p = at + V((i % 2 - 0.5f) * (sz.x + 0.001f), k * (sz.y + 0.0012f), (i / 2 - 0.5f) * (sz.z + 0.001f));
                    var go = Toy("foam_cube", p, 0f); if (go == null) continue;
                    var rb = go.GetComponent<Rigidbody>(); rb.Sleep(); tower.Add(rb);
                }
        }
        /// <summary>Metres the skating duck still has to roll (along +x) before its capsule touches the tower.</summary>
        static float TowerGap => TowerX - 0.073f - Pos.x;
        static void TowerHit()
        {
            Pop("POW!", V(TowerX + 0.04f, 0.36f, TowerZ), "#FF8A3D", 1.5f, 0.7f);
            if (SmashKick <= 0f) return;
            foreach (var rb in tower) if (rb != null) { rb.WakeUp(); rb.AddForce((Vector3.right + Vector3.up * 0.5f + Random.insideUnitSphere * 0.4f) * SmashKick, ForceMode.VelocityChange); }
        }

        /// <summary>Skates, arriving on a cue: roll along 'heading' so that the 'gap' (metres) closes in 'eta' game seconds.
        /// While the run-up is still off screen (t &lt; -0.25) it pushes when behind the schedule and coasts when ahead of
        /// it; on screen it simply pushes. Start the duck a little closer than flat out needs, so there is slack to coast.</summary>
        static void SkateOnCue(Vector3 heading, float gap, float eta, float t)
        {
            float lead = P.Skates.Speed * eta - gap;                    // metres ahead of the schedule
            if (lead > 0.015f) cuePush = false; else if (lead < -0.015f) cuePush = true;
            bool push = t >= -0.25f || gap <= 0f || eta <= 0.02f || cuePush;
            PlayerDuck.DebugWalk = push ? heading : (Vector3?)null;
        }
        static bool cuePush = true;

        /// <summary>The laser dot (the flock chases it), with the film's wider disc under it.</summary>
        static void Dot(Vector3 p)
        {
            LaserPointer.DebugPoint = p;
            if (HaloRadius <= 0f) return;
            if (halo == null)
            {
                halo = ToyKit.Cyl(null, Vector3.zero, HaloRadius, 0.0005f, "#FF5D73"); halo.name = "Film laser halo";
                var m = ComicLook.ToonMaterial(ComicLook.Hex("#FF5D73"), "film_laser_halo"); m.SetFloat("_Unlit", 1f);
                var mr = halo.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            halo.gameObject.SetActive(true); halo.position = Ground(p.x, p.z) + V(0f, 0.0004f, 0f);
        }
        static void HideDot() { if (halo != null) halo.gameObject.SetActive(false); }
        /// <summary>The flock in a loose ring round a point, at growing distances so they do not all arrive (and pounce) at once.</summary>
        static void Ring(Vector3 c, float r0, float step)
        {
            for (int i = 0; i < DuckBrain.All.Count && i < 8; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad + (i / 6) * 0.5f, r = r0 + step * i;
                MoveDuck(i, c + V(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.85f), Mathf.Atan2(-Mathf.Cos(a), -Mathf.Sin(a)) * Mathf.Rad2Deg);
            }
        }

        /// <summary>The flock out of the way (to the bedroom) for a shot of the player's duck alone.</summary>
        static void Shoo() { for (int i = 0; i < DuckBrain.All.Count; i++) MoveDuck(i, V(5.2f + 0.2f * (i % 4), 0f, -0.9f - 0.22f * (i / 4)), 0f); }
        static void FlockEmote(string kind) { foreach (var d in DuckBrain.All) if (d.Body != null) d.Body.PlayEmote(kind); }
        /// <summary>Send home the ducks earlier takes added, so every take starts with the same flock.</summary>
        static void SendHome()
        {
            int n = 0;
            foreach (var d in DuckBrain.All) if (d.name.StartsWith("Duck +")) n++;
            for (int i = 0; i < n; i++) FlockSpawner.Remove();
        }
        /// <summary>Everybody on the lawn, the player in front, the flock in a V behind, all facing the house.</summary>
        static void Party()
        {
            Place(PartyAt, 180f);
            for (int i = 0; i < DuckBrain.All.Count; i++) { int k = i / 2 + 1; float sx = i % 2 == 0 ? -1f : 1f; MoveDuck(i, PartyAt + V(sx * k * 0.17f, 0f, 0.05f + k * 0.12f), 180f); }
        }

        static Vector3 BagSpot(int i)
        {
            var f = Fwd(BagYaw * Mathf.Deg2Rad); var s = new Vector3(f.z, 0f, -f.x);
            var w = Pos + f * bagSpots[i].x + s * bagSpots[i].y;
            return Ground(w.x, w.z);
        }

        /// <summary>Football, staged for one play: the ball ballZ metres up the pitch from the centre spot (towards the goal
        /// orange shoots at), the player right behind it, orange team-mates behind on the right, blue sent to the far end.</summary>
        static bool Kickoff(float ballZ)
        {
            var fg = FootballGame.Ensure(); fg.StartGame();
            if (!FootballGame.Active || fg.Ball == null) return false;
            Sweep(PitchArea, fg.Ball);
            var c = FootballGame.Centre; int nb = 0, no = 0;
            foreach (var kv in fg.Team)
            {
                if (kv.Value == 1) { kv.Key.Teleport(c + V(-0.4f + 0.25f * nb, 0f, -1.0f), 0f); nb++; }
                else { kv.Key.Teleport(c + V(0.45f + 0.14f * (no % 2), 0f, ballZ - 0.5f - 0.14f * no), 0f); no++; }
            }
            var ball = fg.Ball; ball.linearVelocity = Vector3.zero; ball.angularVelocity = Vector3.zero;
            ball.position = c + V(0f, 0.045f, ballZ); ball.transform.position = ball.position;
            Place(c + V(0f, 0f, ballZ - 0.125f), 0f);
            return true;
        }
        static Vector3 BallPos => FootballGame.Instance != null && FootballGame.Instance.Ball != null ? FootballGame.Instance.Ball.position : FootballGame.Centre;

        /// <summary>Skating a loop on the kitchen tiles, drifting once there is speed ('since' = seconds since the shot was set up).</summary>
        static void DriftLap(float since)
        {
            float a = since * 1.25f;
            var d = DriftC + V(Mathf.Cos(a) * 0.75f, 0f, Mathf.Sin(a) * 0.5f) - Pos; d.y = 0f;
            PlayerDuck.DebugWalk = d.normalized;
            PlayerSkates.DebugDrift = since > 2.5f ? 1f : (float?)null;
        }
        /// <summary>Which way the skating duck is really going (it slides, so not where it faces).</summary>
        static Vector3 Heading()
        {
            var v = P.Skates.Velocity; v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Fwd(P.Body.Yaw);
        }

        // ------------------------------------------------------------------ the shots
        static List<Shot> TrailerShots()
        {
            var L = new List<Shot>();
            buildLook = "cream";

            // ================================================================ COLD OPEN  0 - 6
            // 0:00  the race start, on skates, straight at the lens: GO has just gone, the boost fires, the pack is left behind
            L.Add(Cut("hook_zoom", 2f, 3.9f,
                () => { SendHome(); Sweep(HallArea); Place(V(2.54f, 0f, 3.06f), 270f); },
                t =>
                {
                    At(t, -3.7f, "race", () => { RaceGame.Ensure().StartRace(); Skates(true); });       // a frame after SendHome: the grid must not hold ducks that are leaving
                    if (RaceGame.Active && !RaceGame.Frozen) PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 0.25f, "boost", () => { PlayerSkates.DebugBoost = 1f; Say("ZOOM!", "#3A86FF", 1f, 1.8f); });
                    At(t, 0.6f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    Soft(Pos + V(-Lerp(0.75f, 0.48f, U), 0.1f, 0.08f), Pos + V(0f, 0.16f, 0f), 60f, 0.12f);
                }));

            // 0:02  drift on the kitchen tiles, slow motion, from the inside of the turn: sparks, smoke and skid marks, the kitchen towering behind
            L.Add(Cut("hook_drift", 2f, 3.2f,
                () => { Sweep(KitchenArea); Place(V(-5.6f, 0f, -1.0f), 90f); Skates(true); },
                t =>
                {
                    DriftLap(t + 3.2f);
                    if (t >= 0f) Slow = 2f;
                    var f = Heading(); var s = new Vector3(f.z, 0f, -f.x);
                    Soft(Pos - s * 0.55f + f * 0.15f + V(0f, 0.1f, 0f), Pos + V(0f, 0.15f, 0f), 56f, 0.2f);
                }));

            // 0:04  it waddles up to the lens, stops, QUACK! - and a beat of silence before the title
            L.Add(Cut("hook_quack", 2f, 0.5f,
                () =>
                {
                    Sweep(LawnArea); Weed(Lawn + V(0f, 0f, -0.3f), 1.5f); Place(Lawn + V(0f, 0f, 0.14f), 180f);
                    for (int i = 0; i < 4; i++) { int k = i / 2 + 1; MoveDuck(i, Lawn + V((i % 2 == 0 ? -1f : 1f) * k * 0.22f, 0f, 0.35f + k * 0.18f), 180f); }
                },
                t =>
                {
                    Walk(Lawn, 0.02f);
                    At(t, 1.0f, "q", () => { PlayerDuck.DebugChirp = true; Say("QUACK!", "#FFC43D"); });
                    Look(Lawn + V(0.06f, 0.3f, -1.0f), Lawn + V(0f, 0.17f, 0f), 30f); fstop = 2.8f;
                }));

            // (0:06 - 0:08 is the title sting, a graphic)

            // ================================================================ INTRO  8 - 24
            // 0:08  you are small: a wide lens on the floor tracks the duck past table legs and the cardboard ramp
            L.Add(Cut("tiny", 4f, 0.8f,
                () => { Sweep(FloorArea); Place(V(-0.38f, 0f, 1.0f), 270f); MoveDuck(0, V(-0.85f, 0f, 0.62f), 30f); MoveDuck(1, V(-1.25f, 0f, 0.7f), 330f); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    Soft(Pos + V(-0.08f, 0.09f, 0.5f), Pos + V(-0.06f, 0.13f, 0f), 68f, 0.2f); fstop = 5.6f;
                }, false, "YOU ARE A TINY DUCK"));

            // 0:12  through its eyes: in under the table, ducks looking up as you pass
            L.Add(Cut("pov_waddle", 2f, 1.7f,
                () => { Sweep(TableArea); Shoo(); Place(V(0.42f, 0f, 1.2f), 190f); MoveDuck(0, V(-0.04f, 0f, 0.42f), 70f); MoveDuck(1, V(0.68f, 0f, 0.3f), 300f); MoveDuck(2, V(0.22f, 0f, -0.5f), 10f); },
                t =>
                {
                    PlayerDuck.DebugWalk = Fwd(190f * Mathf.Deg2Rad);
                    At(t, 0.2f, "nod", () => FlockEmote("nod"));
                    Eyes(190f + 5f * Mathf.Sin(t * 0.9f), -3f, 88f);
                }, true));

            // 0:14  the duck comes straight at the lens, the flock behind it
            L.Add(Cut("front", 2f, 1.0f,
                () =>
                {
                    Sweep(FloorArea); Place(V(-1.7f, 0f, 1.0f), 90f);
                    for (int i = 0; i < 6; i++) { int k = i / 2 + 1; MoveDuck(i, V(-1.7f - k * 0.2f, 0f, 1.0f + (i % 2 == 0 ? -1f : 1f) * k * 0.13f), 90f); }
                },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.right;
                    Soft(Pos + V(0.62f, 0.07f, 0.06f), Pos + V(0f, 0.17f, 0f), 36f, 0.2f); fstop = 2.8f;
                }));

            // 0:16  the beak: open, reach, CHOMP on the second beat
            L.Add(Cut("grab", 2f, 1.0f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-1.32f, 0f, 1.0f), 90f); Bait("toy_duck", -1.1f, 1.0f); },
                t =>
                {
                    if (Chomp(t, -0.1f))
                    {
                        if (fired.Add("got")) { biteAt = t; Say("CHOMP!", "#FF5D73", -1f, 0.85f, 0.27f); }
                        if (t > biteAt + 0.8f) PlayerDuck.DebugWalk = Vector3.right;
                    }
                    Soft(Pos + V(0.1f, 0.12f, 0.62f), Pos + V(0.08f, 0.13f, 0f), 36f, 0.3f); fstop = 2.8f;
                }));

            // 0:18  ... and off it goes with the rubber duck in its beak, the head in close-up
            L.Add(Cut("carry", 2f, 2.4f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-1.5f, 0f, 1.0f), 90f); Bait("toy_duck", -1.28f, 1.0f); },
                t =>
                {
                    if (Chomp(t, -2.2f)) PlayerDuck.DebugWalk = Vector3.right;
                    Soft(Pos + V(0.16f, 0.19f, 0.6f), Pos + V(0.1f, 0.19f, 0f), 30f, 0.15f); fstop = 2.8f;
                }));

            // 0:20  the flock: QUACK! - and they all nod
            L.Add(Cut("flock", 2f, 0.5f,
                () =>
                {
                    Sweep(FloorArea); Place(V(-1.0f, 0f, 0.75f), 0f);
                    for (int i = 0; i < 6; i++) { int k = i / 2 + 1; MoveDuck(i, V(-1.0f + (i % 2 == 0 ? -1f : 1f) * k * 0.19f, 0f, 0.75f - k * 0.14f), 0f); }
                },
                t =>
                {
                    At(t, 0.4f, "q", () => { PlayerDuck.DebugChirp = true; Say("QUACK!", "#FFC43D"); });
                    At(t, 1.1f, "nod", () => FlockEmote("nod"));
                    Look(V(-0.96f, 0.1f, 0.75f + Lerp(1.05f, 0.95f, U)), V(-1.0f, 0.16f, 0.6f), 38f);
                }));

            // 0:22  everybody dances: three faces, close
            L.Add(Cut("dance", 2f, 0.7f,
                () =>
                {
                    Sweep(FloorArea); Place(V(-1.0f, 0f, 0.78f), 0f);
                    MoveDuck(0, V(-1.24f, 0f, 0.7f), 0f); MoveDuck(1, V(-0.76f, 0f, 0.7f), 0f);
                    MoveDuck(2, V(-1.46f, 0f, 0.5f), 0f); MoveDuck(3, V(-0.54f, 0f, 0.5f), 0f); MoveDuck(4, V(-1.12f, 0f, 0.42f), 0f); MoveDuck(5, V(-0.88f, 0f, 0.42f), 0f);
                },
                t =>
                {
                    At(t, -0.45f, "dance", () => AllEmote("dance"));
                    Look(V(-1.0f + Lerp(-0.14f, 0.14f, U), 0.09f, 1.5f), V(-1.0f, 0.16f, 0.74f), 32f); fstop = 2.8f;
                }));

            // ================================================================ ESCALATION  24 - 48
            // 0:24  the toy bag, first person: it opens, the pointer slides to a ball, pick - and the first one lands on the beat
            L.Add(Cut("bag_open", 2f, 0.9f,
                () => { Sweep(FloorArea); MarkToys(); Place(V(-1.45f, 0f, 0.75f), BagYaw); },
                t =>
                {
                    var bag = Bag; var eye = P.cam.transform.position;
                    At(t, -0.6f, "open", () => bag.Open());
                    if (t > -0.4f) bag.SetDebugAim(new Ray(eye, Vector3.Lerp(bag.TileWorld("help"), bag.TileWorld("wicker_ball"), E((t + 0.4f) / 1.9f)) - eye));
                    Eyes(BagYaw, 3f, Lerp(62f, 52f, U));
                }, true));

            // 0:26  ... and three more, one on every beat: from outside now, the duck and its toys in one picture
            L.Add(Cut("bag_rain", 2f, 0.5f,
                () => { Sweep(FloorArea); Shoo(); MarkToys(); Place(V(-1.45f, 0f, 0.75f), BagYaw); Toy("wicker_ball", BagSpot(0), 0f); BagMenu.HideBeam = true; },
                t =>
                {
                    var bag = Bag; var eye = P.cam.transform.position;
                    At(t, 0.05f, "k1", () => bag.Choose("toy_car")); At(t, 0.5f, "p1", () => { bag.DebugPress(); Pop("POP!", BagSpot(1) + V(0f, 0.13f, 0f), "#FFC43D", 1.0f, 0.45f); });
                    At(t, 0.55f, "k2", () => bag.Choose("sock_ball")); At(t, 1.0f, "p2", () => { bag.DebugPress(); Pop("POP!", BagSpot(2) + V(0f, 0.13f, 0f), "#FF5D73", 1.0f, 0.45f); });
                    At(t, 1.05f, "k3", () => bag.Choose("toy_car")); At(t, 1.5f, "p3", () => { bag.DebugPress(); Pop("POP!", BagSpot(3) + V(0f, 0.13f, 0f), "#3A86FF", 1.0f, 0.45f); });
                    int spot = t < 0.55f ? 1 : t < 1.05f ? 2 : 3;
                    if (t > 0f) bag.SetDebugAim(new Ray(eye, BagSpot(spot) + V(0f, 0.01f, 0f) - eye));
                    var f = Fwd(BagYaw * Mathf.Deg2Rad); var side = new Vector3(f.z, 0f, -f.x);
                    Look(Pos + f * 0.3f - side * 0.66f + V(0f, 0.08f, 0f), Pos + f * 0.2f + V(0f, 0.12f, 0f), 44f); fstop = 4f;
                }, false, "A WHOLE BAG OF TOYS"));

            // 0:28  skates: they snap on (second beat), the first push throws up smoke
            L.Add(Cut("skates_on", 2f, 0.4f,
                () => { Sweep(HallArea); Shoo(); Place(V(4.0f, 0f, HallZ), 270f); },
                t =>
                {
                    At(t, 0.12f, "on", () => { Skates(true); Pop("CLICK!", Pos + V(-0.16f, 0.13f, 0.1f), "#FFC43D", 0.5f, 0.55f); });
                    if (t > 0.55f) PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 1.0f, "boost", () => PlayerSkates.DebugBoost = 1f); At(t, 1.5f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    // at the feet: the skates snap on, one push, and the duck is gone out of the frame
                    Soft(Pos + V(-0.1f, 0.05f, 0.42f), Pos + V(-0.02f, 0.065f, 0f), 34f, t < 0.55f ? 0.02f : 0.5f); fstop = 2.8f;
                }));

            // 0:30  boost: tracking alongside down the hallway, ZOOM! on the second beat, the doorway whips by
            L.Add(Cut("boost", 2f, 2.8f,
                () => { Sweep(HallArea); Place(V(1.6f, 0f, HallZ), 270f); Skates(true); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 0.5f, "boost", () => { PlayerSkates.DebugBoost = 1f; Say("ZOOM!", "#3A86FF", 1f, 1.1f); });
                    At(t, 0.9f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    Soft(Pos + V(-0.08f, 0.1f, 0.5f), Pos + V(-0.05f, 0.15f, 0f), 60f, 0.15f);
                }));

            // 0:32  the block tower: skate into it, slow motion from just before the hit, POW!
            L.Add(Cut("smash", 2f, 2.0f,
                () => { Sweep(FloorArea); Place(V(-1.24f, 0f, TowerZ), 90f); Tower(TowerX, TowerZ, TowerLevels); Skates(true); },
                t =>
                {
                    float gap = TowerGap;
                    SkateOnCue(Vector3.right, gap, 0.45f - t, t);
                    if (t >= 0f && gap < 0.06f) Slow = 3f;
                    if (gap < 0.005f && fired.Add("pow")) TowerHit();
                    float cx = Mathf.Min(Pos.x + 0.52f, TowerX + 0.5f);
                    Soft(V(cx, 0.1f, TowerZ + 0.5f), V(cx - 0.5f, 0.13f, TowerZ), 44f, 0.12f);
                }));

            // 0:34  the laser dot slides across the floor and the flock goes after it; the camera tracks with the dot
            L.Add(Cut("laser_dot", 2f, 2.6f,
                () =>
                {
                    Sweep(FloorArea); Place(V(-0.1f, 0f, 1.95f), 250f);
                    // the flock starts in two rows behind the dot and runs after it, straight at the lens
                    for (int i = 0; i < DuckBrain.All.Count && i < 8; i++) MoveDuck(i, V(-1.86f - 0.16f * (i / 3), 0f, 1.0f + ((i % 3) - 1) * 0.2f), 90f);
                },
                t =>
                {
                    var dot = V(-0.95f + 0.13f * t, 0f, 1.0f);
                    Dot(dot);
                    Look(V(-0.3f, 0.06f, 1.08f), dot + V(-0.25f, 0.1f, 0f), 38f); Focus(dot + V(-0.12f, 0.05f, 0f), 4f);
                }));

            // 0:36  the dot stops: a ring of ducks stare at it and pounce, one after another - seen from above, the dot in the middle
            L.Add(Cut("laser_pounce", 2f, 2.6f,
                () => { Sweep(FloorArea); Place(V(-0.1f, 0f, 1.95f), 250f); Ring(LaserAt, 0.25f, 0.07f); },
                t =>
                {
                    Dot(LaserAt);
                    Look(LaserAt + V(0.04f, Lerp(0.62f, 0.5f, U), 0.4f), LaserAt + V(0f, 0.06f, 0f), 40f); fstop = 4f;
                }));

            // 0:38  football: kick-off, low on the touchline, both teams closing on the ball
            L.Add(Cut("fb_dribble", 2f, 1.6f,
                () => { var fg = FootballGame.Ensure(); fg.StartGame(); Sweep(PitchArea, fg.Ball); LensClear = true; },
                t =>
                {
                    var ball = BallPos; var c = FootballGame.Centre;
                    Walk(ball + V(0f, 0f, -0.02f), 0.01f);
                    Soft(V(c.x - 0.8f, 0.15f, ball.z + 0.05f), V(c.x, 0.12f, ball.z + 0.03f), 42f, 0.3f); fstop = 8f;
                }));

            // 0:40  the goal: over the duck's shoulder, the ball rolls over the line in slow motion, GOAL!, the team dances
            L.Add(Cut("fb_goal", 3f, 0.4f,
                () => { Kickoff(1.08f); },
                t =>
                {
                    var fg = FootballGame.Instance; var c = FootballGame.Centre;
                    bool live = FootballGame.Active && fg != null;
                    bool scored = live && fg.Score[0] > 0;
                    if (live && !scored) Walk(fg.GoalCentre(1), 0.02f); else Stop();
                    if (t >= 0f) Slow = scored ? 1.5f : BallPos.z - c.z > FootballGame.Half - 0.06f ? 3f : 1f;
                    if (scored && fired.Add("goal")) Pop("GOAL!", c + V(0f, 0.3f, FootballGame.Half - 0.12f), "#FF9F0A", 1.5f, 1.6f);
                    Look(c + V(-0.66f, 0.08f, FootballGame.Half - Lerp(0.3f, 0.2f, U)), c + V(0f, 0.11f, FootballGame.Half - 0.1f), 40f); Focus(BallPos, 2.8f); LensClear = true;
                }));

            // 0:43  the cheer, one bar: the scorers dance in front of the goal
            L.Add(Cut("fb_cheer", 1f, 0.6f,
                () =>
                {
                    var c = FootballGame.Centre; var fg = FootballGame.Instance;
                    Sweep(PitchArea, fg != null ? fg.Ball : null); Shoo();
                    Place(c + V(0f, 0f, 0.95f), 180f);
                    MoveDuck(0, c + V(-0.22f, 0f, 1.08f), 180f); MoveDuck(1, c + V(0.22f, 0f, 1.08f), 180f); MoveDuck(2, c + V(0f, 0f, 1.17f), 180f);
                },
                t =>
                {
                    var c = FootballGame.Centre;
                    At(t, -0.45f, "dance", () => AllEmote("dance"));
                    At(t, 0f, "goal", () => Pop("GOAL!", FootballGame.Centre + V(0f, 0.42f, 1.0f), "#FF9F0A", 1.7f, 1.2f));
                    Look(c + V(0.05f, 0.12f, -0.05f), c + V(0f, 0.2f, 0.95f), 34f); fstop = 2.8f;
                }));

            // 0:44  the race: the grid seen through the start arch, GO! on the second beat, the pack bursts at the lens
            L.Add(Cut("race_go", 2f, 2.5f,
                () => { Sweep(HallArea); RaceGame.Ensure().StartRace(); Skates(true); },
                t =>
                {
                    if (RaceGame.Active && !RaceGame.Frozen) PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 0.5f, "go", () => { Pop("GO!", Pos + V(0f, 0.3f, 0.26f), "#8FD35A", 1.9f, 0.9f); PlayerSkates.DebugBoost = 1f; });
                    At(t, 1.0f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    // backing away down the hallway just ahead of the leader: on GO! it bursts at the lens
                    Soft(Pos + V(-0.78f, 0.09f, -0.2f), Pos + V(0.05f, 0.16f, 0f), 42f, 0.3f); fstop = 4f;
                }));

            // 0:46  through the next gate, straight over the camera
            L.Add(Cut("race_gate", 2f, 3.05f,
                () =>
                {
                    Sweep(HallArea); RaceGame.Ensure().StartRace();
                    Place(V(-1.9f, 0f, HallZ), 270f);
                    for (int i = 0; i < DuckBrain.All.Count; i++) MoveDuck(i, V(-1.7f + 0.2f * (i / 2), 0f, HallZ + (i % 2 == 0 ? -0.2f : 0.2f)), 270f);
                },
                t =>
                {
                    var tgt = RaceGame.Ensure().PlayerTarget(); if (tgt.HasValue) Walk(tgt.Value, 0.02f);
                    Look(V(-3.35f, 0.1f, 3.2f), V(-2.5f, 0.15f, HallZ), 46f);
                }));

            // ================================================================ BREATHER  48 - 56  (the mirror; a new colour on the snaps)
            // 0:48  the music drops: the duck walks up to a mirror as tall as a house
            L.Add(Cut("mirror_walk", 2f, 0.8f,
                () => { Sweep(BedroomArea); Place(V(4.36f, 0f, -1.2f), 180f); },
                t =>
                {
                    Walk(V(4.4f, 0f, -1.72f), 0.03f);
                    Soft(Pos + V(-0.16f, 0.09f, 0.42f), Pos + V(0.03f, 0.27f, -0.4f), 54f, 0.25f); fstop = 5.6f;
                }));

            // 0:50  first person: that is you in the glass - a nod, and on the bar the first new colour
            L.Add(Cut("mirror_pov", 2f, 0.6f,
                () => { Sweep(BedroomArea); Shoo(); Place(MirrorStation.StandPos + V(0f, 0f, -0.22f), 180f); WorldPanel.HideAll = true; },
                t =>
                {
                    At(t, 0.2f, "nod", () => Emote("nod"));
                    At(t, 1.0f, "lavender", () => Wear("lavender"));
                    // the reflection stands as far behind the glass (z = -2.408) as the duck stands in front of it
                    Look(P.cam.transform.position, V(4.42f, Pos.y + 0.14f, -4.816f - Pos.z), 40f);
                    Focus(V(4.42f, 0.14f, -2.4f), 11f);
                }, true));

            // 0:52  the duck itself, close and sharp: graphite, two-tone, sky - one look a bar, then a dance
            L.Add(Cut("mirror_hero", 4f, 0.5f,
                () => { Sweep(BedroomArea); Place(MirrorStation.StandPos, 180f); Wear("lavender"); WorldPanel.HideAll = true; },
                t =>
                {
                    At(t, 0.3f, "look", () => Emote("look"));
                    At(t, 1.0f, "graphite", () => Wear("graphite"));
                    At(t, 2.0f, "twotone", () => Wear("cream", "graphite", "", "", "graphite"));
                    At(t, 3.0f, "sky", () => { Wear("sky"); Emote("dance"); });
                    // from the toy-box side (the block ramp rises on the other), swinging round to the front
                    float a = Mathf.Lerp(50f, 12f, E(U)) * Mathf.Deg2Rad;
                    Look(Pos + V(-Mathf.Sin(a) * 0.5f, 0.1f, -Mathf.Cos(a) * 0.5f), Pos + V(0f, 0.1f, 0f), 50f); fstop = 2.8f;
                }, false, "MAKE IT YOUR DUCK"));

            buildLook = "sky";

            // ================================================================ CLIMAX  56 - 72
            // 0:56  more ducks: one pops in (QUACK!) at the top of the bar, another on the next beat
            L.Add(Cut("c_newduck", 1f, 0.6f,
                () => { Sweep(LawnArea); Weed(PartyAt + V(0f, 0f, -0.2f), 1.7f); Party(); },
                t =>
                {
                    At(t, 0.05f, "a", () => NewDuck());
                    At(t, 0.5f, "b", () => NewDuck());
                    Look(PartyAt + V(-0.22f, 0.34f, -0.95f), PartyAt + V(0f, 0.14f, 0.1f), 44f); fstop = 5.6f;
                }, false, "BRING THE WHOLE FLOCK"));

            // 0:57  through the lawn: the grass parts round the duck, clippings fly
            L.Add(Cut("c_grass", 1f, 1.0f,
                () => { Sweep(LawnArea); Weed(Lawn + V(0f, 0f, -0.2f), 1.7f); Shoo(); Place(Lawn + V(-0.5f, 0f, 0.25f), 90f); MoveDuck(0, Lawn + V(-0.82f, 0f, 0.45f), 90f); MoveDuck(1, Lawn + V(-1.1f, 0f, 0.62f), 90f); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.right;
                    Soft(Pos + V(0.12f, 0.3f, -0.85f), Pos + V(-0.1f, 0.12f, 0f), 34f, 0.2f); Focus(P.Body.EyePosition, 4f); LensClear = true;
                }));

            // 0:58  the toy train steams past, the duck watching it go by
            L.Add(Cut("c_train", 1f, 0.5f,
                () =>
                {
                    Sweep(BedroomArea);
                    var tr = Object.FindAnyObjectByType<ToyTrain>();
                    trainC = tr != null ? tr.centre : V(4.95f, 0f, 0.9f); trainR = tr != null ? tr.radius : 0.55f;
                    var eng = tr != null && tr.cars != null && tr.cars.Length > 0 ? tr.cars[0].position : trainC + V(trainR, 0f, 0f);
                    // where the engine will be half way through the shot (its angle round the track grows as it runs)
                    float a = Mathf.Atan2(eng.z - trainC.z, eng.x - trainC.x) + (tr != null ? tr.speed : 0.11f) * 1.0f / trainR + 0.05f;
                    trainU = V(Mathf.Cos(a), 0f, Mathf.Sin(a)); trainT = V(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    // inside the loop, on the lens's side of the track and to one side: the duck in profile, the train steaming past behind it
                    var at = trainC + trainU * (trainR - 0.22f) - trainT * 0.13f;
                    Shoo(); Place(V(at.x, 0f, at.z), Mathf.Atan2(trainT.x, trainT.z) * Mathf.Rad2Deg);
                },
                t =>
                {
                    At(t, 0.1f, "look", () => Emote("look"));
                    // from inside the track (the rug there is always clear): the train crosses the foreground, the duck faces the lens behind it
                    Look(trainC + trainU * (trainR - 0.62f) + trainT * 0.1f + V(0f, 0.12f, 0f), trainC + trainU * (trainR + 0.1f) + V(0f, 0.13f, 0f), 46f); fstop = 5.6f;
                }));

            // 0:59  the garage turntable: REPLACE - the old head shell pops off and flies
            L.Add(Cut("c_garage", 1f, 1.3f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-2.2f, 0f, 1.78f), 100f); WorldPanel.HideAll = true; },
                t =>
                {
                    var g = Garage.Instance;
                    if (g != null)
                    {
                        At(t, -0.8f, "row", () => Click(g.Board, "row_0")); At(t, -0.7f, "rowr", () => WorldPanel.DebugPoint = null);
                        At(t, -0.05f, "rep", () => Click(g.Board, "replace")); At(t, 0.05f, "repr", () => WorldPanel.DebugPoint = null);
                    }
                    wear.ApplyTo(P.gameObject, false);            // the garage flashes the part and then restores the saved look: keep the film's
                    At(t, 0.2f, "new", () => Pop("LIKE NEW!", V(-2.25f, 0.34f, 2.1f), "#FF5D73", 1.25f, 0.78f));
                    // the duck left of centre: the sign whole on its left, room for the word on its right
                    Look(V(-1.55f, 0.12f, 1.45f), V(-2.2f, 0.2f, 1.96f), 46f); Focus(P.Body.EyePosition, 4f);
                }));

            // 1:00  to the puddle: head-on, the camera backing away over the water
            L.Add(Cut("c_puddle", 1f, 0.5f,
                () => { Sweep(LawnArea); Place(V(0.15f, 0f, 5.2f), 270f); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    Soft(Pos + V(-0.9f, 0.12f, 0.1f), Pos + V(0f, 0.15f, 0f), 36f, 0.2f);
                }));

            // 1:01  SPLASH! - in it goes, slow motion from the first wet step, the ducks already in the puddle dance
            L.Add(Cut("c_splash", 2f, 1.0f,
                () =>
                {
                    Sweep(LawnArea); Place(V(PuddleEdgeX + 0.21f, 0f, 5.2f), 270f);
                    MoveDuck(0, V(-0.95f, 0f, 4.88f), 40f); MoveDuck(1, V(-0.78f, 0f, 5.0f), 20f);
                },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    var st = SurfaceTag.At(Pos);
                    bool inWater = st != null && st.type == "water";
                    if (inWater && fired.Add("wet")) { Say("SPLASH!", "#3A86FF", 1f, 1.3f); FlockEmote("dance"); ComicSplash.Spawn(Pos + V(-0.02f, 0f, 0f), 1.5f); splashT = 0.3f; }
                    // every step in the puddle throws up more water (the game does this itself; its sound-word watcher is off while filming)
                    if (inWater && fired.Contains("wet") && (splashT -= Dt) <= 0f) { splashT = 0.3f; ComicSplash.Spawn(Pos + V(-0.03f, 0f, 0f), 1.1f); }
                    if (t >= 0f && fired.Contains("wet")) Slow = 3f;
                    Look(V(-1.25f, 0.1f, 5.62f), V(-0.72f, 0.16f, 5.2f), 40f);
                }));

            // 1:03  boost again, the lens just ahead of the skates
            L.Add(Cut("c_skate", 1f, 2.2f,
                () => { Sweep(HallArea); Shoo(); Place(V(1.7f, 0f, HallZ), 270f); Skates(true); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 0.1f, "boost", () => { PlayerSkates.DebugBoost = 1f; Pop("ZOOM!", Pos + V(-0.3f, 0.2f, 0.17f), "#3A86FF", 1.0f, 0.8f); });
                    At(t, 0.5f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    // right behind the skates, low: the hallway rushes at the lens
                    Soft(Pos + V(0.46f, 0.17f, -0.08f), Pos + V(-0.5f, 0.1f, 0f), 62f, 0.1f); fstop = 5.6f;
                }));

            // 1:04  the drift again, from just ahead: the duck slides at the lens, marks and sparks behind it
            L.Add(Cut("c_drift", 1f, 3.6f,
                () => { Sweep(KitchenArea); Place(V(-5.6f, 0f, -1.0f), 90f); Skates(true); },
                t =>
                {
                    DriftLap(t + 3.6f);
                    var f = Heading(); var s = new Vector3(f.z, 0f, -f.x);
                    Soft(Pos + f * 0.48f - s * 0.1f + V(0f, 0.1f, 0f), Pos + V(0f, 0.15f, 0f), 60f, 0.15f);
                }));

            // 1:05  the race at full pace, tracking alongside the pack
            L.Add(Cut("c_smash", 1f, 2.0f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-1.24f, 0f, TowerZ), 90f); Tower(TowerX, TowerZ, TowerLevels); Skates(true); },
                t =>
                {
                    float gap = TowerGap;
                    SkateOnCue(Vector3.right, gap, 0.25f - t, t);
                    if (gap < 0.005f && fired.Add("pow")) TowerHit();
                    Look(V(TowerX + 0.5f, 0.07f, TowerZ + 0.36f), V(TowerX - 0.02f, 0.12f, TowerZ), 42f); fstop = 4f;
                }));

            // 1:06  football again: the duck brings the ball straight at the lens, team-mates on its heels
            L.Add(Cut("c_kick", 1f, 0.9f,
                () => { Kickoff(-0.2f); },
                t =>
                {
                    var ball = BallPos; var c = FootballGame.Centre;
                    if (FootballGame.Active) Walk(FootballGame.Instance.GoalCentre(1), 0.02f);
                    Soft(V(c.x + 0.1f, 0.1f, ball.z + 0.75f), V(c.x, 0.13f, ball.z - 0.08f), 40f, 0.2f);
                }));

            // 1:07  CHOMP! - a strawberry, from the front
            L.Add(Cut("c_chomp", 1f, 1.75f,
                () => { Sweep(FloorArea); Place(V(-1.22f, 0f, 1.0f), 90f); Bait("strawberry", -1.0f, 1.0f); },
                t =>
                {
                    if (Chomp(t, -0.6f) && fired.Add("got")) Say("CHOMP!", "#FF5D73", 1f, 0.85f);
                    Look(V(-0.45f, 0.12f, 1.38f), V(-1.06f, 0.13f, 1.0f), 36f); fstop = 2.8f;
                }));

            // ---------------------------------------------------------------- 68 - 70: four half-second cuts
            // 1:08  skates, wheels and smoke
            L.Add(Cut("m_skate", 0.5f, 3.0f,
                () => { Sweep(HallArea); Place(V(-2.0f, 0f, HallZ), 270f); Skates(true); },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 0.03f, "boost", () => PlayerSkates.DebugBoost = 1f); At(t, 0.4f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    Soft(Pos + V(-0.06f, 0.06f, 0.5f), Pos + V(-0.03f, 0.07f, 0f), 30f, 0.08f); fstop = 2.8f;
                }));

            // 1:08.5  the flock on the dot
            L.Add(Cut("m_pounce", 0.5f, 2.9f,
                () => { Sweep(FloorArea); Place(V(-0.1f, 0f, 1.95f), 250f); Ring(LaserAt, 0.25f, 0.07f); },
                t =>
                {
                    Dot(LaserAt);
                    Look(LaserAt + V(-0.12f, 0.1f, 0.6f), LaserAt + V(0f, 0.12f, 0f), 36f); fstop = 2.8f;
                }));

            // 1:09  running off with the rubber duck, the head in close-up
            L.Add(Cut("m_train", 0.5f, 0.4f,
                () =>
                {
                    Sweep(BedroomArea); Shoo();
                    var tr = Object.FindAnyObjectByType<ToyTrain>();
                    trainC = tr != null ? tr.centre : V(4.95f, 0f, 0.9f); trainR = tr != null ? tr.radius : 0.55f;
                    var eng = tr != null && tr.cars != null && tr.cars.Length > 0 ? tr.cars[0].position : trainC + V(trainR, 0f, 0f);
                    float a = Mathf.Atan2(eng.z - trainC.z, eng.x - trainC.x) + (tr != null ? tr.speed : 0.11f) * 0.65f / trainR;
                    trainU = V(Mathf.Cos(a), 0f, Mathf.Sin(a)); trainT = V(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                    Place(V(trainC.x, 0f, trainC.z), 0f);
                },
                t =>
                {
                    // low beside the rail, a little ahead of where the engine will be: it steams straight past the lens
                    Look(trainC + trainU * (trainR + 0.11f) + trainT * 0.42f + V(0f, 0.05f, 0f), trainC + trainU * trainR + V(0f, 0.08f, 0f), 34f); fstop = 4f;
                }));

            // 1:09.5  the tower again, from the front, POW! as the cut lands
            L.Add(Cut("m_splash", 0.5f, 0.7f,
                () => { Sweep(LawnArea); Shoo(); Place(V(PuddleEdgeX - 0.1f, 0f, 5.2f), 270f); splashT = 0f; },
                t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    if ((splashT -= Dt) <= 0f) { splashT = 0.16f; ComicSplash.Spawn(Pos + V(-0.03f, 0f, 0f), 1.2f); }
                    Soft(Pos + V(-0.42f, 0.07f, 0.34f), Pos + V(-0.04f, 0.08f, 0f), 34f, 0.1f); fstop = 4f;
                }));

            // ---------------------------------------------------------------- 70 - 72: the stop-time hits
            // 1:10  three looks on the three hits (70.0, 70.375, 70.75)
            L.Add(Cut("m_looks", 1f, 0.4f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-1.0f, 0f, 0.9f), 0f); Wear("lavender"); },
                t =>
                {
                    At(t, -0.3f, "dance", () => Emote("dance"));
                    At(t, 0.25f, "graphite", () => Wear("graphite"));
                    At(t, 0.5f, "sky", () => Wear("sky"));
                    At(t, 0.75f, "cream", () => Wear("cream"));
                    Look(Pos + V(0.06f, 0.14f, 0.95f), Pos + V(0f, 0.15f, 0f), 25f); fstop = 2.8f;
                }));

            // 1:11  QUACK! on the hit, face to the lens
            L.Add(Cut("m_quack", 0.5f, 0.5f,
                () => { Sweep(FloorArea); Shoo(); Place(V(-1.0f, 0f, 0.9f), 0f); },
                t =>
                {
                    At(t, -0.04f, "q", () => { PlayerDuck.DebugChirp = true; Pop("QUACK!", Pos + V(-0.085f, 0.3f, 0.1f), "#FFC43D", 0.7f, 0.9f); });
                    Look(Pos + V(-0.03f, 0.22f, 0.7f), Pos + V(-0.07f, 0.25f, 0f), 24f); fstop = 2.8f;
                }));

            // 1:11.5  everybody dances - and the end card lands on the next bar
            L.Add(Cut("m_dance", 0.5f, 0.9f,
                () => { Sweep(LawnArea); Weed(PartyAt + V(0f, 0f, -0.2f), 1.6f); Party(); },
                t =>
                {
                    At(t, -0.6f, "dance", () => AllEmote("dance"));
                    Look(PartyAt + V(0.03f, 0.36f, -0.8f), PartyAt + V(0f, 0.14f, 0.08f), 40f); fstop = 5.6f;
                }));

            // (1:12 - 1:18 is the end card, a graphic)

            // ================================================================ BUTTON  78 - 81
            // 1:18  silence, a locked-off camera, the end of the hallway. The duck skates in, boosts - and meets the wall on the sting (79.5).
            L.Add(Cut("button", 3f, 3.0f,
                () =>
                {
                    Sweep(HallArea);
                    wallX = Physics.Raycast(V(6.0f, 0.3f, HallZ), Vector3.right, out var hit, 4f, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore) ? hit.point.x : 7.0f;
                    Place(V(wallX - 1.405f, 0f, HallZ), 90f); Skates(true);
                    MoveDuck(0, V(wallX - 0.22f, 0f, HallZ + 0.27f), 180f); hitAt = 99f;
                },
                t =>
                {
                    float gap = wallX - 0.055f - Pos.x;                 // metres to go before the capsule meets the wall
                    if (P.Body.Fallen && fired.Add("hit"))
                    {
                        hitAt = t;
                        PlayerSkates.DebugBoost = null;
                        Pop("BONK!", V(wallX - 0.3f, 0.43f, HallZ), "#FF8A3D", 1.7f, Mathf.Clamp(1.95f - t, 0.3f, 1.2f));
                        if (DuckBrain.All.Count > 0 && DuckBrain.All[0].Body != null) DuckBrain.All[0].Body.PlayEmote("look");
                    }
                    if (fired.Contains("hit")) Stop();
                    else if (gap > 0.45f) SkateOnCue(Vector3.right, gap - 0.45f, 0.68f - t, t);   // be 45 cm out at about 0.62 s ...
                    else { PlayerDuck.DebugWalk = Vector3.right; PlayerSkates.DebugBoost = 1f; }  // ... then boost: 45 cm at boost speed is 0.9 s
                    // down the hallway from behind: the duck speeds away from the lens and meets the end wall ... then, a beat later, close on the heap
                    if (fired.Contains("hit") && t >= 1.999f) { Look(V(Pos.x - 0.62f, 0.46f, Pos.z - 0.2f), Pos + V(-0.08f, 0.06f, 0.04f), 44f); fstop = 11f; }
                    else { Look(V(wallX - 2.25f, 0.15f, HallZ - 0.04f), V(wallX, 0.17f, HallZ), 17f); Focus(V(wallX - 0.2f, 0.15f, HallZ), 5.6f); }
                }));

            return L;
        }
    }
}
