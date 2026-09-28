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
        // how long things take for an engaged player (the bot's pace, plus the curtains and the results card)
        const float ActionSec = 1.5f, LessonOverhead = 6f, BreakSec = 8f, SlotSec = 2.5f;
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
            sb.AppendLine($"Balance sim, {seeds} runs. Pace: {ActionSec} s per action, {BreakSec} s on the map, {SlotSec} s per letter in a theory.");
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
            sb.AppendLine("First run, experiment by experiment (earned in the run; wallet after the map):");
            foreach (var line in runs[0].Lessons) sb.AppendLine(line);
            return sb.ToString();
        }

        class Run
        {
            readonly System.Random _rng;
            GameState G;
            float _t;
            int _rr;
            readonly Dictionary<string, Sample> _res = new Dictionary<string, Sample>();   // automated stations' specimens
            Sample[] _sockets = new Sample[3];
            int _pendHang;
            double _pendAcc;
            public readonly Dictionary<string, (float min, int lesson)> At = new Dictionary<string, (float, int)>();
            public readonly List<string> Order = new List<string>();
            public readonly List<string> Lessons = new List<string>();

            public Run(int seed) { _rng = new System.Random(seed); }

            void Mark(string k) { if (At.ContainsKey(k)) return; At[k] = (_t / 60f, G.Lessons); Order.Add(k); }

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

            void Lesson()
            {
                G.StartLesson();
                _t += LessonOverhead;
                _res.Clear(); _sockets = new Sample[3]; _pendHang = 0; _pendAcc = 0;
                var before = (double[])G.Total.Clone();
                _by.Clear(); _earned.Clear();
                // the circuit's sockets are filled for free
                if (G.Built("ohm"))
                    for (int i = 0; i < Defs.StandSlots(G); i++)
                        _sockets[i] = G.BestSampleFor(Defs.F("ohm"), Free);
                while (G.ActionsLeft > 0 && G.Phase == Phase.Lesson)
                {
                    if (!Act()) break;
                    _t += ActionSec;
                }
                G.EndLesson();
                var run = new double[4];
                for (int i = 0; i < 4; i++) run[i] = G.Total[i] - before[i];
                if (Run0)
                {
                    Lessons.Add($"#{G.Lessons,-4} {_t / 60f,6:0.0} min  +{GameState.Fmt(run[0])} J  +{GameState.Fmt(run[1])} ideas  +{GameState.Fmt(run[2])} C  +{GameState.Fmt(run[3])} kcal   | {Stations().Count} instruments, {G.MaxActions} actions");
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

            /// One action, chosen the way AutoPlayBot chooses it.
            bool Act()
            {
                var all = Stations();
                if (G.LaunchMode) return Launch(all);
                string pick = null; Sample sample = null;
                int free = G.Samples.Count(Free);
                bool manualWants = all.Exists(st => !G.Automated(st) && NeedsSample(st));
                // 1. idle automatons get a specimen
                for (int i = all.Count - 1; i >= 0 && pick == null && !(manualWants && free <= 1); i--)
                {
                    var st = all[i];
                    if (!G.Automated(st) || !NeedsSample(st) || _res.ContainsKey(st)) continue;
                    sample = G.BestSampleFor(MainFormula(G, st), Free);
                    if (sample != null) pick = st;
                }
                // 2. weigh a couple of unknown specimens
                var unknown = G.Samples.FindAll(x => !x.knowM && Free(x));
                if (pick == null && G.Built("dyna") && !G.Automated("dyna") && unknown.Count > 0 && G.Samples.Count - unknown.Count < 2)
                { pick = "dyna"; sample = unknown[_rng.Next(unknown.Count)]; }
                // 2b. keep the steam bench warm
                if (pick == null && G.Built("heat") && !G.Automated("heater") && G.Boiler < 0.55f)
                { sample = G.BestSampleFor(Defs.F("heat"), Free); if (sample != null) pick = "heater"; }
                // 3. the manual stations in turn
                if (pick == null)
                {
                    var manual = new List<string>();
                    for (int i = all.Count - 1; i >= 0; i--) if (!G.Automated(all[i])) manual.Add(all[i]);
                    // on the real bench an instrument is busy for ~1.3 s after it fires, so the bot moves on to the
                    // next one; with no animations here the manual instruments simply take turns
                    _rr++;
                    int shift = _rr;
                    var order = new List<string>();
                    for (int i = 0; i < manual.Count; i++) order.Add(manual[(i + shift) % manual.Count]);
                    foreach (var st in order)
                    {
                        if (st == "stand") { if (_sockets.Take(Defs.StandSlots(G)).Any(x => x == null)) continue; pick = st; sample = null; break; }
                        if (NeedsSample(st)) { sample = G.BestSampleFor(MainFormula(G, st), Free); if (sample == null) continue; }
                        else sample = null;
                        pick = st; break;
                    }
                }
                if (pick == null)
                {   // nothing manual to do: push an automaton by hand
                    pick = all.FirstOrDefault(st => G.Automated(st) && (!NeedsSample(st) || _res.ContainsKey(st)));
                    if (pick == null) return false;
                    sample = _res.TryGetValue(pick, out var r) ? r : null;
                }
                Do(pick, sample, true, 1);
                G.UseAction();
                AutoAlong(pick);
                return true;
            }

            bool Launch(List<string> all)
            {
                for (int k = 0; k < G.PlaceBudget; k++)
                {
                    var st = all.LastOrDefault(s => NeedsSample(s) && !_res.ContainsKey(s));
                    if (st == null) break;
                    var smp = G.BestSampleFor(MainFormula(G, st), Free);
                    if (smp == null) break;
                    _res[st] = smp;
                }
                int whole = Whole();
                foreach (var st in all)
                {
                    if (st == "stand" ? _sockets.Take(Defs.StandSlots(G)).Any(x => x == null) : NeedsSample(st) && !_res.ContainsKey(st)) continue;
                    int times = G.Automated(st) ? Math.Max(1, whole) : 1;
                    var smp = _res.TryGetValue(st, out var r) ? r : null;
                    if (st == "pend") _pendHang++;
                    Do(st, smp, false, times);
                }
                G.UseAction();
                return true;
            }

            int Whole() { double n = G.AutoMult; int w = (int)Math.Floor(n); if (_rng.NextDouble() < n - w) w++; return w; }

            void AutoAlong(string except)
            {
                int whole = Whole();
                if (whole <= 0) return;
                foreach (var st in Stations())
                {
                    if (st == except || !G.Automated(st)) continue;
                    if (NeedsSample(st) && !_res.ContainsKey(st)) continue;
                    if (st == "pend") _pendHang++;
                    Do(st, _res.TryGetValue(st, out var r) ? r : null, false, whole);
                }
            }

            /// What the bench does: an automated instrument keeps the specimen it was given.
            void Do(string st, Sample sample, bool manual, int times)
            {
                if (manual && G.Automated(st) && NeedsSample(st) && sample != null) _res[st] = sample;
                for (int i = 0; i < times; i++)
                    switch (st)
                    {
                        case "fall": Fire("fall", sample, manual ? 1 : G.BallsPerDrop); break;
                        case "pend":
                            {   // every push is worth PendSwings swings, one idea-firing each; fractions carry over
                                if (manual) _pendHang = 0;
                                _pendAcc += G.PendSwings;
                                while (_pendAcc >= 1) { _pendAcc -= 1; Fire("pend", sample, G.PendFatigue(_pendHang)); }
                                break;
                            }
                        case "stand":
                            Fire("ohm", _sockets[0]); Fire("charge", _sockets[0]); Fire("pow", _sockets[0]); Fire("lamp", _sockets[0]);
                            Fire("cap", _sockets[1]); Fire("coil", _sockets[2]);
                            break;
                        case "engine": Fire("carnot", null); Fire("engine", null); break;
                        case "piston": Fire("gas", null); break;
                        case "heater": Fire("heat", sample); break;
                        case "calor": Fire("calm", sample); break;
                        default: Fire(MainFormula(G, st).id, sample); break;
                    }
            }

            void Fire(string id, Sample sample, double extra = 1)
            {
                if (!G.Built(id)) return;
                var f = Defs.F(id);
                double mult = (G.HasPerk("crit") && _rng.NextDouble() < 0.1 ? 2 : 1) * extra;
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
