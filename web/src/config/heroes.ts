import type { HeroDefinition, HeroId, HeroStats } from '../core/types.ts';

// Heldenkonfiguration. Helden werden mit Rissenergie (nicht Gold) auf dem Weg eingesetzt,
// bleiben eine begrenzte Zeit aktiv und benötigen danach eine Abklingzeit.

export const HERO_RULES = {
  maxEnergy: 100,
  startEnergy: 40,
  energyPerKill: 0.6,
  energyPerElite: 3,
  energyPerBoss: 12,
  energyPerReaction: 0.8,
  energyPerWave: 12,
  cooldown: 30,          // Sekunden Kampfzeit nach regulärem Rückzug
  downCooldown: 50,      // nach einem Ausfall
  retreatRefund: 0.35,   // Anteil der Restzeit, der die Abklingzeit verkürzt
  regenPct: 0.02,        // LP-Regeneration pro Sekunde ohne Kontakt
  xpPerLevel: [0, 60, 150, 280, 450],
  xpRadius: 3.2,
  maxLevel: 5,
  ultPerHit: 2.5,
  ultPerKill: 6,
  ultPerSecond: 1.2,
  loadoutSize: 2,
  duoRadius: 4.5,
};

const S = (o: Partial<HeroStats>): HeroStats => ({
  damage: 0, range: 0.9, attackCd: 1, maxHp: 400, speed: 1.6, auraRadius: 0, auraPower: 0, abilityCdMul: 1,
  activeTime: 40, blockCount: 0, goldBonus: 0, armor: 0, ...o,
});

