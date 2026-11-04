using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;
using UText = UnityEngine.UI.Text;

namespace Kernwacht.Game.UI
{
    // Turmübersicht (Taste O), modale Dialoge (Modifikatorwahl, Pause, Bestätigung) und Einführung – Teil von ui/Hud.ts.
    public partial class Hud
    {
        /// <summary>Live-Aktualisierungen der Turmübersicht (Texte, Sperren) ohne Neuaufbau der Knöpfe.</summary>
        readonly List<Action> tlLive = new List<Action>();
        ScrollRect tlRowsScroll;

        public void toggleTowerList(bool? force = null)
        {
            towerListOpen = force ?? !towerListOpen;
            towerWinSig = "";
        }

        static void TxtSet(UText t, string s) { if (t.text != s) t.text = s; }

        void updateTowerList()
        {
            HK.Show(towerWin, towerListOpen);
            if (!towerListOpen) return;
            var s = sim.s;
            // Nur strukturelle Änderungen bauen die Liste neu auf. Gold, Abschüsse, Schaden und Abklingzeiten werden
            // live in die bestehenden Elemente geschrieben – sonst würden Knöpfe in jedem Bild ersetzt und Klicks gingen verloren.
            var pairs = EconomySystem.fusionPairs(sim);
            sig.Reset().S(towerSort).C('|').B(hideMaxed).C('|').S(sellAllArmed).C('|').B(s.paused && v.miniPaused).C('|').S(s.phase).C('|');
            foreach (var t in s.towers) sig.N(t.uid).C(':').S(t.def).C(':').N(t.level).C(':').N(t.spec).C(':').N(t.stars ?? 0).C(':').N(t.cx).C(',').N(t.cy).C(',');
            sig.C('|').N(v.selectedTower).C('|');
            foreach (var p in pairs) sig.N(p.a.uid).C('-').N(p.b.uid).C('-').B(p.ready).C(',');
            if (sig.Same(towerWinSig) || (pointerHeld && towerWinSig != ""))
            {
                for (int i = 0; i < tlLive.Count; i++) tlLive[i]();
                return;
            }
            towerWinSig = sig.ToString();
            tlLive.Clear();
            void live(Action f) { tlLive.Add(f); f(); }
            // Scrollposition über das Neuzeichnen hinweg erhalten
            float prevScroll = tlRowsScroll != null && tlRowsScroll.content != null ? tlRowsScroll.content.anchoredPosition.y : 0;
            tlRowsScroll = null;
            var win = towerWin.transform;
            HK.Clear(win);
            // Türme mit Fusionspartner gelten nie als „ausgebaut“ – sie sollen sichtbar bleiben
            var inPair = new HashSet<double>();
            foreach (var p in pairs) { inPair.Add(p.a.uid); inPair.Add(p.b.uid); }
            bool endless = s.rules.endless;
            bool isMaxed(TowerInstance t) => !inPair.Contains(t.uid) && EconomySystem.nextUpgradeCost(t) == null && !(Towers.TOWERS[t.def].specs.Count == 2 && t.spec == 0 && Towers.TOWERS[t.def].legendary != true) && (!endless || Towers.TOWERS[t.def].attack == "none" || (t.stars ?? 0) >= Goldsinks.STARS.max);
            int maxedCount = s.towers.Count(isMaxed);
            var list = s.towers.Where((t) => !hideMaxed || !isMaxed(t) || t.uid == v.selectedTower).ToList();
            if (towerSort == "damage") U.StableSort(list, (x, y) => y.damageDealt - x.damageDealt);
            else if (towerSort == "level") U.StableSort(list, (x, y) => { double d = y.level - x.level; return d != 0 ? d : x.uid - y.uid; });
            else U.StableSort(list, (x, y) => { double d = U.IndexOf(Towers.BUILD_ORDER, x.def) - U.IndexOf(Towers.BUILD_ORDER, y.def); return d != 0 ? d : x.uid - y.uid; });

            // Kopf
            var head = HK.Flow(win, 6);
            var h2 = HK.T(head.transform, $"Türme ({s.towers.Count})", HK.H2 - 4, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            h2.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(h2, -1, 32);
            void sorter(string id, string name)
            {
                HK.Sm(head.transform, name, () => { towerSort = id; towerWinSig = ""; }, towerSort == id ? Ui.BtnKind.On : Ui.BtnKind.Normal);
            }
            sorter("type", "Typ"); sorter("level", "Stufe"); sorter("damage", "Schaden");
            var upAll = HK.Sm(head.transform, "▲ Alle", () => a.upgradeAll(), Ui.BtnKind.Primary);
            HK.Tip(upAll, endless ? "Alle Türme aufwerten und danach veredeln (★), solange das Gold reicht – günstigste Schritte zuerst (Spezialisierung wählst du selbst)" : "Alle Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht (ohne Spezialisierung)");
            live(() =>
            {
                var c = EconomySystem.upgradeAllCost(sim);
                var starC = endless ? s.towers.Where(EconomySystem.fullyBuilt).Select((t) => EconomySystem.starCost(t)).Where(U.IsFinite).ToList() : new List<double>();
                double cheapest = double.PositiveInfinity;
                foreach (var t in EconomySystem.upgradableTowers(sim)) cheapest = Math.Min(cheapest, EconomySystem.nextUpgradeCost(t) ?? double.PositiveInfinity);
                foreach (var x in starC) cheapest = Math.Min(cheapest, x);
                HK.Enable(upAll, !((c.steps == 0 && starC.Count == 0) || cheapest > s.gold));
                HK.SetBtnText(upAll, U.Truthy(c.steps) ? $"▲ Alle ({HK.fmt(c.total)})" : starC.Count > 0 ? $"★ Alle veredeln (ab {HK.fmt(starC.Min())})" : "▲ Alle");
            });
            var sellAll = sellAllButton(head.transform, "all", null, "Alle verkaufen");
            live(() =>
            {
                var sv = EconomySystem.sellAllValue(sim);
                bool armed = sellAllArmed == "all" && HK.Now() - sellAllArmedAt < 4000;
                HK.SetBtnText(sellAll, armed ? $"Wirklich? +{U.Fmt(sv.gold)} Gold" : $"Alle verkaufen (+{U.Fmt(sv.gold)})");
                var o = sellAll.GetComponent<Outline>();
                if (o != null) o.effectColor = armed ? Theme.Gold : Theme.Line;
            });
            var chk = Ui.Check(head.transform, $"Ausgebaute ausblenden ({maxedCount})", hideMaxed, (on) => { hideMaxed = on; towerWinSig = ""; });
            Ui.Size(chk, -1, 32);
            HK.Tip(HK.IconBtn(head.transform, "close", () => toggleTowerList(false), 32), "Schließen (O/Esc)");

            // Fusionen
            if (pairs.Count > 0)
            {
                var content = Ui.Scroll(win, out var fscroll, 4, new RectOffset(6, 6, 6, 6));
                var fimg = fscroll.gameObject.AddComponent<Image>();
                fimg.sprite = Theme.RoundSmall; fimg.type = Image.Type.Sliced; fimg.color = new Color(1f, 216 / 255f, 74 / 255f, 0.05f);
                var fo = fscroll.gameObject.AddComponent<Outline>(); fo.effectColor = new Color(1f, 216 / 255f, 74 / 255f, 0.35f);
                Ui.Size(fscroll, -1, Mathf.Min(220, 34 + pairs.Count * 40));
                HK.T(content, $"✦ Fusionen ({pairs.Count((p) => p.ready)}/{pairs.Count} bereit)", HK.SMALL, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                foreach (var p in pairs)
                {
                    var ld = Towers.TOWERS[p.recipe.id];
                    var miss = new List<string>();
                    foreach (var x in new[] { p.a, p.b })
                    {
                        if (x.level < 4) miss.Add($"{Towers.TOWERS[x.def].name} Stufe {U.Fmt(x.level)}/4");
                        else if (!U.Truthy(x.spec)) miss.Add($"{Towers.TOWERS[x.def].name} Spezialisierung fehlt");
                    }
                    var row = HK.HRow(content, 5);
                    HK.Img(row.transform, Icons.Model(p.a.def), 28);
                    HK.T(row.transform, "+", 15).horizontalOverflow = HorizontalWrapMode.Overflow;
                    HK.Img(row.transform, Icons.Model(p.b.def), 28);
                    HK.T(row.transform, "→", 15).horizontalOverflow = HorizontalWrapMode.Overflow;
                    HK.Img(row.transform, Icons.Model(p.recipe.id), 28);
                    var main = HK.VCol(row.transform, 1);
                    HK.Grow(main);
                    Bold(main.transform, ld.name, 15, p.ready ? Theme.Hex(0xffe9a0) : Theme.Text);
                    Tiny(main.transform, p.ready ? "Bereit – entsteht in Richtung Lane" : $"Fehlt: {string.Join(", ", miss)}");
                    var pa = p;
                    if (p.ready)
                    {
                        var btn = HK.Sm(row.transform, $"Verschmelzen ({U.Fmt(p.recipe.cost)})", () => a.fuse(pa.a.uid, pa.b.uid), Ui.BtnKind.Primary);
                        HK.Tip(btn, $"{ld.name} entsteht auf dem Feld näher an der Lane ({Towers.TOWERS[p.target.def].name})");
                        live(() => HK.Enable(btn, s.gold >= pa.recipe.cost));
                    }
                    else
                    {
                        var zb = HK.Sm(row.transform, "Zeigen", () =>
                        {
                            var x = pa.a.level < 4 || !U.Truthy(pa.a.spec) ? pa.a : pa.b;
                            a.selectTower(x.uid);
                            a.lookAt(x.cx + 0.5, x.cy + 0.5);
                        });
                        HK.Tip(zb, "Turm anzeigen");
                    }
                }
            }

            // 3.6: je Turmsorte ein Knopf – alle dieser Sorte aufwerten bzw. veredeln, ohne durch die Liste zu scrollen
            if (s.towers.Count > 0)
            {
                var defs = s.towers.Select((t) => t.def).Distinct().Where((d) => Towers.TOWERS[d].attack != "none").ToList();
                U.StableSort(defs, (x, y) => { double d = U.IndexOf(Towers.BUILD_ORDER, x) - U.IndexOf(Towers.BUILD_ORDER, y); return d != 0 ? d : U.IndexOf(Towers.LEGENDARY_ORDER, x) - U.IndexOf(Towers.LEGENDARY_ORDER, y); });
                if (defs.Count > 0)
                {
                    var content = Ui.Scroll(win, out var tscroll, 0, new RectOffset(0, 0, 4, 4));
                    Ui.Size(tscroll, -1, Mathf.Min(200, 8 + ((defs.Count + 1) / 2) * 50));
                    var vlg = content.GetComponent<VerticalLayoutGroup>();
                    if (vlg != null) UnityEngine.Object.DestroyImmediate(vlg);
                    var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(217, 46);
                    grid.spacing = new Vector2(6, 4);
                    foreach (var def in defs)
                    {
                        var group = s.towers.Where((t) => t.def == def).ToList();
                        var uids = group.Select((t) => t.uid).ToList();
                        var d = Towers.TOWERS[def];
                        var lv = EconomySystem.upgradableTowers(sim, uids).Select((t) => EconomySystem.nextUpgradeCost(t) ?? double.PositiveInfinity).ToList();
                        var stc = endless ? group.Where(EconomySystem.fullyBuilt).Select((t) => EconomySystem.starCost(t)).Where(U.IsFinite).ToList() : new List<double>();
                        int needSpec = group.Count((t) => EconomySystem.nextUpgradeCost(t) == null && d.specs.Count == 2 && t.spec == 0 && d.legendary != true);
                        double minStars = group.Min((t) => t.stars ?? 0);
                        double cheapest = double.PositiveInfinity;
                        foreach (var x in lv) cheapest = Math.Min(cheapest, x);
                        foreach (var x in stc) cheapest = Math.Min(cheapest, x);
                        string label = lv.Count > 0 ? $"▲ {HK.fmt(lv.Min())}" : stc.Count > 0 ? $"★ {HK.fmt(stc.Min())}" : needSpec > 0 ? "Spezial." : "✓";
                        var cell = HK.HBox(content, new Color(1, 1, 1, 0.03f), new Color(1, 1, 1, 0.05f), 6, 4, 6);
                        HK.Img(cell.transform, Icons.Model(def), 28);
                        var main = HK.VCol(cell.transform, 0);
                        HK.Grow(main);
                        Bold(main.transform, $"{group.Count}× {d.name}", HK.SMALL);
                        Tiny(main.transform, endless ? $"★{U.Fmt(minStars)}{(needSpec > 0 ? $" · {needSpec} ohne Spez." : "")}" : $"{lv.Count} aufwertbar");
                        var g0 = group;
                        var b = HK.Sm(cell.transform, label, () =>
                        {
                            if (lv.Count == 0 && stc.Count == 0 && needSpec > 0)
                            {
                                var x = g0.First((t) => t.spec == 0);
                                a.selectTower(x.uid);
                                a.lookAt(x.cx + 0.5, x.cy + 0.5);
                                return;
                            }
                            a.upgradeAll(uids);
                        }, lv.Count > 0 ? Ui.BtnKind.Primary : Ui.BtnKind.Normal);
                        if (lv.Count == 0 && stc.Count > 0) HK.Star(b);
                        HK.Tip(b, lv.Count > 0 ? $"Alle {group.Count} {d.name} aufwerten (danach veredeln), solange das Gold reicht" : stc.Count > 0 ? $"Alle {group.Count} {d.name} veredeln, solange das Gold reicht – je Stern {Goldsinks.starText(def)}" : needSpec > 0 ? "Spezialisierung wählen – öffnet die Gruppenauswahl" : "Alles erledigt");
                        if (lv.Count > 0 || stc.Count > 0) live(() => HK.Enable(b, !(cheapest > s.gold)));
                        else HK.Enable(b, needSpec > 0);
                    }
                }
            }

            // Turmliste
            if (list.Count > 0)
            {
                var content = Ui.Scroll(win, out var rscroll, 4, new RectOffset(0, 4, 0, 0));
                Ui.Size(rscroll, -1, -1, -1, 1);
                Ui.Get<LayoutElement>(rscroll.gameObject).minHeight = 80;
                tlRowsScroll = rscroll;
                foreach (var t in list)
                {
                    var d = Towers.TOWERS[t.def];
                    double? next = EconomySystem.nextUpgradeCost(t);
                    bool needSpec = next == null && d.specs.Count == 2 && t.spec == 0 && d.legendary != true;
                    string specName = U.Truthy(t.spec) && d.specs.Count >= t.spec ? d.specs[(int)t.spec - 1].name : "";
                    double tuid = t.uid;
                    var tt = t;
                    var row = HK.HBox(content, new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.35f), v.selectedTower == t.uid ? Theme.Gold : Theme.Line, 6, 4, 6);
                    HK.Img(row.transform, Icons.Model(t.def), 40);
                    var main = HK.VCol(row.transform, 1);
                    HK.Grow(main);
                    var headT = HK.T(main.transform, "", HK.SMALL);
                    var info = Tiny(main.transform, "");
                    live(() =>
                    {
                        sim.c.towerStats.TryGetValue(tt.uid, out var st);
                        TxtSet(headT, $"<b>{d.name}</b><color=#b3a9cc> St. {U.Fmt(tt.level)}{(specName != "" ? $" · {specName}" : "")}{(U.Truthy(tt.stars) ? $" · ★{U.Fmt(tt.stars.Value)}" : "")}{(tt.stunned > 0 ? " · ✷ außer Gefecht" : "")}</color>");
                        TxtSet(info, $"{(st != null ? $"Schaden {HK.fmtNum(HudText.V(st, "damage"), 1)} · Reichweite {HK.fmtNum(HudText.V(st, "range"), 1)} · " : "")}Abschüsse {U.Fmt(tt.kills)} · Gesamtschaden {HK.fmt(tt.damageDealt)}");
                    });
                    var btns = HK.HRow(row.transform, 3);
                    if (next.HasValue)
                    {
                        double nv = next.Value;
                        var up = d.upgrades[(int)t.level - 1];
                        var b = HK.Sm(btns.transform, $"▲ {U.Fmt(nv)}", () => a.upgrade(tuid), Ui.BtnKind.Primary);
                        HK.Tip(b, $"{up.name}: {up.description}");
                        live(() => HK.Enable(b, !(s.gold < nv)));
                    }
                    else if (needSpec)
                    {
                        var b = HK.Sm(btns.transform, "Spezial.", () => { a.selectTower(tuid); a.lookAt(tt.cx + 0.5, tt.cy + 0.5); });
                        HK.Tip(b, "Spezialisierung wählen – öffnet die Turmdetails");
                    }
                    else if (endless && d.attack != "none" && (t.stars ?? 0) < Goldsinks.STARS.max)
                    {
                        double c = EconomySystem.starCost(t);
                        var b = HK.Star(HK.Sm(btns.transform, $"★ {HK.fmt(c)}", () => a.star(tuid)));
                        HK.Tip(b, $"Veredeln ★{U.Fmt((t.stars ?? 0) + 1)}: {Goldsinks.starText(t.def)}");
                        live(() => HK.Enable(b, !(s.gold < c)));
                    }
                    else HK.T(btns.transform, endless ? $"★{U.Fmt(Goldsinks.STARS.max)}" : "max.", HK.TINY, Theme.Muted).horizontalOverflow = HorizontalWrapMode.Overflow;
                    if (d.active != null)
                    {
                        // PORT: TS-Beschriftung „⚡“ – Symbol „bolt“ statt Emoji.
                        var b = HK.B(btns.transform, "", () => a.towerActive(tuid), Ui.BtnKind.Normal, 15, 32, "bolt");
                        b.GetComponent<HudFit>().fixedWidth = 34;
                        string tipTxt = Towers.TOWER_ACTIVES[d.active].name + " auslösen";
                        Ui.Tip(b, () => tipTxt);
                        live(() =>
                        {
                            var act = EconomySystem.activeState(sim, tt);
                            HK.Enable(b, act.available);
                            tipTxt = act.reason ?? $"{Towers.TOWER_ACTIVES[d.active].name} auslösen";
                        });
                    }
                    var ib = HK.Sm(btns.transform, "Info", () => { a.selectTower(tuid); a.lookAt(tt.cx + 0.5, tt.cy + 0.5); });
                    HK.Tip(ib, "Details öffnen und Kamera hinbewegen");
                }
                LayoutRebuilder.ForceRebuildLayoutImmediate(towerWin.rectTransform);
                content.anchoredPosition = new Vector2(content.anchoredPosition.x, prevScroll);
            }
            else
            {
                P(win, s.towers.Count > 0 ? "Alle Türme sind voll ausgebaut." : "Noch keine Türme gebaut.", HK.FS, Theme.Muted);
            }
        }

        // ------------------------------------------------------------ Modale Dialoge

        void updateModal()
        {
            var s = sim.s;
            string want = "";
            if (tutorialActive) want = "tutorial";
            else if (s.phase == "choice" && s.modOffer != null) want = "choice";
            else if (s.paused && !sim.isOver() && !v.miniPaused) want = "pause";
            if (modalKind == "confirm" && want == "pause" && !modalDirty) return;
            if (want == modalKind && !modalDirty) return;
            modalDirty = false;
            modalKind = want;
            HK.Clear(modal.transform);
            HK.Show(modal, want != "");
            if (want == "choice") renderChoice();
            else if (want == "pause") renderPause();
            else if (want == "tutorial") renderTutorial();
        }

        VerticalLayoutGroup Dialog(float width)
        {
            var img = Ui.Panel(modal.transform, "Dialog", new Color(26 / 255f, 20 / 255f, 42 / 255f, 0.96f), true, Theme.Round);
            img.GetComponent<Outline>().effectColor = Theme.LineStrong;
            var col = img.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(22, 22, 18, 20); col.spacing = 8;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = true; col.childForceExpandHeight = false;
            HK.Fit(col, false, true);
            Ui.Place(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(root.rect.width > 100 ? Mathf.Min(width, root.rect.width - 24) : width, 0));
            return col;
        }

        static UText H2(Transform p, string s, TextAnchor a = TextAnchor.MiddleLeft) { return HK.T(p, s, HK.H2, Theme.Text, a, FontStyle.Bold); }

        void renderChoice()
        {
            var s = sim.s;
            var offer = s.modOffer ?? new List<string>();
            var box = Dialog(860).transform;
            H2(box, "Wähle einen Riss-Modifikator", TextAnchor.MiddleCenter);
            P(box, "Gilt für den Rest der Partie.", HK.FS, Theme.Muted).alignment = TextAnchor.MiddleCenter;
            var cards = HK.HRow(box, 11, TextAnchor.UpperCenter, true);
            cards.childForceExpandHeight = true;
            for (int i = 0; i < offer.Count; i++)
            {
                string id = offer[i];
                var m = Modifiers.MODIFIERS[id];
                var card = HK.Box(cards.transform, new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.55f), Theme.LineStrong, 14, 12, 6);
                Ui.Size(card, -1, -1, 1);
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card.GetComponent<Image>();
                var cb = b.colors; cb.highlightedColor = new Color(1.3f, 1.25f, 1.5f); cb.colorMultiplier = 1.5f; b.colors = cb;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                b.onClick.AddListener(() => a.chooseModifier(id));
                string ic = HK.Glyph(m.icon);
                if (ic != "") HK.T(card.transform, ic, 34);
                HK.T(card.transform, m.name, HK.H3, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                P(card.transform, m.description, 16);
                if (m.tags.Contains("tradeoff")) Small(card.transform, "Abwägung", Theme.Warn);
            }
            HK.B(box, $"Überspringen (+{U.Fmt(RogueliteSystem.SKIP_GOLD)} Gold)", () => a.chooseModifier(null));
        }

        void renderPause()
        {
            var s = sim.s;
            var box = Dialog(560).transform;
            H2(box, "Pause");
            P(box, $"{(HudText.MODE_NAMES.TryGetValue(s.mode, out var mn) ? mn : s.mode)} · {s.map.name} · Seed {s.seed.text}", HK.FS, Theme.Muted);
            Tiny(box, $"KERNWACHT Version {Application.version}");
            if (s.modifiers.Count > 0) Small(box, $"Aktive Modifikatoren: {string.Join(", ", s.modifiers.Select((m) => Modifiers.MODIFIERS[m].name))}");
            HK.B(box, "Fortsetzen", () => a.togglePause(), Ui.BtnKind.Primary);
            HK.B(box, "Hilfe & Kodex", () => a.help());
            HK.B(box, "Einstellungen", () => a.settings());
            HK.B(box, "Neustart", () => confirm("Partie mit demselben Seed neu starten?", () => a.restart()));
            HK.B(box, "Hauptmenü", () => confirm(s.phase == "build" ? "Zum Hauptmenü? Die Partie kann später fortgesetzt werden." : "Zum Hauptmenü? Fortgesetzt wird ab der letzten Bauphase.", () => a.quit()));
        }

        void confirm(string text, Action yes)
        {
            modalKind = "confirm";
            HK.Clear(modal.transform);
            var box = Dialog(560).transform;
            P(box, text);
            var row = HK.HRow(box, 8, TextAnchor.MiddleRight);
            HK.B(row.transform, "Ja", () => yes(), Ui.BtnKind.Primary);
            HK.B(row.transform, "Nein", () => { modalKind = ""; modalDirty = true; });
        }

        // ------------------------------------------------------------ Tutorial

        public bool tutorialActive;

        public void startTutorial()
        {
            tutorialActive = true;
            tutorialStep = 0;
            modalDirty = true;
        }

        void renderTutorial()
        {
            bool mazing = sim.s.map.variant == "mazing";
            var steps = new[]
            {
                new[] { "Willkommen, Kernwächter!", "Gegner strömen aus dem violetten Riss und wollen den leuchtenden Kern erreichen. Jeder Gegner, der durchkommt, kostet Kernenergie (♥). Sinkt sie auf 0, ist die Partie verloren." },
                new[] { "Türme bauen", "Wähle unten einen Turm (oder Taste 1–0). Am Desktop klickst du ein freies Feld an. Auf Touchgeräten tippst du ein Feld an, prüfst Vorschau und Reichweite und bestätigst mit „Hier bauen“ oder einem zweiten Tippen." },
                new[] { mazing ? "Mazing: baue ein Labyrinth" : "Classic: der Weg ist fest", mazing
                    ? "Hier gibt es keinen festen Weg. Türme und günstige Barrikaden (B) lenken die Gegner um – die Vorschau zeigt die neue Route. Den Weg vollständig zu blockieren ist nicht möglich. Flieger folgen ebenfalls dem Weg, brauchen aber Türme mit Luftabwehr."
                    : "Gegner folgen dem hellen Weg. Platziere Türme an Kurven, damit sie lange in Reichweite bleiben. Anhöhen geben Reichweite, Leylinien (violetter Ring) Schaden, Goldadern (gelbe Kristalle) mehr Minenertrag." },
                new[] { "Wellen & Gold", "Starte jede Welle mit „▶ Welle“ (Leertaste). Die Leiste oben zeigt, welche Gegner kommen – tippe sie für Details und Konter an. Abschüsse und überstandene Wellen bringen Gold." },
                new[] { "Aufwerten, Fähigkeiten, Fusion", "Tippe einen Turm an: drei Ausbaustufen, danach eine von zwei Spezialisierungen. Ab Stufe 2 haben viele Türme eine aktive Fähigkeit (C). Zwei passende, voll ausgebaute Türme nebeneinander lassen sich zu einem legendären Turm verschmelzen." },
                new[] { "Helden", "Unten links warten deine Helden. Z/X oder Porträt antippen und eine Wegzelle wählen: der Held kämpft dort eine begrenzte Zeit. Fähigkeiten mit A/S/D. Einsätze kosten Rissenergie (⚡), die du durch Abschüsse, Reaktionen und Wellen sammelst. Fällt ein Held, braucht er länger zur Erholung." },
                new[] { "Elemente, Netzwerke, Bosse", "Kombiniere Elemente: Feuer + Kälte = Dampf (nass), Blitz auf nasse Ziele = Überladung, Feuer auf Gift = Giftbrand. Leitpylone vernetzen Türme für Boni und Resonanzen. Bosse kündigen Fähigkeiten an – Resonatoren unterbrechen sie." },
                new[] { "Kamera", "Ziehen verschiebt die Karte, Mausrad oder zwei Finger zoomen, ⟲ ⟳ (Q/E) drehen. Die Minikarte oben rechts springt per Antippen. „?“ öffnet jederzeit Hilfe und Kodex. Viel Erfolg!" },
            };
            int step = Mathf.Clamp(tutorialStep, 0, steps.Length - 1);
            string title = steps[step][0], text = steps[step][1];
            bool last = step == steps.Length - 1;
            var box = Dialog(560).transform;
            Small(box, $"Einführung {step + 1}/{steps.Length}", Theme.Muted);
            H2(box, title);
            P(box, text);
            var row = HK.HRow(box, 8, TextAnchor.MiddleRight);
            HK.B(row.transform, "Überspringen", () => endTutorial());
            HK.B(row.transform, last ? "Los geht’s!" : "Weiter", () =>
            {
                if (last) endTutorial();
                else
                {
                    tutorialStep++;
                    modalDirty = true;
                }
            }, Ui.BtnKind.Primary);
        }

        void endTutorial()
        {
            tutorialActive = false;
            modalDirty = true;
            a.tutorialDone();
        }
    }
}
