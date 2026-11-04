import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SELL_REFUND, TOWERS } from '../src/config/towers.ts';
import { killEnemy } from '../src/sim/DamageSystem.ts';
import { refundValue } from '../src/sim/EconomySystem.ts';
import { addEnemy, buildNearPath, firstOpen, makeSim } from './helpers.ts';

test('Bau zieht exakt die Baukosten ab und verhindert Doppelplatzierung', () => {
  const sim = makeSim();
  const c = firstOpen(sim);
  const g0 = sim.s.gold;
  assert.ok(sim.build('ranger', c.x, c.y).ok);
  assert.equal(sim.s.gold, g0 - TOWERS.ranger.cost);
  const r = sim.build('ranger', c.x, c.y);
  assert.equal(r.ok, false);
  assert.equal(sim.s.gold, g0 - TOWERS.ranger.cost);
  assert.equal(sim.s.towers.length, 1);
});

test('Bau ohne genug Gold wird abgelehnt, Gold bleibt unverändert', () => {
  const sim = makeSim();
  sim.s.gold = 10;
  const c = firstOpen(sim);
  const r = sim.build('ballista', c.x, c.y);
  assert.equal(r.ok, false);
  assert.equal(sim.s.gold, 10);
});

test('Bau auf Weg oder Hindernis ist nicht möglich (Classic)', () => {
  const sim = makeSim();
  const sp = sim.s.map.spawn;
  assert.equal(sim.canBuild('ranger', sp.x, sp.y).ok, false);
  assert.equal(sim.canBuild('ranger', -1, 0).ok, false);
  assert.equal(sim.canBuild('barricade', firstOpen(sim).x, firstOpen(sim).y).ok, false, 'Barrikade nur im Mazing');
});

test('Upgradekosten, Stufenfolge und Spezialisierung', () => {
  const sim = makeSim();
  const t = buildNearPath(sim, 'ember');
  sim.s.gold = 10000;
  const def = TOWERS.ember;
  assert.equal(sim.specialize(t.uid, 1).ok, false, 'Spezialisierung erst ab Stufe 4');
  let invested = def.cost;
  for (let i = 0; i < 3; i++) {
    const g = sim.s.gold;
    assert.ok(sim.upgrade(t.uid).ok);
    assert.equal(g - sim.s.gold, def.upgrades[i].cost);
    invested += def.upgrades[i].cost;
  }
  assert.equal(t.level, 4);
  assert.equal(sim.upgrade(t.uid).ok, false, 'keine weitere normale Stufe');
  const g = sim.s.gold;
  assert.ok(sim.specialize(t.uid, 2).ok);
  assert.equal(g - sim.s.gold, def.specs[1].cost);
  invested += def.specs[1].cost;
  assert.equal(t.invested, invested);
  assert.equal(sim.specialize(t.uid, 1).ok, false, 'nur eine Spezialisierung');
  // Stufen verändern Funktion, nicht nur Schaden: Schwelbrand aktiviert Brandübersprung
  assert.ok(sim.stats(t).burnSpread > 0);
});

test('Verkauf: volle Erstattung in derselben Bauphase, danach 70 %', () => {
  const sim = makeSim();
  const t = buildNearPath(sim, 'storm');
  sim.s.gold = 1000;
  assert.ok(sim.upgrade(t.uid).ok);
  assert.equal(refundValue(sim, t), t.invested);
  sim.startWave();
  // Welle schnell beenden
  for (const e of sim.s.enemies) killEnemy(sim, e, 0);
  sim.s.spawnQueue = [];
  for (let i = 0; i < 5 && sim.s.phase === 'combat'; i++) sim.step();
  assert.notEqual(sim.s.phase, 'combat');
  assert.equal(refundValue(sim, t), Math.floor(t.invested * SELL_REFUND));
  const g = sim.s.gold;
  assert.ok(sim.sell(t.uid).ok);
  assert.equal(sim.s.gold - g, Math.floor(t.invested * SELL_REFUND));
  assert.equal(sim.s.towers.length, 0);
});

test('Wiederholtes Kaufen/Verkaufen vermehrt nie Gold', () => {
  const sim = makeSim();
  const c = firstOpen(sim);
  const g0 = sim.s.gold;
  for (let i = 0; i < 50; i++) {
    assert.ok(sim.build('ranger', c.x, c.y).ok);
    const t = sim.towerAt(c.x, c.y);
    assert.ok(t);
    if (i % 2) sim.upgrade(t.uid);
    sim.sell(t.uid);
    assert.ok(sim.s.gold <= g0);
  }
  assert.equal(sim.s.gold, g0);
});

