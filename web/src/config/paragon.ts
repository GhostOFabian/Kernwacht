import type { PassiveEffects } from './passiveTree.ts';

// Helden-Endlos 2.7: dauerhafte Heldenstufen (bis 99) und Paragon (bis 500, gemeinsam für alle Helden).
// Ab Heldenstufe 99 fließt jede Erfahrung in Paragon-EP. Jede Paragonstufe gibt einen Punkt, der in einem
// von drei Bäumen (Offensive, Defensive, Utility) vergeben wird. Die Wirkungen nutzen dieselben Kennzahlen
// wie der passive Baum und gelten automatisch in jedem Lauf.

export const PARAGON = {
  heroMaxLevel: 99,
  maxLevel: 500,
  /** EP für Paragon P → P+1 = xpBase · (1 + P · xpGrowth) */
  xpBase: 8000,
  xpGrowth: 1 / 250,
  /** Stufenwerte flachen ab Stufe 30 ab (nur noch halber Zuwachs je Stufe). */
  softcapLevel: 30,
  softcapMul: 0.5,
};

export type ParagonTreeId = 'off' | 'def' | 'util' | 'tower';

export const PARAGON_TREES: { id: ParagonTreeId; name: string; icon: string; color: string }[] = [
  { id: 'off', name: 'Offensive', icon: '⚔', color: '#ff8a5c' },
  { id: 'def', name: 'Defensive', icon: '🛡', color: '#6cc6ff' },
  { id: 'util', name: 'Utility', icon: '✦', color: '#ffd36b' },
  { id: 'tower', name: 'Türme', icon: '🏰', color: '#9be36b' },
];

export interface ParagonNode {
  id: string;
  tree: ParagonTreeId;
  name: string;
  icon: string;
  maxRank: number;
  per: Partial<PassiveEffects>;
  description: string;
}

export const PARAGON_NODES: ParagonNode[] = [
  // Offensive
  { id: 'pg_might', tree: 'off', name: 'Wucht', icon: '⚔', maxRank: 50, per: { heroDmgPct: 0.01 }, description: '+1 % Heldenschaden' },
  { id: 'pg_crit', tree: 'off', name: 'Präzision', icon: '🎯', maxRank: 40, per: { heroCrit: 0.003 }, description: '+0,3 % kritische Chance' },
  { id: 'pg_haste', tree: 'off', name: 'Hast', icon: '⚡', maxRank: 40, per: { heroAtkSpeed: 0.006 }, description: '+0,6 % Angriffstempo' },
  { id: 'pg_spell', tree: 'off', name: 'Zauberkraft', icon: '✺', maxRank: 40, per: { abilityPct: 0.01 }, description: '+1 % Fähigkeitsstärke' },

  // Defensive
  { id: 'pg_vital', tree: 'def', name: 'Vitalität', icon: '❤', maxRank: 50, per: { heroHpPct: 0.012 }, description: '+1,2 % Helden-LP' },
  { id: 'pg_armor', tree: 'def', name: 'Panzerung', icon: '🛡', maxRank: 40, per: { heroArmor: 0.002 }, description: '+0,2 % Rüstung' },
  { id: 'pg_regen', tree: 'def', name: 'Regeneration', icon: '✚', maxRank: 40, per: { heroRegen: 0.15 }, description: '+0,15 LP/s' },
  { id: 'pg_leech', tree: 'def', name: 'Lebensraub', icon: '🩸', maxRank: 30, per: { lifesteal: 0.001 }, description: '+0,1 % Lebensraub' },
  { id: 'pg_core', tree: 'def', name: 'Bollwerk', icon: '🏠', maxRank: 20, per: { coreLives: 2 }, description: '+2 Kernenergie (bis +40)' },
  // Utility
  { id: 'pg_greed', tree: 'util', name: 'Gier', icon: '🪙', maxRank: 50, per: { killGoldPct: 0.004 }, description: '+0,4 % Kopfgeld' },
  { id: 'pg_loot', tree: 'util', name: 'Beutejäger', icon: '◆', maxRank: 40, per: { resourcePct: 0.005, lootPct: 0.003 }, description: '+0,5 % Ressourcen, +0,3 % Beute' },
  { id: 'pg_focus', tree: 'util', name: 'Fokus', icon: '⌛', maxRank: 40, per: { heroCdr: 0.002 }, description: '−0,2 % Abklingzeit' },
  { id: 'pg_swift', tree: 'util', name: 'Eile', icon: '👟', maxRank: 30, per: { moveSpeed: 0.01 }, description: '+0,01 Bewegungstempo' },
  { id: 'pg_magnet', tree: 'util', name: 'Magnetismus', icon: '🧲', maxRank: 30, per: { pickupRadius: 0.03 }, description: '+0,03 Aufsammelradius' },
  // Türme (nur für Türme)
  { id: 'pg_tower', tree: 'tower', name: 'Turmmacht', icon: '🏰', maxRank: 50, per: { towerDmgPct: 0.004 }, description: '+0,4 % Turmschaden' },
  { id: 'pg_rhythm', tree: 'tower', name: 'Turmtakt', icon: '♫', maxRank: 40, per: { towerSpeedPct: 0.003 }, description: '+0,3 % Turmangriffstempo' },
  { id: 'pg_reach', tree: 'tower', name: 'Weitblick', icon: '🔭', maxRank: 30, per: { towerRange: 0.01 }, description: '+0,01 Turmreichweite' },
  { id: 'pg_tcrit', tree: 'tower', name: 'Zielwasser', icon: '🎯', maxRank: 40, per: { towerCrit: 0.002 }, description: '+0,2 % kritische Chance für Türme' },
  { id: 'pg_pierce', tree: 'tower', name: 'Durchschlag', icon: '⛏', maxRank: 30, per: { towerPierce: 0.003 }, description: '+0,3 % Panzerbrechung für Türme' },
  { id: 'pg_blast', tree: 'tower', name: 'Sprengkraft', icon: '💥', maxRank: 30, per: { towerSplashPct: 0.004 }, description: '+0,4 % Flächenradius (Kanone, Meteor, Minen …)' },
  { id: 'pg_builder', tree: 'tower', name: 'Baumeister', icon: '🔨', maxRank: 25, per: { towerCostPct: 0.004 }, description: '−0,4 % Baukosten für Türme (max. −10 %)' },
  { id: 'pg_mine', tree: 'tower', name: 'Goldader', icon: '⛏️', maxRank: 30, per: { mineIncomePct: 0.005 }, description: '+0,5 % Goldgräber-Ertrag' },
];

