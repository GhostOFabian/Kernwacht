import type { ChampionId, ChampionStats, EquipSlot, ItemId, PotionId, Rarity, ResourceId } from '../core/types.ts';

// Gemeinsames, datengetriebenes Itemmodell für alle Ausrüstungsarten, dazu Tränke,
// Herstellungsrezepte und das Ladenangebot des Helden-Endlosmodus.

export type Cost = Partial<Record<ResourceId | 'gold', number>>;

export interface ItemDefinition {
  id: ItemId;
  name: string;
  slot: EquipSlot;
  rarity: Rarity;
  icon: string;
  description: string;
  stats: Partial<ChampionStats>;
  /** Nur für bestimmte Helden ausrüstbar. */
  champions?: ChampionId[];
  /** Ab dieser Welle als Beute möglich. */
  minWave: number;
  /** Nur durch Kombination herstellbar (keine Fragmente, kein Drop). */
  craftOnly?: boolean;
}

/** Ausrüstungsslots in fester Reihenfolge (6 Stück). Der Spezialslot nimmt auch einen Trankstapel auf. */
export const SLOTS: { slot: EquipSlot; name: string; icon: string }[] = [
  { slot: 'weapon', name: 'Waffe', icon: '⚔' },
  { slot: 'armor', name: 'Rüstung', icon: '🛡' },
  { slot: 'helm', name: 'Helm', icon: '⛑' },
  { slot: 'artifact', name: 'Artefakt', icon: '🔮' },
  { slot: 'accessory', name: 'Accessoire', icon: '💍' },
  { slot: 'special', name: 'Spezial', icon: '✦' },
];

export const RARITY: Record<Rarity, { name: string; color: string; fragments: number; craftCost: Cost; shopCost: Cost; order: number }> = {
  common: { name: 'Gewöhnlich', color: '#c8c8d0', fragments: 3, craftCost: {}, shopCost: { metal: 8 }, order: 0 },
  rare: { name: 'Selten', color: '#5ab0ff', fragments: 4, craftCost: { crystal: 2 }, shopCost: { metal: 14, crystal: 4 }, order: 1 },
  epic: { name: 'Episch', color: '#c070ff', fragments: 5, craftCost: { essence: 3 }, shopCost: { metal: 20, crystal: 8, essence: 4 }, order: 2 },
  legendary: { name: 'Legendär', color: '#ffb840', fragments: 6, craftCost: { relic: 2 }, shopCost: { metal: 30, crystal: 12, essence: 8, relic: 2 }, order: 3 },
};

const I = (d: ItemDefinition): ItemDefinition => d;

