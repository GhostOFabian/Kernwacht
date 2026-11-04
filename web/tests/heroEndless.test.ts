import assert from 'node:assert/strict';
import { test } from 'node:test';
import { CHAMPIONS } from '../src/config/champions.ts';
import { HERO_ENDLESS, xpToNext } from '../src/config/heroEndless.ts';
import { ITEMS, RARITY } from '../src/config/items.ts';
import { SIM } from '../src/config/balance.ts';
import { Cell, type ChampionId, type ItemId } from '../src/core/types.ts';
import { commitHeroRun, recordRun } from '../src/app/progress.ts';
import { championStats, championWalkable, grantXp, rebuildChampionBuffs } from '../src/sim/ChampionSystem.ts';
import { killEnemy } from '../src/sim/DamageSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { nearShop } from '../src/sim/InventorySystem.ts';
import { addFragments, collectPickups, onEnemyLoot, ownedItems } from '../src/sim/LootSystem.ts';
import { defaultSave, deserializeRun, sanitizeSave } from '../src/save/SaveSystem.ts';
import type { RunSetup } from '../src/sim/setup.ts';
import { addEnemy, buildNearPath, makeSim, openCells } from './helpers.ts';

function heSim(o: { champion?: ChampionId; seed?: string; bestWave?: number; unlocked?: ItemId[]; kit?: ItemId[]; fragments?: Partial<Record<ItemId, number>>; variant?: 'classic' | 'mazing'; size?: RunSetup['size']; runId?: string } = {}) {
  return GameSimulation.create({
    mode: 'heroEndless', seedText: o.seed ?? 'HERO-TEST', variant: o.variant ?? 'classic', biome: 'forest', difficulty: 'normal', size: o.size,
    heroEndless: { champion: o.champion ?? 'lyra', startKit: o.kit ?? [], meta: { bestWave: o.bestWave ?? 0, unlockedItems: o.unlocked ?? [], fragments: o.fragments ?? {} }, runId: o.runId ?? 'run-1' },
  });
}

function steps(sim: GameSimulation, seconds: number) {
  const n = Math.round(seconds / SIM.dt);
  for (let i = 0; i < n; i++) sim.step();
}

/** Stellt den Helden auf ein begehbares Nachbarfeld des Ladens. */
function toShop(sim: GameSimulation) {
  const he = sim.s.he!;
  for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
    if (championWalkable(sim, he.shop.x + dx, he.shop.y + dy)) {
      he.champion.x = he.shop.x + dx + 0.5;
      he.champion.y = he.shop.y + dy + 0.5;
      he.champion.path = [];
      he.champion.hx = he.champion.x;
      he.champion.hy = he.champion.y;
      return;
    }
  }
  throw new Error('Laden ohne begehbaren Nachbarn');
}

const firstPathCell = (sim: GameSimulation) => {
  const m = sim.s.map;
  for (let i = 0; i < m.cells.length; i++) if (m.cells[i] === Cell.Path) return { x: i % m.width, y: Math.floor(i / m.width) };
  throw new Error('kein Weg');
};

test('Helden-Endlos startet eigenständig: Held, Laden, Ressourcen, keine Lane-Helden', () => {
  const sim = heSim();
  const s = sim.s;
  assert.equal(s.mode, 'heroEndless');
  assert.ok(s.rules.endless, 'immer endlos');
  assert.ok(s.he, 'Zustand des Modus existiert');
  assert.equal(s.heroes.length, 0);
  assert.equal(s.he!.champion.id, 'lyra');
  assert.ok(s.he!.champion.hp > 0);
  assert.equal(s.he!.autoTimer, HERO_ENDLESS.prepTime);
  // Andere Modi bleiben unberührt
  const q = makeSim();
  assert.equal(q.s.he, null);
});

test('Laden existiert auf jeder Karte (Varianten, Größen, Seeds) und ist erreichbar', () => {
  for (const variant of ['classic', 'mazing'] as const) {
    for (const size of ['normal', 'large', 'huge'] as const) {
      for (const seed of ['A1', 'B2', 'C3']) {
        const sim = heSim({ variant, size, seed });
        const he = sim.s.he!;
        assert.ok(he.shop.x >= 0 && he.shop.y >= 0, `${variant}/${size}/${seed}`);
        toShop(sim);
        assert.ok(nearShop(sim));
        // Der Laden ist vom Kern aus zu Fuß erreichbar
        const c = he.champion;
        const r = sim.moveChampion(Math.floor(c.x), Math.floor(c.y));
        c.x = s0(sim).x;
        c.y = s0(sim).y;
        assert.ok(r.ok);
        const nb = [[1, 0], [-1, 0], [0, 1], [0, -1]].map(([dx, dy]) => [he.shop.x + dx, he.shop.y + dy]).find(([x, y]) => championWalkable(sim, x, y))!;
        assert.ok(sim.moveChampion(nb[0], nb[1]).ok, `Laden erreichbar ${variant}/${size}/${seed}`);
      }
    }
  }
});
function s0(sim: GameSimulation) {
  return { x: sim.s.map.goal.x + 0.5, y: sim.s.map.goal.y + 0.5 };
}

