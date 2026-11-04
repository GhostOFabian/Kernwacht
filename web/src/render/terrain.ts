import { BIOMES, type BiomeDefinition } from '../config/biomes.ts';
import { hashString, mix32 } from '../core/rng.ts';
import { Cell, type MapDefinition } from '../core/types.ts';
import { spawnsOf } from '../sim/WorldGenerator.ts';
import { mixColor, shade } from './math3d.ts';
import { MAT, type Renderer } from './Renderer.ts';

// Statische Weltgeometrie: schwebende Insel bzw. Insel im Meer, Gelände mit weichen Kanten,
// Wegband mit Randsteinen und Fahrspuren, Gras/Schilf (vom Wind bewegt), Dekor je Biom.

const TOP_STEP = 0.24;
/** Maßstab der Hindernis-Dekoration (Bäume, Felsen, Kristalle …) relativ zum Feld. */
export const DECOR_SCALE = 0.82;

export function cellTop(map: MapDefinition, x: number, y: number): number {
  if (x < 0 || y < 0 || x >= map.width || y >= map.height) return 0;
  const i = y * map.width + x;
  const c = map.cells[i];
  if (c === Cell.Path || c === Cell.Spawn || c === Cell.Goal) return -0.04;
  return map.heights[i] * TOP_STEP;
}

/** Leuchtpunkte, die jedes Bild als weiche Sprites gezeichnet werden (Kristalle, Lava, Pilze). */
export interface GlowPoint { x: number; y: number; z: number; size: number; color: number; phase: number }

export interface SeaInfo { y: number; floating: boolean; color: number }

export const SEA: Record<BiomeDefinition['sea'], SeaInfo & { mat: number }> = {
  water: { y: -0.72, floating: false, color: 0x1e5a7a, mat: MAT.water },
  lava: { y: -0.8, floating: false, color: 0xc03810, mat: MAT.lava },
  ice: { y: -0.74, floating: false, color: 0x7aa8c8, mat: MAT.water },
  sand: { y: -0.5, floating: false, color: 0xc8a868, mat: MAT.std },
  bog: { y: -0.68, floating: false, color: 0x2e3a24, mat: MAT.water },
  clouds: { y: -4.2, floating: true, color: 0xc8d4ec, mat: MAT.unlit },
  abyss: { y: -9, floating: true, color: 0x0e0a1a, mat: MAT.std },
};

const rnd = (seed: number, k: number): number => (mix32(seed * 7919 + k * 104729) % 10000) / 10000;

