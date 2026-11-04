import { runPassive } from '../config/passiveTree.ts';
import { CHAMPIONS, STAT_LIMITS, ZERO_SKILL_MODS, type ChampionSkill, type SkillMods, type SkillNode } from '../config/champions.ts';
import { HERO_ENDLESS, xpToNext } from '../config/heroEndless.ts';
import { PARAGON, PARAGON_NODES, paragonPrePoints, paragonSpent, paragonXpToNext } from '../config/paragon.ts';
import { itemStats } from '../config/items.ts';
import { TOWERS } from '../config/towers.ts';
import { Cell, type ChampionState, type ChampionStats, type EnemyInstance, type HeroEndlessState, type MapDefinition, type TowerBuff } from '../core/types.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { dealDamage } from './DamageSystem.ts';
import { addStatus } from './ElementSystem.ts';
import { enemyContactDps, interruptEnemy } from './EnemySystem.ts';
import { addResource, collectPickups, setGrantXpHook, updatePickups } from './LootSystem.ts';

// Frei steuerbarer Held des Helden-Endlosmodus: Bewegung über ein eigenes Raster-Pathfinding
// (Türme blockieren nicht), Werte aus Stufe + Baum + Ausrüstung + Wirkungen, Kampf, Fähigkeiten,
// Erfahrung und Turmverstärkungen. Alles deterministisch und im GameState serialisierbar.

/** UID, unter der Heldenschaden verbucht wird (kein Turm). */
export const CHAMPION_UID = 990001;

const dist2 = (ax: number, ay: number, bx: number, by: number): number => (ax - bx) ** 2 + (ay - by) ** 2;

// ------------------------------------------------------------ Werte

export function skillModsOf(c: ChampionState): SkillMods {
  const out: SkillMods = { ...ZERO_SKILL_MODS };
  const def = CHAMPIONS[c.id];
  for (const node of def.tree) {
    const rank = c.tree[node.id] ?? 0;
    if (!rank || !node.mods) continue;
    for (const [k, v] of Object.entries(node.mods) as [keyof SkillMods, number][]) out[k] += v * rank;
  }
  return out;
}

/** Berechnet die effektiven Heldenwerte vollständig neu (kein Aufaddieren über die Zeit → keine Drift). */
export function championStats(he: HeroEndlessState): ChampionStats {
  const c = he.champion;
  const def = CHAMPIONS[c.id];
  const st: ChampionStats = { ...def.base };
  const add = (p: Partial<ChampionStats>, k = 1): void => {
    for (const [key, v] of Object.entries(p) as [keyof ChampionStats, number][]) st[key] += v * k;
  };
  add(def.perLevel, effectiveLevelSteps(c.level));
  for (const node of def.tree) {
    const rank = c.tree[node.id] ?? 0;
    if (rank && node.stats) add(node.stats, rank);
  }
  for (const uid of c.equipment) {
    if (uid === null) continue;
    const inst = c.inventory.find((i) => i.uid === uid);
    if (inst) add(itemStats(inst.item, inst.plus));
  }
  for (const b of c.buffs) add(b.add);
  if (he.perks.includes('magnet1')) st.pickupRadius += 0.3;
  if (he.perks.includes('fortune')) st.lootBonus += 0.25;
  // Passiver Baum (dauerhaft)
  const pf = runPassive(he);
  st.maxHp *= 1 + pf.heroHpPct;
  st.damage *= 1 + pf.heroDmgPct;
  st.armor += pf.heroArmor;
  st.cdr += pf.heroCdr;
  st.critChance += pf.heroCrit;
  st.hpRegen += pf.heroRegen;
  st.manaRegen += pf.manaRegen;
  st.supportPower += pf.supportPower;
  st.pickupRadius += pf.pickupRadius;
  st.lootBonus += pf.lootPct;
  // Paragon (2.7)
  st.attackSpeed *= 1 + pf.heroAtkSpeed;
  st.abilityPower *= 1 + pf.abilityPct;
  st.moveSpeed += pf.moveSpeed;
  st.lifesteal += pf.lifesteal;
  for (const [key, [lo, hi]] of Object.entries(STAT_LIMITS) as [keyof ChampionStats, [number, number]][]) st[key] = Math.min(hi, Math.max(lo, st[key]));
  st.maxHp = Math.max(1, st.maxHp);
  st.maxMana = Math.max(0, st.maxMana);
  return st;
}

// ------------------------------------------------------------ Bewegung

/** Start- und Wiederbelebungspunkt: begehbares Nachbarfeld des Kerns, möglichst nahe am Laden. */
export function championHome(map: MapDefinition, shop: { x: number; y: number }): { x: number; y: number } {
  const g = map.goal;
  let best = { x: g.x, y: g.y };
  let bd = Infinity;
  for (let dy = -1; dy <= 1; dy++) {
    for (let dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      const x = g.x + dx;
      const y = g.y + dy;
      if (x < 0 || y < 0 || x >= map.width || y >= map.height) continue;
      if (map.cells[y * map.width + x] === Cell.Blocked || (x === shop.x && y === shop.y)) continue;
      const d = (x - shop.x) ** 2 + (y - shop.y) ** 2 + (dx && dy ? 0.5 : 0);
      if (d < bd) {
        bd = d;
        best = { x, y };
      }
    }
  }
  return { x: best.x + 0.5, y: best.y + 0.5 };
}

export function championWalkable(sim: GameSimulation, x: number, y: number): boolean {
  const m = sim.s.map;
  if (x < 0 || y < 0 || x >= m.width || y >= m.height) return false;
  const he = sim.s.he;
  if (he && he.shop.x === x && he.shop.y === y) return false;
  return m.cells[y * m.width + x] !== Cell.Blocked;
}

