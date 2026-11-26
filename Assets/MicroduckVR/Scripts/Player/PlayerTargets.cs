using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.Games;
using UnityEngine;

namespace MicroduckVR.Player
{
    /// <summary>
    /// Every player duck the flock should care about: your own duck (PlayerDuck.Instance) first, then the other
    /// player's duck when you play together. The ducks follow, keep clear of and chase the laser dots of all of them.
    /// With nobody connected this is exactly the single player: index 0 is your own duck.
    /// </summary>
    public static class PlayerTargets
    {
        /// <summary>The other players' ducks in this house (filled by the network session).</summary>
        public static readonly List<IPlayerTarget> Remote = new List<IPlayerTarget>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Remote.Clear(); }

        static bool HasLocal => PlayerDuck.Instance != null && PlayerDuck.Instance.Body != null;

        /// <summary>The null test the entries had as RemotePlayer: a destroyed component counts as null.</summary>
        static bool Live(IPlayerTarget r) => r is Object o ? o != null : r != null;

        public static int Count
        {
            get
            {
                int n = HasLocal ? 1 : 0;
                foreach (var r in Remote) if (Live(r) && r.Body != null) n++;
                return n;
            }
        }

        /// <summary>Player duck number i (0 = your own when you have one).</summary>
        public static DuckBody Get(int i)
        {
            if (HasLocal) { if (i == 0) return PlayerDuck.Instance.Body; i--; }
            foreach (var r in Remote)
            {
                if (!Live(r) || r.Body == null) continue;
                if (i == 0) return r.Body;
                i--;
            }
            return null;
        }

        /// <summary>The player a flock duck keeps company with: the flock shares itself out between the players.</summary>
        public static DuckBody ForDuck(int duckIndex)
        {
            int n = Count;
            if (n == 0) return null;
            return Get(((duckIndex % n) + n) % n);
        }

        /// <summary>Within range of any other player's duck (things that happen near them count too).</summary>
        public static bool NearRemote(Vector3 p, float range)
        {
            foreach (var r in Remote)
                if (Live(r) && r.Body != null && (r.Body.Pos - p).sqrMagnitude < range * range) return true;
            return false;
        }

        /// <summary>The laser dot (yours or another player's) nearest to a point on the floor plan.</summary>
        public static bool NearestLaser(Vector3 from, out Vector3 point)
        {
            point = default; bool found = false; float best = float.MaxValue;
            if (LaserPointer.Active) { point = LaserPointer.Point; best = Flat(point - from); found = true; }
            foreach (var r in Remote)
            {
                if (!Live(r) || !r.LaserOn) continue;
                float d = Flat(r.LaserPoint - from);
                if (d < best) { best = d; point = r.LaserPoint; found = true; }
            }
            return found;
        }

        static float Flat(Vector3 v) => v.x * v.x + v.z * v.z;
    }
}