export const ITEMS: Record<ItemId, ItemDefinition> = Object.fromEntries([
  // Waffen
  I({ id: 'ironBlade', name: 'Eisenklinge', slot: 'weapon', rarity: 'common', icon: '🗡', minWave: 1, champions: ['brann', 'vela'], description: 'Solide Klinge.', stats: { damage: 6 } }),
  I({ id: 'runeStaff', name: 'Runenstab', slot: 'weapon', rarity: 'common', icon: '🪄', minWave: 1, champions: ['lyra'], description: 'Bündelt Runenkraft für Turmzauber.', stats: { abilityPower: 0.1, supportPower: 0.1 } }),
  I({ id: 'stormBow', name: 'Sturmbogen', slot: 'weapon', rarity: 'rare', icon: '🏹', minWave: 5, champions: ['vela', 'brann'], description: 'Leicht und schnell.', stats: { damage: 8, attackSpeed: 0.15 } }),
  I({ id: 'choirScepter', name: 'Chorzepter', slot: 'weapon', rarity: 'epic', icon: '🎼', minWave: 10, champions: ['lyra'], description: 'Verstärkt jede Turmverstärkung.', stats: { supportPower: 0.25, supportRadius: 0.6 } }),
  I({ id: 'dragonFang', name: 'Drachenzahn', slot: 'weapon', rarity: 'epic', icon: '🦷', minWave: 12, champions: ['brann', 'vela'], description: 'Eine Waffe aus einem Drachenzahn.', stats: { damage: 14, critChance: 0.08 } }),
  I({ id: 'worldBlade', name: 'Weltenklinge', slot: 'weapon', rarity: 'legendary', icon: '⚔', minWave: 15, craftOnly: true, champions: ['brann', 'vela'], description: 'Geschmiedet aus Drachenzahn und Eisen.', stats: { damage: 24, critChance: 0.12, critDamage: 0.3 } }),
  // Rüstungen
  I({ id: 'leather', name: 'Lederwams', slot: 'armor', rarity: 'common', icon: '🦺', minWave: 1, description: 'Leichter Schutz.', stats: { maxHp: 80, armor: 0.04 } }),
  I({ id: 'chainmail', name: 'Kettenhemd', slot: 'armor', rarity: 'rare', icon: '⛓', minWave: 4, description: 'Hält auch Bissen stand.', stats: { maxHp: 150, armor: 0.08 } }),
  I({ id: 'emberPlate', name: 'Glutpanzer', slot: 'armor', rarity: 'epic', icon: '🔥', minWave: 12, description: 'Warmer Stahl, der Wunden schließt.', stats: { maxHp: 220, armor: 0.1, hpRegen: 3 } }),
  I({ id: 'starMantle', name: 'Sternenmantel', slot: 'armor', rarity: 'legendary', icon: '🌌', minWave: 30, description: 'Gewebt aus Sternenlicht.', stats: { maxHp: 300, armor: 0.12, cdr: 0.08 } }),
  I({ id: 'citadelPlate', name: 'Zitadellenpanzer', slot: 'armor', rarity: 'legendary', icon: '🏰', minWave: 15, craftOnly: true, description: 'Unerschütterlich.', stats: { maxHp: 400, armor: 0.18, hpRegen: 5 } }),
  // Helme
  I({ id: 'ironHelm', name: 'Eisenhelm', slot: 'helm', rarity: 'common', icon: '⛑', minWave: 1, description: 'Einfacher Kopfschutz.', stats: { maxHp: 50, armor: 0.03 } }),
  I({ id: 'seerCirclet', name: 'Seherreif', slot: 'helm', rarity: 'rare', icon: '👁', minWave: 3, description: 'Klärt den Geist.', stats: { maxMana: 40, manaRegen: 1.5, cdr: 0.05 } }),
  I({ id: 'runeCrown', name: 'Runenkrone', slot: 'helm', rarity: 'epic', icon: '👑', minWave: 15, description: 'Gekrönte Zauberkraft.', stats: { abilityPower: 0.2, cdr: 0.1 } }),
  // Artefakte
  I({ id: 'emberCore', name: 'Glutkern', slot: 'artifact', rarity: 'common', icon: '🔴', minWave: 1, description: 'Pulsierende Wärme.', stats: { damage: 3, abilityPower: 0.08 } }),
  I({ id: 'manaPrism', name: 'Manaprisma', slot: 'artifact', rarity: 'rare', icon: '🔷', minWave: 4, description: 'Speichert Magie.', stats: { maxMana: 60, manaRegen: 2 } }),
  I({ id: 'riftShard', name: 'Rissscherbe', slot: 'artifact', rarity: 'epic', icon: '💠', minWave: 14, description: 'Ein Splitter des Risses.', stats: { abilityPower: 0.25, critDamage: 0.25 } }),
  I({ id: 'coreHeart', name: 'Herz des Kerns', slot: 'artifact', rarity: 'legendary', icon: '❤', minWave: 15, craftOnly: true, description: 'Die Kraft des Kerns selbst.', stats: { abilityPower: 0.35, supportPower: 0.35, cdr: 0.12 } }),
  // Accessoires
  I({ id: 'swiftBoots', name: 'Flinkstiefel', slot: 'accessory', rarity: 'common', icon: '👢', minWave: 1, description: 'Schneller auf dem Feld.', stats: { moveSpeed: 0.5 } }),
  I({ id: 'hawkEye', name: 'Falkenauge', slot: 'accessory', rarity: 'rare', icon: '🦅', minWave: 3, description: 'Sieht Schwachstellen.', stats: { range: 0.6, critChance: 0.06 } }),
  I({ id: 'vampRing', name: 'Blutring', slot: 'accessory', rarity: 'rare', icon: '💍', minWave: 6, description: 'Treffer heilen.', stats: { lifesteal: 0.08, damage: 3 } }),
  I({ id: 'berserkBand', name: 'Berserkerband', slot: 'accessory', rarity: 'epic', icon: '💢', minWave: 12, champions: ['brann'], description: 'Rasende Schläge treffen Umstehende.', stats: { attackSpeed: 0.3, cleave: 0.2 } }),
  // Spezial
  I({ id: 'magnetAmulet', name: 'Magnetamulett', slot: 'special', rarity: 'common', icon: '🧲', minWave: 1, description: 'Zieht Beute an.', stats: { pickupRadius: 0.8 } }),
  I({ id: 'luckyCharm', name: 'Glücksbringer', slot: 'special', rarity: 'rare', icon: '🍀', minWave: 6, description: 'Mehr Beute.', stats: { lootBonus: 0.3 } }),
  I({ id: 'towerSeal', name: 'Turmsiegel', slot: 'special', rarity: 'epic', icon: '🗼', minWave: 10, description: 'Weitere und stärkere Turmverstärkungen.', stats: { supportPower: 0.2, supportRadius: 1 } }),
  // 3.2 Heldenschmiede: je Held ein epischer Signaturgegenstand und eine legendäre Signaturwaffe (nur herstellbar)
  I({ id: 'lyraSpindle', name: 'Webspindel', slot: 'artifact', rarity: 'epic', icon: '🧵', minWave: 8, craftOnly: true, champions: ['lyra'], description: 'Spinnt Turmverstärkungen weiter. Signatur von Lyra.', stats: { supportPower: 0.2, supportRadius: 0.5, cdr: 0.05 } }),
  I({ id: 'lyraStaff', name: 'Stab der Turmweberin', slot: 'weapon', rarity: 'legendary', icon: '🪄', minWave: 20, craftOnly: true, champions: ['lyra'], description: 'Lyras Meisterstab: jede Verstärkung wird zum Chor. Signaturwaffe von Lyra.', stats: { supportPower: 0.4, abilityPower: 0.2, supportRadius: 0.8 } }),
  I({ id: 'brannSigil', name: 'Klingenrufer-Siegel', slot: 'accessory', rarity: 'epic', icon: '🔱', minWave: 8, craftOnly: true, champions: ['brann'], description: 'Jeder Hieb ruft weitere Klingen. Signatur von Brann.', stats: { damage: 8, cleave: 0.15, lifesteal: 0.04 } }),
  I({ id: 'brannBlade', name: 'Rufklinge', slot: 'weapon', rarity: 'legendary', icon: '⚔', minWave: 20, craftOnly: true, champions: ['brann'], description: 'Branns Schwert spaltet ganze Reihen. Signaturwaffe von Brann.', stats: { damage: 28, cleave: 0.25, critDamage: 0.3 } }),
  I({ id: 'velaQuiver', name: 'Sturmköcher', slot: 'accessory', rarity: 'epic', icon: '🎐', minWave: 8, craftOnly: true, champions: ['vela'], description: 'Pfeile mit Rückenwind. Signatur von Vela.', stats: { attackSpeed: 0.2, range: 0.4 } }),
  I({ id: 'velaBow', name: 'Velas Sturmbogen', slot: 'weapon', rarity: 'legendary', icon: '🏹', minWave: 20, craftOnly: true, champions: ['vela'], description: 'Ein Bogen, der den Wind spannt. Signaturwaffe von Vela.', stats: { damage: 20, attackSpeed: 0.3, critChance: 0.1, range: 0.5 } }),
  I({ id: 'kaelaHelm', name: 'Wächterhelm', slot: 'helm', rarity: 'epic', icon: '⛑', minWave: 8, craftOnly: true, champions: ['kaela'], description: 'Hält jeden Ansturm. Signatur von Kaela.', stats: { maxHp: 180, armor: 0.08 } }),
  I({ id: 'kaelaBlade', name: 'Wächterklinge', slot: 'weapon', rarity: 'legendary', icon: '🗡', minWave: 20, craftOnly: true, champions: ['kaela'], description: 'Schwert und Schild zugleich. Signaturwaffe von Kaela.', stats: { damage: 22, armor: 0.1, maxHp: 200 } }),
  I({ id: 'varoEmber', name: 'Aschenglut', slot: 'artifact', rarity: 'epic', icon: '🔥', minWave: 8, craftOnly: true, champions: ['varo'], description: 'Glimmt in jedem Bolzen. Signatur von Varo.', stats: { damage: 6, critChance: 0.08 } }),
  I({ id: 'varoCrossbow', name: 'Aschenjäger-Armbrust', slot: 'weapon', rarity: 'legendary', icon: '🎯', minWave: 20, craftOnly: true, champions: ['varo'], description: 'Bolzen, die nie ihr Ziel verfehlen. Signaturwaffe von Varo.', stats: { damage: 24, critChance: 0.12, critDamage: 0.4 } }),
  I({ id: 'nyxAmulet', name: 'Rissamulett', slot: 'accessory', rarity: 'epic', icon: '🌀', minWave: 8, craftOnly: true, champions: ['nyx'], description: 'Ein Schritt durch den Riss. Signatur von Nyx.', stats: { moveSpeed: 0.4, critChance: 0.06, cdr: 0.06 } }),
  I({ id: 'nyxDaggers', name: 'Rissdolche', slot: 'weapon', rarity: 'legendary', icon: '🔪', minWave: 20, craftOnly: true, champions: ['nyx'], description: 'Schneiden durch Raum und Rüstung. Signaturwaffe von Nyx.', stats: { damage: 22, attackSpeed: 0.25, critDamage: 0.35 } }),
  I({ id: 'lioraLute', name: 'Runenlaute', slot: 'artifact', rarity: 'epic', icon: '🎻', minWave: 8, craftOnly: true, champions: ['liora'], description: 'Klingt in allen Runen nach. Signatur von Liora.', stats: { supportPower: 0.2, manaRegen: 2, cdr: 0.06 } }),
  I({ id: 'lioraHarp', name: 'Bardenharfe', slot: 'weapon', rarity: 'legendary', icon: '🎼', minWave: 20, craftOnly: true, champions: ['liora'], description: 'Lioras Lied stärkt jeden Turm. Signaturwaffe von Liora.', stats: { supportPower: 0.35, abilityPower: 0.25, cdr: 0.1 } }),
  I({ id: 'thorgrimHelm', name: 'Schmiedehelm', slot: 'helm', rarity: 'epic', icon: '⛑', minWave: 8, craftOnly: true, champions: ['thorgrim'], description: 'Rußgeschwärzt und unverwüstlich. Signatur von Thorgrim.', stats: { maxHp: 150, supportPower: 0.15, armor: 0.05 } }),
  I({ id: 'thorgrimHammer', name: 'Meisterhammer', slot: 'weapon', rarity: 'legendary', icon: '🔨', minWave: 20, craftOnly: true, champions: ['thorgrim'], description: 'Formt Türme wie glühenden Stahl. Signaturwaffe von Thorgrim.', stats: { damage: 16, supportPower: 0.35, armor: 0.08 } }),
  I({ id: 'senaCrystal', name: 'Wetterkristall', slot: 'artifact', rarity: 'epic', icon: '🔷', minWave: 8, craftOnly: true, champions: ['sena'], description: 'Bündelt Sturm und Regen. Signatur von Sena.', stats: { abilityPower: 0.25, maxMana: 50 } }),
  I({ id: 'senaScepter', name: 'Sturmzepter', slot: 'weapon', rarity: 'legendary', icon: '🌩', minWave: 20, craftOnly: true, champions: ['sena'], description: 'Befiehlt dem Himmel. Signaturwaffe von Sena.', stats: { abilityPower: 0.45, cdr: 0.12, manaRegen: 3 } }),
  I({ id: 'oranFocus', name: 'Leylinienfokus', slot: 'artifact', rarity: 'epic', icon: '💠', minWave: 8, craftOnly: true, champions: ['oran'], description: 'Spürt die Kraftlinien der Welt. Signatur von Oran.', stats: { supportRadius: 0.8, supportPower: 0.15 } }),
  I({ id: 'oranStaff', name: 'Leystab', slot: 'weapon', rarity: 'legendary', icon: '🪄', minWave: 20, craftOnly: true, champions: ['oran'], description: 'Leitet Leylinien in jeden Turm. Signaturwaffe von Oran.', stats: { supportPower: 0.3, supportRadius: 1, abilityPower: 0.2 } }),
  I({ id: 'miraLamp', name: 'Prospektorenlampe', slot: 'special', rarity: 'epic', icon: '🏮', minWave: 8, craftOnly: true, champions: ['mira'], description: 'Findet jedes Vorkommen. Signatur von Mira.', stats: { lootBonus: 0.35, pickupRadius: 0.8 } }),
  I({ id: 'miraPick', name: 'Goldhacke', slot: 'weapon', rarity: 'legendary', icon: '⛏', minWave: 20, craftOnly: true, champions: ['mira'], description: 'Miras Hacke schlägt Gold aus Stein. Signaturwaffe von Mira.', stats: { damage: 14, lootBonus: 0.5, supportPower: 0.2 } }),
  I({ id: 'ommarHood', name: 'Schweigehaube', slot: 'helm', rarity: 'epic', icon: '🎭', minWave: 8, craftOnly: true, champions: ['ommar'], description: 'Stille schärft den Geist. Signatur von Ommar.', stats: { cdr: 0.1, maxMana: 40, armor: 0.05 } }),
  I({ id: 'ommarStaff', name: 'Stab der Stille', slot: 'weapon', rarity: 'legendary', icon: '🪄', minWave: 20, craftOnly: true, champions: ['ommar'], description: 'Verstummt Gegner und stärkt Verbündete. Signaturwaffe von Ommar.', stats: { abilityPower: 0.35, supportPower: 0.25, cdr: 0.1 } }),
].map((d) => [d.id, d]));

