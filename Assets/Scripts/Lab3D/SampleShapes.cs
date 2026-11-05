using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The shapes a sample can come in (DESIGN.md 2б, «Образцы разной формы»): a cube, a brick, a ball, a cylinder (a
    /// weight), a stone, an ingot, a ring, a bracelet, a goblet, a vase, a bottle, a bell, a pear, an egg — whatever its
    /// order speaks of. Built here as meshes standing on their base (the
    /// origin in the middle of the bottom), so a sample is put down, hung and stacked the same way whatever its shape;
    /// each knows its own volume, so the sample is scaled to exactly the litres it has.
    public static class SampleShapes
    {
        public static readonly string[] All = { "cube", "brick", "ball", "cylinder", "stone", "ingot", "ring", "bracelet", "goblet", "vase", "bottle", "bell", "pear", "egg" };

        static readonly Dictionary<string, (Mesh mesh, float volume)> _made = new Dictionary<string, (Mesh, float)>();

        /// The mesh of a shape (stones come in a few variants, `variant` picks one) and its volume at scale 1.
        public static (Mesh mesh, float volume) Get(string shape, int variant = 0)
        {
            if (shape == "stone") shape = "stone" + Mathf.Abs(variant % 4);
            if (_made.TryGetValue(shape, out var made)) return made;
            Mesh turned = shape switch
            {
                // things turned on a lathe (a profile from the bottom's middle round to the top's) and rings
                "goblet" => Lathe(new[] { (0f, 0f), (0.3f, 0f), (0.3f, 0.03f), (0.08f, 0.07f), (0.06f, 0.36f), (0.13f, 0.46f), (0.36f, 0.6f), (0.42f, 0.86f), (0.4f, 1f), (0f, 1f) }),
                "vase" => Lathe(new[] { (0f, 0f), (0.25f, 0f), (0.4f, 0.25f), (0.42f, 0.45f), (0.3f, 0.75f), (0.17f, 0.9f), (0.22f, 1f), (0f, 1f) }),
                "bottle" => Lathe(new[] { (0f, 0f), (0.3f, 0f), (0.32f, 0.05f), (0.32f, 0.6f), (0.25f, 0.72f), (0.1f, 0.83f), (0.1f, 1.05f), (0.13f, 1.08f), (0f, 1.08f) }),
                "bell" => Lathe(new[] { (0f, 0f), (0.5f, 0f), (0.48f, 0.05f), (0.36f, 0.25f), (0.3f, 0.6f), (0.22f, 0.78f), (0.1f, 0.83f), (0.06f, 0.93f), (0f, 0.95f) }),
                "pear" => Lathe(new[] { (0f, 0f), (0.24f, 0.01f), (0.42f, 0.18f), (0.41f, 0.42f), (0.25f, 0.62f), (0.18f, 0.8f), (0.1f, 0.93f), (0.03f, 1f), (0f, 1f) }),
                "egg" => Lathe(Egg()),
                "ring" => Torus(0.5f, 0.14f, 1f),
                "bracelet" => Torus(0.5f, 0.07f, 2.6f),
                _ => null,
            };
            if (turned != null)
            {
                turned.name = "Sample " + shape;
                return _made[shape] = (turned, Volume(turned));
            }
            Mesh m = shape switch
            {
                "brick" => Box(new Vector3(1.6f, 0.7f, 0.8f)),
                "ball" => Ball(),
                "cylinder" => Cylinder(),
                "ingot" => Ingot(),
                _ when shape.StartsWith("stone") => Stone(shape[shape.Length - 1] - '0'),
                _ => Box(Vector3.one),
            };
            m = FixWinding(m);
            m.name = "Sample " + shape;
            return _made[shape] = (m, Volume(m));
        }

        /// Volume of a closed mesh: the signed tetrahedra from the origin to every triangle.
        static float Volume(Mesh m)
        {
            var v = m.vertices;
            var t = m.triangles;
            float sum = 0;
            for (int i = 0; i < t.Length; i += 3) sum += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
            return Mathf.Abs(sum) / 6f;
        }

        // ------------------------------------------------------------------ the shapes

        /// Flat-shaded mesh from triangles (each with its own vertices: sharp chalk edges between faces), its triangles
        /// clockwise seen from outside (Unity's front faces).
        static Mesh Flat(List<Vector3> tris)
        {
            var m = new Mesh();
            var idx = new int[tris.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            m.SetVertices(tris);
            m.SetTriangles(idx, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// A quad a, b, c, d going round clockwise seen from outside.
        static void Quad(List<Vector3> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }

        static Mesh Box(Vector3 s)
        {
            float x = s.x / 2, z = s.z / 2, y = s.y;
            var p = new[]
            {
                new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(x, 0, z), new Vector3(-x, 0, z),
                new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z),
            };
            return Flat(Frustum(p));
        }

        /// Six faces of a box-like solid from its bottom four corners (0..3, anticlockwise from above) and top four (4..7).
        static List<Vector3> Frustum(Vector3[] p)
        {
            var t = new List<Vector3>();
            Quad(t, p[0], p[1], p[2], p[3]);   // bottom, seen from below
            Quad(t, p[4], p[7], p[6], p[5]);   // top, seen from above
            Quad(t, p[0], p[4], p[5], p[1]);   // front (-z)
            Quad(t, p[1], p[5], p[6], p[2]);   // right (+x)
            Quad(t, p[2], p[6], p[7], p[3]);   // back (+z)
            Quad(t, p[3], p[7], p[4], p[0]);   // left (-x)
            return t;
        }

        /// A gold bar: wider at the bottom, sloping sides.
        static Mesh Ingot()
        {
            var p = new[]
            {
                new Vector3(-0.5f, 0, -0.25f), new Vector3(0.5f, 0, -0.25f), new Vector3(0.5f, 0, 0.25f), new Vector3(-0.5f, 0, 0.25f),
                new Vector3(-0.38f, 0.32f, -0.16f), new Vector3(0.38f, 0.32f, -0.16f), new Vector3(0.38f, 0.32f, 0.16f), new Vector3(-0.38f, 0.32f, 0.16f),
            };
            return Flat(Frustum(p));
        }

        static Mesh Ball()
        {
            var m = new Mesh();
            const int u = 18, v = 11;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j <= v; j++)
                for (int i = 0; i <= u; i++)
                {
                    float th = Mathf.PI * j / v, ph = 2 * Mathf.PI * i / u;
                    verts.Add(new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph)) * 0.5f + Vector3.up * 0.5f);
                }
            for (int j = 0; j < v; j++)
                for (int i = 0; i < u; i++)
                {
                    int a = j * (u + 1) + i, b = a + u + 1;
                    tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return FixWinding(m);
        }

        /// A weight: a cylinder with a smooth side and flat ends.
        static Mesh Cylinder()
        {
            const int n = 20;
            const float r = 0.45f, h = 0.9f;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= n; i++)
            {
                float a = 2 * Mathf.PI * i / n;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
                verts.Add(d);
                verts.Add(d + Vector3.up * h);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2;
                tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            foreach (float y in new[] { 0f, h })
            {
                int c = verts.Count;
                verts.Add(Vector3.up * y);
                for (int i = 0; i <= n; i++)
                {
                    float a = 2 * Mathf.PI * i / n;
                    verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < n; i++) tris.AddRange(y > 0 ? new[] { c, c + i + 2, c + i + 1 } : new[] { c, c + i + 1, c + i + 2 });
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return FixWinding(m);
        }

        /// A stone: a round blob with lumps, flattened, faceted; standing on its lowest point.
        static Mesh Stone(int seed)
        {
            var rnd = new System.Random(11 + seed * 31);
            // an icosahedron, split twice
            float g = (1 + Mathf.Sqrt(5)) / 2;
            var v = new List<Vector3>
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            for (int split = 0; split < 2; split++)
            {
                var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(key, out int k)) return k;
                    v.Add(((v[a] + v[b]) / 2).normalized);
                    return mid[key] = v.Count - 1;
                }
                var nf = new List<int>();
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            // lumps: a few soft bumps and dents, the whole flattened and stretched a little
            var bumps = new List<(Vector3 d, float k)>();
            for (int i = 0; i < 7; i++)
                bumps.Add((Random3(rnd), (float)(rnd.NextDouble() * 0.5 - 0.22)));
            var stretch = new Vector3(1 + (float)rnd.NextDouble() * 0.35f, 0.62f + (float)rnd.NextDouble() * 0.15f, 0.85f + (float)rnd.NextDouble() * 0.2f);
            for (int i = 0; i < v.Count; i++)
            {
                float r = 1;
                foreach (var (d, k) in bumps) r += k * Mathf.Pow(Mathf.Max(0, Vector3.Dot(v[i], d)), 3);
                r *= 1 + (float)(rnd.NextDouble() - 0.5) * 0.06f;
                v[i] = Vector3.Scale(v[i] * r * 0.5f, stretch);
            }
            float low = float.MaxValue;
            foreach (var p in v) low = Mathf.Min(low, p.y);
            var tris = new List<Vector3>();
            for (int i = 0; i < f.Count; i += 3)
            {
                tris.Add(v[f[i]] - Vector3.up * low);
                tris.Add(v[f[i + 1]] - Vector3.up * low);
                tris.Add(v[f[i + 2]] - Vector3.up * low);
            }
            return FixWinding(Flat(tris));
        }

        /// A solid of revolution: the profile (radius, height) from the axis at the bottom round to the axis at the top,
        /// turned about the vertical; its faces turned outwards as a whole (it may be hollowed in, as a goblet's stem).
        static Mesh Lathe((float r, float y)[] profile)
        {
            const int n = 28;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < profile.Length; i++)
                for (int k = 0; k <= n; k++)
                {
                    float a = k / (float)n * Mathf.PI * 2;
                    v.Add(new Vector3(Mathf.Cos(a) * profile[i].r, profile[i].y, Mathf.Sin(a) * profile[i].r));
                }
            for (int i = 0; i < profile.Length - 1; i++)
                for (int k = 0; k < n; k++)
                {
                    int a = i * (n + 1) + k, b = a + n + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            return Outward(v, t);
        }

        static (float, float)[] Egg()
        {
            var p = new (float, float)[13];
            for (int i = 0; i <= 12; i++)
            {
                float a = i / 12f * Mathf.PI;
                p[i] = (Mathf.Sin(a) * 0.36f * (1 - 0.14f * Mathf.Cos(a)), (1 - Mathf.Cos(a)) / 2);
            }
            p[0] = (0, 0);
            p[12] = (0, 1);
            return p;
        }

        /// A ring lying flat: major radius R, the tube r, stretched `tall` times upwards (a bracelet is a band).
        static Mesh Torus(float R, float r, float tall)
        {
            const int n = 32, m = 14;
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= n; i++)
                for (int j = 0; j <= m; j++)
                {
                    float a = i / (float)n * Mathf.PI * 2, b = j / (float)m * Mathf.PI * 2;
                    float rr = R + Mathf.Cos(b) * r;
                    v.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(b) * r * tall + r * tall, Mathf.Sin(a) * rr));
                }
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                {
                    int a = i * (m + 1) + j, b = a + m + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            return Outward(v, t);
        }

        /// The mesh with all its faces turned out together: if its volume comes out negative, every face is turned.
        static Mesh Outward(List<Vector3> v, List<int> t)
        {
            float sum = 0;
            for (int i = 0; i < t.Count; i += 3) sum += Vector3.Dot(v[t[i]], Vector3.Cross(v[t[i + 1]], v[t[i + 2]]));
            if (sum < 0) for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Vector3 Random3(System.Random r) => new Vector3((float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1), (float)(r.NextDouble() * 2 - 1)).normalized;

        /// Turns every triangle to face out of the solid (away from its middle): whatever order a shape was built in.
        static Mesh FixWinding(Mesh m)
        {
            var v = m.vertices;
            var t = m.triangles;
            var centre = m.bounds.center;
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                var n = Vector3.Cross(b - a, c - a);   // Unity: clockwise seen from outside gives a normal out
                if (Vector3.Dot(n, (a + b + c) / 3 - centre) < 0) { (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]); }
            }
            m.triangles = t;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
