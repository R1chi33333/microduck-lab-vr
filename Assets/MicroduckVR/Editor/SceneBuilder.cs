using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace MicroduckVR.EditorTools
{
    /// <summary>Builds scenes in code so they stay reproducible.</summary>
    public static class SceneBuilder
    {
        const string SceneDir = "Assets/MicroduckVR/Scenes";

        [MenuItem("Microduck/Scenes/Build M0 Link Test")]
        public static void BuildM0()
        {
            Directory.CreateDirectory(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Hard;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            var colours = new[] { new Color(1f, 0.54f, 0.12f), new Color(1f, 0.79f, 0.16f), new Color(0.39f, 0.82f, 1f), new Color(0.75f, 0.35f, 0.95f) };
            for (int i = 0; i < 4; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "Cube " + i;
                float a = i * Mathf.PI * 0.5f;
                c.transform.position = new Vector3(Mathf.Sin(a) * 1.5f, 0.25f, Mathf.Cos(a) * 1.5f);
                c.transform.localScale = Vector3.one * 0.5f;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = colours[i] };
                AssetDatabase.CreateAsset(mat, $"{SceneDir}/M0_Cube{i}.mat");
                c.GetComponent<Renderer>().sharedMaterial = mat;
            }

            var origin = new GameObject("XR Origin");
            origin.transform.position = new Vector3(0f, 1.2f, 0f);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(origin.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            camGo.AddComponent<AudioListener>();
            AddHeadTracking(camGo);

            string path = SceneDir + "/M0_LinkTest.unity";
            EditorSceneManager.SaveScene(scene, path);
            AddToBuild(path);
            Debug.Log("[Microduck] Built " + path);
        }

        public static TrackedPoseDriver AddHeadTracking(GameObject camGo)
        {
            var tpd = camGo.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            var pos = new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            var rot = new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            tpd.positionInput = new InputActionProperty(pos);
            tpd.rotationInput = new InputActionProperty(rot);
            return tpd;
        }

        public static void AddToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == path)) return;
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
