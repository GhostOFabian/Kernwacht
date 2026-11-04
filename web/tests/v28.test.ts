import assert from 'node:assert/strict';
import { test } from 'node:test';
import { SIM } from '../src/config/balance.ts';
import { Cell, type TowerInstance } from '../src/core/types.ts';
import { killEnemy } from '../src/sim/DamageSystem.ts';
import type { GameSimulation } from '../src/sim/GameSimulation.ts';
import { minesOf } from '../src/sim/TowerSystem.ts';
import { addEnemy, buildNearPath, makeSim } from './helpers.ts';

const steps = (sim: GameSimulation, sec: number) => { for (let i = 0, n = Math.round(sec / SIM.dt); i < n; i++) sim.step(); };

/** Gegner weit weg vom Turm halten, damit nur Minen geworfen, aber nicht ausgelöst werden. */
function farEnemy(sim: GameSimulation, t: TowerInstance) {
  const m = sim.s.map;
  let best = { x: 0, y: 0, d: -1 };
  for (let i = 0; i < m.cells.length; i++) {
    if (m.cells[i] !== Cell.Path) continue;
    const x = (i % m.width) + 0.5;
    const y = Math.floor(i / m.width) + 0.5;
    const d = (x - t.cx) ** 2 + (y - t.cy) ** 2;
    if (d > best.d) best = { x, y, d };
  }
  const e = addEnemy(sim, 'brute', best.x, best.y);
  e.hp = e.maxHp = 1e7;
  e.speed = 0;
  return e;
}

test('Minenwerfer: wirft Minen auf die Lane in Reichweite, höchstens mineMax', () => {
  const sim = makeSim({ seedText: 'MINES' });
  const t = buildNearPath(sim, 'minelayer');
  sim.startWave();
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) e.alive = false;
  farEnemy(sim, t);
  steps(sim, 20);
  const st = sim.stats(t);
  const mines = sim.s.zones.filter((z) => z.kind === 'mine' && z.link === t.uid);
  const n = minesOf(sim, t.uid);
  assert.ok(n >= 2 && n <= Math.round(st.mineMax), `Minen ${n}/${st.mineMax}`);
  steps(sim, 20);
  assert.ok(minesOf(sim, t.uid) <= Math.round(st.mineMax), 'nie mehr als mineMax');
  for (const z of mines) {
    const c = sim.s.map.cells[Math.floor(z.y) * sim.s.map.width + Math.floor(z.x)];
    assert.equal(c, Cell.Path, 'Mine liegt auf der Lane');
    assert.ok((z.x - t.cx - 0.5) ** 2 + (z.y - t.cy - 0.5) ** 2 <= (st.range + 0.3) ** 2);
  }
});

test('Mine explodiert nur bei Bodengegnern, Flieger lösen nicht aus; Minen bleiben über das Wellenende liegen', () => {
  const sim = makeSim({ seedText: 'MINES2' });
  const t = buildNearPath(sim, 'minelayer');
  sim.startWave();
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) e.alive = false;
  const anchor = farEnemy(sim, t);
  steps(sim, 6);
  const mine = sim.s.zones.find((z) => z.kind === 'mine' && z.armed <= 0 && z.t > 0)!;
  assert.ok(mine, 'scharfe Mine');
  const fly = addEnemy(sim, 'flyer', mine.x, mine.y);
  fly.hp = fly.maxHp = 1e6;
  fly.speed = 0;
  sim.step();
  assert.ok(mine.t > 0, 'Flieger lösen nicht aus');
  fly.alive = false;
  const g = addEnemy(sim, 'brute', mine.x, mine.y);
  g.hp = g.maxHp = 1e6;
  g.speed = 0;
  sim.step();
  assert.equal(mine.t, 0, 'explodiert');
  assert.ok(g.hp < 1e6, 'Schaden');
  // Wellenende: scharfe Minen bleiben
  g.alive = false;
  const left = sim.s.zones.filter((z) => z.kind === 'mine' && z.armed <= 0 && z.t > 0).length;
  assert.ok(left > 0);
  killEnemy(sim, anchor, 0);
  sim.s.enemies = sim.s.enemies.filter((e) => e.alive);
  steps(sim, 0.5);
  assert.equal(sim.s.phase, 'build');
  assert.equal(sim.s.zones.filter((z) => z.kind === 'mine').length, left);
});

test('Fusion Minenwerfer + Sporenwerfer = Sporenminenfeld: Explosion vergiftet und hinterlässt Sporenwolke', () => {
  const sim = makeSim({ seedText: 'SPOREMINE' });
  sim.s.gold = 1e6;
  const a = buildNearPath(sim, 'minelayer');
  let b: TowerInstance | undefined;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
    if (sim.build('spore', a.cx + dx, a.cy + dy).ok) { b = sim.towerAt(a.cx + dx, a.cy + dy); break; }
  }
  assert.ok(b);
  for (const t of [a, b!]) {
    while (t.level < 4) assert.ok(sim.upgrade(t.uid).ok);
    assert.ok(sim.specialize(t.uid, 1).ok);
  }
  assert.ok(sim.fuse(a.uid, b!.uid).ok);
  assert.equal(sim.c.towerByUid.get(a.uid)?.def, 'sporemine');
  sim.startWave();
  sim.s.spawnQueue = [];
  for (const e of sim.s.enemies) e.alive = false;
  farEnemy(sim, a);
  steps(sim, 4);
  const mine = sim.s.zones.find((z) => z.kind === 'mine' && z.armed <= 0 && z.t > 0)!;
  assert.ok(mine);
  const g = addEnemy(sim, 'brute', mine.x, mine.y);
  g.hp = g.maxHp = 1e6;
  g.speed = 0;
  sim.step();
  assert.ok(g.statuses.some((x) => x.id === 'poison'), 'vergiftet');
  assert.ok(sim.s.zones.some((z) => z.kind === 'poisonCloud' && z.src === a.uid), 'Sporenwolke');
});
