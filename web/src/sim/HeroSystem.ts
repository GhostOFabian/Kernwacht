import { ZERO_STATS } from '../config/stats.ts';
import { DUOS, HERO_RULES, HEROES, heroStats } from '../config/heroes.ts';
import { TOWERS } from '../config/towers.ts';
import { Cell, type EnemyInstance, type GlobalBuffId, type HeroId, type HeroInstance, type HeroMode, type HeroStats, type HitPayload, type Stats } from '../core/types.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { applyHit, dealDamage } from './DamageSystem.ts';
import { addStatus } from './ElementSystem.ts';
import { enemyContactDps, interruptEnemy } from './EnemySystem.ts';
import { isWalkableCell } from './Pathfinding.ts';

// Heldensystem. Helden werden mit Rissenergie auf begehbaren Wegzellen eingesetzt, kämpfen
// eine begrenzte Zeit und brauchen danach eine Abklingzeit. Unterstützer verstärken Türme über
// Auren, die – wie alle Turmwerte – nur bei Änderung neu berechnet werden (Signatur-Vergleich).

export interface HeroAura { dmg: number; speed: number; range: number }

export function heroStatsOf(sim: GameSimulation, h: HeroInstance): HeroStats {
  const st = heroStats(HEROES[h.id], h.perks);
  if (duoActive(sim, 'emberley') && h.id === 'hunter') st.damage *= 1.5;
  if (duoActive(sim, 'goldenbard') && h.id === 'prospector') st.goldBonus += 0.5;
  return st;
}

function active(h: HeroInstance): boolean {
  return h.state === 'active';
}

export function duoActive(sim: GameSimulation, id: string): boolean {
  const d = DUOS.find((x) => x.id === id);
  if (!d) return false;
  const a = sim.s.heroes.find((h) => h.id === d.a && active(h));
  const b = sim.s.heroes.find((h) => h.id === d.b && active(h));
  if (!a || !b) return false;
  return (a.x - b.x) ** 2 + (a.y - b.y) ** 2 <= HERO_RULES.duoRadius ** 2;
}

export function activeDuos(sim: GameSimulation): string[] {
  return DUOS.filter((d) => duoActive(sim, d.id)).map((d) => d.id);
}

function visible(e: EnemyInstance): boolean {
  return e.alive && !e.stealthed && !e.burrowed;
}

function dist2(ax: number, ay: number, bx: number, by: number): number {
  return (ax - bx) ** 2 + (ay - by) ** 2;
}

// ------------------------------------------------------------ Befehle

export function deployHero(sim: GameSimulation, id: HeroId, cx: number, cy: number): CommandResult {
  const s = sim.s;
  const h = s.heroes.find((x) => x.id === id);
  if (!h) return { ok: false, reason: 'Held nicht in der Auswahl' };
  if (s.phase !== 'build' && s.phase !== 'combat') return { ok: false, reason: 'Jetzt nicht möglich' };
  if (h.state === 'active') return { ok: false, reason: 'Held ist bereits im Einsatz' };
  if (h.state !== 'ready') return { ok: false, reason: `Abklingzeit: noch ${Math.ceil(h.cooldown)} s` };
  const def = HEROES[id];
  if (s.riftEnergy < def.cost) return { ok: false, reason: `Zu wenig Rissenergie (${def.cost} benötigt)` };
  const chk = canPlaceHero(sim, cx, cy);
  if (!chk.ok) return chk;
  const st = heroStatsOf(sim, h);
  s.riftEnergy -= def.cost;
  h.state = 'active';
  h.mode = 'hold';
  h.x = cx + 0.5;
  h.y = cy + 0.5;
  h.ax = h.x;
  h.ay = h.y;
  h.hp = st.maxHp;
  h.activeLeft = st.activeTime;
  h.target = 0;
  h.buffT = 0;
  h.buffKind = '';
  s.stats.heroDeploys++;
  sim.c.statsDirty = true;
  sim.emit({ k: 'heroDeploy', hero: id, x: h.x, y: h.y });
  return { ok: true };
}

export function canPlaceHero(sim: GameSimulation, cx: number, cy: number): CommandResult {
  const map = sim.s.map;
  if (cx < 0 || cy < 0 || cx >= map.width || cy >= map.height) return { ok: false, reason: 'Außerhalb der Karte' };
  const i = cy * map.width + cx;
  if (map.cells[i] === Cell.Goal) return { ok: false, reason: 'Nicht direkt am Kern' };
  if (!isWalkableCell(map, i) || sim.c.towerByCell.has(i)) return { ok: false, reason: 'Helden werden auf dem Weg eingesetzt' };
  return { ok: true };
}

