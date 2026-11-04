import { PARAGON_NODES } from './paragon.ts';
import type { HeroEndlessState, TowerId } from '../core/types.ts';

// Passiver Fähigkeitenbaum des Helden-Endlosmodus (dauerhaft, kontoweit).
// Punkte: 1 je 10 erreichte Bestwellen, höchstens 100 (= Bestwelle 1000).
// Die Summe aller Maximalränge ist genau 100: Wer Welle 1000 erreicht, hat den Baum vollständig.
// Werte je Rang sind bewusst klein gehalten; sie wirken automatisch in jedem Lauf.

export const PASSIVE = {
  wavesPerPoint: 10,
  maxPoints: 100,
  /** Benötigte, im Baum bereits ausgegebene Punkte je Stufe. */
  tierGate: [0, 5, 15, 30, 50, 75] as const,
};

export interface PassiveEffects {
  heroHpPct: number;
  heroDmgPct: number;
  heroArmor: number;
  heroCdr: number;
  heroCrit: number;
  heroRegen: number;
  manaRegen: number;
  supportPower: number;
  pickupRadius: number;
  xpPct: number;
  towerDmgPct: number;
  towerSpeedPct: number;
  towerRange: number;
  startGold: number;
  killGoldPct: number;
  resourcePct: number;
  lootPct: number;
  coreLives: number;
  respawnPct: number;
  /** Türme werden so viele Wellen früher freigeschaltet. */
  unlockEarlier: number;
  /** Paragon (2.7): Angriffstempo, Fähigkeitsstärke, Bewegungstempo, Lebensraub des Helden. */
  heroAtkSpeed: number;
  abilityPct: number;
  moveSpeed: number;
  lifesteal: number;
  /** Paragon-Turmbaum (2.9): kritische Chance, Panzerbrechung, Baukostenrabatt, Goldgräber-Ertrag, Turm-Kopfgeld. */
  towerCrit: number;
  towerPierce: number;
  towerCostPct: number;
  mineIncomePct: number;
  towerSplashPct: number;
}

export const ZERO_PASSIVE: PassiveEffects = {
  heroHpPct: 0, heroDmgPct: 0, heroArmor: 0, heroCdr: 0, heroCrit: 0, heroRegen: 0, manaRegen: 0, supportPower: 0, pickupRadius: 0,
  xpPct: 0, towerDmgPct: 0, towerSpeedPct: 0, towerRange: 0, startGold: 0, killGoldPct: 0, resourcePct: 0, lootPct: 0, coreLives: 0,
  respawnPct: 0, unlockEarlier: 0, heroAtkSpeed: 0, abilityPct: 0, moveSpeed: 0, lifesteal: 0,
  towerCrit: 0, towerPierce: 0, towerCostPct: 0, mineIncomePct: 0, towerSplashPct: 0,
};

export interface PassiveNode {
  id: string;
  name: string;
  icon: string;
  tier: number;           // 1..6
  maxRank: number;
  per: Partial<PassiveEffects>;
  description: string;    // Wirkung je Rang
}

