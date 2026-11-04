// Zentrale, typisierte Datenmodelle von KERNWACHT.
// Der gesamte Spielzustand (GameState) besteht aus reinen, JSON-serialisierbaren Daten.
// Abgeleitete Caches (Distanzfelder, Netzwerkgraph) liegen getrennt in SimCache.

import type { StatKey } from '../config/stats.ts';

export type DamageType = 'physical' | 'fire' | 'frost' | 'lightning' | 'poison' | 'arcane' | 'sonic';
export const DAMAGE_TYPES: readonly DamageType[] = ['physical', 'fire', 'frost', 'lightning', 'poison', 'arcane', 'sonic'];

export type TowerId =
  | 'ember' | 'frost' | 'storm' | 'spore' | 'ranger' | 'ballista'
  | 'rune' | 'resonator' | 'goldmine' | 'pylon' | 'cannon' | 'riftbinder' | 'barricade' | 'lanebarricade'
  | 'meteor' | 'tesla' | 'gravity' | 'dragon' | 'laser' | 'totem' | 'minelayer' | 'sporemine' | 'skyguard' | 'skywatch'
  | 'plasma' | 'glacier' | 'voidlance' | 'starfall'
  | 'thornwood' | 'thunderspire' | 'riftgate' | 'phoenix' | 'cathedral' | 'stormeye';

export type HeroId = 'blade' | 'hunter' | 'riftwalker' | 'bard' | 'smith' | 'weather' | 'mystic' | 'prospector' | 'silence';
export type HeroMode = 'hold' | 'advance' | 'retreat';
export type HeroState = 'ready' | 'active' | 'cooldown' | 'down';
export type MutationId = 'armored' | 'hasty' | 'explosive' | 'splitting' | 'vampiric';
export type MapSize = 'normal' | 'large' | 'huge';
export type TowerActiveId = 'overdrive' | 'deepfreeze' | 'overload' | 'goldrush' | 'surge' | 'rewind' | 'meteorstorm' | 'nova';

export type EnemyId =
  | 'runner' | 'swarm' | 'brute' | 'regen' | 'flyer' | 'shield' | 'phase'
  | 'saboteur' | 'scout' | 'broodmother' | 'broodling' | 'boss'
  | 'golem' | 'golemling' | 'stalker' | 'siege' | 'shaman' | 'tunneler' | 'griffin';

export type BossId = 'riftColossus' | 'ashQueen' | 'nullWarden' | 'maw' | 'twinSol' | 'twinLuna' | 'worldbreaker';

export type TargetPriority = 'first' | 'exit' | 'strong' | 'weak' | 'close' | 'random';

export type MapVariant = 'classic' | 'mazing';
export type BiomeId = 'forest' | 'volcano' | 'frozen' | 'crystal' | 'sky' | 'swamp' | 'cavern';
export type GameMode = 'quick' | 'campaign' | 'endless' | 'daily' | 'custom' | 'heroEndless';
export type Difficulty = 'easy' | 'normal' | 'hard';
export type GamePhase = 'build' | 'combat' | 'choice' | 'victory' | 'defeat';

export type AttackKind = 'projectile' | 'pulse' | 'chain' | 'beam' | 'economy' | 'pylon' | 'rift' | 'none'
  | 'meteor' | 'tesla' | 'gravity' | 'dragon' | 'laser' | 'aura' | 'mines';

export type StatusId =
  | 'burn' | 'chill' | 'freeze' | 'freezeImmune' | 'wet' | 'poison' | 'mark'
  | 'brittle' | 'silence' | 'riftMark' | 'root' | 'stun' | 'tether' | 'weather'
  | 'reveal' | 'voidMark' | 'pull' | 'shred';

export type ModifierId =
  | 'chainReaction' | 'wildfire' | 'bounty' | 'netDischarge' | 'eternalIce'
  | 'goldRush' | 'toxicMist' | 'sharpBlades' | 'arcaneEcho' | 'rapidFire'
  | 'glassCannon' | 'bulwark' | 'farSight' | 'resonanceAmp' | 'interest'
  | 'heroism' | 'riftTap';

export type TerrainEventId = 'blizzard' | 'heatwave' | 'riftSurge' | 'sporeBloom' | 'crystalResonance' | 'fog' | 'glowtide';

export type AbilityId =
  | 'quake' | 'netBite' | 'phaseShift'
  | 'riftJump' | 'riftPortals' | 'summonBrood' | 'fireZones' | 'ashFury'
  | 'nullPulse' | 'reShield'
  | 'siegeSlam' | 'heal' | 'devour' | 'stomp' | 'twinBond';

