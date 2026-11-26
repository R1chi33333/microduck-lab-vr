using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicroduckVR.Comic;
using MicroduckVR.Custom;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Turns the game's items into reusable assets under Assets/MicroduckVR/Prefabs:
    ///   Export Prefabs    one prefab per house item (furniture, clutter), per bag toy and per duck colourway, each
    ///                     with its own mesh asset, a bottom-centre pivot and an unrotated frame;
    ///   Make Catalog      a labelled preview sheet (Logs/assets/catalog.png) and the item list (docs/ASSETS.md);
    ///   Export Package    Builds/MicroduckLab_Assets.unitypackage with the prefabs, what they need and CREDITS.txt;
    ///   Link Scene To Prefabs  swaps the House scene's furniture and clutter for instances of those prefabs.
    /// Every menu item is also a public static method without arguments, and logs a "[Microduck]" summary.
    /// </summary>
    public static partial class AssetExport
    {
        public const string Root = "Assets/MicroduckVR/Prefabs";
        public const string IndexPath = Root + "/prefab_index.json";
        public const string CreditsPath = Root + "/CREDITS.txt";
        const string MeshRoot = Root + "/Meshes", MatRoot = Root + "/Materials", LiveDir = Root + "/Ducks/Live";
        const string PackagePath = "Builds/MicroduckLab_Assets.unitypackage";
        const string CatalogPath = "Logs/assets/catalog.png";
        const string DocsPath = "docs/ASSETS.md";
        const string OutlineShaderPath = "Assets/MicroduckVR/Shaders/ComicOutline.shader";
        const string RelinkPref = "Microduck.RelinkHouseAfterBuild";
        const string RelinkMenu = "Microduck/Assets/Relink House After Build";
        /// <summary>At most this many different versions of one item (books, blocks, flowers come in many sizes and colours).</summary>
        const int MaxVariants = 8;
        static readonly string[] Categories = { "Toys", "Furniture", "Clutter", "Ducks" };

        /// <summary>Scripts that may travel in the package: they compile on their own.</summary>
        static readonly HashSet<string> PackageScripts = new HashSet<string>
        {
            "Assets/MicroduckVR/Scripts/World/Grabbable.cs",
            "Assets/MicroduckVR/Scripts/World/SurfaceTag.cs",
        };

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

        // ================================================================== counters
        static int nPrefabs, nMeshes, nMaterials, nWarnings;
        static readonly Dictionary<string, int> perCategory = new Dictionary<string, int>();
        static readonly Dictionary<Object, Object> savedMaterials = new Dictionary<Object, Object>();
        static readonly HashSet<string> usedMaterialPaths = new HashSet<string>();
        static readonly HashSet<string> written = new HashSet<string>();

        static void Reset()
        {
            nPrefabs = nMeshes = nMaterials = nWarnings = 0;
            perCategory.Clear(); savedMaterials.Clear(); usedMaterialPaths.Clear(); written.Clear();
        }

        static void Warn(string msg) { nWarnings++; Debug.LogWarning("[Microduck] " + msg); }

        // ================================================================== 1. Export Prefabs
        [MenuItem("Microduck/Assets/Export Prefabs")]
        public static void ExportPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Export Prefabs: leave Play mode first"); return; }
            var house = EnsureHouseScene();
            if (!house.IsValid()) return;
            Reset();
            foreach (var c in Categories) { EnsureFolder($"{Root}/{c}"); EnsureFolder($"{MeshRoot}/{c}"); }
            var index = LoadIndex();
            int kept = 0, dup = 0, capped = 0, skipped = 0, toys = 0, ducks = 0;
            var temp = OpenTemp();
            try
            {
                // ---------------------------------------------------------- house items
                var houseRoot = FindHouseRoot(house);
                if (houseRoot == null) Warn("Export Prefabs: no 'House' object in " + house.path + " (run Microduck/Scenes/Build House first); house items skipped");
                else
                {
                    var items = CollectHouseItems(houseRoot, ref skipped);
                    var linked = new HashSet<string>(items.Where(i => i.linkedPrefab != null).Select(i => i.linkedPrefab));
                    foreach (var it in items)
                    {
                        if (it.linkedPrefab != null) { kept++; continue; }      // the scene already uses this prefab: the prefab is the original now
                        var c = BuildCandidate(it, temp.root);
                        var e = index.items.FirstOrDefault(x => x.sig == c.sig);
                        if (e != null)
                        {
                            if (written.Contains(e.prefab) || linked.Contains(e.prefab)) { dup++; Object.DestroyImmediate(c.go); continue; }
                        }
                        else
                        {
                            string family = it.category + "/" + it.name;
                            int n = index.items.Count(x => x.category + "/" + x.family == family);
                            if (n >= MaxVariants) { capped++; Object.DestroyImmediate(c.go); continue; }
                            string file = FreeName(index, it.category, Sanitize(it.name), n);
                            e = new IndexEntry { sig = c.sig, category = it.category, family = it.name, name = file, prefab = $"{Root}/{it.category}/{file}.prefab" };
                            index.items.Add(e);
                        }
                        Save(c.go, e.category, e.name, e.name, null);
                        Object.DestroyImmediate(c.go);
                    }
                }

                // ---------------------------------------------------------- bag toys
                foreach (var entry in ToyCatalog.All)
                {
                    var go = ToyCatalog.Spawn(entry.kind, Vector3.zero, 0f);
                    if (go == null) { Warn("toy " + entry.kind + " did not build"); continue; }
                    go.transform.SetParent(temp.root, true);
                    var tag = go.GetComponent<ToyTag>();
                    if (tag != null) Object.DestroyImmediate(tag);        // ToyTag lives in ToyCatalog.cs and cannot be saved on a prefab
                    go.name = entry.label;
                    Normalise(go.transform, false);
                    string file = Sanitize(entry.label);
                    Save(go, "Toys", file, file, null);
                    Object.DestroyImmediate(go);
                    toys++;
                }

                // ---------------------------------------------------------- ducks
                ducks = ExportDucks(temp.root);
            }
            catch (Exception ex) { Debug.LogException(ex); Warn("Export Prefabs stopped early: " + ex.Message); }
            finally
            {
                CloseTemp(temp);
                SaveIndex(index);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // prefabs from an earlier run that this run did not write
            var stale = new List<string>();
            foreach (var cat in Categories)
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { $"{Root}/{cat}" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (!written.Contains(p) && !index.items.Any(x => x.prefab == p)) stale.Add(p);
                }
            if (stale.Count > 0) Warn($"{stale.Count} prefab(s) not written by this run (kept): " + string.Join(", ", stale.Take(12)) + (stale.Count > 12 ? ", ..." : ""));

            string cats = string.Join(", ", Categories.Select(c => $"{c} {(perCategory.TryGetValue(c, out var k) ? k : 0)}"));
            Debug.Log($"[Microduck] Export Prefabs: {nPrefabs} prefabs ({cats}), {nMeshes} meshes, {nMaterials} materials saved, {nWarnings} warnings. " +
                      $"House: {kept} kept (scene uses the prefab), {dup} duplicates folded, {capped} variants over the cap of {MaxVariants}, {skipped} architecture pieces skipped. " +
                      $"Toys {toys}, duck prefabs {ducks}. Index: {IndexPath} ({index.items.Count} entries).");
        }

        static string FreeName(IndexFile index, string category, string baseName, int n)
        {
            for (int k = n; ; k++)
            {
                string name = k == 0 ? baseName : $"{baseName} {k + 1}";
                if (!index.items.Any(x => x.category == category && x.name == name) && !written.Contains($"{Root}/{category}/{name}.prefab")) return name;
            }
        }

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

        // ------------------------------------------------------------------ identity of an item
        /// <summary>
        /// What an item is made of, relative to its pivot: parts, their placement (1 mm), meshes (vertex and index
        /// counts, size to 5 mm), materials, colliders, physics and gameplay values. Items with the same signature
        /// share one prefab; the hand-drawn wobble of each box does not count.
        /// </summary>
        static string Signature(Transform root)
        {
            var sb = new StringBuilder();
            var inv = root.worldToLocalMatrix;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                sb.Append(t == root ? "." : PathOf(t, root)).Append('|');
                var m = inv * t.localToWorldMatrix;
                for (int col = 0; col < 4; col++)
                    for (int row = 0; row < 3; row++) sb.Append(Rn(m[row, col], col == 3 ? 0.001f : 0.002f)).Append(',');
                sb.Append('L').Append(t.gameObject.layer).Append('S').Append((int)GameObjectUtility.GetStaticEditorFlags(t.gameObject)).Append(t.gameObject.activeSelf ? 'A' : 'I').Append('|');
                foreach (var comp in t.GetComponents<Component>())
                {
                    if (comp == null) { sb.Append("missing;"); continue; }
                    if (comp is Transform) continue;
                    sb.Append(comp.GetType().Name).Append(':');
                    switch (comp)
                    {
                        case MeshFilter f: sb.Append(MeshKey(f.sharedMesh)); break;
                        case MeshRenderer r:
                            sb.Append(r.enabled).Append(r.shadowCastingMode).Append(r.receiveShadows);
                            foreach (var mat in r.sharedMaterials) sb.Append('+').Append(mat != null ? mat.name : "null");
                            break;
                        case BoxCollider bc: sb.Append(V(bc.center)).Append(V(bc.size)).Append(bc.isTrigger).Append(PhysName(bc)); break;
                        case SphereCollider sc: sb.Append(V(sc.center)).Append(Rn(sc.radius, 0.001f)).Append(sc.isTrigger).Append(PhysName(sc)); break;
                        case CapsuleCollider cc: sb.Append(V(cc.center)).Append(Rn(cc.radius, 0.001f)).Append(Rn(cc.height, 0.001f)).Append(cc.direction).Append(PhysName(cc)); break;
                        case MeshCollider mc: sb.Append(mc.convex).Append(MeshKey(mc.sharedMesh)).Append(PhysName(mc)); break;
                        case Rigidbody rb:
                            sb.Append(Rn(rb.mass, 0.0005f)).Append(rb.isKinematic).Append(rb.useGravity).Append(Rn(rb.linearDamping, 0.01f)).Append(Rn(rb.angularDamping, 0.01f))
                              .Append(rb.interpolation).Append(rb.collisionDetectionMode);
                            break;
                        case Grabbable g: sb.Append(g.label).Append(Rn(g.gripWidth, 0.0005f)); break;
                        case SurfaceTag st: sb.Append(st.type).Append('/').Append(st.room); break;
                    }
                    sb.Append(';');
                }
                sb.Append('\n');
            }
            using (var md5 = System.Security.Cryptography.MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
        static string Rn(float v, float step) => Mathf.RoundToInt(v / step).ToString();
        static string V(Vector3 v) => $"({Rn(v.x, 0.001f)},{Rn(v.y, 0.001f)},{Rn(v.z, 0.001f)})";
        static string PhysName(Collider c) => c.sharedMaterial != null ? c.sharedMaterial.name : "-";
        static string MeshKey(Mesh m)
        {
            if (m == null) return "none";
            if (IsBuiltin(m)) return "builtin:" + m.name;
            long idx = 0; for (int i = 0; i < m.subMeshCount; i++) idx += m.GetIndexCount(i);
            var s = m.bounds.size; var c = m.bounds.center;
            return $"{m.vertexCount}/{m.subMeshCount}/{idx}/{Rn(s.x, 0.005f)},{Rn(s.y, 0.005f)},{Rn(s.z, 0.005f)}/{Rn(c.x, 0.005f)},{Rn(c.y, 0.005f)},{Rn(c.z, 0.005f)}";
        }

        static bool IsBuiltin(Object o)
        {
            string p = AssetDatabase.GetAssetPath(o);
            return p.StartsWith("Library/") || p.StartsWith("Resources/unity_builtin_extra");
        }

        // ------------------------------------------------------------------ saving
        /// <summary>Saves materials and meshes the item still has only in memory, then the prefab.</summary>
        static void Save(GameObject go, string category, string file, string meshAsset, Dictionary<Mesh, Mesh> sharedMeshes)
        {
            EnsureFolder($"{Root}/{category}");
            PersistMaterials(go, category);
            PersistMeshes(go, $"{MeshRoot}/{category}/{meshAsset}.asset", file, sharedMeshes);
            string path = $"{Root}/{category}/{file}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path, out bool ok);
            if (!ok) { Warn("could not save " + path); return; }
            written.Add(path);
            nPrefabs++;
            perCategory[category] = (perCategory.TryGetValue(category, out var k) ? k : 0) + 1;
        }

        static void PersistMeshes(GameObject go, string meshPath, string file, Dictionary<Mesh, Mesh> shared)
        {
            var uses = new List<(Mesh mesh, string owner)>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) uses.Add((mf.sharedMesh, mf.name));
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) uses.Add((mc.sharedMesh, mc.name + "_collider"));
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) uses.Add((smr.sharedMesh, smr.name));

            var map = new Dictionary<Mesh, Mesh>();
            var create = new List<Mesh>();
            var names = new HashSet<string>();
            foreach (var (mesh, owner) in uses)
            {
                if (mesh == null || map.ContainsKey(mesh)) continue;
                if (IsBuiltin(mesh)) { map[mesh] = mesh; continue; }               // Unity's own cube and sphere are in every project
                if (shared != null && shared.TryGetValue(mesh, out var have)) { map[mesh] = have; continue; }
                var copy = Object.Instantiate(mesh);
                string n = Sanitize(file + "_" + owner); string unique = n;
                for (int i = 2; !names.Add(unique); i++) unique = n + "_" + i;
                copy.name = unique;
                create.Add(copy); map[mesh] = copy;
                if (shared != null) shared[mesh] = copy;
            }
            if (create.Count > 0)
            {
                if (AssetDatabase.LoadMainAssetAtPath(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(create[0], meshPath);
                for (int i = 1; i < create.Count; i++) AssetDatabase.AddObjectToAsset(create[i], create[0]);
                AssetDatabase.SaveAssets();
                nMeshes += create.Count;
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null && map.TryGetValue(mf.sharedMesh, out var m)) mf.sharedMesh = m;
            foreach (var mc in go.GetComponentsInChildren<MeshCollider>(true)) if (mc.sharedMesh != null && map.TryGetValue(mc.sharedMesh, out var m)) mc.sharedMesh = m;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (smr.sharedMesh != null && map.TryGetValue(smr.sharedMesh, out var m)) smr.sharedMesh = m;
        }

        /// <summary>Materials made at runtime (toys, ducks) and physics materials made in code get saved as assets; saved ones are kept as they are.</summary>
        static void PersistMaterials(GameObject go, string category)
        {
            string dir = $"{MatRoot}/{category}";
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null || AssetDatabase.Contains(m)) continue;
                    if (!savedMaterials.TryGetValue(m, out var saved)) { saved = SaveCopy(m, dir, ".mat"); savedMaterials[m] = saved; }
                    mats[i] = (Material)saved; changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
            {
                var pm = col.sharedMaterial;
                if (pm == null || AssetDatabase.Contains(pm)) continue;
                if (!savedMaterials.TryGetValue(pm, out var saved)) { saved = SaveCopy(pm, dir, ".physicMaterial"); savedMaterials[pm] = saved; }
                col.sharedMaterial = (PhysicsMaterial)saved;
            }
        }

        /// <summary>A copy (so the game's runtime caches never turn into assets), written over an earlier export of the same name.</summary>
        static Object SaveCopy(Object src, string dir, string ext)
        {
            EnsureFolder(dir);
            string n = Sanitize(src.name.StartsWith("Toon ") ? src.name.Substring(5) : src.name);
            if (string.IsNullOrEmpty(n)) n = "material";
            string path = $"{dir}/{n}{ext}";
            for (int i = 2; usedMaterialPaths.Contains(path); i++) path = $"{dir}/{n}_{i}{ext}";
            usedMaterialPaths.Add(path);
            var copy = Object.Instantiate(src);
            copy.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && existing.GetType() == copy.GetType())
            {
                EditorUtility.CopySerialized(copy, existing);
                existing.name = copy.name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(copy);
                nMaterials++;
                return existing;
            }
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            nMaterials++;
            return copy;
        }

        // ------------------------------------------------------------------ ducks
        /// <summary>
        /// The duck as a flock duck is built (rig under DuckBody: tilt > model > rig, joints in the stand pose, the
        /// comic look applied), then frozen: DuckBody is taken off, hidden hardware is dropped, meshes are copied so
        /// the prefab needs neither glTFast nor the game scripts. One prefab per colourway, all sharing one mesh asset.
        /// Plus "Duck (live)" in Ducks/Live: DuckBody + DuckSetup + WanderDuck, the game's own runtime duck.
        /// </summary>
        static int ExportDucks(Transform parent)
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            if (glb == null || map == null) { Warn("duck rig or joint map missing (Microduck/Import Duck Rig); no duck prefabs"); return 0; }
            int n = 0;
            var shared = new Dictionary<Mesh, Mesh>();
            foreach (var skin in DuckCustom.SkinIds)
            {
                string name = skin == "cream" ? "Duck" : "Duck " + skin;
                var root = new GameObject(name);
                root.transform.SetParent(parent, false);
                var rig = Object.Instantiate(glb);
                rig.name = "rig";
                var body = root.AddComponent<DuckBody>();
                body.Init(rig, map);
                try { for (int i = 0; i < 30; i++) body.Step(1f / 60f, Vector3.zero, 0f, Vector3.zero, 0f); }
                catch (Exception ex) { Warn("duck settle step failed (" + ex.Message + "); saved in the plain stand pose"); }
                ComicLook.ApplyToDuck(rig.transform, skin);
                Object.DestroyImmediate(body);
                int stripped = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = DuckSetup.LayerDuck;
                    var r = t.GetComponent<Renderer>();
                    if (r != null && !r.enabled)
                    {
                        Object.DestroyImmediate(r);
                        var f = t.GetComponent<MeshFilter>(); if (f != null) Object.DestroyImmediate(f);
                    }
                    foreach (var c in t.GetComponents<Component>())
                        if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) { Object.DestroyImmediate(c); stripped++; }
                }
                if (stripped > 0) Debug.Log($"[Microduck] {name}: took {stripped} non-visual component(s) off the model");
                Normalise(root.transform, false);
                Save(root, "Ducks", name, "Duck", shared);
                Object.DestroyImmediate(root);
                n++;
            }

            // the live one: the same components as a scene duck, built at runtime; no visible model until Play
            EnsureFolder(LiveDir);
            var live = new GameObject("Duck (live)");
            live.transform.SetParent(parent, false);
            live.SetActive(false);
            live.AddComponent<DuckBody>();
            var setup = live.AddComponent<DuckSetup>();
            setup.rigPrefab = glb; setup.jointMap = map; setup.skin = "cream";
            var wander = live.AddComponent<WanderDuck>();
            wander.boundsMin = new Vector2(-1f, -1f); wander.boundsMax = new Vector2(1f, 1f); wander.seed = 1;
            live.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(live, LiveDir + "/Duck (live).prefab", out bool ok);
            if (ok) { written.Add(LiveDir + "/Duck (live).prefab"); nPrefabs++; perCategory["Ducks"] = (perCategory.TryGetValue("Ducks", out var k) ? k : 0) + 1; n++; }
            else Warn("could not save the live duck prefab");
            Object.DestroyImmediate(live);
            return n;
        }

        // ================================================================== 2. Make Catalog
        const int Cell = 256, LabelH = 28, Cols = 8, CatalogLayer = 31;

        [MenuItem("Microduck/Assets/Make Catalog")]
        public static void MakeCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Make Catalog: leave Play mode first"); return; }
            var list = ListPrefabs(false);
            if (list.Count == 0) { Debug.LogError("[Microduck] Make Catalog: no prefabs under " + Root + " (run Export Prefabs first)"); return; }
            int warnings = 0;
            int rows = (list.Count + Cols - 1) / Cols;
            var sheet = new Texture2D(Cols * Cell, rows * (Cell + LabelH), TextureFormat.RGB24, false);
            var px = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 247, 232, 255);
            sheet.SetPixels32(px);

            var info = new List<(string cat, string name, string path, Vector3 size, string physics)>();
            var temp = OpenTemp();
            var lightsOff = new List<Light>();
            RenderTexture rt = null;
            try
            {
                // lights from the open scenes would light (and shadow) the shots: switch them off while shooting
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var sc = SceneManager.GetSceneAt(s);
                    if (!sc.isLoaded || sc == temp.scene) continue;
                    foreach (var g in sc.GetRootGameObjects())
                        foreach (var l in g.GetComponentsInChildren<Light>()) if (l.enabled) { l.enabled = false; lightsOff.Add(l); }
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.74f, 0.80f, 0.98f);
                RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
                RenderSettings.ambientGroundColor = new Color(0.82f, 0.64f, 0.48f);
                RenderSettings.fog = false;
                var sun = new GameObject("catalog sun").AddComponent<Light>();
                sun.transform.SetParent(temp.root, false);
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard; sun.color = new Color(1f, 0.96f, 0.88f);
                sun.cullingMask = 1 << CatalogLayer;
                sun.transform.rotation = Quaternion.Euler(45f, 200f, 0f);

                var camGo = new GameObject("catalog camera");
                camGo.transform.SetParent(temp.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 30f; cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << CatalogLayer;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                rt = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;

                for (int i = 0; i < list.Count; i++)
                {
                    var (cat, path) = list[i];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var inst = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, temp.scene) as GameObject : null;
                    if (inst == null) { Debug.LogWarning("[Microduck] catalog: could not place " + path); warnings++; continue; }
                    string physics = PhysicsSummary(inst);
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CatalogLayer;
                    var rs = inst.GetComponentsInChildren<Renderer>();
                    var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                    foreach (var r in rs) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                    if (!any) { Debug.LogWarning("[Microduck] catalog: " + path + " has nothing to draw"); warnings++; }
                    info.Add((cat, prefab.name, path, b.size, physics));

                    float radius = Mathf.Max(b.extents.magnitude, 0.005f);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                    var dir = new Vector3(0.55f, 0.5f, 1f).normalized;
                    cam.transform.position = b.center + dir * dist;
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.0005f, dist - radius * 1.5f);
                    cam.farClipPlane = dist + radius * 3f;
                    cam.backgroundColor = CategoryColour(cat);
                    cam.Render();

                    int cx = (i % Cols) * Cell, cy = sheet.height - (i / Cols + 1) * (Cell + LabelH);
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, Cell, Cell), cx, cy + LabelH, false);
                    RenderTexture.active = null;
                    FillRect(sheet, cx, cy, Cell, LabelH, new Color(1f, 0.97f, 0.91f));
                    FillRect(sheet, cx, cy + LabelH - 2, Cell, 2, ComicLook.Ink);
                    DrawText(sheet, cx + 6, cy + LabelH - 7, prefab.name, ComicLook.Ink, 2, Cell - 12);
                    Object.DestroyImmediate(inst);
                }
                // grid lines
                for (int c = 1; c < Cols; c++) FillRect(sheet, c * Cell - 1, 0, 2, sheet.height, ComicLook.Ink);
                for (int r = 1; r < rows; r++) FillRect(sheet, 0, sheet.height - r * (Cell + LabelH) - 1, sheet.width, 2, ComicLook.Ink);
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                File.WriteAllBytes(CatalogPath, sheet.EncodeToPNG());
                cam.targetTexture = null;
            }
            catch (Exception ex) { Debug.LogException(ex); warnings++; }
            finally
            {
                foreach (var l in lightsOff) if (l != null) l.enabled = true;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                Object.DestroyImmediate(sheet);
                CloseTemp(temp);
            }
            WriteDocs(info);
            Debug.Log($"[Microduck] Make Catalog: {info.Count} prefabs drawn to {CatalogPath}, list written to {DocsPath}, {warnings} warnings.");
        }

        // ================================================================== 2b. Design sheets (docs/images)
        const string SheetDir = "docs/images";

        /// <summary>Turnaround of every duck colourway and one labelled sheet per item category, for the README and releases.</summary>
        [MenuItem("Microduck/Assets/Make Design Sheets")]
        public static void MakeDesignSheets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Make Design Sheets: leave Play mode first"); return; }
            var all = ListPrefabs(false);
            if (all.Count == 0) { Debug.LogError("[Microduck] Make Design Sheets: run Export Prefabs first"); return; }
            int written = 0;

            // the duck: rows = colourways, columns = views
            var ducks = all.Where(x => x.cat == "Ducks").OrderBy(x => Path.GetFileNameWithoutExtension(x.path) == "Duck" ? 0 : 1).ToList();
            var views = new[] { ("FRONT", 0f), ("3/4", 40f), ("SIDE", 90f), ("BACK", 180f) };
            var shots = new List<(string path, string label, float yaw)>();
            foreach (var d in ducks)
                foreach (var v in views)
                    shots.Add((d.path, Path.GetFileNameWithoutExtension(d.path) + "  " + v.Item1, v.Item2));
            if (shots.Count > 0 && RenderSheet(shots, views.Length, 480, "MICRODUCK  DESIGN SHEET", "Ducks", SheetDir + "/duck_design_sheet.png")) written++;

            foreach (var (cat, cols, cell) in new[] { ("Toys", 6, 360), ("Furniture", 6, 360), ("Clutter", 10, 256) })
            {
                var items = all.Where(x => x.cat == cat).Select(x => (x.path, Path.GetFileNameWithoutExtension(x.path), 0f)).ToList();
                if (items.Count == 0) continue;
                if (RenderSheet(items, cols, cell, $"MICRODUCK LAB  {cat.ToUpperInvariant()}  ({items.Count})", cat, $"{SheetDir}/{cat.ToLowerInvariant()}_sheet.png")) written++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Microduck] Make Design Sheets: {written} sheets written to {SheetDir}.");
        }

        /// <summary>yaw = 0 is the 3/4 catalog view for items; for the duck it is the angle around the duck from its front.</summary>
        static bool RenderSheet(List<(string path, string label, float yaw)> shots, int cols, int cell, string title, string cat, string outPath)
        {
            const int TitleH = 72;
            int labelH = cell >= 360 ? 36 : LabelH;
            int labelScale = cell >= 360 ? 3 : 2;
            bool turnaround = cat == "Ducks";
            int rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cell, TitleH + rows * (cell + labelH), TextureFormat.RGB24, false);
            var px = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 247, 232, 255);
            sheet.SetPixels32(px);
            var temp = OpenTemp();
            var lightsOff = new List<Light>();
            RenderTexture rt = null;
            bool ok = false;
            try
            {
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var sc = SceneManager.GetSceneAt(s);
                    if (!sc.isLoaded || sc == temp.scene) continue;
                    foreach (var g in sc.GetRootGameObjects())
                        foreach (var l in g.GetComponentsInChildren<Light>()) if (l.enabled) { l.enabled = false; lightsOff.Add(l); }
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.74f, 0.80f, 0.98f);
                RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
                RenderSettings.ambientGroundColor = new Color(0.82f, 0.64f, 0.48f);
                RenderSettings.fog = false;
                var sun = new GameObject("sheet sun").AddComponent<Light>();
                sun.transform.SetParent(temp.root, false);
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard; sun.color = new Color(1f, 0.96f, 0.88f);
                sun.cullingMask = 1 << CatalogLayer;
                var camGo = new GameObject("sheet camera");
                camGo.transform.SetParent(temp.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 26f; cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << CatalogLayer;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                rt = new RenderTexture(cell, cell, 24) { antiAliasing = 8 };
                cam.targetTexture = rt;

                for (int i = 0; i < shots.Count; i++)
                {
                    var (path, label, yaw) = shots[i];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var inst = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, temp.scene) as GameObject : null;
                    if (inst == null) { Debug.LogWarning("[Microduck] design sheet: could not place " + path); continue; }
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CatalogLayer;
                    var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                    foreach (var r in inst.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }

                    Vector3 dir;
                    if (turnaround)
                    {
                        float a = (yaw + DuckFrontYaw) * Mathf.Deg2Rad;
                        dir = new Vector3(Mathf.Sin(a), 0.22f, Mathf.Cos(a)).normalized;
                        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(Mathf.Sin(a + 0.8f), 1.1f, Mathf.Cos(a + 0.8f)));
                    }
                    else
                    {
                        dir = new Vector3(0.55f, 0.5f, 1f).normalized;
                        sun.transform.rotation = Quaternion.Euler(45f, 200f, 0f);
                    }
                    float radius = Mathf.Max(b.extents.magnitude, 0.005f);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (turnaround ? 0.92f : 1.05f);
                    cam.transform.position = b.center + dir * dist;
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.0005f, dist - radius * 1.5f);
                    cam.farClipPlane = dist + radius * 3f;
                    cam.backgroundColor = CategoryColour(cat);
                    cam.Render();

                    int cx = (i % cols) * cell, cy = sheet.height - TitleH - (i / cols + 1) * (cell + labelH);
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, cell, cell), cx, cy + labelH, false);
                    RenderTexture.active = null;
                    FillRect(sheet, cx, cy, cell, labelH, new Color(1f, 0.97f, 0.91f));
                    FillRect(sheet, cx, cy + labelH - 2, cell, 2, ComicLook.Ink);
                    DrawText(sheet, cx + 8, cy + labelH - (labelH - 7 * labelScale) / 2, label, ComicLook.Ink, labelScale, cell - 16);
                    Object.DestroyImmediate(inst);
                }
                // title band and grid
                FillRect(sheet, 0, sheet.height - TitleH, sheet.width, TitleH, ComicLook.Hex("#FFD23F"));
                FillRect(sheet, 0, sheet.height - TitleH - 4, sheet.width, 4, ComicLook.Ink);
                DrawText(sheet, 20, sheet.height - 14, title, ComicLook.Ink, 6, sheet.width - 40);
                for (int c = 1; c < cols; c++) FillRect(sheet, c * cell - 1, 0, 2, sheet.height - TitleH, ComicLook.Ink);
                for (int r = 1; r < rows; r++) FillRect(sheet, 0, sheet.height - TitleH - r * (cell + labelH) - 1, sheet.width, 2, ComicLook.Ink);
                FillRect(sheet, 0, 0, sheet.width, 4, ComicLook.Ink); FillRect(sheet, 0, 0, 4, sheet.height, ComicLook.Ink);
                FillRect(sheet, sheet.width - 4, 0, 4, sheet.height, ComicLook.Ink); FillRect(sheet, 0, sheet.height - 4, sheet.width, 4, ComicLook.Ink);
                sheet.Apply();
                string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, sheet.EncodeToPNG());
                cam.targetTexture = null;
                ok = true;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                foreach (var l in lightsOff) if (l != null) l.enabled = true;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                Object.DestroyImmediate(sheet);
                CloseTemp(temp);
            }
            return ok;
        }

        /// <summary>World yaw (degrees) the camera sits at to see the duck prefab's face.</summary>
        const float DuckFrontYaw = 0f;

        static Color CategoryColour(string cat) => cat switch
        {
            "Toys" => ComicLook.Hex("#FFF0C8"),
            "Furniture" => ComicLook.Hex("#DDEBFF"),
            "Clutter" => ComicLook.Hex("#E3F4DC"),
            _ => ComicLook.Hex("#F8E1EC"),
        };

        static string PhysicsSummary(GameObject go)
        {
            string layerName = LayerMask.LayerToName(go.layer);
            var parts = new List<string> { string.IsNullOrEmpty(layerName) ? "layer " + go.layer : layerName };
            var rb = go.GetComponent<Rigidbody>();
            parts.Add(rb == null ? "static" : rb.isKinematic ? "kinematic body" : $"rigidbody {rb.mass * 1000f:0.#} g");
            if (go.GetComponent<Grabbable>() != null) parts.Add("grabbable");
            var cols = go.GetComponentsInChildren<Collider>(true);
            if (cols.Length > 0) parts.Add(string.Join("+", cols.Select(c => c.GetType().Name.Replace("Collider", "")).Distinct()) + " collider" + (cols.Length > 1 ? "s" : ""));
            if (go.GetComponentInChildren<SurfaceTag>(true) != null) parts.Add("surface tag");
            return string.Join(", ", parts);
        }

        static void WriteDocs(List<(string cat, string name, string path, Vector3 size, string physics)> info)
        {
            var sb = new StringBuilder();
            sb.Append(DocsHeader);
            sb.AppendLine($"The list below was written by **Microduck > Assets > Make Catalog** on {DateTime.Now:yyyy-MM-dd}: {info.Count} prefabs. The design sheets (one per category) are in `docs/images/` (**Make Design Sheets**); the all-in-one preview sheet is `Logs/assets/catalog.png` (local, not in git).");
            sb.AppendLine();
            foreach (var cat in Categories)
            {
                var rows = info.Where(x => x.cat == cat).ToList();
                if (rows.Count == 0) continue;
                bool nc = cat == "Ducks";
                sb.AppendLine($"## {cat} ({rows.Count})");
                sb.AppendLine();
                if (nc) sb.AppendLine("**Non-commercial only:** every duck is adapted from Pollen Robotics' Microduck (CC BY-NC-SA 4.0).\n");
                sb.AppendLine("| Item | Size (cm, W × H × D) | Physics | Licence |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var r in rows)
                    sb.AppendLine($"| `{r.path.Substring(Root.Length + 1)}` | {r.size.x * 100f:0.#} × {r.size.y * 100f:0.#} × {r.size.z * 100f:0.#} | {r.physics} | {(nc ? "CC BY-NC-SA 4.0, **non-commercial**" : "MIT")} |");
                sb.AppendLine();
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LiveDir + "/Duck (live).prefab") != null)
            {
                sb.AppendLine("## Ducks/Live (in this project only, not in the package)");
                sb.AppendLine();
                sb.AppendLine("| Item | What it is | Licence |");
                sb.AppendLine("|---|---|---|");
                sb.AppendLine("| `Ducks/Live/Duck (live).prefab` | The game's runtime duck (DuckBody, DuckSetup, WanderDuck). It builds its model from `Models/microduck_rig.glb` when Play starts, so it shows nothing in edit mode. Needs a floor collider, ideally on the World layer (6); it wanders inside x, z in [-1, 1]. | CC BY-NC-SA 4.0, **non-commercial** |");
                sb.AppendLine();
            }
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DocsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString().Replace("\r\n", "\n"));
        }

        /// <summary>Kept in step with the hand-written top of docs/ASSETS.md.</summary>
        const string DocsHeader =