export function buildTerrain(r: Renderer, map: MapDefinition, detail: number): GlowPoint[] {
  const glows: GlowPoint[] = [];
  const biome = BIOMES[map.biome];
  const pal = biome.palette;
  const sea = SEA[biome.sea];
  const { width: w, height: h } = map;
  const salt = hashString(map.name + map.biome);
  r.beginStatic();

  // --- Meer / Wolken / Abgrund rund um die Insel
  const span = Math.max(w, h) + 70;
  r.add('opaque', 'plane', w / 2, sea.y, h / 2, span, 1, span, sea.color, 1, map.biome === 'cavern' ? 0.05 : 0, 0, 0, 0, sea.mat);
  if (biome.sea === 'clouds') {
    for (let k = 0; k < 70; k++) {
      const a = rnd(salt, k) * Math.PI * 2;
      const d = Math.max(w, h) * 0.45 + rnd(salt, k + 99) * 22;
      const s = 2.2 + rnd(salt, k + 7) * 3.2;
      r.add('opaque', 'sphere', w / 2 + Math.cos(a) * d, sea.y + 0.4 + rnd(salt, k + 3), h / 2 + Math.sin(a) * d, s * 1.6, s * 0.7, s * 1.3, mixColor(0xdfe8f8, 0xb8c4e0, rnd(salt, k + 5)), 1, 0, 0, 0, 0, MAT.unlit);
    }
  } else if (biome.sea === 'abyss') {
    for (let k = 0; k < 26; k++) {
      const a = rnd(salt, k) * Math.PI * 2;
      const d = Math.max(w, h) * 0.6 + rnd(salt, k + 99) * 14;
      const s = 0.6 + rnd(salt, k + 7) * 1.4;
      const y = -2 - rnd(salt, k + 3) * 4;
      r.add('opaque', 'gem', w / 2 + Math.cos(a) * d, y, h / 2 + Math.sin(a) * d, s * 1.4, s, s * 1.2, pal.obstacle2, 1, 0, a);
      r.add('opaque', 'cone8', w / 2 + Math.cos(a) * d, y - s * 0.3, h / 2 + Math.sin(a) * d, s, -s * 1.6, s, pal.cliff, 1, 0, a);
      if (k % 3 === 0) glows.push({ x: w / 2 + Math.cos(a) * d, y: y + s * 0.6, z: h / 2 + Math.sin(a) * d, size: 0.6, color: pal.accent, phase: k });
    }
  } else if (biome.sea === 'ice') {
    for (let k = 0; k < 40; k++) {
      const a = rnd(salt, k) * Math.PI * 2;
      const d = Math.max(w, h) * 0.55 + rnd(salt, k + 99) * 16;
      const s = 0.8 + rnd(salt, k + 7) * 2;
      r.add('opaque', 'cyl8', w / 2 + Math.cos(a) * d, sea.y - 0.1, h / 2 + Math.sin(a) * d, s, 0.22, s * 0.8, 0xeaf6ff, 1, 0.05, a);
    }
  } else if (biome.sea === 'sand') {
    for (let k = 0; k < 40; k++) {
      const a = rnd(salt, k) * Math.PI * 2;
      const d = Math.max(w, h) * 0.55 + rnd(salt, k + 99) * 18;
      const s = 2 + rnd(salt, k + 7) * 4;
      r.add('opaque', 'sphere', w / 2 + Math.cos(a) * d, sea.y - s * 0.3, h / 2 + Math.sin(a) * d, s * 1.8, s * 0.55, s, shade(sea.color, 0.92 + rnd(salt, k) * 0.12), 1, 0, a);
    }
  }

  // --- Inselsockel
  const cliff = pal.cliff;
  if (sea.floating) {
    r.add('opaque', 'rbox', w / 2, -1.25, h / 2, w + 0.6, 1.4, h + 0.6, shade(cliff, 0.85), 1, 0, 0, 0, 0, MAT.terrain);
    r.add('opaque', 'rbox', w / 2, -2.35, h / 2, w - 1.2, 1.2, h - 1.2, shade(cliff, 0.7), 1, 0, 0, 0, 0, MAT.terrain);
    r.add('opaque', 'rbox', w / 2, -3.25, h / 2, w - 4, 1.0, h - 4, shade(cliff, 0.55), 1, 0, 0, 0, 0, MAT.terrain);
    const n = Math.round((w + h) * 0.8);
    for (let k = 0; k < n; k++) {
      const x = 1 + rnd(salt, k * 3) * (w - 2);
      const z = 1 + rnd(salt, k * 3 + 1) * (h - 2);
      const s = 0.8 + rnd(salt, k * 3 + 2) * 1.8;
      r.add('opaque', 'cone8', x, -2.2 - rnd(salt, k) * 1.2, z, s, -s * (1.6 + rnd(salt, k + 5) * 2), s, shade(cliff, 0.5 + rnd(salt, k + 9) * 0.2), 1, 0, k);
    }
  } else {
    r.add('opaque', 'rbox', w / 2, -0.9, h / 2, w + 0.5, 0.9, h + 0.5, shade(cliff, 0.8), 1, 0, 0, 0, 0, MAT.terrain);
  }

  // --- Zellen
  const isPath = (x: number, y: number): boolean => {
    if (x < 0 || y < 0 || x >= w || y >= h) return false;
    const c = map.cells[y * w + x];
    return c === Cell.Path || c === Cell.Spawn || c === Cell.Goal || (map.variant === 'mazing' && c === Cell.NoBuild);
  };
  const lush = map.biome === 'forest' || map.biome === 'swamp' || map.biome === 'sky';
  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      const i = y * w + x;
      const c = map.cells[i];
      const top = cellTop(map, x, y);
      const n = rnd(salt, i);
      const cx = x + 0.5;
      const cz = y + 0.5;
      if (c === Cell.Path || c === Cell.Spawn || c === Cell.Goal) {
        const col = shade(pal.path, 0.96 + n * 0.08);
        r.add('opaque', 'box', cx, (top - 0.7) / 2, cz, 1, top + 0.7, 1, col, 1, 0, 0, 0, 0, MAT.terrain);
        // Fahrspuren: zwei dunkle Streifen in Laufrichtung
        const rut = shade(pal.path, 0.82);
        const dirs: [number, number][] = [[1, 0], [-1, 0], [0, 1], [0, -1]];
        for (const [dx, dy] of dirs) {
          if (!isPath(x + dx, y + dy)) continue;
          for (const off of [-0.2, 0.2]) {
            const ox = dy !== 0 ? off : dx * 0.25;
            const oz = dx !== 0 ? off : dy * 0.25;
            r.add('opaque', 'box', cx + ox, top + 0.004, cz + oz, dx !== 0 ? 0.5 : 0.07, 0.01, dy !== 0 ? 0.5 : 0.07, rut, 1, 0, 0, 0, 0, MAT.terrain);
          }
        }
        // Randsteine zur Wiese hin
        for (const [dx, dy] of dirs) {
          if (isPath(x + dx, y + dy) || x + dx < 0 || y + dy < 0 || x + dx >= w || y + dy >= h) continue;
          if (map.cells[(y + dy) * w + x + dx] === Cell.Blocked && detail < 2) continue;
          const stones = detail >= 2 ? 3 : 2;
          for (let k = 0; k < stones; k++) {
            const t = (k + 0.5) / stones - 0.5 + (rnd(salt, i * 13 + k + dx * 3 + dy * 5) - 0.5) * 0.12;
            const sx = cx + dx * 0.44 + (dy !== 0 ? t : 0);
            const sz = cz + dy * 0.44 + (dx !== 0 ? t : 0);
            const s = 0.14 + rnd(salt, i * 17 + k + dx + dy * 2) * 0.1;
            r.add('opaque', 'rbox', sx, top + s * 0.25, sz, s * 1.2, s * 0.7, s, shade(mixColor(pal.cliff, 0xb0a898, 0.45), 0.85 + rnd(salt, i + k) * 0.3), 1, 0, rnd(salt, i * 5 + k) * 3);
          }
        }
        continue;
      }
      // Gelände: Sockel (Fels) + Deckschicht mit weicher Lippe an Abbruchkanten
      const hi = map.heights[i] >= 2;
      let capCol = hi ? pal.high : (x * 7 + y * 3) % 5 === 0 ? pal.ground2 : pal.ground;
      if (c === Cell.NoBuild) capCol = shade(capCol, 0.86);
      capCol = shade(mixColor(capCol, pal.ground2, n * 0.5), 0.97 + n * 0.06);
      r.add('opaque', 'box', cx, (top - 0.7) / 2 - 0.08, cz, 1, top + 0.7 - 0.16, 1, shade(mixColor(cliff, capCol, 0.35), 0.9 + n * 0.1), 1, 0, 0, 0, 0, MAT.terrain);
      r.add('opaque', 'box', cx, top - 0.08, cz, 1, 0.16, 1, capCol, 1, 0, 0, 0, 0, MAT.terrain);
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]] as [number, number][]) {
        const nt = x + dx < 0 || y + dy < 0 || x + dx >= w || y + dy >= h ? -1 : cellTop(map, x + dx, y + dy);
        if (nt < top - 0.1) {
          r.add('opaque', 'rbox', cx + dx * 0.47, top - 0.07, cz + dy * 0.47, dx !== 0 ? 0.16 : 1.02, 0.17, dy !== 0 ? 0.16 : 1.02, shade(capCol, 1.04), 1, 0, 0, 0, 0, MAT.terrain);
        }
      }
      if (c === Cell.Blocked) {
        addDecorBlock(r, map, i, cx, top, cz, pal, glows);
        continue;
      }
      // Bewuchs / Bodendetails
      if (detail <= 0) continue;
      const tufts = lush ? (detail >= 2 ? 3 : 2) : detail >= 2 ? 2 : 1;
      for (let k = 0; k < tufts; k++) {
        const a = rnd(salt, i * 31 + k);
        const b = rnd(salt, i * 37 + k + 11);
        const px = x + 0.15 + a * 0.7;
        const pz = y + 0.15 + b * 0.7;
        addGroundDetail(r, map.biome, px, top, pz, rnd(salt, i * 41 + k), capCol, pal.accent, glows);
      }
    }
  }

  // --- Ressourcen
  for (const i of map.veins) {
    const x = i % w;
    const y = Math.floor(i / w);
    const top = cellTop(map, x, y);
    r.add('opaque', 'gem', x + 0.25, top + 0.06, y + 0.3, 0.2, 0.16, 0.2, 0xf0c040, 1, 0.5, 0.3);
    r.add('opaque', 'gem', x + 0.72, top + 0.05, y + 0.66, 0.15, 0.13, 0.15, 0xf0c040, 1, 0.5, 1.1);
    r.add('opaque', 'gem', x + 0.7, top + 0.04, y + 0.24, 0.12, 0.1, 0.12, 0xffe080, 1, 0.5);
    glows.push({ x: x + 0.5, y: top + 0.15, z: y + 0.5, size: 0.35, color: 0xffd060, phase: i });
  }
  for (const i of map.deposits ?? []) {
    const x = i % w;
    const y = Math.floor(i / w);
    const top = cellTop(map, x, y);
    r.add('opaque', 'octa', x + 0.3, top + 0.12, y + 0.35, 0.16, 0.3, 0.16, 0x7ae0ff, 1, 0.35, 0.3, 0.2);
    r.add('opaque', 'octa', x + 0.68, top + 0.1, y + 0.62, 0.13, 0.24, 0.13, 0x5ac0f0, 1, 0.35, 1.1, -0.25);
    r.add('opaque', 'octa', x + 0.62, top + 0.08, y + 0.26, 0.1, 0.18, 0.1, 0xb0f0ff, 1, 0.35, 0.6, 0.3);
    r.add('opaque', 'disc', x + 0.5, top + 0.015, y + 0.5, 0.86, 1, 0.86, 0x2a5a7a, 0.9, 0);
    glows.push({ x: x + 0.5, y: top + 0.15, z: y + 0.5, size: 0.3, color: 0x7ae0ff, phase: i * 1.3 });
  }
  for (const i of map.ley) {
    const x = i % w;
    const y = Math.floor(i / w);
    const top = cellTop(map, x, y);
    r.add('transparent', 'ring', x + 0.5, top + 0.02, y + 0.5, 0.42, 1, 0.42, 0xc070ff, 0.8, 1);
    glows.push({ x: x + 0.5, y: top + 0.1, z: y + 0.5, size: 0.5, color: 0xc070ff, phase: i * 0.7 });
  }

  // --- Ränder außerhalb der Karte: Uferfelsen, Inselchen
  for (let y = -4; y < h + 4; y++) {
    for (let x = -4; x < w + 4; x++) {
      if (x >= 0 && y >= 0 && x < w && y < h) continue;
      const hsh = mix32(salt ^ ((x + 100) * 73856093) ^ ((y + 100) * 19349663));
      const d = Math.max(x < 0 ? -x : x >= w ? x - w + 1 : 0, y < 0 ? -y : y >= h ? y - h + 1 : 0);
      if (hsh % 100 < (sea.floating ? 40 + d * 22 : 14 + d * 16)) continue;
      const lift = sea.floating ? -0.3 - d * 0.25 : sea.y + 0.35 - d * 0.12 + (hsh % 7) * 0.05;
      const col = shade(hsh % 2 ? pal.ground : pal.ground2, 0.85 - d * 0.06);
      r.add('opaque', 'rbox', x + 0.5, lift - 0.5, y + 0.5, 0.98, 1, 0.98, col, 1, 0, 0, 0, 0, MAT.terrain);
      if (sea.floating) r.add('opaque', 'cone8', x + 0.5, lift - 1, y + 0.5, 0.9, -1.2 - (hsh % 5) * 0.3, 0.9, shade(cliff, 0.6));
      if (hsh % 4 === 0) {
        const kinds = biome.decorKinds;
        addDecorKind(r, kinds[hsh % kinds.length], x + 0.5, lift, y + 0.5, (hsh % 628) / 100, pal, 0.8, glows);
      }
    }
  }

  // --- Startsockel und Kernplattform
  for (const sp of spawnsOf(map)) {
    r.add('opaque', 'cyl16', sp.x + 0.5, -0.06, sp.y + 0.5, 1.0, 0.1, 1.0, 0x2a1838);
    r.add('opaque', 'rbox', sp.x + 0.5 + 0.48, 0.3, sp.y + 0.5, 0.16, 0.7, 0.16, shade(cliff, 1.1));
    r.add('opaque', 'rbox', sp.x + 0.5 - 0.48, 0.3, sp.y + 0.5, 0.16, 0.7, 0.16, shade(cliff, 1.1));
  }
  const gp = map.goal;
  r.add('opaque', 'cyl16', gp.x + 0.5, -0.06, gp.y + 0.5, 1.0, 0.16, 1.0, shade(cliff, 1.15));
  r.add('opaque', 'hex', gp.x + 0.5, 0.08, gp.y + 0.5, 0.72, 0.1, 0.72, shade(cliff, 1.35));
  for (let k = 0; k < 4; k++) {
    const a = (k / 4) * Math.PI * 2 + Math.PI / 4;
    r.add('opaque', 'rbox', gp.x + 0.5 + Math.cos(a) * 0.42, 0.25, gp.y + 0.5 + Math.sin(a) * 0.42, 0.12, 0.5, 0.12, shade(cliff, 1.3), 1, 0, -a);
  }
  r.endStatic();
  return glows;
}

