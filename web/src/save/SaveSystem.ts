import { CAMPAIGN } from '../config/campaign.ts';
import { MODIFIER_IDS } from '../config/modifiers.ts';
import { TOWERS } from '../config/towers.ts';
import { SKINS } from '../config/achievements.ts';
import { BOSSES } from '../config/enemies.ts';
import { HEROES } from '../config/heroes.ts';
import { ITEMS } from '../config/items.ts';
import { sanitizePassive } from '../config/passiveTree.ts';
import { sanitizeKeys } from '../config/keys.ts';
import { sanitizeMastery } from '../config/mastery.ts';
import { emptyParagon, PARAGON, paragonPrePoints, sanitizeParagon } from '../config/paragon.ts';
import { milestoneParagon, xpToNext } from '../config/heroEndless.ts';
import { CHAMPIONS } from '../config/champions.ts';
import type { BossId, ChampionId, GameMode, HeroMetaProgress, GameState, HeroId, PlayerProgress, PlayerSettings, SaveData } from '../core/types.ts';
import { STATE_VERSION } from '../sim/setup.ts';
import { validateMap } from '../sim/WorldGenerator.ts';

// Versionierte, validierte lokale Speicherung.
// Beschädigte Daten werden NICHT stillschweigend gelöscht: der Rohinhalt wird gesichert und
// der Spieler erhält eine verständliche Meldung.