test('Todesbelohnung wird pro Gegner genau einmal vergeben', () => {
  const sim = makeSim();
  const e = addEnemy(sim, 'runner');
  const g = sim.s.gold;
  killEnemy(sim, e, 0);
  killEnemy(sim, e, 0);
  const after = sim.s.gold;
  assert.ok(after > g);
  assert.equal(after - g, Math.max(1, Math.round(e.reward)));
  assert.equal(sim.s.stats.kills, 1);
});

test('Goldgräber zahlt ab der Welle nach dem Bau (nicht bei Bau während des Kampfes) und mehr auf Goldadern', () => {
  const sim = makeSim();
  sim.s.gold = 5000;
  const w = sim.s.map.width;
  const m = sim.s.map;
  // Goldgräber belegen 2×2 Felder: passende Ankerfelder suchen
  const anchors = (pred: (cells: number[]) => boolean) => {
    const out: { x: number; y: number }[] = [];
    for (let y = 0; y < m.height - 1; y++) for (let x = 0; x < m.width - 1; x++) {
      const cells = [y * w + x, y * w + x + 1, (y + 1) * w + x, (y + 1) * w + x + 1];
      if (pred(cells) && sim.canBuild('goldmine', x, y).ok) out.push({ x, y });
    }
    return out;
  };
  const v = anchors((c) => c.some((i) => m.veins.includes(i)))[0];
  assert.ok(v, 'Ader-Platz');
  assert.ok(sim.build('goldmine', v.x, v.y).ok);
  const mine = sim.towerAt(v.x + 1, v.y + 1);
  assert.ok(mine, 'über jedes Feld der Grundfläche erreichbar');
  assert.equal(sim.towerAt(v.x, v.y), mine);
  const cells = anchors((c) => !c.some((i) => m.veins.includes(i)));
  assert.ok(sim.build('goldmine', cells[0].x, cells[0].y).ok);
  const mine2 = sim.towerAt(cells[0].x, cells[0].y);
  assert.ok(mine2);
  const c1 = anchors((c) => !c.some((i) => m.veins.includes(i)))[0];
  cells[1] = c1;
  assert.ok(sim.stats(mine).income > sim.stats(mine2).income);
  sim.startWave();
  // Während des Kampfes gebaut → zahlt am Ende dieser Welle noch nicht
  assert.ok(sim.build('goldmine', cells[1].x, cells[1].y).ok, sim.canBuild('goldmine', cells[1].x, cells[1].y).reason ?? '');
  const late = sim.towerAt(cells[1].x, cells[1].y);
  assert.ok(late);
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) killEnemy(sim, e, 0);
  sim.step();
  assert.ok(mine.goldEarned > 0, 'vor der Welle gebaut → Ertrag');
  assert.equal(late.goldEarned, 0, 'im Kampf gebaut → noch kein Ertrag');
  sim.startWave();
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) killEnemy(sim, e, 0);
  sim.step();
  assert.ok(late.goldEarned > 0, 'ab der folgenden Welle Ertrag');
});

test('Goldgräber belegt 2×2 Felder: alle vier Felder belegt, Teilüberlappung abgelehnt, Verkauf gibt alle frei', () => {
  const sim = makeSim({ seedText: 'MINE-2X2' });
  sim.s.gold = 5000;
  const m = sim.s.map;
  const w = m.width;
  let a: { x: number; y: number } | null = null;
  for (let y = 0; y < m.height - 1 && !a; y++) for (let x = 0; x < m.width - 1 && !a; x++) if (sim.canBuild('goldmine', x, y).ok) a = { x, y };
  assert.ok(a);
  if (!a) return;
  assert.ok(sim.build('goldmine', a.x, a.y).ok);
  const t = sim.towerAt(a.x, a.y)!;
  for (const [dx, dy] of [[0, 0], [1, 0], [0, 1], [1, 1]]) assert.equal(sim.towerAt(a.x + dx, a.y + dy), t);
  assert.equal(sim.canBuild('ember', a.x + 1, a.y + 1).ok, false, 'Feld der Grundfläche belegt');
  assert.equal(sim.canBuild('goldmine', a.x + 1, a.y).ok, false, 'Überlappung');
  assert.equal(sim.canBuild('goldmine', a.x - 1, a.y - 1).ok, false, 'Überlappung');
  assert.ok(sim.sell(t.uid).ok);
  for (const [dx, dy] of [[0, 0], [1, 0], [0, 1], [1, 1]]) assert.equal(sim.towerAt(a.x + dx, a.y + dy), undefined);
  // Am Kartenrand nicht möglich
  assert.equal(sim.canBuild('goldmine', w - 1, 0).ok, false);
});
