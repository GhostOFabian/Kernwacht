import { RUNE_ORDER, RUNE_RULES, RUNE_SLOT_WAVES, RUNE_TIERS, runeEligible, runeLabel, runeMods, runeSockets, type RuneId } from '../config/runes.ts';
import type { RuneInstance, StatMod, TowerId, TowerInstance } from '../core/types.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { lootRand } from './LootSystem.ts';

// Runen (2.7): nur findbar ab Welle 250, in Turmsockel einsetzbar, jederzeit kostenlos wieder herausnehmbar.

export function runesOf(sim: GameSimulation): RuneInstance[] {
  const list = sim.s.he?.runes ?? [];
  // 3.3: Altstände (Rune an einem einzelnen Turm) in die Turmsorte übernehmen; Überzähliges zurück in den Beutel
  if (list.some((r) => r.tower > 0)) {
    for (const r of list) {
      if (r.tower <= 0) continue;
      const t = sim.c.towerByUid.get(r.tower) ?? sim.s.towers.find((x) => x.uid === r.tower);
      r.tower = 0;
      if (!t || r.def) continue;
      const used = list.filter((o) => o.def === t.def).length;
      if (used < runeSockets(t.def, sim.s.wave)) r.def = t.def;
    }
  }
  return list;
}

/** Gesockelte Runen einer Turmsorte. */
export function defRunes(sim: GameSimulation, def: TowerId): RuneInstance[] {
  return runesOf(sim).filter((r) => r.def === def);
}

/** Runen, die auf diesen Turm wirken (die seiner Sorte). */
export function towerRunes(sim: GameSimulation, t: TowerInstance): RuneInstance[] {
  return defRunes(sim, t.def);
}

export function bagRunes(sim: GameSimulation): RuneInstance[] {
  return runesOf(sim).filter((r) => !r.def);
}

/** Wertänderungen aller Runen der Sorte eines Turms. */
export function towerRuneMods(sim: GameSimulation, t: TowerInstance): StatMod[] {
  const out: StatMod[] = [];
  for (const r of towerRunes(sim, t)) out.push(...runeMods(r.rune, r.tier));
  return out;
}

/** Zusätzliches Kopfgeld durch Gierrunen der Sorte des erlegenden Turms. */
export function runeKillGold(sim: GameSimulation, t: TowerInstance): number {
  let g = 0;
  for (const r of towerRunes(sim, t)) if (r.rune === 'greed') g += RUNE_TIERS[r.tier - 1].mul;
  return Math.round(g);
}

/** Rune in die Sorte des Turms sockeln – gilt sofort für alle Türme dieser Sorte. */
export function socketRune(sim: GameSimulation, towerUid: number, runeUid: number): CommandResult {
  const t = sim.c.towerByUid.get(towerUid);
  if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
  return socketRuneDef(sim, t.def, runeUid);
}

export function socketRuneDef(sim: GameSimulation, def: TowerId, runeUid: number): CommandResult {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Nur im Helden-Endlosmodus' };
  const r = runesOf(sim).find((x) => x.uid === runeUid);
  if (!r) return { ok: false, reason: 'Rune nicht gefunden' };
  if (r.def === def) return { ok: false, reason: 'Bereits gesockelt' };
  const cap = runeSockets(def, sim.s.wave);
  if (cap <= 0) return { ok: false, reason: runeEligible(def) ? `Runensockel ab Welle ${RUNE_SLOT_WAVES[0]}` : 'Diese Sorte hat keine Runensockel' };
  if (defRunes(sim, def).length >= cap) return { ok: false, reason: `Alle ${cap} Sockel dieser Sorte belegt` };
  r.def = def;
  r.tower = 0;
  sim.c.statsDirty = true;
  return { ok: true };
}

export function unsocketRune(sim: GameSimulation, runeUid: number): CommandResult {
  const r = runesOf(sim).find((x) => x.uid === runeUid);
  if (!sim.s.he || !r) return { ok: false, reason: 'Rune nicht gefunden' };
  if (!r.def) return { ok: false, reason: 'Liegt bereits im Beutel' };
  r.def = undefined;
  sim.c.statsDirty = true;
  return { ok: true };
}

/** 3.3: Runen gehören der Turmsorte und bleiben beim Verkauf/bei Fusionen gesockelt. */
export function releaseRunes(_sim: GameSimulation, _towerUid: number): void {}

function rollTier(sim: GameSimulation, wave: number): number {
  if (wave < RUNE_RULES.fromWave) return 1;
  const v = lootRand(sim);
  const t3 = Math.min(0.3, 0.05 + (wave - RUNE_RULES.fromWave) / 1000);
  if (v < t3) return 3;
  if (v < t3 + 0.3) return 2;
  return 1;
}

/** Rune finden (Gegner oder Wellenabschluss) – erst ab Welle 250. */
export function dropRune(sim: GameSimulation, x: number, y: number): RuneInstance | null {
  const he = sim.s.he;
  if (!he || sim.s.wave < RUNE_RULES.splinterFrom) return null;
  he.runes ??= [];
  const id: RuneId = RUNE_ORDER[Math.floor(lootRand(sim) * RUNE_ORDER.length) % RUNE_ORDER.length];
  const tier = rollTier(sim, sim.s.wave);
  if (bagRunes(sim).length >= RUNE_RULES.bagMax) {
    // Beutel voll: schwächste Rune im Beutel wird ersetzt, wenn die neue besser ist
    const weakest = bagRunes(sim).sort((a, b) => a.tier - b.tier || a.uid - b.uid)[0];
    if (!weakest || weakest.tier >= tier) return null;
    he.runes = he.runes.filter((r) => r !== weakest);
  }
  const inst: RuneInstance = { uid: sim.uid(), rune: id, tier, tower: 0 };
  he.runes.push(inst);
  sim.emit({ k: 'pickup', kind: 'resource', x, y, label: `✦ ${runeLabel(id, tier)}`, color: 0xc0a0ff });
  sim.emit({ k: 'message', text: `Rune gefunden: ${runeLabel(id, tier)} – im Turmpanel sockeln.`, severity: 'info' });
  return inst;
}

export function rollEnemyRune(sim: GameSimulation, tier: keyof typeof RUNE_RULES.dropChance, x: number, y: number): void {
  if (!sim.s.he || sim.s.wave < RUNE_RULES.splinterFrom) return;
  if (lootRand(sim) < RUNE_RULES.dropChance[tier]) dropRune(sim, x, y);
}
