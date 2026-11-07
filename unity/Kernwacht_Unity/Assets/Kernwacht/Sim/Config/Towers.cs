// Übersetzung von config/towers.ts.
// Zentrale Turmkonfiguration. Kosten, Werte, Upgrades und Spezialisierungen sind vollständig
// datengetrieben. Jede Stufe verändert die Funktionsweise – nicht nur den Schaden.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class Towers
    {
        public static readonly Dictionary<string, TowerDefinition> TOWERS = TowersNew.Add(new Dictionary<string, TowerDefinition>
        {
            ["ranger"] = new TowerDefinition
            {
                id = "ranger",
                active = "overdrive",
                name = "Waldläuferposten",
                role = "Allrounder",
                icon = "🏹",
                hotkey = "1",
                color = 0x6fae4a,
                description = "Günstiger, schneller Bogenturm. Trifft Boden- und Luftziele.",
                damageType = "physical",
                cost = 60,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 9, ["range"] = 3.4, ["cooldown"] = 0.7, ["projectileSpeed"] = 14, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "ranger1", name = "Gespannte Sehnen", description = "+20 % Angriffstempo, +0,3 Reichweite", cost = 45, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.83 }, new StatMod { stat = "range", add = 0.3 } } },
                    new UpgradeDefinition { id = "ranger2", name = "Stahlspitzen", description = "+5 Schaden, ignoriert 20 % Panzerung", cost = 70, mods = new List<StatMod> { new StatMod { stat = "damage", add = 5 }, new StatMod { stat = "armorPierce", add = 0.2 } } },
                    new UpgradeDefinition { id = "ranger3", name = "Adlerauge", description = "+0,5 Reichweite, 10 % kritische Treffer (×2)", cost = 100, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.5 }, new StatMod { stat = "critChance", add = 0.1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "rangerA", name = "Scharfschütze", description = "+1,5 Reichweite, kritische Treffer 30 % (×2,5)", cost = 160, mods = new List<StatMod> { new StatMod { stat = "range", add = 1.5 }, new StatMod { stat = "critChance", add = 0.2 }, new StatMod { stat = "critMult", add = 0.5 }, new StatMod { stat = "damage", add = 6 } } },
                    new UpgradeDefinition { id = "rangerB", name = "Pfeilsalve", description = "Schießt gleichzeitig auf 3 Ziele", cost = 160, mods = new List<StatMod> { new StatMod { stat = "multishot", add = 2 } } },
                },
                synergies = "Profitiert von Runenmarkierungen und Netzwerkboni. Gut gegen Flieger.",
                counters = "Schwach gegen schwer gepanzerte Steinhäuter und Phasenschilde.",
            },
            ["ember"] = new TowerDefinition
            {
                id = "ember",
                active = "overdrive",
                name = "Glutwerfer",
                role = "Feuer · Fläche",
                icon = "🔥",
                hotkey = "2",
                color = 0xe8642c,
                description = "Schleudert Glutkugeln, die explodieren und Ziele in Brand setzen.",
                damageType = "fire",
                cost = 100,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 14, ["range"] = 3, ["cooldown"] = 1.5, ["projectileSpeed"] = 7, ["splash"] = 1.1, ["burnDps"] = 6, ["burnDur"] = 3 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "ember1", name = "Heißere Glut", description = "Brand +4/s", cost = 60, mods = new List<StatMod> { new StatMod { stat = "burnDps", add = 4 } } },
                    new UpgradeDefinition { id = "ember2", name = "Weite Streuung", description = "Explosionsradius +0,3, +0,3 Reichweite", cost = 90, mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.3 }, new StatMod { stat = "range", add = 0.3 } } },
                    new UpgradeDefinition { id = "ember3", name = "Blasebalg", description = "+25 % Angriffstempo, +6 Schaden", cost = 130, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 }, new StatMod { stat = "damage", add = 6 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "emberA", name = "Inferno", description = "Riesige Explosionen: Radius ×1,6, Schaden +60 %", cost = 220, mods = new List<StatMod> { new StatMod { stat = "splash", mul = 1.6 }, new StatMod { stat = "damage", mul = 1.6 } } },
                    new UpgradeDefinition { id = "emberB", name = "Schwelbrand", description = "Brand doppelt so lang, +50 % stärker und springt beim Tod auf Nachbarn über", cost = 220, mods = new List<StatMod> { new StatMod { stat = "burnDur", mul = 2 }, new StatMod { stat = "burnDps", mul = 1.5 }, new StatMod { stat = "burnSpread", add = 1 } } },
                },
                synergies = "Feuer auf vergiftete Ziele löst Giftbrand aus; Feuer auf unterkühlte Ziele erzeugt Dampfstoß (Nässe).",
                counters = "Kann keine Flieger treffen. Aschenkönigin und Sol sind feuerresistent, der Weltenbrecher teilweise.",
            },
            ["frost"] = new TowerDefinition
            {
                id = "frost",
                active = "deepfreeze",
                name = "Frostwächter",
                role = "Kontrolle · Verlangsamung",
                icon = "❄️",
                hotkey = "3",
                color = 0x7cc8f0,
                description = "Sendet Kältewellen aus, die alle Gegner in Reichweite verlangsamen.",
                damageType = "frost",
                cost = 90,
                attack = "pulse",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 4, ["range"] = 2.4, ["cooldown"] = 1.6, ["chillPct"] = 0.15, ["chillDur"] = 2.5, ["chillMax"] = 0.45, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "frost1", name = "Klirrende Kälte", description = "Verlangsamung je Stapel 20 %, Maximum 50 %", cost = 55, mods = new List<StatMod> { new StatMod { stat = "chillPct", add = 0.05 }, new StatMod { stat = "chillMax", add = 0.05 } } },
                    new UpgradeDefinition { id = "frost2", name = "Weiter Atem", description = "+0,5 Reichweite", cost = 80, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.5 } } },
                    new UpgradeDefinition { id = "frost3", name = "Eisige Präzision", description = "+25 % Pulstempo, Kälte hält 1 s länger", cost = 110, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 }, new StatMod { stat = "chillDur", add = 1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "frostA", name = "Permafrost", description = "Maximale Verlangsamung 65 %, Kälte +2 s", cost = 190, mods = new List<StatMod> { new StatMod { stat = "chillMax", add = 0.15 }, new StatMod { stat = "chillDur", add = 2 }, new StatMod { stat = "chillPct", add = 0.05 } } },
                    new UpgradeDefinition { id = "frostB", name = "Eissarg", description = "15 % Chance, Ziele 1,2 s einzufrieren (danach kurz immun)", cost = 190, mods = new List<StatMod> { new StatMod { stat = "freezeChance", add = 0.15 }, new StatMod { stat = "freezeDur", add = 1.2 }, new StatMod { stat = "damage", add = 6 } } },
                },
                synergies = "Kälte + Feuer = Dampfstoß; Feuer auf Kälte macht Ziele nass für Blitzschaden. Dämpft Feuerzonen der Aschenkönigin.",
                counters = "Geringer Schaden. Bosse können nicht eingefroren werden (verlieren aber Standfestigkeit). Luna widersteht Frost – Aschenkönigin, Sol und Aschenschwingen sind frostschwach.",
            },
            ["storm"] = new TowerDefinition
            {
                id = "storm",
                active = "overload",
                name = "Sturmrufer",
                role = "Blitz · Ketten",
                icon = "⚡",
                hotkey = "5",
                color = 0xf2d74a,
                description = "Entfesselt Blitze, die zwischen mehreren Gegnern überspringen.",
                damageType = "lightning",
                cost = 130,
                attack = "chain",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 20, ["range"] = 3.2, ["cooldown"] = 1.4, ["chains"] = 2, ["chainFalloff"] = 0.7, ["chainRange"] = 2, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "storm1", name = "Leitende Luft", description = "+1 Kettensprung", cost = 75, mods = new List<StatMod> { new StatMod { stat = "chains", add = 1 } } },
                    new UpgradeDefinition { id = "storm2", name = "Gewitterzelle", description = "+0,4 Reichweite, Sprungweite +0,4", cost = 100, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.4 }, new StatMod { stat = "chainRange", add = 0.4 } } },
                    new UpgradeDefinition { id = "storm3", name = "Hochspannung", description = "+8 Schaden, weniger Abschwächung je Sprung", cost = 140, mods = new List<StatMod> { new StatMod { stat = "damage", add = 8 }, new StatMod { stat = "chainFalloff", add = 0.1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "stormA", name = "Kettenblitz", description = "+3 Kettensprünge, kaum Abschwächung", cost = 230, mods = new List<StatMod> { new StatMod { stat = "chains", add = 3 }, new StatMod { stat = "chainFalloff", add = 0.1 } } },
                    new UpgradeDefinition { id = "stormB", name = "Donnerkeil", description = "Hauptziel erhält ×2,5 Schaden und wird 0,3 s betäubt", cost = 230, mods = new List<StatMod> { new StatMod { stat = "damage", mul = 2.5 }, new StatMod { stat = "stunDur", add = 0.3 }, new StatMod { stat = "chains", add = -1 } } },
                },
                synergies = "Nasse Ziele erleiden Überladung (+50 % Blitzschaden, +1 Sprung).",
                counters = "Schildträger passen sich wiederholtem Blitzschaden an. Funkenfresser widerstehen Blitz (−50 %), der Nullhüter teilweise.",
            },
            ["spore"] = new TowerDefinition
            {
                id = "spore",
                active = "overdrive",
                name = "Sporenwerfer",
                role = "Gift · Schaden über Zeit",
                icon = "🍄",
                hotkey = "7",
                color = 0x9bd34a,
                description = "Wirft Sporenkapseln, die stapelbares Gift auftragen. Gift unterbindet Regeneration.",
                damageType = "poison",
                cost = 95,
                attack = "projectile",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 4, ["range"] = 3.2, ["cooldown"] = 1.2, ["projectileSpeed"] = 6, ["poisonDps"] = 4, ["poisonDur"] = 5, ["poisonMaxStacks"] = 5, ["splash"] = 0.5 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "spore1", name = "Potente Sporen", description = "Gift +2/s je Stapel", cost = 55, mods = new List<StatMod> { new StatMod { stat = "poisonDps", add = 2 } } },
                    new UpgradeDefinition { id = "spore2", name = "Schleuderarm", description = "+0,5 Reichweite, +15 % Tempo", cost = 85, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.5 }, new StatMod { stat = "cooldown", mul = 0.87 } } },
                    new UpgradeDefinition { id = "spore3", name = "Zähe Sporen", description = "Gift hält 2 s länger, Kapselradius +0,3", cost = 110, mods = new List<StatMod> { new StatMod { stat = "poisonDur", add = 2 }, new StatMod { stat = "splash", add = 0.3 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "sporeA", name = "Seuchenwolke", description = "Hinterlässt 3 s eine Giftwolke, die weiter Stapel aufträgt", cost = 200, mods = new List<StatMod> { new StatMod { stat = "cloudDur", add = 3 }, new StatMod { stat = "cloudRadius", add = 1.1 } } },
                    new UpgradeDefinition { id = "sporeB", name = "Nervengift", description = "Max. 8 Stapel, vergiftete Ziele sind 20 % langsamer", cost = 200, mods = new List<StatMod> { new StatMod { stat = "poisonMaxStacks", add = 3 }, new StatMod { stat = "poisonSlow", add = 0.2 }, new StatMod { stat = "poisonDps", add = 2 } } },
                },
                synergies = "Kontert Regenerierer. Feuer auf Gift löst Giftbrand aus.",
                counters = "Kann keine Flieger treffen. Splitterschwärme, Brutmütter und Höhlengolems widerstehen Gift.",
            },
            ["ballista"] = new TowerDefinition
            {
                id = "ballista",
                active = "overdrive",
                name = "Belagerungsarmbrust",
                role = "Einzelschaden · Panzerbrecher",
                icon = "🎯",
                hotkey = "8",
                color = 0xa07850,
                description = "Feuert schwere Bolzen mit großer Reichweite, die Panzerung teilweise ignorieren.",
                damageType = "physical",
                cost = 150,
                attack = "projectile",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 62, ["range"] = 4.6, ["cooldown"] = 2.3, ["projectileSpeed"] = 18, ["armorPierce"] = 0.5 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "ballista1", name = "Torsionsfedern", description = "+15 % Tempo, +0,4 Reichweite", cost = 90, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.87 }, new StatMod { stat = "range", add = 0.4 } } },
                    new UpgradeDefinition { id = "ballista2", name = "Wolframbolzen", description = "Panzerbrechend 75 %, +15 Schaden", cost = 120, mods = new List<StatMod> { new StatMod { stat = "armorPierce", add = 0.25 }, new StatMod { stat = "damage", add = 15 } } },
                    new UpgradeDefinition { id = "ballista3", name = "Widerhaken", description = "Treffer machen Ziele 3 s spröde (+30 % physischer Schaden)", cost = 150, mods = new List<StatMod> { new StatMod { stat = "brittleDur", add = 3 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "ballistaA", name = "Durchschlag", description = "Bolzen durchschlagen bis zu 4 Gegner in einer Linie", cost = 250, mods = new List<StatMod> { new StatMod { stat = "pierce", add = 4 }, new StatMod { stat = "damage", add = 20 } } },
                    new UpgradeDefinition { id = "ballistaB", name = "Kolossjäger", description = "+1,5 % der max. LP als Zusatzschaden (Bosse: 0,4 %), höchstens das 3-Fache des eigenen Treffers", cost = 250, mods = new List<StatMod> { new StatMod { stat = "maxHpPct", add = 0.015 }, new StatMod { stat = "damage", mul = 1.25 } } },
                },
                synergies = "Panzerbrechend: ideal gegen Steinhäuter, Golems und den Weltenbrecher. Spröde-Effekt verstärkt alle physischen Türme.",
                counters = "Langsam, schwach gegen Schwärme, trifft keine Flieger. Phasenwandler werden zeitweise immun.",
            },
            ["rune"] = new TowerDefinition
            {
                id = "rune",
                active = "nova",
                detector = true,
                name = "Runenweber",
                role = "Arkan · Schwächung",
                icon = "🔮",
                hotkey = "-",
                color = 0xb36cf0,
                description = "Arkane Strahlen markieren Gegner und senken alle ihre Resistenzen.",
                damageType = "arcane",
                cost = 120,
                attack = "beam",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 15, ["range"] = 3.6, ["cooldown"] = 1, ["markPct"] = 0.2, ["markDur"] = 4, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "rune1", name = "Tiefe Runen", description = "Markierung hält 2 s länger", cost = 70, mods = new List<StatMod> { new StatMod { stat = "markDur", add = 2 } } },
                    new UpgradeDefinition { id = "rune2", name = "Fokuskristall", description = "+0,4 Reichweite, +5 Schaden", cost = 95, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.4 }, new StatMod { stat = "damage", add = 5 } } },
                    new UpgradeDefinition { id = "rune3", name = "Zerfallsrune", description = "Markierung senkt Resistenzen um 28 %", cost = 130, mods = new List<StatMod> { new StatMod { stat = "markPct", add = 0.08 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "runeA", name = "Runenbruch", description = "Markierung −40 % und springt auf 2 Gegner in der Nähe über", cost = 220, mods = new List<StatMod> { new StatMod { stat = "markPct", add = 0.12 }, new StatMod { stat = "markSpread", add = 2 } } },
                    new UpgradeDefinition { id = "runeB", name = "Arkanfokus", description = "Schaden steigt pro Treffer am selben Ziel um 30 % (max. +240 %)", cost = 220, mods = new List<StatMod> { new StatMod { stat = "rampPct", add = 0.3 }, new StatMod { stat = "rampMax", add = 8 } } },
                },
                synergies = "Markierte Ziele erleiden mehr Schaden aus allen Quellen. Bricht den Nullschild des Nullhüters effizient.",
                counters = "Geringer Grundschaden gegen Schwärme. Der Risskoloss widersteht Arkan.",
            },
            ["resonator"] = new TowerDefinition
            {
                id = "resonator",
                active = "nova",
                detector = true,
                name = "Resonator",
                role = "Schall · Unterbrechung",
                icon = "🔔",
                hotkey = "=",
                color = 0x5fd0c0,
                description = "Schallwellen unterbrechen gegnerische Fähigkeiten und erschüttern die Standfestigkeit von Bossen.",
                damageType = "sonic",
                cost = 110,
                attack = "pulse",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 7, ["range"] = 2.6, ["cooldown"] = 2, ["interrupt"] = 25, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "reson1", name = "Klangschale", description = "+0,4 Reichweite", cost = 60, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.4 } } },
                    new UpgradeDefinition { id = "reson2", name = "Obertöne", description = "+40 % Unterbrechung, +5 Schaden", cost = 90, mods = new List<StatMod> { new StatMod { stat = "interrupt", mul = 1.4 }, new StatMod { stat = "damage", add = 5 } } },
                    new UpgradeDefinition { id = "reson3", name = "Taktgeber", description = "+25 % Pulstempo", cost = 120, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "resonA", name = "Schockwelle", description = "Stößt Gegner eine Zelle zurück (nicht Bosse), Schaden ×2", cost = 200, mods = new List<StatMod> { new StatMod { stat = "knockback", add = 1 }, new StatMod { stat = "damage", mul = 2 } } },
                    new UpgradeDefinition { id = "resonB", name = "Stille", description = "Getroffene Gegner können 4 s keine Fähigkeiten nutzen, doppelte Unterbrechung", cost = 200, mods = new List<StatMod> { new StatMod { stat = "silenceDur", add = 4 }, new StatMod { stat = "interrupt", mul = 2 } } },
                },
                synergies = "Zerschmettert gefrorene Ziele (Resonanzbruch). Kontert Saboteure, Phasenwandler und Bossfähigkeiten.",
                counters = "Geringer Schaden – braucht unterstützende Türme.",
            },
            ["goldmine"] = new TowerDefinition
            {
                id = "goldmine",
                size = 2,
                active = "goldrush",
                name = "Goldgräber",
                role = "Wirtschaft",
                icon = "⛏️",
                hotkey = "4",
                color = 0xf0c040,
                description = "Fördert nach jeder überstandenen Welle Gold. Auf Goldadern +50 %. Zahlt erst ab der Welle nach dem Bau.",
                damageType = null,
                cost = 110,
                attack = "economy",
                defaultPriority = "first",
                networkable = false,
                @base = new Dictionary<string, double> { ["income"] = 14, ["range"] = 0 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "mine1", name = "Stützbalken", description = "+5 Gold je Welle", cost = 70, mods = new List<StatMod> { new StatMod { stat = "income", add = 5 } } },
                    new UpgradeDefinition { id = "mine2", name = "Lorenbahn", description = "+6 Gold je Welle", cost = 90, mods = new List<StatMod> { new StatMod { stat = "income", add = 6 } } },
                    new UpgradeDefinition { id = "mine3", name = "Sprengmeister", description = "+8 Gold je Welle", cost = 120, mods = new List<StatMod> { new StatMod { stat = "income", add = 8 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "mineA", name = "Tiefenbohrung", description = "Einkommen +60 %", cost = 200, mods = new List<StatMod> { new StatMod { stat = "income", mul = 1.6 } } },
                    new UpgradeDefinition { id = "mineB", name = "Kopfgeldbüro", description = "Abschüsse im Umkreis von 3 Zellen: +2 Gold, Elite +20", cost = 200, mods = new List<StatMod> { new StatMod { stat = "killBounty", add = 2 }, new StatMod { stat = "eliteBounty", add = 20 }, new StatMod { stat = "bountyRange", add = 3 } } },
                },
                synergies = "Auf Goldadern bauen. Lohnt sich bei früher Platzierung.",
                counters = "Kein Kampfwert – zu viele Minen kosten frühe Verteidigung. Amortisation ca. 5 Wellen auf Goldadern, sonst ca. 8 Wellen.",
            },
            ["pylon"] = new TowerDefinition
            {
                id = "pylon",
                active = "surge",
                name = "Leitpylon",
                role = "Netzwerk · Verstärkung",
                icon = "📡",
                hotkey = "i",
                color = 0x4fa8ff,
                description = "Verbindet Türme in Reichweite zu einem Netzwerk: verbundene Türme erhalten Schaden und Tempo. Kombinationen lösen Resonanzen aus.",
                damageType = "lightning",
                cost = 80,
                attack = "pylon",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["linkRange"] = 3, ["maxLinks"] = 4, ["networkBonus"] = 0.12, ["zapDamage"] = 8, ["range"] = 2.4, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "pylon1", name = "Verstärkte Spulen", description = "Verbindungsreichweite +1", cost = 50, mods = new List<StatMod> { new StatMod { stat = "linkRange", add = 1 } } },
                    new UpgradeDefinition { id = "pylon2", name = "Verteiler", description = "+2 Verbindungen", cost = 70, mods = new List<StatMod> { new StatMod { stat = "maxLinks", add = 2 } } },
                    new UpgradeDefinition { id = "pylon3", name = "Resonanzkern", description = "Netzwerkbonus 18 %", cost = 100, mods = new List<StatMod> { new StatMod { stat = "networkBonus", add = 0.06 }, new StatMod { stat = "zapDamage", add = 6 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "pylonA", name = "Weitleiter", description = "Verbindungsreichweite +2, +3 Verbindungen", cost = 180, mods = new List<StatMod> { new StatMod { stat = "linkRange", add = 2 }, new StatMod { stat = "maxLinks", add = 3 } } },
                    new UpgradeDefinition { id = "pylonB", name = "Überladung", description = "Netzwerkbonus ×1,8 und der Pylon schießt selbst Blitze", cost = 180, mods = new List<StatMod> { new StatMod { stat = "networkBonus", mul = 1.8 }, new StatMod { stat = "damage", add = 22 } } },
                },
                synergies = "Resonanzen: Feuer+Frost+Blitz (Elementarresonanz), Rune+Rissbinder+Resonator (Arkankreis), Armbrust+Kanone+Waldläufer (Belagerungsverbund).",
                counters = "Saboteure und der Nullhüter stören Netzwerke.",
            },
            ["cannon"] = new TowerDefinition
            {
                id = "cannon",
                active = "overdrive",
                name = "Kanonenschmiede",
                role = "Physisch · Fläche",
                icon = "💣",
                hotkey = "6",
                color = 0x8a8f99,
                description = "Feuert Granaten mit großem Explosionsradius.",
                damageType = "physical",
                cost = 140,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 40, ["range"] = 3.8, ["cooldown"] = 2.2, ["projectileSpeed"] = 6.5, ["splash"] = 1.3 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "cannon1", name = "Schwarzpulver", description = "+10 Schaden", cost = 80, mods = new List<StatMod> { new StatMod { stat = "damage", add = 10 } } },
                    new UpgradeDefinition { id = "cannon2", name = "Längeres Rohr", description = "+0,6 Reichweite, Radius +0,2", cost = 110, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.6 }, new StatMod { stat = "splash", add = 0.2 } } },
                    new UpgradeDefinition { id = "cannon3", name = "Ladekran", description = "+25 % Feuertempo", cost = 140, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "cannonA", name = "Streubombe", description = "Granate zerfällt in 4 Splitterexplosionen", cost = 240, mods = new List<StatMod> { new StatMod { stat = "fragments", add = 4 } } },
                    new UpgradeDefinition { id = "cannonB", name = "Erschütterung", description = "Betäubt 0,6 s und macht Ziele 3 s spröde", cost = 240, mods = new List<StatMod> { new StatMod { stat = "stunDur", add = 0.6 }, new StatMod { stat = "brittleDur", add = 3 }, new StatMod { stat = "damage", add = 10 } } },
                },
                synergies = "Stark gegen Schwärme und Brutlinge. Spröde verstärkt physische Türme.",
                counters = "Trifft keine Flieger. Steinhäuter, Höhlengolems und Belagerungsbestien widerstehen physischem Schaden.",
            },
            ["riftbinder"] = new TowerDefinition
            {
                id = "riftbinder",
                active = "rewind",
                name = "Rissbinder",
                role = "Raumkontrolle",
                icon = "🌀",
                hotkey = "k",
                color = 0xd04ad0,
                description = "Setzt Rissmale. Nach kurzer Zeit wird das Ziel durch ein Portal auf dem Weg zurückversetzt.",
                damageType = "arcane",
                cost = 135,
                attack = "rift",
                defaultPriority = "exit",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 10, ["range"] = 3.4, ["cooldown"] = 3, ["riftBack"] = 3, ["riftDelay"] = 1.2, ["hitsAir"] = 1, ["riftTargets"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "rift1", name = "Stabile Risse", description = "Rückversetzung +1 Zelle", cost = 80, mods = new List<StatMod> { new StatMod { stat = "riftBack", add = 1 } } },
                    new UpgradeDefinition { id = "rift2", name = "Schnelle Bindung", description = "+20 % Tempo, +0,4 Reichweite", cost = 110, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.83 }, new StatMod { stat = "range", add = 0.4 } } },
                    new UpgradeDefinition { id = "rift3", name = "Zwillingsriss", description = "Markiert 2 Ziele gleichzeitig", cost = 150, mods = new List<StatMod> { new StatMod { stat = "riftTargets", add = 1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "riftA", name = "Weltenriss", description = "Rückversetzung +2 Zellen, +1 Ziel", cost = 240, mods = new List<StatMod> { new StatMod { stat = "riftBack", add = 2 }, new StatMod { stat = "riftTargets", add = 1 } } },
                    new UpgradeDefinition { id = "riftB", name = "Fesselriss", description = "Gemarkte Ziele werden 1 s festgehalten und erleiden 4 s lang +25 % Schaden", cost = 240, mods = new List<StatMod> { new StatMod { stat = "rootDur", add = 1 }, new StatMod { stat = "tetherPct", add = 0.25 }, new StatMod { stat = "damage", add = 15 } } },
                },
                synergies = "Verlängert die Zeit unter Feuer aller Türme. Teil des Arkankreises.",
                counters = "Bosse sind immun gegen Versetzung (erleiden stattdessen +15 % Schaden). Abklingzeit pro Gegner 8 s.",
            },
            ["meteor"] = new TowerDefinition
            {
                id = "meteor",
                active = "meteorstorm",
                name = "Meteorbeschwörer",
                role = "Feuer · Riesiger Einschlag",
                icon = "☄️",
                hotkey = "n",
                color = 0xff5a2a,
                description = "Ruft nach kurzer Ankündigung einen Meteor herab, der eine große Fläche verwüstet und in Brand setzt.",
                damageType = "fire",
                cost = 210,
                attack = "meteor",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 75, ["range"] = 5.2, ["cooldown"] = 4.6, ["splash"] = 1.6, ["burnDps"] = 8, ["burnDur"] = 2.5, ["meteorDelay"] = 1.1, ["meteorCount"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "meteor1", name = "Schwere Brocken", description = "+30 Schaden", cost = 120, mods = new List<StatMod> { new StatMod { stat = "damage", add = 30 } } },
                    new UpgradeDefinition { id = "meteor2", name = "Sternkarte", description = "+0,8 Reichweite, schnellerer Einschlag", cost = 150, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.8 }, new StatMod { stat = "meteorDelay", mul = 0.75 } } },
                    new UpgradeDefinition { id = "meteor3", name = "Glutkern", description = "Einschlagradius +0,4, Brand +6/s", cost = 190, mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.4 }, new StatMod { stat = "burnDps", add = 6 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "meteorA", name = "Sternenschauer", description = "3 kleinere Meteore pro Angriff", cost = 300, mods = new List<StatMod> { new StatMod { stat = "meteorCount", add = 2 }, new StatMod { stat = "damage", mul = 0.6 } } },
                    new UpgradeDefinition { id = "meteorB", name = "Weltenhammer", description = "Doppelter Schaden, betäubt 1 s", cost = 300, mods = new List<StatMod> { new StatMod { stat = "damage", mul = 2 }, new StatMod { stat = "stunDur", add = 1 }, new StatMod { stat = "cooldown", mul = 1.2 } } },
                },
                synergies = "Ideal mit dem Gravitationsbrunnen, der Gegner bündelt. Feuer auf Gift löst Giftbrand aus.",
                counters = "Langsam – schnelle Läufer entkommen dem Einschlag. Trifft keine Flieger.",
            },
            ["tesla"] = new TowerDefinition
            {
                id = "tesla",
                active = "overload",
                name = "Tesla-Spule",
                role = "Blitz · Lichtbogen",
                icon = "🌩️",
                hotkey = "l",
                color = 0x7ad0ff,
                description = "Zappt nahe Gegner und spannt einen dauerhaften Lichtbogen zu anderen Tesla-Spulen in Reichweite. Wer den Bogen kreuzt, wird gegrillt.",
                damageType = "lightning",
                cost = 140,
                attack = "tesla",
                defaultPriority = "close",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 9, ["range"] = 2.4, ["cooldown"] = 0.5, ["arcDps"] = 32, ["arcRange"] = 4.2, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "tesla1", name = "Kupferwicklung", description = "Lichtbogen +12/s", cost = 80, mods = new List<StatMod> { new StatMod { stat = "arcDps", add = 12 } } },
                    new UpgradeDefinition { id = "tesla2", name = "Hochspannungsmast", description = "Bogenreichweite +1,2", cost = 110, mods = new List<StatMod> { new StatMod { stat = "arcRange", add = 1.2 } } },
                    new UpgradeDefinition { id = "tesla3", name = "Kondensator", description = "Zapp +8 Schaden, springt 1× über", cost = 140, mods = new List<StatMod> { new StatMod { stat = "damage", add = 8 }, new StatMod { stat = "chains", add = 1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "teslaA", name = "Gewitterzaun", description = "Lichtbogen ×1,8", cost = 230, mods = new List<StatMod> { new StatMod { stat = "arcDps", mul = 1.8 } } },
                    new UpgradeDefinition { id = "teslaB", name = "Kugelblitz", description = "Zapp ×3 und betäubt kurz", cost = 230, mods = new List<StatMod> { new StatMod { stat = "damage", mul = 3 }, new StatMod { stat = "stunDur", add = 0.25 } } },
                },
                synergies = "Paarweise bauen, sodass der Bogen den Weg kreuzt. Nasse Gegner erleiden Überladung.",
                counters = "Allein schwach. Schildträger passen sich an Blitzschaden an, Funkenfresser widerstehen Blitz.",
            },
            ["gravity"] = new TowerDefinition
            {
                id = "gravity",
                active = "nova",
                name = "Gravitationsbrunnen",
                role = "Kontrolle · Bündeln",
                icon = "🕳️",
                hotkey = "m",
                color = 0x8a6aff,
                description = "Erzeugt Gravitationswellen, die vorauslaufende Gegner zurückziehen und verlangsamen – die Gegner ballen sich zusammen.",
                damageType = "arcane",
                cost = 150,
                attack = "gravity",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 8, ["range"] = 2.6, ["cooldown"] = 3.2, ["pullCells"] = 1, ["chillPct"] = 0.25, ["chillDur"] = 1.5, ["chillMax"] = 0.5 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "grav1", name = "Dichter Kern", description = "+0,4 Reichweite", cost = 90, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.4 } } },
                    new UpgradeDefinition { id = "grav2", name = "Ereignishorizont", description = "Sog +1 Feld", cost = 130, mods = new List<StatMod> { new StatMod { stat = "pullCells", add = 1 } } },
                    new UpgradeDefinition { id = "grav3", name = "Pulsar", description = "+25 % Pulstempo, +10 Schaden", cost = 150, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 }, new StatMod { stat = "damage", add = 10 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "gravA", name = "Singularität", description = "Sog +1, Gegner werden 0,8 s festgehalten", cost = 240, mods = new List<StatMod> { new StatMod { stat = "pullCells", add = 1 }, new StatMod { stat = "rootDur", add = 0.8 } } },
                    new UpgradeDefinition { id = "gravB", name = "Zermalmer", description = "Schaden ×3 und spröde", cost = 240, mods = new List<StatMod> { new StatMod { stat = "damage", mul = 3 }, new StatMod { stat = "brittleDur", add = 3 } } },
                },
                synergies = "Bündelt Gegner für Meteore, Kanonen und Glutwerfer.",
                counters = "Wirkt nicht auf Bosse und Flieger (nur Verlangsamung).",
            },
            ["dragon"] = new TowerDefinition
            {
                id = "dragon",
                active = "overdrive",
                name = "Drachenhorst",
                role = "Feuer · Flieger-Abwehr",
                icon = "🐉",
                hotkey = "+",
                color = 0xd04a3a,
                description = "Ein junger Drache kreist über dem Horst und speit Feuer auf Boden- und Luftziele. Greift Flieger zuerst an und verursacht an ihnen 2,5-fachen Schaden.",
                damageType = "fire",
                cost = 190,
                attack = "dragon",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 34, ["range"] = 3.4, ["cooldown"] = 1.5, ["splash"] = 1, ["burnDps"] = 10, ["burnDur"] = 3, ["hitsAir"] = 1, ["airDmgMul"] = 2.5, ["preferAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "dragon1", name = "Wachstum", description = "+10 Schaden", cost = 110, mods = new List<StatMod> { new StatMod { stat = "damage", add = 10 } } },
                    new UpgradeDefinition { id = "dragon2", name = "Weite Schwingen", description = "+0,6 Reichweite", cost = 130, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.6 } } },
                    new UpgradeDefinition { id = "dragon3", name = "Glutatem", description = "Atem-Radius +0,4, Brand +5/s", cost = 160, mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.4 }, new StatMod { stat = "burnDps", add = 5 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "dragonA", name = "Ältester Drache", description = "Schaden ×1,8, Brand springt über", cost = 280, mods = new List<StatMod> { new StatMod { stat = "damage", mul = 1.8 }, new StatMod { stat = "burnSpread", add = 1 } } },
                    new UpgradeDefinition { id = "dragonB", name = "Sturmdrache", description = "Atem wird zu Blitzschlägen mit 3 Kettensprüngen", cost = 280, mods = new List<StatMod> { new StatMod { stat = "chains", add = 3 }, new StatMod { stat = "cooldown", mul = 0.8 } } },
                },
                synergies = "Flieger-Jäger: Flieger zuerst, ×2,5 Schaden gegen Sturmgreife und Aschenschwingen. Frost auf brennende Ziele löst Thermoschock aus.",
                counters = "Feuerresistente Gegner (Aschenkönigin, Zwilling Sol, Weltenbrecher).",
            },
            ["laser"] = new TowerDefinition
            {
                id = "laser",
                active = "nova",
                name = "Kristalllaser",
                role = "Arkan · Durchdringender Strahl",
                icon = "💎",
                hotkey = "#",
                color = 0x60f0e0,
                description = "Ein Kristallstrahl, der mit der Zeit stärker wird und alle Gegner in seiner Linie durchdringt. Mehrere Laser dicht beieinander stören sich (−12 % je Nachbarlaser in 3 Feldern), direkt am Riss −35 %.",
                damageType = "arcane",
                cost = 200,
                attack = "laser",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 3.4, ["range"] = 3.8, ["cooldown"] = 0.1, ["laserRamp"] = 3, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "laser1", name = "Reiner Kristall", description = "+1,2 Schaden je Puls", cost = 110, mods = new List<StatMod> { new StatMod { stat = "damage", add = 1.2 } } },
                    new UpgradeDefinition { id = "laser2", name = "Fokuslinse", description = "+0,6 Reichweite", cost = 140, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.6 } } },
                    new UpgradeDefinition { id = "laser3", name = "Resonanzgitter", description = "Maximaler Aufbau ×4 statt ×3", cost = 170, mods = new List<StatMod> { new StatMod { stat = "laserRamp", add = 1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "laserA", name = "Prisma", description = "Zwei zusätzliche Strahlen auf weitere Ziele", cost = 280, mods = new List<StatMod> { new StatMod { stat = "multishot", add = 2 }, new StatMod { stat = "damage", mul = 0.75 } } },
                    new UpgradeDefinition { id = "laserB", name = "Sonnenlanze", description = "Aufbau ×5, −15 % Schaden, markiert Ziele (−12 % Resistenzen, nie unter 0)", cost = 280, mods = new List<StatMod> { new StatMod { stat = "laserRamp", add = 1 }, new StatMod { stat = "damage", mul = 0.85 }, new StatMod { stat = "markPct", add = 0.12 }, new StatMod { stat = "markDur", add = 2 } } },
                },
                synergies = "Perfekt an langen Geraden. Profitiert stark von Netzwerkboni.",
                counters = "Zielwechsel setzen den Aufbau zurück. Schwach gegen verstreute Schwärme. Der Risskoloss widersteht Arkan. Nicht stapeln und nicht direkt an den Riss stellen.",
            },
            ["totem"] = new TowerDefinition
            {
                id = "totem",
                active = "nova",
                detector = true,
                name = "Totemschrein",
                role = "Unterstützung · Aura",
                icon = "🗿",
                hotkey = "j",
                color = 0xc0a060,
                description = "Verstärkt alle Türme in seiner Aura und deckt getarnte Gegner auf. Greift nicht selbst an.",
                damageType = "arcane",
                cost = 160,
                attack = "aura",
                defaultPriority = "first",
                networkable = false,
                @base = new Dictionary<string, double> { ["range"] = 2.6, ["auraDmg"] = 0.15, ["auraSpeed"] = 0.1, ["damage"] = 0 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "totem1", name = "Geschnitzte Runen", description = "Aura +0,5 Reichweite", cost = 90, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.5 } } },
                    new UpgradeDefinition { id = "totem2", name = "Ahnenruf", description = "Aura-Schaden +10 %", cost = 130, mods = new List<StatMod> { new StatMod { stat = "auraDmg", add = 0.1 } } },
                    new UpgradeDefinition { id = "totem3", name = "Trommeln", description = "Aura-Tempo +10 %", cost = 160, mods = new List<StatMod> { new StatMod { stat = "auraSpeed", add = 0.1 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "totemA", name = "Kriegstotem", description = "Aura-Schaden +20 %", cost = 260, mods = new List<StatMod> { new StatMod { stat = "auraDmg", add = 0.2 } } },
                    new UpgradeDefinition { id = "totemB", name = "Weitsicht-Totem", description = "Türme in der Aura +0,6 Reichweite (über Weitsicht-Aura)", cost = 260, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.6 }, new StatMod { stat = "auraSpeed", add = 0.1 } } },
                },
                synergies = "Mitten in eine Turmgruppe stellen. Mehrere Totems stapeln nicht – der stärkste zählt.",
                counters = "Kein eigener Schaden. Feuerzonen und Erdstöße treffen dichte Gruppen.",
            },
            ["plasma"] = new TowerDefinition
            {
                id = "plasma",
                legendary = true,
                active = "overload",
                name = "Plasmawerfer",
                role = "Legendär · Feuer & Blitz",
                icon = "🔆",
                hotkey = "",
                color = 0xff8af0,
                description = "Fusion aus Glutwerfer und Sturmrufer: Plasmablitze springen zwischen Gegnern und setzen sie in Brand.",
                damageType = "lightning",
                cost = 0,
                attack = "chain",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 70, ["range"] = 3.8, ["cooldown"] = 1.1, ["chains"] = 5, ["chainFalloff"] = 0.85, ["chainRange"] = 2.4, ["burnDps"] = 14, ["burnDur"] = 3, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Entfaltet sich in dichten Wellen. Verbrennt Regenerierer.",
                counters = "Blitzresistente Saboteure.",
            },
            ["glacier"] = new TowerDefinition
            {
                id = "glacier",
                legendary = true,
                active = "deepfreeze",
                name = "Gletscherkanone",
                role = "Legendär · Frost & Fläche",
                icon = "🧊",
                hotkey = "",
                color = 0x9ae8ff,
                description = "Fusion aus Frostwächter und Kanonenschmiede: Eisgranaten zerspringen und frieren ganze Gruppen ein.",
                damageType = "frost",
                cost = 0,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 140, ["range"] = 4.2, ["cooldown"] = 2, ["projectileSpeed"] = 7, ["splash"] = 1.8, ["chillPct"] = 0.3, ["chillDur"] = 3, ["chillMax"] = 0.7, ["freezeChance"] = 0.25, ["freezeDur"] = 1.2 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Resonatoren zerschmettern Eingefrorene.",
                counters = "Bosse frieren nicht ein.",
            },
            ["voidlance"] = new TowerDefinition
            {
                id = "voidlance",
                legendary = true,
                active = "overdrive",
                name = "Leerenlanze",
                role = "Legendär · Arkan & Durchschlag",
                icon = "🗡️",
                hotkey = "",
                color = 0x9a5aff,
                description = "Fusion aus Belagerungsarmbrust und Runenweber: arkane Lanzen durchbohren ganze Reihen und markieren alles.",
                damageType = "arcane",
                cost = 0,
                attack = "projectile",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 230, ["range"] = 6, ["cooldown"] = 2.2, ["projectileSpeed"] = 20, ["pierce"] = 6, ["markPct"] = 0.35, ["markDur"] = 5, ["armorPierce"] = 1, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Bricht Schilde und Schwachpunkte.",
                counters = "Langsam gegen Schwärme.",
            },
            ["starfall"] = new TowerDefinition
            {
                id = "starfall",
                legendary = true,
                active = "meteorstorm",
                name = "Sternenfall",
                role = "Legendär · Kosmischer Einschlag",
                icon = "🌠",
                hotkey = "",
                color = 0xffe080,
                description = "Fusion aus Meteorbeschwörer und Kristalllaser: lässt einen Hagel aus Sternensplittern niedergehen.",
                damageType = "arcane",
                cost = 0,
                attack = "meteor",
                defaultPriority = "strong",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 140, ["range"] = 6.5, ["cooldown"] = 3.4, ["splash"] = 2, ["fragments"] = 3, ["meteorDelay"] = 0.9, ["meteorCount"] = 2, ["hitsAir"] = 1, ["markPct"] = 0.15, ["markDur"] = 3 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Vernichtet Bossgefolge.",
                counters = "Hohe Ankündigungszeit.",
            },
            ["thornwood"] = new TowerDefinition
            {
                id = "thornwood",
                legendary = true,
                active = "overdrive",
                name = "Dornenhain",
                role = "Legendär · Physisch & Rüstungsbruch",
                icon = "🌿",
                hotkey = "",
                color = 0x7ad04a,
                description = "Fusion aus Waldläuferposten und Sporenwerfer: Dornenpfeile durchbohren mehrere Gegner, reißen blutende Wunden (physischer Schaden über Zeit) und zerfetzen die Rüstung – bis zu 8 Stapel, je Stapel −5 % physische Resistenz.",
                damageType = "physical",
                cost = 0,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 48, ["range"] = 4.6, ["cooldown"] = 0.55, ["projectileSpeed"] = 15, ["pierce"] = 3, ["bleedDps"] = 14, ["bleedDur"] = 5, ["bleedMaxStacks"] = 8, ["shredPct"] = 0.05, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Rüstungsbruch verstärkt alle physischen Türme (Waldläufer, Armbrust, Kanone, Minenwerfer). Stoppt jede Regeneration.",
                counters = "Phasenwandler (zeitweise physisch immun). Steinhäuter und Golems dämpfen den Treffer – der Rüstungsbruch gleicht das mit jedem Stapel aus.",
            },
            ["thunderspire"] = new TowerDefinition
            {
                id = "thunderspire",
                legendary = true,
                active = "overload",
                name = "Donnerspitze",
                role = "Legendär · Blitznetz",
                icon = "⚡",
                hotkey = "",
                color = 0x7ac8ff,
                description = "Fusion aus Tesla-Spule und Leitpylon: zappt im Takt und spannt weitreichende Lichtbögen zu allen Spulen in der Nähe.",
                damageType = "lightning",
                cost = 0,
                attack = "tesla",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 44, ["range"] = 3.2, ["cooldown"] = 0.35, ["arcDps"] = 140, ["arcRange"] = 5.5, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Bildet Lichtbögen mit Tesla-Spulen. Nasse Ziele erleiden Überladung.",
                counters = "Blitzresistente Gegner, Saboteure.",
            },
            ["riftgate"] = new TowerDefinition
            {
                id = "riftgate",
                legendary = true,
                active = "rewind",
                name = "Rissportal",
                role = "Legendär · Raumkontrolle",
                icon = "🌌",
                hotkey = "",
                color = 0xc060ff,
                description = "Fusion aus Rissbinder und Gravitationsbrunnen: markiert bis zu vier Gegner gleichzeitig und schleudert sie weit auf dem Weg zurück.",
                damageType = "arcane",
                cost = 0,
                attack = "rift",
                defaultPriority = "exit",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 130, ["range"] = 4.2, ["cooldown"] = 2, ["riftBack"] = 6, ["riftDelay"] = 0.8, ["riftTargets"] = 4, ["hitsAir"] = 1, ["markPct"] = 0.15, ["markDur"] = 3 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Hält Gegner lange im Feuer deiner Türme.",
                counters = "Bosse werden nicht versetzt.",
            },
            ["phoenix"] = new TowerDefinition
            {
                id = "phoenix",
                legendary = true,
                active = "overdrive",
                name = "Phönixhorst",
                role = "Legendär · Feuersturm",
                icon = "🔥",
                hotkey = "",
                color = 0xff8030,
                description = "Fusion aus Drachenhorst und Glutwerfer: ein Phönix kreist über dem Horst und überzieht Boden- und Luftziele mit springendem Feuer.",
                damageType = "fire",
                cost = 0,
                attack = "dragon",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 70, ["range"] = 4.2, ["cooldown"] = 1.1, ["splash"] = 1.6, ["burnDps"] = 20, ["burnDur"] = 4, ["burnSpread"] = 1, ["hitsAir"] = 1, ["airDmgMul"] = 2.5, ["preferAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Frost auf Brennende = Thermoschock. Flieger zuerst, ×2,5 Schaden gegen Flieger.",
                counters = "Feuerresistente Bosse.",
            },
            ["cathedral"] = new TowerDefinition
            {
                id = "cathedral",
                legendary = true,
                active = "nova",
                detector = true,
                name = "Klangkathedrale",
                role = "Legendär · Schall & Unterbrechung",
                icon = "🔔",
                hotkey = "",
                color = 0x60e0c8,
                description = "Fusion aus Resonator und Totemschrein: gewaltige Glockenschläge unterbrechen Fähigkeiten, erschüttern Bosse und machen Gegner verwundbar.",
                damageType = "sonic",
                cost = 0,
                attack = "pulse",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 58, ["range"] = 3.4, ["cooldown"] = 1.4, ["interrupt"] = 60, ["hitsAir"] = 1, ["markPct"] = 0.2, ["markDur"] = 3, ["brittleDur"] = 2 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Markierung verstärkt alle Türme in der Nähe. Deckt Getarnte auf.",
                counters = "Geringer Einzelschaden.",
            },
            ["stormeye"] = new TowerDefinition
            {
                id = "stormeye",
                legendary = true,
                active = "deepfreeze",
                name = "Sturmauge",
                role = "Legendär · Blitz & Frost",
                icon = "🌩️",
                hotkey = "",
                color = 0xa0d8ff,
                description = "Fusion aus Sturmrufer und Frostwächter: eisige Kettenblitze springen zwischen Gegnern und verlangsamen sie stark.",
                damageType = "lightning",
                cost = 0,
                attack = "chain",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 72, ["range"] = 3.8, ["cooldown"] = 1, ["chains"] = 6, ["chainFalloff"] = 0.85, ["chainRange"] = 2.4, ["chillPct"] = 0.25, ["chillDur"] = 2.5, ["chillMax"] = 0.6, ["hitsAir"] = 1 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Kälte + Blitz auf nasse Ziele = Überladung.",
                counters = "Blitzresistente Saboteure.",
            },
            ["minelayer"] = new TowerDefinition
            {
                id = "minelayer",
                active = "overdrive",
                name = "Minenwerfer",
                role = "Physisch · Minenfeld",
                icon = "💣",
                hotkey = "0",
                color = 0xc0884a,
                description = "Wirft im Umkreis Minen mitten auf die Lane. Sie bleiben liegen (auch über das Wellenende hinaus) und explodieren, sobald ein Bodengegner darauf tritt – Flieger lösen sie nicht aus.",
                damageType = "physical",
                cost = 130,
                attack = "mines",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 46, ["range"] = 3.4, ["cooldown"] = 1.6, ["splash"] = 0.9, ["mineMax"] = 6, ["armorPierce"] = 0.1 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "mine1", name = "Schwarzpulver", description = "+18 Schaden", cost = 75, mods = new List<StatMod> { new StatMod { stat = "damage", add = 18 } } },
                    new UpgradeDefinition { id = "mine2", name = "Großes Magazin", description = "+3 Minen gleichzeitig, +0,5 Reichweite", cost = 100, mods = new List<StatMod> { new StatMod { stat = "mineMax", add = 3 }, new StatMod { stat = "range", add = 0.5 } } },
                    new UpgradeDefinition { id = "mine3", name = "Splitterladung", description = "Explosionsradius +0,35, +15 % Panzerbrechung", cost = 140, mods = new List<StatMod> { new StatMod { stat = "splash", add = 0.35 }, new StatMod { stat = "armorPierce", add = 0.15 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "mineA", name = "Streuminen", description = "Wirft 2 Minen auf einmal, +5 Minen gleichzeitig (je Mine −25 % Schaden)", cost = 220, mods = new List<StatMod> { new StatMod { stat = "multishot", add = 1 }, new StatMod { stat = "mineMax", add = 5 }, new StatMod { stat = "damage", mul = 0.75 } } },
                    new UpgradeDefinition { id = "mineB", name = "Schockminen", description = "Betäubt 0,8 s, +40 % Schaden, macht Ziele 3 s spröde", cost = 240, mods = new List<StatMod> { new StatMod { stat = "stunDur", add = 0.8 }, new StatMod { stat = "damage", mul = 1.4 }, new StatMod { stat = "brittleDur", add = 3 } } },
                },
                synergies = "Mit dem Gravitationsbrunnen oder Frostwächter laufen Gegner gebündelt über die Minen. Fusion mit dem Sporenwerfer ergibt Sporenminen.",
                counters = "Flieger lösen keine Minen aus. Schnelle Schwärme verbrauchen Minen mit Einzelzielen.",
            },
            ["skyguard"] = new TowerDefinition
            {
                id = "skyguard",
                active = "overdrive",
                name = "Flakbatterie",
                role = "Flugabwehr · nur Luftziele",
                icon = "🚀",
                hotkey = "9",
                color = 0x6a8ab0,
                description = "Reine Flugabwehr: Zwillingsgeschütze feuern schnelle Netzgranaten auf Flieger – verlangsamt sie und trifft hart. Bodengegner ignoriert sie vollständig.",
                damageType = "physical",
                cost = 110,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 24, ["range"] = 4.2, ["cooldown"] = 0.55, ["projectileSpeed"] = 22, ["multishot"] = 2, ["hitsAir"] = 1, ["hitsGround"] = 0, ["chillPct"] = 0.12, ["chillDur"] = 2, ["chillMax"] = 0.45 },
                upgrades = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "sky1", name = "Zielradar", description = "+0,6 Reichweite", cost = 70, mods = new List<StatMod> { new StatMod { stat = "range", add = 0.6 } } },
                    new UpgradeDefinition { id = "sky2", name = "Splittergranaten", description = "+10 Schaden, kleine Explosion (0,6 Felder)", cost = 95, mods = new List<StatMod> { new StatMod { stat = "damage", add = 10 }, new StatMod { stat = "splash", add = 0.6 } } },
                    new UpgradeDefinition { id = "sky3", name = "Schnelllader", description = "+25 % Angriffstempo", cost = 125, mods = new List<StatMod> { new StatMod { stat = "cooldown", mul = 0.8 } } },
                },
                specs = new List<UpgradeDefinition>
                {
                    new UpgradeDefinition { id = "skyA", name = "Flakgewitter", description = "4 Ziele gleichzeitig, +8 Schaden", cost = 220, mods = new List<StatMod> { new StatMod { stat = "multishot", add = 2 }, new StatMod { stat = "damage", add = 8 } } },
                    new UpgradeDefinition { id = "skyB", name = "Fangnetze", description = "Netze verlangsamen Flieger um bis zu 70 % und halten 3 s", cost = 220, mods = new List<StatMod> { new StatMod { stat = "chillPct", add = 0.13 }, new StatMod { stat = "chillMax", add = 0.25 }, new StatMod { stat = "chillDur", add = 1 } } },
                },
                synergies = "Bremst Aschenschwingen und Sturmgreife für alle anderen Türme. Fusion mit dem Frostwächter ergibt die Himmelswacht.",
                counters = "Trifft keine Bodengegner – nur zusammen mit anderen Türmen bauen.",
            },
            ["skywatch"] = new TowerDefinition
            {
                id = "skywatch",
                legendary = true,
                active = "deepfreeze",
                name = "Himmelswacht",
                role = "Legendär · nur Luftziele",
                icon = "🌨️",
                hotkey = "",
                color = 0x9ad8ff,
                description = "Fusion aus Flakbatterie und Frostwächter: Eisflak holt jeden Flieger vom Himmel – 6 Ziele gleichzeitig, sehr große Reichweite, Eisexplosionen, die auch Flieger daneben bremsen, bis 75 % Verlangsamung, häufiges Einfrieren und 4-facher Schaden gegen Flieger. Bodengegner ignoriert sie.",
                damageType = "frost",
                cost = 0,
                attack = "projectile",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 80, ["range"] = 6.2, ["cooldown"] = 0.32, ["projectileSpeed"] = 32, ["multishot"] = 6, ["splash"] = 1.1, ["hitsAir"] = 1, ["hitsGround"] = 0, ["chillPct"] = 0.4, ["chillDur"] = 4, ["chillMax"] = 0.75, ["freezeChance"] = 0.15, ["freezeDur"] = 1.5, ["airDmgMul"] = 4 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Hält ganze Fliegerschwärme fest. Resonatoren zerschmettern Eingefrorene.",
                counters = "Trifft keine Bodengegner.",
            },
            ["sporemine"] = new TowerDefinition
            {
                id = "sporemine",
                legendary = true,
                active = "overdrive",
                name = "Sporenminenfeld",
                role = "Legendär · Giftminen",
                icon = "🍄",
                hotkey = "",
                color = 0x8ad040,
                description = "Fusion aus Minenwerfer und Sporenwerfer: wirft Sporenminen, die beim Auslösen explodieren, stark vergiften und eine Sporenwolke hinterlassen, die weiter Giftstapel aufträgt.",
                damageType = "poison",
                cost = 0,
                attack = "mines",
                defaultPriority = "first",
                networkable = true,
                @base = new Dictionary<string, double> { ["damage"] = 70, ["range"] = 4.2, ["cooldown"] = 1.1, ["splash"] = 1.3, ["mineMax"] = 12, ["poisonDps"] = 14, ["poisonDur"] = 6, ["poisonMaxStacks"] = 8, ["cloudDur"] = 4, ["cloudRadius"] = 1.3, ["poisonSlow"] = 0.15 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Feuer auf Gift löst Giftbrand aus. Stoppt jede Regeneration.",
                counters = "Flieger und giftresistente Gegner.",
            },
            ["barricade"] = new TowerDefinition
            {
                id = "barricade",
                name = "Barrikade",
                role = "Hindernis (nur Mazing)",
                icon = "🧱",
                hotkey = "b",
                color = 0x9a8a78,
                description = "Günstiges Hindernis zum Bau von Labyrinthen. Greift nicht an. Darf den Weg nie vollständig blockieren.",
                damageType = null,
                cost = 4,
                attack = "none",
                defaultPriority = "first",
                networkable = false,
                mazingOnly = true,
                @base = new Dictionary<string, double> { ["range"] = 0, ["hitsGround"] = 0 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Verlängert die Route durch die Reichweite deiner Türme.",
                counters = "Auch Flieger folgen dem Labyrinth (Luftabwehr nötig). Pfadspäher suchen die am wenigsten bewachte Route.",
            },
            ["lanebarricade"] = new TowerDefinition
            {
                id = "lanebarricade",
                name = "Lane-Barrikade",
                role = "Hindernis auf der Lane · blockiert 5 s",
                icon = "🚧",
                hotkey = "b",
                color = 0xb08a4a,
                description = "Wird direkt auf die Lane gesetzt (nicht direkt vor einen Riss). Hält Bodengegner auf, bis sie insgesamt 5 s lang blockiert wurden – dann zerbricht sie. Bosse werden nur um 50 % verlangsamt. Kostenlos, aber höchstens 2 Barrikaden je 10 Minuten Spielzeit.",
                damageType = null,
                cost = 0,
                attack = "none",
                defaultPriority = "first",
                networkable = false,
                laneOnly = true,
                @base = new Dictionary<string, double> { ["range"] = 0.85, ["hitsGround"] = 0, ["chillPct"] = 0.5, ["chillDur"] = 1.2, ["holdTime"] = 5 },
                upgrades = new List<UpgradeDefinition>(),
                specs = new List<UpgradeDefinition>(),
                synergies = "Bremst Gegner im Feuer deiner Türme – ideal vor Kanonen, Minen und Meteoren.",
                counters = "Flieger fliegen darüber hinweg. Zählt nicht zum Turmlimit.",
            },
        });

        /** Die zwölf Türme (ohne Barrikade). */
        /** 3.2: Reihenfolge der Bauleiste = Freischaltung im Helden-Endlos (W1, W3, W5, W6, W7 …); Tasten 1, 2, 3 … passend. */
        public static readonly List<string> TOWER_ORDER = new List<string>
        {
            "ranger", "ember", "frost", "goldmine", "storm", "cannon", "bank", "spore", "ballista", "skyguard", "minelayer", "rune", "resonator", "quake", "pylon", "totem", "acid", "riftbinder", "tesla", "seal", "gale", "gravity", "meteor", "ward", "eyrie", "laser", "dragon", "barracks",
        };

        /** Legendäre Türme (nur durch Fusion). */
        public static readonly List<string> LEGENDARY_ORDER = new List<string> { "plasma", "glacier", "voidlance", "starfall", "thornwood", "thunderspire", "riftgate", "phoenix", "cathedral", "stormeye", "sporemine", "skywatch", "silencehall", "banecircle", "acidmortar", "plaguepot", "quakemaw", "cyclone", "stormbattery", "falcon", "sunlance", "order", "treasury", "deepdrill", "shieldnet" };

        /** Fusionsrezepte: zwei voll ausgebaute (Stufe 4 + Spezialisierung) benachbarte Türme. */
        public static readonly List<FusionRecipe> FUSIONS = new List<FusionRecipe>
        {
            new FusionRecipe { id = "plasma", a = "ember", b = "storm", cost = 380 },
            new FusionRecipe { id = "glacier", a = "frost", b = "cannon", cost = 380 },
            new FusionRecipe { id = "voidlance", a = "ballista", b = "rune", cost = 420 },
            new FusionRecipe { id = "starfall", a = "meteor", b = "laser", cost = 480 },
            new FusionRecipe { id = "thornwood", a = "ranger", b = "spore", cost = 340 },
            new FusionRecipe { id = "thunderspire", a = "tesla", b = "pylon", cost = 420 },
            new FusionRecipe { id = "riftgate", a = "riftbinder", b = "gravity", cost = 440 },
            new FusionRecipe { id = "phoenix", a = "dragon", b = "ember", cost = 460 },
            new FusionRecipe { id = "cathedral", a = "resonator", b = "totem", cost = 400 },
            new FusionRecipe { id = "stormeye", a = "storm", b = "frost", cost = 380 },
            new FusionRecipe { id = "sporemine", a = "minelayer", b = "spore", cost = 360 },
            new FusionRecipe { id = "skywatch", a = "skyguard", b = "frost", cost = 380 },
        }.Concat(TowersNew.FUSIONS).ToList();

        /** Maximaler Abstand (Zellen, Mittelpunkt) zwischen Fusionspartnern. */
        public const double FUSION_RANGE = 2.3;

        /// <summary>4.1: Nur noch zwei Zielwahlen – „Erster auf dem Weg“ und „Meiste Lebenspunkte“.</summary>
        public static string basePriority(string p) => p == "strong" ? "strong" : "first";

        public static readonly Dictionary<string, TowerActiveDefinition> TOWER_ACTIVES = new Dictionary<string, TowerActiveDefinition>
        {
            ["overdrive"] = new TowerActiveDefinition { id = "overdrive", name = "Überladung", description = "6 s doppeltes Angriffstempo.", cooldown = 45, duration = 6 },
            ["deepfreeze"] = new TowerActiveDefinition { id = "deepfreeze", name = "Tiefkühlung", description = "Friert alle Gegner in Reichweite 2,5 s ein (Bosse verlieren Standfestigkeit).", cooldown = 40, duration = 0 },
            ["overload"] = new TowerActiveDefinition { id = "overload", name = "Blitzsturm", description = "6 s: +4 Kettensprünge und +50 % Schaden.", cooldown = 45, duration = 6 },
            ["goldrush"] = new TowerActiveDefinition { id = "goldrush", name = "Goldrausch", description = "Fördert sofort das Anderthalbfache des Welleneinkommens.", cooldown = 90, duration = 0 },
            ["surge"] = new TowerActiveDefinition { id = "surge", name = "Netzflut", description = "8 s doppelter Netzwerkbonus im ganzen Netz.", cooldown = 50, duration = 8 },
            ["rewind"] = new TowerActiveDefinition { id = "rewind", name = "Zeitriss", description = "Versetzt alle Gegner in Reichweite 3 Felder zurück (keine Bosse).", cooldown = 45, duration = 0 },
            ["meteorstorm"] = new TowerActiveDefinition { id = "meteorstorm", name = "Meteorsturm", description = "5 zusätzliche Meteore auf Gegner in Reichweite.", cooldown = 50, duration = 0 },
            ["nova"] = new TowerActiveDefinition { id = "nova", name = "Arkannova", description = "Entlädt eine Nova: vierfacher Schaden an allen Gegnern in Reichweite.", cooldown = 40, duration = 0 },
        };

        /** Ab dieser Stufe ist die aktive Fähigkeit eines Turms nutzbar. */
        public const double ACTIVE_MIN_LEVEL = 2;

        /** Baubare Objekte inkl. Barrikade (Mazing). */
        public static readonly List<string> BUILD_ORDER = U.Concat(TOWER_ORDER, new List<string> { "barricade", "lanebarricade" });

        public static bool isBuildable(string id)
        {
            return !U.Truthy(TOWERS[id].legendary);
        }

        /** Erstattungsquote beim Verkauf nach einer Welle. */
        public const double SELL_REFUND = 0.7;
        /** Rückversetzungs-Abklingzeit je Gegner (Rissbinder). */
        public const double RIFT_ENEMY_COOLDOWN = 8;

        // ------------------------------------------------------------ Grundfläche

        /** Kantenlänge der Grundfläche (1 = ein Feld, 2 = 2×2). */
        public static double towerSize(string id)
        {
            return TOWERS[id].size ?? 1;
        }

        /** Alle Zellindizes, die ein Turm mit Ankerfeld (cx, cy) belegt. */
        public static List<double> footprintCells(string id, double cx, double cy, double width)
        {
            double n = towerSize(id);
            List<double> @out = new List<double>();
            for (double dy = 0; dy < n; dy++) for (double dx = 0; dx < n; dx++) @out.Add((cy + dy) * width + cx + dx);
            return @out;
        }

        /** Mittelpunkt der Grundfläche in Weltkoordinaten. */
        public static Vec2 towerCenter(TowerInstance t)
        {
            return towerCenter(t.def, t.cx, t.cy);
        }

        /// <summary>PORT: Überladung für TS-Aufrufe mit Ad-hoc-Objekt <c>{ def, cx, cy }</c>.</summary>
        public static Vec2 towerCenter(string def, double cx, double cy)
        {
            double h = towerSize(def) / 2;
            return new Vec2 { x = cx + h, y = cy + h };
        }
    }
}
