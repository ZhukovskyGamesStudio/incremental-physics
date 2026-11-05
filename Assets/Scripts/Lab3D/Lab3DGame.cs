using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ChalkPhysics.Lab3D
{
    /// A working day in the 3D lab: it is the run (the lesson) of the game, played by its rules (GameState).
    /// Now (DESIGN.md 2б, «Листки-заказы»): customers come to the window behind the counter, each with an order sheet and
    /// a sample on it. Pick the sheet up to read it, take the sample to the apparatus, read the scale by eye, write the
    /// numbers in, stamp the sheet, pack it with its sample and hand the box back: the closer, the more coins.
    /// The game is the day's coordinator: it holds what the scene builder wired up and runs the day's parts in order —
    /// the room (LabRoom), the customers (CustomerLine), the order sheets (OrderBook), the stamp and the box
    /// (DispatchDesk), the hands (LabHands) and the dot (LabPointer), the player's body (PlayerBody), the light
    /// (LabLights), the screen (LabHud); the earlier tagged orders are kept idle (TagOrders).
    /// At six (LabDay) no more orders come; what is on the counter is worked to the end. The door ends the day, at any
    /// hour: the game is saved and the map comes back, with the upgrades and the theories; its button starts the next day.
    public class Lab3DGame : MonoBehaviour
    {
        public LabWalker walker;
        public Camera eye;
        public Transform crate;         // on the counter: an order's sample has its slot here
        public Transform counter;
        public Transform counterSign;   // the board hung on the counter's front: «заказы» is written on it
        public Transform partition;     // the temporary wall across the room: it moves back as the lab grows
        public Transform partitionSide; // and the one along it, on the left
        // the corners the temporary walls make, with the clutter in them (boxes, cobwebs): front left (both walls), front
        // right (the cross wall and the room's right wall), back left (the side wall and the back wall)
        public Transform cornerFL, cornerFR, cornerBL;
        // the order desk's bureaucracy (DESIGN.md 2б, «Труба приёмки»): the stamp on its pad, the dispatch box under the
        // pipe with its lid and its inside, the send button with its cap
        public Transform stamp, stampPad, dispatchBox, boxLid, boxInside, boxTape;
        public GameObject calculator;   // on the weighing table: it comes with the density
        public Transform lightSwitch;   // by the door: the odd measurement «glows in the dark» (DESIGN.md 2б, «Крейзи-измерения»)
        public Material linesMat;       // the chalk outlines' material: greenish and faint in the dark
        public Transform inMouth;       // where the orders come out of their pipe over the counter
        public Transform certTemplate;  // a certificate's frame and paper, hidden: one is made of it for each one earned
        /// The certificates on the walls (DESIGN.md 2б, «Грамоты»).
        [System.NonSerialized] public AwardWall awards;

        /// The layers: glowing samples (the only thing the eye sees in the dark), and the player's own body (only the
        /// mirror sees it).
        public const int GlowLayer = 29, BodyLayer = 30;
        /// What only the eye sees, not the mirror: the hands that carry things (in the mirror the body's arms do).
        public const int ViewLayer = 28;
        public Board3D board;
        public List<Sample3D> samples = new List<Sample3D>();   // one a slot of the counter's crate
        public List<Apparatus3D> apparatus = new List<Apparatus3D>();
        public List<Department> departments = new List<Department>();
        public List<Helper3D> helpers = new List<Helper3D>();
        public List<Customer3D> customers = new List<Customer3D>();
        public PersonaKit kit;
        public List<OrderSheet> sheets = new List<OrderSheet>();   // on the counter, one a sample
        public float reach = 2.8f;
        public LabDay day;
        public Transform door;
        public Material[] sampleMats;   // by density: light, middle, dense (the hatching of a sample is its density)

        GameState _g;
        /// Its own, read from the save the map has just written: the map's one has the map's screens hanging on it.
        public GameState G => _g ??= new GameState();

        /// The day's parts, made as the lab wakes up (each a component of the game's object, run by it in its order).
        public LabRoom Room { get; private set; }
        public LabHud Hud { get; private set; }
        public CustomerLine Line { get; private set; }
        public OrderBook Book { get; private set; }
        public DispatchDesk Desk { get; private set; }
        public LabHands Hands { get; private set; }
        public LabPointer Pointer { get; private set; }
        public PlayerBody Body { get; private set; }
        public LabLights Lights { get; private set; }
        public TagOrders Tags { get; private set; }

        /// The lab works no more today: out through the door. (In the evening only the orders stop coming: what is on the
        /// counter is worked to the end, as in any job.)
        public bool Closed => _leaving;
        public Department Dept(string id) => departments.Find(d => d.id == id);
        /// The lab has this department: its theory found on the map, or (an odd measurement) its apparatus bought there.
        public bool Owns(string id) => G.Built(id) || G.HasPerk(Oddity.Perk(id));
        /// The odd tests the lab can do today (DESIGN.md 2б, «Крейзи-измерения»).
        public List<string> OddTests() { var l = new List<string>(); foreach (var t in Oddity.All) if (Owns(t)) l.Add(t); return l; }

        bool _leaving;
        /// The letters as they were when the last day ended: the morning shows what the map has changed.
        static Dictionary<string, double> _lastDay;

        // in OnEnable, not Start: a script reload in play mode drops subscriptions and calls these two again
        void OnEnable()
        {
            day.OnEvening += Evening;
        }

        void OnDisable()
        {
            day.OnEvening -= Evening;
        }

        /// The day's parts are made; every department's table goes to its place before anything starts (the bath's water
        /// is laid out where it stands).
        void Awake()
        {
            GameState.Use3D();
            Room = Part<LabRoom>();
            Room.Init(this);
            Hud = Part<LabHud>();
            Hud.Init(this);
            Line = Part<CustomerLine>();
            Line.Init(this);
            Book = Part<OrderBook>();
            Book.Init(this);
            Desk = Part<DispatchDesk>();
            Desk.Init(this);
            Hands = Part<LabHands>();
            Hands.Init(this);
            Pointer = Part<LabPointer>();
            Pointer.Init(this);
            Body = Part<PlayerBody>();
            Body.Init(this);
            Lights = Part<LabLights>();
            Lights.Init(this);
            Tags = Part<TagOrders>();
            Tags.Init(this);
            Room.PlaceTables();
        }

        T Part<T>() where T : MonoBehaviour => gameObject.AddComponent<T>();

        void Start()
        {
            GameState.Use3D();
            if (Sfx.I == null) new GameObject("Sfx").AddComponent<Sfx>();   // the map's sound went with the map
            // nothing found yet (the scene started by itself, with no save): the day starts on the map
            if (!G.AnyStation || G.Won) { SceneManager.LoadScene(GameRoot.MapScene); return; }
            G.StartLesson();
            G.Samples.Clear();   // the orders bring their own
            G.GoldFound += Gold;
            day.Day = G.Lessons + 1;
            day.length = G.DaySeconds;
            day.hours = G.DayHours;
            Hands.MakeAnchors();
            Desk.Setup();
            foreach (var a in apparatus)
            {
                a.game = this;
                a.station.SetActive(Owns(a.id));
                a.gameObject.SetActive(a.copy < G.Copies(a.id));
                a.Tried = G.Used.Contains(TriedKey(a));
            }
            // a sign on each table's front, as on the counter: the apparatus's name and formula
            foreach (var d in departments)
            {
                var sign = d.table != null ? d.table.Find("Sign") : null;
                if (sign != null && d.apparatus.Count > 0) Art3D.Signboard(sign, Oddity.Is(d.id) ? Oddity.Title(d.id) : Defs.F(d.id).title + "\n" + d.apparatus[0].formula, 62);
            }
            for (int i = 0; i < samples.Count; i++)
            {
                samples[i].tag = OrderTag.On(samples[i], eye);
                samples[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < helpers.Count; i++)
            {
                helpers[i].game = this;
                helpers[i].gameObject.SetActive(i < G.Crew);
            }
            if (counterSign != null) Art3D.Signboard(counterSign, "заказы");
            foreach (var c in customers) { c.game = this; c.gameObject.SetActive(false); }
            Hud.Build();
            if (board.gameObject.activeSelf) board.Build(this);   // no board in the closet for now
            Hud.Card("День " + day.Day, "", "", 2.5f);
            StartCoroutine(Hud.Dawn());
            OrderSheet.NewDay();   // no story twice a day
            foreach (var c in Career.Morning(G, G.OrdersPerDay)) Hud.Tell(c);   // what opens today, after the day's card
            Morning();
            Room.Open();
            Awards.Morning(G, Room.Stage, LabRoom.LastStage);   // what the map and the room have earned: hung up by AwardWall
            awards = gameObject.AddComponent<AwardWall>();
            awards.Init(this);
            if (calculator != null) calculator.SetActive(G.Built("dens"));
            if (lightSwitch != null) lightSwitch.gameObject.SetActive(Owns("glow"));
            Lights.Setup();
            Body.Build();
            foreach (var sh in sheets)
            {
                sh.view = SheetView.Build(sh, this);
                Line.Remember(sh);
                sh.gameObject.SetActive(false);   // the counter is bare in the morning: the orders come with the customers
                sh.sample.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (_g != null) _g.GoldFound -= Gold;
        }

        void OnApplicationQuit() => _g?.Save();

        static string TriedKey(Apparatus3D a) => "3d:" + a.id + a.copy;

        /// What the map has changed since yesterday is written over the apparatus whose formulas it is in: «g 1 → 1.25».
        void Morning()
        {
            if (_lastDay == null) return;
            foreach (var a in apparatus)
            {
                if (a.copy != 0 || !a.station.activeSelf || Oddity.Is(a.id)) continue;
                float y = a.labelHeight + 0.09f;
                foreach (var l in Defs.F(a.id).Slots())
                {
                    if (!_lastDay.TryGetValue(l, out var was) || System.Math.Abs(G.V(l) - was) < 1e-9) continue;
                    var at = a.transform.TransformPoint(Vector3.up * y);
                    Chalk3DText.Pop(at, Defs.L(l).sym + "  " + GameState.FmtValue(l, was) + " → " + G.LetterValue(l), ChalkTex.Yellow, 0.045f, eye, 9f);
                    y += 0.07f;
                }
            }
        }

        /// Six o'clock: no more orders come today; what is on the counter can still be done and sent.
        void Evening()
        {
            if (Book.AllSent) Hud.Card("Рабочий день окончен", Hud.DaySum, "домой — в дверь", 0);
            else Hud.Card("Рабочий день окончен", "", "новых заказов не будет   ·   доделай и домой", 4f);
        }

        /// Out through the door (once).
        public void Leave()
        {
            if (!_leaving) StartCoroutine(EndDay());
        }

        /// Out through the door: the room goes dark, the day is over and saved, and the map comes.
        IEnumerator EndDay()
        {
            awards?.PutBack();
            _leaving = true;
            Hands.Up();
            walker.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return Hud.Dusk();
            GameRoot.FromDay = true;   // the map comes out of the same veil
            Career.DayEnd(G, Book.AllSent && !day.Evening, (int)G.LessonEarned[(int)Cur.Coin]);
            Awards.DayEnd(G, Book.AllSent);   // hung up tomorrow morning
            _lastDay = new Dictionary<string, double>();
            foreach (var l in G.Known) _lastDay[l] = G.V(l);
            G.EndLesson();
            SceneManager.LoadScene(GameRoot.MapScene);
        }

        /// A golden sample shows itself in an experiment: it is written in the air over it.
        void Gold(Sample s)
        {
            var s3 = samples.Find(x => x.data == s);
            if (s3 != null) Chalk3DText.Pop(s3.transform.position + Vector3.up * 0.12f, "золотой! ×" + GameState.Fmt(G.GoldMult), ChalkTex.Yellow, 0.07f, eye, 2f);
        }

        /// The apparatus has worked its sample, `share` of it (how well it was done): it is tried. In the order sheets'
        /// lab that is all — an apparatus only shows, the sheet pays; with tagged orders the tag gets its result.
        public void Fire(Apparatus3D a, double share, string line, Vector3 at)
        {
            if (Closed || a.Load == null) return;
            if (!a.Tried) { a.Tried = true; G.MarkUsed(TriedKey(a)); }
            if (sheets.Count > 0) return;   // the order sheets' lab: an apparatus only shows, the sheet pays
            Tags.Fire(a, share, line, at);
        }

        void Update()
        {
            if (_g == null || !Hands.Ready) return;   // going back to the map
            G.Tick(Time.deltaTime);
            Room.Tick();
            Line.Tick();
            Desk.Tick();
            Body.Tick();
            Hud.Tick();
            walker.frozen = false;   // the sheet in the hand goes about the room with the player
            var target = Pointer.Aim();
            Pointer.Target = target;
            foreach (var s in samples) if (s.tag != null) s.tag.full = s.At != Sample3D.Where.Crate || s == (Object)target;
            Pointer.Glow(target);
            Hud.Draw(target);
            if (_leaving) return;

            var ms = Mouse.current;
            if (ms == null) return;
            if (ms.leftButton.wasReleasedThisFrame) Hands.Up();
            if (!walker.Looking || walker.CaughtThisFrame || Hands.ClosedThisFrame) return;
            if (ms.leftButton.wasPressedThisFrame) Hands.Down(target);
            if (ms.rightButton.wasPressedThisFrame) Hands.Alt(target);
        }
    }
}
