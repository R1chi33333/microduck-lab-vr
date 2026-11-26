using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>Surface type of a collider (SURFACE_TYPES in house.js): wood, rug, card, table, tile, grass, stone, bed, water.
    /// Needs no other game script, so props carrying it can be shared on their own (Microduck/Assets/Export Package).</summary>
    public class SurfaceTag : MonoBehaviour
    {
        const int WorldMask = 1 << 6;        // the World layer (Layers.World)

        /// <summary>The surface type name stored in scenes and prefabs (see SurfaceTypes).</summary>
        public string type = "wood";
        /// <summary>The room id the surface belongs to.</summary>
        public string room = "living";

        [System.NonSerialized] string parsedType;
        [System.NonSerialized] SurfaceType kind;

        /// <summary>'type' as an enum. Parsed once and cached; parsed again whenever 'type' is set to another string.</summary>
        public SurfaceType Kind
        {
            get
            {
                if (!ReferenceEquals(parsedType, type)) { kind = SurfaceTypes.Parse(type); parsedType = type; }
                return kind;
            }
        }

        /// <summary>Floor grip: 0.15 on tiles up to 1 on rugs.</summary>
        public float Friction => Kind switch { SurfaceType.Tile => 0.15f, SurfaceType.Water => 0.25f, SurfaceType.Table => 0.65f, SurfaceType.Wood => 0.7f, SurfaceType.Card => 0.8f, SurfaceType.Stone => 0.8f, SurfaceType.Grass => 0.9f, SurfaceType.Bed => 0.9f, SurfaceType.Rug => 1f, _ => 0.7f };
        /// <summary>How much the surface bounces (only the bed does).</summary>
        public float Bounce => Kind == SurfaceType.Bed ? 0.65f : 0f;

        /// <summary>The surface under a point (raycast down from just above it), or null.</summary>
        public static SurfaceTag At(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out var hit, 0.3f, WorldMask, QueryTriggerInteraction.Collide))
                return hit.collider.GetComponentInParent<SurfaceTag>();
            return null;
        }
    }
}
