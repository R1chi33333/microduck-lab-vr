using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Player
{
    /// <summary>
    /// The toy bag (port of the web inventory bar), for VR. Y (left) or Tab opens a comic panel floating in front of
    /// you with every toy as a little spinning model. Point with the right controller's laser and pull the trigger
    /// to pick one; the laser then shows a ghost of it where it would land (green: fine, red: no room or too many)
    /// and the trigger drops it there. The bin tile switches the laser to removing toys you placed.
    /// Y again goes back to the panel, then closes it. While the bag is open the trigger does not work the beak.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class BagMenu : MonoBehaviour
    {
        public Camera cam;
        public Transform rightHand;          // tracked aim pose (under the XR Origin); the camera is used without controllers
        public Font font;

        public static bool Busy { get; private set; }
        public enum Mode { Closed, Panel, Place, Trash }
        public Mode State { get; private set; } = Mode.Closed;
        public Vector3 TileWorld(string kind) { foreach (var t in tiles) if (t.kind == kind) return t.tile.position; return panel.position; }
        public string Selected { get; private set; }

        const int Cols = 6; const float Tile = 0.03f, Gap = 0.004f, Dist = 0.22f, Reach = 3f;
        const int TotalCap = 60;

        InputAction menuA, selectA, navA;
        Transform panel; TextMeshPro footer;
        readonly List<(Transform tile, Transform model, string kind)> tiles = new List<(Transform, Transform, string)>();
        int hover = -1;
        LineRenderer laser; Transform dot;
        GameObject ghost; string ghostKind; Transform ghostRing;
        Material ghostOk, ghostBad, tileMat, tileHover, inkMat;
        bool triggerWas; float pulse;
        Ray? debugAim;
        AudioSource audioSrc; AudioClip popClip, clickClip;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            menuA = new InputAction("Bag", InputActionType.Button);
            menuA.AddBinding("<XRController>{LeftHand}/{SecondaryButton}"); menuA.AddBinding("<Keyboard>/tab");
            selectA = new InputAction("BagSelect", InputActionType.Value, expectedControlType: "Axis");
            selectA.AddBinding("<XRController>{RightHand}/trigger"); selectA.AddBinding("<Keyboard>/enter");
            navA = new InputAction("BagNav", InputActionType.Value, expectedControlType: "Vector2");
            navA.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            menuA.Enable(); selectA.Enable(); navA.Enable();

            ghostOk = Unlit("#30D158", "bag_ghost_ok"); ghostBad = Unlit("#FF453A", "bag_ghost_bad");
            tileMat = Unlit("#FFF6E4", "bag_tile"); tileHover = Unlit("#FFD23F", "bag_tile_hover"); inkMat = Unlit("#1B1430", "bag_ink");
            BuildPanel(); BuildLaser();
            audioSrc = gameObject.AddComponent<AudioSource>(); audioSrc.spatialBlend = 0f; audioSrc.volume = 0.5f;
            popClip = Blip(520f, 880f, 0.09f); clickClip = Blip(1200f, 1100f, 0.03f);
        }

        void OnDestroy() { menuA?.Disable(); selectA?.Disable(); navA?.Disable(); Busy = false; }

        static Material Unlit(string hex, string key)
        {
            var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), key);
            m.SetFloat("_Unlit", 1f);
            return m;
        }

        AudioClip Blip(float f0, float f1, float len)
        {
            int n = (int)(44100 * len); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++) { float u = i / (float)n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u) / 44100f; d[i] = Mathf.Sin(ph) * (1f - u) * 0.6f; }
            var c = AudioClip.Create("blip", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        // ------------------------------------------------------------------ building
        void BuildPanel()
        {
            panel = new GameObject("Bag panel").transform;
            var entries = ToyCatalog.All;
            int n = entries.Count + 1 + Actions.Length, rows = Mathf.CeilToInt(n / (float)Cols);
            float w = Cols * (Tile + Gap) + Gap * 2f, h = rows * (Tile + Gap) + 0.05f;
            var slab = MakeMesh(panel, ComicMesh.RoundedBox(new Vector3(w, h, 0.006f), new ComicMesh.Style { radius = 0.01f, segments = 3 }), Unlit("#FFFFFF", "bag_slab"));
            slab.localPosition = new Vector3(0f, -0.004f, 0.004f);
            var title = Text(panel, "TOY BAG", 0.12f, new Vector3(0f, h / 2f - 0.017f, -0.001f));
            title.color = ComicLook.Hex("#FF5D73");
            footer = Text(panel, "", 0.075f, new Vector3(0f, -h / 2f + 0.008f, -0.001f));
            float x0 = -(Cols - 1) * (Tile + Gap) / 2f, y0 = h / 2f - 0.045f;
            for (int i = 0; i < n; i++)
            {
                int c = i % Cols, r = i / Cols;
                var tile = MakeMesh(panel, ComicMesh.RoundedBox(new Vector3(Tile, Tile, 0.004f), new ComicMesh.Style { radius = 0.005f, segments = 2 }), tileMat);
                tile.localPosition = new Vector3(x0 + c * (Tile + Gap), y0 - r * (Tile + Gap), 0f);
                Transform model; string kind;
                if (i < entries.Count)
                {
                    kind = entries[i].kind;
                    var vis = ToyCatalog.BuildVisual(kind);
                    model = new GameObject("preview").transform; model.SetParent(tile, false);
                    vis.transform.SetParent(model, false);
                    var b = ToyKit.LocalBounds(vis.transform);
                    float s = 0.022f / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    vis.transform.localScale = Vector3.one * s; vis.transform.localPosition = -b.center * s;
                    model.localPosition = new Vector3(0f, 0f, -0.014f); model.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                }
                else if (i > entries.Count)
                {
                    var (akind, alabel, acol) = Actions[i - entries.Count - 1];
                    kind = akind;
                    tile.GetComponent<MeshRenderer>().sharedMaterial = Unlit(acol, "bag_action_" + akind);
                    model = new GameObject("label").transform; model.SetParent(tile, false); model.localPosition = new Vector3(0f, 0f, -0.0035f);
                    var lt = Text(model, alabel, alabel.Length > 6 ? 0.045f : 0.06f, Vector3.zero);
                    lt.rectTransform.sizeDelta = new Vector2(Tile, Tile);
                }
                else
                {
                    kind = "trash";
                    model = new GameObject("bin").transform; model.SetParent(tile, false); model.localPosition = new Vector3(0f, -0.006f, -0.012f);
                    ToyKit.Cyl(model, new Vector3(0f, 0.006f, 0f), 0.0075f, 0.014f, "#97A0AE", default, 0.0085f, 14);
                    ToyKit.Cyl(model, new Vector3(0f, 0.0145f, 0f), 0.0095f, 0.002f, "#5B6472", default, -1f, 14);
                    ToyKit.Box(model, new Vector3(0f, 0.017f, 0f), new Vector3(0.006f, 0.002f, 0.002f), "#5B6472", 0.3f);
                    model.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                }
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>()) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
                baseMat[tile] = tile.GetComponent<MeshRenderer>().sharedMaterial;
                tiles.Add((tile, model, kind));
            }
            panel.gameObject.SetActive(false);
        }

        static Transform MakeMesh(Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject("mesh"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        TMP_FontAsset fontAsset;
        TextMeshPro Text(Transform parent, string s, float size, Vector3 at)
        {
            var go = new GameObject("text"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>();
            if (fontAsset == null && font != null) fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset != null) t.font = fontAsset;
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = ComicLook.Ink;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(0.24f, 0.03f);
            return t;
        }

        void BuildLaser()
        {
            var go = new GameObject("Bag laser");
            laser = go.AddComponent<LineRenderer>();
            laser.positionCount = 2; laser.startWidth = 0.0016f; laser.endWidth = 0.0008f; laser.useWorldSpace = true;
            laser.sharedMaterial = Unlit("#FF5D73", "bag_laser"); laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.enabled = false;
            dot = MakeMesh(null, ToyKit.Sphere(), Unlit("#FF5D73", "bag_laser")); dot.name = "Bag laser dot";
            dot.localScale = Vector3.one * 0.007f; dot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ public control (also used by editor tests)
        public void Open()
        {
            var eye = cam.transform.position; var f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            panel.position = eye + f * Dist + Vector3.down * 0.015f;
            panel.rotation = Quaternion.LookRotation(f, Vector3.up);
            panel.gameObject.SetActive(true);
            SetMode(Mode.Panel);
            Play(clickClip);
        }
        public void Close() { panel.gameObject.SetActive(false); DropGhost(); SetMode(Mode.Closed); }
        public void SetDebugAim(Ray? r) => debugAim = r;
        public void DebugPress() => debugPress = true;
        bool debugPress;
        // the bottom rows: games, emotes, swapping duck, the view
        static readonly (string kind, string label, string col)[] Actions =
        {
            ("football", "FOOTBALL", "#FFD9A8"), ("race", "RACE", "#E5CCF5"), ("swap", "SWAP DUCK", "#C9F0E2"), ("view", "VIEW", "#CDE8FF"),
            ("dance", "DANCE", "#FFE2EC"), ("nod", "NOD", "#FFE2EC"), ("shake", "SHAKE", "#FFE2EC"), ("look", "LOOK", "#FFE2EC"),
        };
        readonly Dictionary<Transform, Material> baseMat = new Dictionary<Transform, Material>();
        static bool IsAction(string k) { foreach (var a in Actions) if (a.kind == k) return true; return false; }

        void DoAction(string kind)
        {
            var pd = PlayerDuck.Instance;
            Close(); Play(clickClip);
            switch (kind)
            {
                case "football": var fg = MicroduckVR.Games.FootballGame.Ensure(); if (MicroduckVR.Games.FootballGame.Active) fg.StopGame(); else fg.StartGame(); break;
                case "race": var rg = MicroduckVR.Games.RaceGame.Ensure(); if (MicroduckVR.Games.RaceGame.Active) rg.StopRace(); else rg.StartRace(); break;
                case "swap": if (pd != null) pd.SwapWithNearest(); break;
                case "view": if (pd != null) pd.SetThirdPerson(!pd.thirdPerson); break;
                default: if (pd != null) pd.Body.PlayEmote(kind); break;
            }
        }

        public void Choose(string kind)
        {
            if (IsAction(kind)) { DoAction(kind); return; }
            if (kind == "trash") { Selected = null; panel.gameObject.SetActive(false); SetMode(Mode.Trash); Play(clickClip); return; }
            Selected = kind; panel.gameObject.SetActive(false); SetMode(Mode.Place); Play(clickClip);
        }

        void SetMode(Mode m) { State = m; Busy = m != Mode.Closed; laser.enabled = Busy; dot.gameObject.SetActive(Busy); if (m != Mode.Place) DropGhost(); }

        Ray Aim()
        {
            if (debugAim.HasValue) return debugAim.Value;
            bool tracked = rightHand != null && rightHand.localPosition.sqrMagnitude > 1e-6f;
            return tracked ? new Ray(rightHand.position, rightHand.forward) : new Ray(cam.transform.position, cam.transform.forward);
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            if (cam == null) return;
            if (menuA.WasPressedThisFrame())
            {
                if (State == Mode.Closed) Open();
                else if (State == Mode.Panel) Close();
                else { panel.gameObject.SetActive(true); SetMode(Mode.Panel); Play(clickClip); }
            }
            if (State == Mode.Closed) return;

            bool trig = selectA.ReadValue<float>() > 0.5f, press = (trig && !triggerWas) || debugPress; triggerWas = trig; debugPress = false;
            var ray = Aim();
            Vector3 end = ray.origin + ray.direction * 0.6f;
            pulse += Time.deltaTime;

            if (State == Mode.Panel)
            {
                // spin the previews; hover by laser (or arrow keys on the desktop)
                foreach (var (_, model, k) in tiles) if (!IsAction(k)) model.localRotation = Quaternion.Euler(-20f, pulse * 40f, 0f);
                var plane = new Plane(-panel.forward, panel.position);
                int h = -1;
                if (plane.Raycast(ray, out float d) && d < 1.5f)
                {
                    end = ray.GetPoint(d);
                    var lp = panel.InverseTransformPoint(end);
                    for (int i = 0; i < tiles.Count; i++)
                    {
                        var tp = tiles[i].tile.localPosition;
                        if (Mathf.Abs(lp.x - tp.x) <= (Tile + Gap) / 2f && Mathf.Abs(lp.y - tp.y) <= (Tile + Gap) / 2f) { h = i; break; }
                    }
                }
                var nav = navA.ReadValue<Vector2>();
                if (nav.sqrMagnitude > 0.5f && navA.WasPressedThisFrame()) h = Mathf.Clamp((hover < 0 ? 0 : hover) + (int)Mathf.Sign(nav.x) * (Mathf.Abs(nav.x) > 0.5f ? 1 : 0) - (int)Mathf.Sign(nav.y) * (Mathf.Abs(nav.y) > 0.5f ? Cols : 0), 0, tiles.Count - 1);
                else if (h < 0 && debugAim == null && (rightHand == null || rightHand.localPosition.sqrMagnitude < 1e-6f)) h = hover;
                SetHover(h);
                if (press && hover >= 0) Choose(tiles[hover].kind);
            }
            else if (State == Mode.Place)
            {
                bool ok = false;
                if (Physics.Raycast(ray, out var hit, Reach, WorldQueries.WorldMask | (1 << DuckSetup.LayerProp), QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var duck = PlayerDuck.Instance != null ? PlayerDuck.Instance.Body.transform.position : cam.transform.position;
                    ok = hit.normal.y > 0.6f && Vector3.Distance(hit.point, duck) < Reach && Count(Selected) < ToyCatalog.Get(Selected).cap && Count(null) < TotalCap
                         && !Physics.CheckSphere(hit.point + Vector3.up * 0.022f, 0.016f, ~((1 << DuckSetup.LayerDuck) | (1 << DuckSetup.LayerPlayerHead)), QueryTriggerInteraction.Ignore);
                    ShowGhost(Selected, hit.point, Yaw(hit.point), ok);
                }
                else DropGhost();
                if (press && ok)
                {
                    ToyCatalog.Spawn(Selected, hit.point, Yaw(hit.point));
                    Play(popClip);
                }
            }
            else if (State == Mode.Trash)
            {
                if (Physics.Raycast(ray, out var hit, Reach, ~((1 << DuckSetup.LayerDuck) | (1 << DuckSetup.LayerPlayerHead)), QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var tag = hit.collider.GetComponentInParent<ToyTag>();
                    if (tag != null)
                    {
                        tag.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Abs(Mathf.Sin(pulse * 10f)));
                        if (press) { Poof(tag.transform.position); Destroy(tag.gameObject); Play(popClip); }
                    }
                }
            }

            laser.SetPosition(0, ray.origin); laser.SetPosition(1, end);
            dot.position = end;
            footer.text = State == Mode.Panel && hover >= 0 ? Footer(tiles[hover].kind) : "";
        }

        float Yaw(Vector3 at)
        {
            var to = cam.transform.position - at; to.y = 0f;
            return to.sqrMagnitude > 1e-6f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;
        }

        string Footer(string kind)
        {
            if (kind == "trash") return "BIN: REMOVE TOYS";
            if (kind == "football") return MicroduckVR.Games.FootballGame.Active ? "STOP THE MATCH" : "FOOTBALL ON THE RUG";
            if (kind == "race") return MicroduckVR.Games.RaceGame.Active ? "STOP THE RACE" : "RACE ROUND THE HOUSE";
            if (kind == "swap") return "BECOME THE DUCK IN FRONT";
            if (kind == "view") return "FIRST / THIRD PERSON";
            if (IsAction(kind)) return kind.ToUpperInvariant() + "!";
            var e = ToyCatalog.Get(kind);
            return $"{e.label.ToUpperInvariant()}  {Count(kind)}/{e.cap}" + (e.graspable ? "" : "  (KICK IT!)");
        }

        void SetHover(int h)
        {
            if (h == hover) return;
            if (hover >= 0) { tiles[hover].tile.GetComponent<MeshRenderer>().sharedMaterial = baseMat[tiles[hover].tile]; tiles[hover].tile.localScale = Vector3.one; }
            hover = h;
            if (hover >= 0) { tiles[hover].tile.GetComponent<MeshRenderer>().sharedMaterial = tileHover; tiles[hover].tile.localScale = Vector3.one * 1.15f; Play(clickClip, 0.4f); }
        }

        static int Count(string kind)
        {
            int n = 0;
            foreach (var t in FindObjectsByType<ToyTag>(FindObjectsSortMode.None)) if (kind == null || t.kind == kind) n++;
            return n;
        }

        void ShowGhost(string kind, Vector3 at, float yaw, bool ok)
        {
            if (ghost == null || ghostKind != kind)
            {
                DropGhost();
                ghost = ToyCatalog.BuildVisual(kind); ghostKind = kind; ghost.name = "Bag ghost";
                foreach (var mr in ghost.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostRing = MakeMesh(ghost.transform, ComicMesh.Ring(0.036f, 0.03f, 0.002f, 32), ghostOk);
                ghostRing.localPosition = Vector3.up * 0.0015f;
            }
            ghost.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var m = ok ? ghostOk : ghostBad;
            foreach (var mr in ghost.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = m;
            float s = 1f + 0.04f * Mathf.Sin(pulse * 6f);
            ghostRing.localScale = new Vector3(s, 1f, s);
        }

        void DropGhost() { if (ghost != null) Destroy(ghost); ghost = null; ghostKind = null; }

        void Poof(Vector3 at)
        {
            var mat = Unlit("#FFFFFF", "bag_poof");
            for (int i = 0; i < 6; i++)
            {
                var p = MakeMesh(null, ToyKit.Sphere(), mat);
                p.position = at + Random.insideUnitSphere * 0.01f + Vector3.up * 0.01f;
                p.gameObject.AddComponent<PoofPuff>().dir = (Random.insideUnitSphere + Vector3.up).normalized;
            }
        }

        void Play(AudioClip c, float vol = 1f) { if (audioSrc != null && c != null) audioSrc.PlayOneShot(c, vol); }

        /// <summary>A comic smoke puff: pops out, drifts, shrinks away.</summary>
        class PoofPuff : MonoBehaviour
        {
            public Vector3 dir; float age;
            void Update()
            {
                age += Time.deltaTime; float u = age / 0.45f;
                if (u >= 1f) { Destroy(gameObject); return; }
                transform.position += dir * 0.08f * (1f - u) * Time.deltaTime;
                transform.localScale = Vector3.one * (u < 0.3f ? Mathf.Lerp(0.005f, 0.022f, u / 0.3f) : Mathf.Lerp(0.022f, 0f, (u - 0.3f) / 0.7f));
            }
        }
    }
}
