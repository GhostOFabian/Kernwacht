// Übersetzung von sim/ZoneSystem.ts.
// Bodenzonen: Giftwolken, Bossportale, angekündigte Feuerzonen, Nullpulse, Rissprung-Ziele.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class ZoneSystem
    {
        /// <summary>JS <c>x ** 2</c> (V8/fdlibm: Exponent 2 liefert exakt x*x).</summary>
        private static double Sq(double x) { return x * x; }

        public static void updateZones(GameSimulation sim, double dt)
        {
            var s = sim.s;
            // PORT: Index-Schleife über die erfasste Liste = JS for…of (sieht während der Schleife angehängte Zonen mit).
            var zones = s.zones;
            for (int zi = 0; zi < zones.Count; zi++)
            {
                var z = zones[zi];
                if (z.armed > 0)
                {
                    z.armed -= dt;
                    if (z.armed <= 0)
                    {
                        z.armed = 0;
                        onArmed(sim, z);
                    }
                    continue;
                }
                z.t -= dt;
                if (z.t <= 0) continue;
                if (z.kind == "poisonCloud")
                {
                    z.tick -= dt;
                    if (z.tick <= 0)
                    {
                        z.tick = 0.5;
                        sim.c.towerByUid.TryGetValue(z.src, out var t);
                        double maxStacks = t != null ? U.Round(sim.stats(t)["poisonMaxStacks"]) : 5;
                        double dur = t != null ? sim.stats(t)["poisonDur"] : 4;
                        var cst = t != null ? sim.stats(t) : null;
                        bool corrode = cst != null && cst["corrodePct"] > 0;
                        var enemies = s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!e.alive || e.flying) continue;
                            if (Sq(e.x - z.x) + Sq(e.y - z.y) > z.r * z.r) continue;
                            ElementSystem.addStatus(e, "poison", dur, z.power, z.src, maxStacks);
                            // 4.0 Säurepfütze: zersetzt zusätzlich
                            if (corrode) ElementSystem.addStatus(e, "corrode", U.Max(2, cst["corrodeDur"]), cst["corrodePct"], z.src, U.Max(1, U.Round(cst["corrodeMax"])));
                        }
                    }
                }
                else if (z.kind == "mine")
                {
                    TowerSystem.updateMine(sim, z);
                }
                else if (z.kind == "soldier")
                {
                    TowerSystem.updateSoldier(sim, z, dt);
                }
                else if (z.kind == "burnTrap")
                {
                    z.tick -= dt;
                    if (z.tick <= 0)
                    {
                        z.tick = 0.5;
                        var enemies = s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!e.alive || e.flying) continue;
                            if (Sq(e.x - z.x) + Sq(e.y - z.y) <= z.r * z.r) ElementSystem.addStatus(e, "burn", 2.5, z.power, z.src);
                        }
                    }
                }
                else if (z.kind == "portalIn")
                {
                    var @out = s.zones.FirstOrDefault((o) => o.uid == z.link && o.t > 0);
                    if (@out == null) continue;
                    var enemies = s.enemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var e = enemies[i];
                        if (!e.alive || U.Truthy(e.boss) || e.flying || e.riftCd > s.time) continue;
                        if (Sq(e.x - z.x) + Sq(e.y - z.y) > z.r * z.r) continue;
                        double ox = e.x;
                        double oy = e.y;
                        e.x = @out.x;
                        e.y = @out.y;
                        e.nx = Math.Floor(@out.x);
                        e.ny = Math.Floor(@out.y);
                        e.riftCd = s.time + 6;
                        sim.emit(new GameEvent { k = "teleport", x = ox, y = oy, x2 = e.x, y2 = e.y });
                    }
                }
            }
        }

        private static void onArmed(GameSimulation sim, Zone z)
        {
            var s = sim.s;
            switch (z.kind)
            {
                case "riftJump":
                {
                    sim.c.enemyByUid.TryGetValue(z.src, out var boss);
                    if (boss == null || !boss.alive) return;
                    double ox = boss.x;
                    double oy = boss.y;
                    boss.x = z.x;
                    boss.y = z.y;
                    boss.nx = Math.Floor(z.x);
                    boss.ny = Math.Floor(z.y);
                    boss.traveled += 5;
                    sim.emit(new GameEvent { k = "teleport", x = ox, y = oy, x2 = z.x, y2 = z.y });
                    break;
                }
                case "fireZone":
                {
                    bool damp = s.towers.Any((t) => t.def == "frost" && Sq(t.cx + 0.5 - z.x) + Sq(t.cy + 0.5 - z.y) <= Sq(z.r + 0.6));
                    double dur = damp ? z.power * 0.5 : z.power;
                    double hit = 0;
                    foreach (var t in s.towers)
                    {
                        if (Sq(t.cx + 0.5 - z.x) + Sq(t.cy + 0.5 - z.y) <= z.r * z.r)
                        {
                            sim.c.enemyByUid.TryGetValue(z.src, out var by);
                            if (TowerSystem.disableTower(sim, t, dur, by)) hit++;
                        }
                    }
                    z.t = dur;
                    HeroSystem.zoneHitsHeroes(sim, z.x, z.y, z.r, 120, false);
                    if (damp) sim.emit(new GameEvent { k = "message", text = "Frostwächter dämpfen die Feuerzone!", severity = "info" });
                    else if (hit > 0) sim.emit(new GameEvent { k = "message", text = $"{U.Fmt(hit)} Türme verbrannt ({U.Fmt(dur)} s inaktiv)", severity = "warn" });
                    break;
                }
                case "nullPulse":
                {
                    HeroSystem.zoneHitsHeroes(sim, z.x, z.y, z.r, 0, true);
                    double hit = 0;
                    foreach (var t in s.towers)
                    {
                        if (Sq(t.cx + 0.5 - z.x) + Sq(t.cy + 0.5 - z.y) <= Sq(z.r + 0.5))
                        {
                            t.disrupted = U.Max(t.disrupted, z.power);
                            hit++;
                        }
                    }
                    if (hit > 0)
                    {
                        sim.c.networkDirty = true;
                        sim.emit(new GameEvent { k = "message", text = $"Nullpuls: {U.Fmt(hit)} Türme vom Netzwerk getrennt", severity = "warn" });
                    }
                    break;
                }
                case "portalIn":
                    sim.emit(new GameEvent { k = "message", text = "Risskorridor geöffnet!", severity = "warn" });
                    break;
                case "meteor":
                {
                    sim.c.towerByUid.TryGetValue(z.link, out var t);
                    Dictionary<string, double> st = null;
                    if (t != null) sim.c.towerStats.TryGetValue(t.uid, out st);
                    bool big = z.r >= 1.9;
                    if (t != null && st != null)
                    {
                        var p = TowerSystem.towerPayload(t, st);
                        p.damage = z.power;
                        p.splash = z.r;
                        double r2 = z.r * z.r;
                        // PORT: Index-Schleife über die erfasste Liste = JS for…of.
                        var enemies = s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (!e.alive || (e.flying && st["hitsAir"] <= 0)) continue;
                            double d2 = Sq(e.x - z.x) + Sq(e.y - z.y);
                            if (d2 <= r2) DamageSystem.applyHit(sim, e, p, d2 < r2 * 0.25 ? 1 : 0.7);
                        }
                    }
                    else
                    {
                        var enemies = s.enemies;
                        for (int i = 0; i < enemies.Count; i++)
                        {
                            var e = enemies[i];
                            if (e.alive && !e.flying && Sq(e.x - z.x) + Sq(e.y - z.y) <= z.r * z.r) DamageSystem.dealDamage(sim, e, z.power, "fire", z.src);
                        }
                    }
                    sim.emit(new GameEvent { k = "meteor", x = z.x, y = z.y, r = z.r, big = big });
                    sim.emit(new GameEvent { k = "hit", type = "fire", x = z.x, y = z.y, amount = z.power, splash = z.r });
                    break;
                }
                case "stomp":
                {
                    double hit = 0;
                    foreach (var t in s.towers)
                    {
                        if (Sq(t.cx + 0.5 - z.x) + Sq(t.cy + 0.5 - z.y) <= z.r * z.r)
                        {
                            sim.c.enemyByUid.TryGetValue(z.src, out var by);
                            if (TowerSystem.disableTower(sim, t, z.power, by)) hit++;
                        }
                    }
                    sim.emit(new GameEvent { k = "burst", x = z.x, y = z.y, r = z.r });
                    HeroSystem.zoneHitsHeroes(sim, z.x, z.y, z.r, 150, false);
                    if (hit > 0) sim.emit(new GameEvent { k = "message", text = $"Weltenstampfer: {U.Fmt(hit)} Türme betäubt", severity = "warn" });
                    break;
                }
                case "leyNode":
                    sim.c.statsDirty = true;
                    break;
                default:
                    break;
            }
        }
    }
}
