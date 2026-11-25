using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>Drives the vignette quad in front of the XR camera: eases towards Target, with short pulses on snap turns.</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ComfortVignette : MonoBehaviour
    {
        public float Target;
        public float riseRate = 4f, fallRate = 2f;
        float current, pulse;
        Material mat;
        static readonly int StrengthId = Shader.PropertyToID("_Strength");

        void Awake() { mat = GetComponent<MeshRenderer>().material; }

        public void Pulse(float amount) { pulse = Mathf.Max(pulse, amount); }

        void LateUpdate()
        {
            float goal = Mathf.Max(Target, pulse);
            float rate = goal > current ? riseRate : fallRate;
            current = Mathf.MoveTowards(current, goal, rate * Time.deltaTime);
            pulse = Mathf.MoveTowards(pulse, 0f, 2.5f * Time.deltaTime);
            mat.SetFloat(StrengthId, current);
            GetComponent<MeshRenderer>().enabled = current > 0.005f;
        }
    }
}
