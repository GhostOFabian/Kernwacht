// Übersetzung von sim/DamageSystem.ts.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class DamageSystem
    {
        /// <summary>JS <c>x ** 2</c> (V8/fdlibm: Exponent 2 liefert exakt x*x).</summary>
        private static double Sq(double x) { return x * x; }

        /// <summary>Effektive Resistenz eines Gegners gegen eine Schadensart (ohne Panzerbrechung).</summary>
        public static double resistOf(GameSimulation sim, EnemyInstance e, string type)
        {
            double @base = U.Truthy(e.boss) ? (U.GetN(Enemies.BOSSES[e.boss].resist, type) ?? 0) : (U.GetN(Enemies.ENEMIES[e.def].resist, type) ?? 0);
            double r = @base + (U.GetN(sim.s.rules.enemyResistBonus, type) ?? 0);
            if (e.elite) r += Enemies.ELITE.resistBonus;
            if (e.mutation == "armored") r += 0.25;
            // 3.0: Weltstufe (Rüstung), Affixe „Gepanzert“ und „Immunität“
            if (type == "physical")
            {
                if (U.Truthy(e.tier)) r += U.Min(Scaling.SCALING.tierArmorMax, Scaling.SCALING.tierArmor * e.tier.Value);
                if (e.affixes != null && e.affixes.Contains("armored")) r += 0.2;
            }
            if (e.immune == type) r = U.Max(r, 0.85);
            if (e.challenge == true) r += Scaling.CHALLENGE.resist;
            return r;
        }

        /// <summary>
        /// Wendet Schaden an. Reihenfolge: Resistenz (inkl. Panzerbrechung) → Markierung → Spröde/Fessel
        /// → Phasenschild → Schild (adaptiv bzw. Nullschild) → Lebenspunkte → ggf. Tod.
        /// </summary>
        /// <returns>tatsächlich verursachter Schaden (Schild + LP).</returns>
        public static double dealDamage(GameSimulation sim, EnemyInstance e, double amount, string type, double towerUid, double armorPierce = 0)
        {
            if (!e.alive || amount <= 0 || !U.IsFinite(amount)) return 0;
            double resist = resistOf(sim, e, type);
            if (type == "physical" && resist > 0) resist *= 1 - U.Min(1, armorPierce);
            var mark = ElementSystem.getStatus(e, "mark");
            // Markierung senkt nur vorhandene Resistenzen – höchstens bis 0, sie erzeugt keine zusätzliche Schwäche (2.9.5)
            if (mark != null && resist > 0) resist = U.Max(0, resist - U.Min(Elements.STATUS_RULES.markHardCap, mark.power));
            if (type == "physical")
            {
                var sh = ElementSystem.getStatus(e, "shred");
                if (sh != null && sh.flag > 0 && resist > Elements.STATUS_RULES.shredFloor) resist = U.Max(Elements.STATUS_RULES.shredFloor, resist - (sh.flag / 1000) * sh.stacks);
            }
            // 4.0 Zersetzung (Ätzkessel): senkt alle Resistenzen je Stapel, höchstens bis zur Untergrenze
            var co = ElementSystem.getStatus(e, "corrode");
            if (co != null && resist > Elements.STATUS_RULES.shredFloor) resist = U.Max(Elements.STATUS_RULES.shredFloor, resist - co.power * co.stacks);
            resist = U.Max(-1, U.Min(0.9, resist));
            double dmg = amount * (1 - resist);
            if (type == "physical" && ElementSystem.hasStatus(e, "brittle")) dmg *= 1 + Elements.STATUS_RULES.brittleBonus;
            var tether = ElementSystem.getStatus(e, "tether");
            if (tether != null) dmg *= 1 + tether.power;
            var vm = ElementSystem.getStatus(e, "voidMark");
            if (vm != null) dmg *= 1 + vm.power;
            dmg *= TacticalCharges.damageTakenMul(sim);
            if (e.phaseShift > 0 && type == "physical")
            {
                if (sim.s.tick % 6 == 0) sim.emit(new GameEvent { k = "immune", x = e.x, y = e.y });
                return 0;
            }
            double dealt = 0;
            // Schwachpunkte (Weltenbrecher): Schaden trifft zuerst den vordersten Schwachpunkt, der Kern nur 10 %.
            if (e.plates.Count > 0)
            {
                double toPlate = U.Min(e.plates[0], dmg);
                e.plates[0] -= dmg;
                dealt += toPlate;
                dmg *= 0.1;
                if (e.plates[0] <= 0)
                {
                    e.plates.RemoveAt(0);
                    sim.emit(new GameEvent { k = "plateBreak", x = e.x, y = e.y, left = e.plates.Count });
                    sim.emit(new GameEvent { k = "message", text = e.plates.Count > 0 ? $"Schwachpunkt zerstört! Noch {U.Fmt(e.plates.Count)}." : "Alle Schwachpunkte zerstört – der Kern liegt frei!", severity = "info" });
                    if (e.plates.Count == 0) BossSystem.onBossShieldBroken(sim, e);
                }
            }
            if (e.shield > 0)
            {
                double mul;
                if (e.boss == "nullWarden") mul = U.GetN(Enemies.NULL_SHIELD_MUL, type) ?? 0.5;
                else
                {
                    mul = 1 - 0.15 * e.adapt[type];
                    e.adaptHits[type]++;
                    if (e.adaptHits[type] >= 6 && e.adapt[type] < 3)
                    {
                        e.adaptHits[type] = 0;
                        e.adapt[type]++;
                    }
                }
                double eff = dmg * mul;
                double absorbed = U.Min(e.shield, eff);
                e.shield -= absorbed;
                dealt += absorbed;
                dmg = mul > 0 ? (eff - absorbed) / mul : 0;
                if (e.shield <= 0)
                {
                    e.shield = 0;
                    if (U.Truthy(e.boss)) BossSystem.onBossShieldBroken(sim, e);
                }
            }
            if (dmg > 0)
            {
                double hpDmg = U.Min(e.hp, dmg);
                e.hp -= dmg;
                dealt += hpDmg;
            }
            e.hitFlash = 0.12;
            if (U.Truthy(towerUid))
            {
                if (sim.c.towerByUid.TryGetValue(towerUid, out var t) && t != null)
                {
                    t.damageDealt += dealt;
                    sim.s.stats.damageByTower[t.def] = (U.GetN(sim.s.stats.damageByTower, t.def) ?? 0) + dealt;
                }
                else
                {
                    var h = sim.s.heroes.FirstOrDefault((x) => x.uid == towerUid);
                    if (h != null) h.damageDealt += dealt;
                }
            }
            if (e.hp <= 0) killEnemy(sim, e, towerUid);
            return dealt;
        }

        /// <summary>Höchstens so viel Prozent-LP-Zusatzschaden, als Vielfaches des normalen Treffers.</summary>
        public const double MAX_HP_PCT_CAP = 3;

        /// <summary>Vollständiger Treffer eines Turmangriffs inkl. Kritisch, Reaktionen und Statuseffekten.</summary>
        public static void applyHit(GameSimulation sim, EnemyInstance e, HitPayload p, double mult = 1)
        {
            if (!e.alive) return;
            var st = p.stats;
            double dmg = p.damage * mult;
            if (e.flying && st["airDmgMul"] != 1) dmg *= st["airDmgMul"];
            if (st["critChance"] > 0 && sim.rand() < st["critChance"]) dmg *= st["critMult"];
            // Kolossjäger: Zusatzschaden nach max. LP – gedeckelt auf das 3-Fache des eigenen Treffers,
            // damit er mit dem Turm wächst und nicht allein mit den immer zäheren Gegnern
            if (st["maxHpPct"] > 0) dmg += U.Min(e.maxHp * st["maxHpPct"] * (U.Truthy(e.boss) ? 0.25 : 1), dmg * MAX_HP_PCT_CAP);
            // 4.0 Hinrichtung: Bosse unter 30 % Leben erleiden Zusatzschaden
            if (st["executePct"] > 0 && U.Truthy(e.boss) && e.hp < e.maxHp * 0.3) dmg *= 1 + st["executePct"] * 4;
            if (dmg > 0)
            {
                dmg = ElementSystem.react(sim, e, p.type, dmg, p.tower);
                dealDamage(sim, e, dmg, p.type, p.tower, st["armorPierce"]);
            }
            if (!e.alive) return;
            // 4.0 Hinrichtung (Adlerhorst): Nicht-Bosse unter executePct Leben sterben sofort
            if (st["executePct"] > 0 && !U.Truthy(e.boss) && e.challenge != true && e.hp <= e.maxHp * st["executePct"])
            {
                sim.emit(new GameEvent { k = "execute", x = e.x, y = e.y });
                if (U.Truthy(p.tower) && sim.c.towerByUid.TryGetValue(p.tower, out var et) && et != null) et.damageDealt += U.Max(0, e.hp);
                killEnemy(sim, e, p.tower);
                return;
            }
            applyOnHitEffects(sim, e, p);
        }

        private static void applyOnHitEffects(GameSimulation sim, EnemyInstance e, HitPayload p)
        {
            var st = p.stats;
            bool boss = e.boss != null;
            if (st["burnDps"] > 0 && st["burnDur"] > 0) ElementSystem.addStatus(e, "burn", st["burnDur"], st["burnDps"], p.tower);
            if (st["chillPct"] > 0 && st["chillDur"] > 0)
            {
                var s = ElementSystem.addStatus(e, "chill", st["chillDur"], st["chillPct"], p.tower, 3);
                s.flag = U.Max(s.flag, U.Round(st["chillMax"] * 100));
            }
            if (st["freezeChance"] > 0 && sim.rand() < st["freezeChance"])
            {
                if (boss) EnemySystem.interruptEnemy(sim, e, 15);
                else if (!ElementSystem.hasStatus(e, "freezeImmune")) ElementSystem.addStatus(e, "freeze", st["freezeDur"], 1, p.tower);
            }
            if (st["poisonDps"] > 0 && st["poisonDur"] > 0)
            {
                var s = ElementSystem.addStatus(e, "poison", st["poisonDur"], st["poisonDps"], p.tower, U.Max(1, U.Round(st["poisonMaxStacks"])));
                if (st["poisonSlow"] > 0) s.flag = U.Max(s.flag, U.Round(st["poisonSlow"] * 100));
            }
            if (st["bleedDps"] > 0 && st["bleedDur"] > 0)
            {
                var s = ElementSystem.addStatus(e, "shred", st["bleedDur"], st["bleedDps"], p.tower, U.Max(1, U.Round(st["bleedMaxStacks"])));
                s.flag = U.Max(s.flag, U.Round(st["shredPct"] * 1000));
            }
            if (st["markPct"] > 0 && st["markDur"] > 0)
            {
                ElementSystem.addStatus(e, "mark", st["markDur"], st["markPct"], p.tower);
                if (st["markSpread"] > 0)
                {
                    var near = U.Slice(
                        sim.s.enemies.Where((o) => o.alive && o != e && Sq(o.x - e.x) + Sq(o.y - e.y) <= 2.25).ToList(),
                        0, U.Round(st["markSpread"]));
                    foreach (var o in near) ElementSystem.addStatus(o, "mark", st["markDur"] * 0.75, st["markPct"], p.tower);
                }
            }
            if (st["interrupt"] > 0) EnemySystem.interruptEnemy(sim, e, st["interrupt"]);
            if (st["silenceDur"] > 0 && !boss) ElementSystem.addStatus(e, "silence", st["silenceDur"], 1, p.tower);
            if (st["stunDur"] > 0)
            {
                if (boss) EnemySystem.interruptEnemy(sim, e, 8);
                else ElementSystem.addStatus(e, "stun", st["stunDur"], 1, p.tower);
            }
            if (st["brittleDur"] > 0) ElementSystem.addStatus(e, "brittle", st["brittleDur"], Elements.STATUS_RULES.brittleBonus, p.tower);
            if (st["knockback"] > 0 && !boss && e.knockCd <= sim.s.time)
            {
                // 4.0: Flieger fliegen weiter (Windwacht); Sperrzeit wächst mit der Weite, damit Rückstoß nicht dauerhaft festhält
                double cells = U.Round(st["knockback"] + (e.flying ? st["airKnock"] : 0));
                e.knockCd = sim.s.time + 1.5 + cells;
                EnemySystem.riftTeleport(sim, e, cells);
            }
            // 4.0 Zersetzung, Entgraben, Niederdrücken, Bann
            if (st["corrodePct"] > 0 && st["corrodeDur"] > 0) ElementSystem.addStatus(e, "corrode", st["corrodeDur"], st["corrodePct"], p.tower, U.Max(1, U.Round(st["corrodeMax"])));
            if (st["hitsBurrowed"] > 0 && e.burrowed)
            {
                e.burrowed = false;
                e.surfaced = true;
                sim.emit(new GameEvent { k = "burrow", x = e.x, y = e.y, up = true });
            }
            if (st["groundDur"] > 0 && e.flying) ElementSystem.addStatus(e, "grounded", st["groundDur"], 1, p.tower);
            if (st["sealDur"] > 0) ElementSystem.addStatus(e, "sealed", st["sealDur"], 1, p.tower);
        }

        /// <summary>Tod eines Gegners. Belohnungen werden pro Gegner garantiert nur einmal vergeben.</summary>
        public static void killEnemy(GameSimulation sim, EnemyInstance e, double towerUid)
        {
            if (e.rewarded) return;
            e.rewarded = true;
            e.alive = false;
            e.hp = 0;
            var s = sim.s;
            // Affix „Splitter“: zerfällt in zwei Schwarmlinge (diese splitten nicht weiter)
            if (e.affixes != null && e.affixes.Contains("splitter") && !U.Truthy(e.boss) && !U.Truthy(e.splitChild) && !U.Truthy(e.miniBoss))
            {
                for (int k = 0; k < 2; k++)
                {
                    var c = EnemySystem.spawnEnemyAt(sim, "swarm", false, e);
                    c.splitChild = true;
                    c.x += (k != 0 ? 0.15 : -0.15);
                }
            }
            double gold = U.Max(1, U.Round(e.reward * sim.c.mods.killGoldMul * HeroSystem.heroGoldMul(sim, e) * (1 + PassiveTree.runPassive(sim.s.he).killGoldPct)));
            // Kopfgeldbüro (Goldgräber-Spezialisierung)
            // PORT: Index-Schleifen über erfasste Listen = JS for…of (sieht angehängte Elemente mit).
            var towers = s.towers;
            for (int i = 0; i < towers.Count; i++)
            {
                var tw = towers[i];
                if (!TowersNew.BOUNTY.Contains(tw.def)) continue;
                var st = sim.stats(tw);
                if (st["killBounty"] <= 0) continue;
                double dx = tw.cx + 0.5 - e.x;
                double dy = tw.cy + 0.5 - e.y;
                if (dx * dx + dy * dy <= st["bountyRange"] * st["bountyRange"])
                {
                    double b = U.Round(st["killBounty"] + (e.elite || U.Truthy(e.boss) ? st["eliteBounty"] : 0));
                    gold += b;
                    tw.goldEarned += b;
                }
            }
            s.gold += gold;
            s.stats.goldEarned += gold;
            s.stats.kills++;
            TowerInstance t = null;
            if (U.Truthy(towerUid)) sim.c.towerByUid.TryGetValue(towerUid, out t);
            if (t != null)
            {
                t.kills++;
                if (s.stats.killsByTower == null) s.stats.killsByTower = new Dictionary<string, double>();
                s.stats.killsByTower[t.def] = (U.GetN(s.stats.killsByTower, t.def) ?? 0) + 1;
            }
            // Turm-Meisterschaft (Helden-Endlos): Rangaufstieg macht die Turmwerte neu
            if (t != null && s.he != null && s.he.mastery != null)
            {
                double before = Mastery.masteryRank(U.GetN(s.he.mastery, t.def) ?? 0);
                s.he.mastery[t.def] = (U.GetN(s.he.mastery, t.def) ?? 0) + 1;
                double after = Mastery.masteryRank(U.GetN(s.he.mastery, t.def) ?? 0);
                if (after > before)
                {
                    sim.c.statsDirty = true;
                    sim.emit(new GameEvent { k = "message", text = $"Meisterschaft {Towers.TOWERS[t.def].name}: Rang {U.Fmt(after)} (+{U.Fmt(after)} % Schaden)", severity = "info" });
                }
            }
            if (t != null && s.he != null && s.he.runes != null && s.he.runes.Count > 0)
            {
                double g = RuneSystem.runeKillGold(sim, t);
                if (g > 0)
                {
                    s.gold += g;
                    s.stats.goldEarned += g;
                    t.goldEarned += g;
                }
            }
            if (U.Truthy(e.boss))
            {
                s.stats.bossesKilled.Add(e.boss);
                sim.emit(new GameEvent { k = "message", text = $"{Enemies.BOSSES[e.boss].name} wurde besiegt! +{U.Fmt(gold)} Gold", severity = "info" });
            }
            sim.emit(new GameEvent { k = "kill", enemy = e.def, x = e.x, y = e.y, gold = gold, elite = e.elite, isBoss = e.boss != null });
            if (s.he != null) LootSystem.onEnemyLoot(sim, e);
            sim.emit(new GameEvent
            {
                k = "death", enemy = e.def, boss = e.boss, x = e.x, y = e.y,
                color = U.Truthy(e.boss) ? Enemies.BOSSES[e.boss].color : Enemies.ENEMIES[e.def].color,
                size = U.Truthy(e.boss) ? Enemies.BOSSES[e.boss].size * 2 : Enemies.ENEMIES[e.def].size * 2 * (e.elite ? 1.35 : 1),
            });
            HeroSystem.onEnemyKilled(sim, e, towerUid);
            if (U.Truthy(e.boss)) BossSystem.onBossDeath(sim, e);
            if (e.challenge == true)
            {
                sim.emit(new GameEvent { k = "message", text = "⚔ Challenge bestanden! 5× Kopfgeld, 3 Relikte und eine Rune.", severity = "info" });
                if (s.he != null)
                {
                    LootSystem.addResource(s.he, "relic", 3);
                    RuneSystem.dropRune(sim, e.x, e.y);
                }
            }
            if (U.Truthy(e.mutation))
            {
                s.stats.mutationsKilled++;
                if (e.mutation == "explosive")
                {
                    var towers2 = s.towers;
                    for (int i = 0; i < towers2.Count; i++)
                    {
                        var tw = towers2[i];
                        if (Towers.TOWERS[tw.def].attack == "none") continue;
                        if (Sq(tw.cx + 0.5 - e.x) + Sq(tw.cy + 0.5 - e.y) <= 1.6 * 1.6) TowerSystem.disableTower(sim, tw, 2, null);
                    }
                    sim.emit(new GameEvent { k = "burst", x = e.x, y = e.y, r = 1.6 });
                }
                else if (e.mutation == "splitting" && !U.Truthy(e.boss))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var c = EnemySystem.spawnEnemyAt(sim, e.def, false, e);
                        c.hp = e.maxHp * 0.3;
                        c.maxHp = c.hp;
                        c.reward = U.Max(1, e.reward * 0.15);
                        c.x += (i - 0.5) * 0.25;
                    }
                }
            }

            // Schwelbrand: Brand springt beim Tod über (nicht weiter übertragbar → keine Ketten).
            var burn = ElementSystem.getStatus(e, "burn");
            if (burn != null && burn.flag == 0)
            {
                sim.c.towerByUid.TryGetValue(burn.src, out var src);
                if (src != null && src.def == "ember" && sim.stats(src)["burnSpread"] > 0)
                {
                    int n = 0;
                    var enemies = s.enemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        var o = enemies[i];
                        if (n >= 3) break;
                        if (!o.alive || o == e) continue;
                        if (Sq(o.x - e.x) + Sq(o.y - e.y) > 1.44) continue;
                        ElementSystem.addStatus(o, "burn", U.Max(1, burn.t * 0.5 + 1), burn.power, burn.src, 1, 1);
                        n++;
                    }
                }
            }
            // 4.0 Pestkessel: Gift und Zersetzung springen beim Tod über (Kopien springen nicht weiter)
            var cz = ElementSystem.getStatus(e, "corrode");
            if (cz != null && cz.flag == 0)
            {
                sim.c.towerByUid.TryGetValue(cz.src, out var ps);
                double spread = ps != null ? U.Round(sim.stats(ps)["contagion"]) : 0;
                if (spread > 0)
                {
                    var pz = ElementSystem.getStatus(e, "poison");
                    var pst = sim.stats(ps);
                    int n = 0;
                    var enemies = s.enemies;
                    for (int i = 0; i < enemies.Count && n < spread; i++)
                    {
                        var o = enemies[i];
                        if (!o.alive || o == e || Sq(o.x - e.x) + Sq(o.y - e.y) > 2.25) continue;
                        var oc = ElementSystem.addStatus(o, "corrode", U.Max(2, cz.t), cz.power, cz.src, U.Max(1, U.Round(pst["corrodeMax"])), 1);
                        oc.stacks = U.Max(oc.stacks, U.Max(1, cz.stacks - 1));
                        if (pz != null) ElementSystem.addStatus(o, "poison", U.Max(2, pz.t), pz.power, pz.src, U.Max(1, U.Round(pst["poisonMaxStacks"])));
                        n++;
                    }
                    if (n > 0) sim.emit(new GameEvent { k = "burst", x = e.x, y = e.y, r = 1.5 });
                }
            }
            var def = Enemies.ENEMIES[e.def];
            if (def.onDeathSpawn != null)
            {
                for (int i = 0; i < def.onDeathSpawn.count; i++)
                {
                    var c = EnemySystem.spawnEnemyAt(sim, def.onDeathSpawn.enemy, false, e);
                    c.x += (i - 1.5) * 0.12;
                    c.y += ((i % 2) - 0.5) * 0.12;
                }
            }
        }
    }
}
