import { BIOME_ORDER, BIOMES } from '../config/biomes.ts';
import { hashString, mix32 } from '../core/rng.ts';
import { Cell, type BiomeId, type MapDefinition, type MapVariant } from '../core/types.ts';
import { MAP_VERSION, validateMap, type ValidationResult } from '../sim/WorldGenerator.ts';

// Kartencodes für den lokalen Editor: kompaktes, versioniertes Textformat.
//   RB1.<variant>.<biome>.<w>x<h>.<zellen>.<adern>.<leylinien>
// Zellen werden als Ziffernfolge (Cell-Codes 0–5) mit Lauflängenkodierung gespeichert.

const PREFIX = 'RB1';

function rle(cells: number[]): string {
  let out = '';
  let i = 0;
  while (i < cells.length) {
    const c = cells[i];
    let n = 1;
    while (i + n < cells.length && cells[i + n] === c && n < 999) n++;
    out += n > 1 ? `${c}*${n},` : `${c},`;
    i += n;
  }
  return out.replace(/,$/, '');
}

function unrle(s: string, expected: number): number[] | null {
  const out: number[] = [];
  for (const part of s.split(',')) {
    const m = /^([0-5])(?:\*(\d{1,3}))?$/.exec(part);
    if (!m) return null;
    const n = m[2] ? Number(m[2]) : 1;
    for (let k = 0; k < n; k++) out.push(Number(m[1]));
    if (out.length > expected) return null;
  }
  return out.length === expected ? out : null;
}

export function encodeMap(map: MapDefinition): string {
  return [
    PREFIX, map.variant, map.biome, `${map.width}x${map.height}`, rle(map.cells),
    map.veins.join('-'), map.ley.join('-'),
  ].join('.');
}

export interface DecodeResult { map: MapDefinition | null; error: string | null; validation: ValidationResult | null }

/** Ergänzt abgeleitete Felder (Start/Ziel aus Zellen, Höhen, Dekoration) – deterministisch. */
export function finalizeEditorMap(map: MapDefinition): MapDefinition {
  const w = map.width;
  const spawns: { x: number; y: number }[] = [];
  for (let i = 0; i < map.cells.length; i++) {
    if (map.cells[i] === Cell.Spawn) spawns.push({ x: i % w, y: Math.floor(i / w) });
    if (map.cells[i] === Cell.Goal) map.goal = { x: i % w, y: Math.floor(i / w) };
  }
  if (spawns.length > 0) map.spawn = spawns[0];
  map.spawns = spawns.length > 0 ? spawns : [map.spawn];
  map.heights = new Array<number>(map.cells.length).fill(0);
  const kinds = BIOMES[map.biome].decorKinds;
  const salt = hashString(map.biome + map.variant);
  map.decor = [];
  for (let i = 0; i < map.cells.length; i++) {
    if (map.cells[i] !== Cell.Blocked) continue;
    const h = mix32(salt ^ (i * 2654435761));
    map.decor.push(i, kinds[h % kinds.length], h % 628);
  }
  map.veins = map.veins.filter((i) => map.cells[i] === Cell.Open);
  map.ley = map.ley.filter((i) => map.cells[i] === Cell.Open && !map.veins.includes(i));
  map.version = MAP_VERSION;
  return map;
}

export function decodeMap(code: string): DecodeResult {
  const fail = (error: string): DecodeResult => ({ map: null, error, validation: null });
  const parts = code.trim().split('.');
  if (parts.length !== 7 || parts[0] !== PREFIX) return fail('Unbekanntes Kartencode-Format');
  const [, variant, biome, dims, cellStr, veinStr, leyStr] = parts;
  if (variant !== 'classic' && variant !== 'mazing') return fail('Unbekannte Kartenvariante');
  if (!(BIOME_ORDER as string[]).includes(biome)) return fail('Unbekanntes Biom');
  const dm = /^(\d{1,2})x(\d{1,2})$/.exec(dims);
  if (!dm) return fail('Ungültige Kartengröße');
  const w = Number(dm[1]);
  const h = Number(dm[2]);
  if (w < 8 || h < 6 || w > 48 || h > 40) return fail('Kartengröße außerhalb des erlaubten Bereichs');
  const cells = unrle(cellStr, w * h);
  if (!cells) return fail('Zellen sind beschädigt');
  const parseList = (s: string): number[] | null => {
    if (s === '') return [];
    const xs = s.split('-').map(Number);
    return xs.every((x) => Number.isInteger(x) && x >= 0 && x < w * h) ? xs : null;
  };
  const veins = parseList(veinStr);
  const ley = parseList(leyStr);
  if (!veins || !ley) return fail('Ressourcenliste ist beschädigt');
  const map: MapDefinition = finalizeEditorMap({
    version: MAP_VERSION, width: w, height: h, variant: variant as MapVariant, biome: biome as BiomeId,
    cells, heights: [], veins, ley, decor: [], spawn: { x: 0, y: 0 }, goal: { x: 0, y: 0 },
    terrainEvent: null, fallback: false, name: 'Eigene Karte',
  });
  const validation = validateMap(map);
  return { map, error: null, validation };
}
