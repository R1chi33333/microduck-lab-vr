using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MicroduckVR.Comic;
using MicroduckVR.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MicroduckVR.EditorTools
{
    public static partial class AssetExport
    {
        // ================================================================== 2. Make Catalog
        const int Cell = 256, LabelH = 28, Cols = 8, CatalogLayer = 31;

        [MenuItem("Microduck/Assets/Make Catalog")]
        public static void MakeCatalog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Microduck] Make Catalog: leave Play mode first"); return; }
            var list = ListPrefabs(false);
            if (list.Count == 0) { Debug.LogError("[Microduck] Make Catalog: no prefabs under " + Root + " (run Export Prefabs first)"); return; }
            int warnings = 0;
            int rows = (list.Count + Cols - 1) / Cols;
            var sheet = new Texture2D(Cols * Cell, rows * (Cell + LabelH), TextureFormat.RGB24, false);
            var px = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 247, 232, 255);
            sheet.SetPixels32(px);

            var info = new List<(string cat, string name, string path, Vector3 size, string physics)>();
            var temp = OpenTemp();
            var lightsOff = new List<Light>();
            RenderTexture rt = null;
            try
            {
                // lights from the open scenes would light (and shadow) the shots: switch them off while shooting
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
                var sun = new GameObject("catalog sun").AddComponent<Light>();
                sun.transform.SetParent(temp.root, false);
                sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Hard; sun.color = new Color(1f, 0.96f, 0.88f);
                sun.cullingMask = 1 << CatalogLayer;
                sun.transform.rotation = Quaternion.Euler(45f, 200f, 0f);

                var camGo = new GameObject("catalog camera");
                camGo.transform.SetParent(temp.root, false);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.fieldOfView = 30f; cam.clearFlags = CameraClearFlags.SolidColor; cam.cullingMask = 1 << CatalogLayer;
                camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
                rt = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;

                for (int i = 0; i < list.Count; i++)
                {
                    var (cat, path) = list[i];
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var inst = prefab != null ? PrefabUtility.InstantiatePrefab(prefab, temp.scene) as GameObject : null;
                    if (inst == null) { Debug.LogWarning("[Microduck] catalog: could not place " + path); warnings++; continue; }
                    string physics = PhysicsSummary(inst);
                    foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = CatalogLayer;
                    var rs = inst.GetComponentsInChildren<Renderer>();
                    var b = new Bounds(Vector3.zero, Vector3.zero); bool any = false;
                    foreach (var r in rs) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                    if (!any) { Debug.LogWarning("[Microduck] catalog: " + path + " has nothing to draw"); warnings++; }
                    info.Add((cat, prefab.name, path, b.size, physics));

                    float radius = Mathf.Max(b.extents.magnitude, 0.005f);
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.08f;
                    var dir = new Vector3(0.55f, 0.5f, 1f).normalized;
                    cam.transform.position = b.center + dir * dist;
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.0005f, dist - radius * 1.5f);
                    cam.farClipPlane = dist + radius * 3f;
                    cam.backgroundColor = CategoryColour(cat);
                    cam.Render();

                    int cx = (i % Cols) * Cell, cy = sheet.height - (i / Cols + 1) * (Cell + LabelH);
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, Cell, Cell), cx, cy + LabelH, false);
                    RenderTexture.active = null;
                    FillRect(sheet, cx, cy, Cell, LabelH, new Color(1f, 0.97f, 0.91f));
                    FillRect(sheet, cx, cy + LabelH - 2, Cell, 2, ComicLook.Ink);
                    DrawText(sheet, cx + 6, cy + LabelH - 7, prefab.name, ComicLook.Ink, 2, Cell - 12);
                    Object.DestroyImmediate(inst);
                }
                // grid lines
                for (int c = 1; c < Cols; c++) FillRect(sheet, c * Cell - 1, 0, 2, sheet.height, ComicLook.Ink);
                for (int r = 1; r < rows; r++) FillRect(sheet, 0, sheet.height - r * (Cell + LabelH) - 1, sheet.width, 2, ComicLook.Ink);
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                File.WriteAllBytes(CatalogPath, sheet.EncodeToPNG());
                cam.targetTexture = null;
            }
            catch (Exception ex) { Debug.LogException(ex); warnings++; }
            finally
            {
                foreach (var l in lightsOff) if (l != null) l.enabled = true;
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                Object.DestroyImmediate(sheet);
                CloseTemp(temp);
            }
            WriteDocs(info);
            Debug.Log($"[Microduck] Make Catalog: {info.Count} prefabs drawn to {CatalogPath}, list written to {DocsPath}, {warnings} warnings.");
        }

        static Color CategoryColour(string cat) => cat switch
        {
            "Toys" => ComicLook.Hex("#FFF0C8"),
            "Furniture" => ComicLook.Hex("#DDEBFF"),
            "Clutter" => ComicLook.Hex("#E3F4DC"),
            _ => ComicLook.Hex("#F8E1EC"),
        };

        static string PhysicsSummary(GameObject go)
        {
            string layerName = LayerMask.LayerToName(go.layer);
            var parts = new List<string> { string.IsNullOrEmpty(layerName) ? "layer " + go.layer : layerName };
            var rb = go.GetComponent<Rigidbody>();
            parts.Add(rb == null ? "static" : rb.isKinematic ? "kinematic body" : $"rigidbody {rb.mass * 1000f:0.#} g");
            if (go.GetComponent<Grabbable>() != null) parts.Add("grabbable");
            var cols = go.GetComponentsInChildren<Collider>(true);
            if (cols.Length > 0) parts.Add(string.Join("+", cols.Select(c => c.GetType().Name.Replace("Collider", "")).Distinct()) + " collider" + (cols.Length > 1 ? "s" : ""));
            if (go.GetComponentInChildren<SurfaceTag>(true) != null) parts.Add("surface tag");
            return string.Join(", ", parts);
        }

        static void WriteDocs(List<(string cat, string name, string path, Vector3 size, string physics)> info)
        {
            var sb = new StringBuilder();
            sb.Append(DocsHeader);
            sb.AppendLine($"The list below was written by **Microduck > Assets > Make Catalog** on {DateTime.Now:yyyy-MM-dd}: {info.Count} prefabs. The design sheets (one per category) are in `docs/images/` (**Make Design Sheets**); the all-in-one preview sheet is `Logs/assets/catalog.png` (local, not in git).");
            sb.AppendLine();
            foreach (var cat in Categories)
            {
                var rows = info.Where(x => x.cat == cat).ToList();
                if (rows.Count == 0) continue;
                bool nc = cat == "Ducks";
                sb.AppendLine($"## {cat} ({rows.Count})");
                sb.AppendLine();
                if (nc) sb.AppendLine("**Non-commercial only:** every duck is adapted from Pollen Robotics' Microduck (CC BY-NC-SA 4.0).\n");
                sb.AppendLine("| Item | Size (cm, W × H × D) | Physics | Licence |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var r in rows)
                    sb.AppendLine($"| `{r.path.Substring(Root.Length + 1)}` | {r.size.x * 100f:0.#} × {r.size.y * 100f:0.#} × {r.size.z * 100f:0.#} | {r.physics} | {(nc ? "CC BY-NC-SA 4.0, **non-commercial**" : "MIT")} |");
                sb.AppendLine();
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(LiveDir + "/Duck (live).prefab") != null)
            {
                sb.AppendLine("## Ducks/Live (in this project only, not in the package)");
                sb.AppendLine();
                sb.AppendLine("| Item | What it is | Licence |");
                sb.AppendLine("|---|---|---|");
                sb.AppendLine("| `Ducks/Live/Duck (live).prefab` | The game's runtime duck (DuckBody, DuckSetup, WanderDuck). It builds its model from `Models/microduck_rig.glb` when Play starts, so it shows nothing in edit mode. Needs a floor collider, ideally on the World layer (6); it wanders inside x, z in [-1, 1]. | CC BY-NC-SA 4.0, **non-commercial** |");
                sb.AppendLine();
            }
            string full = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DocsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString().Replace("\r\n", "\n"));
        }

        /// <summary>Kept in step with the hand-written top of docs/ASSETS.md.</summary>
        const string DocsHeader =
