using MicroduckVR.Core;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
                c.layer = Layers.Ceiling;
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

            WallX("wall_hall_back", 2.56f, -7.12f, 7.12f, new[] { (-0.45f, 0.45f, DoorH), (-5.4f, -4.6f, DoorH), (4.6f, 5.4f, DoorH) }, "hallway");
            WallX("wall_hall_front", 3.88f, -7.12f, 7.12f, new[] { (-1.0f, 1.0f, 2.1f) }, "hallway");
            WallZ("wall_living_left", -3.06f, -2.62f, 2.5f, new[] { (-1.2f, -0.4f, DoorH) }, "living");
            WallZ("wall_living_right", 3.06f, -2.62f, 2.5f, new[] { (-1.6f, -0.8f, DoorH) }, "living");
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
    }
}
