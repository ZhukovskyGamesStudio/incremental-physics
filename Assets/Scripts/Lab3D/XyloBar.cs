using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// One bar of the xylophone: a click plays its note.
    public class XyloBar : Pressable
    {
        public Xylo3D xylo;
        public int index;
        public override string Hint => "сыграть «" + Oddity.Options("note")[index] + "»";
        public override void Press() => xylo.Bar(index);
    }
}
