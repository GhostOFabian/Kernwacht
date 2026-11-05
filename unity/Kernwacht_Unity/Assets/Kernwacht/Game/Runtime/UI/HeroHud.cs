using System;
using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Port von ui/HeroHud.ts: HUD-Erweiterung des Helden-Endlosmodus: Heldenleiste (LP/Mana/EP, Skills, Tränke),
// Ressourcen in der Kopfzeile, Laden und Charakterbogen als Fenster. Liest nur; Aktionen über HeroHudActions.
// Darstellung: Bezugsgröße 1920×1080; die Heldenleiste hat im Webspiel zoom 1.2 → px-Werte ×1,2 bei 18-px-Grundschrift.

namespace Kernwacht.Game.UI
{
    public interface HeroHudActions
    {
        void championSkillKey(double slot);
        void usePotion(string potion);
        void slotPotion(string potion);
        void learnSkill(string nodeId);
        void paragonPoint(string nodeId);
        void paragonReset();
        void treeReset();
        void equip(double uid);
        void unequip(double slot);
        void shopBuy(string offerId);
        void shopBuyItem(string item);
        void shopSell(double uid);
        void craft(string recipeId);
        void upgradeItem(double uid);
        void focusChampion();
        void toggleFollow();
        void toggleTopDown();
        void toggleHold();
        void moveToShop();
        /// <summary>Befehlsbanner setzen (atCursor: Feld unter dem Mauszeiger, sonst Position des Helden).</summary>
        void placeBanner(bool atCursor);
        /// <summary>Taktikladung zünden.</summary>
        void useCharge(string id);
    }

    public class HeroHud
    {
        // ------------------------------------------------------------ statische Hilfen (Exporte von HeroHud.ts)

        /// <summary>Aktuell gebundene Taste einer Heldenfähigkeit (Anzeige).</summary>
        public static string skillKey(double slot) { return Keys.keyName(Keys.boundKey($"skill{U.Fmt(slot + 1)}")); }

        // PORT: '⇧' fehlt in der Laufzeitschrift → '↑' (Umschalt) als Kurzzeichen; Tooltips nennen „Umschalt+n“.
        public static readonly string[] POTION_KEYS = { "↑1", "↑2", "↑3", "↑4" };
        private static readonly string[] POTION_KEY_TIPS = { "Umschalt+1", "Umschalt+2", "Umschalt+3", "Umschalt+4" };

