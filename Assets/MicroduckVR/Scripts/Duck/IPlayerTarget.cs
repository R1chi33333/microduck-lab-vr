using UnityEngine;

namespace MicroduckVR.Duck
{
    /// <summary>Another player's duck in this house, as the flock sees it (PlayerTargets.Remote).</summary>
    public interface IPlayerTarget
    {
        /// <summary>The duck's body (null while it is not built).</summary>
        DuckBody Body { get; }
        /// <summary>That player's laser pointer is on.</summary>
        bool LaserOn { get; }
        /// <summary>Where that player's laser dot is.</summary>
        Vector3 LaserPoint { get; }
    }
}
