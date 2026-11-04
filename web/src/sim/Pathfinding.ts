import { Cell, type GridPos, type MapDefinition } from '../core/types.ts';

// Wegfindung über Distanzfelder (Dijkstra vom Ziel aus, 8-Nachbarschaft ohne Eckenschneiden).
// Ein Feld beantwortet für jede Zelle gleichzeitig "wie weit noch?" und "wohin als Nächstes?",
// wodurch beliebig viele Gegner und dynamische Routenänderungen effizient unterstützt werden.

export const INF = 1e9;
const SQRT2 = Math.SQRT2;
const DIRS: ReadonlyArray<readonly [number, number, number]> = [
  [1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1],
  [1, 1, SQRT2], [1, -1, SQRT2], [-1, 1, SQRT2], [-1, -1, SQRT2],
];

export function cellIndex(map: MapDefinition, x: number, y: number): number {
  return y * map.width + x;
}

export function inBounds(map: MapDefinition, x: number, y: number): boolean {
  return x >= 0 && y >= 0 && x < map.width && y < map.height;
}

/** Grundsätzliche Begehbarkeit einer Zelle (ohne Türme). */
export function isWalkableCell(map: MapDefinition, i: number): boolean {
  const c = map.cells[i];
  if (c === Cell.Spawn || c === Cell.Goal || c === Cell.Path) return true;
  if (map.variant === 'mazing') return c === Cell.Open || c === Cell.NoBuild;
  return false;
}

/** Bebaubarkeit (ohne Berücksichtigung vorhandener Türme). */
export function isBuildableCell(map: MapDefinition, i: number): boolean {
  return map.cells[i] === Cell.Open;
}

class MinHeap {
  private keys: number[] = [];
  private vals: number[] = [];
  get size(): number {
    return this.keys.length;
  }
  push(key: number, val: number): void {
    const k = this.keys;
    const v = this.vals;
    k.push(key);
    v.push(val);
    let i = k.length - 1;
    while (i > 0) {
      const p = (i - 1) >> 1;
      if (k[p] < key || (k[p] === key && v[p] <= val)) break;
      k[i] = k[p];
      v[i] = v[p];
      i = p;
    }
    k[i] = key;
    v[i] = val;
  }
  pop(): [number, number] {
    const k = this.keys;
    const v = this.vals;
    const topK = k[0];
    const topV = v[0];
    const lastK = k.pop() as number;
    const lastV = v.pop() as number;
    if (k.length > 0) {
      let i = 0;
      const n = k.length;
      for (;;) {
        const l = 2 * i + 1;
        const r = l + 1;
        let m = i;
        let mk = lastK;
        let mv = lastV;
        if (l < n && (k[l] < mk || (k[l] === mk && v[l] < mv))) { m = l; mk = k[l]; mv = v[l]; }
        if (r < n && (k[r] < mk || (k[r] === mk && v[r] < mv))) { m = r; mk = k[r]; mv = v[r]; }
        if (m === i) break;
        k[i] = k[m];
        v[i] = v[m];
        i = m;
      }
      k[i] = lastK;
      v[i] = lastV;
    }
    return [topK, topV];
  }
}

/**
 * Berechnet das Distanzfeld zum Ziel.
 * @param blocked Zellen, die zusätzlich blockiert sind (Türme im Mazing), 1 = blockiert.
 * @param extraCost optionale Zusatzkosten je Zelle (z. B. Gefahrenfeld für Pfadspäher).
 */
