using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>
    /// The skates' feel (the web's SKATE constants, duck-scale metres and seconds). The defaults are the web numbers;
    /// PlayerSkates uses them as they are unless a tuning asset is given to it or found in Resources.
    /// </summary>
    [CreateAssetMenu(fileName = "SkateTuning", menuName = "Microduck/Skate Tuning")]
    public class SkateTuning : ScriptableObject
    {
        /// <summary>Resources name PlayerSkates looks for when it is given no tuning.</summary>
        public const string ResourceName = "SkateTuning";

        /// <summary>Push acceleration (m/s²).</summary>
        [Header("Rolling")]
        public float accel = 0.42f;
        /// <summary>Top speed before the comfort cap (m/s).</summary>
        public float maxSpeed = 0.42f;
        /// <summary>Rolling friction.</summary>
        public float friction = 0.32f;
        /// <summary>Braking strength (stick pulled back).</summary>
        public float brake = 0.9f;
        /// <summary>How fast the velocity follows the heading.</summary>
        public float grip = 2.4f;
        /// <summary>Turn rate (rad/s).</summary>
        public float turn = 2.0f;
        /// <summary>Push strokes per second.</summary>
        public float strokeHz = 1.25f;

        /// <summary>Top speed multiplier while boosting.</summary>
        [Header("Boost")]
        public float boostMult = 1.5f;
        /// <summary>Boost length (s).</summary>
        public float boostTime = 1.2f;
        /// <summary>Wait after a boost (s).</summary>
        public float boostCooldown = 2.0f;
        /// <summary>Speed added when a boost starts (m/s).</summary>
        public float boostKick = 0.22f;
        /// <summary>Acceleration while boosting (m/s²).</summary>
        public float boostAccel = 1.1f;

        /// <summary>Grip while drifting.</summary>
        [Header("Drift and slingshot")]
        public float driftGrip = 0.45f;
        /// <summary>Turn rate multiplier while drifting.</summary>
        public float driftTurnMult = 1.7f;
        /// <summary>Heading kick when a drift starts (rad).</summary>
        public float driftFlick = 0.22f;
        /// <summary>Slowest speed a drift can start at (m/s).</summary>
        public float driftMinSpeed = 0.1f;
        /// <summary>A drift held this long (s) ends in a slingshot.</summary>
        public float slingAfter = 0.6f;
        /// <summary>Speed added by a slingshot (m/s).</summary>
        public float slingKick = 0.16f;
        /// <summary>Slingshot boost length (s).</summary>
        public float slingTime = 0.5f;

        /// <summary>Hitting a wall faster than this knocks the duck over (m/s).</summary>
        [Header("Crash")]
        public float crashSpeed = 0.5f;

        static SkateTuning defaults;

        /// <summary>An in-memory tuning with the default (web) numbers, made on first use.</summary>
        public static SkateTuning Defaults
        {
            get
            {
                if (defaults == null) { defaults = CreateInstance<SkateTuning>(); defaults.name = "SkateTuning (defaults)"; defaults.hideFlags = HideFlags.HideAndDontSave; }
                return defaults;
            }
        }
    }
}