export const ITEM_ORDER: ItemId[] = Object.keys(ITEMS);

/** Itemwerte inkl. Verstärkungsstufe (je +15 %, max. +3). */
export const PLUS_STEP = 0.15;
export const MAX_PLUS = 3;
export function itemStats(id: ItemId, plus: number): Partial<ChampionStats> {
  const d = ITEMS[id];
  if (!d) return {};
  const k = 1 + PLUS_STEP * Math.min(MAX_PLUS, Math.max(0, plus));
  const out: Partial<ChampionStats> = {};
  for (const [key, v] of Object.entries(d.stats) as [keyof ChampionStats, number][]) out[key] = v * k;
  return out;
}

export function canEquipItem(id: ItemId, champion: ChampionId): boolean {
  const d = ITEMS[id];
  return !!d && (!d.champions || d.champions.includes(champion));
}

// ------------------------------------------------------------ Tränke

export interface PotionDefinition {
  id: PotionId;
  name: string;
  icon: string;
  color: number;
  description: string;
  /** Sofortwirkung (Anteil der Max-LP bzw. -Mana). */
  heal?: number;
  mana?: number;
  /** Zeitlich begrenzte Attribute. */
  buff?: Partial<ChampionStats>;
  /** Turmverstärkung im Umkreis. */
  tower?: { radius: number; dmg: number; speed: number };
  duration: number;
  cooldown: number;
  stack: number;
  rarity: Rarity;
}

