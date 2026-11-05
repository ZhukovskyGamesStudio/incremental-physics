using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The cat (DESIGN.md 2б, «Крейзи-измерения»): a ginger cat asleep on a cushion, a saucer in front of it. Put the
    /// sample on the saucer: the cat wakes, sniffs at it — and purrs (eyes shut, rubbing its head, rumbling), hisses
    /// (back arched, fur up, tail up), plays (bats at it with a paw), or yawns and goes back to sleep. Then it curls up
    /// again; stroke it to see once more.
    public class Cat3D : OddApparatus
    {
        Transform _cat, _body, _head, _tail, _paw, _spikes;
        readonly Transform[] _eyes = new Transform[2], _ears = new Transform[2];
        float _t = -1;
        int _kind;
        /// What the cat did the last time (0 purrs, 1 hisses, 2 plays, 3 sleeps on), -1 not yet.
        public int Shown { get; private set; } = -1;
        public bool Reacting => _t >= 0;

        public override string PutVerb => "положить на блюдце";
        public override bool HasAction => Load != null && _t < 0;
        public override string Verb => "погладить кота";
        public override bool Busy => _t >= 0 && _t < 0.4f;

        protected override Material Needs(PersonaKit kit) => kit.orange;

        protected override void Build(PersonaKit kit)
        {
            // the Blender model (Tools/Blender/odd.py, cat): the cushion and the saucer stay, the cat's pieces move
            var m = ModelLibrary.Spawn("Odd_cat", transform, kit)?.transform;
            if (m == null) return;
            seat = Art3D.Pivot(m, "Seat", transform);
            _cat = Art3D.Empty("Cat", transform, new Vector3(0.08f, 0.06f, -0.08f), new Vector3(0, -20, 0));
            _body = Art3D.Pivot(m, "Body", _cat);
            _spikes = Art3D.Pivot(m, "Spikes", _body);   // the fur up along the back: only when it hisses
            _spikes.gameObject.SetActive(false);
            _paw = Art3D.Pivot(m, "Paw", _body);          // the left front paw: it bats
            _tail = Art3D.Pivot(m, "Tail", _body);
            _head = Art3D.Pivot(m, "Head", _body);
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                _ears[i] = Art3D.Pivot(m, "Ear" + side, _head);
                _eyes[i] = Art3D.Pivot(m, "Eye" + side, _head);
            }
            Art3D.Grip("Grip", transform, new Vector3(0.02f, 0.12f, 0.05f), new Vector3(0.5f, 0.24f, 0.62f));
            Pose(0, 0, 0);
        }

        protected override void OnPut(Sample3D s) => Wake();
        public override void Down() { if (Load != null && _t < 0) Wake(); }

        void Wake()
        {
            _t = 0;
            Shown = -1;
            _kind = Load != null && Load.data != null ? Load.data.cat : 3;
        }

        /// The cat's pose: `awake` 0 (curled up asleep) to 1 (head up), `eyes` 0 shut to 1 open, `arch` 0 to 1 (hissing).
        void Pose(float awake, float eyes, float arch)
        {
            _head.localPosition = new Vector3(0, Mathf.Lerp(0.1f, 0.17f, awake), Mathf.Lerp(0.18f, 0.2f, awake));
            _head.localRotation = Quaternion.Euler(Mathf.Lerp(18, -5, awake), Mathf.Lerp(-10, 0, awake), 0);
            foreach (var e in _eyes) e.localScale = new Vector3(1, Mathf.LerpUnclamped(0.12f, 1, eyes), 1);
            _body.localScale = new Vector3(1 - 0.12f * arch, 1 + 0.4f * arch, 1 - 0.08f * arch);
            _spikes.gameObject.SetActive(arch > 0.5f);
            _tail.localRotation = Quaternion.Euler(0, 0, 80 * arch);   // up from the rump
            for (int i = 0; i < 2; i++) _ears[i].localRotation = Quaternion.Euler(-50 * arch, 0, 0);   // laid back
        }

        void Update()
        {
            if (_head == null || _t < 0) return;
            float was = _t;
            _t += Time.deltaTime;
            // waking up and sniffing at the saucer
            if (_t < 0.7f) { Pose(Mathf.Clamp01(_t / 0.5f), Mathf.Clamp01(_t / 0.4f), 0); _head.localRotation *= Quaternion.Euler(10, -15, 0); return; }
            if (was < 0.7f)
            {
                Shown = _kind;
                Tried = true;
                switch (_kind)
                {
                    case 0: Sfx.Play("purr", 0.8f); break;
                    case 1: Sfx.Play("hiss", 0.75f, 1.7f); break;
                    case 2: Sfx.Play("meow", 0.6f, 1.25f); break;
                    default: Sfx.Play("yawn", 0.6f); break;
                }
            }
            float u = _t - 0.7f, len = 2.4f, s = Mathf.Sin(Mathf.Clamp01(u / len) * Mathf.PI);
            switch (_kind)
            {
                case 0:   // purrs: eyes shut happily, the head rubs from side to side, the whole cat hums
                    Pose(1, 0.15f, 0);
                    _head.localRotation *= Quaternion.Euler(0, 0, Mathf.Sin(u * 4) * 14 * s);
                    _body.localScale = Vector3.one * (1 + Mathf.Sin(u * 70) * 0.012f * s);
                    break;
                case 1:   // hisses: the back up, the fur up, the tail straight up, the ears back, the eyes wide
                    Pose(1, 1.3f, Mathf.Clamp01(u / 0.25f) * Mathf.Clamp01((len - u) / 0.4f));
                    if (u > 1.1f && u - Time.deltaTime <= 1.1f) Sfx.Play("hiss", 0.6f, 1.9f);
                    break;
                case 2:   // plays: bats at the sample with a paw, again and again
                    Pose(1, 1, 0);
                    float bat = Mathf.Max(0, Mathf.Sin(u * 9));
                    _paw.localPosition = new Vector3(-0.05f, 0.03f + bat * 0.05f, 0.21f + bat * 0.07f);
                    if (Load != null) { Load.Driven = true; Load.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(u * 9) * 12 * s); }
                    if (Mathf.Sin(u * 9) > 0.95f && Mathf.Sin((u - Time.deltaTime) * 9) <= 0.95f) Sfx.Play("knock", 0.35f, 1.4f);
                    break;
                default:  // sleeps on: one eye half open, a big yawn, back down
                    Pose(Mathf.Clamp01(1 - u / len) * 0.6f, 0.35f * Mathf.Clamp01(1 - u / 1.2f), 0);
                    _head.localRotation *= Quaternion.Euler(-30 * Mathf.Sin(Mathf.Clamp01(u / 1.2f) * Mathf.PI), 0, 0);
                    break;
            }
            if (u >= len)
            {
                _t = -1;
                _paw.localPosition = new Vector3(-0.05f, 0.03f, 0.21f);
                if (Load != null && Load.Driven) { Load.Driven = false; }
                Pose(0, 0, 0);
            }
        }
    }
}
