import type { EnemyId, ItemId, PotionId, ResourceId } from '../core/types.ts';

// Konfiguration des Helden-Endlosmodus. Alle Zahlen hier sind bewusst zentral gesammelt,
// damit Wellentakt, Erfahrungskurve, Beute und Fortschritt ohne Codeänderung angepasst werden können.

export const HERO_ENDLESS = {
  /** Vorbereitungszeit vor der ersten Welle (s). */
  prepTime: 15,
  /** Übergangszeit zwischen zwei Wellen (s). */
  transitionTime: 8,
  /** Startgold-Faktor gegenüber dem normalen Endlosmodus. */
  startGoldMul: 1,
  /** Erfahrungskurve: benötigte EP für Stufe L → L+1 = xpBase · L^xpExp + xpLinear · L */
  xpBase: 30,
  xpExp: 1.45,
  xpLinear: 20,
  /** Skillpunkte je Stufenaufstieg, zusätzlich ein Bonuspunkt alle bonusPointEvery Stufen. */
  pointsPerLevel: 1,
  bonusPointEvery: 5,
  /** Wiederbelebung: Basis + je Stufe (s). */
  respawnBase: 6,
  respawnPerLevel: 0.5,
  /** Gegnerschaden am Helden wächst mit der Welle. */
  enemyDamagePerWave: 0.05,
  contactRange: 0.65,
  /** Lebensdauer von Beute am Boden (s). */
  pickupLife: { xp: 40, resource: 45, fragment: 60, item: 90, potion: 60 } as Record<string, number>,
  /** Laden: Interaktionsradius (Felder) und Auffrischung des Bestands alle n Wellen. */
  shopRange: 2.2,
  stockEvery: 5,
  /** Inventarplätze (Ausrüstung) und maximale Trankstapel. */
  inventorySlots: 12,
  potionStacks: 6,
  potionGlobalCd: 0.8,
  /** Fragmente, die eine freigeschaltete Itemfamilie überzählig umwandelt, ergeben je Fragment Essenz. */
  fragmentToEssence: 2,
  /** Mini-Boss alle n Wellen ab miniBossFrom (Versatz, damit er nie mit Bosswellen zusammenfällt): 7, 12, 17, … */
  miniBossEvery: 5,
  miniBossFrom: 7,
  miniBossHpMul: 4,
  /** Abbau von Hindernissen: Dauer ≈ mittlere Wellendauer (s), Goldausbeute. */
  mineTimeDefault: 35,
  mineTimeMin: 25,
  mineTimeMax: 60,
  mineGoldBase: 20,
  mineGoldPerWave: 4,
  /** Spezialfelder: Kristalladern je Karte (Goldgräber fördert dort zusätzlich Kristall). */
  depositCount: [2, 4] as [number, number],
  /** Turmverstärkungen: Obergrenzen der Summe aller Quellen. */
  towerBuffCap: { dmg: 1.2, speed: 0.8, range: 1.5, crit: 0.4 },
  /** Startausrüstung: Grundanzahl mitnehmbarer Sammlungsitems (Perks erhöhen). */
  baseCarry: 0,
  /** Ressourcen als Wellenbelohnung. */
  waveResources(wave: number): Partial<Record<ResourceId, number>> {
    return {
      metal: 2 + Math.floor(wave / 3),
      crystal: 1 + Math.floor(wave / 5),
      essence: wave % 5 === 0 ? 2 + Math.floor(wave / 10) : 0,
      relic: wave % 10 === 0 ? 1 : 0,
    };
  },
};

export function xpToNext(level: number): number {
  const base = HERO_ENDLESS.xpBase * Math.pow(level, HERO_ENDLESS.xpExp) + HERO_ENDLESS.xpLinear * level;
  // 3.0: ab Stufe 25 flacher – Stufe 99 nach rund 360 000 statt 1 Mio. EP
  return Math.round(level <= 25 ? base : base * Math.pow(25 / level, 1.2));
}

// ------------------------------------------------------------ Beute

export const LOOT = {
  /** Erfahrung je Bedrohungspunkt des Gegners, wächst mit der Welle. */
  xpPerThreat: 4,
  xpWaveGrowth: 0.06,
  eliteXpMul: 3,
  miniBossXpMul: 10,
  bossXpMul: 25,
  /** Dropchancen (vor Glücksbonus). */
  resourceChance: 0.16,
  potionChance: 0.025,
  fragmentChance: { normal: 0.025, elite: 0.25, miniBoss: 0.6, boss: 1 },
  itemChance: { normal: 0, elite: 0.04, miniBoss: 0.25, boss: 0.5 },
  /** Ressource je Gegnertyp (Standard: Metall). */
  resourceOf(enemy: EnemyId): ResourceId {
    switch (enemy) {
      case 'flyer': case 'griffin': case 'phase': case 'shaman': case 'stalker': return 'crystal';
      case 'broodmother': case 'saboteur': return 'essence';
      default: return 'metal';
    }
  },
  resourceAmount(wave: number): number {
    return 1 + Math.floor(wave / 8);
  },
};

