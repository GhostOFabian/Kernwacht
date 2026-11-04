import { ECONOMY, killRewardMul, waveBudget, waveCountCap, waveHpMul } from '../config/balance.ts';
import { TERRAIN_EVENTS } from '../config/biomes.ts';
import { ENEMIES, MUTATION_IDS, WAVE_ENEMIES } from '../config/enemies.ts';
import { hashSeed, Rng } from '../core/rng.ts';
import type { BossId, DamageType, EnemyId, GameState, SpawnEntry, WaveDefinition, WaveGroup } from '../core/types.ts';
import { AFFIXES, AFFIX_ORDER, affixCount, isChallengeWave, IMMUNE_TYPES, SCALING, worldTier, type AffixId } from '../config/scaling.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { collectIncome } from './EconomySystem.ts';
import { onWaveCleared } from './HeroSystem.ts';
import { spawnEnemy } from './EnemySystem.ts';
import { rollOffer } from './RogueliteSystem.ts';
import { endlessBossFor, isModifierWave } from './setup.ts';
import { makeChallenge } from './ChallengeSystem.ts';
import { HERO_ENDLESS, profileForWave } from '../config/heroEndless.ts';

// Wellendirektor: erzeugt Wellen deterministisch aus Seed + Wellennummer (eigener Zufallsstrom
// pro Welle). Dadurch ist die Vorschau der nächsten Welle exakt und unabhängig vom Kampfverlauf.

const SPACING: Partial<Record<EnemyId, number>> = {
  runner: 0.55, swarm: 0.22, brute: 1.6, regen: 0.9, flyer: 0.8, shield: 1.1, phase: 0.9,
  saboteur: 1.0, scout: 0.7, broodmother: 2.0, golem: 2.2, stalker: 0.9, siege: 1.8, shaman: 1.4, tunneler: 1.2, griffin: 0.7,
};

const SIZE_BUDGET = { normal: 1, large: 1.05, huge: 1.1 } as const;

export function bossForWave(s: GameState, n: number): BossId | null {
  if (s.rules.endless) return endlessBossFor(s.seed.hash, n);
  return s.rules.bosses[n] ?? null;
}

