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
    }
}
