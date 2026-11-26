using MicroduckVR.Comic;
using MicroduckVR.Core;
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
        const string FontResource = "Bangers-Regular";          // Resources.Load name of the comic font
        static GameHud inst;
        /// <summary>The HUD, made on first use.</summary>
        public static GameHud Get()
        {
            if (inst != null) return inst;
            inst = new GameObject("Game HUD").AddComponent<GameHud>();
            return inst;
        }

        /// <summary>Comic sound words use the HUD's font, in play mode and in edit mode.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        static void RegisterFont() { ComicPop.FontProvider = () => Get().Font; }

        Camera cam; Transform root, card, bigRoot, bigBurst; TextMeshPro caption, big, bigInk, arrowL, arrowR; TMP_FontAsset font;
        float bigT, bigDur, hudYaw; bool placed, glide; string capText = "";

        void Awake()
        {
            cam = Camera.main;
            var f = Resources.Load<Font>(FontResource);
            foreach (var tb in FindObjectsByType<MicroduckVR.Flock.ThoughtBubbles>()) if (tb.font != null) f = tb.font;
            if (f != null) font = TMP_FontAsset.CreateFontAsset(f);
            root = new GameObject("hud root").transform; root.SetParent(transform, false);
            card = new GameObject("card").transform; card.SetParent(root, false); card.localPosition = new Vector3(0f, 0.1f, 0f);
            var slab = new GameObject("slab"); slab.transform.SetParent(card, false); slab.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            slab.AddComponent<MeshFilter>().sharedMesh = ComicMesh.RoundedBox(new Vector3(0.23f, 0.04f, 0.005f), new ComicMesh.Style { radius = 0.004f, segments = 2 });
            slab.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);          // a comic caption box, slightly askew
            var m = ComicLook.ToonMaterial(ComicLook.Hex(ComicColors.Yellow), "hud_caption"); m.SetFloat(ShaderIds.Unlit, 1f);
            var mr = slab.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            caption = Text(card, 0.17f, new Vector3(0f, 0f, -0.001f), ComicLook.Ink);
            // the big shout sits a little above eye level (other ducks crowd the middle of the view) on a hard ink shadow,
            // so it reads against any wall
            bigRoot = new GameObject("big").transform; bigRoot.SetParent(root, false); bigRoot.localPosition = new Vector3(0f, 0.045f, 0f);
            // ... and on a white comic burst: without a solid back the ink lines of whatever is behind show through the letters
            var bg = new GameObject("burst"); bigBurst = bg.transform; bigBurst.SetParent(bigRoot, false); bigBurst.localPosition = new Vector3(0f, 0f, 0.008f);
            bg.AddComponent<MeshFilter>().sharedMesh = ComicPop.Burst();
            var bm = ComicLook.ToonMaterial(Color.white, "hud_burst"); bm.SetFloat(ShaderIds.Unlit, 1f);
            var bmr = bg.AddComponent<MeshRenderer>(); bmr.sharedMaterial = bm; bmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; bmr.receiveShadows = false;
            bigInk = Text(bigRoot, 0.5f, new Vector3(0.0045f, -0.0045f, 0.001f), ComicLook.Ink);
            big = Text(bigRoot, 0.5f, Vector3.zero, ComicLook.Hex(ComicColors.Pink));
            big.outlineWidth = 0.2f; big.outlineColor = ComicLook.Ink; bigInk.outlineWidth = 0.2f; bigInk.outlineColor = ComicLook.Ink;
            // "that way": arrows either side of the caption (the race points you at the next gate with them)
            arrowL = Text(root, 0.42f, new Vector3(-0.17f, 0.1f, 0f), ComicLook.Hex(ComicColors.Pink)); arrowL.text = "<<";
            arrowR = Text(root, 0.42f, new Vector3(0.17f, 0.1f, 0f), ComicLook.Hex(ComicColors.Pink)); arrowR.text = ">>";
            arrowL.rectTransform.sizeDelta = arrowR.rectTransform.sizeDelta = new Vector2(0.08f, 0.06f);
            arrowL.gameObject.SetActive(false); arrowR.gameObject.SetActive(false);
            card.gameObject.SetActive(false); bigRoot.gameObject.SetActive(false);
        }

        TextMeshPro Text(Transform p, float size, Vector3 at, Color c)
        {
            var go = new GameObject("text"); go.transform.SetParent(p, false); go.transform.localPosition = at;
            var t = go.AddComponent<TextMeshPro>(); if (font != null) t.font = font;
            t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(0.3f, 0.06f);
            return t;
        }

        /// <summary>The comic font (Bangers), loaded once.</summary>
        public TMP_FontAsset Font => font;
        /// <summary>The HUD (kept out of the mirror's reflection).</summary>
        public static Transform Root => inst != null ? inst.root : null;
        /// <summary>Filming: keep the HUD out of cinematic shots.</summary>
        public static bool Hidden;
        /// <summary>Sets the caption card text; empty hides the card.</summary>
        public void Caption(string s) { capText = s ?? ""; card.gameObject.SetActive(capText.Length > 0); caption.text = capText; }
        /// <summary>Pops a big comic word for a moment.</summary>
        public void Big(string s, string hex = ComicColors.Pink, float dur = 1.1f) 
        {
            // long shouts are set smaller so they never fill the whole view
            float size = Mathf.Min(0.5f, 4.8f / Mathf.Max(1, s.Length));
            big.fontSize = size; bigInk.fontSize = size;
            bigBurst.localScale = new Vector3(s.Length * size * 0.1f * 0.42f * 0.62f + 0.035f, size * 0.1f * 1.15f, 1f);
            big.text = s; bigInk.text = s; big.color = ComicLook.Hex(hex); bigT = 0f; bigDur = dur; bigRoot.gameObject.SetActive(true);
        }
        /// <summary>Arrows beside the caption: -1 turn left, 1 turn right, 0 none.</summary>
        public void Arrow(int dir) { arrowL.gameObject.SetActive(dir < 0); arrowR.gameObject.SetActive(dir > 0); }
        /// <summary>Hides the caption, the arrows and the big word.</summary>
        public void Clear() { Caption(""); Arrow(0); bigRoot.gameObject.SetActive(false); }

        void LateUpdate()
        {
            root.gameObject.SetActive(!Hidden); if (Hidden) return;
            if (cam == null) cam = Camera.main; if (cam == null) return;
            // rides with your eyes (always the same distance, however fast you go), turns after you once you have turned away
            var f = cam.transform.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward; f.Normalize();
            float yawWant = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, dy = Mathf.DeltaAngle(hudYaw, yawWant);
            if (!placed || Mathf.Abs(dy) > 100f) { hudYaw = yawWant; placed = true; glide = false; }
            else
            {
                if (Mathf.Abs(dy) > 25f) glide = true;
                if (glide) { hudYaw = Mathf.LerpAngle(hudYaw, yawWant, 1f - Mathf.Exp(-6f * Time.deltaTime)); if (Mathf.Abs(dy) < 2f) glide = false; }
            }
            var dir = Quaternion.Euler(0f, hudYaw, 0f) * Vector3.forward;
            var eye = cam.transform.position;
            float dist = 0.24f, k = 0.7f;      // close and small (the same size in the eye as 0.34 m at full scale): in front of the tutorial card and of ducks that crowd round
            // never inside a wall: come closer to the eyes instead, and shrink to look the same size
            if (Physics.SphereCast(eye, 0.03f, dir, out var hit, dist + 0.02f, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore)) { float near = Mathf.Max(0.12f, hit.distance - 0.02f); k *= near / dist; dist = near; }
            root.position = eye + dir * dist + Vector3.down * 0.02f * k;
            root.localScale = Vector3.one * k;
            root.rotation = Quaternion.LookRotation(root.position - eye, Vector3.up);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 9f);
            arrowL.transform.localScale = arrowR.transform.localScale = Vector3.one * pulse;
            if (bigRoot.gameObject.activeSelf)
            {
                bigT += Time.deltaTime;
                float s = bigT < 0.15f ? Mathf.SmoothStep(0.3f, 1.15f, bigT / 0.15f) : bigT < 0.3f ? Mathf.Lerp(1.15f, 1f, (bigT - 0.15f) / 0.15f) : bigT > bigDur ? Mathf.Lerp(1f, 0f, (bigT - bigDur) / 0.2f) : 1f;
                bigRoot.localScale = Vector3.one * s;
                if (bigT > bigDur + 0.2f) bigRoot.gameObject.SetActive(false);
            }
        }
    }
}
