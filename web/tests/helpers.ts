import { Cell, type MapVariant, type TowerId } from '../src/core/types.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import type { RunSetup } from '../src/sim/setup.ts';
import { spawnEnemy } from '../src/sim/EnemySystem.ts';
import type { EnemyId } from '../src/core/types.ts';

export function makeSim(over: Partial<RunSetup> = {}): GameSimulation {
  return GameSimulation.create({ mode: 'quick', seedText: 'TEST-SEED', variant: 'classic', biome: 'forest', difficulty: 'normal', ...over });
}

/** Erste bebaubare Zelle (optional mit Mindestabstand zu einer Position). */
export function openCells(sim: GameSimulation): { x: number; y: number }[] {
  const m = sim.s.map;
  const out: { x: number; y: number }[] = [];
  for (let y = 0; y < m.height; y++) for (let x = 0; x < m.width; x++) if (m.cells[y * m.width + x] === Cell.Open && !sim.towerAt(x, y)) out.push({ x, y });
  return out;
}

export function firstOpen(sim: GameSimulation): { x: number; y: number } {
  const c = openCells(sim)[0];
  if (!c) throw new Error('keine freie Zelle');
  return c;
}

/** Baut einen Turm auf der ersten freien Zelle neben dem Weg und gibt die Instanz zurück. */
export function buildNearPath(sim: GameSimulation, def: TowerId, skip = 0) {
  const m = sim.s.map;
  let n = 0;
  for (let y = 0; y < m.height; y++) {
    for (let x = 0; x < m.width; x++) {
      if (m.cells[y * m.width + x] !== Cell.Open || sim.towerAt(x, y)) continue;
      let near = false;
      for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
        const c = m.cells[(y + dy) * m.width + x + dx];
        if (c === Cell.Path) near = true;
      }
      if (!near) continue;
      if (n++ < skip) continue;
      sim.s.gold = Math.max(sim.s.gold, 10000);
      const r = sim.build(def, x, y);
      if (!r.ok) throw new Error(r.reason);
      const t = sim.towerAt(x, y);
      if (!t) throw new Error('Turm fehlt');
      return t;
    }
  }
  throw new Error('kein Bauplatz am Weg');
}

export function addEnemy(sim: GameSimulation, id: EnemyId, x?: number, y?: number) {
  const e = spawnEnemy(sim, id, false, null);
  if (x !== undefined && y !== undefined) {
    e.x = x;
    e.y = y;
  }
  return e;
}

export function variantOf(v: MapVariant): Partial<RunSetup> {
  return { variant: v };
}
