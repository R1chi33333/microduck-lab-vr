using System;
using System.Collections.Generic;
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

    /// <summary>A short history of timestamped samples of one thing, blended at the playback time (no guessing ahead).</summary>
    public class Track<T> where T : struct
    {
        readonly List<double> times = new List<double>(24);
        readonly List<T> values = new List<T>(24);
        readonly Func<T, T, float, T> lerp;
        const int Keep = 24;

        public Track(Func<T, T, float, T> lerp) { this.lerp = lerp; }

        public int Count => times.Count;
        public void Clear() { times.Clear(); values.Clear(); }

        public void Add(double t, T v)
        {
            int n = times.Count;
            if (n > 0)
            {
                double last = times[n - 1];
                if (t < last) return;                                              // stale
                if (t == last) { values[n - 1] = v; return; }
                // after a quiet spell (a toy at rest, nothing sent) hold the old value until just before this one,
                // so the motion starts now instead of being smeared over the whole quiet spell
                if (t - last > 0.25) { times.Add(t - 0.05); values.Add(values[n - 1]); }
            }
            times.Add(t); values.Add(v);
            while (times.Count > Keep) { times.RemoveAt(0); values.RemoveAt(0); }
        }

        public bool Sample(double t, out T v)
        {
            int n = times.Count;
            if (n == 0) { v = default; return false; }
            if (t <= times[0]) { v = values[0]; return true; }
            if (t >= times[n - 1]) { v = values[n - 1]; return true; }
            for (int i = n - 2; i >= 0; i--)
            {
                if (times[i] <= t)
                {
                    float u = (float)((t - times[i]) / Math.Max(1e-6, times[i + 1] - times[i]));
                    v = lerp(values[i], values[i + 1], u);
                    return true;
                }
            }
            v = values[0]; return true;
        }

        public bool Latest(out T v)
        {
            int n = times.Count;
            if (n == 0) { v = default; return false; }
            v = values[n - 1]; return true;
        }
    }

    /// <summary>Position and rotation of a toy.</summary>
    public struct NetPose
    {
        public Vector3 pos; public Quaternion rot;
        public static NetPose Lerp(NetPose a, NetPose b, float u) => new NetPose { pos = Vector3.Lerp(a.pos, b.pos, u), rot = Quaternion.Slerp(a.rot, b.rot, u) };
    }

    static class NetMath
    {
        public static float Wrap(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        public static float LerpYaw(float a, float b, float u) => a + Wrap(b - a) * u;
    }
}
