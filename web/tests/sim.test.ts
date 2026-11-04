import assert from 'node:assert/strict';
import { test } from 'node:test';
import { bossHpMul, ENDLESS, SIM, waveBudget, waveHpMul } from '../src/config/balance.ts';
import { MODIFIERS } from '../src/config/modifiers.ts';
import { Bot, BOT_PROFILES } from '../src/sim/Bot.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { eligibleModifiers, rollOffer } from '../src/sim/RogueliteSystem.ts';
import { endlessBossFor } from '../src/sim/setup.ts';
import { planWave } from '../src/sim/WaveDirector.ts';
import { buildNearPath, makeSim } from './helpers.ts';

test('Pause hält die Simulation an', () => {
  const sim = makeSim();
  sim.startWave();
  sim.setPaused(true);
  const t = sim.s.tick;
  assert.equal(sim.advance(1), 0);
  assert.equal(sim.s.tick, t);
  sim.setPaused(false);
  assert.ok(sim.advance(0.2) > 0);
});

test('Spielgeschwindigkeit erhöht Takte, nicht die Schrittweite', () => {
  const a = makeSim();
  const b = makeSim();
  a.startWave();
  b.startWave();
  b.setSpeed(3);
  for (let i = 0; i < 20; i++) {
    a.advance(0.05);
    b.advance(0.05);
  }
  assert.equal(a.s.tick, 30);
  assert.equal(b.s.tick, 90);
  b.setSpeed(7);
  assert.equal(b.s.speed, 1, 'ungültige Geschwindigkeit fällt auf 1 zurück');
});

test('Determinismus: unterschiedliche Frame-Längen ergeben identische Zustände', () => {
  const run = (frame: number): string => {
    const sim = makeSim({ seedText: 'DETERM' });
    buildNearPath(sim, 'storm');
    buildNearPath(sim, 'ember', 3);
    sim.startWave();
    while (sim.s.tick < 30 * 25) sim.advance(frame);
    // genau auf denselben Tick bringen
    return JSON.stringify({ ...sim.s, tick: 0, speed: 0 });
  };
  // Takte sind identisch, wenn wir gleich viele Schritte ausführen
  const a = makeSim({ seedText: 'DETERM' });
  const b = makeSim({ seedText: 'DETERM' });
  for (const s of [a, b]) {
    buildNearPath(s, 'storm');
    buildNearPath(s, 'ember', 3);
    s.startWave();
  }
  let ta = 0;
  while (ta < 750) { a.advance(1 / 60); ta = a.s.tick; }
  while (b.s.tick < a.s.tick) b.advance(0.1);
  while (b.s.tick > a.s.tick) a.step();
  assert.equal(JSON.stringify(a.s), JSON.stringify(b.s));
  void run;
});

test('Tabwechsel: große Zeitsprünge werden begrenzt (keine Spirale)', () => {
  const sim = makeSim();
  sim.startWave();
  const ticks = sim.advance(30);
  assert.ok(ticks <= SIM.maxTicksPerFrame);
});

test('Modifikatorangebot: drei verschiedene, vereinbare Optionen', () => {
  for (let i = 0; i < 100; i++) {
    const sim = makeSim({ seedText: `MOD-${i}` });
    if (i % 2) sim.s.modifiers.push('glassCannon');
    const offer = rollOffer(sim);
    assert.equal(offer.length, 3);
    assert.equal(new Set(offer).size, 3);
    for (const id of offer) {
      assert.ok(!sim.s.modifiers.includes(id));
      for (const o of offer) assert.ok(!(MODIFIERS[id].exclusive ?? []).includes(o));
    }
    if (i % 2) assert.ok(!offer.includes('bulwark'));
    assert.ok(offer.some((id) => !MODIFIERS[id].requiresTower), 'mindestens ein sofort nutzbares Angebot');
  }
});

test('Modifikatoren wirken auf die Simulation', () => {
  const sim = makeSim();
  const t = buildNearPath(sim, 'ranger');
  const dmg = sim.stats(t).damage;
  const range = sim.stats(t).range;
  sim.s.phase = 'choice';
  sim.s.modOffer = ['glassCannon', 'farSight', 'interest'];
  const lives = sim.s.lives;
  assert.ok(sim.chooseModifier('glassCannon').ok);
  assert.equal(sim.s.lives, lives - 5);
  assert.ok(Math.abs(sim.stats(t).damage - dmg * 1.25) < 1e-6);
  sim.s.phase = 'choice';
  sim.s.modOffer = ['farSight'];
  sim.chooseModifier('farSight');
  assert.ok(sim.stats(t).range > range);
  sim.s.phase = 'choice';
  sim.s.modOffer = ['rapidFire'];
  assert.equal(sim.chooseModifier('bounty').ok, false, 'nur angebotene Modifikatoren');
  const g = sim.s.gold;
  sim.chooseModifier(null);
  assert.ok(sim.s.gold > g, 'Überspringen gibt Gold');
  assert.ok(!eligibleModifiers(sim.s).includes('bulwark'), 'Bollwerk unvereinbar mit Glaskanone');
});

