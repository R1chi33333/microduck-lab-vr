namespace MicroduckVR.Net
{
    /// <summary>Message kinds (the first byte of every message).</summary>
    public enum NetMsg : byte
    {
        Hello = 1, Welcome, Custom, Quack, Emote, Fall, Word, Pow, Splash, Bye,
        FlockRoster = 20, FlockAdd, FlockRemove, FlockMeta, FlockEvent, FlockEmote, AddDuckRequest, RemoveDuckRequest,
        PropMap = 30, ToySpawn, ToyGone, ToyRequest, TrashRequest, Grab, Drop, GrabDenied, Push,
        PlayerState = 50, FlockState, PropState,
    }
}
