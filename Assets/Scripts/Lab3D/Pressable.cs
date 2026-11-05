using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// Something in the room a click works by itself, no sample needed: a xylophone's bar, the light switch.
    public abstract class Pressable : MonoBehaviour
    {
        public abstract string Hint { get; }
        public abstract void Press();
    }
}