export interface KeyValueStore {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export const SAVE_KEY = 'kernwacht.save';
/** Speicherschlüssel des früheren Arbeitstitels – wird beim ersten Laden übernommen, nie gelöscht. */
export const LEGACY_SAVE_KEYS = ['riftbound.save'];
export const SAVE_VERSION = 3;

export function defaultSettings(): PlayerSettings {
  return {
    masterVolume: 0.8, musicVolume: 0.45, sfxVolume: 0.8, muted: false, reducedMotion: false, uiScale: 1,
    quality: 'auto', showGrid: false, showRanges: false, tutorialSeen: false,
    lastVariant: 'classic', lastDifficulty: 'normal', lastBiome: 'random',
    lastSize: 'normal', lastHeroes: ['blade', 'bard'], skin: 'standard', lockRotation: false, keys: {},
  };
}

export function defaultProgress(): PlayerProgress {
  return {
    campaignUnlocked: 1, campaignCompleted: [], campaignStars: {}, bestEndlessWave: {}, bestScores: [],
    dailyCompleted: {}, achievements: [], totalRuns: 0, totalVictories: 0, totalKills: 0, totalBossKills: 0, bossesDefeated: [],
    heroMeta: defaultHeroMeta(),
  };
}

export function defaultHeroMeta(): HeroMetaProgress {
  return { bestWave: 0, unlockedItems: [], fragments: {}, claimedMilestones: [], committedRuns: [], startKit: [], lastChampion: 'lyra', runs: 0, passive: {}, champions: {}, heroLevel: { level: 1, xp: 0 }, towerMastery: {}, paragon: emptyParagon() };
}

function sanitizeHeroMeta(raw: unknown): HeroMetaProgress {
  const d = defaultHeroMeta();
  if (!isObj(raw)) return d;
  const bestWave = Math.round(num(raw.bestWave, 0, 0, 100000));
  const hl = sanitizeHeroLevel(raw.heroLevel, sanitizeChampionProgress(raw.champions));
  const items = Array.isArray(raw.unlockedItems) ? [...new Set(raw.unlockedItems.filter((x): x is string => typeof x === 'string' && x in ITEMS))] : [];
  return {
    bestWave,
    unlockedItems: items,
    fragments: isObj(raw.fragments)
      ? Object.fromEntries(Object.entries(raw.fragments).filter(([k, v]) => k in ITEMS && typeof v === 'number' && !items.includes(k)).map(([k, v]) => [k, Math.round(num(v, 0, 0, 9999))]).filter(([, v]) => (v as number) > 0))
      : {},
    claimedMilestones: Array.isArray(raw.claimedMilestones) ? [...new Set(raw.claimedMilestones.filter((x): x is number => typeof x === 'number' && Number.isFinite(x)))] : [],
    committedRuns: Array.isArray(raw.committedRuns) ? raw.committedRuns.filter((x): x is string => typeof x === 'string').slice(-50) : [],
    startKit: Array.isArray(raw.startKit) ? [...new Set(raw.startKit.filter((x): x is string => typeof x === 'string' && items.includes(x)))].slice(0, 6) : [],
    lastChampion: typeof raw.lastChampion === 'string' && raw.lastChampion in CHAMPIONS ? (raw.lastChampion as ChampionId) : 'lyra',
    runs: Math.round(num(raw.runs, 0, 0, 1e9)),
    passive: sanitizePassive(raw.passive, bestWave),
    champions: sanitizeChampionProgress(raw.champions),
    heroLevel: hl,
    towerMastery: sanitizeMastery(raw.towerMastery),
    paragon: sanitizeParagon(raw.paragon, paragonPrePoints(hl.level) + milestoneParagon(bestWave)),
  };
}

/** Dauerhafte Heldenstufen: Stufe 1–99, EP im Rahmen, nur gültige Skillknoten mit erlaubtem Rang. */
export function sanitizeChampionProgress(raw: unknown): HeroMetaProgress['champions'] {
  const out: HeroMetaProgress['champions'] = {};
  if (!isObj(raw)) return out;
  for (const [id, v] of Object.entries(raw)) {
    if (!(id in CHAMPIONS) || !isObj(v)) continue;
    const level = Math.round(num(v.level, 1, 1, PARAGON.heroMaxLevel));
    const xp = level >= PARAGON.heroMaxLevel ? 0 : Math.max(0, Math.min(xpToNext(level), num(v.xp, 0, 0, 1e12)));
    const tree: Record<string, number> = {};
    if (isObj(v.tree)) {
      for (const node of CHAMPIONS[id as ChampionId].tree) {
        const r = Math.round(num(v.tree[node.id], 0, 0, node.maxRank));
        if (r > 0) tree[node.id] = r;
      }
    }
    out[id as ChampionId] = { level, xp, tree };
  }
  return out;
}

export function defaultSave(): SaveData {
  return { version: SAVE_VERSION, settings: defaultSettings(), progress: defaultProgress(), customMaps: [], resume: null };
}

const num = (v: unknown, d: number, min: number, max: number): number =>
  typeof v === 'number' && Number.isFinite(v) ? Math.min(max, Math.max(min, v)) : d;
const bool = (v: unknown, d: boolean): boolean => (typeof v === 'boolean' ? v : d);
const oneOf = <T extends string>(v: unknown, opts: readonly T[], d: T): T => (typeof v === 'string' && (opts as readonly string[]).includes(v) ? (v as T) : d);
const isObj = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v);

