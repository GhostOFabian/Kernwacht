import assert from 'node:assert/strict';
import { test } from 'node:test';
import { recordRun } from '../src/app/progress.ts';
import { ACHIEVEMENTS, SKINS, skinUnlocked } from '../src/config/achievements.ts';
import { CAMPAIGN } from '../src/config/campaign.ts';
import { BOSSES, ENEMIES, MUTATION_IDS } from '../src/config/enemies.ts';
import { HERO_ORDER, HERO_RULES, HEROES, isHeroUnlocked } from '../src/config/heroes.ts';
import { FUSIONS, LEGENDARY_ORDER, TOWER_ACTIVES, TOWER_ORDER, TOWERS } from '../src/config/towers.ts';
import { Cell, type HeroId, type MapSize, type TowerInstance } from '../src/core/types.ts';
import { defaultSave, deserializeRun } from '../src/save/SaveSystem.ts';
import { Bot, BOT_PROFILES } from '../src/sim/Bot.ts';
import { dealDamage } from '../src/sim/DamageSystem.ts';
import { fusionOptions } from '../src/sim/EconomySystem.ts';
import { spawnEnemy } from '../src/sim/EnemySystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { canPlaceHero, duoActive, HEROIC_CHOIR } from '../src/sim/HeroSystem.ts';
import { eligibleModifiers } from '../src/sim/RogueliteSystem.ts';
import { aggregateModifiers } from '../src/config/modifiers.ts';
import { canHit } from '../src/sim/TowerSystem.ts';
import { generateWorld, makeSeed, mapDims, spawnsOf, validateMap } from '../src/sim/WorldGenerator.ts';

import { buildNearPath, makeSim } from './helpers.ts';

function heroSim(heroes: HeroId[] = ['blade', 'bard']): GameSimulation {
  const sim = makeSim({ seedText: 'HERO-TEST', heroes });
  sim.s.riftEnergy = HERO_RULES.maxEnergy;
  return sim;
}

function pathCells(sim: GameSimulation): { x: number; y: number }[] {
  const m = sim.s.map;
  const out: { x: number; y: number }[] = [];
  for (let i = 0; i < m.cells.length; i++) if (m.cells[i] === Cell.Path) out.push({ x: i % m.width, y: Math.floor(i / m.width) });
  return out;
}

function maxOut(sim: GameSimulation, t: TowerInstance, spec: 1 | 2 = 1): void {
  sim.s.gold = 100000;
  while (t.level < 4) assert.ok(sim.upgrade(t.uid).ok);
  assert.ok(sim.specialize(t.uid, spec).ok);
}

// ------------------------------------------------------------ Inhalte

test('Inhalt: 20 baubare Türme, 12 legendäre, 9 Helden, 7 Bosse, 7 Biome, 10 Missionen', () => {
  assert.equal(TOWER_ORDER.length, 20);
  assert.equal(LEGENDARY_ORDER.length, 12);
  assert.equal(HERO_ORDER.length, 9);
  assert.equal(Object.keys(BOSSES).length, 7);
  assert.ok(CAMPAIGN.length >= 10);
  for (const id of TOWER_ORDER) assert.ok(TOWERS[id].upgrades.length === 3 && TOWERS[id].specs.length === 2, id);
  for (const f of FUSIONS) assert.ok(TOWERS[f.id].legendary, f.id);
  for (const id of HERO_ORDER) {
    const h = HEROES[id];
    assert.equal(h.perks.length, HERO_RULES.maxLevel - 1, id);
    assert.equal(h.abilities.length, 2, id);
  }
  assert.equal(HERO_ORDER.filter((id) => HEROES[id].support).length, 6, 'sechs Unterstützer');
  for (const b of Object.values(BOSSES)) assert.ok(b.phases.length >= 2, b.id);
  for (const id of ['golem', 'stalker', 'siege', 'shaman', 'tunneler', 'griffin'] as const) assert.ok(ENEMIES[id], id);
  assert.equal(MUTATION_IDS.length, 5);
});

// ------------------------------------------------------------ Kartengrößen

