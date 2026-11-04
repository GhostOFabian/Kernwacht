// Übersetzung von config/achievements.ts.
// Erfolge und kosmetische Turm-Skins. Erfolge geben keine spielerischen Vorteile –
// sie schalten ausschließlich Optik frei (keine verpflichtende Werteeskalation).

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class AchievementDefinition
    {
        public string id;
        public string name;
        public string description;
        public string icon;
        /// <summary>Prüft am Ende einer Partie (Zustand + bereits aktualisierter Fortschritt).</summary>
        public Func<GameState, PlayerProgress, bool> check;
    }

    public class SkinDefinition
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Tönung der Turmfarben und Sockelfarbe.</summary>
        public double tint;
        public double tintAmount;
        public double stone;
        public string requires;   // Erfolgs-ID oder null
    }

    public static partial class Achievements
    {
        private static bool won(GameState s) { return s.phase == "victory"; }
        /// <summary>3.9: Nur noch Helden-Endlos – Erfolge über erreichte Wellen (IDs bleiben für bestehende Spielstände gleich).</summary>
        private static bool heWave(GameState s, double n) { return s.mode == "heroEndless" && s.stats.wavesCleared >= n; }

        private static double sumValues(Dictionary<string, double> d)
        {
            double a = 0;
            foreach (double b in d.Values) a = a + b;
            return a;
        }

        public static readonly List<AchievementDefinition> ACHIEVEMENTS = new List<AchievementDefinition>
        {
            new AchievementDefinition { id = "firstWin", name = "Erste Wacht", description = "Erreiche Welle 25 im Helden-Endlos.", icon = "🏆", check = (s, p) => won(s) || heWave(s, 25) },
            new AchievementDefinition { id = "flawless", name = "Makellos", description = "Erreiche Welle 20, ohne Kernenergie zu verlieren.", icon = "💎", check = (s, p) => (won(s) || heWave(s, 20)) && s.stats.livesLost == 0 },
            new AchievementDefinition { id = "hardWin", name = "Eiserner Wille", description = "Erreiche Welle 100 im Helden-Endlos.", icon = "⚔", check = (s, p) => (won(s) && s.difficulty == "hard") || heWave(s, 100) },
            new AchievementDefinition { id = "mazer", name = "Labyrinthmeister", description = "Erreiche Welle 30 auf einer Mazing-Karte.", icon = "🧱", check = (s, p) => (won(s) || heWave(s, 30)) && s.map.variant == "mazing" },
            new AchievementDefinition { id = "hugeWin", name = "Weite Welten", description = "Erreiche Welle 50 auf einer riesigen Karte.", icon = "🗺", check = (s, p) => (won(s) || heWave(s, 50)) && s.map.size == "huge" },
            new AchievementDefinition { id = "bossSlayer", name = "Bossjäger", description = "Besiege insgesamt 10 Bosse.", icon = "👑", check = (_s, p) => p.totalBossKills >= 10 },
            new AchievementDefinition { id = "allBosses", name = "Bestiarium", description = "Besiege jeden der sieben Bossarten mindestens einmal (über alle Partien).", icon = "📖", check = (_s, p) => p.bossesDefeated.Count >= 7 },
            new AchievementDefinition { id = "reactions", name = "Alchemist", description = "Löse in einer Partie 100 Elementarreaktionen aus.", icon = "⚗", check = (s, p) => sumValues(s.stats.reactions) >= 100 },
            new AchievementDefinition { id = "network", name = "Netzarchitekt", description = "Baue ein Netzwerk aus 8 Türmen.", icon = "📡", check = (s, p) => s.stats.maxNetworkSize >= 8 },
            new AchievementDefinition { id = "fusion", name = "Schmelztiegel", description = "Erschaffe einen legendären Turm.", icon = "✦", check = (s, p) => s.stats.fusions >= 1 },
            new AchievementDefinition { id = "heroic", name = "Heldenhaft", description = "Helden erzielen in einer Partie 100 Abschüsse.", icon = "🛡", check = (s, p) => s.stats.heroKills >= 100 },
            new AchievementDefinition { id = "endless30", name = "Unermüdlich", description = "Erreiche Welle 50 im Helden-Endlos.", icon = "∞", check = (s, p) => (s.mode == "endless" && s.stats.wavesCleared >= 30) || heWave(s, 50) },
            new AchievementDefinition { id = "campaign", name = "Hüter des Kerns", description = "Erreiche Welle 200 im Helden-Endlos.", icon = "📜", check = (s, p) => U.Includes(p.campaignCompleted, "m10") || heWave(s, 200) },
            new AchievementDefinition { id = "kills5000", name = "Rissbrecher", description = "Besiege insgesamt 5000 Gegner.", icon = "☠", check = (_s, p) => p.totalKills >= 5000 },
            new AchievementDefinition { id = "rich", name = "Schatzkammer", description = "Verdiene in einer Partie 5000 Gold.", icon = "💰", check = (s, p) => s.stats.goldEarned >= 5000 },
            new AchievementDefinition { id = "mutants", name = "Mutantenjäger", description = "Besiege 25 mutierte Elitegegner in einer Partie.", icon = "🧬", check = (s, p) => s.stats.mutationsKilled >= 25 },
        };

        public static readonly List<SkinDefinition> SKINS = new List<SkinDefinition>
        {
            new SkinDefinition { id = "standard", name = "Standard", description = "Die klassische Optik.", tint = 0xffffff, tintAmount = 0, stone = 0x8c8678, requires = null },
            new SkinDefinition { id = "obsidian", name = "Obsidian & Gold", description = "Schwarzer Stein mit goldenen Akzenten.", tint = 0xffd060, tintAmount = 0.35, stone = 0x2a2630, requires = "firstWin" },
            new SkinDefinition { id = "frost", name = "Ewiges Eis", description = "Kühles Blau und Eisweiß.", tint = 0x9ae0ff, tintAmount = 0.45, stone = 0xc8dce8, requires = "mazer" },
            new SkinDefinition { id = "void", name = "Leere", description = "Violetter Riss-Stein.", tint = 0xc070ff, tintAmount = 0.45, stone = 0x3a2a50, requires = "fusion" },
            new SkinDefinition { id = "royal", name = "Königlich", description = "Weißer Marmor und Purpur.", tint = 0xff70b0, tintAmount = 0.3, stone = 0xe8e4e0, requires = "hardWin" },
            new SkinDefinition { id = "molten", name = "Glutherz", description = "Glühender Basalt.", tint = 0xff6a20, tintAmount = 0.4, stone = 0x3a2420, requires = "bossSlayer" },
        };

        public static bool skinUnlocked(SkinDefinition skin, IReadOnlyList<string> achievements)
        {
            return skin.requires == null || U.Includes(achievements, skin.requires);
        }
    }
}
