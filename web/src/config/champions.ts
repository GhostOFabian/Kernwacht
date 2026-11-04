import type { ChampionId, ChampionStats, DamageType, HeroId, StatusId } from '../core/types.ts';
import { HEROES, isHeroUnlocked } from './heroes.ts';

// Frei steuerbare Helden des Helden-Endlosmodus: Grundwerte, Stufenwachstum, Fähigkeiten und
// Fähigkeitenbäume. Neue Helden werden ausschließlich hier ergänzt (plus Fähigkeitslogik im ChampionSystem).

export type SkillTarget = 'self' | 'tower' | 'point';

export interface ChampionSkill {
  id: string;
  name: string;
  description: string;
  icon: string;          // SVG-Symbolname (ui/icons.ts)
  cooldown: number;
  mana: number;
  target: SkillTarget;
  /** Maximale Wirkentfernung vom Helden (0 = am Helden). */
  castRange: number;
  /** Wirkungsradius (für Vorschau). */
  radius: number;
  unlockLevel: number;
  /** Datengetriebene Wirkung (neue Helden). Ohne Angabe: eigene Logik im ChampionSystem. */
  effects?: SkillEffect[];
}

export interface StatusSpec { id: StatusId; dur: number; power?: number; stacks?: number }

/**
 * Bausteine für Heldenfähigkeiten. Schaden = (base + scale × Heldenschaden) × Fähigkeitsstärke,
 * Turmwerte × Unterstützungsstärke. 'at' = Wirkort (Held oder Zielpunkt).
 */
export type SkillEffect =
  | { kind: 'nova'; at: 'self' | 'target'; radius: number; base: number; scale: number; status?: StatusSpec; air?: boolean }
  | { kind: 'line'; length: number; width: number; base: number; scale: number; status?: StatusSpec }
  | { kind: 'blink' }
  | { kind: 'execute'; count: number; pct: number; bossPct: number }
  | { kind: 'towers'; at: 'self' | 'target'; radius: number; dmg?: number; speed?: number; range?: number; crit?: number; dur: number; repair?: boolean; src: string }
  | { kind: 'allTowers'; dmg?: number; speed?: number; range?: number; crit?: number; dur: number; src: string }
  | { kind: 'self'; add?: Partial<ChampionStats>; pct?: Partial<Record<keyof ChampionStats, number>>; dur: number; heal?: number; src: string }
  | { kind: 'strikes'; at: 'self' | 'target'; radius: number; count: number; base: number; scale: number; status?: StatusSpec }
  | { kind: 'statusAll'; radius: number; status: StatusSpec; at?: 'self' | 'target' }
  | { kind: 'gold'; base: number; perWave: number; resource?: 'metal' | 'crystal' | 'essence' | 'relic'; amount?: number }
  | { kind: 'taunt'; radius: number; dur: number }
  | { kind: 'interrupt'; radius: number; power: number }
  | { kind: 'zone'; zone: 'snare' | 'volley'; radius: number; dur: number; power: number; scale?: number };

/** Fähigkeitsmodifikatoren aus dem Baum (additiv je Rang). */
export interface SkillMods {
  buffDuration: number;
  buffRadius: number;
  extraTargets: number;
  synergy: number;
  auraDmg: number;
  auraSpeed: number;
  auraRange: number;
  auraCrit: number;
  whirlRadius: number;
  dashStun: number;
  pierceWidth: number;
  trapSlow: number;
  volleyPulses: number;
}

export const ZERO_SKILL_MODS: SkillMods = {
  buffDuration: 0, buffRadius: 0, extraTargets: 0, synergy: 0, auraDmg: 0, auraSpeed: 0, auraRange: 0, auraCrit: 0,
  whirlRadius: 0, dashStun: 0, pierceWidth: 0, trapSlow: 0, volleyPulses: 0,
};

export interface SkillNode {
  id: string;
  name: string;
  description: string;
  tier: number;
  maxRank: number;
  /** Wirkung je Rang. */
  stats?: Partial<ChampionStats>;
  mods?: Partial<SkillMods>;
  requires?: { id: string; rank: number }[];
  minLevel?: number;
  /** Dauerhafter Perk, der den Knoten freischaltet. */
  perk?: string;
  /** Erhöht Kernenergie (max. und aktuell) je Rang – wirkt sofort beim Lernen. */
  coreLives?: number;
}

export interface ChampionDefinition {
  id: ChampionId;
  name: string;
  title: string;
  role: 'Unterstützer' | 'Kämpfer' | 'Fernkämpferin';
  description: string;
  model: HeroId;          // vorhandenes 3D-Modell
  color: number;
  accent: number;
  damageType: DamageType;
  melee: boolean;
  base: ChampionStats;
  perLevel: Partial<ChampionStats>;
  skills: [ChampionSkill, ChampionSkill, ChampionSkill, ChampionSkill];
  tree: SkillNode[];
  /** Passive Turmaura (Grundwerte, × Unterstützungsstärke). */
  aura?: { dmg: number; speed: number };
  /** Dauerhafter Perk, der den Helden freischaltet (undefined = von Beginn an). */
  unlockPerk?: string;
  /** Kampagnenheld: frei, sobald er in der Kampagne freigeschaltet ist – oder ab Bestwelle unlockWave. */
  campaignHero?: HeroId;
  unlockWave?: number;
  /** Hält bis zu n Bodengegner in Nahkampfreichweite fest (Blocker). */
  blockCount?: number;
}

/** Für alle Helden: Kernwall (+3 Kernenergie je Rang) – die Zahl erlaubter Durchbrüche ist skillbar. */
const coreNode = (p: string): SkillNode => ({ id: `${p}_core`, name: 'Kernwall', tier: 1, maxRank: 5, coreLives: 3, description: '+3 Kernenergie (maximal und sofort) je Rang – mehr Gegner dürfen durchbrechen.' });

