// Übersetzung von sim/LootSystem.ts.
// Beute des Helden-Endlosmodus: Erfahrung, Ressourcen, Tränke, Fragmente und Items als
// Bodenobjekte. Eigener, deterministischer Zufallsstrom (s.rng.loot). Jedes Objekt wird beim
// Aufsammeln sofort entfernt – eine Doppelvergabe ist ausgeschlossen.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class LootSystem
    {
        public static double lootRand(GameSimulation sim)
        {
            double[] r = RngUtil.step(sim.s.rng.loot ?? 0x51f15e);
            double v = r[0];
            double st = r[1];
            sim.s.rng.loot = st;
            return v;
        }

        /// <summary>Alle Items, die in dieser Partie als „vorhanden“ gelten (Sammlung, Partie, Inventar, am Boden).</summary>
        public static HashSet<string> ownedItems(HeroEndlessState he)
        {
            HashSet<string> set = new HashSet<string>();
            foreach (string i in he.metaOwned) set.Add(i);
            foreach (string i in he.runUnlocks) set.Add(i);
            foreach (ItemInstance i in he.champion.inventory) set.Add(i.item);
            foreach (Pickup p in he.pickups) if (p.kind == "item") set.Add(p.@ref);
            return set;
        }

        /// <summary>Noch nicht freigeschaltete, als Beute mögliche Items.</summary>
        public static List<string> missingItems(HeroEndlessState he, double wave)
        {
            HashSet<string> owned = ownedItems(he);
            // 3.4: nur Gegenstände, die der aktuelle Held tragen kann (Beute, Fragmente, Ladenbündel)
            return Items.ITEM_ORDER.Where((id) => !owned.Contains(id) && !(Items.ITEMS[id].craftOnly == true) && Items.ITEMS[id].minWave <= U.Max(1, wave) && Items.canEquipItem(id, he.champion.id)).ToList();
        }

        private static T pick<T>(GameSimulation sim, List<T> arr)
        {
            return arr[(int)(Math.Floor(lootRand(sim) * arr.Count) % arr.Count)];
        }

        private static void spawnPickup(GameSimulation sim, string kind, double x, double y, double amount, string @ref)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            double a = lootRand(sim) * Math.PI * 2;
            double d = kind == "xp" ? 0.15 : 0.25 + lootRand(sim) * 0.35;
            he.pickups.Add(new Pickup { uid = sim.uid(), kind = kind, x = x + Math.Cos(a) * d, y = y + Math.Sin(a) * d, amount = amount, @ref = @ref, t = U.GetN(HeroEndless.HERO_ENDLESS.pickupLife, kind) ?? 40 });
            // Obergrenze: bei sehr vielen Bodenobjekten verfallen zuerst die ältesten Erfahrungskugeln.
            if (he.pickups.Count > 160)
            {
                int i = he.pickups.FindIndex((p) => p.kind == "xp");
                if (i >= 0) he.pickups.RemoveAt(i);
            }
        }

        /// <summary>Wählt ein Fragmentziel unter den fehlenden Items (seltene Items erst später häufiger).</summary>
        private static string fragmentTarget(GameSimulation sim, HeroEndlessState he)
        {
            List<string> miss = missingItems(he, sim.s.wave);
            if (miss.Count == 0) return null;
            List<double> weights = miss.Select((id) =>
            {
                double o = Items.RARITY[Items.ITEMS[id].rarity].order;
                return U.Max(0.2, 3 - o + U.Min(3, sim.s.wave / 10) * o * 0.5);
            }).ToList();
            double sum = 0;
            for (int k = 0; k < weights.Count; k++) sum = sum + weights[k];
            double r = lootRand(sim) * sum;
            for (int i = 0; i < miss.Count; i++)
            {
                r -= weights[i];
                if (r <= 0) return miss[i];
            }
            return miss[miss.Count - 1];
        }

        public static List<string> availablePotions(HeroEndlessState he)
        {
            return Items.POTION_ORDER.Where((p) =>
            {
                string perk;
                bool has = HeroEndless.POTIONS_BY_PERK.TryGetValue(p, out perk) && !string.IsNullOrEmpty(perk);
                return !has || he.perks.Contains(perk);
            }).ToList();
        }

        /// <summary>Beute beim Tod eines Gegners (nur im Helden-Endlosmodus).</summary>
        public static void onEnemyLoot(GameSimulation sim, EnemyInstance e)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            double wave = U.Max(1, sim.s.wave);
            EnemyDefinition def = Enemies.ENEMIES[e.def];
            string tier = !string.IsNullOrEmpty(e.boss) ? "boss" : e.miniBoss == true ? "miniBoss" : e.elite ? "elite" : "normal";
            var LOOT = HeroEndless.LOOT;
            double luck = 1 + (he.champion != null ? lootLuck(he) : 0);
            // Erfahrung
            double xp = LOOT.xpPerThreat * (!string.IsNullOrEmpty(e.boss) ? 6 : def.threat) * (1 + LOOT.xpWaveGrowth * (wave - 1));
            if (tier == "elite") xp *= LOOT.eliteXpMul;
            if (tier == "miniBoss") xp *= LOOT.miniBossXpMul;
            if (tier == "boss") xp *= LOOT.bossXpMul;
            spawnPickup(sim, "xp", e.x, e.y, U.Round(xp), "xp");
            // Ressourcen
            if (tier != "normal" || lootRand(sim) < LOOT.resourceChance * luck)
            {
                string res = tier == "boss" || tier == "miniBoss" ? (lootRand(sim) < 0.35 ? "relic" : "essence") : tier == "elite" ? "essence" : LOOT.resourceOf(e.def);
                double amount = res == "relic" ? 1 : bonusAmount(sim, LOOT.resourceAmount(wave) * (tier == "normal" ? 1 : 2), PassiveTree.runPassive(he).resourcePct);
                spawnPickup(sim, "resource", e.x, e.y, amount, res);
            }
            // Tränke
            if (lootRand(sim) < LOOT.potionChance * luck * (tier == "normal" ? 1 : 4)) spawnPickup(sim, "potion", e.x, e.y, 1, pick(sim, availablePotions(he)));
            // Fragmente
            if (lootRand(sim) < LOOT.fragmentChance[tier] * luck)
            {
                double n = tier == "boss" ? 3 : tier == "miniBoss" ? 2 : 1;
                for (int k = 0; k < n; k++)
                {
                    string target = fragmentTarget(sim, he);
                    if (!string.IsNullOrEmpty(target)) spawnPickup(sim, "fragment", e.x, e.y, 1, target);
                    else spawnPickup(sim, "resource", e.x, e.y, HeroEndless.HERO_ENDLESS.fragmentToEssence, "essence");
                }
            }
            // Vollständige Items – nie ein bereits vorhandenes (Sammlung, Partie, Inventar, am Boden)
            if (LOOT.itemChance[tier] > 0 && lootRand(sim) < LOOT.itemChance[tier] * luck)
            {
                List<string> miss = missingItems(he, wave);
                if (miss.Count > 0) spawnPickup(sim, "item", e.x, e.y, 1, pick(sim, miss));
                else
                {
                    string target = fragmentTarget(sim, he);
                    if (!string.IsNullOrEmpty(target)) spawnPickup(sim, "fragment", e.x, e.y, 1, target);
                    else spawnPickup(sim, "resource", e.x, e.y, 3, "essence");
                }
            }
            // Runen (erst ab Welle 250)
            RuneSystem.rollEnemyRune(sim, tier, e.x, e.y);
        }

        /// <summary>Prozentbonus auf kleine Ganzzahlen: Rest wird deterministisch per Beute-Zufall gerundet.</summary>
        public static double bonusAmount(GameSimulation sim, double n, double pct)
        {
            if (pct <= 0 || n <= 0) return n;
            double exact = n * (1 + pct);
            double @base = Math.Floor(exact);
            return @base + (lootRand(sim) < exact - @base ? 1 : 0);
        }

        private static double lootLuck(HeroEndlessState he)
        {
            // Glücksbonus aus Ausrüstung/Perks; bewusst ohne championStats-Import (keine Zyklen): Itemwert direkt.
            double luck = (he.perks.Contains("fortune") ? 0.25 : 0) + PassiveTree.runPassive(he).lootPct;
            foreach (double? uid in he.champion.equipment)
            {
                ItemInstance inst = uid == null ? null : he.champion.inventory.Find((i) => i.uid == uid);
                if (inst != null)
                {
                    ItemDefinition idef;
                    double? lb = Items.ITEMS.TryGetValue(inst.item, out idef) && idef != null ? U.GetN(idef.stats, "lootBonus") : null;
                    luck += (lb ?? 0) * (1 + 0.15 * inst.plus);
                }
            }
            return luck;
        }

        public static void updatePickups(GameSimulation sim, double dt)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || he.pickups.Count == 0) return;
            foreach (Pickup p in he.pickups) p.t -= dt;
            if (he.pickups.Any((p) => p.t <= 0)) he.pickups = he.pickups.Where((p) => p.t > 0).ToList();
        }

        /// <summary>Fügt Fragmente hinzu; Fragmente bereits vorhandener Items werden zu Essenz.</summary>
        public static void addFragments(GameSimulation sim, string item, double n)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            if (ownedItems(he).Contains(item))
            {
                addResource(he, "essence", HeroEndless.HERO_ENDLESS.fragmentToEssence * n);
                return;
            }
            he.fragments[item] = (U.GetN(he.fragments, item) ?? 0) + n;
            he.fragmentDelta[item] = (U.GetN(he.fragmentDelta, item) ?? 0) + n;
        }

        public static void addResource(HeroEndlessState he, string r, double n)
        {
            he.resources[r] = (U.GetN(he.resources, r) ?? 0) + n;
            if (n > 0) he.stats.resourcesEarned += n;
        }

        /// <summary>Sammelt Beute im Radius ein. Rückgabe: Anzahl gesammelter Objekte.</summary>
        public static double collectPickups(GameSimulation sim, double radius)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || he.pickups.Count == 0) return 0;
            ChampionState c = he.champion;
            double r2 = radius * radius;
            double pull2 = Math.Pow(radius * 2.2, 2);
            double n = 0;
            List<Pickup> keep = new List<Pickup>();
            List<Pickup> list = he.pickups;
            // PORT: Index-Schleife (for…of in JS sieht während der Schleife angehängte Elemente).
            for (int idx = 0; idx < list.Count; idx++)
            {
                Pickup p = list[idx];
                double d2 = Math.Pow(p.x - c.x, 2) + Math.Pow(p.y - c.y, 2);
                if (d2 > r2)
                {
                    // Leichter Sog in der Nähe des Helden
                    if (d2 <= pull2)
                    {
                        double d = U.Or(Math.Sqrt(d2), 1);
                        p.x += ((c.x - p.x) / d) * 0.05;
                        p.y += ((c.y - p.y) / d) * 0.05;
                    }
                    keep.Add(p);
                    continue;
                }
                if (!applyPickup(sim, he, p))
                {
                    keep.Add(p);
                    continue;
                }
                n++;
            }
            he.pickups = keep;
            return n;
        }

        private static bool applyPickup(GameSimulation sim, HeroEndlessState he, Pickup p)
        {
            string label = "";
            double color = 0xffffff;
            switch (p.kind)
            {
                case "xp":
                    // Erfahrung wird vom ChampionSystem gutgeschrieben (Import vermeiden: über Ereignis + Hook)
                    grantXpHook?.Invoke(sim, p.amount);
                    label = $"+{U.Fmt(p.amount)} EP";
                    color = 0x8affb0;
                    break;
                case "resource":
                    {
                        string r = p.@ref;
                        addResource(he, r, p.amount);
                        label = $"+{U.Fmt(p.amount)} {HeroEndless.RESOURCES[r].name}";
                        color = HeroEndless.RESOURCES[r].color;
                        break;
                    }
                case "potion":
                    {
                        bool ok = addPotion(he, p.@ref, p.amount);
                        if (!ok) return false;
                        label = Items.POTIONS[p.@ref].name;
                        color = Items.POTIONS[p.@ref].color;
                        break;
                    }
                case "fragment":
                    {
                        addFragments(sim, p.@ref, p.amount);
                        ItemDefinition fdef;
                        string fname = Items.ITEMS.TryGetValue(p.@ref, out fdef) && fdef != null ? fdef.name : null;
                        label = $"Fragment: {fname ?? p.@ref}";
                        color = 0xc080ff;
                        break;
                    }
                case "item":
                    {
                        if (!addItemInstance(sim, he, p.@ref))
                        {
                            if (!U.Truthy(he.stats.itemsFound) || sim.s.tick % 60 == 0) sim.emit(new GameEvent { k = "message", text = "Inventar voll – Gegenstand bleibt liegen.", severity = "warn" });
                            return false;
                        }
                        he.stats.itemsFound++;
                        ItemDefinition idef;
                        string iname = Items.ITEMS.TryGetValue(p.@ref, out idef) && idef != null ? idef.name : null;
                        label = $"{iname ?? p.@ref}!";
                        color = 0xffc040;
                        break;
                    }
            }
            sim.emit(new GameEvent { k = "pickup", kind = p.kind, x = p.x, y = p.y, label = label, color = color });
            return true;
        }

        /// <summary>Wird vom ChampionSystem gesetzt (verhindert einen Importzyklus).</summary>
        public static Action<GameSimulation, double> grantXpHook = null;

        public static void setGrantXpHook(Action<GameSimulation, double> fn)
        {
            grantXpHook = fn;
        }

        public static bool addPotion(HeroEndlessState he, string potion, double n)
        {
            PotionDefinition def;
            if (potion == null || !Items.POTIONS.TryGetValue(potion, out def) || def == null) return false;
            PotionStack st = he.champion.potions.Find((x) => x.potion == potion);
            if (st != null)
            {
                if (st.count + n > def.stack) return false;
                st.count += n;
                return true;
            }
            if (he.champion.specialPotion != null && he.champion.specialPotion.potion == potion)
            {
                if (he.champion.specialPotion.count + n > def.stack) return false;
                he.champion.specialPotion.count += n;
                return true;
            }
            if (he.champion.potions.Count >= HeroEndless.HERO_ENDLESS.potionStacks) return false;
            he.champion.potions.Add(new PotionStack { potion = potion, count = n });
            return true;
        }

        /// <summary>
        /// Legt eine Item-Instanz an. Je Item existiert in einer Partie höchstens eine Instanz;
        /// neue Items werden als Freischaltung der Partie vorgemerkt (Sammlung beim Abschluss).
        /// </summary>
        public static bool addItemInstance(GameSimulation sim, HeroEndlessState he, string item, double plus = 0)
        {
            ItemDefinition def;
            if (item == null || !Items.ITEMS.TryGetValue(item, out def) || def == null) return false;
            if (he.champion.inventory.Any((i) => i.item == item)) return false;
            if (he.champion.inventory.Count >= HeroEndless.HERO_ENDLESS.inventorySlots + 6) return false;
            he.champion.inventory.Add(new ItemInstance { uid = sim.uid(), item = item, plus = plus });
            if (!he.metaOwned.Contains(item) && !he.runUnlocks.Contains(item))
            {
                he.runUnlocks.Add(item);
                // Übrige Fragmente dieses Items werden zu Essenz.
                double left = U.GetN(he.fragments, item) ?? 0;
                if (left > 0)
                {
                    he.fragments[item] = 0;
                    he.fragmentDelta[item] = (U.GetN(he.fragmentDelta, item) ?? 0) - left;
                    addResource(he, "essence", left * HeroEndless.HERO_ENDLESS.fragmentToEssence);
                }
            }
            return true;
        }
    }
}
