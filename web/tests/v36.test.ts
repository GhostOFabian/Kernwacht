import assert from 'node:assert/strict';
import { test } from 'node:test';
import { STARS, starMods } from '../src/config/goldsinks.ts';
import { fullyBuilt, starCost } from '../src/sim/EconomySystem.ts';
import { buildNearPath, makeSim } from './helpers.ts';

const endless = (seed: string) => {
  const sim = makeSim({ mode: 'endless', seedText: seed });
  sim.s.rules.towerLimitOff = true;
  sim.s.gold = 1e7;
  return sim;
};
const maxOut = (sim: ReturnType<typeof makeSim>, uid: number) => {
  const t = sim.c.towerByUid.get(uid)!;
  while (t.level < 4) assert.ok(sim.upgrade(uid).ok);
  assert.ok(sim.specialize(uid, 1).ok);
  return t;
};

test('Veredelung: erst voll ausgebaut, dann ★1–★10 mit steigenden Preisen und wichtigeren Werten', () => {
  const sim = endless('STAR');
  const t = buildNearPath(sim, 'ranger');
  assert.equal(sim.star(t.uid).ok, false, 'Stufe 1 noch nicht veredelbar');
  maxOut(sim, t.uid);
  assert.ok(fullyBuilt(t));
  const dmg0 = sim.stats(t).damage;
  const crit0 = sim.stats(t).critChance;
  const c1 = starCost(t);
  const g = sim.s.gold;
  assert.ok(sim.star(t.uid).ok);
  assert.equal(t.stars, 1);
  assert.equal(g - sim.s.gold, c1);
  assert.ok(starCost(t) > c1, 'nächster Stern teurer');
  assert.ok(sim.stats(t).damage > dmg0 * 1.07, 'mehr Schaden');
  assert.ok(sim.stats(t).critChance > crit0, 'Schlüsselwert Krit steigt');
  for (let i = 1; i < STARS.max; i++) assert.ok(sim.star(t.uid).ok);
  assert.equal(t.stars, STARS.max);
  assert.equal(sim.star(t.uid).ok, false, 'Höchste Veredelung');
  // Quick-Modus: keine Veredelung
  const q = makeSim({ seedText: 'STAR' });
  const tq = buildNearPath(q, 'ranger');
  q.s.gold = 1e7;
  maxOut(q, tq.uid);
  assert.equal(q.star(tq.uid).ok, false);
});

test('Veredelung: Goldgräber mehr Einkommen, Frost mehr Verlangsamung; „Alle ▲“ veredelt nach dem Ausbau', () => {
  assert.ok(starMods('goldmine', 3).some((m) => m.stat === 'income' && Math.abs((m.mul ?? 0) - 1.08 ** 3) < 1e-9));
  assert.ok(starMods('frost', 2).some((m) => m.stat === 'chillMax' && Math.abs((m.add ?? 0) - 0.03) < 1e-9));
  const sim = endless('STARALL');
  const a = buildNearPath(sim, 'ranger');
  const b = buildNearPath(sim, 'frost', 2);
  maxOut(sim, a.uid);
  maxOut(sim, b.uid);
  sim.s.gold = starCost(a) + starCost(b) + 10;
  const r = sim.upgradeAll();
  assert.ok(r.ok);
  assert.equal(a.stars, 1);
  assert.equal(b.stars, 1);
  sim.s.gold = 1e7;
  assert.ok(sim.starAll([a.uid]).ok);
  assert.ok((a.stars ?? 0) > 1 && b.stars === 1, 'nur die angegebenen Türme');
});

test('3.8: Bauplatzkauf, Kernausbau und Kernreparatur sind entfernt', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'GONE' }) as unknown as Record<string, unknown>;
  for (const k of ['buySlot', 'coreUpgrade', 'repairCore']) assert.equal(typeof sim[k], 'undefined', k);
});
