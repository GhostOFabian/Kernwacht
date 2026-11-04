import type { StatMod, TowerId } from '../core/types.ts';
import { TOWERS } from './towers.ts';

// 3.6 Goldverwendung in den Endlosmodi: Turm-Veredelung (★1–★10). (Bauplatzkauf und Kernausbau in 3.8 entfernt.)

// ------------------------------------------------------------ Veredelung

export const STARS = {
  /** 3.11: bis ★25; ab ★softcap wirkt jeder Stern nur noch zur Hälfte und wird langsamer teurer (lateGrowth). */
  max: 25,
  softcap: 10,
  lateGrowth: 1.25,
  /** Preis ★n → ★n+1 = Basis · growth^n; Basis = Gesamtwert des voll ausgebauten Turms · baseFactor. */
  baseFactor: 0.6,
  growth: 1.55,
  /** Legendäre Türme (Grundpreis 0): feste Basis. */
  legendaryBase: 900,
  /** Für alle kämpfenden Türme je Stern. */
  dmgPerStar: 0.08,
  speedPerStar: 0.02,
};

/** Je Turmsorte die wichtigsten Werte, die pro Stern zusätzlich steigen (add je Stern bzw. mul je Stern). */
export const STAR_KEYS: Partial<Record<TowerId, { mods: StatMod[]; text: string }>> = {
  ranger: { mods: [{ stat: 'critChance', add: 0.02 }, { stat: 'range', add: 0.05 }], text: '+2 % Krit, +0,05 Reichweite' },
  ember: { mods: [{ stat: 'burnDps', add: 2 }, { stat: 'splash', add: 0.04 }], text: '+2 Brand/s, +0,04 Explosionsradius' },
  frost: { mods: [{ stat: 'chillPct', add: 0.015 }, { stat: 'chillMax', add: 0.015 }, { stat: 'range', add: 0.05 }], text: '+1,5 % Verlangsamung und Höchstwert, +0,05 Reichweite' },
  storm: { mods: [{ stat: 'chains', add: 0.34 }, { stat: 'chainRange', add: 0.08 }], text: '+1 Kettensprung je 3 Sterne, +0,08 Sprungweite' },
  spore: { mods: [{ stat: 'poisonDps', add: 1.5 }, { stat: 'poisonMaxStacks', add: 0.25 }], text: '+1,5 Gift/s je Stapel, +1 Stapel je 4 Sterne' },
  ballista: { mods: [{ stat: 'armorPierce', add: 0.03 }, { stat: 'critMult', add: 0.05 }], text: '+3 % Panzerbrechung, +0,05 Krit-Multiplikator' },
  rune: { mods: [{ stat: 'markPct', add: 0.01 }, { stat: 'range', add: 0.05 }], text: '+1 % Resistenzschwächung, +0,05 Reichweite' },
  resonator: { mods: [{ stat: 'range', add: 0.06 }, { stat: 'interrupt', add: 0.5 }], text: '+0,06 Reichweite, stärkere Unterbrechung' },
  goldmine: { mods: [{ stat: 'income', mul: 1.08 }], text: '+8 % Einkommen (multiplikativ)' },
  pylon: { mods: [{ stat: 'networkBonus', add: 0.01 }, { stat: 'linkRange', add: 0.1 }], text: '+1 % Netzwerkbonus, +0,1 Verbindungsweite' },
  cannon: { mods: [{ stat: 'splash', add: 0.05 }], text: '+0,05 Explosionsradius' },
  riftbinder: { mods: [{ stat: 'riftTargets', add: 0.2 }, { stat: 'range', add: 0.05 }], text: '+1 Ziel je 5 Sterne, +0,05 Reichweite' },
  meteor: { mods: [{ stat: 'splash', add: 0.05 }, { stat: 'meteorCount', add: 0.2 }], text: '+0,05 Einschlagradius, +1 Meteor je 5 Sterne' },
  tesla: { mods: [{ stat: 'arcDps', mul: 1.06 }, { stat: 'arcRange', add: 0.05 }], text: '+6 % Lichtbogen-Schaden, +0,05 Bogenweite' },
  gravity: { mods: [{ stat: 'pullCells', add: 0.1 }, { stat: 'range', add: 0.05 }], text: '+0,1 Felder Sog, +0,05 Reichweite' },
  dragon: { mods: [{ stat: 'airDmgMul', add: 0.1 }, { stat: 'splash', add: 0.03 }], text: '+10 % Schaden gegen Flieger, +0,03 Atemradius' },
  laser: { mods: [{ stat: 'range', add: 0.06 }], text: '+0,06 Strahlreichweite' },
  totem: { mods: [{ stat: 'auraDmg', add: 0.01 }, { stat: 'auraSpeed', add: 0.01 }], text: '+1 % Aura-Schaden und -Tempo' },
  minelayer: { mods: [{ stat: 'mineMax', add: 0.5 }, { stat: 'splash', add: 0.03 }], text: '+1 Mine je 2 Sterne, +0,03 Explosionsradius' },
  skyguard: { mods: [{ stat: 'chillMax', add: 0.015 }, { stat: 'multishot', add: 0.2 }], text: '+1,5 % Verlangsamung, +1 Ziel je 5 Sterne' },
  thornwood: { mods: [{ stat: 'bleedDps', add: 1.5 }, { stat: 'shredPct', add: 0.003 }], text: '+1,5 Blutung/s, +0,3 % Rüstungsbruch je Stapel' },
  sporemine: { mods: [{ stat: 'poisonDps', add: 2 }, { stat: 'mineMax', add: 0.5 }], text: '+2 Gift/s, +1 Mine je 2 Sterne' },
  skywatch: { mods: [{ stat: 'airDmgMul', add: 0.1 }, { stat: 'multishot', add: 0.2 }], text: '+10 % Schaden gegen Flieger, +1 Ziel je 5 Sterne' },
  phoenix: { mods: [{ stat: 'burnDps', add: 3 }, { stat: 'airDmgMul', add: 0.08 }], text: '+3 Brand/s, +8 % gegen Flieger' },
  glacier: { mods: [{ stat: 'chillMax', add: 0.015 }, { stat: 'splash', add: 0.04 }], text: '+1,5 % Verlangsamung, +0,04 Radius' },
};

