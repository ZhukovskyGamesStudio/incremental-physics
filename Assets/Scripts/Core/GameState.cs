using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ChalkPhysics
{
    public enum Phase { Lesson, Break }
    public enum PlaceResult { Ok, WrongUnit, WrongSameUnit, Occupied, NoResearch }

    [Serializable]
    public class SaveData
    {
        public double[] cur = new double[GameState.NCur];
        public double[] total = new double[GameState.NCur];
        public float time;
        public int revolutions, lessons;
        public bool won, muted;
        public List<string> known = new List<string>(), proven = new List<string>(), devices = new List<string>(), perks = new List<string>(), used = new List<string>(), gates = new List<string>(), filled = new List<string>();
        public List<string> builtIds = new List<string>();
        public List<int> builtVals = new List<int>();
        public List<string> lvlIds = new List<string>();
        public List<int> lvlVals = new List<int>();
        public int helpers, rank;
        public List<string> loadout = new List<string>();
        public CareerData career = new CareerData();   // the goals between the days (Career)
    }

    /// A specimen from the box: hidden mass and volume until measured.
    public class Sample
    {
        public int id;
        public double massR, volR, mu;  // multipliers of the letters m and V; roughness 0..1
        public bool knowM, knowV, knowMu, golden, knowGold;
        public int pattern;             // hatch style: the denser the specimen, the more chalk in it (see HatchFor)
        public bool Knows(string prop) => prop == "m" ? knowM : prop == "V" ? knowV : prop == "mu" ? knowMu : true;
        public bool Floats => massR / volR < 1.2;
        public int uses;
        /// The 3D lab's odd measurements (DESIGN.md 2б, «Крейзи-измерения»): its tastes (a bit each: sweet, salty, sour,
        /// bitter, umami), the fish that bites on it (0..4, 5 — nobody), what the cat does (purrs, hisses, plays, sleeps on),
        /// the note it rings (0..6, do to si); does it glow in the dark, show in a mirror, giggle when tickled.
        public int taste, fish, cat, note;
        public bool glows, reflects = true, ticklish;
        /// Instruments this very specimen has already run on: only there may its result be shown in advance.
        public readonly HashSet<string> seenAt = new HashSet<string>();
    }

    /// All game rules and data. The bench calls Fire() whenever an experiment happens.
    /// A lesson gives the player a limited number of actions; when they run out the bell rings and the map opens.
    /// On the map the player opens letters for joules (the tree grows layer by layer) and buys devices and perks
    /// for observations; formulas are found on the alchemy screen by combining letters.
    public class GameState
    {
        public static GameState I;
        public static float DevSpeed = 1f;
        /// A run lasts this long before the seconds bought on the map.
        public const float BaseSeconds = 15f;

        /// How many currencies there are (Cur): joules, ideas, coulombs, kilocalories, coins.
        public const int NCur = 5;
        public readonly double[] Cur = new double[NCur];
        public readonly double[] Total = new double[NCur];
        public readonly double[] LessonEarned = new double[NCur];
        public float PlayTime;
        public int Revolutions, Lessons;
        /// Seconds left in the run; when they run out the bell rings.
        public float TimeLeft;
        public bool Won;
        public Phase Phase = Phase.Break;

        public readonly HashSet<string> Proven = new HashSet<string>();
        public readonly HashSet<string> Devices = new HashSet<string>();
        public readonly HashSet<string> Perks = new HashSet<string>();
        public readonly HashSet<string> Gates = new HashSet<string>();
        public readonly HashSet<string> Used = new HashSet<string>();      // bench hints stop after the first use
        public readonly List<string> Known = new List<string>();           // opened letters
        // ---- the live run: up to four instruments on the bench — the player's own places and one guest
        /// The instruments on the bench in this run (the player's picks and the guest).
        public readonly List<string> Bench = new List<string>();
        /// The guest of this run: a random instrument the player did not pick (null when there is none).
        public string Guest;
        /// What the player picked for their own places last time; the next run starts from the same choice.
        public readonly List<string> Loadout = new List<string>();
        /// Chalk helpers the player has: each finished task brings one. They outlive revolutions.
        public int Helpers;
        /// The qualification: every formula assembled right on the bench is a point; enough points in a row make the next
        /// rank, and a mistake sends the rank a step back.
        public int RankPts;
        /// The 3D lab's goals between the days: the grant, the regulars, the contract, the rival (Career).
        public CareerData Career = new CareerData();
        public readonly Dictionary<string, HashSet<string>> Filled = new Dictionary<string, HashSet<string>>();   // puzzle: formula -> letters placed
        public readonly Dictionary<string, int> Wrong = new Dictionary<string, int>();                            // puzzle: "formula:slot" -> misses
        public readonly Dictionary<string, int> BuiltAt = new Dictionary<string, int>();   // formula -> Lessons when it was found
        readonly Dictionary<string, int> _lvl = new Dictionary<string, int>();
        public readonly Dictionary<string, int> LessonFires = new Dictionary<string, int>();
        public readonly List<Sample> Samples = new List<Sample>();
        public Sample ActiveSample;      // the sample an experiment is being evaluated with
        public int Selected = -1;        // sample picked by the player on the bench

        public event Action Changed, LessonStarted, LessonEnded;
        public event Action<string> FormulaProven, DeviceBought, PerkBought, LetterOpened, GateBought;
        public event Action<Sample> GoldFound;
        bool _preview;                   // while true, a golden sample counts as golden only once the player knows it
        public event Action<int> RevolutionDone;

        // the save's name changes whenever old saves would no longer make sense (8: the rebalance of every epoch)
        static string SavePath => Path.Combine(Application.persistentDataPath, "chalk_save10.json");
        // in a browser the save lives in PlayerPrefs (IndexedDB), which is flushed for sure on Save()
        const string SaveKey = "chalk_save10";
        static bool Web => Application.platform == RuntimePlatform.WebGLPlayer;
        /// The balance simulation (Editor/BalanceSim) plays in a sandbox: no save is read or written, and the boxes
        /// of specimens come from a seeded generator so that runs can be compared.
        public static bool Sandbox;
        public static System.Random SandboxRng;
        /// The 3D check (Editor/Lab3DCheck) goes between the map and the day as the player does, through the save; it keeps
        /// that save in memory, so that the player's own one is never touched.
        public static bool MemorySave;
        static string _memory;
        static void WriteSave(string json) { if (MemorySave) { _memory = json; return; } if (Sandbox) return; if (Web) { PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save(); } else File.WriteAllText(SavePath, json); }
        static string ReadSave() => MemorySave ? _memory : Sandbox ? null : Web ? PlayerPrefs.GetString(SaveKey, "") : File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;

        public GameState()
        {
            I = this;
            if (!Load()) NewGame();
        }

        public void NewGame()
        {
            for (int i = 0; i < NCur; i++) Cur[i] = Total[i] = 0;
            PlayTime = 0; Revolutions = 0; Lessons = 0; Won = false;
            Proven.Clear(); Known.Clear(); Devices.Clear(); Perks.Clear(); Gates.Clear(); Used.Clear(); Filled.Clear(); Wrong.Clear(); BuiltAt.Clear(); _lvl.Clear();
            Bench.Clear(); Loadout.Clear(); Guest = null; Helpers = 0; RankPts = 0; Career = new CareerData();
            Phase = Phase.Break;
        }

        // ---------------- values ----------------
        public int Level(string id) => _lvl.TryGetValue(id, out var l) ? l : 1;

        public double V(string id)
        {
            var d = Defs.L(id);
            if (d.Derived) return Defs.F(d.derivedFrom).eval(this);
            if (d.perSample) return ActiveSample != null ? ActiveSample.mu : d.baseV;
            double v = ValueAt(id, Level(id));
            // a specimen changes only what is written in the formula: its own mass and volume
            if (ActiveSample != null)
            {
                if (id == "m") v *= ActiveSample.massR;
                if (id == "V") v *= ActiveSample.volR;
            }
            return v;
        }

        public double ValueAt(string id, int level)
        {
            var d = Defs.L(id);
            double v = d.mul ? d.baseV * Math.Pow(d.step, level - 1) : d.baseV + d.step * (level - 1);
            return Math.Min(d.max, Math.Max(d.min, v));
        }

        public bool AtLimit(string id)
        {
            var d = Defs.L(id);
            if (d.Derived || d.perSample) return true;
            double v = V(id);
            return d.mul && d.step < 1 ? v <= d.min + 1e-9 : v >= d.max - 1e-9;
        }

        public double UpgradeCost(string id)
        {
            var d = Defs.L(id);
            return Math.Round(d.cost0 * Math.Pow(d.costK, Level(id) - 1));
        }

        /// A letter of an epoch the lab has not reached cannot be levelled.
        public bool CanUpgrade(string id) => !Day3D && Known.Contains(id) && !Defs.L(id).Derived && !Defs.L(id).perSample && Defs.L(id).domain <= Era && UpgradesOpen && !AtLimit(id) && Cur[(int)Defs.L(id).cost] >= UpgradeCost(id);

        public bool Upgrade(string id)
        {
            if (!CanUpgrade(id)) return false;
            Cur[(int)Defs.L(id).cost] -= UpgradeCost(id);
            _lvl[id] = Level(id) + 1;
            Changed?.Invoke();
            return true;
        }

        public double WithNextLevel(string id, Func<double> f)
        {
            int old = Level(id);
            _lvl[id] = old + 1;
            double r = f();
            _lvl[id] = old;
            return r;
        }

        /// No epoch multiplies anything: a revolution brings a new element, not a bigger number.
        public double Mult => DevSpeed;
        /// The epoch the game is in: only its tree, letters and upgrades are on the board.
        public int Era => Math.Min(Revolutions, 2);
        public bool Built(string formulaId) => Proven.Contains(formulaId);
        public bool Has(string device) => Devices.Contains(device);
        public bool HasPerk(string perk) => Perks.Contains(perk);
        /// Whether the bench has anything to do at all.
        public bool AnyStation { get { foreach (var f in Defs.Formulas) if (Built(f.id) && f.station.Length > 0) return true; return false; } }

        // ---------------- bench parameters ----------------
        double PerkSum(PerkKind k) { double n = 0; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == k) n += d.value; } return n; }
        /// How long a run lasts: 15 seconds and whatever the map has added.
        public float LevelSeconds => BaseSeconds + (float)PerkSum(PerkKind.Seconds);

        // ---- the 3D lab (DESIGN.md 2б): a run is a working day there, and the map sells what that day can use
        /// The run is a day in the 3D lab (set by GameRoot and the lab). The balance plays and the 2D bot keep it false.
        public static bool Day3D;
        /// A working day is from eight to six, and every perk of seconds makes it longer by hours: two for 3 s, three
        /// for 5 s — the same share of the day as of a 15-second run.
        public const float SecondsPerHour = 12;
        public static int HoursOf(PerkDef p) => (int)Math.Round(p.value * 2 / 3);
        public int DayHours { get { int h = 10; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Seconds) h += HoursOf(d); } return h; } }
        public float DaySeconds => DayHours * SecondsPerHour;
        /// What the map offers: in the 3D lab every instrument has its own table, and there is neither a random
        /// instrument nor tasks, so the perks for those would buy nothing (DESIGN.md 2, «Ни одной бесполезной покупки»).
        /// In the 3D lab only the order sheets' loop is played for now (DESIGN.md 2б, «Листки-заказы»): the map sells what
        /// coins buy, and nothing else.
        public static bool InPlay(PerkDef p) => Day3D ? p.cost == ChalkPhysics.Cur.Coin : !p.lab3D;
        public static string PerkTitle(PerkDef p) => Day3D && p.id == "act0" ? "Лишние часы" : p.title;
        public static string PerkDesc(PerkDef p) => Day3D && p.kind == PerkKind.Seconds ? $"+{HoursOf(p)} ч к рабочему дню" : p.desc;
        // ---- the lab's orders (DESIGN.md 2б, «Заказы»): samples come in to the counter, each with what to find out
        /// Chalk helpers in the lab: they carry the orders to the apparatus and work them.
        public int Crew => (int)PerkSum(PerkKind.Crew);
        /// How many apparatus of a formula stand in its department.
        public int Copies(string station) { int n = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Copies && d.station == station) n++; } return n; }
        /// Orders on the counter at once.
        public int CounterSlots => Math.Min(8, 4 + (int)PerkSum(PerkKind.Counter));
        /// Seconds of the day between two orders coming in.
        public float OrderEvery { get { float t = 9; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Flow) t /= (float)d.value; } return t; } }

        /// One test of an order: the formula's value on the sample times `share` (how well it was done), in its coin.
        /// Nothing is earned yet: the order pays when it goes back to the counter. The bookkeeping is Fire's.
        public double Measure(FormulaDef f, Sample sample, double share, out Cur cur)
        {
            cur = f.kind == FKind.Energy ? f.yields : ChalkPhysics.Cur.Obs;
            double v = YieldOf(f, cur, sample) * share;
            LessonFires[f.id] = (LessonFires.TryGetValue(f.id, out var n) ? n : 0) + 1;
            sample.uses++;
            sample.seenAt.Add(f.station);
            if (f.reveals == "m") sample.knowM = true;
            if (f.kind == FKind.Energy && sample.golden && !sample.knowGold) { sample.knowGold = true; GoldFound?.Invoke(sample); }
            return v;
        }

        // ---- the order sheets (DESIGN.md 2б, «Листки-заказы»): the dynamometer is read by eye, and it is not quite true
        /// The value of the best of the perks bought from `ids` (the last one bought is the finest), or `none`.
        double Best(string[] ids, double none) { double v = none; foreach (var id in ids) if (HasPerk(id)) v = Math.Min(v, Defs.Pk(id).value); return v; }
        /// The dynamometer's scale: newtons a division.
        public double ScaleStep => Best(new[] { "scale1", "scale2", "scale3" }, 1);
        /// How far off the dynamometer may be, a share of the weight: a new reading every time a sample is hung.
        public double SpringError => Best(new[] { "true1", "true2", "true3" }, 0.05);
        /// The shelf's impact scale: joules a division; and how far off its block may be.
        public double ImpactStep => Best(new[] { "iscale1", "iscale2" }, 0.5);
        public double ImpactError => Best(new[] { "ihard1", "ihard2" }, 0.05);
        /// The 3D lab's own numbers for the letters it uses (DESIGN.md 2б, «Второй прибор — полка»): the shelf is h = 0,5 m
        /// high, so that the energy of its blow is not the weight over again, and h is opened for coins.
        static bool _3d;
        public static void Use3D()
        {
            Day3D = true;
            if (_3d) return;
            _3d = true;
            Defs.Use3DTree();
            var h = Defs.L("h");   // in the bath's V = Sh: how far the water rises
            h.name = "подъём воды";
            h.unit = "м";
            h.fmt = "0.#";
            h.openCur = ChalkPhysics.Cur.Coin;
            h.openCost = 60;       // opened together with S: one purchase
        }

        /// The bath's scale: litres a division; and how far off it may be.
        public double VolumeStep => Best(new[] { "vscale1", "vscale2" }, 0.5);
        public double VolumeError => Best(new[] { "vtrue1", "vtrue2" }, 0.05);
        /// The inclined plane's protractor: degrees a division; and how far off the board may be (a share of the friction).
        public double AngleStep => Best(new[] { "astep1" }, 5);
        public double SlipError => Best(new[] { "atrue1", "atrue2" }, 0.05);

        /// Order sheets on the counter in the morning: no more come during the day.
        public int OrdersPerDay { get { int n = 5; foreach (var id in new[] { "ord1", "ord2", "ord3" }) if (HasPerk(id)) n += (int)Defs.Pk(id).value; return n; } }
        /// What an answer is worth: all of the reward when it is right, nothing when it is a quarter off or more.
        public static double Accuracy(double answer, double truth, double tolerance = 0.25) => truth <= 0 ? 0 : Math.Max(0, 1 - Math.Abs(answer - truth) / truth / tolerance);

        /// Places on the bench the player fills (the guest takes one more).
        public int OwnSlots => 1 + (int)PerkSum(PerkKind.Slots);
        /// Tasks given at once in a run.
        public int TaskSlots => 1 + (int)PerkSum(PerkKind.TaskSlots);
        /// Chalk helpers that may come to one run: none for now — they confused more than they helped (DESIGN.md 2а).
        public int CrewSize => 0;
        /// What a finished task adds to the run: an explicit, earned share of everything it brought.
        public const double TaskBonus = 0.25;
        /// What the guest instrument pays on top: nothing until it is bought.
        public double GuestMult { get { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.GuestMult) m *= d.value; } return m; } }

        // ---- the qualification: an explicit, earned multiplier on everything
        public static readonly string[] RankNames = { "лаборант", "мл. научный сотрудник", "научный сотрудник", "кандидат наук", "доктор наук", "академик" };
        public static readonly double[] RankMults = { 1, 1.25, 1.5, 2, 3, 5 };
        /// Points (formulas assembled right) at which each rank begins.
        public static readonly int[] RankFrom = { 0, 2, 5, 9, 14, 20 };
        public int Rank { get { int r = 0; while (r + 1 < RankFrom.Length && RankPts >= RankFrom[r + 1]) r++; return r; } }
        public double RankMult => RankMults[Rank];
        public string RankName => RankNames[Rank];
        /// Formulas still to assemble for the next rank (0 at the top).
        public int RankToNext => Rank + 1 < RankFrom.Length ? RankFrom[Rank + 1] - RankPts : 0;
        /// The exam comes to the bench once a few formulas are known: there is something to remember.
        public bool QualificationOpen { get { int n = 0; foreach (var f in Defs.Formulas) if (Built(f.id) && f.station.Length > 0) n++; return n >= 3; } }
        public void RankUp() { RankPts++; Save(); }
        public void RankDown() { RankPts = RankFrom[Math.Max(0, Rank - 1)]; Save(); }
        public void AddHelper() { Helpers++; Save(); }

        /// The instruments the player has (one per station: the circuit and the steam engine count once).
        public List<string> BuiltStations()
        {
            var list = new List<string>();
            foreach (var f in Defs.Formulas) if (Built(f.id) && f.station.Length > 0 && !list.Contains(f.station)) list.Add(f.station);
            return list;
        }
        public double StationMult(string station) { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.StationMult && d.station == station) m *= d.value; } return m; }
        /// How long one swing takes on screen: the period T, sped up so that l = g = 1 swings in 2 s.
        public float SwingPeriod => Mathf.Max(0.3f, (float)V("T") / Mathf.PI);
        public int SampleCount { get { int n = 3; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.Samples) n += (int)d.value; } return n; } }
        public double GoldChance => 0.06 + (HasPerk("gold1") ? 0.06 : 0) + (HasPerk("gold2") ? 0.08 : 0) + (HasPerk("gold3") ? 0.1 : 0);
        public double CurMult(Cur c) { double m = 1; foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.CurMult && d.cur == c) m *= d.value; } return m; }
        /// What a golden specimen multiplies an energy experiment by.
        public double GoldMult => HasPerk("shine") ? 5 : 3;
        public int ShelfCapacity => 3;
        public int BallsPerDrop => Has("hopper3") ? 4 : Has("hopper2") ? 2 : 1;

        // ---------------- the steam era: the bench has a temperature ----------------
        /// How hot the bench is (0..1). Every heating raises it, it cools down by itself; the engine, the piston
        /// and the ice all work the harder the hotter it is. A run starts cold.
        public float Boiler;
        public float BoilerRise => HasPerk("stoke") ? 0.4f : 0.22f;     // per heating
        public float BoilerCool => Has("lagging") ? 0.04f : 0.08f;      // per second
        public double BoilerMult => 0.4 + 2.6 * Boiler;
        static bool Steam(string station) => station == "engine" || station == "piston" || station == "ice";

        public double ObsMult { get { double m = (Has("lens1") ? 1.5 : 1) * (Has("lens2") ? 1.5 : 1) * (Has("lens3") ? 1.5 : 1) * (Has("lens4") ? 1.5 : 1); foreach (var p in Perks) { var d = Defs.Pk(p); if (d != null && d.kind == PerkKind.ObsMult) m *= d.value; } return m; } }

        /// Levels of the letters in a measuring formula sharpen it: joules put into letters pay back in observations.
        /// Mechanics has left this behind: its instruments pay their formula's value (paysValue), the pendulum per swing.
        public double Precision(FormulaDef f)
        {
            if (f.kind != FKind.Measure || f.domain == 0) return 1;
            double n = 0;
            foreach (var l in f.Slots()) { var d = Defs.L(l); if (!d.Derived && !d.perSample) n += Level(l) - 1; }
            return 1 + 0.04 * n;
        }

        public static bool IsMeasurer(FormulaDef f) => f.reveals != null;

        /// Everything the bench of this epoch can find out about a specimen is known.
        public bool Measured(Sample s)
        {
            if (s == null) return false;
            bool any = false;
            foreach (var f in Defs.Formulas)
                if (f.reveals != null && Built(f.id)) { any = true; if (!s.Knows(f.reveals)) return false; }
            return any;
        }
        /// "Полная опись": every studied specimen adds a share to the whole run.
        public double CensusRate => HasPerk("census2") ? 0.05 : HasPerk("census") ? 0.04 : 0;
        public bool Census => CensusRate > 0;
        public int StudiedCount { get { int n = 0; foreach (var s in Samples) if (Measured(s)) n++; return n; } }
        public double CensusBonus => CensusRate * StudiedCount;
        /// Whether every property the experiment needs has been measured on this sample.
        public static bool Scientific(Sample s, FormulaDef f)
        {
            if (s == null) return true;
            foreach (var p in f.needs) if (!s.Knows(p)) return false;
            return true;
        }

        /// Yield of one firing of a formula in the given currency, with a given sample (null = generic).
        /// preview = what the player is allowed to know: a secret golden sample counts as ordinary.
        public double YieldOf(FormulaDef f, Cur c, Sample sample = null, bool preview = false)
        {
            var prev = ActiveSample; bool prevP = _preview;
            ActiveSample = sample; _preview = preview;
            double y = 0;
            double sm = StationMult(f.station);
            // an experiment on an unmeasured sample is "unscientific": only half is credited
            double sci = Scientific(sample, f) ? 1 : 0.5;
            // the bath only weighs what the water pushes back: a specimen that sinks gives nothing
            if (f.station == "arch" && sample != null && !sample.Floats) { ActiveSample = prev; _preview = prevP; return 0; }
            if (f.kind == FKind.Energy && f.yields == c) y += f.eval(this) * Mult * sm * sci;
            // ideas come from measuring instruments only: an experiment that just makes energy teaches nothing new
            // a specimen this instrument has already measured teaches half as much: the next ideas are in the next specimen
            if (c == ChalkPhysics.Cur.Obs && f.kind == FKind.Measure) y += (f.pays != null ? f.pays(this) : f.paysValue ? f.eval(this) : f.obs * Precision(f)) * ObsMult * sm * Mult / DevSpeed * sci
                * (sample != null && f.reveals != null && sample.seenAt.Contains(f.station) ? 0.5 : 1);
            ActiveSample = prev; _preview = prevP;
            if (y <= 0) return 0;
            y *= CurMult(c);
            // a golden specimen pays on the result, whatever the experiment
            if (sample != null && sample.golden && (sample.knowGold || !preview)) y *= GoldMult;
            // a specimen studied to the end is worth more with the census
            if (Census && sample != null && Measured(sample)) y *= 1.25;
            if (Steam(f.station)) y *= BoilerMult;
            y *= RankMult;
            return y;
        }

        /// Best sample to use on a station: the one with the biggest known yield, or an unmeasured one
        /// for measurers. Only samples passing the filter (e.g. not busy on the bench) are considered; null if none.
        public Sample BestSampleFor(FormulaDef f, Func<Sample, bool> allowed = null)
        {
            if (!f.sample || Samples.Count == 0) return null;
            if (IsMeasurer(f) && f.reveals != null)
            {
                foreach (var s in Samples) if (!s.Knows(f.reveals) && (allowed == null || allowed(s))) return s;
            }
            Sample best = null; double bv = -1;
            var c = f.kind == FKind.Energy ? f.yields : ChalkPhysics.Cur.Obs;
            foreach (var s in Samples)
            {
                if (allowed != null && !allowed(s)) continue;
                double v = YieldOf(f, c, s, true);
                if (v > bv) { bv = v; best = s; }
            }
            return best;
        }

        public void Fire(FormulaDef f, Sample sample = null, double mult = 1)
        {
            if (!Built(f.id)) return;
            if (f.kind == FKind.Energy) Earn(f.yields, YieldOf(f, f.yields, sample) * mult);
            Earn(ChalkPhysics.Cur.Obs, YieldOf(f, ChalkPhysics.Cur.Obs, sample) * mult);
            LessonFires[f.id] = (LessonFires.TryGetValue(f.id, out var n) ? n : 0) + 1;
            if (sample != null)
            {
                sample.uses++;
                sample.seenAt.Add(f.station);   // from now on this instrument can forecast this specimen
                if (f.reveals == "m") sample.knowM = true;
                if (f.reveals == "V") sample.knowV = true;
                if (f.reveals == "mu") sample.knowMu = true;
                // gold shows itself only in a real experiment, never on a measuring instrument
                if (f.kind == FKind.Energy && sample.golden && !sample.knowGold) { sample.knowGold = true; GoldFound?.Invoke(sample); }
            }
        }

        public void Earn(Cur c, double e)
        {
            if (e <= 0 || double.IsNaN(e)) return;
            int i = (int)c;
            Cur[i] += e;
            Total[i] += e;
            LessonEarned[i] += e;
        }

        /// The player has done this on the bench once: its hint is not needed any more.
        public void MarkUsed(string key) { if (Used.Add(key)) Save(); }

        // ---------------- lessons ----------------
        /// The bench of the next run: the player's own places hold what he picked last time (or the newest instruments),
        /// and the guest is a random instrument from the rest — a new one first, so that it gets tried.
        public void PickBench(System.Random rnd = null)
        {
            rnd = rnd ?? (Sandbox && SandboxRng != null ? SandboxRng : new System.Random());
            var all = BuiltStations();
            Loadout.RemoveAll(x => !all.Contains(x));
            for (int i = all.Count - 1; i >= 0 && Loadout.Count < Math.Min(OwnSlots, all.Count); i--)
                if (!Loadout.Contains(all[i]) && !(all.Count > OwnSlots && IsNew(all[i]))) Loadout.Add(all[i]);
            for (int i = all.Count - 1; i >= 0 && Loadout.Count < Math.Min(OwnSlots, all.Count); i--)
                if (!Loadout.Contains(all[i])) Loadout.Add(all[i]);
            while (Loadout.Count > OwnSlots) Loadout.RemoveAt(Loadout.Count - 1);
            Bench.Clear(); Bench.AddRange(Loadout);
            Guest = null;
            var rest = all.Where(x => !Bench.Contains(x)).ToList();
            if (rest.Count > 0)
            {
                var fresh = rest.Where(IsNew).ToList();
                Guest = fresh.Count > 0 ? fresh[0] : rest[rnd.Next(rest.Count)];
                Bench.Add(Guest);
            }
        }
        /// An instrument the player has never worked with yet: it comes as the guest, and the clock waits for it.
        public bool IsNew(string station) => !Used.Contains(station);

        public void StartLesson()
        {
            if (Phase == Phase.Lesson || Won || !AnyStation) return;
            Phase = Phase.Lesson;
            if (Bench.Count == 0 || Bench.Any(x => !BuiltStations().Contains(x))) PickBench();
            TimeLeft = LevelSeconds;
            Boiler = 0;
            GenerateSamples();
            for (int i = 0; i < NCur; i++) LessonEarned[i] = 0;
            LessonFires.Clear();
            LessonStarted?.Invoke();
            Changed?.Invoke();
        }

        // specimens are 1/2, 1, 2 or 3 times the average (1 twice as often) and 1/2, 1 or 2 rough: easy to weigh up in the head
        static readonly double[] Ratios = { 0.5, 1.0, 1.0, 2.0, 3.0 };
        static readonly double[] Roughness = { 0.5, 1.0, 2.0 };

        /// The six hatches from the lightest to the darkest (Apparatus.Fill): dots, rows, columns, diagonal, grid,
        /// grid with a diagonal. With the size showing the volume, the hatch shows the density, so the pair of them
        /// reads as the weight: a big dark block is heavy, a small dotted one is light.
        static readonly int[] HatchScale = { 5, 3, 4, 1, 0, 2 };
        /// Where the density moves to the next hatch; the third step sits on Floats' 1.2, so the three lighter
        /// hatches always float and the three darker ones always sink.
        static readonly double[] HatchSteps = { 0.35, 0.7, 1.2, 2.0, 3.5 };
        public static int HatchFor(Sample s)
        {
            double d = s.massR / s.volR;
            int k = 0;
            while (k < HatchSteps.Length && d >= HatchSteps[k]) k++;
            return HatchScale[k];
        }
        static readonly System.Random _rng = new System.Random();

        /// One more specimen of this run, by the crate's rules (the 3D lab's crates at the tables and the parcel).
        public Sample AddSample(bool weighed)
        {
            var rnd = Sandbox && SandboxRng != null ? SandboxRng : _rng;
            double heavy = HasPerk("heavy") ? 1.25 : 1;
            var s = new Sample { id = Samples.Count, massR = Ratios[rnd.Next(Ratios.Length)] * heavy, volR = Ratios[rnd.Next(Ratios.Length)], mu = Roughness[rnd.Next(Roughness.Length)] };
            s.golden = rnd.NextDouble() < GoldChance;
            s.pattern = HatchFor(s);
            s.knowM = weighed || HasPerk("autoweigh");
            Samples.Add(s);
            return s;
        }

        void GenerateSamples()
        {
            Samples.Clear();
            Selected = -1;
            var rnd = Sandbox && SandboxRng != null ? SandboxRng : new System.Random();
            int n = SampleCount;
            for (int i = 0; i < n; i++)
            {
                double heavy = HasPerk("heavy") ? 1.25 : 1;
                var s = new Sample { id = i, massR = Ratios[rnd.Next(Ratios.Length)] * heavy, volR = Ratios[rnd.Next(Ratios.Length)], mu = Roughness[rnd.Next(Roughness.Length)] };
                s.golden = rnd.NextDouble() < GoldChance;
                Samples.Add(s);
            }
            // there is always something to find: one clearly heavy and one clearly light sample
            Samples[rnd.Next(n)].massR = 3.0 * (HasPerk("heavy") ? 1.25 : 1);
            int light = rnd.Next(n);
            if (Samples[light].massR < 3.0) Samples[light].massR = 0.5;
            foreach (var s in Samples) s.pattern = HatchFor(s);     // once the masses are final
            if (HasPerk("autoweigh")) foreach (var s in Samples) s.knowM = true;
        }

        public void EndLesson()
        {
            if (Phase != Phase.Lesson) return;
            Phase = Phase.Break;
            Lessons++;
            Bench.Clear();
            LessonEnded?.Invoke();
            Changed?.Invoke();
            Save();
            if (Has("lab")) Autobuy();
        }

        void Autobuy()
        {
            if (RevolutionOffered) return;
            var start = (double[])Cur.Clone();
            bool any = false;
            for (int guard = 0; guard < 50; guard++)
            {
                string best = null; double bc = double.MaxValue;
                foreach (var id in Known)
                {
                    var d = Defs.L(id);
                    if (d.Derived || AtLimit(id) || !CanUpgrade(id)) continue;
                    double c = UpgradeCost(id);
                    if (c < bc && Cur[(int)d.cost] - c >= start[(int)d.cost] * 0.75) { bc = c; best = id; }
                }
                if (best == null) break;
                Upgrade(best);
                any = true;
            }
            if (any) Sfx.Play("up", 0.25f);
        }

        // ---------------- the tree: what is on the map ----------------
        public bool NodeDone(string code)
        {
            switch (Defs.Kind(code))
            {
                case 'L': return Known.Contains(Defs.Id(code));
                case 'F': return Built(Defs.Id(code));
                case 'R': return Revolutions >= int.Parse(Defs.Id(code));
                case 'G': return Gates.Contains(Defs.Id(code));
            }
            return false;
        }

        public bool LayerDone(int k) { foreach (var c in Defs.Tree[k]) if (!NodeDone(c)) return false; return true; }
        bool LayerHasFormula(int k) { foreach (var c in Defs.Tree[k]) if (Defs.Kind(c) == 'F') return true; return false; }
        /// The lesson count when the layer's last formula was found.
        int LayerDoneLesson(int k) { int n = 0; foreach (var c in Defs.Tree[k]) if (Defs.Kind(c) == 'F' && BuiltAt.TryGetValue(Defs.Id(c), out var l)) n = Math.Max(n, l); return n; }

        /// The layer a layer grows out of: the previous one, except that trunk layers skip over a side branch.
        public static int PrevLayer(int k)
        {
            if (k <= 0) return -1;
            if (Defs.LayerEra[k - 1] != Defs.LayerEra[k]) return -1;   // the first layer of an epoch grows out of nothing
            if (Defs.IsBranch[k]) return k - 1;                       // a branch continues from its previous layer (or the revolution)
            int j = k - 1;
            while (j > 0 && Defs.IsBranch[j]) j--;
            return j;
        }

        /// A layer grows out of the previous one once that is done; after a formula the player first goes to a lesson with it.
        public bool LayerVisible(int k)
        {
            if (k < 0 || k >= Defs.Tree.Length) return false;
            if (Defs.LayerEra[k] > Era) return false;   // every epoch reached is on the board, each a tree of its own
            int prev = PrevLayer(k);
            if (prev < 0) return true;
            if (!LayerDone(prev)) return false;
            if (LayerHasFormula(prev) && Lessons <= LayerDoneLesson(prev)) return false;
            return true;
        }

        public bool NodeVisible(string code) { int k = Defs.LayerOf(code); return k >= 0 && LayerVisible(k); }
        public int LastVisibleLayer { get { int last = 0; for (int k = 0; k < Defs.Tree.Length; k++) if (LayerVisible(k)) last = k; return last; } }
        /// A formula's branch of upgrades starts growing once the player has been to a lesson with the formula.
        public bool ChainOpen(string key)
        {
            if (!UpgradesOpen) return false;
            if (key.StartsWith("L:")) return Known.Contains(key.Substring(2)) && Lessons > 0;
            return Built(key) && Lessons > (BuiltAt.TryGetValue(key, out var l) ? l : 0);
        }

        /// An early perk waits only for its formula; everything else waits for its whole branch.
        public bool PerkOpen(PerkDef p) => InPlay(p) && (ChainOpen(p.requires) || (p.early && !p.requires.StartsWith("L:") && Built(p.requires)));

        /// Nothing to upgrade yet: devices, perks and letter levels all wait for the very first joules (from the shelf),
        /// so a new player meets one thing at a time.
        public bool UpgradesOpen => Day3D ? Lessons > 0 : Total[(int)ChalkPhysics.Cur.J] > 0 || Revolutions > 0;

        // ---------------- letters: opened for joules ----------------
        /// Letters that open together: the new letters of one layer of the tree. The theory there needs them all, so
        /// one of them alone would buy nothing — they are one purchase. The layer's first letter leads the pair.
        public List<string> OpenGroup(string id)
        {
            var g = new List<string>();
            int k = Defs.LayerOf("L:" + id);
            if (k >= 0)
                foreach (var c in Defs.Tree[k])
                    if (Defs.Kind(c) == 'L') { var l = Defs.Id(c); if (!Known.Contains(l) && !Defs.L(l).Derived) g.Add(l); }
            if (!g.Contains(id)) g.Add(id);
            return g;
        }

        /// What opening this letter costs: the whole pair it belongs to.
        public double OpenCost(string id) { double c = 0; foreach (var l in OpenGroup(id)) c += Defs.L(l).openCost; return c; }

        public bool CanOpen(string id)
        {
            var d = Defs.L(id);
            return !Known.Contains(id) && !d.Derived && NodeVisible("L:" + id) && Cur[(int)d.openCur] >= OpenCost(id);
        }

        public bool OpenLetter(string id)
        {
            if (!CanOpen(id)) return false;
            var group = OpenGroup(id);
            Cur[(int)Defs.L(id).openCur] -= OpenCost(id);
            foreach (var l in group) AddKnown(l);
            foreach (var l in group) LetterOpened?.Invoke(l);
            Changed?.Invoke();
            Save();
            return true;
        }

        void AddKnown(string id)
        {
            if (Known.Contains(id)) return;
            Known.Add(id);
            Known.Sort((a, b) => Defs.Letters.FindIndex(x => x.id == a).CompareTo(Defs.Letters.FindIndex(x => x.id == b)));
        }

        public bool CanBuyGate(string id) => !Gates.Contains(id) && !Won && NodeVisible("G:" + id) && Cur[(int)ChalkPhysics.Cur.J] >= Defs.Gt(id).cost;

        public bool BuyGate(string id)
        {
            if (!CanBuyGate(id)) return false;
            Cur[(int)ChalkPhysics.Cur.J] -= Defs.Gt(id).cost;
            Gates.Add(id);
            GateBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        // ---------------- alchemy: finding formulas ----------------
        /// A formula can be found once its slot on the map is there, all its letters are open and its epoch has come.
        public bool Available(FormulaDef f)
        {
            if (f.domain > Revolutions || !NodeVisible("F:" + f.id)) return false;
            if (f.kind == FKind.Final && !Gates.Contains("final")) return false;
            foreach (var l in f.Slots()) if (!Known.Contains(l)) return false;
            return true;
        }

        public IEnumerable<FormulaDef> Discoverable()
        {
            foreach (var f in Defs.Formulas) if (!Built(f.id) && Available(f)) yield return f;
        }

        public int DiscoverableCount { get { int n = 0; foreach (var f in Discoverable()) n++; return n; } }
        /// The alchemy screen opens as soon as there is a formula to find.
        public bool AlchemyUnlocked => Proven.Count > 0 || DiscoverableCount > 0;

        public bool IsFilled(string formula, string slot) => Filled.TryGetValue(formula, out var set) && set.Contains(slot);
        public int WrongCount(string formula, string slot) => Wrong.TryGetValue(formula + ":" + slot, out var n) ? n : 0;

        /// The puzzle: a letter is dropped on a question mark of a formula's sketch. The right one sticks; when every
        /// question mark is answered the formula is found and its instrument appears on the bench.
        public PlaceResult Place(string formulaId, string letter, string slot)
        {
            var f = Defs.F(formulaId);
            if (Built(f.id) || !Available(f) || Won) return PlaceResult.NoResearch;
            if (IsFilled(f.id, slot)) return PlaceResult.Occupied;
            if (letter != slot)
            {
                Wrong[f.id + ":" + slot] = WrongCount(f.id, slot) + 1;
                return Defs.L(letter).unit == Defs.L(slot).unit ? PlaceResult.WrongSameUnit : PlaceResult.WrongUnit;
            }
            if (!Filled.TryGetValue(f.id, out var set)) Filled[f.id] = set = new HashSet<string>();
            set.Add(slot);
            bool all = true;
            foreach (var l in f.Slots()) if (!set.Contains(l)) { all = false; break; }
            if (all) Discover(f);
            else { Changed?.Invoke(); Save(); }
            return PlaceResult.Ok;
        }

        public void Discover(FormulaDef f)
        {
            if (Built(f.id)) return;
            Proven.Add(f.id);
            BuiltAt[f.id] = Lessons;
            foreach (var o in f.outputs) AddKnown(o);
            if (f.kind == FKind.Final) Won = true;
            FormulaProven?.Invoke(f.id);
            Changed?.Invoke();
            Save();
        }

        public IEnumerable<DeviceDef> AvailableDevices()
        {
            foreach (var d in Defs.Devices)
                if (!Devices.Contains(d.id) && Built(d.requires) && d.domain <= Era) yield return d;
        }

        public bool BuyDevice(string id)
        {
            var d = Defs.D(id);
            if (d == null || Devices.Contains(id) || !Built(d.requires) || Cur[(int)d.cost] < d.price) return false;
            Cur[(int)d.cost] -= d.price;
            Devices.Add(id);
            DeviceBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        public bool BuyPerk(string id)
        {
            var p = Defs.Pk(id);
            if (p == null || Perks.Contains(id) || p.domain > Era || !PerkOpen(p) || Cur[(int)p.cost] < p.price) return false;
            Cur[(int)p.cost] -= p.price;
            Perks.Add(id);
            PerkBought?.Invoke(id);
            Changed?.Invoke();
            Save();
            return true;
        }

        // ---------------- revolutions ----------------
        public RevolutionDef NextRevolution { get { foreach (var r in Defs.Revolutions) if (r.to > Revolutions) return r; return null; } }
        /// The revolution node is on the map once the tree has grown up to it.
        public bool RevolutionOffered { get { var r = NextRevolution; return r != null && !Won && NodeVisible("R:" + r.to); } }
        public bool CanRevolt => RevolutionOffered && Cur[(int)ChalkPhysics.Cur.J] >= NextRevolution.cost;

        public void Revolt()
        {
            if (!CanRevolt) return;
            var r = NextRevolution;
            for (int i = 0; i < NCur; i++) Cur[i] = 0;
            _lvl.Clear();
            Devices.Clear();
            Perks.Clear();
            // everything goes: theories, instruments, letters. Only the quantities the instruments measured stay,
            // written down in the notebook — the formula of everything will need them all at the end
            var keep = Known.Where(x => Defs.L(x).Derived).ToList();
            Known.Clear();
            foreach (var x in keep) Known.Add(x);
            Proven.Clear(); BuiltAt.Clear(); Filled.Clear(); Wrong.Clear();
            Loadout.Clear(); Bench.Clear();
            Revolutions = r.to;
            foreach (var l in r.freeLetters) AddKnown(l);
            RevolutionDone?.Invoke(Revolutions);
            Changed?.Invoke();
            Save();
        }

        float _saveT;
        public void Tick(float dt)
        {
            if (!Won) PlayTime += dt;
            _saveT += dt;
            if (_saveT > 5f) { _saveT = 0; Save(); }
        }

        public void NotifyChanged() => Changed?.Invoke();

        // ---------------- save / load ----------------
        public void Save()
        {
            try
            {
                var d = new SaveData
                {
                    cur = (double[])Cur.Clone(), total = (double[])Total.Clone(), time = PlayTime, revolutions = Revolutions, lessons = Lessons,
                    won = Won, muted = Sfx.Muted,
                    known = Known.ToList(), proven = Proven.ToList(), devices = Devices.ToList(), perks = Perks.ToList(), used = Used.ToList(), gates = Gates.ToList(),
                    builtIds = BuiltAt.Keys.ToList(), builtVals = BuiltAt.Values.ToList(),
                    lvlIds = _lvl.Keys.ToList(), lvlVals = _lvl.Values.ToList(),
                    helpers = Helpers, rank = RankPts, loadout = Loadout.ToList(), career = Career,
                };
                foreach (var kv in Filled) foreach (var l in kv.Value) d.filled.Add(kv.Key + ":" + l);
                WriteSave(JsonUtility.ToJson(d));
            }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        bool Load()
        {
            try
            {
                string json = ReadSave();
                if (string.IsNullOrEmpty(json)) return false;
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null || d.cur == null || d.cur.Length < 4 || d.total == null || d.total.Length < d.cur.Length) return false;
                for (int i = 0; i < d.cur.Length && i < NCur; i++) { Cur[i] = d.cur[i]; Total[i] = d.total[i]; }   // an older save has no coins
                PlayTime = d.time; Revolutions = d.revolutions; Lessons = d.lessons;
                Won = d.won; Sfx.Muted = d.muted;
                foreach (var x in d.known) if (Defs.HasLetter(x)) Known.Add(x);
                foreach (var x in d.proven) Proven.Add(x);
                // ids that no longer exist (upgrades dropped since the save was written) are quietly forgotten
                foreach (var x in d.devices) if (Defs.D(x) != null) Devices.Add(x);
                foreach (var x in d.perks) if (Defs.Pk(x) != null) Perks.Add(x);
                foreach (var x in d.used) Used.Add(x);
                foreach (var x in d.gates) Gates.Add(x);
                for (int i = 0; i < d.builtIds.Count; i++) BuiltAt[d.builtIds[i]] = d.builtVals[i];
                for (int i = 0; i < d.lvlIds.Count; i++) _lvl[d.lvlIds[i]] = d.lvlVals[i];
                Helpers = d.helpers; RankPts = d.rank;
                if (d.career != null) Career = d.career;
                if (d.loadout != null) Loadout.AddRange(d.loadout);
                foreach (var x in d.filled)
                {
                    int i = x.IndexOf(':');
                    if (i <= 0) continue;
                    string f = x.Substring(0, i), l = x.Substring(i + 1);
                    if (!Filled.TryGetValue(f, out var set)) Filled[f] = set = new HashSet<string>();
                    set.Add(l);
                }
                Phase = Phase.Break;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("Load failed: " + e.Message); return false; }
        }

        public static void DeleteSave()
        {
            try { if (Web) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); } else if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        }

        // ---------------- text helpers ----------------
        public static string Fmt(double v)
        {
            double a = Math.Abs(v);
            string[] suf = { "", "k", "M", "G", "T", "P", "E" };
            if (a < 1e4)
            {
                if (a >= 100) return v.ToString("0");
                if (a >= 10) return v.ToString("0.#");
                return v.ToString("0.##");
            }
            int i = 0;
            while (a >= 1000 && i < suf.Length - 1) { a /= 1000; v /= 1000; i++; }
            return v.ToString(a >= 100 ? "0" : a >= 10 ? "0.#" : "0.##") + suf[i];
        }

        /// Ideas have no name: their number stands alone, with a chalk lightbulb drawn beside it.
        public static string FmtCur(double v, Cur c) { var n = Defs.CurName[(int)c]; return n.Length > 0 ? Fmt(v) + " " + n : Fmt(v); }
        /// The mark a chalk lightbulb is drawn over (see InkText): ideas are written the same way everywhere.
        public const string Bulb = "¤";
        /// The mark a chalk coin is drawn over (see InkText).
        public const string Coin = "¢";
        /// A star of a regular customer's trust: drawn in chalk by InkText, like the coin.
        public const string Star = "§";
        /// A price or a yield in any currency; ideas get their lightbulb after the number.
        public static string FmtPrice(double v, Cur c) => c == ChalkPhysics.Cur.Obs ? Fmt(v) + " " + Bulb : c == ChalkPhysics.Cur.Coin ? Fmt(v) + " " + Coin : FmtCur(v, c);
        public string LetterValue(string id) => FmtValue(id, V(id));

        public static string FmtValue(string id, double v)
        {
            var d = Defs.L(id);
            if (d.fmt == "0%") return (v * 100).ToString("0") + "%";
            return (Math.Abs(v) >= 1e4 ? Fmt(v) : v.ToString(d.fmt)) + (d.unit.Length > 0 ? " " + d.unit : "");
        }

        /// A formula as it is written on the board: letters side by side, no multiplication dots (in chalk they are
        /// specks too small to read), so E = mgh, not E = m·g·h.
        public static string Symbolic(FormulaDef f)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in f.parts) sb.Append(p.StartsWith("{") ? Defs.L(p.Substring(1, p.Length - 2)).sym : p.Replace("·", ""));
            return sb.ToString();
        }
    }
}