export function setHeroMode(sim: GameSimulation, id: HeroId, mode: HeroMode): CommandResult {
  const h = sim.s.heroes.find((x) => x.id === id);
  if (!h || !active(h)) return { ok: false, reason: 'Held ist nicht im Einsatz' };
  h.mode = mode;
  return { ok: true };
}

export function moveHeroAnchor(sim: GameSimulation, id: HeroId, cx: number, cy: number): CommandResult {
  const h = sim.s.heroes.find((x) => x.id === id);
  if (!h || !active(h)) return { ok: false, reason: 'Held ist nicht im Einsatz' };
  const chk = canPlaceHero(sim, cx, cy);
  if (!chk.ok) return chk;
  h.ax = cx + 0.5;
  h.ay = cy + 0.5;
  if (h.mode === 'retreat') h.mode = 'hold';
  return { ok: true };
}

export function chooseHeroPerk(sim: GameSimulation, id: HeroId, choice: 0 | 1): CommandResult {
  const h = sim.s.heroes.find((x) => x.id === id);
  if (!h || !h.pendingPerk) return { ok: false, reason: 'Keine Auswahl offen' };
  const before = heroStatsOf(sim, h);
  h.perks.push(choice);
  h.pendingPerk = h.perks.length < h.level - 1;
  const after = heroStatsOf(sim, h);
  if (after.maxHp > before.maxHp) h.hp += after.maxHp - before.maxHp;
  h.activeLeft += Math.max(0, after.activeTime - before.activeTime);
  sim.c.statsDirty = true;
  return { ok: true };
}

export function abilityReady(sim: GameSimulation, h: HeroInstance, slot: 0 | 1 | 2): { ok: boolean; reason: string | null } {
  if (!active(h)) return { ok: false, reason: 'Nicht im Einsatz' };
  if (sim.s.phase !== 'combat') return { ok: false, reason: 'Nur während einer Welle' };
  if (slot === 2) return h.ult >= 100 ? { ok: true, reason: null } : { ok: false, reason: `Ultimate lädt (${Math.floor(h.ult)} %)` };
  if (h.abilityCd[slot] > 0) return { ok: false, reason: `Bereit in ${Math.ceil(h.abilityCd[slot])} s` };
  return { ok: true, reason: null };
}

export function useHeroAbility(sim: GameSimulation, id: HeroId, slot: 0 | 1 | 2): CommandResult {
  const h = sim.s.heroes.find((x) => x.id === id);
  if (!h) return { ok: false, reason: 'Held nicht gefunden' };
  const r = abilityReady(sim, h, slot);
  if (!r.ok) return { ok: false, reason: r.reason ?? 'Nicht bereit' };
  const def = HEROES[h.id];
  const st = heroStatsOf(sim, h);
  const ab = slot === 2 ? def.ultimate : def.abilities[slot];
  const done = executeAbility(sim, h, ab.id, st);
  if (!done.ok) return done;
  if (slot === 2) h.ult = 0;
  else h.abilityCd[slot] = ab.cooldown * st.abilityCdMul;
  h.anim = 0.6;
  return { ok: true };
}

// ------------------------------------------------------------ Wirtschaft & Erfahrung

/** Goldfaktor eines Abschusses durch Heldeneffekte (Prospektorin, Goldfieber). */
export function heroGoldMul(sim: GameSimulation, e: EnemyInstance): number {
  let mul = 1;
  for (const h of sim.s.heroes) {
    if (h.id !== 'prospector' || !active(h)) continue;
    const st = heroStatsOf(sim, h);
    if (dist2(h.x, h.y, e.x, e.y) <= st.auraRadius ** 2) mul += st.goldBonus;
  }
  if (sim.s.buffs.some((b) => b.id === 'goldFever')) mul *= 2;
  return mul;
}

export function addEnergy(sim: GameSimulation, amount: number): void {
  if (sim.s.heroes.length === 0) return;
  sim.s.riftEnergy = Math.min(HERO_RULES.maxEnergy, sim.s.riftEnergy + amount);
}