/** Kämpft der Turm (bekommt den allgemeinen Schaden-/Tempobonus)? */
export function starFights(def: TowerId): boolean {
  const a = TOWERS[def].attack;
  return a !== 'economy' && a !== 'none' && a !== 'aura';
}

/** Wertänderungen eines Turms mit n Sternen. */
export function starMods(def: TowerId, stars: number): StatMod[] {
  const raw = Math.max(0, Math.min(STARS.max, Math.floor(stars)));
  if (raw <= 0) return [];
  // effektive Sternzahl: bis ★10 voll, danach je Stern halb
  const n = Math.min(raw, STARS.softcap) + Math.max(0, raw - STARS.softcap) * 0.5;
  const out: StatMod[] = [];
  if (starFights(def)) out.push({ stat: 'damageMul', add: STARS.dmgPerStar * n }, { stat: 'attackSpeedMul', add: STARS.speedPerStar * n });
  for (const m of STAR_KEYS[def]?.mods ?? (TOWERS[def].legendary ? [{ stat: 'range', add: 0.05 } as StatMod] : [])) {
    if (m.mul !== undefined) out.push({ stat: m.stat, mul: Math.pow(m.mul, n) });
    else out.push({ stat: m.stat, add: (m.add ?? 0) * n });
  }
  return out;
}

/** Kurzbeschreibung „je Stern“ für die Oberfläche. */
export function starText(def: TowerId): string {
  const parts: string[] = [];
  if (starFights(def)) parts.push(`+${Math.round(STARS.dmgPerStar * 100)} % Schaden, +${Math.round(STARS.speedPerStar * 100)} % Tempo`);
  const k = STAR_KEYS[def]?.text ?? (TOWERS[def].legendary ? '+0,05 Reichweite' : '');
  if (k) parts.push(k);
  return `${parts.join(', ')}${STARS.max > STARS.softcap ? ` (ab ★${STARS.softcap + 1} halb so viel)` : ''}`;
}

