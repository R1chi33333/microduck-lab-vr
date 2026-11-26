namespace MicroduckVR.Duck
{
    /// <summary>Things that happen to an AI duck (DuckBrain.OnEvent): it got or dropped a toy, or stepped onto the bed,
    /// into water or onto tiles. Sent over the network as a byte, so keep the values.</summary>
    public enum DuckEventKind : byte { Got, Drop, Bed, Water, Tile }
}
