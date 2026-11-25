using MicroduckVR.Custom;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the first-run flow: mirror first, tutorial steps that wait for the real action,
    /// the animated controller card, grabbing house clutter, adding a duck, HELP from the bag.</summary>
    public static class FlowTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Flow Test")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.Restart();
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Shot(string n, float up = 0f) { var pd = PlayerDuck.Instance; var c = pd.cam.transform; DebugCapture.Shoot(n, c.position, c.position + c.forward + Vector3.up * up, 1280, 960, 90f, pd.cam.cullingMask); }
        static void Log(string what) => Debug.Log($"[Microduck] flow {what}: card = {TutorialCoach.DebugTitle}");

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; WorldPanel.DebugPoint = null; PlayerDuck.DebugWalk = null; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var bag = pd.GetComponent<BagMenu>();
            switch (step)
            {
                case 0 when t > 1.0: step++; Log("start"); Shot("flow_intro", 0.1f); break;
                case 1 when t > 5.2: step++; Log("after intro"); Shot("flow_mirror_a", 0.12f); break;
                case 2 when t > 5.9: step++; Shot("flow_mirror_b", 0.12f);
                    DebugCapture.Shoot("flow_mirror_wide", new Vector3(4.2f, 0.5f, -0.9f), new Vector3(4.6f, 0.6f, -2.4f), 1280, 960, 75f, pd.cam.cullingMask); break;
                case 3 when t > 8.0: step++; Log("still waiting at mirror (no DONE yet)");
                    foreach (var b in MirrorStation.Instance.Board.buttons) if (b.id == "save") { WorldPanel.DebugPoint = b.t.position; WorldPanel.DebugPress = false; } break;
                case 4 when t > 8.4: step++; WorldPanel.DebugPress = true; break;
                case 5 when t > 9.0: step++; WorldPanel.DebugPoint = null; Log("after DONE"); Shot("flow_walk_card"); break;
                case 6 when t > 11.0: step++; Log("no walking yet"); PlayerDuck.DebugWalk = new Vector3(0f, 0f, 1f); break;
                case 7 when t > 14.5: step++; PlayerDuck.DebugWalk = null; Log("after walking"); Shot("flow_turn_card"); break;
                case 8 when t > 15.0: step++;
                    TutorialCoach.HideAll();
                    Debug.Log($"[Microduck] flow: grabbables in the house = {Grabbable.All.Count}, ducks = {DuckBrain.All.Count}");
                    FlockSpawner.Add(); break;
                case 9 when t > 15.6: step++; Debug.Log($"[Microduck] flow: ducks after ADD = {DuckBrain.All.Count}"); Shot("flow_new_duck", -0.1f); bag.Open(); break;
                case 10 when t > 16.4: step++; Shot("flow_bag"); bag.Choose("help"); break;
                case 11 when t > 17.2: step++; Shot("flow_help"); TutorialCoach.HideAll(); FlockSpawner.Remove(); break;
                case 12 when t > 17.8: step++; Debug.Log($"[Microduck] flow: ducks after REMOVE = {DuckBrain.All.Count}"); EditorApplication.update -= Tick; Debug.Log("[Microduck] flow test done"); break;
            }
        }
    }
}