function addGroundDetail(r: Renderer, biome: string, x: number, y: number, z: number, n: number, ground: number, accent: number, glows: GlowPoint[]): void {
  switch (biome) {
    case 'forest':
    case 'sky': {
      const col = shade(mixColor(ground, 0xd8ff90, 0.35), 1.05 + n * 0.25);
      r.add('opaque', 'cone6', x, y - 0.02, z, 0.14, 0.16 + n * 0.08, 0.14, col, 1, 0.12, n * 6, 0.15, 0, MAT.sway);
      r.add('opaque', 'cone6', x + 0.07, y - 0.02, z + 0.03, 0.1, 0.12 + n * 0.06, 0.1, shade(col, 1.1), 1, 0.12, n * 3, -0.25, 0.1, MAT.sway);
      if (n > 0.85) r.add('opaque', 'sphereLo', x - 0.05, y + 0.18, z - 0.04, 0.07, 0.07, 0.07, biome === 'sky' ? 0xffe0f0 : 0xfff090, 1, 0.3);
      break;
    }
    case 'swamp': {
      const col = shade(0x7a8a46, 0.95 + n * 0.3);
      r.add('opaque', 'cone6', x, y, z, 0.05, 0.4 + n * 0.25, 0.05, col, 1, 0, n * 6, 0.1, 0, MAT.sway);
      r.add('opaque', 'cone6', x + 0.05, y, z, 0.04, 0.3 + n * 0.2, 0.04, shade(col, 1.1), 1, 0, n, -0.2, 0, MAT.sway);
      if (n > 0.8) r.add('opaque', 'cyl8', x, y + 0.4, z, 0.05, 0.12, 0.05, 0x5a3a20, 1, 0, 0, 0, 0, MAT.sway);
      break;
    }
    case 'frozen':
      r.add('opaque', 'sphereLo', x, y, z, 0.2 + n * 0.15, 0.08, 0.16 + n * 0.1, 0xf4faff, 1, 0.05, n * 5);
      break;
    case 'volcano':
      if (n > 0.55) {
        r.add('opaque', 'box', x, y + 0.002, z, 0.4, 0.01, 0.05, 0xff6020, 1, 1, n * 6, 0, 0, MAT.lava);
        if (n > 0.85) glows.push({ x, y: y + 0.05, z, size: 0.3, color: 0xff6020, phase: n * 10 });
      } else r.add('opaque', 'gem', x, y + 0.03, z, 0.12, 0.08, 0.1, 0x2a2224, 1, 0, n * 6);
      break;
    case 'crystal':
      if (n > 0.5) r.add('opaque', 'octa', x, y + 0.06, z, 0.07, 0.18, 0.07, accent, 1, 0.5, n * 6, 0.2);
      else r.add('opaque', 'gem', x, y + 0.02, z, 0.12, 0.06, 0.1, 0xb89860, 1, 0, n * 6);
      break;
    case 'cavern':
      if (n > 0.6) {
        r.add('opaque', 'cyl8', x, y, z, 0.03, 0.1, 0.03, 0xd0c8e0);
        r.add('opaque', 'sphereLo', x, y + 0.1, z, 0.1, 0.05, 0.1, accent, 1, 0.9);
        if (n > 0.8) glows.push({ x, y: y + 0.12, z, size: 0.22, color: accent, phase: n * 9 });
      } else r.add('opaque', 'gem', x, y + 0.02, z, 0.1, 0.06, 0.08, 0x4a4466, 1, 0, n * 6);
      break;
    default:
      break;
  }
}

