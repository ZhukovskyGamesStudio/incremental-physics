using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The inclined plane, μ = tg α (DESIGN.md 2б, «Наклонная плоскость»): a board hinged at one end, a protractor by the
    /// hinge. The sample lies on the board's far end; a click starts the screw jack under the board and it rises by
    /// itself, slowly — two and a half degrees a second, so the hands are free meanwhile (DESIGN.md 2б, «Пиццерия»); at
    /// the angle where
    /// the sample's friction gives way it slides down to the stop at the hinge, and the board stays where it was. The
    /// angle is read where the needle along the board crosses the protractor — by eye, no number is written. The
    /// divisions are 5° to start with (2° bought on the map); the board is not quite even: every time the angle is off
    /// by up to 10% of the sample's friction (5, 2% bought). The friction itself is the tangent of the angle.
    public class Incline3D : MeasuringApparatus
    {
        public Transform pivot;         // the hinge: the board turns about its z
        public Transform seat;          // on the board's far end
        public Transform stop;          // where a sample comes to rest against the stop by the hinge
        public float radius = 0.2f;     // of the protractor
        public float maxAngle = 70;
        public float speed = 2.5f;      // degrees a second as the screw jack lifts the board

        protected override Transform Seat => seat;
        public override string PutVerb => "положить на доску";
        public override bool HasAction => Load != null && !_slid;   // once it has slid, the angle stands: nothing to do but read it
        public override string Verb => _raising ? "остановить подъёмник" : "включить подъёмник";
        public override bool Busy => _sliding;
        /// Where the red mark on the protractor stands — the angle the sample slid at — or, before it, the board's angle
        /// (the check's bot reads it as a player reads the protractor). Its zero mark is the protractor's 0°, at its radius.
        public override double Reading => _slid ? _slipAt : _angle;
        /// The sample has slid and lies still: the angle stands.
        public override bool Settled => Load != null && _slid && !_sliding;
        /// The sample's true angle of sliding: the friction's arctangent.
        public double TrueAngle => Load != null && Load.data != null ? System.Math.Atan(Load.data.mu) * 180 / System.Math.PI : 0;

        float _angle, _slipAt;
        bool _raising, _slid, _sliding, _lowering;

        protected override void OnPut(Sample3D s)
        {
            Roll();
            double mu = s.data != null ? s.data.mu : 0.5;
            _slipAt = Mathf.Min(maxAngle - 1, (float)(System.Math.Atan(mu * (1 + Bias)) * 180 / System.Math.PI));
            _slid = false;
            _lowering = _angle > 0.01f;   // a raised board comes down first
            if (_mark != null) _mark.gameObject.SetActive(false);
        }

        Transform _mark;

        protected override double MaxError(GameState g) => g.SlipError;
        protected override double Step(GameState g) => g.AngleStep;

        /// A red chalk mark on the protractor where the needle stood when the sample gave way: the board goes on rising
        /// while the button is held, the mark stays to be read.
        void Mark(float angle)
        {
            if (_mark == null)
            {
                var go = Art3D.Bare(PrimitiveType.Cube, "SlipMark");
                go.transform.SetParent(transform, false);
                var m = new Material(tickMat) { color = new Color(1f, 0.42f, 0.4f) };
                go.GetComponent<Renderer>().sharedMaterial = m;
                _mark = go.transform;
            }
            _mark.gameObject.SetActive(true);
            float rad = angle * Mathf.Deg2Rad;
            var dir = new Vector3(-Mathf.Cos(rad), Mathf.Sin(rad), 0);
            var centre = pivot.localPosition;
            _mark.localPosition = new Vector3(centre.x, centre.y, 0) + dir * (radius - 0.006f) + Vector3.forward * (zeroMark.transform.localPosition.z + 0.003f);
            _mark.localRotation = Quaternion.Euler(0, 0, 180 - angle);
            _mark.localScale = new Vector3(0.05f, 0.006f, 0.006f);
            Sfx.Play("tick", 0.5f, 1.6f);
        }

        protected override void OnTaken(Sample3D s)
        {
            _raising = false;
            _slid = false;
            _sliding = false;
            _lowering = true;
            if (_mark != null) _mark.gameObject.SetActive(false);
            Sfx.Play("squeak", 0.3f, 0.8f);
        }

        /// A click starts the jack, another stops it.
        public override void Down()
        {
            if (Load == null || _lowering) return;
            _raising = !_raising;
            Sfx.Play("tick", 0.5f, _raising ? 1.1f : 0.8f);
        }

        void Update()
        {
            Ticks();
            if (_lowering)
            {
                _angle = Mathf.MoveTowards(_angle, 0, 60 * Time.deltaTime);
                if (_angle <= 0.001f) _lowering = false;
            }
            else if (_raising && Load != null)
            {   // the jack lifts it until the sample gives way, and stops right there: the board stands at that angle
                _angle = Mathf.Min(maxAngle, _angle + speed * Time.deltaTime);
                if (Random.value < Time.deltaTime * 3) Sfx.Play("squeak", 0.12f, 1.2f + _angle / 100f);
                if (!_slid && _angle >= _slipAt) { _angle = _slipAt; _raising = false; Mark(_slipAt); StartCoroutine(Slide()); }
                else if (_angle >= maxAngle) { _raising = false; Sfx.Play("tick", 0.4f, 0.8f); }
            }
            pivot.localRotation = Quaternion.Euler(0, 0, -_angle);
        }

        /// The sample gives way and slides down the board, faster and faster, to the stop at the hinge.
        System.Collections.IEnumerator Slide()
        {
            _slid = true;
            _sliding = true;
            var s = Load;
            s.Driven = true;
            var from = seat.InverseTransformPoint(s.transform.position);
            var to = seat.InverseTransformPoint(stop.position);
            Sfx.Play("whoosh", 0.4f, 0.7f);
            for (float k = 0; k < 1 && Load == s; k += Time.deltaTime / 0.45f)
            {
                s.transform.position = seat.TransformPoint(Vector3.Lerp(from, to, k * k));
                yield return null;
            }
            if (Load == s)
            {
                s.MoveTo(stop, Vector3.zero, Sample3D.Where.Apparatus);
                s.Snap();
                Sfx.Play("thud", 0.6f, 1.1f);
            }
            _sliding = false;
            Tried = true;
        }

        /// The protractor's divisions: from 0° up every step degrees, numbered every ten, along an arc round the hinge.
        protected override void Lay(double step)
        {
            foreach (var t in Marks) Destroy(t);
            Marks.Clear();
            var centre = pivot.localPosition;
            float z = zeroMark.transform.localPosition.z;
            for (double a = step; a <= maxAngle + 1e-6; a += step)
            {
                bool ten = System.Math.Abs(a / 10 - System.Math.Round(a / 10)) < 1e-6;
                bool five = !ten && System.Math.Abs(a / 5 - System.Math.Round(a / 5)) < 1e-6;
                float len = ten ? 0.03f : five ? 0.02f : 0.012f;
                float rad = (float)a * Mathf.Deg2Rad;
                var dir = new Vector3(-Mathf.Cos(rad), Mathf.Sin(rad), 0);   // the board rises to the viewer's right
                var tick = Art3D.Bare(PrimitiveType.Cube, "Tick " + a.ToString("0"));
                tick.transform.SetParent(transform, false);
                tick.transform.localPosition = new Vector3(centre.x, centre.y, 0) + dir * (radius + len / 2) + Vector3.forward * z;
                tick.transform.localRotation = Quaternion.Euler(0, 0, 180 - (float)a);
                tick.transform.localScale = new Vector3(len, 0.003f, 0.003f);
                tick.GetComponent<Renderer>().sharedMaterial = tickMat;
                Marks.Add(tick);
                if (ten)
                {
                    var at = new Vector3(centre.x, centre.y, 0) + dir * (radius + 0.05f) + Vector3.forward * z;
                    Marks.Add(Chalk3DText.Label(transform, at, a.ToString("0"), ChalkTex.White, 0.018f, game.eye).gameObject);
                }
            }
        }
    }
}
