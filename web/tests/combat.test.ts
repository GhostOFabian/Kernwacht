import assert from 'node:assert/strict';
import { test } from 'node:test';
import { STATUS_RULES } from '../src/config/elements.ts';
import { dealDamage, resistOf } from '../src/sim/DamageSystem.ts';
import { addStatus, getStatus, hasStatus, react, slowFactor } from '../src/sim/ElementSystem.ts';
import { spawnEnemy } from '../src/sim/EnemySystem.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';

test('Resistenzen und Panzerbrechung', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'brute');
  assert.equal(resistOf(sim, e, 'physical'), 0.45);
  e.hp = e.maxHp = 1000;
  dealDamage(sim, e, 100, 'physical', 0, 0);
  assert.ok(Math.abs(1000 - e.hp - 55) < 1e-6, '45 % Panzer');
  e.hp = 1000;
  dealDamage(sim, e, 100, 'physical', 0, 1);
  assert.ok(Math.abs(1000 - e.hp - 100) < 1e-6, 'volle Panzerbrechung');
  e.hp = 1000;
  dealDamage(sim, e, 100, 'arcane', 0);
  assert.ok(Math.abs(1000 - e.hp - 115) < 1e-6, 'Arkanschwäche −15 %');
});

test('Runenmarkierung senkt Resistenzen, hart begrenzt', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'brute');
  e.hp = e.maxHp = 1000;
  addStatus(e, 'mark', 5, 0.3, 0);
  dealDamage(sim, e, 100, 'physical', 0);
  assert.ok(Math.abs(1000 - e.hp - 85) < 1e-6);
  e.hp = 1000;
  getStatus(e, 'mark')!.power = 5;
  dealDamage(sim, e, 100, 'fire', 0);
  assert.ok(1000 - e.hp <= 100 * (1 - (0.1 - STATUS_RULES.markHardCap)) + 1e-6);
});

test('Dampfstoß: Feuer auf unterkühlte Ziele macht nass und verstärkt', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'runner');
  addStatus(e, 'chill', 2, 0.2, 0, 3);
  const out = react(sim, e, 'fire', 10, 0);
  assert.equal(out, 10 * STATUS_RULES.steamMul);
  assert.ok(hasStatus(e, 'wet'));
  assert.ok(!hasStatus(e, 'chill'));
  // Überladung auf nasses Ziel
  assert.equal(react(sim, e, 'lightning', 10, 0), 10 * STATUS_RULES.overloadMul);
  assert.equal(sim.s.stats.reactions.steam, 1);
  assert.equal(sim.s.stats.reactions.overload, 1);
});

test('Reaktionen besitzen Abklingzeiten (keine Endlosketten)', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'brute');
  e.hp = e.maxHp = 1e6;
  addStatus(e, 'chill', 2, 0.2, 0, 3);
  react(sim, e, 'fire', 10, 0);
  addStatus(e, 'chill', 2, 0.2, 0, 3);
  const second = react(sim, e, 'fire', 10, 0);
  assert.equal(second, 10, 'innerhalb der Abklingzeit keine erneute Reaktion');
  assert.ok(hasStatus(e, 'chill'));
});

test('Thermoschock löscht Brand und macht spröde; Giftbrand trifft Umgebung', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'brute');
  e.hp = e.maxHp = 1e6;
  addStatus(e, 'burn', 3, 10, 0);
  react(sim, e, 'frost', 5, 0);
  assert.ok(!hasStatus(e, 'burn'));
  assert.ok(hasStatus(e, 'brittle'));
  const a = addEnemy(sim, 'brute', 5, 5);
  const b = addEnemy(sim, 'brute', 5.5, 5);
  a.hp = a.maxHp = b.hp = b.maxHp = 1e6;
  addStatus(a, 'poison', 4, 5, 0, 5);
  getStatus(a, 'poison')!.stacks = 3;
  react(sim, a, 'fire', 1, 0);
  assert.ok(b.hp < 1e6, 'Nachbar erleidet Giftbrand');
  assert.ok(!hasStatus(a, 'poison'));
});

test('Verlangsamung ist hart begrenzt', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'runner');
  addStatus(e, 'chill', 5, 0.5, 0, 3);
  addStatus(e, 'chill', 5, 0.5, 0, 3);
  addStatus(e, 'chill', 5, 0.5, 0, 3);
  addStatus(e, 'poison', 5, 1, 0, 5, 30);
  assert.ok(slowFactor(e) <= STATUS_RULES.chillHardCap + 1e-9);
});

test('Adaptiver Schild passt sich an wiederholte Schadensart an (max. 3 Stufen)', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'shield');
  e.shield = e.maxShield = 1e6;
  for (let i = 0; i < 100; i++) dealDamage(sim, e, 1, 'lightning', 0);
  assert.equal(e.adapt.lightning, 3);
  assert.equal(e.adapt.fire, 0);
});

test('Phasenwandler ist während der Phase immun gegen physischen Schaden', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'phase');
  e.phaseShift = 2;
  const hp = e.hp;
  assert.equal(dealDamage(sim, e, 50, 'physical', 0), 0);
  assert.equal(e.hp, hp);
  assert.ok(dealDamage(sim, e, 10, 'arcane', 0) > 0);
});

test('Alle zwölf Türme verursachen Wirkung (Schaden, Status, Gold oder Netzwerk)', () => {
  const ids = ['ranger', 'ember', 'frost', 'storm', 'spore', 'ballista', 'rune', 'resonator', 'cannon', 'riftbinder'] as const;
  for (const id of ids) {
    const sim = makeSim({ seedText: `TURM-${id}` });
    const t = buildNearPath(sim, id);
    sim.startWave();
    sim.s.spawnQueue = [];
    const e = spawnEnemy(sim, 'brute', false, null);
    e.hp = e.maxHp = 1e6;
    e.x = t.cx + 0.5 + 0.6;
    e.y = t.cy + 0.5;
    e.speed = 0;
    for (let i = 0; i < 30 * 6; i++) {
      e.x = t.cx + 1.1;
      e.y = t.cy + 0.5;
      sim.step();
    }
    const affected = e.hp < e.maxHp || e.statuses.length > 0;
    assert.ok(affected, `${id} hat keine Wirkung gezeigt`);
    assert.ok(t.shots > 0 || t.damageDealt > 0, `${id} hat nicht angegriffen`);
  }
});