export interface ResourceDefinition { id: ResourceId; name: string; color: number; icon: string; description: string }

export const RESOURCES: Record<ResourceId, ResourceDefinition> = {
  metal: { id: 'metal', name: 'Metall', color: 0xb8c0cc, icon: '⛓', description: 'Häufig. Für Verstärkungen und Ausrüstung.' },
  crystal: { id: 'crystal', name: 'Kristall', color: 0x7ae0ff, icon: '◆', description: 'Von Fliegern und Magiern. Für bessere Gegenstände.' },
  essence: { id: 'essence', name: 'Essenz', color: 0xd080ff, icon: '✧', description: 'Von Elitegegnern und Bossen. Für Seltenes und Tränke.' },
  relic: { id: 'relic', name: 'Relikt', color: 0xffd060, icon: '✦', description: 'Sehr selten. Für legendäre Herstellung.' },
};
export const RESOURCE_ORDER: ResourceId[] = ['metal', 'crystal', 'essence', 'relic'];

// ------------------------------------------------------------ Wellenprofile

export interface WaveProfile {
  id: string;
  name: string;
  /** Gewichtsfaktoren für Gegnertypen. */
  weights: Partial<Record<EnemyId, number>>;
  budgetMul: number;
  capMul: number;
  extraTypes: number;
  speedMul: number;
  extraElites: number;
}

export const WAVE_PROFILES: WaveProfile[] = [
  { id: 'mixed', name: 'Gemischt', weights: {}, budgetMul: 1, capMul: 1, extraTypes: 1, speedMul: 1, extraElites: 0 },
  { id: 'swarm', name: 'Schwarm', weights: { swarm: 4, runner: 3, broodmother: 2 }, budgetMul: 1.05, capMul: 1.5, extraTypes: 0, speedMul: 1.05, extraElites: 0 },
  { id: 'armor', name: 'Panzerzug', weights: { brute: 4, shield: 3, golem: 3, siege: 3 }, budgetMul: 1.1, capMul: 0.8, extraTypes: 0, speedMul: 0.95, extraElites: 0 },
  { id: 'air', name: 'Luftangriff', weights: { flyer: 5, griffin: 5 }, budgetMul: 0.95, capMul: 1, extraTypes: 0, speedMul: 1, extraElites: 0 },
  { id: 'shadow', name: 'Schatten', weights: { stalker: 4, phase: 3, tunneler: 3, saboteur: 2 }, budgetMul: 1, capMul: 1, extraTypes: 0, speedMul: 1.05, extraElites: 0 },
  { id: 'elite', name: 'Elitetrupp', weights: { shaman: 3 }, budgetMul: 0.85, capMul: 0.8, extraTypes: 0, speedMul: 1, extraElites: 2 },
  { id: 'rush', name: 'Ansturm', weights: { runner: 3, scout: 2, griffin: 2 }, budgetMul: 0.9, capMul: 1.2, extraTypes: 0, speedMul: 1.2, extraElites: 0 },
];

/** Deterministische Profilwahl je Welle (keine reine Lineare – Profile wechseln, ab Welle 3). */
export function profileForWave(seedHash: number, wave: number): WaveProfile {
  if (wave < 3) return WAVE_PROFILES[0];
  const h = ((seedHash ^ Math.imul(wave, 0x9e3779b1)) >>> 0) % 1000;
  // Elitetrupps erst ab Welle 8
  const pool = WAVE_PROFILES.filter((p) => p.id !== 'elite' || wave >= 8).filter((p) => p.id !== 'shadow' || wave >= 9);
  return pool[h % pool.length];
}

// ------------------------------------------------------------ Dauerhafte Perks & Meilensteine

export interface PerkDefinition {
  id: string;
  name: string;
  description: string;
  /** Freischaltung über die höchste erreichte Welle. */
  wave: number;
  /** Optionale Voraussetzung (anderer Perk). */
  requires?: string;
  icon: string;
}

