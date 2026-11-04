import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHALLENGE, isChallengeWave } from '../src/config/scaling.ts';
import { TOWERS } from '../src/config/towers.ts';
import { makeChallenge, updateChallenge } from '../src/sim/ChallengeSystem.ts';
import { killEnemy, resistOf } from '../src/sim/DamageSystem.ts';
import { PRESPEC, prespecAvailable, prespecCost, towerCost } from '../src/sim/EconomySystem.ts';
import { addStatus } from '../src/sim/ElementSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { planWave } from '../src/sim/WaveDirector.ts';
import { addEnemy, makeSim, openCells } from './helpers.ts';

const heSim = (seed = 'V31') => GameSimulation.create({
  mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
  heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, ...{} }, runId: `v31-${seed}` },
});

test('Challenge-Wellen: alle 50 Wellen bis 250, danach alle 25 – nur Endlos', () => {
  const hits = [];
  for (let w = 1; w <= 350; w++) if (isChallengeWave(w, true)) hits.push(w);
  assert.deepEqual(hits, [50, 100, 150, 200, 250, 275, 300, 325, 350]);
  assert.equal(isChallengeWave(50, false), false);
  const sim = makeSim({ mode: 'endless', seedText: 'CH' });
  for (const w of [50, 100, 275]) {
    const plan = planWave(sim.s, w);
    assert.ok(plan.groups.some((g) => g.challenge && g.boss), `Welle ${w} hat Challenge-Boss`);
  }
  for (const w of [75, 260]) assert.ok(!planWave(sim.s, w).groups.some((g) => g.challenge), `Welle ${w} ohne Challenge`);
  assert.ok(!planWave(makeSim({ seedText: 'CH' }).s, 10).groups.some((g) => g.challenge));
});

test('Challenge-Boss: mehr LP, Resistenz, kürzere Kontrolle, Schild, Begleiter, Raserei, teurer Durchbruch, große Belohnung', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'CH2' });
  sim.s.wave = 100;
  const plain = addEnemy(sim, 'brute');
  const e = addEnemy(sim, 'brute');
  const hp0 = e.maxHp;
  const res0 = resistOf(sim, e, 'fire');
  const rew0 = e.reward;
  makeChallenge(sim, e);
  assert.equal(e.challenge, true);
  assert.ok(Math.abs(e.maxHp - hp0 * CHALLENGE.hpMul) < 1e-6);
  assert.ok(resistOf(sim, e, 'fire') > res0);
  assert.ok(e.leak >= CHALLENGE.leakMin);
  assert.ok(e.reward >= rew0 * CHALLENGE.rewardMul - 1e-6);
  const st = addStatus(e, 'stun', 2, 1, 0);
  const st2 = addStatus(plain, 'stun', 2, 1, 0);
  assert.ok(st.t < st2.t, 'Kontrolleffekte verkürzt');
  e.statuses = [];
  const n0 = sim.s.enemies.length;
  updateChallenge(sim, e, CHALLENGE.shieldEvery);
  assert.equal(sim.s.enemies.length, n0 + CHALLENGE.summonCount, 'Begleiter gerufen');
  assert.ok(e.shield > 0, 'Schildschub');
  assert.ok(e.shield <= e.maxHp * CHALLENGE.shieldMax + 1e-6);
  const sp = e.speed;
  sim.s.time = e.spawnedAt + CHALLENGE.enrageAfter + 1;
  updateChallenge(sim, e, 0.01);
  assert.equal(e.chEnraged, true);
  assert.ok(Math.abs(e.speed - sp * CHALLENGE.enrageSpeed) < 1e-6);
  const g = sim.s.gold;
  killEnemy(sim, e, 0);
  assert.ok(sim.s.gold - g >= rew0 * 4, 'hohes Kopfgeld');
});

test('Challenge-Boss erscheint im Spiel in Welle 50', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'CH3' });
  sim.s.wave = 49;
  sim.startWave();
  for (let i = 0; i < 30 * 40 && !sim.s.enemies.some((x) => x.challenge); i++) sim.step();
  assert.equal(sim.s.wave, 50);
  assert.ok(sim.s.enemies.some((x) => x.challenge && x.boss), 'Challenge-Boss gespawnt');
});

test('Spezialisierter Turmkauf ab Welle 250: teurer, direkt Höchststufe mit Spezialisierung', () => {
  const sim = heSim('PRE');
  sim.s.gold = 1e6;
  const cell = openCells(sim)[0];
  sim.s.wave = PRESPEC.from - 1;
  assert.equal(prespecAvailable(sim, 'ranger'), false);
  assert.equal(sim.canBuild('ranger', cell.x, cell.y, 1).ok, false);
  sim.s.wave = PRESPEC.from;
  assert.equal(prespecAvailable(sim, 'ranger'), true);
  const d = TOWERS.ranger;
  const full = towerCost(sim, 'ranger') + d.upgrades.reduce((a, u) => a + u.cost, 0) + d.specs[0].cost;
  const cost = prespecCost(sim, 'ranger', 1);
  assert.equal(cost, Math.round(full * PRESPEC.premium));
  assert.ok(cost > full, 'teurer als einzeln ausbauen');
  const g = sim.s.gold;
  assert.ok(sim.build('ranger', cell.x, cell.y, 1).ok);
  const t = sim.towerAt(cell.x, cell.y)!;
  assert.equal(t.level, 4);
  assert.equal(t.spec, 1);
  assert.equal(t.invested, cost);
  assert.equal(g - sim.s.gold, cost);
  assert.equal(sim.specialize(t.uid, 2).ok, false, 'bereits spezialisiert');
  assert.ok(sim.stats(t).multishot >= 0);
  // Normale Türme bleiben normal kaufbar; Goldgräber hat keine Spezialisierung im Kaufmenü
  const c2 = openCells(sim).find((c) => sim.canBuild('frost', c.x, c.y).ok)!;
  assert.ok(sim.build('frost', c2.x, c2.y).ok);
  assert.equal(sim.towerAt(c2.x, c2.y)!.level, 1);
  sim.s.gold = 0;
  const c3 = openCells(sim).find((c) => sim.canBuild('ranger', c.x, c.y).reason !== 'Hier kann nicht gebaut werden')!;
  assert.equal(sim.canBuild('ranger', c3.x, c3.y, 2).ok, false);
});
