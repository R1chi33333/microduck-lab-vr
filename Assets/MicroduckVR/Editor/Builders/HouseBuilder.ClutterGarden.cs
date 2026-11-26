using UnityEngine;

namespace MicroduckVR.EditorTools
{
    public static partial class HouseBuilder
    {
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
