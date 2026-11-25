using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// The comic lawn: thousands of tufts, each a fan of fat leaf-shaped blades, packed so the soil never shows.
    /// The blades use Microduck/ToonGrass (flat colour bands, big two-tone patches, wind waves, parting around
    /// ducks) and are not outlined one by one; a sprinkling of darker "ink tufts" with the normal toon material
    /// gets the usual outline, the way a comic artist dots a lawn with a few drawn clumps. Paths, the puddle,
    /// flower beds and other things on the ground are left clear. Built into combined meshes per 1 m tile.
    /// </summary>
    public static partial class HouseBuilder
    {
        internal static Material GrassMat(string name, bool ground)
        {
            string p = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Microduck/ToonGrass");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
            m.shader = shader; m.SetFloat("_Ground", ground ? 1f : 0f); m.SetFloat("_InkTufts", 0.8f); m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Grass()
        {
            var r = new System.Random(4242);
            var blades = GrassMat("grass_blades", false);
            var ink = M("grass_ink_tuft", "#2F8F40");
            // inside the fence: a thick carpet; outside it, bigger and sparser tufts so the lawn beyond is not bare
            Scatter(r, blades, ink, new Rect(-3.45f, 3.98f, 6.9f, 3.58f), 0.04f, 1f, 6);
            Scatter(r, blades, ink, new Rect(-9f, 7.75f, 18f, 5f), 0.1f, 1.9f, 4);
            Scatter(r, blades, ink, new Rect(-9f, 3.98f, 5.4f, 3.7f), 0.1f, 1.9f, 4);
            Scatter(r, blades, ink, new Rect(3.6f, 3.98f, 5.4f, 3.7f), 0.1f, 1.9f, 4);
        }

        class GrassChunk { public List<Vector3> v = new List<Vector3>(); public List<Vector3> n = new List<Vector3>(); public List<Vector2> uv = new List<Vector2>(); public List<Vector4> blade = new List<Vector4>(); public List<int> t = new List<int>(); }

        static void Scatter(System.Random r, Material blades, Material ink, Rect area, float spacing, float scale, int maxBlades)
        {
            Physics.SyncTransforms();
            var chunks = new Dictionary<(int, int, int), GrassChunk>();
            float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
            for (float z = area.yMin; z < area.yMax; z += spacing)
                for (float x = area.xMin; x < area.xMax; x += spacing)
                {
                    float px = x + Rf(-0.5f, 0.5f) * spacing, pz = z + Rf(-0.5f, 0.5f) * spacing;
                    // only on bare lawn: whatever the down-ray hits first must be grass ground
                    if (!Physics.Raycast(new Vector3(px, 1.2f, pz), Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var tag = hit.collider.GetComponent<MicroduckVR.World.SurfaceTag>();
                    if (tag == null || tag.type != "grass" || hit.normal.y < 0.95f) continue;
                    bool inked = false;                       // ink silhouettes now come from the shader (see ToonGrass / ComicOutline)
                    int n = inked ? 5 : maxBlades - r.Next(2);
                    float size = Rf(0.8f, 1.2f) * scale * (inked ? 1.25f : 1f);
                    var key = (Mathf.FloorToInt(px), Mathf.FloorToInt(pz), inked ? 1 : 0);
                    if (!chunks.TryGetValue(key, out var c)) { c = new GrassChunk(); chunks[key] = c; }
                    var root = new Vector3(px, hit.point.y - 0.003f, pz);
                    float a0 = Rf(0f, Mathf.PI * 2f);
                    for (int b = 0; b < n; b++)
                    {
                        // one upright blade in the middle, the rest fanned out around it
                        bool centre = b == 0;
                        float a = a0 + b * (Mathf.PI * 2f / (n - 1)) + Rf(-0.4f, 0.4f);
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        float h = (centre ? Rf(0.052f, 0.066f) : Rf(0.036f, 0.054f)) * size;
                        float w = Rf(0.015f, 0.021f) * size;
                        float lean = centre ? Rf(0.02f, 0.12f) : Rf(0.3f, 0.6f);
                        var p = root + (centre ? Vector3.zero : dir * Rf(0.004f, 0.012f) * size);
                        // the blade's flat side faces a random way so the tuft looks full from everywhere
                        float fa = Rf(0f, Mathf.PI); var face = new Vector3(Mathf.Cos(fa), 0f, Mathf.Sin(fa));
                        Blade(c, p, dir, face, h, w, lean, new Vector4(px, pz, h, Rf(0f, 1f)), inked);
                    }
                }
            var rootT = new GameObject("Grass " + area.center.ToString("F0")).transform; rootT.SetParent(rooms["garden"], false);
            foreach (var kv in chunks)
            {
                var c = kv.Value; bool inked = kv.Key.Item3 == 1;
                var mesh = new Mesh { name = $"grass {kv.Key}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(c.v); mesh.SetNormals(c.n); mesh.SetUVs(0, c.uv); mesh.SetUVs(1, c.blade); mesh.SetTriangles(c.t, 0); mesh.RecalculateBounds();
                var bb = mesh.bounds; bb.Expand(new Vector3(0.2f, 0.1f, 0.2f)); mesh.bounds = bb;      // room for the sway
                var go = new GameObject(inked ? "grass ink tufts" : "grass chunk") { layer = DuckSetup.LayerWorld };
                go.transform.SetParent(rootT, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = inked ? ink : blades;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>A leaf-shaped blade (narrow foot, fat belly, pointed tip) that curves over along dir; uv.y = height fraction.</summary>
        static void Blade(GrassChunk c, Vector3 p, Vector3 dir, Vector3 face, float h, float w, float lean, Vector4 data, bool twoSided)
        {
            var side = new Vector3(-face.z, 0f, face.x);
            int b0 = c.v.Count;
            float[] us = { 0f, 0.35f, 0.7f }; float[] ws = { 0.5f, 1f, 0.66f };
            for (int i = 0; i < 3; i++)
            {
                float u = us[i];
                var m = p + Vector3.up * h * u * (1f - 0.3f * lean * u) + dir * h * lean * u * u;
                float half = w * 0.5f * ws[i];
                c.v.Add(m - side * half); c.v.Add(m + side * half);
                c.n.Add(Vector3.up); c.n.Add(Vector3.up);
                c.uv.Add(new Vector2(0f, u)); c.uv.Add(new Vector2(1f, u));
                c.blade.Add(data); c.blade.Add(data);
            }
            c.v.Add(p + Vector3.up * h * (1f - 0.3f * lean) + dir * h * lean); c.n.Add(Vector3.up); c.uv.Add(new Vector2(0.5f, 1f)); c.blade.Add(data);
            for (int i = 0; i < 2; i++)
            {
                int q = b0 + i * 2;
                c.t.Add(q); c.t.Add(q + 2); c.t.Add(q + 1); c.t.Add(q + 1); c.t.Add(q + 2); c.t.Add(q + 3);
                // the ink tufts use the toon shader, which culls back faces: give them both sides
                if (twoSided) { c.t.Add(q); c.t.Add(q + 1); c.t.Add(q + 2); c.t.Add(q + 1); c.t.Add(q + 3); c.t.Add(q + 2); }
            }
            c.t.Add(b0 + 4); c.t.Add(b0 + 6); c.t.Add(b0 + 5);
            if (twoSided) { c.t.Add(b0 + 4); c.t.Add(b0 + 5); c.t.Add(b0 + 6); }
        }
    }
}
