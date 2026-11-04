// Übersetzung von config/campaign.ts.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class MissionDefinition
    {
        public string id;
        public double index;
        public string name;
        public string briefing;
        public string goal;
        public string rule;
        public string seed;
        public string biome;     // BiomeId
        public string variant;   // MapVariant
        public double waves;
        public double startGold;
        public double lives;
        /// <summary>TS: Record&lt;number, BossId&gt; → Wellennummer (double) → BossId (wie MissionRules.bosses).</summary>
        public Dictionary<double, string> bosses = new Dictionary<double, string>();
        public List<double> modifierWaves = new List<double>();
        public Dictionary<string, double> enemyResistBonus = new Dictionary<string, double>();   // Partial<Record<DamageType, number>>
        public double veinMul;
        public List<string> bannedTowers = new List<string>();     // TowerId[]
        public List<string> startModifiers = new List<string>();   // ModifierId[]
        public string size;      // MapSize oder null
        /// <summary>Vorgegebene Helden für diese Mission. HeroId[] oder null.</summary>
        public List<string> forcedHeroes;
        public string heroMustSurvive;   // HeroId oder null
        /// <summary>Held, der durch diese Mission freigeschaltet wird. HeroId oder null.</summary>
        public string unlocksHero;
    }

    public static partial class Campaign
    {
        public static readonly List<MissionDefinition> CAMPAIGN = new List<MissionDefinition>
        {
            new MissionDefinition
            {
                id = "m1", index = 0, name = "Der erste Riss", seed = "M1-ERSTER-RISS", biome = "forest", variant = "classic",
                briefing = "Im verwunschenen Wald hat sich ein Riss geöffnet. Halte die ersten Kreaturen auf, bevor sie den Kern erreichen.",
                goal = "Überstehe 8 Wellen.", rule = "Lernmission: großzügiges Startgold. Setze Kaela mit Rissenergie auf dem Weg ein!",
                waves = 8, startGold = 320, lives = 20, bosses = new Dictionary<double, string>(), modifierWaves = new List<double> { 3, 6 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m2", unlocksHero = "hunter", index = 1, name = "Ascheebene", seed = "M2-ASCHEEBENE", biome = "volcano", variant = "classic",
                briefing = "Die Aschenkönigin erhebt sich aus der Glut. Ihre Diener sind an Hitze gewöhnt.",
                goal = "Besiege die Aschenkönigin in Welle 10.", rule = "Alle Gegner +30 % Feuerresistenz.",
                waves = 10, startGold = 280, lives = 20, bosses = new Dictionary<double, string> { [10] = "ashQueen" }, modifierWaves = new List<double> { 3, 7 }, enemyResistBonus = new Dictionary<string, double> { ["fire"] = 0.3 }, veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m3", unlocksHero = "weather", index = 2, name = "Labyrinth der Ruinen", seed = "M3-LABYRINTH", biome = "frozen", variant = "mazing",
                briefing = "Zwischen den gefrorenen Ruinen gibt es keinen festen Weg. Baue ein Labyrinth – aber achte auf die Flieger.",
                goal = "Überstehe 12 Wellen im Mazing-Modus.", rule = "Freies Mazing, ab Welle 5 vermehrt Flieger.",
                waves = 12, startGold = 320, lives = 20, bosses = new Dictionary<double, string>(), modifierWaves = new List<double> { 3, 7, 10 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m4", unlocksHero = "prospector", index = 3, name = "Kristallsturm", seed = "M4-KRISTALLSTURM", biome = "crystal", variant = "classic",
                briefing = "Unter der Kristallwüste liegen reiche Adern. Der Nullhüter wacht über sie.",
                goal = "Besiege den Nullhüter in Welle 12.", rule = "Wenig Startgold, aber Goldadern liefern doppelt.",
                waves = 12, startGold = 200, lives = 20, bosses = new Dictionary<double, string> { [12] = "nullWarden" }, modifierWaves = new List<double> { 3, 7, 10 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 2.0, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m5", unlocksHero = "riftwalker", index = 4, name = "Zerbrochener Himmel", seed = "M5-HIMMELSBRUCH", biome = "sky", variant = "mazing",
                briefing = "Auf den Himmelsinseln ist jeder Fehler tödlich. Der Risskoloss wartet am Ende.",
                goal = "Besiege den Risskoloss in Welle 14.", rule = "Nur 10 Kernenergie. Startmodifikator: Weitblick.",
                waves = 14, startGold = 340, lives = 10, bosses = new Dictionary<double, string> { [14] = "riftColossus" }, modifierWaves = new List<double> { 4, 8, 11 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string> { "farSight" },
            },
            new MissionDefinition
            {
                id = "m6", unlocksHero = "mystic", index = 5, name = "Herz des Risses", seed = "M6-HERZ-DES-RISSES", biome = "volcano", variant = "classic",
                briefing = "Der Ursprung aller Risse. Zwei Wächter stellen sich dir in den Weg.",
                goal = "Besiege den Nullhüter (Welle 8) und den Risskoloss (Welle 15).", rule = "Gegner +15 % Arkanresistenz. Ende des ersten Akts.",
                waves = 15, startGold = 300, lives = 20, bosses = new Dictionary<double, string> { [8] = "nullWarden", [15] = "riftColossus" }, modifierWaves = new List<double> { 3, 6, 10, 13 }, enemyResistBonus = new Dictionary<string, double> { ["arcane"] = 0.15 }, veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m7", index = 6, name = "Schlund im Nebel", seed = "M7-SCHLUND", biome = "swamp", variant = "classic", size = "large",
                briefing = "Im Nebelsumpf verschwinden ganze Wachtürme. Etwas Hungriges lauert in der Tiefe.",
                goal = "Besiege den Schlund der Tiefe in Welle 14.", rule = "Große Karte mit Weggabelung. Schattenschleicher im Nebel – baue Detektoren.",
                waves = 14, startGold = 340, lives = 20, bosses = new Dictionary<double, string> { [14] = "maw" }, modifierWaves = new List<double> { 3, 7, 11 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m8", index = 7, unlocksHero = "silence", name = "Zwillingsmonde", seed = "M8-ZWILLINGE", biome = "cavern", variant = "mazing", size = "large",
                briefing = "In der Leuchtgrotte wachen zwei Geschwister, deren Bund jede Wunde schließt.",
                goal = "Besiege die Zwillingswächter in Welle 15.", rule = "Großes Mazing-Feld. Trenne die Zwillinge, sonst heilen sie sich gegenseitig.",
                waves = 15, startGold = 460, lives = 20, bosses = new Dictionary<double, string> { [8] = "ashQueen", [15] = "twinSol" }, modifierWaves = new List<double> { 3, 6, 10, 13 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
            new MissionDefinition
            {
                id = "m9", index = 8, name = "Das letzte Lied", seed = "M9-LETZTES-LIED", biome = "frozen", variant = "classic", size = "large",
                briefing = "Liora muss das Siegel singen, bis der Riss sich schließt. Schütze die Bardin um jeden Preis.",
                goal = "Überstehe 12 Wellen. Liora darf nicht fallen.", rule = "Vorgegebene Helden: Liora und Kaela. Fällt Liora, ist die Mission verloren.",
                waves = 12, startGold = 360, lives = 20, bosses = new Dictionary<double, string> { [12] = "nullWarden" }, modifierWaves = new List<double> { 3, 6, 9 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
                forcedHeroes = new List<string> { "bard", "blade" }, heroMustSurvive = "bard",
            },
            new MissionDefinition
            {
                id = "m10", index = 9, name = "Weltenbruch", seed = "M10-WELTENBRUCH", biome = "volcano", variant = "classic", size = "huge",
                briefing = "Der Weltenbrecher erhebt sich aus dem Herzen des Risses. Alles, was du gelernt hast, wird nun gebraucht.",
                goal = "Überstehe 20 Wellen und besiege den Weltenbrecher.", rule = "Riesige Karte mit zwei Startpunkten. Finale der Kampagne.",
                waves = 20, startGold = 420, lives = 25, bosses = new Dictionary<double, string> { [7] = "maw", [14] = "twinSol", [20] = "worldbreaker" }, modifierWaves = new List<double> { 3, 6, 10, 13, 17 }, enemyResistBonus = new Dictionary<string, double>(), veinMul = 1.5, bannedTowers = new List<string>(), startModifiers = new List<string>(),
            },
        };

        public const string CAMPAIGN_EPILOGUE = "Der Weltenbrecher ist gefallen, der Kern hält stand. Die Welten atmen auf – du hast die Kampagne abgeschlossen!";
    }
}