/** Validiert und repariert geladene Daten feldweise mit sicheren Standardwerten. */
export function sanitizeSave(raw: unknown): SaveData {
  const d = defaultSave();
  if (!isObj(raw)) return d;
  const s = isObj(raw.settings) ? raw.settings : {};
  const ds = d.settings;
  d.settings = {
    masterVolume: num(s.masterVolume, ds.masterVolume, 0, 1),
    musicVolume: num(s.musicVolume, ds.musicVolume, 0, 1),
    sfxVolume: num(s.sfxVolume, ds.sfxVolume, 0, 1),
    muted: bool(s.muted, ds.muted),
    reducedMotion: bool(s.reducedMotion, ds.reducedMotion),
    uiScale: num(s.uiScale, ds.uiScale, 0.8, 1.4),
    quality: oneOf(s.quality, ['auto', 'low', 'medium', 'high'] as const, ds.quality),
    showGrid: bool(s.showGrid, ds.showGrid),
    showRanges: bool(s.showRanges, ds.showRanges),
    tutorialSeen: bool(s.tutorialSeen, ds.tutorialSeen),
    lastVariant: oneOf(s.lastVariant, ['classic', 'mazing'] as const, ds.lastVariant),
    lastDifficulty: oneOf(s.lastDifficulty, ['easy', 'normal', 'hard'] as const, ds.lastDifficulty),
    lastBiome: oneOf(s.lastBiome, ['random', 'forest', 'volcano', 'frozen', 'crystal', 'sky', 'swamp', 'cavern'] as const, ds.lastBiome),
    lastSize: oneOf(s.lastSize, ['normal', 'large', 'huge'] as const, ds.lastSize),
    lastHeroes: Array.isArray(s.lastHeroes) ? (s.lastHeroes.filter((h): h is HeroId => typeof h === 'string' && h in HEROES).slice(0, 2)) : ds.lastHeroes,
    skin: typeof s.skin === 'string' && SKINS.some((k) => k.id === s.skin) ? s.skin : ds.skin,
    lockRotation: bool(s.lockRotation, ds.lockRotation),
    keys: sanitizeKeys(s.keys),
  };
  const p = isObj(raw.progress) ? raw.progress : {};
  const missionIds = CAMPAIGN.map((m) => m.id);
  d.progress = {
    campaignUnlocked: Math.round(num(p.campaignUnlocked, 1, 1, CAMPAIGN.length)),
    campaignCompleted: Array.isArray(p.campaignCompleted) ? p.campaignCompleted.filter((x): x is string => typeof x === 'string' && missionIds.includes(x)) : [],
    campaignStars: isObj(p.campaignStars)
      ? Object.fromEntries(Object.entries(p.campaignStars).filter(([k, v]) => missionIds.includes(k) && typeof v === 'number').map(([k, v]) => [k, num(v, 0, 0, 3)]))
      : {},
    bestEndlessWave: isObj(p.bestEndlessWave)
      ? Object.fromEntries(Object.entries(p.bestEndlessWave).filter(([, v]) => typeof v === 'number').map(([k, v]) => [k, num(v, 0, 0, 100000)]))
      : {},
    bestScores: Array.isArray(p.bestScores)
      ? p.bestScores.filter(isObj).map((b) => ({
        mode: oneOf(b.mode, ['quick', 'campaign', 'endless', 'daily', 'custom', 'heroEndless'] as const, 'quick' as GameMode),
        score: num(b.score, 0, 0, 1e9),
        seed: typeof b.seed === 'string' ? b.seed.slice(0, 40) : '',
        date: typeof b.date === 'string' ? b.date.slice(0, 30) : '',
        waves: num(b.waves, 0, 0, 100000),
        victory: bool(b.victory, false),
      })).slice(0, 50)
      : [],
    dailyCompleted: isObj(p.dailyCompleted)
      ? Object.fromEntries(Object.entries(p.dailyCompleted).filter(([, v]) => typeof v === 'number').map(([k, v]) => [k.slice(0, 20), num(v, 0, 0, 1e9)]))
      : {},
    achievements: Array.isArray(p.achievements) ? p.achievements.filter((x): x is string => typeof x === 'string').slice(0, 100) : [],
    totalRuns: Math.round(num(p.totalRuns, 0, 0, 1e9)),
    totalVictories: Math.round(num(p.totalVictories, 0, 0, 1e9)),
    totalKills: Math.round(num(p.totalKills, 0, 0, 1e12)),
    totalBossKills: Math.round(num(p.totalBossKills, 0, 0, 1e9)),
    bossesDefeated: Array.isArray(p.bossesDefeated) ? p.bossesDefeated.filter((b): b is BossId => typeof b === 'string' && b in BOSSES) : [],
    heroMeta: sanitizeHeroMeta(p.heroMeta),
  };
  d.customMaps = Array.isArray(raw.customMaps)
    ? raw.customMaps.filter(isObj).filter((m) => typeof m.name === 'string' && typeof m.code === 'string')
      .map((m) => ({ name: String(m.name).slice(0, 40), code: String(m.code).slice(0, 20000) })).slice(0, 30)
    : [];
  d.resume = typeof raw.resume === 'string' && raw.resume.length < 2_000_000 ? raw.resume : null;
  return d;
}

