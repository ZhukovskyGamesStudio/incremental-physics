using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChalkPhysics
{
    /// Dev tool: plays lessons and spends at breaks like an engaged human. Enable on GameRoot ("autoPlayBot").
    public class AutoPlayBot : MonoBehaviour
    {
        public float thinkPerLetter = 2.5f;      // seconds to put one letter into a draft
        public float actionEvery = 0.45f;        // seconds between moves of the hand in a run
        public float breakTime = 6f;             // seconds spent on the map

        public static readonly List<string> Lines = new List<string>();
        GameState G => GameState.I;
        float _think, _act, _break;
        int _rr;                                 // round-robin over the manual instruments
        Apparatus _app;

        void Start()
        {
            _app = FindAnyObjectByType<Apparatus>(FindObjectsInactive.Include);
            G.FormulaProven += id => { Log("formula " + id); if (Defs.F(id).kind == FKind.Energy) LogYields(); };
            G.LetterOpened += id => Log("letter " + id);
            G.GateBought += id => Log("gate " + id);
            G.RevolutionDone += d => Log("REVOLUTION -> " + Defs.DomainNames[d]);
            G.LessonEnded += () => Log("bell");
            Log("start");
        }

        /// Balance check: what every built energy station gives per firing with its best sample, at this moment.
        void LogYields()
        {
            var sb = new System.Text.StringBuilder("yields:");
            foreach (var f in Defs.Formulas)
            {
                if (!G.Built(f.id) || f.kind != FKind.Energy) continue;
                sb.Append(" " + f.id + "=" + GameState.Fmt(G.YieldOf(f, f.yields, G.BestSampleFor(f))));
            }
            Log(sb.ToString());
        }

        void Log(string s)
        {
            string line = $"[BOT] {G.PlayTime / 60f:0.0} min | lesson {G.Lessons} | {s} | J={GameState.Fmt(G.Cur[0])} O={GameState.Fmt(G.Cur[1])} C={GameState.Fmt(G.Cur[2])} K={GameState.Fmt(G.Cur[3])}";
            Lines.Add(line);
            Debug.Log(line);
        }

        void Update()
        {
            if (G == null || _app == null) return;
            if (G.Won) { enabled = false; Log("WON"); return; }
            float dt = Time.deltaTime;

            if (G.Phase == Phase.Lesson)
            {
                _act += dt;
                if (_act < actionEvery) return;
                _act = 0;
                _app.BotStart();
                _app.BotCrew();
                if (Random.value < 0.3f) _app.BotExam();
                // an empty instrument gets a specimen (the measuring ones an unknown one first) …
                foreach (var st in G.Bench)
                {
                    if (st == "stand")
                    {
                        if (!_app.CircuitWants) continue;
                        var sm = G.BestSampleFor(Defs.F("ohm"), _app.Free);
                        if (sm != null && _app.BotFeed("stand", sm)) return;
                        continue;
                    }
                    if (!_app.NeedsSampleAt(st) || _app.HasResident(st)) continue;
                    var f = Defs.Formulas.Find(x => x.station == st && G.Built(x.id));
                    var s = G.BestSampleFor(f, _app.Free);
                    if (s != null && _app.BotFeed(st, s)) return;
                }
                // … then the instruments that have settled are worked in turn
                for (int i = 0; i < G.Bench.Count; i++)
                {
                    var st = G.Bench[(_rr + i) % G.Bench.Count];
                    if (_app.BotWork(st)) { _rr++; return; }
                }
                return;
            }

            // break: puzzle out any formula whose letters are all open (the alchemy), shop, then start the next lesson
            var next = G.Discoverable().FirstOrDefault();
            if (next != null)
            {   // the puzzle: one question mark per think
                _think += dt;
                if (_think >= thinkPerLetter)
                {
                    _think = 0;
                    var slot = next.Slots().FirstOrDefault(s => !G.IsFilled(next.id, s));
                    if (slot != null) { G.Place(next.id, slot, slot); if (G.Built(next.id)) Log("discovered " + next.id); }
                }
                return;
            }
            _break += dt;
            if (_break >= breakTime)
            {
                _break = 0;
                Spend();
                if (!G.AnyStation) { _break = breakTime - 0.5f; return; }
                GameRoot.I.StartLesson(false);
            }
        }

        void Spend() => Shop(G, Log);

        /// The bot's break on the map: a revolution when it can, else every letter on offer, the cheapest upgrades and
        /// letter levels. The 3D check (Editor/Lab3DCheck) shops between its days with it too.
        public static void Shop(GameState G, System.Action<string> Log)
        {
            if (G.CanRevolt) { G.Revolt(); return; }
            // joules: open every letter on offer (they open new formulas), then the gate; keep the rest for levels
            for (int guard = 0; guard < 4; guard++)
            {
                string open = null;
                foreach (var l in Defs.Letters) if (G.CanOpen(l.id)) { open = l.id; break; }
                if (open == null) break;
                G.OpenLetter(open);
            }
            foreach (var g in Defs.Gates) if (G.CanBuyGate(g.id)) G.BuyGate(g.id);
            // upgrades, in each coin: ideas buy the cheapest few; joules buy a machine only while it costs at most half the
            // wallet, since the rest goes to letter levels
            foreach (var coin in new[] { Cur.Obs, Cur.J, Cur.C, Cur.Cal, Cur.Coin })
                for (int buys = 0; buys < 3; buys++)
                {
                    var perk = Defs.Perks.Where(p => !G.HasPerk(p.id) && p.cost == coin && !p.Teaser && G.PerkOpen(p) && p.domain <= G.Era).OrderBy(p => p.price).FirstOrDefault();
                    var dev = G.AvailableDevices().Where(d => d.cost == coin && G.ChainOpen(d.requires)).OrderBy(d => d.price).FirstOrDefault();
                    double have = G.Cur[(int)coin] * (coin is Cur.Obs or Cur.Coin ? 1 : 0.5);
                    if (perk != null && (dev == null || perk.price <= dev.price) && have >= perk.price) { G.BuyPerk(perk.id); Log("perk " + perk.id); }
                    else if (dev != null && have >= dev.price) { G.BuyDevice(dev.id); Log("device " + dev.id); }
                    else break;
                }
            foreach (var p in Defs.Perks)
                if (p.Teaser && !G.HasPerk(p.id) && p.domain <= G.Era && G.ChainOpen(p.requires) && G.Cur[(int)p.cost] >= p.price * 3) { G.BuyPerk(p.id); Log("perk " + p.id); }
            // levels: spend a share of the joules, saving up when something big is on the map
            double reserve = 0;
            foreach (var l in Defs.Letters) if (!G.Known.Contains(l.id) && !l.Derived && l.openCur == Cur.J && G.NodeVisible("L:" + l.id)) reserve = System.Math.Max(reserve, G.OpenCost(l.id));
            if (G.RevolutionOffered) reserve = System.Math.Max(reserve, G.NextRevolution.cost);
            BuyLetters(G, reserve > 0 ? 0.5 : 0.7, reserve);
        }

        static void BuyLetters(GameState G, double frac, double reserve)
        {
            var start = (double[])G.Cur.Clone();
            for (int guard = 0; guard < 60; guard++)
            {
                string best = null; double bc = double.MaxValue;
                foreach (var id in G.Known)
                {
                    var d = Defs.L(id);
                    if (d.Derived || G.AtLimit(id) || !G.CanUpgrade(id)) continue;
                    double c = G.UpgradeCost(id);
                    double floor = start[(int)d.cost] * (1 - frac);
                    if (d.cost == Cur.J) floor = System.Math.Max(floor, System.Math.Min(reserve, start[0] * 0.8));
                    if (c < bc && G.Cur[(int)d.cost] - c >= floor) { bc = c; best = id; }
                }
                if (best == null) break;
                G.Upgrade(best);
            }
        }
    }
}