export interface ParagonProgress {
  level: number;
  xp: number;
  points: Record<string, number>;
}

export function paragonXpToNext(level: number): number {
  return Math.round(PARAGON.xpBase * (1 + level * PARAGON.xpGrowth));
}

export function paragonSpent(points: Record<string, number> | undefined): number {
  let n = 0;
  for (const node of PARAGON_NODES) n += Math.max(0, Math.min(node.maxRank, points?.[node.id] ?? 0));
  return n;
}

export const PARAGON_TOTAL_RANKS = PARAGON_NODES.reduce((a, n) => a + n.maxRank, 0);

export function emptyParagon(): ParagonProgress {
  return { level: 0, xp: 0, points: {} };
}

/** 3.0: Vorab-Paragonpunkte ab Heldenstufe 50 – alle 5 Stufen einer (Stufe 50, 55 … 95 = 10 Punkte). */
export function paragonPrePoints(heroLevel: number): number {
  return heroLevel >= 50 ? Math.floor((Math.min(heroLevel, 99) - 50) / 5) + 1 : 0;
}

/** Bereinigt Paragon-Daten: gültige Knoten, Ränge im Rahmen, nie mehr Punkte vergeben als vorhanden (extra = Vorab- und Meilensteinpunkte). */
export function sanitizeParagon(raw: unknown, extra = 0): ParagonProgress {
  const out = emptyParagon();
  if (typeof raw !== 'object' || raw === null) return out;
  const r = raw as Record<string, unknown>;
  out.level = Math.max(0, Math.min(PARAGON.maxLevel, Math.floor(Number(r.level) || 0)));
  out.xp = out.level >= PARAGON.maxLevel ? 0 : Math.max(0, Math.min(paragonXpToNext(out.level), Number(r.xp) || 0));
  const pts = typeof r.points === 'object' && r.points !== null ? (r.points as Record<string, unknown>) : {};
  let budget = out.level + Math.max(0, extra);
  for (const node of PARAGON_NODES) {
    const v = Math.max(0, Math.min(node.maxRank, Math.floor(Number(pts[node.id]) || 0), budget));
    if (v > 0) {
      out.points[node.id] = v;
      budget -= v;
    }
  }
  return out;
}

/** Vergleichswert für „mehr Fortschritt“ (Stufe zuerst, dann EP). */
export const paragonScore = (p: { level: number; xp: number }): number => p.level * 1e9 + p.xp;
