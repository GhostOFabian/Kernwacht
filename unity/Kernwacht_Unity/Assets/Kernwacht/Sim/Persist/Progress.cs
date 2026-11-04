// Übersetzung von app/progress.ts.
// Überträgt das Ergebnis einer beendeten Partie in den lokalen Fortschritt (rein, testbar).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kernwacht.Sim
{
    public class RunRecord
    {
        public double score;
        public double stars;
        public bool newBestScore;
        public bool newBestEndless;
        public string unlockedMission;   // string | null
        public bool campaignFinished;
        public List<string> newAchievements = new List<string>();
        public List<string> unlockedHeroes = new List<string>();   // HeroId[]
        public HeroRunCommit hero;   // null möglich
    }

    public class HeroRunCommit
    {
        /// <summary>false, wenn dieser Lauf bereits verbucht war (idempotent).</summary>
        public bool committed;
        public double wave;
        public bool newBestWave;
        public List<string> newItems = new List<string>();   // ItemId[]
        public List<string> newPerks = new List<string>();
        public List<double> milestones = new List<double>();
    }

    public static partial class Progress
    {
        /// <summary>Gesamte EP bis zu einer Stufe (Vergleichsmaß für Fortschritt).</summary>
        private static double totalXp(double level, double xp)
        {
            double n = xp;
            for (double l = 1; l < level; l++) n += HeroEndless.xpToNext(l);
            return n;
        }

        /// <summary>
        /// 2.7: Heldenstufe, EP und Skillbaum des Helden sowie das gemeinsame Paragon dauerhaft sichern.
        /// Absolute Werte (wiederholbar ohne Doppelgutschrift); ein älterer Stand überschreibt nie einen neueren.
        /// </summary>
        public static void commitHeroProgress(SaveData save, GameState s)
        {
            HeroEndlessState he = s.he;
            if (he == null || s.mode != "heroEndless") return;
            HeroMetaProgress m = save.progress.heroMeta;
            ChampionState c = he.champion;
            // Gemeinsame Stufe (alle Helden): ein älterer Stand überschreibt nie einen neueren
            if (totalXp(c.level, c.xp) >= totalXp(m.heroLevel.level, m.heroLevel.xp)) m.heroLevel = new HeroLevelProgress { level = c.level, xp = Math.Floor(c.xp) };
            // Skillbaum je Held
            m.champions[c.id] = new ChampionProgress { level = m.heroLevel.level, xp = m.heroLevel.xp, tree = new Dictionary<string, double>(c.tree) };
            // Turm-Meisterschaft: absolute Abschusszahlen, nur nach oben
            foreach (KeyValuePair<string, double> kv in U.Entries(he.mastery ?? new Dictionary<string, double>()))
            {
                if (kv.Value > (U.GetN(m.towerMastery, kv.Key) ?? 0)) m.towerMastery[kv.Key] = kv.Value;
            }
            HeroEndlessParagon p = he.paragon;
            if (p != null && Paragon.paragonScore(p) >= Paragon.paragonScore(m.paragon))
            {
                m.paragon = new ParagonProgress { level = p.level, xp = Math.Floor(p.xp), points = new Dictionary<string, double>(p.points) };
            }
            else if (p != null && p.level == m.paragon.level)
            {
                m.paragon.points = new Dictionary<string, double>(p.points);
            }
        }

        /// <summary>TS <c>delete d[key]</c> mit JS-Schlüsselreihenfolge (ein erneutes Einfügen landet am Ende).</summary>
        private static Dictionary<string, double> deleteKey(Dictionary<string, double> d, string key)
        {
            if (!d.ContainsKey(key)) return d;
            Dictionary<string, double> @out = new Dictionary<string, double>();
            foreach (KeyValuePair<string, double> kv in d) if (kv.Key != key) @out[kv.Key] = kv.Value;
            return @out;
        }

        /// <summary>
        /// Überträgt einen Helden-Endloslauf in den dauerhaften Fortschritt. Idempotent über die runId:
        /// mehrfacher Aufruf (z. B. Fortsetzen + Ende) verbucht Items und Fragmente nur einmal.
        /// </summary>
        public static HeroRunCommit commitHeroRun(SaveData save, GameState s)
        {
            HeroMetaProgress m = save.progress.heroMeta;
            commitHeroProgress(save, s);
            HeroEndlessState he = s.he;
            double wave = s.stats.wavesCleared;
            HeroRunCommit empty = new HeroRunCommit { committed = false, wave = wave, newBestWave = false, newItems = new List<string>(), newPerks = new List<string>(), milestones = new List<double>() };
            if (he == null || s.mode != "heroEndless") return empty;
            if (m.committedRuns.Contains(he.runId)) return empty;
            m.committedRuns.Add(he.runId);
            m.committedRuns = U.Slice(m.committedRuns, -50);
            m.runs++;
            m.lastChampion = he.champion.id;
            // Vergleichsbasis ist der Stand bei Partiebeginn (die Bestwelle wird während des Laufs bereits mitgeschrieben).
            List<string> perksBefore = HeroEndless.activePerks(U.Min(m.bestWave, he.bestWaveAtStart));
            List<string> newItems = new List<string>();
            Action<string> unlock = (id) =>
            {
                if (id == null || !Items.ITEMS.ContainsKey(id) || Items.ITEMS[id] == null || m.unlockedItems.Contains(id)) return;
                m.unlockedItems.Add(id);
                newItems.Add(id);
                m.fragments = deleteKey(m.fragments, id);
            };
            // Fragmente: Veränderung gegenüber dem Start verbuchen (nie negativ)
            foreach (KeyValuePair<string, double> kv in U.Entries(he.fragmentDelta))
            {
                string id = kv.Key;
                double d = kv.Value;
                if (!Items.ITEMS.ContainsKey(id) || Items.ITEMS[id] == null || !U.Truthy(d)) continue;
                double v = U.Max(0, (U.GetN(m.fragments, id) ?? 0) + d);
                if (v > 0) m.fragments[id] = v;
                else m.fragments = deleteKey(m.fragments, id);
            }
            foreach (string id in he.runUnlocks.ToList()) unlock(id);
            bool newBestWave = wave > he.bestWaveAtStart && wave >= m.bestWave;
            m.bestWave = U.Max(m.bestWave, wave);
            List<double> milestones = new List<double>();
            foreach (var ms in HeroEndless.MILESTONES)
            {
                if (m.bestWave < ms.wave || m.claimedMilestones.Contains(ms.wave)) continue;
                m.claimedMilestones.Add(ms.wave);
                milestones.Add(ms.wave);
                foreach (string id in ms.reward.items ?? new List<string>()) unlock(id);
                foreach (KeyValuePair<string, double> kv in U.Entries(ms.reward.fragments ?? new Dictionary<string, double>()))
                {
                    string id = kv.Key;
                    double n = kv.Value;
                    if (!Items.ITEMS.ContainsKey(id) || Items.ITEMS[id] == null || !U.Truthy(n) || m.unlockedItems.Contains(id)) continue;
                    m.fragments[id] = (U.GetN(m.fragments, id) ?? 0) + n;
                }
            }
            // Startausrüstung bereinigen (nur freigeschaltete Items)
            m.startKit = m.startKit.Where((id) => m.unlockedItems.Contains(id)).ToList();
            List<string> newPerks = HeroEndless.activePerks(m.bestWave).Where((id) => !perksBefore.Contains(id)).ToList();
            return new HeroRunCommit { committed = true, wave = wave, newBestWave = newBestWave, newItems = newItems, newPerks = newPerks, milestones = milestones };
        }

        public static string perkName(string id)
        {
            return HeroEndless.PERKS.Find((p) => p.id == id)?.name ?? id;
        }

        /// <summary>Lokales Datum JJJJ-MM-TT. TS-Default <c>d = new Date()</c> → null = jetzt (lokale Zeit).</summary>
        public static string todayKey(DateTime? d = null)
        {
            DateTime dt = d ?? DateTime.Now;
            if (dt.Kind == DateTimeKind.Utc) dt = dt.ToLocalTime();
            return $"{U.Fmt(dt.Year)}-{U.PadStart(U.Fmt(dt.Month), 2, "0")}-{U.PadStart(U.Fmt(dt.Day), 2, "0")}";
        }

        public static RunRecord recordRun(SaveData save, GameState s, DateTime? now = null)
        {
            DateTime nowDt = now ?? DateTime.Now;
            PlayerProgress p = save.progress;
            List<string> heroesBefore = Heroes.HERO_ORDER.Where((id) => Heroes.isHeroUnlocked(id, p.campaignCompleted)).ToList();
            double score = Score.computeScore(s);
            s.score = score;
            bool victory = s.phase == "victory";
            p.totalRuns++;
            if (victory) p.totalVictories++;
            p.totalKills += s.stats.kills;
            p.totalBossKills += s.stats.bossesKilled.Count;
            foreach (string b in s.stats.bossesKilled) if (!p.bossesDefeated.Contains(b)) p.bossesDefeated.Add(b);
            double prevBest = 0;
            foreach (BestScoreEntry b in p.bestScores.Where((b) => b.mode == s.mode)) prevBest = U.Max(prevBest, b.score);
            // TS: now.toISOString().slice(0, 10) – UTC-Datum
            string isoDate = nowDt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            p.bestScores.Add(new BestScoreEntry { mode = s.mode, score = score, seed = s.seed.text, date = isoDate, waves = s.stats.wavesCleared, victory = victory });
            U.StableSort(p.bestScores, (a, b) => b.score - a.score);
            p.bestScores = U.Slice(p.bestScores, 0, 50);
            bool newBestEndless = false;
            if (s.mode == "endless")
            {
                string key = s.map.variant;
                if (s.stats.wavesCleared > (U.GetN(p.bestEndlessWave, key) ?? 0))
                {
                    p.bestEndlessWave[key] = s.stats.wavesCleared;
                    newBestEndless = true;
                }
            }
            if (s.mode == "daily")
            {
                string k = todayKey(nowDt);
                p.dailyCompleted[k] = U.Max(U.GetN(p.dailyCompleted, k) ?? 0, score);
            }
            double stars = 0;
            string unlockedMission = null;
            bool campaignFinished = false;
            if (s.mode == "campaign" && !string.IsNullOrEmpty(s.missionId) && victory)
            {
                stars = Score.campaignStars(s);
                var m = Campaign.CAMPAIGN.Find((x) => x.id == s.missionId);
                if (m != null)
                {
                    if (!p.campaignCompleted.Contains(m.id)) p.campaignCompleted.Add(m.id);
                    p.campaignStars[m.id] = U.Max(U.GetN(p.campaignStars, m.id) ?? 0, stars);
                    if (m.index + 2 > p.campaignUnlocked && m.index + 1 < Campaign.CAMPAIGN.Count)
                    {
                        p.campaignUnlocked = m.index + 2;
                        unlockedMission = Campaign.CAMPAIGN[(int)(m.index + 1)].id;
                    }
                    campaignFinished = m.index == Campaign.CAMPAIGN.Count - 1;
                }
            }
            List<string> newAchievements = new List<string>();
            foreach (var a in Achievements.ACHIEVEMENTS)
            {
                if (p.achievements.Contains(a.id)) continue;
                try
                {
                    if (a.check(s, p))
                    {
                        p.achievements.Add(a.id);
                        newAchievements.Add(a.id);
                    }
                }
                catch
                {
                    // Ein fehlerhafter Erfolgscheck darf das Speichern nicht verhindern.
                }
            }
            HeroRunCommit hero = s.mode == "heroEndless" ? commitHeroRun(save, s) : null;
            List<string> unlockedHeroes = Heroes.HERO_ORDER.Where((id) => Heroes.isHeroUnlocked(id, p.campaignCompleted) && !heroesBefore.Contains(id)).ToList();
            return new RunRecord { score = score, stars = stars, newBestScore = score > prevBest, newBestEndless = newBestEndless, unlockedMission = unlockedMission, campaignFinished = campaignFinished, newAchievements = newAchievements, unlockedHeroes = unlockedHeroes, hero = hero };
        }
    }
}
