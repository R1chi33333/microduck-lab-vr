using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode: restart the tutorial and shoot its card, walk to pass step 2, then the help sheet and the bag.</summary>
    public static class TutorialTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Tutorial Test")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) return;
            var pd = PlayerDuck.Instance; pd.Teleport(new Vector3(-0.2f, 0.05f, 1.6f), Mathf.PI); pd.SetThirdPerson(false);
            TutorialCoach.Restart();
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Shot(string n) { var pd = PlayerDuck.Instance; var c = pd.cam.transform; DebugCapture.Shoot(n, c.position, c.position + c.forward, 1280, 960, 90f, pd.cam.cullingMask); }

        static void Tick()
        {
            var pd = PlayerDuck.Instance;
            if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - t0;
            if (step == 0 && t > 1.0) { step++; Shot("tut_1"); }
            else if (step == 1 && t > 6.5) { step++; Shot("tut_2"); var f = pd.cam.transform.forward; f.y = 0f; PlayerDuck.DebugWalk = f.normalized; }
            else if (step == 2 && t > 10) { step++; PlayerDuck.DebugWalk = null; Shot("tut_3"); }
            else if (step == 3 && t > 11) { step++; TutorialCoach.ShowHelp(); }
            else if (step == 4 && t > 12) { step++; Shot("tut_help"); TutorialCoach.HideAll(); pd.GetComponent<BagMenu>().Open(); }
            else if (step == 5 && t > 13) { step++; Shot("tut_bag"); pd.GetComponent<BagMenu>().Close(); EditorApplication.update -= Tick; Debug.Log("[Microduck] tutorial test done"); }
        }
    }
}