export function onEnemyKilled(sim: GameSimulation, e: EnemyInstance, killerUid: number): void {
  const s = sim.s;
  if (s.heroes.length === 0) return;
  addEnergy(sim, e.boss ? HERO_RULES.energyPerBoss : e.elite ? HERO_RULES.energyPerElite : HERO_RULES.energyPerKill);
  const xp = e.boss ? 120 : Math.max(2, e.reward * 1.5);
  for (const h of s.heroes) {
    if (!active(h)) continue;
    const killer = h.uid === killerUid;
    if (killer) {
      h.kills++;
      s.stats.heroKills++;
      h.ult = Math.min(100, h.ult + HERO_RULES.ultPerKill * ultMul(sim, h));
    }
    if (killer || dist2(h.x, h.y, e.x, e.y) <= HERO_RULES.xpRadius ** 2) grantXp(sim, h, killer ? xp : xp * 0.6);
  }
}

function ultMul(sim: GameSimulation, h: HeroInstance): number {
  return (h.id === 'riftwalker' || h.id === 'silence') && duoActive(sim, 'voidpact') ? 2 : 1;
}

function grantXp(sim: GameSimulation, h: HeroInstance, amount: number): void {
  if (h.level >= HERO_RULES.maxLevel) return;
  h.xp += amount;
  while (h.level < HERO_RULES.maxLevel && h.xp >= HERO_RULES.xpPerLevel[h.level]) {
    h.level++;
    h.pendingPerk = true;
    sim.emit({ k: 'heroLevel', hero: h.id, level: h.level });
    sim.emit({ k: 'message', text: `${HEROES[h.id].name} erreicht Stufe ${h.level} – wähle eine Verbesserung!`, severity: 'info' });
  }
}

/** Wellenende: Energie und verkürzte Abklingzeiten. */
export function onWaveCleared(sim: GameSimulation): void {
  addEnergy(sim, HERO_RULES.energyPerWave);
  for (const h of sim.s.heroes) {
    if (h.state === 'cooldown' || h.state === 'down') {
      h.cooldown = Math.max(0, h.cooldown - 10);
      if (h.cooldown <= 0) h.state = 'ready';
    }
  }
}

// ------------------------------------------------------------ Takt

export function updateHeroes(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  if (s.heroes.length === 0) return;
  const combat = s.phase === 'combat';
  for (const e of s.enemies) e.blockedBy = 0;
  for (const b of s.buffs) b.t -= dt;
  if (s.buffs.some((b) => b.t <= 0)) {
    s.buffs = s.buffs.filter((b) => b.t > 0);
    sim.c.statsDirty = true;
  }
  for (const h of s.heroes) {
    if (h.state === 'cooldown' || h.state === 'down') {
      if (combat) h.cooldown -= dt;
      if (h.cooldown <= 0) {
        h.cooldown = 0;
        h.state = 'ready';
      }
      continue;
    }
    if (!active(h)) continue;
    const st = heroStatsOf(sim, h);
    h.anim = Math.max(0, h.anim - dt);
    if (combat) {
      h.activeLeft -= dt;
      h.ult = Math.min(100, h.ult + HERO_RULES.ultPerSecond * dt * ultMul(sim, h) * (HEROES[h.id].support ? 1.6 : 1));
      for (let k = 0; k < 2; k++) h.abilityCd[k] = Math.max(0, h.abilityCd[k] - dt);
      if (h.buffT > 0) {
        h.buffT -= dt;
        if (h.buffT <= 0) h.buffKind = '';
      }
    }
    if (h.activeLeft <= 0) {
      retire(sim, h, HERO_RULES.cooldown);
      continue;
    }
    moveHero(sim, h, st, dt);
    if (!active(h)) continue;
    if (combat) {
      blockAndContact(sim, h, st, dt);
      if (!active(h)) continue;
      attack(sim, h, st, dt);
      passive(sim, h, st, dt);
    }
  }
  sim.c.heroAuraTimer -= dt;
  if (sim.c.heroAuraTimer <= 0) {
    sim.c.heroAuraTimer = 0.25;
    rebuildAuras(sim);
  }
}

function retire(sim: GameSimulation, h: HeroInstance, cooldown: number): void {
  h.state = 'cooldown';
  h.cooldown = cooldown * sim.c.mods.heroCooldownMul;
  h.target = 0;
  h.buffKind = '';
  h.buffT = 0;
  sim.c.statsDirty = true;
}

