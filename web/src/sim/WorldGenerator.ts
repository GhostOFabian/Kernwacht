import { BIOME_ORDER, BIOMES } from '../config/biomes.ts';
import { hashSeed, hashString, normalizeSeedText, Rng } from '../core/rng.ts';
import { Cell, type BiomeId, type GridPos, type MapDefinition, type MapSize, type MapVariant, type WorldSeed } from '../core/types.ts';
import { cellIndex, computeField, INF, isBuildableCell, isWalkableCell } from './Pathfinding.ts';

// Prozeduraler Weltgenerator. Getrennte Zufallsströme:
//   layout   – Wegeführung/Hindernisse (taktisch relevant)
//   res      – Goldadern, Leylinien (taktisch relevant)
//   height   – Geländehöhen (Anhöhen geben Reichweite)
//   decor    – rein kosmetisch (beeinflusst nie die Balance)
//   meta     – Biom und Geländeereignis

export const MAP_VERSION = 1;

export interface GenerateOptions {
  seed: WorldSeed;
  variant: MapVariant;
  biome?: BiomeId | 'random';
  size?: MapSize;
}

export function makeSeed(text: string): WorldSeed {
  const t = normalizeSeedText(text);
  return { text: t, hash: hashString(t) };
}

export interface ValidationResult { ok: boolean; errors: string[]; pathLength: number; buildable: number }

const DIMS: Record<MapSize, Record<MapVariant, { w: number; h: number }>> = {
  normal: { classic: { w: 22, h: 14 }, mazing: { w: 20, h: 14 } },
  large: { classic: { w: 32, h: 20 }, mazing: { w: 30, h: 20 } },
  huge: { classic: { w: 42, h: 26 }, mazing: { w: 40, h: 26 } },
};

export function mapDims(variant: MapVariant, size: MapSize = 'normal'): { w: number; h: number } {
  return DIMS[size][variant];
}

/** Alle Startpunkte einer Karte (Rückwärtskompatibel zu Karten mit nur spawn). */
export function spawnsOf(map: MapDefinition): GridPos[] {
  return map.spawns && map.spawns.length > 0 ? map.spawns : [map.spawn];
}

export function generateWorld(opts: GenerateOptions): MapDefinition {
  const h = opts.seed.hash;
  const meta = new Rng(hashSeed(h, 'meta'));
  const biome: BiomeId = !opts.biome || opts.biome === 'random' ? meta.pick(BIOME_ORDER) : opts.biome;
  const bdef = BIOMES[biome];
  const terrainEvent = meta.chance(0.8) ? meta.pick(bdef.events) : null;
  const size: MapSize = opts.size ?? 'normal';
  // Normale Karten behalten den bisherigen Zufallsstrom (Seeds bleiben reproduzierbar wie zuvor).
  const layout = new Rng(hashSeed(h, size === 'normal' ? 'layout' : `layout-${size}`));
  const { w, h: hh } = DIMS[size][opts.variant];

  let map: MapDefinition | null = null;
  // Begrenzte Anzahl an Versuchen – keine Endlosschleifen möglich.
  for (let attempt = 0; attempt < 40 && !map; attempt++) {
    const candidate = opts.variant === 'classic'
      ? carveClassic(layout, w, hh, biome, bdef.obstacleDensity, size)
      : carveMazing(layout, w, hh, biome, bdef.obstacleDensity * (size === 'normal' ? 1 : 0.6));
    if (candidate) candidate.size = size;
    if (candidate && validateMap(candidate).ok) map = candidate;
  }
  if (!map) {
    map = fallbackMap(opts.variant, biome);
  }
  map.terrainEvent = terrainEvent;
  assignHeights(map, new Rng(hashSeed(h, 'height')), bdef.heightAmp);
  assignResources(map, new Rng(hashSeed(h, 'res')));
  assignDecor(map, new Rng(hashSeed(h, 'decor')), bdef.decorKinds);
  const sizeName = map.size === 'huge' ? ' · Riesig' : map.size === 'large' ? ' · Groß' : '';
  map.name = `${bdef.name} · ${opts.variant === 'classic' ? 'Classic' : 'Mazing'}${sizeName}`;
  return map;
}