/** Zellcodes des Kartenrasters. */
export const Cell = {
  Open: 0,      // freie, bebaubare Fläche
  Path: 1,      // Weg (Classic) – begehbar, nicht bebaubar
  Blocked: 2,   // Hindernis (Fels, Wasser, Baum) – weder begehbar noch bebaubar
  Spawn: 3,
  Goal: 4,
  NoBuild: 5,   // begehbar, aber nicht bebaubar (Pufferzone im Mazing)
} as const;
export type CellCode = (typeof Cell)[keyof typeof Cell];

export interface GridPos { x: number; y: number }
export interface Vec2 { x: number; y: number }

export type Stats = Record<StatKey, number>;

export interface StatMod { stat: StatKey; add?: number; mul?: number }

export interface UpgradeDefinition {
  id: string;
  name: string;
  description: string;
  cost: number;
  mods: StatMod[];
}

export interface TowerDefinition {
  id: TowerId;
  /** Kantenlänge der Grundfläche in Feldern (Standard 1; Goldgräber 2 = 2×2). Ankerfeld ist oben links. */
  size?: number;
  name: string;
  role: string;
  description: string;
  damageType: DamageType | null;
  cost: number;
  attack: AttackKind;
  defaultPriority: TargetPriority;
  base: Partial<Stats>;
  /** Drei normale Stufen (Level 2, 3, 4). Leer nur bei der Barrikade (kein Turm). */
  upgrades: UpgradeDefinition[];
  /** Zwei alternative Spezialisierungen (ab Level 4). Leer nur bei der Barrikade. */
  specs: UpgradeDefinition[];
  /** Nur im Mazing-Modus verfügbar. */
  mazingOnly?: boolean;
  /** 3.8: nur auf der Lane (Wegfelder) baubar, nicht im Mazing-Modus. */
  laneOnly?: boolean;
  synergies: string;
  counters: string;
  networkable: boolean;
  hotkey: string;
  icon: string;
  color: number;
  /** Aktive Fähigkeit (ab Stufe 2 nutzbar, nur im Kampf). */
  active?: TowerActiveId;
  /** Legendärer Fusionsturm – entsteht nur durch Fusion, nicht baubar. */
  legendary?: boolean;
  /** Erkennt getarnte Gegner in Reichweite. */
  detector?: boolean;
}

export interface FusionRecipe {
  id: TowerId;
  a: TowerId;
  b: TowerId;
  cost: number;
}

export interface TowerActiveDefinition {
  id: TowerActiveId;
  name: string;
  description: string;
  cooldown: number;
  duration: number;
}

export interface EnemyDefinition {
  id: EnemyId;
  name: string;
  role: string;
  description: string;
  counter: string;
  hp: number;
  speed: number;           // Zellen pro Sekunde
  reward: number;
  threat: number;          // Kosten im Wellenbudget
  leak: number;            // Verlust an Kernenergie
  flying: boolean;
  resist: Partial<Record<DamageType, number>>;
  shieldPct: number;       // Schild relativ zu HP
  regenPct: number;        // HP-Anteil pro Sekunde
  abilities: AbilityId[];
  route: 'normal' | 'safe';
  onDeathSpawn?: { enemy: EnemyId; count: number };
  /** Getarnt, bis ein Detektor (Resonator, Runenweber, Totem, Held) ihn aufdeckt. */
  stealth?: boolean;
  /** Gräbt sich ein und taucht kurz vor dem Ziel wieder auf. */
  burrow?: boolean;
  /** Chance, Projektilen auszuweichen. */
  evasion?: number;
  /** Kontaktschaden pro Sekunde gegen Helden. */
  contactDps?: number;
  size: number;
  color: number;
  icon: string;
  minWave: number;
}

export interface BossPhaseDefinition {
  /** Phase beginnt, wenn HP-Anteil <= hpBelow. */
  hpBelow: number;
  name: string;
  abilities: AbilityId[];
  speedMul: number;
  /** Alternativer Auslöser statt HP-Schwelle. */
  trigger?: 'shieldBroken';
}

export interface BossDefinition {
  id: BossId;
  name: string;
  title: string;
  description: string;
  counter: string;
  hp: number;
  speed: number;
  resist: Partial<Record<DamageType, number>>;
  poise: number;
  reward: number;
  leak: number;
  phases: BossPhaseDefinition[];
  shieldPct: number;
  color: number;
  size: number;
  /** Anzahl Schwachpunkte (Weltenbrecher) – Kern nimmt erst danach vollen Schaden. */
  weakPoints?: number;
  /** Partnerboss (Zwillinge), erscheint gemeinsam. */
  partner?: BossId;
}

export interface AbilityDefinition {
  id: AbilityId;
  name: string;
  description: string;
  windup: number;
  cooldown: number;
  interruptible: boolean;
}

