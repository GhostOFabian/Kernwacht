// Übersetzung von sim/Pathfinding.ts.
// Wegfindung über Distanzfelder (Dijkstra vom Ziel aus, 8-Nachbarschaft ohne Eckenschneiden).
// Ein Feld beantwortet für jede Zelle gleichzeitig "wie weit noch?" und "wohin als Nächstes?",
// wodurch beliebig viele Gegner und dynamische Routenänderungen effizient unterstützt werden.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public static partial class Pathfinding
    {
        public const double INF = 1e9;
        private static readonly double SQRT2 = Math.Sqrt(2.0);   // JS Math.SQRT2 = 1.4142135623730951 (korrekt gerundet, identisch)
        private static readonly double[][] DIRS =
        {
            new double[] { 1, 0, 1 }, new double[] { -1, 0, 1 }, new double[] { 0, 1, 1 }, new double[] { 0, -1, 1 },
            new double[] { 1, 1, SQRT2 }, new double[] { 1, -1, SQRT2 }, new double[] { -1, 1, SQRT2 }, new double[] { -1, -1, SQRT2 },
        };

        public static double cellIndex(MapDefinition map, double x, double y)
        {
            return y * map.width + x;
        }

        public static bool inBounds(MapDefinition map, double x, double y)
        {
            return x >= 0 && y >= 0 && x < map.width && y < map.height;
        }

        /// <summary>Grundsätzliche Begehbarkeit einer Zelle (ohne Türme).</summary>
        public static bool isWalkableCell(MapDefinition map, double i)
        {
            double c = map.cells[(int)i];
            if (c == Cell.Spawn || c == Cell.Goal || c == Cell.Path) return true;
            if (map.variant == "mazing") return c == Cell.Open || c == Cell.NoBuild;
            return false;
        }

        /// <summary>Bebaubarkeit (ohne Berücksichtigung vorhandener Türme).</summary>
        public static bool isBuildableCell(MapDefinition map, double i)
        {
            return map.cells[(int)i] == Cell.Open;
        }

        private class MinHeap
        {
            private readonly List<double> keys = new List<double>();
            private readonly List<double> vals = new List<double>();

            public int size { get { return keys.Count; } }

            public void push(double key, double val)
            {
                List<double> k = keys;
                List<double> v = vals;
                k.Add(key);
                v.Add(val);
                int i = k.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (k[p] < key || (k[p] == key && v[p] <= val)) break;
                    k[i] = k[p];
                    v[i] = v[p];
                    i = p;
                }
                k[i] = key;
                v[i] = val;
            }

            public double[] pop()
            {
                List<double> k = keys;
                List<double> v = vals;
                double topK = k[0];
                double topV = v[0];
                double lastK = k[k.Count - 1];
                k.RemoveAt(k.Count - 1);
                double lastV = v[v.Count - 1];
                v.RemoveAt(v.Count - 1);
                if (k.Count > 0)
                {
                    int i = 0;
                    int n = k.Count;
                    for (; ; )
                    {
                        int l = 2 * i + 1;
                        int r = l + 1;
                        int m = i;
                        double mk = lastK;
                        double mv = lastV;
                        if (l < n && (k[l] < mk || (k[l] == mk && v[l] < mv))) { m = l; mk = k[l]; mv = v[l]; }
                        if (r < n && (k[r] < mk || (k[r] == mk && v[r] < mv))) { m = r; mk = k[r]; mv = v[r]; }
                        if (m == i) break;
                        k[i] = k[m];
                        v[i] = v[m];
                        i = m;
                    }
                    k[i] = lastK;
                    v[i] = lastV;
                }
                return new double[] { topK, topV };
            }
        }

        /// <summary>
        /// Berechnet das Distanzfeld zum Ziel.
        /// blocked: Zellen, die zusätzlich blockiert sind (Türme im Mazing), 1 = blockiert.
        /// extraCost: optionale Zusatzkosten je Zelle (z. B. Gefahrenfeld für Pfadspäher).
        /// PORT: TS-Typ ist Float32Array – beim Lesen wird auf float32 gerundet ((double)(float)), damit das
        /// Ergebnis auch dann identisch ist, wenn der Aufrufer ungerundete double-Werte einträgt.
        /// </summary>
        public static double[] computeField(MapDefinition map, byte[] blocked, double[] extraCost = null)
        {
            int n = (int)(map.width * map.height);
            double[] dist = new double[n];
            for (int q = 0; q < n; q++) dist[q] = INF;
            int w = (int)map.width;
            int hgt = (int)map.height;
            byte[] walk = new byte[n];
            for (int i = 0; i < n; i++) walk[i] = (byte)(isWalkableCell(map, i) && !(blocked != null && blocked[i] != 0) ? 1 : 0);
            int g = (int)(map.goal.y * w + map.goal.x);
            if (walk[g] == 0) walk[g] = 1;
            dist[g] = 0;
            MinHeap heap = new MinHeap();
            heap.push(0, g);
            while (heap.size > 0)
            {
                double[] top = heap.pop();
                double d = top[0];
                int i = (int)top[1];
                if (d > dist[i]) continue;
                int x = i % w;
                int y = (i - x) / w;
                foreach (double[] dir in DIRS)
                {
                    int dx = (int)dir[0];
                    int dy = (int)dir[1];
                    double c = dir[2];
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= hgt) continue;
                    int j = ny * w + nx;
                    if (walk[j] == 0) continue;
                    if (dx != 0 && dy != 0)
                    {
                        // Kein Eckenschneiden: beide orthogonalen Nachbarn müssen begehbar sein.
                        if (walk[y * w + nx] == 0 || walk[ny * w + x] == 0) continue;
                    }
                    double nd = d + c + (extraCost != null ? (double)(float)extraCost[j] : 0);
                    if (nd < dist[j])
                    {
                        dist[j] = nd;
                        heap.push(nd, j);
                    }
                }
            }
            return dist;
        }

        /// <summary>Nächste Zelle entlang des Feldes (deterministische Reihenfolge bei Gleichstand).</summary>
        public static GridPos nextCell(MapDefinition map, double[] field, double x, double y)
        {
            double w = map.width;
            double here = field[(int)(y * w + x)];
            double best = here;
            double bx = -1;
            double by = -1;
            foreach (double[] dir in DIRS)
            {
                double dx = dir[0];
                double dy = dir[1];
                double nx = x + dx;
                double ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= map.height) continue;
                double j = ny * w + nx;
                double f = field[(int)j];
                if (f >= INF) continue;
                if (dx != 0 && dy != 0)
                {
                    if (field[(int)(y * w + nx)] >= INF || field[(int)(ny * w + x)] >= INF) continue;
                }
                if (f < best - 1e-9)
                {
                    best = f;
                    bx = nx;
                    by = ny;
                }
            }
            return bx >= 0 ? new GridPos(bx, by) : null;
        }

        /// <summary>Vorherige Zelle (weg vom Ziel) – für Rückversetzungseffekte.</summary>
        public static GridPos previousCell(MapDefinition map, double[] field, double x, double y)
        {
            double w = map.width;
            double here = field[(int)(y * w + x)];
            double best = -1;
            double bx = -1;
            double by = -1;
            foreach (double[] dir in DIRS)
            {
                double dx = dir[0];
                double dy = dir[1];
                double nx = x + dx;
                double ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= w || ny >= map.height) continue;
                double f = field[(int)(ny * w + nx)];
                if (f >= INF) continue;
                if (dx != 0 && dy != 0)
                {
                    if (field[(int)(y * w + nx)] >= INF || field[(int)(ny * w + x)] >= INF) continue;
                }
                // Nur echte Aufwärtsschritte (max. ein Diagonalschritt), damit keine Sprünge über Wände entstehen.
                if (f > here + 1e-9 && f <= here + SQRT2 + 1e-6 && f > best)
                {
                    best = f;
                    bx = nx;
                    by = ny;
                }
            }
            return bx >= 0 ? new GridPos(bx, by) : null;
        }

        /// <summary>Verfolgt den Weg von einer Zelle bis zum Ziel.</summary>
        public static List<GridPos> tracePath(MapDefinition map, double[] field, GridPos from, double maxSteps = 4000)
        {
            List<GridPos> @out = new List<GridPos> { new GridPos(from.x, from.y) };
            if (field[(int)(from.y * map.width + from.x)] >= INF) return @out;
            GridPos cur = from;
            for (int s = 0; s < maxSteps; s++)
            {
                if (cur.x == map.goal.x && cur.y == map.goal.y) break;
                GridPos n = nextCell(map, field, cur.x, cur.y);
                if (n == null) break;
                @out.Add(n);
                cur = n;
            }
            return @out;
        }

        public static bool hasPath(MapDefinition map, byte[] blocked)
        {
            double[] f = computeField(map, blocked);
            return f[(int)(map.spawn.y * map.width + map.spawn.x)] < INF;
        }
    }
}
