import type { AbilityDefinition, AbilityId, BossDefinition, BossId, EnemyDefinition, EnemyId, MutationId } from '../core/types.ts';

// Gegnerkonfiguration. Resistenzen: 0 = normal, 0.5 = halber Schaden, negativ = Schwäche.

export const ENEMIES: Record<EnemyId, EnemyDefinition> = {
  runner: {
    id: 'runner', name: 'Flinkling', role: 'Schneller Läufer', icon: '💨', color: 0xe0c060, size: 0.32,
    description: 'Schnell und schwach. Kommt in Gruppen.', counter: 'Frost verlangsamt, Waldläufer erledigen sie zuverlässig.',
    hp: 30, speed: 1.9, reward: 3, threat: 1, leak: 1, flying: false, resist: {}, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 1,
  },
  swarm: {
    id: 'swarm', name: 'Splitterschwarm', role: 'Schwarm', icon: '🐜', color: 0xc08050, size: 0.22,
    description: 'Viele winzige Gegner auf einmal.', counter: 'Flächenschaden: Kanone, Glutwerfer, Sturmrufer.',
    hp: 13, speed: 1.6, reward: 1, threat: 0.4, leak: 1, flying: false, resist: { poison: 0.3 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 2,
  },
  brute: {
    id: 'brute', name: 'Steinhäuter', role: 'Schwer gepanzert', icon: '🪨', color: 0x8a8070, size: 0.5,
    description: 'Langsam, viele Lebenspunkte und dicker Panzer (physisch −45 %).', counter: 'Belagerungsarmbrust (panzerbrechend), Runenmarkierung, Magie.',
    hp: 150, speed: 0.75, reward: 9, threat: 4, leak: 2, flying: false, resist: { physical: 0.45, fire: 0.1, arcane: -0.15 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 3,
  },
  regen: {
    id: 'regen', name: 'Moosling', role: 'Regenerierend', icon: '🌿', color: 0x4fbf6a, size: 0.36,
    description: 'Regeneriert 4 % LP pro Sekunde, solange er weder brennt noch vergiftet ist.', counter: 'Gift, Feuer oder Rüstungsbruch (Dornenhain) stoppen die Regeneration; Feuer trifft ihn zusätzlich stärker.',
    hp: 75, speed: 1.1, reward: 6, threat: 2.5, leak: 1, flying: false, resist: { fire: -0.2 }, shieldPct: 0, regenPct: 0.04,
    abilities: [], route: 'normal', minWave: 4,
  },
  flyer: {
    id: 'flyer', name: 'Aschenschwinge', role: 'Flieger', icon: '🦇', color: 0xa060c0, size: 0.34,
    description: 'Fliegt über dem Weg zum Kern – Bodentürme wie Kanonen und Sporen erreichen ihn nicht.', counter: 'Flakbatterie (reine Flugabwehr, verlangsamt) und Drachenhorst sind am stärksten; Luftabwehr haben auch u. a. Waldläufer, Frost, Sturmrufer, Runenweber, Resonator, Rissbinder, Tesla, Laser, Drachenhorst).',
    hp: 54, speed: 1.45, reward: 5, threat: 2.6, leak: 1, flying: true, resist: { frost: -0.2, lightning: -0.1 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 5,
  },
  shield: {
    id: 'shield', name: 'Schildträger', role: 'Adaptiver Schild', icon: '🛡️', color: 0x7090d0, size: 0.4,
    description: 'Trägt einen Schild, der sich an wiederholt erlittene Schadensarten anpasst (bis −45 %).', counter: 'Gemischte Schadensarten nutzen. Anpassung wird als Symbol angezeigt.',
    hp: 70, speed: 1.0, reward: 7, threat: 3, leak: 1, flying: false, resist: {}, shieldPct: 0.9, regenPct: 0,
    abilities: [], route: 'normal', minWave: 6,
  },
  phase: {
    id: 'phase', name: 'Phasenwandler', role: 'Zeitweiser Schutz', icon: '👻', color: 0x80e0e0, size: 0.34,
    description: 'Wird alle 6 s für 2 s immun gegen physischen Schaden (kündigt sich durch Flackern an).', counter: 'Magische Schadensarten; Resonator unterbricht die Phase.',
    hp: 62, speed: 1.3, reward: 6, threat: 2.5, leak: 1, flying: false, resist: { arcane: -0.2 }, shieldPct: 0, regenPct: 0,
    abilities: ['phaseShift'], route: 'normal', minWave: 7,
  },
  saboteur: {
    id: 'saboteur', name: 'Funkenfresser', role: 'Netzwerk-Saboteur', icon: '🔌', color: 0xff7040, size: 0.34,
    description: 'Lädt sich neben vernetzten Türmen auf und stört deren Netzwerkverbindung für 6 s.', counter: 'Resonator unterbricht das Aufladen. Schnell ausschalten.',
    hp: 58, speed: 1.4, reward: 6, threat: 2.4, leak: 1, flying: false, resist: { lightning: 0.5 }, shieldPct: 0, regenPct: 0,
    abilities: ['netBite'], route: 'normal', minWave: 8,
  },
  scout: {
    id: 'scout', name: 'Pfadspäher', role: 'Routenwähler', icon: '🧭', color: 0xd0d070, size: 0.3,
    description: 'Wählt im Labyrinth die Route mit der geringsten Turmabdeckung.', counter: 'Gleichmäßige Abdeckung des Labyrinths, Rissbinder.',
    hp: 52, speed: 1.6, reward: 5, threat: 2, leak: 1, flying: false, resist: {}, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'safe', minWave: 6,
  },
  broodmother: {
    id: 'broodmother', name: 'Brutmutter', role: 'Beschwörerin', icon: '🕷️', color: 0x905070, size: 0.5,
    description: 'Setzt beim Tod 4 Brutlinge frei.', counter: 'Flächenschaden in der Nähe ihres Todespunkts.',
    hp: 170, speed: 0.8, reward: 10, threat: 5, leak: 2, flying: false, resist: { poison: 0.3 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', onDeathSpawn: { enemy: 'broodling', count: 4 }, minWave: 9,
  },
  broodling: {
    id: 'broodling', name: 'Brutling', role: 'Nachwuchs', icon: '🕸️', color: 0xb07090, size: 0.2,
    description: 'Klein und schnell.', counter: 'Flächenschaden.',
    hp: 16, speed: 1.9, reward: 1, threat: 0.5, leak: 1, flying: false, resist: {}, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 99,
  },
  golem: {
    id: 'golem', name: 'Höhlengolem', role: 'Zerfallender Koloss', icon: '🗿', color: 0x7a7066, size: 0.55,
    description: 'Schwer und zäh. Zerfällt beim Tod in drei Golemsplitter.', counter: 'Flächenschaden dort, wo er fällt; Panzerbrecher.',
    hp: 260, speed: 0.65, reward: 12, threat: 6, leak: 3, flying: false, resist: { physical: 0.3, poison: 0.4 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', onDeathSpawn: { enemy: 'golemling', count: 3 }, contactDps: 30, minWave: 11,
  },
  golemling: {
    id: 'golemling', name: 'Golemsplitter', role: 'Splitter', icon: '🪨', color: 0x9a9088, size: 0.28,
    description: 'Ein Bruchstück eines Höhlengolems.', counter: 'Flächenschaden.',
    hp: 55, speed: 1.2, reward: 2, threat: 1, leak: 1, flying: false, resist: { physical: 0.2 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', contactDps: 8, minWave: 99,
  },
  stalker: {
    id: 'stalker', name: 'Schattenschleicher', role: 'Getarnt', icon: '🌫️', color: 0x403858, size: 0.34, stealth: true,
    description: 'Unsichtbar und nicht angreifbar, bis ein Detektor ihn aufdeckt (Resonator, Runenweber, Totemschrein in voller Reichweite). Andere Türme und Helden erkennen ihn nur aus nächster Nähe.', counter: 'Detektor-Türme entlang des Wegs platzieren.',
    hp: 70, speed: 1.5, reward: 8, threat: 3, leak: 2, flying: false, resist: { arcane: -0.2 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', contactDps: 18, minWave: 9,
  },
  siege: {
    id: 'siege', name: 'Belagerungsbestie', role: 'Turmbrecher', icon: '🦏', color: 0x8a4a3a, size: 0.55,
    description: 'Rammt Türme am Wegesrand (angekündigt) und setzt sie 4 s außer Gefecht.', counter: 'Resonatoren unterbrechen den Ansturm; Schmiedemeister repariert.',
    hp: 220, speed: 0.8, reward: 12, threat: 5.5, leak: 3, flying: false, resist: { physical: 0.25 }, shieldPct: 0, regenPct: 0,
    abilities: ['siegeSlam'], route: 'normal', contactDps: 40, minWave: 10,
  },
  shaman: {
    id: 'shaman', name: 'Heilerschamane', role: 'Heiler', icon: '🪬', color: 0x5ac08a, size: 0.36,
    description: 'Heilt alle Gegner in der Nähe regelmäßig um 12 % ihrer Lebenspunkte.', counter: 'Priorisieren („Wenigste LP“, Fernkampf), Schall unterbricht.',
    hp: 80, speed: 1.0, reward: 9, threat: 3.5, leak: 1, flying: false, resist: { poison: 0.2 }, shieldPct: 0, regenPct: 0,
    abilities: ['heal'], route: 'normal', contactDps: 10, minWave: 8,
  },
  tunneler: {
    id: 'tunneler', name: 'Tunnelgräber', role: 'Untergräber', icon: '🐛', color: 0xa08a50, size: 0.38, burrow: true,
    description: 'Gräbt sich ein (nicht angreifbar, schneller) und taucht nach etwa sieben Feldern wieder auf.', counter: 'Verteidigung nahe dem Kern; Resonatoren und Wächter der Stille zwingen ihn an die Oberfläche.',
    hp: 120, speed: 1.1, reward: 9, threat: 3.5, leak: 2, flying: false, resist: { physical: 0.15 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', contactDps: 15, minWave: 12,
  },
  griffin: {
    id: 'griffin', name: 'Sturmgreif', role: 'Schneller Flieger', icon: '🦅', color: 0xc0b0f0, size: 0.4, evasion: 0.12,
    description: 'Sehr schneller Flieger, weicht 12 % der Geschosse aus (keine Ketten, Strahlen oder Pulse).', counter: 'Flakbatterie und Himmelswacht (reine Flugabwehr, verlangsamen), Sturmrufer, Tesla, Kristalllaser, Drachenhorst, Frostwächter.',
    hp: 105, speed: 1.8, reward: 9, threat: 3.5, leak: 2, flying: true, resist: { lightning: -0.2 }, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', contactDps: 0, minWave: 13,
  },
  boss: {
    id: 'boss', name: 'Boss', role: 'Boss', icon: '👑', color: 0xffffff, size: 0.8,
    description: 'Platzhalterdefinition – Werte stammen aus BOSSES.', counter: '',
    hp: 1, speed: 0.5, reward: 0, threat: 0, leak: 10, flying: false, resist: {}, shieldPct: 0, regenPct: 0,
    abilities: [], route: 'normal', minWave: 99,
  },
};

/** Gegner, die der Wellendirektor regulär einplanen darf. */
export const WAVE_ENEMIES: EnemyId[] = ['runner', 'swarm', 'brute', 'regen', 'flyer', 'shield', 'phase', 'saboteur', 'scout', 'broodmother', 'shaman', 'stalker', 'siege', 'golem', 'tunneler', 'griffin'];

/** Kontaktschaden je Sekunde gegen Helden (Standard, falls nicht definiert). */
export function contactDpsOf(def: EnemyDefinition): number {
  return def.contactDps ?? 6 + def.threat * 4;
}

export const ELITE = {
  hpMul: 3,
  rewardMul: 4,
  leakMul: 3,
  resistBonus: 0.1,
  sizeMul: 1.35,
  ability: 'quake' as AbilityId,
};

export const ABILITIES: Record<AbilityId, AbilityDefinition> = {
  quake: { id: 'quake', name: 'Erdstoß', description: 'Elite stampft neben dicht stehenden Türmen (≥3 im Umkreis) auf und betäubt sie 3 s.', windup: 2, cooldown: 9, interruptible: true },
  netBite: { id: 'netBite', name: 'Netzbiss', description: 'Stört einen vernetzten Turm in der Nähe für 6 s.', windup: 1.5, cooldown: 7, interruptible: true },
  phaseShift: { id: 'phaseShift', name: 'Phasenverschiebung', description: '2 s Immunität gegen physischen Schaden.', windup: 0.8, cooldown: 6, interruptible: true },
  riftJump: { id: 'riftJump', name: 'Rissprung', description: 'Der Koloss reißt den Raum auf und springt 4 Zellen nach vorn. Ziel wird markiert.', windup: 2.2, cooldown: 9, interruptible: true },
  riftPortals: { id: 'riftPortals', name: 'Risskorridor', description: 'Öffnet für 12 s ein Portalpaar: Gegner im Eingang erscheinen weiter vorn.', windup: 3, cooldown: 18, interruptible: true },
  summonBrood: { id: 'summonBrood', name: 'Aschenbrut', description: 'Beschwört 4 Flinklinge.', windup: 1.5, cooldown: 8, interruptible: true },
  fireZones: { id: 'fireZones', name: 'Feuerzonen', description: 'Markiert Turmgruppen; nach 3 s sind Türme darin 5 s lang verbrannt (inaktiv). Frostwächter dämpfen die Zone.', windup: 3, cooldown: 12, interruptible: false },
  ashFury: { id: 'ashFury', name: 'Aschenwut', description: 'Die Königin wird 40 % schneller und beschwört häufiger.', windup: 0, cooldown: 999, interruptible: false },
  nullPulse: { id: 'nullPulse', name: 'Nullpuls', description: 'Stört 6 s lang Netzwerkverbindungen aller Türme im Radius 4.', windup: 2.5, cooldown: 8, interruptible: true },
  siegeSlam: { id: 'siegeSlam', name: 'Ansturm', description: 'Rammt einen Turm in der Nähe; er ist 4 s außer Gefecht.', windup: 1.6, cooldown: 7, interruptible: true },
  heal: { id: 'heal', name: 'Heilritual', description: 'Heilt Gegner im Umkreis von 2,5 Feldern um 12 % ihrer LP.', windup: 1.2, cooldown: 5, interruptible: true },
  devour: { id: 'devour', name: 'Verschlingen', description: 'Verschlingt einen Turm in der Nähe. Besiegst du den Schlund, kehrt der Turm zurück.', windup: 3, cooldown: 14, interruptible: true },
  stomp: { id: 'stomp', name: 'Weltenstampfer', description: 'Betäubt alle Türme im Umkreis von 2,8 Feldern für 4 s.', windup: 2.6, cooldown: 11, interruptible: true },
  twinBond: { id: 'twinBond', name: 'Zwillingsbund', description: 'Stehen die Zwillinge nah beieinander, heilen sie sich gegenseitig.', windup: 0, cooldown: 999, interruptible: false },
  reShield: { id: 'reShield', name: 'Nullerneuerung', description: 'Einmalig: Der Nullhüter erneuert einen Schild (20 % LP).', windup: 2, cooldown: 999, interruptible: true },
};

export const BOSSES: Record<BossId, BossDefinition> = {
  riftColossus: {
    id: 'riftColossus', name: 'Der Risskoloss', title: 'Wandler zwischen den Rissen', color: 0x8a40e0, size: 0.95,
    description: 'Springt durch Risse nach vorn und öffnet Portalkorridore.',
    counter: 'Resonatoren und Frost brechen seine Standfestigkeit und unterbrechen Sprünge. Rissbinder-Fessel verstärkt Schaden.',
    hp: 2600, speed: 0.55, resist: { arcane: 0.3, physical: 0.15 }, poise: 120, reward: 150, leak: 12, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Rissprünge', abilities: ['riftJump'], speedMul: 1 },
      { hpBelow: 0.55, name: 'Phase 2 – Risskorridor', abilities: ['riftJump', 'riftPortals'], speedMul: 1.15 },
    ],
  },
  ashQueen: {
    id: 'ashQueen', name: 'Die Aschenkönigin', title: 'Herrin der Glut', color: 0xff5020, size: 0.85,
    description: 'Beschwört Flinklinge und setzt angekündigte Feuerzonen auf Turmgruppen.',
    counter: 'Frost-Schaden (Schwäche), Frostwächter dämpfen Feuerzonen. Türme nicht zu dicht stellen.',
    hp: 2200, speed: 0.65, resist: { fire: 0.7, frost: -0.3, poison: 0.2 }, poise: 100, reward: 150, leak: 12, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Aschenbrut', abilities: ['summonBrood'], speedMul: 1 },
      { hpBelow: 0.6, name: 'Phase 2 – Feuerzonen', abilities: ['summonBrood', 'fireZones'], speedMul: 1 },
      { hpBelow: 0.25, name: 'Phase 3 – Aschenwut', abilities: ['ashFury', 'summonBrood', 'fireZones'], speedMul: 1.4 },
    ],
  },
  nullWarden: {
    id: 'nullWarden', name: 'Der Nullhüter', title: 'Stille zwischen den Welten', color: 0x40c0d0, size: 0.9,
    description: 'Ein Nullschild schluckt physischen Schaden. Danach stört er Netzwerke mit Nullpulsen und erneuert einmalig seinen Schild.',
    counter: 'Arkaner und Schallschaden brechen den Schild (×1,5 / ×1,0, physisch nur ×0,25). Resonatoren unterbrechen Nullpulse.',
    hp: 2400, speed: 0.6, resist: { lightning: 0.25 }, poise: 110, reward: 160, leak: 12, shieldPct: 0.45,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Nullschild', abilities: [], speedMul: 1 },
      { hpBelow: 1.0, name: 'Phase 2 – Nullpuls', abilities: ['nullPulse'], speedMul: 1.1, trigger: 'shieldBroken' },
      { hpBelow: 0.3, name: 'Phase 3 – Nullerneuerung', abilities: ['reShield', 'nullPulse'], speedMul: 1.1 },
    ],
  },
  maw: {
    id: 'maw', name: 'Der Schlund der Tiefe', title: 'Hunger aus dem Abgrund', color: 0x6a3a5a, size: 1.0,
    description: 'Verschlingt Türme am Wegesrand. Besiegst du ihn, kehren alle verschlungenen Türme zurück.',
    counter: 'Resonatoren unterbrechen das Verschlingen. Wenige, starke Türme statt vieler am Weg; Gift und Feuer gegen die Brut.',
    hp: 3000, speed: 0.5, resist: { physical: 0.15, arcane: 0.1 }, poise: 120, reward: 170, leak: 12, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Hunger', abilities: ['devour'], speedMul: 1 },
      { hpBelow: 0.5, name: 'Phase 2 – Fressrausch', abilities: ['devour', 'summonBrood'], speedMul: 1.25 },
    ],
  },
  twinSol: {
    id: 'twinSol', name: 'Sol, der Glühende Zwilling', title: 'Zwillingswächter', color: 0xffa040, size: 0.8, partner: 'twinLuna',
    description: 'Erscheint mit Luna. Stehen beide nah beieinander, heilen sie sich gegenseitig. Fällt einer, rast der andere.',
    counter: 'Trennen! Rissbinder und Gravitationsbrunnen ziehen sie auseinander. Frost gegen Sol, Feuer gegen Luna.',
    hp: 1500, speed: 0.6, resist: { fire: 0.6, frost: -0.3 }, poise: 90, reward: 110, leak: 8, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Bund der Zwillinge', abilities: ['twinBond'], speedMul: 1 },
      { hpBelow: 0.35, name: 'Phase 2 – Sonnenzorn', abilities: ['twinBond', 'fireZones'], speedMul: 1.15 },
    ],
  },
  twinLuna: {
    id: 'twinLuna', name: 'Luna, die Kalte Zwillingin', title: 'Zwillingswächter', color: 0x80b0ff, size: 0.8, partner: 'twinSol',
    description: 'Erscheint mit Sol. Stehen beide nah beieinander, heilen sie sich gegenseitig. Fällt einer, rast der andere.',
    counter: 'Trennen! Feuer gegen Luna, Frost gegen Sol.',
    hp: 1500, speed: 0.6, resist: { frost: 0.6, fire: -0.3 }, poise: 90, reward: 110, leak: 8, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Bund der Zwillinge', abilities: ['twinBond'], speedMul: 1 },
      { hpBelow: 0.35, name: 'Phase 2 – Mondfrost', abilities: ['twinBond', 'nullPulse'], speedMul: 1.15 },
    ],
  },
  worldbreaker: {
    id: 'worldbreaker', name: 'Der Weltenbrecher', title: 'Koloss aus vielen Welten', color: 0x5a6a8a, size: 1.15, weakPoints: 3,
    description: 'Ein mehrteiliger Riese. Drei leuchtende Schwachpunkte schützen den Kern (Kern nimmt nur 10 % Schaden). Danach liegt der Kern frei.',
    counter: 'Hoher Einzelschaden (Armbrust, Leerenlanze, Laser) auf die Schwachpunkte; Resonatoren unterbrechen den Stampfer.',
    hp: 4200, speed: 0.42, resist: { fire: 0.2, poison: 0.2 }, poise: 160, reward: 240, leak: 20, shieldPct: 0,
    phases: [
      { hpBelow: 1.0, name: 'Phase 1 – Gepanzerter Koloss', abilities: ['stomp'], speedMul: 1 },
      { hpBelow: 1.0, name: 'Phase 2 – Kern freigelegt', abilities: ['stomp', 'summonBrood'], speedMul: 1.2, trigger: 'shieldBroken' },
      { hpBelow: 0.3, name: 'Phase 3 – Weltenbruch', abilities: ['stomp', 'summonBrood', 'riftJump'], speedMul: 1.35 },
    ],
  },
};

export const BOSS_ORDER: BossId[] = ['ashQueen', 'nullWarden', 'riftColossus'];
/** Bosse, die erst in späteren Wellen bzw. großen Partien auftreten. */
export const LATE_BOSS_ORDER: BossId[] = ['maw', 'twinSol', 'worldbreaker'];
/** Bosse, die als eigenständige Wellenbosse eingeplant werden dürfen (Luna kommt nur mit Sol). */
export const PLANNABLE_BOSSES: BossId[] = ['ashQueen', 'nullWarden', 'riftColossus', 'maw', 'twinSol', 'worldbreaker'];

export interface MutationDefinition { id: MutationId; name: string; description: string; icon: string; color: number }
export const MUTATIONS: Record<MutationId, MutationDefinition> = {
  armored: { id: 'armored', name: 'Gepanzert', description: '+25 % auf alle Resistenzen.', icon: '⛨', color: 0xb0b8c8 },
  hasty: { id: 'hasty', name: 'Hastig', description: '+35 % Tempo.', icon: '»', color: 0xffe060 },
  explosive: { id: 'explosive', name: 'Explosiv', description: 'Explodiert beim Tod und betäubt Türme im Umkreis von 1,6 Feldern für 2 s.', icon: '✸', color: 0xff6030 },
  splitting: { id: 'splitting', name: 'Spaltend', description: 'Teilt sich beim Tod in zwei kleinere Kopien.', icon: '⧉', color: 0xc080ff },
  vampiric: { id: 'vampiric', name: 'Vampirisch', description: 'Regeneriert 2,5 % LP pro Sekunde (Gift/Brand stoppt das).', icon: '♥', color: 0xd03050 },
} as Record<MutationId, MutationDefinition>;
export const MUTATION_IDS: MutationId[] = ['armored', 'hasty', 'explosive', 'splitting', 'vampiric'];

/** Schadensfaktoren gegen den Nullschild. */
export const NULL_SHIELD_MUL: Record<string, number> = {
  physical: 0.25, fire: 0.5, frost: 0.5, lightning: 0.5, poison: 0.35, arcane: 1.5, sonic: 1.0,
};