export interface StatusInstance {
  id: StatusId;
  t: number;          // Restdauer in Sekunden
  stacks: number;
  power: number;      // z. B. DPS, Slow-Anteil, Resistenzreduktion
  src: number;        // Turm-UID oder 0
  flag: number;       // z. B. 1 = darf sich nicht weiter ausbreiten
}

export interface ElementReaction {
  id: string;
  name: string;
  description: string;
  trigger: DamageType;
  requires: StatusId;
  cooldown: number;
}

export interface StatusEffectDefinition {
  id: StatusId;
  name: string;
  description: string;
  icon: string;
  color: number;
  maxStacks: number;
}

export interface AbilityState {
  id: AbilityId;
  cd: number;          // Abklingzeit
  windup: number;      // >0 = wird gerade gewirkt
  tx: number;          // Zielposition (Ankündigung)
  ty: number;
  r: number;
}

export interface EnemyInstance {
  uid: number;
  def: EnemyId;
  boss: BossId | null;
  /** 3.0: Affixe, Immunität und Weltstufe der Welle, in der der Gegner erschien. */
  affixes?: import('../config/scaling.ts').AffixId[];
  immune?: DamageType;
  tier?: number;
  splitChild?: boolean;
  jamCd?: number;
  /** 3.1: Challenge-Boss mit Schildschüben, Beschwörungen und Raserei. */
  challenge?: boolean;
  chShieldCd?: number;
  chSummonCd?: number;
  chEnraged?: boolean;
  elite: boolean;
  x: number; y: number;
  nx: number; ny: number;      // Zielzelle der aktuellen Bewegung
  hp: number; maxHp: number;
  shield: number; maxShield: number;
  speed: number;
  flying: boolean;
  route: 'normal' | 'safe';
  traveled: number;
  remaining: number;
  statuses: StatusInstance[];
  abilities: AbilityState[];
  poise: number; maxPoise: number;
  phase: number;
  phaseShift: number;          // >0 = Phasenschild aktiv
  adapt: Record<DamageType, number>;
  adaptHits: Record<DamageType, number>;
  reward: number;
  leak: number;
  alive: boolean;
  rewarded: boolean;
  riftCd: number;
  knockCd: number;
  reactCds: Record<string, number>;
  abilityMul: number;
  wave: number;
  spawnedAt: number;
  hitFlash: number;
  reshieldUsed: boolean;
  mutation: MutationId | null;
  stealthed: boolean;
  burrowed: boolean;
  surfaced: boolean;
  spawnIndex: number;
  /** Schwachpunkte: verbleibende LP je Punkt (Weltenbrecher). */
  plates: number[];
  plateMax: number;
  /** Verschluckte Türme (Schlund der Tiefe) – werden beim Tod zurückgegeben. */
  swallowed: TowerInstance[];
  /** Blockiert durch einen Helden (UID) oder 0. */
  blockedBy: number;
  enraged: boolean;
  /** Mini-Boss im Helden-Endlosmodus (mehr LP, mehr Beute). */
  miniBoss?: boolean;
}

export interface TowerInstance {
  uid: number;
  def: TowerId;
  cx: number; cy: number;
  level: number;            // 1..4
  spec: 0 | 1 | 2;          // 0 = keine Spezialisierung
  /** 3.6: Veredelung ★0–★10 (nur Endlos, voll ausgebaut). */
  stars?: number;
  invested: number;
  builtWave: number;        // Anzahl gestarteter Wellen beim Bau
  builtInPhase: boolean;    // in der aktuellen Bauphase gebaut → volle Erstattung
  priority: TargetPriority;
  cooldown: number;
  target: number;           // Ziel-UID oder 0
  retarget: number;
  aim: number;
  stunned: number;          // durch Bossfähigkeiten deaktiviert
  disrupted: number;        // Netzwerk gestört
  rampTarget: number;
  rampStacks: number;
  damageDealt: number;
  kills: number;
  goldEarned: number;
  shots: number;
  fireAnim: number;
  activeCd: number;
  activeT: number;
  /** Feldschmiede: verstärkt bis Zeitpunkt. */
  forged: number;
  /** Strahl-/Laseraufbau. */
  charge: number;
  /** Drachenposition (Winkel) für den Drachenhorst. */
  orbit: number;
}

export interface HitPayload {
  damage: number;
  type: DamageType;
  splash: number;
  stats: Stats;
  tower: number;
  towerDef: TowerId;
}

export interface Projectile {
  uid: number;
  x: number; y: number; z: number;
  tx: number; ty: number;
  target: number;
  speed: number;
  payload: HitPayload;
  kind: TowerId;
  pierceLeft: number;
  dirX: number; dirY: number;
  traveled: number;
  maxTravel: number;
  hitList: number[];
  alive: boolean;
}

