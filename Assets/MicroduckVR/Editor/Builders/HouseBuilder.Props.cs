using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
        // ================================================================== props
        /// <summary>Hardback book: covers, spine with bands and a title label, cream page block. Spine faces local -z.
        /// pos = bottom centre when standing; lying books rest on their side.</summary>
        static Transform Book(string room, Vector3 pos, float yaw, float t, float h, float d, string hex, bool lying, bool dynamic)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f) * (lying ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity);
            var r = NewProp(room, "Book", pos + Vector3.up * (lying ? t / 2f : h / 2f), rot);
            float c = Mathf.Min(0.004f, t * 0.2f);
            PBox(r, new Vector3(-t / 2f + c / 2f, 0f, 0f), new Vector3(c, h, d), hex, 0.4f);
            PBox(r, new Vector3(t / 2f - c / 2f, 0f, 0f), new Vector3(c, h, d), hex, 0.4f);
            PBox(r, new Vector3(0f, 0f, -d / 2f + c / 2f), new Vector3(t, h, c), hex, 0.4f);
            PBox(r, new Vector3(0f, 0f, c * 0.5f), new Vector3(t - c * 1.6f, h - 0.008f, d - c * 1.5f), Paper, 0.15f);
            string band = crng.Next(2) == 0 ? Gold : Shade(hex, 0.6f);
            foreach (var s in new[] { -1f, 1f }) PBox(r, new Vector3(0f, s * h * 0.36f, -d / 2f - 0.0006f), new Vector3(t * 0.86f, 0.007f, 0.002f), band, 0.2f);
            PBox(r, new Vector3(0f, h * 0.06f, -d / 2f - 0.0006f), new Vector3(t * 0.62f, h * 0.26f, 0.002f), crng.Next(2) == 0 ? Cream : Shade(hex, 0.55f), 0.3f);
            if (h > 0.1f) PBox(r, new Vector3(0f, h * 0.06f, -d / 2f - 0.0012f), new Vector3(t * 0.3f, h * 0.16f, 0.002f), Ink, 0.2f);   // the "title"
            return dynamic ? Dyn(r, 400f) : Stat(r, room);
        }

        static void BookRow(string room, Vector3 start, Vector3 along, float yaw, int n, bool dynamic, float maxH = 0.25f)
        {
            float o = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = Rf(0.028f, 0.05f), h = Rf(maxH * 0.68f, maxH), d = Rf(0.13f, 0.17f);
                Book(room, start + along * (o + t / 2f), yaw + Rf(-3f, 3f), t, h, d, AnyColour(), false, dynamic);
                o += t + 0.003f;
            }
        }

        static void BookStack(string room, float x, float y, float z, int n, bool dynamic)
        {
            float yy = y;
            for (int i = 0; i < n; i++)
            {
                float t = Rf(0.025f, 0.045f);
                Book(room, new Vector3(x + Rf(-0.015f, 0.015f), yy, z + Rf(-0.015f, 0.015f)), Rf(-30f, 30f), t, Rf(0.17f, 0.23f), Rf(0.12f, 0.16f), AnyColour(), true, dynamic);
                yy += t + 0.001f;
            }
        }

        /// <summary>Sitting cartoon teddy: big head, button eyes with shines, muzzle and nose, inner ears, belly patch,
        /// stubby arms, legs out front with paw pads, and a bow. s = 1 is about 30 cm tall.</summary>
        static Transform Teddy(string room, Vector3 pos, float yaw, float s, string fur = "#C98A55", string bow = "#FF5D73", bool dynamic = true)
        {
            var r = NewProp(room, "Teddy", pos, yaw);
            string light = "#FFE2BF";
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBall(r, V(0f, 0.11f, 0f), V(0.2f, 0.21f, 0.17f), fur);                        // body
            PBall(r, V(0f, 0.105f, 0.055f), V(0.12f, 0.13f, 0.07f), light);                 // belly
            PBall(r, V(0f, 0.285f, 0.005f), V(0.18f, 0.16f, 0.155f), fur);                  // head
            PBall(r, V(0f, 0.262f, 0.07f), V(0.085f, 0.062f, 0.06f), light);                // muzzle
            PBall(r, V(0f, 0.28f, 0.098f), V(0.036f, 0.024f, 0.022f), Ink);                 // nose
            PBox(r, V(0f, 0.251f, 0.1f), V(0.004f, 0.016f, 0.004f), Ink, 0.4f);            // philtrum
            foreach (var sd in new[] { -1f, 1f })
            {
                PBox(r, V(sd * 0.011f, 0.244f, 0.097f), V(0.018f, 0.004f, 0.004f), Ink, 0.4f, new Vector3(0f, 0f, sd * 25f));   // smile
                PBall(r, V(sd * 0.04f, 0.307f, 0.07f), V(0.026f, 0.03f, 0.018f), Ink);      // eye
                PBall(r, V(sd * 0.04f + 0.006f, 0.315f, 0.078f), V(0.009f, 0.009f, 0.006f), "#FFFFFF");   // shine
                PBall(r, V(sd * 0.072f, 0.36f, -0.005f), V(0.062f, 0.062f, 0.034f), fur);   // ear
                PBall(r, V(sd * 0.072f, 0.358f, 0.008f), V(0.036f, 0.036f, 0.016f), light);
                PBall(r, V(sd * 0.105f, 0.15f, 0.035f), V(0.058f, 0.115f, 0.058f), fur, new Vector3(-20f, 0f, sd * 35f));   // arm
                PBall(r, V(sd * 0.058f, 0.04f, 0.08f), V(0.072f, 0.07f, 0.13f), fur);       // leg
                PBall(r, V(sd * 0.058f, 0.042f, 0.142f), V(0.05f, 0.05f, 0.012f), light);   // paw pad
                PBox(r, V(sd * 0.022f, 0.208f, 0.075f), V(0.04f, 0.026f, 0.016f), bow, 0.45f, new Vector3(0f, 0f, sd * 18f));   // bow
            }
            PBall(r, V(0f, 0.208f, 0.083f), V(0.018f, 0.02f, 0.016f), Shade(bow, 0.8f));
            return dynamic ? Dyn(r, 60f) : Stat(r, room);
        }

        /// <summary>Plush bunny: long ears with pink insides, round cheeks, button eyes, cotton tail.</summary>
        static Transform Bunny(string room, Vector3 pos, float yaw, float s, string fur = "#FFFFFF")
        {
            var r = NewProp(room, "Plush bunny", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBall(r, V(0f, 0.08f, 0f), V(0.14f, 0.15f, 0.13f), fur);
            PBall(r, V(0f, 0.19f, 0.01f), V(0.13f, 0.12f, 0.11f), fur);
            PBall(r, V(0f, 0.05f, -0.07f), V(0.05f, 0.05f, 0.05f), fur);
            PBall(r, V(0f, 0.18f, 0.065f), V(0.022f, 0.016f, 0.012f), "#FF8A9A");
            foreach (var sd in new[] { -1f, 1f })
            {
                PBall(r, V(sd * 0.03f, 0.31f, 0f), V(0.042f, 0.15f, 0.026f), fur, new Vector3(0f, 0f, -sd * 12f));
                PBall(r, V(sd * 0.031f, 0.31f, 0.01f), V(0.022f, 0.11f, 0.01f), "#FF8A9A", new Vector3(0f, 0f, -sd * 12f));
                PBall(r, V(sd * 0.032f, 0.205f, 0.05f), V(0.018f, 0.022f, 0.012f), Ink);
                PBall(r, V(sd * 0.045f, 0.175f, 0.045f), V(0.024f, 0.014f, 0.01f), "#FFC2CC");
                PBall(r, V(sd * 0.045f, 0.03f, 0.06f), V(0.05f, 0.04f, 0.07f), fur);
            }
            return Dyn(r, 60f);
        }

        /// <summary>Football: white ball with 12 black pentagon patches (icosahedron directions). Bouncy and kickable.</summary>
        static Transform Football(string room, Vector3 pos, float radius)
        {
            var r = NewProp(room, "Football", pos + Vector3.up * radius, Quaternion.Euler(Rf(0f, 360f), Rf(0f, 360f), 0f));
            PBall(r, Vector3.zero, Vector3.one * radius * 2f, "#FFFFFF");
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            var dirs = new List<Vector3>();
            foreach (var a in new[] { -1f, 1f }) foreach (var b in new[] { -phi, phi })
                { dirs.Add(new Vector3(0f, a, b)); dirs.Add(new Vector3(a, b, 0f)); dirs.Add(new Vector3(b, 0f, a)); }
            foreach (var d in dirs)
            {
                var n = d.normalized;
                PCyl(r, n * (radius - 0.0008f), radius * 0.36f, 0.004f, Ink, Quaternion.FromToRotation(Vector3.up, n).eulerAngles, -1f, 5);
            }
            return Dyn(r, 40f, true, bouncy, 0.04f);
        }

        /// <summary>Bouncy ball with a contrasting stripe around it.</summary>
        static Transform StripeBall(string room, Vector3 pos, float d, string hex, string stripe)
        {
            var r = NewProp(room, "Ball", pos + Vector3.up * d / 2f, Quaternion.Euler(Rf(-40f, 40f), Rf(0f, 360f), Rf(-40f, 40f)));
            PBall(r, Vector3.zero, Vector3.one * d, hex);
            PRing(r, Vector3.zero, d * 0.505f, d * 0.4f, d * 0.22f, stripe);
            return Dyn(r, 80f, true, bouncy);
        }

        /// <summary>2x4 brick with studs.</summary>
        static Transform Lego(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Lego brick", pos, yaw);
            PBox(r, new Vector3(0f, 0.0095f, 0f), new Vector3(0.032f, 0.019f, 0.064f), hex, 0.15f);
            for (int i = 0; i < 2; i++) for (int j = 0; j < 4; j++)
                PCyl(r, new Vector3(-0.008f + i * 0.016f, 0.0205f, -0.024f + j * 0.016f), 0.0048f, 0.0034f, Shade(hex, 1.08f), default, -1f, 12);
            return Dyn(r, 300f);
        }

        /// <summary>Alphabet block: coloured cube with raised panels in a contrasting colour, a simple letter shape on each.</summary>
        static Transform Block(string room, Vector3 pos, float yaw, float s, string hex)
        {
            var r = NewProp(room, "Toy block", pos + Vector3.up * s / 2f, yaw);
            PBox(r, Vector3.zero, Vector3.one * s, hex, 0.18f);
            string panel = hex == Cream ? "#FF5D73" : Cream, letter = AnyColour();
            if (letter == panel) letter = Ink;
            foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up })
            {
                var rot = Quaternion.LookRotation(n, Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up).eulerAngles;
                var pnl = PBox(r, n * (s / 2f), new Vector3(s * 0.72f, s * 0.72f, 0.003f), panel, 0.2f, rot);
                int kind = crng.Next(3);
                if (kind == 0) { PBox(pnl, new Vector3(0f, 0f, 0.002f), new Vector3(s * 0.1f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(0f, 0f, 0.002f), new Vector3(s * 0.4f, s * 0.1f, 0.002f), letter, 0.3f); }
                else if (kind == 1) PRing(pnl, new Vector3(0f, 0f, 0.002f), s * 0.2f, s * 0.11f, 0.002f, letter, new Vector3(90f, 0f, 0f));
                else { PBox(pnl, new Vector3(-s * 0.12f, 0f, 0.002f), new Vector3(s * 0.09f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(s * 0.12f, 0f, 0.002f), new Vector3(s * 0.09f, s * 0.46f, 0.002f), letter, 0.3f); PBox(pnl, new Vector3(0f, s * 0.19f, 0.002f), new Vector3(s * 0.33f, s * 0.08f, 0.002f), letter, 0.3f); }
            }
            return Dyn(r, 250f);
        }

        /// <summary>Crayon lying down: wax body, paper wrapper with two colour stripes, pointed tip.</summary>
        static Transform Crayon(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Crayon", pos + Vector3.up * 0.0065f, yaw);
            var e = new Vector3(90f, 0f, 0f);
            PCyl(r, new Vector3(0f, 0f, -0.006f), 0.006f, 0.07f, hex, e, -1f, 10);
            PCyl(r, new Vector3(0f, 0f, -0.01f), 0.0066f, 0.044f, "#F3EBD7", e, -1f, 10);
            foreach (var z in new[] { -0.027f, 0.007f }) PCyl(r, new Vector3(0f, 0f, z), 0.0068f, 0.004f, hex, e, -1f, 10);
            PCyl(r, new Vector3(0f, 0f, 0.0365f), 0.006f, 0.015f, hex, e, 0.0012f, 10);
            return Dyn(r, 600f);
        }

        /// <summary>Toy car: body, cabin with windows, four wheels with hubcaps, headlights and a bumper.</summary>
        static Transform ToyCar(string room, Vector3 pos, float yaw, string hex, float s = 1f)
        {
            var r = NewProp(room, "Toy car", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBox(r, V(0f, 0.024f, 0f), V(0.07f, 0.024f, 0.125f), hex, 0.4f);
            PBox(r, V(0f, 0.046f, -0.012f), V(0.058f, 0.026f, 0.062f), hex, 0.45f);
            PBox(r, V(0f, 0.047f, 0.02f), V(0.05f, 0.018f, 0.003f), "#9AD8F2", 0.3f, new Vector3(-25f, 0f, 0f));
            PBox(r, V(0f, 0.047f, -0.044f), V(0.05f, 0.017f, 0.003f), "#9AD8F2", 0.3f);
            foreach (var sd in new[] { -1f, 1f })
            {
                PBox(r, V(sd * 0.0295f, 0.047f, -0.012f), V(0.003f, 0.016f, 0.05f), "#9AD8F2", 0.3f);
                foreach (var z in new[] { -0.038f, 0.04f })
                {
                    PCyl(r, V(sd * 0.034f, 0.014f, z), 0.014f * s, 0.012f * s, Ink, new Vector3(0f, 0f, 90f), -1f, 14);
                    PCyl(r, V(sd * 0.0405f, 0.014f, z), 0.0065f * s, 0.002f * s, "#B9C3CF", new Vector3(0f, 0f, 90f), -1f, 10);
                }
                PBox(r, V(sd * 0.022f, 0.027f, 0.063f), V(0.014f, 0.008f, 0.003f), "#FFE45C", 0.4f);
                PBox(r, V(sd * 0.024f, 0.027f, -0.063f), V(0.012f, 0.007f, 0.003f), "#FF5D73", 0.4f);
            }
            PBox(r, V(0f, 0.015f, 0.064f), V(0.072f, 0.008f, 0.006f), "#B9C3CF", 0.5f);
            return Dyn(r, 200f);
        }

        /// <summary>Trainer: sole, upper, toe cap, laces, heel tab.</summary>
        static Transform Shoe(string room, Vector3 pos, float yaw, string hex, float s = 1f)
        {
            var r = NewProp(room, "Shoe", pos, yaw);
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z) * s;
            PBox(r, V(0f, 0.012f, 0f), V(0.1f, 0.024f, 0.27f), "#FFFFFF", 0.5f);
            PBox(r, V(0f, 0.05f, -0.03f), V(0.092f, 0.06f, 0.19f), hex, 0.55f, default, 0.08f);
            PBall(r, V(0f, 0.038f, 0.075f), V(0.092f, 0.055f, 0.11f), hex);
            PBall(r, V(0f, 0.03f, 0.105f), V(0.084f, 0.035f, 0.05f), "#FFFFFF");
            PBox(r, V(0f, 0.078f, 0.01f), V(0.05f, 0.012f, 0.09f), Shade(hex, 0.8f), 0.5f, new Vector3(-12f, 0f, 0f));
            for (int i = 0; i < 3; i++) PBox(r, V(0f, 0.084f + i * 0.003f, 0.035f - i * 0.025f), V(0.05f, 0.004f, 0.007f), "#FFFFFF", 0.5f, new Vector3(-12f, 0f, 0f));
            PBox(r, V(0f, 0.075f, -0.123f), V(0.03f, 0.03f, 0.012f), Shade(hex, 0.7f), 0.5f);
            return Dyn(r, 150f);
        }

        static void ShoePair(string room, float x, float z, string hex, float yaw, float s = 1f)
        {
            Shoe(room, new Vector3(x, 0f, z), yaw, hex, s);
            Shoe(room, new Vector3(x + Mathf.Cos(yaw * Mathf.Deg2Rad) * 0.12f * s, 0f, z - Mathf.Sin(yaw * Mathf.Deg2Rad) * 0.12f * s + 0.03f), yaw + Rf(5f, 25f), hex, s);
        }

        /// <summary>Sock: tube with a cuff, a heel and a toe in a contrasting colour, stripes.</summary>
        static Transform Sock(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Sock", pos, yaw);
            PBox(r, new Vector3(0f, 0.012f, 0f), new Vector3(0.06f, 0.024f, 0.2f), hex, 0.45f);
            PBall(r, new Vector3(0f, 0.012f, 0.095f), new Vector3(0.062f, 0.026f, 0.04f), "#FFFFFF");
            PBall(r, new Vector3(0f, 0.012f, -0.04f), new Vector3(0.062f, 0.026f, 0.04f), "#FFFFFF");
            foreach (var z in new[] { -0.075f, -0.09f }) PBox(r, new Vector3(0f, 0.012f, z), new Vector3(0.063f, 0.027f, 0.006f), Shade(hex, 0.6f), 0.4f);
            return Dyn(r, 60f);
        }

        /// <summary>Mug: tapered cup, ring handle, coffee inside, a stripe.</summary>
        static Transform Mug(string room, Vector3 pos, float yaw, string hex, bool dynamic = true)
        {
            var r = NewProp(room, "Mug", pos, yaw);
            PCyl(r, new Vector3(0f, 0.045f, 0f), 0.038f, 0.09f, hex, default, 0.042f, 18);
            PCyl(r, new Vector3(0f, 0.087f, 0f), 0.037f, 0.004f, "#6B3E26", default, -1f, 18);
            PRing(r, new Vector3(0f, 0.09f, 0f), 0.042f, 0.036f, 0.004f, Shade(hex, 1.1f));
            PRing(r, new Vector3(0f, 0.05f, 0f), 0.0415f, 0.036f, 0.012f, "#FFFFFF");
            PRing(r, new Vector3(0.048f, 0.046f, 0f), 0.026f, 0.017f, 0.012f, hex, new Vector3(90f, 0f, 0f));
            return dynamic ? Dyn(r, 300f) : Stat(r, room);
        }

        /// <summary>Open laptop: base with keyboard and trackpad, tilted lid with a bright screen and a sticker.</summary>
        static Transform Laptop(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Laptop", pos, yaw);
            PBox(r, new Vector3(0f, 0.009f, 0f), new Vector3(0.34f, 0.018f, 0.24f), "#B9C3CF", 0.3f);
            PBox(r, new Vector3(0f, 0.0185f, -0.025f), new Vector3(0.3f, 0.002f, 0.11f), Ink, 0.3f);
            for (int i = 0; i < 4; i++) PBox(r, new Vector3(0f, 0.0195f, -0.07f + i * 0.026f), new Vector3(0.28f, 0.0015f, 0.003f), "#3D3B4F", 0.3f);
            PBox(r, new Vector3(0f, 0.0185f, 0.075f), new Vector3(0.1f, 0.002f, 0.06f), "#97A0AE", 0.3f);
            var lid = PBox(r, new Vector3(0f, 0.018f + Mathf.Cos(15f * Mathf.Deg2Rad) * 0.115f, -0.12f - Mathf.Sin(15f * Mathf.Deg2Rad) * 0.115f), new Vector3(0.34f, 0.23f, 0.012f), "#B9C3CF", 0.3f, new Vector3(-15f, 0f, 0f));
            PBox(lid, new Vector3(0f, 0.006f, 0.0065f), new Vector3(0.3f, 0.18f, 0.002f), "#3A86FF", 0.2f);
            PBall(lid, new Vector3(0.06f, 0.03f, 0.0075f), new Vector3(0.07f, 0.07f, 0.002f), "#FFE45C");      // sun on the wallpaper
            PBox(lid, new Vector3(0f, -0.05f, 0.0075f), new Vector3(0.3f, 0.06f, 0.002f), "#8FD35A", 0.2f);    // hills
            PBall(lid, new Vector3(0.05f, 0.03f, -0.007f), new Vector3(0.05f, 0.05f, 0.002f), "#FF5D73");     // sticker
            return Dyn(r, 600f);
        }

        /// <summary>Game pad: body, two grips, d-pad, four coloured buttons, two sticks.</summary>
        static Transform GamePad(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Game pad", pos, yaw);
            PBox(r, new Vector3(0f, 0.013f, 0f), new Vector3(0.12f, 0.024f, 0.06f), "#3D3B4F", 0.5f);
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.05f, 0.012f, -0.022f), new Vector3(0.042f, 0.024f, 0.055f), "#3D3B4F");
            PBox(r, new Vector3(-0.035f, 0.026f, 0.008f), new Vector3(0.022f, 0.004f, 0.007f), Ink, 0.3f);
            PBox(r, new Vector3(-0.035f, 0.026f, 0.008f), new Vector3(0.007f, 0.004f, 0.022f), Ink, 0.3f);
            string[] bc = { "#FF5D73", "#3A86FF", "#8FD35A", "#FFC43D" };
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI / 2f; PCyl(r, new Vector3(0.035f + Mathf.Cos(a) * 0.009f, 0.026f, 0.008f + Mathf.Sin(a) * 0.009f), 0.0042f, 0.004f, bc[i], default, -1f, 10); }
            foreach (var sd in new[] { -1f, 1f }) PCyl(r, new Vector3(sd * 0.016f, 0.027f, -0.012f), 0.008f, 0.006f, Ink, default, 0.007f, 12);
            return Dyn(r, 300f);
        }

        static Transform Remote(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Remote", pos, yaw);
            PBox(r, new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.18f), "#3D3B4F", 0.5f);
            PCyl(r, new Vector3(0f, 0.021f, 0.065f), 0.007f, 0.004f, "#FF5D73", default, -1f, 10);
            for (int i = 0; i < 3; i++) for (int j = 0; j < 4; j++) PBox(r, new Vector3(-0.012f + i * 0.012f, 0.021f, 0.035f - j * 0.016f), new Vector3(0.008f, 0.003f, 0.008f), "#B9C3CF", 0.4f);
            return Dyn(r, 400f);
        }

        static Transform Magazine(string room, Vector3 pos, float yaw, string hex, bool dynamic = true)
        {
            var r = NewProp(room, "Magazine", pos, yaw);
            PBox(r, new Vector3(0f, 0.004f, 0f), new Vector3(0.21f, 0.008f, 0.28f), hex, 0.2f);
            PBox(r, new Vector3(0f, 0.0085f, 0.1f), new Vector3(0.17f, 0.002f, 0.04f), "#FFFFFF", 0.3f);
            PBox(r, new Vector3(0.02f, 0.0085f, -0.03f), new Vector3(0.12f, 0.002f, 0.13f), Shade(hex, 1.5f), 0.3f);
            PBall(r, new Vector3(0.02f, 0.009f, -0.03f), new Vector3(0.06f, 0.002f, 0.06f), AnyColour());
            return dynamic ? Dyn(r, 600f) : Stat(r, room);
        }
    }
}
