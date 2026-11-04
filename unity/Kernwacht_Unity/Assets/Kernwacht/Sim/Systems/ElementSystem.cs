// Übersetzung von sim/ElementSystem.ts.
// Statuseffekte und Elementarreaktionen. Siehe config/elements.ts für die Kombinationsregeln.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class ElementSystem
    {
        public static StatusInstance getStatus(EnemyInstance e, string id)
        {
            foreach (var s in e.statuses) if (s.id == id) return s;
            return null;
        }

        public static bool hasStatus(EnemyInstance e, string id)
        {
            return getStatus(e, id) != null;
        }

        public static StatusInstance removeStatus(EnemyInstance e, string id)
        {
            int i = e.statuses.FindIndex((s) => s.id == id);
            if (i < 0) return null;
            var removed = e.statuses[i];
            e.statuses.RemoveAt(i);
            return removed;
        }

        /// <summary>
        /// Fügt einen Status hinzu bzw. kombiniert ihn mit einem bestehenden.
        /// maxStacks &gt; 1: Stapel werden addiert (bis Maximum), Dauer erneuert, stärkere Power übernommen.
        /// maxStacks = 1: stärkere Power gewinnt, Dauer = Maximum aus alt und neu.
        /// </summary>
        public static StatusInstance addStatus(EnemyInstance e, string id, double t, double power, double src, double maxStacks = 1, double flag = 0)
        {
            // 3.1: Challenge-Bosse widerstehen Kontrolleffekten
            if (e.challenge == true && (id == "chill" || id == "freeze" || id == "stun" || id == "root")) t *= Scaling.CHALLENGE.ccMul;
            var cur = getStatus(e, id);
            if (cur != null)
            {
                if (maxStacks > 1)
                {
                    cur.stacks = U.Min(maxStacks, cur.stacks + 1);
                    cur.t = U.Max(cur.t, t);
                    if (power >= cur.power)
                    {
                        cur.power = power;
                        cur.src = src;
                    }
                }
                else
                {
                    if (power > cur.power)
                    {
                        cur.power = power;
                        cur.src = src;
                        cur.flag = flag;
                    }
                    cur.t = U.Max(cur.t, t);
                }
                return cur;
            }
            var s = new StatusInstance { id = id, t = t, stacks = 1, power = power, src = src, flag = flag };
            e.statuses.Add(s);
            return s;
        }

        /// <summary>Gesamtverlangsamung durch Statuseffekte (hart begrenzt).</summary>
        public static double slowFactor(EnemyInstance e)
        {
            double slow = 0;
            var chill = getStatus(e, "chill");
            if (chill != null) slow += U.Min(chill.power * chill.stacks, chill.flag > 0 ? chill.flag / 100 : Elements.STATUS_RULES.chillHardCap);
            var poison = getStatus(e, "poison");
            if (poison != null && poison.flag > 0) slow += U.Min(Elements.STATUS_RULES.poisonSlowCap, poison.flag / 100);
            // 3.12: Flieger werden stärker verlangsamt (Netze, Kälte)
            if (e.flying) slow *= Scaling.FLYER.slowMul;
            return U.Min(Elements.STATUS_RULES.chillHardCap, slow);
        }

        public static bool isImmobile(EnemyInstance e)
        {
            return hasStatus(e, "freeze") || hasStatus(e, "stun") || hasStatus(e, "root");
        }

        public static bool canAct(EnemyInstance e)
        {
            return !hasStatus(e, "freeze") && !hasStatus(e, "stun") && !hasStatus(e, "silence");
        }

        private static bool reactionReady(GameSimulation sim, EnemyInstance e, string id)
        {
            var def = Elements.REACTION_BY_ID[id];
            double cd = U.GetN(e.reactCds, id) ?? 0;
            if (cd > sim.s.time) return false;
            e.reactCds[id] = sim.s.time + def.cooldown;
            sim.s.stats.reactions[id] = (U.GetN(sim.s.stats.reactions, id) ?? 0) + 1;
            if (sim.s.heroes.Count > 0) sim.s.riftEnergy = U.Min(Heroes.HERO_RULES.maxEnergy, sim.s.riftEnergy + Heroes.HERO_RULES.energyPerReaction * sim.c.mods.reactionEnergyMul);
            sim.emit(new GameEvent { k = "reaction", id = id, x = e.x, y = e.y });
            return true;
        }

        /// <summary>
        /// Prüft Elementarreaktionen für einen eingehenden Treffer und gibt den angepassten Schaden zurück.
        /// Reaktionsschaden selbst wird mit noReact ausgeführt – keine Ketten möglich.
        /// </summary>
        public static double react(GameSimulation sim, EnemyInstance e, string type, double dmg, double towerUid)
        {
            double @out = dmg;
            if (type == "fire")
            {
                if (hasStatus(e, "chill") && reactionReady(sim, e, "steam"))
                {
                    removeStatus(e, "chill");
                    addStatus(e, "wet", Elements.STATUS_RULES.wetDur, 1, towerUid);
                    @out *= Elements.STATUS_RULES.steamMul;
                }
                var poison = getStatus(e, "poison");
                if (poison != null && reactionReady(sim, e, "toxicBlaze"))
                {
                    removeStatus(e, "poison");
                    double burst = poison.power * poison.stacks * poison.t;
                    double r2 = Elements.STATUS_RULES.toxicBlazeRadius * Elements.STATUS_RULES.toxicBlazeRadius;
                    // PORT: Index-Schleife über die erfasste Liste (JS for…of sieht während der Schleife angehängte Gegner mit).
                    var enemies = sim.s.enemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var o = enemies[i];
                        if (!o.alive) continue;
                        double dx = o.x - e.x;
                        double dy = o.y - e.y;
                        if (dx * dx + dy * dy <= r2) DamageSystem.dealDamage(sim, o, o == e ? burst : burst * 0.6, "poison", poison.src);
                    }
                }
            }
            else if (type == "frost")
            {
                var burn = getStatus(e, "burn");
                if (burn != null && reactionReady(sim, e, "thermalShock"))
                {
                    removeStatus(e, "burn");
                    DamageSystem.dealDamage(sim, e, burn.power * burn.t, "frost", towerUid);
                    addStatus(e, "brittle", 3, Elements.STATUS_RULES.brittleBonus, towerUid);
                }
            }
            else if (type == "lightning")
            {
                if (hasStatus(e, "wet") && reactionReady(sim, e, "overload")) @out *= Elements.STATUS_RULES.overloadMul;
            }
            else if (type == "sonic")
            {
                if (hasStatus(e, "freeze") && reactionReady(sim, e, "shatter"))
                {
                    removeStatus(e, "freeze");
                    addStatus(e, "freezeImmune", Elements.STATUS_RULES.freezeImmuneDur, 1, 0);
                    @out = @out * Elements.STATUS_RULES.shatterMul + e.maxHp * Elements.STATUS_RULES.shatterMaxHpPct;
                }
            }
            return @out;
        }

        /// <summary>Vergeht Zeit für Statuseffekte; verursacht Schaden über Zeit.</summary>
        public static void tickStatuses(GameSimulation sim, EnemyInstance e, double dt)
        {
            // PORT: Index-Schleife über die erfasste Liste = JS for…of (sieht Änderungen der Liste während der Schleife).
            var statuses = e.statuses;
            for (int i = 0; i < statuses.Count; i++)
            {
                var st = statuses[i];
                if (!e.alive) return;
                if (st.id == "burn") DamageSystem.dealDamage(sim, e, st.power * dt, "fire", st.src);
                else if (st.id == "poison") DamageSystem.dealDamage(sim, e, st.power * st.stacks * dt, "poison", st.src);
                else if (st.id == "shred") DamageSystem.dealDamage(sim, e, st.power * st.stacks * dt, "physical", st.src);
                st.t -= dt;
            }
            if (!e.alive) return;
            for (int i = e.statuses.Count - 1; i >= 0; i--)
            {
                var st = e.statuses[i];
                if (st.t > 0) continue;
                e.statuses.RemoveAt(i);
                if (st.id == "freeze") addStatus(e, "freezeImmune", Elements.STATUS_RULES.freezeImmuneDur, 1, 0);
                if (st.id == "riftMark") onRiftMarkExpired(sim, e, st);
            }
        }

        private static void onRiftMarkExpired(GameSimulation sim, EnemyInstance e, StatusInstance st)
        {
            EnemySystem.riftTeleport(sim, e, st.power);
        }
    }
}