@"# Microduck Lab items

Every toy, piece of furniture, bit of household clutter and the duck itself, as separate Unity prefabs under
`Assets/MicroduckVR/Prefabs/`: drag one into a scene (or another project), share them as a `.unitypackage`, or edit
one item on its own.

## Licences

| What | Licence | Commercial use |
|---|---|---|
| `Ducks/` (Duck, Duck graphite, Duck lavender, Duck sky, Duck (live)) and their meshes and materials | [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), adapted from the Microduck robot by [Pollen Robotics](https://github.com/pollen-robotics/microduck_rl) | **No.** Non-commercial only; credit Pollen Robotics; share changes under the same licence |
| Everything else (toys, furniture, clutter, their meshes, materials, shaders, textures and the two scripts) | MIT, (c) 2026 Yutong Jin (see `LICENSE`) | Yes |

The toys that the web game took from glTF sample models (toy car, chess pieces, cookie, toast, croissant) are rebuilt
from simple shapes in code here; no third-party model or font file is in the prefabs or the package. The package
carries all of this in `Assets/MicroduckVR/Prefabs/CREDITS.txt`.

## Making and refreshing the items (Microduck > Assets)

1. Open `Assets/MicroduckVR/Scenes/House.unity` and run **Export Prefabs**. One prefab per item in
   `Prefabs/{Toys,Furniture,Clutter,Ducks}`, its meshes in `Prefabs/Meshes/<category>/<item>.asset`, materials made
   at runtime in `Prefabs/Materials/`; the house's own materials stay in `Materials/House`.
2. **Make Catalog** draws every prefab to `Logs/assets/catalog.png` and rewrites this file; **Make Design Sheets**
   draws the duck turnaround and one labelled sheet per category to `docs/images/`.
3. **Export Package** writes `Builds/MicroduckLab_Assets.unitypackage`.
4. Optional: **Link Scene To Prefabs** swaps the house's furniture and clutter for instances of the prefabs, so
   editing a prefab changes the house. **Build House** makes the house again from code and drops those links; tick
   **Relink House After Build** to link the new house to the prefabs straight after each build.

How items are picked: each named prop the house builder makes (a root with parts, such as `Bookcase`, `Teddy`,
`Mug`), furniture the builder lays out as separate pieces (`Table` = top + 4 legs, `Kitchen chair`, `Bed`, `Kitchen
units`, ...), a few single pieces (`Fridge`, `Shelf`, `Pouf`, ...) and every toy in the toy bag. Walls, floors,
ramps, the pitch, the garden ground and the scenery outside stay part of the house. Identical copies share one
prefab; an item that comes in many sizes or colours (books, blocks, flowers) keeps up to 8 versions (`Book`,
`Book 2`, ...). Pivots sit at the bottom centre, at the floor contact, and items are unrotated.

Using them elsewhere: Unity 6 with URP. The prefabs keep the game's layers by number (World 6, Duck 7, Toy 8,
Prop 10): name those layers in the other project or change them. For the ink outlines add a Full Screen Pass
Renderer Feature using `Materials/ComicOutline.mat` (Depth and Normal inputs) to the URP renderer.

