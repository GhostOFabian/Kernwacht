import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SIM } from '../src/config/balance.ts';
import { PARAGON_NODES } from '../src/config/paragon.ts';
import { countsTowardLimit, LANE_BARRICADE, TOWER_LIMIT, towerLimit } from '../src/config/scaling.ts';
import { TOWERS } from '../src/config/towers.ts';
import { Cell } from '../src/core/types.ts';
import { laneCharges } from '../src/sim/EconomySystem.ts';
import { slowFactor } from '../src/sim/ElementSystem.ts';
import { spawnsOf } from '../src/sim/WorldGenerator.ts';
import { addEnemy, makeSim } from './helpers.ts';

function pathCells(sim: ReturnType<typeof makeSim>) {
  const m = sim.s.map;
  const out: { x: number; y: number; d: number }[] = [];
  for (let y = 0; y < m.height; y++) for (let x = 0; x < m.width; x++) {
    if (m.cells[y * m.width + x] !== Cell.Path) continue;
    const d = Math.min(...spawnsOf(m).map((s) => Math.hypot(s.x - x, s.y - y)));
    out.push({ x, y, d });
  }
  return out;
}

test('3.8: Turmlimit startet bei 20, Goldgräber und Unterstützer frei, Bollwerk +2 Kernenergie', () => {
  assert.equal(TOWER_LIMIT.base, 20);
  assert.equal(towerLimit(0, true), 20);
  assert.equal(countsTowardLimit('goldmine'), false);
  assert.equal(countsTowardLimit('lanebarricade'), false);
  const core = PARAGON_NODES.find((n) => n.id === 'pg_core')!;
  assert.equal(core.per.coreLives, 2);
  assert.ok(core.maxRank * 2 >= 40);
});

test('Lane-Barrikade: nur auf der Lane, nicht vor dem Riss, höchstens 2 je 10 Minuten, verlangsamt Bodengegner', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'LANE' });
  const cells = pathCells(sim);
  const near = cells.find((c) => c.d < LANE_BARRICADE.minSpawnDist)!;
  const far = cells.filter((c) => c.d >= LANE_BARRICADE.minSpawnDist + 2 && Math.abs(c.x - sim.s.map.goal.x) + Math.abs(c.y - sim.s.map.goal.y) > 3);
  assert.ok(near && far.length >= 3);
  assert.match(sim.canBuild('lanebarricade', near.x, near.y).reason ?? '', /Riss/);
  // Auf einem freien Feld neben der Lane: nicht erlaubt
  const m = sim.s.map;
  let open = -1;
  for (let i = 0; i < m.cells.length && open < 0; i++) if (m.cells[i] === Cell.Open) open = i;
  assert.equal(sim.canBuild('lanebarricade', open % m.width, Math.floor(open / m.width)).ok, false);
  const g = sim.s.gold;
  assert.ok(sim.build('lanebarricade', far[0].x, far[0].y).ok);
  assert.ok(sim.build('lanebarricade', far[1].x, far[1].y).ok);
  assert.equal(sim.s.gold, g, 'kostenlos');
  assert.equal(laneCharges(sim).left, 0);
  assert.match(sim.canBuild('lanebarricade', far[2].x, far[2].y).reason ?? '', /nächste in/);
  sim.s.time += LANE_BARRICADE.window + 1;
  assert.equal(laneCharges(sim).left, 2, 'nach 10 Minuten wieder verfügbar');
  // Verlangsamung
  sim.startWave();
  sim.s.enemies = [];
  sim.s.spawnQueue = [];
  const e = addEnemy(sim, 'brute', far[0].x + 0.5, far[0].y + 0.5);
  e.hp = e.maxHp = 1e7;
  const f = addEnemy(sim, 'flyer', far[0].x + 0.5, far[0].y + 0.5);
  f.hp = f.maxHp = 1e7;
  for (let i = 0; i < 6; i++) { e.x = f.x = far[0].x + 0.5; e.y = f.y = far[0].y + 0.5; sim.step(); }
  assert.ok(slowFactor(e) >= 0.45, `Bodengegner verlangsamt: ${slowFactor(e)}`);
  assert.equal(slowFactor(f), 0, 'Flieger fliegen darüber');
  // Mazing und Quick: nicht verfügbar
  assert.equal(makeSim({ seedText: 'LANE' }).canBuild('lanebarricade', far[0].x, far[0].y).ok, false);
  assert.ok(TOWERS.lanebarricade.laneOnly);
  void SIM;
});
