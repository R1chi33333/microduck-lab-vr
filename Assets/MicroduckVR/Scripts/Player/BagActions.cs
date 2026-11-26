namespace MicroduckVR.Player
{
    /// <summary>
    /// Ids of the toy bag's action tiles and of the bin. Strings, not an enum: they share one key space with the toy
    /// kinds (a tile is either), and BagMenu.Choose(string) takes them from the Editor checks and the film tools.
    /// </summary>
    public static class BagActions
    {
        /// <summary>Games and the view: FOOTBALL, RACE, swap duck, first or third person.</summary>
        public const string Football = "football", Race = "race", Swap = "swap", View = "view";
        /// <summary>The four emotes.</summary>
        public const string Dance = "dance", Nod = "nod", Shake = "shake", Look = "look";
        /// <summary>Flock size, the mirror and the tutorial.</summary>
        public const string AddDuck = "addduck", RemoveDuck = "removeduck", Mirror = "mirror", Tutorial = "tutorial";
        /// <summary>Help and credits sheets, and the co-op HOST and JOIN buttons.</summary>
        public const string Help = "help", Credits = "credits", Host = "host", Join = "join";
        /// <summary>The bin tile: remove toys.</summary>
        public const string Trash = "trash";
    }
}