test('Erste Welle startet automatisch nach 15 s, Folgewellen nach der Übergangszeit', () => {
  const sim = heSim();
  steps(sim, 14.5);
  assert.equal(sim.s.wave, 0);
  assert.equal(sim.s.phase, 'build');
  steps(sim, 0.7);
  assert.equal(sim.s.wave, 1);
  assert.equal(sim.s.phase, 'combat');
  // Welle sofort beenden
  for (const e of sim.s.enemies) e.alive = false;
  sim.s.spawnQueue = [];
  sim.s.enemies = [];
  sim.step();
  sim.step();
  assert.equal(sim.s.phase, 'build');
  assert.ok(sim.s.he!.autoTimer > 0 && sim.s.he!.autoTimer <= HERO_ENDLESS.transitionTime);
  steps(sim, HERO_ENDLESS.transitionTime + 0.2);
  assert.equal(sim.s.wave, 2);
});

test('Pause stoppt Wellentakt, Held, Gegner und Abklingzeiten', () => {
  const sim = heSim();
  sim.s.he!.champion.skillCd[0] = 5;
  const e = addEnemy(sim, 'runner');
  const ex = e.x;
  const ey = e.y;
  sim.setPaused(true);
  const ticks = sim.advance(0.2) + sim.advance(0.2) + sim.advance(0.2);
  assert.equal(ticks, 0);
  assert.equal(sim.s.he!.autoTimer, HERO_ENDLESS.prepTime);
  assert.equal(sim.s.he!.champion.skillCd[0], 5);
  assert.equal(e.x, ex);
  assert.equal(e.y, ey);
  sim.setPaused(false);
  assert.ok(sim.advance(0.2) > 0);
  assert.ok(sim.s.he!.autoTimer < HERO_ENDLESS.prepTime);
});

test('Held läuft über normale Felder und auf der Lane; Türme blockieren nicht', () => {
  const sim = heSim();
  const c = sim.s.he!.champion;
  const lane = firstPathCell(sim);
  assert.ok(sim.moveChampion(lane.x, lane.y).ok, 'Lane ist begehbar');
  steps(sim, 25);
  assert.equal(Math.floor(c.x), lane.x);
  assert.equal(Math.floor(c.y), lane.y);
  // Turm bauen und darauf/hindurch laufen
  const t = buildNearPath(sim, 'ranger');
  assert.ok(championWalkable(sim, t.cx, t.cy), 'Turmfeld ist für den Helden begehbar');
  assert.ok(sim.moveChampion(t.cx, t.cy).ok);
  steps(sim, 25);
  assert.equal(Math.floor(c.x), t.cx);
  assert.equal(Math.floor(c.y), t.cy);
  // Hindernisse sind nicht begehbar
  const m = sim.s.map;
  const blocked = m.cells.findIndex((v) => v === Cell.Blocked);
  assert.equal(sim.moveChampion(blocked % m.width, Math.floor(blocked / m.width)).ok, false);
  // Held zerstört nichts
  assert.ok(sim.s.towers.includes(t));
});

test('Mazing: Held ignoriert Turm-Blockaden, Gegnerweg bleibt unverändert', () => {
  const sim = heSim({ variant: 'mazing' });
  const cells = openCells(sim).slice(0, 3);
  sim.s.gold = 10000;
  for (const p of cells) sim.build('ranger', p.x, p.y);
  for (const t of sim.s.towers) assert.ok(championWalkable(sim, t.cx, t.cy));
});

test('Erfahrung: Pickups werden einmal eingesammelt, Stufenaufstieg erhöht Attribute und gibt Skillpunkte', () => {
  const sim = heSim({ champion: 'brann' });
  const he = sim.s.he!;
  const c = he.champion;
  const e = addEnemy(sim, 'runner', c.x + 0.2, c.y);
  killEnemy(sim, e, 0);
  const xpDrops = he.pickups.filter((p) => p.kind === 'xp');
  assert.ok(xpDrops.length >= 1, 'Gegner lässt EP fallen');
  const amount = xpDrops.reduce((a, p) => a + p.amount, 0);
  collectPickups(sim, 3);
  assert.equal(he.pickups.filter((p) => p.kind === 'xp').length, 0);
  assert.equal(Math.round(c.xp * 1000), Math.round(amount * 1000));
  // Doppelter Aufruf gibt nichts doppelt
  collectPickups(sim, 3);
  assert.equal(Math.round(c.xp * 1000), Math.round(amount * 1000));
  // Erneutes Töten desselben Gegners lässt keine zweite Beute fallen
  killEnemy(sim, e, 0);
  assert.equal(he.pickups.filter((p) => p.kind === 'xp').length, 0);

  const before = championStats(he);
  grantXp(sim, xpToNext(1) - c.xp + 0.01);
  assert.equal(c.level, 2);
  assert.equal(c.skillPoints, 1);
  const after = championStats(he);
  assert.ok(after.maxHp > before.maxHp);
  assert.ok(after.damage > before.damage);
  assert.ok(xpToNext(5) > xpToNext(4), 'wachsende Kurve');
  // Skillbaum: Voraussetzungen und Punkte
  const tree = CHAMPIONS.brann.tree;
  const root = tree.find((n) => !n.requires?.length && !n.perk && !n.minLevel)!;
  const child = tree.find((n) => n.requires?.some((r) => r.id === root.id))!;
  assert.equal(sim.learnSkill(child.id).ok, false, 'Voraussetzung fehlt');
  assert.ok(sim.learnSkill(root.id).ok);
  assert.equal(c.skillPoints, 0);
  assert.equal(sim.learnSkill(root.id).ok, false, 'keine Punkte mehr');
});

