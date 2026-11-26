using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the lawn: duck-eye views, the wind, grass parting around the walking duck, clippings.</summary>
    public static class GrassTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Grass Test")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.HideAll(); pd.SetThirdPerson(false);
            pd.Teleport(new Vector3(0.9f, 0.05f, 4.9f), 0f);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; PlayerDuck.DebugWalk = null; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            int cm = pd.cam.cullingMask; var eye = pd.cam.transform.position;
            switch (step)
            {
                case 0 when t > 1.5:
                    step++;
                    Debug.Log($"[Microduck] grass: eye height {eye.y:F3} fps={1f / Time.smoothDeltaTime:F0}");
                    // first person, from the duck's own eye, looking down the garden and across it
                    DebugCapture.Shoot("grass_fp_forward", eye, eye + new Vector3(0.1f, -0.06f, 1f), 1280, 960, 90f, cm);
                    DebugCapture.Shoot("grass_fp_across", eye, eye + new Vector3(-1f, -0.1f, 0.5f), 1280, 960, 90f, cm);
                    DebugCapture.Shoot("grass_fp_down", eye, eye + new Vector3(0.2f, -0.5f, 1f), 1280, 960, 90f, cm);
                    DebugCapture.Shoot("grass_close", new Vector3(0.2f, 0.07f, 5.0f), new Vector3(0.35f, 0.02f, 5.3f), 1280, 960, 60f, cm);
                    DebugCapture.Shoot("grass_garden", new Vector3(0f, 1.1f, 3.0f), new Vector3(0f, 0f, 6.2f), 1280, 960, 70f, cm);
                    PlayerDuck.DebugWalk = new Vector3(-0.5f, 0f, 1f);
                    break;
                case 1 when t > 3.0:
                    step++;
                    var p = pd.Body.Pos;
                    // the duck wading through: third person shots so the parted grass and the clippings show
                    DebugCapture.Shoot("grass_walk_side", p + new Vector3(0.32f, 0.16f, -0.1f), p + Vector3.up * 0.04f, 1280, 960, 60f, ~0);
                    DebugCapture.Shoot("grass_walk_top", p + new Vector3(0.05f, 0.6f, -0.25f), p, 1280, 960, 60f, ~0);
                    DebugCapture.Shoot("grass_fp_walk", eye, eye + new Vector3(-0.5f, -0.12f, 1f), 1280, 960, 90f, cm);
                    break;
                case 2 when t > 3.4:
                    step++; PlayerDuck.DebugWalk = null; EditorApplication.update -= Tick; Debug.Log("[Microduck] grass test done"); break;
            }
        }
    }
}
