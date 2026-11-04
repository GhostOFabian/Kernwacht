import { ECONOMY } from '../config/balance.ts';
import { countsTowardLimit, LANE_BARRICADE, nextTowerSlotWave, towerLimit } from '../config/scaling.ts';
import { STARS } from '../config/goldsinks.ts';
import { releaseRunes } from './RuneSystem.ts';
import { runPassive, towerUnlocked, towerUnlockWave } from '../config/passiveTree.ts';
import { ACTIVE_MIN_LEVEL, footprintCells, FUSION_RANGE, FUSIONS, SELL_REFUND, TOWER_ACTIVES, TOWERS, towerSize } from '../config/towers.ts';
import { Cell, type FusionRecipe, type GridPos, type TowerId, type TowerInstance } from '../core/types.ts';
import { spawnsOf } from './WorldGenerator.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { computeField, inBounds, INF, isBuildableCell, tracePath } from './Pathfinding.ts';

// Gold, Bau, Verkauf und Upgrades.
// Schutz vor Geldvermehrung: Erstattung ist nie höher als die tatsächliche Investition.
// Volle Erstattung nur für Türme, die in der laufenden Bauphase gebaut wurden ("Rückgängig").

export interface BuildCheck {
  ok: boolean;
  reason?: string;
  cost: number;
  affordable: boolean;
  route?: GridPos[];
  blocks?: boolean;
}

/** Aktuelles Turmlimit inkl. Bonusplätzen aus Bosswellen (3.4). */
export function currentTowerLimit(sim: GameSimulation): number {
  const s = sim.s;
  return towerLimit(s.wave, s.rules.endless, s.rules.towerLimitOff) + (s.bossSlots ?? 0);
}

// ------------------------------------------------------------ 3.6 Veredelung, Bauplätze, Kernausbau

/** Voll ausgebaut (Stufe 4 + ggf. Spezialisierung, oder legendär/ohne Stufen)? */
export function fullyBuilt(t: TowerInstance): boolean {
  const d = TOWERS[t.def];
  if (d.legendary) return true;
  if (t.level - 1 < d.upgrades.length) return false;
  return d.specs.length < 2 || t.spec !== 0;
}

/** Preis für den nächsten Stern (Infinity = nicht möglich). */
export function starCost(t: TowerInstance): number {
  const d = TOWERS[t.def];
  const n = t.stars ?? 0;
  if (n >= STARS.max || d.attack === 'none') return Infinity;
  const value = d.legendary ? STARS.legendaryBase : (d.cost + d.upgrades.reduce((a, u) => a + u.cost, 0) + (d.specs[0]?.cost ?? 0)) * STARS.baseFactor;
  return Math.round(value * Math.pow(STARS.growth, Math.min(n, STARS.softcap)) * Math.pow(STARS.lateGrowth, Math.max(0, n - STARS.softcap)));
}

export function starState(sim: GameSimulation, t: TowerInstance): { ok: boolean; reason: string | null; cost: number } {
  const cost = starCost(t);
  if (!sim.s.rules.endless) return { ok: false, reason: 'Veredelung nur in den Endlosmodi', cost };
  if (TOWERS[t.def].attack === 'none') return { ok: false, reason: 'Nicht veredelbar', cost };
  if ((t.stars ?? 0) >= STARS.max) return { ok: false, reason: `Höchste Veredelung ★${STARS.max}`, cost };
  if (!fullyBuilt(t)) return { ok: false, reason: 'Erst voll ausbauen (Stufe 4 und Spezialisierung)', cost };
  if (sim.s.gold < cost) return { ok: false, reason: 'Nicht genug Gold', cost };
  return { ok: true, reason: null, cost };
}

export function starTower(sim: GameSimulation, uid: number): CommandResult {
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  const chk = starState(sim, t);
  if (!chk.ok) return { ok: false, reason: chk.reason ?? 'Nicht möglich' };
  sim.s.gold -= chk.cost;
  sim.s.stats.goldSpent += chk.cost;
  t.invested += chk.cost;
  t.stars = (t.stars ?? 0) + 1;
  markDirty(sim);
  sim.emit({ k: 'upgrade', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5 });
  return { ok: true, message: `${TOWERS[t.def].name} ★${t.stars}` };
}

/** Alle (bzw. die angegebenen) Türme veredeln, solange das Gold reicht – günstigster Schritt zuerst. */
export function starAllTowers(sim: GameSimulation, uids?: readonly number[]): CommandResult & { count: number; spent: number } {
  const set = uids ? new Set(uids) : null;
  let count = 0;
  let spent = 0;
  for (let guard = 0; guard < 3000; guard++) {
    let best: TowerInstance | null = null;
    let bc = Infinity;
    for (const t of sim.s.towers) {
      if (set && !set.has(t.uid)) continue;
      const st = starState(sim, t);
      if (st.ok && st.cost < bc) { best = t; bc = st.cost; }
    }
    if (!best) break;
    if (!starTower(sim, best.uid).ok) break;
    count++;
    spent += bc;
  }
  if (!count) return { ok: false, reason: 'Nichts zu veredeln (voll ausbauen oder mehr Gold)', count, spent };
  return { ok: true, message: `${count}× veredelt für ${spent} Gold`, count, spent };
}

