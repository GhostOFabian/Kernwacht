import { ACHIEVEMENTS } from '../config/achievements.ts';
import { CAMPAIGN } from '../config/campaign.ts';
import { HERO_ORDER, isHeroUnlocked } from '../config/heroes.ts';
import type { GameState, HeroId, ItemId, SaveData } from '../core/types.ts';
import { activePerks, MILESTONES, PERKS, xpToNext } from '../config/heroEndless.ts';
import { paragonScore } from '../config/paragon.ts';
import { ITEMS } from '../config/items.ts';
import { campaignStars, computeScore } from '../sim/Score.ts';

// Überträgt das Ergebnis einer beendeten Partie in den lokalen Fortschritt (rein, testbar).

export interface RunRecord {
  score: number;
  stars: number;
  newBestScore: boolean;
  newBestEndless: boolean;
  unlockedMission: string | null;
  campaignFinished: boolean;
  newAchievements: string[];
  unlockedHeroes: HeroId[];
  hero: HeroRunCommit | null;
}

export interface HeroRunCommit {
  /** false, wenn dieser Lauf bereits verbucht war (idempotent). */
  committed: boolean;
  wave: number;
  newBestWave: boolean;
  newItems: ItemId[];
  newPerks: string[];
  milestones: number[];
}

/**
 * Überträgt einen Helden-Endloslauf in den dauerhaften Fortschritt. Idempotent über die runId:
 * mehrfacher Aufruf (z. B. Fortsetzen + Ende) verbucht Items und Fragmente nur einmal.
 */
/** Gesamte EP bis zu einer Stufe (Vergleichsmaß für Fortschritt). */
function totalXp(level: number, xp: number): number {
  let n = xp;
  for (let l = 1; l < level; l++) n += xpToNext(l);
  return n;
}

/**
 * 2.7: Heldenstufe, EP und Skillbaum des Helden sowie das gemeinsame Paragon dauerhaft sichern.
 * Absolute Werte (wiederholbar ohne Doppelgutschrift); ein älterer Stand überschreibt nie einen neueren.
 */
export function commitHeroProgress(save: SaveData, s: GameState): void {
  const he = s.he;
  if (!he || s.mode !== 'heroEndless') return;
  const m = save.progress.heroMeta;
  const c = he.champion;
  // Gemeinsame Stufe (alle Helden): ein älterer Stand überschreibt nie einen neueren
  if (totalXp(c.level, c.xp) >= totalXp(m.heroLevel.level, m.heroLevel.xp)) m.heroLevel = { level: c.level, xp: Math.floor(c.xp) };
  // Skillbaum je Held
  m.champions[c.id] = { level: m.heroLevel.level, xp: m.heroLevel.xp, tree: { ...c.tree } };
  // Turm-Meisterschaft: absolute Abschusszahlen, nur nach oben
  for (const [k, v] of Object.entries(he.mastery ?? {})) {
    if ((v ?? 0) > (m.towerMastery[k as keyof typeof m.towerMastery] ?? 0)) m.towerMastery[k as keyof typeof m.towerMastery] = v;
  }
  const p = he.paragon;
  if (p && paragonScore(p) >= paragonScore(m.paragon)) {
    m.paragon = { level: p.level, xp: Math.floor(p.xp), points: { ...p.points } };
  } else if (p && p.level === m.paragon.level) {
    m.paragon.points = { ...p.points };
  }
}