function emptyMap(w: number, h: number, variant: MapVariant, biome: BiomeId): MapDefinition {
  return {
    version: MAP_VERSION, width: w, height: h, variant, biome,
    cells: new Array<number>(w * h).fill(Cell.Open),
    heights: new Array<number>(w * h).fill(0),
    veins: [], ley: [], decor: [],
    spawn: { x: 0, y: 0 }, spawns: [{ x: 0, y: 0 }], size: 'normal', goal: { x: w - 1, y: h - 1 },
    terrainEvent: null, fallback: false, name: '',
  };
}

// ---------------------------------------------------------------- Classic

function carveClassic(rng: Rng, w: number, h: number, biome: BiomeId, density: number, size: MapSize = 'normal'): MapDefinition | null {
  const map = emptyMap(w, h, 'classic', biome);
  const sy = rng.int(2, h - 3);
  const gy = rng.int(2, h - 3);
  const spawn = { x: 0, y: sy };
  const goal = { x: w - 1, y: gy };
  const scale = w / 22;

  // Wegpunkte mit gelegentlichen Rückschleifen für taktisch interessante Kurven.
  const count = Math.round(rng.int(3, 5) * scale);
  const pts: GridPos[] = [{ x: 1, y: sy }];
  for (let i = 0; i < count; i++) {
    const base = 2 + Math.round(((i + 1) / (count + 1)) * (w - 5));
    let x = base + rng.int(-2, 2);
    if (i > 0 && rng.chance(0.25)) x = Math.max(2, pts[pts.length - 1].x - rng.int(1, 3));
    x = Math.min(w - 3, Math.max(2, x));
    const y = rng.int(1, h - 2);
    pts.push({ x, y });
  }
  pts.push({ x: w - 2, y: gy });

  const noise = new Float32Array(w * h);
  for (let i = 0; i < noise.length; i++) noise[i] = rng.next() * 2.2;

  const path: GridPos[] = [spawn];
  const inPath = new Uint8Array(w * h);
  inPath[sy * w] = 1;
  for (const target of pts) {
    const seg = astarSegment(map, path, inPath, target, noise);
    if (!seg) return null;
    for (const c of seg) {
      path.push(c);
      inPath[c.y * w + c.x] = 1;
    }
  }
  // Letzter Schritt zum Ziel (rechter Rand).
  const head = path[path.length - 1];
  if (head.y !== gy || head.x !== w - 2) return null;
  path.push(goal);
  inPath[gy * w + w - 1] = 1;

  const areaScale = (w * h) / (22 * 14);
  if (path.length < 38 * scale || path.length > 110 * Math.max(1, areaScale * 0.8)) return null;
  if (!pathIsClean(path)) return null;

  for (const c of path) map.cells[c.y * w + c.x] = Cell.Path;
  map.cells[sy * w] = Cell.Spawn;
  map.cells[gy * w + w - 1] = Cell.Goal;
  map.spawn = spawn;
  map.spawns = [spawn];
  map.goal = goal;

  // Große Karten: zweiter Startpunkt am oberen/unteren Rand, der in den Hauptweg mündet (Weggabelung).
  if (size !== 'normal') {
    const branch = carveBranch(rng, map, path, inPath, noise);
    if (!branch) return null;
  }

  // Hindernisse auf Nicht-Wegzellen, Bauflächen nahe am Weg bleiben bevorzugt frei.
  const dist = distanceToPath(map);
  for (let i = 0; i < w * h; i++) {
    if (map.cells[i] !== Cell.Open) continue;
    const d = dist[i];
    const p = d <= 1 ? density * 0.25 : d <= 2 ? density * 0.6 : density * 1.8;
    if (rng.chance(p)) map.cells[i] = Cell.Blocked;
  }

  // Zufällige Spiegelung für mehr Vielfalt bei Start-/Zielseiten.
  if (rng.chance(0.5)) mirror(map, true, false);
  if (rng.chance(0.5)) mirror(map, false, true);
  return map;
}

