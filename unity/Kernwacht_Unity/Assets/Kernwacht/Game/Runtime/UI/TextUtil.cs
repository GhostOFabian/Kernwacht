// Übersetzung von ui/text.ts – deutsche Texte und Formatierungen für Tooltips, Turmdetails und Wellenvorschau.
// Zusätzlich (gemeinsam genutzt): fmt/fmtNum/pct aus ui/dom.ts, DAMAGE_NAMES aus render/palette.ts und sym() für
// Zeichen, die die Unity-Schrift nicht enthält (Emojis).
// Klassenname TextUtil statt Text (Namenskonflikt mit UnityEngine.UI.Text).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kernwacht.Sim;

namespace Kernwacht.Game.UI
{
    /// <summary>TS: Rückgabeeintrag von waveSummary().</summary>
    public class WaveSummaryItem
    {
        public string label;
        public string icon;
        public double count;
        public bool elite;
        public bool boss;
    }

    /// <summary>TS: Rückgabe von damageMatchups() <c>{ good; bad }</c>.</summary>
    public class DamageMatchups
    {
        public List<string> good = new List<string>();
        public List<string> bad = new List<string>();
    }

    public static class TextUtil
    {
        public static readonly Dictionary<string, string> PRIORITY_NAMES = new Dictionary<string, string>
        {
            ["first"] = "Erster auf dem Weg",
            ["exit"] = "Nächster zum Ausgang",
            ["strong"] = "Meiste Lebenspunkte",
            ["weak"] = "Wenigste Lebenspunkte",
            ["close"] = "Nächster zum Turm",
            ["random"] = "Zufällig",
        };

        public static readonly List<string> PRIORITY_ORDER = new List<string> { "first", "strong" };

        public static readonly Dictionary<string, string> MODE_NAMES = new Dictionary<string, string>
        {
            ["quick"] = "Schnelle Partie",
            ["campaign"] = "Kampagne",
            ["endless"] = "Endlosmodus",
            ["daily"] = "Tagesherausforderung",
            ["custom"] = "Eigene Karte",
            ["heroEndless"] = "Helden-Endlos",
        };

        /// <summary>MODE_NAMES[mode] (TS liefert bei unbekanntem Modus undefined → hier der Schlüssel selbst).</summary>
        public static string modeName(string mode) => mode != null && MODE_NAMES.TryGetValue(mode, out var n) ? n : (mode ?? "");

        /// <summary>
        /// PORT: Kopie von DAMAGE_NAMES aus render/palette.ts (gehört dem Welt-Übersetzer); hier gespiegelt,
        /// damit die Texte nicht von der Welt-Assembly-Reihenfolge abhängen.
        /// </summary>
        public static readonly Dictionary<string, string> DAMAGE_NAMES = new Dictionary<string, string>
        {
            ["physical"] = "Physisch", ["fire"] = "Feuer", ["frost"] = "Frost", ["lightning"] = "Blitz", ["poison"] = "Gift", ["arcane"] = "Arkan", ["sonic"] = "Schall",
        };

        public static string damageTypeName(TowerDefinition def)
        {
            return !string.IsNullOrEmpty(def.damageType) ? U.Get(DAMAGE_NAMES, def.damageType) : "–";
        }

        static double S(Dictionary<string, double> st, string k) => st != null && st.TryGetValue(k, out var v) ? v : 0;

        /// <summary>Schaden pro Sekunde (Direktschaden, ohne Effekte).</summary>
        public static double dps(Dictionary<string, double> st)
        {
            if (S(st, "damage") <= 0 || S(st, "cooldown") <= 0) return 0;
            return (S(st, "damage") * Math.Max(1, S(st, "multishot"))) / S(st, "cooldown");
        }