/** Türme, die gegen das Turmlimit zählen (alles außer Barrikaden). */
export function countedTowers(sim: GameSimulation): number {
  let n = 0;
  for (const t of sim.s.towers) if (countsTowardLimit(t.def)) n++;
  return n;
}

export function towerCost(sim: GameSimulation, def: TowerId): number {
  const m = sim.c.mods;
  let c = TOWERS[def].cost * m.towerCostMul;
  if (def === 'goldmine') c *= m.mineCostMul;
  const pf = runPassive(sim.s.he);
  if (pf.towerCostPct) c *= Math.max(0.6, 1 - pf.towerCostPct);
  return Math.round(c);
}

export function nextUpgradeCost(t: TowerInstance): number | null {
  const def = TOWERS[t.def];
  if (t.level - 1 < def.upgrades.length) return def.upgrades[t.level - 1].cost;
  return null;
}

export function specCost(t: TowerInstance, spec: 1 | 2): number {
  return TOWERS[t.def].specs[spec - 1]?.cost ?? Infinity;
}

/** 3.1: Ab Welle PRESPEC.from (Endlos) können Türme direkt auf Höchststufe mit Spezialisierung gebaut werden – mit Aufpreis. */
export const PRESPEC = { from: 150, premium: 1.25 };

export function prespecAvailable(sim: GameSimulation, def: TowerId): boolean {
  const s = sim.s;
  const d = TOWERS[def];
  return s.rules.endless && s.wave >= PRESPEC.from && d.specs.length >= 2 && d.upgrades.length >= 3 && !d.legendary;
}

/** Preis eines vorspezialisierten Turms: (Grundpreis + alle Stufen + Spezialisierung) × Aufpreis. */
export function prespecCost(sim: GameSimulation, def: TowerId, spec: 1 | 2): number {
  const d = TOWERS[def];
  const ups = d.upgrades.reduce((a, u) => a + u.cost, 0);
  return Math.round((towerCost(sim, def) + ups + (d.specs[spec - 1]?.cost ?? 0)) * PRESPEC.premium);
}

export function refundValue(sim: GameSimulation, t: TowerInstance): number {
  const fresh = t.builtInPhase && sim.s.phase === 'build' && t.builtWave === sim.s.wave;
  return fresh ? t.invested : Math.floor(t.invested * SELL_REFUND);
}

export function canBuildHere(sim: GameSimulation, def: TowerId, cx: number, cy: number, spec: 0 | 1 | 2 = 0): BuildCheck {
  const s = sim.s;
  const map = s.map;
  if (spec && !prespecAvailable(sim, def)) {
    return { ok: false, reason: `Spezialisierter Bau erst ab Welle ${PRESPEC.from}`, cost: Infinity, affordable: false };
  }
  const cost = spec ? prespecCost(sim, def, spec) : towerCost(sim, def);
  const affordable = s.gold >= cost;
  const fail = (reason: string): BuildCheck => ({ ok: false, reason, cost, affordable });
  if (!inBounds(map, cx, cy)) return fail('Außerhalb der Karte');
  const idx = cy * map.width + cx;
  if (s.rules.bannedTowers.includes(def)) return fail('In dieser Mission gesperrt');
  if (s.he && !towerUnlocked(def, s.he, s.wave)) return fail(`Wird ab Welle ${towerUnlockWave(def, s.he)} freigeschaltet`);
  if (TOWERS[def].legendary) return fail('Legendäre Türme entstehen nur durch Fusion');
  if (TOWERS[def].mazingOnly && map.variant !== 'mazing') return fail('Nur im Mazing-Modus verfügbar');
  if (TOWERS[def].laneOnly) return canBuildLane(sim, cx, cy);
  if (countsTowardLimit(def)) {
    const lim = currentTowerLimit(sim);
    if (countedTowers(sim) >= lim) return fail(`Turmlimit erreicht (${lim}) – nächster Bauplatz ab Welle ${nextTowerSlotWave(s.wave)}. Weniger Türme = Fokusbonus.`);
  }
  const size = towerSize(def);
  if (size > 1 && !inBounds(map, cx + size - 1, cy + size - 1)) return fail(`Braucht ${size}×${size} Felder – Außerhalb der Karte`);
  const cells = footprintCells(def, cx, cy, map.width);
  if (cells.some((i) => sim.c.towerByCell.has(i))) return fail(size > 1 ? `Braucht ${size}×${size} freie Felder – belegt` : 'Feld ist bereits belegt');
  if (cells.some((i) => !isBuildableCell(map, i))) return fail(size > 1 ? `Braucht ${size}×${size} freie, bebaubare Felder` : 'Hier kann nicht gebaut werden');
  if (size > 1 && cells.some((i) => map.heights[i] !== map.heights[idx])) return fail(`Braucht ${size}×${size} ebene Felder (gleiche Höhe)`);
  if (map.variant === 'mazing') {
    // Keine Platzierung auf Gegnern oder deren nächster Zielzelle.
    for (const e of s.enemies) {
      if (!e.alive || e.flying) continue;
      const ei = Math.floor(e.y) * map.width + Math.floor(e.x);
      if (cells.includes(ei) || cells.includes(e.ny * map.width + e.nx)) return fail('Ein Gegner steht im Weg');
    }
    const blocked = sim.c.blocked.slice();
    for (const i of cells) blocked[i] = 1;
    const field = computeField(map, blocked);
    for (const sp of spawnsOf(map)) {
      if (field[sp.y * map.width + sp.x] >= INF) return { ok: false, reason: 'Würde den Weg vollständig blockieren', cost, affordable, blocks: true };
    }
    for (const e of s.enemies) {
      if (!e.alive || e.flying) continue;
      if (field[e.ny * map.width + e.nx] >= INF) return { ok: false, reason: 'Würde Gegner einsperren', cost, affordable, blocks: true };
    }
    const route = tracePath(map, field, map.spawn);
    if (!affordable) return { ok: false, reason: 'Nicht genug Gold', cost, affordable, route };
    return { ok: true, cost, affordable, route };
  }
  if (!affordable) return fail('Nicht genug Gold');
  return { ok: true, cost, affordable };
}