export function computeField(map: MapDefinition, blocked: Uint8Array | null, extraCost: Float32Array | null = null): Float64Array {
  const n = map.width * map.height;
  const dist = new Float64Array(n).fill(INF);
  const w = map.width;
  const walk = new Uint8Array(n);
  for (let i = 0; i < n; i++) walk[i] = isWalkableCell(map, i) && !(blocked && blocked[i]) ? 1 : 0;
  const g = map.goal.y * w + map.goal.x;
  if (!walk[g]) walk[g] = 1;
  dist[g] = 0;
  const heap = new MinHeap();
  heap.push(0, g);
  while (heap.size > 0) {
    const [d, i] = heap.pop();
    if (d > dist[i]) continue;
    const x = i % w;
    const y = (i - x) / w;
    for (const [dx, dy, c] of DIRS) {
      const nx = x + dx;
      const ny = y + dy;
      if (nx < 0 || ny < 0 || nx >= w || ny >= map.height) continue;
      const j = ny * w + nx;
      if (!walk[j]) continue;
      if (dx !== 0 && dy !== 0) {
        // Kein Eckenschneiden: beide orthogonalen Nachbarn müssen begehbar sein.
        if (!walk[y * w + nx] || !walk[ny * w + x]) continue;
      }
      const nd = d + c + (extraCost ? extraCost[j] : 0);
      if (nd < dist[j]) {
        dist[j] = nd;
        heap.push(nd, j);
      }
    }
  }
  return dist;
}

/** Nächste Zelle entlang des Feldes (deterministische Reihenfolge bei Gleichstand). */
export function nextCell(map: MapDefinition, field: Float64Array, x: number, y: number): GridPos | null {
  const w = map.width;
  const here = field[y * w + x];
  let best = here;
  let bx = -1;
  let by = -1;
  for (const [dx, dy] of DIRS) {
    const nx = x + dx;
    const ny = y + dy;
    if (nx < 0 || ny < 0 || nx >= w || ny >= map.height) continue;
    const j = ny * w + nx;
    const f = field[j];
    if (f >= INF) continue;
    if (dx !== 0 && dy !== 0) {
      if (field[y * w + nx] >= INF || field[ny * w + x] >= INF) continue;
    }
    if (f < best - 1e-9) {
      best = f;
      bx = nx;
      by = ny;
    }
  }
  return bx >= 0 ? { x: bx, y: by } : null;
}

/** Vorherige Zelle (weg vom Ziel) – für Rückversetzungseffekte. */
export function previousCell(map: MapDefinition, field: Float64Array, x: number, y: number): GridPos | null {
  const w = map.width;
  const here = field[y * w + x];
  let best = -1;
  let bx = -1;
  let by = -1;
  for (const [dx, dy] of DIRS) {
    const nx = x + dx;
    const ny = y + dy;
    if (nx < 0 || ny < 0 || nx >= w || ny >= map.height) continue;
    const f = field[ny * w + nx];
    if (f >= INF) continue;
    if (dx !== 0 && dy !== 0) {
      if (field[y * w + nx] >= INF || field[ny * w + x] >= INF) continue;
    }
    // Nur echte Aufwärtsschritte (max. ein Diagonalschritt), damit keine Sprünge über Wände entstehen.
    if (f > here + 1e-9 && f <= here + SQRT2 + 1e-6 && f > best) {
      best = f;
      bx = nx;
      by = ny;
    }
  }
  return bx >= 0 ? { x: bx, y: by } : null;
}

/** Verfolgt den Weg von einer Zelle bis zum Ziel. */
export function tracePath(map: MapDefinition, field: Float64Array, from: GridPos, maxSteps = 4000): GridPos[] {
  const out: GridPos[] = [{ x: from.x, y: from.y }];
  if (field[from.y * map.width + from.x] >= INF) return out;
  let cur = from;
  for (let s = 0; s < maxSteps; s++) {
    if (cur.x === map.goal.x && cur.y === map.goal.y) break;
    const n = nextCell(map, field, cur.x, cur.y);
    if (!n) break;
    out.push(n);
    cur = n;
  }
  return out;
}

export function hasPath(map: MapDefinition, blocked: Uint8Array | null): boolean {
  const f = computeField(map, blocked);
  return f[map.spawn.y * map.width + map.spawn.x] < INF;
}
