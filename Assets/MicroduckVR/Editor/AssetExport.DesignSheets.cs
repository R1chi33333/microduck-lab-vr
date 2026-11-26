using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MicroduckVR.Comic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ================================================================== 2b. Design sheets (docs/images)
        const string SheetDir = "docs/images";

        /// <summary>Turnaround of every duck colourway and one labelled sheet per item category, for the README and releases.</summary>
        [MenuItem("Microduck/Assets/Make Design Sheets")]
        public static void MakeDesignSheets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Make Design Sheets: leave Play mode first"); return; }
            var all = ListPrefabs(false);
            if (all.Count == 0) { Debug.LogError("[Microduck] Make Design Sheets: run Export Prefabs first"); return; }
            int written = 0;

            // the duck: rows = colourways, columns = views
            var ducks = all.Where(x => x.cat == "Ducks").OrderBy(x => Path.GetFileNameWithoutExtension(x.path) == "Duck" ? 0 : 1).ToList();
            var views = new[] { ("FRONT", 0f), ("3/4", 40f), ("SIDE", 90f), ("BACK", 180f) };
            var shots = new List<(string path, string label, float yaw)>();
            foreach (var d in ducks)
                foreach (var v in views)
                    shots.Add((d.path, Path.GetFileNameWithoutExtension(d.path) + "  " + v.Item1, v.Item2));
            if (shots.Count > 0 && RenderSheet(shots, views.Length, 480, "MICRODUCK  DESIGN SHEET", "Ducks", SheetDir + "/duck_design_sheet.png")) written++;

            foreach (var (cat, cols, cell) in new[] { ("Toys", 6, 360), ("Furniture", 6, 360), ("Clutter", 10, 256) })
            {
                var items = all.Where(x => x.cat == cat).Select(x => (x.path, Path.GetFileNameWithoutExtension(x.path), 0f)).ToList();
                if (items.Count == 0) continue;
                if (RenderSheet(items, cols, cell, $"MICRODUCK LAB  {cat.ToUpperInvariant()}  ({items.Count})", cat, $"{SheetDir}/{cat.ToLowerInvariant()}_sheet.png")) written++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Microduck] Make Design Sheets: {written} sheets written to {SheetDir}.");
        }

        /// <summary>yaw = 0 is the 3/4 catalog view for items; for the duck it is the angle around the duck from its front.</summary>
        static bool RenderSheet(List<(string path, string label, float yaw)> shots, int cols, int cell, string title, string cat, string outPath)
        {
            const int TitleH = 72;
            int labelH = cell >= 360 ? 36 : LabelH;
            int labelScale = cell >= 360 ? 3 : 2;
            bool turnaround = cat == "Ducks";
            int rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * cell, TitleH + rows * (cell + labelH), TextureFormat.RGB24, false);
            var px = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 247, 232, 255);
            sheet.SetPixels32(px);
            var temp = OpenTemp();
            var lightsOff = new List<Light>();
            RenderTexture rt = null;
            bool ok = false;
            try
            {
                for (int s = 0; s < SceneManager.sceneCount; s++)
                {
                    var sc = SceneManager.GetSceneAt(s);
                    if (!sc.isLoaded || sc == temp.scene) continue;
                    foreach (var g in sc.GetRootGameObjects())
                        foreach (var l in g.GetComponentsInChildren<Light>()) if (l.enabled) { l.enabled = false; lightsOff.Add(l); }
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.74f, 0.80f, 0.98f);
                RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
                RenderSettings.ambientGroundColor = new Color(0.82f, 0.64f, 0.48f);
                RenderSettings.fog = false;
                var sun = new GameObject("sheet sun").AddComponent<Light>();
                sun.transform.SetParent(temp.root, false);
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard; sun.color = new Color(1f, 0.96f, 0.88f);
                sun.cullingMask = 1 << CatalogLayer;
                var camGo = new GameObject("sheet camera");
                camGo.transform.SetParent(temp.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 26f; cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << CatalogLayer;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                rt = new RenderTexture(cell, cell, 24) { antiAliasing = 8 };
                cam.targetTexture = rt;

                for (int i = 0; i < shots.Count; i++)
                {
                    var (path, label, yaw) = shots[i];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var inst = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, temp.scene) as GameObject : null;
                    if (inst == null) { Debug.LogWarning("[Microduck] design sheet: could not place " + path); continue; }
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CatalogLayer;
                    var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                    foreach (var r in inst.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }

                    Vector3 dir;
                    if (turnaround)
                    {
                        float a = (yaw + DuckFrontYaw) * Mathf.Deg2Rad;
                        dir = new Vector3(Mathf.Sin(a), 0.22f, Mathf.Cos(a)).normalized;
                        sun.transform.rotation = Quaternion.LookRotation(-new Vector3(Mathf.Sin(a + 0.8f), 1.1f, Mathf.Cos(a + 0.8f)));
                    }
                    else
                    {
                        dir = new Vector3(0.55f, 0.5f, 1f).normalized;
                        sun.transform.rotation = Quaternion.Euler(45f, 200f, 0f);
                    }
                    float radius = Mathf.Max(b.extents.magnitude, 0.005f);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (turnaround ? 0.92f : 1.05f);
                    cam.transform.position = b.center + dir * dist;
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.0005f, dist - radius * 1.5f);
                    cam.farClipPlane = dist + radius * 3f;
                    cam.backgroundColor = CategoryColour(cat);
                    cam.Render();

                    int cx = (i % cols) * cell, cy = sheet.height - TitleH - (i / cols + 1) * (cell + labelH);
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, cell, cell), cx, cy + labelH, false);
                    RenderTexture.active = null;
                    FillRect(sheet, cx, cy, cell, labelH, new Color(1f, 0.97f, 0.91f));
                    FillRect(sheet, cx, cy + labelH - 2, cell, 2, ComicLook.Ink);
                    DrawText(sheet, cx + 8, cy + labelH - (labelH - 7 * labelScale) / 2, label, ComicLook.Ink, labelScale, cell - 16);
                    Object.DestroyImmediate(inst);
                }
                // title band and grid
                FillRect(sheet, 0, sheet.height - TitleH, sheet.width, TitleH, ComicLook.Hex("#FFD23F"));
                FillRect(sheet, 0, sheet.height - TitleH - 4, sheet.width, 4, ComicLook.Ink);
                DrawText(sheet, 20, sheet.height - 14, title, ComicLook.Ink, 6, sheet.width - 40);
                for (int c = 1; c < cols; c++) FillRect(sheet, c * cell - 1, 0, 2, sheet.height - TitleH, ComicLook.Ink);
                for (int r = 1; r < rows; r++) FillRect(sheet, 0, sheet.height - TitleH - r * (cell + labelH) - 1, sheet.width, 2, ComicLook.Ink);
                FillRect(sheet, 0, 0, sheet.width, 4, ComicLook.Ink); FillRect(sheet, 0, 0, 4, sheet.height, ComicLook.Ink);
                FillRect(sheet, sheet.width - 4, 0, 4, sheet.height, ComicLook.Ink); FillRect(sheet, 0, sheet.height - 4, sheet.width, 4, ComicLook.Ink);
                sheet.Apply();
                string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllBytes(full, sheet.EncodeToPNG());
                cam.targetTexture = null;
                ok = true;
            }
            catch (Exception ex) { Debug.LogException(ex); }
            finally
            {
                foreach (var l in lightsOff) if (l != null) l.enabled = true;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                Object.DestroyImmediate(sheet);
                CloseTemp(temp);
            }
            return ok;
        }

        /// <summary>World yaw (degrees) the camera sits at to see the duck prefab's face.</summary>
        const float DuckFrontYaw = 0f;
    }
}