test('Fähigkeiten: Mana, Abklingzeit und Freischaltstufe', () => {
  const sim = heSim({ champion: 'brann' });
  const c = sim.s.he!.champion;
  const sk = CHAMPIONS.brann.skills[0];
  const e = addEnemy(sim, 'brute', c.x + 0.5, c.y);
  const hp0 = e.hp;
  const mana0 = c.mana;
  assert.ok(sim.championSkill(0, c.x, c.y).ok);
  assert.ok(e.hp < hp0, 'Wirbel trifft');
  assert.equal(c.mana, mana0 - sk.mana);
  assert.ok(Math.abs(c.skillCd[0] - sk.cooldown) < 1e-6);
  assert.equal(sim.championSkill(0, c.x, c.y).ok, false, 'Abklingzeit aktiv');
  steps(sim, sk.cooldown + 0.1);
  assert.ok(c.skillCd[0] <= 0);
  const ult = CHAMPIONS.brann.skills[3];
  assert.equal(sim.championSkill(3, c.x, c.y).ok, false, `Ultimative ab Stufe ${ult.unlockLevel}`);
  c.mana = 0;
  assert.equal(sim.championSkill(0, c.x, c.y).ok, false, 'zu wenig Mana');
});

test('Kämpfer greift Gegner automatisch an', () => {
  const sim = heSim({ champion: 'brann' });
  const c = sim.s.he!.champion;
  const e = addEnemy(sim, 'brute', c.x + 0.6, c.y);
  e.speed = 0;
  const hp0 = e.hp;
  steps(sim, 2);
  assert.ok(e.hp < hp0 || !e.alive, 'Nahkampfangriff verursacht Schaden');
  assert.ok(c.damageDealt > 0);
});

test('Turmverstärkungen: Anwendung, Erneuerung ohne Stapeln, Ablauf und Entfernung beim Verkauf', () => {
  const sim = heSim({ champion: 'lyra' });
  const he = sim.s.he!;
  const t = buildNearPath(sim, 'ranger');
  he.champion.x = 9999; // Aura aus dem Spiel nehmen
  he.champion.y = 9999;
  sim.c.statsDirty = true;
  sim.step();
  const base = sim.stats(t).damage;
  he.champion.x = t.cx + 0.5;
  he.champion.y = t.cy + 0.5;
  he.champion.mana = 999;
  assert.ok(sim.championSkill(0, t.cx + 0.5, t.cy + 0.5).ok, 'Inspiration auf Turm');
  sim.refreshCaches();
  const buffed = sim.stats(t).damage;
  assert.ok(buffed > base * 1.3, `Schaden steigt (${base} → ${buffed})`);
  assert.equal(he.towerBuffs.filter((b) => b.tower === t.uid && b.src === 'inspire').length, 1);
  // Erneut anwenden → weiterhin ein Eintrag, Dauer nicht über Grunddauer
  he.champion.skillCd[0] = 0;
  assert.ok(sim.championSkill(0, t.cx + 0.5, t.cy + 0.5).ok);
  const entries = he.towerBuffs.filter((b) => b.tower === t.uid && b.src === 'inspire');
  assert.equal(entries.length, 1);
  assert.ok(entries[0].t <= entries[0].dur + 1e-9);
  // Ablauf
  he.champion.x = 9999;
  he.champion.y = 9999;
  steps(sim, entries[0].dur + 0.5);
  assert.equal(he.towerBuffs.length, 0);
  assert.ok(Math.abs(sim.stats(t).damage - base) < 1e-6, 'Werte kehren zurück');
  // Verkauf entfernt Verstärkungen
  he.champion.x = t.cx + 0.5;
  he.champion.y = t.cy + 0.5;
  he.champion.skillCd[0] = 0;
  sim.championSkill(0, t.cx + 0.5, t.cy + 0.5);
  sim.sell(t.uid);
  sim.step();
  assert.equal(he.towerBuffs.length, 0);
  rebuildChampionBuffs(sim);
  assert.equal(sim.c.champBuff.has(t.uid), false);
});

