// Übersetzung von sim/Bot.ts.
// Automatischer Testspieler für Integrations- und Balancetests. Er spielt ausschließlich über
// die öffentlichen Befehle der Simulation – wie ein menschlicher Spieler über die UI.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class BotProfile
    {
        public string name;
        public List<string> rotation = new List<string>();   // TowerId[]
        public double econ;          // Anzahl Goldgräber, die früh gebaut werden
        public double upgradeBias;   // 0..1: Neigung, eher aufzuwerten als neu zu bauen
    }

    // PORT: TS-Klasse Bot → C#-Klasse Bot (nicht statisch); die freie Konstante BOT_PROFILES liegt als statisches Feld darin.
    public partial class Bot
    {
        public static readonly List<BotProfile> BOT_PROFILES = new List<BotProfile>
        {
            new BotProfile { name = "Ausgewogen", rotation = new List<string> { "ranger", "ember", "frost", "storm", "ballista", "rune", "cannon", "spore", "resonator", "pylon" }, econ = 1, upgradeBias = 0.5 },
            new BotProfile { name = "Elementar", rotation = new List<string> { "ember", "frost", "storm", "pylon", "storm", "ember", "rune", "spore" }, econ = 0, upgradeBias = 0.45 },
            new BotProfile { name = "Physisch", rotation = new List<string> { "ranger", "cannon", "ballista", "ranger", "cannon", "pylon", "ballista" }, econ = 0, upgradeBias = 0.55 },
            new BotProfile { name = "Wirtschaft", rotation = new List<string> { "ranger", "storm", "ember", "ballista", "frost", "rune" }, econ = 3, upgradeBias = 0.5 },
            new BotProfile { name = "Kontrolle", rotation = new List<string> { "ranger", "frost", "riftbinder", "resonator", "storm", "rune", "spore", "cannon" }, econ = 1, upgradeBias = 0.5 },
            new BotProfile { name = "Neue Türme", rotation = new List<string> { "ranger", "tesla", "tesla", "meteor", "gravity", "laser", "dragon", "totem", "resonator" }, econ = 0, upgradeBias = 0.5 },
        };

        private double rot = 0;
        public readonly GameSimulation sim;
        public readonly BotProfile profile;

        public Bot(GameSimulation sim, BotProfile profile)
        {
            this.sim = sim;
            this.profile = profile;
        }

        /// <summary>JS <c>a ** 2</c> (V8/fdlibm: exakt a*a).</summary>
        private static double Sq(double a) { return a * a; }

        /// <summary>Coverage-Score einer Zelle: Anzahl Routenzellen in Reichweite.</summary>
        private double coverage(double cx, double cy, double r, List<GridPos> route)
        {
            double n = 0;
            double r2 = r * r;
            foreach (GridPos p in route) if (Sq(p.x - cx) + Sq(p.y - cy) <= r2) n++;
            return n;
        }

        private GridPos bestCell(string def)
        {
            GameSimulation sim = this.sim;
            MapDefinition map = sim.s.map;
            // Alle Risse berücksichtigen (Weggabelungen auf großen Karten)
            List<GridPos> route = WorldGenerator.spawnsOf(map).SelectMany((sp) => Pathfinding.tracePath(map, sim.c.field, sp)).ToList();
            double r = U.Or(U.GetN(Towers.TOWERS[def].@base, "range") ?? 3, 2.5);
            GridPos best = null;
            double bs = -1;
            for (double y = 0; y < map.height; y++)
            {
                for (double x = 0; x < map.width; x++)
                {
                    double i = y * map.width + x;
                    if (!Pathfinding.isBuildableCell(map, i) || sim.c.towerByCell.ContainsKey(i)) continue;
                    // Im Mazing bleiben die Spalten des Labyrinthplans (inkl. Durchgänge) frei.
                    if (map.variant == "mazing" && x >= 3 && x < map.width - 3 && x % 3 == 0) continue;
                    double sc;
                    if (def == "goldmine") sc = (new double[] { i, i + 1, i + map.width, i + map.width + 1 }.Any((k) => map.veins.Contains(k)) ? 100 : 0) - this.coverage(x + 0.5, y + 0.5, 2, route);
                    else if (def == "pylon")
                    {
                        sc = 0;
                        foreach (TowerInstance t in sim.s.towers) if (Sq(t.cx - x) + Sq(t.cy - y) <= 9) sc += 3;
                        sc += this.coverage(x, y, 2, route) * 0.1;
                    }
                    else sc = this.coverage(x, y, r, route) + (map.heights[(int)i] >= 2 ? 2 : 0) + (map.ley.Contains(i) ? 2 : 0);
                    // Leichte Spannweite für deterministische Vielfalt
                    sc += ((x * 7 + y * 13) % 5) * 0.01;
                    if (sc <= bs) continue;
                    if (!sim.canBuild(def, x, y).ok) continue;
                    bs = sc;
                    best = new GridPos(x, y);
                }
            }
            return best;
        }

        private List<GridPos> wallPlan = null;

        /// <summary>Mazing: Serpentinen-Plan aus Barrikadenreihen mit abwechselnden Durchgängen.</summary>
        private GridPos nextWall()
        {
            GameSimulation sim = this.sim;
            MapDefinition map = sim.s.map;
            if (this.wallPlan == null)
            {
                this.wallPlan = new List<GridPos>();
                bool top = true;
                for (double x = 3; x < map.width - 3; x += 3)
                {
                    List<double> ys = new List<double>();
                    for (double y = 0; y < map.height; y++) ys.Add(y);
                    List<double> gap = top ? new List<double> { 0, 1 } : new List<double> { map.height - 1, map.height - 2 };
                    // Zuerst die Zellen nahe der aktuellen Route, damit frühe Wände sofort wirken
                    U.StableSort(ys, (a, b) => Math.Abs(a - map.spawn.y) - Math.Abs(b - map.spawn.y));
                    foreach (double y in ys) if (!gap.Contains(y)) this.wallPlan.Add(new GridPos(x, y));
                    top = !top;
                }
            }
            while (this.wallPlan.Count > 0)
            {
                GridPos c = this.wallPlan[0];
                BuildCheck chk = sim.canBuild("barricade", c.x, c.y);
                if (chk.ok) return c;
                if (!chk.affordable) return null;
                U.Shift(this.wallPlan);
            }
            return null;
        }

        /// <summary>Nutzt das verfügbare Gold für Bau und Upgrades.</summary>
        public void spend()
        {
            GameSimulation sim = this.sim;
            if (sim.s.map.variant == "mazing")
            {
                // Erst Grundfeuerkraft sichern, dann eine Serpentine aus Barrikaden, danach Türme an die Gänge.
                this.spendTowers(2 + Math.Floor(sim.s.wave / 2));
                double wallsWanted = 30 + sim.s.wave * 8;
                double floor = sim.s.gold * 0.35;
                for (int k = 0; k < 60; k++)
                {
                    double walls = sim.s.towers.Count((t) => t.def == "barricade");
                    if (walls >= wallsWanted || sim.s.gold < 4 || sim.s.gold < floor) break;
                    GridPos c = this.nextWall();
                    if (c == null || !sim.build("barricade", c.x, c.y).ok) break;
                    if (this.wallPlan != null) U.Shift(this.wallPlan);
                }
            }
            this.spendTowers(double.PositiveInfinity);
        }

        /// <summary>Bau- und Ausbauschleife bis maxFighters Kampftürme stehen oder das Gold aufgebraucht ist.</summary>
        private void spendTowers(double maxFighters)
        {
            GameSimulation sim = this.sim;
            for (double guard = 0; guard < 40; guard++)
            {
                GameState s = sim.s;
                List<TowerInstance> fighters = s.towers.Where((t) => t.def != "goldmine" && t.def != "barricade").ToList();
                if (fighters.Count >= maxFighters) return;
                // Ab Welle 7 Detektoren gegen getarnte Gegner einplanen
                double detectors = s.towers.Count((t) => Towers.TOWERS[t.def].detector == true);
                if (s.wave >= 7 && detectors < 1 + Math.Floor((s.wave - 7) / 6) && s.gold >= EconomySystem.towerCost(sim, "rune"))
                {
                    GridPos c = this.bestCell("rune");
                    if (c != null && sim.build("rune", c.x, c.y).ok) continue;
                }
                double mines = s.towers.Count((t) => t.def == "goldmine");
                if (mines < this.profile.econ && s.wave >= 1 && s.wave <= 6 && s.gold >= EconomySystem.towerCost(sim, "goldmine"))
                {
                    GridPos c = this.bestCell("goldmine");
                    if (c != null && sim.build("goldmine", c.x, c.y).ok) continue;
                }
                double minTowers = 4 + s.wave * 0.7;
                bool wantUpgrade = fighters.Count >= minTowers && ((s.wave * 7 + guard) % 10) / 10 < this.profile.upgradeBias + 0.3;
                if (wantUpgrade && fighters.Count > 0)
                {
                    List<TowerInstance> sorted = new List<TowerInstance>(fighters);
                    U.StableSort(sorted, (a, b) => U.Or(a.level - b.level, b.damageDealt - a.damageDealt));
                    TowerInstance cand = sorted[0];
                    double? cost = EconomySystem.nextUpgradeCost(cand);
                    if (cost != null && s.gold >= cost.Value)
                    {
                        if (sim.upgrade(cand.uid).ok) continue;
                    }
                    else if (cost == null && cand.spec == 0 && Towers.TOWERS[cand.def].specs.Count == 2)
                    {
                        double sp = cand.uid % 2 == 0 ? 1 : 2;
                        if (s.gold >= EconomySystem.specCost(cand, sp) && sim.specialize(cand.uid, sp).ok) continue;
                    }
                }
                string def = this.profile.rotation[(int)(this.rot % this.profile.rotation.Count)];
                if (s.gold < EconomySystem.towerCost(sim, def))
                {
                    if (fighters.Count < minTowers && s.gold >= EconomySystem.towerCost(sim, "ranger")) def = "ranger";
                    else return;
                }
                GridPos cell = this.bestCell(def);
                if (cell == null)
                {
                    this.rot++;
                    continue;
                }
                if (sim.build(def, cell.x, cell.y).ok) this.rot++;
                else return;
            }
        }

        /// <summary>Helden einsetzen, Fähigkeiten nutzen, Perks wählen; Fusionen und Turmfähigkeiten.</summary>
        public void tactics()
        {
            GameSimulation sim = this.sim;
            GameState s = sim.s;
            List<GridPos> route = Pathfinding.tracePath(s.map, sim.c.field, s.map.spawn);
            foreach (HeroInstance h in s.heroes)
            {
                if (h.pendingPerk) sim.heroPerk(h.id, h.level % 2);
                if (h.state == "ready" && s.phase == "combat" && s.riftEnergy >= Heroes.HEROES[h.id].cost && route.Count > 4)
                {
                    double frac = Heroes.HEROES[h.id].support ? 0.45 : 0.7;
                    for (int k = 0; k < 6; k++)
                    {
                        GridPos c = route[(int)Math.Min(route.Count - 2, Math.Floor(route.Count * frac) + k)];
                        if (HeroSystem.canPlaceHero(sim, c.x, c.y).ok)
                        {
                            sim.deployHero(h.id, c.x, c.y);
                            break;
                        }
                    }
                }
                if (h.state == "active")
                {
                    bool near = s.enemies.Any((e) => e.alive && Sq(e.x - h.x) + Sq(e.y - h.y) < 9);
                    foreach (double slot in new double[] { 0, 1, 2 })
                    {
                        if (HeroSystem.abilityReady(sim, h, slot).ok && (near || Heroes.HEROES[h.id].support)) sim.heroAbility(h.id, slot);
                    }
                }
            }
            if (s.phase == "combat")
            {
                bool busy = s.enemies.Count((e) => e.alive) >= 6;
                foreach (TowerInstance t in s.towers) if (busy && EconomySystem.activeState(sim, t).available) sim.towerActive(t.uid);
            }
            else
            {
                // PORT: for..of über s.towers; fuse() ändert s.towers, danach wird sofort zurückgekehrt (kein Weiteriterieren).
                foreach (TowerInstance t in s.towers)
                {
                    foreach (var o in EconomySystem.fusionOptions(sim, t))
                    {
                        if (o.affordable && sim.fuse(t.uid, o.partner.uid).ok) return;
                    }
                }
            }
        }

        /// <summary>Eine Entscheidungsrunde ohne Simulationsschritt (für die lebendige Menüwelt).</summary>
        public void act()
        {
            GameSimulation sim = this.sim;
            if (sim.isOver()) return;
            if (sim.s.phase == "choice")
            {
                List<string> offer = sim.s.modOffer ?? new List<string>();
                sim.chooseModifier(offer.Count != 0 ? offer[0] : null);
            }
            if (sim.s.phase == "build")
            {
                this.tactics();
                this.spend();
                sim.startWave();
            }
            else if (sim.s.phase == "combat")
            {
                this.spend();
                this.tactics();
            }
        }

        /// <summary>Spielt die Partie bis zum Ende (oder bis maxWaves).</summary>
        public void play(double maxTicks = 30 * 60 * 90, double maxWaves = double.PositiveInfinity)
        {
            GameSimulation sim = this.sim;
            double ticks = 0;
            while (!sim.isOver() && ticks < maxTicks)
            {
                if (sim.s.phase == "choice")
                {
                    List<string> offer = sim.s.modOffer ?? new List<string>();
                    sim.chooseModifier(offer.Count != 0 ? offer[0] : null);
                }
                if (sim.s.phase == "build")
                {
                    if (sim.s.wave >= maxWaves) return;
                    this.tactics();
                    this.spend();
                    sim.startWave();
                }
                sim.step();
                ticks++;
                if (sim.s.phase == "combat" && sim.s.tick % 90 == 0) this.spend();
                if (sim.s.phase == "combat" && sim.s.tick % 15 == 0) this.tactics();
            }
        }
    }
}
