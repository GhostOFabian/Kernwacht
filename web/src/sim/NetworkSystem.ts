import { RESONANCES, TOWER_MAX_PYLON_LINKS } from '../config/elements.ts';
import { TOWERS } from '../config/towers.ts';
import type { NetworkLink, TowerInstance } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { dealDamage } from './DamageSystem.ts';
import { computeTowerStats } from './TowerSystem.ts';

// Turmnetzwerke. Neuaufbau nur bei Dirty-Flag (Bau, Verkauf, Upgrade, Störung) – nie pro Frame.
// Regeln:
//  * Nur Leitpylonen erzeugen Verbindungen (zu Türmen und anderen Pylonen in linkRange).
//  * Pylone haben eine begrenzte Anzahl Verbindungen (maxLinks), Türme max. TOWER_MAX_PYLON_LINKS.
//  * Gestörte Türme/Pylone (Saboteur, Nullpuls) sind ausgeschlossen.
//  * Bonus eines Netzes = stärkster Pylon (nicht additiv) → keine unkontrollierte Stapelung.

export function rebuildNetwork(sim: GameSimulation): void {
  const s = sim.s;
  const c = sim.c;
  c.linked.clear();
  c.resonanceByTower.clear();
  c.networkBonusByTower.clear();
  const active = s.towers.filter((t) => TOWERS[t.def].networkable && t.disrupted <= 0);
  const pylons = active.filter((t) => t.def === 'pylon').sort((a, b) => a.uid - b.uid);
  const links: NetworkLink[] = [];
  const used = new Map<number, number>();
  const has = new Set<string>();
  const pylonStats = new Map<number, ReturnType<typeof computeTowerStats>>();
  for (const p of pylons) pylonStats.set(p.uid, computeTowerStats(sim, p, false));
  for (const p of pylons) {
    const ps = pylonStats.get(p.uid);
    if (!ps) continue;
    const r2 = ps.linkRange * ps.linkRange;
    const cands = active
      .filter((t) => t !== p && (t.cx - p.cx) ** 2 + (t.cy - p.cy) ** 2 <= r2)
      .sort((a, b) => ((a.cx - p.cx) ** 2 + (a.cy - p.cy) ** 2) - ((b.cx - p.cx) ** 2 + (b.cy - p.cy) ** 2) || a.uid - b.uid);
    for (const t of cands) {
      if ((used.get(p.uid) ?? 0) >= Math.round(ps.maxLinks)) break;
      const key = p.uid < t.uid ? `${p.uid}-${t.uid}` : `${t.uid}-${p.uid}`;
      if (has.has(key)) continue;
      const cap = t.def === 'pylon' ? Math.round(pylonStats.get(t.uid)?.maxLinks ?? 0) : TOWER_MAX_PYLON_LINKS;
      if ((used.get(t.uid) ?? 0) >= cap) continue;
      has.add(key);
      links.push({ a: p.uid, b: t.uid });
      used.set(p.uid, (used.get(p.uid) ?? 0) + 1);
      used.set(t.uid, (used.get(t.uid) ?? 0) + 1);
    }
  }
  // Union-Find
  const parent = new Map<number, number>();
  const find = (x: number): number => {
    let r = x;
    while (parent.get(r) !== r) r = parent.get(r) as number;
    let y = x;
    while (parent.get(y) !== r) {
      const n = parent.get(y) as number;
      parent.set(y, r);
      y = n;
    }
    return r;
  };
  for (const l of links) {
    if (!parent.has(l.a)) parent.set(l.a, l.a);
    if (!parent.has(l.b)) parent.set(l.b, l.b);
    const ra = find(l.a);
    const rb = find(l.b);
    if (ra !== rb) parent.set(Math.max(ra, rb), Math.min(ra, rb));
  }
  const groupsByRoot = new Map<number, number[]>();
  for (const uid of [...parent.keys()].sort((a, b) => a - b)) {
    const r = find(uid);
    const g = groupsByRoot.get(r) ?? [];
    g.push(uid);
    groupsByRoot.set(r, g);
  }
  const groups: { members: number[]; bonus: number; resonances: string[] }[] = [];
  for (const members of groupsByRoot.values()) {
    const towers = members.map((u) => c.towerByUid.get(u)).filter((t): t is TowerInstance => !!t);
    let bonus = 0;
    for (const t of towers) {
      if (t.def === 'pylon') bonus = Math.max(bonus, pylonStats.get(t.uid)?.networkBonus ?? 0);
    }
    bonus *= c.mods.networkBonusMul;
    const defs = new Set(towers.map((t) => t.def));
    const res = RESONANCES.filter((r) => r.requires.every((d) => defs.has(d))).map((r) => r.id);
    const gi = groups.length;
    groups.push({ members, bonus, resonances: res });
    for (const t of towers) {
      c.linked.set(t.uid, gi);
      c.networkBonusByTower.set(t.uid, bonus);
      if (res.length) c.resonanceByTower.set(t.uid, res);
    }
    if (members.length > s.stats.maxNetworkSize) s.stats.maxNetworkSize = members.length;
  }
  s.network = { links, groups, version: s.network.version + 1 };
}

/** Modifikator "Netzentladung": Verbindungslinien schaden kreuzenden Gegnern. */
export function updateNetworkDischarge(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  if (!sim.c.mods.netDischarge || s.network.links.length === 0) return;
  s.netZapTimer -= dt;
  if (s.netZapTimer > 0) return;
  s.netZapTimer = 1;
  for (const l of s.network.links) {
    const a = sim.c.towerByUid.get(l.a);
    const b = sim.c.towerByUid.get(l.b);
    if (!a || !b) continue;
    const dmg = sim.stats(a).zapDamage || 8;
    const ax = a.cx + 0.5;
    const ay = a.cy + 0.5;
    const bx = b.cx + 0.5;
    const by = b.cy + 0.5;
    const vx = bx - ax;
    const vy = by - ay;
    const len2 = vx * vx + vy * vy || 1;
    for (const e of s.enemies) {
      if (!e.alive) continue;
      const t = Math.max(0, Math.min(1, ((e.x - ax) * vx + (e.y - ay) * vy) / len2));
      const px = ax + vx * t;
      const py = ay + vy * t;
      if ((e.x - px) ** 2 + (e.y - py) ** 2 <= 0.45 * 0.45) {
        dealDamage(sim, e, dmg, 'lightning', a.uid);
        sim.emit({ k: 'hit', type: 'lightning', x: e.x, y: e.y, amount: dmg, splash: 0 });
      }
    }
  }
}