export const POTIONS: Record<PotionId, PotionDefinition> = {
  heal: { id: 'heal', name: 'Heiltrank', icon: '❤', color: 0xff5060, description: 'Heilt sofort 40 % der Lebenspunkte.', heal: 0.4, duration: 0, cooldown: 6, stack: 8, rarity: 'common' },
  mana: { id: 'mana', name: 'Manatrank', icon: '💧', color: 0x5090ff, description: 'Stellt sofort 50 % Mana wieder her.', mana: 0.5, duration: 0, cooldown: 6, stack: 8, rarity: 'common' },
  power: { id: 'power', name: 'Krafttrank', icon: '💪', color: 0xff8040, description: '+30 % Schaden für 20 s.', duration: 20, cooldown: 2, stack: 5, rarity: 'rare' },
  haste: { id: 'haste', name: 'Eiltrank', icon: '⚡', color: 0xffe060, description: '+35 % Angriffstempo für 20 s.', duration: 20, cooldown: 2, stack: 5, rarity: 'rare' },
  swift: { id: 'swift', name: 'Windtrank', icon: '🌬', color: 0x80ffd0, description: '+1,2 Bewegungstempo für 20 s.', buff: { moveSpeed: 1.2 }, duration: 20, cooldown: 2, stack: 5, rarity: 'common' },
  stone: { id: 'stone', name: 'Steinhaut', icon: '🪨', color: 0xa09080, description: '+20 % Rüstung für 20 s.', buff: { armor: 0.2 }, duration: 20, cooldown: 2, stack: 5, rarity: 'rare' },
  focus: { id: 'focus', name: 'Fokustrank', icon: '🌀', color: 0xc080ff, description: '+25 % Abklingzeitreduktion für 15 s.', buff: { cdr: 0.25 }, duration: 15, cooldown: 2, stack: 5, rarity: 'epic' },
  towerTonic: { id: 'towerTonic', name: 'Turmelixier', icon: '🗼', color: 0xffc040, description: 'Türme im Umkreis von 4 Feldern: +25 % Schaden und Tempo für 15 s.', tower: { radius: 4, dmg: 0.25, speed: 0.25 }, duration: 15, cooldown: 10, stack: 4, rarity: 'epic' },
};
export const POTION_ORDER: PotionId[] = ['heal', 'mana', 'power', 'haste', 'swift', 'stone', 'focus', 'towerTonic'];

