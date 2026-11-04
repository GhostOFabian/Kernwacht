import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHAMPIONS } from '../src/config/champions.ts';
import { PARAGON, paragonXpToNext, sanitizeParagon } from '../src/config/paragon.ts';
import { runeSockets } from '../src/config/runes.ts';
import { type ChampionId, type GameState } from '../src/core/types.ts';
import { commitHeroProgress } from '../src/app/progress.ts';
import { IsoCamera } from '../src/render/Camera.ts';
import { defaultSave, sanitizeSave } from '../src/save/SaveSystem.ts';
import { championStats, grantXp, paragonFree, skillPointsForLevel } from '../src/sim/ChampionSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { onEnemyLoot } from '../src/sim/LootSystem.ts';
import { bagRunes, dropRune, towerRunes } from '../src/sim/RuneSystem.ts';
import { killEnemy } from '../src/sim/DamageSystem.ts';
import { addEnemy, buildNearPath } from './helpers.ts';

type Meta = NonNullable<Parameters<typeof GameSimulation.create>[0]['heroEndless']>['meta'];

function heSim(champion: ChampionId = 'lyra', meta: Partial<Meta> = {}, seed = 'V27') {
  return GameSimulation.create({
    mode: 'heroEndless', seedText: seed, variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion, startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {}, ...meta }, runId: `v27-${seed}` },
  });
}

// ------------------------------------------------------------ Dauerhafte Heldenstufe & Paragon

test('Heldenstufe und Skillbaum werden aus dem dauerhaften Stand übernommen (inkl. Kernwall)', () => {
  const base = heSim('lyra');
  const core = CHAMPIONS.lyra.tree.find((n) => n.coreLives)!;
  const other = CHAMPIONS.lyra.tree.find((n) => !n.coreLives && !n.requires && !n.perk && !n.minLevel)!;
  const sim = heSim('lyra', { champ: { level: 50, xp: 123, tree: { [core.id]: 2, [other.id]: 3, unbekannt: 4 } } });
  const c = sim.s.he!.champion;
  assert.equal(c.level, 50);
  assert.equal(c.xp, 123);
  assert.equal(c.tree[core.id], 2);
  assert.equal(c.tree.unbekannt, undefined);
  assert.equal(c.skillPoints, skillPointsForLevel(50) - 5);
  assert.equal(sim.s.maxLives, base.s.maxLives + 2 * core.coreLives!);
  assert.ok(championStats(sim.s.he!).maxHp > championStats(base.s.he!).maxHp);
});

test('Heldenstufe endet bei 99, danach fließen EP ins Paragon (max. 500, 1 Punkt je Stufe)', () => {
  const sim = heSim('lyra', { champ: { level: 98, xp: 0, tree: {} } });
  const he = sim.s.he!;
  grantXp(sim, 1e7);
  assert.equal(he.champion.level, PARAGON.heroMaxLevel);
  assert.ok(he.paragon!.level >= 1, `Paragon ${he.paragon!.level}`);
  assert.equal(paragonFree(he), he.paragon!.level + 10, 'Paragonstufen + 10 Vorab-Punkte (Stufe 50–95)');
  grantXp(sim, 1e12);
  assert.equal(he.champion.level, 99);
  assert.ok(he.paragon!.level <= PARAGON.maxLevel);
  // sehr viel EP → Deckel
  for (let i = 0; i < 200; i++) grantXp(sim, 1e9);
  assert.equal(he.paragon!.level, PARAGON.maxLevel);
  assert.equal(he.paragon!.xp, 0);
  assert.ok(paragonXpToNext(10) > paragonXpToNext(0));
});

