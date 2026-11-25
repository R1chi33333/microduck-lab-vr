using MicroduckVR.Comic;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the comic pass: thicker ink, the halftone sky, the puddle, sound-word pops, rooms.</summary>
    public static class ComicTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Comic Test")]
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
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            int cm = pd.cam.cullingMask; var eye = pd.cam.transform.position;
            switch (step)
            {
                case 0 when t > 1.5:
                    step++;
                    Debug.Log($"[Microduck] comic: fps={1f / Time.smoothDeltaTime:F0}");
                    DebugCapture.Shoot("comic_fp_forward", eye, eye + new Vector3(0.1f, -0.06f, 1f), 1280, 960, 90f, cm);
                    DebugCapture.Shoot("comic_fp_sky", eye, eye + new Vector3(0.3f, 0.45f, 1f), 1280, 960, 90f, cm);
                    DebugCapture.Shoot("comic_close", new Vector3(0.2f, 0.07f, 5.0f), new Vector3(0.35f, 0.02f, 5.3f), 1280, 960, 60f, cm);
                    DebugCapture.Shoot("comic_garden", new Vector3(0f, 1.1f, 3.0f), new Vector3(0f, 0f, 6.2f), 1280, 960, 70f, cm);
                    DebugCapture.Shoot("comic_puddle", new Vector3(0.7f, 0.3f, 4.5f), new Vector3(1.45f, 0f, 5.2f), 1280, 960, 60f, cm);
                    ComicPop.Spawn("QUACK!", eye + new Vector3(-0.08f, 0.02f, 0.32f), "#FFC43D");
                    ComicPop.Spawn("POW!", eye + new Vector3(0.12f, -0.06f, 0.42f), "#FF8A3D", 1.2f);
                    break;
                case 1 when t > 1.9:
                    step++;
                    DebugCapture.Shoot("comic_pops", eye, eye + new Vector3(0.05f, -0.06f, 1f), 1280, 960, 90f, cm);
                    pd.Teleport(new Vector3(-0.2f, 0.05f, 1.5f), Mathf.PI);
                    break;
                case 2 when t > 3.0:
                    step++;
                    DebugCapture.Shoot("comic_room", new Vector3(-0.2f, 1.6f, 2.6f), new Vector3(0f, 0f, 0f), 1280, 960, 80f, cm);
                    DebugCapture.Shoot("comic_room_eye", eye, eye + new Vector3(0.3f, 0.02f, -1f), 1280, 960, 90f, cm);
                    EditorApplication.update -= Tick; Debug.Log("[Microduck] comic test done"); break;
            }
        }
    }
}
