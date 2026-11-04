// Übersetzung von sim/Score.ts.
// Punktewertung einer Partie. Rein abgeleitet aus dem Spielzustand (keine zweite Datenquelle).

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class ScoreBreakdown
    {
        public double waves;
        public double kills;
        public double bosses;
        public double lives;
        public double victory;
        public double multiplier;
        public double total;
    }

    public static partial class Score
    {
        private static readonly Dictionary<string, double> DIFF_MUL = new Dictionary<string, double> { { "easy", 0.8 }, { "normal", 1 }, { "hard", 1.3 } };

        public static ScoreBreakdown scoreBreakdown(GameState s)
        {
            double waves = s.stats.wavesCleared * 100;
            double kills = s.stats.kills * 2;
            double bosses = s.stats.bossesKilled.Count * 500;
            double victory = s.phase == "victory" ? 1500 : 0;
            double lives = s.phase == "victory" ? U.Max(0, s.lives) * 40 : 0;
            double multiplier = DIFF_MUL[s.difficulty] * (s.map.variant == "mazing" ? 1.1 : 1);
            double total = U.Round((waves + kills + bosses + victory + lives) * multiplier);
            return new ScoreBreakdown { waves = waves, kills = kills, bosses = bosses, lives = lives, victory = victory, multiplier = multiplier, total = total };
        }

        public static double computeScore(GameState s)
        {
            return scoreBreakdown(s).total;
        }

        /// <summary>Kampagnensterne: 3 ab 80 % Kernenergie, 2 ab 40 %, sonst 1 (nur bei Sieg).</summary>
        public static double campaignStars(GameState s)
        {
            if (s.phase != "victory") return 0;
            double f = s.lives / U.Max(1, s.maxLives);
            return f >= 0.8 ? 3 : f >= 0.4 ? 2 : 1;
        }

        public static string difficultyName(GameState s)
        {
            return Balance.DIFFICULTY[s.difficulty].name;
        }
    }
}
