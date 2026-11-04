// Übersetzung von config/paragon.ts.
// Helden-Endlos 2.7: dauerhafte Heldenstufen (bis 99) und Paragon (bis 500, gemeinsam für alle Helden).
// Ab Heldenstufe 99 fließt jede Erfahrung in Paragon-EP. Jede Paragonstufe gibt einen Punkt, der in einem
// von drei Bäumen (Offensive, Defensive, Utility) vergeben wird. Die Wirkungen nutzen dieselben Kennzahlen
// wie der passive Baum und gelten automatisch in jedem Lauf.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: inline-Objekt <c>PARAGON</c>.</summary>
    public class ParagonConstants
    {
        public double heroMaxLevel;
        public double maxLevel;
        /// <summary>EP für Paragon P → P+1 = xpBase · (1 + P · xpGrowth)</summary>
        public double xpBase;
        public double xpGrowth;
        /// <summary>Stufenwerte flachen ab Stufe 30 ab (nur noch halber Zuwachs je Stufe).</summary>
        public double softcapLevel;
        public double softcapMul;
    }

    // ParagonTreeId = 'off' | 'def' | 'util' | 'tower' (string)

    /// <summary>TS: inline-Typ <c>PARAGON_TREES[i]: { id; name; icon; color }</c>.</summary>
    public class ParagonTreeDefinition
    {
        public string id;   // ParagonTreeId
        public string name;
        public string icon;
        public string color;
    }

    public class ParagonNode
    {
        public string id;
        public string tree;   // ParagonTreeId
        public string name;
        public string icon;
        public double maxRank;
        public Dictionary<string, double> per = new Dictionary<string, double>();   // Partial<PassiveEffects>
        public string description;
    }

    public class ParagonProgress
    {
        public double level;
        public double xp;
        public Dictionary<string, double> points = new Dictionary<string, double>();
    }

    public static partial class Paragon
    {
        public static readonly ParagonConstants PARAGON = new ParagonConstants
        {
            heroMaxLevel = 99,
            maxLevel = 500,
            /** EP für Paragon P → P+1 = xpBase · (1 + P · xpGrowth) */
            xpBase = 8000,
            xpGrowth = 1.0 / 250,
            /** Stufenwerte flachen ab Stufe 30 ab (nur noch halber Zuwachs je Stufe). */
            softcapLevel = 30,
            softcapMul = 0.5,
        };

        public static readonly List<ParagonTreeDefinition> PARAGON_TREES = new List<ParagonTreeDefinition>
        {
            new ParagonTreeDefinition { id = "off", name = "Offensive", icon = "⚔", color = "#ff8a5c" },
            new ParagonTreeDefinition { id = "def", name = "Defensive", icon = "🛡", color = "#6cc6ff" },
            new ParagonTreeDefinition { id = "util", name = "Utility", icon = "✦", color = "#ffd36b" },
            new ParagonTreeDefinition { id = "tower", name = "Türme", icon = "🏰", color = "#9be36b" },
        };

        public static readonly List<ParagonNode> PARAGON_NODES = new List<ParagonNode>
        {
            // Offensive
            new ParagonNode { id = "pg_might", tree = "off", name = "Wucht", icon = "⚔", maxRank = 50, per = new Dictionary<string, double> { ["heroDmgPct"] = 0.01 }, description = "+1 % Heldenschaden" },
            new ParagonNode { id = "pg_crit", tree = "off", name = "Präzision", icon = "🎯", maxRank = 40, per = new Dictionary<string, double> { ["heroCrit"] = 0.003 }, description = "+0,3 % kritische Chance" },
            new ParagonNode { id = "pg_haste", tree = "off", name = "Hast", icon = "⚡", maxRank = 40, per = new Dictionary<string, double> { ["heroAtkSpeed"] = 0.006 }, description = "+0,6 % Angriffstempo" },
            new ParagonNode { id = "pg_spell", tree = "off", name = "Zauberkraft", icon = "✺", maxRank = 40, per = new Dictionary<string, double> { ["abilityPct"] = 0.01 }, description = "+1 % Fähigkeitsstärke" },
            // Defensive
            new ParagonNode { id = "pg_vital", tree = "def", name = "Vitalität", icon = "❤", maxRank = 50, per = new Dictionary<string, double> { ["heroHpPct"] = 0.012 }, description = "+1,2 % Helden-LP" },
            new ParagonNode { id = "pg_armor", tree = "def", name = "Panzerung", icon = "🛡", maxRank = 40, per = new Dictionary<string, double> { ["heroArmor"] = 0.002 }, description = "+0,2 % Rüstung" },
            new ParagonNode { id = "pg_regen", tree = "def", name = "Regeneration", icon = "✚", maxRank = 40, per = new Dictionary<string, double> { ["heroRegen"] = 0.15 }, description = "+0,15 LP/s" },
            new ParagonNode { id = "pg_leech", tree = "def", name = "Lebensraub", icon = "🩸", maxRank = 30, per = new Dictionary<string, double> { ["lifesteal"] = 0.001 }, description = "+0,1 % Lebensraub" },
            new ParagonNode { id = "pg_core", tree = "def", name = "Bollwerk", icon = "🏠", maxRank = 20, per = new Dictionary<string, double> { ["coreLives"] = 2 }, description = "+2 Kernenergie (bis +40)" },
            // Utility
            new ParagonNode { id = "pg_greed", tree = "util", name = "Gier", icon = "🪙", maxRank = 50, per = new Dictionary<string, double> { ["killGoldPct"] = 0.004 }, description = "+0,4 % Kopfgeld" },
            new ParagonNode { id = "pg_loot", tree = "util", name = "Beutejäger", icon = "◆", maxRank = 40, per = new Dictionary<string, double> { ["resourcePct"] = 0.005, ["lootPct"] = 0.003 }, description = "+0,5 % Ressourcen, +0,3 % Beute" },
            new ParagonNode { id = "pg_focus", tree = "util", name = "Fokus", icon = "⌛", maxRank = 40, per = new Dictionary<string, double> { ["heroCdr"] = 0.002 }, description = "−0,2 % Abklingzeit" },
            new ParagonNode { id = "pg_swift", tree = "util", name = "Eile", icon = "👟", maxRank = 30, per = new Dictionary<string, double> { ["moveSpeed"] = 0.01 }, description = "+0,01 Bewegungstempo" },
            new ParagonNode { id = "pg_magnet", tree = "util", name = "Magnetismus", icon = "🧲", maxRank = 30, per = new Dictionary<string, double> { ["pickupRadius"] = 0.03 }, description = "+0,03 Aufsammelradius" },
            // Türme (nur für Türme)
            new ParagonNode { id = "pg_tower", tree = "tower", name = "Turmmacht", icon = "🏰", maxRank = 50, per = new Dictionary<string, double> { ["towerDmgPct"] = 0.004 }, description = "+0,4 % Turmschaden" },
            new ParagonNode { id = "pg_rhythm", tree = "tower", name = "Turmtakt", icon = "♫", maxRank = 40, per = new Dictionary<string, double> { ["towerSpeedPct"] = 0.003 }, description = "+0,3 % Turmangriffstempo" },
            new ParagonNode { id = "pg_reach", tree = "tower", name = "Weitblick", icon = "🔭", maxRank = 30, per = new Dictionary<string, double> { ["towerRange"] = 0.01 }, description = "+0,01 Turmreichweite" },
            new ParagonNode { id = "pg_tcrit", tree = "tower", name = "Zielwasser", icon = "🎯", maxRank = 40, per = new Dictionary<string, double> { ["towerCrit"] = 0.002 }, description = "+0,2 % kritische Chance für Türme" },
            new ParagonNode { id = "pg_pierce", tree = "tower", name = "Durchschlag", icon = "⛏", maxRank = 30, per = new Dictionary<string, double> { ["towerPierce"] = 0.003 }, description = "+0,3 % Panzerbrechung für Türme" },
            new ParagonNode { id = "pg_blast", tree = "tower", name = "Sprengkraft", icon = "💥", maxRank = 30, per = new Dictionary<string, double> { ["towerSplashPct"] = 0.004 }, description = "+0,4 % Flächenradius (Kanone, Meteor, Minen …)" },
            new ParagonNode { id = "pg_builder", tree = "tower", name = "Baumeister", icon = "🔨", maxRank = 25, per = new Dictionary<string, double> { ["towerCostPct"] = 0.004 }, description = "−0,4 % Baukosten für Türme (max. −10 %)" },
            new ParagonNode { id = "pg_mine", tree = "tower", name = "Goldader", icon = "⛏️", maxRank = 30, per = new Dictionary<string, double> { ["mineIncomePct"] = 0.005 }, description = "+0,5 % Goldgräber-Ertrag" },
        };

        public static double paragonXpToNext(double level)
        {
            return U.Round(PARAGON.xpBase * (1 + level * PARAGON.xpGrowth));
        }

        public static double paragonSpent(Dictionary<string, double> points)
        {
            double n = 0;
            foreach (ParagonNode node in PARAGON_NODES) n += U.Max(0, U.Min(node.maxRank, (points != null ? U.GetN(points, node.id) : null) ?? 0));
            return n;
        }

        public static readonly double PARAGON_TOTAL_RANKS = sumMaxRank();
        private static double sumMaxRank()
        {
            double a = 0;
            foreach (ParagonNode n in PARAGON_NODES) a = a + n.maxRank;
            return a;
        }

        public static ParagonProgress emptyParagon()
        {
            return new ParagonProgress { level = 0, xp = 0, points = new Dictionary<string, double>() };
        }

        /// <summary>3.0: Vorab-Paragonpunkte ab Heldenstufe 50 – alle 5 Stufen einer (Stufe 50, 55 … 95 = 10 Punkte).</summary>
        public static double paragonPrePoints(double heroLevel)
        {
            return heroLevel >= 50 ? Math.Floor((U.Min(heroLevel, 99) - 50) / 5) + 1 : 0;
        }

        /// <summary>
        /// Bereinigt Paragon-Daten: gültige Knoten, Ränge im Rahmen, nie mehr Punkte vergeben als vorhanden (extra = Vorab- und Meilensteinpunkte).
        /// TS: <c>raw: unknown</c>. Akzeptiert ParagonProgress, HeroEndlessParagon, HeroLevelProgress sowie JSON-Bäume
        /// (IDictionary&lt;string, object&gt; aus <c>U.ParseJson</c>) und Dictionary&lt;string, double&gt;.
        /// </summary>
        public static ParagonProgress sanitizeParagon(object raw, double extra = 0)
        {
            ParagonProgress out_ = emptyParagon();
            // TS: typeof raw !== 'object' || raw === null → leer (Strings, Zahlen, bool zählen nicht als Objekt)
            if (raw == null || raw is string || raw is double || raw is bool || raw is int || raw is float || raw is long) return out_;
            object rLevel = field(raw, "level");
            object rXp = field(raw, "xp");
            object rPoints = field(raw, "points");
            out_.level = U.Max(0, U.Min(PARAGON.maxLevel, Math.Floor(U.Or(jsNumber(rLevel), 0))));
            out_.xp = out_.level >= PARAGON.maxLevel ? 0 : U.Max(0, U.Min(paragonXpToNext(out_.level), U.Or(jsNumber(rXp), 0)));
            // typeof r.points === 'object' && r.points !== null ? r.points : {}
            object pts = (rPoints != null && !(rPoints is string) && !(rPoints is double) && !(rPoints is bool) && !(rPoints is int)) ? rPoints : null;
            double budget = out_.level + U.Max(0, extra);
            foreach (ParagonNode node in PARAGON_NODES)
            {
                object pv = pts != null ? field(pts, node.id) : Undefined;
                double v = U.Max(0, U.Min(node.maxRank, Math.Floor(U.Or(jsNumber(pv), 0)), budget));
                if (v > 0)
                {
                    out_.points[node.id] = v;
                    budget -= v;
                }
            }
            return out_;
        }

        /// <summary>Vergleichswert für „mehr Fortschritt“ (Stufe zuerst, dann EP).</summary>
        public static double paragonScore(ParagonProgress p) { return p.level * 1e9 + p.xp; }
        public static double paragonScore(HeroEndlessParagon p) { return p.level * 1e9 + p.xp; }
        public static double paragonScore(HeroLevelProgress p) { return p.level * 1e9 + p.xp; }
        public static double paragonScore(double level, double xp) { return level * 1e9 + xp; }

        // ------------------------------------------------------------ Hilfen für `unknown`-Eingaben (nur C#)

        /// <summary>Marker für JS <c>undefined</c> (fehlender Schlüssel).</summary>
        private static readonly object Undefined = new object();

        /// <summary>Liest ein Feld aus einem untypisierten Objekt (JSON-Baum, Dictionary oder bekannte Klasse); fehlt es → Undefined.</summary>
        private static object field(object o, string key)
        {
            if (o is IDictionary<string, object> d) return d.TryGetValue(key, out object v) ? v : Undefined;
            if (o is IDictionary<string, double> dd) return dd.TryGetValue(key, out double v2) ? (object)v2 : Undefined;
            if (o is ParagonProgress pp)
            {
                if (key == "level") return pp.level;
                if (key == "xp") return pp.xp;
                if (key == "points") return pp.points;
                return Undefined;
            }
            if (o is HeroEndlessParagon hp)
            {
                if (key == "level") return hp.level;
                if (key == "xp") return hp.xp;
                if (key == "points") return hp.points;
                if (key == "rev") return hp.rev;
                return Undefined;
            }
            if (o is HeroLevelProgress hl)
            {
                if (key == "level") return hl.level;
                if (key == "xp") return hl.xp;
                return Undefined;
            }
            // PORT: Arrays (IList) und andere Objekte haben in TS keine passenden Schlüssel → undefined.
            return Undefined;
        }

        /// <summary>JS <c>Number(v)</c>: undefined → NaN, null → 0, bool → 0/1, String → Zahlparser, Zahl → Zahl, Objekt → NaN.</summary>
        private static double jsNumber(object v)
        {
            if (ReferenceEquals(v, Undefined)) return double.NaN;
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is float f) return f;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s) return U.ParseNumber(s);
            // PORT: Number([]) = 0 und Number([x]) = Number(x) in JS – hier vereinfacht als NaN behandelt.
            return double.NaN;
        }
    }
}
