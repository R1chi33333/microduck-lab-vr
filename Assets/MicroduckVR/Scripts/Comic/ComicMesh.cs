using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.Comic
{
    /// <summary>
    /// Procedural comic furniture shapes: rounded (beveled) boxes with optional puff (cushions bulge),
    /// taper (narrower top or bottom), and hand-drawn wobble (smooth noise warp so nothing is ruler-straight).
    /// Meshes are centred on the origin, size in metres.
    /// </summary>
    public static class ComicMesh
    {
        /// <summary>How a rounded box is shaped.</summary>
        public struct Style
        {
            /// <summary>Corner radius (m), clamped to half the smallest side.</summary>
            public float radius;
            /// <summary>Corner segments.</summary>
            public int segments;
            /// <summary>Bulge of the faces as a fraction of the smallest half-size (cushions).</summary>
            public float puff;
            /// <summary>0 = straight; 0.2 = top 20% narrower (x and z).</summary>
            public float taperTop;
            /// <summary>Hand-drawn warp amplitude (m).</summary>
            public float wobble;
            /// <summary>Lean of the top along x (m).</summary>
            public float skew;
            /// <summary>Seed of the wobble noise.</summary>
            public int seed;
        }

        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>A rounded box of this size (m), cached by size and style.</summary>
        public static Mesh RoundedBox(Vector3 size, Style s)
        {
            string key = $"{size.x:F3},{size.y:F3},{size.z:F3}|{s.radius:F3},{s.segments},{s.puff:F2},{s.taperTop:F2},{s.wobble:F3},{s.skew:F3},{s.seed}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Vector3 h = size * 0.5f;
            float r = Mathf.Min(s.radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) - 1e-4f);
            r = Mathf.Max(r, 0f);
            int seg = Mathf.Max(1, s.segments);
            Vector3 inner = h - Vector3.one * r;
            // a subdivided cube projected onto the rounded box: grid per face, n x n
            int n = seg * 2 + 4;
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
            Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var fn in faceN)
            {
                Vector3 u = Mathf.Abs(fn.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(fn, u);
                int b = verts.Count;
                for (int j = 0; j <= n; j++)
                    for (int i = 0; i <= n; i++)
                    {
                        // cube point in [-1, 1]^3, with grid lines bunched towards the edges for crisp corners
                        float a = Bunch(i / (float)n), c = Bunch(j / (float)n);
                        Vector3 cp = fn + u * a + v * c;
                        Vector3 p = Shape(cp, h, inner, r, s, out Vector3 nrm);
                        verts.Add(p); normals.Add(nrm);
                    }
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int q = b + j * (n + 1) + i;
                        tris.Add(q); tris.Add(q + n + 1); tris.Add(q + 1);
                        tris.Add(q + 1); tris.Add(q + n + 1); tris.Add(q + n + 2);
                    }
            }
            var mesh = new Mesh { name = "ComicBox " + key };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();          // after warping, recompute so shading follows the bulges
            mesh.RecalculateBounds();
            // fix winding if the first face points inwards
            if (Vector3.Dot(mesh.normals[0], verts[0]) < 0f)
            {
                for (int i = 0; i < tris.Count; i += 3) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
            }
            cache[key] = mesh;
            return mesh;
        }

        static float Bunch(float t)
        {
            // map [0,1] -> [-1,1], denser near the ends
            float x = t * 2f - 1f;
            return Mathf.Sign(x) * (1f - Mathf.Pow(1f - Mathf.Abs(x), 1.6f));
        }

        static Vector3 Shape(Vector3 cp, Vector3 h, Vector3 inner, float r, Style s, out Vector3 nrm)
        {
            // rounded box: clamp to the inner box, push out by r along the direction to the clamped point
            Vector3 onBox = Vector3.Scale(cp, h);
            Vector3 core = new Vector3(Mathf.Clamp(onBox.x, -inner.x, inner.x), Mathf.Clamp(onBox.y, -inner.y, inner.y), Mathf.Clamp(onBox.z, -inner.z, inner.z));
            Vector3 d = onBox - core;
            nrm = d.sqrMagnitude > 1e-12f ? d.normalized : cp.normalized;
            Vector3 p = core + nrm * r;
            // puff: faces bulge outwards towards their centres (cushions)
            if (s.puff > 0f)
            {
                float m = Mathf.Min(h.x, Mathf.Min(h.y, h.z));
                float fx = 1f - cp.x * cp.x, fy = 1f - cp.y * cp.y, fz = 1f - cp.z * cp.z;
                p += new Vector3(Mathf.Sign(cp.x) * fy * fz * Mathf.Abs(cp.x), Mathf.Sign(cp.y) * fx * fz * Mathf.Abs(cp.y), Mathf.Sign(cp.z) * fx * fy * Mathf.Abs(cp.z)) * (s.puff * m);
            }
            // taper and skew along height
            float t01 = (p.y / h.y) * 0.5f + 0.5f;
            float k = 1f - s.taperTop * t01;
            p.x *= k; p.z *= k;
            p.x += s.skew * (t01 - 0.5f);
            // hand-drawn wobble: smooth low-frequency warp
            if (s.wobble > 0f)
            {
                float o = s.seed * 17.3f;
                p += new Vector3(
                    Noise(p.y * 3.1f + o, p.z * 2.7f) ,
                    Noise(p.x * 2.3f + o * 0.5f, p.z * 3.3f) * 0.6f,
                    Noise(p.x * 2.9f, p.y * 3.7f + o)) * s.wobble;
            }
            return p;
        }

        static float Noise(float a, float b) => Mathf.PerlinNoise(a + 11.1f, b + 7.7f) * 2f - 1f;

        /// <summary>Tapered round leg / disc: smooth sides plus flat caps with their own vertices and normals.</summary>
        public static Mesh Leg(float height, float rTop, float rBottom, int sides = 12)
        {
            string key = $"leg3 {height:F4},{rTop:F4},{rBottom:F4},{sides}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            float y0 = -height / 2f, y1 = height / 2f;
            float slope = (rBottom - rTop) / Mathf.Max(height, 1e-5f);
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f; float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                var nr = new Vector3(c, slope, sn).normalized;
                v.Add(new Vector3(c * rBottom, y0, sn * rBottom)); n.Add(nr);
                v.Add(new Vector3(c * rTop, y1, sn * rTop)); n.Add(nr);
            }
            for (int i = 0; i < sides; i++)
            {
                int q = i * 2;
                AddTri(t, v, n, q, q + 1, q + 2); AddTri(t, v, n, q + 2, q + 1, q + 3);
            }
            foreach (var top in new[] { false, true })
            {
                float y = top ? y1 : y0, r = top ? rTop : rBottom; var nn = top ? Vector3.up : Vector3.down;
                int centre = v.Count; v.Add(new Vector3(0f, y, 0f)); n.Add(nn);
                int ring = v.Count;
                for (int i = 0; i <= sides; i++) { float a = i / (float)sides * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(nn); }
                for (int i = 0; i < sides; i++) AddTri(t, v, n, centre, ring + i, ring + i + 1);
            }
            var mesh = new Mesh { name = "ComicLeg " + key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Flat ring (annulus) of the given height, centred on the origin, axis = local y. Mug handles, rails, wheel tyres, pitch circles.</summary>
        public static Mesh Ring(float rOuter, float rInner, float height, int sides = 24)
        {
            string key = $"ring {rOuter:F4},{rInner:F4},{height:F4},{sides}";
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            float y0 = -height / 2f, y1 = height / 2f;
            void Wall(float r, float sgn)
            {
                int b = v.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * r + Vector3.up * y0); n.Add(d * sgn); v.Add(d * r + Vector3.up * y1); n.Add(d * sgn);
                }
                for (int i = 0; i < sides; i++) { int q = b + i * 2; AddTri(t, v, n, q, q + 1, q + 2); AddTri(t, v, n, q + 2, q + 1, q + 3); }
            }
            void Cap(float y, Vector3 nn)
            {
                int b = v.Count;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * rInner + Vector3.up * y); n.Add(nn); v.Add(d * rOuter + Vector3.up * y); n.Add(nn);
                }
                for (int i = 0; i < sides; i++) { int q = b + i * 2; AddTri(t, v, n, q, q + 1, q + 2); AddTri(t, v, n, q + 2, q + 1, q + 3); }
            }
            Wall(rOuter, 1f); Wall(rInner, -1f); Cap(y1, Vector3.up); Cap(y0, Vector3.down);
            var mesh = new Mesh { name = "ComicRing " + key };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
            cache[key] = mesh;
            return mesh;
        }

        /// <summary>Adds a triangle wound so its face normal agrees with the vertices' intended normal (Unity: clockwise = front).</summary>
        static void AddTri(List<int> t, List<Vector3> v, List<Vector3> n, int a, int b, int c)
        {
            Vector3 face = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            Vector3 want = n[a] + n[b] + n[c];
            if (Vector3.Dot(face, want) >= 0f) { t.Add(a); t.Add(b); t.Add(c); }
            else { t.Add(a); t.Add(c); t.Add(b); }
        }
    }
}
