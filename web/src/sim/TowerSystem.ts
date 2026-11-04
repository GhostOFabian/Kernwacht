import { runPassive } from '../config/passiveTree.ts';
import { ECONOMY, SIM } from '../config/balance.ts';
import { EVENT_RANGE_MUL, TERRAIN_EVENTS } from '../config/biomes.ts';
import { RESONANCES } from '../config/elements.ts';
import { ZERO_STATS } from '../config/stats.ts';
import { footprintCells, RIFT_ENEMY_COOLDOWN, TOWERS } from '../config/towers.ts';
import { Cell, type EnemyInstance, type HitPayload, type StatMod, type Stats, type TargetPriority, type TowerActiveId, type TowerInstance } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { applyHit, dealDamage } from './DamageSystem.ts';
import { addStatus, hasStatus } from './ElementSystem.ts';
import { interruptEnemy, riftTeleport } from './EnemySystem.ts';
import { activeState, collectIncomeFor, triggerActive } from './EconomySystem.ts';
import { spawnsOf } from './WorldGenerator.ts';
import { MASTERY, masteryRank } from '../config/mastery.ts';
import { towerRuneMods } from './RuneSystem.ts';
import { starMods } from '../config/goldsinks.ts';
import { focusBonus } from '../config/scaling.ts';
import { countedTowers, currentTowerLimit } from './EconomySystem.ts';

// Turmsystem: Werteberechnung (datengetrieben), Zielauswahl und Angriffsverhalten.

export function applyMods(st: Stats, mods: readonly StatMod[]): void {
  for (const m of mods) {
    if (m.add !== undefined) st[m.stat] += m.add;
    if (m.mul !== undefined) st[m.stat] *= m.mul;
  }
}

