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

        [Header("Rolling")]
        public float accel = 0.42f;
        public float maxSpeed = 0.42f;
        public float friction = 0.32f;
        public float brake = 0.9f;
        public float grip = 2.4f;
        public float turn = 2.0f;
        public float strokeHz = 1.25f;

        [Header("Boost")]
        public float boostMult = 1.5f;
        public float boostTime = 1.2f;
        public float boostCooldown = 2.0f;
        public float boostKick = 0.22f;
        public float boostAccel = 1.1f;

        [Header("Drift and slingshot")]
        public float driftGrip = 0.45f;
        public float driftTurnMult = 1.7f;
        public float driftFlick = 0.22f;
        public float driftMinSpeed = 0.1f;
        public float slingAfter = 0.6f;
        public float slingKick = 0.16f;
        public float slingTime = 0.5f;

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
