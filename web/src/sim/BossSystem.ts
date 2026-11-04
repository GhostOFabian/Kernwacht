import { ABILITIES, BOSSES } from '../config/enemies.ts';
import { TOWERS } from '../config/towers.ts';
import { Cell, type AbilityId, type AbilityState, type EnemyInstance, type ZoneKind } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { nextCell, tracePath } from './Pathfinding.ts';
import { spawnEnemyAt } from './EnemySystem.ts';
import { hasStatus } from './ElementSystem.ts';

// Bosslogik: Phasen, angekündigte Fähigkeiten (Telegraphs) und Unterbrechung über Standfestigkeit.
// Wirkungen mit Zielgebiet laufen über Zonen (ZoneSystem), die während der Ankündigung sichtbar sind.

export function initBossAbilities(sim: GameSimulation, e: EnemyInstance): void {
  if (!e.boss) return;
  const ph = BOSSES[e.boss].phases[0];
  e.abilities = ph.abilities.map((id) => ({ id, cd: 4, windup: 0, tx: 0, ty: 0, r: 0 }));
  void sim;
}

export function onBossShieldBroken(sim: GameSimulation, e: EnemyInstance): void {
  if (!e.boss) return;
  const phases = BOSSES[e.boss].phases;
  const next = phases[e.phase + 1];
  if (next && next.trigger === 'shieldBroken') enterPhase(sim, e, e.phase + 1);
  else sim.emit({ k: 'message', text: 'Schild gebrochen!', severity: 'info' });
}

function enterPhase(sim: GameSimulation, e: EnemyInstance, idx: number): void {
  if (!e.boss) return;
  const def = BOSSES[e.boss];
  const ph = def.phases[idx];
  e.phase = idx;
  const old = new Map<AbilityId, AbilityState>(e.abilities.map((a) => [a.id, a]));
  e.abilities = ph.abilities.map((id) => old.get(id) ?? { id, cd: id === 'reShield' ? 0.5 : 2.5, windup: 0, tx: 0, ty: 0, r: 0 });
  if (sim.s.boss && sim.s.boss.uid === e.uid) {
    sim.s.boss.phase = idx;
    sim.s.boss.phaseName = ph.name;
  }
  sim.emit({ k: 'bossPhase', boss: e.boss, phase: idx, name: ph.name });
  sim.emit({ k: 'message', text: `${def.name}: ${ph.name}`, severity: 'warn' });
  if (ph.abilities.includes('ashFury')) {
    e.abilityMul = 0.6;
    sim.emit({ k: 'ability', id: 'ashFury', x: e.x, y: e.y, stage: 'cast' });
  }
}

export function updateBosses(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  if (s.boss) {
    const be = sim.c.enemyByUid.get(s.boss.uid);
    if (!be || !be.alive) s.boss = null;
  }
  for (const e of s.enemies) {
    if (!e.alive || !e.boss) continue;
    const def = BOSSES[e.boss];
    // Zwillingsbund: nah beieinander heilen sich die Zwillinge gegenseitig.
    if (def.partner) {
      const p = s.enemies.find((o) => o.alive && o.boss === def.partner);
      if (p && (p.x - e.x) ** 2 + (p.y - e.y) ** 2 <= 9) e.hp = Math.min(e.maxHp, e.hp + e.maxHp * 0.02 * dt);
    }
    // Phasenwechsel über HP-Schwellen (Schild-Auslöser separat)
    const next = def.phases[e.phase + 1];
    if (next && !next.trigger && e.hp / e.maxHp <= next.hpBelow) enterPhase(sim, e, e.phase + 1);
    const stunned = hasStatus(e, 'stun');
    for (const a of e.abilities) {
      const ad = ABILITIES[a.id];
      if (a.windup > 0) {
        a.windup -= dt;
        if (a.windup <= 0) {
          a.windup = 0;
          a.cd = ad.cooldown * e.abilityMul;
          castBoss(sim, e, a);
        }
        continue;
      }
      if (stunned) continue;
      a.cd -= dt;
      if (a.cd <= 0) beginBoss(sim, e, a);
    }
  }
}

