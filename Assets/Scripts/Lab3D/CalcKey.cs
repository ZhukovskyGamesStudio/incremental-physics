using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A key of the calculator: pressed with the dot.
    public class CalcKey : MonoBehaviour
    {
        public string key;
        [System.NonSerialized] public Calculator3D calc;
    }
}
