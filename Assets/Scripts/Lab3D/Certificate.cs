namespace ChalkPhysics.Lab3D
{
    /// A certificate hung on a wall (DESIGN.md 2б, «Грамоты»): a click takes it off, to hang it somewhere else (AwardWall).
    public class Certificate : Pressable
    {
        [System.NonSerialized] public AwardWall wall;
        [System.NonSerialized] public AwardData data;

        public override string Hint => wall != null && wall.CanTake ? "снять грамоту" : "";
        public override void Press() { if (wall != null && wall.CanTake) wall.Take(this); }
    }
}
