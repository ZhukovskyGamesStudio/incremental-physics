using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The day's order sheets (DESIGN.md 2б, «Листки-заказы»): each dealt with what to find out and a sample of its
    /// story's shape, answered by eye off the apparatus, paid by how close the answers are (with a tip for a quick
    /// one), and gone — handed back to its customer, or taken away by one who would wait no longer.
    public class OrderBook : MonoBehaviour
    {
        /// How long a customer waits for its order, from the window to the parcel, in seconds of the day: 45, and 35 more
        /// for every question on the sheet.
        public static float PatienceFor(OrderSheet sheet) => PatienceFor(sheet.tests.Count);
        public static float PatienceFor(int questions) => 45 + 35 * Mathf.Max(1, questions);
        /// The tip for a quick order: up to half its reward, as much of it as the customer's patience is left (and the
        /// answers are right).
        public const double TipShare = 0.5;
        public static int Tip(int reward, double hurry, double accuracy) => (int)System.Math.Round(reward * TipShare * hurry * accuracy);
        /// All the customers lost, and all the tips, since the game began (the check's log, day by day).
        public static int LostAll, TipsAll;

        Lab3DGame _game;
        OrderSheet _open;

        GameState G => _game.G;
        List<OrderSheet> Sheets => _game.sheets;

        /// Orders paid today.
        public int Delivered { get; private set; }
        /// Customers who gave up waiting today (the check counts them).
        public int LostCount { get; private set; }
        /// The tip in the last order's coins.
        public int LastTip { get; private set; }
        /// An order has been paid (a tagged one; null for a sheet).
        public event System.Action<Order> OrderPaid;
        /// A sheet has been sent: what was written, the true weight, the coins.
        public event System.Action<OrderSheet, double, int> Sent;

        public void Init(Lab3DGame game) => _game = game;

        /// No more orders today (the evening, or all of the day's have come) and every one is sent: nothing is left but
        /// going home.
        public bool AllSent => (_game.day.Evening || _game.Line.AllCame) && !_game.Line.AnyIncoming && CounterDone;
        /// Every sheet on the counter is sent (more may be on their way).
        public bool CounterDone => Sheets.TrueForAll(s => !s.gameObject.activeSelf || s.sent);

        /// The sheet a sample lies on, if it does.
        public OrderSheet SheetOf(Sample3D s) => s == null || s.At != Sample3D.Where.Crate ? null : Sheets.Find(x => x.sample == s && x.SampleHome && x.gameObject.activeSelf);

        /// A tagged order has been handed in (TagOrders).
        public void Paid(Order o)
        {
            Delivered++;
            OrderPaid?.Invoke(o);
        }

        /// A new order on a sheet: what to find out, and its sample — a volume from 0.4 to 2.8 l and a density from 0.5 to
        /// 4.5 kg/l, to the hundredth; its size is its volume, its hatching its density.
        public void Deal(OrderSheet sh, int n, bool show = true)
        {
            sh.gameObject.SetActive(true);
            var odd = _game.OddTests();
            var fresh = odd.Find(t => !G.Used.Contains("3d:ordered:" + t));   // bought and not yet asked for: it gets this one
            if (fresh != null && !OrderSheet.forceQueue.Contains(fresh)) OrderSheet.forceQueue.Insert(0, fresh);
            // during a contract half the orders are its customer's (his stories, whatever the lab can measure for him)
            string cw = Career.ContractWho(G);
            int pending = Sheets.FindAll(x => x != sh && x.contract && !x.sent && (x.gameObject.activeSelf || _game.Line.Incoming(x))).Count;
            bool contract = cw != null && fresh == null && G.Career.contractGot + pending < G.Career.contractNeed && Random.value < 0.5f;
            for (int tries = 0; ; tries++)
            {
                sh.Deal(n, G.Built("bath"), G.Built("dens"), G.Built("incline"), odd, contract ? cw : null);
                if (!contract || sh.who == cw || tries >= 10) break;
            }
            sh.contract = contract && sh.who == cw;
            sh.reward = Mathf.RoundToInt((float)(sh.reward * Career.RewardMult(G, sh.who)));
            sh.note = Career.RewardNote(G, sh.who);
            foreach (var t in sh.tests) if (Oddity.Is(t)) G.MarkUsed("3d:ordered:" + t);
            sh.view.Fill();
            var s = sh.sample;
            s.gameObject.SetActive(true);
            // a volume from 0.4 to 2.8 l and a density from 0.5 to 4.5 kg/l: a weight the dynamometer can take
            bool small = sh.shape is "ring" or "bracelet" or "egg" or "pear";   // jewellery and the like: smaller
            double vol = System.Math.Round(Random.Range(0.4f, small ? 1.0f : 2.8f), 2);
            double mass = System.Math.Round(System.Math.Clamp(vol * Random.Range(0.5f, 4.5f), 0.6, 6.8), 2);
            var data = new Sample { id = G.Samples.Count, massR = mass, volR = vol, knowM = true, mu = System.Math.Round(Random.Range(0.2f, 0.9f), 2) };
            data.pattern = GameState.HatchFor(data);
            Oddity.Roll(data);
            G.Samples.Add(data);
            Dress(s, data);
            if (!show) { s.gameObject.SetActive(false); sh.gameObject.SetActive(false); }
        }

        /// A 3D sample takes the looks of a specimen of the game: size is the volume, hatching the density.
        public void Dress(Sample3D s, Sample data)
        {
            s.data = data;
            var sheet = Sheets.Find(x => x.sample == s);
            s.shape = sheet != null ? sheet.shape : "cube";
            s.variant = Random.Range(0, 4);
            s.volume = (float)data.volR;
            s.mass = (float)data.massR;
            s.name = $"Sample V={s.volume} m={s.mass}";
            s.Shape();
            float rho = s.mass / s.volume;
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = _game.sampleMats[rho < 0.9f ? 0 : rho > 2.2f ? 2 : 1];
            // each its own hatching: the strokes at their own slant and a little closer or wider apart
            Art3D.Prop(r, "_Angle", Random.Range(-1.3f, 1.3f));
            Art3D.Prop(r, "_Hatch", r.sharedMaterial.GetFloat("_Hatch") * Random.Range(0.7f, 1.4f));
            Art3D.Prop(r, "_Local", 1);
            s.gameObject.layer = data.glows ? Lab3DGame.GlowLayer : 0;   // in the dark the eye sees only this layer
            _game.Lights.Shine(s);
            s.GoHome();
            s.Snap();
        }

        /// The true value of a test on a sheet's sample: the weight F = mg, or the energy of a blow from the shelf's top
        /// E = mgh.
        public double Truth(OrderSheet sheet, string test)
        {
            var d = sheet.sample.data;
            return test switch
            {
                "bath" => d.volR, "dens" => d.massR / d.volR,
                "angle" => System.Math.Atan(d.mu) * 180 / System.Math.PI, "mu" => d.mu,
                _ => d.massR * G.V("g"),
            };
        }

        /// The answers written on the sheet: the order pays its reward times their accuracy, row by row averaged (all of it
        /// for the exact values, nothing for a quarter off). Returns the coins paid.
        public int Submit(OrderSheet sheet, out double accuracy, double hurry = 0)
        {
            accuracy = 0;
            LastTip = 0;
            if (sheet.sent) return 0;
            var rows = new List<double>();
            for (int i = 0; i < sheet.tests.Count; i++)
            {
                var t = sheet.tests[i];
                // a value worked out of two measurements (the density, the friction from the angle) carries both their
                // errors: it is judged more kindly; the angle off the protractor a little more kindly than a weight
                double tolerance = t is "dens" or "mu" ? 0.45 : t == "angle" ? 0.35 : 0.25;
                double row = Oddity.Is(t) ? Oddity.Score(t, sheet.answers[i], Oddity.Truth(sheet.sample.data, t))
                                          : GameState.Accuracy(sheet.Value(i), Truth(sheet, t), tolerance);
                rows.Add(row);
                accuracy += row;
            }
            accuracy /= Mathf.Max(1, sheet.tests.Count);
            int coins = (int)System.Math.Round(sheet.reward * accuracy);
            LastTip = Tip(sheet.reward, hurry, accuracy);   // quick and right: a tip
            coins += LastTip;
            TipsAll += LastTip;
            sheet.sent = true;
            _open = sheet;
            G.Earn(Cur.Coin, coins);
            Delivered++;
            OrderPaid?.Invoke(null);
            Sent?.Invoke(sheet, accuracy, coins);
            foreach (var c in _game.customers) if (c.Order == sheet) c.Served(accuracy);
            int contractsWere = G.Career.contractsDone;
            string contractName = G.Career.contract;
            _game.Hud.Queue(Career.Delivered(G, sheet.who, accuracy, coins, sheet.contract));
            _game.awards?.Earned(Awards.Delivered(G, sheet.who, sheet.tests, rows, accuracy, G.Career.contractsDone > contractsWere, contractName));
            _game.Hud.ShowMoney();
            return coins;
        }

        /// A sent sheet leaves the counter with its sample.
        public void Finish(OrderSheet sheet)
        {
            if (_open == sheet) _open = null;
            sheet.sample.GoHome();
            sheet.sample.gameObject.SetActive(false);
            _game.Desk.Unglide(sheet.leaf);
            sheet.view.Rest();
            sheet.gameObject.SetActive(false);
            if (AllSent)
            {
                var hud = _game.Hud;
                hud.Card("Все заказы выполнены", hud.DaySum, "домой — в дверь", 4f);
                hud.Queue(Career.CheckGoal(G, !_game.day.Evening));   // the goal «all the orders before six»
                hud.Tell(("Все заказы выполнены", hud.DaySum, "домой — в дверь"));   // and it stays up after the goal's card
            }
        }

        /// A customer has waited too long and goes: its order leaves the lab, wherever its sheet and its sample are.
        public void Lost(OrderSheet sheet)
        {
            if (sheet == null || sheet.sent) return;
            LostCount++;
            LostAll++;
            Career.Lost(G, sheet.who);
            _game.Hud.Card("Заказчик ушёл", "", "не дождался заказа №" + sheet.number, 2.2f);
            if (_game.Line.CallOff(sheet)) return;
            _game.Hands.Forget(sheet);
            foreach (var d in _game.departments)
                foreach (var a in d.apparatus)
                    if (a != null && a.Load == sheet.sample) a.Take();
            _game.Desk.Forget(sheet);
            Finish(sheet);
        }
    }
}
