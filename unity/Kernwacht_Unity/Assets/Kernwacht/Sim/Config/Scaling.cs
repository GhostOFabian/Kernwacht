// Übersetzung von config/scaling.ts.
// 3.0 Endlos-Skalierung: exponentieller Anstieg ab Welle 60, Weltstufen alle 50 Wellen, Wellen-Affixe ab
// Welle 100, mehr Elite und Mini-Bosse, nachrückende Wellen ab Welle 120. Alles nur im Endlosmodus.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: Ad-hoc-Objekt <c>SCALING</c> (config/scaling.ts).</summary>
    public class ScalingConstants
    {
        /** Ab dieser Welle wächst die Gegner-LP zusätzlich um expRate je Welle (multiplikativ). */
        public double expFrom;
        public double expRate;
        public double bossExpRate;
        /** Weltstufe = floor(Welle / tierEvery). */
        public double tierEvery;
        public double tierHp;
        public double tierArmor;
        public double tierArmorMax;
        public double tierSpeed;
        public double tierSpeedMax;
        public double tierReward;
        /** Affixe ab Welle affixFrom: 1, ab +75 Wellen 2, ab +150 Wellen 3. */
        public double affixFrom;
        public double affixStep;
        public double affixMax;
        /** Ab dieser Welle: Elitegruppe in jeder zweiten Welle und Mini-Bosse alle 3 Wellen (Helden-Endlos). */
        public double pressureFrom;
        public double miniBossEveryLate;
        public double bossLeakMul;
        /** Nachrückende Wellen (Helden-Endlos): ab Welle overrunFrom rückt nach overrunAfter s Kampf die nächste nach. */
        public double overrunFrom;
        public double overrunAfter;
        /** 3.11: Ab Welle smoothFrom werden Gruppen nach LP verteilt (smoothHpPerSlot Grund-LP je Platz der Mengenobergrenze). */
        public double smoothFrom;
        public double smoothHpPerSlot;
        /** 3.11: Ab Welle expSlowFrom wächst die LP nur noch um expRateLate je Welle (statt expRate). */
        public double expSlowFrom;
        public double expRateLate;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>FLYER</c> (config/scaling.ts).</summary>
    public class FlyerConstants
    {
        public double maxSpeed;
        public double speedMulShare;
        public double slowMul;
    }

    /// <summary>TS: <c>interface AffixDef</c> (config/scaling.ts).</summary>
    public class AffixDef
    {
        public string id;   // AffixId = 'regen' | 'shielded' | 'immune' | 'swift' | 'splitter' | 'jammer' | 'armored'
        public string name;
        public string icon;
        public string description;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>CHALLENGE</c> (config/scaling.ts).</summary>
    public class ChallengeConstants
    {
        /** Bis Welle switchAt alle 50 Wellen, danach alle 25 Wellen. */
        public double every;
        public double switchAt;
        public double everyLate;
        public double hpMul;
        public double resist;
        /** Kontrolleffekte (Verlangsamung, Einfrieren, Betäubung, Wurzeln) wirken nur zu diesem Anteil. */
        public double ccMul;
        public double shieldEvery;
        public double shieldPct;
        public double shieldMax;
        public double summonEvery;
        public double summonCount;
        public double enrageAfter;
        public double enrageSpeed;
        /** Durchbruch kostet mindestens so viel bzw. diesen Anteil der maximalen Kernenergie. */
        public double leakMin;
        public double leakPct;
        public double rewardMul;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>TOWER_LIMIT</c> (config/scaling.ts).</summary>
    public class TowerLimitConstants
    {
        /** Endlosmodi: so viele Türme zu Beginn … */
        public double @base;
        /** … und alle perWaves Wellen einer mehr. */
        public double perWaves;
        /** Fokusbonus: je freiem Bauplatz +focusPer Turmschaden, höchstens focusMax. */
        public double focusPer;
        public double focusMax;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>LANE_BARRICADE</c> (config/scaling.ts).</summary>
    public class LaneBarricadeConstants
    {
        /** Höchstens so viele Barrikaden je Zeitfenster (Spielzeit in Sekunden). */
        public double maxCharges;
        public double window;
        /** Mindestabstand (Felder) zu jedem Riss/Spawn. */
        public double minSpawnDist;
    }

    public static partial class Scaling
    {
        public static readonly ScalingConstants SCALING = new ScalingConstants
        {
            /** Ab dieser Welle wächst die Gegner-LP zusätzlich um expRate je Welle (multiplikativ). */
            expFrom = 60,
            expRate = 1.02,
            bossExpRate = 1.018,
            /** Weltstufe = floor(Welle / tierEvery). */
            tierEvery = 50,
            tierHp = 0.15,
            tierArmor = 0.03,
            tierArmorMax = 0.3,
            tierSpeed = 0.03,
            tierSpeedMax = 0.3,
            tierReward = 0.2,
            /** Affixe ab Welle affixFrom: 1, ab +75 Wellen 2, ab +150 Wellen 3. */
            affixFrom = 100,
            affixStep = 75,
            affixMax = 3,
            /** Ab dieser Welle: Elitegruppe in jeder zweiten Welle und Mini-Bosse alle 3 Wellen (Helden-Endlos). */
            pressureFrom = 100,
            miniBossEveryLate = 3,
            bossLeakMul = 2,
            /** Nachrückende Wellen (Helden-Endlos): ab Welle overrunFrom rückt nach overrunAfter s Kampf die nächste nach. */
            overrunFrom = 120,
            overrunAfter = 45,
            /** 3.11: Ab Welle smoothFrom werden Gruppen nach LP verteilt (smoothHpPerSlot Grund-LP je Platz der Mengenobergrenze). */
            smoothFrom = 100,
            smoothHpPerSlot = 90,
            /** 3.11: Ab Welle expSlowFrom wächst die LP nur noch um expRateLate je Welle (statt expRate). */
            expSlowFrom = 150,
            expRateLate = 1.006,
        };

        /** 3.12: Flieger – Tempo-Obergrenze, nur halber Anteil an Tempo-Zuschlägen, Verlangsamung wirkt stärker. */
        public static readonly FlyerConstants FLYER = new FlyerConstants
        {
            maxSpeed = 2.0,
            speedMulShare = 0.5,
            slowMul = 1.3,
        };

        public static readonly Dictionary<string, AffixDef> AFFIXES = new Dictionary<string, AffixDef>
        {
            ["regen"] = new AffixDef { id = "regen", name = "Regeneration", icon = "✚", description = "Gegner heilen 1 % LP/s (Gift/Brand unterbinden das)" },
            ["shielded"] = new AffixDef { id = "shielded", name = "Schildträger", icon = "⛨", description = "Gegner starten mit Schild (25 % LP)" },
            ["immune"] = new AffixDef { id = "immune", name = "Immunität", icon = "⊘", description = "Gegner sind gegen einen Schadenstyp nahezu immun" },
            ["swift"] = new AffixDef { id = "swift", name = "Flink", icon = "»", description = "+20 % Tempo" },
            ["splitter"] = new AffixDef { id = "splitter", name = "Splitter", icon = "✶", description = "Besiegte Gegner zerfallen in 2 Schwarmlinge" },
            ["jammer"] = new AffixDef { id = "jammer", name = "Störsender", icon = "⚡", description = "Elitegegner betäuben alle 8 s Türme in 1,5 Feldern für 1 s" },
            ["armored"] = new AffixDef { id = "armored", name = "Gepanzert", icon = "▣", description = "+20 % physische Resistenz" },
        };

        public static readonly List<string> AFFIX_ORDER = new List<string> { "regen", "shielded", "immune", "swift", "splitter", "jammer", "armored" };
        public static readonly List<string> IMMUNE_TYPES = new List<string> { "physical", "fire", "frost", "lightning", "poison", "arcane" };

        public static double worldTier(double wave, bool endless)
        {
            return endless ? Math.Floor(U.Max(0, wave) / SCALING.tierEvery) : 0;
        }

        public static double affixCount(double wave, bool endless)
        {
            if (!endless || wave < SCALING.affixFrom) return 0;
            return U.Min(SCALING.affixMax, 1 + Math.Floor((wave - SCALING.affixFrom) / SCALING.affixStep));
        }

        // ------------------------------------------------------------ Challenge-Bosse (3.1)

        public static readonly ChallengeConstants CHALLENGE = new ChallengeConstants
        {
            /** Bis Welle switchAt alle 50 Wellen, danach alle 25 Wellen. */
            every = 50,
            switchAt = 250,
            everyLate = 25,
            hpMul = 3.5,
            resist = 0.15,
            /** Kontrolleffekte (Verlangsamung, Einfrieren, Betäubung, Wurzeln) wirken nur zu diesem Anteil. */
            ccMul = 0.35,
            shieldEvery = 15,
            shieldPct = 0.08,
            shieldMax = 0.3,
            summonEvery = 12,
            summonCount = 3,
            enrageAfter = 60,
            enrageSpeed = 1.4,
            /** Durchbruch kostet mindestens so viel bzw. diesen Anteil der maximalen Kernenergie. */
            leakMin = 10,
            leakPct = 0.25,
            rewardMul = 5,
        };

        /** 3.11: Challenge-Bosse werden ab Welle 150 je 100 Wellen um das 1-Fache stärker (Welle 250: ×7, Welle 500: ×15,75). */
        public static double challengeHpMul(double wave)
        {
            return CHALLENGE.hpMul * (1 + U.Max(0, wave - 150) / 100);
        }

        public static bool isChallengeWave(double wave, bool endless)
        {
            if (!endless || wave <= 0) return false;
            if (wave <= CHALLENGE.switchAt) return wave % CHALLENGE.every == 0;
            return wave % CHALLENGE.everyLate == 0;
        }

        // ------------------------------------------------------------ Turmlimit (3.3, „Weniger ist mehr“)

        public static readonly TowerLimitConstants TOWER_LIMIT = new TowerLimitConstants
        {
            /** Endlosmodi: so viele Türme zu Beginn … */
            @base = 20,
            /** … und alle perWaves Wellen einer mehr. */
            perWaves = 5,
            /** Fokusbonus: je freiem Bauplatz +focusPer Turmschaden, höchstens focusMax. */
            focusPer = 0.02,
            focusMax = 0.3,
        };

        /** 3.7: Türme, die nicht zum Turmlimit zählen – Barrikaden, Unterstützungstürme, die andere Türme verstärken, und (3.8) Goldgräber. */
        public static readonly List<string> LIMIT_FREE = new List<string> { "barricade", "lanebarricade", "pylon", "totem", "goldmine" };

        public static bool countsTowardLimit(string def)
        {
            return !LIMIT_FREE.Contains(def);
        }

        /** Höchstzahl an Türmen (Barrikaden und Unterstützungstürme zählen nicht). Außerhalb der Endlosmodi unbegrenzt. */
        public static double towerLimit(double wave, bool endless, bool off = false)
        {
            if (!endless || off) return double.PositiveInfinity;
            return TOWER_LIMIT.@base + Math.Floor(U.Max(0, wave) / TOWER_LIMIT.perWaves);
        }

        /** Welle, ab der der nächste Bauplatz frei wird. */
        public static double nextTowerSlotWave(double wave)
        {
            return (Math.Floor(U.Max(0, wave) / TOWER_LIMIT.perWaves) + 1) * TOWER_LIMIT.perWaves;
        }

        /** Fokusbonus für wenige Türme (Anteil, 0 … focusMax). */
        public static double focusBonus(double count, double limit)
        {
            if (!U.IsFinite(limit)) return 0;
            return U.Min(TOWER_LIMIT.focusMax, U.Max(0, limit - count) * TOWER_LIMIT.focusPer);
        }

        // ------------------------------------------------------------ Lane-Barrikaden (3.8)

        public static readonly LaneBarricadeConstants LANE_BARRICADE = new LaneBarricadeConstants
        {
            /** Höchstens so viele Barrikaden je Zeitfenster (Spielzeit in Sekunden). */
            maxCharges = 2,
            window = 600,
            /** Mindestabstand (Felder) zu jedem Riss/Spawn. */
            minSpawnDist = 4,
        };
    }
}
