using UnityEngine;

namespace MicroduckVR.EditorTools
{
    /// <summary>
    /// A lived-in house: lots of furniture and everyday clutter per room, in the hand-drawn style.
    /// Coordinates here are Unity space (already mirrored), y = bottom of the object.
    /// Doorways and the main walking lanes are kept clear.
    /// </summary>
    public static partial class HouseBuilder
    {
        static System.Random crng;

        // ------------------------------------------------------------------ Unity-space helpers
        static Material C(string hex) => M("c_" + hex.TrimStart('#'), hex);
        static float Rf(float a, float b) => a + (float)crng.NextDouble() * (b - a);

        /// <summary>Hand-drawn box: (x, z) centre, y bottom.</summary>
        static GameObject UB(string room, string name, float x, float y, float z, float w, float h, float d, string hex, float yaw = 0f, bool collide = false, string surface = null)
            => Box(room, name, new Vector3(x, y + h / 2f, z), new Vector3(w, h, d), C(hex), -yaw, surface, collide);

        static GameObject UC(string room, string name, float x, float y, float z, float r, float h, string hex, bool collide = false, string surface = null)
            => Cyl(room, name, r, h, C(hex), -x, y + h / 2f, z, surface, collide);

        static GameObject Ball(string room, string name, float x, float y, float z, float sx, float sy, float sz, string hex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name; go.layer = MicroduckVR.Duck.DuckSetup.LayerWorld; go.isStatic = true;
            go.transform.SetParent(rooms[room], false);
            go.transform.position = new Vector3(x, y + sy / 2f, z);
            go.transform.localScale = new Vector3(sx, sy, sz);
            go.GetComponent<Renderer>().sharedMaterial = C(hex);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static readonly string[] Bright = { "#FF5D73", "#FFC43D", "#3A86FF", "#1FA6A0", "#7B61FF", "#FF8A3D", "#8FD35A", "#F2F2F2" };
        static string AnyColour() => Bright[crng.Next(Bright.Length)];

        // ------------------------------------------------------------------ composite props
        static void Books(string room, float x, float y, float z, int n, bool alongX, float yaw = 0f)
        {
            float o = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = Rf(0.03f, 0.05f), h = Rf(0.17f, 0.25f), d = Rf(0.13f, 0.17f);
                float bx = alongX ? x + o : x, bz = alongX ? z : z + o;
                UB(room, "Book", bx, y, bz, alongX ? t : d, h, alongX ? d : t, AnyColour(), yaw + Rf(-4f, 4f));
                o += t + 0.004f;
            }
        }

        static void BookPile(string room, float x, float y, float z, int n)
        {
            float yy = y;
            for (int i = 0; i < n; i++) { float h = Rf(0.025f, 0.045f); UB(room, "Book pile", x + Rf(-0.02f, 0.02f), yy, z + Rf(-0.02f, 0.02f), Rf(0.16f, 0.22f), h, Rf(0.12f, 0.16f), AnyColour(), Rf(-25f, 25f)); yy += h; }
        }

        static void Plant(string room, float x, float z, float s = 1f, float y = 0f, bool collide = true)
        {
            UC(room, "Pot", x, y, z, 0.13f * s, 0.24f * s, "#E07A4F", collide, "table");
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f + Rf(0f, 0.5f);
                Ball(room, "Leaf", x + Mathf.Cos(a) * 0.07f * s, y + 0.22f * s, z + Mathf.Sin(a) * 0.07f * s, 0.09f * s, Rf(0.22f, 0.34f) * s, 0.09f * s, i % 2 == 0 ? "#3E9A45" : "#56B65A");
            }
        }

        static void FloorLamp(string room, float x, float z)
        {
            UC(room, "Lamp base", x, 0f, z, 0.14f, 0.03f, "#3D3B4F", true);
            UC(room, "Lamp pole", x, 0.03f, z, 0.012f, 1.35f, "#3D3B4F", true);
            var shade = UC(room, "Lamp shade", x, 1.35f, z, 0.2f, 0.26f, "#FFE45C");
            shade.transform.localScale = new Vector3(1f, 1f, 1f);
        }

