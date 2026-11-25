using MicroduckVR.Comic;
using UnityEngine;

namespace MicroduckVR.Duck
{
    /// <summary>Builds the duck at runtime: instantiates the rig under a DuckBody and adds a mover capsule.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(DuckBody))]
    public class DuckSetup : MonoBehaviour
    {
        public GameObject rigPrefab;
        public DuckJointMap jointMap;
        public string skin = "cream";             // cream | graphite | lavender | sky
        public bool hideHeadFromPlayer;            // the player's own duck: everything but the legs goes to the PlayerHead layer

        public DuckBody Body { get; private set; }
        public CharacterController Mover { get; private set; }

        public const int LayerWorld = 6, LayerDuck = 7, LayerToy = 8, LayerPlayerHead = 9;

        void Awake()
        {
            Body = GetComponent<DuckBody>();
            var start = transform.position; var yaw = transform.eulerAngles.y * Mathf.Deg2Rad;
            var rig = Instantiate(rigPrefab);
            rig.name = "rig";
            SetLayer(rig.transform, LayerDuck);
            float probeY = start.y;
            Body.Ground = (x, z) => WorldQueries.GroundNear(x, z, probeY);   // surface near the duck's height (not the table above it)
            Body.Init(rig, jointMap);
            ComicLook.ApplyToDuck(rig.transform, skin);

            // the capsule that actually moves through the world; the body follows it
            var m = new GameObject(name + " mover");
            m.layer = LayerDuck;
            m.transform.position = start;
            Mover = m.AddComponent<CharacterController>();
            Mover.radius = 0.05f; Mover.height = 0.2f; Mover.center = new Vector3(0f, 0.105f, 0f);
            Mover.stepOffset = 0.03f; Mover.slopeLimit = 50f; Mover.skinWidth = 0.004f; Mover.minMoveDistance = 0f;
            Body.Teleport(new Vector3(start.x, WorldQueries.GroundNear(start.x, start.z, start.y), start.z), yaw);
            // feet and body look for the surface just above where the capsule actually stands (it follows ramps
            // and slopes exactly); probing from the body height could start inside a ramp and miss it
            var mv = Mover.transform;
            Body.Ground = (x, z) => WorldQueries.GroundNear(x, z, mv.position.y);

            if (hideHeadFromPlayer)
            {
                // the player sees out of this duck: hide its shell, electronics and head, keep the legs and feet
                SetLayer(rig.transform, LayerPlayerHead);
                foreach (var legRoot in new[] { "body_yaw2roll", "body_bearing_roll" })
                    if (Body.Nodes.TryGetValue(legRoot, out var lr)) SetLayer(lr, LayerDuck);
            }
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }

    /// <summary>World queries shared by all ducks (raycasts against the World layer).</summary>
    public static class WorldQueries
    {
        public static readonly int WorldMask = 1 << DuckSetup.LayerWorld;

        /// <summary>Highest walkable surface at (x, z) at or below about 10 cm above the probe height.</summary>
        public static float GroundAt(float x, float z) => GroundAt(x, z, 3f);

        public static float GroundAt(float x, float z, float fromY)
        {
            if (Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out var hit, fromY + 5f, WorldMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        public static float GroundNear(float x, float z, float y) => GroundAt(x, z, y + 0.2f);
    }
}