/** Berechnet die effektiven Werte eines Turms (Stufen, Spezialisierung, Resonanzen, Modifikatoren, Terrain, Netzwerk). */
export function computeTowerStats(sim: GameSimulation, t: TowerInstance, includeNetwork = true): Stats {
  const def = TOWERS[t.def];
  const st = { ...ZERO_STATS, ...def.base } as Stats;
  for (let i = 0; i < t.level - 1 && i < def.upgrades.length; i++) applyMods(st, def.upgrades[i].mods);
  if (t.spec > 0) applyMods(st, def.specs[t.spec - 1].mods);
  // 3.6: Veredelung
  if (t.stars) applyMods(st, starMods(t.def, t.stars));
  // Runen (Helden-Endlos 2.7)
  if (sim.s.he?.runes?.length) applyMods(st, towerRuneMods(sim, t));
  let bonus = 0;
  if (includeNetwork) {
    const res = sim.c.resonanceByTower.get(t.uid);
    if (res) for (const id of res) {
      const r = RESONANCES.find((x) => x.id === id);
      if (r) applyMods(st, r.mods);
    }
    bonus = sim.c.networkBonusByTower.get(t.uid) ?? 0;
    // Globale Puffer: Netzflut (eigenes Netz) und Lied der Resonanz (alle Netze) verdoppeln den Netzwerkbonus.
    if (bonus > 0) {
      const gi = sim.c.linked.get(t.uid);
      const doubled = sim.s.buffs.some((b) => b.id === 'resoSong' || (b.id === 'surge' && gi !== undefined && sim.c.linked.get(b.src) === gi));
      if (doubled) bonus *= 2;
    }
  }
  const mods = sim.c.mods;
  const s = sim.s;
  const idx = t.cy * s.map.width + t.cx;
  const type = def.damageType;
  // Totem-Aura (stärkstes Totem in Reichweite, kein Stapeln) und Helden-Auren
  if (def.attack !== 'aura') {
    let aDmg = 0;
    let aSpd = 0;
    for (const o of s.towers) {
      if (o.def !== 'totem' || o.stunned > 0) continue;
      const os = computeTotemStats(sim, o);
      if ((o.cx - t.cx) ** 2 + (o.cy - t.cy) ** 2 > os.range * os.range) continue;
      aDmg = Math.max(aDmg, os.auraDmg);
      aSpd = Math.max(aSpd, os.auraSpeed);
    }
    const ha = sim.c.heroAura.get(t.uid);
    if (ha) {
      aDmg += ha.dmg;
      aSpd += ha.speed;
      st.range += ha.range;
    }
    // Helden-Endlosmodus: Turmverstärkungen des Helden (bereits gedeckelt in rebuildChampionBuffs)
    const pf = runPassive(s.he);
    if (pf.towerDmgPct || pf.towerSpeedPct || pf.towerRange) {
      aDmg += pf.towerDmgPct;
      aSpd += pf.towerSpeedPct;
      st.range += pf.towerRange;
    }
    // Turm-Meisterschaft: +1 % Schaden je Rang dieses Turmtyps
    const mr = masteryRank(s.he?.mastery?.[t.def] ?? 0);
    if (mr > 0) aDmg += MASTERY.perRank * mr;
    // Paragon-Turmbaum
    if (pf.towerCrit) st.critChance += pf.towerCrit;
    if (pf.towerPierce) st.armorPierce = Math.min(0.9, st.armorPierce + pf.towerPierce);
    if (pf.towerSplashPct && st.splash > 0) st.splash *= 1 + pf.towerSplashPct;
    const cb = sim.c.champBuff.get(t.uid);
    if (cb) {
      aDmg += cb.dmg;
      aSpd += cb.speed;
      st.range += cb.range;
      st.critChance += cb.crit;
    }
    st.damageMul *= 1 + aDmg;
    st.attackSpeedMul *= 1 + aSpd;
    if (t.forged > 0) {
      st.damageMul *= 1.35;
      st.range += 0.5;
    }
    if (t.activeT > 0) applyActiveBuff(st, def.active);
    for (const b of s.buffs) {
      if (b.id === 'hymn') st.attackSpeedMul *= 1 + b.power;
      if (b.id === 'worldweb') st.damageMul *= 1 + b.power;
    }
  }
  let dmgMul = st.damageMul * mods.allDamage * (type ? mods.dmgType[type] : 1);
  // 3.3 Fokusbonus: wenige Türme sind stärker (nur Endlos, nur kämpfende Türme)
  if (s.rules.endless && def.attack !== 'economy') dmgMul *= 1 + focusBonus(countedTowers(sim), currentTowerLimit(sim));
  if (s.map.ley.includes(idx) || s.zones.some((z) => z.kind === 'leyNode' && z.armed <= 0 && z.t > 0 && (z.x - t.cx - 0.5) ** 2 + (z.y - t.cy - 0.5) ** 2 <= z.r * z.r)) dmgMul *= ECONOMY.leyDamageMul;
  const ev = s.phase === 'combat' && s.currentWave?.event ? TERRAIN_EVENTS[s.currentWave.event] : null;
  if (ev && type) dmgMul *= ev.damageMul[type] ?? 1;
  if (t.def !== 'pylon') dmgMul *= 1 + bonus;
  if (def.attack === 'laser') dmgMul *= laserPenalty(sim, t).mul;
  st.damage *= dmgMul;
  st.burnDps *= dmgMul * (type === 'fire' ? 1 : mods.dmgType.fire);
  st.poisonDps *= dmgMul;
  st.bleedDps *= dmgMul;
  st.zapDamage *= mods.allDamage * mods.dmgType.lightning;
  if (st.range > 0) {
    const hg = s.map.heights[idx] >= 2 ? ECONOMY.highgroundRangeAdd : 0;
    const evMul = s.phase === 'combat' && s.currentWave?.event ? EVENT_RANGE_MUL[s.currentWave.event] ?? 1 : 1;
    st.range = (st.range + hg) * st.rangeMul * mods.rangeMul * (def.attack === 'aura' ? 1 : evMul);
  }
  st.cooldown = st.cooldown / Math.max(0.1, st.attackSpeedMul * mods.attackSpeedMul * (t.def !== 'pylon' ? 1 + bonus : 1));
  if (type === 'lightning' && st.damage > 0) st.chains += mods.chainsAdd;
  st.chillDur *= mods.chillDurMul;
  st.freezeDur *= mods.chillDurMul;
  st.poisonMaxStacks += mods.poisonStacksAdd;
  st.poisonDur *= mods.poisonDurMul;
  if (st.markPct > 0) st.markPct += mods.markAdd;
  if (st.income > 0) {
    st.income *= mods.incomeMul * (1 + runPassive(s.he).mineIncomePct);
    if (footprintCells(t.def, t.cx, t.cy, s.map.width).some((i) => s.map.veins.includes(i))) st.income *= s.rules.veinMul;
  }
  return st;
}

function applyActiveBuff(st: Stats, id: TowerActiveId | undefined): void {
  if (id === 'overdrive') st.attackSpeedMul *= 2;
  else if (id === 'overload') {
    st.chains += 4;
    st.damageMul *= 1.5;
  }
}

/** Totemwerte ohne Auren (verhindert Rekursion). */
function computeTotemStats(sim: GameSimulation, t: TowerInstance): Stats {
  const cached = sim.c.towerStats.get(t.uid);
  if (cached && t.def === 'totem') return cached;
  const def = TOWERS[t.def];
  const st = { ...ZERO_STATS, ...def.base } as Stats;
  for (let i = 0; i < t.level - 1 && i < def.upgrades.length; i++) applyMods(st, def.upgrades[i].mods);
  if (t.spec > 0) applyMods(st, def.specs[t.spec - 1].mods);
  return st;
}

export function recomputeAllStats(sim: GameSimulation): void {
  sim.c.towerStats.clear();
  for (const t of sim.s.towers) sim.c.towerStats.set(t.uid, computeTowerStats(sim, t, true));
}

export function canHit(st: Stats, e: EnemyInstance): boolean {
  if (!e.alive || e.stealthed || e.burrowed) return false;
  return e.flying ? st.hitsAir > 0 : st.hitsGround > 0;
}

