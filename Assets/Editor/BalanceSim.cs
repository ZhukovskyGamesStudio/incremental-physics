using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ChalkPhysics.EditorTools
{
    /// Headless balance run: plays the whole game in a sandbox GameState the way AutoPlayBot does (same choices on
    /// the bench and on the map), without the bench's animations, and reports when every milestone is reached.
    /// Unity.exe -batchmode -quit -projectPath <project> -executeMethod ChalkPhysics.EditorTools.BalanceSim.Batch -simOut <file> [-simSeeds 5]
    public static class BalanceSim
    {
        // how long things take for an engaged player: a hand that moves a specimen or works an instrument (DESIGN.md 2а),
        // the curtains and the results card around every run, the map and the theories between runs
        const float LessonOverhead = 6f, BreakSec = 8f, SlotSec = 2.5f;
        const float DropSec = 0.6f, TapSec = 0.3f, ShelfSec = 0.7f, PullSec = 0.6f, PushSec = 0.6f;   // a drop and a sweep of the doors; a pull of the spring or the pendulum
        const float RunSec = 1.3f, SettleSec = 0.7f, HelperWait = 1.0f;
        const double BotQ = 0.95, HelperQ = 0.5, TaskOdds = 0.55;
        const float TimeLimit = 8 * 3600f;

        [MenuItem("Tools/Balance sim")]
        public static void Menu()
        {
            var path = Path.Combine(Application.dataPath, "../Logs/balance.txt");
            File.WriteAllText(path, Report(3));
            Debug.Log("[BalanceSim] written to " + Path.GetFullPath(path));
        }

        public static void Batch()
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string k, string def) { int i = Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
            string outPath = Arg("-simOut", Path.Combine(Application.dataPath, "../Logs/balance.txt"));
            int seeds = int.Parse(Arg("-simSeeds", "5"));
            int code = 0;
            try { File.WriteAllText(outPath, Report(seeds)); }
            catch (Exception e) { File.WriteAllText(outPath, "FAILED: " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static string Report(int seeds)
        {
            var runs = new List<Run>();
            for (int i = 0; i < seeds; i++) runs.Add(new Run(1000 + i).Play());
            var sb = new StringBuilder();
            sb.AppendLine($"Balance sim, {seeds} runs. Live runs (15 s and up), {BreakSec} s on the map, {SlotSec} s per letter in a theory.");
            sb.AppendLine("Minutes to each milestone (median, min..max over runs); 'lesson' = experiments done before it.");
            sb.AppendLine();
            var keys = runs[0].Order.ToList();
            foreach (var r in runs) foreach (var k in r.Order) if (!keys.Contains(k)) keys.Add(k);
            foreach (var k in keys)
            {
                var t = runs.Where(r => r.At.ContainsKey(k)).Select(r => r.At[k].min).OrderBy(x => x).ToList();
                var l = runs.Where(r => r.At.ContainsKey(k)).Select(r => r.At[k].lesson).OrderBy(x => x).ToList();
                if (t.Count == 0) continue;
                sb.AppendLine($"{k,-24} {t[t.Count / 2],7:0.0} min  ({t.First():0.0}..{t.Last():0.0})   lesson {l[l.Count / 2],4}" + (t.Count < runs.Count ? $"   reached in {t.Count}/{runs.Count}" : ""));
            }
            sb.AppendLine();
            sb.AppendLine("Rhythm of the map (all runs): purchases per visit, visits with nothing bought, the longest dry streak, the biggest burst.");
            for (int era = 0; era < 3; era++)
            {
                var v = runs.SelectMany(r => r.Visits.Where(x => x.era == era)).ToList();
                if (v.Count == 0) continue;
                int streak = 0;
                foreach (var r in runs) { int cur = 0; foreach (var x in r.Visits.Where(x => x.era == era)) { cur = x.buys == 0 ? cur + 1 : 0; streak = Math.Max(streak, cur); } }
                sb.AppendLine($"epoch {era}: {v.Count / runs.Count} visits per run, {v.Average(x => x.buys):0.0} buys per visit, {100.0 * v.Count(x => x.buys == 0) / v.Count:0}% dry, longest dry streak {streak}, biggest burst {v.Max(x => x.buys)}");
            }
            sb.AppendLine();
            sb.AppendLine("First run, experiment by experiment (earned in the run; wallet after the map):");
            foreach (var line in runs[0].Lessons) sb.AppendLine(line);
            return sb.ToString();
        }

        class Run
        {
            readonly System.Random _rng;
            GameState G;
            float _t;
            readonly Dictionary<string, Sample> _res = new Dictionary<string, Sample>();   // the specimen on each instrument
            Sample[] _sockets = new Sample[3];
            public readonly Dictionary<string, (float min, int lesson)> At = new Dictionary<string, (float, int)>();
            public readonly List<string> Order = new List<string>();
            public readonly List<string> Lessons = new List<string>();

            public Run(int seed) { _rng = new System.Random(seed); }

            void Mark(string k) { _bought.Add(k.Substring(k.IndexOf(' ') + 1)); if (At.ContainsKey(k)) return; At[k] = (_t / 60f, G.Lessons); Order.Add(k); }
            // the rhythm of the map: what was bought on every visit (letter levels counted too)
            readonly List<string> _bought = new List<string>();
            public readonly List<(int era, int buys, int lesson)> Visits = new List<(int, int, int)>();

            public Run Play()
            {
                GameState.Sandbox = true;
                GameState.SandboxRng = _rng;
                G = new GameState();
                G.NewGame();
                G.FormulaProven += id => Mark("theory " + id);
                G.LetterOpened += id => Mark("letter " + id);
                G.RevolutionDone += d => Mark("REVOLUTION " + d);
                G.GateBought += id => Mark("gate " + id);
                // the letters the game starts with are free: open whatever the map offers
                while (!G.Won && _t < TimeLimit && G.Lessons < 5000)
                {
                    Break();
                    if (G.Won) break;
                    if (!G.AnyStation) { _t += BreakSec; continue; }
                    Lesson();
                }
                if (G.Won) Mark("WON");
                GameState.Sandbox = false;
                return this;
            }

            // ---------------- the map ----------------
            void Break()
            {
                _bought.Clear();
                int era = G.Era;
                // theories: every open one is solved, letter by letter
                for (int guard = 0; guard < 50; guard++)
                {
                    var f = G.Discoverable().FirstOrDefault();
                    if (f == null) break;
                    foreach (var s in f.Slots().ToList()) { if (!G.IsFilled(f.id, s)) { G.Place(f.id, s, s); _t += SlotSec; } }
                }
                if (G.Won) return;
                _t += BreakSec;
                Spend();
                // what was just opened may already make a theory solvable
                for (int guard = 0; guard < 50; guard++)
                {
                    var f = G.Discoverable().FirstOrDefault();
                    if (f == null) break;
                    foreach (var s in f.Slots().ToList()) { if (!G.IsFilled(f.id, s)) { G.Place(f.id, s, s); _t += SlotSec; } }
                }
                if (G.Lessons > 0) Visits.Add((era, _bought.Count, G.Lessons));
                if (Run0 && G.Lessons > 0) Lessons.Add("        map: " + (_bought.Count == 0 ? "-" : string.Join(" ", _bought)));
            }

            /// The same shopping as AutoPlayBot.Spend.
            void Spend()
            {
                if (G.CanRevolt) { G.Revolt(); return; }
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
                foreach (var coin in new[] { Cur.Obs, Cur.J, Cur.C, Cur.Cal })
                    for (int buys = 0; buys < 3; buys++)
                    {
                        var perk = Defs.Perks.Where(p => !G.HasPerk(p.id) && p.cost == coin && !p.Teaser && G.PerkOpen(p) && p.domain <= G.Era).OrderBy(p => p.price).FirstOrDefault();
                        var dev = G.AvailableDevices().Where(d => d.cost == coin && G.ChainOpen(d.requires)).OrderBy(d => d.price).FirstOrDefault();
                        double have = G.Cur[(int)coin] * (coin == Cur.Obs ? 1 : 0.5);
                        if (perk != null && (dev == null || perk.price <= dev.price) && have >= perk.price) { G.BuyPerk(perk.id); Mark("perk " + perk.id); }
                        else if (dev != null && have >= dev.price) { G.BuyDevice(dev.id); Mark("device " + dev.id); }
                        else break;
                    }
                foreach (var p in Defs.Perks)
                    if (p.Teaser && !G.HasPerk(p.id) && p.domain <= G.Era && G.ChainOpen(p.requires) && G.Cur[(int)p.cost] >= p.price * 3) { G.BuyPerk(p.id); Mark("perk " + p.id); }
                double reserve = 0;
                foreach (var l in Defs.Letters) if (!G.Known.Contains(l.id) && !l.Derived && l.openCur == Cur.J && G.NodeVisible("L:" + l.id)) reserve = Math.Max(reserve, G.OpenCost(l.id));
                if (G.RevolutionOffered) reserve = Math.Max(reserve, G.NextRevolution.cost);
                BuyLetters(reserve > 0 ? 0.5 : 0.7, reserve);
            }

            void BuyLetters(double frac, double reserve)
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
                        if (d.cost == Cur.J) floor = Math.Max(floor, Math.Min(reserve, start[0] * 0.8));
                        if (c < bc && G.Cur[(int)d.cost] - c >= floor) { bc = c; best = id; }
                    }
                    if (best == null) break;
                    G.Upgrade(best);
                    _bought.Add(Defs.L(best).sym + "+");
                }
            }

            // ---------------- the bench ----------------
            List<string> Stations()
            {
                var all = new List<string>();
                foreach (var f in Defs.Formulas) if (G.Built(f.id) && f.station.Length > 0 && !all.Contains(f.station)) all.Add(f.station);
                return all;
            }

            static FormulaDef MainFormula(GameState g, string st) => Defs.Formulas.Find(x => x.station == st && g.Built(x.id));
            bool NeedsSample(string st) => st != "stand" && st != "engine" && st != "piston" && MainFormula(G, st).sample;
            bool Free(Sample s) => !_res.ContainsValue(s) && !_sockets.Contains(s);

            /// One live run: the bench is loaded, then a single hand works the instruments that have settled, the one waiting
            /// longest first; the pendulum gets a push now and then; the helpers work theirs, slowly and half well.
            void Lesson()
            {
                ChooseBench();
                G.PickBench(_rng);
                G.StartLesson();
                _res.Clear(); _sockets = new Sample[3];
                var before = (double[])G.Total.Clone();
                _by.Clear(); _earned.Clear();
                float T = G.LevelSeconds;
                var bench = G.Bench.ToList();
                var ready = new Dictionary<string, float>();
                float hand = 0;
                // the helpers take the instruments the hand would reach last: the guest, then the oldest
                var crew = new HashSet<string>();
                for (int i = 0; i < G.CrewSize && i < bench.Count - 1; i++) crew.Add(bench[bench.Count - 1 - i]);
                foreach (var st in bench)
                {
                    if (st == "stand")
                    {
                        for (int i = 0; i < Defs.StandSlots(G); i++) { _sockets[i] = G.BestSampleFor(Defs.F("ohm"), Free); hand += DropSec; }
                    }
                    else if (NeedsSample(st))
                    {
                        var smp = G.BestSampleFor(MainFormula(G, st), Free);
                        if (smp == null) continue;
                        _res[st] = smp; hand += DropSec;
                    }
                    ready[st] = hand + SettleSec;
                }
                var helperNext = new Dictionary<string, float>();
                foreach (var st in crew) if (ready.ContainsKey(st)) helperNext[st] = ready[st] + HelperWait - SettleSec;
                bool pend = ready.ContainsKey("pend");
                float pendUntil = -1, nextPush = pend ? ready["pend"] - SettleSec : 1e9f, nextSwing = 1e9f, period = G.SwingPeriod;
                double pendQ = 0.85;
                if (pend && crew.Contains("pend")) { pendUntil = T; nextSwing = ready["pend"] + period; pendQ = 0.6; nextPush = 1e9f; }
                float last = 0;
                void AdvanceTo(float to)
                {
                    to = Mathf.Min(to, T);
                    G.Boiler = Mathf.Max(0, G.Boiler - G.BoilerCool * (to - last));
                    last = to;
                    if (pend) for (; nextSwing <= to; nextSwing += period) if (nextSwing <= pendUntil) Fire("pend", _res["pend"], pendQ);
                    foreach (var st in crew)
                    {
                        if (st == "pend" || !helperNext.ContainsKey(st)) continue;
                        while (helperNext[st] <= to)
                        {
                            float busy = Work(st, HelperQ);
                            helperNext[st] += busy + HelperWait;
                        }
                    }
                }
                while (hand < T)
                {
                    AdvanceTo(hand);
                    if (pend && hand >= nextPush)
                    {   // a push keeps it swinging for a few more periods
                        if (nextSwing > 1e8f) nextSwing = hand + period;
                        pendUntil = hand + 4.5f; nextPush = hand + 4f; hand += PushSec;
                        continue;
                    }
                    string pick = null; float oldest = float.MaxValue;
                    foreach (var st in bench)
                    {
                        if (st == "pend" || crew.Contains(st) || !ready.ContainsKey(st)) continue;
                        if (ready[st] <= hand && ready[st] < oldest) { oldest = ready[st]; pick = st; }
                    }
                    if (pick == null)
                    {   // nothing has settled: the hand waits for the next one (or the next push)
                        float next = nextPush;
                        foreach (var st in bench) if (st != "pend" && !crew.Contains(st) && ready.ContainsKey(st)) next = Mathf.Min(next, ready[st]);
                        hand = next > hand && next < 1e8f ? next : hand + 0.1f;
                        continue;
                    }
                    float cost = pick == "fall" ? ShelfSec : pick == "spring" ? PullSec * 2 : pick == "dyna" ? PullSec : TapSec;
                    double q = pick == "fall" ? 0.925 : pick == "spring" ? 0.925 : pick == "dyna" ? 0.9 : BotQ;
                    hand += cost;
                    float b2 = Work(pick, q);
                    ready[pick] = hand + b2 + SettleSec;
                    // a measured specimen gives half the ideas: swap an unknown one in when there is one
                    var mf = MainFormula(G, pick);
                    if (mf.reveals != null && _res.TryGetValue(pick, out var was) && was.Knows(mf.reveals))
                    {
                        var fresh = G.Samples.FirstOrDefault(x => !x.Knows(mf.reveals) && Free(x));
                        if (fresh != null) { _res[pick] = fresh; hand += DropSec; ready[pick] += DropSec; }
                    }
                }
                AdvanceTo(T);
                // the tasks and the exam, as odds: a fair player finishes about half the tasks and rarely errs
                int tasks = 0;
                for (int k = 0; k < G.TaskSlots; k++) if (_rng.NextDouble() < TaskOdds) tasks++;
                if (tasks > 0) { var got = new double[4]; for (int i = 0; i < 4; i++) got[i] = G.Total[i] - before[i]; for (int i = 0; i < 4; i++) G.Earn((Cur)i, got[i] * tasks * GameState.TaskBonus); }
                if (G.QualificationOpen) { if (_rng.NextDouble() < (G.Rank < 3 ? 0.85 : 0.7)) G.RankUp(); else G.RankDown(); }
                _t += LessonOverhead + T;
                Shares();
                G.EndLesson();
                var run = new double[4];
                for (int i = 0; i < 4; i++) run[i] = G.Total[i] - before[i];
                if (Run0)
                {
                    Lessons.Add($"#{G.Lessons,-4} {_t / 60f,6:0.0} min  +{GameState.Fmt(run[0])} J  +{GameState.Fmt(run[1])} ideas  +{GameState.Fmt(run[2])} C  +{GameState.Fmt(run[3])} kcal   | bench {string.Join(",", bench)} (guest {G.Guest ?? "-"}), {T:0} s, crew {G.CrewSize}, rank {G.Rank}");
                    // where it came from, and the letters behind it
                    Lessons.Add("        by: " + string.Join("  ", _by.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + GameState.Fmt(kv.Value))));
                    if (G.Lessons % 3 == 0) Lessons.Add("        payback: " + Paybacks());
                    Lessons.Add("        letters: " + string.Join("  ", G.Known.Where(id => !Defs.L(id).Derived && !Defs.L(id).perSample).Select(id => $"{Defs.L(id).sym}{G.Level(id)}={G.LetterValue(id)}")) + $"   wallet {GameState.Fmt(G.Cur[0])} J {GameState.Fmt(G.Cur[1])} ideas   owns {G.Perks.Count} perks {G.Devices.Count} devices");
                }
            }
            bool Run0 => Lessons.Count < 1200;
            readonly Dictionary<string, double> _by = new Dictionary<string, double>();
            readonly Dictionary<(string f, Cur c), double> _earned = new Dictionary<(string, Cur), double>();

            /// The comparable choice, measured: how many experiments each upgrade on offer takes to pay for itself, judging
            /// by what every instrument earned in the run just played. Levels of letters and the next ×N perks.
            string Paybacks()
            {
                var items = new List<(double exps, string text)>();
                double Gain(Cur c, Func<FormulaDef, double> ratio)
                {
                    double g = 0;
                    foreach (var kv in _earned) if (kv.Key.c == c) g += kv.Value * (ratio(Defs.F(kv.Key.f)) - 1);
                    return g;
                }
                foreach (var id in G.Known)
                {
                    var d = Defs.L(id);
                    if (d.Derived || d.perSample || G.AtLimit(id)) continue;
                    double cost = G.UpgradeCost(id);
                    double r(FormulaDef f)
                    {
                        var yc = f.kind == FKind.Energy ? f.yields : Cur.Obs;
                        double now = G.YieldOf(f, yc);
                        return now <= 0 ? 1 : G.WithNextLevel(id, () => G.YieldOf(f, yc)) / now;
                    }
                    double gain = Gain(d.cost, r);
                    if (gain > 0) items.Add((cost / gain, $"{d.sym}↑{GameState.Fmt(cost)}{Coin(d.cost)}={cost / gain:0.#}"));
                }
                foreach (var p in Defs.Perks)
                {
                    if (p.kind != PerkKind.StationMult || G.HasPerk(p.id) || !G.PerkOpen(p) || p.domain > G.Era || p.Teaser) continue;
                    double gain = Gain(p.cost, f => f.station == p.station ? p.value : 1);
                    if (gain > 0) items.Add((p.price / gain, $"{p.id}{GameState.Fmt(p.price)}{Coin(p.cost)}={p.price / gain:0.#}"));
                }
                return string.Join("  ", items.OrderBy(x => x.exps).Take(12).Select(x => x.text)) + "   (experiments to pay back)";
            }
            static string Coin(Cur c) => c == Cur.J ? "J" : c == Cur.Obs ? "i" : c == Cur.C ? "C" : "K";

            /// The bot's own places: the instruments that brought the biggest share of some currency last time (a new
            /// one first, to try it), so every coin keeps its source.
            readonly Dictionary<string, double> _share = new Dictionary<string, double>();
            void ChooseBench()
            {
                var all = G.BuiltStations();
                var order = all.OrderByDescending(st => _share.TryGetValue(st, out var v) ? v : 10.0).ToList();
                G.Loadout.Clear();
                foreach (var st in order) if (G.Loadout.Count < G.OwnSlots) G.Loadout.Add(st);
            }
            void Shares()
            {
                var tot = new double[4];
                foreach (var kv in _earned) tot[(int)kv.Key.c] += kv.Value;
                foreach (var st in G.Bench) _share[st] = 0;
                foreach (var kv in _earned)
                {
                    var st = Defs.F(kv.Key.f).station;
                    if (tot[(int)kv.Key.c] > 0) _share[st] = Math.Max(_share.TryGetValue(st, out var v) ? v : 0, kv.Value / tot[(int)kv.Key.c]);
                }
            }

            /// One run of an instrument, done this well; returns how long the instrument is busy with it.
            float Work(string st, double q)
            {
                G.MarkUsed(st);
                var smp = _res.TryGetValue(st, out var r) ? r : null;
                switch (st)
                {
                    case "fall":
                        for (int k = 0; k < G.BallsPerDrop; k++) Fire("fall", smp, q);
                        return 0.6f + 1.6f;                                   // the fall, then the bounces on the peg
                    case "spring": Fire("spring", smp, q); return 1.7f;       // the jump (its measure is the hand's time)
                    case "dyna": Fire("dyna", smp, q); return 2.4f;           // the spring settling before a good measure
                    case "stand":
                        Fire("ohm", _sockets[0], q); Fire("charge", _sockets[0], q); Fire("pow", _sockets[0], q); Fire("lamp", _sockets[0], q);
                        Fire("cap", _sockets[1], q); Fire("coil", _sockets[2], q);
                        return RunSec;
                    case "engine": Fire("carnot", null, q); Fire("engine", null, q); return RunSec;
                    case "piston": Fire("gas", null, q); return RunSec;
                    case "heater": Fire("heat", smp, q); return RunSec;
                    case "calor": Fire("calm", smp, q); return RunSec;
                    case "arch": Fire("arch", smp, q); return 0.4f;
                    default: Fire(MainFormula(G, st).id, smp, q); return RunSec;
                }
            }

            void Fire(string id, Sample sample, double extra = 1)
            {
                if (!G.Built(id)) return;
                var f = Defs.F(id);
                double mult = (G.HasPerk("crit") && _rng.NextDouble() < 0.1 ? 2 : 1) * extra * (f.station == G.Guest ? G.GuestMult : 1);
                var yc = f.kind == FKind.Energy ? f.yields : Cur.Obs;
                double y = G.YieldOf(f, yc, sample) * mult;
                _by[id + (yc == Cur.Obs ? "(i)" : "")] = (_by.TryGetValue(id + (yc == Cur.Obs ? "(i)" : ""), out var was) ? was : 0) + y;
                _earned[(id, yc)] = (_earned.TryGetValue((id, yc), out var e0) ? e0 : 0) + y;
                G.Fire(f, sample, mult);
                if (id == "heat") G.Boiler = Mathf.Min(1f, G.Boiler + G.BoilerRise);
            }
        }
    }
}
