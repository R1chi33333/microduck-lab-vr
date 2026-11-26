using System;
using System.Collections.Generic;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// Every bag toy (port of props.js): its label, whether a duck can carry it in its beak (and how wide the beak
    /// must open) or only kick it, its mass, and how to build its comic model. Models rest on y = 0, front along +z.
    /// </summary>
    public static class ToyCatalog
    {
        public class Entry
        {
            public string kind, label, icon;          // icon: main colour for the bag tile
            public bool graspable = true, ball;       // ball: sphere collider, rolls
            public float grip = 0.02f, mass = 0.005f, bounce = 0.1f;
            public int cap = 6;
            public Action<Transform> build;
        }

        public static readonly List<Entry> Entries = new List<Entry>();
        static readonly Dictionary<string, Entry> byKind = new Dictionary<string, Entry>();
        public static Entry Get(string kind) { Init(); return byKind.TryGetValue(kind, out var e) ? e : null; }
        public static IReadOnlyList<Entry> All { get { Init(); return Entries; } }

        const string Ink = "#1B1430";
        static void Add(Entry e) { Entries.Add(e); byKind[e.kind] = e; }

        static void Init()
        {
            if (Entries.Count > 0) return;
            Add(new Entry { kind = "sock_ball", label = "Sock ball", icon = "#D9534F", graspable = false, ball = true, mass = 0.025f, bounce = 0.35f, cap = 3, build = SockBall });
            Add(new Entry { kind = "foam_cube", label = "Foam cube", icon = "#F0745A", grip = 0.022f, mass = 0.004f, build = FoamCube });
            Add(new Entry { kind = "ring", label = "Ring", icon = "#6FB7A8", grip = 0.012f, mass = 0.008f, build = RingToy });
            Add(new Entry { kind = "pencil", label = "Pencil", icon = "#F2B705", grip = 0.0084f, mass = 0.006f, build = Pencil });
            Add(new Entry { kind = "crayon", label = "Crayon", icon = "#2F6FD6", grip = 0.009f, mass = 0.005f, build = t => Crayon(t, "#2F6FD6") });
            Add(new Entry { kind = "crayon_red", label = "Red crayon", icon = "#E8323F", grip = 0.009f, mass = 0.005f, build = t => Crayon(t, "#E8323F") });
            Add(new Entry { kind = "crayon_green", label = "Green crayon", icon = "#2FA84F", grip = 0.009f, mass = 0.005f, build = t => Crayon(t, "#2FA84F") });
            Add(new Entry { kind = "key", label = "Key", icon = "#C9A24A", grip = 0.0042f, mass = 0.004f, build = Key });
            Add(new Entry { kind = "bottle_cap", label = "Bottle cap", icon = "#D7262B", grip = 0.006f, mass = 0.002f, build = BottleCap });
            Add(new Entry { kind = "bread_crust", label = "Bread crust", icon = "#B8742E", grip = 0.02f, mass = 0.003f, build = BreadCrust });
            Add(new Entry { kind = "toy_brick", label = "Toy brick", icon = "#D7262B", grip = 0.016f, mass = 0.002f, build = Brick });
            Add(new Entry { kind = "toy_duck", label = "Rubber duck", icon = "#FFD23F", grip = 0.019f, mass = 0.006f, build = t => RubberDuck(t, "#FFD23F", "#F28C28") });
            Add(new Entry { kind = "rubber_duck_pink", label = "Pink duck", icon = "#FF9EC0", grip = 0.019f, mass = 0.006f, build = t => RubberDuck(t, "#FF9EC0", "#F2683C") });
            Add(new Entry { kind = "rubber_duck_blue", label = "Blue duck", icon = "#7CC4F2", grip = 0.019f, mass = 0.006f, build = t => RubberDuck(t, "#7CC4F2", "#F2A33C") });
            Add(new Entry { kind = "rubber_duck_mint", label = "Mint duck", icon = "#8FE0C0", grip = 0.019f, mass = 0.006f, build = t => RubberDuck(t, "#8FE0C0", "#F28C28") });
            Add(new Entry { kind = "chess_pawn", label = "Chess pawn", icon = "#EFE7DA", grip = 0.013f, mass = 0.012f, build = Pawn });
            Add(new Entry { kind = "chess_knight", label = "Chess knight", icon = "#EFE7DA", grip = 0.016f, mass = 0.015f, build = Knight });
            Add(new Entry { kind = "wicker_ball", label = "Wicker ball", icon = "#E8A85A", graspable = false, ball = true, mass = 0.02f, bounce = 0.45f, cap = 3, build = WickerBall });
            Add(new Entry { kind = "toy_car", label = "Toy car", icon = "#C8372D", graspable = false, mass = 0.03f, cap = 3, build = ToyCar });
            Add(new Entry { kind = "cookie", label = "Cookie", icon = "#C48A4E", grip = 0.0113f, mass = 0.008f, build = Cookie });
            Add(new Entry { kind = "toast", label = "Toast", icon = "#D9A35E", grip = 0.0063f, mass = 0.01f, build = Toast });
            Add(new Entry { kind = "croissant", label = "Croissant", icon = "#D39552", grip = 0.0219f, mass = 0.01f, build = Croissant });
            Add(new Entry { kind = "strawberry", label = "Strawberry", icon = "#E8323F", grip = 0.02f, mass = 0.006f, build = Strawberry });
            Add(new Entry { kind = "grape", label = "Grape", icon = "#7B4FC8", grip = 0.017f, mass = 0.004f, build = Grape });
            Add(new Entry { kind = "cracker", label = "Cracker", icon = "#E9B86A", grip = 0.005f, mass = 0.003f, build = Cracker });
            Add(new Entry { kind = "toy_figure", label = "Toy figure", icon = "#3A86FF", grip = 0.018f, mass = 0.006f, build = Figure });
            Add(new Entry { kind = "button", label = "Button", icon = "#FF5D73", grip = 0.004f, mass = 0.001f, build = ButtonToy });
            Add(new Entry { kind = "leaf", label = "Leaf", icon = "#5BB54A", grip = 0.006f, mass = 0.0008f, build = Leaf });
            Add(new Entry { kind = "pebble", label = "Pebble", icon = "#A8A2B8", grip = 0.0155f, mass = 0.006f, build = Pebble });
        }

        // ------------------------------------------------------------------ spawning
        /// <summary>The toy's model only (bag previews and placement ghosts).</summary>
        public static GameObject BuildVisual(string kind)
        {
            var e = Get(kind); if (e == null) return null;
            var go = new GameObject(e.label);
            e.build(go.transform);
            return go;
        }

        /// <summary>A live toy resting on a surface at pos: physics body and collider, carried with the beak if graspable.</summary>
        public static GameObject Spawn(string kind, Vector3 pos, float yawDeg)
        {
            var e = Get(kind); if (e == null) return null;
            var go = BuildVisual(kind);
            go.transform.SetPositionAndRotation(pos + Vector3.up * 0.001f, Quaternion.Euler(0f, yawDeg, 0f));
            int layer = e.graspable ? DuckSetup.LayerToy : DuckSetup.LayerProp;
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
            var b = ToyKit.LocalBounds(go.transform);
            var pm = new PhysicsMaterial(e.kind) { bounciness = e.bounce, dynamicFriction = e.ball ? 0.4f : 0.7f, staticFriction = e.ball ? 0.4f : 0.7f, bounceCombine = PhysicsMaterialCombine.Maximum };
            if (e.ball) { var sc = go.AddComponent<SphereCollider>(); sc.center = b.center; sc.radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)); sc.sharedMaterial = pm; }
            else { var bc = go.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; bc.sharedMaterial = pm; }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = e.mass; rb.linearDamping = e.ball ? 0.1f : 0.3f; rb.angularDamping = e.ball ? 0.2f : 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (e.graspable) { var g = go.AddComponent<Grabbable>(); g.label = e.label.ToLowerInvariant(); g.gripWidth = e.grip; }
            go.AddComponent<ToyTag>().kind = kind;
            return go;
        }

        // ------------------------------------------------------------------ models
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        static void SockBall(Transform t)
        {
            const float r = 0.03f;
            ToyKit.Ball(t, V(0, r, 0), Vector3.one * r * 2f, "#E8DCC8");
            string[] bands = { "#D9534F", "#3E6E8E", "#F2B705", "#D9534F" };
            for (int i = 0; i < bands.Length; i++)
            {
                float lat = -0.9f + i * 0.6f, y = Mathf.Sin(lat) * r, rr = Mathf.Cos(lat) * r;
                ToyKit.Ring(t, V(0, r + y, 0), rr + 0.0012f, rr * 0.7f, 0.007f, bands[i]);
            }
            ToyKit.Ball(t, V(0, 2f * r - 0.002f, 0), V(0.016f, 0.008f, 0.016f), "#D9534F");     // the tucked-in cuff
        }

        static void FoamCube(Transform t)
        {
            const float s = 0.022f;
            ToyKit.Box(t, V(0, s / 2f, 0), Vector3.one * s, "#F0745A", 0.35f);
            var rng = new System.Random(3);
            foreach (var n in new[] { Vector3.up, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                for (int i = 0; i < 4; i++)
                {
                    var u = Vector3.Cross(n, Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up);
                    var w = Vector3.Cross(n, u);
                    var p = n * s * 0.5f + u * ((float)rng.NextDouble() - 0.5f) * s * 0.7f + w * ((float)rng.NextDouble() - 0.5f) * s * 0.7f;
                    ToyKit.Ball(t, p + Vector3.up * s / 2f, Vector3.one * 0.0022f, "#C9503C");    // foam pores
                }
        }

        static void RingToy(Transform t)
        {
            ToyKit.Ring(t, V(0, 0.006f, 0), 0.022f, 0.01f, 0.012f, "#6FB7A8");
            ToyKit.Ring(t, V(0, 0.0123f, 0), 0.02f, 0.012f, 0.001f, "#A6E3D6");                  // highlight on the top
        }

        static void Pencil(Transform t)
        {
            const float r = 0.0042f, L = 0.12f, body = L * 0.78f;
            var a = new Vector3(0, 0, 90f);
            var root = new GameObject("pencil").transform; root.SetParent(t, false); root.localPosition = V(0, r * 0.87f, 0);
            ToyKit.Cyl(root, V(0, 0, 0), r, body, "#F2B705", a, -1f, 6);
            ToyKit.Cyl(root, V(-(body / 2f + L * 0.06f), 0, 0), r * 0.95f, L * 0.12f, "#E2C08D", a, 0.0012f, 12);
            ToyKit.Cyl(root, V(-(body / 2f + L * 0.12f + 0.002f), 0, 0), 0.0013f, 0.004f, "#2A2A2C", a, 0.0002f, 8);
            ToyKit.Cyl(root, V(body / 2f + L * 0.025f, 0, 0), r * 1.04f, L * 0.05f, "#C9CCD1", a, -1f, 14);
            ToyKit.Cyl(root, V(body / 2f + L * 0.075f, 0, 0), r * 0.95f, L * 0.05f, "#E98A8A", a, -1f, 14);
            ToyKit.Box(root, V(-0.01f, r * 0.86f, 0), V(0.03f, 0.0008f, 0.003f), "#1B1430", 0.2f);  // printed lettering
        }

        static void Crayon(Transform t, string col)
        {
            const float r = 0.0045f, L = 0.09f;
            var a = new Vector3(0, 0, 90f);
            var root = new GameObject("crayon").transform; root.SetParent(t, false); root.localPosition = V(0, r, 0);
            ToyKit.Cyl(root, V(0, 0, 0), r, L * 0.82f, col, a, -1f, 14);
            ToyKit.Cyl(root, V(-L * 0.46f, 0, 0), r, L * 0.1f, col, a, r * 0.35f, 14);
            ToyKit.Cyl(root, V(L * 0.08f, 0, 0), r * 1.07f, L * 0.6f, "#F3EBD7", a, -1f, 14);
            foreach (var x in new[] { -0.012f, 0.03f }) ToyKit.Cyl(root, V(x, 0, 0), r * 1.09f, 0.003f, col, a, -1f, 14);
            ToyKit.Box(root, V(L * 0.1f, r * 1.05f, 0), V(0.022f, 0.0008f, 0.003f), Ink, 0.2f);
        }

        static void Key(Transform t)
        {
            const string gold = "#C9A24A"; const float th = 0.0024f;
            ToyKit.Ring(t, V(-0.017f, th / 2f, 0), 0.0113f, 0.0045f, th, gold);
            ToyKit.Box(t, V(0.007f, th / 2f, 0), V(0.032f, th, 0.0042f), gold, 0.3f);
            foreach (var (x, d) in new[] { (0.016f, 0.0035f), (0.019f, 0.0028f), (0.022f, 0.0042f) }) ToyKit.Box(t, V(x, th / 2f, -0.0021f - d / 2f), V(0.0022f, th, d), gold, 0.2f);
            ToyKit.Ring(t, V(-0.017f, th + 0.0002f, 0), 0.0098f, 0.0072f, 0.0004f, "#F2D98A");
        }

        static void BottleCap(Transform t)
        {
            const float r = 0.0145f, h = 0.006f;
            ToyKit.Cyl(t, V(0, h / 2f, 0), r * 1.03f, h, "#D7262B", default, r, 28);
            for (int i = 0; i < 21; i++) { float ang = i / 21f * 360f; ToyKit.Box(t, Quaternion.Euler(0, ang, 0) * V(r * 1.01f, h / 2f, 0), V(0.0018f, h * 0.9f, 0.0022f), "#B01E22", 0.3f, V(0, ang, 0)); }
            ToyKit.Cyl(t, V(0, h + 0.0002f, 0), r * 0.86f, 0.0005f, "#F4F1EA", default, -1f, 24);
            ToyKit.Ring(t, V(0, h + 0.0005f, 0), r * 0.55f, r * 0.45f, 0.0004f, "#D7262B");
        }

        static void BreadCrust(Transform t)
        {
            ToyKit.Box(t, V(0, 0.009f, 0), V(0.052f, 0.016f, 0.02f), "#B8742E", 0.45f);
            ToyKit.Box(t, V(0, 0.0085f, 0.0085f), V(0.046f, 0.012f, 0.004f), "#F1DEB4", 0.4f);
            for (int i = 0; i < 5; i++) ToyKit.Ball(t, V(-0.018f + i * 0.009f, 0.0165f, -0.002f), V(0.006f, 0.0025f, 0.012f), "#8A5220");
        }

        static void Brick(Transform t)
        {
            const float W = 0.016f, H = 0.0096f;
            ToyKit.Box(t, V(0, H / 2f, 0), V(W, H, W), "#D7262B", 0.12f);
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f }) ToyKit.Cyl(t, V(sx * W / 4f, H + 0.00085f, sz * W / 4f), 0.0024f, 0.0017f, "#E8434A", default, -1f, 12);
        }

        static void RubberDuck(Transform t, string body, string beak)
        {
            // web +x forward -> here +z forward
            ToyKit.Ball(t, V(0, 0.012f, 0), V(0.028f, 0.0238f, 0.035f), body);
            ToyKit.Ball(t, V(0, 0.02f, -0.016f), V(0.0096f, 0.0156f, 0.012f), body, V(-30, 0, 0));
            ToyKit.Ball(t, V(0, 0.027f, 0.009f), Vector3.one * 0.019f, body);
            ToyKit.Ball(t, V(0, 0.025f, 0.019f), V(0.01f, 0.0055f, 0.014f), beak);
            foreach (var s in new[] { -1f, 1f })
            {
                ToyKit.Ball(t, V(s * 0.0055f, 0.0305f, 0.0155f), Vector3.one * 0.003f, "#111111");
                ToyKit.Ball(t, V(s * 0.0056f, 0.0313f, 0.0165f), Vector3.one * 0.0011f, "#FFFFFF");
                ToyKit.Ball(t, V(s * 0.012f, 0.015f, -0.002f), V(0.006f, 0.009f, 0.016f), body, V(-15, 0, 0));   // wings
            }
        }

        static void Pawn(Transform t)
        {
            const string c = "#EFE7DA", d = "#D9CFBF";
            ToyKit.Cyl(t, V(0, 0.003f, 0), 0.016f, 0.006f, c, default, -1f, 22);
            ToyKit.Cyl(t, V(0, 0.009f, 0), 0.016f, 0.006f, c, default, 0.009f, 22);
            ToyKit.Cyl(t, V(0, 0.021f, 0), 0.009f, 0.018f, c, default, 0.006f, 18);
            ToyKit.Cyl(t, V(0, 0.032f, 0), 0.0095f, 0.004f, d, default, -1f, 18);
            ToyKit.Ball(t, V(0, 0.042f, 0), Vector3.one * 0.016f, c);
            ToyKit.Ring(t, V(0, 0.0062f, 0), 0.0162f, 0.014f, 0.001f, d);
        }

        static void Knight(Transform t)
        {
            const string c = "#EFE7DA", d = "#D9CFBF";
            ToyKit.Cyl(t, V(0, 0.003f, 0), 0.0155f, 0.006f, c, default, -1f, 22);
            ToyKit.Cyl(t, V(0, 0.012f, 0), 0.0137f, 0.012f, c, default, 0.01f, 18);
            ToyKit.Box(t, V(0, 0.03f, -0.002f), V(0.012f, 0.026f, 0.014f), c, 0.4f, V(-12, 0, 0));          // neck
            ToyKit.Box(t, V(0, 0.042f, 0.007f), V(0.011f, 0.012f, 0.022f), c, 0.45f, V(18, 0, 0));          // head and muzzle
            ToyKit.Box(t, V(0, 0.034f, -0.008f), V(0.004f, 0.026f, 0.006f), d, 0.4f, V(-15, 0, 0));         // mane
            foreach (var s in new[] { -1f, 1f })
            {
                ToyKit.Ball(t, V(s * 0.003f, 0.051f, -0.001f), V(0.003f, 0.008f, 0.004f), c);                  // ears
                ToyKit.Ball(t, V(s * 0.0056f, 0.045f, 0.007f), Vector3.one * 0.0022f, Ink);                    // eyes
            }
            ToyKit.Ball(t, V(0, 0.039f, 0.018f), V(0.006f, 0.002f, 0.003f), Ink);                             // nostril line
        }

        static void WickerBall(Transform t)
        {
            const float r = 0.03f;
            ToyKit.Ball(t, V(0, r, 0), Vector3.one * r * 2f, "#E8A85A");
            for (int i = 0; i < 4; i++) ToyKit.Ring(t, V(0, r, 0), r + 0.0008f, r - 0.002f, 0.003f, "#8A4F24", V(90, i * 45f, 0));
            for (int i = 0; i < 3; i++) { float lat = -0.7f + i * 0.7f, y = Mathf.Sin(lat) * r, rr = Mathf.Cos(lat) * r; ToyKit.Ring(t, V(0, r + y, 0), rr + 0.0008f, rr - 0.002f, 0.003f, "#8A4F24"); }
        }

        static void ToyCar(Transform t)
        {
            const float s = 0.62f; string body = "#C8372D";
            Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * s;
            ToyKit.Box(t, S(0, 0.024f, 0), S(0.07f, 0.024f, 0.125f), body, 0.4f);
            ToyKit.Box(t, S(0, 0.046f, -0.012f), S(0.058f, 0.026f, 0.062f), body, 0.45f);
            ToyKit.Box(t, S(0, 0.047f, 0.02f), S(0.05f, 0.018f, 0.003f), "#9AD8F2", 0.3f, V(-25, 0, 0));
            ToyKit.Box(t, S(0, 0.047f, -0.044f), S(0.05f, 0.017f, 0.003f), "#9AD8F2", 0.3f);
            foreach (var sd in new[] { -1f, 1f })
            {
                ToyKit.Box(t, S(sd * 0.0295f, 0.047f, -0.012f), S(0.003f, 0.016f, 0.05f), "#9AD8F2", 0.3f);
                foreach (var z in new[] { -0.038f, 0.04f })
                {
                    ToyKit.Cyl(t, S(sd * 0.034f, 0.014f, z), 0.014f * s, 0.012f * s, "#2A2B2E", V(0, 0, 90), -1f, 14);
                    ToyKit.Cyl(t, S(sd * 0.0405f, 0.014f, z), 0.0065f * s, 0.002f * s, "#B9C3CF", V(0, 0, 90), -1f, 10);
                }
                ToyKit.Box(t, S(sd * 0.022f, 0.027f, 0.063f), S(0.014f, 0.008f, 0.003f), "#FFE45C", 0.4f);
            }
            ToyKit.Box(t, S(0, 0.015f, 0.064f), S(0.072f, 0.008f, 0.006f), "#B9C3CF", 0.5f);
            ToyKit.Box(t, S(0, 0.0365f, 0.03f), S(0.02f, 0.001f, 0.05f), "#FFFFFF", 0.3f);                   // racing stripe
        }

        static void Cookie(Transform t)
        {
            ToyKit.Cyl(t, V(0, 0.0055f, 0), 0.023f, 0.011f, "#C48A4E", default, 0.021f, 24);
            var rng = new System.Random(7);
            for (int i = 0; i < 7; i++) { float a = (float)rng.NextDouble() * Mathf.PI * 2f, rr = 0.004f + (float)rng.NextDouble() * 0.013f; ToyKit.Ball(t, V(Mathf.Cos(a) * rr, 0.011f, Mathf.Sin(a) * rr), V(0.005f, 0.003f, 0.005f), "#5A3A22"); }
            ToyKit.Ring(t, V(0, 0.0105f, 0), 0.0215f, 0.017f, 0.0008f, "#A8703A");
        }

        static void Toast(Transform t)
        {
            ToyKit.Box(t, V(0, 0.0032f, 0), V(0.07f, 0.0063f, 0.058f), "#C98A4A", 0.3f);
            ToyKit.Box(t, V(0, 0.0034f, 0.0005f), V(0.062f, 0.0063f, 0.05f), "#F3DCA8", 0.3f);
            foreach (var s in new[] { -1f, 1f }) ToyKit.Ball(t, V(s * 0.018f, 0.0032f, 0.027f), V(0.036f, 0.0063f, 0.016f), "#C98A4A");   // the domed top
            ToyKit.Box(t, V(0.008f, 0.0068f, -0.005f), V(0.016f, 0.0008f, 0.012f), "#FFE45C", 0.4f);                                       // a pat of butter
        }

        static void Croissant(Transform t)
        {
            const string c = "#D39552", d = "#B8742E";
            for (int i = 0; i < 5; i++)
            {
                float u = (i - 2) / 2f, a = u * 50f * Mathf.Deg2Rad, sz = 1f - Mathf.Abs(u) * 0.45f;
                var p = V(Mathf.Sin(a) * 0.024f, 0.009f * sz, -Mathf.Cos(a) * 0.012f + 0.012f);
                ToyKit.Ball(t, p, V(0.016f, 0.018f, 0.014f) * sz, i % 2 == 0 ? c : d, V(0, -u * 50f, 0));
            }
        }

        static void Strawberry(Transform t)
        {
            ToyKit.Ball(t, V(0, 0.0125f, 0), V(0.022f, 0.026f, 0.022f), "#E8323F");
            ToyKit.Cyl(t, V(0, 0.0055f, 0), 0.007f, 0.006f, "#E8323F", default, 0.009f, 14);                 // the pointed bottom
            ToyKit.Cyl(t, V(0, 0.0262f, 0), 0.0095f, 0.003f, "#3E9A45", default, 0.002f, 6);
            ToyKit.Cyl(t, V(0, 0.029f, 0), 0.0012f, 0.004f, "#3E9A45", default, -1f, 6);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 2.4f, y = 0.006f + (i % 5) * 0.0035f, rr = 0.0105f * Mathf.Sin(Mathf.PI * (y / 0.026f) * 0.9f + 0.3f);
                ToyKit.Ball(t, V(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr), Vector3.one * 0.0016f, "#FFE45C");
            }
        }

        static void Grape(Transform t)
        {
            ToyKit.Ball(t, V(0, 0.0098f, 0), V(0.017f, 0.0196f, 0.017f), "#7B4FC8");
            ToyKit.Cyl(t, V(0, 0.0205f, 0), 0.0012f, 0.004f, "#3E9A45", V(10, 0, 0), -1f, 6);
            ToyKit.Ball(t, V(-0.003f, 0.014f, 0.006f), V(0.004f, 0.005f, 0.002f), "#C9B3FF");                 // shine
        }

        static void Cracker(Transform t)
        {
            const float S = 0.04f, T = 0.005f;
            ToyKit.Box(t, V(0, T / 2f, 0), V(S, T, S), "#E9B86A", 0.35f);
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) ToyKit.Cyl(t, V((i - 1) * 0.011f, T + 0.0002f, (j - 1) * 0.011f), 0.0013f, 0.0006f, "#A56E2E", default, -1f, 8);
            ToyKit.Ring(t, V(0, T + 0.0001f, 0), 0.0185f, 0.0175f, 0.0003f, "#D29A4E");
        }

        static void Figure(Transform t)
        {
            ToyKit.Cyl(t, V(0, 0.011f, 0), 0.009f, 0.022f, "#3A86FF", default, 0.008f, 16);
            ToyKit.Box(t, V(0, 0.0035f, 0.002f), V(0.016f, 0.004f, 0.012f), "#1B1430", 0.4f);                 // shoes
            ToyKit.Ball(t, V(0, 0.03f, 0), Vector3.one * 0.015f, "#FFD23F");
            ToyKit.Ball(t, V(0, 0.0355f, -0.001f), V(0.0155f, 0.007f, 0.0155f), "#6B3E26");                    // hair
            foreach (var s in new[] { -1f, 1f })
            {
                ToyKit.Ball(t, V(s * 0.0028f, 0.0305f, 0.0068f), Vector3.one * 0.0018f, Ink);
                ToyKit.Ball(t, V(s * 0.0098f, 0.016f, 0), V(0.004f, 0.012f, 0.004f), "#3A86FF", V(0, 0, s * 15f));   // arms
                ToyKit.Ball(t, V(s * 0.011f, 0.0095f, 0), Vector3.one * 0.0045f, "#FFD23F");                         // hands
            }
            ToyKit.Box(t, V(0, 0.0275f, 0.0072f), V(0.004f, 0.0008f, 0.001f), Ink, 0.3f);                   // smile
        }

        static void ButtonToy(Transform t)
        {
            const float r = 0.011f, h = 0.004f;
            ToyKit.Cyl(t, V(0, h / 2f, 0), r, h, "#FF5D73", default, -1f, 22);
            ToyKit.Ring(t, V(0, h + 0.0002f, 0), r, r * 0.78f, 0.0008f, "#FF8A9A");
            foreach (var a in new[] { -1f, 1f }) foreach (var b in new[] { -1f, 1f }) ToyKit.Cyl(t, V(a * 0.0028f, h + 0.0002f, b * 0.0028f), 0.0012f, 0.0006f, "#1B1430", default, -1f, 8);
        }

        static void Leaf(Transform t)
        {
            ToyKit.Ball(t, V(0, 0.002f, 0), V(0.024f, 0.003f, 0.06f), "#5BB54A");
            ToyKit.Box(t, V(0, 0.0035f, 0), V(0.0016f, 0.0008f, 0.052f), "#2F7A34", 0.3f);
            foreach (var s in new[] { -1f, 1f }) for (int i = 0; i < 3; i++) ToyKit.Box(t, V(s * 0.004f, 0.0034f, -0.012f + i * 0.012f), V(0.008f, 0.0006f, 0.001f), "#2F7A34", 0.3f, V(0, s * 35f, 0));
            ToyKit.Cyl(t, V(0, 0.002f, -0.033f), 0.0009f, 0.008f, "#2F7A34", V(90, 0, 0), -1f, 6);
        }

        static void Pebble(Transform t)
        {
            ToyKit.Ball(t, V(0, 0.0077f, 0), V(0.0275f, 0.0154f, 0.022f), "#A8A2B8");
            ToyKit.Ball(t, V(-0.004f, 0.0135f, -0.002f), V(0.01f, 0.003f, 0.005f), "#D5D0E0", V(0, 20, 0));
            ToyKit.Ball(t, V(0.006f, 0.008f, 0.009f), V(0.004f, 0.003f, 0.003f), "#8B85A0");
        }
    }
}