/** Einheitlicher Baum für Kampagnenhelden (11 Knoten, Stufe 4 mit Meisterknoten). */
function campaignTree(p: string, o: { s1: Partial<ChampionStats>; s1n: string; s1d: string; s2: Partial<ChampionStats>; s2n: string; s2d: string; s3: Partial<ChampionStats>; s3n: string; s3d: string; m1: Partial<ChampionStats>; m1n: string; m1d: string }): SkillNode[] {
  return [
    { id: `${p}_a`, name: o.s1n, tier: 1, maxRank: 5, stats: o.s1, description: `${o.s1d} je Rang.` },
    { id: `${p}_hp`, name: 'Zähigkeit', tier: 1, maxRank: 5, stats: { maxHp: 35, armor: 0.008 }, description: '+35 LP und +0,8 % Rüstung je Rang.' },
    coreNode(p),
    { id: `${p}_b`, name: o.s2n, tier: 2, maxRank: 3, stats: o.s2, requires: [{ id: `${p}_a`, rank: 2 }], description: `${o.s2d} je Rang.` },
    { id: `${p}_ap`, name: 'Fähigkeitsfokus', tier: 2, maxRank: 3, stats: { abilityPower: 0.08, cdr: 0.03 }, requires: [{ id: `${p}_hp`, rank: 2 }], description: '+8 % Fähigkeitsstärke und +3 % Abklingzeitreduktion je Rang.' },
    { id: `${p}_mana`, name: 'Manaquelle', tier: 2, maxRank: 3, stats: { maxMana: 15, manaRegen: 0.8 }, description: '+15 Mana und +0,8 Manaregeneration je Rang.' },
    { id: `${p}_c`, name: o.s3n, tier: 3, maxRank: 3, stats: o.s3, requires: [{ id: `${p}_b`, rank: 1 }], minLevel: 5, description: `${o.s3d} je Rang.` },
    { id: `${p}_life`, name: 'Lebenswille', tier: 3, maxRank: 3, stats: { hpRegen: 1.5, lifesteal: 0.02 }, requires: [{ id: `${p}_ap`, rank: 1 }], description: '+1,5 LP/s und +2 % Lebensraub je Rang.' },
    { id: `${p}_move`, name: 'Leichtfuß', tier: 3, maxRank: 2, stats: { moveSpeed: 0.25, pickupRadius: 0.15 }, description: '+0,25 Tempo und +0,15 Aufsammelradius je Rang.' },
    { id: `${p}_master`, name: o.m1n, tier: 4, maxRank: 1, stats: o.m1, requires: [{ id: `${p}_c`, rank: 2 }], minLevel: 10, perk: 'mastery', description: `Meisterknoten: ${o.m1d}.` },
    { id: `${p}_wall`, name: 'Unerschütterlich', tier: 4, maxRank: 1, stats: { maxHp: 180, armor: 0.06 }, requires: [{ id: `${p}_life`, rank: 1 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: +180 LP und +6 % Rüstung.' },
  ];
}

const BASE: ChampionStats = {
  maxHp: 400, hpRegen: 1, maxMana: 100, manaRegen: 4, damage: 10, attackSpeed: 1, range: 1, moveSpeed: 2.6,
  armor: 0, critChance: 0.05, critDamage: 1.6, cdr: 0, abilityPower: 1, supportPower: 1, supportRadius: 2.5,
  lootBonus: 0, pickupRadius: 0.9, lifesteal: 0, cleave: 0,
};

export const CHAMPIONS: Record<ChampionId, ChampionDefinition> = {
  lyra: {
    id: 'lyra', name: 'Lyra', title: 'Turmweberin', role: 'Unterstützer', model: 'mystic', color: 0x6a4ab0, accent: 0xffd860,
    description: 'Verstärkt Türme mit Auren und gezielten Runen. Schwach im direkten Kampf, aber mit klugem Stellungsspiel kann sie ganze Verteidigungslinien tragen.',
    damageType: 'arcane', melee: false, aura: { dmg: 0.05, speed: 0.12 },
    base: { ...BASE, maxHp: 360, damage: 9, attackSpeed: 0.9, range: 3, supportRadius: 2.6, maxMana: 130, manaRegen: 5 },
    perLevel: { maxHp: 26, damage: 1.2, maxMana: 8, supportPower: 0.03 },
    skills: [
      { id: 'inspire', name: 'Runensegen', icon: 'up', cooldown: 8, mana: 25, target: 'tower', castRange: 6, radius: 0.6, unlockLevel: 1,
        description: 'Ein Turm erhält 8 s lang +50 % Schaden und +30 % Angriffstempo.' },
      { id: 'field', name: 'Kraftfeld', icon: 'range', cooldown: 14, mana: 35, target: 'point', castRange: 7, radius: 2.5, unlockLevel: 2,
        description: 'Türme im Feld erhalten 10 s lang +35 % Angriffstempo und +0,5 Reichweite.' },
      { id: 'pulse', name: 'Resonanzpuls', icon: 'bolt', cooldown: 20, mana: 40, target: 'self', castRange: 0, radius: 4, unlockLevel: 4,
        description: 'Türme im Umkreis feuern sofort erneut und erhalten 6 s lang +15 % kritische Trefferchance.' },
      { id: 'citadel', name: 'Zitadelle', icon: 'crown', cooldown: 60, mana: 70, target: 'point', castRange: 7, radius: 3.5, unlockLevel: 6,
        description: 'Ultimate: Türme im Bereich erhalten 12 s lang +50 % Schaden, +40 % Tempo, +0,6 Reichweite und +10 % Krit.' },
    ],
    tree: [
      { id: 'l_dmg', name: 'Turmmacht', tier: 1, maxRank: 5, mods: { auraDmg: 0.04 }, description: 'Aura: +4 % Turmschaden je Rang.' },
      { id: 'l_spd', name: 'Taktgeber', tier: 1, maxRank: 5, mods: { auraSpeed: 0.04 }, description: 'Aura: +4 % Turmangriffstempo je Rang.' },
      coreNode('l'),
      { id: 'l_mana', name: 'Runenfluss', tier: 1, maxRank: 3, stats: { maxMana: 15, manaRegen: 1 }, description: '+15 Mana und +1 Manaregeneration je Rang.' },
      { id: 'l_range', name: 'Weitblick', tier: 2, maxRank: 3, mods: { auraRange: 0.2 }, requires: [{ id: 'l_dmg', rank: 2 }], description: 'Aura: +0,2 Turmreichweite je Rang.' },
      { id: 'l_dur', name: 'Langer Atem', tier: 2, maxRank: 3, mods: { buffDuration: 0.2 }, requires: [{ id: 'l_spd', rank: 2 }], description: '+20 % Dauer aller Turmverstärkungen je Rang.' },
      { id: 'l_radius', name: 'Weite Kreise', tier: 2, maxRank: 3, mods: { buffRadius: 0.15 }, stats: { supportRadius: 0.3 }, description: '+15 % Wirkradius und +0,3 Aurareichweite je Rang.' },
      { id: 'l_multi', name: 'Doppelsegen', tier: 3, maxRank: 2, mods: { extraTargets: 1 }, requires: [{ id: 'l_dur', rank: 2 }], minLevel: 6, description: 'Runensegen trifft je Rang einen weiteren Turm in der Nähe (halbe Stärke).' },
      { id: 'l_synergy', name: 'Elementarchor', tier: 3, maxRank: 3, mods: { synergy: 0.04 }, requires: [{ id: 'l_range', rank: 2 }], description: 'Kraftfeld: +4 % Schaden je verschiedener Schadensart im Feld, je Rang.' },
      { id: 'l_crit', name: 'Präzisionsrunen', tier: 3, maxRank: 3, mods: { auraCrit: 0.04 }, requires: [{ id: 'l_radius', rank: 1 }], description: 'Aura: +4 % kritische Trefferchance für Türme je Rang.' },
      { id: 'l_master', name: 'Erzweberin', tier: 4, maxRank: 1, stats: { supportPower: 0.25 }, requires: [{ id: 'l_multi', rank: 1 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: alle Turmverstärkungen +25 %.' },
      { id: 'l_aura', name: 'Ewige Aura', tier: 4, maxRank: 1, stats: { supportRadius: 1 }, mods: { auraDmg: 0.1, auraSpeed: 0.1 }, requires: [{ id: 'l_crit', rank: 1 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: Aura +1 Feld, +10 % Schaden und Tempo.' },
    ],
  },
  brann: {
    id: 'brann', name: 'Brann', title: 'Klingenrufer', role: 'Kämpfer', model: 'blade', color: 0xb04a3a, accent: 0xffc080,
    description: 'Berserker und Wellenbrecher: hält bis zu zwei Gegner im Nahkampf fest, trifft mit jedem Hieb Umstehende, heilt sich durch Treffer und fordert ganze Gruppen heraus.',
    damageType: 'physical', melee: true, blockCount: 2,
    base: { ...BASE, maxHp: 820, hpRegen: 3, damage: 32, attackSpeed: 1.25, range: 1.05, armor: 0.18, moveSpeed: 2.8, cleave: 0.35, lifesteal: 0.04 },
    perLevel: { maxHp: 70, damage: 3.6, armor: 0.006, hpRegen: 0.25 },
    skills: [
      { id: 'whirl', name: 'Wirbelschlag', icon: 'sword', cooldown: 6, mana: 20, target: 'self', castRange: 0, radius: 1.7, unlockLevel: 1,
        description: 'Trifft alle Gegner im Umkreis von 1,7 Feldern (60 + 150 % Schaden).' },
      { id: 'charge', name: 'Sturmangriff', icon: 'advance', cooldown: 9, mana: 25, target: 'point', castRange: 5, radius: 0.8, unlockLevel: 2,
        description: 'Stürmt zu einem Punkt, verletzt Gegner auf dem Weg (80 + 170 % Schaden) und betäubt sie 1,5 s.' },
      { id: 'warcry', name: 'Herausforderung', icon: 'shield', cooldown: 16, mana: 30, target: 'self', castRange: 0, radius: 3, unlockLevel: 4,
        description: 'Fesselt Bodengegner im Umkreis von 3 Feldern 2 s; 6 s lang +40 % Angriffstempo und +20 % Rüstung, heilt 15 % der LP.' },
      { id: 'bladestorm', name: 'Klingensturm', icon: 'crown', cooldown: 45, mana: 60, target: 'self', castRange: 0, radius: 2, unlockLevel: 6,
        description: 'Ultimate: 5 s lang alle 0,5 s Schaden im Umkreis von 2 Feldern (35 + 80 % Schaden), getroffene Gegner werden verlangsamt.' },
    ],
    tree: [
      { id: 'b_dmg', name: 'Schärfe', tier: 1, maxRank: 5, stats: { damage: 4 }, description: '+4 Schaden je Rang.' },
      { id: 'b_hp', name: 'Zähigkeit', tier: 1, maxRank: 5, stats: { maxHp: 60, armor: 0.012 }, description: '+60 LP und +1,2 % Rüstung je Rang.' },
      { id: 'b_spd', name: 'Schnelle Hiebe', tier: 1, maxRank: 5, stats: { attackSpeed: 0.07 }, description: '+0,07 Angriffe/s je Rang.' },
      coreNode('b'),
      { id: 'b_crit', name: 'Schwachstellen', tier: 2, maxRank: 3, stats: { critChance: 0.06 }, requires: [{ id: 'b_dmg', rank: 2 }], description: '+6 % kritische Trefferchance je Rang.' },
      { id: 'b_life', name: 'Blutdurst', tier: 2, maxRank: 3, stats: { lifesteal: 0.05 }, requires: [{ id: 'b_hp', rank: 2 }], description: '+5 % Lebensraub je Rang.' },
      { id: 'b_cleave', name: 'Spaltschlag', tier: 2, maxRank: 3, stats: { cleave: 0.15 }, requires: [{ id: 'b_spd', rank: 2 }], description: 'Angriffe treffen Umstehende mit +15 % Schaden je Rang.' },
      { id: 'b_whirl', name: 'Wirbelmeister', tier: 3, maxRank: 3, stats: { abilityPower: 0.12 }, mods: { whirlRadius: 0.15 }, minLevel: 5, requires: [{ id: 'b_cleave', rank: 1 }], description: '+15 % Wirbelradius und +12 % Fähigkeitsstärke je Rang.' },
      { id: 'b_dash', name: 'Erschütterung', tier: 3, maxRank: 2, mods: { dashStun: 0.5 }, requires: [{ id: 'b_whirl', rank: 1 }], description: 'Sturmangriff betäubt 0,5 s länger je Rang.' },
      { id: 'b_regen', name: 'Regeneration', tier: 3, maxRank: 3, stats: { hpRegen: 2.5 }, requires: [{ id: 'b_life', rank: 1 }], description: '+2,5 LP/s je Rang.' },
      { id: 'b_master', name: 'Kriegsherr', tier: 4, maxRank: 1, stats: { damage: 16, critDamage: 0.5 }, requires: [{ id: 'b_crit', rank: 2 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: +16 Schaden, +50 % kritischer Schaden.' },
      { id: 'b_wall', name: 'Bollwerk', tier: 4, maxRank: 1, stats: { armor: 0.12, maxHp: 260 }, requires: [{ id: 'b_regen', rank: 1 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: +12 % Rüstung, +260 LP.' },
    ],
  },
  vela: {
    id: 'vela', name: 'Vela', title: 'Sturmschützin', role: 'Fernkämpferin', model: 'hunter', color: 0x2a8a70, accent: 0x9affd8,
    description: 'Schnelle Fernkämpferin mit durchschlagenden Pfeilen, Fallen und Pfeilhagel. Wird über die Welle 15 freigeschaltet.',
    damageType: 'physical', melee: false, unlockPerk: 'vela',
    base: { ...BASE, maxHp: 440, damage: 15, attackSpeed: 1.35, range: 4, critChance: 0.08, moveSpeed: 2.9 },
    perLevel: { maxHp: 32, damage: 2, attackSpeed: 0.02 },
    skills: [
      { id: 'pierce', name: 'Durchschlag', icon: 'advance', cooldown: 7, mana: 25, target: 'point', castRange: 7, radius: 0.4, unlockLevel: 1,
        description: 'Ein Pfeil durchbohrt alle Gegner auf einer Linie (50 + 140 % Schaden).' },
      { id: 'trap', name: 'Fallensaat', icon: 'range', cooldown: 12, mana: 30, target: 'point', castRange: 6, radius: 1.3, unlockLevel: 2,
        description: 'Eine Falle verlangsamt Gegner 6 s lang stark und fügt Schaden über Zeit zu.' },
      { id: 'focus', name: 'Fokus', icon: 'star', cooldown: 20, mana: 30, target: 'self', castRange: 0, radius: 0, unlockLevel: 4,
        description: '8 s lang +35 % kritische Trefferchance und +20 % Angriffstempo.' },
      { id: 'volley', name: 'Pfeilhagel', icon: 'crown', cooldown: 45, mana: 60, target: 'point', castRange: 8, radius: 2.2, unlockLevel: 6,
        description: 'Ultimate: drei Salven auf ein Gebiet (je 30 + 80 % Schaden).' },
    ],
    tree: [
      { id: 'v_dmg', name: 'Spitzen', tier: 1, maxRank: 5, stats: { damage: 2.5 }, description: '+2,5 Schaden je Rang.' },
      { id: 'v_spd', name: 'Schnellschuss', tier: 1, maxRank: 5, stats: { attackSpeed: 0.07 }, description: '+0,07 Angriffe/s je Rang.' },
      coreNode('v'),
      { id: 'v_range', name: 'Adlerblick', tier: 1, maxRank: 3, stats: { range: 0.3 }, description: '+0,3 Reichweite je Rang.' },
      { id: 'v_crit', name: 'Tödliche Präzision', tier: 2, maxRank: 3, stats: { critChance: 0.06, critDamage: 0.1 }, requires: [{ id: 'v_dmg', rank: 2 }], description: '+6 % Krit und +10 % kritischer Schaden je Rang.' },
      { id: 'v_pierce', name: 'Breitspitzen', tier: 2, maxRank: 3, stats: { abilityPower: 0.08 }, mods: { pierceWidth: 0.1 }, requires: [{ id: 'v_spd', rank: 2 }], description: 'Durchschlag breiter, +8 % Fähigkeitsstärke je Rang.' },
      { id: 'v_trap', name: 'Fallenkunde', tier: 2, maxRank: 3, mods: { trapSlow: 0.1 }, requires: [{ id: 'v_range', rank: 1 }], description: 'Fallen verlangsamen 10 % stärker je Rang.' },
      { id: 'v_volley', name: 'Salvenmeisterin', tier: 3, maxRank: 2, mods: { volleyPulses: 1 }, requires: [{ id: 'v_pierce', rank: 1 }], minLevel: 6, description: 'Pfeilhagel: eine Salve mehr je Rang.' },
      { id: 'v_move', name: 'Leichtfuß', tier: 3, maxRank: 3, stats: { moveSpeed: 0.25 }, description: '+0,25 Bewegungstempo je Rang.' },
      { id: 'v_mana', name: 'Konzentration', tier: 3, maxRank: 3, stats: { manaRegen: 1, cdr: 0.03 }, requires: [{ id: 'v_trap', rank: 1 }], description: '+1 Manaregeneration, +3 % Abklingzeitreduktion je Rang.' },
      { id: 'v_master', name: 'Sturmherrin', tier: 4, maxRank: 1, stats: { attackSpeed: 0.3, abilityPower: 0.2 }, requires: [{ id: 'v_crit', rank: 2 }], minLevel: 10, perk: 'mastery', description: 'Meisterknoten: +0,3 Angriffe/s, +20 % Fähigkeitsstärke.' },
    ],
  },
  // ---------------------------------------------------------------- Kampagnenhelden
  kaela: {
    id: 'kaela', name: 'Kaela', title: 'Klingenwächterin', role: 'Kämpfer', model: 'blade', color: HEROES.blade.color, accent: HEROES.blade.accent,
    description: 'Die Schildträgerin der Kampagne: hält bis zu vier Gegner fest, verspottet ganze Gruppen und wird mit dem Bollwerk nahezu unverwundbar.',
    damageType: 'physical', melee: true, blockCount: 4, campaignHero: 'blade', unlockWave: 1,
    base: { ...BASE, maxHp: 900, hpRegen: 3, damage: 20, attackSpeed: 1.0, range: 1.05, armor: 0.25, moveSpeed: 2.5, cleave: 0.2 },
    perLevel: { maxHp: 80, damage: 2.2, armor: 0.008, hpRegen: 0.3 },
    skills: [
      { id: 'k_taunt', name: 'Spottschrei', icon: 'shield', cooldown: 12, mana: 25, target: 'self', castRange: 0, radius: 3, unlockLevel: 1,
        description: 'Fesselt Bodengegner im Umkreis von 3 Feldern 2 s; 6 s +25 % Rüstung.',
        effects: [{ kind: 'taunt', radius: 3, dur: 2 }, { kind: 'self', add: { armor: 0.25 }, dur: 6, src: 'k_taunt' }] },
      { id: 'k_whirl', name: 'Wirbelklinge', icon: 'sword', cooldown: 7, mana: 20, target: 'self', castRange: 0, radius: 1.6, unlockLevel: 2,
        description: 'Trifft alle Gegner im Umkreis von 1,6 Feldern (50 + 130 % Schaden).',
        effects: [{ kind: 'nova', at: 'self', radius: 1.6, base: 50, scale: 1.3 }] },
      { id: 'k_wall', name: 'Schildwall', icon: 'hold', cooldown: 18, mana: 30, target: 'self', castRange: 0, radius: 0, unlockLevel: 4,
        description: 'Heilt 20 % der LP, 6 s +35 % Rüstung.',
        effects: [{ kind: 'self', add: { armor: 0.35 }, dur: 6, heal: 0.2, src: 'k_wall' }] },
      { id: 'k_bulwark', name: 'Bollwerk', icon: 'crown', cooldown: 55, mana: 60, target: 'self', castRange: 0, radius: 3, unlockLevel: 6,
        description: 'Ultimate: 6 s maximale Rüstung, alle Bodengegner im Umkreis von 3 Feldern werden 4 s gefesselt.',
        effects: [{ kind: 'self', add: { armor: 0.8 }, dur: 6, src: 'k_bulwark' }, { kind: 'taunt', radius: 3, dur: 4 }] },
    ],
    tree: campaignTree('k', { s1: { armor: 0.015 }, s1n: 'Plattenrüstung', s1d: '+1,5 % Rüstung', s2: { damage: 4 }, s2n: 'Scharfe Klinge', s2d: '+4 Schaden', s3: { cleave: 0.12, abilityPower: 0.06 }, s3n: 'Klingentanz', s3d: '+12 % Flächenschlag, +6 % Fähigkeitsstärke', m1: { maxHp: 300, hpRegen: 4 }, m1n: 'Unbezwingbar', m1d: '+300 LP, +4 LP/s' }),
  },
  varo: {
    id: 'varo', name: 'Varo', title: 'Aschenjäger', role: 'Fernkämpferin', model: 'hunter', color: HEROES.hunter.color, accent: HEROES.hunter.accent,
    description: 'Flammenpfeile, Glutfallen und ein Feuerregen: Varo setzt ganze Wellen in Brand.',
    damageType: 'fire', melee: false, campaignHero: 'hunter', unlockWave: 10,
    base: { ...BASE, maxHp: 430, damage: 17, attackSpeed: 1.2, range: 4.2, critChance: 0.06, moveSpeed: 2.8 },
    perLevel: { maxHp: 30, damage: 2.2, attackSpeed: 0.015 },
    skills: [
      { id: 'o_arrow', name: 'Brandpfeil', icon: 'advance', cooldown: 7, mana: 20, target: 'point', castRange: 7, radius: 0.5, unlockLevel: 1,
        description: 'Ein Pfeil durchbohrt eine Linie (40 + 120 % Schaden) und setzt Gegner 3 s in Brand.',
        effects: [{ kind: 'line', length: 7, width: 0.5, base: 40, scale: 1.2, status: { id: 'burn', dur: 3, power: 12 } }] },
      { id: 'o_trap', name: 'Glutfalle', icon: 'range', cooldown: 10, mana: 25, target: 'point', castRange: 6, radius: 1.3, unlockLevel: 2,
        description: 'Explodiert im Ziel (30 + 60 % Schaden) und lässt Gegner 6 s brennen.',
        effects: [{ kind: 'nova', at: 'target', radius: 1.3, base: 30, scale: 0.6, status: { id: 'burn', dur: 6, power: 10 } }] },
      { id: 'o_fever', name: 'Jagdfieber', icon: 'star', cooldown: 18, mana: 30, target: 'self', castRange: 0, radius: 0, unlockLevel: 4,
        description: '8 s +40 % Angriffstempo und +15 % kritische Trefferchance.',
        effects: [{ kind: 'self', pct: { attackSpeed: 0.4 }, add: { critChance: 0.15 }, dur: 8, src: 'o_fever' }] },
      { id: 'o_rain', name: 'Feuerregen', icon: 'crown', cooldown: 45, mana: 60, target: 'point', castRange: 8, radius: 2.5, unlockLevel: 6,
        description: 'Ultimate: 8 Feuerbrocken auf Gegner im Gebiet (je 60 + 80 % Schaden, Brand).',
        effects: [{ kind: 'strikes', at: 'target', radius: 2.5, count: 8, base: 60, scale: 0.8, status: { id: 'burn', dur: 4, power: 14 } }] },
    ],
    tree: campaignTree('o', { s1: { damage: 2.5 }, s1n: 'Glühende Spitzen', s1d: '+2,5 Schaden', s2: { attackSpeed: 0.08 }, s2n: 'Schnellschuss', s2d: '+0,08 Angriffe/s', s3: { range: 0.3, critChance: 0.04 }, s3n: 'Adlerauge', s3d: '+0,3 Reichweite, +4 % Krit', m1: { damage: 12, critDamage: 0.4 }, m1n: 'Meisterschütze', m1d: '+12 Schaden, +40 % kritischer Schaden' }),
  },
  nyx: {
    id: 'nyx', name: 'Nyx', title: 'Risswandler', role: 'Kämpfer', model: 'riftwalker', color: HEROES.riftwalker.color, accent: HEROES.riftwalker.accent,
    description: 'Assassine zwischen den Welten: springt durch Risse, markiert Gegner und richtet die Gefährlichsten hin.',
    damageType: 'arcane', melee: true, campaignHero: 'riftwalker', unlockWave: 25,
    base: { ...BASE, maxHp: 520, damage: 26, attackSpeed: 1.45, range: 1.05, critChance: 0.12, critDamage: 1.9, moveSpeed: 3.2 },
    perLevel: { maxHp: 40, damage: 3.2, critChance: 0.004 },
    skills: [
      { id: 'n_step', name: 'Rissschritt', icon: 'advance', cooldown: 7, mana: 20, target: 'point', castRange: 6, radius: 1, unlockLevel: 1,
        description: 'Teleportiert zum Ziel: 70 + 160 % Schaden im Umkreis von 1 Feld, betäubt 0,6 s.',
        effects: [{ kind: 'blink' }, { kind: 'nova', at: 'target', radius: 1, base: 70, scale: 1.6, status: { id: 'stun', dur: 0.6 } }] },
      { id: 'n_mark', name: 'Leerenmal', icon: 'range', cooldown: 11, mana: 25, target: 'point', castRange: 6, radius: 2, unlockLevel: 2,
        description: 'Gegner im Umkreis von 2 Feldern erleiden 5 s +30 % Schaden.',
        effects: [{ kind: 'statusAll', at: 'target', radius: 2, status: { id: 'voidMark', dur: 5, power: 0.3 } }] },
      { id: 'n_veil', name: 'Schattenschleier', icon: 'star', cooldown: 16, mana: 30, target: 'self', castRange: 0, radius: 0, unlockLevel: 4,
        description: '6 s +30 % kritische Trefferchance, +1 Tempo, +20 % Rüstung.',
        effects: [{ kind: 'self', add: { critChance: 0.3, moveSpeed: 1, armor: 0.2 }, dur: 6, src: 'n_veil' }] },
      { id: 'n_exec', name: 'Hinrichtung', icon: 'crown', cooldown: 50, mana: 60, target: 'self', castRange: 0, radius: 0, unlockLevel: 6,
        description: 'Ultimate: die 5 Gegner nächst am Kern verlieren 30 % ihrer max. LP (Bosse 6 %).',
        effects: [{ kind: 'execute', count: 5, pct: 0.3, bossPct: 0.06 }] },
    ],
    tree: campaignTree('n', { s1: { damage: 3.5 }, s1n: 'Leerenklinge', s1d: '+3,5 Schaden', s2: { critChance: 0.05 }, s2n: 'Schwachpunkt', s2d: '+5 % Krit', s3: { attackSpeed: 0.1, moveSpeed: 0.15 }, s3n: 'Flinke Hände', s3d: '+0,1 Angriffe/s, +0,15 Tempo', m1: { critDamage: 0.6, damage: 10 }, m1n: 'Todesbote', m1d: '+60 % kritischer Schaden, +10 Schaden' }),
  },
  liora: {
    id: 'liora', name: 'Liora', title: 'Runenbardin', role: 'Unterstützer', model: 'bard', color: HEROES.bard.color, accent: HEROES.bard.accent,
    description: 'Ihre Lieder treiben Türme an: Angriffstempo für ganze Turmgruppen und eine Hymne, die die gesamte Verteidigung beschleunigt.',
    damageType: 'sonic', melee: false, campaignHero: 'bard', unlockWave: 1, aura: { dmg: 0.04, speed: 0.1 },
    base: { ...BASE, maxHp: 380, damage: 9, attackSpeed: 1.0, range: 3.2, supportRadius: 3, maxMana: 130, manaRegen: 5 },
    perLevel: { maxHp: 26, damage: 1.1, maxMana: 8, supportPower: 0.03 },
    skills: [
      { id: 'li_insp', name: 'Inspiration', icon: 'up', cooldown: 10, mana: 25, target: 'self', castRange: 0, radius: 3, unlockLevel: 1,
        description: 'Türme im Umkreis von 3 Feldern: 6 s +30 % Schaden.',
        effects: [{ kind: 'towers', at: 'self', radius: 3, dmg: 0.3, dur: 6, src: 'li_insp' }] },
      { id: 'li_reso', name: 'Lied der Resonanz', icon: 'range', cooldown: 16, mana: 35, target: 'self', castRange: 0, radius: 4.5, unlockLevel: 2,
        description: 'Türme im Umkreis von 4,5 Feldern: 8 s +35 % Angriffstempo.',
        effects: [{ kind: 'towers', at: 'self', radius: 4.5, speed: 0.35, dur: 8, src: 'li_reso' }] },
      { id: 'li_lull', name: 'Schlaflied', icon: 'bolt', cooldown: 14, mana: 30, target: 'point', castRange: 6, radius: 2, unlockLevel: 4,
        description: 'Gegner im Umkreis von 2 Feldern werden stark verlangsamt, Liora heilt 20 % ihrer LP.',
        effects: [{ kind: 'statusAll', at: 'target', radius: 2, status: { id: 'chill', dur: 3, power: 0.2, stacks: 3 } }, { kind: 'self', dur: 0, heal: 0.2, src: 'li_lull' }] },
      { id: 'li_hymn', name: 'Hymne der Welten', icon: 'crown', cooldown: 60, mana: 70, target: 'self', castRange: 0, radius: 0, unlockLevel: 6,
        description: 'Ultimate: alle Türme 8 s +50 % Angriffstempo.',
        effects: [{ kind: 'allTowers', speed: 0.5, dur: 8, src: 'li_hymn' }] },
    ],
    tree: campaignTree('li', { s1: { supportPower: 0.04 }, s1n: 'Kraftvolle Stimme', s1d: '+4 % Unterstützungsstärke', s2: { supportRadius: 0.4 }, s2n: 'Weiter Klang', s2d: '+0,4 Auraradius', s3: { supportPower: 0.06, cdr: 0.03 }, s3n: 'Chorleiterin', s3d: '+6 % Unterstützungsstärke, +3 % Abklingzeitreduktion', m1: { supportPower: 0.25 }, m1n: 'Meisterlied', m1d: '+25 % Unterstützungsstärke' }),
  },
  thorgrim: {
    id: 'thorgrim', name: 'Thorgrim', title: 'Schmiedemeister', role: 'Unterstützer', model: 'smith', color: HEROES.smith.color, accent: HEROES.smith.accent,
    description: 'Repariert und verbessert Türme direkt im Feld und haut mit dem Hammer Gegner um. Hält einen Gegner fest.',
    damageType: 'physical', melee: true, blockCount: 1, campaignHero: 'smith', unlockWave: 1, aura: { dmg: 0.05, speed: 0.03 },
    base: { ...BASE, maxHp: 640, hpRegen: 2, damage: 18, attackSpeed: 0.95, range: 1.05, armor: 0.15, supportRadius: 2.6, moveSpeed: 2.5 },
    perLevel: { maxHp: 55, damage: 2.4, armor: 0.006, supportPower: 0.02 },
    skills: [
      { id: 't_forge', name: 'Feldschmiede', icon: 'up', cooldown: 14, mana: 25, target: 'tower', castRange: 5, radius: 0.6, unlockLevel: 1,
        description: 'Ein Turm erhält 20 s +35 % Schaden und +0,5 Reichweite.',
        effects: [{ kind: 'towers', at: 'target', radius: 0.7, dmg: 0.35, range: 0.5, dur: 20, src: 't_forge' }] },
      { id: 't_repair', name: 'Reparaturwelle', icon: 'range', cooldown: 16, mana: 30, target: 'self', castRange: 0, radius: 5, unlockLevel: 2,
        description: 'Beseitigt Betäubung und Störung aller Türme im Umkreis von 5 Feldern, 6 s +15 % Tempo.',
        effects: [{ kind: 'towers', at: 'self', radius: 5, speed: 0.15, dur: 6, repair: true, src: 't_repair' }] },
      { id: 't_hammer', name: 'Hammerschlag', icon: 'sword', cooldown: 9, mana: 20, target: 'self', castRange: 0, radius: 1.4, unlockLevel: 4,
        description: 'Erdbeben im Umkreis von 1,4 Feldern (60 + 140 % Schaden), betäubt 1 s.',
        effects: [{ kind: 'nova', at: 'self', radius: 1.4, base: 60, scale: 1.4, status: { id: 'stun', dur: 1 } }] },
      { id: 't_master', name: 'Meisterwerk', icon: 'crown', cooldown: 60, mana: 70, target: 'self', castRange: 0, radius: 4, unlockLevel: 6,
        description: 'Ultimate: Türme im Umkreis von 4 Feldern 15 s +40 % Schaden, +0,5 Reichweite, +10 % Krit.',
        effects: [{ kind: 'towers', at: 'self', radius: 4, dmg: 0.4, range: 0.5, crit: 0.1, dur: 15, src: 't_master' }] },
    ],
    tree: campaignTree('t', { s1: { supportPower: 0.04, damage: 1 }, s1n: 'Fleiß', s1d: '+4 % Unterstützungsstärke, +1 Schaden', s2: { armor: 0.03 }, s2n: 'Kettenhemd', s2d: '+3 % Rüstung', s3: { supportRadius: 0.4, supportPower: 0.05 }, s3n: 'Weite Werkstatt', s3d: '+0,4 Auraradius, +5 % Unterstützungsstärke', m1: { supportPower: 0.2, damage: 10 }, m1n: 'Großmeister', m1d: '+20 % Unterstützungsstärke, +10 Schaden' }),
  },
  sena: {
    id: 'sena', name: 'Sena', title: 'Wettermacherin', role: 'Fernkämpferin', model: 'weather', color: HEROES.weather.color, accent: HEROES.weather.accent,
    description: 'Lässt es regnen, damit Blitze doppelt treffen, friert Gruppen ein und ruft ein Gewitter herab.',
    damageType: 'lightning', melee: false, campaignHero: 'weather', unlockWave: 15,
    base: { ...BASE, maxHp: 400, damage: 14, attackSpeed: 0.95, range: 3.8, maxMana: 140, manaRegen: 5.5, abilityPower: 1.1 },
    perLevel: { maxHp: 28, damage: 1.8, maxMana: 8, abilityPower: 0.02 },
    skills: [
      { id: 's_rain', name: 'Regenguss', icon: 'bolt', cooldown: 15, mana: 25, target: 'self', castRange: 0, radius: 0, unlockLevel: 1,
        description: 'Alle Gegner werden 6 s nass (Blitzschaden +50 %).',
        effects: [{ kind: 'statusAll', radius: 0, status: { id: 'wet', dur: 6 } }] },
      { id: 's_frost', name: 'Frosthauch', icon: 'range', cooldown: 10, mana: 30, target: 'self', castRange: 0, radius: 4, unlockLevel: 2,
        description: 'Gegner im Umkreis von 4 Feldern: 20 + 60 % Schaden, stark unterkühlt.',
        effects: [{ kind: 'nova', at: 'self', radius: 4, base: 20, scale: 0.6, status: { id: 'chill', dur: 3, power: 0.25, stacks: 3 }, air: true }] },
      { id: 's_bolt', name: 'Blitzschlag', icon: 'advance', cooldown: 8, mana: 25, target: 'point', castRange: 7, radius: 1.5, unlockLevel: 4,
        description: 'Drei Blitze im Zielgebiet (je 50 + 100 % Schaden).',
        effects: [{ kind: 'strikes', at: 'target', radius: 1.5, count: 3, base: 50, scale: 1 }] },
      { id: 's_storm', name: 'Gewittersturm', icon: 'crown', cooldown: 55, mana: 70, target: 'self', castRange: 0, radius: 9, unlockLevel: 6,
        description: 'Ultimate: 12 Blitzeinschläge (je 70 + 80 % Schaden) auf Gegner im weiten Umkreis, alle werden nass.',
        effects: [{ kind: 'statusAll', radius: 9, status: { id: 'wet', dur: 4 } }, { kind: 'strikes', at: 'self', radius: 9, count: 12, base: 70, scale: 0.8 }] },
    ],
    tree: campaignTree('s', { s1: { abilityPower: 0.05 }, s1n: 'Sturmkraft', s1d: '+5 % Fähigkeitsstärke', s2: { range: 0.3 }, s2n: 'Sturmauge', s2d: '+0,3 Reichweite', s3: { cdr: 0.05, manaRegen: 1 }, s3n: 'Monsun', s3d: '+5 % Abklingzeitreduktion, +1 Mana/s', m1: { abilityPower: 0.3 }, m1n: 'Blitzableiterin', m1d: '+30 % Fähigkeitsstärke' }),
  },
  oran: {
    id: 'oran', name: 'Oran', title: 'Leylinien-Mystiker', role: 'Unterstützer', model: 'mystic', color: HEROES.mystic.color, accent: HEROES.mystic.accent,
    description: 'Setzt Ley-Knoten, die Türme dauerhaft stärken, feuert arkane Ströme und webt ein Netz über alle Türme.',
    damageType: 'arcane', melee: false, campaignHero: 'mystic', unlockWave: 30, aura: { dmg: 0.08, speed: 0 },
    base: { ...BASE, maxHp: 380, damage: 12, attackSpeed: 0.9, range: 3.4, supportRadius: 2.6, maxMana: 140, manaRegen: 5 },
    perLevel: { maxHp: 26, damage: 1.5, maxMana: 8, supportPower: 0.03 },
    skills: [
      { id: 'or_node', name: 'Ley-Knoten', icon: 'up', cooldown: 20, mana: 30, target: 'point', castRange: 6, radius: 2, unlockLevel: 1,
        description: 'Türme im Umkreis von 2 Feldern um das Ziel: 30 s +15 % Schaden.',
        effects: [{ kind: 'towers', at: 'target', radius: 2, dmg: 0.15, dur: 30, src: 'or_node' }] },
      { id: 'or_beam', name: 'Leystrom', icon: 'advance', cooldown: 9, mana: 30, target: 'point', castRange: 8, radius: 0.5, unlockLevel: 2,
        description: 'Arkaner Strahl: 120 + 120 % Schaden auf einer Linie, markiert 4 s.',
        effects: [{ kind: 'line', length: 8, width: 0.5, base: 120, scale: 1.2, status: { id: 'mark', dur: 4, power: 0.2 } }] },
      { id: 'or_flow', name: 'Runenfluss', icon: 'bolt', cooldown: 20, mana: 10, target: 'self', castRange: 0, radius: 0, unlockLevel: 4,
        description: '8 s +6 Mana/s und +20 % Abklingzeitreduktion.',
        effects: [{ kind: 'self', add: { manaRegen: 6, cdr: 0.2 }, dur: 8, src: 'or_flow' }] },
      { id: 'or_web', name: 'Weltennetz', icon: 'crown', cooldown: 60, mana: 70, target: 'self', castRange: 0, radius: 0, unlockLevel: 6,
        description: 'Ultimate: alle Türme 10 s +25 % Schaden.',
        effects: [{ kind: 'allTowers', dmg: 0.25, dur: 10, src: 'or_web' }] },
    ],
    tree: campaignTree('or', { s1: { supportPower: 0.04 }, s1n: 'Starker Strom', s1d: '+4 % Unterstützungsstärke', s2: { abilityPower: 0.06 }, s2n: 'Arkaner Fokus', s2d: '+6 % Fähigkeitsstärke', s3: { supportRadius: 0.4, supportPower: 0.05 }, s3n: 'Weites Netz', s3d: '+0,4 Auraradius, +5 % Unterstützungsstärke', m1: { supportPower: 0.22, abilityPower: 0.1 }, m1n: 'Weltenweber', m1d: '+22 % Unterstützung, +10 % Fähigkeitsstärke' }),
  },
  mira: {
    id: 'mira', name: 'Mira', title: 'Prospektorin', role: 'Unterstützer', model: 'prospector', color: HEROES.prospector.color, accent: HEROES.prospector.accent,
    description: 'Findet Gold und Ressourcen, sprengt Gegnergruppen mit Dynamit und hat das Glück auf ihrer Seite.',
    damageType: 'physical', melee: false, campaignHero: 'prospector', unlockWave: 20,
    base: { ...BASE, maxHp: 430, damage: 13, attackSpeed: 1.0, range: 3.4, lootBonus: 0.15, pickupRadius: 1.3 },
    perLevel: { maxHp: 30, damage: 1.7, lootBonus: 0.01 },
    skills: [
      { id: 'mi_find', name: 'Schatzfund', icon: 'coins', cooldown: 30, mana: 25, target: 'self', castRange: 0, radius: 0, unlockLevel: 1,
        description: 'Findet 30 + 4 × Welle Gold und 3 Metall.',
        effects: [{ kind: 'gold', base: 30, perWave: 4, resource: 'metal', amount: 3 }] },
      { id: 'mi_luck', name: 'Glückssträhne', icon: 'star', cooldown: 22, mana: 25, target: 'self', castRange: 0, radius: 0, unlockLevel: 2,
        description: '10 s +60 % Beutebonus und +20 % kritische Trefferchance.',
        effects: [{ kind: 'self', add: { lootBonus: 0.6, critChance: 0.2 }, dur: 10, src: 'mi_luck' }] },
      { id: 'mi_tnt', name: 'Dynamit', icon: 'advance', cooldown: 10, mana: 25, target: 'point', castRange: 6, radius: 1.5, unlockLevel: 4,
        description: 'Explosion im Ziel: 70 + 120 % Schaden, betäubt 0,5 s.',
        effects: [{ kind: 'nova', at: 'target', radius: 1.5, base: 70, scale: 1.2, status: { id: 'stun', dur: 0.5 } }] },
      { id: 'mi_lode', name: 'Mutterader', icon: 'crown', cooldown: 70, mana: 60, target: 'self', castRange: 0, radius: 0, unlockLevel: 6,
        description: 'Ultimate: sofort 120 + 8 × Welle Gold und 4 Kristall.',
        effects: [{ kind: 'gold', base: 120, perWave: 8, resource: 'crystal', amount: 4 }] },
    ],
    tree: campaignTree('mi', { s1: { lootBonus: 0.04 }, s1n: 'Spürnase', s1d: '+4 % Beutebonus', s2: { pickupRadius: 0.2, damage: 1.5 }, s2n: 'Weiter Blick', s2d: '+0,2 Aufsammelradius, +1,5 Schaden', s3: { lootBonus: 0.06, critChance: 0.03 }, s3n: 'Glückspilz', s3d: '+6 % Beutebonus, +3 % Krit', m1: { lootBonus: 0.3 }, m1n: 'Goldregen', m1d: '+30 % Beutebonus' }),
  },
  ommar: {
    id: 'ommar', name: 'Ommar', title: 'Wächter der Stille', role: 'Unterstützer', model: 'silence', color: HEROES.silence.color, accent: HEROES.silence.accent,
    description: 'Bricht Bossfähigkeiten ab, deckt Getarnte auf, fesselt Gegner und lässt die ganze Welle verstummen.',
    damageType: 'sonic', melee: true, blockCount: 1, campaignHero: 'silence', unlockWave: 40,
    base: { ...BASE, maxHp: 600, hpRegen: 2, damage: 15, attackSpeed: 1.0, range: 1.05, armor: 0.12, moveSpeed: 2.6 },
    perLevel: { maxHp: 48, damage: 1.8, armor: 0.006 },
    skills: [
      { id: 'om_ban', name: 'Schweigebann', icon: 'shield', cooldown: 14, mana: 25, target: 'self', castRange: 0, radius: 6, unlockLevel: 1,
        description: 'Unterbricht Fähigkeiten im Umkreis von 6 Feldern (Bosse verlieren viel Standfestigkeit), Gegner im Umkreis von 4 Feldern verstummen 3 s.',
        effects: [{ kind: 'interrupt', radius: 6, power: 200 }, { kind: 'statusAll', radius: 4, status: { id: 'silence', dur: 3 } }] },
      { id: 'om_unveil', name: 'Enthüllung', icon: 'range', cooldown: 12, mana: 20, target: 'self', castRange: 0, radius: 5, unlockLevel: 2,
        description: 'Deckt 8 s alle Getarnten und Eingegrabenen im Umkreis von 5 Feldern auf.',
        effects: [{ kind: 'statusAll', radius: 5, status: { id: 'reveal', dur: 8 } }] },
      { id: 'om_bind', name: 'Stille Fessel', icon: 'hold', cooldown: 10, mana: 25, target: 'point', castRange: 6, radius: 1.6, unlockLevel: 4,
        description: 'Fesselt Bodengegner im Umkreis von 1,6 Feldern 2 s.',
        effects: [{ kind: 'statusAll', at: 'target', radius: 1.6, status: { id: 'root', dur: 2 } }] },
      { id: 'om_silence', name: 'Absolute Stille', icon: 'crown', cooldown: 60, mana: 70, target: 'self', castRange: 0, radius: 0, unlockLevel: 6,
        description: 'Ultimate: alle Gegner verstummen 6 s, alle Fähigkeiten werden unterbrochen.',
        effects: [{ kind: 'statusAll', radius: 0, status: { id: 'silence', dur: 6 } }, { kind: 'interrupt', radius: 999, power: 999 }] },
    ],
    tree: campaignTree('om', { s1: { armor: 0.012, damage: 1 }, s1n: 'Steinruhe', s1d: '+1,2 % Rüstung, +1 Schaden', s2: { cdr: 0.04 }, s2n: 'Askese', s2d: '+4 % Abklingzeitreduktion', s3: { abilityPower: 0.08, maxHp: 40 }, s3n: 'Tiefe Stille', s3d: '+8 % Fähigkeitsstärke, +40 LP', m1: { cdr: 0.12, armor: 0.06 }, m1n: 'Leere', m1d: '+12 % Abklingzeitreduktion, +6 % Rüstung' }),
  },
};

export const CHAMPION_ORDER: ChampionId[] = ['lyra', 'brann', 'vela', 'kaela', 'liora', 'thorgrim', 'varo', 'sena', 'mira', 'nyx', 'oran', 'ommar'];

/**
 * Freischaltung: Perk (Vela) bzw. Kampagnenheld – frei, wenn der Held in der Kampagne freigeschaltet
 * wurde ODER die Bestwelle unlockWave erreicht ist.
 */
export function championUnlocked(id: ChampionId, perks: readonly string[], bestWave = 0, campaignDone: readonly string[] = []): boolean {
  const d = CHAMPIONS[id];
  if (!d) return false;
  if (d.unlockPerk && !perks.includes(d.unlockPerk)) return false;
  if (d.campaignHero) return isHeroUnlocked(d.campaignHero, campaignDone) || bestWave >= (d.unlockWave ?? 1);
  return true;
}

export function championUnlockText(id: ChampionId): string {
  const d = CHAMPIONS[id];
  if (d.unlockPerk) return 'Freischaltung über Bestwelle 15';
  if (d.campaignHero) {
    const m = HEROES[d.campaignHero].unlock;
    return m === 'start' ? 'Von Beginn an spielbar' : `Freischaltung ab Bestwelle ${d.unlockWave ?? 1}`;
  }
  return '';
}

/**
 * 3.3: Endstufen-Knoten für alle Helden (Stufe 5–8, ab Heldenstufe 25/45/65/90). Damit reichen die Ränge des Baums
 * für alle Punkte bis Heldenstufe 99 (117 Punkte) – es bleibt aber eine Wahl, weil der Baum etwas mehr Ränge hat.
 */
export function endgameNodes(p: string): SkillNode[] {
  return [
    { id: `${p}_e_power`, name: 'Veteranenkraft', tier: 5, maxRank: 10, minLevel: 25, stats: { damage: 1.5, abilityPower: 0.015 }, description: '+1,5 Schaden und +1,5 % Fähigkeitsstärke je Rang.' },
    { id: `${p}_e_hide`, name: 'Eisenhaut', tier: 5, maxRank: 10, minLevel: 25, stats: { maxHp: 30, armor: 0.003 }, description: '+30 LP und +0,3 % Rüstung je Rang.' },
    { id: `${p}_e_mind`, name: 'Geistesquell', tier: 5, maxRank: 10, minLevel: 25, stats: { maxMana: 6, manaRegen: 0.25 }, description: '+6 Mana und +0,25 Manaregeneration je Rang.' },
    { id: `${p}_e_blade`, name: 'Klingenkunst', tier: 6, maxRank: 10, minLevel: 45, requires: [{ id: `${p}_e_power`, rank: 3 }], stats: { critChance: 0.004, critDamage: 0.02 }, description: '+0,4 % kritische Chance und +2 % kritischer Schaden je Rang.' },
    { id: `${p}_e_life`, name: 'Lebensstrom', tier: 6, maxRank: 10, minLevel: 45, requires: [{ id: `${p}_e_hide`, rank: 3 }], stats: { hpRegen: 0.5, lifesteal: 0.003 }, description: '+0,5 LP/s und +0,3 % Lebensraub je Rang.' },
    { id: `${p}_e_flow`, name: 'Zeitfluss', tier: 6, maxRank: 10, minLevel: 45, requires: [{ id: `${p}_e_mind`, rank: 3 }], stats: { cdr: 0.003, attackSpeed: 0.01 }, description: '+0,3 % Abklingzeitreduktion und +0,01 Angriffe/s je Rang.' },
    { id: `${p}_e_bond`, name: 'Turmbund', tier: 7, maxRank: 12, minLevel: 65, requires: [{ id: `${p}_e_flow`, rank: 3 }], stats: { supportPower: 0.015, supportRadius: 0.03 }, description: '+1,5 % Unterstützungsstärke und +0,03 Auraradius je Rang.' },
    { id: `${p}_e_heart`, name: 'Heldenmut', tier: 7, maxRank: 12, minLevel: 65, requires: [{ id: `${p}_e_blade`, rank: 3 }], stats: { damage: 1, maxHp: 20, abilityPower: 0.01 }, description: '+1 Schaden, +20 LP und +1 % Fähigkeitsstärke je Rang.' },
    { id: `${p}_e_legend`, name: 'Ewige Legende', tier: 8, maxRank: 5, minLevel: 90, requires: [{ id: `${p}_e_heart`, rank: 5 }], stats: { damage: 3, maxHp: 60, abilityPower: 0.03, supportPower: 0.03 }, description: 'Krönung: +3 Schaden, +60 LP, +3 % Fähigkeits- und Unterstützungsstärke je Rang.' },
  ];
}
for (const c of Object.values(CHAMPIONS)) c.tree.push(...endgameNodes(c.id));

/** Grenzen der Heldenwerte (verhindert unkontrollierte Werteexplosionen). */
export const STAT_LIMITS: Partial<Record<keyof ChampionStats, [number, number]>> = {
  armor: [0, 0.8], critChance: [0, 0.85], cdr: [0, 0.5], lifesteal: [0, 0.4], cleave: [0, 0.8], attackSpeed: [0.2, 5], moveSpeed: [0.8, 6],
};

export const STAT_NAMES: Record<keyof ChampionStats, string> = {
  maxHp: 'Lebenspunkte', hpRegen: 'LP-Regeneration', maxMana: 'Mana', manaRegen: 'Manaregeneration', damage: 'Schaden',
  attackSpeed: 'Angriffe/s', range: 'Reichweite', moveSpeed: 'Bewegungstempo', armor: 'Rüstung', critChance: 'Kritische Chance',
  critDamage: 'Kritischer Schaden', cdr: 'Abklingzeitreduktion', abilityPower: 'Fähigkeitsstärke', supportPower: 'Unterstützungsstärke',
  supportRadius: 'Auraradius', lootBonus: 'Beutebonus', pickupRadius: 'Aufsammelradius', lifesteal: 'Lebensraub', cleave: 'Flächenschlag',
};

/** Prozentuale Darstellung je Attribut. */
export const STAT_PCT = new Set<keyof ChampionStats>(['armor', 'critChance', 'cdr', 'lootBonus', 'lifesteal', 'cleave']);
export const STAT_MULT = new Set<keyof ChampionStats>(['abilityPower', 'supportPower', 'critDamage']);