export const PASSIVE_NODES: PassiveNode[] = [
  // Stufe 1 (frei)
  { id: 'p_vigor', name: 'Zähigkeit', icon: '❤', tier: 1, maxRank: 5, per: { heroHpPct: 0.03 }, description: '+3 % Helden-LP' },
  { id: 'p_might', name: 'Kraft', icon: '⚔', tier: 1, maxRank: 5, per: { heroDmgPct: 0.03 }, description: '+3 % Heldenschaden' },
  { id: 'p_learn', name: 'Lernfähigkeit', icon: '📖', tier: 1, maxRank: 5, per: { xpPct: 0.03 }, description: '+3 % Erfahrung' },
  { id: 'p_coin', name: 'Sparstrumpf', icon: '🪙', tier: 1, maxRank: 5, per: { startGold: 15 }, description: '+15 Startgold' },
  // Stufe 2 (5 Punkte im Baum)
  { id: 'p_towers', name: 'Turmbaukunst', icon: '🗼', tier: 2, maxRank: 5, per: { towerDmgPct: 0.02 }, description: '+2 % Turmschaden' },
  { id: 'p_tempo', name: 'Taktfrequenz', icon: '⏱', tier: 2, maxRank: 5, per: { towerSpeedPct: 0.015 }, description: '+1,5 % Turm-Angriffstempo' },
  { id: 'p_scav', name: 'Plünderer', icon: '⛓', tier: 2, maxRank: 5, per: { resourcePct: 0.04 }, description: '+4 % Ressourcen aus Beute und Wellen' },
  { id: 'p_magnet', name: 'Magnetismus', icon: '🧲', tier: 2, maxRank: 3, per: { pickupRadius: 0.1 }, description: '+0,1 Aufsammelradius' },
  // Stufe 3 (15)
  { id: 'p_plans', name: 'Bauplan-Archiv', icon: '📐', tier: 3, maxRank: 3, per: { unlockEarlier: 1 }, description: 'Türme 1 Welle früher freigeschaltet' },
  { id: 'p_bounty', name: 'Kopfgeldjäger', icon: '💰', tier: 3, maxRank: 5, per: { killGoldPct: 0.03 }, description: '+3 % Kopfgeld' },
  { id: 'p_core', name: 'Kernpanzerung', icon: '🛡', tier: 3, maxRank: 5, per: { coreLives: 2 }, description: '+2 Kernenergie' },
  { id: 'p_runes', name: 'Schutzrunen', icon: '🔰', tier: 3, maxRank: 5, per: { heroArmor: 0.015 }, description: '+1,5 % Heldenrüstung' },
  // Stufe 4 (30)
  { id: 'p_calm', name: 'Gelassenheit', icon: '🌀', tier: 4, maxRank: 5, per: { heroCdr: 0.02 }, description: '+2 % Abklingzeitreduktion' },
  { id: 'p_range', name: 'Weitsicht', icon: '🔭', tier: 4, maxRank: 5, per: { towerRange: 0.05 }, description: '+0,05 Turmreichweite' },
  { id: 'p_luck', name: 'Glücksfund', icon: '🍀', tier: 4, maxRank: 5, per: { lootPct: 0.04 }, description: '+4 % Beutechance' },
  { id: 'p_revive', name: 'Zähes Leben', icon: '✚', tier: 4, maxRank: 5, per: { respawnPct: 0.06 }, description: '−6 % Wiederbelebungszeit' },
  // Stufe 5 (50)
  { id: 'p_patron', name: 'Schutzpatron', icon: '✨', tier: 5, maxRank: 5, per: { supportPower: 0.02 }, description: '+2 % Unterstützungsstärke (Turmverstärkungen)' },
  { id: 'p_spring', name: 'Lebensquell', icon: '💚', tier: 5, maxRank: 5, per: { heroRegen: 0.4 }, description: '+0,4 LP/s Regeneration' },
  { id: 'p_flow', name: 'Manafluss', icon: '💧', tier: 5, maxRank: 5, per: { manaRegen: 0.4 }, description: '+0,4 Mana/s' },
  { id: 'p_precise', name: 'Präzision', icon: '🎯', tier: 5, maxRank: 5, per: { heroCrit: 0.01 }, description: '+1 % kritische Trefferchance' },
  // Stufe 6 (75)
  { id: 'p_legend', name: 'Legende des Kerns', icon: '👑', tier: 6, maxRank: 4, per: { towerDmgPct: 0.03, heroDmgPct: 0.03, heroHpPct: 0.03 }, description: '+3 % Turmschaden, +3 % Heldenschaden, +3 % Helden-LP' },
];

export const PASSIVE_TOTAL_RANKS = PASSIVE_NODES.reduce((a, n) => a + n.maxRank, 0);

export function passivePoints(bestWave: number): number {
  return Math.max(0, Math.min(PASSIVE.maxPoints, Math.floor(bestWave / PASSIVE.wavesPerPoint)));
}

export function passiveSpent(passive: Record<string, number>): number {
  let n = 0;
  for (const node of PASSIVE_NODES) n += Math.max(0, Math.min(node.maxRank, passive[node.id] ?? 0));
  return n;
}

/** Prüft, ob ein Rang gekauft werden kann; Rückgabe: Grund oder null. */
export function passiveRaiseReason(passive: Record<string, number>, bestWave: number, id: string): string | null {
  const node = PASSIVE_NODES.find((n) => n.id === id);
  if (!node) return 'Unbekannter Knoten';
  const rank = passive[id] ?? 0;
  if (rank >= node.maxRank) return 'Maximaler Rang';
  const spent = passiveSpent(passive);
  if (spent >= passivePoints(bestWave)) return `Keine freien Punkte (1 Punkt je ${PASSIVE.wavesPerPoint} Bestwellen)`;
  const gate = PASSIVE.tierGate[node.tier - 1];
  if (spent < gate) return `Benötigt ${gate} ausgegebene Punkte (Stufe ${node.tier})`;
  return null;
}

