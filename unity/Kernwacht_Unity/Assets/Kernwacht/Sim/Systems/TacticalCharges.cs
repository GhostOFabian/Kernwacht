// Taktikladungen (Umbau Schritt 3).
// Begrenzte, aufsparbare Notfallwerkzeuge für schwere Wellen. Ladungen gibt es alle 5 abgeschlossenen Wellen
// (Bosswellen alle 10 zusätzlich eine), höchstens 3 werden gespeichert. Jede Wirkung kostet eine Ladung;
// danach sperrt eine kurze gemeinsame Abklingzeit, damit nicht alle auf einmal gezündet werden.
// Turmverstärkungen durch Ladungen haben eine eigene Obergrenze (+40 % Schaden / +40 % Tempo).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class ChargeDefinition
    {
        public string id;
        public string name;
        public string icon;
        public double color;
        public string description;
        public double duration;
    }

    public static class TacticalCharges
    {
        public const double MAX_STORED = 3;
        public const double START = 1;
        public const double EVERY = 5;
        /// <summary>Gemeinsame Abklingzeit nach jeder Ladung (s).</summary>
        public const double SHARED_CD = 8;
        public const double CAP_DMG = 0.4, CAP_SPEED = 0.4;

        public static readonly Dictionary<string, ChargeDefinition> CHARGES = new Dictionary<string, ChargeDefinition>
        {
            ["overdrive"] = new ChargeDefinition { id = "overdrive", name = "Überlast", icon = "bolt", color = 0xffd860, duration = 10, description = "Alle Türme: +40 % Angriffstempo für 10 s." },
            ["focus"] = new ChargeDefinition { id = "focus", name = "Brennpunkt", icon = "star", color = 0xff8a5c, duration = 12, description = "Türme im Befehlsradius des Helden und am Banner: +40 % Schaden für 12 s." },
            ["frost"] = new ChargeDefinition { id = "frost", name = "Kältewelle", icon = "snow", color = 0x9ad8ff, duration = 6, description = "Alle Gegner auf der Karte werden 6 s lang um 40 % verlangsamt." },
            ["expose"] = new ChargeDefinition { id = "expose", name = "Schwachstelle", icon = "target", color = 0xc080ff, duration = 10, description = "Alle Gegner erleiden 10 s lang +25 % Schaden." },
        };
        public static readonly List<string> ORDER = new List<string> { "overdrive", "focus", "frost", "expose" };

        public const double FROST_SLOW = 0.4;
        public const double EXPOSE_PCT = 0.25;

        /// <summary>Gutschrift am Wellenende (aus HeroEndlessSystem, einmal je abgeschlossener Welle).</summary>
        public static void onWaveCleared(GameSimulation sim, double wave)
        {
            var he = sim.s.he;
            if (he == null) return;
            var doc = Doctrines.of(he);
            double gain = 0;
            if (wave % doc.chargeEvery == 0) gain++;
            if (wave % 10 == 0) gain++;
            if (gain <= 0) return;
            double before = he.charges;
            he.charges = U.Min(doc.chargeMax, he.charges + gain);
            if (he.charges > before)
                sim.emit(new GameEvent { k = "message", text = $"Taktikladung erhalten ({U.Fmt(he.charges)}/{U.Fmt(doc.chargeMax)}) – für schwere Wellen aufsparen", severity = "info" });
        }

        public static CommandResult check(GameSimulation sim, string id)
        {
            var he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            if (id == null || !CHARGES.ContainsKey(id)) return CommandResult.Fail("Unbekannte Ladung");
            if (he.charges < 1) return CommandResult.Fail($"Keine Taktikladung – neue alle {U.Fmt(Doctrines.of(he).chargeEvery)} Wellen");
            if (he.chargeCd > 0) return CommandResult.Fail($"Bereit in {U.Fmt(Math.Ceiling(he.chargeCd))} s");
            if (id == "focus" && he.champion.dead > 0 && he.banner == null) return CommandResult.Fail("Held ist besiegt und kein Banner gesetzt");
            return CommandResult.Ok();
        }

        public static CommandResult use(GameSimulation sim, string id)
        {
            var chk = check(sim, id);
            if (!chk.ok) return chk;
            var he = sim.s.he;
            var d = CHARGES[id];
            switch (id)
            {
                case "overdrive":
                    foreach (var t in sim.s.towers)
                    {
                        if (!attacks(t)) continue;
                        ChampionSystem.applyTowerBuff(sim, t.uid, "charge:overdrive", new TowerBuffValues { speed = 0.4 }, d.duration);
                    }
                    break;
                case "focus":
                {
                    var st = ChampionSystem.championStats(he);
                    double r2 = Math.Pow(HeroCommand.commandRadius(st, he), 2);
                    var c = he.champion;
                    foreach (var t in sim.s.towers)
                    {
                        if (!attacks(t)) continue;
                        double tx = t.cx + 0.5, ty = t.cy + 0.5;
                        bool inHero = c.dead <= 0 && Math.Pow(tx - c.x, 2) + Math.Pow(ty - c.y, 2) <= r2;
                        bool inBanner = he.banner != null && Math.Pow(tx - he.banner.x, 2) + Math.Pow(ty - he.banner.y, 2) <= r2;
                        if (inHero || inBanner) ChampionSystem.applyTowerBuff(sim, t.uid, "charge:focus", new TowerBuffValues { dmg = 0.4 }, d.duration);
                    }
                    break;
                }
                case "frost":
                    foreach (var e in sim.s.enemies)
                    {
                        if (!e.alive) continue;
                        // flag = Obergrenze in Prozent: die Kältewelle darf allein 40 % erreichen
                        ElementSystem.addStatus(e, "chill", d.duration, FROST_SLOW, 0, 1, FROST_SLOW * 100);
                    }
                    break;
                case "expose":
                    he.exposeT = d.duration;
                    break;
            }
            he.charges = U.Max(0, he.charges - 1);
            he.chargeCd = SHARED_CD;
            he.chargesUsed++;
            var goal = sim.s.map.goal;
            sim.emit(new GameEvent { k = "message", text = $"{d.name}! {d.description}", severity = "info" });
            sim.emit(new GameEvent { k = "towerBuff", x = goal.x + 0.5, y = goal.y + 0.5, color = d.color });
            sim.c.champBuffSig = "";
            return CommandResult.Ok(d.name);
        }

        static bool attacks(TowerInstance t)
        {
            var a = Towers.TOWERS[t.def].attack;
            return a != "economy" && a != "none";
        }

        /// <summary>Takt: Abklingzeit und globale Schwachstelle herunterzählen.</summary>
        public static void update(GameSimulation sim, double dt)
        {
            var he = sim.s.he;
            if (he == null) return;
            if (he.chargeCd > 0) he.chargeCd = U.Max(0, he.chargeCd - dt);
            if (he.exposeT > 0) he.exposeT = U.Max(0, he.exposeT - dt);
        }

        /// <summary>Schadensaufschlag durch „Schwachstelle“ (in DamageSystem.dealDamage).</summary>
        public static double damageTakenMul(GameSimulation sim)
        {
            var he = sim.s.he;
            return he != null && he.exposeT > 0 ? 1 + EXPOSE_PCT : 1;
        }
    }
}