        static readonly string[] SHOW =
        {
            "splash", "chains", "pierce", "multishot", "burnDps", "burnDur", "chillPct", "freezeChance", "poisonDps",
            "poisonMaxStacks", "bleedDps", "bleedMaxStacks", "shredPct", "airDmgMul", "markPct", "interrupt", "knockback", "silenceDur", "stunDur", "armorPierce", "critChance",
            "income", "killBounty", "linkRange", "maxLinks", "networkBonus", "riftBack",
            "meteorCount", "fragments", "arcDps", "arcRange", "pullCells", "laserRamp", "auraDmg", "auraSpeed",
        };

        static readonly HashSet<string> PCT_KEYS = new HashSet<string> { "chillPct", "freezeChance", "markPct", "armorPierce", "critChance", "networkBonus", "auraDmg", "auraSpeed", "shredPct" };

        /// <summary>Wertezeilen [Bezeichnung, Wert] (TS: [string, string][] → Liste von string[2]).</summary>
        public static List<string[]> statLines(Dictionary<string, double> st, TowerDefinition def)
        {
            var @out = new List<string[]>();
            if (S(st, "damage") > 0) @out.Add(new[] { "Schaden", $"{fmtNum(S(st, "damage"))} ({damageTypeName(def)})" });
            if (S(st, "range") > 0) @out.Add(new[] { "Reichweite", $"{fmtNum(S(st, "range"))} Felder" });
            if (S(st, "damage") > 0 && def.attack != "pylon") @out.Add(new[] { "Angriff alle", $"{fmtNum(S(st, "cooldown"), 2)} s" });
            double d = dps(st);
            if (d > 0 && def.attack != "pylon") @out.Add(new[] { "Schaden/s", fmtNum(d) });
            foreach (var k in SHOW)
            {
                double v = S(st, k);
                if (!U.Truthy(v) || (k == "multishot" && v <= 1) || (k == "meteorCount" && v <= 1) || (k == "laserRamp" && v <= 1) || (k == "poisonMaxStacks" && S(st, "poisonDps") <= 0) || (k == "bleedMaxStacks" && S(st, "bleedDps") <= 0) || (k == "airDmgMul" && v <= 1)) continue;
                string label = Stats.STAT_LABELS.TryGetValue(k, out var l) && l != null ? l : k;
                @out.Add(new[] { label, PCT_KEYS.Contains(k) ? pct(v) : fmtNum(v) });
            }
            if (def.attack != "economy" && def.attack != "none")
            {
                var parts = new List<string>();
                if (S(st, "hitsGround") > 0) parts.Add("Boden");
                if (S(st, "hitsAir") > 0) parts.Add("Luft");
                string hits = string.Join(" + ", parts);
                @out.Add(new[] { "Trifft", hits.Length > 0 ? hits : "–" });
            }
            return @out;
        }

        public static Dictionary<string, double> baseStatsOf(string id)
        {
            var st = Stats.zeroStats();
            foreach (var kv in Towers.TOWERS[id].@base) st[kv.Key] = kv.Value;
            return st;
        }

        public static List<WaveSummaryItem> waveSummary(WaveDefinition w)
        {
            return w.groups.Select((g) =>
            {
                if (!string.IsNullOrEmpty(g.boss)) return new WaveSummaryItem { label = Enemies.BOSSES[g.boss].name, icon = "👑", count = 1, elite = false, boss = true };
                var d = Enemies.ENEMIES[g.enemy];
                return new WaveSummaryItem { label = g.elite ? $"Elite-{d.name}" : d.name, icon = d.icon, count = g.count, elite = g.elite, boss = false };
            }).ToList();
        }

        public static string keyLabel(string k)
        {
            return k == "-" ? "−" : k == "=" ? "=" : (k ?? "").ToUpperInvariant();
        }

        // ------------------------------------------------------------ 3.3: Konter direkt aus den Resistenzwerten

        static string pctTxt(double v) => $"{U.Fmt(U.Round(Math.Abs(v) * 100))} %";