function telegraph(sim: GameSimulation, e: EnemyInstance, id: AbilityId, x: number, y: number, r: number, t: number): void {
  if (!e.boss) return;
  sim.s.telegraphs.push({
    uid: sim.uid(), text: `${BOSSES[e.boss].name}: ${ABILITIES[id].name}`, x, y, r, t: Math.max(t, 1.5), total: Math.max(t, 1.5), severity: 'danger',
  });
  sim.emit({ k: 'ability', id, x, y, stage: 'warn' });
}

function zone(sim: GameSimulation, e: EnemyInstance, kind: ZoneKind, x: number, y: number, r: number, armed: number, t: number, power: number): number {
  const uid = sim.uid();
  sim.s.zones.push({ uid, kind, x, y, r, t, armed, power, tick: 0, link: 0, src: e.uid });
  return uid;
}

function beginBoss(sim: GameSimulation, e: EnemyInstance, a: AbilityState): void {
  const ad = ABILITIES[a.id];
  const map = sim.s.map;
  switch (a.id) {
    case 'riftJump': {
      let cx = e.nx;
      let cy = e.ny;
      for (let i = 0; i < 5; i++) {
        const n = nextCell(map, sim.c.field, cx, cy);
        if (!n) break;
        cx = n.x;
        cy = n.y;
      }
      if (cx === map.goal.x && cy === map.goal.y) {
        a.cd = 3; // Kein Sprung direkt in den Kern
        return;
      }
      zone(sim, e, 'riftJump', cx + 0.5, cy + 0.5, 0.8, ad.windup, 0.3, 0);
      a.tx = cx + 0.5;
      a.ty = cy + 0.5;
      telegraph(sim, e, a.id, a.tx, a.ty, 0.8, ad.windup);
      break;
    }
    case 'riftPortals': {
      const path = tracePath(map, sim.c.field, map.spawn);
      if (path.length < 10) {
        a.cd = 5;
        return;
      }
      const pin = path[Math.min(3, path.length - 1)];
      const pout = path[Math.floor(path.length * 0.55)];
      const inUid = zone(sim, e, 'portalIn', pin.x + 0.5, pin.y + 0.5, 0.6, ad.windup, 12, 0);
      const outUid = zone(sim, e, 'portalOut', pout.x + 0.5, pout.y + 0.5, 0.6, ad.windup, 12, 0);
      const zin = sim.s.zones.find((z) => z.uid === inUid);
      const zout = sim.s.zones.find((z) => z.uid === outUid);
      if (zin && zout) {
        zin.link = outUid;
        zout.link = inUid;
      }
      telegraph(sim, e, a.id, pout.x + 0.5, pout.y + 0.5, 0.6, ad.windup);
      break;
    }
    case 'summonBrood':
      telegraph(sim, e, a.id, e.x, e.y, 1, ad.windup);
      break;
    case 'fireZones': {
      const towers = sim.s.towers.filter((t) => t.def !== 'goldmine');
      if (towers.length === 0) {
        a.cd = 3;
        return;
      }
      const scored = towers
        .map((t) => ({ t, n: towers.filter((o) => (o.cx - t.cx) ** 2 + (o.cy - t.cy) ** 2 <= 2.6).length }))
        .sort((p, q) => q.n - p.n || p.t.uid - q.t.uid);
      const picks = [scored[0].t];
      const second = scored.find((p) => (p.t.cx - picks[0].cx) ** 2 + (p.t.cy - picks[0].cy) ** 2 > 9);
      if (second) picks.push(second.t);
      for (const t of picks) {
        zone(sim, e, 'fireZone', t.cx + 0.5, t.cy + 0.5, 1.6, ad.windup, 5, 5);
        telegraph(sim, e, a.id, t.cx + 0.5, t.cy + 0.5, 1.6, ad.windup);
      }
      break;
    }
    case 'nullPulse':
      zone(sim, e, 'nullPulse', e.x, e.y, 4, ad.windup, 0.5, 6);
      telegraph(sim, e, a.id, e.x, e.y, 4, ad.windup);
      break;
    case 'reShield':
      if (e.reshieldUsed) {
        a.cd = 999;
        return;
      }
      telegraph(sim, e, a.id, e.x, e.y, 1, ad.windup);
      break;
    case 'ashFury':
    case 'twinBond':
      a.cd = 999;
      return;
    case 'devour': {
      let best: (typeof sim.s.towers)[number] | null = null;
      let bd = 3.0 * 3.0;
      for (const t of sim.s.towers) {
        if (TOWERS[t.def].attack === 'none') continue;
        const d = (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2;
        if (d < bd) {
          bd = d;
          best = t;
        }
      }
      if (!best) {
        a.cd = 1.5;
        return;
      }
      a.tx = best.cx + 0.5;
      a.ty = best.cy + 0.5;
      a.r = best.uid;
      zone(sim, e, 'devour', a.tx, a.ty, 0.8, ad.windup, 0.3, 0);
      telegraph(sim, e, a.id, a.tx, a.ty, 0.8, ad.windup);
      break;
    }
    case 'stomp':
      zone(sim, e, 'stomp', e.x, e.y, 2.8, ad.windup, 0.4, 4);
      telegraph(sim, e, a.id, e.x, e.y, 2.8, ad.windup);
      break;
    default:
      a.cd = ad.cooldown;
      return;
  }
  a.windup = ad.windup;
}

function castBoss(sim: GameSimulation, e: EnemyInstance, a: AbilityState): void {
  sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'cast' });
  if (a.id === 'summonBrood') {
    for (let i = 0; i < 4; i++) {
      const c = spawnEnemyAt(sim, 'runner', false, e);
      c.x += ((i % 2) - 0.5) * 0.3;
      c.y += (Math.floor(i / 2) - 0.5) * 0.3;
    }
  } else if (a.id === 'devour') {
    const s = sim.s;
    const t = sim.c.towerByUid.get(a.r);
    if (t && (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= 3.6 * 3.6) {
      s.towers = s.towers.filter((x) => x.uid !== t.uid);
      t.target = 0;
      t.stunned = 0;
      e.swallowed.push(t);
      sim.reindex();
      sim.c.networkDirty = true;
      sim.c.statsDirty = true;
      if (s.map.variant === 'mazing') sim.c.fieldDirty = true;
      sim.emit({ k: 'devour', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5 });
      sim.emit({ k: 'message', text: `Der Schlund hat ${TOWERS[t.def].name} verschlungen! Besiege ihn, um ihn zurückzuholen.`, severity: 'danger' });
    }
  } else if (a.id === 'reShield') {
    e.reshieldUsed = true;
    e.maxShield = e.maxHp * 0.2;
    e.shield = e.maxShield;
    sim.emit({ k: 'message', text: 'Der Nullhüter erneuert seinen Schild!', severity: 'warn' });
  }
  // riftJump, riftPortals, fireZones, nullPulse wirken über ihre Zonen (ZoneSystem).
}