        private static string costText(Dictionary<string, double> c)
        {
            var parts = new List<string>();
            double g = U.Get(c, "gold", 0);
            if (U.Truthy(g)) parts.Add($"{U.Fmt(g)} Gold");
            foreach (string r in HeroEndless.RESOURCE_ORDER)
            {
                double v = U.Get(c, r, 0);
                // PORT: Ressourcen-Emoji → Name in Ressourcenfarbe
                if (U.Truthy(v)) parts.Add(HeroKit.Col($"{U.Fmt(v)} {HeroEndless.RESOURCES[r].name}", HeroKit.Hex6(HeroEndless.RESOURCES[r].color)));
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : "kostenlos";
        }

        public static string statValue(string k, double v)
        {
            if (Champions.STAT_PCT.Contains(k)) return $"{U.Fmt(U.Round(v * 100))} %";
            if (Champions.STAT_MULT.Contains(k)) return $"×{HeroKit.fmtNum(v, 2)}";
            return HeroKit.fmtNum(v, k == "attackSpeed" || k == "range" || k == "moveSpeed" || k == "pickupRadius" || k == "supportRadius" ? 2 : 1);
        }

        private static string statDelta(string k, double v)
        {
            string sign = v >= 0 ? "+" : "−";
            double a = Math.Abs(v);
            if (Champions.STAT_PCT.Contains(k) || Champions.STAT_MULT.Contains(k)) return $"{sign}{U.Fmt(U.Round(a * 100))} %";
            return $"{sign}{HeroKit.fmtNum(a, 2)}";
        }

        private static string statName(string k) { string n; return Champions.STAT_NAMES.TryGetValue(k, out n) ? n : k; }

        /// <summary>Abstand der Heldenleiste vom unteren Rand (über der Turmleiste), falls sie nicht umgehängt wird.</summary>
        public static float dockBottom = 96;
        /// <summary>Höhe von Kopfzeile/Turmleiste (Hud: TOP_H/BAR_H), solange das Fenster direkt unter einer Ebene liegt.</summary>
        public static float stageTop = 52, stageBottom = 90;

        // ------------------------------------------------------------ Zustand

        public readonly RectTransform dock;
        public readonly RectTransform resBar;
        public readonly RectTransform win;
        /// <summary>'' | 'sheet' | 'shop'</summary>
        public string kind = "";
        /// <summary>Gerade gewählter Skill (Zielwahl), für die Hervorhebung.</summary>
        public double targeting = -1;
        public bool follow = false;

        private readonly GameSimulation sim;
        private readonly HeroHudActions a;
        private readonly ChampionDefinition def;
        private readonly Color hc;

        private string sheetTab = "gear";   // stats | gear | tree | paragon | passive | collection | progress
        private string shopTab = "potions"; // ShopCategory | forge | sell
        private bool winDirty = true;
        private ulong winSig;
        private bool pointerHeld = false;
        private double selectedItem = 0;

        // Kopfzeile
        private readonly Dictionary<string, Text> resVals = new Dictionary<string, Text>();
        private readonly Dictionary<string, double> resLast = new Dictionary<string, double>();
        private Text best, profile;
        private double bestLast = double.NaN;
        private string profLastId; private int profLastCombat = -1;

        // Heldenleiste
        private Image dockImg; private Outline dockBorder;
        private Image portraitImg; private Text portraitFallback; private float portraitRetry;
        private Text lvl, nameEl;
        private double lvlLast = -1, spLast = -1;
        private HeroBar hp, mana, xp;
        private int hpA = int.MinValue, hpB = int.MinValue, mnA = int.MinValue, mnB = int.MinValue;
        private double xpA = double.NaN, xpB = double.NaN; private int xpMode = -1;
        private Text deadNote; private int deadLast = int.MinValue;
        private RectTransform buffs;
        private readonly List<BuffEl> buffEls = new List<BuffEl>();
        private readonly List<SkillEl> skillEls = new List<SkillEl>();
        private float keyTimer;
        private RectTransform footRow1, footRow2, potionRow, btnRow;
        private readonly List<PotionEl> potionEls = new List<PotionEl>();
        private Text noPotions;
        private ulong potionSig; private bool potionDirty = true;
        private Button sheetBtn, shopBtn, followBtn, holdBtn, topBtn, bannerBtn;

        /// <summary>Hinweis zum Befehl des Helden: welche Türme er anführt und wie es um das Banner steht.</summary>
        private string bannerTip()
        {
            var he = sim.s.he;
            if (he == null) return "";
            var p = HeroCommand.profileOf(he.champion.id);
            string aff = p != null ? p.label : "–";
            string cd = he.bannerCd > 0 ? $"Versetzen in {U.Fmt(Math.Ceiling(he.bannerCd))} s" : "bereit";
            return $"Befehlsbanner (Punkt-Taste: unter den Mauszeiger, Klick: hier)\nWirkt mit halber Stärke wie der Befehl des Helden.\nAffinität: {aff} (voller Bonus, andere Türme ¼)\nBanner: {cd}";
        }
        private bool nearLast;
        private int dockState = -1;

        // ------------------------------------------------------------ Taktikladungen (rechts neben dem Dock)

        private class ChargeEl { public string id; public Button b; public Image bg, icon, cdFill; public Outline border; public int state = -1; }
        private readonly List<ChargeEl> chargeEls = new List<ChargeEl>();
        private Text chargeCount;
        private double chargeLast = -1;

        private void buildCharges(RectTransform dockRt)
        {
            var box = HeroKit.Box(dockRt, new Color(20 / 255f, 15 / 255f, 32 / 255f, 0.92f), Theme.LineStrong, Theme.Round);
            box.name = "Taktikladungen";
            Ui.Get<LayoutElement>(box.gameObject).ignoreLayout = true;
            Ui.Place(box.rectTransform, new Vector2(1, 0), new Vector2(0, 0), new Vector2(8, 0), new Vector2(4 * 50 + 3 * 5 + 14, 84));
            chargeCount = HeroKit.T(box.transform, "Taktik", 13, Theme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
            Ui.Place(chargeCount.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -3), new Vector2(210, 20));
            chargeCount.horizontalOverflow = HorizontalWrapMode.Overflow;
            for (int i = 0; i < TacticalCharges.ORDER.Count; i++)
            {
                string id = TacticalCharges.ORDER[i];
                var d = TacticalCharges.CHARGES[id];
                var el = new ChargeEl { id = id };
                el.b = HeroKit.CardBtn(box.transform, new Color(40 / 255f, 32 / 255f, 60 / 255f, 1f), Theme.LineStrong, () => a.useCharge(id), out el.bg, out el.border);
                Ui.Get<LayoutElement>(el.bg.gameObject).ignoreLayout = true;
                Ui.Place(el.bg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(7 + i * 55, 7), new Vector2(50, 50));
                el.icon = HeroKit.Ic(el.bg.transform, d.icon, 28, Theme.Hex(d.color));
                Ui.Get<LayoutElement>(el.icon.gameObject).ignoreLayout = true;
                Ui.Place(el.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
                el.cdFill = HeroKit.Box(el.bg.transform, new Color(0, 0, 0, 0.55f), null);
                el.cdFill.raycastTarget = false;
                Ui.Get<LayoutElement>(el.cdFill.gameObject).ignoreLayout = true;
                var cr = el.cdFill.rectTransform; cr.anchorMin = Vector2.zero; cr.anchorMax = new Vector2(1, 0); cr.pivot = new Vector2(0.5f, 0); cr.sizeDelta = new Vector2(0, 0); cr.anchoredPosition = Vector2.zero;
                string tip = $"{d.name}: {d.description}\nKostet 1 Taktikladung. Neue Ladungen alle {U.Fmt(Doctrines.of(sim.s.he).chargeEvery)} Wellen (Bosswellen +1), höchstens {U.Fmt(Doctrines.of(sim.s.he).chargeMax)}.";
                HeroKit.Tip(el.bg, tip);
                chargeEls.Add(el);
            }
        }

        private void updateCharges(HeroEndlessState he)
        {
            if (chargeCount == null) return;
            if (he.charges != chargeLast)
            {
                chargeLast = he.charges;
                double mx = Doctrines.of(he).chargeMax;
                chargeCount.text = $"Taktikladungen  <color=#ffd860>{U.Fmt(he.charges)}/{U.Fmt(mx)}</color>";
            }
            float cdFrac = he.chargeCd > 0 ? (float)(he.chargeCd / TacticalCharges.SHARED_CD) : 0f;
            foreach (var el in chargeEls)
            {
                bool ready = he.charges >= 1 && he.chargeCd <= 0;
                int st = ready ? 1 : 0;
                el.cdFill.rectTransform.sizeDelta = new Vector2(0, 50 * Mathf.Clamp01(cdFrac));
                if (st == el.state) continue;
                el.state = st;
                el.icon.color = ready ? Theme.Hex(TacticalCharges.CHARGES[el.id].color) : new Color(0.45f, 0.42f, 0.5f, 1f);
                el.border.effectColor = ready ? Theme.Hex(TacticalCharges.CHARGES[el.id].color, 0.8f) : Theme.LineStrong;
                el.bg.color = ready ? new Color(46 / 255f, 36 / 255f, 70 / 255f, 1f) : new Color(28 / 255f, 22 / 255f, 38 / 255f, 1f);
            }
        }

        // Fenster
        private ScrollRect bodyScroll;
        private LayoutElement bodyLe;
        private string lastScrollKey = "";

        private const float WIN_W = 864;
        private const float BODY_W = WIN_W - 32 - 6;

        public HeroHud(GameSimulation sim, HeroHudActions a, object icons = null)
        {
            this.sim = sim;
            this.a = a;
            var he = sim.s.he;
            if (he == null) throw new Exception("HeroHud ohne Helden-Endlos-Zustand");
            def = Champions.CHAMPIONS[he.champion.id];
            hc = Theme.Hex(def.accent);
            var app = GameApp.I;
            Transform hudParent = app != null ? app.hudLayer : null;

            resBar = buildResBar(hudParent);
            dock = buildDock(hudParent);
            // Fenster zunächst in der HUD-Ebene (GameSession hängt es wie in TS in hud.stage um)
            win = buildWin(hudParent);
        }

        /// <summary>Entfernt alle Elemente (Sitzungsende).</summary>
        public void destroy()
        {
            if (resBar != null) UnityEngine.Object.Destroy(resBar.gameObject);
            if (dock != null) UnityEngine.Object.Destroy(dock.gameObject);
            if (win != null) UnityEngine.Object.Destroy(win.gameObject);
        }

        public void toggle(string kind, string tab = null)
        {
            if (this.kind == kind && tab == null) this.kind = "";
            else this.kind = kind;
            if (tab != null && kind == "sheet") sheetTab = tab;
            if (tab != null && kind == "shop") shopTab = tab;
            winDirty = true;
        }

        public bool close()
        {
            if (string.IsNullOrEmpty(kind)) return false;
            kind = "";
            winDirty = true;
            return true;
        }

        public void refresh()
        {
            winDirty = true;
            potionDirty = true;
            keyTimer = 0;
        }

        // ------------------------------------------------------------ Aufbau

        private RectTransform buildResBar(Transform parent)
        {
            var row = Ui.Row(parent, 6, TextAnchor.MiddleLeft, false);
            row.name = "HeroRes";
            var rt = row.GetComponent<RectTransform>();
            Ui.Place(rt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(10, 40));
            var fit = row.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (string r in HeroEndless.RESOURCE_ORDER)
            {
                var rd = HeroEndless.RESOURCES[r];
                Text v;
                var pill = statPill(row.transform, HeroKit.IconName(rd.icon), Theme.Hex(rd.color), out v);
                resVals[r] = v;
                resLast[r] = double.NaN;
                string tip = $"{rd.name}: {rd.description}";
                HeroKit.Tip(pill, tip);
            }
            var bp = statPill(row.transform, "trophy", Theme.Gold, out best);
            HeroKit.Tip(bp, "Bestwelle (dauerhaft gespeichert)");
            profile = HeroKit.T(row.transform, "", 14, Theme.Muted);
            profile.horizontalOverflow = HorizontalWrapMode.Overflow;
            return rt;
        }

        private static Image statPill(Transform parent, string icon, Color ic, out Text val)
        {
            var img = HeroKit.Box(parent, new Color(0, 0, 0, 0.3f), Theme.Line, Theme.Round);
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 11, 3, 3); h.spacing = 5; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Ui.Size(img, -1, 36);
            HeroKit.Ic(img.transform, icon, 20, ic);
            val = HeroKit.T(img.transform, "0", 18, Theme.Text, FontStyle.Bold);
            val.horizontalOverflow = HorizontalWrapMode.Overflow;
            return img;
        }

        private RectTransform buildDock(Transform parent)
        {
            var he = sim.s.he;
            dockImg = HeroKit.Box(parent, Color.white, hc, Theme.Round);
            dockImg.name = "HeroDock";
            dockImg.sprite = HeroKit.VGrad(new Color(34 / 255f, 26 / 255f, 54 / 255f, 0.84f), new Color(14 / 255f, 10 / 255f, 24 / 255f, 0.9f));
            dockImg.type = Image.Type.Sliced;
            dockBorder = dockImg.GetComponent<Outline>();
            var glow = dockImg.gameObject.AddComponent<Outline>();
            glow.effectColor = HeroKit.A(hc, 0.18f); glow.effectDistance = new Vector2(3, -3);
            var rt = dockImg.rectTransform;
            Ui.Place(rt, Vector2.zero, Vector2.zero, new Vector2(8, dockBottom), new Vector2(396, 200));
            var v = dockImg.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(7, 7, 7, 7); v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = dockImg.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Ui.Size(dockImg, 396);

            // Kopf: Porträt + Balken
            var head = Ui.Row(dockImg.transform, 7, TextAnchor.MiddleLeft, false);
            var pimg = HeroKit.Box(head.transform, Theme.Hex(0x120e1e), hc, Theme.Round);
            Ui.Size(pimg, 62, 62);
            var pbtn = pimg.gameObject.AddComponent<Button>();
            HeroKit.Colors(pbtn);
            pbtn.targetGraphic = pimg;
            pbtn.onClick.AddListener(() => a.focusChampion());
            HeroKit.Tip(pimg, $"{def.name} – Kamera zentrieren");
            var pgo = Ui.Node("Portrait", pimg.transform);
            Ui.Fill(Ui.Rt(pgo), 2);
            portraitImg = pgo.AddComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            portraitFallback = HeroKit.T(pimg.transform, def.name.Length > 0 ? def.name.Substring(0, 1) : "?", 28, Theme.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Fill(portraitFallback.rectTransform);
            setPortrait();
            var lvlBg = Ui.Panel(pimg.transform, "Lvl", Theme.Gold, false, Theme.Circle);
            lvlBg.raycastTarget = false;
            lvlBg.type = Image.Type.Simple;
            Ui.Place(lvlBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 1), new Vector2(23, 23));
            lvl = HeroKit.T(lvlBg.transform, "1", 13, Theme.Hex(0x2a1a00), FontStyle.Bold, TextAnchor.MiddleCenter);
            lvl.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Fill(lvl.rectTransform);

            var bars = Ui.Column(head.transform, 4);
            Ui.Size(bars, -1, -1, 1);
            nameEl = HeroKit.Line(bars.transform, def.name, 15, Theme.Text, FontStyle.Bold);
            hp = new HeroBar(bars.transform, 16, HeroKit.HGrad(Theme.Hex(0x3ad08a), Theme.Hex(0x8affb8)), 12);
            HeroKit.Tip(hp.back, "Lebenspunkte");
            mana = new HeroBar(bars.transform, 11, HeroKit.HGrad(Theme.Hex(0x3a6ae0), Theme.Hex(0x8ab8ff)), 11);
            HeroKit.Tip(mana.back, "Mana");
            xp = new HeroBar(bars.transform, 8, HeroKit.HGrad(Theme.Hex(0xc89a30), Theme.Hex(0xffe08a)), 10);
            HeroKit.Tip(xp.back, "Erfahrung");

            deadNote = HeroKit.T(dockImg.transform, "", 14, Theme.Hex(0xffb0b0), FontStyle.Normal, TextAnchor.MiddleCenter);
            deadNote.gameObject.SetActive(false);

            var bRow = Ui.Row(dockImg.transform, 4, TextAnchor.MiddleLeft, false);
            buffs = bRow.GetComponent<RectTransform>();
            buffs.gameObject.SetActive(false);

            // Skills
            var skills = Ui.Row(dockImg.transform, 6, TextAnchor.MiddleCenter, true);
            skills.name = "Skillbar";
            for (int slot = 0; slot < def.skills.Length; slot++) skillEls.Add(buildSkill(skills.transform, slot));

            // Fuß: Tränke + Knöpfe (umbrechend wie flex-wrap)
            var foot = Ui.Column(dockImg.transform, 5);
            var r1 = Ui.Row(foot.transform, 6, TextAnchor.MiddleLeft, false);
            footRow1 = r1.GetComponent<RectTransform>();
            var pr = Ui.Row(r1.transform, 5, TextAnchor.MiddleLeft, false);
            potionRow = pr.GetComponent<RectTransform>();
            noPotions = HeroKit.T(potionRow, "Keine Tränke", 13, Theme.Muted);
            noPotions.horizontalOverflow = HorizontalWrapMode.Overflow;
            for (int i = 0; i < 5; i++) potionEls.Add(buildPotion(potionRow, i));
            var spacer = Ui.Node("Spacer", r1.transform);
            Ui.Size(Ui.Rt(spacer), 0, -1, 1);
            var r2 = Ui.Row(foot.transform, 6, TextAnchor.MiddleLeft, false);
            footRow2 = r2.GetComponent<RectTransform>();
            var br = Ui.Row(r1.transform, 5, TextAnchor.MiddleRight, false);
            btnRow = br.GetComponent<RectTransform>();
            sheetBtn = HeroKit.Btn(btnRow, "Held", () => toggle("sheet"), Ui.BtnKind.Normal, true, "book", 16, 42);
            HeroKit.Tip(sheetBtn, "Charakter, Inventar, Skillbaum (Tab)");
            shopBtn = HeroKit.Btn(btnRow, "Laden", () => toggle("shop"), Ui.BtnKind.Normal, true, "coins", 16, 42);
            Ui.Tip(shopBtn, () => nearLast ? "Laden & Schmiede öffnen (X)" : "Laden (X): der Held muss beim Laden stehen – Klick zeigt das Angebot");
            holdBtn = HeroKit.Btn(btnRow, null, () => a.toggleHold(), Ui.BtnKind.Normal, true, "hold", 16, 42);
            HeroKit.Tip(holdBtn, "Position festsetzen (Z): Klicks bewegen den Helden nicht mehr, er verfolgt keine Gegner");
            bannerBtn = HeroKit.Btn(btnRow, null, () => a.placeBanner(false), Ui.BtnKind.Normal, true, "flag", 16, 42);
            Ui.Tip(bannerBtn, bannerTip);
            followBtn = HeroKit.Btn(btnRow, null, () => a.toggleFollow(), Ui.BtnKind.Normal, true, "advance", 16, 42);
            HeroKit.Tip(followBtn, "Kamera folgt dem Helden (Y)");
            topBtn = HeroKit.Btn(btnRow, null, () => a.toggleTopDown(), Ui.BtnKind.Normal, true, "map", 16, 42);
            HeroKit.Tip(topBtn, "Draufsicht / Isometrie umschalten (V)");
            footRow2.gameObject.SetActive(false);
            buildCharges(rt);
            return rt;
        }

        private void setPortrait()
        {
            Sprite sp = null;
            try { sp = Icons.Model(def.model); } catch (Exception) { sp = null; }
            portraitImg.sprite = sp;
            portraitImg.enabled = sp != null;
            portraitFallback.gameObject.SetActive(sp == null);
        }

        private SkillEl buildSkill(Transform parent, int slot)
        {
            var sk = def.skills[slot];
            var e = new SkillEl { slot = slot, ult = slot == 3 };
            e.b = HeroKit.CardBtn(parent, Theme.Hex(0x1a1428), Theme.LineStrong, () => a.championSkillKey(slot), out e.bg, out e.border);
            e.b.name = $"Skill{slot}";
            e.bg.sprite = Theme.Round;
            Ui.Size(e.b, -1, 55, 1);
            e.glow = e.b.gameObject.AddComponent<Outline>();
            e.glow.effectDistance = new Vector2(3, -3);
            e.glow.effectColor = Color.clear;
            e.cg = e.b.gameObject.AddComponent<CanvasGroup>();
            var mask = Ui.Node("Clip", e.b.transform);
            Ui.Fill(Ui.Rt(mask), 1);
            mask.AddComponent<RectMask2D>();
            var fgo = Ui.Node("Fill", mask.transform);
            e.fill = fgo.AddComponent<Image>();
            e.fill.raycastTarget = false;
            // Gedämpfte, deckende Füllung: weiße Symbole und Zahlen bleiben auch bei hellen Heldenfarben (Gold) lesbar
            var skBase = Theme.Hex(0x1a1428);
            e.fill.color = e.ult ? Color.Lerp(skBase, new Color(1f, 0.71f, 0.24f, 1f), 0.34f) : Color.Lerp(skBase, hc, 0.26f);
            var fr = e.fill.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 1); fr.pivot = new Vector2(0.5f, 0); fr.offsetMin = fr.offsetMax = Vector2.zero;
            e.icon = HeroKit.Ic(e.b.transform, sk.icon, 26, Theme.Muted);
            HeroKit.Center(e.icon.rectTransform, 26);
            e.key = HeroKit.Abs(e.b.transform, skillKey(slot), 12, Color.white, TextAnchor.UpperLeft, new Vector2(4, -1));
            e.keyStr = e.key.text;
            e.mana = HeroKit.Abs(e.b.transform, U.Fmt(sk.mana), 11, Theme.Hex(0x9ac0ff), TextAnchor.LowerRight, new Vector2(-4, 1));
            e.cd = HeroKit.T(e.b.transform, "", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            e.cd.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Fill(e.cd.rectTransform);
            HeroKit.Shadow(e.cd);
            e.noMana = HeroKit.Frame(e.b.transform, new Color(90 / 255f, 140 / 255f, 1f, 0.6f), 0);
            e.target = HeroKit.Frame(e.b.transform, Color.white, -3);
            e.noMana.enabled = false; e.target.enabled = false;
            Ui.Tip(e.b, () =>
            {
                string k = skillKey(slot);
                string tgt = sk.target == "self" ? "selbst" : sk.target == "tower" ? "Turm" : "Bereich";
                string t = $"{sk.name} ({k}) – {sk.description}\nAbklingzeit {U.Fmt(sk.cooldown)} s · Mana {U.Fmt(sk.mana)} · Ziel: {tgt}{(sk.unlockLevel > 1 ? $" · ab Stufe {U.Fmt(sk.unlockLevel)}" : "")}";
                // PORT: zusätzlich der Grund, warum die Fähigkeit gerade nicht geht (TS zeigt ihn nur über Klassen)
                var chk = ChampionSystem.skillState(sim, slot);
                if (!chk.ok && !string.IsNullOrEmpty(chk.reason)) t += "\n" + HeroKit.Col(chk.reason, Theme.Bad);
                return t;
            });
            return e;
        }

        private PotionEl buildPotion(Transform parent, int i)
        {
            var e = new PotionEl();
            e.b = HeroKit.CardBtn(parent, Theme.Hex(0x160f22), Theme.Line, () => { if (e.potion != null) a.usePotion(e.potion); }, out e.bg, out e.border);
            e.b.name = $"Potion{i}";
            Ui.Size(e.b, 46, 46);
            e.glow = e.b.gameObject.AddComponent<Outline>();
            e.glow.effectDistance = new Vector2(3, -3);
            e.glow.effectColor = Color.clear;
            e.icon = HeroKit.Ic(e.b.transform, "heart", 24, Color.white);
            HeroKit.Center(e.icon.rectTransform, 24);
            e.n = HeroKit.Abs(e.b.transform, "", 11, Color.white, TextAnchor.LowerRight, new Vector2(-3, 0));
            e.n.fontStyle = FontStyle.Bold;
            e.k = HeroKit.Abs(e.b.transform, "", 10, Theme.Muted, TextAnchor.UpperLeft, new Vector2(3, 0));
            var ov = Ui.Panel(e.b.transform, "Cd", new Color(0, 0, 0, 0.55f), false, Theme.RoundSmall);
            ov.raycastTarget = false;
            Ui.Fill(ov.rectTransform);
            e.cdOv = ov;
            e.cdT = HeroKit.T(ov.transform, "", 17, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Fill(e.cdT.rectTransform);
            Ui.Tip(e.b, () => e.tip);
            e.b.gameObject.SetActive(false);
            return e;
        }

        private RectTransform buildWin(Transform parent)
        {
            var img = HeroKit.Box(parent, Theme.PanelSolid, Theme.LineStrong, Theme.Round);
            img.name = "HeroWindow";
            var sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f); sh.effectDistance = new Vector2(0, -10);
            var rt = img.rectTransform;
            Ui.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, (stageBottom - stageTop) / 2f), new Vector2(WIN_W, 400));
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 12, 12); v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = img.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ptr = img.gameObject.AddComponent<HeroPointer>();
            ptr.onChange = (held) => pointerHeld = held;
            img.gameObject.SetActive(false);
            return rt;
        }

        // ------------------------------------------------------------ pro Frame

        public void update()
        {
            if (!fittersChecked) checkFitters();
            var s = sim.s;
            var he = s.he;
            if (he == null) return;
            var c = he.champion;
            var st = ChampionSystem.championStats(he);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f);
            updateCharges(he);

            foreach (string r in HeroEndless.RESOURCE_ORDER)
            {
                double v = U.Get(he.resources, r, 0);
                if (resLast[r] != v) { resLast[r] = v; resVals[r].text = HeroKit.fmt(v); }
            }
            double bw = Math.Max(he.bestWaveAtStart, s.stats.wavesCleared);
            if (bw != bestLast) { bestLast = bw; best.text = U.Fmt(bw); }
            int combat = s.phase == "combat" ? 1 : 0;
            if (he.profile != profLastId || combat != profLastCombat)
            {
                profLastId = he.profile; profLastCombat = combat;
                WaveProfile prof = null;
                foreach (var p in HeroEndless.WAVE_PROFILES) if (p.id == he.profile) { prof = p; break; }
                profile.text = ""; // Wellenart steht in der Vorschau „Nächste Welle …“ (Hud-Streifen)
            }
            if (c.level != lvlLast || c.skillPoints != spLast)
            {
                lvlLast = c.level; spLast = c.skillPoints;
                lvl.text = U.Fmt(c.level);
                nameEl.text = $"{def.name} · {def.role}{(c.skillPoints > 0 ? $" · {HeroKit.Col(HeroKit.STAR, Theme.Gold)} {U.Fmt(c.skillPoints)} Punkt{(c.skillPoints > 1 ? "e" : "")}" : "")}";
            }
            if (portraitImg.sprite == null)
            {
                portraitRetry -= Time.unscaledDeltaTime;
                if (portraitRetry <= 0) { portraitRetry = 0.5f; setPortrait(); }
            }
            // Balken
            hp.Set((float)Math.Max(0, c.hp / Math.Max(1, st.maxHp)));
            int ha = (int)Math.Ceiling(c.hp), hb = (int)U.Round(st.maxHp);
            if (ha != hpA || hb != hpB) { hpA = ha; hpB = hb; hp.text.text = $"{ha}/{hb}"; }
            mana.Set(st.maxMana > 0 ? (float)Math.Max(0, c.mana / st.maxMana) : 0);
            int ma = (int)Math.Floor(c.mana), mb = (int)U.Round(st.maxMana);
            if (ma != mnA || mb != mnB) { mnA = ma; mnB = mb; mana.text.text = $"{ma}/{mb}"; }
            var pg = he.paragon;
            if (c.level >= Paragon.PARAGON.heroMaxLevel && pg != null)
            {
                double pneed = Paragon.paragonXpToNext(pg.level);
                bool maxed = pg.level >= Paragon.PARAGON.maxLevel;
                xp.Set(maxed ? 1f : (float)Math.Min(1, pg.xp / pneed));
                double xa = pg.level, xb = maxed ? -1 : U.Round(pg.xp);
                if (xpMode != 1 || xa != xpA || xb != xpB)
                {
                    xpMode = 1; xpA = xa; xpB = xb;
                    xp.text.text = maxed ? $"Paragon {U.Fmt(pg.level)} (max.)" : $"Paragon {U.Fmt(pg.level)} · {HeroKit.fmt(pg.xp)}/{HeroKit.fmt(pneed)}";
                }
            }
            else
            {
                double need = HeroEndless.xpToNext(c.level);
                xp.Set((float)Math.Min(1, c.xp / need));
                double xa = Math.Floor(c.xp);
                if (xpMode != 0 || xa != xpA || need != xpB)
                {
                    xpMode = 0; xpA = xa; xpB = need;
                    xp.text.text = $"EP {U.Fmt(xa)}/{U.Fmt(need)}";
                }
            }
            // Zustände der Leiste (tot, gehalten, Knöpfe)
            var mining = he.mining;
            bool dead = c.dead > 0;
            bool held = c.holdPos == true;
            bool near = InventorySystem.nearShop(sim);
            nearLast = near;
            int ds = (dead ? 1 : 0) | (held ? 2 : 0) | (near ? 4 : 0) | (follow ? 8 : 0);
            if (ds != dockState)
            {
                dockState = ds;
                // PORT: CSS saturate(0.4) → Porträt grau, Rahmen gedämpft; gestrichelter Rahmen (gehalten) → halbtransparent
                portraitImg.color = dead ? new Color(0.55f, 0.55f, 0.55f, 1) : Color.white;
                dockBorder.effectColor = HeroKit.A(dead ? Color.Lerp(hc, Color.gray, 0.6f) : hc, held ? 0.45f : 1f);
                HeroKit.On(shopBtn, near);
                HeroKit.On(followBtn, follow);
                HeroKit.On(holdBtn, held);
            }
            bool showNote = dead || mining != null;
            if (deadNote.gameObject.activeSelf != showNote) deadNote.gameObject.SetActive(showNote);
            if (dead)
            {
                int v = (int)Math.Ceiling(c.dead);
                if (deadLast != v) { deadLast = v; deadNote.text = $"Gefallen – Wiederbelebung am Kern in {v} s"; }
            }
            else if (mining != null)
            {
                int v = -1 - (int)Math.Ceiling(mining.t);
                if (deadLast != v) { deadLast = v; deadNote.text = $"Baut ab – noch {(int)Math.Ceiling(mining.t)} s (nicht unterbrechbar)"; }
            }
            // .btn.glow (Skillpunkte frei)
            var sbo = sheetBtn.GetComponent<Outline>();
            if (c.skillPoints > 0) sbo.effectColor = Color.Lerp(HeroKit.A(Theme.Gold, 0.55f), Theme.Gold, pulse);
            else if (sbo.effectColor != Theme.Line) sbo.effectColor = Theme.Line;

            updateBuffs(c);
            updateSkills(c, st, pulse);
            updatePotions(c);
            updateWindow();
        }

        private void updateBuffs(ChampionState c)
        {
            int n = c.buffs.Count;
            if (buffs.gameObject.activeSelf != (n > 0)) buffs.gameObject.SetActive(n > 0);
            while (buffEls.Count < n) buffEls.Add(buildBuff(buffEls.Count));
            for (int i = 0; i < buffEls.Count; i++)
            {
                var e = buffEls[i];
                if (i >= n) { if (e.go.activeSelf) e.go.SetActive(false); e.buff = null; continue; }
                var b = c.buffs[i];
                if (!e.go.activeSelf) e.go.SetActive(true);
                e.buff = b;
                if (!ReferenceEquals(e.src, b.src))
                {
                    e.src = b.src;
                    string icon = "star";
                    string name = b.src;
                    if (b.src.StartsWith("potion:", StringComparison.Ordinal))
                    {
                        PotionDefinition pd;
                        if (Items.POTIONS.TryGetValue(b.src.Substring(7), out pd) && pd != null) { name = pd.name; icon = HeroKit.IconName(pd.icon); }
                    }
                    else
                    {
                        foreach (var k in def.skills) if (k.id == b.src) { name = k.name; break; }
                    }
                    e.name = name;
                    e.icon.sprite = HeroKit.Spr(icon);
                }
                int t = (int)Math.Ceiling(b.t);
                if (t != e.t) { e.t = t; e.text.text = $"{t} s"; }
            }
        }

        private BuffEl buildBuff(int i)
        {
            var e = new BuffEl();
            var img = HeroKit.Box(buffs, new Color(15 / 255f, 11 / 255f, 25 / 255f, 0.8f), hc, Theme.Round);
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(7, 8, 1, 1); h.spacing = 3; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Ui.Size(img, -1, 24);
            e.go = img.gameObject;
            e.icon = HeroKit.Ic(img.transform, "star", 14, Color.white);
            e.text = HeroKit.T(img.transform, "", 13, Theme.Text);
            e.text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Tip(img, () =>
            {
                var b = e.buff;
                if (b == null) return "";
                var parts = new List<string>();
                foreach (var kv in b.add) parts.Add($"{statName(kv.Key)} {statDelta(kv.Key, kv.Value)}");
                return $"{e.name}: {string.Join(", ", parts)}";
            });
            return e;
        }

        private void updateSkills(ChampionState c, ChampionStats st, float pulse)
        {
            keyTimer -= Time.unscaledDeltaTime;
            bool keys = keyTimer <= 0;
            if (keys) keyTimer = 1f;
            for (int slot = 0; slot < skillEls.Count; slot++)
            {
                var e = skillEls[slot];
                var sk = def.skills[slot];
                var chk = ChampionSystem.skillState(sim, slot);
                double cdv = slot < c.skillCd.Count ? c.skillCd[slot] : 0;
                bool locked = c.level < sk.unlockLevel;
                bool ready = chk.ok;
                bool nomana = !locked && cdv <= 0 && c.mana < sk.mana;
                bool tg = (int)targeting == slot && targeting >= 0;
                int state = (ready ? 1 : 0) | (locked ? 2 : 0) | (nomana ? 4 : 0) | (tg ? 8 : 0);
                if (state != e.state)
                {
                    e.state = state;
                    // Gesperrt: nicht mehr durchscheinend (das färbte den Knopf rosa), sondern dunkel mit gut lesbarer Stufe
                    e.cg.alpha = 1f;
                    e.bg.color = locked ? Theme.Hex(0x15111f) : Theme.Hex(0x1a1428);
                    e.fill.enabled = !locked;
                    e.cd.color = locked ? Theme.Hex(0xc9bfe0) : Color.white;
                    e.icon.color = locked ? Color.clear : ready ? Color.white : Theme.Muted;
                    e.noMana.enabled = nomana;
                    e.target.enabled = tg;
                    if (!ready) { e.border.effectColor = Theme.LineStrong; e.glow.effectColor = Color.clear; }
                }
                if (ready)
                {
                    // .sk.ready: Rahmen in Heldenfarbe + Schein; .sk.ult.ready: Gold, pulsierend (perkGlow)
                    if (e.ult) { e.border.effectColor = Theme.Gold; e.glow.effectColor = HeroKit.A(Theme.Gold, 0.15f + 0.45f * pulse); }
                    else { e.border.effectColor = hc; e.glow.effectColor = HeroKit.A(hc, 0.4f); }
                }
                double total = Math.Max(0.1, sk.cooldown * (1 - st.cdr));
                double p = cdv > 0 ? 1 - cdv / total : 1;
                float f = (float)(U.Round(Math.Max(0, Math.Min(1, p)) * 100) / 100);
                if (f != e.fillLast) { e.fillLast = f; e.fill.rectTransform.anchorMax = new Vector2(1, f); }
                int cdCode = locked ? -1000 - (int)sk.unlockLevel : cdv > 0 ? (int)Math.Ceiling(cdv) : 0;
                if (cdCode != e.cdLast)
                {
                    e.cdLast = cdCode;
                    e.cd.text = locked ? $"St. {U.Fmt(sk.unlockLevel)}" : cdv > 0 ? $"{(int)Math.Ceiling(cdv)}" : "";
                }
                if (keys)
                {
                    string k = skillKey(slot);
                    if (k != e.keyStr) { e.keyStr = k; e.key.text = k; }
                }
            }
        }

        private void updatePotions(ChampionState c)
        {
            var sg = new HeroSig();
            foreach (var p in c.potions) { sg.Add(p.potion); sg.Add(p.count); }
            sg.Add(c.specialPotion != null ? c.specialPotion.potion : null);
            sg.Add(c.specialPotion != null ? c.specialPotion.count : -1);
            foreach (var kv in c.potionCd) { sg.Add(kv.Key); sg.Add(Math.Ceiling(kv.Value)); }
            if (!potionDirty && sg.h == potionSig) return;
            potionDirty = false;
            potionSig = sg.h;
            int n = 0;
            int stacks = Math.Min(4, c.potions.Count);
            for (int i = 0; i < stacks; i++) fillPotion(potionEls[n++], c, c.potions[i].potion, c.potions[i].count, POTION_KEYS[i], POTION_KEY_TIPS[i], false);
            if (c.specialPotion != null) fillPotion(potionEls[n++], c, c.specialPotion.potion, c.specialPotion.count, "↑5", "Umschalt+5", true);
            for (int i = n; i < potionEls.Count; i++) { potionEls[i].potion = null; potionEls[i].b.gameObject.SetActive(false); }
            noPotions.gameObject.SetActive(n == 0);
            // flex-wrap nachbilden: passen Tränke und Knöpfe nicht in eine Zeile, rücken die Knöpfe in die zweite
            float potW = n == 0 ? noPotions.preferredWidth : n * 46 + (n - 1) * 5;
            float btnW = 0;
            for (int i = 0; i < btnRow.childCount; i++)
            {
                var le = btnRow.GetChild(i).GetComponent<LayoutElement>();
                btnW += (le != null ? le.preferredWidth : 40) + (i > 0 ? 5 : 0);
            }
            bool wrap = potW + 6 + btnW > 396 - 14;
            Transform want = wrap ? (Transform)footRow2 : footRow1;
            if (btnRow.parent != want) btnRow.SetParent(want, false);
            footRow2.gameObject.SetActive(wrap);
        }

        private void fillPotion(PotionEl e, ChampionState c, string potion, double count, string key, string keyTip, bool special)
        {
            var d = Items.POTIONS[potion];
            var ps = InventorySystem.potionState(sim, potion);
            double cd = U.Get(c.potionCd, potion, 0);
            Color pc = Theme.Hex(d.color);
            e.potion = potion;
            e.b.gameObject.SetActive(true);
            e.icon.sprite = HeroKit.Spr(HeroKit.IconName(d.icon));
            e.icon.color = pc;
            e.border.effectColor = special ? Theme.Gold : HeroKit.A(pc, 0.6f);
            e.glow.effectColor = ps.ok ? HeroKit.A(pc, 0.22f) : Color.clear;
            e.glow.effectDistance = new Vector2(2, -2);
            e.bg.color = Theme.Hex(0x160f22);
            e.n.text = $"×{U.Fmt(count)}";
            e.k.text = key;
            bool showCd = cd > 0;
            e.cdOv.gameObject.SetActive(showCd);
            if (showCd) e.cdT.text = U.Fmt(Math.Ceiling(cd));
            e.tip = $"{d.name} ({keyTip}) – {d.description}{(special ? " · im Spezialslot" : "")}";
        }

        private bool fittersChecked;

        /// <summary>Nach dem Umhängen in Layout-Gruppen (statsBox, stageLeft) steuert der Elternteil die Größe.</summary>
        private void checkFitters()
        {
            fittersChecked = true;
            foreach (var rt in new[] { resBar, dock })
            {
                if (rt == null || rt.parent == null || rt.parent.GetComponent<LayoutGroup>() == null) continue;
                var f = rt.GetComponent<ContentSizeFitter>();
                if (f != null) UnityEngine.Object.Destroy(f);
            }
        }

        /// <summary>Liegt das Fenster direkt unter einer App-Ebene (nicht in hud.stage)?</summary>
        private bool winInLayer()
        {
            var app = GameApp.I;
            if (app == null) return true;
            var p = win.parent;
            return p == null || p == app.hudLayer || p == app.overlayLayer || p == app.root;
        }

        // ------------------------------------------------------------ Fenster

        private void updateWindow()
        {
            bool show = !string.IsNullOrEmpty(kind);
            if (win.gameObject.activeSelf != show) win.gameObject.SetActive(show);
            if (!show) return;
            var s = sim.s;
            var he = s.he;
            if (he == null) return;
            var c = he.champion;
            var g = new HeroSig();
            g.Add(kind); g.Add(sheetTab); g.Add(shopTab); g.Add(selectedItem); g.Add(s.gold);
            foreach (var kv in he.resources) { g.Add(kv.Key); g.Add(kv.Value); }
            g.Add(c.level); g.Add(c.skillPoints);
            foreach (var kv in c.tree) { g.Add(kv.Key); g.Add(kv.Value); }
            if (he.paragon != null) { g.Add(he.paragon.level); g.Add(he.paragon.rev); g.Add(Math.Floor(he.paragon.xp / 500)); } else g.Add(-1);
            g.Add(s.phase);
            foreach (var i in c.inventory) { g.Add(i.uid); g.Add(i.item); g.Add(i.plus); }
            foreach (var e in c.equipment) g.Add(e.HasValue ? e.Value : -1e300);
            foreach (var p in c.potions) { g.Add(p.potion); g.Add(p.count); }
            g.Add(c.specialPotion != null ? c.specialPotion.potion : ""); g.Add(c.specialPotion != null ? c.specialPotion.count : 0);
            foreach (var kv in he.fragments) { g.Add(kv.Key); g.Add(kv.Value); }
            foreach (var kv in he.shopStock) { g.Add(kv.Key); g.Add(kv.Value); }
            g.Add(InventorySystem.nearShop(sim)); g.Add(s.wave);
            foreach (var u in he.runUnlocks) g.Add(u);
            if (!winDirty && g.h == winSig) return;
            // Nicht neu aufbauen, solange im Fenster eine Taste/ein Finger gedrückt ist (sonst gehen Klicks verloren)
            if (pointerHeld && !winDirty) return;
            winSig = g.h;
            winDirty = false;
            rebuildWindow();
        }

        const string NOT_AT_SHOP = "Held ist nicht am Laden";

        private void rebuildWindow()
        {
            var s = sim.s;
            var he = s.he;
            var c = he.champion;
            string scrollKey = kind + "|" + (kind == "sheet" ? sheetTab : shopTab);
            float keepY = bodyScroll != null && scrollKey == lastScrollKey ? bodyScroll.content.anchoredPosition.y : 0;
            lastScrollKey = scrollKey;
            HeroKit.Wipe(win);
            bodyScroll = null;
            if (kind == "sheet")
            {
                double pFree = ChampionSystem.paragonFree(he);
                head($"{Champions.CHAMPIONS[c.id].name} – Stufe {U.Fmt(c.level)}/{U.Fmt(Paragon.PARAGON.heroMaxLevel)}{(he.paragon != null && U.Truthy(he.paragon.level) ? $" · Paragon {U.Fmt(he.paragon.level)}" : "")}");
                tabs(new[]
                {
                    ("gear", "Ausrüstung"), ("stats", "Werte"), ("tree", $"Skillbaum{(U.Truthy(c.skillPoints) ? $" ({U.Fmt(c.skillPoints)})" : "")}"),
                    // Aufgeräumt: „Passiv“ entfernt – der passive Baum wird im Startbildschirm vergeben und ist dort zu sehen
                    ("paragon", $"Paragon{(U.Truthy(pFree) ? $" ({U.Fmt(pFree)})" : "")}"), ("collection", "Sammlung"), ("progress", "Fortschritt"),
                }, sheetTab, (t) => sheetTab = t);
                var body = makeBody();
                if (sheetTab == "gear") renderGear(body, false);
                else if (sheetTab == "stats") renderStats(body);
                else if (sheetTab == "tree") renderTree(body);
                else if (sheetTab == "paragon") renderParagon(body);
                else if (sheetTab == "collection") renderCollection(body);
                else if (sheetTab == "passive")
                {
                    HeroKit.P(body, "Stand dieses Laufs (beim Start übernommen). Punkte vergibst du im Startbildschirm des Helden-Endlosmodus.", 15, Theme.Muted);
                    PassiveTreeView.renderPassiveTree(body, he.passive ?? new Dictionary<string, double>(), he.bestWaveAtStart, false, null, null, BODY_W);
                }
                else renderProgress(body);
            }
            else
            {
                bool near = InventorySystem.nearShop(sim);
                head("Laden & Schmiede");
                if (!near)
                {
                    var n = HeroKit.HBox(win, new Color(1f, 170 / 255f, 60 / 255f, 0.12f), Theme.Warn, 12, 8, 10);
                    var t = HeroKit.P(n.transform, "Der Held ist nicht am Laden. Angebote ansehen ist möglich, Kaufen, Verkaufen und Herstellen nur in Reichweite. ", 15, Theme.Text);
                    Ui.Size(t, -1, -1, 1);
                    HeroKit.Btn(n.transform, "Zum Laden laufen", () => a.moveToShop());
                }
                var list = new List<(string, string)>();
                foreach (var x in Items.SHOP_CATEGORIES) list.Add((x.id, x.name));
                list.Add(("forge", "Schmiede"));
                list.Add(("sell", "Verkaufen"));
                tabs(list.ToArray(), shopTab, (t) => shopTab = t);
                var body = makeBody();
                if (shopTab == "forge") renderForge(body);
                else if (shopTab == "sell") renderGear(body, true);
                else if (shopTab == "collection") renderCollectionShop(body);
                else renderOffers(body, shopTab);
                var wallet = Ui.Column(win, 4);
                var line = Ui.Panel(wallet.transform, "Line", Theme.Line, false, null);
                line.sprite = null;
                Ui.Size(line, -1, 1);
                var sb = new System.Text.StringBuilder();
                sb.Append($"Gold {HeroKit.fmt(s.gold)} · ");
                foreach (string r in HeroEndless.RESOURCE_ORDER)
                    sb.Append(HeroKit.Col($"{HeroEndless.RESOURCES[r].name} {U.Fmt(U.Get(he.resources, r, 0))}", HeroKit.Hex6(HeroEndless.RESOURCES[r].color))).Append("   ");
                HeroKit.P(wallet.transform, sb.ToString(), 14, Theme.Muted);
            }
            layoutWindow(keepY);
        }

        private void head(string title)
        {
            var row = Ui.Row(win, 8, TextAnchor.MiddleLeft, false);
            var t = HeroKit.T(row.transform, title, 22, Theme.Text, FontStyle.Bold);
            Ui.Size(t, -1, -1, 1);
            var b = HeroKit.Btn(row.transform, null, () => close(), Ui.BtnKind.Normal, true, "close", 16, 36);
            HeroKit.Tip(b, "Schließen (Esc)");
        }

        private void tabs((string id, string name)[] list, string cur, Action<string> set)
        {
            // .seg.he-tabs: umbrechend
            var wrap = new HeroGrid.Wrap(win, BODY_W + 6, 5);
            foreach (var (id, name) in list)
            {
                string tid = id;
                var b = HeroKit.Btn(wrap.parentFor(HeroKit.BtnWidth(name, 16)), name, () => { set(tid); winDirty = true; }, tid == cur ? Ui.BtnKind.On : Ui.BtnKind.Normal);
                if (tid == cur) HeroKit.Label(b).color = Theme.Gold;
            }
        }

        private RectTransform makeBody()
        {
            ScrollRect sr;
            var content = Ui.Scroll(win, out sr, 6, new RectOffset(0, 6, 2, 6));
            bodyScroll = sr;
            bodyLe = Ui.Size(sr, -1, 0);
            return content;
        }

        private void layoutWindow(float keepY)
        {
            if (bodyScroll == null) { LayoutRebuilder.ForceRebuildLayoutImmediate(win); return; }
            bodyLe.preferredHeight = 0; bodyLe.minHeight = 0;
            LayoutRebuilder.ForceRebuildLayoutImmediate(win);
            float chrome = LayoutUtility.GetPreferredHeight(win);
            var content = bodyScroll.content;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float ch = LayoutUtility.GetPreferredHeight(content);
            var prt = win.parent as RectTransform;
            float avail = prt != null && prt.rect.height > 0 ? prt.rect.height : 1080;
            bool layer = winInLayer();
            avail -= (layer ? stageTop + stageBottom : 0) + 20;
            win.anchoredPosition = new Vector2(0, layer ? (stageBottom - stageTop) / 2f : 0);
            // Feste Fensterhöhe: Reiter und Schließen-Knopf bleiben beim Umschalten an derselben Stelle
            float bh = Mathf.Max(140, Mathf.Min(avail - chrome, 640));
            bodyLe.preferredHeight = bh; bodyLe.minHeight = bh;
            LayoutRebuilder.ForceRebuildLayoutImmediate(win);
            float y = Mathf.Clamp(keepY, 0, Mathf.Max(0, ch - bh));
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);
        }

        // ------------------------------------------------------------ Inhalte

        private Button itemCard(Transform parent, ItemInstance inst, int equippedSlot)
        {
            var d = Items.ITEMS[inst.item];
            var r = Items.RARITY[d.rarity];
            Color rc = HeroKit.Css(r.color, Theme.Hex(0xc8c8d0));
            double uid = inst.uid;
            Image bg; Outline border;
            var b = HeroKit.CardBtn(parent, HeroKit.NodeBg, HeroKit.A(rc, 0.7f), () => { selectedItem = selectedItem == uid ? 0 : uid; winDirty = true; }, out bg, out border);
            if (selectedItem == inst.uid) { var o = b.gameObject.AddComponent<Outline>(); o.effectColor = rc; o.effectDistance = new Vector2(2.5f, -2.5f); }
            itemBody(b.transform, rc, HeroKit.IconName(d.icon), d.name, inst.plus > 0 ? $"+{U.Fmt(inst.plus)}" : null, equippedSlot >= 0);
            HeroKit.Tip(b, $"{d.name}{(inst.plus > 0 ? $" +{U.Fmt(inst.plus)}" : "")} ({r.name}) – {d.description}");
            return b;
        }

        /// <summary>Inhalt einer .item-Kachel: Farbleiste links, Symbol, Name, Zusatz (gold), Häkchen für angelegt.</summary>
        private static void itemBody(Transform t, Color rc, string icon, string name, string extra, bool worn, string iconText = null)
        {
            var h = t.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 7, 4, 4); h.spacing = 5; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            var le = Ui.Get<LayoutElement>(t.gameObject); le.minHeight = 40;
            var bar = Ui.Panel(t, "Rar", rc, false, Theme.RoundSmall);
            bar.raycastTarget = false;
            Ui.Get<LayoutElement>(bar.gameObject).ignoreLayout = true;
            var br = bar.rectTransform; br.anchorMin = Vector2.zero; br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 0.5f); br.offsetMin = Vector2.zero; br.offsetMax = new Vector2(4, 0);
            if (iconText != null) { var q = HeroKit.T(t, iconText, 19, Theme.Muted, FontStyle.Bold, TextAnchor.MiddleCenter); Ui.Size(q, 22, 22); }
            else HeroKit.Ic(t, icon, 22, rc);
            var n = HeroKit.Line(t, name, 15, Theme.Text);
            Ui.Size(n, -1, -1, 1);
            if (!string.IsNullOrEmpty(extra)) { var p = HeroKit.T(t, extra, 12, Theme.Gold, FontStyle.Bold); p.horizontalOverflow = HorizontalWrapMode.Overflow; }
            if (worn)
            {
                var w = HeroKit.Abs(t, "√", 12, Theme.Good, TextAnchor.UpperRight, new Vector2(-4, 0));
                w.fontStyle = FontStyle.Bold;
            }
        }

        private void renderGear(Transform body, bool selling)
        {
            var he = sim.s.he;
            if (he == null) return;
            var c = he.champion;
            var byUid = new Dictionary<double, ItemInstance>();
            foreach (var i in c.inventory) byUid[i.uid] = i;
            if (!selling)
            {
                HeroKit.H3(body, "Ausrüstung");
                var grid = new HeroGrid(body, BODY_W, 100, 6, 3);
                for (int i = 0; i < Items.SLOTS.Count; i++)
                {
                    var sl = Items.SLOTS[i];
                    double? uid = i < c.equipment.Count ? c.equipment[i] : null;
                    ItemInstance inst = null;
                    if (uid.HasValue) byUid.TryGetValue(uid.Value, out inst);
                    bool potionSlot = i == 5 && c.specialPotion != null;
                    var slot = HeroKit.Box(grid.Next(), new Color(0, 0, 0, 0.12f), potionSlot || inst != null ? Theme.LineStrong : HeroKit.A(Theme.LineStrong, 0.55f), Theme.Round);
                    grid.Fit(slot);
                    var v = slot.gameObject.AddComponent<VerticalLayoutGroup>();
                    v.padding = new RectOffset(5, 5, 5, 5); v.spacing = 3;
                    v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                    Ui.Get<LayoutElement>(slot.gameObject).minHeight = 72;
                    var lab = Ui.Row(slot.transform, 4, TextAnchor.MiddleLeft, false);
                    if (!potionSlot) HeroKit.Ic(lab.transform, HeroKit.IconName(sl.icon), 13, Theme.Muted);
                    HeroKit.T(lab.transform, sl.name, 13, Theme.Muted);
                    if (potionSlot)
                    {
                        var p = Items.POTIONS[c.specialPotion.potion];
                        Image bg; Outline bo;
                        var b = HeroKit.CardBtn(slot.transform, HeroKit.NodeBg, HeroKit.A(Theme.Hex(p.color), 0.7f), () => a.unequip(5), out bg, out bo);
                        itemBody(b.transform, Theme.Hex(p.color), HeroKit.IconName(p.icon), $"{p.name} ×{U.Fmt(c.specialPotion.count)}", null, true);
                        HeroKit.Tip(b, $"{p.name} ×{U.Fmt(c.specialPotion.count)} – Klick legt ihn zurück");
                    }
                    else if (inst != null) itemCard(slot.transform, inst, i);
                    else HeroKit.T(slot.transform, "leer", 13, Theme.Muted);
                }
            }
            HeroKit.H3(body, $"Inventar {c.inventory.Count}/{U.Fmt(HeroEndless.HERO_ENDLESS.inventorySlots + 6)}");
            if (c.inventory.Count > 0)
            {
                var grid = new HeroGrid(body, BODY_W, 125, 5);
                foreach (var i in c.inventory)
                {
                    var b = itemCard(grid.Next(), i, c.equipment.IndexOf(i.uid));
                    grid.Fit(b);
                }
            }
            else HeroKit.P(body, "Noch keine Gegenstände. Gegner lassen Fragmente und seltene Items fallen; Fragmente werden in der Schmiede zu Items.", 15, Theme.Muted);
            ItemInstance sel;
            if (byUid.TryGetValue(selectedItem, out sel)) itemDetail(body, sel, selling);
            if (!selling)
            {
                HeroKit.H3(body, $"Tränke ({c.potions.Count}/{U.Fmt(HeroEndless.HERO_ENDLESS.potionStacks)} Stapel)");
                if (c.potions.Count > 0)
                {
                    var grid = new HeroGrid(body, BODY_W, 190, 5);
                    foreach (var p in c.potions)
                    {
                        var d = Items.POTIONS[p.potion];
                        Color pc = Theme.Hex(d.color);
                        string pid = p.potion;
                        var box = HeroKit.Box(grid.Next(), HeroKit.NodeBg, HeroKit.A(pc, 0.7f), Theme.RoundSmall);
                        grid.Fit(box);
                        HeroKit.Tip(box, d.description);
                        var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
                        v.padding = new RectOffset(10, 7, 4, 5); v.spacing = 4;
                        v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                        var bar = Ui.Panel(box.transform, "Rar", pc, false, Theme.RoundSmall);
                        bar.raycastTarget = false;
                        Ui.Get<LayoutElement>(bar.gameObject).ignoreLayout = true;
                        var brt = bar.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(0, 1); brt.pivot = new Vector2(0, 0.5f); brt.offsetMin = Vector2.zero; brt.offsetMax = new Vector2(4, 0);
                        var top = Ui.Row(box.transform, 5, TextAnchor.MiddleLeft, false);
                        HeroKit.Ic(top.transform, HeroKit.IconName(d.icon), 22, pc);
                        var nm = HeroKit.Line(top.transform, $"{d.name} ×{U.Fmt(p.count)}/{U.Fmt(d.stack)}", 15, Theme.Text);
                        Ui.Size(nm, -1, -1, 1);
                        var btns = Ui.Row(box.transform, 5, TextAnchor.MiddleLeft, false);
                        HeroKit.Btn(btns.transform, "Nutzen", () => a.usePotion(pid));
                        var sb = HeroKit.Btn(btns.transform, "Spezialslot", () => a.slotPotion(pid));
                        HeroKit.Tip(sb, "In den Spezialslot legen (Schnellzugriff Umschalt+5)");
                        HeroKit.Enable(sb, !(c.equipment.Count > 5 && c.equipment[5] != null));
                    }
                }
                else HeroKit.P(body, "Keine Tränke.", 15, Theme.Muted);
            }
        }

        private void itemDetail(Transform body, ItemInstance inst, bool selling)
        {
            var he = sim.s.he;
            if (he == null) return;
            var c = he.champion;
            var d = Items.ITEMS[inst.item];
            int slotIdx = Items.SLOTS.FindIndex((x) => x.slot == d.slot);
            int worn = c.equipment.IndexOf(inst.uid);
            double? otherUid = slotIdx >= 0 && slotIdx < c.equipment.Count ? c.equipment[slotIdx] : null;
            ItemInstance other = otherUid.HasValue && otherUid.Value != inst.uid ? c.inventory.Find((i) => i.uid == otherUid.Value) : null;
            var mine = Items.itemStats(inst.item, inst.plus);
            var theirs = other != null ? Items.itemStats(other.item, other.plus) : new Dictionary<string, double>();
            var keys = new List<string>(mine.Keys);
            foreach (var k in theirs.Keys) if (!keys.Contains(k)) keys.Add(k);
            bool allowed = d.champions == null || d.champions.Contains(c.id);
            var up = InventorySystem.upgradeState(sim, inst.uid);
            var rar = Items.RARITY[d.rarity];
            Color rc = HeroKit.Css(rar.color, Color.white);
            var box = HeroKit.Box(body, new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.4f), rc, Theme.Round);
            var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 9, 9); v.spacing = 5;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var h4 = Ui.Row(box.transform, 6, TextAnchor.MiddleLeft, false);
            HeroKit.Ic(h4.transform, HeroKit.IconName(d.icon), 22, rc);
            var ht = HeroKit.T(h4.transform, $"<b>{d.name}{(inst.plus > 0 ? $" +{U.Fmt(inst.plus)}" : "")}</b> <size=14>{HeroKit.Col($"{rar.name} · {(slotIdx >= 0 ? Items.SLOTS[slotIdx].name : "")}", rc)}</size>", 18, Theme.Text);
            Ui.Size(ht, -1, -1, 1);
            string only = "";
            if (d.champions != null)
            {
                var names = new List<string>();
                foreach (var x in d.champions) names.Add(Champions.CHAMPIONS[x].name);
                only = $" Nur für: {string.Join(", ", names)}.";
            }
            HeroKit.P(box.transform, d.description + only, 15, Theme.Muted);
            foreach (var k in keys)
            {
                double m = U.Get(mine, k, 0), th = U.Get(theirs, k, 0);
                double delta = m - th;
                var row = Ui.Row(box.transform, 8, TextAnchor.MiddleLeft, false);
                var c1 = HeroKit.T(row.transform, statName(k), 15, Theme.Text); Ui.Size(c1, 230);
                var c2 = HeroKit.T(row.transform, statDelta(k, m), 15, Theme.Text); Ui.Size(c2, 100);
                if (other != null)
                {
                    var c3 = HeroKit.T(row.transform, delta == 0 ? "±0" : statDelta(k, delta), 15, delta > 0 ? Theme.Good : delta < 0 ? Theme.Bad : Theme.Muted);
                    Ui.Size(c3, 100);
                }
            }
            if (other != null) HeroKit.P(box.transform, $"Vergleich mit angelegtem {Items.ITEMS[other.item].name}", 13, Theme.Muted);
            var btns = new HeroGrid.Wrap(box.transform, BODY_W - 24, 6);
            double uid = inst.uid;
            if (selling)
            {
                string lbl = $"Verkaufen ({costText(Items.sellValue(inst.item, inst.plus))})";
                HeroKit.Btn(btns.parentFor(HeroKit.BtnWidth(lbl, 18)), lbl, () => { a.shopSell(uid); selectedItem = 0; }, Ui.BtnKind.Danger, false);
            }
            else if (worn >= 0)
            {
                int w = worn;
                HeroKit.Btn(btns.parentFor(HeroKit.BtnWidth("Ablegen", 18)), "Ablegen", () => a.unequip(w), Ui.BtnKind.Normal, false);
            }
            else
            {
                var eb = HeroKit.Btn(btns.parentFor(HeroKit.BtnWidth("Anlegen", 18)), "Anlegen", () => a.equip(uid), Ui.BtnKind.Primary, false);
                HeroKit.Enable(eb, allowed);
                if (!allowed) HeroKit.Tip(eb, "Dieser Held kann das Item nicht tragen");
            }
            if (inst.plus < Items.MAX_PLUS)
            {
                string lbl = $"Verstärken +{U.Fmt(inst.plus + 1)} ({costText(up.cost)})";
                var ub = HeroKit.Btn(btns.parentFor(HeroKit.BtnWidth(lbl, 18)), lbl, () => a.upgradeItem(uid), Ui.BtnKind.Normal, false);
                HeroKit.Enable(ub, up.ok);
                if (!string.IsNullOrEmpty(up.reason)) HeroKit.Tip(ub, up.reason);
            }
        }

        private void renderStats(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var st = ChampionSystem.championStats(he);
            var c = he.champion;
            foreach (var kv in Champions.STAT_NAMES)
            {
                var row = Ui.Row(body, 8, TextAnchor.MiddleLeft, false);
                var c1 = HeroKit.T(row.transform, kv.Value, 16, Theme.Text); Ui.Size(c1, 300);
                HeroKit.T(row.transform, statValue(kv.Key, st[kv.Key]), 16, Theme.Text, FontStyle.Bold);
            }
            HeroKit.P(body, $"Abschüsse {U.Fmt(c.kills)} · Schaden {HeroKit.fmt(c.damageDealt)} · EP gesammelt {HeroKit.fmt(he.stats.xpCollected)} · Tränke {U.Fmt(he.stats.potionsUsed)} · hergestellt {U.Fmt(he.stats.itemsCrafted)}", 15, Theme.Muted);
        }

        private void renderTree(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var c = he.champion;
            var tree = Champions.CHAMPIONS[c.id].tree;
            double spentTree = 0;
            foreach (var n in tree) spentTree += U.Get(c.tree, n.id, 0);
            var row = Ui.Row(body, 8, TextAnchor.MiddleLeft, false);
            var p = HeroKit.T(row.transform, $"Fertigkeitspunkte: <b>{U.Fmt(c.skillPoints)}</b> · 1 Punkt je Stufe, Bonuspunkt alle {U.Fmt(HeroEndless.HERO_ENDLESS.bonusPointEvery)} Stufen.", 15, Theme.Text);
            Ui.Size(p, -1, -1, 1);
            if (spentTree > 0)
            {
                var rb = HeroKit.Btn(row.transform, "Skillbaum zurücksetzen", () => a.treeReset());
                HeroKit.Tip(rb, $"Alle {U.Fmt(spentTree)} vergebenen Punkte kostenlos zurückgeben – jederzeit möglich, wird dauerhaft gespeichert");
            }
            var tiers = new List<double>();
            foreach (var n in tree) if (!tiers.Contains(n.tier)) tiers.Add(n.tier);
            U.StableSort(tiers, (x, y) => x - y);
            foreach (double tier in tiers)
            {
                HeroKit.H3(body, $"Stufe {U.Fmt(tier)}");
                var grid = new HeroGrid(body, BODY_W, 165, 5);
                foreach (var n in tree) if (n.tier == tier) grid.Fit(nodeCard(grid.Next(), n));
            }
        }

        private void renderParagon(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var c = he.champion;
            var p = he.paragon ?? new HeroEndlessParagon { level = 0, xp = 0, points = new Dictionary<string, double>(), rev = 0 };
            double free = ChampionSystem.paragonFree(he);
            int pathLim = Paragon.pathLimit(he.bestWaveAtStart);
            var activeP = Paragon.activePaths(p.points, pathLim);
            var fx = PassiveTree.paragonEffects(p.points, null, activeP);
            var lines = PassiveTreeView.effectLines(fx);
            HeroKit.P(body, $"Paragon <b>{U.Fmt(p.level)}/{U.Fmt(Paragon.PARAGON.maxLevel)}</b> · frei <b>{U.Fmt(free)}</b> · gemeinsam für alle Helden, dauerhaft. Punkte: {U.Fmt(p.level)} aus Paragonstufen (ab Heldenstufe {U.Fmt(Paragon.PARAGON.heroMaxLevel)}) + {U.Fmt(Paragon.paragonPrePoints(c.level))} Vorab-Punkte (ab Stufe 50 alle 5 Stufen, jetzt Stufe {U.Fmt(c.level)}) + {U.Fmt(he.paragonBonus ?? 0)} aus Meilensteinen (W100/150/200/250/300).", 15, Theme.Text);
            HeroKit.P(body, $"Pfade: höchstens <b>{pathLim}</b> wirken gleichzeitig (die mit den meisten Punkten){(pathLim < 3 ? $", ein dritter ab Bestwelle {U.Fmt(Paragon.THIRD_PATH_WAVE)}" : "")}. Spezialisiere dich – Zurücksetzen ist kostenlos.", 13, Theme.Muted);
            if (lines.Count > 0) HeroKit.P(body, $"Wirkung: {string.Join(" · ", lines)}", 13, Theme.Muted);
            var grid = new HeroGrid(body, BODY_W, 242, 10);
            foreach (var t in Paragon.PARAGON_TREES)
            {
                Color pc = HeroKit.Css(t.color, Theme.Gold);
                var nodes = Paragon.PARAGON_NODES.FindAll((n) => n.tree == t.id);
                double spent = 0;
                foreach (var n in nodes) spent += U.Get(p.points, n.id, 0);
                var box = HeroKit.Box(grid.Next(), new Color(1, 1, 1, 0.02f), pc, Theme.Round);
                grid.Fit(box);
                var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
                v.padding = new RectOffset(9, 9, 7, 7); v.spacing = 5;
                v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
                var h = Ui.Row(box.transform, 5, TextAnchor.MiddleLeft, false);
                HeroKit.Ic(h.transform, HeroKit.IconName(t.icon), 18, pc);
                bool act = activeP.Contains(t.id);
                string stTag = spent <= 0 ? "" : act ? " · aktiv" : " · ruht";
                HeroKit.T(h.transform, $"<b>{HeroKit.Col(t.name, pc)}</b> <size=13>{HeroKit.Col($"{U.Fmt(spent)} Punkte{stTag}", act || spent <= 0 ? Theme.Muted : Theme.Bad)}</size>", 17, Theme.Text);
                foreach (var n in nodes)
                {
                    double r = U.Get(p.points, n.id, 0);
                    string why = ChampionSystem.paragonRaiseReason(he, n.id);
                    string nid = n.id;
                    string tip = $"{n.description} je Rang{(!string.IsNullOrEmpty(why) && r < n.maxRank ? $"\n{why}" : "")}";
                    nodeButton(box.transform, HeroKit.IconName(n.icon), n.name, $"{U.Fmt(r)}/{U.Fmt(n.maxRank)}", $"{n.description} je Rang", null,
                        r > 0, r >= n.maxRank, string.IsNullOrEmpty(why), string.IsNullOrEmpty(why), tip, () => a.paragonPoint(nid));
                }
            }
            if (Paragon.paragonSpent(p.points) > 0)
            {
                var rb = HeroKit.Btn(HeroKit.Inline(body), "Paragon zurücksetzen", () => a.paragonReset());
                HeroKit.Tip(rb, "Alle Paragonpunkte kostenlos zurückgeben – jederzeit möglich");
            }
        }

        private Button nodeCard(Transform parent, SkillNode n)
        {
            var c = sim.s.he?.champion;
            double rank = c != null ? U.Get(c.tree, n.id, 0) : 0;
            var chk = ChampionSystem.nodeState(sim, n);
            var reqParts = new List<string>();
            if (n.requires != null)
            {
                var tree = Champions.CHAMPIONS[c?.id ?? "lyra"].tree;
                foreach (var r in n.requires)
                {
                    var rn = tree.Find((x) => x.id == r.id);
                    reqParts.Add($"{(rn != null ? rn.name : r.id)} {U.Fmt(r.rank)}");
                }
            }
            string req = string.Join(", ", reqParts);
            string tip = $"{n.description}{(req.Length > 0 ? $"\nBenötigt: {req}" : "")}{(U.Truthy(n.minLevel) ? $"\nAb Stufe {U.Fmt(n.minLevel.Value)}" : "")}{(!string.IsNullOrEmpty(chk.reason) && rank < n.maxRank ? $"\n{chk.reason}" : "")}";
            string nid = n.id;
            return nodeButton(parent, null, n.name, $"{U.Fmt(rank)}/{U.Fmt(n.maxRank)}", n.description, !string.IsNullOrEmpty(n.perk) ? $"{HeroKit.STAR} Meister" : null,
                rank > 0, rank >= n.maxRank, chk.ok, chk.ok, tip, () => a.learnSkill(nid));
        }

        /// <summary>.node-Karte (Skillbaum, Paragon): Name, Rang oben rechts, Beschreibung, optionaler Tag.</summary>
        internal static Button nodeButton(Transform parent, string icon, string name, string rank, string desc, string tag, bool has, bool max, bool can, bool enabled, string tip, Action click, float minH = 76)
        {
            Color border = can ? Theme.Gold : max ? Theme.Gold : has ? Theme.Accent : Theme.LineStrong;
            Image bg; Outline bo;
            var b = HeroKit.CardBtn(parent, HeroKit.NodeBg, border, click, out bg, out bo);
            if (can) { var g = b.gameObject.AddComponent<Outline>(); g.effectColor = new Color(1f, 216 / 255f, 96 / 255f, 0.3f); g.effectDistance = new Vector2(3, -3); }
            var v = b.gameObject.AddComponent<VerticalLayoutGroup>();
            // Rang unten rechts statt oben: der Name bekommt die volle Breite und bricht nicht mitten im Wort um
            v.padding = new RectOffset(7, 7, 6, 18); v.spacing = 2;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            Ui.Get<LayoutElement>(b.gameObject).minHeight = minH;
            var top = Ui.Row(b.transform, 4, TextAnchor.MiddleLeft, false);
            top.padding = new RectOffset(0, 0, 0, 0);
            if (icon != null) HeroKit.Ic(top.transform, icon, 17, Theme.Text);
            var nt = HeroKit.T(top.transform, $"<b>{name}</b>", 15, Theme.Text);
            Ui.Size(nt, -1, -1, 1);
            var rk = HeroKit.Abs(b.transform, rank, 13, Theme.Gold, TextAnchor.LowerRight, new Vector2(-7, 4));
            rk.fontStyle = FontStyle.Bold;
            HeroKit.T(b.transform, desc, 13, Theme.Text);
            if (tag != null) HeroKit.T(b.transform, tag, 12, Theme.Gold);
            b.interactable = enabled;
            if (!enabled && !max) Ui.Get<CanvasGroup>(b.gameObject).alpha = 0.55f;
            HeroKit.Tip(b, tip);
            return b;
        }

        private void renderCollection(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var owned = new HashSet<string>(he.metaOwned);
            foreach (var u in he.runUnlocks) owned.Add(u);
            HeroKit.P(body, $"Dauerhafte Sammlung: einmal freigeschaltete Items bleiben erhalten, werden nie erneut als identischer Drop vergeben und können im Laden für Ressourcen in einen Lauf geholt werden. Neue Freischaltungen dieser Partie ({HeroKit.STAR}) werden bei Partieende gespeichert.", 15, Theme.Muted);
            var grid = new HeroGrid(body, BODY_W, 125, 5);
            foreach (var d in Items.ITEMS.Values)
            {
                if (!(d.champions == null || d.champions.Contains(he.champion.id) || owned.Contains(d.id))) continue;
                bool has = owned.Contains(d.id);
                double frag = U.Get(he.fragments, d.id, 0);
                var rar = Items.RARITY[d.rarity];
                double need = rar.fragments;
                Color rc = HeroKit.Css(rar.color, Color.white);
                var box = HeroKit.Box(grid.Next(), HeroKit.NodeBg, HeroKit.A(rc, 0.7f), Theme.RoundSmall);
                grid.Fit(box);
                string extra = has ? (he.runUnlocks.Contains(d.id) ? $"{HeroKit.STAR} neu" : null) : d.craftOnly == true ? "Rezept" : $"{U.Fmt(frag)}/{U.Fmt(need)}";
                itemBody(box.transform, rc, HeroKit.IconName(d.icon), d.name, extra, false, has ? null : "?");
                if (!has) Ui.Get<CanvasGroup>(box.gameObject).alpha = 0.45f;
                HeroKit.Tip(box, $"{d.name} ({rar.name}) – {d.description}{(d.craftOnly == true ? " · nur durch Kombination" : "")}");
            }
        }

        private void renderProgress(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            double best = he.bestWaveAtStart;
            HeroKit.P(body, $"Bestwelle bei Partiebeginn: <b>{U.Fmt(best)}</b>. Perks gelten ab dem nächsten Lauf, sobald die Welle erreicht wurde.", 15, Theme.Text);
            HeroKit.H3(body, "Perks");
            var list = Ui.Column(body, 3);
            foreach (var p in HeroEndless.PERKS)
            {
                var row = Ui.Row(list.transform, 6, TextAnchor.MiddleLeft, false);
                HeroKit.Ic(row.transform, HeroKit.IconName(p.icon), 17, Theme.Text);
                var t = HeroKit.T(row.transform, $"<b>{p.name}</b> {HeroKit.Col($"Welle {U.Fmt(p.wave)}: {p.description}", Theme.Muted)}", 15, Theme.Text);
                Ui.Size(t, -1, -1, 1);
                if (!he.perks.Contains(p.id)) row.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            }
            HeroKit.H3(body, "Meilensteine (einmalig)");
            var ml = Ui.Column(body, 3);
            foreach (var m in HeroEndless.MILESTONES)
            {
                var t = HeroKit.T(ml.transform, $"<b>W{U.Fmt(m.wave)} {m.name}</b> {HeroKit.Col(m.description, Theme.Muted)}", 15, Theme.Text);
                if (!(best >= m.wave)) t.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
            }
        }

        /// <summary>.offer: links Name/Beschreibung, rechts Knopf, darunter der Grund in Rot.</summary>
        private Transform offerBox(Transform body, bool ok, Color? rc, out Transform left)
        {
            var box = HeroKit.Box(body, new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.35f), rc.HasValue ? HeroKit.A(rc.Value, 0.5f) : Theme.Line, Theme.Round);
            var v = box.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(9, 9, 6, 6); v.spacing = 2;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            if (!ok) box.gameObject.AddComponent<CanvasGroup>().alpha = 0.75f;
            var row = Ui.Row(box.transform, 8, TextAnchor.MiddleLeft, false);
            var l = Ui.Column(row.transform, 1);
            Ui.Size(l, -1, -1, 1);
            left = l.transform;
            return row.transform;
        }

        private void renderOffers(Transform body, string cat)
        {
            var he = sim.s.he;
            if (he == null) return;
            if (cat == "fragments" || cat == "materials" || cat == "special")
                HeroKit.P(body, $"Begrenzter Bestand wird alle {U.Fmt(HeroEndless.HERO_ENDLESS.stockEvery)} Wellen aufgefüllt.", 13, Theme.Muted);
            foreach (var o in Items.SHOP_OFFERS)
            {
                if (o.category != cat) continue;
                var chk = InventorySystem.offerState(sim, o);
                Transform left;
                var row = offerBox(body, chk.ok, null, out left);
                HeroKit.T(left, $"<b>{o.name}</b>{(chk.left != null ? $"<size=13>{HeroKit.Col($" · Bestand {U.Fmt(chk.left.Value)}", Theme.Muted)}</size>" : "")}", 16, Theme.Text);
                HeroKit.P(left, o.description, 14, Theme.Muted);
                string oid = o.id;
                var b = HeroKit.Btn(row, costText(o.cost), () => a.shopBuy(oid), Ui.BtnKind.Primary);
                HeroKit.Enable(b, chk.ok);
                if (!string.IsNullOrEmpty(chk.reason)) HeroKit.Tip(b, chk.reason);
                if (!chk.ok && !string.IsNullOrEmpty(chk.reason) && chk.reason != NOT_AT_SHOP) HeroKit.P(row.parent, chk.reason, 13, Theme.Bad); // steht schon oben im Hinweis
            }
        }

        private void renderCollectionShop(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var items = InventorySystem.collectionOffers(he);
            HeroKit.P(body, "Freigeschaltete Items aus der Sammlung können hier für Ressourcen in den aktuellen Lauf geholt werden (jedes Item einmal pro Lauf). Rucksack-Perks erlauben kostenlose Mitnahme beim Start.", 15, Theme.Muted);
            if (items.Count == 0) { HeroKit.P(body, "Noch keine Items freigeschaltet.", 16, Theme.Muted); return; }
            foreach (var id in items)
            {
                var d = Items.ITEMS[id];
                var chk = InventorySystem.collectionState(sim, id);
                var rar = Items.RARITY[d.rarity];
                Color rc = HeroKit.Css(rar.color, Color.white);
                Transform left;
                var row = offerBox(body, chk.ok, rc, out left);
                var h = Ui.Row(left, 5, TextAnchor.MiddleLeft, false);
                HeroKit.Ic(h.transform, HeroKit.IconName(d.icon), 18, rc);
                HeroKit.T(h.transform, $"<b>{d.name}</b> <size=13>{HeroKit.Col(rar.name, rc)}</size>", 16, Theme.Text);
                HeroKit.P(left, d.description, 14, Theme.Muted);
                string iid = id;
                var b = HeroKit.Btn(row, costText(chk.cost), () => a.shopBuyItem(iid), Ui.BtnKind.Primary);
                HeroKit.Enable(b, chk.ok);
                if (!string.IsNullOrEmpty(chk.reason)) HeroKit.Tip(b, chk.reason);
                if (!chk.ok && !string.IsNullOrEmpty(chk.reason) && chk.reason != NOT_AT_SHOP) HeroKit.P(row.parent, chk.reason, 13, Theme.Bad); // steht schon oben im Hinweis
            }
        }

        private void renderForge(Transform body)
        {
            var he = sim.s.he;
            if (he == null) return;
            var frag = Items.RECIPES.FindAll((r) => r.kind == "fragments" && r.fragments != null && U.Get(he.fragments, r.fragments.item, 0) > 0);
            var combine = Items.RECIPES.FindAll((r) => r.kind == "combine");
            var heroRec = Items.RECIPES.FindAll((r) => r.kind == "hero" && r.champion == he.champion.id);
            Action<Transform, RecipeDefinition> row = (parent, r) =>
            {
                var chk = InventorySystem.recipeState(sim, r);
                ItemDefinition d = r.result != null ? Items.ITEMS[r.result] : null;
                var parts = new List<string>();
                if (r.fragments != null) parts.Add($"{U.Fmt(chk.have)}/{U.Fmt(r.fragments.count)} Fragmente");
                if (r.items != null) foreach (var i in r.items) parts.Add(Items.ITEMS[i].name);
                parts.Add(costText(r.cost));
                Color? rc = d != null ? HeroKit.Css(Items.RARITY[d.rarity].color, Color.white) : (Color?)null;
                Transform left;
                var rowT = offerBox(parent, chk.ok, rc, out left);
                var h = Ui.Row(left, 5, TextAnchor.MiddleLeft, false);
                if (d != null) HeroKit.Ic(h.transform, HeroKit.IconName(d.icon), 18, rc.Value);
                string sub = "";
                if (d != null && r.kind == "hero")
                {
                    string slotName = "";
                    foreach (var x in Items.SLOTS) if (x.slot == d.slot) { slotName = x.name; break; }
                    sub = $" <size=13>{HeroKit.Col($"{Items.RARITY[d.rarity].name} · {slotName}", rc.Value)}</size>";
                }
                var title = HeroKit.T(h.transform, $"<b>{r.name}</b>{sub}", 16, Theme.Text);
                Ui.Size(title, -1, -1, 1);
                HeroKit.P(left, string.Join(" + ", parts), 14, Theme.Muted);
                if (d != null && r.kind == "hero")
                {
                    var sp = new List<string>();
                    foreach (var kv in d.stats) sp.Add($"{statName(kv.Key)} {statDelta(kv.Key, kv.Value)}");
                    HeroKit.P(left, $"{d.description} {string.Join(", ", sp)}", 13, Theme.Text);
                }
                string rid = r.id;
                var b = HeroKit.Btn(rowT, "Herstellen", () => a.craft(rid), Ui.BtnKind.Primary);
                HeroKit.Enable(b, chk.ok);
                if (!string.IsNullOrEmpty(chk.reason)) HeroKit.Tip(b, chk.reason);
                if (!chk.ok && !string.IsNullOrEmpty(chk.reason) && chk.reason != NOT_AT_SHOP) HeroKit.P(rowT.parent, chk.reason, 13, Theme.Bad); // steht schon oben im Hinweis
            };
            HeroKit.H3(body, $"Heldenschmiede – {Champions.CHAMPIONS[he.champion.id].name}");
            HeroKit.P(body, "Signaturgegenstände nur für diesen Helden: der epische ab Welle 8 aus Ressourcen, die legendäre Waffe ab Welle 20 aus dem epischen Gegenstand.", 15, Theme.Muted);
            var hf = Ui.Column(body, 5);
            hf.name = "HeroForge";
            foreach (var r in heroRec) row(hf.transform, r);
            HeroKit.H3(body, "Fragmente zusammensetzen");
            if (frag.Count > 0) { var col = Ui.Column(body, 5); foreach (var r in frag) row(col.transform, r); }
            else HeroKit.P(body, "Keine Fragmente vorhanden.", 15, Theme.Muted);
            HeroKit.H3(body, "Kombinationen (legendär)");
            var cc = Ui.Column(body, 5);
            foreach (var r in combine) row(cc.transform, r);
            HeroKit.H3(body, "Verstärken");
            HeroKit.P(body, $"Items im Tab „Verkaufen“ oder im Charakterbogen auswählen und verstärken (bis +{U.Fmt(Items.MAX_PLUS)}, je Stufe +15 % Werte).", 15, Theme.Muted);
        }

        // ------------------------------------------------------------ Elementklassen

        private sealed class SkillEl
        {
            public int slot; public bool ult;
            public Button b; public Image bg; public Outline border, glow; public Image fill, icon, noMana, target;
            public Text key, mana, cd; public CanvasGroup cg;
            public int state = -1; public float fillLast = -1; public int cdLast = int.MinValue; public string keyStr;
        }

        private sealed class PotionEl
        {
            public Button b; public Image bg; public Outline border, glow; public Image icon, cdOv; public Text n, k, cdT;
            public string potion; public string tip = "";
        }

        private sealed class BuffEl
        {
            public GameObject go; public Image icon; public Text text; public string src; public string name; public int t = int.MinValue; public ChampionBuff buff;
        }
    }

    /// <summary>Balken der Heldenleiste (.hb): Hintergrund, Verlaufsfüllung, Zahl darüber.</summary>
    internal sealed class HeroBar
    {
        public readonly Image back, fill;
        public readonly Text text;
        private float last = -1;

        public HeroBar(Transform parent, float height, Sprite grad, int fontSize)
        {
            back = Ui.Panel(parent, "Hb", Theme.Hex(0x0b0814), false, Theme.RoundSmall);
            Ui.Size(back, -1, height);
            var clip = Ui.Node("Clip", back.transform);
            Ui.Fill(Ui.Rt(clip));
            clip.AddComponent<RectMask2D>();
            var go = Ui.Node("Fill", clip.transform);
            fill = go.AddComponent<Image>();
            fill.sprite = grad;
            fill.raycastTarget = false;
            var r = fill.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.offsetMin = r.offsetMax = Vector2.zero;
            var gl = Ui.Node("Gloss", go.transform);
            var gi = gl.AddComponent<Image>(); gi.color = new Color(1, 1, 1, 0.3f); gi.raycastTarget = false;
            var gr = gi.rectTransform; gr.anchorMin = new Vector2(0, 1); gr.anchorMax = new Vector2(1, 1); gr.pivot = new Vector2(0.5f, 1); gr.offsetMin = new Vector2(0, -1); gr.offsetMax = Vector2.zero;
            text = HeroKit.T(back.transform, "", fontSize, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Ui.Fill(text.rectTransform);
            HeroKit.Shadow(text);
        }

        public void Set(float v)
        {
            v = Mathf.Clamp01(v);
            if (Mathf.Abs(v - last) < 0.0005f) return;
            last = v;
            fill.rectTransform.anchorMax = new Vector2(v, 1);
        }
    }

    /// <summary>Meldet gedrückte Zeiger im Fenster (gegen Neuaufbau während eines Klicks).</summary>
    internal class HeroPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<bool> onChange;
        public void OnPointerDown(PointerEventData e) { onChange?.Invoke(true); }
        public void OnPointerUp(PointerEventData e) { onChange?.Invoke(false); }
        void OnDisable() { onChange?.Invoke(false); }
    }

    /// <summary>Allokationsfreie Signatur (FNV-artiger 64-Bit-Hash) statt der TS-Signaturstrings.</summary>
    internal struct HeroSig
    {
        public ulong h;
        private bool init;
        private void Mix(ulong v)
        {
            if (!init) { h = 14695981039346656037UL; init = true; }
            h ^= v; h *= 1099511628211UL; h ^= h >> 29;
        }
        public void Add(double d) { Mix((ulong)BitConverter.DoubleToInt64Bits(d)); }
        public void Add(bool b) { Mix(b ? 0x51UL : 0x52UL); }
        public void Add(string s)
        {
            if (s == null) { Mix(0x9e3779b97f4a7c15UL); return; }
            Mix((ulong)s.Length + 0x1000);
            for (int i = 0; i < s.Length; i++) Mix(s[i]);
        }
    }

    /// <summary>Raster mit festen Spalten (CSS grid repeat(auto-fill, minmax(min, 1fr))); Zeilen gleich hoch.</summary>
    internal sealed class HeroGrid
    {
        private readonly Transform root;
        private readonly int cols;
        public readonly float cellW;
        private readonly float gap;
        private Transform row;
        private int n;

        public HeroGrid(Transform parent, float width, float minW, float gap, int fixedCols = 0)
        {
            this.gap = gap;
            cols = fixedCols > 0 ? fixedCols : Mathf.Max(1, Mathf.FloorToInt((width + gap) / (minW + gap)));
            cellW = (width - gap * (cols - 1)) / cols;
            root = Ui.Column(parent, gap).transform;
        }

        public Transform Next()
        {
            if (n % cols == 0)
            {
                var r = Ui.Row(root, gap, TextAnchor.UpperLeft, false);
                r.childForceExpandHeight = true;
                row = r.transform;
            }
            n++;
            return row;
        }

        public void Fit(Component c)
        {
            var le = Ui.Get<LayoutElement>(c.gameObject);
            le.preferredWidth = cellW; le.minWidth = cellW; le.flexibleWidth = 0;
        }

        /// <summary>Umbrechende Zeile (flex-wrap) für Elemente bekannter Breite.</summary>
        internal sealed class Wrap
        {
            private readonly Transform root;
            private readonly float width, gap;
            private Transform row;
            private float used;

            public Wrap(Transform parent, float width, float gap)
            {
                this.width = width; this.gap = gap;
                root = Ui.Column(parent, gap).transform;
            }

            public Transform parentFor(float w)
            {
                if (row == null || used + w > width)
                {
                    row = Ui.Row(root, gap, TextAnchor.MiddleLeft, false).transform;
                    used = 0;
                }
                used += w + gap;
                return row;
            }
        }
    }

    /// <summary>Kleine Bausteine der Heldenoberfläche (ergänzen Ui.cs, ohne es zu ändern).</summary>
    internal static class HeroKit
    {
        // PORT: '★' / '✦' sind in der Laufzeitschrift nicht enthalten → '♦' (WGL4-Zeichen)
        public const string STAR = "♦";
        public static readonly Color NodeBg = Theme.Hex(0x1a1428);

        public static string fmt(double n) { return U.De(U.Round(n), 0); }
        public static string fmtNum(double n, double digits = 1) { return U.De(n, digits); }
        public static string Hex6(double rgb) { return "#" + (((int)rgb) & 0xffffff).ToString("x6"); }
        public static Color A(Color c, float a) { c.a = a; return c; }
        public static Color Css(string css, Color fallback)
        {
            Color c;
            return !string.IsNullOrEmpty(css) && ColorUtility.TryParseHtmlString(css, out c) ? c : fallback;
        }
        public static string Col(string text, Color c) { return $"<color=#{ColorUtility.ToHtmlStringRGBA(c)}>{text}</color>"; }
        public static string Col(string text, string css) { return $"<color={css}>{text}</color>"; }

        // ------------------------------------------------------------ Emoji → Symbolname (ui/icons.ts)

        private static readonly Dictionary<string, string> EMOJI = new Dictionary<string, string>
        {
            ["⚔"] = "sword", ["🗡"] = "sword", ["🔪"] = "sword", ["🔱"] = "sword", ["🦷"] = "sword", ["💪"] = "sword", ["💢"] = "sword",
            ["🏹"] = "range", ["🎯"] = "range", ["🔭"] = "range", ["👁"] = "range", ["🧲"] = "range", ["🔴"] = "range",
            ["🛡"] = "shield", ["⛑"] = "shield", ["🦺"] = "shield", ["🪨"] = "shield", ["🔰"] = "shield",
            ["❤"] = "heart", ["💚"] = "heart", ["🩸"] = "heart", ["✚"] = "plus",
            ["🪄"] = "star", ["✦"] = "star", ["✧"] = "star", ["✨"] = "star", ["★"] = "star", ["✺"] = "star", ["💠"] = "star", ["🔷"] = "star", ["◆"] = "star", ["🔮"] = "star",
            ["🪙"] = "coin", ["💰"] = "coins",
            ["🗼"] = "up", ["🏰"] = "up", ["🏮"] = "up", ["🏪"] = "coins", ["🏠"] = "shield",
            ["🎒"] = "scroll", ["📖"] = "book", ["📐"] = "pen", ["🧵"] = "pen",
            ["🎼"] = "wave", ["♫"] = "wave", ["🎻"] = "wave", ["🎐"] = "wave", ["💧"] = "wave", ["🌬"] = "wave",
            ["🍀"] = "dice", ["🎭"] = "dice", ["🌀"] = "rotR", ["🌌"] = "infinity",
            ["🔥"] = "energy", ["💥"] = "energy", ["⚡"] = "bolt", ["🌩"] = "bolt",
            ["👑"] = "crown", ["⛏"] = "gear", ["⚒"] = "gear", ["🔨"] = "gear", ["⚗"] = "fuse", ["🧪"] = "fuse", ["⛓"] = "fuse", ["💍"] = "fuse",
            ["👢"] = "advance", ["👟"] = "advance", ["🦅"] = "advance", ["⏱"] = "calendar", ["⌛"] = "calendar",
        };

        /// <summary>Symbolname für ein Emoji der Konfiguration (Schrift hat keine Emojis); Symbolnamen bleiben unverändert.</summary>
        public static string IconName(string emoji)
        {
            if (string.IsNullOrEmpty(emoji)) return "star";
            string e = emoji.Replace("️", "");
            string n;
            if (EMOJI.TryGetValue(e, out n)) return n;
            bool ascii = true;
            foreach (char ch in e) if (ch > 127) { ascii = false; break; }
            return ascii ? e : "star";
        }

        public static Sprite Spr(string name)
        {
            Sprite s = null;
            try { s = Icons.Get(name); } catch (Exception) { s = null; }
            if (s == null && name != "star") { try { s = Icons.Get("star"); } catch (Exception) { } }
            return s;
        }

        // ------------------------------------------------------------ Sprites

        private static readonly Dictionary<string, Sprite> grads = new Dictionary<string, Sprite>();

        public static Sprite HGrad(Color a, Color b) { return Grad(a, b, false); }
        public static Sprite VGrad(Color top, Color bottom) { return Grad(bottom, top, true); }

        private static Sprite Grad(Color a, Color b, bool vertical)
        {
            string key = ColorUtility.ToHtmlStringRGBA(a) + ColorUtility.ToHtmlStringRGBA(b) + (vertical ? "v" : "h");
            Sprite s;
            if (grads.TryGetValue(key, out s) && s != null) return s;
            if (vertical)
            {
                // abgerundet (9-Slice) mit senkrechtem Verlauf
                const int size = 64, radius = 14;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float al = RoundAlpha(x, y, size, radius);
                        Color c = Color.Lerp(a, b, y / (float)(size - 1));
                        c.a *= al;
                        px[y * size + x] = c;
                    }
                tex.SetPixels32(px);
                tex.Apply();
                s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            }
            else
            {
                const int w = 64;
                var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color32[w];
                for (int x = 0; x < w; x++) px[x] = Color.Lerp(a, b, x / (float)(w - 1));
                tex.SetPixels32(px);
                tex.Apply();
                s = Sprite.Create(tex, new Rect(0, 0, w, 1), new Vector2(0.5f, 0.5f), 100f);
            }
            grads[key] = s;
            return s;
        }

        private static float RoundAlpha(int x, int y, int size, int radius)
        {
            float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
            float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
            return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
        }

        private static Sprite ring;
        /// <summary>Abgerundeter Rahmen (nur Kontur, 9-Slice) für Hervorhebungen (outline, inset box-shadow).</summary>
        public static Sprite Ring
        {
            get
            {
                if (ring != null) return ring;
                const int size = 32, radius = 10; const float thick = 2f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                        float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                        float inner = (dx > 0 && dy > 0) ? radius - d : edge;
                        float outer = Mathf.Clamp01(radius - d + 0.5f);
                        float al = outer * Mathf.Clamp01(thick - inner + 0.5f);
                        px[y * size + x] = new Color32(255, 255, 255, (byte)(al * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
                return ring;
            }
        }

        // ------------------------------------------------------------ Elemente

        public static Text T(Transform p, string s, float size, Color? c = null, FontStyle st = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft)
        {
            return Ui.Label(p, s ?? "", Mathf.RoundToInt(size), c ?? Theme.Text, a, st);
        }

        /// <summary>Absatz (umbrechend).</summary>
        public static Text P(Transform p, string s, float size, Color c)
        {
            var t = T(p, s, size, c, FontStyle.Normal, TextAnchor.UpperLeft);
            t.lineSpacing = 1.05f;
            return t;
        }

        public static Text H3(Transform p, string s)
        {
            var t = T(p, s, 17, Theme.Text, FontStyle.Bold, TextAnchor.LowerLeft);
            Ui.Size(t, -1, 30);
            return t;
        }

        /// <summary>Einzeilig, abgeschnitten (white-space: nowrap; overflow: hidden).</summary>
        public static Text Line(Transform p, string s, float size, Color c, FontStyle st = FontStyle.Normal)
        {
            var t = T(p, s, size, c, st);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            Ui.Size(t, -1, Mathf.Ceil(size * 1.25f));
            return t;
        }

        /// <summary>Absolut positionierter Text in einer Ecke (ignoriert das Layout).</summary>
        public static Text Abs(Transform p, string s, float size, Color c, TextAnchor corner, Vector2 offset)
        {
            var t = T(p, s, size, c, FontStyle.Bold, corner);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Get<LayoutElement>(t.gameObject).ignoreLayout = true;
            var r = t.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(Mathf.Max(0, offset.x), Mathf.Max(0, offset.y));
            r.offsetMax = new Vector2(Mathf.Min(0, offset.x), Mathf.Min(0, offset.y));
            Shadow(t);
            return t;
        }

        public static void Shadow(Text t)
        {
            var s = Ui.Get<Shadow>(t.gameObject);
            s.effectColor = new Color(0, 0, 0, 0.85f);
            s.effectDistance = new Vector2(1, -1);
        }

        public static Image Ic(Transform p, string name, float size, Color c)
        {
            var go = Ui.Node("Ic", p);
            var img = go.AddComponent<Image>();
            img.sprite = Spr(name);
            img.preserveAspect = true;
            img.color = c;
            img.raycastTarget = false;
            Ui.Size(img, size, size);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            return img;
        }

        public static void Center(RectTransform r, float size)
        {
            Ui.Get<LayoutElement>(r.gameObject).ignoreLayout = true;
            Ui.Place(r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        }

        /// <summary>Hervorhebungsrahmen (Ring) über dem Element; inset &lt; 0 = außerhalb (outline-offset).</summary>
        public static Image Frame(Transform p, Color c, float inset)
        {
            var go = Ui.Node("Frame", p);
            var img = go.AddComponent<Image>();
            img.sprite = Ring; img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false;
            Ui.Get<LayoutElement>(go).ignoreLayout = true;
            Ui.Fill(img.rectTransform, inset);
            return img;
        }

        public static Image Box(Transform p, Color bg, Color? border, Sprite sprite = null)
        {
            var img = Ui.Panel(p, "Box", bg, border.HasValue, sprite ?? Theme.RoundSmall);
            if (border.HasValue) img.GetComponent<Outline>().effectColor = border.Value;
            return img;
        }

        public static HorizontalLayoutGroup HBox(Transform p, Color bg, Color border, int padX, int padY, float spacing)
        {
            var img = Box(p, bg, border, Theme.Round);
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(padX, padX, padY, padY); h.spacing = spacing; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        /// <summary>Zeile für einen einzelnen, nicht gestreckten Knopf.</summary>
        public static Transform Inline(Transform p) { return Ui.Row(p, 6, TextAnchor.MiddleLeft, false).transform; }

        public static void Colors(Selectable b)
        {
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.2f, 1.35f, 1f);
            cb.pressedColor = new Color(0.85f, 0.8f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            cb.colorMultiplier = 1.4f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        }

        /// <summary>Kachel-Knopf (Hintergrund + Rahmen), Inhalt baut der Aufrufer.</summary>
        public static Button CardBtn(Transform p, Color bg, Color border, Action click, out Image img, out Outline outline)
        {
            img = Box(p, bg, border, Theme.RoundSmall);
            outline = img.GetComponent<Outline>();
            var b = img.gameObject.AddComponent<Button>();
            Colors(b);
            b.targetGraphic = img;
            if (click != null) b.onClick.AddListener(() => click());
            return b;
        }

        public static float BtnWidth(string text, int fs)
        {
            // grobe Schätzung für Umbrüche (genaue Breite setzt Btn selbst)
            string plain = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]+>", "");
            return plain.Length * fs * 0.52f + 26;
        }

        /// <summary>.btn (.sm): Text, optional Symbol; Breite nach Inhalt.</summary>
        public static Button Btn(Transform p, string text, Action click, Ui.BtnKind kind = Ui.BtnKind.Normal, bool sm = true, string icon = null, int size = 0, float height = 0)
        {
            int fs = size > 0 ? size : sm ? 16 : 18;
            float h = height > 0 ? height : sm ? 34 : 40;
            var b = Ui.Button(p, text ?? "", click, kind, fs);
            var le = b.GetComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            var t = b.GetComponentInChildren<Text>();
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            float tw = string.IsNullOrEmpty(text) ? 0 : t.preferredWidth;
            float w;
            if (icon != null)
            {
                float isz = fs + 3;
                var ic = Ic(b.transform, icon, isz, Theme.Text);
                Ui.Get<LayoutElement>(ic.gameObject).ignoreLayout = true;
                if (tw <= 0)
                {
                    Center(ic.rectTransform, isz);
                    w = Mathf.Max(h, 36);
                }
                else
                {
                    Ui.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(11, 0), new Vector2(isz, isz));
                    var tr = t.rectTransform;
                    tr.offsetMin = new Vector2(11 + isz + 5, tr.offsetMin.y);
                    tr.offsetMax = new Vector2(-10, tr.offsetMax.y);
                    t.alignment = TextAnchor.MiddleLeft;
                    w = 11 + isz + 5 + tw + 11;
                }
            }
            else w = Mathf.Max(h, tw + (sm ? 22 : 30));
            le.minWidth = w; le.preferredWidth = w;
            return b;
        }

        public static Text Label(Button b) { return b.GetComponentInChildren<Text>(); }

        public static void Enable(Button b, bool on)
        {
            b.interactable = on;
            Ui.Get<CanvasGroup>(b.gameObject).alpha = on ? 1f : 0.45f;
        }

        /// <summary>.btn.on: goldener Rahmen und goldene Schrift (auch Symbol).</summary>
        public static void On(Button b, bool on)
        {
            Ui.SetKind(b, on ? Ui.BtnKind.On : Ui.BtnKind.Normal);
            Color c = on ? Theme.Gold : Theme.Text;
            // Nur Schrift und Symbole einfärben – nicht die Knopffläche (sonst Gold auf Gold bzw. weiß auf gelb)
            foreach (var g in b.GetComponentsInChildren<Graphic>())
            {
                if (g.gameObject == b.gameObject) continue;
                if (g is Image im && (im.sprite == Theme.Round || im.sprite == null || im.type == Image.Type.Sliced)) continue;
                g.color = c;
            }
        }

        public static void Tip(Component c, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Ui.Tip(c, () => text);
        }

        /// <summary>Kinder entfernen (sofort deaktiviert, damit das Layout sie nicht mehr zählt).</summary>
        public static void Wipe(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                go.SetActive(false);
                go.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
