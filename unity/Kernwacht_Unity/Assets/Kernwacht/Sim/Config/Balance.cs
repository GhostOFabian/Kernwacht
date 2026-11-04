// Übersetzung von config/balance.ts.
// Zentrale Balancewerte. Änderungen hier wirken auf alle Systeme.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: Ad-hoc-Objekt <c>SIM</c> (config/balance.ts).</summary>
    public class SimConstants
    {
        public double tickRate;
        public double dt;
        /** Maximale Simulationsticks pro Renderframe (schützt vor "Spirale des Todes" nach Tabwechsel). */
        public double maxTicksPerFrame;
        public double retargetInterval;
        public List<double> speeds = new List<double>();
    }

    /// <summary>TS: Werttyp von <c>DIFFICULTY</c> (config/balance.ts).</summary>
    public class DifficultyDefinition
    {
        public string name;
        public double hpMul;
        public double goldMul;
        public double lives;
        public double rewardMul;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>ECONOMY</c> (config/balance.ts).</summary>
    public class EconomyConstants
    {
        public Dictionary<string, double> startGold = new Dictionary<string, double>();   // Record<MapVariant, number>
        public double waveBonusBase;
        public double waveBonusPerWave;
        public double bossWaveBonus;
        public double interestRate;
        public double interestCap;
        public double veinMul;
        /** Goldvorkommen: zusätzlicher Abbau des Goldgräbers je Welle (Grundwert + je Welle). */
        public double veinFlat;
        public double veinFlatPerWave;
        public double leyDamageMul;
        public double highgroundRangeAdd;
    }

    /// <summary>TS: Ad-hoc-Objekt <c>QUICK_RUN</c> (config/balance.ts).</summary>
    public class QuickRunConstants
    {
        public double waves;
        public List<double> bosses = new List<double>();
        public List<double> modifierWaves = new List<double>();
    }

    /// <summary>TS: Ad-hoc-Objekt <c>ENDLESS</c> (config/balance.ts).</summary>
    public class EndlessConstants
    {
        public double bossEvery;
        public double modifierEvery;
    }

    public static partial class Balance
    {
        public static readonly SimConstants SIM = new SimConstants
        {
            tickRate = 30,
            dt = 1.0 / 30,
            /** Maximale Simulationsticks pro Renderframe (schützt vor "Spirale des Todes" nach Tabwechsel). */
            maxTicksPerFrame = 12,
            retargetInterval = 0.2,
            speeds = new List<double> { 1, 2, 3, 5, 8 },
        };

        public static readonly Dictionary<string, DifficultyDefinition> DIFFICULTY = new Dictionary<string, DifficultyDefinition>
        {
            ["easy"] = new DifficultyDefinition { name = "Entspannt", hpMul = 0.8, goldMul = 1.15, lives = 30, rewardMul = 1.1 },
            ["normal"] = new DifficultyDefinition { name = "Normal", hpMul = 1.1, goldMul = 1.0, lives = 20, rewardMul = 1.0 },
            ["hard"] = new DifficultyDefinition { name = "Hart", hpMul = 1.25, goldMul = 0.9, lives = 15, rewardMul = 0.95 },
        };

        public static readonly EconomyConstants ECONOMY = new EconomyConstants
        {
            startGold = new Dictionary<string, double> { ["classic"] = 260, ["mazing"] = 430 },
            waveBonusBase = 30,
            waveBonusPerWave = 5,
            bossWaveBonus = 40,
            interestRate = 0.05,
            interestCap = 40,
            veinMul = 1.5,
            /** Goldvorkommen: zusätzlicher Abbau des Goldgräbers je Welle (Grundwert + je Welle). */
            veinFlat = 8,
            veinFlatPerWave = 1.5,
            leyDamageMul = 1.15,
            highgroundRangeAdd = 0.4,
        };

        /** Lebenspunktskalierung pro Welle – polynomiell begrenzt, nie exponentiell. */
        public static double waveHpMul(double wave, bool endless)
        {
            double w = U.Max(0, wave - 1);
            if (!endless) return 1 + 0.12 * w + 0.007 * w * w;
            // Endlos: nach Welle 30 flacht der quadratische Anteil ab (Wurzelanteil), damit der Modus
            // durch Taktik statt durch reine Zahlen schwer wird.
            double quad = U.Min(w, 30);
            double tail = U.Max(0, w - 30);
            double @base = 1 + 0.12 * w + 0.007 * quad * quad + 0.45 * tail + 2 * Math.Sqrt(tail);
            // 3.0: ab Welle 60 zusätzlich sanft exponentiell – es gibt immer eine Wand, die man weiter hinausschiebt.
            // 3.11: ab Welle 150 deutlich flacher (expRateLate), damit Spielerstärke und Gegner im Gleichschritt wachsen.
            double early = U.Max(0, U.Min(wave, Scaling.SCALING.expSlowFrom) - Scaling.SCALING.expFrom);
            double late = U.Max(0, wave - Scaling.SCALING.expSlowFrom);
            return @base * Math.Pow(Scaling.SCALING.expRate, early) * Math.Pow(Scaling.SCALING.expRateLate, late);
        }

        public static double waveBudget(double wave)
        {
            return 8 + 5 * wave + 0.16 * wave * wave;
        }

        public static double waveCountCap(double wave)
        {
            return U.Min(60, 10 + wave * 2);
        }

        /** 3.2: allgemeiner Aufschlag auf alle Boss-LP. */
        public const double BOSS_HP_GLOBAL = 1.5;

        public static double bossHpMul(double wave, bool endless)
        {
            // Bosse skalieren schwächer als normale Gegner; sie fordern Mechanik statt nur LP.
            // 3.2: Boss-LP allgemein ×BOSS_HP_GLOBAL, im Endlos nach Welle 40 steiler (+0,07 statt +0,05 je Welle).
            double w = U.Max(0, wave - 10);
            if (!endless) return BOSS_HP_GLOBAL * (1 + 0.09 * w);
            // 3.11: ab Welle 150 wachsen Bosse genau wie die normalen Wellen (bleiben relevant, ohne davonzulaufen)
            if (wave > Scaling.SCALING.expSlowFrom) return bossHpMul(Scaling.SCALING.expSlowFrom, true) * (waveHpMul(wave, true) / waveHpMul(Scaling.SCALING.expSlowFrom, true));
            return BOSS_HP_GLOBAL * (1 + 0.09 * U.Min(w, 30) + 0.07 * U.Max(0, w - 30)) * Math.Pow(Scaling.SCALING.bossExpRate, U.Max(0, wave - Scaling.SCALING.expFrom));
        }

        public static double killRewardMul(double wave)
        {
            return 1 + 0.035 * U.Max(0, wave - 1);
        }

        public static readonly QuickRunConstants QUICK_RUN = new QuickRunConstants
        {
            waves = 20,
            bosses = new List<double> { 10, 20 },
            modifierWaves = new List<double> { 3, 6, 10, 14, 17 },
        };

        public static readonly EndlessConstants ENDLESS = new EndlessConstants
        {
            bossEvery = 10,
            modifierEvery = 4,
        };

        public const double LIVES_DEFAULT = 20;
    }
}