/** Kürzester Weg (8 Richtungen, keine Ecken durch Hindernisse). Türme blockieren nicht. */
export function findChampionPath(sim: GameSimulation, sx: number, sy: number, tx: number, ty: number): number[] | null {
  const m = sim.s.map;
  const w = m.width;
  if (!championWalkable(sim, tx, ty)) return null;
  if (sx === tx && sy === ty) return [];
  const n = w * m.height;
  const dist = new Float64Array(n).fill(Infinity);
  const prev = new Int32Array(n).fill(-1);
  const start = sy * w + sx;
  const goal = ty * w + tx;
  dist[start] = 0;
  // Einfacher Dijkstra mit linearer Suche reicht für ≤ 1100 Zellen und seltene Befehle.
  const open: number[] = [start];
  const inOpen = new Uint8Array(n);
  inOpen[start] = 1;
  while (open.length) {
    let bi = 0;
    for (let k = 1; k < open.length; k++) if (dist[open[k]] < dist[open[bi]]) bi = k;
    const cur = open[bi];
    open[bi] = open[open.length - 1];
    open.pop();
    inOpen[cur] = 0;
    if (cur === goal) break;
    const cx = cur % w;
    const cy = (cur - cx) / w;
    for (let dy = -1; dy <= 1; dy++) {
      for (let dx = -1; dx <= 1; dx++) {
        if (!dx && !dy) continue;
        const nx = cx + dx;
        const ny = cy + dy;
        if (!championWalkable(sim, nx, ny)) continue;
        if (dx && dy && (!championWalkable(sim, cx + dx, cy) || !championWalkable(sim, cx, cy + dy))) continue;
        const ni = ny * w + nx;
        const nd = dist[cur] + (dx && dy ? Math.SQRT2 : 1);
        if (nd < dist[ni]) {
          dist[ni] = nd;
          prev[ni] = cur;
          if (!inOpen[ni]) {
            inOpen[ni] = 1;
            open.push(ni);
          }
        }
      }
    }
  }
  if (!Number.isFinite(dist[goal])) return null;
  const path: number[] = [];
  for (let at = goal; at !== start && at >= 0; at = prev[at]) path.push(at);
  path.reverse();
  return path;
}

/** Position festsetzen/lösen. Festgesetzt: Laufbefehle werden ignoriert, keine Verfolgung von Gegnern. */
export function setHoldPosition(sim: GameSimulation, hold: boolean): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held in dieser Partie' };
  const c = he.champion;
  c.holdPos = hold;
  if (hold) {
    c.path = [];
    c.hx = c.x;
    c.hy = c.y;
  }
  return { ok: true };
}

export function orderMove(sim: GameSimulation, tx: number, ty: number): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held in dieser Partie' };
  const c = he.champion;
  if (c.dead > 0) return { ok: false, reason: `Held wird wiederbelebt (${Math.ceil(c.dead)} s)` };
  if (c.holdPos) return { ok: false, reason: 'Held ist festgesetzt – mit Z oder dem Anker-Knopf lösen' };
  if (he.mining) return { ok: false, reason: `Der Held baut ab und kann nicht unterbrochen werden (${Math.ceil(he.mining.t)} s)` };
  he.mineTarget = -1;
  const path = findChampionPath(sim, Math.floor(c.x), Math.floor(c.y), tx, ty);
  if (!path) return { ok: false, reason: 'Dieses Feld ist nicht erreichbar' };
  c.path = path;
  c.hx = tx + 0.5;
  c.hy = ty + 0.5;
  c.target = 0;
  return { ok: true };
}

function moveAlong(sim: GameSimulation, c: ChampionState, speed: number, dt: number): void {
  const m = sim.s.map;
  let budget = speed * dt;
  while (budget > 0 && c.path.length) {
    const i = c.path[0];
    const tx = (i % m.width) + 0.5;
    const ty = Math.floor(i / m.width) + 0.5;
    const d = Math.hypot(tx - c.x, ty - c.y);
    if (d <= budget) {
      c.x = tx;
      c.y = ty;
      budget -= d;
      c.path.shift();
    } else {
      c.x += ((tx - c.x) / d) * budget;
      c.y += ((ty - c.y) / d) * budget;
      c.facing = Math.atan2(ty - c.y, tx - c.x);
      budget = 0;
    }
  }
}

// ------------------------------------------------------------ Erfahrung & Fähigkeitenbaum

/** Stufenzuwachs der Grundwerte: bis Stufe 30 voll, danach halb (sonst wären Stufe-99-Helden übermächtig). */
export function effectiveLevelSteps(level: number): number {
  const L = Math.max(1, level);
  if (L <= PARAGON.softcapLevel) return L - 1;
  return PARAGON.softcapLevel - 1 + (L - PARAGON.softcapLevel) * PARAGON.softcapMul;
}

/** Skillpunkte, die ein Held bis zu dieser Stufe insgesamt erhält. */
export function skillPointsForLevel(level: number): number {
  let n = 0;
  for (let l = 2; l <= level; l++) n += HERO_ENDLESS.pointsPerLevel + (l % HERO_ENDLESS.bonusPointEvery === 0 ? 1 : 0);
  return n;
}

export function grantXp(sim: GameSimulation, amount: number): void {
  const he = sim.s.he;
  if (!he || amount <= 0) return;
  const c = he.champion;
  const mul = (he.perks.includes('wisdom') ? 1.1 : 1) * (1 + runPassive(he).xpPct);
  he.stats.xpCollected += amount * mul;
  if (c.level >= PARAGON.heroMaxLevel) {
    grantParagonXp(sim, amount * mul);
    return;
  }
  c.xp += amount * mul;
  let guard = 0;
  while (c.level < PARAGON.heroMaxLevel && c.xp >= xpToNext(c.level) && guard++ < 100) {
    const before = championStats(he);
    c.xp -= xpToNext(c.level);
    c.level++;
    c.skillPoints += HERO_ENDLESS.pointsPerLevel + (c.level % HERO_ENDLESS.bonusPointEvery === 0 ? 1 : 0);
    const after = championStats(he);
    // Zuwachs an Maximalwerten wird sofort aufgefüllt, dazu 25 % Heilung.
    c.hp = Math.min(after.maxHp, c.hp + (after.maxHp - before.maxHp) + after.maxHp * 0.25);
    c.mana = Math.min(after.maxMana, c.mana + (after.maxMana - before.maxMana));
    he.stats.levelUps++;
    sim.emit({ k: 'champLevel', level: c.level, x: c.x, y: c.y });
  }
  if (c.level >= PARAGON.heroMaxLevel && c.xp > 0) {
    // Überschüssige EP auf Stufe 99 gehen direkt ins Paragon
    const rest = c.xp;
    c.xp = 0;
    grantParagonXp(sim, rest);
  }
}

/** Paragon-EP (ab Heldenstufe 99): jede Paragonstufe gibt einen Punkt, höchstens Paragon 500. */
export function grantParagonXp(sim: GameSimulation, amount: number): void {
  const he = sim.s.he;
  if (!he || amount <= 0) return;
  const p = (he.paragon ??= { level: 0, xp: 0, points: {}, rev: 0 });
  if (p.level >= PARAGON.maxLevel) return;
  p.xp += amount;
  let guard = 0;
  while (p.level < PARAGON.maxLevel && p.xp >= paragonXpToNext(p.level) && guard++ < 100) {
    p.xp -= paragonXpToNext(p.level);
    p.level++;
    const c = he.champion;
    sim.emit({ k: 'message', text: `✦ Paragon ${p.level} erreicht – 1 Paragonpunkt (Charakter → Paragon)`, severity: 'info' });
    sim.emit({ k: 'champLevel', level: c.level, x: c.x, y: c.y });
  }
  if (p.level >= PARAGON.maxLevel) p.xp = 0;
}