export type ZoneKind = 'poisonCloud' | 'fireZone' | 'portalIn' | 'portalOut' | 'quake' | 'nullPulse' | 'riftJump'
  | 'meteor' | 'burnTrap' | 'leyNode' | 'stomp' | 'siegeSlam' | 'devour' | 'mine';

export interface Zone {
  uid: number;
  kind: ZoneKind;
  x: number; y: number; r: number;
  t: number;            // Restdauer
  armed: number;        // >0 = Ankündigungsphase (Telegraph)
  power: number;
  tick: number;
  link: number;         // Partnerzone (Portale) oder Quelle
  src: number;
}

export interface MapDefinition {
  version: number;
  width: number;
  height: number;
  variant: MapVariant;
  biome: BiomeId;
  cells: number[];          // Cell-Codes
  heights: number[];        // 0..3, Anhöhe ab 2
  veins: number[];          // Zellindizes mit Goldadern
  /** Kristalladern (nur Helden-Endlos): Goldgräber fördert hier zusätzlich Kristall. */
  deposits?: number[];
  /** Änderungszähler (z. B. nach Abbau eines Hindernisses) – Darstellung baut das Gelände neu. */
  rev?: number;
  ley: number[];            // Zellindizes mit Leylinien (+Schaden)
  decor: number[];          // [index, art, rotation*100] Tripel – rein kosmetisch
  spawn: GridPos;
  /** Alle Startpunkte (inkl. spawn). Große Karten können Weggabelungen mit mehreren Starts haben. */
  spawns?: GridPos[];
  size?: MapSize;
  goal: GridPos;
  terrainEvent: TerrainEventId | null;
  fallback: boolean;
  name: string;
}

export interface WorldSeed {
  text: string;    // sichtbarer, kopierbarer Seed
  hash: number;    // abgeleiteter uint32
}

export interface SpawnEntry {
  at: number;
  enemy: EnemyId;
  elite: boolean;
  boss: BossId | null;
  mutation?: MutationId | null;
  miniBoss?: boolean;
  challenge?: boolean;
}

export interface WaveGroup { enemy: EnemyId; count: number; elite: boolean; boss: BossId | null; mutation?: MutationId | null; miniBoss?: boolean; challenge?: boolean }

export interface WaveDefinition {
  index: number;
  groups: WaveGroup[];
  hpMul: number;
  speedMul: number;
  rewardMul: number;
  /** 3.0: Weltstufe und Wellen-Affixe (nur Endlos). */
  tier?: number;
  affixes?: import('../config/scaling.ts').AffixId[];
  immune?: DamageType;
  event: TerrainEventId | null;
  isBoss: boolean;
}

export interface RunModifier {
  id: ModifierId;
  name: string;
  description: string;
  tags: string[];
  exclusive?: ModifierId[];
  requiresTower?: TowerId[];
  /** Nur anbieten, wenn Helden in der Partie sind. */
  requiresHeroes?: boolean;
  icon: string;
}

export interface ResonanceDefinition {
  id: string;
  name: string;
  description: string;
  requires: TowerId[];
  mods: StatMod[];
}

export interface MissionRules {
  waves: number;
  startGold: number;
  lives: number;
  bosses: Record<number, BossId>;
  enemyResistBonus: Partial<Record<DamageType, number>>;
  /** 3.3: Turmlimit abgeschaltet (nur automatisierte Tests). */
  towerLimitOff?: boolean;
  veinMul: number;
  modifierWaves: number[];
  bannedTowers: TowerId[];
  startModifiers: ModifierId[];
  endless: boolean;
  /** Mission scheitert, wenn dieser Held fällt. */
  heroMustSurvive?: HeroId;
  /** Vorgegebene Helden (Mission). */
  forcedHeroes?: HeroId[];
}

export interface RunStats {
  kills: number;
  leaks: number;
  livesLost: number;
  goldEarned: number;
  goldSpent: number;
  goldPerWave: number[];
  damageByTower: Partial<Record<TowerId, number>>;
  builtByTower: Partial<Record<TowerId, number>>;
  bossesKilled: BossId[];
  bossesFaced: BossId[];
  reactions: Record<string, number>;
  wavesCleared: number;
  maxNetworkSize: number;
  duration: number;
  heroDeploys: number;
  heroKills: number;
  heroDowns: number;
  fusions: number;
  activesUsed: number;
  mutationsKilled: number;
}

export interface Telegraph {
  uid: number;
  text: string;
  x: number; y: number; r: number;
  t: number;
  total: number;
  severity: 'info' | 'warn' | 'danger';
}

export interface NetworkLink { a: number; b: number }

