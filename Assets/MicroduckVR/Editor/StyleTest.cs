using MicroduckVR.Games;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;


namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the comic style pass: grass at duck height, garden and backdrop, floors, laser, bag, race gates.</summary>
    public static class StyleTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Style Test")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.HideAll(); pd.SetThirdPerson(false);
            pd.Teleport(new Vector3(0.6f, 0.05f, 4.4f), Mathf.PI);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Eye(string n, float up = 0f)
        {
            var c = PlayerDuck.Instance.cam;
            DebugCapture.Shoot(n, c.transform.position, c.transform.position + c.transform.forward + Vector3.up * up, 1280, 960, 90f, c.cullingMask);
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; LaserPointer.DebugPoint = null; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var cm = pd.cam.cullingMask;
            switch (step)
            {
                case 0 when t > 1.5:
                    step++;
                    Eye("style_grass_eye");
                    DebugCapture.Shoot("style_grass_close", new Vector3(0.3f, 0.05f, 4.5f), new Vector3(0.45f, 0.02f, 4.85f), 1280, 960, 60f, cm);
                    DebugCapture.Shoot("style_garden", new Vector3(0f, 1.1f, 3.0f), new Vector3(0f, 0f, 6.2f), 1280, 960, 70f, cm);
                    DebugCapture.Shoot("style_backdrop", new Vector3(0f, 0.35f, 7.0f), new Vector3(0f, 1.2f, 20f), 1280, 960, 80f, cm);
                    Debug.Log($"[Microduck] style fps={1f / Time.smoothDeltaTime:F0}");
                    break;
                case 1 when t > 2.0:
                    step++; pd.Teleport(new Vector3(-0.2f, 0.05f, 1.5f), Mathf.PI); break;
                case 2 when t > 3.0:
                    step++; Eye("style_room_eye", 0.15f);
                    DebugCapture.Shoot("style_room_high", new Vector3(-0.2f, 1.6f, 2.6f), new Vector3(0f, 0f, 0f), 1280, 960, 80f, cm);
                    LaserPointer.DebugPoint = new Vector3(-0.1f, 0f, 1.15f);
                    break;
                case 3 when t > 3.5:
                    step++; DebugCapture.Shoot("style_laser", new Vector3(-0.45f, 0.25f, 1.6f), new Vector3(-0.1f, 0.02f, 1.15f), 1280, 960, 60f, cm);
                    LaserPointer.DebugPoint = null;
                    pd.GetComponent<BagMenu>().Open();
                    break;
                case 4 when t > 4.2:
                    step++; Eye("style_bag"); pd.GetComponent<BagMenu>().Close();
                    RaceGame.Ensure().StartRace();
                    break;
                case 5 when t > 5.5:
                    step++; Eye("style_race", 0.1f);
                    var g = GameObject.Find("Race gate 1");
                    if (g != null) DebugCapture.Shoot("style_gate", g.transform.position + g.transform.forward * -0.5f + Vector3.up * 0.22f + g.transform.right * 0.15f, g.transform.position + Vector3.up * 0.12f, 1280, 960, 60f, cm);
                    break;
                case 6 when t > 6.0:
                    step++; RaceGame.Ensure().StopRace();
                    DebugCapture.Shoot("style_kitchen_door", new Vector3(2.2f, 0.4f, -1.2f), new Vector3(5f, 0.6f, -1.2f), 1280, 960, 70f, cm);
                    DebugCapture.Shoot("style_flowers", new Vector3(1.6f, 0.18f, 4.6f), new Vector3(2.2f, 0.08f, 6.2f), 1280, 960, 70f, cm);
                    LaserPointer.DebugPoint = new Vector3(-0.1f, 0f, 1.15f);
                    break;
                case 7 when t > 6.5:
                    step++; DebugCapture.Shoot("style_laser2", new Vector3(-0.45f, 0.25f, 1.6f), new Vector3(-0.1f, 0.02f, 1.15f), 1280, 960, 60f, cm);
                    LaserPointer.DebugPoint = null; EditorApplication.update -= Tick; Debug.Log("[Microduck] style test done"); break;
            }
        }
    }
}