/** Gesamtbudget an Paragonpunkten: Paragonstufe + Vorab-Punkte (ab Heldenstufe 50) + Meilensteine. */
export function paragonBudget(he: HeroEndlessState): number {
  return (he.paragon?.level ?? 0) + paragonPrePoints(he.champion.level) + (he.paragonBonus ?? 0);
}

export function paragonFree(he: HeroEndlessState): number {
  const p = he.paragon;
  return Math.max(0, paragonBudget(he) - paragonSpent(p?.points));
}

export function paragonRaiseReason(he: HeroEndlessState, nodeId: string): string | null {
  const node = PARAGON_NODES.find((n) => n.id === nodeId);
  if (!node) return 'Unbekannter Knoten';
  const p = (he.paragon ??= { level: 0, xp: 0, points: {}, rev: 0 });
  if (paragonBudget(he) <= 0) return `Paragonpunkte gibt es ab Heldenstufe 50 (alle 5 Stufen), ab Stufe ${PARAGON.heroMaxLevel} je Paragonstufe und durch Meilensteine`;
  if ((p.points[node.id] ?? 0) >= node.maxRank) return 'Maximaler Rang';
  if (paragonFree(he) <= 0) return 'Keine Paragonpunkte frei';
  return null;
}

export function spendParagonPoint(sim: GameSimulation, nodeId: string): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const why = paragonRaiseReason(he, nodeId);
  if (why) return { ok: false, reason: why };
  const p = he.paragon!;
  const node = PARAGON_NODES.find((n) => n.id === nodeId)!;
  const before = championStats(he);
  p.points[nodeId] = (p.points[nodeId] ?? 0) + 1;
  p.rev++;
  if (node.per.coreLives) {
    sim.s.lives += node.per.coreLives;
    sim.s.maxLives += node.per.coreLives;
  }
  const after = championStats(he);
  he.champion.hp = Math.min(after.maxHp, he.champion.hp + Math.max(0, after.maxHp - before.maxHp));
  sim.c.champBuffSig = '';
  sim.c.statsDirty = true;
  return { ok: true };
}

/** Paragonpunkte kostenlos neu verteilen (3.4: jederzeit; Bollwerk-Kernenergie wird wieder abgezogen). */
export function resetParagon(sim: GameSimulation): CommandResult {
  const he = sim.s.he;
  if (!he?.paragon) return { ok: false, reason: 'Kein Paragon' };
  if (paragonSpent(he.paragon.points) <= 0) return { ok: false, reason: 'Keine Punkte vergeben' };
  const core = (he.paragon.points.pg_core ?? 0) * (PARAGON_NODES.find((n) => n.id === 'pg_core')?.per.coreLives ?? 0);
  sim.s.maxLives = Math.max(1, sim.s.maxLives - core);
  sim.s.lives = Math.max(1, Math.min(sim.s.maxLives, sim.s.lives - core));
  he.paragon.points = {};
  he.paragon.rev++;
  const st = championStats(he);
  he.champion.hp = Math.min(st.maxHp, he.champion.hp);
  sim.c.champBuffSig = '';
  sim.c.statsDirty = true;
  return { ok: true };
}

export function nodeState(sim: GameSimulation, node: SkillNode): { ok: boolean; reason: string | null } {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const c = he.champion;
  const rank = c.tree[node.id] ?? 0;
  if (rank >= node.maxRank) return { ok: false, reason: 'Maximaler Rang' };
  if (node.perk && !he.perks.includes(node.perk)) return { ok: false, reason: 'Benötigt den Perk „Meisterschaft“ (Welle 20)' };
  if (node.minLevel && c.level < node.minLevel) return { ok: false, reason: `Ab Heldenstufe ${node.minLevel}` };
  for (const r of node.requires ?? []) {
    if ((c.tree[r.id] ?? 0) < r.rank) {
      const other = CHAMPIONS[c.id].tree.find((n) => n.id === r.id);
      return { ok: false, reason: `Benötigt ${other?.name ?? r.id} Rang ${r.rank}` };
    }
  }
  if (c.skillPoints <= 0) return { ok: false, reason: 'Keine Fertigkeitspunkte' };
  return { ok: true, reason: null };
}

/** 3.4: Skillbaum des Helden kostenlos zurücksetzen – alle Punkte zurück, Kernwall-Kernenergie wird abgezogen. */
export function resetSkillTree(sim: GameSimulation): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const c = he.champion;
  const tree = CHAMPIONS[c.id].tree;
  let spent = 0;
  let core = 0;
  for (const n of tree) {
    const r = c.tree[n.id] ?? 0;
    spent += r;
    if (n.coreLives) core += r * n.coreLives;
  }
  if (spent <= 0) return { ok: false, reason: 'Keine Punkte vergeben' };
  sim.s.maxLives = Math.max(1, sim.s.maxLives - core);
  sim.s.lives = Math.max(1, Math.min(sim.s.maxLives, sim.s.lives - core));
  c.tree = {};
  c.skillPoints += spent;
  const st = championStats(he);
  c.hp = Math.min(st.maxHp, c.hp);
  c.mana = Math.min(st.maxMana, c.mana);
  sim.c.champBuffSig = '';
  sim.c.statsDirty = true;
  return { ok: true, message: `${spent} Punkte zurückerstattet` };
}

export function spendSkillPoint(sim: GameSimulation, nodeId: string): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const node = CHAMPIONS[he.champion.id].tree.find((n) => n.id === nodeId);
  if (!node) return { ok: false, reason: 'Unbekannter Knoten' };
  const st = nodeState(sim, node);
  if (!st.ok) return { ok: false, reason: st.reason ?? 'Nicht möglich' };
  const before = championStats(he);
  he.champion.tree[node.id] = (he.champion.tree[node.id] ?? 0) + 1;
  he.champion.skillPoints--;
  if (node.coreLives) {
    // Kernwall: mehr Kernenergie – die Zahl erlaubter Durchbrüche steigt sofort
    sim.s.lives += node.coreLives;
    sim.s.maxLives += node.coreLives;
  }
  const after = championStats(he);
  he.champion.hp = Math.min(after.maxHp, he.champion.hp + Math.max(0, after.maxHp - before.maxHp));
  sim.c.champBuffSig = '';
  return { ok: true };
}