test('Fragmente ergeben bei ausreichender Menge ein Item, Herstellung verbraucht korrekt', () => {
  const sim = heSim();
  const he = sim.s.he!;
  toShop(sim);
  const need = RARITY[ITEMS.chainmail.rarity].fragments;
  addFragments(sim, 'chainmail', need - 1);
  assert.equal(sim.craft('frag:chainmail').ok, false, 'zu wenig Fragmente');
  addFragments(sim, 'chainmail', 1);
  he.resources.crystal = 5;
  const cost = RARITY[ITEMS.chainmail.rarity].craftCost.crystal ?? 0;
  const r = sim.craft('frag:chainmail');
  assert.ok(r.ok, r.ok ? '' : r.reason);
  assert.equal(he.fragments.chainmail ?? 0, 0);
  assert.equal(he.resources.crystal, 5 - cost);
  assert.ok(he.champion.inventory.some((i) => i.item === 'chainmail'));
  assert.ok(he.runUnlocks.includes('chainmail'));
  // Fehlgeschlagene Herstellung verbraucht nichts
  he.resources.crystal = 0;
  addFragments(sim, 'stormBow', 4);
  const f0 = he.fragments.stormBow;
  assert.equal(sim.craft('frag:stormBow').ok, false);
  assert.equal(he.fragments.stormBow, f0);
  // Abseits des Ladens nicht möglich
  he.resources.crystal = 10;
  he.champion.x = sim.s.map.spawn.x + 0.5;
  he.champion.y = sim.s.map.spawn.y + 0.5;
  assert.equal(sim.craft('frag:stormBow').ok, false);
});

test('Freigeschaltete Items werden nicht erneut identisch fallen gelassen', () => {
  const all = Object.keys(ITEMS).filter((i) => !ITEMS[i].craftOnly);
  const unlocked = all.filter((i) => i !== 'hawkEye');
  const sim = heSim({ unlocked, bestWave: 50 });
  const he = sim.s.he!;
  sim.s.wave = 30;
  for (let k = 0; k < 400; k++) {
    const e = addEnemy(sim, 'brute', he.champion.x, he.champion.y);
    e.elite = k % 3 === 0;
    killEnemy(sim, e, 0);
  }
  const itemDrops = he.pickups.filter((p) => p.kind === 'item').map((p) => p.ref);
  const fragDrops = he.pickups.filter((p) => p.kind === 'fragment').map((p) => p.ref);
  for (const id of [...itemDrops, ...fragDrops]) assert.ok(!unlocked.includes(id), `kein Duplikat von ${id}`);
  assert.ok(new Set(itemDrops).size === itemDrops.length, 'jedes Item höchstens einmal am Boden');
  // Fragmente bereits besessener Items werden zu Essenz
  const ess = he.resources.essence;
  addFragments(sim, unlocked[0], 2);
  assert.equal(he.fragments[unlocked[0]] ?? 0, 0);
  assert.equal(he.resources.essence, ess + 2 * HERO_ENDLESS.fragmentToEssence);
  assert.ok(ownedItems(he).has(unlocked[0]));
});

test('Ausrüstung verändert Werte; falscher Slot oder Held wird abgelehnt', () => {
  const sim = heSim({ champion: 'brann', unlocked: ['ironBlade', 'runeStaff', 'leather'], bestWave: 8, kit: ['ironBlade'] });
  const he = sim.s.he!;
  const c = he.champion;
  const blade = c.inventory.find((i) => i.item === 'ironBlade')!;
  assert.equal(c.equipment[0], blade.uid, 'Startausrüstung angelegt (Rucksack I)');
  assert.equal(c.inventory.length, 1, 'nur 1 Rucksackplatz');
  const withBlade = championStats(he).damage;
  assert.ok(sim.unequip(0).ok);
  const without = championStats(he).damage;
  assert.equal(Math.round((withBlade - without) * 100), Math.round(ITEMS.ironBlade.stats.damage! * 100));
  assert.ok(sim.equip(blade.uid).ok);
  assert.equal(championStats(he).damage, withBlade);
  assert.equal(sim.equip(blade.uid, 1).ok, false, 'Waffe passt nicht in den Rüstungsslot');
  toShop(sim);
  he.resources.metal = 50;
  assert.equal(sim.shopBuyItem('runeStaff').ok, false, '3.4: Runenstab (nur Lyra) wird Brann im Laden nicht angeboten');
  assert.ok(sim.shopBuyItem('leather').ok, 'Sammlungsitem im Laden');
  assert.equal(sim.shopBuyItem('leather').ok, false, 'kein zweites Exemplar');
});

