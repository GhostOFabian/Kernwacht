import assert from 'node:assert/strict';
import { test } from 'node:test';
import { PARAGON_NODES, PARAGON_TREES } from '../src/config/paragon.ts';
import { towerCost } from '../src/sim/EconomySystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { buildNearPath } from './helpers.ts';

function heSim(points: Record<string, number> = {}, level = 60) {
  return GameSimulation.create({
    mode: 'heroEndless', seedText: 'V29', variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, champ: { level: 99, xp: 0, tree: {} }, paragon: { level, xp: 0, points } }, runId: 'v29' },
  });
}

test('Paragon hat einen eigenen Turm-Baum mit Knoten, die nur Türme verbessern', () => {
  assert.equal(PARAGON_TREES.length, 4);
  const tower = PARAGON_NODES.filter((n) => n.tree === 'tower');
  assert.ok(tower.length >= 6);
  for (const n of tower) for (const k of Object.keys(n.per)) assert.ok(/^tower|^mine/.test(k), `${n.id}: ${k} wirkt nur auf Türme`);
});

test('Turm-Paragon: Baukosten, kritische Chance, Panzerbrechung, Schaden und Goldgräber-Ertrag', () => {
  const base = heSim();
  const pg = heSim({ pg_builder: 25, pg_tcrit: 20, pg_pierce: 10, pg_tower: 5 }, 60);
  assert.ok(towerCost(pg, 'ranger') < towerCost(base, 'ranger'), 'billiger');
  assert.ok(towerCost(pg, 'ranger') >= Math.round(towerCost(base, 'ranger') * 0.9) - 1, 'höchstens −10 %');
  const a = buildNearPath(base, 'ranger');
  const b = buildNearPath(pg, 'ranger');
  const sa = base.stats(a);
  const sb = pg.stats(b);
  assert.ok(sb.critChance > sa.critChance);
  assert.ok(sb.armorPierce > sa.armorPierce);
  assert.ok(sb.damage > sa.damage);
  // Laufende Vergabe wirkt sofort
  const sim = heSim({}, 10);
  sim.s.gold = 1e5;
  let m = undefined as ReturnType<typeof sim.towerAt>;
  for (let y = 0; y < sim.s.map.height - 1 && !m; y++) for (let x = 0; x < sim.s.map.width - 1 && !m; x++) if (sim.build('goldmine', x, y).ok) m = sim.towerAt(x, y);
  assert.ok(m);
  if (!m) return;
  const inc0 = sim.stats(m).income;
  for (let i = 0; i < 10; i++) assert.ok(sim.paragonPoint('pg_mine').ok);
  assert.ok(sim.stats(m).income > inc0, 'Goldgräber-Ertrag steigt');
});

test('Größere Felder sind nur Darstellung: Reichweiten in Feldern bleiben unverändert', async () => {
  const { makeSim } = await import('./helpers.ts');
  const { TOWERS } = await import('../src/config/towers.ts');
  const sim = makeSim({ seedText: 'RANGE29' });
  sim.s.gold = 1e5;
  const m = sim.s.map;
  for (let y = 0; y < m.height; y++) for (let x = 0; x < m.width; x++) {
    const i = y * m.width + x;
    if (m.heights[i] >= 2 || m.ley.includes(i)) continue;
    if (sim.build('ranger', x, y).ok) {
      const t = sim.towerAt(x, y)!;
      assert.equal(sim.stats(t).range, TOWERS.ranger.base.range);
      return;
    }
  }
  assert.fail('kein Bauplatz');
});

test('Alle Türme verkaufen: Erlös wie Einzelverkauf, Runen zurück in den Beutel, Gruppe nur eine Sorte', async () => {
  const { makeSim } = await import('./helpers.ts');
  const { sellAllValue, refundValue } = await import('../src/sim/EconomySystem.ts');
  const sim = makeSim({ seedText: 'SELLALL' });
  const a = buildNearPath(sim, 'ranger');
  const b = buildNearPath(sim, 'ember', 3);
  const c = buildNearPath(sim, 'ranger', 6);
  sim.startWave();
  const expect = refundValue(sim, a) + refundValue(sim, b) + refundValue(sim, c);
  assert.equal(sellAllValue(sim).gold, expect);
  // Nur Waldläufer
  const g0 = sim.s.gold;
  assert.ok(sim.sellAll([a.uid, c.uid]).ok);
  assert.equal(sim.s.towers.length, 1);
  assert.equal(sim.s.gold - g0, expect - refundValue(sim, b));
  assert.ok(sim.sellAll().ok);
  assert.equal(sim.s.towers.length, 0);
  assert.equal(sim.sellAll().ok, false, 'nichts mehr da');
});