// ------------------------------------------------------------ Turmverstärkungen

/**
 * Verstärkt einen Turm. Regeln: je Turm und Quelle genau ein Eintrag; erneutes Anwenden erneuert die
 * Dauer (ohne Verlängerung über die Grunddauer hinaus) und übernimmt jeweils den höheren Wert.
 * Verschiedene Quellen addieren sich, die Summe ist gedeckelt (HERO_ENDLESS.towerBuffCap).
 */
export function applyTowerBuff(sim: GameSimulation, tower: number, src: string, v: { dmg?: number; speed?: number; range?: number; crit?: number }, dur: number): void {
  const he = sim.s.he;
  if (!he || dur <= 0) return;
  const cur = he.towerBuffs.find((b) => b.tower === tower && b.src === src);
  if (cur) {
    cur.t = Math.max(cur.t, dur);
    cur.dur = Math.max(cur.dur, dur);
    cur.dmg = Math.max(cur.dmg, v.dmg ?? 0);
    cur.speed = Math.max(cur.speed, v.speed ?? 0);
    cur.range = Math.max(cur.range, v.range ?? 0);
    cur.crit = Math.max(cur.crit, v.crit ?? 0);
  } else {
    he.towerBuffs.push({ tower, src, t: dur, dur, dmg: v.dmg ?? 0, speed: v.speed ?? 0, range: v.range ?? 0, crit: v.crit ?? 0 });
  }
  const t = sim.c.towerByUid.get(tower);
  if (t) sim.emit({ k: 'towerBuff', x: t.cx + 0.5, y: t.cy + 0.5, color: CHAMPIONS[he.champion.id].accent });
  sim.c.champBuffSig = '';
}

function updateTowerBuffs(sim: GameSimulation, dt: number): void {
  const he = sim.s.he;
  if (!he) return;
  let changed = false;
  for (const b of he.towerBuffs) b.t -= dt;
  const before = he.towerBuffs.length;
  he.towerBuffs = he.towerBuffs.filter((b) => b.t > 0 && sim.c.towerByUid.has(b.tower));
  if (he.towerBuffs.length !== before) changed = true;
  sim.c.champBuffTimer -= dt;
  if (!changed && sim.c.champBuffTimer > 0 && sim.c.champBuffSig !== '') return;
  sim.c.champBuffTimer = 0.25;
  rebuildChampionBuffs(sim);
}

/** Aggregiert Fähigkeiten-Verstärkungen und die passive Aura je Turm (mit Obergrenzen). */
export function rebuildChampionBuffs(sim: GameSimulation): void {
  const he = sim.s.he;
  if (!he) return;
  const cap = HERO_ENDLESS.towerBuffCap;
  const map = new Map<number, { dmg: number; speed: number; range: number; crit: number }>();
  const get = (uid: number) => {
    let a = map.get(uid);
    if (!a) {
      a = { dmg: 0, speed: 0, range: 0, crit: 0 };
      map.set(uid, a);
    }
    return a;
  };
  for (const b of he.towerBuffs) {
    const a = get(b.tower);
    a.dmg += b.dmg;
    a.speed += b.speed;
    a.range += b.range;
    a.crit += b.crit;
  }
  const c = he.champion;
  const def = CHAMPIONS[c.id];
  if (c.dead <= 0) {
    const st = championStats(he);
    const sm = skillModsOf(c);
    const auraDmg = ((def.aura?.dmg ?? 0) + sm.auraDmg) * st.supportPower;
    const auraSpd = ((def.aura?.speed ?? 0) + sm.auraSpeed) * st.supportPower;
    if (auraDmg > 0 || auraSpd > 0 || sm.auraRange > 0 || sm.auraCrit > 0) {
      const r2 = st.supportRadius ** 2;
      for (const t of sim.s.towers) {
        if (TOWERS[t.def].attack === 'economy' || TOWERS[t.def].attack === 'none') continue;
        if (dist2(t.cx + 0.5, t.cy + 0.5, c.x, c.y) > r2) continue;
        const a = get(t.uid);
        a.dmg += auraDmg;
        a.speed += auraSpd;
        a.range += sm.auraRange;
        a.crit += sm.auraCrit;
      }
    }
  }
  for (const a of map.values()) {
    a.dmg = Math.min(cap.dmg, a.dmg);
    a.speed = Math.min(cap.speed, a.speed);
    a.range = Math.min(cap.range, a.range);
    a.crit = Math.min(cap.crit, a.crit);
  }
  const sig = [...map.entries()].sort((x, y) => x[0] - y[0]).map(([k, v]) => `${k}:${v.dmg.toFixed(3)}/${v.speed.toFixed(3)}/${v.range.toFixed(2)}/${v.crit.toFixed(2)}`).join(',');
  if (sig !== sim.c.champBuffSig) {
    sim.c.champBuff = map;
    sim.c.champBuffSig = sig || '-';
    sim.c.statsDirty = true;
  }
}

export function buffsOfTower(sim: GameSimulation, uid: number): TowerBuff[] {
  return sim.s.he?.towerBuffs.filter((b) => b.tower === uid) ?? [];
}

// ------------------------------------------------------------ Fähigkeiten

export function skillState(sim: GameSimulation, slot: number): { ok: boolean; reason: string | null; skill: ChampionSkill | null } {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held', skill: null };
  const c = he.champion;
  const skill = CHAMPIONS[c.id].skills[slot];
  if (!skill) return { ok: false, reason: 'Kein Skill', skill: null };
  if (c.dead > 0) return { ok: false, reason: 'Held ist besiegt', skill };
  if (c.level < skill.unlockLevel) return { ok: false, reason: `Ab Stufe ${skill.unlockLevel}`, skill };
  if (c.skillCd[slot] > 0) return { ok: false, reason: `Bereit in ${Math.ceil(c.skillCd[slot])} s`, skill };
  if (c.mana < skill.mana) return { ok: false, reason: 'Zu wenig Mana', skill };
  if (he.mining) return { ok: false, reason: `Baut gerade ab (${Math.ceil(he.mining.t)} s)`, skill };
  return { ok: true, reason: null, skill };
}

