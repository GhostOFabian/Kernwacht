import { BIOMES } from '../config/biomes.ts';
import { HERO_ENDLESS } from '../config/heroEndless.ts';
import { RESOURCES } from '../config/heroEndless.ts';
import { Cell, type ResourceId } from '../core/types.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { addResource, lootRand } from './LootSystem.ts';

// Helden-Endlos: Bäume, Felsen und andere Hindernisse abbauen. Der Abbau dauert so lange wie eine
// reguläre Welle, ist nicht unterbrechbar (der Held steht still, kann nicht fallen) und bringt
// zufällig Gold und Ressourcen. Danach ist das Feld frei und bebaubar.

/** Nicht abbaubare Hinderniskinder: Lava (3) und Wolken (7). */
const UNMINABLE = new Set([3, 7]);

export function decorKindAt(sim: GameSimulation, idx: number): number | null {
  const d = sim.s.map.decor;
  for (let k = 0; k + 2 < d.length; k += 3) if (d[k] === idx) return d[k + 1];
  return null;
}

/** Abbauzeit: mittlere bisherige Wellendauer (mindestens/höchstens begrenzt), sonst Standardwert. */
export function mineDuration(sim: GameSimulation): number {
  const s = sim.s;
  const avg = s.stats.wavesCleared > 0 ? s.stats.duration / s.stats.wavesCleared : HERO_ENDLESS.mineTimeDefault;
  return Math.round(Math.min(HERO_ENDLESS.mineTimeMax, Math.max(HERO_ENDLESS.mineTimeMin, avg)));
}

export function mineState(sim: GameSimulation, cx: number, cy: number): { ok: boolean; reason: string | null } {
  const s = sim.s;
  const he = s.he;
  if (!he) return { ok: false, reason: 'Nur im Helden-Endlosmodus' };
  const m = s.map;
  if (cx < 0 || cy < 0 || cx >= m.width || cy >= m.height) return { ok: false, reason: 'Außerhalb der Karte' };
  const idx = cy * m.width + cx;
  if (m.cells[idx] !== Cell.Blocked) return { ok: false, reason: 'Hier gibt es nichts abzubauen' };
  if (he.shop.x === cx && he.shop.y === cy) return { ok: false, reason: 'Der Laden kann nicht abgebaut werden' };
  const kind = decorKindAt(sim, idx);
  if (kind === null || UNMINABLE.has(kind)) return { ok: false, reason: 'Dieses Hindernis lässt sich nicht abbauen' };
  if (he.mining) return { ok: false, reason: 'Der Held baut bereits ab' };
  if (he.champion.dead > 0) return { ok: false, reason: 'Held ist besiegt' };
  return { ok: true, reason: null };
}

const adjacent = (sim: GameSimulation, idx: number): boolean => {
  const c = sim.s.he?.champion;
  if (!c) return false;
  const w = sim.s.map.width;
  const x = idx % w;
  const y = Math.floor(idx / w);
  return Math.max(Math.abs(c.x - (x + 0.5)), Math.abs(c.y - (y + 0.5))) <= 1.55;
};

/** Startet den Abbau (sofort, wenn der Held danebensteht) oder merkt das Ziel vor. */
export function orderMine(sim: GameSimulation, cx: number, cy: number): CommandResult & { walk?: boolean } {
  const chk = mineState(sim, cx, cy);
  if (!chk.ok) return { ok: false, reason: chk.reason ?? 'Nicht möglich' };
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held' };
  const idx = cy * sim.s.map.width + cx;
  if (adjacent(sim, idx)) {
    startMining(sim, idx);
    return { ok: true };
  }
  he.mineTarget = idx;
  return { ok: true, walk: true };
}

function startMining(sim: GameSimulation, idx: number): void {
  const he = sim.s.he;
  if (!he) return;
  const total = mineDuration(sim);
  he.mining = { cell: idx, t: total, total };
  he.mineTarget = -1;
  const c = he.champion;
  c.path = [];
  c.channel = 0;
  const w = sim.s.map.width;
  c.facing = Math.atan2(Math.floor(idx / w) + 0.5 - c.y, (idx % w) + 0.5 - c.x);
  sim.emit({ k: 'mineStart', x: (idx % w) + 0.5, y: Math.floor(idx / w) + 0.5, total });
}

/** Takt: vorgemerktes Ziel beim Eintreffen beginnen, laufenden Abbau fortsetzen und abschließen. */
export function updateMining(sim: GameSimulation, dt: number): void {
  const he = sim.s.he;
  if (!he) return;
  const c = he.champion;
  if (!he.mining && he.mineTarget !== undefined && he.mineTarget >= 0 && c.path.length === 0 && c.dead <= 0) {
    const idx = he.mineTarget;
    const w = sim.s.map.width;
    if (adjacent(sim, idx) && mineState(sim, idx % w, Math.floor(idx / w)).ok) startMining(sim, idx);
    else he.mineTarget = -1;
  }
  const m = he.mining;
  if (!m) return;
  m.t -= dt;
  c.anim = Math.max(c.anim, 0.3);
  if (m.t > 0) return;
  finishMining(sim, m.cell);
}

function finishMining(sim: GameSimulation, idx: number): void {
  const s = sim.s;
  const he = s.he;
  if (!he) return;
  const map = s.map;
  const kind = decorKindAt(sim, idx);
  map.cells[idx] = Cell.Open;
  const dec: number[] = [];
  for (let k = 0; k + 2 < map.decor.length; k += 3) if (map.decor[k] !== idx) dec.push(map.decor[k], map.decor[k + 1], map.decor[k + 2]);
  map.decor = dec;
  map.rev = (map.rev ?? 0) + 1;
  sim.c.fieldDirty = true;
  sim.c.statsDirty = true;
  he.mining = null;
  he.mined = (he.mined ?? 0) + 1;
  // Zufällige Ausbeute, wächst mit der Welle
  const wave = Math.max(1, s.wave);
  const r = () => lootRand(sim);
  const gold = Math.round((HERO_ENDLESS.mineGoldBase + HERO_ENDLESS.mineGoldPerWave * wave) * (0.5 + r()));
  s.gold += gold;
  s.stats.goldEarned += gold;
  const parts = [`+${gold} Gold`];
  const give = (res: ResourceId, n: number) => {
    if (n <= 0) return;
    addResource(he, res, n);
    parts.push(`+${n} ${RESOURCES[res].icon}`);
  };
  const scale = 1 + wave / 12;
  // Kristallgruppen bringen eher Kristall, Bäume/Pilze eher Metall (Holz wird als Baumaterial verbucht)
  const crystalBias = kind === 2 || kind === 4 ? 0.35 : 0;
  if (r() < 0.75) give('metal', Math.round((2 + r() * 5) * scale));
  if (r() < 0.35 + crystalBias) give('crystal', Math.round((1 + r() * 3) * scale));
  if (r() < 0.15) give('essence', 1 + Math.floor(r() * 2 * scale));
  if (r() < 0.04) give('relic', 1);
  const x = (idx % map.width) + 0.5;
  const y = Math.floor(idx / map.width) + 0.5;
  sim.emit({ k: 'mined', x, y, label: parts.join(' ') });
  sim.emit({ k: 'message', text: `Abgebaut (${BIOMES[map.biome].name}): ${parts.join(', ')} – das Feld ist jetzt frei.`, severity: 'info' });
}

export function isMining(sim: GameSimulation): boolean {
  return !!sim.s.he?.mining;
}
