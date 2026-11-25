using System.IO;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Renders debug screenshots to Logs/shots/ from a temporary additive scene, so the open scene
    /// is never touched. Used to check poses and looks without the headset.
    /// </summary>
    public static class DebugCapture
    {
        const string ShotDir = "Logs/shots";

        [MenuItem("Microduck/Debug/Capture Duck Walk")]
        public static void DuckWalk()
        {
            var prev = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Hard;
                sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = Vector3.one * 0.3f;

                var duck = SpawnDuck(new Vector3(0f, 0f, 0f), 0f);
                if (duck == null) return;

                // stand pose shot
                Shoot("duck_stand_front", new Vector3(0.05f, 0.16f, 0.45f), new Vector3(0f, 0.11f, 0f));
                Shoot("duck_stand_side", new Vector3(0.45f, 0.14f, 0.02f), new Vector3(0f, 0.11f, 0f));

                // walk forward while turning, 1.2 s at 90 Hz
                float dt = 1f / 90f, yaw = 0f, speed = Gait.MaxSpeed * 0.8f, yawRate = 0.6f;
                Vector3 pos = Vector3.zero;
                for (int i = 0; i < 108; i++)
                {
                    yaw += yawRate * dt;
                    Vector3 vel = GltfSpace.Forward(yaw) * speed;
                    pos += vel * dt;
                    duck.Step(dt, pos, yaw, vel, yawRate);
                }
                Vector3 c = duck.transform.position + Vector3.up * 0.1f;
                Shoot("duck_walk_side", c + new Vector3(0.42f, 0.05f, 0.05f), c);
                Shoot("duck_walk_front", c + GltfSpace.Forward(yaw) * 0.42f + Vector3.up * 0.06f, c);

                // reach down for a point in front
                duck.ReachState.target = duck.transform.position + GltfSpace.Forward(yaw) * 0.09f + Vector3.up * 0.01f;
                duck.ReachState.weight = 1f; duck.JawTarget = 0.5f;
                for (int i = 0; i < 120; i++) duck.Step(dt, pos, yaw, Vector3.zero, 0f);
                Shoot("duck_reach_side", c + Quaternion.AngleAxis(90f, Vector3.up) * GltfSpace.Forward(yaw) * 0.42f, c - Vector3.up * 0.03f);
                Debug.Log($"[Microduck] capture done: reach err={duck.ReachState.err:F3} m, leg err L={duck.Legs["left"].err:F4} R={duck.Legs["right"].err:F4}");
            }
            finally
            {
                SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>Play mode only: first-person view from the player's eye, plus an overview of the room.</summary>
        [MenuItem("Microduck/Debug/Capture Play Views")]
        public static void PlayViews()
        {
            if (!Application.isPlaying) { Debug.Log("[Microduck] PlayViews needs Play mode"); return; }
            var pd = Object.FindFirstObjectByType<MicroduckVR.Player.PlayerDuck>();
            if (pd == null || pd.cam == null) { Debug.Log("[Microduck] no PlayerDuck"); return; }
            var t = pd.cam.transform;
            string stamp = System.DateTime.Now.ToString("HHmmss");
            Shoot("fp_" + stamp, t.position, t.position + t.forward, 1280, 960, 90f, pd.cam.cullingMask);
            Shoot("fp_down_" + stamp, t.position, t.position + t.forward * 0.6f + Vector3.down * 0.5f, 1280, 960, 90f, pd.cam.cullingMask);
            Shoot("overview_" + stamp, new Vector3(2.3f, 1.9f, 2.2f), new Vector3(0f, 0.1f, 0.2f), 1280, 800, 60f);
            var b = pd.Body;
            Debug.Log($"[Microduck] play views {stamp}: eye={t.position.ToString("F3")} duck={b.transform.position.ToString("F3")} yaw={b.Yaw:F2} originScale={pd.xrOrigin.localScale.x:F2} fps={1f / Time.smoothDeltaTime:F0} xr={UnityEngine.XR.XRSettings.isDeviceActive}");
        }

        public static DuckBody SpawnDuck(Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            if (prefab == null || map == null) { Debug.LogError("[Microduck] rig or joint map missing: run Microduck/Import Duck Rig"); return null; }
            var root = new GameObject("Duck");
            root.transform.SetPositionAndRotation(pos, Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up));
            var rig = (GameObject)Object.Instantiate(prefab);
            var body = root.AddComponent<DuckBody>();
            body.Init(rig, map);
            return body;
        }

        public static void Shoot(string name, Vector3 from, Vector3 lookAt, int w = 960, int h = 720, float fov = 40f, int cullingMask = ~0)
        {
            Directory.CreateDirectory(ShotDir);
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.005f; cam.farClipPlane = 50f; cam.cullingMask = cullingMask;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1f, 0.97f, 0.91f);
            go.transform.position = from; go.transform.LookAt(lookAt);
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            File.WriteAllBytes(Path.Combine(ShotDir, name + ".png"), tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }
}
