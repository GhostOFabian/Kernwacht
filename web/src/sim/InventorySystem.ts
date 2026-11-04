import { HERO_ENDLESS } from '../config/heroEndless.ts';
import { canEquipItem, ITEMS, MAX_PLUS, POTION_PCT, POTIONS, RARITY, RECIPES, sellValue, SHOP_OFFERS, SLOTS, upgradeCost, type Cost, type RecipeDefinition, type ShopOffer } from '../config/items.ts';
import type { ChampionStats, HeroEndlessState, ItemId, PotionId, ResourceId } from '../core/types.ts';
import { applyTowerBuff, championStats } from './ChampionSystem.ts';
import type { CommandResult, GameSimulation } from './GameSimulation.ts';
import { addFragments, addItemInstance, addPotion, addResource, lootRand, missingItems, ownedItems } from './LootSystem.ts';

// Ausrüstung, Tränke, Laden und Herstellung des Helden-Endlosmodus. Jede Transaktion prüft zuerst
// alle Bedingungen und verändert den Zustand erst danach – fehlgeschlagene Vorgänge verlieren nichts.

const fail = (reason: string): CommandResult => ({ ok: false, reason });

export function nearShop(sim: GameSimulation): boolean {
  const he = sim.s.he;
  if (!he) return false;
  const c = he.champion;
  if (c.dead > 0) return false;
  return (c.x - (he.shop.x + 0.5)) ** 2 + (c.y - (he.shop.y + 0.5)) ** 2 <= HERO_ENDLESS.shopRange ** 2;
}

// ------------------------------------------------------------ Kosten

export function canAfford(sim: GameSimulation, cost: Cost): boolean {
  const he = sim.s.he;
  if (!he) return false;
  for (const [k, v] of Object.entries(cost) as [ResourceId | 'gold', number][]) {
    if (!v) continue;
    if (k === 'gold' ? sim.s.gold < v : (he.resources[k] ?? 0) < v) return false;
  }
  return true;
}

function pay(sim: GameSimulation, cost: Cost): void {
  const he = sim.s.he;
  if (!he) return;
  for (const [k, v] of Object.entries(cost) as [ResourceId | 'gold', number][]) {
    if (!v) continue;
    if (k === 'gold') {
      sim.s.gold -= v;
      sim.s.stats.goldSpent += v;
    } else he.resources[k] -= v;
  }
}

function gain(he: HeroEndlessState, cost: Cost): void {
  for (const [k, v] of Object.entries(cost) as [ResourceId | 'gold', number][]) {
    if (v && k !== 'gold') addResource(he, k, v);
  }
}

// ------------------------------------------------------------ Ausrüstung

export function equipItem(sim: GameSimulation, uid: number, slotIndex?: number): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const c = he.champion;
  const inst = c.inventory.find((i) => i.uid === uid);
  if (!inst) return fail('Gegenstand nicht im Inventar');
  const def = ITEMS[inst.item];
  if (!canEquipItem(inst.item, c.id)) return fail('Dieser Held kann den Gegenstand nicht tragen');
  const idx = slotIndex ?? SLOTS.findIndex((s) => s.slot === def.slot);
  if (idx < 0 || idx >= SLOTS.length) return fail('Unbekannter Slot');
  if (SLOTS[idx].slot !== def.slot) return fail(`Passt nicht in den Slot „${SLOTS[idx].name}“`);
  if (idx === 5 && c.specialPotion) return fail('Spezialslot ist mit einem Trank belegt');
  const before = championStats(he);
  // Ein Item kann nur in einem Slot liegen
  for (let i = 0; i < c.equipment.length; i++) if (c.equipment[i] === uid) c.equipment[i] = null;
  c.equipment[idx] = uid;
  keepRatios(he, before);
  sim.c.champBuffSig = '';
  return { ok: true };
}

export function unequipSlot(sim: GameSimulation, slotIndex: number): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const c = he.champion;
  if (slotIndex === 5 && c.specialPotion) {
    const p = c.specialPotion;
    c.specialPotion = null;
    if (!addPotion(he, p.potion, p.count)) {
      c.specialPotion = p;
      return fail('Kein Platz für den Trankstapel');
    }
    return { ok: true };
  }
  if (c.equipment[slotIndex] === null || c.equipment[slotIndex] === undefined) return fail('Slot ist leer');
  const before = championStats(he);
  c.equipment[slotIndex] = null;
  keepRatios(he, before);
  sim.c.champBuffSig = '';
  return { ok: true };
}

