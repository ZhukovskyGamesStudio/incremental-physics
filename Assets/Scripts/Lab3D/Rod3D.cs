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
            // the Blender model (Tools/Blender/odd.py, pond): the tub of water with reeds and lily pads, the post, the
            // board behind; what moves is its own piece — the fish under the water, the rod, the hook, the float, the sand
            var m = ModelLibrary.Spawn("Odd_pond", transform, kit)?.transform;
            if (m == null) return;
            for (int i = 0; i < 3; i++) _shadows.Add(Art3D.Pivot(m, "Shadow" + i, transform));
            _pivot = Art3D.Pivot(m, "Rod", transform);
            _pivot.name = "RodPivot";
            _tip = Art3D.Empty("Tip", _pivot, new Vector3(0, 1.3f, 0));
            _hook = Art3D.Pivot(m, "Hook", transform);
            _float = Art3D.Pivot(m, "Float", transform);
            _line = Art3D.Line("Line", transform, kit.line, 0.0025f);
            _line.positionCount = 3;
            _sandTop = Art3D.Pivot(m, "SandTop", transform);
            _sandBottom = Art3D.Pivot(m, "SandBottom", transform);
            Poster(kit);
            Grips();
        }

        /// The board behind the tub (the model's): the fish that live in it, each with its name under it, and «кто клюёт»
        /// over them.
        void Poster(PersonaKit kit)
        {
            const float z = -0.37f, y0 = 0.95f, bw = 1.46f, bh = 0.7f;
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
            _sandTop.localScale = new Vector3(1, Mathf.Max(0.02f, 1 - sand), 1);
            _sandBottom.localScale = new Vector3(1, Mathf.Max(0.02f, sand), 1);
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

    /// The five fish of the pond, Blender models (Tools/Blender/odd.py, fish), head along +x: a striped perch, a round
    /// golden crucian, a long spotted pike, a dark whiskered catfish, a small spiky ruffe — each unlike the others at a
    /// glance. On the board behind the tub and on the line.
    public static class FishArt
    {
        static readonly string[] Kinds = { "perch", "crucian", "pike", "catfish", "ruffe" };

        public static void Build(int kind, Transform root, PersonaKit k, float scale)
        {
            var go = ModelLibrary.Spawn("Odd_fish_" + Kinds[Mathf.Clamp(kind, 0, Kinds.Length - 1)], root, k);
            if (go != null) go.transform.localScale = Vector3.one * scale;
        }
    }
}
