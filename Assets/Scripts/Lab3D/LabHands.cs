using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The player's hands (the mouse, or the check's bot): what they hold — a sample or two in the right, an order sheet
    /// in the left — and what a click does on what the dot is on. Left button: take, put, work (hold to lift or squeeze)
    /// — on an apparatus with a sample the left button always works it. Right button: read an order, take a sample off an
    /// apparatus, or swap it for the one in hand when the hands are full.
    public class LabHands : MonoBehaviour
    {
        Lab3DGame _game;
        readonly List<Sample3D> _holding = new List<Sample3D>();
        SheetView _reading;   // the sheet held before the eyes
        Apparatus3D _pressing;
        Transform _hand, _leftHand, _stampHand;
        int _closedFrame = -1;

        GameState G => _game.G;
        DispatchDesk Desk => _game.Desk;
        LabPointer Pointer => _game.Pointer;
        AwardWall Wall => _game.awards;   // the certificates: one may be in both hands

        public void Init(Lab3DGame game) => _game = game;

        /// Where things are carried, before the eyes: the samples in the right hand, the stamp in the left.
        public void MakeAnchors()
        {
            var eye = _game.eye.transform;
            _hand = new GameObject("Hand").transform;
            _hand.SetParent(eye, false);
            _hand.localPosition = new Vector3(0.16f, -0.13f, 0.38f);
            _leftHand = new GameObject("LeftHand").transform;
            _leftHand.SetParent(eye, false);
            _leftHand.localPosition = new Vector3(-0.15f, -0.15f, 0.4f);
            _stampHand = new GameObject("StampHand").transform;
            _stampHand.SetParent(eye, false);
            _stampHand.localPosition = new Vector3(-0.13f, -0.2f, 0.36f);
        }

        /// The day has started (the hands are there): nothing is done before.
        public bool Ready => _hand != null;
        /// Where the right hand carries a sample (and both hands the box).
        public Transform Anchor => _hand;
        /// Where the stamp is held.
        public Transform StampAnchor => _stampHand;

        /// The sample on top in the hands (the one put down next), or null.
        public Sample3D Held => _holding.Count > 0 ? _holding[_holding.Count - 1] : null;
        public int HandCount => _holding.Count;
        public IReadOnlyList<Sample3D> Holding => _holding;
        public bool Holds(Sample3D s) => _holding.Contains(s);
        /// One sample in the hands; two with the tray from the map.
        int HandSize => G.HasPerk("tray") ? 2 : 1;
        /// No room for another sample (or the box is in the hands).
        public bool Full => _holding.Count >= HandSize || Desk.BoxInHand;
        /// The sheet in the left hand, or null.
        public OrderSheet Reading => _reading != null ? _reading.Sheet : null;
        /// The sheet in the left hand, as it is shown (open or flying back).
        public SheetView ReadingView => _reading;
        /// Nothing in the hands: a certificate can be taken off the wall.
        public bool Empty => Held == null && !Desk.StampHeld && _reading == null && !Desk.BoxInHand && !Desk.Sending && !_game.Closed;
        /// The sheet was put down this very frame: the click (or Esc) that did it is spent on that.
        public bool ClosedThisFrame => Time.frameCount == _closedFrame;
        /// The dot is on a top surface (a table, the floor, a box, a sample's top) a held sample can be laid on.
        bool OnTop => Pointer.HitAny && Pointer.HitNormal.y > 0.6f;
        bool IsCrate(Object o) => o == (Object)_game.crate;

        // ------------------------------------------------------------------ the buttons

        /// Left button down on `target`.
        public void Down(Object target)
        {
            if (Wall != null && Wall.Holding) { Wall.Click(target); return; }   // both hands on the certificate
            switch (target)
            {
                case Sample3D s when s.At is Sample3D.Where.Crate or Sample3D.Where.Free:
                    if (Held != null && OnTop) DropHeld();   // on top of it: a tower
                    else if (!Full) Take(s);
                    else if (s.At == Sample3D.Where.Crate) PutBack();
                    break;
                case OrderSheet sheet:
                    if (Desk.StampHeld) Desk.StampOn(sheet);   // the stamp in hand stamps, whatever the other hand holds
                    else OpenSheet(sheet);   // into the left hand, its sample in hand or not (the counter takes the sample back)
                    break;
                case Apparatus3D a:
                    if (a.Load != null && a.HasAction) { a.Down(); _pressing = a; }   // working it, whatever is in hand
                    else if (a.Busy || a.helper != null) break;                       // a helper's: hands off
                    else if (Held != null) PutOn(a);   // on an empty one; on the dynamometer, a swap
                    else if (a.Load != null) Take(a.Take());
                    break;
                case Customer3D cu:
                    if (cu.AtWindow) { cu.Take(); break; }
                    Desk.Give(cu);
                    break;
                default:
                    if (target is CalcKey key) { key.calc?.Press(key); break; }
                    if (target is Pressable press) { press.Press(); break; }
                    if (target == (Object)_game.stamp && !Desk.Stamping) { Desk.ClickStamp(); break; }
                    if (target == (Object)_game.dispatchBox && !Desk.Sending && !Desk.BoxInHand) { Desk.BoxClick(); break; }
                    if (IsCrate(target) && _reading != null) { CloseSheet(); break; }             // the sheet back on the counter first
                    if (Held != null && OnTop && target != (Object)_game.door) { DropHeld(); break; }   // laid down anywhere
                    if (IsCrate(target) && Held != null) PutBack();
                    if (target == (Object)_game.door) _game.Leave();
                    break;
            }
        }

        /// Left button up, wherever the dot is: lets go of what was being lifted or squeezed.
        public void Up()
        {
            if (_pressing != null) _pressing.Up();
            _pressing = null;
        }

        /// Right button: reads an order, or takes the sample off an apparatus (or swaps it for the one in hand).
        public void Alt(Object target)
        {
            if (Wall != null && Wall.Holding) { Wall.PutBack(); return; }
            // an order: the right button reads it, the sheet or the sample on it (close up the sample hides the sheet)
            var sheet = target as OrderSheet ?? _game.Book.SheetOf(target as Sample3D);
            if (sheet != null && !sheet.sent && !_game.Line.Incoming(sheet)) { OpenSheet(sheet); return; }
            if (target is not Apparatus3D a || a.Load == null || a.Busy || a.helper != null) return;
            if (Full) PutOn(a);   // full hands: a swap
            else Take(a.Take());
        }

        /// What putting the top sample back on the counter does: hands in its order, or only puts it down.
        string BackVerb => Held != null && Held.order != null && Held.order.Done ? "сдать" : "вернуть";

        /// What a click would do, in a word or two (DESIGN.md, «Меньше текста»).
        public string Hint(Object target)
        {
            if (Wall != null && Wall.Holding) return Wall.HintFor(target);
            switch (target)
            {
                case Sample3D s:
                    if (Held != null && OnTop) return "положить сверху";
                    return (!Full ? "взять" : BackVerb) + (_game.Book.SheetOf(s) != null ? "\nПКМ — читать заказ" : "");
                case OrderSheet sheet:
                    if (Desk.StampHeld) return sheet.stamped ? "уже принято" : sheet.Answered ? "поставить печать" : "сначала впиши ответы";
                    return Reading != null ? "взять этот заказ" : "взять заказ";
                case Apparatus3D a:
                    if (a.helper != null) return a.Load != null && a.HasAction ? a.Verb : "";
                    if (a.Load == null) return Held != null && !a.Busy ? a.PutVerb : "";
                    if (a.HasAction) return a.Verb + (a.Busy ? "" : Full ? "\nПКМ — поменять" : "\nПКМ — снять");
                    if (a.Busy) return "";
                    return Held != null ? a.PutVerb : "снять";
                default:
                    if (target == (Object)_game.door) return "домой";
                    if (target is CalcKey key) return "нажать «" + key.key + "»";
                    if (target is Pressable press) return press.Hint;
                    if (target is Customer3D who) return who.AtWindow ? "взять заказ" : Desk.BoxOrder != null && who.Order == Desk.BoxOrder ? "отдать заказ" : "не его заказ";
                    if (target == (Object)_game.stamp) return Desk.StampHint;
                    if (target == (Object)_game.dispatchBox) return Desk.BoxHint;
                    if (IsCrate(target) && _reading != null) return "положить листок";
                    if (Held != null && OnTop) return "положить";
                    return IsCrate(target) && Held != null ? BackVerb : "";
            }
        }

        // ------------------------------------------------------------------ the samples in the right hand

        /// The top sample in hand goes onto the apparatus; the one that was there comes into the hands.
        void PutOn(Apparatus3D a)
        {
            var top = Held;
            _holding.Remove(top);
            Take(a.Put(top));
            Arrange();
        }

        public void Take(Sample3D s)
        {
            if (s == null || _holding.Contains(s)) return;
            _holding.Add(s);
            Sfx.Play("pickup", 0.55f, 1f, 0.08f);
            Arrange();
        }

        /// A sample out of the hands, to wherever the game puts it (the box).
        public void Release(Sample3D s)
        {
            _holding.Remove(s);
            Arrange();
        }

        /// The samples in the hands: the top one in front, the other one on the tray beside it.
        void Arrange()
        {
            for (int i = 0; i < _holding.Count; i++)
                _holding[i].MoveTo(_hand, i == _holding.Count - 1 ? Vector3.zero : new Vector3(-0.11f, -0.015f, 0.05f), Sample3D.Where.Hand);
        }

        /// A sample thrown at a window (DESIGN.md 2б): it flies, one pane of the four shatters, five coins go on the glazier,
        /// and the sample drops to the floor under the window.
        public void Throw(WindowPane pane)
        {
            var s = Held;
            if (s == null || pane.broken || _game.Closed) return;
            _holding.Remove(s);
            Arrange();
            StartCoroutine(ThrowIt(s, pane));
        }

        IEnumerator ThrowIt(Sample3D s, WindowPane pane)
        {
            s.MoveTo(null, s.transform.position, Sample3D.Where.Hand);
            s.Driven = true;
            var from = s.transform.position;
            var to = pane.transform.position;
            Sfx.Play("whoosh", 0.6f, 1.3f);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.3f)
            {
                s.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
                s.transform.Rotate(400 * Time.deltaTime, 250 * Time.deltaTime, 0);
                yield return null;
            }
            pane.Break();
            Sfx.Play("crash", 1f);
            var c = G.Cur;
            c[(int)Cur.Coin] = System.Math.Max(0, c[(int)Cur.Coin] - 5);
            _game.Hud.ShowMoney();
            _game.Hud.Pulse();
            Chalk3DText.Pop(to + pane.transform.forward * 0.3f + Vector3.up * 0.2f, "−5 " + GameState.Coin + "  стекольщику", ChalkTex.Red, 0.07f, _game.eye, 2.4f);
            s.Driven = false;
            s.Drop(to + pane.transform.forward * 0.25f, Quaternion.Euler(0, Random.Range(0, 360f), 0));
        }

        /// The top sample in hand laid down where the dot is: it falls and lies there.
        void DropHeld()
        {
            var s = Held;
            _holding.Remove(s);
            var yaw = Quaternion.Euler(0, _game.eye.transform.eulerAngles.y + Random.Range(-8f, 8f), 0);
            s.Drop(Pointer.HitPoint + Pointer.HitNormal * 0.004f, yaw);
            Arrange();
            Sfx.Play("pickup", 0.45f, 0.8f, 0.08f);
        }

        /// The top sample in hand back into its slot on the counter: that hands in its order, if it is done.
        void PutBack()
        {
            var top = Held;
            _holding.Remove(top);
            top.GoHome();
            Arrange();
            _game.Tags.Returned(top);
        }

        // ------------------------------------------------------------------ the sheet in the left hand

        public void OpenSheet(OrderSheet sheet)
        {
            if (Desk.StampHeld) { Sfx.Play("nope", 0.5f); _game.Hud.Card("Руки заняты", "", "положи печать на подушечку", 1.6f); return; }
            if (_reading != null) CloseSheet();   // one sheet in the hand at a time
            _reading = sheet.view;
            _reading.Show();
            Pointer.Unlit(sheet);   // no highlight on it in the hand: the highlight is strokes of chalk over the writing
            Sfx.Play("paper", 0.7f);
        }

        /// The sheet is put back down on the counter; the eyes go back to the room.
        public void CloseSheet()
        {
            var v = _reading;
            _reading = null;
            if (v != null) { v.Hide(); Sfx.Play("paper", 0.45f, 0.9f); }
            _game.walker.frozen = false;
            // the eyes are the player's again at once: the click (or Esc) that put the sheet down is spent on that
            _game.walker.Catch();
            _closedFrame = Time.frameCount;
        }

        /// The sheet in the left hand goes where the game carries it (into the box): held no more, not put back down.
        public void LetGoSheet()
        {
            _reading.Away();
            _reading = null;
        }

        /// A customer has given up: its sheet and its sample out of the hands, the apparatus with it let go of.
        public void Forget(OrderSheet sheet)
        {
            if (_reading != null && Reading == sheet) { _reading.Away(); _reading = null; }
            var s = sheet.sample;
            if (_holding.Remove(s)) Arrange();
            if (_pressing != null && _pressing.Load == s) { _pressing.Up(); _pressing = null; }
        }
    }
}