/** Zweiter Weg von einem Rand zu einer Mündungszelle im Hauptweg. */
function carveBranch(rng: Rng, map: MapDefinition, main: GridPos[], inPath: Uint8Array, noise: Float32Array): boolean {
  const w = map.width;
  const h = map.height;
  for (let attempt = 0; attempt < 8; attempt++) {
    const j = main[Math.floor(main.length * rng.range(0.35, 0.7))];
    const top = rng.chance(0.5);
    const sx = Math.max(3, Math.min(w - 4, Math.round(j.x + rng.int(-6, 6))));
    const start: GridPos = { x: sx, y: top ? 0 : h - 1 };
    if (inPath[start.y * w + start.x]) continue;
    // Zielzellen: orthogonale Nachbarn der Mündung, die nicht zum Hauptweg gehören.
    const targets = [[1, 0], [-1, 0], [0, 1], [0, -1]]
      .map(([dx, dy]) => ({ x: j.x + dx, y: j.y + dy }))
      .filter((p) => p.x > 0 && p.y > 0 && p.x < w - 1 && p.y < h - 1 && !inPath[p.y * w + p.x]);
    if (targets.length === 0) continue;
    const target = targets[rng.int(0, targets.length - 1)];
    const relax = new Set<number>();
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) relax.add((target.y + dy) * w + target.x + dx);
    const local = inPath.slice();
    local[start.y * w + start.x] = 1;
    const seg = astarSegment(map, [start], local, target, noise, relax);
    if (!seg || seg.length < 4) continue;
    for (const c of seg) map.cells[c.y * w + c.x] = Cell.Path;
    map.cells[start.y * w + start.x] = Cell.Spawn;
    for (const c of seg) inPath[c.y * w + c.x] = 1;
    inPath[start.y * w + start.x] = 1;
    map.spawns = [map.spawn, start];
    return true;
  }
  return false;
}

function astarSegment(map: MapDefinition, path: GridPos[], inPath: Uint8Array, target: GridPos, noise: Float32Array, relax: Set<number> | null = null): GridPos[] | null {
  const w = map.width;
  const h = map.height;
  const head = path[path.length - 1];
  const prev = path.length > 1 ? path[path.length - 2] : head;
  if (head.x === target.x && head.y === target.y) return [];
  const allowed = (x: number, y: number): boolean => {
    if (x < 1 || y < 1 || x > w - 2 || y > h - 2) return false;
    if (inPath[y * w + x]) return false;
    if (relax && relax.has(y * w + x)) return true;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx;
        const ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
        if (!inPath[ny * w + nx]) continue;
        const isHead = nx === head.x && ny === head.y;
        const isPrev = nx === prev.x && ny === prev.y;
        if (!isHead && !isPrev) return false;
      }
    }
    return true;
  };
  const n = w * h;
  const g = new Float64Array(n).fill(INF);
  const from = new Int32Array(n).fill(-1);
  const closed = new Uint8Array(n);
  const s = head.y * w + head.x;
  g[s] = 0;
  const open: number[] = [s];
  const hfn = (i: number): number => Math.abs((i % w) - target.x) + Math.abs(Math.floor(i / w) - target.y);
  let guard = 0;
  while (open.length > 0 && guard++ < 20000) {
    let bi = 0;
    for (let k = 1; k < open.length; k++) {
      if (g[open[k]] + hfn(open[k]) < g[open[bi]] + hfn(open[bi])) bi = k;
    }
    const cur = open.splice(bi, 1)[0];
    if (closed[cur]) continue;
    closed[cur] = 1;
    const cx = cur % w;
    const cy = (cur - cx) / w;
    if (cx === target.x && cy === target.y) {
      const out: GridPos[] = [];
      let c = cur;
      while (c !== s && c >= 0) {
        out.push({ x: c % w, y: Math.floor(c / w) });
        c = from[c];
      }
      out.reverse();
      return out;
    }
    const dirs = [[1, 0], [-1, 0], [0, 1], [0, -1]];
    for (const [dx, dy] of dirs) {
      const nx = cx + dx;
      const ny = cy + dy;
      if (!allowed(nx, ny)) continue;
      const j = ny * w + nx;
      if (closed[j]) continue;
      // Kurven sind etwas teurer – erzeugt lesbare, gerade Abschnitte.
      const turn = from[cur] >= 0 && (cur - from[cur]) !== (j - cur) ? 0.6 : 0;
      const ng = g[cur] + 1 + noise[j] + turn;
      if (ng < g[j]) {
        g[j] = ng;
        from[j] = cur;
        open.push(j);
      }
    }
  }
  return null;
}