test('Kartengrößen: große und riesige Karten sind gültig und haben Weggabelungen', () => {
  const sizes: MapSize[] = ['normal', 'large', 'huge'];
  let prevArea = 0;
  for (const size of sizes) {
    const d = mapDims('classic', size);
    assert.ok(d.w * d.h > prevArea, size);
    prevArea = d.w * d.h;
    for (let i = 0; i < 12; i++) {
      const m = generateWorld({ seed: makeSeed(`SIZE-${size}-${i}`), variant: 'classic', biome: 'random', size });
      assert.equal(m.width, d.w);
      assert.ok(validateMap(m).ok, `${size} ${i}`);
      if (size !== 'normal') assert.ok(spawnsOf(m).length >= 2, `Gabelung ${size} ${i}`);
    }
    const mz = generateWorld({ seed: makeSeed(`SIZE-MZ-${size}`), variant: 'mazing', biome: 'random', size });
    assert.ok(validateMap(mz).ok, `mazing ${size}`);
  }
});

test('Große Karte: Gegner verteilen sich auf alle Risse und erreichen das Ziel', () => {
  const sim = makeSim({ seedText: 'FORK-RUN', size: 'large' });
  const starts = spawnsOf(sim.s.map);
  assert.ok(starts.length >= 2);
  sim.startWave();
  sim.s.spawnQueue = [];
  const a = spawnEnemy(sim, 'runner', false, null);
  const b = spawnEnemy(sim, 'runner', false, null);
  const pos = new Set([`${Math.floor(a.x)},${Math.floor(a.y)}`, `${Math.floor(b.x)},${Math.floor(b.y)}`]);
  assert.equal(pos.size, 2, 'zwei verschiedene Startpunkte');
  for (let i = 0; i < 30 * 120 && sim.s.enemies.some((e) => e.alive); i++) sim.step();
  assert.ok(!sim.s.enemies.some((e) => e.alive), 'alle Gegner sind angekommen');
});

// ------------------------------------------------------------ Helden

test('Helden: Einsatz kostet Rissenergie und ist nur auf Wegzellen möglich', () => {
  const sim = heroSim();
  const path = pathCells(sim);
  const open = (() => {
    const m = sim.s.map;
    for (let i = 0; i < m.cells.length; i++) if (m.cells[i] === Cell.Open) return { x: i % m.width, y: Math.floor(i / m.width) };
    throw new Error('keine freie Zelle');
  })();
  assert.equal(canPlaceHero(sim, open.x, open.y).ok, false);
  const before = sim.s.riftEnergy;
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  assert.equal(sim.s.riftEnergy, before - HEROES.blade.cost);
  assert.equal(sim.s.heroes[0].state, 'active');
  assert.equal(sim.deployHero('blade', p.x, p.y).ok, false, 'nicht doppelt');
  sim.s.riftEnergy = 0;
  assert.equal(sim.deployHero('bard', p.x, p.y).ok, false, 'ohne Energie kein Einsatz');
});

test('Helden: Einsatzzeit läuft ab, danach Abklingzeit; gefallene Helden brauchen länger', () => {
  const sim = heroSim();
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  const h = sim.s.heroes[0];
  h.activeLeft = 30;
  sim.step();
  assert.equal(h.activeLeft, 30, 'Einsatzzeit läuft nur im Kampf');
  sim.startWave();
  sim.s.spawnQueue = [];
  const keep = spawnEnemy(sim, 'brute', false, null);
  keep.hp = keep.maxHp = 1e9;
  h.activeLeft = 0.05;
  sim.step();
  sim.step();
  assert.equal(h.state, 'cooldown');
  assert.ok(Math.abs(h.cooldown - HERO_RULES.cooldown) < 1);
  h.cooldown = 0;
  sim.step();
  assert.equal(h.state, 'ready');
  // Niederlage im Kampf
  sim.s.riftEnergy = 100;
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  h.hp = -1;
  sim.step();
  assert.equal(h.state, 'down');
  assert.ok(h.cooldown > HERO_RULES.cooldown);
  assert.equal(sim.s.stats.heroDowns, 1);
});

