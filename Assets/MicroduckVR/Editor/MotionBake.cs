using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Microduck > Assets > Bake Duck Moves: plays every move of the procedural duck body (DuckBody) in a little
    /// studio, frame by frame at 30 fps, and records it twice:
    ///   - as Unity animation clips for the static duck prefab (Prefabs/Ducks/Animations/Duck_*.anim), an Animator
    ///     controller with one trigger per move, and "Duck (animated).prefab" ready to drop in a scene;
    ///   - as numbered frames in Logs/motion/&lt;move&gt;/ for the README's animated pictures.
    /// The game itself keeps animating the ducks in code; the clips are for reuse elsewhere.
    /// </summary>
    public static partial class AssetExport
    {
        const string AnimDir = Root + "/Ducks/Animations";
        const string MotionFrames = "Logs/motion";
        const float Fps = 30f;
        const int GifSize = 480;

        class Move
        {
            public string id, title;
            public float warm, len;          // seconds simulated before recording, seconds recorded
            public bool loop, keepTurn;      // keepTurn: the clip turns the duck round (Turn); otherwise it stays in place
            public float camAngle = 35f;     // degrees round from the duck's front
            public Action<float, DuckBody, Ctl> drive;
        }

        class Ctl { public float v, w; public Vector3 dir; }

        static List<Move> Moves() => new List<Move>
        {
            new Move { id = "Idle", title = "IDLE", warm = 1f, len = 3.7f, loop = true, drive = (t, b, c) => { } },
            new Move { id = "Walk", title = "WALK", warm = 1.5f, len = 0f, loop = true, camAngle = 70f, drive = (t, b, c) => c.v = Gait.MaxSpeed * 0.8f },
            new Move { id = "Turn", title = "TURN ON THE SPOT", warm = 0.6f, len = 3f, loop = true, keepTurn = true, drive = (t, b, c) => c.w = Mathf.PI * 2f / 3f },
            new Move { id = "Sit", title = "SIT", warm = 0.3f, len = 3.6f, drive = (t, b, c) => b.SitTarget = t > 0.1f && t < 2.4f },
            new Move { id = "Crouch", title = "CROUCH", warm = 0.3f, len = 2.2f, drive = (t, b, c) => b.CrouchTarget = t > 0.1f && t < 1.4f },
            new Move { id = "Hop", title = "HOP", warm = 0.3f, len = 2.2f, drive = (t, b, c) =>
                {
                    float u = (t - 0.1f) % 0.6f; bool on = t > 0.1f && t < 1.9f;
                    b.CrouchTarget = on && u < 0.2f;
                    b.Hop = on && u >= 0.2f ? Mathf.Sin(Mathf.PI * (u - 0.2f) / 0.4f) * 0.03f : 0f;
                } },
            new Move { id = "Bite", title = "REACH AND BITE", warm = 0.3f, len = 3.0f, camAngle = 80f, drive = (t, b, c) =>
                {
                    Vector3 toy = b.Pos + GltfSpace.Forward(b.Yaw) * 0.1f; toy.y = 0.012f;
                    bool reach = t > 0.1f && t < 2.0f;
                    b.ReachState.target = reach ? toy : (Vector3?)null; b.ReachState.weight = reach ? 1f : 0f;
                    b.JawTarget = t > 0.3f && t < 1.3f ? 0.5f : 0f;
                    b.JawStop = t >= 1.3f && t < 2.4f ? 0.12f : 0f;
                } },
            Emote("Nod", "NOD", 1.6f), Emote("Shake", "SHAKE", 1.6f), Emote("Look", "LOOK AROUND", 3.1f), Emote("Dance", "DANCE", 2.9f),
            new Move { id = "Wiggle", title = "HAPPY WIGGLE", warm = 0.3f, len = 1.8f, drive = (t, b, c) => { if (t < 0.05f) b.HappyWiggle = 1f; } },
            new Move { id = "FallGetUp", title = "FALL AND GET UP", warm = 0.3f, len = 4.2f, camAngle = 50f, drive = (t, b, c) =>
                { if (t >= 0.2f && t < 0.2f + 1f / Fps && !b.Fallen) b.StartTip(new Vector3(0.64f, 0f, -0.77f)); } },
        };

        static Move Emote(string id, string title, float len) => new Move
        {
            id = id, title = title, warm = 0.3f, len = len,
            drive = (t, b, c) => { if (t >= 0.1f && t < 0.1f + 1f / Fps) b.PlayEmote(id.ToLowerInvariant()); },
        };

        [MenuItem("Microduck/Assets/Bake Duck Moves")]
        public static void BakeDuckMoves()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Bake Duck Moves: leave Play mode first"); return; }
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            var duckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Ducks/Duck.prefab");
            if (glb == null || map == null) { Debug.LogError("[Microduck] Bake Duck Moves: duck rig or joint map missing"); return; }
            if (duckPrefab == null) { Debug.LogError("[Microduck] Bake Duck Moves: no Ducks/Duck.prefab (run Export Prefabs first)"); return; }

            // the prefab's own paths and the offset Export Prefabs gave the tilt node (pivot at the bottom centre)
            var prefabPaths = new HashSet<string>(duckPrefab.GetComponentsInChildren<Transform>(true).Select(t => AnimationUtility.CalculateTransformPath(t, duckPrefab.transform)));
            var prefabTilt = duckPrefab.transform.Find("duck_tilt");
            Vector3 tiltOffset = prefabTilt != null ? prefabTilt.localPosition : Vector3.zero;

            EnsureFolder(AnimDir);
            var clips = new List<(Move m, AnimationClip clip)>();
            int warnings = 0, frames = 0;
            var temp = OpenTemp();
            var lightsOff = new List<Light>();
            RenderTexture rt = null; Texture2D tex = null;
            try
            {
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var sc = SceneManager.GetSceneAt(s);
                    if (!sc.isLoaded || sc == temp.scene) continue;
                    foreach (var g in sc.GetRootGameObjects())
                        foreach (var l in g.GetComponentsInChildren<Light>()) if (l.enabled) { l.enabled = false; lightsOff.Add(l); }
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.74f, 0.80f, 0.98f);
                RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
                RenderSettings.ambientGroundColor = new Color(0.82f, 0.64f, 0.48f);
                RenderSettings.fog = false;
                var sun = new GameObject("studio sun").AddComponent<Light>();
                sun.transform.SetParent(temp.root, false);
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f;
                sun.color = new Color(1f, 0.96f, 0.88f); sun.cullingMask = 1 << CatalogLayer;
                sun.transform.rotation = Quaternion.Euler(55f, 150f, 0f);

                // a checked floor so walking reads as walking
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.name = "studio floor"; floor.layer = CatalogLayer;
                floor.transform.SetParent(temp.root, false);
                floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));
                floor.transform.localScale = new Vector3(12f, 12f, 1f);
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                var check = new Texture2D(2, 2, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                Color a = ComicLook.Hex("#F6E3C8"), bcol = ComicLook.Hex("#EED3B0");
                check.SetPixels(new[] { a, bcol, bcol, a }); check.Apply();
                var fm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fm.SetTexture("_BaseMap", check); fm.SetTextureScale("_BaseMap", new Vector2(60f, 60f)); fm.SetFloat("_Smoothness", 0f);
                floor.GetComponent<Renderer>().sharedMaterial = fm;

                var camGo = new GameObject("studio camera");
                camGo.transform.SetParent(temp.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 30f; cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << CatalogLayer;
                cam.backgroundColor = CategoryColour("Ducks"); cam.nearClipPlane = 0.02f; cam.farClipPlane = 20f;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                rt = new RenderTexture(GifSize, GifSize, 24) { antiAliasing = 8 };
                cam.targetTexture = rt;
                tex = new Texture2D(GifSize, GifSize, TextureFormat.RGB24, false);

                foreach (var m in Moves())
                {
                    try
                    {
                        var (clip, n, w) = BakeOne(m, glb, map, temp.root, cam, rt, tex, prefabPaths, tiltOffset);
                        warnings += w; frames += n;
                        if (clip != null) clips.Add((m, clip));
                    }
                    catch (Exception ex) { Debug.LogException(ex); warnings++; }
                }
                cam.targetTexture = null;
            }
            finally
            {
                foreach (var l in lightsOff) if (l != null) l.enabled = true;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
                CloseTemp(temp);
            }

            var ctrl = BuildController(clips);
            BuildAnimatedDuck(duckPrefab, ctrl);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Microduck] Bake Duck Moves: {clips.Count} clips in {AnimDir}, {frames} frames in {MotionFrames}, {warnings} warnings.");
        }

        static (AnimationClip clip, int frames, int warnings) BakeOne(Move m, GameObject glb, DuckJointMap map, Transform parent, Camera cam,
            RenderTexture rt, Texture2D tex, HashSet<string> prefabPaths, Vector3 tiltOffset)
        {
            int warnings = 0;
            float dt = 1f / Fps;
            var root = new GameObject("Duck");
            root.transform.SetParent(parent, false);
            var rig = Object.Instantiate(glb); rig.name = "rig";
            var body = root.AddComponent<DuckBody>();
            body.Init(rig, map);
            ComicLook.ApplyToDuck(rig.transform, "cream");
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CatalogLayer;

            Vector3 pos = Vector3.zero; float yaw = 0f;
            for (int i = 0; i < 30; i++) body.Step(1f / 60f, pos, yaw, Vector3.zero, 0f);   // settle into the stand pose, as the prefab was

            var nodes = root.GetComponentsInChildren<Transform>(true).Where(t => t != root.transform).ToList();
            var paths = nodes.Select(t => AnimationUtility.CalculateTransformPath(t, root.transform)).ToList();
            int missing = paths.Count(p => !prefabPaths.Contains(p));
            if (missing > 0) { Debug.LogWarning($"[Microduck] {m.id}: {missing} node(s) are not in Duck.prefab; their curves will do nothing"); warnings++; }
            var lp = nodes.Select(_ => new List<Vector3>()).ToList();
            var lr = nodes.Select(_ => new List<Quaternion>()).ToList();

            string dir = Path.Combine(MotionFrames, m.id.ToLowerInvariant());
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);

            var c = new Ctl();
            float t0 = -m.warm, time = t0;
            Vector3 startPos = pos; float startYaw = yaw;
            bool recording = false; int wraps = 0, frame = 0; float prevPhase = body.Phase, recT = 0f;
            int maxFrames = Mathf.CeilToInt((m.warm + Mathf.Max(m.len, 4f)) * Fps) + 4;
            for (int step = 0; step < maxFrames; step++)
            {
                c.v = 0f; c.w = 0f;
                m.drive(Mathf.Max(0f, time), body, c);
                yaw += c.w * dt;
                Vector3 vel = GltfSpace.Forward(yaw) * c.v;
                pos += vel * dt;
                body.Step(dt, pos, yaw, vel, c.w);

                if (!recording && time >= 0f)
                {
                    // walking: start where a stride starts, so the loop closes
                    bool ok = m.len > 0f || (prevPhase > 0.8f && body.Phase < 0.2f);
                    if (ok) { recording = true; startPos = pos; startYaw = yaw; recT = 0f; }
                }
                else if (recording && m.len <= 0f && prevPhase > 0.8f && body.Phase < 0.2f) wraps++;
                prevPhase = body.Phase;

                if (recording)
                {
                    // the root never moves in the clip: its height and (for Turn) its turning go onto duck_tilt
                    Quaternion rel = Quaternion.Inverse(Quaternion.Euler(0f, startYaw * Mathf.Rad2Deg, 0f)) * root.transform.rotation;
                    if (!m.keepTurn) rel = Quaternion.identity;
                    Vector3 lift = new Vector3(0f, root.transform.position.y - startPos.y, 0f);
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        var n = nodes[i];
                        Vector3 p = n.localPosition; Quaternion r = n.localRotation;
                        if (n.parent == root.transform) { p = tiltOffset + lift + rel * p; r = rel * r; }
                        if (lr[i].Count > 0 && Quaternion.Dot(lr[i][lr[i].Count - 1], r) < 0f) r = new Quaternion(-r.x, -r.y, -r.z, -r.w);
                        lp[i].Add(p); lr[i].Add(r);
                    }
                    // the picture: the camera follows the duck
                    bool fall = m.id == "FallGetUp";
                    Vector3 at = new Vector3(pos.x, fall ? 0.08f : 0.125f, pos.z) + (fall ? new Vector3(0.64f, 0f, -0.77f) * 0.11f : Vector3.zero);
                    float ang = (startYaw * Mathf.Rad2Deg + m.camAngle) * Mathf.Deg2Rad;
                    cam.transform.position = at + new Vector3(Mathf.Sin(ang), 0.42f, Mathf.Cos(ang)).normalized * (fall ? 1.15f : 1.0f);
                    cam.transform.LookAt(at);
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, GifSize, GifSize), 0, 0, false);
                    RenderTexture.active = null;
                    FillRect(tex, 0, GifSize - 44, GifSize, 44, ComicLook.Hex("#FFD23F"));
                    FillRect(tex, 0, GifSize - 47, GifSize, 3, ComicLook.Ink);
                    DrawText(tex, 14, GifSize - 8, m.title, ComicLook.Ink, 4, GifSize - 28);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, $"f{frame:D4}.png"), tex.EncodeToPNG());
                    frame++; recT += dt;
                    bool done = m.len > 0f ? recT >= m.len - 1e-4f : wraps >= 4;
                    if (done) break;
                }
                time += dt;
            }
            Object.DestroyImmediate(root);
            if (frame < 2) { Debug.LogWarning($"[Microduck] {m.id}: nothing recorded"); return (null, 0, warnings + 1); }

            // ---- the clip (kept: same asset and GUID when baked again)
            string clipPath = $"{AnimDir}/Duck_{m.id}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool isNew = clip == null;
            if (isNew) clip = new AnimationClip();
            clip.ClearCurves();
            clip.frameRate = Fps;
            int curves = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                curves += Curve(clip, paths[i], "m_LocalPosition.x", lp[i].Select(v => v.x).ToList(), dt);
                curves += Curve(clip, paths[i], "m_LocalPosition.y", lp[i].Select(v => v.y).ToList(), dt);
                curves += Curve(clip, paths[i], "m_LocalPosition.z", lp[i].Select(v => v.z).ToList(), dt);
                bool rotates = Varies(lr[i].Select(q => q.x).ToList()) || Varies(lr[i].Select(q => q.y).ToList()) || Varies(lr[i].Select(q => q.z).ToList()) || Varies(lr[i].Select(q => q.w).ToList());
                if (rotates)
                {
                    curves += Curve(clip, paths[i], "m_LocalRotation.x", lr[i].Select(q => q.x).ToList(), dt, true);
                    curves += Curve(clip, paths[i], "m_LocalRotation.y", lr[i].Select(q => q.y).ToList(), dt, true);
                    curves += Curve(clip, paths[i], "m_LocalRotation.z", lr[i].Select(q => q.z).ToList(), dt, true);
                    curves += Curve(clip, paths[i], "m_LocalRotation.w", lr[i].Select(q => q.w).ToList(), dt, true);
                }
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = m.loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (isNew) AssetDatabase.CreateAsset(clip, clipPath); else EditorUtility.SetDirty(clip);
            Debug.Log($"[Microduck] {m.id}: {frame} frames ({frame / Fps:0.00} s), {curves} curves");
            return (clip, frame, warnings);
        }

        static bool Varies(List<float> v) { float lo = v.Min(), hi = v.Max(); return hi - lo > 1e-5f; }

        static int Curve(AnimationClip clip, string path, string prop, List<float> v, float dt, bool force = false)
        {
            if (!force && !Varies(v)) return 0;
            var keys = new Keyframe[v.Count];
            for (int i = 0; i < v.Count; i++)
            {
                float a = v[Mathf.Max(0, i - 1)], b = v[Mathf.Min(v.Count - 1, i + 1)];
                float slope = (b - a) / (dt * (Mathf.Min(v.Count - 1, i + 1) - Mathf.Max(0, i - 1)));
                keys[i] = new Keyframe(i * dt, v[i], slope, slope);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), prop), new AnimationCurve(keys));
            return 1;
        }

        /// <summary>Idle by default; every move has a trigger of its own name, and the one-off moves go back to Idle.</summary>
        static AnimatorController BuildController(List<(Move m, AnimationClip clip)> clips)
        {
            string path = AnimDir + "/Duck.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            int i = 0;
            foreach (var (m, clip) in clips)
            {
                var st = sm.AddState(m.id, new Vector3(300f + (i % 3) * 220f, (i / 3) * 70f, 0f));
                st.motion = clip; states[m.id] = st; i++;
                if (m.id != "Idle") ctrl.AddParameter(m.id, AnimatorControllerParameterType.Trigger);
            }
            if (!states.TryGetValue("Idle", out var idle)) return ctrl;
            sm.defaultState = idle;
            foreach (var (m, _) in clips)
            {
                if (m.id == "Idle") continue;
                var st = states[m.id];
                var any = sm.AddAnyStateTransition(st);
                any.AddCondition(AnimatorConditionMode.If, 0f, m.id);
                any.hasExitTime = false; any.duration = 0.15f; any.canTransitionToSelf = false;
                if (!m.loop)
                {
                    var back = st.AddTransition(idle);
                    back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.2f;
                }
            }
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void BuildAnimatedDuck(GameObject duckPrefab, AnimatorController ctrl)
        {
            var inst = PrefabUtility.InstantiatePrefab(duckPrefab) as GameObject;
            if (inst == null) return;
            try
            {
                inst.name = "Duck (animated)";
                var an = inst.GetComponent<Animator>(); if (an == null) an = inst.AddComponent<Animator>();
                an.runtimeAnimatorController = ctrl; an.applyRootMotion = false;
                PrefabUtility.SaveAsPrefabAsset(inst, AnimDir + "/Duck (animated).prefab");
            }
            finally { Object.DestroyImmediate(inst); }
        }
    }
}
