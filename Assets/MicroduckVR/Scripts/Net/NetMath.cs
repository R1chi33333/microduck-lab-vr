using UnityEngine;

namespace MicroduckVR.Net
{
    static class NetMath
    {
        /// <summary>An angle wrapped into -pi..pi.</summary>
        public static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        /// <summary>Blends two yaws the short way round.</summary>
        public static float LerpYaw(float a, float b, float u) => a + Wrap(b - a) * u;
    }
}