// ------------------------------------------------------------ Lane-Barrikaden (3.8)

/** Verfügbare Lane-Barrikaden und Sekunden bis zur nächsten freien Ladung. */
export function laneCharges(sim: GameSimulation): { left: number; next: number } {
  const s = sim.s;
  const recent = (s.laneBarricades ?? []).filter((t) => s.time - t < LANE_BARRICADE.window).sort((a, b) => a - b);
  const left = Math.max(0, LANE_BARRICADE.maxCharges - recent.length);
  const next = left > 0 ? 0 : Math.max(0, recent[0] + LANE_BARRICADE.window - s.time);
  return { left, next };
}

const mmss = (sec: number): string => `${Math.floor(sec / 60)}:${String(Math.floor(sec % 60)).padStart(2, '0')}`;

function canBuildLane(sim: GameSimulation, cx: number, cy: number): BuildCheck {
  const s = sim.s;
  const map = s.map;
  const fail = (reason: string): BuildCheck => ({ ok: false, reason, cost: 0, affordable: true });
  if (!s.rules.endless) return fail('Lane-Barrikaden nur in den Endlosmodi');
  if (map.variant === 'mazing') return fail('Im Mazing-Modus die normale Barrikade nutzen');
  if (!inBounds(map, cx, cy)) return fail('Außerhalb der Karte');
  const idx = cy * map.width + cx;
  if (map.cells[idx] !== Cell.Path) return fail('Nur direkt auf die Lane (Weg) setzen');
  if (sim.c.towerByCell.has(idx)) return fail('Feld ist bereits belegt');
  for (const sp of spawnsOf(map)) {
    if (Math.hypot(sp.x - cx, sp.y - cy) < LANE_BARRICADE.minSpawnDist) return fail(`Nicht direkt vor einem Riss – mindestens ${LANE_BARRICADE.minSpawnDist} Felder Abstand`);
  }
  if (Math.abs(map.goal.x - cx) + Math.abs(map.goal.y - cy) <= 1) return fail('Nicht direkt am Kern');
  const ch = laneCharges(sim);
  if (ch.left <= 0) return fail(`Höchstens ${LANE_BARRICADE.maxCharges} Barrikaden je ${LANE_BARRICADE.window / 60} Minuten – nächste in ${mmss(ch.next)}`);
  return { ok: true, cost: 0, affordable: true };
}

