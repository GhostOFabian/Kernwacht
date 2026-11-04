// Übersetzung von ui/screens/heroSetup.ts – Startbildschirm des Helden-Endlosmodus:
// Heldenwahl, Startausrüstung (Rucksack), Karte, Fortschritt, passiver Baum.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class HeroSetupScreen
    {
        public static RectTransform renderHeroSetup(GameApp app, MapDefinition heroMap = null)
        {
            var st = app.save.settings;
            var meta = app.save.progress.heroMeta;
            var perks = HeroEndless.activePerks(meta.bestWave);
            double slots = HeroEndless.carrySlots(perks);
            var done = app.save.progress.campaignCompleted;
            string champion = meta.lastChampion != null && Champions.CHAMPIONS.ContainsKey(meta.lastChampion) && Champions.championUnlocked(meta.lastChampion, perks, meta.bestWave, done) ? meta.lastChampion : "lyra";
            List<string> kit = meta.startKit.Where((i) => meta.unlockedItems.Contains(i)).Take((int)slots).ToList();
            double nowMs = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            var setup = new RunSetup
            {
                mode = "heroEndless",
                seedText = RngUtil.randomSeedText(U.ToUint32(U.BitXor(nowMs, UnityEngine.Random.value * 1e9))),
                variant = st.lastVariant, biome = st.lastBiome,
                difficulty = st.lastDifficulty, size = st.lastSize == "normal" ? "large" : st.lastSize,
            };
            // 3.10: eigene Karte aus dem Karteneditor (direkt übergeben oder aus den gespeicherten Karten gewählt)
            var savedMaps = new List<(string name, MapDefinition map)>();
            foreach (var cm in app.save.customMaps)
            {
                MapDefinition m = null;
                try { m = decodeMap(cm.code); } catch { m = null; }
                if (m != null && WorldGenerator.validateMap(m).ok) savedMaps.Add((cm.name, m));
            }
            string mapChoice = heroMap != null ? "editor" : "random";
            Text mapHint = null;
            void applyMap()
            {
                MapDefinition m = mapChoice == "editor" ? heroMap
                    : mapChoice.StartsWith("saved:", StringComparison.Ordinal) && int.TryParse(mapChoice.Substring(6), out var si) && si >= 0 && si < savedMaps.Count ? savedMaps[si].map : null;
                setup.customMap = m != null ? U.DeepClone(m) : null;
                if (m != null)
                {
                    setup.variant = m.variant;
                    setup.biome = m.biome;
                }
                if (mapHint != null)
                    mapHint.text = TextUtil.sym(m != null ? $"Eigene Karte „{m.name}“ ({(m.variant == "mazing" ? "Mazing" : "Classic")}, {U.Fmt(m.width)}×{U.Fmt(m.height)}) – Variante, Größe und Biom kommen aus der Karte." : "Zufällige Welt aus dem Seed.");
            }

            RectTransform champBox = null, kitBox = null, passiveBox = null;
            Action renderChamps = null, renderKit = null, renderPassive = null;

            renderChamps = () =>
            {
                Common.clear(champBox);
                Common.gridRows(champBox, Champions.CHAMPION_ORDER.Count, 4, (row, i) =>
                {
                    string id = Champions.CHAMPION_ORDER[i];
                    var d = Champions.CHAMPIONS[id];
                    bool ok = Champions.championUnlocked(id, perks, meta.bestWave, done);
                    var perk = !string.IsNullOrEmpty(d.unlockPerk) ? HeroEndless.PERKS.FirstOrDefault((p) => p.id == d.unlockPerk) : null;
                    var hc = Theme.Hex(d.accent);
                    bool on = champion == id;
                    var t = Common.tile(row, () => { champion = id; renderChamps(); renderKit(); Common.sfx(app, "select"); }, on ? hc : (Color?)null);
                    t.btn.gameObject.name = "champ-" + id;
                    if (!ok)
                    {
                        t.btn.interactable = false;
                        Ui.Get<CanvasGroup>(t.btn.gameObject).alpha = 0.55f;
                    }
                    var ic = Ui.Row(t.col, 0, TextAnchor.MiddleCenter, false);
                    Common.modelIcon(ic.transform, d.model, 64);
                    Common.para(t.col, $"<b>{d.name}</b>", "", TextAnchor.UpperCenter);
                    Common.para(t.col, $"Stufe {U.Fmt(meta.heroLevel.level)}/{U.Fmt(Paragon.PARAGON.heroMaxLevel)}", "tiny muted", TextAnchor.UpperCenter);
                    Common.label(t.col, $"{d.title} · {d.role}", 14, hc, FontStyle.Normal, TextAnchor.UpperCenter);
                    Common.label(t.col, ok ? d.description : $"Gesperrt · {(perk != null ? $"Freischaltung: Welle {U.Fmt(perk.wave)} erreichen" : Champions.championUnlockText(id))}", 13, Theme.Muted, FontStyle.Normal, TextAnchor.UpperCenter);
                    if (ok) Common.label(t.col, $"Skills: {string.Join(", ", d.skills.Select((s) => s.name))}", 12, Theme.Text, FontStyle.Normal, TextAnchor.UpperCenter);
                    return t.rt;
                }, 8);
            };

            renderKit = () =>
            {
                kit = kit.Where((i) => Items.canEquipItem(i, champion) || Items.ITEMS[i].slot == "special").ToList();
                // 3.4: nur Gegenstände anzeigen, die der gewählte Held tragen kann
                var owned = meta.unlockedItems.Where((i) => Items.ITEMS.ContainsKey(i) && Items.canEquipItem(i, champion)).ToList();
                Common.clear(kitBox);
                var carry1 = HeroEndless.PERKS.FirstOrDefault((p) => p.id == "carry1");
                Common.para(kitBox, slots > 0 ? $"Rucksack: {kit.Count}/{U.Fmt(slots)} Items aus der Sammlung kostenlos mitnehmen." : $"Rucksack gesperrt – erreiche Welle {(carry1 != null ? U.Fmt(carry1.wave) : "")} für den ersten Platz. Freigeschaltete Items können im Laden jederzeit für Ressourcen in den Lauf geholt werden.", "small");
                if (owned.Count > 0)
                {
                    Common.gridRows(kitBox, owned.Count, 5, (row, i) =>
                    {
                        string id = owned[i];
                        var d = Items.ITEMS[id];
                        bool on = kit.Contains(id);
                        bool can = Items.canEquipItem(id, champion);
                        Color rc = ColorUtility.TryParseHtmlString(Items.RARITY[d.rarity].color, out var rcc) ? rcc : Theme.Hex(0xc8c8d0);
                        var img = Ui.Panel(row, "kit-" + id, Theme.Hex(0x1a1428), true, Theme.RoundSmall);
                        var o = img.GetComponent<Outline>();
                        o.effectColor = on ? rc : new Color(rc.r, rc.g, rc.b, 0.7f);
                        if (on) o.effectDistance = new Vector2(2, -2);
                        var b = img.gameObject.AddComponent<Button>();
                        b.targetGraphic = img;
                        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                        b.onClick.AddListener(() =>
                        {
                            kit = on ? kit.Where((x) => x != id).ToList() : kit.Concat(new[] { id }).ToList();
                            renderKit();
                        });
                        Common.setDisabled(b, slots == 0 || !can || (!on && kit.Count >= slots));
                        Ui.Tip(b, () => $"{d.name} – {d.description}{(can ? "" : " (nicht für diesen Helden)")}");
                        var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
                        h.padding = new RectOffset(0, 6, 4, 4);
                        h.spacing = 6;
                        h.childAlignment = TextAnchor.MiddleLeft;
                        h.childControlWidth = true; h.childControlHeight = true;
                        h.childForceExpandWidth = false; h.childForceExpandHeight = true;
                        var strip = Ui.Panel(img.transform, "Rarity", rc, false, Theme.RoundSmall);
                        strip.raycastTarget = false;
                        Ui.Size(strip, 5);
                        var le = Ui.Get<LayoutElement>(img.gameObject);
                        le.minHeight = 40;
                        var n = Common.label(img.transform, d.name, 14, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                        Ui.Size(n, -1, -1, 1);
                        return img.rectTransform;
                    }, 5);
                }
                else Common.para(kitBox, "Noch keine Items in der Sammlung.", "muted small");
            };

            renderPassive = () =>
            {
                Common.clear(passiveBox);
                passiveTree(passiveBox, meta.passive, meta.bestWave, true,
                    (id) =>
                    {
                        if (PassiveTree.passiveRaiseReason(meta.passive, meta.bestWave, id) != null) return;
                        meta.passive[id] = (meta.passive.TryGetValue(id, out var cur) ? cur : 0) + 1;
                        app.persist();
                        Common.sfx(app, "upgrade");
                        renderPassive();
                    },
                    () =>
                    {
                        meta.passive = new Dictionary<string, double>();
                        app.persist();
                        Common.sfx(app, "sell");
                        renderPassive();
                    });
            };

            var nextPerk = HeroEndless.PERKS.FirstOrDefault((p) => !perks.Contains(p.id));
            var nextMs = HeroEndless.MILESTONES.FirstOrDefault((m) => !meta.claimedMilestones.Contains(m.wave));

            var el = Common.shell(app, "Helden-Endlos", "menu", (inner) =>
            {
                var cols = Common.columns(inner, 22, 1.25f, 1f);
                var c1 = cols[0];
                var c2 = cols[1];
                // Spalte 1: Held, Startausrüstung, passiver Baum
                Common.h3(c1, "Held");
                champBox = Ui.Column(c1, 8).GetComponent<RectTransform>();
                champBox.gameObject.name = "ChampPicks";
                Common.h3(c1, "Startausrüstung");
                kitBox = Ui.Column(c1, 6).GetComponent<RectTransform>();
                kitBox.gameObject.name = "StartKit";
                passiveBox = Common.card(c1, "Passive");

                // Spalte 2: Fortschritt + Optionen
                var summary = Common.card(c2, "HeroMeta");
                Common.h3(summary, "Fortschritt");
                double freePts = Math.Max(0, meta.paragon.level + Paragon.paragonPrePoints(meta.heroLevel.level) + HeroEndless.milestoneParagon(meta.bestWave) - Paragon.paragonSpent(meta.paragon.points));
                var facts = new List<string>
                {
                    $"Bestwelle: <b>{U.Fmt(meta.bestWave)}</b> · Läufe: {U.Fmt(meta.runs)}",
                    $"Paragon (alle Helden): <b>{U.Fmt(meta.paragon.level)}/{U.Fmt(Paragon.PARAGON.maxLevel)}</b> · {U.Fmt(freePts)} Punkte frei (inkl. Vorab-Punkte ab Stufe 50 und Meilensteine) – vergeben im Lauf unter Charakter → Paragon. Die Heldenstufe (max. {U.Fmt(Paragon.PARAGON.heroMaxLevel)}) gilt für alle Helden gemeinsam und bleibt dauerhaft erhalten; jeder Held hat seinen eigenen Skillbaum.",
                    $"Sammlung: {meta.unlockedItems.Count}/{Items.ITEMS.Count} Items · Fragmente: {U.Fmt(meta.fragments.Values.Sum())}",
                    $"Aktive Perks: {(perks.Count > 0 ? string.Join(", ", perks.Select((pid) => HeroEndless.PERKS.FirstOrDefault((x) => x.id == pid)?.name)) : "keine")}",
                };
                if (nextPerk != null) facts.Add($"Nächster Perk: {nextPerk.icon} {nextPerk.name} ab Welle {U.Fmt(nextPerk.wave)}");
                if (nextMs != null) facts.Add($"Nächster Meilenstein: {nextMs.name} (Welle {U.Fmt(nextMs.wave)})");
                Common.bullets(summary, facts);
                Common.para(summary, $"Ablauf: {U.Fmt(HeroEndless.HERO_ENDLESS.prepTime)} s Vorbereitung, danach starten die Wellen automatisch (Pause hält alles an). Der Held läuft per Klick/Rechtsklick über die Karte – auch auf der Gegnerbahn und durch Türme hindurch. Gegner lassen Erfahrung, Ressourcen, Tränke und Fragmente fallen. Der Laden steht nahe dem Kern.", "tiny muted");

                var options = Common.card(c2, "Options");
                var mapOpts = new List<(string, string)> { ("random", "Zufallswelt (Seed)") };
                if (heroMap != null) mapOpts.Add(("editor", $"Aus dem Editor: {heroMap.name}"));
                for (int i = 0; i < savedMaps.Count; i++) mapOpts.Add(($"saved:{i}", $"Gespeichert: {savedMaps[i].name}"));
                Common.select(Common.field(options, "Karte"), mapOpts, mapChoice, (v) => { mapChoice = v; applyMap(); }, "Karte");
                mapHint = Common.para(options, "", "tiny muted");
                mapHint.gameObject.name = "map-hint";
                applyMap();
                var er = Ui.Row(options, 0, TextAnchor.MiddleLeft, false);
                Common.btn(er.transform, "Karteneditor öffnen", () => app.go("editor"), Ui.BtnKind.Normal, "pen", true);
                Common.seg(Common.field(options, "Kartenvariante"), new List<(string, string)> { ("classic", "Classic"), ("mazing", "Mazing") }, setup.variant, (x) => { setup.variant = x; }, "Kartenvariante");
                Common.seg(Common.field(options, "Kartengröße"), new List<(string, string)> { ("normal", "Normal"), ("large", "Groß"), ("huge", "Riesig") }, setup.size ?? "large", (x) => { setup.size = x; }, "Kartengröße");
                var biomeOpts = new List<(string, string)> { ("random", "Zufällig (aus Seed)") };
                foreach (var b in Biomes.BIOME_ORDER) biomeOpts.Add((b, Biomes.BIOMES[b].name));
                Common.select(Common.field(options, "Biom"), biomeOpts, setup.biome, (v) => { setup.biome = v; }, "Biom");
                Common.seg(Common.field(options, "Schwierigkeit"), new[] { "easy", "normal", "hard" }.Select((d) => (d, Balance.DIFFICULTY[d].name)).ToList(), setup.difficulty, (x) => { setup.difficulty = x; }, "Schwierigkeit");
                Common.input(Common.field(options, "Seed"), setup.seedText, 32, (f, v) =>
                {
                    setup.seedText = !string.IsNullOrEmpty(v) ? v : setup.seedText;
                    if (string.IsNullOrEmpty(v)) f.SetTextWithoutNotify(setup.seedText);
                });
                Common.space(options, 4);
                var start = Common.bigButton(options, "play", "Lauf starten", null, () =>
                {
                    Common.sfx(app, "ui");
                    if (setup.customMap != null) { setup.variant = setup.customMap.variant; setup.biome = setup.customMap.biome; }
                    app.startRun(new RunSetup
                    {
                        mode = setup.mode, seedText = setup.seedText, variant = setup.variant, biome = setup.biome, difficulty = setup.difficulty,
                        missionId = setup.missionId, customMap = setup.customMap, size = setup.size, heroes = setup.heroes,
                        heroEndless = app.heroEndlessSetup(champion, kit),
                    });
                }, Ui.BtnKind.Primary);
                start.gameObject.name = "start-hero";
            });
            renderPassive();
            renderChamps();
            renderKit();
            return el;
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform heroSetupScreen(GameApp app, MapDefinition heroMap = null) => renderHeroSetup(app, heroMap);

        // ------------------------------------------------------------ Passiver Baum (Heldenleisten-Übersetzer: PassiveTreeView)

        /// <summary>
        /// Ruft PassiveTreeView.renderPassiveTree(...) des Heldenleisten-Teils auf (per Reflexion, damit die Signatur –
        /// mit oder ohne Eltern-Transform als erstem Parameter – nicht festgelegt sein muss). Fehlt sie, zeigt ein
        /// einfacher Ersatz den Baum mit denselben Regeln.
        /// PORT: Erwartet `static renderPassiveTree([Transform parent,] Dictionary<string,double> passive, double bestWave, bool editable, Action<string> onRaise, Action onReset)`.
        /// </summary>
        static void passiveTree(RectTransform parent, Dictionary<string, double> passive, double bestWave, bool editable, Action<string> onRaise, Action onReset)
        {
            try
            {
                var type = typeof(Common).Assembly.GetTypes().FirstOrDefault((t) => t.Name == "PassiveTreeView");
                var m = type?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault((x) => x.Name == "renderPassiveTree");
                if (m != null)
                {
                    var ps = m.GetParameters();
                    var values = new object[] { passive, bestWave, editable, onRaise, onReset };
                    var args = new object[ps.Length];
                    int vi = 0;
                    bool ok = true;
                    for (int i = 0; i < ps.Length && ok; i++)
                    {
                        var pt = ps[i].ParameterType;
                        if (typeof(Transform).IsAssignableFrom(pt)) { args[i] = parent; continue; }
                        if (vi < values.Length)
                        {
                            var v = values[vi++];
                            if (v == null || pt.IsInstanceOfType(v)) args[i] = v;
                            else if (pt == typeof(float)) args[i] = (float)(double)v;
                            else if (pt == typeof(int)) args[i] = (int)(double)v;
                            else ok = false;
                        }
                        else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                        else ok = false;
                    }
                    if (ok)
                    {
                        var res = m.Invoke(null, args);
                        Transform rt = res is Component c ? c.transform : res is GameObject g ? g.transform : null;
                        if (rt != null && rt != parent && rt.parent != parent) rt.SetParent(parent, false);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HeroSetup] PassiveTreeView nicht nutzbar, Ersatzdarstellung: {e.Message}");
                Common.clear(parent);
            }
            fallbackPassiveTree(parent, passive, bestWave, editable, onRaise, onReset);
        }

        static void fallbackPassiveTree(Transform parent, Dictionary<string, double> passive, double bestWave, bool editable, Action<string> onRaise, Action onReset)
        {
            var PS = PassiveTree.PASSIVE;
            double points = PassiveTree.passivePoints(bestWave);
            double spent = PassiveTree.passiveSpent(passive);
            double? nextAt = points < PS.maxPoints ? (points + 1) * PS.wavesPerPoint : (double?)null;
            var head = Ui.Row(parent, 8, TextAnchor.MiddleLeft, false);
            var ht = Common.label(head.transform, $"<b>Passiver Baum</b> · Punkte <b>{U.Fmt(points - spent)} frei</b> ({U.Fmt(spent)}/{U.Fmt(points)} vergeben, max. {U.Fmt(PS.maxPoints)} bei Bestwelle {U.Fmt(PS.maxPoints * PS.wavesPerPoint)})", Common.FS);
            Ui.Size(ht, -1, -1, 1);
            if (editable && spent > 0 && onReset != null)
            {
                var rb = Common.btn(head.transform, "Zurücksetzen", () => onReset(), Ui.BtnKind.Normal, null, true);
                Ui.Tip(rb, () => "Alle Punkte kostenlos zurückerstatten");
            }
            Common.para(parent, $"1 Punkt je {U.Fmt(PS.wavesPerPoint)} Bestwellen{(nextAt != null ? $" · nächster Punkt bei Bestwelle {U.Fmt(nextAt.Value)}" : "")}. Wirkt automatisch in jedem Helden-Endloslauf (Stand beim Laufstart). {U.Fmt(PassiveTree.PASSIVE_TOTAL_RANKS)} Ränge insgesamt.", "tiny muted");
            for (int tier = 1; tier <= PS.tierGate.Length; tier++)
            {
                double gate = PS.tierGate[tier - 1];
                bool open = spent >= gate;
                Common.label(parent, $"Stufe {tier}{(U.Truthy(gate) ? $" <size={Common.TINY}><color={Theme.Css(Theme.Muted)}>· ab {U.Fmt(gate)} vergebenen Punkten</color></size>" : "")}", Common.SMALL, open ? Theme.Text : Theme.Muted, FontStyle.Bold);
                var nodes = PassiveTree.PASSIVE_NODES.Where((n) => n.tier == tier).ToList();
                Common.gridRows(parent, nodes.Count, 3, (row, i) =>
                {
                    var n = nodes[i];
                    double rank = passive.TryGetValue(n.id, out var rv) ? rv : 0;
                    string why = PassiveTree.passiveRaiseReason(passive, bestWave, n.id);
                    var t = Common.tile(row, () => onRaise?.Invoke(n.id), rank >= n.maxRank ? Theme.Gold : (editable && why == null ? Theme.Accent : (Color?)null), null, new RectOffset(8, 8, 6, 6), 2);
                    t.btn.interactable = editable && why == null;
                    if (!open) Ui.Get<CanvasGroup>(t.btn.gameObject).alpha = 0.6f;
                    Ui.Tip(t.btn, () => $"{n.name}: {n.description} je Rang{(why != null && rank < n.maxRank ? $"\n{why}" : "")}");
                    Common.para(t.col, $"<b>{n.name}</b>  {U.Fmt(rank)}/{U.Fmt(n.maxRank)}", "small", TextAnchor.UpperCenter);
                    Common.para(t.col, $"{n.description} je Rang", "tiny", TextAnchor.UpperCenter);
                    return t.rt;
                }, 5);
            }
        }

        // ------------------------------------------------------------ editor/mapCode.ts: decodeMap (nur Lesen)

        const string PREFIX = "RB1";
        static readonly Regex RleRx = new Regex(@"^([0-5])(?:\*(\d{1,3}))?$");
        static readonly Regex DimRx = new Regex(@"^(\d{1,2})x(\d{1,2})$");

        static List<double> unrle(string s, int expected)
        {
            var @out = new List<double>();
            foreach (var part in s.Split(','))
            {
                var m = RleRx.Match(part);
                if (!m.Success) return null;
                int n = m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
                for (int k = 0; k < n; k++) @out.Add(m.Groups[1].Value[0] - '0');
                if (@out.Count > expected) return null;
            }
            return @out.Count == expected ? @out : null;
        }

        /// <summary>Ergänzt abgeleitete Felder (Start/Ziel aus Zellen, Höhen, Dekoration) – deterministisch (editor/mapCode.ts).</summary>
        static MapDefinition finalizeEditorMap(MapDefinition map)
        {
            int w = (int)map.width;
            var spawns = new List<GridPos>();
            for (int i = 0; i < map.cells.Count; i++)
            {
                if (map.cells[i] == Cell.Spawn) spawns.Add(new GridPos(i % w, Math.Floor((double)i / w)));
                if (map.cells[i] == Cell.Goal) map.goal = new GridPos(i % w, Math.Floor((double)i / w));
            }
            if (spawns.Count > 0) map.spawn = spawns[0];
            map.spawns = spawns.Count > 0 ? spawns : new List<GridPos> { map.spawn };
            map.heights = U.Fill(map.cells.Count, 0.0);
            var kinds = Biomes.BIOMES[map.biome].decorKinds;
            double salt = RngUtil.hashString(map.biome + map.variant);
            map.decor = new List<double>();
            for (int i = 0; i < map.cells.Count; i++)
            {
                if (map.cells[i] != Cell.Blocked) continue;
                double h = RngUtil.mix32(U.BitXor(salt, i * 2654435761.0));
                map.decor.Add(i);
                map.decor.Add(kinds[(int)(h % kinds.Count)]);
                map.decor.Add(h % 628);
            }
            map.veins = map.veins.Where((i) => map.cells[(int)i] == Cell.Open).ToList();
            var veins = map.veins;
            map.ley = map.ley.Where((i) => map.cells[(int)i] == Cell.Open && !veins.Contains(i)).ToList();
            map.version = WorldGenerator.MAP_VERSION;
            return map;
        }

        /// <summary>Kartencode RB1.&lt;variant&gt;.&lt;biome&gt;.&lt;w&gt;x&lt;h&gt;.&lt;zellen&gt;.&lt;adern&gt;.&lt;leylinien&gt; → Karte (null bei Fehler).</summary>
        static MapDefinition decodeMap(string code)
        {
            if (code == null) return null;
            var parts = code.Trim().Split('.');
            if (parts.Length != 7 || parts[0] != PREFIX) return null;
            string variant = parts[1], biome = parts[2], dims = parts[3], cellStr = parts[4], veinStr = parts[5], leyStr = parts[6];
            if (variant != "classic" && variant != "mazing") return null;
            if (!Biomes.BIOME_ORDER.Contains(biome)) return null;
            var dm = DimRx.Match(dims);
            if (!dm.Success) return null;
            int w = int.Parse(dm.Groups[1].Value, CultureInfo.InvariantCulture);
            int h = int.Parse(dm.Groups[2].Value, CultureInfo.InvariantCulture);
            if (w < 8 || h < 6 || w > 48 || h > 40) return null;
            var cells = unrle(cellStr, w * h);
            if (cells == null) return null;
            List<double> parseList(string s)
            {
                if (s == "") return new List<double>();
                var xs = new List<double>();
                foreach (var p in s.Split('-'))
                {
                    double x = p.Trim().Length == 0 ? 0 : (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN);
                    if (double.IsNaN(x) || Math.Floor(x) != x || x < 0 || x >= w * h) return null;
                    xs.Add(x);
                }
                return xs;
            }
            var veins = parseList(veinStr);
            var ley = parseList(leyStr);
            if (veins == null || ley == null) return null;
            return finalizeEditorMap(new MapDefinition
            {
                version = WorldGenerator.MAP_VERSION, width = w, height = h, variant = variant, biome = biome,
                cells = cells, heights = new List<double>(), veins = veins, ley = ley, decor = new List<double>(), spawn = new GridPos(0, 0), goal = new GridPos(0, 0),
                terrainEvent = null, fallback = false, name = "Eigene Karte",
            });
        }
    }
}
