import assert from 'node:assert/strict';
import { test } from 'node:test';
import { recordRun } from '../src/app/progress.ts';
import { decodeMap, encodeMap } from '../src/editor/mapCode.ts';
import { Cell } from '../src/core/types.ts';
import { canSerializeRun, defaultSave, deserializeRun, SAVE_KEY, sanitizeSave, SaveSystem, type KeyValueStore } from '../src/save/SaveSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';
import { generateWorld, makeSeed, validateMap } from '../src/sim/WorldGenerator.ts';
import { buildNearPath, makeSim } from './helpers.ts';

class MemStore implements KeyValueStore {
  m = new Map<string, string>();
  getItem(k: string) { return this.m.get(k) ?? null; }
  setItem(k: string, v: string) { this.m.set(k, v); }
  removeItem(k: string) { this.m.delete(k); }
}

test('Speichern und Laden: Einstellungen und Fortschritt bleiben erhalten', () => {
  const store = new MemStore();
  const sys = new SaveSystem(store);
  const d = defaultSave();
  d.settings.musicVolume = 0.2;
  d.progress.campaignUnlocked = 3;
  d.progress.bestEndlessWave.mazing = 17;
  assert.ok(sys.save(d));
  const r = sys.load();
  assert.equal(r.error, null);
  assert.equal(r.data.settings.musicVolume, 0.2);
  assert.equal(r.data.progress.campaignUnlocked, 3);
  assert.equal(r.data.progress.bestEndlessWave.mazing, 17);
});

test('Beschädigte Daten: Sicherungskopie + verständliche Meldung, kein stilles Löschen', () => {
  const store = new MemStore();
  store.setItem(SAVE_KEY, '{kaputt');
  const r = new SaveSystem(store).load();
  assert.ok(r.error && r.error.includes('Sicherungskopie'));
  assert.ok(r.backupKey && store.getItem(r.backupKey) === '{kaputt');
  assert.equal(store.getItem(SAVE_KEY), '{kaputt', 'Original wird nicht gelöscht');
});

test('Validierung repariert Felder mit sicheren Standardwerten', () => {
  const s = sanitizeSave({ version: 1, settings: { masterVolume: 9, muted: 'ja', quality: 'ultra' }, progress: { campaignUnlocked: -4, campaignCompleted: ['m1', 'xx', 5] } });
  assert.equal(s.settings.masterVolume, 1);
  assert.equal(s.settings.muted, false);
  assert.equal(s.settings.quality, 'auto');
  assert.equal(s.progress.campaignUnlocked, 1);
  assert.deepEqual(s.progress.campaignCompleted, ['m1']);
  assert.deepEqual(s.customMaps, []);
});

test('Neuere Spielstandversion wird nicht überschrieben', () => {
  const store = new MemStore();
  store.setItem(SAVE_KEY, JSON.stringify({ version: 99 }));
  const r = new SaveSystem(store).load();
  assert.ok(r.error);
});

test('Laufende Partie: Serialisierung in der Bauphase und exakte Fortsetzung', () => {
  const a = makeSim({ seedText: 'FORTSETZEN' });
  buildNearPath(a, 'storm');
  buildNearPath(a, 'cannon', 2);
  a.startWave();
  while (a.s.phase === 'combat') a.step();
  if (a.s.phase === 'choice') a.chooseModifier(a.s.modOffer?.[0] ?? null);
  assert.ok(canSerializeRun(a.s));
  const json = a.serialize();
  const { state, error } = deserializeRun(json);
  assert.equal(error, null);
  const b = new GameSimulation(state!);
  for (const s of [a, b]) {
    s.startWave();
    for (let i = 0; i < 30 * 20; i++) s.step();
  }
  // network.version ist nur ein Cache-Zähler (Neuaufbau beim Laden) und kein Spielzustand.
  const norm = (x: GameSimulation): string => JSON.stringify({ ...x.s, network: { ...x.s.network, version: 0 } });
  assert.equal(norm(a), norm(b));
});