function moveHero(sim: GameSimulation, h: HeroInstance, st: HeroStats, dt: number): void {
  const s = sim.s;
  let tx = h.ax;
  let ty = h.ay;
  if (h.mode === 'retreat') {
    tx = s.map.goal.x + 0.5;
    ty = s.map.goal.y + 0.5;
    if (dist2(h.x, h.y, tx, ty) < 0.3) {
      retire(sim, h, HERO_RULES.cooldown * (1 - HERO_RULES.retreatRefund));
      return;
    }
  } else if (h.mode === 'advance' && s.phase === 'combat') {
    let best: EnemyInstance | null = null;
    let bd = 9 * 9;
    for (const e of s.enemies) {
      if (!visible(e) || e.flying) continue;
      const d = dist2(h.ax, h.ay, e.x, e.y);
      if (d < bd) {
        bd = d;
        best = e;
      }
    }
    if (best) {
      tx = best.x;
      ty = best.y;
      const stop = Math.max(0.55, Math.min(st.range, 3) - 0.2);
      if (dist2(h.x, h.y, tx, ty) <= stop * stop) return;
    }
  }
  const dx = tx - h.x;
  const dy = ty - h.y;
  const d = Math.hypot(dx, dy);
  if (d < 0.02) return;
  const step = Math.min(d, st.speed * dt * (h.blocking > 0 ? 0 : 1));
  h.x += (dx / d) * step;
  h.y += (dy / d) * step;
  h.facing = Math.atan2(dy, dx);
}

function blockAndContact(sim: GameSimulation, h: HeroInstance, st: HeroStats, dt: number): void {
  const s = sim.s;
  let blocked = 0;
  let incoming = 0;
  for (const e of s.enemies) {
    if (!visible(e) || e.flying) continue;
    const d = dist2(h.x, h.y, e.x, e.y);
    if (d > 0.7 * 0.7) continue;
    if (!e.boss && blocked < st.blockCount && e.blockedBy === 0) {
      e.blockedBy = h.uid;
      blocked++;
    }
    incoming += enemyContactDps(e);
  }
  h.blocking = blocked;
  if (h.buffKind === 'bulwark') return;
  let mul = 1 - Math.min(0.8, st.armor);
  if (h.id === 'blade' && duoActive(sim, 'ironwatch')) mul *= 0.5;
  if (incoming > 0) h.hp -= incoming * mul * dt;
  else h.hp = Math.min(st.maxHp, h.hp + st.maxHp * HERO_RULES.regenPct * dt);
  if (h.hp <= 0) {
    h.hp = 0;
    h.state = 'down';
    h.cooldown = HERO_RULES.downCooldown * sim.c.mods.heroCooldownMul;
    h.target = 0;
    h.buffKind = '';
    s.stats.heroDowns++;
    sim.c.statsDirty = true;
    for (const e of s.enemies) if (e.blockedBy === h.uid) e.blockedBy = 0;
    sim.emit({ k: 'heroDown', hero: h.id, x: h.x, y: h.y });
    sim.emit({ k: 'message', text: `${HEROES[h.id].name} ist gefallen! (${HERO_RULES.downCooldown} s Abklingzeit)`, severity: 'danger' });
    if (s.rules.heroMustSurvive === h.id && s.phase !== 'victory') {
      s.phase = 'defeat';
      sim.emit({ k: 'message', text: 'Mission gescheitert: der zu schützende Held ist gefallen.', severity: 'danger' });
      sim.emit({ k: 'defeat' });
    }
  }
}

function heroPayload(h: HeroInstance, st: HeroStats, splash: number): HitPayload {
  const def = HEROES[h.id];
  const stats = { ...ZERO_STATS } as Stats;
  stats.damage = st.damage;
  stats.hitsAir = st.range > 1.5 ? 1 : 0;
  if (h.id === 'hunter') {
    stats.burnDps = 6 + st.damage * 0.2;
    stats.burnDur = 3;
  } else if (h.id === 'weather') {
    stats.chillPct = 0.15;
    stats.chillDur = 2;
    stats.chillMax = 0.45;
  } else if (h.id === 'bard' || h.id === 'silence') {
    stats.interrupt = 10;
  }
  return { damage: st.damage, type: def.damageType, splash, stats, tower: h.uid, towerDef: 'ranger' };
}