/** Migration älterer Versionen. v1 → v2: Feld customMaps ergänzt. v2 → v3: Helden, Skins, Gesamtstatistik (Standardwerte über sanitizeSave). */
export function migrate(raw: Record<string, unknown>): Record<string, unknown> {
  const v = typeof raw.version === 'number' ? raw.version : 0;
  const out = { ...raw };
  if (v < 2 && !Array.isArray(out.customMaps)) out.customMaps = [];
  out.version = SAVE_VERSION;
  return out;
}

export interface LoadResult { data: SaveData; error: string | null; backupKey: string | null }

export class SaveSystem {
  constructor(private readonly store: KeyValueStore | null) {}

  load(): LoadResult {
    if (!this.store) return { data: defaultSave(), error: 'Lokaler Speicher ist nicht verfügbar – Fortschritt wird nicht gespeichert.', backupKey: null };
    let raw: string | null = null;
    try {
      raw = this.store.getItem(SAVE_KEY);
      if (raw === null) {
        for (const k of LEGACY_SAVE_KEYS) {
          raw = this.store.getItem(k);
          if (raw !== null) break;
        }
      }
    } catch {
      return { data: defaultSave(), error: 'Lokaler Speicher ist blockiert – Fortschritt wird nicht gespeichert.', backupKey: null };
    }
    if (raw === null) return { data: defaultSave(), error: null, backupKey: null };
    try {
      const parsed: unknown = JSON.parse(raw);
      if (!isObj(parsed)) throw new Error('Kein Objekt');
      if (typeof parsed.version === 'number' && parsed.version > SAVE_VERSION) {
        throw new Error(`Spielstand stammt aus einer neueren Version (${parsed.version})`);
      }
      return { data: sanitizeSave(migrate(parsed)), error: null, backupKey: null };
    } catch (err) {
      const backupKey = `${SAVE_KEY}.backup.${Date.now()}`;
      try {
        this.store.setItem(backupKey, raw);
      } catch {
        /* Sicherung nicht möglich */
      }
      const msg = err instanceof Error ? err.message : String(err);
      return {
        data: defaultSave(),
        error: `Der Spielstand konnte nicht geladen werden (${msg}). Eine Sicherungskopie wurde unter „${backupKey}“ abgelegt; es wird mit Standardwerten gespielt.`,
        backupKey,
      };
    }
  }

  save(data: SaveData): boolean {
    if (!this.store) return false;
    try {
      this.store.setItem(SAVE_KEY, JSON.stringify({ ...data, version: SAVE_VERSION }));
      return true;
    } catch {
      return false;
    }
  }
}

// ------------------------------------------------------------ Laufende Partie

/**
 * Prüft einen serialisierten Spielzustand. Partien werden nur in der Bauphase gespeichert,
 * in der keine Gegner, Projektile oder Zonen existieren – so ist die Wiederherstellung vollständig.
 */