export const PERKS: PerkDefinition[] = [
  { id: 'magnet1', wave: 3, icon: '🧲', name: 'Sammlerhand', description: '+0,3 Aufsammelradius.' },
  { id: 'pouch1', wave: 5, icon: '🧪', name: 'Startbeutel', description: 'Start mit 2 Heiltränken.' },
  { id: 'supply1', wave: 6, icon: '⛓', name: 'Vorrat I', description: 'Start mit 6 Metall und 2 Kristall.' },
  { id: 'carry1', wave: 8, icon: '🎒', name: 'Rucksack I', description: 'Ein Item aus der Sammlung kostenlos mitnehmen.' },
  { id: 'alchemy', wave: 10, icon: '⚗', name: 'Alchemie', description: 'Fokus- und Turmelixier werden verfügbar (Beute und Laden).' },
  { id: 'smith', wave: 12, icon: '⚒', name: 'Meisterschmied', description: 'Kombinationsrezepte für legendäre Items.' },
  { id: 'trader', wave: 14, icon: '🏪', name: 'Händlergilde', description: 'Der Laden tauscht Relikte und verkauft Fertigkeitsfolianten.' },
  { id: 'vela', wave: 15, icon: '🏹', name: 'Neue Heldin: Vela', description: 'Die Sturmschützin Vela wird wählbar.' },
  { id: 'supply2', wave: 16, requires: 'supply1', icon: '⛓', name: 'Vorrat II', description: 'Start mit zusätzlich 8 Metall, 4 Kristall und 2 Essenz.' },
  { id: 'carry2', wave: 18, requires: 'carry1', icon: '🎒', name: 'Rucksack II', description: 'Ein weiteres Item mitnehmen.' },
  { id: 'mastery', wave: 20, icon: '★', name: 'Meisterschaft', description: 'Schaltet die Meisterknoten in allen Fähigkeitenbäumen frei.' },
  { id: 'wisdom', wave: 22, icon: '📖', name: 'Weisheit', description: '+10 % Erfahrung.' },
  { id: 'gold', wave: 25, icon: '🪙', name: 'Kriegskasse', description: '+80 Startgold.' },
  { id: 'carry3', wave: 30, requires: 'carry2', icon: '🎒', name: 'Rucksack III', description: 'Ein drittes Item mitnehmen.' },
  { id: 'fortune', wave: 35, icon: '🍀', name: 'Glückspilz', description: '+25 % Dropchance für Items und Fragmente.' },
];

export interface MilestoneDefinition { wave: number; name: string; reward: { items?: ItemId[]; fragments?: Partial<Record<ItemId, number>>; paragon?: number; startGold?: number }; description: string }

/** Einmalige Belohnungen beim ersten Erreichen einer Welle. Danach alle 25 Wellen ein Folgemeilenstein. */
export const MILESTONES: MilestoneDefinition[] = [
  { wave: 5, name: 'Erste Bewährung', reward: { fragments: { leather: 3 } }, description: 'Fragmente für einen Lederwams.' },
  { wave: 10, name: 'Wellenbrecher', reward: { items: ['luckyCharm'] }, description: 'Schaltet den Glücksbringer frei.' },
  { wave: 20, name: 'Unbeugsam', reward: { items: ['towerSeal'] }, description: 'Schaltet das Turmsiegel frei.' },
  { wave: 30, name: 'Veteran', reward: { items: ['starMantle'] }, description: 'Schaltet den Sternenmantel frei.' },
  { wave: 40, name: 'Legende', reward: { items: ['runeCrown'] }, description: 'Schaltet die Runenkrone frei.' },
  { wave: 50, name: 'Unsterblich', reward: {}, description: 'Titel „Unsterblich“.' },
  { wave: 100, name: 'Hundertschaft', reward: { paragon: 2, startGold: 150 }, description: 'Dauerhaft +2 Paragonpunkte und +150 Startgold.' },
  { wave: 150, name: 'Weltenwächter', reward: { paragon: 2, startGold: 150 }, description: 'Dauerhaft +2 Paragonpunkte und +150 Startgold.' },
  { wave: 200, name: 'Unbezwingbar', reward: { paragon: 3, startGold: 200 }, description: 'Dauerhaft +3 Paragonpunkte und +200 Startgold.' },
  { wave: 250, name: 'Runenmeister', reward: { paragon: 3 }, description: 'Dauerhaft +3 Paragonpunkte.' },
  { wave: 300, name: 'Kernlegende', reward: { paragon: 5, startGold: 300 }, description: 'Dauerhaft +5 Paragonpunkte und +300 Startgold.' },
];

/** 3.0: dauerhafte Paragonpunkte aus erreichten Meilensteinen (Bestwelle). */
export function milestoneParagon(bestWave: number): number {
  return MILESTONES.filter((m) => m.wave <= bestWave).reduce((a, m) => a + (m.reward.paragon ?? 0), 0);
}

/** 3.0: dauerhaftes Startgold aus erreichten Meilensteinen (Bestwelle). */
export function milestoneGold(bestWave: number): number {
  return MILESTONES.filter((m) => m.wave <= bestWave).reduce((a, m) => a + (m.reward.startGold ?? 0), 0);
}

export function perkUnlocked(id: string, bestWave: number): boolean {
  const p = PERKS.find((x) => x.id === id);
  if (!p) return false;
  if (bestWave < p.wave) return false;
  return p.requires ? perkUnlocked(p.requires, bestWave) : true;
}

export function activePerks(bestWave: number): string[] {
  return PERKS.filter((p) => perkUnlocked(p.id, bestWave)).map((p) => p.id);
}

export function carrySlots(perks: readonly string[]): number {
  return HERO_ENDLESS.baseCarry + (perks.includes('carry1') ? 1 : 0) + (perks.includes('carry2') ? 1 : 0) + (perks.includes('carry3') ? 1 : 0);
}

export const POTIONS_BY_PERK: Partial<Record<PotionId, string>> = { focus: 'alchemy', towerTonic: 'alchemy' };
