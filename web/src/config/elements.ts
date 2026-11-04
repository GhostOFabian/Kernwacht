import type { ElementReaction, ResonanceDefinition, StatusEffectDefinition, StatusId } from '../core/types.ts';

// Statuseffekte und Elementarreaktionen.
// Kombinationsregeln:
//  * Brand: kein Stapeln – stärkster DPS-Wert gewinnt, Dauer wird erneuert.
//  * Kälte: bis zu 3 Stapel, Gesamtverlangsamung durch chillMax des Turms begrenzt (hart max. 75 %).
//  * Gift: stapelbar bis poisonMaxStacks, jeder neue Treffer erneuert die Dauer.
//  * Markierung: stärkster Wert gilt, kein Stapeln.
//  * Einfrieren: danach 3 s Einfrier-Immunität (verhindert Dauerkontrolle).
// Reaktionsschaden löst niemals weitere Reaktionen aus; jede Reaktion besitzt eine
// Abklingzeit pro Gegner. Dadurch sind Endlosketten ausgeschlossen.

export const STATUS_DEFS: Record<StatusId, StatusEffectDefinition> = {
  burn: { id: 'burn', name: 'Brennend', description: 'Erleidet Feuerschaden über Zeit. Stoppt Regeneration.', icon: '🔥', color: 0xff7030, maxStacks: 1 },
  chill: { id: 'chill', name: 'Unterkühlt', description: 'Verlangsamt (stapelbar bis 3).', icon: '❄', color: 0x80d0ff, maxStacks: 3 },
  freeze: { id: 'freeze', name: 'Eingefroren', description: 'Kann sich nicht bewegen und keine Fähigkeiten wirken.', icon: '🧊', color: 0xc0f0ff, maxStacks: 1 },
  freezeImmune: { id: 'freezeImmune', name: 'Aufgetaut', description: 'Kurzzeitig immun gegen Einfrieren.', icon: '·', color: 0x6090a0, maxStacks: 1 },
  wet: { id: 'wet', name: 'Nass', description: 'Erleidet +50 % Blitzschaden (Überladung).', icon: '💧', color: 0x4080ff, maxStacks: 1 },
  poison: { id: 'poison', name: 'Vergiftet', description: 'Giftschaden je Stapel. Stoppt Regeneration.', icon: '☠', color: 0x90e040, maxStacks: 8 },
  mark: { id: 'mark', name: 'Runenmarkiert', description: 'Alle Resistenzen gesenkt.', icon: '✦', color: 0xc080ff, maxStacks: 1 },
  brittle: { id: 'brittle', name: 'Spröde', description: '+30 % physischer Schaden.', icon: '◇', color: 0xd0d0d0, maxStacks: 1 },
  silence: { id: 'silence', name: 'Verstummt', description: 'Kann keine Fähigkeiten wirken.', icon: '🔇', color: 0x60e0d0, maxStacks: 1 },
  riftMark: { id: 'riftMark', name: 'Rissmal', description: 'Wird gleich zurückversetzt.', icon: '◎', color: 0xe060e0, maxStacks: 1 },
  root: { id: 'root', name: 'Gefesselt', description: 'Bewegungsunfähig.', icon: '⛓', color: 0xe060e0, maxStacks: 1 },
  stun: { id: 'stun', name: 'Betäubt', description: 'Bewegungsunfähig, keine Fähigkeiten.', icon: '✷', color: 0xffff80, maxStacks: 1 },
  tether: { id: 'tether', name: 'Rissfessel', description: 'Erleidet zusätzlichen Schaden.', icon: '⚯', color: 0xff60ff, maxStacks: 1 },
  reveal: { id: 'reveal', name: 'Aufgedeckt', description: 'Getarnt oder eingegraben, aber sichtbar und angreifbar.', icon: '👁', color: 0xffffff, maxStacks: 1 },
  voidMark: { id: 'voidMark', name: 'Leerenmal', description: 'Erleidet zusätzlichen Schaden.', icon: '✧', color: 0xa060ff, maxStacks: 1 },
  pull: { id: 'pull', name: 'Gravitation', description: 'Wird zurückgezogen und verlangsamt.', icon: '◉', color: 0x8a6aff, maxStacks: 1 },
  shred: { id: 'shred', name: 'Rüstungsbruch', description: 'Blutet (physischer Schaden je Stapel) und verliert physische Resistenz je Stapel. Stoppt Regeneration.', icon: '🩸', color: 0xd04040, maxStacks: 8 },
  weather: { id: 'weather', name: 'Wetter', description: 'Vom Geländeereignis betroffen.', icon: '☁', color: 0xa0a0c0, maxStacks: 1 },
};

