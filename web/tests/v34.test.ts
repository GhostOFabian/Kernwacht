import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHAMPIONS } from '../src/config/champions.ts';
import { canEquipItem } from '../src/config/items.ts';
import { towerLimit } from '../src/config/scaling.ts';
import type { ChampionId } from '../src/core/types.ts';
import { currentTowerLimit } from '../src/sim/EconomySystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { missingItems } from '../src/sim/LootSystem.ts';
import { collectionOffers } from '../src/sim/InventorySystem.ts';
import { completeWave } from '../src/sim/WaveDirector.ts';
import { makeSim } from './helpers.ts';

const heSim = (champion: ChampionId = 'brann', seed = 'V34', meta: Record<string, unknown> = {}) => GameSimulation.create({
  mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
  heroEndless: { champion, startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, ...meta }, runId: `v34-${seed}-${champion}` },
});

test('Skillbaum zurücksetzen: alle Punkte zurück, Kernwall-Energie wieder abgezogen, jederzeit', () => {
  const sim = heSim('brann', 'TREE', { champ: { level: 30, xp: 0, tree: {} } });
  const he = sim.s.he!;
  const c = he.champion;
  const pts = c.skillPoints;
  const core = CHAMPIONS.brann.tree.find((n) => n.coreLives)!;
  const lives = sim.s.maxLives;
  assert.ok(sim.learnSkill(core.id).ok);
  assert.ok(sim.learnSkill(core.id).ok);
  const other = CHAMPIONS.brann.tree.find((n) => n.tier === 1 && !n.coreLives)!;
  assert.ok(sim.learnSkill(other.id).ok);
  assert.equal(sim.s.maxLives, lives + 2 * core.coreLives!);
  sim.s.phase = 'combat';
  const r = sim.treeReset();
  assert.ok(r.ok);
  assert.equal(c.skillPoints, pts);
  assert.deepEqual(c.tree, {});
  assert.equal(sim.s.maxLives, lives);
  assert.equal(sim.treeReset().ok, false, 'nichts mehr zurückzusetzen');
});

test('Paragon zurücksetzen auch im Kampf', () => {
  const sim = heSim('brann', 'PARA', { champ: { level: 99, xp: 0, tree: {} }, paragon: { level: 5, xp: 0, points: {} } });
  assert.ok(sim.paragonPoint('pg_might').ok);
  sim.s.phase = 'combat';
  assert.ok(sim.paragonReset().ok);
  assert.equal(sim.paragonReset().ok, false);
});

test('Bosswelle geschafft: +1 Turmplatz, keine Kernenergie zurück (3.8)', () => {
  const sim = makeSim({ mode: 'endless', seedText: 'BOSSSLOT' });
  sim.s.wave = 10;
  sim.s.lives = 5;
  const lim = currentTowerLimit(sim);
  assert.equal(lim, towerLimit(10, true));
  sim.s.currentWave = { index: 10, isBoss: true, groups: [] } as unknown as typeof sim.s.currentWave;
  sim.s.phase = 'combat';
  completeWave(sim);
  assert.equal(sim.s.bossSlots, 1);
  assert.equal(currentTowerLimit(sim), lim + 1);
  assert.equal(sim.s.lives, 5, '3.8: keine Kernenergie zurück');
  // normale Welle: kein Platz
  sim.s.wave = 11;
  sim.s.currentWave = { index: 11, isBoss: false, groups: [] } as unknown as typeof sim.s.currentWave;
  sim.s.phase = 'combat';
  completeWave(sim);
  assert.equal(sim.s.bossSlots, 1);
});

test('Laden und Beute: nur Gegenstände für den aktuellen Helden', () => {
  const sim = heSim('lyra', 'SHOP', { bestWave: 30, unlockedItems: ['ironBlade', 'runeStaff', 'leather'] });
  const he = sim.s.he!;
  for (const id of missingItems(he, 30)) assert.ok(canEquipItem(id, 'lyra'), id);
  assert.ok(!missingItems(he, 30).includes('dragonFang'), 'Brann-/Vela-Waffe nicht für Lyra');
  const offers = collectionOffers(he);
  assert.ok(offers.includes('runeStaff') && offers.includes('leather'));
  assert.ok(!offers.includes('ironBlade'), 'Eisenklinge (Brann/Vela) nicht im Laden für Lyra');
});
