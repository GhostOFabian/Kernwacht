using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Game
{
    public enum Phase { Build, Combat, Victory, Defeat }

    /// <summary>Rückmeldungen der Simulation an die Darstellung.</summary>
    public interface IGameView
    {
        void EnemySpawned(Enemy e);
        void EnemyDied(Enemy e, bool leaked);
        void TowerChanged(Tower t, bool removed);
        void ProjectileSpawned(Projectile p);
        void ProjectileEnded(Projectile p);
        void MineChanged(Mine m, bool removed);
        void CloudChanged(Cloud c, bool removed);
        void MeteorChanged(Meteor m, bool removed);
        void Beam(Vector2 from, float fromZ, Vector2 to, Color color, float width, float duration);
        void Burst(Vector2 at, float radius, Color color);
        void Shot(Tower t, Vector2 at);
        void FloatText(Vector2 at, string text, Color color);
        void Message(string text, int severity);
    }

    /// <summary>
    /// Spielsimulation des Kerns (Classic-Modus): Raster, Wegfeld, Wellen, Türme, Gegner, Bosse, Status, Gold.
    /// Werte und Formeln stammen aus dem Webspiel (src/sim, src/config); fortgeschrittene Systeme (Helden, Runen,
    /// Netzwerke, Roguelite-Modifikatoren) sind noch nicht übertragen.
    /// </summary>
    public class GameSim
    {
        public const float Dt = 1f / 30f;

        public readonly GameData data;
        public readonly MapDef map;
        public readonly DifficultyDef diff;
        public readonly bool endless;
        public IGameView view;

        public Phase phase = Phase.Build;
        public int gold, lives, wave, totalWaves;
        public float time, waveTime;
        public WaveDef current;
        public Enemy bossEnemy;
        public string bossPhaseName = "";
        public int kills, leaks;

        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Tower> towers = new List<Tower>();
        public readonly List<Projectile> projectiles = new List<Projectile>();
        public readonly List<Mine> mines = new List<Mine>();
        public readonly List<Cloud> clouds = new List<Cloud>();
        public readonly List<Meteor> meteors = new List<Meteor>();
        readonly List<SpawnEntry> queue = new List<SpawnEntry>();
        readonly Tower[] occupancy;
        public readonly int[] field;          // Wegdistanz zum Kern (Zellen), -1 = nicht begehbar
        int nextUid = 1;
        bool statsDirty = true;
        readonly System.Random rng;

        static readonly Dictionary<string, float> Spacing = new Dictionary<string, float>
        {
            { "runner", 0.55f }, { "swarm", 0.22f }, { "brute", 1.6f }, { "regen", 0.9f }, { "flyer", 0.8f }, { "shield", 1.1f }, { "phase", 0.9f },
            { "saboteur", 1.0f }, { "scout", 0.7f }, { "broodmother", 2.0f }, { "golem", 2.2f }, { "stalker", 0.9f }, { "siege", 1.8f },
            { "shaman", 1.4f }, { "tunneler", 1.2f }, { "griffin", 0.7f },
        };

        static readonly Dictionary<string, float> NullShieldMul = new Dictionary<string, float>
        {
            { "physical", 0.25f }, { "fire", 0.5f }, { "frost", 0.5f }, { "lightning", 0.5f }, { "poison", 0.35f }, { "arcane", 1.5f }, { "sonic", 1.0f },
        };

        public GameSim(GameData data, MapDef map, string difficulty, bool endless, int seed)
        {
            this.data = data;
            this.map = map;
            this.endless = endless;
            diff = data.difficulty.TryGetValue(difficulty, out var d) ? d : new DifficultyDef { id = "normal", name = "Normal", hpMul = 1.1f, goldMul = 1, rewardMul = 1, lives = 20 };
            gold = Mathf.RoundToInt(map.startGold * diff.goldMul);
            lives = diff.lives;
            totalWaves = map.waveCount;
            occupancy = new Tower[map.width * map.height];
            field = BuildField();
            rng = new System.Random(seed);
        }

        public float Rand() => (float)rng.NextDouble();

        // ================================================================== Raster

        public int Idx(int x, int y) => y * map.width + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < map.width && y < map.height;
        public int CellAt(int x, int y) => InBounds(x, y) ? map.cells[Idx(x, y)] : Cell.Blocked;
        public Tower TowerAt(int x, int y) => InBounds(x, y) ? occupancy[Idx(x, y)] : null;
        public int HeightAt(int x, int y) => InBounds(x, y) ? map.heights[Idx(x, y)] : 0;

        int[] BuildField()
        {
            int n = map.width * map.height;
            var f = new int[n];
            for (int i = 0; i < n; i++) f[i] = -1;
            var q = new Queue<int>();
            int g = Idx(map.goal.x, map.goal.y);
            f[g] = 0;
            q.Enqueue(g);
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % map.width, y = i / map.width;
                for (int k = 0; k < 4; k++)
                {
                    int ax = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ay = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!InBounds(ax, ay)) continue;
                    int j = Idx(ax, ay);
                    if (f[j] >= 0 || !Cell.Walkable(map.cells[j])) continue;
                    f[j] = f[i] + 1;
                    q.Enqueue(j);
                }
            }
            return f;
        }

        /// <summary>Liste der Wegzellen vom Start bis zum Kern (für die Darstellung).</summary>
        public List<Vector2Int> Trace(Vector2Int from)
        {
            var r = new List<Vector2Int> { from };
            var c = from;
            for (int s = 0; s < 4000; s++)
            {
                var n = NextCell(c.x, c.y);
                if (n == c) break;
                r.Add(n);
                c = n;
            }
            return r;
        }

        Vector2Int NextCell(int x, int y)
        {
            int best = InBounds(x, y) ? field[Idx(x, y)] : int.MaxValue;
            if (best < 0) best = int.MaxValue;
            var r = new Vector2Int(x, y);
            for (int k = 0; k < 4; k++)
            {
                int ax = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ay = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (!InBounds(ax, ay)) continue;
                int v = field[Idx(ax, ay)];
                if (v >= 0 && v < best) { best = v; r = new Vector2Int(ax, ay); }
            }
            return r;
        }

        // ================================================================== Bauen

        public string CanBuild(TowerDef def, int x, int y)
        {
            if (def == null) return "Kein Turm gewählt";
            if (def.legendary) return "Legendäre Türme entstehen nur durch Fusion";
            if (def.mazingOnly) return "Nur im Mazing-Modus";
            for (int dy = 0; dy < def.size; dy++)
                for (int dx = 0; dx < def.size; dx++)
                {
                    int cx = x + dx, cy = y + dy;
                    if (!InBounds(cx, cy)) return "Außerhalb der Karte";
                    int c = map.cells[Idx(cx, cy)];
                    if (def.laneOnly) { if (c != Cell.Path) return "Nur auf dem Weg baubar"; }
                    else if (c != Cell.Open) return c == Cell.Path ? "Auf dem Weg kann nicht gebaut werden" : "Hier ist kein Bauplatz";
                    if (occupancy[Idx(cx, cy)] != null) return "Feld ist belegt";
                }
            if (gold < def.cost) return "Nicht genug Gold";
            return null;
        }

        public Tower Build(TowerDef def, int x, int y)
        {
            if (CanBuild(def, x, y) != null) return null;
            gold -= def.cost;
            var t = new Tower { uid = nextUid++, def = def, cx = x, cy = y, invested = def.cost, priority = def.priority };
            t.high = HeightAt(x, y) >= 2;
            t.ley = map.ley.Contains(Idx(x, y));
            towers.Add(t);
            Occupy(t, t);
            statsDirty = true;
            RecomputeStats();
            view?.TowerChanged(t, false);
            return t;
        }

        void Occupy(Tower t, Tower value)
        {
            for (int dy = 0; dy < t.def.size; dy++)
                for (int dx = 0; dx < t.def.size; dx++)
                    if (InBounds(t.cx + dx, t.cy + dy)) occupancy[Idx(t.cx + dx, t.cy + dy)] = value;
        }

        public UpgradeDef NextUpgrade(Tower t) => t.level - 1 < t.def.upgrades.Count ? t.def.upgrades[t.level - 1] : null;

        public string Upgrade(Tower t)
        {
            var u = NextUpgrade(t);
            if (u == null) return "Höchste Stufe erreicht";
            if (gold < u.cost) return "Nicht genug Gold";
            gold -= u.cost;
            t.invested += u.cost;
            t.level++;
            statsDirty = true;
            RecomputeStats();
            view?.TowerChanged(t, false);
            return null;
        }

        public string Specialize(Tower t, int which)
        {
            if (!t.CanSpec) return "Erst alle Stufen ausbauen";
            if (which < 1 || which > t.def.specs.Count) return "Unbekannte Spezialisierung";
            var s = t.def.specs[which - 1];
            if (gold < s.cost) return "Nicht genug Gold";
            gold -= s.cost;
            t.invested += s.cost;
            t.spec = which;
            statsDirty = true;
            RecomputeStats();
            view?.TowerChanged(t, false);
            return null;
        }

        public int RefundValue(Tower t) => Mathf.FloorToInt(t.invested * data.sellRefund);

        public void Sell(Tower t)
        {
            if (!towers.Contains(t)) return;
            gold += RefundValue(t);
            towers.Remove(t);
            Occupy(t, null);
            foreach (var m in mines) if (m.tower == t && !m.dead) { m.dead = true; view?.MineChanged(m, true); }
            mines.RemoveAll(m => m.dead);
            statsDirty = true;
            RecomputeStats();
            view?.TowerChanged(t, true);
        }

        public struct FusionOption { public FusionDef recipe; public Tower partner; public bool ready; }

        public List<FusionOption> FusionOptions(Tower t)
        {
            var r = new List<FusionOption>();
            foreach (var f in data.fusions)
            {
                if (f.a != t.def.id && f.b != t.def.id) continue;
                string other = f.a == t.def.id ? f.b : f.a;
                foreach (var p in towers)
                {
                    if (p == t || p.def.id != other) continue;
                    if (Vector2.Distance(new Vector2(t.cx, t.cy), new Vector2(p.cx, p.cy)) > data.fusionRange) continue;
                    r.Add(new FusionOption { recipe = f, partner = p, ready = t.MaxLevel && t.spec > 0 && p.MaxLevel && p.spec > 0 });
                }
            }
            return r;
        }

        public string Fuse(Tower t, FusionOption o)
        {
            if (!o.ready) return "Beide Türme müssen voll ausgebaut und spezialisiert sein";
            if (gold < o.recipe.cost) return "Nicht genug Gold";
            if (!data.towers.TryGetValue(o.recipe.id, out var def)) return "Fusionsturm unbekannt";
            gold -= o.recipe.cost;
            var p = o.partner;
            towers.Remove(p);
            Occupy(p, null);
            view?.TowerChanged(p, true);
            Occupy(t, null);
            view?.TowerChanged(t, true);
            // Der legendäre Turm entsteht auf dem Feld, das näher am Weg liegt
            if (LaneDistance(p.cx, p.cy) < LaneDistance(t.cx, t.cy)) { t.cx = p.cx; t.cy = p.cy; }
            t.def = def;
            t.level = 1 + def.upgrades.Count;
            t.spec = 0;
            t.invested += p.invested + o.recipe.cost;
            t.priority = def.priority;
            t.cd = 0;
            t.high = HeightAt(t.cx, t.cy) >= 2;
            Occupy(t, t);
            statsDirty = true;
            RecomputeStats();
            view?.TowerChanged(t, false);
            view?.Message($"Legendärer Turm erschaffen: {def.name}!", 0);
            return null;
        }

        int LaneDistance(int x, int y)
        {
            int best = 999;
            for (int i = 0; i < map.cells.Length; i++)
            {
                if (!Cell.Walkable(map.cells[i])) continue;
                int px = i % map.width, py = i / map.width;
                best = Mathf.Min(best, Mathf.Max(Mathf.Abs(px - x), Mathf.Abs(py - y)));
            }
            return best;
        }

        // ================================================================== Turmwerte

        public void RecomputeStats()
        {
            if (!statsDirty) return;
            statsDirty = false;
            foreach (var t in towers)
            {
                var st = t.st;
                st.Clear();
                foreach (var kv in data.zeroStats) st[kv.Key] = kv.Value;
                foreach (var kv in t.def.stats) st[kv.Key] = kv.Value;
                for (int i = 0; i < t.level - 1 && i < t.def.upgrades.Count; i++) ApplyMods(st, t.def.upgrades[i].mods);
                if (t.spec > 0 && t.spec <= t.def.specs.Count) ApplyMods(st, t.def.specs[t.spec - 1].mods);
                if (t.high) st["range"] = Get(st, "range") + 0.4f;
                if (t.ley) st["damage"] = Get(st, "damage") * 1.15f;
            }
            // Unterstützer: Totem (Aura) und Pylon (Netzwerkbonus) verstärken Türme in Reichweite
            foreach (var t in towers) { t.auraDmg = 0; t.auraSpeed = 0; t.pylonBonus = 0; }
            foreach (var s in towers)
            {
                if (s.def.attack == "aura")
                {
                    float r = s.S("range");
                    foreach (var t in towers)
                    {
                        if (t == s || t.def.attack == "aura" || t.def.attack == "economy") continue;
                        if (Vector2.Distance(s.Center, t.Center) > r + 0.5f) continue;
                        t.auraDmg = Mathf.Max(t.auraDmg, s.S("auraDmg"));
                        t.auraSpeed = Mathf.Max(t.auraSpeed, s.S("auraSpeed"));
                    }
                }
                else if (s.def.attack == "pylon")
                {
                    float r = s.S("linkRange");
                    int links = Mathf.RoundToInt(s.S("maxLinks"));
                    var near = new List<Tower>();
                    foreach (var t in towers)
                        if (t != s && t.def.attack != "pylon" && t.def.attack != "economy" && Vector2.Distance(s.Center, t.Center) <= r + 0.5f) near.Add(t);
                    near.Sort((a, b) => Vector2.Distance(s.Center, a.Center).CompareTo(Vector2.Distance(s.Center, b.Center)));
                    for (int i = 0; i < near.Count && i < links; i++) near[i].pylonBonus = Mathf.Max(near[i].pylonBonus, s.S("networkBonus"));
                }
            }
            foreach (var t in towers)
            {
                if (t.auraDmg > 0 || t.pylonBonus > 0) t.st["damageMul"] = Get(t.st, "damageMul") * (1 + t.auraDmg + t.pylonBonus);
                if (t.auraSpeed > 0) t.st["attackSpeedMul"] = Get(t.st, "attackSpeedMul") * (1 + t.auraSpeed);
            }
        }

        static float Get(Dictionary<string, float> st, string k) => st.TryGetValue(k, out var v) ? v : 0f;

        static void ApplyMods(Dictionary<string, float> st, List<StatMod> mods)
        {
            foreach (var m in mods)
            {
                float v = Get(st, m.stat);
                if (m.add.HasValue) v += m.add.Value;
                if (m.mul.HasValue) v *= m.mul.Value;
                st[m.stat] = v;
            }
        }

        /// <summary>Werte eines (noch nicht gebauten) Turms auf Stufe 1 für Vorschau und Reichweitenkreis.</summary>
        public float PreviewRange(TowerDef def, int x, int y)
        {
            float r = def.stats.TryGetValue("range", out var v) ? v : 0f;
            if (HeightAt(x, y) >= 2) r += 0.4f;
            return r;
        }

        // ================================================================== Wellen

        public WaveDef WaveFor(int n)
        {
            if (n >= 1 && n <= map.waves.Count) return map.waves[n - 1];
            // Endlos jenseits der exportierten Wellen: letzte 10 Wellen wiederholen, LP nach Webspiel-Kurve
            if (map.waves.Count == 0) return new WaveDef { index = n };
            int cnt = Mathf.Min(10, map.waves.Count);
            var src = map.waves[map.waves.Count - cnt + ((n - 1) % cnt)];
            float hp = n - 1 < data.endlessHp.Count ? data.endlessHp[n - 1] : data.endlessHp[data.endlessHp.Count - 1] * Mathf.Pow(1.03f, n - data.endlessHp.Count);
            var w = new WaveDef { index = n, hpMul = hp, speedMul = src.speedMul, rewardMul = 1 + 0.035f * (n - 1), isBoss = src.isBoss };
            foreach (var g in src.groups) w.groups.Add(new WaveGroup { enemy = g.enemy, boss = g.boss, count = Mathf.Min(60, g.count + (n - src.index) / 10), elite = g.elite, mutation = g.mutation });
            return w;
        }

        public bool CanStartWave => phase == Phase.Build;

        public void StartWave()
        {
            if (phase != Phase.Build) return;
            wave++;
            phase = Phase.Combat;
            waveTime = 0;
            current = WaveFor(wave);
            queue.Clear();
            float t = 1f;
            foreach (var g in current.groups)
            {
                float gap = g.boss != null ? 3f : Spacing.TryGetValue(g.enemy, out var sp) ? sp : 0.8f;
                for (int i = 0; i < g.count; i++)
                {
                    queue.Add(new SpawnEntry { at = t, enemy = g.enemy, boss = g.boss, elite = g.elite, mutation = g.mutation, miniBoss = g.miniBoss });
                    t += g.elite ? 2f : gap;
                }
                t += 2.2f;
            }
            view?.Message(current.isBoss ? $"Welle {wave}: Bosswelle!" : $"Welle {wave} beginnt", current.isBoss ? 2 : 0);
        }

        void CompleteWave()
        {
            bool wasBoss = current != null && current.isBoss;
            int bonus = Mathf.RoundToInt(data.waveBonusBase + data.waveBonusPerWave * wave + (wasBoss ? data.bossWaveBonus : 0));
            int income = 0;
            foreach (var t in towers) if (t.def.attack == "economy") income += Mathf.RoundToInt(t.S("income"));
            int total = bonus + income;
            gold += total;
            current = null;
            projectiles.ForEach(p => view?.ProjectileEnded(p));
            projectiles.Clear();
            view?.Message($"Welle {wave} geschafft: +{total} Gold" + (income > 0 ? $" (davon {income} aus Goldgräbern)" : ""), 0);
            if (!endless && wave >= totalWaves) { phase = Phase.Victory; view?.Message("Sieg! Der Kern hält stand.", 0); return; }
            phase = Phase.Build;
        }

        // ================================================================== Gegner

        Enemy MakeEnemy(string id, bool elite, string bossId, string mutation)
        {
            BossDef b = null;
            if (!string.IsNullOrEmpty(bossId)) data.bosses.TryGetValue(bossId, out b);
            EnemyDef def;
            if (!data.enemies.TryGetValue(b != null ? "boss" : id, out def)) data.enemies.TryGetValue("runner", out def);
            var w = current;
            var e = new Enemy { uid = nextUid++, def = def, boss = b, elite = elite, mutation = mutation, spawnedAt = time };
            if (b != null)
            {
                float bm = BossHpMul(wave);
                e.hp = b.hp * bm * diff.hpMul;
                e.speed = b.speed;
                e.reward = b.reward * (w?.rewardMul ?? 1);
                e.leak = b.leak;
                e.poise = e.maxPoise = b.poise;
                e.shield = e.hp * b.shieldPct;
                e.size = b.size;
            }
            else
            {
                e.hp = def.hp * (w?.hpMul ?? 1) * diff.hpMul * (elite ? data.eliteHpMul : 1);
                e.speed = def.speed * (elite ? 0.9f : 1);
                e.reward = def.reward * (w?.rewardMul ?? 1) * (elite ? data.eliteRewardMul : 1) * diff.rewardMul;
                e.leak = def.leak * (elite ? data.eliteLeakMul : 1);
                e.shield = e.hp * def.shieldPct;
                e.size = def.size * (elite ? data.eliteSize : 1);
            }
            float sm = w?.speedMul ?? 1;
            if (b == null && def.flying) e.speed = Mathf.Min(data.flyerMaxSpeed, e.speed * (1 + (sm - 1) * data.flyerSpeedShare));
            else e.speed *= sm;
            if (mutation == "hasty") e.speed *= 1.35f;
            e.maxHp = e.hp;
            e.maxShield = e.shield;
            e.flying = b == null && def.flying;
            if (b != null && b.weakPoints > 0)
            {
                e.plateMax = e.hp * 0.12f;
                for (int i = 0; i < b.weakPoints; i++) e.plates.Add(e.plateMax);
            }
            var sp = map.spawns.Count > 0 ? map.spawns[b != null ? 0 : (e.uid % map.spawns.Count)] : new Vector2Int(0, 0);
            e.x = sp.x + 0.5f;
            e.y = sp.y + 0.5f;
            e.nx = sp.x;
            e.ny = sp.y;
            e.abilityCd = 2 + Rand() * 3;
            e.remaining = field[Idx(sp.x, sp.y)];
            return e;
        }

        float BossHpMul(int n)
        {
            if (!endless) return 1.5f * (1 + 0.09f * Mathf.Max(0, n - 10));
            int i = Mathf.Clamp(n - 1, 0, data.endlessBossHp.Count - 1);
            return data.endlessBossHp.Count > 0 ? data.endlessBossHp[i] : 1.5f;
        }

        public Enemy Spawn(string id, bool elite, string bossId, string mutation)
        {
            var e = MakeEnemy(id, elite, bossId, mutation);
            enemies.Add(e);
            view?.EnemySpawned(e);
            if (e.boss != null)
            {
                bossEnemy = e;
                bossPhaseName = e.boss.phases.Count > 0 ? e.boss.phases[0].name : "";
                view?.Message($"{e.boss.name} erscheint! {e.boss.counter}", 2);
                if (!string.IsNullOrEmpty(e.boss.partner) && !enemies.Exists(o => o.alive && o.boss != null && o.boss.id == e.boss.partner))
                {
                    var p = MakeEnemy("boss", false, e.boss.partner, null);
                    p.x -= 0.4f;
                    enemies.Add(p);
                    view?.EnemySpawned(p);
                }
            }
            return e;
        }

        Enemy SpawnAt(string id, Enemy at)
        {
            var e = MakeEnemy(id, false, null, null);
            e.x = at.x; e.y = at.y; e.nx = at.nx; e.ny = at.ny; e.remaining = at.remaining;
            enemies.Add(e);
            view?.EnemySpawned(e);
            return e;
        }

        // ================================================================== Schritt

        public void Step()
        {
            if (phase == Phase.Victory || phase == Phase.Defeat) return;
            time += Dt;
            RecomputeStats();
            if (phase == Phase.Combat)
            {
                waveTime += Dt;
                while (queue.Count > 0 && queue[0].at <= waveTime)
                {
                    var q = queue[0];
                    queue.RemoveAt(0);
                    var e = Spawn(q.enemy, q.elite, q.boss, q.mutation);
                    if (q.miniBoss) { e.maxHp *= 3; e.hp = e.maxHp; e.reward *= 4; }
                }
            }
            for (int i = 0; i < enemies.Count; i++) if (enemies[i].alive) UpdateEnemy(enemies[i]);
            for (int i = 0; i < towers.Count; i++) UpdateTower(towers[i]);
            UpdateProjectiles();
            UpdateZones();
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.alive) continue;
                if (bossEnemy == e) bossEnemy = enemies.Find(o => o.alive && o.boss != null);
                enemies.RemoveAt(i);
            }
            if (lives <= 0 && phase != Phase.Defeat)
            {
                lives = 0;
                phase = Phase.Defeat;
                view?.Message("Der Kern ist gefallen.", 2);
                return;
            }
            if (phase == Phase.Combat && queue.Count == 0 && enemies.Count == 0) CompleteWave();
        }

        float SlowFactor(Enemy e)
        {
            if (e.Has("freeze") || e.Has("stun") || e.Has("root") || e.staggerT > 0) return 0f;
            float slow = 0;
            var c = e.Get("chill");
            if (c != null) slow = Mathf.Min(c.flag > 0 ? c.flag / 100f : 0.6f, c.power * c.stacks);
            var p = e.Get("poison");
            if (p != null && p.flag > 0) slow += Mathf.Min(0.3f, p.flag / 100f);
            if (e.IsBoss) slow *= 0.6f;
            return 1f - Mathf.Min(data.chillHardCap, slow);
        }

        void UpdateEnemy(Enemy e)
        {
            // Status: Schaden über Zeit und Ablauf
            for (int i = 0; i < e.statuses.Count && e.alive; i++)
            {
                var s = e.statuses[i];
                if (s.id == "burn") Damage(e, s.power * Dt, "fire", s.src, 0);
                else if (s.id == "poison") Damage(e, s.power * s.stacks * Dt, "poison", s.src, 0);
                else if (s.id == "shred") Damage(e, s.power * s.stacks * Dt, "physical", s.src, 0);
                s.t -= Dt;
            }
            if (!e.alive) return;
            for (int i = e.statuses.Count - 1; i >= 0; i--)
            {
                var s = e.statuses[i];
                if (s.t > 0) continue;
                e.statuses.RemoveAt(i);
                if (s.id == "freeze") e.Add("freezeImmune", data.freezeImmuneDur, 1, null);
            }
            if (e.hitFlash > 0) e.hitFlash -= Dt;
            if (e.staggerT > 0) e.staggerT -= Dt;

            // Regeneration (nur ohne Brand/Gift/Rüstungsbruch)
            if (e.def.regenPct > 0 && !e.Has("burn") && !e.Has("poison") && !e.Has("shred"))
                e.hp = Mathf.Min(e.maxHp, e.hp + e.maxHp * e.def.regenPct * Dt);

            Abilities(e);
            if (e.boss != null) BossPhases(e);

            // Bewegung entlang des Wegfelds
            float step = e.speed * SlowFactor(e) * Dt;
            float bossMul = 1f;
            if (e.boss != null && e.phase < e.boss.phases.Count) bossMul = e.boss.phases[e.phase].speedMul * (e.enraged ? 1.3f : 1f);
            step *= bossMul;
            int guard = 0;
            while (step > 0 && guard++ < 4)
            {
                float tx = e.nx + 0.5f, ty = e.ny + 0.5f;
                float dx = tx - e.x, dy = ty - e.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d <= step)
                {
                    e.x = tx; e.y = ty;
                    step -= d;
                    if (e.nx == map.goal.x && e.ny == map.goal.y) { Leak(e); return; }
                    var n = NextCell(e.nx, e.ny);
                    if (n.x == e.nx && n.y == e.ny) { Leak(e); return; }
                    e.nx = n.x; e.ny = n.y;
                }
                else
                {
                    e.x += dx / d * step;
                    e.y += dy / d * step;
                    step = 0;
                }
            }
            int fi = field[Idx(e.nx, e.ny)];
            e.remaining = (fi < 0 ? 0 : fi) + Vector2.Distance(e.Pos, new Vector2(e.nx + 0.5f, e.ny + 0.5f));
        }

        void Abilities(Enemy e)
        {
            if (e.Has("silence") || e.Has("stun") || e.Has("freeze")) return;
            var abil = e.def.abilities;
            if (abil.Contains("phaseShift"))
            {
                if (e.phaseShift > 0) e.phaseShift -= Dt;
                else
                {
                    e.phaseCd -= Dt;
                    if (e.phaseCd <= 0) { e.phaseShift = 2f; e.phaseCd = 6f; }
                }
            }
            if (abil.Contains("heal"))
            {
                e.abilityCd -= Dt;
                if (e.abilityCd <= 0)
                {
                    e.abilityCd = 4f;
                    foreach (var o in enemies)
                        if (o.alive && o != e && (o.Pos - e.Pos).sqrMagnitude <= 2.25f) o.hp = Mathf.Min(o.maxHp, o.hp + o.maxHp * 0.12f);
                    view?.Burst(e.Pos, 1.5f, new Color(0.4f, 1f, 0.5f));
                }
            }
        }

        void BossPhases(Enemy e)
        {
            var b = e.boss;
            // Phasenwechsel nach LP-Schwelle oder Auslöser „Schild gebrochen“
            for (int p = e.phase + 1; p < b.phases.Count; p++)
            {
                var ph = b.phases[p];
                bool trig = ph.trigger == "shieldBroken" ? (e.maxShield > 0 && e.shield <= 0) || (b.weakPoints > 0 && e.plates.Count == 0) : e.hp / e.maxHp <= ph.hpBelow;
                if (ph.trigger != "shieldBroken" && ph.hpBelow >= 1f) trig = false;
                if (!trig) break;
                e.phase = p;
                bossPhaseName = ph.name;
                view?.Message($"{b.name}: {ph.name}", 2);
                if (ph.abilities.Contains("reShield") && !e.reshieldUsed) { e.reshieldUsed = true; e.shield = e.maxShield * 0.6f; }
            }
            if (e.staggerT > 0) return;
            var abil = e.phase < b.phases.Count ? b.phases[e.phase].abilities : new List<string>();
            e.abilityCd -= Dt;
            if (e.abilityCd > 0) return;
            e.abilityCd = 7f;
            if (abil.Contains("summonBrood"))
            {
                for (int k = 0; k < 3; k++) { var c = SpawnAt(b.id == "maw" ? "broodling" : "runner", e); c.x += (k - 1) * 0.2f; }
                view?.Message($"{b.name} beschwört Brut!", 1);
            }
            if (abil.Contains("riftJump"))
            {
                for (int k = 0; k < 2; k++)
                {
                    var n = NextCell(e.nx, e.ny);
                    if (n.x == map.goal.x && n.y == map.goal.y) break;
                    e.nx = n.x; e.ny = n.y;
                }
                e.x = e.nx + 0.5f; e.y = e.ny + 0.5f;
                view?.Burst(e.Pos, 1.2f, new Color(0.6f, 0.3f, 1f));
            }
            if (abil.Contains("twinBond"))
            {
                foreach (var o in enemies)
                    if (o != e && o.alive && o.boss != null && o.boss.id == b.partner && (o.Pos - e.Pos).sqrMagnitude < 4f)
                    {
                        e.hp = Mathf.Min(e.maxHp, e.hp + e.maxHp * 0.03f);
                        o.hp = Mathf.Min(o.maxHp, o.hp + o.maxHp * 0.03f);
                    }
            }
            if (abil.Contains("devour"))
            {
                // Der Schlund lähmt den nächsten Turm am Weg für kurze Zeit
                Tower best = null; float bd = 2.5f;
                foreach (var t in towers) { float d = Vector2.Distance(t.Center, e.Pos); if (d < bd) { bd = d; best = t; } }
                if (best != null) { best.cd = Mathf.Max(best.cd, 5f); view?.Message($"{b.name} lähmt {best.def.name}!", 1); }
            }
        }

        void Leak(Enemy e)
        {
            if (!e.alive) return;
            e.alive = false;
            e.leaked = true;
            int l = Mathf.Max(1, Mathf.RoundToInt(e.leak));
            lives -= l;
            leaks++;
            view?.EnemyDied(e, true);
            view?.Message($"{e.Name} hat den Kern erreicht (−{l} Leben)", 1);
        }

        // ================================================================== Schaden

        float Resist(Enemy e, string type)
        {
            var dict = e.boss != null ? e.boss.resist : e.def.resist;
            float r = dict.TryGetValue(type, out var v) ? v : 0f;
            if (e.elite) r += data.eliteResist;
            if (e.mutation == "armored") r += 0.25f;
            return r;
        }

        public float Damage(Enemy e, float amount, string type, Tower src, float armorPierce)
        {
            if (!e.alive || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return 0;
            if (string.IsNullOrEmpty(type)) type = "physical";
            float resist = Resist(e, type);
            if (type == "physical" && resist > 0) resist *= 1 - Mathf.Min(1, armorPierce);
            var mark = e.Get("mark");
            if (mark != null && resist > 0) resist = Mathf.Max(0, resist - Mathf.Min(data.markHardCap, mark.power));
            if (type == "physical")
            {
                var sh = e.Get("shred");
                if (sh != null && sh.flag > 0 && resist > -0.2f) resist = Mathf.Max(-0.2f, resist - sh.flag / 1000f * sh.stacks);
            }
            resist = Mathf.Clamp(resist, -1f, 0.9f);
            float dmg = amount * (1 - resist);
            if (type == "physical" && e.Has("brittle")) dmg *= 1 + data.brittleBonus;
            var teth = e.Get("tether");
            if (teth != null) dmg *= 1 + teth.power;
            if (e.phaseShift > 0 && type == "physical") return 0;
            float dealt = 0;
            if (e.plates.Count > 0)
            {
                float toPlate = Mathf.Min(e.plates[0], dmg);
                e.plates[0] -= dmg;
                dealt += toPlate;
                dmg *= 0.1f;
                if (e.plates[0] <= 0)
                {
                    e.plates.RemoveAt(0);
                    view?.Message(e.plates.Count > 0 ? $"Schwachpunkt zerstört! Noch {e.plates.Count}." : "Alle Schwachpunkte zerstört – der Kern liegt frei!", 0);
                }
            }
            if (e.shield > 0)
            {
                float mul;
                if (e.boss != null && e.boss.id == "nullWarden") mul = NullShieldMul.TryGetValue(type, out var nm) ? nm : 0.5f;
                else
                {
                    e.adapt.TryGetValue(type, out int ad);
                    mul = 1 - 0.15f * ad;
                    e.adaptHits.TryGetValue(type, out int ah);
                    ah++;
                    if (ah >= 6 && ad < 3) { ah = 0; e.adapt[type] = ad + 1; }
                    e.adaptHits[type] = ah;
                }
                float eff = dmg * mul;
                float absorbed = Mathf.Min(e.shield, eff);
                e.shield -= absorbed;
                dealt += absorbed;
                dmg = mul > 0 ? (eff - absorbed) / mul : 0;
                if (e.shield <= 0) e.shield = 0;
            }
            if (dmg > 0)
            {
                dealt += Mathf.Min(e.hp, dmg);
                e.hp -= dmg;
            }
            e.hitFlash = 0.12f;
            if (src != null) src.damageDealt += dealt;
            if (e.hp <= 0) Kill(e, src);
            return dealt;
        }

        void Kill(Enemy e, Tower src)
        {
            if (e.rewarded) return;
            e.rewarded = true;
            e.alive = false;
            e.hp = 0;
            int g = Mathf.Max(1, Mathf.RoundToInt(e.reward));
            // Kopfgeldbüro (Goldgräber-Spezialisierung)
            foreach (var t in towers)
            {
                if (t.def.attack != "economy" || t.S("killBounty") <= 0) continue;
                if (Vector2.Distance(t.Center, e.Pos) <= t.S("bountyRange")) g += Mathf.RoundToInt(t.S("killBounty") + (e.elite || e.IsBoss ? t.S("eliteBounty") : 0));
            }
            gold += g;
            kills++;
            if (src != null) src.kills++;
            view?.EnemyDied(e, false);
            view?.FloatText(e.Pos, "+" + g, new Color(1f, 0.85f, 0.3f));
            if (!string.IsNullOrEmpty(e.def.deathSpawn) && e.boss == null)
                for (int k = 0; k < e.def.deathSpawnCount; k++) { var c = SpawnAt(e.def.deathSpawn, e); c.x += (k - e.def.deathSpawnCount * 0.5f) * 0.15f; }
            if (e.boss != null) view?.Message($"{e.boss.name} besiegt! +{g} Gold", 0);
            // Zwillinge: fällt einer, rast der andere
            if (e.boss != null && !string.IsNullOrEmpty(e.boss.partner))
                foreach (var o in enemies) if (o.alive && o.boss != null && o.boss.id == e.boss.partner) o.enraged = true;
            // Schwelbrand: Brand springt beim Tod über
            var burn = e.Get("burn");
            if (burn != null && burn.src != null && burn.src.S("burnSpread") > 0)
                foreach (var o in enemies)
                    if (o.alive && (o.Pos - e.Pos).sqrMagnitude <= 1.44f) o.Add("burn", burn.t + 1, burn.power, burn.src);
        }

        bool ReactReady(Enemy e, string id)
        {
            if (e.reactCd.TryGetValue(id, out var t) && t > time) return false;
            e.reactCd[id] = time + 1.5f;
            return true;
        }

        float React(Enemy e, string type, float dmg, Tower src)
        {
            if (type == "fire")
            {
                if (e.Has("chill") && ReactReady(e, "steam")) { e.Remove("chill"); e.Add("wet", 5, 1, src); dmg *= 1.4f; view?.FloatText(e.Pos, "Dampfstoß", Color.white); }
                var p = e.Get("poison");
                if (p != null && ReactReady(e, "toxicBlaze"))
                {
                    e.Remove("poison");
                    float burst = p.power * p.stacks * p.t;
                    foreach (var o in enemies.ToArray()) if (o.alive && (o.Pos - e.Pos).sqrMagnitude <= 1.44f) Damage(o, o == e ? burst : burst * 0.6f, "poison", p.src, 0);
                    view?.Burst(e.Pos, 1.2f, new Color(0.6f, 1f, 0.2f));
                }
            }
            else if (type == "frost")
            {
                var b = e.Get("burn");
                if (b != null && ReactReady(e, "thermalShock")) { e.Remove("burn"); Damage(e, b.power * b.t, "frost", src, 0); e.Add("brittle", 3, data.brittleBonus, src); }
            }
            else if (type == "lightning")
            {
                if (e.Has("wet") && ReactReady(e, "overload")) dmg *= 1.5f;
            }
            else if (type == "sonic")
            {
                if (e.Has("freeze") && ReactReady(e, "shatter")) { e.Remove("freeze"); e.Add("freezeImmune", data.freezeImmuneDur, 1, null); dmg = dmg * 2f + e.maxHp * 0.06f; view?.FloatText(e.Pos, "Resonanzbruch", Color.cyan); }
            }
            return dmg;
        }

        public void Interrupt(Enemy e, float power)
        {
            if (!e.alive || power <= 0) return;
            if (e.boss != null)
            {
                e.poise -= power;
                if (e.poise <= 0) { e.poise = e.maxPoise; e.staggerT = 1.2f; e.abilityCd = Mathf.Max(e.abilityCd, 3f); view?.FloatText(e.Pos, "Taumelt!", Color.yellow); }
            }
            else
            {
                e.abilityCd = Mathf.Max(e.abilityCd, 2f);
                if (e.phaseShift > 0) e.phaseShift = 0;
            }
        }

        float TowerDamage(Tower t) => t.S("damage") * t.S("damageMul");

        /// <summary>Vollständiger Treffer inkl. Kritisch, Reaktionen und Statuseffekten (applyHit im Webspiel).</summary>
        void Hit(Enemy e, Tower t, float mult)
        {
            if (!e.alive) return;
            float dmg = TowerDamage(t) * mult;
            if (e.flying && t.S("airDmgMul") != 1f) dmg *= t.S("airDmgMul");
            if (t.S("critChance") > 0 && Rand() < t.S("critChance")) dmg *= t.S("critMult");
            if (t.S("maxHpPct") > 0) dmg += e.maxHp * t.S("maxHpPct") * (e.IsBoss ? 0.25f : 1f);
            if (t.S("rampPct") > 0)
            {
                if (t.rampTarget == e) t.rampHits = Mathf.Min(t.rampHits + 1, Mathf.RoundToInt(t.S("rampMax")));
                else { t.rampTarget = e; t.rampHits = 0; }
                dmg *= 1 + t.S("rampPct") * t.rampHits;
            }
            string type = t.def.damageType ?? "physical";
            if (dmg > 0)
            {
                dmg = React(e, type, dmg, t);
                Damage(e, dmg, type, t, t.S("armorPierce"));
            }
            if (!e.alive) return;
            OnHitEffects(e, t);
        }

        void OnHitEffects(Enemy e, Tower t)
        {
            bool boss = e.IsBoss;
            if (t.S("burnDps") > 0 && t.S("burnDur") > 0) e.Add("burn", t.S("burnDur"), t.S("burnDps") * t.S("damageMul"), t);
            if (t.S("chillPct") > 0 && t.S("chillDur") > 0)
            {
                var s = e.Add("chill", t.S("chillDur"), t.S("chillPct"), t, 3);
                s.flag = Mathf.Max(s.flag, Mathf.RoundToInt(t.S("chillMax") * 100));
            }
            if (t.S("freezeChance") > 0 && Rand() < t.S("freezeChance"))
            {
                if (boss) Interrupt(e, 15);
                else if (!e.Has("freezeImmune")) e.Add("freeze", t.S("freezeDur"), 1, t);
            }
            if (t.S("poisonDps") > 0 && t.S("poisonDur") > 0)
            {
                var s = e.Add("poison", t.S("poisonDur"), t.S("poisonDps") * t.S("damageMul"), t, Mathf.Max(1, Mathf.RoundToInt(t.S("poisonMaxStacks"))));
                if (t.S("poisonSlow") > 0) s.flag = Mathf.Max(s.flag, Mathf.RoundToInt(t.S("poisonSlow") * 100));
            }
            if (t.S("bleedDps") > 0 && t.S("bleedDur") > 0)
            {
                var s = e.Add("shred", t.S("bleedDur"), t.S("bleedDps"), t, Mathf.Max(1, Mathf.RoundToInt(t.S("bleedMaxStacks"))));
                s.flag = Mathf.Max(s.flag, Mathf.RoundToInt(t.S("shredPct") * 1000));
            }
            if (t.S("markPct") > 0 && t.S("markDur") > 0)
            {
                e.Add("mark", t.S("markDur"), t.S("markPct"), t);
                int spread = Mathf.RoundToInt(t.S("markSpread"));
                if (spread > 0)
                    foreach (var o in enemies)
                    {
                        if (spread <= 0) break;
                        if (!o.alive || o == e || (o.Pos - e.Pos).sqrMagnitude > 2.25f) continue;
                        o.Add("mark", t.S("markDur") * 0.75f, t.S("markPct"), t);
                        spread--;
                    }
            }
            if (t.S("interrupt") > 0) Interrupt(e, t.S("interrupt"));
            if (t.S("silenceDur") > 0 && !boss) e.Add("silence", t.S("silenceDur"), 1, t);
            if (t.S("stunDur") > 0) { if (boss) Interrupt(e, 8); else e.Add("stun", t.S("stunDur"), 1, t); }
            if (t.S("brittleDur") > 0) e.Add("brittle", t.S("brittleDur"), data.brittleBonus, t);
            if (t.S("rootDur") > 0 && !boss) e.Add("root", t.S("rootDur"), 1, t);
            if (t.S("tetherPct") > 0) e.Add("tether", 3f, boss ? 0.15f : t.S("tetherPct"), t);
            if (t.S("knockback") > 0 && !boss) PushBack(e, Mathf.RoundToInt(t.S("knockback")));
        }

        /// <summary>Gegner zurück auf dem Weg versetzen (Rissbinder, Schockwelle, Gravitation).</summary>
        void PushBack(Enemy e, int cells)
        {
            if (cells <= 0 || e.IsBoss) return;
            int cx = Mathf.FloorToInt(e.x), cy = Mathf.FloorToInt(e.y);
            if (!InBounds(cx, cy)) return;
            for (int s = 0; s < cells; s++)
            {
                int cur = field[Idx(cx, cy)];
                int bx = cx, by = cy, bv = cur;
                for (int k = 0; k < 4; k++)
                {
                    int ax = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ay = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                    if (!InBounds(ax, ay)) continue;
                    int v = field[Idx(ax, ay)];
                    if (v > bv) { bv = v; bx = ax; by = ay; }
                }
                if (bx == cx && by == cy) break;
                cx = bx; cy = by;
            }
            e.x = cx + 0.5f; e.y = cy + 0.5f;
            var n = NextCell(cx, cy);
            e.nx = n.x; e.ny = n.y;
        }

        // ================================================================== Türme

        bool CanHit(Tower t, Enemy e)
        {
            if (!e.alive) return false;
            if (e.flying) return t.S("hitsAir") > 0;
            return t.S("hitsGround") > 0;
        }

        float Range(Tower t) => t.S("range") * t.S("rangeMul");

        List<Enemy> InRange(Tower t, float range)
        {
            var r = new List<Enemy>();
            var c = t.Center;
            float r2 = (range + 0.15f) * (range + 0.15f);
            foreach (var e in enemies) if (CanHit(t, e) && (e.Pos - c).sqrMagnitude <= r2) r.Add(e);
            return r;
        }

        Enemy PickTarget(Tower t, List<Enemy> list)
        {
            Enemy best = null;
            float bv = float.MaxValue;
            bool preferAir = t.S("preferAir") > 0 && list.Exists(e => e.flying);
            foreach (var e in list)
            {
                if (preferAir && !e.flying) continue;
                float v;
                switch (t.priority)
                {
                    case "strong": v = -(e.hp + e.shield); break;
                    case "last": v = -e.remaining; break;
                    case "close": v = (e.Pos - t.Center).sqrMagnitude; break;
                    default: v = e.remaining; break;
                }
                if (v < bv) { bv = v; best = e; }
            }
            return best;
        }

        List<Enemy> PickTargets(Tower t, List<Enemy> list, int n)
        {
            var r = new List<Enemy>();
            var pool = new List<Enemy>(list);
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                var e = PickTarget(t, pool);
                if (e == null) break;
                r.Add(e);
                pool.Remove(e);
            }
            return r;
        }

        void UpdateTower(Tower t)
        {
            string a = t.def.attack;
            if (a == "economy" || a == "aura" || a == "none") return;
            float rate = t.S("attackSpeedMul");
            t.cd -= Dt * (rate > 0 ? rate : 1f);
            float range = Range(t);

            if (a == "laser")
            {
                var list = InRange(t, range);
                if (t.laserTarget == null || !t.laserTarget.alive || !list.Contains(t.laserTarget)) { t.laserTarget = PickTarget(t, list); t.laserRamp = 0; }
                if (t.laserTarget == null) { t.tview?.ClearTarget(); return; }
                t.laserRamp = Mathf.Min(t.S("laserRamp"), t.laserRamp + Dt);
                int beams = Mathf.Max(1, Mathf.RoundToInt(t.S("multishot")));
                var targets = beams > 1 ? PickTargets(t, list, beams) : new List<Enemy> { t.laserTarget };
                foreach (var e in targets)
                {
                    // Laser: kontinuierlicher Strahl, Schaden je Sekunde = damage/cooldown, steigt bis laserRamp
                    float m = (1 + t.laserRamp) * Dt / Mathf.Max(0.05f, t.S("cooldown"));
                    Hit(e, t, m);
                    if (Mathf.Repeat(time, 0.1f) < Dt) view?.Beam(t.Center, 1.2f, e.Pos, t.def.color, 0.06f, 0.11f);
                }
                t.tview?.SetTarget(ViewPos(t.laserTarget));
                return;
            }

            if (a == "pylon")
            {
                if (t.cd > 0) return;
                var list = InRange(t, range);
                var e = PickTarget(t, list);
                if (e == null) return;
                t.cd = 1f;
                float zap = t.S("zapDamage") + t.S("damage");
                Damage(e, zap * t.S("damageMul"), "lightning", t, 0);
                view?.Beam(t.Center, 1.3f, e.Pos, t.def.color, 0.05f, 0.15f);
                return;
            }

            if (a == "tesla")
            {
                var list = InRange(t, range);
                var e = PickTarget(t, list);
                if (e == null) { t.tview?.ClearTarget(); return; }
                t.tview?.SetTarget(ViewPos(e));
                if (t.S("arcDps") > 0)
                {
                    Damage(e, t.S("arcDps") * t.S("damageMul") * Dt, "lightning", t, 0);
                    if (Mathf.Repeat(time, 0.15f) < Dt) view?.Beam(t.Center, 1.3f, e.Pos, t.def.color, 0.04f, 0.12f);
                }
                if (t.cd > 0) return;
                t.cd = t.S("cooldown");
                Chain(t, e, Mathf.RoundToInt(t.S("chains")));
                t.tview?.Fire();
                return;
            }

            if (t.cd > 0) return;

            if (a == "mines")
            {
                int max = Mathf.RoundToInt(t.S("mineMax"));
                if (t.minesOut >= max) return;
                // Mine auf eine zufällige Wegzelle in Reichweite legen
                var cells = new List<Vector2Int>();
                int r = Mathf.CeilToInt(range);
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = t.cx + dx, y = t.cy + dy;
                        if (!InBounds(x, y) || !Cell.Walkable(map.cells[Idx(x, y)])) continue;
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), t.Center) > range) continue;
                        cells.Add(new Vector2Int(x, y));
                    }
                if (cells.Count == 0) return;
                int shots = Mathf.Max(1, Mathf.RoundToInt(t.S("multishot")));
                for (int k = 0; k < shots && t.minesOut < max; k++)
                {
                    var c = cells[rng.Next(cells.Count)];
                    var m = new Mine { tower = t, pos = new Vector2(c.x + 0.2f + Rand() * 0.6f, c.y + 0.2f + Rand() * 0.6f) };
                    mines.Add(m);
                    t.minesOut++;
                    view?.MineChanged(m, false);
                }
                t.cd = t.S("cooldown");
                t.tview?.Fire();
                return;
            }

            var inRange = InRange(t, range);
            if (inRange.Count == 0) { t.tview?.ClearTarget(); return; }

            switch (a)
            {
                case "pulse":
                case "gravity":
                {
                    t.cd = t.S("cooldown");
                    foreach (var e in inRange)
                    {
                        Hit(e, t, 1f);
                        if (a == "gravity" && e.alive && !e.IsBoss) PushBack(e, Mathf.RoundToInt(t.S("pullCells")));
                    }
                    view?.Burst(t.Center, range, t.def.color);
                    t.tview?.Fire();
                    break;
                }
                case "chain":
                {
                    var e = PickTarget(t, inRange);
                    t.tview?.SetTarget(ViewPos(e));
                    t.cd = t.S("cooldown");
                    Chain(t, e, Mathf.RoundToInt(t.S("chains")));
                    t.tview?.Fire();
                    break;
                }
                case "beam":
                {
                    var e = PickTarget(t, inRange);
                    t.tview?.SetTarget(ViewPos(e));
                    t.cd = t.S("cooldown");
                    Hit(e, t, 1f);
                    view?.Beam(t.Center, 1.3f, e.Pos, t.def.color, 0.08f, 0.25f);
                    t.tview?.Fire();
                    break;
                }
                case "rift":
                {
                    t.cd = t.S("cooldown");
                    var targets = PickTargets(t, inRange, Mathf.Max(1, Mathf.RoundToInt(t.S("riftTargets"))));
                    foreach (var e in targets)
                    {
                        Hit(e, t, 1f);
                        if (e.alive) PushBack(e, Mathf.RoundToInt(t.S("riftBack")));
                        if (e.alive && e.IsBoss) Interrupt(e, 20);
                        view?.Beam(t.Center, 1.3f, e.Pos, t.def.color, 0.1f, 0.3f);
                        view?.Burst(e.Pos, 0.6f, t.def.color);
                    }
                    t.tview?.Fire();
                    break;
                }
                case "meteor":
                {
                    t.cd = t.S("cooldown");
                    int count = Mathf.Max(1, Mathf.RoundToInt(t.S("meteorCount")));
                    var targets = PickTargets(t, inRange, count);
                    for (int k = 0; k < count; k++)
                    {
                        var e = targets[k % targets.Count];
                        var pos = e.Pos + (k >= targets.Count ? Random.insideUnitCircle * 0.6f : Vector2.zero);
                        float d = t.S("meteorDelay");
                        var m = new Meteor { tower = t, pos = pos, t = d, total = d };
                        meteors.Add(m);
                        view?.MeteorChanged(m, false);
                    }
                    t.tview?.Fire();
                    break;
                }
                default: // projectile, dragon
                {
                    int shots = Mathf.Max(1, Mathf.RoundToInt(t.S("multishot")));
                    var targets = shots > 1 ? PickTargets(t, inRange, shots) : new List<Enemy> { PickTarget(t, inRange) };
                    if (targets.Count == 0 || targets[0] == null) return;
                    t.tview?.SetTarget(ViewPos(targets[0]));
                    t.cd = t.S("cooldown");
                    foreach (var e in targets)
                    {
                        var p = new Projectile
                        {
                            tower = t, target = e, pos = t.Center, z = 1.1f, aim = e.Pos,
                            speed = Mathf.Max(4f, t.S("projectileSpeed")), pierceLeft = Mathf.RoundToInt(t.S("pierce")),
                        };
                        if (p.pierceLeft > 0) p.hit = new HashSet<Enemy>();
                        projectiles.Add(p);
                        view?.ProjectileSpawned(p);
                    }
                    t.tview?.Fire();
                    view?.Shot(t, targets[0].Pos);
                    break;
                }
            }
        }

        Vector3 ViewPos(Enemy e) => e.view != null ? e.view.transform.position : new Vector3(e.x, 0, e.y);

        void Chain(Tower t, Enemy first, int chains)
        {
            if (first == null) return;
            var hit = new HashSet<Enemy>();
            var cur = first;
            Vector2 from = t.Center;
            float fromZ = 1.3f;
            float mult = 1f;
            float falloff = t.S("chainFalloff");
            float cr = t.S("chainRange");
            if (cur.Has("wet")) chains++;
            for (int k = 0; k <= chains && cur != null; k++)
            {
                hit.Add(cur);
                view?.Beam(from, fromZ, cur.Pos, t.def.color, 0.05f, 0.18f);
                Hit(cur, t, mult);
                from = cur.Pos;
                fromZ = 0.5f;
                mult *= falloff;
                Enemy next = null;
                float bd = cr * cr;
                foreach (var o in enemies)
                {
                    if (!CanHit(t, o) || hit.Contains(o)) continue;
                    float d = (o.Pos - from).sqrMagnitude;
                    if (d <= bd) { bd = d; next = o; }
                }
                cur = next;
            }
        }

        void UpdateProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var p = projectiles[i];
                if (p.target != null && p.target.alive) p.aim = p.target.Pos;
                var d = p.aim - p.pos;
                float step = p.speed * Dt;
                float dist = d.magnitude;
                p.z = Mathf.MoveTowards(p.z, p.target != null && p.target.flying ? 0.9f : 0.35f, step * 0.5f);
                if (dist > step)
                {
                    p.pos += d / dist * step;
                    // Durchschlag: Gegner auf der Flugbahn treffen
                    if (p.pierceLeft > 0)
                        foreach (var e in enemies.ToArray())
                        {
                            if (!CanHit(p.tower, e) || p.hit.Contains(e) || e == p.target) continue;
                            if ((e.Pos - p.pos).sqrMagnitude > 0.16f) continue;
                            p.hit.Add(e);
                            Hit(e, p.tower, 1f);
                            if (--p.pierceLeft <= 0) break;
                        }
                    continue;
                }
                p.pos = p.aim;
                Impact(p);
                projectiles.RemoveAt(i);
                view?.ProjectileEnded(p);
            }
        }

        void Impact(Projectile p)
        {
            var t = p.tower;
            float splash = t.S("splash");
            if (splash > 0)
            {
                foreach (var e in enemies.ToArray())
                {
                    if (!CanHit(t, e)) continue;
                    float d = Vector2.Distance(e.Pos, p.pos);
                    if (d > splash) continue;
                    Hit(e, t, e == p.target ? 1f : 0.7f);
                }
                view?.Burst(p.pos, splash, t.def.color);
            }
            else if (p.target != null && p.target.alive) Hit(p.target, t, 1f);
            if (t.S("cloudDur") > 0)
            {
                var c = new Cloud { tower = t, pos = p.pos, r = Mathf.Max(0.8f, t.S("cloudRadius")), t = t.S("cloudDur") };
                clouds.Add(c);
                view?.CloudChanged(c, false);
            }
        }

        void UpdateZones()
        {
            for (int i = mines.Count - 1; i >= 0; i--)
            {
                var m = mines[i];
                if (m.arm > 0) { m.arm -= Dt; continue; }
                Enemy trig = null;
                foreach (var e in enemies) if (e.alive && !e.flying && (e.Pos - m.pos).sqrMagnitude <= 0.2f) { trig = e; break; }
                if (trig == null) continue;
                float r = Mathf.Max(0.6f, m.tower.S("splash"));
                foreach (var e in enemies.ToArray()) if (e.alive && !e.flying && Vector2.Distance(e.Pos, m.pos) <= r) Hit(e, m.tower, 1f);
                if (m.tower.S("cloudDur") > 0) { var c = new Cloud { tower = m.tower, pos = m.pos, r = Mathf.Max(0.8f, m.tower.S("cloudRadius")), t = m.tower.S("cloudDur") }; clouds.Add(c); view?.CloudChanged(c, false); }
                view?.Burst(m.pos, r, m.tower.def.color);
                m.dead = true;
                m.tower.minesOut = Mathf.Max(0, m.tower.minesOut - 1);
                mines.RemoveAt(i);
                view?.MineChanged(m, true);
            }
            for (int i = clouds.Count - 1; i >= 0; i--)
            {
                var c = clouds[i];
                c.t -= Dt;
                if (Mathf.Repeat(time, 0.5f) < Dt)
                    foreach (var e in enemies)
                        if (e.alive && !e.flying && Vector2.Distance(e.Pos, c.pos) <= c.r)
                        {
                            var s = e.Add("poison", c.tower.S("poisonDur"), c.tower.S("poisonDps"), c.tower, Mathf.Max(1, Mathf.RoundToInt(c.tower.S("poisonMaxStacks"))));
                            if (c.tower.S("poisonSlow") > 0) s.flag = Mathf.Max(s.flag, Mathf.RoundToInt(c.tower.S("poisonSlow") * 100));
                        }
                if (c.t <= 0) { clouds.RemoveAt(i); view?.CloudChanged(c, true); }
            }
            for (int i = meteors.Count - 1; i >= 0; i--)
            {
                var m = meteors[i];
                m.t -= Dt;
                if (m.t > 0) continue;
                var t = m.tower;
                float r = Mathf.Max(0.6f, t.S("splash"));
                foreach (var e in enemies.ToArray()) if (CanHit(t, e) && Vector2.Distance(e.Pos, m.pos) <= r) Hit(e, t, 1f);
                int frags = Mathf.RoundToInt(t.S("fragments"));
                for (int k = 0; k < frags; k++)
                {
                    var fp = m.pos + Random.insideUnitCircle * r * 1.4f;
                    foreach (var e in enemies.ToArray()) if (CanHit(t, e) && Vector2.Distance(e.Pos, fp) <= r * 0.5f) Hit(e, t, 0.35f);
                    view?.Burst(fp, r * 0.5f, t.def.color);
                }
                view?.Burst(m.pos, r, t.def.color);
                meteors.RemoveAt(i);
                view?.MeteorChanged(m, true);
            }
        }

        public int RemainingInWave => queue.Count + enemies.Count;
    }
}
