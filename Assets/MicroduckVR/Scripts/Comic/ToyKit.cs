using UnityEngine;
using Object = UnityEngine.Object;

namespace MicroduckVR.Comic
{
    /// <summary>Runtime comic parts for toys: rounded boxes, round legs and discs, rings and balls, toon materials.</summary>
    public static class ToyKit
    {
        static Mesh sphere;
        public static Mesh Sphere()
        {
            if (sphere != null) return sphere;
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = g.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(g); else Object.DestroyImmediate(g);
            return sphere;
        }

        public static Material Mat(string hex) => ComicLook.ToonMaterial(ComicLook.Hex(hex), "toy_" + hex, ComicLook.DefaultRim, 0.7f);   // shiny plastic: hard spec pop

        public static Transform Part(Transform p, Mesh mesh, string hex, Vector3 lp, Vector3 le, Vector3 ls)
        {
            var go = new GameObject("part");
            go.transform.SetParent(p, false);
            go.transform.localPosition = lp; go.transform.localRotation = Quaternion.Euler(le); go.transform.localScale = ls;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(hex);
            return go.transform;
        }
        static float Q(float v) => Mathf.Max(0.0005f, Mathf.Round(v * 2000f) / 2000f);
        public static Transform Box(Transform p, Vector3 c, Vector3 s, string hex, float round = 0.3f, Vector3 e = default)
        {
            var sz = new Vector3(Q(s.x), Q(s.y), Q(s.z));
            float m = Mathf.Min(sz.x, Mathf.Min(sz.y, sz.z)) * 0.5f;
            return Part(p, ComicMesh.RoundedBox(sz, new ComicMesh.Style { radius = m * round, segments = 2 }), hex, c, e, Vector3.one);
        }
        public static Transform Cyl(Transform p, Vector3 c, float r, float h, string hex, Vector3 e = default, float rTop = -1f, int sides = 16)
            => Part(p, ComicMesh.Leg(Q(h), Q(rTop < 0f ? r : rTop), Q(r), sides), hex, c, e, Vector3.one);
        public static Transform Ball(Transform p, Vector3 c, Vector3 size, string hex, Vector3 e = default) => Part(p, Sphere(), hex, c, e, size);
        public static Transform Ring(Transform p, Vector3 c, float ro, float ri, float h, string hex, Vector3 e = default)
            => Part(p, ComicMesh.Ring(Q(ro), Q(ri), Q(h)), hex, c, e, Vector3.one);

        public static Bounds LocalBounds(Transform root)
        {
            bool any = false; var b = new Bounds();
            var inv = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds; var m = inv * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var q = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
                }
            }
            return b;
        }
    }
}
