import type { BiomeId, BossId, DamageType, HeroId, MapSize, MapVariant, ModifierId, TowerId } from '../core/types.ts';

export interface MissionDefinition {
  id: string;
  index: number;
  name: string;
  briefing: string;
  goal: string;
  rule: string;
  seed: string;
  biome: BiomeId;
  variant: MapVariant;
  waves: number;
  startGold: number;
  lives: number;
  bosses: Record<number, BossId>;
  modifierWaves: number[];
  enemyResistBonus: Partial<Record<DamageType, number>>;
  veinMul: number;
  bannedTowers: TowerId[];
  startModifiers: ModifierId[];
  size?: MapSize;
  /** Vorgegebene Helden für diese Mission. */
  forcedHeroes?: HeroId[];
  heroMustSurvive?: HeroId;
  /** Held, der durch diese Mission freigeschaltet wird. */
  unlocksHero?: HeroId;
}

export const CAMPAIGN: MissionDefinition[] = [
  {
    id: 'm1', index: 0, name: 'Der erste Riss', seed: 'M1-ERSTER-RISS', biome: 'forest', variant: 'classic',
    briefing: 'Im verwunschenen Wald hat sich ein Riss geöffnet. Halte die ersten Kreaturen auf, bevor sie den Kern erreichen.',
    goal: 'Überstehe 8 Wellen.', rule: 'Lernmission: großzügiges Startgold. Setze Kaela mit Rissenergie auf dem Weg ein!',
    waves: 8, startGold: 320, lives: 20, bosses: {}, modifierWaves: [3, 6], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm2', unlocksHero: 'hunter', index: 1, name: 'Ascheebene', seed: 'M2-ASCHEEBENE', biome: 'volcano', variant: 'classic',
    briefing: 'Die Aschenkönigin erhebt sich aus der Glut. Ihre Diener sind an Hitze gewöhnt.',
    goal: 'Besiege die Aschenkönigin in Welle 10.', rule: 'Alle Gegner +30 % Feuerresistenz.',
    waves: 10, startGold: 280, lives: 20, bosses: { 10: 'ashQueen' }, modifierWaves: [3, 7], enemyResistBonus: { fire: 0.3 }, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm3', unlocksHero: 'weather', index: 2, name: 'Labyrinth der Ruinen', seed: 'M3-LABYRINTH', biome: 'frozen', variant: 'mazing',
    briefing: 'Zwischen den gefrorenen Ruinen gibt es keinen festen Weg. Baue ein Labyrinth – aber achte auf die Flieger.',
    goal: 'Überstehe 12 Wellen im Mazing-Modus.', rule: 'Freies Mazing, ab Welle 5 vermehrt Flieger.',
    waves: 12, startGold: 320, lives: 20, bosses: {}, modifierWaves: [3, 7, 10], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm4', unlocksHero: 'prospector', index: 3, name: 'Kristallsturm', seed: 'M4-KRISTALLSTURM', biome: 'crystal', variant: 'classic',
    briefing: 'Unter der Kristallwüste liegen reiche Adern. Der Nullhüter wacht über sie.',
    goal: 'Besiege den Nullhüter in Welle 12.', rule: 'Wenig Startgold, aber Goldadern liefern doppelt.',
    waves: 12, startGold: 200, lives: 20, bosses: { 12: 'nullWarden' }, modifierWaves: [3, 7, 10], enemyResistBonus: {}, veinMul: 2.0, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm5', unlocksHero: 'riftwalker', index: 4, name: 'Zerbrochener Himmel', seed: 'M5-HIMMELSBRUCH', biome: 'sky', variant: 'mazing',
    briefing: 'Auf den Himmelsinseln ist jeder Fehler tödlich. Der Risskoloss wartet am Ende.',
    goal: 'Besiege den Risskoloss in Welle 14.', rule: 'Nur 10 Kernenergie. Startmodifikator: Weitblick.',
    waves: 14, startGold: 340, lives: 10, bosses: { 14: 'riftColossus' }, modifierWaves: [4, 8, 11], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: ['farSight'],
  },
  {
    id: 'm6', unlocksHero: 'mystic', index: 5, name: 'Herz des Risses', seed: 'M6-HERZ-DES-RISSES', biome: 'volcano', variant: 'classic',
    briefing: 'Der Ursprung aller Risse. Zwei Wächter stellen sich dir in den Weg.',
    goal: 'Besiege den Nullhüter (Welle 8) und den Risskoloss (Welle 15).', rule: 'Gegner +15 % Arkanresistenz. Ende des ersten Akts.',
    waves: 15, startGold: 300, lives: 20, bosses: { 8: 'nullWarden', 15: 'riftColossus' }, modifierWaves: [3, 6, 10, 13], enemyResistBonus: { arcane: 0.15 }, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm7', index: 6, name: 'Schlund im Nebel', seed: 'M7-SCHLUND', biome: 'swamp', variant: 'classic', size: 'large',
    briefing: 'Im Nebelsumpf verschwinden ganze Wachtürme. Etwas Hungriges lauert in der Tiefe.',
    goal: 'Besiege den Schlund der Tiefe in Welle 14.', rule: 'Große Karte mit Weggabelung. Schattenschleicher im Nebel – baue Detektoren.',
    waves: 14, startGold: 340, lives: 20, bosses: { 14: 'maw' }, modifierWaves: [3, 7, 11], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm8', index: 7, unlocksHero: 'silence', name: 'Zwillingsmonde', seed: 'M8-ZWILLINGE', biome: 'cavern', variant: 'mazing', size: 'large',
    briefing: 'In der Leuchtgrotte wachen zwei Geschwister, deren Bund jede Wunde schließt.',
    goal: 'Besiege die Zwillingswächter in Welle 15.', rule: 'Großes Mazing-Feld. Trenne die Zwillinge, sonst heilen sie sich gegenseitig.',
    waves: 15, startGold: 460, lives: 20, bosses: { 8: 'ashQueen', 15: 'twinSol' }, modifierWaves: [3, 6, 10, 13], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
  {
    id: 'm9', index: 8, name: 'Das letzte Lied', seed: 'M9-LETZTES-LIED', biome: 'frozen', variant: 'classic', size: 'large',
    briefing: 'Liora muss das Siegel singen, bis der Riss sich schließt. Schütze die Bardin um jeden Preis.',
    goal: 'Überstehe 12 Wellen. Liora darf nicht fallen.', rule: 'Vorgegebene Helden: Liora und Kaela. Fällt Liora, ist die Mission verloren.',
    waves: 12, startGold: 360, lives: 20, bosses: { 12: 'nullWarden' }, modifierWaves: [3, 6, 9], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
    forcedHeroes: ['bard', 'blade'], heroMustSurvive: 'bard',
  },
  {
    id: 'm10', index: 9, name: 'Weltenbruch', seed: 'M10-WELTENBRUCH', biome: 'volcano', variant: 'classic', size: 'huge',
    briefing: 'Der Weltenbrecher erhebt sich aus dem Herzen des Risses. Alles, was du gelernt hast, wird nun gebraucht.',
    goal: 'Überstehe 20 Wellen und besiege den Weltenbrecher.', rule: 'Riesige Karte mit zwei Startpunkten. Finale der Kampagne.',
    waves: 20, startGold: 420, lives: 25, bosses: { 7: 'maw', 14: 'twinSol', 20: 'worldbreaker' }, modifierWaves: [3, 6, 10, 13, 17], enemyResistBonus: {}, veinMul: 1.5, bannedTowers: [], startModifiers: [],
  },
];

export const CAMPAIGN_EPILOGUE = 'Der Weltenbrecher ist gefallen, der Kern hält stand. Die Welten atmen auf – du hast die Kampagne abgeschlossen!';
