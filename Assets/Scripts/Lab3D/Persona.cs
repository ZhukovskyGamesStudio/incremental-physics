using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The chalk materials a customer is drawn with (made by the scene builder).
    [System.Serializable]
    public class PersonaKit
    {
        public Material skin, dark, white, red, blue, green, yellow, brown, grey, orange, purple, metal, clear;
        /// For the odd departments (DESIGN.md 2б, «Крейзи-измерения»): the tongue's pink, the pond's water, a plain white
        /// line (a fishing line).
        public Material pink, water, line;
    }

    /// The customers behind the counter's window (DESIGN.md 2б, «Заказчики»): people of the trade their order speaks
    /// of — a driver, an engineer, a baker, a granny, a cosmonaut… — and a few creatures (a fish, a duck, a beaver, a gnome,
    /// a robot). Built of simple shapes in chalk: a body, a head, eyes, and what tells who they are (a hat, a helmet, a
    /// beard, glasses, an apron). They stand on the floor of the corridor, facing +z of `root`; the counter's sill is at
    /// 1.21 m, so the head and shoulders show over it.
    public static class Persona
    {
        public static readonly string[] All =
        {
            "driver", "engineer", "cosmonaut", "librarian", "baker", "granny", "kid", "mechanic", "postman", "strongman",
            "teacher", "robot", "curator", "blacksmith", "sailor", "tailor", "fish", "gnome", "beaver", "duck", "jeweler",
            "cook", "fisherman", "vampire", "musician", "clown",
        };

        /// What the player calls them (on the box going back: «для: пекарь»).
        public static string Name(string who) => who switch
        {
            "driver" => "шофёр", "engineer" => "инженер", "cosmonaut" => "космонавт", "librarian" => "библиотекарь", "baker" => "пекарь",
            "granny" => "бабушка", "kid" => "мальчишка", "mechanic" => "механик", "postman" => "почтальон", "strongman" => "силач",
            "teacher" => "учитель", "robot" => "робот", "curator" => "хранитель музея", "blacksmith" => "кузнец", "sailor" => "моряк",
            "tailor" => "портной", "fish" => "рыба", "gnome" => "гном", "beaver" => "бобр", "duck" => "утка", "jeweler" => "ювелир",
            "cook" => "повар", "fisherman" => "рыбак", "vampire" => "вампир", "musician" => "музыкант", "clown" => "клоун",
            _ => who,
        };

        static PersonaKit _k;
        static Transform _root;

        /// Height of the top of its head over the floor (for the "!" over it and the words it says).
        public static float Build(string who, Transform root, PersonaKit kit)
        {
            _k = kit;
            _root = root;
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
            switch (who)
            {
                case "robot": return Robot();
                case "fish": return Fish();
                case "duck": return Duck();
                case "beaver": return Beaver();
                case "gnome": return Gnome();
            }
            // a person: the trade picks the clothes and what is on the head
            Material coat = who switch
            {
                "driver" => kit.blue, "engineer" => kit.orange, "cosmonaut" => kit.white, "librarian" => kit.green, "baker" => kit.white,
                "granny" => kit.purple, "kid" => kit.red, "mechanic" => kit.blue, "postman" => kit.blue, "strongman" => kit.red,
                "teacher" => kit.brown, "curator" => kit.dark, "blacksmith" => kit.grey, "sailor" => kit.white, "tailor" => kit.yellow,
                "jeweler" => kit.purple,
                "cook" => kit.white, "fisherman" => kit.green, "vampire" => kit.dark, "musician" => kit.blue, "clown" => kit.yellow,
                _ => kit.grey,
            };
            float tall = who == "kid" ? 0.82f : who == "strongman" ? 1.08f : 1f;
            float wide = who == "strongman" ? 1.45f : who == "granny" ? 1.1f : 1f;
            float head = Body(coat, tall, wide, who == "kid" ? kit.blue : kit.dark);
            switch (who)
            {
                case "driver": Cap(head, kit.blue, false); Moustache(head); break;
                case "engineer": HardHat(head, kit.yellow); Glasses(head); break;
                case "cosmonaut": Part(PrimitiveType.Sphere, kit.clear, new Vector3(0, head, 0), Vector3.one * 0.36f); Part(PrimitiveType.Cylinder, kit.white, new Vector3(0, head - 0.2f, 0), new Vector3(0.3f, 0.03f, 0.3f)); break;
                case "librarian": Hair(head, kit.brown); Part(PrimitiveType.Sphere, kit.brown, new Vector3(0, head + 0.1f, -0.1f), Vector3.one * 0.12f); Glasses(head); break;
                case "baker": Part(PrimitiveType.Cylinder, kit.white, new Vector3(0, head + 0.17f, 0), new Vector3(0.22f, 0.09f, 0.22f)); Part(PrimitiveType.Sphere, kit.white, new Vector3(0, head + 0.27f, 0), new Vector3(0.3f, 0.14f, 0.3f)); Moustache(head); break;
                case "granny": Part(PrimitiveType.Sphere, kit.red, new Vector3(0, head + 0.03f, -0.015f), new Vector3(0.29f, 0.29f, 0.29f)); Part(PrimitiveType.Sphere, kit.red, new Vector3(0, head - 0.15f, 0.11f), Vector3.one * 0.06f); Glasses(head); break;
                case "kid": Cap(head, kit.red, true); break;
                case "mechanic": Cap(head, kit.red, false); Part(PrimitiveType.Cube, kit.dark, new Vector3(0.06f, head - 0.07f, 0.125f), new Vector3(0.05f, 0.015f, 0.01f)); break;
                case "postman": Cap(head, kit.blue, false); Part(PrimitiveType.Cube, kit.brown, new Vector3(0.24f * wide, head - 0.55f, 0.04f), new Vector3(0.1f, 0.2f, 0.24f)); Part(PrimitiveType.Cube, kit.brown, new Vector3(0.02f, head - 0.4f, 0.125f), new Vector3(0.34f, 0.03f, 0.02f)).localRotation = Quaternion.Euler(0, 0, -40); break;
                case "strongman": Moustache(head); Part(PrimitiveType.Sphere, kit.dark, new Vector3(0, head + 0.1f, 0), new Vector3(0.2f, 0.06f, 0.2f)); break;
                case "teacher": Hair(head, kit.grey); Glasses(head); Part(PrimitiveType.Cube, kit.red, new Vector3(0, head - 0.33f, 0.12f), new Vector3(0.05f, 0.2f, 0.02f)); break;
                case "curator": Part(PrimitiveType.Cylinder, kit.dark, new Vector3(0, head + 0.2f, 0), new Vector3(0.2f, 0.11f, 0.2f)); Part(PrimitiveType.Cylinder, kit.dark, new Vector3(0, head + 0.1f, 0), new Vector3(0.32f, 0.01f, 0.32f)); Part(PrimitiveType.Cylinder, kit.yellow, new Vector3(-0.05f, head + 0.01f, 0.125f), new Vector3(0.06f, 0.004f, 0.06f)).localRotation = Quaternion.Euler(90, 0, 0); Part(PrimitiveType.Cube, kit.red, new Vector3(0, head - 0.2f, 0.12f), new Vector3(0.12f, 0.04f, 0.02f)); break;
                case "blacksmith": Beard(head, kit.brown); Part(PrimitiveType.Sphere, kit.brown, new Vector3(0, head - 0.6f, 0.03f), new Vector3(0.37f, 0.56f, 0.23f)); break;
                case "sailor": Part(PrimitiveType.Cylinder, kit.white, new Vector3(0, head + 0.12f, 0), new Vector3(0.26f, 0.03f, 0.26f)); Part(PrimitiveType.Cylinder, kit.dark, new Vector3(0, head + 0.09f, 0), new Vector3(0.24f, 0.02f, 0.24f)); Beard(head, kit.grey); Telnyashka(tall, wide, kit.blue); break;
                case "jeweler":   // a loupe in one eye, a waistcoat, a bow tie
                    Hair(head, kit.grey); Moustache(head);
                    Part(PrimitiveType.Cylinder, kit.dark, new Vector3(0.05f, head + 0.025f, 0.13f), new Vector3(0.07f, 0.04f, 0.07f)).localRotation = Quaternion.Euler(90, 0, 0);
                    Part(PrimitiveType.Cube, kit.yellow, new Vector3(0, head - 0.2f, 0.12f), new Vector3(0.12f, 0.04f, 0.02f)); break;
                case "cook":   // a tall white toque and a red neckerchief
                    Moustache(head);
                    Part(PrimitiveType.Cylinder, kit.white, new Vector3(0, head + 0.2f, 0), new Vector3(0.22f, 0.12f, 0.22f));
                    Part(PrimitiveType.Sphere, kit.white, new Vector3(0, head + 0.33f, 0), new Vector3(0.3f, 0.16f, 0.3f));
                    Part(PrimitiveType.Cube, kit.red, new Vector3(0, head - 0.2f, 0.11f), new Vector3(0.16f, 0.06f, 0.03f)); break;
                case "fisherman":   // a floppy hat, a beard, a rod over the shoulder
                    Beard(head, kit.grey);
                    Part(PrimitiveType.Cylinder, kit.green, new Vector3(0, head + 0.09f, 0), new Vector3(0.36f, 0.015f, 0.36f));
                    Part(PrimitiveType.Sphere, kit.green, new Vector3(0, head + 0.12f, 0), new Vector3(0.24f, 0.14f, 0.24f));
                    Part(PrimitiveType.Cylinder, kit.brown, new Vector3(0.48f, head - 0.08f, 0.13f), new Vector3(0.015f, 0.55f, 0.015f)).localRotation = Quaternion.Euler(0, 0, -25); break;
                case "vampire":   // pale, black slicked hair with a peak, a cape with a tall red collar, fangs
                    Part(PrimitiveType.Sphere, kit.dark, new Vector3(0, head + 0.05f, -0.025f), new Vector3(0.255f, 0.22f, 0.25f));
                    Part(PrimitiveType.Cube, kit.dark, new Vector3(0, head + 0.09f, 0.1f), new Vector3(0.04f, 0.06f, 0.03f)).localRotation = Quaternion.Euler(0, 0, 45);
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        Part(PrimitiveType.Cube, kit.red, new Vector3(sx * 0.09f, head - 0.14f, -0.04f), new Vector3(0.015f, 0.17f, 0.13f)).localRotation = Quaternion.Euler(0, sx * 25, sx * -8);
                        Part(PrimitiveType.Cube, kit.white, new Vector3(sx * 0.02f, head - 0.085f, 0.115f), new Vector3(0.012f, 0.025f, 0.01f));
                    }
                    Part(PrimitiveType.Capsule, kit.dark, new Vector3(0, head - 0.62f, -0.07f), new Vector3(0.48f, 0.46f, 0.2f)); break;
                case "musician":   // curly hair, a bow tie, a violin under the chin
                    Hair(head, kit.brown);
                    Part(PrimitiveType.Cube, kit.red, new Vector3(0, head - 0.2f, 0.12f), new Vector3(0.12f, 0.04f, 0.02f));
                    var violin = Part(PrimitiveType.Sphere, kit.orange, new Vector3(0.1f, head - 0.25f, 0.13f), new Vector3(0.13f, 0.05f, 0.22f));
                    violin.localRotation = Quaternion.Euler(10, -35, -20);
                    Part(PrimitiveType.Cube, kit.dark, new Vector3(0.02f, head - 0.22f, 0.24f), new Vector3(0.012f, 0.012f, 0.12f)).localRotation = Quaternion.Euler(10, -35, -20); break;
                case "clown":   // a red nose, a frizz of hair, a small hat, a big ruff
                    Part(PrimitiveType.Sphere, kit.red, new Vector3(0, head - 0.03f, 0.135f), Vector3.one * 0.06f);
                    foreach (float sx in new[] { -1f, 1f }) Part(PrimitiveType.Sphere, kit.orange, new Vector3(sx * 0.12f, head + 0.03f, -0.02f), Vector3.one * 0.13f);
                    Part(PrimitiveType.Cylinder, kit.green, new Vector3(0, head + 0.17f, 0), new Vector3(0.1f, 0.05f, 0.1f));
                    Part(PrimitiveType.Sphere, kit.white, new Vector3(0, head - 0.17f, 0), new Vector3(0.36f, 0.09f, 0.32f)); break;
                case "tailor": Hair(head, kit.dark); Glasses(head); Part(PrimitiveType.Cube, kit.yellow, new Vector3(0.09f, head - 0.4f, 0.12f), new Vector3(0.03f, 0.3f, 0.01f)); Part(PrimitiveType.Cube, kit.yellow, new Vector3(-0.09f, head - 0.4f, 0.12f), new Vector3(0.03f, 0.3f, 0.01f)); break;
            }
            return head + 0.2f;
        }

        /// A striped vest: thin blue rings all the way round the body and the arms, as the coat of Body is laid.
        static void Telnyashka(float tall, float wide, Material stripe)
        {
            float hip = 0.82f * tall, shoulders = 1.42f * tall;
            // the body's straight part, between its rounded ends
            for (float y = hip + 0.11f; y < shoulders - 0.07f; y += 0.052f)
                Part(PrimitiveType.Cylinder, stripe, new Vector3(0, y, 0), new Vector3(0.4f * wide + 0.008f, 0.0095f, 0.268f));
            foreach (float sx in new[] { -1f, 1f })
            {
                var rot = Quaternion.Euler(-25, 0, sx * 8);
                var c = new Vector3(sx * (0.2f * wide + 0.035f), shoulders - 0.22f, 0.04f);
                for (float t = -0.15f; t <= 0.19f; t += 0.052f)
                    Part(PrimitiveType.Cylinder, stripe, c + rot * Vector3.up * t, new Vector3(0.097f, 0.0085f, 0.097f)).localRotation = rot;
            }
        }

        // ------------------------------------------------------------------ a person

        /// Legs, a coat, arms, a neck and a head with eyes and a nose; returns the head's height.
        static float Body(Material coat, float tall, float wide, Material legs)
        {
            float hip = 0.82f * tall, shoulders = 1.42f * tall, head = 1.6f * tall;
            Part(PrimitiveType.Cylinder, legs, new Vector3(-0.08f, hip / 2, 0), new Vector3(0.11f, hip / 2, 0.11f));
            Part(PrimitiveType.Cylinder, legs, new Vector3(0.08f, hip / 2, 0), new Vector3(0.11f, hip / 2, 0.11f));
            Part(PrimitiveType.Capsule, coat, new Vector3(0, (hip + shoulders) / 2, 0), new Vector3(0.4f * wide, (shoulders - hip) / 2 + 0.12f, 0.26f));
            foreach (float sx in new[] { -1f, 1f })
            {
                var arm = Part(PrimitiveType.Capsule, coat, new Vector3(sx * (0.2f * wide + 0.035f), shoulders - 0.22f, 0.04f), new Vector3(0.09f, 0.24f, 0.09f));
                arm.localRotation = Quaternion.Euler(-25, 0, sx * 8);
                Part(PrimitiveType.Sphere, _k.skin, new Vector3(sx * (0.2f * wide + 0.055f), shoulders - 0.42f, 0.13f), Vector3.one * 0.08f);
            }
            Part(PrimitiveType.Cylinder, _k.skin, new Vector3(0, shoulders + 0.04f, 0), new Vector3(0.09f, 0.05f, 0.09f));
            Part(PrimitiveType.Sphere, _k.skin, new Vector3(0, head, 0), new Vector3(0.24f, 0.27f, 0.24f));
            Eyes(head, 0.11f, 0.045f, 0.035f);
            Part(PrimitiveType.Sphere, _k.skin, new Vector3(0, head - 0.03f, 0.125f), new Vector3(0.045f, 0.05f, 0.05f));
            return head;
        }

        static void Eyes(float y, float front, float apart, float size)
        {
            foreach (float sx in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, _k.dark, new Vector3(sx * apart, y + 0.025f, front), new Vector3(size, size * 1.3f, size * 0.6f));
        }

        static void Cap(float head, Material m, bool backwards)
        {
            Part(PrimitiveType.Sphere, m, new Vector3(0, head + 0.07f, 0), new Vector3(0.26f, 0.16f, 0.26f));
            Part(PrimitiveType.Cube, m, new Vector3(0, head + 0.06f, backwards ? -0.15f : 0.15f), new Vector3(0.18f, 0.015f, 0.12f));
        }

        static void HardHat(float head, Material m)
        {
            Part(PrimitiveType.Sphere, m, new Vector3(0, head + 0.08f, 0), new Vector3(0.28f, 0.2f, 0.28f));
            Part(PrimitiveType.Cylinder, m, new Vector3(0, head + 0.04f, 0.01f), new Vector3(0.34f, 0.01f, 0.34f));
        }

        static void Hair(float head, Material m) => Part(PrimitiveType.Sphere, m, new Vector3(0, head + 0.05f, -0.02f), new Vector3(0.26f, 0.22f, 0.25f));

        static void Glasses(float head)
        {
            foreach (float sx in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, _k.dark, new Vector3(sx * 0.05f, head + 0.025f, 0.122f), new Vector3(0.07f, 0.003f, 0.07f)).localRotation = Quaternion.Euler(90, 0, 0);
            Part(PrimitiveType.Cube, _k.dark, new Vector3(0, head + 0.03f, 0.125f), new Vector3(0.04f, 0.008f, 0.008f));
        }

        static void Moustache(float head) => Part(PrimitiveType.Cube, _k.dark, new Vector3(0, head - 0.07f, 0.118f), new Vector3(0.11f, 0.025f, 0.02f));

        static void Beard(float head, Material m) => Part(PrimitiveType.Sphere, m, new Vector3(0, head - 0.12f, 0.06f), new Vector3(0.22f, 0.24f, 0.16f));

        // ------------------------------------------------------------------ the creatures

        static float Robot()
        {
            Part(PrimitiveType.Cylinder, _k.metal, new Vector3(0, 0.45f, 0), new Vector3(0.14f, 0.45f, 0.14f));
            Part(PrimitiveType.Cylinder, _k.metal, new Vector3(0, 1.41f, 0), new Vector3(0.1f, 0.05f, 0.1f));
            Part(PrimitiveType.Cube, _k.metal, new Vector3(0, 1.12f, 0), new Vector3(0.44f, 0.5f, 0.3f));
            Part(PrimitiveType.Cube, _k.yellow, new Vector3(0, 1.18f, 0.152f), new Vector3(0.2f, 0.12f, 0.01f));
            Part(PrimitiveType.Cube, _k.metal, new Vector3(0, 1.58f, 0), new Vector3(0.32f, 0.26f, 0.26f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, _k.blue, new Vector3(sx * 0.07f, 1.6f, 0.132f), new Vector3(0.06f, 0.05f, 0.01f));
                Part(PrimitiveType.Cube, _k.metal, new Vector3(sx * 0.25f, 1.08f, 0.06f), new Vector3(0.07f, 0.36f, 0.07f));
            }
            Part(PrimitiveType.Cylinder, _k.metal, new Vector3(0, 1.78f, 0), new Vector3(0.012f, 0.06f, 0.012f));
            Part(PrimitiveType.Sphere, _k.red, new Vector3(0, 1.85f, 0), Vector3.one * 0.045f);
            return 1.9f;
        }

        static float Fish()
        {
            // a big orange fish in a ball of water, the bowl on a little stand
            Part(PrimitiveType.Cylinder, _k.metal, new Vector3(0, 0.6f, 0), new Vector3(0.05f, 0.6f, 0.05f));
            Part(PrimitiveType.Sphere, _k.clear, new Vector3(0, 1.55f, 0), Vector3.one * 0.62f);
            Part(PrimitiveType.Sphere, _k.orange, new Vector3(0, 1.55f, 0), new Vector3(0.36f, 0.26f, 0.2f)).localRotation = Quaternion.Euler(0, 90, 0);
            var tail = Part(PrimitiveType.Cube, _k.orange, new Vector3(0, 1.55f, -0.22f), new Vector3(0.02f, 0.2f, 0.14f));
            tail.localRotation = Quaternion.Euler(0, 0, 0);
            Part(PrimitiveType.Cube, _k.orange, new Vector3(0, 1.7f, 0), new Vector3(0.02f, 0.08f, 0.14f));
            foreach (float sx in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, _k.white, new Vector3(sx * 0.06f, 1.6f, 0.13f), Vector3.one * 0.07f);
                Part(PrimitiveType.Sphere, _k.dark, new Vector3(sx * 0.06f, 1.6f, 0.16f), Vector3.one * 0.035f);
            }
            return 1.86f;
        }

        static float Duck()
        {
            Part(PrimitiveType.Cylinder, _k.orange, new Vector3(-0.1f, 0.39f, 0), new Vector3(0.05f, 0.39f, 0.05f));
            Part(PrimitiveType.Cylinder, _k.orange, new Vector3(0.1f, 0.39f, 0), new Vector3(0.05f, 0.39f, 0.05f));
            Part(PrimitiveType.Sphere, _k.yellow, new Vector3(0, 1.33f, 0.06f), new Vector3(0.18f, 0.16f, 0.18f));   // the neck
            Part(PrimitiveType.Sphere, _k.yellow, new Vector3(0, 1.05f, 0), new Vector3(0.62f, 0.62f, 0.7f));
            Part(PrimitiveType.Sphere, _k.yellow, new Vector3(0, 1.58f, 0.08f), Vector3.one * 0.36f);
            Part(PrimitiveType.Sphere, _k.orange, new Vector3(0, 1.53f, 0.27f), new Vector3(0.18f, 0.06f, 0.16f));
            Eyes(1.62f, 0.23f, 0.08f, 0.05f);
            foreach (float sx in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, _k.yellow, new Vector3(sx * 0.3f, 1.08f, 0), new Vector3(0.1f, 0.3f, 0.4f));
            return 1.8f;
        }

        static float Beaver()
        {
            Part(PrimitiveType.Capsule, _k.brown, new Vector3(0, 0.85f, 0), new Vector3(0.55f, 0.6f, 0.45f));
            Part(PrimitiveType.Cube, _k.dark, new Vector3(0, 0.12f, -0.38f), new Vector3(0.3f, 0.06f, 0.5f)).localRotation = Quaternion.Euler(-15, 0, 0);
            Part(PrimitiveType.Sphere, _k.brown, new Vector3(0, 1.55f, 0.02f), new Vector3(0.36f, 0.32f, 0.34f));
            Part(PrimitiveType.Sphere, _k.orange, new Vector3(0, 1.5f, 0.17f), new Vector3(0.16f, 0.1f, 0.08f));
            Part(PrimitiveType.Cube, _k.white, new Vector3(0, 1.42f, 0.2f), new Vector3(0.07f, 0.06f, 0.015f));
            Part(PrimitiveType.Sphere, _k.dark, new Vector3(0, 1.53f, 0.21f), new Vector3(0.05f, 0.035f, 0.03f));
            Eyes(1.6f, 0.16f, 0.08f, 0.045f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, _k.brown, new Vector3(sx * 0.14f, 1.7f, 0), Vector3.one * 0.08f);
                Part(PrimitiveType.Sphere, _k.brown, new Vector3(sx * 0.2f, 1.12f, 0.18f), new Vector3(0.1f, 0.14f, 0.1f));
            }
            return 1.75f;
        }

        static float Gnome()
        {
            // a small gnome, so he stands on a crate to see over the counter
            Part(PrimitiveType.Cube, _k.brown, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.45f));
            Part(PrimitiveType.Capsule, _k.blue, new Vector3(0, 1.0f, 0), new Vector3(0.4f, 0.36f, 0.32f));
            Part(PrimitiveType.Sphere, _k.skin, new Vector3(0, 1.5f, 0), new Vector3(0.24f, 0.24f, 0.24f));
            Part(PrimitiveType.Sphere, _k.skin, new Vector3(0, 1.48f, 0.12f), new Vector3(0.07f, 0.06f, 0.06f));
            Eyes(1.52f, 0.11f, 0.05f, 0.035f);
            Beard(1.5f, _k.white);
            // a tall pointed red hat: stacked rings, narrower to the tip
            for (int i = 0; i < 6; i++)
            {
                float f = i / 6f;
                Part(PrimitiveType.Cylinder, _k.red, new Vector3(0, 1.6f + i * 0.06f, -0.01f * i), new Vector3(0.27f * (1 - f) + 0.03f, 0.03f, 0.27f * (1 - f) + 0.03f));
            }
            return 2.0f;
        }

        // ------------------------------------------------------------------

        static Transform Part(PrimitiveType type, Material m, Vector3 at, Vector3 size) => Art3D.Part(type, m, _root, at, size);
    }
}