test('Endlosmodus: bis Welle 60 kontrolliert, danach sanft exponentiell (3.0)', () => {
  for (let w = 2; w < 400; w++) assert.ok(waveHpMul(w, true) >= waveHpMul(w - 1, true));
  const r0 = waveHpMul(60, true) / waveHpMul(30, true);
  assert.ok(r0 < 4, `frühes Wachstum zu stark: ${r0}`);
  // Ab Welle 60: zusätzlicher Faktor 1,02 je Welle
  const lin = (w: number) => { const x = w - 1; const q = Math.min(x, 30); const t = Math.max(0, x - 30); return 1 + 0.12 * x + 0.007 * q * q + 0.45 * t + 2 * Math.sqrt(t); };
  assert.ok(Math.abs(waveHpMul(140, true) / lin(140) - Math.pow(1.02, 80)) < 1e-6);
  // 3.11: ab Welle 150 nur noch ×1,006 je Welle
  assert.ok(Math.abs(waveHpMul(250, true) / lin(250) - Math.pow(1.02, 90) * Math.pow(1.006, 100)) < 1e-6);
  assert.ok(waveHpMul(200, true) / waveHpMul(100, true) > 3, 'späte Wellen werden deutlich härter');
  assert.ok(waveBudget(100) < 3000);
  assert.ok(bossHpMul(100, true) < 30, "Boss-LP W100 (3.2: ×1,5 allgemein)");
  assert.ok(bossHpMul(100, true) > 15);
  const sim = makeSim({ mode: 'endless' });
  assert.equal(sim.s.rules.endless, true);
  for (let w = 1; w <= 40; w++) {
    const boss = endlessBossFor(sim.s.seed.hash, w);
    assert.equal(boss !== null, w % ENDLESS.bossEvery === 0);
    const plan = planWave(sim.s, w);
    const count = plan.groups.reduce((n, g) => n + g.count, 0);
    assert.ok(count <= 70, `Welle ${w}: ${count} Gegner`);
  }
});

test('Wellenplanung ist deterministisch und unabhängig vom Kampfverlauf', () => {
  const a = makeSim({ seedText: 'WELLE' });
  const b = makeSim({ seedText: 'WELLE' });
  buildNearPath(b, 'storm');
  for (let i = 0; i < 100; i++) b.rand();
  assert.deepEqual(planWave(a.s, 7), planWave(b.s, 7));
});

test('Tagesherausforderung: gleicher Tag → gleiche Regeln und Karte', () => {
  const s1 = makeSim({ mode: 'daily', seedText: 'DAILY-20261015', variant: 'classic' });
  const s2 = makeSim({ mode: 'daily', seedText: 'DAILY-20261015', variant: 'mazing' });
  assert.deepEqual(s1.s.rules, s2.s.rules);
  assert.deepEqual(s1.s.map, s2.s.map, 'Variante wird aus dem Seed bestimmt');
  const s3 = makeSim({ mode: 'daily', seedText: 'DAILY-20261016' });
  assert.notDeepEqual(s1.s.map.cells, s3.s.map.cells);
});

test('Integration: Partie ohne Türme endet in einer Niederlage', () => {
  const sim = makeSim({ seedText: 'LEER' });
  for (let i = 0; i < 30 * 60 * 30 && !sim.isOver(); i++) {
    if (sim.s.phase === 'build') sim.startWave();
    if (sim.s.phase === 'choice') sim.chooseModifier(null);
    sim.step();
  }
  assert.equal(sim.s.phase, 'defeat');
  assert.equal(sim.lastError, null);
});

test('Integration: vollständige Partie vom Start bis zum Sieg (Bot, Entspannt)', () => {
  let wins = 0;
  for (const p of BOT_PROFILES) {
    const sim = GameSimulation.create({ mode: 'quick', seedText: `SIEG-${p.name}`, variant: 'classic', biome: 'random', difficulty: 'easy' });
    new Bot(sim, p).play();
    assert.ok(sim.isOver());
    assert.equal(sim.lastError, null);
    if (sim.s.phase === 'victory') wins++;
  }
  assert.ok(wins >= 1, 'mindestens ein Bot-Profil gewinnt auf Entspannt');
});
