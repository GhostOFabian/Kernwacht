import { CHAMPIONS, championUnlocked } from '../config/champions.ts';
import { sanitizeMastery } from '../config/mastery.ts';
import { SCALING } from '../config/scaling.ts';
import { callNextWave, earlyCallState } from './WaveDirector.ts';
import { PARAGON, paragonPrePoints, sanitizeParagon } from '../config/paragon.ts';
import { activePerks, carrySlots, HERO_ENDLESS, milestoneGold, milestoneParagon, profileForWave, RESOURCE_ORDER, xpToNext } from '../config/heroEndless.ts';
import { canEquipItem, ITEMS, SLOTS } from '../config/items.ts';
import { hashSeed } from '../core/rng.ts';
import { Cell, type ChampionId, type ChampionProgress, type TowerId, type ChampionState, type GameState, type HeroEndlessState, type ItemId, type MapDefinition, type ResourceId } from '../core/types.ts';
import { championHome, championStats, skillPointsForLevel } from './ChampionSystem.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { addPotion, addResource, bonusAmount, lootRand } from './LootSystem.ts';
import { dropRune } from './RuneSystem.ts';
import { RUNE_RULES } from '../config/runes.ts';
import { runPassive, sanitizePassive, TOWER_UNLOCKS, towerUnlockWave } from '../config/passiveTree.ts';
import { TOWERS, towerCenter } from '../config/towers.ts';
import { onDeposit } from './EconomySystem.ts';
import { hasPath } from './Pathfinding.ts';

// Rahmen des Helden-Endlosmodus: Startzustand, automatischer Wellentakt, Wellenbelohnungen, Ladenbestand.

export interface HeroEndlessSetup {
  champion: ChampionId;
  /** Gewünschte Sammlungsitems für den Start (werden auf die Rucksack-Plätze gekürzt). */
  startKit: ItemId[];
  /** Schnappschuss des dauerhaften Fortschritts beim Start. */
  meta: {
    bestWave: number; unlockedItems: ItemId[]; fragments: Partial<Record<ItemId, number>>; passive?: Record<string, number>; campaignDone?: string[];
    /** 2.7: dauerhafter Stand des gewählten Helden und das gemeinsame Paragon. */
    champ?: ChampionProgress; paragon?: { level: number; xp: number; points: Record<string, number> };
    /** 3.0: dauerhafte Turm-Meisterschaft (Abschüsse je Turmtyp). */
    mastery?: Partial<Record<TowerId, number>>;
  };
  runId: string;
}

/** Sucht eine Hindernis-Zelle nahe dem Kern mit begehbarem Nachbarn; sonst wird eine freie Zelle umgewandelt. */
export function placeShop(map: MapDefinition): { x: number; y: number } {
  const w = map.width;
  const h = map.height;
  const walk = (i: number): boolean => map.cells[i] !== Cell.Blocked;
  const dist = new Int32Array(w * h).fill(-1);
  const start = map.goal.y * w + map.goal.x;
  dist[start] = 0;
  const q = [start];
  for (let qi = 0; qi < q.length; qi++) {
    const cur = q[qi];
    const cx = cur % w;
    const cy = (cur - cx) / w;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const nx = cx + dx;
      const ny = cy + dy;
      if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
      const ni = ny * w + nx;
      if (dist[ni] >= 0 || !walk(ni)) continue;
      dist[ni] = dist[cur] + 1;
      q.push(ni);
    }
  }
  // 1) Hindernis neben erreichbarer Zelle, Abstand ≥ 2 zum Kern, nicht am Kartenrand
  let best = -1;
  let bestD = Infinity;
  for (let i = 0; i < w * h; i++) {
    if (map.cells[i] !== Cell.Blocked) continue;
    const x = i % w;
    const y = (i - x) / w;
    if (x === 0 || y === 0 || x === w - 1 || y === h - 1) continue;
    let d = Infinity;
    for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      const ni = (y + dy) * w + x + dx;
      if (dist[ni] >= 0) d = Math.min(d, dist[ni]);
    }
    if (d < 1 || !Number.isFinite(d)) continue;
    const score = Math.abs(d - 2.5);
    if (score < bestD) {
      bestD = score;
      best = i;
    }
  }
  // 2) Rückfall: freie Zelle in Kernnähe, deren Umwandlung keinen Weg blockiert
  if (best < 0 || bestD > 4) {
    const cand = q.filter((i) => map.cells[i] === Cell.Open && dist[i] >= 2).sort((a, b) => dist[a] - dist[b]);
    for (const i of cand) {
      map.cells[i] = Cell.Blocked;
      if (hasPath(map, null)) {
        best = i;
        break;
      }
      map.cells[i] = Cell.Open;
    }
  }
  if (best < 0) best = start; // äußerster Notfall: Laden steht am Kern
  // Dekoration an der Ladenzelle entfernen
  const dec: number[] = [];
  for (let k = 0; k + 2 < map.decor.length; k += 3) if (map.decor[k] !== best) dec.push(map.decor[k], map.decor[k + 1], map.decor[k + 2]);
  map.decor = dec;
  return { x: best % w, y: Math.floor(best / w) };
}

