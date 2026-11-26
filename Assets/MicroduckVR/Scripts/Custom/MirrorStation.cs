using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using MicroduckVR.Player;
using MicroduckVR.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Custom
{
    /// <summary>
    /// The bedroom mirror (port of the customise screen): a tall comic-framed mirror that shows your duck, and next
    /// to it a board to restyle yourself: colourway, paint per part, voice, random, and SAVE. The reflection is
    /// rendered by a camera behind the glass with a frustum fitted to the mirror (only while you are close and it is
    /// in view), so it is a picture in the frame rather than a second stereo world.
    /// </summary>
    public class MirrorStation : MonoBehaviour
    {
        public static MirrorStation Instance { get; private set; }
        // Unity space: mirror on the bedroom's north wall, facing into the room (+z)
        static readonly Vector3 MirrorCentre = new Vector3(4.42f, 0.75f, -2.42f);   // clear of the toy box
        /// <summary>Where a duck stands to see itself and reach the board; face yaw = pi (towards the mirror).</summary>
        public static Vector3 Centre => MirrorCentre;
        /// <summary>Whose eye the reflection is drawn for (filming); the player's own when not set.</summary>
        public static Camera Viewer;
        static readonly int InkScaleId = Shader.PropertyToID("_InkCamScale");
        public static readonly Vector3 StandPos = new Vector3(4.42f, 0.05f, -2.05f);   // close enough to see your colours in the glass
        /// <summary>How many times DONE! has been pressed on the board (the tutorial waits for the first).</summary>
        public static int SaveCount { get; private set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; Viewer = null; SaveCount = 0; InZone = false; }
        const float W = 0.6f, H = 1.4f;

        public WorldPanel Board => board;
        /// <summary>The look as it is on the board right now (the network session shows it on the other PC).</summary>
        public DuckCustom Current => custom;
        readonly System.Collections.Generic.List<Renderer> hidden = new System.Collections.Generic.List<Renderer>();
        Camera mirrorCam; RenderTexture rt; Transform glass; readonly System.Collections.Generic.List<GameObject> glints = new System.Collections.Generic.List<GameObject>();
        WorldPanel board, prompt; DuckCustom custom; PlayerDuck pd;
        InputAction openA;
        /// <summary>The board is out (X near the mirror, or the first-run tutorial); otherwise only the "PRESS X" prompt shows.</summary>
        public bool BoardOpen { get; private set; }
        /// <summary>You are standing at the mirror: X opens the board here instead of dancing.</summary>
        public static bool InZone { get; private set; }
        // the reflection is sharp enough to read your colours at the stand (about 20 px a degree in the headset)
        const int RtW = 1536, RtH = 3584;
        readonly System.Random rng = new System.Random();
        string paintPart = "";

        public static void Ensure(PlayerDuck player, TMP_FontAsset font)
        {
            if (Instance != null) return;
            var go = new GameObject("Mirror station"); Instance = go.AddComponent<MirrorStation>();
            Instance.pd = player; Instance.Build(font);
        }

        void Build(TMP_FontAsset font)
        {
            custom = DuckCustom.Load();
            custom.ApplyTo(pd.gameObject);

            // frame: chunky rounded wood with feet, leaning on the wall
            var frame = new GameObject("Mirror frame").transform; frame.SetParent(transform, false);
            frame.position = MirrorCentre;
            foreach (var (pos, size) in new[] { (new Vector3(0f, H / 2f + 0.04f, 0f), new Vector2(W + 0.16f, 0.08f)), (new Vector3(0f, -H / 2f - 0.04f, 0f), new Vector2(W + 0.16f, 0.08f)),
                                                 (new Vector3(-W / 2f - 0.04f, 0f, 0f), new Vector2(0.08f, H + 0.16f)), (new Vector3(W / 2f + 0.04f, 0f, 0f), new Vector2(0.08f, H + 0.16f)) })
                ToyKit.Box(frame, pos + new Vector3(0f, 0f, 0.0f), new Vector3(size.x, size.y, 0.05f), ComicColors.Orange, 0.4f);
            ToyKit.Box(frame, new Vector3(0f, 0f, -0.016f), new Vector3(W + 0.1f, H + 0.1f, 0.016f), "#C9622A", 0.2f);   // back board (wall side)
            ToyKit.Ball(frame, new Vector3(0f, H / 2f + 0.1f, 0.01f), new Vector3(0.12f, 0.12f, 0.04f), ComicColors.Yellow);   // a star-ish knob on top
            foreach (var t in frame.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layers.World;
            var col = frame.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(W + 0.16f, H + 0.16f, 0.06f);

            // the glass: a quad whose UVs map straight onto the mirror camera's fitted frustum
            rt = new RenderTexture(RtW, RtH, 24) { antiAliasing = 2, name = "MirrorRT", filterMode = FilterMode.Trilinear, useMipMap = true, autoGenerateMips = true, anisoLevel = 4 };
            var g = new GameObject("Mirror glass"); g.transform.SetParent(transform, false); g.transform.position = MirrorCentre + new Vector3(0f, 0f, 0.0f);
            var mesh = new Mesh { name = "mirror quad" };
            float x0 = -W / 2f, x1 = W / 2f, y0 = -H / 2f, y1 = H / 2f;
            mesh.vertices = new[] { new Vector3(x0, y0, 0.012f), new Vector3(x1, y0, 0.012f), new Vector3(x1, y1, 0.012f), new Vector3(x0, y1, 0.012f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };   // facing +z, into the room mesh.RecalculateNormals(); mesh.RecalculateBounds();
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = rt };
            mat.SetTexture(ShaderIds.BaseMap, rt);
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glass = g.transform;
            // comic glints: two white diagonal strokes in the top corner
            var gl = ComicLook.ToonMaterial(Color.white, "mirror_glint"); gl.SetFloat(ShaderIds.Unlit, 1f);
            foreach (var (dx, dy, len, wd) in new[] { (-W * 0.26f, H * 0.34f, W * 0.34f, 0.012f), (-W * 0.15f, H * 0.3f, W * 0.18f, 0.007f) })
            {
                var st = ToyKit.Box(g.transform, new Vector3(dx, dy, 0.0135f), new Vector3(len, wd, 0.001f), "#FFFFFF", 0f, new Vector3(0f, 0f, 40f));
                var smr = st.GetComponent<MeshRenderer>(); smr.sharedMaterial = gl; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                st.gameObject.layer = Layers.World; glints.Add(st.gameObject);
            }
            var cg = new GameObject("Mirror camera"); cg.transform.SetParent(transform, false);
            mirrorCam = cg.AddComponent<Camera>(); mirrorCam.enabled = false; mirrorCam.targetTexture = rt;
            mirrorCam.clearFlags = CameraClearFlags.SolidColor; mirrorCam.backgroundColor = ComicLook.Hex("#9AD8F2");
            mirrorCam.cullingMask = ~0;
            var urp = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urp.renderPostProcessing = false;

            BuildBoard(font);
            BuildPrompt(font);
            openA = new InputAction("MirrorOpen", InputActionType.Button);
            openA.AddBinding(InputPaths.LeftPrimary); openA.AddBinding(InputPaths.KeyX); openA.Enable();
            Sign(font, "MAKE YOUR DUCK!", new Vector3(MirrorCentre.x - W / 2f - 0.57f, 0.82f, MirrorCentre.z + 0.03f), Quaternion.LookRotation(Vector3.back, Vector3.up), 1.5f, ComicColors.Yellow);
        }

        /// <summary>A comic name plate: a tilted colour tab with ink lettering (used over the mirror and the garage).</summary>
        public static void Sign(TMP_FontAsset font, string text, Vector3 at, Quaternion facing, float size, string hex)
        {
            var go = new GameObject("Sign " + text); go.transform.SetPositionAndRotation(at, facing * Quaternion.Euler(0f, 0f, 3f));
            float w = (text.Length * 0.034f + 0.06f) * size, h = 0.09f * size;
            ToyKit.Box(go.transform, new Vector3(0f, 0f, 0.012f * size), new Vector3(w + 0.03f * size, h + 0.03f * size, 0.016f * size), ComicColors.Ink, 0.35f);
            ToyKit.Box(go.transform, Vector3.zero, new Vector3(w, h, 0.02f * size), hex, 0.35f);
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.gameObject.layer = Layers.World; }
            var tg = new GameObject("text"); tg.transform.SetParent(go.transform, false); tg.transform.localPosition = new Vector3(0f, 0f, -0.012f * size);
            var t = tg.AddComponent<TextMeshPro>(); if (font != null) t.font = font;
            t.text = text; t.fontSize = 0.62f * size; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.color = ComicLook.Ink;
            t.rectTransform.sizeDelta = new Vector2(w, h);
        }

        // ------------------------------------------------------------------ customise board
        void BuildBoard(TMP_FontAsset font)
        {
            var go = new GameObject("Customise board");
            go.transform.SetParent(transform, false);
            board = go.AddComponent<WorldPanel>();
            board.font = font; board.cam = pd.cam; board.reach = 1.2f;
            var bag = pd.GetComponent<BagMenu>(); if (bag != null) board.rightHand = bag.rightHand;
            // everything on the board is sized to read from where it floats (see WorldPanel.follow): about 1.3 degrees of
            // letter height on the buttons, more on the title and the two big buttons
            const float bw = 0.4f, bh = 0.36f, lx = -bw / 2f + 0.05f, x0 = -0.085f, dx = 0.072f;
            board.follow = true; board.focus = MirrorCentre; board.readDist = 0.42f; board.sideDeg = 38f; board.minY = StandPos.y + bh / 2f + 0.03f;
            board.panelRect = new Rect(-bw / 2f, -bh / 2f, bw, bh);
            board.Slab(new Vector2(bw, bh), "#FFFDF5", new Vector3(0f, 0f, 0.003f), 0.12f);
            board.Slab(new Vector2(bw, 0.04f), ComicColors.Pink, new Vector3(0f, bh / 2f - 0.02f, 0f), 0.4f);
            board.Label("MAKE YOUR DUCK!", new Vector3(0f, bh / 2f - 0.021f, -0.004f), 0.27f, "#FFFFFF", bw);

            float y = bh / 2f - 0.07f;
            board.Label("BODY", new Vector3(lx, y, -0.004f), 0.17f, ComicColors.Ink, 0.09f);
            for (int i = 0; i < 4; i++) { var s = DuckCustom.SkinIds[i]; Swatch("skin_" + s, new Vector2(x0 + i * dx, y), s, () => { custom.skin = s; Apply(); }, new Vector2(0.066f, 0.04f)); }
            y -= 0.05f;
            board.Label("PAINT\nA PART", new Vector3(lx, y - 0.02f, -0.004f), 0.135f, ComicColors.Ink, 0.09f);
            for (int i = 0; i < 4; i++) { var p = DuckCustom.PartIds[i]; board.Add("part_" + p, p.ToUpperInvariant(), new Vector2(x0 + i * dx, y), new Vector2(0.066f, 0.034f), "#E6E9F0", () => { paintPart = paintPart == p ? "" : p; Refresh(); }, 0.19f); }
            y -= 0.042f;
            for (int i = 0; i < 4; i++) { var s = DuckCustom.SkinIds[i]; Swatch("paint_" + s, new Vector2(x0 + i * 0.055f, y), s, () => { if (paintPart != "") { custom.SetPart(paintPart, s); Apply(); } }, new Vector2(0.05f, 0.032f)); }
            board.Add("paint_clear", "SAME", new Vector2(0.15f, y), new Vector2(0.07f, 0.032f), "#E6E9F0", () => { if (paintPart != "") { custom.SetPart(paintPart, ""); Apply(); } }, 0.17f);
            y -= 0.05f;
            board.Label("VOICE", new Vector3(lx, y, -0.004f), 0.17f, ComicColors.Ink, 0.09f);
            for (int i = 0; i < 4; i++) { int v = i; board.Add("voice_" + v, DuckCustom.Voices[v].name, new Vector2(x0 + v * dx, y), new Vector2(0.068f, 0.034f), "#C9F0E2", () => { custom.voice = v; Apply(); }, 0.15f); }
            y -= 0.062f;
            board.Add("random", "RANDOM!", new Vector2(-0.095f, y), new Vector2(0.17f, 0.048f), "#E5CCF5", () => { custom.Randomise(rng); Apply(); }, 0.25f);
            board.Label("POINT YOUR RIGHT HAND + TRIGGER TO PICK.  DONE! WHEN YOU LIKE IT", new Vector3(0f, -bh / 2f + 0.026f, -0.004f), 0.1f, "#6B6480", bw - 0.03f);
            board.Add("save", "DONE!", new Vector2(0.095f, y), new Vector2(0.17f, 0.048f), ComicColors.Green, () => { custom.Save(); SaveCount++; OpenBoard(false); MicroduckVR.Games.GameHud.Get().Big("LOOKING GOOD!", ComicColors.Pink, 1.2f); pd.Body.PlayEmote(EmoteKind.Dance); }, 0.26f);
            Refresh();
        }

        /// <summary>The little card that floats where the board would be: which button opens the board.</summary>
        void BuildPrompt(TMP_FontAsset font)
        {
            var go = new GameObject("Mirror prompt"); go.transform.SetParent(transform, false);
            prompt = go.AddComponent<WorldPanel>(); prompt.font = font; prompt.cam = pd.cam; prompt.reach = 0f;
            prompt.follow = true; prompt.focus = MirrorCentre; prompt.readDist = 0.42f; prompt.sideDeg = 30f; prompt.drop = 0.04f; prompt.minY = StandPos.y + 0.08f;
            prompt.panelRect = new Rect(0f, 0f, 0f, 0f);
            prompt.Slab(new Vector2(0.27f, 0.1f), "#FFFDF5", new Vector3(0f, 0f, 0.003f), 0.3f);
            prompt.Slab(new Vector2(0.27f, 0.018f), ComicColors.Yellow, new Vector3(0f, 0.041f, 0.001f), 0.4f);
            prompt.Slab(new Vector2(0.05f, 0.05f), ComicColors.Ink, new Vector3(-0.09f, -0.008f, -0.002f), 1f);
            prompt.Label("X", new Vector3(-0.09f, -0.008f, -0.005f), 0.3f, "#FFFFFF", 0.05f);
            prompt.Label("PRESS TO\nMAKE YOUR DUCK", new Vector3(0.03f, -0.008f, -0.004f), 0.19f, ComicColors.Ink, 0.18f);
            go.SetActive(false);
        }

        /// <summary>Bring the board out (or put it away); the first-run tutorial opens it for you.</summary>
        public void OpenBoard(bool on)
        {
            BoardOpen = on;
            if (on) { custom = DuckCustom.Load(); Refresh(); }
        }

        void Swatch(string id, Vector2 at, string skin, System.Action click, Vector2 size)
        {
            var sk = ComicLook.Skins[skin];
            var b = board.Add(id, "", at, size, "#FFFFFF", click);
            float r = Mathf.Min(size.x, size.y) * 0.36f;
            var dot = new GameObject("swatch"); dot.transform.SetParent(board.transform, false);
            dot.transform.localPosition = new Vector3(at.x, at.y, -0.0135f); dot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dot.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Leg(0.002f, r, r, 20);
            var mr = dot.AddComponent<MeshRenderer>(); mr.sharedMaterial = WorldPanel.Unlit("#" + ColorUtility.ToHtmlStringRGB(sk.shell)); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Apply() { custom.ApplyTo(pd.gameObject); Refresh(); }

        void Refresh()
        {
            foreach (var b in board.buttons)
            {
                bool on = b.id == "skin_" + custom.skin || b.id == "part_" + paintPart || b.id == "voice_" + custom.voice
                          || (paintPart != "" && b.id == "paint_" + custom.Part(paintPart)) || (paintPart != "" && b.id == "paint_clear" && custom.Part(paintPart) == "");
                board.Select(b, on);
            }
        }

        // ------------------------------------------------------------------ the reflection
        void LateUpdate()
        {
            if (pd == null || pd.cam == null) return;
            var vc = Viewer != null ? Viewer : pd.cam;
            var eye = vc.transform.position;
            var toEye = eye - MirrorCentre;
            bool near = toEye.magnitude < 2.6f && toEye.z > 0.02f && Vector3.Dot(vc.transform.forward, -toEye.normalized) > 0.1f;
            // at the mirror: a prompt for X; X brings the board out (and puts it away), walking off puts it away
            var me = pd.cam.transform.position - MirrorCentre;
            bool zone = me.magnitude < 1.1f && me.z > 0.05f;
            InZone = zone;
            if (zone && openA.WasPressedThisFrame() && !BagMenu.Busy) OpenBoard(!BoardOpen);
            if (!zone && BoardOpen && me.magnitude > 1.4f) OpenBoard(false);
            board.gameObject.SetActive(BoardOpen && me.magnitude < 1.5f);
            prompt.gameObject.SetActive(zone && !BoardOpen && !BagMenu.Busy);
            if (!near) return;
            // the eye reflected through the glass plane (z = const), looking into the room through the mirror's rectangle
            float zPlane = MirrorCentre.z + 0.012f;
            var r = new Vector3(eye.x, eye.y, 2f * zPlane - eye.z);
            mirrorCam.transform.SetPositionAndRotation(r, Quaternion.LookRotation(Vector3.forward, Vector3.up));
            float d = zPlane - r.z, n = Mathf.Max(0.005f, d), f = 30f;
            // camera space x is world +x here; the frustum is the mirror rectangle seen from the reflected eye
            float l = (MirrorCentre.x - W / 2f - r.x) * n / d, rr = (MirrorCentre.x + W / 2f - r.x) * n / d;
            float b = (MirrorCentre.y - H / 2f - r.y) * n / d, t = (MirrorCentre.y + H / 2f - r.y) * n / d;
            mirrorCam.projectionMatrix = Matrix4x4.Frustum(l, rr, b, t, n, f);
            mirrorCam.nearClipPlane = n; mirrorCam.farClipPlane = f;
            foreach (var gg in glints) gg.SetActive(false);          // the reflection must not see its own glints
            // ... nor the comfort vignette that rides on your head: in the mirror it showed up as a dark box round the head
            hidden.Clear();
            foreach (var v in FindObjectsByType<ComfortVignette>()) { var vr = v.GetComponent<Renderer>(); if (vr != null && vr.enabled) { vr.enabled = false; hidden.Add(vr); } }
            // ... nor the boards, cards and captions floating in front of your eyes: the glass showed their backs
            foreach (var root in UiRoots()) if (root != null && root.gameObject.activeInHierarchy)
                foreach (var ur in root.GetComponentsInChildren<Renderer>()) if (ur.enabled) { ur.enabled = false; hidden.Add(ur); }
            // the ink line is measured against the picture it is drawn into; in the glass it has to match the weight of the
            // lines round it on screen, however much of the mirror the viewer's eye takes in
            float seen = 2f * Mathf.Abs(eye.z - zPlane) * Mathf.Tan(vc.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Shader.SetGlobalFloat(InkScaleId, Mathf.Clamp(seen / H, 0.12f, 1.2f));
            mirrorCam.Render();
            Shader.SetGlobalFloat(InkScaleId, 0f);
            foreach (var hr in hidden) hr.enabled = true;
            foreach (var gg in glints) gg.SetActive(true);
        }

        /// <summary>Filming: a sharper reflection (a multiple of the game's own resolution; 1 puts it back).</summary>
        public void Sharpen(int factor)
        {
            int k = Mathf.Clamp(factor, 1, 2), w = RtW * k, h = RtH * k;
            if (rt == null || rt.width == w) return;
            rt.Release(); rt.width = w; rt.height = h; rt.Create();
        }

        System.Collections.Generic.IEnumerable<Transform> UiRoots()
        {
            yield return board.transform; yield return prompt.transform;
            if (MicroduckVR.Custom.Garage.Instance != null && MicroduckVR.Custom.Garage.Instance.Board != null) yield return MicroduckVR.Custom.Garage.Instance.Board.transform;
            if (TutorialCoach.Root != null) yield return TutorialCoach.Root;
            if (MicroduckVR.Games.GameHud.Root != null) yield return MicroduckVR.Games.GameHud.Root;
            if (BagMenu.Root != null) yield return BagMenu.Root;
        }

        void OnDestroy() { openA?.Disable(); if (rt != null) rt.Release(); if (Instance == this) Instance = null; InZone = false; }
    }
}
