using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The dot in the middle of the screen: what it is on — a sample, a sheet, an apparatus, a customer at the window,
    /// the stamp, the box, the counter, the door — and the light under it. A thing is lit up only if a click on it does
    /// something; a new apparatus pulses while a sample is in hand, the door in the evening, the box when it is wanted.
    public class LabPointer : MonoBehaviour
    {
        Lab3DGame _game;
        string _aimedAt = "";
        Vector3 _hitPoint, _hitNormal;
        bool _hitAny;
        Collider _hitCol;
        readonly Dictionary<Renderer, float> _glow = new Dictionary<Renderer, float>();
        readonly Dictionary<Object, Renderer[]> _parts = new Dictionary<Object, Renderer[]>();
        Pressable _litPress;

        LabHands Hands => _game.Hands;

        public void Init(Lab3DGame game) => _game = game;

        /// What the dot is on: the point, its surface, the collider (a certificate is hung by them).
        public bool HitAny => _hitAny;
        public Vector3 HitPoint => _hitPoint;
        public Vector3 HitNormal => _hitNormal;
        public Collider HitCollider => _hitCol;
        /// What a click would act on now (Aim's result this frame).
        public Object Target { get; set; }
        /// What the line of sight ran into, and how far (F3 shows it under the dot).
        public string AimedAt => _aimedAt;

        /// What the dot points at: a sample on the counter, an apparatus (with whatever is on it), the counter's crate,
        /// the door; null for anything else.
        public Object Aim()
        {
            var ray = _game.eye.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            var hits = Physics.RaycastAll(ray, _game.reach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            Object found = null;
            Apparatus3D.Pointed = null;
            _aimedAt = "ничего";
            _hitAny = false;
            _hitCol = null;
            var walker = _game.walker.transform;
            foreach (var hit in hits)
            {
                // the player's own body and whatever is in the hands are not in the way
                if (hit.collider.transform.IsChildOf(walker)) continue;
                _aimedAt = hit.collider.name + " " + hit.distance.ToString("0.00");
                found = What(hit.collider);
                Apparatus3D.Pointed = hit.collider;
                Apparatus3D.PointedAt = hit.point;
                _hitPoint = hit.point;
                _hitNormal = hit.normal;
                _hitCol = hit.collider;
                _hitAny = true;
                break;
            }
            if (found is Sample3D or OrderSheet or Apparatus3D or Customer3D or Pressable || (found != null && (found == (Object)_game.stamp || found == (Object)_game.dispatchBox))) return found;
            // a near miss on the counter (the table round a sheet, the edge of a sample): the closest sheet or sample
            // within a few centimetres of the line of sight
            foreach (var hit in Physics.SphereCastAll(ray, 0.035f, _game.reach, ~0, QueryTriggerInteraction.Collide))
            {
                if (hit.collider.transform.IsChildOf(walker)) continue;
                var o = What(hit.collider);
                if (o is Sample3D or OrderSheet && (found == null || found == (Object)_game.crate)) { found = o; _aimedAt += " → " + hit.collider.name; break; }
            }
            return found;
        }

        /// What a collider is to the hands: a sample on the counter, a sheet, an apparatus, the counter, the door.
        Object What(Collider c)
        {
            var g = _game;
            if (g.awards != null && g.awards.Holding && AwardWall.IsWall(c)) return c;   // a certificate in hand: a wall to hang it on
            var s = c.GetComponentInParent<Sample3D>();
            if (s != null && g.Line.AnyIncoming && g.sheets.Exists(x => x.sample == s && g.Line.Incoming(x))) return null;
            if (s != null && s.At == Sample3D.Where.Apparatus) return g.apparatus.Find(a => a.Load == s);   // a sample on an apparatus: the apparatus
            if (s != null) return s.At is Sample3D.Where.Crate or Sample3D.Where.Free ? s : null;
            if (g.stampPad != null && c.transform == g.stampPad) return g.stamp;   // the pad: where the stamp goes back
            var sheet = c.GetComponentInParent<OrderSheet>();
            if (sheet != null && g.Line.Incoming(sheet)) return null;
            if (sheet != null && !sheet.sent && sheet != Hands.Reading && sheet != g.Desk.BoxOrder) return sheet;
            var key = c.GetComponentInParent<CalcKey>();
            if (key != null) return key;
            var press = c.GetComponentInParent<Pressable>();
            if (press != null && press.enabled) return press;
            var cu = c.GetComponentInParent<Customer3D>();
            if (cu != null) return cu.AtWindow || (g.Desk.BoxInHand && cu.Waiting) ? cu : null;
            var a = c.GetComponentInParent<Apparatus3D>();
            if (a != null) return a;
            if (g.stamp != null && c.transform.IsChildOf(g.stamp)) return g.stamp;
            if (g.dispatchBox != null && c.transform.IsChildOf(g.dispatchBox)) return g.dispatchBox;
            if (c.transform.IsChildOf(g.crate)) return g.crate;
            if (g.door != null && c.transform.IsChildOf(g.door)) return g.door;
            return null;
        }

        // ------------------------------------------------------------------ the light under the dot

        Renderer[] Parts(Object o)
        {
            if (_parts.TryGetValue(o, out var r)) return r;
            var all = new List<Renderer>();
            foreach (var x in ((Component)o).GetComponentsInChildren<Renderer>(true))
                if (o is Sample3D || x.GetComponentInParent<Sample3D>() == null) all.Add(x);   // a thing without the samples on it
            return _parts[o] = all.ToArray();
        }

        /// Under the dot a thing is lit up; a new apparatus pulses until it has been worked once, the counter while a
        /// done order is in hand, the door in the evening (every frame, from the game).
        public void Glow(Object target)
        {
            var g = _game;
            var held = Hands.Held;
            if (target != null && Hands.Hint(target).Length == 0) target = null;   // nothing to do with it now: not lit up
            if (g.awards != null && g.awards.Holding) target = null;
            float pulse = 0.3f + 0.3f * Mathf.Sin(Time.time * 4);
            foreach (var a in g.apparatus)
                if (a.Active) Set(Parts(a), a == (Object)target ? 1 : a.Tried || held == null ? 0 : pulse);
            if (g.door != null) Set(Parts(g.door), target == (Object)g.door ? 1 : g.Book.AllSent ? pulse : g.day.Evening ? pulse * 0.5f : 0);
            foreach (var sh in g.sheets) if (sh.gameObject.activeSelf) Set(Parts(sh), sh == (Object)target ? 1 : held == sh.sample ? pulse : 0);
            if (g.stamp != null) Set(Parts(g.stamp), g.stamp == (Object)target ? 1 : 0);
            if (g.dispatchBox != null) Set(Parts(g.dispatchBox), g.dispatchBox == (Object)target ? 1 : g.Desk.BoxInHand ? 0 : g.Desk.BoxCalls ? pulse * 0.6f : 0);
            foreach (var s in g.samples) if (s.gameObject.activeSelf) Set(Parts(s), s == (Object)target ? 1 : 0);
            if (_litPress != null && _litPress != (Object)target) { Set(Parts(_litPress), 0); _litPress = null; }
            if (target is Pressable pp) { Set(Parts(pp), 1); _litPress = pp; }
        }

        /// No light on it (a sheet taken into the hand: the light is strokes of chalk over its writing).
        public void Unlit(Object o) => Set(Parts(o), 0);

        void Set(Renderer[] rs, float v)
        {
            foreach (var r in rs)
            {
                if (_glow.TryGetValue(r, out var was) && Mathf.Abs(was - v) < 0.01f) continue;
                _glow[r] = v;
                Art3D.Prop(r, "_Glow", v);
            }
        }
    }
}
