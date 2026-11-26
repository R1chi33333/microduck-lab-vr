using System.Collections.Generic;
using UnityEngine;

namespace MicroduckVR.Comic
{
    public partial class ComicFX
    {
        // ------------------------------------------------------------------ the atlas (port of sfxAtlas)
        // Shapes are written in the web's 128 px canvas units (y down) as signed distance functions, then filled
        // layer over layer in canvas paint order with an antialiased edge. A stroke of width w is the shape grown by
        // w/2, painted in ink underneath the fill.
        delegate float Sdf(float x, float y);

        static Texture2D BuildAtlas()
        {
            const int W = Cell * 4, H = Cell * 2; const float K = Cell / 128f;
            Color ink = ComicLook.Ink;
            var cells = new List<(Sdf sd, Color c)>[8];
            for (int i = 0; i < 8; i++) cells[i] = new List<(Sdf, Color)>();

            long r = 7; float rnd() { r = r * 16807 % 2147483647; return r / 2147483647f; }

            // billowy puff: union of circles, ink outline drawn as fat circles underneath, darker inner shading blob
            void Puff(int ci, int count, float rad)
            {
                var bl = new List<Vector3>();
                for (int k = 0; k < count; k++)
                {
                    float a = k / (float)count * Mathf.PI * 2f + rnd() * 0.5f, d = rad * (0.42f + rnd() * 0.12f);
                    bl.Add(new Vector3(64f + Mathf.Cos(a) * d, 66f + Mathf.Sin(a) * d * 0.85f, rad * (0.38f + rnd() * 0.16f)));
                }
                bl.Add(new Vector3(64f, 66f, rad * 0.55f));
                var c = bl.ToArray();
                float Union(float x, float y, float grow, float ox, float oy, float mul)
                {
                    float m = float.MaxValue;
                    foreach (var b in c) { float dx = x - (b.x + b.z * ox), dy = y - (b.y + b.z * oy); m = Mathf.Min(m, Mathf.Sqrt(dx * dx + dy * dy) - (b.z * mul + grow)); }
                    return m;
                }
                float hx = 64f - rad * 0.32f, hy = 66f - rad * 0.36f, hr = rad * 0.2f;
                cells[ci].Add(((x, y) => Union(x, y, 6f, 0f, 0f, 1f), ink));
                cells[ci].Add(((x, y) => Union(x, y, 0f, 0f, 0f, 1f), ComicLook.Hex("#F3E6CC")));
                cells[ci].Add(((x, y) => Mathf.Max(Union(x, y, 0f, 0.35f, 0.45f, 0.72f), Union(x, y, 0f, 0f, 0f, 1f)), ComicLook.Hex("#CDB894")));
                cells[ci].Add(((x, y) => Mathf.Max(Mathf.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy)) - hr, Union(x, y, 0f, 0f, 0f, 1f)), ComicLook.Hex("#FFFBF0")));
            }
            Puff(0, 7, 40f); Puff(1, 6, 36f); Puff(2, 5, 30f);

            // spark burst: jagged star, yellow with white core and ink edge
            Vector2[] StarShape(float R, float Ri, int count)
            {
                var p = new Vector2[count * 2];
                for (int k = 0; k < count * 2; k++) { float a = k / (float)(count * 2) * Mathf.PI * 2f, rr = (k % 2 == 1 ? Ri : R) * (0.85f + rnd() * 0.3f); p[k] = new Vector2(64f + Mathf.Cos(a) * rr, 64f + Mathf.Sin(a) * rr); }
                return p;
            }
            var s1 = StarShape(54f, 22f, 9); var s2 = StarShape(26f, 11f, 7);
            cells[3].Add(((x, y) => SdPoly(s1, x, y) - 4.5f, ink));
            cells[3].Add(((x, y) => SdPoly(s1, x, y), ComicLook.Hex("#FFD23F")));
            cells[3].Add(((x, y) => SdPoly(s2, x, y), Color.white));

            // 4-point glint
            Vector2[] GlintShape(float R, float w)
            {
                var p = new Vector2[8];
                for (int k = 0; k < 8; k++) { float a = k / 8f * Mathf.PI * 2f - Mathf.PI / 2f, rr = k % 2 == 1 ? w : R; p[k] = new Vector2(64f + Mathf.Cos(a) * rr, 64f + Mathf.Sin(a) * rr); }
                return p;
            }
            var g1 = GlintShape(58f, 13f); var g2 = GlintShape(30f, 7f);
            cells[4].Add(((x, y) => SdPoly(g1, x, y) - 4f, ink));
            cells[4].Add(((x, y) => SdPoly(g1, x, y), Color.white));
            cells[4].Add(((x, y) => SdPoly(g2, x, y), ComicLook.Hex("#FFE98A")));

