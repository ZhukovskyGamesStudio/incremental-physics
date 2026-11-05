using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The order desk's bureaucracy (DESIGN.md 2б, «Труба приёмки», «Посылка заказчику»): the stamp on its pad and the
    /// dispatch box. A sheet with every answer written in is stamped; the stamped sheet and its own sample go into the
    /// box; the box is packed (its flaps fold over, a tape across them), taken up and handed over the sill to its
    /// customer, who pays. Things the desk carries glide to their places: a sheet into the box, the stamp to the hand
    /// or back onto its pad, the box into the hands.
    public class DispatchDesk : MonoBehaviour
    {
        Lab3DGame _game;
        bool _stampHeld, _stamping, _packed, _boxInHand;
        OrderSheet _boxSheet;   // the sheet in the dispatch box
        Sample3D _boxSample;
        Transform _stampHome, _boxHome;
        Vector3 _stampPos, _boxPos;
        Quaternion _stampRot, _boxRot;
        readonly Dictionary<Transform, (Vector3 p, Quaternion r, float s)> _glide = new Dictionary<Transform, (Vector3, Quaternion, float)>();
        readonly Dictionary<Transform, Vector3> _glideVia = new Dictionary<Transform, Vector3>();

        GameState G => _game.G;
        Transform Stamp => _game.stamp;
        Transform Box => _game.dispatchBox;
        LabHands Hands => _game.Hands;

        /// The box is on its way to the customer, or being packed: the desk waits.
        public bool Sending { get; private set; }
        public bool StampHeld => _stampHeld;
        /// The stamp is coming down on a sheet.
        public bool Stamping => _stamping;
        /// The box is packed (its lid shut and taped); in the hands, to be given to its customer.
        public bool BoxPacked => _packed;
        public bool BoxInHand => _boxInHand;
        /// The order in the box (the customer it goes back to).
        public OrderSheet BoxOrder => _boxSheet;
        /// The sheet and its own sample are both in the box: it can be packed.
        public bool BoxReady => _boxSheet != null && _boxSample != null && _boxSample == _boxSheet.sample;

        public void Init(Lab3DGame game) => _game = game;

        /// In the morning: where the stamp and the box belong; the tape is not on yet; the stamp's strokes fixed to it.
        public void Setup()
        {
            if (Stamp != null) { _stampHome = Stamp.parent; _stampPos = Stamp.localPosition; _stampRot = Stamp.localRotation; }
            if (Box != null) { _boxPos = Box.localPosition; _boxRot = Box.localRotation; _boxHome = Box.parent; }
            if (_game.boxTape != null) _game.boxTape.gameObject.SetActive(false);
            // strokes fixed to a thing that is carried about: it does not swim with them or flicker as it turns
            if (Stamp != null) foreach (var r in Stamp.GetComponentsInChildren<Renderer>()) Art3D.Prop(r, "_Local", 1);
        }

        // ------------------------------------------------------------------ gliding

        /// Things the desk carries glide to their places: a sheet into the box, the stamp back to its pad.
        void GlideTo(Transform t, Transform parent, Vector3 pos, Quaternion rot, float scale, Vector3? via = null)
        {
            t.SetParent(parent, true);
            _glide[t] = (pos, rot, scale);
            if (via.HasValue) _glideVia[t] = via.Value;
            else _glideVia.Remove(t);
        }

        /// It glides no more: the game moves it from here on.
        public void Unglide(Transform t)
        {
            _glide.Remove(t);
            _glideVia.Remove(t);
        }

        /// Over the dispatch box's walls: things go into it from above.
        Vector3 OverBox => _game.boxInside.position + Vector3.up * 0.26f;

        /// Every frame, from the game.
        public void Tick()
        {
            float k = 1 - Mathf.Exp(-Time.deltaTime * 14);
            foreach (var kv in _glide)
            {
                var t = kv.Key;
                if (_glideVia.TryGetValue(t, out var via))
                {   // first over the edge it would cut through
                    t.position = Vector3.MoveTowards(Vector3.Lerp(t.position, via, k * 1.4f), via, Time.deltaTime * 0.4f);
                    t.localRotation = Quaternion.Slerp(t.localRotation, kv.Value.r, k);
                    if ((t.position - via).sqrMagnitude < 0.0001f) _glideVia.Remove(t);
                    continue;
                }
                t.localPosition = Vector3.Lerp(t.localPosition, kv.Value.p, k);
                t.localRotation = Quaternion.Slerp(t.localRotation, kv.Value.r, k);
                t.localScale = Vector3.Lerp(t.localScale, Vector3.one * kv.Value.s, k);
            }
        }

        // ------------------------------------------------------------------ the stamp

        /// A click on the stamp (or its pad), while it is not stamping: taken into the hand, or put back down.
        public void ClickStamp()
        {
            if (_stampHeld) StampBack();
            else if (Hands.Reading == null) TakeStamp();
            else { Sfx.Play("nope", 0.5f); _game.Hud.Card("Руки заняты", "", "положи листок на стойку", 1.6f); }
        }

        /// What a click on the stamp does.
        public string StampHint => _stamping ? "" : _stampHeld ? "положить печать" : Hands.Reading == null ? "взять печать" : "";

        void TakeStamp()
        {
            _stampHeld = true;
            GlideTo(Stamp, Hands.StampAnchor, Vector3.zero, Quaternion.Euler(-20, 20, 0), 1);
            Sfx.Play("tick", 0.6f, 0.8f);
        }

        void StampBack()
        {
            _stampHeld = false;
            GlideTo(Stamp, _stampHome, _stampPos, _stampRot, 1);
            Sfx.Play("place", 0.5f, 0.8f);
        }

        /// The stamp in hand comes down on a sheet.
        public void StampOn(OrderSheet sheet) => StartCoroutine(StampIt(sheet));

        /// The stamp on a sheet: only one with every answer written in. It comes down on the sheet's corner with a fat
        /// thump, the mark swells in, and the stamp goes back onto its pad.
        IEnumerator StampIt(OrderSheet sheet)
        {
            if (_stamping) yield break;
            if (sheet.stamped) { Sfx.Play("nope", 0.5f); yield break; }
            if (!sheet.Answered) { Sfx.Play("nope", 0.6f); _game.Hud.Card("Сначала впиши ответы", "", "листок — ЛКМ, цифры — с клавиатуры", 2f); yield break; }
            _stamping = true;
            var stamp = Stamp;
            _glide.Remove(stamp);
            stamp.SetParent(_stampHome, true);
            var over = sheet.leaf.TransformPoint(new Vector3(0.06f, 0.004f, -0.1125f));
            Vector3 from = stamp.position;
            var fromRot = stamp.rotation;
            var flat = sheet.leaf.rotation * Quaternion.Euler(0, -12, 0);
            for (float t = 0; t < 1; t += Time.deltaTime / 0.22f)   // over the sheet's corner, raised
            {
                float e = t * t * (3 - 2 * t);
                stamp.position = Vector3.Lerp(from, over + Vector3.up * 0.09f, e);
                stamp.rotation = Quaternion.Slerp(fromRot, flat, e);
                yield return null;
            }
            for (float t = 0; t < 1; t += Time.deltaTime / 0.07f)   // down, hard
            {
                stamp.position = over + Vector3.up * (0.09f * (1 - t * t));
                yield return null;
            }
            stamp.position = over;
            Sfx.Play("stamp", 1f, 1f, 0.04f);
            sheet.stamped = true;
            sheet.view.Stamped();
            stamp.localScale = new Vector3(1.08f, 0.85f, 1.08f);   // squashed by the blow
            yield return new WaitForSeconds(0.12f);
            stamp.localScale = Vector3.one;
            _stamping = false;
            GlideTo(stamp, Hands.StampAnchor, Vector3.zero, Quaternion.Euler(-20, 20, 0), 1);   // back in the hand: put it down when you like
        }

        // ------------------------------------------------------------------ the box: packed and handed back

        /// A click on the box, while it is on the counter and not on its way (DESIGN.md 2б, «Посылка заказчику»): in goes
        /// the stamped sheet from the left hand or the sample from the right; with both of its order in it, a click packs
        /// it (the lid shuts, a tape across it), another takes it up to be given to the customer; with empty hands it
        /// gives back what is in it.
        public void BoxClick()
        {
            if (_packed)
            {
                if (!_boxInHand && Hands.HandCount == 0 && Hands.Reading == null && !_stampHeld) TakeBox();
                else { Sfx.Play("nope", 0.5f); _game.Hud.Card("Руки заняты", "", "чтобы взять ящик, освободи руки", 1.6f); }
                return;
            }
            if (Hands.Reading != null)
            {
                var sheet = Hands.Reading;
                if (!sheet.stamped) { Sfx.Play("nope", 0.6f); _game.Hud.Card("Без печати не примут", "", "положи листок на стойку и поставь печать", 2f); return; }
                if (_boxSheet != null) { Sfx.Play("nope", 0.5f); return; }
                _boxSheet = sheet;
                Hands.LetGoSheet();
                GlideTo(sheet.leaf, _game.boxInside, Vector3.up * 0.002f, Quaternion.identity, 1, OverBox);
                Sfx.Play("paper", 0.6f, 0.9f);
                return;
            }
            if (Hands.Held != null)
            {
                if (_boxSample != null) { Sfx.Play("nope", 0.5f); return; }
                var s = Hands.Held;
                Hands.Release(s);
                _boxSample = s;
                if (s.Size > 0.185f)
                {   // too tall to stand under the lid (a bottle, a vase): it lies down along the box
                    s.MoveTo(_game.boxInside, new Vector3(0.02f, s.Extent.z / 2 + 0.004f, -s.Size / 2), Sample3D.Where.Box);
                    s.Turn(Quaternion.Euler(90, 0, 0));
                }
                else s.MoveTo(_game.boxInside, new Vector3(0.04f, 0.004f, 0.05f), Sample3D.Where.Box);
                s.Via(OverBox);
                Sfx.Play("thud", 0.5f, 1.3f);
                return;
            }
            if (BoxReady) { StartCoroutine(Pack()); return; }
            if (_boxSheet != null && _boxSample != null)
            {
                var whose = _game.sheets.Find(x => x.sample == _boxSample);
                Sfx.Play("nope", 0.6f);
                _game.Hud.Card("Не тот образец", "", whose != null ? "это образец заказа №" + whose.number : "нужен образец заказа №" + _boxSheet.number, 2f);
            }
            if (_boxSample != null && !Hands.Full) { var s = _boxSample; _boxSample = null; s.Via(OverBox); Hands.Take(s); return; }
            if (_boxSheet != null) { var sh = _boxSheet; _boxSheet = null; Unglide(sh.leaf); sh.view.Rest(); Hands.OpenSheet(sh); }
        }

        /// What a click on the box does.
        public string BoxHint
        {
            get
            {
                var reading = Hands.Reading;
                return Sending || _boxInHand ? "" : _packed ? "взять ящик" : reading != null ? (reading.stamped ? "положить листок в ящик" : "сначала печать") : Hands.Held != null ? "положить образец в ящик" : BoxReady ? "упаковать"
                    : _boxSample != null || _boxSheet != null ? "достать" : "";
            }
        }

        /// The box calls (pulses) while there is something to do with it: a stamped sheet in hand, its order in it, packed.
        public bool BoxCalls => (Hands.Reading != null && Hands.Reading.stamped) || BoxReady || _packed;

        /// The lid's two flaps and the angle each stands open at.
        (Transform flap, float open)[] Flaps()
        {
            var l = _game.boxLid.Find("FlapL");
            var r = _game.boxLid.Find("FlapR");
            return new[] { (l, 115f), (r, -115f) };
        }

        /// The lid claps shut and a strip of tape goes over it: the order is ready to go back.
        IEnumerator Pack()
        {
            Sending = true;
            // the flaps fold over one after the other, each with a little bounce as it lands
            foreach (var (flap, open) in Flaps())
            {
                for (float t = 0; t < 1; t += Time.deltaTime / 0.38f)
                {
                    float e = t < 0.8f ? Mathf.SmoothStep(0, 1.06f, t / 0.8f) : Mathf.Lerp(1.06f, 1f, (t - 0.8f) / 0.2f);
                    flap.localRotation = Quaternion.Euler(0, 0, open * (1 - e));
                    yield return null;
                }
                flap.localRotation = Quaternion.identity;
                Sfx.Play("lid", 0.7f, open > 0 ? 0.95f : 1.1f);
            }
            yield return new WaitForSeconds(0.12f);
            var tape = _game.boxTape;
            if (tape != null)
            {   // the tape is pulled over the lid and down the front
                tape.gameObject.SetActive(true);
                Sfx.Play("paper", 0.8f, 0.6f);
                for (float t = 0; t < 1; t += Time.deltaTime / 0.3f) { tape.localScale = new Vector3(1, 1, Mathf.SmoothStep(0.05f, 1, t)); yield return null; }
                tape.localScale = Vector3.one;
            }
            _packed = true;
            Chalk3DText.Pop(Box.position + Vector3.up * 0.25f, "для: " + Persona.Name(_boxSheet.who), ChalkTex.White, 0.05f, _game.eye, 2f);
            Sending = false;
        }

        void TakeBox()
        {
            _boxInHand = true;
            GlideTo(Box, Hands.Anchor, new Vector3(-0.04f, -0.2f, 0.3f), Quaternion.Euler(8, -10, 0), 0.6f);   // low in the hands: the view stays free
            Sfx.Play("pickup", 0.7f, 0.8f);
        }

        /// The box in hand, a click on a customer: its own takes it, anyone else is told whose it is.
        public void Give(Customer3D cu)
        {
            if (!_boxInHand || Sending) return;
            if (_boxSheet != null && cu.Order == _boxSheet) StartCoroutine(Deliver(cu));
            else { Sfx.Play("nope", 0.5f); _game.Hud.Card("Это не его заказ", "", _boxSheet != null ? "ящик для: " + Persona.Name(_boxSheet.who) : "", 2f); }
        }

        /// The box handed over the sill to its customer: the coins come (they pay by the answers' accuracy), the customer
        /// takes it the way it was done and goes off with it; a fresh empty box comes up on the counter.
        IEnumerator Deliver(Customer3D cu)
        {
            Sending = true;
            var sheet = _boxSheet;
            var box = Box;
            var hud = _game.Hud;
            var eye = _game.eye;
            _glide.Remove(box);
            box.SetParent(null, true);
            var from = box.position;
            var fromRot = box.rotation;
            var to = cu.transform.position + Vector3.up * 1.12f + cu.transform.forward * 0.28f;
            var toRot = cu.transform.rotation;
            Sfx.Play("pickup", 0.6f, 0.9f);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.5f)
            {
                float e = k * k * (3 - 2 * k);
                box.position = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.18f);
                box.rotation = Quaternion.Slerp(fromRot, toRot, e);
                yield return null;
            }
            double before = G.Cur[(int)Cur.Coin];
            hud.Roll(before);   // the counter shows the coins as they arrive, not at once
            var book = _game.Book;
            int coins = book.Submit(sheet, out var accuracy, cu.Hurry);
            Sfx.Play("coins", 1f, 1f, 0.03f);
            var at = cu.transform.position + Vector3.up * 1.95f;
            hud.FlyCoins(cu.transform.position + Vector3.up * 1.5f, before, G.Cur[(int)Cur.Coin], coins);
            Chalk3DText.Pop(at, "+" + coins + " " + GameState.Coin, ChalkTex.Green, 0.11f, eye, 2.4f);
            for (int i = 0; i < Mathf.Clamp(coins / 4, 3, 9); i++)
                Chalk3DText.Pop(at + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.15f, 0.1f), Random.Range(-0.1f, 0.1f)), GameState.Coin, ChalkTex.Green, 0.05f, eye, Random.Range(0.6f, 1.3f));
            hud.Pulse();
            int tip = book.LastTip;
            if (tip > 0) Chalk3DText.Pop(at + Vector3.up * 0.16f, "чаевые +" + tip, ChalkTex.Yellow, 0.07f, eye, 2.4f);
            if (!book.CounterDone) hud.Card("Заказ №" + sheet.number + " отдан", "+" + coins + " " + GameState.Coin, $"точность {accuracy * 100:0}%" + (tip > 0 ? $" · чаевые +{tip}" : ""), 2.5f);
            _boxSheet = null;
            _boxSample = null;
            book.Finish(sheet);   // the sheet and the sample, out of the box and gone: the customer has them in the parcel
            // the customer goes off with a copy of the packed box; the box itself comes back to the counter, empty
            _game.Body.HideBoxHands();   // the hands stay with the player
            var parcel = Instantiate(box.gameObject);
            foreach (var c in parcel.GetComponentsInChildren<Collider>()) Destroy(c);
            cu.Carry(parcel.transform);
            Home();
            for (float k = 0; k < 1; k += Time.deltaTime / 0.35f)
            {
                box.localScale = Vector3.one * Mathf.Lerp(0.2f, 1, 1 - (1 - k) * (1 - k));
                yield return null;
            }
            box.localScale = Vector3.one;
            Sfx.Play("thud", 0.6f);
            Sending = false;
        }

        /// The box back on its place on the counter, its flaps open, no tape, out of the hands.
        void Home()
        {
            var box = Box;
            box.SetParent(_boxHome, false);
            box.localPosition = _boxPos;
            box.localRotation = _boxRot;
            foreach (var (flap, open) in Flaps()) flap.localRotation = Quaternion.Euler(0, 0, open);
            if (_game.boxTape != null) _game.boxTape.gameObject.SetActive(false);
            _packed = false;
            _boxInHand = false;
        }

        /// The box back on its place on the counter, its flaps open, no tape: as after a delivery.
        void OpenBox()
        {
            if (!_packed && !_boxInHand) return;
            _glide.Remove(Box);
            Home();
            Box.localScale = Vector3.one;
            _game.Body.HideBoxHands();
        }

        /// A customer has given up: its sheet and its sample out of the box (and the box back on the counter, open, if it
        /// was in the hands or packed).
        public void Forget(OrderSheet sheet)
        {
            var s = sheet.sample;
            if (_boxSheet != sheet && _boxSample != s) return;
            if (_boxSheet == sheet) { _boxSheet = null; Unglide(sheet.leaf); }
            if (_boxSample == s) _boxSample = null;
            OpenBox();
        }
    }
}
