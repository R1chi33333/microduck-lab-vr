namespace MicroduckVR.World
{
    /// <summary>Surface types of the house (SURFACE_TYPES in house.js). Wood is the default: no tag, or a name it does not know.</summary>
    public enum SurfaceType { Wood, Rug, Card, Table, Tile, Grass, Stone, Bed, Water }

    /// <summary>The surface type names as SurfaceTag.type stores them in scenes and prefabs, and the conversions.
    /// Needs no other game script (it travels with SurfaceTag in the asset package).</summary>
    public static class SurfaceTypes
    {
        public const string Wood = "wood", Rug = "rug", Card = "card", Table = "table", Tile = "tile",
            Grass = "grass", Stone = "stone", Bed = "bed", Water = "water";

        /// <summary>The type for a stored name; unknown names and null are wood.</summary>
        public static SurfaceType Parse(string name)
        {
            switch (name)
            {
                case Rug: return SurfaceType.Rug;
                case Card: return SurfaceType.Card;
                case Table: return SurfaceType.Table;
                case Tile: return SurfaceType.Tile;
                case Grass: return SurfaceType.Grass;
                case Stone: return SurfaceType.Stone;
                case Bed: return SurfaceType.Bed;
                case Water: return SurfaceType.Water;
                default: return SurfaceType.Wood;
            }
        }

        /// <summary>The stored name of a type.</summary>
        public static string Name(SurfaceType type)
        {
            switch (type)
            {
                case SurfaceType.Rug: return Rug;
                case SurfaceType.Card: return Card;
                case SurfaceType.Table: return Table;
                case SurfaceType.Tile: return Tile;
                case SurfaceType.Grass: return Grass;
                case SurfaceType.Stone: return Stone;
                case SurfaceType.Bed: return Bed;
                case SurfaceType.Water: return Water;
                default: return Wood;
            }
        }
    }
}