export function planWave(s: GameState, n: number): WaveDefinition {
  const rng = new Rng(hashSeed(s.seed.hash, `wave-${n}-${s.mode}-${s.map.variant}`));
  const endless = s.rules.endless;
  const challenge = isChallengeWave(n, endless);
  // Challenge-Wellen haben immer einen Boss (auch außerhalb des 10er-Rhythmus)
  const boss = bossForWave(s, n) ?? (challenge ? endlessBossFor(s.seed.hash, Math.max(10, Math.round(n / 10) * 10)) : null);
  const sizeMul = SIZE_BUDGET[s.map.size ?? 'normal'];
  // Helden-Endlos: datengetriebene Wellenprofile verändern Zusammensetzung, Budget, Menge und Tempo.
  const profile = s.mode === 'heroEndless' ? profileForWave(s.seed.hash, n) : null;
  let budget = waveBudget(n) * sizeMul * (profile?.budgetMul ?? 1);
  const groups: WaveGroup[] = [];
  if (boss) {
    groups.push({ enemy: 'boss', count: 1, elite: false, boss, ...(challenge ? { challenge: true } : {}) });
    budget *= 0.35;
  }
  const pool = WAVE_ENEMIES.filter((id) => ENEMIES[id].minWave <= n)
    .filter((id) => id !== 'scout' || s.map.variant === 'mazing');
  const typeCount = Math.min(pool.length, 1 + (n >= 4 ? 1 : 0) + (n >= 10 ? 1 : 0) + (endless && n >= 20 ? 1 : 0) + (sizeMul > 1 && n >= 7 ? 1 : 0) + (profile && n >= 6 ? profile.extraTypes : 0));
  const chosen: EnemyId[] = [];
  const avail = [...pool];
  for (let k = 0; k < typeCount && avail.length > 0; k++) {
    const pick = rng.weighted(avail, (id) => {
      const fresh = n - ENEMIES[id].minWave;
      let w = fresh < 3 ? 3 : 1.4;
      if (id === 'flyer' && s.map.variant === 'mazing') w *= 1.8;
      if (id === 'runner') w *= n < 4 ? 2 : 0.8;
      if (profile) w *= profile.weights[id] ?? 1;
      return w;
    });
    chosen.push(pick);
    avail.splice(avail.indexOf(pick), 1);
  }
  const cap = Math.round(waveCountCap(n) * sizeMul * (profile?.capMul ?? 1));
  let total = 0;
  const share = budget / Math.max(1, chosen.length);
  // 3.11: Späte Endloswellen stoßen an die Mengenobergrenze – dann bestimmte bisher allein der Gegnertyp die Wellen-LP
  // (90 Aschenschwingen ≈ 16 Mio., 90 Höhlengolems ≈ 88 Mio.). Ab Welle 100 wird die Menge stattdessen nach Lebenspunkten
  // verteilt: jede Gruppe erhält denselben LP-Anteil, Splitter beim Tod zählen mit.
  if (endless && n >= SCALING.smoothFrom) {
    const lpBudget = cap * SCALING.smoothHpPerSlot;
    const eff = (id: EnemyId) => ENEMIES[id].hp + (ENEMIES[id].onDeathSpawn ? ENEMIES[ENEMIES[id].onDeathSpawn!.enemy].hp * ENEMIES[id].onDeathSpawn!.count : 0);
    for (const id of chosen) {
      const count = Math.max(1, Math.min(cap, Math.round(lpBudget / chosen.length / eff(id))));
      groups.push({ enemy: id, count, elite: false, boss: null });
    }
  } else for (const id of chosen) {
    // Neu eingeführte Gegnertypen erscheinen zunächst in kleinerer Zahl.
    const fresh = n - ENEMIES[id].minWave < 2 && chosen.length > 1 ? 0.6 : 1;
    let count = Math.max(1, Math.floor((share * fresh) / ENEMIES[id].threat));
    if (total + count > cap) count = Math.max(1, cap - total);
    total += count;
    groups.push({ enemy: id, count, elite: false, boss: null });
  }
  // Elitegegner: ab Welle 5 regelmäßig, im Endlosmodus zunehmend.
  const eliteWave = n >= 5 && (n % 4 === 1 || (endless && n >= 15 && n % 2 === 1) || (endless && n >= SCALING.pressureFrom));
  if (eliteWave) {
    const elitePool = (['brute', 'shield', 'regen', 'runner', 'phase', 'saboteur', 'golem', 'siege', 'shaman'] as EnemyId[]).filter((id) => ENEMIES[id].minWave <= n);
    const count = endless ? 1 + Math.floor(n / 15) : 1;
    // Ab Welle 8 tragen Elitegegner eine (angekündigte) Mutation.
    const mutation = n >= 8 ? rng.pick(MUTATION_IDS) : null;
    groups.push({ enemy: rng.pick(elitePool), count: Math.min(4, count), elite: true, boss: null, mutation });
  }
  if (profile) {
    const elitePool = (['brute', 'shield', 'regen', 'runner', 'golem', 'siege', 'shaman'] as EnemyId[]).filter((id) => ENEMIES[id].minWave <= n);
    if (profile.extraElites > 0 && elitePool.length) groups.push({ enemy: rng.pick(elitePool), count: profile.extraElites + Math.floor(n / 20), elite: true, boss: null, mutation: null });
    // Mini-Boss: alle n Wellen (nicht in Bosswellen) ein stark verstärkter Elitegegner
    const late = n >= SCALING.pressureFrom;
    const miniEvery = late ? SCALING.miniBossEveryLate : HERO_ENDLESS.miniBossEvery;
    const miniFrom = late ? SCALING.pressureFrom : HERO_ENDLESS.miniBossFrom;
    if (!boss && n >= HERO_ENDLESS.miniBossFrom && (n - miniFrom) % miniEvery === 0 && elitePool.length) {
      groups.push({ enemy: rng.pick(elitePool.filter((id) => id !== 'runner')) ?? elitePool[0], count: 1, elite: true, boss: null, mutation: n >= 10 ? rng.pick(MUTATION_IDS) : null, miniBoss: true });
    }
  }
  const ev = s.map.terrainEvent;
  const event = ev && n % TERRAIN_EVENTS[ev].every === 0 ? ev : null;
  // 3.0: Weltstufe und Affixe (eigener Zufallsstrom, damit frühere Wellen unverändert bleiben)
  const tier = worldTier(n, endless);
  const affixes: AffixId[] = [];
  let immune: DamageType | undefined;
  const nAff = affixCount(n, endless);
  if (nAff > 0) {
    const ar = new Rng(hashSeed(s.seed.hash, `affix-${n}`));
    const pool = [...AFFIX_ORDER];
    for (let k = 0; k < nAff && pool.length; k++) affixes.push(pool.splice(Math.floor(ar.next() * pool.length) % pool.length, 1)[0]);
    if (affixes.includes('immune')) immune = IMMUNE_TYPES[Math.floor(ar.next() * IMMUNE_TYPES.length) % IMMUNE_TYPES.length];
  }
  return {
    index: n,
    groups,
    hpMul: waveHpMul(n, endless) * (1 + SCALING.tierHp * tier),
    speedMul: (event ? TERRAIN_EVENTS[event].enemySpeedMul : 1) * (profile?.speedMul ?? 1) * (1 + Math.min(SCALING.tierSpeedMax, SCALING.tierSpeed * tier)) * (affixes.includes('swift') ? 1.2 : 1),
    rewardMul: killRewardMul(n) * (event ? TERRAIN_EVENTS[event].goldMul : 1) * (1 + SCALING.tierReward * tier),
    event,
    isBoss: boss !== null,
    ...(tier > 0 ? { tier } : {}),
    ...(affixes.length ? { affixes } : {}),
    ...(immune ? { immune } : {}),
  };
}