            // zig-zag bolts (pointing +x): pale gold with ink outline
            void BoltCell(int ci, float[] pts)
            {
                var p = new Vector2[pts.Length / 2];
                for (int k = 0; k < p.Length; k++) p[k] = new Vector2(pts[k * 2] * 128f, pts[k * 2 + 1] * 128f);
                cells[ci].Add(((x, y) => SdPoly(p, x, y) - 4.5f, ink));
                cells[ci].Add(((x, y) => SdPoly(p, x, y), ComicLook.Hex("#FFE45C")));
            }
            BoltCell(5, new[] { 0.04f, 0.52f, 0.36f, 0.3f, 0.34f, 0.47f, 0.66f, 0.26f, 0.62f, 0.44f, 0.97f, 0.38f, 0.6f, 0.62f, 0.63f, 0.5f, 0.33f, 0.72f, 0.36f, 0.56f });
            BoltCell(6, new[] { 0.03f, 0.45f, 0.3f, 0.62f, 0.33f, 0.44f, 0.6f, 0.66f, 0.6f, 0.48f, 0.97f, 0.58f, 0.58f, 0.32f, 0.57f, 0.5f, 0.31f, 0.3f, 0.29f, 0.47f });

            // speed streak: tapered stroke, ink with a white core (two quadratic curves, flattened)
            Vector2[] StreakShape(float h)
            {
                const int N = 24; var p = new List<Vector2>();
                for (int k = 0; k <= N; k++) p.Add(Quad(new Vector2(4f, 64f), new Vector2(64f, 64f - h), new Vector2(124f, 64f - h * 0.25f), k / (float)N));
                for (int k = 0; k <= N; k++) p.Add(Quad(new Vector2(124f, 64f + h * 0.25f), new Vector2(64f, 64f + h), new Vector2(4f, 64f), k / (float)N));
                return p.ToArray();
            }
            var st1 = StreakShape(16f); var st2 = StreakShape(7f);
            cells[7].Add(((x, y) => SdPoly(st1, x, y), ink));
            cells[7].Add(((x, y) => SdPoly(st2, x, y), Color.white));

            // paint: every pixel of a cell, layer over layer (straight alpha "over"), clipped to its cell
            var px = new Color32[W * H];
            var ink32 = (Color32)ink; ink32.a = 0;
            for (int iy = 0; iy < H; iy++)
            {
                int canvasRow = H - 1 - iy;                 // texture rows go up, canvas rows go down
                int cr = canvasRow / Cell; float y = (canvasRow % Cell + 0.5f) / K;
                for (int ix = 0; ix < W; ix++)
                {
                    var layers = cells[cr * 4 + ix / Cell]; float x = (ix % Cell + 0.5f) / K;
                    float a = 0f, rr = ink.r, gg = ink.g, bb = ink.b;
                    for (int l = 0; l < layers.Count; l++)
                    {
                        float cov = Mathf.Clamp01(0.5f - layers[l].sd(x, y) * K);
                        if (cov <= 0f) { if (l == 0) break; continue; }       // outside the outermost (ink) layer: empty
                        var c = layers[l].c; float na = cov + a * (1f - cov);
                        rr = (c.r * cov + rr * a * (1f - cov)) / na; gg = (c.g * cov + gg * a * (1f - cov)) / na; bb = (c.b * cov + bb * a * (1f - cov)) / na;
                        a = na;
                    }
                    px[iy * W + ix] = a > 0f ? new Color32((byte)(rr * 255f + 0.5f), (byte)(gg * 255f + 0.5f), (byte)(bb * 255f + 0.5f), (byte)(a * 255f + 0.5f)) : ink32;
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, 6, false) { name = "comic fx atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static Vector2 Quad(Vector2 a, Vector2 c, Vector2 b, float t) { float u = 1f - t; return u * u * a + 2f * u * t * c + t * t * b; }

        /// <summary>Signed distance to a closed polygon (negative inside, nonzero winding like canvas fill).</summary>
        static float SdPoly(Vector2[] p, float x, float y)
        {
            float best = float.MaxValue; int wn = 0;
            for (int i = 0, m = p.Length; i < m; i++)
            {
                Vector2 a = p[i], b = p[(i + 1) % m];
                float ex = b.x - a.x, ey = b.y - a.y, wx = x - a.x, wy = y - a.y;
                float t = Mathf.Clamp01((wx * ex + wy * ey) / Mathf.Max(ex * ex + ey * ey, 1e-8f));
                float dx = wx - ex * t, dy = wy - ey * t; best = Mathf.Min(best, dx * dx + dy * dy);
                float cross = ex * wy - ey * wx;
                if (a.y <= y) { if (b.y > y && cross > 0f) wn++; }
                else if (b.y <= y && cross < 0f) wn--;
            }
            float s = Mathf.Sqrt(best);
            return wn != 0 ? -s : s;
        }
    }
}
