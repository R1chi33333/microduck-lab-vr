using UnityEngine;

namespace MicroduckVR.Net
{
    static class NetMath
    {
        public static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        public static float LerpYaw(float a, float b, float u) => a + Wrap(b - a) * u;
    }
}
