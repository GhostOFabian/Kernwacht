// Übersetzung von sim/InventorySystem.ts.
// Ausrüstung, Tränke, Laden und Herstellung des Helden-Endlosmodus. Jede Transaktion prüft zuerst
// alle Bedingungen und verändert den Zustand erst danach – fehlgeschlagene Vorgänge verlieren nichts.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>TS: Rückgabe von potionState: { ok; reason: string | null; count }.</summary>
    public class PotionCheck
    {
        public bool ok;
        public string reason;
        public double count;
    }

    /// <summary>TS: Rückgabe von offerState: { ok; reason: string | null; left: number | null }.</summary>
    public class OfferCheck
    {
        public bool ok;
        public string reason;
        public double? left;
    }

    /// <summary>TS: Rückgabe von collectionState/upgradeState: { ok; reason: string | null; cost: Cost }.</summary>
    public class CostCheck
    {
        public bool ok;
        public string reason;
        public Dictionary<string, double> cost = new Dictionary<string, double>();   // Cost
    }

    /// <summary>TS: Rückgabe von recipeState: { ok; reason: string | null; have: number }.</summary>
    public class RecipeCheck
    {
        public bool ok;
        public string reason;
        public double have;
    }

    public static partial class InventorySystem
    {
        private static CommandResult fail(string reason) { return CommandResult.Fail(reason); }

        public static bool nearShop(GameSimulation sim)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return false;
            ChampionState c = he.champion;
            if (c.dead > 0) return false;
            return Math.Pow(c.x - (he.shop.x + 0.5), 2) + Math.Pow(c.y - (he.shop.y + 0.5), 2) <= Math.Pow(HeroEndless.HERO_ENDLESS.shopRange, 2);
        }

        // ------------------------------------------------------------ Kosten

        public static bool canAfford(GameSimulation sim, Dictionary<string, double> cost)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return false;
            foreach (KeyValuePair<string, double> kv in U.Entries(cost))
            {
                string k = kv.Key;
                double v = kv.Value;
                if (!U.Truthy(v)) continue;
                if (k == "gold" ? sim.s.gold < v : (U.GetN(he.resources, k) ?? 0) < v) return false;
            }
            return true;
        }

        private static void pay(GameSimulation sim, Dictionary<string, double> cost)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            foreach (KeyValuePair<string, double> kv in U.Entries(cost))
            {
                string k = kv.Key;
                double v = kv.Value;
                if (!U.Truthy(v)) continue;
                if (k == "gold")
                {
                    sim.s.gold -= v;
                    sim.s.stats.goldSpent += v;
                }
                else he.resources[k] = U.Get(he.resources, k, double.NaN) - v;   // TS: undefined - v = NaN
            }
        }

        private static void gain(HeroEndlessState he, Dictionary<string, double> cost)
        {
            foreach (KeyValuePair<string, double> kv in U.Entries(cost))
            {
                if (U.Truthy(kv.Value) && kv.Key != "gold") LootSystem.addResource(he, kv.Key, kv.Value);
            }
        }

        // ------------------------------------------------------------ Ausrüstung

        public static CommandResult equipItem(GameSimulation sim, double uid, double? slotIndex = null)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            ChampionState c = he.champion;
            ItemInstance inst = c.inventory.Find((i) => i.uid == uid);
            if (inst == null) return fail("Gegenstand nicht im Inventar");
            ItemDefinition def = Items.ITEMS[inst.item];
            if (!Items.canEquipItem(inst.item, c.id)) return fail("Dieser Held kann den Gegenstand nicht tragen");
            double idx = slotIndex ?? Items.SLOTS.FindIndex((s) => s.slot == def.slot);
            if (idx < 0 || idx >= Items.SLOTS.Count) return fail("Unbekannter Slot");
            if (Items.SLOTS[(int)idx].slot != def.slot) return fail($"Passt nicht in den Slot „{Items.SLOTS[(int)idx].name}“");
            if (idx == 5 && c.specialPotion != null) return fail("Spezialslot ist mit einem Trank belegt");
            ChampionStats before = ChampionSystem.championStats(he);
            // Ein Item kann nur in einem Slot liegen
            for (int i = 0; i < c.equipment.Count; i++) if (c.equipment[i] == uid) c.equipment[i] = null;
            c.equipment[(int)idx] = uid;
            keepRatios(he, before);
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        public static CommandResult unequipSlot(GameSimulation sim, double slotIndex)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            ChampionState c = he.champion;
            if (slotIndex == 5 && c.specialPotion != null)
            {
                PotionStack p = c.specialPotion;
                c.specialPotion = null;
                if (!LootSystem.addPotion(he, p.potion, p.count))
                {
                    c.specialPotion = p;
                    return fail("Kein Platz für den Trankstapel");
                }
                return CommandResult.Ok();
            }
            // TS: c.equipment[slotIndex] ist undefined außerhalb des Bereichs
            bool inRange = slotIndex >= 0 && slotIndex < c.equipment.Count && slotIndex == Math.Floor(slotIndex);
            if (!inRange || c.equipment[(int)slotIndex] == null) return fail("Slot ist leer");
            ChampionStats before = ChampionSystem.championStats(he);
            c.equipment[(int)slotIndex] = null;
            keepRatios(he, before);
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        /// <summary>LP und Mana behalten beim Ausrüstungswechsel ihren Anteil (kein Heilen durch An-/Ablegen).</summary>
        private static void keepRatios(HeroEndlessState he, ChampionStats before)
        {
            ChampionStats after = ChampionSystem.championStats(he);
            ChampionState c = he.champion;
            c.hp = U.Min(after.maxHp, (c.hp / U.Max(1, before.maxHp)) * after.maxHp);
            c.mana = U.Min(after.maxMana, before.maxMana > 0 ? (c.mana / before.maxMana) * after.maxMana : c.mana);
        }

        /// <summary>Legt einen Trankstapel in den Spezialslot (Schnellzugriff).</summary>
        public static CommandResult slotPotion(GameSimulation sim, string potion)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            ChampionState c = he.champion;
            // TS: c.equipment[5] !== null (undefined zählt als belegt)
            if (c.equipment.Count <= 5 || c.equipment[5] != null) return fail("Spezialslot ist belegt");
            int i = c.potions.FindIndex((p) => p.potion == potion);
            if (i < 0) return fail("Trank nicht im Inventar");
            if (c.specialPotion != null)
            {
                PotionStack old = c.specialPotion;
                c.specialPotion = c.potions[i];
                U.Splice(c.potions, i, 1, old);
            }
            else
            {
                c.specialPotion = c.potions[i];
                U.Splice(c.potions, i, 1);
            }
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Tränke

        public static PotionCheck potionState(GameSimulation sim, string potion)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return new PotionCheck { ok = false, reason = "Kein Held", count = 0 };
            ChampionState c = he.champion;
            double count = (c.potions.Find((p) => p.potion == potion)?.count ?? 0) + (c.specialPotion?.potion == potion ? c.specialPotion.count : 0);
            if (count <= 0) return new PotionCheck { ok = false, reason = "Keine Tränke", count = count };
            if (c.dead > 0) return new PotionCheck { ok = false, reason = "Held ist besiegt", count = count };
            if ((U.GetN(c.potionCd, potion) ?? 0) > 0) return new PotionCheck { ok = false, reason = $"Bereit in {U.Fmt(Math.Ceiling(U.GetN(c.potionCd, potion) ?? 0))} s", count = count };
            return new PotionCheck { ok = true, reason = null, count = count };
        }

        public static CommandResult usePotion(GameSimulation sim, string potion)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            PotionCheck chk = potionState(sim, potion);
            if (!chk.ok) return fail(chk.reason ?? "Nicht möglich");
            ChampionState c = he.champion;
            PotionDefinition def = Items.POTIONS[potion];
            ChampionStats st = ChampionSystem.championStats(he);
            if (U.Truthy(def.heal)) c.hp = U.Min(st.maxHp, c.hp + st.maxHp * def.heal.Value);
            if (U.Truthy(def.mana)) c.mana = U.Min(st.maxMana, c.mana + st.maxMana * def.mana.Value);
            if (def.duration > 0)
            {
                Dictionary<string, double> add = new Dictionary<string, double>(def.buff ?? new Dictionary<string, double>());
                Dictionary<string, double> pct;
                if (Items.POTION_PCT.TryGetValue(potion, out pct) && pct != null)
                {
                    // Prozentwirkungen beziehen sich auf die Werte ohne diesen Trank (kein Aufschaukeln).
                    ChampionBuff self = c.buffs.Find((b) => b.src == $"potion:{potion}");
                    foreach (KeyValuePair<string, double> kv in U.Entries(pct)) add[kv.Key] = (st[kv.Key] - ((self != null ? U.GetN(self.add, kv.Key) : null) ?? 0)) * kv.Value;
                }
                if (def.tower != null)
                {
                    foreach (TowerInstance t in sim.s.towers)
                    {
                        if (Math.Pow(t.cx + 0.5 - c.x, 2) + Math.Pow(t.cy + 0.5 - c.y, 2) <= Math.Pow(def.tower.radius, 2)) ChampionSystem.applyTowerBuff(sim, t.uid, $"potion:{potion}", new TowerBuffValues { dmg = def.tower.dmg, speed = def.tower.speed }, def.duration);
                    }
                }
                else
                {
                    // Gleicher Trank erneuert die Dauer, verlängert aber nicht über die Grunddauer hinaus.
                    c.buffs = c.buffs.Where((b) => b.src != $"potion:{potion}").ToList();
                    c.buffs.Add(new ChampionBuff { src = $"potion:{potion}", t = def.duration, dur = def.duration, add = add });
                }
            }
            // Verbrauch: zuerst normales Inventar, dann Spezialslot
            PotionStack stack = c.potions.Find((p) => p.potion == potion);
            if (stack != null)
            {
                stack.count--;
                if (stack.count <= 0) c.potions = c.potions.Where((p) => p != stack).ToList();
            }
            else if (c.specialPotion?.potion == potion)
            {
                c.specialPotion.count--;
                if (c.specialPotion.count <= 0) c.specialPotion = null;
            }
            c.potionCd[potion] = def.cooldown;
            foreach (string p in U.Keys(Items.POTIONS)) c.potionCd[p] = U.Max(U.GetN(c.potionCd, p) ?? 0, HeroEndless.HERO_ENDLESS.potionGlobalCd);
            c.potionCd[potion] = def.cooldown;
            he.stats.potionsUsed++;
            sim.emit(new GameEvent { k = "potion", potion = potion, x = c.x, y = c.y });
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Laden

        public static OfferCheck offerState(GameSimulation sim, ShopOffer offer)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return new OfferCheck { ok = false, reason = "Kein Held", left = null };
            double? left = offer.stock != null ? (U.GetN(he.shopStock, offer.id) ?? offer.stock) : null;
            if (!string.IsNullOrEmpty(offer.perk) && !he.perks.Contains(offer.perk)) return new OfferCheck { ok = false, reason = "Noch nicht freigeschaltet", left = left };
            if (left != null && left <= 0) return new OfferCheck { ok = false, reason = "Ausverkauft", left = left };
            if (!nearShop(sim)) return new OfferCheck { ok = false, reason = "Held ist nicht am Laden", left = left };
            if (!canAfford(sim, offer.cost)) return new OfferCheck { ok = false, reason = "Nicht genug Ressourcen", left = left };
            if (U.Truthy(offer.give.randomFragment) && LootSystem.missingItems(he, U.Max(sim.s.wave, he.bestWaveAtStart)).Count == 0) return new OfferCheck { ok = false, reason = "Alle Items sind bereits freigeschaltet", left = left };
            if (!string.IsNullOrEmpty(offer.give.potion))
            {
                string p = offer.give.potion;
                PotionStack have = he.champion.potions.Find((x) => x.potion == p) ?? (he.champion.specialPotion?.potion == p ? he.champion.specialPotion : null);
                if (have != null ? have.count >= Items.POTIONS[p].stack : he.champion.potions.Count >= HeroEndless.HERO_ENDLESS.potionStacks) return new OfferCheck { ok = false, reason = "Kein Platz für diesen Trank", left = left };
            }
            return new OfferCheck { ok = true, reason = null, left = left };
        }

        public static CommandResult buyOffer(GameSimulation sim, string offerId)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            ShopOffer offer = Items.SHOP_OFFERS.Find((o) => o.id == offerId);
            if (offer == null) return fail("Unbekanntes Angebot");
            OfferCheck chk = offerState(sim, offer);
            if (!chk.ok) return fail(chk.reason ?? "Nicht möglich");
            pay(sim, offer.cost);
            if (offer.stock != null) he.shopStock[offer.id] = (chk.left ?? offer.stock.Value) - 1;
            var g = offer.give;
            if (!string.IsNullOrEmpty(g.potion)) LootSystem.addPotion(he, g.potion, 1);
            if (g.resource != null) foreach (KeyValuePair<string, double> kv in U.Entries(g.resource)) LootSystem.addResource(he, kv.Key, kv.Value);
            if (U.Truthy(g.skillPoint)) he.champion.skillPoints += g.skillPoint.Value;
            if (U.Truthy(g.randomFragment))
            {
                for (int k = 0; k < g.randomFragment.Value; k++)
                {
                    List<string> miss = LootSystem.missingItems(he, U.Max(sim.s.wave, he.bestWaveAtStart));
                    if (miss.Count == 0)
                    {
                        LootSystem.addResource(he, "essence", HeroEndless.HERO_ENDLESS.fragmentToEssence);
                        continue;
                    }
                    LootSystem.addFragments(sim, miss[(int)(Math.Floor(LootSystem.lootRand(sim) * miss.Count) % miss.Count)], 1);
                }
            }
            return CommandResult.Ok();
        }

        /// <summary>Items der Sammlung (dauerhaft freigeschaltet), die im Laden gekauft werden können.</summary>
        public static List<string> collectionOffers(HeroEndlessState he)
        {
            // 3.4: im Laden nur Gegenstände, die der aktuelle Held tragen kann
            List<string> uniq = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string id in he.metaOwned) if (seen.Add(id)) uniq.Add(id);
            foreach (string id in he.runUnlocks) if (seen.Add(id)) uniq.Add(id);
            return uniq.Where((id) => id != null && Items.ITEMS.ContainsKey(id) && Items.ITEMS[id] != null && Items.canEquipItem(id, he.champion.id)).ToList();
        }

        public static CostCheck collectionState(GameSimulation sim, string item)
        {
            HeroEndlessState he = sim.s.he;
            ItemDefinition idef;
            string rarity = (item != null && Items.ITEMS.TryGetValue(item, out idef) && idef != null ? idef.rarity : null) ?? "common";
            Dictionary<string, double> cost = new Dictionary<string, double>(Items.RARITY[rarity].shopCost);
            if (he == null) return new CostCheck { ok = false, reason = "Kein Held", cost = cost };
            if (!collectionOffers(he).Contains(item)) return new CostCheck { ok = false, reason = "Nicht in der Sammlung", cost = cost };
            if (he.champion.inventory.Any((i) => i.item == item)) return new CostCheck { ok = false, reason = "Bereits im Inventar", cost = cost };
            if (he.champion.inventory.Count >= HeroEndless.HERO_ENDLESS.inventorySlots + 6) return new CostCheck { ok = false, reason = "Inventar voll", cost = cost };
            if (!nearShop(sim)) return new CostCheck { ok = false, reason = "Held ist nicht am Laden", cost = cost };
            if (!canAfford(sim, cost)) return new CostCheck { ok = false, reason = "Nicht genug Ressourcen", cost = cost };
            return new CostCheck { ok = true, reason = null, cost = cost };
        }

        public static CommandResult buyCollectionItem(GameSimulation sim, string item)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            CostCheck chk = collectionState(sim, item);
            if (!chk.ok) return fail(chk.reason ?? "Nicht möglich");
            pay(sim, chk.cost);
            LootSystem.addItemInstance(sim, he, item);
            return CommandResult.Ok();
        }

        public static CommandResult sellItem(GameSimulation sim, double uid)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            if (!nearShop(sim)) return fail("Held ist nicht am Laden");
            ChampionState c = he.champion;
            ItemInstance inst = c.inventory.Find((i) => i.uid == uid);
            if (inst == null) return fail("Gegenstand nicht gefunden");
            ChampionStats before = ChampionSystem.championStats(he);
            for (int i = 0; i < c.equipment.Count; i++) if (c.equipment[i] == uid) c.equipment[i] = null;
            c.inventory = c.inventory.Where((i) => i.uid != uid).ToList();
            keepRatios(he, before);
            gain(he, Items.sellValue(inst.item, inst.plus));
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Herstellung

        public static RecipeCheck recipeState(GameSimulation sim, RecipeDefinition r)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return new RecipeCheck { ok = false, reason = "Kein Held", have = 0 };
            double have = r.fragments != null ? (U.GetN(he.fragments, r.fragments.item) ?? 0) : 0;
            if (!string.IsNullOrEmpty(r.champion) && r.champion != he.champion.id) return new RecipeCheck { ok = false, reason = "Nur für einen anderen Helden", have = have };
            if (!string.IsNullOrEmpty(r.perk) && !he.perks.Contains(r.perk)) return new RecipeCheck { ok = false, reason = "Benötigt den Perk „Meisterschmied“ (Welle 12)", have = have };
            if (U.Truthy(r.minWave) && U.Max(sim.s.wave, he.bestWaveAtStart) < r.minWave.Value) return new RecipeCheck { ok = false, reason = $"Ab Welle {U.Fmt(r.minWave.Value)}", have = have };
            if (!string.IsNullOrEmpty(r.result) && he.champion.inventory.Any((i) => i.item == r.result)) return new RecipeCheck { ok = false, reason = "Bereits im Inventar", have = have };
            if (r.fragments != null && have < r.fragments.count) return new RecipeCheck { ok = false, reason = $"Fragmente {U.Fmt(have)}/{U.Fmt(r.fragments.count)}", have = have };
            foreach (string it in r.items ?? new List<string>())
            {
                if (!he.champion.inventory.Any((i) => i.item == it))
                {
                    ItemDefinition idef;
                    string nm = it != null && Items.ITEMS.TryGetValue(it, out idef) && idef != null ? idef.name : null;
                    return new RecipeCheck { ok = false, reason = $"Benötigt {nm ?? it}", have = have };
                }
            }
            if ((r.items == null || r.items.Count == 0) && he.champion.inventory.Count >= HeroEndless.HERO_ENDLESS.inventorySlots + 6) return new RecipeCheck { ok = false, reason = "Inventar voll", have = have };
            if (!canAfford(sim, r.cost)) return new RecipeCheck { ok = false, reason = "Nicht genug Ressourcen", have = have };
            if (!nearShop(sim)) return new RecipeCheck { ok = false, reason = "Nur an der Schmiede beim Laden", have = have };
            return new RecipeCheck { ok = true, reason = null, have = have };
        }

        public static CommandResult craftRecipe(GameSimulation sim, string recipeId)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            RecipeDefinition r = Items.RECIPES.Find((x) => x.id == recipeId);
            if (r == null || string.IsNullOrEmpty(r.result)) return fail("Unbekanntes Rezept");
            RecipeCheck chk = recipeState(sim, r);
            if (!chk.ok) return fail(chk.reason ?? "Nicht möglich");
            // Alle Prüfungen bestanden – jetzt atomar verbrauchen.
            ChampionState c = he.champion;
            ChampionStats before = ChampionSystem.championStats(he);
            pay(sim, r.cost);
            if (r.fragments != null)
            {
                he.fragments[r.fragments.item] = (U.GetN(he.fragments, r.fragments.item) ?? 0) - r.fragments.count;
                he.fragmentDelta[r.fragments.item] = (U.GetN(he.fragmentDelta, r.fragments.item) ?? 0) - r.fragments.count;
            }
            double bestPlus = 0;
            foreach (string it in r.items ?? new List<string>())
            {
                ItemInstance inst = c.inventory.Find((i) => i.item == it);
                if (inst == null) continue;
                bestPlus = U.Max(bestPlus, inst.plus);
                for (int i = 0; i < c.equipment.Count; i++) if (c.equipment[i] == inst.uid) c.equipment[i] = null;
                c.inventory = c.inventory.Where((i) => i.uid != inst.uid).ToList();
            }
            // Kombinationen behalten die halbe Verstärkung der besten Zutat (abgerundet).
            LootSystem.addItemInstance(sim, he, r.result, Math.Floor(bestPlus / 2));
            keepRatios(he, before);
            he.stats.itemsCrafted++;
            sim.emit(new GameEvent { k = "craft", item = r.result, label = Items.ITEMS[r.result].name });
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        public static CostCheck upgradeState(GameSimulation sim, double uid)
        {
            HeroEndlessState he = sim.s.he;
            ItemInstance inst = he?.champion.inventory.Find((i) => i.uid == uid);
            Dictionary<string, double> cost = Items.upgradeCost(inst?.plus ?? 0);
            if (he == null || inst == null) return new CostCheck { ok = false, reason = "Gegenstand nicht gefunden", cost = cost };
            if (inst.plus >= Items.MAX_PLUS) return new CostCheck { ok = false, reason = $"Maximal +{U.Fmt(Items.MAX_PLUS)}", cost = cost };
            if (!nearShop(sim)) return new CostCheck { ok = false, reason = "Nur an der Schmiede beim Laden", cost = cost };
            if (!canAfford(sim, cost)) return new CostCheck { ok = false, reason = "Nicht genug Ressourcen", cost = cost };
            return new CostCheck { ok = true, reason = null, cost = cost };
        }

        public static CommandResult upgradeItem(GameSimulation sim, double uid)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return fail("Kein Held");
            CostCheck chk = upgradeState(sim, uid);
            if (!chk.ok) return fail(chk.reason ?? "Nicht möglich");
            ItemInstance inst = he.champion.inventory.Find((i) => i.uid == uid);
            if (inst == null) return fail("Gegenstand nicht gefunden");
            ChampionStats before = ChampionSystem.championStats(he);
            pay(sim, chk.cost);
            inst.plus++;
            keepRatios(he, before);
            sim.emit(new GameEvent { k = "craft", item = inst.item, label = $"{Items.ITEMS[inst.item].name} +{U.Fmt(inst.plus)}" });
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        /// <summary>TS: <c>export { ownedItems }</c> (Weiterexport aus LootSystem).</summary>
        public static HashSet<string> ownedItems(HeroEndlessState he) { return LootSystem.ownedItems(he); }
    }
}
