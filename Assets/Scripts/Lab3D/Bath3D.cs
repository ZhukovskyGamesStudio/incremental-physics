using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The bath, V = Sh (DESIGN.md 2б, «Ванночка»): an overflow can and a measuring cylinder, as in a school lab. The can
    /// is big enough for any sample (a stone, a brick) and full of water up to its spout. A sample goes in from above,
    /// over the rim; the water it pushes aside pours out of the spout in a stream into the measuring cylinder beside it,
    /// and the cylinder's water stands at the sample's volume against its scale: a litre is ten centimetres there, so
    /// the divisions are big. Taken out, the cylinder drains back into the can by its hose. The can is not quite true:
    /// every time some 10% more or less pours out (drops left on the spout). No number is written: it is read by eye.
    /// The water in the can is simulated with hundreds of little balls (never drawn): the surface laid over them waves
    /// and sloshes; a splash is a few strokes of chalk flying off.
    public class Bath3D : MeasuringApparatus
    {
        public Transform seat;          // the middle of the can's bottom: the sample's place
        public Transform cylBase;       // the middle of the measuring cylinder's inside bottom (its empty mark is the zero)
        public Transform spoutTip;      // where the stream leaves the can
        public Material bodyMat, edgeMat;
        public Vector3 inner = new Vector3(0.34f, 0.3f, 0.26f);   // the inside of the can: width, height, depth
        public float spout = 0.2f;      // the can's water up to the spout
        public float cylRadius = 0.0564f;   // inside: a hundred square centimetres, ten centimetres a litre
        public float cylHeight = 0.4f;
        public float radius = 0.013f;   // of a ball of water
        public float maxLitres = 3.5f;

        protected override Transform Seat => seat;
        public override string PutVerb => "опустить в воду";
        /// The sample's true volume.
        public double Volume => Load != null ? Load.volume : 0;
        /// The water in the cylinder, in litres, as it stands now.
        public override double Reading => _cylShown / PerCyl;
        public override bool Settled => Load != null && InPlace && _calm >= 0.4f;
        public float Calm => Load != null && InPlace ? _calm : 0;
        float PerCyl => 0.001f / (Mathf.PI * cylRadius * cylRadius);   // metres a litre in the cylinder
        float CanArea => inner.x * inner.z;

        float _cyl;                     // litres in the cylinder
        float _cylShown, _cylV;         // its surface as drawn (a little sway), in metres
        float _calm, _tankLevel, _wobble, _rate;
        /// How fast the spout lets the water out: litres a second for a litre standing over it (and as the root of it).
        public float pourRate = 0.22f;
        /// The water is still running out of the spout (the check waits for it).
        public bool Pouring => _rate > 0;
        bool _wasIn;

        // the balls of water in the can (the tank's own space: y up from the seat)
        Vector3[] _p, _prev, _vel;
        int[] _cellStart, _cellCount, _order, _cellOf, _fillAt;
        int _nx, _ny, _nz;
        float _cell;
        bool _ready;

        // what is drawn: the can's water, the cylinder's, the stream, the chalk splashes
        Mesh _body;
        Vector3[] _bodyV;
        LineRenderer _edge, _cylEdge, _stream;
        Vector3[] _edgeP;
        Transform _cylWater;
        float[] _top1, _top2, _h, _hTmp;
        bool _hReady;
        int CX = 10, CZ = 7;
        const int RX = 24, RZ = 12;
        struct Dash { public LineRenderer line; public Vector3 p, v; public float life; }
        readonly List<Dash> _dashes = new List<Dash>();

        bool InPlace => Load != null && (Load.transform.position - seat.position).sqrMagnitude < 0.004f * 0.004f;

        void Start() => Pour();

        void Pour()
        {
            if (_ready || seat == null) return;
            _ready = true;
            float d = radius * 2;
            int cx = Mathf.FloorToInt(inner.x / d), cz = Mathf.FloorToInt(inner.z / d), cy = Mathf.FloorToInt(spout / d);
            int n = cx * cy * cz;
            _p = new Vector3[n]; _prev = new Vector3[n]; _vel = new Vector3[n];
            _order = new int[n]; _cellOf = new int[n];
            var rnd = new System.Random(7);
            int i = 0;
            for (int y = 0; y < cy; y++)
                for (int z = 0; z < cz; z++)
                    for (int x = 0; x < cx; x++, i++)
                    {
                        float j() => (float)(rnd.NextDouble() - 0.5) * radius * 0.3f;
                        _p[i] = new Vector3(-inner.x / 2 + radius + x * d + j(), radius + y * d + j(), -inner.z / 2 + radius + z * d + j());
                        _prev[i] = _p[i];
                    }
            _cell = d;
            _nx = Mathf.CeilToInt(inner.x / _cell) + 1;
            _nz = Mathf.CeilToInt(inner.z / _cell) + 1;
            _ny = Mathf.CeilToInt((inner.y + 0.3f) / _cell) + 1;
            _cellStart = new int[_nx * _ny * _nz + 1];
            _cellCount = new int[_nx * _ny * _nz];
            _fillAt = new int[_cellCount.Length];
            for (int s = 0; s < 200; s++) Step(1 / 120f);
            for (int s = 0; s < n; s++) _vel[s] = Vector3.zero;
            _tankLevel = spout;
            var z0 = zeroMark.transform.position;   // the cylinder's empty mark: at its inside bottom
            zeroMark.transform.position = new Vector3(z0.x, cylBase.position.y, z0.z);
            BuildWater();
            Surface();
        }

        protected override void OnPut(Sample3D s)
        {
            _calm = 0;
            Roll();
            s.Via(OverRim());   // in from above, over the glass
        }

        protected override double MaxError(GameState g) => g.VolumeError;
        protected override double Step(GameState g) => g.VolumeStep;
        protected override bool CanLay => _ready;

        protected override void OnTaken(Sample3D s)
        {
            _calm = 0;
            s.Via(OverRim());   // out the way it came: straight up first
        }

        Vector3 OverRim() => seat.TransformPoint(Vector3.up * (inner.y + 0.03f));

        /// How much of the sample is under the can's water, in litres (a sample going in or coming out is partly in).
        float Submerged()
        {
            if (Load == null) return 0;
            var at = seat.InverseTransformPoint(Load.transform.position);
            if (Mathf.Abs(at.x) > inner.x / 2 + 0.05f || Mathf.Abs(at.z) > inner.z / 2 + 0.05f) return 0;
            return Load.volume * Mathf.Clamp01((spout - at.y) / Mathf.Max(0.001f, Load.Size));
        }

        void Update()
        {
            if (!_ready) Pour();
            Ticks();
            float dt = Time.deltaTime;
            // the water pushed aside pours over the spout into the cylinder; a sample taken out, the cylinder drains back
            float sub = Submerged();
            float want = sub * (1 + Bias);
            float before = _cyl;
            // out of a narrow spout the water runs the slower the less of it stands over the spout: a litre in about nine
            // seconds, three in a quarter of a minute, the last of it in drops
            if (want > _cyl) { _rate = Mathf.Max(0.006f, pourRate * Mathf.Sqrt(want - _cyl)); _cyl = Mathf.MoveTowards(_cyl, want, _rate * dt); }
            else
            {
                _rate = 0;
                if (want < _cyl - 0.001f) _cyl = Mathf.MoveTowards(_cyl, want, 1.6f * dt);
            }
            bool pouring = _rate > 0 && _cyl > before, draining = _cyl < before - 0.0005f;
            // the can's own level: up by what is in it, down by what has gone out (over the spout it runs off at once)
            _tankLevel = spout + (sub - _cyl / (1 + Bias)) * 0.001f / CanArea;
            // the cylinder's surface sways a little as it fills
            float target = _cyl * PerCyl;
            Settle(ref _cylShown, ref _cylV, target, 1.4f, 0.35f, dt);
            _cylShown = Mathf.Max(0, _cylShown);
            bool still = AtRest(_cylShown, _cylV, target) && !pouring;
            _calm = still ? _calm + dt : 0;
            int steps = Mathf.Clamp(Mathf.RoundToInt(dt * 120), 1, 3);
            for (int i = 0; i < steps; i++) Step(Mathf.Min(dt / steps, 1 / 60f));
            Surface();
            Stream(pouring);
            Splash(draining);
            Dashes();
        }

        /// In with a splash, out with a plop; a gurgle while the cylinder drains.
        void Splash(bool draining)
        {
            if (draining && Random.value < Time.deltaTime * 3) Sfx.Play("plop", 0.25f, 0.7f + Random.value * 0.3f);
            if (Load == null) { _wasIn = false; return; }
            var local = seat.InverseTransformPoint(Load.transform.position);
            bool inWater = local.y < _tankLevel && Mathf.Abs(local.x) < inner.x / 2;
            if (inWater != _wasIn)
            {
                var at = seat.TransformPoint(new Vector3(local.x, _tankLevel, local.z));
                if (inWater) { Sfx.Play("splash", 0.75f, 1f, 0.08f); Burst(at, 12, 0.9f); }
                else { Sfx.Play("plop", 0.6f); Burst(at, 5, 0.5f); }
            }
            _wasIn = inWater;
        }

        // ------------------------------------------------------------------ the balls of water (never drawn)

        void Step(float dt)
        {
            if (_p == null || dt <= 0) return;
            var g = seat.InverseTransformDirection(Physics.gravity);
            int n = _p.Length;
            for (int i = 0; i < n; i++)
            {
                _vel[i] += g * dt;
                _prev[i] = _p[i];
                _p[i] += _vel[i] * dt;
            }
            bool box = Load != null && Load.gameObject.activeInHierarchy;
            Vector3 bc = Vector3.zero, bh = Vector3.zero;
            if (box)
            {
                var e = Load.Extent;
                float k = Mathf.Pow(Load.volume * 0.001f / Mathf.Max(1e-9f, e.x * e.y * e.z), 1f / 3f);
                bc = seat.InverseTransformPoint(Load.transform.position) + Vector3.up * (Load.Size / 2);
                bh = e * (k / 2) + Vector3.one * radius;
            }
            Grid();
            float d = radius * 2, d2 = d * d;
            for (int it = 0; it < 3; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    Cell(_p[i], out int ix, out int iy, out int iz);
                    for (int z = Mathf.Max(0, iz - 1); z <= Mathf.Min(_nz - 1, iz + 1); z++)
                        for (int y = Mathf.Max(0, iy - 1); y <= Mathf.Min(_ny - 1, iy + 1); y++)
                            for (int x = Mathf.Max(0, ix - 1); x <= Mathf.Min(_nx - 1, ix + 1); x++)
                            {
                                int c = (z * _ny + y) * _nx + x;
                                for (int k = _cellStart[c], end = _cellStart[c] + _cellCount[c]; k < end; k++)
                                {
                                    int j = _order[k];
                                    if (j <= i) continue;
                                    var delta = _p[j] - _p[i];
                                    float dd = delta.sqrMagnitude;
                                    if (dd >= d2 || dd < 1e-12f) continue;
                                    float dist = Mathf.Sqrt(dd);
                                    var push = delta * ((d - dist) / dist * 0.5f);
                                    _p[i] -= push;
                                    _p[j] += push;
                                }
                            }
                }
                for (int i = 0; i < n; i++)
                {
                    var p = _p[i];
                    p.x = Mathf.Clamp(p.x, -inner.x / 2 + radius, inner.x / 2 - radius);
                    p.z = Mathf.Clamp(p.z, -inner.z / 2 + radius, inner.z / 2 - radius);
                    if (p.y < radius) p.y = radius;
                    if (box)
                    {
                        var q = p - bc;
                        float ox = bh.x - Mathf.Abs(q.x), oy = bh.y - Mathf.Abs(q.y), oz = bh.z - Mathf.Abs(q.z);
                        if (ox > 0 && oy > 0 && oz > 0)
                        {
                            if (oy < ox && oy < oz && q.y > 0) p.y += oy;
                            else if (ox < oz) p.x += Mathf.Sign(q.x) * ox;
                            else p.z += Mathf.Sign(q.z) * oz;
                        }
                    }
                    _p[i] = p;
                }
            }
            for (int i = 0; i < n; i++)
            {
                var v = (_p[i] - _prev[i]) / dt;
                float sp = v.magnitude;
                if (sp > 2.5f) v *= 2.5f / sp;
                else if (sp < 0.06f) v *= 0.6f;
                _vel[i] = v * 0.99f;
            }
            Viscous();
        }

        void Viscous()
        {
            int n = _p.Length;
            float reach = radius * 2.4f, r2 = reach * reach;
            for (int i = 0; i < n; i++)
            {
                Cell(_p[i], out int ix, out int iy, out int iz);
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int z = Mathf.Max(0, iz - 1); z <= Mathf.Min(_nz - 1, iz + 1); z++)
                    for (int y = Mathf.Max(0, iy - 1); y <= Mathf.Min(_ny - 1, iy + 1); y++)
                        for (int x = Mathf.Max(0, ix - 1); x <= Mathf.Min(_nx - 1, ix + 1); x++)
                        {
                            int c = (z * _ny + y) * _nx + x;
                            for (int k = _cellStart[c], end = _cellStart[c] + _cellCount[c]; k < end; k++)
                            {
                                int j = _order[k];
                                if (j == i || (_p[j] - _p[i]).sqrMagnitude > r2) continue;
                                sum += _vel[j];
                                count++;
                            }
                        }
                if (count > 0) _vel[i] = Vector3.Lerp(_vel[i], sum / count, 0.25f);
            }
        }

        void Cell(Vector3 p, out int x, out int y, out int z)
        {
            x = Mathf.Clamp((int)((p.x + inner.x / 2) / _cell), 0, _nx - 1);
            y = Mathf.Clamp((int)(p.y / _cell), 0, _ny - 1);
            z = Mathf.Clamp((int)((p.z + inner.z / 2) / _cell), 0, _nz - 1);
        }

        void Grid()
        {
            System.Array.Clear(_cellCount, 0, _cellCount.Length);
            int n = _p.Length;
            for (int i = 0; i < n; i++)
            {
                Cell(_p[i], out int x, out int y, out int z);
                _cellOf[i] = (z * _ny + y) * _nx + x;
                _cellCount[_cellOf[i]]++;
            }
            int sum = 0;
            for (int c = 0; c < _cellCount.Length; c++) { _cellStart[c] = sum; sum += _cellCount[c]; }
            System.Array.Copy(_cellStart, _fillAt, _fillAt.Length);
            for (int i = 0; i < n; i++) _order[_fillAt[_cellOf[i]]++] = i;
        }

        // ------------------------------------------------------------------ what is drawn

        static LineRenderer Line(string name, Transform parent, float width, Material m, bool loop)
        {
            var l = Art3D.LocalLine(name, parent, m, width);
            l.loop = loop;
            l.numCornerVertices = 2;
            l.numCapVertices = 2;
            return l;
        }

        void BuildWater()
        {
            CX = Mathf.Max(4, Mathf.RoundToInt(inner.x / (radius * 2)));
            CZ = Mathf.Max(3, Mathf.RoundToInt(inner.z / (radius * 2)));
            _top1 = new float[CX * CZ]; _top2 = new float[CX * CZ]; _h = new float[CX * CZ]; _hTmp = new float[CX * CZ];
            // the can's water: a clear body with a wavy top
            var go = new GameObject("WaterBody");
            go.transform.SetParent(seat, false);
            _body = new Mesh { name = "Water" };
            _body.MarkDynamic();
            int top = (RX + 1) * (RZ + 1), sideX = (RX + 1) * 2, sideZ = (RZ + 1) * 2;
            _bodyV = new Vector3[top + 2 * sideX + 2 * sideZ];
            var tris = new List<int>();
            int T(int i, int j) => j * (RX + 1) + i;
            for (int j = 0; j < RZ; j++)
                for (int i = 0; i < RX; i++)
                {
                    tris.AddRange(new[] { T(i, j), T(i, j + 1), T(i + 1, j) });
                    tris.AddRange(new[] { T(i + 1, j), T(i, j + 1), T(i + 1, j + 1) });
                }
            int front = top, back = top + sideX, left = top + 2 * sideX, right = left + sideZ;
            for (int i = 0; i < RX; i++)
            {
                int a = front + i * 2, c = a + 2;
                tris.AddRange(new[] { a, c, a + 1, c, c + 1, a + 1 });
                a = back + i * 2; c = a + 2;
                tris.AddRange(new[] { a, a + 1, c, c, a + 1, c + 1 });
            }
            for (int j = 0; j < RZ; j++)
            {
                int a = left + j * 2, c = a + 2;
                tris.AddRange(new[] { a, c, a + 1, c, c + 1, a + 1 });
                a = right + j * 2; c = a + 2;
                tris.AddRange(new[] { a, a + 1, c, c, a + 1, c + 1 });
            }
            _body.vertices = _bodyV;
            _body.SetTriangles(tris, 0);
            go.AddComponent<MeshFilter>().sharedMesh = _body;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = bodyMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _edge = Line("WaterEdge", seat, 0.0035f, edgeMat, true);
            _edgeP = new Vector3[2 * RX + 2 * RZ];
            _edge.positionCount = _edgeP.Length;
            // the cylinder's water: a clear column, a chalk ring round its top
            var col = Art3D.Bare(PrimitiveType.Cylinder, "CylinderWater");
            col.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;
            _cylWater = col.transform;
            _cylWater.SetParent(cylBase, false);
            _cylEdge = Line("CylinderWaterEdge", cylBase, 0.003f, edgeMat, true);
            _cylEdge.positionCount = 24;
            _stream = Line("Stream", transform, 0.012f, bodyMat, false);
            _stream.useWorldSpace = true;
            _stream.positionCount = 10;
            _stream.enabled = false;
        }

        float X(int i) => -inner.x / 2 + inner.x * i / RX;
        float Z(int j) => -inner.z / 2 + inner.z * j / RZ;

        float Height(float x, float z)
        {
            if (!_hReady) return _tankLevel;
            float u = Mathf.Clamp((x + inner.x / 2) / inner.x * CX - 0.5f, 0, CX - 1.001f);
            float v = Mathf.Clamp((z + inner.z / 2) / inner.z * CZ - 0.5f, 0, CZ - 1.001f);
            int i = (int)u, j = (int)v;
            float fu = u - i, fv = v - j;
            float a = Mathf.Lerp(_h[j * CX + i], _h[j * CX + i + 1], fu);
            float b = Mathf.Lerp(_h[(j + 1) * CX + i], _h[(j + 1) * CX + i + 1], fu);
            return Mathf.Lerp(a, b, fv);
        }

        /// The can's surface: the shape of the balls' top (column by column, second highest ball, smoothed in space and
        /// time, waves softened), its mean put at the can's level; the cylinder's column at its level.
        void Surface()
        {
            if (_body == null) return;
            for (int c = 0; c < _top1.Length; c++) { _top1[c] = float.NegativeInfinity; _top2[c] = float.NegativeInfinity; }
            for (int i = 0; i < _p.Length; i++)
            {
                var p = _p[i];
                int cx = Mathf.Clamp((int)((p.x + inner.x / 2) / inner.x * CX), 0, CX - 1);
                int cz = Mathf.Clamp((int)((p.z + inner.z / 2) / inner.z * CZ), 0, CZ - 1);
                int c = cz * CX + cx;
                float y = p.y + radius;
                if (y > _top1[c]) { _top2[c] = _top1[c]; _top1[c] = y; }
                else if (y > _top2[c]) _top2[c] = y;
            }
            for (int c = 0; c < _hTmp.Length; c++) _hTmp[c] = !float.IsNegativeInfinity(_top2[c]) ? _top2[c] : _top1[c];
            for (int pass = 0; pass < 4; pass++)
                for (int c = 0; c < _hTmp.Length; c++)
                {
                    if (!float.IsNegativeInfinity(_hTmp[c])) continue;
                    float s = 0; int k = 0;
                    int cx = c % CX, cz = c / CX;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = cx + dx, z = cz + dz;
                            if (x < 0 || z < 0 || x >= CX || z >= CZ || float.IsNegativeInfinity(_hTmp[z * CX + x])) continue;
                            s += _hTmp[z * CX + x]; k++;
                        }
                    if (k > 0) _hTmp[c] = s / k;
                }
            for (int c = 0; c < _hTmp.Length; c++) if (float.IsNegativeInfinity(_hTmp[c])) _hTmp[c] = _tankLevel;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int c = 0; c < _h.Length; c++)
                {
                    float s = 0; int k = 0;
                    int cx = c % CX, cz = c / CX;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int x = cx + dx, z = cz + dz;
                            if (x < 0 || z < 0 || x >= CX || z >= CZ) continue;
                            float w = dx == 0 && dz == 0 ? 2 : 1;
                            s += _hTmp[z * CX + x] * w; k += (int)w;
                        }
                    _top1[c] = s / k;
                }
                System.Array.Copy(_top1, _hTmp, _hTmp.Length);
            }
            float mean = 0;
            for (int c = 0; c < _hTmp.Length; c++) mean += _hTmp[c];
            mean /= _hTmp.Length;
            float keep = _hReady ? 0.7f : 0;
            for (int c = 0; c < _h.Length; c++) _h[c] = Mathf.Lerp(_tankLevel + (_hTmp[c] - mean) * 0.6f, _h[c], keep);
            _hReady = true;
            int top = (RX + 1) * (RZ + 1), sideX = (RX + 1) * 2, sideZ = (RZ + 1) * 2;
            int front = top, back = top + sideX, left = top + 2 * sideX, right = left + sideZ;
            float hx = inner.x / 2 - 0.001f, hz = inner.z / 2 - 0.001f, bottom = 0.001f;
            for (int j = 0; j <= RZ; j++)
                for (int i = 0; i <= RX; i++)
                    _bodyV[j * (RX + 1) + i] = new Vector3(X(i), Height(X(i), Z(j)), Z(j));
            for (int i = 0; i <= RX; i++)
            {
                float x = X(i);
                _bodyV[front + i * 2] = new Vector3(x, bottom, hz);
                _bodyV[front + i * 2 + 1] = new Vector3(x, Height(x, hz), hz);
                _bodyV[back + i * 2] = new Vector3(x, bottom, -hz);
                _bodyV[back + i * 2 + 1] = new Vector3(x, Height(x, -hz), -hz);
            }
            for (int j = 0; j <= RZ; j++)
            {
                float z = Z(j);
                _bodyV[left + j * 2] = new Vector3(-hx, bottom, z);
                _bodyV[left + j * 2 + 1] = new Vector3(-hx, Height(-hx, z), z);
                _bodyV[right + j * 2] = new Vector3(hx, bottom, z);
                _bodyV[right + j * 2 + 1] = new Vector3(hx, Height(hx, z), z);
            }
            _body.vertices = _bodyV;
            _body.RecalculateNormals();
            _body.RecalculateBounds();
            int e = 0;
            for (int i = 0; i < RX; i++) { float x = X(i); _edgeP[e++] = new Vector3(x, Height(x, hz), hz + 0.002f); }
            for (int j = RZ; j > 0; j--) { float z = Z(j); _edgeP[e++] = new Vector3(hx, Height(hx, z), z); }
            for (int i = RX; i > 0; i--) { float x = X(i); _edgeP[e++] = new Vector3(x, Height(x, -hz), -hz); }
            for (int j = 0; j < RZ; j++) { float z = Z(j); _edgeP[e++] = new Vector3(-hx, Height(-hx, z), z); }
            _edge.SetPositions(_edgeP);
            // the cylinder's column and the ring round its top (a little tilt as it sways)
            float h = Mathf.Max(0.0005f, _cylShown);
            _cylWater.gameObject.SetActive(_cylShown > 0.001f);
            _cylWater.localPosition = Vector3.up * (h / 2);
            _cylWater.localScale = new Vector3(cylRadius * 2 - 0.002f, h / 2, cylRadius * 2 - 0.002f);
            _cylEdge.enabled = _cylShown > 0.001f;
            float tilt = Mathf.Clamp(_cylV * 0.4f, -0.006f, 0.006f);
            for (int k = 0; k < 24; k++)
            {
                float a = k / 24f * Mathf.PI * 2;
                _cylEdge.SetPosition(k, new Vector3(Mathf.Cos(a) * (cylRadius - 0.001f), h + Mathf.Cos(a) * tilt, Mathf.Sin(a) * (cylRadius - 0.001f)));
            }
        }

        /// The stream from the spout into the cylinder while the water pours: an arc that wavers a little.
        void Stream(bool pouring)
        {
            // a stream while it runs, as thick as it runs; drops at the end of it
            bool drops = _rate > 0 && _rate < 0.03f;
            _stream.enabled = pouring && !drops;
            if (drops && Random.value < Time.deltaTime * 2.5f) { Burst(spoutTip.position, 1, 0.12f); Sfx.Play("plop", 0.12f, 1.4f + Random.value * 0.3f); }
            if (!_stream.enabled) return;
            _stream.widthMultiplier = 0.012f * Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(_rate / 0.25f));
            _wobble += Time.deltaTime * 20;
            var a = spoutTip.position;
            var b = cylBase.position + Vector3.up * _cylShown;
            for (int k = 0; k < _stream.positionCount; k++)
            {
                float t = k / (float)(_stream.positionCount - 1);
                var p = Vector3.Lerp(a, b, t);
                p.y = Mathf.Lerp(a.y, b.y, t * t) + Mathf.Sin(_wobble + t * 7) * 0.002f * t;
                _stream.SetPosition(k, p);
            }
            if (Random.value < Time.deltaTime * 6) Burst(b, 2, 0.35f);
        }

        /// A splash: short strokes of chalk thrown up and out, falling and fading.
        void Burst(Vector3 at, int count, float strength)
        {
            for (int i = 0; i < count; i++)
            {
                var l = Line("Splash", transform, 0.004f, edgeMat, false);
                l.useWorldSpace = true;
                l.positionCount = 2;
                var dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.8f;
                _dashes.Add(new Dash { line = l, p = at + Random.insideUnitSphere * 0.03f, v = dir.normalized * Random.Range(0.6f, 1.3f) * strength, life = Random.Range(0.35f, 0.6f) });
            }
        }

        void Dashes()
        {
            for (int i = _dashes.Count - 1; i >= 0; i--)
            {
                var d = _dashes[i];
                d.life -= Time.deltaTime;
                if (d.life <= 0) { Destroy(d.line.gameObject); _dashes.RemoveAt(i); continue; }
                d.v += Physics.gravity * Time.deltaTime;
                d.p += d.v * Time.deltaTime;
                d.line.SetPosition(0, d.p);
                d.line.SetPosition(1, d.p - d.v.normalized * 0.025f);
                d.line.widthMultiplier = 0.004f * Mathf.Clamp01(d.life / 0.2f);
                _dashes[i] = d;
            }
        }

        /// The cylinder's divisions, upwards from its empty mark every step litres.
        protected override void Lay(double step) =>
            ScaleTicks.Build(transform, zeroMark, Outward(cylBase.position, 1e-4f), -PerCyl, step, maxLitres, tickMat, game.eye, Marks, 0.018f);
    }
}
