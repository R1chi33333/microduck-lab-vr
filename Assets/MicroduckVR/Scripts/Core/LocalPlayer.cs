using System;
using UnityEngine;

namespace MicroduckVR.Core
{
    /// <summary>What lower layers (comic effects, the lawn) need to know about the player's own duck.</summary>
    public interface ILocalPlayer
    {
        /// <summary>The duck's body is built.</summary>
        bool HasBody { get; }
        /// <summary>Where the duck's body is (only when HasBody).</summary>
        Vector3 BodyPos { get; }
        /// <summary>The player's camera (can be null).</summary>
        Camera Cam { get; }
        /// <summary>The view is behind the duck instead of in its eyes.</summary>
        bool ThirdPerson { get; }
    }

    /// <summary>The player's own duck for code below the player layer; the player fills Resolve when it loads.</summary>
    public static class LocalPlayer
    {
        public static Func<ILocalPlayer> Resolve;

        /// <summary>The player's duck, or null when there is none.</summary>
        public static ILocalPlayer Current => Resolve != null ? Resolve() : null;
    }
}
