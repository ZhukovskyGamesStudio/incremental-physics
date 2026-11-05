using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The feather (DESIGN.md 2б, «Крейзи-измерения»): the sample lies on a velvet cushion, a big white feather lies on
    /// the table beside it. Hold the button with the dot on the sample — the feather comes up and tickles it, to and fro;
    /// look away or let go and it goes back down. A ticklish sample soon wriggles and hops and giggles; the rest lie
    /// still, however long you tickle.
    public class Feather3D : OddApparatus
    {
        Transform _feather;
        float _up;   // 0 the feather lies on the table, 1 it is over the sample
        /// The bot's hands have no dot: it holds the feather on the sample all the same.
        [System.NonSerialized] public bool steady;
        bool _holding;
        float _tickled, _giggle;
        /// The sample giggled under the feather (the bot sees it).
        public bool Giggled { get; private set; }
        public float Tickled => _tickled;

        public override string PutVerb => "положить на подушечку";
        public override bool HasAction => Load != null;
        public override string Verb => "держать пером на образце";

        protected override Material Needs(PersonaKit kit) => kit.purple;

        protected override void Build(PersonaKit kit)
        {
            Art3D.Part(PrimitiveType.Cylinder, kit.purple, transform, new Vector3(0, 0.02f, 0.06f), new Vector3(0.26f, 0.02f, 0.26f));
            foreach (float a in new[] { 0f, 90f, 180f, 270f })   // tassels at its edge
                Art3D.Part(PrimitiveType.Sphere, kit.yellow, transform, new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * 0.13f, 0.02f, 0.06f + Mathf.Sin(a * Mathf.Deg2Rad) * 0.13f), Vector3.one * 0.022f);
            seat = Art3D.Empty("Seat", transform, new Vector3(0, 0.04f, 0.06f));
            // the feather lies on the table beside the cushion
            _feather = Art3D.Empty("Feather", transform, RestPos, null);
            _feather.localRotation = RestRot;
            Quill(kit);
            Art3D.Grip("Grip", transform, new Vector3(0, 0.1f, 0.06f), new Vector3(0.3f, 0.2f, 0.3f));
        }

        /// A white quill hanging from the arm: a bare tip of the shaft at the top, a little fluff, then the vane — barbs
        /// on both sides of the gently curving shaft, swept towards its point, widest a third of the way down — and the
        /// shaft's point at the bottom, which does the tickling.
        void Quill(PersonaKit kit)
        {
            const float L = 0.32f;
            Vector3 At(float t) => new Vector3(Mathf.Sin(t * 1.4f) * 0.035f, -t * L, 0);
            const int n = 16;
            for (int i = 0; i < n; i++)
            {   // the shaft, in short pieces along its curve, thinner to the point
                float t0 = i / (float)n, t1 = (i + 1) / (float)n;
                var a = At(t0);
                var b = At(t1);
                float w = Mathf.Lerp(0.006f, 0.0025f, t0);
                var seg = Art3D.Part(PrimitiveType.Cylinder, kit.white, _feather, (a + b) / 2, new Vector3(w, (b - a).magnitude / 2, w));
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }
            for (int i = 0; i < 4; i++)   // a little fluff at the bottom of the vane's start
            {
                float t = 0.1f + i * 0.025f;
                foreach (float s in new[] { -1f, 1f })
                    Art3D.Part(PrimitiveType.Sphere, kit.white, _feather, At(t) + new Vector3(s * 0.012f, 0, 0), new Vector3(0.018f, 0.012f, 0.008f), new Vector3(0, 0, s * 30));
            }
            for (int i = 0; i <= 30; i++)
            {   // the barbs, swept down towards the point
                float t = 0.2f + i / 30f * 0.76f;
                float u = (t - 0.2f) / 0.76f;
                float w = 0.05f * Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f)) + 0.006f;
                var p = At(t);
                var tangent = (At(t + 0.01f) - p).normalized;
                foreach (float s in new[] { -1f, 1f })
                {
                    var side = Quaternion.Euler(0, 0, s * 55) * tangent;   // out to the side, sloping down the shaft
                    var barb = Art3D.Part(PrimitiveType.Cube, kit.white, _feather, p + side.normalized * (w * 0.5f), new Vector3(w, 0.0022f, 0.0022f));
                    barb.localRotation = Quaternion.FromToRotation(Vector3.right, side.normalized);
                }
            }
        }

        protected override void OnPut(Sample3D s) { Giggled = false; _tickled = 0; }
        protected override void OnTaken(Sample3D s) { _holding = false; _tickled = 0; }
        public override void Down() { if (Load != null) { _holding = true; Tried = true; } }
        public override void Up() => _holding = false;

        static readonly Vector3 RestPos = new Vector3(0.2f, 0.006f, -0.14f);
        // lying flat: the quill hangs along its -y, turned to lie along +x, its vane flat, a little askew on the table
        static readonly Quaternion RestRot = Quaternion.Euler(0, -25, 0) * Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 90);

        /// The dot is on the cushion or the sample on it.
        bool OnMe() => Pointed != null && (Pointed.transform.IsChildOf(transform) || (Load != null && Pointed.transform.IsChildOf(Load.transform)));

        void Update()
        {
            if (_feather == null) return;
            bool on = _holding && Load != null && (steady || OnMe());
            _up = Mathf.MoveTowards(_up, on ? 1 : 0, Time.deltaTime / 0.25f);
            float e = _up * _up * (3 - 2 * _up);
            // over the sample: hanging point down, its tip just on the sample's top, wagging
            float top = 0.04f + (Load != null ? Load.Size : 0.05f);
            var overPos = new Vector3(0.0f, top + 0.3f, 0.06f);
            var wag = Quaternion.Euler(Mathf.Sin(Time.time * 26) * 6, 0, Mathf.Sin(Time.time * 13) * 12);
            _feather.localPosition = Vector3.Lerp(RestPos, overPos, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.12f;
            _feather.localRotation = Quaternion.Slerp(RestRot, wag, e);
            if (on && _up > 0.9f)
            {
                _tickled += Time.deltaTime;
                if (Random.value < Time.deltaTime * 4) Sfx.Play("whoosh", 0.12f, 2.2f);
            }
            else if (!on) _tickled = 0;
            if (Load == null) return;
            bool giggling = on && _tickled > 0.5f && Load.data != null && Load.data.ticklish;
            if (giggling)
            {   // it wriggles and hops, and giggles
                Giggled = true;
                Load.Driven = true;
                float hop = Mathf.Abs(Mathf.Sin(Time.time * 11)) * 0.02f;
                Load.transform.position = seat.position + Vector3.up * hop;
                Load.transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 31) * 6, Mathf.Sin(Time.time * 7) * 20, Mathf.Sin(Time.time * 37) * 10);
                _giggle -= Time.deltaTime;
                if (_giggle <= 0) { _giggle = 0.7f; Sfx.Play("giggle", 0.7f, Random.Range(0.95f, 1.15f), 0); }
            }
            else if (Load.Driven) Load.Driven = false;
        }
    }
}
