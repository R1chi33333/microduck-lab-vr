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
    public static partial class Film
    {
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
                    At(t, 0.2f, "nod", () => FlockEmote(EmoteKind.Nod));
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
                    At(t, 1.1f, "nod", () => FlockEmote(EmoteKind.Nod));
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
                    At(t, -0.45f, "dance", () => AllEmote(EmoteKind.Dance));
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
                    At(t, -0.45f, "dance", () => AllEmote(EmoteKind.Dance));
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
                    At(t, 0.2f, "nod", () => Emote(EmoteKind.Nod));
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
                    At(t, 0.3f, "look", () => Emote(EmoteKind.Look));
                    At(t, 1.0f, "graphite", () => Wear("graphite"));
                    At(t, 2.0f, "twotone", () => Wear("cream", "graphite", "", "", "graphite"));
                    At(t, 3.0f, "sky", () => { Wear("sky"); Emote(EmoteKind.Dance); });
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
                    At(t, 0.1f, "look", () => Emote(EmoteKind.Look));
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
                    bool inWater = st != null && st.Kind == SurfaceType.Water;
                    if (inWater && fired.Add("wet")) { Say("SPLASH!", "#3A86FF", 1f, 1.3f); FlockEmote(EmoteKind.Dance); ComicSplash.Spawn(Pos + V(-0.02f, 0f, 0f), 1.5f); splashT = 0.3f; }
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
                    At(t, -0.3f, "dance", () => Emote(EmoteKind.Dance));
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
                    At(t, -0.6f, "dance", () => AllEmote(EmoteKind.Dance));
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
                        if (DuckBrain.All.Count > 0 && DuckBrain.All[0].Body != null) DuckBrain.All[0].Body.PlayEmote(EmoteKind.Look);
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
