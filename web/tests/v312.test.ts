import assert from 'node:assert/strict';
import { test } from 'node:test';
import { FLYER } from '../src/config/scaling.ts';
import { addStatus, slowFactor } from '../src/sim/ElementSystem.ts';
import { makeEnemy } from '../src/sim/EnemySystem.ts';
import { planWave } from '../src/sim/WaveDirector.ts';
import { addEnemy, makeSim } from './helpers.ts';

test('3.12: Flieger höchstens 2 Felder/s, auch mit Weltstufe und „Flink“', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'FLY12' });
  for (const w of [150, 250, 300, 450]) {
    sim.s.wave = w;
    const p = planWave(sim.s, w);
    sim.s.currentWave = { ...p, speedMul: 1.6 };
    for (const id of ['flyer', 'griffin'] as const) {
      const e = makeEnemy(sim, id, false, null);
      assert.ok(e.speed <= FLYER.maxSpeed + 1e-9, `${id} W${w}: ${e.speed}`);
    }
    const g = makeEnemy(sim, 'runner', false, null);
    assert.ok(g.speed > 2, 'Bodengegner unverändert schnell');
  }
});

test('3.12: Verlangsamung wirkt auf Flieger 30 % stärker', () => {
  const sim = makeSim({ seedText: 'FLY12B' });
  const f = addEnemy(sim, 'flyer');
  const g = addEnemy(sim, 'runner');
  for (const e of [f, g]) { const s = addStatus(e, 'chill', 3, 0.3, 0, 1); s.flag = 60; }
  assert.ok(Math.abs(slowFactor(f) - slowFactor(g) * FLYER.slowMul) < 1e-9);
});