export function useChampionSkill(sim: GameSimulation, slot: number, tx: number, ty: number): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const c = he.champion;
  const chk = skillState(sim, slot);
  if (!chk.ok || !chk.skill) return { ok: false, reason: chk.reason ?? 'Nicht bereit' };
  const sk = chk.skill;
  const st = championStats(he);
  const sm = skillModsOf(c);
  if (sk.target !== 'self' && dist2(tx, ty, c.x, c.y) > (sk.castRange + 0.5) ** 2) return { ok: false, reason: 'Ziel außer Reichweite' };
  const def = CHAMPIONS[c.id];
  const sp = st.supportPower;
  const ap = st.abilityPower;
  const durMul = 1 + sm.buffDuration;
  const radMul = 1 + sm.buffRadius;
  const towersIn = (x: number, y: number, r: number) => sim.s.towers.filter((t) => TOWERS[t.def].attack !== 'economy' && TOWERS[t.def].attack !== 'none' && dist2(t.cx + 0.5, t.cy + 0.5, x, y) <= r * r);
  const enemiesIn = (x: number, y: number, r: number) => sim.s.enemies.filter((e) => e.alive && !e.stealthed && !e.burrowed && dist2(e.x, e.y, x, y) <= r * r);
  let r = sk.radius;
  if (sk.effects) {
    const res = applySkillEffects(sim, he, sk, tx, ty, st);
    if (!res.ok) return res;
    sim.emit({ k: 'champSkill', skill: sk.id, x: c.x, y: c.y, tx: res.tx, ty: res.ty, r: sk.radius || 0.8, color: def.accent });
    return finishSkill(sim, he, slot, sk, st, null);
  }
  switch (sk.id) {
    case 'inspire': {
      const t = sim.towerAt(Math.floor(tx), Math.floor(ty));
      if (!t || TOWERS[t.def].attack === 'economy' || TOWERS[t.def].attack === 'none') return { ok: false, reason: 'Ziel muss ein Kampfturm sein' };
      applyTowerBuff(sim, t.uid, 'inspire', { dmg: 0.5 * sp, speed: 0.3 * sp }, 8 * durMul);
      const extra = towersIn(t.cx + 0.5, t.cy + 0.5, 2.5).filter((o) => o !== t)
        .sort((a, b) => dist2(a.cx, a.cy, t.cx, t.cy) - dist2(b.cx, b.cy, t.cx, t.cy) || a.uid - b.uid).slice(0, Math.round(sm.extraTargets));
      for (const o of extra) applyTowerBuff(sim, o.uid, 'inspire', { dmg: 0.25 * sp, speed: 0.15 * sp }, 8 * durMul);
      tx = t.cx + 0.5;
      ty = t.cy + 0.5;
      break;
    }
    case 'field': {
      r = sk.radius * radMul;
      const ts = towersIn(tx, ty, r);
      const types = new Set(ts.map((t) => TOWERS[t.def].damageType).filter(Boolean));
      const syn = sm.synergy * types.size;
      for (const t of ts) applyTowerBuff(sim, t.uid, 'field', { speed: 0.35 * sp, range: 0.5, dmg: syn * sp }, 10 * durMul);
      break;
    }
    case 'pulse': {
      r = sk.radius * radMul;
      for (const t of towersIn(c.x, c.y, r)) {
        t.cooldown = 0;
        applyTowerBuff(sim, t.uid, 'pulse', { crit: 0.15 * sp }, 6 * durMul);
      }
      tx = c.x;
      ty = c.y;
      break;
    }
    case 'citadel': {
      r = sk.radius * radMul;
      for (const t of towersIn(tx, ty, r)) applyTowerBuff(sim, t.uid, 'citadel', { dmg: 0.5 * sp, speed: 0.4 * sp, range: 0.6, crit: 0.1 }, 12 * durMul);
      break;
    }
    case 'whirl': {
      r = sk.radius * (1 + sm.whirlRadius);
      for (const e of enemiesIn(c.x, c.y, r)) hitEnemy(sim, he, e, (60 + 1.5 * st.damage) * ap, false);
      tx = c.x;
      ty = c.y;
      break;
    }
    case 'charge': {
      const tcx = Math.floor(tx);
      const tcy = Math.floor(ty);
      if (!championWalkable(sim, tcx, tcy)) return { ok: false, reason: 'Ziel ist nicht begehbar' };
      const x0 = c.x;
      const y0 = c.y;
      const len = Math.hypot(tx - x0, ty - y0) || 1;
      const ux = (tx - x0) / len;
      const uy = (ty - y0) / len;
      for (const e of sim.s.enemies) {
        if (!e.alive || e.stealthed || e.burrowed || e.flying) continue;
        const rx = e.x - x0;
        const ry = e.y - y0;
        const along = rx * ux + ry * uy;
        if (along < -0.3 || along > len + 0.3) continue;
        if (Math.abs(rx * uy - ry * ux) > 0.8) continue;
        hitEnemy(sim, he, e, (80 + 1.7 * st.damage) * ap, false);
        if (e.alive && !e.boss) addStatus(e, 'stun', 1.5 + sm.dashStun, 1, CHAMPION_UID);
      }
      c.x = tcx + 0.5;
      c.y = tcy + 0.5;
      c.hx = c.x;
      c.hy = c.y;
      c.path = [];
      c.facing = Math.atan2(uy, ux);
      sim.emit({ k: 'champSkill', skill: sk.id, x: x0, y: y0, tx: c.x, ty: c.y, r: 0.8, color: def.accent });
      return finishSkill(sim, he, slot, sk, st, null);
    }
    case 'warcry':
      c.buffs = c.buffs.filter((b) => b.src !== 'warcry');
      c.buffs.push({ src: 'warcry', t: 6, dur: 6, add: { attackSpeed: st.attackSpeed * 0.4, armor: 0.2 } });
      c.hp = Math.min(st.maxHp, c.hp + st.maxHp * 0.15);
      // Herausforderung: Bodengegner in der Nähe werden gefesselt
      for (const e of enemiesIn(c.x, c.y, 3 * radMul)) if (!e.boss && !e.flying) addStatus(e, 'root', 2, 1, CHAMPION_UID);
      tx = c.x;
      ty = c.y;
      break;
    case 'bladestorm':
      c.channel = 5;
      c.channelTick = 0;
      tx = c.x;
      ty = c.y;
      break;
    case 'pierce': {
      const len0 = Math.hypot(tx - c.x, ty - c.y) || 1;
      const ux = (tx - c.x) / len0;
      const uy = (ty - c.y) / len0;
      const reach = sk.castRange + 1;
      const width = 0.45 + sm.pierceWidth;
      for (const e of sim.s.enemies) {
        if (!e.alive || e.stealthed || e.burrowed) continue;
        const rx = e.x - c.x;
        const ry = e.y - c.y;
        const along = rx * ux + ry * uy;
        if (along < 0 || along > reach) continue;
        if (Math.abs(rx * uy - ry * ux) > width) continue;
        hitEnemy(sim, he, e, (50 + 1.4 * st.damage) * ap, true);
      }
      tx = c.x + ux * reach;
      ty = c.y + uy * reach;
      c.facing = Math.atan2(uy, ux);
      break;
    }
    case 'trap':
      he.zones.push({ uid: sim.uid(), kind: 'snare', x: tx, y: ty, r: sk.radius, t: 6, tick: 0, power: (0.35 + sm.trapSlow) });
      break;
    case 'focus':
      c.buffs = c.buffs.filter((b) => b.src !== 'focus');
      c.buffs.push({ src: 'focus', t: 8, dur: 8, add: { critChance: 0.35, attackSpeed: st.attackSpeed * 0.2 } });
      tx = c.x;
      ty = c.y;
      break;
    case 'volley':
      he.zones.push({ uid: sim.uid(), kind: 'volley', x: tx, y: ty, r: sk.radius, t: 0.5 * (3 + sm.volleyPulses) - 0.01, tick: 0, power: (30 + 0.8 * st.damage) * ap });
      break;
    default:
      return { ok: false, reason: 'Unbekannte Fähigkeit' };
  }
  sim.emit({ k: 'champSkill', skill: sk.id, x: c.x, y: c.y, tx, ty, r, color: def.accent });
  return finishSkill(sim, he, slot, sk, st, null);
}

