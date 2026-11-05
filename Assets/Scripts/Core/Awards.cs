using System;
using System.Collections.Generic;

namespace ChalkPhysics
{
    /// A certificate the lab has earned (DESIGN.md 2б, «Грамоты»): which, who signed it, on what day, and where on the walls
    /// it hangs (the player moves it; it stays there from day to day).
    [Serializable]
    public class AwardData
    {
        public string id, signer;
        public int day;
        public bool placed, seen;
        public float x, y, z, yaw;
        public string wall = "";   // a temporary wall it rides on as the lab grows («Partition», «PartitionSide»), or the room's own
    }

    /// The achievements of the 3D lab (DESIGN.md 2б, «Грамоты»): each a step of the lab's way — the first order, the first
    /// volume, the density worked out, the room without its temporary walls, the grant, a regular, a contract, the rival,
    /// the ranks. Each one earned brings a certificate with its picture and a signature, hung on a wall.
    public static class Awards
    {
        public struct Def
        {
            public string id, title, why, icon, signer;
            public Def(string id, string title, string why, string icon, string signer = "президент Академии наук")
            { this.id = id; this.title = title; this.why = why; this.icon = icon; this.signer = signer; }
        }

        public static readonly Def[] All =
        {
            new Def("first", "Первый заказ", "за первый сданный заказ", "parcel"),
            new Def("eye", "Зоркий глаз", "за измерение точнее 98%", "lens"),
            new Def("eureka", "Эврика!", "за первый объём, измеренный водой", "arch"),
            new Def("rho", "Плотность найдена", "за плотность ρ = m / V, посчитанную точно", "density"),
            new Def("mu", "Знаток трения", "за трение μ = tg α, найденное точно", "slide"),
            new Def("odd", "Мастер на все руки", "за заказы пяти разных отделов в один день", "tongue"),
            new Def("odd7", "Все отделы", "за все отделы лаборатории", "cat"),
            new Def("room", "Своя лаборатория", "за лабораторию без временных стен", "table"),
            new Def("clean", "Без хвостов", "за день, когда все заказы сданы точно", "clock"),
            new Def("famous", "Известная лаборатория", "за рекламу в газете", "sign", "редакция газеты"),
            new Def("tool", "Точный прибор", "за прибор с лучшей шкалой и лучшей точностью", "dyna"),
            new Def("tools", "Эталонная лаборатория", "за три прибора с лучшими улучшениями", "gear"),
            new Def("grant", "Стипендиат академии", "за первую премию гранта", "bank"),
            new Def("regular", "Свой человек", "за заказчика с тремя звёздами", "trust"),
            new Def("contract", "Надёжный подрядчик", "за выполненный контракт", "list"),
            new Def("rival", "Пробиркин обойдён", "за неделю, выигранную у соперника", "star", "проф. Пробиркин (неохотно)"),
            new Def("cand", "Кандидат наук", "за звание кандидата наук", "mortar"),
            new Def("acad", "Академик", "за высшее звание", "mortar"),
            new Def("hundred", "Сотый заказ", "за сто сданных заказов", "coin"),
        };

        public static Def Get(string id) => Array.Find(All, d => d.id == id);

        public static bool Has(GameState g, string id) => g.Career.awards.Exists(a => a.id == id);

        public static int Count(GameState g) => g.Career.awards.Count;

        /// Gives the award once: a new certificate, told on the map tomorrow.
        static void Give(GameState g, List<AwardData> got, string id, string signer = null)
        {
            if (Has(g, id)) return;
            var d = Get(id);
            var a = new AwardData { id = id, signer = signer ?? d.signer, day = g.Lessons + 1 };
            g.Career.awards.Add(a);
            g.Career.news.Add($"Грамота «{d.title}»");
            got.Add(a);
        }

        /// The best of each instrument bought: its finest scale and its truest make.
        static readonly (string scale, string truth)[] Best = { ("scale3", "true3"), ("vscale2", "vtrue2"), ("astep1", "atrue2") };

        /// What can be earned at any moment, the save alone saying: the map's purchases, the ranks, the counts.
        static void Standing(GameState g, List<AwardData> got)
        {
            var c = g.Career;
            if (c.delivered >= 1) Give(g, got, "first");
            if (c.delivered >= 100) Give(g, got, "hundred");
            if (c.grantLevel >= 1) Give(g, got, "grant");
            if (c.weeksWon >= 1) Give(g, got, "rival");
            if (g.Rank >= 3) Give(g, got, "cand");
            if (g.Rank >= GameState.RankFrom.Length - 1) Give(g, got, "acad");
            int depts = g.Built("incline") ? 1 : 0;
            foreach (var t in Lab3D.Oddity.All) if (g.HasPerk(Lab3D.Oddity.Perk(t))) depts++;
            if (depts >= Lab3D.Oddity.All.Length + 1) Give(g, got, "odd7");
            if (g.HasPerk("ord3")) Give(g, got, "famous");
            int best = 0;
            foreach (var (scale, truth) in Best) if (g.HasPerk(scale) && g.HasPerk(truth)) best++;
            if (best >= 1) Give(g, got, "tool");
            if (best >= Best.Length) Give(g, got, "tools");
        }

        /// The morning: what the map has bought, how far the room has grown (stage 4 — no temporary walls left).
        public static List<AwardData> Morning(GameState g, int roomStage, int lastStage)
        {
            var got = new List<AwardData>();
            _deptsToday.Clear();
            Standing(g, got);
            if (roomStage >= lastStage) Give(g, got, "room");
            if (got.Count > 0) g.Save();
            return got;
        }

        /// The departments whose questions were answered today (the density's — the calculator — counts as its own).
        static readonly HashSet<string> _deptsToday = new HashSet<string>();

        /// An order delivered: its questions and how well each row was answered, its customer, the contract it closed.
        public static List<AwardData> Delivered(GameState g, string who, IList<string> tests, IList<double> rows, double accuracy,
                                                bool contractDone, string contractName)
        {
            var got = new List<AwardData>();
            if (!Has(g, "first")) Give(g, got, "first", Lab3D.Persona.Name(who));
            for (int i = 0; i < tests.Count && i < rows.Count; i++)
            {
                var t = tests[i];
                if (rows[i] >= 0.6) _deptsToday.Add(t == "mu" ? "angle" : t);   // the department that answered it
                if (Lab3D.Oddity.Is(t)) continue;
                if (rows[i] >= 0.98) Give(g, got, "eye");
                if (t == "bath" && rows[i] >= 0.6) Give(g, got, "eureka");
                if (t == "dens" && rows[i] >= 0.9) Give(g, got, "rho");
                if (t == "mu" && rows[i] >= 0.9) Give(g, got, "mu");
            }
            if (_deptsToday.Count >= 5) Give(g, got, "odd");
            if (Career.Stars(g, who) >= 3) Give(g, got, "regular", Lab3D.Persona.Name(who));
            if (contractDone) Give(g, got, "contract", contractName);
            Standing(g, got);
            return got;
        }

        /// The day ends: a clean day (every order of it delivered, each one accurate), the week against the rival, the rank.
        public static List<AwardData> DayEnd(GameState g, bool allDone)
        {
            var got = new List<AwardData>();
            var c = g.Career;
            if (allDone && c.todayDelivered >= 4 && c.todayGood >= c.todayDelivered) Give(g, got, "clean");
            Standing(g, got);
            return got;
        }
    }
}
