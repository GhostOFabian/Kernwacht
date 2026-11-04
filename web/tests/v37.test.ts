import assert from 'node:assert/strict';
import { test } from 'node:test';
import { countsTowardLimit } from '../src/config/scaling.ts';
import { countedTowers, currentTowerLimit } from '../src/sim/EconomySystem.ts';
import { makeSim, openCells } from './helpers.ts';

test('Unterstützungstürme (Leitpylon, Totemschrein) zählen nicht zum Turmlimit', () => {
  assert.equal(countsTowardLimit('pylon'), false);
  assert.equal(countsTowardLimit('totem'), false);
  assert.equal(countsTowardLimit('ranger'), true);
  const sim = makeSim({ mode: 'endless', seedText: 'UTIL' });
  sim.s.gold = 1e7;
  const lim = currentTowerLimit(sim);
  let built = 0;
  for (const c of openCells(sim)) { if (built >= lim) break; if (sim.build('ranger', c.x, c.y).ok) built++; }
  assert.equal(countedTowers(sim), lim, 'Limit voll');
  const free = openCells(sim).filter((c) => sim.canBuild('pylon', c.x, c.y).ok);
  assert.ok(free.length >= 2);
  assert.ok(sim.build('pylon', free[0].x, free[0].y).ok, 'Pylon trotz vollem Limit');
  const f2 = openCells(sim).find((c) => sim.canBuild('totem', c.x, c.y).ok)!;
  assert.ok(sim.build('totem', f2.x, f2.y).ok, 'Totem trotz vollem Limit');
  assert.equal(countedTowers(sim), lim);
  const f3 = openCells(sim).find((c) => sim.canBuild('frost', c.x, c.y).reason?.startsWith('Turmlimit'));
  assert.ok(f3, 'normaler Turm weiterhin gesperrt');
});