/** Prozentuale Trankwirkungen (abhängig von den Basiswerten des Helden). */
export const POTION_PCT: Partial<Record<PotionId, Partial<Record<keyof ChampionStats, number>>>> = {
  power: { damage: 0.3 },
  haste: { attackSpeed: 0.35 },
};

// ------------------------------------------------------------ Herstellung

export type RecipeKind = 'fragments' | 'combine' | 'upgrade' | 'hero';

export interface RecipeDefinition {
  id: string;
  kind: RecipeKind;
  name: string;
  /** Ergebnis-Item (bei Fragment- und Kombinationsrezepten). */
  result?: ItemId;
  /** Fragmente eines Items. */
  fragments?: { item: ItemId; count: number };
  /** Verbrauchte Items (müssen im Heldeninventar liegen). */
  items?: ItemId[];
  cost: Cost;
  minWave?: number;
  perk?: string;
  /** 3.2: nur für diesen Helden (Heldenschmiede). */
  champion?: ChampionId;
}

function fragmentRecipes(): RecipeDefinition[] {
  return ITEM_ORDER.filter((id) => !ITEMS[id].craftOnly).map((id) => ({
    id: `frag:${id}`, kind: 'fragments' as const, name: ITEMS[id].name, result: id,
    fragments: { item: id, count: RARITY[ITEMS[id].rarity].fragments }, cost: { ...RARITY[ITEMS[id].rarity].craftCost },
  }));
}