export const HEROES: Record<HeroId, HeroDefinition> = {
  blade: {
    id: 'blade', name: 'Klingenwächterin Kaela', title: 'Schild der Lane', role: 'Blocker', support: false, icon: '🛡️',
    description: 'Hält bis zu drei Bodengegner fest und zieht ihre Aufmerksamkeit auf sich.', damageType: 'physical', cost: 35,
    base: S({ damage: 22, range: 0.9, attackCd: 0.8, maxHp: 900, blockCount: 3, armor: 0.25 }),
    abilities: [
      { id: 'taunt', name: 'Spottschrei', description: 'Hält alle Bodengegner im Umkreis von 3 Feldern 1,5 s fest.', cooldown: 12 },
      { id: 'whirl', name: 'Wirbelklinge', description: '70 Schaden an allen Gegnern im Umkreis von 1,6 Feldern.', cooldown: 8 },
    ],
    ultimate: { id: 'bulwark', name: 'Bollwerk', description: '6 s unverwundbar, alle Gegner im Umkreis von 3 Feldern werden 4 s festgehalten.', cooldown: 0 },
    perks: [
      [{ id: 'b2a', name: 'Turmschild', description: '+35 % LP', mul: { maxHp: 1.35 } }, { id: 'b2b', name: 'Scharfe Klinge', description: '+40 % Schaden', mul: { damage: 1.4 } }],
      [{ id: 'b3a', name: 'Phalanx', description: '+2 geblockte Gegner', add: { blockCount: 2 } }, { id: 'b3b', name: 'Kampfrausch', description: '+30 % Angriffstempo', mul: { attackCd: 0.77 } }],
      [{ id: 'b4a', name: 'Eisenhaut', description: '+20 % Rüstung', add: { armor: 0.2 } }, { id: 'b4b', name: 'Veteranin', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }],
      [{ id: 'b5a', name: 'Unbeugsam', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'b5b', name: 'Klingensturm', description: '+60 % Schaden', mul: { damage: 1.6 } }],
    ],
    color: 0x5a7ac0, accent: 0xe0e8ff, unlock: 'start',
  },
  hunter: {
    id: 'hunter', name: 'Aschenjäger Varo', title: 'Flammenpfeil', role: 'Fernkampf', support: false, icon: '🏹',
    description: 'Fernkämpfer mit Feuerpfeilen, die Ziele in Brand setzen.', damageType: 'fire', cost: 35,
    base: S({ damage: 18, range: 3.6, attackCd: 0.7, maxHp: 420 }),
    abilities: [
      { id: 'flameArrows', name: 'Brandpfeile', description: '6 s: Pfeile explodieren (Radius 1).', cooldown: 14 },
      { id: 'trap', name: 'Glutfalle', description: 'Legt eine Feuerzone auf den Weg (6 s Brand).', cooldown: 10 },
    ],
    ultimate: { id: 'firerain', name: 'Feuerregen', description: '8 Feuerbrocken prasseln auf Gegner in Reichweite (je 80 Schaden).', cooldown: 0 },
    perks: [
      [{ id: 'h2a', name: 'Adlerauge', description: '+0,8 Reichweite', add: { range: 0.8 } }, { id: 'h2b', name: 'Glühende Spitzen', description: '+40 % Schaden', mul: { damage: 1.4 } }],
      [{ id: 'h3a', name: 'Schnellschuss', description: '+30 % Angriffstempo', mul: { attackCd: 0.77 } }, { id: 'h3b', name: 'Lederwams', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'h4a', name: 'Jagdfieber', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'h4b', name: 'Ausdauer', description: '+12 s Einsatzzeit', add: { activeTime: 12 } }],
      [{ id: 'h5a', name: 'Meisterschütze', description: '+70 % Schaden', mul: { damage: 1.7 } }, { id: 'h5b', name: 'Weitschuss', description: '+1,2 Reichweite', add: { range: 1.2 } }],
    ],
    color: 0xc0502a, accent: 0xffc060, unlock: 'm2',
  },
  riftwalker: {
    id: 'riftwalker', name: 'Risswandler Nyx', title: 'Klinge zwischen den Welten', role: 'Assassine', support: false, icon: '🗡️',
    description: 'Springt durch Risse zu Gegnern nahe dem Kern und erledigt Durchbrecher.', damageType: 'arcane', cost: 40,
    base: S({ damage: 34, range: 1.0, attackCd: 0.85, maxHp: 520, speed: 2.4, armor: 0.1 }),
    abilities: [
      { id: 'riftstep', name: 'Rissschritt', description: 'Teleport zum Gegner, der dem Kern am nächsten ist: 90 Schaden.', cooldown: 7 },
      { id: 'voidmark', name: 'Leerenmal', description: 'Gegner im Umkreis von 2 Feldern erleiden 5 s +30 % Schaden.', cooldown: 11 },
    ],
    ultimate: { id: 'execute', name: 'Hinrichtung', description: 'Die 5 Gegner nächst am Kern verlieren 30 % ihrer max. LP (Bosse 6 %).', cooldown: 0 },
    perks: [
      [{ id: 'r2a', name: 'Leerenklinge', description: '+40 % Schaden', mul: { damage: 1.4 } }, { id: 'r2b', name: 'Schattenhaut', description: '+20 % Rüstung', add: { armor: 0.2 } }],
      [{ id: 'r3a', name: 'Rissrhythmus', description: 'Fähigkeiten −30 % Abklingzeit', mul: { abilityCdMul: 0.7 } }, { id: 'r3b', name: 'Flinke Hände', description: '+30 % Angriffstempo', mul: { attackCd: 0.77 } }],
      [{ id: 'r4a', name: 'Zähigkeit', description: '+45 % LP', mul: { maxHp: 1.45 } }, { id: 'r4b', name: 'Jagdgeschwindigkeit', description: '+40 % Lauftempo', mul: { speed: 1.4 } }],
      [{ id: 'r5a', name: 'Todesbote', description: '+80 % Schaden', mul: { damage: 1.8 } }, { id: 'r5b', name: 'Ewige Wacht', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }],
    ],
    color: 0x6a3aa0, accent: 0xd080ff, unlock: 'm5',
  },
  bard: {
    id: 'bard', name: 'Runenbardin Liora', title: 'Stimme der Resonanz', role: 'Unterstützung', support: true, icon: '🎵',
    description: 'Ihre Aura beschleunigt Türme. Ihre Lieder verstärken ganze Netzwerke.', damageType: 'sonic', cost: 30,
    base: S({ damage: 8, range: 2.6, attackCd: 1.2, maxHp: 380, auraRadius: 3, auraPower: 0.2 }),
    abilities: [
      { id: 'resoSong', name: 'Lied der Resonanz', description: '8 s doppelter Netzwerkbonus für alle Netze.', cooldown: 18 },
      { id: 'inspire', name: 'Inspiration', description: 'Türme im Umkreis von 3 Feldern: 6 s +30 % Schaden.', cooldown: 12 },
    ],
    ultimate: { id: 'hymn', name: 'Hymne der Welten', description: 'Alle Türme: 8 s +50 % Angriffstempo.', cooldown: 0 },
    perks: [
      [{ id: 'd2a', name: 'Weiter Klang', description: 'Aura +1 Feld', add: { auraRadius: 1 } }, { id: 'd2b', name: 'Kraftvolle Stimme', description: 'Aura +10 %', add: { auraPower: 0.1 } }],
      [{ id: 'd3a', name: 'Refrain', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'd3b', name: 'Bühnenpräsenz', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'd4a', name: 'Zugabe', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'd4b', name: 'Chorleiterin', description: 'Aura +10 %', add: { auraPower: 0.1 } }],
      [{ id: 'd5a', name: 'Meisterlied', description: 'Aura +15 %', add: { auraPower: 0.15 } }, { id: 'd5b', name: 'Echo', description: 'Aura +1,5 Felder', add: { auraRadius: 1.5 } }],
    ],
    color: 0x3aa0a0, accent: 0xa0fff0, unlock: 'start',
  },
  smith: {
    id: 'smith', name: 'Schmiedemeister Thorgrim', title: 'Hüter der Türme', role: 'Reparatur', support: true, icon: '🔨',
    description: 'Repariert betäubte, verbrannte und gestörte Türme in seiner Nähe und schmiedet sie kurzzeitig stärker.', damageType: 'physical', cost: 30,
    base: S({ damage: 16, range: 0.9, attackCd: 1.1, maxHp: 700, armor: 0.2, auraRadius: 3, auraPower: 0 }),
    abilities: [
      { id: 'forge', name: 'Feldschmiede', description: 'Der nächste Turm erhält 20 s die Werte einer höheren Stufe (+35 % Schaden, +0,5 Reichweite).', cooldown: 14 },
      { id: 'repairWave', name: 'Reparaturwelle', description: 'Beseitigt Betäubung und Störung aller Türme im Umkreis von 5 Feldern.', cooldown: 16 },
    ],
    ultimate: { id: 'masterwork', name: 'Meisterwerk', description: 'Alle Türme im Umkreis von 4 Feldern: 15 s geschmiedet.', cooldown: 0 },
    perks: [
      [{ id: 's2a', name: 'Weite Werkstatt', description: 'Reparaturradius +1', add: { auraRadius: 1 } }, { id: 's2b', name: 'Schwerer Hammer', description: '+60 % Schaden', mul: { damage: 1.6 } }],
      [{ id: 's3a', name: 'Fleiß', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 's3b', name: 'Kettenhemd', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 's4a', name: 'Lange Schicht', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 's4b', name: 'Amboss', description: 'blockt 2 Gegner', add: { blockCount: 2 } }],
      [{ id: 's5a', name: 'Großmeister', description: 'Fähigkeiten −30 % Abklingzeit', mul: { abilityCdMul: 0.7 } }, { id: 's5b', name: 'Bergkönig', description: '+20 % Rüstung', add: { armor: 0.2 } }],
    ],
    color: 0x8a6a4a, accent: 0xffb060, unlock: 'start',
  },
  weather: {
    id: 'weather', name: 'Wettermacherin Sena', title: 'Herrin der Stürme', role: 'Elementar', support: true, icon: '🌦️',
    description: 'Ruft Regen und Frost herbei und ermöglicht so mächtige Elementarreaktionen.', damageType: 'frost', cost: 35,
    base: S({ damage: 10, range: 3, attackCd: 1.0, maxHp: 400 }),
    abilities: [
      { id: 'rain', name: 'Regenguss', description: 'Alle Gegner werden 6 s nass (Blitz +50 %).', cooldown: 15 },
      { id: 'frostBreath', name: 'Frosthauch', description: 'Gegner im Umkreis von 4 Feldern werden stark unterkühlt.', cooldown: 10 },
    ],
    ultimate: { id: 'thunderstorm', name: 'Gewittersturm', description: '12 Blitzeinschläge à 70 Schaden auf zufällige Gegner, alle werden nass.', cooldown: 0 },
    perks: [
      [{ id: 'w2a', name: 'Sturmauge', description: '+0,8 Reichweite', add: { range: 0.8 } }, { id: 'w2b', name: 'Hagel', description: '+60 % Schaden', mul: { damage: 1.6 } }],
      [{ id: 'w3a', name: 'Wetterwechsel', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'w3b', name: 'Nebelmantel', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'w4a', name: 'Langer Sturm', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'w4b', name: 'Kältewelle', description: '+30 % Angriffstempo', mul: { attackCd: 0.77 } }],
      [{ id: 'w5a', name: 'Monsun', description: 'Fähigkeiten −30 % Abklingzeit', mul: { abilityCdMul: 0.7 } }, { id: 'w5b', name: 'Blitzableiterin', description: '+80 % Schaden', mul: { damage: 1.8 } }],
    ],
    color: 0x4a7ad0, accent: 0xc0e8ff, unlock: 'm3',
  },
  mystic: {
    id: 'mystic', name: 'Leylinien-Mystiker Oran', title: 'Wandelnder Knoten', role: 'Netzwerk', support: true, icon: '🔯',
    description: 'Wirkt wie ein wandelnder Pylon: Türme in seiner Nähe erhalten Netzwerkkraft. Kann Leylinien erschaffen.', damageType: 'arcane', cost: 35,
    base: S({ damage: 12, range: 2.8, attackCd: 1.0, maxHp: 420, auraRadius: 2.8, auraPower: 0.15 }),
    abilities: [
      { id: 'leyNode', name: 'Ley-Knoten', description: '30 s: Türme im Umkreis von 2 Feldern +15 % Schaden (wie Leylinie).', cooldown: 20 },
      { id: 'leyBeam', name: 'Leystrom', description: 'Arkaner Strahl: 140 Schaden an allen Gegnern in einer Linie.', cooldown: 9 },
    ],
    ultimate: { id: 'worldweb', name: 'Weltennetz', description: 'Alle Türme: 10 s +25 % Schaden.', cooldown: 0 },
    perks: [
      [{ id: 'm2a', name: 'Weites Netz', description: 'Aura +1 Feld', add: { auraRadius: 1 } }, { id: 'm2b', name: 'Starker Strom', description: 'Aura +8 %', add: { auraPower: 0.08 } }],
      [{ id: 'm3a', name: 'Meditation', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'm3b', name: 'Arkaner Fokus', description: '+60 % Schaden', mul: { damage: 1.6 } }],
      [{ id: 'm4a', name: 'Ausdauer', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'm4b', name: 'Runenschild', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'm5a', name: 'Weltenweber', description: 'Aura +12 %', add: { auraPower: 0.12 } }, { id: 'm5b', name: 'Allnetz', description: 'Aura +1,5 Felder', add: { auraRadius: 1.5 } }],
    ],
    color: 0x7a4ad0, accent: 0xc0a0ff, unlock: 'm6',
  },
  prospector: {
    id: 'prospector', name: 'Prospektorin Mira', title: 'Glücksgräberin', role: 'Wirtschaft', support: true, icon: '💰',
    description: 'Abschüsse in ihrer Nähe bringen mehr Gold. Findet einmal pro Welle einen Schatz.', damageType: 'physical', cost: 25,
    base: S({ damage: 8, range: 2.4, attackCd: 1.1, maxHp: 360, auraRadius: 3, goldBonus: 0.5 }),
    abilities: [
      { id: 'treasure', name: 'Schatzfund', description: 'Einmal pro Welle: 40 + 4 × Welle Gold.', cooldown: 6 },
      { id: 'goldFever', name: 'Goldfieber', description: '8 s: alle Abschüsse +100 % Gold.', cooldown: 25 },
    ],
    ultimate: { id: 'motherlode', name: 'Mutterader', description: 'Sofort 120 + 8 × Welle Gold.', cooldown: 0 },
    perks: [
      [{ id: 'p2a', name: 'Spürnase', description: 'Goldbonus +25 %', add: { goldBonus: 0.25 } }, { id: 'p2b', name: 'Weiter Blick', description: 'Aura +1 Feld', add: { auraRadius: 1 } }],
      [{ id: 'p3a', name: 'Geschäftssinn', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'p3b', name: 'Dicke Börse', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'p4a', name: 'Überstunden', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'p4b', name: 'Glückspilz', description: 'Goldbonus +25 %', add: { goldBonus: 0.25 } }],
      [{ id: 'p5a', name: 'Goldregen', description: 'Goldbonus +40 %', add: { goldBonus: 0.4 } }, { id: 'p5b', name: 'Handelsimperium', description: 'Aura +1,5 Felder', add: { auraRadius: 1.5 } }],
    ],
    color: 0xc0a030, accent: 0xfff0a0, unlock: 'm4',
  },
  silence: {
    id: 'silence', name: 'Wächter der Stille Ommar', title: 'Der Schweigende', role: 'Kontrolle', support: true, icon: '🔕',
    description: 'Gegner in seiner Aura können keine Fähigkeiten wirken; Bosse verlieren dort Standfestigkeit. Deckt Getarnte auf.', damageType: 'sonic', cost: 40,
    base: S({ damage: 12, range: 1.0, attackCd: 1.0, maxHp: 650, armor: 0.15, auraRadius: 2.5, auraPower: 12 }),
    abilities: [
      { id: 'silenceBan', name: 'Schweigebann', description: 'Bricht eine angekündigte Bossfähigkeit im Umkreis von 6 Feldern sofort ab.', cooldown: 16 },
      { id: 'unveil', name: 'Enthüllung', description: 'Deckt 8 s alle Getarnten und Eingegrabenen im Umkreis von 5 Feldern auf.', cooldown: 12 },
    ],
    ultimate: { id: 'absoluteSilence', name: 'Absolute Stille', description: 'Alle Gegner 6 s verstummt, Bosse verlieren sämtliche Standfestigkeit.', cooldown: 0 },
    perks: [
      [{ id: 'q2a', name: 'Weite Stille', description: 'Aura +1 Feld', add: { auraRadius: 1 } }, { id: 'q2b', name: 'Tiefe Stille', description: 'Standfestigkeitsentzug +8/s', add: { auraPower: 8 } }],
      [{ id: 'q3a', name: 'Askese', description: 'Fähigkeiten −25 % Abklingzeit', mul: { abilityCdMul: 0.75 } }, { id: 'q3b', name: 'Steinruhe', description: '+40 % LP', mul: { maxHp: 1.4 } }],
      [{ id: 'q4a', name: 'Lange Wache', description: '+15 s Einsatzzeit', add: { activeTime: 15 } }, { id: 'q4b', name: 'Wehrhaft', description: 'blockt 2 Gegner', add: { blockCount: 2 } }],
      [{ id: 'q5a', name: 'Leere', description: 'Standfestigkeitsentzug +12/s', add: { auraPower: 12 } }, { id: 'q5b', name: 'Stille Weite', description: 'Aura +1,5 Felder', add: { auraRadius: 1.5 } }],
    ],
    color: 0x2a5a6a, accent: 0x80e0f0, unlock: 'm8',
  },
};

