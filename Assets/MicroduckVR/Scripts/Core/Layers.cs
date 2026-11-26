namespace MicroduckVR.Core
{
    /// <summary>The game's layers (ProjectSettings/TagManager; SceneBuilderM1.EnsureLayers names them).</summary>
    public static class Layers
    {
        public const int World = 6, Duck = 7, Toy = 8, PlayerHead = 9, Prop = 10, Ceiling = 11;

        /// <summary>Raycast mask of the World layer.</summary>
        public const int WorldMask = 1 << World;
    }
}
