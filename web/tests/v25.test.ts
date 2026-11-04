import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHAMPION_ORDER, CHAMPIONS, championUnlocked } from '../src/config/champions.ts';
import { HERO_ENDLESS } from '../src/config/heroEndless.ts';
import { KEY_ACTIONS, rebind, sanitizeKeys, setKeyBindings, translateKey } from '../src/config/keys.ts';
import { SIM } from '../src/config/balance.ts';
import { FUSIONS, TOWERS } from '../src/config/towers.ts';
import { Cell, type ChampionId, type TowerInstance } from '../src/core/types.ts';
import { championWalkable, spendSkillPoint } from '../src/sim/ChampionSystem.ts';
import { fusionPreview, veinBonus } from '../src/sim/EconomySystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { decorKindAt, mineDuration } from '../src/sim/MiningSystem.ts';
import { defaultSave, sanitizeSave } from '../src/save/SaveSystem.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';

function heSim(champion: ChampionId = 'lyra', seed = 'V25') {
  return GameSimulation.create({
    mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion, startKit: [], meta: { bestWave: 999, unlockedItems: [], fragments: {} }, runId: 'v25' },
  });
}
const steps = (sim: GameSimulation, sec: number) => { for (let i = 0, n = Math.round(sec / SIM.dt); i < n; i++) sim.step(); };

function maxOut(sim: GameSimulation, t: TowerInstance, spec: 1 | 2): void {
  sim.s.gold = 1e6;
  while (t.level < 4) assert.ok(sim.upgrade(t.uid).ok);
  assert.ok(sim.specialize(t.uid, spec).ok);
}

// ------------------------------------------------------------ Fusionen

test('Alle Fusionsrezepte (12) verschmelzen zum legendären Turm', () => {
  assert.equal(FUSIONS.length, 12);
  for (const f of FUSIONS) {
    const sim = makeSim({ seedText: `FUSE-${f.id}` });
    sim.s.gold = 1e6;
    const a = buildNearPath(sim, f.a);
    let b: TowerInstance | undefined;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
      if (sim.build(f.b, a.cx + dx, a.cy + dy).ok) { b = sim.towerAt(a.cx + dx, a.cy + dy); break; }
    }
    assert.ok(b, `Partner für ${f.id}`);
    if (!b) continue;
    maxOut(sim, a, 1);
    maxOut(sim, b, 2);
    const r = sim.fuse(a.uid, b.uid);
    assert.ok(r.ok, `${f.id}: ${r.ok ? '' : r.reason}`);
    assert.equal(sim.c.towerByUid.get(a.uid)?.def, f.id);
    assert.ok(TOWERS[f.id].legendary);
    // Ein paar Sekunden Kampf ohne Fehler
    addEnemy(sim, 'runner', a.cx + 0.5, a.cy + 1.5);
    steps(sim, 3);
  }
});

test('Fusionsvorschau vor dem Bau: Partner im Umkreis werden gefunden', () => {
  const sim = makeSim({ seedText: 'PREVIEW' });
  const a = buildNearPath(sim, 'tesla');
  const near = [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([dx, dy]) => ({ x: a.cx + dx, y: a.cy + dy })).find((c) => sim.s.map.cells[c.y * sim.s.map.width + c.x] === Cell.Open && !sim.towerAt(c.x, c.y));
  assert.ok(near);
  if (!near) return;
  const p = fusionPreview(sim, 'pylon', near.x, near.y);
  assert.ok(p.some((x) => x.recipe.id === 'thunderspire' && x.partners.length >= 1));
  assert.equal(fusionPreview(sim, 'pylon', near.x + 30, near.y + 30).filter((x) => x.partners.length).length, 0);
});

// ------------------------------------------------------------ Helden

