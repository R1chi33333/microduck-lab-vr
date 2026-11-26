using System;
using UnityEngine;

namespace MicroduckVR.Net
{
    /// <summary>
    /// Playback clock for what the other PC sends: runs a little behind the newest snapshot (Delay), so there are
    /// always two snapshots to blend between. Every snapshot carries the sender's own clock; the playback time
    /// follows it smoothly and jumps only after a long gap.
    /// </summary>
    public class NetClock
    {
        /// <summary>How far playback runs behind the newest snapshot (s).</summary>
        public const double Delay = 0.1;
        double latest = double.NegativeInfinity, render;
        bool started;

        /// <summary>A snapshot has arrived since the last reset.</summary>
        public bool Started => started;
        /// <summary>The playback time, in the sender's clock.</summary>
        public double Now => render;

        /// <summary>A snapshot stamped with the sender's time has arrived.</summary>
        public void Observe(double senderTime)
        {
            if (senderTime > latest) latest = senderTime;
            if (!started) { render = latest - Delay; started = true; }
        }

        /// <summary>Moves playback on by a frame, easing towards the newest time minus Delay.</summary>
        public void Advance(float dt)
        {
            if (!started) return;
            render += dt;
            double target = latest - Delay, err = target - render;
            if (Math.Abs(err) > 0.5) render = target;                              // a long hiccup: catch up at once
            else render += err * Mathf.Clamp01(dt * 3f);                          // otherwise drift gently onto it
        }

        /// <summary>Forgets everything (a new session).</summary>
        public void Reset() { started = false; latest = double.NegativeInfinity; }
    }
}
