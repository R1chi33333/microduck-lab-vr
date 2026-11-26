using UnityEditor;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
        /// <summary>Cardboard box with flaps seam and packing tape over the top and down the sides.</summary>
        static Transform Carton(string room, Vector3 pos, float yaw, Vector3 size, bool collide = true)
        {
            var r = NewProp(room, "Cardboard box", pos + Vector3.up * size.y / 2f, yaw);
            PBox(r, Vector3.zero, size, "#D9A066", 0.06f);
            PBox(r, new Vector3(0f, size.y / 2f, 0f), new Vector3(size.x + 0.001f, 0.003f, 0.004f), "#A8733F", 0.2f);
            PBox(r, new Vector3(0f, size.y / 2f + 0.001f, 0f), new Vector3(0.06f, 0.002f, size.z + 0.002f), "#F2D49B", 0.2f);
            foreach (var s in new[] { -1f, 1f }) PBox(r, new Vector3(0f, size.y / 2f - 0.04f, s * (size.z / 2f + 0.001f)), new Vector3(0.06f, 0.08f, 0.002f), "#F2D49B", 0.2f);
            PBox(r, new Vector3(size.x * 0.22f, size.y * 0.15f, size.z / 2f + 0.001f), new Vector3(0.05f, 0.04f, 0.002f), Ink, 0.2f);   // "this way up" mark
            return Stat(r, room, collide, "card");
        }

        static void CartonStack(string room, float x, float z, int n, float yaw = 0f)
        {
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = Rf(0.3f, 0.42f) * (1f - i * 0.14f), h = s * Rf(0.65f, 0.85f);
                Carton(room, new Vector3(x + Rf(-0.03f, 0.03f), y, z + Rf(-0.03f, 0.03f)), yaw + Rf(-10f, 10f), new Vector3(s, h, s * Rf(0.8f, 1f)));
                y += h;
            }
        }

        /// <summary>Potted plant: pot with rim, soil, and leaves fanned out from the middle.</summary>
        static Transform Plant(string room, Vector3 pos, float s, string pot = "#E07A4F", bool collide = true)
        {
            var r = NewProp(room, "Plant", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, 0.11f * s, 0f), 0.095f * s, 0.22f * s, pot, default, 0.13f * s, 18);
            PRing(r, new Vector3(0f, 0.22f * s, 0f), 0.14f * s, 0.11f * s, 0.035f * s, Shade(pot, 1.12f));
            PCyl(r, new Vector3(0f, 0.225f * s, 0f), 0.115f * s, 0.01f * s, "#6B3E26", default, -1f, 18);
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * 360f + Rf(-10f, 10f), tilt = Rf(18f, 42f), len = Rf(0.22f, 0.34f) * s;
                var dir = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(tilt, 0f, 0f);
                var c = new Vector3(0f, 0.23f * s, 0f) + dir * Vector3.up * len * 0.5f;
                PBall(r, c, new Vector3(0.075f * s, len, 0.02f * s), i % 2 == 0 ? "#3E9A45" : "#56B65A", dir.eulerAngles);
            }
            return Stat(r, room, collide, "table");
        }

        static Transform FloorLamp(string room, Vector3 pos)
        {
            var r = NewProp(room, "Floor lamp", pos);
            PCyl(r, new Vector3(0f, 0.015f, 0f), 0.14f, 0.03f, "#3D3B4F", default, 0.12f, 20);
            PCyl(r, new Vector3(0f, 0.72f, 0f), 0.012f, 1.38f, "#3D3B4F", default, -1f, 10);
            PCyl(r, new Vector3(0f, 1.4f, 0f), 0.2f, 0.26f, "#FFE45C", default, 0.12f, 22);
            PRing(r, new Vector3(0f, 1.27f, 0f), 0.205f, 0.19f, 0.016f, "#FF8A3D");
            PRing(r, new Vector3(0f, 1.53f, 0f), 0.125f, 0.11f, 0.014f, "#FF8A3D");
            PBall(r, new Vector3(0f, 1.32f, 0f), new Vector3(0.07f, 0.08f, 0.07f), "#FFFDE0");
            var t = Stat(r, room);
            var bc = r.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(0.2f, 1.4f, 0.2f);
            return t;
        }

        static Transform Orange(string room, Vector3 pos, bool apple)
        {
            var r = NewProp(room, apple ? "Apple" : "Orange", pos, Rf(0f, 360f));
            string c = apple ? "#FF5D73" : "#FF8A3D";
            PBall(r, new Vector3(0f, 0.034f, 0f), new Vector3(0.072f, apple ? 0.066f : 0.07f, 0.072f), c);
            PCyl(r, new Vector3(0f, 0.072f, 0f), 0.003f, 0.016f, "#6B3E26", new Vector3(10f, 0f, 0f), -1f, 6);
            PBall(r, new Vector3(0.012f, 0.072f, 0f), new Vector3(0.024f, 0.006f, 0.012f), "#3E9A45", new Vector3(0f, 0f, 20f));
            return Dyn(r, 600f, true);
        }

        static Transform PetBowl(string room, Vector3 pos, string hex, string food)
        {
            var r = NewProp(room, "Pet bowl", pos);
            PCyl(r, new Vector3(0f, 0.022f, 0f), 0.065f, 0.044f, hex, default, 0.085f, 20);
            PRing(r, new Vector3(0f, 0.044f, 0f), 0.086f, 0.07f, 0.006f, Shade(hex, 1.15f));
            PCyl(r, new Vector3(0f, 0.04f, 0f), 0.07f, 0.006f, food, default, -1f, 20);
            for (int i = 0; i < 6; i++) PBall(r, new Vector3(Rf(-0.04f, 0.04f), 0.045f, Rf(-0.04f, 0.04f)), Vector3.one * 0.014f, Shade(food, 0.8f));
            return Dyn(r, 400f);
        }

        static Transform Bone(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Chew bone", pos, yaw);
            PCyl(r, new Vector3(0f, 0.013f, 0f), 0.011f, 0.11f, Cream, new Vector3(0f, 0f, 90f), -1f, 10);
            foreach (var sd in new[] { -1f, 1f }) foreach (var z in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.058f, 0.013f, z * 0.011f), Vector3.one * 0.026f, Cream);
            return Dyn(r, 300f);
        }

        /// <summary>Kettle: tapered body, lid with knob, spout, ring handle.</summary>
        static Transform Kettle(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Kettle", pos, yaw);
            PCyl(r, new Vector3(0f, 0.09f, 0f), 0.09f, 0.18f, hex, default, 0.06f, 20);
            PCyl(r, new Vector3(0f, 0.185f, 0f), 0.06f, 0.012f, Shade(hex, 0.8f), default, 0.05f, 20);
            PBall(r, new Vector3(0f, 0.2f, 0f), new Vector3(0.025f, 0.02f, 0.025f), Ink);
            PCyl(r, new Vector3(0f, 0.1f, 0.09f), 0.016f, 0.09f, hex, new Vector3(50f, 0f, 0f), 0.01f, 10);
            PRing(r, new Vector3(0f, 0.15f, -0.08f), 0.05f, 0.037f, 0.02f, Ink, new Vector3(0f, 0f, 90f));
            PCyl(r, new Vector3(0f, 0.005f, 0f), 0.095f, 0.01f, Ink, default, -1f, 20);
            return Stat(r, room);
        }

        static Transform Toaster(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Toaster", pos, yaw);
            PBox(r, new Vector3(0f, 0.085f, 0f), new Vector3(0.26f, 0.17f, 0.16f), "#B9C3CF", 0.35f, default, 0.04f);
            foreach (var x in new[] { -0.05f, 0.05f })
            {
                PBox(r, new Vector3(x, 0.171f, 0f), new Vector3(0.03f, 0.004f, 0.12f), Ink, 0.4f);
                PBox(r, new Vector3(x, 0.19f, 0f), new Vector3(0.012f, 0.04f, 0.1f), "#D9A35E", 0.3f);   // toast popping up
            }
            PBox(r, new Vector3(0.131f, 0.12f, 0f), new Vector3(0.008f, 0.012f, 0.03f), "#FF5D73", 0.4f);
            return Stat(r, room);
        }

        static Transform CerealBox(string room, Vector3 pos, float yaw, string hex, float h)
        {
            var r = NewProp(room, "Cereal box", pos, yaw);
            PBox(r, new Vector3(0f, h / 2f, 0f), new Vector3(0.19f, h, 0.07f), hex, 0.1f);
            PBox(r, new Vector3(0f, h * 0.82f, 0.036f), new Vector3(0.15f, h * 0.15f, 0.002f), "#FFFFFF", 0.2f);
            PCyl(r, new Vector3(0f, h * 0.38f, 0.036f), 0.055f, 0.002f, "#FFFFFF", new Vector3(90f, 0f, 0f), -1f, 18);
            for (int i = 0; i < 5; i++) PBall(r, new Vector3(Rf(-0.03f, 0.03f), h * 0.4f + Rf(-0.01f, 0.02f), 0.038f), new Vector3(0.016f, 0.016f, 0.004f), "#FFC43D");
            return Stat(r, room);
        }

        static Transform Jar(string room, Vector3 pos, string lid, float h)
        {
            var r = NewProp(room, "Jar", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, h / 2f, 0f), 0.035f, h, "#CFEFF5", default, -1f, 16);
            PCyl(r, new Vector3(0f, h * 0.35f, 0f), 0.031f, h * 0.65f, lid == "#FFC43D" ? "#8B5A2B" : Shade(lid, 1.4f), default, -1f, 16);
            PCyl(r, new Vector3(0f, h + 0.008f, 0f), 0.038f, 0.016f, lid, default, -1f, 16);
            PBox(r, new Vector3(0f, h * 0.55f, 0.035f), new Vector3(0.035f, 0.025f, 0.002f), "#FFFFFF", 0.3f);
            return Stat(r, room);
        }

        static Transform ShoppingBag(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Shopping bag", pos, yaw);
            PBox(r, new Vector3(0f, 0.17f, 0f), new Vector3(0.3f, 0.34f, 0.16f), hex, 0.08f);
            foreach (var x in new[] { -0.06f, 0.06f }) PRing(r, new Vector3(x, 0.36f, 0f), 0.04f, 0.032f, 0.012f, Shade(hex, 0.75f), new Vector3(90f, 0f, 0f));
            PCyl(r, new Vector3(-0.07f, 0.36f, 0.02f), 0.03f, 0.22f, "#D9A35E", new Vector3(0f, 0f, 15f), -1f, 12);                   // baguette
            PCyl(r, new Vector3(0.06f, 0.37f, -0.02f), 0.018f, 0.16f, "#FFFFFF", new Vector3(0f, 0f, -12f), -1f, 10);                 // leek
            PBall(r, new Vector3(0.075f, 0.46f, -0.02f), new Vector3(0.05f, 0.09f, 0.03f), "#56B65A", new Vector3(0f, 0f, -12f));
            PBox(r, new Vector3(0f, 0.2f, 0.081f), new Vector3(0.12f, 0.1f, 0.002f), "#FFFFFF", 0.3f);
            return Stat(r, room, true);
        }

        static Transform Bin(string room, Vector3 pos, string hex)
        {
            var r = NewProp(room, "Trash bin", pos);
            PCyl(r, new Vector3(0f, 0.22f, 0f), 0.15f, 0.44f, hex, default, 0.175f, 22);
            PCyl(r, new Vector3(0f, 0.455f, 0f), 0.18f, 0.03f, Shade(hex, 0.75f), default, 0.17f, 22);
            PBall(r, new Vector3(0f, 0.475f, 0f), new Vector3(0.05f, 0.025f, 0.05f), Shade(hex, 0.75f));
            PBox(r, new Vector3(0f, 0.02f, 0.165f), new Vector3(0.1f, 0.02f, 0.06f), Ink, 0.4f);
            return Stat(r, room, true);
        }

        static Transform WateringCan(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Watering can", pos, yaw);
            PCyl(r, new Vector3(0f, 0.1f, 0f), 0.1f, 0.2f, hex, default, 0.09f, 20);
            PCyl(r, new Vector3(0f, 0.15f, 0.14f), 0.015f, 0.2f, hex, new Vector3(55f, 0f, 0f), 0.012f, 10);
            PCyl(r, new Vector3(0f, 0.22f, 0.22f), 0.022f, 0.03f, Shade(hex, 0.8f), new Vector3(55f, 0f, 0f), 0.03f, 12);
            PRing(r, new Vector3(0f, 0.22f, -0.03f), 0.08f, 0.066f, 0.02f, hex, new Vector3(0f, 0f, 90f));
            return Stat(r, room, true);
        }

        /// <summary>Garden gnome: boots, blue coat with belt, white beard, pink face and nose, tall red hat.</summary>
        static Transform Gnome(string room, Vector3 pos, float yaw)
        {
            var r = NewProp(room, "Garden gnome", pos, yaw);
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.03f, 0.015f, 0.015f), new Vector3(0.04f, 0.03f, 0.06f), "#6B3E26");
            PCyl(r, new Vector3(0f, 0.08f, 0f), 0.07f, 0.13f, "#3A86FF", default, 0.05f, 18);
            PRing(r, new Vector3(0f, 0.07f, 0f), 0.068f, 0.05f, 0.016f, Ink);
            PBox(r, new Vector3(0f, 0.07f, 0.064f), new Vector3(0.022f, 0.018f, 0.006f), Gold, 0.3f);
            PBall(r, new Vector3(0f, 0.15f, 0.035f), new Vector3(0.09f, 0.1f, 0.05f), "#FFFFFF");
            PBall(r, new Vector3(0f, 0.18f, 0.01f), new Vector3(0.07f, 0.065f, 0.065f), "#FFE2BF");
            PBall(r, new Vector3(0f, 0.178f, 0.045f), new Vector3(0.022f, 0.02f, 0.02f), "#FF8A9A");
            foreach (var sd in new[] { -1f, 1f }) PBall(r, new Vector3(sd * 0.015f, 0.19f, 0.04f), new Vector3(0.01f, 0.012f, 0.006f), Ink);
            PCyl(r, new Vector3(0f, 0.25f, -0.005f), 0.045f, 0.13f, "#FF5D73", new Vector3(-8f, 0f, 0f), 0.004f, 16);
            return Stat(r, room, true);
        }

        static Transform Bucket(string room, Vector3 pos, string hex)
        {
            var r = NewProp(room, "Bucket", pos, Rf(0f, 360f));
            PCyl(r, new Vector3(0f, 0.09f, 0f), 0.09f, 0.18f, hex, default, 0.115f, 20);
            PRing(r, new Vector3(0f, 0.18f, 0f), 0.12f, 0.105f, 0.01f, Shade(hex, 1.15f));
            PRing(r, new Vector3(0f, 0.2f, 0f), 0.11f, 0.102f, 0.006f, "#B9C3CF", new Vector3(0f, 0f, 90f));
            PCyl(r, new Vector3(0f, 0.17f, 0f), 0.1f, 0.01f, "#E8C07A", default, -1f, 20);     // sand
            return Stat(r, room, true);
        }

        static Transform Spade(string room, Vector3 pos, float yaw, string hex)
        {
            var r = NewProp(room, "Spade", pos, yaw);
            PBox(r, new Vector3(0f, 0.01f, 0.08f), new Vector3(0.07f, 0.008f, 0.09f), hex, 0.4f);
            PCyl(r, new Vector3(0f, 0.012f, -0.06f), 0.007f, 0.2f, "#FFC43D", new Vector3(90f, 0f, 0f), -1f, 8);
            PBox(r, new Vector3(0f, 0.012f, -0.165f), new Vector3(0.05f, 0.014f, 0.016f), "#FFC43D", 0.4f);
            return Dyn(r, 300f);
        }
    }
}
