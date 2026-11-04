import type { DamageType } from '../core/types.ts';

// 3.0 Endlos-Skalierung: exponentieller Anstieg ab Welle 60, Weltstufen alle 50 Wellen, Wellen-Affixe ab
// Welle 100, mehr Elite und Mini-Bosse, nachrückende Wellen ab Welle 120. Alles nur im Endlosmodus.

export const SCALING = {
  /** Ab dieser Welle wächst die Gegner-LP zusätzlich um expRate je Welle (multiplikativ). */
  expFrom: 60,
  expRate: 1.02,
  bossExpRate: 1.018,
  /** Weltstufe = floor(Welle / tierEvery). */
  tierEvery: 50,
  tierHp: 0.15,
  tierArmor: 0.03,
  tierArmorMax: 0.3,
  tierSpeed: 0.03,
  tierSpeedMax: 0.3,
  tierReward: 0.2,
  /** Affixe ab Welle affixFrom: 1, ab +75 Wellen 2, ab +150 Wellen 3. */
  affixFrom: 100,
  affixStep: 75,
  affixMax: 3,
  /** Ab dieser Welle: Elitegruppe in jeder zweiten Welle und Mini-Bosse alle 3 Wellen (Helden-Endlos). */
  pressureFrom: 100,
  miniBossEveryLate: 3,
  bossLeakMul: 2,
  /** Nachrückende Wellen (Helden-Endlos): ab Welle overrunFrom rückt nach overrunAfter s Kampf die nächste nach. */
  overrunFrom: 120,
  overrunAfter: 45,
  /** 3.11: Ab Welle smoothFrom werden Gruppen nach LP verteilt (smoothHpPerSlot Grund-LP je Platz der Mengenobergrenze). */
  smoothFrom: 100,
  smoothHpPerSlot: 90,
  /** 3.11: Ab Welle expSlowFrom wächst die LP nur noch um expRateLate je Welle (statt expRate). */
  expSlowFrom: 150,
  expRateLate: 1.006,
} as const;

/** 3.12: Flieger – Tempo-Obergrenze, nur halber Anteil an Tempo-Zuschlägen, Verlangsamung wirkt stärker. */
export const FLYER = {
  maxSpeed: 2.0,
  speedMulShare: 0.5,
  slowMul: 1.3,
};

export type AffixId = 'regen' | 'shielded' | 'immune' | 'swift' | 'splitter' | 'jammer' | 'armored';

export interface AffixDef {
  id: AffixId;
  name: string;
  icon: string;
  description: string;
}

export const AFFIXES: Record<AffixId, AffixDef> = {
  regen: { id: 'regen', name: 'Regeneration', icon: '✚', description: 'Gegner heilen 1 % LP/s (Gift/Brand unterbinden das)' },
  shielded: { id: 'shielded', name: 'Schildträger', icon: '⛨', description: 'Gegner starten mit Schild (25 % LP)' },
  immune: { id: 'immune', name: 'Immunität', icon: '⊘', description: 'Gegner sind gegen einen Schadenstyp nahezu immun' },
  swift: { id: 'swift', name: 'Flink', icon: '»', description: '+20 % Tempo' },
  splitter: { id: 'splitter', name: 'Splitter', icon: '✶', description: 'Besiegte Gegner zerfallen in 2 Schwarmlinge' },
  jammer: { id: 'jammer', name: 'Störsender', icon: '⚡', description: 'Elitegegner betäuben alle 8 s Türme in 1,5 Feldern für 1 s' },
  armored: { id: 'armored', name: 'Gepanzert', icon: '▣', description: '+20 % physische Resistenz' },
};

export const AFFIX_ORDER: AffixId[] = ['regen', 'shielded', 'immune', 'swift', 'splitter', 'jammer', 'armored'];
export const IMMUNE_TYPES: DamageType[] = ['physical', 'fire', 'frost', 'lightning', 'poison', 'arcane'];

export function worldTier(wave: number, endless: boolean): number {
  return endless ? Math.floor(Math.max(0, wave) / SCALING.tierEvery) : 0;
}