/** LP und Mana behalten beim Ausrüstungswechsel ihren Anteil (kein Heilen durch An-/Ablegen). */
function keepRatios(he: HeroEndlessState, before: ChampionStats): void {
  const after = championStats(he);
  const c = he.champion;
  c.hp = Math.min(after.maxHp, (c.hp / Math.max(1, before.maxHp)) * after.maxHp);
  c.mana = Math.min(after.maxMana, before.maxMana > 0 ? (c.mana / before.maxMana) * after.maxMana : c.mana);
}

/** Legt einen Trankstapel in den Spezialslot (Schnellzugriff). */
export function slotPotion(sim: GameSimulation, potion: PotionId): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const c = he.champion;
  if (c.equipment[5] !== null) return fail('Spezialslot ist belegt');
  const i = c.potions.findIndex((p) => p.potion === potion);
  if (i < 0) return fail('Trank nicht im Inventar');
  if (c.specialPotion) {
    const old = c.specialPotion;
    c.specialPotion = c.potions[i];
    c.potions.splice(i, 1, old);
  } else {
    c.specialPotion = c.potions[i];
    c.potions.splice(i, 1);
  }
  return { ok: true };
}

// ------------------------------------------------------------ Tränke

export function potionState(sim: GameSimulation, potion: PotionId): { ok: boolean; reason: string | null; count: number } {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held', count: 0 };
  const c = he.champion;
  const count = (c.potions.find((p) => p.potion === potion)?.count ?? 0) + (c.specialPotion?.potion === potion ? c.specialPotion.count : 0);
  if (count <= 0) return { ok: false, reason: 'Keine Tränke', count };
  if (c.dead > 0) return { ok: false, reason: 'Held ist besiegt', count };
  if ((c.potionCd[potion] ?? 0) > 0) return { ok: false, reason: `Bereit in ${Math.ceil(c.potionCd[potion] ?? 0)} s`, count };
  return { ok: true, reason: null, count };
}

export function usePotion(sim: GameSimulation, potion: PotionId): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const chk = potionState(sim, potion);
  if (!chk.ok) return fail(chk.reason ?? 'Nicht möglich');
  const c = he.champion;
  const def = POTIONS[potion];
  const st = championStats(he);
  if (def.heal) c.hp = Math.min(st.maxHp, c.hp + st.maxHp * def.heal);
  if (def.mana) c.mana = Math.min(st.maxMana, c.mana + st.maxMana * def.mana);
  if (def.duration > 0) {
    const add: Partial<ChampionStats> = { ...(def.buff ?? {}) };
    const pct = POTION_PCT[potion];
    if (pct) {
      // Prozentwirkungen beziehen sich auf die Werte ohne diesen Trank (kein Aufschaukeln).
      const self = c.buffs.find((b) => b.src === `potion:${potion}`);
      for (const [k, v] of Object.entries(pct) as [keyof ChampionStats, number][]) add[k] = (st[k] - (self?.add[k] ?? 0)) * v;
    }
    if (def.tower) {
      for (const t of sim.s.towers) {
        if ((t.cx + 0.5 - c.x) ** 2 + (t.cy + 0.5 - c.y) ** 2 <= def.tower.radius ** 2) applyTowerBuff(sim, t.uid, `potion:${potion}`, { dmg: def.tower.dmg, speed: def.tower.speed }, def.duration);
      }
    } else {
      // Gleicher Trank erneuert die Dauer, verlängert aber nicht über die Grunddauer hinaus.
      c.buffs = c.buffs.filter((b) => b.src !== `potion:${potion}`);
      c.buffs.push({ src: `potion:${potion}`, t: def.duration, dur: def.duration, add });
    }
  }
  // Verbrauch: zuerst normales Inventar, dann Spezialslot
  const stack = c.potions.find((p) => p.potion === potion);
  if (stack) {
    stack.count--;
    if (stack.count <= 0) c.potions = c.potions.filter((p) => p !== stack);
  } else if (c.specialPotion?.potion === potion) {
    c.specialPotion.count--;
    if (c.specialPotion.count <= 0) c.specialPotion = null;
  }
  c.potionCd[potion] = def.cooldown;
  for (const p of Object.keys(POTIONS) as PotionId[]) c.potionCd[p] = Math.max(c.potionCd[p] ?? 0, HERO_ENDLESS.potionGlobalCd);
  c.potionCd[potion] = def.cooldown;
  he.stats.potionsUsed++;
  sim.emit({ k: 'potion', potion, x: c.x, y: c.y });
  sim.c.champBuffSig = '';
  return { ok: true };
}

// ------------------------------------------------------------ Laden