/** Kristalladern: freie, bebaubare Felder abseits von Goldadern, Leylinien, Start und Kern. */
function placeDeposits(state: GameState): void {
  const m = state.map;
  if (m.deposits && m.deposits.length) return;
  const w = m.width;
  let h = hashSeed(state.seed.hash, 'deposits') >>> 0;
  const rnd = (): number => {
    h = (Math.imul(h ^ (h >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0;
    return (h >>> 8) / 16777216;
  };
  const cand: number[] = [];
  for (let i = 0; i < m.cells.length; i++) {
    if (m.cells[i] !== Cell.Open || m.veins.includes(i) || m.ley.includes(i)) continue;
    const x = i % w;
    const y = Math.floor(i / w);
    if (Math.abs(x - m.goal.x) + Math.abs(y - m.goal.y) < 3 || Math.abs(x - m.spawn.x) + Math.abs(y - m.spawn.y) < 3) continue;
    cand.push(i);
  }
  const [lo, hi] = HERO_ENDLESS.depositCount;
  const n = Math.min(cand.length, lo + Math.floor(rnd() * (hi - lo + 1)) + (m.size === 'huge' ? 2 : m.size === 'large' ? 1 : 0));
  const out: number[] = [];
  for (let k = 0; k < n && cand.length; k++) out.push(cand.splice(Math.floor(rnd() * cand.length), 1)[0]);
  m.deposits = out.sort((a, b) => a - b);
}

function makeChampion(id: ChampionId, x: number, y: number): ChampionState {
  return {
    id, x, y, hx: x, hy: y, path: [], hp: 1, mana: 0, level: 1, xp: 0, skillPoints: 0, tree: {}, skillCd: [0, 0, 0, 0],
    attackCd: 0, target: 0, facing: 0, anim: 0, buffs: [], equipment: [null, null, null, null, null, null], specialPotion: null,
    inventory: [], potions: [], potionCd: {}, dead: 0, channel: 0, channelTick: 0, kills: 0, damageDealt: 0,
  };
}

/** Ergänzt einen frisch erzeugten Spielzustand um den Helden-Endlos-Teil. */
export function initHeroEndless(state: GameState, setup: HeroEndlessSetup): void {
  const meta = setup.meta;
  const perks = activePerks(meta.bestWave);
  const champ: ChampionId = CHAMPIONS[setup.champion] && championUnlocked(setup.champion, perks, meta.bestWave, meta.campaignDone ?? []) ? setup.champion : 'lyra';
  const shop = placeShop(state.map);
  const home = championHome(state.map, shop);
  const resources = Object.fromEntries(RESOURCE_ORDER.map((r) => [r, 0])) as Record<ResourceId, number>;
  const owned = [...new Set(meta.unlockedItems.filter((i) => ITEMS[i]))];
  const he: HeroEndlessState = {
    runId: setup.runId,
    champion: makeChampion(champ, home.x, home.y),
    pickups: [],
    zones: [],
    resources,
    towerBuffs: [],
    shop,
    autoTimer: HERO_ENDLESS.prepTime,
    metaOwned: owned,
    runUnlocks: [],
    fragments: Object.fromEntries(Object.entries(meta.fragments).filter(([k, v]) => ITEMS[k] && (v ?? 0) > 0 && !owned.includes(k))),
    fragmentDelta: {},
    perks,
    bestWaveAtStart: meta.bestWave,
    shopStock: {},
    stockWave: 0,
    profile: profileForWave(state.seed.hash, 1).id,
    rewardedWave: 0,
    stats: { xpCollected: 0, itemsCrafted: 0, itemsFound: 0, potionsUsed: 0, resourcesEarned: 0, levelUps: 0 },
  };
  state.he = he;
  state.rng.loot = hashSeed(state.seed.hash, 'loot');
  placeDeposits(state);
  // Startausrüstung: freigeschaltete Items bis zur Anzahl der Rucksack-Plätze, passend angelegt
  const kit = [...new Set(setup.startKit)].filter((i) => owned.includes(i)).slice(0, carrySlots(perks));
  const c = he.champion;
  for (const item of kit) {
    const uid = state.nextUid++;
    c.inventory.push({ uid, item, plus: 0 });
    const slot = SLOTS.findIndex((s) => s.slot === ITEMS[item].slot);
    if (slot >= 0 && c.equipment[slot] === null && canEquipItem(item, champ)) c.equipment[slot] = uid;
  }
  // Dauerhafte Perks
  if (perks.includes('pouch1')) addPotion(he, 'heal', 2);
  if (perks.includes('supply1')) {
    addResource(he, 'metal', 6);
    addResource(he, 'crystal', 2);
  }
  if (perks.includes('supply2')) {
    addResource(he, 'metal', 8);
    addResource(he, 'crystal', 4);
    addResource(he, 'essence', 2);
  }
  if (perks.includes('gold')) state.gold += 80;
  he.passive = sanitizePassive(meta.passive ?? {}, meta.bestWave);
  // 2.7: dauerhafte Heldenstufe und Skillbaum übernehmen, Paragon (gemeinsam) laden
  // Paragon: Vorab-Punkte ab Heldenstufe 50 und Meilensteinpunkte zählen zum Budget
  const lvlForPg = meta.champ ? Math.max(1, Math.min(PARAGON.heroMaxLevel, Math.floor(meta.champ.level) || 1)) : 1;
  he.paragonBonus = milestoneParagon(meta.bestWave);
  const pg = sanitizeParagon(meta.paragon, paragonPrePoints(lvlForPg) + he.paragonBonus);
  he.paragon = { ...pg, rev: 0 };
  state.gold += milestoneGold(meta.bestWave);
  he.mastery = sanitizeMastery(meta.mastery ?? {});
  if (meta.champ) {
    const level = Math.max(1, Math.min(PARAGON.heroMaxLevel, Math.floor(meta.champ.level) || 1));
    c.level = level;
    c.xp = level >= PARAGON.heroMaxLevel ? 0 : Math.max(0, Math.min(xpToNext(level) - 1, meta.champ.xp || 0));
    let spent = 0;
    for (const node of CHAMPIONS[champ].tree) {
      const r = Math.max(0, Math.min(node.maxRank, Math.floor(meta.champ.tree?.[node.id] ?? 0)));
      if (!r) continue;
      c.tree[node.id] = r;
      spent += r;
      // Kernwall-Ränge wirken sofort auf die Kernenergie
      if (node.coreLives) {
        state.lives += node.coreLives * r;
        state.maxLives += node.coreLives * r;
      }
    }
    c.skillPoints = Math.max(0, skillPointsForLevel(level) - spent);
  }
  const pf = runPassive(he);
  state.gold += pf.startGold;
  state.lives += pf.coreLives;
  state.maxLives += pf.coreLives;
  he.stats.resourcesEarned = 0;
  const st = championStats(he);
  c.hp = st.maxHp;
  c.mana = st.maxMana;
}

/** Automatischer Wellentakt, Wellenbelohnungen und Ladenbestand. Läuft nur, wenn die Simulation läuft (Pause stoppt alles). */
export function updateHeroEndless(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  const he = s.he;
  if (!he) return;
  // 3.0: Nachrückende Wellen – ab Welle 120 rückt nach 45 s Kampf die nächste Welle nach
  if (s.phase === 'combat' && s.wave >= SCALING.overrunFrom) {
    if (he.overrunWave !== s.wave) {
      he.overrunWave = s.wave;
      he.overrunT = 0;
    }
    he.overrunT = (he.overrunT ?? 0) + dt;
    if (he.overrunT >= SCALING.overrunAfter && earlyCallState(s) === null) {
      he.overrunT = 0;
      sim.emit({ k: 'message', text: `Welle ${s.wave} dauert zu lange – Welle ${s.wave + 1} rückt nach!`, severity: 'warn' });
      callNextWave(sim);
    }
  }
  // Wellenbelohnung genau einmal pro abgeschlossener Welle
  while (he.rewardedWave < s.stats.wavesCleared) {
    he.rewardedWave++;
    if (he.rewardedWave >= RUNE_RULES.splinterFrom && lootRand(sim) < RUNE_RULES.waveChance) dropRune(sim, s.map.goal.x + 0.5, s.map.goal.y + 0.5);
    const r = HERO_ENDLESS.waveResources(he.rewardedWave);
    let total = 0;
    for (const [k, v] of Object.entries(r) as [ResourceId, number][]) {
      if (v > 0) {
        const n = bonusAmount(sim, v, runPassive(he).resourcePct);
        addResource(he, k, n);
        total += n;
      }
    }
    if (total > 0) sim.emit({ k: 'pickup', kind: 'resource', x: he.champion.x, y: he.champion.y, label: `Wellenbeute +${total}`, color: 0xffd36b });
    // Kristalladern: Goldgräber auf einer Ader fördern zusätzlich Kristall
    for (const t of s.towers) {
      if (t.def !== 'goldmine' || !onDeposit(sim, t) || t.builtWave >= he.rewardedWave) continue;
      const n = 1 + Math.floor(he.rewardedWave / 8) + (t.level >= 3 ? 1 : 0);
      addResource(he, 'crystal', n);
      sim.emit({ k: 'pickup', kind: 'resource', x: towerCenter(t).x, y: towerCenter(t).y, label: `+${n} ◆ Kristallader`, color: 0x7ae0ff });
    }
    // Neue Türme für die nächste Welle freigeschaltet?
    const next = he.rewardedWave + 1;
    const fresh = (Object.keys(TOWER_UNLOCKS) as (keyof typeof TOWER_UNLOCKS)[])
      .filter((id) => TOWERS[id] && !TOWERS[id].legendary && (!TOWERS[id].mazingOnly || s.map.variant === 'mazing') && towerUnlockWave(id, he) === next);
    if (fresh.length) sim.emit({ k: 'message', text: `Neu freigeschaltet: ${fresh.map((id) => TOWERS[id].name).join(', ')}`, severity: 'info' });
  }
  // Ladenbestand alle n Wellen auffrischen
  if (s.wave >= he.stockWave + HERO_ENDLESS.stockEvery) {
    he.stockWave = s.wave - (s.wave % HERO_ENDLESS.stockEvery);
    he.shopStock = {};
  }
  if (s.phase === 'build') {
    he.profile = profileForWave(s.seed.hash, s.wave + 1).id;
    if (he.autoTimer < 0) he.autoTimer = HERO_ENDLESS.transitionTime;
    he.autoTimer -= dt;
    if (he.autoTimer <= 0) {
      he.autoTimer = -1;
      sim.startWave();
    }
  } else if (s.phase === 'combat') {
    he.autoTimer = -1;
  }
  // 'choice' (Modifikatorauswahl) hält den Countdown an, bis gewählt wurde.
}
