// Helden als Befehlshaber (Umbau Schritt 2).
// Jeder Held führt die Türme in seinem Befehlsradius an. Türme seiner Affinität (Element bzw. Turmart)
// erhalten den vollen Befehlsbonus, alle anderen nur ein Viertel. Zusätzlich kann der Held ein Banner
// setzen, das an einem zweiten Ort mit halber Stärke wirkt (Umsetzen: 20 s Abklingzeit).
// Befehlsboni haben eine eigene Obergrenze, damit Türme die Hauptquelle des Schadens bleiben.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>Gesetztes Befehlsbanner des Helden (im Spielstand gespeichert).</summary>
    public class HeroBanner
    {
        public double x;
        public double y;
    }

    /// <summary>Befehlsprofil eines Helden: welche Türme er besonders gut anführt und womit.</summary>
    public class CommandProfile
    {
        /// <summary>Schadensarten der Affinitätstürme ("*" = alle).</summary>
        public string[] types = new string[0];
        /// <summary>Zusätzliche Turmarten unabhängig von der Schadensart.</summary>
        public string[] towers = new string[0];
        public double dmg;
        public double speed;
        public double range;
        public double crit;
        /// <summary>Kurzbezeichnung für die Oberfläche, z. B. „Feuertürme“.</summary>
        public string label;
    }

    public static class HeroCommand
    {
        /// <summary>Stärke außerhalb der Affinität.</summary>
        public const double OFF_AFFINITY = 0.25;
        /// <summary>Stärke des Banners gegenüber dem Helden selbst.</summary>
        public const double BANNER_POWER = 0.5;
        /// <summary>Abklingzeit, bevor das Banner erneut versetzt werden kann (s).</summary>
        public const double BANNER_CD = 20;
        /// <summary>Befehlsradius = Unterstützungsradius + dieser Wert (Grundwert 2,5 + 0,5 = 3 Felder).</summary>
        public const double RADIUS_BONUS = 0.5;
        /// <summary>Obergrenze nur für Befehlsboni (Held + Banner + Aura).</summary>
        public const double CAP_DMG = 0.8, CAP_SPEED = 0.5, CAP_RANGE = 1.0, CAP_CRIT = 0.25;

        static CommandProfile P(string label, string[] types, string[] towers, double dmg, double speed, double range = 0, double crit = 0)
            => new CommandProfile { label = label, types = types ?? new string[0], towers = towers ?? new string[0], dmg = dmg, speed = speed, range = range, crit = crit };

        /// <summary>Affinitätstabelle der zwölf Helden.</summary>
        public static readonly Dictionary<string, CommandProfile> PROFILES = new Dictionary<string, CommandProfile>
        {
            // Unterstützer: Lyra führt alle Türme an, dafür schwächer
            ["lyra"] = P("alle Türme", new[] { "*" }, null, 0.08, 0.06),
            ["brann"] = P("Geschütz- und Kanonentürme", null, new[] { "ranger", "cannon", "ballista", "thornwood" }, 0.18, 0.08),
            ["vela"] = P("Schützentürme", null, new[] { "ranger", "ballista", "skyguard", "skywatch" }, 0.12, 0.12, 0.3),
            ["kaela"] = P("Frost-, Minen- und Kanonentürme", new[] { "frost" }, new[] { "minelayer", "cannon", "sporemine" }, 0.14, 0.08),
            ["varo"] = P("Feuertürme", new[] { "fire" }, null, 0.18, 0.08),
            ["nyx"] = P("Riss- und Gravitationstürme", null, new[] { "riftbinder", "gravity", "riftgate", "voidlance", "rune" }, 0.16, 0.1),
            ["liora"] = P("Klang- und Totemtürme", new[] { "sonic" }, new[] { "totem" }, 0.1, 0.16),
            ["thorgrim"] = P("schwere Türme", null, new[] { "cannon", "ballista", "minelayer", "skyguard", "thornwood" }, 0.16, 0.06, 0, 0.05),
            ["sena"] = P("Blitztürme", new[] { "lightning" }, null, 0.16, 0.1),
            ["oran"] = P("Leitnetz und Strahlen", null, new[] { "pylon", "tesla", "laser", "rune", "thunderspire" }, 0.14, 0.1, 0.2),
            ["mira"] = P("Gift- und Goldtürme", new[] { "poison" }, new[] { "goldmine" }, 0.16, 0.1),
            ["ommar"] = P("Frost- und Klangtürme", new[] { "frost", "sonic" }, null, 0.12, 0.1, 0, 0.05),
        };

        public static CommandProfile profileOf(string champId)
        {
            return champId != null && PROFILES.TryGetValue(champId, out var p) ? p : null;
        }

        /// <summary>Gehört dieser Turm zur Affinität des Helden?</summary>
        public static bool affine(string champId, string towerDef)
        {
            var p = profileOf(champId);
            if (p == null || towerDef == null || !Towers.TOWERS.TryGetValue(towerDef, out var d)) return false;
            foreach (var t in p.towers) if (t == towerDef) return true;
            foreach (var ty in p.types)
            {
                if (ty == "*") return true;
                if (d.damageType != null && d.damageType == ty) return true;
            }
            return false;
        }

        /// <summary>Affinität inklusive angelegter Befehlsrelikte.</summary>
        public static bool affine(HeroEndlessState he, string towerDef)
        {
            if (he == null) return false;
            if (affine(he.champion.id, towerDef)) return true;
            if (towerDef == null || !Towers.TOWERS.TryGetValue(towerDef, out var d)) return false;
            foreach (double? uid in he.champion.equipment)
            {
                if (uid == null) continue;
                ItemInstance inst = null;
                foreach (var i in he.champion.inventory) if (i.uid == uid.Value) { inst = i; break; }
                if (inst == null || !Items.ITEMS.TryGetValue(inst.item, out var it) || it.affinity == null) continue;
                foreach (var a in it.affinity)
                    if (a == "*" || a == towerDef || (d.damageType != null && a == d.damageType)) return true;
            }
            return false;
        }

        /// <summary>Anteil des Befehlsbonus für einen Turm (1 = Affinität, sonst 0,25).</summary>
        public static double affinityShare(HeroEndlessState he, string towerDef)
        {
            return affine(he, towerDef) ? 1 : OFF_AFFINITY;
        }

        public static double commandRadius(ChampionStats st, HeroEndlessState he = null)
        {
            return st.supportRadius + RADIUS_BONUS + (he != null ? Doctrines.of(he).commandRadius : 0);
        }

        /// <summary>Heldenschaden wächst mit der Welle mit (Wurzel der Lebenspunktkurve), damit der Held auch spät spürbar bleibt.</summary>
        public static double heroWaveMul(GameSimulation sim)
        {
            double w = sim.s.wave;
            double doc = Doctrines.of(sim.s.he).heroDmgMul;
            if (w <= 1) return doc;
            return U.Max(1, Math.Pow(Balance.waveHpMul(w, true), 0.5)) * doc;
        }

        /// <summary>Banner setzen bzw. versetzen (Taste B).</summary>
        public static CommandResult placeBanner(GameSimulation sim, double x, double y)
        {
            var he = sim.s.he;
            if (he == null) return CommandResult.Fail("Kein Held");
            if (he.champion.dead > 0) return CommandResult.Fail("Held ist besiegt");
            if (he.bannerCd > 0) return CommandResult.Fail($"Banner bereit in {U.Fmt(Math.Ceiling(he.bannerCd))} s");
            var map = sim.s.map;
            if (x < 0 || y < 0 || x >= map.width || y >= map.height) return CommandResult.Fail("Außerhalb der Karte");
            he.banner = new HeroBanner { x = Math.Floor(x) + 0.5, y = Math.Floor(y) + 0.5 };
            he.bannerCd = BANNER_CD;
            sim.emit(new GameEvent { k = "towerBuff", x = he.banner.x, y = he.banner.y, color = Champions.CHAMPIONS[he.champion.id].accent });
            sim.c.champBuffSig = "";
            return CommandResult.Ok("Banner gesetzt");
        }
    }
}
