import { DIFFICULTY, ECONOMY, ENDLESS, QUICK_RUN } from '../config/balance.ts';
import { BOSS_ORDER, LATE_BOSS_ORDER } from '../config/enemies.ts';
import { HERO_RULES, HEROES } from '../config/heroes.ts';
import { CAMPAIGN } from '../config/campaign.ts';
import { MODIFIER_IDS } from '../config/modifiers.ts';
import { hashSeed, Rng } from '../core/rng.ts';
import type {
  BiomeId, BossId, Difficulty, GameMode, GameState, HeroId, HeroInstance, MapDefinition, MapSize, MapVariant, MissionRules, RunStats, WorldSeed,
} from '../core/types.ts';
import { generateWorld, makeSeed } from './WorldGenerator.ts';
import { initHeroEndless, type HeroEndlessSetup } from './HeroEndlessSystem.ts';

export const STATE_VERSION = 2;

export interface RunSetup {
  mode: GameMode;
  seedText: string;
  variant: MapVariant;
  biome: BiomeId | 'random';
  difficulty: Difficulty;
  missionId?: string | null;
  customMap?: MapDefinition | null;
  size?: MapSize;
  heroes?: HeroId[];
  /** Nur im Modus 'heroEndless': Held, Startausrüstung und Fortschritts-Schnappschuss. */
  heroEndless?: HeroEndlessSetup;
}

export function emptyStats(): RunStats {
  return {
    kills: 0, leaks: 0, livesLost: 0, goldEarned: 0, goldSpent: 0, goldPerWave: [],
    damageByTower: {}, builtByTower: {}, bossesKilled: [], bossesFaced: [], reactions: {},
    wavesCleared: 0, maxNetworkSize: 0, duration: 0,
    heroDeploys: 0, heroKills: 0, heroDowns: 0, fusions: 0, activesUsed: 0, mutationsKilled: 0,
  };
}

/** Leitet die Regeln einer Partie aus Modus, Mission bzw. Tages-Seed ab. */
export function createRules(setup: RunSetup, seed: WorldSeed): MissionRules {
  const diff = DIFFICULTY[setup.difficulty];
  const base: MissionRules = {
    waves: QUICK_RUN.waves,
    startGold: Math.round(ECONOMY.startGold[setup.variant] * diff.goldMul),
    lives: diff.lives,
    bosses: {},
    enemyResistBonus: {},
    veinMul: ECONOMY.veinMul,
    modifierWaves: [...QUICK_RUN.modifierWaves],
    bannedTowers: [],
    startModifiers: [],
    endless: false,
  };
  const rng = new Rng(hashSeed(seed.hash, 'rules'));
  const bossOrder = rng.shuffle([...BOSS_ORDER]);
  const lateOrder = rng.shuffle([...LATE_BOSS_ORDER]);
  const big = (setup.size ?? 'normal') !== 'normal';
  // Größere Karten: etwas mehr Startgold, da die Wege länger und die Bauflächen weiter verteilt sind.
  const sizeGold = setup.size === 'huge' ? 1.35 : setup.size === 'large' ? 1.15 : 1;
  base.startGold = Math.round(base.startGold * sizeGold);
  // Mehrere Risse bedeuten mehr Druck: größere Karten erhalten zusätzlich mehr Kernenergie.
  base.lives = Math.round(base.lives * (setup.size === 'huge' ? 1.3 : setup.size === 'large' ? 1.15 : 1));
  switch (setup.mode) {
    case 'campaign': {
      const m = CAMPAIGN.find((x) => x.id === setup.missionId);
      if (!m) throw new Error(`Unbekannte Mission: ${String(setup.missionId)}`);
      return {
        ...base,
        waves: m.waves,
        startGold: Math.round(m.startGold * diff.goldMul),
        lives: Math.round(m.lives * (diff.lives / 20)),
        bosses: { ...m.bosses },
        enemyResistBonus: { ...m.enemyResistBonus },
        veinMul: m.veinMul,
        modifierWaves: [...m.modifierWaves],
        bannedTowers: [...m.bannedTowers],
        startModifiers: [...m.startModifiers],
        forcedHeroes: m.forcedHeroes ? [...m.forcedHeroes] : undefined,
        heroMustSurvive: m.heroMustSurvive,
      };
    }
    case 'heroEndless':
    case 'endless': {
      const bosses: Record<number, BossId> = {};
      return { ...base, waves: 0, endless: true, bosses, modifierWaves: [] };
    }
    case 'daily': {
      const startMod = rng.pick(MODIFIER_IDS.filter((m) => m !== 'glassCannon' && m !== 'bulwark'));
      return {
        ...base,
        waves: 15,
        bosses: { 8: bossOrder[0], 15: lateOrder[0] },
        modifierWaves: [3, 6, 10, 13],
        startModifiers: [startMod],
      };
    }
    default:
      // Große Karten: der Endboss stammt aus den späten Bossen (Schlund, Zwillinge, Weltenbrecher).
      return { ...base, bosses: { [QUICK_RUN.bosses[0]]: bossOrder[0], [QUICK_RUN.bosses[1]]: big || rng.chance(0.5) ? lateOrder[0] : bossOrder[1] } };
  }
}

