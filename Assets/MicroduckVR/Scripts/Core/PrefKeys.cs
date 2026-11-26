namespace MicroduckVR.Core
{
    /// <summary>PlayerPrefs keys. Never change these strings: players would lose what was saved under them.</summary>
    public static class PrefKeys
    {
        /// <summary>The customised duck (JSON of DuckCustom).</summary>
        public const string Custom = "microduck_custom_v1";
        /// <summary>Garage: the odometer reading when a part was replaced (the part id follows).</summary>
        public const string PartPrefix = "microduck_part_";
        /// <summary>Garage: how far the player's duck has walked.</summary>
        public const string Odometer = "microduck_odometer";
        /// <summary>The tutorial was played to the end.</summary>
        public const string TutorialDone = "microduck_tutorial_done_v2";
    }
}