/** Datengetriebene Fähigkeiten (Kampagnenhelden). Prüft zuerst alle Bedingungen, wirkt dann. */
function applySkillEffects(sim: GameSimulation, he: HeroEndlessState, sk: ChampionSkill, tx: number, ty: number, st: ChampionStats): { ok: true; tx: number; ty: number } | { ok: false; reason: string } {
  const c = he.champion;
  const effects = sk.effects ?? [];
  const ap = st.abilityPower;
  const sp = st.supportPower;
  const sm = skillModsOf(c);
  const durMul = 1 + sm.buffDuration;
  const radMul = 1 + sm.buffRadius;
  const isCombatTower = (def: string) => TOWERS[def as keyof typeof TOWERS].attack !== 'economy' && TOWERS[def as keyof typeof TOWERS].attack !== 'none';
  if (sk.target === 'tower') {
    const t = sim.towerAt(Math.floor(tx), Math.floor(ty));
    if (!t || !isCombatTower(t.def)) return { ok: false, reason: 'Ziel muss ein Kampfturm sein' };
    tx = t.cx + 0.5;
    ty = t.cy + 0.5;
  }
  if (effects.some((e) => e.kind === 'blink') && !championWalkable(sim, Math.floor(tx), Math.floor(ty))) return { ok: false, reason: 'Ziel ist nicht begehbar' };
  const center = (at: 'self' | 'target' | undefined): [number, number] => (at === 'target' ? [tx, ty] : [c.x, c.y]);
  const foes = (x: number, y: number, rad: number, air = true) => sim.s.enemies.filter((e) => e.alive && !e.stealthed && !e.burrowed && (air || !e.flying) && (rad <= 0 || dist2(e.x, e.y, x, y) <= rad * rad));
  const status = (e: EnemyInstance, s?: { id: string; dur: number; power?: number; stacks?: number }) => {
    if (!s || !e.alive) return;
    if ((s.id === 'stun' || s.id === 'root') && (e.boss || (s.id === 'root' && e.flying))) return;
    addStatus(e, s.id as never, s.dur, s.power ?? 1, CHAMPION_UID, s.stacks ?? 1);
  };
  for (const ef of effects) {
    switch (ef.kind) {
      case 'blink':
        c.x = Math.floor(tx) + 0.5;
        c.y = Math.floor(ty) + 0.5;
        c.hx = c.x;
        c.hy = c.y;
        c.path = [];
        break;
      case 'nova': {
        const [x, y] = center(ef.at);
        for (const e of foes(x, y, ef.radius * (1 + sm.whirlRadius), ef.air !== false)) {
          hitEnemy(sim, he, e, (ef.base + ef.scale * st.damage) * ap, true);
          status(e, ef.status);
        }
        break;
      }
      case 'line': {
        const len0 = Math.hypot(tx - c.x, ty - c.y) || 1;
        const ux = (tx - c.x) / len0;
        const uy = (ty - c.y) / len0;
        for (const e of foes(c.x, c.y, 0)) {
          const rx = e.x - c.x;
          const ry = e.y - c.y;
          const along = rx * ux + ry * uy;
          if (along < 0 || along > ef.length || Math.abs(rx * uy - ry * ux) > ef.width + sm.pierceWidth) continue;
          hitEnemy(sim, he, e, (ef.base + ef.scale * st.damage) * ap, true);
          status(e, ef.status);
        }
        tx = c.x + ux * ef.length;
        ty = c.y + uy * ef.length;
        c.facing = Math.atan2(uy, ux);
        break;
      }
      case 'execute': {
        const list = foes(c.x, c.y, 0).sort((a, b) => a.remaining - b.remaining).slice(0, ef.count);
        for (const e of list) hitEnemy(sim, he, e, e.maxHp * (e.boss ? ef.bossPct : ef.pct) * ap, false);
        break;
      }
      case 'towers': {
        const [x, y] = center(ef.at);
        const rad = ef.radius * radMul;
        for (const t of sim.s.towers) {
          if (dist2(t.cx + 0.5, t.cy + 0.5, x, y) > rad * rad) continue;
          if (ef.repair) {
            t.stunned = 0;
            t.disrupted = 0;
          }
          if (!isCombatTower(t.def)) continue;
          applyTowerBuff(sim, t.uid, ef.src, { dmg: (ef.dmg ?? 0) * sp, speed: (ef.speed ?? 0) * sp, range: ef.range ?? 0, crit: ef.crit ?? 0 }, ef.dur * durMul);
        }
        break;
      }
      case 'allTowers':
        for (const t of sim.s.towers) if (isCombatTower(t.def)) applyTowerBuff(sim, t.uid, ef.src, { dmg: (ef.dmg ?? 0) * sp, speed: (ef.speed ?? 0) * sp, range: ef.range ?? 0, crit: ef.crit ?? 0 }, ef.dur * durMul);
        break;
      case 'self': {
        if (ef.dur > 0) {
          const add: Partial<ChampionStats> = { ...(ef.add ?? {}) };
          for (const [k, v] of Object.entries(ef.pct ?? {}) as [keyof ChampionStats, number][]) add[k] = (add[k] ?? 0) + st[k] * v;
          c.buffs = c.buffs.filter((b) => b.src !== ef.src);
          c.buffs.push({ src: ef.src, t: ef.dur, dur: ef.dur, add });
        }
        if (ef.heal) c.hp = Math.min(st.maxHp, c.hp + st.maxHp * ef.heal);
        break;
      }
      case 'strikes': {
        const [x, y] = center(ef.at);
        const pool = foes(x, y, ef.radius);
        for (let k = 0; k < ef.count && pool.length; k++) {
          const e = pool[Math.floor(sim.rand() * pool.length) % pool.length];
          hitEnemy(sim, he, e, (ef.base + ef.scale * st.damage) * ap, true);
          status(e, ef.status);
          sim.emit({ k: 'champSkill', skill: 'strike', x: e.x, y: e.y, tx: e.x, ty: e.y, r: 0.6, color: CHAMPIONS[c.id].accent });
        }
        break;
      }
      case 'statusAll': {
        const [x, y] = center(ef.at);
        for (const e of sim.s.enemies) {
          if (!e.alive) continue;
          if (ef.radius > 0 && dist2(e.x, e.y, x, y) > (ef.radius * radMul) ** 2) continue;
          if (ef.status.id !== 'reveal' && (e.stealthed || e.burrowed)) continue;
          status(e, ef.status);
        }
        break;
      }
      case 'taunt':
        for (const e of foes(c.x, c.y, ef.radius * radMul, false)) status(e, { id: 'root', dur: ef.dur });
        break;
      case 'interrupt':
        for (const e of sim.s.enemies) if (e.alive && dist2(e.x, e.y, c.x, c.y) <= ef.radius * ef.radius) interruptEnemy(sim, e, ef.power * ap);
        break;
      case 'gold': {
        const g = Math.round((ef.base + ef.perWave * Math.max(1, sim.s.wave)) * (1 + st.lootBonus * 0.5));
        sim.s.gold += g;
        sim.s.stats.goldEarned += g;
        if (ef.resource && ef.amount) addResource(he, ef.resource, ef.amount);
        sim.emit({ k: 'pickup', kind: 'resource', x: c.x, y: c.y, label: `+${g} Gold${ef.resource && ef.amount ? ` · +${ef.amount}` : ''}`, color: 0xffd84a });
        break;
      }
      case 'zone':
        he.zones.push({ uid: sim.uid(), kind: ef.zone, x: tx, y: ty, r: ef.radius, t: ef.dur, tick: 0, power: ef.power + (ef.scale ?? 0) * st.damage });
        break;
      default:
        break;
    }
  }
  return { ok: true, tx, ty };
}

