import type { StatMod, TowerDefinition, TowerId } from '../core/types.ts';
import { TOWERS } from './towers.ts';

// Helden-Endlos 2.7: Runen werden in Türme gesockelt. Sie sind nur findbar (kein Kauf, keine Herstellung) und
// fallen erst ab Welle 250. Drei Qualitätsstufen; Sockel je Turm wachsen mit dem Ausbau.

export type RuneId = 'might' | 'haste' | 'reach' | 'precision' | 'ruin' | 'frost' | 'venom' | 'greed';

export interface RuneDef {
  id: RuneId;
  name: string;
  icon: string;
  color: string;
  /** Wirkung bei Qualität 1; höhere Qualitäten skalieren mit RUNE_TIERS[q].mul. */
  mods: StatMod[];
  description: string;
}

export const RUNE_TIERS = [
  { tier: 1, name: 'Runensplitter', mul: 1 },
  { tier: 2, name: 'Rune', mul: 1.8 },
  { tier: 3, name: 'Uralte Rune', mul: 3 },
] as const;

export const RUNES: Record<RuneId, RuneDef> = {
  might: { id: 'might', name: 'Kraftrune', icon: 'ᚦ', color: '#ff7a59', mods: [{ stat: 'damageMul', add: 0.12 }], description: '+12 % Schaden' },
  haste: { id: 'haste', name: 'Hastrune', icon: 'ᚱ', color: '#ffd84a', mods: [{ stat: 'attackSpeedMul', add: 0.09 }], description: '+9 % Angriffstempo' },
  reach: { id: 'reach', name: 'Weitrune', icon: 'ᛉ', color: '#7ad8ff', mods: [{ stat: 'range', add: 0.35 }], description: '+0,35 Reichweite' },
  precision: { id: 'precision', name: 'Präzisionsrune', icon: 'ᛏ', color: '#ff9ae0', mods: [{ stat: 'critChance', add: 0.06 }, { stat: 'critMult', add: 0.15 }], description: '+6 % kritische Chance, +0,15 kritischer Multiplikator' },
  ruin: { id: 'ruin', name: 'Bruchrune', icon: 'ᚺ', color: '#c0a0ff', mods: [{ stat: 'armorPierce', add: 0.08 }], description: '+8 % Panzerbrechung' },
  frost: { id: 'frost', name: 'Frostrune', icon: 'ᛁ', color: '#a8ecff', mods: [{ stat: 'chillPct', add: 0.06 }, { stat: 'chillDur', add: 0.6 }], description: '+6 % Verlangsamung je Treffer' },
  venom: { id: 'venom', name: 'Giftrune', icon: 'ᛇ', color: '#8aff7a', mods: [{ stat: 'poisonDps', add: 4 }, { stat: 'poisonDur', add: 1 }, { stat: 'poisonMaxStacks', add: 1 }], description: '+4 Gift-SpS je Stapel' },
  greed: { id: 'greed', name: 'Gierrune', icon: 'ᚠ', color: '#ffc04a', mods: [], description: '+1 Gold je Abschuss durch Türme dieser Sorte' },
};

export const RUNE_ORDER: RuneId[] = ['might', 'haste', 'reach', 'precision', 'ruin', 'frost', 'venom', 'greed'];

export const RUNE_RULES = {
  /** Volle Runen (Qualität II/III) fallen erst ab dieser Welle. */
  fromWave: 250,
  /** 3.0: Runensplitter (Qualität I) schon ab dieser Welle. */
  splinterFrom: 100,
  dropChance: { normal: 0.0015, elite: 0.04, miniBoss: 0.25, boss: 0.6 },
  /** Wellenabschluss: zusätzliche Chance je Welle ab fromWave. */
  waveChance: 0.12,
  /** Höchstens so viele ungesockelte Runen im Beutel. */
  bagMax: 40,
};

/** Wirkungen einer Rune in einer Qualität (1–3). */
export function runeMods(id: RuneId, tier: number): StatMod[] {
  const mul = RUNE_TIERS[Math.max(0, Math.min(2, tier - 1))].mul;
  return RUNES[id].mods.map((m) => ({ stat: m.stat, add: (m.add ?? 0) * mul }));
}

export function runeLabel(id: RuneId, tier: number): string {
  return `${RUNE_TIERS[Math.max(0, Math.min(2, tier - 1))].name}: ${RUNES[id].name}`;
}

export function runeEffectText(id: RuneId, tier: number): string {
  const mul = RUNE_TIERS[Math.max(0, Math.min(2, tier - 1))].mul;
  return mul === 1 ? RUNES[id].description : `${RUNES[id].description} ×${String(mul).replace('.', ',')}`;
}

/** Türme mit Sockeln: alle kämpfenden und unterstützenden Türme (keine Wirtschaft, keine Barrikade). */
export function runeEligible(def: TowerDefinition | TowerId): boolean {
  const d = typeof def === 'string' ? TOWERS[def] : def;
  return d.attack !== 'economy' && d.attack !== 'none';
}

/** 3.3: Runensockel gehören der Turmsorte. 1 ab Welle 100, 2 ab 250, 3 ab 400, (3.11) 4 ab 500; legendäre Fusionen je +1. */
export const RUNE_SLOT_WAVES = [100, 250, 400, 500] as const;

export function runeSockets(def: TowerId, wave: number): number {
  if (!runeEligible(def)) return 0;
  const base = RUNE_SLOT_WAVES.filter((w) => wave >= w).length;
  return base > 0 && TOWERS[def].legendary ? base + 1 : base;
}

/** Welle, ab der die nächste Sockelstufe frei wird (null = alle frei). */
export function nextRuneSlotWave(wave: number): number | null {
  return RUNE_SLOT_WAVES.find((w) => wave < w) ?? null;
}