test('Ungültige Spielstände werden abgelehnt', () => {
  const sim = makeSim();
  sim.startWave();
  const r = deserializeRun(sim.serialize());
  assert.equal(r.state, null);
  assert.equal(deserializeRun('nicht json').state, null);
  const bad = JSON.parse(makeSim().serialize());
  bad.towers = [{ def: 'quantenkanone', cx: 0, cy: 0 }];
  assert.equal(deserializeRun(JSON.stringify(bad)).state, null);
});

test('Fortschritt: Kampagnensieg schaltet nächste Mission frei und vergibt Sterne', () => {
  const save = defaultSave();
  const sim = GameSimulation.create({ mode: 'campaign', missionId: 'm1', seedText: '', variant: 'classic', biome: 'forest', difficulty: 'normal' });
  sim.s.phase = 'victory';
  sim.s.stats.wavesCleared = 8;
  const r = recordRun(save, sim.s);
  assert.equal(r.unlockedMission, 'm2');
  assert.equal(save.progress.campaignUnlocked, 2);
  assert.equal(r.stars, 3);
  assert.ok(save.progress.campaignCompleted.includes('m1'));
  assert.ok(r.score > 0);
  const e = makeSim({ mode: 'endless' });
  e.s.phase = 'defeat';
  e.s.stats.wavesCleared = 12;
  assert.ok(recordRun(save, e.s).newBestEndless);
  assert.equal(save.progress.bestEndlessWave.classic, 12);
});

test('Kartencode: Export/Import ist verlustfrei, ungültige Codes werden abgelehnt', () => {
  for (const v of ['classic', 'mazing'] as const) {
    const m = generateWorld({ seed: makeSeed(`CODE-${v}`), variant: v, biome: 'crystal' });
    const code = encodeMap(m);
    const r = decodeMap(code);
    assert.equal(r.error, null);
    assert.ok(r.map);
    assert.deepEqual(r.map.cells, m.cells);
    assert.deepEqual(r.map.spawn, m.spawn);
    assert.deepEqual(r.map.goal, m.goal);
    assert.ok(r.validation?.ok);
  }
  assert.ok(decodeMap('RB1.classic.forest.22x14.9*5.').error);
  assert.ok(decodeMap('hallo').error);
  // Karte ohne Weg ist nicht spielbar
  const m = generateWorld({ seed: makeSeed('OHNEWEG'), variant: 'classic', biome: 'forest' });
  m.cells = m.cells.map((c) => (c === Cell.Path ? Cell.Open : c));
  const r = decodeMap(encodeMap(m));
  assert.equal(r.validation?.ok, false);
  assert.equal(validateMap(r.map!).ok, false);
});

test('Eigene Karte ist als Partie spielbar', () => {
  const m = generateWorld({ seed: makeSeed('EIGEN'), variant: 'classic', biome: 'forest' });
  const r = decodeMap(encodeMap(m));
  const sim = GameSimulation.create({ mode: 'custom', seedText: 'KARTE-1', variant: 'classic', biome: 'forest', difficulty: 'normal', customMap: r.map });
  buildNearPath(sim, 'ranger');
  sim.startWave();
  for (let i = 0; i < 300; i++) sim.step();
  assert.equal(sim.lastError, null);
  assert.ok(sim.s.enemies.length > 0 || sim.s.stats.kills > 0);
});

test('Spielstand unter dem früheren Speicherschlüssel wird übernommen', () => {
  const store = new MemStore();
  const d = defaultSave();
  d.progress.campaignUnlocked = 4;
  store.setItem('riftbound.save', JSON.stringify(d));
  const r = new SaveSystem(store).load();
  assert.equal(r.error, null);
  assert.equal(r.data.progress.campaignUnlocked, 4);
  assert.ok(store.getItem('riftbound.save'), 'alter Eintrag bleibt erhalten');
});