/** Bereinigt einen gespeicherten Baum: unbekannte Knoten, zu hohe Ränge, zu viele Punkte, Stufensperren. */
export function sanitizePassive(raw: unknown, bestWave: number): Record<string, number> {
  const out: Record<string, number> = {};
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return out;
  const src = raw as Record<string, unknown>;
  // In Stufenreihenfolge neu aufbauen, damit Stufensperren und Punktelimit gelten.
  const nodes = [...PASSIVE_NODES].sort((a, b) => a.tier - b.tier);
  for (const node of nodes) {
    const want = typeof src[node.id] === 'number' ? Math.floor(src[node.id] as number) : 0;
    for (let k = 0; k < Math.min(node.maxRank, Math.max(0, want)); k++) {
      if (passiveRaiseReason(out, bestWave, node.id) !== null) break;
      out[node.id] = (out[node.id] ?? 0) + 1;
    }
  }
  return out;
}

export function passiveEffects(passive: Record<string, number> | undefined): PassiveEffects {
  const fx: PassiveEffects = { ...ZERO_PASSIVE };
  if (!passive) return fx;
  for (const node of PASSIVE_NODES) {
    const r = Math.max(0, Math.min(node.maxRank, passive[node.id] ?? 0));
    if (!r) continue;
    for (const [k, v] of Object.entries(node.per) as [keyof PassiveEffects, number][]) fx[k] += v * r;
  }
  return fx;
}

/** Paragon-Wirkungen (gemeinsam für alle Helden). */
export function paragonEffects(points: Record<string, number> | undefined, into: PassiveEffects = { ...ZERO_PASSIVE }): PassiveEffects {
  if (!points) return into;
  for (const node of PARAGON_NODES) {
    const r = Math.max(0, Math.min(node.maxRank, points[node.id] ?? 0));
    if (!r) continue;
    for (const [k, v] of Object.entries(node.per) as [keyof PassiveEffects, number][]) into[k] += v * r;
  }
  return into;
}

const fxCache = new WeakMap<object, { passive: object | undefined; rev: number; fx: PassiveEffects }>();
/** Effekte des Laufs: passiver Baum (Schnappschuss beim Start) plus Paragon (im Lauf vergebbar, daher mit Revision). */
export function runPassive(he: HeroEndlessState | null | undefined): PassiveEffects {
  if (!he || (!he.passive && !he.paragon)) return ZERO_PASSIVE;
  const rev = he.paragon?.rev ?? 0;
  const c = fxCache.get(he);
  if (c && c.passive === he.passive && c.rev === rev) return c.fx;
  const fx = paragonEffects(he.paragon?.points, passiveEffects(he.passive));
  fxCache.set(he, { passive: he.passive, rev, fx });
  return fx;
}

// ------------------------------------------------------------ Turmfreischaltung (Helden-Endlos)


/**
 * Welle, ab der ein Turm im Helden-Endlosmodus gebaut werden kann.
 * Reihenfolge so gewählt, dass Konter rechtzeitig verfügbar sind (Luftabwehr vor Fliegern W5,
 * Detektoren vor Schattenschleichern W9) und alle Elementarreaktionen sowie Fusionspartner
 * paarweise freigeschaltet werden (Glut+Sturm, Frost+Kanone, Armbrust+Runenweber, Meteor+Laser).
 */
export const TOWER_UNLOCKS: Record<TowerId, number> = {
  ranger: 1, ember: 1, frost: 1, goldmine: 1, barricade: 1, lanebarricade: 1,
  storm: 3, cannon: 3,
  spore: 5, ballista: 5,
  rune: 7, resonator: 7,
  pylon: 9, totem: 9,
  riftbinder: 11, tesla: 11,
  gravity: 14, meteor: 14,
  laser: 17, dragon: 17,
  minelayer: 6, sporemine: 0, skyguard: 5, skywatch: 0,
  plasma: 0, glacier: 0, voidlance: 0, starfall: 0, thornwood: 0, thunderspire: 0, riftgate: 0, phoenix: 0, cathedral: 0, stormeye: 0,
};

export function towerUnlockWave(def: TowerId, he: HeroEndlessState | null | undefined): number {
  if (!he) return 1;
  const base = TOWER_UNLOCKS[def] ?? 1;
  if (base <= 1) return 1;
  return Math.max(1, base - runPassive(he).unlockEarlier);
}

/** Turm im aktuellen Lauf baubar? Maßgeblich ist die als Nächstes anstehende Welle. */
export function towerUnlocked(def: TowerId, he: HeroEndlessState | null | undefined, wave: number): boolean {
  if (!he) return true;
  return wave + 1 >= towerUnlockWave(def, he);
}
