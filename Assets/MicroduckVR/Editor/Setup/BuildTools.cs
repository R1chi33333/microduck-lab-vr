using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace MicroduckVR.EditorTools
{
    /// <summary>Windows build of the House scene (OpenXR, for Quest over Link) into Builds/Windows.</summary>
    public static class BuildTools
    {
        public const string Exe = "Builds/Windows/MicroduckLabVR.exe";

        [MenuItem("Microduck/Build Windows")]
        public static void BuildWindows()
        {
            PlayerSettings.productName = "Microduck Lab VR";
            PlayerSettings.companyName = "Yutong Jin";
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            AlwaysInclude("Universal Render Pipeline/Unlit");   // the mirror glass material is made at runtime
            AlwaysInclude("Microduck/ToonLit");
            AlwaysInclude("Microduck/ComicFX");                  // skate effects: materials made at runtime (ComicFX.cs)
            AlwaysInclude("Microduck/ComicSkid");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilderHouse.ScenePath },
                locationPathName = Exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log($"[Microduck] build {s.result}: {s.totalSize / 1e6:F0} MB, {s.totalTime.TotalSeconds:F0} s, {s.totalErrors} errors -> {System.IO.Path.GetFullPath(Exe)}");
        }

        static void AlwaysInclude(string shaderName)
        {
            var shader = Shader.Find(shaderName); if (shader == null) return;
            var gs = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs); var arr = so.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            arr.InsertArrayElementAtIndex(arr.arraySize); arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
        }
    }
}
