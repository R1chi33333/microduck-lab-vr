using System;

namespace MicroduckVR.Duck
{
    /// <summary>A component on a duck that reports what happens to it (the flock's brain does): the duck's sounds listen.</summary>
    public interface IDuckEventSource
    {
        /// <summary>(kind, context), raised right after DuckBrain.OnEvent.</summary>
        event Action<DuckEventKind, string> DuckEvent;
        /// <summary>The duck has a mind of its own (it chirps now and then by itself).</summary>
        bool IsBrain { get; }
    }
}
