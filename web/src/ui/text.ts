import { BOSSES, ENEMIES } from '../config/enemies.ts';
import { STAT_LABELS, ZERO_STATS as ZERO, type StatKey } from '../config/stats.ts';
import { TOWERS } from '../config/towers.ts';
import type { Stats, TargetPriority, TowerDefinition, TowerId, WaveDefinition } from '../core/types.ts';
import { DAMAGE_NAMES } from '../render/palette.ts';
import { fmtNum, pct } from './dom.ts';

// Deutsche Texte und Formatierungen für Tooltips, Turmdetails und Wellenvorschau.

export const PRIORITY_NAMES: Record<TargetPriority, string> = {
  first: 'Erster auf dem Weg',
  exit: 'Nächster zum Ausgang',
  strong: 'Meiste Lebenspunkte',
  weak: 'Wenigste Lebenspunkte',
  close: 'Nächster zum Turm',
  random: 'Zufällig',
};

export const PRIORITY_ORDER: TargetPriority[] = ['first', 'exit', 'strong', 'weak', 'close', 'random'];

export const MODE_NAMES = {
  quick: 'Schnelle Partie', campaign: 'Kampagne', endless: 'Endlosmodus', daily: 'Tagesherausforderung', custom: 'Eigene Karte', heroEndless: 'Helden-Endlos',
} as const;

export function damageTypeName(def: TowerDefinition): string {
  return def.damageType ? DAMAGE_NAMES[def.damageType] : '–';
}

/** Schaden pro Sekunde (Direktschaden, ohne Effekte). */
export function dps(st: Stats): number {
  if (st.damage <= 0 || st.cooldown <= 0) return 0;
  return (st.damage * Math.max(1, st.multishot)) / st.cooldown;
}

const SHOW: StatKey[] = [
  'splash', 'chains', 'pierce', 'multishot', 'burnDps', 'burnDur', 'chillPct', 'freezeChance', 'poisonDps',
  'poisonMaxStacks', 'bleedDps', 'bleedMaxStacks', 'shredPct', 'airDmgMul', 'markPct', 'interrupt', 'knockback', 'silenceDur', 'stunDur', 'armorPierce', 'critChance',
  'income', 'killBounty', 'linkRange', 'maxLinks', 'networkBonus', 'riftBack',
  'meteorCount', 'fragments', 'arcDps', 'arcRange', 'pullCells', 'laserRamp', 'auraDmg', 'auraSpeed',
];

const PCT_KEYS = new Set<StatKey>(['chillPct', 'freezeChance', 'markPct', 'armorPierce', 'critChance', 'networkBonus', 'auraDmg', 'auraSpeed', 'shredPct']);

export function statLines(st: Stats, def: TowerDefinition): [string, string][] {
  const out: [string, string][] = [];
  if (st.damage > 0) out.push(['Schaden', `${fmtNum(st.damage)} (${damageTypeName(def)})`]);
  if (st.range > 0) out.push(['Reichweite', `${fmtNum(st.range)} Felder`]);
  if (st.damage > 0 && def.attack !== 'pylon') out.push(['Angriff alle', `${fmtNum(st.cooldown, 2)} s`]);
  const d = dps(st);
  if (d > 0 && def.attack !== 'pylon') out.push(['Schaden/s', fmtNum(d)]);
  for (const k of SHOW) {
    const v = st[k];
    if (!v || (k === 'multishot' && v <= 1) || (k === 'meteorCount' && v <= 1) || (k === 'laserRamp' && v <= 1) || (k === 'poisonMaxStacks' && st.poisonDps <= 0) || (k === 'bleedMaxStacks' && st.bleedDps <= 0) || (k === 'airDmgMul' && v <= 1)) continue;
    const label = STAT_LABELS[k] ?? k;
    out.push([label, PCT_KEYS.has(k) ? pct(v) : fmtNum(v)]);
  }
  if (def.attack !== 'economy' && def.attack !== 'none') out.push(['Trifft', [st.hitsGround > 0 ? 'Boden' : '', st.hitsAir > 0 ? 'Luft' : ''].filter(Boolean).join(' + ') || '–']);
  return out;
}

export function baseStatsOf(id: TowerId): Stats {
  return { ...ZERO, ...TOWERS[id].base } as Stats;
}


export function waveSummary(w: WaveDefinition): { label: string; icon: string; count: number; elite: boolean; boss: boolean }[] {
  return w.groups.map((g) => {
    if (g.boss) return { label: BOSSES[g.boss].name, icon: '👑', count: 1, elite: false, boss: true };
    const d = ENEMIES[g.enemy];
    return { label: g.elite ? `Elite-${d.name}` : d.name, icon: d.icon, count: g.count, elite: g.elite, boss: false };
  });
}

export function keyLabel(k: string): string {
  return k === '-' ? '−' : k === '=' ? '=' : k.toUpperCase();
}

// ------------------------------------------------------------ 3.3: Konter direkt aus den Resistenzwerten

const pctTxt = (v: number): string => `${Math.round(Math.abs(v) * 100)} %`;

/** Gegner und Bosse, die gegen eine Schadensart schwach (≤ −10 %) bzw. resistent (≥ 20 %) sind. */
export function damageMatchups(type: import('../core/types.ts').DamageType): { good: string[]; bad: string[] } {
  const good: string[] = [];
  const bad: string[] = [];
  const add = (name: string, v: number | undefined) => {
    if (v === undefined) return;
    if (v <= -0.1) good.push(`${name} +${pctTxt(v)}`);
    else if (v >= 0.2) bad.push(`${name} −${pctTxt(v)}`);
  };
  for (const e of Object.values(ENEMIES)) if (e.id !== 'boss') add(e.name, e.resist[type]);
  for (const b of Object.values(BOSSES)) add(b.name, b.resist[type]);
  return { good, bad };
}

/** Kampftürme (nicht legendär) einer Schadensart, für Konter-Hinweise am Gegner. */
export function towersOfType(type: import('../core/types.ts').DamageType): string[] {
  return Object.values(TOWERS).filter((t) => t.damageType === type && !t.legendary && t.attack !== 'economy' && t.attack !== 'none' && t.attack !== 'pylon').map((t) => t.name);
}
