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
            foreach (var pop in Object.FindObjectsByType<ComicPop>()) Object.Destroy(pop.gameObject);   // no sound word from the last shot hanging in this one
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
        static void MarkToys() { toysBefore = new HashSet<ToyTag>(Object.FindObjectsByType<ToyTag>()); }
        static void Clean()
        {
            foreach (var g in filmToys) if (g != null) { g.SetActive(false); Object.Destroy(g); }
            filmToys.Clear(); tower.Clear();
            if (toysBefore == null) return;
            foreach (var tt in Object.FindObjectsByType<ToyTag>()) if (!toysBefore.Contains(tt)) { tt.gameObject.SetActive(false); Object.Destroy(tt.gameObject); }
            toysBefore = null;
        }

        /// <summary>Put away the small loose toys lying on the floor of a stage (they would trip the skates, sit in front of the lens
        /// or be bitten by mistake); they come back when the next shot is set up. Big props (teddy, beach ball) and things up on
        /// furniture stay where they are, and so does 'keep', e.g. the match ball.</summary>
        static void Sweep(Rect area, Rigidbody keep = null)
        {
            foreach (var rb in Object.FindObjectsByType<Rigidbody>())
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
            foreach (var tr in Object.FindObjectsByType<Transform>())
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
        static void FlockEmote(EmoteKind kind) { foreach (var d in DuckBrain.All) if (d.Body != null) d.Body.PlayEmote(kind); }
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
    }
}