export interface NetworkState {
  links: NetworkLink[];
  groups: { members: number[]; bonus: number; resonances: string[] }[];
  version: number;
}

export interface BossState {
  uid: number;
  id: BossId;
  phase: number;
  phaseName: string;
}

export interface GameState {
  version: number;
  seed: WorldSeed;
  mode: GameMode;
  difficulty: Difficulty;
  missionId: string | null;
  rules: MissionRules;
  map: MapDefinition;
  tick: number;
  time: number;
  gold: number;
  lives: number;
  maxLives: number;
  /** 3.4: zusätzliche Turmplätze aus geschafften Bosswellen (Endlos). */
  bossSlots?: number;
  /** 3.8: Zeitpunkte (Spielzeit s), zu denen Lane-Barrikaden gebaut wurden – für die Abklingzeit. */
  laneBarricades?: number[];

  wave: number;                 // Anzahl gestarteter Wellen
  phase: GamePhase;
  waveTime: number;
  currentWave: WaveDefinition | null;
  spawnQueue: SpawnEntry[];
  towers: TowerInstance[];
  enemies: EnemyInstance[];
  projectiles: Projectile[];
  zones: Zone[];
  telegraphs: Telegraph[];
  modifiers: ModifierId[];
  modOffer: ModifierId[] | null;
  rng: { combat: number; mods: number; loot?: number };
  nextUid: number;
  boss: BossState | null;
  network: NetworkState;
  stats: RunStats;
  netZapTimer: number;
  speed: number;
  paused: boolean;
  score: number;
  heroes: HeroInstance[];
  /** Zustand des Helden-Endlosmodus (null in allen anderen Modi). */
  he?: HeroEndlessState | null;
  riftEnergy: number;
  /** Zeitlich begrenzte globale Effekte (Heldenfähigkeiten, Turmfähigkeiten). */
  buffs: GlobalBuff[];
}

export type GlobalBuffId = 'hymn' | 'worldweb' | 'resoSong' | 'goldFever' | 'surge';
export interface GlobalBuff { id: GlobalBuffId; t: number; power: number; src: number }

export interface HeroStats {
  damage: number;
  range: number;
  attackCd: number;
  maxHp: number;
  speed: number;
  auraRadius: number;
  auraPower: number;
  abilityCdMul: number;
  activeTime: number;
  blockCount: number;
  goldBonus: number;
  armor: number;
}

export interface HeroPerk {
  id: string;
  name: string;
  description: string;
  add?: Partial<HeroStats>;
  mul?: Partial<HeroStats>;
}

export interface HeroAbilityDefinition {
  id: string;
  name: string;
  description: string;
  cooldown: number;
}

export interface HeroDefinition {
  id: HeroId;
  name: string;
  title: string;
  role: 'Blocker' | 'Fernkampf' | 'Assassine' | 'Unterstützung' | 'Reparatur' | 'Elementar' | 'Netzwerk' | 'Wirtschaft' | 'Kontrolle';
  support: boolean;
  description: string;
  damageType: DamageType;
  cost: number;           // Rissenergie für einen Einsatz
  base: HeroStats;
  abilities: [HeroAbilityDefinition, HeroAbilityDefinition];
  ultimate: HeroAbilityDefinition;
  perks: [HeroPerk, HeroPerk][];   // je Stufe 2..5 eine Wahl
  color: number;
  accent: number;
  icon: string;
  unlock: string;         // Missions-ID oder 'start'
}

export interface HeroInstance {
  id: HeroId;
  uid: number;
  state: HeroState;
  mode: HeroMode;
  x: number; y: number;
  ax: number; ay: number;          // Ankerpunkt
  hp: number;
  level: number;
  xp: number;
  perks: number[];                 // gewählte Perks (0/1) je Stufe
  pendingPerk: boolean;
  activeLeft: number;
  cooldown: number;
  abilityCd: [number, number];
  ult: number;                     // 0..100
  attackCd: number;
  target: number;
  buffT: number;                   // z. B. Bollwerk, Brandpfeile
  buffKind: string;
  kills: number;
  damageDealt: number;
  treasureWave: number;
  facing: number;
  anim: number;
  blocking: number;
}

export interface PlayerSettings {
  masterVolume: number;
  musicVolume: number;
  sfxVolume: number;
  muted: boolean;
  reducedMotion: boolean;
  uiScale: number;
  quality: 'auto' | 'low' | 'medium' | 'high';
  showGrid: boolean;
  showRanges: boolean;
  tutorialSeen: boolean;
  lastVariant: MapVariant;
  lastDifficulty: Difficulty;
  lastBiome: BiomeId | 'random';
  lastSize: MapSize;
  lastHeroes: HeroId[];
  skin: string;
  /** Kameradrehung (Q/E, Drehknöpfe) gesperrt. */
  /** Eigene Tastenbelegung: Aktion → Taste (nur Abweichungen vom Standard). */
  keys: Record<string, string>;
  lockRotation: boolean;
  /** 3.13: Kino-Effekte (Tilt-Shift-Unschärfe, Filmkorn, Sonnendunst); fehlt = an. */
  cinematicFx?: boolean;
}