function addDecorBlock(r: Renderer, map: MapDefinition, i: number, x: number, top: number, z: number, pal: BiomeDefinition['palette'], glows: GlowPoint[]): void {
  let kind = BIOMES[map.biome].decorKinds[0];
  let rot = 0;
  for (let k = 0; k < map.decor.length; k += 3) {
    if (map.decor[k] === i) {
      kind = map.decor[k + 1];
      rot = map.decor[k + 2] / 100;
      break;
    }
  }
  // 2.9.5: Bäume/Felsen etwas kleiner (wie die Modelle), damit sie Türme auf Nachbarfeldern – v. a. auf Anhöhen – nicht verdecken
  addDecorKind(r, kind, x, top, z, rot, pal, kind === 3 || kind === 7 ? 1 : DECOR_SCALE, glows);
}

export function addDecorKind(r: Renderer, kind: number, x: number, y: number, z: number, rot: number, pal: BiomeDefinition['palette'], s: number, glows: GlowPoint[]): void {
  const sway = MAT.sway;
  switch (kind) {
    case 0: { // Laubbaum mit runder Krone
      r.add('opaque', 'cyl8', x, y, z, 0.18 * s, 0.6 * s, 0.18 * s, 0x6a4a2a, 1, 0, rot);
      r.add('opaque', 'sphere', x, y + 0.75 * s, z, 0.78 * s, 0.66 * s, 0.78 * s, pal.obstacle, 1, 0, rot, 0, 0, sway);
      r.add('opaque', 'sphere', x + 0.18 * s, y + 0.98 * s, z - 0.1 * s, 0.52 * s, 0.46 * s, 0.52 * s, shade(pal.obstacle, 1.18), 1, 0, rot, 0, 0, sway);
      r.add('opaque', 'sphere', x - 0.16 * s, y + 0.62 * s, z + 0.16 * s, 0.46 * s, 0.4 * s, 0.46 * s, shade(pal.obstacle, 0.88), 1, 0, rot, 0, 0, sway);
      break;
    }
    case 1: // Fels
      r.add('opaque', 'gem', x, y + 0.18 * s, z, 0.82 * s, 0.56 * s, 0.7 * s, pal.obstacle2, 1, 0, rot);
      r.add('opaque', 'gem', x + 0.24 * s, y + 0.08 * s, z - 0.22 * s, 0.38 * s, 0.3 * s, 0.36 * s, shade(pal.obstacle2, 0.82), 1, 0, rot + 1);
      r.add('opaque', 'sphereLo', x - 0.1 * s, y + 0.38 * s, z + 0.05 * s, 0.3 * s, 0.08 * s, 0.25 * s, shade(pal.ground, 1.05), 1, 0, rot);
      break;
    case 2: // Kristallgruppe
      r.add('opaque', 'octa', x, y + 0.5 * s, z, 0.34 * s, 1.1 * s, 0.34 * s, pal.accent, 1, 0.55, rot, 0.12);
      r.add('opaque', 'octa', x + 0.24 * s, y + 0.26 * s, z + 0.1 * s, 0.2 * s, 0.6 * s, 0.2 * s, pal.obstacle, 1, 0.45, rot, -0.35);
      r.add('opaque', 'octa', x - 0.2 * s, y + 0.2 * s, z - 0.14 * s, 0.16 * s, 0.45 * s, 0.16 * s, pal.accent, 1, 0.45, rot + 1, 0.4);
      glows.push({ x, y: y + 0.6 * s, z, size: 0.9 * s, color: pal.accent, phase: rot * 3 });
      break;
    case 3: // Lavabecken
      r.add('opaque', 'cyl16', x, y - 0.02, z, 0.86 * s, 0.05, 0.86 * s, 0xff6a20, 1, 1, 0, 0, 0, MAT.lava);
      r.add('opaque', 'gem', x + 0.3 * s, y + 0.1, z + 0.22 * s, 0.32 * s, 0.26 * s, 0.32 * s, pal.obstacle, 1, 0, rot);
      r.add('opaque', 'gem', x - 0.32 * s, y + 0.06, z - 0.2 * s, 0.24 * s, 0.2 * s, 0.24 * s, pal.obstacle, 1, 0, rot + 2);
      glows.push({ x, y: y + 0.1, z, size: 0.9 * s, color: 0xff6a20, phase: rot * 2 });
      break;
    case 4: // Eisblock
      r.add('opaque', 'rbox', x, y + 0.28 * s, z, 0.72 * s, 0.56 * s, 0.62 * s, 0xc8ecff, 1, 0.12, rot);
      r.add('opaque', 'octa', x + 0.2 * s, y + 0.6 * s, z - 0.1 * s, 0.22 * s, 0.6 * s, 0.22 * s, 0xe8f8ff, 1, 0.25, rot, 0.25);
      break;
    case 5: // Ruine
      r.add('opaque', 'cyl12', x - 0.18 * s, y, z, 0.3 * s, 0.95 * s, 0.3 * s, pal.obstacle2, 1, 0, rot);
      r.add('opaque', 'rbox', x - 0.18 * s, y + 1.0 * s, z, 0.38 * s, 0.1 * s, 0.38 * s, shade(pal.obstacle2, 1.1), 1, 0, rot);
      r.add('opaque', 'rbox', x + 0.18 * s, y + 0.12, z + 0.16 * s, 0.44 * s, 0.24 * s, 0.3 * s, pal.obstacle, 1, 0, rot);
      r.add('opaque', 'cyl12', x + 0.1 * s, y + 0.1, z - 0.22 * s, 0.2 * s, 0.6 * s, 0.2 * s, pal.obstacle2, 1, 0, rot, 0, Math.PI / 2);
      break;
    case 6: // Pilz
      r.add('opaque', 'cyl8', x, y, z, 0.16 * s, 0.38 * s, 0.16 * s, 0xe8dcc0);
      r.add('opaque', 'sphere', x, y + 0.42 * s, z, 0.56 * s, 0.32 * s, 0.56 * s, 0xd04a4a);
      r.add('opaque', 'sphereLo', x + 0.12 * s, y + 0.55 * s, z + 0.06 * s, 0.07 * s, 0.05 * s, 0.07 * s, 0xffffff);
      r.add('opaque', 'sphereLo', x - 0.1 * s, y + 0.54 * s, z - 0.1 * s, 0.06 * s, 0.05 * s, 0.06 * s, 0xffffff);
      break;
    case 7: // Wolke / Nebelbank
      r.add('opaque', 'sphere', x, y + 0.25, z, 0.72 * s, 0.42 * s, 0.62 * s, 0xf0f4ff, 1, 0.2);
      r.add('opaque', 'sphere', x + 0.25, y + 0.32, z + 0.1, 0.5 * s, 0.38 * s, 0.48 * s, 0xffffff, 1, 0.2);
      break;
    case 8: // Trauerweide / Schilf
      r.add('opaque', 'cyl8', x, y, z, 0.2 * s, 0.7 * s, 0.2 * s, 0x4a3a28, 1, 0, rot, 0.08);
      r.add('opaque', 'sphere', x, y + 0.9 * s, z, 0.9 * s, 0.5 * s, 0.9 * s, pal.obstacle, 1, 0, rot, 0, 0, sway);
      for (let k = 0; k < 5; k++) {
        const a = rot + (k / 5) * Math.PI * 2;
        r.add('opaque', 'cone6', x + Math.cos(a) * 0.36 * s, y + 0.95 * s, z + Math.sin(a) * 0.36 * s, 0.14 * s, -0.6 * s, 0.14 * s, shade(pal.obstacle, 1.15), 1, 0, a, 0, 0, sway);
      }
      break;
    case 9: // Leuchtpilz
      r.add('opaque', 'cyl8', x, y, z, 0.14 * s, 0.7 * s, 0.14 * s, 0xd8d0e8, 1, 0.1, 0, 0.06);
      r.add('opaque', 'sphere', x, y + 0.72 * s, z, 0.76 * s, 0.34 * s, 0.76 * s, pal.accent, 1, 0.8);
      r.add('opaque', 'cyl8', x + 0.28 * s, y, z + 0.2 * s, 0.08 * s, 0.32 * s, 0.08 * s, 0xd8d0e8);
      r.add('opaque', 'sphere', x + 0.28 * s, y + 0.34 * s, z + 0.2 * s, 0.3 * s, 0.14 * s, 0.3 * s, 0xc080ff, 1, 0.8);
      glows.push({ x, y: y + 0.7 * s, z, size: 1.0 * s, color: pal.accent, phase: rot * 4 });
      break;
    default:
      break;
  }
}