function inRange(t: TowerInstance, st: Stats, e: EnemyInstance): boolean {
  const dx = e.x - (t.cx + 0.5);
  const dy = e.y - (t.cy + 0.5);
  const r = st.range + 0.15;
  return dx * dx + dy * dy <= r * r;
}

function priorityScore(p: TargetPriority, t: TowerInstance, e: EnemyInstance): number {
  switch (p) {
    case 'first': return -e.traveled;
    case 'exit': return e.remaining;
    case 'strong': return -(e.hp + e.shield);
    case 'weak': return e.hp + e.shield;
    case 'close': return (e.x - t.cx - 0.5) ** 2 + (e.y - t.cy - 0.5) ** 2;
    default: return 0;
  }
}

/** Wählt bis zu n Ziele nach Priorität. Deterministisch (UID als Gleichstandsauflösung). */
export function pickTargets(sim: GameSimulation, t: TowerInstance, st: Stats, n: number, filter?: (e: EnemyInstance) => boolean): EnemyInstance[] {
  const cands: EnemyInstance[] = [];
  for (const e of sim.s.enemies) {
    if (!canHit(st, e) || !inRange(t, st, e)) continue;
    if (filter && !filter(e)) continue;
    cands.push(e);
  }
  if (cands.length === 0) return cands;
  // 3.3: Drachenhorst/Phönix greifen Flieger in Reichweite zuerst an
  if (st.preferAir > 0 && cands.some((e) => e.flying)) {
    const air = cands.filter((e) => e.flying);
    cands.length = 0;
    cands.push(...air);
  }
  if (t.priority === 'random') {
    for (let i = cands.length - 1; i > 0; i--) {
      const j = Math.floor(sim.rand() * (i + 1));
      [cands[i], cands[j]] = [cands[j], cands[i]];
    }
    return cands.slice(0, n);
  }
  if (n === 1) {
    let best = cands[0];
    let bs = priorityScore(t.priority, t, best);
    for (let i = 1; i < cands.length; i++) {
      const sc = priorityScore(t.priority, t, cands[i]);
      if (sc < bs || (sc === bs && cands[i].uid < best.uid)) {
        best = cands[i];
        bs = sc;
      }
    }
    return [best];
  }
  return cands
    .map((e) => ({ e, s: priorityScore(t.priority, t, e) }))
    .sort((a, b) => a.s - b.s || a.e.uid - b.e.uid)
    .slice(0, n)
    .map((x) => x.e);
}

export function towerPayload(t: TowerInstance, st: Stats): HitPayload {
  return payload(t, st);
}

function payload(t: TowerInstance, st: Stats): HitPayload {
  const def = TOWERS[t.def];
  return { damage: st.damage, type: def.damageType ?? 'physical', splash: st.splash, stats: st, tower: t.uid, towerDef: t.def };
}

/**
 * Turmfähigkeiten lösen ab Stufe 2 automatisch aus, sobald die Abklingzeit abgelaufen ist und
 * sie sinnvoll wirken (Gegner in Reichweite; Goldrausch/Netzflut während jeder Welle).
 * Manuelles Auslösen (C) bleibt möglich, verändert aber nichts an der Abklingzeit.
 */
export function autoTriggerActives(sim: GameSimulation): void {
  const s = sim.s;
  if (!s.enemies.some((e) => e.alive)) return;
  for (const t of s.towers) {
    const d = TOWERS[t.def];
    if (!d.active || t.activeCd > 0 || t.stunned > 0) continue;
    if (!activeState(sim, t).available) continue;
    if (d.active !== 'goldrush' && d.active !== 'surge') {
      const st = sim.stats(t);
      const r = Math.max(1.5, st.range);
      const x = t.cx + 0.5;
      const y = t.cy + 0.5;
      if (!s.enemies.some((e) => e.alive && !e.stealthed && !e.burrowed && (e.x - x) ** 2 + (e.y - y) ** 2 <= r * r)) continue;
    }
    triggerActive(sim, t.uid);
  }
}

/** 3.8: Lane-Barrikaden verlangsamen Bodengegner, die über sie laufen. */
function updateLaneBarricades(sim: GameSimulation): void {
  const s = sim.s;
  for (const t of s.towers) {
    if (t.def !== 'lanebarricade') continue;
    const st = sim.stats(t);
    const r2 = st.range * st.range;
    const cx = t.cx + 0.5;
    const cy = t.cy + 0.5;
    for (const e of s.enemies) {
      if (!e.alive || e.flying || e.burrowed) continue;
      if ((e.x - cx) ** 2 + (e.y - cy) ** 2 > r2) continue;
      const c = addStatus(e, 'chill', st.chillDur, st.chillPct, t.uid, 1);
      c.flag = Math.max(c.flag, Math.round(st.chillPct * 100));
    }
  }
}

