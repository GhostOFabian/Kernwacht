using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Kernwacht.Sim.EditorTools
{
    /// <summary>
    /// Abgleich der C#-Simulation mit dem Webspiel (TypeScript): gleiche Zufallswerte, gleiche Karte, gleiche Wellen,
    /// gleicher Partieverlauf. Gegenstück: web/scripts/simDigest.ts. Ergebnis: Konsole und Logs/sim_selftest.txt.
    /// </summary>
    public static class SimSelfTest
    {
        static string R2(double x) => U.Fmt(U.Round(x * 100) / 100);

        [MenuItem("Kernwacht/Test · Simulation abgleichen", priority = 90)]
        public static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("== Zufall ==");
                sb.AppendLine($"hashString(KERNWACHT)={U.Fmt(RngUtil.hashString("KERNWACHT"))} (soll 772350587)");
                sb.AppendLine($"hashSeed(123,layout)={U.Fmt(RngUtil.hashSeed(123, "layout"))} (soll 814437203)");
                var r = new Rng(42);
                var v = new List<string>();
                for (int i = 0; i < 5; i++) v.Add(U.Fmt(r.next()));
                sb.AppendLine("Rng(42).next x5=" + string.Join(", ", v) + " (soll 0.6011037519201636, 0.44829055899754167, 0.8524657934904099, 0.6697340414393693, 0.17481389874592423)");

                sb.AppendLine("== Karte ==");
                var map = WorldGenerator.generateWorld(new GenerateOptions { seed = WorldGenerator.makeSeed("KERNWACHT"), variant = "classic", biome = "forest", size = "normal" });
                double cs = 0, hs = 0;
                for (int i = 0; i < map.cells.Count; i++) { cs += map.cells[i] * (i + 1); hs += map.heights[i] * (i + 1); }
                sb.AppendLine($"{U.Fmt(map.width)}x{U.Fmt(map.height)} spawn {U.Fmt(map.spawn.x)},{U.Fmt(map.spawn.y)} goal {U.Fmt(map.goal.x)},{U.Fmt(map.goal.y)} cells={U.Fmt(cs)} heights={U.Fmt(hs)} decor={map.decor.Count} event={map.terrainEvent} name={map.name}");
                sb.AppendLine("(soll 22x14 spawn 21,9 goal 0,8 cells=18259 heights=38047 decor=60 event=sporeBloom)");

                sb.AppendLine("== Partie (Lyra, Bot) ==");
                sb.Append(Digest("lyra", 12));
                sb.AppendLine("== brann 30 ==");
                sb.Append(Digest("brann", 30));
                sb.AppendLine("---");
                sb.Append(Digest("vela", 30));
            }
            catch (Exception e)
            {
                sb.AppendLine("AUSNAHME: " + e);
            }
            var text = sb.ToString();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/sim_selftest.txt", text);
            Debug.Log("[Kernwacht] Simulationsabgleich:\n" + text);
        }

        public static string Digest(string champion, int maxWave)
        {
            var sim = GameSimulation.create(new RunSetup
            {
                mode = "heroEndless", seedText = "KERNWACHT", variant = "classic", biome = "forest", difficulty = "normal", size = "normal",
                heroEndless = new HeroEndlessSetup { champion = champion, startKit = new List<string>(), meta = new HeroEndlessSetupMeta { bestWave = 0 }, runId = "digest" },
            });
            var bot = new Bot(sim, Bot.BOT_PROFILES[0]);
            var o = new StringBuilder();
            var m = sim.s.map;
            o.AppendLine($"map {U.Fmt(m.width)}x{U.Fmt(m.height)} spawn {U.Fmt(m.spawn.x)},{U.Fmt(m.spawn.y)} goal {U.Fmt(m.goal.x)},{U.Fmt(m.goal.y)} shop {U.Fmt(sim.s.he.shop.x)},{U.Fmt(sim.s.he.shop.y)} gold {U.Fmt(sim.s.gold)}");
            double lastWave = 0;
            int ticks = 0;
            while (!sim.isOver() && ticks < 30 * 60 * 40)
            {
                if (sim.s.phase == "choice") sim.chooseModifier(sim.s.modOffer != null && sim.s.modOffer.Count > 0 ? sim.s.modOffer[0] : null);
                if (sim.s.phase == "build") { bot.tactics(); bot.spend(); }
                sim.step();
                ticks++;
                if (sim.s.phase == "combat" && sim.s.tick % 90 == 0) bot.spend();
                if (sim.s.phase == "combat" && sim.s.tick % 15 == 0) bot.tactics();
                if (sim.s.tick % 10 == 0) Brain(sim);
                if (sim.lastError != null) { o.AppendLine("FEHLER " + sim.lastError.Split('\n')[0]); break; }
                if (sim.s.stats.wavesCleared != lastWave)
                {
                    lastWave = sim.s.stats.wavesCleared;
                    var he = sim.s.he;
                    var c = he.champion;
                    var res = new List<string>();
                    foreach (var kv in he.resources) res.Add($"{kv.Key}={U.Fmt(kv.Value)}");
                    o.AppendLine($"W{U.Fmt(lastWave)} tick={U.Fmt(sim.s.tick)} gold={U.Fmt(sim.s.gold)} lives={U.Fmt(sim.s.lives)} kills={U.Fmt(sim.s.stats.kills)} towers={sim.s.towers.Count} lvl={U.Fmt(c.level)} xp={R2(c.xp)} hp={R2(c.hp)} pos={R2(c.x)},{R2(c.y)} pickups={he.pickups.Count} inv={c.inventory.Count} res={string.Join(",", res)}");
                    if (lastWave >= maxWave) break;
                }
            }
            return o.ToString();
        }

        static void Brain(GameSimulation sim)
        {
            var he = sim.s.he;
            var c = he.champion;
            if (c.dead > 0) return;
            var st = ChampionSystem.championStats(he);
            if (c.hp < st.maxHp * 0.4) sim.usePotion("heal");
            var def = Champions.CHAMPIONS[c.id];
            foreach (var n in def.tree) if (c.skillPoints > 0) sim.learnSkill(n.id);
            if (def.skills[0].target == "self") sim.championSkill(0, c.x, c.y);
            if (c.path.Count == 0)
            {
                Pickup best = null;
                double bd = 9;
                foreach (var p in he.pickups)
                {
                    double d = U.Hypot(p.x - c.x, p.y - c.y);
                    if (d < bd) { bd = d; best = p; }
                }
                if (best != null) sim.moveChampion(Math.Floor(best.x), Math.Floor(best.y));
            }
            foreach (var i in c.inventory.ToArray()) if (!c.equipment.Contains(i.uid)) sim.equip(i.uid);
        }
    }
}
