using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The apparatus of an odd department (DESIGN.md 2б, «Крейзи-измерения»): the tongue, the pond, the cat, the mirror,
    /// the xylophone, the feather. It answers not with a number but with what it does, and it is built at runtime of
    /// simple shapes in chalk (Art3D) of the kit's materials, as the lab wakes up — the scene has only its table.
    public abstract class OddApparatus : Apparatus3D
    {
        /// Where the sample is put (made with the rest of it).
        protected Transform seat;
        protected override Transform Seat => seat;

        /// The kit's material it is made of that an older kit did not have yet: without it, nothing is built.
        protected abstract Material Needs(PersonaKit kit);
        /// Makes it of the kit's materials, under its own transform.
        protected abstract void Build(PersonaKit kit);

        void Awake()
        {
            var kit = game != null ? game.kit : null;
            if (kit == null || Needs(kit) == null) return;
            Build(kit);
        }
    }
}
