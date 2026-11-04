import assert from 'node:assert/strict';
import { test } from 'node:test';
import { Cell } from '../src/core/types.ts';
import { makeSim } from './helpers.ts';
import type { GameSimulation } from '../src/sim/GameSimulation.ts';

/** Sucht einen 3×2-Block freier Zellen für Pylon + Türme. */
function block(sim: GameSimulation): { x: number; y: number } {
  const m = sim.s.map;
  for (let y = 0; y < m.height - 1; y++) for (let x = 0; x < m.width - 2; x++) {
    let ok = true;
    for (let dy = 0; dy < 2; dy++) for (let dx = 0; dx < 3; dx++) if (m.cells[(y + dy) * m.width + x + dx] !== Cell.Open) ok = false;
    if (ok) return { x, y };
  }
  throw new Error('kein Block');
}

test('Leitpylon verbindet Türme in Reichweite und gewährt Bonus', () => {
  const sim = makeSim();
  sim.s.gold = 10000;
  const b = block(sim);
  assert.ok(sim.build('ember', b.x, b.y).ok);
  const ember = sim.towerAt(b.x, b.y)!;
  const before = sim.stats(ember).damage;
  assert.equal(sim.s.network.links.length, 0);
  assert.ok(sim.build('pylon', b.x + 1, b.y).ok);
  assert.equal(sim.s.network.links.length, 1);
  assert.ok(sim.stats(ember).damage > before, 'Netzwerkbonus erhöht Schaden');
  assert.ok(sim.c.linked.has(ember.uid));
  // Verkauf des Pylons trennt das Netzwerk
  const pylon = sim.towerAt(b.x + 1, b.y)!;
  sim.sell(pylon.uid);
  assert.equal(sim.s.network.links.length, 0);
  assert.equal(sim.stats(ember).damage, before);
});

test('Resonanz Feuer+Frost+Blitz wird im selben Netz aktiv', () => {
  const sim = makeSim();
  sim.s.gold = 10000;
  const b = block(sim);
  sim.build('pylon', b.x + 1, b.y);
  sim.build('ember', b.x, b.y);
  sim.build('frost', b.x + 2, b.y);
  const storm = sim.build('storm', b.x + 1, b.y + 1);
  assert.ok(storm.ok);
  const g = sim.s.network.groups[0];
  assert.ok(g.resonances.includes('elemental'));
  const ember = sim.towerAt(b.x, b.y)!;
  assert.ok((sim.c.resonanceByTower.get(ember.uid) ?? []).includes('elemental'));
});

test('Gestörte Türme verlieren die Verbindung; Netzwerk wird nicht pro Frame neu aufgebaut', () => {
  const sim = makeSim();
  sim.s.gold = 10000;
  const b = block(sim);
  sim.build('pylon', b.x + 1, b.y);
  sim.build('ember', b.x, b.y);
  const v0 = sim.s.network.version;
  for (let i = 0; i < 60; i++) sim.step();
  assert.equal(sim.s.network.version, v0, 'keine Neuberechnung ohne Änderung');
  const ember = sim.towerAt(b.x, b.y)!;
  ember.disrupted = 5;
  sim.c.networkDirty = true;
  sim.refreshCaches();
  assert.equal(sim.s.network.links.length, 0);
});

test('Ein Turm hat höchstens zwei Pylonverbindungen', () => {
  const sim = makeSim();
  sim.s.gold = 10000;
  const b = block(sim);
  sim.build('ember', b.x + 1, b.y);
  sim.build('pylon', b.x, b.y);
  sim.build('pylon', b.x + 2, b.y);
  sim.build('pylon', b.x + 1, b.y + 1);
  const ember = sim.towerAt(b.x + 1, b.y)!;
  const links = sim.s.network.links.filter((l) => l.a === ember.uid || l.b === ember.uid).length;
  assert.ok(links <= 2);
});
