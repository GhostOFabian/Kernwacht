// Übersetzung von sim/TowerSystem.ts.
// Turmsystem: Werteberechnung (datengetrieben), Zielauswahl und Angriffsverhalten.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>TS: Rückgabeobjekt von <c>laserPenalty</c> (<c>{ mul, neighbors, nearRift }</c>).</summary>
    public class LaserPenaltyResult
    {
        public double mul;
        public double neighbors;
        public bool nearRift;
    }

    /// <summary>TS: Objektkonstante <c>LASER</c>.</summary>
    public class LaserRules
    {
        public double interferenceRange;
        public double interferencePer;
        public double interferenceMax;
        public double riftRange;
        public double riftMul;
    }

    /// <summary>TS: Objektkonstante <c>MINE</c>.</summary>
    public class MineRules
    {
        public double flight;
        public double trigger;
        public double life;
        public double spacing;
    }

    public static partial class TowerSystem
    {
        /// <summary>JS <c>x ** 2</c> (V8/fdlibm: Exponent 2 liefert exakt x*x).</summary>
        private static double Sq(double x) { return x * x; }

        /// <summary>Lesen eines Stat-Werts wie JS (fehlender Schlüssel = undefined → NaN in Rechnungen).</summary>
        private static double StatOrNaN(Dictionary<string, double> st, string key)
        {
            return st.TryGetValue(key, out var v) ? v : double.NaN;
        }

        /// <summary>TS <c>{ ...ZERO_STATS, ...def.base }</c>.</summary>
        private static Dictionary<string, double> spreadBase(TowerDefinition def)
        {
            var st = new Dictionary<string, double>(Stats.ZERO_STATS);
            foreach (var kv in def.@base) st[kv.Key] = kv.Value;
            return st;
        }

        /// <summary>TS <c>{ ...p, type }</c> (flache Kopie; stats bleibt dieselbe Referenz).</summary>
        private static HitPayload withType(HitPayload p, string type)
        {
            return new HitPayload { damage = p.damage, type = type, splash = p.splash, stats = p.stats, tower = p.tower, towerDef = p.towerDef };
        }

        /// <summary>PORT: Parameter als IEnumerable, damit List&lt;StatMod&gt; und StatMod[] gleichermaßen passen.</summary>
        public static void applyMods(Dictionary<string, double> st, IEnumerable<StatMod> mods)
        {
            foreach (var m in mods)
            {
                if (m.add != null) st[m.stat] = StatOrNaN(st, m.stat) + m.add.Value;
                if (m.mul != null) st[m.stat] = StatOrNaN(st, m.stat) * m.mul.Value;
            }
        }

        /// <summary>Berechnet die effektiven Werte eines Turms (Stufen, Spezialisierung, Resonanzen, Modifikatoren, Terrain, Netzwerk).</summary>
        public static Dictionary<string, double> computeTowerStats(GameSimulation sim, TowerInstance t, bool includeNetwork = true)
        {
            var def = Towers.TOWERS[t.def];
            var st = spreadBase(def);
            for (int i = 0; i < t.level - 1 && i < def.upgrades.Count; i++) applyMods(st, def.upgrades[i].mods);
            if (t.spec > 0) applyMods(st, def.specs[(int)t.spec - 1].mods);
            // Fusions-Erbe (legendäre Türme): halbe Elternspezialisierungen + Kombinationsbonus
            FusionHeritage.apply(st, t);
            // 3.6: Veredelung
            if (U.Truthy(t.stars)) applyMods(st, Goldsinks.starMods(t.def, t.stars.Value));
            // Runen (Helden-Endlos 2.7)
            if (sim.s.he != null && sim.s.he.runes != null && sim.s.he.runes.Count > 0) applyMods(st, RuneSystem.towerRuneMods(sim, t));
            double bonus = 0;
            if (includeNetwork)
            {
                if (sim.c.resonanceByTower.TryGetValue(t.uid, out var res) && res != null)
                {
                    foreach (var id in res)
                    {
                        var r = Elements.RESONANCES.FirstOrDefault((x) => x.id == id);
                        if (r != null) applyMods(st, r.mods);
                    }
                }
                bonus = sim.c.networkBonusByTower.TryGetValue(t.uid, out var nb) ? nb : 0;
                // Globale Puffer: Netzflut (eigenes Netz) und Lied der Resonanz (alle Netze) verdoppeln den Netzwerkbonus.
                if (bonus > 0)
                {
                    bool hasGi = sim.c.linked.TryGetValue(t.uid, out var gi);
                    bool doubled = sim.s.buffs.Any((b) => b.id == "resoSong" || (b.id == "surge" && hasGi && sim.c.linked.TryGetValue(b.src, out var gb) && gb == gi));
                    if (doubled) bonus *= 2;
                }
            }
            var mods = sim.c.mods;
            var s = sim.s;
            double idx = t.cy * s.map.width + t.cx;
            string type = def.damageType;
            // Totem-Aura (stärkstes Totem in Reichweite, kein Stapeln) und Helden-Auren
            if (def.attack != "aura")
            {
                double aDmg = 0;
                double aSpd = 0;
                foreach (var o in s.towers)
                {
                    if (o.def != "totem" || o.stunned > 0) continue;
                    var os = computeTotemStats(sim, o);
                    if (Sq(o.cx - t.cx) + Sq(o.cy - t.cy) > os["range"] * os["range"]) continue;
                    aDmg = U.Max(aDmg, os["auraDmg"]);
                    aSpd = U.Max(aSpd, os["auraSpeed"]);
                }
                if (sim.c.heroAura.TryGetValue(t.uid, out var ha) && ha != null)
                {
                    aDmg += ha.dmg;
                    aSpd += ha.speed;
                    st["range"] += ha.range;
                }
                // Helden-Endlosmodus: Turmverstärkungen des Helden (bereits gedeckelt in rebuildChampionBuffs)
                var pf = PassiveTree.runPassive(s.he);
                if (U.Truthy(pf.towerDmgPct) || U.Truthy(pf.towerSpeedPct) || U.Truthy(pf.towerRange))
                {
                    aDmg += pf.towerDmgPct;
                    aSpd += pf.towerSpeedPct;
                    st["range"] += pf.towerRange;
                }
                // Turm-Meisterschaft: +1 % Schaden je Rang dieses Turmtyps
                double mr = Mastery.masteryRank((s.he?.mastery != null ? U.GetN(s.he.mastery, t.def) : null) ?? 0);
                if (mr > 0) aDmg += Mastery.MASTERY.perRank * mr;
                // Paragon-Turmbaum
                if (U.Truthy(pf.towerCrit)) st["critChance"] += pf.towerCrit;
                if (U.Truthy(pf.towerPierce)) st["armorPierce"] = U.Min(0.9, st["armorPierce"] + pf.towerPierce);
                if (U.Truthy(pf.towerSplashPct) && st["splash"] > 0) st["splash"] *= 1 + pf.towerSplashPct;
                // PORT: Werttyp von SimCache.champBuff (TS inline { dmg, speed, range, crit }) unbekannt → var.
                if (sim.c.champBuff.TryGetValue(t.uid, out var cb) && cb != null)
                {
                    aDmg += cb.dmg;
                    aSpd += cb.speed;
                    st["range"] += cb.range;
                    st["critChance"] += cb.crit;
                }
                st["damageMul"] *= 1 + aDmg;
                st["attackSpeedMul"] *= 1 + aSpd;
                if (t.forged > 0)
                {
                    st["damageMul"] *= 1.35;
                    st["range"] += 0.5;
                }
                if (t.activeT > 0) applyActiveBuff(st, def.active);
                foreach (var b in s.buffs)
                {
                    if (b.id == "hymn") st["attackSpeedMul"] *= 1 + b.power;
                    if (b.id == "worldweb") st["damageMul"] *= 1 + b.power;
                }
            }
            double dmgMul = st["damageMul"] * mods.allDamage * (U.Truthy(type) ? mods.dmgType[type] : 1);
            // 3.3 Fokusbonus: wenige Türme sind stärker (nur Endlos, nur kämpfende Türme)
            if (s.rules.endless && def.attack != "economy") dmgMul *= 1 + Scaling.focusBonus(EconomySystem.countedTowers(sim), EconomySystem.currentTowerLimit(sim));
            if (s.map.ley.Contains(idx) || s.zones.Any((z) => z.kind == "leyNode" && z.armed <= 0 && z.t > 0 && Sq(z.x - t.cx - 0.5) + Sq(z.y - t.cy - 0.5) <= z.r * z.r)) dmgMul *= Balance.ECONOMY.leyDamageMul;
            var ev = s.phase == "combat" && U.Truthy(s.currentWave?.@event) ? Biomes.TERRAIN_EVENTS[s.currentWave.@event] : null;
            if (ev != null && U.Truthy(type)) dmgMul *= U.GetN(ev.damageMul, type) ?? 1;
            if (t.def != "pylon") dmgMul *= 1 + bonus;
            if (def.attack == "laser") dmgMul *= laserPenalty(sim, t).mul;
            st["damage"] *= dmgMul;
            st["burnDps"] *= dmgMul * (type == "fire" ? 1 : mods.dmgType["fire"]);
            st["poisonDps"] *= dmgMul;
            st["bleedDps"] *= dmgMul;
            st["zapDamage"] *= mods.allDamage * mods.dmgType["lightning"];
            if (st["range"] > 0)
            {
                double hg = s.map.heights[(int)idx] >= 2 ? Balance.ECONOMY.highgroundRangeAdd : 0;
                double evMul = s.phase == "combat" && U.Truthy(s.currentWave?.@event) ? (U.GetN(Biomes.EVENT_RANGE_MUL, s.currentWave.@event) ?? 1) : 1;
                st["range"] = (st["range"] + hg) * st["rangeMul"] * mods.rangeMul * (def.attack == "aura" ? 1 : evMul);
            }
            st["cooldown"] = st["cooldown"] / U.Max(0.1, st["attackSpeedMul"] * mods.attackSpeedMul * (t.def != "pylon" ? 1 + bonus : 1));
            if (type == "lightning" && st["damage"] > 0) st["chains"] += mods.chainsAdd;
            st["chillDur"] *= mods.chillDurMul;
            st["freezeDur"] *= mods.chillDurMul;
            st["poisonMaxStacks"] += mods.poisonStacksAdd;
            st["poisonDur"] *= mods.poisonDurMul;
            if (st["markPct"] > 0) st["markPct"] += mods.markAdd;
            if (st["income"] > 0)
            {
                st["income"] *= mods.incomeMul * (1 + PassiveTree.runPassive(s.he).mineIncomePct);
                if (Towers.footprintCells(t.def, t.cx, t.cy, s.map.width).Any((i) => s.map.veins.Contains(i))) st["income"] *= s.rules.veinMul;
            }
            return st;
        }

        private static void applyActiveBuff(Dictionary<string, double> st, string id)
        {
            if (id == "overdrive") st["attackSpeedMul"] *= 2;
            else if (id == "overload")
            {
                st["chains"] += 4;
                st["damageMul"] *= 1.5;
            }
        }

        /// <summary>Totemwerte ohne Auren (verhindert Rekursion).</summary>
        private static Dictionary<string, double> computeTotemStats(GameSimulation sim, TowerInstance t)
        {
            if (sim.c.towerStats.TryGetValue(t.uid, out var cached) && cached != null && t.def == "totem") return cached;
            var def = Towers.TOWERS[t.def];
            var st = spreadBase(def);
            for (int i = 0; i < t.level - 1 && i < def.upgrades.Count; i++) applyMods(st, def.upgrades[i].mods);
            if (t.spec > 0) applyMods(st, def.specs[(int)t.spec - 1].mods);
            // Fusions-Erbe (legendäre Türme): halbe Elternspezialisierungen + Kombinationsbonus
            FusionHeritage.apply(st, t);
            return st;
        }

        public static void recomputeAllStats(GameSimulation sim)
        {
            sim.c.towerStats.Clear();
            foreach (var t in sim.s.towers) sim.c.towerStats[t.uid] = computeTowerStats(sim, t, true);
        }

        public static bool canHit(Dictionary<string, double> st, EnemyInstance e)
        {
            if (!e.alive || e.stealthed || e.burrowed) return false;
            return e.flying ? st["hitsAir"] > 0 : st["hitsGround"] > 0;
        }

        private static bool inRange(TowerInstance t, Dictionary<string, double> st, EnemyInstance e)
        {
            double dx = e.x - (t.cx + 0.5);
            double dy = e.y - (t.cy + 0.5);
            double r = st["range"] + 0.15;
            return dx * dx + dy * dy <= r * r;
        }

        private static double priorityScore(string p, TowerInstance t, EnemyInstance e)
        {
            switch (p)
            {
                case "first": return -e.traveled;
                case "exit": return e.remaining;
                case "strong": return -(e.hp + e.shield);
                case "weak": return e.hp + e.shield;
                case "close": return Sq(e.x - t.cx - 0.5) + Sq(e.y - t.cy - 0.5);
                default: return 0;
            }
        }

        private class ScoredEnemy
        {
            public EnemyInstance e;
            public double s;
        }

        /// <summary>Wählt bis zu n Ziele nach Priorität. Deterministisch (UID als Gleichstandsauflösung).</summary>
        public static List<EnemyInstance> pickTargets(GameSimulation sim, TowerInstance t, Dictionary<string, double> st, double n, Func<EnemyInstance, bool> filter = null)
        {
            var cands = new List<EnemyInstance>();
            foreach (var e in sim.s.enemies)
            {
                if (!canHit(st, e) || !inRange(t, st, e)) continue;
                if (filter != null && !filter(e)) continue;
                cands.Add(e);
            }
            if (cands.Count == 0) return cands;
            // 3.3: Drachenhorst/Phönix greifen Flieger in Reichweite zuerst an
            if (st["preferAir"] > 0 && cands.Any((x) => x.flying))
            {
                var air = cands.Where((x) => x.flying).ToList();
                cands.Clear();
                cands.AddRange(air);
            }
            if (t.priority == "random")
            {
                for (int i = cands.Count - 1; i > 0; i--)
                {
                    int j = (int)Math.Floor(sim.rand() * (i + 1));
                    var tmp = cands[i];
                    cands[i] = cands[j];
                    cands[j] = tmp;
                }
                return U.Slice(cands, 0, n);
            }
            if (n == 1)
            {
                var best = cands[0];
                double bs = priorityScore(t.priority, t, best);
                for (int i = 1; i < cands.Count; i++)
                {
                    double sc = priorityScore(t.priority, t, cands[i]);
                    if (sc < bs || (sc == bs && cands[i].uid < best.uid))
                    {
                        best = cands[i];
                        bs = sc;
                    }
                }
                return new List<EnemyInstance> { best };
            }
            var scored = cands.Select((x) => new ScoredEnemy { e = x, s = priorityScore(t.priority, t, x) }).ToList();
            U.StableSort(scored, (a, b) => U.Or(a.s - b.s, a.e.uid - b.e.uid));
            return U.Slice(scored, 0, n).Select((x) => x.e).ToList();
        }

        /// <summary>
        /// Wie pickTargets(…, 1), aber ohne Listen anzulegen (läuft für jeden Turm, sobald er kein Ziel hat –
        /// mit vielen Türmen hunderte Male pro Sekunde). Gleiches Ergebnis: Flieger zuerst bei preferAir,
        /// Zufall bei „random“, sonst beste Prioritätswertung mit UID als Gleichstandsauflösung.
        /// </summary>
        public static EnemyInstance pickBest(GameSimulation sim, TowerInstance t, Dictionary<string, double> st)
        {
            if (t.priority == "random") { var r = pickTargets(sim, t, st, 1); return r.Count > 0 ? r[0] : null; }
            bool preferAir = st["preferAir"] > 0;
            EnemyInstance best = null, bestAir = null;
            double bs = 0, bsAir = 0;
            foreach (var e in sim.s.enemies)
            {
                if (!canHit(st, e) || !inRange(t, st, e)) continue;
                double sc = priorityScore(t.priority, t, e);
                if (best == null || sc < bs || (sc == bs && e.uid < best.uid)) { best = e; bs = sc; }
                if (preferAir && e.flying && (bestAir == null || sc < bsAir || (sc == bsAir && e.uid < bestAir.uid))) { bestAir = e; bsAir = sc; }
            }
            return bestAir ?? best;
        }

        public static HitPayload towerPayload(TowerInstance t, Dictionary<string, double> st)
        {
            return payload(t, st);
        }

        private static HitPayload payload(TowerInstance t, Dictionary<string, double> st)
        {
            var def = Towers.TOWERS[t.def];
            return new HitPayload { damage = st["damage"], type = def.damageType ?? "physical", splash = st["splash"], stats = st, tower = t.uid, towerDef = t.def };
        }

        /// <summary>
        /// Turmfähigkeiten lösen ab Stufe 2 automatisch aus, sobald die Abklingzeit abgelaufen ist und
        /// sie sinnvoll wirken (Gegner in Reichweite; Goldrausch/Netzflut während jeder Welle).
        /// Manuelles Auslösen (C) bleibt möglich, verändert aber nichts an der Abklingzeit.
        /// </summary>
        public static void autoTriggerActives(GameSimulation sim)
        {
            var s = sim.s;
            if (!s.enemies.Any((e) => e.alive)) return;
            // PORT: Index-Schleife über die erfasste Liste = JS for…of.
            var towers = s.towers;
            for (int ti = 0; ti < towers.Count; ti++)
            {
                var t = towers[ti];
                var d = Towers.TOWERS[t.def];
                if (!U.Truthy(d.active) || t.activeCd > 0 || t.stunned > 0) continue;
                if (!EconomySystem.activeState(sim, t).available) continue;
                if (d.active != "goldrush" && d.active != "surge")
                {
                    var st = sim.stats(t);
                    double r = U.Max(1.5, st["range"]);
                    double x = t.cx + 0.5;
                    double y = t.cy + 0.5;
                    if (!s.enemies.Any((e) => e.alive && !e.stealthed && !e.burrowed && Sq(e.x - x) + Sq(e.y - y) <= r * r)) continue;
                }
                EconomySystem.triggerActive(sim, t.uid);
            }
        }

        /// <summary>
        /// Lane-Barrikaden halten Bodengegner auf: wer sie erreicht, bleibt stehen, bis die Barrikade insgesamt
        /// holdTime Sekunden lang Gegner aufgehalten hat – dann zerbricht sie. Bosse werden nur stark verlangsamt.
        /// t.charge zählt die verbrauchte Haltezeit.
        /// </summary>
        private static void updateLaneBarricades(GameSimulation sim, double dt)
        {
            var s = sim.s;
            List<TowerInstance> broken = null;
            foreach (var t in s.towers)
            {
                if (t.def != "lanebarricade") continue;
                var st = sim.stats(t);
                double r2 = st["range"] * st["range"];
                double cx = t.cx + 0.5;
                double cy = t.cy + 0.5;
                bool held = false;
                foreach (var e in s.enemies)
                {
                    if (!e.alive || e.flying || e.burrowed) continue;
                    if (Sq(e.x - cx) + Sq(e.y - cy) > r2) continue;
                    if (U.Truthy(e.boss))
                    {
                        var c = ElementSystem.addStatus(e, "chill", st["chillDur"], st["chillPct"], t.uid, 1);
                        c.flag = U.Max(c.flag, U.Round(st["chillPct"] * 100));
                    }
                    else ElementSystem.addStatus(e, "root", 0.2, 1, t.uid);
                    held = true;
                }
                if (!held) continue;
                t.charge += dt;
                if (t.charge >= st["holdTime"]) (broken ??= new List<TowerInstance>()).Add(t);
            }
            if (broken == null) return;
            foreach (var t in broken)
            {
                s.towers = s.towers.Where((x) => x.uid != t.uid).ToList();
                sim.emit(new GameEvent { k = "sell", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5, refund = 0 });
                sim.emit(new GameEvent { k = "message", text = "Lane-Barrikade zerbrochen", severity = "info" });
            }
            sim.reindex();
            EconomySystem.markDirty(sim);
        }

        /// <summary>Leere Liste gleichen Elementtyps (TS: <c>sim.pendingActives = []</c>).</summary>
        private static List<T> emptyLike<T>(List<T> l) { return new List<T>(); }

        public static void updateTowers(GameSimulation sim, double dt)
        {
            var s = sim.s;
            if (sim.pendingActives.Count > 0)
            {
                // PORT: Elementtyp von GameSimulation.pendingActives (TS { uid, id }) wird per Typinferenz übernommen.
                var list = sim.pendingActives;
                sim.pendingActives = emptyLike(list);
                foreach (var a in list)
                {
                    if (sim.c.towerByUid.TryGetValue(a.uid, out var pt) && pt != null) executeActive(sim, pt, a.id);
                }
            }
            updateTeslaArcs(sim, dt);
            if (s.phase == "combat" && s.tick % 3 == 0) updateLaneBarricades(sim, dt * 3);
            if (s.phase == "combat" && s.tick % 6 == 0) autoTriggerActives(sim);
            // PORT: Index-Schleife über die erfasste Liste = JS for…of (z. B. zurückgegebene verschluckte Türme werden mit durchlaufen).
            var towers = s.towers;
            for (int ti = 0; ti < towers.Count; ti++)
            {
                var t = towers[ti];
                t.fireAnim = U.Max(0, t.fireAnim - dt);
                if (t.activeCd > 0) t.activeCd = U.Max(0, t.activeCd - dt);
                if (t.activeT > 0)
                {
                    t.activeT -= dt;
                    if (t.activeT <= 0)
                    {
                        t.activeT = 0;
                        sim.c.statsDirty = true;
                    }
                }
                if (t.forged > 0)
                {
                    t.forged -= dt;
                    if (t.forged <= 0)
                    {
                        t.forged = 0;
                        sim.c.statsDirty = true;
                    }
                }
                if (t.def == "dragon" || t.def == "phoenix") t.orbit += dt * 1.7;
                if (t.disrupted > 0)
                {
                    t.disrupted -= dt;
                    if (t.disrupted <= 0)
                    {
                        t.disrupted = 0;
                        sim.c.networkDirty = true;
                    }
                }
                if (t.stunned > 0)
                {
                    t.stunned = U.Max(0, t.stunned - dt);
                    continue;
                }
                var def = Towers.TOWERS[t.def];
                if (def.attack == "economy" || def.attack == "none" || def.attack == "aura") continue;
                var st = sim.stats(t);
                if (st["damage"] <= 0 && def.attack == "pylon") continue;
                t.cooldown = U.Max(0, t.cooldown - dt);
                t.retarget -= dt;
                EnemyInstance target = null;
                if (U.Truthy(t.target)) sim.c.enemyByUid.TryGetValue(t.target, out target);
                if (target == null || !canHit(st, target) || !inRange(t, st, target) || t.retarget <= 0)
                {
                    t.retarget = Balance.SIM.retargetInterval;
                    target = pickBest(sim, t, st);
                    t.target = target != null ? target.uid : 0;
                }
                // PORT: Math.atan2 kann sich im letzten Bit von V8 (fdlibm) unterscheiden.
                if (target != null) t.aim = Math.Atan2(target.y - (t.cy + 0.5), target.x - (t.cx + 0.5));
                if (def.attack == "laser" && (target == null || target.uid != t.rampTarget))
                {
                    t.charge = 0;
                    t.rampTarget = target != null ? target.uid : 0;
                }
                if (t.cooldown > 0) continue;
                bool fired = fire(sim, t, st, target);
                if (fired)
                {
                    t.cooldown = st["cooldown"];
                    t.fireAnim = 0.18;
                    t.shots++;
                }
            }
        }

        private static bool fire(GameSimulation sim, TowerInstance t, Dictionary<string, double> st, EnemyInstance target)
        {
            var def = Towers.TOWERS[t.def];
            double ox = t.cx + 0.5;
            double oy = t.cy + 0.5;
            // PORT: Schleifen über sim.s.enemies als Index-Schleifen über die erfasste Liste (JS for…of sieht
            // während der Schleife gespawnte Gegner, z. B. aus Todesabspaltungen, mit).
            switch (def.attack)
            {
                case "mines":
                    return throwMines(sim, t, st, ox, oy);
                case "projectile":
                {
                    if (target == null) return false;
                    var targets = st["multishot"] > 1 ? pickTargets(sim, t, st, U.Round(st["multishot"])) : new List<EnemyInstance> { target };
                    foreach (var e in targets)
                    {
                        double d = U.Or(U.Hypot(e.x - ox, e.y - oy), 1);
                        sim.s.projectiles.Add(new Projectile
                        {
                            uid = sim.uid(), x = ox, y = oy, z = 0.9, tx = e.x, ty = e.y, target = e.uid, speed = U.Or(st["projectileSpeed"], 10),
                            payload = payload(t, st), kind = t.def, pierceLeft = U.Round(st["pierce"]),
                            dirX = (e.x - ox) / d, dirY = (e.y - oy) / d, traveled = 0, maxTravel = st["range"] + 1.5, hitList = new List<double>(), alive = true,
                        });
                        sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = e.x, ty = e.y, uid = t.uid });
                    }
                    return true;
                }
                case "pulse":
                {
                    var hits = pickTargets(sim, t, st, 999);
                    if (hits.Count == 0) return false;
                    var p = payload(t, st);
                    foreach (var e in hits) DamageSystem.applyHit(sim, e, p);
                    sim.emit(new GameEvent { k = "pulse", tower = t.def, x = ox, y = oy, r = st["range"] });
                    return true;
                }
                case "chain":
                case "pylon":
                {
                    if (target == null) return false;
                    var p = payload(t, st);
                    var hit = new HashSet<double>();
                    var pts = new List<double> { ox, oy };
                    EnemyInstance cur = target;
                    double mult = 1;
                    double jumps = U.Round(st["chains"]) + (ElementSystem.hasStatus(target, "wet") ? 1 : 0);
                    while (cur != null)
                    {
                        hit.Add(cur.uid);
                        pts.Add(cur.x);
                        pts.Add(cur.y);
                        double cx = cur.x;
                        double cy = cur.y;
                        DamageSystem.applyHit(sim, cur, p, mult);
                        if (jumps-- <= 0) break;
                        mult *= st["chainFalloff"];
                        EnemyInstance next = null;
                        double bd = st["chainRange"] * st["chainRange"];
                        var enemies = sim.s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!e.alive || hit.Contains(e.uid) || !canHit(st, e)) continue;
                            double d = Sq(e.x - cx) + Sq(e.y - cy);
                            if (d < bd)
                            {
                                bd = d;
                                next = e;
                            }
                        }
                        cur = next;
                    }
                    sim.emit(new GameEvent { k = "chain", pts = pts });
                    sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = target.x, ty = target.y, uid = t.uid });
                    return true;
                }
                case "beam":
                {
                    if (target == null) return false;
                    double mult = 1;
                    if (st["rampPct"] > 0)
                    {
                        if (t.rampTarget == target.uid) t.rampStacks = U.Min(st["rampMax"], t.rampStacks + 1);
                        else
                        {
                            t.rampTarget = target.uid;
                            t.rampStacks = 0;
                        }
                        mult = 1 + st["rampPct"] * t.rampStacks;
                    }
                    sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = target.x, ty = target.y, uid = t.uid });
                    DamageSystem.applyHit(sim, target, payload(t, st), mult);
                    return true;
                }
                case "rift":
                {
                    if (target == null) return false;
                    double now = sim.s.time;
                    var fresh = pickTargets(sim, t, st, U.Round(st["riftTargets"]), (x) => x.boss != null || (x.riftCd <= now && !ElementSystem.hasStatus(x, "riftMark")));
                    var targets = fresh.Count > 0 ? fresh : new List<EnemyInstance> { target };
                    var p = payload(t, st);
                    foreach (var e in targets)
                    {
                        sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = e.x, ty = e.y, uid = t.uid });
                        DamageSystem.applyHit(sim, e, p);
                        if (!e.alive) continue;
                        if (U.Truthy(e.boss))
                        {
                            ElementSystem.addStatus(e, "tether", 4, U.Max(0.15, st["tetherPct"]), t.uid);
                        }
                        else if (e.riftCd <= now && !ElementSystem.hasStatus(e, "riftMark"))
                        {
                            ElementSystem.addStatus(e, "riftMark", st["riftDelay"], st["riftBack"], t.uid);
                            e.riftCd = now + Towers.RIFT_ENEMY_COOLDOWN;
                            if (st["rootDur"] > 0) ElementSystem.addStatus(e, "root", st["rootDur"], 1, t.uid);
                            if (st["tetherPct"] > 0) ElementSystem.addStatus(e, "tether", 4, st["tetherPct"], t.uid);
                        }
                    }
                    return true;
                }
                case "meteor":
                {
                    if (target == null) return false;
                    double n = U.Max(1, U.Round(st["meteorCount"]));
                    var targets = n > 1 ? pickTargets(sim, t, st, n) : new List<EnemyInstance> { target };
                    for (int i = 0; i < targets.Count; i++)
                    {
                        var e = targets[i];
                        double r = n > 1 ? st["splash"] * 0.8 : st["splash"];
                        sim.s.zones.Add(new Zone { uid = sim.uid(), kind = "meteor", x = e.x, y = e.y, r = r, t = 0.1, armed = st["meteorDelay"] + i * 0.25, power = st["damage"], tick = 0, link = t.uid, src = t.uid });
                        if (st["fragments"] > 0)
                        {
                            for (int k = 0; k < U.Round(st["fragments"]); k++)
                            {
                                // PORT: Math.cos/Math.sin können sich im letzten Bit von V8 (fdlibm) unterscheiden.
                                double a = (k / st["fragments"]) * Math.PI * 2 + sim.rand();
                                sim.s.zones.Add(new Zone { uid = sim.uid(), kind = "meteor", x = e.x + Math.Cos(a) * 1.3, y = e.y + Math.Sin(a) * 1.3, r = 0.8, t = 0.1, armed = st["meteorDelay"] + 0.35 + k * 0.12, power = st["damage"] * 0.4, tick = 0, link = t.uid, src = t.uid });
                            }
                        }
                    }
                    sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = target.x, ty = target.y, uid = t.uid });
                    return true;
                }
                case "tesla":
                {
                    if (target == null) return false;
                    var p = payload(t, st);
                    var pts = new List<double> { ox, oy };
                    var hit = new HashSet<double>();
                    EnemyInstance cur = target;
                    double jumps = U.Round(st["chains"]);
                    while (cur != null)
                    {
                        hit.Add(cur.uid);
                        pts.Add(cur.x);
                        pts.Add(cur.y);
                        double cx = cur.x;
                        double cy = cur.y;
                        DamageSystem.applyHit(sim, cur, p);
                        if (jumps-- <= 0) break;
                        EnemyInstance next = null;
                        double bd = 2.2 * 2.2;
                        var enemies = sim.s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!canHit(st, e) || hit.Contains(e.uid)) continue;
                            double d = Sq(e.x - cx) + Sq(e.y - cy);
                            if (d < bd)
                            {
                                bd = d;
                                next = e;
                            }
                        }
                        cur = next;
                    }
                    sim.emit(new GameEvent { k = "chain", pts = pts });
                    sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = target.x, ty = target.y, uid = t.uid });
                    return true;
                }
                case "gravity":
                {
                    var hits = pickTargets(sim, t, st, 999);
                    if (hits.Count == 0) return false;
                    var p = payload(t, st);
                    var ground = hits.Where((x) => !x.flying && !U.Truthy(x.boss)).ToList();
                    var sorted = U.StableSort(new List<EnemyInstance>(ground), (a, b) => U.Or(a.remaining - b.remaining, a.uid - b.uid));
                    double median = sorted.Count > 0 ? sorted[(int)Math.Floor(sorted.Count / 2.0)].remaining : 0;
                    foreach (var e in hits)
                    {
                        DamageSystem.applyHit(sim, e, p);
                        if (!e.alive) continue;
                        ElementSystem.addStatus(e, "pull", 1, 1, t.uid);
                        if (!e.flying && !U.Truthy(e.boss) && e.remaining < median - 0.5) EnemySystem.riftTeleport(sim, e, U.Round(st["pullCells"]));
                        if (st["rootDur"] > 0 && !U.Truthy(e.boss)) ElementSystem.addStatus(e, "root", st["rootDur"], 1, t.uid);
                    }
                    sim.emit(new GameEvent { k = "gravity", x = ox, y = oy, r = st["range"] });
                    return true;
                }
                case "dragon":
                {
                    if (target == null) return false;
                    var p = payload(t, st);
                    // PORT: Math.cos/Math.sin können sich im letzten Bit von V8 (fdlibm) unterscheiden.
                    double dx = ox + Math.Cos(t.orbit) * 0.9;
                    double dy = oy + Math.Sin(t.orbit) * 0.9;
                    if (st["chains"] > 0)
                    {
                        var pts = new List<double> { dx, dy };
                        var hit = new HashSet<double>();
                        EnemyInstance cur = target;
                        double jumps = U.Round(st["chains"]);
                        while (cur != null)
                        {
                            hit.Add(cur.uid);
                            pts.Add(cur.x);
                            pts.Add(cur.y);
                            double cx = cur.x;
                            double cy = cur.y;
                            DamageSystem.applyHit(sim, cur, withType(p, "lightning"));
                            if (jumps-- <= 0) break;
                            EnemyInstance next = null;
                            double bd = 2.4 * 2.4;
                            var enemies = sim.s.enemies;
                            for (int i = 0; i < enemies.Count; i++)
                            {
                                var e = enemies[i];
                                if (!canHit(st, e) || hit.Contains(e.uid)) continue;
                                double d = Sq(e.x - cx) + Sq(e.y - cy);
                                if (d < bd)
                                {
                                    bd = d;
                                    next = e;
                                }
                            }
                            cur = next;
                        }
                        sim.emit(new GameEvent { k = "chain", pts = pts });
                    }
                    else
                    {
                        double r2 = st["splash"] * st["splash"];
                        var enemies = sim.s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!canHit(st, e)) continue;
                            if (Sq(e.x - target.x) + Sq(e.y - target.y) <= r2) DamageSystem.applyHit(sim, e, p, e == target ? 1 : 0.8);
                        }
                        sim.emit(new GameEvent { k = "breath", x = dx, y = dy, tx = target.x, ty = target.y });
                    }
                    return true;
                }
                case "laser":
                {
                    if (target == null) return false;
                    t.charge = U.Min(1, t.charge + st["cooldown"] / 3);
                    double mult = 1 + (U.Max(1, st["laserRamp"]) - 1) * t.charge;
                    var beams = new List<EnemyInstance> { target };
                    if (st["multishot"] > 1)
                        beams.AddRange(U.Slice(pickTargets(sim, t, st, U.Round(st["multishot"])).Where((x) => x != target).ToList(), 0, U.Round(st["multishot"]) - 1));
                    var p = payload(t, st);
                    for (int bi = 0; bi < beams.Count; bi++)
                    {
                        var tg = beams[bi];
                        double ddx = tg.x - ox;
                        double ddy = tg.y - oy;
                        double len = U.Or(U.Hypot(ddx, ddy), 1);
                        double ux = ddx / len;
                        double uy = ddy / len;
                        double reach = st["range"] + 0.3;
                        var enemies = sim.s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!canHit(st, e)) continue;
                            double rx = e.x - ox;
                            double ry = e.y - oy;
                            double along = rx * ux + ry * uy;
                            if (along < 0 || along > reach) continue;
                            double perp = Math.Abs(rx * uy - ry * ux);
                            if (perp > 0.38) continue;
                            DamageSystem.applyHit(sim, e, p, bi == 0 ? mult : mult * 0.75);
                        }
                    }
                    if (sim.s.tick % 30 == 0) sim.emit(new GameEvent { k = "laser", x = ox, y = oy, tx = target.x, ty = target.y, power = t.charge, uid = t.uid });
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>Lichtbögen zwischen Tesla-Spulen: Gegner, die den Bogen kreuzen, erleiden Blitzschaden.</summary>
        private static void updateTeslaArcs(GameSimulation sim, double dt)
        {
            var teslas = sim.s.towers.Where((t) => (t.def == "tesla" || t.def == "thunderspire") && t.stunned <= 0).ToList();
            if (teslas.Count < 2) return;
            for (int i = 0; i < teslas.Count; i++)
            {
                var a = teslas[i];
                var sa = sim.stats(a);
                for (int j = i + 1; j < teslas.Count; j++)
                {
                    var b = teslas[j];
                    var sb = sim.stats(b);
                    double reach = U.Min(sa["arcRange"], sb["arcRange"]);
                    double ax = a.cx + 0.5, ay = a.cy + 0.5, bx = b.cx + 0.5, by = b.cy + 0.5;
                    double vx = bx - ax, vy = by - ay;
                    double len2 = vx * vx + vy * vy;
                    if (len2 > reach * reach || len2 == 0) continue;
                    double dps = (sa["arcDps"] + sb["arcDps"]) / 2;
                    // PORT: Index-Schleife über die erfasste Liste = JS for…of.
                    var enemies = sim.s.enemies;
                    for (int ei = 0; ei < enemies.Count; ei++)
                    {
                        var e = enemies[ei];
                        if (!e.alive || e.stealthed || e.burrowed) continue;
                        double k = U.Max(0, U.Min(1, ((e.x - ax) * vx + (e.y - ay) * vy) / len2));
                        double px = ax + vx * k, py = ay + vy * k;
                        if (Sq(e.x - px) + Sq(e.y - py) <= 0.45 * 0.45) DamageSystem.dealDamage(sim, e, dps * dt, "lightning", a.uid);
                    }
                }
            }
        }

        /// <summary>Liefert Tesla-Paare mit aktivem Lichtbogen (für die Darstellung). TS-Tupel → Array der Länge 2.</summary>
        public static List<TowerInstance[]> teslaPairs(GameSimulation sim)
        {
            var @out = new List<TowerInstance[]>();
            var teslas = sim.s.towers.Where((t) => (t.def == "tesla" || t.def == "thunderspire") && t.stunned <= 0).ToList();
            for (int i = 0; i < teslas.Count; i++)
            {
                for (int j = i + 1; j < teslas.Count; j++)
                {
                    sim.c.towerStats.TryGetValue(teslas[i].uid, out var sa);
                    sim.c.towerStats.TryGetValue(teslas[j].uid, out var sb);
                    if (sa == null || sb == null) continue;
                    double reach = U.Min(sa["arcRange"], sb["arcRange"]);
                    if (Sq(teslas[i].cx - teslas[j].cx) + Sq(teslas[i].cy - teslas[j].cy) <= reach * reach) @out.Add(new TowerInstance[] { teslas[i], teslas[j] });
                }
            }
            return @out;
        }

        private static void executeActive(GameSimulation sim, TowerInstance t, string id)
        {
            var st = sim.stats(t);
            double ox = t.cx + 0.5;
            double oy = t.cy + 0.5;
            double range = U.Max(st["range"], 2.5);
            var inRangeEnemies = sim.s.enemies.Where((x) => x.alive && !x.stealthed && !x.burrowed && Sq(x.x - ox) + Sq(x.y - oy) <= range * range).ToList();
            switch (id)
            {
                case "deepfreeze":
                    foreach (var e in inRangeEnemies)
                    {
                        if (U.Truthy(e.boss)) EnemySystem.interruptEnemy(sim, e, 40);
                        else ElementSystem.addStatus(e, "freeze", 2.5, 1, t.uid);
                    }
                    sim.emit(new GameEvent { k = "pulse", tower = "frost", x = ox, y = oy, r = range });
                    break;
                case "goldrush":
                {
                    double g = EconomySystem.collectIncomeFor(sim, t, 1.5);
                    sim.emit(new GameEvent { k = "gold", amount = g, x = ox, y = oy });
                    break;
                }
                case "rewind":
                    foreach (var e in inRangeEnemies) if (!U.Truthy(e.boss)) EnemySystem.riftTeleport(sim, e, 3);
                    break;
                case "meteorstorm":
                {
                    var tg = inRangeEnemies.Where((x) => !x.flying).ToList();
                    for (int i = 0; i < 5 && tg.Count > 0; i++)
                    {
                        var e = tg[(int)Math.Floor(sim.rand() * tg.Count)];
                        sim.s.zones.Add(new Zone { uid = sim.uid(), kind = "meteor", x = e.x + (sim.rand() - 0.5), y = e.y + (sim.rand() - 0.5), r = 1.3, t = 0.1, armed = 0.6 + i * 0.4, power = U.Max(60, st["damage"]), tick = 0, link = t.uid, src = t.uid });
                    }
                    break;
                }
                case "nova":
                {
                    var p = payload(t, st);
                    p.damage = U.Max(st["damage"], 25) * 4;
                    if (!U.Truthy(Towers.TOWERS[t.def].damageType)) p.type = "arcane";
                    foreach (var e in inRangeEnemies) DamageSystem.applyHit(sim, e, p);
                    sim.emit(new GameEvent { k = "pulse", tower = "resonator", x = ox, y = oy, r = range });
                    break;
                }
                case "surge":
                    sim.s.buffs.Add(new GlobalBuff { id = "surge", t = 8, power = 1, src = t.uid });
                    sim.c.statsDirty = true;
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Kristalllaser-Dämpfung (2.6): Mehrere Laser dicht beieinander stören sich gegenseitig
        /// (−20 % je weiterem Laser in 3 Feldern, höchstens −60 %), und direkt am Riss (≤ 3 Felder zu einem
        /// Gegnereingang) bricht die Rissenergie den Strahl (−35 %).
        /// </summary>
        public static LaserPenaltyResult laserPenalty(GameSimulation sim, TowerInstance t)
        {
            var s = sim.s;
            double neighbors = 0;
            foreach (var o in s.towers)
            {
                if (o == t || Towers.TOWERS[o.def].attack != "laser") continue;
                if (Sq(o.cx - t.cx) + Sq(o.cy - t.cy) <= Sq(LASER.interferenceRange)) neighbors++;
            }
            bool nearRift = WorldGenerator.spawnsOf(s.map).Any((sp) => Sq(sp.x - t.cx) + Sq(sp.y - t.cy) <= Sq(LASER.riftRange));
            double mul = U.Max(1 - LASER.interferenceMax, 1 - LASER.interferencePer * neighbors) * (nearRift ? LASER.riftMul : 1);
            return new LaserPenaltyResult { mul = mul, neighbors = neighbors, nearRift = nearRift };
        }

        public static readonly LaserRules LASER = new LaserRules { interferenceRange = 3, interferencePer = 0.2, interferenceMax = 0.6, riftRange = 3, riftMul = 0.65 };

        // ------------------------------------------------------------ Minenwerfer (2.8)

        public static readonly MineRules MINE = new MineRules { flight = 0.55, trigger = 0.5, life = 60, spacing = 0.8 };

        /// <summary>Anzahl der Minen (fliegend oder scharf), die ein Turm gerade im Feld hat.</summary>
        public static double minesOf(GameSimulation sim, double uid)
        {
            double n = 0;
            foreach (var z in sim.s.zones) if (z.kind == "mine" && z.link == uid && (z.t > 0 || z.armed > 0)) n++;
            return n;
        }

        private class MineCand
        {
            public double x;
            public double y;
            public double score;
        }

        /// <summary>
        /// Wirft Minen auf freie Lane-Felder in Reichweite – bevorzugt dorthin, wo Gegner gleich vorbeilaufen,
        /// und mit Abstand zu bereits liegenden Minen. Nur solange Gegner unterwegs sind.
        /// </summary>
        private static bool throwMines(GameSimulation sim, TowerInstance t, Dictionary<string, double> st, double ox, double oy)
        {
            var s = sim.s;
            if (!s.enemies.Any((e) => e.alive)) return false;
            double max = U.Round(st["mineMax"]);
            double have = minesOf(sim, t.uid);
            if (have >= max) return false;
            var map = s.map;
            double r = st["range"];
            var cands = new List<MineCand>();
            double x0 = U.Max(0, Math.Floor(ox - r));
            double x1 = U.Min(map.width - 1, Math.Floor(ox + r));
            double y0 = U.Max(0, Math.Floor(oy - r));
            double y1 = U.Min(map.height - 1, Math.Floor(oy + r));
            var mines = s.zones.Where((z) => z.kind == "mine" && (z.t > 0 || z.armed > 0)).ToList();
            for (double y = y0; y <= y1; y++)
            {
                for (double x = x0; x <= x1; x++)
                {
                    double i = y * map.width + x;
                    double c = map.cells[(int)i];
                    bool lane = c == Cell.Path || (map.variant == "mazing" && (c == Cell.Open || c == Cell.NoBuild) && !sim.c.towerByCell.ContainsKey(i));
                    if (!lane) continue;
                    double cx = x + 0.5;
                    double cy = y + 0.5;
                    if (Sq(cx - ox) + Sq(cy - oy) > r * r) continue;
                    if (map.variant == "mazing" && sim.c.field[(int)i] >= 1e8) continue;
                    double near = double.PositiveInfinity;
                    foreach (var m in mines) near = U.Min(near, Sq(m.x - cx) + Sq(m.y - cy));
                    if (near < MINE.spacing * MINE.spacing) continue;
                    // Gegner, die auf das Feld zulaufen: kleiner Abstand → höhere Priorität
                    double foe = double.PositiveInfinity;
                    foreach (var e in s.enemies) if (e.alive && !e.flying) foe = U.Min(foe, Sq(e.x - cx) + Sq(e.y - cy));
                    cands.Add(new MineCand { x = cx, y = cy, score = Math.Sqrt(U.Min(foe, 64)) + sim.rand() * 1.5 });
                }
            }
            if (cands.Count == 0) return false;
            U.StableSort(cands, (a, b) => a.score - b.score);
            double n = U.Max(1, U.Round(st["multishot"]));
            double thrown = 0;
            foreach (var c in cands)
            {
                if (thrown >= n || have >= max) break;
                if (thrown > 0 && s.zones.Any((z) => z.kind == "mine" && z.link == t.uid && Sq(z.x - c.x) + Sq(z.y - c.y) < MINE.spacing * MINE.spacing)) continue;
                double jx = (sim.rand() - 0.5) * 0.3;
                double jy = (sim.rand() - 0.5) * 0.3;
                s.zones.Add(new Zone { uid = sim.uid(), kind = "mine", x = c.x + jx, y = c.y + jy, r = MINE.trigger, t = MINE.life, armed = MINE.flight + thrown * 0.12, power = st["damage"], tick = 0, link = t.uid, src = t.uid });
                thrown++;
                have++;
            }
            if (!U.Truthy(thrown)) return false;
            sim.emit(new GameEvent { k = "shot", tower = t.def, x = ox, y = oy, tx = cands[0].x, ty = cands[0].y, uid = t.uid });
            // PORT: Math.atan2 kann sich im letzten Bit von V8 (fdlibm) unterscheiden.
            t.aim = Math.Atan2(cands[0].y - oy, cands[0].x - ox);
            return true;
        }

        /// <summary>Scharfe Mine: explodiert, sobald ein Bodengegner darauf tritt.</summary>
        public static void updateMine(GameSimulation sim, Zone z)
        {
            var s = sim.s;
            bool trig = false;
            foreach (var e in s.enemies)
            {
                if (!e.alive || e.flying || e.burrowed) continue;
                if (Sq(e.x - z.x) + Sq(e.y - z.y) <= z.r * z.r)
                {
                    trig = true;
                    break;
                }
            }
            if (!trig) return;
            z.t = 0;
            sim.c.towerByUid.TryGetValue(z.link, out var t);
            var st = t != null ? sim.stats(t) : null;
            if (t != null && st != null)
            {
                var p = payload(t, st);
                double rad = U.Max(0.6, st["splash"]);
                double r2 = rad * rad;
                // PORT: Index-Schleife über die erfasste Liste = JS for…of.
                var enemies = s.enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.alive || e.flying) continue;
                    double d2 = Sq(e.x - z.x) + Sq(e.y - z.y);
                    if (d2 <= r2) DamageSystem.applyHit(sim, e, p, d2 < r2 * 0.3 ? 1 : 0.75);
                }
                if (st["cloudDur"] > 0 && st["cloudRadius"] > 0)
                {
                    s.zones.Add(new Zone { uid = sim.uid(), kind = "poisonCloud", x = z.x, y = z.y, r = st["cloudRadius"], t = st["cloudDur"], armed = 0, power = st["poisonDps"], tick = 0, link = 0, src = t.uid });
                }
                sim.emit(new GameEvent { k = "hit", type = Towers.TOWERS[t.def].damageType ?? "physical", x = z.x, y = z.y, amount = st["damage"], splash = rad });
            }
            else
            {
                // Turm verkauft: die Mine explodiert trotzdem mit ihrer gespeicherten Ladung
                var enemies = s.enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e.alive && !e.flying && Sq(e.x - z.x) + Sq(e.y - z.y) <= 1) DamageSystem.dealDamage(sim, e, z.power, "physical", 0);
                }
                sim.emit(new GameEvent { k = "hit", type = "physical", x = z.x, y = z.y, amount = z.power, splash = 1 });
            }
            sim.emit(new GameEvent { k = "burst", x = z.x, y = z.y, r = t != null && st != null ? U.Max(0.6, st["splash"]) : 1 });
        }
    }
}
