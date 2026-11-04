import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SIM } from '../src/config/balance.ts';
import { milestoneGold, milestoneParagon, xpToNext } from '../src/config/heroEndless.ts';
import { masteryRank } from '../src/config/mastery.ts';
import { paragonPrePoints } from '../src/config/paragon.ts';
import { affixCount, SCALING, worldTier } from '../src/config/scaling.ts';
import { commitHeroProgress } from '../src/app/progress.ts';
import { defaultSave } from '../src/save/SaveSystem.ts';
import { killEnemy, resistOf } from '../src/sim/DamageSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { planWave } from '../src/sim/WaveDirector.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';

const heSim = (seed = 'V30', meta: Record<string, unknown> = {}) => GameSimulation.create({
  mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
  heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, ...meta }, runId: `v30-${seed}` },
});
const steps = (sim: GameSimulation, sec: number) => { for (let i = 0, n = Math.round(sec / SIM.dt); i < n; i++) sim.step(); };

test('Weltstufen und Affixe: alle 50 Wellen eine Stufe, Affixe ab 100 (1→2→3), deterministisch, nur Endlos', () => {
  assert.equal(worldTier(49, true), 0);
  assert.equal(worldTier(137, true), 2);
  assert.equal(worldTier(137, false), 0);
  assert.deepEqual([99, 100, 174, 175, 250, 400].map((w) => affixCount(w, true)), [0, 1, 1, 2, 3, 3]);
  const sim = makeSim({ mode: 'endless', seedText: 'AFFIX' });
  const a = planWave(sim.s, 180);
  const b = planWave(sim.s, 180);
  assert.equal(a.tier, 3);
  assert.equal(a.affixes?.length, 2);
  assert.deepEqual(a.affixes, b.affixes);
  assert.ok(a.groups.some((g) => g.elite), 'ab Welle 100 in jeder Welle Elite');
  assert.equal(planWave(sim.s, 30).affixes, undefined);
  const quick = makeSim({ seedText: 'AFFIX' });
  assert.equal(planWave(quick.s, 30).tier, undefined);
});

test('Affixe wirken: Schild, Immunität, Panzerung, Weltstufen-Rüstung, Splitter, Störsender', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'AFFIX2' });
  sim.s.wave = 260;
  sim.s.currentWave = { ...planWave(sim.s, 260), affixes: ['shielded', 'immune', 'armored'], immune: 'fire', tier: 5 };
  const e = addEnemy(sim, 'brute');
  assert.ok(e.shield >= e.maxHp * 0.25 - 1e-6, 'Schild');
  assert.ok(resistOf(sim, e, 'fire') >= 0.85, 'Immunität');
  const plain = makeSim({ mode: 'endless', seedText: 'AFFIX3' });
  const p = addEnemy(plain, 'brute');
  assert.ok(resistOf(sim, e, 'physical') >= resistOf(plain, p, 'physical') + 0.2 + 0.15 - 1e-6, 'Panzerung + Weltstufe');
  // Splitter
  sim.s.currentWave = { ...sim.s.currentWave, affixes: ['splitter', 'jammer'] };
  const sp = addEnemy(sim, 'brute');
  const n0 = sim.s.enemies.filter((x) => x.alive).length;
  killEnemy(sim, sp, 0);
  const kids = sim.s.enemies.filter((x) => x.alive && x.splitChild);
  assert.equal(kids.length, 2);
  assert.equal(sim.s.enemies.filter((x) => x.alive).length, n0 - 1 + 2);
  killEnemy(sim, kids[0], 0);
  assert.equal(sim.s.enemies.filter((x) => x.alive && x.splitChild).length, 1, 'Kinder splitten nicht weiter');
  // Störsender: Elite betäubt Türme in der Nähe
  const t = buildNearPath(sim, 'ranger');
  const el = addEnemy(sim, 'brute', t.cx + 0.5, t.cy + 1.2);
  el.elite = true;
  el.jamCd = 0.01;
  el.speed = 0;
  steps(sim, 0.2);
  assert.ok(t.stunned > 0, 'Turm betäubt');
});

test('Nachrückende Wellen ab Welle 120 (Helden-Endlos) und doppelter Boss-Durchbruch ab Welle 100', () => {
  const sim = heSim('OVERRUN');
  sim.s.wave = 129;
  sim.startWave();
  assert.equal(sim.s.wave, 130);
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) { e.hp = e.maxHp = 1e9; e.speed = 0; }
  if (!sim.s.enemies.some((e) => e.alive)) { const x = addEnemy(sim, 'brute'); x.hp = x.maxHp = 1e9; x.speed = 0; }
  for (const e of sim.s.enemies) if (e.boss) e.alive = false;
  steps(sim, SCALING.overrunAfter + 1);
  assert.ok(sim.s.wave >= 131, `nächste Welle nachgerückt (Welle ${sim.s.wave})`);
  const b = makeSim({ mode: 'endless', seedText: 'BOSSLEAK' });
  b.s.wave = 120;
  const bossE = addEnemy(b, 'boss');
  const q = makeSim({ mode: 'endless', seedText: 'BOSSLEAK' });
  q.s.wave = 50;
  const bossQ = addEnemy(q, 'boss');
  assert.ok(bossE.leak >= bossQ.leak * 2 - 1e-6 || bossE.leak > 1);
});

test('Fortschritt: flachere EP-Kurve, Vorab-Paragonpunkte, Meilensteine, Turm-Meisterschaft', () => {
  let total = 0;
  for (let l = 1; l < 99; l++) total += xpToNext(l);
  assert.ok(total < 400000, `EP bis 99: ${total}`);
  assert.deepEqual([49, 50, 54, 55, 95, 99].map(paragonPrePoints), [0, 1, 1, 2, 10, 10]);
  assert.equal(milestoneParagon(99), 0);
  assert.equal(milestoneParagon(137), 2);
  assert.equal(milestoneParagon(300), 15);
  // Meilenstein-Startgold und -Paragon im Lauf
  const base = heSim('MS0');
  const ms = heSim('MS0', { bestWave: 160 });
  assert.ok(ms.s.gold - base.s.gold >= milestoneGold(160), 'Meilenstein-Startgold');
  assert.equal(ms.s.he!.paragonBonus, 4);
  // Turm-Meisterschaft
  assert.deepEqual([0, 49, 50, 199, 200, 1e9].map(masteryRank), [0, 0, 1, 1, 2, 20]);
  const sim = heSim('MASTERY', { mastery: { ranger: 190 } });
  sim.s.wave = 1;
  const t = buildNearPath(sim, 'ranger');
  const d0 = sim.stats(t).damage;
  for (let i = 0; i < 12; i++) killEnemy(sim, addEnemy(sim, 'runner'), t.uid);
  sim.refreshCaches();
  assert.equal(masteryRank(sim.s.he!.mastery!.ranger!), 2);
  assert.ok(sim.stats(t).damage > d0, 'Rang 2 gibt mehr Schaden');
  const save = defaultSave();
  commitHeroProgress(save, sim.s);
  assert.equal(save.progress.heroMeta.towerMastery.ranger, 202);
  commitHeroProgress(save, sim.s);
  assert.equal(save.progress.heroMeta.towerMastery.ranger, 202, 'keine Doppelzählung');
});
