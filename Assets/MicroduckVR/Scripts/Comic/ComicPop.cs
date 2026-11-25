using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using MicroduckVR.Player;
using MicroduckVR.World;
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
        float age, life, size; Transform tilt;

        public static void Spawn(string word, Vector3 at, string hex = "#FF5D73", float size = 1f, float life = 0.9f)
        {
            var go = new GameObject("Pop " + word); go.transform.position = at;
            var p = go.AddComponent<ComicPop>(); p.life = life; p.size = size;
            p.tilt = new GameObject("tilt").transform; p.tilt.SetParent(go.transform, false);
            p.tilt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
            if (burstMat == null) { burstMat = ComicLook.ToonMaterial(Color.white, "pop_burst"); burstMat.SetFloat("_Unlit", 1f); }
            var b = new GameObject("burst"); b.transform.SetParent(p.tilt, false); b.transform.localPosition = new Vector3(0f, 0f, 0.014f);
            b.transform.localScale = new Vector3(word.Length * 0.012f + 0.03f, 0.036f, 1f);
            b.AddComponent<MeshFilter>().sharedMesh = Burst();
            var mr = b.AddComponent<MeshRenderer>(); mr.sharedMaterial = burstMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var tg = new GameObject("text"); tg.transform.SetParent(p.tilt, false);
            var t = tg.AddComponent<TextMeshPro>(); var f = GameHud.Get().Font; if (f != null) t.font = f;
            t.text = word; t.fontSize = 0.3f; t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.color = ComicLook.Hex(hex); t.outlineWidth = 0.25f; t.outlineColor = ComicLook.Ink;
            t.rectTransform.sizeDelta = new Vector2(0.4f, 0.08f);
            go.transform.localScale = Vector3.zero;
            p.Face();
        }

        void Face()
        {
            var cam = Camera.main; if (cam == null) return;
            var d = transform.position - cam.transform.position;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }
            float s = age < 0.1f ? Mathf.Lerp(0f, 1.2f, age / 0.1f) : age < 0.18f ? Mathf.Lerp(1.2f, 1f, (age - 0.1f) / 0.08f) : age > life - 0.12f ? Mathf.Lerp(1f, 0f, (age - (life - 0.12f)) / 0.12f) : 1f;
            transform.localScale = Vector3.one * (s * size);
            transform.position += Vector3.up * 0.025f * Time.deltaTime;
            Face();
        }

        /// <summary>A spiky comic burst, radius 1, with a little thickness so the ink pass outlines it.</summary>
        static Mesh Burst()
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

    /// <summary>Watches the player's duck and the toys and throws out sound words at the right moments.</summary>
    public class ComicEvents : MonoBehaviour
    {
        int chirps = -1, grabs = -1; bool boosting, inWater; float scanT, powCd, waterT;
        readonly Dictionary<Rigidbody, float> speeds = new Dictionary<Rigidbody, float>();
        readonly List<Rigidbody> props = new List<Rigidbody>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindAnyObjectByType<ComicEvents>() == null) new GameObject("Comic events").AddComponent<ComicEvents>(); }

        /// <summary>Where a word about the player's own duck goes: in front of you in first person, over the duck in third.</summary>
        static Vector3 Near(PlayerDuck pd, float ahead, float up)
        {
            var c = pd.cam.transform; var f = c.forward; f.y = 0f; f = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            if (pd.thirdPerson) return pd.Body.Pos + Vector3.up * (0.2f + up);
            return c.position + f * ahead + Vector3.up * up + c.right * Random.Range(-0.05f, 0.05f);
        }

        void Update()
        {
            var pd = PlayerDuck.Instance; if (pd == null || pd.cam == null || pd.Body == null) return;
            float dt = Time.deltaTime; powCd -= dt;
            if (chirps < 0) { chirps = pd.ChirpCount; grabs = pd.GrabCount; }
            if (pd.ChirpCount != chirps) { chirps = pd.ChirpCount; ComicPop.Spawn("QUACK!", Near(pd, 0.3f, 0.05f), "#FFC43D"); }
            if (pd.GrabCount != grabs) { grabs = pd.GrabCount; ComicPop.Spawn("CHOMP!", Near(pd, 0.28f, 0.02f), "#FF5D73", 0.85f); }
            var sk = pd.Skates;
            bool b = sk != null && sk.On && sk.Boosting;
            if (b && !boosting) ComicPop.Spawn("ZOOM!", Near(pd, 0.4f, -0.02f), "#3A86FF", 1.1f);
            boosting = b;

            waterT -= dt;
            if (waterT <= 0f)
            {
                waterT = 0.1f;
                bool w = Physics.Raycast(pd.Body.Pos + Vector3.up * 0.05f, Vector3.down, out var hit, 0.3f, 1 << DuckSetup.LayerWorld, QueryTriggerInteraction.Ignore)
                         && hit.collider.GetComponent<SurfaceTag>() is SurfaceTag st && st.type == "water";
                if (w && !inWater) ComicPop.Spawn("SPLASH!", Near(pd, 0.32f, 0f), "#3A86FF", 1.1f);
                inWater = w;
            }

            // a toy suddenly sent flying near you: POW!
            scanT -= dt;
            if (scanT <= 0f)
            {
                scanT = 1.5f; props.Clear(); speeds.Clear();
                foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
                    if (rb.gameObject.layer == DuckSetup.LayerProp && !rb.isKinematic && (rb.position - pd.Body.Pos).sqrMagnitude < 9f) { props.Add(rb); speeds[rb] = rb.linearVelocity.magnitude; }
            }
            foreach (var rb in props)
            {
                if (rb == null) continue;
                float s = rb.linearVelocity.magnitude, last = speeds.TryGetValue(rb, out var l) ? l : s;
                if (s - last > 0.45f && powCd <= 0f) { powCd = 0.7f; ComicPop.Spawn(Random.value < 0.5f ? "POW!" : "BONK!", rb.worldCenterOfMass + Vector3.up * 0.1f, "#FF8A3D", 1.2f); }
                speeds[rb] = s;
            }
        }
    }
}
