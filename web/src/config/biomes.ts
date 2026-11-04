import type { BiomeId, DamageType, TerrainEventId } from '../core/types.ts';

export interface BiomeDefinition {
  id: BiomeId;
  name: string;
  description: string;
  palette: {
    ground: number; ground2: number; path: number; cliff: number; high: number;
    obstacle: number; obstacle2: number; accent: number; sky: number; fog: number; light: number;
  };
  obstacleDensity: number;
  heightAmp: number;
  decorKinds: number[];       // 0 Baum, 1 Fels, 2 Kristall, 3 Lava, 4 Eis, 5 Ruine, 6 Pilz, 7 Wolke, 8 Weide/Schilf, 9 Leuchtpilz
  /** Umgebung rund um die schwebende Insel: Wasser, Lava, Wolkenmeer, Eiswasser, Sand, Sumpf, Abgrund. */
  sea: 'water' | 'lava' | 'clouds' | 'ice' | 'sand' | 'bog' | 'abyss';
  /** Umgebungspartikel. */
  weather: 'fireflies' | 'embers' | 'snow' | 'sparkles' | 'wind' | 'mist' | 'spores';
  /** Farbstimmung (Nachbearbeitung): Tönung und Sättigung. */
  grade: { tint: [number, number, number]; saturation: number; contrast: number };
  events: TerrainEventId[];
  musicRoot: number;          // MIDI-Grundton
  scale: number[];
}

export const BIOMES: Record<BiomeId, BiomeDefinition> = {
  forest: {
    id: 'forest', name: 'Verwunschener Wald', description: 'Moosige Lichtungen zwischen uralten Bäumen.',
    palette: { ground: 0x4f8a3c, ground2: 0x5c9a45, path: 0xb89a6a, cliff: 0x5a4632, high: 0x6aa850, obstacle: 0x2f6a34, obstacle2: 0x7a6a58, accent: 0xb6f07a, sky: 0x1d2b22, fog: 0x2a3d2e, light: 0xfff2d8 },
    obstacleDensity: 0.13, heightAmp: 1.0, decorKinds: [0, 0, 1, 6], events: ['sporeBloom', 'blizzard'], sea: 'water', weather: 'fireflies', grade: { tint: [1.0, 1.01, 0.96], saturation: 1.0, contrast: 1.05 }, musicRoot: 57, scale: [0, 2, 4, 7, 9],
  },
  volcano: {
    id: 'volcano', name: 'Vulkanödland', description: 'Erstarrte Lava, Ascheregen und glühende Spalten.',
    palette: { ground: 0x4a3a36, ground2: 0x564440, path: 0x9a7a60, cliff: 0x2a2020, high: 0x6a4a40, obstacle: 0x2c2224, obstacle2: 0xff6a20, accent: 0xff8a30, sky: 0x2a1410, fog: 0x3a2018, light: 0xffd0a0 },
    obstacleDensity: 0.11, heightAmp: 1.3, decorKinds: [1, 3, 1, 2], events: ['heatwave', 'riftSurge'], sea: 'lava', weather: 'embers', grade: { tint: [1.08, 0.98, 0.9], saturation: 1.1, contrast: 1.1 }, musicRoot: 50, scale: [0, 1, 5, 7, 8],
  },
  frozen: {
    id: 'frozen', name: 'Gefrorene Ruinen', description: 'Verschneite Trümmer einer vergessenen Festung.',
    palette: { ground: 0xc8d8e4, ground2: 0xb8cada, path: 0x8a96a8, cliff: 0x6a7a8a, high: 0xe0ecf4, obstacle: 0x9ab0c4, obstacle2: 0x7a8494, accent: 0x9ae0ff, sky: 0x18222e, fog: 0x2a3a4a, light: 0xe8f4ff },
    obstacleDensity: 0.12, heightAmp: 0.9, decorKinds: [4, 5, 5, 1], events: ['blizzard', 'crystalResonance'], sea: 'ice', weather: 'snow', grade: { tint: [0.94, 1.0, 1.08], saturation: 0.95, contrast: 1.05 }, musicRoot: 55, scale: [0, 2, 3, 7, 10],
  },
  crystal: {
    id: 'crystal', name: 'Kristallwüste', description: 'Glühende Kristalle ragen aus goldenem Sand.',
    palette: { ground: 0xd8b878, ground2: 0xcaa868, path: 0xa88858, cliff: 0x8a6a48, high: 0xe8c888, obstacle: 0x9a70d0, obstacle2: 0x60c0d0, accent: 0xe0a0ff, sky: 0x2a1e30, fog: 0x3a2a3a, light: 0xfff0d0 },
    obstacleDensity: 0.1, heightAmp: 1.1, decorKinds: [2, 2, 1, 2], events: ['crystalResonance', 'heatwave'], sea: 'sand', weather: 'sparkles', grade: { tint: [1.04, 0.99, 1.04], saturation: 1.12, contrast: 1.05 }, musicRoot: 52, scale: [0, 3, 5, 7, 10],
  },
  sky: {
    id: 'sky', name: 'Zerbrochene Himmelsinseln', description: 'Schwebende Felsinseln über einem endlosen Wolkenmeer.',
    palette: { ground: 0x7ab0a0, ground2: 0x8ac0b0, path: 0xd8d0b8, cliff: 0x5a6a78, high: 0x9ad0c0, obstacle: 0x4a5a70, obstacle2: 0xf0f4ff, accent: 0xa0f0ff, sky: 0x2a3a5a, fog: 0x4a5a7a, light: 0xffffff },
    obstacleDensity: 0.15, heightAmp: 1.2, decorKinds: [7, 1, 0, 2], events: ['riftSurge', 'crystalResonance'], sea: 'clouds', weather: 'wind', grade: { tint: [0.98, 1.0, 1.06], saturation: 1.05, contrast: 1.0 }, musicRoot: 60, scale: [0, 2, 4, 7, 11],
  },
  swamp: {
    id: 'swamp', name: 'Nebelsumpf', description: 'Knorrige Weiden, schwarzes Wasser und Irrlichter im Nebel.',
    palette: { ground: 0x4a5a3a, ground2: 0x526442, path: 0x7a6a4a, cliff: 0x3a3a2a, high: 0x5a6a46, obstacle: 0x3a4a2a, obstacle2: 0x5a5040, accent: 0x9ae0a0, sky: 0x141c18, fog: 0x2a3a30, light: 0xe8f0d8 },
    obstacleDensity: 0.13, heightAmp: 0.8, decorKinds: [8, 8, 1, 6], events: ['fog', 'sporeBloom'],
    sea: 'bog', weather: 'mist', grade: { tint: [0.96, 1.03, 0.97], saturation: 0.95, contrast: 1.08 }, musicRoot: 53, scale: [0, 2, 3, 7, 8],
  },
  cavern: {
    id: 'cavern', name: 'Leuchtgrotte', description: 'Eine unterirdische Höhle voller biolumineszenter Riesenpilze.',
    palette: { ground: 0x3a3450, ground2: 0x423c5a, path: 0x6a5a6a, cliff: 0x221e30, high: 0x4a4466, obstacle: 0x2a2440, obstacle2: 0x5a4a7a, accent: 0x60f0d0, sky: 0x0c0a14, fog: 0x1a1628, light: 0xd8d0ff },
    obstacleDensity: 0.12, heightAmp: 1.1, decorKinds: [9, 9, 1, 2], events: ['glowtide', 'crystalResonance'],
    sea: 'abyss', weather: 'spores', grade: { tint: [0.98, 0.98, 1.08], saturation: 1.15, contrast: 1.1 }, musicRoot: 49, scale: [0, 3, 5, 6, 10],
  },
};

