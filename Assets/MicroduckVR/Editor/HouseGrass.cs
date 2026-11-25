using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Real comic grass on the lawn: tufts of 3-5 fat, tapered, slightly curved blades, two greens, tall enough to
    /// reach a duck's knees (so in VR you see it standing up around you). Blades get the ink outline like everything
    /// else and are lit flat (normals point up) so the whole lawn reads as clean cel colour, not noise. Paths, the
    /// puddle, flower beds and other things on the ground are left clear. Built into a few combined meshes.
    /// </summary>
    public static partial class HouseBuilder
    {
        static void Grass()
        {
            var r = new System.Random(4242);
            var dark = M("grass_blade_dark", "#3E9A45"); var light = M("grass_blade_light", "#6CBF4F"); var tip = M("grass_blade_tip", "#8FD35A");
            var mats = new[] { dark, light, tip };
            // inside the fence: dense; outside it, a sparser band so the lawn beyond is not bare
            Scatter(r, mats, new Rect(-3.45f, 3.98f, 6.9f, 3.58f), 0.075f, 1f);
            Scatter(r, mats, new Rect(-9f, 7.75f, 18f, 5f), 0.22f, 1.25f);
            Scatter(r, mats, new Rect(-9f, 3.98f, 5.4f, 3.7f), 0.22f, 1.25f);
            Scatter(r, mats, new Rect(3.6f, 3.98f, 5.4f, 3.7f), 0.22f, 1.25f);
        }

        static void Scatter(System.Random r, Material[] mats, Rect area, float spacing, float scale)
        {
            Physics.SyncTransforms();
            // one mesh builder per 1 m chunk per material
            var chunks = new Dictionary<(int, int, int), (List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)>();
            float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
            for (float z = area.yMin; z < area.yMax; z += spacing)
                for (float x = area.xMin; x < area.xMax; x += spacing)
                {
                    float px = x + Rf(-0.45f, 0.45f) * spacing, pz = z + Rf(-0.45f, 0.45f) * spacing;
                    // only on bare lawn: whatever the down-ray hits first must be grass ground
                    if (!Physics.Raycast(new Vector3(px, 1.2f, pz), Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var tag = hit.collider.GetComponent<MicroduckVR.World.SurfaceTag>();
                    if (tag == null || tag.type != "grass" || hit.normal.y < 0.95f) continue;
                    int blades = 3 + r.Next(3);
                    float size = Rf(0.75f, 1.25f) * scale;
                    for (int b = 0; b < blades; b++)
                    {
                        int mi = b == 0 ? 0 : r.Next(3);
                        var key = (Mathf.FloorToInt(px), Mathf.FloorToInt(pz), mi);
                        if (!chunks.TryGetValue(key, out var c)) { c = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>()); chunks[key] = c; }
                        float a = Rf(0f, Mathf.PI * 2f), lean = Rf(0.15f, 0.55f);
                        var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        var basePos = new Vector3(px, hit.point.y - 0.002f, pz) + dir * Rf(0f, 0.012f) * size;
                        Blade(c.v, c.n, c.uv, c.t, basePos, dir, Rf(0.035f, 0.07f) * size, Rf(0.007f, 0.011f) * size, lean);
                    }
                }
            var root = new GameObject("Grass " + area.center.ToString("F0")).transform; root.SetParent(rooms["garden"], false);
            foreach (var kv in chunks)
            {
                var (v, n, uv, t) = kv.Value;
                var mesh = new Mesh { name = $"grass {kv.Key}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
                var go = new GameObject("grass chunk") { layer = DuckSetup.LayerWorld, isStatic = true };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[kv.Key.Item3];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>A fat tapered blade with a little bend, both faces, normals up (flat cel lighting).</summary>
        static void Blade(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 p, Vector3 dir, float h, float w, float lean)
        {
            var side = new Vector3(-dir.z, 0f, dir.x);
            const int segs = 3;
            int b0 = v.Count;
            for (int i = 0; i <= segs; i++)
            {
                float u = i / (float)segs;
                float bend = lean * u * u;                                   // curves over towards the tip
                var c = p + Vector3.up * h * u * (1f - 0.25f * bend) + dir * h * bend * 0.8f;
                float half = w * 0.5f * (1f - u) + 0.0006f;
                v.Add(c - side * half); v.Add(c + side * half);
                n.Add(Vector3.up); n.Add(Vector3.up);
                uv.Add(new Vector2(0f, u)); uv.Add(new Vector2(1f, u));
            }
            for (int i = 0; i < segs; i++)
            {
                int q = b0 + i * 2;
                // front and back faces
                t.Add(q); t.Add(q + 2); t.Add(q + 1); t.Add(q + 1); t.Add(q + 2); t.Add(q + 3);
                t.Add(q); t.Add(q + 1); t.Add(q + 2); t.Add(q + 1); t.Add(q + 3); t.Add(q + 2);
            }
        }
    }
}