/** Boss für Endloswellen: zyklisch in seedabhängiger Reihenfolge. */
export function endlessBossFor(seedHash: number, wave: number): BossId | null {
  if (wave % ENDLESS.bossEvery !== 0) return null;
  const r = new Rng(hashSeed(seedHash, 'endlessBoss'));
  const order = [...r.shuffle([...BOSS_ORDER]), ...r.shuffle([...LATE_BOSS_ORDER])];
  return order[(wave / ENDLESS.bossEvery - 1) % order.length];
}

export function isModifierWave(rules: MissionRules, wave: number): boolean {
  if (rules.endless) return wave > 0 && wave % ENDLESS.modifierEvery === 0;
  return rules.modifierWaves.includes(wave);
}

export function resolveSetup(setup: RunSetup): { seed: WorldSeed; variant: MapVariant; biome: BiomeId | 'random'; size: MapSize } {
  if (setup.mode === 'campaign') {
    const m = CAMPAIGN.find((x) => x.id === setup.missionId);
    if (!m) throw new Error(`Unbekannte Mission: ${String(setup.missionId)}`);
    return { seed: makeSeed(m.seed), variant: m.variant, biome: m.biome, size: m.size ?? 'normal' };
  }
  const seed = makeSeed(setup.seedText);
  if (setup.mode === 'daily') {
    const r = new Rng(hashSeed(seed.hash, 'dailyVariant'));
    const variant: MapVariant = r.chance(0.5) ? 'classic' : 'mazing';
    return { seed, variant, biome: 'random', size: r.chance(0.5) ? 'large' : 'normal' };
  }
  return { seed, variant: setup.variant, biome: setup.biome, size: setup.size ?? 'normal' };
}

export function makeHero(id: HeroId, uid: number): HeroInstance {
  const def = HEROES[id];
  return {
    id, uid, state: 'ready', mode: 'hold', x: 0, y: 0, ax: 0, ay: 0, hp: def.base.maxHp, level: 1, xp: 0, perks: [], pendingPerk: false,
    activeLeft: 0, cooldown: 0, abilityCd: [0, 0], ult: 0, attackCd: 0, target: 0, buffT: 0, buffKind: '', kills: 0, damageDealt: 0,
    treasureWave: -1, facing: 0, anim: 0, blocking: 0,
  };
}

export function createInitialState(setup: RunSetup): GameState {
  const { seed, variant, biome, size } = resolveSetup(setup);
  const map = setup.customMap ? structuredCloneMap(setup.customMap) : generateWorld({ seed, variant, biome, size });
  const rules = createRules({ ...setup, variant: map.variant, size: map.size ?? size }, seed);
  const heroIds = setup.mode === 'heroEndless' ? [] : (rules.forcedHeroes ?? setup.heroes ?? []).filter((h, i, a) => h in HEROES && a.indexOf(h) === i).slice(0, HERO_RULES.loadoutSize);
  const state: GameState = {
    version: STATE_VERSION,
    seed,
    mode: setup.mode,
    difficulty: setup.difficulty,
    missionId: setup.missionId ?? null,
    rules,
    map,
    tick: 0,
    time: 0,
    gold: rules.startGold,
    lives: rules.lives,
    maxLives: rules.lives,
    wave: 0,
    phase: 'build',
    waveTime: 0,
    currentWave: null,
    spawnQueue: [],
    towers: [],
    enemies: [],
    projectiles: [],
    zones: [],
    telegraphs: [],
    modifiers: [...rules.startModifiers],
    modOffer: null,
    rng: { combat: hashSeed(seed.hash, 'combat'), mods: hashSeed(seed.hash, 'mods') },
    nextUid: 1,
    boss: null,
    network: { links: [], groups: [], version: 0 },
    stats: emptyStats(),
    netZapTimer: 0,
    speed: 1,
    paused: false,
    score: 0,
    heroes: heroIds.map((h, i) => makeHero(h, 900000 + i)),
    riftEnergy: HERO_RULES.startEnergy,
    buffs: [],
    he: null,
  };
  if (setup.mode === 'heroEndless') {
    initHeroEndless(state, setup.heroEndless ?? { champion: 'lyra', startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {} }, runId: `${seed.text}-${Date.now().toString(36)}` });
  }
  return state;
}

function structuredCloneMap(m: MapDefinition): MapDefinition {
  return JSON.parse(JSON.stringify(m)) as MapDefinition;
}
