using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Installs the comic look in the URP renderer: a Full Screen Pass feature running the ink outline
    /// (needs depth + normals), hard 4K main-light shadows. Safe to run again.
    /// </summary>
    public static class ComicSetup
    {
        const string RendererPath = "Assets/MicroduckVR/Settings/MicroduckRenderer.asset";
        const string PipePath = "Assets/MicroduckVR/Settings/MicroduckURP.asset";
        public const string OutlineMatPath = "Assets/MicroduckVR/Materials/ComicOutline.mat";

        [MenuItem("Microduck/Setup/Install Comic Look")]
        public static void Install()
        {
            System.IO.Directory.CreateDirectory("Assets/MicroduckVR/Materials");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Microduck/ComicOutline"));
                AssetDatabase.CreateAsset(mat, OutlineMatPath);
            }

            mat.SetFloat("_LineWidth", 2.7f);
            mat.SetFloat("_FadeStart", 16f); mat.SetFloat("_FadeEnd", 70f);
            mat.SetFloat("_GrassInkNear", 1.0f); mat.SetFloat("_GrassInkFar", 3.0f);
            mat.SetFloat("_NormalThreshold", 0.58f);
            mat.SetFloat("_DepthThreshold", 0.07f);
            EditorUtility.SetDirty(mat);

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) { Debug.LogError("[Microduck] renderer data missing: run Configure Project first"); return; }
            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == "Comic Outline");
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "Comic Outline";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var so = new SerializedObject(data);
                var list = so.FindProperty("m_RendererFeatures");
                var map = so.FindProperty("m_RendererFeatureMap");
                list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedProperties();
            }
            feature.passMaterial = mat;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            feature.fetchColorBuffer = true;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);

            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipePath);
            if (pipe != null)
            {
                pipe.mainLightShadowmapResolution = 4096;
                pipe.shadowDistance = 10f;
                pipe.supportsCameraDepthTexture = true;
                EditorUtility.SetDirty(pipe);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Microduck] Comic look installed (outline feature, hard 4K shadows).");
        }

        // A/B switches for the surface print and the fill terms (shader globals, editor session and play mode)
        [MenuItem("Microduck/Debug/Comic Look/Toggle Halftone + Hatching")]
        static void TogglePrint() => Toggle("_ComicPatternsOff", "halftone + hatching");
        [MenuItem("Microduck/Debug/Comic Look/Toggle Hemisphere Fill")]
        static void ToggleFill() => Toggle("_ComicFillOff", "hemisphere fill");
        [MenuItem("Microduck/Debug/Comic Look/Toggle Floor Contact")]
        static void ToggleContact() => Toggle("_ComicContactOff", "floor contact darkening");
        static void Toggle(string id, string what)
        {
            bool off = Shader.GetGlobalFloat(id) < 0.5f;
            Shader.SetGlobalFloat(id, off ? 1f : 0f);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            Debug.Log($"[Microduck] {what}: {(off ? "off" : "on")}");
        }
    }
}
