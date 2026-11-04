// Exportiert Spieldaten (Türme, Gegner, Bosse, Balance, Karten, Wellen) als JSON für das Unity-Projekt.
import { writeFileSync, mkdirSync } from 'node:fs';
import { TOWERS, FUSIONS, SELL_REFUND, FUSION_RANGE } from '../src/config/towers.ts';
import { ZERO_STATS } from '../src/config/stats.ts';
import { STATUS_RULES } from '../src/config/elements.ts';
import { SCALING, FLYER } from '../src/config/scaling.ts';
import { ENEMIES, BOSSES, WAVE_ENEMIES, ELITE, MUTATIONS } from '../src/config/enemies.ts';
import { DIFFICULTY, ECONOMY, QUICK_RUN, LIVES_DEFAULT, waveHpMul, bossHpMul, killRewardMul } from '../src/config/balance.ts';
import { BIOMES, BIOME_ORDER } from '../src/config/biomes.ts';
import { spawnsOf } from '../src/sim/WorldGenerator.ts';
import { createInitialState } from '../src/sim/setup.ts';
import { planWave } from '../src/sim/WaveDirector.ts';

const out = process.argv[2] ?? 'unity_export';
mkdirSync(out, { recursive: true });
const w = (n: string, v: unknown) => writeFileSync(`${out}/${n}.json`, JSON.stringify(v, null, 1));

w('towers', { towers: Object.values(TOWERS), fusions: FUSIONS, zeroStats: ZERO_STATS, sellRefund: SELL_REFUND, fusionRange: FUSION_RANGE });
w('enemies', { enemies: Object.values(ENEMIES), bosses: Object.values(BOSSES), waveEnemies: WAVE_ENEMIES, elite: ELITE, mutations: Object.values(MUTATIONS) });
const curve = [] as { wave: number; hpMul: number; bossHpMul: number; rewardMul: number }[];
for (let n = 1; n <= 100; n++) curve.push({ wave: n, hpMul: waveHpMul(n, true), bossHpMul: bossHpMul(n, true), rewardMul: killRewardMul(n) });
w('balance', { statusRules: STATUS_RULES, flyer: FLYER, difficulty: DIFFICULTY, economy: ECONOMY, quickRun: QUICK_RUN, lives: LIVES_DEFAULT, endlessCurve: curve });

const maps = [] as unknown[];
const seeds = ['KERNWACHT', 'ASCHE', 'FROSTMOOR', 'NEBELHAIN', 'RISS', 'GLUTSPUR'];
BIOME_ORDER.forEach((biome, i) => {
  const seedText = seeds[i % seeds.length] + (i >= seeds.length ? i : '');
  const state = createInitialState({ mode: 'quick', variant: 'classic', difficulty: 'normal', seedText, biome, size: 'normal' });
  const map = state.map;
  const waves = [] as unknown[];
  for (let n = 1; n <= 30; n++) waves.push(planWave(state, n));
  maps.push({
    id: `${biome}`, name: map.name, seed: seedText, biome, biomeName: BIOMES[biome].name,
    width: map.width, height: map.height, cells: map.cells, heights: map.heights,
    spawns: spawnsOf(map), goal: map.goal, decor: map.decor ?? [], veins: map.veins ?? [], ley: map.ley ?? [],
    terrainEvent: map.terrainEvent, palette: BIOMES[biome].palette, decorKinds: BIOMES[biome].decorKinds, bosses: state.rules.bosses, waveCount: state.rules.waves, startGold: state.rules.startGold, lives: state.rules.lives, waves,
  });
});
w('maps', { maps });
console.log('ok', maps.length, 'Karten');