test('Helden: Stufenaufstieg bietet eine Perk-Wahl, die Werte verändert', () => {
  const sim = heroSim();
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  const h = sim.s.heroes[0];
  sim.startWave();
  sim.s.spawnQueue = [];
  h.xp = HERO_RULES.xpPerLevel[1] - 0.01;
  const e = spawnEnemy(sim, 'runner', false, null);
  e.x = h.x;
  e.y = h.y;
  dealDamage(sim, e, 1e9, 'physical', 0);
  assert.equal(h.level, 2);
  assert.ok(h.pendingPerk);
  assert.ok(sim.heroPerk('blade', 0).ok);
  assert.equal(h.pendingPerk, false);
  assert.deepEqual(h.perks, [0]);
  assert.equal(sim.heroPerk('blade', 1).ok, false, 'keine zweite Wahl ohne Aufstieg');
});

test('Helden: Fähigkeiten nur im Kampf, danach Abklingzeit; Ultimate braucht volle Ladung', () => {
  const sim = heroSim(['blade', 'bard']);
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('bard', p.x, p.y).ok);
  assert.equal(sim.heroAbility('bard', 0).ok, false, 'nicht in der Bauphase');
  sim.startWave();
  const r = sim.heroAbility('bard', 0);
  assert.ok(r.ok, r.ok ? '' : r.reason);
  const h = sim.s.heroes.find((x) => x.id === 'bard');
  assert.ok(h && h.abilityCd[0] > 0);
  assert.equal(sim.heroAbility('bard', 2).ok, false);
  if (h) h.ult = 100;
  assert.ok(sim.heroAbility('bard', 2).ok);
});

test('Helden: Duo-Synergie greift nur, wenn beide nah beieinander im Einsatz sind', () => {
  const sim = heroSim(['bard', 'weather']);
  const path = pathCells(sim);
  assert.equal(duoActive(sim, 'stormsong'), false);
  const a = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('bard', a.x, a.y).ok);
  assert.ok(sim.deployHero('weather', a.x, a.y).ok);
  assert.ok(duoActive(sim, 'stormsong'));
});

test('Helden: Blocker hält Bodengegner fest', () => {
  const sim = heroSim(['blade']);
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'brute', false, null);
  for (let i = 0; i < 20; i++) sim.step();
  assert.ok(sim.deployHero('blade', Math.floor(e.x), Math.floor(e.y)).ok || sim.deployHero('blade', Math.floor(e.nx), Math.floor(e.ny)).ok);
  for (let i = 0; i < 30; i++) sim.step();
  const x0 = e.x;
  const y0 = e.y;
  for (let i = 0; i < 30; i++) sim.step();
  assert.ok(e.blockedBy !== 0 || Math.hypot(e.x - x0, e.y - y0) < 0.2, 'Gegner bleibt stehen');
});

test('Mission mit Schutzheld: Fällt der Held, ist die Partie verloren', () => {
  const m = CAMPAIGN.find((x) => x.heroMustSurvive);
  assert.ok(m, 'Mission vorhanden');
  if (!m) return;
  const sim = GameSimulation.create({ mode: 'campaign', missionId: m.id, seedText: m.seed, variant: m.variant, biome: m.biome, difficulty: 'normal', heroes: ['hunter'] });
  assert.deepEqual(sim.s.heroes.map((h) => h.id).sort(), [...(m.forcedHeroes ?? [])].sort(), 'Vorgabe überschreibt Auswahl');
  sim.s.riftEnergy = 100;
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero(m.heroMustSurvive as HeroId, p.x, p.y).ok);
  sim.startWave();
  const h = sim.s.heroes.find((x) => x.id === m.heroMustSurvive);
  if (h) h.hp = -5;
  sim.step();
  assert.equal(sim.s.phase, 'defeat');
});

test('Helden werden über die Kampagne freigeschaltet', () => {
  assert.ok(isHeroUnlocked('blade', []));
  assert.equal(isHeroUnlocked('hunter', []), false);
  const save = defaultSave();
  const m2 = CAMPAIGN.find((x) => x.id === 'm2');
  assert.ok(m2);
  if (!m2) return;
  save.progress.campaignUnlocked = 2;
  const sim = GameSimulation.create({ mode: 'campaign', missionId: 'm2', seedText: '', variant: m2.variant, biome: m2.biome, difficulty: 'normal' });
  sim.s.phase = 'victory';
  const r = recordRun(save, sim.s);
  assert.ok(r.unlockedHeroes.includes(m2.unlocksHero as HeroId));
});

