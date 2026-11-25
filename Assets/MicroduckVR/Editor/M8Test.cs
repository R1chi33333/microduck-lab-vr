using MicroduckVR.Custom;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of M8: the mirror (reflection, RANDOM, SAVE), the garage (third person, a part, REPLACE), credits.</summary>
    public static class M8Test
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/M8 Test")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.HideAll(); pd.SetThirdPerson(false);
            pd.Teleport(new Vector3(4.42f, 0.05f, -1.85f), Mathf.PI);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Shot(string n) { var pd = PlayerDuck.Instance; var c = pd.cam.transform; DebugCapture.Shoot(n, c.position, c.position + c.forward * 0.5f + Vector3.up * 0.12f, 1280, 960, 90f, pd.cam.cullingMask); }
        static void Click(WorldPanel p, string id) { foreach (var b in p.buttons) if (b.id == id) { WorldPanel.DebugPoint = b.t.position; WorldPanel.DebugPress = false; return; } }

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; WorldPanel.DebugPoint = null; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var mirror = MirrorStation.Instance; var garage = Garage.Instance;
            switch (step)
            {
                case 0 when t > 1.0: step++; Shot("m8_mirror"); Click(mirror.Board, "random"); break;
                case 1 when t > 1.4: step++; WorldPanel.DebugPress = true; break;
                case 2 when t > 2.0: step++; WorldPanel.DebugPoint = null; Shot("m8_mirror_random"); Debug.Log("[Microduck] m8: skin now " + pd.GetComponent<MicroduckVR.Duck.DuckSetup>().skin); break;
                case 3 when t > 2.5: step++; pd.Teleport(new Vector3(-2.2f, 0.05f, 1.78f), Mathf.PI); break;
                case 4 when t > 3.5: step++; DebugCapture.Shoot("m8_garage", pd.cam.transform.position, pd.cam.transform.position + pd.cam.transform.forward, 1280, 960, 90f, pd.cam.cullingMask); Click(garage.Board, "row_0"); break;
                case 5 when t > 3.9: step++; WorldPanel.DebugPress = true; break;
                case 6 when t > 4.15: step++; DebugCapture.Shoot("m8_garage_flash", pd.cam.transform.position, pd.cam.transform.position + pd.cam.transform.forward, 1280, 960, 90f, pd.cam.cullingMask); Click(garage.Board, "replace"); break;
                case 7 when t > 4.5: step++; WorldPanel.DebugPress = true; break;
                case 8 when t > 4.65: step++; DebugCapture.Shoot("m8_garage_pop", pd.cam.transform.position, pd.cam.transform.position + pd.cam.transform.forward, 1280, 960, 90f, pd.cam.cullingMask); WorldPanel.DebugPoint = null; break;
                case 9 when t > 5.5: step++; pd.Teleport(new Vector3(-0.2f, 0.05f, 1.5f), Mathf.PI); TutorialCoach.ShowSheet("CREDITS", TutorialCoach.CreditsText, "Y TO CLOSE"); break;
                case 10 when t > 6.5: step++; Shot("m8_credits"); TutorialCoach.HideAll(); EditorApplication.update -= Tick; Debug.Log("[Microduck] m8 test done"); break;
            }
        }
    }
}
