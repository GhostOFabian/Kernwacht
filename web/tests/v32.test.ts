import assert from 'node:assert/strict';
import { test } from 'node:test';
import { BOSS_HP_GLOBAL, bossHpMul } from '../src/config/balance.ts';
import { CHAMPIONS } from '../src/config/champions.ts';
import { canEquipItem, ITEMS, RECIPES } from '../src/config/items.ts';
import { TOWER_UNLOCKS } from '../src/config/passiveTree.ts';
import { BUILD_ORDER, TOWER_ORDER, TOWERS } from '../src/config/towers.ts';
import type { ChampionId } from '../src/core/types.ts';
import { dealDamage } from '../src/sim/DamageSystem.ts';
import { PRESPEC, respecCost, RESPEC_MUL, specCost } from '../src/sim/EconomySystem.ts';
import { getStatus } from '../src/sim/ElementSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { championWalkable } from '../src/sim/ChampionSystem.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';
import { SIM } from '../src/config/balance.ts';

const heSim = (champion: ChampionId = 'lyra', seed = 'V32') => GameSimulation.create({
  mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
  heroEndless: { champion, startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {} }, runId: `v32-${seed}-${champion}` },
});

test('Dornenhain: physisch, Rüstungsbruch bis 8 Stapel, Blutungsschaden und weniger physische Resistenz', () => {
  const d = TOWERS.thornwood;
  assert.equal(d.damageType, 'physical');
  assert.ok(!('poisonDps' in d.base) || !d.base.poisonDps);
  const sim = makeSim({ seedText: 'THORN' });
  sim.s.gold = 1e5;
  const t = buildNearPath(sim, 'ranger');
  t.def = 'thornwood';
  sim.reindex();
  sim.c.statsDirty = true;
  sim.refreshCaches();
  const st = sim.stats(t);
  assert.ok(st.bleedDps > 0 && st.shredPct > 0);
  const e = addEnemy(sim, 'golem');
  e.hp = e.maxHp = 1e9;
  const plainHp = (() => { const o = addEnemy(sim, 'golem'); o.hp = o.maxHp = 1e9; dealDamage(sim, o, 1000, 'physical', 0); return 1e9 - o.hp; })();
  for (let i = 0; i < 12; i++) {
    t.cooldown = 0;
    t.target = e.uid;
    e.x = t.cx + 0.5; e.y = t.cy + 0.5;
    sim.step();
  }
  const s = getStatus(e, 'shred');
  assert.ok(s, 'Rüstungsbruch aktiv');
  assert.ok(s!.stacks <= 8);
  const hp0 = e.hp;
  dealDamage(sim, e, 1000, 'physical', 0);
  const shredded = hp0 - e.hp;
  assert.ok(shredded > plainHp, `mehr physischer Schaden durch Rüstungsbruch: ${shredded} vs ${plainHp}`);
});

test('Turmleiste nach Freischaltung sortiert, Tasten in Reihenfolge eindeutig', () => {
  const waves = TOWER_ORDER.map((id) => TOWER_UNLOCKS[id]);
  for (let i = 1; i < waves.length; i++) assert.ok(waves[i] >= waves[i - 1], `${TOWER_ORDER[i]} vor ${TOWER_ORDER[i - 1]}`);
  assert.deepEqual(TOWER_ORDER.slice(0, 10).map((id) => TOWERS[id].hotkey), ['1', '2', '3', '4', '5', '6', '7', '8', '9', '0']);
  // Lane-Barrikade (3.8) teilt die Taste B mit der Mazing-Barrikade (nie gleichzeitig verfügbar)
  const keys = BUILD_ORDER.filter((id) => id !== 'lanebarricade').map((id) => TOWERS[id].hotkey);
  assert.equal(new Set(keys).size, keys.length, 'keine doppelten Tasten');
});