function attack(sim: GameSimulation, h: HeroInstance, st: HeroStats, dt: number): void {
  h.attackCd = Math.max(0, h.attackCd - dt);
  if (h.attackCd > 0 || st.damage <= 0) return;
  const s = sim.s;
  let target = h.target ? sim.c.enemyByUid.get(h.target) : undefined;
  const r = st.range + 0.1;
  const canAir = st.range > 1.5;
  if (!target || !visible(target) || (target.flying && !canAir) || dist2(h.x, h.y, target.x, target.y) > r * r) {
    target = undefined;
    let bd = r * r;
    for (const e of s.enemies) {
      if (!visible(e) || (e.flying && !canAir)) continue;
      const d = dist2(h.x, h.y, e.x, e.y);
      // Blockierte bzw. dem Kern nahe Gegner bevorzugen
      const score = d - (e.blockedBy === h.uid ? 2 : 0) + e.remaining * 0.01;
      if (d <= r * r && score < bd) {
        bd = score;
        target = e;
      }
    }
    h.target = target ? target.uid : 0;
  }
  if (!target) return;
  const splash = h.buffKind === 'flame' || (h.id === 'hunter' && duoActive(sim, 'emberley')) ? 1 : 0;
  const p = heroPayload(h, st, splash);
  if (splash > 0) {
    for (const e of s.enemies) if (visible(e) && dist2(e.x, e.y, target.x, target.y) <= splash * splash) applyHit(sim, e, p, e === target ? 1 : 0.7);
  } else applyHit(sim, target, p);
  h.attackCd = st.attackCd;
  h.facing = Math.atan2(target.y - h.y, target.x - h.x);
  h.ult = Math.min(100, h.ult + HERO_RULES.ultPerHit * ultMul(sim, h));
  h.anim = Math.max(h.anim, 0.25);
  sim.emit({ k: 'heroAttack', hero: h.id, x: h.x, y: h.y, tx: target.x, ty: target.y, melee: st.range <= 1.5 });
}

/** Dauerhafte Wirkungen: Reparatur, Stille-Aura. */
function passive(sim: GameSimulation, h: HeroInstance, st: HeroStats, dt: number): void {
  const s = sim.s;
  if (h.buffKind === 'nulled') return;
  if (h.id === 'smith') {
    let changed = false;
    for (const t of s.towers) {
      if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) > st.auraRadius ** 2) continue;
      if (t.stunned > 0) {
        t.stunned = Math.max(0, t.stunned - dt * 3);
      }
      if (t.disrupted > 0) {
        t.disrupted = 0;
        changed = true;
      }
    }
    if (changed) sim.c.networkDirty = true;
  } else if (h.id === 'silence') {
    for (const e of s.enemies) {
      if (!e.alive || dist2(e.x, e.y, h.x, h.y) > st.auraRadius ** 2) continue;
      addStatus(e, 'reveal', 0.5, 1, h.uid);
      if (e.boss) interruptEnemy(sim, e, st.auraPower * dt);
      else addStatus(e, 'silence', 0.5, 1, h.uid);
    }
  }
}

/** Schadensbonus der Resonanz „Heldenchor“. */
export const HEROIC_CHOIR = 0.2;

