using MicroduckVR.Comic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MicroduckVR.EditorTools
{
    /// <summary>Renders the same sofa + coffee table + armchair in three furniture styles, for picking one.</summary>
    public static class FurnitureStyles
    {
        public enum Kind { Chunky, HandDrawn, Retro, Boxy }
        /// <summary>Set by the house builder so generated meshes are saved as assets (scenes keep them).</summary>
        internal static System.Func<Mesh, Mesh> MeshSaver = m => m;
        /// <summary>Material factory (hex, key); the house builder swaps in one that saves assets.</summary>
        internal static System.Func<string, string, Material> MatFactory = (hex, key) => ComicLook.ToonMaterial(ComicLook.Hex(hex), key);
        static Material Mat(string hex, string key) => MatFactory(hex, key);

        [MenuItem("Microduck/Debug/Capture Furniture Styles")]
        public static void Capture()
        {
            var prev = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard;
                sun.transform.rotation = Quaternion.Euler(58f, -40f, 0f);
                var floorMat = Mat("#E9A85A", "fs_floor");
                var wallMat = Mat("#FFF0D2", "fs_wall");
                var kinds = new[] { Kind.Boxy, Kind.Chunky, Kind.HandDrawn, Kind.Retro };
                for (int i = 0; i < kinds.Length; i++)
                {
                    var origin = new Vector3(i * 20f, 0f, 120f);
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = origin + new Vector3(0f, -0.01f, 0f); floor.transform.localScale = new Vector3(6f, 0.02f, 6f);
                    floor.GetComponent<Renderer>().sharedMaterial = floorMat;
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.position = origin + new Vector3(0f, 1.25f, -1.3f); wall.transform.localScale = new Vector3(6f, 2.5f, 0.12f);
                    wall.GetComponent<Renderer>().sharedMaterial = wallMat;
                    var set = new GameObject(kinds[i] + " set").transform; set.position = origin;
                    BuildSofa(set, new Vector3(0f, 0f, -0.75f), kinds[i]);
                    BuildCoffeeTable(set, new Vector3(0f, 0f, 0.35f), kinds[i]);
                    BuildArmchair(set, new Vector3(1.75f, 0f, 0.1f), kinds[i]);
                    string n = kinds[i].ToString().ToLowerInvariant();
                    DebugCapture.Shoot("style_" + n + "_human", origin + new Vector3(-1.9f, 1.55f, 2.5f), origin + new Vector3(0.3f, 0.35f, -0.2f), 1200, 800, 50f);
                    DebugCapture.Shoot("style_" + n + "_duck", origin + new Vector3(-0.55f, 0.22f, 1.35f), origin + new Vector3(0.1f, 0.4f, -0.5f), 1200, 800, 90f);
                }
                Debug.Log("[Microduck] furniture styles captured");
            }
            finally
            {
                SceneManager.SetActiveScene(prev);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ------------------------------------------------------------------ style presets
        static ComicMesh.Style Frame(Kind k, int seed) => k switch
        {
            Kind.Chunky => new ComicMesh.Style { radius = 0.09f, segments = 4, puff = 0.10f, seed = seed },
            Kind.HandDrawn => new ComicMesh.Style { radius = 0.035f, segments = 3, puff = 0.06f, wobble = 0.018f, skew = 0.015f, taperTop = 0.04f, seed = seed },
            Kind.Retro => new ComicMesh.Style { radius = 0.025f, segments = 3, puff = 0.02f, taperTop = 0.03f, seed = seed },
            _ => new ComicMesh.Style { radius = 0.002f, segments = 1, seed = seed },
        };
        static ComicMesh.Style Cushion(Kind k, int seed) => k switch
        {
            Kind.Chunky => new ComicMesh.Style { radius = 0.1f, segments = 5, puff = 0.35f, seed = seed },
            Kind.HandDrawn => new ComicMesh.Style { radius = 0.06f, segments = 4, puff = 0.22f, wobble = 0.014f, skew = 0.01f, seed = seed },
            Kind.Retro => new ComicMesh.Style { radius = 0.05f, segments = 4, puff = 0.12f, seed = seed },
            _ => new ComicMesh.Style { radius = 0.002f, segments = 1, seed = seed },
        };

        static GameObject Part(Transform parent, Mesh mesh, Vector3 pos, Material mat, float yawDeg = 0f, float tiltDeg = 0f)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(tiltDeg, yawDeg, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = MeshSaver(mesh);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.layer = MicroduckVR.Duck.DuckSetup.LayerWorld; go.isStatic = true;
            return go;
        }

        static void Legs(Transform t, Kind k, Vector3 centre, float w, float d, float h, Material mat)
        {
            if (k == Kind.Boxy) return;
            float inset = k == Kind.Retro ? 0.08f : 0.1f;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                var p = centre + new Vector3(sx * (w / 2f - inset), h / 2f, sz * (d / 2f - inset));
                if (k == Kind.Chunky) Part(t, ComicMesh.Leg(h, 0.045f, 0.035f), p, mat);
                else if (k == Kind.HandDrawn) Part(t, ComicMesh.Leg(h, 0.03f, 0.018f), p, mat, 0f, sz * 6f);
                else
                {
                    // splayed mid-century legs
                    var go = Part(t, ComicMesh.Leg(h * 1.05f, 0.022f, 0.011f), p + new Vector3(sx * 0.02f, 0f, sz * 0.02f), mat);
                    go.transform.localRotation = Quaternion.Euler(sz * 10f, 0f, -sx * 10f);
                }
            }
        }

        internal static void BuildSofa(Transform set, Vector3 at, Kind k)
        {
            var t = new GameObject("Sofa").transform; t.SetParent(set, false); t.localPosition = at;
            var body = Mat("#27B3B0", "fs_sofa");
            var cush = Mat("#3FC9C4", "fs_sofa_cushion");
            var leg = Mat("#3D3B4F", "fs_leg");
            float L = 2.1f, D = 0.9f;
            float lift = k == Kind.Boxy ? 0f : (k == Kind.Retro ? 0.16f : 0.08f);
            if (k == Kind.Boxy)
            {
                Part(t, ComicMesh.RoundedBox(new Vector3(L, 0.44f, D), Frame(k, 1)), new Vector3(0, 0.22f, 0), body);
                Part(t, ComicMesh.RoundedBox(new Vector3(L, 0.5f, 0.2f), Frame(k, 2)), new Vector3(0, 0.6f, -D / 2 + 0.1f), body);
                foreach (var s in new[] { -1f, 1f }) Part(t, ComicMesh.RoundedBox(new Vector3(0.16f, 0.6f, D), Frame(k, 3)), new Vector3(s * (L / 2 - 0.08f), 0.3f, 0), body);
                return;
            }
            Legs(t, k, Vector3.zero, L, D, lift, leg);
            // base
            Part(t, ComicMesh.RoundedBox(new Vector3(L, 0.2f, D), Frame(k, 1)), new Vector3(0, lift + 0.1f, 0), body);
            // three seat cushions
            float cw = (L - 0.36f) / 3f;
            for (int i = 0; i < 3; i++)
                Part(t, ComicMesh.RoundedBox(new Vector3(cw - 0.02f, 0.16f, D - 0.22f), Cushion(k, 10 + i)), new Vector3((i - 1) * cw, lift + 0.27f, 0.08f), cush);
            // back: frame + three back cushions leaning back
            Part(t, ComicMesh.RoundedBox(new Vector3(L, 0.48f, 0.2f), Frame(k, 2)), new Vector3(0, lift + 0.4f, -D / 2 + 0.1f), body, 0f, -6f);
            for (int i = 0; i < 3; i++)
                Part(t, ComicMesh.RoundedBox(new Vector3(cw - 0.02f, 0.38f, 0.16f), Cushion(k, 20 + i)), new Vector3((i - 1) * cw, lift + 0.5f, -D / 2 + 0.25f), cush, 0f, -10f);
            // arms: chunky rounded, hand-drawn bulging, retro thin and low
            float armW = k == Kind.Retro ? 0.12f : 0.2f, armH = k == Kind.Retro ? 0.32f : 0.42f;
            foreach (var s in new[] { -1f, 1f })
                Part(t, ComicMesh.RoundedBox(new Vector3(armW, armH, D), Frame(k, 30 + (int)s)), new Vector3(s * (L / 2 - armW / 2), lift + armH / 2 + 0.06f, 0), body);
        }

        internal static void BuildCoffeeTable(Transform set, Vector3 at, Kind k)
        {
            var t = new GameObject("Coffee table").transform; t.SetParent(set, false); t.localPosition = at;
            var top = Mat("#FFC43D", "fs_ct");
            var leg = Mat("#3D3B4F", "fs_leg");
            float w = 1.0f, d = 0.5f, h = 0.4f;
            if (k == Kind.Boxy)
            {
                Part(t, ComicMesh.RoundedBox(new Vector3(w, 0.035f, d), Frame(k, 40)), new Vector3(0, h - 0.0175f, 0), top);
                foreach (var sx in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
                    Part(t, ComicMesh.RoundedBox(new Vector3(0.04f, h - 0.035f, 0.04f), Frame(k, 41)), new Vector3(sx * 0.44f, (h - 0.035f) / 2f, sz * 0.19f), leg);
                return;
            }
            var topStyle = Frame(k, 40); topStyle.radius = k == Kind.Chunky ? 0.03f : 0.015f; topStyle.puff = k == Kind.Chunky ? 0.3f : topStyle.puff;
            float th = k == Kind.Chunky ? 0.07f : 0.045f;
            Part(t, ComicMesh.RoundedBox(new Vector3(w, th, d), topStyle), new Vector3(0, h - th / 2f, 0), top);
            if (k == Kind.Retro)
            {
                var shelf = Frame(k, 42);
                Part(t, ComicMesh.RoundedBox(new Vector3(w - 0.2f, 0.02f, d - 0.12f), shelf), new Vector3(0, 0.14f, 0), top);
            }
            Legs(t, k, Vector3.zero, w, d, h - th, leg);
        }

        internal static void BuildArmchair(Transform set, Vector3 at, Kind k)
        {
            var t = new GameObject("Armchair").transform; t.SetParent(set, false); t.localPosition = at; t.localRotation = Quaternion.Euler(0f, -35f, 0f);
            var body = Mat("#FF5D73", "fs_chair");
            var cush = Mat("#FF8A9A", "fs_chair_cushion");
            var leg = Mat("#3D3B4F", "fs_leg");
            float W = 0.85f, D = 0.8f;
            if (k == Kind.Boxy)
            {
                Part(t, ComicMesh.RoundedBox(new Vector3(W, 0.6f, D), Frame(k, 50)), new Vector3(0, 0.3f, 0), body);
                return;
            }
            float lift = k == Kind.Retro ? 0.18f : 0.08f;
            Legs(t, k, Vector3.zero, W, D, lift, leg);
            Part(t, ComicMesh.RoundedBox(new Vector3(W, 0.2f, D), Frame(k, 51)), new Vector3(0, lift + 0.1f, 0), body);
            Part(t, ComicMesh.RoundedBox(new Vector3(W - 0.3f, 0.16f, D - 0.2f), Cushion(k, 52)), new Vector3(0, lift + 0.27f, 0.06f), cush);
            Part(t, ComicMesh.RoundedBox(new Vector3(W, 0.55f, 0.18f), Frame(k, 53)), new Vector3(0, lift + 0.45f, -D / 2 + 0.09f), body, 0f, -8f);
            foreach (var s in new[] { -1f, 1f })
                Part(t, ComicMesh.RoundedBox(new Vector3(0.15f, 0.38f, D), Frame(k, 54 + (int)s)), new Vector3(s * (W / 2 - 0.075f), lift + 0.25f, 0), body);
        }
    }
}
