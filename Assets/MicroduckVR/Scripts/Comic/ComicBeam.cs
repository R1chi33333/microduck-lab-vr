using UnityEngine;

namespace MicroduckVR.Comic
{
    /// <summary>
    /// A comic laser beam: a chunky flat-coloured tube (the ink pass outlines it) with a fat dot and a white core
    /// where it lands. Replaces thin LineRenderers so pointers read like everything else in the game.
    /// </summary>
    public class ComicBeam
    {
        readonly Transform tube, dot, core;
        public float radius;
        bool shown;

        public ComicBeam(string name, string hex, float radius = 0.0016f, float dotRadius = 0.007f)
        {
            this.radius = radius;
            var mat = ComicLook.ToonMaterial(ComicLook.Hex(hex), "beam_" + hex); mat.SetFloat(ShaderIds.Unlit, 1f);
            var white = ComicLook.ToonMaterial(Color.white, "beam_core"); white.SetFloat(ShaderIds.Unlit, 1f);
            tube = Make(name + " beam", ComicMesh.Leg(1f, 1f, 1f, 10), mat);
            dot = Make(name + " dot", ComicMesh.Leg(0.25f, 1f, 1f, 20), mat);
            core = Make(name + " core", ComicMesh.Leg(0.3f, 1f, 1f, 16), white);
            dot.localScale = new Vector3(dotRadius, dotRadius, dotRadius);
            core.localScale = new Vector3(dotRadius * 0.45f, dotRadius, dotRadius * 0.45f);
            Show(false);
        }

        static Transform Make(string n, Mesh m, Material mat)
        {
            var go = new GameObject(n);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void Show(bool on) { shown = on; if (tube == null) return; tube.gameObject.SetActive(on); dot.gameObject.SetActive(on); core.gameObject.SetActive(on); }
        public void ShowDot(bool on) { dot.gameObject.SetActive(on); core.gameObject.SetActive(on); }

        /// <summary>From a to b; the dot lies on the surface with the given normal (or faces back up the beam).</summary>
        public void Set(Vector3 a, Vector3 b, Vector3? normal = null)
        {
            if (!shown) return;                          // a late Set after Show(false) must not bring the beam back
            var d = b - a; float len = d.magnitude;
            var cam = Camera.main;
            bool fromEye = cam != null && (a - cam.transform.position).sqrMagnitude < 0.04f * 0.04f;   // desktop fallback: a tube out of your own eye is just a smear
            tube.gameObject.SetActive(len >= 1e-4f && !fromEye);
            if (len >= 1e-4f) tube.SetPositionAndRotation((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len));
            tube.localScale = new Vector3(radius, len, radius);
            var n = normal ?? (len >= 1e-4f ? -d / len : Vector3.up);
            var rot = Quaternion.FromToRotation(Vector3.up, n);
            dot.SetPositionAndRotation(b + n * 0.0008f, rot);
            core.SetPositionAndRotation(b + n * 0.0012f, rot);
        }
    }
}