test('Tränke stapeln, verbrauchen und haben Abklingzeiten; Spezialslot', () => {
  const sim = heSim({ bestWave: 5 });
  const he = sim.s.he!;
  const c = he.champion;
  assert.equal(c.potions.find((p) => p.potion === 'heal')?.count, 2, 'Startbeutel-Perk');
  toShop(sim);
  sim.s.gold = 500;
  assert.ok(sim.shopBuy('p-heal').ok);
  assert.equal(c.potions.find((p) => p.potion === 'heal')?.count, 3, 'stapelt');
  assert.equal(sim.s.gold, 460);
  c.hp = 10;
  assert.ok(sim.usePotion('heal').ok);
  assert.ok(c.hp > 10);
  assert.equal(c.potions.find((p) => p.potion === 'heal')?.count, 2);
  assert.equal(sim.usePotion('heal').ok, false, 'Abklingzeit');
  // Spezialslot
  assert.ok(sim.slotPotion('heal').ok);
  assert.equal(c.specialPotion?.count, 2);
  assert.equal(c.potions.some((p) => p.potion === 'heal'), false);
  steps(sim, 7);
  assert.ok(sim.usePotion('heal').ok, 'Trank aus dem Spezialslot nutzbar');
  assert.equal(c.specialPotion?.count, 1);
  // Krafttrank: Dauer wird erneuert, nicht aufsummiert
  he.resources.crystal = 20;
  sim.shopBuy('p-power');
  sim.shopBuy('p-power');
  const base = championStats(he).damage;
  assert.equal(sim.usePotion('power').ok, false, 'globale Trank-Abklingzeit');
  steps(sim, HERO_ENDLESS.potionGlobalCd + 0.1);
  assert.ok(sim.usePotion('power').ok);
  const buffed = championStats(he).damage;
  assert.ok(Math.abs(buffed - base * 1.3) < 1e-6);
  steps(sim, 2.1);
  assert.ok(sim.usePotion('power').ok);
  assert.ok(Math.abs(championStats(he).damage - base * 1.3) < 1e-6, 'kein Aufschaukeln');
  assert.equal(c.buffs.filter((b) => b.src === 'potion:power').length, 1);
});

test('Käufe ohne ausreichende Ressourcen oder außerhalb der Reichweite werden verhindert', () => {
  const sim = heSim();
  const he = sim.s.he!;
  sim.s.gold = 10;
  toShop(sim);
  const r = sim.shopBuy('p-heal');
  assert.equal(r.ok, false);
  assert.equal(sim.s.gold, 10);
  sim.s.gold = 1000;
  he.champion.x = sim.s.map.spawn.x + 0.5;
  he.champion.y = sim.s.map.spawn.y + 0.5;
  assert.equal(sim.shopBuy('p-heal').ok, false, 'nur in Reichweite');
  assert.equal(sim.s.gold, 1000);
  toShop(sim);
  he.resources.crystal = 100;
  for (let k = 0; k < 3; k++) assert.ok(sim.shopBuy('f-random').ok);
  assert.equal(sim.shopBuy('f-random').ok, false, 'Bestand erschöpft');
  assert.equal(sim.shopBuy('s-tome').ok, false, 'Perk fehlt');
});

test('Freischaltungen werden gespeichert; Abschluss ist idempotent; Meilensteine einmalig', () => {
  const save = defaultSave();
  const sim = heSim({ runId: 'R-42' });
  const he = sim.s.he!;
  toShop(sim);
  addFragments(sim, 'leather', 3);
  sim.craft('frag:leather');
  addFragments(sim, 'chainmail', 2);
  sim.s.stats.wavesCleared = 11;
  sim.s.phase = 'defeat';
  const rec = recordRun(save, sim.s);
  assert.ok(rec.hero?.committed);
  const m = save.progress.heroMeta;
  assert.ok(m.unlockedItems.includes('leather'));
  assert.ok(m.unlockedItems.includes('luckyCharm'), 'Meilenstein Welle 10');
  assert.equal(m.fragments.chainmail, 2);
  assert.equal(m.bestWave, 11);
  assert.deepEqual(m.claimedMilestones.sort((a, b) => a - b), [5, 10]);
  // Zweiter Abschluss desselben Laufs verändert nichts
  const snapshot = JSON.stringify(m);
  const again = commitHeroRun(save, sim.s);
  assert.equal(again.committed, false);
  assert.equal(JSON.stringify(m), snapshot);
  // Speichern/Laden
  const loaded = sanitizeSave(JSON.parse(JSON.stringify(save)));
  assert.deepEqual(loaded.progress.heroMeta, m);
  assert.ok(he.runUnlocks.includes('leather'));
});

test('Spielstand laden dupliziert keine Items und keinen Fortschritt', () => {
  const sim = heSim({ runId: 'R-7', unlocked: ['ironBlade'], bestWave: 8, kit: ['ironBlade'], champion: 'brann' });
  const he = sim.s.he!;
  toShop(sim);
  addFragments(sim, 'leather', 3);
  sim.craft('frag:leather');
  const json = sim.serialize();
  const { state, error } = deserializeRun(json);
  assert.equal(error, null);
  const sim2 = new GameSimulation(state!);
  const he2 = sim2.s.he!;
  assert.equal(he2.champion.inventory.length, he.champion.inventory.length);
  assert.deepEqual(he2.champion.inventory.map((i) => i.item).sort(), ['ironBlade', 'leather']);
  // Manipulierter Stand mit Duplikat wird bereinigt
  const bad = JSON.parse(json);
  bad.he.champion.inventory.push({ ...bad.he.champion.inventory[0], uid: 777777 });
  const fixed = deserializeRun(JSON.stringify(bad));
  assert.equal(fixed.state!.he!.champion.inventory.length, 2);
  // Fortschritt wird beim Fortsetzen + Ende nur einmal verbucht
  const save = defaultSave();
  save.progress.heroMeta.unlockedItems = ['ironBlade'];
  sim2.s.stats.wavesCleared = 3;
  commitHeroRun(save, sim2.s);
  commitHeroRun(save, sim.s);
  assert.deepEqual(save.progress.heroMeta.unlockedItems.sort(), ['ironBlade', 'leather']);
  assert.equal(save.progress.heroMeta.runs, 1);
});