export function updateTowers(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  if (sim.pendingActives.length) {
    const list = sim.pendingActives;
    sim.pendingActives = [];
    for (const a of list) {
      const t = sim.c.towerByUid.get(a.uid);
      if (t) executeActive(sim, t, a.id);
    }
  }
  updateTeslaArcs(sim, dt);
  if (s.phase === 'combat' && s.tick % 3 === 0) updateLaneBarricades(sim);
  if (s.phase === 'combat' && s.tick % 6 === 0) autoTriggerActives(sim);
  for (const t of s.towers) {
    t.fireAnim = Math.max(0, t.fireAnim - dt);
    if (t.activeCd > 0) t.activeCd = Math.max(0, t.activeCd - dt);
    if (t.activeT > 0) {
      t.activeT -= dt;
      if (t.activeT <= 0) {
        t.activeT = 0;
        sim.c.statsDirty = true;
      }
    }
    if (t.forged > 0) {
      t.forged -= dt;
      if (t.forged <= 0) {
        t.forged = 0;
        sim.c.statsDirty = true;
      }
    }
    if (t.def === 'dragon' || t.def === 'phoenix') t.orbit += dt * 1.7;
    if (t.disrupted > 0) {
      t.disrupted -= dt;
      if (t.disrupted <= 0) {
        t.disrupted = 0;
        sim.c.networkDirty = true;
      }
    }
    if (t.stunned > 0) {
      t.stunned = Math.max(0, t.stunned - dt);
      continue;
    }
    const def = TOWERS[t.def];
    if (def.attack === 'economy' || def.attack === 'none' || def.attack === 'aura') continue;
    const st = sim.stats(t);
    if (st.damage <= 0 && def.attack === 'pylon') continue;
    t.cooldown = Math.max(0, t.cooldown - dt);
    t.retarget -= dt;
    let target = t.target ? sim.c.enemyByUid.get(t.target) : undefined;
    if (!target || !canHit(st, target) || !inRange(t, st, target) || t.retarget <= 0) {
      t.retarget = SIM.retargetInterval;
      const picked = pickTargets(sim, t, st, 1);
      target = picked[0];
      t.target = target ? target.uid : 0;
    }
    if (target) t.aim = Math.atan2(target.y - (t.cy + 0.5), target.x - (t.cx + 0.5));
    if (def.attack === 'laser' && (!target || target.uid !== t.rampTarget)) {
      t.charge = 0;
      t.rampTarget = target ? target.uid : 0;
    }
    if (t.cooldown > 0) continue;
    const fired = fire(sim, t, st, target);
    if (fired) {
      t.cooldown = st.cooldown;
      t.fireAnim = 0.18;
      t.shots++;
    }
  }
}

