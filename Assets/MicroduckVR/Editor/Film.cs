using System;
using System.Collections.Generic;
using System.IO;
using MicroduckVR.Comic;
using MicroduckVR.Custom;
using MicroduckVR.Dev;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Games;
using MicroduckVR.Player;
using MicroduckVR.World;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Films the game from inside the editor: a list of shots, each with its own camera move and its own bit of
    /// scripted play, rendered frame by frame on a stepped game clock (so nothing stutters; see Quality for the cinema settings)
    /// with the game's own sound, one MP4 per shot under Logs/film. Preview writes three small stills per shot.
    /// </summary>
    public static partial class Film
    {
        public class Shot
        {
            public string name, caption; public float dur, pre; public bool fp;
            public Action start; public Action<float> tick;
        }

        /// <summary>How a film is rendered. Cinema: every frame is rendered at twice the size and filtered down
        /// (no jaggies, steady ink lines), several sub-frames are blended (real motion blur, 180 degree shutter),
        /// with depth of field and a light grade, 60 frames a second.</summary>
        class Quality { public int w = 1920, h = 1080, fps = 30, ss = 1, sub = 1, acc = 1, bitrate = 10000000; public bool post; }
        static readonly Quality Plain = new Quality(), Cinema = new Quality { fps = 60, ss = 2, bitrate = 45000000, post = true },
                                Stills = new Quality { w = 480, h = 270 };
        static Quality Q = Plain;
        /// <summary>Game seconds per simulated frame at normal speed.</summary>
        static float Dt => 1f / (Q.fps * Q.sub);
        /// <summary>Slow motion for the current shot (2 = half speed). A shot may change it while it runs.</summary>
        static float Slow = 1f;
        /// <summary>0..1 through the current shot, in screen time (what camera moves should ease along).</summary>
        static float U;

        const int Fps = 30;
        static List<Shot> shots; static int si, sub, outFrames; static float elapsed, outT; static bool started, preview;
        static string tag, dir; static Camera cam; static RenderTexture rt, accum, outRt; static Texture2D tex; static MediaEncoder enc; static FilmRig rig; static Material accMat;
        static readonly HashSet<string> fired = new HashSet<string>(); static readonly HashSet<int> stills = new HashSet<int>();
        static readonly List<Renderer> hidden = new List<Renderer>();
        static StreamWriter manifest; static int savedCapture; static bool audioOn; static Texture2D atlas;
        static UnityEngine.Rendering.Volume volume; static UnityEngine.Rendering.Universal.DepthOfField dof;
        static float savedShadowDist; static int savedCascades; static AnisotropicFiltering savedAniso; static float savedLod;
        // focus: where the lens is focused and how wide open it is (f-number; big = everything sharp)
        static Vector3 focusPoint; static float fstop = 4f;
        /// <summary>A shot can ask the flock to keep out of the space right in front of the lens.</summary>
        static bool LensClear;
        static Material inkMat; static UnityEngine.Rendering.Universal.UniversalRendererData rdata; static bool lentPost;

        static PlayerDuck P => PlayerDuck.Instance;
        static Vector3 Pos => P.Body.Pos;
        static BagMenu Bag => P.GetComponent<BagMenu>();

        [MenuItem("Microduck/Film/Trailer")] public static void Trailer() => Begin("trailer", TrailerShots(), false, Cinema);
        [MenuItem("Microduck/Film/Trailer (preview stills)")] public static void TrailerPreview() => Begin("trailer", TrailerShots(), true, Stills);
        [MenuItem("Microduck/Film/Demo")] public static void Demo() => Begin("demo", DemoShots(), false, Plain);
        /// <summary>Only the tutorial part of the walkthrough (to re-film it on its own).</summary>
        public static void DemoTutorial() => Begin("demo_tutorial", DemoShots().GetRange(0, 1), false, Plain);
        [MenuItem("Microduck/Film/Demo (preview stills)")] public static void DemoPreview() => Begin("demo", DemoShots(), true, new Quality { w = 480, h = 270 });
        /// <summary>A few named trailer shots only, e.g. to re-film them: set Only first.</summary>
        public static string Only = "";
        public static void TrailerOnly() { var all = TrailerShots(); var pick = all.FindAll(x => ("," + Only + ",").Contains("," + x.name + ",")); Begin("trailer_only", pick, false, Cinema); }

        static void Begin(string t, List<Shot> list, bool prev, Quality q)
        {
            if (!Application.isPlaying || P == null) { Debug.Log("[Microduck] film needs Play mode"); return; }
            if (rig != null) { Debug.Log("[Microduck] film already running"); return; }
            tag = t; shots = list; preview = prev; Q = q; si = 0; elapsed = 0f; started = false;
            dir = Path.Combine("Logs", "film", tag + (prev ? "_preview" : "")); if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            manifest = new StreamWriter(Path.Combine(dir, "manifest.txt"));
            if (prev) atlas = new Texture2D(1440, 270 * 6, TextureFormat.RGBA32, false);
            var go = new GameObject("Film camera"); cam = go.AddComponent<Camera>(); cam.enabled = false;
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 90f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = ComicLook.Hex("#9AD8F2");
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            var cd = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); cd.renderPostProcessing = Q.post;
            cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            rt = new RenderTexture(Q.w * Q.ss, Q.h * Q.ss, 24) { antiAliasing = 4, filterMode = FilterMode.Bilinear }; cam.targetTexture = rt;
            accum = new RenderTexture(Q.w, Q.h, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            outRt = new RenderTexture(Q.w, Q.h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            accMat = new Material(Shader.Find("Hidden/Microduck/FilmAccum"));
            tex = new Texture2D(Q.w, Q.h, TextureFormat.RGBA32, false);
            savedCapture = Time.captureFramerate; Time.captureFramerate = Q.fps * Q.sub;
            audioOn = !prev && AudioRenderer.Start();
            ComicPop.Viewer = cam;
            foreach (var tb in UnityEngine.Object.FindObjectsByType<ThoughtBubbles>(FindObjectsSortMode.None)) { tb.viewer = cam; if (Q.post) tb.showThoughts = false; }
            // the film look: a real lens (depth of field), a touch of grade; and the engine's quality dials turned up
            if (Q.post)
            {
                var vg = new GameObject("Film volume"); volume = vg.AddComponent<UnityEngine.Rendering.Volume>(); volume.isGlobal = true; volume.priority = 100f;
                var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); volume.profile = prof;
                dof = prof.Add<UnityEngine.Rendering.Universal.DepthOfField>(true);
                dof.mode.Override(UnityEngine.Rendering.Universal.DepthOfFieldMode.Bokeh); dof.bladeCount.Override(7); dof.bladeCurvature.Override(1f);
                var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true); ca.saturation.Override(9f); ca.contrast.Override(7f); ca.postExposure.Override(0.06f);
                var vi = prof.Add<UnityEngine.Rendering.Universal.Vignette>(true); vi.intensity.Override(0.2f); vi.smoothness.Override(0.5f);
            }
            // The project's renderer was made in code and carries no post-processing data, so depth of field and the grade
            // silently did nothing: lend it the pipeline's own for the length of the film.
            rdata = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>("Assets/MicroduckVR/Settings/MicroduckRenderer.asset");
            lentPost = false;
            if (Q.post && rdata != null && rdata.postProcessData == null)
            {
                rdata.postProcessData = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                lentPost = rdata.postProcessData != null; rdata.SetDirty();
                Debug.Log("[Microduck] film: post-processing " + (lentPost ? "on" : "NOT available"));
            }
            LaserPointer.HideBeam = Q.post;
            // a finer pen on far things and a sharper mirror: both only pay off at film resolution
            inkMat = AssetDatabase.LoadAssetAtPath<Material>(ComicSetup.OutlineMatPath);
            if (inkMat != null) { inkMat.SetFloat("_FarThin", Q.post && Q.ss > 1 ? 0.5f : 0f); inkMat.SetFloat("_ThinNear", 0.7f); inkMat.SetFloat("_ThinFar", 2.2f); }
            if (MirrorStation.Instance != null) MirrorStation.Instance.Sharpen(Q.post ? Q.ss * 2 : 1);
            MirrorStation.Viewer = cam;
            var pipe = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipe != null) { savedShadowDist = pipe.shadowDistance; savedCascades = pipe.shadowCascadeCount; if (Q.post) { pipe.shadowDistance = 45f; pipe.shadowCascadeCount = 4; } }
            savedAniso = QualitySettings.anisotropicFiltering; savedLod = QualitySettings.lodBias; QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable; QualitySettings.lodBias = 4f;
            rig = new GameObject("Film rig").AddComponent<FilmRig>(); rig.OnFrame = Frame;
            Debug.Log($"[Microduck] film {tag}: {shots.Count} shots, {(prev ? "preview" : "full")}, {Q.w}x{Q.h}@{Q.fps} ss{Q.ss} sub{Q.sub}/{Q.acc}");
        }

        static void End()
        {
            CloseEncoder();
            if (audioOn) AudioRenderer.Stop();
            Time.captureFramerate = savedCapture; ComicPop.Viewer = null;
            foreach (var tb in UnityEngine.Object.FindObjectsByType<ThoughtBubbles>(FindObjectsSortMode.None)) { tb.viewer = P != null ? P.cam : null; tb.showThoughts = true; }
            Reset(); GameHud.Hidden = false;
            manifest.Dispose();
            var pipe = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipe != null) { pipe.shadowDistance = savedShadowDist; pipe.shadowCascadeCount = savedCascades; }
            QualitySettings.anisotropicFiltering = savedAniso; QualitySettings.lodBias = savedLod;
            if (inkMat != null) inkMat.SetFloat("_FarThin", 0f);
            if (lentPost && rdata != null) { rdata.postProcessData = null; rdata.SetDirty(); lentPost = false; }
            LaserPointer.HideBeam = false;
            if (MirrorStation.Instance != null) MirrorStation.Instance.Sharpen(1);
            MirrorStation.Viewer = null;
            if (volume != null) { UnityEngine.Object.Destroy(volume.gameObject); volume = null; dof = null; }
            cam.targetTexture = null;
            foreach (var o in new UnityEngine.Object[] { rt, accum, outRt, tex, accMat, cam.gameObject, rig.gameObject }) UnityEngine.Object.Destroy(o);
            rig = null;
            Debug.Log($"[Microduck] film {tag} done");
        }

        static void CloseEncoder() { if (enc != null) { enc.Dispose(); enc = null; } }

        static void Frame()
        {
            if (P == null) return;
            if (si >= shots.Count) { End(); return; }
            var s = shots[si];
            if (!started)
            {
                started = true; elapsed = 0f; outT = 0f; outFrames = 0; sub = 0; Slow = 1f; U = 0f; fstop = 4f; LensClear = false;
                fired.Clear(); stills.Clear(); Reset(); camInit = false; biteAt = -1f; grabPhase = 0; phaseT = 0f;
                s.start?.Invoke();
                if (!preview)
                {
                    var va = new VideoTrackEncoderAttributes(new H264EncoderAttributes { gopSize = (uint)Q.fps, numConsecutiveBFrames = 0, profile = VideoEncodingProfile.H264High })
                    { frameRate = new MediaRational(Q.fps), width = (uint)Q.w, height = (uint)Q.h, targetBitRate = (uint)Q.bitrate };
                    string path = Path.Combine(dir, $"{si:D2}_{s.name}.mp4");
                    if (audioOn) enc = new MediaEncoder(path, va, new AudioTrackAttributes { sampleRate = new MediaRational(AudioSettings.outputSampleRate), channelCount = 2, language = "und" });
                    else enc = new MediaEncoder(path, va);
                }
                manifest.WriteLine($"{si:D2}_{s.name}|{s.dur:F2}|{s.caption}"); manifest.Flush();
            }
            float t = elapsed - s.pre;
            U = Mathf.Clamp01(outT / s.dur);
            cam.cullingMask = s.fp ? P.cam.cullingMask : ~0;
            focusPoint = cam.transform.position + cam.transform.forward * 3f;
            s.tick?.Invoke(t);
            if (LensClear) DuckBrain.ClearView(cam.transform.position, cam.transform.position + cam.transform.forward * 0.42f, true);
            if (dof != null)
            {
                float d = Mathf.Max(0.05f, Vector3.Dot(focusPoint - cam.transform.position, cam.transform.forward));
                dof.focusDistance.Override(d); dof.aperture.Override(Mathf.Clamp(fstop, 1f, 32f));
                dof.focalLength.Override(Mathf.Clamp(12f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), 8f, 200f));
            }
            // sound: keep the offline renderer in step with the stepped clock, even during the run-up
            NativeArray<float> buf = default;
            if (audioOn) { int n = AudioRenderer.GetSampleCountForCaptureFrame(); buf = new NativeArray<float>(n * 2, Allocator.Temp); AudioRenderer.Render(buf); }
            bool lastSub = sub == Q.sub - 1;
            if (t >= 0f)
            {
                bool shoot = !preview;
                if (preview)
                {
                    int k = outT < 1f / Q.fps ? 0 : outT >= s.dur * 0.5f && outT < s.dur * 0.5f + 1f / Q.fps ? 1 : outT >= s.dur - 2f / Q.fps ? 2 : -1;
                    shoot = k >= 0 && !stills.Contains(k); if (shoot) stills.Add(k);
                }
                if (shoot && sub >= Q.sub - Q.acc)
                {
                    hidden.Clear();
                    foreach (var v in UnityEngine.Object.FindObjectsByType<ComfortVignette>(FindObjectsSortMode.None)) { var r = v.GetComponent<Renderer>(); if (r != null && r.enabled) { r.enabled = false; hidden.Add(r); } }
                    cam.Render();
                    foreach (var r in hidden) r.enabled = true;
                    var prevRt = RenderTexture.active;
                    if (sub == Q.sub - Q.acc) { RenderTexture.active = accum; GL.Clear(false, true, Color.clear); }
                    accMat.SetFloat("_Weight", 1f / Q.acc); Graphics.Blit(rt, accum, accMat);
                    if (lastSub)
                    {
                        Graphics.Blit(accum, outRt);
                        RenderTexture.active = outRt; tex.ReadPixels(new Rect(0, 0, Q.w, Q.h), 0, 0); tex.Apply(false);
                        if (preview) atlas.SetPixels32((stills.Count - 1) * 480, (5 - si % 6) * 270, 480, 270, tex.GetPixels32());
                        else enc.AddFrame(tex);
                    }
                    RenderTexture.active = prevRt;
                }
                if (!preview && audioOn)
                {
                    // slow motion has no usable sound: keep the track the right length with silence
                    if (Mathf.Abs(Slow - 1f) < 0.01f) enc.AddSamples(buf);
                    else { int n = Mathf.RoundToInt(AudioSettings.outputSampleRate / (float)(Q.fps * Q.sub)); var z = new NativeArray<float>(n * 2, Allocator.Temp, NativeArrayOptions.ClearMemory); enc.AddSamples(z); z.Dispose(); }
                }
                if (lastSub) { outFrames++; outT = outFrames / (float)Q.fps; }
            }
            if (buf.IsCreated) buf.Dispose();
            elapsed += Dt / Mathf.Max(0.05f, Slow);
            sub = t >= 0f ? (sub + 1) % Q.sub : 0;
            Time.captureFramerate = Mathf.Max(1, Mathf.RoundToInt(Q.fps * Q.sub * Mathf.Max(0.05f, Slow)));
            bool done = outT >= s.dur - 1e-4f;
            if (done && preview && (si % 6 == 5 || si == shots.Count - 1)) { atlas.Apply(false); File.WriteAllBytes(Path.Combine(dir, $"sheet_{si / 6}.jpg"), atlas.EncodeToJPG(78)); atlas.SetPixels32(new Color32[1440 * 270 * 6]); }
            if (done) { CloseEncoder(); Debug.Log($"[Microduck] film shot {si:D2} {s.name} ok  ({Info()})"); si++; started = false; }
        }

        static string Info() => $"pos {Pos.ToString("F2")} grabs {P.GrabCount} drops {P.DropCount} ducks {DuckBrain.All.Count} card {TutorialCoach.DebugTitle}";

        // ------------------------------------------------------------------ helpers for the shot scripts
        static bool camInit; static Vector3 camPos, camVel, lookPos, lookVel; static float biteAt = -1f; static int grabPhase; static float phaseT;

        static void At(float t, float when, string key, Action a) { if (t >= when && fired.Add(key)) a(); }
        static float E(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }
        static Vector3 Fwd(float yaw) => new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>Camera at a place, looking at a point.</summary>
        static void Look(Vector3 from, Vector3 at, float fov = 40f)
        {
            cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up); cam.fieldOfView = fov;
            focusPoint = at;
        }
        /// <summary>Focus the lens on a point, with an f-number (2 = very shallow, 16 = everything sharp).</summary>
        static void Focus(Vector3 at, float f) { focusPoint = at; fstop = f; }
        static float Lerp(float a, float b, float u) => Mathf.Lerp(a, b, E(u));
        static Vector3 Lerp(Vector3 a, Vector3 b, float u) => Vector3.Lerp(a, b, E(u));
        /// <summary>The same, eased so a moving subject is followed like by a camera operator, not a robot.</summary>
        static void Soft(Vector3 from, Vector3 at, float fov = 40f, float lag = 0.25f)
        {
            if (!camInit) { camInit = true; camPos = from; lookPos = at; camVel = lookVel = Vector3.zero; }
            camPos = Vector3.SmoothDamp(camPos, from, ref camVel, lag, 100f, Dt);
            lookPos = Vector3.SmoothDamp(lookPos, at, ref lookVel, lag * 0.7f, 100f, Dt);
            Look(camPos, lookPos, fov);
        }
        /// <summary>Through the duck's own eyes (the player's view), looking the way given.</summary>
        static void Eyes(float yawDeg, float pitchDeg = 8f, float fov = 92f)
        {
            cam.transform.position = P.cam.transform.position; cam.transform.rotation = Quaternion.Euler(pitchDeg, yawDeg, 0f); cam.fieldOfView = fov;
            focusPoint = cam.transform.position + cam.transform.forward * 1.2f; fstop = 16f;
        }
        static void Chase(float dist = 0.5f, float height = 0.28f, float fov = 55f, float side = 0f)
        {
            var f = Fwd(P.Body.Yaw); var r = new Vector3(f.z, 0f, -f.x);
            Soft(Pos - f * dist + r * side + Vector3.up * height, Pos + Vector3.up * 0.14f + f * 0.1f, fov, 0.35f);
        }

        static bool Walk(Vector3 to, float stop = 0.06f)
        {
            var d = to - Pos; d.y = 0f;
            if (d.magnitude < stop) { PlayerDuck.DebugWalk = null; return true; }
            PlayerDuck.DebugWalk = d.normalized; return false;
        }
        static void Stop() => PlayerDuck.DebugWalk = null;

        /// <summary>Open the beak, waddle up to a thing until the beak is on it, bite. True once it is held.</summary>
        static bool Grab(Vector3 thing)
        {
            if (P.Held != null) { grabPhase = 2; PlayerDuck.DebugBeak = 0f; return true; }
            if (grabPhase == 0)
            {
                PlayerDuck.DebugBeak = 1f; phaseT += Dt;
                var d = thing - Pos; d.y = 0f;
                if (d.magnitude > 0.1f) PlayerDuck.DebugWalk = d.normalized; else Stop();
                if (phaseT > 0.6f && (Grabbable.Nearest(P.Body.BeakGap(), 0.02f) != null || phaseT > 6f)) { grabPhase = 1; phaseT = 0f; PlayerDuck.DebugBeak = 0f; Stop(); }
            }
            else if (grabPhase == 1) { phaseT += Dt; if (phaseT > 0.5f) { grabPhase = 0; phaseT = 0.3f; } }   // missed: try again
            return false;
        }
        static void Drop() { if (P.Held != null) PlayerDuck.DebugBeak = 1f; else PlayerDuck.DebugBeak = 0f; }
        static void Place(Vector3 p, float yawDeg) { P.Teleport(new Vector3(p.x, p.y + 0.03f, p.z), yawDeg * Mathf.Deg2Rad); P.FaceView(yawDeg * Mathf.Deg2Rad); }
        static void Skates(bool on) { if (P.Skates.On != on) P.Skates.Toggle(P.Body.Yaw, Vector3.zero); }
        static void Click(WorldPanel p, string id) { foreach (var b in p.buttons) if (b.id == id) { WorldPanel.DebugPoint = b.t.position; WorldPanel.DebugPress = true; return; } }
        static void MoveDuck(int i, Vector3 p, float yawDeg) { if (i < DuckBrain.All.Count) DuckBrain.All[i].Teleport(p, yawDeg * Mathf.Deg2Rad); }
        static void Emote(string kind) { P.Body.PlayEmote(kind); }
        static void AllEmote(string kind) { P.Body.PlayEmote(kind); foreach (var d in DuckBrain.All) if (d.Body != null) d.Body.PlayEmote(kind); }
        static Vector3 Ground(float x, float z) => new Vector3(x, WorldQueries.GroundAt(x, z, 0.5f), z);

        /// <summary>Everything back to a calm state between shots.</summary>
        static void Reset()
        {
            PlayerDuck.DebugWalk = null; PlayerDuck.DebugBeak = null; PlayerDuck.DebugChirp = false;
            PlayerSkates.DebugDrift = null; PlayerSkates.DebugBoost = null; LaserPointer.DebugPoint = null; WorldPanel.DebugPoint = null;
            if (P == null) return;
            P.DropHeld();
            var bag = Bag; if (bag != null) { bag.SetDebugAim(null); if (bag.State != BagMenu.Mode.Closed) bag.Close(); }
            if (P.Skates != null && P.Skates.On) Skates(false);
            if (FootballGame.Active) FootballGame.Instance.StopGame();
            if (RaceGame.Active) RaceGame.Ensure().StopRace();
            TutorialCoach.HideAll(); if (P.thirdPerson) P.SetThirdPerson(false);
            GameHud.Hidden = true;
        }

        // ------------------------------------------------------------------ the duck's model sheet
        /// <summary>Stand the duck in the sun in the garden, facing the house (run a moment before ModelSheet).</summary>
        public static void ModelSheetPrep()
        {
            if (!Application.isPlaying || P == null) return;
            TutorialCoach.HideAll(); Reset(); Place(V(1.3f, 0f, 5.7f), 0f);
        }

        [MenuItem("Microduck/Film/Model Sheet")]
        public static void ModelSheet()
        {
            if (!Application.isPlaying || P == null) return;
            TutorialCoach.HideAll(); Reset(); GameHud.Hidden = false;
            // only this duck in the picture
            var others = new List<Renderer>();
            foreach (var d in DuckBrain.All) foreach (var r in d.GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; others.Add(r); }
            var renderers = new List<Renderer>();
            foreach (var r in P.Body.GetComponentsInChildren<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) renderers.Add(r);
            var saved = new List<Material[]>(); foreach (var r in renderers) saved.Add(r.sharedMaterials);
            int mask = (1 << DuckSetup.LayerDuck) | (1 << DuckSetup.LayerPlayerHead);
            var c = Pos + Vector3.up * 0.15f; float yaw = P.Body.Yaw;
            var views = new (string n, float ang, float h)[] { ("front", 28f, 0.03f), ("side", 95f, 0.0f), ("back", 205f, 0.04f) };
            Material Flat(string hex, bool unlit) { var m = new Material(Shader.Find("Microduck/ToonLit")); m.SetColor("_BaseColor", ComicLook.Hex(hex)); m.SetFloat("_Unlit", unlit ? 1f : 0f); return m; }
            void Paint(Material m) { foreach (var r in renderers) { var a = new Material[r.sharedMaterials.Length]; for (int i = 0; i < a.Length; i++) a[i] = m; r.sharedMaterials = a; } }
            void Restore() { for (int i = 0; i < renderers.Count; i++) renderers[i].sharedMaterials = saved[i]; }
            void Shoot(string name, bool wire, bool ink, string bg)
            {
                foreach (var v in views)
                {
                    var d = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg + v.ang, 0f) * Vector3.forward;
                    DebugCapture.ShootStyled($"model_{name}_{v.n}", c + d * 0.62f + Vector3.up * v.h, c, 1600, 2000, 32f, mask, wire, ink, ComicLook.Hex(bg));
                }
            }
            Paint(Flat("#1B1430", true)); Shoot("1_wire", true, false, "#FFF6E4");
            Paint(Flat("#C9CCD6", false)); Shoot("2_clay", false, false, "#FFF6E4");
            Paint(Flat("#FFFFFF", true)); Shoot("3_ink", false, true, "#FFFFFF");
            Restore(); Shoot("4_colour", false, true, "#FFF6E4");
            foreach (var r in others) r.enabled = true;
            Debug.Log("[Microduck] model sheet done");
        }
    }
}
