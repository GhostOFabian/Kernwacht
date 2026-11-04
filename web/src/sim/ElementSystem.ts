import { REACTION_BY_ID, STATUS_RULES } from '../config/elements.ts';
import { CHALLENGE, FLYER } from '../config/scaling.ts';
import { HERO_RULES } from '../config/heroes.ts';
import type { DamageType, EnemyInstance, StatusId, StatusInstance } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { dealDamage } from './DamageSystem.ts';
import { riftTeleport } from './EnemySystem.ts';

// Statuseffekte und Elementarreaktionen. Siehe config/elements.ts für die Kombinationsregeln.

export function getStatus(e: EnemyInstance, id: StatusId): StatusInstance | undefined {
  for (const s of e.statuses) if (s.id === id) return s;
  return undefined;
}

export function hasStatus(e: EnemyInstance, id: StatusId): boolean {
  return getStatus(e, id) !== undefined;
}

export function removeStatus(e: EnemyInstance, id: StatusId): StatusInstance | undefined {
  const i = e.statuses.findIndex((s) => s.id === id);
  if (i < 0) return undefined;
  return e.statuses.splice(i, 1)[0];
}

/**
 * Fügt einen Status hinzu bzw. kombiniert ihn mit einem bestehenden.
 * maxStacks > 1: Stapel werden addiert (bis Maximum), Dauer erneuert, stärkere Power übernommen.
 * maxStacks = 1: stärkere Power gewinnt, Dauer = Maximum aus alt und neu.
 */
export function addStatus(e: EnemyInstance, id: StatusId, t: number, power: number, src: number, maxStacks = 1, flag = 0): StatusInstance {
  // 3.1: Challenge-Bosse widerstehen Kontrolleffekten
  if (e.challenge && (id === 'chill' || id === 'freeze' || id === 'stun' || id === 'root')) t *= CHALLENGE.ccMul;
  const cur = getStatus(e, id);
  if (cur) {
    if (maxStacks > 1) {
      cur.stacks = Math.min(maxStacks, cur.stacks + 1);
      cur.t = Math.max(cur.t, t);
      if (power >= cur.power) {
        cur.power = power;
        cur.src = src;
      }
    } else {
      if (power > cur.power) {
        cur.power = power;
        cur.src = src;
        cur.flag = flag;
      }
      cur.t = Math.max(cur.t, t);
    }
    return cur;
  }
  const s: StatusInstance = { id, t, stacks: 1, power, src, flag };
  e.statuses.push(s);
  return s;
}

/** Gesamtverlangsamung durch Statuseffekte (hart begrenzt). */
export function slowFactor(e: EnemyInstance): number {
  let slow = 0;
  const chill = getStatus(e, 'chill');
  if (chill) slow += Math.min(chill.power * chill.stacks, chill.flag > 0 ? chill.flag / 100 : STATUS_RULES.chillHardCap);
  const poison = getStatus(e, 'poison');
  if (poison && poison.flag > 0) slow += Math.min(STATUS_RULES.poisonSlowCap, poison.flag / 100);
  // 3.12: Flieger werden stärker verlangsamt (Netze, Kälte)
  if (e.flying) slow *= FLYER.slowMul;
  return Math.min(STATUS_RULES.chillHardCap, slow);
}

export function isImmobile(e: EnemyInstance): boolean {
  return hasStatus(e, 'freeze') || hasStatus(e, 'stun') || hasStatus(e, 'root');
}

export function canAct(e: EnemyInstance): boolean {
  return !hasStatus(e, 'freeze') && !hasStatus(e, 'stun') && !hasStatus(e, 'silence');
}

