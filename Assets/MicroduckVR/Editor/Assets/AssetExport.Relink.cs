using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ================================================================== 4. Link Scene To Prefabs
        [MenuItem("Microduck/Assets/Link Scene To Prefabs")]
        public static void LinkSceneToPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Link Scene To Prefabs: leave Play mode first"); return; }
            var house = EnsureHouseScene();
            if (house.IsValid()) Link(house, true);
        }

        public static bool RelinkAfterBuild => EditorPrefs.GetBool(RelinkPref, false);

        [MenuItem(RelinkMenu)]
        public static void ToggleRelinkAfterBuild()
        {
            EditorPrefs.SetBool(RelinkPref, !RelinkAfterBuild);
            Debug.Log("[Microduck] Relink House After Build: " + (RelinkAfterBuild ? "on" : "off"));
        }

        [MenuItem(RelinkMenu, true)]
        static bool ToggleRelinkAfterBuildValidate() { Menu.SetChecked(RelinkMenu, RelinkAfterBuild); return true; }

        /// <summary>Called by Build House after it saved the scene, when Relink House After Build is on.</summary>
        internal static void RelinkBuiltHouse(Scene scene)
        {
            if (!File.Exists(IndexPath)) { Debug.Log("[Microduck] Relink after build: no prefab index yet (run Export Prefabs), house left as built"); return; }
            Link(scene, true);
        }

        /// <summary>
        /// Replaces each house item that matches an exported prefab (same signature) by an instance of it at the same
        /// world place, same name, parent and sibling order (the network code finds scene props by that path).
        /// Items another scene component points at (the toy train's cars) are left alone. Undo-able, then saved.
        /// </summary>
        static void Link(Scene house, bool save)
        {
            var index = LoadIndex();
            if (index.items.Count == 0) { Debug.LogError("[Microduck] Link Scene To Prefabs: no prefab index at " + IndexPath + " (run Export Prefabs first)"); return; }
            var houseRoot = FindHouseRoot(house);
            if (houseRoot == null) { Debug.LogError("[Microduck] Link Scene To Prefabs: no 'House' object in " + house.path); return; }
            int skipped = 0, already = 0, unmatched = 0, referenced = 0, linked = 0, warnings = 0;
            var items = CollectHouseItems(houseRoot, ref skipped);
            var refs = ReferencedFrom(house);
            var plan = new List<(Item it, IndexEntry e, Vector3 pos, Quaternion rot, bool resetScale)>();
            var keptNames = new List<string>();

            var temp = OpenTemp();
            try
            {
                foreach (var it in items)
                {
                    if (it.linkedPrefab != null || it.pieces.Any(p => PrefabUtility.IsPartOfPrefabInstance(p.gameObject))) { already++; continue; }
                    if (PointedAtFromOutside(it, refs)) { referenced++; keptNames.Add(it.name); continue; }
                    var c = BuildCandidate(it, temp.root);
                    var e = index.items.FirstOrDefault(x => x.sig == c.sig);
                    Object.DestroyImmediate(c.go);
                    if (e == null) { unmatched++; continue; }
                    plan.Add((it, e, c.pivotPos, c.pivotRot, c.resetScale));
                }
            }
            finally { CloseTemp(temp); }

            Undo.SetCurrentGroupName("Link Scene To Prefabs");
            int group = Undo.GetCurrentGroup();
            foreach (var (it, e, pos, rot, resetScale) in plan)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.prefab);
                if (prefab == null) { Debug.LogWarning("[Microduck] Link: prefab missing " + e.prefab); warnings++; continue; }
                var first = it.pieces[0];
                var parent = first.parent; int sibling = first.GetSiblingIndex();
                var inst = PrefabUtility.InstantiatePrefab(prefab, house) as GameObject;
                if (inst == null) { Debug.LogWarning("[Microduck] Link: could not place " + e.prefab); warnings++; continue; }
                Undo.RegisterCreatedObjectUndo(inst, "Link Scene To Prefabs");
                inst.transform.SetParent(parent, false);
                inst.transform.SetPositionAndRotation(pos, rot);
                if (!it.wrap && !resetScale) inst.transform.localScale = first.localScale;
                inst.name = it.wrap ? it.name : first.name;
                foreach (var p in it.pieces) Undo.DestroyObjectImmediate(p.gameObject);
                inst.transform.SetSiblingIndex(sibling);
                linked++;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(house);
            if (save && plan.Count > 0) EditorSceneManager.SaveScene(house);
            if (keptNames.Count > 0) Debug.Log("[Microduck] Link: left as built (other scene objects point at them): " + string.Join(", ", keptNames.Distinct()));
            Debug.Log($"[Microduck] Link Scene To Prefabs: {linked} items now prefab instances, {already} were already, {unmatched} with no matching prefab " +
                      $"(variants over the cap, or changed since the export), {referenced} left because other objects point at them, {warnings} warnings" +
                      (save && plan.Count > 0 ? "; scene saved." : "."));
        }

        /// <summary>GameObjects that a component in the scene points at, with the GameObjects of those components.</summary>
        static Dictionary<GameObject, List<GameObject>> ReferencedFrom(Scene s)
        {
            var refs = new Dictionary<GameObject, List<GameObject>>();
            foreach (var root in s.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var p = so.GetIterator();
                    while (p.NextVisible(true))
                    {
                        if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                        GameObject g = null;
                        if (p.objectReferenceValue is GameObject go) g = go;
                        else if (p.objectReferenceValue is Component c) g = c.gameObject;
                        if (g == null || g.scene != s) continue;
                        if (!refs.TryGetValue(g, out var owners)) refs[g] = owners = new List<GameObject>();
                        owners.Add(mb.gameObject);
                    }
                }
            return refs;
        }

        static bool PointedAtFromOutside(Item it, Dictionary<GameObject, List<GameObject>> refs)
        {
            bool Inside(GameObject g) { foreach (var p in it.pieces) if (g.transform.IsChildOf(p)) return true; return false; }
            foreach (var p in it.pieces)
                foreach (var t in p.GetComponentsInChildren<Transform>(true))
                    if (refs.TryGetValue(t.gameObject, out var owners) && owners.Any(o => !Inside(o))) return true;
            return false;
        }
    }
}
