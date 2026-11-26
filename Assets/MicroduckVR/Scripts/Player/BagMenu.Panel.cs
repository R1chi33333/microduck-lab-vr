using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.World;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Player
{
    public partial class BagMenu
    {
        readonly Dictionary<Transform, Material> baseMat = new Dictionary<Transform, Material>();

        // ------------------------------------------------------------------ building
        void BuildPanel()
        {
            panel = new GameObject("Bag panel").transform; Root = panel;
            var entries = ToyCatalog.All;
            // toys, then the bin and help, then one row of actions (games, emotes, swap, view)
            // toys and the bin as picture tiles; under them the word buttons (games, emotes, ducks, help)
            int nA = entries.Count + 1, rowsA = Mathf.CeilToInt(nA / (float)Cols), n = nA + Actions.Length;
            int rowsB = Mathf.CeilToInt(Actions.Length / (float)ACols);
            float w = Cols * (Tile + Gap) + Gap * 2f, h = rowsA * (Tile + Gap) + 0.008f + rowsB * (AH + Gap) + 0.056f + NetRow;
            float aw = (Cols * (Tile + Gap) - Gap - (ACols - 1) * Gap) / ACols;
            var slab = MakeMesh(panel, ComicMesh.RoundedBox(new Vector3(w, h, 0.006f), new ComicMesh.Style { radius = 0.01f, segments = 3 }), Unlit("#FFFFFF", "bag_slab"));
            slab.localPosition = new Vector3(0f, -0.004f, 0.004f);
            var tab = MakeMesh(panel, ComicMesh.RoundedBox(new Vector3(0.11f, 0.03f, 0.005f), new ComicMesh.Style { radius = 0.008f, segments = 3 }), Unlit(ComicColors.Yellow, "bag_tab"));
            tab.localPosition = new Vector3(0f, h / 2f + 0.002f, -0.0015f); tab.localRotation = Quaternion.Euler(0f, 0f, -2f);
            var title = Text(panel, "TOY BAG!", 0.16f, new Vector3(0f, h / 2f + 0.001f, -0.0045f));
            title.color = ComicLook.Ink; title.transform.localRotation = Quaternion.Euler(0f, 0f, -2f);
            footer = Text(panel, "", 0.13f, new Vector3(0f, -h / 2f + 0.011f + NetRow, -0.001f)); footer.rectTransform.sizeDelta = new Vector2(w, 0.03f);
            netLine = Text(panel, "", 0.1f, new Vector3(0f, -h / 2f + 0.01f, -0.001f)); netLine.rectTransform.sizeDelta = new Vector2(w - 0.012f, 0.02f);
            netLine.enableAutoSizing = true; netLine.fontSizeMin = 0.05f; netLine.fontSizeMax = 0.1f;   // long addresses shrink to fit
            netLine.color = ComicLook.Hex(ComicColors.Blue);
            float x0 = -(Cols - 1) * (Tile + Gap) / 2f, y0 = h / 2f - 0.045f;
            for (int i = 0; i < n; i++)
            {
                bool word = i >= nA; int j = i - nA;
                var size = word ? new Vector2(aw, AH) : new Vector2(Tile, Tile);
                var at = word ? new Vector2(-(ACols - 1) * (aw + Gap) / 2f + (j % ACols) * (aw + Gap), y0 - rowsA * (Tile + Gap) - 0.008f + (Tile - AH) / 2f - (j / ACols) * (AH + Gap))
                              : new Vector2(x0 + (i % Cols) * (Tile + Gap), y0 - (i / Cols) * (Tile + Gap));
                var tile = MakeMesh(panel, ComicMesh.RoundedBox(new Vector3(size.x, size.y, 0.004f), new ComicMesh.Style { radius = 0.005f, segments = 2 }), tileMat);
                tile.localPosition = new Vector3(at.x, at.y, 0f);
                tileHalf.Add(size / 2f);
                Transform model; string kind;
                if (i < entries.Count)
                {
                    kind = entries[i].kind;
                    var vis = ToyCatalog.BuildVisual(kind);
                    model = new GameObject("preview").transform; model.SetParent(tile, false);
                    vis.transform.SetParent(model, false);
                    var b = ToyKit.LocalBounds(vis.transform);
                    float s = Tile * 0.78f / Mathf.Max(b.size.magnitude, 1e-4f);     // the diagonal, so spinning never pokes out of the tile
                    vis.transform.localScale = Vector3.one * s; vis.transform.localPosition = -b.center * s;
                    model.localPosition = new Vector3(0f, 0f, -0.014f); model.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                }
                else if (word)
                {
                    var (akind, alabel, acol) = Actions[j];
                    kind = akind;
                    tile.GetComponent<MeshRenderer>().sharedMaterial = Unlit(acol, "bag_action_" + akind);
                    model = new GameObject("label").transform; model.SetParent(tile, false); model.localPosition = new Vector3(0f, 0f, -0.0035f);
                    var lt = Text(model, alabel, alabel.Length > 9 ? 0.125f : 0.15f, Vector3.zero);
                    lt.rectTransform.sizeDelta = size;
                    actionLabels[akind] = lt;
                }
                else
                {
                    kind = BagActions.Trash;
                    model = new GameObject("bin").transform; model.SetParent(tile, false); model.localPosition = new Vector3(0f, -0.006f, -0.012f);
                    ToyKit.Cyl(model, new Vector3(0f, 0.006f, 0f), 0.0075f, 0.014f, "#97A0AE", default, 0.0085f, 14);
                    ToyKit.Cyl(model, new Vector3(0f, 0.0145f, 0f), 0.0095f, 0.002f, "#5B6472", default, -1f, 14);
                    ToyKit.Box(model, new Vector3(0f, 0.017f, 0f), new Vector3(0.006f, 0.002f, 0.002f), "#5B6472", 0.3f);
                    model.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                }
                foreach (var mr in model.GetComponentsInChildren<MeshRenderer>()) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; }
                baseMat[tile] = tile.GetComponent<MeshRenderer>().sharedMaterial;
                tiles.Add((tile, model, kind));
            }
            panel.gameObject.SetActive(false);
        }

        static Transform MakeMesh(Transform parent, Mesh mesh, Material mat)
        {
            var go = new GameObject("mesh"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        TMP_FontAsset fontAsset;
        TextMeshPro Text(Transform parent, string s, float size, Vector3 at)
        {
            var go = new GameObject("text"); go.transform.SetParent(parent, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>();
            if (fontAsset == null && font != null) fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset != null) t.font = fontAsset;
            t.text = s; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.color = ComicLook.Ink;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(0.24f, 0.03f);
            return t;
        }

        void BuildLaser()
        {
            laser = new ComicBeam("Bag laser", ComicColors.Pink, 0.0022f, 0.008f);
        }

        void SetHover(int h)
        {
            if (h == hover) return;
            if (hover >= 0) { tiles[hover].tile.GetComponent<MeshRenderer>().sharedMaterial = baseMat[tiles[hover].tile]; tiles[hover].tile.localScale = Vector3.one; }
            hover = h;
            if (hover >= 0) { tiles[hover].tile.GetComponent<MeshRenderer>().sharedMaterial = tileHover; tiles[hover].tile.localScale = Vector3.one * (tileHalf[hover].x > Tile ? 1.06f : 1.15f); Play(clickClip, 0.4f); }
        }

        void ShowGhost(string kind, Vector3 at, float yaw, bool ok)
        {
            if (ghost == null || ghostKind != kind)
            {
                DropGhost();
                ghost = ToyCatalog.BuildVisual(kind); ghostKind = kind; ghost.name = "Bag ghost";
                foreach (var mr in ghost.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostRing = MakeMesh(ghost.transform, ComicMesh.Ring(0.036f, 0.03f, 0.002f, 32), ghostOk);
                ghostRing.localPosition = Vector3.up * 0.0015f;
            }
            ghost.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var m = ok ? ghostOk : ghostBad;
            foreach (var mr in ghost.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = m;
            float s = 1f + 0.04f * Mathf.Sin(pulse * 6f);
            ghostRing.localScale = new Vector3(s, 1f, s);
        }

        void DropGhost() { if (ghost != null) Destroy(ghost); ghost = null; ghostKind = null; }
    }
}