export const REACTIONS: ElementReaction[] = [
  { id: 'steam', name: 'Dampfstoß', trigger: 'fire', requires: 'chill', cooldown: 1.0,
    description: 'Feuer auf unterkühlte Ziele: Kälte verdampft, +40 % Trefferschaden, Ziel wird 5 s nass.' },
  { id: 'thermalShock', name: 'Thermoschock', trigger: 'frost', requires: 'burn', cooldown: 1.5,
    description: 'Frost auf brennende Ziele: löscht den Brand, verursacht den verbleibenden Brandschaden sofort und macht spröde.' },
  { id: 'overload', name: 'Überladung', trigger: 'lightning', requires: 'wet', cooldown: 0,
    description: 'Blitz auf nasse Ziele: +50 % Schaden und +1 Kettensprung.' },
  { id: 'toxicBlaze', name: 'Giftbrand', trigger: 'fire', requires: 'poison', cooldown: 2.0,
    description: 'Feuer auf vergiftete Ziele: das Gift explodiert und trifft Gegner im Umkreis von 1,2 Zellen.' },
  { id: 'shatter', name: 'Resonanzbruch', trigger: 'sonic', requires: 'freeze', cooldown: 1.0,
    description: 'Schall auf gefrorene Ziele: zerschmettert das Eis, +100 % Schaden plus 6 % der max. LP.' },
];

export const REACTION_BY_ID: Record<string, ElementReaction> = Object.fromEntries(REACTIONS.map((r) => [r.id, r]));

export const STATUS_RULES = {
  chillHardCap: 0.75,
  freezeImmuneDur: 3,
  wetDur: 5,
  brittleBonus: 0.3,
  overloadMul: 1.5,
  steamMul: 1.4,
  shatterMul: 2.0,
  shatterMaxHpPct: 0.06,
  toxicBlazeRadius: 1.2,
  markHardCap: 0.6,
  bossTetherPct: 0.15,
  poisonSlowCap: 0.3,
  /** Rüstungsbruch senkt die physische Resistenz höchstens bis zu diesem Wert (leicht negativ = verwundbar). */
  shredFloor: -0.2,
};

// Netzwerkresonanzen: Wenn alle geforderten Turmtypen im selben aktiven Netzwerk sind,
// erhalten alle Mitglieder des Netzwerks die Modifikatoren.
export const RESONANCES: ResonanceDefinition[] = [
  { id: 'elemental', name: 'Elementarresonanz', requires: ['ember', 'frost', 'storm'],
    description: 'Feuer, Frost und Blitz im selben Netz: +20 % Schaden, +10 % Statusdauer.',
    mods: [{ stat: 'damageMul', mul: 1.2 }, { stat: 'burnDur', mul: 1.1 }, { stat: 'chillDur', mul: 1.1 }] },
  { id: 'arcaneCircle', name: 'Arkankreis', requires: ['rune', 'riftbinder', 'resonator'],
    description: 'Rune, Rissbinder und Resonator: Markierungen +50 % länger, Unterbrechung +50 %, Reichweite +0,3.',
    mods: [{ stat: 'markDur', mul: 1.5 }, { stat: 'interrupt', mul: 1.5 }, { stat: 'range', add: 0.3 }] },
  { id: 'siegeLink', name: 'Belagerungsverbund', requires: ['ballista', 'cannon', 'ranger'],
    description: 'Armbrust, Kanone und Waldläufer: +20 % Panzerbrechung, +10 % Tempo.',
    mods: [{ stat: 'armorPierce', add: 0.2 }, { stat: 'attackSpeedMul', mul: 1.1 }] },
  { id: 'blight', name: 'Fäulnisnetz', requires: ['spore', 'ember', 'rune'],
    description: 'Sporen, Glut und Rune: Gift und Brand +25 % stärker.',
    mods: [{ stat: 'poisonDps', mul: 1.25 }, { stat: 'burnDps', mul: 1.25 }] },
  { id: 'cosmic', name: 'Kosmische Konvergenz', requires: ['meteor', 'gravity', 'laser'],
    description: 'Meteor, Gravitation und Laser: +25 % Schaden, +0,3 Reichweite.',
    mods: [{ stat: 'damageMul', mul: 1.25 }, { stat: 'range', add: 0.3 }] },
  { id: 'dragonfire', name: 'Drachenglut', requires: ['dragon', 'ember', 'meteor'],
    description: 'Drache, Glut und Meteor: Brand +40 %, +10 % Tempo.',
    mods: [{ stat: 'burnDps', mul: 1.4 }, { stat: 'attackSpeedMul', mul: 1.1 }] },
  { id: 'stormgrid', name: 'Gewitternetz', requires: ['tesla', 'storm', 'pylon'],
    description: 'Tesla, Sturmrufer und Pylon: Lichtbogen +50 %, +1 Kettensprung.',
    mods: [{ stat: 'arcDps', mul: 1.5 }, { stat: 'chains', add: 1 }] },
];

/** Maximale Pylonverbindungen eines Nicht-Pylon-Turms (verhindert Mehrfachstapelung). */
export const TOWER_MAX_PYLON_LINKS = 2;