export function buildTower(sim: GameSimulation, def: TowerId, cx: number, cy: number, spec: 0 | 1 | 2 = 0): CommandResult {
  const chk = canBuildHere(sim, def, cx, cy, spec);
  if (!chk.ok) return { ok: false, reason: chk.reason ?? 'Nicht möglich' };
  const s = sim.s;
  const d = TOWERS[def];
  const t: TowerInstance = {
    uid: sim.uid(), def, cx, cy, level: spec ? d.upgrades.length + 1 : 1, spec, invested: chk.cost, builtWave: s.wave,
    builtInPhase: s.phase === 'build', priority: d.defaultPriority, cooldown: 0.3, target: 0, retarget: 0,
    aim: 0, stunned: 0, disrupted: 0, rampTarget: 0, rampStacks: 0, damageDealt: 0, kills: 0, goldEarned: 0,
    shots: 0, fireAnim: 0, activeCd: 0, activeT: 0, forged: 0, charge: 0, orbit: 0,
  };
  s.gold -= chk.cost;
  s.stats.goldSpent += chk.cost;
  s.stats.builtByTower[def] = (s.stats.builtByTower[def] ?? 0) + 1;
  s.towers.push(t);
  if (d.laneOnly) s.laneBarricades = [...(s.laneBarricades ?? []).filter((x) => s.time - x < LANE_BARRICADE.window), s.time];
  sim.reindex();
  markDirty(sim);
  sim.emit({ k: 'build', tower: def, x: cx + 0.5, y: cy + 0.5 });
  return { ok: true };
}

export function sellTower(sim: GameSimulation, uid: number): CommandResult {
  const s = sim.s;
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  const refund = refundValue(sim, t);
  s.gold += refund;
  s.stats.goldSpent -= Math.min(refund, s.stats.goldSpent);
  s.towers = s.towers.filter((x) => x.uid !== uid);
  releaseRunes(sim, uid);
  sim.reindex();
  markDirty(sim);
  sim.emit({ k: 'sell', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5, refund });
  return { ok: true, message: `+${refund} Gold` };
}

export function upgradeTower(sim: GameSimulation, uid: number): CommandResult {
  const s = sim.s;
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  const cost = nextUpgradeCost(t);
  if (cost === null) return { ok: false, reason: TOWERS[t.def].upgrades.length === 0 ? 'Nicht aufwertbar' : 'Maximale Stufe erreicht – Spezialisierung wählen' };
  if (s.gold < cost) return { ok: false, reason: 'Nicht genug Gold' };
  s.gold -= cost;
  s.stats.goldSpent += cost;
  t.invested += cost;
  t.level++;
  markDirty(sim);
  sim.emit({ k: 'upgrade', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5 });
  return { ok: true };
}

export function specializeTower(sim: GameSimulation, uid: number, spec: 1 | 2): CommandResult {
  const s = sim.s;
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  if (TOWERS[t.def].specs.length < 2) return { ok: false, reason: 'Nicht spezialisierbar' };
  if (t.level < 4) return { ok: false, reason: 'Erst alle drei Stufen ausbauen' };
  if (t.spec !== 0) return { ok: false, reason: 'Bereits spezialisiert' };
  const cost = specCost(t, spec);
  if (s.gold < cost) return { ok: false, reason: 'Nicht genug Gold' };
  s.gold -= cost;
  s.stats.goldSpent += cost;
  t.invested += cost;
  t.spec = spec;
  markDirty(sim);
  sim.emit({ k: 'upgrade', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5 });
  return { ok: true };
}

// ------------------------------------------------------------ Spezialisierung wechseln (3.2)

/** Ein Wechsel der Spezialisierung kostet diesen Anteil des Preises der neuen Spezialisierung. */
export const RESPEC_MUL = 0.5;

/** Kosten, um einen Turm auf Spezialisierung spec zu bringen (0 = bereits so; Infinity = nicht möglich). */
export function respecCost(t: TowerInstance, spec: 1 | 2): number {
  const d = TOWERS[t.def];
  if (d.legendary || d.specs.length < 2) return Infinity;
  if (t.spec === spec) return 0;
  const c = specCost(t, spec);
  return t.spec === 0 ? c : Math.round(c * RESPEC_MUL);
}

export function respecTower(sim: GameSimulation, uid: number, spec: 1 | 2): CommandResult {
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  if (t.spec === 0) return specializeTower(sim, uid, spec);
  if (t.spec === spec) return { ok: false, reason: 'Bereits so spezialisiert' };
  const cost = respecCost(t, spec);
  if (!Number.isFinite(cost)) return { ok: false, reason: 'Nicht spezialisierbar' };
  if (sim.s.gold < cost) return { ok: false, reason: 'Nicht genug Gold' };
  sim.s.gold -= cost;
  sim.s.stats.goldSpent += cost;
  t.invested += cost;
  t.spec = spec;
  markDirty(sim);
  sim.emit({ k: 'upgrade', tower: t.def, x: t.cx + 0.5, y: t.cy + 0.5 });
  return { ok: true };
}

/** Restkosten, um einen Turm auf Höchststufe mit Spezialisierung spec zu bringen. */
export function specTargetCost(t: TowerInstance, spec: 1 | 2): number {
  const d = TOWERS[t.def];
  if (d.legendary || d.specs.length < 2) return Infinity;
  let c = 0;
  for (let l = t.level; l <= d.upgrades.length; l++) c += d.upgrades[l - 1].cost;
  return c + respecCost(t, spec);
}

