namespace MicroduckVR.Duck
{
    /// <summary>Gait tuning, same numbers as the web version (metres, seconds, radians).</summary>
    public static class Gait
    {
        /// <summary>Top walking speed (m/s).</summary>
        public const float MaxSpeed = 0.16f;
        /// <summary>Top turn rate (rad/s).</summary>
        public const float MaxYawRate = 2.2f;
        /// <summary>Length of a foot swing, as a share of the gait cycle.</summary>
        public const float Swing = 0.4f;
        /// <summary>How high a foot lifts in a step (m).</summary>
        public const float Lift = 0.016f;
        /// <summary>Sideways distance of each foot from the centre (m).</summary>
        public const float FootX = 0.045f;
        /// <summary>Pigeon-toed inward turn of the feet (rad).</summary>
        public const float ToeIn = 0.11f;
        /// <summary>How far the body dips while walking (m).</summary>
        public const float Crouch = 0.006f;
    }
}
