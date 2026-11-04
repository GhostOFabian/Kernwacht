// Übersetzung von sim/RuneSystem.ts.
// Runen (2.7): nur findbar ab Welle 250, in Turmsockel einsetzbar, jederzeit kostenlos wieder herausnehmbar.
//
// PORT: Annahmen über config/runes.ts (Klasse Runes): RUNE_ORDER Liste/Array von string, RUNE_TIERS Liste/Array
// mit Feld mul, RUNE_SLOT_WAVES Liste/Array von double, RUNE_RULES Objekt mit fromWave/splinterFrom/bagMax und
// dropChance als Dictionary<string, double>; runeEligible(string), runeSockets(string, double) → double,
// runeMods(string, double) → List<StatMod>, runeLabel(string, double) → string.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class RuneSystem
    {
        public static List<RuneInstance> runesOf(GameSimulation sim)
        {
            List<RuneInstance> list = sim.s.he?.runes ?? new List<RuneInstance>();
            // 3.3: Altstände (Rune an einem einzelnen Turm) in die Turmsorte übernehmen; Überzähliges zurück in den Beutel
            if (list.Any((r) => r.tower > 0))
            {
                foreach (RuneInstance r in list)
                {
                    if (r.tower <= 0) continue;
                    double rt = r.tower;
                    TowerInstance t = U.Get(sim.c.towerByUid, r.tower) ?? sim.s.towers.FirstOrDefault((x) => x.uid == rt);
                    r.tower = 0;
                    if (t == null || U.Truthy(r.def)) continue;
                    double used = list.Count((o) => o.def == t.def);
                    if (used < Runes.runeSockets(t.def, sim.s.wave)) r.def = t.def;
                }
            }
            return list;
        }

        /// <summary>Gesockelte Runen einer Turmsorte.</summary>
        public static List<RuneInstance> defRunes(GameSimulation sim, string def)
        {
            return runesOf(sim).Where((r) => r.def == def).ToList();
        }

        /// <summary>Runen, die auf diesen Turm wirken (die seiner Sorte).</summary>
        public static List<RuneInstance> towerRunes(GameSimulation sim, TowerInstance t)
        {
            return defRunes(sim, t.def);
        }

        public static List<RuneInstance> bagRunes(GameSimulation sim)
        {
            return runesOf(sim).Where((r) => !U.Truthy(r.def)).ToList();
        }

        /// <summary>Wertänderungen aller Runen der Sorte eines Turms.</summary>
        public static List<StatMod> towerRuneMods(GameSimulation sim, TowerInstance t)
        {
            List<StatMod> @out = new List<StatMod>();
            foreach (RuneInstance r in towerRunes(sim, t)) @out.AddRange(Runes.runeMods(r.rune, r.tier));
            return @out;
        }

        /// <summary>Zusätzliches Kopfgeld durch Gierrunen der Sorte des erlegenden Turms.</summary>
        public static double runeKillGold(GameSimulation sim, TowerInstance t)
        {
            double g = 0;
            foreach (RuneInstance r in towerRunes(sim, t)) if (r.rune == "greed") g += Runes.RUNE_TIERS[(int)(r.tier - 1)].mul;
            return U.Round(g);
        }

        /// <summary>Rune in die Sorte des Turms sockeln – gilt sofort für alle Türme dieser Sorte.</summary>
        public static CommandResult socketRune(GameSimulation sim, double towerUid, double runeUid)
        {
            TowerInstance t = U.Get(sim.c.towerByUid, towerUid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            return socketRuneDef(sim, t.def, runeUid);
        }

        public static CommandResult socketRuneDef(GameSimulation sim, string def, double runeUid)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Nur im Helden-Endlosmodus");
            RuneInstance r = runesOf(sim).FirstOrDefault((x) => x.uid == runeUid);
            if (r == null) return CommandResult.Fail("Rune nicht gefunden");
            if (r.def == def) return CommandResult.Fail("Bereits gesockelt");
            double cap = Runes.runeSockets(def, sim.s.wave);
            if (cap <= 0) return CommandResult.Fail(Runes.runeEligible(def) ? $"Runensockel ab Welle {U.Fmt(Runes.RUNE_SLOT_WAVES[0])}" : "Diese Sorte hat keine Runensockel");
            if (defRunes(sim, def).Count >= cap) return CommandResult.Fail($"Alle {U.Fmt(cap)} Sockel dieser Sorte belegt");
            r.def = def;
            r.tower = 0;
            sim.c.statsDirty = true;
            return CommandResult.Ok();
        }

        public static CommandResult unsocketRune(GameSimulation sim, double runeUid)
        {
            RuneInstance r = runesOf(sim).FirstOrDefault((x) => x.uid == runeUid);
            if (sim.s.he == null || r == null) return CommandResult.Fail("Rune nicht gefunden");
            if (!U.Truthy(r.def)) return CommandResult.Fail("Liegt bereits im Beutel");
            r.def = null;
            sim.c.statsDirty = true;
            return CommandResult.Ok();
        }

        /// <summary>3.3: Runen gehören der Turmsorte und bleiben beim Verkauf/bei Fusionen gesockelt.</summary>
        public static void releaseRunes(GameSimulation _sim, double _towerUid) { }

        private static double rollTier(GameSimulation sim, double wave)
        {
            if (wave < Runes.RUNE_RULES.fromWave) return 1;
            double v = LootSystem.lootRand(sim);
            double t3 = U.Min(0.3, 0.05 + (wave - Runes.RUNE_RULES.fromWave) / 1000);
            if (v < t3) return 3;
            if (v < t3 + 0.3) return 2;
            return 1;
        }

        /// <summary>Rune finden (Gegner oder Wellenabschluss) – erst ab Welle 250. null = keine.</summary>
        public static RuneInstance dropRune(GameSimulation sim, double x, double y)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || sim.s.wave < Runes.RUNE_RULES.splinterFrom) return null;
            he.runes ??= new List<RuneInstance>();
            int orderLen = Enumerable.Count(Runes.RUNE_ORDER);
            string id = Runes.RUNE_ORDER[(int)(Math.Floor(LootSystem.lootRand(sim) * orderLen) % orderLen)];
            double tier = rollTier(sim, sim.s.wave);
            if (bagRunes(sim).Count >= Runes.RUNE_RULES.bagMax)
            {
                // Beutel voll: schwächste Rune im Beutel wird ersetzt, wenn die neue besser ist
                List<RuneInstance> bag = bagRunes(sim);
                U.StableSort(bag, (a, b) => U.Or(a.tier - b.tier, a.uid - b.uid));
                RuneInstance weakest = bag.Count > 0 ? bag[0] : null;
                if (weakest == null || weakest.tier >= tier) return null;
                he.runes = he.runes.Where((r) => r != weakest).ToList();
            }
            RuneInstance inst = new RuneInstance { uid = sim.uid(), rune = id, tier = tier, tower = 0 };
            he.runes.Add(inst);
            sim.emit(new GameEvent { k = "pickup", kind = "resource", x = x, y = y, label = $"✦ {Runes.runeLabel(id, tier)}", color = 0xc0a0ff });
            sim.emit(new GameEvent { k = "message", text = $"Rune gefunden: {Runes.runeLabel(id, tier)} – im Turmpanel sockeln.", severity = "info" });
            return inst;
        }

        /// <summary>tier: Schlüssel von RUNE_RULES.dropChance ('normal' | 'elite' | 'miniBoss' | 'boss').</summary>
        public static void rollEnemyRune(GameSimulation sim, string tier, double x, double y)
        {
            if (sim.s.he == null || sim.s.wave < Runes.RUNE_RULES.splinterFrom) return;
            // PORT: dropChance als Dictionary<string, double> angenommen.
            if (LootSystem.lootRand(sim) < Runes.RUNE_RULES.dropChance[tier]) dropRune(sim, x, y);
        }
    }
}