/** Gruppe: alle Türme ausbauen und auf Spezialisierung spec setzen bzw. umstellen, solange das Gold reicht (günstigste zuerst). */
export function groupSpecialize(sim: GameSimulation, uids: readonly number[], spec: 1 | 2): CommandResult & { count: number; spent: number; left: number } {
  const set = new Set(uids);
  const todo = sim.s.towers
    .filter((t) => set.has(t.uid) && t.spec !== spec && Number.isFinite(specTargetCost(t, spec)))
    .sort((a, b) => specTargetCost(a, spec) - specTargetCost(b, spec) || a.uid - b.uid);
  let count = 0;
  let spent = 0;
  for (const t of todo) {
    const c = specTargetCost(t, spec);
    if (c > sim.s.gold) continue;
    const g0 = sim.s.gold;
    while (nextUpgradeCost(t) !== null) if (!upgradeTower(sim, t.uid).ok) break;
    if (!respecTower(sim, t.uid, spec).ok) {
      spent += g0 - sim.s.gold;
      continue;
    }
    spent += g0 - sim.s.gold;
    count++;
  }
  const left = todo.length - count;
  const name = TOWERS[todo[0]?.def ?? 'ranger'].specs[spec - 1]?.name ?? '';
  if (!todo.length) return { ok: false, reason: 'Alle bereits so spezialisiert', count, spent, left };
  if (!count) return { ok: false, reason: 'Nicht genug Gold', count, spent, left };
  return { ok: true, message: `${count}× auf ${name} gestellt für ${spent} Gold${left ? ` (${left} fehlen noch – zu wenig Gold)` : ''}`, count, spent, left };
}

function markDirty(sim: GameSimulation): void {
  sim.c.networkDirty = true;
  sim.c.statsDirty = true;
  if (sim.s.map.variant === 'mazing') sim.c.fieldDirty = true;
}

/** Einkommen der Goldgräber am Wellenende. Nur Türme, die vor Wellenbeginn standen. */
/** Zusätzliches Gold eines Goldgräbers auf einem Goldvorkommen je Welle. */
export function veinBonus(wave: number): number {
  return Math.round(ECONOMY.veinFlat + ECONOMY.veinFlatPerWave * wave);
}

export function collectIncome(sim: GameSimulation): number {
  let total = 0;
  for (const t of sim.s.towers) {
    if (t.def !== 'goldmine' || t.builtWave >= sim.s.wave) continue;
    let inc = Math.round(sim.stats(t).income);
    // Goldvorkommen: zusätzlicher Abbau neben dem normalen Ertrag
    if (onVein(sim, t)) inc += veinBonus(sim.s.wave);
    t.goldEarned += inc;
    total += inc;
    sim.emit({ k: 'gold', amount: inc, x: t.cx + 0.5, y: t.cy + 0.5 });
  }
  return total;
}

// ------------------------------------------------------------ Fusion

export interface FusionOption { recipe: FusionRecipe; partner: TowerInstance; affordable: boolean }

function fused(t: TowerInstance): boolean {
  const d = TOWERS[t.def];
  return t.level >= 4 && (d.specs.length === 0 || t.spec !== 0);
}

/** Mögliche Fusionen für einen Turm (voll ausgebaut + benachbarter, voll ausgebauter Partner). */
export function fusionOptions(sim: GameSimulation, t: TowerInstance): FusionOption[] {
  const out: FusionOption[] = [];
  for (const r of FUSIONS) {
    const other = r.a === t.def ? r.b : r.b === t.def ? r.a : null;
    if (!other) continue;
    let best: TowerInstance | null = null;
    let bd = Infinity;
    for (const o of sim.s.towers) {
      if (o === t || o.def !== other) continue;
      const d = Math.hypot(o.cx - t.cx, o.cy - t.cy);
      if (d <= FUSION_RANGE && d < bd) {
        bd = d;
        best = o;
      }
    }
    if (best) out.push({ recipe: r, partner: best, affordable: sim.s.gold >= r.cost });
  }
  return out;
}

/**
 * Vorschau beim Platzieren: welche Fusionen ein Turm dieses Typs an (cx, cy) mit bereits gebauten
 * Türmen eingehen könnte (Partner in Reichweite, unabhängig von Stufe/Spezialisierung).
 */
export function fusionPreview(sim: GameSimulation, def: TowerId, cx: number, cy: number): { recipe: FusionRecipe; partnerDef: TowerId; partners: TowerInstance[] }[] {
  return FUSIONS.filter((r) => r.a === def || r.b === def).map((r) => {
    const partnerDef = r.a === def ? r.b : r.a;
    const partners = sim.s.towers.filter((o) => o.def === partnerDef && Math.hypot(o.cx - cx, o.cy - cy) <= FUSION_RANGE);
    return { recipe: r, partnerDef, partners };
  });
}

