using MicroduckVR.Comic;
using MicroduckVR.Core;
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
        public string skin = SkinIds.Cream;             // cream | graphite | lavender | sky
        public bool hideHeadFromPlayer;            // the player's own duck: everything but the legs goes to the PlayerHead layer

        public DuckBody Body { get; private set; }
        public CharacterController Mover { get; private set; }

        public const int LayerWorld = Layers.World, LayerDuck = Layers.Duck, LayerToy = Layers.Toy, LayerPlayerHead = Layers.PlayerHead, LayerProp = Layers.Prop, LayerCeiling = Layers.Ceiling;

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
            m.AddComponent<MicroduckVR.World.PropPusher>();              // walking into loose toys nudges them along
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
                foreach (var legRoot in new[] { JointNames.LeftLegRoot, JointNames.RightLegRoot })
                    if (Body.Nodes.TryGetValue(legRoot, out var lr)) SetLayer(lr, LayerDuck);
            }
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
