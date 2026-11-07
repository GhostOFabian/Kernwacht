// Übersetzung von config/goldsinks.ts.
// 3.6 Goldverwendung in den Endlosmodi: Turm-Veredelung (★1–★10). (Bauplatzkauf und Kernausbau in 3.8 entfernt.)

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: Ad-hoc-Objekt <c>STARS</c> (config/goldsinks.ts).</summary>
    public class StarsConstants
    {
        /** 3.11: bis ★25; ab ★softcap wirkt jeder Stern nur noch zur Hälfte und wird langsamer teurer (lateGrowth). */
        public double max;
        public double softcap;
        public double lateGrowth;
        /** Preis ★n → ★n+1 = Basis · growth^n; Basis = Gesamtwert des voll ausgebauten Turms · baseFactor. */
        public double baseFactor;
        public double growth;
        /** Legendäre Türme (Grundpreis 0): feste Basis. */
        public double legendaryBase;
        /** Für alle kämpfenden Türme je Stern. */
        public double dmgPerStar;
        public double speedPerStar;
    }

    /// <summary>TS: Werttyp von <c>STAR_KEYS</c>: <c>{ mods: StatMod[]; text: string }</c>.</summary>
    public class StarKeyDefinition
    {
        public List<StatMod> mods = new List<StatMod>();
        public string text;
    }

    public static partial class Goldsinks
    {
        // ------------------------------------------------------------ Veredelung

        public static readonly StarsConstants STARS = new StarsConstants
        {
            /** 3.11: bis ★25; ab ★softcap wirkt jeder Stern nur noch zur Hälfte und wird langsamer teurer (lateGrowth). */
            max = 25,
            softcap = 10,
            lateGrowth = 1.25,
            /** Preis ★n → ★n+1 = Basis · growth^n; Basis = Gesamtwert des voll ausgebauten Turms · baseFactor. */
            baseFactor = 0.6,
            growth = 1.45, // 4.1: vorher 1,55 – späte Sterne waren das einzige Goldziel und zu teuer
            /** Legendäre Türme (Grundpreis 0): feste Basis. */
            legendaryBase = 700,
            /** Für alle kämpfenden Türme je Stern. */
            dmgPerStar = 0.08,
            speedPerStar = 0.02,
        };

        /** Je Turmsorte die wichtigsten Werte, die pro Stern zusätzlich steigen (add je Stern bzw. mul je Stern). */
        public static readonly Dictionary<string, StarKeyDefinition> STAR_KEYS = new Dictionary<string, StarKeyDefinition>
        {
            ["ranger"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "critChance", add = 0.02 }, new StatMod { stat = "range", add = 0.05 } }, text = "+2 % Krit, +0,05 Reichweite" },
            ["ember"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "burnDps", add = 2 }, new StatMod { stat = "splash", add = 0.04 } }, text = "+2 Brand/s, +0,04 Explosionsradius" },
            ["frost"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "chillPct", add = 0.015 }, new StatMod { stat = "chillMax", add = 0.015 }, new StatMod { stat = "range", add = 0.05 } }, text = "+1,5 % Verlangsamung und Höchstwert, +0,05 Reichweite" },
            ["storm"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "chains", add = 0.34 }, new StatMod { stat = "chainRange", add = 0.08 } }, text = "+1 Kettensprung je 3 Sterne, +0,08 Sprungweite" },
            ["spore"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "poisonDps", add = 1.5 }, new StatMod { stat = "poisonMaxStacks", add = 0.25 } }, text = "+1,5 Gift/s je Stapel, +1 Stapel je 4 Sterne" },
            ["ballista"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "armorPierce", add = 0.03 }, new StatMod { stat = "critMult", add = 0.05 } }, text = "+3 % Panzerbrechung, +0,05 Krit-Multiplikator" },
            ["rune"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "markPct", add = 0.01 }, new StatMod { stat = "range", add = 0.05 } }, text = "+1 % Resistenzschwächung, +0,05 Reichweite" },
            ["resonator"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "range", add = 0.06 }, new StatMod { stat = "interrupt", add = 0.5 } }, text = "+0,06 Reichweite, stärkere Unterbrechung" },
            ["goldmine"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "income", mul = 1.08 } }, text = "+8 % Einkommen (multiplikativ)" },
            ["pylon"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "networkBonus", add = 0.01 }, new StatMod { stat = "linkRange", add = 0.1 } }, text = "+1 % Netzwerkbonus, +0,1 Verbindungsweite" },
            ["cannon"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.05 } }, text = "+0,05 Explosionsradius" },
            ["riftbinder"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "riftTargets", add = 0.2 }, new StatMod { stat = "range", add = 0.05 } }, text = "+1 Ziel je 5 Sterne, +0,05 Reichweite" },
            ["meteor"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.05 }, new StatMod { stat = "meteorCount", add = 0.2 } }, text = "+0,05 Einschlagradius, +1 Meteor je 5 Sterne" },
            ["tesla"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "arcDps", mul = 1.06 }, new StatMod { stat = "arcRange", add = 0.05 } }, text = "+6 % Lichtbogen-Schaden, +0,05 Bogenweite" },
            ["gravity"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "pullCells", add = 0.1 }, new StatMod { stat = "range", add = 0.05 } }, text = "+0,1 Felder Sog, +0,05 Reichweite" },
            ["dragon"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "airDmgMul", add = 0.1 }, new StatMod { stat = "splash", add = 0.03 } }, text = "+10 % Schaden gegen Flieger, +0,03 Atemradius" },
            ["laser"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "range", add = 0.06 } }, text = "+0,06 Strahlreichweite" },
            ["totem"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "auraDmg", add = 0.01 }, new StatMod { stat = "auraSpeed", add = 0.01 } }, text = "+1 % Aura-Schaden und -Tempo" },
            ["minelayer"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "mineMax", add = 0.5 }, new StatMod { stat = "splash", add = 0.03 } }, text = "+1 Mine je 2 Sterne, +0,03 Explosionsradius" },
            ["skyguard"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "chillMax", add = 0.015 }, new StatMod { stat = "multishot", add = 0.2 } }, text = "+1,5 % Verlangsamung, +1 Ziel je 5 Sterne" },
            ["thornwood"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "bleedDps", add = 1.5 }, new StatMod { stat = "shredPct", add = 0.003 } }, text = "+1,5 Blutung/s, +0,3 % Rüstungsbruch je Stapel" },
            ["sporemine"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "poisonDps", add = 2 }, new StatMod { stat = "mineMax", add = 0.5 } }, text = "+2 Gift/s, +1 Mine je 2 Sterne" },
            ["skywatch"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "airDmgMul", add = 0.1 }, new StatMod { stat = "multishot", add = 0.2 } }, text = "+10 % Schaden gegen Flieger, +1 Ziel je 5 Sterne" },
            ["phoenix"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "burnDps", add = 3 }, new StatMod { stat = "airDmgMul", add = 0.08 } }, text = "+3 Brand/s, +8 % gegen Flieger" },
            ["seal"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "silenceDur", add = 0.05 }, new StatMod { stat = "range", add = 0.05 } }, text = "+0,05 s Stille, +0,05 Reichweite" },
            ["acid"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "corrodePct", add = 0.002 }, new StatMod { stat = "splash", add = 0.03 } }, text = "+0,2 % Zersetzung je Stapel, +0,03 Spritzradius" },
            ["quake"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "range", add = 0.04 }, new StatMod { stat = "stunDur", add = 0.01 } }, text = "+0,04 Reichweite, +0,01 s Taumeln" },
            ["gale"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "range", add = 0.05 }, new StatMod { stat = "chillPct", add = 0.005 } }, text = "+0,05 Reichweite, +0,5 % Verlangsamung" },
            ["eyrie"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "critChance", add = 0.01 }, new StatMod { stat = "range", add = 0.08 } }, text = "+1 % Krit, +0,08 Reichweite" },
            ["barracks"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "soldierHold", add = 0.3 } }, text = "+0,3 s Halten je Soldat" },
            ["bank"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "interestCap", add = 1.5 } }, text = "+1,5 Gold Zinsgrenze" },
            ["ward"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "range", add = 0.05 }, new StatMod { stat = "wardCd", add = -0.15 } }, text = "+0,05 Reichweite, 0,15 s schnelleres Nachladen" },
            ["glacier"] = new StarKeyDefinition { mods = new List<StatMod> { new StatMod { stat = "chillMax", add = 0.015 }, new StatMod { stat = "splash", add = 0.04 } }, text = "+1,5 % Verlangsamung, +0,04 Radius" },
        };

        /** Kämpft der Turm (bekommt den allgemeinen Schaden-/Tempobonus)? */
        public static bool starFights(string def)
        {
            string a = Towers.TOWERS[def].attack;
            return a != "economy" && a != "none" && a != "aura";
        }

        /** Wertänderungen eines Turms mit n Sternen. */
        public static List<StatMod> starMods(string def, double stars)
        {
            double raw = U.Max(0, U.Min(STARS.max, Math.Floor(stars)));
            if (raw <= 0) return new List<StatMod>();
            // effektive Sternzahl: bis ★10 voll, danach je Stern halb
            double n = U.Min(raw, STARS.softcap) + U.Max(0, raw - STARS.softcap) * 0.5;
            List<StatMod> @out = new List<StatMod>();
            if (starFights(def))
            {
                @out.Add(new StatMod { stat = "damageMul", add = STARS.dmgPerStar * n });
                @out.Add(new StatMod { stat = "attackSpeedMul", add = STARS.speedPerStar * n });
            }
            StarKeyDefinition sk = U.Get(STAR_KEYS, def);
            List<StatMod> mods = sk?.mods ?? (U.Truthy(Towers.TOWERS[def].legendary) ? new List<StatMod> { new StatMod { stat = "range", add = 0.05 } } : new List<StatMod>());
            foreach (StatMod m in mods)
            {
                if (m.mul != null) @out.Add(new StatMod { stat = m.stat, mul = Math.Pow(m.mul.Value, n) });
                else @out.Add(new StatMod { stat = m.stat, add = (m.add ?? 0) * n });
            }
            return @out;
        }

        /** Kurzbeschreibung „je Stern“ für die Oberfläche. */
        public static string starText(string def)
        {
            List<string> parts = new List<string>();
            if (starFights(def)) parts.Add($"+{U.Fmt(U.Round(STARS.dmgPerStar * 100))} % Schaden, +{U.Fmt(U.Round(STARS.speedPerStar * 100))} % Tempo");
            StarKeyDefinition sk = U.Get(STAR_KEYS, def);
            string k = sk?.text ?? (U.Truthy(Towers.TOWERS[def].legendary) ? "+0,05 Reichweite" : "");
            if (U.Truthy(k)) parts.Add(k);
            return $"{U.Join(parts, ", ")}{(STARS.max > STARS.softcap ? $" (ab ★{U.Fmt(STARS.softcap + 1)} halb so viel)" : "")}";
        }
    }
}
