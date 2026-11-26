namespace MicroduckVR.Player
{
    /// <summary>
    /// Ids of the toy bag's action tiles and of the bin. Strings, not an enum: they share one key space with the toy
    /// kinds (a tile is either), and BagMenu.Choose(string) takes them from the Editor checks and the film tools.
    /// </summary>
    public static class BagActions
    {
        public const string Football = "football", Race = "race", Swap = "swap", View = "view";
        public const string Dance = "dance", Nod = "nod", Shake = "shake", Look = "look";
        public const string AddDuck = "addduck", RemoveDuck = "removeduck", Mirror = "mirror", Tutorial = "tutorial";
        public const string Help = "help", Credits = "credits", Host = "host", Join = "join";
        /// <summary>The bin tile: remove toys.</summary>
        public const string Trash = "trash";
    }
}
