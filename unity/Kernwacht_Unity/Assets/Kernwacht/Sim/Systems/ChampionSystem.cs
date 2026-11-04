// Übersetzung von sim/ChampionSystem.ts.
// Frei steuerbarer Held des Helden-Endlosmodus: Bewegung über ein eigenes Raster-Pathfinding
// (Türme blockieren nicht), Werte aus Stufe + Baum + Ausrüstung + Wirkungen, Kampf, Fähigkeiten,
// Erfahrung und Turmverstärkungen. Alles deterministisch und im GameState serialisierbar.
//
// PORT: Annahmen über Typen aus anderen Modulen (beim Kompilieren abgleichen):
// - Champions.CHAMPIONS: Dictionary<string, ChampionDefinition>; ChampionDefinition.@base (ChampionStats),
//   perLevel (Dictionary<string,double>), skills (List/Array von ChampionSkill), tree (List<SkillNode>),
//   aura (Objekt mit dmg/speed oder null), blockCount (double?), melee (bool), accent (double), name, damageType.
// - SkillNode: id, name, maxRank, stats/mods (Dictionary<string,double> oder null), requires (Liste mit id/rank oder null),
//   minLevel (double?), perk (string), coreLives (double?).
// - SkillMods: Klasse mit öffentlichen double-Feldern wie in TS (Standard 0 = ZERO_SKILL_MODS).
// - ChampionSkill: id, target, castRange, radius, unlockLevel, mana, cooldown, effects (List<SkillEffect> oder null).
// - SkillEffect: eine Klasse für die Union; Feld base → @base; Namenskonflikt pct: 'execute' (Zahl) → pct,
//   'self' (Partial<Record<keyof ChampionStats, number>>) → pctStats (Dictionary<string,double>).
//   Zahl-/bool-Felder dürfen double/double? bzw. bool/bool? sein (Hilfen N/Nd/NotFalse unten).
// - StatusSpec: id (string), dur (double), power (double?), stacks (double?).
// - Champions.STAT_LIMITS: Dictionary<string, double[]> (oder List<double>).
// - Paragon.PARAGON_NODES: List<ParagonNode>; ParagonNode.per: Dictionary<string,double> (Partial<PassiveEffects>).
// - PassiveTree.runPassive(he) → PassiveEffects (Klasse mit öffentlichen double-Feldern).
// - HeroEndless.HERO_ENDLESS: Objekt mit Feldern wie in TS (towerBuffCap.dmg/speed/range/crit usw.).
// - SimCache.champBuff: Dictionary<double, ChampTowerBuff> (Klasse unten definiert).
// - LootSystem.setGrantXpHook(Action<GameSimulation, double>).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>TS: Parameter <c>v: { dmg?: number; speed?: number; range?: number; crit?: number }</c> von applyTowerBuff.</summary>
    public class TowerBuffValues
    {
        public double? dmg;
        public double? speed;
        public double? range;
        public double? crit;
    }

    /// <summary>TS: Rückgabe von nodeState <c>{ ok: boolean; reason: string | null }</c>.</summary>
    public class NodeStateResult
    {
        public bool ok;
        public string reason;
    }

    /// <summary>TS: Rückgabe von skillState <c>{ ok: boolean; reason: string | null; skill: ChampionSkill | null }</c>.</summary>
    public class SkillStateResult
    {
        public bool ok;
        public string reason;
        public ChampionSkill skill;
    }

    public static partial class ChampionSystem
    {
        /// <summary>UID, unter der Heldenschaden verbucht wird (kein Turm).</summary>
        public const double CHAMPION_UID = 990001;

        /// <summary>
        /// TS: <c>setGrantXpHook((sim, amount) =&gt; grantXp(sim, amount))</c> auf Modulebene (läuft beim Import).
        /// PORT: Der statische Konstruktor läuft erst beim ersten Zugriff auf ChampionSystem. GameSimulation sollte
        /// deshalb beim Erzeugen <see cref="registerHooks"/> aufrufen (idempotent).
        /// </summary>
        static ChampionSystem()
        {
            registerHooks();
        }

        public static void registerHooks()
        {
            LootSystem.setGrantXpHook((sim, amount) => grantXp(sim, amount));
        }

        private static double dist2(double ax, double ay, double bx, double by) { return pow2(ax - bx) + pow2(ay - by); }

        /// <summary>JS <c>x ** 2</c> (V8/fdlibm rechnet für Exponent 2 exakt x*x).</summary>
        private static double pow2(double x) { return x * x; }

        // ---- Hilfen für Felder, deren Nullbarkeit im fremden Modul offen ist (PORT)
        private static double N(double x) { return x; }
        private static double N(double? x) { return x ?? 0; }
        private static double Nd(double x, double d) { return x; }
        private static double Nd(double? x, double d) { return x ?? d; }
        private static bool NotFalse(bool x) { return x != false; }
        private static bool NotFalse(bool? x) { return x != false; }

        // ------------------------------------------------------------ Werte

        /// <summary>TS: <c>out[k] += v</c> für k aus keyof SkillMods.</summary>
        private static void addSkillMod(SkillMods m, string k, double v)
        {
            switch (k)
            {
                case "buffDuration": m.buffDuration += v; break;
                case "buffRadius": m.buffRadius += v; break;
                case "extraTargets": m.extraTargets += v; break;
                case "synergy": m.synergy += v; break;
                case "auraDmg": m.auraDmg += v; break;
                case "auraSpeed": m.auraSpeed += v; break;
                case "auraRange": m.auraRange += v; break;
                case "auraCrit": m.auraCrit += v; break;
                case "whirlRadius": m.whirlRadius += v; break;
                case "dashStun": m.dashStun += v; break;
                case "pierceWidth": m.pierceWidth += v; break;
                case "trapSlow": m.trapSlow += v; break;
                case "volleyPulses": m.volleyPulses += v; break;
                default: break;
            }
        }

        public static SkillMods skillModsOf(ChampionState c)
        {
            // PORT: { ...ZERO_SKILL_MODS } – frische Instanz mit allen Feldern 0.
            SkillMods @out = new SkillMods();
            var def = Champions.CHAMPIONS[c.id];
            foreach (var node in def.tree)
            {
                double rank = U.Get(c.tree, node.id, 0);
                if (!U.Truthy(rank) || node.mods == null) continue;
                foreach (KeyValuePair<string, double> kv in node.mods) addSkillMod(@out, kv.Key, kv.Value * rank);
            }
            return @out;
        }

        /// <summary>Berechnet die effektiven Heldenwerte vollständig neu (kein Aufaddieren über die Zeit → keine Drift).</summary>
        public static ChampionStats championStats(HeroEndlessState he)
        {
            ChampionState c = he.champion;
            var def = Champions.CHAMPIONS[c.id];
            ChampionStats st = def.@base.Copy();
            Action<Dictionary<string, double>, double> add = (p, k) =>
            {
                foreach (KeyValuePair<string, double> kv in p) st[kv.Key] += kv.Value * k;
            };
            add(def.perLevel, effectiveLevelSteps(c.level));
            foreach (var node in def.tree)
            {
                double rank = U.Get(c.tree, node.id, 0);
                if (U.Truthy(rank) && node.stats != null) add(node.stats, rank);
            }
            foreach (double? uid in c.equipment)
            {
                if (uid == null) continue;
                ItemInstance inst = c.inventory.FirstOrDefault((i) => i.uid == uid.Value);
                if (inst != null) add(Items.itemStats(inst.item, inst.plus), 1);
            }
            foreach (ChampionBuff b in c.buffs) add(b.add, 1);
            if (U.Includes(he.perks, "magnet1")) st.pickupRadius += 0.3;
            if (U.Includes(he.perks, "fortune")) st.lootBonus += 0.25;
            // Passiver Baum (dauerhaft)
            var pf = PassiveTree.runPassive(he);
            st.maxHp *= 1 + pf.heroHpPct;
            st.damage *= 1 + pf.heroDmgPct;
            st.armor += pf.heroArmor;
            st.cdr += pf.heroCdr;
            st.critChance += pf.heroCrit;
            st.hpRegen += pf.heroRegen;
            st.manaRegen += pf.manaRegen;
            st.supportPower += pf.supportPower;
            st.pickupRadius += pf.pickupRadius;
            st.lootBonus += pf.lootPct;
            // Paragon (2.7)
            st.attackSpeed *= 1 + pf.heroAtkSpeed;
            st.abilityPower *= 1 + pf.abilityPct;
            st.moveSpeed += pf.moveSpeed;
            st.lifesteal += pf.lifesteal;
            foreach (var kv in Champions.STAT_LIMITS)
            {
                string key = kv.Key;
                double lo = kv.Value[0];
                double hi = kv.Value[1];
                st[key] = U.Min(hi, U.Max(lo, st[key]));
            }
            st.maxHp = U.Max(1, st.maxHp);
            st.maxMana = U.Max(0, st.maxMana);
            return st;
        }

        // ------------------------------------------------------------ Bewegung

        /// <summary>Start- und Wiederbelebungspunkt: begehbares Nachbarfeld des Kerns, möglichst nahe am Laden.</summary>
        public static Vec2 championHome(MapDefinition map, Vec2 shop)
        {
            GridPos g = map.goal;
            Vec2 best = new Vec2(g.x, g.y);
            double bd = double.PositiveInfinity;
            for (double dy = -1; dy <= 1; dy++)
            {
                for (double dx = -1; dx <= 1; dx++)
                {
                    if (!U.Truthy(dx) && !U.Truthy(dy)) continue;
                    double x = g.x + dx;
                    double y = g.y + dy;
                    if (x < 0 || y < 0 || x >= map.width || y >= map.height) continue;
                    if (map.cells[(int)(y * map.width + x)] == Cell.Blocked || (x == shop.x && y == shop.y)) continue;
                    double d = pow2(x - shop.x) + pow2(y - shop.y) + (U.Truthy(dx) && U.Truthy(dy) ? 0.5 : 0);
                    if (d < bd)
                    {
                        bd = d;
                        best = new Vec2(x, y);
                    }
                }
            }
            return new Vec2(best.x + 0.5, best.y + 0.5);
        }

        public static bool championWalkable(GameSimulation sim, double x, double y)
        {
            MapDefinition m = sim.s.map;
            if (x < 0 || y < 0 || x >= m.width || y >= m.height) return false;
            HeroEndlessState he = sim.s.he;
            if (he != null && he.shop.x == x && he.shop.y == y) return false;
            return m.cells[(int)(y * m.width + x)] != Cell.Blocked;
        }

        /// <summary>Kürzester Weg (8 Richtungen, keine Ecken durch Hindernisse). Türme blockieren nicht. null = unerreichbar.</summary>
        public static List<double> findChampionPath(GameSimulation sim, double sx, double sy, double tx, double ty)
        {
            MapDefinition m = sim.s.map;
            int w = (int)m.width;
            if (!championWalkable(sim, tx, ty)) return null;
            if (sx == tx && sy == ty) return new List<double>();
            int n = (int)(w * m.height);
            double[] dist = new double[n];
            for (int i = 0; i < n; i++) dist[i] = double.PositiveInfinity;
            int[] prev = new int[n];
            for (int i = 0; i < n; i++) prev[i] = -1;
            int start = (int)(sy * w + sx);
            int goal = (int)(ty * w + tx);
            dist[start] = 0;
            // Einfacher Dijkstra mit linearer Suche reicht für ≤ 1100 Zellen und seltene Befehle.
            List<int> open = new List<int> { start };
            byte[] inOpen = new byte[n];
            inOpen[start] = 1;
            while (open.Count > 0)
            {
                int bi = 0;
                for (int k = 1; k < open.Count; k++) if (dist[open[k]] < dist[open[bi]]) bi = k;
                int cur = open[bi];
                open[bi] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                inOpen[cur] = 0;
                if (cur == goal) break;
                int cx = cur % w;
                int cy = (cur - cx) / w;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (!championWalkable(sim, nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!championWalkable(sim, cx + dx, cy) || !championWalkable(sim, cx, cy + dy))) continue;
                        int ni = ny * w + nx;
                        double nd = dist[cur] + (dx != 0 && dy != 0 ? 1.4142135623730951 /* Math.SQRT2 */ : 1);
                        if (nd < dist[ni])
                        {
                            dist[ni] = nd;
                            prev[ni] = cur;
                            if (inOpen[ni] == 0)
                            {
                                inOpen[ni] = 1;
                                open.Add(ni);
                            }
                        }
                    }
                }
            }
            if (!U.IsFinite(dist[goal])) return null;
            List<double> path = new List<double>();
            for (int at = goal; at != start && at >= 0; at = prev[at]) path.Add(at);
            path.Reverse();
            return path;
        }

        /// <summary>Position festsetzen/lösen. Festgesetzt: Laufbefehle werden ignoriert, keine Verfolgung von Gegnern.</summary>
        public static CommandResult setHoldPosition(GameSimulation sim, bool hold)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held in dieser Partie");
            ChampionState c = he.champion;
            c.holdPos = hold;
            if (hold)
            {
                c.path = new List<double>();
                c.hx = c.x;
                c.hy = c.y;
            }
            return CommandResult.Ok();
        }

        public static CommandResult orderMove(GameSimulation sim, double tx, double ty)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held in dieser Partie");
            ChampionState c = he.champion;
            if (c.dead > 0) return CommandResult.Fail($"Held wird wiederbelebt ({U.Fmt(Math.Ceiling(c.dead))} s)");
            if (c.holdPos == true) return CommandResult.Fail("Held ist festgesetzt – mit Z oder dem Anker-Knopf lösen");
            if (he.mining != null) return CommandResult.Fail($"Der Held baut ab und kann nicht unterbrochen werden ({U.Fmt(Math.Ceiling(he.mining.t))} s)");
            he.mineTarget = -1;
            List<double> path = findChampionPath(sim, Math.Floor(c.x), Math.Floor(c.y), tx, ty);
            if (path == null) return CommandResult.Fail("Dieses Feld ist nicht erreichbar");
            c.path = path;
            c.hx = tx + 0.5;
            c.hy = ty + 0.5;
            c.target = 0;
            return CommandResult.Ok();
        }

        private static void moveAlong(GameSimulation sim, ChampionState c, double speed, double dt)
        {
            MapDefinition m = sim.s.map;
            double budget = speed * dt;
            while (budget > 0 && c.path.Count > 0)
            {
                double i = c.path[0];
                double tx = (i % m.width) + 0.5;
                double ty = Math.Floor(i / m.width) + 0.5;
                double d = U.Hypot(tx - c.x, ty - c.y);
                if (d <= budget)
                {
                    c.x = tx;
                    c.y = ty;
                    budget -= d;
                    U.Shift(c.path);
                }
                else
                {
                    c.x += ((tx - c.x) / d) * budget;
                    c.y += ((ty - c.y) / d) * budget;
                    c.facing = Math.Atan2(ty - c.y, tx - c.x);
                    budget = 0;
                }
            }
        }

        // ------------------------------------------------------------ Erfahrung & Fähigkeitenbaum

        /// <summary>Stufenzuwachs der Grundwerte: bis Stufe 30 voll, danach halb (sonst wären Stufe-99-Helden übermächtig).</summary>
        public static double effectiveLevelSteps(double level)
        {
            double L = U.Max(1, level);
            if (L <= Paragon.PARAGON.softcapLevel) return L - 1;
            return Paragon.PARAGON.softcapLevel - 1 + (L - Paragon.PARAGON.softcapLevel) * Paragon.PARAGON.softcapMul;
        }

        /// <summary>Skillpunkte, die ein Held bis zu dieser Stufe insgesamt erhält.</summary>
        public static double skillPointsForLevel(double level)
        {
            double n = 0;
            for (double l = 2; l <= level; l++) n += HeroEndless.HERO_ENDLESS.pointsPerLevel + (l % HeroEndless.HERO_ENDLESS.bonusPointEvery == 0 ? 1 : 0);
            return n;
        }

        public static void grantXp(GameSimulation sim, double amount)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || amount <= 0) return;
            ChampionState c = he.champion;
            double mul = (U.Includes(he.perks, "wisdom") ? 1.1 : 1) * (1 + PassiveTree.runPassive(he).xpPct);
            he.stats.xpCollected += amount * mul;
            if (c.level >= Paragon.PARAGON.heroMaxLevel)
            {
                grantParagonXp(sim, amount * mul);
                return;
            }
            c.xp += amount * mul;
            int guard = 0;
            while (c.level < Paragon.PARAGON.heroMaxLevel && c.xp >= HeroEndless.xpToNext(c.level) && guard++ < 100)
            {
                ChampionStats before = championStats(he);
                c.xp -= HeroEndless.xpToNext(c.level);
                c.level++;
                c.skillPoints += HeroEndless.HERO_ENDLESS.pointsPerLevel + (c.level % HeroEndless.HERO_ENDLESS.bonusPointEvery == 0 ? 1 : 0);
                ChampionStats after = championStats(he);
                // Zuwachs an Maximalwerten wird sofort aufgefüllt, dazu 25 % Heilung.
                c.hp = U.Min(after.maxHp, c.hp + (after.maxHp - before.maxHp) + after.maxHp * 0.25);
                c.mana = U.Min(after.maxMana, c.mana + (after.maxMana - before.maxMana));
                he.stats.levelUps++;
                sim.emit(new GameEvent { k = "champLevel", level = c.level, x = c.x, y = c.y });
            }
            if (c.level >= Paragon.PARAGON.heroMaxLevel && c.xp > 0)
            {
                // Überschüssige EP auf Stufe 99 gehen direkt ins Paragon
                double rest = c.xp;
                c.xp = 0;
                grantParagonXp(sim, rest);
            }
        }

        /// <summary>Paragon-EP (ab Heldenstufe 99): jede Paragonstufe gibt einen Punkt, höchstens Paragon 500.</summary>
        public static void grantParagonXp(GameSimulation sim, double amount)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || amount <= 0) return;
            HeroEndlessParagon p = (he.paragon ??= new HeroEndlessParagon { level = 0, xp = 0, points = new Dictionary<string, double>(), rev = 0 });
            if (p.level >= Paragon.PARAGON.maxLevel) return;
            p.xp += amount;
            int guard = 0;
            while (p.level < Paragon.PARAGON.maxLevel && p.xp >= Paragon.paragonXpToNext(p.level) && guard++ < 100)
            {
                p.xp -= Paragon.paragonXpToNext(p.level);
                p.level++;
                ChampionState c = he.champion;
                sim.emit(new GameEvent { k = "message", text = $"✦ Paragon {U.Fmt(p.level)} erreicht – 1 Paragonpunkt (Charakter → Paragon)", severity = "info" });
                sim.emit(new GameEvent { k = "champLevel", level = c.level, x = c.x, y = c.y });
            }
            if (p.level >= Paragon.PARAGON.maxLevel) p.xp = 0;
        }

        /// <summary>Gesamtbudget an Paragonpunkten: Paragonstufe + Vorab-Punkte (ab Heldenstufe 50) + Meilensteine.</summary>
        public static double paragonBudget(HeroEndlessState he)
        {
            return (he.paragon != null ? he.paragon.level : 0) + Paragon.paragonPrePoints(he.champion.level) + (he.paragonBonus ?? 0);
        }

        public static double paragonFree(HeroEndlessState he)
        {
            HeroEndlessParagon p = he.paragon;
            return U.Max(0, paragonBudget(he) - Paragon.paragonSpent(p?.points));
        }

        public static string paragonRaiseReason(HeroEndlessState he, string nodeId)
        {
            var node = Paragon.PARAGON_NODES.FirstOrDefault((n) => n.id == nodeId);
            if (node == null) return "Unbekannter Knoten";
            HeroEndlessParagon p = (he.paragon ??= new HeroEndlessParagon { level = 0, xp = 0, points = new Dictionary<string, double>(), rev = 0 });
            if (paragonBudget(he) <= 0) return $"Paragonpunkte gibt es ab Heldenstufe 50 (alle 5 Stufen), ab Stufe {U.Fmt(Paragon.PARAGON.heroMaxLevel)} je Paragonstufe und durch Meilensteine";
            if (U.Get(p.points, node.id, 0) >= node.maxRank) return "Maximaler Rang";
            if (paragonFree(he) <= 0) return "Keine Paragonpunkte frei";
            return null;
        }

        public static CommandResult spendParagonPoint(GameSimulation sim, string nodeId)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            string why = paragonRaiseReason(he, nodeId);
            if (U.Truthy(why)) return CommandResult.Fail(why);
            HeroEndlessParagon p = he.paragon;
            var node = Paragon.PARAGON_NODES.FirstOrDefault((n) => n.id == nodeId);
            ChampionStats before = championStats(he);
            p.points[nodeId] = U.Get(p.points, nodeId, 0) + 1;
            p.rev++;
            // PORT: ParagonNode.per als Dictionary<string,double> (Partial<PassiveEffects>) angenommen.
            double? coreLives = U.GetN(node.per, "coreLives");
            if (U.Truthy(coreLives))
            {
                sim.s.lives += coreLives.Value;
                sim.s.maxLives += coreLives.Value;
            }
            ChampionStats after = championStats(he);
            he.champion.hp = U.Min(after.maxHp, he.champion.hp + U.Max(0, after.maxHp - before.maxHp));
            sim.c.champBuffSig = "";
            sim.c.statsDirty = true;
            return CommandResult.Ok();
        }

        /// <summary>Paragonpunkte kostenlos neu verteilen (3.4: jederzeit; Bollwerk-Kernenergie wird wieder abgezogen).</summary>
        public static CommandResult resetParagon(GameSimulation sim)
        {
            HeroEndlessState he = sim.s.he;
            if (he?.paragon == null) return CommandResult.Fail("Kein Paragon");
            if (Paragon.paragonSpent(he.paragon.points) <= 0) return CommandResult.Fail("Keine Punkte vergeben");
            var coreNode = Paragon.PARAGON_NODES.FirstOrDefault((n) => n.id == "pg_core");
            double core = U.Get(he.paragon.points, "pg_core", 0) * (coreNode != null ? (U.GetN(coreNode.per, "coreLives") ?? 0) : 0);
            sim.s.maxLives = U.Max(1, sim.s.maxLives - core);
            sim.s.lives = U.Max(1, U.Min(sim.s.maxLives, sim.s.lives - core));
            he.paragon.points = new Dictionary<string, double>();
            he.paragon.rev++;
            ChampionStats st = championStats(he);
            he.champion.hp = U.Min(st.maxHp, he.champion.hp);
            sim.c.champBuffSig = "";
            sim.c.statsDirty = true;
            return CommandResult.Ok();
        }

        public static NodeStateResult nodeState(GameSimulation sim, SkillNode node)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return new NodeStateResult { ok = false, reason = "Kein Held" };
            ChampionState c = he.champion;
            double rank = U.Get(c.tree, node.id, 0);
            if (rank >= node.maxRank) return new NodeStateResult { ok = false, reason = "Maximaler Rang" };
            if (U.Truthy(node.perk) && !U.Includes(he.perks, node.perk)) return new NodeStateResult { ok = false, reason = "Benötigt den Perk „Meisterschaft“ (Welle 20)" };
            if (U.Truthy(node.minLevel) && c.level < N(node.minLevel)) return new NodeStateResult { ok = false, reason = $"Ab Heldenstufe {U.Fmt(N(node.minLevel))}" };
            if (node.requires != null)
            {
                foreach (var r in node.requires)
                {
                    if (U.Get(c.tree, r.id, 0) < r.rank)
                    {
                        var other = Champions.CHAMPIONS[c.id].tree.FirstOrDefault((n) => n.id == r.id);
                        return new NodeStateResult { ok = false, reason = $"Benötigt {(other != null && other.name != null ? other.name : r.id)} Rang {U.Fmt(r.rank)}" };
                    }
                }
            }
            if (c.skillPoints <= 0) return new NodeStateResult { ok = false, reason = "Keine Fertigkeitspunkte" };
            return new NodeStateResult { ok = true, reason = null };
        }

        /// <summary>3.4: Skillbaum des Helden kostenlos zurücksetzen – alle Punkte zurück, Kernwall-Kernenergie wird abgezogen.</summary>
        public static CommandResult resetSkillTree(GameSimulation sim)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            ChampionState c = he.champion;
            var tree = Champions.CHAMPIONS[c.id].tree;
            double spent = 0;
            double core = 0;
            foreach (var n in tree)
            {
                double r = U.Get(c.tree, n.id, 0);
                spent += r;
                if (U.Truthy(n.coreLives)) core += r * N(n.coreLives);
            }
            if (spent <= 0) return CommandResult.Fail("Keine Punkte vergeben");
            sim.s.maxLives = U.Max(1, sim.s.maxLives - core);
            sim.s.lives = U.Max(1, U.Min(sim.s.maxLives, sim.s.lives - core));
            c.tree = new Dictionary<string, double>();
            c.skillPoints += spent;
            ChampionStats st = championStats(he);
            c.hp = U.Min(st.maxHp, c.hp);
            c.mana = U.Min(st.maxMana, c.mana);
            sim.c.champBuffSig = "";
            sim.c.statsDirty = true;
            return CommandResult.Ok($"{U.Fmt(spent)} Punkte zurückerstattet");
        }

        public static CommandResult spendSkillPoint(GameSimulation sim, string nodeId)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            var node = Champions.CHAMPIONS[he.champion.id].tree.FirstOrDefault((n) => n.id == nodeId);
            if (node == null) return CommandResult.Fail("Unbekannter Knoten");
            NodeStateResult st = nodeState(sim, node);
            if (!st.ok) return CommandResult.Fail(st.reason ?? "Nicht möglich");
            ChampionStats before = championStats(he);
            he.champion.tree[node.id] = U.Get(he.champion.tree, node.id, 0) + 1;
            he.champion.skillPoints--;
            if (U.Truthy(node.coreLives))
            {
                // Kernwall: mehr Kernenergie – die Zahl erlaubter Durchbrüche steigt sofort
                sim.s.lives += N(node.coreLives);
                sim.s.maxLives += N(node.coreLives);
            }
            ChampionStats after = championStats(he);
            he.champion.hp = U.Min(after.maxHp, he.champion.hp + U.Max(0, after.maxHp - before.maxHp));
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Turmverstärkungen

        /// <summary>
        /// Verstärkt einen Turm. Regeln: je Turm und Quelle genau ein Eintrag; erneutes Anwenden erneuert die
        /// Dauer (ohne Verlängerung über die Grunddauer hinaus) und übernimmt jeweils den höheren Wert.
        /// Verschiedene Quellen addieren sich, die Summe ist gedeckelt (HERO_ENDLESS.towerBuffCap).
        /// </summary>
        public static void applyTowerBuff(GameSimulation sim, double tower, string src, TowerBuffValues v, double dur)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || dur <= 0) return;
            TowerBuff cur = he.towerBuffs.FirstOrDefault((b) => b.tower == tower && b.src == src);
            if (cur != null)
            {
                cur.t = U.Max(cur.t, dur);
                cur.dur = U.Max(cur.dur, dur);
                cur.dmg = U.Max(cur.dmg, v.dmg ?? 0);
                cur.speed = U.Max(cur.speed, v.speed ?? 0);
                cur.range = U.Max(cur.range, v.range ?? 0);
                cur.crit = U.Max(cur.crit, v.crit ?? 0);
            }
            else
            {
                he.towerBuffs.Add(new TowerBuff { tower = tower, src = src, t = dur, dur = dur, dmg = v.dmg ?? 0, speed = v.speed ?? 0, range = v.range ?? 0, crit = v.crit ?? 0 });
            }
            TowerInstance t = U.Get(sim.c.towerByUid, tower);
            if (t != null) sim.emit(new GameEvent { k = "towerBuff", x = t.cx + 0.5, y = t.cy + 0.5, color = Champions.CHAMPIONS[he.champion.id].accent });
            sim.c.champBuffSig = "";
        }

        private static void updateTowerBuffs(GameSimulation sim, double dt)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            bool changed = false;
            foreach (TowerBuff b in he.towerBuffs) b.t -= dt;
            int before = he.towerBuffs.Count;
            he.towerBuffs = he.towerBuffs.Where((b) => b.t > 0 && sim.c.towerByUid.ContainsKey(b.tower)).ToList();
            if (he.towerBuffs.Count != before) changed = true;
            sim.c.champBuffTimer -= dt;
            if (!changed && sim.c.champBuffTimer > 0 && sim.c.champBuffSig != "") return;
            sim.c.champBuffTimer = 0.25;
            rebuildChampionBuffs(sim);
        }

        /// <summary>Aggregiert Fähigkeiten-Verstärkungen und die passive Aura je Turm (mit Obergrenzen).</summary>
        public static void rebuildChampionBuffs(GameSimulation sim)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            var cap = HeroEndless.HERO_ENDLESS.towerBuffCap;
            Dictionary<double, ChampTowerBuff> map = new Dictionary<double, ChampTowerBuff>();
            Func<double, ChampTowerBuff> get = (uid) =>
            {
                ChampTowerBuff a;
                if (!map.TryGetValue(uid, out a))
                {
                    a = new ChampTowerBuff { dmg = 0, speed = 0, range = 0, crit = 0 };
                    map[uid] = a;
                }
                return a;
            };
            foreach (TowerBuff b in he.towerBuffs)
            {
                ChampTowerBuff a = get(b.tower);
                a.dmg += b.dmg;
                a.speed += b.speed;
                a.range += b.range;
                a.crit += b.crit;
            }
            ChampionState c = he.champion;
            var def = Champions.CHAMPIONS[c.id];
            if (c.dead <= 0)
            {
                ChampionStats st = championStats(he);
                SkillMods sm = skillModsOf(c);
                double auraDmg = ((def.aura != null ? N(def.aura.dmg) : 0) + sm.auraDmg) * st.supportPower;
                double auraSpd = ((def.aura != null ? N(def.aura.speed) : 0) + sm.auraSpeed) * st.supportPower;
                if (auraDmg > 0 || auraSpd > 0 || sm.auraRange > 0 || sm.auraCrit > 0)
                {
                    double r2 = pow2(st.supportRadius);
                    foreach (TowerInstance t in sim.s.towers)
                    {
                        if (Towers.TOWERS[t.def].attack == "economy" || Towers.TOWERS[t.def].attack == "none") continue;
                        if (dist2(t.cx + 0.5, t.cy + 0.5, c.x, c.y) > r2) continue;
                        ChampTowerBuff a = get(t.uid);
                        a.dmg += auraDmg;
                        a.speed += auraSpd;
                        a.range += sm.auraRange;
                        a.crit += sm.auraCrit;
                    }
                }
            }
            foreach (ChampTowerBuff a in map.Values)
            {
                a.dmg = U.Min(cap.dmg, a.dmg);
                a.speed = U.Min(cap.speed, a.speed);
                a.range = U.Min(cap.range, a.range);
                a.crit = U.Min(cap.crit, a.crit);
            }
            List<KeyValuePair<double, ChampTowerBuff>> entries = U.Entries(map);
            U.StableSort(entries, (x, y) => x.Key - y.Key);
            string sig = U.Join(entries.Select((kv) => $"{U.Fmt(kv.Key)}:{U.Fixed(kv.Value.dmg, 3)}/{U.Fixed(kv.Value.speed, 3)}/{U.Fixed(kv.Value.range, 2)}/{U.Fixed(kv.Value.crit, 2)}"), ",");
            if (sig != sim.c.champBuffSig)
            {
                sim.c.champBuff = map;
                sim.c.champBuffSig = U.Or(sig, "-");
                sim.c.statsDirty = true;
            }
        }

        public static List<TowerBuff> buffsOfTower(GameSimulation sim, double uid)
        {
            return sim.s.he?.towerBuffs.Where((b) => b.tower == uid).ToList() ?? new List<TowerBuff>();
        }

        // ------------------------------------------------------------ Fähigkeiten

        public static SkillStateResult skillState(GameSimulation sim, double slot)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return new SkillStateResult { ok = false, reason = "Kein Held", skill = null };
            ChampionState c = he.champion;
            // TS: CHAMPIONS[c.id].skills[slot] – außerhalb des Bereichs undefined
            ChampionSkill skill = slot >= 0 && slot == Math.Floor(slot) ? Enumerable.ElementAtOrDefault(Champions.CHAMPIONS[c.id].skills, (int)slot) : null;
            if (skill == null) return new SkillStateResult { ok = false, reason = "Kein Skill", skill = null };
            if (c.dead > 0) return new SkillStateResult { ok = false, reason = "Held ist besiegt", skill = skill };
            if (c.level < skill.unlockLevel) return new SkillStateResult { ok = false, reason = $"Ab Stufe {U.Fmt(skill.unlockLevel)}", skill = skill };
            double cd = skillCdAt(c, slot);
            if (cd > 0) return new SkillStateResult { ok = false, reason = $"Bereit in {U.Fmt(Math.Ceiling(cd))} s", skill = skill };
            if (c.mana < skill.mana) return new SkillStateResult { ok = false, reason = "Zu wenig Mana", skill = skill };
            if (he.mining != null) return new SkillStateResult { ok = false, reason = $"Baut gerade ab ({U.Fmt(Math.Ceiling(he.mining.t))} s)", skill = skill };
            return new SkillStateResult { ok = true, reason = null, skill = skill };
        }

        /// <summary>TS <c>c.skillCd[slot]</c>; fehlender Eintrag = undefined (NaN in Vergleichen → false).</summary>
        private static double skillCdAt(ChampionState c, double slot)
        {
            int i = (int)slot;
            return i >= 0 && i < c.skillCd.Count ? c.skillCd[i] : double.NaN;
        }

        public static CommandResult useChampionSkill(GameSimulation sim, double slot, double tx, double ty)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            ChampionState c = he.champion;
            SkillStateResult chk = skillState(sim, slot);
            if (!chk.ok || chk.skill == null) return CommandResult.Fail(chk.reason ?? "Nicht bereit");
            ChampionSkill sk = chk.skill;
            ChampionStats st = championStats(he);
            SkillMods sm = skillModsOf(c);
            if (sk.target != "self" && dist2(tx, ty, c.x, c.y) > pow2(sk.castRange + 0.5)) return CommandResult.Fail("Ziel außer Reichweite");
            var def = Champions.CHAMPIONS[c.id];
            double sp = st.supportPower;
            double ap = st.abilityPower;
            double durMul = 1 + sm.buffDuration;
            double radMul = 1 + sm.buffRadius;
            Func<double, double, double, List<TowerInstance>> towersIn = (x, y, rr) => sim.s.towers.Where((t) => Towers.TOWERS[t.def].attack != "economy" && Towers.TOWERS[t.def].attack != "none" && dist2(t.cx + 0.5, t.cy + 0.5, x, y) <= rr * rr).ToList();
            Func<double, double, double, List<EnemyInstance>> enemiesIn = (x, y, rr) => sim.s.enemies.Where((e) => e.alive && !e.stealthed && !e.burrowed && dist2(e.x, e.y, x, y) <= rr * rr).ToList();
            double r = sk.radius;
            if (sk.effects != null)
            {
                SkillEffectResult res = applySkillEffects(sim, he, sk, tx, ty, st);
                if (!res.ok) return CommandResult.Fail(res.reason);
                sim.emit(new GameEvent { k = "champSkill", skill = sk.id, x = c.x, y = c.y, tx = res.tx, ty = res.ty, r = U.Or(sk.radius, 0.8), color = def.accent });
                return finishSkill(sim, he, slot, sk, st, null);
            }
            switch (sk.id)
            {
                case "inspire":
                {
                    TowerInstance t = sim.towerAt(Math.Floor(tx), Math.Floor(ty));
                    if (t == null || Towers.TOWERS[t.def].attack == "economy" || Towers.TOWERS[t.def].attack == "none") return CommandResult.Fail("Ziel muss ein Kampfturm sein");
                    applyTowerBuff(sim, t.uid, "inspire", new TowerBuffValues { dmg = 0.5 * sp, speed = 0.3 * sp }, 8 * durMul);
                    List<TowerInstance> extraAll = towersIn(t.cx + 0.5, t.cy + 0.5, 2.5).Where((o) => o != t).ToList();
                    U.StableSort(extraAll, (a, b) => U.Or(dist2(a.cx, a.cy, t.cx, t.cy) - dist2(b.cx, b.cy, t.cx, t.cy), a.uid - b.uid));
                    List<TowerInstance> extra = U.Slice(extraAll, 0, U.Round(sm.extraTargets));
                    foreach (TowerInstance o in extra) applyTowerBuff(sim, o.uid, "inspire", new TowerBuffValues { dmg = 0.25 * sp, speed = 0.15 * sp }, 8 * durMul);
                    tx = t.cx + 0.5;
                    ty = t.cy + 0.5;
                    break;
                }
                case "field":
                {
                    r = sk.radius * radMul;
                    List<TowerInstance> ts = towersIn(tx, ty, r);
                    HashSet<string> types = new HashSet<string>(ts.Select((t) => Towers.TOWERS[t.def].damageType).Where((d) => U.Truthy(d)));
                    double syn = sm.synergy * types.Count;
                    foreach (TowerInstance t in ts) applyTowerBuff(sim, t.uid, "field", new TowerBuffValues { speed = 0.35 * sp, range = 0.5, dmg = syn * sp }, 10 * durMul);
                    break;
                }
                case "pulse":
                {
                    r = sk.radius * radMul;
                    foreach (TowerInstance t in towersIn(c.x, c.y, r))
                    {
                        t.cooldown = 0;
                        applyTowerBuff(sim, t.uid, "pulse", new TowerBuffValues { crit = 0.15 * sp }, 6 * durMul);
                    }
                    tx = c.x;
                    ty = c.y;
                    break;
                }
                case "citadel":
                {
                    r = sk.radius * radMul;
                    foreach (TowerInstance t in towersIn(tx, ty, r)) applyTowerBuff(sim, t.uid, "citadel", new TowerBuffValues { dmg = 0.5 * sp, speed = 0.4 * sp, range = 0.6, crit = 0.1 }, 12 * durMul);
                    break;
                }
                case "whirl":
                {
                    r = sk.radius * (1 + sm.whirlRadius);
                    foreach (EnemyInstance e in enemiesIn(c.x, c.y, r)) hitEnemy(sim, he, e, (60 + 1.5 * st.damage) * ap, false);
                    tx = c.x;
                    ty = c.y;
                    break;
                }
                case "charge":
                {
                    double tcx = Math.Floor(tx);
                    double tcy = Math.Floor(ty);
                    if (!championWalkable(sim, tcx, tcy)) return CommandResult.Fail("Ziel ist nicht begehbar");
                    double x0 = c.x;
                    double y0 = c.y;
                    double len = U.Or(U.Hypot(tx - x0, ty - y0), 1);
                    double ux = (tx - x0) / len;
                    double uy = (ty - y0) / len;
                    // Index-Schleife: Treffer können Gegner hinzufügen (JS iteriert neue Elemente mit)
                    for (int i = 0; i < sim.s.enemies.Count; i++)
                    {
                        EnemyInstance e = sim.s.enemies[i];
                        if (!e.alive || e.stealthed || e.burrowed || e.flying) continue;
                        double rx = e.x - x0;
                        double ry = e.y - y0;
                        double along = rx * ux + ry * uy;
                        if (along < -0.3 || along > len + 0.3) continue;
                        if (Math.Abs(rx * uy - ry * ux) > 0.8) continue;
                        hitEnemy(sim, he, e, (80 + 1.7 * st.damage) * ap, false);
                        if (e.alive && !U.Truthy(e.boss)) ElementSystem.addStatus(e, "stun", 1.5 + sm.dashStun, 1, CHAMPION_UID);
                    }
                    c.x = tcx + 0.5;
                    c.y = tcy + 0.5;
                    c.hx = c.x;
                    c.hy = c.y;
                    c.path = new List<double>();
                    c.facing = Math.Atan2(uy, ux);
                    sim.emit(new GameEvent { k = "champSkill", skill = sk.id, x = x0, y = y0, tx = c.x, ty = c.y, r = 0.8, color = def.accent });
                    return finishSkill(sim, he, slot, sk, st, null);
                }
                case "warcry":
                {
                    c.buffs = c.buffs.Where((b) => b.src != "warcry").ToList();
                    c.buffs.Add(new ChampionBuff { src = "warcry", t = 6, dur = 6, add = new Dictionary<string, double> { { "attackSpeed", st.attackSpeed * 0.4 }, { "armor", 0.2 } } });
                    c.hp = U.Min(st.maxHp, c.hp + st.maxHp * 0.15);
                    // Herausforderung: Bodengegner in der Nähe werden gefesselt
                    foreach (EnemyInstance e in enemiesIn(c.x, c.y, 3 * radMul)) if (!U.Truthy(e.boss) && !e.flying) ElementSystem.addStatus(e, "root", 2, 1, CHAMPION_UID);
                    tx = c.x;
                    ty = c.y;
                    break;
                }
                case "bladestorm":
                    c.channel = 5;
                    c.channelTick = 0;
                    tx = c.x;
                    ty = c.y;
                    break;
                case "pierce":
                {
                    double len0 = U.Or(U.Hypot(tx - c.x, ty - c.y), 1);
                    double ux = (tx - c.x) / len0;
                    double uy = (ty - c.y) / len0;
                    double reach = sk.castRange + 1;
                    double width = 0.45 + sm.pierceWidth;
                    for (int i = 0; i < sim.s.enemies.Count; i++)
                    {
                        EnemyInstance e = sim.s.enemies[i];
                        if (!e.alive || e.stealthed || e.burrowed) continue;
                        double rx = e.x - c.x;
                        double ry = e.y - c.y;
                        double along = rx * ux + ry * uy;
                        if (along < 0 || along > reach) continue;
                        if (Math.Abs(rx * uy - ry * ux) > width) continue;
                        hitEnemy(sim, he, e, (50 + 1.4 * st.damage) * ap, true);
                    }
                    tx = c.x + ux * reach;
                    ty = c.y + uy * reach;
                    c.facing = Math.Atan2(uy, ux);
                    break;
                }
                case "trap":
                    he.zones.Add(new ChampionZone { uid = sim.uid(), kind = "snare", x = tx, y = ty, r = sk.radius, t = 6, tick = 0, power = (0.35 + sm.trapSlow) });
                    break;
                case "focus":
                    c.buffs = c.buffs.Where((b) => b.src != "focus").ToList();
                    c.buffs.Add(new ChampionBuff { src = "focus", t = 8, dur = 8, add = new Dictionary<string, double> { { "critChance", 0.35 }, { "attackSpeed", st.attackSpeed * 0.2 } } });
                    tx = c.x;
                    ty = c.y;
                    break;
                case "volley":
                    he.zones.Add(new ChampionZone { uid = sim.uid(), kind = "volley", x = tx, y = ty, r = sk.radius, t = 0.5 * (3 + sm.volleyPulses) - 0.01, tick = 0, power = (30 + 0.8 * st.damage) * ap });
                    break;
                default:
                    return CommandResult.Fail("Unbekannte Fähigkeit");
            }
            sim.emit(new GameEvent { k = "champSkill", skill = sk.id, x = c.x, y = c.y, tx = tx, ty = ty, r = r, color = def.accent });
            return finishSkill(sim, he, slot, sk, st, null);
        }

        /// <summary>TS: Rückgabe von applySkillEffects <c>{ ok: true; tx; ty } | { ok: false; reason }</c>.</summary>
        private class SkillEffectResult
        {
            public bool ok;
            public double tx;
            public double ty;
            public string reason;
        }

        /// <summary>Datengetriebene Fähigkeiten (Kampagnenhelden). Prüft zuerst alle Bedingungen, wirkt dann.</summary>
        private static SkillEffectResult applySkillEffects(GameSimulation sim, HeroEndlessState he, ChampionSkill sk, double tx, double ty, ChampionStats st)
        {
            ChampionState c = he.champion;
            List<SkillEffect> effects = sk.effects ?? new List<SkillEffect>();   // PORT: effects als List<SkillEffect> angenommen
            double ap = st.abilityPower;
            double sp = st.supportPower;
            SkillMods sm = skillModsOf(c);
            double durMul = 1 + sm.buffDuration;
            double radMul = 1 + sm.buffRadius;
            Func<string, bool> isCombatTower = (d) => Towers.TOWERS[d].attack != "economy" && Towers.TOWERS[d].attack != "none";
            if (sk.target == "tower")
            {
                TowerInstance t = sim.towerAt(Math.Floor(tx), Math.Floor(ty));
                if (t == null || !isCombatTower(t.def)) return new SkillEffectResult { ok = false, reason = "Ziel muss ein Kampfturm sein" };
                tx = t.cx + 0.5;
                ty = t.cy + 0.5;
            }
            if (effects.Any((e) => e.kind == "blink") && !championWalkable(sim, Math.Floor(tx), Math.Floor(ty))) return new SkillEffectResult { ok = false, reason = "Ziel ist nicht begehbar" };
            // center(at): Zielpunkt oder Held (liest tx/ty zum Aufrufzeitpunkt, wie die TS-Closure)
            Func<string, double[]> center = (at) => (at == "target" ? new double[] { tx, ty } : new double[] { c.x, c.y });
            Func<double, double, double, bool, List<EnemyInstance>> foes = (x, y, rad, air) => sim.s.enemies.Where((e) => e.alive && !e.stealthed && !e.burrowed && (air || !e.flying) && (rad <= 0 || dist2(e.x, e.y, x, y) <= rad * rad)).ToList();
            Action<EnemyInstance, StatusSpec> status = (e, s) =>
            {
                if (s == null || !e.alive) return;
                if ((s.id == "stun" || s.id == "root") && (U.Truthy(e.boss) || (s.id == "root" && e.flying))) return;
                ElementSystem.addStatus(e, s.id, s.dur, Nd(s.power, 1), CHAMPION_UID, Nd(s.stacks, 1));
            };
            foreach (SkillEffect ef in effects)
            {
                switch (ef.kind)
                {
                    case "blink":
                        c.x = Math.Floor(tx) + 0.5;
                        c.y = Math.Floor(ty) + 0.5;
                        c.hx = c.x;
                        c.hy = c.y;
                        c.path = new List<double>();
                        break;
                    case "nova":
                    {
                        double[] xy = center(ef.at);
                        double x = xy[0], y = xy[1];
                        foreach (EnemyInstance e in foes(x, y, N(ef.radius) * (1 + sm.whirlRadius), NotFalse(ef.air)))
                        {
                            hitEnemy(sim, he, e, (N(ef.@base) + N(ef.scale) * st.damage) * ap, true);
                            status(e, ef.status);
                        }
                        break;
                    }
                    case "line":
                    {
                        double len0 = U.Or(U.Hypot(tx - c.x, ty - c.y), 1);
                        double ux = (tx - c.x) / len0;
                        double uy = (ty - c.y) / len0;
                        foreach (EnemyInstance e in foes(c.x, c.y, 0, true))
                        {
                            double rx = e.x - c.x;
                            double ry = e.y - c.y;
                            double along = rx * ux + ry * uy;
                            if (along < 0 || along > N(ef.length) || Math.Abs(rx * uy - ry * ux) > N(ef.width) + sm.pierceWidth) continue;
                            hitEnemy(sim, he, e, (N(ef.@base) + N(ef.scale) * st.damage) * ap, true);
                            status(e, ef.status);
                        }
                        tx = c.x + ux * N(ef.length);
                        ty = c.y + uy * N(ef.length);
                        c.facing = Math.Atan2(uy, ux);
                        break;
                    }
                    case "execute":
                    {
                        List<EnemyInstance> all = foes(c.x, c.y, 0, true);
                        U.StableSort(all, (a, b) => a.remaining - b.remaining);
                        List<EnemyInstance> list = U.Slice(all, 0, N(ef.count));
                        // PORT: 'execute'.pct als Zahlfeld pct angenommen.
                        foreach (EnemyInstance e in list) hitEnemy(sim, he, e, e.maxHp * (U.Truthy(e.boss) ? N(ef.bossPct) : N(ef.pct)) * ap, false);
                        break;
                    }
                    case "towers":
                    {
                        double[] xy = center(ef.at);
                        double x = xy[0], y = xy[1];
                        double rad = N(ef.radius) * radMul;
                        foreach (TowerInstance t in sim.s.towers)
                        {
                            if (dist2(t.cx + 0.5, t.cy + 0.5, x, y) > rad * rad) continue;
                            if (U.Truthy(ef.repair))
                            {
                                t.stunned = 0;
                                t.disrupted = 0;
                            }
                            if (!isCombatTower(t.def)) continue;
                            applyTowerBuff(sim, t.uid, ef.src, new TowerBuffValues { dmg = N(ef.dmg) * sp, speed = N(ef.speed) * sp, range = N(ef.range), crit = N(ef.crit) }, N(ef.dur) * durMul);
                        }
                        break;
                    }
                    case "allTowers":
                        foreach (TowerInstance t in sim.s.towers) if (isCombatTower(t.def)) applyTowerBuff(sim, t.uid, ef.src, new TowerBuffValues { dmg = N(ef.dmg) * sp, speed = N(ef.speed) * sp, range = N(ef.range), crit = N(ef.crit) }, N(ef.dur) * durMul);
                        break;
                    case "self":
                    {
                        if (N(ef.dur) > 0)
                        {
                            Dictionary<string, double> add = ef.add != null ? new Dictionary<string, double>(ef.add) : new Dictionary<string, double>();
                            // PORT: 'self'.pct (Partial<Record<keyof ChampionStats, number>>) als Feld pctStats angenommen.
                            if (ef.pctStats != null) foreach (KeyValuePair<string, double> kv in ef.pctStats) add[kv.Key] = U.Get(add, kv.Key, 0) + st[kv.Key] * kv.Value;
                            string src = ef.src;
                            c.buffs = c.buffs.Where((b) => b.src != src).ToList();
                            c.buffs.Add(new ChampionBuff { src = ef.src, t = N(ef.dur), dur = N(ef.dur), add = add });
                        }
                        if (U.Truthy(ef.heal)) c.hp = U.Min(st.maxHp, c.hp + st.maxHp * N(ef.heal));
                        break;
                    }
                    case "strikes":
                    {
                        double[] xy = center(ef.at);
                        double x = xy[0], y = xy[1];
                        List<EnemyInstance> pool = foes(x, y, N(ef.radius), true);
                        for (double k = 0; k < N(ef.count) && pool.Count > 0; k++)
                        {
                            EnemyInstance e = pool[(int)(Math.Floor(sim.rand() * pool.Count) % pool.Count)];
                            hitEnemy(sim, he, e, (N(ef.@base) + N(ef.scale) * st.damage) * ap, true);
                            status(e, ef.status);
                            sim.emit(new GameEvent { k = "champSkill", skill = "strike", x = e.x, y = e.y, tx = e.x, ty = e.y, r = 0.6, color = Champions.CHAMPIONS[c.id].accent });
                        }
                        break;
                    }
                    case "statusAll":
                    {
                        double[] xy = center(ef.at);
                        double x = xy[0], y = xy[1];
                        for (int i = 0; i < sim.s.enemies.Count; i++)
                        {
                            EnemyInstance e = sim.s.enemies[i];
                            if (!e.alive) continue;
                            if (N(ef.radius) > 0 && dist2(e.x, e.y, x, y) > pow2(N(ef.radius) * radMul)) continue;
                            if (ef.status.id != "reveal" && (e.stealthed || e.burrowed)) continue;
                            status(e, ef.status);
                        }
                        break;
                    }
                    case "taunt":
                        foreach (EnemyInstance e in foes(c.x, c.y, N(ef.radius) * radMul, false)) status(e, new StatusSpec { id = "root", dur = N(ef.dur) });
                        break;
                    case "interrupt":
                        for (int i = 0; i < sim.s.enemies.Count; i++)
                        {
                            EnemyInstance e = sim.s.enemies[i];
                            if (e.alive && dist2(e.x, e.y, c.x, c.y) <= N(ef.radius) * N(ef.radius)) EnemySystem.interruptEnemy(sim, e, N(ef.power) * ap);
                        }
                        break;
                    case "gold":
                    {
                        double g = U.Round((N(ef.@base) + N(ef.perWave) * U.Max(1, sim.s.wave)) * (1 + st.lootBonus * 0.5));
                        sim.s.gold += g;
                        sim.s.stats.goldEarned += g;
                        if (U.Truthy(ef.resource) && U.Truthy(ef.amount)) LootSystem.addResource(he, ef.resource, N(ef.amount));
                        sim.emit(new GameEvent { k = "pickup", kind = "resource", x = c.x, y = c.y, label = $"+{U.Fmt(g)} Gold{(U.Truthy(ef.resource) && U.Truthy(ef.amount) ? $" · +{U.Fmt(N(ef.amount))}" : "")}", color = 0xffd84a });
                        break;
                    }
                    case "zone":
                        he.zones.Add(new ChampionZone { uid = sim.uid(), kind = ef.zone, x = tx, y = ty, r = N(ef.radius), t = N(ef.dur), tick = 0, power = N(ef.power) + N(ef.scale) * st.damage });
                        break;
                    default:
                        break;
                }
            }
            return new SkillEffectResult { ok = true, tx = tx, ty = ty };
        }

        private static CommandResult finishSkill(GameSimulation sim, HeroEndlessState he, double slot, ChampionSkill sk, ChampionStats st, object _x)
        {
            ChampionState c = he.champion;
            c.mana -= sk.mana;
            // PORT: JS würde das Array bei fehlendem Index verlängern; hier mit 0 auffüllen.
            int i = (int)slot;
            while (c.skillCd.Count <= i) c.skillCd.Add(0);
            c.skillCd[i] = sk.cooldown * (1 - st.cdr);
            c.anim = 0.4;
            sim.c.champBuffSig = "";
            return CommandResult.Ok();
        }

        /// <summary>Heldenschaden an einem Gegner (Krit, Lebensraub).</summary>
        private static double hitEnemy(GameSimulation sim, HeroEndlessState he, EnemyInstance e, double amount, bool canCrit)
        {
            ChampionState c = he.champion;
            ChampionStats st = championStats(he);
            double dmg = amount;
            if (canCrit && sim.rand() < st.critChance) dmg *= st.critDamage;
            double dealt = DamageSystem.dealDamage(sim, e, dmg, Champions.CHAMPIONS[c.id].damageType, CHAMPION_UID);
            c.damageDealt += dealt;
            if (st.lifesteal > 0) c.hp = U.Min(st.maxHp, c.hp + dealt * st.lifesteal);
            if (!e.alive) c.kills++;
            return dealt;
        }

        // ------------------------------------------------------------ Takt

        public static void updateChampion(GameSimulation sim, double dt)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            // Eigene Blockaden jeden Takt neu vergeben (die Lane-Helden setzen ihre selbst zurück)
            foreach (EnemyInstance e in sim.s.enemies) if (e.blockedBy == CHAMPION_UID) e.blockedBy = 0;
            GameState s = sim.s;
            ChampionState c = he.champion;
            updateTowerBuffs(sim, dt);
            LootSystem.updatePickups(sim, dt);
            updateChampionZones(sim, dt);
            for (int i = 0; i < c.skillCd.Count; i++) c.skillCd[i] = U.Max(0, c.skillCd[i] - dt);
            foreach (string k in U.Keys(c.potionCd)) c.potionCd[k] = U.Max(0, U.Get(c.potionCd, k, 0) - dt);
            if (c.buffs.Count > 0)
            {
                foreach (ChampionBuff b in c.buffs) b.t -= dt;
                c.buffs = c.buffs.Where((b) => b.t > 0).ToList();
            }
            c.anim = U.Max(0, c.anim - dt);
            if (c.dead > 0)
            {
                c.dead -= dt;
                if (c.dead <= 0)
                {
                    c.dead = 0;
                    ChampionStats st0 = championStats(he);
                    c.hp = st0.maxHp;
                    c.mana = st0.maxMana;
                    sim.emit(new GameEvent { k = "champRespawn", x = c.x, y = c.y });
                    sim.c.champBuffSig = "";
                }
                return;
            }
            ChampionStats st = championStats(he);
            c.hp = U.Min(st.maxHp, c.hp + st.hpRegen * dt);
            if (he.mining != null)
            {
                // Abbau: Held steht still, greift nicht an und kann nicht fallen (nicht unterbrechbar)
                c.mana = U.Min(st.maxMana, c.mana + st.manaRegen * dt);
                c.path = new List<double>();
                LootSystem.collectPickups(sim, st.pickupRadius);
                return;
            }
            c.mana = U.Min(st.maxMana, c.mana + st.manaRegen * dt);
            var def = Champions.CHAMPIONS[c.id];

            // Kanalisierter Klingensturm
            double speed = st.moveSpeed;
            if (c.channel > 0)
            {
                c.channel -= dt;
                c.channelTick -= dt;
                speed *= 0.7;
                if (c.channelTick <= 0)
                {
                    c.channelTick = 0.5;
                    for (int i = 0; i < s.enemies.Count; i++)
                    {
                        EnemyInstance e = s.enemies[i];
                        if (e.alive && !e.stealthed && !e.burrowed && dist2(e.x, e.y, c.x, c.y) <= 4)
                        {
                            hitEnemy(sim, he, e, (35 + 0.8 * st.damage) * st.abilityPower, false);
                            if (e.alive) ElementSystem.addStatus(e, "chill", 1, 0.15, CHAMPION_UID, 3);
                        }
                    }
                    sim.emit(new GameEvent { k = "champSkill", skill = "bladestormTick", x = c.x, y = c.y, tx = c.x, ty = c.y, r = 2, color = def.accent });
                }
            }

            // Blocker (Brann, Kaela …): hält Bodengegner in Nahkampfreichweite fest, solange er steht
            if (U.Truthy(def.blockCount) && c.path.Count == 0 && c.dead <= 0)
            {
                List<EnemyInstance> nearAll = s.enemies
                    .Where((e) => e.alive && !U.Truthy(e.boss) && !e.flying && !e.burrowed && e.blockedBy == 0 && dist2(e.x, e.y, c.x, c.y) <= 0.85 * 0.85)
                    .ToList();
                U.StableSort(nearAll, (a, b) => dist2(a.x, a.y, c.x, c.y) - dist2(b.x, b.y, c.x, c.y));
                List<EnemyInstance> near = U.Slice(nearAll, 0, N(def.blockCount));
                foreach (EnemyInstance e in near) e.blockedBy = CHAMPION_UID;
            }

            // Bewegung
            if (c.path.Count > 0)
            {
                moveAlong(sim, c, speed, dt);
            }
            else if (def.melee && c.holdPos != true)
            {
                // Nahkämpfer verfolgen Gegner nahe ihres Haltepunkts und kehren danach zurück.
                EnemyInstance tgt = U.Truthy(c.target) ? U.Get(sim.c.enemyByUid, c.target) : null;
                double gx = c.hx;
                double gy = c.hy;
                if (tgt != null && tgt.alive && !tgt.flying && dist2(tgt.x, tgt.y, c.hx, c.hy) <= 2.5 * 2.5 && dist2(tgt.x, tgt.y, c.x, c.y) > pow2(st.range * 0.85))
                {
                    gx = tgt.x;
                    gy = tgt.y;
                }
                double d = U.Hypot(gx - c.x, gy - c.y);
                if (d > 0.05 && !(tgt != null && dist2(tgt.x, tgt.y, c.x, c.y) <= pow2(st.range * 0.85)))
                {
                    double step = U.Min(d, speed * dt);
                    double nx = c.x + ((gx - c.x) / d) * step;
                    double ny = c.y + ((gy - c.y) / d) * step;
                    if (championWalkable(sim, Math.Floor(nx), Math.Floor(ny)))
                    {
                        c.x = nx;
                        c.y = ny;
                        c.facing = Math.Atan2(gy - c.y, gx - c.x);
                    }
                }
            }

            // Ziel wählen und angreifen (nicht während des Laufens über einen Befehl)
            c.attackCd = U.Max(0, c.attackCd - dt);
            if (c.path.Count == 0)
            {
                double reach = def.melee ? U.Max(st.range, 2.5) : st.range;
                EnemyInstance cur = U.Truthy(c.target) ? U.Get(sim.c.enemyByUid, c.target) : null;
                if (cur == null || !cur.alive || cur.stealthed || cur.burrowed || dist2(cur.x, cur.y, c.x, c.y) > pow2(reach + 0.4) || (def.melee && cur.flying))
                {
                    cur = null;
                    double bd = reach * reach;
                    foreach (EnemyInstance e in s.enemies)
                    {
                        if (!e.alive || e.stealthed || e.burrowed || (def.melee && e.flying)) continue;
                        double d = dist2(e.x, e.y, c.x, c.y);
                        if (d < bd)
                        {
                            bd = d;
                            cur = e;
                        }
                    }
                    c.target = cur != null ? cur.uid : 0;
                }
                if (cur != null && c.attackCd <= 0 && dist2(cur.x, cur.y, c.x, c.y) <= pow2(st.range + 0.3))
                {
                    c.attackCd = 1 / st.attackSpeed;
                    c.facing = Math.Atan2(cur.y - c.y, cur.x - c.x);
                    c.anim = 0.3;
                    double tx = cur.x;
                    double ty = cur.y;
                    hitEnemy(sim, he, cur, st.damage, true);
                    if (st.cleave > 0)
                    {
                        for (int i = 0; i < s.enemies.Count; i++)
                        {
                            EnemyInstance e = s.enemies[i];
                            if (e == cur || !e.alive || e.flying || e.stealthed || dist2(e.x, e.y, tx, ty) > 1) continue;
                            hitEnemy(sim, he, e, st.damage * st.cleave, false);
                        }
                    }
                    sim.emit(new GameEvent { k = "champAttack", x = c.x, y = c.y, tx = tx, ty = ty, melee = def.melee, color = def.accent });
                }
            }

            // Gegner verletzen den Helden bei Kontakt
            if (s.phase == "combat")
            {
                double scale = 1 + HeroEndless.HERO_ENDLESS.enemyDamagePerWave * s.wave;
                double cr2 = pow2(HeroEndless.HERO_ENDLESS.contactRange);
                double dmg = 0;
                foreach (EnemyInstance e in s.enemies)
                {
                    if (!e.alive || e.flying || e.burrowed) continue;
                    if (dist2(e.x, e.y, c.x, c.y) > cr2 * (U.Truthy(e.boss) ? 2.5 : 1)) continue;
                    dmg += EnemySystem.enemyContactDps(e) * (e.miniBoss == true ? 2 : 1) * scale * dt;
                }
                if (dmg > 0) c.hp -= dmg * (1 - st.armor);
            }
            if (c.hp <= 0)
            {
                c.hp = 0;
                c.dead = (HeroEndless.HERO_ENDLESS.respawnBase + HeroEndless.HERO_ENDLESS.respawnPerLevel * c.level) * U.Max(0.5, 1 - PassiveTree.runPassive(he).respawnPct);
                c.path = new List<double>();
                c.channel = 0;
                c.target = 0;
                c.buffs = new List<ChampionBuff>();
                sim.emit(new GameEvent { k = "champDown", x = c.x, y = c.y });
                sim.emit(new GameEvent { k = "message", text = $"{def.name} ist gefallen – Wiederbelebung am Kern in {U.Fmt(Math.Ceiling(c.dead))} s.", severity = "warn" });
                Vec2 home = championHome(s.map, he.shop);
                c.x = home.x;
                c.y = home.y;
                c.hx = c.x;
                c.hy = c.y;
                sim.c.champBuffSig = "";
                return;
            }
            LootSystem.collectPickups(sim, st.pickupRadius);
        }

        private static void updateChampionZones(GameSimulation sim, double dt)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null || he.zones.Count == 0) return;
            for (int zi = 0; zi < he.zones.Count; zi++)
            {
                ChampionZone z = he.zones[zi];
                z.t -= dt;
                z.tick -= dt;
                if (z.kind == "snare")
                {
                    for (int i = 0; i < sim.s.enemies.Count; i++)
                    {
                        EnemyInstance e = sim.s.enemies[i];
                        if (!e.alive || e.flying || dist2(e.x, e.y, z.x, z.y) > z.r * z.r) continue;
                        ElementSystem.addStatus(e, "chill", 0.4, U.Min(0.7, z.power) / 2, CHAMPION_UID, 2);
                        if (z.tick <= 0) DamageSystem.dealDamage(sim, e, 6 + sim.s.wave * 1.2, "physical", CHAMPION_UID);
                    }
                    if (z.tick <= 0) z.tick = 0.5;
                }
                else if (z.kind == "volley" && z.tick <= 0)
                {
                    z.tick = 0.5;
                    for (int i = 0; i < sim.s.enemies.Count; i++)
                    {
                        EnemyInstance e = sim.s.enemies[i];
                        if (!e.alive || e.stealthed || e.burrowed || dist2(e.x, e.y, z.x, z.y) > z.r * z.r) continue;
                        double dealt = DamageSystem.dealDamage(sim, e, z.power, "physical", CHAMPION_UID);
                        he.champion.damageDealt += dealt;
                    }
                    sim.emit(new GameEvent { k = "champSkill", skill = "volleyTick", x = z.x, y = z.y, tx = z.x, ty = z.y, r = z.r, color = Champions.CHAMPIONS[he.champion.id].accent });
                }
            }
            he.zones = he.zones.Where((z) => z.t > 0).ToList();
        }
    }
}
