// Übersetzung von sim/HeroEndlessSystem.ts.
// Rahmen des Helden-Endlosmodus: Startzustand, automatischer Wellentakt, Wellenbelohnungen, Ladenbestand.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>TS: inline-Typ <c>HeroEndlessSetup.meta</c> – Schnappschuss des dauerhaften Fortschritts beim Start.</summary>
    public class HeroEndlessSetupMeta
    {
        public double bestWave;
        public List<string> unlockedItems = new List<string>();   // ItemId[]
        public Dictionary<string, double> fragments = new Dictionary<string, double>();   // Partial<Record<ItemId, number>>
        public Dictionary<string, double> passive;   // optional
        public List<string> campaignDone;            // optional
        /// <summary>2.7: dauerhafter Stand des gewählten Helden und das gemeinsame Paragon.</summary>
        public ChampionProgress champ;               // optional
        /// <summary>TS: { level; xp; points } (strukturgleich mit ParagonProgress), optional.</summary>
        public ParagonProgress paragon;
        /// <summary>3.0: dauerhafte Turm-Meisterschaft (Abschüsse je Turmtyp).</summary>
        public Dictionary<string, double> mastery;   // optional
    }

    public class HeroEndlessSetup
    {
        public string champion;   // ChampionId
        /// <summary>Gewünschte Sammlungsitems für den Start (werden auf die Rucksack-Plätze gekürzt).</summary>
        public List<string> startKit = new List<string>();
        /// <summary>Schnappschuss des dauerhaften Fortschritts beim Start.</summary>
        public HeroEndlessSetupMeta meta;
        public string runId;
        /// <summary>Gewählte Doktrin (Doctrines.DOCTRINES), null = Gleichgewicht.</summary>
        public string doctrine;
    }

    public static partial class HeroEndlessSystem
    {
        private static readonly int[][] DIRS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };

        /// <summary>Sucht eine Hindernis-Zelle nahe dem Kern mit begehbarem Nachbarn; sonst wird eine freie Zelle umgewandelt.</summary>
        public static Vec2 placeShop(MapDefinition map)
        {
            int w = (int)map.width;
            int h = (int)map.height;
            Func<int, bool> walk = (i) => map.cells[i] != Cell.Blocked;
            int[] dist = new int[w * h];
            for (int k = 0; k < dist.Length; k++) dist[k] = -1;
            int start = (int)(map.goal.y * w + map.goal.x);
            dist[start] = 0;
            List<int> q = new List<int> { start };
            for (int qi = 0; qi < q.Count; qi++)
            {
                int cur = q[qi];
                int cx = cur % w;
                int cy = (cur - cx) / w;
                foreach (int[] dd in DIRS)
                {
                    int nx = cx + dd[0];
                    int ny = cy + dd[1];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx;
                    if (dist[ni] >= 0 || !walk(ni)) continue;
                    dist[ni] = dist[cur] + 1;
                    q.Add(ni);
                }
            }
            // 1) Hindernis neben erreichbarer Zelle, Abstand ≥ 2 zum Kern, nicht am Kartenrand
            int best = -1;
            double bestD = double.PositiveInfinity;
            for (int i = 0; i < w * h; i++)
            {
                if (map.cells[i] != Cell.Blocked) continue;
                int x = i % w;
                int y = (i - x) / w;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1) continue;
                double d = double.PositiveInfinity;
                foreach (int[] dd in DIRS)
                {
                    int ni = (y + dd[1]) * w + x + dd[0];
                    if (dist[ni] >= 0) d = U.Min(d, dist[ni]);
                }
                if (d < 1 || !U.IsFinite(d)) continue;
                double score = Math.Abs(d - 2.5);
                if (score < bestD)
                {
                    bestD = score;
                    best = i;
                }
            }
            // 2) Rückfall: freie Zelle in Kernnähe, deren Umwandlung keinen Weg blockiert
            if (best < 0 || bestD > 4)
            {
                List<int> cand = U.StableSort(q.Where((i) => map.cells[i] == Cell.Open && dist[i] >= 2).ToList(), (a, b) => dist[a] - dist[b]);
                foreach (int i in cand)
                {
                    map.cells[i] = Cell.Blocked;
                    if (Pathfinding.hasPath(map, null))
                    {
                        best = i;
                        break;
                    }
                    map.cells[i] = Cell.Open;
                }
            }
            if (best < 0) best = start; // äußerster Notfall: Laden steht am Kern
            // Dekoration an der Ladenzelle entfernen
            List<double> dec = new List<double>();
            for (int k = 0; k + 2 < map.decor.Count; k += 3) if (map.decor[k] != best) { dec.Add(map.decor[k]); dec.Add(map.decor[k + 1]); dec.Add(map.decor[k + 2]); }
            map.decor = dec;
            return new Vec2(best % w, Math.Floor((double)best / w));
        }

        /// <summary>Kristalladern: freie, bebaubare Felder abseits von Goldadern, Leylinien, Start und Kern.</summary>
        private static void placeDeposits(GameState state)
        {
            MapDefinition m = state.map;
            if (m.deposits != null && m.deposits.Count > 0) return;
            double w = m.width;
            uint h = U.ToUint32(RngUtil.hashSeed(state.seed.hash, "deposits"));
            Func<double> rnd = () =>
            {
                // TS: h = (Math.imul(h ^ (h >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0; – bitgleich in uint (mod 2^32)
                unchecked
                {
                    uint x = h ^ (h >> 15);
                    h = x * 0x2c1b3c6dU + 0x9e3779b9U;
                }
                return (h >> 8) / 16777216.0;
            };
            List<double> cand = new List<double>();
            for (int i = 0; i < m.cells.Count; i++)
            {
                if (m.cells[i] != Cell.Open || m.veins.Contains(i) || m.ley.Contains(i)) continue;
                double x = i % w;
                double y = Math.Floor(i / w);
                if (Math.Abs(x - m.goal.x) + Math.Abs(y - m.goal.y) < 3 || Math.Abs(x - m.spawn.x) + Math.Abs(y - m.spawn.y) < 3) continue;
                cand.Add(i);
            }
            double lo = HeroEndless.HERO_ENDLESS.depositCount[0];
            double hi = HeroEndless.HERO_ENDLESS.depositCount[1];
            double n = U.Min(cand.Count, lo + Math.Floor(rnd() * (hi - lo + 1)) + (m.size == "huge" ? 2 : m.size == "large" ? 1 : 0));
            // zufällige Reihenfolge der Kandidaten, dann 2×2-Blöcke (passend für den Goldgräber)
            List<double> order = new List<double>();
            while (cand.Count > 0) order.Add(U.Splice(cand, Math.Floor(rnd() * cand.Count), 1)[0]);
            var taken = new HashSet<double>(m.veins);
            foreach (var l in m.ley) taken.Add(l);
            List<double> @out = WorldGenerator.pickBlocks(m, order, (int)n, taken, null);
            m.deposits = U.StableSort(@out, (a, b) => a - b);
        }

        private static ChampionState makeChampion(string id, double x, double y)
        {
            return new ChampionState
            {
                id = id, x = x, y = y, hx = x, hy = y, path = new List<double>(), hp = 1, mana = 0, level = 1, xp = 0, skillPoints = 0, tree = new Dictionary<string, double>(), skillCd = new List<double> { 0, 0, 0, 0 },
                attackCd = 0, target = 0, facing = 0, anim = 0, buffs = new List<ChampionBuff>(), equipment = new List<double?> { null, null, null, null, null, null }, specialPotion = null,
                inventory = new List<ItemInstance>(), potions = new List<PotionStack>(), potionCd = new Dictionary<string, double>(), dead = 0, channel = 0, channelTick = 0, kills = 0, damageDealt = 0,
            };
        }

        /// <summary>Ergänzt einen frisch erzeugten Spielzustand um den Helden-Endlos-Teil.</summary>
        public static void initHeroEndless(GameState state, HeroEndlessSetup setup)
        {
            HeroEndlessSetupMeta meta = setup.meta;
            List<string> perks = HeroEndless.activePerks(meta.bestWave);
            string champ = setup.champion != null && Champions.CHAMPIONS.ContainsKey(setup.champion) && Champions.championUnlocked(setup.champion, perks, meta.bestWave, meta.campaignDone ?? new List<string>()) ? setup.champion : "lyra";
            Vec2 shop = placeShop(state.map);
            Vec2 home = ChampionSystem.championHome(state.map, shop);
            Dictionary<string, double> resources = new Dictionary<string, double>();
            foreach (string r in HeroEndless.RESOURCE_ORDER) resources[r] = 0;
            List<string> owned = new List<string>();
            {
                HashSet<string> seen = new HashSet<string>();
                foreach (string it in meta.unlockedItems.Where((x) => x != null && Items.ITEMS.ContainsKey(x) && Items.ITEMS[x] != null)) if (seen.Add(it)) owned.Add(it);
            }
            Dictionary<string, double> frags = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in U.Entries(meta.fragments))
            {
                if (Items.ITEMS.ContainsKey(kv.Key) && Items.ITEMS[kv.Key] != null && kv.Value > 0 && !owned.Contains(kv.Key)) frags[kv.Key] = kv.Value;
            }
            HeroEndlessState he = new HeroEndlessState
            {
                runId = setup.runId,
                champion = makeChampion(champ, home.x, home.y),
                pickups = new List<Pickup>(),
                zones = new List<ChampionZone>(),
                resources = resources,
                towerBuffs = new List<TowerBuff>(),
                shop = shop,
                autoTimer = HeroEndless.HERO_ENDLESS.prepTime,
                metaOwned = owned,
                runUnlocks = new List<string>(),
                fragments = frags,
                fragmentDelta = new Dictionary<string, double>(),
                perks = perks,
                bestWaveAtStart = meta.bestWave,
                shopStock = new Dictionary<string, double>(),
                stockWave = 0,
                profile = HeroEndless.profileForWave(state.seed.hash, 1).id,
                rewardedWave = 0,
                charges = TacticalCharges.START,
                doctrine = setup.doctrine != null && Doctrines.DOCTRINES.ContainsKey(setup.doctrine) ? setup.doctrine : Doctrines.DEFAULT,
                stats = new HeroEndlessRunStats { xpCollected = 0, itemsCrafted = 0, itemsFound = 0, potionsUsed = 0, resourcesEarned = 0, levelUps = 0 },
            };
            state.he = he;
            state.rng.loot = RngUtil.hashSeed(state.seed.hash, "loot");
            placeDeposits(state);
            // Startausrüstung: freigeschaltete Items bis zur Anzahl der Rucksack-Plätze, passend angelegt
            List<string> kitAll = new List<string>();
            {
                HashSet<string> seen = new HashSet<string>();
                foreach (string it in setup.startKit) if (seen.Add(it)) kitAll.Add(it);
            }
            List<string> kit = U.Slice(kitAll.Where((x) => owned.Contains(x)).ToList(), 0, HeroEndless.carrySlots(perks));
            ChampionState c = he.champion;
            foreach (string item in kit)
            {
                double uid = state.nextUid++;
                c.inventory.Add(new ItemInstance { uid = uid, item = item, plus = 0 });
                int slot = Items.SLOTS.FindIndex((s) => s.slot == Items.ITEMS[item].slot);
                if (slot >= 0 && c.equipment[slot] == null && Items.canEquipItem(item, champ)) c.equipment[slot] = uid;
            }
            // Dauerhafte Perks
            if (perks.Contains("pouch1")) LootSystem.addPotion(he, "heal", 2);
            if (perks.Contains("supply1"))
            {
                LootSystem.addResource(he, "metal", 6);
                LootSystem.addResource(he, "crystal", 2);
            }
            if (perks.Contains("supply2"))
            {
                LootSystem.addResource(he, "metal", 8);
                LootSystem.addResource(he, "crystal", 4);
                LootSystem.addResource(he, "essence", 2);
            }
            if (perks.Contains("gold")) state.gold += 80;
            he.passive = PassiveTree.sanitizePassive(meta.passive ?? new Dictionary<string, double>(), meta.bestWave);
            // 2.7: dauerhafte Heldenstufe und Skillbaum übernehmen, Paragon (gemeinsam) laden
            // Paragon: Vorab-Punkte ab Heldenstufe 50 und Meilensteinpunkte zählen zum Budget
            double lvlForPg = meta.champ != null ? U.Max(1, U.Min(Paragon.PARAGON.heroMaxLevel, U.Or(Math.Floor(meta.champ.level), 1))) : 1;
            he.paragonBonus = HeroEndless.milestoneParagon(meta.bestWave);
            ParagonProgress pg = Paragon.sanitizeParagon(meta.paragon, Paragon.paragonPrePoints(lvlForPg) + he.paragonBonus.Value);
            he.paragon = new HeroEndlessParagon { level = pg.level, xp = pg.xp, points = pg.points, rev = 0 };
            state.gold += HeroEndless.milestoneGold(meta.bestWave);
            he.mastery = Mastery.sanitizeMastery(meta.mastery ?? new Dictionary<string, double>());
            if (meta.champ != null)
            {
                double level = U.Max(1, U.Min(Paragon.PARAGON.heroMaxLevel, U.Or(Math.Floor(meta.champ.level), 1)));
                c.level = level;
                c.xp = level >= Paragon.PARAGON.heroMaxLevel ? 0 : U.Max(0, U.Min(HeroEndless.xpToNext(level) - 1, U.Or(meta.champ.xp, 0)));
                double spent = 0;
                foreach (var node in Champions.CHAMPIONS[champ].tree)
                {
                    double? tv = meta.champ.tree != null ? U.GetN(meta.champ.tree, node.id) : null;
                    double r = U.Max(0, U.Min(node.maxRank, Math.Floor(tv ?? 0)));
                    if (!U.Truthy(r)) continue;
                    c.tree[node.id] = r;
                    spent += r;
                    // Kernwall-Ränge wirken sofort auf die Kernenergie
                    if (U.Truthy(node.coreLives))
                    {
                        state.lives += node.coreLives.Value * r;
                        state.maxLives += node.coreLives.Value * r;
                    }
                }
                c.skillPoints = U.Max(0, ChampionSystem.skillPointsForLevel(level) - spent);
            }
            var pf = PassiveTree.runPassive(he);
            state.gold += pf.startGold;
            state.lives += pf.coreLives;
            state.maxLives += pf.coreLives;
            he.stats.resourcesEarned = 0;
            ChampionStats st = ChampionSystem.championStats(he);
            c.hp = st.maxHp;
            c.mana = st.maxMana;
        }

        /// <summary>Automatischer Wellentakt, Wellenbelohnungen und Ladenbestand. Läuft nur, wenn die Simulation läuft (Pause stoppt alles).</summary>
        /// <summary>Pause nach einer Bosswelle (s).</summary>
        public const double BOSS_PAUSE = 5;
        /// <summary>Nach so vielen Sekunden ohne jeden Fortschritt der letzten Gegner rückt die nächste Welle nach.</summary>
        public const double STALL_TIME = 8;

        /// <summary>
        /// Wächter gegen hängende Wellen: Sind alle Gegner erschienen und bewegt sich keiner der übrigen mehr
        /// (festgesteckt, unerreichbar), wird die Welle wie bei einem vorzeitigen Ruf abgeschlossen.
        /// </summary>
        private static void checkStall(GameSimulation sim, double dt)
        {
            GameState s = sim.s;
            HeroEndlessState he = s.he;
            if (s.spawnQueue.Count > 0) { he.stallT = 0; he.stallSum = -1; return; }
            double sum = 0;
            int alive = 0;
            foreach (var e in s.enemies)
            {
                if (!e.alive) continue;
                alive++;
                sum += e.remaining + (e.maxHp > 0 ? e.hp / e.maxHp * 4 : 0); // Schaden zählt auch als Fortschritt
            }
            if (alive == 0) { he.stallT = 0; he.stallSum = -1; return; }
            if (he.stallSum < 0 || sum < he.stallSum - 0.05) { he.stallSum = sum; he.stallT = 0; return; }
            he.stallT += dt;
            if (he.stallT < STALL_TIME) return;
            he.stallT = 0;
            he.stallSum = -1;
            if (WaveDirector.earlyCallState(s) != null) return; // z. B. Bosskampf läuft
            sim.emit(new GameEvent { k = "message", text = "Die letzten Gegner kommen nicht weiter – die nächste Welle rückt nach.", severity = "info" });
            WaveDirector.callNextWave(sim);
        }

        public static void updateHeroEndless(GameSimulation sim, double dt)
        {
            GameState s = sim.s;
            HeroEndlessState he = s.he;
            if (he == null) return;
            TacticalCharges.update(sim, dt);
            // 3.0: Nachrückende Wellen – ab Welle 120 rückt nach 45 s Kampf die nächste Welle nach
            if (s.phase == "combat" && s.wave >= Scaling.SCALING.overrunFrom)
            {
                if (he.overrunWave != s.wave)
                {
                    he.overrunWave = s.wave;
                    he.overrunT = 0;
                }
                he.overrunT = (he.overrunT ?? 0) + dt;
                if (he.overrunT >= Scaling.SCALING.overrunAfter && WaveDirector.earlyCallState(s) == null)
                {
                    he.overrunT = 0;
                    sim.emit(new GameEvent { k = "message", text = $"Welle {U.Fmt(s.wave)} dauert zu lange – Welle {U.Fmt(s.wave + 1)} rückt nach!", severity = "warn" });
                    WaveDirector.callNextWave(sim);
                }
            }
            // Wellenbelohnung genau einmal pro abgeschlossener Welle
            while (he.rewardedWave < s.stats.wavesCleared)
            {
                he.rewardedWave++;
                TacticalCharges.onWaveCleared(sim, he.rewardedWave);
                if (he.rewardedWave >= Runes.RUNE_RULES.splinterFrom && LootSystem.lootRand(sim) < Runes.RUNE_RULES.waveChance) RuneSystem.dropRune(sim, s.map.goal.x + 0.5, s.map.goal.y + 0.5);
                Dictionary<string, double> r = HeroEndless.HERO_ENDLESS.waveResources(he.rewardedWave);
                double total = 0;
                foreach (KeyValuePair<string, double> kv in U.Entries(r))
                {
                    if (kv.Value > 0)
                    {
                        double n = LootSystem.bonusAmount(sim, kv.Value, PassiveTree.runPassive(he).resourcePct);
                        LootSystem.addResource(he, kv.Key, n);
                        total += n;
                    }
                }
                if (total > 0) sim.emit(new GameEvent { k = "pickup", kind = "resource", x = he.champion.x, y = he.champion.y, label = $"Wellenbeute +{U.Fmt(total)}", color = 0xffd36b });
                // Kristalladern: Goldgräber auf einer Ader fördern zusätzlich Kristall
                foreach (TowerInstance t in s.towers)
                {
                    if (!TowersNew.MINES.Contains(t.def) || !EconomySystem.onDeposit(sim, t) || t.builtWave >= he.rewardedWave) continue;
                    double n = 1 + Math.Floor(he.rewardedWave / 8) + (t.level >= 3 ? 1 : 0);
                    LootSystem.addResource(he, "crystal", n);
                    sim.emit(new GameEvent { k = "pickup", kind = "resource", x = Towers.towerCenter(t).x, y = Towers.towerCenter(t).y, label = $"+{U.Fmt(n)} ◆ Kristallader", color = 0x7ae0ff });
                }
                // Neue Türme für die nächste Welle freigeschaltet?
                double next = he.rewardedWave + 1;
                List<string> fresh = U.Keys(PassiveTree.TOWER_UNLOCKS)
                    .Where((id) => Towers.TOWERS.ContainsKey(id) && Towers.TOWERS[id] != null && !(Towers.TOWERS[id].legendary == true) && (!(Towers.TOWERS[id].mazingOnly == true) || s.map.variant == "mazing") && PassiveTree.towerUnlockWave(id, he) == next).ToList();
                if (fresh.Count > 0) sim.emit(new GameEvent { k = "message", text = $"Neu freigeschaltet: {string.Join(", ", fresh.Select((id) => Towers.TOWERS[id].name))}", severity = "info" });
            }
            // Ladenbestand alle n Wellen auffrischen
            if (s.wave >= he.stockWave + HeroEndless.HERO_ENDLESS.stockEvery)
            {
                he.stockWave = s.wave - (s.wave % HeroEndless.HERO_ENDLESS.stockEvery);
                he.shopStock = new Dictionary<string, double>();
            }
            if (s.phase == "build")
            {
                he.profile = HeroEndless.profileForWave(s.seed.hash, s.wave + 1).id;
                // Nächste Welle sofort, nach einer Bosswelle 5 s Verschnaufpause
                // Erster Takt der Bauphase startet noch nicht – so ist der Zustand einen Takt lang sicherbar.
                if (he.autoTimer < 0) he.autoTimer = he.lastWaveBoss ? BOSS_PAUSE : dt * 0.5;
                else he.autoTimer -= dt;
                if (he.autoTimer <= 0)
                {
                    he.autoTimer = -1;
                    sim.startWave();
                }
            }
            else if (s.phase == "combat")
            {
                he.autoTimer = -1;
                checkStall(sim, dt);
            }
            // 'choice' (Modifikatorauswahl) hält den Countdown an, bis gewählt wurde.
        }
    }
}
