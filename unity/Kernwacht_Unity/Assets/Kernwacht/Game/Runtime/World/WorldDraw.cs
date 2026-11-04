// Sofortzeichner der Welt (Rolle von render/Renderer.ts): Grundkörper, weiche Sprites (Glühen, Ringe, Rauch, Sterne),
// Leuchtstrahlen und Overlay (Lebensbalken, Status-Symbole, Bildblitz). Alles wird jedes Bild neu gesammelt und in
// wenigen Zeichenaufrufen abgegeben (gebündelte dynamische Meshes, GPU-Instancing). Keine Speicheranforderung pro Bild.
// Koordinaten wie im Webspiel (x = Spalte, z = Zeile, y = oben); Umrechnung nach Unity über (x, y, −z).
using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kernwacht.Game
{
    /// <summary>Farbhilfen (math3d.ts: hexToRgb, mixColor, shade) auf 0xRRGGBB-Ganzzahlen.</summary>
    public static class Col
    {
        static int R(double v) => (int)System.Math.Floor(v + 0.5);

        public static int Mix(int a, int b, double t)
        {
            int ar = (a >> 16) & 255, ag = (a >> 8) & 255, ab = a & 255;
            int br = (b >> 16) & 255, bg = (b >> 8) & 255, bb = b & 255;
            int r = Mathf.Clamp(R(ar + (br - ar) * t), 0, 255);
            int g = Mathf.Clamp(R(ag + (bg - ag) * t), 0, 255);
            int bl = Mathf.Clamp(R(ab + (bb - ab) * t), 0, 255);
            return (r << 16) | (g << 8) | bl;
        }

        public static int Shade(int c, double f)
        {
            int r = Mathf.Clamp(R(((c >> 16) & 255) * f), 0, 255);
            int g = Mathf.Clamp(R(((c >> 8) & 255) * f), 0, 255);
            int b = Mathf.Clamp(R((c & 255) * f), 0, 255);
            return (r << 16) | (g << 8) | b;
        }

        public static Color ToColor(int hex, float a = 1f) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f, a);

        /// <summary>Hex als lineare Farbe (für Lichter/Umgebung, die Unity nicht selbst umrechnet).</summary>
        public static Color Linear(int hex) => QualitySettings.activeColorSpace == ColorSpace.Linear ? ToColor(hex).linear : ToColor(hex);

        static readonly byte[] lin = BuildLin();
        static byte[] BuildLin()
        {
            var t = new byte[256];
            for (int i = 0; i < 256; i++) t[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.GammaToLinearSpace(i / 255f) * 255f), 0, 255);
            return t;
        }

        /// <summary>Vertexfarbe (in linearem Farbraum vorab umgerechnet, da Vertexfarben nicht konvertiert werden).</summary>
        public static Color32 Vert(int hex, float a)
        {
            byte al = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            int r = (hex >> 16) & 255, g = (hex >> 8) & 255, b = hex & 255;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) return new Color32(lin[r], lin[g], lin[b], al);
            return new Color32((byte)r, (byte)g, (byte)b, al);
        }

        public static string Css(int hex) => "#" + (hex & 0xffffff).ToString("x6");
    }

    /// <summary>Sprite-Formen (Renderer.ts SHAPE).</summary>
    public static class SHAPE
    {
        public const int glow = 0, disc = 1, ring = 2, spark = 3, smoke = 4, star = 5, beam = 6, vortex = 7;
        /// <summary>Zusätzlich (Unity): weiches Quadrat für das Bauraster.</summary>
        public const int square = 8;
    }

    public class WorldDraw
    {
        // ------------------------------------------------------------ Zustand je Bild
        public Camera camera;
        public Vector3 camRight = Vector3.right, camUp = Vector3.up, toCam = Vector3.up;   // Spielkoordinaten
        public float time;
        /// <summary>Additive Leuchteffekte gedämpft (Lesbarkeit vor Glanz) – wie Renderer.glowScale.</summary>
        public float glowScale = 0.5f;

        // ------------------------------------------------------------ Materialien
        public Material matLit, matUnlitSolid, matTrans, matSpriteAlpha, matSpriteAdd, matLine, matLineTop, matOverlay;
        public Texture2D atlas;
        const int LAYER = 0;

        // ------------------------------------------------------------ Puffer
        class QuadBuf
        {
            public readonly int cap;
            public readonly Vector3[] pos;
            public readonly Vector2[] uv;
            public readonly Color32[] col;
            public readonly int[] idx;
            public int n;
            public Mesh mesh;
            public QuadBuf(int cap)
            {
                this.cap = cap;
                pos = new Vector3[cap * 4];
                uv = new Vector2[cap * 4];
                col = new Color32[cap * 4];
                idx = new int[cap * 6];
                for (int q = 0; q < cap; q++)
                {
                    int v = q * 4, i = q * 6;
                    idx[i] = v; idx[i + 1] = v + 1; idx[i + 2] = v + 2;
                    idx[i + 3] = v; idx[i + 4] = v + 2; idx[i + 5] = v + 3;
                }
                mesh = new Mesh { name = "kw_sprites" };
                mesh.MarkDynamic();
            }
        }

        class TriBuf
        {
            public readonly Vector3[] pos;
            public readonly Color32[] col;
            public readonly int[] idx;
            public int nv, ni;
            public Mesh mesh;
            public TriBuf(int vcap, int icap)
            {
                pos = new Vector3[vcap];
                col = new Color32[vcap];
                idx = new int[icap];
                mesh = new Mesh { name = "kw_lines" };
                mesh.MarkDynamic();
            }
            public bool Room(int v, int i) => nv + v <= pos.Length && ni + i <= idx.Length;
            public int V(Vector3 p, Color32 c)
            {
                pos[nv] = p; col[nv] = c;
                return nv++;
            }
            public void T(int a, int b, int c) { idx[ni++] = a; idx[ni++] = b; idx[ni++] = c; }
        }

        class Batch
        {
            public int mesh;
            public int mode;   // 0 beleuchtet, 1 unbeleuchtet (leuchtend), 2 durchscheinend
            public readonly Matrix4x4[] m = new Matrix4x4[1023];
            public int n;
            public readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            public Color color;
        }

        readonly QuadBuf sprAlpha = new QuadBuf(12000);
        readonly QuadBuf sprAdd = new QuadBuf(16000);
        readonly QuadBuf sprTop = new QuadBuf(2000);
        readonly TriBuf lines = new TriBuf(40000, 96000);
        readonly TriBuf linesTop = new TriBuf(8000, 20000);
        readonly TriBuf overlay = new TriBuf(24000, 48000);
        readonly Dictionary<long, Batch> batches = new Dictionary<long, Batch>();
        readonly List<Batch> used = new List<Batch>();
        readonly Stack<Batch> free = new Stack<Batch>();
        static readonly Bounds HUGE = new Bounds(Vector3.zero, new Vector3(100000, 100000, 100000));
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public WorldDraw()
        {
            foreach (var b in new[] { sprAlpha, sprAdd, sprTop }) b.mesh.bounds = HUGE;
            foreach (var b in new[] { lines, linesTop, overlay }) b.mesh.bounds = HUGE;
            CreateMaterials();
        }

        // ------------------------------------------------------------ Materialien anlegen

        static Material LitBase()
        {
            var ga = GameAssets.Instance;
            if (ga != null && ga.lit != null) return new Material(ga.lit);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            return new Material(sh);
        }

        static Material FxBase(bool transparentAsset)
        {
            var ga = GameAssets.Instance;
            Material src = ga != null ? (transparentAsset ? ga.transparent : ga.unlit) : null;
            if (src != null) return new Material(src);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            bool urp = sh != null;
            if (!urp) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            return m;
        }

        static void SetBlend(Material m, BlendMode src, BlendMode dst)
        {
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)src);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)dst);
            if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", (float)dst);
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, c);
        }

        /// <summary>Vertexfarb-Material ohne Textur (Hidden/Internal-Colored); Rückfall: unbeleuchtetes Effektmaterial.</summary>
        static Material Colored(BlendMode src, BlendMode dst, bool ztestAlways, int queue)
        {
            var sh = Shader.Find("Hidden/Internal-Colored");
            Material m;
            if (sh != null)
            {
                m = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                m.SetInt("_SrcBlend", (int)src);
                m.SetInt("_DstBlend", (int)dst);
                m.SetInt("_Cull", (int)CullMode.Off);
                m.SetInt("_ZWrite", 0);
                m.SetInt("_ZTest", (int)(ztestAlways ? CompareFunction.Always : CompareFunction.LessEqual));
                m.SetColor("_Color", Color.white);
            }
            else
            {
                // PORT: ohne Internal-Colored ist der Overlay-Durchgriff (ZTest Always) nicht verfügbar.
                m = FxBase(false);
                SetBlend(m, src, dst);
                SetColor(m, Color.white);
            }
            m.renderQueue = queue;
            return m;
        }

        void CreateMaterials()
        {
            matLit = LitBase();
            matLit.name = "kw_lit";
            if (matLit.HasProperty("_Cull")) matLit.SetFloat("_Cull", 0f);
            if (matLit.HasProperty("_Smoothness")) matLit.SetFloat("_Smoothness", 0.25f);
            matLit.enableInstancing = true;

            matUnlitSolid = FxBase(false);
            matUnlitSolid.name = "kw_unlitSolid";
            if (matUnlitSolid.HasProperty("_ZWrite")) matUnlitSolid.SetFloat("_ZWrite", 1f);
            matUnlitSolid.renderQueue = 2450;

            matTrans = FxBase(true);
            matTrans.name = "kw_trans";
            matTrans.renderQueue = 3000;

            atlas = BuildAtlas();
            matSpriteAlpha = FxBase(false);
            matSpriteAlpha.name = "kw_spriteAlpha";
            SetBlend(matSpriteAlpha, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha);
            if (matSpriteAlpha.HasProperty("_BaseMap")) matSpriteAlpha.SetTexture("_BaseMap", atlas);
            if (matSpriteAlpha.HasProperty("_MainTex")) matSpriteAlpha.SetTexture("_MainTex", atlas);
            SetColor(matSpriteAlpha, Color.white);
            matSpriteAlpha.renderQueue = 3010;

            matSpriteAdd = FxBase(false);
            matSpriteAdd.name = "kw_spriteAdd";
            SetBlend(matSpriteAdd, BlendMode.SrcAlpha, BlendMode.One);
            if (matSpriteAdd.HasProperty("_BaseMap")) matSpriteAdd.SetTexture("_BaseMap", atlas);
            if (matSpriteAdd.HasProperty("_MainTex")) matSpriteAdd.SetTexture("_MainTex", atlas);
            SetColor(matSpriteAdd, Color.white);
            matSpriteAdd.renderQueue = 3030;

            matLine = Colored(BlendMode.SrcAlpha, BlendMode.One, false, 3020);
            matLineTop = Colored(BlendMode.SrcAlpha, BlendMode.One, true, 3900);
            matOverlay = Colored(BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, true, 4000);
        }

        /// <summary>Material für Geisterbilder (Bauvorschau, getarnte Gegner): durchscheinend, unbeleuchtet.</summary>
        public Material NewGhostMaterial()
        {
            var m = FxBase(true);
            m.name = "kw_ghost";
            m.renderQueue = 3005;
            return m;
        }

        // ------------------------------------------------------------ Sprite-Atlas (prozedural, wie der Sprite-Shader)

        const int TILE = 128, GRID = 4;
        static readonly Vector4[] tileRect = new Vector4[GRID * GRID];

        static float Hash(float x, float y) { float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }
        static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }
        static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        static float ShapeAlpha(int tile, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (tile)
            {
                case 0: return Mathf.Exp(-r * r * 3.2f) * SS(1f, 0.7f, r);
                case 1: return SS(1f, 1f - 0.06f, r);
                case 2: return SS(1f, 1f - 0.3f, r);
                case 3: return SS(1f, 1f - 0.6f, r);
                case 4: case 5: case 6: case 7:
                {
                    float w = tile == 4 ? 0.04f : tile == 5 ? 0.08f : tile == 6 ? 0.12f : 0.18f;
                    return SS(w, 0f, Mathf.Abs(r - (1f - w)));
                }
                case 8: return Mathf.Exp(-(u * u * 2f + v * v * 10f));
                case 9: case 10:
                {
                    float s = tile == 9 ? 0f : 7.3f;
                    float n = Noise(u * 2.2f + s, v * 2.2f + s) * 0.6f + Noise(u * 4.7f - s, v * 4.7f - s) * 0.4f;
                    return SS(1f, 0.15f, r) * (0.35f + 0.65f * n);
                }
                case 11:
                    return Mathf.Min(1f, Mathf.Exp(-r * r * 7f) + (Mathf.Max(0, 1 - Mathf.Abs(u) * 9) * Mathf.Max(0, 1 - Mathf.Abs(v)) + Mathf.Max(0, 1 - Mathf.Abs(v) * 9) * Mathf.Max(0, 1 - Mathf.Abs(u))) * 0.7f);
                case 12:
                {
                    float a = (1 - SS(0, 1, Mathf.Abs(v))) * SS(1f, 0.85f, Mathf.Abs(u));
                    return Mathf.Min(1f, a * a + (1 - SS(0, 0.25f, Mathf.Abs(v))) * 0.6f);
                }
                case 13:
                {
                    float ang = Mathf.Atan2(v, u);
                    float sw = Mathf.Sin(ang * 3f + r * 9f) * 0.5f + 0.5f;
                    return Mathf.Min(1f, SS(1f, 0.25f, r) * (0.25f + 0.75f * sw * sw) * SS(0f, 0.2f, r) + Mathf.Exp(-r * r * 14f) * 0.8f);
                }
                case 14:
                {
                    float m = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    return SS(1f, 0.82f, m);
                }
                default: return Mathf.Exp(-r * r * 8f);
            }
        }

        static Texture2D BuildAtlas()
        {
            int size = TILE * GRID;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "kw_spriteAtlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            const float margin = 1.06f;
            for (int t = 0; t < GRID * GRID; t++)
            {
                int tx = t % GRID, ty = t / GRID;
                for (int y = 0; y < TILE; y++)
                    for (int x = 0; x < TILE; x++)
                    {
                        float u = ((x + 0.5f) / TILE * 2f - 1f) * margin;
                        float v = ((y + 0.5f) / TILE * 2f - 1f) * margin;
                        float a = Mathf.Abs(u) > 1f || Mathf.Abs(v) > 1f ? 0f : Mathf.Clamp01(ShapeAlpha(t, u, v));
                        px[(ty * TILE + y) * size + tx * TILE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                // UV-Rechteck so, dass uv −1..1 auf den inneren Bereich (ohne Rand) fällt
                float pad = (1f - 1f / margin) * 0.5f * TILE;
                tileRect[t] = new Vector4((tx * TILE + pad) / size, (ty * TILE + pad) / size, (TILE - 2 * pad) / size, (TILE - 2 * pad) / size);
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static int TileOf(int shape, float p1, float p2, float seed)
        {
            switch (shape)
            {
                case SHAPE.glow: return 0;
                case SHAPE.disc: return p1 <= 0.12f ? 1 : p1 <= 0.42f ? 2 : 3;
                case SHAPE.ring:
                {
                    float w = Mathf.Max(p2, 0.04f);
                    return w <= 0.06f ? 4 : w <= 0.1f ? 5 : w <= 0.14f ? 6 : 7;
                }
                case SHAPE.spark: return 8;
                case SHAPE.smoke: return ((int)Mathf.Abs(seed) & 1) == 0 ? 9 : 10;
                case SHAPE.star: return 11;
                case SHAPE.beam: return 12;
                case SHAPE.vortex: return 13;
                case SHAPE.square: return 14;
                default: return 15;
            }
        }

        // ------------------------------------------------------------ Bildablauf

        public void begin(Camera cam, float t)
        {
            camera = cam;
            time = t;
            if (cam != null)
            {
                var tr = cam.transform;
                camRight = G(tr.right);
                camUp = G(tr.up);
                toCam = G(-tr.forward);
            }
            sprAlpha.n = 0; sprAdd.n = 0; sprTop.n = 0;
            lines.nv = 0; lines.ni = 0;
            linesTop.nv = 0; linesTop.ni = 0;
            overlay.nv = 0; overlay.ni = 0;
            for (int i = 0; i < used.Count; i++) { used[i].n = 0; free.Push(used[i]); }
            used.Clear();
            batches.Clear();
        }

        static Vector3 G(Vector3 u) => new Vector3(u.x, u.y, -u.z);
        static Vector3 W(float x, float y, float z) => new Vector3(x, y, -z);

        public void end()
        {
            var cam = camera;
            // Grundkörper
            for (int i = 0; i < used.Count; i++)
            {
                var b = used[i];
                if (b.n == 0) continue;
                var mesh = WorldMeshes.Get(b.mesh);
                if (b.mode == 0)
                {
                    b.mpb.SetColor(BaseColorId, b.color);
                    b.mpb.SetColor(ColorId, b.color);
                    if (matLit.enableInstancing && SystemInfo.supportsInstancing)
                        Graphics.DrawMeshInstanced(mesh, 0, matLit, b.m, b.n, b.mpb, ShadowCastingMode.On, true, LAYER, cam);
                    else
                        for (int k = 0; k < b.n; k++) Graphics.DrawMesh(mesh, b.m[k], matLit, LAYER, cam, 0, b.mpb, true, true);
                }
                else
                {
                    var mat = b.mode == 1 ? matUnlitSolid : matTrans;
                    b.mpb.SetColor(BaseColorId, b.color);
                    b.mpb.SetColor(ColorId, b.color);
                    for (int k = 0; k < b.n; k++) Graphics.DrawMesh(mesh, b.m[k], mat, LAYER, cam, 0, b.mpb, false, false);
                }
            }
            Flush(sprAlpha, matSpriteAlpha, cam);
            Flush(lines, matLine, cam);
            Flush(sprAdd, matSpriteAdd, cam);
            Flush(sprTop, matSpriteAdd, cam);
            Flush(linesTop, matLineTop, cam);
            Flush(overlay, matOverlay, cam);
        }

        void Flush(QuadBuf b, Material mat, Camera cam)
        {
            var m = b.mesh;
            m.Clear(true);
            if (b.n == 0) return;
            int nv = b.n * 4;
            m.SetVertices(b.pos, 0, nv);
            m.SetUVs(0, b.uv, 0, nv);
            m.SetColors(b.col, 0, nv);
            m.SetIndices(b.idx, 0, b.n * 6, MeshTopology.Triangles, 0, false);
            m.bounds = HUGE;
            Graphics.DrawMesh(m, Matrix4x4.identity, mat, LAYER, cam, 0, null, false, false);
        }

        void Flush(TriBuf b, Material mat, Camera cam)
        {
            var m = b.mesh;
            m.Clear(true);
            if (b.ni == 0) return;
            m.SetVertices(b.pos, 0, b.nv);
            m.SetColors(b.col, 0, b.nv);
            m.SetIndices(b.idx, 0, b.ni, MeshTopology.Triangles, 0, false);
            m.bounds = HUGE;
            Graphics.DrawMesh(m, Matrix4x4.identity, mat, LAYER, cam, 0, null, false, false);
        }

        // ------------------------------------------------------------ Grundkörper

        /// <summary>
        /// Grundkörper (Renderer.add): pass 'opaque' | 'transparent'. Leuchten (emit) ≥ 0,8 wird unbeleuchtet gezeichnet,
        /// schwächeres Leuchten hellt die Farbe auf. Gleiche Form + Farbe werden gebündelt (Instancing).
        /// </summary>
        public void add(string pass, string mesh, float x, float y, float z, float sx, float sy, float sz, int color, float alpha = 1, float emit = 0, float yaw = 0, float pitch = 0, float roll = 0)
        {
            int mode;
            int col = color & 0xffffff;
            if (pass == "transparent") mode = 2;
            else if (emit >= 0.8f) { mode = 1; col = Col.Mix(col, 0xffffff, Mathf.Min(0.35f, (emit - 0.8f) * 0.3f + 0.08f)); }
            else
            {
                mode = 0;
                if (emit > 0.01f) col = Col.Mix(col, 0xffffff, Mathf.Min(0.4f, emit * 0.35f));
            }
            if (mode != 2) alpha = 1;
            if (alpha <= 0.003f) return;
            int mi = WorldMeshes.Index(mesh);
            int a8 = Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
            long key = ((long)mi << 40) | ((long)mode << 34) | ((long)a8 << 24) | (uint)col;
            if (!batches.TryGetValue(key, out var b) || b.n >= b.m.Length)
            {
                b = free.Count > 0 ? free.Pop() : new Batch();
                b.n = 0;
                b.mesh = mi;
                b.mode = mode;
                var c = Col.ToColor(col, alpha);
                b.color = c;
                batches[key] = b;
                used.Add(b);
            }
            b.m[b.n++] = WorldMeshes.TRS(x, y, z, yaw, pitch, roll, sx, sy, sz);
        }

        // ------------------------------------------------------------ Sprites

        /// <summary>Sprite mit expliziten Achsen (Halbgrößen-Vektoren U/V, Spielkoordinaten).</summary>
        public void sprite(bool additive, int shape, float x, float y, float z, float ux, float uy, float uz, float vx, float vy, float vz, int color, float alpha, float p1 = 0.3f, float p2 = 0.15f, float seed = 0, bool top = false)
        {
            var b = top ? sprTop : additive ? sprAdd : sprAlpha;
            if (b.n >= b.cap) return;
            float a = additive || top ? alpha * glowScale : alpha;
            if (a <= 0.003f) return;
            var rect = tileRect[TileOf(shape, p1, p2, seed)];
            int o = b.n * 4;
            var c = Col.Vert(color, Mathf.Min(1f, a));
            b.pos[o] = W(x - ux - vx, y - uy - vy, z - uz - vz);
            b.pos[o + 1] = W(x + ux - vx, y + uy - vy, z + uz - vz);
            b.pos[o + 2] = W(x + ux + vx, y + uy + vy, z + uz + vz);
            b.pos[o + 3] = W(x - ux + vx, y - uy + vy, z - uz + vz);
            b.uv[o] = new Vector2(rect.x, rect.y);
            b.uv[o + 1] = new Vector2(rect.x + rect.z, rect.y);
            b.uv[o + 2] = new Vector2(rect.x + rect.z, rect.y + rect.w);
            b.uv[o + 3] = new Vector2(rect.x, rect.y + rect.w);
            b.col[o] = c; b.col[o + 1] = c; b.col[o + 2] = c; b.col[o + 3] = c;
            b.n++;
        }

        /// <summary>Kamerazugewandtes Sprite.</summary>
        public void billboard(bool additive, int shape, float x, float y, float z, float size, int color, float alpha, float rot = 0, float stretch = 1, float seed = 0, bool top = false)
        {
            float c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            var r = camRight; var q = camUp;
            float ux = (r.x * c + q.x * s) * size * stretch, uy = (r.y * c + q.y * s) * size * stretch, uz = (r.z * c + q.z * s) * size * stretch;
            float vx = (-r.x * s + q.x * c) * size, vy = (-r.y * s + q.y * c) * size, vz = (-r.z * s + q.z * c) * size;
            sprite(additive, shape, x, y, z, ux, uy, uz, vx, vy, vz, color, alpha, 0.3f, 0.15f, seed, top);
        }

        /// <summary>Flach auf dem Boden liegendes Sprite (Dekale, Ringe, Schockwellen).</summary>
        public void ground(bool additive, int shape, float x, float y, float z, float size, int color, float alpha, float rot = 0, float p1 = 0.3f, float p2 = 0.15f)
        {
            float c = Mathf.Cos(rot) * size, s = Mathf.Sin(rot) * size;
            sprite(additive, shape, x, y, z, c, 0, s, -s, 0, c, color, alpha, p1, p2);
        }

        // ------------------------------------------------------------ Strahlen

        static readonly float[] beamAcross = { -1f, -0.25f, 0f, 0.25f, 1f };
        static readonly float[] beamAlpha = { 0f, 0.712f, 1.6f, 0.712f, 0f };

        /// <summary>Leuchtender Strahl zwischen zwei Punkten (kamerazugewandt, additiv).</summary>
        public void line(float x0, float y0, float z0, float x1, float y1, float z1, float width, int color, float alpha, bool additive = true, bool top = false)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            float len = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-4f) return;
            var t = toCam;
            float px = dy * t.z - dz * t.y, py = dz * t.x - dx * t.z, pz = dx * t.y - dy * t.x;
            float pl = Mathf.Sqrt(px * px + py * py + pz * pz);
            if (pl < 1e-6f) pl = 1;
            px = px / pl * width; py = py / pl * width; pz = pz / pl * width;
            var b = top ? linesTop : lines;
            if (!b.Room(10, 24)) return;
            float a = alpha * glowScale;
            int start = b.nv;
            for (int k = 0; k < 5; k++)
            {
                float f = beamAcross[k];
                var c = Col.Vert(color, Mathf.Clamp01(a * beamAlpha[k]));
                b.V(W(x0 + px * f, y0 + py * f, z0 + pz * f), c);
                b.V(W(x1 + px * f, y1 + py * f, z1 + pz * f), c);
            }
            for (int k = 0; k < 4; k++)
            {
                int i0 = start + k * 2;
                b.T(i0, i0 + 1, i0 + 3);
                b.T(i0, i0 + 3, i0 + 2);
            }
        }

        // ------------------------------------------------------------ Overlay (über allem)

        /// <summary>Kamerazugewandtes Rechteck (Lebensbalken): Breite entlang der waagrechten Bildschirmachse, Höhe senkrecht.</summary>
        public void overlayBox(float x, float y, float z, float w, float h, int color, float alpha, float angle = 0)
        {
            if (!overlay.Room(4, 6)) return;
            float rx = camRight.x, rz = camRight.z;
            float rl = Mathf.Sqrt(rx * rx + rz * rz);
            if (rl < 1e-5f) { rx = 1; rz = 0; rl = 1; }
            rx /= rl; rz /= rl;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            // Achsen in der Bildebene (waagrecht = rechts, senkrecht = Welt-oben), um angle gedreht
            float ax = rx * c, ay = s, az = rz * c;
            float bx = -rx * s, by = c, bz = -rz * s;
            float hw = w / 2, hh = h / 2;
            var col = Col.Vert(color, alpha);
            int i0 = overlay.V(W(x - ax * hw - bx * hh, y - ay * hw - by * hh, z - az * hw - bz * hh), col);
            overlay.V(W(x + ax * hw - bx * hh, y + ay * hw - by * hh, z + az * hw - bz * hh), col);
            overlay.V(W(x + ax * hw + bx * hh, y + ay * hw + by * hh, z + az * hw + bz * hh), col);
            overlay.V(W(x - ax * hw + bx * hh, y - ay * hw + by * hh, z - az * hw + bz * hh), col);
            overlay.T(i0, i0 + 1, i0 + 2);
            overlay.T(i0, i0 + 2, i0 + 3);
        }

        /// <summary>Status-Symbol (Form + Farbe): Dreieck (Kegel), Raute (Oktaeder/Edelstein), Quadrat, Kreis, Ring.</summary>
        public void overlayGlyph(string mesh, float x, float y, float z, float size, float sy, int color, float alpha = 1)
        {
            if (!overlay.Room(18, 48)) return;
            float rx = camRight.x, rz = camRight.z;
            float rl = Mathf.Sqrt(rx * rx + rz * rz);
            if (rl < 1e-5f) { rx = 1; rz = 0; rl = 1; }
            rx /= rl; rz /= rl;
            float hw = size / 2, hh = size * Mathf.Abs(sy) / 2;
            bool flip = sy < 0;
            var col = Col.Vert(color, alpha);
            Vector3 P(float u, float v) => W(x + rx * u * hw, y + v * hh, z + rz * u * hw);
            switch (mesh)
            {
                case "cone4":
                case "cone6":
                case "cone8":
                {
                    float d = flip ? -1 : 1;
                    int i0 = overlay.V(P(0, d), col);
                    overlay.V(P(-1, -d), col);
                    overlay.V(P(1, -d), col);
                    overlay.T(i0, i0 + 1, i0 + 2);
                    break;
                }
                case "octa":
                case "gem":
                {
                    int i0 = overlay.V(P(0, 1), col);
                    overlay.V(P(1, 0), col);
                    overlay.V(P(0, -1), col);
                    overlay.V(P(-1, 0), col);
                    overlay.T(i0, i0 + 1, i0 + 2);
                    overlay.T(i0, i0 + 2, i0 + 3);
                    break;
                }
                case "box":
                case "rbox":
                {
                    int i0 = overlay.V(P(-1, -1), col);
                    overlay.V(P(1, -1), col);
                    overlay.V(P(1, 1), col);
                    overlay.V(P(-1, 1), col);
                    overlay.T(i0, i0 + 1, i0 + 2);
                    overlay.T(i0, i0 + 2, i0 + 3);
                    break;
                }
                case "ring":
                case "ringThin":
                case "torus":
                {
                    int i0 = overlay.nv;
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k / 8f * Mathf.PI * 2;
                        float cu = Mathf.Cos(a), sv = Mathf.Sin(a);
                        overlay.V(P(cu, sv), col);
                        overlay.V(P(cu * 0.55f, sv * 0.55f), col);
                    }
                    for (int k = 0; k < 8; k++)
                    {
                        int a0 = i0 + k * 2, a1 = i0 + ((k + 1) % 8) * 2;
                        overlay.T(a0, a1, a1 + 1);
                        overlay.T(a0, a1 + 1, a0 + 1);
                    }
                    break;
                }
                default:
                {
                    int c0 = overlay.V(P(0, 0), col);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k / 8f * Mathf.PI * 2;
                        overlay.V(P(Mathf.Cos(a), Mathf.Sin(a)), col);
                    }
                    for (int k = 0; k < 8; k++) overlay.T(c0, c0 + 1 + k, c0 + 1 + (k + 1) % 8);
                    break;
                }
            }
        }

        /// <summary>Bildschirmfüllender Blitz und Vignette (Rolle des Nachbearbeitungsschritts). Werte 0..1.</summary>
        public void screenFx(float flash, int flashColor, float vignette, int vignetteColor)
        {
            var cam = camera;
            if (cam == null || !overlay.Room(16, 40)) return;
            var tr = cam.transform;
            float halfH = cam.orthographic ? cam.orthographicSize : Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * (cam.nearClipPlane + 0.05f);
            float halfW = halfH * cam.aspect;
            var c = tr.position + tr.forward * (cam.nearClipPlane + 0.05f);
            Vector3 right = tr.right, up = tr.up;
            Vector3 Pt(float u, float v) => c + right * (u * halfW * 1.02f) + up * (v * halfH * 1.02f);
            if (vignette > 0.003f)
            {
                var outer = Col.Vert(vignetteColor, Mathf.Clamp01(vignette));
                var inner = Col.Vert(vignetteColor, 0f);
                int o = overlay.nv;
                overlay.pos[overlay.nv] = Pt(-1, -1); overlay.col[overlay.nv++] = outer;
                overlay.pos[overlay.nv] = Pt(1, -1); overlay.col[overlay.nv++] = outer;
                overlay.pos[overlay.nv] = Pt(1, 1); overlay.col[overlay.nv++] = outer;
                overlay.pos[overlay.nv] = Pt(-1, 1); overlay.col[overlay.nv++] = outer;
                overlay.pos[overlay.nv] = Pt(-0.6f, -0.5f); overlay.col[overlay.nv++] = inner;
                overlay.pos[overlay.nv] = Pt(0.6f, -0.5f); overlay.col[overlay.nv++] = inner;
                overlay.pos[overlay.nv] = Pt(0.6f, 0.5f); overlay.col[overlay.nv++] = inner;
                overlay.pos[overlay.nv] = Pt(-0.6f, 0.5f); overlay.col[overlay.nv++] = inner;
                for (int k = 0; k < 4; k++)
                {
                    int a0 = o + k, a1 = o + (k + 1) % 4, b0 = o + 4 + k, b1 = o + 4 + (k + 1) % 4;
                    overlay.T(a0, a1, b1);
                    overlay.T(a0, b1, b0);
                }
            }
            if (flash > 0.003f)
            {
                var fc = Col.Vert(flashColor, Mathf.Clamp01(flash));
                int o = overlay.nv;
                overlay.pos[overlay.nv] = Pt(-1, -1); overlay.col[overlay.nv++] = fc;
                overlay.pos[overlay.nv] = Pt(1, -1); overlay.col[overlay.nv++] = fc;
                overlay.pos[overlay.nv] = Pt(1, 1); overlay.col[overlay.nv++] = fc;
                overlay.pos[overlay.nv] = Pt(-1, 1); overlay.col[overlay.nv++] = fc;
                overlay.T(o, o + 1, o + 2);
                overlay.T(o, o + 2, o + 3);
            }
        }
    }
}
