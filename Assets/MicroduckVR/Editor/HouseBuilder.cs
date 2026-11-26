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

        // ================================================================ shell: floors and walls
        static Material wall, trim, plank;
        static void Shell()
        {
            wall = M("wall", "#FFF0D2"); trim = M("trim", "#FFFFFF"); plank = M("floor_planks", "#E9A85A");
            // walls: keep the shade side a pale lavender cream instead of a muddy grey
            wall.SetFloat("_MidMul", 0.95f); wall.SetFloat("_ShadowMul", 0.86f); wall.SetFloat("_DeepMul", 0.8f); wall.SetFloat("_TintAmount", 0.3f);
            // big plaster planes: lighter hatching, so a wall turned away from the sun reads as shade, not texture
            wall.SetFloat("_Hatch", 0.55f); trim.SetFloat("_Hatch", 0.55f);
            // floors per room (2 cm slabs, top at y = 0)
            Textured(plank, "floor_planks.png", new Vector2(6.0f, 5.0f), 0.6f);
            var hallM = Textured(M("floor_hall", "#E19B4C"), "floor_planks_narrow.png", new Vector2(14.24f, 1.44f), 0.6f);
            var tileM = Textured(M("kitchen_tile", "#BFE6DE"), "floor_tiles.png", new Vector2(4.0f, 5.12f), 0.6f);
            var bedM = Textured(M("bedroom_floor", "#F2DCF2"), "floor_planks_narrow.png", new Vector2(4.0f, 5.12f), 0.6f);
            S("living", "Floor living", 6.0f, 0.02f, 5.0f, plank, 0f, -0.01f, 0f, 0f, "wood");
            S("hallway", "Floor hallway", 14.24f, 0.02f, 1.44f, hallM, 0f, -0.01f, 3.22f, 0f, "wood");
            S("kitchen", "Floor kitchen", 3.88f + 0.12f, 0.02f, 5.0f + 0.12f, tileM, 5.06f, -0.01f, -0.06f, 0f, "tile");
            S("bedroom", "Floor bedroom", 3.88f + 0.12f, 0.02f, 5.0f + 0.12f, bedM, -5.06f, -0.01f, -0.06f, 0f, "wood");
            S("garden", "Lawn", 60f, 0.02f, 44f, GrassMat("lawn_soil", true), 0f, -0.03f, 10f, 0f, "grass");
            // ceilings over the rooms (no shadows, so the rooms stay sunlit; hidden from the debug top views)
            var ceil = M("ceiling", "#FFF6E4"); ceil.SetFloat("_Unlit", 1f);
            foreach (var (room, cx, cz, w, d) in new[] { ("living", 0f, 0f, 6.24f, 5.24f), ("kitchen", 5.06f, -0.06f, 4.12f, 5.24f), ("bedroom", -5.06f, -0.06f, 4.12f, 5.24f), ("hallway", 0f, 3.22f, 14.36f, 1.56f) })
            {
                var c = S(room, "Ceiling " + room, w, 0.03f, d, ceil, cx, WH + 0.015f, cz, 0f, null, false);
                c.layer = MicroduckVR.Duck.DuckSetup.LayerCeiling;
                c.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            S("garden", "Grass", 7.0f, 0.02f, 3.66f, GrassMat("lawn_soil", true), 0f, -0.009f, 5.77f, 0f, "grass");

            // living back wall with the window
            float bz = -2.5f - WT / 2f;
            var WIN = (x: 0.9f, w: 1.4f, y0: 0.85f, h: 1.25f);
            S("living", "Wall back L", WIN.x - WIN.w / 2f + 3f, WH, WT, wall, (-3f + WIN.x - WIN.w / 2f) / 2f, WH / 2f, bz);
            S("living", "Wall back R", 3f - (WIN.x + WIN.w / 2f), WH, WT, wall, (3f + WIN.x + WIN.w / 2f) / 2f, WH / 2f, bz);
            S("living", "Wall back sill", WIN.w, WIN.y0, WT, wall, WIN.x, WIN.y0 / 2f, bz);
            S("living", "Wall back top", WIN.w, WH - (WIN.y0 + WIN.h), WT, wall, WIN.x, (WIN.y0 + WIN.h + WH) / 2f, bz);
            B("living", "Window sill", WIN.w + 0.12f, 0.06f, 0.16f, trim, WIN.x, WIN.y0 - 0.02f, -2.48f);
            S("living", "Window view", WIN.w, WIN.h, 0.01f, M("window_view", "#9AD8F2"), WIN.x, WIN.y0 + WIN.h / 2f, bz - 0.04f, 0f, null, false);

            WallX("wall_hall_back", 2.56f, -7.12f, 7.12f, new[] { (-0.45f, 0.45f, DOOR_H), (-5.4f, -4.6f, DOOR_H), (4.6f, 5.4f, DOOR_H) }, "hallway");
            WallX("wall_hall_front", 3.88f, -7.12f, 7.12f, new[] { (-1.0f, 1.0f, 2.1f) }, "hallway");
            WallZ("wall_living_left", -3.06f, -2.62f, 2.5f, new[] { (-1.2f, -0.4f, DOOR_H) }, "living");
            WallZ("wall_living_right", 3.06f, -2.62f, 2.5f, new[] { (-1.6f, -0.8f, DOOR_H) }, "living");
            WallX("wall_kitchen_back", -2.56f, 3.0f, 7.12f, new (float, float, float)[0], "kitchen");
            WallX("wall_bedroom_back", -2.56f, -7.12f, -3.0f, new (float, float, float)[0], "bedroom");
            WallZ("wall_east", 7.06f, -2.62f, 3.94f, new (float, float, float)[0], "kitchen");
            WallZ("wall_west", -7.06f, -2.62f, 3.94f, new (float, float, float)[0], "bedroom");
        }

        static Material Textured(Material m, string tex, Vector2 size, float tile)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MicroduckVR/Textures/" + tex);
            if (t != null) { m.SetTexture("_BaseMap", t); m.SetTextureScale("_BaseMap", size / tile); EditorUtility.SetDirty(m); }
            return m;
        }

        static void WallX(string id, float z, float x0, float x1, (float a, float b, float h)[] gaps, string room)
        {
            float a = x0; int i = 0;
            System.Array.Sort(gaps, (p, q) => p.a.CompareTo(q.a));
            foreach (var g in gaps)
            {
                if (g.a - a > 0.001f) S(room, id + "_" + i++, g.a - a, WH, WT, wall, (a + g.a) / 2f, WH / 2f, z);
                S(room, id + "_lintel" + i, g.b - g.a, WH - g.h, WT, wall, (g.a + g.b) / 2f, g.h + (WH - g.h) / 2f, z);
                foreach (var s in new[] { -1f, 1f }) B(room, id + "_jamb", 0.05f, g.h, WT + 0.03f, trim, (s < 0 ? g.a : g.b) + s * 0.025f, g.h / 2f, z, 0f, null, false);
                B(room, id + "_head", g.b - g.a + 0.1f, 0.05f, WT + 0.03f, trim, (g.a + g.b) / 2f, g.h + 0.025f, z, 0f, null, false);
                a = g.b;
            }
            if (x1 - a > 0.001f) S(room, id + "_" + i, x1 - a, WH, WT, wall, (a + x1) / 2f, WH / 2f, z);
        }

        static void WallZ(string id, float x, float z0, float z1, (float a, float b, float h)[] gaps, string room)
        {
            float a = z0; int i = 0;
            foreach (var g in gaps)
            {
                if (g.a - a > 0.001f) S(room, id + "_" + i++, WT, WH, g.a - a, wall, x, WH / 2f, (a + g.a) / 2f);
                S(room, id + "_lintel" + i, WT, WH - g.h, g.b - g.a, wall, x, g.h + (WH - g.h) / 2f, (g.a + g.b) / 2f);
                foreach (var s in new[] { -1f, 1f }) B(room, id + "_jamb", WT + 0.03f, g.h, 0.05f, trim, x, g.h / 2f, (s < 0 ? g.a : g.b) + s * 0.025f, 0f, null, false);
                B(room, id + "_head", WT + 0.03f, 0.05f, g.b - g.a + 0.1f, trim, x, g.h + 0.025f, (g.a + g.b) / 2f, 0f, null, false);
                a = g.b;
            }
            if (z1 - a > 0.001f) S(room, id + "_" + i, WT, WH, z1 - a, wall, x, WH / 2f, (a + z1) / 2f);
        }

        // ================================================================ living room
        static void Living()
        {
            const string R = "living";
            var oak = M("table_oak", "#C98A55"); var sage = M("sage", "#8FD3B6");
            // oak table: top at 0.75
            B(R, "Tabletop", 1.6f, 0.04f, 1.0f, oak, 0f, T_H - 0.02f, 0f, 0f, "table");
            foreach (var lx in new[] { -0.73f, 0.73f }) foreach (var lz in new[] { -0.43f, 0.43f })
                B(R, "Table leg", 0.045f, T_H - 0.04f, 0.045f, oak, lx, (T_H - 0.04f) / 2f, lz);
            // tabletop platform + ramp + notebook step
            B(R, "Platform", 0.3f, 0.04f, 0.32f, sage, 0.48f, T_H + 0.02f, 0.12f, 0f, "table");
            Wedge(R, "Platform ramp", 0.48f - 0.15f - 0.26f, 0f, 0.48f - 0.15f, 0.04f, 0.12f, 0.16f, sage, T_H, "table");
            B(R, "Notebook", 0.21f, 0.016f, 0.15f, M("notebook", "#FF5D73"), -0.32f, T_H + 0.008f, 0.2f, 0.18f, "card");
            // cardboard switchback: floor -> landing box -> tabletop's right edge
            var card = M("cardboard", "#E3A866");
            const float z1 = -0.3f, z2 = 0.12f, w = 0.22f, xLand0 = 2.2f, xLand1 = 2.55f, xFoot = 0.95f, hLand = 0.375f;
            Wedge(R, "Ramp flight 1", xFoot, 0f, xLand0, hLand, z2, w, card);
            Wedge(R, "Ramp flight 2", xLand0, hLand, 0.8f, T_H, z1, w, card);
            float LZ0 = z1 - w / 2f - 0.012f, LZ1 = z2 + w / 2f + 0.012f;
            B(R, "Ramp landing", xLand1 - xLand0, hLand, LZ1 - LZ0, card, (xLand0 + xLand1) / 2f, hLand / 2f, (LZ0 + LZ1) / 2f, 0f, "card");
            B(R, "Ramp landing rail", 0.012f, 0.025f, LZ1 - LZ0, card, xLand1 - 0.006f, hLand + 0.0125f, (LZ0 + LZ1) / 2f);
            // side rails along both flights
            foreach (var (xa, ha, xb, hb, zc) in new[] { (xFoot, 0f, xLand0, hLand, z2), (xLand0, hLand, 0.8f, T_H, z1) })
                foreach (var s in new[] { -1f, 1f })
                    Wedge(R, "Ramp rail", xa, ha + 0.025f, xb, hb + 0.025f, zc + s * (w / 2f + 0.006f), 0.012f, card, 0f, "card");

            // sofa (teal), coffee table, shelf, plant, pouf
            var sofaM = M("sofa", "#27B3B0");
            var SF = (x: -1.66f, z: -2.5f + 0.54f, L: 2.19f, D: 1.02f, seat: 0.46f, back: 0.72f, arm: 0.6f);
            // visuals: the cushioned hand-drawn sofa and coffee table; colliders: simple invisible boxes
            FurnitureStyles.MeshSaver = SaveMesh; FurnitureStyles.MatFactory = (hex, key) => M(key, hex);
            FurnitureStyles.BuildSofa(rooms[R], P(SF.x, 0f, SF.z), FurnitureStyles.Kind.HandDrawn);
            FurnitureStyles.BuildCoffeeTable(rooms[R], P(-1.66f, 0f, -1.0f), FurnitureStyles.Kind.HandDrawn);
            FurnitureStyles.BuildArmchair(rooms[R], P(2.2f, 0f, -1.2f), FurnitureStyles.Kind.HandDrawn);
            FurnitureStyles.MeshSaver = m => m; FurnitureStyles.MatFactory = (hex, key) => ComicLook.ToonMaterial(ComicLook.Hex(hex), key);
            Hidden(R, "Sofa seat collider", SF.L, 0.5f, SF.D, SF.x, 0.25f, SF.z, "rug");
            Hidden(R, "Sofa back collider", SF.L, 0.9f, 0.24f, SF.x, 0.45f, SF.z - SF.D / 2f + 0.12f);
            Hidden(R, "Coffee table collider", 1.0f, 0.05f, 0.5f, -1.66f, 0.375f, -1.0f, "table");
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Hidden(R, "Coffee table leg collider", 0.05f, 0.35f, 0.05f, -1.66f + sx * 0.4f, 0.175f, -1.0f + sz * 0.15f);
            Hidden(R, "Armchair collider", 0.9f, 0.55f, 0.9f, 2.2f, 0.275f, -1.2f, "rug");
            B(R, "Shelf", 1.3f, 0.46f, 0.34f, M("shelf", "#7B61FF"), 0.9f, 0.23f, -2.5f + 0.19f, 0f, "table");
            Cyl(R, "Plant pot", 0.23f, 0.336f, M("pot", "#E07A4F"), 2.52f, 0.168f, -2.08f, "table");
            Cyl(R, "Plant", 0.2f, 0.5f, M("plant_leaf", "#3E9A45"), 2.52f, 0.336f + 0.25f, -2.08f, null, false);
            Cyl(R, "Pouf", 0.3f, 0.2f, M("pouf", "#FF5D73"), -0.15f, 0.1f, -2.06f, "rug");
            // rug + football pitch + goals
            S(R, "Rug", 1.85f, 0.006f, 3.1f, M("rug", "#8E6FD6"), -1.93f, 0.003f, 0.73f, 0f, "rug");
            S(R, "Pitch", 1.3f, 0.002f, 2.5f, M("pitch", "#5FBF5A"), -1.93f, 0.007f, 0.73f, 0f, null, false);
            var white = M("goal_white", "#FFFFFF");
            foreach (var end in new[] { -1f, 1f })
            {
                float mz = 0.73f + end * (1.25f + 0.06f);
                B(R, "Goal bar", 0.354f, 0.014f, 0.014f, white, -1.93f, 0.006f + 0.16f, mz - end * 0.06f, 0f, null, true);
                foreach (var sx in new[] { -1f, 1f }) B(R, "Goal side", 0.006f, 0.16f, 0.12f, white, -1.93f + sx * 0.17f, 0.006f + 0.08f, mz);
                B(R, "Goal back", 0.34f, 0.16f, 0.006f, white, -1.93f, 0.006f + 0.08f, mz + end * 0.06f);
            }
            // garage corner mat + turntable
            S(R, "Garage mat", 1.25f, 0.004f, 1.0f, M("garage_mat", "#3D3B4F"), 2.2f, 0.002f, 1.78f, 0f, "rug");
            Cyl(R, "Turntable", 0.2f, 0.018f, M("turntable", "#FFC43D"), 2.2f, 0.009f, 1.78f, "table");
        }

        // ================================================================ hallway
        static void Hallway()
        {
            const string R = "hallway";
            float hz = (2.62f + 3.82f) / 2f;
            var tape = M("tape_yellow", "#FFC43D");
            for (float x = -6.2f + 0.4f; x < 6.2f - 0.3f; x += 0.5f) S(R, "Dash", 0.22f, 0.002f, 0.03f, tape, x, 0.001f, hz, 0f, null, false);
            S(R, "Start line", 0.06f, 0.002f, 0.95f, M("start_red", "#FF5D73"), -6.2f, 0.001f, hz, 0f, null, false);
            S(R, "Finish line", 0.06f, 0.002f, 0.95f, M("finish_blue", "#3A86FF"), 6.2f, 0.001f, hz, 0f, null, false);
            var frame = M("frame", "#3D3B4F");
            string[] pics = { "#FF5D73", "#FFC43D", "#1FA6A0", "#7B61FF", "#3A86FF" };
            float[] xs = { -6.3f, -3.6f, -2.0f, 2.0f, 3.6f, 6.3f };
            for (int i = 0; i < xs.Length; i++)
            {
                float z = i % 2 == 1 ? 3.82f - 0.02f : 2.62f + 0.02f;
                B(R, "Picture frame", 0.5f, 0.38f, 0.03f, frame, xs[i], 1.45f, z, 0f, null, false);
                S(R, "Picture", 0.42f, 0.3f, 0.035f, M("picture" + i % pics.Length, pics[i % pics.Length]), xs[i], 1.45f, z, 0f, null, false);
            }
            foreach (var dx in new[] { 0f, 0.14f }) B(R, "Shoe", 0.1f, 0.07f, 0.26f, M("shoe", "#FF5D73"), 1.45f + dx, 0.035f, 3.82f - 0.2f);
        }

        // ================================================================ kitchen
        static void Kitchen()
        {
            const string R = "kitchen";
            float Kz0 = -2.5f, Kx1 = 7f;
            var cab = M("cabinet", "#FFF6E4"); var counter = M("counter", "#3D3B4F"); var knob = M("knob", "#FFC43D");
            float c0 = 3.3f, c1 = 5.9f, cd = 0.6f, ch = 0.86f;
            B(R, "Cabinets", c1 - c0, ch, cd, cab, (c0 + c1) / 2f, ch / 2f, Kz0 + cd / 2f, 0f, "table");
            B(R, "Counter", c1 - c0 + 0.04f, 0.05f, cd + 0.04f, counter, (c0 + c1) / 2f, ch + 0.025f, Kz0 + cd / 2f, 0f, "table");
            for (float x = c0 + 0.3f; x < c1; x += 0.6f)
            {
                S(R, "Cabinet door", 0.56f, 0.78f, 0.02f, cab, x, 0.43f, Kz0 + cd + 0.01f, 0f, null, false);
                B(R, "Knob", 0.12f, 0.025f, 0.03f, knob, x, 0.74f, Kz0 + cd + 0.03f, 0f, null, false);
                B(R, "Upper cabinet", 0.56f, 0.7f, 0.34f, cab, x, 1.75f, Kz0 + 0.17f, 0f, null, false);
            }
            S(R, "Sink", 0.5f, 0.02f, 0.36f, M("sink", "#B9C3CF"), 4.4f, ch + 0.052f, Kz0 + 0.3f, 0f, null, false);
            B(R, "Fridge", 0.72f, 1.85f, 0.68f, M("fridge", "#8FD3B6"), Kx1 - 0.42f, 0.925f, Kz0 + 0.38f);
            // kitchen table + chairs: ducks run underneath, only the legs collide
            var KT = (x: 5.1f, z: 0.6f, w: 1.25f, d: 0.8f, h: 0.74f);
            var ktM = M("kitchen_table", "#FF8A3D"); var leg = M("kitchen_leg", "#3D3B4F"); var chair = M("chair", "#FFC43D");
            B(R, "Kitchen table", KT.w, 0.05f, KT.d, ktM, KT.x, KT.h, KT.z, 0f, "table");
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                Cyl(R, "Kitchen table leg", 0.025f, KT.h, leg, KT.x + sx * (KT.w / 2f - 0.07f), KT.h / 2f, KT.z + sz * (KT.d / 2f - 0.07f));
            var chairs = new[] { (KT.x - 0.32f, KT.z - KT.d / 2f - 0.25f, 0), (KT.x + 0.32f, KT.z - KT.d / 2f - 0.25f, 0), (KT.x - 0.32f, KT.z + KT.d / 2f + 0.25f, 1), (KT.x + 0.32f, KT.z + KT.d / 2f + 0.25f, 1) };
            foreach (var (cx, cz, flip) in chairs)
            {
                B(R, "Chair seat", 0.4f, 0.04f, 0.4f, chair, cx, 0.45f, cz, 0f, "table");
                B(R, "Chair back", 0.4f, 0.42f, 0.035f, chair, cx, 0.68f, cz + (flip == 1 ? 0.18f : -0.18f));
                foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                    Cyl(R, "Chair leg", 0.016f, 0.45f, leg, cx + sx * 0.16f, 0.225f, cz + sz * 0.16f);
            }
            B(R, "Fruit bowl", 0.3f, 0.06f, 0.3f, M("bowl", "#3A86FF"), KT.x, KT.h + 0.055f, KT.z, 0f, null, false);
        }

        // ================================================================ bedroom
        static void Bedroom()
        {
            const string R = "bedroom";
            float Bz0 = -2.5f, Bx0 = -7f;
            // round target rug
            string[] ring = { "#7B61FF", "#FFC43D", "#FF5D73", "#7B61FF", "#FFF6E4" };
            for (int i = 0; i < 5; i++) Cyl(R, "Round rug " + i, 1.0f * (1f - i * 0.19f), 0.006f + i * 0.0005f, M("rug_ring" + i, ring[i]), -4.95f, 0.003f + i * 0.00025f, 0.9f, i == 0 ? "rug" : null, i == 0);
            // low kids' bed: frame + bouncy mattress + headboard + pillow + quilt
            var BED = (x0: -6.92f, x1: -5.0f, z0: -2.45f, z1: -1.4f, top: 0.15f);
            float bcx = (BED.x0 + BED.x1) / 2f, bcz = (BED.z0 + BED.z1) / 2f, bw = BED.x1 - BED.x0, bd = BED.z1 - BED.z0;
            var bedM = M("bed_frame", "#3A86FF");
            B(R, "Bed frame", bw, 0.08f, bd, bedM, bcx, 0.04f, bcz, 0f, "wood");
            B(R, "Mattress", bw - 0.04f, BED.top - 0.08f, bd - 0.04f, M("mattress", "#FFF6E4"), bcx, 0.08f + (BED.top - 0.08f) / 2f, bcz, 0f, "bed");
            B(R, "Quilt", bw * 0.55f, 0.012f, bd - 0.02f, M("quilt", "#FF5D73"), bcx + bw * 0.2f, BED.top + 0.006f, bcz, 0f, "bed");
            B(R, "Pillow", 0.3f, 0.06f, 0.55f, M("pillow", "#FFFFFF"), BED.x0 + 0.25f, BED.top + 0.03f, bcz, 0f, "bed");
            B(R, "Headboard", 0.08f, 0.6f, bd, bedM, BED.x0 + 0.04f, 0.3f, bcz);
            // building-block ramp: floor -> foot of the bed
            Wedge(R, "Block ramp", -4.4f, 0f, BED.x1, BED.top, -1.92f, 0.34f, M("block_ramp", "#FFC43D"), 0f, "wood");
            // toy box, bookcase
            B(R, "Toy box", 0.55f, 0.34f, 0.38f, M("toy_box", "#FF8A3D"), -3.65f, 0.17f, Bz0 + 0.25f);
            B(R, "Toy box lid", 0.55f, 0.03f, 0.38f, M("toy_lid", "#FFC43D"), -3.65f, 0.5f, Bz0 + 0.08f, 0.6f, null, false);
            B(R, "Bookcase", 0.3f, 0.9f, 0.9f, M("bookcase", "#FFF6E4"), Bx0 + 0.15f, 0.45f, 1.6f);
            string[] blocks = { "#FF5D73", "#3A86FF", "#1FA6A0", "#7B61FF" };
            for (int i = 0; i < 6; i++) B(R, "Book block", 0.2f, 0.2f, 0.1f, M("block" + i % 4, blocks[i % 4]), Bx0 + 0.17f, 0.55f + (i > 2 ? 0.28f : 0f), 1.3f + (i % 3) * 0.24f, 0f, null, false);
        }

        // ================================================================ garden
        static void Garden()
        {
            const string R = "garden";
            float Gz0 = 3.94f, Gz1 = 7.6f, Gx0 = -3.5f, Gx1 = 3.5f;
            crng = new System.Random(99);                     // the bush and flower builders use it
            var stone = M("stone", "#C9C3D6");
            S(R, "Patio slab", 2.2f, 0.006f, 0.5f, stone, 0f, 0.003f, Gz0 + 0.25f, 0f, "stone");
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f, x = Mathf.Sin(t * 2.4f) * 0.9f - 0.2f, z = Gz0 + 0.75f + t * 2.5f, r = 0.2f + (i % 2) * 0.04f;
                Cyl(R, "Stepping stone", r, 0.012f, stone, x, 0.006f, z, "stone");
            }
            // puddle (splashable, slows you down)
            var pud = Cyl(R, "Puddle", 1f, 0.003f, M("puddle_rim", "#3A86FF"), 1.45f, 0.0015f, 5.2f, "water");
            pud.transform.localScale = new Vector3(0.84f, 0.0015f, 0.56f);
            // comic water: a lighter inside, so the darker rim reads as a drawn edge, and white shine strokes
            var pudIn = Cyl(R, "Puddle inside", 1f, 0.003f, M("puddle", "#5BC0EB"), 1.45f, 0.0024f, 5.2f, null, false);
            pudIn.transform.localScale = new Vector3(0.84f * 0.9f, 0.0015f, 0.56f * 0.88f);
            var shine = M("puddle_shine", "#FFFFFF"); shine.SetFloat("_Unlit", 1f);
            foreach (var (sx, sz, len, rot) in new[] { (1.2f, 5.05f, 0.2f, 18f), (1.62f, 5.3f, 0.13f, 18f), (1.78f, 5.12f, 0.07f, 18f) })
            {
                var st = Cyl(R, "Puddle shine", 1f, 0.003f, shine, sx, 0.0032f, sz, null, false);
                st.transform.localScale = new Vector3(len, 0.0015f, 0.022f); st.transform.localRotation = Quaternion.Euler(0f, rot, 0f);
                st.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // gentle grassy slope with a plateau by the fence
            Wedge(R, "Garden slope", 1.7f, 0f, 2.9f, 0.17f, 6.7f, 1.6f, GrassMat("lawn_soil", true), 0f, "grass");
            B(R, "Slope plateau", 3.45f - 2.9f, 0.17f, 1.6f, GrassMat("lawn_soil", true), (2.9f + 3.45f) / 2f, 0.085f, 6.7f, 0f, "grass");
            // picket fence
            var fence = M("fence", "#FFFFFF");
            void Run(float ax, float az, float bx, float bz)
            {
                float L = Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)); int n = Mathf.FloorToInt(L / 0.14f);
                float ry = Mathf.Atan2(-(bz - az), bx - ax);
                for (int i = 0; i <= n; i++) { float t = (float)i / n; B(R, "Picket", 0.07f, 0.5f, 0.025f, fence, ax + (bx - ax) * t, 0.25f, az + (bz - az) * t, ry, null, false); }
                foreach (var y in new[] { 0.15f, 0.38f }) B(R, "Fence rail", L, 0.05f, 0.02f, fence, (ax + bx) / 2f, y, (az + bz) / 2f, ry, null, false);
                B(R, "Fence collider", Mathf.Abs(bx - ax) + 0.04f, 0.5f, Mathf.Abs(bz - az) + 0.04f, fence, (ax + bx) / 2f, 0.25f, (az + bz) / 2f).GetComponent<Renderer>().enabled = false;
            }
            Run(Gx0, Gz1, Gx1, Gz1); Run(Gx0, Gz0, Gx0, Gz1); Run(Gx1, Gz0, Gx1, Gz1);
            // potted bushes
            var pot = M("pot", "#E07A4F");
            foreach (var (x, z, r) in new[] { (-3.1f, 4.35f, 0.2f), (-3.1f, 7.15f, 0.24f), (3.05f, 4.4f, 0.18f), (-1.6f, 7.2f, 0.17f) })
            {
                Cyl(R, "Garden pot", r, r * 1.4f, pot, x, r * 0.7f, z, "table");
                Stat(Bush(R, P(x, r * 1.4f - 0.02f, z), r * 4.2f), R);
            }
            // flowers
            var rnd = new System.Random(77);
            string[] petals = { "#FF5D73", "#FFE45C", "#7B61FF", "#FF8A9A", "#FFFFFF" };
            for (int i = 0; i < 26; i++)
            {
                float x = Gx0 + 0.3f + (float)rnd.NextDouble() * (Gx1 - Gx0 - 0.6f), z = Gz0 + 0.7f + (float)rnd.NextDouble() * (Gz1 - Gz0 - 1.0f);
                if (Mathf.Abs(x - 1.45f) < 0.6f && Mathf.Abs(z - 5.2f) < 0.45f) continue;
                if (x > 1.6f && z > 5.9f) continue;
                Stat(Flower(R, P(x, 0f, z), petals[i % petals.Length], 2.0f + (float)rnd.NextDouble() * 0.6f), R);
            }
        }

        /// <summary>Room rectangles in Unity space (x mirrored) for AI wandering: (minX, minZ, maxX, maxZ).</summary>
        public static readonly Dictionary<string, Vector4> RoomRects = new Dictionary<string, Vector4>
        {
            { "living",  new Vector4(-2.85f, -2.35f, 2.85f, 2.35f) },
            { "kitchen", new Vector4(-6.85f, -1.6f, -3.3f, 2.35f) },
            { "bedroom", new Vector4(3.3f, -1.2f, 6.85f, 2.35f) },
            { "hallway", new Vector4(-6.85f, 2.8f, 6.85f, 3.65f) },
            { "garden",  new Vector4(-3.3f, 4.1f, 3.3f, 7.4f) },
        };
    }
}
