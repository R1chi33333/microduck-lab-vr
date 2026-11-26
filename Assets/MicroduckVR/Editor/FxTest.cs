using System.IO;
using MicroduckVR.Player;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Play mode check of the skate effects: a lap of drifting on the kitchen tiles with a boost and a hard stop,
    /// stills through the player's own eyes (with the vignette and speed lines) and from the side (Logs/shots/fx_*).</summary>
    public static class FxTest
    {
        static double t0; static int shot; static readonly Vector3 C = new Vector3(-5.0f, 0f, -1.0f);
        static readonly float[] At = { 1.0f, 1.6f, 2.8f, 3.3f, 3.8f, 4.3f, 4.8f, 5.6f, 5.9f, 6.2f };

        public static void Run()
        {
            var pd = PlayerDuck.Instance; if (!Application.isPlaying || pd == null) return;
            TutorialCoach.HideAll(); pd.SetThirdPerson(false);
            pd.Teleport(new Vector3(-5.6f, 0.05f, -1.0f), Mathf.PI * 0.5f);
            if (!pd.Skates.On) pd.Skates.Toggle(pd.Body.Yaw, Vector3.zero);
            t0 = EditorApplication.timeSinceStartup; shot = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            var pd = PlayerDuck.Instance;
            if (!Application.isPlaying || pd == null) { Done(); return; }
            float t = (float)(EditorApplication.timeSinceStartup - t0);
            var p = pd.Body.Pos;
            if (t < 5.4f)
            {
                float a = t * 1.25f;
                var d = C + new Vector3(Mathf.Cos(a) * 0.75f, 0f, Mathf.Sin(a) * 0.5f) - p; d.y = 0f;
                PlayerDuck.DebugWalk = d.normalized;
                PlayerSkates.DebugDrift = t > 2.5f ? 1f : (float?)null;
                PlayerSkates.DebugBoost = t > 1.3f && t < 1.8f ? 1f : (float?)null;
            }
            else { PlayerSkates.DebugDrift = null; PlayerSkates.DebugBoost = null; var v = pd.Skates.Velocity; v.y = 0f; PlayerDuck.DebugWalk = v.sqrMagnitude > 1e-4f ? -v.normalized : (Vector3?)null; }
            if (shot < At.Length && t > At[shot])
            {
                var c = pd.cam.transform;
                Eye($"fx_{shot}_eye", c.position, c.position + c.forward, pd.cam.cullingMask);
                var v = pd.Skates.Velocity; v.y = 0f; var f = v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward; var s = new Vector3(f.z, 0f, -f.x);
                DebugCapture.Shoot($"fx_{shot}_side", p - s * 0.6f + f * 0.1f + Vector3.up * 0.14f, p + Vector3.up * 0.12f, 1280, 720, 56f, ~0);
                Debug.Log($"[Microduck] fx shot {shot} t={t:F1} speed={pd.Skates.Speed:F2} drift={pd.Skates.Drifting} boost={pd.Skates.Boosting}");
                shot++;
            }
            if (t > 6.6f) Done();
        }

        static void Done()
        {
            EditorApplication.update -= Tick;
            PlayerDuck.DebugWalk = null; PlayerSkates.DebugDrift = null; PlayerSkates.DebugBoost = null;
            Debug.Log("[Microduck] fx test done");
        }

        /// <summary>Through the player's eyes, keeping the vignette (where the speed lines are drawn).</summary>
        static void Eye(string name, Vector3 from, Vector3 at, int mask)
        {
            Directory.CreateDirectory("Logs/shots");
            var go = new GameObject("EyeCam"); var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 96f; cam.nearClipPlane = 0.005f; cam.farClipPlane = 90f; cam.cullingMask = mask;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = MicroduckVR.Comic.ComicLook.Hex("#9AD8F2");
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            go.transform.position = from; go.transform.LookAt(at);
            var rt = new RenderTexture(1280, 1280, 24) { antiAliasing = 4 }; cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt; var tex = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine("Logs/shots", name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }
}
