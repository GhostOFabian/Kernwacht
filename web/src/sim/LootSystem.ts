import { runPassive } from '../config/passiveTree.ts';
import { rollEnemyRune } from './RuneSystem.ts';
import { ENEMIES } from '../config/enemies.ts';
import { HERO_ENDLESS, LOOT, POTIONS_BY_PERK, RESOURCES } from '../config/heroEndless.ts';
import { ITEM_ORDER, ITEMS, POTION_ORDER, POTIONS, RARITY, canEquipItem } from '../config/items.ts';
import { step as rngStep } from '../core/rng.ts';
import type { EnemyInstance, HeroEndlessState, ItemId, PickupKind, PotionId, ResourceId } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';

// Beute des Helden-Endlosmodus: Erfahrung, Ressourcen, Tränke, Fragmente und Items als
// Bodenobjekte. Eigener, deterministischer Zufallsstrom (s.rng.loot). Jedes Objekt wird beim
// Aufsammeln sofort entfernt – eine Doppelvergabe ist ausgeschlossen.

export function lootRand(sim: GameSimulation): number {
  const [v, st] = rngStep(sim.s.rng.loot ?? 0x51f15e);
  sim.s.rng.loot = st;
  return v;
}

/** Alle Items, die in dieser Partie als „vorhanden“ gelten (Sammlung, Partie, Inventar, am Boden). */
export function ownedItems(he: HeroEndlessState): Set<ItemId> {
  const set = new Set<ItemId>([...he.metaOwned, ...he.runUnlocks]);
  for (const i of he.champion.inventory) set.add(i.item);
  for (const p of he.pickups) if (p.kind === 'item') set.add(p.ref);
  return set;
}

/** Noch nicht freigeschaltete, als Beute mögliche Items. */
export function missingItems(he: HeroEndlessState, wave: number): ItemId[] {
  const owned = ownedItems(he);
  // 3.4: nur Gegenstände, die der aktuelle Held tragen kann (Beute, Fragmente, Ladenbündel)
  return ITEM_ORDER.filter((id) => !owned.has(id) && !ITEMS[id].craftOnly && ITEMS[id].minWave <= Math.max(1, wave) && canEquipItem(id, he.champion.id));
}

function pick<T>(sim: GameSimulation, arr: readonly T[]): T {
  return arr[Math.floor(lootRand(sim) * arr.length) % arr.length];
}

function spawnPickup(sim: GameSimulation, kind: PickupKind, x: number, y: number, amount: number, ref: string): void {
  const he = sim.s.he;
  if (!he) return;
  const a = lootRand(sim) * Math.PI * 2;
  const d = kind === 'xp' ? 0.15 : 0.25 + lootRand(sim) * 0.35;
  he.pickups.push({ uid: sim.uid(), kind, x: x + Math.cos(a) * d, y: y + Math.sin(a) * d, amount, ref, t: HERO_ENDLESS.pickupLife[kind] ?? 40 });
  // Obergrenze: bei sehr vielen Bodenobjekten verfallen zuerst die ältesten Erfahrungskugeln.
  if (he.pickups.length > 160) {
    const i = he.pickups.findIndex((p) => p.kind === 'xp');
    if (i >= 0) he.pickups.splice(i, 1);
  }
}

/** Wählt ein Fragmentziel unter den fehlenden Items (seltene Items erst später häufiger). */
function fragmentTarget(sim: GameSimulation, he: HeroEndlessState): ItemId | null {
  const miss = missingItems(he, sim.s.wave);
  if (!miss.length) return null;
  const weights = miss.map((id) => {
    const o = RARITY[ITEMS[id].rarity].order;
    return Math.max(0.2, 3 - o + Math.min(3, sim.s.wave / 10) * o * 0.5);
  });
  let r = lootRand(sim) * weights.reduce((a, b) => a + b, 0);
  for (let i = 0; i < miss.length; i++) {
    r -= weights[i];
    if (r <= 0) return miss[i];
  }
  return miss[miss.length - 1];
}

export function availablePotions(he: HeroEndlessState): PotionId[] {
  return POTION_ORDER.filter((p) => !POTIONS_BY_PERK[p] || he.perks.includes(POTIONS_BY_PERK[p] as string));
}

