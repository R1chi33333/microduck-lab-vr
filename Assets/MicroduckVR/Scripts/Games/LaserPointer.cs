using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Player;
using MicroduckVR.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MicroduckVR.Games
{
    /// <summary>
    /// The cat-toy laser: hold the right grip (or L) and a red dot appears where the right controller points.
    /// Ducks nearby gather round it and pounce on it (DuckBrain reads Active / Point).
    /// </summary>
    public class LaserPointer : MonoBehaviour
    {
        public Camera cam;
        public Transform rightHand;
        public static bool Active { get; private set; }
        public static Vector3 Point { get; private set; }
        public static Vector3? DebugPoint;                 // editor tests

        InputAction holdA;
        LineRenderer beam; Transform dot, halo;
        float t;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            holdA = new InputAction("Laser", InputActionType.Value, expectedControlType: "Axis");
            holdA.AddBinding("<XRController>{RightHand}/grip"); holdA.AddBinding("<Keyboard>/l");
            holdA.Enable();
            var red = ComicLook.ToonMaterial(ComicLook.Hex("#FF2A2A"), "laser_red"); red.SetFloat("_Unlit", 1f);
            var pink = ComicLook.ToonMaterial(ComicLook.Hex("#FFB3B3"), "laser_core"); pink.SetFloat("_Unlit", 1f);
            beam = new GameObject("Laser beam").AddComponent<LineRenderer>();
            beam.positionCount = 2; beam.startWidth = 0.0012f; beam.endWidth = 0.0006f; beam.sharedMaterial = red;
            beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beam.enabled = false;
            dot = Disc("Laser dot", red, 0.011f); halo = Disc("Laser core", pink, 0.005f); halo.SetParent(dot, true);
            dot.gameObject.SetActive(false);
        }

        Transform Disc(string name, Material m, float r)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = ComicMesh.Leg(0.0008f, r, r, 20);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void OnDestroy() { holdA?.Disable(); Active = false; }

        void Update()
        {
            t += Time.deltaTime;
            bool on = (holdA.ReadValue<float>() > 0.5f && !BagMenu.Busy) || DebugPoint.HasValue;
            Vector3 from = rightHand != null && rightHand.localPosition.sqrMagnitude > 1e-6f ? rightHand.position : cam.transform.position - cam.transform.up * 0.02f;
            Vector3 dir = rightHand != null && rightHand.localPosition.sqrMagnitude > 1e-6f ? rightHand.forward : cam.transform.forward;
            bool hitOk = false; RaycastHit hit = default;
            if (DebugPoint.HasValue) { hitOk = Physics.Raycast(DebugPoint.Value + Vector3.up * 0.3f, Vector3.down, out hit, 1f, WorldQueries.WorldMask); from = DebugPoint.Value + Vector3.up * 0.3f; }
            else if (on) hitOk = Physics.Raycast(from, dir, out hit, 6f, WorldQueries.WorldMask | (1 << DuckSetup.LayerProp), QueryTriggerInteraction.Ignore);
            Active = on && hitOk;
            beam.enabled = Active; dot.gameObject.SetActive(Active);
            if (!Active) return;
            Point = hit.point;
            dot.position = hit.point + hit.normal * 0.0015f;
            dot.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            float s = 1f + 0.15f * Mathf.Sin(t * 30f);
            dot.localScale = new Vector3(s, 1f, s);
            beam.SetPosition(0, from); beam.SetPosition(1, hit.point);
        }
    }
}
