using System.Collections.Generic;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.World
{
    /// <summary>
    /// Rooms, doorways and spawn points of the house in Unity space (web layout with X mirrored),
    /// plus the queries the flock uses: which room a point is in, a door-to-door route between rooms
    /// (BFS, as in agent.js _route), and a random walkable spot nearby.
    /// </summary>
    public static class HouseLayout
    {
        public struct Room { public string id; public Rect rect; public bool outdoor; }   // rect: x = X, y = Z
        public struct Door { public string id; public Vector3 centre; public bool alongX; public string a, b; }

        public static readonly Room[] Rooms =
        {
            new Room { id = RoomIds.Living,  rect = Rect.MinMaxRect(-3f, -2.5f, 3f, 2.5f) },
            new Room { id = RoomIds.Kitchen, rect = Rect.MinMaxRect(-7f, -2.5f, -3.12f, 2.5f) },
            new Room { id = RoomIds.Bedroom, rect = Rect.MinMaxRect(3.12f, -2.5f, 7f, 2.5f) },
            new Room { id = RoomIds.Hallway, rect = Rect.MinMaxRect(-7f, 2.62f, 7f, 3.82f) },
            new Room { id = RoomIds.Garden,  rect = Rect.MinMaxRect(-3.5f, 3.94f, 3.5f, 7.6f), outdoor = true },
        };

        // alongX: the wall runs along X, so you cross it in Z
        public static readonly Door[] Doors =
        {
            new Door { id = "living_hall",    centre = new Vector3(0f, 0f, 2.56f),    alongX = true,  a = RoomIds.Living,  b = RoomIds.Hallway },
            new Door { id = "bedroom_hall",   centre = new Vector3(5.0f, 0f, 2.56f),  alongX = true,  a = RoomIds.Bedroom, b = RoomIds.Hallway },
            new Door { id = "kitchen_hall",   centre = new Vector3(-5.0f, 0f, 2.56f), alongX = true,  a = RoomIds.Kitchen, b = RoomIds.Hallway },
            new Door { id = "living_bed",     centre = new Vector3(3.06f, 0f, -0.8f), alongX = false, a = RoomIds.Living,  b = RoomIds.Bedroom },
            new Door { id = "living_kitchen", centre = new Vector3(-3.06f, 0f, -1.2f), alongX = false, a = RoomIds.Living, b = RoomIds.Kitchen },
            new Door { id = "patio",          centre = new Vector3(0f, 0f, 3.88f),    alongX = true,  a = RoomIds.Hallway, b = RoomIds.Garden },
        };

        public static readonly Dictionary<string, Vector3[]> Spawns = new Dictionary<string, Vector3[]>
        {
            { RoomIds.Living,  new[] { new Vector3(0.9f, 0f, 0.6f), new Vector3(1.6f, 0f, -1.4f), new Vector3(-0.4f, 0f, 1.8f), new Vector3(0.3f, 0f, -1.6f) } },
            { RoomIds.Hallway, new[] { new Vector3(5.8f, 0f, 3.22f), new Vector3(2.5f, 0f, 3.22f), new Vector3(-2.5f, 0f, 3.22f), new Vector3(-5.8f, 0f, 3.22f) } },
            { RoomIds.Kitchen, new[] { new Vector3(-3.8f, 0f, 1.6f), new Vector3(-6.2f, 0f, 1.5f), new Vector3(-4.2f, 0f, -1.0f), new Vector3(-6.3f, 0f, -0.6f) } },
            { RoomIds.Bedroom, new[] { new Vector3(4.2f, 0f, 0.4f), new Vector3(5.6f, 0f, 1.7f), new Vector3(3.8f, 0f, 1.8f), new Vector3(6.3f, 0f, -0.6f) } },
            { RoomIds.Garden,  new[] { new Vector3(0.6f, 0f, 5.2f), new Vector3(-0.8f, 0f, 6.4f), new Vector3(2.2f, 0f, 6.6f), new Vector3(2.4f, 0f, 4.6f) } },
        };

        public static readonly Dictionary<string, string> RoomName = new Dictionary<string, string>
        { { RoomIds.Living, "living room" }, { RoomIds.Kitchen, "kitchen" }, { RoomIds.Bedroom, "bedroom" }, { RoomIds.Hallway, "hallway" }, { RoomIds.Garden, "garden" } };

        public static string RoomAt(Vector3 p)
        {
            foreach (var r in Rooms) if (r.rect.Contains(new Vector2(p.x, p.z))) return r.id;
            return null;
        }

        public static Vector3 Centre(string room)
        {
            foreach (var r in Rooms) if (r.id == room) return new Vector3(r.rect.center.x, 0f, r.rect.center.y);
            return Vector3.zero;
        }

        /// <summary>Waypoints from 'from' to 'to' through doorways (35 cm either side of each door), ending at 'to'.</summary>
        public static List<Vector3> Route(Vector3 from, Vector3 to)
        {
            var pts = new List<Vector3>();
            string r0 = RoomAt(from), r1 = RoomAt(to);
            if (r0 == null || r1 == null || r0 == r1) { pts.Add(to); return pts; }
            var prev = new Dictionary<string, (string room, Door door)?> { [r0] = null };
            var q = new Queue<string>(); q.Enqueue(r0);
            while (q.Count > 0)
            {
                var r = q.Dequeue(); if (r == r1) break;
                foreach (var d in Doors)
                {
                    string n = d.a == r ? d.b : d.b == r ? d.a : null;
                    if (n == null || prev.ContainsKey(n)) continue;
                    prev[n] = (r, d); q.Enqueue(n);
                }
            }
            if (!prev.ContainsKey(r1)) { pts.Add(to); return pts; }
            var steps = new List<(string from, Door door)>();
            for (var r = r1; prev[r].HasValue; r = prev[r].Value.room) steps.Insert(0, (prev[r].Value.room, prev[r].Value.door));
            foreach (var (fromRoom, d) in steps)
            {
                Vector3 cross = d.alongX ? Vector3.forward : Vector3.right;
                Vector3 fc = Centre(fromRoom);
                float sgn = Mathf.Sign(Vector3.Dot(fc - d.centre, cross)); if (sgn == 0f) sgn = -1f;
                pts.Add(d.centre + cross * 0.35f * sgn);
                pts.Add(d.centre - cross * 0.35f * sgn);
            }
            pts.Add(to);
            return pts;
        }

        /// <summary>A random spot within radius of p that is walkable at about p's height and clear of furniture.</summary>
        public static Vector3 RandomSpot(Vector3 p, float radius, System.Random rng)
        {
            string room = RoomAt(p);
            for (int i = 0; i < 24; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = radius * Mathf.Sqrt((float)rng.NextDouble());
                var c = new Vector3(p.x + Mathf.Cos(a) * r, p.y, p.z + Mathf.Sin(a) * r);
                if (room != null && RoomAt(c) != room) continue;
                float gy = WorldQueries.GroundNear(c.x, c.z, p.y);
                if (Mathf.Abs(gy - p.y) > 0.03f) continue;                       // keep to the same level (no edges, no climbing)
                if (Physics.CheckSphere(new Vector3(c.x, gy + 0.11f, c.z), 0.07f, WorldQueries.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                c.y = gy;
                return c;
            }
            return p;
        }
    }
}