test('Spezialisierung wechseln: einzeln zum halben Preis, ganze Gruppe per Knopf (inkl. Ausbau)', () => {
  const sim = makeSim({ seedText: 'RESPEC' });
  const a = buildNearPath(sim, 'ranger');
  const b = buildNearPath(sim, 'ranger');
  const c = buildNearPath(sim, 'ranger');
  sim.s.gold = 1e5;
  while (a.level < 4) assert.ok(sim.upgrade(a.uid).ok);
  assert.ok(sim.specialize(a.uid, 1).ok);
  assert.equal(respecCost(a, 2), Math.round(specCost(a, 2) * RESPEC_MUL));
  const g = sim.s.gold;
  assert.ok(sim.respec(a.uid, 2).ok);
  assert.equal(a.spec, 2);
  assert.equal(g - sim.s.gold, Math.round(specCost(a, 2) * RESPEC_MUL));
  assert.equal(sim.respec(a.uid, 2).ok, false);
  // Gruppe: alle auf A, auch b und c (Stufe 1) werden ausgebaut
  const r = sim.groupSpec([a.uid, b.uid, c.uid], 1);
  assert.ok(r.ok, (r as { reason?: string }).reason ?? "");
  for (const t of [a, b, c]) {
    assert.equal(t.level, 4);
    assert.equal(t.spec, 1);
  }
  assert.equal(sim.groupSpec([a.uid, b.uid, c.uid], 1).ok, false, 'bereits alle A');
  // Zu wenig Gold: so viele wie möglich
  sim.s.gold = respecCost(a, 2) + 1;
  const r2 = sim.groupSpec([a.uid, b.uid, c.uid], 2);
  assert.ok(r2.ok);
  assert.equal([a, b, c].filter((t) => t.spec === 2).length, 1);
});

test('Spezialisierter Kauf ab Welle 150, Boss-LP allgemein erhöht', () => {
  assert.equal(PRESPEC.from, 150);
  const sim = heSim('lyra', 'P150');
  sim.s.gold = 1e6;
  sim.s.wave = 150;
  const t = buildNearPath(sim, 'frost');
  sim.sell(t.uid);
  assert.ok(sim.build('frost', t.cx, t.cy, 2).ok);
  assert.equal(sim.towerAt(t.cx, t.cy)!.spec, 2);
  assert.equal(BOSS_HP_GLOBAL, 1.5);
  assert.ok(Math.abs(bossHpMul(10, false) - 1.5) < 1e-9);
  assert.ok(bossHpMul(100, true) > 20);
});

test('Heldenschmiede: jeder Held hat zwei eigene herstellbare Gegenstände', () => {
  const ids = Object.keys(CHAMPIONS) as ChampionId[];
  assert.ok(ids.length >= 12);
  for (const id of ids) {
    const rec = RECIPES.filter((r) => r.kind === 'hero' && r.champion === id);
    assert.equal(rec.length, 2, id);
    for (const r of rec) {
      const d = ITEMS[r.result!];
      assert.ok(d && d.craftOnly && canEquipItem(d.id, id), `${id}: ${r.result}`);
      assert.equal(canEquipItem(d.id, id === 'lyra' ? 'brann' : 'lyra'), false);
    }
  }
});

test('Heldenschmiede: herstellen am Laden, Waffe aus Signaturgegenstand, fremde Rezepte gesperrt', () => {
  const sim = heSim('brann', 'FORGE');
  const he = sim.s.he!;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1]]) {
    if (championWalkable(sim, he.shop.x + dx, he.shop.y + dy)) {
      he.champion.x = he.champion.hx = he.shop.x + dx + 0.5;
      he.champion.y = he.champion.hy = he.shop.y + dy + 0.5;
      he.champion.path = [];
      break;
    }
  }
  he.resources = { metal: 999, crystal: 999, essence: 999, relic: 999 } as typeof he.resources;
  sim.s.wave = 5;
  assert.equal(sim.craft('hero:brannSigil').ok, false, 'erst ab Welle 8');
  sim.s.wave = 8;
  assert.equal(sim.craft('hero:lyraSpindle').ok, false, 'fremder Held');
  const r1 = sim.craft('hero:brannSigil');
  assert.ok(r1.ok, (r1 as { reason?: string }).reason ?? "");
  assert.ok(he.champion.inventory.some((i) => i.item === 'brannSigil'));
  assert.equal(sim.craft('hero:brannBlade').ok, false, 'erst ab Welle 20');
  sim.s.wave = 20;
  const r2 = sim.craft('hero:brannBlade');
  assert.ok(r2.ok, (r2 as { reason?: string }).reason ?? "");
  assert.ok(he.champion.inventory.some((i) => i.item === 'brannBlade'));
  assert.ok(!he.champion.inventory.some((i) => i.item === 'brannSigil'), 'Signaturgegenstand verbraucht');
  for (let i = 0; i < Math.round(1 / SIM.dt); i++) sim.step();
});
