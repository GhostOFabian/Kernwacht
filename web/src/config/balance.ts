import { SCALING } from './scaling.ts';
import type { Difficulty, MapVariant } from '../core/types.ts';

// Zentrale Balancewerte. Änderungen hier wirken auf alle Systeme.

export const SIM = {
  tickRate: 30,
  dt: 1 / 30,
  /** Maximale Simulationsticks pro Renderframe (schützt vor "Spirale des Todes" nach Tabwechsel). */
  maxTicksPerFrame: 12,
  retargetInterval: 0.2,
  speeds: [1, 2, 3, 5, 8] as const,
};

export const DIFFICULTY: Record<Difficulty, { name: string; hpMul: number; goldMul: number; lives: number; rewardMul: number }> = {
  easy: { name: 'Entspannt', hpMul: 0.8, goldMul: 1.15, lives: 30, rewardMul: 1.1 },
  normal: { name: 'Normal', hpMul: 1.1, goldMul: 1.0, lives: 20, rewardMul: 1.0 },
  hard: { name: 'Hart', hpMul: 1.25, goldMul: 0.9, lives: 15, rewardMul: 0.95 },
};

export const ECONOMY = {
  startGold: { classic: 260, mazing: 430 } as Record<MapVariant, number>,
  waveBonusBase: 30,
  waveBonusPerWave: 5,
  bossWaveBonus: 40,
  interestRate: 0.05,
  interestCap: 40,
  veinMul: 1.5,
  /** Goldvorkommen: zusätzlicher Abbau des Goldgräbers je Welle (Grundwert + je Welle). */
  veinFlat: 8,
  veinFlatPerWave: 1.5,
  leyDamageMul: 1.15,
  highgroundRangeAdd: 0.4,
};

/** Lebenspunktskalierung pro Welle – polynomiell begrenzt, nie exponentiell. */
export function waveHpMul(wave: number, endless: boolean): number {
  const w = Math.max(0, wave - 1);
  if (!endless) return 1 + 0.12 * w + 0.007 * w * w;
  // Endlos: nach Welle 30 flacht der quadratische Anteil ab (Wurzelanteil), damit der Modus
  // durch Taktik statt durch reine Zahlen schwer wird.
  const quad = Math.min(w, 30);
  const tail = Math.max(0, w - 30);
  const base = 1 + 0.12 * w + 0.007 * quad * quad + 0.45 * tail + 2 * Math.sqrt(tail);
  // 3.0: ab Welle 60 zusätzlich sanft exponentiell – es gibt immer eine Wand, die man weiter hinausschiebt.
  // 3.11: ab Welle 150 deutlich flacher (expRateLate), damit Spielerstärke und Gegner im Gleichschritt wachsen.
  const early = Math.max(0, Math.min(wave, SCALING.expSlowFrom) - SCALING.expFrom);
  const late = Math.max(0, wave - SCALING.expSlowFrom);
  return base * Math.pow(SCALING.expRate, early) * Math.pow(SCALING.expRateLate, late);
}

export function waveBudget(wave: number): number {
  return 8 + 5 * wave + 0.16 * wave * wave;
}

export function waveCountCap(wave: number): number {
  return Math.min(60, 10 + wave * 2);
}

/** 3.2: allgemeiner Aufschlag auf alle Boss-LP. */
export const BOSS_HP_GLOBAL = 1.5;

export function bossHpMul(wave: number, endless: boolean): number {
  // Bosse skalieren schwächer als normale Gegner; sie fordern Mechanik statt nur LP.
  // 3.2: Boss-LP allgemein ×BOSS_HP_GLOBAL, im Endlos nach Welle 40 steiler (+0,07 statt +0,05 je Welle).
  const w = Math.max(0, wave - 10);
  if (!endless) return BOSS_HP_GLOBAL * (1 + 0.09 * w);
  // 3.11: ab Welle 150 wachsen Bosse genau wie die normalen Wellen (bleiben relevant, ohne davonzulaufen)
  if (wave > SCALING.expSlowFrom) return bossHpMul(SCALING.expSlowFrom, true) * (waveHpMul(wave, true) / waveHpMul(SCALING.expSlowFrom, true));
  return BOSS_HP_GLOBAL * (1 + 0.09 * Math.min(w, 30) + 0.07 * Math.max(0, w - 30)) * Math.pow(SCALING.bossExpRate, Math.max(0, wave - SCALING.expFrom));
}

export function killRewardMul(wave: number): number {
  return 1 + 0.035 * Math.max(0, wave - 1);
}

export const QUICK_RUN = {
  waves: 20,
  bosses: [10, 20],
  modifierWaves: [3, 6, 10, 14, 17],
};

export const ENDLESS = {
  bossEvery: 10,
  modifierEvery: 4,
};

export const LIVES_DEFAULT = 20;
