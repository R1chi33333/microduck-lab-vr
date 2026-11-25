using MicroduckVR.Duck;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Play mode check for the living-room switchback: drops the player on the landing and walks it up
    /// flight 2 to the tabletop, logging capsule height, body height, foot heights and the ramp surface,
    /// with side shots on the way. Output: Logs/microduck_cmd.log via Debug.Log and Logs/shots.
    /// </summary>
    public static class RampTest
    {
        static double t0; static double nextLog; static int shot;

        [MenuItem("Microduck/Debug/Ramp Walk Test")]
        public static void Run()
        {
            if (!Application.isPlaying || PlayerDuck.Instance == null) { Debug.Log("[Microduck] RampTest needs Play mode"); return; }
            var pd = PlayerDuck.Instance;
            pd.Teleport(new Vector3(-2.4f, 0.45f, -0.3f), Mathf.PI / 2f);
            PlayerDuck.DebugWalk = Vector3.right;
            t0 = EditorApplication.timeSinceStartup; nextLog = 0; shot = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance;
            double t = EditorApplication.timeSinceStartup - t0;
            if (!Application.isPlaying || pd == null || t > 7.0) { PlayerDuck.DebugWalk = null; EditorApplication.update -= Tick; Debug.Log("[Microduck] ramp test done"); return; }
            var b = pd.Body; var m = pd.GetComponent<DuckSetup>().Mover.transform.position;
            if (t >= nextLog)
            {
                nextLog += 0.5;
                var L = b.Legs["left"].foot.pos; var R = b.Legs["right"].foot.pos;
                float surf = WorldQueries.GroundAt(m.x, m.z, 2f);
                Debug.Log($"[Microduck] ramp t={t:F1} x={m.x:F2} mover.y={m.y:F3} base={b.BaseY:F3} footL={L.y:F3} footR={R.y:F3} surfTop={surf:F3} probe={WorldQueries.GroundNear(m.x, m.z, m.y):F3}");
            }
            if ((shot == 0 && t > 2.0) || (shot == 1 && t > 4.0))
            {
                shot++;
                DebugCapture.Shoot($"ramp_side_{shot}", new Vector3(m.x, m.y + 0.18f, m.z - 0.75f), new Vector3(m.x, m.y + 0.05f, m.z), 1280, 800, 40f);
                var cam = pd.cam.transform;
                DebugCapture.Shoot($"ramp_fp_{shot}", cam.position, cam.position + cam.forward * 0.6f + Vector3.down * 0.45f, 1280, 960, 90f, pd.cam.cullingMask);
            }
        }
    }
}