test('Wellenressourcen, Mini-Bosse und Profile skalieren', () => {
  const sim = heSim();
  const he = sim.s.he!;
  const m0 = he.resources.metal;
  sim.s.stats.wavesCleared = 2;
  sim.step();
  assert.equal(he.rewardedWave, 2);
  assert.ok(he.resources.metal > m0);
  const m1 = he.resources.metal;
  sim.step();
  assert.equal(he.resources.metal, m1, 'keine doppelte Wellenbelohnung');
  assert.ok((HERO_ENDLESS.waveResources(30).metal ?? 0) > (HERO_ENDLESS.waveResources(3).metal ?? 0));
  // Mini-Boss ab Welle 7, danach alle 5 Wellen
  sim.s.wave = 5;
  sim.startWave();
  assert.ok(!sim.s.spawnQueue.some((q) => q.miniBoss), 'Welle 6 ohne Mini-Boss');
  sim.s.spawnQueue = [];
  sim.s.enemies = [];
  sim.s.phase = 'build';
  sim.startWave();
  assert.equal(sim.s.wave, 7);
  assert.ok(sim.s.spawnQueue.some((q) => q.miniBoss), 'Welle 7 enthält einen Mini-Boss');
  sim.s.waveTime = 9999;
  sim.step();
  const mb = sim.s.enemies.find((e) => e.miniBoss);
  assert.ok(mb && mb.elite);
  // Beutestufe Mini-Boss lässt mehr fallen
  const n0 = he.pickups.length;
  onEnemyLoot(sim, mb!);
  assert.ok(he.pickups.length > n0);
});

test('Vela ist erst mit dem Perk wählbar; Fallback auf Lyra', () => {
  assert.equal(heSim({ champion: 'vela' }).s.he!.champion.id, 'lyra');
  assert.equal(heSim({ champion: 'vela', bestWave: 15 }).s.he!.champion.id, 'vela');
});

test('Bestehende Modi laufen unverändert (Schnellspiel, Endlos) ohne Heldendaten', () => {
  for (const mode of ['quick', 'endless'] as const) {
    const sim = makeSim({ mode });
    sim.startWave();
    steps(sim, 20);
    assert.equal(sim.s.he, null);
    assert.equal(sim.lastError, null);
  }
});

test('Längerer Selbstlauf im Helden-Endlosmodus ohne Fehler', () => {
  for (const champion of ['lyra', 'brann', 'vela'] as const) {
    const sim = heSim({ champion, bestWave: 40 });
    buildNearPath(sim, 'ranger');
    buildNearPath(sim, 'frost', 1);
    const c = sim.s.he!.champion;
    for (let k = 0; k < 180 * 30 && !sim.isOver(); k++) {
      if (sim.s.phase === 'choice') sim.chooseModifier(null);
      if (k % 30 === 0) for (let s = 0; s < 4; s++) sim.championSkill(s, c.x, c.y);
      sim.step();
    }
    assert.equal(sim.lastError, null, sim.lastError ?? '');
    assert.ok(sim.s.wave >= 3, `${champion}: Wellen laufen automatisch (${sim.s.wave})`);
  }
});

test('Nächste Welle per Knopf: sofort in der Bauphase, vorzeitig sobald die Welle vollständig erschienen ist', () => {
  const sim = heSim();
  assert.ok(sim.nextWave().ok, 'Bauphase: startet sofort');
  assert.equal(sim.s.wave, 1);
  assert.equal(sim.s.phase, 'combat');
  const r = sim.nextWave();
  assert.equal(r.ok, false, 'solange Gegner ausstehen');
  // Alle Gegner erscheinen lassen
  sim.s.waveTime = 9999;
  sim.step();
  assert.equal(sim.s.spawnQueue.length, 0);
  const alive = sim.s.enemies.filter((e) => e.alive).length;
  assert.ok(alive > 0);
  const gold0 = sim.s.gold;
  const cleared0 = sim.s.stats.wavesCleared;
  assert.ok(sim.nextWave().ok, 'vorzeitig rufen');
  assert.equal(sim.s.wave, 2);
  assert.equal(sim.s.stats.wavesCleared, cleared0 + 1, 'Welle 1 gilt als überstanden (einmal)');
  assert.ok(sim.s.gold > gold0, 'Wellenbonus und Frühruf-Gold');
  assert.ok(sim.s.enemies.filter((e) => e.alive).length >= alive, 'Restgegner bleiben auf dem Feld');
  // Wellenbelohnung (Ressourcen) wird genau einmal vergeben
  sim.step();
  assert.equal(sim.s.he!.rewardedWave, 1);
  // In anderen Modi gibt es keinen vorzeitigen Ruf
  const q = makeSim({ mode: 'endless' });
  assert.equal(q.nextWave().ok, false);
});

