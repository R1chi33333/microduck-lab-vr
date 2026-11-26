using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
    }
}
