using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MicroduckVR.Comic
{
    /// <summary>
    /// A comic sound word ("QUACK!", "POW!") on a spiky burst: pops in with a little overshoot, hangs for a moment
    /// facing you, and pops out. Lives in the world, never on the camera.
    /// </summary>
    public class ComicPop : MonoBehaviour
    {
        static Mesh burst; static Material burstMat;
        /// <summary>Who the words face (filming); the main camera when not set.</summary>
        public static Camera Viewer;
        /// <summary>The comic font for the words (the game HUD fills it, and makes the HUD on first use); none: TextMeshPro's default.</summary>
        public static System.Func<TMP_FontAsset> FontProvider;
        float age, life, size; Transform tilt, ride; Vector3 rideOffset;

        /// <param name="ride">Travel with this transform (your own head): a word about yourself must not be left hanging in the
        /// air for you to skate through.</param>
        public static void Spawn(string word, Vector3 at, string hex = ComicColors.Pink, float size = 1f, float life = 0.9f, Transform ride = null)
        {
            var go = new GameObject("Pop " + word); go.transform.position = at;
            var p = go.AddComponent<ComicPop>(); p.life = life; p.size = size;
            if (ride != null) { p.ride = ride; p.rideOffset = at - ride.position; }
            p.tilt = new GameObject("tilt").transform; p.tilt.SetParent(go.transform, false);
            p.tilt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            if (burstMat == null) { burstMat = ComicLook.ToonMaterial(Color.white, "pop_burst"); burstMat.SetFloat(ShaderIds.Unlit, 1f); }
            var b = new GameObject("burst"); b.transform.SetParent(p.tilt, false); b.transform.localPosition = new Vector3(0f, 0f, 0.014f);
            b.transform.localScale = new Vector3(word.Length * 0.012f + 0.03f, 0.036f, 1f);
            b.AddComponent<MeshFilter>().sharedMesh = Burst();
            var mr = b.AddComponent<MeshRenderer>(); mr.sharedMaterial = burstMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var tg = new GameObject("text"); tg.transform.SetParent(p.tilt, false);
            var t = tg.AddComponent<TextMeshPro>(); var f = FontProvider != null ? FontProvider() : null; if (f != null) t.font = f;
            t.text = word; t.fontSize = 0.3f; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = ComicLook.Hex(hex); t.outlineWidth = 0.25f; t.outlineColor = ComicLook.Ink;
            t.rectTransform.sizeDelta = new Vector2(0.4f, 0.08f);
            go.transform.localScale = Vector3.zero;
            p.Face();
        }

        void Face()
        {
            var cam = Viewer != null ? Viewer : Camera.main; if (cam == null) return;
            var d = transform.position - cam.transform.position;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            float s = age < 0.1f ? Mathf.Lerp(0f, 1.2f, age / 0.1f) : age < 0.18f ? Mathf.Lerp(1.2f, 1f, (age - 0.1f) / 0.08f) : age > life - 0.12f ? Mathf.Lerp(1f, 0f, (age - (life - 0.12f)) / 0.12f) : 1f;
            transform.localScale = Vector3.one * (s * size);
            if (ride != null) { rideOffset += Vector3.up * 0.025f * Time.deltaTime; transform.position = ride.position + rideOffset; }
            else transform.position += Vector3.up * 0.025f * Time.deltaTime;
            Face();
        }

        /// <summary>A spiky comic burst, radius 1, with a little thickness so the ink pass outlines it.</summary>
        public static Mesh Burst()
        {
            if (burst != null) return burst;
            var v = new List<Vector3>(); var t = new List<int>(); const int P = 28; const float th = 0.004f;
            float[] tips = { 1.0f, 1.18f, 0.95f, 1.25f, 1.05f, 1.15f, 0.98f };
            foreach (var z in new[] { -th, th })
            {
                v.Add(new Vector3(0f, 0f, z));
                for (int i = 0; i < P; i++) { float a = i * Mathf.PI * 2f / P, r = i % 2 == 0 ? tips[(i / 2) % tips.Length] : 0.74f; v.Add(new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, z)); }
            }
            int o = P + 1;
            for (int i = 0; i < P; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % P;
                t.Add(0); t.Add(a); t.Add(b); t.Add(o); t.Add(o + b); t.Add(o + a);
                t.Add(a); t.Add(o + a); t.Add(o + b); t.Add(a); t.Add(o + b); t.Add(b);
            }
            burst = new Mesh { name = "comic burst" }; burst.SetVertices(v); burst.SetTriangles(t, 0); burst.RecalculateNormals(); burst.RecalculateBounds();
            return burst;
        }
    }
}