function buildQueue(wave: WaveDefinition): SpawnEntry[] {
  const q: SpawnEntry[] = [];
  let t = 1.0;
  for (const g of wave.groups) {
    const gap = g.boss ? 3 : SPACING[g.enemy] ?? 0.8;
    for (let i = 0; i < g.count; i++) {
      q.push({ at: t, enemy: g.enemy, elite: g.elite, boss: g.boss, mutation: g.mutation ?? null, ...(g.miniBoss ? { miniBoss: true } : {}), ...(g.challenge ? { challenge: true } : {}) });
      t += g.elite ? 2 : gap;
    }
    t += 2.2;
  }
  return q;
}

export function startNextWave(sim: GameSimulation): void {
  const s = sim.s;
  s.wave++;
  s.phase = 'combat';
  sim.c.statsDirty = true;
  s.waveTime = 0;
  for (const t of s.towers) t.builtInPhase = false;
  s.currentWave = planWave(s, s.wave);
  s.spawnQueue = buildQueue(s.currentWave);
  sim.c.statsDirty = true; // Geländeereignisse können Turmwerte verändern
  sim.emit({ k: 'waveStart', wave: s.wave, boss: s.currentWave.isBoss });
  const tier = s.currentWave.tier ?? 0;
  if (tier > 0 && worldTier(s.wave - 1, s.rules.endless) < tier) {
    sim.emit({ k: 'message', text: `🌐 Weltstufe ${tier} erreicht: Gegner deutlich stärker – Kopfgeld +${Math.round(SCALING.tierReward * tier * 100)} %`, severity: 'danger' });
  }
  if (s.currentWave.affixes?.length) {
    sim.emit({ k: 'message', text: `Wellen-Affixe: ${s.currentWave.affixes.map((a) => `${AFFIXES[a].icon} ${AFFIXES[a].name}`).join(', ')}`, severity: 'warn' });
  }
  if (s.currentWave.event) {
    const ev = TERRAIN_EVENTS[s.currentWave.event];
    sim.emit({ k: 'message', text: `Geländeereignis: ${ev.name} – ${ev.description}`, severity: 'info' });
  }
}

export function updateSpawns(sim: GameSimulation, _dt: number): void {
  const s = sim.s;
  while (s.spawnQueue.length > 0 && s.spawnQueue[0].at <= s.waveTime) {
    const e = s.spawnQueue.shift() as SpawnEntry;
    const en = spawnEnemy(sim, e.enemy, e.elite, e.boss, e.mutation ?? null);
    if (e.miniBoss) {
      en.miniBoss = true;
      en.maxHp *= HERO_ENDLESS.miniBossHpMul;
      en.hp = en.maxHp;
      en.reward *= 4;
      sim.emit({ k: 'message', text: 'Ein Mini-Boss nähert sich!', severity: 'warn' });
    }
    if (e.challenge) makeChallenge(sim, en);
  }
}