        /// <summary>Gegner und Bosse, die gegen eine Schadensart schwach (≤ −10 %) bzw. resistent (≥ 20 %) sind.</summary>
        public static DamageMatchups damageMatchups(string type)
        {
            var r = new DamageMatchups();
            void add(string name, double? v)
            {
                if (v == null) return;
                if (v.Value <= -0.1) r.good.Add($"{name} +{pctTxt(v.Value)}");
                else if (v.Value >= 0.2) r.bad.Add($"{name} −{pctTxt(v.Value)}");
            }
            foreach (var e in Enemies.ENEMIES.Values) if (e.id != "boss") add(e.name, U.GetN(e.resist, type));
            foreach (var b in Enemies.BOSSES.Values) add(b.name, U.GetN(b.resist, type));
            return r;
        }

        /// <summary>Kampftürme (nicht legendär) einer Schadensart, für Konter-Hinweise am Gegner.</summary>
        public static List<string> towersOfType(string type)
        {
            return Towers.TOWERS.Values.Where((t) => t.damageType == type && t.legendary != true && t.attack != "economy" && t.attack != "none" && t.attack != "pylon").Select((t) => t.name).ToList();
        }

        // ------------------------------------------------------------ aus ui/dom.ts

        /// <summary>Ganzzahl mit deutschem Tausenderpunkt (dom.ts fmt).</summary>
        public static string fmt(double n) => U.De(U.Round(n));

        /// <summary>Zahl mit höchstens <paramref name="digits"/> Nachkommastellen, deutsch (dom.ts fmtNum).</summary>
        public static string fmtNum(double n, double digits = 1) => U.De(n, digits);

        /// <summary>Anteil als Prozent (dom.ts pct).</summary>
        public static string pct(double n) => $"{U.Fmt(U.Round(n * 100))} %";

        // ------------------------------------------------------------ Schriftzeichen (Unity-Port)

        static readonly (string from, string to)[] SYM =
        {
            ("✔", "√"), ("✓", "√"), ("✖", "×"), ("✕", "×"), ("★", "*"), ("☆", "·"), ("⚡", "Energie"), ("Ⓐ", "(A)"), ("Ⓑ", "(B)"),
            ("✚", "+"), ("⟲", "Reset"), ("↻", ""), ("⧉", ""), ("✎", ""), ("⚔", ""), ("⚓", ""), ("☠", ""), ("❄", ""),
        };

        /// <summary>
        /// Ersetzt bzw. entfernt Zeichen, die die LegacyRuntime-Schrift nicht kennt (Emojis, Zusatzsymbole).
        /// Bekannte Symbole werden durch lesbare Zeichen ersetzt; der Rest (Emoji-Blöcke, Variationsselektoren) entfällt.
        /// </summary>
        public static string sym(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            bool need = false;
            foreach (char c in s) if (c >= 0x2190 && c != 0x2212 || char.IsSurrogate(c)) { need = true; break; }
            if (!need) return s;
            foreach (var (from, to) in SYM) s = s.Replace(from, to);
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c)) { i++; continue; }   // Emojis außerhalb der BMP
                if (char.IsSurrogate(c)) continue;
                if (c == '️' || c == '︎' || c == '‍' || c == '⃣') continue;
                if (c >= 0x2600 && c <= 0x27BF && c != '♠' && c != '♣' && c != '♥' && c != '♦' && c != '☺' && c != '☼' && c != '♪' && c != '♫') continue;
                if (c >= 0x2B00 && c <= 0x2BFF) continue;
                if (c >= 0x2900 && c <= 0x297F) continue;
                if (c >= 0x2300 && c <= 0x23FF) continue;
                if (c >= 0x2460 && c <= 0x24FF) continue;
                sb.Append(c);
            }
            // Doppelte Leerzeichen und führende Leerzeichen nach entfernten Symbolen glätten
            string r = sb.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            r = r.Replace("( )", "").Replace("()", "");
            return r.TrimStart();
        }
    }
}