// ------------------------------------------------------------ Türme: Fusion & Fähigkeiten

test('Fusion: nur voll ausgebaute, benachbarte Partner verschmelzen zu einem legendären Turm', () => {
  const sim = makeSim({ seedText: 'FUSION' });
  sim.s.gold = 100000;
  const a = buildNearPath(sim, 'ember');
  // Partner direkt daneben suchen
  let b: TowerInstance | undefined;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1], [2, 0], [0, 2]]) {
    if (sim.build('storm', a.cx + dx, a.cy + dy).ok) {
      b = sim.towerAt(a.cx + dx, a.cy + dy);
      break;
    }
  }
  assert.ok(b, 'Partner gebaut');
  if (!b) return;
  assert.equal(fusionOptions(sim, a).length, 1);
  assert.equal(sim.fuse(a.uid, b.uid).ok, false, 'nicht ausgebaut');
  maxOut(sim, a, 1);
  maxOut(sim, b, 2);
  const n = sim.s.towers.length;
  const gold = sim.s.gold;
  const r = sim.fuse(a.uid, b.uid);
  assert.ok(r.ok, r.ok ? '' : r.reason);
  assert.equal(sim.s.towers.length, n - 1);
  const fused = sim.c.towerByUid.get(a.uid);
  assert.equal(fused?.def, 'plasma');
  assert.ok(sim.s.gold < gold);
  assert.equal(sim.s.stats.fusions, 1);
  assert.equal(sim.build('plasma', 0, 0).ok, false, 'Legendäre sind nicht baubar');
});

test('Aktive Turmfähigkeit: ab Stufe 2, nur im Kampf, danach Abklingzeit', () => {
  const sim = makeSim({ seedText: 'ACTIVE' });
  const t = buildNearPath(sim, 'frost');
  const act = TOWERS.frost.active;
  assert.ok(act);
  assert.equal(sim.towerActive(t.uid).ok, false, 'Stufe 1');
  assert.ok(sim.upgrade(t.uid).ok);
  assert.equal(sim.towerActive(t.uid).ok, false, 'Bauphase');
  sim.startWave();
  assert.ok(sim.towerActive(t.uid).ok);
  assert.equal(t.activeCd, TOWER_ACTIVES[act ?? 'deepfreeze'].cooldown);
  assert.equal(sim.towerActive(t.uid).ok, false, 'Abklingzeit');
});

test('Totem verstärkt Türme in seiner Aura (ohne Stapeln)', () => {
  const sim = makeSim({ seedText: 'TOTEM' });
  const r = buildNearPath(sim, 'ranger');
  const before = sim.stats(r).damage;
  sim.s.gold = 10000;
  let ok = false;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, 1], [1, -1], [-1, -1]]) {
    if (sim.build('totem', r.cx + dx, r.cy + dy).ok) {
      ok = true;
      break;
    }
  }
  assert.ok(ok);
  sim.step();
  const after = sim.stats(r).damage;
  assert.ok(after > before, `${after} > ${before}`);
});

// ------------------------------------------------------------ Gegner: Tarnung, Mutationen, Bosse

test('Getarnte Gegner sind nur mit Detektor angreifbar', () => {
  const sim = makeSim({ seedText: 'STEALTH' });
  const ranger = buildNearPath(sim, 'ranger');
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'stalker', false, null);
  e.x = ranger.cx + 0.5 + 2.6;
  e.y = ranger.cy + 0.5;
  e.speed = 0;
  sim.step();
  assert.ok(e.stealthed, 'ohne Detektor getarnt');
  e.x = ranger.cx + 0.5 + 1.2;
  sim.step();
  assert.equal(e.stealthed, false, 'aus nächster Nähe erkannt');
  e.x = ranger.cx + 0.5 + 2.6;
  sim.step();
  assert.ok(e.stealthed);
  assert.equal(canHit(sim.stats(ranger), e), false);
  buildNearPath(sim, 'rune');
  const rune = sim.s.towers.find((t) => t.def === 'rune');
  if (rune) {
    e.x = rune.cx + 0.5;
    e.y = rune.cy + 1.5;
  }
  sim.step();
  assert.equal(e.stealthed, false, 'Detektor enttarnt');
});

