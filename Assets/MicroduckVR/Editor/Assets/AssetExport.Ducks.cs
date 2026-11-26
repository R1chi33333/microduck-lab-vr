using System;
using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Core;
using MicroduckVR.Custom;
using MicroduckVR.Dev;
using MicroduckVR.Duck;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ------------------------------------------------------------------ ducks
        /// <summary>
        /// The duck as a flock duck is built (rig under DuckBody: tilt > model > rig, joints in the stand pose, the
        /// comic look applied), then frozen: DuckBody is taken off, hidden hardware is dropped, meshes are copied so
        /// the prefab needs neither glTFast nor the game scripts. One prefab per colourway, all sharing one mesh asset.
        /// Plus "Duck (live)" in Ducks/Live: DuckBody + DuckSetup + WanderDuck, the game's own runtime duck.
        /// </summary>
        static int ExportDucks(Transform parent)
        {
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(RigImporter.GlbPath);
            var map = AssetDatabase.LoadAssetAtPath<DuckJointMap>(RigImporter.JointMapPath);
            if (glb == null || map == null) { Warn("duck rig or joint map missing (Microduck/Import Duck Rig); no duck prefabs"); return 0; }
            int n = 0;
            var shared = new Dictionary<Mesh, Mesh>();
            foreach (var skin in DuckCustom.SkinIds)
            {
                string name = skin == "cream" ? "Duck" : "Duck " + skin;
                var root = new GameObject(name);
                root.transform.SetParent(parent, false);
                var rig = Object.Instantiate(glb);
                rig.name = "rig";
                var body = root.AddComponent<DuckBody>();
                body.Init(rig, map);
                try { for (int i = 0; i < 30; i++) body.Step(1f / 60f, Vector3.zero, 0f, Vector3.zero, 0f); }
                catch (Exception ex) { Warn("duck settle step failed (" + ex.Message + "); saved in the plain stand pose"); }
                ComicLook.ApplyToDuck(rig.transform, skin);
                Object.DestroyImmediate(body);
                int stripped = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = Layers.Duck;
                    var r = t.GetComponent<Renderer>();
                    if (r != null && !r.enabled)
                    {
                        Object.DestroyImmediate(r);
                        var f = t.GetComponent<MeshFilter>(); if (f != null) Object.DestroyImmediate(f);
                    }
                    foreach (var c in t.GetComponents<Component>())
                        if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) { Object.DestroyImmediate(c); stripped++; }
                }
                if (stripped > 0) Debug.Log($"[Microduck] {name}: took {stripped} non-visual component(s) off the model");
                Normalise(root.transform, false);
                Save(root, "Ducks", name, "Duck", shared);
                Object.DestroyImmediate(root);
                n++;
            }

            // the live one: the same components as a scene duck, built at runtime; no visible model until Play
            EnsureFolder(LiveDir);
            var live = new GameObject("Duck (live)");
            live.transform.SetParent(parent, false);
            live.SetActive(false);
            live.AddComponent<DuckBody>();
            var setup = live.AddComponent<DuckSetup>();
            setup.rigPrefab = glb; setup.jointMap = map; setup.skin = "cream";
            var wander = live.AddComponent<WanderDuck>();
            wander.boundsMin = new Vector2(-1f, -1f); wander.boundsMax = new Vector2(1f, 1f); wander.seed = 1;
            live.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(live, LiveDir + "/Duck (live).prefab", out bool ok);
            if (ok) { written.Add(LiveDir + "/Duck (live).prefab"); nPrefabs++; perCategory["Ducks"] = (perCategory.TryGetValue("Ducks", out var k) ? k : 0) + 1; n++; }
            else Warn("could not save the live duck prefab");
            Object.DestroyImmediate(live);
            return n;
        }
    }
}