export function commitHeroRun(save: SaveData, s: GameState): HeroRunCommit {
  const m = save.progress.heroMeta;
  commitHeroProgress(save, s);
  const he = s.he;
  const wave = s.stats.wavesCleared;
  const empty: HeroRunCommit = { committed: false, wave, newBestWave: false, newItems: [], newPerks: [], milestones: [] };
  if (!he || s.mode !== 'heroEndless') return empty;
  if (m.committedRuns.includes(he.runId)) return empty;
  m.committedRuns.push(he.runId);
  m.committedRuns = m.committedRuns.slice(-50);
  m.runs++;
  m.lastChampion = he.champion.id;
  // Vergleichsbasis ist der Stand bei Partiebeginn (die Bestwelle wird während des Laufs bereits mitgeschrieben).
  const perksBefore = activePerks(Math.min(m.bestWave, he.bestWaveAtStart));
  const newItems: ItemId[] = [];
  const unlock = (id: ItemId): void => {
    if (!ITEMS[id] || m.unlockedItems.includes(id)) return;
    m.unlockedItems.push(id);
    newItems.push(id);
    delete m.fragments[id];
  };
  // Fragmente: Veränderung gegenüber dem Start verbuchen (nie negativ)
  for (const [id, d] of Object.entries(he.fragmentDelta)) {
    if (!ITEMS[id] || !d) continue;
    const v = Math.max(0, (m.fragments[id] ?? 0) + d);
    if (v > 0) m.fragments[id] = v;
    else delete m.fragments[id];
  }
  for (const id of he.runUnlocks) unlock(id);
  const newBestWave = wave > he.bestWaveAtStart && wave >= m.bestWave;
  m.bestWave = Math.max(m.bestWave, wave);
  const milestones: number[] = [];
  for (const ms of MILESTONES) {
    if (m.bestWave < ms.wave || m.claimedMilestones.includes(ms.wave)) continue;
    m.claimedMilestones.push(ms.wave);
    milestones.push(ms.wave);
    for (const id of ms.reward.items ?? []) unlock(id);
    for (const [id, n] of Object.entries(ms.reward.fragments ?? {})) {
      if (!ITEMS[id] || !n || m.unlockedItems.includes(id)) continue;
      m.fragments[id] = (m.fragments[id] ?? 0) + n;
    }
  }
  // Startausrüstung bereinigen (nur freigeschaltete Items)
  m.startKit = m.startKit.filter((id) => m.unlockedItems.includes(id));
  const newPerks = activePerks(m.bestWave).filter((id) => !perksBefore.includes(id));
  return { committed: true, wave, newBestWave, newItems, newPerks, milestones };
}

export function perkName(id: string): string {
  return PERKS.find((p) => p.id === id)?.name ?? id;
}

export function todayKey(d = new Date()): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

export function recordRun(save: SaveData, s: GameState, now = new Date()): RunRecord {
  const p = save.progress;
  const heroesBefore = HERO_ORDER.filter((id) => isHeroUnlocked(id, p.campaignCompleted));
  const score = computeScore(s);
  s.score = score;
  const victory = s.phase === 'victory';
  p.totalRuns++;
  if (victory) p.totalVictories++;
  p.totalKills += s.stats.kills;
  p.totalBossKills += s.stats.bossesKilled.length;
  for (const b of s.stats.bossesKilled) if (!p.bossesDefeated.includes(b)) p.bossesDefeated.push(b);
  const prevBest = p.bestScores.filter((b) => b.mode === s.mode).reduce((m, b) => Math.max(m, b.score), 0);
  p.bestScores.push({ mode: s.mode, score, seed: s.seed.text, date: now.toISOString().slice(0, 10), waves: s.stats.wavesCleared, victory });
  p.bestScores.sort((a, b) => b.score - a.score);
  p.bestScores = p.bestScores.slice(0, 50);
  let newBestEndless = false;
  if (s.mode === 'endless') {
    const key = s.map.variant;
    if (s.stats.wavesCleared > (p.bestEndlessWave[key] ?? 0)) {
      p.bestEndlessWave[key] = s.stats.wavesCleared;
      newBestEndless = true;
    }
  }
  if (s.mode === 'daily') {
    const k = todayKey(now);
    p.dailyCompleted[k] = Math.max(p.dailyCompleted[k] ?? 0, score);
  }
  let stars = 0;
  let unlockedMission: string | null = null;
  let campaignFinished = false;
  if (s.mode === 'campaign' && s.missionId && victory) {
    stars = campaignStars(s);
    const m = CAMPAIGN.find((x) => x.id === s.missionId);
    if (m) {
      if (!p.campaignCompleted.includes(m.id)) p.campaignCompleted.push(m.id);
      p.campaignStars[m.id] = Math.max(p.campaignStars[m.id] ?? 0, stars);
      if (m.index + 2 > p.campaignUnlocked && m.index + 1 < CAMPAIGN.length) {
        p.campaignUnlocked = m.index + 2;
        unlockedMission = CAMPAIGN[m.index + 1].id;
      }
      campaignFinished = m.index === CAMPAIGN.length - 1;
    }
  }
  const newAchievements: string[] = [];
  for (const a of ACHIEVEMENTS) {
    if (p.achievements.includes(a.id)) continue;
    try {
      if (a.check(s, p)) {
        p.achievements.push(a.id);
        newAchievements.push(a.id);
      }
    } catch {
      // Ein fehlerhafter Erfolgscheck darf das Speichern nicht verhindern.
    }
  }
  const hero = s.mode === 'heroEndless' ? commitHeroRun(save, s) : null;
  const unlockedHeroes = HERO_ORDER.filter((id) => isHeroUnlocked(id, p.campaignCompleted) && !heroesBefore.includes(id));
  return { score, stars, newBestScore: score > prevBest, newBestEndless, unlockedMission, campaignFinished, newAchievements, unlockedHeroes, hero };
}
