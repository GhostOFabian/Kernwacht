import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SIM } from '../src/config/balance.ts';
import { ENEMIES } from '../src/config/enemies.ts';
import { FUSIONS, TOWERS } from '../src/config/towers.ts';
import { slowFactor } from '../src/sim/ElementSystem.ts';
import { pickTargets } from '../src/sim/TowerSystem.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';

const steps = (sim: ReturnType<typeof makeSim>, sec: number, pin?: () => void) => { for (let i = 0, n = Math.round(sec / SIM.dt); i < n; i++) { pin?.(); sim.step(); } };

test('Flakbatterie: nur Luftziele, verlangsamt Flieger, ignoriert Bodengegner', () => {
  const d = TOWERS.skyguard;
  assert.equal(d.base.hitsGround, 0);
  assert.equal(d.base.hitsAir, 1);
  const sim = makeSim({ seedText: 'FLAK' });
  const t = buildNearPath(sim, 'skyguard');
  const g = addEnemy(sim, 'brute', t.cx + 0.5, t.cy + 1.2);
  const a = addEnemy(sim, 'flyer', t.cx + 0.5, t.cy + 1.4);
  g.hp = g.maxHp = a.hp = a.maxHp = 1e6;
  assert.deepEqual(pickTargets(sim, t, sim.stats(t), 4).map((e) => e.uid), [a.uid], 'nur der Flieger ist ein Ziel');
  sim.s.phase = 'combat';
  steps(sim, 3, () => { g.x = a.x = t.cx + 0.5; g.y = t.cy + 1.2; a.y = t.cy + 1.4; });
  assert.equal(g.hp, 1e6, 'Bodengegner unberührt');
  assert.ok(a.hp < 1e6, 'Flieger getroffen');
  assert.ok(slowFactor(a) > 0.2, `Flieger verlangsamt: ${slowFactor(a)}`);
});

test('Himmelswacht: Fusion aus Flakbatterie und Frostwächter, reine Flugabwehr mit ×4 gegen Flieger', () => {
  const f = FUSIONS.find((x) => x.id === 'skywatch');
  assert.ok(f && f.a === 'skyguard' && f.b === 'frost');
  const d = TOWERS.skywatch;
  assert.ok(d.legendary);
  assert.equal(d.base.hitsGround, 0);
  assert.equal(d.base.airDmgMul, 4, '3.8: ×4 gegen Flieger');
  assert.ok((d.base.chillMax ?? 0) >= 0.7);
});

test('Flieger langsamer und etwas schwächer (3.5)', () => {
  assert.ok(ENEMIES.flyer.speed <= 1.5);
  assert.ok(ENEMIES.griffin.speed <= 1.85);
  assert.ok((ENEMIES.griffin.evasion ?? 0) <= 0.12);
  assert.ok(ENEMIES.flyer.hp < 64 && ENEMIES.griffin.hp < 125);
});