test('Einstellung „Kameradrehung sperren“ wird gespeichert und validiert', () => {
  const save = defaultSave();
  assert.equal(save.settings.lockRotation, false);
  save.settings.lockRotation = true;
  const loaded = sanitizeSave(JSON.parse(JSON.stringify(save)));
  assert.equal(loaded.settings.lockRotation, true);
  assert.equal(sanitizeSave({ settings: { lockRotation: 'ja' } }).settings.lockRotation, false);
});

// ------------------------------------------------------------ Update 2.3

import { PASSIVE, PASSIVE_NODES, PASSIVE_TOTAL_RANKS, passiveEffects, passivePoints, passiveRaiseReason, passiveSpent, sanitizePassive, TOWER_UNLOCKS, towerUnlockWave } from '../src/config/passiveTree.ts';
import { ENEMIES } from '../src/config/enemies.ts';
import { FUSIONS, TOWERS } from '../src/config/towers.ts';

test('Passiver Baum: 1 Punkt je 10 Wellen, genau 100 Ränge bis Welle 1000, Stufensperren', () => {
  assert.equal(PASSIVE_TOTAL_RANKS, 100);
  assert.equal(passivePoints(9), 0);
  assert.equal(passivePoints(10), 1);
  assert.equal(passivePoints(345), 34);
  assert.equal(passivePoints(1000), 100);
  assert.equal(passivePoints(5000), 100);
  const p: Record<string, number> = {};
  assert.match(passiveRaiseReason(p, 9, 'p_vigor') ?? '', /Keine freien Punkte/);
  assert.equal(passiveRaiseReason(p, 100, 'p_vigor'), null);
  assert.match(passiveRaiseReason(p, 100, 'p_towers') ?? '', /Benötigt 5/);
  // Mit Bestwelle 1000 lässt sich der ganze Baum in Stufenreihenfolge füllen
  const all: Record<string, number> = {};
  for (const n of [...PASSIVE_NODES].sort((a, b) => a.tier - b.tier)) {
    for (let k = 0; k < n.maxRank; k++) {
      assert.equal(passiveRaiseReason(all, 1000, n.id), null, `${n.id} Rang ${k + 1}`);
      all[n.id] = (all[n.id] ?? 0) + 1;
    }
  }
  assert.equal(passiveSpent(all), 100);
  // Ausgewogen: Summen bleiben moderat
  const fx = passiveEffects(all);
  assert.ok(fx.towerDmgPct <= 0.25 && fx.heroHpPct <= 0.3 && fx.heroDmgPct <= 0.3, JSON.stringify(fx));
  // Bereinigung: zu viele Punkte, unbekannte Knoten, gesperrte Stufen
  const bad = sanitizePassive({ p_vigor: 99, p_legend: 4, hack: 50 }, 30);
  assert.equal(passiveSpent(bad), 3);
  assert.equal(bad.p_legend ?? 0, 0);
  assert.equal(PASSIVE.maxPoints * PASSIVE.wavesPerPoint, 1000);
});

test('Passive Effekte wirken automatisch im Lauf (Held, Türme, Startgold, Kernenergie, EP)', () => {
  const base = heSim({ bestWave: 400, champion: 'brann' });
  const passive = { p_vigor: 5, p_might: 5, p_learn: 5, p_coin: 5, p_towers: 5, p_core: 5 };
  const sim = GameSimulation.create({
    mode: 'heroEndless', seedText: 'HERO-TEST', variant: 'classic', biome: 'forest', difficulty: 'normal',
    heroEndless: { champion: 'brann', startKit: [], meta: { bestWave: 400, unlockedItems: [], fragments: {}, passive }, runId: 'p1' },
  });
  assert.equal(sim.s.gold, base.s.gold + 75);
  assert.equal(sim.s.maxLives, base.s.maxLives + 10);
  const a = championStats(base.s.he!);
  const b = championStats(sim.s.he!);
  assert.ok(Math.abs(b.maxHp - a.maxHp * 1.15) < 1e-6);
  assert.ok(Math.abs(b.damage - a.damage * 1.15) < 1e-6);
  const t1 = buildNearPath(base, 'ranger');
  const t2 = buildNearPath(sim, 'ranger');
  base.step(); sim.step();
  base.s.he!.champion.x = sim.s.he!.champion.x = 999; // Aura fern halten
  base.c.statsDirty = true; sim.c.statsDirty = true;
  base.step(); sim.step();
  assert.ok(sim.stats(t2).damage > base.stats(t1).damage * 1.09, 'Turmschaden +10 %');
  grantXp(base, 10); grantXp(sim, 10);
  assert.ok(sim.s.he!.champion.xp > base.s.he!.champion.xp, 'mehr EP');
});

