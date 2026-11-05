using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// The lab's certificates (DESIGN.md 2б, «Грамоты»): each achievement earned brings one — «ГРАМОТА», what for, a picture
    /// in a laurel wreath, the day, a seal and a signature — and it hangs on a free bit of wall. A click takes it off the
    /// wall into both hands; a click on a free bit of wall hangs it there (a chalk frame shows where it would go: green —
    /// it fits, red — a table, a window, the door or another certificate is in the way); the right button puts it back
    /// where it was. Where each hangs is kept in the save, from day to day; on a temporary wall it rides along as the lab
    /// grows, and when that wall is gone it finds itself another place.
    public class AwardWall : MonoBehaviour
    {
        Lab3DGame _game;
        readonly List<Certificate> _certs = new List<Certificate>();
        Certificate _held;
        Transform _ghost;
        Renderer[] _ghostBars;
        readonly Transform[] _hands = new Transform[2];
        float _seenFor;
        Vector3 _crossWas, _sideWas;
        float _wallsStill;
        bool _wallsMoved;

        /// The certificate with its frame, half its width and height.
        public static readonly Vector2 Half = new Vector2(0.172f, 0.222f);
        const float Depth = 0.007f;   // from the wall's face to the certificate's middle

        public bool Holding => _held != null;
        public bool CanTake => _held == null && _game != null && _game.Hands.Empty;
        public IReadOnlyList<Certificate> All => _certs;
        GameState G => _game.G;

        public void Init(Lab3DGame game)
        {
            _game = game;
            StartCoroutine(HangAll());
        }

        /// The morning: every certificate to its place on the walls (once the departments built at runtime are standing);
        /// new ones — earned at the end of yesterday or by this morning's purchases — find a place and are told on a card.
        IEnumerator HangAll()
        {
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            foreach (var a in G.Career.awards)
            {
                var c = Make(a);
                if (a.placed && Put(c, OnWallNow(a, new Vector3(a.x, a.y, a.z)), Quaternion.Euler(0, a.yaw, 0) * Vector3.forward)) continue;
                if (a.placed) Debug.Log($"[LAB3D] грамота {a.id} утром не на месте: {Why}");
                if (!a.placed) Tell(a);
                AutoPlace(c);
            }
            G.Save();
        }

        /// New achievements in the middle of the day: their certificates come up on a free wall, at once.
        public void Earned(List<AwardData> got)
        {
            if (got == null) return;
            foreach (var a in got)
            {
                var c = Make(a);
                AutoPlace(c);
                if (c.gameObject.activeSelf) StartCoroutine(Appear(c));
                Tell(a);
            }
            if (got.Count > 0) G.Save();
        }

        void Tell(AwardData a)
        {
            var d = Awards.Get(a.id);
            _game.Hud.Tell(("Грамота «" + d.title + "»", "", d.why + " — висит на стене"));
        }

        IEnumerator Appear(Certificate c)
        {
            var t = c.transform;
            Sfx.Play("correct", 0.7f, 1.1f);
            Chalk3DText.Pop(t.position + t.forward * 0.15f + Vector3.up * 0.3f, "грамота!", ChalkTex.Yellow, 0.07f, _game.eye, 2.2f);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.5f)
            {
                float s = k < 0.7f ? Mathf.SmoothStep(0, 1.08f, k / 0.7f) : Mathf.Lerp(1.08f, 1, (k - 0.7f) / 0.3f);
                t.localScale = Vector3.one * s;
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------ the certificate itself

        Certificate Make(AwardData a)
        {
            GameObject go;
            if (_game.certTemplate != null)
            {
                go = Instantiate(_game.certTemplate.gameObject, _game.certTemplate.parent);
                go.SetActive(true);
            }
            else
            {   // no template in the scene (built before certificates were): a plain board of the kit's
                go = new GameObject("Certificate");
                Art3D.Part(PrimitiveType.Cube, _game.kit.brown, go.transform, Vector3.zero, new Vector3(0.344f, 0.444f, 0.012f));
                go.AddComponent<BoxCollider>().size = new Vector3(0.344f, 0.444f, 0.025f);
            }
            go.name = "Certificate " + a.id;
            var c = go.AddComponent<Certificate>();
            c.wall = this;
            c.data = a;
            Write(c);
            _certs.Add(c);
            return c;
        }

        /// The writing and the picture, in chalk on the paper (a canvas 600 × 800, half a millimetre a unit).
        void Write(Certificate c)
        {
            var d = Awards.Get(c.data.id);
            var rt = Art3D.Upright(c.transform, new Vector3(0, 0, 0.0036f), new Vector2(600, 800));
            rt.localScale = Vector3.one * 0.0005f;
            var s = UIF.Shape(rt, 40 + c.data.id.GetHashCode() % 50, "Drawing");
            s.Rect(Vector2.zero, new Vector2(548, 748), new Color(1f, 0.84f, 0.32f, 0.85f), 5);
            s.Rect(Vector2.zero, new Vector2(516, 716), ChalkTex.Dim, 2.5f);
            UIF.Text(rt, "ГРАМОТА", 92, ChalkTex.Yellow, new Vector2(0, 300), new Vector2(540, 120));
            var title = UIF.Text(rt, d.title, 54, ChalkTex.White, new Vector2(0, 205), new Vector2(500, 80));
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 30;
            title.resizeTextMaxSize = Mathf.RoundToInt(54 * ChalkTex.FontScale);
            title.verticalOverflow = VerticalWrapMode.Truncate;
            // the picture in a laurel wreath
            var mid = new Vector2(0, 25);
            Wreath(s, mid, 118);
            Lab.DrawIcon(s, d.icon, ChalkTex.White, 3.3f, mid + new Vector2(0, 6));
            if (c.data.id == "acad") Lab.DrawIcon(s, "star", ChalkTex.Yellow, 1.2f, mid + new Vector2(0, 92));
            UIF.Text(rt, d.why, 32, ChalkTex.White, new Vector2(0, -170), new Vector2(480, 90));
            // the day, the seal, the signature
            UIF.Text(rt, "день " + c.data.day, 28, ChalkTex.Dim, new Vector2(-190, -232), new Vector2(160, 40));
            var seal = new Vector2(-185, -300);
            s.Circle(seal, 50, ChalkTex.Red, 3.5f);
            s.Circle(seal, 38, new Color(1f, 0.45f, 0.45f, 0.6f), 2f);
            Lab.DrawIcon(s, "star", ChalkTex.Red, 1.4f, seal);
            Signature(s, new Vector2(120, -262), c.data.id.GetHashCode());
            s.Line(new Vector2(-10, -292), new Vector2(250, -292), ChalkTex.Dim, 2.5f);
            var who = UIF.Text(rt, c.data.signer ?? d.signer, 26, ChalkTex.Dim, new Vector2(120, -322), new Vector2(330, 40));
            who.resizeTextForBestFit = true;
            who.resizeTextMinSize = 16;
            who.resizeTextMaxSize = Mathf.RoundToInt(26 * ChalkTex.FontScale);
        }

        /// Two branches of laurel round the picture, tied with a ribbon at the bottom.
        static void Wreath(ChalkShape s, Vector2 c, float r)
        {
            var leaf = new Color(0.55f, 1f, 0.6f, 0.9f);
            for (int side = -1; side <= 1; side += 2)
            {
                float a0 = -Mathf.PI * 0.42f, a1 = Mathf.PI * 0.38f;
                var stem = new List<Vector2>();
                for (int i = 0; i <= 16; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / 16f);
                    stem.Add(c + new Vector2(side * Mathf.Cos(a) * r, Mathf.Sin(a) * r));
                }
                s.Poly(stem, leaf, 3f, false, false);
                for (int i = 1; i <= 9; i++)
                {
                    float a = Mathf.Lerp(a0, a1, i / 10f);
                    var p = c + new Vector2(side * Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                    var along = new Vector2(-side * Mathf.Sin(a), Mathf.Cos(a));   // up the branch
                    var outward = new Vector2(side * Mathf.Cos(a), Mathf.Sin(a));
                    foreach (float o in new[] { 1f, -0.8f })
                    {
                        var tip = p + along * 20 + outward * 16 * o;
                        s.Poly(new[] { p, p + along * 8 + outward * 9 * o + along * 2, tip, p + along * 9 + outward * 2 * o }, leaf, 2.2f, true, false);
                    }
                }
            }
            var knot = c + new Vector2(0, -r * 0.97f);
            s.Circle(knot, 7, ChalkTex.Red, 2.5f);
            s.Line(knot, knot + new Vector2(-24, -30), ChalkTex.Red, 3f);
            s.Line(knot, knot + new Vector2(22, -32), ChalkTex.Red, 3f);
        }

        /// A signature: a quick looped scribble, a little different on every certificate.
        static void Signature(ChalkShape s, Vector2 at, int seed)
        {
            var rnd = new System.Random(seed);
            var pts = new List<Vector2>();
            float x = -100;
            for (int i = 0; i <= 60; i++)
            {
                float t = i / 60f;
                x = -100 + t * 210;
                float loop = Mathf.Sin(t * Mathf.PI * (5 + (float)rnd.NextDouble() * 0.3f));
                float y = loop * (14 + 10 * (1 - t)) + (t < 0.2f ? (0.2f - t) * 120 : 0);
                pts.Add(at + new Vector2(x + Mathf.Cos(t * Mathf.PI * 10) * 9, y));
            }
            s.Poly(pts, ChalkTex.White, 3f, false, false);
            s.Line(at + new Vector2(60, -14), at + new Vector2(118, 6), ChalkTex.White, 2.5f);   // the flourish
        }

        // ------------------------------------------------------------------ where it can hang

        /// Why the last Fits said no (for the check's log).
        public static string Why = "";

        public static bool IsWall(Collider c) => c != null && c.name is "Walls" or "Partition" or "PartitionSide";

        bool Mine(Collider c, Certificate self) =>
            (self != null && c.transform.IsChildOf(self.transform)) || c.transform.IsChildOf(_game.walker.transform) || (_ghost != null && c.transform.IsChildOf(_ghost));

        /// The first thing behind `from` along −n is a wall (not a table, a window, the door's leaf…): where its face is.
        string _blocker = "";

        bool WallBehind(Vector3 from, Vector3 n, Certificate self, out Vector3 at, out Collider col)
        {
            _blocker = "ничего";
            at = default;
            col = null;
            var hits = Physics.RaycastAll(from, -n, 0.5f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (Mine(h.collider, self)) continue;
                if (!IsWall(h.collider) || Vector3.Dot(h.normal, n) < 0.9f) { _blocker = h.collider.name; return false; }
                at = h.point;
                col = h.collider;
                return true;
            }
            return false;
        }

        /// The lab as it is now: inside the temporary walls.
        bool Inside(Vector3 p)
        {
            var cross = _game.partition;
            var side = _game.partitionSide;
            float minZ = cross != null && cross.gameObject.activeInHierarchy ? cross.position.z + cross.lossyScale.z / 2 : -9;
            float minX = side != null && side.gameObject.activeInHierarchy ? side.position.x + side.lossyScale.x / 2 : -9;
            return p.z >= minZ - 0.03f && p.x >= minX - 0.03f && p.x <= LabRoom.RightWall + 0.08f && p.z <= LabRoom.BackWall + 0.08f;
        }

        /// Can a certificate hang with its middle near `point` on the wall whose face looks along `normal`? Where exactly,
        /// turned how, and on which wall (a temporary one it rides on).
        public bool Fits(Vector3 point, Vector3 normal, Certificate self, out Vector3 at, out Quaternion rot, out string wallName)
        {
            Why = "";
            at = point;
            rot = Quaternion.identity;
            wallName = "";
            var n = new Vector3(normal.x, 0, normal.z);
            if (Mathf.Abs(normal.y) > 0.3f || n.sqrMagnitude < 0.01f) return false;
            // the walls stand along the room's axes
            n = Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector3(Mathf.Sign(n.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(n.z));
            rot = Quaternion.LookRotation(n, Vector3.up);
            point.y = Mathf.Clamp(point.y, 0.7f, 2.6f);
            at = point + n * Depth;
            if (!WallBehind(point + n * 0.25f, n, self, out var face, out var col)) { Why = "нет стены за серединой: " + _blocker; return false; }
            at = face + n * Depth;
            wallName = col.name is "Partition" or "PartitionSide" ? col.name : "";
            var right = rot * Vector3.right;
            for (int i = 0; i < 4; i++)
            {   // the whole frame on the same flat wall: not over a window's hole, the door, a corner
                var corner = face + right * (Half.x * (i % 2 == 0 ? -1 : 1)) + Vector3.up * (Half.y * (i < 2 ? -1 : 1));
                if (!WallBehind(corner + n * 0.25f, n, self, out var f2, out _) || Mathf.Abs(Vector3.Dot(f2 - face, n)) > 0.02f) { Why = "угол не на стене"; return false; }
            }
            // nothing in front of it, up to 40 cm out: a table, an apparatus (not hidden behind one), a window, the switch,
            // another certificate
            foreach (var c in Physics.OverlapBox(at + n * 0.21f, new Vector3(Half.x, Half.y, 0.2f), rot, ~0, QueryTriggerInteraction.Collide))
                if (!IsWall(c) && !Mine(c, self)) { Why = "мешает " + c.name; return false; }
            if (!Inside(at)) { Why = "за временной стеной"; return false; }
            return true;
        }

        /// Hangs it at `point` if it fits there.
        bool Put(Certificate c, Vector3 point, Vector3 normal)
        {
            if (!Fits(point, normal, c, out var at, out var rot, out var wallName)) return false;
            Hang(c, at, rot, wallName);
            return true;
        }

        void Hang(Certificate c, Vector3 at, Quaternion rot, string wallName)
        {
            var t = c.transform;
            t.SetParent(_game.certTemplate != null ? _game.certTemplate.parent : null, true);
            t.SetPositionAndRotation(at, rot);
            t.localScale = Vector3.one;
            c.gameObject.SetActive(true);
            foreach (var col in c.GetComponentsInChildren<Collider>()) col.enabled = true;
            var a = c.data;
            a.placed = true;
            a.x = at.x; a.y = at.y; a.z = at.z;
            a.yaw = rot.eulerAngles.y;
            a.wall = wallName;
            Physics.SyncTransforms();
        }

        /// A free place on the walls of the lab as it is now: the middle row at eye height first, nearest the closet's
        /// middle first; then a row higher, then lower.
        public void AutoPlace(Certificate c)
        {
            var cands = new List<(Vector3 p, Vector3 n, float score)>();
            var home = new Vector3(1.2f, 1.7f, 2.2f);
            var cross = _game.partition;
            var side = _game.partitionSide;
            bool crossOn = cross != null && cross.gameObject.activeInHierarchy, sideOn = side != null && side.gameObject.activeInHierarchy;
            float minZ = crossOn ? cross.position.z + cross.lossyScale.z / 2 : -3.6f;
            float minX = sideOn ? side.position.x + side.lossyScale.x / 2 : -3.15f;
            float[] rows = { 1.78f, 2.25f, 1.38f };
            for (int r = 0; r < rows.Length; r++)
            {
                float y = rows[r], penalty = r * 1.2f;
                void Add(Vector3 p, Vector3 n) => cands.Add((p, n, Vector3.Distance(p, home) + penalty));
                for (float z = 3.3f; z >= -3.4f; z -= 0.2f) Add(new Vector3(LabRoom.RightWall - 0.05f, y, z), Vector3.left);
                for (float x = 2.8f; x >= -3.0f; x -= 0.2f) Add(new Vector3(x, y, LabRoom.BackWall - 0.05f), Vector3.back);
                for (float x = 2.8f; x >= minX + 0.2f; x -= 0.2f) Add(new Vector3(x, y, minZ + 0.05f), Vector3.forward);
                for (float z = 3.3f; z >= minZ + 0.2f; z -= 0.2f) Add(new Vector3(minX + 0.05f, y, z), Vector3.right);
            }
            cands.Sort((a, b) => a.score.CompareTo(b.score));
            c.gameObject.SetActive(true);
            foreach (var col in c.GetComponentsInChildren<Collider>()) col.enabled = false;   // not in its own way
            foreach (var (p, n, _) in cands)
                if (Put(c, p, n)) return;
            c.data.placed = false;   // no free wall today: it waits in the drawer till tomorrow
            c.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ in the hands

        public void Take(Certificate c)
        {
            if (!CanTake || c == null) return;
            _held = c;
            c.data.seen = true;
            Glow(c, 0);
            foreach (var col in c.GetComponentsInChildren<Collider>()) col.enabled = false;
            MakeHands(c);
            StartCoroutine(Fly(c.transform, true, null));
            Sfx.Play("paper", 0.7f, 0.8f);
        }

        /// Where it is carried: low in front, in both hands, its face to the eyes.
        void Carried(out Vector3 p, out Quaternion r)
        {
            var e = _game.eye.transform;
            p = e.TransformPoint(new Vector3(0, -0.23f, 0.46f));
            r = e.rotation * Quaternion.Euler(-14, 180, 0);
        }

        IEnumerator Fly(Transform t, bool toHands, System.Action done, Vector3 to = default, Quaternion toRot = default)
        {
            t.SetParent(null, true);
            var from = t.position;
            var fromRot = t.rotation;
            var fromScale = t.localScale.x;
            for (float k = 0; k < 1; k += Time.deltaTime / 0.3f)
            {
                float e = k * k * (3 - 2 * k);
                Vector3 p; Quaternion r;
                if (toHands) Carried(out p, out r); else { p = to; r = toRot; }
                t.position = Vector3.Lerp(from, p, e) + (toHands ? Vector3.zero : Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.04f);
                t.rotation = Quaternion.Slerp(fromRot, r, e);
                t.localScale = Vector3.one * Mathf.Lerp(fromScale, toHands ? 0.75f : 1f, e);
                if (toHands && _held == null) yield break;
                yield return null;
            }
            if (toHands && _held != null && _held.transform == t)
            {
                t.SetParent(_game.eye.transform, true);
                t.localPosition = new Vector3(0, -0.23f, 0.46f);
                t.localRotation = Quaternion.Euler(-14, 180, 0);
                t.localScale = Vector3.one * 0.75f;
            }
            done?.Invoke();
        }

        void MakeHands(Certificate c)
        {
            var kit = _game.kit;
            if (kit == null || kit.skin == null) return;
            for (int i = 0; i < 2; i++)
            {
                if (_hands[i] != null) Destroy(_hands[i].gameObject);
                float sx = i == 0 ? -1 : 1;
                var h = new GameObject("CertHand").transform;
                h.SetParent(c.transform, false);
                var m = ModelLibrary.Spawn("Body_hand_cert", h, kit);   // a hand at its side (Tools/Blender/body.py); the left one mirrored
                if (m != null) m.transform.localScale = new Vector3(sx, 1, 1);
                foreach (var x in h.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Lab3DGame.ViewLayer;
                _hands[i] = h;
            }
        }

        void DropHands()
        {
            for (int i = 0; i < 2; i++) if (_hands[i] != null) { _hands[i].gameObject.SetActive(false); Destroy(_hands[i].gameObject); _hands[i] = null; }
        }

        /// The hint under the dot while a certificate is carried.
        public string HintFor(Object target)
        {
            if (target is Collider wall && IsWall(wall))
                return Fits(_game.Pointer.HitPoint, _game.Pointer.HitNormal, _held, out _, out _, out _) ? "повесить грамоту" : "здесь не повесить\nПКМ — вернуть на место";
            return "наведи на свободную стену\nПКМ — вернуть на место";
        }

        /// A click while a certificate is carried: on a free bit of wall it is hung there.
        public void Click(Object target)
        {
            if (_held == null) return;
            if (target is Collider wall && IsWall(wall) && Fits(_game.Pointer.HitPoint, _game.Pointer.HitNormal, _held, out var at, out var rot, out var wallName))
            {
                HangHeld(at, rot, wallName);
                return;
            }
            Sfx.Play("nope", 0.5f);
            _game.Hud.Tell(("Здесь не повесить", "", "нужна свободная стена: без столов, окон и других грамот"), true);
        }

        void HangHeld(Vector3 at, Quaternion rot, string wallName)
        {
            var c = _held;
            _held = null;
            DropHands();
            HideGhost();
            StartCoroutine(Fly(c.transform, false, () =>
            {
                Hang(c, at, rot, wallName);
                Sfx.Play("thud", 0.45f, 1.4f);
                G.Save();
            }, at, rot));
            Sfx.Play("paper", 0.5f, 1.1f);
        }

        /// The right button, or the day ending, while one is carried: back where it hung.
        public void PutBack()
        {
            var c = _held;
            if (c == null) return;
            var a = c.data;
            if (Fits(new Vector3(a.x, a.y, a.z), Quaternion.Euler(0, a.yaw, 0) * Vector3.forward, c, out var at, out var rot, out var wallName))
                HangHeld(at, rot, wallName);
            else
            {
                _held = null;
                DropHands();
                HideGhost();
                c.transform.SetParent(null, true);
                AutoPlace(c);
                G.Save();
            }
        }

        /// For the check: hang the carried one at a point of a wall, at once. True if it fits there.
        public bool HangNow(Vector3 point, Vector3 normal)
        {
            var c = _held;
            if (c == null || !Fits(point, normal, c, out var at, out var rot, out var wallName)) return false;
            _held = null;
            DropHands();
            HideGhost();
            StopAllCoroutines();
            Hang(c, at, rot, wallName);
            G.Save();
            return true;
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (_game == null) return;
            Ghost();
            Ride();
            Fresh();
        }

        /// While one is carried: a chalk frame on the wall under the dot, where it would hang — green if it fits.
        void Ghost()
        {
            if (_held == null || !_game.Pointer.HitAny || !IsWall(_game.Pointer.HitCollider)) { HideGhost(); return; }
            bool ok = Fits(_game.Pointer.HitPoint, _game.Pointer.HitNormal, _held, out var at, out var rot, out _);
            if (Mathf.Abs(_game.Pointer.HitNormal.y) > 0.3f) { HideGhost(); return; }
            if (_ghost == null)
            {
                _ghost = new GameObject("CertGhost").transform;
                _ghostBars = new Renderer[4];
                for (int i = 0; i < 4; i++)
                {
                    bool across = i < 2;
                    var bar = Art3D.Part(PrimitiveType.Cube, _game.kit.green, _ghost,
                        across ? new Vector3(0, Half.y * (i == 0 ? -1 : 1), 0) : new Vector3(Half.x * (i == 2 ? -1 : 1), 0, 0),
                        across ? new Vector3(Half.x * 2 + 0.012f, 0.012f, 0.004f) : new Vector3(0.012f, Half.y * 2 + 0.012f, 0.004f));
                    _ghostBars[i] = bar.GetComponent<Renderer>();
                }
            }
            _ghost.gameObject.SetActive(true);
            _ghost.SetPositionAndRotation(at, rot);
            var m = ok ? _game.kit.green : _game.kit.red;
            foreach (var r in _ghostBars) if (r.sharedMaterial != m) r.sharedMaterial = m;
        }

        void HideGhost() { if (_ghost != null) _ghost.gameObject.SetActive(false); }

        /// The certificates on a temporary wall go with it as it slides back; once it has stopped (or is gone), each one
        /// that no longer fits finds another place.
        void Ride()
        {
            var cross = _game.partition;
            var side = _game.partitionSide;
            if (cross == null || side == null) return;
            bool moved = (cross.position - _crossWas).sqrMagnitude > 1e-8f || (side.position - _sideWas).sqrMagnitude > 1e-8f;
            _crossWas = cross.position;
            _sideWas = side.position;
            foreach (var c in _certs)
            {
                if (c == _held || !c.data.placed || !c.gameObject.activeSelf || c.data.wall.Length == 0) continue;
                c.transform.position = OnWallNow(c.data, c.transform.position) + c.transform.forward * Depth;
            }
            if (moved)
            {
                _wallsMoved = true;
                _wallsStill = 0;
                return;
            }
            if (!_wallsMoved) return;
            _wallsStill += Time.deltaTime;
            if (_wallsStill < 0.4f) return;
            _wallsMoved = false;
            Physics.SyncTransforms();
            foreach (var c in _certs)
            {
                if (c == _held || !c.gameObject.activeSelf) continue;
                var a = c.data;
                if (a.wall.Length == 0) continue;
                var p = c.transform.position;
                if (!Put(c, p, c.transform.forward)) { Debug.Log($"[LAB3D] грамота {a.id} со стены {a.wall}: {Why}"); AutoPlace(c); }
            }
            G.Save();
        }

        /// A point of a certificate on a temporary wall, moved onto that wall's face where the wall stands now.
        Vector3 OnWallNow(AwardData a, Vector3 p)
        {
            var cross = _game.partition;
            var side = _game.partitionSide;
            if (a.wall == "Partition" && cross != null) p.z = cross.position.z + cross.lossyScale.z / 2;
            else if (a.wall == "PartitionSide" && side != null) p.x = side.position.x + side.lossyScale.x / 2;
            return p;
        }

        /// A new certificate glows softly until it has been looked at.
        void Fresh()
        {
            float pulse = 0.25f + 0.25f * Mathf.Sin(Time.time * 4);
            foreach (var c in _certs)
            {
                if (c.data.seen || c == _held) continue;
                if (_game.Pointer.Target == (Object)c)
                {
                    _seenFor += Time.deltaTime;
                    if (_seenFor > 0.6f) { c.data.seen = true; _seenFor = 0; }
                    continue;   // lit up by the game: it is under the dot
                }
                Glow(c, pulse);
            }
        }

        static void Glow(Certificate c, float v)
        {
            foreach (var r in c.GetComponentsInChildren<Renderer>()) Art3D.Prop(r, "_Glow", v);
        }
    }
}
