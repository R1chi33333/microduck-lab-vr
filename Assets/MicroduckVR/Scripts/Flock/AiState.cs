namespace MicroduckVR.Flock
{
    /// <summary>What an AI duck is doing (DuckBrain.State). Sent over the network as a byte, so keep the values.</summary>
    public enum AiState : byte { Idle, Follow, Roam, Wander, Toy, Sit, Race, Football, Laser }

    /// <summary>The state names as the web version and the logs write them.</summary>
    public static class AiStates
    {
        /// <summary>The state's name ("idle", "follow", ...).</summary>
        public static string Name(AiState state)
        {
            switch (state)
            {
                case AiState.Idle: return "idle";
                case AiState.Follow: return "follow";
                case AiState.Roam: return "roam";
                case AiState.Wander: return "wander";
                case AiState.Toy: return "toy";
                case AiState.Sit: return "sit";
                case AiState.Race: return "race";
                case AiState.Football: return "football";
                case AiState.Laser: return "laser";
                default: return "";
            }
        }
    }
}
