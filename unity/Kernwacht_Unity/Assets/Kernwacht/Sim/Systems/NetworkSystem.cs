// Übersetzung von sim/NetworkSystem.ts.
// Turmnetzwerke. Neuaufbau nur bei Dirty-Flag (Bau, Verkauf, Upgrade, Störung) – nie pro Frame.
// Regeln:
//  * Nur Leitpylonen erzeugen Verbindungen (zu Türmen und anderen Pylonen in linkRange).
//  * Pylone haben eine begrenzte Anzahl Verbindungen (maxLinks), Türme max. TOWER_MAX_PYLON_LINKS.
//  * Gestörte Türme/Pylone (Saboteur, Nullpuls) sind ausgeschlossen.
//  * Bonus eines Netzes = stärkster Pylon (nicht additiv) → keine unkontrollierte Stapelung.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class NetworkSystem
    {
        /// <summary>JS <c>x ** 2</c> (V8/fdlibm: Exponent 2 liefert exakt x*x).</summary>
        private static double Sq(double x) { return x * x; }

        public static void rebuildNetwork(GameSimulation sim)
        {
            var s = sim.s;
            var c = sim.c;
            c.linked.Clear();
            c.resonanceByTower.Clear();
            c.networkBonusByTower.Clear();
            var active = s.towers.Where((t) => Towers.TOWERS[t.def].networkable && t.disrupted <= 0).ToList();
            var pylons = U.StableSort(active.Where((t) => t.def == "pylon").ToList(), (a, b) => a.uid - b.uid);
            var links = new List<NetworkLink>();
            var used = new Dictionary<double, double>();
            var has = new HashSet<string>();
            var pylonStats = new Dictionary<double, Dictionary<string, double>>();
            foreach (var p in pylons) pylonStats[p.uid] = TowerSystem.computeTowerStats(sim, p, false);
            foreach (var p in pylons)
            {
                if (!pylonStats.TryGetValue(p.uid, out var ps) || ps == null) continue;
                double r2 = ps["linkRange"] * ps["linkRange"];
                var cands = U.StableSort(
                    active.Where((t) => t != p && Sq(t.cx - p.cx) + Sq(t.cy - p.cy) <= r2).ToList(),
                    (a, b) => U.Or((Sq(a.cx - p.cx) + Sq(a.cy - p.cy)) - (Sq(b.cx - p.cx) + Sq(b.cy - p.cy)), a.uid - b.uid));
                foreach (var t in cands)
                {
                    if ((U.GetN(used, p.uid) ?? 0) >= U.Round(ps["maxLinks"])) break;
                    string key = p.uid < t.uid ? $"{U.Fmt(p.uid)}-{U.Fmt(t.uid)}" : $"{U.Fmt(t.uid)}-{U.Fmt(p.uid)}";
                    if (has.Contains(key)) continue;
                    double cap;
                    if (t.def == "pylon")
                    {
                        double? ml = pylonStats.TryGetValue(t.uid, out var tps) && tps != null ? tps["maxLinks"] : (double?)null;
                        cap = U.Round(ml ?? 0);
                    }
                    else cap = Elements.TOWER_MAX_PYLON_LINKS;
                    if ((U.GetN(used, t.uid) ?? 0) >= cap) continue;
                    has.Add(key);
                    links.Add(new NetworkLink { a = p.uid, b = t.uid });
                    used[p.uid] = (U.GetN(used, p.uid) ?? 0) + 1;
                    used[t.uid] = (U.GetN(used, t.uid) ?? 0) + 1;
                }
            }
            // Union-Find
            var parent = new Dictionary<double, double>();
            double find(double x)
            {
                double r = x;
                while (parent[r] != r) r = parent[r];
                double y = x;
                while (parent[y] != r)
                {
                    double n = parent[y];
                    parent[y] = r;
                    y = n;
                }
                return r;
            }
            foreach (var l in links)
            {
                if (!parent.ContainsKey(l.a)) parent[l.a] = l.a;
                if (!parent.ContainsKey(l.b)) parent[l.b] = l.b;
                double ra = find(l.a);
                double rb = find(l.b);
                if (ra != rb) parent[U.Max(ra, rb)] = U.Min(ra, rb);
            }
            // PORT: Dictionary ohne Löschen hält die Einfügereihenfolge (wie JS Map).
            var groupsByRoot = new Dictionary<double, List<double>>();
            foreach (var uid in U.StableSort(parent.Keys.ToList(), (a, b) => a - b))
            {
                double r = find(uid);
                if (!groupsByRoot.TryGetValue(r, out var g) || g == null) g = new List<double>();
                g.Add(uid);
                groupsByRoot[r] = g;
            }
            var groups = new List<NetworkGroup>();
            foreach (var members in groupsByRoot.Values)
            {
                var towers = members
                    .Select((u) => c.towerByUid.TryGetValue(u, out var tw) ? tw : null)
                    .Where((t) => t != null)
                    .ToList();
                double bonus = 0;
                foreach (var t in towers)
                {
                    if (t.def == "pylon") bonus = U.Max(bonus, pylonStats.TryGetValue(t.uid, out var bps) && bps != null ? bps["networkBonus"] : 0);
                }
                bonus *= c.mods.networkBonusMul;
                var defs = new HashSet<string>(towers.Select((t) => t.def));
                var res = Elements.RESONANCES.Where((r) => r.requires.All((d) => defs.Contains(d))).Select((r) => r.id).ToList();
                double gi = groups.Count;
                groups.Add(new NetworkGroup { members = members, bonus = bonus, resonances = res });
                foreach (var t in towers)
                {
                    c.linked[t.uid] = gi;
                    c.networkBonusByTower[t.uid] = bonus;
                    if (res.Count > 0) c.resonanceByTower[t.uid] = res;
                }
                if (members.Count > s.stats.maxNetworkSize) s.stats.maxNetworkSize = members.Count;
            }
            s.network = new NetworkState { links = links, groups = groups, version = s.network.version + 1 };
        }

        /// <summary>Modifikator "Netzentladung": Verbindungslinien schaden kreuzenden Gegnern.</summary>
        public static void updateNetworkDischarge(GameSimulation sim, double dt)
        {
            var s = sim.s;
            if (!sim.c.mods.netDischarge || s.network.links.Count == 0) return;
            s.netZapTimer -= dt;
            if (s.netZapTimer > 0) return;
            s.netZapTimer = 1;
            // PORT: Index-Schleifen über erfasste Listen = JS for…of.
            var links = s.network.links;
            for (int li = 0; li < links.Count; li++)
            {
                var l = links[li];
                sim.c.towerByUid.TryGetValue(l.a, out var a);
                sim.c.towerByUid.TryGetValue(l.b, out var b);
                if (a == null || b == null) continue;
                double dmg = U.Or(sim.stats(a)["zapDamage"], 8);
                double ax = a.cx + 0.5;
                double ay = a.cy + 0.5;
                double bx = b.cx + 0.5;
                double by = b.cy + 0.5;
                double vx = bx - ax;
                double vy = by - ay;
                double len2 = U.Or(vx * vx + vy * vy, 1);
                var enemies = s.enemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (!e.alive) continue;
                    double t = U.Max(0, U.Min(1, ((e.x - ax) * vx + (e.y - ay) * vy) / len2));
                    double px = ax + vx * t;
                    double py = ay + vy * t;
                    if (Sq(e.x - px) + Sq(e.y - py) <= 0.45 * 0.45)
                    {
                        DamageSystem.dealDamage(sim, e, dmg, "lightning", a.uid);
                        sim.emit(new GameEvent { k = "hit", type = "lightning", x = e.x, y = e.y, amount = dmg, splash = 0 });
                    }
                }
            }
        }
    }
}
