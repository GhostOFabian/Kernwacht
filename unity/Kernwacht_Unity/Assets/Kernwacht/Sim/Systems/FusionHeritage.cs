// Fusions-Erbe: Das Ergebnis einer Fusion hängt von den Spezialisierungen beider Elterntürme ab.
// Jeder Elternturm vererbt seine gewählte Spezialisierung mit halber Stärke (nur Werte, die der
// legendäre Turm auch nutzt). Dazu kommt ein Kombinationsbonus, wenn beide spezialisiert sind.
// 4.1: Jeder Turm hat nur noch eine Spezialisierung; Fusion geht auch ohne (dann ohne Erbe).
// "BB"/"AB" bleiben nur für ältere Spielstände.

using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class FusionCombo
    {
        public string id;
        public string name;
        public string description;
        public List<StatMod> mods;
    }

    public static class FusionHeritage
    {
        public const double SHARE = 0.5;

        public static readonly Dictionary<string, FusionCombo> COMBOS = new Dictionary<string, FusionCombo>
        {
            ["AA"] = new FusionCombo { id = "AA", name = "Volle Linie", description = "+15 % Schaden (beide Eltern spezialisiert)", mods = new List<StatMod> { new StatMod { stat = "damage", mul = 1.15 } } },
            ["BB"] = new FusionCombo { id = "BB", name = "Reine Linie B", description = "+15 % Angriffstempo", mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.87 } } },
            ["AB"] = new FusionCombo { id = "AB", name = "Mischlinie", description = "+0,4 Reichweite und +8 % kritische Trefferchance", mods = new List<StatMod> { new StatMod { stat = "range", add = 0.4 }, new StatMod { stat = "critChance", add = 0.08 } } },
        };

        public static FusionRecipe recipeOf(string fusedId)
        {
            return Towers.FUSIONS.FirstOrDefault((f) => f.id == fusedId);
        }

        public static string comboKey(double specA, double specB)
        {
            if (specA <= 0 || specB <= 0) return null;
            if (specA == specB) return specA == 1 ? "AA" : "BB";
            return "AB";
        }

        static UpgradeDefinition specOf(string towerId, double spec)
        {
            if (towerId == null || spec <= 0 || !Towers.TOWERS.TryGetValue(towerId, out var d) || d.specs == null) return null;
            int i = (int)spec - 1;
            return i >= 0 && i < d.specs.Count ? d.specs[i] : null;
        }

        /// <summary>Wendet das Erbe auf die Werte eines legendären Turms an (nach Stufen, vor Runen und Verstärkungen).</summary>
        public static void apply(Dictionary<string, double> st, TowerInstance t)
        {
            if (t.fuseA <= 0 && t.fuseB <= 0) return;
            var r = recipeOf(t.def);
            if (r == null) return;
            inherit(st, specOf(r.a, t.fuseA));
            inherit(st, specOf(r.b, t.fuseB));
            var key = comboKey(t.fuseA, t.fuseB);
            if (key == null) return;
            foreach (var m in COMBOS[key].mods)
            {
                // fehlende Werte nur bei Zuschlägen anlegen (z. B. kritische Trefferchance), nie mit NaN rechnen
                bool has = st.TryGetValue(m.stat, out var v);
                if (m.add != null) st[m.stat] = (has ? v : 0) + m.add.Value;
                if (m.mul != null && has && v != 0) st[m.stat] = st[m.stat] * m.mul.Value;
            }
        }

        static void inherit(Dictionary<string, double> st, UpgradeDefinition spec)
        {
            if (spec == null || spec.mods == null) return;
            foreach (var m in spec.mods)
            {
                // nur Werte, die der legendäre Turm tatsächlich hat – keine halben Fremdmechaniken
                if (!st.TryGetValue(m.stat, out var v) || v == 0) continue;
                if (m.add != null) st[m.stat] = v + m.add.Value * SHARE;
                if (m.mul != null) st[m.stat] = st[m.stat] * (1 + (m.mul.Value - 1) * SHARE);
            }
        }

        /// <summary>Kurzbeschreibung der Variante, z. B. für die Fusionsvorschau und das Turmfeld.</summary>
        public static string describe(string fusedId, double specA, double specB)
        {
            var r = recipeOf(fusedId);
            if (r == null) return "";
            var sa = specOf(r.a, specA);
            var sb = specOf(r.b, specB);
            var key = comboKey(specA, specB);
            var parts = new List<string>();
            if (sa != null) parts.Add($"{Towers.TOWERS[r.a].name}: {sa.name} (halb)");
            if (sb != null) parts.Add($"{Towers.TOWERS[r.b].name}: {sb.name} (halb)");
            string combo = key != null ? $" · {COMBOS[key].name}: {COMBOS[key].description}" : "";
            if (parts.Count == 0 && combo == "") return "keins (Eltern nicht spezialisiert)";
            return string.Join(" + ", parts) + combo;
        }
    }
}