test('Paragonpunkte: drei Bäume, Wirkung sofort, Bollwerk erhöht Kernenergie, Zurücksetzen in der Bauphase', () => {
  const sim = heSim('brann', { champ: { level: 99, xp: 0, tree: {} }, paragon: { level: 12, xp: 0, points: {} } });
  const he = sim.s.he!;
  assert.equal(paragonFree(he), 22);
  const hp0 = championStats(he).maxHp;
  const dmg0 = championStats(he).damage;
  for (let i = 0; i < 4; i++) assert.ok(sim.paragonPoint('pg_vital').ok);
  for (let i = 0; i < 4; i++) assert.ok(sim.paragonPoint('pg_might').ok);
  const lives = sim.s.maxLives;
  assert.ok(sim.paragonPoint('pg_core').ok);
  assert.equal(sim.s.maxLives, lives + 2, '3.8: Bollwerk +2 Kernenergie je Punkt');
  assert.ok(championStats(he).maxHp > hp0);
  assert.ok(championStats(he).damage > dmg0);
  assert.equal(paragonFree(he), 13);
  assert.equal(sim.paragonPoint('nope').ok, false);
  assert.ok(sim.paragonReset().ok);
  assert.equal(paragonFree(he), 22);
  assert.equal(sim.s.maxLives, lives);
});

test('Paragon unter Stufe 99 nicht verfügbar; Fortschritt wird dauerhaft und ohne Rückschritt gespeichert', () => {
  const sim = heSim('lyra');
  assert.equal(sim.paragonPoint('pg_might').ok, false);
  const save = defaultSave();
  const he = sim.s.he!;
  he.champion.level = 40;
  he.champion.xp = 10;
  he.paragon = { level: 5, xp: 100, points: { pg_haste: 2 }, rev: 1 };
  commitHeroProgress(save, sim.s as GameState);
  const m = save.progress.heroMeta;
  assert.equal(m.champions.lyra?.level, 40);
  assert.equal(m.paragon.level, 5);
  // Älterer Stand überschreibt nicht
  he.champion.level = 30;
  he.paragon.level = 3;
  commitHeroProgress(save, sim.s as GameState);
  assert.equal(m.champions.lyra?.level, 40);
  assert.equal(m.paragon.level, 5);
  // Bereinigung beim Laden
  const raw = JSON.parse(JSON.stringify(save));
  raw.progress.heroMeta.champions.lyra.level = 500;
  raw.progress.heroMeta.paragon = { level: 2, xp: 1e12, points: { pg_might: 50, pg_vital: 50 } };
  const clean = sanitizeSave(raw).progress.heroMeta;
  assert.equal(clean.champions.lyra?.level, 99);
  assert.deepEqual(clean.paragon.points, { pg_might: 12 }, 'Budget = Paragonstufe 2 + 10 Vorab-Punkte (gemeinsame Stufe 99)');
  assert.deepEqual(sanitizeParagon(null), { level: 0, xp: 0, points: {} });
});

// ------------------------------------------------------------ Runen

test('Runen: Splitter ab Welle 100, volle Runen ab Welle 250', () => {
  const sim = heSim('lyra', {}, 'RUNE-A');
  sim.s.wave = 99;
  for (let i = 0; i < 300; i++) {
    const e = addEnemy(sim, 'brute');
    e.miniBoss = true;
    onEnemyLoot(sim, e);
  }
  assert.equal(sim.s.he!.runes?.length ?? 0, 0);
  assert.equal(dropRune(sim, 1, 1), null);
  sim.s.wave = 180;
  for (let i = 0; i < 40; i++) dropRune(sim, 1, 1);
  assert.ok((sim.s.he!.runes ?? []).length >= 30);
  assert.ok((sim.s.he!.runes ?? []).every((r) => r.tier === 1), 'vor Welle 250 nur Splitter');
  sim.s.he!.runes = [];
  sim.s.wave = 260;
  let n = 0;
  for (let i = 0; i < 50; i++) {
    const e = addEnemy(sim, 'brute');
    e.miniBoss = true;
    onEnemyLoot(sim, e);
    n = sim.s.he!.runes?.length ?? 0;
  }
  assert.ok(n > 5, `Runen ab Welle 250: ${n}`);
});

