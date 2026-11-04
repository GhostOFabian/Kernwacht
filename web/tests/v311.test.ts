import assert from 'node:assert/strict';
import { test } from 'node:test';
import { bossHpMul, waveHpMul } from '../src/config/balance.ts';
import { ENEMIES } from '../src/config/enemies.ts';
import { STARS, starMods } from '../src/config/goldsinks.ts';
import { runeSockets } from '../src/config/runes.ts';
import { CHALLENGE, challengeHpMul } from '../src/config/scaling.ts';
import { starCost } from '../src/sim/EconomySystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { planWave } from '../src/sim/WaveDirector.ts';

const eff = (id: keyof typeof ENEMIES) => { const e = ENEMIES[id]; return e.hp + (e.onDeathSpawn ? ENEMIES[e.onDeathSpawn.enemy].hp * e.onDeathSpawn.count : 0); };

test('3.11: Wachstum ab Welle 150 flacher – 150→500 etwa ×25 statt ×3000', () => {
  const r = waveHpMul(500, true) / waveHpMul(150, true);
  assert.ok(r > 10 && r < 40, `Faktor ${r}`);
  const per50 = waveHpMul(300, true) / waveHpMul(250, true);
  assert.ok(per50 < 1.8, `je 50 Wellen ×${per50}`);
  for (let w = 151; w <= 600; w++) assert.ok(waveHpMul(w, true) > waveHpMul(w - 1, true));
});

test('3.11: Bosse wachsen ab 150 wie die Wellen, Challenge-Bosse werden stärker', () => {
  const b = bossHpMul(400, true) / bossHpMul(150, true);
  const w = waveHpMul(400, true) / waveHpMul(150, true);
  assert.ok(Math.abs(b - w) < 1e-9);
  assert.equal(challengeHpMul(150), CHALLENGE.hpMul);
  assert.ok(Math.abs(challengeHpMul(250) - CHALLENGE.hpMul * 2) < 1e-9);
  assert.ok(challengeHpMul(500) > 15);
});

test('3.11: Späte Wellen ohne Ausreißer – Gruppen nach LP verteilt', () => {
  const sim = GameSimulation.create({ mode: 'heroEndless', seedText: 'SMOOTH', variant: 'classic', biome: 'forest', difficulty: 'normal', size: 'large',
    heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {} }, runId: 'v311' } });
  const v: number[] = [];
  for (let n = 150; n <= 300; n++) {
    const p = planWave(sim.s, n);
    v.push(p.groups.filter((g) => !g.boss && !g.elite).reduce((a, g) => a + eff(g.enemy) * g.count, 0));
  }
  const max = Math.max(...v);
  const min = Math.min(...v);
  assert.ok(max / min < 3, `Spannweite ${max}/${min}`);
});

test('3.11: Veredelung bis ★25 (ab ★11 halbe Wirkung, flacherer Preis), 4. Runensockel ab Welle 500', () => {
  assert.equal(STARS.max, 25);
  const dmg = (n: number) => starMods('ranger', n).find((m) => m.stat === 'damageMul')?.add ?? 0;
  assert.ok(Math.abs(dmg(10) - 0.8) < 1e-9);
  assert.ok(Math.abs(dmg(20) - 1.2) < 1e-9, 'ab ★11 halb');
  const t = (n: number) => ({ def: 'ranger', stars: n, level: 4, spec: 1 } as never);
  assert.ok(starCost(t(11)) / starCost(t(10)) < 1.3);
  assert.ok(starCost(t(9)) / starCost(t(8)) > 1.5);
  assert.equal(runeSockets('ranger', 499), 3);
  assert.equal(runeSockets('ranger', 500), 4);
  assert.equal(runeSockets('plasma', 500), 5);
});