/** Beute beim Tod eines Gegners (nur im Helden-Endlosmodus). */
export function onEnemyLoot(sim: GameSimulation, e: EnemyInstance): void {
  const he = sim.s.he;
  if (!he) return;
  const wave = Math.max(1, sim.s.wave);
  const def = ENEMIES[e.def];
  const tier: 'normal' | 'elite' | 'miniBoss' | 'boss' = e.boss ? 'boss' : e.miniBoss ? 'miniBoss' : e.elite ? 'elite' : 'normal';
  const luck = 1 + (he.champion ? lootLuck(he) : 0);
  // Erfahrung
  let xp = LOOT.xpPerThreat * (e.boss ? 6 : def.threat) * (1 + LOOT.xpWaveGrowth * (wave - 1));
  if (tier === 'elite') xp *= LOOT.eliteXpMul;
  if (tier === 'miniBoss') xp *= LOOT.miniBossXpMul;
  if (tier === 'boss') xp *= LOOT.bossXpMul;
  spawnPickup(sim, 'xp', e.x, e.y, Math.round(xp), 'xp');
  // Ressourcen
  if (tier !== 'normal' || lootRand(sim) < LOOT.resourceChance * luck) {
    const res: ResourceId = tier === 'boss' || tier === 'miniBoss' ? (lootRand(sim) < 0.35 ? 'relic' : 'essence') : tier === 'elite' ? 'essence' : LOOT.resourceOf(e.def);
    const amount = res === 'relic' ? 1 : bonusAmount(sim, LOOT.resourceAmount(wave) * (tier === 'normal' ? 1 : 2), runPassive(he).resourcePct);
    spawnPickup(sim, 'resource', e.x, e.y, amount, res);
  }
  // Tränke
  if (lootRand(sim) < LOOT.potionChance * luck * (tier === 'normal' ? 1 : 4)) spawnPickup(sim, 'potion', e.x, e.y, 1, pick(sim, availablePotions(he)));
  // Fragmente
  if (lootRand(sim) < LOOT.fragmentChance[tier] * luck) {
    const n = tier === 'boss' ? 3 : tier === 'miniBoss' ? 2 : 1;
    for (let k = 0; k < n; k++) {
      const target = fragmentTarget(sim, he);
      if (target) spawnPickup(sim, 'fragment', e.x, e.y, 1, target);
      else spawnPickup(sim, 'resource', e.x, e.y, HERO_ENDLESS.fragmentToEssence, 'essence');
    }
  }
  // Vollständige Items – nie ein bereits vorhandenes (Sammlung, Partie, Inventar, am Boden)
  if (LOOT.itemChance[tier] > 0 && lootRand(sim) < LOOT.itemChance[tier] * luck) {
    const miss = missingItems(he, wave);
    if (miss.length) spawnPickup(sim, 'item', e.x, e.y, 1, pick(sim, miss));
    else {
      const target = fragmentTarget(sim, he);
      if (target) spawnPickup(sim, 'fragment', e.x, e.y, 1, target);
      else spawnPickup(sim, 'resource', e.x, e.y, 3, 'essence');
    }
  }
  // Runen (erst ab Welle 250)
  rollEnemyRune(sim, tier, e.x, e.y);
}

/** Prozentbonus auf kleine Ganzzahlen: Rest wird deterministisch per Beute-Zufall gerundet. */
export function bonusAmount(sim: GameSimulation, n: number, pct: number): number {
  if (pct <= 0 || n <= 0) return n;
  const exact = n * (1 + pct);
  const base = Math.floor(exact);
  return base + (lootRand(sim) < exact - base ? 1 : 0);
}

function lootLuck(he: HeroEndlessState): number {
  // Glücksbonus aus Ausrüstung/Perks; bewusst ohne championStats-Import (keine Zyklen): Itemwert direkt.
  let luck = (he.perks.includes('fortune') ? 0.25 : 0) + runPassive(he).lootPct;
  for (const uid of he.champion.equipment) {
    const inst = uid === null ? undefined : he.champion.inventory.find((i) => i.uid === uid);
    if (inst) luck += (ITEMS[inst.item]?.stats.lootBonus ?? 0) * (1 + 0.15 * inst.plus);
  }
  return luck;
}

export function updatePickups(sim: GameSimulation, dt: number): void {
  const he = sim.s.he;
  if (!he || he.pickups.length === 0) return;
  for (const p of he.pickups) p.t -= dt;
  if (he.pickups.some((p) => p.t <= 0)) he.pickups = he.pickups.filter((p) => p.t > 0);
}

/** Fügt Fragmente hinzu; Fragmente bereits vorhandener Items werden zu Essenz. */
export function addFragments(sim: GameSimulation, item: ItemId, n: number): void {
  const he = sim.s.he;
  if (!he) return;
  if (ownedItems(he).has(item)) {
    addResource(he, 'essence', HERO_ENDLESS.fragmentToEssence * n);
    return;
  }
  he.fragments[item] = (he.fragments[item] ?? 0) + n;
  he.fragmentDelta[item] = (he.fragmentDelta[item] ?? 0) + n;
}

export function addResource(he: HeroEndlessState, r: ResourceId, n: number): void {
  he.resources[r] = (he.resources[r] ?? 0) + n;
  if (n > 0) he.stats.resourcesEarned += n;
}

