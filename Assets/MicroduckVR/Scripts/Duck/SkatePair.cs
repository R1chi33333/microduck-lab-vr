using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using UnityEngine;

namespace MicroduckVR.Duck
{
    /// <summary>
    /// A pair of roller skates (port of makeSkates in props.js): glossy deck, metal trucks, four fat wheels with hubs
    /// and a rubber stopper, built as comic parts and clipped under each foot's sole. Wheels roll with the distance
    /// travelled. Height = deck top to floor, the amount the duck rises while wearing them.
    /// </summary>
    public class SkatePair
    {
        public const float L = 0.056f, Wd = 0.034f, T = 0.006f, Wr = 0.0075f, Ww = 0.007f;
        public const float Height = T + 0.004f + Wr * 0.6f + Wr;

        public readonly Transform Left, Right;
        readonly List<(Transform t, Quaternion r)> wheels = new List<(Transform, Quaternion)>();
        float roll;

        public SkatePair(string deck = "#E6513D", string wheel = "#F5E6B8")
        {
            Left = One("Skate left", deck, wheel); Right = One("Skate right", deck, wheel);
        }

        Transform One(string name, string deck, string wheel)
        {
            var g = new GameObject(name).transform;      // origin = top centre of the deck, forward +z
            ToyKit.Box(g, new Vector3(0f, -T / 2f, 0f), new Vector3(Wd, T, L), deck, 0.42f);
            ToyKit.Box(g, new Vector3(0f, -0.0008f, 0f), new Vector3(Wd * 0.7f, 0.0012f, L * 0.7f), ComicColors.Ink, 0.3f);      // grip tape
            ToyKit.Box(g, new Vector3(0f, -0.0009f, L * 0.22f), new Vector3(Wd * 0.5f, 0.0012f, 0.006f), "#FFE45C", 0.3f);  // stripe
            foreach (var z in new[] { -L * 0.3f, L * 0.3f }) ToyKit.Box(g, new Vector3(0f, -T - 0.002f, z), new Vector3(Wd * 0.8f, 0.004f, 0.012f), "#B9BEC4", 0.3f);
            ToyKit.Cyl(g, new Vector3(0f, -T - 0.002f, L / 2f + 0.002f), 0.0045f, 0.006f, "#2A2A2C", new Vector3(90f, 0f, 0f), 0.004f, 14);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                var w = new GameObject("wheel").transform; w.SetParent(g, false);
                w.localPosition = new Vector3(sx * (Wd / 2f - Ww / 2f), -T - 0.004f - Wr * 0.6f, sz * L * 0.3f);
                ToyKit.Cyl(w, Vector3.zero, Wr, Ww, wheel, new Vector3(0f, 0f, 90f), -1f, 18);
                ToyKit.Cyl(w, Vector3.zero, Wr * 0.45f, Ww * 1.06f, "#D6D9DD", new Vector3(0f, 0f, 90f), -1f, 10);
                ToyKit.Box(w, Vector3.zero, new Vector3(Ww * 1.08f, Wr * 1.3f, 0.0015f), "#E6513D", 0.3f);   // a spoke so the roll shows
                wheels.Add((w, w.localRotation));
            }
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.gameObject.layer = Layers.Duck;
            g.gameObject.SetActive(false);
            return g;
        }

        /// <summary>Clip the skates under the feet: deck forward along the foot, deck up along the duck's up.</summary>
        public void Attach(DuckBody body)
        {
            foreach (var (side, sk) in new[] { ("left", Left), ("right", Right) })
            {
                var leg = body.Legs[side];
                var parent = leg.ankle;
                sk.SetParent(parent, false);
                var up = parent.InverseTransformDirection(body.transform.up).normalized;
                var fwd = Vector3.ProjectOnPlane(leg.fLocal, up).normalized;
                sk.localRotation = Quaternion.LookRotation(fwd, up);
                sk.localPosition = parent.InverseTransformPoint(leg.sole.position);
                sk.gameObject.SetActive(true);
            }
        }

        public void Detach()
        {
            foreach (var sk in new[] { Left, Right }) { sk.SetParent(null, false); sk.gameObject.SetActive(false); }
        }

        /// <summary>Roll the wheels by a travelled distance (m, signed).</summary>
        public void Spin(float distance)
        {
            roll += distance / Wr * Mathf.Rad2Deg;
            foreach (var (t, r) in wheels) t.localRotation = Quaternion.AngleAxis(roll, Vector3.right) * r;
        }

        public void SetLayer(int layer)
        {
            foreach (var sk in new[] { Left, Right }) foreach (var t in sk.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