/** Nicht aufeinanderfolgende Wegzellen dürfen sich nicht berühren (auch nicht diagonal). */
function pathIsClean(path: GridPos[]): boolean {
  const pos = new Map<number, number>();
  path.forEach((p, i) => pos.set(p.y * 1000 + p.x, i));
  for (let i = 0; i < path.length; i++) {
    const p = path[i];
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if (dx === 0 && dy === 0) continue;
        const j = pos.get((p.y + dy) * 1000 + p.x + dx);
        if (j === undefined) continue;
        const diff = Math.abs(j - i);
        if (dx !== 0 && dy !== 0 ? diff > 2 : diff > 1) return false;
      }
    }
  }
  return true;
}

function mirror(map: MapDefinition, mx: boolean, my: boolean): void {
  const { width: w, height: h } = map;
  const cells = map.cells.slice();
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const sx = mx ? w - 1 - x : x;
      const sy = my ? h - 1 - y : y;
      map.cells[y * w + x] = cells[sy * w + sx];
    }
  }
  const f = (p: GridPos): GridPos => ({ x: mx ? w - 1 - p.x : p.x, y: my ? h - 1 - p.y : p.y });
  map.spawn = f(map.spawn);
  if (map.spawns) map.spawns = map.spawns.map(f);
  map.goal = f(map.goal);
}

// ---------------------------------------------------------------- Mazing

function carveMazing(rng: Rng, w: number, h: number, biome: BiomeId, density: number): MapDefinition | null {
  const map = emptyMap(w, h, 'mazing', biome);
  const sy = rng.int(2, h - 3);
  const gy = rng.int(2, h - 3);
  map.spawn = { x: 0, y: sy };
  map.spawns = [map.spawn];
  map.goal = { x: w - 1, y: gy };
  map.cells[sy * w] = Cell.Spawn;
  map.cells[gy * w + w - 1] = Cell.Goal;
  // Pufferzonen: direkt um Start und Ziel darf nicht gebaut werden.
  for (const p of [map.spawn, map.goal]) {
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const x = p.x + dx;
        const y = p.y + dy;
        if (x < 0 || y < 0 || x >= w || y >= h) continue;
        const i = y * w + x;
        if (map.cells[i] === Cell.Open) map.cells[i] = Cell.NoBuild;
      }
    }
  }
  const clusters = Math.round((density * w * h) / 2.6);
  for (let c = 0; c < clusters; c++) {
    let x = rng.int(2, w - 3);
    let y = rng.int(0, h - 1);
    const size = rng.int(1, 4);
    for (let k = 0; k < size; k++) {
      const i = y * w + x;
      const nearEnds = Math.abs(x - map.spawn.x) + Math.abs(y - map.spawn.y) < 3 || Math.abs(x - map.goal.x) + Math.abs(y - map.goal.y) < 3;
      if (map.cells[i] === Cell.Open && !nearEnds) map.cells[i] = Cell.Blocked;
      if (rng.chance(0.5)) x = Math.min(w - 3, Math.max(2, x + rng.int(-1, 1)));
      else y = Math.min(h - 1, Math.max(0, y + rng.int(-1, 1)));
    }
  }
  const field = computeField(map, null);
  if (field[sy * w] >= INF) return null;
  return map;
}

// ---------------------------------------------------------------- Höhen, Ressourcen, Deko

