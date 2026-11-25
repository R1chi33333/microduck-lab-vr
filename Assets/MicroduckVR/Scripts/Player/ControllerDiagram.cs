using System.Collections.Generic;
using MicroduckVR.Comic;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>
    /// A flat comic drawing of the two Touch controllers for tutorial cards (seen from above: tracking ring, body,
    /// thumbstick, the two face buttons, the trigger tab at the front and the grip tab on the inner side). Highlight()
    /// paints the parts to press yellow and rings each with a wobbly red circle, plus an arrow from the label.
    /// Built in the parent's XY plane, facing -z (towards the viewer).
    /// </summary>
    public class ControllerDiagram
    {
        public enum Part { Stick, StickClick, Trigger, Grip, A, B, X, Y }

        readonly Transform root;
        readonly Dictionary<(bool right, Part part), (Transform t, MeshRenderer mr, Material normal, float r)> parts = new Dictionary<(bool, Part), (Transform, MeshRenderer, Material, float)>();
        readonly List<Transform> rings = new List<Transform>();
        readonly Material hi, red, ink;
        readonly TMP_FontAsset font;
        readonly TextMeshPro leftLabel, rightLabel;
        float t;

        static Material Unlit(string hex)
        {
            var m = ComicLook.ToonMaterial(ComicLook.Hex(hex), "diagram_" + hex); m.SetFloat("_Unlit", 1f); return m;
        }

        public ControllerDiagram(Transform parent, Vector3 at, float scale, TMP_FontAsset font)
        {
            this.font = font;
            root = new GameObject("Controllers").transform; root.SetParent(parent, false);
            root.localPosition = at; root.localScale = Vector3.one * scale;
            hi = Unlit("#FFE45C"); red = Unlit("#FF2A2A"); ink = Unlit("#1B1430");
            Build(false, new Vector3(-0.032f, 0f, 0f));
            Build(true, new Vector3(0.032f, 0f, 0f));
            leftLabel = Label("LEFT", new Vector3(-0.032f, -0.047f, -0.004f), 0.09f, "#97A0AE");
            rightLabel = Label("RIGHT", new Vector3(0.032f, -0.047f, -0.004f), 0.09f, "#97A0AE");
        }

        Transform Disc(Transform p, Vector3 at, float r, string hex, int sides = 24)
        {
            var go = new GameObject("disc"); go.transform.SetParent(p, false);
            go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Leg(0.002f, r, r, sides);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Unlit(hex); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        Transform Slab(Transform p, Vector3 at, Vector2 size, string hex, float round, float rotZ = 0f)
        {
            var go = new GameObject("slab"); go.transform.SetParent(p, false);
            go.transform.localPosition = at; go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            float m = Mathf.Min(size.x, size.y) * 0.5f;
            go.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(size.x, size.y, 0.002f), new ComicMesh.Style { radius = m * round, segments = 3 });
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = Unlit(hex); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        TextMeshPro Label(string s, Vector3 at, float size, string hex, Transform p = null)
        {
            var go = new GameObject("label"); go.transform.SetParent(p ?? root, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); if (font != null) t.font = font;
            t.text = s; t.fontSize = size; t.color = ComicLook.Hex(hex); t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(0.06f, 0.012f);
            return t;
        }

        void Add(bool right, Part part, Transform t, float r)
        {
            var mr = t.GetComponent<MeshRenderer>();
            parts[(right, part)] = (t, mr, mr.sharedMaterial, r);
        }

        void Build(bool right, Vector3 at)
        {
            float s = right ? 1f : -1f;           // mirror the left controller
            var c = new GameObject(right ? "right" : "left").transform; c.SetParent(root, false); c.localPosition = at;
            // trigger tab at the front (drawn first, behind the body), grip tab on the inner side
            Add(right, Part.Trigger, Slab(c, new Vector3(-s * 0.004f, 0.028f, 0.001f), new Vector2(0.012f, 0.014f), "#3D3B4F", 0.6f, s * 15f), 0.009f);
            Add(right, Part.Grip, Slab(c, new Vector3(-s * 0.019f, -0.012f, 0.001f), new Vector2(0.008f, 0.022f), "#3D3B4F", 0.8f, s * -8f), 0.012f);
            // tracking ring and body
            var ring = new GameObject("ring"); ring.transform.SetParent(c, false); ring.transform.localPosition = new Vector3(s * 0.006f, 0.02f, 0f);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ring.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Ring(0.017f, 0.0115f, 0.002f, 32);
            var rr = ring.AddComponent<MeshRenderer>(); rr.sharedMaterial = Unlit("#5B6472"); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Slab(c, new Vector3(0f, -0.004f, -0.0005f), new Vector2(0.03f, 0.052f), "#97A0AE", 0.9f);
            Slab(c, new Vector3(0f, 0.002f, -0.001f), new Vector2(0.026f, 0.03f), "#B9C3CF", 0.7f);
            // thumbstick: the same circle highlights for "push" and for "click"
            var stick = Disc(c, new Vector3(-s * 0.005f, 0.008f, -0.002f), 0.0068f, "#2A2B2E");
            Disc(stick, new Vector3(0f, -0.0015f, 0f), 0.0042f, "#5B6472").localRotation = Quaternion.identity;   // stick parent faces -z along its -y
            Add(right, Part.Stick, stick, 0.0068f); parts[(right, Part.StickClick)] = parts[(right, Part.Stick)];
            // face buttons: A / B on the right, X / Y on the left
            var low = Disc(c, new Vector3(s * 0.006f, -0.005f, -0.002f), 0.0045f, "#E6E9F0");
            var high = Disc(c, new Vector3(s * 0.0085f, 0.0045f, -0.002f), 0.0045f, "#E6E9F0");
            Add(right, right ? Part.A : Part.X, low, 0.0045f); Add(right, right ? Part.B : Part.Y, high, 0.0045f);
            Label(right ? "A" : "X", low.localPosition + new Vector3(0f, -0.0002f, -0.002f), 0.06f, "#1B1430", c);
            Label(right ? "B" : "Y", high.localPosition + new Vector3(0f, -0.0002f, -0.002f), 0.06f, "#1B1430", c);
        }

        /// <summary>Highlight these parts (right hand?, part). Others go back to normal.</summary>
        public void Highlight(params (bool right, Part part)[] which)
        {
            foreach (var kv in parts) kv.Value.mr.sharedMaterial = kv.Value.normal;
            foreach (var r in rings) Object.Destroy(r.gameObject);
            rings.Clear();
            bool anyL = false, anyR = false;
            foreach (var (right, part) in which)
            {
                if (!parts.TryGetValue((right, part), out var p)) continue;
                p.mr.sharedMaterial = hi;
                if (right) anyR = true; else anyL = true;
                // a hand-drawn red circle round it (two slightly offset rings read as a pen stroke)
                var world = p.t.position; var local = root.InverseTransformPoint(world);
                for (int k = 0; k < 2; k++)
                {
                    var g = new GameObject("circle"); g.transform.SetParent(root, false);
                    g.transform.localPosition = new Vector3(local.x + k * 0.0006f, local.y - k * 0.0005f, -0.006f);
                    g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    float ro = p.r + 0.0055f + k * 0.0006f;
                    g.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Ring(ro, ro - 0.0016f, 0.001f, 28);
                    var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = red; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rings.Add(g.transform);
                }
            }
            leftLabel.color = ComicLook.Hex(anyL ? "#FF2A2A" : "#97A0AE");
            rightLabel.color = ComicLook.Hex(anyR ? "#FF2A2A" : "#97A0AE");
        }

        /// <summary>Gentle pulse of the red circles.</summary>
        public void Tick(float dt)
        {
            t += dt;
            float s = 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 4f));
            foreach (var r in rings) r.localScale = new Vector3(s, 1f, s);
        }
    }
}
