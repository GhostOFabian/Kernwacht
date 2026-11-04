// Übersetzung von ui/screens/results.ts – Ergebnisbildschirm nach einer Partie.

using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    /// <summary>TS: export interface ResultParams { state; record } (ScreenParams.results).</summary>
    public class ResultParams
    {
        public GameState state;
        public RunRecord record;
    }

    public static class ResultsScreen
    {
        static string duration(double sec)
        {
            double m = Math.Floor(sec / 60);
            double s = Math.Floor(sec % 60);
            return $"{U.Fmt(m)}:{U.PadStart(U.Fmt(s), 2, "0")} min";
        }

        public static RectTransform renderResults(GameApp app, ResultParams p)
        {
            var s = p.state;
            var r = p.record;
            var st = s.stats;
            bool win = s.phase == "victory";
            var sb = Score.scoreBreakdown(s);
            var dmg = U.StableSort(st.damageByTower.ToList(), (a, b) => b.Value - a.Value);
            double maxDmg = Math.Max(1, dmg.Count > 0 ? dmg.Max((kv) => kv.Value) : 0);
            double maxGold = Math.Max(1, st.goldPerWave.Count > 0 ? st.goldPerWave.Max() : 0);
            var mission = s.missionId != null ? Campaign.CAMPAIGN.FirstOrDefault((m) => m.id == s.missionId) : null;
            var next = r.unlockedMission != null ? Campaign.CAMPAIGN.FirstOrDefault((m) => m.id == r.unlockedMission) : null;
            var he = s.he;
            var hr = r.hero;
            void again()
            {
                app.startRun(new RunSetup
                {
                    heroEndless = he != null ? app.heroEndlessSetup(he.champion.id, app.save.progress.heroMeta.startKit) : null,
                    mode = s.mode, seedText = s.seed.text, variant = s.map.variant, biome = s.map.biome, difficulty = s.difficulty,
                    missionId = s.missionId, customMap = s.mode == "custom" ? s.map : null, size = s.map.size ?? "normal", heroes = s.heroes.Select((x) => x.id).ToList(),
                });
            }
            string title = win ? "Sieg!" : he != null ? "Lauf beendet" : "Niederlage";
            var content = new List<Action<Transform>>();

            // .result-banner
            content.Add((t) =>
            {
                var banner = Ui.Panel(t, "ResultBanner", win ? new Color(1f, 216 / 255f, 74 / 255f, 0.2f) : new Color(1f, 90 / 255f, 90 / 255f, 0.18f), true, Theme.Round);
                banner.GetComponent<Outline>().effectColor = win ? new Color(1f, 216 / 255f, 74 / 255f, 0.5f) : new Color(1f, 120 / 255f, 120 / 255f, 0.4f);
                var h = banner.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(18, 18, 12, 12);
                h.spacing = 18;
                h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = true; h.childControlHeight = true;
                h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                Common.image(banner.transform, Icons.Get(win ? "trophy" : "skull"), 64, win ? Theme.Gold : Theme.Hex(0xff8080));
                var col = Ui.Column(banner.transform, 4);
                Ui.Size(col, -1, -1, 1);
                Common.label(col.transform, win ? (mission != null ? $"Mission „{mission.name}“ geschafft" : "Der Kern hält stand!") : "Der Kern ist gefallen", Common.H2, Theme.Text, FontStyle.Bold);
                Common.para(col.transform, $"{TextUtil.modeName(s.mode)} · {s.map.name} · {U.Fmt(st.wavesCleared)} Wellen überstanden");
                if (U.Truthy(r.stars))
                {
                    var stars = Ui.Row(col.transform, 4, TextAnchor.MiddleLeft, false);
                    for (int i = 0; i < 3; i++) Common.image(stars.transform, Icons.Get("star"), 34, i < r.stars ? Theme.Gold : new Color(1, 1, 1, 0.22f));
                }
            });
            if (r.campaignFinished) content.Add((t) => Common.notice(t, Campaign.CAMPAIGN_EPILOGUE, "good"));
            if (next != null) content.Add((t) => Common.notice(t, $"Neue Mission freigeschaltet: {next.name}", "good"));
            if (r.newBestScore) content.Add((t) => Common.notice(t, "Neuer persönlicher Bestwert für diesen Modus!", "good"));
            if (r.newBestEndless) content.Add((t) => Common.notice(t, "Neue beste Endloswelle!", "good"));
            foreach (var id in r.unlockedHeroes ?? new List<string>())
            {
                string hid = id;
                content.Add((t) => Common.notice(t, $"Neuer Held freigeschaltet: {Heroes.HEROES[hid].name} – {Heroes.HEROES[hid].title}", "good"));
            }
            if ((r.newAchievements ?? new List<string>()).Count > 0)
                content.Add((t) => Common.notice(t, "<b>Erfolge errungen: </b>" + string.Join(" · ", r.newAchievements.Select((id) =>
                {
                    var a = Achievements.ACHIEVEMENTS.FirstOrDefault((x) => x.id == id);
                    return a != null ? $"{a.icon} {a.name}" : id;
                })), "good"));
            if (he != null)
            {
                content.Add((t) =>
                {
                    var c = Common.card(t, "HeroResult");
                    Common.h3(c, $"{Champions.CHAMPIONS[he.champion.id].name} · Stufe {U.Fmt(he.champion.level)}");
                    Common.para(c, $"Welle {U.Fmt(st.wavesCleared)} erreicht{(hr != null && hr.newBestWave ? " – neue Bestwelle!" : "")} · {U.Fmt(he.champion.kills)} Abschüsse durch den Helden · {TextUtil.fmt(he.champion.damageDealt)} Schaden");
                    Common.para(c, $"EP gesammelt {TextUtil.fmt(he.stats.xpCollected)} · Items gefunden {U.Fmt(he.stats.itemsFound)} · hergestellt {U.Fmt(he.stats.itemsCrafted)} · Tränke genutzt {U.Fmt(he.stats.potionsUsed)} · Ressourcen {TextUtil.fmt(he.stats.resourcesEarned)}", "small");
                    if (hr != null && hr.newItems.Count > 0)
                        Common.notice(c, "<b>Dauerhaft freigeschaltet: </b>" + string.Join(" · ", hr.newItems.Select((i) => $"{(Items.ITEMS.TryGetValue(i, out var it) ? it.icon : "")} {(Items.ITEMS.TryGetValue(i, out var it2) ? it2.name : i)}")), "good");
                    if (hr != null && hr.newPerks.Count > 0)
                        Common.notice(c, "<b>Neue Perks: </b>" + string.Join(" · ", hr.newPerks.Select((pk) => HeroEndless.PERKS.FirstOrDefault((x) => x.id == pk)?.name ?? pk)), "good");
                    if (hr != null && hr.milestones.Count > 0)
                        Common.notice(c, "<b>Meilensteine: </b>" + string.Join(" · ", hr.milestones.Select((w) => HeroEndless.MILESTONES.FirstOrDefault((m) => m.wave == w)?.name ?? $"W{U.Fmt(w)}")), "good");
                });
            }
            // Punkte, Seed, Statistik
            content.Add((t) =>
            {
                var cols = Common.columns(t, 12, 1, 1, 1);
                var c1 = Common.card(cols[0]);
                Common.h3(c1, $"Punkte: {TextUtil.fmt(sb.total)}");
                Common.statsTable(c1, new List<string[]>
                {
                    new[] { "Wellen", TextUtil.fmt(sb.waves) },
                    new[] { "Abschüsse", TextUtil.fmt(sb.kills) },
                    new[] { "Bosse", TextUtil.fmt(sb.bosses) },
                    new[] { "Sieg + Kernenergie", TextUtil.fmt(sb.victory + sb.lives) },
                    new[] { "Multiplikator", $"×{U.Fixed(sb.multiplier, 2)}" },
                });
                var c2 = Common.card(cols[1]);
                Common.h3(c2, "Seed");
                var sr = Ui.Row(c2, 8, TextAnchor.MiddleLeft, false);
                var code = Ui.Panel(sr.transform, "Seed", new Color(0, 0, 0, 0.35f), true, Theme.RoundSmall);
                var cl = Common.label(code.transform, s.seed.text, 18, Theme.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
                Ui.Fill(cl.rectTransform, 4);
                Ui.Size(code, Mathf.Max(120, cl.preferredWidth + 24), 34);
                string seedText = s.seed.text;
                Common.btn(sr.transform, "Kopieren", () => Common.copyText(app, seedText), Ui.BtnKind.Normal, "copy", true);
                Common.para(c2, "Gleicher Seed + gleiche Regeln = gleiche Karte und gleiche Wellen.", "small muted");
                Common.h3(c2, "Modifikatoren");
                if (s.modifiers.Count > 0) Common.bullets(c2, s.modifiers.Select((m) => $"{Modifiers.MODIFIERS[m].icon} {Modifiers.MODIFIERS[m].name}: {Modifiers.MODIFIERS[m].description}"), "small");
                else Common.para(c2, "keine", "muted small");
                var c3 = Common.card(cols[2]);
                Common.h3(c3, "Statistik");
                var rows = new List<string[]>
                {
                    new[] { "Abschüsse", TextUtil.fmt(st.kills) },
                    new[] { "Durchgebrochen", $"{U.Fmt(st.leaks)} (−{U.Fmt(st.livesLost)} ♥)" },
                    new[] { "Gold verdient / ausgegeben", $"{TextUtil.fmt(st.goldEarned)} / {TextUtil.fmt(st.goldSpent)}" },
                    new[] { "Größtes Netzwerk", $"{U.Fmt(st.maxNetworkSize)} Türme" },
                    new[] { "Kampfzeit", duration(st.duration) },
                    new[] { "Bosse", st.bossesFaced.Count > 0 ? string.Join(", ", st.bossesFaced.Select((b) => $"{Enemies.BOSSES[b].name} {(st.bossesKilled.Contains(b) ? "√" : "×")}")) : "–" },
                };
                string reac = string.Join(", ", st.reactions.Select((kv) => $"{(Elements.REACTION_BY_ID.TryGetValue(kv.Key, out var rd) && rd != null ? rd.name : kv.Key)} ×{U.Fmt(kv.Value)}"));
                rows.Add(new[] { "Reaktionen", reac.Length > 0 ? reac : "–" });
                if (s.heroes.Count > 0) rows.Add(new[] { "Helden", $"{U.Fmt(st.heroDeploys)} Einsätze · {U.Fmt(st.heroKills)} Abschüsse · {U.Fmt(st.heroDowns)} gefallen" });
                rows.Add(new[] { "Fusionen / Turmfähigkeiten", $"{U.Fmt(st.fusions)} / {U.Fmt(st.activesUsed)}" });
                rows.Add(new[] { "Mutanten besiegt", U.Fmt(st.mutationsKilled) });
                Common.statsTable(c3, rows);
            });
            // Schaden je Turmtyp, Gold pro Welle
            content.Add((t) =>
            {
                var cols = Common.columns(t, 12, 1, 1);
                var c1 = Common.card(cols[0]);
                Common.h3(c1, "Schaden je Turmtyp");
                if (dmg.Count > 0)
                {
                    var bars = Ui.Column(c1, 5);
                    foreach (var kv in dmg)
                    {
                        var row = Ui.Row(bars.transform, 8, TextAnchor.MiddleLeft, false);
                        var hl = Ui.Row(row.transform, 6, TextAnchor.MiddleLeft, false);
                        Ui.Size(hl, 210);
                        Common.modelIcon(hl.transform, kv.Key, 30);
                        var nm = Common.label(hl.transform, Towers.TOWERS.TryGetValue(kv.Key, out var td) ? td.name : kv.Key, Common.SMALL, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                        Ui.Size(nm, -1, -1, 1);
                        var track = Ui.Panel(row.transform, "Track", new Color(0, 0, 0, 0.45f), false, Theme.RoundSmall);
                        Ui.Size(track, -1, 14, 1);
                        var fill = Ui.Panel(track.transform, "Fill", Theme.Accent, false, Theme.RoundSmall);
                        var fr = fill.rectTransform;
                        fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2((float)(kv.Value / maxDmg), 1);
                        fr.offsetMin = fr.offsetMax = Vector2.zero;
                        var hv = Common.label(row.transform, TextUtil.fmt(kv.Value), Common.SMALL, Theme.Text, FontStyle.Normal, TextAnchor.MiddleRight);
                        Ui.Size(hv, 86);
                    }
                }
                else Common.para(c1, "–", "muted");
                var c2 = Common.card(cols[1]);
                Common.h3(c2, "Gold pro Welle");
                if (st.goldPerWave.Count > 0)
                {
                    var vb = Ui.Row(c2, 2, TextAnchor.LowerLeft, true);
                    vb.childForceExpandHeight = true;
                    Ui.Size(vb, -1, 132);
                    for (int i = 0; i < st.goldPerWave.Count; i++)
                    {
                        double g = st.goldPerWave[i];
                        int wi = i;
                        var colGo = Ui.Node("VB", vb.transform);
                        var hit = colGo.AddComponent<Image>();
                        hit.color = new Color(1, 1, 1, 0.001f);
                        var le = colGo.AddComponent<LayoutElement>();
                        le.minWidth = 3; le.preferredWidth = 0; le.flexibleWidth = 1;
                        var bar = Ui.Panel(colGo.transform, "Bar", Theme.Gold, false, Theme.RoundSmall);
                        var br = bar.rectTransform;
                        br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(1, (float)(g / maxGold));
                        br.offsetMin = br.offsetMax = Vector2.zero;
                        bar.raycastTarget = false;
                        Ui.Tip(hit, () => $"Welle {wi + 1}: {U.Fmt(g)} Gold");
                    }
                }
                else Common.para(c2, "–", "muted");
            });
            content.Add((t) =>
            {
                var row = Ui.Row(t, 10, TextAnchor.MiddleCenter, false);
                Common.btn(row.transform, "Gleicher Seed erneut", again, Ui.BtnKind.Primary);
                Common.btn(row.transform, "Neuer Lauf", () => app.go("heroSetup"), Ui.BtnKind.Normal, "sword");
                Common.btn(row.transform, "Hauptmenü", () => app.go("menu"));
            });
            var el = Common.shell(app, title, null, content.ToArray());
            el.gameObject.name = "Screen_Results";
            return el;
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform resultsScreen(GameApp app, ResultParams p) => renderResults(app, p);
    }
}
