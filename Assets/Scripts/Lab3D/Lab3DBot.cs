using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// Plays a day of the 3D lab for the headless check (Assets/Editor/Lab3DCheck.cs) through the same hands as the
    /// mouse (DESIGN.md 2б, «Листки-заказы», «Труба приёмки»): for every order sheet it takes the sample to each apparatus
    /// the sheet asks for — the dynamometer (waits for the pointer to stop), the bath (waits for the water to stand still) —
    /// reads the scale as a player does, to the nearest half division, puts the sample back on its sheet and writes the
    /// numbers in, the density as weight ÷ volume on the calculator; then the stamp, the sheet and its sample into the box, the button. Walking is a pause of `walk`
    /// seconds per trip. With every sheet sent, or in the evening, it goes out through the door. Counts coins and accuracy.
    public class Lab3DBot : MonoBehaviour
    {
        public Lab3DGame game;
        public float walk = 2.2f;   // a person who looks around and walks, not the fastest possible hands
        /// Every order sent in the check: its accuracy (0..1).
        public static readonly List<double> accuracy = new List<double>();
        public static int paid, byBot;
        /// Something the check may take a picture of: "stamped", "boxed", "sending".
        public static string moment = "";
        /// The check asks the bot to wait between two orders (to take a picture of a sheet in the hand); `idle` says it is.
        public static bool hold, idle;
        /// What the odd departments told it, for the check's log (DESIGN.md 2б, «Крейзи-измерения»).
        public static readonly List<string> notes = new List<string>();
        float _evening = -1;

        void OnEnable()
        {
            game.Book.Sent += Count;
            StartCoroutine(Run());   // in OnEnable: a script reload in play mode stops coroutines and calls it again
        }

        void OnDisable() => game.Book.Sent -= Count;

        void Count(OrderSheet s, double acc, int coins)
        {
            accuracy.Add(acc);
            paid++;
            byBot++;
        }

        /// An odd measurement (DESIGN.md 2б, «Крейзи-измерения»), as a player does it: the sample to the department, the
        /// apparatus worked, what it does looked at (the tongue's reactions, the fish on the line, the cat…), the sample
        /// back onto its sheet. The glow: the light off with the sample on its sheet, a look, the light on.
        IEnumerator Odd(OrderSheet sheet, string test, Dictionary<string, int> got)
        {
            var s = sheet.sample;
            var d = s.data;
            if (test == "glow")
            {
                yield return new WaitForSeconds(walk * 0.5f);
                var sw = game.lightSwitch != null ? game.lightSwitch.GetComponent<LightSwitch>() : null;
                if (sw != null) game.Hands.Down(sw);
                yield return new WaitForSeconds(0.5f);
                yield return Moment("dark");
                yield return new WaitForSeconds(0.5f);
                got[test] = d.glows ? 1 : 2;
                if (sw != null && game.Lights.Dark) game.Hands.Down(sw);
                notes.Add($"свет: образец №{sheet.number} {(d.glows ? "светится" : "не светится")}");
                yield return new WaitForSeconds(0.3f);
                yield break;
            }
            var app = game.Dept(test)?.apparatus[0];
            if (app == null || !app.Active) { got[test] = 1; yield break; }
            game.Hands.Down(s);                                          // off its sheet
            yield return new WaitForSeconds(0.4f);
            yield return Moment("carry");                          // in the hand
            yield return new WaitForSeconds(walk);
            game.Hands.Down(app);                                        // onto the apparatus
            yield return new WaitForSeconds(0.4f);
            switch (app)
            {
                case Tongue3D tg:
                {   // every place of the tongue in turn: where did it react?
                    int mask = 0;
                    for (int z = 0; z < 5; z++)
                    {
                        tg.wanted = z;
                        if (z > 0) game.Hands.Down(tg);
                        for (float t = 0; t < 3 && !tg.Done; t += Time.deltaTime) { if (tg.Reacted && moment != "taste-seen") { yield return Moment("taste"); moment = "taste-seen"; } yield return null; }
                        if (tg.Reacted) mask |= 1 << z;
                        yield return new WaitForSeconds(0.2f);
                    }
                    tg.wanted = -1;
                    got[test] = mask;
                    notes.Add($"язык: образец №{sheet.number} — {Names("taste", mask)} (на самом деле {Names("taste", d.taste)})");
                    break;
                }
                case Rod3D rod:
                {
                    game.Hands.Down(rod);                                // cast
                    for (float t = 0; t < Rod3D.Sand + 3 && !rod.Biting && !rod.SandOut; t += Time.deltaTime) yield return null;
                    if (rod.Biting) yield return Moment("bite");
                    yield return new WaitForSeconds(0.3f);
                    game.Hands.Down(rod);                                // pull out
                    yield return new WaitForSeconds(0.9f);
                    if (rod.Caught is >= 0 and < 5) yield return Moment("fish");
                    got[test] = 1 << (rod.Caught < 0 ? 5 : rod.Caught);
                    notes.Add($"пруд: образец №{sheet.number} — {Names("fish", got[test])} (на самом деле {Names("fish", 1 << d.fish)})");
                    break;
                }
                case Mirror3D:
                    yield return new WaitForSeconds(0.8f);
                    yield return Moment("mirror");
                    got[test] = d.reflects ? 1 : 2;
                    notes.Add($"зеркало: образец №{sheet.number} {(d.reflects ? "отражается" : "не отражается")}");
                    break;
                case Cat3D cat:
                    for (float t = 0; t < 3 && cat.Shown < 0; t += Time.deltaTime) yield return null;
                    yield return new WaitForSeconds(0.5f);
                    yield return Moment("cat");
                    for (float t = 0; t < 5 && cat.Reacting; t += Time.deltaTime) yield return null;
                    got[test] = 1 << Mathf.Max(0, cat.Shown);
                    notes.Add($"кот: образец №{sheet.number} — {Names("cat", got[test])} (на самом деле {Names("cat", 1 << d.cat)})");
                    break;
                case Xylo3D xy:
                {
                    game.Hands.Down(xy);                                 // the mallet
                    for (float t = 0; t < 1.5f && !xy.Rang; t += Time.deltaTime) yield return null;
                    yield return Moment("note");
                    foreach (int bar in new[] { 0, 2, 4, d.note })   // and the bars, to compare by ear
                    {
                        yield return new WaitForSeconds(0.35f);
                        xy.Bar(bar);
                    }
                    int n = d.note;
                    if (Random.value < 0.15f) n = Mathf.Clamp(n + (Random.value < 0.5f ? -1 : 1), 0, 6);   // an ear is not perfect
                    got[test] = 1 << n;
                    notes.Add($"ксилофон: образец №{sheet.number} — {Names("note", got[test])} (на самом деле {Names("note", 1 << d.note)})");
                    break;
                }
                case Feather3D f:
                    f.steady = true;                               // the bot holds it on the sample without a dot
                    game.Hands.Down(f);                                  // hold: the feather tickles
                    yield return new WaitForSeconds(1.1f);
                    yield return Moment("tickle");
                    game.Hands.Up();
                    got[test] = f.Giggled ? 1 : 2;
                    notes.Add($"перо: образец №{sheet.number} {(f.Giggled ? "хихикает" : "не боится")} (на самом деле {(d.ticklish ? "боится" : "не боится")})");
                    break;
            }
            for (float t = 0; t < 6 && app.Busy; t += Time.deltaTime) yield return null;
            game.Hands.Alt(app);                                         // off the apparatus
            yield return new WaitForSeconds(walk);
            game.Hands.Down(game.crate);                                 // back onto its sheet (a click on the counter)
            yield return new WaitForSeconds(0.6f);
        }

        /// Something worth a picture: the check sees it and clears it (or a moment passes with no check looking).
        IEnumerator Moment(string m)
        {
            moment = m;
            float t0 = Time.realtimeSinceStartup;
            while (moment == m && Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
        }

        static string Names(string test, int mask)
        {
            var o = Oddity.Options(test);
            var l = new List<string>();
            for (int k = 0; k < o.Length; k++) if (((mask >> k) & 1) != 0) l.Add(o[k]);
            return l.Count > 0 ? string.Join(", ", l) : "ничего";
        }

        IEnumerator Run()
        {
            yield return null;
            var dyn = (Dynamometer3D)game.Dept("dyna").apparatus[0];
            var bath = game.Dept("bath")?.apparatus[0] as Bath3D;
            var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
            while (true)
            {
                idle = true;
                while (hold) yield return null;
                idle = false;
                if (game.day.Evening && _evening < 0) _evening = Time.time;
                var waiting = game.customers.Find(c => c.AtWindow);
                if (waiting != null)
                {   // someone at the window: take the order
                    yield return new WaitForSeconds(walk * 0.5f);
                    game.Hands.Down(waiting);
                    yield return new WaitForSeconds(1.2f);
                }
                // the order whose customer is nearest to giving up, first
                OrderSheet sheet = null;
                float least = 2;
                foreach (var sh in game.sheets)
                {
                    if (!sh.gameObject.activeSelf || sh.sent || game.Line.Incoming(sh)) continue;
                    var who = game.customers.Find(c => c.Order == sh);
                    float h = who != null ? who.Hurry : 1;
                    if (h < least) { least = h; sheet = sh; }
                }
                // home when all is done, or if something has gone wrong long after six (the day never ends by itself now)
                if (game.Book.AllSent || (_evening >= 0 && Time.time - _evening > 60))
                {
                    yield return new WaitForSeconds(walk * 2);   // over to the door
                    game.Hands.Down(game.door);
                    yield break;
                }
                if (sheet == null) { yield return new WaitForSeconds(0.5f); continue; }   // the next order is on its way
                // the order as a job of its own: if its customer gives up waiting, the job stops and the hands are freed
                _busy = true;
                var job = StartCoroutine(Work(sheet, dyn, bath, ru));
                while (_busy)
                {
                    if (!sheet.sent && !sheet.gameObject.activeSelf) { StopCoroutine(job); _busy = false; yield return Recover(); }
                    var next = game.customers.Find(c => c.AtWindow);   // a click at the window takes no hands: done between two steps
                    if (next != null && Random.value < Time.deltaTime / 2f) game.Hands.Down(next);
                    yield return null;
                }
            }
        }

        bool _busy;

        /// After an order taken away mid-work: the button up, the sheet put down, the stamp back, any sample back.
        IEnumerator Recover()
        {
            game.Hands.Up();
            if (game.Hands.Reading != null) game.Hands.CloseSheet();
            if (game.Desk.StampHeld) game.Hands.Down(game.stamp);
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 3 && game.Hands.Held != null; i++) { game.Hands.Down(game.crate); yield return new WaitForSeconds(0.3f); }
        }

        /// One order, from its sheet to its customer.
        IEnumerator Work(OrderSheet sheet, Dynamometer3D dyn, Bath3D bath, System.Globalization.CultureInfo ru)
        {
            // the density is worked out of the weight and the volume: both are measured for it
            bool dens = sheet.tests.Contains("dens");
            var measure = new List<string>();
            if (sheet.tests.Contains("angle") || sheet.tests.Contains("mu")) measure.Add("incline");
            if (sheet.tests.Contains("dyna") || dens) measure.Add("dyna");
            if (sheet.tests.Contains("bath") || dens) measure.Add("bath");
            foreach (var t in sheet.tests) if (Oddity.Is(t)) measure.Add(t);
            double force = 0, volume = 0, angle = 0;
            var odd = new Dictionary<string, int>();
            foreach (var m in measure)
            {
                if (Oddity.Is(m)) { yield return Odd(sheet, m, odd); continue; }
                game.Hands.Down(sheet.sample);                       // off its sheet
                yield return new WaitForSeconds(walk);
                var slope = game.Dept("incline")?.apparatus[0] as Incline3D;
                if (m == "incline" && slope != null && slope.Active)
                {
                    game.Hands.Down(slope);                          // onto the board
                    yield return new WaitForSeconds(0.5f);
                    game.Hands.Down(slope);                          // the jack on: the board rises until it slides
                    for (float t = 0; t < 30 && !slope.Settled; t += Time.deltaTime) yield return null;
                    game.Hands.Up();
                    double half = game.G.AngleStep / 2;
                    angle = System.Math.Round(slope.Reading / half) * half;
                    notes.Add($"доска: образец №{sheet.number} съехал на {slope.Reading:0.#}° (на самом деле {slope.TrueAngle:0.#}°), подъёмник {(slope.Settled ? "встал" : "не дождался")}");
                    yield return new WaitForSeconds(0.6f);     // reading the protractor
                    game.Hands.Alt(slope);                           // off the board
                }
                else if (m == "bath" && bath != null && bath.Active)
                {
                    game.Hands.Down(bath);                           // into the water
                    for (float t = 0; t < 30 && !bath.Settled; t += Time.deltaTime) yield return null;
                    double half = game.G.VolumeStep / 2;
                    volume = System.Math.Round(bath.Reading / half) * half;
                    game.Hands.Alt(bath);                            // out of the water
                }
                else
                {
                    game.Hands.Down(dyn);                            // hung on the hook
                    for (float t = 0; t < 20 && !dyn.Settled; t += Time.deltaTime) yield return null;
                    double half = game.G.ScaleStep / 2;
                    force = System.Math.Round(dyn.Reading / half) * half;
                    game.Hands.Alt(dyn);                             // off the hook
                }
                yield return new WaitForSeconds(walk);
                game.Hands.Down(game.crate);                         // back onto its sheet (a click on the counter)
                yield return new WaitForSeconds(0.6f);         // writing the number in
            }
            if (force <= 0) force = 0.1;
            if (volume <= 0) volume = 0.1;
            double rho = force / game.G.V("g") / volume;
            if (dens && game.calculator != null && game.calculator.activeSelf)
            {   // on the calculator, as a player would: weight ÷ volume =
                yield return new WaitForSeconds(walk * 0.5f);
                var keys = game.calculator.GetComponentsInChildren<CalcKey>();
                foreach (char c in force.ToString("0.##", ru) + "÷" + volume.ToString("0.##", ru) + "=")
                {
                    var k = System.Array.Find(keys, x => x.key == c.ToString());
                    if (k != null) game.Hands.Down(k);
                    yield return new WaitForSeconds(0.15f);
                }
                moment = "calculated";
                yield return new WaitForSeconds(walk * 0.5f);
            }
            for (int i = 0; i < sheet.tests.Count; i++)
            {
                if (Oddity.Is(sheet.tests[i]))
                {   // the boxes it saw ticked, by their numbers
                    int got = odd.TryGetValue(sheet.tests[i], out var g) ? g : 1;
                    string s = "";
                    for (int k = 0; k < 9; k++) if (((got >> k) & 1) != 0) s += (char)('1' + k);
                    sheet.answers[i] = s;
                    continue;
                }
                double v = sheet.tests[i] switch
                {
                    "bath" => volume, "dens" => System.Math.Round(rho, 2),
                    "angle" => angle, "mu" => System.Math.Round(System.Math.Tan(angle * System.Math.PI / 180), 2),
                    _ => force,
                };
                sheet.answers[i] = v.ToString("0.##", ru);
            }
            if (game.Hands.Reading != null) { game.Hands.CloseSheet(); yield return new WaitForSeconds(0.4f); }   // a free hand for the stamp
            game.Hands.Down(game.stamp);                             // the stamp
            yield return new WaitForSeconds(0.3f);
            game.Hands.Down(sheet);                                  // on the sheet
            for (float t = 0; t < 2 && !sheet.stamped; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.5f);
            moment = "stamped";
            game.Hands.Down(game.stamp);                             // the stamp back on its pad
            yield return new WaitForSeconds(0.3f);
            game.Hands.Down(sheet);                                  // the stamped sheet into the hand
            yield return new WaitForSeconds(0.4f);
            game.Hands.Down(game.dispatchBox);                       // into the box
            yield return new WaitForSeconds(0.3f);
            game.Hands.Down(sheet.sample);                           // its sample
            yield return new WaitForSeconds(0.4f);
            game.Hands.Down(game.dispatchBox);                       // into the box too
            yield return new WaitForSeconds(0.5f);
            moment = "boxed";
            game.Hands.Down(game.dispatchBox);                       // pack it
            yield return new WaitForSeconds(0.25f);
            moment = "packing";                                // the first flap folding over
            for (float t = 0; t < 3 && !game.Desk.BoxPacked; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(0.4f);
            moment = "packed";
            yield return new WaitForSeconds(0.3f);
            game.Hands.Down(game.dispatchBox);                       // take it up
            yield return new WaitForSeconds(walk * 0.5f);
            moment = "sending";
            var owner = game.customers.Find(c => c.Order == sheet);
            for (float t = 0; t < 8 && owner != null && !owner.Waiting; t += Time.deltaTime) yield return null;
            if (owner != null) game.Hands.Down(owner);               // to its customer
            yield return new WaitForSeconds(0.75f);
            moment = "paid";                                   // the coins on their way to the counter
            for (float t = 0; t < 6 && game.Desk.Sending; t += Time.deltaTime) yield return null;
            _busy = false;
        }
    }
}
