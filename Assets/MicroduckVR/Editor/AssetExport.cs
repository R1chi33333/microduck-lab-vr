using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicroduckVR.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// Turns the game's items into reusable assets under Assets/MicroduckVR/Prefabs:
    ///   Export Prefabs    one prefab per house item (furniture, clutter), per bag toy and per duck colourway, each
    ///                     with its own mesh asset, a bottom-centre pivot and an unrotated frame;
    ///   Make Catalog      a labelled preview sheet (Logs/assets/catalog.png) and the item list (docs/ASSETS.md);
    ///   Export Package    Builds/MicroduckLab_Assets.unitypackage with the prefabs, what they need and CREDITS.txt;
    ///   Link Scene To Prefabs  swaps the House scene's furniture and clutter for instances of those prefabs.
    /// Every menu item is also a public static method without arguments, and logs a "[Microduck]" summary.
    /// </summary>
    public static partial class AssetExport
    {
        public const string Root = "Assets/MicroduckVR/Prefabs";
        public const string IndexPath = Root + "/prefab_index.json";
        public const string CreditsPath = Root + "/CREDITS.txt";
        const string MeshRoot = Root + "/Meshes", MatRoot = Root + "/Materials", LiveDir = Root + "/Ducks/Live";
        const string PackagePath = "Builds/MicroduckLab_Assets.unitypackage";
        const string CatalogPath = "Logs/assets/catalog.png";
        const string DocsPath = "docs/ASSETS.md";
        const string OutlineShaderPath = "Assets/MicroduckVR/Shaders/ComicOutline.shader";
        const string RelinkPref = "Microduck.RelinkHouseAfterBuild";
        const string RelinkMenu = "Microduck/Assets/Relink House After Build";
        /// <summary>At most this many different versions of one item (books, blocks, flowers come in many sizes and colours).</summary>
        const int MaxVariants = 8;
        static readonly string[] Categories = { "Toys", "Furniture", "Clutter", "Ducks" };

        /// <summary>Scripts that may travel in the package: they compile on their own.</summary>
        static readonly HashSet<string> PackageScripts = new HashSet<string>
        {
            "Assets/MicroduckVR/Scripts/World/Grabbable.cs",
            "Assets/MicroduckVR/Scripts/World/SurfaceTag.cs",
        };

        // ================================================================== counters
        static int nPrefabs, nMeshes, nMaterials, nWarnings;
        static readonly Dictionary<string, int> perCategory = new Dictionary<string, int>();
        static readonly Dictionary<Object, Object> savedMaterials = new Dictionary<Object, Object>();
        static readonly HashSet<string> usedMaterialPaths = new HashSet<string>();
        static readonly HashSet<string> written = new HashSet<string>();

        static void Reset()
        {
            nPrefabs = nMeshes = nMaterials = nWarnings = 0;
            perCategory.Clear(); savedMaterials.Clear(); usedMaterialPaths.Clear(); written.Clear();
        }

        static void Warn(string msg) { nWarnings++; Debug.LogWarning("[Microduck] " + msg); }

        // ================================================================== 1. Export Prefabs
        [MenuItem("Microduck/Assets/Export Prefabs")]
        public static void ExportPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Export Prefabs: leave Play mode first"); return; }
            var house = EnsureHouseScene();
            if (!house.IsValid()) return;
            Reset();
            foreach (var c in Categories) { EnsureFolder($"{Root}/{c}"); EnsureFolder($"{MeshRoot}/{c}"); }
            var index = LoadIndex();
            int kept = 0, dup = 0, capped = 0, skipped = 0, toys = 0, ducks = 0;
            var temp = OpenTemp();
            try
            {
                // ---------------------------------------------------------- house items
                var houseRoot = FindHouseRoot(house);
                if (houseRoot == null) Warn("Export Prefabs: no 'House' object in " + house.path + " (run Microduck/Scenes/Build House first); house items skipped");
                else
                {
                    var items = CollectHouseItems(houseRoot, ref skipped);
                    var linked = new HashSet<string>(items.Where(i => i.linkedPrefab != null).Select(i => i.linkedPrefab));
                    foreach (var it in items)
                    {
                        if (it.linkedPrefab != null) { kept++; continue; }      // the scene already uses this prefab: the prefab is the original now
                        var c = BuildCandidate(it, temp.root);
                        var e = index.items.FirstOrDefault(x => x.sig == c.sig);
                        if (e != null)
                        {
                            if (written.Contains(e.prefab) || linked.Contains(e.prefab)) { dup++; Object.DestroyImmediate(c.go); continue; }
                        }
                        else
                        {
                            string family = it.category + "/" + it.name;
                            int n = index.items.Count(x => x.category + "/" + x.family == family);
                            if (n >= MaxVariants) { capped++; Object.DestroyImmediate(c.go); continue; }
                            string file = FreeName(index, it.category, Sanitize(it.name), n);
                            e = new IndexEntry { sig = c.sig, category = it.category, family = it.name, name = file, prefab = $"{Root}/{it.category}/{file}.prefab" };
                            index.items.Add(e);
                        }
                        Save(c.go, e.category, e.name, e.name, null);
                        Object.DestroyImmediate(c.go);
                    }
                }

                // ---------------------------------------------------------- bag toys
                foreach (var entry in ToyCatalog.All)
                {
                    var go = ToyCatalog.Spawn(entry.kind, Vector3.zero, 0f);
                    if (go == null) { Warn("toy " + entry.kind + " did not build"); continue; }
                    go.transform.SetParent(temp.root, true);
                    var tag = go.GetComponent<ToyTag>();
                    if (tag != null) Object.DestroyImmediate(tag);        // ToyTag is game-only runtime data: keep it off the exported prefabs
                    go.name = entry.label;
                    Normalise(go.transform, false);
                    string file = Sanitize(entry.label);
                    Save(go, "Toys", file, file, null);
                    Object.DestroyImmediate(go);
                    toys++;
                }

                // ---------------------------------------------------------- ducks
                ducks = ExportDucks(temp.root);
            }
            catch (Exception ex) { Debug.LogException(ex); Warn("Export Prefabs stopped early: " + ex.Message); }
            finally
            {
                CloseTemp(temp);
                SaveIndex(index);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // prefabs from an earlier run that this run did not write
            var stale = new List<string>();
            foreach (var cat in Categories)
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { $"{Root}/{cat}" }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (!written.Contains(p) && !index.items.Any(x => x.prefab == p)) stale.Add(p);
                }
            if (stale.Count > 0) Warn($"{stale.Count} prefab(s) not written by this run (kept): " + string.Join(", ", stale.Take(12)) + (stale.Count > 12 ? ", ..." : ""));

            string cats = string.Join(", ", Categories.Select(c => $"{c} {(perCategory.TryGetValue(c, out var k) ? k : 0)}"));
            Debug.Log($"[Microduck] Export Prefabs: {nPrefabs} prefabs ({cats}), {nMeshes} meshes, {nMaterials} materials saved, {nWarnings} warnings. " +
                      $"House: {kept} kept (scene uses the prefab), {dup} duplicates folded, {capped} variants over the cap of {MaxVariants}, {skipped} architecture pieces skipped. " +
                      $"Toys {toys}, duck prefabs {ducks}. Index: {IndexPath} ({index.items.Count} entries).");
        }

        static string FreeName(IndexFile index, string category, string baseName, int n)
        {
            for (int k = n; ; k++)
            {
                string name = k == 0 ? baseName : $"{baseName} {k + 1}";
                if (!index.items.Any(x => x.category == category && x.name == name) && !written.Contains($"{Root}/{category}/{name}.prefab")) return name;
            }
        }

        // ================================================================== helpers
        class Temp { public Scene scene, prev; public Transform root; }

        /// <summary>An empty additive scene, made active, where copies are built and shot; the open scenes stay untouched.</summary>
        static Temp OpenTemp()
        {
            var prev = SceneManager.GetActiveScene();
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(s);
            var root = new GameObject("asset export").transform;
            return new Temp { scene = s, prev = prev, root = root };
        }

        static void CloseTemp(Temp t)
        {
            if (t == null) return;
            if (t.prev.IsValid() && t.prev.isLoaded) SceneManager.SetActiveScene(t.prev);
            if (t.scene.IsValid()) EditorSceneManager.CloseScene(t.scene, true);
        }

        /// <summary>The House scene, opened if needed (only when the open scene has no unsaved changes).</summary>
        static Scene EnsureHouseScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.path == SceneBuilderHouse.ScenePath && s.isLoaded) { SceneManager.SetActiveScene(s); return s; }
            }
            if (!File.Exists(SceneBuilderHouse.ScenePath)) { Debug.LogError("[Microduck] no " + SceneBuilderHouse.ScenePath + " (run Microduck/Scenes/Build House first)"); return default; }
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) { Debug.LogError("[Microduck] the open scene has unsaved changes: save it or open " + SceneBuilderHouse.ScenePath + " first"); return default; }
            return EditorSceneManager.OpenScene(SceneBuilderHouse.ScenePath, OpenSceneMode.Single);
        }

        static IndexFile LoadIndex()
        {
            IndexFile f = null;
            try { if (File.Exists(IndexPath)) f = JsonUtility.FromJson<IndexFile>(File.ReadAllText(IndexPath)); }
            catch (Exception ex) { Warn("could not read " + IndexPath + ": " + ex.Message); }
            f ??= new IndexFile();
            f.items ??= new List<IndexEntry>();
            f.items.RemoveAll(e => e == null || string.IsNullOrEmpty(e.sig) || !File.Exists(e.prefab));   // a deleted prefab frees its slot
            return f;
        }

        static void SaveIndex(IndexFile f)
        {
            EnsureFolder(Root);
            f.items = f.items.OrderBy(e => e.prefab, StringComparer.Ordinal).ToList();
            File.WriteAllText(IndexPath, JsonUtility.ToJson(f, true));
            AssetDatabase.ImportAsset(IndexPath);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string Sanitize(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s) if (ch != '#') sb.Append(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '_' || ch == '-' || ch == '(' || ch == ')' ? ch : '_');
            return sb.ToString().Trim();
        }
    }
}
