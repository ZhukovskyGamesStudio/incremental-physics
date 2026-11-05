using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A sample in the 3D lab: a chalk cube whose size is its volume and whose hatching is its density (DESIGN.md,
    /// «Образцы»). It glides to wherever it is put: its slot in the crate, the hand, an apparatus. While an apparatus
    /// throws it about (the spring launches it), the apparatus moves it itself.
    public class Sample3D : MonoBehaviour
    {
        public float mass = 1;
        public float volume = 1;
        /// Its shape (SampleShapes: a cube, a brick, a ball, a weight, a stone, an ingot), as its order tells; a stone's
        /// lumps are one of a few.
        public string shape = "cube";
        public int variant;
        float _unitVolume = 1;
        Bounds _unit = new Bounds(new Vector3(0, 0.5f, 0), Vector3.one);
        /// Metres a unit of its mesh: a litre is a thousandth of a cubic metre (a ten-centimetre cube, DESIGN.md 2б,
        /// «Ванночка»), so what the bath says of it, it really displaces, and a bigger sample is plainly bigger.
        public float Scale => Mathf.Pow(volume * 0.001f / _unitVolume, 1f / 3f);
        /// Its height in metres (it stands on its base: hung by its top, it is this far under the hook).
        public float Size => Scale * _unit.size.y;
        /// Its width, height and depth in metres.
        public Vector3 Extent => _unit.size * Scale;

        public Transform home;          // its slot in the crate
        /// The specimen of the 2D game behind it: its mass and volume, gold, what has been measured on it.
        [System.NonSerialized] public Sample data;
        /// A second weighing gives half the ideas; an unweighed sample counts half elsewhere.
        public bool Weighed => data != null && data.knowM;
        public enum Where { Crate, Hand, Apparatus, Helper, Box, Free }
        /// The order it came in with (DESIGN.md 2б, «Заказы»), and the tag over it that shows the order.
        [System.NonSerialized] public Order order;
        [System.NonSerialized] public OrderTag tag;
        public Where At = Where.Crate;
        /// An apparatus moves it this frame; it does not glide on its own.
        [System.NonSerialized] public bool Driven;

        Vector3 _local;
        Quaternion _rot = Quaternion.identity;
        Vector3? _via;

        /// On its way to where it is put, first through this point (in the world): over the rim of the bath or the box, so
        /// that it goes in from above and not through a wall. Set after (or before) MoveTo; it stays until it is passed.
        public void Via(Vector3 world) => _via = world;

        public void Shape()
        {
            if (Application.isPlaying)
            {   // in play only: the meshes are made at runtime and are not to be saved into the scene
                var (mesh, vol) = SampleShapes.Get(shape, variant);
                GetComponent<MeshFilter>().sharedMesh = mesh;
                _unitVolume = vol;
                _unit = mesh.bounds;
                var col = GetComponent<BoxCollider>();
                if (col != null) { col.center = _unit.center; col.size = _unit.size; }
            }
            transform.localScale = Vector3.one * Scale;
        }

        /// Moves the sample under `parent`, keeping where it is now, and lets it glide to `local`.
        public void MoveTo(Transform parent, Vector3 local, Where at)
        {
            Body(false);
            transform.SetParent(parent, true);
            _local = local;
            _rot = Quaternion.identity;
            At = at;
            Driven = false;
        }

        /// Turned as it lies (on its side in the box), after MoveTo.
        public void Turn(Quaternion r) => _rot = r;

        public void Snap()
        {
            transform.localPosition = _local;
            transform.localRotation = _rot;
        }

        public void GoHome() => MoveTo(home, Vector3.zero, Where.Crate);

        /// Laid down anywhere (DESIGN.md 2б, «Каморка»): on a table, the floor, a box, another sample — it falls and lies
        /// where it falls, and samples stack into a tower (that may topple).
        public void Drop(Vector3 at, Quaternion rot)
        {
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(at, rot);
            _via = null;
            At = Where.Free;
            Driven = false;
            Body(true);
        }

        /// The sample's body: dynamic when it lies free, carried along (kinematic) everywhere else.
        void Body(bool free)
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;
            // carried, the body must not be smoothed: interpolation would leave it a frame behind whatever carries it (it
            // sank through the dispatch box's bottom going up the pipe)
            if (!free) { if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; } rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; return; }
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        /// Comes into being in its slot: it swells from nothing a little past its size and settles.
        public void Appear() => StartCoroutine(Pop(true));
        /// Goes away from its slot (taken back by its owner): it shrinks to nothing and is put away.
        public void Vanish() => StartCoroutine(Pop(false));

        System.Collections.IEnumerator Pop(bool appear)
        {
            if (!appear) for (float t = 0; t < 0.35f && At == Where.Crate; t += Time.deltaTime) yield return null;   // done gliding home
            for (float k = 0; k < 1; k += Time.deltaTime / (appear ? 0.45f : 0.3f))
            {
                float x = appear ? Mathf.Max(0.01f, 1 + Mathf.Sin(k * Mathf.PI * 1.25f) * 0.2f * (1 - k) - Mathf.Pow(1 - k, 3)) : 1 - k;
                transform.localScale = Vector3.one * Scale * x;
                yield return null;
            }
            transform.localScale = Vector3.one * Scale;
            if (!appear) gameObject.SetActive(false);
        }

        void Update()
        {
            if (Driven || At == Where.Free) return;   // carried by an apparatus, or lying free where physics has it
            float k = 1 - Mathf.Exp(-Time.deltaTime * 14);
            if (_via.HasValue)
            {
                var v = _via.Value;
                transform.position = Vector3.MoveTowards(Vector3.Lerp(transform.position, v, k * 1.4f), v, Time.deltaTime * 0.4f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, _rot, k);
                if ((transform.position - v).sqrMagnitude < 0.0001f) _via = null;
                return;
            }
            transform.localPosition = Vector3.Lerp(transform.localPosition, _local, k);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _rot, k);
        }

        void Start()
        {
            if (home != null && At == Where.Crate) { MoveTo(home, Vector3.zero, Where.Crate); Snap(); }
        }
    }
}