export function deserializeRun(json: string): { state: GameState | null; error: string | null } {
  try {
    const s: unknown = JSON.parse(json);
    if (!isObj(s)) return { state: null, error: 'Ungültiges Format' };
    if (s.version !== STATE_VERSION) return { state: null, error: 'Spielstand stammt aus einer älteren Version und kann nicht fortgesetzt werden' };
    const st = s as unknown as GameState;
    if (!isObj(st.map) || !Array.isArray(st.towers) || !isObj(st.rules) || !isObj(st.seed)) return { state: null, error: 'Unvollständiger Spielstand' };
    if (!validateMap(st.map).ok) return { state: null, error: 'Karte des Spielstands ist ungültig' };
    if (st.phase !== 'build') return { state: null, error: 'Nur Bauphasen sind fortsetzbar' };
    if (typeof st.gold !== 'number' || typeof st.lives !== 'number' || typeof st.wave !== 'number') return { state: null, error: 'Fehlende Kernwerte' };
    for (const t of st.towers) {
      if (!isObj(t) || !(t.def in TOWERS)) return { state: null, error: 'Unbekannter Turmtyp' };
      if (t.cx < 0 || t.cy < 0 || t.cx >= st.map.width || t.cy >= st.map.height) return { state: null, error: 'Turm außerhalb der Karte' };
    }
    if (!Array.isArray(st.modifiers) || st.modifiers.some((m) => !MODIFIER_IDS.includes(m))) return { state: null, error: 'Unbekannter Modifikator' };
    st.enemies = [];
    st.projectiles = [];
    st.zones = [];
    st.telegraphs = [];
    st.spawnQueue = [];
    st.paused = false;
    if (!Array.isArray(st.heroes) || !Array.isArray(st.buffs)) return { state: null, error: 'Heldendaten fehlen' };
    for (const h of st.heroes) {
      if (!isObj(h) || !(h.id in HEROES)) return { state: null, error: 'Unbekannter Held' };
      if (h.state === 'active') {
        // Aktive Helden werden beim Speichern in der Bauphase sicher zurückgezogen.
        h.state = 'ready';
      }
    }
    st.buffs = [];
    if (st.mode === 'heroEndless') {
      const he = st.he;
      if (!isObj(he) || !isObj(he.champion) || !(he.champion.id in CHAMPIONS) || typeof he.runId !== 'string') return { state: null, error: 'Heldendaten des Endlosmodus fehlen' };
      const c = he.champion;
      if (!Array.isArray(c.inventory) || !Array.isArray(c.equipment) || c.equipment.length !== 6 || !Array.isArray(c.potions)) return { state: null, error: 'Inventar beschädigt' };
      // Keine doppelten Items: jedes Item höchstens einmal, nur bekannte IDs
      const seen = new Set<string>();
      c.inventory = c.inventory.filter((i) => isObj(i) && i.item in ITEMS && !seen.has(i.item) && (seen.add(i.item), true));
      const uids = new Set(c.inventory.map((i) => i.uid));
      c.equipment = c.equipment.map((u) => (typeof u === 'number' && uids.has(u) ? u : null));
      c.path = [];
      c.channel = 0;
      he.towerBuffs = Array.isArray(he.towerBuffs) ? he.towerBuffs.filter((b) => st.towers.some((t) => t.uid === b.tower)) : [];
      he.zones = [];
      if (!Array.isArray(he.pickups)) he.pickups = [];
      if (!isObj(he.passive)) he.passive = {};
    } else st.he = null;
    return { state: st, error: null };
  } catch (err) {
    return { state: null, error: err instanceof Error ? err.message : String(err) };
  }
}

export function canSerializeRun(s: GameState): boolean {
  return s.phase === 'build' && s.enemies.length === 0 && s.projectiles.length === 0 && s.spawnQueue.length === 0;
}

/**
 * Gemeinsame Heldenstufe (2.9.5). Ältere Spielstände hatten eine Stufe je Held – übernommen wird der weiteste
 * Stand (höchste Gesamt-EP) aller Helden, damit niemand Fortschritt verliert.
 */
export function sanitizeHeroLevel(raw: unknown, champions: HeroMetaProgress['champions']): { level: number; xp: number } {
  const total = (l: number, x: number): number => { let n = x; for (let k = 1; k < l; k++) n += xpToNext(k); return n; };
  let best = { level: 1, xp: 0 };
  const consider = (l: number, x: number): void => {
    const level = Math.round(Math.max(1, Math.min(PARAGON.heroMaxLevel, l || 1)));
    const xp = level >= PARAGON.heroMaxLevel ? 0 : Math.max(0, Math.min(xpToNext(level), x || 0));
    if (total(level, xp) > total(best.level, best.xp)) best = { level, xp };
  };
  if (isObj(raw)) consider(Number(raw.level), Number(raw.xp));
  for (const c of Object.values(champions)) if (c) consider(c.level, c.xp);
  return best;
}
