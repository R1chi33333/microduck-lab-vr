using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Imports TextMeshPro's essential resources (shaders, settings) without the interactive dialog.</summary>
    public static class TmpSetup
    {
        [MenuItem("Microduck/Setup/Import TextMeshPro Essentials")]
        public static void Import()
        {
            if (Directory.Exists("Assets/TextMesh Pro/Resources")) { Debug.Log("[Microduck] TMP essentials already present"); return; }
            var cache = Path.GetFullPath("Library/PackageCache");
            var pkg = Directory.GetDirectories(cache, "com.unity.ugui*")
                .Select(d => Path.Combine(d, "Package Resources", "TMP Essential Resources.unitypackage"))
                .FirstOrDefault(File.Exists);
            if (pkg == null) { Debug.LogError("[Microduck] TMP Essential Resources package not found"); return; }
#pragma warning disable CS0618
            AssetDatabase.ImportPackage(pkg, false);
#pragma warning restore CS0618
            Debug.Log("[Microduck] TMP essentials imported from " + pkg);
        }
    }
}