        static void Teddy(string room, float x, float y, float z, float s, string hex = "#C98A55")
        {
            Ball(room, "Teddy body", x, y, z, 0.18f * s, 0.2f * s, 0.15f * s, hex);
            Ball(room, "Teddy head", x, y + 0.17f * s, z, 0.14f * s, 0.13f * s, 0.13f * s, hex);
            Ball(room, "Teddy ear", x - 0.05f * s, y + 0.27f * s, z, 0.05f * s, 0.05f * s, 0.03f * s, hex);
            Ball(room, "Teddy ear", x + 0.05f * s, y + 0.27f * s, z, 0.05f * s, 0.05f * s, 0.03f * s, hex);
            Ball(room, "Teddy snout", x, y + 0.2f * s, z + 0.06f * s, 0.06f * s, 0.045f * s, 0.04f * s, "#FFE2BF");
        }

        static void Shoes(string room, float x, float z, string hex, float yaw)
        {
            UB(room, "Shoe", x, 0f, z, 0.1f, 0.07f, 0.26f, hex, yaw);
            UB(room, "Shoe", x + 0.13f, 0f, z + 0.03f, 0.1f, 0.07f, 0.26f, hex, yaw + 12f);
        }

        static void ToyScatter(string room, float x0, float x1, float z0, float z1, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float x = Rf(x0, x1), z = Rf(z0, z1);
                switch (crng.Next(5))
                {
                    case 0: UB(room, "Toy block", x, 0f, z, 0.05f, 0.05f, 0.05f, AnyColour(), Rf(0f, 90f)); break;
                    case 1: Ball(room, "Toy ball", x, 0f, z, 0.08f, 0.08f, 0.08f, AnyColour()); break;
                    case 2: UB(room, "Crayon", x, 0f, z, 0.012f, 0.012f, 0.09f, AnyColour(), Rf(0f, 180f)); break;
                    case 3: UB(room, "Toy car", x, 0.012f, z, 0.07f, 0.035f, 0.12f, AnyColour(), Rf(0f, 180f)); UC(room, "Wheel", x, 0f, z, 0.015f, 0.012f, "#3D3B4F"); break;
                    default: UB(room, "Lego", x, 0f, z, 0.032f, 0.02f, 0.064f, AnyColour(), Rf(0f, 180f)); break;
                }
            }
        }

        static void CardboardBoxes(string room, float x, float z, int n)
        {
            float y = 0f;
            for (int i = 0; i < n; i++) { float s = Rf(0.28f, 0.4f) * (1f - i * 0.12f); UB(room, "Cardboard box", x + Rf(-0.03f, 0.03f), y, z + Rf(-0.03f, 0.03f), s, s * 0.8f, s, "#D9A066", Rf(-10f, 10f), true, "card"); y += s * 0.8f; }
        }

        // ------------------------------------------------------------------ rooms
        static void Clutter()
        {
            crng = new System.Random(2027);
            LivingClutter(); KitchenClutter(); BedroomClutter(); HallwayClutter(); GardenClutter();
        }

        static void LivingClutter()
        {
            const string R = "living";
            // TV corner on the hallway wall (south), left of the doorway
            UB(R, "TV console", -1.15f, 0f, 2.28f, 1.3f, 0.42f, 0.36f, "#7B61FF", 0f, true, "table");
            UB(R, "TV", -1.15f, 0.42f, 2.36f, 0.95f, 0.56f, 0.06f, "#2A2B2E");
            UB(R, "TV screen", -1.15f, 0.47f, 2.32f, 0.86f, 0.46f, 0.02f, "#3A86FF");
            UB(R, "Game console", -0.75f, 0.42f, 2.22f, 0.22f, 0.05f, 0.16f, "#F2F2F2");
            UB(R, "Game pad", -0.6f, 0f, 1.9f, 0.14f, 0.035f, 0.09f, "#3D3B4F", 30f);
            Books(R, -1.75f, 0.05f, 2.24f, 7, true);
            Plant(R, -2.0f, 2.25f, 1.1f, 0.42f, false);
            // reading corner on the east side: bookcase, floor lamp, side table with a mug, piles of books
            UB(R, "Bookcase", 2.78f, 0f, 1.55f, 0.36f, 1.6f, 1.1f, "#FFF6E4", 0f, true);
            for (int s = 0; s < 4; s++)
            {
                UB(R, "Shelf board", 2.74f, 0.32f + s * 0.38f, 1.55f, 0.32f, 0.025f, 1.04f, "#E8D8BE");
                Books(R, 2.74f, 0.345f + s * 0.38f, 1.08f, 9 - s, false, 90f);
            }
            FloorLamp(R, 0.3f, -2.25f);
            UC(R, "Side table", 0.3f, 0f, -1.75f, 0.2f, 0.5f, "#FFC43D", true, "table");
            UC(R, "Mug", 0.3f, 0.5f, -1.75f, 0.04f, 0.09f, "#FF5D73");
            BookPile(R, 2.35f, 0f, 1.95f, 5);
            BookPile(R, 1.1f, 0f, -0.55f, 3);
            // magazines and remote on the coffee table, cushions thrown on the floor by the sofa
            UB(R, "Magazine", 1.5f, 0.4f, -1.0f, 0.22f, 0.008f, 0.28f, "#FF5D73", 12f);
            UB(R, "Magazine", 1.85f, 0.4f, -0.95f, 0.22f, 0.008f, 0.28f, "#1FA6A0", -20f);
            UB(R, "Remote", 1.3f, 0.4f, -1.1f, 0.05f, 0.02f, 0.18f, "#3D3B4F", 40f);
            UB(R, "Floor cushion", 2.45f, 0f, -0.45f, 0.42f, 0.14f, 0.42f, "#FFC43D", 25f, true, "rug");
            UB(R, "Floor cushion", 2.8f, 0f, -0.2f, 0.4f, 0.13f, 0.4f, "#FF8A3D", -15f, true, "rug");
            // the dog's corner under the window: basket, bone, bowl
            UC(R, "Pet basket", -1.9f, 0f, -2.05f, 0.34f, 0.12f, "#C98A55", true, "rug");
            UC(R, "Pet cushion", -1.9f, 0.04f, -2.05f, 0.28f, 0.06f, "#FF8A9A");
            UB(R, "Chew bone", -1.55f, 0f, -1.7f, 0.14f, 0.03f, 0.04f, "#FFF6E4", 35f);
            UC(R, "Pet bowl", -1.35f, 0f, -2.15f, 0.08f, 0.04f, "#3A86FF");
            // a laundry basket and stray socks, shoes near the hall door
            UC(R, "Laundry basket", -2.6f, 0f, 0.55f, 0.2f, 0.3f, "#8FD3B6", true);
            Ball(R, "Laundry", -2.6f, 0.26f, 0.55f, 0.34f, 0.14f, 0.3f, "#FFF6E4");
            UB(R, "Sock", -2.25f, 0f, 0.8f, 0.05f, 0.02f, 0.18f, "#FF5D73", 50f);
            UB(R, "Sock", 0.35f, 0f, 0.95f, 0.05f, 0.02f, 0.18f, "#3A86FF", -20f);
            Shoes(R, 0.75f, 2.2f, "#FFC43D", 10f);
            CardboardBoxes(R, -2.65f, 2.2f, 2);
            // toys left on the rug and under the table
            ToyScatter(R, 1.1f, 2.7f, 1.1f, 2.1f, 10);
            ToyScatter(R, -0.6f, 0.6f, -0.35f, 0.35f, 7);
            Teddy(R, 0.45f, 0f, 0.25f, 0.9f);
            // things on the big table
            UB(R, "Laptop", 0.25f, 0.75f, -0.25f, 0.34f, 0.02f, 0.24f, "#97A0AE", -10f);
            UC(R, "Mug", -0.55f, 0.75f, -0.3f, 0.04f, 0.09f, "#FFC43D");
            UB(R, "Pencil", -0.15f, 0.75f, 0.32f, 0.008f, 0.008f, 0.17f, "#FFC43D", 70f);
            Plant(R, 0.6f, -0.35f, 0.5f, 0.75f, false);
            // wall art
            UB(R, "Poster", 1.5f, 1.2f, -2.43f, 0.7f, 0.5f, 0.02f, "#FFC43D");
            UB(R, "Poster art", 1.5f, 1.25f, -2.42f, 0.5f, 0.35f, 0.02f, "#FF5D73");
            UB(R, "Clock", -2.94f, 1.7f, 1.2f, 0.03f, 0.3f, 0.3f, "#FFF6E4");
        }

        static void KitchenClutter()
        {
            const string R = "kitchen";
            // counter tops (y = 0.91): kettle, toaster, cereal boxes, jars, a fruit bowl
            UC(R, "Kettle", -3.6f, 0.91f, -2.2f, 0.08f, 0.2f, "#FF5D73");
            UB(R, "Toaster", -4.0f, 0.91f, -2.2f, 0.26f, 0.17f, 0.16f, "#B9C3CF");
            UB(R, "Cereal box", -4.95f, 0.91f, -2.3f, 0.2f, 0.3f, 0.07f, "#FFC43D", 8f);
            UB(R, "Cereal box", -5.2f, 0.91f, -2.28f, 0.18f, 0.26f, 0.07f, "#3A86FF", -6f);
            for (int i = 0; i < 4; i++) UC(R, "Jar", -5.55f + i * 0.09f, 0.91f, -2.35f, 0.035f, Rf(0.1f, 0.16f), Bright[i]);
            UC(R, "Dish rack", -4.6f, 0.91f, -2.05f, 0.12f, 0.05f, "#97A0AE");
            for (int i = 0; i < 4; i++) UC(R, "Plate", -4.62f + i * 0.015f, 0.95f, -2.05f, 0.1f, 0.012f, "#FFFFFF");
            // the kitchen table: plates, cups, a newspaper; under it a cat bowl and a dropped spoon
            foreach (var (px, pz) in new[] { (-4.8f, 0.4f), (-5.4f, 0.4f), (-4.8f, 0.8f), (-5.4f, 0.8f) })
            {
                UC(R, "Plate", px, 0.765f, pz, 0.1f, 0.012f, "#FFFFFF");
                UC(R, "Cup", px + 0.12f, 0.765f, pz, 0.035f, 0.08f, AnyColour());
            }
            UB(R, "Newspaper", -5.1f, 0.765f, 0.95f, 0.3f, 0.01f, 0.22f, "#EDEAE0", 15f);
            UC(R, "Cat bowl", -5.0f, 0f, 0.55f, 0.07f, 0.035f, "#FF5D73");
            UB(R, "Spoon", -4.75f, 0f, 0.75f, 0.03f, 0.008f, 0.14f, "#B9C3CF", 30f);
            // trash bin by the fridge, a step stool, a mop bucket, shopping bags by the door
            UC(R, "Trash bin", -6.72f, 0f, -1.45f, 0.17f, 0.45f, "#3A86FF", true);
            UC(R, "Bin lid", -6.72f, 0.45f, -1.45f, 0.18f, 0.03f, "#2A6BD6");
            UB(R, "Step stool", -6.55f, 0f, 1.7f, 0.36f, 0.24f, 0.3f, "#FF8A3D", 15f, true, "table");
            UC(R, "Mop bucket", -6.6f, 0f, 0.6f, 0.15f, 0.22f, "#FFC43D", true);
            UC(R, "Mop", -6.6f, 0.15f, 0.6f, 0.012f, 1.0f, "#C98A55");
            UB(R, "Shopping bag", -3.55f, 0f, 1.95f, 0.3f, 0.34f, 0.16f, "#8FD35A", -10f, true);
            UB(R, "Shopping bag", -3.85f, 0f, 2.15f, 0.28f, 0.3f, 0.15f, "#FFF6E4", 20f, true);
            Ball(R, "Orange", -3.6f, 0f, 1.6f, 0.07f, 0.07f, 0.07f, "#FF8A3D");
            Ball(R, "Apple", -3.75f, 0f, 1.5f, 0.07f, 0.07f, 0.07f, "#FF5D73");
            // fridge magnets and a calendar
            for (int i = 0; i < 5; i++) UB(R, "Magnet", -6.4f + Rf(-0.2f, 0.2f), Rf(0.9f, 1.6f), -1.75f, 0.06f, 0.06f, 0.01f, AnyColour());
            UB(R, "Calendar", -3.2f, 1.2f, 0.8f, 0.02f, 0.45f, 0.32f, "#FFFFFF");
            UB(R, "Floor mat", -4.6f, 0f, -1.7f, 0.9f, 0.006f, 0.5f, "#FF5D73");
        }

        static void BedroomClutter()
        {
            const string R = "bedroom";
            // wardrobe, desk with lamp and drawings, a chair, a beanbag
            UB(R, "Wardrobe", 3.6f, 0f, 2.15f, 0.9f, 1.8f, 0.55f, "#8FD3B6", 0f, true);
            UB(R, "Wardrobe knob", 3.6f, 0.95f, 1.86f, 0.06f, 0.06f, 0.03f, "#FFC43D");
            UB(R, "Desk", 6.7f, 0.6f, -0.1f, 0.55f, 0.04f, 1.0f, "#FFC43D", 0f, true, "table");
            foreach (var dz in new[] { -0.45f, 0.35f }) UB(R, "Desk leg", 6.7f, 0f, -0.1f + dz, 0.5f, 0.6f, 0.04f, "#FFC43D", 0f, true);
            UC(R, "Desk lamp", 6.8f, 0.64f, -0.45f, 0.06f, 0.3f, "#3A86FF");
            for (int i = 0; i < 4; i++) UB(R, "Drawing", 6.65f + Rf(-0.08f, 0.08f), 0.64f + i * 0.003f, Rf(-0.1f, 0.3f), 0.21f, 0.002f, 0.29f, i % 2 == 0 ? "#FFFFFF" : "#FFF2B0", Rf(-30f, 30f));
            UB(R, "Desk chair", 6.2f, 0f, -0.1f, 0.38f, 0.42f, 0.38f, "#FF5D73", 0f, true, "table");
            UB(R, "Chair back", 6.0f, 0.42f, -0.1f, 0.05f, 0.4f, 0.38f, "#FF5D73");
            UB(R, "Beanbag", 3.85f, 0f, 1.35f, 0.62f, 0.38f, 0.62f, "#7B61FF", 20f, true, "rug");
            // teddy on the bed, a pile of plushies, toys everywhere
            Teddy(R, 6.35f, 0.16f, -1.95f, 1.1f);
            Teddy(R, 4.3f, 0f, -0.2f, 0.8f, "#FF8A9A");
            Ball(R, "Plush bunny", 4.55f, 0f, -0.35f, 0.12f, 0.16f, 0.1f, "#FFFFFF");
            ToyScatter(R, 3.9f, 6.0f, 0.1f, 1.8f, 16);
            ToyScatter(R, 3.6f, 4.6f, -1.2f, -0.6f, 6);
            // toy train loop on the round rug, block tower, book piles
            for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2f; UB(R, "Track", 4.95f + Mathf.Cos(a) * 0.55f, 0.006f, 0.9f + Mathf.Sin(a) * 0.55f, 0.06f, 0.012f, 0.32f, "#C98A55", -a * Mathf.Rad2Deg); }
            UB(R, "Toy train", 5.5f, 0.018f, 0.9f, 0.08f, 0.07f, 0.16f, "#FF5D73");
            for (int i = 0; i < 6; i++) UB(R, "Block tower", 5.75f + Rf(-0.01f, 0.01f), i * 0.06f, 1.75f + Rf(-0.01f, 0.01f), 0.06f, 0.06f, 0.06f, Bright[i % Bright.Length], Rf(-10f, 10f));
            BookPile(R, 6.5f, 0f, 0.7f, 4);
            // laundry pile, slippers, a backpack, posters
            Ball(R, "Laundry pile", 6.5f, 0f, 2.15f, 0.5f, 0.2f, 0.42f, "#3A86FF");
            Ball(R, "Laundry pile", 6.35f, 0.08f, 2.05f, 0.35f, 0.15f, 0.3f, "#FFF6E4");
            Shoes(R, 4.6f, -1.25f, "#FF8A9A", -30f);
            UB(R, "Backpack", 3.4f, 0f, -0.1f, 0.3f, 0.38f, 0.18f, "#FF8A3D", 70f, true);
            UB(R, "Poster", 6.94f, 1.2f, 1.2f, 0.02f, 0.6f, 0.45f, "#3A86FF");
            UB(R, "Poster star", 6.93f, 1.35f, 1.2f, 0.02f, 0.2f, 0.2f, "#FFE45C");
            UB(R, "Poster", 5.2f, 1.3f, -2.43f, 0.5f, 0.4f, 0.02f, "#8FD35A");
        }

        static void HallwayClutter()
        {
            const string R = "hallway";
            // along the back wall (z ~ 2.75) between doorways, along the front wall (z ~ 3.65) away from the patio
            UB(R, "Shoe rack", 2.4f, 0f, 3.62f, 0.8f, 0.32f, 0.3f, "#C98A55", 0f, true, "table");
            for (int i = 0; i < 4; i++) UB(R, "Rack shoe", 2.1f + i * 0.2f, 0.32f, 3.62f, 0.1f, 0.07f, 0.24f, AnyColour());
            Shoes(R, 1.6f, 3.4f, "#3A86FF", -20f);
            Shoes(R, -1.6f, 3.45f, "#FF5D73", 25f);
            UC(R, "Umbrella stand", -2.1f, 0f, 3.62f, 0.12f, 0.45f, "#3A86FF", true);
            UC(R, "Umbrella", -2.08f, 0.2f, 3.6f, 0.03f, 0.6f, "#FF5D73");
            UC(R, "Umbrella", -2.14f, 0.2f, 3.64f, 0.03f, 0.55f, "#FFC43D");
            UB(R, "Console table", -3.2f, 0.7f, 2.78f, 0.9f, 0.04f, 0.3f, "#FFF6E4", 0f, true, "table");
            foreach (var dx in new[] { -0.4f, 0.4f }) UB(R, "Console leg", -3.2f + dx, 0f, 2.78f, 0.04f, 0.7f, 0.04f, "#FFF6E4", 0f, true);
            UC(R, "Key bowl", -3.0f, 0.74f, 2.78f, 0.1f, 0.04f, "#FFC43D");
            Plant(R, -3.55f, 2.78f, 0.6f, 0.74f, false);
            CardboardBoxes(R, -6.5f, 3.55f, 3);
            CardboardBoxes(R, 6.55f, 2.85f, 2);
            Plant(R, 6.6f, 3.55f, 1.2f);
            UB(R, "Skateboard", 3.6f, 0.02f, 2.8f, 0.2f, 0.02f, 0.7f, "#FF8A3D", 80f);
            UB(R, "Scooter", -4.0f, 0f, 3.65f, 0.12f, 0.6f, 0.5f, "#1FA6A0", 0f, true);
            UB(R, "Doormat", 0f, 0f, 3.6f, 0.9f, 0.006f, 0.45f, "#C98A55");
            Ball(R, "Tennis ball", 0.9f, 0f, 3.0f, 0.065f, 0.065f, 0.065f, "#C8E62E");
        }

        static void GardenClutter()
        {
            const string R = "garden";
            // bench along the back fence, a watering can, a bucket and spade, a ball, a garden gnome, raised beds
            UB(R, "Bench seat", 0.6f, 0.4f, 7.25f, 1.2f, 0.05f, 0.35f, "#C98A55", 0f, true, "table");
            foreach (var dx in new[] { -0.5f, 0.5f }) UB(R, "Bench leg", 0.6f + dx, 0f, 7.25f, 0.06f, 0.4f, 0.3f, "#C98A55", 0f, true);
            UB(R, "Bench back", 0.6f, 0.45f, 7.4f, 1.2f, 0.35f, 0.04f, "#C98A55");
            UC(R, "Watering can", 2.7f, 0f, 5.0f, 0.1f, 0.2f, "#3A86FF", true);
            UB(R, "Spout", 2.55f, 0.12f, 5.0f, 0.2f, 0.03f, 0.03f, "#3A86FF", 30f);
            UC(R, "Bucket", 2.9f, 0f, 6.95f, 0.11f, 0.18f, "#FFC43D", true);
            UB(R, "Spade", 2.75f, 0f, 6.85f, 0.06f, 0.02f, 0.45f, "#FF5D73", 30f);
            Ball(R, "Garden ball", 0.9f, 0f, 6.55f, 0.18f, 0.18f, 0.18f, "#FF5D73");
            // gnome: hat, face, beard, body
            UC(R, "Gnome body", -2.8f, 0f, 4.5f, 0.07f, 0.14f, "#3A86FF", true);
            Ball(R, "Gnome beard", -2.8f, 0.1f, 4.55f, 0.1f, 0.1f, 0.06f, "#FFFFFF");
            Ball(R, "Gnome face", -2.8f, 0.17f, 4.52f, 0.07f, 0.07f, 0.07f, "#FFE2BF");
            UC(R, "Gnome hat", -2.8f, 0.2f, 4.5f, 0.045f, 0.14f, "#FF5D73");
            foreach (var (bx, bz) in new[] { (2.3f, 7.3f), (-1.0f, 7.3f) })
            {
                UB(R, "Raised bed", bx, 0f, bz, 1.0f, 0.18f, 0.3f, "#C98A55", 0f, true);
                for (int i = 0; i < 6; i++) Ball(R, "Bed flower", bx - 0.4f + i * 0.16f, 0.18f, bz, 0.1f, 0.14f, 0.1f, i % 2 == 0 ? "#FF5D73" : "#FFE45C");
            }
            UC(R, "Hose reel", 3.2f, 0f, 4.3f, 0.16f, 0.12f, "#8FD35A", true);
            for (int i = 0; i < 8; i++) Ball(R, "Fallen leaf", Rf(-2.5f, 2.5f), 0f, Rf(4.6f, 7.0f), 0.08f, 0.01f, 0.05f, i % 2 == 0 ? "#E07A4F" : "#FFC43D");
        }
    }
}
