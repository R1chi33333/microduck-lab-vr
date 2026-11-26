using UnityEngine;

namespace MicroduckVR.Comic
{
    /// <summary>A comic splash: fat drops thrown up out of the puddle and a ring spreading on the water.</summary>
    public class ComicSplash : MonoBehaviour
    {
        static Material[] mats;
        Transform[] drops; Vector3[] vel; float[] sizes; Transform ring; float age, size;
        const float Life = 0.8f;

        public static void Spawn(Vector3 at, float size = 1f)
        {
            if (mats == null)
            {
                mats = new Material[3]; string[] hex = { ComicColors.Blue, "#BFE3FF", "#FFFFFF" };
                for (int i = 0; i < 3; i++) { mats[i] = ComicLook.ToonMaterial(ComicLook.Hex(hex[i]), "splash_" + i); mats[i].SetFloat(ShaderIds.Unlit, 1f); }
            }
            var go = new GameObject("Splash"); go.transform.position = at;
            var s = go.AddComponent<ComicSplash>(); s.size = size;
            const int N = 10;
            s.drops = new Transform[N]; s.vel = new Vector3[N]; s.sizes = new float[N];
            for (int i = 0; i < N; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(d.GetComponent<Collider>());
                d.name = "drop"; d.transform.SetParent(go.transform, false);
                var mr = d.GetComponent<MeshRenderer>(); mr.sharedMaterial = mats[i % 3]; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                float a = (i + Random.value * 0.7f) * Mathf.PI * 2f / N, out_ = Random.Range(0.1f, 0.28f) * size;
                s.sizes[i] = Random.Range(0.013f, 0.026f) * size;
                d.transform.localScale = new Vector3(0.8f, 1.25f, 0.8f) * s.sizes[i];
                d.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.02f + Vector3.up * 0.012f;
                s.vel[i] = new Vector3(Mathf.Cos(a) * out_, Random.Range(0.45f, 0.85f) * Mathf.Sqrt(size), Mathf.Sin(a) * out_);
                s.drops[i] = d.transform;
            }
            s.ring = ToyKit.Ring(go.transform, new Vector3(0f, 0.004f, 0f), 0.05f * size, 0.038f * size, 0.004f, "#FFFFFF");
            var rr = s.ring.GetComponent<MeshRenderer>(); if (rr != null) { rr.sharedMaterial = mats[2]; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        }

        void Update()
        {
            float dt = Time.deltaTime; age += dt;
            if (age >= Life) { Destroy(gameObject); return; }
            for (int i = 0; i < drops.Length; i++)
            {
                vel[i].y -= 3.0f * dt;                          // toy-box gravity: the drops hang in the air long enough to see
                var p = drops[i].localPosition + vel[i] * dt;
                bool down = p.y < 0.006f && vel[i].y < 0f;
                drops[i].localPosition = p;
                float k = down ? 0f : age > Life - 0.2f ? (Life - age) / 0.2f : 1f;
                drops[i].localScale = new Vector3(0.8f, 1.25f, 0.8f) * sizes[i] * k;
            }
            float u = age / Life, grow = 1f + u * 2.4f;
            ring.localScale = new Vector3(grow, Mathf.Max(0.05f, 1f - u), grow);
        }
    }
}