";

        // ------------------------------------------------------------------ a tiny 5 x 7 font for the labels
        static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            { 'A', new byte[] { 0x7C, 0x12, 0x11, 0x12, 0x7C } }, { 'B', new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 } },
            { 'C', new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 } }, { 'D', new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C } },
            { 'E', new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 } }, { 'F', new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 } },
            { 'G', new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A } }, { 'H', new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F } },
            { 'I', new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 } }, { 'J', new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 } },
            { 'K', new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 } }, { 'L', new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 } },
            { 'M', new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F } }, { 'N', new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F } },
            { 'O', new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E } }, { 'P', new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 } },
            { 'Q', new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E } }, { 'R', new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 } },
            { 'S', new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 } }, { 'T', new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 } },
            { 'U', new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F } }, { 'V', new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F } },
            { 'W', new byte[] { 0x3F, 0x40, 0x38, 0x40, 0x3F } }, { 'X', new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 } },
            { 'Y', new byte[] { 0x07, 0x08, 0x70, 0x08, 0x07 } }, { 'Z', new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 } },
            { '0', new byte[] { 0x3E, 0x51, 0x49, 0x45, 0x3E } }, { '1', new byte[] { 0x00, 0x42, 0x7F, 0x40, 0x00 } },
            { '2', new byte[] { 0x42, 0x61, 0x51, 0x49, 0x46 } }, { '3', new byte[] { 0x21, 0x41, 0x45, 0x4B, 0x31 } },
            { '4', new byte[] { 0x18, 0x14, 0x12, 0x7F, 0x10 } }, { '5', new byte[] { 0x27, 0x45, 0x45, 0x45, 0x39 } },
            { '6', new byte[] { 0x3C, 0x4A, 0x49, 0x49, 0x30 } }, { '7', new byte[] { 0x01, 0x71, 0x09, 0x05, 0x03 } },
            { '8', new byte[] { 0x36, 0x49, 0x49, 0x49, 0x36 } }, { '9', new byte[] { 0x06, 0x49, 0x49, 0x29, 0x1E } },
            { '-', new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 } }, { '(', new byte[] { 0x00, 0x1C, 0x22, 0x41, 0x00 } },
            { ')', new byte[] { 0x00, 0x41, 0x22, 0x1C, 0x00 } }, { '.', new byte[] { 0x00, 0x60, 0x60, 0x00, 0x00 } },
            { ',', new byte[] { 0x00, 0x50, 0x30, 0x00, 0x00 } }, { '/', new byte[] { 0x20, 0x10, 0x08, 0x04, 0x02 } },
            { '_', new byte[] { 0x40, 0x40, 0x40, 0x40, 0x40 } }, { '\'', new byte[] { 0x00, 0x05, 0x03, 0x00, 0x00 } },
        };

        /// <summary>Text with its top-left corner at (x, top), y up as in the texture; clipped to maxW pixels.</summary>
        static void DrawText(Texture2D tex, int x, int top, string s, Color c, int scale, int maxW)
        {
            int x0 = x;
            foreach (char raw in s)
            {
                if (x + 5 * scale > x0 + maxW) break;
                char ch = char.ToUpperInvariant(raw);
                if (Glyphs.TryGetValue(ch, out var g))
                    for (int col = 0; col < 5; col++)
                        for (int row = 0; row < 7; row++)
                            if (((g[col] >> row) & 1) != 0) FillRect(tex, x + col * scale, top - (row + 1) * scale, scale, scale, c);
                x += 6 * scale;
            }
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int j = Mathf.Max(0, y); j < Mathf.Min(tex.height, y + h); j++)
                for (int i = Mathf.Max(0, x); i < Mathf.Min(tex.width, x + w); i++) tex.SetPixel(i, j, c);
        }

        /// <summary>(category, path) of every prefab, categories in order, names sorted; Ducks/Live only when asked.</summary>
        static List<(string cat, string path)> ListPrefabs(bool includeLive)
        {
            var list = new List<(string, string)>();
            foreach (var cat in Categories)
            {
                string dir = $"{Root}/{cat}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => (includeLive || !p.StartsWith(LiveDir + "/")) && !p.StartsWith(AnimDir + "/")).Distinct().OrderBy(p => p, StringComparer.Ordinal);
                foreach (var p in paths) list.Add((cat, p));
            }
            return list;
        }

        // ================================================================== 3. Export Package
        [MenuItem("Microduck/Assets/Export Package")]
        public static void ExportPackage()
        {
            var roots = ListPrefabs(false).Select(x => x.path).ToList();
            if (roots.Count == 0) { Debug.LogError("[Microduck] Export Package: no prefabs under " + Root + " (run Export Prefabs first)"); return; }
            int warnings = 0;
            if (File.Exists(CreditsPath)) roots.Add(CreditsPath); else { Debug.LogWarning("[Microduck] Export Package: " + CreditsPath + " is missing; the package has no credits file"); warnings++; }
            foreach (var p in new[] { OutlineShaderPath, ComicSetup.OutlineMatPath }) if (File.Exists(p)) roots.Add(p);
            if (AssetDatabase.IsValidFolder(AnimDir))                                   // the duck's moves: clips, controller, animated duck
                roots.AddRange(AssetDatabase.FindAssets("", new[] { AnimDir }).Select(AssetDatabase.GUIDToAssetPath).Where(x => !AssetDatabase.IsValidFolder(x)));

            var keep = new SortedSet<string>(StringComparer.Ordinal);
            int left = 0;
            foreach (var d in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!d.StartsWith("Assets/")) continue;                                   // packages and Unity's built-in assets
                if (d.EndsWith(".unity") || d.Contains("/Editor/") || d.StartsWith("Assets/MicroduckVR/Settings/") || d.StartsWith("Assets/MicroduckVR/Scenes/")
                    || d == IndexPath || d.StartsWith(LiveDir + "/")) { left++; continue; }
                if (d.EndsWith(".cs") && !PackageScripts.Contains(d))
                {
                    Debug.LogWarning("[Microduck] Export Package: left out " + d + " (it needs other game scripts); items using it would show a missing script"); warnings++; left++; continue;
                }
                if (d.EndsWith(".glb") || d.EndsWith(".gltf"))
                {
                    Debug.LogWarning("[Microduck] Export Package: left out " + d + " (needs glTFast); run Export Prefabs again so the meshes are copied"); warnings++; left++; continue;
                }
                keep.Add(d);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PackagePath));
            AssetDatabase.ExportPackage(keep.ToArray(), PackagePath, ExportPackageOptions.Default);

            int N(Func<string, bool> f) => keep.Count(f);
            long bytes = File.Exists(PackagePath) ? new FileInfo(PackagePath).Length : 0;
            Debug.Log($"[Microduck] Export Package: {PackagePath} ({bytes / 1024} KB), {keep.Count} files: " +
                      $"{N(p => p.EndsWith(".prefab"))} prefabs, {N(p => p.EndsWith(".asset"))} mesh assets, {N(p => p.EndsWith(".mat"))} materials, " +
                      $"{N(p => p.EndsWith(".physicMaterial") || p.EndsWith(".physicsMaterial"))} physics materials, {N(p => p.EndsWith(".shader") || p.EndsWith(".hlsl"))} shaders, " +
                      $"{N(p => p.EndsWith(".png") || p.EndsWith(".jpg"))} textures, {N(p => p.EndsWith(".ttf") || p.EndsWith(".otf"))} fonts, {N(p => p.EndsWith(".cs"))} scripts, " +
                      $"credits {(keep.Contains(CreditsPath) ? "yes" : "NO")}; {left} dependencies left out, {warnings} warnings.");
        }

        // ================================================================== 4. Link Scene To Prefabs
        [MenuItem("Microduck/Assets/Link Scene To Prefabs")]
        public static void LinkSceneToPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Link Scene To Prefabs: leave Play mode first"); return; }
            var house = EnsureHouseScene();
            if (house.IsValid()) Link(house, true);
        }

        public static bool RelinkAfterBuild => EditorPrefs.GetBool(RelinkPref, false);

        [MenuItem(RelinkMenu)]
        public static void ToggleRelinkAfterBuild()
        {
            EditorPrefs.SetBool(RelinkPref, !RelinkAfterBuild);
            Debug.Log("[Microduck] Relink House After Build: " + (RelinkAfterBuild ? "on" : "off"));
        }

        [MenuItem(RelinkMenu, true)]
        static bool ToggleRelinkAfterBuildValidate() { Menu.SetChecked(RelinkMenu, RelinkAfterBuild); return true; }

        /// <summary>Called by Build House after it saved the scene, when Relink House After Build is on.</summary>
        internal static void RelinkBuiltHouse(Scene scene)
        {
            if (!File.Exists(IndexPath)) { Debug.Log("[Microduck] Relink after build: no prefab index yet (run Export Prefabs), house left as built"); return; }
            Link(scene, true);
        }

        /// <summary>
        /// Replaces each house item that matches an exported prefab (same signature) by an instance of it at the same
        /// world place, same name, parent and sibling order (the network code finds scene props by that path).
        /// Items another scene component points at (the toy train's cars) are left alone. Undo-able, then saved.
        /// </summary>
        static void Link(Scene house, bool save)
        {
            var index = LoadIndex();
            if (index.items.Count == 0) { Debug.LogError("[Microduck] Link Scene To Prefabs: no prefab index at " + IndexPath + " (run Export Prefabs first)"); return; }
            var houseRoot = FindHouseRoot(house);
            if (houseRoot == null) { Debug.LogError("[Microduck] Link Scene To Prefabs: no 'House' object in " + house.path); return; }
            int skipped = 0, already = 0, unmatched = 0, referenced = 0, linked = 0, warnings = 0;
            var items = CollectHouseItems(houseRoot, ref skipped);
            var refs = ReferencedFrom(house);
            var plan = new List<(Item it, IndexEntry e, Vector3 pos, Quaternion rot, bool resetScale)>();
            var keptNames = new List<string>();

            var temp = OpenTemp();
            try
            {
                foreach (var it in items)
                {
                    if (it.linkedPrefab != null || it.pieces.Any(p => PrefabUtility.IsPartOfPrefabInstance(p.gameObject))) { already++; continue; }
                    if (PointedAtFromOutside(it, refs)) { referenced++; keptNames.Add(it.name); continue; }
                    var c = BuildCandidate(it, temp.root);
                    var e = index.items.FirstOrDefault(x => x.sig == c.sig);
                    Object.DestroyImmediate(c.go);
                    if (e == null) { unmatched++; continue; }
                    plan.Add((it, e, c.pivotPos, c.pivotRot, c.resetScale));
                }
            }
            finally { CloseTemp(temp); }

            Undo.SetCurrentGroupName("Link Scene To Prefabs");
            int group = Undo.GetCurrentGroup();
            foreach (var (it, e, pos, rot, resetScale) in plan)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.prefab);
                if (prefab == null) { Debug.LogWarning("[Microduck] Link: prefab missing " + e.prefab); warnings++; continue; }
                var first = it.pieces[0];
                var parent = first.parent; int sibling = first.GetSiblingIndex();
                var inst = PrefabUtility.InstantiatePrefab(prefab, house) as GameObject;
                if (inst == null) { Debug.LogWarning("[Microduck] Link: could not place " + e.prefab); warnings++; continue; }
                Undo.RegisterCreatedObjectUndo(inst, "Link Scene To Prefabs");
                inst.transform.SetParent(parent, false);
                inst.transform.SetPositionAndRotation(pos, rot);
                if (!it.wrap && !resetScale) inst.transform.localScale = first.localScale;
                inst.name = it.wrap ? it.name : first.name;
                foreach (var p in it.pieces) Undo.DestroyObjectImmediate(p.gameObject);
                inst.transform.SetSiblingIndex(sibling);
                linked++;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(house);
            if (save && plan.Count > 0) EditorSceneManager.SaveScene(house);
            if (keptNames.Count > 0) Debug.Log("[Microduck] Link: left as built (other scene objects point at them): " + string.Join(", ", keptNames.Distinct()));
            Debug.Log($"[Microduck] Link Scene To Prefabs: {linked} items now prefab instances, {already} were already, {unmatched} with no matching prefab " +
                      $"(variants over the cap, or changed since the export), {referenced} left because other objects point at them, {warnings} warnings" +
                      (save && plan.Count > 0 ? "; scene saved." : "."));
        }

        /// <summary>GameObjects that a component in the scene points at, with the GameObjects of those components.</summary>
        static Dictionary<GameObject, List<GameObject>> ReferencedFrom(Scene s)
        {
            var refs = new Dictionary<GameObject, List<GameObject>>();
            foreach (var root in s.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                        GameObject g = null;
                        if (p.objectReferenceValue is GameObject go) g = go;
                        else if (p.objectReferenceValue is Component c) g = c.gameObject;
                        if (g == null || g.scene != s) continue;
                        if (!refs.TryGetValue(g, out var owners)) refs[g] = owners = new List<GameObject>();
                        owners.Add(mb.gameObject);
                    }
                }
            return refs;
        }

        static bool PointedAtFromOutside(Item it, Dictionary<GameObject, List<GameObject>> refs)
        {
            bool Inside(GameObject g) { foreach (var p in it.pieces) if (g.transform.IsChildOf(p)) return true; return false; }
            foreach (var p in it.pieces)
                foreach (var t in p.GetComponentsInChildren<Transform>(true))
                    if (refs.TryGetValue(t.gameObject, out var owners) && owners.Any(o => !Inside(o))) return true;
            return false;
        }

        // ================================================================== helpers
        class Temp { public Scene scene, prev; public Transform root; }

        /// <summary>An empty additive scene, made active, where copies are built and shot; the open scenes stay untouched.</summary>
        static Temp OpenTemp()
        {
            var prev = SceneManager.GetActiveScene();
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(s);
            var root = new GameObject("asset export").transform;
            return new Temp { scene = s, prev = prev, root = root };
        }

        static void CloseTemp(Temp t)
        {
            if (t == null) return;
            if (t.prev.IsValid() && t.prev.isLoaded) SceneManager.SetActiveScene(t.prev);
            if (t.scene.IsValid()) EditorSceneManager.CloseScene(t.scene, true);
        }

        /// <summary>The House scene, opened if needed (only when the open scene has no unsaved changes).</summary>
        static Scene EnsureHouseScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == SceneBuilderHouse.ScenePath && s.isLoaded) { SceneManager.SetActiveScene(s); return s; }
            }
            if (!File.Exists(SceneBuilderHouse.ScenePath)) { Debug.LogError("[Microduck] no " + SceneBuilderHouse.ScenePath + " (run Microduck/Scenes/Build House first)"); return default; }
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) { Debug.LogError("[Microduck] the open scene has unsaved changes: save it or open " + SceneBuilderHouse.ScenePath + " first"); return default; }
            return EditorSceneManager.OpenScene(SceneBuilderHouse.ScenePath, OpenSceneMode.Single);
        }

        static IndexFile LoadIndex()
        {
            IndexFile f = null;
            try { if (File.Exists(IndexPath)) f = JsonUtility.FromJson<IndexFile>(File.ReadAllText(IndexPath)); }
            catch (Exception ex) { Warn("could not read " + IndexPath + ": " + ex.Message); }
            f ??= new IndexFile();
            f.items ??= new List<IndexEntry>();
            f.items.RemoveAll(e => e == null || string.IsNullOrEmpty(e.sig) || !File.Exists(e.prefab));   // a deleted prefab frees its slot
            return f;
        }

        static void SaveIndex(IndexFile f)
        {
            EnsureFolder(Root);
            f.items = f.items.OrderBy(e => e.prefab, StringComparer.Ordinal).ToList();
            File.WriteAllText(IndexPath, JsonUtility.ToJson(f, true));
            AssetDatabase.ImportAsset(IndexPath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s) if (ch != '#') sb.Append(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '(' || ch == ')' ? ch : '_');
            return sb.ToString().Trim();
        }
    }
}
