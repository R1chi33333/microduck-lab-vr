using MicroduckVR.Core;
using MicroduckVR.Duck;
using MicroduckVR.World;
using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
                SetLayerAll(car, Layers.Prop, false);
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
    }
}