function reactionReady(sim: GameSimulation, e: EnemyInstance, id: string): boolean {
  const def = REACTION_BY_ID[id];
  const cd = e.reactCds[id] ?? 0;
  if (cd > sim.s.time) return false;
  e.reactCds[id] = sim.s.time + def.cooldown;
  sim.s.stats.reactions[id] = (sim.s.stats.reactions[id] ?? 0) + 1;
  if (sim.s.heroes.length) sim.s.riftEnergy = Math.min(HERO_RULES.maxEnergy, sim.s.riftEnergy + HERO_RULES.energyPerReaction * sim.c.mods.reactionEnergyMul);
  sim.emit({ k: 'reaction', id, x: e.x, y: e.y });
  return true;
}

/**
 * Prüft Elementarreaktionen für einen eingehenden Treffer und gibt den angepassten Schaden zurück.
 * Reaktionsschaden selbst wird mit noReact ausgeführt – keine Ketten möglich.
 */
export function react(sim: GameSimulation, e: EnemyInstance, type: DamageType, dmg: number, towerUid: number): number {
  let out = dmg;
  if (type === 'fire') {
    if (hasStatus(e, 'chill') && reactionReady(sim, e, 'steam')) {
      removeStatus(e, 'chill');
      addStatus(e, 'wet', STATUS_RULES.wetDur, 1, towerUid);
      out *= STATUS_RULES.steamMul;
    }
    const poison = getStatus(e, 'poison');
    if (poison && reactionReady(sim, e, 'toxicBlaze')) {
      removeStatus(e, 'poison');
      const burst = poison.power * poison.stacks * poison.t;
      const r2 = STATUS_RULES.toxicBlazeRadius * STATUS_RULES.toxicBlazeRadius;
      for (const o of sim.s.enemies) {
        if (!o.alive) continue;
        const dx = o.x - e.x;
        const dy = o.y - e.y;
        if (dx * dx + dy * dy <= r2) dealDamage(sim, o, o === e ? burst : burst * 0.6, 'poison', poison.src);
      }
    }
  } else if (type === 'frost') {
    const burn = getStatus(e, 'burn');
    if (burn && reactionReady(sim, e, 'thermalShock')) {
      removeStatus(e, 'burn');
      dealDamage(sim, e, burn.power * burn.t, 'frost', towerUid);
      addStatus(e, 'brittle', 3, STATUS_RULES.brittleBonus, towerUid);
    }
  } else if (type === 'lightning') {
    if (hasStatus(e, 'wet') && reactionReady(sim, e, 'overload')) out *= STATUS_RULES.overloadMul;
  } else if (type === 'sonic') {
    if (hasStatus(e, 'freeze') && reactionReady(sim, e, 'shatter')) {
      removeStatus(e, 'freeze');
      addStatus(e, 'freezeImmune', STATUS_RULES.freezeImmuneDur, 1, 0);
      out = out * STATUS_RULES.shatterMul + e.maxHp * STATUS_RULES.shatterMaxHpPct;
    }
  }
  return out;
}

/** Vergeht Zeit für Statuseffekte; verursacht Schaden über Zeit. */
export function tickStatuses(sim: GameSimulation, e: EnemyInstance, dt: number): void {
  for (const st of e.statuses) {
    if (!e.alive) return;
    if (st.id === 'burn') dealDamage(sim, e, st.power * dt, 'fire', st.src);
    else if (st.id === 'poison') dealDamage(sim, e, st.power * st.stacks * dt, 'poison', st.src);
    else if (st.id === 'shred') dealDamage(sim, e, st.power * st.stacks * dt, 'physical', st.src);
    st.t -= dt;
  }
  if (!e.alive) return;
  for (let i = e.statuses.length - 1; i >= 0; i--) {
    const st = e.statuses[i];
    if (st.t > 0) continue;
    e.statuses.splice(i, 1);
    if (st.id === 'freeze') addStatus(e, 'freezeImmune', STATUS_RULES.freezeImmuneDur, 1, 0);
    if (st.id === 'riftMark') onRiftMarkExpired(sim, e, st);
  }
}

function onRiftMarkExpired(sim: GameSimulation, e: EnemyInstance, st: StatusInstance): void {
  riftTeleport(sim, e, st.power);
}
