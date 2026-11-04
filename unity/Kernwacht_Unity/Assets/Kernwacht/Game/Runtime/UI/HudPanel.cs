using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;
using UText = UnityEngine.UI.Text;

namespace Kernwacht.Game.UI
{
    // Seitenleiste des HUDs (Turmdetails, Bauvorschau, Gegner, Helden, Hindernisse) – Teil von ui/Hud.ts.
    public partial class Hud
    {
        readonly Sig volSig = new Sig();
        /// <summary>Verkaufen-Knopf des Turmpanels (für clickSell).</summary>
        Button sellBtn;
        /// <summary>„Synergien &amp; Konter“ aufgeklappt (details/summary).</summary>
        bool detailsOpen;
        /// <summary>3.1: ausgewählte Rune (Klick = Info), Doppelklick = sockeln bzw. herausnehmen.</summary>
        string runeSel;
        string runeClickKey = "";
        double runeClickT;

        static readonly Color UpgBg = new Color(60 / 255f, 45 / 255f, 95 / 255f, 0.35f);
        static readonly Color SpecBorder = new Color(1f, 216 / 255f, 74 / 255f, 0.45f);
        static readonly Color ResUp = Theme.Hex(0xffb0b0);
        static readonly Color ResDown = Theme.Good;

        // ------------------------------------------------------------ Bausteine

        static UText P(Transform p, string s, int size = HK.FS, Color? c = null) { return HK.T(p, s, size, c); }
        static UText Small(Transform p, string s, Color? c = null) { return HK.T(p, s, HK.SMALL, c); }
        static UText Tiny(Transform p, string s, Color? c = null) { return HK.T(p, s, HK.TINY, c ?? Theme.Muted); }
        static UText Bold(Transform p, string s, int size = HK.FS, Color? c = null) { return HK.T(p, s, size, c, TextAnchor.MiddleLeft, FontStyle.Bold); }

        static void Hint(Transform p, string s)
        {
            var b = HK.Box(p, new Color(96 / 255f, 232 / 255f, 212 / 255f, 0.1f), HK.Accent2, 10, 7);
            HK.T(b.transform, s, 16);
        }

        /// <summary>Kopf (.ph): großes Symbol, Name, Unterzeile, Schließen-Knopf.</summary>
        void Ph(Transform p, Sprite icon, string title, string sub, Action onClose, string glyph = null)
        {
            var row = HK.HRow(p, 10, TextAnchor.MiddleLeft);
            if (icon != null) HK.Img(row.transform, icon, 56);
            else
            {
                var g = HK.T(row.transform, glyph ?? "", 34, Theme.Text, TextAnchor.MiddleCenter);
                Ui.Size(g, 56, 56);
            }
            var col = HK.VCol(row.transform, 1);
            HK.Grow(col);
            HK.T(col.transform, title, HK.H3, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            if (!string.IsNullOrEmpty(sub)) Small(col.transform, sub, Theme.Muted);
            if (onClose != null)
            {
                var c = HK.IconBtn(row.transform, "close", onClose, 36);
                HK.Tip(c, "Schließen (Esc)");
            }
        }

        static HorizontalLayoutGroup Upg(Transform p, Color? border = null, Color? bg = null)
        {
            return HK.HBox(p, bg ?? UpgBg, border ?? Theme.Line, 10, 7, 8);
        }

        static VerticalLayoutGroup UpgText(Transform row, string title, string desc)
        {
            var col = HK.VCol(row, 2);
            HK.Grow(col);
            Bold(col.transform, title, 16);
            if (!string.IsNullOrEmpty(desc)) Small(col.transform, desc);
            return col;
        }

        void closeAction()
        {
            v.infoEnemy = null;
            a.cancel();
            panelSig = "";
        }

        // ------------------------------------------------------------ Seitenleiste

        void updatePanel()
        {
            var s = sim.s;
            TowerInstance t = null;
            if (v.selectedTower != 0) sim.c.towerByUid.TryGetValue(v.selectedTower, out t);
            HeroInstance hero = null;
            if (v.selectedHero != 0) foreach (var x in s.heroes) if (x.uid == v.selectedHero) { hero = x; break; }
            EnemyInstance foe = null;
            if (v.selectedEnemy != 0) sim.c.enemyByUid.TryGetValue(v.selectedEnemy, out foe);
            if (v.selectedEnemy != 0 && (foe == null || !foe.alive)) v.selectedEnemy = 0;
            sig.Reset();
            volSig.Reset();
            if (t != null)
            {
                sim.c.towerStats.TryGetValue(t.uid, out var st);
                var act = EconomySystem.activeState(sim, t);
                bool group = v.groupDef == t.def;
                // Strukturteil (Neuaufbau sofort) – Gold zählt nur als „bezahlbar ja/nein“ der Kosten in diesem Panel
                var costs = new List<double> { EconomySystem.nextUpgradeCost(t) ?? -1, EconomySystem.specCost(t, 1), EconomySystem.specCost(t, 2), EconomySystem.starCost(t) };
                if (group)
                {
                    double m = double.PositiveInfinity;
                    foreach (var o in s.towers) if (o.def == t.def && EconomySystem.fullyBuilt(o)) m = Math.Min(m, EconomySystem.starCost(o));
                    costs.Add(m);
                }
                costs.Add(EconomySystem.respecCost(t, 1));
                costs.Add(EconomySystem.respecCost(t, 2));
                if (group)
                {
                    for (int sp = 1; sp <= 2; sp++)
                    {
                        double m = double.PositiveInfinity;
                        foreach (var o in s.towers) if (o.def == t.def && o.spec != sp) m = Math.Min(m, EconomySystem.specTargetCost(o, sp));
                        costs.Add(m);
                    }
                }
                foreach (var r in Towers.FUSIONS) if (r.a == t.def || r.b == t.def) costs.Add(r.cost);
                // Live-Teil (Neuaufbau höchstens 1× je Sekunde)
                volSig.N(Math.Floor(t.damageDealt / 200)).C('-').N(t.kills).C('-').N(st != null ? U.Round(HudText.V(st, "damage") * 10) : -1).C('-').N(Math.Ceiling(t.activeCd)).C('-').N(Math.Floor(s.gold / 50));
                sig.C('t').N(t.uid).C('-').N(t.stars ?? 0).C('-');
                foreach (var c in costs) sig.C(c >= 0 && c <= s.gold ? '1' : '0');
                sig.C('-').S(sellAllArmed).C('-').S(runeSel).C('-');
                if (s.he?.runes != null) foreach (var r in s.he.runes) sig.N(r.uid).C(':').S(r.def ?? "").C(',');
                sig.C('-');
                if (group) foreach (var o in s.towers) if (o.def == t.def) sig.N(o.level).N(o.spec).N(o.stars ?? 0);
                sig.C('-').S(t.def).C('-').N(t.level).C('-').N(t.spec).C('-').S(t.priority).C('-').S(s.phase).C('-').B(sellArmed == t.uid).C('-').B(t.disrupted > 0).C('-').B(t.stunned > 0).C('-').N(s.network.version).C('-').B(act.available).C('-');
                foreach (var f in EconomySystem.fusionOptions(sim, t)) sig.N(f.partner.uid).C(':').B(f.affordable).C(':').N(f.partner.level).C(':').N(f.partner.spec).C(',');
                sig.C('|').N(s.towers.Count).C('|').N(s.wave).C('-').B(s.paused && v.miniPaused);
            }
            else if (v.selectedObstacle >= 0 && s.he != null)
            {
                sig.C('x').N(v.selectedObstacle).C('-');
                if (s.he.mining != null) sig.N(Math.Ceiling(s.he.mining.t)); else sig.C('n');
                sig.C('-').N(s.map.cells[(int)v.selectedObstacle]).C('-').N(s.wave).C('-').B(s.he.champion.dead > 0);
            }
            else if (foe != null && foe.alive)
            {
                sig.C('n').N(foe.uid).C('-').N(Math.Ceiling(foe.hp)).C('-').N(Math.Ceiling(foe.shield)).C('-').N(foe.phase).C('-');
                foreach (var x in foe.statuses) sig.S(x.id).N(x.stacks).N(Math.Ceiling(x.t)).C(',');
                sig.C('-').B(foe.stealthed).C('-').B(foe.burrowed).C('-').N(U.Round(ElementSystem.slowFactor(foe) * 20));
            }
            else if (hero != null)
            {
                var st = HeroSystem.heroStatsOf(sim, hero);
                sig.C('h').N(hero.uid).C('-').S(hero.state).C('-').N(hero.level).C('-').N(Math.Floor(hero.xp)).C('-').B(hero.pendingPerk).C('-');
                foreach (var pk in hero.perks) sig.N(pk);
                sig.C('-').S(hero.mode).C('-').N(hero.kills).C('-').N(U.Round(st.damage)).C('-');
                foreach (var d in HeroSystem.activeDuos(sim)) sig.S(d).C(',');
            }
            else if (!string.IsNullOrEmpty(v.placingHero))
            {
                sig.C('p').S(v.placingHero).C('-').B(s.riftEnergy >= Heroes.HEROES[v.placingHero].cost);
            }
            else if (!string.IsNullOrEmpty(v.buildDef))
            {
                double bs = v.buildSpec ?? 0;
                double cost = U.Truthy(bs) ? EconomySystem.prespecCost(sim, v.buildDef, bs) : EconomySystem.towerCost(sim, v.buildDef);
                sig.C('b').S(v.buildDef).C('-').N(bs).C('-');
                if (v.pendingCell != null) sig.N(v.pendingCell.x).C(',').N(v.pendingCell.y);
                sig.C('-').S(v.pendingCheck == null ? "u" : v.pendingCheck.ok ? "t" : "f").C('-').S(v.pendingCheck?.reason).C('-').B(s.gold >= cost).C('-').B(v.touchMode);
            }
            else if (!string.IsNullOrEmpty(v.infoEnemy))
            {
                sig.C('e').S(v.infoEnemy).C('-').S(bossInfo);
            }
            else if (!string.IsNullOrEmpty(hoverInfo))
            {
                sig.C('o').S(hoverInfo);
            }
            else
            {
                sig.C('i').S(s.phase).C('-').N(s.wave).C('-');
                foreach (var x in s.heroes) sig.S(x.state).C(',');
            }
            // Während eine Maustaste/ein Finger im Panel gedrückt ist, nie neu aufbauen (sonst geht der Klick verloren).
            // Reine Live-Werte (Schaden, Abschüsse, Abklingzeit) bauen höchstens einmal pro Sekunde neu auf.
            double now = HK.Now();
            if (sig.Same(panelSig) && (volSig.Same(panelVol) || now - panelBuiltAt < 1000)) return;
            if (pointerHeld && panelSig != "") return;
            string newSig = sig.ToString();
            // Beim Neuaufbau desselben Inhalts die Scrollposition behalten (sonst springt das Panel nach oben)
            bool sameSubject = Head(newSig) == Head(panelSig);
            float keepScroll = sameSubject ? panelBody.anchoredPosition.y : 0;
            panelSig = newSig;
            panelVol = volSig.ToString();
            panelBuiltAt = now;
            HK.Clear(panelBody);
            sellBtn = null;
            if (t != null) renderTower(t);
            else if (v.selectedObstacle >= 0 && s.he != null) renderObstacle(v.selectedObstacle);
            else if (foe != null) renderEnemyInstance(foe);
            else if (hero != null) renderHero(hero);
            else if (!string.IsNullOrEmpty(v.placingHero)) renderPlacing(v.placingHero);
            else if (!string.IsNullOrEmpty(v.buildDef)) renderBuild(v.buildDef);
            else if (!string.IsNullOrEmpty(v.infoEnemy)) renderEnemy(v.infoEnemy);
            else if (!string.IsNullOrEmpty(hoverInfo)) renderBuild(hoverInfo, true);
            else renderIdle();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelBody);
            panelScroll.StopMovement();
            float maxY = Mathf.Max(0, panelBody.rect.height - ((RectTransform)panelScroll.viewport).rect.height);
            panelBody.anchoredPosition = new Vector2(panelBody.anchoredPosition.x, Mathf.Clamp(keepScroll, 0, maxY));
        }