export function canFuse(t: TowerInstance, partner: TowerInstance): boolean {
  return fused(t) && fused(partner);
}

export function fuseTowers(sim: GameSimulation, uid: number, partnerUid: number): CommandResult {
  const s = sim.s;
  const t = sim.c.towerByUid.get(uid);
  const p = sim.c.towerByUid.get(partnerUid);
  if (!t || !p) return { ok: false, reason: 'Turm nicht gefunden' };
  const r = FUSIONS.find((f) => (f.a === t.def && f.b === p.def) || (f.b === t.def && f.a === p.def));
  if (!r) return { ok: false, reason: 'Diese Türme lassen sich nicht verschmelzen' };
  if (Math.hypot(t.cx - p.cx, t.cy - p.cy) > FUSION_RANGE) return { ok: false, reason: 'Die Türme stehen zu weit auseinander' };
  if (!canFuse(t, p)) return { ok: false, reason: 'Beide Türme müssen voll ausgebaut und spezialisiert sein' };
  if (s.gold < r.cost) return { ok: false, reason: 'Nicht genug Gold' };
  s.gold -= r.cost;
  s.stats.goldSpent += r.cost;
  t.def = r.id;
  t.level = 4;
  t.spec = 0;
  t.invested += p.invested + r.cost;
  t.priority = TOWERS[r.id].defaultPriority;
  t.target = 0;
  t.charge = 0;
  t.activeCd = 0;
  t.activeT = 0;
  t.builtInPhase = false;
  // Der legendäre Turm entsteht immer auf dem Feld, das näher an der Lane liegt
  const keepPartnerCell = laneDistance(sim, p.cx, p.cy) < laneDistance(sim, t.cx, t.cy);
  const px = p.cx;
  const py = p.cy;
  s.towers = s.towers.filter((x) => x.uid !== p.uid);
  if (keepPartnerCell) {
    t.cx = px;
    t.cy = py;
  }
  releaseRunes(sim, p.uid);
  s.stats.fusions++;
  s.stats.builtByTower[r.id] = (s.stats.builtByTower[r.id] ?? 0) + 1;
  sim.reindex();
  markDirty(sim);
  sim.emit({ k: 'fusion', tower: r.id, x: t.cx + 0.5, y: t.cy + 0.5 });
  sim.emit({ k: 'message', text: `Legendärer Turm erschaffen: ${TOWERS[r.id].name}!`, severity: 'info' });
  return { ok: true };
}

// ------------------------------------------------------------ Aktive Turmfähigkeiten

export function activeState(sim: GameSimulation, t: TowerInstance): { available: boolean; reason: string | null } {
  const d = TOWERS[t.def];
  if (!d.active) return { available: false, reason: 'Keine aktive Fähigkeit' };
  if (t.level < ACTIVE_MIN_LEVEL && !d.legendary) return { available: false, reason: `Ab Stufe ${ACTIVE_MIN_LEVEL}` };
  if (sim.s.phase !== 'combat') return { available: false, reason: 'Nur während einer Welle' };
  if (t.activeCd > 0) return { available: false, reason: `Bereit in ${Math.ceil(t.activeCd)} s` };
  if (t.stunned > 0) return { available: false, reason: 'Turm ist außer Gefecht' };
  return { available: true, reason: null };
}

