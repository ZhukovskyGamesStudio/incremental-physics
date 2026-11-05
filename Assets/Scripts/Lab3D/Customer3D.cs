using System.Collections;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A customer (DESIGN.md 2б, «Заказчики»): someone of the trade its order speaks of (Persona), outside the lab — in
    /// the corridor behind the counter's window; the lab is the player's own and nobody else walks about in it. It comes
    /// along the corridor, waits its turn and steps up to the window with a "!" over its head. A click on it: it hands
    /// the order over the sill — the sheet and the sample on it — onto a free place on the counter, and steps back to
    /// wait. When its order goes up the pipe it takes it the way it was done — hops for joy at a good answer, nods at a
    /// so-so one, shakes its head at a bad one — and walks off down the corridor.
    public class Customer3D : MonoBehaviour
    {
        public Lab3DGame game;
        public Transform look;      // the persona is built under it
        public float speed = 1.3f;
        /// What it is doing, for the check.
        public string Doing { get; private set; } = "";
        public OrderSheet Order { get; private set; }
        /// At the window with its order, waiting to be served: a click takes the order.
        public bool AtWindow { get; private set; }

        double _accuracy = -1;
        Transform _carried;
        /// Served and waiting a step back from the window for its order to come back.
        public bool Waiting => Doing == "ждёт";

        /// Takes the packed box handed to it: it holds it before itself and goes off with it.
        public void Carry(Transform parcel)
        {
            _carried = parcel;
            parcel.SetParent(transform, true);
        }
        bool _taken;
        float _top, _bob;

        public IEnumerator Visit(OrderSheet sheet, int number)
        {
            Order = sheet;
            _accuracy = -1;
            _taken = false;
            _top = Persona.Build(sheet.who, look, game.kit);
            int stars = Career.Stars(game.G, sheet.who);   // a regular: his stars over his head
            if (stars > 0) Chalk3DText.Label(look, Vector3.up * (_top + 0.38f), Career.StarsText(stars), ChalkTex.Yellow, 0.1f, game.eye);
            if (sheet.contract) Chalk3DText.Label(look, Vector3.up * (_top + (stars > 0 ? 0.5f : 0.38f)), "контракт", ChalkTex.Cyan, 0.07f, game.eye);
            transform.position = game.Line.CorridorEnd(true);
            gameObject.SetActive(true);
            Doing = "идёт";
            game.Line.Queue(this);
            while (!game.Line.Turn(this))
            {
                yield return Walk(game.Line.QueueSpot(this), true);
                yield return null;
            }
            yield return Walk(game.Line.WindowSpot(), false);
            Face(Vector3.back);
            AtWindow = true;
            Doing = "у окна";
            Sfx.Play("bell", 0.5f, 1.2f);
            var mark = Chalk3DText.Label(transform, Vector3.up * (_top + 0.15f), "!", ChalkTex.Yellow, 0.16f, game.eye);
            // its patience runs from the moment it is at the window until its order comes back
            _patience = 0;
            _patienceMax = OrderBook.PatienceFor(sheet);
            ShowClock(true);
            while (!_taken)
            {
                Idle();
                if (Impatient()) { AtWindow = false; Destroy(mark.gameObject); yield return GiveUp(-1); yield break; }
                yield return null;
            }
            AtWindow = false;
            Destroy(mark.gameObject);
            Doing = "отдаёт";
            yield return Lean(0.18f, 14);
            yield return game.Line.HandOver(sheet, number, transform.position + Vector3.up * 1.3f + transform.forward * 0.3f);
            yield return Lean(0.15f, 0);
            game.Line.WindowFree(this);
            Doing = "ждёт";
            int spot = game.Line.TakeSpot();
            yield return Walk(game.Line.Spot(spot), false);
            Face(Vector3.back);
            while (_accuracy < 0 && !game.Closed)
            {
                Idle();
                if (Impatient()) { yield return GiveUp(spot); yield break; }
                yield return null;
            }
            ShowClock(false);
            if (game.Closed) yield break;
            Order = null;   // served: the counter's sheet may come back with someone else's order now
            yield return new WaitForSeconds(0.4f);   // the coins first, then what it thinks
            yield return React();
            game.Line.FreeSpot(spot);
            Doing = "уходит";
            yield return Walk(game.Line.CorridorEnd(false), false);
            Order = null;
            Doing = "";
            if (_carried != null) Destroy(_carried.gameObject);
            _carried = null;
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ patience (DESIGN.md 2б, «Пиццерия»)

        float _patience, _patienceMax;
        Transform _clock;
        LineRenderer _ring, _rim;
        Material _ringMat;

        /// The check runs its patience out at once (to see a customer give up).
        public void RunOut() => _patience = _patienceMax;

        /// How much of its patience is left, 1 to 0: what the tip is paid by.
        public float Hurry => _patienceMax > 0 ? Mathf.Clamp01(1 - _patience / _patienceMax) : 0;

        /// Its patience runs while the day does — not while its order lies packed and ready for it (handing it over is
        /// a moment). True when it has run out.
        bool Impatient()
        {
            if (game.day.Running && !(game.Desk.BoxOrder == Order && game.Desk.BoxPacked)) _patience += Time.deltaTime;
            Clock();
            return _patience >= _patienceMax && !game.Closed;
        }

        /// It has waited long enough: says so, shakes its head and goes off; its order leaves the counter with it.
        IEnumerator GiveUp(int spot)
        {
            ShowClock(false);
            if (spot < 0) game.Line.WindowFree(this);
            Doing = "уходит";
            game.Book.Lost(Order);
            var at = transform.position + Vector3.up * (_top + 0.1f);
            Sfx.Play("nope", 0.45f, 0.6f);
            Chalk3DText.Pop(at, "ну и ладно!", ChalkTex.Red, 0.09f, game.eye, 1.8f);
            yield return Shake(22);
            if (spot >= 0) game.Line.FreeSpot(spot);
            yield return Walk(game.Line.CorridorEnd(false), false);
            Order = null;
            Doing = "";
            gameObject.SetActive(false);
        }

        void ShowClock(bool on)
        {
            if (on && _clock == null)
            {
                _clock = new GameObject("Patience").transform;
                _clock.SetParent(transform, false);
                _rim = Art3D.LocalLine("Rim", _clock, Art3D.Unlit(new Color(0.32f, 0.35f, 0.33f)), 0.006f);
                _ring = Art3D.LocalLine("Left", _clock, _ringMat = Art3D.Unlit(), 0.016f);
                _rim.positionCount = 41;
                for (int i = 0; i <= 40; i++) { float a = i / 40f * Mathf.PI * 2; _rim.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.075f); }
            }
            if (_clock != null) _clock.gameObject.SetActive(on);
            if (on) Clock();
        }

        /// A chalk clock beside its head: the arc of what is left of its patience, green, then yellow, then a red that
        /// blinks at the end.
        void Clock()
        {
            if (_clock == null || !_clock.gameObject.activeSelf) return;
            _clock.localPosition = new Vector3(0.34f, _top - 0.08f, 0);
            if (game.eye != null) _clock.rotation = Quaternion.LookRotation(_clock.position - game.eye.transform.position, Vector3.up);
            float left = Hurry;
            int n = Mathf.Max(2, Mathf.CeilToInt(40 * left) + 1);
            _ring.positionCount = n;
            for (int i = 0; i < n; i++)
            {   // from twelve o'clock, round the way the hands go
                float a = Mathf.PI / 2 - 2 * Mathf.PI * left * i / (n - 1);
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.075f);
            }
            Color c = left > 0.5f ? ChalkTex.Green : left > 0.25f ? ChalkTex.Yellow : ChalkTex.Red;
            if (left < 0.15f && Mathf.Repeat(Time.time * 3, 1) < 0.35f) c *= 0.4f;
            _ringMat.color = c;
            _ring.enabled = left > 0.001f;
        }

        /// The player clicked it at the window: the order is taken.
        public void Take() { if (AtWindow) _taken = true; }

        /// Its order has gone up the pipe, done this well.
        public void Served(double accuracy) => _accuracy = accuracy;

        /// Standing: a slow breath, and it turns a little to keep the player in view.
        void Idle()
        {
            _bob += Time.deltaTime * 2.2f;
            look.localPosition = Vector3.up * (0.008f * Mathf.Sin(_bob));
            var to = game.walker.transform.position - transform.position;
            to.y = 0;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Vector3.Slerp(Vector3.back, to.normalized, 0.35f), Vector3.up), 1 - Mathf.Exp(-3 * Time.deltaTime));
        }

        /// Hops for joy, nods, or shakes its head — the answer's accuracy, as the customer sees it.
        IEnumerator React()
        {
            var at = transform.position + Vector3.up * (_top + 0.1f);
            if (_accuracy >= 0.8)
            {
                Sfx.Play("coins", 0.35f, 1.6f);
                Chalk3DText.Pop(at, "спасибо!", ChalkTex.Green, 0.09f, game.eye, 1.8f);
                for (int i = 0; i < 2; i++) yield return Jump(0.16f, 0.3f);
            }
            else if (_accuracy >= 0.4)
            {
                Chalk3DText.Pop(at, "ну… ладно", ChalkTex.White, 0.08f, game.eye, 1.8f);
                yield return Lean(0.2f, 14);
                yield return Lean(0.2f, 0);
            }
            else
            {
                Sfx.Play("nope", 0.35f, 0.7f);
                Chalk3DText.Pop(at, "хм…", ChalkTex.Yellow, 0.09f, game.eye, 1.8f);
                yield return Shake(28);
            }
            yield return new WaitForSeconds(0.3f);
        }

        /// Shakes its head for a moment, `rate` the quicker.
        IEnumerator Shake(float rate)
        {
            var rot = transform.rotation;
            for (float k = 0; k < 0.7f; k += Time.deltaTime)
            {
                transform.rotation = rot * Quaternion.Euler(0, Mathf.Sin(k * rate) * 14 * (1 - k / 0.7f), 0);
                yield return null;
            }
            transform.rotation = rot;
        }

        IEnumerator Jump(float height, float time)
        {
            var from = transform.position;
            for (float k = 0; k < 1; k += Time.deltaTime / time)
            {
                transform.position = from + Vector3.up * (Mathf.Sin(k * Mathf.PI) * height);
                yield return null;
            }
            transform.position = from;
        }

        /// Leans forward (over the sill, a nod) to `deg` degrees: a bow from the hips, so the body below them stays where
        /// it is and does not go into the sill.
        IEnumerator Lean(float t, float deg)
        {
            var from = look.localRotation;
            var to = Quaternion.Euler(deg, 0, 0);
            var hip = Vector3.up * 0.9f;
            for (float k = 0; k < 1; k += Time.deltaTime / t)
            {
                var r = Quaternion.Slerp(from, to, k);
                look.localRotation = r;
                look.localPosition = hip - r * hip;
                yield return null;
            }
            look.localRotation = to;
            look.localPosition = hip - to * hip;
        }

        void Face(Vector3 dir) => transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        /// Walks there along the corridor floor, bobbing, facing where it goes; `once` stops after a short way (to look
        /// again whether its turn has come).
        IEnumerator Walk(Vector3 to, bool once)
        {
            to.y = 0;
            float until = Time.time + (once ? 0.4f : 60);
            while ((transform.position - to).sqrMagnitude > 0.0004f && Time.time < until)
            {
                var d = to - transform.position;
                d.y = 0;
                float step = speed * Time.deltaTime;
                if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d, Vector3.up), 1 - Mathf.Exp(-10 * Time.deltaTime));
                transform.position = d.magnitude <= step ? to : transform.position + d.normalized * step;
                _bob += Time.deltaTime * 9;
                look.localPosition = Vector3.up * (0.03f * Mathf.Abs(Mathf.Sin(_bob)));
                yield return null;
            }
            look.localPosition = Vector3.zero;
        }
    }
}
