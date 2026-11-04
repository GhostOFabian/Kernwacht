import assert from 'node:assert/strict';
import { test } from 'node:test';
import { TOWERS } from '../src/config/towers.ts';
import { Cell } from '../src/core/types.ts';
import { nextUpgradeCost, upgradeAllCost } from '../src/sim/EconomySystem.ts';
import { laserPenalty } from '../src/sim/TowerSystem.ts';
import { spawnsOf } from '../src/sim/WorldGenerator.ts';
import { buildNearPath, makeSim } from './helpers.ts';

test('Alles aufwerten: bis Stufe 4, ohne Spezialisierung, günstigste zuerst, nur solange das Gold reicht', () => {
  const sim = makeSim({ seedText: 'UPALL' });
  const a = buildNearPath(sim, 'ember');
  const b = buildNearPath(sim, 'frost', 3);
  const c = buildNearPath(sim, 'ranger', 6);
  // Genug Gold für alles
  sim.s.gold = 1e5;
  const full = upgradeAllCost(sim);
  assert.equal(full.steps, 9);
  const before = sim.s.gold;
  const r = sim.upgradeAll();
  assert.ok(r.ok);
  for (const t of [a, b, c]) {
    assert.equal(t.level, 4);
    assert.equal(t.spec, 0, 'keine automatische Spezialisierung');
  }
  assert.equal(before - sim.s.gold, full.total);
  assert.equal(sim.upgradeAll().ok, false, 'nichts mehr zu tun');
  // Knappes Gold: verteilt sich, kein Minus
  const sim2 = makeSim({ seedText: 'UPALL2' });
  const x = buildNearPath(sim2, 'ember');
  const y = buildNearPath(sim2, 'frost', 3);
  const cx = nextUpgradeCost(x)!;
  const cy = nextUpgradeCost(y)!;
  sim2.s.gold = cx + cy + 5;
  assert.ok(sim2.upgradeAll().ok);
  assert.equal(x.level, 2);
  assert.equal(y.level, 2);
  assert.ok(sim2.s.gold >= 0 && sim2.s.gold < Math.min(nextUpgradeCost(x)!, nextUpgradeCost(y)!));
});

test('Alles aufwerten für eine Turmsorte (Gruppenauswahl) lässt andere Türme unberührt', () => {
  const sim = makeSim({ seedText: 'UPGROUP' });
  const a = buildNearPath(sim, 'ember');
  const b = buildNearPath(sim, 'ember', 3);
  const o = buildNearPath(sim, 'frost', 6);
  sim.s.gold = 1e5;
  assert.ok(sim.upgradeAll([a.uid, b.uid]).ok);
  assert.equal(a.level, 4);
  assert.equal(b.level, 4);
  assert.equal(o.level, 1);
});

test('Kristalllaser: Nachbarlaser und Rissnähe dämpfen den Schaden', () => {
  const sim = makeSim({ seedText: 'LASERNERF' });
  sim.s.gold = 1e5;
  const m = sim.s.map;
  const sp = spawnsOf(m)[0];
  // Ein einzelner Laser weit weg vom Riss
  const far = buildNearPath(sim, 'laser', 30);
  const pFar = laserPenalty(sim, far);
  const solo = (sp.x - far.cx) ** 2 + (sp.y - far.cy) ** 2 > 9;
  if (solo) assert.equal(pFar.mul, 1);
  const dmgSolo = sim.stats(far).damage;
  // Zwei weitere Laser direkt daneben
  let added = 0;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1]]) {
    if (added >= 2) break;
    if (sim.build('laser', far.cx + dx, far.cy + dy).ok) added++;
  }
  assert.equal(added, 2);
  const p = laserPenalty(sim, far);
  assert.equal(p.neighbors, 2);
  assert.ok(sim.stats(far).damage < dmgSolo * 0.65, 'gestapelte Laser schwächer');
  // Am Riss
  let near: ReturnType<typeof sim.towerAt>;
  for (let r = 1; r <= 3 && !near; r++) for (let dy = -r; dy <= r && !near; dy++) for (let dx = -r; dx <= r && !near; dx++) {
    const x = sp.x + dx, y = sp.y + dy;
    if (x < 0 || y < 0 || x >= m.width || y >= m.height || m.cells[y * m.width + x] !== Cell.Open) continue;
    if (sim.build('laser', x, y).ok) near = sim.towerAt(x, y);
  }
  assert.ok(near, 'Bauplatz am Riss');
  if (near) assert.ok(laserPenalty(sim, near).nearRift);
  assert.ok(TOWERS.laser.base.damage! < 3.5, 'Grundschaden gesenkt');
});
