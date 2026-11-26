using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode: switch to third person, shoot the view standing and while walking, then switch back.</summary>
    public static class ViewTest
    {
        static double t0; static int step;

        [MenuItem("Microduck/Debug/Third Person Test")]
        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            pd.Teleport(new Vector3(-0.2f, 0.05f, 1.6f), Mathf.PI);
            pd.SetThirdPerson(true);
            t0 = EditorApplication.timeSinceStartup; step = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; return; }
            double t = EditorApplication.timeSinceStartup - t0; var c = pd.cam.transform;
            if (step == 0 && t > 0.6) { step = 1; DebugCapture.Shoot("third_stand", c.position, c.position + c.forward, 1280, 960, 90f, pd.cam.cullingMask); var fw = c.forward; fw.y = 0f; PlayerDuck.DebugWalk = fw.normalized; }
            if (step == 1 && t > 2.2) { step = 2; DebugCapture.Shoot("third_walk", c.position, c.position + c.forward, 1280, 960, 90f, pd.cam.cullingMask); PlayerDuck.DebugWalk = null; }
            if (step == 2 && t > 2.6) { step = 3; Debug.Log($"[Microduck] third person: cam={c.position.ToString("F3")} duck={pd.Body.transform.position.ToString("F3")}"); pd.SetThirdPerson(false); EditorApplication.update -= Tick; }
        }
    }
}
