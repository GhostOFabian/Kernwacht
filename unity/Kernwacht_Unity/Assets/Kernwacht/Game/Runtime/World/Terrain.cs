// Übertragung von render/terrain.ts: statische Weltgeometrie – Insel im Meer bzw. schwebende Insel, Gelände mit
// Höhenstufen und Klippen, Wegband mit Randsteinen und Fahrspuren, Bewuchs, Dekor je Biom (Modelle aus dem Katalog),
// Goldadern, Kristalladern, Leylinien, Start- und Kernplattform.
// Unity: alles Statische wird in wenige Meshes gebacken (Farben über eine kleine Palettentextur, damit das URP-Lit-
// Material mit Schatten und Nebel verwendet werden kann).
using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kernwacht.Game
{
    /// <summary>Leuchtpunkt, der jedes Bild als weiches Sprite gezeichnet wird (Kristalle, Lava, Pilze).</summary>
    public class GlowPoint
    {
        public float x, y, z, size;
        public int color;
        public float phase;
        public GlowPoint(float x, float y, float z, float size, int color, float phase) { this.x = x; this.y = y; this.z = z; this.size = size; this.color = color; this.phase = phase; }
    }

    public class SeaInfo
    {
        public float y;
        public bool floating;
        public int color;
        /// <summary>0 Standard, 1 Wasser, 2 Lava, 4 unbeleuchtet (Renderer.ts MAT).</summary>
        public int mat;
        public SeaInfo(float y, bool floating, int color, int mat) { this.y = y; this.floating = floating; this.color = color; this.mat = mat; }
    }

    public static class Terrain
    {
        public const double TOP_STEP = 0.24;
        /// <summary>Maßstab der Hindernis-Dekoration (Bäume, Felsen, Kristalle …) relativ zum Feld.</summary>
        public const double DECOR_SCALE = 0.82;

        public static double cellTop(MapDefinition map, double x, double y)
        {
            if (x < 0 || y < 0 || x >= map.width || y >= map.height) return 0;
            int i = (int)(y * map.width + x);
            double c = map.cells[i];
            if (c == Cell.Path || c == Cell.Spawn || c == Cell.Goal) return -0.04;
            return map.heights[i] * TOP_STEP;
        }

        /// <summary>cellTop für Weltpositionen (Zelle = floor).</summary>
        public static float topAt(MapDefinition map, double x, double y) => (float)cellTop(map, System.Math.Floor(x), System.Math.Floor(y));

        public static readonly Dictionary<string, SeaInfo> SEA = new Dictionary<string, SeaInfo>
        {
            ["water"] = new SeaInfo(-0.72f, false, 0x1e5a7a, 1),
            ["lava"] = new SeaInfo(-0.8f, false, 0xc03810, 2),
            ["ice"] = new SeaInfo(-0.74f, false, 0x7aa8c8, 1),
            ["sand"] = new SeaInfo(-0.5f, false, 0xc8a868, 0),
            ["bog"] = new SeaInfo(-0.68f, false, 0x2e3a24, 1),
            ["clouds"] = new SeaInfo(-4.2f, true, 0xc8d4ec, 4),
            ["abyss"] = new SeaInfo(-9f, true, 0x0e0a1a, 0),
        };

        public static SeaInfo sea(string kind) => kind != null && SEA.TryGetValue(kind, out var s) ? s : SEA["water"];

        internal static uint Mix32(double x) => (uint)RngUtil.mix32(x);

        internal static float rnd(double seed, double k) => (float)((RngUtil.mix32(seed * 7919 + k * 104729) % 10000) / 10000);
    }

    /// <summary>Baut und hält die Geländeobjekte (Meshes, Meer, Dekor-Modelle).</summary>
    public class TerrainView
    {
        readonly Transform root;
        readonly WorldDraw draw;
        readonly ModelPool pool;
        readonly List<ModelInst> props = new List<ModelInst>();
        readonly List<Object> owned = new List<Object>();
        readonly List<GameObject> objects = new List<GameObject>();

        // Aufbaupuffer
        class TBuf
        {
            public readonly List<Vector3> v = new List<Vector3>(65536);
            public readonly List<Vector3> n = new List<Vector3>(65536);
            public readonly List<Vector2> uv = new List<Vector2>(65536);
            public void Clear() { v.Clear(); n.Clear(); uv.Clear(); }
        }
        readonly TBuf lit = new TBuf();
        readonly TBuf glow = new TBuf();
        const int PAL = 128;
        readonly Dictionary<int, int> texel = new Dictionary<int, int>();
        readonly Color32[] palPx = new Color32[PAL * PAL];
        int palN;

        // Zustand während des Aufbaus
        MapDefinition map;
        BiomeDefinition biome;
        BiomePalette pal;
        double salt;
        List<GlowPoint> glows;
        bool usePropModels;

        public TerrainView(Transform parent, WorldDraw draw, ModelPool pool)
        {
            root = new GameObject("Gelaende").transform;
            root.SetParent(parent, false);
            this.draw = draw;
            this.pool = pool;
        }

        public void Clear()
        {
            foreach (var p in props) pool.Release(p);
            props.Clear();
            foreach (var go in objects) if (go != null) Object.Destroy(go);
            objects.Clear();
            foreach (var o in owned) if (o != null) Object.Destroy(o);
            owned.Clear();
        }

        // ------------------------------------------------------------ Bauhelfer

        Vector2 UV(int color)
        {
            color &= 0xffffff;
            if (!texel.TryGetValue(color, out int i))
            {
                if (palN >= PAL * PAL) i = 0;
                else
                {
                    i = palN++;
                    var c = Col.ToColor(color);
                    palPx[i] = new Color32((byte)Mathf.RoundToInt(c.r * 255), (byte)Mathf.RoundToInt(c.g * 255), (byte)Mathf.RoundToInt(c.b * 255), 255);
                    texel[color] = i;
                }
            }
            return new Vector2((i % PAL + 0.5f) / PAL, (i / PAL + 0.5f) / PAL);
        }

        /// <summary>Grundkörper in das statische Gelände backen (Renderer.add im beginStatic-Abschnitt).</summary>
        void Add(string mesh, float x, float y, float z, float sx, float sy, float sz, int color, float emit = 0, float yaw = 0, float pitch = 0, float roll = 0, bool glowMat = false)
        {
            var d = WorldMeshes.Data(mesh);
            var m = WorldMeshes.TRS(x, y, z, yaw, pitch, roll, sx, sy, sz);
            var nm = m.inverse.transpose;
            TBuf b;
            int col = color & 0xffffff;
            if (glowMat || emit >= 0.8f) { b = glow; col = Col.Mix(col, 0xffffff, 0.08); }
            else
            {
                b = lit;
                if (emit > 0.01f) col = Col.Mix(col, 0xffffff, Mathf.Min(0.4f, emit * 0.35f));
            }
            var uv = UV(col);
            for (int i = 0; i < d.v.Length; i++)
            {
                b.v.Add(m.MultiplyPoint3x4(d.v[i]));
                var nn = nm.MultiplyVector(d.n[i]);
                b.n.Add(nn.sqrMagnitude > 1e-12f ? nn.normalized : Vector3.up);
                b.uv.Add(uv);
            }
        }

        /// <summary>Viereck in Spielkoordinaten (a,b,c,d) mit Spielnormale.</summary>
        void Quad(Vector3 a, Vector3 b2, Vector3 c, Vector3 d, Vector3 normal, int color)
        {
            var uv = UV(color);
            var n = new Vector3(normal.x, normal.y, -normal.z);
            Vector3 A = IsoCamera.W(a.x, a.y, a.z), B = IsoCamera.W(b2.x, b2.y, b2.z), C = IsoCamera.W(c.x, c.y, c.z), D = IsoCamera.W(d.x, d.y, d.z);
            lit.v.Add(A); lit.v.Add(B); lit.v.Add(C);
            lit.v.Add(A); lit.v.Add(C); lit.v.Add(D);
            for (int k = 0; k < 6; k++) { lit.n.Add(n); lit.uv.Add(uv); }
        }

        /// <summary>Zellensäule: Deckfläche und nur die sichtbaren Seitenflächen bis zum Nachbarn (oben capCol, darunter Fels).</summary>
        void Column(int x, int y, float top, int capCol, int sideCol, float capBand)
        {
            int w = (int)map.width, h = (int)map.height;
            Quad(new Vector3(x, top, y), new Vector3(x + 1, top, y), new Vector3(x + 1, top, y + 1), new Vector3(x, top, y + 1), Vector3.up, capCol);
            for (int s = 0; s < 4; s++)
            {
                int dx = s == 0 ? 1 : s == 1 ? -1 : 0;
                int dy = s == 2 ? 1 : s == 3 ? -1 : 0;
                int nx = x + dx, ny = y + dy;
                float nt = nx < 0 || ny < 0 || nx >= w || ny >= h ? -0.7f : (float)Terrain.cellTop(map, nx, ny);
                float bottom = Mathf.Max(nt, -0.7f);
                if (bottom >= top - 0.001f) continue;
                float band = Mathf.Max(bottom, top - capBand);
                SideFace(x, y, dx, dy, band, top, capCol);
                if (band > bottom + 0.001f) SideFace(x, y, dx, dy, bottom, band, sideCol);
            }
        }

        void SideFace(int x, int y, int dx, int dy, float y0, float y1, int col)
        {
            Vector3 a, b, n = new Vector3(dx, 0, dy);
            if (dx == 1) { a = new Vector3(x + 1, 0, y); b = new Vector3(x + 1, 0, y + 1); }
            else if (dx == -1) { a = new Vector3(x, 0, y + 1); b = new Vector3(x, 0, y); }
            else if (dy == 1) { a = new Vector3(x + 1, 0, y + 1); b = new Vector3(x, 0, y + 1); }
            else { a = new Vector3(x, 0, y); b = new Vector3(x + 1, 0, y); }
            Quad(new Vector3(a.x, y0, a.z), new Vector3(b.x, y0, b.z), new Vector3(b.x, y1, b.z), new Vector3(a.x, y1, a.z), n, col);
        }

        // ------------------------------------------------------------ Aufbau

        /// <summary>Gelände bauen (buildTerrain). Liefert die Leuchtpunkte.</summary>
        public List<GlowPoint> build(MapDefinition m, int detail)
        {
            Clear();
            map = m;
            biome = Biomes.BIOMES.TryGetValue(m.biome ?? "forest", out var bd) ? bd : Biomes.BIOMES["forest"];
            pal = biome.palette;
            glows = new List<GlowPoint>();
            lit.Clear(); glow.Clear();
            texel.Clear(); palN = 0;
            usePropModels = ModelPool.Catalog != null;
            var seaI = Terrain.sea(biome.sea);
            int w = (int)m.width, h = (int)m.height;
            salt = RngUtil.hashString((m.name ?? "") + m.biome);
            int cliff = (int)pal.cliff;
            int ground = (int)pal.ground, ground2 = (int)pal.ground2;
            int obstacle2 = (int)pal.obstacle2, accent = (int)pal.accent;

            // --- Meer / Wolken / Abgrund rund um die Insel
            BuildSea(seaI, w, h);
            if (biome.sea == "clouds")
            {
                for (int k = 0; k < 70; k++)
                {
                    float a = Terrain.rnd(salt, k) * Mathf.PI * 2;
                    float d = Mathf.Max(w, h) * 0.45f + Terrain.rnd(salt, k + 99) * 22;
                    float s = 2.2f + Terrain.rnd(salt, k + 7) * 3.2f;
                    Add("sphere", w / 2f + Mathf.Cos(a) * d, seaI.y + 0.4f + Terrain.rnd(salt, k + 3), h / 2f + Mathf.Sin(a) * d, s * 1.6f, s * 0.7f, s * 1.3f, Col.Mix(0xdfe8f8, 0xb8c4e0, Terrain.rnd(salt, k + 5)), 0, glowMat: true);
                }
            }
            else if (biome.sea == "abyss")
            {
                for (int k = 0; k < 26; k++)
                {
                    float a = Terrain.rnd(salt, k) * Mathf.PI * 2;
                    float d = Mathf.Max(w, h) * 0.6f + Terrain.rnd(salt, k + 99) * 14;
                    float s = 0.6f + Terrain.rnd(salt, k + 7) * 1.4f;
                    float yy = -2 - Terrain.rnd(salt, k + 3) * 4;
                    float px = w / 2f + Mathf.Cos(a) * d, pz = h / 2f + Mathf.Sin(a) * d;
                    Add("gem", px, yy, pz, s * 1.4f, s, s * 1.2f, obstacle2, 0, a);
                    Add("cone8", px, yy - s * 0.3f, pz, s, -s * 1.6f, s, cliff, 0, a);
                    if (k % 3 == 0) glows.Add(new GlowPoint(px, yy + s * 0.6f, pz, 0.6f, accent, k));
                }
            }
            else if (biome.sea == "ice")
            {
                for (int k = 0; k < 40; k++)
                {
                    float a = Terrain.rnd(salt, k) * Mathf.PI * 2;
                    float d = Mathf.Max(w, h) * 0.55f + Terrain.rnd(salt, k + 99) * 16;
                    float s = 0.8f + Terrain.rnd(salt, k + 7) * 2;
                    Add("cyl8", w / 2f + Mathf.Cos(a) * d, seaI.y - 0.1f, h / 2f + Mathf.Sin(a) * d, s, 0.22f, s * 0.8f, 0xeaf6ff, 0.05f, a);
                }
            }
            else if (biome.sea == "sand")
            {
                for (int k = 0; k < 40; k++)
                {
                    float a = Terrain.rnd(salt, k) * Mathf.PI * 2;
                    float d = Mathf.Max(w, h) * 0.55f + Terrain.rnd(salt, k + 99) * 18;
                    float s = 2 + Terrain.rnd(salt, k + 7) * 4;
                    Add("sphere", w / 2f + Mathf.Cos(a) * d, seaI.y - s * 0.3f, h / 2f + Mathf.Sin(a) * d, s * 1.8f, s * 0.55f, s, Col.Shade(seaI.color, 0.92 + Terrain.rnd(salt, k) * 0.12), 0, a);
                }
            }

            // --- Inselsockel
            if (seaI.floating)
            {
                Add("rbox", w / 2f, -1.25f, h / 2f, w + 0.6f, 1.4f, h + 0.6f, Col.Shade(cliff, 0.85));
                Add("rbox", w / 2f, -2.35f, h / 2f, w - 1.2f, 1.2f, h - 1.2f, Col.Shade(cliff, 0.7));
                Add("rbox", w / 2f, -3.25f, h / 2f, w - 4, 1.0f, h - 4, Col.Shade(cliff, 0.55));
                int n = Mathf.RoundToInt((w + h) * 0.8f);
                for (int k = 0; k < n; k++)
                {
                    float x = 1 + Terrain.rnd(salt, k * 3) * (w - 2);
                    float z = 1 + Terrain.rnd(salt, k * 3 + 1) * (h - 2);
                    float s = 0.8f + Terrain.rnd(salt, k * 3 + 2) * 1.8f;
                    Add("cone8", x, -2.2f - Terrain.rnd(salt, k) * 1.2f, z, s, -s * (1.6f + Terrain.rnd(salt, k + 5) * 2), s, Col.Shade(cliff, 0.5 + Terrain.rnd(salt, k + 9) * 0.2), 0, k);
                }
            }
            else Add("rbox", w / 2f, -0.9f, h / 2f, w + 0.5f, 0.9f, h + 0.5f, Col.Shade(cliff, 0.8));

            // --- Zellen
            bool lush = m.biome == "forest" || m.biome == "swamp" || m.biome == "sky";
            int stoneBase = Col.Mix(cliff, 0xb0a898, 0.45);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double c = m.cells[i];
                    float top = (float)Terrain.cellTop(m, x, y);
                    float n = Terrain.rnd(salt, i);
                    float cx = x + 0.5f, cz = y + 0.5f;
                    if (c == Cell.Path || c == Cell.Spawn || c == Cell.Goal)
                    {
                        int col = Col.Shade((int)pal.path, 0.96 + n * 0.08);
                        Column(x, y, top, col, col, 0);
                        // Fahrspuren: zwei dunkle Streifen in Laufrichtung
                        int rut = Col.Shade((int)pal.path, 0.82);
                        for (int s = 0; s < 4; s++)
                        {
                            int dx = s == 0 ? 1 : s == 1 ? -1 : 0, dy = s == 2 ? 1 : s == 3 ? -1 : 0;
                            if (!IsPath(x + dx, y + dy)) continue;
                            for (int o = 0; o < 2; o++)
                            {
                                float off = o == 0 ? -0.2f : 0.2f;
                                float ox = dy != 0 ? off : dx * 0.25f;
                                float oz = dx != 0 ? off : dy * 0.25f;
                                Add("box", cx + ox, top + 0.004f, cz + oz, dx != 0 ? 0.5f : 0.07f, 0.01f, dy != 0 ? 0.5f : 0.07f, rut);
                            }
                        }
                        // Randsteine zur Wiese hin
                        for (int s = 0; s < 4; s++)
                        {
                            int dx = s == 0 ? 1 : s == 1 ? -1 : 0, dy = s == 2 ? 1 : s == 3 ? -1 : 0;
                            if (IsPath(x + dx, y + dy) || x + dx < 0 || y + dy < 0 || x + dx >= w || y + dy >= h) continue;
                            if (m.cells[(y + dy) * w + x + dx] == Cell.Blocked && detail < 2) continue;
                            int stones = detail >= 2 ? 3 : 2;
                            for (int k = 0; k < stones; k++)
                            {
                                float t = (k + 0.5f) / stones - 0.5f + (Terrain.rnd(salt, i * 13 + k + dx * 3 + dy * 5) - 0.5f) * 0.12f;
                                float sx = cx + dx * 0.44f + (dy != 0 ? t : 0);
                                float sz = cz + dy * 0.44f + (dx != 0 ? t : 0);
                                float sc = 0.14f + Terrain.rnd(salt, i * 17 + k + dx + dy * 2) * 0.1f;
                                Add("rbox", sx, top + sc * 0.25f, sz, sc * 1.2f, sc * 0.7f, sc, Col.Shade(stoneBase, 0.85 + Terrain.rnd(salt, i + k) * 0.3), 0, Terrain.rnd(salt, i * 5 + k) * 3);
                            }
                        }
                        continue;
                    }
                    // Gelände: Sockel (Fels) + Deckschicht mit weicher Lippe an Abbruchkanten
                    bool hi = m.heights[i] >= 2;
                    int capCol = hi ? (int)pal.high : (x * 7 + y * 3) % 5 == 0 ? ground2 : ground;
                    if (c == Cell.NoBuild) capCol = Col.Shade(capCol, 0.86);
                    capCol = Col.Shade(Col.Mix(capCol, ground2, n * 0.5), 0.97 + n * 0.06);
                    int sideCol = Col.Shade(Col.Mix(cliff, capCol, 0.35), 0.9 + n * 0.1);
                    Column(x, y, top, capCol, sideCol, 0.16f);
                    for (int s = 0; s < 4; s++)
                    {
                        int dx = s == 0 ? 1 : s == 1 ? -1 : 0, dy = s == 2 ? 1 : s == 3 ? -1 : 0;
                        float nt = x + dx < 0 || y + dy < 0 || x + dx >= w || y + dy >= h ? -1 : (float)Terrain.cellTop(m, x + dx, y + dy);
                        if (nt < top - 0.1f)
                            Add("rbox", cx + dx * 0.47f, top - 0.07f, cz + dy * 0.47f, dx != 0 ? 0.16f : 1.02f, 0.17f, dy != 0 ? 0.16f : 1.02f, Col.Shade(capCol, 1.04));
                    }
                    if (c == Cell.Blocked)
                    {
                        AddDecorBlock(i, cx, top, cz);
                        continue;
                    }
                    // Bewuchs / Bodendetails
                    if (detail <= 0) continue;
                    int tufts = lush ? (detail >= 2 ? 3 : 2) : detail >= 2 ? 2 : 1;
                    for (int k = 0; k < tufts; k++)
                    {
                        float a = Terrain.rnd(salt, i * 31 + k);
                        float b = Terrain.rnd(salt, i * 37 + k + 11);
                        float px = x + 0.15f + a * 0.7f;
                        float pz = y + 0.15f + b * 0.7f;
                        AddGroundDetail(m.biome, px, top, pz, Terrain.rnd(salt, i * 41 + k), capCol, accent);
                    }
                }
            }

            // --- Ressourcen
            foreach (var vi in m.veins)
            {
                int x = (int)(vi % w), y = (int)System.Math.Floor(vi / w);
                float top = (float)Terrain.cellTop(m, x, y);
                Add("gem", x + 0.25f, top + 0.06f, y + 0.3f, 0.2f, 0.16f, 0.2f, 0xf0c040, 0.5f, 0.3f);
                Add("gem", x + 0.72f, top + 0.05f, y + 0.66f, 0.15f, 0.13f, 0.15f, 0xf0c040, 0.5f, 1.1f);
                Add("gem", x + 0.7f, top + 0.04f, y + 0.24f, 0.12f, 0.1f, 0.12f, 0xffe080, 0.5f);
                glows.Add(new GlowPoint(x + 0.5f, top + 0.15f, y + 0.5f, 0.35f, 0xffd060, (float)vi));
            }
            if (m.deposits != null)
                foreach (var di in m.deposits)
                {
                    int x = (int)(di % w), y = (int)System.Math.Floor(di / w);
                    float top = (float)Terrain.cellTop(m, x, y);
                    Add("octa", x + 0.3f, top + 0.12f, y + 0.35f, 0.16f, 0.3f, 0.16f, 0x7ae0ff, 0.35f, 0.3f, 0.2f);
                    Add("octa", x + 0.68f, top + 0.1f, y + 0.62f, 0.13f, 0.24f, 0.13f, 0x5ac0f0, 0.35f, 1.1f, -0.25f);
                    Add("octa", x + 0.62f, top + 0.08f, y + 0.26f, 0.1f, 0.18f, 0.1f, 0xb0f0ff, 0.35f, 0.6f, 0.3f);
                    Add("disc", x + 0.5f, top + 0.015f, y + 0.5f, 0.43f, 1, 0.43f, 0x2a5a7a);
                    glows.Add(new GlowPoint(x + 0.5f, top + 0.15f, y + 0.5f, 0.3f, 0x7ae0ff, (float)(di * 1.3)));
                }
            foreach (var li in m.ley)
            {
                int x = (int)(li % w), y = (int)System.Math.Floor(li / w);
                float top = (float)Terrain.cellTop(m, x, y);
                Add("ring", x + 0.5f, top + 0.02f, y + 0.5f, 0.42f, 1, 0.42f, Col.Mix(0xc070ff, 0xffffff, 0.1), 1, glowMat: true);
                glows.Add(new GlowPoint(x + 0.5f, top + 0.1f, y + 0.5f, 0.5f, 0xc070ff, (float)(li * 0.7)));
            }

            // --- Ränder außerhalb der Karte: Uferfelsen, Inselchen
            var kinds = biome.decorKinds;
            for (int y = -4; y < h + 4; y++)
            {
                for (int x = -4; x < w + 4; x++)
                {
                    if (x >= 0 && y >= 0 && x < w && y < h) continue;
                    uint hsh = Terrain.Mix32((double)((uint)salt ^ (uint)((x + 100) * 73856093) ^ (uint)((y + 100) * 19349663)));
                    int d = Mathf.Max(x < 0 ? -x : x >= w ? x - w + 1 : 0, y < 0 ? -y : y >= h ? y - h + 1 : 0);
                    if (hsh % 100 < (seaI.floating ? 40 + d * 22 : 14 + d * 16)) continue;
                    float lift = seaI.floating ? -0.3f - d * 0.25f : seaI.y + 0.35f - d * 0.12f + (hsh % 7) * 0.05f;
                    int col = Col.Shade(hsh % 2 == 1 ? ground : ground2, 0.85 - d * 0.06);
                    Add("rbox", x + 0.5f, lift - 0.5f, y + 0.5f, 0.98f, 1, 0.98f, col);
                    if (seaI.floating) Add("cone8", x + 0.5f, lift - 1, y + 0.5f, 0.9f, -1.2f - (hsh % 5) * 0.3f, 0.9f, Col.Shade(cliff, 0.6));
                    if (hsh % 4 == 0 && kinds.Count > 0)
                        AddDecorKind((int)kinds[(int)(hsh % (uint)kinds.Count)], x + 0.5f, lift, y + 0.5f, (hsh % 628) / 100f, 0.8f, (int)(hsh % 97));
                }
            }

            // --- Startsockel und Kernplattform
            foreach (var sp in WorldGenerator.spawnsOf(m))
            {
                Add("cyl16", (float)sp.x + 0.5f, -0.06f, (float)sp.y + 0.5f, 1.0f, 0.1f, 1.0f, 0x2a1838);
                Add("rbox", (float)sp.x + 0.5f + 0.48f, 0.3f, (float)sp.y + 0.5f, 0.16f, 0.7f, 0.16f, Col.Shade(cliff, 1.1));
                Add("rbox", (float)sp.x + 0.5f - 0.48f, 0.3f, (float)sp.y + 0.5f, 0.16f, 0.7f, 0.16f, Col.Shade(cliff, 1.1));
            }
            var gp = m.goal;
            float gx = (float)gp.x + 0.5f, gz = (float)gp.y + 0.5f;
            Add("cyl16", gx, -0.06f, gz, 1.0f, 0.16f, 1.0f, Col.Shade(cliff, 1.15));
            Add("hex", gx, 0.08f, gz, 0.72f, 0.1f, 0.72f, Col.Shade(cliff, 1.35));
            for (int k = 0; k < 4; k++)
            {
                float a = k / 4f * Mathf.PI * 2 + Mathf.PI / 4;
                Add("rbox", gx + Mathf.Cos(a) * 0.42f, 0.25f, gz + Mathf.Sin(a) * 0.42f, 0.12f, 0.5f, 0.12f, Col.Shade(cliff, 1.3), 0, -a);
            }

            Finish();
            return glows;
        }

        bool IsPath(int x, int y)
        {
            int w = (int)map.width, h = (int)map.height;
            if (x < 0 || y < 0 || x >= w || y >= h) return false;
            double c = map.cells[y * w + x];
            return c == Cell.Path || c == Cell.Spawn || c == Cell.Goal || (map.variant == "mazing" && c == Cell.NoBuild);
        }

        void AddDecorBlock(int i, float x, float top, float z)
        {
            int kind = biome.decorKinds.Count > 0 ? (int)biome.decorKinds[0] : 1;
            float rot = 0;
            var d = map.decor;
            for (int k = 0; k + 2 < d.Count; k += 3)
            {
                if (d[k] == i)
                {
                    kind = (int)d[k + 1];
                    rot = (float)(d[k + 2] / 100);
                    break;
                }
            }
            // 2.9.5: Bäume/Felsen etwas kleiner (wie die Modelle), damit sie Türme auf Nachbarfeldern nicht verdecken
            AddDecorKind(kind, x, top, z, rot, kind == 3 || kind == 7 ? 1 : (float)Terrain.DECOR_SCALE, i);
        }

        /// <summary>Dekor als Modell aus dem Katalog (Rückfall: Grundkörper wie terrain.ts addDecorKind). Leuchtpunkte immer.</summary>
        void AddDecorKind(int kind, float x, float y, float z, float rot, float s, int variant)
        {
            // Leuchtpunkte wie im Webspiel
            if (kind == 2) glows.Add(new GlowPoint(x, y + 0.6f * s, z, 0.9f * s, (int)pal.accent, rot * 3));
            else if (kind == 3) glows.Add(new GlowPoint(x, y + 0.1f, z, 0.9f * s, 0xff6a20, rot * 2));
            else if (kind == 9) glows.Add(new GlowPoint(x, y + 0.7f * s, z, 1.0f * s, (int)pal.accent, rot * 4));
            string id = WorldModels.propFor(kind, variant);
            if (usePropModels && ModelPool.Has(id))
            {
                var inst = pool.Acquire(id, animate: false, at: IsoCamera.W(x, y, z));
                inst.tr.rotation = Quaternion.Euler(0, -rot * Mathf.Rad2Deg, 0);
                inst.Place(x, y, z, WorldModels.propHeight(kind) * s);
                props.Add(inst);
                return;
            }
            AddDecorPrimitive(kind, x, y, z, rot, s);
        }

        void AddDecorPrimitive(int kind, float x, float y, float z, float rot, float s)
        {
            int obstacle = (int)pal.obstacle, obstacle2 = (int)pal.obstacle2, accent = (int)pal.accent, groundC = (int)pal.ground;
            switch (kind)
            {
                case 0: // Laubbaum mit runder Krone
                    Add("cyl8", x, y, z, 0.18f * s, 0.6f * s, 0.18f * s, 0x6a4a2a, 0, rot);
                    Add("sphere", x, y + 0.75f * s, z, 0.78f * s, 0.66f * s, 0.78f * s, obstacle, 0, rot);
                    Add("sphere", x + 0.18f * s, y + 0.98f * s, z - 0.1f * s, 0.52f * s, 0.46f * s, 0.52f * s, Col.Shade(obstacle, 1.18), 0, rot);
                    Add("sphere", x - 0.16f * s, y + 0.62f * s, z + 0.16f * s, 0.46f * s, 0.4f * s, 0.46f * s, Col.Shade(obstacle, 0.88), 0, rot);
                    break;
                case 1: // Fels
                    Add("gem", x, y + 0.18f * s, z, 0.82f * s, 0.56f * s, 0.7f * s, obstacle2, 0, rot);
                    Add("gem", x + 0.24f * s, y + 0.08f * s, z - 0.22f * s, 0.38f * s, 0.3f * s, 0.36f * s, Col.Shade(obstacle2, 0.82), 0, rot + 1);
                    Add("sphereLo", x - 0.1f * s, y + 0.38f * s, z + 0.05f * s, 0.3f * s, 0.08f * s, 0.25f * s, Col.Shade(groundC, 1.05), 0, rot);
                    break;
                case 2: // Kristallgruppe
                    Add("octa", x, y + 0.5f * s, z, 0.34f * s, 1.1f * s, 0.34f * s, accent, 0.55f, rot, 0.12f);
                    Add("octa", x + 0.24f * s, y + 0.26f * s, z + 0.1f * s, 0.2f * s, 0.6f * s, 0.2f * s, obstacle, 0.45f, rot, -0.35f);
                    Add("octa", x - 0.2f * s, y + 0.2f * s, z - 0.14f * s, 0.16f * s, 0.45f * s, 0.16f * s, accent, 0.45f, rot + 1, 0.4f);
                    break;
                case 3: // Lavabecken
                    Add("cyl16", x, y - 0.02f, z, 0.86f * s, 0.05f, 0.86f * s, 0xff6a20, 1);
                    Add("gem", x + 0.3f * s, y + 0.1f, z + 0.22f * s, 0.32f * s, 0.26f * s, 0.32f * s, obstacle, 0, rot);
                    Add("gem", x - 0.32f * s, y + 0.06f, z - 0.2f * s, 0.24f * s, 0.2f * s, 0.24f * s, obstacle, 0, rot + 2);
                    break;
                case 4: // Eisblock
                    Add("rbox", x, y + 0.28f * s, z, 0.72f * s, 0.56f * s, 0.62f * s, 0xc8ecff, 0.12f, rot);
                    Add("octa", x + 0.2f * s, y + 0.6f * s, z - 0.1f * s, 0.22f * s, 0.6f * s, 0.22f * s, 0xe8f8ff, 0.25f, rot, 0.25f);
                    break;
                case 5: // Ruine
                    Add("cyl12", x - 0.18f * s, y, z, 0.3f * s, 0.95f * s, 0.3f * s, obstacle2, 0, rot);
                    Add("rbox", x - 0.18f * s, y + 1.0f * s, z, 0.38f * s, 0.1f * s, 0.38f * s, Col.Shade(obstacle2, 1.1), 0, rot);
                    Add("rbox", x + 0.18f * s, y + 0.12f, z + 0.16f * s, 0.44f * s, 0.24f * s, 0.3f * s, obstacle, 0, rot);
                    Add("cyl12", x + 0.1f * s, y + 0.1f, z - 0.22f * s, 0.2f * s, 0.6f * s, 0.2f * s, obstacle2, 0, rot, 0, Mathf.PI / 2);
                    break;
                case 6: // Pilz
                    Add("cyl8", x, y, z, 0.16f * s, 0.38f * s, 0.16f * s, 0xe8dcc0);
                    Add("sphere", x, y + 0.42f * s, z, 0.56f * s, 0.32f * s, 0.56f * s, 0xd04a4a);
                    Add("sphereLo", x + 0.12f * s, y + 0.55f * s, z + 0.06f * s, 0.07f * s, 0.05f * s, 0.07f * s, 0xffffff);
                    Add("sphereLo", x - 0.1f * s, y + 0.54f * s, z - 0.1f * s, 0.06f * s, 0.05f * s, 0.06f * s, 0xffffff);
                    break;
                case 7: // Wolke / Nebelbank
                    Add("sphere", x, y + 0.25f, z, 0.72f * s, 0.42f * s, 0.62f * s, 0xf0f4ff, 0.2f);
                    Add("sphere", x + 0.25f, y + 0.32f, z + 0.1f, 0.5f * s, 0.38f * s, 0.48f * s, 0xffffff, 0.2f);
                    break;
                case 8: // Trauerweide / Schilf
                    Add("cyl8", x, y, z, 0.2f * s, 0.7f * s, 0.2f * s, 0x4a3a28, 0, rot, 0.08f);
                    Add("sphere", x, y + 0.9f * s, z, 0.9f * s, 0.5f * s, 0.9f * s, obstacle, 0, rot);
                    for (int k = 0; k < 5; k++)
                    {
                        float a = rot + k / 5f * Mathf.PI * 2;
                        Add("cone6", x + Mathf.Cos(a) * 0.36f * s, y + 0.95f * s, z + Mathf.Sin(a) * 0.36f * s, 0.14f * s, -0.6f * s, 0.14f * s, Col.Shade(obstacle, 1.15), 0, a);
                    }
                    break;
                case 9: // Leuchtpilz
                    Add("cyl8", x, y, z, 0.14f * s, 0.7f * s, 0.14f * s, 0xd8d0e8, 0.1f, 0, 0.06f);
                    Add("sphere", x, y + 0.72f * s, z, 0.76f * s, 0.34f * s, 0.76f * s, accent, 0.8f);
                    Add("cyl8", x + 0.28f * s, y, z + 0.2f * s, 0.08f * s, 0.32f * s, 0.08f * s, 0xd8d0e8);
                    Add("sphere", x + 0.28f * s, y + 0.34f * s, z + 0.2f * s, 0.3f * s, 0.14f * s, 0.3f * s, 0xc080ff, 0.8f);
                    break;
            }
        }

        void AddGroundDetail(string bio, float x, float y, float z, float n, int groundC, int accent)
        {
            switch (bio)
            {
                case "forest":
                case "sky":
                {
                    int col = Col.Shade(Col.Mix(groundC, 0xd8ff90, 0.35), 1.05 + n * 0.25);
                    Add("cone4", x, y - 0.02f, z, 0.14f, 0.16f + n * 0.08f, 0.14f, col, 0.12f, n * 6, 0.15f);
                    Add("cone4", x + 0.07f, y - 0.02f, z + 0.03f, 0.1f, 0.12f + n * 0.06f, 0.1f, Col.Shade(col, 1.1), 0.12f, n * 3, -0.25f, 0.1f);
                    if (n > 0.85f) Add("sphereLo", x - 0.05f, y + 0.18f, z - 0.04f, 0.07f, 0.07f, 0.07f, bio == "sky" ? 0xffe0f0 : 0xfff090, 0.3f);
                    break;
                }
                case "swamp":
                {
                    int col = Col.Shade(0x7a8a46, 0.95 + n * 0.3);
                    Add("cone4", x, y, z, 0.05f, 0.4f + n * 0.25f, 0.05f, col, 0, n * 6, 0.1f);
                    Add("cone4", x + 0.05f, y, z, 0.04f, 0.3f + n * 0.2f, 0.04f, Col.Shade(col, 1.1), 0, n, -0.2f);
                    if (n > 0.8f) Add("cyl8", x, y + 0.4f, z, 0.05f, 0.12f, 0.05f, 0x5a3a20);
                    break;
                }
                case "frozen":
                    Add("sphereLo", x, y, z, 0.2f + n * 0.15f, 0.08f, 0.16f + n * 0.1f, 0xf4faff, 0.05f, n * 5);
                    break;
                case "volcano":
                    if (n > 0.55f)
                    {
                        Add("box", x, y + 0.002f, z, 0.4f, 0.01f, 0.05f, 0xff6020, 1, n * 6);
                        if (n > 0.85f) glows.Add(new GlowPoint(x, y + 0.05f, z, 0.3f, 0xff6020, n * 10));
                    }
                    else Add("gem", x, y + 0.03f, z, 0.12f, 0.08f, 0.1f, 0x2a2224, 0, n * 6);
                    break;
                case "crystal":
                    if (n > 0.5f) Add("octa", x, y + 0.06f, z, 0.07f, 0.18f, 0.07f, accent, 0.5f, n * 6, 0.2f);
                    else Add("gem", x, y + 0.02f, z, 0.12f, 0.06f, 0.1f, 0xb89860, 0, n * 6);
                    break;
                case "cavern":
                    if (n > 0.6f)
                    {
                        Add("cyl8", x, y, z, 0.03f, 0.1f, 0.03f, 0xd0c8e0);
                        Add("sphereLo", x, y + 0.1f, z, 0.1f, 0.05f, 0.1f, accent, 0.9f);
                        if (n > 0.8f) glows.Add(new GlowPoint(x, y + 0.12f, z, 0.22f, accent, n * 9));
                    }
                    else Add("gem", x, y + 0.02f, z, 0.1f, 0.06f, 0.08f, 0x4a4466, 0, n * 6);
                    break;
            }
        }

        // ------------------------------------------------------------ Meer

        void BuildSea(SeaInfo s, int w, int h)
        {
            float span = Mathf.Max(w, h) + 70;
            float cx = w / 2f, cz = h / 2f;
            var pal2 = biome.palette;
            int fogCol = Col.Mix((int)pal2.sky, (int)pal2.fog, 0.5);
            // Verlaufstextur: Meeresfarbe → Dunst am Rand
            var tex = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "kw_sea" };
            owned.Add(tex);
            for (int i = 0; i < 64; i++)
            {
                float t = i / 63f;
                int c = s.mat == 1 ? Col.Mix(Col.Shade(s.color, 1.05), Col.Shade(s.color, 0.8), Mathf.Clamp01(t * 1.6f)) : s.color;
                c = Col.Mix(c, fogCol, Mathf.Clamp01((t - 0.45f) / 0.55f) * (s.floating ? 0.85f : 0.7f));
                tex.SetPixel(i, 0, Col.ToColor(c));
            }
            tex.Apply(false, true);
            const int RINGS = 12, SEG = 48;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var nrm = new List<Vector3>();
            var tri = new List<int>();
            v.Add(IsoCamera.W(cx, s.y, cz)); uv.Add(new Vector2(0, 0.5f)); nrm.Add(Vector3.up);
            for (int r = 1; r <= RINGS; r++)
            {
                float f = r / (float)RINGS;
                float rad = span * 0.5f * f;
                for (int k = 0; k < SEG; k++)
                {
                    float a = k / (float)SEG * Mathf.PI * 2;
                    v.Add(IsoCamera.W(cx + Mathf.Cos(a) * rad, s.y, cz + Mathf.Sin(a) * rad));
                    uv.Add(new Vector2(f, 0.5f));
                    nrm.Add(Vector3.up);
                }
            }
            for (int k = 0; k < SEG; k++) { tri.Add(0); tri.Add(1 + (k + 1) % SEG); tri.Add(1 + k); }
            for (int r = 1; r < RINGS; r++)
                for (int k = 0; k < SEG; k++)
                {
                    int a0 = 1 + (r - 1) * SEG + k, a1 = 1 + (r - 1) * SEG + (k + 1) % SEG;
                    int b0 = a0 + SEG, b1 = a1 + SEG;
                    tri.Add(a0); tri.Add(a1); tri.Add(b1);
                    tri.Add(a0); tri.Add(b1); tri.Add(b0);
                }
            var mesh = new Mesh { name = "kw_sea" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(nrm); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            Material mat;
            bool unlit = s.mat == 2 || s.mat == 4 || biome.sea == "abyss";
            if (unlit)
            {
                mat = new Material(draw.matUnlitSolid) { name = "kw_seaMat" };
                mat.renderQueue = 1990;
            }
            else
            {
                mat = new Material(draw.matLit) { name = "kw_seaMat" };
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", s.mat == 1 ? 0.82f : 0.15f);
            }
            SetTex(mat, tex);
            owned.Add(mat);
            var go = new GameObject("Meer");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = !unlit;
            objects.Add(go);
        }

        static void SetTex(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        }

        // ------------------------------------------------------------ Abschluss: Meshes anlegen

        void Finish()
        {
            var tex = new Texture2D(PAL, PAL, TextureFormat.RGBA32, false) { name = "kw_terrainPalette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(palPx);
            tex.Apply(false, true);
            owned.Add(tex);

            var litMat = new Material(draw.matLit) { name = "kw_terrain" };
            if (litMat.HasProperty("_Smoothness")) litMat.SetFloat("_Smoothness", 0.08f);
            SetTex(litMat, tex);
            owned.Add(litMat);
            var glowMat = new Material(draw.matUnlitSolid) { name = "kw_terrainGlow" };
            SetTex(glowMat, tex);
            owned.Add(glowMat);

            MakeObject("Gelaende_Licht", lit, litMat, true);
            MakeObject("Gelaende_Leuchten", glow, glowMat, false);
            lit.Clear();
            glow.Clear();
        }

        void MakeObject(string name, TBuf b, Material mat, bool shadows)
        {
            if (b.v.Count == 0) return;
            // In Stücke von höchstens ~60 000 Ecken teilen (16-Bit-Indizes, schneller auf schwachen GPUs)
            const int CHUNK = 60000;
            for (int start = 0; start < b.v.Count; start += CHUNK)
            {
                int n = Mathf.Min(CHUNK, b.v.Count - start);
                n -= n % 3;
                if (n <= 0) break;
                var mesh = new Mesh { name = "kw_" + name };
                mesh.SetVertices(b.v, start, n);
                mesh.SetNormals(b.n, start, n);
                mesh.SetUVs(0, b.uv, start, n);
                var tri = new int[n];
                for (int i = 0; i < n; i++) tri[i] = i;
                mesh.SetTriangles(tri, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                owned.Add(mesh);
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = shadows;
                objects.Add(go);
            }
        }
    }
}