function assignHeights(map: MapDefinition, rng: Rng, amp: number): void {
  const { width: w, height: h } = map;
  const step = 4;
  const gw = Math.ceil(w / step) + 2;
  const gh = Math.ceil(h / step) + 2;
  const grid = new Float32Array(gw * gh);
  for (let i = 0; i < grid.length; i++) grid[i] = rng.next();
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      const c = map.cells[i];
      if (c !== Cell.Open && c !== Cell.Blocked) {
        map.heights[i] = 0;
        continue;
      }
      const fx = x / step;
      const fy = y / step;
      const x0 = Math.floor(fx);
      const y0 = Math.floor(fy);
      const tx = fx - x0;
      const ty = fy - y0;
      const s = (a: number) => a * a * (3 - 2 * a);
      const v00 = grid[y0 * gw + x0];
      const v10 = grid[y0 * gw + x0 + 1];
      const v01 = grid[(y0 + 1) * gw + x0];
      const v11 = grid[(y0 + 1) * gw + x0 + 1];
      const v = (v00 * (1 - s(tx)) + v10 * s(tx)) * (1 - s(ty)) + (v01 * (1 - s(tx)) + v11 * s(tx)) * s(ty);
      const lvl = v * amp;
      map.heights[i] = lvl > 0.72 ? 2 : lvl > 0.45 ? 1 : 0;
    }
  }
}

function assignResources(map: MapDefinition, rng: Rng): void {
  const dist = distanceToPath(map);
  const candidates: number[] = [];
  for (let i = 0; i < map.cells.length; i++) {
    if (!isBuildableCell(map, i)) continue;
    if (map.variant === 'classic' && dist[i] > 3) continue;
    candidates.push(i);
  }
  rng.shuffle(candidates);
  const k = Math.sqrt((map.width * map.height) / (22 * 14));
  const veins = Math.round(rng.int(3, 6) * k);
  map.veins = candidates.slice(0, veins).sort((a, b) => a - b);
  const ley = Math.round(rng.int(2, 4) * k);
  map.ley = candidates.slice(veins, veins + ley).sort((a, b) => a - b);
}

function assignDecor(map: MapDefinition, rng: Rng, kinds: number[]): void {
  map.decor = [];
  for (let i = 0; i < map.cells.length; i++) {
    if (map.cells[i] !== Cell.Blocked) continue;
    map.decor.push(i, rng.pick(kinds), Math.round(rng.next() * 628));
  }
}

/** Chebyshev-Distanz jeder Zelle zur nächsten begehbaren Zelle. */
export function distanceToPath(map: MapDefinition): Int32Array {
  const n = map.width * map.height;
  const d = new Int32Array(n).fill(999);
  const q: number[] = [];
  for (let i = 0; i < n; i++) {
    const c = map.cells[i];
    if (c === Cell.Path || c === Cell.Spawn || c === Cell.Goal) {
      d[i] = 0;
      q.push(i);
    }
  }
  if (map.variant === 'mazing') return d.fill(1);
  let head = 0;
  while (head < q.length) {
    const i = q[head++];
    const x = i % map.width;
    const y = (i - x) / map.width;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx;
        const ny = y + dy;
        if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height) continue;
        const j = ny * map.width + nx;
        if (d[j] > d[i] + 1) {
          d[j] = d[i] + 1;
          q.push(j);
        }
      }
    }
  }
  return d;
}

// ---------------------------------------------------------------- Validierung & Fallback

