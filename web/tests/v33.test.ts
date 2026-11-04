import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHAMPIONS } from '../src/config/champions.ts';
import { BOSSES, ENEMIES } from '../src/config/enemies.ts';
import { focusBonus, TOWER_LIMIT, towerLimit } from '../src/config/scaling.ts';
import { TOWERS } from '../src/config/towers.ts';
import { applyHit } from '../src/sim/DamageSystem.ts';
import { skillPointsForLevel } from '../src/sim/ChampionSystem.ts';
import { countedTowers } from '../src/sim/EconomySystem.ts';
import { pickTargets, towerPayload } from '../src/sim/TowerSystem.ts';
import { damageMatchups } from '../src/ui/text.ts';
import { addEnemy, buildNearPath, makeSim, openCells } from './helpers.ts';

test('Turmlimit (Endlos): 20 zu Beginn (3.8), alle 5 Wellen +1, Fokusbonus für freie Plätze, normale Modi unbegrenzt', () => {
  assert.equal(towerLimit(0, true), TOWER_LIMIT.base);
  assert.equal(towerLimit(4, true), 20);
  assert.equal(towerLimit(5, true), 21);
  assert.equal(towerLimit(100, true), 40);
  assert.equal(towerLimit(100, false), Infinity);
  assert.equal(focusBonus(0, 26), TOWER_LIMIT.focusMax);
  assert.ok(Math.abs(focusBonus(20, 26) - 0.12) < 1e-9);
  assert.equal(focusBonus(5, Infinity), 0);
  const sim = makeSim({ mode: 'endless', seedText: 'LIMIT' });
  sim.s.gold = 1e6;
  let built = 0;
  for (const c of openCells(sim)) {
    if (sim.build('ranger', c.x, c.y).ok) built++;
    if (built > 25) break;
  }
  assert.equal(built, 20);
  assert.equal(countedTowers(sim), 20);
  const c = openCells(sim).find((x) => sim.canBuild('ranger', x.x, x.y).reason?.startsWith('Turmlimit'));
  assert.ok(c, 'Grund „Turmlimit erreicht“');
  // Fokus: ein einzelner Turm ist stärker als einer von sechs
  const solo = makeSim({ mode: 'endless', seedText: 'LIMIT' });
  solo.s.gold = 1e6;
  const t1 = buildNearPath(solo, 'ranger');
  const full = sim.s.towers[0];
  assert.ok(solo.stats(t1).damage > sim.stats(full).damage * 1.05);
  // Quick-Modus: kein Limit
  const q = makeSim({ seedText: 'LIMIT' });
  q.s.gold = 1e6;
  let qb = 0;
  for (const cc of openCells(q)) { if (q.build('ranger', cc.x, cc.y).ok) qb++; if (qb > 10) break; }
  assert.equal(qb, 11);
});

test('Drachenhorst: Flieger zuerst und ×2,5 Schaden gegen Flieger', () => {
  assert.equal(TOWERS.dragon.base.airDmgMul, 2.5);
  const sim = makeSim({ seedText: 'DRAGON' });
  const t = buildNearPath(sim, 'dragon');
  const st = sim.stats(t);
  const ground = addEnemy(sim, 'brute', t.cx + 0.5, t.cy + 0.5);
  const air = addEnemy(sim, 'flyer', t.cx + 1.5, t.cy + 0.5);
  const tg = pickTargets(sim, t, st, 1);
  assert.equal(tg[0]?.uid, air.uid, 'Flieger zuerst');
  ground.hp = ground.maxHp = air.hp = air.maxHp = 1e7;
  const p = towerPayload(t, { ...st, critChance: 0, splash: 0, burnDps: 0 });
  applyHit(sim, ground, p);
  applyHit(sim, air, p);
  const dg = (1e7 - ground.hp) / (1 - (ENEMIES.brute.resist.fire ?? 0));
  const da = (1e7 - air.hp) / (1 - (ENEMIES.flyer.resist.fire ?? 0));
  assert.ok(Math.abs(da / dg - 2.5) < 0.05, `Verhältnis ${da / dg}`);
});

test('Skillbaum: bis Heldenstufe 99 reichen die Ränge für alle Punkte', () => {
  const pts = skillPointsForLevel(99);
  for (const c of Object.values(CHAMPIONS)) {
    const ranks = c.tree.reduce((a, n) => a + n.maxRank, 0);
    assert.ok(ranks >= pts, `${c.id}: ${ranks} Ränge < ${pts} Punkte`);
    assert.ok(new Set(c.tree.map((n) => n.id)).size === c.tree.length, `${c.id}: doppelte Knoten`);
    for (const n of c.tree) for (const r of n.requires ?? []) assert.ok(c.tree.some((o) => o.id === r.id), `${c.id}: ${n.id} braucht ${r.id}`);
  }
});

test('Konter passen zu den Resistenzen: genannte Konter-Schadensarten werden nicht resistiert', () => {
  // Schlund: Gift und Feuer gegen die Brut; Weltenbrecher: Armbrust/Leerenlanze (physisch/arkan)
  assert.ok((BOSSES.maw.resist.poison ?? 0) <= 0 && (BOSSES.maw.resist.fire ?? 0) <= 0);
  assert.ok((BOSSES.worldbreaker.resist.physical ?? 0) <= 0 && (BOSSES.worldbreaker.resist.arcane ?? 0) <= 0);
  // Abgeleitete Matchups enthalten die Datenwerte
  const fire = damageMatchups('fire');
  assert.ok(fire.bad.some((x) => x.startsWith('Die Aschenkönigin')));
  assert.ok(fire.good.some((x) => x.startsWith('Moosling')));
  const ln = damageMatchups('lightning');
  assert.ok(ln.bad.some((x) => x.startsWith('Funkenfresser')));
  assert.match(TOWERS.storm.counters, /Funkenfresser/);
  assert.match(TOWERS.spore.counters, /golem/i);
  assert.doesNotMatch(TOWERS.thornwood.counters, /schnell erholen/);
});
