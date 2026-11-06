// Übersetzung von sim/WorldGenerator.ts.
// Prozeduraler Weltgenerator. Getrennte Zufallsströme:
//   layout   – Wegeführung/Hindernisse (taktisch relevant)
//   res      – Goldadern, Leylinien (taktisch relevant)
//   height   – Geländehöhen (Anhöhen geben Reichweite)
//   decor    – rein kosmetisch (beeinflusst nie die Balance)
//   meta     – Biom und Geländeereignis
//
// PORT: Float32Array (noise, Höhengitter) → double[] mit expliziter float32-Rundung ((double)(float)x) beim Schreiben,
// damit die Werte bitgenau denen der TS-Version entsprechen (Math.fround-Semantik).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class GenerateOptions
    {
        public WorldSeed seed;
        public string variant;   // MapVariant
        public string biome;     // BiomeId | 'random' | null (undefined)
        public string size;      // MapSize oder null
    }

    public class ValidationResult
    {
        public bool ok;
        public List<string> errors = new List<string>();
        public double pathLength;
        public double buildable;
    }

    /// <summary>TS: Ad-hoc-Typ <c>{ w: number; h: number }</c>.</summary>
    public class MapDims
    {
        public double w;
        public double h;
        public MapDims() { }
        public MapDims(double w, double h) { this.w = w; this.h = h; }
    }

    public static partial class WorldGenerator
    {
        public const double MAP_VERSION = 1;

        public static WorldSeed makeSeed(string text)
        {
            string t = RngUtil.normalizeSeedText(text);
            return new WorldSeed { text = t, hash = RngUtil.hashString(t) };
        }

        private static readonly Dictionary<string, Dictionary<string, MapDims>> DIMS = new Dictionary<string, Dictionary<string, MapDims>>
        {
            // Unity 1.1: alle Größen etwas größer (≈ +12–15 % Kantenlänge)
            { "normal", new Dictionary<string, MapDims> { { "classic", new MapDims(24, 16) }, { "mazing", new MapDims(22, 16) } } },
            { "large", new Dictionary<string, MapDims> { { "classic", new MapDims(36, 22) }, { "mazing", new MapDims(34, 22) } } },
            { "huge", new Dictionary<string, MapDims> { { "classic", new MapDims(48, 30) }, { "mazing", new MapDims(46, 30) } } },
        };

        public static MapDims mapDims(string variant, string size = "normal")
        {
            return DIMS[size][variant];
        }

        /// <summary>Alle Startpunkte einer Karte (Rückwärtskompatibel zu Karten mit nur spawn).</summary>
        public static List<GridPos> spawnsOf(MapDefinition map)
        {
            return map.spawns != null && map.spawns.Count > 0 ? map.spawns : new List<GridPos> { map.spawn };
        }

        public static MapDefinition generateWorld(GenerateOptions opts)
        {
            double h = opts.seed.hash;
            Rng meta = new Rng(RngUtil.hashSeed(h, "meta"));
            string biome = string.IsNullOrEmpty(opts.biome) || opts.biome == "random" ? meta.pick(Biomes.BIOME_ORDER) : opts.biome;
            BiomeDefinition bdef = Biomes.BIOMES[biome];
            string terrainEvent = meta.chance(0.8) ? meta.pick(bdef.events) : null;
            string size = opts.size ?? "normal";
            // Normale Karten behalten den bisherigen Zufallsstrom (Seeds bleiben reproduzierbar wie zuvor).
            Rng layout = new Rng(RngUtil.hashSeed(h, size == "normal" ? "layout" : $"layout-{size}"));
            MapDims dims = DIMS[size][opts.variant];
            double w = dims.w;
            double hh = dims.h;

            MapDefinition map = null;
            // Begrenzte Anzahl an Versuchen – keine Endlosschleifen möglich.
            for (int attempt = 0; attempt < 40 && map == null; attempt++)
            {
                MapDefinition candidate = opts.variant == "classic"
                    ? carveClassic(layout, w, hh, biome, bdef.obstacleDensity, size)
                    : carveMazing(layout, w, hh, biome, bdef.obstacleDensity * (size == "normal" ? 1 : 0.6));
                if (candidate != null) candidate.size = size;
                if (candidate != null && validateMap(candidate).ok) map = candidate;
            }
            if (map == null)
            {
                map = fallbackMap(opts.variant, biome);
            }
            map.terrainEvent = terrainEvent;
            assignHeights(map, new Rng(RngUtil.hashSeed(h, "height")), bdef.heightAmp);
            assignResources(map, new Rng(RngUtil.hashSeed(h, "res")));
            assignDecor(map, new Rng(RngUtil.hashSeed(h, "decor")), bdef.decorKinds);
            string sizeName = map.size == "huge" ? " · Riesig" : map.size == "large" ? " · Groß" : "";
            map.name = $"{bdef.name} · {(opts.variant == "classic" ? "Classic" : "Mazing")}{sizeName}";
            return map;
        }

        private static MapDefinition emptyMap(double w, double h, string variant, string biome)
        {
            return new MapDefinition
            {
                version = MAP_VERSION, width = w, height = h, variant = variant, biome = biome,
                cells = U.Fill(w * h, Cell.Open),
                heights = U.Fill(w * h, 0.0),
                veins = new List<double>(), ley = new List<double>(), decor = new List<double>(),
                spawn = new GridPos(0, 0), spawns = new List<GridPos> { new GridPos(0, 0) }, size = "normal", goal = new GridPos(w - 1, h - 1),
                terrainEvent = null, fallback = false, name = "",
            };
        }

        // ---------------------------------------------------------------- Classic

        private static MapDefinition carveClassic(Rng rng, double w, double h, string biome, double density, string size = "normal")
        {
            MapDefinition map = emptyMap(w, h, "classic", biome);
            double sy = rng.@int(2, h - 3);
            double gy = rng.@int(2, h - 3);
            GridPos spawn = new GridPos(0, sy);
            GridPos goal = new GridPos(w - 1, gy);
            double scale = w / 22;

            // Wegpunkte mit gelegentlichen Rückschleifen für taktisch interessante Kurven.
            double count = U.Round(rng.@int(3, 5) * scale);
            List<GridPos> pts = new List<GridPos> { new GridPos(1, sy) };
            for (int i = 0; i < count; i++)
            {
                double @base = 2 + U.Round(((i + 1) / (count + 1)) * (w - 5));
                double x = @base + rng.@int(-2, 2);
                if (i > 0 && rng.chance(0.25)) x = U.Max(2, pts[pts.Count - 1].x - rng.@int(1, 3));
                x = U.Min(w - 3, U.Max(2, x));
                double y = rng.@int(1, h - 2);
                pts.Add(new GridPos(x, y));
            }
            pts.Add(new GridPos(w - 2, gy));

            double[] noise = new double[(int)(w * h)];
            for (int i = 0; i < noise.Length; i++) noise[i] = (double)(float)(rng.next() * 2.2);

            List<GridPos> path = new List<GridPos> { spawn };
            byte[] inPath = new byte[(int)(w * h)];
            inPath[(int)(sy * w)] = 1;
            foreach (GridPos target in pts)
            {
                List<GridPos> seg = astarSegment(map, path, inPath, target, noise);
                if (seg == null) return null;
                foreach (GridPos c in seg)
                {
                    path.Add(c);
                    inPath[(int)(c.y * w + c.x)] = 1;
                }
            }
            // Letzter Schritt zum Ziel (rechter Rand).
            GridPos head = path[path.Count - 1];
            if (head.y != gy || head.x != w - 2) return null;
            path.Add(goal);
            inPath[(int)(gy * w + w - 1)] = 1;

            double areaScale = (w * h) / (22 * 14);
            if (path.Count < 38 * scale || path.Count > 110 * U.Max(1, areaScale * 0.8)) return null;
            if (!pathIsClean(path)) return null;

            foreach (GridPos c in path) map.cells[(int)(c.y * w + c.x)] = Cell.Path;
            map.cells[(int)(sy * w)] = Cell.Spawn;
            map.cells[(int)(gy * w + w - 1)] = Cell.Goal;
            map.spawn = spawn;
            map.spawns = new List<GridPos> { spawn };
            map.goal = goal;

            // Große Karten: zweiter Startpunkt am oberen/unteren Rand, der in den Hauptweg mündet (Weggabelung).
            if (size != "normal")
            {
                bool branch = carveBranch(rng, map, path, inPath, noise);
                if (!branch) return null;
            }

            // Hindernisse auf Nicht-Wegzellen, Bauflächen nahe am Weg bleiben bevorzugt frei.
            int[] dist = distanceToPath(map);
            for (int i = 0; i < w * h; i++)
            {
                if (map.cells[i] != Cell.Open) continue;
                double d = dist[i];
                double p = d <= 1 ? density * 0.25 : d <= 2 ? density * 0.6 : density * 1.8;
                if (rng.chance(p)) map.cells[i] = Cell.Blocked;
            }

            // Zufällige Spiegelung für mehr Vielfalt bei Start-/Zielseiten.
            if (rng.chance(0.5)) mirror(map, true, false);
            if (rng.chance(0.5)) mirror(map, false, true);
            return map;
        }

        private static readonly double[][] ORTHO = { new double[] { 1, 0 }, new double[] { -1, 0 }, new double[] { 0, 1 }, new double[] { 0, -1 } };

        /// <summary>Zweiter Weg von einem Rand zu einer Mündungszelle im Hauptweg.</summary>
        private static bool carveBranch(Rng rng, MapDefinition map, List<GridPos> main, byte[] inPath, double[] noise)
        {
            double w = map.width;
            double h = map.height;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                GridPos j = main[(int)Math.Floor(main.Count * rng.range(0.35, 0.7))];
                bool top = rng.chance(0.5);
                double sx = U.Max(3, U.Min(w - 4, U.Round(j.x + rng.@int(-6, 6))));
                GridPos start = new GridPos(sx, top ? 0 : h - 1);
                if (inPath[(int)(start.y * w + start.x)] != 0) continue;
                // Zielzellen: orthogonale Nachbarn der Mündung, die nicht zum Hauptweg gehören.
                List<GridPos> targets = ORTHO
                    .Select(dd => new GridPos(j.x + dd[0], j.y + dd[1]))
                    .Where(p => p.x > 0 && p.y > 0 && p.x < w - 1 && p.y < h - 1 && inPath[(int)(p.y * w + p.x)] == 0)
                    .ToList();
                if (targets.Count == 0) continue;
                GridPos target = targets[(int)rng.@int(0, targets.Count - 1)];
                HashSet<double> relax = new HashSet<double>();
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) relax.Add((target.y + dy) * w + target.x + dx);
                byte[] local = (byte[])inPath.Clone();
                local[(int)(start.y * w + start.x)] = 1;
                List<GridPos> seg = astarSegment(map, new List<GridPos> { start }, local, target, noise, relax);
                if (seg == null || seg.Count < 4) continue;
                foreach (GridPos c in seg) map.cells[(int)(c.y * w + c.x)] = Cell.Path;
                map.cells[(int)(start.y * w + start.x)] = Cell.Spawn;
                foreach (GridPos c in seg) inPath[(int)(c.y * w + c.x)] = 1;
                inPath[(int)(start.y * w + start.x)] = 1;
                map.spawns = new List<GridPos> { map.spawn, start };
                return true;
            }
            return false;
        }

        private static List<GridPos> astarSegment(MapDefinition map, List<GridPos> path, byte[] inPath, GridPos target, double[] noise, HashSet<double> relax = null)
        {
            int w = (int)map.width;
            int h = (int)map.height;
            GridPos head = path[path.Count - 1];
            GridPos prev = path.Count > 1 ? path[path.Count - 2] : head;
            if (head.x == target.x && head.y == target.y) return new List<GridPos>();
            Func<int, int, bool> allowed = (x, y) =>
            {
                if (x < 1 || y < 1 || x > w - 2 || y > h - 2) return false;
                if (inPath[y * w + x] != 0) return false;
                if (relax != null && relax.Contains(y * w + x)) return true;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (inPath[ny * w + nx] == 0) continue;
                        bool isHead = nx == head.x && ny == head.y;
                        bool isPrev = nx == prev.x && ny == prev.y;
                        if (!isHead && !isPrev) return false;
                    }
                }
                return true;
            };
            int n = w * h;
            double[] g = new double[n];
            for (int q = 0; q < n; q++) g[q] = Pathfinding.INF;
            int[] from = new int[n];
            for (int q = 0; q < n; q++) from[q] = -1;
            byte[] closed = new byte[n];
            int s = (int)(head.y * w + head.x);
            g[s] = 0;
            List<int> open = new List<int> { s };
            Func<int, double> hfn = (i) => Math.Abs((i % w) - target.x) + Math.Abs(Math.Floor((double)i / w) - target.y);
            int guard = 0;
            while (open.Count > 0 && guard++ < 20000)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++)
                {
                    if (g[open[k]] + hfn(open[k]) < g[open[bi]] + hfn(open[bi])) bi = k;
                }
                int cur = open[bi];
                open.RemoveAt(bi);
                if (closed[cur] != 0) continue;
                closed[cur] = 1;
                int cx = cur % w;
                int cy = (cur - cx) / w;
                if (cx == target.x && cy == target.y)
                {
                    List<GridPos> @out = new List<GridPos>();
                    int c = cur;
                    while (c != s && c >= 0)
                    {
                        @out.Add(new GridPos(c % w, Math.Floor((double)c / w)));
                        c = from[c];
                    }
                    @out.Reverse();
                    return @out;
                }
                foreach (double[] dd in ORTHO)
                {
                    int dx = (int)dd[0];
                    int dy = (int)dd[1];
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!allowed(nx, ny)) continue;
                    int j = ny * w + nx;
                    if (closed[j] != 0) continue;
                    // Kurven sind etwas teurer – erzeugt lesbare, gerade Abschnitte.
                    double turn = from[cur] >= 0 && (cur - from[cur]) != (j - cur) ? 0.6 : 0;
                    double ng = g[cur] + 1 + noise[j] + turn;
                    if (ng < g[j])
                    {
                        g[j] = ng;
                        from[j] = cur;
                        open.Add(j);
                    }
                }
            }
            return null;
        }

        /// <summary>Nicht aufeinanderfolgende Wegzellen dürfen sich nicht berühren (auch nicht diagonal).</summary>
        private static bool pathIsClean(List<GridPos> path)
        {
            Dictionary<double, int> pos = new Dictionary<double, int>();
            for (int i = 0; i < path.Count; i++) pos[path[i].y * 1000 + path[i].x] = i;
            for (int i = 0; i < path.Count; i++)
            {
                GridPos p = path[i];
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int j;
                        if (!pos.TryGetValue((p.y + dy) * 1000 + p.x + dx, out j)) continue;
                        int diff = Math.Abs(j - i);
                        if (dx != 0 && dy != 0 ? diff > 2 : diff > 1) return false;
                    }
                }
            }
            return true;
        }

        private static void mirror(MapDefinition map, bool mx, bool my)
        {
            double w = map.width;
            double h = map.height;
            List<double> cells = new List<double>(map.cells);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sx = mx ? w - 1 - x : x;
                    double sy = my ? h - 1 - y : y;
                    map.cells[(int)(y * w + x)] = cells[(int)(sy * w + sx)];
                }
            }
            Func<GridPos, GridPos> f = (p) => new GridPos(mx ? w - 1 - p.x : p.x, my ? h - 1 - p.y : p.y);
            map.spawn = f(map.spawn);
            if (map.spawns != null) map.spawns = map.spawns.Select(f).ToList();
            map.goal = f(map.goal);
        }

        // ---------------------------------------------------------------- Mazing

        private static MapDefinition carveMazing(Rng rng, double w, double h, string biome, double density)
        {
            MapDefinition map = emptyMap(w, h, "mazing", biome);
            double sy = rng.@int(2, h - 3);
            double gy = rng.@int(2, h - 3);
            map.spawn = new GridPos(0, sy);
            map.spawns = new List<GridPos> { map.spawn };
            map.goal = new GridPos(w - 1, gy);
            map.cells[(int)(sy * w)] = Cell.Spawn;
            map.cells[(int)(gy * w + w - 1)] = Cell.Goal;
            // Pufferzonen: direkt um Start und Ziel darf nicht gebaut werden.
            foreach (GridPos p in new List<GridPos> { map.spawn, map.goal })
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        double x = p.x + dx;
                        double y = p.y + dy;
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        int i = (int)(y * w + x);
                        if (map.cells[i] == Cell.Open) map.cells[i] = Cell.NoBuild;
                    }
                }
            }
            double clusters = U.Round((density * w * h) / 2.6);
            for (int c = 0; c < clusters; c++)
            {
                double x = rng.@int(2, w - 3);
                double y = rng.@int(0, h - 1);
                double size = rng.@int(1, 4);
                for (int k = 0; k < size; k++)
                {
                    int i = (int)(y * w + x);
                    bool nearEnds = Math.Abs(x - map.spawn.x) + Math.Abs(y - map.spawn.y) < 3 || Math.Abs(x - map.goal.x) + Math.Abs(y - map.goal.y) < 3;
                    if (map.cells[i] == Cell.Open && !nearEnds) map.cells[i] = Cell.Blocked;
                    if (rng.chance(0.5)) x = U.Min(w - 3, U.Max(2, x + rng.@int(-1, 1)));
                    else y = U.Min(h - 1, U.Max(0, y + rng.@int(-1, 1)));
                }
            }
            double[] field = Pathfinding.computeField(map, null);
            if (field[(int)(sy * w)] >= Pathfinding.INF) return null;
            return map;
        }

        // ---------------------------------------------------------------- Höhen, Ressourcen, Deko

        private static double smooth(double a) { return a * a * (3 - 2 * a); }

        private static void assignHeights(MapDefinition map, Rng rng, double amp)
        {
            double w = map.width;
            double h = map.height;
            double step = 4;
            double gw = Math.Ceiling(w / step) + 2;
            double gh = Math.Ceiling(h / step) + 2;
            double[] grid = new double[(int)(gw * gh)];
            for (int i = 0; i < grid.Length; i++) grid[i] = (double)(float)rng.next();
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = (int)(y * w + x);
                    double c = map.cells[i];
                    if (c != Cell.Open && c != Cell.Blocked)
                    {
                        map.heights[i] = 0;
                        continue;
                    }
                    double fx = x / step;
                    double fy = y / step;
                    double x0 = Math.Floor(fx);
                    double y0 = Math.Floor(fy);
                    double tx = fx - x0;
                    double ty = fy - y0;
                    double v00 = grid[(int)(y0 * gw + x0)];
                    double v10 = grid[(int)(y0 * gw + x0 + 1)];
                    double v01 = grid[(int)((y0 + 1) * gw + x0)];
                    double v11 = grid[(int)((y0 + 1) * gw + x0 + 1)];
                    double v = (v00 * (1 - smooth(tx)) + v10 * smooth(tx)) * (1 - smooth(ty)) + (v01 * (1 - smooth(tx)) + v11 * smooth(tx)) * smooth(ty);
                    double lvl = v * amp;
                    map.heights[i] = lvl > 0.72 ? 2 : lvl > 0.45 ? 1 : 0;
                }
            }
        }

        private static void assignResources(MapDefinition map, Rng rng)
        {
            int[] dist = distanceToPath(map);
            List<double> candidates = new List<double>();
            for (int i = 0; i < map.cells.Count; i++)
            {
                if (!Pathfinding.isBuildableCell(map, i)) continue;
                if (map.variant == "classic" && dist[i] > 3) continue;
                candidates.Add(i);
            }
            rng.shuffle(candidates);
            double k = Math.Sqrt((map.width * map.height) / (22 * 14));
            // 4.0: höchstens 4 Goldvorkommen je Karte (auch auf großen Karten)
            double veins = U.Min(4, U.Round(rng.@int(3, 6) * k));
            double ley = U.Round(rng.@int(2, 4) * k);
            // Goldadern als 2×2-Flächen, damit ein Goldgräber (2×2) genau darauf passt
            var taken = new HashSet<double>();
            map.veins = U.StableSort(pickBlocks(map, candidates, (int)veins, taken, null), (a, b) => a - b);
            var leyList = new List<double>();
            foreach (var c in candidates)
            {
                if (leyList.Count >= ley) break;
                if (taken.Contains(c)) continue;
                leyList.Add(c);
                taken.Add(c);
            }
            map.ley = U.StableSort(leyList, (a, b) => a - b);
        }

        /// <summary>
        /// Wählt bis zu <paramref name="count"/> 2×2-Blöcke aus ebenen, bebaubaren Feldern (Reihenfolge der Kandidaten).
        /// Liefert alle Zellen der Blöcke; belegte Zellen kommen in <paramref name="taken"/>.
        /// </summary>
        public static List<double> pickBlocks(MapDefinition map, List<double> candidates, int count, HashSet<double> taken, Func<int, bool> extraOk)
        {
            int w = (int)map.width, h = (int)map.height;
            var cand = new HashSet<double>(candidates);
            var outCells = new List<double>();
            int blocks = 0;
            foreach (var seed in candidates)
            {
                if (blocks >= count) break;
                if (taken.Contains(seed)) continue;
                int sx = (int)(seed % w), sy = (int)Math.Floor(seed / w);
                // die vier möglichen 2×2-Blöcke, die dieses Feld enthalten
                for (int o = 0; o < 4; o++)
                {
                    int ax = sx - (o & 1), ay = sy - (o >> 1);
                    if (ax < 0 || ay < 0 || ax + 1 >= w || ay + 1 >= h) continue;
                    bool ok = true;
                    double hgt = map.heights.Count > ay * w + ax ? map.heights[ay * w + ax] : 0;
                    for (int dy = 0; dy < 2 && ok; dy++)
                        for (int dx = 0; dx < 2 && ok; dx++)
                        {
                            int ci = (ay + dy) * w + ax + dx;
                            if (!cand.Contains(ci) || taken.Contains(ci) || !Pathfinding.isBuildableCell(map, ci)) ok = false;
                            else if (map.heights.Count > ci && map.heights[ci] != hgt) ok = false;
                            else if (extraOk != null && !extraOk(ci)) ok = false;
                        }
                    // ein Feld Abstand zu anderen Blöcken, damit jede Ader als eigene Fläche erkennbar bleibt
                    for (int dy = -1; dy <= 2 && ok; dy++)
                        for (int dx = -1; dx <= 2 && ok; dx++)
                        {
                            int nx = ax + dx, ny = ay + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            if (taken.Contains(ny * w + nx)) ok = false;
                        }
                    if (!ok) continue;
                    for (int dy = 0; dy < 2; dy++)
                        for (int dx = 0; dx < 2; dx++)
                        {
                            double ci = (ay + dy) * w + ax + dx;
                            outCells.Add(ci);
                            taken.Add(ci);
                        }
                    blocks++;
                    break;
                }
            }
            return outCells;
        }

        /// <summary>PORT: BiomeDefinition.decorKinds als List&lt;double&gt; angenommen.</summary>
        private static void assignDecor(MapDefinition map, Rng rng, List<double> kinds)
        {
            map.decor = new List<double>();
            for (int i = 0; i < map.cells.Count; i++)
            {
                if (map.cells[i] != Cell.Blocked) continue;
                double kind = rng.pick(kinds);
                double rot = U.Round(rng.next() * 628);
                map.decor.Add(i);
                map.decor.Add(kind);
                map.decor.Add(rot);
            }
        }

        /// <summary>Chebyshev-Distanz jeder Zelle zur nächsten begehbaren Zelle.</summary>
        public static int[] distanceToPath(MapDefinition map)
        {
            int n = (int)(map.width * map.height);
            int W = (int)map.width;
            int H = (int)map.height;
            int[] d = new int[n];
            for (int q0 = 0; q0 < n; q0++) d[q0] = 999;
            List<int> q = new List<int>();
            for (int i = 0; i < n; i++)
            {
                double c = map.cells[i];
                if (c == Cell.Path || c == Cell.Spawn || c == Cell.Goal)
                {
                    d[i] = 0;
                    q.Add(i);
                }
            }
            if (map.variant == "mazing")
            {
                for (int q1 = 0; q1 < n; q1++) d[q1] = 1;
                return d;
            }
            int head = 0;
            while (head < q.Count)
            {
                int i = q[head++];
                int x = i % W;
                int y = (i - x) / W;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int j = ny * W + nx;
                        if (d[j] > d[i] + 1)
                        {
                            d[j] = d[i] + 1;
                            q.Add(j);
                        }
                    }
                }
            }
            return d;
        }

        // ---------------------------------------------------------------- Validierung & Fallback

        public static ValidationResult validateMap(MapDefinition map)
        {
            List<string> errors = new List<string>();
            double w = map.width;
            double h = map.height;
            if (w < 8 || h < 6 || w > 48 || h > 40) errors.Add("Ungültige Kartengröße");
            if (map.cells.Count != w * h) errors.Add("Zellanzahl passt nicht zur Kartengröße");
            Func<GridPos, bool> inb = (p) => p.x >= 0 && p.y >= 0 && p.x < w && p.y < h;
            if (!inb(map.spawn)) errors.Add("Start außerhalb der Karte");
            if (!inb(map.goal)) errors.Add("Ziel außerhalb der Karte");
            if (map.spawn.x == map.goal.x && map.spawn.y == map.goal.y) errors.Add("Start und Ziel sind identisch");
            if (errors.Count > 0) return new ValidationResult { ok = false, errors = errors, pathLength = 0, buildable = 0 };
            List<GridPos> starts = spawnsOf(map);
            foreach (GridPos sp in starts)
            {
                if (!inb(sp) || map.cells[(int)Pathfinding.cellIndex(map, sp.x, sp.y)] != Cell.Spawn) errors.Add("Startzelle fehlt");
            }
            if (map.cells[(int)Pathfinding.cellIndex(map, map.goal.x, map.goal.y)] != Cell.Goal) errors.Add("Zielzelle fehlt");
            double spawns = 0;
            double goals = 0;
            foreach (double c in map.cells)
            {
                if (c == Cell.Spawn) spawns++;
                if (c == Cell.Goal) goals++;
            }
            if (spawns != starts.Count || spawns < 1 || spawns > 3 || goals != 1) errors.Add("Genau ein Ziel und 1–3 eingetragene Starts erforderlich");
            if (errors.Count > 0) return new ValidationResult { ok = false, errors = errors, pathLength = 0, buildable = 0 };
            double[] field = Pathfinding.computeField(map, null);
            double sd = field[(int)Pathfinding.cellIndex(map, map.spawn.x, map.spawn.y)];
            if (starts.Any((sp) => field[(int)Pathfinding.cellIndex(map, sp.x, sp.y)] >= Pathfinding.INF)) errors.Add("Kein begehbarer Weg vom Start zum Ziel");
            if (map.variant == "classic")
            {
                for (int i = 0; i < map.cells.Count; i++)
                {
                    if (map.cells[i] == Cell.Path && field[i] >= Pathfinding.INF)
                    {
                        errors.Add("Wegzelle ohne Verbindung zum Ziel");
                        break;
                    }
                }
            }
            double buildable = 0;
            int[] dist = distanceToPath(map);
            for (int i = 0; i < map.cells.Count; i++)
            {
                if (Pathfinding.isBuildableCell(map, i) && dist[i] <= 2) buildable++;
            }
            double minBuild = U.Round((map.variant == "classic" ? 30 : 60) * U.Min(2, (w * h) / (22 * 14)));
            if (buildable < minBuild) errors.Add($"Zu wenige Bauflächen ({U.Fmt(buildable)} < {U.Fmt(minBuild)})");
            double walk = 0;
            for (int i = 0; i < map.cells.Count; i++) if (Pathfinding.isWalkableCell(map, i)) walk++;
            if (walk < 8) errors.Add("Zu wenige begehbare Zellen");
            return new ValidationResult { ok = errors.Count == 0, errors = errors, pathLength = sd < Pathfinding.INF ? sd : 0, buildable = buildable };
        }

        /// <summary>Kontrollierter Fallback: deterministische Serpentinenkarte bzw. offenes Feld.</summary>
        public static MapDefinition fallbackMap(string variant, string biome)
        {
            MapDims dims = DIMS["normal"][variant];
            double w = dims.w;
            double h = dims.h;
            MapDefinition map = emptyMap(w, h, variant, biome);
            map.fallback = true;
            if (variant == "classic")
            {
                List<GridPos> pts = new List<GridPos>();
                double x = 0;
                double y = 2;
                pts.Add(new GridPos(x, y));
                double[] lanes = { 2, 11, 2, 11, 6 };
                double[] cols = { 1, 5, 9, 13, 17, 20 };
                for (int k = 0; k < cols.Length - 1; k++)
                {
                    while (x < cols[k]) pts.Add(new GridPos(++x, y));
                    double ty = lanes[k];
                    while (y != ty) pts.Add(new GridPos(x, (y += U.Sign(ty - y))));
                }
                while (x < w - 1) pts.Add(new GridPos(++x, y));
                foreach (GridPos p in pts) map.cells[(int)(p.y * w + p.x)] = Cell.Path;
                map.spawn = pts[0];
                map.goal = pts[pts.Count - 1];
            }
            else
            {
                map.spawn = new GridPos(0, Math.Floor(h / 2));
                map.goal = new GridPos(w - 1, Math.Floor(h / 2));
                for (int dy = -1; dy <= 1; dy++)
                {
                    foreach (GridPos p in new List<GridPos> { map.spawn, map.goal })
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            double xx = p.x + dx;
                            double yy = p.y + dy;
                            if (xx >= 0 && xx < w && yy >= 0 && yy < h) map.cells[(int)(yy * w + xx)] = Cell.NoBuild;
                        }
                    }
                }
            }
            map.cells[(int)(map.spawn.y * w + map.spawn.x)] = Cell.Spawn;
            map.spawns = new List<GridPos> { map.spawn };
            map.cells[(int)(map.goal.y * w + map.goal.x)] = Cell.Goal;
            map.name = "Fallback";
            return map;
        }
    }
}