export function triggerActive(sim: GameSimulation, uid: number): CommandResult {
  const t = sim.c.towerByUid.get(uid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  const st = activeState(sim, t);
  if (!st.available) return { ok: false, reason: st.reason ?? 'Nicht verfügbar' };
  const id = TOWERS[t.def].active;
  if (!id) return { ok: false, reason: 'Keine aktive Fähigkeit' };
  const a = TOWER_ACTIVES[id];
  t.activeCd = a.cooldown;
  t.activeT = a.duration;
  sim.s.stats.activesUsed++;
  sim.c.statsDirty = true;
  sim.pendingActives.push({ uid: t.uid, id });
  sim.emit({ k: 'towerActive', tower: t.def, active: id, x: t.cx + 0.5, y: t.cy + 0.5 });
  return { ok: true };
}

/** Sofortiger Minenertrag (aktive Fähigkeit Goldrausch). */
export function collectIncomeFor(sim: GameSimulation, t: TowerInstance, mul: number): number {
  const inc = Math.round(sim.stats(t).income * mul);
  sim.s.gold += inc;
  sim.s.stats.goldEarned += inc;
  t.goldEarned += inc;
  return inc;
}

/** Steht der Turm (irgendein Feld seiner Grundfläche) auf einer Goldader? */
export function onVein(sim: GameSimulation, t: TowerInstance): boolean {
  return footprintCells(t.def, t.cx, t.cy, sim.s.map.width).some((i) => sim.s.map.veins.includes(i));
}

/** Steht der Turm (irgendein Feld seiner Grundfläche) auf einem Kristallvorkommen? */
export function onDeposit(sim: GameSimulation, t: TowerInstance): boolean {
  const d = sim.s.map.deposits;
  return !!d && footprintCells(t.def, t.cx, t.cy, sim.s.map.width).some((i) => d.includes(i));
}

// ------------------------------------------------------------ Alles aufwerten (2.6)

/** Türme, die noch eine normale Stufe aufwerten können (ohne Spezialisierung). */
export function upgradableTowers(sim: GameSimulation, uids?: readonly number[]): TowerInstance[] {
  const set = uids ? new Set(uids) : null;
  return sim.s.towers.filter((t) => (!set || set.has(t.uid)) && nextUpgradeCost(t) !== null);
}

/** Gesamtkosten, um die gewählten Türme bis zur höchsten Stufe auszubauen (ohne Spezialisierung). */
export function upgradeAllCost(sim: GameSimulation, uids?: readonly number[]): { total: number; steps: number } {
  let total = 0;
  let steps = 0;
  for (const t of upgradableTowers(sim, uids)) {
    const d = TOWERS[t.def];
    for (let l = t.level; l - 1 < d.upgrades.length; l++) {
      total += d.upgrades[l - 1].cost;
      steps++;
    }
  }
  return { total, steps };
}

/**
 * Wertet alle (bzw. die gewählten) Türme bis zur höchsten Stufe auf, solange das Gold reicht.
 * Immer die günstigste mögliche Stufe zuerst – so verteilt sich das Gold gleichmäßig. Spezialisierungen
 * werden nie automatisch gewählt.
 */
export function upgradeAllTowers(sim: GameSimulation, uids?: readonly number[]): CommandResult & { count: number; spent: number } {
  let count = 0;
  let spent = 0;
  for (let guard = 0; guard < 2000; guard++) {
    const cand = upgradableTowers(sim, uids)
      .map((t) => ({ t, c: nextUpgradeCost(t) ?? Infinity }))
      .filter((x) => x.c <= sim.s.gold)
      .sort((a, b) => a.c - b.c || a.t.level - b.t.level || a.t.uid - b.t.uid);
    if (!cand.length) break;
    const r = upgradeTower(sim, cand[0].t.uid);
    if (!r.ok) break;
    count++;
    spent += cand[0].c;
  }
  // 3.6: danach veredeln (Endlos), solange das Gold reicht
  let stars = 0;
  if (sim.s.rules.endless) {
    const r = starAllTowers(sim, uids);
    stars = r.count;
    spent += r.spent;
  }
  if (!count && !stars) {
    const left = upgradableTowers(sim, uids).length;
    return { ok: false, reason: left ? 'Nicht genug Gold für eine weitere Stufe' : sim.s.rules.endless ? 'Nichts mehr aufzuwerten oder zu veredeln (Spezialisierung wählen oder mehr Gold)' : 'Alle Türme sind bereits auf der höchsten Stufe', count, spent };
  }
  return { ok: true, message: `${count ? `${count}× aufgewertet` : ''}${count && stars ? ', ' : ''}${stars ? `${stars}× veredelt` : ''} für ${spent} Gold`, count: count + stars, spent };
}

// ------------------------------------------------------------ Alle verkaufen (2.9.2)

/** Erlös, wenn alle (bzw. die angegebenen) Türme verkauft würden. */
export function sellAllValue(sim: GameSimulation, uids?: readonly number[]): { count: number; gold: number } {
  const set = uids ? new Set(uids) : null;
  let gold = 0;
  let count = 0;
  for (const t of sim.s.towers) {
    if (set && !set.has(t.uid)) continue;
    gold += refundValue(sim, t);
    count++;
  }
  return { count, gold };
}

/** Verkauft alle (bzw. die angegebenen) Türme. Gesockelte Runen kehren in den Beutel zurück. */
export function sellAllTowers(sim: GameSimulation, uids?: readonly number[]): CommandResult & { count: number; gold: number } {
  const set = uids ? new Set(uids) : null;
  const list = sim.s.towers.filter((t) => !set || set.has(t.uid)).map((t) => t.uid);
  if (!list.length) return { ok: false, reason: 'Keine Türme zum Verkaufen', count: 0, gold: 0 };
  const g0 = sim.s.gold;
  let count = 0;
  for (const uid of list) if (sellTower(sim, uid).ok) count++;
  const gold = sim.s.gold - g0;
  return { ok: true, message: `${count} Türme verkauft: +${gold} Gold`, count, gold };
}

/** Abstand (Felder, euklidisch) von einem Feld zur nächsten Lane-Zelle (Weg im Classic, Gegnerroute im Mazing). */
export function laneDistance(sim: GameSimulation, cx: number, cy: number): number {
  const map = sim.s.map;
  let lane: GridPos[];
  if (map.variant === 'mazing') lane = spawnsOf(map).flatMap((sp) => tracePath(map, sim.c.field, sp));
  else {
    lane = [];
    for (let i = 0; i < map.cells.length; i++) if (map.cells[i] === Cell.Path) lane.push({ x: i % map.width, y: Math.floor(i / map.width) });
  }
  let best = Infinity;
  for (const c of lane) best = Math.min(best, (c.x - cx) ** 2 + (c.y - cy) ** 2);
  return Math.sqrt(best);
}

/** Alle Fusionspaare im Feld (jedes Paar einmal), mit Bereitschaft und dem Feld, auf dem der legendäre Turm entsteht. */
export function fusionPairs(sim: GameSimulation): { a: TowerInstance; b: TowerInstance; recipe: FusionRecipe; ready: boolean; affordable: boolean; target: TowerInstance }[] {
  const out: { a: TowerInstance; b: TowerInstance; recipe: FusionRecipe; ready: boolean; affordable: boolean; target: TowerInstance }[] = [];
  const seen = new Set<string>();
  for (const t of sim.s.towers) {
    for (const f of fusionOptions(sim, t)) {
      const key = [t.uid, f.partner.uid].sort((x, y) => x - y).join(':');
      if (seen.has(key)) continue;
      seen.add(key);
      const target = laneDistance(sim, f.partner.cx, f.partner.cy) < laneDistance(sim, t.cx, t.cy) ? f.partner : t;
      out.push({ a: t, b: f.partner, recipe: f.recipe, ready: canFuse(t, f.partner), affordable: sim.s.gold >= f.recipe.cost, target });
    }
  }
  return out;
}

// ------------------------------------------------------------ Legendäre Türme versetzen (2.9.5)

export const MOVE_STUN = 3;

/** Prüft, ob ein legendärer Turm auf ein anderes Feld versetzt werden darf (kostenlos, danach kurz außer Gefecht). */
export function canMoveTower(sim: GameSimulation, uid: number, cx: number, cy: number): BuildCheck {
  const s = sim.s;
  const map = s.map;
  const t = sim.c.towerByUid.get(uid);
  const fail = (reason: string, blocks = false): BuildCheck => ({ ok: false, reason, cost: 0, affordable: true, blocks });
  if (!t) return fail('Turm nicht gefunden');
  if (!TOWERS[t.def].legendary) return fail('Nur legendäre Türme können versetzt werden');
  if (!inBounds(map, cx, cy)) return fail('Außerhalb der Karte');
  if (t.cx === cx && t.cy === cy) return fail('Der Turm steht bereits hier');
  const idx = cy * map.width + cx;
  const other = sim.c.towerByCell.get(idx);
  if (other && other !== t) return fail('Feld ist bereits belegt');
  if (!isBuildableCell(map, idx)) return fail('Hier kann nicht gebaut werden');
  if (map.variant === 'mazing') {
    for (const e of s.enemies) {
      if (!e.alive || e.flying) continue;
      if ((Math.floor(e.x) === cx && Math.floor(e.y) === cy) || (e.nx === cx && e.ny === cy)) return fail('Ein Gegner steht im Weg');
    }
    const blocked = sim.c.blocked.slice();
    for (const i of footprintCells(t.def, t.cx, t.cy, map.width)) blocked[i] = 0;
    blocked[idx] = 1;
    const field = computeField(map, blocked);
    for (const sp of spawnsOf(map)) if (field[sp.y * map.width + sp.x] >= INF) return fail('Würde den Weg vollständig blockieren', true);
    for (const e of s.enemies) {
      if (!e.alive || e.flying) continue;
      if (field[e.ny * map.width + e.nx] >= INF) return fail('Würde Gegner einsperren', true);
    }
    return { ok: true, cost: 0, affordable: true, route: tracePath(map, field, map.spawn) };
  }
  return { ok: true, cost: 0, affordable: true };
}

export function moveTower(sim: GameSimulation, uid: number, cx: number, cy: number): CommandResult {
  const chk = canMoveTower(sim, uid, cx, cy);
  if (!chk.ok) return { ok: false, reason: chk.reason ?? 'Nicht möglich' };
  const t = sim.c.towerByUid.get(uid)!;
  const from = { x: t.cx + 0.5, y: t.cy + 0.5 };
  t.cx = cx;
  t.cy = cy;
  t.target = 0;
  t.charge = 0;
  t.stunned = Math.max(t.stunned, MOVE_STUN);
  sim.reindex();
  markDirty(sim);
  sim.emit({ k: 'sell', tower: t.def, x: from.x, y: from.y, refund: 0 });
  sim.emit({ k: 'build', tower: t.def, x: cx + 0.5, y: cy + 0.5 });
  sim.emit({ k: 'message', text: `${TOWERS[t.def].name} versetzt – ${MOVE_STUN} s Aufbau`, severity: 'info' });
  return { ok: true };
}
