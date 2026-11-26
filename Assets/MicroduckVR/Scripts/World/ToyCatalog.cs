using System;
using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// Every bag toy (port of props.js): its label, whether a duck can carry it in its beak (and how wide the beak
    /// must open) or only kick it, its mass, and how to build its comic model. Models rest on y = 0, front along +z.
    /// </summary>
    public static partial class ToyCatalog
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

        const string Ink = ComicColors.Ink;
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
            Add(new Entry { kind = "toy_figure", label = "Toy figure", icon = ComicColors.Blue, grip = 0.018f, mass = 0.006f, build = Figure });
            Add(new Entry { kind = "button", label = "Button", icon = ComicColors.Pink, grip = 0.004f, mass = 0.001f, build = ButtonToy });
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
    }
}
