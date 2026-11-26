using System.Collections.Generic;
using MicroduckVR.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace MicroduckVR.Comic
{
    /// <summary>
    /// Hand-drawn comic skate effects (port of skatefx.js): billowy smoke puffs that swell and pop, jagged sparks,
    /// four-point glints, zig-zag energy bolts and speed streaks, plus skid marks on the floor.
    ///
    /// The shapes are drawn once into an atlas texture at startup (the same shapes and colours as the web canvas).
    /// All particles are camera-facing quads in one dynamic mesh, one draw call; all skid marks are a ring buffer in a
    /// second mesh. Fixed pools, nothing allocated per frame, no GameObject per particle.
    ///
    /// Comfort: nothing is ever drawn closer than MinEyeDistance to the player's eyes (particles shrink away as they
    /// get near), and nothing here touches the camera.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ComicFX : MonoBehaviour
    {
        public const int Max = 320, MaxSkids = 360;
        public const float SkidLife = 4f;
        /// <summary>Nothing is drawn closer than this to the player's eyes (m).</summary>
        public const float MinEyeDistance = 0.12f;
        const int Cell = 256;              // atlas cell (px); the web uses 128, VR wants it crisper

        // particle kinds (web: 0 smoke, 1 spark, 2 glint, 3 bolt, 4 streak)
        const byte KSmoke = 0, KSpark = 1, KGlint = 2, KBolt = 3, KStreak = 4;

        static ComicFX inst;
        static bool quitting;

        /// <summary>The effects system (made on first use).</summary>
        public static ComicFX Get()
        {
            if (inst != null || quitting) return inst;
            inst = FindAnyObjectByType<ComicFX>();
            if (inst == null) inst = new GameObject("Comic FX").AddComponent<ComicFX>();
            return inst;
        }

        // the atlas takes a moment to draw: do it while the scene loads, not on the first push-off
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { quitting = false; Get(); }

        void OnApplicationQuit() { quitting = true; }

        // ---- particle state (struct of arrays)
        readonly Vector3[] pos = new Vector3[Max], vel = new Vector3[Max], dir = new Vector3[Max];
        readonly float[] age = new float[Max], life = new float[Max], s0 = new float[Max], rot = new float[Max], rv = new float[Max], stretch = new float[Max];
        readonly byte[] cell = new byte[Max], kind = new byte[Max];
        int n, drawn;

        // ---- particle mesh: four vertices per particle, all at its centre; the shader spreads the corners
        Mesh mesh; Material mat; Texture2D atlas;
        readonly Vector3[] mv = new Vector3[Max * 4];
        readonly Vector4[] mu0 = new Vector4[Max * 4], mu1 = new Vector4[Max * 4];

        // ---- skid marks: ring buffer of flat floor quads
        Mesh skidMesh; Material skidMat;
        readonly Vector3[] sv = new Vector3[MaxSkids * 4];
        readonly Vector2[] su0 = new Vector2[MaxSkids * 4], su1 = new Vector2[MaxSkids * 4];
        int sk; bool skidDirty; float lastSkid = -99f, clock;

        uint seed = 12345;
        float Rnd() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) * (1f / 16777216f); }

        static readonly Vector2[] Corners = { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f) };
        static readonly int FxTimeId = Shader.PropertyToID("_FxTime"), LifeId = Shader.PropertyToID("_Life"), InkId = Shader.PropertyToID("_InkColor"), MainTexId = Shader.PropertyToID("_MainTex");
        const MeshUpdateFlags Quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers;

        void Awake()
        {
            if (inst != null && inst != this) { Destroy(gameObject); return; }
            inst = this;
            var fxShader = Shader.Find("Microduck/ComicFX"); var skidShader = Shader.Find("Microduck/ComicSkid");
            if (fxShader == null || skidShader == null) { Debug.LogWarning("[Microduck] ComicFX shaders missing: skate effects off"); enabled = false; return; }
            atlas = BuildAtlas();
            mat = new Material(fxShader) { name = "Comic FX" }; mat.SetTexture(MainTexId, atlas);
            skidMat = new Material(skidShader) { name = "Comic skids" }; skidMat.SetColor(InkId, ComicLook.Ink); skidMat.SetFloat(LifeId, SkidLife);

            var big = new Bounds(Vector3.zero, Vector3.one * 2000f);
            var tris = new int[Max * 6];
            for (int i = 0; i < Max; i++)
            {
                int v = i * 4, t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1; tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
                for (int c = 0; c < 4; c++) mu0[v + c] = new Vector4(Corners[c].x, Corners[c].y, 0f, 0f);
            }
            mesh = new Mesh { name = "comic fx" }; mesh.MarkDynamic();
            mesh.SetVertices(mv); mesh.SetUVs(0, mu0); mesh.SetUVs(1, mu1); mesh.SetTriangles(tris, 0, false); mesh.bounds = big;

            var stris = new int[MaxSkids * 6];
            for (int i = 0; i < MaxSkids; i++)
            {
                int v = i * 4, t = i * 6;
                stris[t] = v; stris[t + 1] = v + 2; stris[t + 2] = v + 1; stris[t + 3] = v; stris[t + 4] = v + 3; stris[t + 5] = v + 2;
                for (int c = 0; c < 4; c++) su1[v + c] = new Vector2(-99f, 0f);
            }
            skidMesh = new Mesh { name = "comic skids" }; skidMesh.MarkDynamic();
            skidMesh.SetVertices(sv); skidMesh.SetUVs(0, su0); skidMesh.SetUVs(1, su1); skidMesh.SetTriangles(stris, 0, false); skidMesh.bounds = big;
        }

        void OnDestroy()
        {
            if (inst == this) inst = null;
            if (mesh != null) Destroy(mesh); if (skidMesh != null) Destroy(skidMesh);
            if (mat != null) Destroy(mat); if (skidMat != null) Destroy(skidMat); if (atlas != null) Destroy(atlas);
        }

        // ------------------------------------------------------------------ spawning (web api, same sizes and lifetimes)
        int Spawn(byte k, Vector3 p, Vector3 v, float size, float lifeS, Vector3 d)
        {
            if (n >= Max || !enabled) return -1;
            int i = n++;
            pos[i] = p; vel[i] = v; dir[i] = d;
            age[i] = 0f; life[i] = lifeS; kind[i] = k; s0[i] = size; stretch[i] = 1f;
            rot[i] = (Rnd() - 0.5f) * 6.28f; rv[i] = (Rnd() - 0.5f) * 3f;
            cell[i] = k == KSmoke ? (byte)Mathf.Min(2, (int)(Rnd() * 3f)) : k == KSpark ? (byte)3 : k == KGlint ? (byte)4 : k == KBolt ? (byte)(Rnd() < 0.5f ? 6 : 5) : (byte)7;
            if (k == KBolt || k == KStreak) { rot[i] = k == KBolt ? (Rnd() - 0.5f) * 0.5f : 0f; rv[i] = 0f; }
            if (k == KGlint) { rot[i] = 0f; rv[i] = 0f; }
            return i;
        }

        /// <summary>A billowy puff. back: the way it drifts (backwards along the travel). carry: extra velocity
        /// (the skater's own, so a puff can keep pace for a moment).</summary>
        public static void Smoke(Vector3 p, Vector3 back, float scale = 1f, Vector3 carry = default)
        {
            var f = Get(); if (f == null) return;
            float sz = (0.035f + f.Rnd() * 0.025f) * scale;
            var at = new Vector3(p.x + (f.Rnd() - 0.5f) * 0.01f, p.y + 0.008f, p.z + (f.Rnd() - 0.5f) * 0.01f);
            float vx = back.x * (0.12f + f.Rnd() * 0.1f) + (f.Rnd() - 0.5f) * 0.06f, vy = 0.05f + f.Rnd() * 0.06f, vz = back.z * (0.12f + f.Rnd() * 0.1f) + (f.Rnd() - 0.5f) * 0.06f;
            f.Spawn(KSmoke, at, new Vector3(vx, vy, vz) + carry, sz, 0.4f + f.Rnd() * 0.4f, Vector3.zero);
        }

        /// <summary>A jagged spark thrown up and out; falls.</summary>
        public static void Spark(Vector3 p, float scale = 1f, Vector3 carry = default)
        {
            var f = Get(); if (f == null) return;
            float a = f.Rnd() * 6.28f, sp = 0.25f + f.Rnd() * 0.45f;
            var v = new Vector3(Mathf.Cos(a) * sp, 0.25f + f.Rnd() * 0.5f, Mathf.Sin(a) * sp);
            f.Spawn(KSpark, p + Vector3.up * 0.01f, v + carry, (0.014f + f.Rnd() * 0.012f) * scale, 0.22f + f.Rnd() * 0.18f, Vector3.zero);
        }

        /// <summary>A four-point glint that twinkles in and out near a point.</summary>
        public static void Glint(Vector3 p, float scale = 1f)
        {
            var f = Get(); if (f == null) return;
            var at = new Vector3(p.x + (f.Rnd() - 0.5f) * 0.05f, p.y + 0.02f + f.Rnd() * 0.06f, p.z + (f.Rnd() - 0.5f) * 0.05f);
            f.Spawn(KGlint, at, new Vector3(0f, 0.02f, 0f), (0.03f + f.Rnd() * 0.02f) * scale, 0.3f + f.Rnd() * 0.12f, Vector3.zero);
        }

        /// <summary>A zig-zag energy bolt lying along dir (unit), flickering.</summary>
        public static void Bolt(Vector3 p, Vector3 d, float scale = 1f, Vector3 carry = default)
        {
            var f = Get(); if (f == null) return;
            int i = f.Spawn(KBolt, p, new Vector3(d.x * 0.1f, 0.02f, d.z * 0.1f) + carry, (0.05f + f.Rnd() * 0.03f) * scale, 0.16f + f.Rnd() * 0.1f, d);
            if (i >= 0) f.stretch[i] = 1.6f + f.Rnd() * 0.6f;
        }

        /// <summary>A speed streak along the travel direction d (unit), left behind a wheel.</summary>
        public static void Streak(Vector3 p, Vector3 d, float scale = 1f)
        {
            var f = Get(); if (f == null) return;
            int i = f.Spawn(KStreak, p, new Vector3(-d.x * 0.05f, 0f, -d.z * 0.05f), 0.018f * scale, 0.14f + f.Rnd() * 0.08f, d);
            if (i >= 0) f.stretch[i] = 4f + f.Rnd() * 2f;
        }

        /// <summary>One skid stroke on the floor from a to b (both already at floor height), width w.</summary>
        public static void Skid(Vector3 a, Vector3 b, float w)
        {
            var f = Get(); if (f == null || !f.enabled) return;
            var d = b - a; d.y = 0f; float L = d.magnitude; if (L < 1e-4f) return;
            var along = d / L * ((L + w * 0.6f) * 0.5f); var across = new Vector3(-d.z, 0f, d.x) / L * (w * 0.5f);
            var mid = (a + b) * 0.5f;
            int v = f.sk * 4; float seedS = f.Rnd();
            f.sv[v] = mid - along - across; f.sv[v + 1] = mid + along - across; f.sv[v + 2] = mid + along + across; f.sv[v + 3] = mid - along + across;
            f.su0[v] = new Vector2(0f, 0f); f.su0[v + 1] = new Vector2(1f, 0f); f.su0[v + 2] = new Vector2(1f, 1f); f.su0[v + 3] = new Vector2(0f, 1f);
            for (int c = 0; c < 4; c++) f.su1[v + c] = new Vector2(f.clock, seedS);
            f.sk = (f.sk + 1) % MaxSkids; f.skidDirty = true; f.lastSkid = f.clock;
        }

        /// <summary>Drop every particle and skid mark (scene resets, filming cuts).</summary>
        public static void Clear()
        {
            var f = inst; if (f == null) return;
            f.n = 0;
            for (int i = 0; i < f.su1.Length; i++) f.su1[i] = new Vector2(-99f, 0f);
            f.skidDirty = true; f.lastSkid = -99f;
        }

        public static int Count => inst != null ? inst.n : 0;

        // ------------------------------------------------------------------ simulation and drawing
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            clock += dt;

            for (int i = n - 1; i >= 0; i--)
            {
                age[i] += dt;
                if (age[i] >= life[i]) { Kill(i); continue; }
                byte k = kind[i];
                if (k == KSpark) vel[i].y -= 2.2f * dt;
                if (k == KSmoke) vel[i] *= Mathf.Exp(-2.2f * dt);
                pos[i] += vel[i] * dt;
                rot[i] += rv[i] * dt;
            }

            // the player's own eyes (first person only): particles shrink away before they get too close
            bool guard = false; Vector3 eye = default;
            var pd = PlayerDuck.Instance;
            if (pd != null && pd.cam != null && !pd.thirdPerson && ComicPop.Viewer == null) { guard = true; eye = pd.cam.transform.position; }

            for (int i = 0; i < n; i++)
            {
                float t = age[i] / life[i], sc;
                switch (kind[i])
                {
                    case KSmoke:     // pop: swell to 1.35x, then vanish
                        sc = t < 0.18f ? 0.35f + 0.65f * (t / 0.18f) * (1.25f - 0.25f * t / 0.18f)
                           : t < 0.8f ? 1f + 0.35f * (t - 0.18f) / 0.62f
                           : (1.35f + (t - 0.8f) * 1.5f) * (1f - Mathf.Pow((t - 0.8f) / 0.2f, 2f));
                        break;
                    case KSpark: sc = 1f - t * t; break;
                    case KGlint: sc = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.1f)); break;
                    case KBolt: sc = ((int)(age[i] * 30f) % 2 == 1 ? 0.85f : 1.1f) * (1f - t * 0.4f); break;
                    default: sc = 1f - t; break;
                }
                float size = s0[i] * Mathf.Max(0f, sc);
                if (guard)
                {
                    float reach = (pos[i] - eye).magnitude - size * 0.5f * Mathf.Max(1f, stretch[i]);
                    size *= Mathf.Clamp01((reach - MinEyeDistance) / 0.04f);
                }
                var d = dir[i] * stretch[i];
                var u1 = new Vector4(d.x, d.y, d.z, size);
                int v = i * 4;
                for (int c = 0; c < 4; c++)
                {
                    mv[v + c] = pos[i];
                    mu0[v + c] = new Vector4(Corners[c].x, Corners[c].y, rot[i], cell[i]);
                    mu1[v + c] = u1;
                }
            }
            // quads freed this frame collapse to nothing
            for (int i = n; i < drawn; i++) { int v = i * 4; for (int c = 0; c < 4; c++) mu1[v + c] = Vector4.zero; }

            if (n > 0 || drawn > 0)
            {
                // always the whole buffer: a shorter upload would shrink the mesh under its index buffer
                mesh.SetVertices(mv, 0, mv.Length, Quiet);
                mesh.SetUVs(0, mu0, 0, mu0.Length, Quiet);
                mesh.SetUVs(1, mu1, 0, mu1.Length, Quiet);
            }
            drawn = n;
            if (n > 0)
            {
                var rp = new RenderParams(mat) { worldBounds = mesh.bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMesh(in rp, mesh, 0, Matrix4x4.identity);
            }

            if (skidDirty)
            {
                skidMesh.SetVertices(sv, 0, sv.Length, Quiet);
                skidMesh.SetUVs(0, su0, 0, su0.Length, Quiet);
                skidMesh.SetUVs(1, su1, 0, su1.Length, Quiet);
                skidDirty = false;
            }
            if (clock - lastSkid < SkidLife + 0.1f)
            {
                skidMat.SetFloat(FxTimeId, clock);
                var rp = new RenderParams(skidMat) { worldBounds = skidMesh.bounds, shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMesh(in rp, skidMesh, 0, Matrix4x4.identity);
            }
        }

        void Kill(int i)
        {
            int j = --n; if (i == j) return;
            pos[i] = pos[j]; vel[i] = vel[j]; dir[i] = dir[j];
            age[i] = age[j]; life[i] = life[j]; s0[i] = s0[j]; rot[i] = rot[j]; rv[i] = rv[j]; stretch[i] = stretch[j];
            cell[i] = cell[j]; kind[i] = kind[j];
        }

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
