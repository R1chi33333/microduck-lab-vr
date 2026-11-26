using System.Text;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>Prints the imported rig hierarchy and a handedness check (debug).</summary>
    public static class RigProbe
    {
        [MenuItem("Microduck/Debug/Probe Rig Import")]
        public static void Probe()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            if (prefab == null) { Debug.LogError("[Microduck] probe: rig asset not found"); return; }
            var go = (GameObject)Object.Instantiate(prefab);
            var sb = new StringBuilder("[Microduck] probe hierarchy:\n");
            Walk(go.transform, 0, sb);
            Debug.Log(sb.ToString());
            var n = Find(go.transform, "body_yaw2roll");
            if (n != null) Debug.Log($"[Microduck] probe body_yaw2roll localPos={n.localPosition.ToString("F4")} localRot={n.localRotation.ToString("F4")} (expect pos (-0.0175,-0.0050,0.0060), rot (0.7071,0,-0.7071,0) or its negation)");
            var t = Find(go.transform, "body_trunk");
            if (t != null) Debug.Log($"[Microduck] probe body_trunk localPos={t.localPosition.ToString("F4")} parent={t.parent?.name} parentRot={t.parent?.localRotation.ToString("F3")} parentScale={t.parent?.localScale.ToString("F3")}");
            int renderers = go.GetComponentsInChildren<Renderer>(true).Length;
            Debug.Log($"[Microduck] probe renderers={renderers}");
            Object.DestroyImmediate(go);
        }

        static void Walk(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 3) return;
            sb.Append(' ', depth * 2).Append(t.name).Append('\n');
            foreach (Transform c in t) if (c.name.StartsWith("body_") || c.name.StartsWith("marker_") || depth < 1) Walk(c, depth + 1, sb);
        }

        public static Transform Find(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var r = Find(c, name); if (r != null) return r; }
            return null;
        }
    }
}
