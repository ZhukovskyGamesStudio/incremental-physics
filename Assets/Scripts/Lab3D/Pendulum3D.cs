using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The pendulum, T = 2π√(l/g): click to push the sample on its thread. Every swing (side to side) pays mg ideas,
    /// a full swing all of it, a dying one down to half; the swing dies down by itself. A push counts in full when
    /// the sample is at the end of its swing and hardly at all when it rushes through the bottom, so pushing in time
    /// keeps it going. A shorter thread (l) and a stronger g swing faster: more swings a minute.
    public class Pendulum3D : Apparatus3D
    {
        public Transform pivot;         // the thread hangs from it and swings with it
        public Transform thread;        // one metre long at scale 1
        public Transform bob;           // the end of the thread: where the sample's top is
        public float maxAngle = 40f;

        float _amp, _phase;
        bool _fresh;

        protected override Transform Seat => bob;
        protected override Vector3 SeatFor(Sample3D s) => Vector3.down * s.Size;
        public override bool HasAction => true;
        public override string Verb => "толкнуть";
        public override string PutVerb => "повесить";
        public float Amp => _amp;
        public float Swing => Mathf.Abs(Mathf.Sin(_phase));   // 0 at the ends of a swing, 1 at the bottom

        float Length => 0.22f + 0.3f * Mathf.Sqrt((float)V("l"));
        float Period => Mathf.Clamp(1.7f * Mathf.Sqrt((float)(V("l") / G)), 0.45f, 2.5f);

        public override void Down()
        {
            if (Load == null) return;
            float good = _amp < 0.05f ? 1 : 1 - Swing;
            if (_amp < 0.05f) _phase = 0;
            _amp = Mathf.Min(1, _amp + 0.9f * good);
            _fresh = true;
        }

        protected override void OnTaken(Sample3D s) => _amp = 0;

        void Update()
        {
            if (game == null) return;
            float len = Length;
            thread.localScale = new Vector3(1, len, 1);
            bob.localPosition = Vector3.down * len;
            if (_amp > 0)
            {
                float before = Mathf.Floor(_phase / Mathf.PI);
                _phase += 2 * Mathf.PI / Period * Time.deltaTime;
                if (Mathf.Floor(_phase / Mathf.PI) > before) Swung();
            }
            pivot.localRotation = Quaternion.Euler(0, 0, _amp * maxAngle * Mathf.Cos(_phase));
        }

        /// The sample has reached the end of a swing.
        void Swung()
        {
            if (Load != null && _amp >= 0.25f)
            {
                Pay(0.5 + 0.5 * _amp, _fresh ? "mg = " + GameState.Fmt(M * G) : null);
                _fresh = false;
            }
            _amp *= 0.85f;
            if (_amp < 0.03f) _amp = 0;
        }
    }
}
