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
        public const double Delay = 0.1;
        double latest = double.NegativeInfinity, render;
        bool started;

        public bool Started => started;
        public double Now => render;

        public void Observe(double senderTime)
        {
            if (senderTime > latest) latest = senderTime;
            if (!started) { render = latest - Delay; started = true; }
        }

        public void Advance(float dt)
        {
            if (!started) return;
            render += dt;
            double target = latest - Delay, err = target - render;
            if (Math.Abs(err) > 0.5) render = target;                              // a long hiccup: catch up at once
            else render += err * Mathf.Clamp01(dt * 3f);                          // otherwise drift gently onto it
        }

        public void Reset() { started = false; latest = double.NegativeInfinity; }
    }
}