export function validateMap(map: MapDefinition): ValidationResult {
  const errors: string[] = [];
  const { width: w, height: h } = map;
  if (w < 8 || h < 6 || w > 48 || h > 40) errors.push('Ungültige Kartengröße');
  if (map.cells.length !== w * h) errors.push('Zellanzahl passt nicht zur Kartengröße');
  const inb = (p: GridPos) => p.x >= 0 && p.y >= 0 && p.x < w && p.y < h;
  if (!inb(map.spawn)) errors.push('Start außerhalb der Karte');
  if (!inb(map.goal)) errors.push('Ziel außerhalb der Karte');
  if (map.spawn.x === map.goal.x && map.spawn.y === map.goal.y) errors.push('Start und Ziel sind identisch');
  if (errors.length) return { ok: false, errors, pathLength: 0, buildable: 0 };
  const starts = spawnsOf(map);
  for (const sp of starts) {
    if (!inb(sp) || map.cells[cellIndex(map, sp.x, sp.y)] !== Cell.Spawn) errors.push('Startzelle fehlt');
  }
  if (map.cells[cellIndex(map, map.goal.x, map.goal.y)] !== Cell.Goal) errors.push('Zielzelle fehlt');
  let spawns = 0;
  let goals = 0;
  for (const c of map.cells) {
    if (c === Cell.Spawn) spawns++;
    if (c === Cell.Goal) goals++;
  }
  if (spawns !== starts.length || spawns < 1 || spawns > 3 || goals !== 1) errors.push('Genau ein Ziel und 1–3 eingetragene Starts erforderlich');
  if (errors.length) return { ok: false, errors, pathLength: 0, buildable: 0 };
  const field = computeField(map, null);
  const sd = field[cellIndex(map, map.spawn.x, map.spawn.y)];
  if (starts.some((sp) => field[cellIndex(map, sp.x, sp.y)] >= INF)) errors.push('Kein begehbarer Weg vom Start zum Ziel');
  if (map.variant === 'classic') {
    for (let i = 0; i < map.cells.length; i++) {
      if (map.cells[i] === Cell.Path && field[i] >= INF) {
        errors.push('Wegzelle ohne Verbindung zum Ziel');
        break;
      }
    }
  }
  let buildable = 0;
  const dist = distanceToPath(map);
  for (let i = 0; i < map.cells.length; i++) {
    if (isBuildableCell(map, i) && dist[i] <= 2) buildable++;
  }
  const minBuild = Math.round((map.variant === 'classic' ? 30 : 60) * Math.min(2, (w * h) / (22 * 14)));
  if (buildable < minBuild) errors.push(`Zu wenige Bauflächen (${buildable} < ${minBuild})`);
  let walk = 0;
  for (let i = 0; i < map.cells.length; i++) if (isWalkableCell(map, i)) walk++;
  if (walk < 8) errors.push('Zu wenige begehbare Zellen');
  return { ok: errors.length === 0, errors, pathLength: sd < INF ? sd : 0, buildable };
}

/** Kontrollierter Fallback: deterministische Serpentinenkarte bzw. offenes Feld. */
export function fallbackMap(variant: MapVariant, biome: BiomeId): MapDefinition {
  const { w, h } = DIMS.normal[variant];
  const map = emptyMap(w, h, variant, biome);
  map.fallback = true;
  if (variant === 'classic') {
    const pts: GridPos[] = [];
    let x = 0;
    let y = 2;
    pts.push({ x, y });
    const lanes = [2, 11, 2, 11, 6];
    const cols = [1, 5, 9, 13, 17, 20];
    for (let k = 0; k < cols.length - 1; k++) {
      while (x < cols[k]) pts.push({ x: ++x, y });
      const ty = lanes[k];
      while (y !== ty) pts.push({ x, y: (y += Math.sign(ty - y)) });
    }
    while (x < w - 1) pts.push({ x: ++x, y });
    for (const p of pts) map.cells[p.y * w + p.x] = Cell.Path;
    map.spawn = pts[0];
    map.goal = pts[pts.length - 1];
  } else {
    map.spawn = { x: 0, y: Math.floor(h / 2) };
    map.goal = { x: w - 1, y: Math.floor(h / 2) };
    for (let dy = -1; dy <= 1; dy++) {
      for (const p of [map.spawn, map.goal]) {
        for (let dx = -1; dx <= 1; dx++) {
          const xx = p.x + dx;
          const yy = p.y + dy;
          if (xx >= 0 && xx < w && yy >= 0 && yy < h) map.cells[yy * w + xx] = Cell.NoBuild;
        }
      }
    }
  }
  map.cells[map.spawn.y * w + map.spawn.x] = Cell.Spawn;
  map.spawns = [map.spawn];
  map.cells[map.goal.y * w + map.goal.x] = Cell.Goal;
  map.name = 'Fallback';
  return map;
}
