// Übersetzung von sim/setup.ts.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public class RunSetup
    {
        public string mode;         // GameMode
        public string seedText;
        public string variant;      // MapVariant
        public string biome;        // BiomeId | 'random'
        public string difficulty;   // Difficulty
        public string missionId;    // string | null | undefined
        public MapDefinition customMap;
        public string size;         // MapSize oder null
        public List<string> heroes; // HeroId[] oder null
        /// <summary>Nur im Modus 'heroEndless': Held, Startausrüstung und Fortschritts-Schnappschuss.</summary>
        public HeroEndlessSetup heroEndless;
    }

    /// <summary>TS: Rückgabetyp von resolveSetup <c>{ seed; variant; biome; size }</c>.</summary>
    public class ResolvedSetup
    {
        public WorldSeed seed;
        public string variant;   // MapVariant
        public string biome;     // BiomeId | 'random'
        public string size;      // MapSize
    }

    public static partial class Setup
    {
        public const double STATE_VERSION = 2;

        public static RunStats emptyStats()
        {
            return new RunStats
            {
                kills = 0, leaks = 0, livesLost = 0, goldEarned = 0, goldSpent = 0, goldPerWave = new List<double>(),
                damageByTower = new Dictionary<string, double>(), builtByTower = new Dictionary<string, double>(),
                bossesKilled = new List<string>(), bossesFaced = new List<string>(), reactions = new Dictionary<string, double>(),
                wavesCleared = 0, maxNetworkSize = 0, duration = 0,
                heroDeploys = 0, heroKills = 0, heroDowns = 0, fusions = 0, activesUsed = 0, mutationsKilled = 0,
            };
        }

        /// <summary>TS-Spread <c>{ ...base }</c> (flache Kopie).</summary>
        private static MissionRules copyRules(MissionRules b)
        {
            return new MissionRules
            {
                waves = b.waves,
                startGold = b.startGold,
                lives = b.lives,
                bosses = b.bosses,
                enemyResistBonus = b.enemyResistBonus,
                towerLimitOff = b.towerLimitOff,
                veinMul = b.veinMul,
                modifierWaves = b.modifierWaves,
                bannedTowers = b.bannedTowers,
                startModifiers = b.startModifiers,
                endless = b.endless,
                heroMustSurvive = b.heroMustSurvive,
                forcedHeroes = b.forcedHeroes,
            };
        }

        /// <summary>Leitet die Regeln einer Partie aus Modus, Mission bzw. Tages-Seed ab.</summary>
        public static MissionRules createRules(RunSetup setup, WorldSeed seed)
        {
            var diff = Balance.DIFFICULTY[setup.difficulty];
            MissionRules @base = new MissionRules
            {
                waves = Balance.QUICK_RUN.waves,
                startGold = U.Round(Balance.ECONOMY.startGold[setup.variant] * diff.goldMul),
                lives = diff.lives,
                bosses = new Dictionary<double, string>(),
                enemyResistBonus = new Dictionary<string, double>(),
                veinMul = Balance.ECONOMY.veinMul,
                modifierWaves = new List<double>(Balance.QUICK_RUN.modifierWaves),
                bannedTowers = new List<string>(),
                startModifiers = new List<string>(),
                endless = false,
                // PORT: optionale Felder der TS-Basis fehlen (undefined) → null
                towerLimitOff = null,
                heroMustSurvive = null,
                forcedHeroes = null,
            };
            Rng rng = new Rng(RngUtil.hashSeed(seed.hash, "rules"));
            List<string> bossOrder = rng.shuffle(new List<string>(Enemies.BOSS_ORDER));
            List<string> lateOrder = rng.shuffle(new List<string>(Enemies.LATE_BOSS_ORDER));
            bool big = (setup.size ?? "normal") != "normal";
            // Größere Karten: etwas mehr Startgold, da die Wege länger und die Bauflächen weiter verteilt sind.
            double sizeGold = setup.size == "huge" ? 1.35 : setup.size == "large" ? 1.15 : 1;
            @base.startGold = U.Round(@base.startGold * sizeGold);
            // Mehrere Risse bedeuten mehr Druck: größere Karten erhalten zusätzlich mehr Kernenergie.
            @base.lives = U.Round(@base.lives * (setup.size == "huge" ? 1.3 : setup.size == "large" ? 1.15 : 1));
            switch (setup.mode)
            {
                case "campaign":
                {
                    var m = Campaign.CAMPAIGN.Find((x) => x.id == setup.missionId);
                    if (m == null) throw new Exception($"Unbekannte Mission: {setup.missionId ?? "null"}");
                    MissionRules r = copyRules(@base);
                    r.waves = m.waves;
                    r.startGold = U.Round(m.startGold * diff.goldMul);
                    r.lives = U.Round(m.lives * (diff.lives / 20));
                    // PORT: MissionDefinition.bosses als Dictionary<double, string> angenommen (wie MissionRules.bosses).
                    r.bosses = new Dictionary<double, string>(m.bosses);
                    r.enemyResistBonus = new Dictionary<string, double>(m.enemyResistBonus);
                    r.veinMul = m.veinMul;
                    r.modifierWaves = new List<double>(m.modifierWaves);
                    r.bannedTowers = new List<string>(m.bannedTowers);
                    r.startModifiers = new List<string>(m.startModifiers);
                    r.forcedHeroes = m.forcedHeroes != null ? new List<string>(m.forcedHeroes) : null;
                    r.heroMustSurvive = m.heroMustSurvive;
                    return r;
                }
                case "heroEndless":
                case "endless":
                {
                    Dictionary<double, string> bosses = new Dictionary<double, string>();
                    MissionRules r = copyRules(@base);
                    r.waves = 0;
                    r.endless = true;
                    r.bosses = bosses;
                    r.modifierWaves = new List<double>();
                    return r;
                }
                case "daily":
                {
                    string startMod = rng.pick(Modifiers.MODIFIER_IDS.Where((m) => m != "glassCannon" && m != "bulwark").ToList());
                    MissionRules r = copyRules(@base);
                    r.waves = 15;
                    r.bosses = new Dictionary<double, string> { [8] = bossOrder[0], [15] = lateOrder[0] };
                    r.modifierWaves = new List<double> { 3, 6, 10, 13 };
                    r.startModifiers = new List<string> { startMod };
                    return r;
                }
                default:
                {
                    // Große Karten: der Endboss stammt aus den späten Bossen (Schlund, Zwillinge, Weltenbrecher).
                    MissionRules r = copyRules(@base);
                    Dictionary<double, string> bosses = new Dictionary<double, string>();
                    bosses[Balance.QUICK_RUN.bosses[0]] = bossOrder[0];
                    bosses[Balance.QUICK_RUN.bosses[1]] = big || rng.chance(0.5) ? lateOrder[0] : bossOrder[1];
                    r.bosses = bosses;
                    return r;
                }
            }
        }

        /// <summary>Boss für Endloswellen: zyklisch in seedabhängiger Reihenfolge.</summary>
        public static string endlessBossFor(double seedHash, double wave)
        {
            if (wave % Balance.ENDLESS.bossEvery != 0) return null;
            Rng r = new Rng(RngUtil.hashSeed(seedHash, "endlessBoss"));
            List<string> first = r.shuffle(new List<string>(Enemies.BOSS_ORDER));
            List<string> second = r.shuffle(new List<string>(Enemies.LATE_BOSS_ORDER));
            List<string> order = U.Concat(first, second);
            return order[(int)((wave / Balance.ENDLESS.bossEvery - 1) % order.Count)];
        }

        public static bool isModifierWave(MissionRules rules, double wave)
        {
            if (rules.endless) return wave > 0 && wave % Balance.ENDLESS.modifierEvery == 0;
            return rules.modifierWaves.Contains(wave);
        }

        public static ResolvedSetup resolveSetup(RunSetup setup)
        {
            if (setup.mode == "campaign")
            {
                var m = Campaign.CAMPAIGN.Find((x) => x.id == setup.missionId);
                if (m == null) throw new Exception($"Unbekannte Mission: {setup.missionId ?? "null"}");
                return new ResolvedSetup { seed = WorldGenerator.makeSeed(m.seed), variant = m.variant, biome = m.biome, size = m.size ?? "normal" };
            }
            WorldSeed seed = WorldGenerator.makeSeed(setup.seedText);
            if (setup.mode == "daily")
            {
                Rng r = new Rng(RngUtil.hashSeed(seed.hash, "dailyVariant"));
                string variant = r.chance(0.5) ? "classic" : "mazing";
                return new ResolvedSetup { seed = seed, variant = variant, biome = "random", size = r.chance(0.5) ? "large" : "normal" };
            }
            return new ResolvedSetup { seed = seed, variant = setup.variant, biome = setup.biome, size = setup.size ?? "normal" };
        }

        public static HeroInstance makeHero(string id, double uid)
        {
            HeroDefinition def = Heroes.HEROES[id];
            return new HeroInstance
            {
                id = id, uid = uid, state = "ready", mode = "hold", x = 0, y = 0, ax = 0, ay = 0, hp = def.@base.maxHp, level = 1, xp = 0, perks = new List<double>(), pendingPerk = false,
                activeLeft = 0, cooldown = 0, abilityCd = new double[] { 0, 0 }, ult = 0, attackCd = 0, target = 0, buffT = 0, buffKind = "", kills = 0, damageDealt = 0,
                treasureWave = -1, facing = 0, anim = 0, blocking = 0,
            };
        }

        public static GameState createInitialState(RunSetup setup)
        {
            ResolvedSetup res = resolveSetup(setup);
            WorldSeed seed = res.seed;
            string variant = res.variant;
            string biome = res.biome;
            string size = res.size;
            MapDefinition map = setup.customMap != null ? structuredCloneMap(setup.customMap) : WorldGenerator.generateWorld(new GenerateOptions { seed = seed, variant = variant, biome = biome, size = size });
            // TS: { ...setup, variant: map.variant, size: map.size ?? size }
            RunSetup rulesSetup = new RunSetup
            {
                mode = setup.mode, seedText = setup.seedText, variant = map.variant, biome = setup.biome, difficulty = setup.difficulty,
                missionId = setup.missionId, customMap = setup.customMap, size = map.size ?? size, heroes = setup.heroes, heroEndless = setup.heroEndless,
            };
            MissionRules rules = createRules(rulesSetup, seed);
            List<string> heroIds;
            if (setup.mode == "heroEndless") heroIds = new List<string>();
            else
            {
                List<string> a = rules.forcedHeroes ?? setup.heroes ?? new List<string>();
                heroIds = U.Slice(a.Where((h, i) => Heroes.HEROES.ContainsKey(h) && a.IndexOf(h) == i).ToList(), 0, Heroes.HERO_RULES.loadoutSize);
            }
            GameState state = new GameState
            {
                version = STATE_VERSION,
                seed = seed,
                mode = setup.mode,
                difficulty = setup.difficulty,
                missionId = setup.missionId ?? null,
                rules = rules,
                map = map,
                tick = 0,
                time = 0,
                gold = rules.startGold,
                lives = rules.lives,
                maxLives = rules.lives,
                wave = 0,
                phase = "build",
                waveTime = 0,
                currentWave = null,
                spawnQueue = new List<SpawnEntry>(),
                towers = new List<TowerInstance>(),
                enemies = new List<EnemyInstance>(),
                projectiles = new List<Projectile>(),
                zones = new List<Zone>(),
                telegraphs = new List<Telegraph>(),
                modifiers = new List<string>(rules.startModifiers),
                modOffer = null,
                rng = new RngState { combat = RngUtil.hashSeed(seed.hash, "combat"), mods = RngUtil.hashSeed(seed.hash, "mods") },
                nextUid = 1,
                boss = null,
                network = new NetworkState { links = new List<NetworkLink>(), groups = new List<NetworkGroup>(), version = 0 },
                stats = emptyStats(),
                netZapTimer = 0,
                speed = 1,
                paused = false,
                score = 0,
                heroes = heroIds.Select((h, i) => makeHero(h, 900000 + i)).ToList(),
                riftEnergy = Heroes.HERO_RULES.startEnergy,
                buffs = new List<GlobalBuff>(),
                he = null,
            };
            if (setup.mode == "heroEndless")
            {
                // PORT: Klassenname des TS-Inline-Typs HeroEndlessSetup.meta angenommen als HeroEndlessSetupMeta (HeroEndlessSystem.cs).
                HeroEndlessSetup hes = setup.heroEndless ?? new HeroEndlessSetup
                {
                    champion = "lyra",
                    startKit = new List<string>(),
                    meta = new HeroEndlessSetupMeta { bestWave = 0, unlockedItems = new List<string>(), fragments = new Dictionary<string, double>() },
                    runId = $"{seed.text}-{toBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())}",
                };
                HeroEndlessSystem.initHeroEndless(state, hes);
            }
            return state;
        }

        private static MapDefinition structuredCloneMap(MapDefinition m)
        {
            return U.DeepClone(m);
        }

        /// <summary>JS <c>Date.now().toString(36)</c>.</summary>
        private static string toBase36(long v)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (v == 0) return "0";
            bool neg = v < 0;
            ulong u = neg ? (ulong)(-v) : (ulong)v;
            var sb = new System.Text.StringBuilder();
            while (u > 0)
            {
                sb.Insert(0, digits[(int)(u % 36)]);
                u /= 36;
            }
            if (neg) sb.Insert(0, '-');
            return sb.ToString();
        }
    }
}
