using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Prop kit: small detailed props built from comic parts (rounded boxes, round legs, rings, balls) under one root.
    /// A prop is either static scenery (World layer, batched) or a dynamic toy (Prop layer, Rigidbody with one simple
    /// collider) that ducks push around by walking into it. All coordinates are Unity space; parts are local to the root.
    /// </summary>
    public static partial class HouseBuilder
    {
        static Mesh sphereMesh;
        static PhysicsMaterial bouncy, grippy;

        static Mesh SphereMesh()
        {
            if (sphereMesh != null) return sphereMesh;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphereMesh = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g);
            return sphereMesh;
        }

        static PhysicsMaterial PhysMat(string name, float bounce, float friction)
        {
            string p = $"{MatDir}/{name}.physicMaterial";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(p);
            if (m == null) { m = new PhysicsMaterial(name); AssetDatabase.CreateAsset(m, p); }
            m.bounciness = bounce; m.dynamicFriction = friction; m.staticFriction = friction;
            m.bounceCombine = PhysicsMaterialCombine.Maximum; m.frictionCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(m);
            return m;
        }

        static float Q(float v) => Mathf.Max(0.001f, Mathf.Round(v * 1000f) / 1000f);   // 1 mm grid so meshes are shared

        // ------------------------------------------------------------------ roots and parts
        static Transform NewProp(string room, string name, Vector3 pos, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rooms[room], false);
            go.transform.SetPositionAndRotation(pos, rot);
            return go.transform;
        }
        static Transform NewProp(string room, string name, Vector3 pos, float yawDeg = 0f) => NewProp(room, name, pos, Quaternion.Euler(0f, yawDeg, 0f));

        static Transform Part(Transform parent, string name, Mesh mesh, string hex, Vector3 lp, Vector3 le, Vector3 ls)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp; go.transform.localRotation = Quaternion.Euler(le); go.transform.localScale = ls;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = C(hex);
            return go.transform;
        }

        /// <summary>Rounded box part. round = corner radius as a fraction of the smallest half-size.</summary>
        static Transform PBox(Transform p, Vector3 c, Vector3 size, string hex, float round = 0.3f, Vector3 e = default, float puff = 0f)
        {
            var sz = new Vector3(Q(size.x), Q(size.y), Q(size.z));
            float m = Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) * 0.5f;
            float big = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z));
            var st = new ComicMesh.Style { radius = Q(m * round), segments = m > 0.01f ? 3 : 2, puff = puff, wobble = big > 0.15f ? Mathf.Min(0.004f, big * 0.01f) : 0f, seed = 1 };
            return Part(p, "box", SaveMesh(ComicMesh.RoundedBox(sz, st)), hex, c, e, Vector3.one);
        }

        /// <summary>Round part along local y: radius at the bottom r, at the top rTop (defaults to r). sides = 5 makes a pentagon.</summary>
        static Transform PCyl(Transform p, Vector3 c, float r, float h, string hex, Vector3 e = default, float rTop = -1f, int sides = 16)
            => Part(p, "cyl", SaveMesh(ComicMesh.Leg(Q(h), Q(rTop < 0f ? r : rTop), Q(r), sides)), hex, c, e, Vector3.one);

        static Transform PBall(Transform p, Vector3 c, Vector3 size, string hex, Vector3 e = default)
            => Part(p, "ball", SphereMesh(), hex, c, e, size);

        static Transform PRing(Transform p, Vector3 c, float ro, float ri, float h, string hex, Vector3 e = default)
            => Part(p, "ring", SaveMesh(ComicMesh.Ring(Q(ro), Q(ri), Q(h))), hex, c, e, Vector3.one);

        /// <summary>Local-space bounds of all renderers under root.</summary>
        static Bounds LocalBounds(Transform root)
        {
            bool any = false; var b = new Bounds();
            var inv = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds; var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var q = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
                }
            }
            return b;
        }

        static void SetLayerAll(Transform t, int layer, bool isStatic)
        {
            t.gameObject.layer = layer; t.gameObject.isStatic = isStatic;
            foreach (Transform c in t) SetLayerAll(c, layer, isStatic);
        }

        /// <summary>Dynamic toy: Prop layer, one box (or sphere) collider around its parts, mass from its size.</summary>
        static Transform Dyn(Transform root, float density = 250f, bool sphere = false, PhysicsMaterial pm = null, float minMass = 0.015f)
        {
            SetLayerAll(root, DuckSetup.LayerProp, false);
            var b = LocalBounds(root);
            Collider col;
            if (sphere) { var sc = root.gameObject.AddComponent<SphereCollider>(); sc.center = b.center; sc.radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)); col = sc; }
            else { var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; col = bc; }
            col.sharedMaterial = pm ?? grippy;
            var rb = root.gameObject.AddComponent<Rigidbody>();
            float vol = b.size.x * b.size.y * b.size.z * (sphere ? 0.52f : 0.7f);
            rb.mass = Mathf.Clamp(vol * density, minMass, 4f);
            rb.linearDamping = 0.15f; rb.angularDamping = sphere ? 0.25f : 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // anything a duck could get its beak round can be picked up and carried
            float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)), small = Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z));
            if (big <= 0.14f && small <= 0.07f && rb.mass <= 0.25f)
            {
                var g = root.gameObject.AddComponent<MicroduckVR.World.Grabbable>();
                g.label = root.name.ToLowerInvariant(); g.gripWidth = Mathf.Clamp(small, 0.008f, 0.03f);
            }
            return root;
        }

        /// <summary>Static scenery: World layer, batched; optional box collider with a surface tag.</summary>
        static Transform Stat(Transform root, string room, bool collide = false, string surface = null)
        {
            SetLayerAll(root, DuckSetup.LayerWorld, true);
            if (collide)
            {
                var b = LocalBounds(root);
                var bc = root.gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
                if (surface != null) Tag(root.gameObject, surface, room);
            }
            return root;
        }

        // ------------------------------------------------------------------ colour helpers
        static string Shade(string hex, float f)
        {
            var c = ComicLook.Hex(hex);
            c = f < 1f ? Color.Lerp(Color.black, c, f) : Color.Lerp(c, Color.white, f - 1f);
            return "#" + ColorUtility.ToHtmlStringRGB(c);
        }
        const string Ink = "#2A2B2E", Cream = "#FFF6E4", Paper = "#FFFDF5", Gold = "#FFD23F";

        // ================================================================== props
        /// <summary>Hardback book: covers, spine with bands and a title label, cream page block. Spine faces local -z.
        /// pos = bottom centre when standing; lying books rest on their side.</summary>
        static Transform Book(string room, Vector3 pos, float yaw, float t, float h, float d, string hex, bool lying, bool dynamic)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f) * (lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            var r = NewProp(room, "Book", pos + Vector3.up * (lying ? t / 2f : h / 2f), rot);
            float c = Mathf.Min(0.004f, t * 0.2f);
            PBox(r, new Vector3(-t / 2f + c / 2f, 0f, 0f), new Vector3(c, h, d), hex, 0.4f);
            PBox(r, new Vector3(t / 2f - c / 2f, 0f, 0f), new Vector3(c, h, d), hex, 0.4f);
            PBox(r, new Vector3(0f, 0f, -d / 2f + c / 2f), new Vector3(t, h, c), hex, 0.4f);
            PBox(r, new Vector3(0f, 0f, c * 0.5f), new Vector3(t - c * 1.6f, h - 0.008f, d - c * 1.5f), Paper, 0.15f);
            string band = crng.Next(2) == 0 ? Gold : Shade(hex, 0.6f);
            foreach (var s in new[] { -1f, 1f }) PBox(r, new Vector3(0f, s * h * 0.36f, -d / 2f - 0.0006f), new Vector3(t * 0.86f, 0.007f, 0.002f), band, 0.2f);
            PBox(r, new Vector3(0f, h * 0.06f, -d / 2f - 0.0006f), new Vector3(t * 0.62f, h * 0.26f, 0.002f), crng.Next(2) == 0 ? Cream : Shade(hex, 0.55f), 0.3f);
            if (h > 0.1f) PBox(r, new Vector3(0f, h * 0.06f, -d / 2f - 0.0012f), new Vector3(t * 0.3f, h * 0.16f, 0.002f), Ink, 0.2f);   // the "title"
            return dynamic ? Dyn(r, 400f) : Stat(r, room);
        }

        static void BookRow(string room, Vector3 start, Vector3 along, float yaw, int n, bool dynamic, float maxH = 0.25f)
        {
            float o = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = Rf(0.028f, 0.05f), h = Rf(maxH * 0.68f, maxH), d = Rf(0.13f, 0.17f);
                Book(room, start + along * (o + t / 2f), yaw + Rf(-3f, 3f), t, h, d, AnyColour(), false, dynamic);
                o += t + 0.003f;
            }
        }

        static void BookStack(string room, float x, float y, float z, int n, bool dynamic)
        {
            float yy = y;
            for (int i = 0; i < n; i++)
            {
                float t = Rf(0.025f, 0.045f);
                Book(room, new Vector3(x + Rf(-0.015f, 0.015f), yy, z + Rf(-0.015f, 0.015f)), Rf(-30f, 30f), t, Rf(0.17f, 0.23f), Rf(0.12f, 0.16f), AnyColour(), true, dynamic);
                yy += t + 0.001f;
            }
        }

        /// <summary>Sitting cartoon teddy: big head, button eyes with shines, muzzle and nose, inner ears, belly patch,
        /// stubby arms, legs out front with paw pads, and a bow. s = 1 is about 30 cm tall.</summary>
        static Transform Teddy(string room, Vector3 pos, float yaw, float s, string fur = "#C98A55", string bow = "#FF5D73", bool dynamic = true)
        {
            var r = NewProp(room, "Teddy", pos, yaw);
            string light = "#FFE2BF";
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBall(r, V(0f, 0.11f, 0f), V(0.2f, 0.21f, 0.17f), fur);                        // body
            PBall(r, V(0f, 0.105f, 0.055f), V(0.12f, 0.13f, 0.07f), light);                 // belly
            PBall(r, V(0f, 0.285f, 0.005f), V(0.18f, 0.16f, 0.155f), fur);                  // head
            PBall(r, V(0f, 0.262f, 0.07f), V(0.085f, 0.062f, 0.06f), light);                // muzzle
            PBall(r, V(0f, 0.28f, 0.098f), V(0.036f, 0.024f, 0.022f), Ink);                 // nose
            PBox(r, V(0f, 0.251f, 0.1f), V(0.004f, 0.016f, 0.004f), Ink, 0.4f);            // philtrum
            foreach (var sd in new[] { -1f, 1f })
            {
                PBox(r, V(sd * 0.011f, 0.244f, 0.097f), V(0.018f, 0.004f, 0.004f), Ink, 0.4f, new Vector3(0f, 0f, sd * 25f));   // smile
                PBall(r, V(sd * 0.04f, 0.307f, 0.07f), V(0.026f, 0.03f, 0.018f), Ink);      // eye
                PBall(r, V(sd * 0.04f + 0.006f, 0.315f, 0.078f), V(0.009f, 0.009f, 0.006f), "#FFFFFF");   // shine
                PBall(r, V(sd * 0.072f, 0.36f, -0.005f), V(0.062f, 0.062f, 0.034f), fur);   // ear
                PBall(r, V(sd * 0.072f, 0.358f, 0.008f), V(0.036f, 0.036f, 0.016f), light);
                PBall(r, V(sd * 0.105f, 0.15f, 0.035f), V(0.058f, 0.115f, 0.058f), fur, new Vector3(-20f, 0f, sd * 35f));   // arm
                PBall(r, V(sd * 0.058f, 0.04f, 0.08f), V(0.072f, 0.07f, 0.13f), fur);       // leg
                PBall(r, V(sd * 0.058f, 0.042f, 0.142f), V(0.05f, 0.05f, 0.012f), light);   // paw pad
                PBox(r, V(sd * 0.022f, 0.208f, 0.075f), V(0.04f, 0.026f, 0.016f), bow, 0.45f, new Vector3(0f, 0f, sd * 18f));   // bow
            }
            PBall(r, V(0f, 0.208f, 0.083f), V(0.018f, 0.02f, 0.016f), Shade(bow, 0.8f));
            return dynamic ? Dyn(r, 60f) : Stat(r, room);
        }

        /// <summary>Plush bunny: long ears with pink insides, round cheeks, button eyes, cotton tail.</summary>
        static Transform Bunny(string room, Vector3 pos, float yaw, float s, string fur = "#FFFFFF")
        {
            var r = NewProp(room, "Plush bunny", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBall(r, V(0f, 0.08f, 0f), V(0.14f, 0.15f, 0.13f), fur);
            PBall(r, V(0f, 0.19f, 0.01f), V(0.13f, 0.12f, 0.11f), fur);
            PBall(r, V(0f, 0.05f, -0.07f), V(0.05f, 0.05f, 0.05f), fur);
            PBall(r, V(0f, 0.18f, 0.065f), V(0.022f, 0.016f, 0.012f), "#FF8A9A");
            foreach (var sd in new[] { -1f, 1f })
            {
                PBall(r, V(sd * 0.03f, 0.31f, 0f), V(0.042f, 0.15f, 0.026f), fur, new Vector3(0f, 0f, -sd * 12f));
                PBall(r, V(sd * 0.031f, 0.31f, 0.01f), V(0.022f, 0.11f, 0.01f), "#FF8A9A", new Vector3(0f, 0f, -sd * 12f));
                PBall(r, V(sd * 0.032f, 0.205f, 0.05f), V(0.018f, 0.022f, 0.012f), Ink);
                PBall(r, V(sd * 0.045f, 0.175f, 0.045f), V(0.024f, 0.014f, 0.01f), "#FFC2CC");
                PBall(r, V(sd * 0.045f, 0.03f, 0.06f), V(0.05f, 0.04f, 0.07f), fur);
            }
            return Dyn(r, 60f);
        }

        /// <summary>Football: white ball with 12 black pentagon patches (icosahedron directions). Bouncy and kickable.</summary>
        static Transform Football(string room, Vector3 pos, float radius)
        {
            var r = NewProp(room, "Football", pos + Vector3.up * radius, Quaternion.Euler(Rf(0f, 360f), Rf(0f, 360f), 0f));
            PBall(r, Vector3.zero, Vector3.one * radius * 2f, "#FFFFFF");
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            var dirs = new List<Vector3>();
            foreach (var a in new[] { -1f, 1f }) foreach (var b in new[] { -phi, phi })
                { dirs.Add(new Vector3(0f, a, b)); dirs.Add(new Vector3(a, b, 0f)); dirs.Add(new Vector3(b, 0f, a)); }
            foreach (var d in dirs)
            {
                var n = d.normalized;
                PCyl(r, n * (radius - 0.0008f), radius * 0.36f, 0.004f, Ink, Quaternion.FromToRotation(Vector3.up, n).eulerAngles, -1f, 5);
            }
            return Dyn(r, 40f, true, bouncy, 0.04f);
        }

        /// <summary>Bouncy ball with a contrasting stripe around it.</summary>
        static Transform StripeBall(string room, Vector3 pos, float d, string hex, string stripe)
        {
            var r = NewProp(room, "Ball", pos + Vector3.up * d / 2f, Quaternion.Euler(Rf(-40f, 40f), Rf(0f, 360f), Rf(-40f, 40f)));
            PBall(r, Vector3.zero, Vector3.one * d, hex);
            PRing(r, Vector3.zero, d * 0.505f, d * 0.4f, d * 0.22f, stripe);
            return Dyn(r, 80f, true, bouncy);
        }

        /// <summary>2x4 brick with studs.</summary>
        static Transform Lego(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Lego brick", pos, yaw);
            PBox(r, new Vector3(0f, 0.0095f, 0f), new Vector3(0.032f, 0.019f, 0.064f), hex, 0.15f);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 4; j++)
                PCyl(r, new Vector3(-0.008f + i * 0.016f, 0.0205f, -0.024f + j * 0.016f), 0.0048f, 0.0034f, Shade(hex, 1.08f), default, -1f, 12);
            return Dyn(r, 300f);
        }

        /// <summary>Alphabet block: coloured cube with raised panels in a contrasting colour, a simple letter shape on each.</summary>
        static Transform Block(string room, Vector3 pos, float yaw, float s, string hex)
        {
            var r = NewProp(room, "Toy block", pos + Vector3.up * s / 2f, yaw);
            PBox(r, Vector3.zero, Vector3.one * s, hex, 0.18f);
            string panel = hex == Cream ? "#FF5D73" : Cream, letter = AnyColour();
            if (letter == panel) letter = Ink;
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
            {
                var rot = Quaternion.LookRotation(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up).eulerAngles;
                var pnl = PBox(r, n * (s / 2f), new Vector3(s * 0.72f, s * 0.72f, 0.003f), panel, 0.2f, rot);
                int kind = crng.Next(3);
                if (kind == 0) { PBox(pnl, new Vector3(0f, 0f, 0.002f), new Vector3(s * 0.1f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(0f, 0f, 0.002f), new Vector3(s * 0.4f, s * 0.1f, 0.002f), letter, 0.3f); }
                else if (kind == 1) PRing(pnl, new Vector3(0f, 0f, 0.002f), s * 0.2f, s * 0.11f, 0.002f, letter, new Vector3(90f, 0f, 0f));
                else { PBox(pnl, new Vector3(-s * 0.12f, 0f, 0.002f), new Vector3(s * 0.09f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(s * 0.12f, 0f, 0.002f), new Vector3(s * 0.09f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(0f, s * 0.19f, 0.002f), new Vector3(s * 0.33f, s * 0.08f, 0.002f), letter, 0.3f); }
            }
            return Dyn(r, 250f);
        }

        /// <summary>Crayon lying down: wax body, paper wrapper with two colour stripes, pointed tip.</summary>
        static Transform Crayon(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Crayon", pos + Vector3.up * 0.0065f, yaw);
            var e = new Vector3(90f, 0f, 0f);
            PCyl(r, new Vector3(0f, 0f, -0.006f), 0.006f, 0.07f, hex, e, -1f, 10);
            PCyl(r, new Vector3(0f, 0f, -0.01f), 0.0066f, 0.044f, "#F3EBD7", e, -1f, 10);
            foreach (var z in new[] { -0.027f, 0.007f }) PCyl(r, new Vector3(0f, 0f, z), 0.0068f, 0.004f, hex, e, -1f, 10);
            PCyl(r, new Vector3(0f, 0f, 0.0365f), 0.006f, 0.015f, hex, e, 0.0012f, 10);
            return Dyn(r, 600f);
        }

        /// <summary>Toy car: body, cabin with windows, four wheels with hubcaps, headlights and a bumper.</summary>
        static Transform ToyCar(string room, Vector3 pos, float yaw, string hex, float s = 1f)
        {
            var r = NewProp(room, "Toy car", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBox(r, V(0f, 0.024f, 0f), V(0.07f, 0.024f, 0.125f), hex, 0.4f);
            PBox(r, V(0f, 0.046f, -0.012f), V(0.058f, 0.026f, 0.062f), hex, 0.45f);
            PBox(r, V(0f, 0.047f, 0.02f), V(0.05f, 0.018f, 0.003f), "#9AD8F2", 0.3f, new Vector3(-25f, 0f, 0f));
            PBox(r, V(0f, 0.047f, -0.044f), V(0.05f, 0.017f, 0.003f), "#9AD8F2", 0.3f);
            foreach (var sd in new[] { -1f, 1f })
            {
                PBox(r, V(sd * 0.0295f, 0.047f, -0.012f), V(0.003f, 0.016f, 0.05f), "#9AD8F2", 0.3f);
                foreach (var z in new[] { -0.038f, 0.04f })
                {
                    PCyl(r, V(sd * 0.034f, 0.014f, z), 0.014f * s, 0.012f * s, Ink, new Vector3(0f, 0f, 90f), -1f, 14);
                    PCyl(r, V(sd * 0.0405f, 0.014f, z), 0.0065f * s, 0.002f * s, "#B9C3CF", new Vector3(0f, 0f, 90f), -1f, 10);
                }
                PBox(r, V(sd * 0.022f, 0.027f, 0.063f), V(0.014f, 0.008f, 0.003f), "#FFE45C", 0.4f);
                PBox(r, V(sd * 0.024f, 0.027f, -0.063f), V(0.012f, 0.007f, 0.003f), "#FF5D73", 0.4f);
            }
            PBox(r, V(0f, 0.015f, 0.064f), V(0.072f, 0.008f, 0.006f), "#B9C3CF", 0.5f);
            return Dyn(r, 200f);
        }

        /// <summary>Trainer: sole, upper, toe cap, laces, heel tab.</summary>
        static Transform Shoe(string room, Vector3 pos, float yaw, string hex, float s = 1f)
        {
            var r = NewProp(room, "Shoe", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBox(r, V(0f, 0.012f, 0f), V(0.1f, 0.024f, 0.27f), "#FFFFFF", 0.5f);
            PBox(r, V(0f, 0.05f, -0.03f), V(0.092f, 0.06f, 0.19f), hex, 0.55f, default, 0.08f);
            PBall(r, V(0f, 0.038f, 0.075f), V(0.092f, 0.055f, 0.11f), hex);
            PBall(r, V(0f, 0.03f, 0.105f), V(0.084f, 0.035f, 0.05f), "#FFFFFF");
            PBox(r, V(0f, 0.078f, 0.01f), V(0.05f, 0.012f, 0.09f), Shade(hex, 0.8f), 0.5f, new Vector3(-12f, 0f, 0f));
            for (int i = 0; i < 3; i++) PBox(r, V(0f, 0.084f + i * 0.003f, 0.035f - i * 0.025f), V(0.05f, 0.004f, 0.007f), "#FFFFFF", 0.5f, new Vector3(-12f, 0f, 0f));
            PBox(r, V(0f, 0.075f, -0.123f), V(0.03f, 0.03f, 0.012f), Shade(hex, 0.7f), 0.5f);
            return Dyn(r, 150f);
        }

        static void ShoePair(string room, float x, float z, string hex, float yaw, float s = 1f)
        {
            Shoe(room, new Vector3(x, 0f, z), yaw, hex, s);
            Shoe(room, new Vector3(x + Mathf.Cos(yaw * Mathf.Deg2Rad) * 0.12f * s, 0f, z - Mathf.Sin(yaw * Mathf.Deg2Rad) * 0.12f * s + 0.03f), yaw + Rf(5f, 25f), hex, s);
        }

        /// <summary>Sock: tube with a cuff, a heel and a toe in a contrasting colour, stripes.</summary>
        static Transform Sock(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Sock", pos, yaw);
            PBox(r, new Vector3(0f, 0.012f, 0f), new Vector3(0.06f, 0.024f, 0.2f), hex, 0.45f);
            PBall(r, new Vector3(0f, 0.012f, 0.095f), new Vector3(0.062f, 0.026f, 0.04f), "#FFFFFF");
            PBall(r, new Vector3(0f, 0.012f, -0.04f), new Vector3(0.062f, 0.026f, 0.04f), "#FFFFFF");
            foreach (var z in new[] { -0.075f, -0.09f }) PBox(r, new Vector3(0f, 0.012f, z), new Vector3(0.063f, 0.027f, 0.006f), Shade(hex, 0.6f), 0.4f);
            return Dyn(r, 60f);
        }

        /// <summary>Mug: tapered cup, ring handle, coffee inside, a stripe.</summary>
        static Transform Mug(string room, Vector3 pos, float yaw, string hex, bool dynamic = true)
        {
            var r = NewProp(room, "Mug", pos, yaw);
            PCyl(r, new Vector3(0f, 0.045f, 0f), 0.038f, 0.09f, hex, default, 0.042f, 18);
            PCyl(r, new Vector3(0f, 0.087f, 0f), 0.037f, 0.004f, "#6B3E26", default, -1f, 18);
            PRing(r, new Vector3(0f, 0.09f, 0f), 0.042f, 0.036f, 0.004f, Shade(hex, 1.1f));
            PRing(r, new Vector3(0f, 0.05f, 0f), 0.0415f, 0.036f, 0.012f, "#FFFFFF");
            PRing(r, new Vector3(0.048f, 0.046f, 0f), 0.026f, 0.017f, 0.012f, hex, new Vector3(90f, 0f, 0f));
            return dynamic ? Dyn(r, 300f) : Stat(r, room);
        }

        /// <summary>Open laptop: base with keyboard and trackpad, tilted lid with a bright screen and a sticker.</summary>
        static Transform Laptop(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Laptop", pos, yaw);
            PBox(r, new Vector3(0f, 0.009f, 0f), new Vector3(0.34f, 0.018f, 0.24f), "#B9C3CF", 0.3f);
            PBox(r, new Vector3(0f, 0.0185f, -0.025f), new Vector3(0.3f, 0.002f, 0.11f), Ink, 0.3f);
            for (int i = 0; i < 4; i++) PBox(r, new Vector3(0f, 0.0195f, -0.07f + i * 0.026f), new Vector3(0.28f, 0.0015f, 0.003f), "#3D3B4F", 0.3f);
            PBox(r, new Vector3(0f, 0.0185f, 0.075f), new Vector3(0.1f, 0.002f, 0.06f), "#97A0AE", 0.3f);
            var lid = PBox(r, new Vector3(0f, 0.018f + Mathf.Cos(15f * Mathf.Deg2Rad) * 0.115f, -0.12f - Mathf.Sin(15f * Mathf.Deg2Rad) * 0.115f), new Vector3(0.34f, 0.23f, 0.012f), "#B9C3CF", 0.3f, new Vector3(-15f, 0f, 0f));
            PBox(lid, new Vector3(0f, 0.006f, 0.0065f), new Vector3(0.3f, 0.18f, 0.002f), "#3A86FF", 0.2f);
            PBall(lid, new Vector3(0.06f, 0.03f, 0.0075f), new Vector3(0.07f, 0.07f, 0.002f), "#FFE45C");      // sun on the wallpaper
            PBox(lid, new Vector3(0f, -0.05f, 0.0075f), new Vector3(0.3f, 0.06f, 0.002f), "#8FD35A", 0.2f);    // hills
            PBall(lid, new Vector3(0.05f, 0.03f, -0.007f), new Vector3(0.05f, 0.05f, 0.002f), "#FF5D73");     // sticker
            return Dyn(r, 600f);
        }

        /// <summary>Game pad: body, two grips, d-pad, four coloured buttons, two sticks.</summary>
        static Transform GamePad(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Game pad", pos, yaw);
            PBox(r, new Vector3(0f, 0.013f, 0f), new Vector3(0.12f, 0.024f, 0.06f), "#3D3B4F", 0.5f);
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.05f, 0.012f, -0.022f), new Vector3(0.042f, 0.024f, 0.055f), "#3D3B4F");
            PBox(r, new Vector3(-0.035f, 0.026f, 0.008f), new Vector3(0.022f, 0.004f, 0.007f), Ink, 0.3f);
            PBox(r, new Vector3(-0.035f, 0.026f, 0.008f), new Vector3(0.007f, 0.004f, 0.022f), Ink, 0.3f);
            string[] bc = { "#FF5D73", "#3A86FF", "#8FD35A", "#FFC43D" };
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2f; PCyl(r, new Vector3(0.035f + Mathf.Cos(a) * 0.009f, 0.026f, 0.008f + Mathf.Sin(a) * 0.009f), 0.0042f, 0.004f, bc[i], default, -1f, 10); }
            foreach (var sd in new[] { -1f, 1f }) PCyl(r, new Vector3(sd * 0.016f, 0.027f, -0.012f), 0.008f, 0.006f, Ink, default, 0.007f, 12);
            return Dyn(r, 300f);
        }

        static Transform Remote(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Remote", pos, yaw);
            PBox(r, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.18f), "#3D3B4F", 0.5f);
            PCyl(r, new Vector3(0f, 0.021f, 0.065f), 0.007f, 0.004f, "#FF5D73", default, -1f, 10);
            for (int i = 0; i < 3; i++) for (int j = 0; j < 4; j++) PBox(r, new Vector3(-0.012f + i * 0.012f, 0.021f, 0.035f - j * 0.016f), new Vector3(0.008f, 0.003f, 0.008f), "#B9C3CF", 0.4f);
            return Dyn(r, 400f);
        }

        static Transform Magazine(string room, Vector3 pos, float yaw, string hex, bool dynamic = true)
        {
            var r = NewProp(room, "Magazine", pos, yaw);
            PBox(r, new Vector3(0f, 0.004f, 0f), new Vector3(0.21f, 0.008f, 0.28f), hex, 0.2f);
            PBox(r, new Vector3(0f, 0.0085f, 0.1f), new Vector3(0.17f, 0.002f, 0.04f), "#FFFFFF", 0.3f);
            PBox(r, new Vector3(0.02f, 0.0085f, -0.03f), new Vector3(0.12f, 0.002f, 0.13f), Shade(hex, 1.5f), 0.3f);
            PBall(r, new Vector3(0.02f, 0.009f, -0.03f), new Vector3(0.06f, 0.002f, 0.06f), AnyColour());
            return dynamic ? Dyn(r, 600f) : Stat(r, room);
        }

        /// <summary>Cardboard box with flaps seam and packing tape over the top and down the sides.</summary>
        static Transform Carton(string room, Vector3 pos, float yaw, Vector3 size, bool collide = true)
        {
            var r = NewProp(room, "Cardboard box", pos + Vector3.up * size.y / 2f, yaw);
            PBox(r, Vector3.zero, size, "#D9A066", 0.06f);
            PBox(r, new Vector3(0f, size.y / 2f, 0f), new Vector3(size.x + 0.001f, 0.003f, 0.004f), "#A8733F", 0.2f);
            PBox(r, new Vector3(0f, size.y / 2f + 0.001f, 0f), new Vector3(0.06f, 0.002f, size.z + 0.002f), "#F2D49B", 0.2f);
            foreach (var s in new[] { -1f, 1f }) PBox(r, new Vector3(0f, size.y / 2f - 0.04f, s * (size.z / 2f + 0.001f)), new Vector3(0.06f, 0.08f, 0.002f), "#F2D49B", 0.2f);
            PBox(r, new Vector3(size.x * 0.22f, size.y * 0.15f, size.z / 2f + 0.001f), new Vector3(0.05f, 0.04f, 0.002f), Ink, 0.2f);   // "this way up" mark
            return Stat(r, room, collide, "card");
        }

        static void CartonStack(string room, float x, float z, int n, float yaw = 0f)
        {
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = Rf(0.3f, 0.42f) * (1f - i * 0.14f), h = s * Rf(0.65f, 0.85f);
                Carton(room, new Vector3(x + Rf(-0.03f, 0.03f), y, z + Rf(-0.03f, 0.03f)), yaw + Rf(-10f, 10f), new Vector3(s, h, s * Rf(0.8f, 1f)));
                y += h;
            }
        }

        /// <summary>Potted plant: pot with rim, soil, and leaves fanned out from the middle.</summary>
        static Transform Plant(string room, Vector3 pos, float s, string pot = "#E07A4F", bool collide = true)
        {
            var r = NewProp(room, "Plant", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, 0.11f * s, 0f), 0.095f * s, 0.22f * s, pot, default, 0.13f * s, 18);
            PRing(r, new Vector3(0f, 0.22f * s, 0f), 0.14f * s, 0.11f * s, 0.035f * s, Shade(pot, 1.12f));
            PCyl(r, new Vector3(0f, 0.225f * s, 0f), 0.115f * s, 0.01f * s, "#6B3E26", default, -1f, 18);
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * 360f + Rf(-10f, 10f), tilt = Rf(18f, 42f), len = Rf(0.22f, 0.34f) * s;
                var dir = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                var c = new Vector3(0f, 0.23f * s, 0f) + dir * Vector3.up * len * 0.5f;
                PBall(r, c, new Vector3(0.075f * s, len, 0.02f * s), i % 2 == 0 ? "#3E9A45" : "#56B65A", dir.eulerAngles);
            }
            return Stat(r, room, collide, "table");
        }

        static Transform FloorLamp(string room, Vector3 pos)
        {
            var r = NewProp(room, "Floor lamp", pos);
            PCyl(r, new Vector3(0f, 0.015f, 0f), 0.14f, 0.03f, "#3D3B4F", default, 0.12f, 20);
            PCyl(r, new Vector3(0f, 0.72f, 0f), 0.012f, 1.38f, "#3D3B4F", default, -1f, 10);
            PCyl(r, new Vector3(0f, 1.4f, 0f), 0.2f, 0.26f, "#FFE45C", default, 0.12f, 22);
            PRing(r, new Vector3(0f, 1.27f, 0f), 0.205f, 0.19f, 0.016f, "#FF8A3D");
            PRing(r, new Vector3(0f, 1.53f, 0f), 0.125f, 0.11f, 0.014f, "#FF8A3D");
            PBall(r, new Vector3(0f, 1.32f, 0f), new Vector3(0.07f, 0.08f, 0.07f), "#FFFDE0");
            var t = Stat(r, room);
            var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(0.2f, 1.4f, 0.2f);
            return t;
        }

        static Transform Orange(string room, Vector3 pos, bool apple)
        {
            var r = NewProp(room, apple ? "Apple" : "Orange", pos, Rf(0f, 360f));
            string c = apple ? "#FF5D73" : "#FF8A3D";
            PBall(r, new Vector3(0f, 0.034f, 0f), new Vector3(0.072f, apple ? 0.066f : 0.07f, 0.072f), c);
            PCyl(r, new Vector3(0f, 0.072f, 0f), 0.003f, 0.016f, "#6B3E26", new Vector3(10f, 0f, 0f), -1f, 6);
            PBall(r, new Vector3(0.012f, 0.072f, 0f), new Vector3(0.024f, 0.006f, 0.012f), "#3E9A45", new Vector3(0f, 0f, 20f));
            return Dyn(r, 600f, true);
        }

        static Transform PetBowl(string room, Vector3 pos, string hex, string food)
        {
            var r = NewProp(room, "Pet bowl", pos);
            PCyl(r, new Vector3(0f, 0.022f, 0f), 0.065f, 0.044f, hex, default, 0.085f, 20);
            PRing(r, new Vector3(0f, 0.044f, 0f), 0.086f, 0.07f, 0.006f, Shade(hex, 1.15f));
            PCyl(r, new Vector3(0f, 0.04f, 0f), 0.07f, 0.006f, food, default, -1f, 20);
            for (int i = 0; i < 6; i++) PBall(r, new Vector3(Rf(-0.04f, 0.04f), 0.045f, Rf(-0.04f, 0.04f)), Vector3.one * 0.014f, Shade(food, 0.8f));
            return Dyn(r, 400f);
        }

        static Transform Bone(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Chew bone", pos, yaw);
            PCyl(r, new Vector3(0f, 0.013f, 0f), 0.011f, 0.11f, Cream, new Vector3(0f, 0f, 90f), -1f, 10);
            foreach (var sd in new[] { -1f, 1f }) foreach (var z in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.058f, 0.013f, z * 0.011f), Vector3.one * 0.026f, Cream);
            return Dyn(r, 300f);
        }

        /// <summary>Kettle: tapered body, lid with knob, spout, ring handle.</summary>
        static Transform Kettle(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Kettle", pos, yaw);
            PCyl(r, new Vector3(0f, 0.09f, 0f), 0.09f, 0.18f, hex, default, 0.06f, 20);
            PCyl(r, new Vector3(0f, 0.185f, 0f), 0.06f, 0.012f, Shade(hex, 0.8f), default, 0.05f, 20);
            PBall(r, new Vector3(0f, 0.2f, 0f), new Vector3(0.025f, 0.02f, 0.025f), Ink);
            PCyl(r, new Vector3(0f, 0.1f, 0.09f), 0.016f, 0.09f, hex, new Vector3(50f, 0f, 0f), 0.01f, 10);
            PRing(r, new Vector3(0f, 0.15f, -0.08f), 0.05f, 0.037f, 0.02f, Ink, new Vector3(0f, 0f, 90f));
            PCyl(r, new Vector3(0f, 0.005f, 0f), 0.095f, 0.01f, Ink, default, -1f, 20);
            return Stat(r, room);
        }

        static Transform Toaster(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Toaster", pos, yaw);
            PBox(r, new Vector3(0f, 0.085f, 0f), new Vector3(0.26f, 0.17f, 0.16f), "#B9C3CF", 0.35f, default, 0.04f);
            foreach (var x in new[] { -0.05f, 0.05f })
            {
                PBox(r, new Vector3(x, 0.171f, 0f), new Vector3(0.03f, 0.004f, 0.12f), Ink, 0.4f);
                PBox(r, new Vector3(x, 0.19f, 0f), new Vector3(0.012f, 0.04f, 0.1f), "#D9A35E", 0.3f);   // toast popping up
            }
            PBox(r, new Vector3(0.131f, 0.12f, 0f), new Vector3(0.008f, 0.012f, 0.03f), "#FF5D73", 0.4f);
            return Stat(r, room);
        }

        static Transform CerealBox(string room, Vector3 pos, float yaw, string hex, float h)
        {
            var r = NewProp(room, "Cereal box", pos, yaw);
            PBox(r, new Vector3(0f, h / 2f, 0f), new Vector3(0.19f, h, 0.07f), hex, 0.1f);
            PBox(r, new Vector3(0f, h * 0.82f, 0.036f), new Vector3(0.15f, h * 0.15f, 0.002f), "#FFFFFF", 0.2f);
            PCyl(r, new Vector3(0f, h * 0.38f, 0.036f), 0.055f, 0.002f, "#FFFFFF", new Vector3(90f, 0f, 0f), -1f, 18);
            for (int i = 0; i < 5; i++) PBall(r, new Vector3(Rf(-0.03f, 0.03f), h * 0.4f + Rf(-0.01f, 0.02f), 0.038f), new Vector3(0.016f, 0.016f, 0.004f), "#FFC43D");
            return Stat(r, room);
        }

        static Transform Jar(string room, Vector3 pos, string lid, float h)
        {
            var r = NewProp(room, "Jar", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, h / 2f, 0f), 0.035f, h, "#CFEFF5", default, -1f, 16);
            PCyl(r, new Vector3(0f, h * 0.35f, 0f), 0.031f, h * 0.65f, lid == "#FFC43D" ? "#8B5A2B" : Shade(lid, 1.4f), default, -1f, 16);
            PCyl(r, new Vector3(0f, h + 0.008f, 0f), 0.038f, 0.016f, lid, default, -1f, 16);
            PBox(r, new Vector3(0f, h * 0.55f, 0.035f), new Vector3(0.035f, 0.025f, 0.002f), "#FFFFFF", 0.3f);
            return Stat(r, room);
        }

        static Transform ShoppingBag(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Shopping bag", pos, yaw);
            PBox(r, new Vector3(0f, 0.17f, 0f), new Vector3(0.3f, 0.34f, 0.16f), hex, 0.08f);
            foreach (var x in new[] { -0.06f, 0.06f }) PRing(r, new Vector3(x, 0.36f, 0f), 0.04f, 0.032f, 0.012f, Shade(hex, 0.75f), new Vector3(90f, 0f, 0f));
            PCyl(r, new Vector3(-0.07f, 0.36f, 0.02f), 0.03f, 0.22f, "#D9A35E", new Vector3(0f, 0f, 15f), -1f, 12);                   // baguette
            PCyl(r, new Vector3(0.06f, 0.37f, -0.02f), 0.018f, 0.16f, "#FFFFFF", new Vector3(0f, 0f, -12f), -1f, 10);                 // leek
            PBall(r, new Vector3(0.075f, 0.46f, -0.02f), new Vector3(0.05f, 0.09f, 0.03f), "#56B65A", new Vector3(0f, 0f, -12f));
            PBox(r, new Vector3(0f, 0.2f, 0.081f), new Vector3(0.12f, 0.1f, 0.002f), "#FFFFFF", 0.3f);
            return Stat(r, room, true);
        }

        static Transform Bin(string room, Vector3 pos, string hex)
        {
            var r = NewProp(room, "Trash bin", pos);
            PCyl(r, new Vector3(0f, 0.22f, 0f), 0.15f, 0.44f, hex, default, 0.175f, 22);
            PCyl(r, new Vector3(0f, 0.455f, 0f), 0.18f, 0.03f, Shade(hex, 0.75f), default, 0.17f, 22);
            PBall(r, new Vector3(0f, 0.475f, 0f), new Vector3(0.05f, 0.025f, 0.05f), Shade(hex, 0.75f));
            PBox(r, new Vector3(0f, 0.02f, 0.165f), new Vector3(0.1f, 0.02f, 0.06f), Ink, 0.4f);
            return Stat(r, room, true);
        }

        static Transform WateringCan(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Watering can", pos, yaw);
            PCyl(r, new Vector3(0f, 0.1f, 0f), 0.1f, 0.2f, hex, default, 0.09f, 20);
            PCyl(r, new Vector3(0f, 0.15f, 0.14f), 0.015f, 0.2f, hex, new Vector3(55f, 0f, 0f), 0.012f, 10);
            PCyl(r, new Vector3(0f, 0.22f, 0.22f), 0.022f, 0.03f, Shade(hex, 0.8f), new Vector3(55f, 0f, 0f), 0.03f, 12);
            PRing(r, new Vector3(0f, 0.22f, -0.03f), 0.08f, 0.066f, 0.02f, hex, new Vector3(0f, 0f, 90f));
            return Stat(r, room, true);
        }

        /// <summary>Garden gnome: boots, blue coat with belt, white beard, pink face and nose, tall red hat.</summary>
        static Transform Gnome(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Garden gnome", pos, yaw);
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.03f, 0.015f, 0.015f), new Vector3(0.04f, 0.03f, 0.06f), "#6B3E26");
            PCyl(r, new Vector3(0f, 0.08f, 0f), 0.07f, 0.13f, "#3A86FF", default, 0.05f, 18);
            PRing(r, new Vector3(0f, 0.07f, 0f), 0.068f, 0.05f, 0.016f, Ink);
            PBox(r, new Vector3(0f, 0.07f, 0.064f), new Vector3(0.022f, 0.018f, 0.006f), Gold, 0.3f);
            PBall(r, new Vector3(0f, 0.15f, 0.035f), new Vector3(0.09f, 0.1f, 0.05f), "#FFFFFF");
            PBall(r, new Vector3(0f, 0.18f, 0.01f), new Vector3(0.07f, 0.065f, 0.065f), "#FFE2BF");
            PBall(r, new Vector3(0f, 0.178f, 0.045f), new Vector3(0.022f, 0.02f, 0.02f), "#FF8A9A");
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.015f, 0.19f, 0.04f), new Vector3(0.01f, 0.012f, 0.006f), Ink);
            PCyl(r, new Vector3(0f, 0.25f, -0.005f), 0.045f, 0.13f, "#FF5D73", new Vector3(-8f, 0f, 0f), 0.004f, 16);
            return Stat(r, room, true);
        }

        static Transform Bucket(string room, Vector3 pos, string hex)
        {
            var r = NewProp(room, "Bucket", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, 0.09f, 0f), 0.09f, 0.18f, hex, default, 0.115f, 20);
            PRing(r, new Vector3(0f, 0.18f, 0f), 0.12f, 0.105f, 0.01f, Shade(hex, 1.15f));
            PRing(r, new Vector3(0f, 0.2f, 0f), 0.11f, 0.102f, 0.006f, "#B9C3CF", new Vector3(0f, 0f, 90f));
            PCyl(r, new Vector3(0f, 0.17f, 0f), 0.1f, 0.01f, "#E8C07A", default, -1f, 20);     // sand
            return Stat(r, room, true);
        }

        static Transform Spade(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Spade", pos, yaw);
            PBox(r, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.07f, 0.008f, 0.09f), hex, 0.4f);
            PCyl(r, new Vector3(0f, 0.012f, -0.06f), 0.007f, 0.2f, "#FFC43D", new Vector3(90f, 0f, 0f), -1f, 8);
            PBox(r, new Vector3(0f, 0.012f, -0.165f), new Vector3(0.05f, 0.014f, 0.016f), "#FFC43D", 0.4f);
            return Dyn(r, 300f);
        }
    }
}
