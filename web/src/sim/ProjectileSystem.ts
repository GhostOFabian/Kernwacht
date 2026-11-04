import { ENEMIES } from '../config/enemies.ts';
import type { EnemyInstance, Projectile } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { applyHit } from './DamageSystem.ts';

// Projektile. Stirbt das Ziel unterwegs, fliegt das Projektil zur letzten bekannten Position:
// Flächengeschosse explodieren dort, Einzelgeschosse verfallen ohne Wirkung.

export function updateProjectiles(sim: GameSimulation, dt: number): void {
  for (const p of sim.s.projectiles) {
    if (!p.alive) continue;
    if (p.pierceLeft > 0) updatePiercing(sim, p, dt);
    else updateHoming(sim, p, dt);
  }
}

function updateHoming(sim: GameSimulation, p: Projectile, dt: number): void {
  const target = sim.c.enemyByUid.get(p.target);
  if (target && target.alive) {
    p.tx = target.x;
    p.ty = target.y;
  }
  const dx = p.tx - p.x;
  const dy = p.ty - p.y;
  const d = Math.hypot(dx, dy);
  const step = p.speed * dt;
  p.traveled += Math.min(step, d);
  if (d > step && p.traveled < 40) {
    p.x += (dx / d) * step;
    p.y += (dy / d) * step;
    return;
  }
  p.x = p.tx;
  p.y = p.ty;
  p.alive = false;
  impact(sim, p, target && target.alive ? target : undefined);
}

function impact(sim: GameSimulation, p: Projectile, target: EnemyInstance | undefined): void {
  const pl = p.payload;
  const st = pl.stats;
  if (pl.splash > 0) {
    const r2 = pl.splash * pl.splash;
    for (const e of sim.s.enemies) {
      if (!e.alive || (e.flying && st.hitsAir <= 0)) continue;
      const d2 = (e.x - p.x) ** 2 + (e.y - p.y) ** 2;
      if (d2 > r2) continue;
      applyHit(sim, e, pl, e === target ? 1 : 0.75);
    }
  } else if (target) {
    const ev = target.boss ? 0 : ENEMIES[target.def].evasion ?? 0;
    if (ev > 0 && sim.rand() < ev) sim.emit({ k: 'immune', x: target.x, y: target.y });
    else applyHit(sim, target, pl);
  }
  sim.emit({ k: 'hit', type: pl.type, x: p.x, y: p.y, amount: pl.damage, splash: pl.splash });
  if (st.fragments > 0) {
    const n = Math.round(st.fragments);
    for (let i = 0; i < n; i++) {
      const a = (i / n) * Math.PI * 2 + sim.rand() * 0.8;
      const fx = p.x + Math.cos(a) * 0.95;
      const fy = p.y + Math.sin(a) * 0.95;
      for (const e of sim.s.enemies) {
        if (!e.alive || e.flying) continue;
        if ((e.x - fx) ** 2 + (e.y - fy) ** 2 <= 0.7 * 0.7) applyHit(sim, e, pl, 0.4);
      }
      sim.emit({ k: 'hit', type: pl.type, x: fx, y: fy, amount: pl.damage * 0.4, splash: 0.7 });
    }
  }
  if (st.cloudDur > 0 && st.cloudRadius > 0) {
    sim.s.zones.push({
      uid: sim.uid(), kind: 'poisonCloud', x: p.x, y: p.y, r: st.cloudRadius, t: st.cloudDur, armed: 0,
      power: st.poisonDps, tick: 0, link: 0, src: pl.tower,
    });
  }
}

function updatePiercing(sim: GameSimulation, p: Projectile, dt: number): void {
  const step = p.speed * dt;
  const sub = Math.max(1, Math.ceil(step / 0.25));
  for (let k = 0; k < sub && p.alive; k++) {
    p.x += p.dirX * (step / sub);
    p.y += p.dirY * (step / sub);
    p.traveled += step / sub;
    for (const e of sim.s.enemies) {
      if (!e.alive || e.flying || p.hitList.includes(e.uid)) continue;
      if ((e.x - p.x) ** 2 + (e.y - p.y) ** 2 > 0.42 * 0.42) continue;
      p.hitList.push(e.uid);
      applyHit(sim, e, p.payload);
      sim.emit({ k: 'hit', type: p.payload.type, x: e.x, y: e.y, amount: p.payload.damage, splash: 0 });
      if (p.hitList.length >= p.pierceLeft) {
        p.alive = false;
        break;
      }
    }
    if (p.traveled >= p.maxTravel || p.x < -1 || p.y < -1 || p.x > sim.s.map.width + 1 || p.y > sim.s.map.height + 1) p.alive = false;
  }
}
