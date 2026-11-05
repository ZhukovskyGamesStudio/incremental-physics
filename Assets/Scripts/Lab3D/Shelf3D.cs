using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The drop shelf, E = mgh (DESIGN.md 2б, «Второй прибор — полка»): hold the button and the carriage lifts the sample
    /// up the posts, to the top bar at the height h; let go and it falls onto the stake and drives it into the block.
    /// The stake goes in by the energy of the blow — a scale beside it, in joules, reads it by eye — and stays in until
    /// the next lift pulls it out again. Dropped from lower than the top, the blow is weaker than mgh; the block is not
    /// quite true either: every blow is off by up to 10% (less with the map's upgrades).
    public class Shelf3D : MeasuringApparatus
    {
        public Transform carriage;      // its top is where the sample stands
        public Transform stake;         // its top at rest is the impact scale's zero
        public float travel = 0.59f;    // from the stake up to the top bar
        public float liftTime = 1.2f;
        public float fallG = 6f;
        public float perJoule = 0.03f;  // metres the stake goes in a joule
        public float maxJoules = 3f;    // where the impact scale ends

        enum State { Rest, Lift, Fall, Bounce }
        State _state;
        float _y, _v, _frac, _t, _sink, _sinkTarget;
        bool _dropped;
        Vector3 _rest, _stakeRest;

        protected override Transform Seat => carriage;
        public override bool HasAction => true;
        public override bool Busy => _state == State.Lift || _state == State.Fall;
        public override string Verb => "поднять: держи";
        public float Lifted => _y / travel;
        /// The energy of a blow from the top bar: E = mgh, the sample's weight times the height.
        public double Energy => Load != null ? Load.mass * G * V("h") : 0;
        /// What the stake shows on the scale now, in joules.
        public override double Reading => _sink / perJoule;
        /// The sample has been dropped and the stake has stopped: the blow can be read.
        public override bool Settled => Load != null && _dropped && _state == State.Rest && Mathf.Abs(_sink - _sinkTarget) < 0.0005f;

        void Awake()
        {
            _rest = carriage.localPosition;
            _stakeRest = stake.localPosition;
        }

        public override void Down()
        {
            if (Load == null || !(_state == State.Rest || _state == State.Bounce)) return;
            _state = State.Lift;
            _y = Mathf.Max(_y, 0);
            _sinkTarget = 0;   // the lift pulls the stake back out
            _dropped = false;
        }

        public override void Up()
        {
            if (_state != State.Lift) return;
            _frac = _y / travel;
            _v = 0;
            _state = State.Fall;
        }

        protected override void OnPut(Sample3D s) { _dropped = false; _sinkTarget = 0; }

        protected override void OnTaken(Sample3D s)
        {
            if (_state == State.Lift) { _state = State.Fall; _frac = 0; }
            _dropped = false;
            _sinkTarget = 0;
        }

        void Update()
        {
            Ticks();
            float dt = Time.deltaTime;
            switch (_state)
            {
                case State.Lift:
                    _y = Mathf.Min(travel, _y + travel / liftTime * dt);
                    break;
                case State.Fall:
                    _v += fallG * dt;
                    _y -= _v * dt;
                    if (_y <= 0) { _y = 0; Hit(); }
                    break;
                case State.Bounce:
                    _t += dt;
                    _y = 0.02f * _frac * Mathf.Abs(Mathf.Sin(Mathf.PI * _t / 0.22f)) * Mathf.Max(0, 1 - _t / 0.66f);
                    if (_t > 0.66f) { _y = 0; _state = State.Rest; }
                    break;
            }
            // the blow drives the stake in at once; the next lift draws it out more slowly
            _sink = Mathf.MoveTowards(_sink, _sinkTarget, (_sinkTarget > _sink ? 0.6f : 0.12f) * dt);
            carriage.localPosition = _rest + Vector3.up * (_y - _sink);
            stake.localPosition = _stakeRest + Vector3.down * _sink;
        }

        void Hit()
        {
            _state = State.Bounce;
            _t = 0;
            if (Load == null) return;
            Roll();   // every blow its own error
            float e = (float)Energy * _frac * (1 + Bias);
            _sinkTarget = Mathf.Min(e, maxJoules + 0.1f) * perJoule;
            _dropped = true;
            Sfx.Play("thud", 0.5f + 0.5f * _frac);
            if (_frac >= 0.08f) Pay(1, null);   // tried: a new apparatus stops calling
        }

        protected override double MaxError(GameState g) => g.ImpactError;
        protected override double Step(GameState g) => g.ImpactStep;

        /// The impact scale's divisions, every step joules (finer with the map's upgrades).
        protected override void Lay(double step) =>
            ScaleTicks.Build(transform, zeroMark, Outward(stake.position), perJoule, step, maxJoules, tickMat, game.eye, Marks, 0.018f);
    }
}