export function checkWaveEnd(sim: GameSimulation): void {
  const s = sim.s;
  if (s.phase !== 'combat') return;
  if (s.spawnQueue.length > 0 || s.enemies.some((e) => e.alive)) return;
  completeWave(sim);
}

/**
 * Helden-Endlos: Die nächste Welle vorzeitig rufen, sobald die aktuelle vollständig erschienen ist.
 * Die laufende Welle gilt als überstanden (Belohnung sofort), übrige Gegner bleiben auf dem Feld.
 * Wer früh ruft, erhält einen kleinen Goldbonus. Rückgabe: Grund, falls nicht möglich.
 */
export function earlyCallState(s: GameState): string | null {
  if (s.mode !== 'heroEndless') return 'Nur im Helden-Endlosmodus';
  if (s.phase === 'build') return null;
  if (s.phase !== 'combat') return 'Erst Modifikator wählen';
  if (s.spawnQueue.length > 0) return `Die aktuelle Welle erscheint noch (${s.spawnQueue.length} Gegner ausstehend)`;
  if (s.enemies.some((e) => e.alive && e.boss)) return 'Nicht während eines Bosskampfs';
  return null;
}

export const EARLY_CALL_GOLD = 4;

export function callNextWave(sim: GameSimulation): string | null {
  const s = sim.s;
  const reason = earlyCallState(s);
  if (reason) return reason;
  if (s.phase === 'combat') {
    const alive = s.enemies.filter((e) => e.alive).length;
    const bonus = EARLY_CALL_GOLD * Math.max(1, alive);
    s.gold += bonus;
    s.stats.goldEarned += bonus;
    completeWave(sim, true);
    if ((s.phase as string) !== 'build') return null; // Modifikatorauswahl zuerst
  }
  startNextWave(sim);
  return null;
}

export function completeWave(sim: GameSimulation, early = false): void {
  const s = sim.s;
  const mods = sim.c.mods;
  const wasBoss = s.currentWave?.isBoss ?? false;
  const bonus = Math.round((ECONOMY.waveBonusBase + ECONOMY.waveBonusPerWave * s.wave + (wasBoss ? ECONOMY.bossWaveBonus : 0)) * mods.waveGoldMul);
  const income = collectIncome(sim);
  onWaveCleared(sim);
  let interest = 0;
  if (mods.interest) interest = Math.min(ECONOMY.interestCap, Math.floor(s.gold * ECONOMY.interestRate));
  const total = bonus + income + interest;
  s.gold += total;
  s.stats.goldEarned += total;
  s.stats.goldPerWave.push(total);
  s.stats.wavesCleared++;
  if (!early) {
    // Bei vorzeitigem Ruf laufen Geschosse und Zonen gegen die verbliebenen Gegner weiter.
    // Scharfe Minen bleiben liegen (Minenfeld über das Wellenende hinaus)
    s.zones = s.zones.filter((z) => z.kind === 'mine' && z.armed <= 0 && z.t > 0);
    s.telegraphs = [];
    s.projectiles = [];
  }
  s.currentWave = null;
  sim.c.statsDirty = true;
  if (wasBoss && s.rules.endless) {
    // 3.4: Bosswelle geschafft → ein zusätzlicher Turmplatz
    s.bossSlots = (s.bossSlots ?? 0) + 1;
    sim.emit({ k: 'message', text: 'Bosswelle geschafft: +1 Turmplatz', severity: 'info' });
  }
  sim.emit({ k: 'waveClear', wave: s.wave, bonus: total });
  if (!s.rules.endless && s.wave >= s.rules.waves) {
    s.phase = 'victory';
    sim.emit({ k: 'victory' });
    return;
  }
  if (isModifierWave(s.rules, s.wave)) {
    s.modOffer = rollOffer(sim);
    s.phase = s.modOffer.length > 0 ? 'choice' : 'build';
  } else {
    s.phase = 'build';
  }
}

/** Vorschau für die HUD: Zusammensetzung der nächsten Welle. */
export function previewNextWave(s: GameState): WaveDefinition | null {
  if (!s.rules.endless && s.wave >= s.rules.waves) return null;
  return planWave(s, s.wave + 1);
}