test('Runen je Turmsorte (3.3): Sockel nach Welle, gilt für alle Türme der Sorte, Verkauf behält Runen, Gierrune', () => {
  const sim = heSim('lyra', {}, 'RUNE-B');
  sim.s.gold = 1e5;
  sim.s.wave = 90;
  const t = buildNearPath(sim, 'ranger');
  const t2 = buildNearPath(sim, 'ranger', 3);
  const he = sim.s.he!;
  he.runes = [{ uid: sim.uid(), rune: 'might', tier: 3, tower: 0 }, { uid: sim.uid(), rune: 'greed', tier: 1, tower: 0 }, { uid: sim.uid(), rune: 'reach', tier: 1, tower: 0 }];
  assert.equal(runeSockets('ranger', 90), 0);
  assert.equal(sim.socketRune(t.uid, he.runes[0].uid).ok, false, 'vor Welle 100 kein Sockel');
  sim.s.wave = 100;
  assert.equal(runeSockets('ranger', 100), 1);
  assert.equal(runeSockets('thornwood', 100), 2, 'legendär +1');
  assert.equal(runeSockets('ranger', 250), 2);
  assert.equal(runeSockets('ranger', 400), 3);
  const dmg = sim.stats(t2).damage;
  assert.ok(sim.socketRune(t.uid, he.runes[0].uid).ok);
  assert.ok(sim.stats(t2).damage > dmg * 1.3, 'Uralte Kraftrune wirkt auch auf den anderen Waldläufer');
  assert.equal(sim.socketRune(t2.uid, he.runes[1].uid).ok, false, 'Sorte hat nur 1 Sockel auf Welle 100');
  sim.s.wave = 250;
  assert.ok(sim.socketRune(t2.uid, he.runes[1].uid).ok);
  assert.equal(towerRunes(sim, t).length, 2);
  assert.equal(towerRunes(sim, t2).length, 2);
  // Gierrune
  const e = addEnemy(sim, 'runner');
  const g = sim.s.gold;
  killEnemy(sim, e, t.uid);
  assert.ok(sim.s.gold - g >= 2, 'Kopfgeld inkl. Gierrune');
  // Herausnehmen
  assert.ok(sim.unsocketRune(he.runes[1].uid).ok);
  assert.equal(towerRunes(sim, t2).length, 1);
  // Verkauf: Runen bleiben bei der Sorte
  assert.ok(sim.sell(t.uid).ok);
  assert.ok(sim.sell(t2.uid).ok);
  assert.equal(bagRunes(sim).length, 2);
  const t3 = buildNearPath(sim, 'ranger');
  assert.equal(towerRunes(sim, t3).length, 1, 'neuer Waldläufer hat die Rune der Sorte sofort');
  // Altstand (Rune an einzelnem Turm) wird übernommen
  he.runes.push({ uid: sim.uid(), rune: 'haste', tier: 2, tower: t3.uid });
  assert.equal(towerRunes(sim, t3).length, 2);
  assert.ok(he.runes.every((r) => r.tower === 0));
});

// ------------------------------------------------------------ Kamera

test('Kamera frei drehbar: Gier beliebig, Neigung begrenzt, Zurücksetzen', () => {
  const cam = new IsoCamera();
  cam.resize(800, 600);
  cam.setMap(20, 14);
  const y0 = cam.yaw;
  cam.orbit(0.7, 0);
  assert.ok(Math.abs(cam.yaw - y0 - 0.7) < 1e-9);
  cam.orbit(0, 5);
  for (let i = 0; i < 100; i++) cam.update(0.05);
  assert.ok(cam.pitch <= (80 * Math.PI) / 180 + 1e-6);
  cam.orbit(0, -10);
  for (let i = 0; i < 100; i++) cam.update(0.05);
  assert.ok(cam.pitch >= (17 * Math.PI) / 180 - 1e-6);
  cam.resetView();
  for (let i = 0; i < 100; i++) cam.update(0.05);
  assert.ok(Math.abs(cam.yaw - y0) < 1e-3);
  assert.ok(Math.abs(cam.pitch - Math.atan(1 / Math.SQRT2)) < 1e-3);
});