function finishSkill(sim: GameSimulation, he: HeroEndlessState, slot: number, sk: ChampionSkill, st: ChampionStats, _x: null): CommandResult {
  const c = he.champion;
  c.mana -= sk.mana;
  c.skillCd[slot] = sk.cooldown * (1 - st.cdr);
  c.anim = 0.4;
  sim.c.champBuffSig = '';
  return { ok: true };
}

/** Heldenschaden an einem Gegner (Krit, Lebensraub). */
function hitEnemy(sim: GameSimulation, he: HeroEndlessState, e: EnemyInstance, amount: number, canCrit: boolean): number {
  const c = he.champion;
  const st = championStats(he);
  let dmg = amount;
  if (canCrit && sim.rand() < st.critChance) dmg *= st.critDamage;
  const dealt = dealDamage(sim, e, dmg, CHAMPIONS[c.id].damageType, CHAMPION_UID);
  c.damageDealt += dealt;
  if (st.lifesteal > 0) c.hp = Math.min(st.maxHp, c.hp + dealt * st.lifesteal);
  if (!e.alive) c.kills++;
  return dealt;
}

setGrantXpHook((sim, amount) => grantXp(sim, amount));

// ------------------------------------------------------------ Takt

export function updateChampion(sim: GameSimulation, dt: number): void {
  const he = sim.s.he;
  if (!he) return;
  // Eigene Blockaden jeden Takt neu vergeben (die Lane-Helden setzen ihre selbst zurück)
  for (const e of sim.s.enemies) if (e.blockedBy === CHAMPION_UID) e.blockedBy = 0;
  const s = sim.s;
  const c = he.champion;
  updateTowerBuffs(sim, dt);
  updatePickups(sim, dt);
  updateChampionZones(sim, dt);
  for (let i = 0; i < c.skillCd.length; i++) c.skillCd[i] = Math.max(0, c.skillCd[i] - dt);
  for (const k of Object.keys(c.potionCd) as (keyof typeof c.potionCd)[]) c.potionCd[k] = Math.max(0, (c.potionCd[k] ?? 0) - dt);
  if (c.buffs.length) {
    for (const b of c.buffs) b.t -= dt;
    c.buffs = c.buffs.filter((b) => b.t > 0);
  }
  c.anim = Math.max(0, c.anim - dt);
  if (c.dead > 0) {
    c.dead -= dt;
    if (c.dead <= 0) {
      c.dead = 0;
      const st = championStats(he);
      c.hp = st.maxHp;
      c.mana = st.maxMana;
      sim.emit({ k: 'champRespawn', x: c.x, y: c.y });
      sim.c.champBuffSig = '';
    }
    return;
  }
  const st = championStats(he);
  c.hp = Math.min(st.maxHp, c.hp + st.hpRegen * dt);
  if (he.mining) {
    // Abbau: Held steht still, greift nicht an und kann nicht fallen (nicht unterbrechbar)
    c.mana = Math.min(st.maxMana, c.mana + st.manaRegen * dt);
    c.path = [];
    collectPickups(sim, st.pickupRadius);
    return;
  }
  c.mana = Math.min(st.maxMana, c.mana + st.manaRegen * dt);
  const def = CHAMPIONS[c.id];

  // Kanalisierter Klingensturm
  let speed = st.moveSpeed;
  if (c.channel > 0) {
    c.channel -= dt;
    c.channelTick -= dt;
    speed *= 0.7;
    if (c.channelTick <= 0) {
      c.channelTick = 0.5;
      for (const e of s.enemies) {
        if (e.alive && !e.stealthed && !e.burrowed && dist2(e.x, e.y, c.x, c.y) <= 4) {
          hitEnemy(sim, he, e, (35 + 0.8 * st.damage) * st.abilityPower, false);
          if (e.alive) addStatus(e, 'chill', 1, 0.15, CHAMPION_UID, 3);
        }
      }
      sim.emit({ k: 'champSkill', skill: 'bladestormTick', x: c.x, y: c.y, tx: c.x, ty: c.y, r: 2, color: def.accent });
    }
  }

  // Blocker (Brann, Kaela …): hält Bodengegner in Nahkampfreichweite fest, solange er steht
  if (def.blockCount && c.path.length === 0 && c.dead <= 0) {
    const near = s.enemies
      .filter((e) => e.alive && !e.boss && !e.flying && !e.burrowed && e.blockedBy === 0 && dist2(e.x, e.y, c.x, c.y) <= 0.85 * 0.85)
      .sort((a, b) => dist2(a.x, a.y, c.x, c.y) - dist2(b.x, b.y, c.x, c.y))
      .slice(0, def.blockCount);
    for (const e of near) e.blockedBy = CHAMPION_UID;
  }

  // Bewegung
  if (c.path.length) {
    moveAlong(sim, c, speed, dt);
  } else if (def.melee && !c.holdPos) {
    // Nahkämpfer verfolgen Gegner nahe ihres Haltepunkts und kehren danach zurück.
    const tgt = c.target ? sim.c.enemyByUid.get(c.target) : undefined;
    let gx = c.hx;
    let gy = c.hy;
    if (tgt && tgt.alive && !tgt.flying && dist2(tgt.x, tgt.y, c.hx, c.hy) <= 2.5 * 2.5 && dist2(tgt.x, tgt.y, c.x, c.y) > (st.range * 0.85) ** 2) {
      gx = tgt.x;
      gy = tgt.y;
    }
    const d = Math.hypot(gx - c.x, gy - c.y);
    if (d > 0.05 && !(tgt && dist2(tgt.x, tgt.y, c.x, c.y) <= (st.range * 0.85) ** 2)) {
      const step = Math.min(d, speed * dt);
      const nx = c.x + ((gx - c.x) / d) * step;
      const ny = c.y + ((gy - c.y) / d) * step;
      if (championWalkable(sim, Math.floor(nx), Math.floor(ny))) {
        c.x = nx;
        c.y = ny;
        c.facing = Math.atan2(gy - c.y, gx - c.x);
      }
    }
  }

  // Ziel wählen und angreifen (nicht während des Laufens über einen Befehl)
  c.attackCd = Math.max(0, c.attackCd - dt);
  if (!c.path.length) {
    const reach = def.melee ? Math.max(st.range, 2.5) : st.range;
    let cur = c.target ? sim.c.enemyByUid.get(c.target) : undefined;
    if (!cur || !cur.alive || cur.stealthed || cur.burrowed || dist2(cur.x, cur.y, c.x, c.y) > (reach + 0.4) ** 2 || (def.melee && cur.flying)) {
      cur = undefined;
      let bd = reach * reach;
      for (const e of s.enemies) {
        if (!e.alive || e.stealthed || e.burrowed || (def.melee && e.flying)) continue;
        const d = dist2(e.x, e.y, c.x, c.y);
        if (d < bd) {
          bd = d;
          cur = e;
        }
      }
      c.target = cur ? cur.uid : 0;
    }
    if (cur && c.attackCd <= 0 && dist2(cur.x, cur.y, c.x, c.y) <= (st.range + 0.3) ** 2) {
      c.attackCd = 1 / st.attackSpeed;
      c.facing = Math.atan2(cur.y - c.y, cur.x - c.x);
      c.anim = 0.3;
      const tx = cur.x;
      const ty = cur.y;
      hitEnemy(sim, he, cur, st.damage, true);
      if (st.cleave > 0) {
        for (const e of s.enemies) {
          if (e === cur || !e.alive || e.flying || e.stealthed || dist2(e.x, e.y, tx, ty) > 1) continue;
          hitEnemy(sim, he, e, st.damage * st.cleave, false);
        }
      }
      sim.emit({ k: 'champAttack', x: c.x, y: c.y, tx, ty, melee: def.melee, color: def.accent });
    }
  }

  // Gegner verletzen den Helden bei Kontakt
  if (s.phase === 'combat') {
    const scale = 1 + HERO_ENDLESS.enemyDamagePerWave * s.wave;
    const cr2 = HERO_ENDLESS.contactRange ** 2;
    let dmg = 0;
    for (const e of s.enemies) {
      if (!e.alive || e.flying || e.burrowed) continue;
      if (dist2(e.x, e.y, c.x, c.y) > cr2 * (e.boss ? 2.5 : 1)) continue;
      dmg += enemyContactDps(e) * (e.miniBoss ? 2 : 1) * scale * dt;
    }
    if (dmg > 0) c.hp -= dmg * (1 - st.armor);
  }
  if (c.hp <= 0) {
    c.hp = 0;
    c.dead = (HERO_ENDLESS.respawnBase + HERO_ENDLESS.respawnPerLevel * c.level) * Math.max(0.5, 1 - runPassive(he).respawnPct);
    c.path = [];
    c.channel = 0;
    c.target = 0;
    c.buffs = [];
    sim.emit({ k: 'champDown', x: c.x, y: c.y });
    sim.emit({ k: 'message', text: `${def.name} ist gefallen – Wiederbelebung am Kern in ${Math.ceil(c.dead)} s.`, severity: 'warn' });
    const home = championHome(s.map, he.shop);
    c.x = home.x;
    c.y = home.y;
    c.hx = c.x;
    c.hy = c.y;
    sim.c.champBuffSig = '';
    return;
  }
  collectPickups(sim, st.pickupRadius);
}