/** Sammelt Beute im Radius ein. Rückgabe: Anzahl gesammelter Objekte. */
export function collectPickups(sim: GameSimulation, radius: number): number {
  const he = sim.s.he;
  if (!he || he.pickups.length === 0) return 0;
  const c = he.champion;
  const r2 = radius * radius;
  const pull2 = (radius * 2.2) ** 2;
  let n = 0;
  const keep = [];
  for (const p of he.pickups) {
    const d2 = (p.x - c.x) ** 2 + (p.y - c.y) ** 2;
    if (d2 > r2) {
      // Leichter Sog in der Nähe des Helden
      if (d2 <= pull2) {
        const d = Math.sqrt(d2) || 1;
        p.x += ((c.x - p.x) / d) * 0.05;
        p.y += ((c.y - p.y) / d) * 0.05;
      }
      keep.push(p);
      continue;
    }
    if (!applyPickup(sim, he, p)) {
      keep.push(p);
      continue;
    }
    n++;
  }
  he.pickups = keep;
  return n;
}

function applyPickup(sim: GameSimulation, he: HeroEndlessState, p: { kind: PickupKind; ref: string; amount: number; x: number; y: number }): boolean {
  let label = '';
  let color = 0xffffff;
  switch (p.kind) {
    case 'xp':
      // Erfahrung wird vom ChampionSystem gutgeschrieben (Import vermeiden: über Ereignis + Hook)
      grantXpHook?.(sim, p.amount);
      label = `+${p.amount} EP`;
      color = 0x8affb0;
      break;
    case 'resource': {
      const r = p.ref as ResourceId;
      addResource(he, r, p.amount);
      label = `+${p.amount} ${RESOURCES[r].name}`;
      color = RESOURCES[r].color;
      break;
    }
    case 'potion': {
      const ok = addPotion(he, p.ref as PotionId, p.amount);
      if (!ok) return false;
      label = POTIONS[p.ref as PotionId].name;
      color = POTIONS[p.ref as PotionId].color;
      break;
    }
    case 'fragment':
      addFragments(sim, p.ref, p.amount);
      label = `Fragment: ${ITEMS[p.ref]?.name ?? p.ref}`;
      color = 0xc080ff;
      break;
    case 'item': {
      if (!addItemInstance(sim, he, p.ref)) {
        if (!he.stats.itemsFound || sim.s.tick % 60 === 0) sim.emit({ k: 'message', text: 'Inventar voll – Gegenstand bleibt liegen.', severity: 'warn' });
        return false;
      }
      he.stats.itemsFound++;
      label = `${ITEMS[p.ref]?.name ?? p.ref}!`;
      color = 0xffc040;
      break;
    }
  }
  sim.emit({ k: 'pickup', kind: p.kind, x: p.x, y: p.y, label, color });
  return true;
}

/** Wird vom ChampionSystem gesetzt (verhindert einen Importzyklus). */
export let grantXpHook: ((sim: GameSimulation, amount: number) => void) | null = null;
export function setGrantXpHook(fn: (sim: GameSimulation, amount: number) => void): void {
  grantXpHook = fn;
}

export function addPotion(he: HeroEndlessState, potion: PotionId, n: number): boolean {
  const def = POTIONS[potion];
  if (!def) return false;
  const st = he.champion.potions.find((x) => x.potion === potion);
  if (st) {
    if (st.count + n > def.stack) return false;
    st.count += n;
    return true;
  }
  if (he.champion.specialPotion && he.champion.specialPotion.potion === potion) {
    if (he.champion.specialPotion.count + n > def.stack) return false;
    he.champion.specialPotion.count += n;
    return true;
  }
  if (he.champion.potions.length >= HERO_ENDLESS.potionStacks) return false;
  he.champion.potions.push({ potion, count: n });
  return true;
}

/**
 * Legt eine Item-Instanz an. Je Item existiert in einer Partie höchstens eine Instanz;
 * neue Items werden als Freischaltung der Partie vorgemerkt (Sammlung beim Abschluss).
 */
export function addItemInstance(sim: GameSimulation, he: HeroEndlessState, item: ItemId, plus = 0): boolean {
  if (!ITEMS[item]) return false;
  if (he.champion.inventory.some((i) => i.item === item)) return false;
  if (he.champion.inventory.length >= HERO_ENDLESS.inventorySlots + 6) return false;
  he.champion.inventory.push({ uid: sim.uid(), item, plus });
  if (!he.metaOwned.includes(item) && !he.runUnlocks.includes(item)) {
    he.runUnlocks.push(item);
    // Übrige Fragmente dieses Items werden zu Essenz.
    const left = he.fragments[item] ?? 0;
    if (left > 0) {
      he.fragments[item] = 0;
      he.fragmentDelta[item] = (he.fragmentDelta[item] ?? 0) - left;
      addResource(he, 'essence', left * HERO_ENDLESS.fragmentToEssence);
    }
  }
  return true;
}