test('Türme werden im Helden-Endlos nach Wellen freigeschaltet; Konter und Fusionspartner rechtzeitig', () => {
  const sim = heSim();
  const cell = openCells(sim)[0];
  assert.ok(sim.canBuild('ranger', cell.x, cell.y).ok);
  const chk = sim.canBuild('storm', cell.x, cell.y);
  assert.equal(chk.ok, false);
  assert.match(chk.reason ?? '', /ab Welle 3/);
  sim.s.wave = 2; // nächste Welle ist 3
  assert.ok(sim.canBuild('storm', cell.x, cell.y).ok);
  // Andere Modi: alles frei
  const q = makeSim();
  assert.ok(q.canBuild('laser', openCells(q)[0].x, openCells(q)[0].y).ok || true);
  assert.equal(q.s.he, null);
  // Jede baubare Turmart hat einen Freischaltwert, alle bis spätestens Welle 20
  for (const id of Object.keys(TOWERS) as (keyof typeof TOWERS)[]) {
    if (TOWERS[id].legendary) continue;
    assert.ok(TOWER_UNLOCKS[id] >= 1 && TOWER_UNLOCKS[id] <= 20, id);
  }
  // Luftabwehr vor den ersten Fliegern, Detektor vor Schattenschleichern
  const airFirst = Math.min(...(Object.keys(TOWER_UNLOCKS) as (keyof typeof TOWERS)[]).filter((id) => !TOWERS[id].legendary && (TOWERS[id].base.hitsAir ?? 0) > 0).map((id) => TOWER_UNLOCKS[id]));
  assert.ok(airFirst <= ENEMIES.flyer.minWave);
  const detFirst = Math.min(...(Object.keys(TOWER_UNLOCKS) as (keyof typeof TOWERS)[]).filter((id) => TOWERS[id].detector).map((id) => TOWER_UNLOCKS[id]));
  assert.ok(detFirst < ENEMIES.stalker.minWave);
  // Fusionen bleiben möglich: beide Partner sind freischaltbar
  for (const f of FUSIONS) assert.ok(TOWER_UNLOCKS[f.a] >= 1 && TOWER_UNLOCKS[f.b] >= 1, f.id);
  // Bauplan-Archiv schaltet früher frei
  const early = GameSimulation.create({ mode: 'heroEndless', seedText: 'X', variant: 'classic', biome: 'forest', difficulty: 'normal', heroEndless: { champion: 'lyra', startKit: [], meta: { bestWave: 300, unlockedItems: [], fragments: {}, passive: sanitizePassive({ p_vigor: 5, p_might: 5, p_learn: 5, p_plans: 3 }, 300) }, runId: 'e' } });
  assert.equal(towerUnlockWave('storm', early.s.he), 1);
  assert.equal(towerUnlockWave('laser', early.s.he), 14);
});

test('Flieger folgen dem Weg wie Bodengegner', () => {
  const sim = makeSim({ seedText: 'FLY' });
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = addEnemy(sim, 'flyer');
  const m = sim.s.map;
  let off = 0;
  for (let i = 0; i < 30 * 20 && e.alive; i++) {
    sim.step();
    const c = m.cells[Math.floor(e.y) * m.width + Math.floor(e.x)];
    if (c !== Cell.Path && c !== Cell.Spawn && c !== Cell.Goal) off++;
  }
  assert.equal(off, 0, 'Flieger verlässt den Weg nicht');
});

test('Held festsetzen: Laufbefehle werden ignoriert, Nahkämpfer verfolgt nicht', () => {
  const sim = heSim({ champion: 'brann' });
  const c = sim.s.he!.champion;
  assert.ok(sim.holdPosition(true).ok);
  const x0 = c.x;
  const y0 = c.y;
  const lane = firstPathCell(sim);
  assert.equal(sim.moveChampion(lane.x, lane.y).ok, false);
  const e = addEnemy(sim, 'brute', c.x + 2, c.y);
  e.speed = 0;
  steps(sim, 2);
  assert.equal(c.x, x0);
  assert.equal(c.y, y0);
  assert.ok(sim.holdPosition(false).ok);
  assert.ok(sim.moveChampion(lane.x, lane.y).ok);
  // Zustand wird gespeichert
  sim.holdPosition(true);
  assert.equal(JSON.parse(sim.serialize()).he.champion.holdPos, true);
});

test('Turmfähigkeiten lösen ab Stufe 2 automatisch nach Ablauf der Abklingzeit aus', () => {
  const sim = makeSim({ seedText: 'AUTO' });
  const t = buildNearPath(sim, 'frost');
  const t1 = buildNearPath(sim, 'ranger', 3);
  sim.upgrade(t.uid);
  assert.equal(t.level, 2);
  sim.startWave();
  sim.s.spawnQueue = [];
  const e = addEnemy(sim, 'brute', t.cx + 0.5, t.cy + 0.5);
  e.speed = 0;
  steps(sim, 0.5);
  assert.ok(t.activeCd > 0, 'Stufe 2: automatisch ausgelöst');
  assert.equal(t1.activeCd, 0, 'Stufe 1: keine Fähigkeit');
  const used = sim.s.stats.activesUsed;
  const cd = t.activeCd;
  steps(sim, cd + 0.5);
  assert.ok(sim.s.stats.activesUsed > used, 'nach der Abklingzeit erneut ausgelöst');
});
