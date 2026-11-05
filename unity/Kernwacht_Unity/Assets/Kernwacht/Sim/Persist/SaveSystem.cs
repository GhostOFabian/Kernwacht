// Übersetzung von save/SaveSystem.ts.
// Versionierte, validierte lokale Speicherung.
// Beschädigte Daten werden NICHT stillschweigend gelöscht: der Rohinhalt wird gesichert und
// der Spieler erhält eine verständliche Meldung.
//
// PORT: Rohdaten (TS `unknown`) liegen hier in der neutralen Baumform von U.ParseJson vor:
// Objekt → Dictionary<string, object>, Array → List<object>, Zahl → double, string, bool, null.
// PORT: Object.entries ordnet in JS ganzzahlige Schlüssel aufsteigend vor die übrigen; die hier
// gelesenen Schlüssel (Missions-, Item-, Helden-IDs, Daten) sind nie ganzzahlig, daher Einfügereihenfolge.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>Ersatz für den TS-<c>KeyValueStore</c> (localStorage). Load gibt null zurück, wenn der Schlüssel fehlt.</summary>
    public interface ISaveStorage
    {
        string Load(string key);
        void Save(string key, string value);
        void Remove(string key);
    }

    public class LoadResult
    {
        public SaveData data;
        public string error;
        public string backupKey;
    }

    /// <summary>TS: Rückgabe von deserializeRun: { state: GameState | null; error: string | null }.</summary>
    public class DeserializeRunResult
    {
        public GameState state;
        public string error;
    }

    public class SaveSystem
    {
        public const string SAVE_KEY = "kernwacht.save";
        /// <summary>Speicherschlüssel des früheren Arbeitstitels – wird beim ersten Laden übernommen, nie gelöscht.</summary>
        public static readonly List<string> LEGACY_SAVE_KEYS = new List<string> { "riftbound.save" };
        public const double SAVE_VERSION = 3;

        public static PlayerSettings defaultSettings()
        {
            return new PlayerSettings
            {
                masterVolume = 0.8, musicVolume = 0.45, sfxVolume = 0.8, muted = false, reducedMotion = false, uiScale = 1,
                quality = "auto", showGrid = false, showRanges = false, tutorialSeen = false,
                lastVariant = "classic", lastDifficulty = "normal", lastBiome = "random",
                lastSize = "normal", lastHeroes = new List<string> { "blade", "bard" }, skin = "standard", lockRotation = false, keys = new Dictionary<string, string>(),
            };
        }

        public static PlayerProgress defaultProgress()
        {
            return new PlayerProgress
            {
                campaignUnlocked = 1, campaignCompleted = new List<string>(), campaignStars = new Dictionary<string, double>(), bestEndlessWave = new Dictionary<string, double>(), bestScores = new List<BestScoreEntry>(),
                dailyCompleted = new Dictionary<string, double>(), achievements = new List<string>(), totalRuns = 0, totalVictories = 0, totalKills = 0, totalBossKills = 0, bossesDefeated = new List<string>(),
                heroMeta = defaultHeroMeta(),
            };
        }

        public static HeroMetaProgress defaultHeroMeta()
        {
            return new HeroMetaProgress
            {
                bestWave = 0, unlockedItems = new List<string>(), fragments = new Dictionary<string, double>(), claimedMilestones = new List<double>(), committedRuns = new List<string>(), startKit = new List<string>(),
                lastChampion = "lyra", runs = 0, passive = new Dictionary<string, double>(), champions = new Dictionary<string, ChampionProgress>(), heroLevel = new HeroLevelProgress { level = 1, xp = 0 },
                towerMastery = new Dictionary<string, double>(), paragon = Paragon.emptyParagon(),
            };
        }

        private static HeroMetaProgress sanitizeHeroMeta(object rawIn)
        {
            HeroMetaProgress d = defaultHeroMeta();
            if (!isObj(rawIn)) return d;
            Dictionary<string, object> raw = (Dictionary<string, object>)rawIn;
            double bestWave = U.Round(num(getv(raw, "bestWave"), 0, 0, 100000));
            HeroLevelProgress hl = sanitizeHeroLevel(getv(raw, "heroLevel"), sanitizeChampionProgress(getv(raw, "champions")));
            List<string> items = getv(raw, "unlockedItems") is List<object> ui
                ? uniq(ui.Where((x) => x is string xs && Items.ITEMS.ContainsKey(xs)).Select((x) => (string)x))
                : new List<string>();
            Dictionary<string, double> fragments = new Dictionary<string, double>();
            if (isObj(getv(raw, "fragments")))
            {
                foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)getv(raw, "fragments"))
                {
                    if (!(Items.ITEMS.ContainsKey(kv.Key) && kv.Value is double && !items.Contains(kv.Key))) continue;
                    double v = U.Round(num(kv.Value, 0, 0, 9999));
                    if (v > 0) fragments[kv.Key] = v;
                }
            }
            List<double> claimed = new List<double>();
            if (getv(raw, "claimedMilestones") is List<object> cm)
            {
                HashSet<double> seen = new HashSet<double>();
                foreach (object x in cm) if (x is double xd && U.IsFinite(xd) && seen.Add(xd)) claimed.Add(xd);
            }
            object lc = getv(raw, "lastChampion");
            object ld = getv(raw, "lastDoctrine");
            return new HeroMetaProgress
            {
                bestWave = bestWave,
                unlockedItems = items,
                fragments = fragments,
                claimedMilestones = claimed,
                committedRuns = getv(raw, "committedRuns") is List<object> cr ? U.Slice(cr.Where((x) => x is string).Select((x) => (string)x).ToList(), -50) : new List<string>(),
                startKit = getv(raw, "startKit") is List<object> sk ? U.Slice(uniq(sk.Where((x) => x is string xs && items.Contains(xs)).Select((x) => (string)x)), 0, 6) : new List<string>(),
                lastChampion = lc is string lcs && Champions.CHAMPIONS.ContainsKey(lcs) ? lcs : "lyra",
                lastDoctrine = ld is string lds && Doctrines.DOCTRINES.ContainsKey(lds) ? lds : Doctrines.DEFAULT,
                runs = U.Round(num(getv(raw, "runs"), 0, 0, 1e9)),
                passive = PassiveTree.sanitizePassive(getv(raw, "passive"), bestWave),
                champions = sanitizeChampionProgress(getv(raw, "champions")),
                heroLevel = hl,
                towerMastery = Mastery.sanitizeMastery(getv(raw, "towerMastery")),
                paragon = Paragon.sanitizeParagon(getv(raw, "paragon"), Paragon.paragonPrePoints(hl.level) + HeroEndless.milestoneParagon(bestWave)),
            };
        }

        /// <summary>Dauerhafte Heldenstufen: Stufe 1–99, EP im Rahmen, nur gültige Skillknoten mit erlaubtem Rang.</summary>
        public static Dictionary<string, ChampionProgress> sanitizeChampionProgress(object raw)
        {
            Dictionary<string, ChampionProgress> @out = new Dictionary<string, ChampionProgress>();
            if (!isObj(raw)) return @out;
            foreach (KeyValuePair<string, object> e in (Dictionary<string, object>)raw)
            {
                string id = e.Key;
                if (!Champions.CHAMPIONS.ContainsKey(id) || !isObj(e.Value)) continue;
                Dictionary<string, object> v = (Dictionary<string, object>)e.Value;
                double level = U.Round(num(getv(v, "level"), 1, 1, Paragon.PARAGON.heroMaxLevel));
                double xp = level >= Paragon.PARAGON.heroMaxLevel ? 0 : U.Max(0, U.Min(HeroEndless.xpToNext(level), num(getv(v, "xp"), 0, 0, 1e12)));
                Dictionary<string, double> tree = new Dictionary<string, double>();
                if (isObj(getv(v, "tree")))
                {
                    Dictionary<string, object> vt = (Dictionary<string, object>)getv(v, "tree");
                    foreach (var node in Champions.CHAMPIONS[id].tree)
                    {
                        double r = U.Round(num(getv(vt, node.id), 0, 0, node.maxRank));
                        if (r > 0) tree[node.id] = r;
                    }
                }
                @out[id] = new ChampionProgress { level = level, xp = xp, tree = tree };
            }
            return @out;
        }

        public static SaveData defaultSave()
        {
            return new SaveData { version = SAVE_VERSION, settings = defaultSettings(), progress = defaultProgress(), customMaps = new List<CustomMapEntry>(), resume = null };
        }

        private static double num(object v, double d, double min, double max)
        {
            return v is double x && U.IsFinite(x) ? U.Min(max, U.Max(min, x)) : d;
        }

        private static bool @bool(object v, bool d) { return v is bool b ? b : d; }

        private static string oneOf(object v, string[] opts, string d) { return v is string s && opts.Contains(s) ? s : d; }

        private static bool isObj(object v) { return v is Dictionary<string, object>; }

        /// <summary>Feldzugriff auf ein Rohobjekt; fehlender Schlüssel → null (TS undefined).</summary>
        private static object getv(Dictionary<string, object> o, string key)
        {
            object v;
            return o != null && o.TryGetValue(key, out v) ? v : null;
        }

        /// <summary>TS <c>[...new Set(xs)]</c>: Duplikate entfernen, erste Vorkommen in Reihenfolge.</summary>
        private static List<string> uniq(IEnumerable<string> xs)
        {
            List<string> @out = new List<string>();
            HashSet<string> seen = new HashSet<string>();
            foreach (string x in xs) if (seen.Add(x)) @out.Add(x);
            return @out;
        }

        /// <summary>JS <c>Number(v)</c> für Rohwerte.</summary>
        private static double jsNumber(object v)
        {
            if (v == null) return 0;   // PORT: null → 0; undefined (fehlender Schlüssel) wäre NaN – hier überall gleichwertig
            if (v is double d) return d;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s) return U.ParseNumber(s);
            if (v is List<object> l)
            {
                if (l.Count == 0) return 0;
                if (l.Count == 1) return jsNumber(jsString(l[0]));
                return double.NaN;
            }
            return double.NaN;
        }

        /// <summary>JS <c>String(v)</c> für Rohwerte (Eigenschaftsschlüssel für <c>in</c>).</summary>
        private static string jsString(object v)
        {
            if (v == null) return "null";
            if (v is string s) return s;
            if (v is double d) return U.Fmt(d);
            if (v is bool b) return b ? "true" : "false";
            if (v is List<object> l) return string.Join(",", l.Select((x) => x == null ? "" : jsString(x)));
            return "[object Object]";
        }

        /// <summary>JS <c>key in obj</c> für eine Konfigurationstabelle.</summary>
        private static bool keyIn<V>(object key, Dictionary<string, V> table)
        {
            return table.ContainsKey(jsString(key));
        }

        /// <summary>JS <c>a === b</c> für Rohwerte.</summary>
        private static bool strictEq(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is double da && b is double db) return da == db;
            if (a is string sa && b is string sb) return sa == sb;
            if (a is bool ba && b is bool bb) return ba == bb;
            return ReferenceEquals(a, b);
        }

        /// <summary>Validiert und repariert geladene Daten feldweise mit sicheren Standardwerten.</summary>
        public static SaveData sanitizeSave(object rawIn)
        {
            SaveData d = defaultSave();
            if (!isObj(rawIn)) return d;
            Dictionary<string, object> raw = (Dictionary<string, object>)rawIn;
            Dictionary<string, object> s = isObj(getv(raw, "settings")) ? (Dictionary<string, object>)getv(raw, "settings") : new Dictionary<string, object>();
            PlayerSettings ds = d.settings;
            object skin = getv(s, "skin");
            d.settings = new PlayerSettings
            {
                masterVolume = num(getv(s, "masterVolume"), ds.masterVolume, 0, 1),
                musicVolume = num(getv(s, "musicVolume"), ds.musicVolume, 0, 1),
                sfxVolume = num(getv(s, "sfxVolume"), ds.sfxVolume, 0, 1),
                muted = @bool(getv(s, "muted"), ds.muted),
                reducedMotion = @bool(getv(s, "reducedMotion"), ds.reducedMotion),
                uiScale = num(getv(s, "uiScale"), ds.uiScale, 0.8, 1.4),
                quality = oneOf(getv(s, "quality"), new[] { "auto", "low", "medium", "high" }, ds.quality),
                showGrid = @bool(getv(s, "showGrid"), ds.showGrid),
                showRanges = @bool(getv(s, "showRanges"), ds.showRanges),
                tutorialSeen = @bool(getv(s, "tutorialSeen"), ds.tutorialSeen),
                lastVariant = oneOf(getv(s, "lastVariant"), new[] { "classic", "mazing" }, ds.lastVariant),
                lastDifficulty = oneOf(getv(s, "lastDifficulty"), new[] { "easy", "normal", "hard" }, ds.lastDifficulty),
                lastBiome = oneOf(getv(s, "lastBiome"), new[] { "random", "forest", "volcano", "frozen", "crystal", "sky", "swamp", "cavern" }, ds.lastBiome),
                lastSize = oneOf(getv(s, "lastSize"), new[] { "normal", "large", "huge" }, ds.lastSize),
                lastHeroes = getv(s, "lastHeroes") is List<object> lh ? U.Slice(lh.Where((h) => h is string hs && Heroes.HEROES.ContainsKey(hs)).Select((h) => (string)h).ToList(), 0, 2) : ds.lastHeroes,
                skin = skin is string sks && Achievements.SKINS.Any((k) => k.id == sks) ? sks : ds.skin,
                lockRotation = @bool(getv(s, "lockRotation"), ds.lockRotation),
                keys = Keys.sanitizeKeys(getv(s, "keys")),
            };
            Dictionary<string, object> p = isObj(getv(raw, "progress")) ? (Dictionary<string, object>)getv(raw, "progress") : new Dictionary<string, object>();
            List<string> missionIds = Campaign.CAMPAIGN.Select((m) => m.id).ToList();
            Dictionary<string, double> campaignStars = new Dictionary<string, double>();
            if (isObj(getv(p, "campaignStars")))
                foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)getv(p, "campaignStars"))
                    if (missionIds.Contains(kv.Key) && kv.Value is double) campaignStars[kv.Key] = num(kv.Value, 0, 0, 3);
            Dictionary<string, double> bestEndlessWave = new Dictionary<string, double>();
            if (isObj(getv(p, "bestEndlessWave")))
                foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)getv(p, "bestEndlessWave"))
                    if (kv.Value is double) bestEndlessWave[kv.Key] = num(kv.Value, 0, 0, 100000);
            List<BestScoreEntry> bestScores = new List<BestScoreEntry>();
            if (getv(p, "bestScores") is List<object> bs)
            {
                bestScores = U.Slice(bs.Where(isObj).Select((bo) =>
                {
                    Dictionary<string, object> b = (Dictionary<string, object>)bo;
                    return new BestScoreEntry
                    {
                        mode = oneOf(getv(b, "mode"), new[] { "quick", "campaign", "endless", "daily", "custom", "heroEndless" }, "quick"),
                        score = num(getv(b, "score"), 0, 0, 1e9),
                        seed = getv(b, "seed") is string bseed ? U.Slice(bseed, 0, 40) : "",
                        date = getv(b, "date") is string bdate ? U.Slice(bdate, 0, 30) : "",
                        waves = num(getv(b, "waves"), 0, 0, 100000),
                        victory = @bool(getv(b, "victory"), false),
                    };
                }).ToList(), 0, 50);
            }
            Dictionary<string, double> dailyCompleted = new Dictionary<string, double>();
            if (isObj(getv(p, "dailyCompleted")))
                foreach (KeyValuePair<string, object> kv in (Dictionary<string, object>)getv(p, "dailyCompleted"))
                    if (kv.Value is double) dailyCompleted[U.Slice(kv.Key, 0, 20)] = num(kv.Value, 0, 0, 1e9);
            d.progress = new PlayerProgress
            {
                campaignUnlocked = U.Round(num(getv(p, "campaignUnlocked"), 1, 1, Campaign.CAMPAIGN.Count)),
                campaignCompleted = getv(p, "campaignCompleted") is List<object> cc ? cc.Where((x) => x is string xs && missionIds.Contains(xs)).Select((x) => (string)x).ToList() : new List<string>(),
                campaignStars = campaignStars,
                bestEndlessWave = bestEndlessWave,
                bestScores = bestScores,
                dailyCompleted = dailyCompleted,
                achievements = getv(p, "achievements") is List<object> ac ? U.Slice(ac.Where((x) => x is string).Select((x) => (string)x).ToList(), 0, 100) : new List<string>(),
                totalRuns = U.Round(num(getv(p, "totalRuns"), 0, 0, 1e9)),
                totalVictories = U.Round(num(getv(p, "totalVictories"), 0, 0, 1e9)),
                totalKills = U.Round(num(getv(p, "totalKills"), 0, 0, 1e12)),
                totalBossKills = U.Round(num(getv(p, "totalBossKills"), 0, 0, 1e9)),
                bossesDefeated = getv(p, "bossesDefeated") is List<object> bd ? bd.Where((b) => b is string bss && Enemies.BOSSES.ContainsKey(bss)).Select((b) => (string)b).ToList() : new List<string>(),
                heroMeta = sanitizeHeroMeta(getv(p, "heroMeta")),
            };
            d.customMaps = getv(raw, "customMaps") is List<object> cmaps
                ? U.Slice(cmaps.Where(isObj).Select((m) => (Dictionary<string, object>)m).Where((m) => getv(m, "name") is string && getv(m, "code") is string)
                    .Select((m) => new CustomMapEntry { name = U.Slice(jsString(getv(m, "name")), 0, 40), code = U.Slice(jsString(getv(m, "code")), 0, 20000) }).ToList(), 0, 30)
                : new List<CustomMapEntry>();
            d.resume = getv(raw, "resume") is string res && res.Length < 2000000 ? res : null;
            return d;
        }

        /// <summary>Migration älterer Versionen. v1 → v2: Feld customMaps ergänzt. v2 → v3: Helden, Skins, Gesamtstatistik (Standardwerte über sanitizeSave).</summary>
        public static Dictionary<string, object> migrate(Dictionary<string, object> raw)
        {
            double v = getv(raw, "version") is double rv ? rv : 0;
            Dictionary<string, object> @out = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> kv in raw) @out[kv.Key] = kv.Value;
            if (v < 2 && !(getv(@out, "customMaps") is List<object>)) @out["customMaps"] = new List<object>();
            @out["version"] = SAVE_VERSION;
            return @out;
        }

        // ------------------------------------------------------------ Instanz (TS-Klasse SaveSystem)

        private readonly ISaveStorage store;
        /// <summary>PORT: Ersatz für Date.now() (Millisekunden) für den Sicherungsschlüssel; injizierbar.</summary>
        private readonly Func<double> now;

        public SaveSystem(ISaveStorage store, Func<double> now = null)
        {
            this.store = store;
            this.now = now ?? (() => (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public LoadResult load()
        {
            if (store == null) return new LoadResult { data = defaultSave(), error = "Lokaler Speicher ist nicht verfügbar – Fortschritt wird nicht gespeichert.", backupKey = null };
            string raw = null;
            try
            {
                raw = store.Load(SAVE_KEY);
                if (raw == null)
                {
                    foreach (string k in LEGACY_SAVE_KEYS)
                    {
                        raw = store.Load(k);
                        if (raw != null) break;
                    }
                }
            }
            catch
            {
                return new LoadResult { data = defaultSave(), error = "Lokaler Speicher ist blockiert – Fortschritt wird nicht gespeichert.", backupKey = null };
            }
            if (raw == null) return new LoadResult { data = defaultSave(), error = null, backupKey = null };
            try
            {
                // PORT: Fehlermeldungen des JSON-Parsers weichen von JSON.parse ab.
                object parsed = U.ParseJson(raw);
                if (!isObj(parsed)) throw new Exception("Kein Objekt");
                Dictionary<string, object> po = (Dictionary<string, object>)parsed;
                if (getv(po, "version") is double pv && pv > SAVE_VERSION)
                {
                    throw new Exception($"Spielstand stammt aus einer neueren Version ({U.Fmt(pv)})");
                }
                return new LoadResult { data = sanitizeSave(migrate(po)), error = null, backupKey = null };
            }
            catch (Exception err)
            {
                string backupKey = $"{SAVE_KEY}.backup.{U.Fmt(now())}";
                try
                {
                    store.Save(backupKey, raw);
                }
                catch
                {
                    /* Sicherung nicht möglich */
                }
                string msg = err.Message;
                return new LoadResult
                {
                    data = defaultSave(),
                    error = $"Der Spielstand konnte nicht geladen werden ({msg}). Eine Sicherungskopie wurde unter „{backupKey}“ abgelegt; es wird mit Standardwerten gespielt.",
                    backupKey = backupKey,
                };
            }
        }

        public bool save(SaveData data)
        {
            if (store == null) return false;
            try
            {
                // TS: JSON.stringify({ ...data, version: SAVE_VERSION })
                SaveData copy = new SaveData { version = SAVE_VERSION, settings = data.settings, progress = data.progress, customMaps = data.customMaps, resume = data.resume };
                store.Save(SAVE_KEY, U.ToJson(copy));
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ------------------------------------------------------------ Laufende Partie

        /// <summary>
        /// Prüft einen serialisierten Spielzustand. Partien werden nur in der Bauphase gespeichert,
        /// in der keine Gegner, Projektile oder Zonen existieren – so ist die Wiederherstellung vollständig.
        /// PORT: Prüfungen und Reparaturen laufen auf dem Rohbaum (U.ParseJson); erst am Ende wird er in GameState umgewandelt.
        /// </summary>
        public static DeserializeRunResult deserializeRun(string json)
        {
            try
            {
                object parsed = U.ParseJson(json);
                if (!isObj(parsed)) return new DeserializeRunResult { state = null, error = "Ungültiges Format" };
                Dictionary<string, object> st = (Dictionary<string, object>)parsed;
                if (!(getv(st, "version") is double ver && ver == Setup.STATE_VERSION)) return new DeserializeRunResult { state = null, error = "Spielstand stammt aus einer älteren Version und kann nicht fortgesetzt werden" };
                if (!isObj(getv(st, "map")) || !(getv(st, "towers") is List<object>) || !isObj(getv(st, "rules")) || !isObj(getv(st, "seed"))) return new DeserializeRunResult { state = null, error = "Unvollständiger Spielstand" };
                Dictionary<string, object> rawMap = (Dictionary<string, object>)getv(st, "map");
                MapDefinition map = (MapDefinition)U.ConvertJson(rawMap, typeof(MapDefinition));
                if (!WorldGenerator.validateMap(map).ok) return new DeserializeRunResult { state = null, error = "Karte des Spielstands ist ungültig" };
                if (!(getv(st, "phase") is string ph && ph == "build")) return new DeserializeRunResult { state = null, error = "Nur Bauphasen sind fortsetzbar" };
                if (!(getv(st, "gold") is double) || !(getv(st, "lives") is double) || !(getv(st, "wave") is double)) return new DeserializeRunResult { state = null, error = "Fehlende Kernwerte" };
                List<object> towers = (List<object>)getv(st, "towers");
                foreach (object to in towers)
                {
                    if (!isObj(to) || !keyIn(getv((Dictionary<string, object>)to, "def"), Towers.TOWERS)) return new DeserializeRunResult { state = null, error = "Unbekannter Turmtyp" };
                    Dictionary<string, object> t = (Dictionary<string, object>)to;
                    double cx = jsNumber(getv(t, "cx"));
                    double cy = jsNumber(getv(t, "cy"));
                    if (cx < 0 || cy < 0 || cx >= jsNumber(getv(rawMap, "width")) || cy >= jsNumber(getv(rawMap, "height"))) return new DeserializeRunResult { state = null, error = "Turm außerhalb der Karte" };
                }
                if (!(getv(st, "modifiers") is List<object> mods) || mods.Any((m) => !(m is string ms && Modifiers.MODIFIER_IDS.Contains(ms)))) return new DeserializeRunResult { state = null, error = "Unbekannter Modifikator" };
                st["enemies"] = new List<object>();
                st["projectiles"] = new List<object>();
                st["zones"] = new List<object>();
                st["telegraphs"] = new List<object>();
                st["spawnQueue"] = new List<object>();
                st["paused"] = false;
                if (!(getv(st, "heroes") is List<object>) || !(getv(st, "buffs") is List<object>)) return new DeserializeRunResult { state = null, error = "Heldendaten fehlen" };
                foreach (object ho in (List<object>)getv(st, "heroes"))
                {
                    if (!isObj(ho) || !keyIn(getv((Dictionary<string, object>)ho, "id"), Heroes.HEROES)) return new DeserializeRunResult { state = null, error = "Unbekannter Held" };
                    Dictionary<string, object> h = (Dictionary<string, object>)ho;
                    if (getv(h, "state") is string hs && hs == "active")
                    {
                        // Aktive Helden werden beim Speichern in der Bauphase sicher zurückgezogen.
                        h["state"] = "ready";
                    }
                }
                st["buffs"] = new List<object>();
                if (getv(st, "mode") is string mode && mode == "heroEndless")
                {
                    object heo = getv(st, "he");
                    if (!isObj(heo) || !isObj(getv((Dictionary<string, object>)heo, "champion")) || !keyIn(getv((Dictionary<string, object>)getv((Dictionary<string, object>)heo, "champion"), "id"), Champions.CHAMPIONS) || !(getv((Dictionary<string, object>)heo, "runId") is string)) return new DeserializeRunResult { state = null, error = "Heldendaten des Endlosmodus fehlen" };
                    Dictionary<string, object> he = (Dictionary<string, object>)heo;
                    Dictionary<string, object> c = (Dictionary<string, object>)getv(he, "champion");
                    if (!(getv(c, "inventory") is List<object>) || !(getv(c, "equipment") is List<object> eqList) || eqList.Count != 6 || !(getv(c, "potions") is List<object>)) return new DeserializeRunResult { state = null, error = "Inventar beschädigt" };
                    // Keine doppelten Items: jedes Item höchstens einmal, nur bekannte IDs
                    HashSet<object> seen = new HashSet<object>();
                    List<object> inv = new List<object>();
                    foreach (object i in (List<object>)getv(c, "inventory"))
                    {
                        if (!isObj(i)) continue;
                        object item = getv((Dictionary<string, object>)i, "item");
                        if (!keyIn(item, Items.ITEMS) || seen.Contains(item ?? NULL_KEY)) continue;
                        seen.Add(item ?? NULL_KEY);
                        inv.Add(i);
                    }
                    c["inventory"] = inv;
                    HashSet<object> uids = new HashSet<object>();
                    foreach (object i in inv) uids.Add(getv((Dictionary<string, object>)i, "uid") ?? NULL_KEY);
                    c["equipment"] = eqList.Select((u) => u is double && uids.Contains(u) ? u : null).ToList();
                    c["path"] = new List<object>();
                    c["channel"] = 0.0;
                    if (getv(he, "towerBuffs") is List<object> tbs)
                    {
                        List<object> keepB = new List<object>();
                        foreach (object b in tbs)
                        {
                            if (b == null) throw new Exception("Cannot read properties of null (reading 'tower')");
                            object bt = b is Dictionary<string, object> bd ? getv(bd, "tower") : null;
                            if (towers.Any((t) => strictEq(getv((Dictionary<string, object>)t, "uid"), bt))) keepB.Add(b);
                        }
                        he["towerBuffs"] = keepB;
                    }
                    else he["towerBuffs"] = new List<object>();
                    he["zones"] = new List<object>();
                    if (!(getv(he, "pickups") is List<object>)) he["pickups"] = new List<object>();
                    if (!isObj(getv(he, "passive"))) he["passive"] = new Dictionary<string, object>();
                }
                else st["he"] = null;
                GameState state = (GameState)U.ConvertJson(st, typeof(GameState));
                return new DeserializeRunResult { state = state, error = null };
            }
            catch (Exception err)
            {
                return new DeserializeRunResult { state = null, error = err.Message };
            }
        }

        /// <summary>Platzhalter für null-Werte in HashSet&lt;object&gt; (HashSet erlaubt null, aber der Klarheit halber).</summary>
        private static readonly object NULL_KEY = new object();

        public static bool canSerializeRun(GameState s)
        {
            return s.phase == "build" && s.enemies.Count == 0 && s.projectiles.Count == 0 && s.spawnQueue.Count == 0;
        }

        /// <summary>
        /// Gemeinsame Heldenstufe (2.9.5). Ältere Spielstände hatten eine Stufe je Held – übernommen wird der weiteste
        /// Stand (höchste Gesamt-EP) aller Helden, damit niemand Fortschritt verliert.
        /// </summary>
        public static HeroLevelProgress sanitizeHeroLevel(object raw, Dictionary<string, ChampionProgress> champions)
        {
            Func<double, double, double> total = (l, x) => { double n = x; for (double k = 1; k < l; k++) n += HeroEndless.xpToNext(k); return n; };
            HeroLevelProgress best = new HeroLevelProgress { level = 1, xp = 0 };
            Action<double, double> consider = (l, x) =>
            {
                double level = U.Round(U.Max(1, U.Min(Paragon.PARAGON.heroMaxLevel, U.Or(l, 1))));
                double xp = level >= Paragon.PARAGON.heroMaxLevel ? 0 : U.Max(0, U.Min(HeroEndless.xpToNext(level), U.Or(x, 0)));
                if (total(level, xp) > total(best.level, best.xp)) best = new HeroLevelProgress { level = level, xp = xp };
            };
            if (isObj(raw)) consider(jsNumber(getv((Dictionary<string, object>)raw, "level")), jsNumber(getv((Dictionary<string, object>)raw, "xp")));
            foreach (ChampionProgress c in champions.Values) if (c != null) consider(c.level, c.xp);
            return best;
        }
    }
}
