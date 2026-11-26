using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of M7: football match, house race, laser chase, duck swap and the bag's action tiles.</summary>
    public static class GamesTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Games Test")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) return;
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Log(string s) => Debug.Log("[Microduck] games: " + s);

        static void Tick()
        {
            var pd = PlayerDuck.Instance;
            if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; LaserPointer.DebugPoint = null; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var cam = pd.cam.transform;
            switch (step)
            {
                case 0:
                    FootballGame.Ensure().StartGame(); Log($"football active={FootballGame.Active}"); step++; break;
                case 1 when t > 8:
                    DebugCapture.Shoot("games_football", new Vector3(2.9f, 0.7f, 0.73f), new Vector3(1.93f, 0f, 0.73f), 1280, 800, 65f);
                    DebugCapture.Shoot("games_hud", cam.position, cam.position + cam.forward, 1280, 960, 90f, pd.cam.cullingMask);
                    var f = FootballGame.Instance; Log($"football score {f.Score[0]}:{f.Score[1]} ball={f.BallPos.ToString("F2")}");
                    f.StopGame(); RaceGame.Ensure().StartRace(); step++; break;
                case 2 when t > 13:
                    DebugCapture.Shoot("games_race_start", new Vector3(-0.2f, 0.6f, 3.3f), new Vector3(-2.0f, 0f, 3.2f), 1280, 800, 60f);
                    step++; break;
                case 3 when t > 26:
                    var sb = new System.Text.StringBuilder("race:");
                    foreach (var b in DuckBrain.All) sb.Append($" [{b.index} {AiStates.Name(b.State)} {MicroduckVR.World.HouseLayout.RoomAt(b.Pos)} target={RaceGame.Instance.TargetFor(b).ToString("F1")}]");
                    Log(sb.ToString());
                    DebugCapture.Shoot("games_race_run", new Vector3(-4.2f, 1.6f, 1.2f), new Vector3(-4.6f, 0f, -0.2f), 1280, 800, 70f);
                    RaceGame.Instance.StopRace(); step++; break;
                case 4 when t > 27:
                    var d0 = DuckBrain.All[0];
                    var fw = new Vector3(Mathf.Sin(d0.Body.Yaw), 0f, Mathf.Cos(d0.Body.Yaw));
                    LaserPointer.DebugPoint = d0.Pos + fw * 0.35f;
                    step++; break;
                case 5 when t > 30:
                    var lpnt = LaserPointer.DebugPoint.Value;
                    DebugCapture.Shoot("games_laser", lpnt + new Vector3(0.45f, 0.35f, 0.3f), lpnt, 1280, 800, 55f);
                    int near = 0; foreach (var b in DuckBrain.All) if (Vector3.Distance(b.Pos, lpnt) < 0.25f) near++;
                    Log($"laser at {lpnt.ToString("F2")}, ducks within 25 cm: {near}");
                    LaserPointer.DebugPoint = null; step++; break;
                case 6 when t > 31:
                    var before = pd.Body.Pos; pd.SwapWithNearest();
                    Log($"swap: player {before.ToString("F2")} -> {pd.Body.Pos.ToString("F2")} skin={pd.GetComponent<MicroduckVR.Duck.DuckSetup>().skin}");
                    var bag = pd.GetComponent<BagMenu>(); bag.Open(); step++; break;
                case 7 when t > 32:
                    DebugCapture.Shoot("games_bag", cam.position, cam.position + cam.forward, 1280, 960, 90f, pd.cam.cullingMask);
                    pd.GetComponent<BagMenu>().Close(); EditorApplication.update -= Tick; Log("done"); break;
            }
        }
    }
}