function rebuildAuras(sim: GameSimulation): void {
  const s = sim.s;
  const map = new Map<number, HeroAura>();
  const stormsong = duoActive(sim, 'stormsong');
  const ironwatch = duoActive(sim, 'ironwatch');
  for (const h of s.heroes) {
    if (!active(h) || h.buffKind === 'nulled') continue;
    const st = heroStatsOf(sim, h);
    for (const t of s.towers) {
      const d2 = dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y);
      let a: HeroAura | undefined;
      const get = (): HeroAura => {
        a = map.get(t.uid);
        if (!a) {
          a = { dmg: 0, speed: 0, range: 0 };
          map.set(t.uid, a);
        }
        return a;
      };
      if (h.id === 'bard' && d2 <= st.auraRadius ** 2) get().speed += st.auraPower;
      if (h.id === 'mystic' && d2 <= st.auraRadius ** 2) {
        const x = get();
        x.dmg += st.auraPower;
        x.speed += st.auraPower * 0.5;
      }
      if (h.id === 'blade' && ironwatch && d2 <= 2.5 ** 2) get().dmg += 0.15;
    }
  }
  // Heldenchor: Die Bardin in Reichweite eines Netzes mit Feuer, Frost und Blitz verstärkt das ganze Netz.
  const bard = s.heroes.find((h) => h.id === 'bard' && active(h) && h.buffKind !== 'nulled');
  if (bard) {
    const r2 = heroStatsOf(sim, bard).auraRadius ** 2;
    for (const g of s.network.groups) {
      const members = g.members.map((uid) => sim.c.towerByUid.get(uid)).filter((t): t is NonNullable<typeof t> => !!t);
      const types = new Set(members.map((t) => TOWERS[t.def].damageType));
      if (!types.has('fire') || !types.has('frost') || !types.has('lightning')) continue;
      if (!members.some((t) => dist2(t.cx + 0.5, t.cy + 0.5, bard.x, bard.y) <= r2)) continue;
      for (const t of members) {
        let a = map.get(t.uid);
        if (!a) {
          a = { dmg: 0, speed: 0, range: 0 };
          map.set(t.uid, a);
        }
        a.dmg += HEROIC_CHOIR;
      }
    }
  }
  if (stormsong) {
    for (const t of s.towers) {
      if (TOWERS[t.def].damageType !== 'lightning') continue;
      let a = map.get(t.uid);
      if (!a) {
        a = { dmg: 0, speed: 0, range: 0 };
        map.set(t.uid, a);
      }
      a.dmg += 0.4;
    }
  }
  const sig = [...map.entries()].sort((x, y) => x[0] - y[0]).map(([k, v]) => `${k}:${v.dmg.toFixed(2)}/${v.speed.toFixed(2)}`).join(',');
  if (sig !== sim.c.heroAuraSig) {
    sim.c.heroAura = map;
    sim.c.heroAuraSig = sig;
    sim.c.statsDirty = true;
  }
}

// ------------------------------------------------------------ Fähigkeiten

function addBuff(sim: GameSimulation, id: GlobalBuffId, t: number, power: number, src: number): void {
  const s = sim.s;
  const cur = s.buffs.find((b) => b.id === id);
  if (cur) cur.t = Math.max(cur.t, t);
  else s.buffs.push({ id, t, power, src });
  sim.c.statsDirty = true;
}

function enemiesWithin(sim: GameSimulation, x: number, y: number, r: number): EnemyInstance[] {
  return sim.s.enemies.filter((e) => visible(e) && dist2(e.x, e.y, x, y) <= r * r);
}

