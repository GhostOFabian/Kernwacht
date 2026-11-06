// Übersetzung von sim/BossSystem.ts.
// Bosslogik: Phasen, angekündigte Fähigkeiten (Telegraphs) und Unterbrechung über Standfestigkeit.
// Wirkungen mit Zielgebiet laufen über Zonen (ZoneSystem), die während der Ankündigung sichtbar sind.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class BossSystem
    {
        private static double Sq(double x) { return x * x; }

        public static void initBossAbilities(GameSimulation sim, EnemyInstance e)
        {
            if (!U.Truthy(e.boss)) return;
            var ph = Enemies.BOSSES[e.boss].phases[0];
            e.abilities = ph.abilities.Select((id) => new AbilityState { id = id, cd = 4, windup = 0, tx = 0, ty = 0, r = 0 }).ToList();
        }

        public static void onBossShieldBroken(GameSimulation sim, EnemyInstance e)
        {
            if (!U.Truthy(e.boss)) return;
            var phases = Enemies.BOSSES[e.boss].phases;
            int ni = (int)(e.phase + 1);
            var next = ni >= 0 && ni < phases.Count ? phases[ni] : null;
            if (next != null && next.trigger == "shieldBroken") enterPhase(sim, e, e.phase + 1);
            else sim.emit(new GameEvent { k = "message", text = "Schild gebrochen!", severity = "info" });
        }

        private static void enterPhase(GameSimulation sim, EnemyInstance e, double idx)
        {
            if (!U.Truthy(e.boss)) return;
            var def = Enemies.BOSSES[e.boss];
            var ph = def.phases[(int)idx];
            e.phase = idx;
            var old = new Dictionary<string, AbilityState>();
            foreach (var a in e.abilities) old[a.id] = a;
            e.abilities = ph.abilities.Select((id) => U.Get(old, id) ?? new AbilityState { id = id, cd = id == "reShield" ? 0.5 : 2.5, windup = 0, tx = 0, ty = 0, r = 0 }).ToList();
            if (sim.s.boss != null && sim.s.boss.uid == e.uid)
            {
                sim.s.boss.phase = idx;
                sim.s.boss.phaseName = ph.name;
            }
            sim.emit(new GameEvent { k = "bossPhase", boss = e.boss, phase = idx, name = ph.name });
            sim.emit(new GameEvent { k = "message", text = $"{def.name}: {ph.name}", severity = "warn" });
            if (ph.abilities.Contains("ashFury"))
            {
                e.abilityMul = 0.6;
                sim.emit(new GameEvent { k = "ability", id = "ashFury", x = e.x, y = e.y, stage = "cast" });
            }
        }

        public static void updateBosses(GameSimulation sim, double dt)
        {
            var s = sim.s;
            if (s.boss != null)
            {
                var be = U.Get(sim.c.enemyByUid, s.boss.uid);
                if (be == null || !be.alive) s.boss = null;
            }
            // Index-Schleife: castBoss (Brut) kann während der Schleife Gegner anhängen – JS iteriert diese mit.
            var enemies = s.enemies;
            for (int ei = 0; ei < enemies.Count; ei++)
            {
                var e = enemies[ei];
                if (!e.alive || !U.Truthy(e.boss)) continue;
                var def = Enemies.BOSSES[e.boss];
                // Zwillingsbund: nah beieinander heilen sich die Zwillinge gegenseitig.
                if (U.Truthy(def.partner))
                {
                    var p = s.enemies.FirstOrDefault((o) => o.alive && o.boss == def.partner);
                    if (p != null && Sq(p.x - e.x) + Sq(p.y - e.y) <= 9 && !ElementSystem.hasStatus(e, "sealed")) e.hp = U.Min(e.maxHp, e.hp + e.maxHp * 0.02 * dt);
                }
                // Phasenwechsel über HP-Schwellen (Schild-Auslöser separat)
                int ni = (int)(e.phase + 1);
                var next = ni >= 0 && ni < def.phases.Count ? def.phases[ni] : null;
                if (next != null && !U.Truthy(next.trigger) && e.hp / e.maxHp <= next.hpBelow) enterPhase(sim, e, e.phase + 1);
                bool stunned = ElementSystem.hasStatus(e, "stun");
                var abs = e.abilities;
                for (int ai = 0; ai < abs.Count; ai++)
                {
                    var a = abs[ai];
                    var ad = Enemies.ABILITIES[a.id];
                    if (a.windup > 0)
                    {
                        a.windup -= dt;
                        if (a.windup <= 0)
                        {
                            a.windup = 0;
                            a.cd = ad.cooldown * e.abilityMul;
                            castBoss(sim, e, a);
                        }
                        continue;
                    }
                    if (stunned) continue;
                    a.cd -= dt;
                    if (a.cd <= 0) beginBoss(sim, e, a);
                }
            }
        }

        private static void telegraph(GameSimulation sim, EnemyInstance e, string id, double x, double y, double r, double t)
        {
            if (!U.Truthy(e.boss)) return;
            sim.s.telegraphs.Add(new Telegraph
            {
                uid = sim.uid(), text = $"{Enemies.BOSSES[e.boss].name}: {Enemies.ABILITIES[id].name}", x = x, y = y, r = r, t = U.Max(t, 1.5), total = U.Max(t, 1.5), severity = "danger",
            });
            sim.emit(new GameEvent { k = "ability", id = id, x = x, y = y, stage = "warn" });
        }

        private static double zone(GameSimulation sim, EnemyInstance e, string kind, double x, double y, double r, double armed, double t, double power)
        {
            double uid = sim.uid();
            sim.s.zones.Add(new Zone { uid = uid, kind = kind, x = x, y = y, r = r, t = t, armed = armed, power = power, tick = 0, link = 0, src = e.uid });
            return uid;
        }

        private class ScoredTower
        {
            public TowerInstance t;
            public double n;
        }

        private static void beginBoss(GameSimulation sim, EnemyInstance e, AbilityState a)
        {
            var ad = Enemies.ABILITIES[a.id];
            var map = sim.s.map;
            switch (a.id)
            {
                case "riftJump":
                {
                    // 4.0: Challenge-Bosse springen nicht mehr im letzten Drittel des Weges und seltener
                    if (e.challenge == true)
                    {
                        var sp = map.spawn;
                        double full = sim.c.field[(int)(sp.y * map.width + sp.x)];
                        if (full < Pathfinding.INF && e.remaining < full * 0.35)
                        {
                            a.cd = 4;
                            return;
                        }
                    }
                    double cx = e.nx;
                    double cy = e.ny;
                    for (int i = 0; i < 5; i++)
                    {
                        var n = Pathfinding.nextCell(map, sim.c.field, cx, cy);
                        if (n == null) break;
                        cx = n.x;
                        cy = n.y;
                    }
                    if (cx == map.goal.x && cy == map.goal.y)
                    {
                        a.cd = 3; // Kein Sprung direkt in den Kern
                        return;
                    }
                    zone(sim, e, "riftJump", cx + 0.5, cy + 0.5, 0.8, ad.windup, 0.3, 0);
                    a.tx = cx + 0.5;
                    a.ty = cy + 0.5;
                    telegraph(sim, e, a.id, a.tx, a.ty, 0.8, ad.windup);
                    break;
                }
                case "riftPortals":
                {
                    var path = Pathfinding.tracePath(map, sim.c.field, map.spawn);
                    if (path.Count < 10)
                    {
                        a.cd = 5;
                        return;
                    }
                    var pin = path[(int)U.Min(3, path.Count - 1)];
                    var pout = path[(int)Math.Floor(path.Count * 0.55)];
                    double inUid = zone(sim, e, "portalIn", pin.x + 0.5, pin.y + 0.5, 0.6, ad.windup, 12, 0);
                    double outUid = zone(sim, e, "portalOut", pout.x + 0.5, pout.y + 0.5, 0.6, ad.windup, 12, 0);
                    var zin = sim.s.zones.FirstOrDefault((z) => z.uid == inUid);
                    var zout = sim.s.zones.FirstOrDefault((z) => z.uid == outUid);
                    if (zin != null && zout != null)
                    {
                        zin.link = outUid;
                        zout.link = inUid;
                    }
                    telegraph(sim, e, a.id, pout.x + 0.5, pout.y + 0.5, 0.6, ad.windup);
                    break;
                }
                case "summonBrood":
                    telegraph(sim, e, a.id, e.x, e.y, 1, ad.windup);
                    break;
                case "fireZones":
                {
                    var towers = sim.s.towers.Where((t) => !TowersNew.MINES.Contains(t.def)).ToList();
                    if (towers.Count == 0)
                    {
                        a.cd = 3;
                        return;
                    }
                    var scored = U.StableSort(
                        towers.Select((t) => new ScoredTower { t = t, n = towers.Count((o) => Sq(o.cx - t.cx) + Sq(o.cy - t.cy) <= 2.6) }).ToList(),
                        (p, q) => U.Or(q.n - p.n, p.t.uid - q.t.uid));
                    var picks = new List<TowerInstance> { scored[0].t };
                    var second = scored.FirstOrDefault((p) => Sq(p.t.cx - picks[0].cx) + Sq(p.t.cy - picks[0].cy) > 9);
                    if (second != null) picks.Add(second.t);
                    foreach (var t in picks)
                    {
                        zone(sim, e, "fireZone", t.cx + 0.5, t.cy + 0.5, 1.6, ad.windup, 5, 5);
                        telegraph(sim, e, a.id, t.cx + 0.5, t.cy + 0.5, 1.6, ad.windup);
                    }
                    break;
                }
                case "nullPulse":
                    zone(sim, e, "nullPulse", e.x, e.y, 4, ad.windup, 0.5, 6);
                    telegraph(sim, e, a.id, e.x, e.y, 4, ad.windup);
                    break;
                case "reShield":
                    if (e.reshieldUsed)
                    {
                        a.cd = 999;
                        return;
                    }
                    telegraph(sim, e, a.id, e.x, e.y, 1, ad.windup);
                    break;
                case "ashFury":
                case "twinBond":
                    a.cd = 999;
                    return;
                case "devour":
                {
                    TowerInstance best = null;
                    double bd = 3.0 * 3.0;
                    foreach (var t in sim.s.towers)
                    {
                        if (Towers.TOWERS[t.def].attack == "none") continue;
                        double d = Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y);
                        if (d < bd)
                        {
                            bd = d;
                            best = t;
                        }
                    }
                    if (best == null)
                    {
                        a.cd = 1.5;
                        return;
                    }
                    a.tx = best.cx + 0.5;
                    a.ty = best.cy + 0.5;
                    a.r = best.uid;
                    zone(sim, e, "devour", a.tx, a.ty, 0.8, ad.windup, 0.3, 0);
                    telegraph(sim, e, a.id, a.tx, a.ty, 0.8, ad.windup);
                    break;
                }
                case "stomp":
                    zone(sim, e, "stomp", e.x, e.y, 2.8, ad.windup, 0.4, 4);
                    telegraph(sim, e, a.id, e.x, e.y, 2.8, ad.windup);
                    break;
                default:
                    a.cd = ad.cooldown;
                    return;
            }
            a.windup = ad.windup;
        }

        private static void castBoss(GameSimulation sim, EnemyInstance e, AbilityState a)
        {
            sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "cast" });
            if (a.id == "summonBrood")
            {
                for (int i = 0; i < 4; i++)
                {
                    var c = EnemySystem.spawnEnemyAt(sim, "runner", false, e);
                    c.x += ((i % 2) - 0.5) * 0.3;
                    c.y += (Math.Floor(i / 2.0) - 0.5) * 0.3;
                }
            }
            else if (a.id == "devour")
            {
                var s = sim.s;
                var t = U.Get(sim.c.towerByUid, a.r);
                if (t != null && Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= 3.6 * 3.6)
                {
                    s.towers = s.towers.Where((x) => x.uid != t.uid).ToList();
                    t.target = 0;
                    t.stunned = 0;
                    e.swallowed.Add(t);
                    sim.reindex();
                    sim.c.networkDirty = true;
                    sim.c.statsDirty = true;
                    if (s.map.variant == "mazing") sim.c.fieldDirty = true;
                    sim.emit(new GameEvent { k = "devour", tower = t.def, x = t.cx + 0.5, y = t.cy + 0.5 });
                    sim.emit(new GameEvent { k = "message", text = $"Der Schlund hat {Towers.TOWERS[t.def].name} verschlungen! Besiege ihn, um ihn zurückzuholen.", severity = "danger" });
                }
            }
            else if (a.id == "reShield")
            {
                e.reshieldUsed = true;
                e.maxShield = e.maxHp * 0.2;
                e.shield = e.maxShield;
                sim.emit(new GameEvent { k = "message", text = "Der Nullhüter erneuert seinen Schild!", severity = "warn" });
            }
            // riftJump, riftPortals, fireZones, nullPulse wirken über ihre Zonen (ZoneSystem).
        }

        /// <summary>Tod eines Bosses: verschlungene Türme zurückgeben, Zwilling gerät in Raserei.</summary>
        public static void onBossDeath(GameSimulation sim, EnemyInstance e)
        {
            var s = sim.s;
            if (!U.Truthy(e.boss)) return;
            if (e.swallowed.Count > 0)
            {
                double refund = 0;
                foreach (var t in e.swallowed)
                {
                    double i = t.cy * s.map.width + t.cx;
                    if (!sim.c.towerByCell.ContainsKey(i) && s.map.cells[(int)i] == Cell.Open)
                    {
                        t.stunned = 0;
                        s.towers.Add(t);
                    }
                    else
                    {
                        refund += Math.Floor(t.invested * 0.7);
                    }
                }
                e.swallowed = new List<TowerInstance>();
                s.gold += refund;
                sim.reindex();
                sim.c.networkDirty = true;
                sim.c.statsDirty = true;
                if (s.map.variant == "mazing") sim.c.fieldDirty = true;
                sim.emit(new GameEvent { k = "message", text = $"Die verschlungenen Türme sind zurück{(U.Truthy(refund) ? $" (+{U.Fmt(refund)} Gold für blockierte Felder)" : "")}!", severity = "info" });
            }
            var partner = Enemies.BOSSES[e.boss].partner;
            if (U.Truthy(partner))
            {
                var p = s.enemies.FirstOrDefault((o) => o.alive && o.boss == partner);
                if (p != null && !p.enraged)
                {
                    p.enraged = true;
                    p.hp = U.Min(p.maxHp, p.hp + p.maxHp * 0.3);
                    sim.emit(new GameEvent { k = "message", text = $"{Enemies.BOSSES[partner].name} gerät in Raserei!", severity = "danger" });
                }
            }
        }
    }
}