@"# Microduck Lab items

Every toy, piece of furniture, bit of household clutter and the duck itself, as separate Unity prefabs under
`Assets/MicroduckVR/Prefabs/`: drag one into a scene (or another project), share them as a `.unitypackage`, or edit
one item on its own.

## Licences

| What | Licence | Commercial use |
|---|---|---|
| `Ducks/` (Duck, Duck graphite, Duck lavender, Duck sky, Duck (live)) and their meshes and materials | [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/), adapted from the Microduck robot by [Pollen Robotics](https://github.com/pollen-robotics/microduck_rl) | **No.** Non-commercial only; credit Pollen Robotics; share changes under the same licence |
| Everything else (toys, furniture, clutter, their meshes, materials, shaders, textures and the two scripts) | MIT, (c) 2026 Yutong Jin (see `LICENSE`) | Yes |

The toys that the web game took from glTF sample models (toy car, chess pieces, cookie, toast, croissant) are rebuilt
from simple shapes in code here; no third-party model or font file is in the prefabs or the package. The package
carries all of this in `Assets/MicroduckVR/Prefabs/CREDITS.txt`.

## Making and refreshing the items (Microduck > Assets)

1. Open `Assets/MicroduckVR/Scenes/House.unity` and run **Export Prefabs**. One prefab per item in
   `Prefabs/{Toys,Furniture,Clutter,Ducks}`, its meshes in `Prefabs/Meshes/<category>/<item>.asset`, materials made
   at runtime in `Prefabs/Materials/`; the house's own materials stay in `Materials/House`.
2. **Make Catalog** draws every prefab to `Logs/assets/catalog.png` and rewrites this file; **Make Design Sheets**
   draws the duck turnaround and one labelled sheet per category to `docs/images/`.
3. **Export Package** writes `Builds/MicroduckLab_Assets.unitypackage`.
4. Optional: **Link Scene To Prefabs** swaps the house's furniture and clutter for instances of the prefabs, so
   editing a prefab changes the house. **Build House** makes the house again from code and drops those links; tick
   **Relink House After Build** to link the new house to the prefabs straight after each build.

How items are picked: each named prop the house builder makes (a root with parts, such as `Bookcase`, `Teddy`,
`Mug`), furniture the builder lays out as separate pieces (`Table` = top + 4 legs, `Kitchen chair`, `Bed`, `Kitchen
units`, ...), a few single pieces (`Fridge`, `Shelf`, `Pouf`, ...) and every toy in the toy bag. Walls, floors,
ramps, the pitch, the garden ground and the scenery outside stay part of the house. Identical copies share one
prefab; an item that comes in many sizes or colours (books, blocks, flowers) keeps up to 8 versions (`Book`,
`Book 2`, ...). Pivots sit at the bottom centre, at the floor contact, and items are unrotated.

Using them elsewhere: Unity 6 with URP. The prefabs keep the game's layers by number (World 6, Duck 7, Toy 8,
Prop 10): name those layers in the other project or change them. For the ink outlines add a Full Screen Pass
Renderer Feature using `Materials/ComicOutline.mat` (Depth and Normal inputs) to the URP renderer.

