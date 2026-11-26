using UnityEngine;

namespace MicroduckVR.Duck
{
    public partial class DuckBody
    {
        // ------------------------------------------------------------------ emotes
        public void PlayEmote(string kind, float dur = 0f)
        {
            if (dur <= 0f) dur = kind switch { "nod" => 1.1f, "shake" => 1.1f, "look" => 2.6f, "dance" => 2.4f, _ => 1.2f };
            CurrentEmote = new Emote { kind = kind, t = 0f, dur = dur };
            OnEmote?.Invoke(kind, dur);
        }

        // returns offsets already mirrored to Unity (x, yaw, roll negated vs the web formulas)
        (float x, float y, float pitch, float yaw, float roll) EmoteOffsets(float dt)
        {
            float ox = 0, oy = 0, opitch = 0, oyaw = 0, oroll = 0, gy = 0, gp = 0;
            var E = CurrentEmote;
            if (E != null)
            {
                E.t += dt; float t = E.t, u = Mathf.Min(1f, t / E.dur), env = Mathf.Sin(Mathf.PI * u);
                switch (E.kind)
                {
                    case "nod": gp = Mathf.Sin(t * 11f) * 0.38f * env; break;
                    case "shake": gy = Mathf.Sin(t * 13f) * 0.55f * env; break;
                    case "look": gy = Mathf.Sin(t * 2.2f) * 0.95f * env; gp = -0.18f * env + Mathf.Sin(t * 1.3f) * 0.1f * env; break;
                    case "dance":
                        oroll = -Mathf.Sin(t * 9f) * 0.14f * env; ox = -Mathf.Sin(t * 9f) * 0.006f * env; oy = -Mathf.Abs(Mathf.Sin(t * 9f)) * 0.008f * env;
                        oyaw = -Mathf.Sin(t * 4.5f) * 0.2f * env; gy = -Mathf.Sin(t * 4.5f + 1f) * 0.4f * env; HappyWiggle = Mathf.Max(HappyWiggle, env);
                        break;
                }
                if (u >= 1f) CurrentEmote = null;
            }
            GazeYawOff = Damp(GazeYawOff, gy, 18f, dt); GazePitchOff = Damp(GazePitchOff, gp, 18f, dt);
            return (ox, oy, opitch, oyaw, oroll);
        }
    }
}