/** 3.2 Heldenschmiede: Kosten und Wellen der Signaturrezepte. */
export const HERO_RECIPE_COST: { epic: Cost; legendary: Cost } = { epic: { metal: 24, crystal: 10, essence: 4 }, legendary: { essence: 8, relic: 3 } };
export const HERO_RECIPE_WAVE = { epic: 8, legendary: 20 };

export const RECIPES: RecipeDefinition[] = [
  ...fragmentRecipes(),
  { id: 'hero:lyraSpindle', kind: 'hero', name: 'Webspindel', result: 'lyraSpindle', champion: 'lyra', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:lyraStaff', kind: 'hero', name: 'Stab der Turmweberin', result: 'lyraStaff', champion: 'lyra', items: ['lyraSpindle'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:brannSigil', kind: 'hero', name: 'Klingenrufer-Siegel', result: 'brannSigil', champion: 'brann', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:brannBlade', kind: 'hero', name: 'Rufklinge', result: 'brannBlade', champion: 'brann', items: ['brannSigil'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:velaQuiver', kind: 'hero', name: 'Sturmköcher', result: 'velaQuiver', champion: 'vela', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:velaBow', kind: 'hero', name: 'Velas Sturmbogen', result: 'velaBow', champion: 'vela', items: ['velaQuiver'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:kaelaHelm', kind: 'hero', name: 'Wächterhelm', result: 'kaelaHelm', champion: 'kaela', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:kaelaBlade', kind: 'hero', name: 'Wächterklinge', result: 'kaelaBlade', champion: 'kaela', items: ['kaelaHelm'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:varoEmber', kind: 'hero', name: 'Aschenglut', result: 'varoEmber', champion: 'varo', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:varoCrossbow', kind: 'hero', name: 'Aschenjäger-Armbrust', result: 'varoCrossbow', champion: 'varo', items: ['varoEmber'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:nyxAmulet', kind: 'hero', name: 'Rissamulett', result: 'nyxAmulet', champion: 'nyx', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:nyxDaggers', kind: 'hero', name: 'Rissdolche', result: 'nyxDaggers', champion: 'nyx', items: ['nyxAmulet'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:lioraLute', kind: 'hero', name: 'Runenlaute', result: 'lioraLute', champion: 'liora', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:lioraHarp', kind: 'hero', name: 'Bardenharfe', result: 'lioraHarp', champion: 'liora', items: ['lioraLute'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:thorgrimHelm', kind: 'hero', name: 'Schmiedehelm', result: 'thorgrimHelm', champion: 'thorgrim', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:thorgrimHammer', kind: 'hero', name: 'Meisterhammer', result: 'thorgrimHammer', champion: 'thorgrim', items: ['thorgrimHelm'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:senaCrystal', kind: 'hero', name: 'Wetterkristall', result: 'senaCrystal', champion: 'sena', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:senaScepter', kind: 'hero', name: 'Sturmzepter', result: 'senaScepter', champion: 'sena', items: ['senaCrystal'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:oranFocus', kind: 'hero', name: 'Leylinienfokus', result: 'oranFocus', champion: 'oran', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:oranStaff', kind: 'hero', name: 'Leystab', result: 'oranStaff', champion: 'oran', items: ['oranFocus'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:miraLamp', kind: 'hero', name: 'Prospektorenlampe', result: 'miraLamp', champion: 'mira', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:miraPick', kind: 'hero', name: 'Goldhacke', result: 'miraPick', champion: 'mira', items: ['miraLamp'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'hero:ommarHood', kind: 'hero', name: 'Schweigehaube', result: 'ommarHood', champion: 'ommar', cost: HERO_RECIPE_COST.epic, minWave: HERO_RECIPE_WAVE.epic },
  { id: 'hero:ommarStaff', kind: 'hero', name: 'Stab der Stille', result: 'ommarStaff', champion: 'ommar', items: ['ommarHood'], cost: HERO_RECIPE_COST.legendary, minWave: HERO_RECIPE_WAVE.legendary },
  { id: 'combine:worldBlade', kind: 'combine', name: 'Weltenklinge', result: 'worldBlade', items: ['dragonFang', 'ironBlade'], cost: { relic: 3 }, minWave: 15, perk: 'smith' },
  { id: 'combine:citadelPlate', kind: 'combine', name: 'Zitadellenpanzer', result: 'citadelPlate', items: ['emberPlate', 'chainmail'], cost: { relic: 3 }, minWave: 15, perk: 'smith' },
  { id: 'combine:coreHeart', kind: 'combine', name: 'Herz des Kerns', result: 'coreHeart', items: ['riftShard', 'manaPrism'], cost: { relic: 2, essence: 6 }, minWave: 15, perk: 'smith' },
];

/** Kosten der Verstärkung von +n auf +n+1. */
export function upgradeCost(plus: number): Cost {
  if (plus <= 0) return { metal: 6, crystal: 2 };
  if (plus === 1) return { metal: 12, crystal: 5, essence: 2 };
  return { metal: 20, crystal: 10, essence: 5, relic: 1 };
}

// ------------------------------------------------------------ Laden

export type ShopCategory = 'potions' | 'fragments' | 'materials' | 'collection' | 'special';

export interface ShopOffer {
  id: string;
  category: ShopCategory;
  name: string;
  description: string;
  cost: Cost;
  /** Bestand je Auffrischung (undefined = unbegrenzt). */
  stock?: number;
  perk?: string;
  give: { potion?: PotionId; resource?: Partial<Record<ResourceId, number>>; randomFragment?: number; skillPoint?: number };
}

export const SHOP_OFFERS: ShopOffer[] = [
  { id: 'p-heal', category: 'potions', name: 'Heiltrank', description: POTIONS.heal.description, cost: { gold: 40 }, give: { potion: 'heal' } },
  { id: 'p-mana', category: 'potions', name: 'Manatrank', description: POTIONS.mana.description, cost: { gold: 40 }, give: { potion: 'mana' } },
  { id: 'p-swift', category: 'potions', name: 'Windtrank', description: POTIONS.swift.description, cost: { metal: 3 }, give: { potion: 'swift' } },
  { id: 'p-power', category: 'potions', name: 'Krafttrank', description: POTIONS.power.description, cost: { crystal: 3 }, give: { potion: 'power' } },
  { id: 'p-haste', category: 'potions', name: 'Eiltrank', description: POTIONS.haste.description, cost: { crystal: 3 }, give: { potion: 'haste' } },
  { id: 'p-stone', category: 'potions', name: 'Steinhaut', description: POTIONS.stone.description, cost: { metal: 5 }, give: { potion: 'stone' } },
  { id: 'p-focus', category: 'potions', name: 'Fokustrank', description: POTIONS.focus.description, cost: { essence: 2 }, perk: 'alchemy', give: { potion: 'focus' } },
  { id: 'p-tower', category: 'potions', name: 'Turmelixier', description: POTIONS.towerTonic.description, cost: { essence: 3, gold: 30 }, perk: 'alchemy', give: { potion: 'towerTonic' } },
  { id: 'f-random', category: 'fragments', name: 'Fragmentbündel', description: 'Ein Fragment eines noch nicht freigeschalteten Items (zufällig).', cost: { crystal: 5 }, stock: 3, give: { randomFragment: 1 } },
  { id: 'f-random3', category: 'fragments', name: 'Großes Bündel', description: 'Drei Fragmente noch fehlender Items.', cost: { crystal: 10, essence: 3 }, stock: 1, give: { randomFragment: 3 } },
  { id: 'm-metal', category: 'materials', name: '5 Metall', description: 'Gold gegen Metall tauschen.', cost: { gold: 60 }, give: { resource: { metal: 5 } } },
  { id: 'm-crystal', category: 'materials', name: '3 Kristall', description: 'Gold gegen Kristall tauschen.', cost: { gold: 90 }, give: { resource: { crystal: 3 } } },
  { id: 'm-essence', category: 'materials', name: '1 Essenz', description: 'Metall und Kristall veredeln.', cost: { metal: 8, crystal: 3 }, stock: 5, give: { resource: { essence: 1 } } },
  { id: 'm-relic', category: 'materials', name: '1 Relikt', description: 'Essenz zu einem Relikt verdichten.', cost: { essence: 8 }, stock: 1, perk: 'trader', give: { resource: { relic: 1 } } },
  { id: 's-tome', category: 'special', name: 'Fertigkeitsfoliant', description: '+1 Fertigkeitspunkt für den Helden.', cost: { essence: 12 }, stock: 1, perk: 'trader', give: { skillPoint: 1 } },
];

export const SHOP_CATEGORIES: { id: ShopCategory; name: string }[] = [
  { id: 'potions', name: 'Tränke' },
  { id: 'collection', name: 'Ausrüstung' },
  { id: 'fragments', name: 'Fragmente' },
  { id: 'materials', name: 'Materialien' },
  { id: 'special', name: 'Spezial' },
];

/** Verkaufserlös eines Items (in Metall/Kristall, 40 % des Ladenpreises). */
export function sellValue(id: ItemId, plus: number): Cost {
  const r = RARITY[ITEMS[id]?.rarity ?? 'common'].shopCost;
  return { metal: Math.floor((r.metal ?? 0) * 0.4) + plus * 2, crystal: Math.floor((r.crystal ?? 0) * 0.4) };
}
