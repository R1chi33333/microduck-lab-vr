using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>Drives the vignette quad in front of the XR camera: eases towards Target, with short pulses on snap turns.
    /// It also carries the comic speed lines (static ink wedges round the edge of the view, fading in with SpeedLines).</summary>
    [RequireComponent(typeof(MeshRenderer))]
    public class ComfortVignette : MonoBehaviour
    {
        /// <summary>Comic speed lines round the edge of the view when skating fast (a comfort option: off shows none).</summary>
        public static bool SpeedLinesOn = true;

        /// <summary>How strong the vignette should be (0..1), set by the player each frame.</summary>
        public float Target;
        /// <summary>Speed lines, 0..1, set by the player each frame.</summary>
        public float SpeedLines;
        /// <summary>How fast the vignette tightens and eases off again (per second).</summary>
        public float riseRate = 4f, fallRate = 2f;
        float current, pulse, lines;
        Material mat; MeshRenderer mr;
        static readonly int StrengthId = Shader.PropertyToID("_Strength"), LinesId = Shader.PropertyToID("_SpeedLines");

        void Awake() { mr = GetComponent<MeshRenderer>(); mat = mr.material; }

        /// <summary>A short squeeze of the vignette (a bump, a fall).</summary>
        public void Pulse(float amount) { pulse = Mathf.Max(pulse, amount); }

        void LateUpdate()
        {
            float goal = Mathf.Max(Target, pulse);
            float rate = goal > current ? riseRate : fallRate;
            current = Mathf.MoveTowards(current, goal, rate * Time.deltaTime);
            pulse = Mathf.MoveTowards(pulse, 0f, 2.5f * Time.deltaTime);
            float lineGoal = SpeedLinesOn ? Mathf.Clamp01(SpeedLines) : 0f;
            lines = Mathf.MoveTowards(lines, lineGoal, (lineGoal > lines ? 3f : 2f) * Time.deltaTime);
            mat.SetFloat(StrengthId, current);
            mat.SetFloat(LinesId, lines);
            mr.enabled = current > 0.005f || lines > 0.005f;
        }
    }
}