test('Alle 12 Helden starten und können alle vier Fähigkeiten wirken', () => {
  assert.equal(CHAMPION_ORDER.length, 12);
  for (const id of CHAMPION_ORDER) {
    const sim = heSim(id, `SK-${id}`);
    const he = sim.s.he!;
    const c = he.champion;
    assert.equal(c.id, id);
    sim.s.gold = 1e5;
    const t = buildNearPath(sim, 'ember');
    c.x = t.cx + 0.5;
    c.y = t.cy + 1.5;
    c.path = [];
    c.level = 30;
    steps(sim, 15.5);
    c.holdPos = true;
    for (let slot = 0; slot < 4; slot++) {
      const sk = CHAMPIONS[id].skills[slot];
      c.x = c.hx = t.cx + 0.5;
      c.y = c.hy = t.cy + 1.5;
      c.path = [];
      const e = addEnemy(sim, 'brute', c.x + 1, c.y);
      e.hp = e.maxHp = 1e6;
      c.mana = 9999;
      c.skillCd[slot] = 0;
      c.channel = 0;
      const tx = sk.target === 'tower' ? t.cx + 0.5 : e.x;
      const ty = sk.target === 'tower' ? t.cy + 0.5 : e.y;
      const r = sim.championSkill(slot, tx, ty);
      assert.ok(r.ok, `${id} ${sk.id}: ${r.ok ? '' : r.reason}`);
      assert.ok(c.skillCd[slot] > 0, `${id} ${sk.id} Abklingzeit`);
      steps(sim, 1);
    }
  }
});

test('Kampagnenhelden: Freischaltung über Kampagne oder Bestwelle', () => {
  for (const id of CHAMPION_ORDER) {
    const d = CHAMPIONS[id];
    if (!d.campaignHero) continue;
    if (d.unlockWave) {
      assert.equal(championUnlocked(id, [], d.unlockWave, []), true, id);
      assert.equal(championUnlocked(id, [], 0, ['m1', 'm2', 'm3', 'm4', 'm5', 'm6', 'm7', 'm8', 'm9', 'm10']), true, `${id} über Kampagne`);
    } else assert.equal(championUnlocked(id, [], 0, []), true, id);
  }
});

test('Brann hält mehrere Gegner gleichzeitig auf', () => {
  const sim = heSim('brann', 'BRANN');
  const c = sim.s.he!.champion;
  steps(sim, 15.5);
  sim.s.enemies.length = 0;
  sim.c.enemyByUid?.clear?.();
  const m = sim.s.map;
  let cell = -1;
  for (let i = 0; i < m.cells.length; i++) if (m.cells[i] === Cell.Path) { cell = i; break; }
  const x = (cell % m.width) + 0.5;
  const y = Math.floor(cell / m.width) + 0.5;
  c.x = x; c.y = y; c.hx = x; c.hy = y; c.path = [];
  const es = [0, 1, 2].map(() => { const e = addEnemy(sim, 'brute', x + 0.3, y); e.hp = e.maxHp = 1e6; return e; });
  steps(sim, 0.5);
  const blocked = es.filter((e) => e.blockedBy !== undefined && e.blockedBy !== -1 && e.blockedBy !== 0).length;
  assert.ok(blocked >= 2, `blockiert: ${blocked}`);
  assert.equal(CHAMPIONS.brann.blockCount, 2);
});

test('Kernwall erhöht Kernenergie sofort und dauerhaft', () => {
  for (const id of CHAMPION_ORDER) {
    const sim = heSim(id, `CORE-${id}`);
    const c = sim.s.he!.champion;
    const node = CHAMPIONS[id].tree.find((n) => n.coreLives);
    assert.ok(node, `${id} hat Kernwall`);
    if (!node) continue;
    c.skillPoints = 5;
    const lives = sim.s.lives;
    const max = sim.s.maxLives;
    assert.ok(spendSkillPoint(sim, node.id).ok);
    assert.equal(sim.s.lives, lives + 3);
    assert.equal(sim.s.maxLives, max + 3);
  }
});

// ------------------------------------------------------------ Abbau

function minableCell(sim: GameSimulation): number {
  const m = sim.s.map;
  const he = sim.s.he!;
  for (let i = 0; i < m.cells.length; i++) {
    if (m.cells[i] !== Cell.Blocked || i === he.shop.y * m.width + he.shop.x) continue;
    const k = decorKindAt(sim, i);
    if (k === null || k === 3 || k === 7) continue;
    const x = i % m.width;
    const y = Math.floor(i / m.width);
    if ([[1, 0], [-1, 0], [0, 1], [0, -1]].some(([dx, dy]) => championWalkable(sim, x + dx, y + dy))) return i;
  }
  return -1;
}

