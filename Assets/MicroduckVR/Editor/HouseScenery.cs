using System.Collections.Generic;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// What you see outside: a ring of comic clouds in the sky, round inked trees and hedges beyond the garden
    /// fence, and the neighbours' houses with chunky roofs, so doorways and the garden look out onto a world
    /// instead of an empty colour. Unity space; all static, no colliders.
    /// </summary>
    public static partial class HouseBuilder
    {
        static void Scenery()
        {
            const string R = "garden";
            var rnd = new System.Random(808);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

            // the sky: a big drum painted with a blue gradient and halftone dots fading up from the horizon
            {
                var skyM = M("sky_drum", "#FFFFFF"); skyM.SetFloat("_Unlit", 1f);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MicroduckVR/Textures/sky_halftone.png");
                if (tex != null) { skyM.SetTexture("_BaseMap", tex); skyM.SetTextureScale("_BaseMap", new Vector2(6f, 1f)); EditorUtility.SetDirty(skyM); }
                const int N = 48; const float SR = 52f, Y0 = -5f, Y1 = 45f;
                var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
                for (int i = 0; i <= N; i++)
                {
                    float a = i / (float)N * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    v.Add(d * SR + Vector3.up * Y0); v.Add(d * SR + Vector3.up * Y1); n.Add(-d); n.Add(-d);
                    uv.Add(new Vector2(i / (float)N, 0.02f)); uv.Add(new Vector2(i / (float)N, 0.97f));
                    if (i < N) { int q = i * 2; t.Add(q); t.Add(q + 1); t.Add(q + 2); t.Add(q + 2); t.Add(q + 1); t.Add(q + 3); t.Add(q); t.Add(q + 2); t.Add(q + 1); t.Add(q + 2); t.Add(q + 3); t.Add(q + 1); }
                }
                int c0 = v.Count; v.Add(Vector3.up * Y1); n.Add(Vector3.down); uv.Add(new Vector2(0.5f, 0.97f));
                for (int i = 0; i < N; i++) { t.Add(c0); t.Add(i * 2 + 3); t.Add(i * 2 + 1); t.Add(c0); t.Add(i * 2 + 1); t.Add(i * 2 + 3); }
                var mesh = new Mesh { name = "sky drum" }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
                var sky = new GameObject("Sky drum") { layer = DuckSetup.LayerWorld };
                sky.transform.SetParent(rooms[R], false); sky.transform.position = new Vector3(0f, 0f, 3f);
                sky.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
                var smr = sky.AddComponent<MeshRenderer>(); smr.sharedMaterial = skyM; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false;
            }

            // clouds: clusters of fat white puffs, flat-lit (the ink pass draws their outlines)
            var cloudMat = M("cloud", "#FFFFFF"); cloudMat.SetFloat("_Unlit", 1f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + Rf(-0.15f, 0.15f), d = Rf(32f, 46f);
                var c = NewProp(R, "Cloud", new Vector3(Mathf.Cos(a) * d, Rf(9f, 16f), 3f + Mathf.Sin(a) * d), Rf(0f, 360f));
                int puffs = 4 + rnd.Next(4); float s = Rf(2.2f, 3.6f);
                for (int k = 0; k < puffs; k++)
                {
                    float x = (k - puffs / 2f) * s * 0.55f + Rf(-0.3f, 0.3f), y = Rf(0f, 0.6f) * s - Mathf.Abs(k - puffs / 2f) * 0.25f * s;
                    var b = PBall(c, new Vector3(x, y, Rf(-0.4f, 0.4f) * s), new Vector3(s * Rf(1f, 1.4f), s * Rf(0.8f, 1.1f), s * Rf(0.9f, 1.2f)), "#FFFFFF");
                    b.GetComponent<MeshRenderer>().sharedMaterial = cloudMat;
                }
                foreach (var mr in c.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Stat(c, R);
            }

            // hedges along the lawn beyond the fence, and round trees
            void Hedge(Vector3 a, Vector3 b)
            {
                float L = Vector3.Distance(a, b); int n = Mathf.CeilToInt(L / 0.7f);
                var h = NewProp(R, "Hedge", a);
                for (int i = 0; i <= n; i++)
                {
                    var p = Vector3.Lerp(a, b, i / (float)n) - a;
                    PBall(h, p + Vector3.up * 0.45f, new Vector3(Rf(0.9f, 1.2f), Rf(0.8f, 1.1f), Rf(0.8f, 1.0f)), i % 2 == 0 ? "#3E9A45" : "#56B65A");
                }
                Stat(h, R);
            }
            Hedge(new Vector3(-11f, 0f, 13f), new Vector3(11f, 0f, 13f));
            Hedge(new Vector3(-11f, 0f, -1f), new Vector3(-11f, 0f, 13f));
            Hedge(new Vector3(11f, 0f, -1f), new Vector3(11f, 0f, 13f));

            void Tree(Vector3 p, float s)
            {
                var t = NewProp(R, "Tree", p, Rf(0f, 360f));
                PCyl(t, new Vector3(0f, 1.1f * s, 0f), 0.22f * s, 2.2f * s, "#A8733F", default, 0.16f * s, 14);
                PBall(t, new Vector3(0f, 2.6f * s, 0f), Vector3.one * 2.2f * s, "#3E9A45");
                PBall(t, new Vector3(0.7f * s, 2.3f * s, 0.3f * s), Vector3.one * 1.5f * s, "#56B65A");
                PBall(t, new Vector3(-0.6f * s, 2.2f * s, -0.4f * s), Vector3.one * 1.5f * s, "#4AA84E");
                PBall(t, new Vector3(0.1f * s, 3.4f * s, -0.1f * s), Vector3.one * 1.3f * s, "#56B65A");
                if (rnd.Next(2) == 0) for (int k = 0; k < 4; k++) PBall(t, new Vector3(Rf(-0.9f, 0.9f) * s, Rf(2.1f, 3.3f) * s, Rf(-0.9f, 0.9f) * s) + new Vector3(0f, 0f, 0f), Vector3.one * 0.22f * s, "#FF5D73");   // apples
                Stat(t, R);
            }
            foreach (var (x, z, s) in new[] { (-8f, 10f, 1.1f), (-4.5f, 11.5f, 0.9f), (1f, 11f, 1.2f), (5.5f, 10.5f, 1f), (8.5f, 12f, 1.15f), (-9.5f, 4f, 1f), (9.5f, 5f, 1.05f), (-9.2f, 1f, 0.9f), (9.3f, 0.5f, 0.95f) })
                Tree(new Vector3(x, 0f, z), s);

            // the neighbours' houses beyond the hedge
            string[] walls = { "#FFC43D", "#8FD3B6", "#FF8A9A", "#9AD8F2", "#FFF6E4" };
            string[] roofs = { "#FF5D73", "#7B61FF", "#3A86FF", "#1FA6A0", "#FF8A3D" };
            int hi = 0;
            foreach (var (x, z, yaw) in new[] { (-14f, 18f, 10f), (-5f, 19f, -5f), (5f, 18.5f, 4f), (14f, 17.5f, -12f), (-18f, 6f, 80f), (18f, 7f, -85f) })
            {
                var h = NewProp(R, "Neighbour house", new Vector3(x, 0f, z), yaw);
                float w = Rf(5f, 7f), d = Rf(4.5f, 6f), ht = Rf(3.2f, 4.2f);
                PBox(h, new Vector3(0f, ht / 2f, 0f), new Vector3(w, ht, d), walls[hi % walls.Length], 0.06f);
                // a chunky gable roof from two slabs
                foreach (var s in new[] { -1f, 1f })
                    PBox(h, new Vector3(0f, ht + 0.9f, s * d * 0.27f), new Vector3(w + 0.6f, 0.35f, d * 0.62f), roofs[hi % roofs.Length], 0.3f, new Vector3(s * -32f, 0f, 0f));
                PBox(h, new Vector3(0f, 0.95f, -d / 2f - 0.02f), new Vector3(1.0f, 1.9f, 0.08f), "#A8733F", 0.2f);
                foreach (var sx in new[] { -1f, 1f })
                {
                    PBox(h, new Vector3(sx * w * 0.3f, ht * 0.6f, -d / 2f - 0.02f), new Vector3(1.1f, 0.9f, 0.08f), "#FFFFFF", 0.2f);
                    PBox(h, new Vector3(sx * w * 0.3f, ht * 0.6f, -d / 2f - 0.05f), new Vector3(0.9f, 0.7f, 0.06f), "#9AD8F2", 0.2f);
                }
                PBox(h, new Vector3(w * 0.25f, ht + 1.5f, d * 0.1f), new Vector3(0.5f, 1.2f, 0.5f), "#C9622A", 0.2f);   // chimney
                Stat(h, R); hi++;
            }
        }

        /// <summary>Puffy bush: three or four lumpy balls in two greens.</summary>
        static Transform Bush(string room, Vector3 pos, float s)
        {
            var b = NewProp(room, "Bush", pos, Rf(0f, 360f));
            PBall(b, new Vector3(0f, 0.12f * s, 0f), new Vector3(0.28f, 0.24f, 0.26f) * s, "#3E9A45");
            PBall(b, new Vector3(0.1f * s, 0.2f * s, 0.04f * s), new Vector3(0.2f, 0.2f, 0.2f) * s, "#56B65A");
            PBall(b, new Vector3(-0.09f * s, 0.18f * s, -0.05f * s), new Vector3(0.18f, 0.18f, 0.18f) * s, "#4AA84E");
            PBall(b, new Vector3(0f, 0.27f * s, 0f), new Vector3(0.14f, 0.13f, 0.14f) * s, "#6CBF4F");
            return b;
        }

        /// <summary>Comic flower: a chunky stem with two leaves, five puffy petals round a yellow centre.</summary>
        static Transform Flower(string room, Vector3 pos, string petal, float s = 1f)
        {
            var f = NewProp(room, "Flower", pos, Rf(0f, 360f));
            float h = 0.12f * s;
            PCyl(f, new Vector3(0f, h / 2f, 0f), 0.006f * s, h, "#3E9A45", default, 0.0045f * s, 8);
            foreach (var sd in new[] { -1f, 1f }) PBall(f, new Vector3(sd * 0.016f * s, h * 0.4f, 0f), new Vector3(0.03f, 0.008f, 0.014f) * s, "#56B65A", new Vector3(0f, 0f, sd * 25f));
            var head = new GameObject("head").transform; head.SetParent(f, false); head.localPosition = new Vector3(0f, h, 0f); head.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            for (int k = 0; k < 5; k++) { float a = k * 72f * Mathf.Deg2Rad; PBall(head, new Vector3(Mathf.Cos(a) * 0.017f * s, 0f, Mathf.Sin(a) * 0.017f * s), new Vector3(0.024f, 0.012f, 0.024f) * s, petal); }
            PBall(head, new Vector3(0f, 0.004f * s, 0f), new Vector3(0.018f, 0.012f, 0.018f) * s, "#FFC43D");
            return f;
        }
    }
}
