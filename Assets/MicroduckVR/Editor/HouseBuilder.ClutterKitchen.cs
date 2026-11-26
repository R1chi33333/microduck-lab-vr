using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
    }
}
