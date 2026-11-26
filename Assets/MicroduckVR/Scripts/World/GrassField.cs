using System.Collections.Generic;
using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Flock;
using MicroduckVR.Player;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// Life in the lawn: tells the grass shader where the ducks and loose toys are so the blades part around
    /// them, and throws up a few comic grass clippings when the player's duck wades through.
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        const int Max = 12;
        static readonly int PushersId = Shader.PropertyToID("_GrassPushers"), CountId = Shader.PropertyToID("_GrassPusherCount");
        readonly Vector4[] pushers = new Vector4[Max];
        readonly List<Rigidbody> props = new List<Rigidbody>();
        readonly List<(Transform t, Vector3 v, float age, float life, float spin)> bits = new List<(Transform, Vector3, float, float, float)>();
        float scanT, bitT; Vector3 lastPos; bool hasLast; Material leafA, leafB; static Mesh leaf;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (FindAnyObjectByType<GrassField>() == null) new GameObject("Grass field").AddComponent<GrassField>(); }

        void Start()
        {
            leafA = ComicLook.ToonMaterial(ComicLook.Hex("#8FD35A"), "grass_bit_a"); leafA.SetFloat("_Unlit", 1f);
            leafB = ComicLook.ToonMaterial(ComicLook.Hex("#46A64A"), "grass_bit_b"); leafB.SetFloat("_Unlit", 1f);
        }

        void OnDisable() { Shader.SetGlobalFloat(CountId, 0f); }

        static bool OnLawn(Vector3 p) => p.z > 3.9f && p.y < 0.25f;

        void LateUpdate()
        {
            scanT -= Time.deltaTime;
            if (scanT <= 0f)
            {
                scanT = 1f; props.Clear();
                foreach (var rb in FindObjectsByType<Rigidbody>())
                    if (rb.gameObject.layer == DuckSetup.LayerProp && OnLawn(rb.position)) props.Add(rb);
            }
            int n = 0;
            var pd = PlayerDuck.Instance;
            if (pd != null && pd.Body != null && OnLawn(pd.Body.Pos)) pushers[n++] = Pack(pd.Body.Pos, 0.085f);
            foreach (var d in DuckBrain.All) if (n < Max && d != null && OnLawn(d.Pos)) pushers[n++] = Pack(d.Pos, 0.085f);
            foreach (var r in MicroduckVR.Net.PlayerTargets.Remote) if (n < Max && r != null && r.Body != null && OnLawn(r.Body.Pos)) pushers[n++] = Pack(r.Body.Pos, 0.085f);
            foreach (var rb in props)
            {
                if (n >= Max) break;
                if (rb == null || !OnLawn(rb.position)) continue;
                float r = Mathf.Clamp(rb.GetComponentInChildren<Collider>() is Collider c ? Mathf.Max(c.bounds.extents.x, c.bounds.extents.z) * 1.25f : 0.05f, 0.03f, 0.12f);
                pushers[n++] = Pack(rb.worldCenterOfMass, r);
            }
            for (int i = n; i < Max; i++) pushers[i] = new Vector4(0f, -100f, 0f, 0.001f);
            Shader.SetGlobalVectorArray(PushersId, pushers); Shader.SetGlobalFloat(CountId, n);

            Clippings(pd);
        }

        static Vector4 Pack(Vector3 p, float r) => new Vector4(p.x, p.y, p.z, r);

        // ------------------------------------------------------------------ clippings
        void Clippings(PlayerDuck pd)
        {
            float dt = Time.deltaTime;
            if (pd != null && pd.Body != null)
            {
                var p = pd.Body.Pos;
                float speed = hasLast && dt > 1e-5f ? Vector3.ProjectOnPlane(p - lastPos, Vector3.up).magnitude / dt : 0f;
                lastPos = p; hasLast = true; bitT -= dt;
                if (speed > 0.06f && speed < 3f && bitT <= 0f && bits.Count < 40 && OnGrass(p))
                {
                    bitT = Mathf.Lerp(0.16f, 0.05f, Mathf.Clamp01(speed / 0.6f));
                    var at = new Vector3(p.x, p.y, p.z) + Random.insideUnitSphere * 0.03f; at.y = p.y + 0.03f;
                    var v = Random.insideUnitSphere * 0.12f; v.y = Random.Range(0.22f, 0.4f);
                    var t = Bit(Random.value < 0.5f ? leafA : leafB, at);
                    bits.Add((t, v, 0f, Random.Range(0.5f, 0.8f), Random.Range(-540f, 540f)));
                }
            }
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var (t, v, age, life, spin) = bits[i]; age += dt;
                if (age >= life) { Destroy(t.gameObject); bits.RemoveAt(i); continue; }
                v += Vector3.down * 0.9f * dt; v *= Mathf.Exp(-1.5f * dt); t.position += v * dt;
                t.Rotate(spin * dt, spin * 0.6f * dt, 0f, Space.Self);
                float u = age / life, s = 0.02f * (u < 0.15f ? u / 0.15f : 1f - 0.5f * (u - 0.15f) / 0.85f);
                t.localScale = new Vector3(s, s, s);
                bits[i] = (t, v, age, life, spin);
            }
        }

        static bool OnGrass(Vector3 p)
        {
            if (!OnLawn(p)) return false;
            if (!Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out var hit, 0.3f, 1 << DuckSetup.LayerWorld, QueryTriggerInteraction.Ignore)) return false;
            var tag = hit.collider.GetComponent<SurfaceTag>();
            return tag != null && tag.Kind == SurfaceType.Grass;
        }

        Transform Bit(Material m, Vector3 at)
        {
            var go = new GameObject("grass bit");
            go.AddComponent<MeshFilter>().sharedMesh = Leaf();
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            go.transform.SetPositionAndRotation(at, Random.rotation); go.transform.localScale = Vector3.zero;
            return go.transform;
        }

        /// <summary>A little pointed leaf, 1 long, with a bit of thickness so the ink pass outlines it.</summary>
        static Mesh Leaf()
        {
            if (leaf != null) return leaf;
            var v = new List<Vector3>(); var tr = new List<int>();
            Vector2[] o = { new Vector2(0f, -0.5f), new Vector2(0.2f, -0.1f), new Vector2(0.14f, 0.25f), new Vector2(0f, 0.5f), new Vector2(-0.14f, 0.25f), new Vector2(-0.2f, -0.1f) };
            const float th = 0.05f; int n = o.Length;
            foreach (var z in new[] { -th, th }) { v.Add(new Vector3(0f, 0f, z)); foreach (var p in o) v.Add(new Vector3(p.x, p.y, z)); }
            int k = n + 1;
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                tr.Add(0); tr.Add(b); tr.Add(a); tr.Add(k); tr.Add(k + a); tr.Add(k + b);
                tr.Add(a); tr.Add(b); tr.Add(k + b); tr.Add(a); tr.Add(k + b); tr.Add(k + a);
            }
            leaf = new Mesh { name = "grass leaf" }; leaf.SetVertices(v); leaf.SetTriangles(tr, 0); leaf.RecalculateNormals(); leaf.RecalculateBounds();
            return leaf;
        }
    }
}
