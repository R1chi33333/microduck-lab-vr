using MicroduckVR.Comic;
using MicroduckVR.Player;
using MicroduckVR.World;
using TMPro;
using UnityEngine;

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
        const float W = 0.6f, H = 1.4f;

        public WorldPanel Board => board;
        Camera mirrorCam; RenderTexture rt; Transform glass;
        WorldPanel board; DuckCustom custom; PlayerDuck pd;
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
                ToyKit.Box(frame, pos + new Vector3(0f, 0f, 0.0f), new Vector3(size.x, size.y, 0.05f), "#FF8A3D", 0.4f);
            ToyKit.Box(frame, new Vector3(0f, 0f, -0.016f), new Vector3(W + 0.1f, H + 0.1f, 0.016f), "#C9622A", 0.2f);   // back board (wall side)
            ToyKit.Ball(frame, new Vector3(0f, H / 2f + 0.1f, 0.01f), new Vector3(0.12f, 0.12f, 0.04f), "#FFC43D");   // a star-ish knob on top
            foreach (var t in frame.GetComponentsInChildren<Transform>()) t.gameObject.layer = MicroduckVR.Duck.DuckSetup.LayerWorld;
            var col = frame.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(W + 0.16f, H + 0.16f, 0.06f);

            // the glass: a quad whose UVs map straight onto the mirror camera's fitted frustum
            rt = new RenderTexture(512, 1194, 24) { antiAliasing = 2, name = "MirrorRT" };
            var g = new GameObject("Mirror glass"); g.transform.SetParent(transform, false); g.transform.position = MirrorCentre + new Vector3(0f, 0f, 0.0f);
            var mesh = new Mesh { name = "mirror quad" };
            float x0 = -W / 2f, x1 = W / 2f, y0 = -H / 2f, y1 = H / 2f;
            mesh.vertices = new[] { new Vector3(x0, y0, 0.012f), new Vector3(x1, y0, 0.012f), new Vector3(x1, y1, 0.012f), new Vector3(x0, y1, 0.012f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };   // facing +z, into the room mesh.RecalculateNormals(); mesh.RecalculateBounds();
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = rt };
            mat.SetTexture("_BaseMap", rt);
            var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glass = g.transform;
            var cg = new GameObject("Mirror camera"); cg.transform.SetParent(transform, false);
            mirrorCam = cg.AddComponent<Camera>(); mirrorCam.enabled = false; mirrorCam.targetTexture = rt;
            mirrorCam.clearFlags = CameraClearFlags.SolidColor; mirrorCam.backgroundColor = ComicLook.Hex("#9AD8F2");
            mirrorCam.cullingMask = ~0;
            var urp = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); urp.renderPostProcessing = false;

            BuildBoard(font);
        }

        // ------------------------------------------------------------------ customise board
        void BuildBoard(TMP_FontAsset font)
        {
            var go = new GameObject("Customise board");
            go.transform.SetParent(transform, false);
            // to the right of the mirror, low enough for a duck, angled towards the room
            go.transform.position = new Vector3(5.08f, 0.34f, -2.34f);   // on the wall over the foot of the bed
            go.transform.rotation = Quaternion.Euler(-10f, 200f, 0f) * Quaternion.Euler(0f, 0f, 0f);
            go.transform.rotation = Quaternion.LookRotation(go.transform.position - new Vector3(4.42f, 0.25f, -1.8f), Vector3.up);
            board = go.AddComponent<WorldPanel>();
            board.font = font; board.cam = pd.cam; board.reach = 1.6f;
            var bag = pd.GetComponent<BagMenu>(); if (bag != null) board.rightHand = bag.rightHand;
            const float bw = 0.3f, bh = 0.27f;
            board.panelRect = new Rect(-bw / 2f, -bh / 2f, bw, bh);
            board.Slab(new Vector2(bw, bh), "#FFFDF5", new Vector3(0f, 0f, 0.003f), 0.15f);
            board.Slab(new Vector2(bw, 0.03f), "#FF5D73", new Vector3(0f, bh / 2f - 0.015f, 0f), 0.4f);
            board.Label("MIRROR, MIRROR!", new Vector3(0f, bh / 2f - 0.016f, -0.004f), 0.17f, "#FFFFFF", bw);

            float y = bh / 2f - 0.05f;
            board.Label("COLOUR", new Vector3(-bw / 2f + 0.035f, y, -0.004f), 0.09f, "#1B1430", 0.07f);
            for (int i = 0; i < 4; i++) { var s = DuckCustom.SkinIds[i]; Swatch("skin_" + s, new Vector2(-0.06f + i * 0.045f, y), s, () => { custom.skin = s; Apply(); }); }
            y -= 0.04f;
            board.Label("PAINT", new Vector3(-bw / 2f + 0.035f, y, -0.004f), 0.09f, "#1B1430", 0.07f);
            for (int i = 0; i < 4; i++) { var p = DuckCustom.PartIds[i]; board.Add("part_" + p, p.ToUpperInvariant(), new Vector2(-0.06f + i * 0.045f, y), new Vector2(0.04f, 0.026f), "#E6E9F0", () => { paintPart = paintPart == p ? "" : p; Refresh(); }, 0.06f); }
            y -= 0.035f;
            for (int i = 0; i < 4; i++) { var s = DuckCustom.SkinIds[i]; Swatch("paint_" + s, new Vector2(-0.06f + i * 0.045f, y), s, () => { if (paintPart != "") { custom.SetPart(paintPart, s); Apply(); } }, 0.02f); }
            board.Add("paint_clear", "SAME", new Vector2(0.12f, y), new Vector2(0.04f, 0.022f), "#E6E9F0", () => { if (paintPart != "") { custom.SetPart(paintPart, ""); Apply(); } }, 0.05f);
            y -= 0.04f;
            board.Label("VOICE", new Vector3(-bw / 2f + 0.035f, y, -0.004f), 0.09f, "#1B1430", 0.07f);
            for (int i = 0; i < 4; i++) { int v = i; board.Add("voice_" + v, DuckCustom.Voices[v].name, new Vector2(-0.06f + v * 0.045f, y), new Vector2(0.043f, 0.026f), "#C9F0E2", () => { custom.voice = v; Apply(); }, 0.045f); }
            y -= 0.045f;
            board.Add("random", "RANDOM!", new Vector2(-0.07f, y), new Vector2(0.11f, 0.032f), "#E5CCF5", () => { custom.Randomise(rng); Apply(); }, 0.09f);
            board.Add("save", "SAVE", new Vector2(0.07f, y), new Vector2(0.11f, 0.032f), "#30D158", () => { custom.Save(); MicroduckVR.Games.GameHud.Get().Big("LOOKING GOOD!", "#FF5D73", 1.2f); pd.Body.PlayEmote("dance"); }, 0.1f);
            Refresh();
        }

        void Swatch(string id, Vector2 at, string skin, System.Action click, float r = 0.026f)
        {
            var sk = ComicLook.Skins[skin];
            var b = board.Add(id, "", at, new Vector2(0.04f, r + 0.006f), "#FFFFFF", click);
            var dot = new GameObject("swatch"); dot.transform.SetParent(board.transform, false);
            dot.transform.localPosition = new Vector3(at.x, at.y, -0.007f); dot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            dot.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Leg(0.002f, r * 0.42f, r * 0.42f, 20);
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
            var eye = pd.cam.transform.position;
            var toEye = eye - MirrorCentre;
            bool near = toEye.magnitude < 2.6f && toEye.z > 0.02f && Vector3.Dot(pd.cam.transform.forward, -toEye.normalized) > 0.1f;
            board.gameObject.SetActive(toEye.magnitude < 1.5f);
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
            mirrorCam.Render();
        }

        void OnDestroy() { if (rt != null) rt.Release(); if (Instance == this) Instance = null; }
    }
}
