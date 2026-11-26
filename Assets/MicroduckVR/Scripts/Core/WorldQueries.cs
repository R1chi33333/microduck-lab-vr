using UnityEngine;

namespace MicroduckVR.Core
{
    /// <summary>World queries shared by all ducks (raycasts against the World layer).</summary>
    public static class WorldQueries
    {
        /// <summary>Raycast mask of the World layer.</summary>
        public static readonly int WorldMask = 1 << Layers.World;

        /// <summary>Highest walkable surface at (x, z) at or below about 10 cm above the probe height.</summary>
        public static float GroundAt(float x, float z) => GroundAt(x, z, 3f);

        /// <summary>Highest walkable surface at (x, z), probing down from fromY; 0 when there is none.</summary>
        public static float GroundAt(float x, float z, float fromY)
        {
            if (Physics.Raycast(new Vector3(x, fromY, z), Vector3.down, out var hit, fromY + 5f, WorldMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return 0f;
        }

        /// <summary>The ground just under a height y: probes from 20 cm above it, so steps up are found too.</summary>
        public static float GroundNear(float x, float z, float y) => GroundAt(x, z, y + 0.2f);
    }
}