export function affixCount(wave: number, endless: boolean): number {
  if (!endless || wave < SCALING.affixFrom) return 0;
  return Math.min(SCALING.affixMax, 1 + Math.floor((wave - SCALING.affixFrom) / SCALING.affixStep));
}

// ------------------------------------------------------------ Challenge-Bosse (3.1)

export const CHALLENGE = {
  /** Bis Welle switchAt alle 50 Wellen, danach alle 25 Wellen. */
  every: 50,
  switchAt: 250,
  everyLate: 25,
  hpMul: 3.5,
  resist: 0.15,
  /** Kontrolleffekte (Verlangsamung, Einfrieren, Betäubung, Wurzeln) wirken nur zu diesem Anteil. */
  ccMul: 0.35,
  shieldEvery: 15,
  shieldPct: 0.08,
  shieldMax: 0.3,
  summonEvery: 12,
  summonCount: 3,
  enrageAfter: 60,
  enrageSpeed: 1.4,
  /** Durchbruch kostet mindestens so viel bzw. diesen Anteil der maximalen Kernenergie. */
  leakMin: 10,
  leakPct: 0.25,
  rewardMul: 5,
};

/** 3.11: Challenge-Bosse werden ab Welle 150 je 100 Wellen um das 1-Fache stärker (Welle 250: ×7, Welle 500: ×15,75). */
export function challengeHpMul(wave: number): number {
  return CHALLENGE.hpMul * (1 + Math.max(0, wave - 150) / 100);
}

export function isChallengeWave(wave: number, endless: boolean): boolean {
  if (!endless || wave <= 0) return false;
  if (wave <= CHALLENGE.switchAt) return wave % CHALLENGE.every === 0;
  return wave % CHALLENGE.everyLate === 0;
}

// ------------------------------------------------------------ Turmlimit (3.3, „Weniger ist mehr“)

export const TOWER_LIMIT = {
  /** Endlosmodi: so viele Türme zu Beginn … */
  base: 20,
  /** … und alle perWaves Wellen einer mehr. */
  perWaves: 5,
  /** Fokusbonus: je freiem Bauplatz +focusPer Turmschaden, höchstens focusMax. */
  focusPer: 0.02,
  focusMax: 0.3,
};

/** 3.7: Türme, die nicht zum Turmlimit zählen – Barrikaden, Unterstützungstürme, die andere Türme verstärken, und (3.8) Goldgräber. */
export const LIMIT_FREE: readonly string[] = ['barricade', 'lanebarricade', 'pylon', 'totem', 'goldmine'];

export function countsTowardLimit(def: string): boolean {
  return !LIMIT_FREE.includes(def);
}

/** Höchstzahl an Türmen (Barrikaden und Unterstützungstürme zählen nicht). Außerhalb der Endlosmodi unbegrenzt. */
export function towerLimit(wave: number, endless: boolean, off = false): number {
  if (!endless || off) return Infinity;
  return TOWER_LIMIT.base + Math.floor(Math.max(0, wave) / TOWER_LIMIT.perWaves);
}

/** Welle, ab der der nächste Bauplatz frei wird. */
export function nextTowerSlotWave(wave: number): number {
  return (Math.floor(Math.max(0, wave) / TOWER_LIMIT.perWaves) + 1) * TOWER_LIMIT.perWaves;
}

/** Fokusbonus für wenige Türme (Anteil, 0 … focusMax). */
export function focusBonus(count: number, limit: number): number {
  if (!Number.isFinite(limit)) return 0;
  return Math.min(TOWER_LIMIT.focusMax, Math.max(0, limit - count) * TOWER_LIMIT.focusPer);
}

// ------------------------------------------------------------ Lane-Barrikaden (3.8)

export const LANE_BARRICADE = {
  /** Höchstens so viele Barrikaden je Zeitfenster (Spielzeit in Sekunden). */
  maxCharges: 2,
  window: 600,
  /** Mindestabstand (Felder) zu jedem Riss/Spawn. */
  minSpawnDist: 4,
};
