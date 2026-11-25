using System.IO;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// M1 prototype scene: the living room at real scale (grey-box, web layout mirrored to Unity X),
    /// the player's duck with the XR rig, three wandering ducks and a few toys.
    /// </summary>
    public static class SceneBuilderM1
    {
        const string Dir = "Assets/MicroduckVR/Scenes";
        const string MatDir = "Assets/MicroduckVR/Materials/M1";

        [MenuItem("Microduck/Scenes/Build M1 Be The Duck")]
        public static void Build()
        {
            EnsureLayers();
            Directory.CreateDirectory(Dir); Directory.CreateDirectory(MatDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.6f, 0.68f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Hard;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(52f, 150f, 0f);

            BuildLivingRoom();

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);

            MakePlayer(new Vector3(0.2f, 0f, 1.4f), 180f, rigPrefab, map);

            // the flock (placeholder wander AI)
            var spots = new[] { (new Vector3(-0.9f, 0f, 0.6f), 40f), (new Vector3(0.8f, 0f, 0.3f), -60f), (new Vector3(-0.2f, 0f, -0.3f), 170f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var d = MakeDuck("Duck " + (i + 1), spots[i].Item1, spots[i].Item2, rigPrefab, map, false);
                d.AddComponent<WanderDuck>().seed = 11 + i * 7;
            }

            // toys
            MakeToy("Foam cube", PrimitiveType.Cube, new Vector3(0.25f, 0.012f, 1.15f), new Vector3(0.022f, 0.022f, 0.022f), new Color(1f, 0.79f, 0.16f));
            MakeToy("Toy brick", PrimitiveType.Cube, new Vector3(0.05f, 0.01f, 1.1f), new Vector3(0.018f, 0.018f, 0.032f), new Color(0.84f, 0.15f, 0.17f));
            MakeToy("Bead", PrimitiveType.Sphere, new Vector3(0.38f, 0.01f, 1.05f), Vector3.one * 0.02f, new Color(0.39f, 0.82f, 1f));

            string path = Dir + "/M1_BeTheDuck.unity";
            EditorSceneManager.SaveScene(scene, path);
            SceneBuilder.AddToBuild(path);
            Debug.Log("[Microduck] Built " + path);
        }

        internal static PlayerDuck MakePlayer(Vector3 pos, float yawDeg, GameObject rigPrefab, DuckJointMap map)
        {
            Directory.CreateDirectory(MatDir);
            var player = MakeDuck("Player Duck", pos, yawDeg, rigPrefab, map, true);
            var origin = new GameObject("XR Origin");
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            camGo.transform.SetParent(origin.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 60f;
            cam.backgroundColor = MicroduckVR.Comic.ComicLook.Hex("#9AD8F2"); cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            SceneBuilder.AddHeadTracking(camGo);
            var vig = GameObject.CreatePrimitive(PrimitiveType.Quad);
            vig.name = "Comfort Vignette";
            Object.DestroyImmediate(vig.GetComponent<Collider>());
            vig.transform.SetParent(camGo.transform, false);
            vig.transform.localPosition = new Vector3(0f, 0f, 0.5f);
            vig.transform.localScale = Vector3.one * 2.2f;
            var vigMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Vignette.mat");
            if (vigMat == null) { vigMat = new Material(Shader.Find("Microduck/Vignette")); AssetDatabase.CreateAsset(vigMat, MatDir + "/Vignette.mat"); }
            var mr = vig.GetComponent<MeshRenderer>(); mr.sharedMaterial = vigMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var cv = vig.AddComponent<ComfortVignette>();
            var pd = player.AddComponent<PlayerDuck>();
            pd.xrOrigin = origin.transform; pd.cam = cam; pd.vignette = cv;
            // tracked controllers (aim pose) under the origin; the right one points the bag's laser
            var right = new GameObject("Right Hand"); right.transform.SetParent(origin.transform, false);
            SceneBuilder.AddHandTracking(right, "RightHand");
            var left = new GameObject("Left Hand"); left.transform.SetParent(origin.transform, false);
            SceneBuilder.AddHandTracking(left, "LeftHand");
            var bag = player.AddComponent<BagMenu>();
            bag.cam = cam; bag.rightHand = right.transform;
            bag.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MicroduckVR/Fonts/Bangers-Regular.ttf");
            return pd;
        }

        internal static GameObject MakeDuck(string name, Vector3 pos, float yawDeg, GameObject rigPrefab, DuckJointMap map, bool isPlayer)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            go.SetActive(false);
            go.AddComponent<DuckBody>();
            var s = go.AddComponent<DuckSetup>();
            s.rigPrefab = rigPrefab; s.jointMap = map; s.hideHeadFromPlayer = isPlayer;
            go.SetActive(true);
            return go;
        }

        internal static void MakeToy(string name, PrimitiveType type, Vector3 pos, Vector3 size, Color col)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.layer = DuckSetup.LayerToy;
            go.transform.position = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = Mat(name, col);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.01f; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            go.AddComponent<Grabbable>().label = name.ToLowerInvariant();
        }

        // ------------------------------------------------------------------ living room (web layout, X mirrored)
        static void BuildLivingRoom()
        {
            var room = new GameObject("Living Room").transform;
            var wood = Mat("Floor wood", MicroduckVR.Comic.ComicLook.Hex("#E9A85A"));
            var wall = Mat("Wall", MicroduckVR.Comic.ComicLook.Hex("#FFF2DC"));
            var rug = Mat("Rug", MicroduckVR.Comic.ComicLook.Hex("#8E6FD6"));
            var sofaM = Mat("Sofa", MicroduckVR.Comic.ComicLook.Hex("#27B3B0"));
            var tableM = Mat("Table oak", MicroduckVR.Comic.ComicLook.Hex("#C98A55"));
            var shelfM = Mat("Shelf", new Color(0.97f, 0.97f, 0.95f));

            Box(room, "Floor", new Vector3(0f, -0.01f, 0f), new Vector3(6.24f, 0.02f, 5.24f), wood);
            Box(room, "Rug", new Vector3(0f, 0.004f, 0.4f), new Vector3(2.6f, 0.008f, 1.8f), rug);
            const float H = 2.5f, T = 0.12f;
            // north wall (z = -2.56) solid
            Box(room, "Wall N", new Vector3(0f, H / 2f, -2.56f), new Vector3(6.24f, H, T), wall);
            // south wall (z = +2.56), door to the hallway at x in [-0.45, 0.45]
            WallWithGap(room, "Wall S", axisX: true, at: 2.56f, from: -3.12f, to: 3.12f, gapA: -0.45f, gapB: 0.45f, H, T, wall);
            // east wall (x = +3.06 after mirroring): door to the bedroom, z in [-1.2, -0.4]
            WallWithGap(room, "Wall E", axisX: false, at: 3.06f, from: -2.5f, to: 2.5f, gapA: -1.2f, gapB: -0.4f, H, T, wall);
            // west wall (x = -3.06): door to the kitchen, z in [-1.6, -0.8]
            WallWithGap(room, "Wall W", axisX: false, at: -3.06f, from: -2.5f, to: 2.5f, gapA: -1.6f, gapB: -0.8f, H, T, wall);

            // the oak table at the room centre (web: 1.6 x 1.0, 0.75 high)
            Box(room, "Table top", new Vector3(0f, 0.73f, 0f), new Vector3(1.6f, 0.04f, 1.0f), tableM);
            foreach (var lx in new[] { -0.73f, 0.73f }) foreach (var lz in new[] { -0.43f, 0.43f })
                Box(room, "Table leg", new Vector3(lx, 0.355f, lz), new Vector3(0.05f, 0.71f, 0.05f), tableM);
            // sofa against the north wall
            Box(room, "Sofa seat", new Vector3(0f, 0.22f, -2.05f), new Vector3(2.1f, 0.44f, 0.8f), sofaM);
            Box(room, "Sofa back", new Vector3(0f, 0.6f, -2.35f), new Vector3(2.1f, 0.5f, 0.2f), sofaM);
            Box(room, "Sofa arm L", new Vector3(-1.0f, 0.34f, -2.05f), new Vector3(0.18f, 0.68f, 0.8f), sofaM);
            Box(room, "Sofa arm R", new Vector3(1.0f, 0.34f, -2.05f), new Vector3(0.18f, 0.68f, 0.8f), sofaM);
            // bookshelf on the east wall, armchair-ish block on the west
            Box(room, "Bookshelf", new Vector3(2.8f, 0.9f, 1.4f), new Vector3(0.4f, 1.8f, 1.2f), shelfM);
            Box(room, "Armchair", new Vector3(-2.4f, 0.3f, 1.3f), new Vector3(0.8f, 0.6f, 0.8f), sofaM);
            // a low step/ramp a duck can climb
            Box(room, "Book stack", new Vector3(1.2f, 0.015f, 1.0f), new Vector3(0.3f, 0.03f, 0.22f), Mat("Books", new Color(0.18f, 0.44f, 0.84f)));
        }

        static void WallWithGap(Transform parent, string name, bool axisX, float at, float from, float to, float gapA, float gapB, float H, float T, Material m)
        {
            float doorH = 2.0f;
            void Seg(float a, float b, string n, float y0, float y1)
            {
                if (b - a < 0.01f) return;
                float mid = (a + b) / 2f, len = b - a, yc = (y0 + y1) / 2f, h = y1 - y0;
                var pos = axisX ? new Vector3(mid, yc, at) : new Vector3(at, yc, mid);
                var size = axisX ? new Vector3(len, h, T) : new Vector3(T, h, len);
                Box(parent, n, pos, size, m);
            }
            Seg(from, gapA, name + " a", 0f, H);
            Seg(gapB, to, name + " b", 0f, H);
            Seg(gapA, gapB, name + " lintel", doorH, H);
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.layer = DuckSetup.LayerWorld; go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material Mat(string name, Color c)
        {
            string p = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Microduck/ToonLit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
            m.shader = shader;
            m.SetColor("_BaseColor", c);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ layers
        public static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            void Set(int i, string n) { var p = layers.GetArrayElementAtIndex(i); if (p.stringValue != n) p.stringValue = n; }
            Set(DuckSetup.LayerWorld, "World"); Set(DuckSetup.LayerDuck, "Duck"); Set(DuckSetup.LayerToy, "Toy"); Set(DuckSetup.LayerPlayerHead, "PlayerHead"); Set(DuckSetup.LayerProp, "Prop");
            tm.ApplyModifiedProperties();
        }
    }
}
