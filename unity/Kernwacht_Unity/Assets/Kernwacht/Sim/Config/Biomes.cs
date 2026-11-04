// Übersetzung von config/biomes.ts.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: inline-Typ <c>BiomeDefinition.palette</c>.</summary>
    public class BiomePalette
    {
        public double ground;
        public double ground2;
        public double path;
        public double cliff;
        public double high;
        public double obstacle;
        public double obstacle2;
        public double accent;
        public double sky;
        public double fog;
        public double light;
    }

    /// <summary>TS: inline-Typ <c>BiomeDefinition.grade</c>. tint ist ein Tupel [number, number, number] → double[3].</summary>
    public class BiomeGrade
    {
        public double[] tint;
        public double saturation;
        public double contrast;
    }

    public class BiomeDefinition
    {
        public string id;   // BiomeId
        public string name;
        public string description;
        public BiomePalette palette;
        public double obstacleDensity;
        public double heightAmp;
        public List<double> decorKinds = new List<double>();   // 0 Baum, 1 Fels, 2 Kristall, 3 Lava, 4 Eis, 5 Ruine, 6 Pilz, 7 Wolke, 8 Weide/Schilf, 9 Leuchtpilz
        /** Umgebung rund um die schwebende Insel: Wasser, Lava, Wolkenmeer, Eiswasser, Sand, Sumpf, Abgrund. */
        public string sea;       // 'water' | 'lava' | 'clouds' | 'ice' | 'sand' | 'bog' | 'abyss'
        /** Umgebungspartikel. */
        public string weather;   // 'fireflies' | 'embers' | 'snow' | 'sparkles' | 'wind' | 'mist' | 'spores'
        /** Farbstimmung (Nachbearbeitung): Tönung und Sättigung. */
        public BiomeGrade grade;
        public List<string> events = new List<string>();   // TerrainEventId[]
        public double musicRoot;   // MIDI-Grundton
        public List<double> scale = new List<double>();
    }

    public class TerrainEventDefinition
    {
        public string id;   // TerrainEventId
        public string name;
        public string description;
        public double every;
        public double enemySpeedMul;
        public double goldMul;
        public Dictionary<string, double> damageMul = new Dictionary<string, double>();   // Partial<Record<DamageType, number>>
    }

    public static partial class Biomes
    {
        public static readonly Dictionary<string, BiomeDefinition> BIOMES = new Dictionary<string, BiomeDefinition>
        {
            ["forest"] = new BiomeDefinition
            {
                id = "forest",
                name = "Verwunschener Wald",
                description = "Moosige Lichtungen zwischen uralten Bäumen.",
                palette = new BiomePalette { ground = 0x4f8a3c, ground2 = 0x5c9a45, path = 0xb89a6a, cliff = 0x5a4632, high = 0x6aa850, obstacle = 0x2f6a34, obstacle2 = 0x7a6a58, accent = 0xb6f07a, sky = 0x1d2b22, fog = 0x2a3d2e, light = 0xfff2d8 },
                obstacleDensity = 0.13,
                heightAmp = 1,
                decorKinds = new List<double> { 0, 0, 1, 6 },
                events = new List<string> { "sporeBloom", "blizzard" },
                sea = "water",
                weather = "fireflies",
                grade = new BiomeGrade { tint = new double[] { 1, 1.01, 0.96 }, saturation = 1, contrast = 1.05 },
                musicRoot = 57,
                scale = new List<double> { 0, 2, 4, 7, 9 },
            },
            ["volcano"] = new BiomeDefinition
            {
                id = "volcano",
                name = "Vulkanödland",
                description = "Erstarrte Lava, Ascheregen und glühende Spalten.",
                palette = new BiomePalette { ground = 0x4a3a36, ground2 = 0x564440, path = 0x9a7a60, cliff = 0x2a2020, high = 0x6a4a40, obstacle = 0x2c2224, obstacle2 = 0xff6a20, accent = 0xff8a30, sky = 0x2a1410, fog = 0x3a2018, light = 0xffd0a0 },
                obstacleDensity = 0.11,
                heightAmp = 1.3,
                decorKinds = new List<double> { 1, 3, 1, 2 },
                events = new List<string> { "heatwave", "riftSurge" },
                sea = "lava",
                weather = "embers",
                grade = new BiomeGrade { tint = new double[] { 1.08, 0.98, 0.9 }, saturation = 1.1, contrast = 1.1 },
                musicRoot = 50,
                scale = new List<double> { 0, 1, 5, 7, 8 },
            },
            ["frozen"] = new BiomeDefinition
            {
                id = "frozen",
                name = "Gefrorene Ruinen",
                description = "Verschneite Trümmer einer vergessenen Festung.",
                palette = new BiomePalette { ground = 0xc8d8e4, ground2 = 0xb8cada, path = 0x8a96a8, cliff = 0x6a7a8a, high = 0xe0ecf4, obstacle = 0x9ab0c4, obstacle2 = 0x7a8494, accent = 0x9ae0ff, sky = 0x18222e, fog = 0x2a3a4a, light = 0xe8f4ff },
                obstacleDensity = 0.12,
                heightAmp = 0.9,
                decorKinds = new List<double> { 4, 5, 5, 1 },
                events = new List<string> { "blizzard", "crystalResonance" },
                sea = "ice",
                weather = "snow",
                grade = new BiomeGrade { tint = new double[] { 0.94, 1, 1.08 }, saturation = 0.95, contrast = 1.05 },
                musicRoot = 55,
                scale = new List<double> { 0, 2, 3, 7, 10 },
            },
            ["crystal"] = new BiomeDefinition
            {
                id = "crystal",
                name = "Kristallwüste",
                description = "Glühende Kristalle ragen aus goldenem Sand.",
                palette = new BiomePalette { ground = 0xd8b878, ground2 = 0xcaa868, path = 0xa88858, cliff = 0x8a6a48, high = 0xe8c888, obstacle = 0x9a70d0, obstacle2 = 0x60c0d0, accent = 0xe0a0ff, sky = 0x2a1e30, fog = 0x3a2a3a, light = 0xfff0d0 },
                obstacleDensity = 0.1,
                heightAmp = 1.1,
                decorKinds = new List<double> { 2, 2, 1, 2 },
                events = new List<string> { "crystalResonance", "heatwave" },
                sea = "sand",
                weather = "sparkles",
                grade = new BiomeGrade { tint = new double[] { 1.04, 0.99, 1.04 }, saturation = 1.12, contrast = 1.05 },
                musicRoot = 52,
                scale = new List<double> { 0, 3, 5, 7, 10 },
            },
            ["sky"] = new BiomeDefinition
            {
                id = "sky",
                name = "Zerbrochene Himmelsinseln",
                description = "Schwebende Felsinseln über einem endlosen Wolkenmeer.",
                palette = new BiomePalette { ground = 0x7ab0a0, ground2 = 0x8ac0b0, path = 0xd8d0b8, cliff = 0x5a6a78, high = 0x9ad0c0, obstacle = 0x4a5a70, obstacle2 = 0xf0f4ff, accent = 0xa0f0ff, sky = 0x2a3a5a, fog = 0x4a5a7a, light = 0xffffff },
                obstacleDensity = 0.15,
                heightAmp = 1.2,
                decorKinds = new List<double> { 7, 1, 0, 2 },
                events = new List<string> { "riftSurge", "crystalResonance" },
                sea = "clouds",
                weather = "wind",
                grade = new BiomeGrade { tint = new double[] { 0.98, 1, 1.06 }, saturation = 1.05, contrast = 1 },
                musicRoot = 60,
                scale = new List<double> { 0, 2, 4, 7, 11 },
            },
            ["swamp"] = new BiomeDefinition
            {
                id = "swamp",
                name = "Nebelsumpf",
                description = "Knorrige Weiden, schwarzes Wasser und Irrlichter im Nebel.",
                palette = new BiomePalette { ground = 0x4a5a3a, ground2 = 0x526442, path = 0x7a6a4a, cliff = 0x3a3a2a, high = 0x5a6a46, obstacle = 0x3a4a2a, obstacle2 = 0x5a5040, accent = 0x9ae0a0, sky = 0x141c18, fog = 0x2a3a30, light = 0xe8f0d8 },
                obstacleDensity = 0.13,
                heightAmp = 0.8,
                decorKinds = new List<double> { 8, 8, 1, 6 },
                events = new List<string> { "fog", "sporeBloom" },
                sea = "bog",
                weather = "mist",
                grade = new BiomeGrade { tint = new double[] { 0.96, 1.03, 0.97 }, saturation = 0.95, contrast = 1.08 },
                musicRoot = 53,
                scale = new List<double> { 0, 2, 3, 7, 8 },
            },
            ["cavern"] = new BiomeDefinition
            {
                id = "cavern",
                name = "Leuchtgrotte",
                description = "Eine unterirdische Höhle voller biolumineszenter Riesenpilze.",
                palette = new BiomePalette { ground = 0x3a3450, ground2 = 0x423c5a, path = 0x6a5a6a, cliff = 0x221e30, high = 0x4a4466, obstacle = 0x2a2440, obstacle2 = 0x5a4a7a, accent = 0x60f0d0, sky = 0xc0a14, fog = 0x1a1628, light = 0xd8d0ff },
                obstacleDensity = 0.12,
                heightAmp = 1.1,
                decorKinds = new List<double> { 9, 9, 1, 2 },
                events = new List<string> { "glowtide", "crystalResonance" },
                sea = "abyss",
                weather = "spores",
                grade = new BiomeGrade { tint = new double[] { 0.98, 0.98, 1.08 }, saturation = 1.15, contrast = 1.1 },
                musicRoot = 49,
                scale = new List<double> { 0, 3, 5, 6, 10 },
            },
        };

        public static readonly List<string> BIOME_ORDER = new List<string> { "forest", "volcano", "frozen", "crystal", "sky", "swamp", "cavern" };

        public static readonly Dictionary<string, TerrainEventDefinition> TERRAIN_EVENTS = new Dictionary<string, TerrainEventDefinition>
        {
            ["blizzard"] = new TerrainEventDefinition { id = "blizzard", name = "Schneesturm", every = 4, enemySpeedMul = 0.85, goldMul = 1, damageMul = new Dictionary<string, double> { ["fire"] = 0.85 }, description = "Gegner sind 15 % langsamer, Feuer −15 %." },
            ["heatwave"] = new TerrainEventDefinition { id = "heatwave", name = "Hitzewelle", every = 4, enemySpeedMul = 1, goldMul = 1, damageMul = new Dictionary<string, double> { ["fire"] = 1.25, ["frost"] = 0.85 }, description = "Feuer +25 %, Frost −15 %." },
            ["riftSurge"] = new TerrainEventDefinition { id = "riftSurge", name = "Risswoge", every = 5, enemySpeedMul = 1.15, goldMul = 1.2, damageMul = new Dictionary<string, double>(), description = "Gegner +15 % schneller, aber +20 % Gold." },
            ["sporeBloom"] = new TerrainEventDefinition { id = "sporeBloom", name = "Sporenblüte", every = 4, enemySpeedMul = 1, goldMul = 1, damageMul = new Dictionary<string, double> { ["poison"] = 1.3 }, description = "Gift +30 %." },
            ["crystalResonance"] = new TerrainEventDefinition { id = "crystalResonance", name = "Kristallresonanz", every = 4, enemySpeedMul = 1, goldMul = 1, damageMul = new Dictionary<string, double> { ["arcane"] = 1.25, ["sonic"] = 1.25 }, description = "Arkan und Schall +25 %." },
            ["fog"] = new TerrainEventDefinition { id = "fog", name = "Dichter Nebel", every = 4, enemySpeedMul = 0.9, goldMul = 1.1, damageMul = new Dictionary<string, double>(), description = "Gegner −10 % Tempo, +10 % Gold, aber alle Türme −10 % Reichweite." },
            ["glowtide"] = new TerrainEventDefinition { id = "glowtide", name = "Leuchtflut", every = 4, enemySpeedMul = 1, goldMul = 1, damageMul = new Dictionary<string, double> { ["poison"] = 1.25, ["arcane"] = 1.15 }, description = "Gift +25 %, Arkan +15 %." },
        };

        /** Reichweitenfaktor durch Geländeereignisse. */
        public static readonly Dictionary<string, double> EVENT_RANGE_MUL = new Dictionary<string, double> { ["fog"] = 0.9 };
    }
}