        static string Head(string s)
        {
            if (s == null) return "";
            int i = s.IndexOf('-');
            return i < 0 ? s : s.Substring(0, i);
        }

        void renderIdle()
        {
            var s = sim.s;
            var body = panelBody;
            HK.T(body, HudText.MODE_NAMES.TryGetValue(s.mode, out var mode) ? mode : s.mode, HK.H3, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            P(body, $"{s.map.name} · Seed {s.seed.text}", HK.FS, Theme.Muted);
            // Aufgeräumt: Bedienhinweise stehen im Hinweisbalken unten und unter Hilfe – hier nur, was diese Partie betrifft
            if (s.he == null)
                P(body, s.phase == "build"
                    ? "Bauphase: Wähle unten einen Turm und platziere ihn. Starte die Welle, wenn du bereit bist."
                    : "Kampf läuft. Tippe Türme an, um sie aufzuwerten, Fähigkeiten auszulösen oder ihre Zielwahl zu ändern.");
            if (s.heroes.Count > 0) Small(body, $"Helden: {string.Join(" & ", s.heroes.Select((x) => Heroes.HEROES[x.id].name))}. Mit Z/X (oder Porträt antippen) auf dem Weg einsetzen – das kostet Rissenergie, die du durch Abschüsse, Reaktionen und Wellen sammelst.");
            Small(body, "Tipp: Turm, Gegner oder Hindernis anklicken zeigt hier Werte und Aktionen.", Theme.Muted);
            int sp = WorldGenerator.spawnsOf(s.map).Count;
            if (sp > 1) Small(body, $"⚠ Weggabelung: Gegner kommen aus {sp} Rissen.", Theme.Warn);
        }

        void renderBuild(string id, bool hover = false)
        {
            var d = Towers.TOWERS[id];
            double spec = !hover && v.buildDef == id ? (v.buildSpec ?? 0) : 0;
            double cost = U.Truthy(spec) ? EconomySystem.prespecCost(sim, id, spec) : EconomySystem.towerCost(sim, id);
            var st = HudText.baseStatsOf(id);
            var body = panelBody;
            // PORT: Symbole zeigen das Modell ohne Stufen-/Spezialisierungsvariante (Icons.Model kennt nur Modell-IDs).
            string sub = U.Truthy(spec)
                ? $"★ {(d.specs.Count >= spec ? d.specs[(int)spec - 1].name : "")} · Höchststufe · {HK.fmt(cost)} Gold"
                : $"{d.role}{(d.damageType != null ? $" · {HudText.Dmg(d.damageType)}" : "")} · {U.Fmt(cost)} Gold";
            Ph(body, Icons.Model(id), d.name, sub, hover ? (Action)null : closeAction);
            if (!hover)
            {
                var chk = v.pendingCheck;
                if (v.touchMode)
                {
                    if (v.pendingCell != null && chk != null)
                    {
                        if (chk.ok) HK.B(body, $"✔ Hier bauen ({U.Fmt(cost)} Gold)", () => a.confirmBuild(), Ui.BtnKind.Primary);
                        else P(body, $"✖ {chk.reason ?? "Nicht möglich"}", HK.FS, Theme.Bad);
                        if (chk.ok) Small(body, "Tipp: dasselbe Feld erneut antippen baut ebenfalls.", Theme.Muted);
                    }
                    else
                    {
                        Hint(body, "Tippe ein freies Feld an, um die Platzierung zu prüfen.");
                    }
                }
                else
                {
                    Hint(body, "Klicke auf ein freies Feld. Umschalt+Klick baut mehrere. Rechtsklick oder Esc bricht ab.");
                    if (chk != null && !chk.ok && v.pendingCell != null) P(body, $"✖ {chk.reason ?? ""}", HK.FS, Theme.Bad);
                }
                if (sim.s.gold < cost) P(body, $"Es fehlen {U.Fmt(cost - sim.s.gold)} Gold.", HK.FS, Theme.Bad);
            }
            // Vorschau (Endausbau)
            var prev = HK.HRow(body, 6, TextAnchor.MiddleCenter);
            HK.Img(prev.transform, Icons.Model(id), 110);
            Tiny(body, "Endausbau: Spezialisierung A (drehend) und B").alignment = TextAnchor.MiddleCenter;
            P(body, d.description);
            statTable(body, HudText.statLines(st, d));
            if (d.active != null)
            {
                var ta = Towers.TOWER_ACTIVES[d.active];
                Small(body, $"<b>Aktiv ab Stufe {U.Fmt(Towers.ACTIVE_MIN_LEVEL)}: {ta.name}</b> – {ta.description}");
            }
            fieldHint(body, id, hover ? null : v.pendingCell);
            fusionHints(body, id, hover ? null : v.pendingCell);
            details(body, d);
            if (!hover) HK.B(body, "Abbrechen", () => a.cancel());
        }

        /// <summary>Spezialfelder unter dem gewählten Feld (Goldvorkommen, Kristallader, Leylinie).</summary>
        void fieldHint(Transform body, string id, GridPos cell)
        {
            var m = sim.s.map;
            var lines = new List<string>();
            if (cell != null)
            {
                var cells = Towers.footprintCells(id, cell.x, cell.y, m.width).Where((k) => k >= 0 && k < m.cells.Count).ToList();
                double i = cell.y * m.width + cell.x;
                double sz = Towers.towerSize(id);
                if (sz > 1) lines.Add($"▦ Belegt {U.Fmt(sz)}×{U.Fmt(sz)} Felder (ab dem gewählten Feld nach rechts/unten)");
                if (cells.Any((k) => m.veins.Contains(k))) lines.Add(id == "goldmine" ? $"● Goldvorkommen: +50 % Ertrag und zusätzlich +{U.Fmt(EconomySystem.veinBonus(sim.s.wave + 1))} Gold je Welle" : "● Goldvorkommen – ideal für einen Goldgräber (zusätzlicher Goldabbau)");
                if (cells.Any((k) => m.deposits != null && m.deposits.Contains(k))) lines.Add(id == "goldmine" ? $"◆ Kristallader: Goldgräber fördert zusätzlich {U.Fmt(1 + Math.Floor((sim.s.wave + 1) / 8))}+ Kristall je Welle" : "◆ Kristallader – ein Goldgräber fördert hier zusätzlich Kristall");
                if (m.ley.Contains(i)) lines.Add("✦ Leylinie: +15 % Schaden für Türme auf diesem Feld");
            }
            else if (id == "goldmine")
            {
                lines.Add($"Belegt 2×2 Felder. Spezialfelder: Goldvorkommen (gelbe Kristalle) geben +50 % Ertrag und zusätzlich Gold je Welle{(m.deposits != null && m.deposits.Count > 0 ? ", Kristalladern (blau) zusätzlich Kristall" : "")}.");
            }
            if (lines.Count == 0) return;
            var box = HK.Box(body, new Color(1f, 216 / 255f, 74 / 255f, 0.06f), SpecBorder, 10, 7);
            foreach (var l in lines) Small(box.transform, l);
        }

        /// <summary>Fusionsmöglichkeiten schon beim Platzieren: Rezept, Partner in Reichweite des gewählten Feldes.</summary>
        void fusionHints(Transform body, string id, GridPos cell)
        {
            var s = sim.s;
            List<FusionPreviewEntry> recipes = cell != null
                ? EconomySystem.fusionPreview(sim, id, cell.x, cell.y)
                : Towers.FUSIONS.Where((r) => r.a == id || r.b == id).Select((r) => new FusionPreviewEntry { recipe = r, partnerDef = r.a == id ? r.b : r.a }).ToList();
            if (recipes.Count == 0) return;
            var box = HK.Box(body, new Color(1f, 216 / 255f, 74 / 255f, 0.06f), SpecBorder, 10, 7);
            Bold(box.transform, "✦ Fusionen mit diesem Turm", 16);
            foreach (var f in recipes)
            {
                var ld = Towers.TOWERS[f.recipe.id];
                var pd = Towers.TOWERS[f.partnerDef];
                bool locked = s.he != null && !PassiveTree.towerUnlocked(f.partnerDef, s.he, s.wave);
                string status = "";
                if (cell != null) status = f.partners.Count > 0 ? $" – ✔ {f.partners.Count}× {pd.name} in Reichweite (gold markiert)" : $" – kein {pd.name} im Umkreis von {HK.fmtNum(Towers.FUSION_RANGE, 1)} Feldern";
                var row = HK.HRow(box.transform, 4, TextAnchor.UpperLeft);
                HK.Img(row.transform, Icons.Model(f.recipe.id), 20);
                Color c = cell != null ? (f.partners.Count > 0 ? Theme.Good : Theme.Hex(0xffb0a0)) : Theme.Text;
                var tt = HK.T(row.transform, $"{pd.name} → <b>{ld.name}</b> ({U.Fmt(f.recipe.cost)} Gold){(locked ? $" · {pd.name} ab Welle {U.Fmt(PassiveTree.towerUnlockWave(f.partnerDef, s.he))}" : "")}{status}", 14, c);
                HK.Grow(tt);
            }
            Tiny(box.transform, $"Beide Türme brauchen Stufe 4 und eine Spezialisierung, höchstens {HK.fmtNum(Towers.FUSION_RANGE, 1)} Felder Abstand.");
        }

        static void statTable(Transform body, List<string[]> rows)
        {
            var col = HK.VCol(body, 1);
            col.padding = new RectOffset(0, 0, 4, 4);
            foreach (var r in rows)
            {
                var row = HK.HRow(col.transform, 8, TextAnchor.UpperLeft);
                var k = HK.T(row.transform, r[0], HK.SMALL, Theme.Muted);
                HK.Grow(k);
                var val = HK.T(row.transform, r[1], HK.SMALL, Theme.Text, TextAnchor.UpperRight);
                HK.Grow(val);
            }
        }

        /// <summary>details/summary „Synergien &amp; Konter“ – aufgeklappt bleibt aufgeklappt.</summary>
        void details(Transform body, TowerDefinition d)
        {
            var b = HK.Sm(body, $"{(detailsOpen ? "▼" : "►")} Synergien & Konter", null, Ui.BtnKind.Ghost);
            HK.Label(b).alignment = TextAnchor.MiddleLeft;
            b.onClick.AddListener(() => { detailsOpen = !detailsOpen; panelSig = ""; });
            if (!detailsOpen) return;
            Small(body, $"✚ {d.synergies}");
            Small(body, $"✖ {d.counters}");
            matchupLines(body, d.damageType);
        }

        /// <summary>„Alle verkaufen“ mit zweitem Klick zur Bestätigung (innerhalb von 4 s).</summary>
        Button sellAllButton(Transform p, string key, List<double> uids, string label)
        {
            var s = sim.s;
            var sv = EconomySystem.sellAllValue(sim, uids);
            bool miniP = s.paused && v.miniPaused;
            bool armed = sellAllArmed == key && HK.Now() - sellAllArmedAt < 4000;
            var b = HK.Sm(p, armed ? $"Wirklich? +{U.Fmt(sv.gold)} Gold" : $"{label} (+{U.Fmt(sv.gold)})", () =>
            {
                if (sellAllArmed == key && HK.Now() - sellAllArmedAt < 4000)
                {
                    sellAllArmed = "";
                    a.sellAll(uids);
                }
                else
                {
                    sellAllArmed = key;
                    sellAllArmedAt = HK.Now();
                }
                towerWinSig = "";
                panelSig = "";
            }, Ui.BtnKind.Danger);
            if (armed) b.GetComponent<Outline>().effectColor = Theme.Gold;
            HK.Enable(b, !(sv.count == 0 || miniP));
            HK.Tip(b, miniP ? "In der Mini-Pause kann nicht verkauft werden" : $"{U.Fmt(sv.count)} Türme verkaufen für +{U.Fmt(sv.gold)} Gold (zweimal klicken)");
            return b;
        }

        /// <summary>Klick zeigt die Wirkung; zweiter Klick auf dieselbe Rune innerhalb von 450 ms führt die Aktion aus (robust auch bei Neuaufbau).</summary>
        void runeTap(string key, Action act)
        {
            double now = HK.Now();
            bool dbl = runeClickKey == key && now - runeClickT < 450;
            if (dbl) { runeClickKey = ""; runeClickT = 0; } else { runeClickKey = key; runeClickT = now; }
            if (dbl)
            {
                runeSel = null;
                act();
            }
            else
            {
                runeSel = key;
            }
            forcePanelRefresh();
        }

        Button RuneBtn(Transform p, RuneDef rd, double tier, bool sel, string count, string title, Action onClick)
        {
            var c = HK.ColorOf(rd.color, Theme.Muted);
            var img = Ui.Panel(p, "Rune", new Color(0, 0, 0, 0.3f), true, Theme.Circle);
            var o = img.GetComponent<Outline>();
            o.effectColor = sel ? HK.StarGold : c;
            o.effectDistance = new Vector2(2, -2);
            Ui.Size(img, 40, 40);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            b.onClick.AddListener(() => onClick());
            // PORT: Runenzeichen (ᚦ ᚱ …) fehlen in der UI-Schrift – Anfangsbuchstabe des Namens in Runenfarbe.
            string g = HK.Glyph(rd.icon);
            var ic = HK.T(img.transform, g != "" ? g : rd.name.Substring(0, 1), 18, c, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Fill(ic.rectTransform);
            var tl = HK.T(img.transform, U.Repeat("I", tier), 10, HK.StarGold, TextAnchor.LowerRight, FontStyle.Bold);
            Ui.Place(tl.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(2, -2), new Vector2(24, 12));
            if (!string.IsNullOrEmpty(count))
            {
                var n = HK.T(img.transform, count, 10, Color.white, TextAnchor.UpperRight, FontStyle.Bold);
                Ui.Place(n.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(6, 4), new Vector2(26, 12));
            }
            HK.Tip(img, title);
            return b;
        }

        void renderRuneSockets(Transform body, TowerInstance t)
        {
            var s = sim.s;
            if (!Runes.runeEligible(t.def)) return;
            double cap = Runes.runeSockets(t.def, s.wave);
            var inT = RuneSystem.towerRunes(sim, t);
            int sortCount = s.towers.Count((o) => o.def == t.def);
            double? nextW = Runes.nextRuneSlotWave(s.wave);
            var bag = RuneSystem.bagRunes(sim);
            if (cap == 0 && bag.Count == 0 && s.wave < Runes.RUNE_RULES.splinterFrom) return;
            string tname = Towers.TOWERS[t.def].name;
            bool legendary = Towers.TOWERS[t.def].legendary == true;
            var box = HK.Box(body, new Color(192 / 255f, 160 / 255f, 1f, 0.06f), new Color(192 / 255f, 160 / 255f, 1f, 0.5f), 10, 7, 5);
            var bt = box.transform;
            Bold(bt, $"Runen der Sorte {tname} {inT.Count}/{U.Fmt(cap)}", 16);
            Tiny(bt, $"Gilt für alle {sortCount} {tname}{(legendary ? " (Fusion)" : "")}. Wechsel wirkt sofort auf alle.{(nextW.HasValue && U.Truthy(nextW.Value) ? $" Nächster Sockel ab Welle {U.Fmt(nextW.Value)}." : "")}{(legendary ? " Legendär: +1 Sockel." : "")}");
            Action infoAct = null;
            string infoRune = null; double infoTier = 0; bool infoIn = false; string infoExtra = "";
            var slots = HK.Flow(bt, 5);
            for (int i = 0; i < cap; i++)
            {
                if (i >= inT.Count)
                {
                    var e = Ui.Panel(slots.transform, "Empty", new Color(0, 0, 0, 0.2f), true, Theme.Circle);
                    e.GetComponent<Outline>().effectColor = new Color(1, 1, 1, 0.25f);
                    Ui.Size(e, 40, 40);
                    var pl = HK.T(e.transform, "+", 18, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleCenter);
                    Ui.Fill(pl.rectTransform);
                    continue;
                }
                var r = inT[i];
                string key = $"s:{U.Fmt(r.uid)}";
                double ruid = r.uid;
                Action act = () => a.unsocketRune(ruid);
                if (runeSel == key) { infoAct = act; infoRune = r.rune; infoTier = r.tier; infoIn = true; infoExtra = ""; }
                RuneBtn(slots.transform, Runes.RUNES[r.rune], r.tier, runeSel == key, null, $"{Runes.runeLabel(r.rune, r.tier)} – Klick: Wirkung · Doppelklick: herausnehmen", () => runeTap(key, act));
            }
            if (bag.Count > 0)
            {
                // Beutel: gleiche Runen gleicher Qualität zusammenfassen
                var groups = new Dictionary<string, List<RuneInstance>>();
                var order = new List<string>();
                foreach (var r in bag)
                {
                    string k = $"{r.rune}:{U.Fmt(r.tier)}";
                    if (!groups.TryGetValue(k, out var l)) { l = new List<RuneInstance>(); groups[k] = l; order.Add(k); }
                    l.Add(r);
                }
                var gl = order.Select((k) => groups[k]).ToList();
                U.StableSort(gl, (x, y) => { double dt = y[0].tier - x[0].tier; return dt != 0 ? dt : U.IndexOf(Runes.RUNE_ORDER, x[0].rune) - U.IndexOf(Runes.RUNE_ORDER, y[0].rune); });
                Tiny(bt, inT.Count < cap ? "Beutel – Klick: Wirkung · Doppelklick: sockeln" : "Beutel – Klick: Wirkung (alle Sockel belegt)");
                var bagFlow = HK.Flow(bt, 5);
                foreach (var g in gl)
                {
                    var r = g[0];
                    string key = $"b:{r.rune}:{U.Fmt(r.tier)}";
                    double tuid = t.uid, ruid = r.uid;
                    Action act = () => a.socketRune(tuid, ruid);
                    if (runeSel == key) { infoAct = act; infoRune = r.rune; infoTier = r.tier; infoIn = false; infoExtra = g.Count > 1 ? $"×{g.Count}" : ""; }
                    RuneBtn(bagFlow.transform, Runes.RUNES[r.rune], r.tier, runeSel == key, g.Count > 1 ? $"×{g.Count}" : null, $"{Runes.runeLabel(r.rune, r.tier)} – Klick: Wirkung · Doppelklick: sockeln", () => runeTap(key, act));
                }
            }
            else if (s.wave < Runes.RUNE_RULES.splinterFrom)
            {
                Tiny(bt, $"Runen sind nur findbar – Splitter ab Welle {U.Fmt(Runes.RUNE_RULES.splinterFrom)}, volle Runen ab Welle {U.Fmt(Runes.RUNE_RULES.fromWave)}.");
            }
            if (infoRune != null)
            {
                var rd = Runes.RUNES[infoRune];
                var rc = HK.ColorOf(rd.color, HK.StarGold);
                var ib = HK.Box(bt, new Color(1, 1, 1, 0.05f), rc, 9, 6, 4);
                Bold(ib.transform, $"{HK.Rc(Runes.runeLabel(infoRune, infoTier), rc)}{(infoExtra != "" ? $" <color=#b3a9cc>{infoExtra}</color>" : "")}", 15);
                Small(ib.transform, Runes.runeEffectText(infoRune, infoTier));
                var row = HK.HRow(ib.transform, 6);
                HK.Grow(Tiny(row.transform, infoIn ? "Doppelklick: zurück in den Beutel" : "Doppelklick: in diese Turmsorte sockeln"));
                var fa = infoAct;
                var btn = HK.Sm(row.transform, infoIn ? "Herausnehmen" : "Sockeln", () => { runeSel = null; fa(); forcePanelRefresh(); });
                HK.Enable(btn, infoIn || inT.Count < cap);
            }
        }

        /// <summary>3.6: Die wichtigste nächste Aktion direkt unter dem Kopf – ohne Scrollen.</summary>
        void quickActions(Transform body, TowerInstance t)
        {
            var s = sim.s;
            var d = Towers.TOWERS[t.def];
            double? next = EconomySystem.nextUpgradeCost(t);
            double tuid = t.uid;
            if (next.HasValue)
            {
                var up = d.upgrades[(int)t.level - 1];
                var b = HK.B(body, $"▲ Stufe {U.Fmt(t.level + 1)}: {up.name} · {HK.fmt(next.Value)} Gold", () => a.upgrade(tuid), Ui.BtnKind.Primary);
                HK.Enable(b, s.gold >= next.Value);
                HK.Tip(b, up.description);
                return;
            }
            if (d.specs.Count == 2 && t.spec == 0 && d.legendary != true)
            {
                var row = HK.HRow(body, 4, TextAnchor.MiddleLeft, true);
                for (int i = 0; i < 2; i++)
                {
                    var sp = d.specs[i];
                    double c = EconomySystem.specCost(t, i + 1);
                    int spn = i + 1;
                    var b = HK.B(row.transform, $"{(i == 0 ? "(A)" : "(B)")} {sp.name} · {HK.fmt(c)}", () => a.specialize(tuid, spn), Ui.BtnKind.Primary, 14);
                    Ui.Size(b, -1, -1, 1);
                    HK.Enable(b, s.gold >= c);
                    HK.Tip(b, sp.description);
                }
                return;
            }
            if (!s.rules.endless || d.attack == "none") return;
            double n = t.stars ?? 0;
            if (n >= Goldsinks.STARS.max)
            {
                Small(body, $"★{U.Fmt(Goldsinks.STARS.max)} – höchste Veredelung erreicht", HK.StarGold);
                return;
            }
            double sc = EconomySystem.starCost(t);
            var sb = HK.Star(HK.B(body, $"★ Veredeln ★{U.Fmt(n)} → ★{U.Fmt(n + 1)} · {HK.fmt(sc)} Gold", () => a.star(tuid), Ui.BtnKind.Primary));
            HK.Enable(sb, s.gold >= sc);
            HK.Tip(sb, $"Veredeln: {Goldsinks.starText(t.def)} je Stern (bis ★{U.Fmt(Goldsinks.STARS.max)})");
            Tiny(body, $"Je Stern: {Goldsinks.starText(t.def)}");
        }

        void renderTower(TowerInstance t)
        {
            var s = sim.s;
            var d = Towers.TOWERS[t.def];
            sim.c.towerStats.TryGetValue(t.uid, out var st);
            var body = panelBody;
            double tuid = t.uid;
            string specName = U.Truthy(t.spec) && d.specs.Count >= t.spec ? d.specs[(int)t.spec - 1].name : null;
            string title = d.name + (d.legendary == true ? "  <size=12><color=#ffd84a>LEGENDÄR</color></size>" : "");
            string sub = (d.upgrades.Count > 0 ? $"Stufe {U.Fmt(t.level)}/4{(specName != null ? $" · {specName}" : "")}" : d.role) + (U.Truthy(t.stars) ? $" <color=#ffd36b><b>★{U.Fmt(t.stars.Value)}</b></color>" : "");
            Ph(body, Icons.Model(t.def), title, sub, closeAction);
            quickActions(body, t);
            if (v.groupDef == t.def)
            {
                var group = s.towers.Where((o) => o.def == t.def).ToList();
                var uids = group.Select((o) => o.uid).ToList();
                var open = EconomySystem.upgradableTowers(sim, uids);
                var cost = EconomySystem.upgradeAllCost(sim, uids);
                double cheapest = double.PositiveInfinity;
                foreach (var o in open) cheapest = Math.Min(cheapest, EconomySystem.nextUpgradeCost(o) ?? double.PositiveInfinity);
                var gb = HK.Box(body, new Color(217 / 255f, 180 / 255f, 90 / 255f, 0.08f), Theme.Hex(0xd9b45a), 10, 7, 5);
                var g = gb.transform;
                Bold(g, $"▦ {group.Count}× {d.name} ausgewählt", 16);
                Tiny(g, open.Count > 0 ? $"{open.Count} davon noch aufwertbar · komplett {HK.fmt(cost.total)} Gold" : "Alle auf der höchsten Stufe");
                var row = HK.Flow(g, 4);
                var gu = HK.Sm(row.transform, "▲ Alle aufwerten", () => a.upgradeAll(uids), Ui.BtnKind.Primary);
                HK.Enable(gu, !(open.Count == 0 || cheapest > s.gold));
                HK.Tip(gu, "Alle Türme dieser Sorte aufwerten, solange das Gold reicht (ohne Spezialisierung)");
                HK.Sm(row.transform, "Nur diesen", () => a.clearGroup());
                sellAllButton(row.transform, $"g:{t.def}", uids, "Alle verkaufen");
                if (s.rules.endless)
                {
                    var st2 = group.Select((o) => new { o, c = EconomySystem.starCost(o) }).Where((x) => EconomySystem.fullyBuilt(x.o) && U.IsFinite(x.c)).ToList();
                    double total = st2.Sum((x) => x.c);
                    double ch = st2.Count > 0 ? st2.Min((x) => x.c) : double.PositiveInfinity;
                    var sb = HK.Star(HK.Sm(g, $"★ Alle veredeln{(st2.Count > 0 ? $" ({HK.fmt(total)})" : "")}", () => a.starAll(uids)));
                    HK.Enable(sb, !(st2.Count == 0 || ch > s.gold));
                    HK.Tip(sb, st2.Count > 0 ? $"Alle voll ausgebauten {d.name} um einen Stern veredeln (zusammen {HK.fmt(total)} Gold) – bei zu wenig Gold so viele wie möglich. Je Stern: {Goldsinks.starText(t.def)}." : "Erst voll ausbauen (Stufe 4 und Spezialisierung)");
                }
                if (d.specs.Count == 2 && d.legendary != true)
                {
                    var sr = HK.Flow(g, 4);
                    for (int sp = 1; sp <= 2; sp++)
                    {
                        int spn = sp;
                        var need = group.Where((o) => o.spec != spn).ToList();
                        double total = need.Sum((o) => EconomySystem.specTargetCost(o, spn));
                        double cheapestSp = need.Count > 0 ? need.Min((o) => EconomySystem.specTargetCost(o, spn)) : double.PositiveInfinity;
                        var b = HK.Sm(sr.transform, $"{(sp == 1 ? "(A)" : "(B)")} Alle: {d.specs[sp - 1].name}{(need.Count > 0 ? $" ({HK.fmt(total)})" : " ✓")}", () => a.groupSpec(uids, spn), need.Count > 0 ? Ui.BtnKind.Normal : Ui.BtnKind.On);
                        HK.Enable(b, !(need.Count == 0 || cheapestSp > s.gold));
                        HK.Tip(b, need.Count > 0 ? $"Alle {group.Count} auf Höchststufe bringen und auf „{d.specs[sp - 1].name}“ stellen (Wechsel {U.Fmt(U.Round(EconomySystem.RESPEC_MUL * 100))} % des Preises). Gesamt {HK.fmt(total)} Gold – bei zu wenig Gold so viele wie möglich." : "Alle bereits so spezialisiert");
                    }
                }
            }
            if (s.rules.endless && d.attack != "economy" && d.attack != "none")
            {
                double lim = EconomySystem.currentTowerLimit(sim);
                double n = EconomySystem.countedTowers(sim);
                double fb = Scaling.focusBonus(n, lim);
                double per = U.Round(Scaling.TOWER_LIMIT.focusPer * 100);
                if (U.IsFinite(lim))
                {
                    var fl = Small(body, fb > 0
                        ? $"✦ Fokusbonus: +{U.Fmt(U.Round(fb * 100))} % Schaden, weil {U.Fmt(lim - n)} Bauplätze frei sind (je freier Platz +{U.Fmt(per)} %)."
                        : $"Fokusbonus: 0 % – alle {U.Fmt(lim)} Bauplätze belegt. Freie Plätze geben je +{U.Fmt(per)} % Schaden.");
                    fl.raycastTarget = true;
                    HK.Tip(fl, "Jeder freie Bauplatz macht alle Türme stärker. Klick auf das Bauplatz-Symbol in der Turmleiste erklärt das genauer.");
                }
            }
            if (s.he != null) renderRuneSockets(body, t);
            if (s.he?.mastery != null && d.attack != "economy" && d.attack != "none")
            {
                double k = s.he.mastery.TryGetValue(t.def, out var kv) ? kv : 0;
                double r = Mastery.masteryRank(k);
                double? nx = Mastery.masteryNext(k);
                Small(body, $"Meisterschaft {d.name}: Rang {U.Fmt(r)}/{U.Fmt(Mastery.MASTERY.maxRank)} (+{U.Fmt(r)} % Schaden) · {HK.fmt(k)}{(nx.HasValue && U.Truthy(nx.Value) ? $" / {HK.fmt(nx.Value)}" : "")} Abschüsse", HK.StarGold);
            }
            if (t.stunned > 0) P(body, $"✷ Außer Gefecht ({HK.fmtNum(t.stunned)} s)", HK.FS, Theme.Bad);
            if (t.disrupted > 0) P(body, $"✖ Netzwerk gestört ({HK.fmtNum(t.disrupted)} s)", HK.FS, Theme.Bad);
            // Fusion: für jedes passende Rezept direkt zeigen, was noch fehlt
            var fus = EconomySystem.fusionOptions(sim, t);
            foreach (var r in Towers.FUSIONS)
            {
                if (r.a != t.def && r.b != t.def) continue;
                var ld = Towers.TOWERS[r.id];
                string otherDef = r.a == t.def ? r.b : r.a;
                var near = fus.FirstOrDefault((f) => f.recipe.id == r.id)?.partner;
                var others = s.towers.Where((o) => o != t && o.def == otherDef).ToList();
                bool selfOk = t.level >= 4 && t.spec != 0;
                bool partnerOk = near != null && near.level >= 4 && near.spec != 0;
                bool goldOk = s.gold >= r.cost;
                string ok(bool yes) => yes ? "✔" : "✖";
                string on = Towers.TOWERS[otherDef].name;
                string range = HK.fmtNum(Towers.FUSION_RANGE, 1);
                string partnerText;
                if (near != null) partnerText = $"{on} in der Nähe (gold markiert) – {(near.level >= 4 ? "Stufe 4 ✓" : $"Stufe {U.Fmt(near.level)}/4")}{(U.Truthy(near.spec) ? ", spezialisiert ✓" : ", Spezialisierung fehlt")}";
                else if (others.Count > 0) partnerText = $"{on} steht zu weit weg – höchstens {range} Felder Abstand nötig";
                else if (s.he != null && !PassiveTree.towerUnlocked(otherDef, s.he, s.wave)) partnerText = $"{on} bauen (wird ab Welle {U.Fmt(PassiveTree.towerUnlockWave(otherDef, s.he))} freigeschaltet), höchstens {range} Felder entfernt";
                else partnerText = $"{on} in höchstens {range} Feldern Abstand bauen";
                bool ready = selfOk && partnerOk && goldOk;
                var box = HK.Box(body, new Color(1f, 216 / 255f, 74 / 255f, ready ? 0.16f : 0.1f), new Color(1f, 216 / 255f, 74 / 255f, ready ? 1f : 0.7f), 10, 7, 4);
                var head = HK.HRow(box.transform, 8, TextAnchor.UpperLeft);
                HK.Img(head.transform, Icons.Model(r.id), 44);
                var col = HK.VCol(head.transform, 2);
                HK.Grow(col);
                Bold(col.transform, $"✦ Fusion: {ld.name}", 16);
                Tiny(col.transform, ld.description);
                var bad = Theme.Hex(0xffb0a0);
                HK.T(col.transform, $"{ok(selfOk)} Dieser Turm: {(t.level >= 4 ? "Stufe 4 ✓" : $"Stufe {U.Fmt(t.level)}/4 – noch {U.Fmt(4 - t.level)}× aufwerten")}{(t.level >= 4 ? (U.Truthy(t.spec) ? ", spezialisiert ✓" : ", Spezialisierung fehlt") : ", danach spezialisieren")}", 14, selfOk ? Theme.Good : bad);
                HK.T(col.transform, $"{ok(partnerOk)} Partner: {partnerText}", 14, partnerOk ? Theme.Good : bad);
                HK.T(col.transform, $"{ok(goldOk)} {U.Fmt(r.cost)} Gold", 14, goldOk ? Theme.Good : bad);
                if (near != null)
                {
                    double nuid = near.uid;
                    var fb2 = HK.B(box.transform, "Verschmelzen", () => a.fuse(tuid, nuid), Ui.BtnKind.Primary);
                    HK.Enable(fb2, ready);
                }
            }
            // Aktive Fähigkeit
            if (d.active != null)
            {
                var ta = Towers.TOWER_ACTIVES[d.active];
                var asx = EconomySystem.activeState(sim, t);
                var row = Upg(body, new Color(96 / 255f, 232 / 255f, 212 / 255f, 0.6f), new Color(40 / 255f, 110 / 255f, 120 / 255f, 0.2f));
                var col = UpgText(row.transform, $"⚡ {ta.name}", ta.description);
                Tiny(col.transform, $"Abklingzeit {U.Fmt(ta.cooldown)} s · löst ab Stufe {U.Fmt(Towers.ACTIVE_MIN_LEVEL)} automatisch aus, sobald Gegner in Reichweite sind");
                var b = HK.B(row.transform, asx.available ? "Auslösen (C)" : (asx.reason ?? "–"), () => a.towerActive(tuid), asx.available ? Ui.BtnKind.Primary : Ui.BtnKind.Normal, 14);
                HK.Enable(b, asx.available);
                HK.Tip(b, asx.reason ?? "Auslösen (C)");
            }
            if (st != null) statTable(body, HudText.statLines(st, d));
            // Netzwerk
            if (sim.c.linked.TryGetValue(t.uid, out var gi))
            {
                var gr = (int)gi < s.network.groups.Count ? s.network.groups[(int)gi] : null;
                int links = s.network.links.Count((l) => l.a == t.uid || l.b == t.uid);
                var res = (gr?.resonances ?? new List<string>()).Select((id) => Elements.RESONANCES.FirstOrDefault((x) => x.id == id)?.name ?? id).ToList();
                var nb = HK.Box(body, new Color(40 / 255f, 90 / 255f, 160 / 255f, 0.15f), new Color(90 / 255f, 184 / 255f, 1f, 0.45f), 9, 6, 3);
                Small(nb.transform, $"Netzwerk: {gr?.members.Count ?? 0} Türme, {links} Verbindung(en) · Bonus +{HK.pct(gr?.bonus ?? 0)} Schaden & Tempo");
                if (res.Count > 0) Small(nb.transform, $"✦ Resonanz aktiv: {string.Join(", ", res)}", Theme.Gold);
            }
            else if (d.networkable && t.def != "pylon")
            {
                Small(body, "Nicht vernetzt – ein Leitpylon in Reichweite verbindet diesen Turm.", Theme.Muted);
            }
            if (t.def == "goldmine")
                Small(body, $"Bisher gefördert: {HK.fmt(t.goldEarned)} Gold. Investiert: {HK.fmt(t.invested)} Gold.{(t.builtWave >= s.wave && s.phase == "build" ? " Zahlt ab Ende der nächsten Welle." : "")}");
            else if (d.attack != "none")
                Small(body, $"Schaden gesamt: {HK.fmt(t.damageDealt)} · Abschüsse: {U.Fmt(t.kills)}", Theme.Muted);
            // Zielwahl
            if (st != null && HudText.V(st, "damage") > 0 && d.attack != "pulse" && d.attack != "aura")
            {
                Small(body, "Zielwahl (T):", Theme.Muted);
                var seg = HK.Flow(body, 4);
                foreach (var p in HudText.PRIORITY_ORDER)
                {
                    if (p == "random" && d.attack != "rift") continue;
                    string pp = p;
                    var b = HK.Sm(seg.transform, HudText.PRIORITY_NAMES[p], () => a.setPriority(tuid, pp), t.priority == p ? Ui.BtnKind.On : Ui.BtnKind.Normal);
                    b.GetComponent<HudFit>().fixedWidth = 151;
                    Ui.Size(b, 151);
                }
            }
            // Upgrades
            double? next = EconomySystem.nextUpgradeCost(t);
            if (next.HasValue)
            {
                var up = d.upgrades[(int)t.level - 1];
                var row = Upg(body);
                UpgText(row.transform, $"Stufe {U.Fmt(t.level + 1)}: {up.name}", up.description);
                var b = HK.B(row.transform, $"▲ {U.Fmt(next.Value)} Gold (U)", () => a.upgrade(tuid), Ui.BtnKind.Primary, 15);
                HK.Enable(b, s.gold >= next.Value);
            }
            else if (d.specs.Count == 2 && t.spec == 0 && d.legendary != true)
            {
                bool anyAff = s.gold >= Math.Min(EconomySystem.specCost(t, 1), EconomySystem.specCost(t, 2));
                Bold(body, anyAff ? "★ Spezialisierung bereit – jetzt wählen!" : "★ Spezialisierung wählen (später gegen Aufpreis wechselbar)", 16).color = anyAff ? HK.StarGold : Theme.Text;
                if (anyAff) Small(body, "Später gegen Aufpreis wechselbar.", Theme.Muted);
                for (int i = 0; i < 2; i++)
                {
                    var sp = d.specs[i];
                    double c = EconomySystem.specCost(t, i + 1);
                    int spn = i + 1;
                    var row = Upg(body, SpecBorder);
                    HK.Img(row.transform, Icons.Model(t.def), 44);
                    UpgText(row.transform, $"{(i == 0 ? "(A)" : "(B)")} {sp.name}", sp.description);
                    var b = HK.B(row.transform, $"{U.Fmt(c)} Gold", () => a.specialize(tuid, spn), Ui.BtnKind.Primary, 15);
                    HK.Enable(b, s.gold >= c);
                }
            }
            else if (U.Truthy(t.spec))
            {
                var cur = d.specs[(int)t.spec - 1];
                Small(body, $"Spezialisiert: {cur.name} – {cur.description}");
                if (d.specs.Count == 2 && d.legendary != true)
                {
                    int other = t.spec == 1 ? 2 : 1;
                    double c = EconomySystem.respecCost(t, other);
                    var row = Upg(body, SpecBorder);
                    HK.Img(row.transform, Icons.Model(t.def), 44);
                    UpgText(row.transform, $"⇄ Wechseln zu {(other == 1 ? "(A)" : "(B)")} {d.specs[other - 1].name}", $"{d.specs[other - 1].description} (Wechsel kostet {U.Fmt(U.Round(EconomySystem.RESPEC_MUL * 100))} % des Preises)");
                    var b = HK.B(row.transform, $"{U.Fmt(c)} Gold", () => a.respec(tuid, other), Ui.BtnKind.Normal, 15);
                    HK.Enable(b, s.gold >= c);
                }
            }
            if (d.legendary == true)
            {
                var mb = HK.B(body, "⇄ Versetzen", () => a.startMove(tuid));
                HK.Tip(mb, "Kostenlos auf ein anderes freies Feld versetzen – danach 3 s außer Gefecht");
            }
            // Verkauf
            double refund = EconomySystem.refundValue(sim, t);
            bool full = refund >= t.invested;
            bool armed = sellArmed == t.uid;
            bool noSell = s.paused && v.miniPaused;
            sellBtn = HK.B(body, noSell ? "Verkaufen in der Mini-Pause gesperrt" : armed ? $"Wirklich verkaufen? +{U.Fmt(refund)} Gold" : $"Verkaufen: +{U.Fmt(refund)} Gold{(full ? " (volle Erstattung)" : " (70 %)")}", () =>
            {
                if (sellArmed == tuid)
                {
                    sellArmed = 0;
                    a.sell(tuid);
                }
                else
                {
                    sellArmed = tuid;
                    panelSig = "";
                }
            }, armed ? Ui.BtnKind.Danger : Ui.BtnKind.Normal);
            HK.Enable(sellBtn, !noSell);
            if (noSell) HK.Tip(sellBtn, "In der Mini-Pause kann nicht verkauft werden");
            details(body, d);
        }

        void heroHeader(Transform body, string id, bool close)
        {
            var def = Heroes.HEROES[id];
            Ph(body, Icons.Model(id), def.name, $"{def.title} · {def.role}", close ? closeAction : (Action)null);
        }

        void renderPlacing(string id)
        {
            var def = Heroes.HEROES[id];
            var s = sim.s;
            var body = panelBody;
            heroHeader(body, id, true);
            Hint(body, "Tippe eine Wegzelle an, um den Helden dort abzusetzen. Er hält diese Stelle, bis du ihm einen neuen Punkt zuweist.");
            if (s.riftEnergy < def.cost) P(body, $"Es fehlen {U.Fmt(Math.Ceiling(def.cost - s.riftEnergy))} Rissenergie.", HK.FS, Theme.Bad);
            P(body, def.description);
        }

        void renderHero(HeroInstance hero)
        {
            var def = Heroes.HEROES[hero.id];
            var st = HeroSystem.heroStatsOf(sim, hero);
            var body = panelBody;
            var xpl = Heroes.HERO_RULES.xpPerLevel;
            double? nextXp = hero.level < xpl.Count ? xpl[(int)hero.level] : (double?)null;
            string hid = hero.id;
            heroHeader(body, hero.id, true);
            var xr = HK.HRow(body, 8);
            HK.T(xr.transform, $"Stufe {U.Fmt(hero.level)}/{U.Fmt(Heroes.HERO_RULES.maxLevel)}", 15).horizontalOverflow = HorizontalWrapMode.Overflow;
            var bar = new HudBar(xr.transform, Theme.Hex(0xe6c05a), 6, 1);
            HK.Grow(bar.root);
            bar.Set(nextXp.HasValue && U.Truthy(nextXp.Value) ? (float)Math.Min(1, hero.xp / nextXp.Value) : 1);
            HK.T(xr.transform, nextXp.HasValue && U.Truthy(nextXp.Value) ? $"{U.Fmt(Math.Floor(hero.xp))}/{U.Fmt(nextXp.Value)} EP" : "max.", HK.TINY, Theme.Muted).horizontalOverflow = HorizontalWrapMode.Overflow;
            if (hero.pendingPerk)
            {
                int idx = hero.perks.Count;
                var pair = idx < def.perks.Count ? def.perks[idx] : null;
                if (pair != null)
                {
                    var pc = HK.Box(body, new Color(1f, 216 / 255f, 74 / 255f, 0.06f), Theme.Gold, 9, 7, 5);
                    Bold(pc.transform, $"★ Stufe {idx + 2}: Wähle eine Verbesserung", 16);
                    var opts = HK.HRow(pc.transform, 6, TextAnchor.UpperLeft, true);
                    for (int i = 0; i < pair.Length; i++)
                    {
                        var pk = pair[i];
                        int ci = i;
                        var card = HK.Box(opts.transform, new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.45f), Theme.LineStrong, 8, 7, 3);
                        Ui.Size(card, -1, -1, 1);
                        var b = card.gameObject.AddComponent<Button>();
                        b.targetGraphic = card.GetComponent<Image>();
                        b.onClick.AddListener(() => a.heroPerk(hid, ci));
                        Bold(card.transform, pk.name, 15);
                        Small(card.transform, pk.description);
                    }
                }
            }
            if (hero.state == "active")
            {
                Small(body, "Verhalten:", Theme.Muted);
                var seg = HK.Flow(body, 4);
                foreach (var m in new[] { "hold", "advance", "retreat" })
                {
                    string mm = m;
                    HK.Sm(seg.transform, MODE_LABEL[m], () => a.heroMode(hid, mm), hero.mode == m ? Ui.BtnKind.On : Ui.BtnKind.Normal, m);
                }
                Tiny(body, "Halten: bleibt am Ankerpunkt (Wegzelle antippen versetzt ihn). Vorrücken: greift nahe Gegner an. Rückzug: kehrt zum Kern zurück und erhält einen Teil der Abklingzeit erstattet.");
            }
            var rows = new List<string[]>();
            if (st.damage > 0) rows.Add(new[] { "Schaden", $"{HK.fmtNum(st.damage)} ({HudText.Dmg(def.damageType)})" });
            rows.Add(new[] { "Reichweite", $"{HK.fmtNum(st.range)} Felder" });
            rows.Add(new[] { "Lebenspunkte", $"{HK.fmt(hero.hp)} / {HK.fmt(st.maxHp)}" });
            if (st.armor > 0) rows.Add(new[] { "Rüstung", HK.pct(st.armor) });
            if (st.blockCount > 0) rows.Add(new[] { "Hält fest", $"{U.Fmt(U.Round(st.blockCount))} Gegner" });
            if (st.auraRadius > 0) rows.Add(new[] { "Aura", $"{HK.fmtNum(st.auraRadius)} Felder · {HK.pct(st.auraPower)}" });
            if (st.goldBonus > 0) rows.Add(new[] { "Goldbonus", HK.pct(st.goldBonus) });
            rows.Add(new[] { "Einsatzzeit", $"{U.Fmt(Math.Ceiling(hero.activeLeft))} / {U.Fmt(U.Round(st.activeTime))} s" });
            rows.Add(new[] { "Abschüsse", U.Fmt(hero.kills) });
            statTable(body, rows);
            string[] keys = { "A", "S", "D" };
            for (int slot = 0; slot < 3; slot++)
            {
                var ab = slot == 2 ? def.ultimate : def.abilities[slot];
                var r = HeroSystem.abilityReady(sim, hero, slot);
                int sl = slot;
                var row = Upg(body, slot == 2 ? SpecBorder : (Color?)null);
                UpgText(row.transform, $"{keys[slot]} · {ab.name}{(slot == 2 ? " (Ultimate)" : "")}", ab.description);
                var b = HK.B(row.transform, r.ok ? "Wirken" : (r.reason ?? "–"), () => a.heroAbility(hid, sl), r.ok ? Ui.BtnKind.Primary : Ui.BtnKind.Normal, 14);
                HK.Enable(b, r.ok);
                HK.Tip(b, r.reason ?? "");
            }
            var duos = HeroSystem.activeDuos(sim);
            var myDuos = Heroes.DUOS.Where((d) => d.a == hero.id || d.b == hero.id).ToList();
            if (myDuos.Count > 0)
            {
                Small(body, "<b>Duo-Synergien:</b>");
                foreach (var d in myDuos)
                {
                    bool on = duos.Contains(d.id);
                    Small(body, $"{(on ? "✔" : "○")} {d.name} (mit {lastWord(Heroes.HEROES[d.a == hero.id ? d.b : d.a].name)}): {d.description}", on ? Theme.Good : Theme.Muted);
                }
            }
            if (hero.perks.Count > 0)
            {
                var names = new List<string>();
                for (int i = 0; i < hero.perks.Count; i++)
                {
                    var pair = i < def.perks.Count ? def.perks[i] : null;
                    int c = (int)hero.perks[i];
                    names.Add(pair != null && c >= 0 && c < pair.Length && pair[c] != null ? pair[c].name : "?");
                }
                Small(body, $"Gewählt: {string.Join(", ", names)}", Theme.Muted);
            }
        }

        void renderEnemy(string id)
        {
            var body = panelBody;
            if (id == "boss" && !string.IsNullOrEmpty(bossInfo))
            {
                var b = Enemies.BOSSES[bossInfo];
                Ph(body, Icons.Model(bossInfo), b.name, b.title, closeAction);
                P(body, b.description);
                Small(body, $"Konter: {b.counter}");
                foreach (var p in b.phases)
                    Small(body, $"• {p.name}{(p.abilities.Count > 0 ? $": {string.Join(", ", p.abilities.Select((x) => Enemies.ABILITIES[x].name))}" : "")}");
                resistLine(body, b.resist);
                return;
            }
            if (id == "boss") return;
            var d = Enemies.ENEMIES[id];
            Ph(body, Icons.Model(id), d.name, d.role, closeAction);
            P(body, d.description);
            var rows = new List<string[]>
            {
                new[] { "Lebenspunkte (Basis)", HK.fmt(d.hp) },
                new[] { "Tempo", $"{HK.fmtNum(d.speed)} Felder/s" },
                new[] { "Kopfgeld", $"{U.Fmt(d.reward)} Gold" },
                new[] { "Kernschaden", U.Fmt(d.leak) },
                new[] { "Fliegt", d.flying ? "ja" : "nein" },
            };
            if (d.stealth == true) rows.Add(new[] { "Getarnt", "nur durch Detektoren sichtbar" });
            if (d.burrow == true) rows.Add(new[] { "Gräbt sich ein", "zeitweise unangreifbar" });
            statTable(body, rows);
            resistLine(body, d.resist);
            if (d.abilities.Count > 0) Small(body, $"Fähigkeiten: {string.Join(" ", d.abilities.Select((x) => $"{Enemies.ABILITIES[x].name} – {Enemies.ABILITIES[x].description}"))}");
            Small(body, $"Konter: {d.counter}");
        }

        static readonly Dictionary<double, string> OBSTACLE_NAMES = new Dictionary<double, string>
        {
            [0] = "Baum", [1] = "Fels", [2] = "Kristallgruppe", [3] = "Lavafeld", [4] = "Eisblock", [5] = "Ruine", [6] = "Riesenpilz", [7] = "Wolke", [8] = "Schilf", [9] = "Leuchtpilz",
        };

        /// <summary>Hindernis im Helden-Endlos: abbauen (dauert eine Welle, nicht unterbrechbar, zufällige Ausbeute).</summary>
        void renderObstacle(double idx)
        {
            var s = sim.s;
            var he = s.he;
            if (he == null) return;
            double w = s.map.width;
            double? kind = MiningSystem.decorKindAt(sim, idx);
            var chk = MiningSystem.mineState(sim, idx % w, Math.Floor(idx / w));
            var busy = he.mining;
            double dur = MiningSystem.mineDuration(sim);
            var body = panelBody;
            // PORT: TS zeigt hier 🌳/💎/🍄/🪨 – Emojis fehlen in der UI-Schrift; stattdessen ein schlichtes Zeichen.
            string glyph = kind == 0 ? "♣" : kind == 2 ? "◆" : kind == 6 || kind == 9 ? "♠" : "■";
            string name = kind.HasValue ? (OBSTACLE_NAMES.TryGetValue(kind.Value, out var nm) ? nm : "Hindernis") : "Freies Feld";
            Ph(body, null, name, "Hindernis – mit dem Helden abbaubar", () => { v.selectedObstacle = -1; panelSig = ""; }, glyph);
            if (busy != null && busy.cell == idx)
            {
                P(body, $"Wird abgebaut … noch {U.Fmt(Math.Ceiling(busy.t))} s");
                var bar = new HudBar(body, Theme.Hex(0x4ac8bc), 10, 1);
                bar.Set((float)(1 - busy.t / busy.total));
            }
            else
            {
                Small(body, $"Abbau dauert ungefähr eine Welle (≈ {U.Fmt(dur)} s) und kann nicht unterbrochen werden: Der Held steht still, greift nicht an und kann in dieser Zeit nicht fallen.");
                Small(body, $"Ausbeute (zufällig): {U.Fmt(HeroEndless.HERO_ENDLESS.mineGoldBase + HeroEndless.HERO_ENDLESS.mineGoldPerWave * Math.Max(1, s.wave))} Gold (±50 %), meist Metall, oft Kristall{(kind == 2 || kind == 4 ? " (hier besonders viel)" : "")}, selten Essenz oder ein Relikt. Danach ist das Feld frei und bebaubar.");
                var b = HK.B(body, chk.ok ? $"Abbauen (≈ {U.Fmt(dur)} s)" : chk.reason ?? "Nicht möglich", () => a.mine(idx), Ui.BtnKind.Primary);
                HK.Enable(b, chk.ok);
                HK.Tip(b, chk.reason ?? "");
            }
        }

        static readonly string[] DAMAGE_TYPES = { "physical", "fire", "frost", "lightning", "poison", "arcane", "sonic" };

        /// <summary>Live-Werte eines angeklickten Gegners: LP, Schild, Tempo, Resistenzen, Effekte, Fähigkeiten.</summary>
        void renderEnemyInstance(EnemyInstance e)
        {
            var body = panelBody;
            var boss = !string.IsNullOrEmpty(e.boss) ? Enemies.BOSSES[e.boss] : null;
            var d = Enemies.ENEMIES[e.def];
            string name = boss != null ? boss.name : d.name;
            var tags = new List<string>();
            if (e.miniBoss == true) tags.Add("Mini-Boss");
            else if (e.elite) tags.Add("Elite");
            if (boss != null) tags.Add($"Boss · Phase {U.Fmt(e.phase + 1)}{(e.phase < boss.phases.Count ? $": {boss.phases[(int)e.phase].name}" : "")}");
            if (e.mutation != null && Enemies.MUTATIONS.TryGetValue(e.mutation, out var mut)) tags.Add($"{HK.Glyph(mut.icon)} {mut.name}".Trim());
            if (e.enraged) tags.Add("Raserei");
            Sprite img = boss != null ? Icons.Model(e.boss) : e.def != "boss" ? Icons.Model(e.def) : null;
            double slow = ElementSystem.slowFactor(e);
            double speed = e.speed * (1 - slow);
            var res = new List<KeyValuePair<string, double>>();
            foreach (var k in DAMAGE_TYPES)
            {
                double rv = DamageSystem.resistOf(sim, e, k);
                if (Math.Abs(rv) > 0.001) res.Add(new KeyValuePair<string, double>(k, rv));
            }
            Ph(body, img, name, tags.Count > 0 ? string.Join(" · ", tags) : (boss != null ? boss.title : d.role), () => { a.selectEnemy(0); panelSig = ""; }, boss != null ? "♛" : null);
            double hpPct = Math.Max(0, e.hp / Math.Max(1, e.maxHp));
            var hp = new HudBar(body, Theme.Hex(0xe0503f), 18, 11, false);
            hp.Set((float)hpPct);
            hp.label.text = $"{HK.fmt(Math.Ceiling(e.hp))} / {HK.fmt(e.maxHp)} LP";
            if (e.maxShield > 0)
            {
                var sh = new HudBar(body, Theme.Hex(0x4ac8bc), 14, 10, false);
                sh.Set((float)(e.shield / e.maxShield));
                sh.label.text = $"Schild {HK.fmt(e.shield)} / {HK.fmt(e.maxShield)}";
            }
            var rows = new List<string[]>
            {
                new[] { "Tempo", $"{HK.fmtNum(speed, 2)} Felder/s{(slow > 0.005 ? $" (−{U.Fmt(U.Round(slow * 100))} % verlangsamt)" : "")}" },
                new[] { "Kernschaden bei Durchbruch", $"{U.Fmt(Math.Max(1, U.Round(e.leak)))} ♥" },
                new[] { "Kopfgeld", $"{U.Fmt(U.Round(e.reward))} Gold" },
            };
            if (sim.s.he != null) rows.Add(new[] { "Schaden am Helden", $"{HK.fmtNum(EnemySystem.enemyContactDps(e) * (e.miniBoss == true ? 2 : 1) * (1 + 0.05 * sim.s.wave), 0)}/s bei Kontakt" });
            rows.Add(new[] { "Bewegung", e.flying ? "fliegt" : e.burrowed ? "eingegraben (unangreifbar)" : "Boden" });
            if (e.stealthed) rows.Add(new[] { "Tarnung", "getarnt – nur Detektoren sehen ihn" });
            if (e.plates.Count > 0) rows.Add(new[] { "Schwachpunkte", string.Join(" · ", e.plates.Select((p) => p > 0 ? HK.fmt(p) : "✖")) });
            statTable(body, rows);
            string resTxt = res.Count > 0
                ? string.Join("", res.Select((kv) => HK.Rc($"{HudText.Dmg(kv.Key)} {(kv.Value > 0 ? "−" : "+")}{U.Fmt(U.Round(Math.Abs(kv.Value) * 100))} % ", kv.Value > 0 ? ResUp : ResDown)))
                : "keine";
            Small(body, $"Resistenzen: {resTxt}");
            Bold(body, "Effekte", 17);
            if (e.statuses.Count > 0)
            {
                foreach (var x in e.statuses)
                {
                    if (!Elements.STATUS_DEFS.TryGetValue(x.id, out var sd)) continue;
                    string ic = HK.Glyph(sd.icon);
                    Small(body, $"• {(ic != "" ? ic + " " : "")}{sd.name}{(x.stacks > 1 ? $" ×{U.Fmt(x.stacks)}" : "")} – {HK.fmtNum(x.t, 1)} s · <color=#b3a9cc>{sd.description}</color>");
                }
            }
            else Small(body, "Keine Effekte aktiv.", Theme.Muted);
            if (e.abilities.Count > 0) Small(body, $"Fähigkeiten: {string.Join(", ", e.abilities.Select((ab) => $"{Enemies.ABILITIES[ab.id].name}{(ab.windup > 0 ? " (wird gewirkt!)" : ab.cd > 0 ? $" ({U.Fmt(Math.Ceiling(ab.cd))} s)" : "")}"))}");
            if (e.mutation != null && Enemies.MUTATIONS.TryGetValue(e.mutation, out var mu)) Small(body, $"{HK.Glyph(mu.icon)} {mu.description}".Trim());
            Small(body, $"Konter: {(boss != null ? boss.counter : d.counter)}");
        }

        void resistLine(Transform body, Dictionary<string, double> r)
        {
            var parts = r.Where((kv) => U.Truthy(kv.Value)).Select((kv) => $"{HudText.Dmg(kv.Key)} {(kv.Value > 0 ? "−" : "+")}{U.Fmt(U.Round(Math.Abs(kv.Value) * 100))} %").ToList();
            // 3.3: Konter direkt aus den Werten – passt dadurch immer zu den Resistenzen
            var weak = r.Where((kv) => kv.Value <= -0.1).Select((kv) => $"{HudText.Dmg(kv.Key)} ({string.Join(", ", HudText.towersOfType(kv.Key))})").ToList();
            var strong = r.Where((kv) => kv.Value >= 0.2).Select((kv) => HudText.Dmg(kv.Key)).ToList();
            Small(body, $"Resistenzen: {(parts.Count > 0 ? string.Join(", ", parts) : "keine")}");
            if (weak.Count > 0) Small(body, $"▲ Schwach gegen: {string.Join(" · ", weak)}", ResDown);
            if (strong.Count > 0) Small(body, $"▼ Meiden: {string.Join(", ", strong)}", ResUp);
        }

        /// <summary>3.3: Stärken/Schwächen eines Turms gegen konkrete Gegner, aus den Resistenzwerten berechnet.</summary>
        void matchupLines(Transform body, string damageType)
        {
            if (damageType == null) return;
            var good = new List<string>();
            var bad = new List<string>();
            HudText.damageMatchups(damageType, good, bad);
            string dn = HudText.Dmg(damageType);
            if (good.Count > 0) Small(body, $"▲ {dn} wirkt stark gegen: {string.Join(", ", good)}", ResDown);
            if (bad.Count > 0) Small(body, $"▼ {dn} wird gedämpft von: {string.Join(", ", bad)}", ResUp);
        }
    }
}