// Referenzwerte (Node, `node --import tsx` gegen src/sim/WorldGenerator.ts, setup.ts, WaveDirector.ts):
// generateWorld({ seed: makeSeed('KERNWACHT'), variant: 'classic', biome: 'forest', size: 'normal' }):
//   width=22 height=14 spawn={21,9} spawns=[{21,9}] goal={0,8} fallback=false terrainEvent=sporeBloom
//   Σ cells[i]*(i+1) = 18259 ; Σ heights[i]*(i+1) = 38047 ; decor.length = 60
//   veins=[44,78,130,169,240,295] ley=[158,273,300] name="Verwunschener Wald · Classic"
// createInitialState({ mode:'heroEndless', seedText:'KERNWACHT', variant:'classic', biome:'forest', difficulty:'normal', size:'normal',
//   heroEndless:{ champion:'lyra', startKit:[], meta:{ bestWave:0, unlockedItems:[], fragments:{} }, runId:'test' } }):
//   map 22x14, spawn {21,9}, goal {0,8}, gold=260, lives=20
// planWave(s, n)  (Format enemy x count, E=elite, /mutation, M=miniBoss, /bossId):
//   n=1: runner x12                          hpMul=1 speedMul=1 rewardMul=1 event=null
//   n=2: runner x14                          hpMul=1.127 speedMul=1 rewardMul=1.035
//   n=3: brute x6                            hpMul=1.268 speedMul=0.95 rewardMul=1.07
//   n=4: swarm x18, regen x1                 hpMul=1.4229999999999998 speedMul=1 rewardMul=1.105 event=sporeBloom
//   n=5: flyer x4, regen x4, brute x1 E      hpMul=1.592 speedMul=1 rewardMul=1.1400000000000001
//   n=7: regen x10, flyer x10, brute x1 E M
//   n=10: boss x1 /ashQueen, stalker x1, saboteur x3, shaman x2, regen x2 E
//   n=100: boss x1 /twinSol, phase x17, swarm x60, runner x36, tunneler x9, broodmother x5, golem x4 E /armored; affixes=[immune] immune=arcane