function fire(sim: GameSimulation, t: TowerInstance, st: Stats, target: EnemyInstance | undefined): boolean {
  const def = TOWERS[t.def];
  const ox = t.cx + 0.5;
  const oy = t.cy + 0.5;
  switch (def.attack) {
    case 'mines':
      return throwMines(sim, t, st, ox, oy);
    case 'projectile': {
      if (!target) return false;
      const targets = st.multishot > 1 ? pickTargets(sim, t, st, Math.round(st.multishot)) : [target];
      for (const e of targets) {
        const d = Math.hypot(e.x - ox, e.y - oy) || 1;
        sim.s.projectiles.push({
          uid: sim.uid(), x: ox, y: oy, z: 0.9, tx: e.x, ty: e.y, target: e.uid, speed: st.projectileSpeed || 10,
          payload: payload(t, st), kind: t.def, pierceLeft: Math.round(st.pierce),
          dirX: (e.x - ox) / d, dirY: (e.y - oy) / d, traveled: 0, maxTravel: st.range + 1.5, hitList: [], alive: true,
        });
        sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: e.x, ty: e.y, uid: t.uid });
      }
      return true;
    }
    case 'pulse': {
      const hits = pickTargets(sim, t, st, 999);
      if (hits.length === 0) return false;
      const p = payload(t, st);
      for (const e of hits) applyHit(sim, e, p);
      sim.emit({ k: 'pulse', tower: t.def, x: ox, y: oy, r: st.range });
      return true;
    }
    case 'chain':
    case 'pylon': {
      if (!target) return false;
      const p = payload(t, st);
      const hit = new Set<number>();
      const pts: number[] = [ox, oy];
      let cur: EnemyInstance | undefined = target;
      let mult = 1;
      let jumps = Math.round(st.chains) + (hasStatus(target, 'wet') ? 1 : 0);
      while (cur) {
        hit.add(cur.uid);
        pts.push(cur.x, cur.y);
        const cx = cur.x;
        const cy = cur.y;
        applyHit(sim, cur, p, mult);
        if (jumps-- <= 0) break;
        mult *= st.chainFalloff;
        let next: EnemyInstance | undefined;
        let bd = st.chainRange * st.chainRange;
        for (const e of sim.s.enemies) {
          if (!e.alive || hit.has(e.uid) || !canHit(st, e)) continue;
          const d = (e.x - cx) ** 2 + (e.y - cy) ** 2;
          if (d < bd) {
            bd = d;
            next = e;
          }
        }
        cur = next;
      }
      sim.emit({ k: 'chain', pts });
      sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: target.x, ty: target.y, uid: t.uid });
      return true;
    }
    case 'beam': {
      if (!target) return false;
      let mult = 1;
      if (st.rampPct > 0) {
        if (t.rampTarget === target.uid) t.rampStacks = Math.min(st.rampMax, t.rampStacks + 1);
        else {
          t.rampTarget = target.uid;
          t.rampStacks = 0;
        }
        mult = 1 + st.rampPct * t.rampStacks;
      }
      sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: target.x, ty: target.y, uid: t.uid });
      applyHit(sim, target, payload(t, st), mult);
      return true;
    }
    case 'rift': {
      if (!target) return false;
      const now = sim.s.time;
      const fresh = pickTargets(sim, t, st, Math.round(st.riftTargets), (e) => e.boss !== null || (e.riftCd <= now && !hasStatus(e, 'riftMark')));
      const targets = fresh.length > 0 ? fresh : [target];
      const p = payload(t, st);
      for (const e of targets) {
        sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: e.x, ty: e.y, uid: t.uid });
        applyHit(sim, e, p);
        if (!e.alive) continue;
        if (e.boss) {
          addStatus(e, 'tether', 4, Math.max(0.15, st.tetherPct), t.uid);
        } else if (e.riftCd <= now && !hasStatus(e, 'riftMark')) {
          addStatus(e, 'riftMark', st.riftDelay, st.riftBack, t.uid);
          e.riftCd = now + RIFT_ENEMY_COOLDOWN;
          if (st.rootDur > 0) addStatus(e, 'root', st.rootDur, 1, t.uid);
          if (st.tetherPct > 0) addStatus(e, 'tether', 4, st.tetherPct, t.uid);
        }
      }
      return true;
    }
    case 'meteor': {
      if (!target) return false;
      const n = Math.max(1, Math.round(st.meteorCount));
      const targets = n > 1 ? pickTargets(sim, t, st, n) : [target];
      targets.forEach((e, i) => {
        const r = n > 1 ? st.splash * 0.8 : st.splash;
        sim.s.zones.push({ uid: sim.uid(), kind: 'meteor', x: e.x, y: e.y, r, t: 0.1, armed: st.meteorDelay + i * 0.25, power: st.damage, tick: 0, link: t.uid, src: t.uid });
        if (st.fragments > 0) {
          for (let k = 0; k < Math.round(st.fragments); k++) {
            const a = (k / st.fragments) * Math.PI * 2 + sim.rand();
            sim.s.zones.push({ uid: sim.uid(), kind: 'meteor', x: e.x + Math.cos(a) * 1.3, y: e.y + Math.sin(a) * 1.3, r: 0.8, t: 0.1, armed: st.meteorDelay + 0.35 + k * 0.12, power: st.damage * 0.4, tick: 0, link: t.uid, src: t.uid });
          }
        }
      });
      sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: target.x, ty: target.y, uid: t.uid });
      return true;
    }
    case 'tesla': {
      if (!target) return false;
      const p = payload(t, st);
      const pts: number[] = [ox, oy];
      const hit = new Set<number>();
      let cur: EnemyInstance | undefined = target;
      let jumps = Math.round(st.chains);
      while (cur) {
        hit.add(cur.uid);
        pts.push(cur.x, cur.y);
        const cx = cur.x;
        const cy = cur.y;
        applyHit(sim, cur, p);
        if (jumps-- <= 0) break;
        let next: EnemyInstance | undefined;
        let bd = 2.2 * 2.2;
        for (const e of sim.s.enemies) {
          if (!canHit(st, e) || hit.has(e.uid)) continue;
          const d = (e.x - cx) ** 2 + (e.y - cy) ** 2;
          if (d < bd) {
            bd = d;
            next = e;
          }
        }
        cur = next;
      }
      sim.emit({ k: 'chain', pts });
      sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: target.x, ty: target.y, uid: t.uid });
      return true;
    }
    case 'gravity': {
      const hits = pickTargets(sim, t, st, 999);
      if (hits.length === 0) return false;
      const p = payload(t, st);
      const ground = hits.filter((e) => !e.flying && !e.boss);
      const sorted = [...ground].sort((a, b) => a.remaining - b.remaining || a.uid - b.uid);
      const median = sorted.length ? sorted[Math.floor(sorted.length / 2)].remaining : 0;
      for (const e of hits) {
        applyHit(sim, e, p);
        if (!e.alive) continue;
        addStatus(e, 'pull', 1, 1, t.uid);
        if (!e.flying && !e.boss && e.remaining < median - 0.5) riftTeleport(sim, e, Math.round(st.pullCells));
        if (st.rootDur > 0 && !e.boss) addStatus(e, 'root', st.rootDur, 1, t.uid);
      }
      sim.emit({ k: 'gravity', x: ox, y: oy, r: st.range });
      return true;
    }
    case 'dragon': {
      if (!target) return false;
      const p = payload(t, st);
      const dx = ox + Math.cos(t.orbit) * 0.9;
      const dy = oy + Math.sin(t.orbit) * 0.9;
      if (st.chains > 0) {
        const pts: number[] = [dx, dy];
        const hit = new Set<number>();
        let cur: EnemyInstance | undefined = target;
        let jumps = Math.round(st.chains);
        while (cur) {
          hit.add(cur.uid);
          pts.push(cur.x, cur.y);
          const cx = cur.x;
          const cy = cur.y;
          applyHit(sim, cur, { ...p, type: 'lightning' });
          if (jumps-- <= 0) break;
          let next: EnemyInstance | undefined;
          let bd = 2.4 * 2.4;
          for (const e of sim.s.enemies) {
            if (!canHit(st, e) || hit.has(e.uid)) continue;
            const d = (e.x - cx) ** 2 + (e.y - cy) ** 2;
            if (d < bd) {
              bd = d;
              next = e;
            }
          }
          cur = next;
        }
        sim.emit({ k: 'chain', pts });
      } else {
        const r2 = st.splash * st.splash;
        for (const e of sim.s.enemies) {
          if (!canHit(st, e)) continue;
          if ((e.x - target.x) ** 2 + (e.y - target.y) ** 2 <= r2) applyHit(sim, e, p, e === target ? 1 : 0.8);
        }
        sim.emit({ k: 'breath', x: dx, y: dy, tx: target.x, ty: target.y });
      }
      return true;
    }
    case 'laser': {
      if (!target) return false;
      t.charge = Math.min(1, t.charge + st.cooldown / 3);
      const mult = 1 + (Math.max(1, st.laserRamp) - 1) * t.charge;
      const beams = [target, ...(st.multishot > 1 ? pickTargets(sim, t, st, Math.round(st.multishot)).filter((e) => e !== target).slice(0, Math.round(st.multishot) - 1) : [])];
      const p = payload(t, st);
      beams.forEach((tg, bi) => {
        const ddx = tg.x - ox;
        const ddy = tg.y - oy;
        const len = Math.hypot(ddx, ddy) || 1;
        const ux = ddx / len;
        const uy = ddy / len;
        const reach = st.range + 0.3;
        for (const e of sim.s.enemies) {
          if (!canHit(st, e)) continue;
          const rx = e.x - ox;
          const ry = e.y - oy;
          const along = rx * ux + ry * uy;
          if (along < 0 || along > reach) continue;
          const perp = Math.abs(rx * uy - ry * ux);
          if (perp > 0.38) continue;
          applyHit(sim, e, p, bi === 0 ? mult : mult * 0.75);
        }
      });
      if (sim.s.tick % 30 === 0) sim.emit({ k: 'laser', x: ox, y: oy, tx: target.x, ty: target.y, power: t.charge, uid: t.uid });
      return true;
    }
    default:
      return false;
  }
}

