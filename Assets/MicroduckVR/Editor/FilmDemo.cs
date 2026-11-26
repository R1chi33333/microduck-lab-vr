using System.Collections.Generic;
using MicroduckVR.Custom;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// The gameplay walkthrough: every feature once, in the order a new player meets them. Mostly the player's own
    /// view or a plain follow camera - this one is about showing what happens, not about pretty framing.
    /// </summary>
    public static partial class Film
    {
        static float viewYaw; static string lastCard = ""; static float cardT;

        static List<Shot> DemoShots()
        {
            var L = new List<Shot>();

            // ---- first run: the mirror, then the tutorial reacting to what the player really does
            L.Add(new Shot { name = "tutorial", dur = 64f, pre = 0.2f, fp = true, caption = "First run: make your duck, then a tutorial that waits for every action",
                start = () => { GameHud.Hidden = false; TutorialCoach.Restart(); viewYaw = 180f; lastCard = ""; cardT = 0f; },
                tick = t =>
                {
                    GameHud.Hidden = false;
                    string card = TutorialCoach.DebugTitle; if (card != lastCard) { Debug.Log($"[Microduck] film card at {t:F1}: {card}"); lastCard = card; cardT = 0f; grabPhase = 0; phaseT = 0f; PlayerDuck.DebugBeak = 0f; Stop(); } else cardT += Dt;
                    var b = MirrorStation.Instance.Board; var bag = Bag; var eye = P.cam.transform.position; float pitch = 6f;
                    if (card.StartsWith("MAKE YOUR DUCK"))
                    {
                        string[] ids = { "skin_sky", "part_head", "paint_lavender", "voice_2", "random", "skin_cream", "save" };
                        for (int i = 0; i < ids.Length; i++) { int ii = i; At(cardT, 2.0f + i * 1.3f, "m" + i, () => Click(b, ids[ii])); At(cardT, 2.15f + i * 1.3f, "mr" + i, () => WorldPanel.DebugPoint = null); }
                        pitch = 0f;
                    }
                    else if (card.StartsWith("WADDLE"))
                    {
                        // (cut) the rest of the lesson is filmed on a clear bit of living-room floor
                        At(cardT, 0f, "tolr", () => { Place(V(-1.6f, 0f, 0.6f), 55f); viewYaw = 55f; });
                        if (cardT > 1.8f) PlayerDuck.DebugWalk = Fwd(viewYaw * Mathf.Deg2Rad);
                    }
                    else if (card.StartsWith("TURN")) { if (cardT > 1.6f) { viewYaw = Mathf.MoveTowards(viewYaw, 110f, 60f * Dt); P.FaceView(viewYaw * Mathf.Deg2Rad); PlayerDuck.DebugWalk = Fwd(viewYaw * Mathf.Deg2Rad); } }
                    else if (card.StartsWith("OPEN YOUR BEAK")) { if (cardT > 1.8f) PlayerDuck.DebugBeak = 1f; pitch = 24f; }
                    else if (card.StartsWith("GRAB IT"))
                    {
                        pitch = 26f;
                        Grabbable toy = null; float bd = 0.6f; foreach (var g in Grabbable.All) { if (g.label != "rubber duck") continue; float d = Vector3.Distance(g.Centre, Pos); if (d < bd) { bd = d; toy = g; } }
                        if (cardT > 1.5f && toy != null) Grab(toy.Centre);
                    }
                    else if (card.StartsWith("DROP IT")) { pitch = 20f; if (cardT < 2.6f) { if (cardT > 0.6f) PlayerDuck.DebugWalk = Fwd(viewYaw * Mathf.Deg2Rad); } else { Stop(); PlayerDuck.DebugBeak = cardT < 3.0f ? 1f : 0f; } }
                    else if (card.StartsWith("QUACK")) At(cardT, 1.8f, "quack", () => PlayerDuck.DebugChirp = true);
                    else if (card.StartsWith("TOY BAG")) At(cardT, 1.8f, "bagopen", () => bag.Open());
                    else if (card.StartsWith("PICK A TOY"))
                    {
                        if (cardT > 0.5f && cardT < 2.4f) bag.SetDebugAim(new Ray(eye, Vector3.Lerp(bag.TileWorld("pebble"), bag.TileWorld("toy_car"), E((cardT - 0.5f) / 1.8f)) - eye));
                        At(cardT, 2.5f, "pickcar", () => bag.Choose("toy_car"));
                    }
                    else if (card.StartsWith("PUT IT DOWN"))
                    {
                        pitch = 22f; var p = Pos + Fwd(viewYaw * Mathf.Deg2Rad) * 0.4f; p = Ground(p.x, p.z) + V(0f, 0.01f, 0f);
                        bag.SetDebugAim(new Ray(eye, p - eye)); At(cardT, 1.8f, "putcar", () => bag.DebugPress());
                    }
                    else if (card.StartsWith("CLOSE THE BAG")) At(cardT, 1.6f, "bagclose", () => { bag.SetDebugAim(null); bag.Close(); });
                    else if (card.StartsWith("ROLLER SKATES")) { viewYaw = Mathf.MoveTowards(viewYaw, 75f, 60f * Dt); P.FaceView(viewYaw * Mathf.Deg2Rad); At(cardT, 1.8f, "skon", () => Skates(true)); }
                    else if (card.StartsWith("ROLL!")) { if (cardT > 1f) PlayerDuck.DebugWalk = Fwd(viewYaw * Mathf.Deg2Rad); }
                    else if (card.StartsWith("BOOST")) { PlayerDuck.DebugWalk = Fwd((viewYaw + 180f) * Mathf.Deg2Rad); if (cardT > 1.5f) PlayerSkates.DebugBoost = 1f; }
                    else { PlayerSkates.DebugBoost = null; if (card.StartsWith("DRIFT")) Stop(); }
                    Eyes(viewYaw, pitch, 88f);
                } });

            // ---- moving about
            L.Add(new Shot { name = "walk", dur = 9f, pre = 0.3f, caption = "Left stick: waddle where you look.  Right stick: turn",
                start = () => { Place(V(-1.5f, 0f, 0.7f), 60f); },
                tick = t => { if (t < 4.5f) Walk(V(-0.5f, 0f, 1.4f)); else Walk(V(0.3f, 0f, 2.0f)); Chase(0.55f, 0.3f, 60f); } });

            L.Add(new Shot { name = "grab", dur = 11f, pre = 0.3f, caption = "Right trigger: open the beak, let go to bite, squeeze again to drop",
                start = () => { Place(V(-1.4f, 0f, 1.0f), 90f); ToyCatalog.Spawn("rubber_duck_pink", Ground(-1.15f, 1.0f), -90f); },
                tick = t =>
                {
                    if (!fired.Contains("got")) { if (t > 0.8f && Grab(Ground(-1.15f, 1.0f))) { fired.Add("got"); biteAt = t; } }
                    else if (t < biteAt + 4.5f) { if (t > biteAt + 0.8f) Walk(V(-0.4f, 0f, 1.5f)); }
                    else { Stop(); PlayerDuck.DebugBeak = t < biteAt + 5.0f ? 1f : 0f; }
                    var f = Fwd(P.Body.Yaw); Soft(Pos + f * 0.05f + V(0.1f, 0.2f, -0.55f), Pos + f * 0.06f + V(0f, 0.11f, 0f), 40f, 0.3f);
                } });

            L.Add(new Shot { name = "clutter", dur = 9f, pre = 0.3f, caption = "Small things lying around the house can be carried too",
                start = () =>
                {
                    // a small loose bit of house clutter (not a bag toy), brought to a clear patch of floor
                    Grabbable best = null; float bd = 99f; var from = V(-1.0f, 0f, 1.0f);
                    foreach (var g in Grabbable.All) { if (g.GetComponent<ToyTag>() != null || g.IsHeld || g.Centre.y > 0.08f || g.Radius > 0.025f) continue; float d = Vector3.Distance(g.Centre, from); if (d < bd) { bd = d; best = g; } }
                    clutter = best; Place(V(-1.75f, 0f, 0.62f), 90f);
                    if (best != null) { best.Body.position = Ground(-1.5f, 0.62f) + V(0f, 0.03f, 0f); best.Body.linearVelocity = Vector3.zero; best.transform.position = best.Body.position; }
                },
                tick = t =>
                {
                    var c = clutter != null ? clutter.Centre : Pos;
                    if (!fired.Contains("got")) { if (t > 0.6f && clutter != null && Grab(c)) { fired.Add("got"); biteAt = t; } }
                    else if (t > biteAt + 0.7f) Walk(V(-0.5f, 0f, 1.4f));
                    var f = Fwd(P.Body.Yaw); Soft(Pos + f * 0.05f + V(0.05f, 0.2f, -0.5f), Pos + f * 0.06f + V(0f, 0.1f, 0f), 42f, 0.3f);
                } });

            L.Add(new Shot { name = "quack", dur = 4.5f, pre = 0.3f, caption = "A: quack",
                start = () => { Place(V(-1.0f, 0f, 1.0f), 180f); },
                tick = t => { At(t, 0.8f, "q1", () => PlayerDuck.DebugChirp = true); At(t, 2.2f, "q2", () => PlayerDuck.DebugChirp = true); Look(Pos + V(0.12f, 0.16f, -0.55f), Pos + V(0f, 0.17f, 0f), 40f); } });

            // ---- the toy bag
            L.Add(new Shot { name = "bag", dur = 17f, pre = 0.3f, fp = true, caption = "Y: the toy bag - 29 toys to put down, and a bin to clear them",
                start = () => { Place(V(-1.45f, 0f, 0.75f), 60f); },
                tick = t =>
                {
                    var bag = Bag; var eye = P.cam.transform.position; float pitch = 4f;
                    Vector3 Spot(int i) { var f = Fwd(60f * Mathf.Deg2Rad); var p = Pos + f * (0.38f + 0.09f * (i % 3)) + V(f.z, 0f, -f.x) * ((i - 2.5f) * 0.1f); return Ground(p.x, p.z) + V(0f, 0.01f, 0f); }
                    At(t, 0.6f, "open", () => bag.Open());
                    if (t > 0.8f && t < 3.4f) bag.SetDebugAim(new Ray(eye, Vector3.Lerp(bag.TileWorld("sock_ball"), bag.TileWorld("wicker_ball"), E((t - 0.8f) / 2.6f)) - eye));
                    At(t, 3.5f, "pick1", () => bag.Choose("wicker_ball"));
                    if (t > 3.5f && t < 6.2f) { pitch = 22f; int k = Mathf.Clamp((int)((t - 3.7f) / 0.8f), 0, 2); bag.SetDebugAim(new Ray(eye, Spot(k) - eye)); for (int i = 0; i < 3; i++) At(t, 4.1f + i * 0.8f, "p1" + i, () => bag.DebugPress()); }
                    At(t, 6.4f, "reopen", () => { bag.SetDebugAim(null); bag.Close(); bag.Open(); });
                    if (t > 6.6f && t < 8.3f) bag.SetDebugAim(new Ray(eye, Vector3.Lerp(bag.TileWorld("wicker_ball"), bag.TileWorld("strawberry"), E((t - 6.6f) / 1.7f)) - eye));
                    At(t, 8.4f, "pick2", () => bag.Choose("strawberry"));
                    if (t > 8.4f && t < 11.2f) { pitch = 22f; int k = Mathf.Clamp((int)((t - 8.6f) / 0.8f), 0, 2) + 3; bag.SetDebugAim(new Ray(eye, Spot(k) - eye)); for (int i = 0; i < 3; i++) At(t, 9.0f + i * 0.8f, "p2" + i, () => bag.DebugPress()); }
                    At(t, 11.4f, "reopen2", () => { bag.SetDebugAim(null); bag.Close(); bag.Open(); });
                    if (t > 11.6f && t < 12.8f) bag.SetDebugAim(new Ray(eye, Vector3.Lerp(bag.TileWorld("strawberry"), bag.TileWorld("trash"), E((t - 11.6f) / 1.2f)) - eye));
                    At(t, 12.9f, "bin", () => bag.Choose("trash"));
                    if (t > 12.9f) { pitch = 22f; int k = Mathf.Clamp((int)((t - 13.1f) / 0.9f), 0, 3); bag.SetDebugAim(new Ray(eye, Spot(k) + V(0f, 0.01f, 0f) - eye)); for (int i = 0; i < 4; i++) At(t, 13.6f + i * 0.9f, "bin" + i, () => bag.DebugPress()); }
                    Eyes(60f, pitch, 84f);
                } });

            // ---- skates
            L.Add(new Shot { name = "skates", dur = 16f, pre = 0.3f, caption = "B: roller skates.  Left trigger: boost.  Left grip + steer: drift",
                start = () => { Place(V(6.2f, 0f, 3.22f), 270f); },
                tick = t =>
                {
                    At(t, 1.0f, "on", () => Skates(true));
                    if (t > 1.8f && t < 8f) PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 3.5f, "boost", () => PlayerSkates.DebugBoost = 1f); At(t, 3.9f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    if (t >= 8f && t < 13.5f) { float a = (t - 8f) * 1.3f; PlayerDuck.DebugWalk = V(-Mathf.Cos(a), 0f, -Mathf.Sin(a) * 0.9f); PlayerSkates.DebugDrift = 1f; }
                    if (t >= 13.5f) { PlayerSkates.DebugDrift = null; Stop(); }
                    At(t, 14.6f, "off", () => Skates(false));
                    Chase(0.6f, 0.32f, 62f);
                } });

            // ---- laser
            L.Add(new Shot { name = "laser", dur = 9f, pre = 3f, caption = "Right grip: laser dot - the other ducks chase it",
                start = () =>
                {
                    Place(V(-2.6f, 0f, 2.3f), 120f);
                    MoveDuck(0, V(-0.2f, 0f, 1.6f), 270f); MoveDuck(1, V(-1.7f, 0f, 0.5f), 0f); MoveDuck(2, V(-0.4f, 0f, 0.8f), 300f); MoveDuck(3, V(-1.9f, 0f, 1.2f), 90f); MoveDuck(4, V(-1.0f, 0f, 1.9f), 180f);
                },
                tick = t =>
                {
                    float a = (t + 3f) * 0.55f; var dot = V(-1.0f + Mathf.Cos(a) * 0.3f, 0f, 1.05f + Mathf.Sin(a) * 0.25f);
                    LaserPointer.DebugPoint = dot; Soft(V(-1.0f, 0.9f, 2.3f), V(-1.0f, 0.05f, 1.05f), 48f, 0.5f);
                } });

            // ---- third person, the real in-game view
            L.Add(new Shot { name = "third", dur = 9f, pre = 0.3f, fp = true, caption = "Left stick click: step outside and see your duck (third person)",
                start = () => { Place(V(-1.5f, 0f, 0.7f), 60f); },
                tick = t =>
                {
                    At(t, 1.5f, "out", () => P.SetThirdPerson(true)); At(t, 7.8f, "in", () => P.SetThirdPerson(false));
                    if (t > 2.5f && t < 7f) Walk(V(-0.4f, 0f, 1.5f));
                    cam.transform.SetPositionAndRotation(P.cam.transform.position, P.cam.transform.rotation); cam.fieldOfView = 88f; cam.cullingMask = P.cam.cullingMask;
                } });

            // ---- games
            L.Add(new Shot { name = "football", dur = 24f, pre = 1.5f, caption = "Toy bag > FOOTBALL: two teams on the rug, first goals win",
                start = () => { GameHud.Hidden = false; FootballGame.Ensure().StartGame(); },
                tick = t =>
                {
                    GameHud.Hidden = false;
                    var fg = FootballGame.Instance; var ball = fg != null && fg.Ball != null ? fg.Ball.position : FootballGame.Centre;
                    Walk(ball + V(0f, 0f, -0.02f), 0.01f);
                    Soft(FootballGame.Centre + V(0.6f, 0.6f, -0.95f), Vector3.Lerp(FootballGame.Centre, ball, 0.6f) + V(0f, 0.05f, 0f), 58f, 0.5f);
                } });

            L.Add(new Shot { name = "race", dur = 36f, pre = 0.6f, caption = "Toy bag > RACE: one lap of the whole house through the gates",
                start = () => { GameHud.Hidden = false; RaceGame.Ensure().StartRace(); },
                tick = t =>
                {
                    GameHud.Hidden = false;
                    var tgt = RaceGame.Ensure().PlayerTarget(); if (tgt.HasValue) Walk(tgt.Value, 0.02f); else Stop();
                    if (t < 3.6f) Look(V(3.3f, 0.34f, 3.44f), V(1.6f, 0.12f, 3.22f), 46f); else Chase(0.7f, 0.42f, 64f);      // in front of the gate behind the grid, clear of its pointer
                } });

            L.Add(new Shot { name = "emotes", dur = 10f, pre = 0.3f, caption = "Toy bag: DANCE, NOD, SHAKE, LOOK",
                start = () => { Place(V(-1.0f, 0f, 1.0f), 180f); },
                tick = t =>
                {
                    At(t, 0.5f, "e1", () => Emote("dance")); At(t, 4.0f, "e2", () => Emote("nod")); At(t, 6.0f, "e3", () => Emote("shake")); At(t, 8.0f, "e4", () => Emote("look"));
                    Look(Pos + V(0.15f, 0.15f, -0.6f), Pos + V(0f, 0.15f, 0f), 42f);
                } });

            L.Add(new Shot { name = "swap", dur = 8f, pre = 0.3f, caption = "Toy bag > SWAP DUCK: become the duck in front of you",
                start = () => { Place(V(-1.4f, 0f, 1.0f), 90f); MoveDuck(0, V(-0.8f, 0f, 1.0f), 270f); },
                tick = t => { At(t, 2.5f, "swap", () => P.SwapWithNearest()); if (t > 4.5f) Walk(Pos + Fwd(P.Body.Yaw), 0.01f); Soft(V(-1.1f, 0.4f, 2.0f), V(-1.1f, 0.12f, 1.0f), 50f, 0.4f); } });

            L.Add(new Shot { name = "ducks", dur = 10f, pre = 0.3f, caption = "Toy bag > ADD DUCK / REMOVE DUCK",
                start = () => { GameHud.Hidden = false; Place(V(0.35f, 0f, 4.75f), 0f); },
                tick = t =>
                {
                    GameHud.Hidden = false;
                    for (int i = 0; i < 4; i++) { int ii = i; At(t, 0.8f + i * 1.1f, "add" + i, () => { P.FaceView((ii * 60f - 90f) * Mathf.Deg2Rad); FlockSpawner.Add(); }); }
                    for (int i = 0; i < 2; i++) At(t, 6.5f + i * 1.3f, "rem" + i, () => FlockSpawner.Remove());
                    Look(V(0.6f, 0.3f, 5.75f), Pos + V(0f, 0.12f, 0.1f), 60f);
                } });

            // ---- the garage
            L.Add(new Shot { name = "garage", dur = 12f, pre = 0.3f, fp = true, caption = "The garage: step on the turntable, pick a part, REPLACE",
                start = () => { GameHud.Hidden = false; Place(V(-2.2f, 0f, 2.3f), 180f); },
                tick = t =>
                {
                    GameHud.Hidden = false;
                    if (t < 3f) Walk(V(-2.2f, 0f, 1.78f), 0.03f); else Stop();
                    var g = Garage.Instance;
                    if (g != null) { At(t, 5.0f, "row", () => Click(g.Board, "row_1")); At(t, 5.2f, "rowr", () => WorldPanel.DebugPoint = null); At(t, 7.5f, "rep", () => Click(g.Board, "replace")); At(t, 7.7f, "repr", () => WorldPanel.DebugPoint = null); }
                    cam.transform.SetPositionAndRotation(P.cam.transform.position, P.cam.transform.rotation); cam.fieldOfView = 88f; cam.cullingMask = P.cam.cullingMask;
                } });

            // ---- outside
            L.Add(new Shot { name = "garden", dur = 12f, pre = 0.3f, caption = "The garden: grass that parts around you, a puddle to splash in",
                start = () => { Place(V(0.7f, 0f, 4.5f), 330f); },
                tick = t => { if (t < 5f) Walk(V(-0.2f, 0f, 5.5f)); else Walk(V(-2.0f, 0f, 5.2f)); Chase(0.5f, 0.3f, 62f); } });

            L.Add(new Shot { name = "train", dur = 6f, pre = 1f, caption = "The bedroom: a toy train doing laps",
                start = () => { Place(V(4.95f, 0f, 0.9f), 0f); },
                tick = t => { var tr = Object.FindAnyObjectByType<ToyTrain>(); var eng = tr != null && tr.cars != null && tr.cars.Length > 0 ? tr.cars[0].position : V(4.95f, 0.05f, 1.45f); Soft(V(4.95f, 0.5f, 2.2f), Vector3.Lerp(V(4.95f, 0.05f, 0.9f), eng, 0.5f), 50f, 0.5f); } });

            // ---- help and credits
            L.Add(new Shot { name = "help", dur = 9f, pre = 0.3f, fp = true, caption = "Toy bag > HELP and CREDITS",
                start = () => { Place(V(-1.45f, 0f, 0.75f), 60f); },
                tick = t =>
                {
                    var bag = Bag;
                    At(t, 0.5f, "open", () => bag.Open()); At(t, 1.8f, "help", () => bag.Choose("help"));
                    At(t, 5.0f, "open2", () => { TutorialCoach.HideAll(); bag.Open(); }); At(t, 5.8f, "cred", () => bag.Choose("credits"));
                    Eyes(60f, 8f, 84f);
                } });
            return L;
        }

        static Grabbable clutter;
    }
}