export interface PlayerProgress {
  campaignUnlocked: number;            // Anzahl freigeschalteter Missionen
  campaignCompleted: string[];
  campaignStars: Record<string, number>;
  bestEndlessWave: Record<string, number>;   // key: variant
  bestScores: { mode: GameMode; score: number; seed: string; date: string; waves: number; victory: boolean }[];
  dailyCompleted: Record<string, number>;    // Datum → Score
  achievements: string[];
  totalRuns: number;
  totalVictories: number;
  totalKills: number;
  totalBossKills: number;
  bossesDefeated: BossId[];
  heroMeta: HeroMetaProgress;
}

export interface HeroMetaProgress {
  /** Höchste je erreichte (abgeschlossene) Welle im Helden-Endlosmodus. */
  bestWave: number;
  /** Dauerhaft freigeschaltete Items (Sammlung). */
  unlockedItems: ItemId[];
  /** Gesammelte Item-Fragmente je Item. */
  fragments: Partial<Record<ItemId, number>>;
  /** Einmalige Meilensteine, deren Belohnung bereits vergeben wurde. */
  claimedMilestones: number[];
  /** Bereits verbuchte Partien (verhindert doppelte Gutschrift). */
  committedRuns: string[];
  /** Gewählte Startausrüstung aus der Sammlung. */
  startKit: ItemId[];
  lastChampion: ChampionId;
  runs: number;
  /** Passiver Baum: Knoten-ID → Rang. */
  passive: Record<string, number>;
  /** 2.7: Skillbaum je Held (Stufe/EP darin nur noch zur Abwärtskompatibilität). */
  champions: Partial<Record<ChampionId, ChampionProgress>>;
  /** 2.9.5: gemeinsame Heldenstufe (bis 99) für alle Helden. */
  heroLevel: { level: number; xp: number };
  /** 3.0: Turm-Meisterschaft – dauerhafte Abschüsse je Turmtyp. */
  towerMastery: Partial<Record<TowerId, number>>;
  /** 2.7: Paragon, gemeinsam für alle Helden. */
  paragon: { level: number; xp: number; points: Record<string, number> };
}

export interface RuneInstance {
  uid: number;
  rune: import('../config/runes.ts').RuneId;
  tier: number;
  /** Veraltet (bis 3.2: einzelner Turm); wird beim Laden in def umgewandelt. */
  tower: number;
  /** 3.3: gesockelt in dieser Turmsorte (gilt für alle Türme der Sorte); fehlt = im Beutel. */
  def?: import('./types.ts').TowerId;
}

export interface ChampionProgress {
  level: number;
  xp: number;
  tree: Record<string, number>;
}

export interface SaveData {
  version: number;
  settings: PlayerSettings;
  progress: PlayerProgress;
  customMaps: { name: string; code: string }[];
  resume: string | null;              // serialisierter GameState (nur Bauphase)
}

