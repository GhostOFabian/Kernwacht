// Übersetzung von sim/EnemySystem.ts – Gegner erzeugen, bewegen, Fähigkeiten, Tarnung, Durchbruch.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class EnemySystem
    {
        private static double Sq(double x) { return x * x; }

        private static Dictionary<string, double> zeroByType()
        {
            var r = new Dictionary<string, double>();
            foreach (var t in Types.DAMAGE_TYPES) r[t] = 0;
            return r;
        }

        /// <summary>Erzeugt einen Gegner mit wellenabhängigen Werten.</summary>
        public static EnemyInstance makeEnemy(GameSimulation sim, string id, bool elite, string boss, string mutation = null)
        {
            var s = sim.s;
            var wave = s.currentWave;
            var diff = Balance.DIFFICULTY[s.difficulty];
            var def = Enemies.ENEMIES[id];
            double hp;
            double speed;
            double reward;
            double leak;
            double poise = 0;
            double shield = 0;
            if (U.Truthy(boss))
            {
                var b = Enemies.BOSSES[boss];
                hp = b.hp * Balance.bossHpMul(s.wave, s.rules.endless) * diff.hpMul * sim.c.mods.bossHpMul;
                speed = b.speed;
                reward = b.reward * (wave != null ? wave.rewardMul : 1);
                leak = b.leak * (s.rules.endless && s.wave >= Scaling.SCALING.pressureFrom ? Scaling.SCALING.bossLeakMul : 1);
                poise = b.poise;
                shield = hp * b.shieldPct;
            }
            else
            {
                hp = def.hp * (wave != null ? wave.hpMul : 1) * diff.hpMul * (elite ? Enemies.ELITE.hpMul : 1);
                speed = def.speed * (elite ? 0.9 : 1);
                reward = def.reward * (wave != null ? wave.rewardMul : 1) * (elite ? Enemies.ELITE.rewardMul : 1);
                leak = def.leak * (elite ? Enemies.ELITE.leakMul : 1);
                shield = hp * def.shieldPct;
            }
            // 3.12: Flieger profitieren nur halb von Tempo-Zuschlägen (Weltstufe, „Flink“, Wellenprofil) und haben eine Obergrenze
            double sm = wave != null ? wave.speedMul : 1;
            if (!U.Truthy(boss) && def.flying) speed = U.Min(Scaling.FLYER.maxSpeed, speed * (1 + (sm - 1) * Scaling.FLYER.speedMulShare));
            // 4.0: Bosse erhalten Tempo-Zuschläge (Weltstufe, „Flink“, Wellenprofil) nur bis +20 %
            else if (U.Truthy(boss)) speed *= U.Min(sm, 1.2);
            else speed *= sm;
            if (mutation == "hasty") speed *= 1.35;
            var abilities = new List<string>(def.abilities);
            if (elite && !def.flying && !abilities.Contains(Enemies.ELITE.ability)) abilities.Add(Enemies.ELITE.ability);
            var starts = WorldGenerator.spawnsOf(s.map);
            double spawnIndex = U.Truthy(boss) ? 0 : (s.nextUid % starts.Count);
            var sp = starts[(int)spawnIndex];
            double plateCount = U.Truthy(boss) ? (Enemies.BOSSES[boss].weakPoints ?? 0) : 0;
            double plateMax = U.Truthy(plateCount) ? hp * 0.12 : 0;
            // Reihenfolge wie im TS-Objektliteral: uid() vor den rand()-Aufrufen der Fähigkeiten.
            double uid = sim.uid();
            var abilityStates = new List<AbilityState>();
            foreach (var a in abilities) abilityStates.Add(new AbilityState { id = a, cd = 2 + sim.rand() * 3, windup = 0, tx = 0, ty = 0, r = 0 });
            var e = new EnemyInstance
            {
                uid = uid, def = U.Truthy(boss) ? "boss" : id, boss = boss, elite = elite,
                x = sp.x + 0.5, y = sp.y + 0.5, nx = sp.x, ny = sp.y,
                hp = hp, maxHp = hp, shield = shield, maxShield = shield, speed = speed,
                flying = U.Truthy(boss) ? false : def.flying, route = def.route,
                traveled = 0, remaining = Pathfinding.INF, statuses = new List<StatusInstance>(),
                abilities = abilityStates,
                poise = poise, maxPoise = poise, phase = 0, phaseShift = 0,
                adapt = zeroByType(), adaptHits = zeroByType(),
                reward = reward, leak = leak, alive = true, rewarded = false, riftCd = 0, knockCd = 0, reactCds = new Dictionary<string, double>(), abilityMul = 1,
                wave = s.wave, spawnedAt = s.time, hitFlash = 0, reshieldUsed = false,
                mutation = mutation, stealthed = def.stealth == true && !U.Truthy(boss), burrowed = false, surfaced = false, spawnIndex = spawnIndex,
                plates = U.Fill<double>(plateCount, plateMax), plateMax = plateMax, swallowed = new List<TowerInstance>(), blockedBy = 0, enraged = false,
            };
            // 3.0: Weltstufe und Wellen-Affixe
            if (wave != null && U.Truthy(wave.tier)) e.tier = wave.tier;
            if (wave != null && wave.affixes != null && wave.affixes.Count > 0)
            {
                e.affixes = wave.affixes;
                if (U.Truthy(wave.immune)) e.immune = wave.immune;
                if (wave.affixes.Contains("shielded"))
                {
                    e.shield += e.maxHp * 0.25;
                    e.maxShield = e.shield;
                }
                if (wave.affixes.Contains("jammer") && elite) e.jamCd = 4 + sim.rand() * 4;
            }
            if (U.Truthy(boss)) BossSystem.initBossAbilities(sim, e);
            return e;
        }

        public static EnemyInstance spawnEnemy(GameSimulation sim, string id, bool elite, string boss, string mutation = null)
        {
            var e = makeEnemy(sim, id, elite, boss, mutation);
            sim.s.enemies.Add(e);
            sim.c.enemyByUid[e.uid] = e;
            if (U.Truthy(boss))
            {
                var partner = Enemies.BOSSES[boss].partner;
                if (U.Truthy(partner) && !sim.s.enemies.Any((o) => o.alive && o.boss == partner))
                {
                    var p = makeEnemy(sim, "boss", false, partner);
                    p.x -= 0.4;
                    sim.s.enemies.Add(p);
                    sim.c.enemyByUid[p.uid] = p;
                    if (!sim.s.stats.bossesFaced.Contains(partner)) sim.s.stats.bossesFaced.Add(partner);
                }
                sim.s.boss = new BossState { uid = e.uid, id = boss, phase = 0, phaseName = Enemies.BOSSES[boss].phases[0].name };
                if (!sim.s.stats.bossesFaced.Contains(boss)) sim.s.stats.bossesFaced.Add(boss);
                sim.emit(new GameEvent { k = "bossPhase", boss = boss, phase = 0, name = Enemies.BOSSES[boss].phases[0].name });
                sim.emit(new GameEvent { k = "message", text = $"{Enemies.BOSSES[boss].name} erscheint!", severity = "danger" });
            }
            return e;
        }

        /// <summary>Erzeugt einen Gegner an der Position eines anderen (Brut, Beschwörung).</summary>
        public static EnemyInstance spawnEnemyAt(GameSimulation sim, string id, bool elite, EnemyInstance at)
        {
            var e = makeEnemy(sim, id, elite, null);
            e.x = at.x;
            e.y = at.y;
            e.nx = U.Truthy(at.boss) || at.flying ? Math.Floor(at.x) : at.nx;
            e.ny = U.Truthy(at.boss) || at.flying ? Math.Floor(at.y) : at.ny;
            e.traveled = at.traveled;
            e.remaining = at.remaining;
            if (e.flying)
            {
                e.nx = Math.Floor(e.x);
                e.ny = Math.Floor(e.y);
            }
            sim.s.enemies.Add(e);
            sim.c.enemyByUid[e.uid] = e;
            return e;
        }

        public static GridPos enemyCell(EnemyInstance e)
        {
            return new GridPos { x = Math.Floor(e.x), y = Math.Floor(e.y) };
        }

        /// <summary>Unterbricht Fähigkeiten. Bosse verlieren Standfestigkeit; bei 0 werden sie taumelnd unterbrochen.</summary>
        public static void interruptEnemy(GameSimulation sim, EnemyInstance e, double power)
        {
            if (!e.alive || power <= 0) return;
            if (U.Truthy(e.boss))
            {
                e.poise -= power;
                if (e.poise <= 0)
                {
                    e.poise = e.maxPoise;
                    bool canceled0 = false;
                    foreach (var a in e.abilities)
                    {
                        if (a.windup > 0 && Enemies.ABILITIES[a.id].interruptible)
                        {
                            a.windup = 0;
                            a.cd = Enemies.ABILITIES[a.id].cooldown * 0.6;
                            canceled0 = true;
                            sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "interrupt" });
                        }
                    }
                    ElementSystem.addStatus(e, "stun", 1.5, 1, 0);
                    sim.s.zones = sim.s.zones.Where((z) => !(z.src == e.uid && z.armed > 0 && z.kind != "fireZone")).ToList();
                    sim.emit(new GameEvent { k = "message", text = canceled0 ? "Bossfähigkeit unterbrochen! Boss taumelt." : "Standfestigkeit gebrochen – Boss taumelt!", severity = "info" });
                }
                return;
            }
            bool canceled = false;
            foreach (var a in e.abilities)
            {
                if (a.windup > 0 && Enemies.ABILITIES[a.id].interruptible)
                {
                    a.windup = 0;
                    a.cd = Enemies.ABILITIES[a.id].cooldown;
                    canceled = true;
                    sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "interrupt" });
                }
            }
            if (canceled) sim.s.zones = sim.s.zones.Where((z) => !(z.src == e.uid && z.armed > 0)).ToList();
        }

        /// <summary>Versetzt einen Gegner entlang seiner Route zurück (Rissbinder, Rückstoß).</summary>
        public static void riftTeleport(GameSimulation sim, EnemyInstance e, double cells)
        {
            if (!e.alive || U.Truthy(e.boss) || cells <= 0) return;
            double ox = e.x;
            double oy = e.y;
            var map = sim.s.map;
            {
                // Auch Flieger werden entlang ihres Weges zurückversetzt.
                var field = sim.c.field;
                double cx = e.nx;
                double cy = e.ny;
                double moved = 0;
                for (int i = 0; i < U.Round(cells); i++)
                {
                    var p = Pathfinding.previousCell(map, field, cx, cy);
                    if (p == null) break;
                    cx = p.x;
                    cy = p.y;
                    moved++;
                }
                if (moved == 0) return;
                e.x = cx + 0.5;
                e.y = cy + 0.5;
                e.nx = cx;
                e.ny = cy;
                e.traveled = U.Max(0, e.traveled - moved);
            }
            sim.emit(new GameEvent { k = "teleport", x = ox, y = oy, x2 = e.x, y2 = e.y });
        }

        public static void updateEnemies(GameSimulation sim, double dt)
        {
            var s = sim.s;
            // Index-Schleife: Während des Updates können neue Gegner (Brut) entstehen.
            int count = s.enemies.Count;
            for (int i = 0; i < count; i++)
            {
                var e = s.enemies[i];
                if (!e.alive) continue;
                e.hitFlash = U.Max(0, e.hitFlash - dt);
                ElementSystem.tickStatuses(sim, e, dt);
                if (!e.alive) continue;
                // Regeneration – unterbunden durch Gift oder Brand
                var def = Enemies.ENEMIES[e.def];
                double regen = def.regenPct + (e.mutation == "vampiric" ? 0.025 : 0) + ((e.affixes != null && e.affixes.Contains("regen")) ? 0.01 : 0);
                if (e.challenge == true) ChallengeSystem.updateChallenge(sim, e, dt);
                // Affix „Störsender“: Elitegegner betäuben regelmäßig Türme in der Nähe
                if (e.jamCd.HasValue)
                {
                    e.jamCd -= dt;
                    if (e.jamCd <= 0)
                    {
                        e.jamCd = 8;
                        double hit = 0;
                        foreach (var t in s.towers)
                        {
                            if (Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= 1.5 * 1.5)
                            {
                                if (TowerSystem.disableTower(sim, t, 1, e)) hit++;
                            }
                        }
                        if (U.Truthy(hit)) sim.emit(new GameEvent { k = "burst", x = e.x, y = e.y, r = 1.5 });
                    }
                }
                if (regen > 0 && !ElementSystem.hasStatus(e, "poison") && !ElementSystem.hasStatus(e, "burn") && !ElementSystem.hasStatus(e, "shred") && !ElementSystem.hasStatus(e, "sealed"))
                {
                    e.hp = U.Min(e.maxHp, e.hp + e.maxHp * regen * dt);
                }
                updateConcealment(sim, e);
                if (e.phaseShift > 0) e.phaseShift = U.Max(0, e.phaseShift - dt);
                if (U.Truthy(e.boss) && e.poise < e.maxPoise && !e.abilities.Any((a) => a.windup > 0))
                {
                    e.poise = U.Min(e.maxPoise, e.poise + 6 * dt);
                }
                if (!U.Truthy(e.boss)) updateEnemyAbilities(sim, e, dt);
                moveEnemy(sim, e, dt);
                if (s.phase == "defeat") return;
            }
        }

        private static void updateEnemyAbilities(GameSimulation sim, EnemyInstance e, double dt)
        {
            foreach (var a in e.abilities)
            {
                var def = Enemies.ABILITIES[a.id];
                if (a.windup > 0)
                {
                    if (!ElementSystem.canAct(e))
                    {
                        a.windup = 0;
                        a.cd = def.cooldown * 0.5;
                        continue;
                    }
                    a.windup -= dt;
                    if (a.windup <= 0)
                    {
                        a.windup = 0;
                        executeEnemyAbility(sim, e, a.id, a.tx, a.ty, a.r);
                        a.cd = def.cooldown;
                    }
                    continue;
                }
                a.cd -= dt;
                if (a.cd > 0 || !ElementSystem.canAct(e)) continue;
                // Bedingungen prüfen und ggf. Wirken beginnen (telegraphiert über windup)
                if (a.id == "phaseShift")
                {
                    a.windup = def.windup;
                    a.tx = e.x;
                    a.ty = e.y;
                }
                else if (a.id == "netBite")
                {
                    double best = 0;
                    double bd = 2.0 * 2.0;
                    foreach (var t in sim.s.towers)
                    {
                        if (!sim.c.linked.ContainsKey(t.uid) || t.disrupted > 0) continue;
                        double d = Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y);
                        if (d <= bd)
                        {
                            bd = d;
                            best = t.uid;
                        }
                    }
                    if (U.Truthy(best))
                    {
                        var t = U.Get(sim.c.towerByUid, best);
                        if (t != null)
                        {
                            a.windup = def.windup;
                            a.tx = t.cx + 0.5;
                            a.ty = t.cy + 0.5;
                            a.r = best;
                            sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "warn" });
                        }
                    }
                    else a.cd = 0.5;
                }
                else if (a.id == "siegeSlam")
                {
                    double best = 0;
                    double bd = 1.9 * 1.9;
                    foreach (var t0 in sim.s.towers)
                    {
                        if (t0.stunned > 0 || Towers.TOWERS[t0.def].attack == "none") continue;
                        double d = Sq(t0.cx + 0.5 - e.x) + Sq(t0.cy + 0.5 - e.y);
                        if (d <= bd)
                        {
                            bd = d;
                            best = t0.uid;
                        }
                    }
                    var t = U.Truthy(best) ? U.Get(sim.c.towerByUid, best) : null;
                    if (t != null)
                    {
                        a.windup = def.windup;
                        a.tx = t.cx + 0.5;
                        a.ty = t.cy + 0.5;
                        a.r = best;
                        sim.s.zones.Add(new Zone { uid = sim.uid(), kind = "siegeSlam", x = a.tx, y = a.ty, r = 0.7, t = 0.3, armed = def.windup, power = 4, tick = 0, link = 0, src = e.uid });
                        sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "warn" });
                    }
                    else a.cd = 0.5;
                }
                else if (a.id == "heal")
                {
                    bool hurt = sim.s.enemies.Any((o) => o.alive && o != e && o.hp < o.maxHp * 0.85 && Sq(o.x - e.x) + Sq(o.y - e.y) <= 6.25);
                    if (hurt)
                    {
                        a.windup = def.windup;
                        a.tx = e.x;
                        a.ty = e.y;
                        a.r = 2.5;
                        sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "warn" });
                    }
                    else a.cd = 0.8;
                }
                else if (a.id == "quake")
                {
                    double n = 0;
                    foreach (var t in sim.s.towers) if (Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= 2.2 * 2.2) n++;
                    if (n >= 3)
                    {
                        a.windup = def.windup;
                        a.tx = e.x;
                        a.ty = e.y;
                        a.r = 2.2;
                        sim.s.zones.Add(new Zone { uid = sim.uid(), kind = "quake", x = e.x, y = e.y, r = 2.2, t = 0.4, armed = def.windup, power = 3, tick = 0, link = 0, src = e.uid });
                        sim.emit(new GameEvent { k = "ability", id = a.id, x = e.x, y = e.y, stage = "warn" });
                    }
                    else a.cd = 0.5;
                }
            }
        }

        private static void executeEnemyAbility(GameSimulation sim, EnemyInstance e, string id, double tx, double ty, double r)
        {
            sim.emit(new GameEvent { k = "ability", id = id, x = e.x, y = e.y, stage = "cast" });
            if (id == "phaseShift")
            {
                e.phaseShift = 2;
            }
            else if (id == "netBite")
            {
                var t = U.Get(sim.c.towerByUid, r);
                if (t != null && Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= 2.6 * 2.6)
                {
                    t.disrupted = 6;
                    sim.c.networkDirty = true;
                    sim.emit(new GameEvent { k = "message", text = "Ein Funkenfresser stört dein Netzwerk!", severity = "warn" });
                }
            }
            else if (id == "quake")
            {
                foreach (var t in sim.s.towers)
                {
                    if (Sq(t.cx + 0.5 - tx) + Sq(t.cy + 0.5 - ty) <= r * r) TowerSystem.disableTower(sim, t, 3, e);
                }
            }
            else if (id == "siegeSlam")
            {
                var t = U.Get(sim.c.towerByUid, r);
                if (t != null && Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= 2.4 * 2.4)
                {
                    if (TowerSystem.disableTower(sim, t, 4, e)) sim.emit(new GameEvent { k = "burst", x = t.cx + 0.5, y = t.cy + 0.5, r = 0.8 });
                }
            }
            else if (id == "heal")
            {
                foreach (var o in sim.s.enemies)
                {
                    if (!o.alive || Sq(o.x - e.x) + Sq(o.y - e.y) > r * r || ElementSystem.hasStatus(o, "sealed")) continue;
                    o.hp = U.Min(o.maxHp, o.hp + o.maxHp * (U.Truthy(o.boss) ? 0.03 : 0.12));
                }
                sim.emit(new GameEvent { k = "heal", x = e.x, y = e.y, r = r });
            }
        }

        // Tarnung und Eingraben: Detektoren (und Aufdeckung) machen Gegner sichtbar und angreifbar.
        /// <summary>Getarnte Gegner werden von normalen Türmen nur aus nächster Nähe erkannt.</summary>
        private const double CLOSE_DETECT = 1.6;
        private const double HERO_DETECT = 1.8;
        /// <summary>Strecke (Felder), die ein Tunnelgräber unter der Erde zurücklegt, bevor er auftaucht.</summary>
        private const double BURROW_DISTANCE = 7;

        private static void updateConcealment(GameSimulation sim, EnemyInstance e)
        {
            var def = Enemies.ENEMIES[e.def];
            if (def.stealth != true && def.burrow != true) return;
            bool detected = ElementSystem.hasStatus(e, "reveal");
            if (!detected)
            {
                foreach (var t in sim.s.towers)
                {
                    if (t.stunned > 0 || Towers.TOWERS[t.def].attack == "none") continue;
                    // Detektoren decken in voller Reichweite auf, jeder andere Turm nur aus nächster Nähe.
                    var st = U.Get(sim.c.towerStats, t.uid);
                    double r = Towers.TOWERS[t.def].detector == true ? (st != null ? st["range"] : 2.5) + 0.2 : CLOSE_DETECT;
                    if (Sq(t.cx + 0.5 - e.x) + Sq(t.cy + 0.5 - e.y) <= r * r)
                    {
                        detected = true;
                        break;
                    }
                }
            }
            if (!detected)
            {
                foreach (var h in sim.s.heroes)
                {
                    if (h.state == "active" && Sq(h.x - e.x) + Sq(h.y - e.y) <= HERO_DETECT * HERO_DETECT)
                    {
                        detected = true;
                        break;
                    }
                }
            }
            if (def.stealth == true) e.stealthed = !detected;
            if (def.burrow == true)
            {
                if (!e.burrowed && !e.surfaced && e.traveled > 1.5 && !detected)
                {
                    e.burrowed = true;
                    sim.emit(new GameEvent { k = "burrow", x = e.x, y = e.y, up = false });
                }
                else if (e.burrowed && (detected || e.remaining <= 5 || e.traveled > 1.5 + BURROW_DISTANCE))
                {
                    e.burrowed = false;
                    e.surfaced = true;
                    sim.emit(new GameEvent { k = "burrow", x = e.x, y = e.y, up = true });
                }
            }
        }

        /// <summary>Kontaktschaden von Gegnern gegen einen Helden (pro Sekunde).</summary>
        public static double enemyContactDps(EnemyInstance e)
        {
            if (U.Truthy(e.boss)) return 60;
            return Enemies.contactDpsOf(Enemies.ENEMIES[e.def]) * (e.elite ? 2.5 : 1);
        }

        private static void moveEnemy(GameSimulation sim, EnemyInstance e, double dt)
        {
            var s = sim.s;
            var map = s.map;
            double speed = e.speed * (1 - ElementSystem.slowFactor(e));
            if (U.Truthy(e.boss)) speed *= Enemies.BOSSES[e.boss].phases[(int)U.Min(e.phase, Enemies.BOSSES[e.boss].phases.Count - 1)].speedMul;
            if (ElementSystem.isImmobile(e)) speed = 0;
            if (U.Truthy(e.blockedBy)) speed = 0;
            if (e.burrowed) speed *= 1.3;
            if (e.enraged) speed *= 1.3;
            foreach (var a in e.abilities)
            {
                if (a.windup > 0) speed *= U.Truthy(e.boss) ? 0.5 : a.id == "quake" ? 0 : 1;
            }
            double step = speed * dt;
            // Flieger folgen demselben Weg wie Bodentruppen (nur in der Luft schwebend dargestellt).
            var field = e.route == "safe" ? sim.c.safeField : sim.c.field;
            int guard = 0;
            while (step > 1e-9 && guard++ < 8)
            {
                double tx = e.nx + 0.5;
                double ty = e.ny + 0.5;
                double dx = tx - e.x;
                double dy = ty - e.y;
                double d = U.Hypot(dx, dy);
                if (d > step)
                {
                    e.x += (dx / d) * step;
                    e.y += (dy / d) * step;
                    e.traveled += step;
                    step = 0;
                    break;
                }
                e.x = tx;
                e.y = ty;
                e.traveled += d;
                step -= d;
                if (e.nx == map.goal.x && e.ny == map.goal.y)
                {
                    leak(sim, e);
                    return;
                }
                var n = Pathfinding.nextCell(map, field, e.nx, e.ny);
                if (n == null && field != sim.c.field) n = Pathfinding.nextCell(map, sim.c.field, e.nx, e.ny);
                if (n == null)
                {
                    // Sollte durch die Bauprüfung ausgeschlossen sein – sicherer Rückfall: direkt zum Ziel.
                    n = new GridPos { x = e.nx + U.Sign(map.goal.x - e.nx), y = e.ny + U.Sign(map.goal.y - e.ny) };
                }
                e.nx = n.x;
                e.ny = n.y;
            }
            double f = sim.c.field[(int)(e.ny * map.width + e.nx)];
            e.remaining = (f < Pathfinding.INF ? f : 0) + U.Hypot(e.nx + 0.5 - e.x, e.ny + 0.5 - e.y);
        }

        private static void leak(GameSimulation sim, EnemyInstance e)
        {
            var s = sim.s;
            e.alive = false;
            e.rewarded = true; // kein Kopfgeld nach Durchbruch
            double loss = U.Max(1, U.Round(e.leak));
            s.lives = U.Max(0, s.lives - loss);
            s.stats.leaks++;
            s.stats.livesLost += loss;
            sim.emit(new GameEvent { k = "leak", enemy = e.def, lives = loss });
            if (U.Truthy(e.boss)) sim.emit(new GameEvent { k = "message", text = $"{Enemies.BOSSES[e.boss].name} hat den Kern erreicht!", severity = "danger" });
            if (s.lives <= 0 && s.phase != "defeat" && s.phase != "victory")
            {
                s.phase = "defeat";
                sim.emit(new GameEvent { k = "defeat" });
            }
        }
    }
}