/** Lichtbögen zwischen Tesla-Spulen: Gegner, die den Bogen kreuzen, erleiden Blitzschaden. */
function updateTeslaArcs(sim: GameSimulation, dt: number): void {
  const teslas = sim.s.towers.filter((t) => (t.def === 'tesla' || t.def === 'thunderspire') && t.stunned <= 0);
  if (teslas.length < 2) return;
  for (let i = 0; i < teslas.length; i++) {
    const a = teslas[i];
    const sa = sim.stats(a);
    for (let j = i + 1; j < teslas.length; j++) {
      const b = teslas[j];
      const sb = sim.stats(b);
      const reach = Math.min(sa.arcRange, sb.arcRange);
      const ax = a.cx + 0.5, ay = a.cy + 0.5, bx = b.cx + 0.5, by = b.cy + 0.5;
      const vx = bx - ax, vy = by - ay;
      const len2 = vx * vx + vy * vy;
      if (len2 > reach * reach || len2 === 0) continue;
      const dps = (sa.arcDps + sb.arcDps) / 2;
      for (const e of sim.s.enemies) {
        if (!e.alive || e.stealthed || e.burrowed) continue;
        const k = Math.max(0, Math.min(1, ((e.x - ax) * vx + (e.y - ay) * vy) / len2));
        const px = ax + vx * k, py = ay + vy * k;
        if ((e.x - px) ** 2 + (e.y - py) ** 2 <= 0.45 * 0.45) dealDamage(sim, e, dps * dt, 'lightning', a.uid);
      }
    }
  }
}

/** Liefert Tesla-Paare mit aktivem Lichtbogen (für die Darstellung). */
export function teslaPairs(sim: GameSimulation): [TowerInstance, TowerInstance][] {
  const out: [TowerInstance, TowerInstance][] = [];
  const teslas = sim.s.towers.filter((t) => (t.def === 'tesla' || t.def === 'thunderspire') && t.stunned <= 0);
  for (let i = 0; i < teslas.length; i++) {
    for (let j = i + 1; j < teslas.length; j++) {
      const sa = sim.c.towerStats.get(teslas[i].uid);
      const sb = sim.c.towerStats.get(teslas[j].uid);
      if (!sa || !sb) continue;
      const reach = Math.min(sa.arcRange, sb.arcRange);
      if ((teslas[i].cx - teslas[j].cx) ** 2 + (teslas[i].cy - teslas[j].cy) ** 2 <= reach * reach) out.push([teslas[i], teslas[j]]);
    }
  }
  return out;
}

