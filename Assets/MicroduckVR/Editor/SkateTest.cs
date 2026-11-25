using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode skate check on the lawn: skates on, push and boost along +x, then a held drift curving
    /// towards +z and a slingshot on release. Logs speed, drift and lean; shots of the pose, smoke, sparks and skids.</summary>
    public static class SkateTest
    {
        static double t0, nextLog; static int shot;

        [MenuItem("Microduck/Debug/Skate Test")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) { Debug.Log("[Microduck] SkateTest needs Play mode"); return; }
            var pd = PlayerDuck.Instance;
            pd.Teleport(new Vector3(-2.6f, 0.05f, 5.0f), Mathf.PI / 2f);
            if (!pd.Skates.On) pd.Skates.Toggle(Mathf.PI / 2f, Vector3.zero);
            PlayerDuck.DebugWalk = Vector3.right; PlayerSkates.DebugBoost = null; PlayerSkates.DebugDrift = null;
            t0 = EditorApplication.timeSinceStartup; nextLog = 0; shot = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance;
            if (!Application.isPlaying || pd == null) { EditorApplication.update -= Tick; Clear(); return; }
            double t = EditorApplication.timeSinceStartup - t0;
            var sk = pd.Skates; var b = pd.Body; var p = b.transform.position;
            if (t > 0.8 && t < 1.0) PlayerSkates.DebugBoost = 1f; else PlayerSkates.DebugBoost = null;
            if (t > 2.0 && t < 3.4) { PlayerSkates.DebugDrift = 1f; PlayerDuck.DebugWalk = Vector3.forward; }
            else if (t >= 3.4 && t < 4.2) { PlayerSkates.DebugDrift = null; PlayerDuck.DebugWalk = Vector3.forward; }
            else if (t >= 4.2) { PlayerDuck.DebugWalk = null; }
            if (t >= nextLog)
            {
                nextLog += 0.25;
                Debug.Log($"[Microduck] skate t={t:F2} pos=({p.x:F2},{p.y:F3},{p.z:F2}) speed={sk.Speed:F2} drift={sk.Drifting} boost={sk.Boosting} yaw={b.Yaw * Mathf.Rad2Deg:F0} lean={b.SkateLean:F2} skateW={b.SkateW:F2}");
            }
            var f = new Vector3(Mathf.Sin(b.Yaw), 0f, Mathf.Cos(b.Yaw));
            if (shot == 0 && t > 1.3) { shot++; DebugCapture.Shoot("skate_boost", p - f * 0.55f + Vector3.up * 0.2f + Vector3.Cross(Vector3.up, f) * 0.25f, p + Vector3.up * 0.08f, 1280, 800, 50f); }
            if (shot == 1 && t > 2.8) { shot++; DebugCapture.Shoot("skate_drift", p + f * 0.5f + Vector3.up * 0.18f + Vector3.Cross(Vector3.up, f) * 0.3f, p + Vector3.up * 0.07f, 1280, 800, 55f); }
            if (shot == 2 && t > 3.2) { shot++; var cam = pd.cam.transform; DebugCapture.Shoot("skate_fp_down", cam.position, cam.position + f * 0.4f + Vector3.down * 0.45f, 1280, 960, 90f, pd.cam.cullingMask); }
            if (shot == 3 && t > 5.0) { shot++; DebugCapture.Shoot("skate_skids", new Vector3(-1.2f, 2.2f, 5.5f), new Vector3(-1.0f, 0f, 5.6f), 1280, 800, 60f); }
            if (t > 6.5) { EditorApplication.update -= Tick; Clear(); Debug.Log("[Microduck] skate test done"); }
        }

        static void Clear() { PlayerDuck.DebugWalk = null; PlayerSkates.DebugBoost = null; PlayerSkates.DebugDrift = null; }
    }
}
