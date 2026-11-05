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

        /// The material of a colour by its name ("blue", "skin"…), or null.
        public Material ByName(string colour) => colour switch
        {
            "skin" => skin, "dark" => dark, "white" => white, "red" => red, "blue" => blue, "green" => green, "yellow" => yellow,
            "brown" => brown, "grey" => grey, "orange" => orange, "purple" => purple, "metal" => metal, "clear" => clear,
            "pink" => pink, "water" => water, "line" => line, _ => null,
        };

        /// Every mesh of a Blender model gets the material of the colour its name ends with ("Coat.blue", "Hat.red";
        /// Tools/Blender/kit.py names them so); a name without a known colour keeps what it has.
        public void Paint(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var parts = r.name.Split('.');
                var m = parts.Length > 1 ? ByName(parts[1]) : null;
                if (m != null) r.sharedMaterial = m;
            }
        }
    }

    /// The customers behind the counter's window (DESIGN.md 2б, «Заказчики»): people of the trade their order speaks
    /// of — a driver, an engineer, a baker, a granny, a cosmonaut… — and a few creatures (a fish, a duck, a beaver, a gnome,
    /// a robot). Each is a Blender model (Tools/Blender/customers.py) in the kit's colours: a one-piece body, a face, and
    /// what tells who they are (a hat, a helmet, a beard, glasses, an apron). They stand on the floor of the corridor,
    /// facing +z of `root`; the counter's sill is at 1.21 m, so the head and shoulders show over it.
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

        /// Puts the customer's Blender model (Tools/Blender/customers.py, "Persona_<who>") under `root`, painted in
        /// the kit's chalk; returns the height of the top of its head over the floor, as the model marks it ("Top"):
        /// where the "!" over it, its stars and the words it says go.
        public static float Build(string who, Transform root, PersonaKit kit)
        {
            for (int i = root.childCount - 1; i >= 0; i--) Object.Destroy(root.GetChild(i).gameObject);
            var go = ModelLibrary.Spawn("Persona_" + who, root, kit);
            if (go == null) return 1.8f;
            var top = go.transform.Find("Top");
            if (top != null) return top.localPosition.y;
            float y = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) y = Mathf.Max(y, root.InverseTransformPoint(r.bounds.max).y);
            return y;
        }
    }
}
