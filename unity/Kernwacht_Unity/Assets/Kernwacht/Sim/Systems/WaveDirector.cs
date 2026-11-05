// Übersetzung von sim/WaveDirector.ts.
// Wellendirektor: erzeugt Wellen deterministisch aus Seed + Wellennummer (eigener Zufallsstrom
// pro Welle). Dadurch ist die Vorschau der nächsten Welle exakt und unabhängig vom Kampfverlauf.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class WaveDirector
    {
        private static readonly Dictionary<string, double> SPACING = new Dictionary<string, double>
        {
            { "runner", 0.55 }, { "swarm", 0.22 }, { "brute", 1.6 }, { "regen", 0.9 }, { "flyer", 0.8 }, { "shield", 1.1 }, { "phase", 0.9 },
            { "saboteur", 1.0 }, { "scout", 0.7 }, { "broodmother", 2.0 }, { "golem", 2.2 }, { "stalker", 0.9 }, { "siege", 1.8 }, { "shaman", 1.4 }, { "tunneler", 1.2 }, { "griffin", 0.7 },
        };

        private static readonly Dictionary<string, double> SIZE_BUDGET = new Dictionary<string, double> { { "normal", 1 }, { "large", 1.05 }, { "huge", 1.1 } };

        public static string bossForWave(GameState s, double n)
        {
            if (s.rules.endless) return Setup.endlessBossFor(s.seed.hash, n);
            return U.Get(s.rules.bosses, n, null);
        }

        public static WaveDefinition planWave(GameState s, double n)
        {
            Rng rng = new Rng(RngUtil.hashSeed(s.seed.hash, $"wave-{U.Fmt(n)}-{s.mode}-{s.map.variant}"));
            bool endless = s.rules.endless;
            bool challenge = Scaling.isChallengeWave(n, endless);
            // Challenge-Wellen haben immer einen Boss (auch außerhalb des 10er-Rhythmus)
            string boss = bossForWave(s, n) ?? (challenge ? Setup.endlessBossFor(s.seed.hash, U.Max(10, U.Round(n / 10) * 10)) : null);
            double sizeMul = SIZE_BUDGET[s.map.size ?? "normal"];
            // Helden-Endlos: datengetriebene Wellenprofile verändern Zusammensetzung, Budget, Menge und Tempo.
            WaveProfile profile = s.mode == "heroEndless" ? HeroEndless.profileForWave(s.seed.hash, n) : null;
            double budget = Balance.waveBudget(n) * sizeMul * (profile != null ? profile.budgetMul : 1);
            List<WaveGroup> groups = new List<WaveGroup>();
            if (boss != null)
            {
                groups.Add(new WaveGroup { enemy = "boss", count = 1, elite = false, boss = boss, challenge = challenge ? (bool?)true : null });
                budget *= 0.35;
            }
            List<string> pool = Enemies.WAVE_ENEMIES.Where((id) => Enemies.ENEMIES[id].minWave <= n)
                .Where((id) => id != "scout" || s.map.variant == "mazing").ToList();
            double typeCount = U.Min(pool.Count, 1 + (n >= 4 ? 1 : 0) + (n >= 10 ? 1 : 0) + (endless && n >= 20 ? 1 : 0) + (sizeMul > 1 && n >= 7 ? 1 : 0) + (profile != null && n >= 6 ? profile.extraTypes : 0)
                // Unity 1.1: Helden-Endlos mischt früher mehrere Sorten
                + (s.mode == "heroEndless" && n >= 2 ? 1 : 0) + (s.mode == "heroEndless" && n >= 14 ? 1 : 0));
            List<string> chosen = new List<string>();
            List<string> avail = new List<string>(pool);
            for (int k = 0; k < typeCount && avail.Count > 0; k++)
            {
                string pick = rng.weighted(avail, (id) =>
                {
                    double fresh = n - Enemies.ENEMIES[id].minWave;
                    double w = fresh < 3 ? 3 : 1.4;
                    if (id == "flyer" && s.map.variant == "mazing") w *= 1.8;
                    if (id == "runner") w *= n < 4 ? 2 : 0.8;
                    if (profile != null) w *= U.GetN(profile.weights, id) ?? 1;
                    return w;
                });
                chosen.Add(pick);
                avail.RemoveAt(avail.IndexOf(pick));
            }
            double cap = U.Round(Balance.waveCountCap(n) * sizeMul * (profile != null ? profile.capMul : 1));
            double total = 0;
            double share = budget / U.Max(1, chosen.Count);
            // Unity 1.1: Budget nach Bedrohung^0,6 verteilen – teure Sorten bekommen mehr, billige weniger.
            // Vorher gleiche Budgetanteile: 32 Splitterschwärme neben einer einzelnen Aschenschwinge.
            double wSum = 0;
            foreach (string id in chosen) wSum += Math.Pow(Enemies.ENEMIES[id].threat, 0.6);
            // 3.11: Späte Endloswellen stoßen an die Mengenobergrenze – dann bestimmte bisher allein der Gegnertyp die Wellen-LP
            // (90 Aschenschwingen ≈ 16 Mio., 90 Höhlengolems ≈ 88 Mio.). Ab Welle 100 wird die Menge stattdessen nach Lebenspunkten
            // verteilt: jede Gruppe erhält denselben LP-Anteil, Splitter beim Tod zählen mit.
            if (endless && n >= Scaling.SCALING.smoothFrom)
            {
                double lpBudget = cap * Scaling.SCALING.smoothHpPerSlot;
                Func<string, double> eff = (id) => Enemies.ENEMIES[id].hp + (Enemies.ENEMIES[id].onDeathSpawn != null ? Enemies.ENEMIES[Enemies.ENEMIES[id].onDeathSpawn.enemy].hp * Enemies.ENEMIES[id].onDeathSpawn.count : 0);
                foreach (string id in chosen)
                {
                    double count = U.Max(1, U.Min(cap, U.Round(lpBudget / chosen.Count / eff(id))));
                    groups.Add(new WaveGroup { enemy = id, count = count, elite = false, boss = null });
                }
            }
            else foreach (string id in chosen)
            {
                // Neu eingeführte Gegnertypen erscheinen zunächst in kleinerer Zahl.
                double fresh = n - Enemies.ENEMIES[id].minWave < 2 && chosen.Count > 1 ? 0.6 : 1;
                double myShare = wSum > 0 ? budget * Math.Pow(Enemies.ENEMIES[id].threat, 0.6) / wSum : share;
                double count = U.Max(1, Math.Floor((myShare * fresh) / Enemies.ENEMIES[id].threat));
                if (total + count > cap) count = U.Max(1, cap - total);
                total += count;
                groups.Add(new WaveGroup { enemy = id, count = count, elite = false, boss = null });
            }
            // Elitegegner: ab Welle 5 regelmäßig, im Endlosmodus zunehmend.
            bool eliteWave = n >= 5 && (n % 4 == 1 || (endless && n >= 15 && n % 2 == 1) || (endless && n >= Scaling.SCALING.pressureFrom));
            if (eliteWave)
            {
                List<string> elitePool = new List<string> { "brute", "shield", "regen", "runner", "phase", "saboteur", "golem", "siege", "shaman" }.Where((id) => Enemies.ENEMIES[id].minWave <= n).ToList();
                double count = endless ? 1 + Math.Floor(n / 15) : 1;
                // Ab Welle 8 tragen Elitegegner eine (angekündigte) Mutation.
                string mutation = n >= 8 ? rng.pick(Enemies.MUTATION_IDS) : null;
                string enemy = rng.pick(elitePool);
                groups.Add(new WaveGroup { enemy = enemy, count = U.Min(4, count), elite = true, boss = null, mutation = mutation });
            }
            if (profile != null)
            {
                List<string> elitePool = new List<string> { "brute", "shield", "regen", "runner", "golem", "siege", "shaman" }.Where((id) => Enemies.ENEMIES[id].minWave <= n).ToList();
                if (profile.extraElites > 0 && elitePool.Count > 0) groups.Add(new WaveGroup { enemy = rng.pick(elitePool), count = profile.extraElites + Math.Floor(n / 20), elite = true, boss = null, mutation = null });
                // Mini-Boss: alle n Wellen (nicht in Bosswellen) ein stark verstärkter Elitegegner
                bool late = n >= Scaling.SCALING.pressureFrom;
                double miniEvery = late ? Scaling.SCALING.miniBossEveryLate : HeroEndless.HERO_ENDLESS.miniBossEvery;
                double miniFrom = late ? Scaling.SCALING.pressureFrom : HeroEndless.HERO_ENDLESS.miniBossFrom;
                if (boss == null && n >= HeroEndless.HERO_ENDLESS.miniBossFrom && (n - miniFrom) % miniEvery == 0 && elitePool.Count > 0)
                {
                    string enemy = rng.pick(elitePool.Where((id) => id != "runner").ToList()) ?? elitePool[0];
                    string mutation = n >= 10 ? rng.pick(Enemies.MUTATION_IDS) : null;
                    groups.Add(new WaveGroup { enemy = enemy, count = 1, elite = true, boss = null, mutation = mutation, miniBoss = true });
                }
            }
            string ev = s.map.terrainEvent;
            string @event = !string.IsNullOrEmpty(ev) && n % Biomes.TERRAIN_EVENTS[ev].every == 0 ? ev : null;
            // 3.0: Weltstufe und Affixe (eigener Zufallsstrom, damit frühere Wellen unverändert bleiben)
            double tier = Scaling.worldTier(n, endless);
            List<string> affixes = new List<string>();
            string immune = null;
            double nAff = Scaling.affixCount(n, endless);
            if (nAff > 0)
            {
                Rng ar = new Rng(RngUtil.hashSeed(s.seed.hash, $"affix-{U.Fmt(n)}"));
                List<string> apool = new List<string>(Scaling.AFFIX_ORDER);
                for (int k = 0; k < nAff && apool.Count > 0; k++)
                {
                    int idx = (int)(Math.Floor(ar.next() * apool.Count) % apool.Count);
                    string a = apool[idx];
                    apool.RemoveAt(idx);
                    affixes.Add(a);
                }
                if (affixes.Contains("immune"))
                {
                    List<string> it = new List<string>(Scaling.IMMUNE_TYPES);
                    immune = it[(int)(Math.Floor(ar.next() * it.Count) % it.Count)];
                }
            }
            return new WaveDefinition
            {
                index = n,
                groups = groups,
                hpMul = Balance.waveHpMul(n, endless) * (1 + Scaling.SCALING.tierHp * tier),
                speedMul = (@event != null ? Biomes.TERRAIN_EVENTS[@event].enemySpeedMul : 1) * (profile != null ? profile.speedMul : 1) * (1 + U.Min(Scaling.SCALING.tierSpeedMax, Scaling.SCALING.tierSpeed * tier)) * (affixes.Contains("swift") ? 1.2 : 1),
                rewardMul = Balance.killRewardMul(n) * (@event != null ? Biomes.TERRAIN_EVENTS[@event].goldMul : 1) * (1 + Scaling.SCALING.tierReward * tier),
                @event = @event,
                isBoss = boss != null,
                tier = tier > 0 ? (double?)tier : null,
                affixes = affixes.Count > 0 ? affixes : null,
                immune = !string.IsNullOrEmpty(immune) ? immune : null,
            };
        }

        private static List<SpawnEntry> buildQueue(WaveDefinition wave)
        {
            List<SpawnEntry> q = new List<SpawnEntry>();
            double t = 1.0;
            Func<WaveGroup, SpawnEntry> entry = (g) => new SpawnEntry
            {
                at = t, enemy = g.enemy, elite = g.elite, boss = g.boss, mutation = g.mutation ?? null,
                miniBoss = g.miniBoss == true ? (bool?)true : null,
                challenge = g.challenge == true ? (bool?)true : null,
            };
            // Bosse zuerst (wie bisher)
            foreach (WaveGroup g in wave.groups)
            {
                if (string.IsNullOrEmpty(g.boss)) continue;
                for (int i = 0; i < g.count; i++) { q.Add(entry(g)); t += 3; }
                t += 2.2;
            }
            // Unity 1.1: übrige Gruppen ineinander verzahnt statt nacheinander – jede Welle ist durchgehend gemischt.
            // Reihenfolge: immer die Gruppe, von der anteilig am meisten übrig ist (gleichmäßig über die Welle verteilt).
            List<WaveGroup> rest = wave.groups.Where((g) => string.IsNullOrEmpty(g.boss) && g.count > 0).ToList();
            double[] left = rest.Select((g) => g.count).ToArray();
            while (true)
            {
                int best = -1;
                double bestFrac = 0;
                for (int i = 0; i < rest.Count; i++)
                {
                    if (left[i] <= 0) continue;
                    double frac = left[i] / rest[i].count;
                    if (best < 0 || frac > bestFrac + 1e-9) { best = i; bestFrac = frac; }
                }
                if (best < 0) break;
                WaveGroup g = rest[best];
                q.Add(entry(g));
                left[best]--;
                double gap = U.GetN(SPACING, g.enemy) ?? 0.8;
                t += g.elite ? 1.6 : gap;
            }
            return q;
        }

        public static void startNextWave(GameSimulation sim)
        {
            GameState s = sim.s;
            s.wave++;
            s.phase = "combat";
            sim.c.statsDirty = true;
            s.waveTime = 0;
            foreach (TowerInstance t in s.towers) t.builtInPhase = false;
            s.currentWave = planWave(s, s.wave);
            s.spawnQueue = buildQueue(s.currentWave);
            sim.c.statsDirty = true; // Geländeereignisse können Turmwerte verändern
            sim.emit(new GameEvent { k = "waveStart", wave = s.wave, isBoss = s.currentWave.isBoss });
            double tier = s.currentWave.tier ?? 0;
            if (tier > 0 && Scaling.worldTier(s.wave - 1, s.rules.endless) < tier)
            {
                sim.emit(new GameEvent { k = "message", text = $"🌐 Weltstufe {U.Fmt(tier)} erreicht: Gegner deutlich stärker – Kopfgeld +{U.Fmt(U.Round(Scaling.SCALING.tierReward * tier * 100))} %", severity = "danger" });
            }
            if (s.currentWave.affixes != null && s.currentWave.affixes.Count > 0)
            {
                sim.emit(new GameEvent { k = "message", text = $"Wellen-Affixe: {string.Join(", ", s.currentWave.affixes.Select((a) => $"{Scaling.AFFIXES[a].icon} {Scaling.AFFIXES[a].name}"))}", severity = "warn" });
            }
            if (!string.IsNullOrEmpty(s.currentWave.@event))
            {
                var ev = Biomes.TERRAIN_EVENTS[s.currentWave.@event];
                sim.emit(new GameEvent { k = "message", text = $"Geländeereignis: {ev.name} – {ev.description}", severity = "info" });
            }
        }

        public static void updateSpawns(GameSimulation sim, double _dt)
        {
            GameState s = sim.s;
            while (s.spawnQueue.Count > 0 && s.spawnQueue[0].at <= s.waveTime)
            {
                SpawnEntry e = U.Shift(s.spawnQueue);
                EnemyInstance en = EnemySystem.spawnEnemy(sim, e.enemy, e.elite, e.boss, e.mutation ?? null);
                if (e.miniBoss == true)
                {
                    en.miniBoss = true;
                    en.maxHp *= HeroEndless.HERO_ENDLESS.miniBossHpMul;
                    en.hp = en.maxHp;
                    en.reward *= 4;
                    sim.emit(new GameEvent { k = "message", text = "Ein Mini-Boss nähert sich!", severity = "warn" });
                }
                if (e.challenge == true) ChallengeSystem.makeChallenge(sim, en);
            }
        }

        public static void checkWaveEnd(GameSimulation sim)
        {
            GameState s = sim.s;
            if (s.phase != "combat") return;
            if (s.spawnQueue.Count > 0 || s.enemies.Any((e) => e.alive)) return;
            completeWave(sim);
        }

        /// <summary>
        /// Helden-Endlos: Die nächste Welle vorzeitig rufen, sobald die aktuelle vollständig erschienen ist.
        /// Die laufende Welle gilt als überstanden (Belohnung sofort), übrige Gegner bleiben auf dem Feld.
        /// Wer früh ruft, erhält einen kleinen Goldbonus. Rückgabe: Grund, falls nicht möglich.
        /// </summary>
        public static string earlyCallState(GameState s)
        {
            if (s.mode != "heroEndless") return "Nur im Helden-Endlosmodus";
            if (s.phase == "build") return null;
            if (s.phase != "combat") return "Erst Modifikator wählen";
            if (s.spawnQueue.Count > 0) return $"Die aktuelle Welle erscheint noch ({U.Fmt(s.spawnQueue.Count)} Gegner ausstehend)";
            if (s.enemies.Any((e) => e.alive && !string.IsNullOrEmpty(e.boss))) return "Nicht während eines Bosskampfs";
            return null;
        }

        public const double EARLY_CALL_GOLD = 4;

        public static string callNextWave(GameSimulation sim)
        {
            GameState s = sim.s;
            string reason = earlyCallState(s);
            if (!string.IsNullOrEmpty(reason)) return reason;
            if (s.phase == "combat")
            {
                double alive = s.enemies.Count((e) => e.alive);
                double bonus = EARLY_CALL_GOLD * U.Max(1, alive);
                s.gold += bonus;
                s.stats.goldEarned += bonus;
                completeWave(sim, true);
                if (s.phase != "build") return null; // Modifikatorauswahl zuerst
            }
            startNextWave(sim);
            return null;
        }

        public static void completeWave(GameSimulation sim, bool early = false)
        {
            GameState s = sim.s;
            ModAggregate mods = sim.c.mods;
            bool wasBoss = s.currentWave?.isBoss ?? false;
            double bonus = U.Round((Balance.ECONOMY.waveBonusBase + Balance.ECONOMY.waveBonusPerWave * s.wave + (wasBoss ? Balance.ECONOMY.bossWaveBonus : 0)) * mods.waveGoldMul);
            double income = EconomySystem.collectIncome(sim);
            HeroSystem.onWaveCleared(sim);
            double interest = 0;
            if (mods.interest) interest = U.Min(Balance.ECONOMY.interestCap, Math.Floor(s.gold * Balance.ECONOMY.interestRate));
            double total = bonus + income + interest;
            s.gold += total;
            s.stats.goldEarned += total;
            s.stats.goldPerWave.Add(total);
            s.stats.wavesCleared++;
            if (!early)
            {
                // Bei vorzeitigem Ruf laufen Geschosse und Zonen gegen die verbliebenen Gegner weiter.
                // Scharfe Minen bleiben liegen (Minenfeld über das Wellenende hinaus)
                s.zones = s.zones.Where((z) => z.kind == "mine" && z.armed <= 0 && z.t > 0).ToList();
                s.telegraphs = new List<Telegraph>();
                s.projectiles = new List<Projectile>();
            }
            s.currentWave = null;
            sim.c.statsDirty = true;
            if (wasBoss && s.rules.endless)
            {
                // 3.4: Bosswelle geschafft → ein zusätzlicher Turmplatz
                s.bossSlots = (s.bossSlots ?? 0) + 1;
                sim.emit(new GameEvent { k = "message", text = "Bosswelle geschafft: +1 Turmplatz", severity = "info" });
            }
            sim.emit(new GameEvent { k = "waveClear", wave = s.wave, bonus = total });
            if (!s.rules.endless && s.wave >= s.rules.waves)
            {
                s.phase = "victory";
                sim.emit(new GameEvent { k = "victory" });
                return;
            }
            if (Setup.isModifierWave(s.rules, s.wave))
            {
                s.modOffer = RogueliteSystem.rollOffer(sim);
                s.phase = s.modOffer.Count > 0 ? "choice" : "build";
            }
            else
            {
                s.phase = "build";
            }
        }

        /// <summary>Vorschau für die HUD: Zusammensetzung der nächsten Welle.</summary>
        public static WaveDefinition previewNextWave(GameState s)
        {
            if (!s.rules.endless && s.wave >= s.rules.waves) return null;
            return planWave(s, s.wave + 1);
        }
    }
}
