// Übersetzung von sim/MiningSystem.ts.
// Helden-Endlos: Bäume, Felsen und andere Hindernisse abbauen. Der Abbau dauert so lange wie eine
// reguläre Welle, ist nicht unterbrechbar (der Held steht still, kann nicht fallen) und bringt
// zufällig Gold und Ressourcen. Danach ist das Feld frei und bebaubar.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: Rückgabetyp von mineState <c>{ ok: boolean; reason: string | null }</c>.</summary>
    public class MineStateResult
    {
        public bool ok;
        public string reason;
    }

    /// <summary>TS: <c>CommandResult &amp; { walk?: boolean }</c>.</summary>
    // MineResult = MineResult (in GameSimulation.cs definiert)

    public static partial class MiningSystem
    {
        /// <summary>Nicht abbaubare Hinderniskinder: Lava (3) und Wolken (7).</summary>
        private static readonly HashSet<double> UNMINABLE = new HashSet<double> { 3, 7 };

        public static double? decorKindAt(GameSimulation sim, double idx)
        {
            List<double> d = sim.s.map.decor;
            for (int k = 0; k + 2 < d.Count; k += 3) if (d[k] == idx) return d[k + 1];
            return null;
        }

        /// <summary>Abbauzeit: mittlere bisherige Wellendauer (mindestens/höchstens begrenzt), sonst Standardwert.</summary>
        public static double mineDuration(GameSimulation sim)
        {
            GameState s = sim.s;
            double avg = s.stats.wavesCleared > 0 ? s.stats.duration / s.stats.wavesCleared : HeroEndless.HERO_ENDLESS.mineTimeDefault;
            return U.Round(U.Min(HeroEndless.HERO_ENDLESS.mineTimeMax, U.Max(HeroEndless.HERO_ENDLESS.mineTimeMin, avg)));
        }

        public static MineStateResult mineState(GameSimulation sim, double cx, double cy)
        {
            GameState s = sim.s;
            HeroEndlessState he = s.he;
            if (he == null) return new MineStateResult { ok = false, reason = "Nur im Helden-Endlosmodus" };
            MapDefinition m = s.map;
            if (cx < 0 || cy < 0 || cx >= m.width || cy >= m.height) return new MineStateResult { ok = false, reason = "Außerhalb der Karte" };
            double idx = cy * m.width + cx;
            if (m.cells[(int)idx] != Cell.Blocked) return new MineStateResult { ok = false, reason = "Hier gibt es nichts abzubauen" };
            if (he.shop.x == cx && he.shop.y == cy) return new MineStateResult { ok = false, reason = "Der Laden kann nicht abgebaut werden" };
            double? kind = decorKindAt(sim, idx);
            if (kind == null || UNMINABLE.Contains(kind.Value)) return new MineStateResult { ok = false, reason = "Dieses Hindernis lässt sich nicht abbauen" };
            if (he.mining != null) return new MineStateResult { ok = false, reason = "Der Held baut bereits ab" };
            if (he.champion.dead > 0) return new MineStateResult { ok = false, reason = "Held ist besiegt" };
            return new MineStateResult { ok = true, reason = null };
        }

        private static bool adjacent(GameSimulation sim, double idx)
        {
            ChampionState c = sim.s.he?.champion;
            if (c == null) return false;
            double w = sim.s.map.width;
            double x = idx % w;
            double y = Math.Floor(idx / w);
            return U.Max(Math.Abs(c.x - (x + 0.5)), Math.Abs(c.y - (y + 0.5))) <= 1.55;
        }

        /// <summary>Startet den Abbau (sofort, wenn der Held danebensteht) oder merkt das Ziel vor.</summary>
        public static MineResult orderMine(GameSimulation sim, double cx, double cy)
        {
            MineStateResult chk = mineState(sim, cx, cy);
            if (!chk.ok) return new MineResult { ok = false, reason = chk.reason ?? "Nicht möglich" };
            HeroEndlessState he = sim.s.he;
            if (he == null) return new MineResult { ok = false, reason = "Kein Held" };
            double idx = cy * sim.s.map.width + cx;
            if (adjacent(sim, idx))
            {
                startMining(sim, idx);
                return new MineResult { ok = true };
            }
            he.mineTarget = idx;
            return new MineResult { ok = true, walk = true };
        }

        private static void startMining(GameSimulation sim, double idx)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            double total = mineDuration(sim);
            he.mining = new MiningState { cell = idx, t = total, total = total };
            he.mineTarget = -1;
            ChampionState c = he.champion;
            c.path = new List<double>();
            c.channel = 0;
            double w = sim.s.map.width;
            c.facing = Math.Atan2(Math.Floor(idx / w) + 0.5 - c.y, (idx % w) + 0.5 - c.x);
            sim.emit(new GameEvent { k = "mineStart", x = (idx % w) + 0.5, y = Math.Floor(idx / w) + 0.5, total = total });
        }

        /// <summary>Takt: vorgemerktes Ziel beim Eintreffen beginnen, laufenden Abbau fortsetzen und abschließen.</summary>
        public static void updateMining(GameSimulation sim, double dt)
        {
            HeroEndlessState he = sim.s.he;
            if (he == null) return;
            ChampionState c = he.champion;
            if (he.mining == null && he.mineTarget.HasValue && he.mineTarget.Value >= 0 && c.path.Count == 0 && c.dead <= 0)
            {
                double idx = he.mineTarget.Value;
                double w = sim.s.map.width;
                if (adjacent(sim, idx) && mineState(sim, idx % w, Math.Floor(idx / w)).ok) startMining(sim, idx);
                else he.mineTarget = -1;
            }
            MiningState m = he.mining;
            if (m == null) return;
            m.t -= dt;
            c.anim = U.Max(c.anim, 0.3);
            if (m.t > 0) return;
            finishMining(sim, m.cell);
        }

        private static void finishMining(GameSimulation sim, double idx)
        {
            GameState s = sim.s;
            HeroEndlessState he = s.he;
            if (he == null) return;
            MapDefinition map = s.map;
            double? kind = decorKindAt(sim, idx);
            map.cells[(int)idx] = Cell.Open;
            List<double> dec = new List<double>();
            for (int k = 0; k + 2 < map.decor.Count; k += 3) if (map.decor[k] != idx) { dec.Add(map.decor[k]); dec.Add(map.decor[k + 1]); dec.Add(map.decor[k + 2]); }
            map.decor = dec;
            map.rev = (map.rev ?? 0) + 1;
            sim.c.fieldDirty = true;
            sim.c.statsDirty = true;
            he.mining = null;
            he.mined = (he.mined ?? 0) + 1;
            // Zufällige Ausbeute, wächst mit der Welle
            double wave = U.Max(1, s.wave);
            Func<double> r = () => LootSystem.lootRand(sim);
            double gold = U.Round((HeroEndless.HERO_ENDLESS.mineGoldBase + HeroEndless.HERO_ENDLESS.mineGoldPerWave * wave) * (0.5 + r()));
            s.gold += gold;
            s.stats.goldEarned += gold;
            List<string> parts = new List<string> { $"+{U.Fmt(gold)} Gold" };
            Action<string, double> give = (res, n) =>
            {
                if (n <= 0) return;
                LootSystem.addResource(he, res, n);
                parts.Add($"+{U.Fmt(n)} {HeroEndless.RESOURCES[res].icon}");
            };
            double scale = 1 + wave / 12;
            // Kristallgruppen bringen eher Kristall, Bäume/Pilze eher Metall (Holz wird als Baumaterial verbucht)
            double crystalBias = kind == 2 || kind == 4 ? 0.35 : 0;
            if (r() < 0.75) give("metal", U.Round((2 + r() * 5) * scale));
            if (r() < 0.35 + crystalBias) give("crystal", U.Round((1 + r() * 3) * scale));
            if (r() < 0.15) give("essence", 1 + Math.Floor(r() * 2 * scale));
            if (r() < 0.04) give("relic", 1);
            double x = (idx % map.width) + 0.5;
            double y = Math.Floor(idx / map.width) + 0.5;
            sim.emit(new GameEvent { k = "mined", x = x, y = y, label = string.Join(" ", parts) });
            sim.emit(new GameEvent { k = "message", text = $"Abgebaut ({Biomes.BIOMES[map.biome].name}): {string.Join(", ", parts)} – das Feld ist jetzt frei.", severity = "info" });
        }

        public static bool isMining(GameSimulation sim)
        {
            return sim.s.he?.mining != null;
        }
    }
}
