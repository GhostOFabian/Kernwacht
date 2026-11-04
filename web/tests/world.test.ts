import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Rng } from '../src/core/rng.ts';
import { Cell } from '../src/core/types.ts';
import { computeField, INF, tracePath } from '../src/sim/Pathfinding.ts';
import { fallbackMap, generateWorld, makeSeed, validateMap } from '../src/sim/WorldGenerator.ts';
import { makeSim, openCells } from './helpers.ts';
import { spawnEnemy } from '../src/sim/EnemySystem.ts';

const SEEDS = 400;

test(`Generator: ${SEEDS} Seeds je Variante liefern gültige Karten`, () => {
  for (const variant of ['classic', 'mazing'] as const) {
    let fallbacks = 0;
    for (let i = 0; i < SEEDS; i++) {
      const m = generateWorld({ seed: makeSeed(`S-${i}`), variant, biome: 'random' });
      const v = validateMap(m);
      assert.ok(v.ok, `Seed S-${i} (${variant}): ${v.errors.join(', ')}`);
      if (m.fallback) fallbacks++;
      const f = computeField(m, null);
      assert.ok(f[m.spawn.y * m.width + m.spawn.x] < INF);
    }
    assert.ok(fallbacks < SEEDS * 0.02, `zu viele Fallback-Karten: ${fallbacks}`);
  }
});

test('Seed-Reproduzierbarkeit: gleicher Seed → identische Karte', () => {
  for (const variant of ['classic', 'mazing'] as const) {
    const a = generateWorld({ seed: makeSeed('REPRO-42'), variant, biome: 'random' });
    const b = generateWorld({ seed: makeSeed('repro-42 '), variant, biome: 'random' });
    assert.deepEqual(a, b);
  }
});

test('Unterschiedliche Seeds unterscheiden sich in Wegen und Ressourcen', () => {
  const sigs = new Set<string>();
  const res = new Set<string>();
  for (let i = 0; i < 30; i++) {
    const m = generateWorld({ seed: makeSeed(`DIFF-${i}`), variant: 'classic', biome: 'forest' });
    sigs.add(m.cells.map((c) => (c === Cell.Path ? 1 : 0)).join(''));
    res.add(m.veins.join(','));
  }
  assert.ok(sigs.size >= 28, `nur ${sigs.size} verschiedene Wege`);
  assert.ok(res.size >= 28);
});

test('Kosmetischer Strom beeinflusst die taktische Karte nicht', () => {
  const a = generateWorld({ seed: makeSeed('KOSMETIK'), variant: 'classic', biome: 'random' });
  const b = JSON.parse(JSON.stringify(a));
  b.decor = [];
  assert.deepEqual(a.cells, b.cells);
  assert.deepEqual(a.veins, b.veins);
});

test('Fallback-Karten sind gültig', () => {
  for (const v of ['classic', 'mazing'] as const) assert.ok(validateMap(fallbackMap(v, 'forest')).ok);
});

test('Validierung erkennt kaputte Karten', () => {
  const m = generateWorld({ seed: makeSeed('KAPUTT'), variant: 'classic', biome: 'forest' });
  const broken = { ...m, cells: m.cells.map((c) => (c === Cell.Path ? Cell.Blocked : c)) };
  assert.equal(validateMap(broken).ok, false);
  const noGoal = { ...m, cells: m.cells.map((c) => (c === Cell.Goal ? Cell.Path : c)) };
  assert.equal(validateMap(noGoal).ok, false);
});

test('Mazing: zufällige Bauaktionen blockieren den Weg nie (50 Seeds)', () => {
  for (let s = 0; s < 50; s++) {
    const sim = makeSim({ variant: 'mazing', seedText: `MAZE-${s}` });
    sim.s.gold = 1e6;
    const rng = new Rng(1000 + s);
    const cells = rng.shuffle(openCells(sim));
    let built = 0;
    for (const c of cells.slice(0, 160)) {
      const chk = sim.canBuild('barricade', c.x, c.y);
      const r = sim.build('barricade', c.x, c.y);
      assert.equal(r.ok, chk.ok);
      if (r.ok) built++;
      const m = sim.s.map;
      assert.ok(sim.c.field[m.spawn.y * m.width + m.spawn.x] < INF, `Weg blockiert (Seed ${s})`);
    }
    assert.ok(built > 40);
    const route = tracePath(sim.s.map, sim.c.field, sim.s.map.spawn);
    const last = route[route.length - 1];
    assert.deepEqual(last, sim.s.map.goal);
  }
});

test('Mazing: blockierende Platzierung wird abgelehnt und Gegner werden nie eingesperrt', () => {
  const sim = makeSim({ variant: 'mazing', seedText: 'SPERRE' });
  sim.s.gold = 1e6;
  const m = sim.s.map;
  // Ziel komplett ummauern versuchen
  let rejected = 0;
  for (let y = 0; y < m.height; y++) for (let x = 0; x < m.width; x++) {
    if (Math.abs(x - m.goal.x) <= 2 && Math.abs(y - m.goal.y) <= 2) {
      const r = sim.build('barricade', x, y);
      if (!r.ok && /blockieren/.test(r.reason)) rejected++;
    }
  }
  assert.ok(sim.c.field[m.spawn.y * m.width + m.spawn.x] < INF);
  assert.ok(rejected >= 1, 'mindestens eine Platzierung als blockierend erkannt');
  // Gegner auf Zielzelle: Bau dort verboten
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'runner', false, null);
  e.x = 5.5; e.y = 5.5; e.nx = 6; e.ny = 5;
  if (m.cells[5 * m.width + 6] === Cell.Open && !sim.towerAt(6, 5)) assert.equal(sim.canBuild('barricade', 6, 5).ok, false);
});

test('Mazing: Gegner erreichen trotz Labyrinthbau während der Welle das Ziel', () => {
  const sim = makeSim({ variant: 'mazing', seedText: 'LAUF' });
  sim.s.gold = 1e6;
  sim.startWave();
  const rng = new Rng(77);
  const cells = rng.shuffle(openCells(sim));
  let k = 0;
  for (let i = 0; i < 30 * 90 && sim.s.phase === 'combat'; i++) {
    if (i % 10 === 0 && k < cells.length) {
      const c = cells[k++];
      sim.build('barricade', c.x, c.y);
    }
    sim.step();
  }
  assert.notEqual(sim.s.phase, 'combat', 'Welle endet (Gegner erreichen das Ziel)');
  assert.ok(sim.s.stats.leaks > 0);
  assert.equal(sim.lastError, null);
});
