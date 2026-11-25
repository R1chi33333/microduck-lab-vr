using MicroduckVR.Comic;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Games
{
    /// <summary>
    /// Comic HUD for the games: a small white caption card (score, lap, time) and big pop-up words (3, 2, 1, GO!,
    /// GOAL!). It floats in front of you and lazily follows your view instead of being glued to your face.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        static GameHud inst;
        public static GameHud Get()
        {
            if (inst != null) return inst;
            inst = new GameObject("Game HUD").AddComponent<GameHud>();
            return inst;
        }

        Camera cam; Transform root, card; TextMeshPro caption, big; TMP_FontAsset font;
        float bigT, bigDur; string capText = "";

        void Awake()
        {
            cam = Camera.main;
            var f = Resources.Load<Font>("Bangers-Regular");
            foreach (var tb in FindObjectsByType<MicroduckVR.Flock.ThoughtBubbles>(FindObjectsSortMode.None)) if (tb.font != null) f = tb.font;
            if (f != null) font = TMP_FontAsset.CreateFontAsset(f);
            root = new GameObject("hud root").transform; root.SetParent(transform, false);
            card = new GameObject("card").transform; card.SetParent(root, false); card.localPosition = new Vector3(0f, 0.085f, 0f);
            var slab = new GameObject("slab"); slab.transform.SetParent(card, false); slab.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            slab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(0.16f, 0.028f, 0.004f), new ComicMesh.Style { radius = 0.008f, segments = 3 });
            var m = ComicLook.ToonMaterial(Color.white, "hud_white"); m.SetFloat("_Unlit", 1f);
            var mr = slab.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            caption = Text(card, 0.11f, Vector3.zero, ComicLook.Ink);
            big = Text(root, 0.5f, new Vector3(0f, 0.01f, 0f), ComicLook.Hex("#FF5D73"));
            big.outlineWidth = 0.25f; big.outlineColor = ComicLook.Ink;
            card.gameObject.SetActive(false); big.gameObject.SetActive(false);
        }

        TextMeshPro Text(Transform p, float size, Vector3 at, Color c)
        {
            var go = new GameObject("text"); go.transform.SetParent(p, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); if (font != null) t.font = font;
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(0.3f, 0.06f);
            return t;
        }

        public void Caption(string s) { capText = s ?? ""; card.gameObject.SetActive(capText.Length > 0); caption.text = capText; }
        public void Big(string s, string hex = "#FF5D73", float dur = 1.1f) { big.text = s; big.color = ComicLook.Hex(hex); bigT = 0f; bigDur = dur; big.gameObject.SetActive(true); }
        public void Clear() { Caption(""); big.gameObject.SetActive(false); }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main; if (cam == null) return;
            // lazy follow: sits 0.3 m ahead (duck scale) and catches up when the view drifts away
            var f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            var want = cam.transform.position + f * 0.3f + Vector3.down * 0.02f;
            float a = Vector3.Angle(root.position - cam.transform.position, f);
            if (Vector3.Distance(root.position, want) > 0.5f) root.position = want;
            else if (a > 25f || Vector3.Distance(root.position, want) > 0.08f) root.position = Vector3.Lerp(root.position, want, 1f - Mathf.Exp(-4f * Time.deltaTime));
            root.rotation = Quaternion.LookRotation(root.position - cam.transform.position, Vector3.up);
            if (big.gameObject.activeSelf)
            {
                bigT += Time.deltaTime;
                float s = bigT < 0.15f ? Mathf.SmoothStep(0.3f, 1.25f, bigT / 0.15f) : bigT < 0.3f ? Mathf.Lerp(1.25f, 1f, (bigT - 0.15f) / 0.15f) : bigT > bigDur ? Mathf.Lerp(1f, 0f, (bigT - bigDur) / 0.2f) : 1f;
                big.transform.localScale = Vector3.one * s;
                if (bigT > bigDur + 0.2f) big.gameObject.SetActive(false);
            }
        }
    }
}
