// Übersetzung von sim/EconomySystem.ts.
// Gold, Bau, Verkauf und Upgrades.
// Schutz vor Geldvermehrung: Erstattung ist nie höher als die tatsächliche Investition.
// Volle Erstattung nur für Türme, die in der laufenden Bauphase gebaut wurden ("Rückgängig").

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class BuildCheck
    {
        public bool ok;
        public string reason;
        public double cost;
        public bool affordable;
        public List<GridPos> route;
        public bool? blocks;
    }

    /// <summary>TS: Rückgabe von <c>starState</c> ({ ok, reason, cost }).</summary>
    public class StarState
    {
        public bool ok;
        public string reason;
        public double cost;
    }

    /// <summary>TS: <c>CommandResult &amp; { count; spent }</c> (starAllTowers, upgradeAllTowers).</summary>
    public class CountSpentResult : CommandResult
    {
        public double count;
        public double spent;
    }

    /// <summary>TS: <c>CommandResult &amp; { count; spent; left }</c> (groupSpecialize).</summary>
    public class GroupSpecializeResult : CommandResult
    {
        public double count;
        public double spent;
        public double left;
    }

    /// <summary>TS: <c>CommandResult &amp; { count; gold }</c> (sellAllTowers).</summary>
    public class SellAllResult : CommandResult
    {
        public double count;
        public double gold;
    }

    /// <summary>TS: Rückgabe von <c>laneCharges</c> ({ left, next }).</summary>
    public class LaneCharges
    {
        public double left;
        public double next;
    }

    /// <summary>TS: Konstante <c>PRESPEC</c> ({ from, premium }).</summary>
    public class PrespecConfig
    {
        public double from;
        public double premium;
    }

    public class FusionOption
    {
        public FusionRecipe recipe;
        public TowerInstance partner;
        public bool affordable;
    }

    /// <summary>TS: Element der Rückgabe von <c>fusionPreview</c>.</summary>
    public class FusionPreviewEntry
    {
        public FusionRecipe recipe;
        public string partnerDef;   // TowerId
        public List<TowerInstance> partners = new List<TowerInstance>();
    }

    /// <summary>TS: Rückgabe von <c>activeState</c> ({ available, reason }).</summary>
    public class ActiveState
    {
        public bool available;
        public string reason;
    }

    /// <summary>TS: Rückgabe von <c>upgradeAllCost</c> ({ total, steps }).</summary>
    public class UpgradeAllCost
    {
        public double total;
        public double steps;
    }

    /// <summary>TS: Rückgabe von <c>sellAllValue</c> ({ count, gold }).</summary>
    public class SellAllValue
    {
        public double count;
        public double gold;
    }

    /// <summary>TS: Element der Rückgabe von <c>fusionPairs</c>.</summary>
    public class FusionPair
    {
        public TowerInstance a;
        public TowerInstance b;
        public FusionRecipe recipe;
        public bool ready;
        public bool affordable;
        public TowerInstance target;
    }

    public static partial class EconomySystem
    {
        private static double Sq(double x) { return x * x; }

        /// <summary>Aktuelles Turmlimit inkl. Bonusplätzen aus Bosswellen (3.4).</summary>
        public static double currentTowerLimit(GameSimulation sim)
        {
            var s = sim.s;
            return Scaling.towerLimit(s.wave, s.rules.endless, s.rules.towerLimitOff ?? false) + (s.bossSlots ?? 0);
        }

        // ------------------------------------------------------------ 3.6 Veredelung, Bauplätze, Kernausbau

        /// <summary>Voll ausgebaut (Stufe 4 + ggf. Spezialisierung, oder legendär/ohne Stufen)?</summary>
        public static bool fullyBuilt(TowerInstance t)
        {
            var d = Towers.TOWERS[t.def];
            if (d.legendary == true) return true;
            if (t.level - 1 < d.upgrades.Count) return false;
            return d.specs.Count == 0 || t.spec != 0;
        }

        /// <summary>Preis für den nächsten Stern (Infinity = nicht möglich).</summary>
        public static double starCost(TowerInstance t)
        {
            var d = Towers.TOWERS[t.def];
            double n = t.stars ?? 0;
            if (n >= Goldsinks.STARS.max || d.attack == "none") return double.PositiveInfinity;
            double ups = 0;
            foreach (var u in d.upgrades) ups = ups + u.cost;
            double value = d.legendary == true ? Goldsinks.STARS.legendaryBase : (d.cost + ups + (d.specs.Count > 0 ? d.specs[0].cost : 0)) * Goldsinks.STARS.baseFactor;
            // PORT: Math.Pow (.NET) vs. JS Math.pow kann im letzten Bit abweichen; durch U.Round praktisch irrelevant.
            return U.Round(value * Math.Pow(Goldsinks.STARS.growth, U.Min(n, Goldsinks.STARS.softcap)) * Math.Pow(Goldsinks.STARS.lateGrowth, U.Max(0, n - Goldsinks.STARS.softcap)));
        }

        public static StarState starState(GameSimulation sim, TowerInstance t)
        {
            double cost = starCost(t);
            if (!sim.s.rules.endless) return new StarState { ok = false, reason = "Veredelung nur in den Endlosmodi", cost = cost };
            if (Towers.TOWERS[t.def].attack == "none") return new StarState { ok = false, reason = "Nicht veredelbar", cost = cost };
            if ((t.stars ?? 0) >= Goldsinks.STARS.max) return new StarState { ok = false, reason = $"Höchste Veredelung ★{U.Fmt(Goldsinks.STARS.max)}", cost = cost };
            if (!fullyBuilt(t)) return new StarState { ok = false, reason = "Erst voll ausbauen (Stufe 4 und Spezialisierung)", cost = cost };
            if (sim.s.gold < cost) return new StarState { ok = false, reason = "Nicht genug Gold", cost = cost };
            return new StarState { ok = true, reason = null, cost = cost };
        }

        public static CommandResult starTower(GameSimulation sim, double uid)
        {
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            var chk = starState(sim, t);
            if (!chk.ok) return CommandResult.Fail(chk.reason ?? "Nicht möglich");
            sim.s.gold -= chk.cost;
            sim.s.stats.goldSpent += chk.cost;
            t.invested += chk.cost;
            t.stars = (t.stars ?? 0) + 1;
            markDirty(sim);
            sim.emit(new GameEvent { k = "upgrade", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5 });
            return CommandResult.Ok($"{Towers.TOWERS[t.def].name} ★{U.Fmt(t.stars)}");
        }

        /// <summary>Alle (bzw. die angegebenen) Türme veredeln, solange das Gold reicht – günstigster Schritt zuerst.</summary>
        public static CountSpentResult starAllTowers(GameSimulation sim, IEnumerable<double> uids = null)
        {
            var set = uids != null ? new HashSet<double>(uids) : null;
            double count = 0;
            double spent = 0;
            for (int guard = 0; guard < 3000; guard++)
            {
                TowerInstance best = null;
                double bc = double.PositiveInfinity;
                foreach (var t in sim.s.towers)
                {
                    if (set != null && !set.Contains(t.uid)) continue;
                    var st = starState(sim, t);
                    if (st.ok && st.cost < bc) { best = t; bc = st.cost; }
                }
                if (best == null) break;
                if (!starTower(sim, best.uid).ok) break;
                count++;
                spent += bc;
            }
            if (!U.Truthy(count)) return new CountSpentResult { ok = false, reason = "Nichts zu veredeln (voll ausbauen oder mehr Gold)", count = count, spent = spent };
            return new CountSpentResult { ok = true, message = $"{U.Fmt(count)}× veredelt für {U.Fmt(spent)} Gold", count = count, spent = spent };
        }

        /// <summary>Türme, die gegen das Turmlimit zählen (alles außer Barrikaden).</summary>
        public static double countedTowers(GameSimulation sim)
        {
            double n = 0;
            foreach (var t in sim.s.towers) if (Scaling.countsTowardLimit(t.def)) n++;
            return n;
        }

        public static double towerCost(GameSimulation sim, string def)
        {
            var m = sim.c.mods;
            double c = Towers.TOWERS[def].cost * m.towerCostMul;
            if (def == "goldmine") c *= m.mineCostMul;
            var pf = PassiveTree.runPassive(sim.s.he);
            if (U.Truthy(pf.towerCostPct)) c *= U.Max(0.6, 1 - pf.towerCostPct);
            return U.Round(c);
        }

        public static double? nextUpgradeCost(TowerInstance t)
        {
            var def = Towers.TOWERS[t.def];
            if (t.level - 1 < def.upgrades.Count) return def.upgrades[(int)(t.level - 1)].cost;
            return null;
        }

        /// <summary>TS <c>arr[i]?.cost</c>: undefined (null) außerhalb des Bereichs.</summary>
        private static UpgradeDefinition At(List<UpgradeDefinition> list, double i)
        {
            if (i < 0 || i >= list.Count || i != Math.Floor(i)) return null;
            return list[(int)i];
        }

        public static double specCost(TowerInstance t, double spec)
        {
            var u = At(Towers.TOWERS[t.def].specs, spec - 1);
            return u != null ? u.cost : double.PositiveInfinity;
        }

        /// <summary>3.1: Ab Welle PRESPEC.from (Endlos) können Türme direkt auf Höchststufe mit Spezialisierung gebaut werden – mit Aufpreis.</summary>
        public static readonly PrespecConfig PRESPEC = new PrespecConfig { from = 150, premium = 1.25 };

        public static bool prespecAvailable(GameSimulation sim, string def)
        {
            var s = sim.s;
            var d = Towers.TOWERS[def];
            return s.rules.endless && s.wave >= PRESPEC.from && d.specs.Count >= 1 && d.upgrades.Count >= 3 && d.legendary != true;
        }

        /// <summary>Preis eines vorspezialisierten Turms: (Grundpreis + alle Stufen + Spezialisierung) × Aufpreis.</summary>
        public static double prespecCost(GameSimulation sim, string def, double spec)
        {
            var d = Towers.TOWERS[def];
            double ups = 0;
            foreach (var u in d.upgrades) ups = ups + u.cost;
            var sp = At(d.specs, spec - 1);
            return U.Round((towerCost(sim, def) + ups + (sp != null ? sp.cost : 0)) * PRESPEC.premium);
        }

        public static double refundValue(GameSimulation sim, TowerInstance t)
        {
            bool fresh = t.builtInPhase && sim.s.phase == "build" && t.builtWave == sim.s.wave;
            return fresh ? t.invested : Math.Floor(t.invested * Towers.SELL_REFUND);
        }

        public static BuildCheck canBuildHere(GameSimulation sim, string def, double cx, double cy, double spec = 0)
        {
            var s = sim.s;
            var map = s.map;
            if (U.Truthy(spec) && !prespecAvailable(sim, def))
            {
                return new BuildCheck { ok = false, reason = $"Spezialisierter Bau erst ab Welle {U.Fmt(PRESPEC.from)}", cost = double.PositiveInfinity, affordable = false };
            }
            double cost = U.Truthy(spec) ? prespecCost(sim, def, spec) : towerCost(sim, def);
            bool affordable = s.gold >= cost;
            BuildCheck fail(string reason) => new BuildCheck { ok = false, reason = reason, cost = cost, affordable = affordable };
            if (!Pathfinding.inBounds(map, cx, cy)) return fail("Außerhalb der Karte");
            double idx = cy * map.width + cx;
            if (s.rules.bannedTowers.Contains(def)) return fail("In dieser Mission gesperrt");
            if (s.he != null && !PassiveTree.towerUnlocked(def, s.he, s.wave)) return fail($"Wird ab Welle {U.Fmt(PassiveTree.towerUnlockWave(def, s.he))} freigeschaltet");
            if (Towers.TOWERS[def].legendary == true) return fail("Legendäre Türme entstehen nur durch Fusion");
            if (Towers.TOWERS[def].mazingOnly == true && map.variant != "mazing") return fail("Nur im Mazing-Modus verfügbar");
            if (Towers.TOWERS[def].laneOnly == true) return canBuildLane(sim, cx, cy);
            if (Scaling.countsTowardLimit(def))
            {
                double lim = currentTowerLimit(sim);
                if (countedTowers(sim) >= lim) return fail($"Turmlimit erreicht ({U.Fmt(lim)}) – nächster Bauplatz ab Welle {U.Fmt(Scaling.nextTowerSlotWave(s.wave))}. Weniger Türme = Fokusbonus.");
            }
            double size = Towers.towerSize(def);
            if (size > 1 && !Pathfinding.inBounds(map, cx + size - 1, cy + size - 1)) return fail($"Braucht {U.Fmt(size)}×{U.Fmt(size)} Felder – Außerhalb der Karte");
            var cells = Towers.footprintCells(def, cx, cy, map.width);
            if (cells.Any((i) => sim.c.towerByCell.ContainsKey(i))) return fail(size > 1 ? $"Braucht {U.Fmt(size)}×{U.Fmt(size)} freie Felder – belegt" : "Feld ist bereits belegt");
            if (cells.Any((i) => !Pathfinding.isBuildableCell(map, i))) return fail(size > 1 ? $"Braucht {U.Fmt(size)}×{U.Fmt(size)} freie, bebaubare Felder" : "Hier kann nicht gebaut werden");
            if (size > 1 && cells.Any((i) => map.heights[(int)i] != map.heights[(int)idx])) return fail($"Braucht {U.Fmt(size)}×{U.Fmt(size)} ebene Felder (gleiche Höhe)");
            if (map.variant == "mazing")
            {
                // Keine Platzierung auf Gegnern oder deren nächster Zielzelle.
                foreach (var e in s.enemies)
                {
                    if (!e.alive || e.flying) continue;
                    double ei = Math.Floor(e.y) * map.width + Math.Floor(e.x);
                    if (cells.Contains(ei) || cells.Contains(e.ny * map.width + e.nx)) return fail("Ein Gegner steht im Weg");
                }
                var blocked = (byte[])sim.c.blocked.Clone();
                foreach (var i in cells) blocked[(int)i] = 1;
                var field = Pathfinding.computeField(map, blocked);
                foreach (var sp in WorldGenerator.spawnsOf(map))
                {
                    if (field[(int)(sp.y * map.width + sp.x)] >= Pathfinding.INF) return new BuildCheck { ok = false, reason = "Würde den Weg vollständig blockieren", cost = cost, affordable = affordable, blocks = true };
                }
                foreach (var e in s.enemies)
                {
                    if (!e.alive || e.flying) continue;
                    if (field[(int)(e.ny * map.width + e.nx)] >= Pathfinding.INF) return new BuildCheck { ok = false, reason = "Würde Gegner einsperren", cost = cost, affordable = affordable, blocks = true };
                }
                var route = Pathfinding.tracePath(map, field, map.spawn);
                if (!affordable) return new BuildCheck { ok = false, reason = "Nicht genug Gold", cost = cost, affordable = affordable, route = route };
                return new BuildCheck { ok = true, cost = cost, affordable = affordable, route = route };
            }
            if (!affordable) return fail("Nicht genug Gold");
            return new BuildCheck { ok = true, cost = cost, affordable = affordable };
        }

        // ------------------------------------------------------------ Lane-Barrikaden (3.8)

        /// <summary>Verfügbare Lane-Barrikaden und Sekunden bis zur nächsten freien Ladung.</summary>
        public static LaneCharges laneCharges(GameSimulation sim)
        {
            var s = sim.s;
            var recent = U.StableSort((s.laneBarricades ?? new List<double>()).Where((t) => s.time - t < Scaling.LANE_BARRICADE.window).ToList(), (a, b) => a - b);
            double left = U.Max(0, Scaling.LANE_BARRICADE.maxCharges - recent.Count);
            double next = left > 0 ? 0 : U.Max(0, recent[0] + Scaling.LANE_BARRICADE.window - s.time);
            return new LaneCharges { left = left, next = next };
        }

        private static string mmss(double sec)
        {
            return $"{U.Fmt(Math.Floor(sec / 60))}:{U.PadStart(U.Fmt(Math.Floor(sec % 60)), 2, "0")}";
        }

        private static BuildCheck canBuildLane(GameSimulation sim, double cx, double cy)
        {
            var s = sim.s;
            var map = s.map;
            BuildCheck fail(string reason) => new BuildCheck { ok = false, reason = reason, cost = 0, affordable = true };
            if (!s.rules.endless) return fail("Lane-Barrikaden nur in den Endlosmodi");
            if (map.variant == "mazing") return fail("Im Mazing-Modus die normale Barrikade nutzen");
            if (!Pathfinding.inBounds(map, cx, cy)) return fail("Außerhalb der Karte");
            double idx = cy * map.width + cx;
            if (map.cells[(int)idx] != Cell.Path) return fail("Nur direkt auf die Lane (Weg) setzen");
            if (sim.c.towerByCell.ContainsKey(idx)) return fail("Feld ist bereits belegt");
            foreach (var sp in WorldGenerator.spawnsOf(map))
            {
                if (U.Hypot(sp.x - cx, sp.y - cy) < Scaling.LANE_BARRICADE.minSpawnDist) return fail($"Nicht direkt vor einem Riss – mindestens {U.Fmt(Scaling.LANE_BARRICADE.minSpawnDist)} Felder Abstand");
            }
            if (Math.Abs(map.goal.x - cx) + Math.Abs(map.goal.y - cy) <= 1) return fail("Nicht direkt am Kern");
            var ch = laneCharges(sim);
            if (ch.left <= 0) return fail($"Höchstens {U.Fmt(Scaling.LANE_BARRICADE.maxCharges)} Barrikaden je {U.Fmt(Scaling.LANE_BARRICADE.window / 60)} Minuten – nächste in {mmss(ch.next)}");
            return new BuildCheck { ok = true, cost = 0, affordable = true };
        }

        public static CommandResult buildTower(GameSimulation sim, string def, double cx, double cy, double spec = 0)
        {
            var chk = canBuildHere(sim, def, cx, cy, spec);
            if (!chk.ok) return CommandResult.Fail(chk.reason ?? "Nicht möglich");
            var s = sim.s;
            var d = Towers.TOWERS[def];
            var t = new TowerInstance
            {
                uid = sim.uid(), def = def, cx = cx, cy = cy, level = U.Truthy(spec) ? d.upgrades.Count + 1 : 1, spec = spec, invested = chk.cost, builtWave = s.wave,
                builtInPhase = s.phase == "build", priority = Towers.basePriority(d.defaultPriority), cooldown = 0.3, target = 0, retarget = 0,
                aim = 0, stunned = 0, disrupted = 0, rampTarget = 0, rampStacks = 0, damageDealt = 0, kills = 0, goldEarned = 0,
                shots = 0, fireAnim = 0, activeCd = 0, activeT = 0, forged = 0, charge = 0, orbit = 0,
            };
            s.gold -= chk.cost;
            s.stats.goldSpent += chk.cost;
            s.stats.builtByTower[def] = (U.GetN(s.stats.builtByTower, def) ?? 0) + 1;
            if (s.stats.firstWaveByTower != null && !s.stats.firstWaveByTower.ContainsKey(def)) s.stats.firstWaveByTower[def] = s.wave;
            s.towers.Add(t);
            if (d.laneOnly == true)
            {
                var lb = (s.laneBarricades ?? new List<double>()).Where((x) => s.time - x < Scaling.LANE_BARRICADE.window).ToList();
                lb.Add(s.time);
                s.laneBarricades = lb;
            }
            sim.reindex();
            markDirty(sim);
            sim.emit(new GameEvent { k = "build", tower = def, x = cx + 0.5, y = cy + 0.5 });
            return CommandResult.Ok();
        }

        public static CommandResult sellTower(GameSimulation sim, double uid)
        {
            var s = sim.s;
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            double refund = refundValue(sim, t);
            s.gold += refund;
            s.stats.goldSpent -= U.Min(refund, s.stats.goldSpent);
            s.towers = s.towers.Where((x) => x.uid != uid).ToList();
            RuneSystem.releaseRunes(sim, uid);
            sim.reindex();
            markDirty(sim);
            sim.emit(new GameEvent { k = "sell", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5, refund = refund });
            return CommandResult.Ok($"+{U.Fmt(refund)} Gold");
        }

        public static CommandResult upgradeTower(GameSimulation sim, double uid)
        {
            var s = sim.s;
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            double? cost = nextUpgradeCost(t);
            if (cost == null) return CommandResult.Fail(Towers.TOWERS[t.def].upgrades.Count == 0 ? "Nicht aufwertbar" : "Maximale Stufe erreicht – jetzt spezialisieren");
            if (s.gold < cost.Value) return CommandResult.Fail("Nicht genug Gold");
            s.gold -= cost.Value;
            s.stats.goldSpent += cost.Value;
            t.invested += cost.Value;
            t.level++;
            markDirty(sim);
            sim.emit(new GameEvent { k = "upgrade", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5 });
            return CommandResult.Ok();
        }

        public static CommandResult specializeTower(GameSimulation sim, double uid, double spec)
        {
            var s = sim.s;
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            if (Towers.TOWERS[t.def].specs.Count < 1) return CommandResult.Fail("Nicht spezialisierbar");
            if (t.level < 4) return CommandResult.Fail("Erst alle drei Stufen ausbauen");
            if (t.spec != 0) return CommandResult.Fail("Bereits spezialisiert");
            double cost = specCost(t, spec);
            if (s.gold < cost) return CommandResult.Fail("Nicht genug Gold");
            s.gold -= cost;
            s.stats.goldSpent += cost;
            t.invested += cost;
            t.spec = spec;
            markDirty(sim);
            sim.emit(new GameEvent { k = "upgrade", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5 });
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Spezialisierung wechseln (3.2)

        /// <summary>Ein Wechsel der Spezialisierung kostet diesen Anteil des Preises der neuen Spezialisierung.</summary>
        public const double RESPEC_MUL = 0.5;

        /// <summary>Kosten, um einen Turm auf Spezialisierung spec zu bringen (0 = bereits so; Infinity = nicht möglich).</summary>
        public static double respecCost(TowerInstance t, double spec)
        {
            var d = Towers.TOWERS[t.def];
            if (d.legendary == true || d.specs.Count < 2) return double.PositiveInfinity;
            if (t.spec == spec) return 0;
            double c = specCost(t, spec);
            return t.spec == 0 ? c : U.Round(c * RESPEC_MUL);
        }

        public static CommandResult respecTower(GameSimulation sim, double uid, double spec)
        {
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            if (t.spec == 0) return specializeTower(sim, uid, spec);
            if (t.spec == spec) return CommandResult.Fail("Bereits so spezialisiert");
            double cost = respecCost(t, spec);
            if (!U.IsFinite(cost)) return CommandResult.Fail("Nicht spezialisierbar");
            if (sim.s.gold < cost) return CommandResult.Fail("Nicht genug Gold");
            sim.s.gold -= cost;
            sim.s.stats.goldSpent += cost;
            t.invested += cost;
            t.spec = spec;
            markDirty(sim);
            sim.emit(new GameEvent { k = "upgrade", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5 });
            return CommandResult.Ok();
        }

        /// <summary>Restkosten, um einen Turm auf Höchststufe mit Spezialisierung spec zu bringen.</summary>
        public static double specTargetCost(TowerInstance t, double spec)
        {
            var d = Towers.TOWERS[t.def];
            if (d.legendary == true || d.specs.Count < 2) return double.PositiveInfinity;
            double c = 0;
            for (double l = t.level; l <= d.upgrades.Count; l++) c += d.upgrades[(int)(l - 1)].cost;
            return c + respecCost(t, spec);
        }

        /// <summary>Gruppe: alle Türme ausbauen und auf Spezialisierung spec setzen bzw. umstellen, solange das Gold reicht (günstigste zuerst).</summary>
        public static GroupSpecializeResult groupSpecialize(GameSimulation sim, IEnumerable<double> uids, double spec)
        {
            var set = new HashSet<double>(uids);
            var todo = U.StableSort(
                sim.s.towers.Where((t) => set.Contains(t.uid) && t.spec != spec && U.IsFinite(specTargetCost(t, spec))).ToList(),
                (a, b) => U.Or(specTargetCost(a, spec) - specTargetCost(b, spec), a.uid - b.uid));
            double count = 0;
            double spent = 0;
            foreach (var t in todo)
            {
                double c = specTargetCost(t, spec);
                if (c > sim.s.gold) continue;
                double g0 = sim.s.gold;
                while (nextUpgradeCost(t) != null) if (!upgradeTower(sim, t.uid).ok) break;
                if (!respecTower(sim, t.uid, spec).ok)
                {
                    spent += g0 - sim.s.gold;
                    continue;
                }
                spent += g0 - sim.s.gold;
                count++;
            }
            double left = todo.Count - count;
            var nameSpec = At(Towers.TOWERS[todo.Count > 0 ? todo[0].def : "ranger"].specs, spec - 1);
            string name = nameSpec != null ? nameSpec.name : "";
            if (todo.Count == 0) return new GroupSpecializeResult { ok = false, reason = "Alle bereits so spezialisiert", count = count, spent = spent, left = left };
            if (!U.Truthy(count)) return new GroupSpecializeResult { ok = false, reason = "Nicht genug Gold", count = count, spent = spent, left = left };
            return new GroupSpecializeResult { ok = true, message = $"{U.Fmt(count)}× auf {name} gestellt für {U.Fmt(spent)} Gold{(U.Truthy(left) ? $" ({U.Fmt(left)} fehlen noch – zu wenig Gold)" : "")}", count = count, spent = spent, left = left };
        }

        internal static void markDirty(GameSimulation sim)
        {
            sim.c.networkDirty = true;
            sim.c.statsDirty = true;
            if (sim.s.map.variant == "mazing") sim.c.fieldDirty = true;
        }

        // Einkommen der Goldgräber am Wellenende. Nur Türme, die vor Wellenbeginn standen.
        /// <summary>Zusätzliches Gold eines Goldgräbers auf einem Goldvorkommen je Welle.</summary>
        public static double veinBonus(double wave)
        {
            return U.Round(Balance.ECONOMY.veinFlat + Balance.ECONOMY.veinFlatPerWave * wave);
        }

        public static double collectIncome(GameSimulation sim)
        {
            double total = 0;
            foreach (var t in sim.s.towers)
            {
                if (!TowersNew.MINES.Contains(t.def) || t.builtWave >= sim.s.wave) continue;
                double inc = U.Round(sim.stats(t)["income"]);
                // Goldvorkommen: zusätzlicher Abbau neben dem normalen Ertrag
                if (onVein(sim, t)) inc += veinBonus(sim.s.wave);
                // Befehl (z. B. Mira): Befehlsbonus des Helden erhöht den Goldertrag
                if (sim.c.champBuff.TryGetValue(t.uid, out var cb) && cb != null && cb.dmg > 0) inc = U.Round(inc * (1 + cb.dmg));
                t.goldEarned += inc;
                total += inc;
                sim.emit(new GameEvent { k = "gold", amount = inc, x = t.cx + 0.5, y = t.cy + 0.5 });
            }
            total += collectInterest(sim);
            return total;
        }

        /// <summary>Zinsgrenze eines Kontors in der aktuellen Welle (wächst um 0,6 Gold je Welle).</summary>
        public static double interestCap(GameSimulation sim, Dictionary<string, double> st)
        {
            return st["interestCap"] + 0.6 * sim.s.wave;
        }

        /// <summary>
        /// 4.0 Handelskontor: Zinsen auf ungenutztes Gold. Mehrere Kontore teilen sich den Goldvorrat –
        /// jedes verzinst einen eigenen Teil (höchster Zinssatz zuerst), damit sich Kontore nicht vervielfachen.
        /// </summary>
        public static double collectInterest(GameSimulation sim)
        {
            var banks = new List<TowerInstance>();
            foreach (var t in sim.s.towers)
                if (t.builtWave < sim.s.wave && sim.stats(t)["interest"] > 0) banks.Add(t);
            if (banks.Count == 0) return 0;
            U.StableSort(banks, (a, b) => U.Or(sim.stats(b)["interest"] - sim.stats(a)["interest"], a.uid - b.uid));
            double pool = U.Max(0, sim.s.gold);
            double total = 0;
            foreach (var t in banks)
            {
                if (pool <= 0) break;
                var st = sim.stats(t);
                double cap = interestCap(sim, st);
                double slice = U.Min(pool, cap / st["interest"]);
                pool -= slice;
                double inc = U.Round(slice * st["interest"]);
                if (sim.c.champBuff.TryGetValue(t.uid, out var cb) && cb != null && cb.dmg > 0) inc = U.Round(inc * (1 + cb.dmg));
                if (inc <= 0) continue;
                t.goldEarned += inc;
                total += inc;
                var c = Towers.towerCenter(t);
                sim.emit(new GameEvent { k = "gold", amount = inc, x = c.x, y = c.y });
            }
            return total;
        }

        // ------------------------------------------------------------ Fusion

        private static bool fused(TowerInstance t)
        {
            // 4.1: Fusion schon ab Stufe 4 – die Spezialisierung (Stufe 5) ist dafür nicht mehr nötig
            return t.level >= 4;
        }

        /// <summary>Mögliche Fusionen für einen Turm (voll ausgebaut + benachbarter, voll ausgebauter Partner).</summary>
        public static List<FusionOption> fusionOptions(GameSimulation sim, TowerInstance t)
        {
            var @out = new List<FusionOption>();
            foreach (var r in Towers.FUSIONS)
            {
                string other = r.a == t.def ? r.b : r.b == t.def ? r.a : null;
                if (!U.Truthy(other)) continue;
                TowerInstance best = null;
                double bd = double.PositiveInfinity;
                foreach (var o in sim.s.towers)
                {
                    if (o == t || o.def != other) continue;
                    double d = U.Hypot(o.cx - t.cx, o.cy - t.cy);
                    if (d <= Towers.FUSION_RANGE && d < bd)
                    {
                        bd = d;
                        best = o;
                    }
                }
                if (best != null) @out.Add(new FusionOption { recipe = r, partner = best, affordable = sim.s.gold >= r.cost });
            }
            return @out;
        }

        /// <summary>
        /// Vorschau beim Platzieren: welche Fusionen ein Turm dieses Typs an (cx, cy) mit bereits gebauten
        /// Türmen eingehen könnte (Partner in Reichweite, unabhängig von Stufe/Spezialisierung).
        /// </summary>
        public static List<FusionPreviewEntry> fusionPreview(GameSimulation sim, string def, double cx, double cy)
        {
            return Towers.FUSIONS.Where((r) => r.a == def || r.b == def).Select((r) =>
            {
                string partnerDef = r.a == def ? r.b : r.a;
                var partners = sim.s.towers.Where((o) => o.def == partnerDef && U.Hypot(o.cx - cx, o.cy - cy) <= Towers.FUSION_RANGE).ToList();
                return new FusionPreviewEntry { recipe = r, partnerDef = partnerDef, partners = partners };
            }).ToList();
        }

        public static bool canFuse(TowerInstance t, TowerInstance partner)
        {
            return fused(t) && fused(partner);
        }

        public static CommandResult fuseTowers(GameSimulation sim, double uid, double partnerUid)
        {
            var s = sim.s;
            var t = U.Get(sim.c.towerByUid, uid);
            var p = U.Get(sim.c.towerByUid, partnerUid);
            if (t == null || p == null) return CommandResult.Fail("Turm nicht gefunden");
            var r = Towers.FUSIONS.FirstOrDefault((f) => (f.a == t.def && f.b == p.def) || (f.b == t.def && f.a == p.def));
            if (r == null) return CommandResult.Fail("Diese Türme lassen sich nicht verschmelzen");
            if (U.Hypot(t.cx - p.cx, t.cy - p.cy) > Towers.FUSION_RANGE) return CommandResult.Fail("Die Türme stehen zu weit auseinander");
            if (!canFuse(t, p)) return CommandResult.Fail("Beide Türme müssen voll ausgebaut und spezialisiert sein");
            if (s.gold < r.cost) return CommandResult.Fail("Nicht genug Gold");
            s.gold -= r.cost;
            s.stats.goldSpent += r.cost;
            // Erbe: Spezialisierungen beider Eltern merken (Reihenfolge wie im Rezept)
            var ta = t.def == r.a ? t : p;
            var tb = ta == t ? p : t;
            double specA = ta.spec, specB = tb.spec;
            t.fuseA = specA;
            t.fuseB = specB;
            t.def = r.id;
            t.level = 4;
            t.spec = 0;
            t.invested += p.invested + r.cost;
            t.priority = Towers.basePriority(Towers.TOWERS[r.id].defaultPriority);
            t.target = 0;
            t.charge = 0;
            t.activeCd = 0;
            t.activeT = 0;
            t.builtInPhase = false;
            // Der legendäre Turm entsteht immer auf dem Feld, das näher an der Lane liegt –
            // 2×2-Fusionen (Goldgräber) bleiben auf der Fläche des Goldgräbers
            bool keepPartnerCell = Towers.towerSize(r.id) > 1 ? Towers.towerSize(p.def) > 1 : laneDistance(sim, p.cx, p.cy) < laneDistance(sim, t.cx, t.cy);
            double px = p.cx;
            double py = p.cy;
            s.towers = s.towers.Where((x) => x.uid != p.uid).ToList();
            if (keepPartnerCell)
            {
                t.cx = px;
                t.cy = py;
            }
            RuneSystem.releaseRunes(sim, p.uid);
            s.stats.fusions++;
            s.stats.builtByTower[r.id] = (U.GetN(s.stats.builtByTower, r.id) ?? 0) + 1;
            if (s.stats.firstWaveByTower != null && !s.stats.firstWaveByTower.ContainsKey(r.id)) s.stats.firstWaveByTower[r.id] = s.wave;
            sim.reindex();
            markDirty(sim);
            sim.emit(new GameEvent { k = "fusion", tower = r.id, x = t.cx + 0.5, y = t.cy + 0.5 });
            sim.emit(new GameEvent { k = "message", text = $"Legendärer Turm erschaffen: {Towers.TOWERS[r.id].name}! Erbe: {FusionHeritage.describe(r.id, specA, specB)}", severity = "info" });
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Aktive Turmfähigkeiten

        public static ActiveState activeState(GameSimulation sim, TowerInstance t)
        {
            var d = Towers.TOWERS[t.def];
            if (!U.Truthy(d.active)) return new ActiveState { available = false, reason = "Keine aktive Fähigkeit" };
            if (t.level < Towers.ACTIVE_MIN_LEVEL && d.legendary != true) return new ActiveState { available = false, reason = $"Ab Stufe {U.Fmt(Towers.ACTIVE_MIN_LEVEL)}" };
            if (sim.s.phase != "combat") return new ActiveState { available = false, reason = "Nur während einer Welle" };
            if (t.activeCd > 0) return new ActiveState { available = false, reason = $"Bereit in {U.Fmt(Math.Ceiling(t.activeCd))} s" };
            if (t.stunned > 0) return new ActiveState { available = false, reason = "Turm ist außer Gefecht" };
            return new ActiveState { available = true, reason = null };
        }

        public static CommandResult triggerActive(GameSimulation sim, double uid)
        {
            var t = U.Get(sim.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            var st = activeState(sim, t);
            if (!st.available) return CommandResult.Fail(st.reason ?? "Nicht verfügbar");
            string id = Towers.TOWERS[t.def].active;
            if (!U.Truthy(id)) return CommandResult.Fail("Keine aktive Fähigkeit");
            var a = Towers.TOWER_ACTIVES[id];
            t.activeCd = a.cooldown;
            t.activeT = a.duration;
            sim.s.stats.activesUsed++;
            sim.c.statsDirty = true;
            sim.pendingActives.Add(new PendingActive { uid = t.uid, id = id });
            sim.emit(new GameEvent { k = "towerActive", tower = t.def, active = id, x = t.cx + 0.5, y = t.cy + 0.5 });
            return CommandResult.Ok();
        }

        /// <summary>Sofortiger Minenertrag (aktive Fähigkeit Goldrausch).</summary>
        public static double collectIncomeFor(GameSimulation sim, TowerInstance t, double mul)
        {
            double inc = U.Round(sim.stats(t)["income"] * mul);
            sim.s.gold += inc;
            sim.s.stats.goldEarned += inc;
            t.goldEarned += inc;
            return inc;
        }

        /// <summary>Steht der Turm (irgendein Feld seiner Grundfläche) auf einer Goldader?</summary>
        public static bool onVein(GameSimulation sim, TowerInstance t)
        {
            return Towers.footprintCells(t.def, t.cx, t.cy, sim.s.map.width).Any((i) => sim.s.map.veins.Contains(i));
        }

        /// <summary>Steht der Turm (irgendein Feld seiner Grundfläche) auf einem Kristallvorkommen?</summary>
        public static bool onDeposit(GameSimulation sim, TowerInstance t)
        {
            var d = sim.s.map.deposits;
            return d != null && Towers.footprintCells(t.def, t.cx, t.cy, sim.s.map.width).Any((i) => d.Contains(i));
        }

        // ------------------------------------------------------------ Alles aufwerten (2.6)

        /// <summary>Türme, die noch eine normale Stufe aufwerten können (ohne Spezialisierung).</summary>
        public static List<TowerInstance> upgradableTowers(GameSimulation sim, IEnumerable<double> uids = null)
        {
            var set = uids != null ? new HashSet<double>(uids) : null;
            return sim.s.towers.Where((t) => (set == null || set.Contains(t.uid)) && nextUpgradeCost(t) != null).ToList();
        }

        /// <summary>Gesamtkosten, um die gewählten Türme bis zur höchsten Stufe auszubauen (ohne Spezialisierung).</summary>
        public static UpgradeAllCost upgradeAllCost(GameSimulation sim, IEnumerable<double> uids = null)
        {
            double total = 0;
            double steps = 0;
            foreach (var t in upgradableTowers(sim, uids))
            {
                var d = Towers.TOWERS[t.def];
                for (double l = t.level; l - 1 < d.upgrades.Count; l++)
                {
                    total += d.upgrades[(int)(l - 1)].cost;
                    steps++;
                }
            }
            return new UpgradeAllCost { total = total, steps = steps };
        }

        private class UpgradeCandidate
        {
            public TowerInstance t;
            public double c;
        }

        /// <summary>
        /// Wertet alle (bzw. die gewählten) Türme bis zur höchsten Stufe auf, solange das Gold reicht.
        /// Immer die günstigste mögliche Stufe zuerst – so verteilt sich das Gold gleichmäßig. Spezialisierungen
        /// werden nie automatisch gewählt.
        /// </summary>
        public static CountSpentResult upgradeAllTowers(GameSimulation sim, IEnumerable<double> uids = null)
        {
            // uids einmal materialisieren (TS: readonly Array, mehrfach gelesen)
            List<double> uidList = uids != null ? uids.ToList() : null;
            double count = 0;
            double spent = 0;
            for (int guard = 0; guard < 2000; guard++)
            {
                var cand = U.StableSort(
                    upgradableTowers(sim, uidList)
                        .Select((t) => new UpgradeCandidate { t = t, c = nextUpgradeCost(t) ?? double.PositiveInfinity })
                        .Where((x) => x.c <= sim.s.gold)
                        .ToList(),
                    (a, b) => U.Or(U.Or(a.c - b.c, a.t.level - b.t.level), a.t.uid - b.t.uid));
                if (cand.Count == 0) break;
                var r0 = upgradeTower(sim, cand[0].t.uid);
                if (!r0.ok) break;
                count++;
                spent += cand[0].c;
            }
            // 3.6: danach veredeln (Endlos), solange das Gold reicht
            double stars = 0;
            if (sim.s.rules.endless)
            {
                var r = starAllTowers(sim, uidList);
                stars = r.count;
                spent += r.spent;
            }
            if (!U.Truthy(count) && !U.Truthy(stars))
            {
                double left = upgradableTowers(sim, uidList).Count;
                return new CountSpentResult { ok = false, reason = U.Truthy(left) ? "Nicht genug Gold für eine weitere Stufe" : sim.s.rules.endless ? "Nichts mehr aufzuwerten oder zu veredeln (spezialisieren oder mehr Gold)" : "Alle Türme sind bereits auf der höchsten Stufe", count = count, spent = spent };
            }
            return new CountSpentResult { ok = true, message = $"{(U.Truthy(count) ? $"{U.Fmt(count)}× aufgewertet" : "")}{(U.Truthy(count) && U.Truthy(stars) ? ", " : "")}{(U.Truthy(stars) ? $"{U.Fmt(stars)}× veredelt" : "")} für {U.Fmt(spent)} Gold", count = count + stars, spent = spent };
        }

        // ------------------------------------------------------------ Alle verkaufen (2.9.2)

        /// <summary>Erlös, wenn alle (bzw. die angegebenen) Türme verkauft würden.</summary>
        public static SellAllValue sellAllValue(GameSimulation sim, IEnumerable<double> uids = null)
        {
            var set = uids != null ? new HashSet<double>(uids) : null;
            double gold = 0;
            double count = 0;
            foreach (var t in sim.s.towers)
            {
                if (set != null && !set.Contains(t.uid)) continue;
                gold += refundValue(sim, t);
                count++;
            }
            return new SellAllValue { count = count, gold = gold };
        }

        /// <summary>Verkauft alle (bzw. die angegebenen) Türme. Gesockelte Runen kehren in den Beutel zurück.</summary>
        public static SellAllResult sellAllTowers(GameSimulation sim, IEnumerable<double> uids = null)
        {
            var set = uids != null ? new HashSet<double>(uids) : null;
            var list = sim.s.towers.Where((t) => set == null || set.Contains(t.uid)).Select((t) => t.uid).ToList();
            if (list.Count == 0) return new SellAllResult { ok = false, reason = "Keine Türme zum Verkaufen", count = 0, gold = 0 };
            double g0 = sim.s.gold;
            double count = 0;
            foreach (var uid in list) if (sellTower(sim, uid).ok) count++;
            double gold = sim.s.gold - g0;
            return new SellAllResult { ok = true, message = $"{U.Fmt(count)} Türme verkauft: +{U.Fmt(gold)} Gold", count = count, gold = gold };
        }

        /// <summary>Abstand (Felder, euklidisch) von einem Feld zur nächsten Lane-Zelle (Weg im Classic, Gegnerroute im Mazing).</summary>
        public static double laneDistance(GameSimulation sim, double cx, double cy)
        {
            var map = sim.s.map;
            List<GridPos> lane;
            if (map.variant == "mazing") lane = WorldGenerator.spawnsOf(map).SelectMany((sp) => Pathfinding.tracePath(map, sim.c.field, sp)).ToList();
            else
            {
                lane = new List<GridPos>();
                for (int i = 0; i < map.cells.Count; i++) if (map.cells[i] == Cell.Path) lane.Add(new GridPos { x = i % map.width, y = Math.Floor(i / map.width) });
            }
            double best = double.PositiveInfinity;
            foreach (var c in lane) best = U.Min(best, Sq(c.x - cx) + Sq(c.y - cy));
            return Math.Sqrt(best);
        }

        /// <summary>Alle Fusionspaare im Feld (jedes Paar einmal), mit Bereitschaft und dem Feld, auf dem der legendäre Turm entsteht.</summary>
        public static List<FusionPair> fusionPairs(GameSimulation sim)
        {
            var @out = new List<FusionPair>();
            var seen = new HashSet<string>();
            foreach (var t in sim.s.towers)
            {
                foreach (var f in fusionOptions(sim, t))
                {
                    var keyParts = U.StableSort(new List<double> { t.uid, f.partner.uid }, (x, y) => x - y);
                    string key = string.Join(":", keyParts.Select((v) => U.Fmt(v)));
                    if (seen.Contains(key)) continue;
                    seen.Add(key);
                    var target = laneDistance(sim, f.partner.cx, f.partner.cy) < laneDistance(sim, t.cx, t.cy) ? f.partner : t;
                    @out.Add(new FusionPair { a = t, b = f.partner, recipe = f.recipe, ready = canFuse(t, f.partner), affordable = sim.s.gold >= f.recipe.cost, target = target });
                }
            }
            return @out;
        }

        // ------------------------------------------------------------ Legendäre Türme versetzen (2.9.5)

        public const double MOVE_STUN = 3;

        /// <summary>Prüft, ob ein legendärer Turm auf ein anderes Feld versetzt werden darf (kostenlos, danach kurz außer Gefecht).</summary>
        public static BuildCheck canMoveTower(GameSimulation sim, double uid, double cx, double cy)
        {
            var s = sim.s;
            var map = s.map;
            var t = U.Get(sim.c.towerByUid, uid);
            BuildCheck fail(string reason, bool blocks = false) => new BuildCheck { ok = false, reason = reason, cost = 0, affordable = true, blocks = blocks };
            if (t == null) return fail("Turm nicht gefunden");
            if (Towers.TOWERS[t.def].legendary != true) return fail("Nur legendäre Türme können versetzt werden");
            if (!Pathfinding.inBounds(map, cx, cy)) return fail("Außerhalb der Karte");
            if (t.cx == cx && t.cy == cy) return fail("Der Turm steht bereits hier");
            double idx = cy * map.width + cx;
            var other = U.Get(sim.c.towerByCell, idx);
            if (other != null && other != t) return fail("Feld ist bereits belegt");
            if (!Pathfinding.isBuildableCell(map, idx)) return fail("Hier kann nicht gebaut werden");
            if (map.variant == "mazing")
            {
                foreach (var e in s.enemies)
                {
                    if (!e.alive || e.flying) continue;
                    if ((Math.Floor(e.x) == cx && Math.Floor(e.y) == cy) || (e.nx == cx && e.ny == cy)) return fail("Ein Gegner steht im Weg");
                }
                var blocked = (byte[])sim.c.blocked.Clone();
                foreach (var i in Towers.footprintCells(t.def, t.cx, t.cy, map.width)) blocked[(int)i] = 0;
                blocked[(int)idx] = 1;
                var field = Pathfinding.computeField(map, blocked);
                foreach (var sp in WorldGenerator.spawnsOf(map)) if (field[(int)(sp.y * map.width + sp.x)] >= Pathfinding.INF) return fail("Würde den Weg vollständig blockieren", true);
                foreach (var e in s.enemies)
                {
                    if (!e.alive || e.flying) continue;
                    if (field[(int)(e.ny * map.width + e.nx)] >= Pathfinding.INF) return fail("Würde Gegner einsperren", true);
                }
                return new BuildCheck { ok = true, cost = 0, affordable = true, route = Pathfinding.tracePath(map, field, map.spawn) };
            }
            return new BuildCheck { ok = true, cost = 0, affordable = true };
        }

        public static CommandResult moveTower(GameSimulation sim, double uid, double cx, double cy)
        {
            var chk = canMoveTower(sim, uid, cx, cy);
            if (!chk.ok) return CommandResult.Fail(chk.reason ?? "Nicht möglich");
            var t = sim.c.towerByUid[uid];
            var @from = new Vec2 { x = t.cx + 0.5, y = t.cy + 0.5 };
            t.cx = cx;
            t.cy = cy;
            t.target = 0;
            t.charge = 0;
            t.stunned = U.Max(t.stunned, MOVE_STUN);
            sim.reindex();
            markDirty(sim);
            sim.emit(new GameEvent { k = "sell", tower = t.def, x = @from.x, y = @from.y, refund = 0 });
            sim.emit(new GameEvent { k = "build", tower = t.def, x = cx + 0.5, y = cy + 0.5 });
            sim.emit(new GameEvent { k = "message", text = $"{Towers.TOWERS[t.def].name} versetzt – {U.Fmt(MOVE_STUN)} s Aufbau", severity = "info" });
            return CommandResult.Ok();
        }
    }
}
