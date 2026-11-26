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
        /// <summary>The imported duck rig (Models/microduck_rig.glb).</summary>
        public GameObject rigPrefab;
        /// <summary>The rig's hinge data.</summary>
        public DuckJointMap jointMap;
        /// <summary>Colourway: cream, graphite, lavender or sky (SkinIds).</summary>
        public string skin = SkinIds.Cream;
        /// <summary>The player's own duck: everything but the legs goes to the PlayerHead layer.</summary>
        public bool hideHeadFromPlayer;

        /// <summary>The duck's body, made in Awake.</summary>
        public DuckBody Body { get; private set; }
        /// <summary>The capsule that moves the duck.</summary>
        public CharacterController Mover { get; private set; }

        void Awake()
        {
            Body = GetComponent<DuckBody>();
            var start = transform.position; var yaw = transform.eulerAngles.y * Mathf.Deg2Rad;
            var rig = Instantiate(rigPrefab);
            rig.name = "rig";
            SetLayer(rig.transform, Layers.Duck);
            float probeY = start.y;
            Body.Ground = (x, z) => WorldQueries.GroundNear(x, z, probeY);   // surface near the duck's height (not the table above it)
            Body.Init(rig, jointMap);
            ComicLook.ApplyToDuck(rig.transform, skin);

            // the capsule that actually moves through the world; the body follows it
            var m = new GameObject(name + " mover");
            m.layer = Layers.Duck;
            m.transform.position = start;
            Mover = m.AddComponent<CharacterController>();
            Mover.radius = 0.05f; Mover.height = 0.2f; Mover.center = new Vector3(0f, 0.105f, 0f);
            m.AddComponent<PropPusher>();                                // walking into loose toys nudges them along
            Mover.stepOffset = 0.03f; Mover.slopeLimit = 50f; Mover.skinWidth = 0.004f; Mover.minMoveDistance = 0f;
            Body.Teleport(new Vector3(start.x, WorldQueries.GroundNear(start.x, start.z, start.y), start.z), yaw);
            // feet and body look for the surface just above where the capsule actually stands (it follows ramps
            // and slopes exactly); probing from the body height could start inside a ramp and miss it
            var mv = Mover.transform;
            Body.Ground = (x, z) => WorldQueries.GroundNear(x, z, mv.position.y);

            if (hideHeadFromPlayer)
            {
                // the player sees out of this duck: hide its shell, electronics and head, keep the legs and feet
                SetLayer(rig.transform, Layers.PlayerHead);
                foreach (var legRoot in new[] { JointNames.LeftLegRoot, JointNames.RightLegRoot })
                    if (Body.Nodes.TryGetValue(legRoot, out var lr)) SetLayer(lr, Layers.Duck);
            }
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
