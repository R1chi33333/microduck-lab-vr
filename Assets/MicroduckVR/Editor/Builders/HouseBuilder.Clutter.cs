using System.Collections.Generic;
using MicroduckVR.Core;
using MicroduckVR.Duck;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// A lived-in house: lots of detailed furniture and everyday mess per room, in the hand-drawn comic style.
    /// Loose small things are physics toys (ducks push them around by walking into them); furniture and things
    /// on shelves and counters are static. Coordinates are Unity space (already mirrored), y = bottom of the object.
    /// Doorways and the main walking lanes are kept clear.
    /// </summary>
    public static partial class HouseBuilder
    {
        static System.Random crng;

        static Material C(string hex) => M("c_" + hex.TrimStart('#'), hex);
        static float Rf(float a, float b) => a + (float)crng.NextDouble() * (b - a);

        /// <summary>Hand-drawn box for furniture: (x, z) centre, y bottom, yaw in Unity degrees.</summary>
        static GameObject UB(string room, string name, float x, float y, float z, float w, float h, float d, string hex, float yaw = 0f, bool collide = false, string surface = null)
            => Box(room, name, new Vector3(x, y + h / 2f, z), new Vector3(w, h, d), C(hex), -yaw, surface, collide);

        static GameObject UC(string room, string name, float x, float y, float z, float r, float h, string hex, bool collide = false, string surface = null)
            => Cyl(room, name, r, h, C(hex), -x, y + h / 2f, z, surface, collide);

        static readonly string[] Bright = { "#FF5D73", "#FFC43D", "#3A86FF", "#1FA6A0", "#7B61FF", "#FF8A3D", "#8FD35A", "#F2F2F2" };
        static string AnyColour() => Bright[crng.Next(Bright.Length)];

        // ------------------------------------------------------------------ placement of loose toys
        static readonly List<(Vector2 p, float r)> placed = new List<(Vector2, float)>();

        /// <summary>True when a toy of radius r fits at (x, z) on the floor: clear of furniture colliders and other toys.</summary>
        static bool Free(float x, float z, float r, float y = 0f)
        {
            foreach (var (p, pr) in placed) if (Vector2.Distance(p, new Vector2(x, z)) < r + pr) return false;
            Physics.SyncTransforms();
            return !Physics.CheckSphere(new Vector3(x, y + r + 0.012f, z), r, ~(1 << Layers.Duck), QueryTriggerInteraction.Ignore);
        }
        static void Mark(float x, float z, float r) => placed.Add((new Vector2(x, z), r));

        /// <summary>Random small toys strewn over an area (blocks, bricks, crayons, cars, balls), avoiding a ring if given.</summary>
        static void ToyScatter(string room, float x0, float x1, float z0, float z1, int n, Vector3? avoidC = null, float avoidR0 = 0f, float avoidR1 = 0f)
        {
            int made = 0;
            for (int tries = 0; made < n && tries < n * 12; tries++)
            {
                float x = Rf(x0, x1), z = Rf(z0, z1), yaw = Rf(0f, 360f);
                if (avoidC.HasValue) { float d = Vector2.Distance(new Vector2(x, z), new Vector2(avoidC.Value.x, avoidC.Value.z)); if (d > avoidR0 && d < avoidR1) continue; }
                int kind = crng.Next(6);
                float r = kind == 3 ? 0.07f : kind == 4 ? 0.05f : 0.045f;
                if (!Free(x, z, r)) continue;
                var p = new Vector3(x, 0f, z);
                switch (kind)
                {
                    case 0: Block(room, p, yaw, Rf(0.045f, 0.06f), AnyColour()); break;
                    case 1: Lego(room, p, yaw, AnyColour()); break;
                    case 2: Crayon(room, p, yaw, AnyColour()); break;
                    case 3: ToyCar(room, p, yaw, AnyColour()); break;
                    case 4: StripeBall(room, p, Rf(0.055f, 0.08f), AnyColour(), "#FFFFFF"); break;
                    default: Block(room, p, yaw, Rf(0.04f, 0.05f), AnyColour()); break;
                }
                Mark(x, z, r); made++;
            }
        }

        // ------------------------------------------------------------------ rooms
        static void Clutter()
        {
            crng = new System.Random(2027);
            placed.Clear();
            bouncy = PhysMat("prop_bouncy", 0.55f, 0.45f);
            grippy = PhysMat("prop_grip", 0.05f, 0.7f);
            LivingClutter(); KitchenClutter(); BedroomClutter(); HallwayClutter(); GardenClutter();
        }
    }
}