function executeAbility(sim: GameSimulation, h: HeroInstance, id: string, st: HeroStats): CommandResult {
  const s = sim.s;
  const def = HEROES[h.id];
  const scale = (st.damage / Math.max(1, def.base.damage)) * (1 + 0.1 * (h.level - 1));
  const emit = (r: number, x = h.x, y = h.y): void => { sim.emit({ k: 'heroAbility', hero: h.id, ability: id, x, y, r }); };
  switch (id) {
    // Klingenwächterin
    case 'taunt':
      for (const e of enemiesWithin(sim, h.x, h.y, 3)) if (!e.flying && !e.boss) addStatus(e, 'root', 1.5, 1, h.uid);
      emit(3);
      break;
    case 'whirl':
      for (const e of enemiesWithin(sim, h.x, h.y, 1.6)) dealDamage(sim, e, 70 * scale, 'physical', h.uid);
      emit(1.6);
      break;
    case 'bulwark':
      h.buffKind = 'bulwark';
      h.buffT = 6;
      for (const e of enemiesWithin(sim, h.x, h.y, 3)) {
        if (e.boss) interruptEnemy(sim, e, 30);
        else if (!e.flying) addStatus(e, 'root', 4, 1, h.uid);
      }
      emit(3);
      break;
    // Aschenjäger
    case 'flameArrows':
      h.buffKind = 'flame';
      h.buffT = 6;
      emit(1);
      break;
    case 'trap': {
      const tg = sim.c.enemyByUid.get(h.target);
      const x = tg ? tg.x : h.x;
      const y = tg ? tg.y : h.y;
      s.zones.push({ uid: sim.uid(), kind: 'burnTrap', x, y, r: 1.2, t: 6, armed: 0, power: 12 * scale, tick: 0, link: 0, src: h.uid });
      emit(1.2, x, y);
      break;
    }
    case 'firerain': {
      const tg = enemiesWithin(sim, h.x, h.y, st.range + 2).filter((e) => !e.flying);
      if (tg.length === 0) return { ok: false, reason: 'Keine Ziele in Reichweite' };
      for (let i = 0; i < 8; i++) {
        const e = tg[i % tg.length];
        s.zones.push({ uid: sim.uid(), kind: 'meteor', x: e.x + (sim.rand() - 0.5) * 0.8, y: e.y + (sim.rand() - 0.5) * 0.8, r: 1.0, t: 0.1, armed: 0.3 + i * 0.2, power: 80 * scale, tick: 0, link: 0, src: h.uid });
      }
      emit(st.range + 2);
      break;
    }
    // Risswandler
    case 'riftstep': {
      let best: EnemyInstance | null = null;
      for (const e of s.enemies) if (visible(e) && !e.flying && (!best || e.remaining < best.remaining)) best = e;
      if (!best) return { ok: false, reason: 'Kein Ziel' };
      const ox = h.x;
      const oy = h.y;
      h.x = best.x - 0.3;
      h.y = best.y;
      h.ax = h.x;
      h.ay = h.y;
      dealDamage(sim, best, 90 * scale, 'arcane', h.uid);
      h.target = best.uid;
      sim.emit({ k: 'teleport', x: ox, y: oy, x2: h.x, y2: h.y });
      emit(0.8);
      break;
    }
    case 'voidmark':
      for (const e of enemiesWithin(sim, h.x, h.y, 2)) addStatus(e, 'voidMark', 5, 0.3, h.uid);
      emit(2);
      break;
    case 'execute': {
      const list = s.enemies.filter(visible).sort((a, b) => a.remaining - b.remaining).slice(0, 5);
      if (list.length === 0) return { ok: false, reason: 'Keine Ziele' };
      for (const e of list) {
        dealDamage(sim, e, e.maxHp * (e.boss ? 0.06 : 0.3), 'arcane', h.uid);
        sim.emit({ k: 'heroAbility', hero: h.id, ability: 'executeHit', x: e.x, y: e.y, r: 0.6 });
      }
      emit(1);
      break;
    }
    // Runenbardin
    case 'resoSong':
      addBuff(sim, 'resoSong', 8, 1, h.uid);
      emit(4);
      break;
    case 'inspire':
      for (const t of s.towers) if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) <= 9) t.forged = Math.max(t.forged, 6);
      sim.c.statsDirty = true;
      emit(3);
      break;
    case 'hymn':
      addBuff(sim, 'hymn', 8, 0.5, h.uid);
      emit(8);
      break;
    // Schmiedemeister
    case 'forge': {
      let best: (typeof s.towers)[number] | null = null;
      let bd = Infinity;
      for (const t of s.towers) {
        if (TOWERS[t.def].attack === 'none' || TOWERS[t.def].attack === 'economy') continue;
        const d = dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y);
        if (d < bd) {
          bd = d;
          best = t;
        }
      }
      if (!best || bd > 16) return { ok: false, reason: 'Kein Turm in der Nähe' };
      best.forged = Math.max(best.forged, 20);
      sim.c.statsDirty = true;
      emit(0.8, best.cx + 0.5, best.cy + 0.5);
      break;
    }
    case 'repairWave': {
      for (const t of s.towers) {
        if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) > 25) continue;
        t.stunned = 0;
        t.disrupted = 0;
      }
      sim.c.networkDirty = true;
      emit(5);
      break;
    }
    case 'masterwork':
      for (const t of s.towers) if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) <= 16) t.forged = Math.max(t.forged, 15);
      sim.c.statsDirty = true;
      emit(4);
      break;
    // Wettermacherin
    case 'rain':
      for (const e of s.enemies) if (e.alive) addStatus(e, 'wet', 6, 1, h.uid);
      emit(12);
      break;
    case 'frostBreath':
      for (const e of enemiesWithin(sim, h.x, h.y, 4)) {
        const c = addStatus(e, 'chill', 4, 0.22, h.uid, 3);
        c.stacks = 3;
        c.flag = Math.max(c.flag, 60);
      }
      emit(4);
      break;
    case 'thunderstorm': {
      const list = s.enemies.filter(visible);
      if (list.length === 0) return { ok: false, reason: 'Keine Ziele' };
      for (const e of list) addStatus(e, 'wet', 6, 1, h.uid);
      const p = heroPayload(h, st, 0);
      p.type = 'lightning';
      p.damage = 70 * scale;
      p.stats = { ...p.stats, chillPct: 0, damage: p.damage };
      for (let i = 0; i < 12; i++) {
        const e = list[Math.floor(sim.rand() * list.length)];
        if (!e.alive) continue;
        applyHit(sim, e, p);
        sim.emit({ k: 'heroAbility', hero: h.id, ability: 'strike', x: e.x, y: e.y, r: 0.5 });
      }
      emit(10);
      break;
    }
    // Leylinien-Mystiker
    case 'leyNode':
      s.zones.push({ uid: sim.uid(), kind: 'leyNode', x: h.x, y: h.y, r: 2, t: 30, armed: 0.01, power: 0.15, tick: 0, link: 0, src: h.uid });
      emit(2);
      break;
    case 'leyBeam': {
      let best: EnemyInstance | null = null;
      let bd = 36;
      for (const e of s.enemies) {
        if (!visible(e)) continue;
        const d = dist2(h.x, h.y, e.x, e.y);
        if (d < bd) {
          bd = d;
          best = e;
        }
      }
      if (!best) return { ok: false, reason: 'Kein Ziel in Reichweite' };
      const len = Math.sqrt(bd) || 1;
      const ux = (best.x - h.x) / len;
      const uy = (best.y - h.y) / len;
      for (const e of s.enemies) {
        if (!visible(e)) continue;
        const rx = e.x - h.x;
        const ry = e.y - h.y;
        const along = rx * ux + ry * uy;
        if (along < 0 || along > 6) continue;
        if (Math.abs(rx * uy - ry * ux) <= 0.45) dealDamage(sim, e, 140 * scale, 'arcane', h.uid);
      }
      sim.emit({ k: 'laser', x: h.x, y: h.y, tx: h.x + ux * 6, ty: h.y + uy * 6, power: 1, uid: h.uid });
      emit(0.6);
      break;
    }
    case 'worldweb':
      addBuff(sim, 'worldweb', 10, 0.25, h.uid);
      emit(8);
      break;
    // Prospektorin
    case 'treasure': {
      if (h.treasureWave === s.wave) return { ok: false, reason: 'In dieser Welle schon gefunden' };
      h.treasureWave = s.wave;
      const g = 40 + 4 * s.wave;
      s.gold += g;
      s.stats.goldEarned += g;
      sim.emit({ k: 'gold', amount: g, x: h.x, y: h.y });
      emit(0.8);
      break;
    }
    case 'goldFever':
      addBuff(sim, 'goldFever', 8, 1, h.uid);
      emit(3);
      break;
    case 'motherlode': {
      const g = 120 + 8 * s.wave;
      s.gold += g;
      s.stats.goldEarned += g;
      sim.emit({ k: 'gold', amount: g, x: h.x, y: h.y });
      emit(2);
      break;
    }
    // Wächter der Stille
    case 'silenceBan': {
      let done = false;
      for (const e of s.enemies) {
        if (!e.alive || !e.boss || dist2(e.x, e.y, h.x, h.y) > 36) continue;
        for (const a of e.abilities) {
          if (a.windup > 0) {
            a.windup = 0;
            a.cd = 6;
            done = true;
            sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'interrupt' });
          }
        }
        if (done) s.zones = s.zones.filter((z) => !(z.src === e.uid && z.armed > 0));
      }
      if (!done) return { ok: false, reason: 'Keine angekündigte Bossfähigkeit in Reichweite' };
      emit(6);
      break;
    }
    case 'unveil':
      for (const e of s.enemies) if (e.alive && dist2(e.x, e.y, h.x, h.y) <= 25) addStatus(e, 'reveal', 8, 1, h.uid);
      emit(5);
      break;
    case 'absoluteSilence':
      for (const e of s.enemies) {
        if (!e.alive) continue;
        if (e.boss) interruptEnemy(sim, e, e.maxPoise + 1);
        else addStatus(e, 'silence', 6, 1, h.uid);
      }
      emit(12);
      break;
    default:
      return { ok: false, reason: 'Unbekannte Fähigkeit' };
  }
  return { ok: true };
}

/** Schaden an Helden durch Bosszonen (Feuerzone, Stampfer) bzw. Nullpuls (Auren aus). */
export function zoneHitsHeroes(sim: GameSimulation, x: number, y: number, r: number, damage: number, nulls: boolean): void {
  for (const h of sim.s.heroes) {
    if (!active(h) || dist2(h.x, h.y, x, y) > r * r) continue;
    if (nulls) {
      h.buffKind = 'nulled';
      h.buffT = 6;
      sim.c.statsDirty = true;
      continue;
    }
    if (h.buffKind === 'bulwark') continue;
    h.hp = Math.max(1, h.hp - damage);
  }
}
