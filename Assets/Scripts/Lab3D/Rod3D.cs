using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The pond (DESIGN.md 2б, «Крейзи-измерения»): a wooden tub of water with reeds, a rod on a post at its front, and a
    /// board behind it with the five fish that live in it, named. The sample is the bait on the hook. Click — the rod
    /// swings back and casts it into the water, the float bobs, an hourglass by the rod starts. If anything bites on
    /// such a bait, it does before the sand runs out: the float dips and the little bell on the rod's tip rings. Click
    /// again — the rod pulls it out: the fish hangs on the line a moment, then flops back into the water. Nobody bit by
    /// the time the sand ran out — nobody bites on it. Fish take their time (DESIGN.md 2б, «Пиццерия»): the sand runs a
    /// third of a minute, a bite may come at any moment of it, and a fish left on the line too long gets off the hook —
    /// it bites again a little later, if the sand has not run out.
    public class Rod3D : OddApparatus
    {
        public const float Sand = 20f;             // seconds the hourglass runs
        public const float Hold = 6f;              // seconds a biting fish stays on the hook before it gets off
        const float Water = 0.42f;                 // the water's level over the floor
        static readonly Vector3 CastAt = new Vector3(-0.05f, Water, -0.02f);
        const float RestTilt = 22, CastTilt = -48;

        enum State { Rest, Casting, Waiting, Reeling, Showing }
        State _state;
        Transform _pivot, _tip, _hook, _float, _sandTop, _sandBottom, _fishOnLine;
        LineRenderer _line;
        readonly List<Transform> _shadows = new List<Transform>();
        float _t, _tilt = RestTilt, _biteAt, _ring;
        bool _bit, _hadBite;
        Vector3 _hookFrom;
        /// What came out on the hook the last time: a fish (0..4), nobody (5), or -1 (pulled out before the sand ran out
        /// with nothing on it: nothing is known yet).
        public int Caught { get; private set; } = -1;
        public bool Biting => _state == State.Waiting && _bit;
        public bool SandOut => _state == State.Waiting && _t >= Sand;

        protected override Transform Seat => _hook;
        protected override Vector3 SeatFor(Sample3D s) => Vector3.down * (s.Size + 0.01f);
        public override string PutVerb => "насадить на крючок";
        public override bool HasAction => Load != null && _state is State.Rest or State.Waiting;
        public override string Verb => _state == State.Rest ? "закинуть" : _bit ? "тянуть!" : _t >= Sand ? (_hadBite ? "вытащить — сорвалась" : "вытащить") : "подсечь";
        public override bool Busy => _state is State.Casting or State.Reeling or State.Showing;

        protected override Material Needs(PersonaKit kit) => kit.water;

        protected override void Build(PersonaKit kit)
        {
            var wood = kit.brown;
            // the tub
            const float w = 1.5f, d = 0.72f, h = 0.5f, th = 0.05f;
            Art3D.Part(PrimitiveType.Cube, wood, transform, new Vector3(0, 0.03f, 0), new Vector3(w, 0.06f, d));
            Art3D.Part(PrimitiveType.Cube, wood, transform, new Vector3(0, h / 2, d / 2 - th / 2), new Vector3(w, h, th));
            Art3D.Part(PrimitiveType.Cube, wood, transform, new Vector3(0, h / 2, -d / 2 + th / 2), new Vector3(w, h, th));
            Art3D.Part(PrimitiveType.Cube, wood, transform, new Vector3(w / 2 - th / 2, h / 2, 0), new Vector3(th, h, d));
            Art3D.Part(PrimitiveType.Cube, wood, transform, new Vector3(-w / 2 + th / 2, h / 2, 0), new Vector3(th, h, d));
            foreach (float y in new[] { 0.12f, 0.38f })   // hoops round it
                Art3D.Part(PrimitiveType.Cube, kit.metal, transform, new Vector3(0, y, d / 2 + 0.003f), new Vector3(w + 0.01f, 0.025f, 0.01f));
            Art3D.Part(PrimitiveType.Cube, kit.dark, transform, new Vector3(0, 0.065f, 0), new Vector3(w - 2 * th, 0.01f, d - 2 * th));
            Art3D.Part(PrimitiveType.Cube, kit.water, transform, new Vector3(0, Water, 0.01f), new Vector3(w - 2 * th, 0.004f, d - 2 * th), null, "Water");
            // reeds at the back corners, lily pads on the water
            var r = new System.Random(9);
            foreach (float cx in new[] { -0.62f, 0.6f })
                for (int i = 0; i < 4; i++)
                {
                    float x = cx + (float)(r.NextDouble() - 0.5) * 0.12f, z = -0.24f + (float)r.NextDouble() * 0.08f, hh = 0.35f + (float)r.NextDouble() * 0.3f;
                    var reed = Art3D.Part(PrimitiveType.Cylinder, kit.green, transform, new Vector3(x, Water + hh / 2, z), new Vector3(0.012f, hh / 2, 0.012f), new Vector3((float)(r.NextDouble() - 0.5) * 12, 0, (float)(r.NextDouble() - 0.5) * 12));
                    if (i % 2 == 0) Art3D.Part(PrimitiveType.Capsule, kit.brown, reed, new Vector3(0, 0.85f, 0), new Vector3(2.4f, 0.12f, 2.4f));
                }
            foreach (var (x, z, s) in new[] { (0.3f, 0.12f, 0.14f), (-0.4f, -0.1f, 0.11f), (0.45f, -0.15f, 0.09f) })
                Art3D.Part(PrimitiveType.Cylinder, kit.green, transform, new Vector3(x, Water + 0.004f, z), new Vector3(s, 0.003f, s));
            // fish swimming under the water: dark shapes going round
            for (int i = 0; i < 3; i++)
                _shadows.Add(Art3D.Part(PrimitiveType.Sphere, kit.dark, transform, new Vector3(0, Water - 0.12f - i * 0.05f, 0), new Vector3(0.05f, 0.025f, 0.13f)));
            // the post, the rod on its pivot, the reel, the bell at its tip
            Art3D.Part(PrimitiveType.Cylinder, kit.metal, transform, new Vector3(0.62f, 0.46f, 0.42f), new Vector3(0.04f, 0.46f, 0.04f));
            _pivot = Art3D.Empty("RodPivot", transform, new Vector3(0.62f, 0.93f, 0.42f));
            Art3D.Part(PrimitiveType.Cylinder, kit.brown, _pivot, new Vector3(0, 0.65f, 0), new Vector3(0.024f, 0.65f, 0.024f));
            Art3D.Part(PrimitiveType.Cylinder, kit.dark, _pivot, new Vector3(0, -0.08f, 0), new Vector3(0.034f, 0.12f, 0.034f));
            Art3D.Part(PrimitiveType.Cylinder, kit.metal, _pivot, new Vector3(0.03f, 0.06f, 0), new Vector3(0.07f, 0.012f, 0.07f), new Vector3(0, 0, 90));
            _tip = Art3D.Empty("Tip", _pivot, new Vector3(0, 1.3f, 0));
            Art3D.Part(PrimitiveType.Sphere, kit.yellow, _tip, new Vector3(0, -0.03f, 0.015f), new Vector3(0.035f, 0.03f, 0.035f), null, "Bell");
            _hook = Art3D.Empty("Hook", transform, Vector3.zero);
            Art3D.Part(PrimitiveType.Cube, kit.metal, _hook, new Vector3(0, -0.006f, 0), new Vector3(0.004f, 0.012f, 0.004f));
            _float = Art3D.Empty("Float", transform, Vector3.zero);
            Art3D.Part(PrimitiveType.Sphere, kit.red, _float, new Vector3(0, 0.012f, 0), new Vector3(0.03f, 0.024f, 0.03f));
            Art3D.Part(PrimitiveType.Sphere, kit.white, _float, new Vector3(0, -0.006f, 0), new Vector3(0.028f, 0.02f, 0.028f));
            _line = Art3D.Line("Line", transform, kit.line, 0.0025f);
            _line.positionCount = 3;
            // the hourglass on the tub's front edge, by the post
            var glass = Art3D.Empty("Hourglass", transform, new Vector3(0.42f, h, 0.36f));
            Art3D.Part(PrimitiveType.Cylinder, kit.brown, glass, new Vector3(0, 0.005f, 0), new Vector3(0.07f, 0.005f, 0.07f));
            Art3D.Part(PrimitiveType.Cylinder, kit.brown, glass, new Vector3(0, 0.125f, 0), new Vector3(0.07f, 0.005f, 0.07f));
            Art3D.Part(PrimitiveType.Sphere, kit.clear, glass, new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.055f, 0.05f));
            Art3D.Part(PrimitiveType.Sphere, kit.clear, glass, new Vector3(0, 0.095f, 0), new Vector3(0.05f, 0.055f, 0.05f));
            _sandTop = Art3D.Part(PrimitiveType.Sphere, kit.yellow, glass, new Vector3(0, 0.09f, 0), new Vector3(0.04f, 0.04f, 0.04f));
            _sandBottom = Art3D.Part(PrimitiveType.Sphere, kit.yellow, glass, new Vector3(0, 0.022f, 0), new Vector3(0.04f, 0.0f, 0.04f));
            Poster(kit);
            Grips();
        }

        /// The board behind the tub: the fish that live in it, each with its name under it, and «кто клюёт» over them.
        void Poster(PersonaKit kit)
        {
            const float z = -0.37f, y0 = 0.95f, bw = 1.46f, bh = 0.7f;
            foreach (float x in new[] { -0.66f, 0.66f })
                Art3D.Part(PrimitiveType.Cube, kit.brown, transform, new Vector3(x * 1.04f, (y0 + bh) / 2, z - 0.012f), new Vector3(0.04f, y0 + bh, 0.025f));
            Art3D.Part(PrimitiveType.Cube, kit.grey, transform, new Vector3(0, y0 + bh / 2, z - 0.008f), new Vector3(bw, bh, 0.01f));
            var words = Art3D.Upright(transform, new Vector3(0, y0 + bh / 2, z), new Vector2(bw * 1000, bh * 1000));
            Art3D.Word(words, "Кто клюёт", new Vector2(0, bh * 500 - 70), 64, ChalkTex.White, 600);
            var names = Oddity.Options("fish");
            for (int i = 0; i < 5; i++)
            {
                float x = -0.56f + i * 0.28f;
                var spot = Art3D.Empty("Fish " + names[i], transform, new Vector3(x, y0 + 0.36f, z + 0.03f));
                FishArt.Build(i, spot, kit, 0.8f);
                Art3D.Word(words, names[i], new Vector2(-x * 1000, -95), 46, ChalkTex.White, 260);
            }
        }

        /// What the dot finds: the rod and the tub (the water) — clicking either works the rod.
        void Grips()
        {
            Art3D.Grip("TubGrip", transform, new Vector3(0, 0.25f, 0), new Vector3(1.5f, 0.5f, 0.72f));
            Art3D.Grip("PostGrip", transform, new Vector3(0.62f, 0.6f, 0.45f), new Vector3(0.2f, 1.2f, 0.2f));
            var rodGrip = Art3D.Grip("RodGrip", _pivot, new Vector3(0, 0.65f, 0), new Vector3(0.1f, 1.3f, 0.1f));
            rodGrip.transform.localPosition = new Vector3(0, 0.65f, 0);
        }

        protected override void OnPut(Sample3D s)
        {
            _state = State.Rest;
            Caught = -1;
            Sfx.Play("tick", 0.5f, 1.3f);
        }

        protected override void OnTaken(Sample3D s)
        {
            _state = State.Rest;
            _bit = false;
            if (_fishOnLine != null) Destroy(_fishOnLine.gameObject);
        }

        public override void Down()
        {
            if (Load == null) return;
            if (_state == State.Rest)
            {
                _state = State.Casting;
                _t = 0;
                _bit = false;
                Caught = -1;
                _hookFrom = _hook.position;
                Load.Driven = true;
                Sfx.Play("whoosh", 0.55f, 1.2f);
            }
            else if (_state == State.Waiting)
            {
                // pulled out: what bit, if anything bit; nobody, if the sand ran out with nothing on the line
                Caught = _bit ? Load.data.fish : _t >= Sand && !_hadBite ? 5 : -1;   // a fish that got off: someone does bite
                _state = State.Reeling;
                _t = 0;
                _hookFrom = _hook.position;
                Sfx.Play("reel", 0.6f);
                if (_bit) { _fishOnLine = Art3D.Empty("Caught", transform, Vector3.zero); FishArt.Build(Load.data.fish, _fishOnLine, game.kit, 1f); }
                Tried = true;
            }
        }

        Vector3 RestHook => _tip.position + Vector3.down * 0.72f;
        Vector3 Cast => transform.TransformPoint(CastAt);

        void Update()
        {
            if (_pivot == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            Swim();
            float tilt = RestTilt;
            Vector3 hook = RestHook, flt = hook + Vector3.up * 0.2f;
            switch (_state)
            {
                case State.Rest:
                    if (Load != null) Load.Driven = false;
                    break;
                case State.Casting:
                {   // the rod swings back, the bait flies in an arc and drops into the water
                    float k = Mathf.Clamp01(_t / 0.8f);
                    tilt = Mathf.Lerp(RestTilt, CastTilt, Mathf.SmoothStep(0, 1, Mathf.Clamp01(_t / 0.35f)));
                    var to = Cast + Vector3.down * 0.12f;
                    hook = Vector3.Lerp(_hookFrom, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.35f;
                    flt = Vector3.Lerp(_hookFrom + Vector3.up * 0.2f, Cast, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
                    if (k >= 1)
                    {
                        _state = State.Waiting;
                        _t = 0;
                        _ring = 0;
                        _hadBite = false;
                        _biteAt = Load != null && Load.data != null && Load.data.fish < 5 ? Random.Range(3f, Sand - Hold) : float.MaxValue;
                        Sfx.Play("splash", 0.7f);
                    }
                    break;
                }
                case State.Waiting:
                {
                    tilt = CastTilt;
                    float bob = Mathf.Sin(_t * 2.2f) * 0.004f;
                    if (!_bit && _t >= _biteAt) { _bit = true; _hadBite = true; }
                    if (_bit && _t >= _biteAt + Hold)
                    {   // nobody came: the fish gets off the hook with a splash; another bite a little later, if there is time
                        _bit = false;
                        Sfx.Play("splash", 0.35f, 1.5f);
                        float again = _t + Random.Range(3f, 6f);
                        _biteAt = again < Sand - 1 ? again : float.MaxValue;
                    }
                    if (_bit)
                    {   // the float dips, the tip shakes, the bell rings
                        bob = -Mathf.Abs(Mathf.Sin(_t * 9)) * 0.03f;
                        tilt += Mathf.Sin(_t * 40) * 1.5f;
                        _ring -= dt;
                        if (_ring <= 0) { _ring = 1.1f; Sfx.Play("tinkle", 0.75f, 1f, 0.03f); }
                    }
                    flt = Cast + Vector3.up * bob;
                    hook = Cast + Vector3.down * 0.12f + Vector3.up * bob;
                    break;
                }
                case State.Reeling:
                {
                    float k = Mathf.Clamp01(_t / 0.7f);
                    tilt = Mathf.Lerp(CastTilt, RestTilt, Mathf.SmoothStep(0, 1, k));
                    hook = Vector3.Lerp(_hookFrom, RestHook, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.3f;
                    flt = hook + Vector3.up * 0.2f;
                    if (k >= 1)
                    {
                        _state = Caught >= 0 && Caught < 5 ? State.Showing : State.Rest;
                        _t = 0;
                        if (_state == State.Showing) Sfx.Play("splash", 0.4f, 1.6f);
                    }
                    break;
                }
                case State.Showing:
                    // the fish flaps on the line a moment, then back it goes into the water
                    if (_t > 2.6f)
                    {
                        if (_fishOnLine != null) StartCoroutine(Release(_fishOnLine));
                        _fishOnLine = null;
                        _state = State.Rest;
                    }
                    break;
            }
            _tilt = tilt;
            _pivot.localRotation = Quaternion.Euler(_tilt, 0, 0);
            _hook.position = hook;
            _float.position = flt;
            _line.SetPosition(0, _tip.position);
            _line.SetPosition(1, flt);
            _line.SetPosition(2, hook);
            if (Load != null && Load.Driven) Load.transform.position = hook + Vector3.down * (Load.Size + 0.01f);
            if (_fishOnLine != null)
            {
                float hang = (Load != null ? Load.Size : 0) + 0.13f;   // its mouth on the bait, under it
                _fishOnLine.position = hook + Vector3.down * hang;
                _fishOnLine.rotation = transform.rotation * Quaternion.Euler(0, 0, 90) * Quaternion.Euler(Mathf.Sin(Time.time * 14) * 25, 0, 0);
            }
            // the hourglass: the sand runs while the bait is in the water
            float sand = _state == State.Waiting ? Mathf.Clamp01(_t / Sand) : _state == State.Casting ? 0 : 1;
            _sandTop.localScale = new Vector3(0.04f, 0.04f * (1 - sand), 0.04f);
            _sandBottom.localScale = new Vector3(0.04f, 0.035f * sand, 0.04f);
        }

        System.Collections.IEnumerator Release(Transform fish)
        {
            var from = fish.position;
            var to = Cast + new Vector3(Random.Range(-0.2f, 0.2f), 0, 0);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.5f)
            {
                fish.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.25f;
                fish.Rotate(0, 0, 600 * Time.deltaTime, Space.Self);
                yield return null;
            }
            Sfx.Play("splash", 0.6f, 1.2f);
            Destroy(fish.gameObject);
        }

        void Swim()
        {
            for (int i = 0; i < _shadows.Count; i++)
            {
                float a = Time.time * (0.4f + 0.13f * i) + i * 2.1f;
                var p = new Vector3(Mathf.Cos(a) * (0.42f - i * 0.08f), _shadows[i].localPosition.y, Mathf.Sin(a) * (0.18f - i * 0.03f));
                var dir = new Vector3(-Mathf.Sin(a) * (0.42f - i * 0.08f), 0, Mathf.Cos(a) * (0.18f - i * 0.03f));
                _shadows[i].localPosition = p;
                _shadows[i].localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }
    }

    /// The five fish of the pond, built of chalk shapes, head along +x: a striped perch, a round golden crucian, a long
    /// pike, a dark whiskered catfish, a small spiky ruffe — each looks unlike the others at a glance. Everything on the
    /// body (stripes, spots, fins, spines) is set on its surface: the body is an ellipsoid, and its height and thickness
    /// at a point along it are known.
    public static class FishArt
    {
        public static void Build(int kind, Transform root, PersonaKit k, float scale)
        {
            float len, tall, thick;
            Material body;
            switch (kind)
            {
                case 0: len = 0.2f; tall = 0.09f; body = k.green; break;    // perch
                case 1: len = 0.17f; tall = 0.13f; body = k.yellow; break;  // crucian
                case 2: len = 0.34f; tall = 0.06f; body = k.green; break;   // pike
                case 3: len = 0.3f; tall = 0.1f; body = k.dark; break;      // catfish
                default: len = 0.13f; tall = 0.07f; body = k.brown; break;  // ruffe
            }
            thick = tall * 0.55f;
            // the body's half-height and half-thickness at x along it (an ellipsoid)
            float F(float x) => Mathf.Sqrt(Mathf.Max(0, 1 - (2 * x / len) * (2 * x / len)));
            float Up(float x) => tall / 2 * F(x);
            float Side(float x) => thick / 2 * F(x);
            Transform P(PrimitiveType t, Material m, Vector3 at, Vector3 size, float rz = 0) =>
                Art3D.Part(t, m, root, at * scale, size * scale, new Vector3(0, 0, rz));
            P(PrimitiveType.Sphere, body, Vector3.zero, new Vector3(len, tall, thick));
            // the tail: two fins in a V from the body's end
            float tailX = -len / 2 - tall * 0.12f;
            P(PrimitiveType.Sphere, body, new Vector3(tailX, tall * 0.16f, 0), new Vector3(tall * 0.62f, tall * 0.2f, 0.01f), 40);
            P(PrimitiveType.Sphere, body, new Vector3(tailX, -tall * 0.16f, 0), new Vector3(tall * 0.62f, tall * 0.2f, 0.01f), -40);
            // the eyes, set into the head's sides
            float ex = len * 0.3f, ey = tall * 0.12f, ez = Side(ex) * Mathf.Sqrt(Mathf.Max(0, 1 - (ey / Up(ex)) * (ey / Up(ex))));
            float er = kind == 4 ? 0.028f : 0.02f;
            foreach (float sz in new[] { -1f, 1f })
            {
                P(PrimitiveType.Sphere, k.white, new Vector3(ex, ey, sz * ez), new Vector3(er, er, er * 0.6f));
                P(PrimitiveType.Sphere, k.dark, new Vector3(ex + 0.002f, ey, sz * (ez + er * 0.22f)), new Vector3(er * 0.5f, er * 0.5f, er * 0.3f));
            }
            switch (kind)
            {
                case 0:   // perch: dark bands round the body, red fins below, a spiny fin on the back
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -len * 0.25f + i * len * 0.13f;
                        P(PrimitiveType.Sphere, k.dark, new Vector3(x, 0, 0), new Vector3(0.012f, tall * F(x) * 1.03f, thick * F(x) * 1.04f));
                    }
                    P(PrimitiveType.Sphere, k.red, new Vector3(0.0f, -Up(0) + 0.004f, 0), new Vector3(len * 0.22f, tall * 0.32f, 0.008f));
                    P(PrimitiveType.Sphere, k.dark, new Vector3(-len * 0.02f, Up(0) - 0.004f, 0), new Vector3(len * 0.4f, tall * 0.45f, 0.008f));
                    break;
                case 1:   // crucian: round and golden, orange fins on the back and below
                    P(PrimitiveType.Sphere, k.orange, new Vector3(-len * 0.05f, Up(0) - 0.005f, 0), new Vector3(len * 0.45f, tall * 0.3f, 0.008f));
                    P(PrimitiveType.Sphere, k.orange, new Vector3(-len * 0.1f, -Up(0) + 0.005f, 0), new Vector3(len * 0.25f, tall * 0.22f, 0.008f));
                    break;
                case 2:   // pike: a long flat snout, yellow spots on the sides, its back fin far back
                    P(PrimitiveType.Sphere, body, new Vector3(len * 0.47f, -tall * 0.05f, 0), new Vector3(len * 0.3f, tall * 0.42f, thick * 0.6f));
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -len * 0.3f + i * len * 0.11f, y = (i % 2 - 0.5f) * tall * 0.3f;
                        float z = Side(x) * Mathf.Sqrt(Mathf.Max(0, 1 - (y / Mathf.Max(0.001f, Up(x))) * (y / Mathf.Max(0.001f, Up(x)))));
                        foreach (float sz in new[] { -1f, 1f }) P(PrimitiveType.Sphere, k.yellow, new Vector3(x, y, sz * z), new Vector3(0.016f, 0.012f, 0.006f));
                    }
                    P(PrimitiveType.Sphere, body, new Vector3(-len * 0.33f, Up(-len * 0.33f) - 0.002f, 0), new Vector3(len * 0.16f, tall * 0.5f, 0.008f));
                    break;
                case 3:   // catfish: a wide flat head, long whiskers from the mouth
                    P(PrimitiveType.Sphere, body, new Vector3(len * 0.3f, -tall * 0.05f, 0), new Vector3(len * 0.4f, tall * 0.85f, thick * 1.5f));
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        var w = Art3D.Part(PrimitiveType.Cube, k.dark, root, new Vector3(len * 0.62f, -tall * 0.18f, sz * thick * 0.45f) * scale, new Vector3(len * 0.42f, 0.004f, 0.004f) * scale);
                        w.localRotation = Quaternion.Euler(0, sz * -25, -20);
                    }
                    break;
                default:  // ruffe: a big spiny fin all along its back, big eyes
                    P(PrimitiveType.Sphere, k.dark, new Vector3(-len * 0.02f, Up(0) - 0.003f, 0), new Vector3(len * 0.7f, tall * 0.85f, 0.008f));
                    for (int i = 0; i < 6; i++)
                    {   // the spines stand out of the body through the fin, their feet inside the back
                        float x = -len * 0.25f + i * len * 0.1f;
                        P(PrimitiveType.Cube, k.dark, new Vector3(x, Up(x) + tall * 0.15f, 0), new Vector3(0.004f, tall * 0.55f, 0.004f), -10 + i * 4);
                    }
                    break;
            }
        }
    }
}
