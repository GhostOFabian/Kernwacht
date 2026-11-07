// Übersetzung von sim/HeroSystem.ts.
// Heldensystem. Helden werden mit Rissenergie auf begehbaren Wegzellen eingesetzt, kämpfen
// eine begrenzte Zeit und brauchen danach eine Abklingzeit. Unterstützer verstärken Türme über
// Auren, die – wie alle Turmwerte – nur bei Änderung neu berechnet werden (Signatur-Vergleich).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class HeroAura
    {
        public double dmg;
        public double speed;
        public double range;
    }

    /// <summary>TS-Rückgabe von abilityReady: <c>{ ok: boolean; reason: string | null }</c>.</summary>
    public class HeroAbilityReady
    {
        public bool ok;
        public string reason;
    }

    public static partial class HeroSystem
    {
        /// <summary>
        /// JS <c>a ** b</c>. PORT: V8 (fdlibm) liefert für Exponent 2 exakt x*x – hier ebenso erzwungen.
        /// </summary>
        private static double Pow(double b, double e)
        {
            if (e == 2) return b * b;
            return Math.Pow(b, e);
        }

        public static HeroStats heroStatsOf(GameSimulation sim, HeroInstance h)
        {
            HeroStats st = Heroes.heroStats(Heroes.HEROES[h.id], h.perks);
            if (duoActive(sim, "emberley") && h.id == "hunter") st.damage *= 1.5;
            if (duoActive(sim, "goldenbard") && h.id == "prospector") st.goldBonus += 0.5;
            return st;
        }

        private static bool active(HeroInstance h)
        {
            return h.state == "active";
        }

        public static bool duoActive(GameSimulation sim, string id)
        {
            var d = Heroes.DUOS.FirstOrDefault((x) => x.id == id);
            if (d == null) return false;
            HeroInstance a = sim.s.heroes.FirstOrDefault((h) => h.id == d.a && active(h));
            HeroInstance b = sim.s.heroes.FirstOrDefault((h) => h.id == d.b && active(h));
            if (a == null || b == null) return false;
            return Pow(a.x - b.x, 2) + Pow(a.y - b.y, 2) <= Pow(Heroes.HERO_RULES.duoRadius, 2);
        }

        public static List<string> activeDuos(GameSimulation sim)
        {
            return Heroes.DUOS.Where((d) => duoActive(sim, d.id)).Select((d) => d.id).ToList();
        }

        private static bool visible(EnemyInstance e)
        {
            return e.alive && !e.stealthed && !e.burrowed;
        }

        private static double dist2(double ax, double ay, double bx, double by)
        {
            return Pow(ax - bx, 2) + Pow(ay - by, 2);
        }

        // ------------------------------------------------------------ Befehle

        public static CommandResult deployHero(GameSimulation sim, string id, double cx, double cy)
        {
            GameState s = sim.s;
            HeroInstance h = s.heroes.FirstOrDefault((x) => x.id == id);
            if (h == null) return CommandResult.Fail("Held nicht in der Auswahl");
            if (s.phase != "build" && s.phase != "combat") return CommandResult.Fail("Jetzt nicht möglich");
            if (h.state == "active") return CommandResult.Fail("Held ist bereits im Einsatz");
            if (h.state != "ready") return CommandResult.Fail($"Abklingzeit: noch {U.Fmt(Math.Ceiling(h.cooldown))} s");
            HeroDefinition def = Heroes.HEROES[id];
            if (s.riftEnergy < def.cost) return CommandResult.Fail($"Zu wenig Rissenergie ({U.Fmt(def.cost)} benötigt)");
            CommandResult chk = canPlaceHero(sim, cx, cy);
            if (!chk.ok) return chk;
            HeroStats st = heroStatsOf(sim, h);
            s.riftEnergy -= def.cost;
            h.state = "active";
            h.mode = "hold";
            h.x = cx + 0.5;
            h.y = cy + 0.5;
            h.ax = h.x;
            h.ay = h.y;
            h.hp = st.maxHp;
            h.activeLeft = st.activeTime;
            h.target = 0;
            h.buffT = 0;
            h.buffKind = "";
            s.stats.heroDeploys++;
            sim.c.statsDirty = true;
            sim.emit(new GameEvent { k = "heroDeploy", hero = id, x = h.x, y = h.y });
            return CommandResult.Ok();
        }

        public static CommandResult canPlaceHero(GameSimulation sim, double cx, double cy)
        {
            MapDefinition map = sim.s.map;
            if (cx < 0 || cy < 0 || cx >= map.width || cy >= map.height) return CommandResult.Fail("Außerhalb der Karte");
            double i = cy * map.width + cx;
            if (map.cells[(int)i] == Cell.Goal) return CommandResult.Fail("Nicht direkt am Kern");
            if (!Pathfinding.isWalkableCell(map, i) || sim.c.towerByCell.ContainsKey(i)) return CommandResult.Fail("Helden werden auf dem Weg eingesetzt");
            return CommandResult.Ok();
        }

        public static CommandResult setHeroMode(GameSimulation sim, string id, string mode)
        {
            HeroInstance h = sim.s.heroes.FirstOrDefault((x) => x.id == id);
            if (h == null || !active(h)) return CommandResult.Fail("Held ist nicht im Einsatz");
            h.mode = mode;
            return CommandResult.Ok();
        }

        public static CommandResult moveHeroAnchor(GameSimulation sim, string id, double cx, double cy)
        {
            HeroInstance h = sim.s.heroes.FirstOrDefault((x) => x.id == id);
            if (h == null || !active(h)) return CommandResult.Fail("Held ist nicht im Einsatz");
            CommandResult chk = canPlaceHero(sim, cx, cy);
            if (!chk.ok) return chk;
            h.ax = cx + 0.5;
            h.ay = cy + 0.5;
            if (h.mode == "retreat") h.mode = "hold";
            return CommandResult.Ok();
        }

        public static CommandResult chooseHeroPerk(GameSimulation sim, string id, double choice)
        {
            HeroInstance h = sim.s.heroes.FirstOrDefault((x) => x.id == id);
            if (h == null || !h.pendingPerk) return CommandResult.Fail("Keine Auswahl offen");
            HeroStats before = heroStatsOf(sim, h);
            h.perks.Add(choice);
            h.pendingPerk = h.perks.Count < h.level - 1;
            HeroStats after = heroStatsOf(sim, h);
            if (after.maxHp > before.maxHp) h.hp += after.maxHp - before.maxHp;
            h.activeLeft += Math.Max(0, after.activeTime - before.activeTime);
            sim.c.statsDirty = true;
            return CommandResult.Ok();
        }

        public static HeroAbilityReady abilityReady(GameSimulation sim, HeroInstance h, double slot)
        {
            if (!active(h)) return new HeroAbilityReady { ok = false, reason = "Nicht im Einsatz" };
            if (sim.s.phase != "combat") return new HeroAbilityReady { ok = false, reason = "Nur während einer Welle" };
            if (slot == 2) return h.ult >= 100 ? new HeroAbilityReady { ok = true, reason = null } : new HeroAbilityReady { ok = false, reason = $"Ultimate lädt ({U.Fmt(Math.Floor(h.ult))} %)" };
            if (h.abilityCd[(int)slot] > 0) return new HeroAbilityReady { ok = false, reason = $"Bereit in {U.Fmt(Math.Ceiling(h.abilityCd[(int)slot]))} s" };
            return new HeroAbilityReady { ok = true, reason = null };
        }

        public static CommandResult useHeroAbility(GameSimulation sim, string id, double slot)
        {
            HeroInstance h = sim.s.heroes.FirstOrDefault((x) => x.id == id);
            if (h == null) return CommandResult.Fail("Held nicht gefunden");
            HeroAbilityReady r = abilityReady(sim, h, slot);
            if (!r.ok) return CommandResult.Fail(r.reason ?? "Nicht bereit");
            HeroDefinition def = Heroes.HEROES[h.id];
            HeroStats st = heroStatsOf(sim, h);
            HeroAbilityDefinition ab = slot == 2 ? def.ultimate : def.abilities[(int)slot];
            CommandResult done = executeAbility(sim, h, ab.id, st);
            if (!done.ok) return done;
            if (slot == 2) h.ult = 0;
            else h.abilityCd[(int)slot] = ab.cooldown * st.abilityCdMul;
            h.anim = 0.6;
            return CommandResult.Ok();
        }

        // ------------------------------------------------------------ Wirtschaft & Erfahrung

        /// <summary>Goldfaktor eines Abschusses durch Heldeneffekte (Prospektorin, Goldfieber).</summary>
        public static double heroGoldMul(GameSimulation sim, EnemyInstance e)
        {
            double mul = 1;
            foreach (HeroInstance h in sim.s.heroes)
            {
                if (h.id != "prospector" || !active(h)) continue;
                HeroStats st = heroStatsOf(sim, h);
                if (dist2(h.x, h.y, e.x, e.y) <= Pow(st.auraRadius, 2)) mul += st.goldBonus;
            }
            if (sim.s.buffs.Any((b) => b.id == "goldFever")) mul *= 2;
            return mul;
        }

        public static void addEnergy(GameSimulation sim, double amount)
        {
            if (sim.s.heroes.Count == 0) return;
            sim.s.riftEnergy = Math.Min(Heroes.HERO_RULES.maxEnergy, sim.s.riftEnergy + amount);
        }

        public static void onEnemyKilled(GameSimulation sim, EnemyInstance e, double killerUid)
        {
            GameState s = sim.s;
            if (s.heroes.Count == 0) return;
            addEnergy(sim, U.Truthy(e.boss) ? Heroes.HERO_RULES.energyPerBoss : e.elite ? Heroes.HERO_RULES.energyPerElite : Heroes.HERO_RULES.energyPerKill);
            double xp = U.Truthy(e.boss) ? 120 : Math.Max(2, e.reward * 1.5);
            foreach (HeroInstance h in s.heroes)
            {
                if (!active(h)) continue;
                bool killer = h.uid == killerUid;
                if (killer)
                {
                    h.kills++;
                    s.stats.heroKills++;
                    h.ult = Math.Min(100, h.ult + Heroes.HERO_RULES.ultPerKill * ultMul(sim, h));
                }
                if (killer || dist2(h.x, h.y, e.x, e.y) <= Pow(Heroes.HERO_RULES.xpRadius, 2)) grantXp(sim, h, killer ? xp : xp * 0.6);
            }
        }

        private static double ultMul(GameSimulation sim, HeroInstance h)
        {
            return (h.id == "riftwalker" || h.id == "silence") && duoActive(sim, "voidpact") ? 2 : 1;
        }

        private static void grantXp(GameSimulation sim, HeroInstance h, double amount)
        {
            if (h.level >= Heroes.HERO_RULES.maxLevel) return;
            h.xp += amount;
            while (h.level < Heroes.HERO_RULES.maxLevel && h.xp >= Heroes.HERO_RULES.xpPerLevel[(int)h.level])
            {
                h.level++;
                h.pendingPerk = true;
                sim.emit(new GameEvent { k = "heroLevel", hero = h.id, level = h.level });
                sim.emit(new GameEvent { k = "message", text = $"{Heroes.HEROES[h.id].name} erreicht Stufe {U.Fmt(h.level)} – wähle eine Verbesserung!", severity = "info" });
            }
        }

        /// <summary>Wellenende: Energie und verkürzte Abklingzeiten.</summary>
        public static void onWaveCleared(GameSimulation sim)
        {
            addEnergy(sim, Heroes.HERO_RULES.energyPerWave);
            foreach (HeroInstance h in sim.s.heroes)
            {
                if (h.state == "cooldown" || h.state == "down")
                {
                    h.cooldown = Math.Max(0, h.cooldown - 10);
                    if (h.cooldown <= 0) h.state = "ready";
                }
            }
        }

        // ------------------------------------------------------------ Takt

        public static void updateHeroes(GameSimulation sim, double dt)
        {
            GameState s = sim.s;
            // 4.1: globale Puffer (z. B. Netzflut) laufen auch ohne Lane-Helden ab – im Helden-Endlos blieben sie sonst dauerhaft
            foreach (GlobalBuff b in s.buffs) b.t -= dt;
            if (s.buffs.Any((b) => b.t <= 0))
            {
                s.buffs = s.buffs.Where((b) => b.t > 0).ToList();
                sim.c.statsDirty = true;
            }
            if (s.heroes.Count == 0) return;
            bool combat = s.phase == "combat";
            foreach (EnemyInstance e in s.enemies) e.blockedBy = 0;
            foreach (HeroInstance h in s.heroes)
            {
                if (h.state == "cooldown" || h.state == "down")
                {
                    if (combat) h.cooldown -= dt;
                    if (h.cooldown <= 0)
                    {
                        h.cooldown = 0;
                        h.state = "ready";
                    }
                    continue;
                }
                if (!active(h)) continue;
                HeroStats st = heroStatsOf(sim, h);
                h.anim = Math.Max(0, h.anim - dt);
                if (combat)
                {
                    h.activeLeft -= dt;
                    h.ult = Math.Min(100, h.ult + Heroes.HERO_RULES.ultPerSecond * dt * ultMul(sim, h) * (Heroes.HEROES[h.id].support ? 1.6 : 1));
                    for (int k = 0; k < 2; k++) h.abilityCd[k] = Math.Max(0, h.abilityCd[k] - dt);
                    if (h.buffT > 0)
                    {
                        h.buffT -= dt;
                        if (h.buffT <= 0) h.buffKind = "";
                    }
                }
                if (h.activeLeft <= 0)
                {
                    retire(sim, h, Heroes.HERO_RULES.cooldown);
                    continue;
                }
                moveHero(sim, h, st, dt);
                if (!active(h)) continue;
                if (combat)
                {
                    blockAndContact(sim, h, st, dt);
                    if (!active(h)) continue;
                    attack(sim, h, st, dt);
                    passive(sim, h, st, dt);
                }
            }
            sim.c.heroAuraTimer -= dt;
            if (sim.c.heroAuraTimer <= 0)
            {
                sim.c.heroAuraTimer = 0.25;
                rebuildAuras(sim);
            }
        }

        private static void retire(GameSimulation sim, HeroInstance h, double cooldown)
        {
            h.state = "cooldown";
            h.cooldown = cooldown * sim.c.mods.heroCooldownMul;
            h.target = 0;
            h.buffKind = "";
            h.buffT = 0;
            sim.c.statsDirty = true;
        }

        private static void moveHero(GameSimulation sim, HeroInstance h, HeroStats st, double dt)
        {
            GameState s = sim.s;
            double tx = h.ax;
            double ty = h.ay;
            if (h.mode == "retreat")
            {
                tx = s.map.goal.x + 0.5;
                ty = s.map.goal.y + 0.5;
                if (dist2(h.x, h.y, tx, ty) < 0.3)
                {
                    retire(sim, h, Heroes.HERO_RULES.cooldown * (1 - Heroes.HERO_RULES.retreatRefund));
                    return;
                }
            }
            else if (h.mode == "advance" && s.phase == "combat")
            {
                EnemyInstance best = null;
                double bd = 9 * 9;
                foreach (EnemyInstance e in s.enemies)
                {
                    if (!visible(e) || e.flying) continue;
                    double d0 = dist2(h.ax, h.ay, e.x, e.y);
                    if (d0 < bd)
                    {
                        bd = d0;
                        best = e;
                    }
                }
                if (best != null)
                {
                    tx = best.x;
                    ty = best.y;
                    double stop = Math.Max(0.55, Math.Min(st.range, 3) - 0.2);
                    if (dist2(h.x, h.y, tx, ty) <= stop * stop) return;
                }
            }
            double dx = tx - h.x;
            double dy = ty - h.y;
            double d = U.Hypot(dx, dy);
            if (d < 0.02) return;
            double step = Math.Min(d, st.speed * dt * (h.blocking > 0 ? 0 : 1));
            h.x += (dx / d) * step;
            h.y += (dy / d) * step;
            // PORT: Math.atan2 – V8 (fdlibm) und .NET können im letzten Bit abweichen (nur Blickrichtung/Darstellung).
            h.facing = Math.Atan2(dy, dx);
        }

        private static void blockAndContact(GameSimulation sim, HeroInstance h, HeroStats st, double dt)
        {
            GameState s = sim.s;
            double blocked = 0;
            double incoming = 0;
            foreach (EnemyInstance e in s.enemies)
            {
                if (!visible(e) || e.flying) continue;
                double d = dist2(h.x, h.y, e.x, e.y);
                if (d > 0.7 * 0.7) continue;
                if (!U.Truthy(e.boss) && blocked < st.blockCount && e.blockedBy == 0)
                {
                    e.blockedBy = h.uid;
                    blocked++;
                }
                incoming += EnemySystem.enemyContactDps(e);
            }
            h.blocking = blocked;
            if (h.buffKind == "bulwark") return;
            double mul = 1 - Math.Min(0.8, st.armor);
            if (h.id == "blade" && duoActive(sim, "ironwatch")) mul *= 0.5;
            if (incoming > 0) h.hp -= incoming * mul * dt;
            else h.hp = Math.Min(st.maxHp, h.hp + st.maxHp * Heroes.HERO_RULES.regenPct * dt);
            if (h.hp <= 0)
            {
                h.hp = 0;
                h.state = "down";
                h.cooldown = Heroes.HERO_RULES.downCooldown * sim.c.mods.heroCooldownMul;
                h.target = 0;
                h.buffKind = "";
                s.stats.heroDowns++;
                sim.c.statsDirty = true;
                foreach (EnemyInstance e in s.enemies) if (e.blockedBy == h.uid) e.blockedBy = 0;
                sim.emit(new GameEvent { k = "heroDown", hero = h.id, x = h.x, y = h.y });
                sim.emit(new GameEvent { k = "message", text = $"{Heroes.HEROES[h.id].name} ist gefallen! ({U.Fmt(Heroes.HERO_RULES.downCooldown)} s Abklingzeit)", severity = "danger" });
                if (s.rules.heroMustSurvive == h.id && s.phase != "victory")
                {
                    s.phase = "defeat";
                    sim.emit(new GameEvent { k = "message", text = "Mission gescheitert: der zu schützende Held ist gefallen.", severity = "danger" });
                    sim.emit(new GameEvent { k = "defeat" });
                }
            }
        }

        private static HitPayload heroPayload(HeroInstance h, HeroStats st, double splash)
        {
            HeroDefinition def = Heroes.HEROES[h.id];
            Dictionary<string, double> stats = Stats.zeroStats();
            stats["damage"] = st.damage;
            stats["hitsAir"] = st.range > 1.5 ? 1 : 0;
            if (h.id == "hunter")
            {
                stats["burnDps"] = 6 + st.damage * 0.2;
                stats["burnDur"] = 3;
            }
            else if (h.id == "weather")
            {
                stats["chillPct"] = 0.15;
                stats["chillDur"] = 2;
                stats["chillMax"] = 0.45;
            }
            else if (h.id == "bard" || h.id == "silence")
            {
                stats["interrupt"] = 10;
            }
            return new HitPayload { damage = st.damage, type = def.damageType, splash = splash, stats = stats, tower = h.uid, towerDef = "ranger" };
        }

        private static void attack(GameSimulation sim, HeroInstance h, HeroStats st, double dt)
        {
            h.attackCd = Math.Max(0, h.attackCd - dt);
            if (h.attackCd > 0 || st.damage <= 0) return;
            GameState s = sim.s;
            EnemyInstance target = U.Truthy(h.target) ? U.Get(sim.c.enemyByUid, h.target) : null;
            double r = st.range + 0.1;
            bool canAir = st.range > 1.5;
            if (target == null || !visible(target) || (target.flying && !canAir) || dist2(h.x, h.y, target.x, target.y) > r * r)
            {
                target = null;
                double bd = r * r;
                foreach (EnemyInstance e in s.enemies)
                {
                    if (!visible(e) || (e.flying && !canAir)) continue;
                    double d = dist2(h.x, h.y, e.x, e.y);
                    // Blockierte bzw. dem Kern nahe Gegner bevorzugen
                    double score = d - (e.blockedBy == h.uid ? 2 : 0) + e.remaining * 0.01;
                    if (d <= r * r && score < bd)
                    {
                        bd = score;
                        target = e;
                    }
                }
                h.target = target != null ? target.uid : 0;
            }
            if (target == null) return;
            double splash = h.buffKind == "flame" || (h.id == "hunter" && duoActive(sim, "emberley")) ? 1 : 0;
            HitPayload p = heroPayload(h, st, splash);
            if (splash > 0)
            {
                // PORT: for..of über s.enemies – applyHit kann Gegner anhängen (Splitter); JS iteriert diese mit → Index-Schleife.
                for (int i = 0; i < s.enemies.Count; i++)
                {
                    EnemyInstance e = s.enemies[i];
                    if (visible(e) && dist2(e.x, e.y, target.x, target.y) <= splash * splash) DamageSystem.applyHit(sim, e, p, e == target ? 1 : 0.7);
                }
            }
            else DamageSystem.applyHit(sim, target, p);
            h.attackCd = st.attackCd;
            h.facing = Math.Atan2(target.y - h.y, target.x - h.x);
            h.ult = Math.Min(100, h.ult + Heroes.HERO_RULES.ultPerHit * ultMul(sim, h));
            h.anim = Math.Max(h.anim, 0.25);
            sim.emit(new GameEvent { k = "heroAttack", hero = h.id, x = h.x, y = h.y, tx = target.x, ty = target.y, melee = st.range <= 1.5 });
        }

        /// <summary>Dauerhafte Wirkungen: Reparatur, Stille-Aura.</summary>
        private static void passive(GameSimulation sim, HeroInstance h, HeroStats st, double dt)
        {
            GameState s = sim.s;
            if (h.buffKind == "nulled") return;
            if (h.id == "smith")
            {
                bool changed = false;
                foreach (TowerInstance t in s.towers)
                {
                    if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) > Pow(st.auraRadius, 2)) continue;
                    if (t.stunned > 0)
                    {
                        t.stunned = Math.Max(0, t.stunned - dt * 3);
                    }
                    if (t.disrupted > 0)
                    {
                        t.disrupted = 0;
                        changed = true;
                    }
                }
                if (changed) sim.c.networkDirty = true;
            }
            else if (h.id == "silence")
            {
                for (int i = 0; i < s.enemies.Count; i++)
                {
                    EnemyInstance e = s.enemies[i];
                    if (!e.alive || dist2(e.x, e.y, h.x, h.y) > Pow(st.auraRadius, 2)) continue;
                    ElementSystem.addStatus(e, "reveal", 0.5, 1, h.uid);
                    if (U.Truthy(e.boss)) EnemySystem.interruptEnemy(sim, e, st.auraPower * dt);
                    else ElementSystem.addStatus(e, "silence", 0.5, 1, h.uid);
                }
            }
        }

        /// <summary>Schadensbonus der Resonanz „Heldenchor“.</summary>
        public const double HEROIC_CHOIR = 0.2;

        private static void rebuildAuras(GameSimulation sim)
        {
            GameState s = sim.s;
            Dictionary<double, HeroAura> map = new Dictionary<double, HeroAura>();
            bool stormsong = duoActive(sim, "stormsong");
            bool ironwatch = duoActive(sim, "ironwatch");
            foreach (HeroInstance h in s.heroes)
            {
                if (!active(h) || h.buffKind == "nulled") continue;
                HeroStats st = heroStatsOf(sim, h);
                foreach (TowerInstance t in s.towers)
                {
                    double d2 = dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y);
                    HeroAura a = null;
                    Func<HeroAura> get = () =>
                    {
                        a = U.Get(map, t.uid);
                        if (a == null)
                        {
                            a = new HeroAura { dmg = 0, speed = 0, range = 0 };
                            map[t.uid] = a;
                        }
                        return a;
                    };
                    if (h.id == "bard" && d2 <= Pow(st.auraRadius, 2)) get().speed += st.auraPower;
                    if (h.id == "mystic" && d2 <= Pow(st.auraRadius, 2))
                    {
                        HeroAura x = get();
                        x.dmg += st.auraPower;
                        x.speed += st.auraPower * 0.5;
                    }
                    if (h.id == "blade" && ironwatch && d2 <= Pow(2.5, 2)) get().dmg += 0.15;
                }
            }
            // Heldenchor: Die Bardin in Reichweite eines Netzes mit Feuer, Frost und Blitz verstärkt das ganze Netz.
            HeroInstance bard = s.heroes.FirstOrDefault((h) => h.id == "bard" && active(h) && h.buffKind != "nulled");
            if (bard != null)
            {
                double r2 = Pow(heroStatsOf(sim, bard).auraRadius, 2);
                foreach (NetworkGroup g in s.network.groups)
                {
                    List<TowerInstance> members = g.members.Select((uid) => U.Get(sim.c.towerByUid, uid)).Where((t) => t != null).ToList();
                    HashSet<string> types = new HashSet<string>(members.Select((t) => Towers.TOWERS[t.def].damageType));
                    if (!types.Contains("fire") || !types.Contains("frost") || !types.Contains("lightning")) continue;
                    if (!members.Any((t) => dist2(t.cx + 0.5, t.cy + 0.5, bard.x, bard.y) <= r2)) continue;
                    foreach (TowerInstance t in members)
                    {
                        HeroAura a = U.Get(map, t.uid);
                        if (a == null)
                        {
                            a = new HeroAura { dmg = 0, speed = 0, range = 0 };
                            map[t.uid] = a;
                        }
                        a.dmg += HEROIC_CHOIR;
                    }
                }
            }
            if (stormsong)
            {
                foreach (TowerInstance t in s.towers)
                {
                    if (Towers.TOWERS[t.def].damageType != "lightning") continue;
                    HeroAura a = U.Get(map, t.uid);
                    if (a == null)
                    {
                        a = new HeroAura { dmg = 0, speed = 0, range = 0 };
                        map[t.uid] = a;
                    }
                    a.dmg += 0.4;
                }
            }
            List<KeyValuePair<double, HeroAura>> entries = U.Entries(map);
            U.StableSort(entries, (x, y) => x.Key - y.Key);
            string sig = U.Join(entries.Select((kv) => $"{U.Fmt(kv.Key)}:{U.Fixed(kv.Value.dmg, 2)}/{U.Fixed(kv.Value.speed, 2)}"), ",");
            if (sig != sim.c.heroAuraSig)
            {
                sim.c.heroAura = map;
                sim.c.heroAuraSig = sig;
                sim.c.statsDirty = true;
            }
        }

        // ------------------------------------------------------------ Fähigkeiten

        private static void addBuff(GameSimulation sim, string id, double t, double power, double src)
        {
            GameState s = sim.s;
            GlobalBuff cur = s.buffs.FirstOrDefault((b) => b.id == id);
            if (cur != null) cur.t = Math.Max(cur.t, t);
            else s.buffs.Add(new GlobalBuff { id = id, t = t, power = power, src = src });
            sim.c.statsDirty = true;
        }

        private static List<EnemyInstance> enemiesWithin(GameSimulation sim, double x, double y, double r)
        {
            return sim.s.enemies.Where((e) => visible(e) && dist2(e.x, e.y, x, y) <= r * r).ToList();
        }

        private static CommandResult executeAbility(GameSimulation sim, HeroInstance h, string id, HeroStats st)
        {
            GameState s = sim.s;
            HeroDefinition def = Heroes.HEROES[h.id];
            double scale = (st.damage / Math.Max(1, def.@base.damage)) * (1 + 0.1 * (h.level - 1));
            // TS: Standardwerte x = h.x, y = h.y werden beim Aufruf ausgewertet.
            Action<double, double?, double?> emitA = (r, ex, ey) =>
            {
                double x = ex ?? h.x;
                double y = ey ?? h.y;
                sim.emit(new GameEvent { k = "heroAbility", hero = h.id, ability = id, x = x, y = y, r = r });
            };
            switch (id)
            {
                // Klingenwächterin
                case "taunt":
                    foreach (EnemyInstance e in enemiesWithin(sim, h.x, h.y, 3)) if (!e.flying && !U.Truthy(e.boss)) ElementSystem.addStatus(e, "root", 1.5, 1, h.uid);
                    emitA(3, null, null);
                    break;
                case "whirl":
                    foreach (EnemyInstance e in enemiesWithin(sim, h.x, h.y, 1.6)) DamageSystem.dealDamage(sim, e, 70 * scale, "physical", h.uid);
                    emitA(1.6, null, null);
                    break;
                case "bulwark":
                    h.buffKind = "bulwark";
                    h.buffT = 6;
                    foreach (EnemyInstance e in enemiesWithin(sim, h.x, h.y, 3))
                    {
                        if (U.Truthy(e.boss)) EnemySystem.interruptEnemy(sim, e, 30);
                        else if (!e.flying) ElementSystem.addStatus(e, "root", 4, 1, h.uid);
                    }
                    emitA(3, null, null);
                    break;
                // Aschenjäger
                case "flameArrows":
                    h.buffKind = "flame";
                    h.buffT = 6;
                    emitA(1, null, null);
                    break;
                case "trap":
                    {
                        EnemyInstance tg = U.Get(sim.c.enemyByUid, h.target);
                        double x = tg != null ? tg.x : h.x;
                        double y = tg != null ? tg.y : h.y;
                        s.zones.Add(new Zone { uid = sim.uid(), kind = "burnTrap", x = x, y = y, r = 1.2, t = 6, armed = 0, power = 12 * scale, tick = 0, link = 0, src = h.uid });
                        emitA(1.2, x, y);
                        break;
                    }
                case "firerain":
                    {
                        List<EnemyInstance> tg = enemiesWithin(sim, h.x, h.y, st.range + 2).Where((e) => !e.flying).ToList();
                        if (tg.Count == 0) return CommandResult.Fail("Keine Ziele in Reichweite");
                        for (int i = 0; i < 8; i++)
                        {
                            EnemyInstance e = tg[i % tg.Count];
                            // Reihenfolge wie im TS-Objektliteral: uid, dann x (rand), dann y (rand)
                            double zuid = sim.uid();
                            double zx = e.x + (sim.rand() - 0.5) * 0.8;
                            double zy = e.y + (sim.rand() - 0.5) * 0.8;
                            s.zones.Add(new Zone { uid = zuid, kind = "meteor", x = zx, y = zy, r = 1.0, t = 0.1, armed = 0.3 + i * 0.2, power = 80 * scale, tick = 0, link = 0, src = h.uid });
                        }
                        emitA(st.range + 2, null, null);
                        break;
                    }
                // Risswandler
                case "riftstep":
                    {
                        EnemyInstance best = null;
                        foreach (EnemyInstance e in s.enemies) if (visible(e) && !e.flying && (best == null || e.remaining < best.remaining)) best = e;
                        if (best == null) return CommandResult.Fail("Kein Ziel");
                        double ox = h.x;
                        double oy = h.y;
                        h.x = best.x - 0.3;
                        h.y = best.y;
                        h.ax = h.x;
                        h.ay = h.y;
                        DamageSystem.dealDamage(sim, best, 90 * scale, "arcane", h.uid);
                        h.target = best.uid;
                        sim.emit(new GameEvent { k = "teleport", x = ox, y = oy, x2 = h.x, y2 = h.y });
                        emitA(0.8, null, null);
                        break;
                    }
                case "voidmark":
                    foreach (EnemyInstance e in enemiesWithin(sim, h.x, h.y, 2)) ElementSystem.addStatus(e, "voidMark", 5, 0.3, h.uid);
                    emitA(2, null, null);
                    break;
                case "execute":
                    {
                        List<EnemyInstance> sorted = s.enemies.Where(visible).ToList();
                        U.StableSort(sorted, (a, b) => a.remaining - b.remaining);
                        List<EnemyInstance> list = U.Slice(sorted, 0, 5);
                        if (list.Count == 0) return CommandResult.Fail("Keine Ziele");
                        foreach (EnemyInstance e in list)
                        {
                            DamageSystem.dealDamage(sim, e, e.maxHp * (U.Truthy(e.boss) ? 0.06 : 0.3), "arcane", h.uid);
                            sim.emit(new GameEvent { k = "heroAbility", hero = h.id, ability = "executeHit", x = e.x, y = e.y, r = 0.6 });
                        }
                        emitA(1, null, null);
                        break;
                    }
                // Runenbardin
                case "resoSong":
                    addBuff(sim, "resoSong", 8, 1, h.uid);
                    emitA(4, null, null);
                    break;
                case "inspire":
                    foreach (TowerInstance t in s.towers) if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) <= 9) t.forged = Math.Max(t.forged, 6);
                    sim.c.statsDirty = true;
                    emitA(3, null, null);
                    break;
                case "hymn":
                    addBuff(sim, "hymn", 8, 0.5, h.uid);
                    emitA(8, null, null);
                    break;
                // Schmiedemeister
                case "forge":
                    {
                        TowerInstance best = null;
                        double bd = double.PositiveInfinity;
                        foreach (TowerInstance t in s.towers)
                        {
                            if (Towers.TOWERS[t.def].attack == "none" || Towers.TOWERS[t.def].attack == "economy") continue;
                            double d = dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y);
                            if (d < bd)
                            {
                                bd = d;
                                best = t;
                            }
                        }
                        if (best == null || bd > 16) return CommandResult.Fail("Kein Turm in der Nähe");
                        best.forged = Math.Max(best.forged, 20);
                        sim.c.statsDirty = true;
                        emitA(0.8, best.cx + 0.5, best.cy + 0.5);
                        break;
                    }
                case "repairWave":
                    {
                        foreach (TowerInstance t in s.towers)
                        {
                            if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) > 25) continue;
                            t.stunned = 0;
                            t.disrupted = 0;
                        }
                        sim.c.networkDirty = true;
                        emitA(5, null, null);
                        break;
                    }
                case "masterwork":
                    foreach (TowerInstance t in s.towers) if (dist2(t.cx + 0.5, t.cy + 0.5, h.x, h.y) <= 16) t.forged = Math.Max(t.forged, 15);
                    sim.c.statsDirty = true;
                    emitA(4, null, null);
                    break;
                // Wettermacherin
                case "rain":
                    foreach (EnemyInstance e in s.enemies) if (e.alive) ElementSystem.addStatus(e, "wet", 6, 1, h.uid);
                    emitA(12, null, null);
                    break;
                case "frostBreath":
                    foreach (EnemyInstance e in enemiesWithin(sim, h.x, h.y, 4))
                    {
                        StatusInstance c = ElementSystem.addStatus(e, "chill", 4, 0.22, h.uid, 3);
                        c.stacks = 3;
                        c.flag = Math.Max(c.flag, 60);
                    }
                    emitA(4, null, null);
                    break;
                case "thunderstorm":
                    {
                        List<EnemyInstance> list = s.enemies.Where(visible).ToList();
                        if (list.Count == 0) return CommandResult.Fail("Keine Ziele");
                        foreach (EnemyInstance e in list) ElementSystem.addStatus(e, "wet", 6, 1, h.uid);
                        HitPayload p = heroPayload(h, st, 0);
                        p.type = "lightning";
                        p.damage = 70 * scale;
                        Dictionary<string, double> ns = new Dictionary<string, double>(p.stats);
                        ns["chillPct"] = 0;
                        ns["damage"] = p.damage;
                        p.stats = ns;
                        for (int i = 0; i < 12; i++)
                        {
                            EnemyInstance e = list[(int)Math.Floor(sim.rand() * list.Count)];
                            if (!e.alive) continue;
                            DamageSystem.applyHit(sim, e, p);
                            sim.emit(new GameEvent { k = "heroAbility", hero = h.id, ability = "strike", x = e.x, y = e.y, r = 0.5 });
                        }
                        emitA(10, null, null);
                        break;
                    }
                // Leylinien-Mystiker
                case "leyNode":
                    s.zones.Add(new Zone { uid = sim.uid(), kind = "leyNode", x = h.x, y = h.y, r = 2, t = 30, armed = 0.01, power = 0.15, tick = 0, link = 0, src = h.uid });
                    emitA(2, null, null);
                    break;
                case "leyBeam":
                    {
                        EnemyInstance best = null;
                        double bd = 36;
                        foreach (EnemyInstance e in s.enemies)
                        {
                            if (!visible(e)) continue;
                            double d = dist2(h.x, h.y, e.x, e.y);
                            if (d < bd)
                            {
                                bd = d;
                                best = e;
                            }
                        }
                        if (best == null) return CommandResult.Fail("Kein Ziel in Reichweite");
                        double len = U.Or(Math.Sqrt(bd), 1);
                        double ux = (best.x - h.x) / len;
                        double uy = (best.y - h.y) / len;
                        // PORT: for..of über s.enemies – dealDamage kann Gegner anhängen; JS iteriert diese mit → Index-Schleife.
                        for (int i = 0; i < s.enemies.Count; i++)
                        {
                            EnemyInstance e = s.enemies[i];
                            if (!visible(e)) continue;
                            double rx = e.x - h.x;
                            double ry = e.y - h.y;
                            double along = rx * ux + ry * uy;
                            if (along < 0 || along > 6) continue;
                            if (Math.Abs(rx * uy - ry * ux) <= 0.45) DamageSystem.dealDamage(sim, e, 140 * scale, "arcane", h.uid);
                        }
                        sim.emit(new GameEvent { k = "laser", x = h.x, y = h.y, tx = h.x + ux * 6, ty = h.y + uy * 6, power = 1, uid = h.uid });
                        emitA(0.6, null, null);
                        break;
                    }
                case "worldweb":
                    addBuff(sim, "worldweb", 10, 0.25, h.uid);
                    emitA(8, null, null);
                    break;
                // Prospektorin
                case "treasure":
                    {
                        if (h.treasureWave == s.wave) return CommandResult.Fail("In dieser Welle schon gefunden");
                        h.treasureWave = s.wave;
                        double g = 40 + 4 * s.wave;
                        s.gold += g;
                        s.stats.goldEarned += g;
                        sim.emit(new GameEvent { k = "gold", amount = g, x = h.x, y = h.y });
                        emitA(0.8, null, null);
                        break;
                    }
                case "goldFever":
                    addBuff(sim, "goldFever", 8, 1, h.uid);
                    emitA(3, null, null);
                    break;
                case "motherlode":
                    {
                        double g = 120 + 8 * s.wave;
                        s.gold += g;
                        s.stats.goldEarned += g;
                        sim.emit(new GameEvent { k = "gold", amount = g, x = h.x, y = h.y });
                        emitA(2, null, null);
                        break;
                    }
                // Wächter der Stille
                case "silenceBan":
                    {
                        bool done = false;
                        foreach (EnemyInstance e in s.enemies)
                        {
                            if (!e.alive || !U.Truthy(e.boss) || dist2(e.x, e.y, h.x, h.y) > 36) continue;
                            foreach (AbilityState a in e.abilities)
                            {
                                if (a.windup > 0)
                                {
                                    a.windup = 0;
                                    a.cd = 6;
                                    done = true;
                                    sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "interrupt" });
                                }
                            }
                            if (done) s.zones = s.zones.Where((z) => !(z.src == e.uid && z.armed > 0)).ToList();
                        }
                        if (!done) return CommandResult.Fail("Keine angekündigte Bossfähigkeit in Reichweite");
                        emitA(6, null, null);
                        break;
                    }
                case "unveil":
                    foreach (EnemyInstance e in s.enemies) if (e.alive && dist2(e.x, e.y, h.x, h.y) <= 25) ElementSystem.addStatus(e, "reveal", 8, 1, h.uid);
                    emitA(5, null, null);
                    break;
                case "absoluteSilence":
                    // PORT: for..of über s.enemies – interruptEnemy könnte Gegner anhängen → Index-Schleife.
                    for (int i = 0; i < s.enemies.Count; i++)
                    {
                        EnemyInstance e = s.enemies[i];
                        if (!e.alive) continue;
                        if (U.Truthy(e.boss)) EnemySystem.interruptEnemy(sim, e, e.maxPoise + 1);
                        else ElementSystem.addStatus(e, "silence", 6, 1, h.uid);
                    }
                    emitA(12, null, null);
                    break;
                default:
                    return CommandResult.Fail("Unbekannte Fähigkeit");
            }
            return CommandResult.Ok();
        }

        /// <summary>Schaden an Helden durch Bosszonen (Feuerzone, Stampfer) bzw. Nullpuls (Auren aus).</summary>
        public static void zoneHitsHeroes(GameSimulation sim, double x, double y, double r, double damage, bool nulls)
        {
            foreach (HeroInstance h in sim.s.heroes)
            {
                if (!active(h) || dist2(h.x, h.y, x, y) > r * r) continue;
                if (nulls)
                {
                    h.buffKind = "nulled";
                    h.buffT = 6;
                    sim.c.statsDirty = true;
                    continue;
                }
                if (h.buffKind == "bulwark") continue;
                h.hp = Math.Max(1, h.hp - damage);
            }
        }
    }
}