export function offerState(sim: GameSimulation, offer: ShopOffer): { ok: boolean; reason: string | null; left: number | null } {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held', left: null };
  const left = offer.stock !== undefined ? he.shopStock[offer.id] ?? offer.stock : null;
  if (offer.perk && !he.perks.includes(offer.perk)) return { ok: false, reason: 'Noch nicht freigeschaltet', left };
  if (left !== null && left <= 0) return { ok: false, reason: 'Ausverkauft', left };
  if (!nearShop(sim)) return { ok: false, reason: 'Held ist nicht am Laden', left };
  if (!canAfford(sim, offer.cost)) return { ok: false, reason: 'Nicht genug Ressourcen', left };
  if (offer.give.randomFragment && missingItems(he, Math.max(sim.s.wave, he.bestWaveAtStart)).length === 0) return { ok: false, reason: 'Alle Items sind bereits freigeschaltet', left };
  if (offer.give.potion) {
    const p = offer.give.potion;
    const have = he.champion.potions.find((x) => x.potion === p) ?? (he.champion.specialPotion?.potion === p ? he.champion.specialPotion : null);
    if (have ? have.count >= POTIONS[p].stack : he.champion.potions.length >= HERO_ENDLESS.potionStacks) return { ok: false, reason: 'Kein Platz für diesen Trank', left };
  }
  return { ok: true, reason: null, left };
}

export function buyOffer(sim: GameSimulation, offerId: string): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const offer = SHOP_OFFERS.find((o) => o.id === offerId);
  if (!offer) return fail('Unbekanntes Angebot');
  const chk = offerState(sim, offer);
  if (!chk.ok) return fail(chk.reason ?? 'Nicht möglich');
  pay(sim, offer.cost);
  if (offer.stock !== undefined) he.shopStock[offer.id] = (chk.left ?? offer.stock) - 1;
  const g = offer.give;
  if (g.potion) addPotion(he, g.potion, 1);
  if (g.resource) for (const [k, v] of Object.entries(g.resource) as [ResourceId, number][]) addResource(he, k, v);
  if (g.skillPoint) he.champion.skillPoints += g.skillPoint;
  if (g.randomFragment) {
    for (let k = 0; k < g.randomFragment; k++) {
      const miss = missingItems(he, Math.max(sim.s.wave, he.bestWaveAtStart));
      if (!miss.length) {
        addResource(he, 'essence', HERO_ENDLESS.fragmentToEssence);
        continue;
      }
      addFragments(sim, miss[Math.floor(lootRand(sim) * miss.length) % miss.length], 1);
    }
  }
  return { ok: true };
}

/** Items der Sammlung (dauerhaft freigeschaltet), die im Laden gekauft werden können. */
export function collectionOffers(he: HeroEndlessState): ItemId[] {
  // 3.4: im Laden nur Gegenstände, die der aktuelle Held tragen kann
  return [...new Set([...he.metaOwned, ...he.runUnlocks])].filter((id) => ITEMS[id] && canEquipItem(id, he.champion.id));
}

export function collectionState(sim: GameSimulation, item: ItemId): { ok: boolean; reason: string | null; cost: Cost } {
  const he = sim.s.he;
  const cost = { ...RARITY[ITEMS[item]?.rarity ?? 'common'].shopCost };
  if (!he) return { ok: false, reason: 'Kein Held', cost };
  if (!collectionOffers(he).includes(item)) return { ok: false, reason: 'Nicht in der Sammlung', cost };
  if (he.champion.inventory.some((i) => i.item === item)) return { ok: false, reason: 'Bereits im Inventar', cost };
  if (he.champion.inventory.length >= HERO_ENDLESS.inventorySlots + 6) return { ok: false, reason: 'Inventar voll', cost };
  if (!nearShop(sim)) return { ok: false, reason: 'Held ist nicht am Laden', cost };
  if (!canAfford(sim, cost)) return { ok: false, reason: 'Nicht genug Ressourcen', cost };
  return { ok: true, reason: null, cost };
}

export function buyCollectionItem(sim: GameSimulation, item: ItemId): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const chk = collectionState(sim, item);
  if (!chk.ok) return fail(chk.reason ?? 'Nicht möglich');
  pay(sim, chk.cost);
  addItemInstance(sim, he, item);
  return { ok: true };
}

export function sellItem(sim: GameSimulation, uid: number): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  if (!nearShop(sim)) return fail('Held ist nicht am Laden');
  const c = he.champion;
  const inst = c.inventory.find((i) => i.uid === uid);
  if (!inst) return fail('Gegenstand nicht gefunden');
  const before = championStats(he);
  for (let i = 0; i < c.equipment.length; i++) if (c.equipment[i] === uid) c.equipment[i] = null;
  c.inventory = c.inventory.filter((i) => i.uid !== uid);
  keepRatios(he, before);
  gain(he, sellValue(inst.item, inst.plus));
  sim.c.champBuffSig = '';
  return { ok: true };
}

// ------------------------------------------------------------ Herstellung

