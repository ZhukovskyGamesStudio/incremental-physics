using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The customers behind the counter's window (DESIGN.md 2б, «Заказчики»): when each comes, the line along the corridor
    /// to the window, the places a served customer waits in, and the order it brings — the sheet with its sample, dealt
    /// as it comes and put on the counter over the sill when it is served. The customers themselves walk and wait
    /// (Customer3D); this keeps the timetable and the places.
    public class CustomerLine : MonoBehaviour
    {
        /// The window in the wall behind the counter (its middle on the corridor's floor) and the corridor outside.
        public static readonly Vector3 Window = new Vector3(1.95f, 0, 3.98f);
        const float CorridorZ = 4.12f, BackZ = 4.6f;

        Lab3DGame _game;
        readonly List<Customer3D> _queue = new List<Customer3D>();
        Customer3D _atWindow;
        readonly bool[] _spots = new bool[5];
        int _arrivals;
        float _nextArrival = 1f;
        readonly HashSet<OrderSheet> _incoming = new HashSet<OrderSheet>();
        readonly Dictionary<OrderSheet, (Vector3 p, Quaternion r)> _slot = new Dictionary<OrderSheet, (Vector3, Quaternion)>();

        GameState G => _game.G;
        LabDay Day => _game.day;
        List<OrderSheet> Sheets => _game.sheets;

        public void Init(Lab3DGame game) => _game = game;

        /// Its place on the counter, where the customer puts it down.
        public void Remember(OrderSheet sh) => _slot[sh] = (sh.transform.localPosition, sh.transform.localRotation);

        /// The sheet is still coming out of the pipe: not to be taken yet.
        public bool Incoming(OrderSheet s) => _incoming.Contains(s);
        /// Some order is on its way to the counter in a customer's hands.
        public bool AnyIncoming => _incoming.Count > 0;
        /// All of the day's customers have come.
        public bool AllCame => _arrivals >= G.OrdersPerDay;

        /// A customer gives up before its order is on the counter: it never comes. False if it was there already.
        public bool CallOff(OrderSheet sheet)
        {
            if (!_incoming.Remove(sheet)) return false;
            sheet.gameObject.SetActive(false);
            sheet.sample.gameObject.SetActive(false);
            return true;
        }

        /// The customers come on a timetable (DESIGN.md 2б, «Пиццерия»): as many a day as the map has bought, the first at
        /// once, the last at a little past four, closer together round noon (the rush hour) — whether or not the ones
        /// before have been served. A lab with nothing to do calls the next one at once. None after six; a full counter
        /// makes the next one wait. Every frame, from the game.
        public void Tick()
        {
            if (_game.Closed || Day.Evening || _arrivals >= G.OrdersPerDay) return;
            bool idle = _incoming.Count == 0 && Sheets.TrueForAll(s => !s.gameObject.activeSelf || s.sent);
            if (Day.T < _nextArrival && !(idle && Day.T > 1f)) return;
            var free = Sheets.Find(s => !s.gameObject.activeSelf && !_incoming.Contains(s));
            if (free == null || !_game.customers.Exists(c => !c.gameObject.activeSelf)) return;   // no room, or nobody to bring it yet
            _arrivals++;
            _nextArrival = Mathf.Max(Day.T + 2f, ArrivalTime(_arrivals));
            StartCoroutine(Arrive(free, _arrivals));
        }

        /// When the n-th customer of the day (from 0) comes: the first at once, the last at 0.8 of the day; the gaps are a
        /// third as long at noon as in the morning and the evening.
        float ArrivalTime(int n) => Timetable(n, G.OrdersPerDay, Day.length);

        /// The timetable itself: when the n-th of `all` customers comes in a day `length` seconds long.
        public static float Timetable(int n, int all, float length)
        {
            float u = Mathf.Clamp01(n / (float)Mathf.Max(1, all));
            float f = u + 0.5f * Mathf.Sin(2 * Mathf.PI * u) / (2 * Mathf.PI);
            return 0.8f * length * f;
        }

        /// A customer brings the order: one of them who is free, along the corridor to the window. The sheet is dealt now
        /// (the customer is of its story's trade) and put on the counter when the customer is served.
        IEnumerator Arrive(OrderSheet sh, int n)
        {
            var c = _game.customers.Find(x => !x.gameObject.activeSelf);
            _incoming.Add(sh);
            _game.Book.Deal(sh, n, false);
            if (c == null) { _incoming.Remove(sh); sh.gameObject.SetActive(true); sh.sample.gameObject.SetActive(true); yield break; }
            yield return c.Visit(sh, n);
        }

        /// Where the corridor comes from (to the right, past the window) and where it goes (to the left).
        public Vector3 CorridorEnd(bool start) => new Vector3(start ? 4.4f : -0.6f, 0, CorridorZ);
        public Vector3 WindowSpot() => Window;

        /// In the line for the window: the first one in it whose turn it is steps up, once the window is free.
        public void Queue(Customer3D c) { if (!_queue.Contains(c)) _queue.Add(c); }

        public bool Turn(Customer3D c)
        {
            if (_atWindow != null || _queue.Count == 0 || _queue[0] != c) return false;
            _atWindow = c;
            _queue.Remove(c);
            return true;
        }

        /// Its place in the line: along the corridor to the right of the window, one behind the other.
        public Vector3 QueueSpot(Customer3D c) => new Vector3(Window.x + 0.75f + 0.6f * Mathf.Max(0, _queue.IndexOf(c)), 0, CorridorZ);

        public void WindowFree(Customer3D c) { if (_atWindow == c) _atWindow = null; }

        /// The customer hands the order over the sill: the sheet with its sample comes from its hands onto its place.
        public IEnumerator HandOver(OrderSheet sh, int n, Vector3 from)
        {
            var t = sh.transform;
            var (rest, restRot) = _slot[sh];
            sh.gameObject.SetActive(true);
            sh.sample.gameObject.SetActive(true);
            var local = t.parent.InverseTransformPoint(from);
            Sfx.Play("paper", 0.75f);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.5f)
            {
                float e = k * k * (3 - 2 * k);
                t.localPosition = Vector3.Lerp(local, rest, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f);
                t.localRotation = Quaternion.Slerp(restRot * Quaternion.Euler(0, 25, 0), restRot, e);
                yield return null;
            }
            t.localPosition = rest;
            t.localRotation = restRot;
            Sfx.Play("pickup", 0.6f, 0.9f);
            Chalk3DText.Pop(sh.leaf.position + Vector3.up * 0.14f, "заказ №" + n, ChalkTex.White, 0.05f, _game.eye, 1.6f);
            _incoming.Remove(sh);
        }

        /// The places a served customer waits in, a step back from the window: in sight through it, side by side.
        public int TakeSpot()
        {
            int[] order = { 0, 1, 2, 3, 4 };
            foreach (int i in order) if (!_spots[i]) { _spots[i] = true; return i; }
            return _spots.Length - 1;
        }

        public void FreeSpot(int i) { if (i >= 0 && i < _spots.Length) _spots[i] = false; }

        public Vector3 Spot(int i)
        {
            float[] xs = { Window.x - 0.42f, Window.x + 0.42f, Window.x - 0.95f, Window.x + 0.95f, Window.x };
            return new Vector3(xs[Mathf.Clamp(i, 0, xs.Length - 1)], 0, BackZ + (i == 4 ? 0.35f : 0));
        }
    }
}
