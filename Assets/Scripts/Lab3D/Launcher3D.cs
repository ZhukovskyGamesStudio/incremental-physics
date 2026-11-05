using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The spring, E = kx²/2: hold the button and the plate goes down, squeezing the spring; let go and the sample
    /// jumps up along the ruler and lands back on the plate, and the landing pays. Squeezed all the way it pays the
    /// whole kx²/2, barely squeezed half: the jump is as high as the squeeze is deep (squared, like the energy).
    public class Launcher3D : Apparatus3D
    {
        public Transform plate;         // its top is where the sample stands
        public Transform spring;        // stands on its bottom, `length` metres tall
        public float length = 0.18f;
        public float squeeze = 0.09f;   // the deepest the plate goes
        public float squeezeTime = 0.8f;
        public float jumpG = 6f;

        enum State { Rest, Squeeze, Fly }
        State _state;
        float _c, _shot, _t, _v0, _back;
        Vector3 _rest;

        protected override Transform Seat => plate;
        public override bool HasAction => true;
        public override bool Busy => _state != State.Rest;
        public override string Verb => "сжать: держи";

        void Awake() => _rest = plate.localPosition;

        public override void Down()
        {
            if (Load != null && _state == State.Rest) { _state = State.Squeeze; _c = 0; }
        }

        public override void Up()
        {
            if (_state != State.Squeeze) return;
            _shot = _c;
            _back = _c;
            _t = 0;
            if (_c < 0.1f) { _state = State.Rest; return; }
            float height = 0.06f + 0.5f * _c * _c;
            _v0 = Mathf.Sqrt(2 * jumpG * height);
            _state = State.Fly;
            Load.Driven = true;
        }

        protected override void OnTaken(Sample3D s) => _state = State.Rest;

        void Update()
        {
            float dt = Time.deltaTime;
            float press = 0;
            switch (_state)
            {
                case State.Squeeze:
                    _c = Mathf.Min(1, _c + dt / squeezeTime);
                    press = _c;
                    break;
                case State.Fly:
                    _t += dt;
                    float y = _v0 * _t - jumpG * _t * _t / 2;
                    if (y <= 0 && _t > 0.05f) { Land(); break; }
                    Load.transform.position = plate.parent.TransformPoint(_rest) + Vector3.up * Mathf.Max(0, y);
                    break;
            }
            // the plate springs back after a shot: a quick wobble that dies away
            _back = _state == State.Squeeze ? _c : _back * Mathf.Exp(-9 * dt);
            float wobble = _state == State.Squeeze ? press : _back * Mathf.Cos(_t * 30);
            plate.localPosition = _rest + Vector3.down * (squeeze * wobble);
            spring.localScale = new Vector3(1, (length - squeeze * wobble) / length, 1);
        }

        void Land()
        {
            _state = State.Rest;
            Load.Driven = false;
            Load.Snap();
            double x = V("x");
            Pay(0.5 + 0.5 * _shot * _shot, "E = kx²/2 = " + GameState.Fmt(V("k") * x * x / 2));
        }
    }
}
