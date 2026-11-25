using System.IO;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
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

            HouseBuilder.Build();

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            SceneBuilderM1.MakePlayer(new Vector3(-0.2f, 0f, 1.5f), 180f, rigPrefab, map);

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
                var w = d.AddComponent<WanderDuck>();
                w.seed = 11 + i * 7;
                var r = HouseBuilder.RoomRects[room];
                w.boundsMin = new Vector2(r.x, r.y); w.boundsMax = new Vector2(r.z, r.w);
            }

            // toys: some on the living floor, some in the kitchen and bedroom (X mirrored from the web props list)
            SceneBuilderM1.MakeToy("Foam cube", PrimitiveType.Cube, new Vector3(-0.1f, 0.012f, 1.25f), Vector3.one * 0.022f, new Color(1f, 0.79f, 0.16f));
            SceneBuilderM1.MakeToy("Toy brick", PrimitiveType.Cube, new Vector3(-0.35f, 0.01f, 1.2f), new Vector3(0.018f, 0.018f, 0.032f), new Color(0.84f, 0.15f, 0.17f));
            SceneBuilderM1.MakeToy("Bead", PrimitiveType.Sphere, new Vector3(0.05f, 0.01f, 1.15f), Vector3.one * 0.02f, new Color(0.39f, 0.82f, 1f));
            SceneBuilderM1.MakeToy("Strawberry", PrimitiveType.Sphere, new Vector3(-4.3f, 0.011f, 1.7f), Vector3.one * 0.022f, new Color(1f, 0.36f, 0.45f));
            SceneBuilderM1.MakeToy("Grape", PrimitiveType.Sphere, new Vector3(-5.9f, 0.01f, 1.5f), Vector3.one * 0.018f, new Color(0.48f, 0.38f, 1f));
            SceneBuilderM1.MakeToy("Toy figure", PrimitiveType.Capsule, new Vector3(4.3f, 0.02f, 0.9f), new Vector3(0.016f, 0.02f, 0.016f), new Color(0.12f, 0.65f, 0.63f));
            SceneBuilderM1.MakeToy("Crayon", PrimitiveType.Cube, new Vector3(4.0f, 0.006f, 1.6f), new Vector3(0.012f, 0.012f, 0.06f), new Color(0.84f, 0.2f, 0.2f));
            SceneBuilderM1.MakeToy("Pebble", PrimitiveType.Sphere, new Vector3(-0.6f, 0.01f, 6.1f), new Vector3(0.022f, 0.014f, 0.02f), new Color(0.8f, 0.78f, 0.85f));

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.AddToBuild(ScenePath);
            Debug.Log("[Microduck] Built " + ScenePath);
        }
    }
}
