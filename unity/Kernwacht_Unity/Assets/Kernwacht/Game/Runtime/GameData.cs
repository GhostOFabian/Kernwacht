using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Game
{
    // Datenmodell der exportierten Spieldaten (Resources/KernwachtData/*.json, erzeugt mit web/scripts/export_unity.ts).

    public class StatMod { public string stat; public float? add; public float? mul; }

    public class UpgradeDef
    {
        public string id, name, description;
        public int cost;
        public List<StatMod> mods = new List<StatMod>();
    }

    public class TowerDef
    {
        public string id, name, role, description, icon, damageType, attack, priority, synergies, counters;
        public int cost, size = 1;
        public bool legendary, laneOnly, mazingOnly, detector;
        public Color color;
        public Dictionary<string, float> stats = new Dictionary<string, float>();
        public List<UpgradeDef> upgrades = new List<UpgradeDef>();
        public List<UpgradeDef> specs = new List<UpgradeDef>();
    }

    public class FusionDef { public string id, a, b; public int cost; }

    public class EnemyDef
    {
        public string id, name, role, description, counter, route;
        public float hp, speed, reward, threat, leak, size, shieldPct, regenPct;
        public bool flying, stealth;
        public int minWave;
        public Dictionary<string, float> resist = new Dictionary<string, float>();
        public List<string> abilities = new List<string>();
        public string deathSpawn; public int deathSpawnCount;
        public Color color;
    }

    public class BossPhase { public float hpBelow, speedMul; public string name, trigger; public List<string> abilities = new List<string>(); }

    public class BossDef
    {
        public string id, name, title, description, counter, partner;
        public float hp, speed, poise, reward, leak, shieldPct, size;
        public int weakPoints;
        public Color color;
        public Dictionary<string, float> resist = new Dictionary<string, float>();
        public List<BossPhase> phases = new List<BossPhase>();
    }

    public class WaveGroup { public string enemy, boss, mutation; public int count; public bool elite, miniBoss; }

    public class WaveDef
    {
        public int index;
        public float hpMul = 1, speedMul = 1, rewardMul = 1;
        public bool isBoss;
        public List<WaveGroup> groups = new List<WaveGroup>();
    }

    public class MapDef
    {
        public string id, name, seed, biome, biomeName;
        public int width, height, waveCount, startGold, lives;
        public int[] cells, heights;
        public List<Vector2Int> spawns = new List<Vector2Int>();
        public Vector2Int goal;
        public List<int> decor = new List<int>();
        public HashSet<int> veins = new HashSet<int>();
        public HashSet<int> ley = new HashSet<int>();
        public Dictionary<int, string> bosses = new Dictionary<int, string>();
        public List<WaveDef> waves = new List<WaveDef>();
        public Dictionary<string, Color> palette = new Dictionary<string, Color>();
        public Color Pal(string k, Color def) => palette.TryGetValue(k, out var c) ? c : def;
    }

    public class DifficultyDef { public string id, name; public float hpMul, goldMul, rewardMul; public int lives; }

    /// <summary>Zellcodes wie im Webspiel.</summary>
    public static class Cell
    {
        public const int Open = 0, Path = 1, Blocked = 2, Spawn = 3, Goal = 4, NoBuild = 5;
        public static bool Walkable(int c) => c == Path || c == Spawn || c == Goal || c == NoBuild;
    }

    /// <summary>Alle Spieldaten, einmal aus den JSON-Dateien geladen.</summary>
    public class GameData
    {
        public readonly Dictionary<string, TowerDef> towers = new Dictionary<string, TowerDef>();
        public readonly List<TowerDef> towerOrder = new List<TowerDef>();
        public readonly List<FusionDef> fusions = new List<FusionDef>();
        public readonly Dictionary<string, float> zeroStats = new Dictionary<string, float>();
        public readonly Dictionary<string, EnemyDef> enemies = new Dictionary<string, EnemyDef>();
        public readonly Dictionary<string, BossDef> bosses = new Dictionary<string, BossDef>();
        public readonly List<MapDef> maps = new List<MapDef>();
        public readonly Dictionary<string, DifficultyDef> difficulty = new Dictionary<string, DifficultyDef>();
        public readonly List<float> endlessHp = new List<float>();
        public readonly List<float> endlessBossHp = new List<float>();
        public float sellRefund = 0.7f, fusionRange = 2.3f;
        public float waveBonusBase = 30, waveBonusPerWave = 5, bossWaveBonus = 40, interestRate = 0.05f, interestCap = 40;
        public float brittleBonus = 0.3f, markHardCap = 0.6f, chillHardCap = 0.75f, freezeImmuneDur = 3f;
        public float eliteHpMul = 3, eliteRewardMul = 4, eliteLeakMul = 3, eliteResist = 0.1f, eliteSize = 1.35f;
        public float flyerMaxSpeed = 2.2f, flyerSpeedShare = 0.5f;

        static GameData _instance;
        public static GameData Instance => _instance ?? (_instance = Load());

        static Dictionary<string, object> LoadJson(string name)
        {
            var ta = Resources.Load<TextAsset>("KernwachtData/" + name);
            if (ta == null) { Debug.LogError("[Kernwacht] Spieldaten fehlen: Resources/KernwachtData/" + name + ".json"); return new Dictionary<string, object>(); }
            return Json.Parse(ta.text) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        public static Color Hex(object v, Color def)
        {
            if (!(v is double d)) return def;
            int c = (int)d;
            return new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f, 1f);
        }

        static Dictionary<string, float> NumMap(Dictionary<string, object> d)
        {
            var r = new Dictionary<string, float>();
            if (d == null) return r;
            foreach (var kv in d) if (kv.Value is double x) r[kv.Key] = (float)x;
            return r;
        }

        static List<string> StrList(List<object> l)
        {
            var r = new List<string>();
            if (l == null) return r;
            foreach (var o in l) if (o is string s) r.Add(s);
            return r;
        }

        static UpgradeDef ParseUpgrade(Dictionary<string, object> u)
        {
            var r = new UpgradeDef { id = u.Str("id"), name = u.Str("name"), description = u.Str("description"), cost = u.Int("cost") };
            var mods = u.Arr("mods");
            if (mods != null)
                foreach (var mo in mods)
                {
                    var m = mo as Dictionary<string, object>;
                    if (m == null) continue;
                    var sm = new StatMod { stat = m.Str("stat") };
                    if (m.ContainsKey("add")) sm.add = m.Num("add");
                    if (m.ContainsKey("mul")) sm.mul = m.Num("mul");
                    r.mods.Add(sm);
                }
            return r;
        }

        static GameData Load()
        {
            var g = new GameData();
            // ---------------- Türme
            var tj = LoadJson("towers");
            foreach (var kv in NumMap(tj.Obj("zeroStats"))) g.zeroStats[kv.Key] = kv.Value;
            g.sellRefund = tj.Num("sellRefund", 0.7f);
            g.fusionRange = tj.Num("fusionRange", 2.3f);
            var tl = tj.Arr("towers");
            if (tl != null)
                foreach (var o in tl)
                {
                    var t = o as Dictionary<string, object>;
                    if (t == null) continue;
                    var d = new TowerDef
                    {
                        id = t.Str("id"), name = t.Str("name"), role = t.Str("role"), description = t.Str("description"),
                        icon = t.Str("icon"), damageType = t.Str("damageType", null), attack = t.Str("attack"),
                        priority = t.Str("defaultPriority", "first"), synergies = t.Str("synergies"), counters = t.Str("counters"),
                        cost = t.Int("cost"), size = Mathf.Max(1, t.Int("size", 1)),
                        legendary = t.Bool("legendary"), laneOnly = t.Bool("laneOnly"), mazingOnly = t.Bool("mazingOnly"),
                        detector = t.Bool("detector"),
                        color = Hex(t.Get("color"), Color.gray),
                        stats = NumMap(t.Obj("base")),
                    };
                    var ups = t.Arr("upgrades");
                    if (ups != null) foreach (var u in ups) if (u is Dictionary<string, object> ud) d.upgrades.Add(ParseUpgrade(ud));
                    var sps = t.Arr("specs");
                    if (sps != null) foreach (var u in sps) if (u is Dictionary<string, object> ud) d.specs.Add(ParseUpgrade(ud));
                    g.towers[d.id] = d;
                    g.towerOrder.Add(d);
                }
            var fl = tj.Arr("fusions");
            if (fl != null)
                foreach (var o in fl)
                    if (o is Dictionary<string, object> f)
                        g.fusions.Add(new FusionDef { id = f.Str("id"), a = f.Str("a"), b = f.Str("b"), cost = f.Int("cost") });

            // ---------------- Gegner und Bosse
            var ej = LoadJson("enemies");
            var el = ej.Arr("enemies");
            if (el != null)
                foreach (var o in el)
                {
                    var e = o as Dictionary<string, object>;
                    if (e == null) continue;
                    var d = new EnemyDef
                    {
                        id = e.Str("id"), name = e.Str("name"), role = e.Str("role"), description = e.Str("description"),
                        counter = e.Str("counter"), route = e.Str("route", "normal"),
                        hp = e.Num("hp"), speed = e.Num("speed", 1), reward = e.Num("reward"), threat = e.Num("threat", 1),
                        leak = e.Num("leak", 1), size = e.Num("size", 0.35f), shieldPct = e.Num("shieldPct"), regenPct = e.Num("regenPct"),
                        flying = e.Bool("flying"), stealth = e.Bool("stealth"), minWave = e.Int("minWave"),
                        resist = NumMap(e.Obj("resist")), abilities = StrList(e.Arr("abilities")),
                        color = Hex(e.Get("color"), Color.gray),
                    };
                    var ds = e.Obj("onDeathSpawn");
                    if (ds != null) { d.deathSpawn = ds.Str("enemy"); d.deathSpawnCount = ds.Int("count"); }
                    g.enemies[d.id] = d;
                }
            var bl = ej.Arr("bosses");
            if (bl != null)
                foreach (var o in bl)
                {
                    var b = o as Dictionary<string, object>;
                    if (b == null) continue;
                    var d = new BossDef
                    {
                        id = b.Str("id"), name = b.Str("name"), title = b.Str("title"), description = b.Str("description"),
                        counter = b.Str("counter"), partner = b.Str("partner", null),
                        hp = b.Num("hp"), speed = b.Num("speed", 0.5f), poise = b.Num("poise"), reward = b.Num("reward"),
                        leak = b.Num("leak", 10), shieldPct = b.Num("shieldPct"), size = b.Num("size", 1), weakPoints = b.Int("weakPoints"),
                        resist = NumMap(b.Obj("resist")),
                        color = Hex(b.Get("color"), Color.red),
                    };
                    var pl = b.Arr("phases");
                    if (pl != null)
                        foreach (var po in pl)
                            if (po is Dictionary<string, object> p)
                                d.phases.Add(new BossPhase { hpBelow = p.Num("hpBelow", 1), speedMul = p.Num("speedMul", 1), name = p.Str("name"), trigger = p.Str("trigger", null), abilities = StrList(p.Arr("abilities")) });
                    g.bosses[d.id] = d;
                }
            var elite = ej.Obj("elite");
            if (elite != null)
            {
                g.eliteHpMul = elite.Num("hpMul", 3); g.eliteRewardMul = elite.Num("rewardMul", 4); g.eliteLeakMul = elite.Num("leakMul", 3);
                g.eliteResist = elite.Num("resistBonus", 0.1f); g.eliteSize = elite.Num("sizeMul", 1.35f);
            }

            // ---------------- Balance
            var bj = LoadJson("balance");
            var diff = bj.Obj("difficulty");
            if (diff != null)
                foreach (var kv in diff)
                    if (kv.Value is Dictionary<string, object> dd)
                        g.difficulty[kv.Key] = new DifficultyDef { id = kv.Key, name = dd.Str("name"), hpMul = dd.Num("hpMul", 1), goldMul = dd.Num("goldMul", 1), rewardMul = dd.Num("rewardMul", 1), lives = dd.Int("lives", 20) };
            var eco = bj.Obj("economy");
            if (eco != null)
            {
                g.waveBonusBase = eco.Num("waveBonusBase", 30); g.waveBonusPerWave = eco.Num("waveBonusPerWave", 5);
                g.bossWaveBonus = eco.Num("bossWaveBonus", 40); g.interestRate = eco.Num("interestRate", 0.05f); g.interestCap = eco.Num("interestCap", 40);
            }
            var sr = bj.Obj("statusRules");
            if (sr != null)
            {
                g.brittleBonus = sr.Num("brittleBonus", 0.3f); g.markHardCap = sr.Num("markHardCap", 0.6f);
                g.chillHardCap = sr.Num("chillHardCap", 0.75f); g.freezeImmuneDur = sr.Num("freezeImmuneDur", 3);
            }
            var fly = bj.Obj("flyer");
            if (fly != null) { g.flyerMaxSpeed = fly.Num("maxSpeed", 2.2f); g.flyerSpeedShare = fly.Num("speedMulShare", 0.5f); }
            var curve = bj.Arr("endlessCurve");
            if (curve != null)
                foreach (var o in curve)
                    if (o is Dictionary<string, object> c) { g.endlessHp.Add(c.Num("hpMul", 1)); g.endlessBossHp.Add(c.Num("bossHpMul", 1)); }

            // ---------------- Karten mit Wellen
            var mj = LoadJson("maps");
            var ml = mj.Arr("maps");
            if (ml != null)
                foreach (var o in ml)
                {
                    var m = o as Dictionary<string, object>;
                    if (m == null) continue;
                    var d = new MapDef
                    {
                        id = m.Str("id"), name = m.Str("name"), seed = m.Str("seed"), biome = m.Str("biome"), biomeName = m.Str("biomeName"),
                        width = m.Int("width"), height = m.Int("height"), waveCount = m.Int("waveCount", 20),
                        startGold = m.Int("startGold", 260), lives = m.Int("lives", 20),
                    };
                    d.cells = IntArray(m.Arr("cells"), d.width * d.height);
                    d.heights = IntArray(m.Arr("heights"), d.width * d.height);
                    var sp = m.Arr("spawns");
                    if (sp != null) foreach (var so in sp) if (so is Dictionary<string, object> s) d.spawns.Add(new Vector2Int(s.Int("x"), s.Int("y")));
                    var goal = m.Obj("goal");
                    d.goal = new Vector2Int(goal.Int("x"), goal.Int("y"));
                    var dec = m.Arr("decor");
                    if (dec != null) foreach (var x in dec) d.decor.Add(Mathf.RoundToInt(Json.AsFloat(x)));
                    var vn = m.Arr("veins");
                    if (vn != null) foreach (var x in vn) d.veins.Add(Mathf.RoundToInt(Json.AsFloat(x)));
                    var ly = m.Arr("ley");
                    if (ly != null) foreach (var x in ly) d.ley.Add(Mathf.RoundToInt(Json.AsFloat(x)));
                    var bo = m.Obj("bosses");
                    if (bo != null) foreach (var kv in bo) if (int.TryParse(kv.Key, out int wn) && kv.Value is string bs) d.bosses[wn] = bs;
                    var pal = m.Obj("palette");
                    if (pal != null) foreach (var kv in pal) d.palette[kv.Key] = Hex(kv.Value, Color.gray);
                    var wl = m.Arr("waves");
                    if (wl != null)
                        foreach (var wo in wl)
                        {
                            var w = wo as Dictionary<string, object>;
                            if (w == null) continue;
                            var wd = new WaveDef { index = w.Int("index"), hpMul = w.Num("hpMul", 1), speedMul = w.Num("speedMul", 1), rewardMul = w.Num("rewardMul", 1), isBoss = w.Bool("isBoss") };
                            var gl = w.Arr("groups");
                            if (gl != null)
                                foreach (var go in gl)
                                    if (go is Dictionary<string, object> gg)
                                        wd.groups.Add(new WaveGroup { enemy = gg.Str("enemy"), boss = gg.Str("boss", null), mutation = gg.Str("mutation", null), count = gg.Int("count", 1), elite = gg.Bool("elite"), miniBoss = gg.Bool("miniBoss") });
                            d.waves.Add(wd);
                        }
                    g.maps.Add(d);
                }
            Debug.Log($"[Kernwacht] Spieldaten geladen: {g.towers.Count} Türme, {g.enemies.Count} Gegner, {g.bosses.Count} Bosse, {g.maps.Count} Karten.");
            return g;
        }

        static int[] IntArray(List<object> l, int n)
        {
            var r = new int[n];
            if (l == null) return r;
            for (int i = 0; i < n && i < l.Count; i++) r[i] = Mathf.RoundToInt(Json.AsFloat(l[i]));
            return r;
        }

        public float Zero(string stat) => zeroStats.TryGetValue(stat, out var v) ? v : 0f;
    }
}
