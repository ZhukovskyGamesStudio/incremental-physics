using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// An apparatus that measures something about the sample and shows it on a scale, read by eye (DESIGN.md 2б,
    /// «Листки-заказы»): the dynamometer, the drop shelf, the bath, the inclined plane. No number is written anywhere. The
    /// scale's divisions are laid at runtime from its zero mark, finer as the map's upgrades buy them; and the apparatus
    /// is not quite true: every measurement is off by a share of the true value, rolled anew up to its error (smaller
    /// with the upgrades).
    public abstract class MeasuringApparatus : Apparatus3D
    {
        public Renderer zeroMark;       // the scale's zero: its divisions are laid from it
        public Material tickMat;        // bright chalk on the scale's dull one

        /// What the scale shows right now (the check's bot reads it as a player reads the scale).
        public abstract double Reading { get; }
        /// The measurement has stopped moving: it can be read.
        public abstract bool Settled { get; }

        /// This measurement's error, a share of the true value.
        protected float Bias;
        /// The scale's divisions and their numbers, as laid now.
        protected readonly List<GameObject> Marks = new List<GameObject>();
        double _step = -1;

        /// The largest error now, a share (the game's rules with the map's upgrades).
        protected abstract double MaxError(GameState g);
        /// The scale's division now, in its unit.
        protected abstract double Step(GameState g);
        /// Lays the scale's divisions every `step` (into Marks).
        protected abstract void Lay(double step);
        /// The apparatus is ready for its divisions (the bath, once its water is poured).
        protected virtual bool CanLay => true;

        /// A new error for this measurement: measure again and it comes out elsewhere.
        protected void Roll()
        {
            float err = game != null && Application.isPlaying ? (float)MaxError(game.G) : 0;
            Bias = Random.Range(-err, err);
        }

        /// The divisions laid again when the map has made them finer (every frame, from Update).
        protected void Ticks()
        {
            if (game == null || zeroMark == null || !Application.isPlaying || !CanLay) return;
            double step = Step(game.G);
            if (System.Math.Abs(step - _step) < 1e-9) return;
            _step = step;
            Lay(step);
        }

        /// Sideways along the scale's plate, away from `from` (the spring, the stake): where its numbers stand.
        protected Vector3 Outward(Vector3 from, float lean = 0) =>
            transform.right * Mathf.Sign(Vector3.Dot(zeroMark.bounds.center - from, transform.right) + lean);

        /// A damped spring, `x` going to `target` at `hz` with its overshoot, the frame in small steps: a slow or
        /// fast-forwarded frame must not slow it down (the dynamometer's pointer, the measuring cylinder's water).
        protected static void Settle(ref float x, ref float v, float target, float hz, float damping, float deltaTime)
        {
            float w = 2 * Mathf.PI * hz;
            int n = Mathf.Clamp(Mathf.CeilToInt(deltaTime * 240), 1, 60);
            float dt = deltaTime / n;
            for (int i = 0; i < n; i++)
            {
                float a = -w * w * (x - target) - 2 * damping * w * v;
                v += a * dt;
                x += v * dt;
            }
        }

        /// Close enough to `target` and hardly moving: still.
        protected static bool AtRest(float x, float v, float target) => Mathf.Abs(x - target) < 0.0015f && Mathf.Abs(v) < 0.01f;
    }
}
