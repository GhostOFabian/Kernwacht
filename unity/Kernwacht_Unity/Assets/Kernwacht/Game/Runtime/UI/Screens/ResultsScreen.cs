// Übersetzung von ui/screens/results.ts – Ergebnisbildschirm nach einer Partie.

using System;
using System.Collections.Generic;
using System.Collections;
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
                    heroEndless = he != null ? app.heroEndlessSetup(he.champion.id, app.save.progress.heroMeta.startKit, he.doctrine) : null,
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
            // 4.0: Turmstatistik – Anzahl, Schaden, Anteil und Effizienz je Turmtyp
            content.Add((t) => towerTable(app, t, s));
            // Gold pro Welle
            content.Add((t) =>
            {
                var cols = Common.columns(t, 12, 1);
                var c2 = Common.card(cols[0]);
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

        class TowerRow
        {
            public string id;
            public string name;
            public double end, built, dmg, kills, invested, first = -1;
            public double perWave(GameState s) => first < 0 ? -1 : dmg / Math.Max(1, s.stats.wavesCleared - first);
        }

        static List<TowerRow> towerRows(GameState s)
        {
            var st = s.stats;
            var rows = new Dictionary<string, TowerRow>();
            TowerRow get(string id)
            {
                if (!rows.TryGetValue(id, out var r))
                {
                    r = new TowerRow { id = id, name = Towers.TOWERS.TryGetValue(id, out var td) ? td.name : id };
                    rows[id] = r;
                }
                return r;
            }
            foreach (var kv in st.damageByTower) get(kv.Key).dmg = kv.Value;
            foreach (var kv in st.builtByTower) get(kv.Key).built = kv.Value;
            if (st.killsByTower != null) foreach (var kv in st.killsByTower) get(kv.Key).kills = kv.Value;
            if (st.firstWaveByTower != null) foreach (var kv in st.firstWaveByTower) get(kv.Key).first = kv.Value;
            foreach (var tw in s.towers)
            {
                var r = get(tw.def);
                r.end++;
                r.invested += tw.invested;
            }
            var list = rows.Values.Where((r) => r.id != "barricade" && r.id != "lanebarricade").ToList();
            U.StableSort(list, (a, b) => U.Or(b.dmg - a.dmg, b.end - a.end));
            return list;
        }

        /// <summary>Text der Turmstatistik zum Kopieren (z. B. zum Teilen für die Balance).</summary>
        static string towerTableText(GameState s)
        {
            var rows = towerRows(s);
            double total = Math.Max(1, rows.Sum((r) => r.dmg));
            var sb = new System.Text.StringBuilder();
            sb.Append($"Kernwacht · {TextUtil.modeName(s.mode)} · Welle {U.Fmt(s.stats.wavesCleared)} · Seed {s.seed.text}\n");
            if (s.he != null) sb.Append($"Held: {Champions.CHAMPIONS[s.he.champion.id].name} St. {U.Fmt(s.he.champion.level)} · Schaden {TextUtil.fmt(s.he.champion.damageDealt)} · Doktrin {Doctrines.of(s.he).name}\n");
            sb.Append("Turm\tAm Ende\tGebaut\tAb Welle\tSchaden\tAnteil\tSchaden/Welle seit Bau\tAbschüsse\tInvestiert\tSchaden/100 Gold\n");
            foreach (var r in rows)
                sb.Append($"{r.name}\t{U.Fmt(r.end)}\t{U.Fmt(r.built)}\t{(r.first >= 0 ? U.Fmt(r.first) : "–")}\t{TextUtil.fmt(r.dmg)}\t{U.Fixed(r.dmg / total * 100, 1)} %\t{(r.perWave(s) >= 0 ? TextUtil.fmt(r.perWave(s)) : "–")}\t{U.Fmt(r.kills)}\t{TextUtil.fmt(r.invested)}\t{(r.invested > 0 ? TextUtil.fmt(r.dmg / r.invested * 100) : "–")}\n");
            return sb.ToString();
        }

        static void towerTable(GameApp app, Transform t, GameState s)
        {
            var rows = towerRows(s);
            double total = Math.Max(1, rows.Sum((r) => r.dmg));
            double maxDmg = Math.Max(1, rows.Count > 0 ? rows.Max((r) => r.dmg) : 0);
            var c = Common.card(t, "TowerStats");
            var head = Ui.Row(c, 10, TextAnchor.MiddleLeft, false);
            var ht = Common.h3(head.transform, "Türme im Lauf");
            Ui.Size(ht, -1, -1, 1);
            string text = towerTableText(s);
            Common.btn(head.transform, "Als Bild kopieren", () => app.StartCoroutine(captureStats(app, s)), Ui.BtnKind.Primary, "copy", true);
            Common.btn(head.transform, "Als Text kopieren", () => Common.copyText(app, text), Ui.BtnKind.Normal, "copy", true);
            Common.para(c, "Anzahl am Ende des Laufs, insgesamt gebaut (Fusionen zählen als gebaut), Schaden und Anteil am Turmschaden, Abschüsse, investiertes Gold der verbliebenen Türme, Schaden je 100 Gold, Welle des ersten Baus und Schaden je Welle seit dem ersten Bau. „Als Bild kopieren“ legt eine gut lesbare Übersicht in die Zwischenablage.", "small muted");
            if (rows.Count == 0) { Common.para(c, "–", "muted"); return; }
            float[] w = { 230, 56, 60, -1, 92, 62, 72, 80, 90, 64, 84 };
            string[] hd = { "Turm", "Ende", "Gebaut", "Schaden", "", "Anteil", "Abschüsse", "Investiert", "je 100 Gold", "ab Welle", "je Welle" };
            var hr = Ui.Row(c, 8, TextAnchor.MiddleLeft, false);
            for (int k = 0; k < hd.Length; k++)
            {
                if (k == 4) continue;
                var l = Common.label(hr.transform, hd[k], Common.SMALL, Theme.Muted, FontStyle.Bold, k == 0 || k == 3 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                if (k == 3) Ui.Size(l, -1, -1, 1); else Ui.Size(l, w[k] + (k == 5 ? w[4] + 8 : 0));
            }
            var body = Ui.Column(c, 4);
            foreach (var r in rows)
            {
                var row = Ui.Row(body.transform, 8, TextAnchor.MiddleLeft, false);
                var nm = Ui.Row(row.transform, 6, TextAnchor.MiddleLeft, false);
                Ui.Size(nm, w[0]);
                Common.modelIcon(nm.transform, r.id, 28);
                var nl = Common.label(nm.transform, r.name, Common.SMALL, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                Ui.Size(nl, -1, -1, 1);
                num(row.transform, U.Fmt(r.end), w[1], r.end > 0 ? Theme.Text : Theme.Muted);
                num(row.transform, U.Fmt(r.built), w[2], Theme.Muted);
                var track = Ui.Panel(row.transform, "Track", new Color(0, 0, 0, 0.45f), false, Theme.RoundSmall);
                Ui.Size(track, -1, 14, 1);
                var fill = Ui.Panel(track.transform, "Fill", Theme.Accent, false, Theme.RoundSmall);
                var fr = fill.rectTransform;
                fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2((float)(r.dmg / maxDmg), 1);
                fr.offsetMin = fr.offsetMax = Vector2.zero;
                num(row.transform, TextUtil.fmt(r.dmg), w[4], Theme.Text);
                num(row.transform, $"{U.Fixed(r.dmg / total * 100, 1)} %", w[5], r.dmg / total >= 0.25 ? Theme.Gold : Theme.Text);
                num(row.transform, U.Fmt(r.kills), w[6], Theme.Muted);
                num(row.transform, r.invested > 0 ? TextUtil.fmt(r.invested) : "–", w[7], Theme.Muted);
                num(row.transform, r.invested > 0 && r.dmg > 0 ? TextUtil.fmt(r.dmg / r.invested * 100) : "–", w[8], Theme.Text);
                num(row.transform, r.first >= 0 ? U.Fmt(r.first) : "–", w[9], Theme.Muted);
                num(row.transform, r.perWave(s) >= 0 ? TextUtil.fmt(r.perWave(s)) : "–", w[10], Theme.Text);
            }
        }

        // ------------------------------------------------------------ 4.1: Statistik als Bild

        static Text shotText(Transform p, string text, float x, float y, float w, float h, int size, Color col, TextAnchor align, FontStyle style = FontStyle.Normal)
        {
            var t = Ui.Label(p, text, size, col, align, style);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            Ui.Place(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
            return t;
        }

        static void shotBox(Transform p, float x, float y, float w, float h, Color col)
        {
            var img = Ui.Panel(p, "Box", col, false, null);
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            Ui.Place(img.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
        }

        /// <summary>
        /// Baut eine aufgeräumte, bildschirmfüllende Zusammenfassung (Lauf, Held, Turmtabelle), nimmt sie als Bild auf
        /// und legt das PNG in die Zwischenablage (sonst Download).
        /// </summary>
        static IEnumerator captureStats(GameApp app, GameState s)
        {
            var rows = towerRows(s);
            double total = Math.Max(1, rows.Sum((r) => r.dmg));
            double maxDmg = Math.Max(1, rows.Count > 0 ? rows.Max((r) => r.dmg) : 0);
            var effs = rows.Where((r) => r.invested > 0 && r.dmg > 0).Select((r) => r.dmg / r.invested * 100).OrderBy((v) => v).ToList();
            double effLo = effs.Count > 0 ? effs[effs.Count / 3] : 0, effHi = effs.Count > 0 ? effs[effs.Count * 2 / 3] : 0;
            var layer = app.tooltipLayer != null ? app.tooltipLayer : app.root;
            var go = Ui.Node("StatsShot", layer);
            var rt = Ui.Fill(go.GetComponent<RectTransform>());
            go.transform.SetAsLastSibling();
            var bg = go.AddComponent<Image>();
            bg.color = Theme.Bg;
            float W = app.root.rect.width, H = app.root.rect.height;
            float m = 22;
            // Kopf
            var st = s.stats;
            shotText(go.transform, $"Kernwacht · {TextUtil.modeName(s.mode)}{(s.theme == "scifi" ? " · Sci-Fi" : "")} · Welle {U.Fmt(st.wavesCleared)} · {Balance.DIFFICULTY[s.difficulty].name}", m, m, W - 2 * m, 30, 24, Theme.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            string line2 = $"Seed {s.seed.text} · Karte {Biomes.BIOMES[s.map.biome].name} ({s.map.variant}) · Kampfzeit {duration(st.duration)} · Gold verdient {TextUtil.fmt(st.goldEarned)} / ausgegeben {TextUtil.fmt(st.goldSpent)} · Leben {U.Fmt(s.lives)}/{U.Fmt(s.maxLives)}";
            shotText(go.transform, line2, m, m + 32, W - 2 * m, 22, 16, Theme.Text, TextAnchor.UpperLeft);
            string line3 = s.he != null
                ? $"Held: {Champions.CHAMPIONS[s.he.champion.id].name} St. {U.Fmt(s.he.champion.level)} · Heldenschaden {TextUtil.fmt(s.he.champion.damageDealt)} · Doktrin {Doctrines.of(s.he).name} · Turmschaden gesamt {TextUtil.fmt(total)}"
                : $"Turmschaden gesamt {TextUtil.fmt(total)}";
            shotText(go.transform, line3, m, m + 56, W - 2 * m, 22, 16, Theme.Muted, TextAnchor.UpperLeft);
            if (s.modifiers.Count > 0)
            {
                string mods = string.Join(", ", s.modifiers.Select((id) => Modifiers.MODIFIERS.TryGetValue(id, out var md) ? md.name : id));
                shotText(go.transform, "Modifikatoren: " + mods, m, m + 80, W - 2 * m, 22, 14, Theme.Muted, TextAnchor.UpperLeft);
            }
            float top = m + 108;
            // Tabelle (bei vielen Türmen zweispaltig)
            int n = Math.Max(1, rows.Count);
            float avail = H - top - m - 26;
            int cols = avail / n < 19 ? 2 : 1;
            int per = (n + cols - 1) / cols;
            float rowH = Mathf.Clamp(avail / per, 14, 30);
            int fs = Mathf.Clamp(Mathf.RoundToInt(rowH * 0.6f), 11, 18);
            float gap = 24;
            float tw = (W - 2 * m - gap * (cols - 1)) / cols;
            // Spalten: Name, Ende, Gebaut, Schaden (+Balken), Anteil, Abschüsse, Investiert, je 100 Gold
            float[] fr = { 0.22f, 0.05f, 0.06f, 0.06f, 0.17f, 0.07f, 0.1f, 0.07f, 0.09f, 0.11f };
            string[] hd = { "Turm", "Ende", "Gebaut", "ab W.", "Schaden", "Anteil", "je Welle", "Kills", "Investiert", "je 100 Gold" };
            for (int c = 0; c < cols; c++)
            {
                float x0 = m + c * (tw + gap);
                float x = x0;
                for (int k = 0; k < hd.Length; k++)
                {
                    float cw = fr[k] * tw;
                    shotText(go.transform, hd[k], x + 4, top, cw - 8, 22, fs, Theme.Muted, k == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.Bold);
                    x += cw;
                }
                shotBox(go.transform, x0, top + 23, tw, 1, Theme.LineStrong);
                for (int i = 0; i < per; i++)
                {
                    int idx = c * per + i;
                    if (idx >= rows.Count) break;
                    var r = rows[idx];
                    float y = top + 26 + i * rowH;
                    if (i % 2 == 0) shotBox(go.transform, x0, y, tw, rowH, new Color(1, 1, 1, 0.045f));
                    double eff = r.invested > 0 && r.dmg > 0 ? r.dmg / r.invested * 100 : -1;
                    Color effC = eff < 0 ? Theme.Muted : eff >= effHi ? Theme.Good : eff <= effLo ? Theme.Bad : Theme.Text;
                    string[] cells =
                    {
                        $"{idx + 1}. {r.name}", U.Fmt(r.end), U.Fmt(r.built), r.first >= 0 ? U.Fmt(r.first) : "–", TextUtil.fmt(r.dmg), $"{U.Fixed(r.dmg / total * 100, 1)} %",
                        r.perWave(s) >= 0 ? TextUtil.fmt(r.perWave(s)) : "–", U.Fmt(r.kills), r.invested > 0 ? TextUtil.fmt(r.invested) : "–", eff >= 0 ? TextUtil.fmt(eff) : "–",
                    };
                    x = x0;
                    for (int k = 0; k < cells.Length; k++)
                    {
                        float cw = fr[k] * tw;
                        if (k == 4)
                        {
                            float bw = (cw - 8) * 0.45f;
                            shotBox(go.transform, x + 4, y + rowH * 0.35f, bw, rowH * 0.3f, new Color(0, 0, 0, 0.45f));
                            shotBox(go.transform, x + 4, y + rowH * 0.35f, Mathf.Max(1, bw * (float)(r.dmg / maxDmg)), rowH * 0.3f, Theme.Accent);
                        }
                        Color col = k == 9 ? effC : k == 5 && r.dmg / total >= 0.2 ? Theme.Gold : k == 0 ? Theme.Text : r.end > 0 || k != 1 ? Theme.Text : Theme.Muted;
                        shotText(go.transform, cells[k], x + 4, y, cw - 8, rowH, fs, col, k == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                        x += cw;
                    }
                }
            }
            shotText(go.transform, "Ende = Türme am Ende · Gebaut = insgesamt gebaut (Fusionen zählen) · ab W. = erstmals gebaut in Welle · je Welle = Schaden je Welle seit dem ersten Bau · Investiert = Gold in den verbliebenen Türmen · je 100 Gold: grün = oberes Drittel, rot = unteres Drittel", m, H - m - 18, W - 2 * m, 18, 12, Theme.Muted, TextAnchor.MiddleLeft);
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); }
            catch (Exception e) { Debug.LogWarning("[Statistik] Aufnahme fehlgeschlagen: " + e.Message); }
            UnityEngine.Object.Destroy(go);
            if (tex == null) { app.toast("Bild konnte nicht aufgenommen werden.", "warn", 4000); yield break; }
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);
            int res = WebClipboard.CopyPng(png, $"kernwacht-welle-{U.Fmt(st.wavesCleared)}.png");
            if (res == 1) app.toast("Statistik als Bild in die Zwischenablage gelegt – mit Strg+V einfügen. (Verweigert der Browser das, wird das Bild heruntergeladen.)", "info", 6000);
            else if (res == 2) app.toast("Statistik als Bild heruntergeladen.", "info", 5000);
            else app.toast("Bild konnte nicht kopiert werden.", "warn", 4000);
        }

        static void num(Transform p, string text, float width, Color col)
        {
            var l = Common.label(p, text, Common.SMALL, col, FontStyle.Normal, TextAnchor.MiddleRight);
            Ui.Size(l, width);
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform resultsScreen(GameApp app, ResultParams p) => renderResults(app, p);
    }
}
