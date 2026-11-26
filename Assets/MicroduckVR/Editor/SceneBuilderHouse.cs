using System.IO;
using MicroduckVR.Duck;
using MicroduckVR.Audio;
using MicroduckVR.Flock;
using MicroduckVR.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>The main scene: the whole house, the player's duck, a flock spread over the rooms, toys.</summary>
    public static class SceneBuilderHouse
    {
        public const string ScenePath = "Assets/MicroduckVR/Scenes/House.unity";

        [MenuItem("Microduck/Scenes/Build House (main)")]
        public static void Build()
        {
            SceneBuilderM1.EnsureLayers();
            Directory.CreateDirectory("Assets/MicroduckVR/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(64f, 205f, 0f);   // sunlight comes in from the garden side

            // soft fill without extra lights: a three-colour ambient (cool sky above, neutral walls, warm floor
            // bounce from below). ToonLit reads its hue and up/down gradient (normalised, so the bands stay hard)
            // and leans the shadow sides of things towards it, so shaded walls, floors and undersides differ.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.74f, 0.80f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
            RenderSettings.ambientGroundColor = new Color(0.82f, 0.64f, 0.48f);
            RenderSettings.ambientIntensity = 1f;
            DynamicGI.UpdateEnvironment();

            HouseBuilder.Build();

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            var player = SceneBuilderM1.MakePlayer(new Vector3(-0.2f, 0f, 1.5f), 180f, rigPrefab, map);
            var pa = player.gameObject.AddComponent<DuckAudio>(); pa.seed = 3; pa.idleChirps = false;

            // the flock: two in the living room, one in each other room (web spawn points, X mirrored)
            var flock = new (string room, Vector3 pos, float yaw)[]
            {
                ("living", new Vector3(0.9f, 0f, 0.6f), 40f), ("living", new Vector3(1.6f, 0f, -1.4f), -60f),
                ("kitchen", new Vector3(-3.8f, 0f, 1.6f), 0f), ("bedroom", new Vector3(4.2f, 0f, 0.4f), 0f),
                ("hallway", new Vector3(2.5f, 0f, 3.22f), 90f), ("garden", new Vector3(0.6f, 0f, 5.2f), 0f),
            };
            string[] skins = { "cream", "lavender", "sky", "graphite", "cream", "sky" };
            for (int i = 0; i < flock.Length; i++)
            {
                var (room, pos, yaw) = flock[i];
                var d = SceneBuilderM1.MakeDuck("Duck " + (i + 1) + " (" + room + ")", pos, yaw, rigPrefab, map, false);
                d.GetComponent<DuckSetup>().skin = skins[i];
                var brain = d.AddComponent<DuckBrain>();
                brain.seed = 11 + i * 7; brain.index = i;
                d.AddComponent<DuckAudio>().seed = 11 + i * 7;
            }

            // thought bubbles over the flock
            var tb = new GameObject("Thought Bubbles").AddComponent<ThoughtBubbles>();
            tb.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/MicroduckVR/Fonts/Bangers-Regular.ttf");
            tb.viewer = player.cam;

            // toys from the bag catalogue, out around the house (built when the scene starts)
            var spawner = new GameObject("Toys").AddComponent<ToySpawner>();
            spawner.items = new[]
            {
                T("toy_duck", -0.9f, 0f, 1.0f, 30f), T("foam_cube", -1.2f, 0f, 1.3f), T("toy_brick", -1.0f, 0f, 1.55f, 20f), T("ring", -0.7f, 0f, -1.0f),
                T("pencil", 0.0f, 0.75f, 0.1f, 70f), T("cookie", -0.2f, 0.75f, 0.25f), T("sock_ball", 1.6f, 0f, 1.25f), T("wicker_ball", 2.3f, 0f, 0.2f),
                T("chess_pawn", 1.4f, 0.4f, -0.85f), T("chess_knight", 1.52f, 0.4f, -0.8f, 200f),
                T("strawberry", -4.3f, 0f, 1.7f), T("grape", -5.9f, 0f, 1.5f), T("bread_crust", -4.6f, 0f, 1.15f, 40f), T("cracker", -5.6f, 0f, -0.3f, 15f),
                T("toast", -4.2f, 0f, -0.6f, 60f), T("croissant", -5.6f, 0.765f, 0.6f, 100f), T("bottle_cap", -6.2f, 0f, 0.0f), T("button", -3.7f, 0f, -0.3f),
                T("toy_figure", 3.5f, 0f, -1.6f, 150f), T("crayon_red", 3.4f, 0f, 0.6f, 20f), T("crayon_green", 3.45f, 0f, 0.75f, 60f),
                T("rubber_duck_pink", 5.3f, 0.165f, -1.9f, 250f), T("key", 6.55f, 0.645f, -0.35f, 30f),
                T("rubber_duck_blue", -0.8f, 0f, 3.3f, 90f), T("toy_car", 1.0f, 0f, 3.35f, 80f),
                T("pebble", -0.6f, 0f, 6.1f), T("leaf", 1.5f, 0f, 5.5f, 40f), T("rubber_duck_mint", -1.0f, 0f, 5.45f, 200f), T("crayon", 0.4f, 0f, 6.3f, 120f),
            };

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.AddToBuild(ScenePath);
            Debug.Log("[Microduck] Built " + ScenePath);
        }

        static ToySpawner.Item T(string kind, float x, float y, float z, float yaw = 0f) => new ToySpawner.Item { kind = kind, pos = new Vector3(x, y, z), yaw = yaw };
    }
}