export type GameEvent =
  | { k: 'shot'; tower: TowerId; x: number; y: number; tx: number; ty: number; uid: number }
  | { k: 'hit'; type: DamageType; x: number; y: number; amount: number; splash: number }
  | { k: 'chain'; pts: number[] }
  | { k: 'pulse'; tower: TowerId; x: number; y: number; r: number }
  | { k: 'kill'; enemy: EnemyId; x: number; y: number; gold: number; elite: boolean; boss: boolean }
  | { k: 'leak'; enemy: EnemyId; lives: number }
  | { k: 'reaction'; id: string; x: number; y: number }
  | { k: 'gold'; amount: number; x: number; y: number }
  | { k: 'build'; tower: TowerId; x: number; y: number }
  | { k: 'sell'; tower: TowerId; x: number; y: number; refund: number }
  | { k: 'upgrade'; tower: TowerId; x: number; y: number }
  | { k: 'waveStart'; wave: number; boss: boolean }
  | { k: 'waveClear'; wave: number; bonus: number }
  | { k: 'bossPhase'; boss: BossId; phase: number; name: string }
  | { k: 'ability'; id: AbilityId; x: number; y: number; stage: 'warn' | 'cast' | 'interrupt' }
  | { k: 'teleport'; x: number; y: number; x2: number; y2: number }
  | { k: 'immune'; x: number; y: number }
  | { k: 'victory' }
  | { k: 'defeat' }
  | { k: 'message'; text: string; severity: 'info' | 'warn' | 'danger' }
  | { k: 'heroDeploy'; hero: HeroId; x: number; y: number }
  | { k: 'heroDown'; hero: HeroId; x: number; y: number }
  | { k: 'heroAbility'; hero: HeroId; ability: string; x: number; y: number; r: number }
  | { k: 'heroAttack'; hero: HeroId; x: number; y: number; tx: number; ty: number; melee: boolean }
  | { k: 'heroLevel'; hero: HeroId; level: number }
  | { k: 'meteor'; x: number; y: number; r: number; big: boolean }
  | { k: 'laser'; x: number; y: number; tx: number; ty: number; power: number; uid: number }
  | { k: 'arc'; x: number; y: number; tx: number; ty: number }
  | { k: 'gravity'; x: number; y: number; r: number }
  | { k: 'breath'; x: number; y: number; tx: number; ty: number }
  | { k: 'fusion'; tower: TowerId; x: number; y: number }
  | { k: 'towerActive'; tower: TowerId; active: TowerActiveId; x: number; y: number }
  | { k: 'plateBreak'; x: number; y: number; left: number }
  | { k: 'burst'; x: number; y: number; r: number }
  | { k: 'devour'; tower: TowerId; x: number; y: number }
  | { k: 'heal'; x: number; y: number; r: number }
  | { k: 'death'; enemy: EnemyId; boss: BossId | null; x: number; y: number; color: number; size: number }
  | { k: 'burrow'; x: number; y: number; up: boolean }
  | { k: 'champAttack'; x: number; y: number; tx: number; ty: number; melee: boolean; color: number }
  | { k: 'champSkill'; skill: string; x: number; y: number; tx: number; ty: number; r: number; color: number }
  | { k: 'champLevel'; level: number; x: number; y: number }
  | { k: 'champDown'; x: number; y: number }
  | { k: 'champRespawn'; x: number; y: number }
  | { k: 'pickup'; kind: PickupKind; x: number; y: number; label: string; color: number }
  | { k: 'towerBuff'; x: number; y: number; color: number }
  | { k: 'potion'; potion: PotionId; x: number; y: number }
  | { k: 'craft'; item: ItemId; label: string }
  | { k: 'mined'; x: number; y: number; label: string }
  | { k: 'mineStart'; x: number; y: number; total: number };


// ------------------------------------------------------------------ Helden-Endlosmodus

export type ChampionId = 'lyra' | 'brann' | 'vela' | 'kaela' | 'varo' | 'nyx' | 'liora' | 'thorgrim' | 'sena' | 'oran' | 'mira' | 'ommar';
export type ItemId = string;
export type PotionId = 'heal' | 'mana' | 'power' | 'haste' | 'swift' | 'stone' | 'focus' | 'towerTonic';
export type ResourceId = 'metal' | 'crystal' | 'essence' | 'relic';
export type EquipSlot = 'weapon' | 'armor' | 'helm' | 'artifact' | 'accessory' | 'special';
export type Rarity = 'common' | 'rare' | 'epic' | 'legendary';

/** Attribute eines frei steuerbaren Helden. */
export interface ChampionStats {
  maxHp: number;
  hpRegen: number;
  maxMana: number;
  manaRegen: number;
  damage: number;
  attackSpeed: number;     // Angriffe pro Sekunde
  range: number;
  moveSpeed: number;       // Felder pro Sekunde
  armor: number;           // 0..0,8 Schadensreduktion
  critChance: number;
  critDamage: number;      // Multiplikator
  cdr: number;             // Abklingzeitreduktion 0..0,5
  abilityPower: number;    // Multiplikator für Fähigkeiten
  supportPower: number;    // Multiplikator für Turm-Verstärkungen
  supportRadius: number;   // Felder
  lootBonus: number;       // zusätzliche Dropchance (Faktor)
  pickupRadius: number;    // Felder
  lifesteal: number;
  cleave: number;          // Flächenanteil von Standardangriffen
}

/** Zeitlich begrenzte Wirkung am Helden (Tränke, Fähigkeiten). Gleiche Quelle erneuert statt zu stapeln. */
export interface ChampionBuff {
  src: string;
  t: number;
  dur: number;
  add: Partial<ChampionStats>;
}

export interface ItemInstance {
  uid: number;
  item: ItemId;
  /** Verstärkungsstufe 0..3 (je +15 % Itemwerte). */
  plus: number;
}

export interface PotionStack { potion: PotionId; count: number }

/** Zeitlich begrenzte Turmverstärkung. Je Turm und Quelle höchstens ein Eintrag. */
export interface TowerBuff {
  tower: number;
  src: string;
  t: number;
  dur: number;
  dmg: number;
  speed: number;
  range: number;
  crit: number;
}

