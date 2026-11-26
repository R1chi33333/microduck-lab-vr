using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
    }
}
