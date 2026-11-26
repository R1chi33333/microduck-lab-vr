using MicroduckVR.Comic;
using MicroduckVR.Duck;
using MicroduckVR.Player;
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
        ComicBeam beam;
        /// <summary>Filming: only the dot, not the line from the hand (on film it reads as a pole standing on the floor).</summary>
        public static bool HideBeam;
        float t;

        void Start()
        {
            if (cam == null) cam = Camera.main;
            holdA = new InputAction("Laser", InputActionType.Value, expectedControlType: "Axis");
            holdA.AddBinding(InputPaths.RightGrip); holdA.AddBinding(InputPaths.KeyL);
            holdA.Enable();
            beam = new ComicBeam("Laser", "#FF5D73", 0.0028f, 0.016f);
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
            beam.Show(Active && !HideBeam);
            if (!Active) return;
            Point = hit.point;
            beam.Set(from, hit.point, hit.normal);
        }
    }
}
