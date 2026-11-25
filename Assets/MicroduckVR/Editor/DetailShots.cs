using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Play mode: close-up shots of the house props at duck height, plus a push test (the player walks into the
    /// football on the centre spot and the ball's travel is logged), with shots of the train a moment apart.
    /// </summary>
    public static class DetailShots
    {
        static double t0; static int step; static Transform ball; static Vector3 ball0;

        [MenuItem("Microduck/Debug/Detail Shots")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) { Debug.Log("[Microduck] DetailShots needs Play mode"); return; }
            void S(string n, Vector3 from, Vector3 to, float fov = 60f) => DebugCapture.Shoot("detail_" + n, from, to, 1280, 800, fov);
            S("pitch", new Vector3(1.93f, 0.25f, -0.2f), new Vector3(1.93f, 0f, 1.0f));
            S("goal", new Vector3(1.75f, 0.15f, 1.55f), new Vector3(1.93f, 0.07f, 2.0f), 55f);
            S("bookcase", new Vector3(1.6f, 0.7f, 1.5f), new Vector3(2.75f, 0.6f, 1.5f), 65f);
            S("teddy", new Vector3(0.3f, 0.2f, 0.75f), new Vector3(0.3f, 0.12f, 1.35f), 50f);
            S("tv", new Vector3(-0.9f, 0.45f, 1.1f), new Vector3(-1.1f, 0.5f, 2.3f), 60f);
            S("floor_toys", new Vector3(0.2f, 0.18f, -0.9f), new Vector3(0.2f, 0f, 0.3f), 70f);
            S("train", new Vector3(4.95f, 0.45f, -0.1f), new Vector3(4.95f, 0f, 0.9f), 60f);
            S("bedroom", new Vector3(3.6f, 0.35f, -1.5f), new Vector3(5.5f, 0.1f, 1.0f), 75f);
            S("kitchen", new Vector3(-4.3f, 1.2f, -1.2f), new Vector3(-4.5f, 0.9f, -2.3f), 70f);
            S("hall", new Vector3(0.8f, 0.3f, 3.2f), new Vector3(3.0f, 0.1f, 3.4f), 70f);
            S("garden", new Vector3(0.0f, 0.5f, 4.4f), new Vector3(1.0f, 0.1f, 6.8f), 70f);
            foreach (var g in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                if (g.name == "Football" && Vector3.Distance(g.position, new Vector3(1.93f, 0.04f, 0.73f)) < 0.1f) ball = g.transform;
            ball0 = ball != null ? ball.position : Vector3.zero;
            PlayerDuck.Instance.Teleport(new Vector3(1.93f, 0.05f, 0.42f), 0f);
            PlayerDuck.DebugWalk = Vector3.forward;
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            double t = EditorApplication.timeSinceStartup - t0;
            if (!Application.isPlaying) { EditorApplication.update -= Tick; PlayerDuck.DebugWalk = null; return; }
            if (step == 0 && t > 1.0) { step = 1; DebugCapture.Shoot("detail_train2", new Vector3(4.95f, 0.45f, -0.1f), new Vector3(4.95f, 0f, 0.9f), 1280, 800, 60f); }
            if (step == 1 && t > 2.5) { step = 2; DebugCapture.Shoot("detail_push", new Vector3(2.4f, 0.3f, 0.6f), new Vector3(1.93f, 0.02f, 1.0f), 1280, 800, 60f); }
            if (step == 2 && t > 4.0)
            {
                step = 3; PlayerDuck.DebugWalk = null; EditorApplication.update -= Tick;
                var duck = PlayerDuck.Instance.Body.transform.position;
                Debug.Log($"[Microduck] push test: ball {ball0.ToString("F3")} -> {(ball != null ? ball.position.ToString("F3") : "missing")} duck at {duck.ToString("F3")}");
            }
        }
    }
}
