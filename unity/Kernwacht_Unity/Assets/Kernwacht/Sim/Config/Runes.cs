// Übersetzung von config/runes.ts.
// Helden-Endlos 2.7: Runen werden in Türme gesockelt. Sie sind nur findbar (kein Kauf, keine Herstellung) und
// fallen erst ab Welle 250. Drei Qualitätsstufen; Sockel je Turm wachsen mit dem Ausbau.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    // RuneId = 'might' | 'haste' | 'reach' | 'precision' | 'ruin' | 'frost' | 'venom' | 'greed' (string)

    public class RuneDef
    {
        public string id;   // RuneId
        public string name;
        public string icon;
        public string color;
        /// <summary>Wirkung bei Qualität 1; höhere Qualitäten skalieren mit RUNE_TIERS[q].mul.</summary>
        public List<StatMod> mods = new List<StatMod>();
        public string description;
    }

    /// <summary>TS: Elementtyp von <c>RUNE_TIERS</c> (as const).</summary>
    public class RuneTier
    {
        public double tier;
        public string name;
        public double mul;
    }

    /// <summary>TS: inline-Objekt <c>RUNE_RULES</c>.</summary>
    public class RuneRules
    {
        /// <summary>Volle Runen (Qualität II/III) fallen erst ab dieser Welle.</summary>
        public double fromWave;
        /// <summary>3.0: Runensplitter (Qualität I) schon ab dieser Welle.</summary>
        public double splinterFrom;
        /// <summary>Schlüssel: 'normal' | 'elite' | 'miniBoss' | 'boss'.</summary>
        public Dictionary<string, double> dropChance = new Dictionary<string, double>();
        /// <summary>Wellenabschluss: zusätzliche Chance je Welle ab fromWave.</summary>
        public double waveChance;
        /// <summary>Höchstens so viele ungesockelte Runen im Beutel.</summary>
        public double bagMax;
    }

    public static partial class Runes
    {
        public static readonly RuneTier[] RUNE_TIERS = new RuneTier[]
        {
            new RuneTier { tier = 1, name = "Runensplitter", mul = 1 },
            new RuneTier { tier = 2, name = "Rune", mul = 1.8 },
            new RuneTier { tier = 3, name = "Uralte Rune", mul = 3 },
        };

        public static readonly Dictionary<string, RuneDef> RUNES = new Dictionary<string, RuneDef>
        {
            ["might"] = new RuneDef { id = "might", name = "Kraftrune", icon = "ᚦ", color = "#ff7a59", mods = new List<StatMod> { new StatMod { stat = "damageMul", add = 0.12 } }, description = "+12 % Schaden" },
            ["haste"] = new RuneDef { id = "haste", name = "Hastrune", icon = "ᚱ", color = "#ffd84a", mods = new List<StatMod> { new StatMod { stat = "attackSpeedMul", add = 0.09 } }, description = "+9 % Angriffstempo" },
            ["reach"] = new RuneDef { id = "reach", name = "Weitrune", icon = "ᛉ", color = "#7ad8ff", mods = new List<StatMod> { new StatMod { stat = "range", add = 0.35 } }, description = "+0,35 Reichweite" },
            ["precision"] = new RuneDef { id = "precision", name = "Präzisionsrune", icon = "ᛏ", color = "#ff9ae0", mods = new List<StatMod> { new StatMod { stat = "critChance", add = 0.06 }, new StatMod { stat = "critMult", add = 0.15 } }, description = "+6 % kritische Chance, +0,15 kritischer Multiplikator" },
            ["ruin"] = new RuneDef { id = "ruin", name = "Bruchrune", icon = "ᚺ", color = "#c0a0ff", mods = new List<StatMod> { new StatMod { stat = "armorPierce", add = 0.08 } }, description = "+8 % Panzerbrechung" },
            ["frost"] = new RuneDef { id = "frost", name = "Frostrune", icon = "ᛁ", color = "#a8ecff", mods = new List<StatMod> { new StatMod { stat = "chillPct", add = 0.06 }, new StatMod { stat = "chillDur", add = 0.6 } }, description = "+6 % Verlangsamung je Treffer" },
            ["venom"] = new RuneDef { id = "venom", name = "Giftrune", icon = "ᛇ", color = "#8aff7a", mods = new List<StatMod> { new StatMod { stat = "poisonDps", add = 4 }, new StatMod { stat = "poisonDur", add = 1 }, new StatMod { stat = "poisonMaxStacks", add = 1 } }, description = "+4 Gift-SpS je Stapel" },
            ["greed"] = new RuneDef { id = "greed", name = "Gierrune", icon = "ᚠ", color = "#ffc04a", mods = new List<StatMod>(), description = "+1 Gold je Abschuss durch Türme dieser Sorte" },
        };

        public static readonly List<string> RUNE_ORDER = new List<string> { "might", "haste", "reach", "precision", "ruin", "frost", "venom", "greed" };

        public static readonly RuneRules RUNE_RULES = new RuneRules
        {
            /** Volle Runen (Qualität II/III) fallen erst ab dieser Welle. */
            fromWave = 250,
            /** 3.0: Runensplitter (Qualität I) schon ab dieser Welle. */
            splinterFrom = 100,
            dropChance = new Dictionary<string, double> { ["normal"] = 0.0015, ["elite"] = 0.04, ["miniBoss"] = 0.25, ["boss"] = 0.6 },
            /** Wellenabschluss: zusätzliche Chance je Welle ab fromWave. */
            waveChance = 0.12,
            /** Höchstens so viele ungesockelte Runen im Beutel. */
            bagMax = 40,
        };

        /// <summary>Wirkungen einer Rune in einer Qualität (1–3).</summary>
        public static List<StatMod> runeMods(string id, double tier)
        {
            double mul = RUNE_TIERS[(int)U.Max(0, U.Min(2, tier - 1))].mul;
            List<StatMod> out_ = new List<StatMod>();
            foreach (StatMod m in RUNES[id].mods) out_.Add(new StatMod { stat = m.stat, add = (m.add ?? 0) * mul });
            return out_;
        }

        public static string runeLabel(string id, double tier)
        {
            return $"{RUNE_TIERS[(int)U.Max(0, U.Min(2, tier - 1))].name}: {RUNES[id].name}";
        }

        public static string runeEffectText(string id, double tier)
        {
            double mul = RUNE_TIERS[(int)U.Max(0, U.Min(2, tier - 1))].mul;
            return mul == 1 ? RUNES[id].description : $"{RUNES[id].description} ×{replaceFirst(U.Fmt(mul), ".", ",")}";
        }

        /// <summary>JS <c>str.replace(a, b)</c> mit String-Muster: ersetzt nur das erste Vorkommen.</summary>
        private static string replaceFirst(string s, string a, string b)
        {
            int i = s.IndexOf(a, StringComparison.Ordinal);
            return i < 0 ? s : s.Substring(0, i) + b + s.Substring(i + a.Length);
        }

        /// <summary>Türme mit Sockeln: alle kämpfenden und unterstützenden Türme (keine Wirtschaft, keine Barrikade).</summary>
        public static bool runeEligible(TowerDefinition def)
        {
            TowerDefinition d = def;
            return d.attack != "economy" && d.attack != "none";
        }

        public static bool runeEligible(string def)
        {
            return runeEligible(Towers.TOWERS[def]);
        }

        /// <summary>3.3: Runensockel gehören der Turmsorte. 1 ab Welle 100, 2 ab 250, 3 ab 400, (3.11) 4 ab 500; legendäre Fusionen je +1.</summary>
        public static readonly double[] RUNE_SLOT_WAVES = { 100, 250, 400, 500 };

        public static double runeSockets(string def, double wave)
        {
            if (!runeEligible(def)) return 0;
            double @base = 0;
            foreach (double w in RUNE_SLOT_WAVES) if (wave >= w) @base++;
            return @base > 0 && Towers.TOWERS[def].legendary == true ? @base + 1 : @base;
        }

        /// <summary>Welle, ab der die nächste Sockelstufe frei wird (null = alle frei).</summary>
        public static double? nextRuneSlotWave(double wave)
        {
            foreach (double w in RUNE_SLOT_WAVES) if (wave < w) return w;
            return null;
        }
    }
}
