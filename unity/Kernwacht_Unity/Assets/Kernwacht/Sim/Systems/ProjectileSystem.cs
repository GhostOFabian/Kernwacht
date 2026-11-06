// Übersetzung von sim/ProjectileSystem.ts.
// Projektile. Stirbt das Ziel unterwegs, fliegt das Projektil zur letzten bekannten Position:
// Flächengeschosse explodieren dort, Einzelgeschosse verfallen ohne Wirkung.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class ProjectileSystem
    {
        /// <summary>JS <c>x ** 2</c> (V8/fdlibm: Exponent 2 liefert exakt x*x).</summary>
        private static double Sq(double x) { return x * x; }

        public static void updateProjectiles(GameSimulation sim, double dt)
        {
            // PORT: Index-Schleife über die erfasste Liste = JS for…of (sieht während der Schleife angehängte Projektile mit).
            var projectiles = sim.s.projectiles;
            for (int i = 0; i < projectiles.Count; i++)
            {
                var p = projectiles[i];
                if (!p.alive) continue;
                if (p.pierceLeft > 0) updatePiercing(sim, p, dt);
                else updateHoming(sim, p, dt);
            }
        }

        private static void updateHoming(GameSimulation sim, Projectile p, double dt)
        {
            sim.c.enemyByUid.TryGetValue(p.target, out var target);
            if (target != null && target.alive)
            {
                p.tx = target.x;
                p.ty = target.y;
            }
            double dx = p.tx - p.x;
            double dy = p.ty - p.y;
            double d = U.Hypot(dx, dy);
            double step = p.speed * dt;
            p.traveled += U.Min(step, d);
            if (d > step && p.traveled < 40)
            {
                p.x += (dx / d) * step;
                p.y += (dy / d) * step;
                return;
            }
            p.x = p.tx;
            p.y = p.ty;
            p.alive = false;
            impact(sim, p, target != null && target.alive ? target : null);
        }

        private static void impact(GameSimulation sim, Projectile p, EnemyInstance target)
        {
            var pl = p.payload;
            var st = pl.stats;
            if (pl.splash > 0)
            {
                double r2 = pl.splash * pl.splash;
                var enemies = sim.s.enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.alive || (e.flying && !ElementSystem.hasStatus(e, "grounded") && st["hitsAir"] <= 0)) continue;
                    double d2 = Sq(e.x - p.x) + Sq(e.y - p.y);
                    if (d2 > r2) continue;
                    DamageSystem.applyHit(sim, e, pl, e == target ? 1 : 0.75);
                }
            }
            else if (target != null)
            {
                double ev = U.Truthy(target.boss) ? 0 : (Enemies.ENEMIES[target.def].evasion ?? 0);
                if (ev > 0 && sim.rand() < ev) sim.emit(new GameEvent { k = "immune", x = target.x, y = target.y });
                else DamageSystem.applyHit(sim, target, pl);
            }
            sim.emit(new GameEvent { k = "hit", type = pl.type, x = p.x, y = p.y, amount = pl.damage, splash = pl.splash });
            if (st["fragments"] > 0)
            {
                double n = U.Round(st["fragments"]);
                for (int i = 0; i < n; i++)
                {
                    // PORT: Math.cos/Math.sin können sich im letzten Bit von V8 (fdlibm) unterscheiden.
                    double a = (i / n) * Math.PI * 2 + sim.rand() * 0.8;
                    double fx = p.x + Math.Cos(a) * 0.95;
                    double fy = p.y + Math.Sin(a) * 0.95;
                    var enemies = sim.s.enemies;
                    for (int j = 0; j < enemies.Count; j++)
                    {
                        var e = enemies[j];
                        if (!e.alive || e.flying) continue;
                        if (Sq(e.x - fx) + Sq(e.y - fy) <= 0.7 * 0.7) DamageSystem.applyHit(sim, e, pl, 0.4);
                    }
                    sim.emit(new GameEvent { k = "hit", type = pl.type, x = fx, y = fy, amount = pl.damage * 0.4, splash = 0.7 });
                }
            }
            if (st["cloudDur"] > 0 && st["cloudRadius"] > 0)
            {
                sim.s.zones.Add(new Zone
                {
                    uid = sim.uid(), kind = "poisonCloud", x = p.x, y = p.y, r = st["cloudRadius"], t = st["cloudDur"], armed = 0,
                    power = st["poisonDps"], tick = 0, link = 0, src = pl.tower,
                });
            }
        }

        private static void updatePiercing(GameSimulation sim, Projectile p, double dt)
        {
            double step = p.speed * dt;
            double sub = U.Max(1, Math.Ceiling(step / 0.25));
            for (int k = 0; k < sub && p.alive; k++)
            {
                p.x += p.dirX * (step / sub);
                p.y += p.dirY * (step / sub);
                p.traveled += step / sub;
                var enemies = sim.s.enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.alive || (e.flying && !ElementSystem.hasStatus(e, "grounded")) || p.hitList.Contains(e.uid)) continue;
                    if (Sq(e.x - p.x) + Sq(e.y - p.y) > 0.42 * 0.42) continue;
                    p.hitList.Add(e.uid);
                    DamageSystem.applyHit(sim, e, p.payload);
                    sim.emit(new GameEvent { k = "hit", type = p.payload.type, x = e.x, y = e.y, amount = p.payload.damage, splash = 0 });
                    if (p.hitList.Count >= p.pierceLeft)
                    {
                        p.alive = false;
                        break;
                    }
                }
                if (p.traveled >= p.maxTravel || p.x < -1 || p.y < -1 || p.x > sim.s.map.width + 1 || p.y > sim.s.map.height + 1) p.alive = false;
            }
        }
    }
}
