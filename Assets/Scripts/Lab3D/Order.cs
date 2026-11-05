using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// An order of the lab (DESIGN.md 2б, «Заказы»): a sample brought in to the counter with what to find out about
    /// it, the formulas of the apparatus to put it through. Every test done writes what it pays on the sample's tag;
    /// back on the counter with all of them done, the order pays it all at once and the sample goes back to its owner.
    public class Order
    {
        /// The pendulum's test is a few swings: it counts them, each paying mg.
        public const int PendSwings = 3;

        public readonly List<string> tests = new List<string>();                              // apparatus ids
        public readonly Dictionary<string, double> got = new Dictionary<string, double>();    // test -> what it pays
        public int swings;
        public double swingSum;
        public Helper3D by;     // a helper has taken it on
        public float came;      // the day's clock when it came in: the oldest goes first

        public bool Done { get { foreach (var t in tests) if (!got.ContainsKey(t)) return false; return true; } }
        public bool Wants(string test) => tests.Contains(test) && !got.ContainsKey(test);
        public double Pay(Cur c) { double s = 0; foreach (var kv in got) if (CurOf(kv.Key) == c) s += kv.Value; return s; }
        public static Cur CurOf(string test) { var f = Defs.F(test); return f.kind == FKind.Energy ? f.yields : Cur.Obs; }
        /// A test as the tag writes it: the formula's value, short.
        public static string Name(string test) => test switch { "dyna" => "F", "fall" => "mgh", "spring" => "kx²/2", "pend" => "T", _ => test };
        public static string Unit(Cur c) => c == Cur.Obs ? GameState.Bulb : "Дж";
    }

    /// The tag over an order's sample: one line a test, "?" until it is done, then what it pays, in the coin's colour.
    /// It faces the camera and keeps one size on the screen. On the counter, where the orders stand in a row, it is
    /// short — what is still to do, or "✓" — and reads in full under the dot.
    public class OrderTag : MonoBehaviour
    {
        Sample3D _s;
        Camera _eye;
        Order _o;
        readonly List<InkText> _lines = new List<InkText>();
        /// A line's height a metre away: the tag keeps one size on the screen, near or far.
        const float LineH = 0.04f, ShortH = 0.034f;
        /// Read in full: set every frame by the game (under the dot, off the counter).
        [System.NonSerialized] public bool full;
        bool _wasFull = true;

        public static OrderTag On(Sample3D s, Camera eye)
        {
            var rt = Art3D.WorldCanvas("Tag");
            var go = rt.gameObject;
            rt.sizeDelta = new Vector2(600, 240);
            var tag = go.AddComponent<OrderTag>();
            tag._s = s;
            tag._eye = eye;
            for (int i = 0; i < 3; i++)
                tag._lines.Add(InkText.On(UIF.Text(go.transform, "", 60, ChalkTex.White, new Vector2(0, 80 - 80 * i), new Vector2(600, 80))));
            go.SetActive(false);
            return tag;
        }

        public void Show(Order o)
        {
            _o = o;
            gameObject.SetActive(o != null);
            if (o == null) return;
            if (!full)
            {   // short: the tests still to do, or done
                var left = new List<string>();
                foreach (var t in o.tests) if (!o.got.ContainsKey(t)) left.Add(Order.Name(t));
                var col = left.Count == 0 ? ChalkTex.Green : ChalkTex.White;
                _lines[0].Label.enabled = true;
                _lines[0].Label.color = col;
                _lines[0].Set(left.Count == 0 ? "✓" : string.Join(" ", left) + " ?", col);
                for (int i = 1; i < _lines.Count; i++) _lines[i].Label.enabled = false;
                return;
            }
            for (int i = 0; i < _lines.Count; i++)
            {
                _lines[i].Label.enabled = i < o.tests.Count;
                if (i >= o.tests.Count) continue;
                string t = o.tests[i];
                var c = Order.CurOf(t);
                bool done = o.got.TryGetValue(t, out var v);
                var col = !done ? ChalkTex.White : c == Cur.Obs ? ChalkTex.Cyan : ChalkTex.Yellow;
                _lines[i].Label.color = col;
                _lines[i].Set(done ? Order.Name(t) + "  +" + GameState.Fmt(v) + " " + Order.Unit(c) : Order.Name(t) + "  ?", col);
            }
        }

        void LateUpdate()
        {
            if (_s == null || !_s.gameObject.activeInHierarchy) { gameObject.SetActive(false); return; }
            if (full != _wasFull) { _wasFull = full; Show(_o); }
            bool held = _s.At == Sample3D.Where.Hand;
            float d = _eye != null ? Vector3.Distance(_eye.transform.position, _s.transform.position) : 1;
            float h = (full || held ? LineH : ShortH) * Mathf.Max(d, 0.3f);
            int rows = full ? (_o != null ? _o.tests.Count : 1) : 1;
            transform.localScale = Vector3.one * (h / 80f);
            // the first line is 80 units over the canvas' middle: lift the tag so that its last line clears the sample
            float over = (rows - 1) * h - h * 0.5f;
            transform.position = _s.transform.position + Vector3.up * (_s.Size + (held ? 0.02f : 0.03f) + over);
            if (_eye != null) transform.rotation = Quaternion.LookRotation(transform.position - _eye.transform.position, Vector3.up);
        }
    }
}
