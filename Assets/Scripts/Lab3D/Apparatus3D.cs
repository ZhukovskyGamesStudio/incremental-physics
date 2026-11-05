using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// An apparatus on a table of the 3D lab: a sample is put on it, and the player works it with the mouse button
    /// (lift, press, push) or it works by itself (the dynamometer). It fires its formula of the 2D game (Defs: dyna, fall,
    /// spring, pend) through the game, which keeps the score.
    public abstract class Apparatus3D : MonoBehaviour
    {
        public string id;               // its formula and station in Defs: dyna, fall, spring, pend
        public string formula;          // as written on the board and over the apparatus
        public GameObject station;      // the table with the apparatus: there once its formula is found on the map
        public Lab3DGame game;          // set by the builder too: a script reload in play mode keeps it
        /// Has it been worked once: a new apparatus is outlined until then (DESIGN.md, «Эффекты — из мира доски»).
        public bool Tried;
        public float labelHeight = 1;   // where its formula is written, over its base (the builder measures it)
        public int copy;                // 0 for the department's first; the rest are bought on the map
        /// A chalk helper has taken it on: it is carrying a sample to it or working it.
        [System.NonSerialized] public Helper3D helper;
        /// In the room today: its formula is found and this copy of it bought.
        public bool Active => station.activeInHierarchy && gameObject.activeSelf;

        public Sample3D Load { get; private set; }
        /// What the dot is on this frame (the game aims for the hands): a part of an apparatus, the tongue's zone…
        public static Collider Pointed;
        /// And where on it, in the world.
        public static Vector3 PointedAt;
        protected abstract Transform Seat { get; }
        protected virtual Vector3 SeatFor(Sample3D s) => Vector3.zero;
        /// Something is happening that a swap would break (the carriage is in the air, the sample is flying).
        public virtual bool Busy => false;
        /// Does the button do something to it, or is it only a place to put a sample (the dynamometer)?
        public virtual bool HasAction => false;
        /// What the button does now, as the hint under the dot says it: one word or two.
        public virtual string Verb => "";
        public virtual string PutVerb => "положить";

        /// Puts a sample on; the one that was there is returned (null if none).
        public Sample3D Put(Sample3D s)
        {
            var old = Take();
            Load = s;
            s.MoveTo(Seat, SeatFor(s), Sample3D.Where.Apparatus);
            OnPut(s);
            return old;
        }

        public Sample3D Take()
        {
            if (Load == null) return null;
            var s = Load;
            Load = null;
            s.Driven = false;
            OnTaken(s);
            return s;
        }

        protected virtual void OnPut(Sample3D s) { }
        protected virtual void OnTaken(Sample3D s) { }
        public virtual void Down() { }
        public virtual void Up() { }

        /// A letter's value; 1 in a picture in edit mode, where there is no game going on.
        protected double V(string letter) => Application.isPlaying && game != null ? game.G.V(letter) : 1;
        /// The mass in the formulas: the letter m times the sample's own mass (as in the 2D game).
        protected double M => V("m") * Load.mass;
        protected double G => V("g");

        /// Fires the formula: its value on this sample times `share`, how well it was done (DESIGN.md 2а, «Буквы —
        /// потолок, мини-игра — доля»). The upgrades from the map, the rank and the halves for an unweighed or a
        /// weighed-again sample are the game's own (GameState.YieldOf). `line` is written in the air: the formula's value.
        protected void Pay(double share, string line, Vector3? at = null)
        {
            Tried = true;
            game.Fire(this, share, line, at ?? Load.transform.position);
        }
    }
}
