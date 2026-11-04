import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SIM } from '../src/config/balance.ts';
import { Cell, type MapDefinition } from '../src/core/types.ts';
import { decodeMap, encodeMap, finalizeEditorMap } from '../src/editor/mapCode.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { generateWorld, makeSeed, MAP_VERSION, validateMap } from '../src/sim/WorldGenerator.ts';

function editorLine(): MapDefinition {
  const w = 22;
  const hh = 14;
  const cells = new Array<number>(w * hh).fill(Cell.Open);
  const my = 7;
  for (let x = 0; x < w; x++) cells[my * w + x] = Cell.Path;
  cells[my * w] = Cell.Spawn;
  cells[my * w + w - 1] = Cell.Goal;
  return finalizeEditorMap({
    version: MAP_VERSION, width: w, height: hh, variant: 'classic', biome: 'forest', cells, heights: [], veins: [], ley: [], decor: [],
    spawn: { x: 0, y: my }, goal: { x: w - 1, y: my }, terrainEvent: null, fallback: false, name: 'Testkarte',
  });
}

const heRun = (map: MapDefinition) => GameSimulation.create({
  mode: 'heroEndless', seedText: 'EDIT', variant: map.variant, biome: map.biome, difficulty: 'normal', customMap: map,
  heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {} }, runId: 'v310' },
});

test('3.10: Editorkarte (Kartencode) lässt sich im Helden-Endlos spielen – Laden, Held, Wellen', () => {
  const m0 = editorLine();
  assert.ok(validateMap(m0).ok);
  const code = encodeMap(m0);
  const m = decodeMap(code).map!;
  assert.ok(m);
  const sim = heRun({ ...m, name: 'Testkarte' });
  const he = sim.s.he!;
  assert.equal(sim.s.mode, 'heroEndless');
  assert.equal(sim.s.map.width, 22);
  assert.ok(he.shop.x >= 0 && he.shop.y >= 0, 'Laden platziert');
  assert.equal(sim.s.map.cells[he.shop.y * 22 + he.shop.x] !== Cell.Path, true, 'Laden nicht auf der Lane');
  sim.startWave();
  for (let i = 0; i < Math.round(40 / SIM.dt) && sim.s.wave < 2; i++) sim.step();
  assert.ok(sim.s.wave >= 1);
  assert.ok(!sim.isOver());
});

test('3.10: Vorlage aus Seed als Editorkarte im Helden-Endlos (Mazing)', () => {
  const g = generateWorld({ seed: makeSeed('VORLAGE'), variant: 'mazing', biome: 'volcano' });
  const m = finalizeEditorMap({ ...g, heights: [], decor: [], name: 'Vorlage' });
  assert.ok(validateMap(m).ok);
  const sim = heRun(m);
  assert.equal(sim.s.map.variant, 'mazing');
  assert.ok(sim.s.he!.shop.x >= 0);
});
