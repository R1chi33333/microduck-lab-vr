using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ================================================================== what counts as an item
        /// <summary>
        /// Furniture the house builder makes from separate pieces side by side in a room (a table top and its legs,
        /// a chair's seat, back and legs, the invisible colliders next to the sofa). The pieces of one item are the
        /// anchor plus the listed parts within radius metres (in plan) of it.
        /// </summary>
        struct Assembly { public string name, category, anchor; public float radius; public string[] parts; }
        static Assembly A(string name, string category, string anchor, float radius, params string[] parts)
            => new Assembly { name = name, category = category, anchor = anchor, radius = radius, parts = parts };

        static readonly Assembly[] Assemblies =
        {
            A("Table", "Furniture", "Tabletop", 1.0f, "Table leg"),
            A("Sofa", "Furniture", "Sofa", 1.3f, "Sofa seat collider", "Sofa back collider"),
            A("Coffee table", "Furniture", "Coffee table", 0.6f, "Coffee table collider", "Coffee table leg collider"),
            A("Armchair", "Furniture", "Armchair", 0.3f, "Armchair collider"),
            A("Side table", "Furniture", "Side table", 0.1f, "Side table top"),
            A("Floor plant", "Furniture", "Plant pot", 0.1f, "Plant"),
            A("Kitchen units", "Furniture", "Cabinets", 1.6f, "Counter", "Cabinet door", "Knob", "Upper cabinet", "Sink"),
            A("Kitchen table", "Furniture", "Kitchen table", 0.8f, "Kitchen table leg"),
            A("Kitchen chair", "Furniture", "Chair seat", 0.3f, "Chair back", "Chair leg"),
            A("Bed", "Furniture", "Bed frame", 1.2f, "Mattress", "Quilt", "Pillow", "Headboard"),
            A("Toy box", "Furniture", "Toy box", 0.4f, "Toy box lid"),
            A("Scooter", "Clutter", "Scooter deck", 0.5f, "Scooter stem", "Scooter bar", "Scooter wheel"),
            A("Picture", "Clutter", "Picture frame", 0.06f, "Picture"),
            A("Potted bush", "Clutter", "Garden pot", 0.1f, "Bush"),
            A("Football goal", "Clutter", "Goal back", 0.25f, "Goal bar", "Goal side"),
        };

        /// <summary>Single-piece objects (no children) that are items of their own; other single pieces are architecture.</summary>
        static readonly HashSet<string> Singles = new HashSet<string> { "Shelf", "Pouf", "Fridge", "Fruit bowl", "Notebook", "Step stool", "Floor mat", "Shoe", "Bookcase", "Book block" };

        static readonly HashSet<string> FurnitureNames = new HashSet<string>
        {
            "Sofa", "Coffee table", "Armchair", "Shelf", "Pouf", "Fridge", "Bookcase", "TV console", "Floor lamp", "Wardrobe", "Desk",
            "Desk chair", "Beanbag", "Shoe rack", "Console table", "Bench", "Raised bed", "Step stool", "Umbrella stand", "Pet bed",
            "Laundry basket", "Trash bin", "Hose reel",
        };

        /// <summary>Walls, floors, ramps, the pitch, the garden and the world outside: part of the house, not items
        /// (single pieces must be in Singles anyway; this list matters for things built from parts).</summary>
        static readonly string[] Architecture =
        {
            "Wall", "wall_", "Ceiling", "Lawn", "Grass", "Window", "Rug", "Pitch", "Garage mat", "Turntable", "Dash", "Start line",
            "Finish line", "Ramp", "Platform", "Sky drum", "Cloud", "Hedge", "Tree", "Neighbour house", "Patio slab", "Stepping stone",
            "Puddle", "Garden slope", "Slope plateau", "Picket", "Fence", "Round rug", "Block ramp", "Toy train", "Goal",
        };

        static string CategoryFor(string name)
        {
            foreach (var a in Assemblies) if (a.name == name) return a.category;
            return FurnitureNames.Contains(name) ? "Furniture" : "Clutter";
        }

        class Item
        {
            public string name, category, room, linkedPrefab;
            public readonly List<Transform> pieces = new List<Transform>();
            public bool wrap;                         // several pieces under a new root named after the item
            public Quaternion frame = Quaternion.identity;
            public int order;
        }

        /// <summary>An item copied into the export scene: root at the origin, unrotated, pivot at the bottom centre.</summary>
        class Candidate { public GameObject go; public Vector3 pivotPos; public Quaternion pivotRot; public bool resetScale; public string sig; }

        [Serializable] class IndexEntry { public string sig, prefab, category, family, name; }
        [Serializable] class IndexFile { public List<IndexEntry> items = new List<IndexEntry>(); }

        // ------------------------------------------------------------------ collecting
        static Transform FindHouseRoot(Scene s)
        {
            foreach (var g in s.GetRootGameObjects()) if (g.name == "House") return g.transform;
            return null;
        }

        static List<Item> CollectHouseItems(Transform house, ref int skipped)
        {
            var all = new List<Item>();
            foreach (Transform room in house)
            {
                if (!room.name.StartsWith("Room ")) continue;
                string roomId = room.name.Substring(5);
                var kids = new List<Transform>();
                foreach (Transform k in room) kids.Add(k);
                var claimed = new HashSet<Transform>();
                var items = new List<Item>();

                // things already linked to one of our prefabs
                foreach (var k in kids)
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(k.gameObject)) continue;
                    string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(k.gameObject);
                    if (string.IsNullOrEmpty(p) || !p.StartsWith(Root + "/")) continue;
                    var it = new Item { name = k.name, category = CategoryFor(k.name), room = roomId, linkedPrefab = p, order = k.GetSiblingIndex() };
                    it.pieces.Add(k); claimed.Add(k); items.Add(it);
                }

                // furniture made of separate pieces
                foreach (var a in Assemblies)
                    foreach (var anchor in kids)
                    {
                        if (claimed.Contains(anchor) || anchor.name != a.anchor) continue;
                        var it = new Item { name = a.name, category = a.category, room = roomId, wrap = true, order = anchor.GetSiblingIndex() };
                        it.frame = Quaternion.Euler(0f, anchor.rotation.eulerAngles.y, 0f);
                        it.pieces.Add(anchor); claimed.Add(anchor);
                        foreach (var k in kids)
                        {
                            if (claimed.Contains(k) || Array.IndexOf(a.parts, k.name) < 0) continue;
                            var d = k.position - anchor.position; d.y = 0f;
                            if (d.magnitude > a.radius) continue;
                            it.pieces.Add(k); claimed.Add(k);
                        }
                        items.Add(it);
                    }

                // props (a root with parts) and the listed single pieces
                foreach (var k in kids)
                {
                    if (claimed.Contains(k)) continue;
                    // single pieces only when listed; props with parts unless they are scenery
                    bool skip = k.childCount == 0 ? !Singles.Contains(k.name) : Architecture.Any(pre => k.name.StartsWith(pre));
                    if (skip || !HasRenderer(k)) { skipped++; continue; }
                    var it = new Item { name = k.name, category = CategoryFor(k.name), room = roomId, order = k.GetSiblingIndex() };
                    it.pieces.Add(k); items.Add(it);
                }
                items.Sort((x, y) => x.order.CompareTo(y.order));
                all.AddRange(items);
            }
            return all;
        }

        static bool HasRenderer(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (r.enabled) return true;
            return false;
        }

        // ------------------------------------------------------------------ normalising
        /// <summary>Copies an item into the export scene, moves its pivot to the bottom centre and its root to the origin.</summary>
        static Candidate BuildCandidate(Item it, Transform parent)
        {
            var c = new Candidate();
            GameObject root;
            if (!it.wrap)
            {
                var src = it.pieces[0];
                root = Object.Instantiate(src.gameObject, parent, true);
                root.name = src.name;
                c.pivotRot = src.rotation;
            }
            else
            {
                var first = it.pieces[0];
                root = new GameObject(it.name);
                root.transform.SetParent(parent, false);
                root.transform.SetPositionAndRotation(first.position, it.frame);
                root.layer = first.gameObject.layer;
                GameObjectUtility.SetStaticEditorFlags(root, GameObjectUtility.GetStaticEditorFlags(first.gameObject));
                foreach (var p in it.pieces) { var k = Object.Instantiate(p.gameObject, root.transform, true); k.name = p.name; }
                c.pivotRot = it.frame;
            }
            c.pivotPos = Normalise(root.transform, true);
            c.resetScale = root.transform.childCount == 0;
            c.go = root;
            c.sig = Signature(root.transform);
            return c;
        }

        /// <summary>
        /// Puts the pivot at the bottom centre of the item (its own axes) and the root at the origin, unrotated; the
        /// parts keep their places relative to each other. A single piece without children gets its offset and scale
        /// baked into a copy of its mesh and colliders. Returns the old world position of the new pivot.
        /// </summary>
        static Vector3 Normalise(Transform root, bool bakeLeafScale)
        {
            var b = LocalBounds(root);
            var pivot = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 world = root.TransformPoint(pivot);
            bool leaf = root.childCount == 0 && bakeLeafScale;
            Vector3 s = leaf ? root.localScale : Vector3.one;
            if (Mathf.Abs(s.x) < 1e-6f || Mathf.Abs(s.y) < 1e-6f || Mathf.Abs(s.z) < 1e-6f) s = Vector3.one;
            bool moved = pivot.sqrMagnitude > 1e-12f || s != Vector3.one;

            if (moved)
            {
                foreach (Transform ch in root) ch.localPosition -= pivot;
                var mf = root.GetComponent<MeshFilter>();
                Mesh orig = mf != null ? mf.sharedMesh : null, baked = null;
                if (orig != null) { baked = Bake(orig, pivot, s); mf.sharedMesh = baked; }
                foreach (var col in root.GetComponents<Collider>())
                {
                    switch (col)
                    {
                        case BoxCollider bc:
                            bc.center = Vector3.Scale(bc.center - pivot, s);
                            var sz = Vector3.Scale(bc.size, s); bc.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                            break;
                        case SphereCollider sc:
                            sc.center = Vector3.Scale(sc.center - pivot, s);
                            sc.radius *= Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                            break;
                        case CapsuleCollider cc:
                            cc.center = Vector3.Scale(cc.center - pivot, s);
                            break;
                        case MeshCollider mc:
                            if (mc.sharedMesh != null) mc.sharedMesh = mc.sharedMesh == orig && baked != null ? baked : Bake(mc.sharedMesh, pivot, s);
                            break;
                    }
                }
            }
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            if (leaf) root.localScale = Vector3.one;
            return world;
        }

        static Mesh Bake(Mesh src, Vector3 pivot, Vector3 s)
        {
            var m = Object.Instantiate(src);
            m.name = src.name;
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i] - pivot, s);
            m.vertices = v;
            if (s != Vector3.one)
            {
                var n = m.normals;
                if (n != null && n.Length == v.Length)
                {
                    var inv = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);
                    for (int i = 0; i < n.Length; i++) n[i] = Vector3.Scale(n[i], inv).normalized;
                    m.normals = n;
                }
            }
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Bounds of the visible meshes and the colliders under root, in root's local space.</summary>
        static Bounds LocalBounds(Transform root)
        {
            var inv = root.worldToLocalMatrix;
            bool any = false; var b = new Bounds();
            void Add(Matrix4x4 m, Bounds lb)
            {
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var q = m.MultiplyPoint3x4(corner);
                    if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
                }
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled) continue;
                Add(inv * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            }
            foreach (var col in root.GetComponentsInChildren<Collider>(true))
            {
                Bounds lb;
                switch (col)
                {
                    case BoxCollider bc: lb = new Bounds(bc.center, bc.size); break;
                    case SphereCollider sc: lb = new Bounds(sc.center, Vector3.one * sc.radius * 2f); break;
                    case CapsuleCollider cc: lb = new Bounds(cc.center, Vector3.one * Mathf.Max(cc.height, cc.radius * 2f)); break;
                    case MeshCollider mc when mc.sharedMesh != null: lb = mc.sharedMesh.bounds; break;
                    default: continue;
                }
                Add(inv * col.transform.localToWorldMatrix, lb);
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
