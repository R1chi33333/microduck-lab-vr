using System.Collections.Generic;
using MicroduckVR.Duck;
using MicroduckVR.World;
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
            return !Physics.CheckSphere(new Vector3(x, y + r + 0.012f, z), r, ~(1 << DuckSetup.LayerDuck), QueryTriggerInteraction.Ignore);
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

        // ================================================================== living room
        static void LivingClutter()
        {
            const string R = "living";
            Pitch(R);
            TvCorner(R);
            Bookcase(R, new Vector3(2.78f, 0f, 1.55f));

            // reading corner by the sofa end: lamp, side table with a mug, a stack of books on the floor
            FloorLamp(R, new Vector3(0.3f, 0f, -2.25f));
            UC(R, "Side table", 0.3f, 0f, -1.75f, 0.2f, 0.5f, "#FFC43D", true, "table");
            UC(R, "Side table top", 0.3f, 0.5f, -1.75f, 0.22f, 0.025f, "#FF8A3D");
            Mug(R, new Vector3(0.33f, 0.525f, -1.72f), 30f, "#FF5D73");
            BookStack(R, 2.35f, 0f, 1.95f, 5, true); Mark(2.35f, 1.95f, 0.15f);
            BookStack(R, 1.0f, 0f, -0.6f, 3, true); Mark(1.0f, -0.6f, 0.15f);

            // coffee table: magazines, the remote, a bowl of crisps; game pad dropped on the floor
            Magazine(R, new Vector3(1.5f, 0.4f, -1.0f), 12f, "#FF5D73");
            Magazine(R, new Vector3(1.86f, 0.4f, -0.95f), -20f, "#1FA6A0");
            Remote(R, new Vector3(1.25f, 0.4f, -1.1f), 40f);
            GamePad(R, new Vector3(-0.35f, 0f, 1.75f), 30f); Mark(-0.35f, 1.75f, 0.08f);

            // cushions thrown off the sofa
            Cushion(R, new Vector3(0.6f, 0f, -1.3f), 25f, "#FFC43D");
            Cushion(R, new Vector3(2.45f, 0f, -0.45f), -15f, "#FF8A3D");

            // the dog's corner under the window
            PetBed(R, new Vector3(-1.9f, 0f, -2.05f));
            PetBowl(R, new Vector3(-1.35f, 0f, -2.15f), "#3A86FF", "#B5793E");
            Bone(R, new Vector3(-1.55f, 0f, -1.7f), 35f);

            // laundry basket with a sock trail, shoes by the hall door, boxes in the corner
            LaundryBasket(R, new Vector3(-2.6f, 0f, 0.6f));
            Sock(R, new Vector3(-2.25f, 0f, 0.85f), 50f, "#FF5D73");
            Sock(R, new Vector3(0.35f, 0f, 0.95f), -20f, "#3A86FF");
            ShoePair(R, 0.7f, 2.15f, "#FFC43D", 10f);
            CartonStack(R, -2.68f, 2.28f, 2);

            // toys everywhere: by the pitch, under the table, in front of the TV
            Teddy(R, new Vector3(0.15f, 0f, 1.3f), 165f, 0.9f);
            Bunny(R, new Vector3(0.5f, 0f, 1.45f), 200f, 0.9f, "#FFE2BF");
            Mark(0.15f, 1.3f, 0.15f); Mark(0.5f, 1.45f, 0.12f);
            ToyScatter(R, 0.85f, 1.2f, -0.4f, 1.9f, 6);
            ToyScatter(R, -0.6f, 0.6f, -0.35f, 0.35f, 7);
            ToyScatter(R, -0.5f, 0.9f, 0.65f, 1.95f, 8);

            // the big table: laptop, mug, pencils, a little plant
            Laptop(R, new Vector3(0.22f, 0.75f, -0.25f), -10f);
            Mug(R, new Vector3(-0.15f, 0.75f, -0.3f), 120f, "#FFC43D");
            for (int i = 0; i < 2; i++) Crayon(R, new Vector3(0.05f + i * 0.04f, 0.75f, 0.32f), 70f + i * 25f, AnyColour());
            Plant(R, new Vector3(0.6f, 0.75f, -0.35f), 0.45f, "#7B61FF", false);

            // wall art
            Poster(R, new Vector3(1.5f, 1.25f, -2.43f), 0f, new Vector2(0.7f, 0.5f), "#FFC43D");
            WallClock(R, new Vector3(-2.94f, 1.7f, 1.2f), 90f);
        }

        /// <summary>Football pitch details on the rug: mowing stripes, white lines, centre circle, penalty boxes,
        /// spots, corner flags, nets on the goals, and a football on the centre spot.</summary>
        static void Pitch(string R)
        {
            const float cx = 1.93f, cz = 0.73f, hx = 0.62f, hz = 1.245f, y = 0.0085f;
            var root = NewProp(R, "Pitch details", new Vector3(cx, 0f, cz));
            for (int i = 0; i < 10; i++) if (i % 2 == 0) PBox(root, new Vector3(0f, y, -1.25f + 0.125f + i * 0.25f), new Vector3(1.3f, 0.002f, 0.25f), "#54B04F", 0f);
            const string L = "#FFFFFF"; const float lw = 0.014f, ly = 0.0098f, lh = 0.002f;
            void Line(float x, float z, float w, float d) => PBox(root, new Vector3(x, ly, z), new Vector3(w, lh, d), L, 0f);
            foreach (var s in new[] { -1f, 1f })
            {
                Line(s * hx, 0f, lw, hz * 2f + lw);             // touchlines
                Line(0f, s * hz, hx * 2f + lw, lw);             // goal lines
                Line(0f, s * (hz - 0.27f), 0.8f, lw);           // penalty box
                foreach (var sx in new[] { -1f, 1f }) Line(sx * 0.4f, s * (hz - 0.135f), lw, 0.27f);
                Line(0f, s * (hz - 0.09f), 0.5f, lw);           // goal box
                foreach (var sx in new[] { -1f, 1f }) Line(sx * 0.25f, s * (hz - 0.045f), lw, 0.09f);
                PCyl(root, new Vector3(0f, ly, s * (hz - 0.18f)), 0.012f, lh, L, default, -1f, 12);   // penalty spot
                foreach (var sx in new[] { -1f, 1f })                                                    // corner flags
                {
                    PCyl(root, new Vector3(sx * hx, 0.06f, s * hz), 0.0035f, 0.11f, "#FFFFFF", default, -1f, 8);
                    PBox(root, new Vector3(sx * hx, 0.1f, s * hz - sx * 0.022f), new Vector3(0.003f, 0.03f, 0.04f), s > 0 ? "#FFC43D" : "#FF5D73", 0f, new Vector3(0f, sx * 90f - 90f, 0f));
                }
                // goal nets: back, sides and roof as a grid of thin strings
                float gz = s * (hz + 0.06f), back = s * (hz + 0.12f);
                for (int i = 0; i <= 11; i++) PBox(root, new Vector3(-0.165f + i * 0.03f, 0.086f, back - s * 0.002f), new Vector3(0.0025f, 0.16f, 0.0025f), "#E6E9F0", 0f);
                for (int j = 1; j <= 5; j++) PBox(root, new Vector3(0f, 0.006f + j * 0.027f, back - s * 0.002f), new Vector3(0.34f, 0.0025f, 0.0025f), "#E6E9F0", 0f);
                foreach (var sx in new[] { -1f, 1f })
                {
                    for (int i = 1; i <= 3; i++) PBox(root, new Vector3(sx * 0.168f, 0.086f, s * (hz + i * 0.03f)), new Vector3(0.0025f, 0.16f, 0.0025f), "#E6E9F0", 0f);
                    for (int j = 1; j <= 5; j++) PBox(root, new Vector3(sx * 0.168f, 0.006f + j * 0.027f, gz), new Vector3(0.0025f, 0.0025f, 0.12f), "#E6E9F0", 0f);
                }
                for (int i = 1; i <= 3; i++) PBox(root, new Vector3(0f, 0.164f, s * (hz + i * 0.03f)), new Vector3(0.34f, 0.0025f, 0.0025f), "#E6E9F0", 0f);
                for (int i = 0; i <= 11; i++) PBox(root, new Vector3(-0.165f + i * 0.03f, 0.164f, gz), new Vector3(0.0025f, 0.0025f, 0.12f), "#E6E9F0", 0f);
            }
            Line(0f, 0f, hx * 2f, lw);                                                                  // halfway line
            PRing(root, new Vector3(0f, ly, 0f), 0.167f, 0.153f, lh, L);                              // centre circle
            PCyl(root, new Vector3(0f, ly, 0f), 0.014f, lh, L, default, -1f, 12);                    // centre spot
            Stat(root, R);
            Football(R, new Vector3(cx, 0.008f, cz), 0.032f); Mark(cx, cz, 0.1f);
            Football(R, new Vector3(cx + 0.3f, 0.008f, cz - 0.7f), 0.026f); Mark(cx + 0.3f, cz - 0.7f, 0.08f);
        }

        /// <summary>TV console with drawers, a TV on a stand showing a cartoon, a game console and a few books.</summary>
        static void TvCorner(string R)
        {
            var con = NewProp(R, "TV console", new Vector3(-1.15f, 0f, 2.28f));
            PBox(con, new Vector3(0f, 0.22f, 0f), new Vector3(1.3f, 0.4f, 0.36f), "#7B61FF", 0.08f);
            foreach (var sx in new[] { -1f, 1f }) foreach (var lx in new[] { -0.6f, 0.6f })
                PCyl(con, new Vector3(lx, 0.01f, sx * 0.14f), 0.02f, 0.02f, Ink);
            foreach (var x in new[] { -0.32f, 0.32f })
            {
                PBox(con, new Vector3(x, 0.22f, -0.181f), new Vector3(0.58f, 0.32f, 0.012f), Shade("#7B61FF", 1.15f), 0.2f);
                PBall(con, new Vector3(x * 0.4f, 0.24f, -0.19f), new Vector3(0.035f, 0.035f, 0.02f), Gold);
            }
            Stat(con, R, true, "table");
            var tv = NewProp(R, "TV", new Vector3(-1.15f, 0.42f, 2.36f));
            PBox(tv, new Vector3(0f, 0.01f, 0f), new Vector3(0.32f, 0.02f, 0.16f), Ink, 0.4f);
            PBox(tv, new Vector3(0f, 0.05f, 0.01f), new Vector3(0.06f, 0.07f, 0.03f), Ink, 0.4f);
            PBox(tv, new Vector3(0f, 0.36f, 0f), new Vector3(0.96f, 0.56f, 0.05f), Ink, 0.12f);
            PBox(tv, new Vector3(0f, 0.37f, -0.026f), new Vector3(0.88f, 0.48f, 0.004f), "#9AD8F2", 0.04f);
            // the cartoon on screen: sun, clouds, hills, a little house
            PBall(tv, new Vector3(0.28f, 0.5f, -0.03f), new Vector3(0.12f, 0.12f, 0.004f), "#FFE45C");
            foreach (var (x, yy) in new[] { (-0.25f, 0.52f), (-0.18f, 0.53f), (-0.11f, 0.51f) }) PBall(tv, new Vector3(x, yy, -0.03f), new Vector3(0.1f, 0.06f, 0.004f), "#FFFFFF");
            PBall(tv, new Vector3(-0.2f, 0.16f, -0.031f), new Vector3(0.5f, 0.2f, 0.004f), "#8FD35A");
            PBall(tv, new Vector3(0.18f, 0.15f, -0.032f), new Vector3(0.55f, 0.16f, 0.004f), "#56B65A");
            PBox(tv, new Vector3(0.1f, 0.27f, -0.034f), new Vector3(0.1f, 0.08f, 0.004f), "#FF5D73", 0.1f);
            PBox(tv, new Vector3(0.1f, 0.33f, -0.035f), new Vector3(0.12f, 0.03f, 0.004f), "#7B61FF", 0.1f, new Vector3(0f, 0f, 0f));
            PCyl(tv, new Vector3(0.41f, 0.12f, -0.026f), 0.006f, 0.004f, "#FF5D73", new Vector3(90f, 0f, 0f), -1f, 8);   // standby light
            Stat(tv, R);
            var gc = NewProp(R, "Game console", new Vector3(-0.66f, 0.42f, 2.24f), -8f);
            PBox(gc, new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.05f, 0.16f), "#F2F2F2", 0.35f);
            PBox(gc, new Vector3(0f, 0.051f, 0f), new Vector3(0.2f, 0.003f, 0.02f), "#3A86FF", 0.3f);
            PCyl(gc, new Vector3(0.08f, 0.025f, -0.081f), 0.006f, 0.003f, "#8FD35A", new Vector3(90f, 0f, 0f), -1f, 8);
            Stat(gc, R);
            BookRow(R, new Vector3(-1.78f, 0.42f, 2.24f), Vector3.right, 0f, 3, false, 0.24f);
        }

        /// <summary>Bookcase against the east wall, shelves packed with books (spines out), a few lying stacks and ornaments.</summary>
        static void Bookcase(string R, Vector3 at)
        {
            var bc = NewProp(R, "Bookcase", at);
            const float w = 0.36f, h = 1.6f, d = 1.1f;
            foreach (var s in new[] { -1f, 1f }) PBox(bc, new Vector3(0f, h / 2f, s * (d / 2f - 0.0125f)), new Vector3(w, h, 0.025f), "#FFF6E4", 0.2f);
            PBox(bc, new Vector3(0.17f, h / 2f, 0f), new Vector3(0.02f, h, d), "#E8D8BE", 0.2f);
            PBox(bc, new Vector3(0f, h - 0.0125f, 0f), new Vector3(w + 0.02f, 0.025f, d + 0.02f), "#FFF6E4", 0.2f);
            PBox(bc, new Vector3(0f, 0.04f, 0f), new Vector3(w, 0.08f, d), "#E8D8BE", 0.2f);
            for (int s = 0; s < 4; s++) PBox(bc, new Vector3(0f, 0.32f + s * 0.38f, 0f), new Vector3(w - 0.02f, 0.025f, d - 0.05f), "#FFF6E4", 0.2f);
            Stat(bc, R, true);
            for (int s = 0; s < 4; s++)
            {
                float y = 0.3325f + s * 0.38f;
                if (s == 3) y = 0.08f;                       // bottom shelf on the plinth
                int n = 9 + crng.Next(4);
                BookRow(R, new Vector3(at.x - 0.03f, y, at.z - 0.5f), Vector3.forward, 90f, n, false, s == 3 ? 0.22f : 0.29f);
                if (s % 2 == 0) BookStack(R, at.x - 0.03f, y, at.z + 0.35f, 3, false);
                else { var vase = NewProp(R, "Vase", new Vector3(at.x - 0.03f, y, at.z + 0.38f)); PCyl(vase, new Vector3(0f, 0.08f, 0f), 0.05f, 0.16f, "#1FA6A0", default, 0.03f, 16); for (int i = 0; i < 3; i++) PBall(vase, new Vector3(Rf(-0.03f, 0.03f), 0.2f + i * 0.02f, Rf(-0.03f, 0.03f)), Vector3.one * 0.05f, i == 1 ? "#FFE45C" : "#FF5D73"); Stat(vase, R); }
            }
        }

        static void Cushion(string R, Vector3 pos, float yaw, string hex)
        {
            var c = NewProp(R, "Floor cushion", pos, yaw);
            PBox(c, new Vector3(0f, 0.07f, 0f), new Vector3(0.42f, 0.14f, 0.42f), hex, 0.45f, default, 0.25f);
            PCyl(c, new Vector3(0f, 0.155f, 0f), 0.02f, 0.012f, Shade(hex, 0.7f), default, -1f, 10);
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) }) PBall(c, new Vector3(x * 0.2f, 0.07f, z * 0.2f), new Vector3(0.05f, 0.03f, 0.05f), Shade(hex, 0.8f));
            Stat(c, R, true, "rug");
        }

        static void PetBed(string R, Vector3 pos)
        {
            var b = NewProp(R, "Pet bed", pos);
            PRing(b, new Vector3(0f, 0.07f, 0f), 0.34f, 0.25f, 0.14f, "#C98A55");
            for (int i = 0; i < 3; i++) PRing(b, new Vector3(0f, 0.03f + i * 0.045f, 0f), 0.345f, 0.335f, 0.008f, "#A8733F");
            PCyl(b, new Vector3(0f, 0.03f, 0f), 0.3f, 0.06f, "#FF8A9A", default, -1f, 24);
            PBall(b, new Vector3(0.1f, 0.07f, 0.05f), new Vector3(0.1f, 0.05f, 0.08f), "#FFFFFF");
            Stat(b, R, true, "rug");
        }

        static void LaundryBasket(string R, Vector3 pos)
        {
            var b = NewProp(R, "Laundry basket", pos);
            PCyl(b, new Vector3(0f, 0.15f, 0f), 0.18f, 0.3f, "#8FD3B6", default, 0.21f, 20);
            for (int i = 0; i < 4; i++) PRing(b, new Vector3(0f, 0.05f + i * 0.07f, 0f), 0.186f + i * 0.008f, 0.17f, 0.012f, Shade("#8FD3B6", 0.8f));
            PRing(b, new Vector3(0f, 0.3f, 0f), 0.215f, 0.19f, 0.02f, Shade("#8FD3B6", 0.8f));
            PBall(b, new Vector3(0f, 0.3f, 0f), new Vector3(0.34f, 0.12f, 0.3f), "#FFF6E4");
            PBall(b, new Vector3(0.06f, 0.34f, -0.05f), new Vector3(0.16f, 0.08f, 0.12f), "#3A86FF");
            PBox(b, new Vector3(0.18f, 0.3f, 0.06f), new Vector3(0.05f, 0.025f, 0.18f), "#FF5D73", 0.45f, new Vector3(0f, 20f, 50f));   // sleeve hanging out
            Stat(b, R, true);
        }

        static void Poster(string R, Vector3 c, float yaw, Vector2 size, string bg)
        {
            var p = NewProp(R, "Poster", c, yaw);
            PBox(p, Vector3.zero, new Vector3(size.x + 0.04f, size.y + 0.04f, 0.02f), Ink, 0.15f);
            PBox(p, new Vector3(0f, 0f, 0.011f), new Vector3(size.x, size.y, 0.003f), bg, 0.05f);
            PBall(p, new Vector3(-size.x * 0.2f, size.y * 0.12f, 0.013f), new Vector3(size.y * 0.5f, size.y * 0.5f, 0.003f), "#FF5D73");
            PBall(p, new Vector3(size.x * 0.22f, -size.y * 0.12f, 0.014f), new Vector3(size.y * 0.42f, size.y * 0.42f, 0.003f), "#3A86FF");
            PBox(p, new Vector3(0f, -size.y * 0.38f, 0.014f), new Vector3(size.x * 0.7f, size.y * 0.08f, 0.003f), Ink, 0.3f);
            Stat(p, R);
        }

        static void WallClock(string R, Vector3 c, float yaw)
        {
            var k = NewProp(R, "Clock", c, yaw);
            PCyl(k, Vector3.zero, 0.16f, 0.03f, "#FF5D73", new Vector3(90f, 0f, 0f), -1f, 28);
            PCyl(k, new Vector3(0f, 0f, 0.016f), 0.135f, 0.004f, "#FFFFFF", new Vector3(90f, 0f, 0f), -1f, 28);
            for (int i = 0; i < 12; i++) { float a = i * 30f * Mathf.Deg2Rad; PBox(k, new Vector3(Mathf.Sin(a) * 0.115f, Mathf.Cos(a) * 0.115f, 0.019f), new Vector3(0.008f, i % 3 == 0 ? 0.025f : 0.012f, 0.003f), Ink, 0.3f, new Vector3(0f, 0f, -i * 30f)); }
            PBox(k, new Vector3(0.03f, 0.02f, 0.021f), new Vector3(0.012f, 0.075f, 0.003f), Ink, 0.4f, new Vector3(0f, 0f, -55f));
            PBox(k, new Vector3(-0.012f, 0.045f, 0.022f), new Vector3(0.008f, 0.1f, 0.003f), Ink, 0.4f, new Vector3(0f, 0f, 15f));
            PCyl(k, new Vector3(0f, 0f, 0.023f), 0.01f, 0.004f, Gold, new Vector3(90f, 0f, 0f), -1f, 10);
            Stat(k, R);
        }

        // ================================================================== kitchen
        static void KitchenClutter()
        {
            const string R = "kitchen";
            const float top = 0.91f;
            Kettle(R, new Vector3(-3.6f, top, -2.2f), 200f, "#FF5D73");
            Toaster(R, new Vector3(-4.0f, top, -2.2f), 0f);
            CerealBox(R, new Vector3(-4.95f, top, -2.3f), 8f, "#FFC43D", 0.3f);
            CerealBox(R, new Vector3(-5.2f, top, -2.28f), -6f, "#3A86FF", 0.26f);
            for (int i = 0; i < 4; i++) Jar(R, new Vector3(-5.55f + i * 0.09f, top, -2.35f), Bright[i], Rf(0.1f, 0.15f));
            var rack = NewProp(R, "Dish rack", new Vector3(-4.6f, top, -2.08f));
            PBox(rack, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.02f, 0.2f), "#97A0AE", 0.3f);
            for (int i = 0; i < 5; i++) { PCyl(rack, new Vector3(-0.1f + i * 0.045f, 0.1f, 0f), 0.09f, 0.012f, i % 2 == 0 ? "#FFFFFF" : "#9AD8F2", new Vector3(0f, 0f, 90f - 12f), -1f, 20); }
            Stat(rack, R);
            // the kitchen table: places set for four, newspaper, a cup knocked over
            foreach (var (px, pz) in new[] { (-4.8f, 0.4f), (-5.4f, 0.4f), (-4.8f, 0.8f), (-5.4f, 0.8f) })
            {
                var pl = NewProp(R, "Plate", new Vector3(px, 0.765f, pz));
                PCyl(pl, new Vector3(0f, 0.004f, 0f), 0.08f, 0.008f, "#FFFFFF", default, 0.1f, 24);
                PRing(pl, new Vector3(0f, 0.009f, 0f), 0.1f, 0.085f, 0.004f, "#3A86FF");
                if (crng.Next(2) == 0) for (int i = 0; i < 3; i++) PBall(pl, new Vector3(Rf(-0.04f, 0.04f), 0.012f, Rf(-0.04f, 0.04f)), new Vector3(0.03f, 0.012f, 0.03f), "#D9A35E");   // crumbs of toast
                Stat(pl, R);
                Mug(R, new Vector3(px + 0.13f, 0.765f, pz + 0.02f), Rf(0f, 360f), AnyColour(), false);
            }
            Magazine(R, new Vector3(-5.1f, 0.765f, 0.95f), 15f, "#EDEAE0", false);
            // floor: the cat's bowl and a dropped spoon, a bin, a stool, a mop bucket, shopping and runaway fruit
            PetBowl(R, new Vector3(-5.0f, 0f, 0.55f), "#FF5D73", "#C98A55");
            var spoon = NewProp(R, "Spoon", new Vector3(-4.75f, 0f, 0.75f), 30f);
            PBox(spoon, new Vector3(0f, 0.004f, -0.03f), new Vector3(0.012f, 0.006f, 0.11f), "#B9C3CF", 0.4f);
            PBall(spoon, new Vector3(0f, 0.006f, 0.04f), new Vector3(0.035f, 0.012f, 0.05f), "#B9C3CF");
            Dyn(spoon, 800f);
            Bin(R, new Vector3(-6.72f, 0f, -1.45f), "#3A86FF");
            UB(R, "Step stool", -6.55f, 0f, 1.7f, 0.36f, 0.24f, 0.3f, "#FF8A3D", 15f, true, "table");
            Bucket(R, new Vector3(-6.6f, 0f, 0.6f), "#FFC43D");
            var mop = NewProp(R, "Mop", new Vector3(-6.75f, 0f, 0.45f));
            PCyl(mop, new Vector3(0.05f, 0.55f, 0f), 0.012f, 1.1f, "#C98A55", new Vector3(0f, 0f, -8f), -1f, 8);
            for (int i = 0; i < 8; i++) PBox(mop, new Vector3(Rf(-0.03f, 0.03f), 0.04f, Rf(-0.04f, 0.04f)), new Vector3(0.02f, 0.08f, 0.02f), "#F2F2F2", 0.5f, new Vector3(Rf(-20f, 20f), 0f, Rf(-20f, 20f)));
            Stat(mop, R);
            ShoppingBag(R, new Vector3(-3.55f, 0f, 1.95f), -10f, "#8FD35A");
            ShoppingBag(R, new Vector3(-3.9f, 0f, 2.15f), 20f, "#FFF6E4");
            Orange(R, new Vector3(-3.6f, 0f, 1.6f), false);
            Orange(R, new Vector3(-3.78f, 0f, 1.45f), true);
            Orange(R, new Vector3(-4.1f, 0f, 1.25f), false);
            // fridge magnets, calendar, a floor mat
            for (int i = 0; i < 6; i++)
            {
                var mg = NewProp(R, "Magnet", new Vector3(-6.4f + Rf(-0.2f, 0.2f), Rf(0.9f, 1.6f), -1.75f));
                if (i % 2 == 0) PBall(mg, Vector3.zero, new Vector3(0.06f, 0.06f, 0.015f), AnyColour());
                else PBox(mg, Vector3.zero, new Vector3(0.07f, 0.05f, 0.012f), AnyColour(), 0.4f, new Vector3(0f, 0f, Rf(-15f, 15f)));
                Stat(mg, R);
            }
            var cal = NewProp(R, "Calendar", new Vector3(-3.2f, 1.42f, 0.8f), -90f);
            PBox(cal, Vector3.zero, new Vector3(0.32f, 0.45f, 0.01f), "#FFFFFF", 0.1f);
            PBox(cal, new Vector3(0f, 0.13f, 0.006f), new Vector3(0.3f, 0.17f, 0.002f), "#8FD35A", 0.1f);
            for (int i = 0; i < 4; i++) for (int j = 0; j < 5; j++) PBox(cal, new Vector3(-0.12f + j * 0.06f, -0.01f - i * 0.045f, 0.006f), new Vector3(0.045f, 0.03f, 0.002f), (i * 5 + j) == 9 ? "#FF5D73" : "#EDEAE0", 0.2f);
            Stat(cal, R);
            UB(R, "Floor mat", -4.6f, 0f, -1.7f, 0.9f, 0.006f, 0.5f, "#FF5D73");
        }

        // ================================================================== bedroom
        static void BedroomClutter()
        {
            const string R = "bedroom";
            // wardrobe with two doors, knobs, feet, and a sock caught in the door
            var wr = NewProp(R, "Wardrobe", new Vector3(3.6f, 0f, 2.15f));
            PBox(wr, new Vector3(0f, 0.93f, 0f), new Vector3(0.9f, 1.75f, 0.55f), "#8FD3B6", 0.06f);
            foreach (var s in new[] { -1f, 1f })
            {
                PBox(wr, new Vector3(s * 0.222f, 0.95f, -0.277f), new Vector3(0.43f, 1.6f, 0.012f), Shade("#8FD3B6", 1.12f), 0.15f);
                PBall(wr, new Vector3(s * 0.04f, 0.98f, -0.29f), new Vector3(0.04f, 0.04f, 0.025f), Gold);
                PCyl(wr, new Vector3(s * 0.38f, 0.03f, 0f), 0.035f, 0.06f, Ink, default, 0.025f);
            }
            PBox(wr, new Vector3(0f, 1.82f, 0f), new Vector3(0.94f, 0.04f, 0.58f), "#FFF6E4", 0.3f);
            PBox(wr, new Vector3(0.02f, 0.5f, -0.29f), new Vector3(0.05f, 0.12f, 0.02f), "#FF5D73", 0.45f, new Vector3(0f, 0f, 12f));
            Stat(wr, R, true);
            Carton(R, new Vector3(3.55f, 1.84f, 2.15f), 5f, new Vector3(0.4f, 0.25f, 0.4f), false);

            // desk: drawers, a lamp, a pencil pot, drawings, a stack of books; a chair pulled out
            var desk = NewProp(R, "Desk", new Vector3(6.7f, 0f, -0.1f));
            PBox(desk, new Vector3(0f, 0.62f, 0f), new Vector3(0.55f, 0.04f, 1.0f), "#FFC43D", 0.3f);
            PBox(desk, new Vector3(0f, 0.3f, -0.45f), new Vector3(0.5f, 0.6f, 0.04f), "#FFC43D", 0.3f);
            PBox(desk, new Vector3(0f, 0.3f, 0.3f), new Vector3(0.5f, 0.6f, 0.36f), "#FFC43D", 0.15f);
            for (int i = 0; i < 3; i++) { PBox(desk, new Vector3(-0.252f, 0.1f + i * 0.19f, 0.3f), new Vector3(0.01f, 0.17f, 0.32f), Shade("#FFC43D", 1.2f), 0.2f); PBall(desk, new Vector3(-0.262f, 0.1f + i * 0.19f, 0.3f), new Vector3(0.02f, 0.025f, 0.06f), "#FF5D73"); }
            Stat(desk, R, true, "table");
            var lamp = NewProp(R, "Desk lamp", new Vector3(6.82f, 0.64f, -0.45f), -60f);
            PCyl(lamp, new Vector3(0f, 0.012f, 0f), 0.07f, 0.024f, "#3A86FF", default, 0.06f, 18);
            PCyl(lamp, new Vector3(0f, 0.13f, 0.03f), 0.01f, 0.24f, "#3A86FF", new Vector3(15f, 0f, 0f), -1f, 8);
            PCyl(lamp, new Vector3(0f, 0.27f, -0.02f), 0.012f, 0.16f, "#3A86FF", new Vector3(-60f, 0f, 0f), -1f, 8);
            PCyl(lamp, new Vector3(0f, 0.27f, -0.12f), 0.065f, 0.09f, "#3A86FF", new Vector3(150f, 0f, 0f), 0.03f, 18);
            PBall(lamp, new Vector3(0f, 0.25f, -0.15f), Vector3.one * 0.04f, "#FFFDE0");
            Stat(lamp, R);
            var pot = NewProp(R, "Pencil pot", new Vector3(6.85f, 0.64f, 0.2f));
            PCyl(pot, new Vector3(0f, 0.05f, 0f), 0.04f, 0.1f, "#FF5D73", default, -1f, 16);
            for (int i = 0; i < 5; i++) { PCyl(pot, new Vector3(Rf(-0.02f, 0.02f), 0.11f, Rf(-0.02f, 0.02f)), 0.006f, 0.16f, AnyColour(), new Vector3(Rf(-12f, 12f), 0f, Rf(-12f, 12f)), -1f, 6); }
            Stat(pot, R);
            for (int i = 0; i < 5; i++)
            {
                var dr = NewProp(R, "Drawing", new Vector3(6.62f + Rf(-0.08f, 0.08f), 0.641f + i * 0.0025f, Rf(-0.15f, 0.25f)), Rf(-30f, 30f));
                PBox(dr, new Vector3(0f, 0.001f, 0f), new Vector3(0.21f, 0.002f, 0.29f), "#FFFFFF", 0f);
                PBall(dr, new Vector3(Rf(-0.04f, 0.04f), 0.0025f, Rf(-0.06f, 0.06f)), new Vector3(0.08f, 0.002f, 0.08f), AnyColour());
                PBox(dr, new Vector3(0f, 0.0025f, -0.08f), new Vector3(0.14f, 0.002f, 0.04f), "#8FD35A", 0.3f);
                Stat(dr, R);
            }
            BookStack(R, 6.75f, 0.64f, -0.05f, 3, false);
            var ch = NewProp(R, "Desk chair", new Vector3(6.15f, 0f, -0.15f), 100f);
            PBox(ch, new Vector3(0f, 0.4f, 0f), new Vector3(0.38f, 0.05f, 0.38f), "#FF5D73", 0.4f, default, 0.1f);
            PBox(ch, new Vector3(0f, 0.68f, -0.17f), new Vector3(0.36f, 0.4f, 0.04f), "#FF5D73", 0.4f);
            foreach (var (x, z) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) }) PCyl(ch, new Vector3(x * 0.16f, 0.19f, z * 0.16f), 0.018f, 0.38f, Ink, default, 0.014f, 8);
            Stat(ch, R, true, "table");
            var bb = NewProp(R, "Beanbag", new Vector3(3.85f, 0f, 1.3f), 20f);
            PBox(bb, new Vector3(0f, 0.18f, 0f), new Vector3(0.64f, 0.36f, 0.64f), "#7B61FF", 0.5f, default, 0.35f);
            PBall(bb, new Vector3(0f, 0.34f, 0.05f), new Vector3(0.35f, 0.08f, 0.3f), Shade("#7B61FF", 0.85f));
            Stat(bb, R, true, "rug");

            // plushies: a teddy on the bed, more on the floor
            Teddy(R, new Vector3(6.35f, 0.15f, -1.95f), -100f, 1.1f, "#C98A55", "#3A86FF", false);
            Teddy(R, new Vector3(4.3f, 0f, -0.2f), 30f, 0.8f, "#FF8A9A", "#FFC43D");
            Bunny(R, new Vector3(4.6f, 0f, -0.4f), -20f, 1f);
            Mark(4.3f, -0.2f, 0.14f); Mark(4.6f, -0.4f, 0.12f);

            // the wind-up train circling the round rug
            Train(R, new Vector3(4.95f, 0f, 0.9f), 0.55f);

            // a block tower, book pile, toys everywhere (not on the tracks)
            for (int i = 0; i < 6; i++) Block(R, new Vector3(5.85f + Rf(-0.006f, 0.006f), i * 0.0605f, 1.75f + Rf(-0.006f, 0.006f)), Rf(-12f, 12f), 0.06f, Bright[i % Bright.Length]);
            Mark(5.85f, 1.75f, 0.1f);
            BookStack(R, 6.5f, 0f, 0.75f, 4, true); Mark(6.5f, 0.75f, 0.15f);
            var c0 = new Vector3(4.95f, 0f, 0.9f);
            ToyScatter(R, 3.8f, 6.1f, 0.0f, 1.9f, 16, c0, 0.42f, 0.72f);
            ToyScatter(R, 3.6f, 4.7f, -1.2f, -0.55f, 6);

            // a laundry pile, slippers, a backpack, posters
            var lp = NewProp(R, "Laundry pile", new Vector3(6.5f, 0f, 2.15f));
            PBall(lp, new Vector3(0f, 0.08f, 0f), new Vector3(0.5f, 0.18f, 0.42f), "#3A86FF");
            PBall(lp, new Vector3(-0.08f, 0.17f, -0.05f), new Vector3(0.34f, 0.12f, 0.28f), "#FFF6E4");
            PBox(lp, new Vector3(0.12f, 0.22f, 0.02f), new Vector3(0.22f, 0.03f, 0.06f), "#FF5D73", 0.45f, new Vector3(0f, 40f, 10f));
            PBox(lp, new Vector3(-0.2f, 0.05f, -0.2f), new Vector3(0.06f, 0.025f, 0.22f), "#FFC43D", 0.45f, new Vector3(0f, 70f, 0f));
            Stat(lp, R, true, "rug");
            ShoePair(R, 4.55f, -1.3f, "#FF8A9A", -30f, 0.85f);
            var bp = NewProp(R, "Backpack", new Vector3(3.4f, 0f, -0.1f), 70f);
            PBox(bp, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.38f, 0.17f), "#FF8A3D", 0.45f, default, 0.12f);
            PBox(bp, new Vector3(0f, 0.13f, 0.09f), new Vector3(0.22f, 0.15f, 0.05f), Shade("#FF8A3D", 0.85f), 0.45f, default, 0.1f);
            PBox(bp, new Vector3(0f, 0.21f, 0.115f), new Vector3(0.18f, 0.008f, 0.006f), Ink, 0.3f);
            PBox(bp, new Vector3(0f, 0.34f, 0.087f), new Vector3(0.26f, 0.008f, 0.006f), Ink, 0.3f);
            foreach (var s in new[] { -1f, 1f }) PBox(bp, new Vector3(s * 0.08f, 0.22f, -0.095f), new Vector3(0.04f, 0.3f, 0.02f), Ink, 0.45f);
            PRing(bp, new Vector3(0f, 0.4f, 0f), 0.03f, 0.022f, 0.012f, Ink, new Vector3(90f, 0f, 0f));
            Stat(bp, R, true);
            Poster(R, new Vector3(6.94f, 1.25f, 1.2f), -90f, new Vector2(0.45f, 0.6f), "#3A86FF");
            Poster(R, new Vector3(5.2f, 1.35f, -2.43f), 0f, new Vector2(0.5f, 0.4f), "#8FD35A");
        }

        /// <summary>Circular wooden track (sleepers and two rails) plus a wind-up train: engine with boiler, chimney,
        /// dome, cab, cowcatcher and spoked wheels, an open wagon full of blocks, and a coach.</summary>
        static void Train(string R, Vector3 c, float radius)
        {
            var track = NewProp(R, "Train track", c + Vector3.up * 0.009f);
            for (int i = 0; i < 30; i++)
            {
                float a = i / 30f * 360f;
                var d = Quaternion.Euler(0f, a, 0f);
                PBox(track, d * new Vector3(radius, 0.003f, 0f), new Vector3(0.15f, 0.006f, 0.024f), "#C98A55", 0.3f, new Vector3(0f, a, 0f));
            }
            foreach (var off in new[] { -0.045f, 0.045f }) PRing(track, new Vector3(0f, 0.01f, 0f), radius + off + 0.004f, radius + off - 0.004f, 0.008f, "#97A0AE");
            Stat(track, R);
            const float rail = 0.023f;

            Transform WheelPair(Transform car, float z)
            {
                foreach (var sd in new[] { -1f, 1f })
                {
                    var w = PCyl(car, new Vector3(sd * 0.055f, 0.024f, z), 0.024f, 0.01f, Ink, new Vector3(0f, 0f, 90f), -1f, 18);
                    w.name = "Wheel";
                    PCyl(w, new Vector3(0f, -sd * 0.0055f, 0f), 0.011f, 0.003f, "#FF5D73", default, -1f, 12);
                    PBox(w, new Vector3(0f, -sd * 0.0062f, 0f), new Vector3(0.04f, 0.002f, 0.006f), "#FFFFFF", 0.3f);
                }
                return car;
            }

            var engine = NewProp(R, "Train engine", c);
            PBox(engine, new Vector3(0f, 0.032f, 0f), new Vector3(0.1f, 0.02f, 0.21f), "#FF5D73", 0.3f);
            PCyl(engine, new Vector3(0f, 0.078f, 0.035f), 0.045f, 0.13f, "#3A86FF", new Vector3(90f, 0f, 0f), -1f, 20);
            foreach (var z in new[] { -0.012f, 0.035f, 0.082f }) PRing(engine, new Vector3(0f, 0.078f, z), 0.048f, 0.043f, 0.007f, Gold, new Vector3(90f, 0f, 0f));
            PCyl(engine, new Vector3(0f, 0.078f, 0.103f), 0.046f, 0.012f, Ink, new Vector3(90f, 0f, 0f), -1f, 20);
            PCyl(engine, new Vector3(0f, 0.078f, 0.11f), 0.012f, 0.006f, "#FFE45C", new Vector3(90f, 0f, 0f), -1f, 12);
            PCyl(engine, new Vector3(0f, 0.145f, 0.075f), 0.014f, 0.05f, Ink, default, 0.024f, 16);          // chimney, flared
            PRing(engine, new Vector3(0f, 0.17f, 0.075f), 0.027f, 0.018f, 0.007f, Ink);
            PBall(engine, new Vector3(0f, 0.127f, 0.025f), new Vector3(0.04f, 0.035f, 0.04f), Gold);           // dome
            PBox(engine, new Vector3(0f, 0.1f, -0.06f), new Vector3(0.1f, 0.1f, 0.075f), "#FF5D73", 0.2f);   // cab
            PBox(engine, new Vector3(0f, 0.156f, -0.062f), new Vector3(0.122f, 0.014f, 0.1f), Ink, 0.4f);
            foreach (var sd in new[] { -1f, 1f }) PBox(engine, new Vector3(sd * 0.051f, 0.118f, -0.06f), new Vector3(0.003f, 0.035f, 0.042f), "#9AD8F2", 0.3f);
            PBox(engine, new Vector3(0f, 0.13f, -0.0215f), new Vector3(0.07f, 0.03f, 0.003f), "#9AD8F2", 0.3f);
            PBox(engine, new Vector3(0f, 0.02f, 0.118f), new Vector3(0.09f, 0.032f, 0.03f), Gold, 0.3f, new Vector3(-35f, 0f, 0f));   // cowcatcher
            PBox(engine, new Vector3(0f, 0.026f, -0.112f), new Vector3(0.02f, 0.012f, 0.02f), Ink, 0.3f);
            foreach (var z in new[] { 0.065f, 0f, -0.065f }) WheelPair(engine, z);
            var chimneyTop = new GameObject("Chimney top").transform; chimneyTop.SetParent(engine, false); chimneyTop.localPosition = new Vector3(0f, 0.18f, 0.075f);

            var wagon = NewProp(R, "Train wagon", c);
            PBox(wagon, new Vector3(0f, 0.032f, 0f), new Vector3(0.1f, 0.02f, 0.17f), Ink, 0.3f);
            PBox(wagon, new Vector3(0f, 0.068f, 0f), new Vector3(0.1f, 0.055f, 0.16f), "#FFC43D", 0.2f);
            PBox(wagon, new Vector3(0f, 0.093f, 0f), new Vector3(0.08f, 0.006f, 0.14f), "#A8733F", 0.2f);
            for (int i = 0; i < 4; i++) PBox(wagon, new Vector3(Rf(-0.02f, 0.02f), 0.11f, -0.05f + i * 0.033f), Vector3.one * 0.026f, Bright[(i + 2) % Bright.Length], 0.2f, new Vector3(0f, Rf(0f, 45f), Rf(-10f, 10f)));
            foreach (var z in new[] { -0.05f, 0.05f }) WheelPair(wagon, z);
            PBox(wagon, new Vector3(0f, 0.026f, -0.093f), new Vector3(0.02f, 0.012f, 0.02f), Ink, 0.3f);

            var coach = NewProp(R, "Train coach", c);
            PBox(coach, new Vector3(0f, 0.032f, 0f), new Vector3(0.1f, 0.02f, 0.17f), Ink, 0.3f);
            PBox(coach, new Vector3(0f, 0.08f, 0f), new Vector3(0.1f, 0.08f, 0.165f), "#8FD35A", 0.2f);
            PBox(coach, new Vector3(0f, 0.126f, 0f), new Vector3(0.112f, 0.016f, 0.18f), "#FFF6E4", 0.6f);
            foreach (var sd in new[] { -1f, 1f }) for (int i = 0; i < 3; i++) PBox(coach, new Vector3(sd * 0.051f, 0.09f, -0.05f + i * 0.05f), new Vector3(0.003f, 0.03f, 0.03f), "#9AD8F2", 0.3f);
            foreach (var sd in new[] { -1f, 1f }) PBox(coach, new Vector3(sd * 0.051f, 0.06f, 0f), new Vector3(0.003f, 0.008f, 0.16f), "#FFC43D", 0.3f);
            foreach (var z in new[] { -0.055f, 0.055f }) WheelPair(coach, z);

            var cars = new[] { engine, wagon, coach };
            foreach (var car in cars)
            {
                SetLayerAll(car, DuckSetup.LayerProp, false);
                var b = LocalBounds(car);
                var bc = car.gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size;
                var rb = car.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            var runner = new GameObject("Toy train").AddComponent<ToyTrain>();
            runner.transform.SetParent(rooms[R], false);
            runner.centre = c; runner.radius = radius; runner.height = rail; runner.cars = cars;
            runner.behind = new[] { 0f, 0.23f, 0.43f };
            runner.chimneyTop = chimneyTop;
            var smoke = M("smoke_puff", "#FFF6E4"); smoke.SetFloat("_Unlit", 1f);
            runner.smokeMaterial = smoke; runner.smokeMesh = SphereMesh();
            runner.Place(true);
        }

        // ================================================================== hallway
        static void HallwayClutter()
        {
            const string R = "hallway";
            var rack = NewProp(R, "Shoe rack", new Vector3(2.4f, 0f, 3.62f));
            foreach (var y in new[] { 0.04f, 0.18f, 0.32f }) PBox(rack, new Vector3(0f, y - 0.0125f, 0f), new Vector3(0.8f, 0.025f, 0.3f), "#C98A55", 0.3f);
            foreach (var s in new[] { -1f, 1f }) PBox(rack, new Vector3(s * 0.39f, 0.16f, 0f), new Vector3(0.025f, 0.32f, 0.3f), "#A8733F", 0.3f);
            Stat(rack, R, true, "table");
            for (int i = 0; i < 3; i++) Shoe(R, new Vector3(2.15f + i * 0.24f, 0.18f, 3.6f), 180f + Rf(-6f, 6f), AnyColour(), 0.8f);
            for (int i = 0; i < 3; i++) Shoe(R, new Vector3(2.15f + i * 0.24f, 0.32f, 3.6f), 180f + Rf(-6f, 6f), AnyColour(), 0.8f);
            ShoePair(R, 1.55f, 3.42f, "#3A86FF", -20f);
            ShoePair(R, -1.65f, 3.48f, "#FF5D73", 25f);
            var us = NewProp(R, "Umbrella stand", new Vector3(-2.1f, 0f, 3.62f));
            PCyl(us, new Vector3(0f, 0.22f, 0f), 0.11f, 0.44f, "#3A86FF", default, 0.12f, 18);
            PRing(us, new Vector3(0f, 0.44f, 0f), 0.125f, 0.105f, 0.02f, Shade("#3A86FF", 0.8f));
            foreach (var (dx, dz, col, lean) in new[] { (0.03f, 0.02f, "#FF5D73", 8f), (-0.035f, -0.02f, "#FFC43D", -10f) })
            {
                PCyl(us, new Vector3(dx, 0.45f, dz), 0.035f, 0.55f, col, new Vector3(lean, 0f, lean * 0.6f), 0.008f, 8);
                PRing(us, new Vector3(dx + lean * 0.004f, 0.8f, dz), 0.03f, 0.022f, 0.012f, Ink, new Vector3(90f, 0f, 0f));
            }
            Stat(us, R, true);
            var con = NewProp(R, "Console table", new Vector3(-3.2f, 0f, 2.8f));
            PBox(con, new Vector3(0f, 0.7f, 0f), new Vector3(0.9f, 0.04f, 0.3f), "#FFF6E4", 0.3f);
            foreach (var dx in new[] { -0.4f, 0.4f }) foreach (var dz in new[] { -0.11f, 0.11f }) PCyl(con, new Vector3(dx, 0.34f, dz), 0.018f, 0.68f, "#C98A55", default, 0.012f, 8);
            PBox(con, new Vector3(0f, 0.2f, 0f), new Vector3(0.82f, 0.02f, 0.24f), "#FFF6E4", 0.3f);
            Stat(con, R, true, "table");
            var bowl = NewProp(R, "Key bowl", new Vector3(-3.0f, 0.72f, 2.8f));
            PCyl(bowl, new Vector3(0f, 0.02f, 0f), 0.07f, 0.04f, "#FFC43D", default, 0.1f, 18);
            PRing(bowl, new Vector3(0.02f, 0.045f, 0.01f), 0.018f, 0.012f, 0.004f, "#B9C3CF", new Vector3(30f, 0f, 0f));
            PBox(bowl, new Vector3(0.05f, 0.042f, 0.02f), new Vector3(0.04f, 0.004f, 0.01f), "#B9C3CF", 0.3f, new Vector3(0f, 30f, 20f));
            Stat(bowl, R);
            Plant(R, new Vector3(-3.5f, 0.72f, 2.8f), 0.55f, "#FF5D73", false);
            Magazine(R, new Vector3(-3.15f, 0.22f, 2.8f), 5f, "#7B61FF", false);
            CartonStack(R, -6.5f, 3.55f, 3);
            CartonStack(R, 6.55f, 2.88f, 2, 20f);
            Plant(R, new Vector3(6.6f, 0f, 3.55f), 1.1f);
            var sk = NewProp(R, "Skateboard", new Vector3(3.6f, 0f, 2.85f), 80f);
            PBox(sk, new Vector3(0f, 0.04f, 0f), new Vector3(0.2f, 0.014f, 0.72f), "#FF8A3D", 0.45f);
            PBox(sk, new Vector3(0f, 0.048f, 0f), new Vector3(0.12f, 0.003f, 0.3f), "#FFE45C", 0.3f);
            foreach (var z in new[] { -0.24f, 0.24f })
            {
                PBox(sk, new Vector3(0f, 0.025f, z), new Vector3(0.14f, 0.012f, 0.03f), "#97A0AE", 0.3f);
                foreach (var sd in new[] { -1f, 1f }) PCyl(sk, new Vector3(sd * 0.075f, 0.017f, z), 0.017f, 0.02f, "#FFFFFF", new Vector3(0f, 0f, 90f), -1f, 14);
            }
            Dyn(sk, 120f);
            UB(R, "Scooter deck", -4.0f, 0.03f, 3.6f, 0.12f, 0.03f, 0.5f, "#1FA6A0", 0f, true);
            UB(R, "Scooter stem", -4.0f, 0.06f, 3.82f, 0.04f, 0.65f, 0.04f, "#1FA6A0");
            UB(R, "Scooter bar", -4.0f, 0.7f, 3.82f, 0.4f, 0.03f, 0.03f, "#FF5D73");
            foreach (var z in new[] { 3.37f, 3.82f }) UC(R, "Scooter wheel", -4.0f, 0f, z, 0.04f, 0.02f, Ink);
            var mat = NewProp(R, "Doormat", new Vector3(0f, 0f, 3.6f));
            PBox(mat, new Vector3(0f, 0.004f, 0f), new Vector3(0.9f, 0.008f, 0.45f), "#C98A55", 0.3f);
            PBox(mat, new Vector3(0f, 0.008f, 0f), new Vector3(0.78f, 0.003f, 0.34f), "#A8733F", 0.3f);
            PBox(mat, new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.003f, 0.08f), Cream, 0.3f);
            Stat(mat, R);
            var tb = NewProp(R, "Tennis ball", new Vector3(0.9f, 0.033f, 3.05f), Quaternion.Euler(30f, 40f, 10f));
            PBall(tb, Vector3.zero, Vector3.one * 0.066f, "#C8E62E");
            PRing(tb, Vector3.zero, 0.0335f, 0.028f, 0.006f, "#FFFFFF", new Vector3(0f, 0f, 30f));
            Dyn(tb, 120f, true, bouncy);
        }

        // ================================================================== garden
        static void GardenClutter()
        {
            const string R = "garden";
            var bench = NewProp(R, "Bench", new Vector3(0.6f, 0f, 7.25f));
            for (int i = 0; i < 3; i++) PBox(bench, new Vector3(0f, 0.42f, -0.11f + i * 0.11f), new Vector3(1.2f, 0.03f, 0.09f), "#C98A55", 0.4f);
            for (int i = 0; i < 2; i++) PBox(bench, new Vector3(0f, 0.55f + i * 0.12f, 0.16f), new Vector3(1.2f, 0.08f, 0.03f), "#C98A55", 0.4f, new Vector3(-8f, 0f, 0f));
            foreach (var dx in new[] { -0.52f, 0.52f }) { PBox(bench, new Vector3(dx, 0.2f, 0f), new Vector3(0.05f, 0.4f, 0.32f), Ink, 0.3f); PBox(bench, new Vector3(dx, 0.55f, 0.15f), new Vector3(0.05f, 0.3f, 0.04f), Ink, 0.3f); }
            Stat(bench, R, true, "table");
            WateringCan(R, new Vector3(2.7f, 0f, 5.0f), 30f, "#3A86FF");
            Bucket(R, new Vector3(2.9f, 0f, 6.95f), "#FFC43D");
            Spade(R, new Vector3(2.7f, 0f, 6.75f), 30f, "#FF5D73");
            StripeBall(R, new Vector3(0.9f, 0f, 6.55f), 0.18f, "#FF5D73", "#FFFFFF");
            Football(R, new Vector3(-0.3f, 0f, 5.6f), 0.045f);
            Gnome(R, new Vector3(-2.8f, 0f, 4.5f), 160f);
            foreach (var (bx, bz) in new[] { (2.3f, 7.3f), (-1.0f, 7.3f) })
            {
                var bed = NewProp(R, "Raised bed", new Vector3(bx, 0f, bz));
                PBox(bed, new Vector3(0f, 0.09f, 0f), new Vector3(1.0f, 0.18f, 0.3f), "#C98A55", 0.2f);
                PBox(bed, new Vector3(0f, 0.18f, 0f), new Vector3(0.92f, 0.01f, 0.22f), "#6B3E26", 0.2f);
                for (int i = 0; i < 6; i++)
                {
                    float fx = -0.4f + i * 0.16f, fh = Rf(0.12f, 0.22f);
                    string petal = i % 2 == 0 ? "#FF5D73" : i % 3 == 0 ? "#7B61FF" : "#FFE45C";
                    PCyl(bed, new Vector3(fx, 0.18f + fh / 2f, 0f), 0.006f, fh, "#3E9A45", default, -1f, 6);
                    PBall(bed, new Vector3(fx + 0.025f, 0.18f + fh * 0.5f, 0f), new Vector3(0.05f, 0.012f, 0.025f), "#56B65A", new Vector3(0f, 0f, 30f));
                    for (int k = 0; k < 5; k++) { float a = k * 72f * Mathf.Deg2Rad; PBall(bed, new Vector3(fx + Mathf.Cos(a) * 0.028f, 0.18f + fh, Mathf.Sin(a) * 0.028f), new Vector3(0.036f, 0.016f, 0.036f), petal); }
                    PBall(bed, new Vector3(fx, 0.185f + fh, 0f), new Vector3(0.028f, 0.02f, 0.028f), "#FFC43D");
                }
                Stat(bed, R, true);
            }
            var reel = NewProp(R, "Hose reel", new Vector3(3.2f, 0f, 4.3f), 90f);
            PCyl(reel, new Vector3(0f, 0.16f, 0f), 0.13f, 0.1f, "#3E9A45", new Vector3(0f, 0f, 90f), -1f, 22);
            foreach (var s in new[] { -1f, 1f }) PCyl(reel, new Vector3(s * 0.06f, 0.16f, 0f), 0.16f, 0.015f, "#8FD35A", new Vector3(0f, 0f, 90f), -1f, 22);
            for (int i = 0; i < 4; i++) PRing(reel, new Vector3(-0.04f + i * 0.027f, 0.16f, 0f), 0.135f, 0.115f, 0.022f, "#56B65A", new Vector3(0f, 0f, 90f));
            foreach (var s in new[] { -1f, 1f }) PBox(reel, new Vector3(s * 0.08f, 0.08f, 0f), new Vector3(0.02f, 0.16f, 0.16f), Ink, 0.3f);
            Stat(reel, R, true);
            for (int i = 0; i < 10; i++)
            {
                var lf = NewProp(R, "Fallen leaf", new Vector3(Rf(-2.5f, 2.5f), 0.003f, Rf(4.6f, 7.0f)), Rf(0f, 360f));
                string col = i % 3 == 0 ? "#E07A4F" : i % 3 == 1 ? "#FFC43D" : "#FF8A3D";
                PBall(lf, Vector3.zero, new Vector3(0.07f, 0.006f, 0.045f), col);
                PBox(lf, new Vector3(0f, 0.002f, 0f), new Vector3(0.08f, 0.002f, 0.004f), Shade(col, 0.7f), 0.3f);
                Stat(lf, R);
            }
        }
    }
}