test('Fusion: der legendäre Turm entsteht immer auf dem Feld näher an der Lane; Fusionspaare werden erkannt', async () => {
  const { makeSim } = await import('./helpers.ts');
  const { fusionPairs, laneDistance } = await import('../src/sim/EconomySystem.ts');
  const { Cell } = await import('../src/core/types.ts');
  const sim = makeSim({ seedText: 'FUSE-LANE' });
  sim.s.gold = 1e6;
  const m = sim.s.map;
  // Zwei Felder: eins direkt am Weg, eins ein Feld weiter weg (in einer Linie)
  let near: { x: number; y: number } | null = null;
  let far: { x: number; y: number } | null = null;
  outer: for (let y = 2; y < m.height - 2; y++) for (let x = 2; x < m.width - 2; x++) {
    if (m.cells[y * m.width + x] !== Cell.Path) continue;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const n = { x: x + dx, y: y + dy };
      const f = { x: x + 2 * dx, y: y + 2 * dy };
      if (laneDistance(sim, n.x, n.y) < laneDistance(sim, f.x, f.y) && sim.canBuild('ember', f.x, f.y).ok && sim.canBuild('storm', n.x, n.y).ok) {
        near = n;
        far = f;
        break outer;
      }
    }
  }
  assert.ok(near && far, 'Bauplätze');
  if (!near || !far) return;
  assert.ok(sim.build('ember', far.x, far.y).ok);
  assert.ok(sim.build('storm', near.x, near.y).ok);
  const a = sim.towerAt(far.x, far.y)!;
  const b = sim.towerAt(near.x, near.y)!;
  let pairs = fusionPairs(sim);
  assert.equal(pairs.length, 1);
  assert.equal(pairs[0].ready, false);
  assert.equal(pairs[0].target, b);
  for (const [t, sp] of [[a, 1], [b, 2]] as const) {
    while (t.level < 4) assert.ok(sim.upgrade(t.uid).ok);
    assert.ok(sim.specialize(t.uid, sp).ok);
  }
  pairs = fusionPairs(sim);
  assert.equal(pairs[0].ready, true);
  // Verschmelzen vom weiter entfernten Turm aus → Ergebnis trotzdem am Weg
  assert.ok(sim.fuse(a.uid, b.uid).ok);
  const res = sim.towerAt(near.x, near.y);
  assert.ok(res && res.def === 'plasma', 'legendärer Turm am Weg');
  assert.equal(sim.towerAt(far.x, far.y), undefined, 'weiter entferntes Feld ist frei');
  assert.equal(fusionPairs(sim).length, 0);
});

test('Heldenstufe ist für alle Helden gemeinsam: Fortschritt mit einem Helden gilt für jeden anderen; alte Stände werden übernommen', async () => {
  const { commitHeroProgress } = await import('../src/app/progress.ts');
  const { defaultSave, sanitizeSave } = await import('../src/save/SaveSystem.ts');
  const { skillPointsForLevel } = await import('../src/sim/ChampionSystem.ts');
  const save = defaultSave();
  const m = save.progress.heroMeta;
  // Lauf mit Lyra bis Stufe 42
  const a = GameSimulation.create({ mode: 'heroEndless', seedText: 'LV-A', variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, champ: { level: m.heroLevel.level, xp: m.heroLevel.xp, tree: {} } }, runId: 'a' } });
  a.s.he!.champion.level = 42;
  commitHeroProgress(save, a.s);
  assert.equal(m.heroLevel.level, 42);
  // Neuer Lauf mit Brann: startet ebenfalls auf Stufe 42, eigener (leerer) Skillbaum mit vollen Punkten
  const b = GameSimulation.create({ mode: 'heroEndless', seedText: 'LV-B', variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion: 'brann', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, champ: { level: m.heroLevel.level, xp: m.heroLevel.xp, tree: m.champions.brann?.tree ?? {} } }, runId: 'b' } });
  assert.equal(b.s.he!.champion.level, 42);
  assert.equal(b.s.he!.champion.skillPoints, skillPointsForLevel(42));
  // Älterer Spielstand mit Stufen je Held: der weiteste Stand wird die gemeinsame Stufe
  const raw = JSON.parse(JSON.stringify(defaultSave()));
  delete raw.progress.heroMeta.heroLevel;
  raw.progress.heroMeta.champions = { lyra: { level: 12, xp: 0, tree: {} }, vela: { level: 37, xp: 5, tree: {} } };
  assert.equal(sanitizeSave(raw).progress.heroMeta.heroLevel.level, 37);
});

test('Legendäre Türme versetzen: nur legendär, freies Feld, Runen und Ausbau bleiben, kurz außer Gefecht', async () => {
  const { makeSim, openCells } = await import('./helpers.ts');
  const sim = makeSim({ seedText: 'MOVE-LEG' });
  sim.s.gold = 1e6;
  const a = buildNearPath(sim, 'ember');
  let b: ReturnType<typeof sim.towerAt>;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1]]) if (!b && sim.build('storm', a.cx + dx, a.cy + dy).ok) b = sim.towerAt(a.cx + dx, a.cy + dy);
  assert.ok(b);
  const free = openCells(sim)[5];
  assert.equal(sim.moveTower(a.uid, free.x, free.y).ok, false, 'nicht legendär');
  for (const [t, sp] of [[a, 1], [b!, 2]] as const) {
    while (t.level < 4) assert.ok(sim.upgrade(t.uid).ok);
    assert.ok(sim.specialize(t.uid, sp).ok);
  }
  assert.ok(sim.fuse(a.uid, b!.uid).ok);
  const leg = sim.s.towers.find((t) => t.def === 'plasma')!;
  const inv = leg.invested;
  const from = { x: leg.cx, y: leg.cy };
  const other = buildNearPath(sim, 'ranger', 4);
  assert.equal(sim.moveTower(leg.uid, other.cx, other.cy).ok, false, 'belegt');
  const dest = openCells(sim).find((c) => Math.abs(c.x - from.x) + Math.abs(c.y - from.y) > 3)!;
  assert.ok(sim.moveTower(leg.uid, dest.x, dest.y).ok);
  assert.equal(sim.towerAt(dest.x, dest.y), leg);
  assert.equal(sim.towerAt(from.x, from.y), undefined, 'altes Feld frei');
  assert.equal(leg.invested, inv);
  assert.ok(leg.stunned > 0, 'kurz außer Gefecht');
});
