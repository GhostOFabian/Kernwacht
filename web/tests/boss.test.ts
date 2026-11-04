import assert from 'node:assert/strict';
import { test } from 'node:test';
import { BOSSES } from '../src/config/enemies.ts';
import { dealDamage } from '../src/sim/DamageSystem.ts';
import { interruptEnemy, spawnEnemy } from '../src/sim/EnemySystem.ts';
import { makeSim } from './helpers.ts';

function bossSim(id: keyof typeof BOSSES) {
  const sim = makeSim({ seedText: `BOSS-${id}` });
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'boss', false, id);
  return { sim, e };
}

test('Alle Bosse besitzen mindestens zwei Phasen', () => {
  for (const b of Object.values(BOSSES)) assert.ok(b.phases.length >= 2, b.id);
});

test('Aschenkönigin wechselt über LP-Schwellen in Phase 2 und 3', () => {
  const { sim, e } = bossSim('ashQueen');
  assert.equal(sim.s.boss?.phase, 0);
  e.hp = e.maxHp * 0.55;
  sim.step();
  assert.equal(e.phase, 1);
  assert.ok(e.abilities.some((a) => a.id === 'fireZones'));
  e.hp = e.maxHp * 0.2;
  sim.step();
  assert.equal(e.phase, 2);
  assert.ok(e.abilityMul < 1, 'Aschenwut beschleunigt Fähigkeiten');
});

test('Nullhüter: Schildbruch löst Phase 2 aus, physischer Schaden wirkt kaum gegen den Schild', () => {
  const { sim, e } = bossSim('nullWarden');
  assert.ok(e.shield > 0);
  const s0 = e.shield;
  dealDamage(sim, e, 100, 'physical', 0);
  const phys = s0 - e.shield;
  const s1 = e.shield;
  dealDamage(sim, e, 100, 'arcane', 0);
  const arc = s1 - e.shield;
  assert.ok(arc > phys * 4, 'Arkan bricht den Schild deutlich besser');
  dealDamage(sim, e, 1e7, 'arcane', 0);
  assert.equal(e.phase, 1);
  assert.ok(e.abilities.some((a) => a.id === 'nullPulse'));
});

test('Risskoloss: angekündigter Sprung ist durch Standfestigkeitsbruch unterbrechbar', () => {
  const { sim, e } = bossSim('riftColossus');
  let warned = false;
  for (let i = 0; i < 30 * 12 && !warned; i++) {
    sim.step();
    warned = e.abilities.some((a) => a.windup > 0);
  }
  assert.ok(warned, 'Fähigkeit wird angekündigt (Windup)');
  assert.ok(sim.s.telegraphs.length > 0 || sim.s.zones.some((z) => z.armed > 0), 'sichtbare Ankündigung');
  interruptEnemy(sim, e, 1e6);
  assert.ok(e.abilities.every((a) => a.windup === 0), 'Windup abgebrochen');
});

test('Bosskill zählt und Sieg wird nach der letzten Welle erkannt', () => {
  const sim = makeSim({ seedText: 'SIEG' });
  sim.s.rules.waves = 1;
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'boss', false, 'riftColossus');
  dealDamage(sim, e, 1e9, 'arcane', 0);
  assert.ok(sim.s.stats.bossesKilled.includes('riftColossus'));
  for (let i = 0; i < 5; i++) sim.step();
  assert.equal(sim.s.phase, 'victory');
  assert.ok(sim.isOver());
});

test('Niederlage, wenn die Kernenergie 0 erreicht', () => {
  const sim = makeSim({ seedText: 'NIEDERLAGE' });
  sim.s.lives = 1;
  sim.startWave();
  for (let i = 0; i < 30 * 120 && !sim.isOver(); i++) sim.step();
  assert.equal(sim.s.phase, 'defeat');
  assert.ok(sim.s.lives <= 0);
  const g = sim.s.gold;
  assert.equal(sim.build('ranger', 0, 0).ok, false, 'nach Spielende keine Befehle');
  assert.equal(sim.s.gold, g);
});