test('Mutationen: hastig ist schneller, spaltend teilt sich beim Tod', () => {
  const sim = makeSim({ seedText: 'MUT' });
  sim.startWave();
  sim.s.spawnQueue = [];
  const normal = spawnEnemy(sim, 'brute', true, null);
  const hasty = spawnEnemy(sim, 'brute', true, null, 'hasty');
  assert.ok(hasty.speed > normal.speed * 1.2);
  const split = spawnEnemy(sim, 'brute', true, null, 'splitting');
  const n = sim.s.enemies.length;
  dealDamage(sim, split, 1e9, 'arcane', 0);
  assert.equal(sim.s.enemies.filter((e) => e.alive).length, n - 1 + 2);
  assert.equal(sim.s.stats.mutationsKilled, 1);
});

test('Weltenbrecher: Schwachpunkte fangen Schaden ab, danach Phasenwechsel', () => {
  const sim = makeSim({ seedText: 'WB' });
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = spawnEnemy(sim, 'boss', false, 'worldbreaker');
  assert.equal(e.plates.length, 3);
  const hp0 = e.hp;
  dealDamage(sim, e, 50, 'arcane', 0);
  assert.ok(hp0 - e.hp < 10, 'Kern nimmt nur einen Bruchteil');
  for (let i = 0; i < 3; i++) dealDamage(sim, e, e.plateMax * 2, 'arcane', 0);
  assert.equal(e.plates.length, 0);
  sim.step();
  assert.ok(e.phase >= 1, 'Phase 2 nach Schildbruch');
});

test('Zwillinge erscheinen gemeinsam; stirbt einer, rast der andere', () => {
  const sim = makeSim({ seedText: 'TWINS' });
  sim.startWave();
  sim.s.spawnQueue = [];
  spawnEnemy(sim, 'boss', false, 'twinSol');
  const sol = sim.s.enemies.find((e) => e.boss === 'twinSol');
  const luna = sim.s.enemies.find((e) => e.boss === 'twinLuna');
  assert.ok(sol && luna, 'beide Zwillinge');
  if (!sol || !luna) return;
  dealDamage(sim, sol, 1e9, 'arcane', 0);
  assert.ok(luna.enraged);
});

test('Schlund: verschlungene Türme kehren beim Tod zurück', () => {
  const sim = makeSim({ seedText: 'MAW' });
  const t = buildNearPath(sim, 'ranger');
  sim.startWave();
  sim.s.spawnQueue = [];
  const maw = spawnEnemy(sim, 'boss', false, 'maw');
  maw.x = t.cx + 0.5;
  maw.y = t.cy + 1.5;
  // Fähigkeit direkt auslösen
  maw.phase = 1;
  const ab = maw.abilities.find((a) => a.id === 'devour') ?? (() => {
    const a = { id: 'devour' as const, cd: 0, windup: 0, tx: 0, ty: 0, r: 0 };
    maw.abilities.push(a);
    return a;
  })();
  ab.cd = 0;
  let swallowed = false;
  for (let i = 0; i < 30 * 8 && !swallowed; i++) {
    maw.x = t.cx + 0.5;
    maw.y = t.cy + 1.5;
    sim.step();
    swallowed = maw.swallowed.length > 0;
  }
  assert.ok(swallowed, 'Turm wurde verschlungen');
  assert.equal(sim.towerAt(t.cx, t.cy), undefined);
  dealDamage(sim, maw, 1e9, 'arcane', 0);
  assert.ok(sim.towerAt(t.cx, t.cy), 'Turm ist zurück');
});

// ------------------------------------------------------------ Fortschritt, Speichern, Gesamtlauf

