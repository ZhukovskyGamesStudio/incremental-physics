using UnityEngine;
using UnityEngine.Rendering;

namespace ChalkPhysics.Lab3D
{
    /// The mirror (DESIGN.md 2б, «Крейзи-измерения»): a tall mirror in a wooden frame standing on the table, a little
    /// pedestal in front of it. Put the sample on the pedestal and look: in the mirror is the room, the player in a lab
    /// coat — and the sample, or no sample (some things do not show in mirrors). The reflection is real: a camera at the
    /// eye's mirror image looks through the glass, and its picture is laid on the glass.
    public class Mirror3D : OddApparatus
    {
        public int lensRenderer = 1;    // the pipeline's 3D renderer: the chalk outlines are in the mirror too
        const float W = 0.5f, H = 0.72f, Bottom = 0.08f, Back = -0.2f;

        Transform _glass;
        Camera _cam;
        Mesh _mesh;
        readonly Vector3[] _corners = new Vector3[4];
        readonly Vector2[] _uv = new Vector2[4];
        Renderer[] _hidden = new Renderer[0];

        public override string PutVerb => "поставить перед зеркалом";

        protected override Material Needs(PersonaKit kit) => kit.line;

        protected override void Build(PersonaKit kit)
        {
            // the frame and its feet
            float cy = Bottom + H / 2;
            Art3D.Part(PrimitiveType.Cube, kit.brown, transform, new Vector3(0, cy, Back - 0.012f), new Vector3(W + 0.08f, H + 0.08f, 0.02f));
            Art3D.Part(PrimitiveType.Cube, kit.brown, transform, new Vector3(-W / 2 - 0.02f, Bottom / 2, Back), new Vector3(0.04f, Bottom, 0.18f));
            Art3D.Part(PrimitiveType.Cube, kit.brown, transform, new Vector3(W / 2 + 0.02f, Bottom / 2, Back), new Vector3(0.04f, Bottom, 0.18f));
            foreach (var (x, y, w, h) in new[] { (0f, Bottom - 0.02f, W + 0.08f, 0.04f), (0f, Bottom + H + 0.02f, W + 0.08f, 0.04f), (-W / 2 - 0.02f, cy, 0.04f, H + 0.08f), (W / 2 + 0.02f, cy, 0.04f, H + 0.08f) })
                Art3D.Part(PrimitiveType.Cube, kit.yellow, transform, new Vector3(x, y, Back + 0.004f), new Vector3(w, h, 0.012f));
            // the pedestal
            Art3D.Part(PrimitiveType.Cylinder, kit.white, transform, new Vector3(0, 0.06f, 0.12f), new Vector3(0.12f, 0.06f, 0.12f));
            Art3D.Part(PrimitiveType.Cylinder, kit.white, transform, new Vector3(0, 0.125f, 0.12f), new Vector3(0.16f, 0.006f, 0.16f));
            seat = Art3D.Empty("Seat", transform, new Vector3(0, 0.128f, 0.12f));
            Art3D.Grip("Grip", transform, new Vector3(0, 0.45f, 0), new Vector3(W + 0.1f, 0.9f, 0.5f));
            // the glass: its own quad (its corners' texture places are set every frame), on a layer the mirror's camera skips
            var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
            g.layer = 31;
            _glass = g.transform;
            _glass.SetParent(transform, false);
            _glass.localPosition = new Vector3(0, cy, Back);
            _mesh = new Mesh { name = "MirrorGlass" };
            _mesh.vertices = new[] { new Vector3(-W / 2, -H / 2, 0), new Vector3(W / 2, -H / 2, 0), new Vector3(W / 2, H / 2, 0), new Vector3(-W / 2, H / 2, 0) };
            _mesh.uv = new Vector2[4];
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };   // seen from +z, from the room
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            g.GetComponent<MeshFilter>().sharedMesh = _mesh;
            var rt = new RenderTexture(560, 800, 24) { name = "Mirror" };
            var mat = Art3D.Unlit(rt);
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var camGo = new GameObject("MirrorCamera");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.targetTexture = rt;
            _cam.cullingMask = ~((1 << 31) | (1 << Lab3DGame.ViewLayer));   // not the glass, not the hands only the eye sees
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.08f, 0.11f, 0.1f);
            _cam.enabled = false;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(_cam);
            data.SetRenderer(lensRenderer);
            data.renderPostProcessing = false;
        }

        void OnEnable() { RenderPipelineManager.beginCameraRendering += Begin; RenderPipelineManager.endCameraRendering += End; }
        void OnDisable() { RenderPipelineManager.beginCameraRendering -= Begin; RenderPipelineManager.endCameraRendering -= End; }

        /// The samples that do not show in a mirror are not drawn for its camera.
        void Begin(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam || game == null) return;
            int n = 0;
            foreach (var s in game.samples) if (s.gameObject.activeInHierarchy && s.data != null && !s.data.reflects) n++;
            if (_hidden.Length != n) _hidden = new Renderer[n];
            int i = 0;
            foreach (var s in game.samples)
                if (s.gameObject.activeInHierarchy && s.data != null && !s.data.reflects) { _hidden[i] = s.GetComponent<Renderer>(); _hidden[i++].forceRenderingOff = true; }
        }

        void End(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _cam) return;
            foreach (var r in _hidden) if (r != null) r.forceRenderingOff = false;
        }

        /// Placed and drawn at once (a picture taken from somewhere the camera was not a moment ago).
        public void Refresh()
        {
            LateUpdate();
            if (_cam != null && _cam.enabled) _cam.Render();
        }

        void LateUpdate()
        {
            if (_cam == null || game == null || game.eye == null) return;
            var eye = game.eye.transform.position;
            var n = transform.forward;   // the glass looks at the room, the table's front
            var p = _glass.position;
            float d = Vector3.Dot(eye - p, n);
            bool near = d > 0.05f && (eye - p).sqrMagnitude < 25 && !game.Lights.Dark;
            _cam.enabled = near;
            if (!near) return;
            // the camera at the eye's image behind the glass, looking out through it into the room
            var image = eye - 2 * d * n;
            _cam.transform.SetPositionAndRotation(image, Quaternion.LookRotation(n, Vector3.up));
            var right = _glass.right * (W / 2);
            var up = _glass.up * (H / 2);
            _corners[0] = p - right - up; _corners[1] = p + right - up; _corners[2] = p + right + up; _corners[3] = p - right + up;
            float l = float.MaxValue, r = float.MinValue, b = float.MaxValue, t = float.MinValue, near0 = 0;
            var local = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                local[i] = _cam.transform.InverseTransformPoint(_corners[i]);
                l = Mathf.Min(l, local[i].x); r = Mathf.Max(r, local[i].x);
                b = Mathf.Min(b, local[i].y); t = Mathf.Max(t, local[i].y);
                near0 += local[i].z / 4;
            }
            near0 = Mathf.Max(0.02f, near0);
            _cam.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near0, near0 + 20);
            for (int i = 0; i < 4; i++) _uv[i] = new Vector2((local[i].x - l) / (r - l), (local[i].y - b) / (t - b));
            _mesh.uv = _uv;
        }
    }
}
