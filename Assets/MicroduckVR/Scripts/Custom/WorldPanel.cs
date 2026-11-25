using System;
using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Custom
{
    /// <summary>
    /// A comic panel standing in the world (the mirror's customise board, the garage board): buttons you point at
    /// with the right controller's laser and press with the trigger. While you point at it the trigger does not
    /// work the beak. Buttons are rectangles in the panel's plane (x right, y up), the panel faces -z.
    /// </summary>
    public class WorldPanel : MonoBehaviour
    {
        public class Button { public string id, label; public Rect rect; public Transform t; public MeshRenderer mr; public Material normal; public TextMeshPro text; public Action click; public bool selected; }

        public Camera cam; public Transform rightHand; public TMP_FontAsset font;
        public float reach = 1.2f;
        public readonly List<Button> buttons = new List<Button>();
        public bool Aimed { get; private set; }
        public static bool AnyAimed => aimedCount > 0;
        public static Vector3? DebugPoint;       // editor tests: a point on a panel to "aim" at
        public static bool DebugPress;
        static int aimedCount;

        InputAction trig; bool was; Button hover;
        ComicBeam laser; Material hiMat, selMat;

        public static Material Unlit(string hex) { var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), "panel_" + hex); m.SetFloat("_Unlit", 1f); return m; }

        void Awake()
        {
            trig = new InputAction("PanelPress", InputActionType.Value, expectedControlType: "Axis");
            trig.AddBinding("<XRController>{RightHand}/trigger"); trig.AddBinding("<Keyboard>/enter"); trig.Enable();
            hiMat = Unlit("#FFE45C"); selMat = Unlit("#FF8A9A");
            laser = new ComicBeam("Panel laser", "#FF5D73", 0.0022f, 0.007f);
        }

        void OnDestroy() { trig?.Disable(); if (Aimed) aimedCount--; }
        void OnDisable() { if (Aimed) { aimedCount--; Aimed = false; } laser?.Show(false); }

        public Transform Slab(Vector2 size, string hex, Vector3 at, float round = 0.25f)
        {
            var go = new GameObject("slab"); go.transform.SetParent(transform, false); go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(size.x, size.y, 0.004f), new ComicMesh.Style { radius = Mathf.Min(size.x, size.y) * 0.5f * round, segments = 3 });
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Unlit(hex); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public TextMeshPro Label(string s, Vector3 at, float size, string hex, float width = 0.2f, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject("text"); go.transform.SetParent(transform, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = ComicLook.Hex(hex); t.alignment = align; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width, size * 0.15f);
            return t;
        }

        public Button Add(string id, string label, Vector2 centre, Vector2 size, string hex, Action click, float textSize = 0.06f)
        {
            var b = new Button { id = id, label = label, rect = new Rect(centre - size / 2f, size), click = click };
            b.t = Slab(size, hex, new Vector3(centre.x, centre.y, -0.003f), 0.5f);
            b.mr = b.t.GetComponent<MeshRenderer>(); b.normal = b.mr.sharedMaterial;
            if (!string.IsNullOrEmpty(label)) b.text = Label(label, new Vector3(centre.x, centre.y, -0.006f), textSize, "#1B1430", size.x);
            buttons.Add(b);
            return b;
        }

        public void Select(Button b, bool on) { b.selected = on; Refresh(b); }
        void Refresh(Button b) { b.mr.sharedMaterial = b == hover ? hiMat : b.selected ? selMat : b.normal; b.t.localScale = b == hover ? new Vector3(1.08f, 1.08f, 1f) : Vector3.one; }

        void Update()
        {
            if (cam == null) cam = Camera.main; if (cam == null) return;
            bool tracked = rightHand != null && rightHand.localPosition.sqrMagnitude > 1e-6f;
            var ray = DebugPoint.HasValue ? new Ray(cam.transform.position, (DebugPoint.Value - cam.transform.position).normalized)
                    : tracked ? new Ray(rightHand.position, rightHand.forward) : new Ray(cam.transform.position, cam.transform.forward);
            var plane = new Plane(-transform.forward, transform.position);
            Button h = null; bool aim = false; Vector3 hitP = default;
            if (!BagMenu.Busy && plane.Raycast(ray, out float d) && d < reach)
            {
                hitP = ray.GetPoint(d);
                var lp = transform.InverseTransformPoint(hitP);
                var bounds = PanelRect();
                if (bounds.Contains(new Vector2(lp.x, lp.y)))
                {
                    aim = true;
                    foreach (var b in buttons) if (b.t.gameObject.activeInHierarchy && b.rect.Contains(new Vector2(lp.x, lp.y))) { h = b; break; }
                }
            }
            if (aim != Aimed) { aimedCount += aim ? 1 : -1; Aimed = aim; }
            laser.Show(aim); if (aim) laser.Set(ray.origin, hitP);
            if (h != hover) { var old = hover; hover = h; if (old != null) Refresh(old); if (h != null) Refresh(h); }
            bool t = trig.ReadValue<float>() > 0.5f || DebugPress; bool press = t && !was; was = t; DebugPress = false;
            if (press && hover != null) hover.click?.Invoke();
        }

        public Rect panelRect;
        Rect PanelRect() => panelRect;
    }
}