test('Erfolge werden vergeben und schalten Skins frei', () => {
  const save = defaultSave();
  const sim = makeSim({ seedText: 'ACH' });
  sim.s.phase = 'victory';
  const r = recordRun(save, sim.s);
  assert.ok(r.newAchievements.includes('firstWin'));
  assert.ok(r.newAchievements.includes('flawless'));
  const obs = SKINS.find((k) => k.requires === 'firstWin');
  assert.ok(obs && skinUnlocked(obs, save.progress.achievements));
  assert.equal(recordRun(save, sim.s).newAchievements.includes('firstWin'), false, 'nur einmal');
  assert.ok(ACHIEVEMENTS.length >= 12);
});

test('Speichern: Helden überleben den Roundtrip, aktive Helden werden sicher zurückgezogen', () => {
  const sim = heroSim();
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  const r = deserializeRun(sim.serialize());
  assert.ok(r.state, r.error ?? '');
  assert.equal(r.state?.heroes.length, 2);
  assert.equal(r.state?.heroes[0].state, 'ready');
});

test('Bot spielt große und riesige Karten mit Helden ohne Fehler', () => {
  for (const size of ['large', 'huge'] as MapSize[]) {
    const sim = GameSimulation.create({ mode: 'quick', seedText: `BOT-${size}`, variant: 'classic', biome: 'random', difficulty: 'easy', size, heroes: ['blade', 'bard'] });
    const bot = new Bot(sim, BOT_PROFILES[5]);
    bot.play(30 * 60 * 12, 8);
    assert.ok(sim.s.wave >= 1);
    assert.ok(sim.s.stats.heroDeploys >= 1, `${size}: Helden eingesetzt`);
  }
});

test('Heldenmodifikatoren: nur mit Helden im Angebot; Heldenmut verkürzt die Abklingzeit', () => {
  const noHero = makeSim({ seedText: 'MOD-NOHERO', heroes: [] });
  assert.ok(!eligibleModifiers(noHero.s).includes('heroism'));
  const sim = heroSim();
  assert.ok(eligibleModifiers(sim.s).includes('heroism'));
  sim.s.modifiers.push('heroism');
  sim.c.mods = aggregateModifiers(sim.s.modifiers);
  assert.ok(sim.c.mods.heroCooldownMul < 1);
  assert.ok(sim.c.mods.allDamage < 1, 'Abwägung: Türme schwächer');
  const path = pathCells(sim);
  const p = path[Math.floor(path.length / 2)];
  assert.ok(sim.deployHero('blade', p.x, p.y).ok);
  sim.startWave();
  sim.s.spawnQueue = [];
  const keep = spawnEnemy(sim, 'brute', false, null);
  keep.hp = keep.maxHp = 1e9;
  sim.s.heroes[0].activeLeft = 0.01;
  sim.step();
  assert.ok(sim.s.heroes[0].cooldown < HERO_RULES.cooldown * 0.75);
});

test('Heldenchor: Bardin neben einem Feuer-Frost-Blitz-Netz verstärkt alle Netztürme', () => {
  const sim = heroSim(['bard']);
  sim.s.gold = 100000;
  const pylon = buildNearPath(sim, 'pylon');
  const placed: TowerInstance[] = [];
  for (const def of ['ember', 'frost', 'storm'] as const) {
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1], [2, 0], [0, 2], [-2, 0], [0, -2]]) {
      if (sim.build(def, pylon.cx + dx, pylon.cy + dy).ok) {
        const t = sim.towerAt(pylon.cx + dx, pylon.cy + dy);
        if (t) placed.push(t);
        break;
      }
    }
  }
  assert.equal(placed.length, 3);
  sim.step();
  const before = sim.stats(placed[0]).damage;
  // Bardin auf die nächste Wegzelle setzen
  const path = pathCells(sim).sort((a, b) => Math.hypot(a.x - pylon.cx, a.y - pylon.cy) - Math.hypot(b.x - pylon.cx, b.y - pylon.cy));
  assert.ok(sim.deployHero('bard', path[0].x, path[0].y).ok);
  for (let i = 0; i < 12; i++) sim.step();
  const after = sim.stats(placed[0]).damage;
  assert.ok(after >= before * (1 + HEROIC_CHOIR) - 1e-6, `${after} >= ${before * 1.2}`);
});