function executeActive(sim: GameSimulation, t: TowerInstance, id: TowerActiveId): void {
  const st = sim.stats(t);
  const ox = t.cx + 0.5;
  const oy = t.cy + 0.5;
  const range = Math.max(st.range, 2.5);
  const inRangeEnemies = sim.s.enemies.filter((e) => e.alive && !e.stealthed && !e.burrowed && (e.x - ox) ** 2 + (e.y - oy) ** 2 <= range * range);
  switch (id) {
    case 'deepfreeze':
      for (const e of inRangeEnemies) {
        if (e.boss) interruptEnemy(sim, e, 40);
        else addStatus(e, 'freeze', 2.5, 1, t.uid);
      }
      sim.emit({ k: 'pulse', tower: 'frost', x: ox, y: oy, r: range });
      break;
    case 'goldrush': {
      const g = collectIncomeFor(sim, t, 1.5);
      sim.emit({ k: 'gold', amount: g, x: ox, y: oy });
      break;
    }
    case 'rewind':
      for (const e of inRangeEnemies) if (!e.boss) riftTeleport(sim, e, 3);
      break;
    case 'meteorstorm': {
      const tg = inRangeEnemies.filter((e) => !e.flying);
      for (let i = 0; i < 5 && tg.length; i++) {
        const e = tg[Math.floor(sim.rand() * tg.length)];
        sim.s.zones.push({ uid: sim.uid(), kind: 'meteor', x: e.x + (sim.rand() - 0.5), y: e.y + (sim.rand() - 0.5), r: 1.3, t: 0.1, armed: 0.6 + i * 0.4, power: Math.max(60, st.damage), tick: 0, link: t.uid, src: t.uid });
      }
      break;
    }
    case 'nova': {
      const p = payload(t, st);
      p.damage = Math.max(st.damage, 25) * 4;
      if (!TOWERS[t.def].damageType) p.type = 'arcane';
      for (const e of inRangeEnemies) applyHit(sim, e, p);
      sim.emit({ k: 'pulse', tower: 'resonator', x: ox, y: oy, r: range });
      break;
    }
    case 'surge':
      sim.s.buffs.push({ id: 'surge', t: 8, power: 1, src: t.uid });
      sim.c.statsDirty = true;
      break;
    default:
      break;
  }
}

/**
 * Kristalllaser-Dämpfung (2.6): Mehrere Laser dicht beieinander stören sich gegenseitig
 * (−20 % je weiterem Laser in 3 Feldern, höchstens −60 %), und direkt am Riss (≤ 3 Felder zu einem
 * Gegnereingang) bricht die Rissenergie den Strahl (−35 %).
 */
export function laserPenalty(sim: GameSimulation, t: TowerInstance): { mul: number; neighbors: number; nearRift: boolean } {
  const s = sim.s;
  let neighbors = 0;
  for (const o of s.towers) {
    if (o === t || TOWERS[o.def].attack !== 'laser') continue;
    if ((o.cx - t.cx) ** 2 + (o.cy - t.cy) ** 2 <= LASER.interferenceRange ** 2) neighbors++;
  }
  const nearRift = spawnsOf(s.map).some((sp) => (sp.x - t.cx) ** 2 + (sp.y - t.cy) ** 2 <= LASER.riftRange ** 2);
  const mul = Math.max(1 - LASER.interferenceMax, 1 - LASER.interferencePer * neighbors) * (nearRift ? LASER.riftMul : 1);
  return { mul, neighbors, nearRift };
}

export const LASER = { interferenceRange: 3, interferencePer: 0.2, interferenceMax: 0.6, riftRange: 3, riftMul: 0.65 };

// ------------------------------------------------------------ Minenwerfer (2.8)

export const MINE = { flight: 0.55, trigger: 0.5, life: 60, spacing: 0.8 };

/** Anzahl der Minen (fliegend oder scharf), die ein Turm gerade im Feld hat. */
export function minesOf(sim: GameSimulation, uid: number): number {
  let n = 0;
  for (const z of sim.s.zones) if (z.kind === 'mine' && z.link === uid && (z.t > 0 || z.armed > 0)) n++;
  return n;
}

/**
 * Wirft Minen auf freie Lane-Felder in Reichweite – bevorzugt dorthin, wo Gegner gleich vorbeilaufen,
 * und mit Abstand zu bereits liegenden Minen. Nur solange Gegner unterwegs sind.
 */