";

        // ------------------------------------------------------------------ a tiny 5 x 7 font for the labels
        static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            { 'A', new byte[] { 0x7C, 0x12, 0x11, 0x12, 0x7C } }, { 'B', new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x36 } },
            { 'C', new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x22 } }, { 'D', new byte[] { 0x7F, 0x41, 0x41, 0x22, 0x1C } },
            { 'E', new byte[] { 0x7F, 0x49, 0x49, 0x49, 0x41 } }, { 'F', new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x01 } },
            { 'G', new byte[] { 0x3E, 0x41, 0x49, 0x49, 0x7A } }, { 'H', new byte[] { 0x7F, 0x08, 0x08, 0x08, 0x7F } },
            { 'I', new byte[] { 0x00, 0x41, 0x7F, 0x41, 0x00 } }, { 'J', new byte[] { 0x20, 0x40, 0x41, 0x3F, 0x01 } },
            { 'K', new byte[] { 0x7F, 0x08, 0x14, 0x22, 0x41 } }, { 'L', new byte[] { 0x7F, 0x40, 0x40, 0x40, 0x40 } },
            { 'M', new byte[] { 0x7F, 0x02, 0x0C, 0x02, 0x7F } }, { 'N', new byte[] { 0x7F, 0x04, 0x08, 0x10, 0x7F } },
            { 'O', new byte[] { 0x3E, 0x41, 0x41, 0x41, 0x3E } }, { 'P', new byte[] { 0x7F, 0x09, 0x09, 0x09, 0x06 } },
            { 'Q', new byte[] { 0x3E, 0x41, 0x51, 0x21, 0x5E } }, { 'R', new byte[] { 0x7F, 0x09, 0x19, 0x29, 0x46 } },
            { 'S', new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 } }, { 'T', new byte[] { 0x01, 0x01, 0x7F, 0x01, 0x01 } },
            { 'U', new byte[] { 0x3F, 0x40, 0x40, 0x40, 0x3F } }, { 'V', new byte[] { 0x1F, 0x20, 0x40, 0x20, 0x1F } },
            { 'W', new byte[] { 0x3F, 0x40, 0x38, 0x40, 0x3F } }, { 'X', new byte[] { 0x63, 0x14, 0x08, 0x14, 0x63 } },
            { 'Y', new byte[] { 0x07, 0x08, 0x70, 0x08, 0x07 } }, { 'Z', new byte[] { 0x61, 0x51, 0x49, 0x45, 0x43 } },
            { '0', new byte[] { 0x3E, 0x51, 0x49, 0x45, 0x3E } }, { '1', new byte[] { 0x00, 0x42, 0x7F, 0x40, 0x00 } },
            { '2', new byte[] { 0x42, 0x61, 0x51, 0x49, 0x46 } }, { '3', new byte[] { 0x21, 0x41, 0x45, 0x4B, 0x31 } },
            { '4', new byte[] { 0x18, 0x14, 0x12, 0x7F, 0x10 } }, { '5', new byte[] { 0x27, 0x45, 0x45, 0x45, 0x39 } },
            { '6', new byte[] { 0x3C, 0x4A, 0x49, 0x49, 0x30 } }, { '7', new byte[] { 0x01, 0x71, 0x09, 0x05, 0x03 } },
            { '8', new byte[] { 0x36, 0x49, 0x49, 0x49, 0x36 } }, { '9', new byte[] { 0x06, 0x49, 0x49, 0x29, 0x1E } },
            { '-', new byte[] { 0x08, 0x08, 0x08, 0x08, 0x08 } }, { '(', new byte[] { 0x00, 0x1C, 0x22, 0x41, 0x00 } },
            { ')', new byte[] { 0x00, 0x41, 0x22, 0x1C, 0x00 } }, { '.', new byte[] { 0x00, 0x60, 0x60, 0x00, 0x00 } },
            { ',', new byte[] { 0x00, 0x50, 0x30, 0x00, 0x00 } }, { '/', new byte[] { 0x20, 0x10, 0x08, 0x04, 0x02 } },
            { '_', new byte[] { 0x40, 0x40, 0x40, 0x40, 0x40 } }, { '\'', new byte[] { 0x00, 0x05, 0x03, 0x00, 0x00 } },
        };

        /// <summary>Text with its top-left corner at (x, top), y up as in the texture; clipped to maxW pixels.</summary>
        static void DrawText(Texture2D tex, int x, int top, string s, Color c, int scale, int maxW)
        {
            int x0 = x;
            foreach (char raw in s)
            {
                if (x + 5 * scale > x0 + maxW) break;
                char ch = char.ToUpperInvariant(raw);
                if (Glyphs.TryGetValue(ch, out var g))
                    for (int col = 0; col < 5; col++)
                        for (int row = 0; row < 7; row++)
                            if (((g[col] >> row) & 1) != 0) FillRect(tex, x + col * scale, top - (row + 1) * scale, scale, scale, c);
                x += 6 * scale;
            }
        }

        static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
        {
            for (int j = Mathf.Max(0, y); j < Mathf.Min(tex.height, y + h); j++)
                for (int i = Mathf.Max(0, x); i < Mathf.Min(tex.width, x + w); i++) tex.SetPixel(i, j, c);
        }

        /// <summary>(category, path) of every prefab, categories in order, names sorted; Ducks/Live only when asked.</summary>
        static List<(string cat, string path)> ListPrefabs(bool includeLive)
        {
            var list = new List<(string, string)>();
            foreach (var cat in Categories)
            {
                string dir = $"{Root}/{cat}";
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                var paths = AssetDatabase.FindAssets("t:Prefab", new[] { dir }).Select(g => AssetDatabase.GUIDToAssetPath(g))
                    .Where(p => (includeLive || !p.StartsWith(LiveDir + "/")) && !p.StartsWith(AnimDir + "/")).Distinct().OrderBy(p => p, StringComparer.Ordinal);
                foreach (var p in paths) list.Add((cat, p));
            }
            return list;
        }
    }
}
