using MicroduckVR.Flock;
using MicroduckVR.Player;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Six fixed stills at duck eye height, for before/after comparisons of the look (Logs/shots/look_*).</summary>
    public static class LookShots
    {
        public static string Prefix = "look";

        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { Debug.Log("[Microduck] look shots need Play mode"); return; }
            TutorialCoach.HideAll();
            int m = pd.cam.cullingMask;
            if (DuckBrain.All.Count > 0) DuckBrain.All[0].Teleport(new Vector3(4.5f, 0f, 0f), Mathf.PI * 0.1f);
            Shot("1_table", new Vector3(-1f, 0.2f, 1.6f), new Vector3(0f, 0.12f, 0.2f), m);
            Shot("2_close", new Vector3(-1.05f, 0.22f, 1.35f), new Vector3(-0.8f, 0.08f, 1.0f), m);
            Shot("3_wall", new Vector3(0f, 0.2f, 2f), new Vector3(0f, 0.35f, -2.4f), m);
            Shot("4_garden", new Vector3(0f, 0.2f, 5.5f), new Vector3(0.2f, 0.12f, 7.5f), m);
            Shot("5_kitchen", new Vector3(-4.3f, 0.2f, 1.2f), new Vector3(-5.5f, 0.4f, 0f), m);
            Shot("6_duck", new Vector3(4.5f, 0.2f, 0.55f), new Vector3(4.5f, 0.14f, 0f), m);
            Debug.Log("[Microduck] look shots done: " + Prefix);
        }

        static void Shot(string n, Vector3 from, Vector3 at, int mask) => DebugCapture.Shoot(Prefix + "_" + n, from, at, 1600, 900, 70f, mask);
    }
}