/** Tod eines Bosses: verschlungene Türme zurückgeben, Zwilling gerät in Raserei. */
export function onBossDeath(sim: GameSimulation, e: EnemyInstance): void {
  const s = sim.s;
  if (!e.boss) return;
  if (e.swallowed.length > 0) {
    let refund = 0;
    for (const t of e.swallowed) {
      const i = t.cy * s.map.width + t.cx;
      if (!sim.c.towerByCell.has(i) && s.map.cells[i] === Cell.Open) {
        t.stunned = 0;
        s.towers.push(t);
      } else {
        refund += Math.floor(t.invested * 0.7);
      }
    }
    e.swallowed = [];
    s.gold += refund;
    sim.reindex();
    sim.c.networkDirty = true;
    sim.c.statsDirty = true;
    if (s.map.variant === 'mazing') sim.c.fieldDirty = true;
    sim.emit({ k: 'message', text: `Die verschlungenen Türme sind zurück${refund ? ` (+${refund} Gold für blockierte Felder)` : ''}!`, severity: 'info' });
  }
  const partner = BOSSES[e.boss].partner;
  if (partner) {
    const p = s.enemies.find((o) => o.alive && o.boss === partner);
    if (p && !p.enraged) {
      p.enraged = true;
      p.hp = Math.min(p.maxHp, p.hp + p.maxHp * 0.3);
      sim.emit({ k: 'message', text: `${BOSSES[partner].name} gerät in Raserei!`, severity: 'danger' });
    }
  }
}
