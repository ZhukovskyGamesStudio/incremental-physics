using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The dynamometer (DESIGN.md 2б, «Листки-заказы»): a sample hung on the hook stretches the spring by its weight,
    /// with an overshoot that dies down in about three seconds, and the pointer stops against the scale. No number is
    /// written anywhere: the weight is read off the scale by eye. The scale's divisions are 1 N to start with, 0.5 N bought
    /// on the map; finer than that they would run together, so the finer ones (0.2, 0.1 N) come with a loupe: a little
    /// window beside the scale that follows the pointer and shows it and the divisions round it 2.5 or 4 times bigger.
    /// The spring is not quite true: every hanging it is off by up to 10% (5, 2, 0.5% bought).
    public class Dynamometer3D : MeasuringApparatus
    {
        public Transform spring;        // pivot at its top, the mesh hangs down `rest` metres
        public Transform hook;          // pivot at its eye
        public float rest = 0.16f;
        public float perUnit = 0.04f;   // metres of stretch a newton
        public float maxForce = 7f;     // where the scale ends: a heavier pull stops against it
        public float hz = 1.1f;
        public float damping = 0.07f;   // a soft spring: it bobs about nine seconds before it stands still
        /// The J of the hook: a sample's top sits this far below the hook's eye.
        public float hookDrop = 0.058f;

        protected override Transform Seat => hook;
        protected override Vector3 SeatFor(Sample3D s) => Vector3.down * (hookDrop + s.Size);
        public override string PutVerb => "повесить";

        /// The sample's true weight, F = mg.
        public double Force => Load != null ? Load.mass * G : 0;
        /// What the pointer shows right now, in newtons.
        public override double Reading => _x / perUnit;
        /// The pointer has stopped.
        public override bool Settled => Load != null && _calm >= 0.35f;
        /// How long the pointer has been still (the check takes its picture just before the bot reads it).
        public float Calm => Load != null ? _calm : 0;

        float _x, _v, _calm;
        Vector3 _top;

        void Awake() => _top = hook.localPosition + Vector3.up * rest;

        protected override void OnPut(Sample3D s)
        {
            _calm = 0;
            // one twang, lower the heavier the sample: the spring stretches more
            if (Application.isPlaying) Sfx.Play("spring", 0.55f, Mathf.Lerp(1.3f, 0.75f, Mathf.Clamp01((float)Force / maxForce)), 0);
            Roll();   // a new error every hanging: hang it again and the pointer stops elsewhere
        }

        protected override double MaxError(GameState g) => g.SpringError;
        protected override double Step(GameState g) => g.ScaleStep;

        /// For pictures in edit mode: hang it and be at rest at once.
        public void HangAtRest(Sample3D s)
        {
            if (_top == Vector3.zero) Awake();
            Put(s);
            _x = Target;
            _v = 0;
            Pose();
            s.Snap();
        }

        /// Where the pointer comes to rest: the weight, off by this hanging's error, as far as the scale goes.
        float Target => Load == null ? 0 : Mathf.Min((float)Force * (1 + Bias), maxForce + 0.15f) * perUnit;

        void Update()
        {
            Ticks();
            Loupe();
            float target = Target;
            Settle(ref _x, ref _v, target, hz, damping, Time.deltaTime);
            _x = Mathf.Max(_x, -0.02f);
            if (Load != null)
            {   // the bottom of the sample stops on the base (3 cm thick) and bounces back off it
                float lowest = _top.y - rest - hookDrop - Load.Size - 0.033f;
                if (_x > lowest) { _x = lowest; if (_v > 0) _v = -_v * 0.25f; }
            }
            Pose();
            bool still = Load != null && AtRest(_x, _v, target);
            _calm = still ? _calm + Time.deltaTime : 0;
        }

        void Pose()
        {
            spring.localScale = new Vector3(1, (rest + _x) / rest, 1);
            hook.localPosition = _top + Vector3.down * (rest + _x);
        }

        // ------------------------------------------------------------------ the loupe

        public int lensRenderer = 1;    // the pipeline's 3D renderer (the chalk outlines): the lens draws with it too
        Camera _lensCam;
        Transform _lens, _slider, _arm;

        /// With the fine divisions (0.2 N and finer) a magnifying glass on a slider by the scale's edge: a round lens in a
        /// metal rim, in front of the pointer, that moves up and down with it. Through it that very spot of the scale —
        /// the pointer's tip, the divisions, the numbers — is 2.5 or 4 times bigger (a camera behind the glass draws it).
        void Loupe()
        {
            if (game == null || zeroMark == null || !Application.isPlaying) return;
            double step = game.G.ScaleStep;
            float zoom = step < 0.15 ? 3f : step < 0.35 ? 2f : 0;
            if (zoom <= 0) { if (_lens != null) _lens.gameObject.SetActive(false); return; }
            var plate = zeroMark.bounds.center;
            // the plate faces the room along the apparatus' depth (the spring hangs in front of it); its divisions and
            // numbers run off sideways, away from the spring (as the ticks are laid)
            var toViewer = transform.forward * Mathf.Sign(Vector3.Dot(spring.position - plate, transform.forward) + 1e-5f);
            var outward = Outward(spring.position, 1e-5f);
            const float r = 0.05f, front = 0.075f;
            const int layer = 31;   // the glass and its rim are not in their own picture
            if (_lens == null) MakeLens(r, layer);
            _lens.gameObject.SetActive(true);
            // the lens: in front of the pointer's tip (where it meets the divisions), a little to the numbers' side
            float pointer = zeroMark.bounds.center.y - _x;
            var b = zeroMark.bounds;
            float half = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z))));
            var look = new Vector3(plate.x, pointer, plate.z) + outward * (half * 0.6f);   // the spot it magnifies
            _lens.position = look + toViewer * front;
            _lens.rotation = Quaternion.LookRotation(-toViewer, Vector3.up);
            // the camera a little behind the glass, seeing a span `zoom` times smaller than the glass
            float dist = front + 0.06f;
            _lensCam.transform.position = look + toViewer * dist;
            _lensCam.transform.rotation = _lens.rotation;
            _lensCam.fieldOfView = 2 * Mathf.Atan(r / zoom / dist) * Mathf.Rad2Deg;
            // the slider rides the plate's outer edge; the arm reaches from it to the rim
            var edge = new Vector3(plate.x, pointer, plate.z) + outward * (half + 0.012f) + toViewer * 0.004f;
            _slider.position = edge;
            _slider.rotation = _lens.rotation;
            _slider.localScale = new Vector3(0.014f, 0.022f, 0.012f);
            var rimPoint = _lens.position + outward * r * 0.95f;
            _arm.position = (edge + rimPoint) / 2;
            _arm.rotation = Quaternion.LookRotation(rimPoint - edge, Vector3.up);
            _arm.localScale = new Vector3(0.005f, 0.005f, (rimPoint - edge).magnitude);
        }

        void MakeLens(float r, int layer)
        {
            var rt = new RenderTexture(512, 512, 16) { name = "Loupe" };
            _lens = new GameObject("Loupe").transform;
            _lens.SetParent(transform, true);
            var camGo = new GameObject("LoupeCamera");
            camGo.transform.SetParent(_lens, false);
            _lensCam = camGo.AddComponent<Camera>();
            _lensCam.targetTexture = rt;
            _lensCam.nearClipPlane = 0.01f;
            _lensCam.farClipPlane = 2f;
            _lensCam.cullingMask = ~((1 << layer) | (1 << Lab3DGame.BodyLayer));
            _lensCam.clearFlags = CameraClearFlags.SolidColor;
            _lensCam.backgroundColor = new Color(0.105f, 0.14f, 0.125f);
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_lensCam).SetRenderer(lensRenderer);
            // the glass: a disc, its picture mapped round it
            var disc = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
            disc.layer = layer;
            disc.transform.SetParent(_lens, false);
            disc.GetComponent<MeshFilter>().sharedMesh = Disc(r, 40);
            disc.GetComponent<MeshRenderer>().sharedMaterial = Art3D.Unlit(rt);
            // the rim, and a gleam on the glass
            var metal = Art3D.Unlit(new Color(0.72f, 0.8f, 0.88f));
            var rim = Ring("Rim", layer, 0.007f, metal);
            rim.loop = true;
            rim.positionCount = 40;
            for (int i = 0; i < 40; i++) { float a = i / 40f * Mathf.PI * 2; rim.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -0.002f)); }
            var gleam = Ring("Gleam", layer, 0.003f, Art3D.Unlit(Color.white));
            gleam.positionCount = 8;
            for (int i = 0; i < 8; i++) { float a = Mathf.Lerp(1.9f, 2.7f, i / 7f); gleam.SetPosition(i, new Vector3(Mathf.Cos(a) * r * 0.78f, Mathf.Sin(a) * r * 0.78f, -0.003f)); }
            // the slider on the scale's edge and the arm from it to the rim
            _slider = Bar("Slider", layer, metal);
            _arm = Bar("Arm", layer, metal);
            // a bright tip on the pointer, where it meets the divisions: it shows through the glass (dark metal does not)
            var plate = zeroMark.bounds.center;
            var outward = Outward(spring.position, 1e-5f);
            float half = Mathf.Abs(Vector3.Dot(zeroMark.bounds.extents, new Vector3(Mathf.Abs(outward.x), 0, Mathf.Abs(outward.z))));
            var tipAt = new Vector3(plate.x, plate.y - _x, plate.z) - outward * (half + 0.006f);
            var tip = Art3D.Bare(PrimitiveType.Cube, "PointerTip");
            tip.transform.position = tipAt;
            tip.transform.rotation = transform.rotation;
            tip.transform.SetParent(hook, true);
            tip.transform.localScale = Vector3.one;
            tip.transform.localScale = new Vector3(0.012f / hook.lossyScale.x, 0.0025f / hook.lossyScale.y, 0.004f / hook.lossyScale.z);
            tip.GetComponent<Renderer>().sharedMaterial = Art3D.Unlit(ChalkTex.Yellow);
        }

        LineRenderer Ring(string name, int layer, float width, Material m)
        {
            var l = Art3D.LocalLine(name, _lens, m, width);
            l.gameObject.layer = layer;
            return l;
        }

        Transform Bar(string name, int layer, Material m)
        {
            var go = Art3D.Bare(PrimitiveType.Cube, name);
            go.layer = layer;
            go.transform.SetParent(_lens, false);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        /// A flat disc facing -z (towards whoever looks at it from in front), its texture mapped round it.
        static Mesh Disc(float r, int n)
        {
            var v = new Vector3[n + 1];
            var uv = new Vector2[n + 1];
            var t = new int[n * 3];
            v[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2;
                v[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                uv[i + 1] = new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f);
                t[i * 3] = 0;
                t[i * 3 + 1] = (i + 1) % n + 1;   // clockwise seen from -z
                t[i * 3 + 2] = i + 1;
            }
            var m = new Mesh { vertices = v, uv = uv, triangles = t };
            m.RecalculateNormals();
            return m;
        }

        /// The scale's divisions, laid down from the zero tick every step newtons (the map's upgrades make them finer):
        /// along the plate's width, away from the spring, so that the numbers stand beside the plate, not behind it.
        protected override void Lay(double step) =>
            ScaleTicks.Build(transform, zeroMark, Outward(spring.position), perUnit, step, maxForce, tickMat, game.eye, Marks);
    }
}
