using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ================================================================== 3. Export Package
        /// <summary>SurfaceTag's enum: no asset refers to its file, so it is added to the package by hand.</summary>
        const string SurfaceTypeScript = "Assets/MicroduckVR/Scripts/World/Shared/SurfaceType.cs";
        /// <summary>The assembly definition of the shared scripts, so they compile on their own in the importing project (added when it exists).</summary>
        const string SharedAsmdef = "Assets/MicroduckVR/Scripts/World/Shared/MicroduckVR.World.Shared.asmdef";

        [MenuItem("Microduck/Assets/Export Package")]
        public static void ExportPackage()
        {
            var roots = ListPrefabs(false).Select(x => x.path).ToList();
            if (roots.Count == 0) { Debug.LogError("[Microduck] Export Package: no prefabs under " + Root + " (run Export Prefabs first)"); return; }
            int warnings = 0;
            if (File.Exists(CreditsPath)) roots.Add(CreditsPath); else { Debug.LogWarning("[Microduck] Export Package: " + CreditsPath + " is missing; the package has no credits file"); warnings++; }
            foreach (var p in new[] { OutlineShaderPath, ComicSetup.OutlineMatPath }) if (File.Exists(p)) roots.Add(p);
            if (File.Exists(SurfaceTypeScript)) roots.Add(SurfaceTypeScript);
            if (File.Exists(SharedAsmdef)) roots.Add(SharedAsmdef);
            if (AssetDatabase.IsValidFolder(AnimDir))                                   // the duck's moves: clips, controller, animated duck
                roots.AddRange(AssetDatabase.FindAssets("", new[] { AnimDir }).Select(AssetDatabase.GUIDToAssetPath).Where(x => !AssetDatabase.IsValidFolder(x)));

            var keep = new SortedSet<string>(StringComparer.Ordinal);
            int left = 0;
            foreach (var d in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (!d.StartsWith("Assets/")) continue;                                   // packages and Unity's built-in assets
                if (d.EndsWith(".unity") || d.Contains("/Editor/") || d.StartsWith("Assets/MicroduckVR/Settings/") || d.StartsWith("Assets/MicroduckVR/Scenes/")
                    || d == IndexPath || d.StartsWith(LiveDir + "/")) { left++; continue; }
                if (d.EndsWith(".cs") && !PackageScripts.Contains(d))
                {
                    Debug.LogWarning("[Microduck] Export Package: left out " + d + " (it needs other game scripts); items using it would show a missing script"); warnings++; left++; continue;
                }
                if (d.EndsWith(".glb") || d.EndsWith(".gltf"))
                {
                    Debug.LogWarning("[Microduck] Export Package: left out " + d + " (needs glTFast); run Export Prefabs again so the meshes are copied"); warnings++; left++; continue;
                }
                keep.Add(d);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(PackagePath));
#pragma warning disable CS0618   // the classic export call; it still writes the same .unitypackage in Unity 6.6
            AssetDatabase.ExportPackage(keep.ToArray(), PackagePath, ExportPackageOptions.Default);
#pragma warning restore CS0618

            int N(Func<string, bool> f) => keep.Count(f);
            long bytes = File.Exists(PackagePath) ? new FileInfo(PackagePath).Length : 0;
            Debug.Log($"[Microduck] Export Package: {PackagePath} ({bytes / 1024} KB), {keep.Count} files: " +
                      $"{N(p => p.EndsWith(".prefab"))} prefabs, {N(p => p.EndsWith(".asset"))} mesh assets, {N(p => p.EndsWith(".mat"))} materials, " +
                      $"{N(p => p.EndsWith(".physicMaterial") || p.EndsWith(".physicsMaterial"))} physics materials, {N(p => p.EndsWith(".shader") || p.EndsWith(".hlsl"))} shaders, " +
                      $"{N(p => p.EndsWith(".png") || p.EndsWith(".jpg"))} textures, {N(p => p.EndsWith(".ttf") || p.EndsWith(".otf"))} fonts, {N(p => p.EndsWith(".cs"))} scripts, " +
                      $"credits {(keep.Contains(CreditsPath) ? "yes" : "NO")}; {left} dependencies left out, {warnings} warnings.");
        }
    }
}
