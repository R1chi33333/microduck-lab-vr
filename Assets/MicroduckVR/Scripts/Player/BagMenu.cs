using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Core;
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
    public partial class BagMenu : MonoBehaviour
    {
        /// <summary>The player's camera.</summary>
        public Camera cam;
        /// <summary>Tracked aim pose (under the XR Origin); the camera is used without controllers.</summary>
        public Transform rightHand;
        /// <summary>The bag's text font.</summary>
        public Font font;

        /// <summary>The bag is open, placing a toy or binning: other pointers stand down.</summary>
        public static bool Busy { get; private set; }
        /// <summary>What the bag is doing: closed, panel open, placing a toy, binning toys.</summary>
        public enum Mode { Closed, Panel, Place, Trash }
        /// <summary>What the bag is doing now.</summary>
        public Mode State { get; private set; } = Mode.Closed;
        /// <summary>Toys placed from the bag so far (the tutorial counts them).</summary>
        public int PlacedCount { get; private set; }
        /// <summary>Where the bag panel is.</summary>
        public Vector3 PanelPos => panel.position;
        /// <summary>Where a tile is on the panel (the panel's centre if there is no such tile).</summary>
        public Vector3 TileWorld(string kind) { foreach (var t in tiles) if (t.kind == kind) return t.tile.position; return panel.position; }
        /// <summary>The toy kind being placed, or null.</summary>
        public string Selected { get; private set; }

        const int Cols = 8; const float Tile = 0.03f, Gap = 0.004f, Dist = 0.32f, Reach = 3f;
        const int TotalCap = 60;

        InputAction menuA, selectA, navA;
        Transform panel; TextMeshPro footer, netLine;
        /// <summary>The bag's panel (kept out of the mirror's reflection).</summary>
        public static Transform Root { get; private set; }
        readonly Dictionary<string, TextMeshPro> actionLabels = new Dictionary<string, TextMeshPro>();
        const float NetRow = 0.018f;                                    // the playing-together status line under the footer
        readonly List<(Transform tile, Transform model, string kind)> tiles = new List<(Transform, Transform, string)>();
        readonly List<Vector2> tileHalf = new List<Vector2>();          // half size of each tile, for aiming
        const int ACols = 4; const float AH = 0.024f;                   // the word buttons: four wide ones a row, so their labels are big enough to read
        int hover = -1;
        ComicBeam laser;
        GameObject ghost; string ghostKind; Transform ghostRing;
        Material ghostOk, ghostBad, tileMat, tileHover, inkMat;
        bool triggerWas; float pulse;
        Ray? debugAim;
        AudioSource audioSrc; AudioClip popClip, clickClip;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            menuA = new InputAction("Bag", InputActionType.Button);
            menuA.AddBinding(InputPaths.LeftSecondary); menuA.AddBinding(InputPaths.KeyTab);
            selectA = new InputAction("BagSelect", InputActionType.Value, expectedControlType: "Axis");
            selectA.AddBinding(InputPaths.RightTrigger); selectA.AddBinding(InputPaths.KeyEnter);
            navA = new InputAction("BagNav", InputActionType.Value, expectedControlType: "Vector2");
            navA.AddCompositeBinding("2DVector").With("Up", InputPaths.KeyUp).With("Down", InputPaths.KeyDown).With("Left", InputPaths.KeyLeft).With("Right", InputPaths.KeyRight);
            menuA.Enable(); selectA.Enable(); navA.Enable();

            ghostOk = Unlit(ComicColors.Green, "bag_ghost_ok"); ghostBad = Unlit(ComicColors.Pink, "bag_ghost_bad");
            tileMat = Unlit("#FFF6E4", "bag_tile"); tileHover = Unlit("#FFD23F", "bag_tile_hover"); inkMat = Unlit(ComicColors.Ink, "bag_ink");
            BuildPanel(); BuildLaser();
            audioSrc = gameObject.AddComponent<AudioSource>(); audioSrc.spatialBlend = 0f; audioSrc.volume = 0.5f;
            popClip = Blip(520f, 880f, 0.09f); clickClip = Blip(1200f, 1100f, 0.03f);
        }

        void OnDestroy() { menuA?.Disable(); selectA?.Disable(); navA?.Disable(); Busy = false; }

        static Material Unlit(string hex, string key)
        {
            var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), key);
            m.SetFloat(ShaderIds.Unlit, 1f);
            return m;
        }

        AudioClip Blip(float f0, float f1, float len)
        {
            int n = (int)(44100 * len); var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++) { float u = i / (float)n; ph += 2f * Mathf.PI * Mathf.Lerp(f0, f1, u) / 44100f; d[i] = Mathf.Sin(ph) * (1f - u) * 0.6f; }
            var c = AudioClip.Create("blip", n, 1, 44100, false); c.SetData(d, 0); return c;
        }

        // ------------------------------------------------------------------ public control (also used by editor tests)
        /// <summary>Opens the bag panel in front of the player, closer if a wall is in the way.</summary>
        public void Open()
        {
            var eye = cam.transform.position; var f = cam.transform.forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            var want = eye + f * Dist + Vector3.down * 0.03f;
            // never inside a wall or a piece of furniture: come closer to the eyes instead, and shrink to look the same size
            var to = want - eye; float dist = to.magnitude, k = 1f;
            if (Physics.SphereCast(eye, 0.04f, to / dist, out var hit, dist, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore)) { float near = Mathf.Max(0.16f, hit.distance - 0.02f); want = eye + to / dist * near; k = near / dist; }
            panel.position = want; panel.localScale = Vector3.one * k;
            panel.rotation = Quaternion.LookRotation(f, Vector3.up);
            panel.gameObject.SetActive(true);
            SetMode(Mode.Panel);
            Play(clickClip);
        }
        /// <summary>Closes the bag and drops any toy being placed.</summary>
        public void Close() { panel.gameObject.SetActive(false); DropGhost(); SetMode(Mode.Closed); }
        /// <summary>Editor tests: aim along this ray instead of the controller (null to stop).</summary>
        public void SetDebugAim(Ray? r) => debugAim = r;
        /// <summary>Editor tests: press the trigger once.</summary>
        public void DebugPress() => debugPress = true;
        bool debugPress;

        /// <summary>Filming from outside: the pointer line is left out of the picture.</summary>
        public static bool HideBeam;
        void SetMode(Mode m) { State = m; Busy = m != Mode.Closed; laser.Show(Busy && !HideBeam); if (m != Mode.Place) DropGhost(); }

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
                MicroduckVR.Flock.DuckBrain.ClearView(cam.transform.position, panel.position);
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
                        if (Mathf.Abs(lp.x - tp.x) <= tileHalf[i].x + Gap / 2f && Mathf.Abs(lp.y - tp.y) <= tileHalf[i].y + Gap / 2f) { h = i; break; }
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
                if (Physics.Raycast(ray, out var hit, Reach, WorldQueries.WorldMask | (1 << Layers.Prop), QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var duck = PlayerDuck.Instance != null ? PlayerDuck.Instance.Body.transform.position : cam.transform.position;
                    ok = hit.normal.y > 0.6f && Vector3.Distance(hit.point, duck) < Reach && Count(Selected) < ToyCatalog.Get(Selected).cap && Count(null) < TotalCap
                         && !Physics.CheckSphere(hit.point + Vector3.up * 0.022f, 0.016f, ~((1 << Layers.Duck) | (1 << Layers.PlayerHead)), QueryTriggerInteraction.Ignore);
                    ShowGhost(Selected, hit.point, Yaw(hit.point), ok);
                }
                else DropGhost();
                if (press && ok)
                {
                    if (!Coop.RequestToy(Selected, hit.point, Yaw(hit.point))) ToyCatalog.Spawn(Selected, hit.point, Yaw(hit.point));   // joined: the host makes it
                    PlacedCount++;
                    Play(popClip);
                }
            }
            else if (State == Mode.Trash)
            {
                if (Physics.Raycast(ray, out var hit, Reach, ~((1 << Layers.Duck) | (1 << Layers.PlayerHead)), QueryTriggerInteraction.Ignore))
                {
                    end = hit.point;
                    var tag = hit.collider.GetComponentInParent<ToyTag>();
                    if (tag != null)
                    {
                        tag.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Abs(Mathf.Sin(pulse * 10f)));
                        if (press) { Poof(tag.transform.position); if (!Coop.RequestTrash(tag.gameObject)) Destroy(tag.gameObject); Play(popClip); }
                    }
                }
            }

            if (HideBeam) laser.Show(false);
            laser.Set(ray.origin, end);
            footer.text = State == Mode.Panel && hover >= 0 ? Footer(tiles[hover].kind) : "";
            if (State == Mode.Panel) NetLabels();
        }

        float Yaw(Vector3 at)
        {
            var to = cam.transform.position - at; to.y = 0f;
            return to.sqrMagnitude > 1e-6f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : 0f;
        }

        static int Count(string kind)
        {
            int n = 0;
            foreach (var t in FindObjectsByType<ToyTag>()) if (kind == null || t.kind == kind) n++;
            return n;
        }

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