export function recipeState(sim: GameSimulation, r: RecipeDefinition): { ok: boolean; reason: string | null; have: number } {
  const he = sim.s.he;
  if (!he) return { ok: false, reason: 'Kein Held', have: 0 };
  const have = r.fragments ? he.fragments[r.fragments.item] ?? 0 : 0;
  if (r.champion && r.champion !== he.champion.id) return { ok: false, reason: 'Nur für einen anderen Helden', have };
  if (r.perk && !he.perks.includes(r.perk)) return { ok: false, reason: 'Benötigt den Perk „Meisterschmied“ (Welle 12)', have };
  if (r.minWave && Math.max(sim.s.wave, he.bestWaveAtStart) < r.minWave) return { ok: false, reason: `Ab Welle ${r.minWave}`, have };
  if (r.result && he.champion.inventory.some((i) => i.item === r.result)) return { ok: false, reason: 'Bereits im Inventar', have };
  if (r.fragments && have < r.fragments.count) return { ok: false, reason: `Fragmente ${have}/${r.fragments.count}`, have };
  for (const it of r.items ?? []) if (!he.champion.inventory.some((i) => i.item === it)) return { ok: false, reason: `Benötigt ${ITEMS[it]?.name ?? it}`, have };
  if (!r.items?.length && he.champion.inventory.length >= HERO_ENDLESS.inventorySlots + 6) return { ok: false, reason: 'Inventar voll', have };
  if (!canAfford(sim, r.cost)) return { ok: false, reason: 'Nicht genug Ressourcen', have };
  if (!nearShop(sim)) return { ok: false, reason: 'Nur an der Schmiede beim Laden', have };
  return { ok: true, reason: null, have };
}

export function craftRecipe(sim: GameSimulation, recipeId: string): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const r = RECIPES.find((x) => x.id === recipeId);
  if (!r || !r.result) return fail('Unbekanntes Rezept');
  const chk = recipeState(sim, r);
  if (!chk.ok) return fail(chk.reason ?? 'Nicht möglich');
  // Alle Prüfungen bestanden – jetzt atomar verbrauchen.
  const c = he.champion;
  const before = championStats(he);
  pay(sim, r.cost);
  if (r.fragments) {
    he.fragments[r.fragments.item] = (he.fragments[r.fragments.item] ?? 0) - r.fragments.count;
    he.fragmentDelta[r.fragments.item] = (he.fragmentDelta[r.fragments.item] ?? 0) - r.fragments.count;
  }
  let bestPlus = 0;
  for (const it of r.items ?? []) {
    const inst = c.inventory.find((i) => i.item === it);
    if (!inst) continue;
    bestPlus = Math.max(bestPlus, inst.plus);
    for (let i = 0; i < c.equipment.length; i++) if (c.equipment[i] === inst.uid) c.equipment[i] = null;
    c.inventory = c.inventory.filter((i) => i.uid !== inst.uid);
  }
  // Kombinationen behalten die halbe Verstärkung der besten Zutat (abgerundet).
  addItemInstance(sim, he, r.result, Math.floor(bestPlus / 2));
  keepRatios(he, before);
  he.stats.itemsCrafted++;
  sim.emit({ k: 'craft', item: r.result, label: ITEMS[r.result].name });
  sim.c.champBuffSig = '';
  return { ok: true };
}

export function upgradeState(sim: GameSimulation, uid: number): { ok: boolean; reason: string | null; cost: Cost } {
  const he = sim.s.he;
  const inst = he?.champion.inventory.find((i) => i.uid === uid);
  const cost = upgradeCost(inst?.plus ?? 0);
  if (!he || !inst) return { ok: false, reason: 'Gegenstand nicht gefunden', cost };
  if (inst.plus >= MAX_PLUS) return { ok: false, reason: `Maximal +${MAX_PLUS}`, cost };
  if (!nearShop(sim)) return { ok: false, reason: 'Nur an der Schmiede beim Laden', cost };
  if (!canAfford(sim, cost)) return { ok: false, reason: 'Nicht genug Ressourcen', cost };
  return { ok: true, reason: null, cost };
}

export function upgradeItem(sim: GameSimulation, uid: number): CommandResult {
  const he = sim.s.he;
  if (!he) return fail('Kein Held');
  const chk = upgradeState(sim, uid);
  if (!chk.ok) return fail(chk.reason ?? 'Nicht möglich');
  const inst = he.champion.inventory.find((i) => i.uid === uid);
  if (!inst) return fail('Gegenstand nicht gefunden');
  const before = championStats(he);
  pay(sim, chk.cost);
  inst.plus++;
  keepRatios(he, before);
  sim.emit({ k: 'craft', item: inst.item, label: `${ITEMS[inst.item].name} +${inst.plus}` });
  sim.c.champBuffSig = '';
  return { ok: true };
}

export { ownedItems };
