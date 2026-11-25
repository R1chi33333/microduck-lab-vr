using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>Surface type of a collider (SURFACE_TYPES in house.js): wood, rug, card, table, tile, grass, stone, bed, water.</summary>
    public class SurfaceTag : MonoBehaviour
    {
        public string type = "wood";
        public string room = "living";

        public float Friction => type switch { "tile" => 0.15f, "water" => 0.25f, "table" => 0.65f, "wood" => 0.7f, "card" => 0.8f, "stone" => 0.8f, "grass" => 0.9f, "bed" => 0.9f, "rug" => 1f, _ => 0.7f };
        public float Bounce => type == "bed" ? 0.65f : 0f;

        /// <summary>The surface under a point (raycast down from just above it), or null.</summary>
        public static SurfaceTag At(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out var hit, 0.3f, WorldQueries.WorldMask, QueryTriggerInteraction.Collide))
                return hit.collider.GetComponentInParent<SurfaceTag>();
            return null;
        }
    }
}
