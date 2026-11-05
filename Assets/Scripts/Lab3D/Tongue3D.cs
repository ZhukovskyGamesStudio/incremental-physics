using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The big tongue (DESIGN.md 2б, «Крейзи-измерения»): a pink tongue on a board, its five tastes written round it in
    /// chalk — sweet at the tip, salty and sour at the sides, umami in the middle, bitter at the back. The sample is put on
    /// one of them; a moment later the tongue reacts if the sample has that taste — and each taste its own way (a happy
    /// wiggle, a curl of the tip, a pucker, a recoil, a slow swell) — or lies still. Put it on another place, see again;
    /// tick on the sheet the tastes it reacted to.
    public class Tongue3D : OddApparatus
    {
        public static readonly string[] Zones = { "сладкое", "солёное", "кислое", "горькое", "умами" };
        // on the tongue's top, its tip to the front (+z); salty on the left of whoever faces it, sour on the right
        static readonly Vector3[] ZoneAt =
        {
            new Vector3(0, 0, 0.19f), new Vector3(0.105f, 0, 0.04f), new Vector3(-0.105f, 0, 0.04f), new Vector3(0, 0, -0.2f), new Vector3(0, 0, -0.07f),
        };
        /// Where each taste's card stands on the board, beside its place (the sweet one in front of the tip, the bitter one
        /// behind the root, the others at the sides), and the colour its place and card share.
        static readonly Vector3[] CardAt =
        {
            new Vector3(0, 0.02f, 0.31f), new Vector3(0.27f, 0.02f, 0.07f), new Vector3(-0.27f, 0.02f, 0.07f), new Vector3(0, 0.02f, -0.33f), new Vector3(-0.27f, 0.02f, -0.17f),
        };
        static readonly Color[] Ink =
        {
            ChalkTex.Yellow, ChalkTex.Cyan, ChalkTex.Green, new Color(0.8f, 0.62f, 1f), new Color(1f, 0.64f, 0.32f),
        };
        const float Top = 0.072f;   // the tongue's top over the table

        Transform _body;
        readonly Transform[] _seats = new Transform[5];
        Collider _grip;
        LineRenderer _ring;
        readonly Transform[] _patches = new Transform[5];
        readonly Vector3[] _patchSize = new Vector3[5];
        readonly Transform[] _cards = new Transform[5];
        int _zone = -1;
        float _t = -1;          // since the sample came onto its place
        float _react = -1;      // the reaction going on, since it began
        int _kind;
        /// The bot's hands have no dot: the place it means to put the sample on.
        [System.NonSerialized] public int wanted = -1;
        /// The tongue has had its taste of the sample on its place: did it react?
        public bool Done => Load != null && _t > 0.5f && _react < 0;
        public bool Reacted { get; private set; }
        public int Zone => _zone;

        protected override Transform Seat => _seats[_zone = Mathf.Max(0, Aimed())];
        public override bool HasAction => Load != null;
        public override string PutVerb { get { int z = Aimed(); return "положить: " + (z >= 0 ? Zones[z] : "на язык"); } }
        public override string Verb { get { int z = Aimed(); return z >= 0 && z != _zone ? "переложить: " + Zones[z] : "попробовать ещё"; } }
        public override bool Busy => _react >= 0;

        /// Which place the dot is on (or the bot's), -1 for none: the place nearest to where the dot is on the tongue (or
        /// on the sample lying on it).
        int Aimed()
        {
            if (!OnMe()) return wanted;
            var p = _body.InverseTransformPoint(PointedAt);
            int best = 0;
            float d = float.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                var s = _seats[i].localPosition;
                float dd = (p.x - s.x) * (p.x - s.x) + (p.z - s.z) * (p.z - s.z);
                if (dd < d) { d = dd; best = i; }
            }
            return best;
        }

        bool OnMe() => Pointed != null && (Pointed == _grip || (Load != null && Pointed.transform.IsChildOf(Load.transform)));

        /// The tongue's top over its body's origin at (x, z): the higher of its two humps (the long front one, the root).
        static float SurfaceY(float x, float z)
        {
            float H(Vector3 c, Vector3 r)
            {
                float u = (x - c.x) / r.x, w = (z - c.z) / r.z;
                float q = 1 - u * u - w * w;
                return q <= 0 ? -1 : c.y + r.y * Mathf.Sqrt(q);
            }
            return Mathf.Max(H(new Vector3(0, 0, 0.02f), new Vector3(0.15f, 0.05f, 0.25f)), H(new Vector3(0, 0.005f, -0.17f), new Vector3(0.16f, 0.055f, 0.12f)));
        }

        protected override Material Needs(PersonaKit kit) => kit.pink;

        protected override void Build(PersonaKit kit)
        {
            // a wooden board under it, wide enough for the cards round it
            Art3D.Part(PrimitiveType.Cube, kit.brown, transform, new Vector3(0, 0.01f, -0.01f), new Vector3(0.72f, 0.02f, 0.7f));
            _body = Art3D.Empty("Tongue", transform, new Vector3(0, 0.02f, 0));
            Art3D.Part(PrimitiveType.Sphere, kit.pink, _body, new Vector3(0, 0.0f, 0.02f), new Vector3(0.3f, 0.1f, 0.5f));
            Art3D.Part(PrimitiveType.Sphere, kit.pink, _body, new Vector3(0, 0.005f, -0.17f), new Vector3(0.32f, 0.11f, 0.24f));
            var paint = new[] { kit.yellow, kit.blue, kit.green, kit.purple, kit.orange };
            for (int i = 0; i < 5; i++)
            {
                var p = ZoneAt[i];
                float y = SurfaceY(p.x, p.z);
                _seats[i] = Art3D.Empty("Seat " + Zones[i], _body, new Vector3(p.x, y - 0.006f, p.z));
                // the place itself: a patch of its colour lying on the tongue, along its curve, outlined in chalk
                const float h = 0.004f;
                var n = new Vector3(-(SurfaceY(p.x + h, p.z) - SurfaceY(p.x - h, p.z)) / (2 * h), 1, -(SurfaceY(p.x, p.z + h) - SurfaceY(p.x, p.z - h)) / (2 * h)).normalized;
                _patchSize[i] = i == 0 ? new Vector3(0.075f, 0.012f, 0.062f) : new Vector3(0.088f, 0.012f, 0.088f);
                _patches[i] = Art3D.Part(PrimitiveType.Sphere, paint[i], _body, new Vector3(p.x, y + 0.0015f, p.z), _patchSize[i], null, "Place " + Zones[i]);
                _patches[i].localRotation = Quaternion.FromToRotation(Vector3.up, n);
                // its card: a small dark board standing on the wooden one, leaning back, the taste written in the same colour
                var stand = Art3D.Empty("Card " + Zones[i], transform, CardAt[i], new Vector3(-28, 0, 0));
                Art3D.Part(PrimitiveType.Cube, kit.dark, stand, new Vector3(0, 0.032f, 0), new Vector3(0.19f, 0.064f, 0.008f));
                Art3D.Part(PrimitiveType.Cube, paint[i], stand, new Vector3(0.077f, 0.032f, 0.0045f), new Vector3(0.018f, 0.018f, 0.002f));   // its colour, as a dot (on the reader's left)
                var c = Art3D.Upright(stand, new Vector3(-0.012f, 0.032f, 0.0045f), new Vector2(160, 64));
                Art3D.Word(c, Zones[i], Vector2.zero, 38, Ink[i], 160);
                _cards[i] = stand;
            }
            // the salty and sour cards stand right by their places; umami's, at the side, points at the middle with an arrow
            {
                var z = ZoneAt[4];
                var to = _body.localPosition + new Vector3(z.x - 0.035f, SurfaceY(z.x, z.z) + 0.03f, z.z - 0.03f);
                var from = CardAt[4] + new Vector3(0.1f, 0.07f, 0.01f);
                var l = Art3D.LocalLine("Arrow умами", transform, kit.line, 0.003f);
                var d = (to - from).normalized;
                var side = Vector3.Cross(d, Vector3.up).normalized * 0.018f;
                l.positionCount = 5;
                l.SetPositions(new[] { from, to, to - d * 0.025f + side, to, to - d * 0.025f - side });
            }
            // one grip over the whole tongue: the place is the one nearest the dot
            _grip = Art3D.Grip("TongueGrip", _body, new Vector3(0, 0.03f, -0.02f), new Vector3(0.36f, 0.08f, 0.62f));
            // the chalk ring round the place the dot is on
            _ring = Art3D.LocalLine("Ring", _body, kit.line, 0.006f);
            _ring.loop = true;
            _ring.positionCount = 24;
            for (int i = 0; i < 24; i++) { float a = i / 24f * Mathf.PI * 2; _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.06f, 0, Mathf.Sin(a) * 0.06f)); }
            _ring.enabled = false;
        }

        protected override void OnPut(Sample3D s) => Taste();

        protected override void OnTaken(Sample3D s)
        {
            _t = -1;
            _react = -1;
            Reacted = false;
            if (_body != null) { _body.localRotation = Quaternion.identity; _body.localScale = Vector3.one; _body.localPosition = new Vector3(0, 0.02f, 0); }
        }

        public override void Down()
        {
            if (Load == null || Busy) return;
            int z = Aimed();
            if (z >= 0 && z != _zone)
            {
                _zone = z;
                Load.MoveTo(_seats[z], Vector3.zero, Sample3D.Where.Apparatus);
                Sfx.Play("pickup", 0.4f, 1.1f);
            }
            Taste();
        }

        void Taste()
        {
            _t = 0;
            _react = -1;
            Reacted = false;
        }

        void Update()
        {
            if (_body == null) return;
            // the ring shows which place a click goes to
            bool show = OnMe() && game != null && (Load != null || game.Hands.Held != null) && !Busy;
            _ring.enabled = show;
            int lit = show ? Aimed() : -1;
            if (show) _ring.transform.localPosition = _seats[lit].localPosition + Vector3.up * 0.014f;
            // the place under the dot swells a little and its card leans out: which one the click goes to
            float pulse = 1.18f + 0.06f * Mathf.Sin(Time.time * 7);
            for (int i = 0; i < 5; i++)
            {
                _patches[i].localScale = Vector3.Scale(_patchSize[i], i == lit ? new Vector3(pulse, 1.4f, pulse) : Vector3.one);
                _cards[i].localScale = Vector3.one * (i == lit ? 1.18f : 1);
            }
            if (Load != null && _t >= 0)
            {
                float was = _t;
                _t += Time.deltaTime;
                if (was < 0.5f && _t >= 0.5f)
                {
                    Tried = true;
                    if (Load.data != null && ((Load.data.taste >> _zone) & 1) != 0) { Reacted = true; _react = 0; _kind = _zone; Sound(); }
                }
            }
            if (_react < 0) return;
            _react += Time.deltaTime;
            float k = _react / 1.4f, fade = 1 - k, s = Mathf.Sin(k * Mathf.PI);
            var pos = new Vector3(0, 0.02f, 0);
            var rot = Quaternion.identity;
            var scale = Vector3.one;
            switch (_kind)
            {
                case 0: rot = Quaternion.Euler(0, Mathf.Sin(_react * 18) * 9 * fade, Mathf.Sin(_react * 18) * 3 * fade); break;   // sweet: a happy wiggle
                case 1: rot = Quaternion.Euler(-Mathf.Abs(Mathf.Sin(_react * 7)) * 14 * fade, 0, 0); break;                       // salty: the tip curls, twice
                case 2: scale = new Vector3(1 - 0.2f * s, 1 + 0.1f * s, 1 - 0.08f * s); pos.x = Mathf.Sin(_react * 60) * 0.004f * s; break;   // sour: a pucker and a shiver
                case 3: pos.z = -0.06f * s; rot = Quaternion.Euler(0, 0, Mathf.Sin(_react * 40) * 7 * s); break;                 // bitter: it recoils
                case 4: scale = new Vector3(1 + 0.06f * s, 1 + 0.5f * s, 1 + 0.04f * Mathf.Sin(_react * 6)); break;            // umami: a slow swell
            }
            _body.localPosition = pos;
            _body.localRotation = rot;
            _body.localScale = scale;
            if (k >= 1) { _react = -1; _body.localPosition = new Vector3(0, 0.02f, 0); _body.localRotation = Quaternion.identity; _body.localScale = Vector3.one; }
        }

        void Sound()
        {
            switch (_kind)
            {
                case 0: Sfx.Play("lick", 0.7f, 1.15f); break;
                case 1: Sfx.Play("lick", 0.7f, 0.9f); break;
                case 2: Sfx.Play("sour", 0.6f); break;
                case 3: Sfx.Play("bleh", 0.6f); break;
                default: Sfx.Play("mmm", 0.6f); break;
            }
        }
    }
}
