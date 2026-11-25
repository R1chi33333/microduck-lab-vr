using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEditor.XR.OpenXR.Features;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// One-shot project configuration for Quest Pro over Link (Windows, OpenXR):
    /// URP asset (MSAA 4x, hard shadows), OpenXR loader on Standalone, Touch Pro + Touch
    /// interaction profiles, Single Pass Instanced. Safe to run again.
    /// Batch: Unity.exe -batchmode -projectPath . -executeMethod MicroduckVR.EditorTools.ProjectSetup.Configure -quit
    /// </summary>
    public static class ProjectSetup
    {
        const string SettingsDir = "Assets/MicroduckVR/Settings";

        [MenuItem("Microduck/Setup/Configure Project (URP + OpenXR)")]
        public static void Configure()
        {
            Directory.CreateDirectory(SettingsDir);
            ConfigureUrp();
            ConfigurePlayer();
            ConfigureXr();
            AssetDatabase.SaveAssets();
            Debug.Log("[Microduck] Project configured.");
        }

        static void ConfigureUrp()
        {
            string rendererPath = SettingsDir + "/MicroduckRenderer.asset";
            string pipePath = SettingsDir + "/MicroduckURP.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipePath);
            if (pipe == null)
            {
                pipe = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipe, pipePath);
            }
            pipe.msaaSampleCount = 4;
            pipe.renderScale = 1f;
            pipe.supportsHDR = false;
            pipe.shadowDistance = 12f;
            pipe.shadowCascadeCount = 2;
            pipe.supportsCameraDepthTexture = true;
            pipe.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(pipe);

            GraphicsSettings.defaultRenderPipeline = pipe;
            var names = QualitySettings.names;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipe;
                QualitySettings.vSyncCount = 0;          // the XR runtime paces frames
                QualitySettings.antiAliasing = 4;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "Yutong Jin";
            PlayerSettings.productName = "Microduck Lab VR";
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.runInBackground = true;
        }

        static void ConfigureXr()
        {
            const BuildTargetGroup group = BuildTargetGroup.Standalone;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(general.AssignedSettings, "UnityEngine.XR.OpenXR.OpenXRLoader", group);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (openxr == null) { Debug.LogError("[Microduck] OpenXR settings not found for Standalone."); return; }
            openxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            FeatureHelpers.RefreshFeatures(group);
            foreach (var f in openxr.GetFeatures<OpenXRFeature>())
            {
                string n = f.GetType().Name;
                if (n == "MetaQuestTouchProControllerProfile" || n == "OculusTouchControllerProfile")
                {
                    f.enabled = true;
                    Debug.Log("[Microduck] Enabled OpenXR feature " + n);
                }
            }
            EditorUtility.SetDirty(openxr);
        }
    }
}