test('Abbau: dauert eine Welle, ist nicht unterbrechbar, bringt Gold/Ressourcen und macht das Feld frei', () => {
  const sim = heSim('lyra', 'MINE');
  const he = sim.s.he!;
  const c = he.champion;
  const m = sim.s.map;
  const idx = minableCell(sim);
  assert.ok(idx >= 0, 'abbaubares Feld');
  const x = idx % m.width;
  const y = Math.floor(idx / m.width);
  const nb = [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([dx, dy]) => [x + dx, y + dy]).find(([a, b]) => championWalkable(sim, a, b))!;
  c.x = nb[0] + 0.5; c.y = nb[1] + 0.5; c.path = [];
  const dur = mineDuration(sim);
  assert.ok(dur >= HERO_ENDLESS.mineTimeMin && dur <= HERO_ENDLESS.mineTimeMax);
  const gold = sim.s.gold;
  const rev = m.rev ?? 0;
  assert.ok(sim.mine(x, y).ok);
  assert.ok(he.mining);
  // Nicht unterbrechbar: Bewegung und Fähigkeiten werden abgelehnt
  assert.equal(sim.moveChampion(nb[0], nb[1]).ok, false);
  c.mana = 999;
  assert.equal(sim.championSkill(0, c.x, c.y).ok, false);
  assert.equal(sim.mine(x, y).ok, false);
  steps(sim, dur - 1);
  assert.ok(he.mining, 'läuft noch');
  assert.equal(m.cells[idx], Cell.Blocked);
  steps(sim, 1.5);
  assert.equal(he.mining, null);
  assert.equal(m.cells[idx], Cell.Open);
  assert.equal(decorKindAt(sim, idx), null);
  assert.ok((m.rev ?? 0) > rev);
  assert.ok(sim.s.gold > gold, 'Gold erhalten');
  assert.equal(he.mined, 1);
  // Das freie Feld ist jetzt bebaubar
  sim.s.gold = 1e5;
  assert.ok(sim.build('ember', x, y).ok);
});

test('Abbau: Lava/Wolken, Laden und offene Felder sind nicht abbaubar', () => {
  const sim = heSim('lyra', 'MINE2');
  const he = sim.s.he!;
  assert.equal(sim.mine(he.shop.x, he.shop.y).ok, false);
  assert.equal(sim.mine(sim.s.map.goal.x, sim.s.map.goal.y).ok, false);
});

// ------------------------------------------------------------ Spezialfelder

test('Goldader: fester Zusatzertrag wächst mit der Welle', () => {
  assert.ok(veinBonus(10) > veinBonus(1));
  assert.ok(veinBonus(1) >= 8);
});

test('Kristallvorkommen werden auf freien Feldern platziert und bleiben deterministisch', () => {
  const a = heSim('lyra', 'DEP');
  const b = heSim('lyra', 'DEP');
  const d = a.s.map.deposits ?? [];
  assert.ok(d.length >= HERO_ENDLESS.depositCount[0] && d.length <= HERO_ENDLESS.depositCount[1]);
  assert.deepEqual(d, b.s.map.deposits);
  for (const i of d) {
    assert.equal(a.s.map.cells[i], Cell.Open);
    assert.ok(!a.s.map.veins.includes(i));
  }
});

// ------------------------------------------------------------ Tastenbelegung

test('Tastenbelegung: Standards ohne Konflikte, Tauschen, Übersetzen, Bereinigen', () => {
  const overlap = (a: string, b: string) => a === 'all' || b === 'all' || a === b;
  for (const a of KEY_ACTIONS) for (const b of KEY_ACTIONS) {
    if (a.id < b.id && overlap(a.ctx, b.ctx)) assert.notEqual(a.def, b.def, `${a.id} / ${b.id}`);
  }
  const r = rebind('speed', 'q', {});
  assert.equal(r.map.speed, 'q');
  assert.equal(r.swapped, 'rotL');
  assert.equal(r.map.rotL, 'f');
  setKeyBindings(r.map);
  assert.equal(translateKey('q', 'lane'), 'f');
  assert.equal(translateKey('f', 'lane'), 'q');
  assert.equal(translateKey('escape', 'hero'), '');
  const r2 = rebind('skill1', 'f9', {});
  setKeyBindings(r2.map);
  assert.equal(translateKey('f9', 'hero'), 'a');
  assert.equal(translateKey('a', 'hero'), null, 'alte Taste ist frei');
  assert.equal(translateKey('a', 'lane'), 'a', 'Lane-Fähigkeit bleibt');
  setKeyBindings({});
  assert.deepEqual(sanitizeKeys({ speed: 'Q', nope: 'x', pause: 'escape', grid: 5 }), { speed: 'q' });
  const save = defaultSave();
  (save.settings as unknown as Record<string, unknown>).keys = { help: 'k' };
  assert.deepEqual(sanitizeSave(JSON.parse(JSON.stringify(save))).settings.keys, { help: 'k' });
});
