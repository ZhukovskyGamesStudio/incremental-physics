using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The orders of the lab before the order sheets, kept for later and idle now (DESIGN.md 2б, «Заказы»): a sample in a
    /// slot of the counter with a tag of what to find out about it — the formulas of the departments to put it through.
    /// Carry it to the apparatus, work them, bring it back to the counter: done, the order pays everything on its tag and
    /// the sample goes back to its owner. Chalk helpers bought on the map do the same by themselves (Helper3D).
    public class TagOrders : MonoBehaviour
    {
        Lab3DGame _game;
        float _nextOrder;

        GameState G => _game.G;
        List<Sample3D> Samples => _game.samples;

        /// An apparatus has done a test: the order on its sample, what it pays and in which coin.
        public event System.Action<Apparatus3D, Cur, double> Paid;

        public void Init(Lab3DGame game) => _game = game;

        /// An order comes in now and then, while the counter has room for it.
        public void Orders()
        {
            if (_game.Closed || _game.day.T < _nextOrder) return;
            int open = 0;
            foreach (var s in Samples) if (s.order != null) open++;
            if (open >= G.CounterSlots) return;
            if (NewOrder()) _nextOrder = _game.day.T + G.OrderEvery;
        }

        /// A new order in a free slot of the counter: a fresh specimen and one to three tests of the departments there are.
        bool NewOrder()
        {
            Sample3D s = null;
            for (int i = 0; i < Samples.Count && i < G.CounterSlots; i++)
                if (Samples[i].order == null && !Samples[i].gameObject.activeSelf) { s = Samples[i]; break; }
            if (s == null) return false;
            var tests = new List<string>();
            foreach (var d in _game.departments) if (d.apparatus.Exists(a => a.Active)) tests.Add(d.id);
            if (tests.Count == 0) return false;
            float r = Random.value;
            int n = Mathf.Min(tests.Count, r < 0.5f ? 1 : r < 0.85f ? 2 : 3);
            while (tests.Count > n) tests.RemoveAt(Random.Range(0, tests.Count));
            var o = new Order { came = _game.day.T };
            o.tests.AddRange(tests);
            s.gameObject.SetActive(true);
            _game.Book.Dress(s, G.AddSample(true));
            s.order = o;
            s.tag.Show(o);
            s.Appear();
            return true;
        }

        /// A sample is back in its slot on the counter: with every test of its order done, the order pays it all and the
        /// sample goes back to its owner.
        public void Returned(Sample3D s)
        {
            var o = s.order;
            if (o == null || !o.Done || _game.Closed) return;
            double ideas = o.Pay(Cur.Obs), joules = o.Pay(Cur.J);
            G.Earn(Cur.Obs, ideas);
            G.Earn(Cur.J, joules);
            s.order = null;
            s.tag.Show(null);
            s.Vanish();
            var at = s.home.position + Vector3.up * 0.16f;
            var eye = _game.eye;
            if (ideas > 0) Chalk3DText.Pop(at, "+" + GameState.Fmt(ideas) + " " + GameState.Bulb, ChalkTex.Cyan, 0.08f, eye, 1.8f);
            if (joules > 0) Chalk3DText.Pop(at + Vector3.up * (ideas > 0 ? 0.1f : 0), "+" + GameState.Fmt(joules) + " Дж", ChalkTex.Yellow, 0.08f, eye, 1.8f);
            _game.Book.Paid(o);
            _game.Hud.ShowMoney();
        }

        /// A job for a helper: the oldest order on the counter with a test whose department has a free apparatus. Both
        /// are promised to it until it lets go.
        public bool FindJob(Helper3D h, out Sample3D sample, out Apparatus3D app)
        {
            sample = null;
            app = null;
            foreach (var s in Samples)
            {
                var o = s.order;
                if (o == null || o.by != null || o.Done || s.At != Sample3D.Where.Crate || s.transform.parent != s.home) continue;
                if (sample != null && o.came >= sample.order.came) continue;
                foreach (var t in o.tests)
                {
                    if (!o.Wants(t)) continue;
                    var a = _game.Dept(t)?.Free();
                    if (a != null) { sample = s; app = a; break; }
                }
            }
            if (sample == null) return false;
            sample.order.by = h;
            app.helper = h;
            return true;
        }

        /// The apparatus has worked its sample, `share` of it (how well it was done): if the sample's order asks for this
        /// test, its result is written on the tag (the better of two tries; the pendulum counts its swings). It pays when
        /// the order is handed in at the counter.
        public void Fire(Apparatus3D a, double share, string line, Vector3 at)
        {
            var s = a.Load;
            var o = s.order;
            var eye = _game.eye;
            var cam = eye.transform;
            var p = at + cam.right * 0.16f + Vector3.up * 0.04f - cam.forward * 0.06f;
            if (line != null) Chalk3DText.Pop(p, line, ChalkTex.White, 0.045f, eye);
            if (o == null || !o.tests.Contains(a.id) || (a.id == "pend" && o.got.ContainsKey("pend")))
            {
                if (o == null || !o.tests.Contains(a.id)) Chalk3DText.Pop(p + Vector3.up * 0.065f, "не по заказу", ChalkTex.Dim, 0.05f, eye, 1.2f);
                return;
            }
            double v = G.Measure(Defs.F(a.id), s.data, share, out var cur);
            if (a.id == "pend")
            {
                o.swings++;
                o.swingSum += v;
                if (o.swings >= Order.PendSwings) o.got["pend"] = o.swingSum;
            }
            else if (!o.got.TryGetValue(a.id, out var had) || v > had) o.got[a.id] = v;
            s.tag.Show(o);
            Paid?.Invoke(a, cur, v);
            Chalk3DText.Pop(p + Vector3.up * 0.065f, "+" + GameState.Fmt(v) + " " + Order.Unit(cur), cur == Cur.Obs ? ChalkTex.Cyan : ChalkTex.Yellow, 0.06f, eye, 1.2f);
        }
    }
}
