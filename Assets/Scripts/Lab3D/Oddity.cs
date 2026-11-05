using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The odd measurements of the 3D lab (DESIGN.md 2б, «Крейзи-измерения»): not a number read off a scale but a box to
    /// tick — what the big tongue tastes, which fish bites, does it glow in the dark, does it show in the mirror, what the
    /// cat does, which note it rings, is it ticklish. Each is a row of options on the order sheet; the taste may tick
    /// several, the rest one. Each comes with its own department, bought on the map.
    public static class Oddity
    {
        /// The odd tests, in the order the map opens them; the department of each has the same id (the switch for "glow").
        public static readonly string[] All = { "taste", "glow", "fish", "cat", "tickle", "mirror", "note" };

        public static bool Is(string test) => System.Array.IndexOf(All, test) >= 0;

        /// What the map sells to open it.
        public static string Perk(string test) => "x_" + test;

        public static string[] Options(string test) => test switch
        {
            "taste" => new[] { "сладкий", "солёный", "кислый", "горький", "умами" },
            "fish" => new[] { "окунь", "карась", "щука", "сом", "ёрш", "никто" },
            "cat" => new[] { "мурлычет", "шипит", "играет", "спит" },
            "note" => new[] { "до", "ре", "ми", "фа", "соль", "ля", "си" },
            "glow" or "mirror" or "tickle" => new[] { "да", "нет" },
            _ => null,
        };

        /// What an order of these questions pays against a weighing: as much as the work it takes. A flick of the switch
        /// (which sees every sample at once) is half a weighing; the taste (up to five places), the pond (a wait for a
        /// bite) and the note (by ear) are more than one.
        public static float Work(System.Collections.Generic.List<string> tests)
        {
            float k = 1;
            foreach (var t in tests)
                k *= t switch
                {
                    "glow" => 0.5f, "mirror" => 0.6f, "tickle" => 0.7f, "cat" => 0.8f,
                    "taste" => 1.3f, "fish" => 1.3f, "note" => 1.3f,
                    _ => 1f,
                };
            return k;
        }

        /// May several boxes be ticked (the taste: a sample can be sweet and sour at once)?
        public static bool Multi(string test) => test == "taste";

        public static string RowName(string test) => test switch
        {
            "taste" => "Вкус",
            "fish" => "Кто клюёт",
            "glow" => "Светится в темноте",
            "mirror" => "Отражается в зеркале",
            "cat" => "Что делает кот",
            "note" => "Какую ноту поёт",
            "tickle" => "Боится щекотки",
            _ => test,
        };

        /// The department's sign: its name and, instead of a formula, its question.
        public static string Title(string id) => id switch
        {
            "taste" => "Язык\nкакой вкус?",
            "fish" => "Пруд\nкто клюёт?",
            "glow" => "Выключатель",
            "mirror" => "Зеркало\nотражается?",
            "cat" => "Кот\nчто он скажет?",
            "note" => "Ксилофон\nкакая нота?",
            "tickle" => "Перо\nбоится щекотки?",
            _ => id,
        };

        /// The true answer: the ticked boxes as bits (bit k — option k).
        public static int Truth(Sample d, string test) => test switch
        {
            "taste" => d.taste,
            "fish" => 1 << d.fish,
            "cat" => 1 << d.cat,
            "note" => 1 << d.note,
            "glow" => d.glows ? 1 : 2,
            "mirror" => d.reflects ? 1 : 2,
            "tickle" => d.ticklish ? 1 : 2,
            _ => 0,
        };

        /// The boxes ticked in an answer: it is written as the options' numbers, "135" — the first, third and fifth.
        public static int Ticked(string answer)
        {
            int m = 0;
            if (answer != null) foreach (char c in answer) if (c >= '1' && c <= '9') m |= 1 << (c - '1');
            return m;
        }

        /// How right the ticks are, 0..1. One option: right or not (a note one step off counts half — it was close). The
        /// taste: every box counts, ticked or left; four of five right is two thirds, three of five a third.
        public static double Score(string test, string answer, int truth)
        {
            int got = Ticked(answer);
            if (test == "taste")
            {
                int right = 0;
                for (int k = 0; k < 5; k++) if (((got >> k) & 1) == ((truth >> k) & 1)) right++;
                return Mathf.Clamp01((right - 2) / 3f);
            }
            if (got == truth) return 1;
            if (test == "note" && got != 0 && (got == truth << 1 || got == truth >> 1)) return 0.5;
            return 0;
        }

        /// A new sample's odd nature: one to three tastes, the fish (one in six nobody), glowing two in five, a mirror
        /// missing it one in four (DESIGN.md 2б).
        public static void Roll(Sample d)
        {
            int tastes = Random.value < 0.5f ? 1 : Random.value < 0.75f ? 2 : 3;
            d.taste = 0;
            while (CountBits(d.taste) < tastes) d.taste |= 1 << Random.Range(0, 5);
            d.fish = Random.Range(0, 6);
            d.cat = Random.Range(0, 4);
            d.note = Random.Range(0, 7);
            d.glows = Random.value < 0.4f;
            d.reflects = Random.value < 0.75f;
            d.ticklish = Random.value < 0.5f;
        }

        static int CountBits(int m) { int n = 0; for (; m != 0; m &= m - 1) n++; return n; }

        /// The notes as pitches over do (equal temperament): do re mi fa sol la si.
        public static readonly float[] Pitch = { 1f, 1.1225f, 1.2599f, 1.3348f, 1.4983f, 1.6818f, 1.8877f };
    }
}