export const HERO_ORDER: HeroId[] = ['blade', 'hunter', 'riftwalker', 'bard', 'smith', 'weather', 'mystic', 'prospector', 'silence'];

export interface DuoDefinition { id: string; name: string; a: HeroId; b: HeroId; description: string }

/** Duo-Synergien: gelten, solange beide Helden aktiv und nah beieinander sind. */
export const DUOS: DuoDefinition[] = [
  { id: 'stormsong', name: 'Sturmlied', a: 'bard', b: 'weather', description: 'Blitztürme +40 % Schaden.' },
  { id: 'ironwatch', name: 'Eiserne Wacht', a: 'blade', b: 'smith', description: 'Kaela erleidet 50 % weniger Schaden, Türme in ihrer Nähe +15 % Schaden.' },
  { id: 'emberley', name: 'Glutleylinie', a: 'hunter', b: 'mystic', description: 'Varos Schaden +50 %, seine Pfeile treffen in einem Radius.' },
  { id: 'voidpact', name: 'Leerenpakt', a: 'riftwalker', b: 'silence', description: 'Ultimate-Aufladung beider Helden doppelt so schnell.' },
  { id: 'goldenbard', name: 'Goldene Ballade', a: 'bard', b: 'prospector', description: 'Goldbonus +50 %.' },
];

/** Helden, die von Beginn an verfügbar sind; weitere werden in der Kampagne freigeschaltet. */
export function isHeroUnlocked(id: HeroId, completedMissions: readonly string[]): boolean {
  const u = HEROES[id].unlock;
  return u === 'start' || completedMissions.includes(u);
}

export function heroStats(def: HeroDefinition, perks: readonly number[]): HeroStats {
  const st: HeroStats = { ...def.base };
  perks.forEach((choice, i) => {
    const p = def.perks[i]?.[choice];
    if (!p) return;
    for (const [k, v] of Object.entries(p.add ?? {})) st[k as keyof HeroStats] += v as number;
    for (const [k, v] of Object.entries(p.mul ?? {})) st[k as keyof HeroStats] *= v as number;
  });
  return st;
}
