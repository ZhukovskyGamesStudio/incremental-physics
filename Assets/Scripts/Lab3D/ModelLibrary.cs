using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The Blender models the game makes at runtime (the customers, the odd departments' things, the player's hands and
    /// body), on the Lab3D game object. The scene builder (Lab3DBuilder) fills it with every FBX under Assets/Lab3D/Models; a model is
    /// found by its file name, e.g. "Persona_sailor" (Assets/Lab3D/Models/Customers/Persona_sailor.fbx).
    /// Conventions (Tools/Blender/kit.py makes the models by them):
    /// - Unity axes as they are: the model's origin is its pivot, it faces +z, a figure stands on y = 0.
    /// - Every mesh object is named "Part.colour" ("Coat.blue", "Skin.skin"): PersonaKit.Paint gives it the kit's
    ///   chalk material of that colour.
    /// - Empties come in as child transforms, markers by name ("Top" — the head-top height of a customer, "Seat" —
    ///   where a sample goes); a moving piece's parts are "Group-Part.colour" round the marker "Group" (Art3D.Pivot).
    public class ModelLibrary : MonoBehaviour
    {
        public List<GameObject> models = new List<GameObject>();

        static ModelLibrary _i;
        Dictionary<string, GameObject> _byName;

        /// The scene's library (the Lab3D game object's).
        public static ModelLibrary I => _i != null ? _i : (_i = FindAnyObjectByType<ModelLibrary>());

        void Awake() => _i = this;

        /// The model of that file name, or null.
        public GameObject Get(string name)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, GameObject>();
                foreach (var m in models) if (m != null) _byName[m.name] = m;
            }
            return _byName.TryGetValue(name, out var go) ? go : null;
        }

        /// A copy of the model under `parent`, at its origin, painted with the kit's chalk by its parts' names, casting
        /// no shadows (as the rest of what is made at runtime). Null, with an error, when there is no such model.
        public static GameObject Spawn(string name, Transform parent, PersonaKit kit)
        {
            var src = I != null ? I.Get(name) : null;
            if (src == null) { Debug.LogError("ModelLibrary: no model " + name); return null; }
            var go = Instantiate(src, parent, false);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            kit.Paint(go);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