export const BIOME_ORDER: BiomeId[] = ['forest', 'volcano', 'frozen', 'crystal', 'sky', 'swamp', 'cavern'];

export interface TerrainEventDefinition {
  id: TerrainEventId;
  name: string;
  description: string;
  every: number;
  enemySpeedMul: number;
  goldMul: number;
  damageMul: Partial<Record<DamageType, number>>;
}

export const TERRAIN_EVENTS: Record<TerrainEventId, TerrainEventDefinition> = {
  blizzard: { id: 'blizzard', name: 'Schneesturm', every: 4, enemySpeedMul: 0.85, goldMul: 1, damageMul: { fire: 0.85 },
    description: 'Gegner sind 15 % langsamer, Feuer −15 %.' },
  heatwave: { id: 'heatwave', name: 'Hitzewelle', every: 4, enemySpeedMul: 1, goldMul: 1, damageMul: { fire: 1.25, frost: 0.85 },
    description: 'Feuer +25 %, Frost −15 %.' },
  riftSurge: { id: 'riftSurge', name: 'Risswoge', every: 5, enemySpeedMul: 1.15, goldMul: 1.2, damageMul: {},
    description: 'Gegner +15 % schneller, aber +20 % Gold.' },
  sporeBloom: { id: 'sporeBloom', name: 'Sporenblüte', every: 4, enemySpeedMul: 1, goldMul: 1, damageMul: { poison: 1.3 },
    description: 'Gift +30 %.' },
  crystalResonance: { id: 'crystalResonance', name: 'Kristallresonanz', every: 4, enemySpeedMul: 1, goldMul: 1, damageMul: { arcane: 1.25, sonic: 1.25 },
    description: 'Arkan und Schall +25 %.' },
  fog: { id: 'fog', name: 'Dichter Nebel', every: 4, enemySpeedMul: 0.9, goldMul: 1.1, damageMul: {},
    description: 'Gegner −10 % Tempo, +10 % Gold, aber alle Türme −10 % Reichweite.' },
  glowtide: { id: 'glowtide', name: 'Leuchtflut', every: 4, enemySpeedMul: 1, goldMul: 1, damageMul: { poison: 1.25, arcane: 1.15 },
    description: 'Gift +25 %, Arkan +15 %.' },
};

/** Reichweitenfaktor durch Geländeereignisse. */
export const EVENT_RANGE_MUL: Partial<Record<TerrainEventId, number>> = { fog: 0.9 };
