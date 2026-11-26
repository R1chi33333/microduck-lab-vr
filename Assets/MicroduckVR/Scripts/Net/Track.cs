using System;
using System.Collections.Generic;

namespace MicroduckVR.Net
{
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
}