function updateChampionZones(sim: GameSimulation, dt: number): void {
  const he = sim.s.he;
  if (!he || he.zones.length === 0) return;
  for (const z of he.zones) {
    z.t -= dt;
    z.tick -= dt;
    if (z.kind === 'snare') {
      for (const e of sim.s.enemies) {
        if (!e.alive || e.flying || dist2(e.x, e.y, z.x, z.y) > z.r * z.r) continue;
        addStatus(e, 'chill', 0.4, Math.min(0.7, z.power) / 2, CHAMPION_UID, 2);
        if (z.tick <= 0) dealDamage(sim, e, 6 + sim.s.wave * 1.2, 'physical', CHAMPION_UID);
      }
      if (z.tick <= 0) z.tick = 0.5;
    } else if (z.kind === 'volley' && z.tick <= 0) {
      z.tick = 0.5;
      for (const e of sim.s.enemies) {
        if (!e.alive || e.stealthed || e.burrowed || dist2(e.x, e.y, z.x, z.y) > z.r * z.r) continue;
        const dealt = dealDamage(sim, e, z.power, 'physical', CHAMPION_UID);
        he.champion.damageDealt += dealt;
      }
      sim.emit({ k: 'champSkill', skill: 'volleyTick', x: z.x, y: z.y, tx: z.x, ty: z.y, r: z.r, color: CHAMPIONS[he.champion.id].accent });
    }
  }
  he.zones = he.zones.filter((z) => z.t > 0);
}