function throwMines(sim: GameSimulation, t: TowerInstance, st: Stats, ox: number, oy: number): boolean {
  const s = sim.s;
  if (!s.enemies.some((e) => e.alive)) return false;
  const max = Math.round(st.mineMax);
  let have = minesOf(sim, t.uid);
  if (have >= max) return false;
  const map = s.map;
  const r = st.range;
  const cands: { x: number; y: number; score: number }[] = [];
  const x0 = Math.max(0, Math.floor(ox - r));
  const x1 = Math.min(map.width - 1, Math.floor(ox + r));
  const y0 = Math.max(0, Math.floor(oy - r));
  const y1 = Math.min(map.height - 1, Math.floor(oy + r));
  const mines = s.zones.filter((z) => z.kind === 'mine' && (z.t > 0 || z.armed > 0));
  for (let y = y0; y <= y1; y++) {
    for (let x = x0; x <= x1; x++) {
      const i = y * map.width + x;
      const c = map.cells[i];
      const lane = c === Cell.Path || (map.variant === 'mazing' && (c === Cell.Open || c === Cell.NoBuild) && !sim.c.towerByCell.has(i));
      if (!lane) continue;
      const cx = x + 0.5;
      const cy = y + 0.5;
      if ((cx - ox) ** 2 + (cy - oy) ** 2 > r * r) continue;
      if (map.variant === 'mazing' && sim.c.field[i] >= 1e8) continue;
      let near = Infinity;
      for (const m of mines) near = Math.min(near, (m.x - cx) ** 2 + (m.y - cy) ** 2);
      if (near < MINE.spacing * MINE.spacing) continue;
      // Gegner, die auf das Feld zulaufen: kleiner Abstand → höhere Priorität
      let foe = Infinity;
      for (const e of s.enemies) if (e.alive && !e.flying) foe = Math.min(foe, (e.x - cx) ** 2 + (e.y - cy) ** 2);
      cands.push({ x: cx, y: cy, score: Math.sqrt(Math.min(foe, 64)) + sim.rand() * 1.5 });
    }
  }
  if (!cands.length) return false;
  cands.sort((a, b) => a.score - b.score);
  const n = Math.max(1, Math.round(st.multishot));
  let thrown = 0;
  for (const c of cands) {
    if (thrown >= n || have >= max) break;
    if (thrown > 0 && s.zones.some((z) => z.kind === 'mine' && z.link === t.uid && (z.x - c.x) ** 2 + (z.y - c.y) ** 2 < MINE.spacing * MINE.spacing)) continue;
    const jx = (sim.rand() - 0.5) * 0.3;
    const jy = (sim.rand() - 0.5) * 0.3;
    s.zones.push({ uid: sim.uid(), kind: 'mine', x: c.x + jx, y: c.y + jy, r: MINE.trigger, t: MINE.life, armed: MINE.flight + thrown * 0.12, power: st.damage, tick: 0, link: t.uid, src: t.uid });
    thrown++;
    have++;
  }
  if (!thrown) return false;
  sim.emit({ k: 'shot', tower: t.def, x: ox, y: oy, tx: cands[0].x, ty: cands[0].y, uid: t.uid });
  t.aim = Math.atan2(cands[0].y - oy, cands[0].x - ox);
  return true;
}

/** Scharfe Mine: explodiert, sobald ein Bodengegner darauf tritt. */
export function updateMine(sim: GameSimulation, z: import('../core/types.ts').Zone): void {
  const s = sim.s;
  let trig = false;
  for (const e of s.enemies) {
    if (!e.alive || e.flying || e.burrowed) continue;
    if ((e.x - z.x) ** 2 + (e.y - z.y) ** 2 <= z.r * z.r) {
      trig = true;
      break;
    }
  }
  if (!trig) return;
  z.t = 0;
  const t = sim.c.towerByUid.get(z.link);
  const st = t ? sim.stats(t) : undefined;
  if (t && st) {
    const p = payload(t, st);
    const rad = Math.max(0.6, st.splash);
    const r2 = rad * rad;
    for (const e of s.enemies) {
      if (!e.alive || e.flying) continue;
      const d2 = (e.x - z.x) ** 2 + (e.y - z.y) ** 2;
      if (d2 <= r2) applyHit(sim, e, p, d2 < r2 * 0.3 ? 1 : 0.75);
    }
    if (st.cloudDur > 0 && st.cloudRadius > 0) {
      s.zones.push({ uid: sim.uid(), kind: 'poisonCloud', x: z.x, y: z.y, r: st.cloudRadius, t: st.cloudDur, armed: 0, power: st.poisonDps, tick: 0, link: 0, src: t.uid });
    }
    sim.emit({ k: 'hit', type: TOWERS[t.def].damageType ?? 'physical', x: z.x, y: z.y, amount: st.damage, splash: rad });
  } else {
    // Turm verkauft: die Mine explodiert trotzdem mit ihrer gespeicherten Ladung
    for (const e of s.enemies) if (e.alive && !e.flying && (e.x - z.x) ** 2 + (e.y - z.y) ** 2 <= 1) dealDamage(sim, e, z.power, 'physical', 0);
    sim.emit({ k: 'hit', type: 'physical', x: z.x, y: z.y, amount: z.power, splash: 1 });
  }
  sim.emit({ k: 'burst', x: z.x, y: z.y, r: t && st ? Math.max(0.6, st.splash) : 1 });
}