export interface ChampionState {
  id: ChampionId;
  x: number; y: number;
  /** Haltepunkt (letzter Bewegungsbefehl) */
  hx: number; hy: number;
  path: number[];            // Zellindizes des aktuellen Wegs
  hp: number;
  mana: number;
  level: number;
  xp: number;
  skillPoints: number;
  tree: Record<string, number>;
  skillCd: number[];
  attackCd: number;
  target: number;
  facing: number;
  anim: number;
  buffs: ChampionBuff[];
  equipment: (number | null)[];   // Item-Instanz-UID je Slot (6)
  specialPotion: PotionStack | null;   // Trank im Spezialslot
  inventory: ItemInstance[];
  potions: PotionStack[];
  potionCd: Partial<Record<PotionId, number>>;
  dead: number;              // >0 = Wiederbelebung in s
  channel: number;           // laufende Kanalisierung (Klingensturm)
  channelTick: number;
  kills: number;
  damageDealt: number;
  /** Position festgesetzt: Klicks bewegen den Helden nicht, er verfolgt keine Gegner. */
  holdPos?: boolean;
}

export type PickupKind = 'xp' | 'resource' | 'fragment' | 'item' | 'potion';

export interface Pickup {
  uid: number;
  kind: PickupKind;
  x: number; y: number;
  amount: number;
  ref: string;               // Ressourcen-, Item- oder Trank-ID
  t: number;                 // Restlebensdauer
}

/** Kurzlebige Heldenwirkungen am Boden (Fallen, Pfeilhagel). */
export interface ChampionZone {
  uid: number;
  kind: 'snare' | 'volley';
  x: number; y: number; r: number;
  t: number;
  tick: number;
  power: number;
}

export interface HeroEndlessState {
  /** 3.0: Paragonpunkte aus Meilensteinen (Bestwelle beim Start). */
  paragonBonus?: number;
  /** 3.0: Turm-Meisterschaft – Abschüsse je Turmtyp (dauerhafter Stand beim Start + dieser Lauf). */
  mastery?: Partial<Record<TowerId, number>>;
  /** 3.0: Kampfdauer der aktuellen Welle für nachrückende Wellen. */
  overrunT?: number;
  overrunWave?: number;
  /** Runen (2.7): gefundene Runen; tower = UID des Turms, in dem sie gesockelt ist, 0 = im Beutel. */
  runes?: RuneInstance[];
  /** Paragon (2.7): gemeinsamer, dauerhafter Stand aller Helden; rev steigt bei jeder Punktvergabe. */
  paragon?: { level: number; xp: number; points: Record<string, number>; rev: number };
  runId: string;
  champion: ChampionState;
  pickups: Pickup[];
  zones: ChampionZone[];
  resources: Record<ResourceId, number>;
  towerBuffs: TowerBuff[];
  shop: { x: number; y: number };
  /** Countdown bis zum automatischen Wellenstart (s); <0 = inaktiv */
  autoTimer: number;
  /** Bei Partiebeginn bekannte, dauerhaft freigeschaltete Items. */
  metaOwned: ItemId[];
  /** In dieser Partie neu freigeschaltete Items (gedroppt oder hergestellt). */
  runUnlocks: ItemId[];
  /** Aktueller Fragmentbestand (Sammlung + Partie). */
  fragments: Partial<Record<ItemId, number>>;
  /** Veränderung des Fragmentbestands gegenüber Partiebeginn (wird beim Abschluss verbucht). */
  fragmentDelta: Partial<Record<ItemId, number>>;
  /** Aktive, dauerhafte Perks (bei Partiebeginn aus dem Fortschritt übernommen). */
  perks: string[];
  bestWaveAtStart: number;
  shopStock: Record<string, number>;
  stockWave: number;
  /** Profil der aktuellen bzw. nächsten Welle. */
  profile: string;
  /** Höchste Welle, deren Wellenbelohnung (Ressourcen) bereits vergeben wurde. */
  rewardedWave: number;
  stats: { xpCollected: number; itemsCrafted: number; itemsFound: number; potionsUsed: number; resourcesEarned: number; levelUps: number };
  /** Passiver Baum (Schnappschuss bei Laufbeginn; dauerhafter Fortschritt). */
  passive?: Record<string, number>;
  /** Laufender Abbau eines Hindernisses (Baum, Fels …) – nicht unterbrechbar. */
  mining?: { cell: number; t: number; total: number } | null;
  /** Hindernis, zu dem der Held läuft, um es abzubauen. */
  mineTarget?: number;
  /** Abgebaute Hindernisse in diesem Lauf. */
  mined?: number;
}
