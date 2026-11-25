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
    /// The trailer. The idea behind the camera: you are small. So the camera lives at duck height - on the floor,
    /// in the grass, under the table - and only twice goes up high, to show how big the house is. Wide lenses close
    /// to the duck for comedy and speed, long lenses for the quiet beats, and every move is a slow push or a track;
    /// the cuts do the rhythm.
    /// </summary>
    public static partial class Film
    {
        static List<Shot> TrailerShots()
        {
            var L = new List<Shot>();

            // 1. cold open: something is coming through the grass
            L.Add(new Shot { name = "grass", dur = 6.5f, pre = 1.5f,
                start = () => { Place(V(2.2f, 0f, 4.9f), 0f); },
                tick = t => { Walk(V(2.2f, 0f, 6.6f)); float u = E(t / 6.5f); Look(V(2.38f, 0.05f + 0.03f * u, 6.3f + 0.12f * u), Pos + V(0f, 0.12f, 0f), 36f); } });

            // 2. the hero: a slow low orbit, the furniture towering behind
            L.Add(new Shot { name = "hero", dur = 6.5f, pre = 1f,
                start = () => { Place(V(0.2f, 0f, 1.9f), 180f); },
                tick = t =>
                {
                    At(t, 1.2f, "look", () => Emote("look")); At(t, 4.2f, "q", () => PlayerDuck.DebugChirp = true);
                    float a = Mathf.Lerp(150f, 215f, E(t / 6.5f)) * Mathf.Deg2Rad;
                    Look(Pos + V(Mathf.Sin(a) * 0.56f, 0.1f, Mathf.Cos(a) * 0.56f), Pos + V(0f, 0.16f, 0f), 38f);
                } });

            // 3. how big the house is: the only high, wide shot of the first half
            L.Add(new Shot { name = "house", dur = 5.5f, pre = 1f,
                start = () => { Place(V(-0.4f, 0f, 1.7f), 150f); MoveDuck(0, V(0.9f, 0f, 0.9f), 40f); MoveDuck(1, V(1.5f, 0f, -0.6f), -60f); },
                tick = t => { Walk(V(0.9f, 0f, -0.6f)); float u = E(t / 5.5f); Look(V(-2.3f + 0.3f * u, 2.0f - 0.25f * u, 2.2f - 0.2f * u), V(0.3f, 0.1f, -0.2f), 50f); } });

            // 4. through its eyes: under the table, between the legs
            L.Add(new Shot { name = "pov", dur = 5f, pre = 0.5f, fp = true,
                start = () => { Place(V(0.5f, 0f, 1.5f), 195f); },
                tick = t => { Walk(V(-0.3f, 0f, -1.4f)); Eyes(195f + 6f * Mathf.Sin(t * 0.9f), 2f, 96f); } });

            // 5. the beak: a tight profile, open - reach - bite - carry
            L.Add(new Shot { name = "grab", dur = 7f, pre = 0.5f,
                start = () => { Place(V(-1.3f, 0f, 1.0f), 90f); ToyCatalog.Spawn("toy_duck", Ground(-1.08f, 1.0f), -90f); },
                tick = t =>
                {
                    if (t > 0.6f && !fired.Contains("got")) { if (Grab(Ground(-1.08f, 1.0f))) { fired.Add("got"); biteAt = t; } }
                    if (biteAt >= 0f && t > biteAt + 0.8f) Walk(V(-0.3f, 0f, 1.5f));
                    var f = Fwd(P.Body.Yaw);
                    Soft(Pos + f * 0.08f + V(0f, 0.12f, -0.5f), Pos + f * 0.07f + V(0f, 0.12f, 0f), 32f, 0.3f);
                } });

            // 6. the flock comes running for the laser dot
            L.Add(new Shot { name = "laser", dur = 7f, pre = 4f,
                start = () =>
                {
                    Place(V(-2.6f, 0f, 2.3f), 120f);
                    MoveDuck(0, V(-0.2f, 0f, 1.6f), 270f); MoveDuck(1, V(-1.7f, 0f, 0.5f), 0f); MoveDuck(2, V(-0.4f, 0f, 0.8f), 300f); MoveDuck(3, V(-1.9f, 0f, 1.2f), 90f); MoveDuck(4, V(-1.0f, 0f, 1.9f), 180f);
                },
                tick = t =>
                {
                    float a = (t + 4f) * 0.55f; var dot = V(-1.0f + Mathf.Cos(a) * 0.3f, 0f, 1.05f + Mathf.Sin(a) * 0.25f);
                    LaserPointer.DebugPoint = dot;
                    float u = E(t / 7f); Soft(V(-1.0f + 0.25f * u, 0.95f - 0.2f * u, 2.3f), V(-1.0f, 0.05f, 1.05f) * 0.6f + (dot + V(0f, 0.05f, 0f)) * 0.4f, 46f, 0.5f);
                } });

            // 7. the toy bag: your own hands, a rain of toys
            L.Add(new Shot { name = "bag", dur = 7f, pre = 0.3f, fp = true,
                start = () => { Place(V(-1.45f, 0f, 0.75f), 60f); },
                tick = t =>
                {
                    var bag = Bag; var eye = P.cam.transform.position; var f = Fwd(60f * Mathf.Deg2Rad); var r = V(f.z, 0f, -f.x);
                    At(t, 0.5f, "open", () => bag.Open());
                    if (t > 0.6f && t < 2.2f) { var from = bag.TileWorld("help"); var to = bag.TileWorld("toy_duck"); bag.SetDebugAim(new Ray(eye, Vector3.Lerp(from, to, E((t - 0.6f) / 1.4f)) - eye)); }
                    At(t, 2.3f, "pick", () => bag.Choose("toy_duck"));
                    if (t > 2.3f)
                    {
                        var offs = new[] { V(0.42f, 0f, 0f), V(0.5f, 0f, 0.14f), V(0.36f, 0f, -0.15f), V(0.6f, 0f, -0.05f), V(0.48f, 0f, -0.25f) };
                        int k = Mathf.Clamp((int)((t - 2.5f) / 0.8f), 0, offs.Length - 1);
                        var w = Pos + f * offs[k].x + r * offs[k].z; var p = Ground(w.x, w.z) + V(0f, 0.01f, 0f);
                        bag.SetDebugAim(new Ray(eye, p - eye));
                        for (int i = 0; i < offs.Length; i++) At(t, 2.9f + i * 0.8f, "put" + i, () => bag.DebugPress());
                    }
                    Eyes(60f, t < 2.3f ? 4f : 24f, 80f);
                } });

            // 8. skates: the hallway is a drag strip
            L.Add(new Shot { name = "skates", dur = 7f, pre = 1.2f,
                start = () => { Place(V(5.6f, 0f, 3.22f), 270f); Skates(true); },
                tick = t =>
                {
                    PlayerDuck.DebugWalk = Vector3.left;
                    At(t, 1.6f, "boost", () => PlayerSkates.DebugBoost = 1f); At(t, 2.0f, "boostoff", () => PlayerSkates.DebugBoost = null);
                    At(t, 4.4f, "boost2", () => PlayerSkates.DebugBoost = 1f); At(t, 4.8f, "boostoff2", () => PlayerSkates.DebugBoost = null);
                    Soft(Pos + V(-0.5f, 0.05f, 0.17f), Pos + V(0f, 0.12f, 0f), 62f, 0.18f);
                } });

            // 9. drift: from above, the skid marks drawing a curve on the floor
            L.Add(new Shot { name = "drift", dur = 6.5f, pre = 2.2f,
                start = () => { Place(V(-5.6f, 0f, -1.0f), 90f); Skates(true); },
                tick = t =>
                {
                    float a = (t + 2.2f) * 1.25f; var c = V(-5.0f, 0f, -1.0f);
                    var target = c + V(Mathf.Cos(a) * 0.75f, 0f, Mathf.Sin(a) * 0.5f);
                    PlayerDuck.DebugWalk = (target - Pos).normalized;
                    PlayerSkates.DebugDrift = t > 0.3f ? 1f : (float?)null;
                    Soft(V(-6.6f, 1.35f, -1.7f), Vector3.Lerp(c, Pos, 0.7f) + V(0f, 0.05f, 0f), 34f, 0.4f);
                } });

            // 10. football: low on the touchline, a long lens squeezing the players together
            L.Add(new Shot { name = "football", dur = 8f, pre = 2.5f,
                start = () => { FootballGame.Ensure().StartGame(); },
                tick = t =>
                {
                    var fg = FootballGame.Instance; var ball = fg != null && fg.Ball != null ? fg.Ball.position : FootballGame.Centre;
                    Walk(ball + V(0f, 0f, -0.02f), 0.01f);
                    Soft(FootballGame.Centre + V(0.7f, 0.1f, -0.4f + 0.3f * E(t / 8f)), Vector3.Lerp(FootballGame.Centre, ball, 0.75f) + V(0f, 0.06f, 0f), 36f, 0.45f);
                } });

            // 11. the race: on the start line, they come straight at the lens
            L.Add(new Shot { name = "race", dur = 7.5f, pre = 1.6f,
                start = () => { RaceGame.Ensure().StartRace(); GameHud.Hidden = false; },
                tick = t =>
                {
                    var tgt = RaceGame.Ensure().PlayerTarget(); if (tgt.HasValue) Walk(tgt.Value, 0.02f);
                    if (t < 3.8f) Look(V(3.75f, 0.42f - 0.1f * E(t / 3.8f), 3.26f), V(1.6f, 0.12f, 3.22f), 42f);
                    else Soft(Pos + V(-0.7f, 0.2f, 0.3f), Pos + V(0f, 0.12f, 0f), 55f, 0.25f);
                } });

            // 12. the mirror: who do you want to be? four looks, four cuts of colour
            L.Add(new Shot { name = "mirror", dur = 7f, pre = 0.6f, fp = true,
                start = () => { Place(MirrorStation.StandPos + V(0f, 0f, -0.22f), 180f); },
                tick = t =>
                {
                    var b = MirrorStation.Instance.Board;
                    string[] ids = { "skin_lavender", "skin_sky", "random", "skin_graphite", "random", "skin_cream" };
                    for (int i = 0; i < ids.Length; i++) { int ii = i; At(t, 0.9f + i * 1.0f, "c" + i, () => Click(b, ids[ii])); At(t, 1.0f + i * 1.0f, "r" + i, () => WorldPanel.DebugPoint = null); }
                    Eyes(180f + 2f * Mathf.Sin(t * 0.8f), -6f, Mathf.Lerp(66f, 56f, E(t / 7f)));
                } });

            // 13. more ducks! they pop in one after another
            L.Add(new Shot { name = "ducks", dur = 6f, pre = 0.5f,
                start = () => { Place(V(0.35f, 0f, 4.75f), 0f); },
                tick = t =>
                {
                    for (int i = 0; i < 5; i++) { int ii = i; At(t, 0.6f + i * 0.85f, "add" + i, () => { P.FaceView((ii * 55f - 110f) * Mathf.Deg2Rad); FlockSpawner.Add(); }); }
                    float u = E(t / 6f); Look(V(0.5f + 0.2f * u, 0.14f, 5.5f + 0.15f * u), Pos + V(0f, 0.14f, 0f), 56f);
                } });

            // 14. splash
            L.Add(new Shot { name = "puddle", dur = 4.5f, pre = 0.5f,
                start = () => { Place(V(-0.75f, 0f, 5.05f), 280f); GameHud.Hidden = true; },
                tick = t => { Walk(V(-2.2f, 0f, 5.3f)); float u = E(t / 4.5f); Look(V(-1.45f, 1.0f - 0.2f * u, 6.4f), Vector3.Lerp(V(-1.45f, 0f, 5.2f), Pos, 0.6f), 40f); } });

            // 15. the toy train goes by, a duck watches
            L.Add(new Shot { name = "train", dur = 5f, pre = 1f,
                start = () => { Place(V(4.95f, 0f, 0.9f), 0f); },
                tick = t =>
                {
                    var tr = Object.FindAnyObjectByType<ToyTrain>(); var eng = tr != null && tr.cars != null && tr.cars.Length > 0 ? tr.cars[0].position : V(4.95f, 0.05f, 1.45f);
                    At(t, 0.5f, "look", () => Emote("look"));
                    Soft(V(4.95f, 0.06f, 2.05f), Vector3.Lerp(Pos + V(0f, 0.1f, 0f), eng, 0.65f), 48f, 0.5f);
                } });

            // 16. everybody dance: the push-in that ends on the title
            L.Add(new Shot { name = "dance", dur = 8f, pre = 1f,
                start = () =>
                {
                    Place(V(0f, 0f, 5.0f), 180f);
                    for (int i = 0; i < DuckBrain.All.Count; i++) { int k = i / 2 + 1; float sx = i % 2 == 0 ? -1f : 1f; MoveDuck(i, V(sx * k * 0.2f, 0f, 5.0f + k * 0.13f), 180f); }
                },
                tick = t =>
                {
                    At(t, 0f, "d1", () => AllEmote("dance")); At(t, 3.6f, "d2", () => AllEmote("dance")); At(t, 5.2f, "q", () => PlayerDuck.DebugChirp = true);
                    float u = E(t / 8f);
                    Look(V(0f, 0.1f + 0.4f * u * u, 4.28f - 0.2f * u), V(0f, 0.15f + 2.8f * u * u * u, 5.2f + 2.5f * u), Mathf.Lerp(52f, 44f, u));
                } });
            return L;
        }

    }
}
