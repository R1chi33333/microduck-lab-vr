using System.Collections.Generic;
using System.IO;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Builds the whole Microduck house (port of world.js + house.js): living room with the oak table,
    /// cardboard switchback ramp, sofa, coffee table, shelf, plant, pouf, rug with the football pitch;
    /// hallway drag strip; kitchen (slippery tiles); kids' bedroom (bouncy bed, block ramp); garden
    /// (grass, stepping stones, puddle, slope, fence, pots). Web coordinates are written as-is and
    /// mirrored on X here; web floor y = -0.75 becomes Unity y = 0 (so web tabletop y = 0 is 0.75 here).
    /// </summary>
    public static partial class HouseBuilder
    {
        const string MatDir = "Assets/MicroduckVR/Materials/House";
        const string MeshDir = "Assets/MicroduckVR/Meshes/House";
        const float WH = 2.5f, WT = 0.12f, DOOR_H = 2.0f, T_H = 0.75f;

        static Transform root;
        static readonly Dictionary<string, Transform> rooms = new Dictionary<string, Transform>();

        // web -> Unity: mirror X
        static Vector3 P(float x, float y, float z) => new Vector3(-x, y, z);

        public static Transform Build()
        {
            Directory.CreateDirectory(MatDir);
            if (Directory.Exists(MeshDir)) AssetDatabase.DeleteAsset(MeshDir);
            Directory.CreateDirectory(MeshDir); AssetDatabase.Refresh();
            savedMeshes.Clear(); seedCounter = 0;
            root = new GameObject("House").transform;
            rooms.Clear();
            foreach (var k in new[] { "living", "hallway", "kitchen", "bedroom", "garden" })
            {
                var g = new GameObject("Room " + k).transform; g.SetParent(root, false); rooms[k] = g;
            }
            Shell(); Living(); Hallway(); Kitchen(); Bedroom(); Garden();
            Clutter();
            Scenery();
            Grass();
            return root;
        }

        // ================================================================ materials
        static Material M(string name, string hex)
        {
            string p = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Microduck/ToonLit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
            m.shader = shader; m.SetColor("_BaseColor", ComicLook.Hex(hex)); m.enableInstancing = true;
            ApplyToonDefaults(m, Glossy.Contains(name) ? 0.8f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // glossy things get the web's hard specular pop (toonify spec for roughness < 0.5); the rest stay matte
        static readonly HashSet<string> Glossy = new HashSet<string> { "bowl", "fridge", "knob", "sink", "pot", "puddle", "puddle_rim", "toy_box", "toy_lid", "turntable", "finish_blue", "block0", "block1", "block2", "block3", "block_ramp", "shoe" };

        /// <summary>
        /// The comic print defaults on a saved toon material (rim band, spec pop, halftone / hatching, fill,
        /// floor contact). Saved materials keep old values for properties they already had, so the builders set
        /// them explicitly.
        /// </summary>
        internal static void ApplyToonDefaults(Material m, float spec)
        {
            m.SetFloat("_RimAmount", ComicLook.DefaultRim); m.SetFloat("_RimT", ComicLook.DefaultRimT);
            m.SetFloat("_SpecAmount", spec); m.SetFloat("_SpecT", 0.55f);
            m.SetFloat("_Halftone", 1f); m.SetFloat("_Hatch", 1f);
            m.SetFloat("_FillAmount", 0.6f); m.SetFloat("_ContactAO", 0.45f); m.SetFloat("_ContactHeight", 0.1f);
        }

        // ================================================================ primitives
        static int seedCounter;
        /// <summary>Hand-drawn comic style (style B): rounded, slightly wobbly and bulging, scaled to the part's size.</summary>
        static ComicMesh.Style HandDrawn(Vector3 size)
        {
            float m = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            return new ComicMesh.Style
            {
                radius = Mathf.Min(0.035f, m * 0.3f), segments = 3,
                puff = 0.05f, wobble = Mathf.Min(0.016f, m * 0.08f + Mathf.Max(size.x, size.z) * 0.004f),
                skew = Mathf.Min(0.012f, size.y * 0.02f), taperTop = size.y > 0.3f ? 0.03f : 0f, seed = ++seedCounter,
            };
        }

        static GameObject Box(string room, string name, Vector3 center, Vector3 size, Material mat, float yawDegWeb = 0f, string surface = null, bool collide = true, bool styled = true)
        {
            GameObject go;
            if (styled && Mathf.Min(size.x, Mathf.Min(size.y, size.z)) > 0.012f)
            {
                go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(ComicMesh.RoundedBox(size, HandDrawn(size)));
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                if (collide) go.AddComponent<BoxCollider>().size = size;
                go.transform.SetParent(rooms[room], false);
                go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, -yawDegWeb, 0f));
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(rooms[room], false);
                go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, -yawDegWeb, 0f));
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                if (!collide) Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            go.layer = DuckSetup.LayerWorld; go.isStatic = true;
            if (collide && surface != null) Tag(go, surface, room);
            return go;
        }

        static readonly HashSet<Mesh> savedMeshes = new HashSet<Mesh>();
        internal static Mesh SaveMesh(Mesh m)
        {
            if (savedMeshes.Contains(m) || AssetDatabase.Contains(m)) return m;
            AssetDatabase.CreateAsset(m, $"{MeshDir}/{savedMeshes.Count:D4}.asset");
            savedMeshes.Add(m);
            return m;
        }

        // box from web centre + web size
        static GameObject B(string room, string name, float w, float h, float d, Material mat, float x, float y, float z, float ryRad = 0f, string surface = null, bool collide = true, bool styled = true)
            => Box(room, name, P(x, y, z), new Vector3(w, h, d), mat, ryRad * Mathf.Rad2Deg, surface, collide, styled);
        // straight architecture (walls, floors, thin decals): no hand-drawn warp
        static GameObject S(string room, string name, float w, float h, float d, Material mat, float x, float y, float z, float ryRad = 0f, string surface = null, bool collide = true)
            => Box(room, name, P(x, y, z), new Vector3(w, h, d), mat, ryRad * Mathf.Rad2Deg, surface, collide, false);

        static GameObject Cyl(string room, string name, float r, float h, Material mat, float x, float y, float z, string surface = null, bool collide = true)
        {
            var go = new GameObject(name) { layer = DuckSetup.LayerWorld, isStatic = true };
            go.transform.SetParent(rooms[room], false);
            go.transform.position = P(x, y, z);
            bool flat = h < r * 0.3f;                                    // rugs, stones, puddles: straight disc
            var mesh = SaveMesh(ComicMesh.Leg(h, flat ? r : r * 0.82f, r, flat ? 28 : 14));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collide) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true; if (surface != null) Tag(go, surface, room); }
            return go;
        }

        /// <summary>Sloped block along web X: from xa (top height ha) to xb (top height hb), centred on z, width w, floor at y0.</summary>
        static GameObject Wedge(string room, string name, float xa, float ha, float xb, float hb, float zc, float w, Material mat, float y0 = 0f, string surface = "card")
        {
            var go = new GameObject(name) { layer = DuckSetup.LayerWorld, isStatic = true };
            go.transform.SetParent(rooms[room], false);
            var v = new List<Vector3>();
            foreach (var z in new[] { zc - w / 2f, zc + w / 2f })
            {
                v.Add(P(xa, y0, z)); v.Add(P(xb, y0, z)); v.Add(P(xb, y0 + hb, z)); v.Add(P(xa, y0 + ha, z));
            }
            // faces (quads as two tris); build flat-shaded by duplicating vertices per face
            int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 } };
            var verts = new List<Vector3>(); var tris = new List<int>();
            foreach (var f in faces)
            {
                int b = verts.Count; foreach (var i in f) verts.Add(v[i]);
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            // mirroring flips winding: fix by checking a face normal against the expected outward direction
            mesh.RecalculateNormals();
            var centre = Vector3.zero; foreach (var p in v) centre += p; centre /= v.Count;
            if (Vector3.Dot(mesh.normals[0], verts[0] - centre) < 0f) { for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, $"{MatDir}/{name}_mesh.asset");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
            Tag(go, surface, room);
            return go;
        }

        static GameObject Hidden(string room, string name, float w, float h, float d, float x, float y, float z, string surface = null)
        {
            var go = S(room, name, w, h, d, null, x, y, z, 0f, surface);
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            return go;
        }

        static void Tag(GameObject go, string type, string room)
        {
            var t = go.AddComponent<SurfaceTag>(); t.type = type; t.room = room;
        }
    }
}
