// Übertragung von render/geometry.ts: prozedurale Grundkörper (Würfel, Zylinder, Kegel, Kugeln, Ringe …).
// Erzeugt in Spielkoordinaten (rechtshändig) und beim Umwandeln in Unity-Meshes an z gespiegelt (siehe IsoCamera.W).
// Die Materialien zeichnen beidseitig (Cull Off) wie im Webspiel; Flächennormalen zeigen immer nach außen.
using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>Rohdaten eines Grundkörpers (Unity-Lokalkoordinaten, nicht indiziert).</summary>
    public class MeshData
    {
        public Vector3[] v;
        public Vector3[] n;
    }

    public static class WorldMeshes
    {
        public static readonly string[] IDS =
        {
            "box", "rbox", "cyl6", "cyl8", "cyl12", "cyl16", "cone4", "cone6", "cone8", "cone12",
            "octa", "sphere", "sphereLo", "gem", "ring", "ringThin", "disc", "torus", "hex", "plane", "leaf",
        };

        static readonly Dictionary<string, int> index = new Dictionary<string, int>();
        static MeshData[] data;
        static Mesh[] meshes;

        static void Init()
        {
            if (data != null) return;
            data = new MeshData[IDS.Length];
            meshes = new Mesh[IDS.Length];
            for (int i = 0; i < IDS.Length; i++) index[IDS[i]] = i;
        }

        public static int Index(string id)
        {
            Init();
            return id != null && index.TryGetValue(id, out var i) ? i : 0;
        }

        public static MeshData Data(int i)
        {
            Init();
            if (data[i] == null) data[i] = Build(IDS[i]);
            return data[i];
        }

        public static MeshData Data(string id) => Data(Index(id));

        public static Mesh Get(int i)
        {
            Init();
            if (meshes[i] == null)
            {
                var d = Data(i);
                var m = new Mesh { name = "kw_" + IDS[i] };
                m.vertices = d.v;
                m.normals = d.n;
                var tri = new int[d.v.Length];
                for (int k = 0; k < tri.Length; k++) tri[k] = k;
                m.triangles = tri;
                m.RecalculateBounds();
                m.UploadMeshData(true);
                meshes[i] = m;
            }
            return meshes[i];
        }

        public static Mesh Get(string id) => Get(Index(id));

        // ------------------------------------------------------------ Bauhelfer

        class Builder
        {
            public readonly List<Vector3> p = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public Vector3 center;

            public void TriN(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
            {
                p.Add(a); p.Add(b); p.Add(c);
                n.Add(na.normalized); n.Add(nb.normalized); n.Add(nc.normalized);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var nn = Vector3.Cross(b - a, c - a);
                if (nn.sqrMagnitude < 1e-12f) nn = Vector3.up;
                nn.Normalize();
                var mid = (a + b + c) / 3f;
                if (Vector3.Dot(nn, mid - center) < 0) nn = -nn;
                TriN(a, b, c, nn, nn, nn);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            /// <summary>Spielkoordinaten → Unity-Lokalkoordinaten (z gespiegelt).</summary>
            public MeshData Build()
            {
                var v = new Vector3[p.Count];
                var nn = new Vector3[p.Count];
                for (int i = 0; i < p.Count; i++)
                {
                    v[i] = new Vector3(p[i].x, p[i].y, -p[i].z);
                    nn[i] = new Vector3(n[i].x, n[i].y, -n[i].z);
                }
                // Spiegelung kehrt die Umlaufrichtung um – Dreiecke umdrehen, damit Vorderseiten außen bleiben.
                for (int i = 0; i + 2 < v.Length; i += 3)
                {
                    var t = v[i + 1]; v[i + 1] = v[i + 2]; v[i + 2] = t;
                    var u = nn[i + 1]; nn[i + 1] = nn[i + 2]; nn[i + 2] = u;
                }
                return new MeshData { v = v, n = nn };
            }
        }

        static MeshData Build(string id)
        {
            switch (id)
            {
                case "box": return Box();
                case "rbox": return Box(); // PORT: Fase (roundedBox) vereinfacht als Würfel
                case "cyl6": return Cylinder(6, 0.5f, 0.5f, true, false);
                case "cyl8": return Cylinder(10, 0.5f, 0.5f, true, true);
                case "cyl12": return Cylinder(14, 0.5f, 0.5f, true, true);
                case "cyl16": return Cylinder(20, 0.5f, 0.5f, true, true);
                case "cone4": return Cylinder(4, 0.5f, 0, true, false);
                case "cone6": return Cylinder(8, 0.5f, 0, true, true);
                case "cone8": return Cylinder(12, 0.5f, 0, true, true);
                case "cone12": return Cylinder(18, 0.5f, 0, true, true);
                case "octa": return Octahedron();
                case "sphere": return Sphere(1, true);
                case "sphereLo": return Sphere(0, true);
                case "gem": return Sphere(0, false);
                case "ring": return Ring(48, 0.88f);
                case "ringThin": return Ring(48, 0.95f);
                case "disc": return Disc(32);
                case "torus": return Torus(18, 6, 0.12f);
                case "hex": return Cylinder(6, 0.5f, 0.5f, true, false);
                case "plane": return Plane(8);
                case "leaf": return Leaf();
                default: return Box();
            }
        }

        /// <summary>Würfel 1×1×1, zentriert.</summary>
        static MeshData Box()
        {
            var b = new Builder();
            const float h = 0.5f;
            Vector3 V(float x, float y, float z) => new Vector3(x * h, y * h, z * h);
            b.Quad(V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1));
            b.Quad(V(1, -1, -1), V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1));
            b.Quad(V(1, -1, 1), V(1, -1, -1), V(1, 1, -1), V(1, 1, 1));
            b.Quad(V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1));
            b.Quad(V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1), V(-1, 1, -1));
            b.Quad(V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1));
            return b.Build();
        }

        /// <summary>Zylinder/Kegelstumpf, Höhe 1 (y 0..1), Radius unten rb, oben rt.</summary>
        static MeshData Cylinder(int segments, float rb, float rt, bool caps, bool smooth)
        {
            var b = new Builder { center = new Vector3(0, 0.5f, 0) };
            float slope = rb - rt;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                var p0 = new Vector3(c0 * rb, 0, s0 * rb);
                var p1 = new Vector3(c1 * rb, 0, s1 * rb);
                var q0 = new Vector3(c0 * rt, 1, s0 * rt);
                var q1 = new Vector3(c1 * rt, 1, s1 * rt);
                if (smooth)
                {
                    var n0 = new Vector3(c0, slope, s0);
                    var n1 = new Vector3(c1, slope, s1);
                    if (rt > 0)
                    {
                        b.TriN(p1, p0, q0, n1, n0, n0);
                        b.TriN(p1, q0, q1, n1, n0, n1);
                    }
                    else
                    {
                        var na = new Vector3(Mathf.Cos((a0 + a1) / 2), slope, Mathf.Sin((a0 + a1) / 2));
                        b.TriN(p1, p0, new Vector3(0, 1, 0), n1, n0, na);
                    }
                }
                else if (rt > 0) b.Quad(p1, p0, q0, q1);
                else b.Tri(p1, p0, new Vector3(0, 1, 0));
                if (caps)
                {
                    if (rt > 0) b.TriN(new Vector3(0, 1, 0), q1, q0, Vector3.up, Vector3.up, Vector3.up);
                    b.TriN(Vector3.zero, p0, p1, Vector3.down, Vector3.down, Vector3.down);
                }
            }
            return b.Build();
        }

        /// <summary>Oktaeder, Radius 0,5, zentriert (facettiert).</summary>
        static MeshData Octahedron()
        {
            var b = new Builder();
            const float r = 0.5f;
            var t = new Vector3(0, r, 0);
            var d = new Vector3(0, -r, 0);
            var ring = new[] { new Vector3(r, 0, 0), new Vector3(0, 0, r), new Vector3(-r, 0, 0), new Vector3(0, 0, -r) };
            for (int i = 0; i < 4; i++)
            {
                var a = ring[i];
                var c = ring[(i + 1) % 4];
                b.Tri(t, c, a);
                b.Tri(d, a, c);
            }
            return b.Build();
        }

        /// <summary>Ikosaeder-Kugel, Radius 0,5.</summary>
        static MeshData Sphere(int subdiv, bool smooth)
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0), new Vector3(0, -1, t), new Vector3(0, 1, t),
                new Vector3(0, -1, -t), new Vector3(0, 1, -t), new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var faces = new List<int[]>
            {
                new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
            };
            for (int s = 0; s < subdiv; s++)
            {
                var nf = new List<int[]>();
                var cache = new Dictionary<long, int>();
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (cache.TryGetValue(key, out var hit)) return hit;
                    verts.Add(((verts[a] + verts[c]) * 0.5f).normalized);
                    cache[key] = verts.Count - 1;
                    return verts.Count - 1;
                }
                foreach (var f in faces)
                {
                    int ab = Mid(f[0], f[1]), bc = Mid(f[1], f[2]), ca = Mid(f[2], f[0]);
                    nf.Add(new[] { f[0], ab, ca }); nf.Add(new[] { f[1], bc, ab }); nf.Add(new[] { f[2], ca, bc }); nf.Add(new[] { ab, bc, ca });
                }
                faces = nf;
            }
            var b = new Builder();
            foreach (var f in faces)
            {
                Vector3 va = verts[f[0]], vb = verts[f[1]], vc = verts[f[2]];
                if (smooth) b.TriN(va * 0.5f, vb * 0.5f, vc * 0.5f, va, vb, vc);
                else b.Tri(va * 0.5f, vb * 0.5f, vc * 0.5f);
            }
            return b.Build();
        }

        /// <summary>Flacher Kreisring in der XZ-Ebene, Außenradius 1.</summary>
        static MeshData Ring(int segments, float inner)
        {
            var b = new Builder();
            var up = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2;
                var o0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                var o1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                b.TriN(o0 * inner, o1 * inner, o1, up, up, up);
                b.TriN(o0 * inner, o1, o0, up, up, up);
            }
            return b.Build();
        }

        /// <summary>Gefüllte Scheibe in der XZ-Ebene, Radius 1.</summary>
        static MeshData Disc(int segments)
        {
            var b = new Builder();
            var up = Vector3.up;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2;
                float a1 = (i + 1) / (float)segments * Mathf.PI * 2;
                b.TriN(Vector3.zero, new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), up, up, up);
            }
            return b.Build();
        }

        /// <summary>Ebene 1×1 (zentriert) in der XZ-Ebene.</summary>
        static MeshData Plane(int div)
        {
            var b = new Builder();
            var up = Vector3.up;
            for (int z = 0; z < div; z++)
                for (int x = 0; x < div; x++)
                {
                    float x0 = x / (float)div - 0.5f, x1 = (x + 1) / (float)div - 0.5f, z0 = z / (float)div - 0.5f, z1 = (z + 1) / (float)div - 0.5f;
                    b.TriN(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), up, up, up);
                    b.TriN(new Vector3(x0, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), up, up, up);
                }
            return b.Build();
        }

        /// <summary>Torus, Radius 0,5, Rohrdicke tube.</summary>
        static MeshData Torus(int seg, int sides, float tube)
        {
            var b = new Builder();
            const float R = 0.5f;
            Vector3 Pt(float u, float v) => new Vector3((R + tube * Mathf.Cos(v)) * Mathf.Cos(u), tube * Mathf.Sin(v), (R + tube * Mathf.Cos(v)) * Mathf.Sin(u));
            Vector3 Nr(float u, float v) => new Vector3(Mathf.Cos(v) * Mathf.Cos(u), Mathf.Sin(v), Mathf.Cos(v) * Mathf.Sin(u));
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    float u0 = i / (float)seg * Mathf.PI * 2, u1 = (i + 1) / (float)seg * Mathf.PI * 2;
                    float v0 = j / (float)sides * Mathf.PI * 2, v1 = (j + 1) / (float)sides * Mathf.PI * 2;
                    b.TriN(Pt(u0, v0), Pt(u0, v1), Pt(u1, v1), Nr(u0, v0), Nr(u0, v1), Nr(u1, v1));
                    b.TriN(Pt(u0, v0), Pt(u1, v1), Pt(u1, v0), Nr(u0, v0), Nr(u1, v1), Nr(u1, v0));
                }
            return b.Build();
        }

        /// <summary>Flügel/Blatt: flaches, spitz zulaufendes Element in der XZ-Ebene (Länge entlang +Z).</summary>
        static MeshData Leaf()
        {
            var b = new Builder();
            var up = Vector3.up;
            var pts = new[] { new Vector3(0, 0, 0), new Vector3(0.35f, 0, 0.3f), new Vector3(0.25f, 0, 0.75f), new Vector3(0, 0, 1), new Vector3(-0.25f, 0, 0.75f), new Vector3(-0.35f, 0, 0.3f) };
            for (int i = 1; i < pts.Length - 1; i++) b.TriN(pts[0], pts[i], pts[i + 1], up, up, up);
            return b.Build();
        }

        // ------------------------------------------------------------ Transformation (wie math3d.writeTRS)

        /// <summary>
        /// Modellmatrix in Unity-Koordinaten aus Spiel-TRS (R = Ry·Rz·Rx wie writeTRS) – Spiegelung F·M·F, damit Spiel- und
        /// Unity-Ausrichtung übereinstimmen.
        /// </summary>
        public static Matrix4x4 TRS(float x, float y, float z, float yaw, float pitch, float roll, float sx, float sy, float sz)
        {
            float cy = Mathf.Cos(yaw), syw = Mathf.Sin(yaw);
            float cp = Mathf.Cos(pitch), sp = Mathf.Sin(pitch);
            float cr = Mathf.Cos(roll), sr = Mathf.Sin(roll);
            // Spalten (Spielkoordinaten)
            float m00 = cy * cp, m10 = sp, m20 = -syw * cp;                                  // Spalte 0
            float m01 = -cy * sp * cr + syw * sr, m11 = cp * cr, m21 = syw * sp * cr + cy * sr; // Spalte 1
            float m02 = cy * sp * sr + syw * cr, m12 = -cp * sr, m22 = -syw * sp * sr + cy * cr; // Spalte 2
            var m = new Matrix4x4();
            // F·M·F: Vorzeichen f_i·f_j mit f = (1, 1, −1)
            m.m00 = m00 * sx; m.m01 = m01 * sy; m.m02 = -m02 * sz; m.m03 = x;
            m.m10 = m10 * sx; m.m11 = m11 * sy; m.m12 = -m12 * sz; m.m13 = y;
            m.m20 = -m20 * sx; m.m21 = -m21 * sy; m.m22 = m22 * sz; m.m23 = -z;
            m.m30 = 0; m.m31 = 0; m.m32 = 0; m.m33 = 1;
            return m;
        }
    }
}
