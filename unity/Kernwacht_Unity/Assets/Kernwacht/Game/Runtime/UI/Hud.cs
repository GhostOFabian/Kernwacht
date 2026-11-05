using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UText = UnityEngine.UI.Text;

namespace Kernwacht.Game.UI
{
    // HUD der Hauptspielansicht (Port von ui/Hud.ts). Liest den Simulationszustand nur; alle Aktionen laufen über HudActions.

    public interface HudActions
    {
        void selectBuild(string def, double? spec = null);
        void confirmBuild();
        void cancel();
        void selectTower(double uid);
        void upgrade(double uid);
        /// <summary>Alle bzw. die angegebenen Türme bis zur höchsten Stufe aufwerten (ohne Spezialisierung).</summary>
        void upgradeAll(List<double> uids = null);
        /// <summary>Gruppenauswahl (Doppelklick) aufheben.</summary>
        void clearGroup();
        void resetView();
        /// <summary>Legendären Turm versetzen (Zielfeld anklicken).</summary>
        void startMove(double uid);
        /// <summary>Alle bzw. die angegebenen Türme verkaufen.</summary>
        void sellAll(List<double> uids = null);
        void socketRune(double towerUid, double runeUid);
        void unsocketRune(double runeUid);
        void specialize(double uid, double spec);
        void respec(double uid, double spec);
        void star(double uid);
        void starAll(List<double> uids = null);
        void groupSpec(List<double> uids, double spec);
        void sell(double uid);
        void setPriority(double uid, string p);
        void startWave();
        void togglePause();
        void cycleSpeed();
        void chooseModifier(string id);
        void rotate(double dir);
        void zoom(double f);
        void fit();
        void restart();
        void quit();
        void help();
        void settings();
        void toggleRanges();
        void tutorialDone();
        void heroCard(string id);
        void heroMode(string id, string mode);
        void heroAbility(string id, double slot);
        void heroPerk(string id, double choice);
        void towerActive(double uid);
        void fuse(double uid, double partner);
        void lookAt(double x, double z);
        void toggleRotationLock();
        void miniPause();
        void mine(double cell);
        void selectEnemy(double uid);
    }

    public class HudView
    {
        public string buildDef;
        /// <summary>3.1: gewählte Spezialisierung beim Bau (0 = normal).</summary>
        public double? buildSpec;
        public double selectedTower;
        public GridPos pendingCell;
        public BuildCheck pendingCheck;
        public bool touchMode;
        public string infoEnemy;
        public bool showRanges;
        public string placingHero;
        public double selectedHero;
        /// <summary>Angeklickter Gegner (UID) für das Live-Infopanel.</summary>
        public double selectedEnemy;
        public bool rotationLocked;
        /// <summary>Mini-Pause: Simulation angehalten, aber ohne Pausenmenü – Bauen und Aufwerten bleiben möglich.</summary>
        public bool miniPaused;
        /// <summary>Angeklicktes Hindernis (Zellindex) im Helden-Endlos, -1 = keins.</summary>
        public double selectedObstacle = -1;
        /// <summary>Doppelklick auf einen Turm: alle Türme dieser Sorte sind gemeinsam ausgewählt.</summary>
        public string groupDef;
    }

    public partial class Hud
    {
        // Bezugsmaße (1920×1080, CSS-px)
        const float TOP_H = 52;
        const float BAR_H = 90;
        /// <summary>Turmknöpfe: freigeschaltet breit mit großem Porträt, gesperrt als schmaler Chip.</summary>
        const float TBTN = 78, TBTN_LOCKED = 40, TICON = 58;
        const float PANEL_W = 340;

        static readonly Dictionary<string, string> MODE_LABEL = new Dictionary<string, string> { ["hold"] = "Halten", ["advance"] = "Vorrücken", ["retreat"] = "Rückzug" };

        readonly GameSimulation sim;
        readonly HudActions a;
        readonly HudView v;
        readonly Sig sig = new Sig();

        public readonly RectTransform root;
        public readonly RectTransform stage;
        public readonly RectTransform floaters;
        /// <summary>Container für zusätzliche Kopfzeilenwerte (z. B. Ressourcen im Helden-Endlosmodus).</summary>
        public readonly RectTransform statsBox;
        public readonly RectTransform stageLeft;
        /// <summary>Ebene für Popups (Spezialisierungswahl beim Kauf, Bauplatz-Erklärung).</summary>
        readonly RectTransform popLayer;

        // Kopfzeile
        readonly UText gold;
        readonly Image goldStat;
        double goldShown, goldLast, goldTxt = double.NaN;
        float goldBump;
        readonly UText lives;
        readonly Image livesStat;
        bool livesLow;
        double livesA = double.NaN, livesB = double.NaN;
        readonly UText wave;
        string waveTxt;
        readonly UText energy;
        double energyV = double.NaN;
        readonly Button startBtn;
        string startTitle = "Welle starten (Leertaste)";
        string startSig;
        bool startCountdown;
        readonly Button speedBtn;
        double speedV = double.NaN;
        readonly Button pauseBtn;
        readonly Button towerListBtn;
        int specReady = -1;
        bool pauseShown;
        readonly Button lockBtn;
        readonly Button miniBtn;
        readonly Button upAllBtn;
        string upAllSig = "";
        string upAllTitle = "";

        // Turmleiste
        readonly RectTransform towerBar;
        readonly Button limitStat;
        readonly UText limitV, limitB;
        string limitSig;
        string limitTitle = "";
        RectTransform limitPop;
        string limitPopSig;

        class TowerBtn
        {
            public string id;
            public Button btn;
            public Image bg;
            public Outline outline;
            public UText cost, key, prespec, free;
            public CanvasGroup cg;
            public HudHover hover;
            /// <summary>Fusionsmarke: leuchtet, wenn der Turm mit dem angeklickten Turm auf dem Feld kombinierbar ist.</summary>
            public Image fuse;
            public UText fuseLbl;
            public string fuseWith;
            public string costTxt;
            public string costSig;
            public int flags = -1;
            public LayoutElement le;
            public Image icon, elem;
            public bool chip;
            public Image cmd;
        }
        readonly Dictionary<string, TowerBtn> towerBtns = new Dictionary<string, TowerBtn>();
        readonly List<TowerBtn> towerBtnList = new List<TowerBtn>();
        /// <summary>3.1: Auswahl normal / spezialisiert beim Turmkauf (ab Welle 250).</summary>
        RectTransform specPop;
        string specPopTower;
        HudHover specPopHover;

        // Bühne
        readonly RectTransform stageTop;
        readonly RectTransform banners;
        readonly Image letterTop, letterBot;
        float letterH;
        readonly HudFlow strip;
        string stripSig = "";
        readonly RectTransform mods;
        string modsSig = "";
        readonly RectTransform heroBar;
        readonly Dictionary<string, HeroCardEls> heroCards = new Dictionary<string, HeroCardEls>();
        readonly Image bossBox;
        readonly UText bossName, bossPhase;
        readonly HudBar bossHp, bossPoise;
        string bossSig, bossHpSig;
        bool bossChallenge;
        readonly RectTransform warnings;
        string warnSig = "";
        readonly List<Outline> warnPulse = new List<Outline>();
        readonly Image miniBadge;
        readonly RawImage mini;
        readonly Button followBtn;

        // Seitenleiste
        readonly Image panel;
        readonly RectTransform panelBody;
        readonly ScrollRect panelScroll;
        readonly HudHover panelHover;
        string panelSig = "";
        string panelVol = "";
        double panelBuiltAt;
        /// <summary>Maustaste/Finger gerade im Panel oder in der Turmübersicht gedrückt.</summary>
        bool pointerHeld;

        // Turmübersicht (Taste O)
        readonly Image towerWin;
        readonly HudHover towerWinHover;
        string towerWinSig = "";
        public bool towerListOpen;
        string towerSort = "type";
        /// <summary>Turmübersicht: voll ausgebaute Türme ausblenden.</summary>
        bool hideMaxed = true;

        // Modale Dialoge
        readonly Image modal;
        string modalKind = "";
        bool modalDirty = true;
        double sellArmed;
        /// <summary>Zweistufige Bestätigung für „Alle verkaufen“: Schlüssel der scharf geschalteten Schaltfläche.</summary>
        string sellAllArmed = "";
        double sellAllArmedAt;
        int tutorialStep;
        double lastFrame = HK.Now();

        public bool showMinimap;
        /// <summary>Kamera folgt dem aktiven Boss.</summary>
        public bool followBoss;
        /// <summary>Kameraausschnitt in Weltkoordinaten (für die Minikarte), von der Sitzung gesetzt.</summary>
        public List<double[]> camQuad = new List<double[]>();
        public string hoverInfo;
        public string bossInfo;
        public Action<string> onModInfo;

        // Banner
        class BannerEl { public RectTransform rt; public CanvasGroup cg; public float t; public float ms; public bool boss; }
        readonly List<BannerEl> bannerList = new List<BannerEl>();

        // Tooltip
        RectTransform tipBox;
        UText tipText;
        LayoutElement tipLe;
        TipSource tipSrc;
        float tipT;

        /// <param name="icons">PORT: entspricht der IconFactory des Webspiels; in Unity statisch (Icons.Get/Model) – wird ignoriert.</param>
        public Hud(GameSimulation sim, HudActions a, HudView v, bool mazing, object icons = null)
        {
            this.sim = sim;
            this.a = a;
            this.v = v;
            var s = sim.s;
            showMinimap = s.map.width * s.map.height > 320;

            Transform parent = GameApp.I != null ? (Transform)GameApp.I.hudLayer : null;
            root = Ui.Rt(Ui.Node("Hud", parent));
            Ui.Fill(root);
            root.gameObject.AddComponent<HudTicker>().hud = this;

            // ---------------------------------------------------------- Kopfzeile
            var top = HK.Plain(root, "Topbar", HK.BarBg, true);
            var topRt = top.rectTransform;
            topRt.anchorMin = new Vector2(0, 1); topRt.anchorMax = new Vector2(1, 1); topRt.pivot = new Vector2(0.5f, 1);
            topRt.sizeDelta = new Vector2(0, TOP_H); topRt.anchoredPosition = Vector2.zero;
            var topLine = HK.Plain(top.transform, "Edge", new Color(1f, 0.85f, 0.29f, 0.45f));
            var tl = topLine.rectTransform; tl.anchorMin = new Vector2(0, 0); tl.anchorMax = new Vector2(1, 0); tl.pivot = new Vector2(0.5f, 0); tl.sizeDelta = new Vector2(0, 1);

            var stats = HK.HRow(top.transform, 8, TextAnchor.MiddleLeft);
            statsBox = stats.GetComponent<RectTransform>();
            Ui.Place(statsBox, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(0, TOP_H - 8));
            HK.Fit(stats, true, false);
            HK.Tip(HK.IconBtn(statsBox, "menu", () => a.togglePause(), 44), "Menü: Pause, Hilfe, Einstellungen (Esc/P)");
            gold = StatPill(statsBox, "coin", Theme.Gold, Theme.Gold, "Gold", out goldStat);
            lives = StatPill(statsBox, "heart", Theme.Hex(0xff6a80), Theme.Text, "Kernenergie", out livesStat);
            wave = StatPill(statsBox, "wave", Theme.Hex(0x9ad0ff), Theme.Text, "Welle", out _);
            if (s.heroes.Count > 0) energy = StatPill(statsBox, "energy", Theme.Hex(0x8ae8ff), Theme.Hex(0x8ae8ff), $"Rissenergie für Helden (max. {U.Fmt(Heroes.HERO_RULES.maxEnergy)})", out _);

            var controls = HK.HRow(top.transform, 8, TextAnchor.MiddleRight);
            var cRt = controls.GetComponent<RectTransform>();
            Ui.Place(cRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(0, TOP_H - 8));
            HK.Fit(controls, true, false);
            // Aufgeräumt: eindeutige Symbole, gleich hohe Knöpfe, keine doppelte Pause-Taste (☰ öffnet das Menü)
            towerListBtn = HK.B(cRt, "Türme", () => toggleTowerList(), Ui.BtnKind.Normal, 16, 40, "scroll");
            Ui.Tip(towerListBtn, () => specReady > 0 ? $"Turmübersicht (O) – {specReady} Turm/Türme können jetzt spezialisiert werden (★)" : "Turmübersicht: alle Türme mit Aufwerten und Infos (O)");
            upAllBtn = HK.B(cRt, "Alle aufwerten", () => a.upgradeAll(), Ui.BtnKind.Normal, 16, 40, "up");
            Ui.Tip(upAllBtn, () => upAllTitle);
            speedBtn = HK.B(cRt, "1×", () => a.cycleSpeed(), Ui.BtnKind.Normal, 17, 40, "speed");
            HK.Tip(speedBtn, "Spielgeschwindigkeit 1× / 2× / 3× / 5× / 8× (F)");
            miniBtn = HK.B(cRt, "Pause", () => a.miniPause(), Ui.BtnKind.Normal, 16, 40, "pause");
            HK.Tip(miniBtn, "Spiel anhalten, ohne das Menü zu öffnen – Bauen und Aufwerten bleiben möglich (Leertaste im Kampf)");
            pauseBtn = null; // PORT: zweite Pause-Taste entfernt – das Menü (☰, Esc/P) pausiert bereits
            startBtn = HK.B(cRt, "Welle", () => a.startWave(), Ui.BtnKind.Primary, 17, 40, "play");
            startBtn.GetComponent<HudFit>().minW = 150;
            Ui.Tip(startBtn, () => startTitle);

            // ---------------------------------------------------------- Mitte: Bühne + Seitenleiste
            var middle = Ui.Rt(Ui.Node("Middle", root));
            middle.anchorMin = Vector2.zero; middle.anchorMax = Vector2.one;
            middle.offsetMin = new Vector2(0, BAR_H); middle.offsetMax = new Vector2(0, -TOP_H);
            middle.SetAsFirstSibling();

            stage = Ui.Rt(Ui.Node("Stage", middle));
            stage.anchorMin = Vector2.zero; stage.anchorMax = Vector2.one;
            stage.offsetMin = Vector2.zero; stage.offsetMax = new Vector2(-PANEL_W, 0);
            stage.gameObject.AddComponent<RectMask2D>();

            floaters = Ui.Rt(Ui.Node("Floaters", stage));
            Ui.Fill(floaters);

            letterTop = HK.Plain(stage, "LetterTop", Color.black);
            var lt = letterTop.rectTransform; lt.anchorMin = new Vector2(0, 1); lt.anchorMax = new Vector2(1, 1); lt.pivot = new Vector2(0.5f, 1); lt.sizeDelta = new Vector2(0, 0);
            letterBot = HK.Plain(stage, "LetterBot", Color.black);
            var lb = letterBot.rectTransform; lb.anchorMin = new Vector2(0, 0); lb.anchorMax = new Vector2(1, 0); lb.pivot = new Vector2(0.5f, 0); lb.sizeDelta = new Vector2(0, 0);

            var bn = Ui.Column(stage, 4);
            bn.childAlignment = TextAnchor.UpperCenter;
            bn.childForceExpandWidth = false;
            banners = bn.GetComponent<RectTransform>();
            banners.anchorMin = new Vector2(0, 1); banners.anchorMax = new Vector2(1, 1); banners.pivot = new Vector2(0.5f, 1);
            banners.sizeDelta = new Vector2(0, 0); banners.anchoredPosition = new Vector2(0, -80);
            HK.Fit(bn, false, true);

            var sl = Ui.Column(stage, 6);
            sl.childAlignment = TextAnchor.LowerLeft;
            sl.childForceExpandWidth = false;
            stageLeft = sl.GetComponent<RectTransform>();
            Ui.Place(stageLeft, Vector2.zero, Vector2.zero, new Vector2(6, 6), new Vector2(0, 0));
            HK.Fit(sl, true, true);
            var mc = Ui.Column(stageLeft, 4);
            mc.childForceExpandWidth = false;
            mods = mc.GetComponent<RectTransform>();
            heroBar = HK.HRow(stageLeft, 6, TextAnchor.LowerLeft).GetComponent<RectTransform>();

            mini = Ui.Node("MiniMap", stage).AddComponent<RawImage>();
            Ui.Place(mini.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(160, 160));
            var mo = mini.gameObject.AddComponent<Outline>(); mo.effectColor = Theme.LineStrong; mo.effectDistance = new Vector2(1, -1);
            mini.gameObject.AddComponent<HudPress>().onDown = miniClick;
            HK.Tip(mini, "Minikarte – antippen, um die Kamera zu verschieben");

            var cam = Ui.Column(stage, 4);
            cam.childForceExpandWidth = false;
            cam.childAlignment = TextAnchor.LowerRight;
            var camRt = cam.GetComponent<RectTransform>();
            Ui.Place(camRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), Vector2.zero);
            HK.Fit(cam, true, true);
            lockBtn = CamBtn(camRt, "lock", () => a.toggleRotationLock(), "Kameradrehung sperren/freigeben (Q/E wirkungslos, wenn gesperrt)");
            CamBtn(camRt, "rotL", () => a.rotate(-1), "Drehen links (Q, 90°). Frei drehen: mittlere Maustaste oder Strg/Alt + Ziehen, Zwei-Finger-Drehung, Tasten , und .");
            CamBtn(camRt, "rotR", () => a.rotate(1), "Drehen rechts (E, 90°). Neigung: mittlere Maustaste/Strg + hoch/runter ziehen oder Bild↑/Bild↓");
            CamBtn(camRt, "back", () => a.resetView(), "Blickwinkel zurücksetzen (Pos1)"); // PORT: TS zeigt hier das Zeichen ⟲
            CamBtn(camRt, "fit", () => a.fit(), "Ganze Karte");
            CamBtn(camRt, "minimap", () => { showMinimap = !showMinimap; }, "Minikarte");
            followBtn = CamBtn(camRt, "crown", null, "Kamera folgt dem Boss");
            followBtn.onClick.AddListener(() =>
            {
                followBoss = !followBoss;
                HK.On(followBtn, followBoss);
            });
            CamBtn(camRt, "range", () => a.toggleRanges(), "Alle Reichweiten (R)");
            HK.On(lockBtn, v.rotationLocked);

            var st = Ui.Column(stage, 6);
            st.childAlignment = TextAnchor.UpperCenter;
            st.childForceExpandWidth = false;
            stageTop = st.GetComponent<RectTransform>();
            stageTop.anchorMin = new Vector2(0, 1); stageTop.anchorMax = new Vector2(1, 1); stageTop.pivot = new Vector2(0.5f, 1);
            stageTop.offsetMin = new Vector2(180, 0); stageTop.offsetMax = new Vector2(-180, -6);
            HK.Fit(st, false, true);

            miniBadge = Ui.Panel(stageTop, "MiniPause", new Color(20 / 255f, 14 / 255f, 34 / 255f, 0.92f), true, Theme.Round);
            miniBadge.GetComponent<Outline>().effectColor = Theme.Gold;
            var mbRow = miniBadge.gameObject.AddComponent<HorizontalLayoutGroup>();
            mbRow.padding = new RectOffset(16, 16, 4, 4); mbRow.childControlWidth = true; mbRow.childControlHeight = true; mbRow.childForceExpandWidth = false;
            HK.T(miniBadge.transform, "Mini-Pause – Bauen, Aufwerten und Fähigkeiten weiter möglich · Leertaste setzt fort", HK.SMALL, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            miniBadge.gameObject.SetActive(false);

            strip = HK.Flow(stageTop, 4, true);

            bossBox = Ui.Panel(stageTop, "Boss", new Color(15 / 255f, 11 / 255f, 25 / 255f, 0.88f), true, Theme.Round);
            bossBox.GetComponent<Outline>().effectColor = new Color(1f, 110 / 255f, 110 / 255f, 0.6f);
            var bv = bossBox.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.padding = new RectOffset(12, 12, 7, 9); bv.spacing = 3; bv.childControlWidth = true; bv.childControlHeight = true; bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            Ui.Size(bossBox, 560);
            bossName = HK.T(bossBox.transform, "", HK.FS, Theme.Hex(0xffd0d0), TextAnchor.MiddleLeft, FontStyle.Bold);
            bossPhase = HK.T(bossBox.transform, "", HK.SMALL, Theme.Warn);
            bossHp = new HudBar(bossBox.transform, Theme.Hex(0xe0503f), 16, 11, true);
            bossPoise = new HudBar(bossBox.transform, Theme.Hex(0x4ac8bc), 10, 9);
            bossBox.gameObject.SetActive(false);

            var wc = Ui.Column(stageTop, 4);
            wc.childAlignment = TextAnchor.UpperCenter;
            wc.childForceExpandWidth = false;
            warnings = wc.GetComponent<RectTransform>();

            // Turmübersicht
            towerWin = Ui.Panel(stage, "TowerList", Theme.PanelSolid, true, Theme.Round);
            towerWin.GetComponent<Outline>().effectColor = Theme.LineStrong;
            var tw = towerWin.rectTransform;
            tw.anchorMin = new Vector2(1, 0); tw.anchorMax = new Vector2(1, 1); tw.pivot = new Vector2(1, 1);
            tw.sizeDelta = new Vector2(470, -16); tw.anchoredPosition = new Vector2(-64, -8);
            var twv = towerWin.gameObject.AddComponent<VerticalLayoutGroup>();
            twv.padding = new RectOffset(14, 14, 10, 12); twv.spacing = 6;
            twv.childControlWidth = true; twv.childControlHeight = true; twv.childForceExpandWidth = true; twv.childForceExpandHeight = false;
            towerWinHover = towerWin.gameObject.AddComponent<HudHover>();
            towerWin.gameObject.SetActive(false);

            var ver = HK.T(stage, $"v{Application.version}", 12, new Color(1, 1, 1, 0.55f), TextAnchor.LowerRight);
            ver.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(ver.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-58, 4), new Vector2(200, 16));

            // Seitenleiste (immer sichtbar wie im Desktop-Layout: grid-template-columns: 1fr 320px)
            panel = Ui.Panel(middle, "Panel", HK.PanelBg, false, Theme.RoundSmall);
            var pr = panel.rectTransform;
            pr.anchorMin = new Vector2(1, 0); pr.anchorMax = new Vector2(1, 1); pr.pivot = new Vector2(1, 0.5f);
            pr.sizeDelta = new Vector2(PANEL_W, 0); pr.anchoredPosition = Vector2.zero;
            var pEdge = HK.Plain(panel.transform, "Edge", Theme.Line);
            var pe = pEdge.rectTransform; pe.anchorMin = new Vector2(0, 0); pe.anchorMax = new Vector2(0, 1); pe.pivot = new Vector2(0, 0.5f); pe.sizeDelta = new Vector2(1, 0);
            panelBody = Ui.Scroll(panel.transform, out panelScroll, 6, new RectOffset(15, 15, 12, 22));
            Ui.Fill(panelScroll.GetComponent<RectTransform>());
            panelHover = panel.gameObject.AddComponent<HudHover>();

            // ---------------------------------------------------------- Turmleiste
            var tb = HK.Plain(root, "TowerBar", HK.BarBg, true);
            var tbr = tb.rectTransform;
            tbr.anchorMin = new Vector2(0, 0); tbr.anchorMax = new Vector2(1, 0); tbr.pivot = new Vector2(0.5f, 0);
            tbr.sizeDelta = new Vector2(0, BAR_H); tbr.anchoredPosition = Vector2.zero;
            var tbLine = HK.Plain(tb.transform, "Edge", new Color(183 / 255f, 123 / 255f, 1f, 0.5f));
            var tbl = tbLine.rectTransform; tbl.anchorMin = new Vector2(0, 1); tbl.anchorMax = new Vector2(1, 1); tbl.pivot = new Vector2(0.5f, 1); tbl.sizeDelta = new Vector2(0, 1);
            var tRow = HK.HRow(tb.transform, 4, TextAnchor.MiddleLeft);
            tRow.padding = new RectOffset(6, 6, 6, 6);
            towerBar = tRow.GetComponent<RectTransform>();
            Ui.Fill(towerBar);
            if (s.rules.endless)
            {
                limitStat = Ui.Button(towerBar, "", () => toggleLimitPop(), Ui.BtnKind.Ghost, 12);
                Ui.Size(limitStat, 60, TBTN);
                limitStat.GetComponent<Outline>().effectColor = Theme.LineStrong;
                var lbl = limitStat.GetComponentInChildren<UText>();
                lbl.gameObject.SetActive(false);
                var li = HK.Img(limitStat.transform, Icons.Get("shield"), 18, Theme.Text);
                Ui.Place(li.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -5), new Vector2(18, 18));
                limitV = HK.T(limitStat.transform, "", 13, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                Ui.Place(limitV.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -3), new Vector2(52, 16));
                limitB = HK.T(limitStat.transform, "", 10, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                Ui.Place(limitB.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 5), new Vector2(52, 13));
                Ui.Tip(limitStat, () => limitTitle);
            }
            foreach (var id in Towers.BUILD_ORDER)
            {
                var d = Towers.TOWERS[id];
                if (d.mazingOnly == true && !mazing) continue;
                if (d.laneOnly == true && (mazing || !s.rules.endless)) continue;
                bool banned = s.rules.bannedTowers.Contains(id);
                towerBtns[id] = MakeTowerBtn(id, banned);
            }

            // ---------------------------------------------------------- Dialog- und Popup-Ebene
            modal = HK.Plain(root, "Modal", new Color(8 / 255f, 6 / 255f, 14 / 255f, 0.6f), true);
            Ui.Fill(modal.rectTransform);
            modal.gameObject.SetActive(false);
            popLayer = Ui.Rt(Ui.Node("Popups", root));
            Ui.Fill(popLayer);

            buildHeroBar();
            goldShown = s.gold;
            goldLast = s.gold;
        }

        /// <summary>Entfernt das HUD (Ende der Sitzung).</summary>
        public void dispose()
        {
            if (tipBox != null) UnityEngine.Object.Destroy(tipBox.gameObject);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        UText StatPill(Transform p, string icon, Color icColor, Color vColor, string title, out Image pill)
        {
            pill = Ui.Panel(p, "Stat", new Color(0, 0, 0, 0.3f), true, Theme.Round);
            var h = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(10, 13, 2, 2); h.spacing = 5; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Ui.Size(pill, -1, 34);
            HK.Img(pill.transform, Icons.Get(icon), 20, icColor);
            var t = HK.T(pill.transform, "", HK.FS, vColor, TextAnchor.MiddleLeft, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            HK.Tip(pill, title);
            return t;
        }

        Button CamBtn(Transform p, string icon, Action act, string title)
        {
            var b = HK.IconBtn(p, icon, act, 44);
            b.GetComponent<Image>().color = new Color(28 / 255f, 22 / 255f, 44 / 255f, 0.85f);
            HK.Tip(b, title);
            return b;
        }

        static string towerKey(string id) { return Keys.keyName(Keys.boundKey($"tower:{(id == "lanebarricade" ? "barricade" : id)}")); }

        TowerBtn MakeTowerBtn(string id, bool banned)
        {
            var d = Towers.TOWERS[id];
            var tb = new TowerBtn { id = id };
            tb.bg = Ui.Panel(towerBar, "TBtn", Theme.Hex(0x28203c), true, Theme.Round);
            tb.outline = tb.bg.GetComponent<Outline>();
            tb.outline.effectColor = Theme.LineStrong;
            tb.le = Ui.Size(tb.bg, TBTN, TBTN);
            tb.btn = tb.bg.gameObject.AddComponent<Button>();
            tb.btn.targetGraphic = tb.bg;
            var nav = tb.btn.navigation; nav.mode = Navigation.Mode.None; tb.btn.navigation = nav;
            var cb = tb.btn.colors; cb.highlightedColor = new Color(1.25f, 1.2f, 1.4f); cb.colorMultiplier = 1.4f; cb.disabledColor = new Color(1, 1, 1, 0.5f); tb.btn.colors = cb;
            tb.cg = tb.bg.gameObject.AddComponent<CanvasGroup>();
            // Elementfarbe als schmaler Streifen oben (Feuer, Frost, Blitz …) – Türme auf einen Blick unterscheidbar
            tb.elem = HK.Plain(tb.bg.transform, "Element", elemColor(d.damageType));
            var er = tb.elem.rectTransform; er.anchorMin = new Vector2(0, 1); er.anchorMax = new Vector2(1, 1); er.pivot = new Vector2(0.5f, 1);
            er.anchoredPosition = new Vector2(0, -2); er.sizeDelta = new Vector2(-12, 4);
            tb.elem.raycastTarget = false;
            var ic = HK.Img(tb.bg.transform, Icons.Model(id, 160), TICON);
            Ui.Place(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -3), new Vector2(TICON, TICON));
            tb.icon = ic;
            tb.cost = HK.T(tb.bg.transform, U.Fmt(d.cost), 13, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            tb.cost.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Place(tb.cost.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(TBTN, 17));
            tb.cost.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
            tb.key = HK.T(tb.bg.transform, towerKey(id), 12, Theme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Place(tb.key.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(5, -5), new Vector2(30, 15));
            tb.key.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            tb.prespec = HK.T(tb.bg.transform, "★", 11, HK.StarGold, TextAnchor.LowerLeft);
            Ui.Place(tb.prespec.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 16), new Vector2(14, 14));
            tb.prespec.gameObject.SetActive(false);
            tb.free = HK.T(tb.bg.transform, "∞", 12, Theme.Hex(0x9be36b), TextAnchor.UpperRight, FontStyle.Bold);
            Ui.Place(tb.free.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -1), new Vector2(16, 14));
            tb.free.gameObject.SetActive(false);
            // Fusionsmarke (Symbol + Beschriftung über dem Knopf), standardmäßig aus
            // Befehlsmarke: Turm gehört zur Affinität des Helden (voller Befehlsbonus)
            var heS = sim.s.he;
            if (heS != null)
            {
                var fg = HK.Img(tb.bg.transform, Icons.Get("flag"), 15, Theme.Hex(Champions.CHAMPIONS[heS.champion.id].accent));
                fg.raycastTarget = false;
                Ui.Place(fg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(4, -21), new Vector2(15, 15));
                fg.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
                tb.cmd = fg;
                fg.gameObject.SetActive(false);
            }
            tb.fuse = HK.Img(tb.bg.transform, Icons.Get("fuse"), 20);
            tb.fuse.color = FUSE_COL;
            tb.fuse.raycastTarget = false;
            Ui.Place(tb.fuse.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-2, -16), new Vector2(20, 20));
            tb.fuse.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
            tb.fuse.gameObject.SetActive(false);
            tb.fuseLbl = HK.T(tb.bg.transform, "Fusion", 12, Theme.Hex(0x10202a), TextAnchor.MiddleCenter, FontStyle.Bold);
            tb.fuseLbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            var fl = Ui.Node("FuseTag", tb.bg.transform).AddComponent<Image>();
            fl.sprite = Theme.Round; fl.type = Image.Type.Sliced; fl.color = FUSE_COL; fl.raycastTarget = false;
            Ui.Place(fl.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(62, 18));
            tb.fuseLbl.transform.SetParent(fl.transform, false);
            Ui.Fill(tb.fuseLbl.rectTransform);
            tb.fuseLbl.raycastTarget = false;
            fl.gameObject.SetActive(false);
            if (banned) { tb.btn.interactable = false; Common.dim(tb.bg, 0.7f); }
            tb.btn.onClick.AddListener(() =>
            {
                if (specPop != null && specPopTower == id) { closeSpecPop(); return; }
                if (EconomySystem.prespecAvailable(sim, id) && !(sim.s.he != null && !PassiveTree.towerUnlocked(id, sim.s.he, sim.s.wave))) { openSpecPop(id); return; }
                a.selectBuild(v.buildDef == id ? null : id);
            });
            tb.hover = tb.bg.gameObject.AddComponent<HudHover>();
            tb.hover.onEnter = (e) => { if (HK.IsMouse(e) && string.IsNullOrEmpty(v.buildDef) && v.selectedTower == 0) hoverInfo = id; };
            tb.hover.onExit = () => { hoverInfo = null; };
            Ui.Tip(tb.bg, () => towerTitle(id));
            towerBtnList.Add(tb);
            return tb;
        }

        static Color elemColor(string dmg)
        {
            if (string.IsNullOrEmpty(dmg)) return new Color(0.62f, 0.58f, 0.7f);
            int c = Palette.damageColor(dmg);
            return new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);
        }

        /// <summary>Gesperrter Turm: schmaler Chip (kleines Symbol, Freischaltwelle) statt vollem Knopf – spart Platz in der Leiste.</summary>
        void setChip(TowerBtn tb, bool chip)
        {
            if (tb.chip == chip && tb.le != null && tb.le.preferredWidth > 0) return;
            tb.chip = chip;
            float w = chip ? TBTN_LOCKED : TBTN;
            tb.le.preferredWidth = w; tb.le.minWidth = w;
            ((RectTransform)tb.bg.transform).sizeDelta = new Vector2(w, TBTN);
            float ic = chip ? 30 : TICON;
            Ui.Place(tb.icon.rectTransform, new Vector2(0.5f, chip ? 0.5f : 1), new Vector2(0.5f, chip ? 0.5f : 1), chip ? new Vector2(0, 6) : new Vector2(0, -3), new Vector2(ic, ic));
            tb.icon.color = chip ? new Color(0.62f, 0.62f, 0.68f, 1f) : Color.white;
            tb.key.gameObject.SetActive(!chip);
            tb.cost.fontSize = chip ? 11 : 13;
            tb.cost.rectTransform.sizeDelta = new Vector2(w, 17);
            tb.elem.rectTransform.sizeDelta = new Vector2(chip ? -8 : -12, chip ? 3 : 4);
        }

        /// <summary>Turm gehört zur Affinität des Helden (Lyra führt alle an – dann keine Marke).</summary>
        bool commandMarked(string id)
        {
            var he = sim.s.he;
            var prof = he != null ? HeroCommand.profileOf(he.champion.id) : null;
            if (prof == null || (prof.types.Length > 0 && prof.types[0] == "*")) return false;
            return HeroCommand.affine(he, id);
        }

        string towerTitle(string id)
        {
            var s = sim.s;
            var d = Towers.TOWERS[id];
            if (s.he != null && !PassiveTree.towerUnlocked(id, s.he, s.wave))
                return $"{d.name} – wird ab Welle {U.Fmt(PassiveTree.towerUnlockWave(id, s.he))} freigeschaltet";
            if (id == "lanebarricade")
            {
                var ch = EconomySystem.laneCharges(sim);
                var lbk = Scaling.LANE_BARRICADE;
                return $"Lane-Barrikade ({Keys.keyName(Keys.boundKey("tower:barricade"))}) – auf die Lane setzen, verlangsamt Bodengegner um 50 %. {(ch.left > 0 ? $"{U.Fmt(ch.left)} verfügbar" : "Abklingzeit läuft")} (höchstens {U.Fmt(lbk.maxCharges)} je {U.Fmt(lbk.window / 60)} Minuten, nicht direkt vor einem Riss).";
            }
            string dmg = d.damageType != null ? $" · {HudText.Dmg(d.damageType)}" : "";
            string lim = s.he != null && s.rules.endless && !Scaling.countsTowardLimit(id) ? " · zählt nicht zum Turmlimit" : "";
            string fz = towerBtns.TryGetValue(id, out var tbx) && tbx.fuseWith != null ? $"\n✦ Fusionspartner: neben {tbx.fuseWith} stellen" : "";
            string cmd = commandMarked(id) ? $"\nBefehl: {Champions.CHAMPIONS[s.he.champion.id].name} führt diesen Turm mit vollem Bonus an" : "";
            return $"{d.name} ({towerKey(id)}) – {d.role}{dmg}{lim}{fz}{cmd}";
        }

        static readonly Color FUSE_COL = new Color(0.48f, 0.88f, 1f, 1f);
        readonly Dictionary<string, string> fusePartners = new Dictionary<string, string>();
        double fuseSel = -1;
        string fuseSelDef;

        /// <summary>Partner des angeklickten Turms für alle Fusionsrezepte (Turmkennung → Name der Fusion).</summary>
        void updateFusePartners()
        {
            var s = sim.s;
            string def = null;
            if (v.selectedTower != 0)
                foreach (var t in s.towers) if (t.uid == v.selectedTower) { def = t.def; break; }
            if (v.selectedTower == fuseSel && def == fuseSelDef) return;
            fuseSel = v.selectedTower; fuseSelDef = def;
            fusePartners.Clear();
            if (def != null)
                foreach (var r in Towers.FUSIONS)
                {
                    string partner = r.a == def ? r.b : r.b == def ? r.a : null;
                    if (partner != null && !fusePartners.ContainsKey(partner)) fusePartners[partner] = Towers.TOWERS.TryGetValue(r.id, out var ld) ? ld.name : r.id;
                }
            string selName = def != null && Towers.TOWERS.TryGetValue(def, out var sd) ? sd.name : null;
            foreach (var tb in towerBtnList)
            {
                bool on = fusePartners.TryGetValue(tb.id, out var fname);
                tb.fuseWith = on ? $"{selName} → {fname}" : null;
                tb.fuse.gameObject.SetActive(on);
                tb.fuseLbl.transform.parent.gameObject.SetActive(on);
                if (on) tb.fuseLbl.text = "Fusion";
                tb.flags = -1; // Rahmen neu setzen
            }
        }

        // ------------------------------------------------------------ Bauplatz-Erklärung (Turmlimit)

        void toggleLimitPop()
        {
            if (limitPop != null) { UnityEngine.Object.Destroy(limitPop.gameObject); limitPop = null; return; }
            var box = HK.Box(popLayer, new Color(14 / 255f, 18 / 255f, 28 / 255f, 0.97f), new Color(1f, 0.83f, 0.42f, 0.33f), 10, 10, 6);
            limitPop = box.GetComponent<RectTransform>();
            HK.Fit(box, false, true);
            var r = HK.RectIn(limitStat.GetComponent<RectTransform>(), popLayer);
            float left = Mathf.Max(8, r.xMin - popLayer.rect.xMin);
            float bottom = Mathf.Max(8, r.yMax - popLayer.rect.yMin + 8);
            Ui.Place(limitPop, Vector2.zero, Vector2.zero, new Vector2(left, bottom), new Vector2(340, 0));
            limitPopSig = null;
            fillLimitPop();
        }

        void fillLimitPop()
        {
            if (limitPop == null) return;
            var s = sim.s;
            double lim = EconomySystem.currentTowerLimit(sim);
            double n = EconomySystem.countedTowers(sim);
            double free = Math.Max(0, lim - n);
            double fb = Scaling.focusBonus(n, lim);
            double per = U.Round(Scaling.TOWER_LIMIT.focusPer * 100);
            double max = U.Round(Scaling.TOWER_LIMIT.focusMax * 100);
            sig.Reset().N(n).C('/').N(lim).C('/').N(s.wave).C('/').N(s.bossSlots ?? 0);
            if (!sig.Changed(ref limitPopSig)) return;
            var t = limitPop.transform;
            HK.Clear(t);
            var gold = HK.StarGold;
            HK.T(t, $"Bauplätze: {U.Fmt(n)} von {U.Fmt(lim)} belegt", 14, gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            HK.T(t, "Im Endlosmodus kannst du nur begrenzt viele Türme bauen. Nicht mit gezählt werden Barrikaden, Goldgräber und Unterstützungstürme, die andere Türme verstärken (Leitpylon, Totemschrein) – die kannst du frei bauen. Eine Fusion macht aus zwei Türmen einen.", HK.TINY);
            HK.T(t, $"✦ Fokusbonus: +{U.Fmt(U.Round(fb * 100))} % Schaden", 14, gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            HK.T(t, $"Weniger ist mehr: Jeder Bauplatz, den du frei lässt, macht ALLE deine Türme um {U.Fmt(per)} % stärker (höchstens +{U.Fmt(max)} %).", HK.TINY);
            HK.T(t, $"Jetzt: {U.Fmt(free)} freie Plätze × {U.Fmt(per)} % = +{U.Fmt(Math.Min(max, free * per))} % Schaden{(free * per > max ? $" (gedeckelt auf {U.Fmt(max)} %)" : "")}. Baust du einen Turm mehr, sinkt der Bonus um {U.Fmt(per)} %.", HK.TINY);
            HK.T(t, "Neue Bauplätze", 14, gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            HK.T(t, $"+1 alle {U.Fmt(Scaling.TOWER_LIMIT.perWaves)} Wellen (nächster ab Welle {U.Fmt(Scaling.nextTowerSlotWave(s.wave))}) und +1 nach jeder geschafften Bosswelle (bisher +{U.Fmt(s.bossSlots ?? 0)}).", HK.TINY);
            var row = HK.HRow(t, 6);
            HK.Sm(row.transform, "Verstanden", () => toggleLimitPop());
        }

        // ------------------------------------------------------------ Spezialisierung beim Kauf (3.1)

        void closeSpecPop()
        {
            if (specPop != null) UnityEngine.Object.Destroy(specPop.gameObject);
            specPop = null;
            specPopTower = null;
            specPopHover = null;
        }

        void openSpecPop(string id)
        {
            closeSpecPop();
            if (!towerBtns.TryGetValue(id, out var tbn)) return;
            var d = Towers.TOWERS[id];
            double goldNow = sim.s.gold;
            var box = HK.Box(popLayer, new Color(14 / 255f, 18 / 255f, 28 / 255f, 0.97f), new Color(1f, 0.83f, 0.42f, 0.33f), 8, 8, 6);
            HK.Fit(box, false, true);
            specPop = box.GetComponent<RectTransform>();
            specPopTower = id;
            specPopHover = box.gameObject.AddComponent<HudHover>();
            HK.T(box.transform, $"{d.name} kaufen", 14, HK.StarGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            void opt(double spec, string title, string desc, double cost)
            {
                var o = HK.HBox(box.transform, new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.1f), 8, 6, 8);
                var btn = o.gameObject.AddComponent<Button>();
                btn.targetGraphic = o.GetComponent<Image>();
                var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
                btn.onClick.AddListener(() => { closeSpecPop(); a.selectBuild(id, spec); });
                var col = HK.VCol(o.transform, 1);
                HK.Grow(col);
                HK.T(col.transform, title, 13, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                HK.T(col.transform, desc, 12, new Color(1, 1, 1, 0.75f));
                var c = HK.T(o.transform, $"{HK.fmt(cost)} Gold", 13, goldNow < cost ? Theme.Hex(0xff7a6b) : HK.StarGold, TextAnchor.MiddleRight, FontStyle.Bold);
                c.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            opt(0, "Normal (Stufe 1)", d.role, EconomySystem.towerCost(sim, id));
            for (int i = 0; i < Math.Min(2, d.specs.Count); i++)
            {
                var sp = d.specs[i];
                opt(i + 1, $"★ {sp.name} (Höchststufe)", sp.description, EconomySystem.prespecCost(sim, id, i + 1));
            }
            HK.T(box.transform, $"Spezialisierte Türme werden direkt auf Höchststufe gebaut (+{U.Fmt(U.Round((EconomySystem.PRESPEC.premium - 1) * 100))} % Aufpreis).", 11, new Color(1, 1, 1, 0.7f));
            var r = HK.RectIn(tbn.bg.rectTransform, popLayer);
            float w = popLayer.rect.width;
            float left = Mathf.Max(8, Mathf.Min(w - 300, (r.center.x - popLayer.rect.xMin) - 140));
            float bottom = Mathf.Max(8, r.yMax - popLayer.rect.yMin + 8);
            Ui.Place(specPop, Vector2.zero, Vector2.zero, new Vector2(left, bottom), new Vector2(280, 0));
        }

        // ------------------------------------------------------------ Banner

        public void banner(string title, string sub = "", string kind = "wave", double ms = 2600)
        {
            var bg = Ui.Panel(banners, "Banner", new Color(12 / 255f, 8 / 255f, 22 / 255f, 0.55f), false, Theme.Round);
            var col = bg.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(40, 40, 8, 10); col.spacing = 2; col.childAlignment = TextAnchor.MiddleCenter;
            col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = false; col.childForceExpandHeight = false;
            var cg = bg.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false; cg.alpha = 0;
            if (kind == "boss") Spaced(HK.T(bg.transform, "EIN BOSS ERSCHEINT", 14, Theme.Hex(0xff8a8a), TextAnchor.MiddleCenter, FontStyle.Bold));
            int size = kind == "boss" ? 58 : kind == "clear" ? 36 : kind == "info" ? 32 : 48;
            Color tc = kind == "boss" ? Theme.Hex(0xffd0c8) : kind == "clear" ? Theme.Hex(0xd8ffe0) : kind == "lose" ? Theme.Hex(0xffb0b0) : kind == "info" ? Theme.Hex(0xffe0b0) : Theme.Hex(0xfff4d0);
            Color glow = kind == "boss" ? new Color(1, 0.24f, 0.24f, 0.7f) : kind == "clear" ? new Color(0.4f, 1, 0.63f, 0.45f) : kind == "lose" ? new Color(1, 0.24f, 0.24f, 0.6f) : new Color(1, 0.85f, 0.29f, 0.5f);
            var tt = HK.T(bg.transform, title, size, tc, TextAnchor.MiddleCenter, FontStyle.Bold);
            tt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = tt.gameObject.AddComponent<Outline>(); o.effectColor = glow; o.effectDistance = new Vector2(2, -2);
            tt.gameObject.AddComponent<Shadow>().effectColor = Color.black;
            if (!string.IsNullOrEmpty(sub)) Spaced(HK.T(bg.transform, sub.ToUpperInvariant(), HK.FS, Theme.Hex(0xe8dcff), TextAnchor.MiddleCenter));
            if (kind == "boss") letterOn++;
            var el = new BannerEl { rt = bg.rectTransform, cg = cg, t = 0, ms = (float)ms, boss = kind == "boss" };
            bannerList.Add(el);
            while (bannerList.Count > 2) removeBanner(bannerList[0]);
        }

        int letterOn;

        static void Spaced(UText t)
        {
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.gameObject.AddComponent<Shadow>().effectColor = Color.black;
            // PORT: letter-spacing gibt es in uGUI-Text nicht – Großschreibung wie im CSS (text-transform: uppercase).
        }

        void removeBanner(BannerEl el)
        {
            bannerList.Remove(el);
            if (el.boss) letterOn = Math.Max(0, letterOn - 1);
            if (el.rt != null) UnityEngine.Object.Destroy(el.rt.gameObject);
        }

        public void bossBanner(string id)
        {
            var b = Enemies.BOSSES[id];
            banner(b.name, b.title, "boss", 3000);
        }

        // ------------------------------------------------------------ Taktgeber: Banner, Tooltips, Zeiger, kleine Animationen

        public void tick(float dt)
        {
            if (root == null) return;
            // Banner ein-/ausblenden
            for (int i = bannerList.Count - 1; i >= 0; i--)
            {
                var b = bannerList[i];
                b.t += dt * 1000f;
                if (b.rt == null) { bannerList.RemoveAt(i); continue; }
                float inP = Mathf.Clamp01(b.t / 500f);
                float outP = Mathf.Clamp01((b.t - b.ms) / 500f);
                float ease = 1 - Mathf.Pow(1 - inP, 3);
                b.cg.alpha = ease * (1 - outP);
                float sc = (0.85f + 0.15f * ease) * (1 + 0.02f * outP);
                b.rt.localScale = new Vector3(sc, sc, 1);
                if (b.t >= b.ms + 500) removeBanner(b);
            }
            float sh = stage.rect.height;
            float by = -Mathf.Max(70, sh * 0.16f);
            if (Mathf.Abs(banners.anchoredPosition.y - by) > 0.5f) banners.anchoredPosition = new Vector2(0, by);
            float targetL = letterOn > 0 ? sh * 0.09f : 0;
            letterH = Mathf.MoveTowards(letterH, targetL, Mathf.Max(1, sh * 0.09f) * dt * 2f);
            letterTop.rectTransform.sizeDelta = new Vector2(0, letterH);
            letterBot.rectTransform.sizeDelta = new Vector2(0, letterH);

            // Gold: kurzes Anschwellen bei Einnahmen
            if (goldBump > 0)
            {
                goldBump = Mathf.Max(0, goldBump - dt);
                float k = Mathf.Sin((1 - goldBump / 0.35f) * Mathf.PI) * 0.12f;
                goldStat.rectTransform.localScale = new Vector3(1 + k, 1 + k, 1);
            }
            // Pulsieren: wenig Kernenergie, gefährliche Warnungen
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f);
            var lo = livesStat.GetComponent<Outline>();
            if (lo != null) lo.effectColor = livesLow ? Color.Lerp(Theme.Line, Theme.Bad, pulse) : Theme.Line;
            float wp = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2.5f);
            for (int i = 0; i < warnPulse.Count; i++) if (warnPulse[i] != null) warnPulse[i].effectColor = Color.Lerp(new Color(1, 0.42f, 0.42f, 0.4f), Theme.Bad, wp);

            // Zeiger im Panel/in der Turmübersicht gedrückt halten → kein Neuaufbau (sonst geht der Klick verloren)
            var p = Pointer.current;
            bool pressed = p != null && p.press.isPressed;
            if (p != null && p.press.wasPressedThisFrame)
            {
                if ((panelHover != null && panelHover.over) || (towerWinHover != null && towerWinHover.over)) pointerHeld = true;
                // Spezialisierungswahl schließt bei Klick daneben
                if (specPop != null && !(specPopHover != null && specPopHover.over))
                {
                    bool onBtn = specPopTower != null && towerBtns.TryGetValue(specPopTower, out var tbn) && tbn.hover.over;
                    if (!onBtn) closeSpecPop();
                }
            }
            if (!pressed) pointerHeld = false;

            tickTooltip(dt, p);
        }

        void tickTooltip(float dt, Pointer p)
        {
            var src = TipSource.Current;
            if (src != null && (src.transform == null || !src.transform.IsChildOf(root))) src = null;
            if (src != tipSrc) { tipSrc = src; tipT = 0; }
            string text = null;
            if (src != null)
            {
                tipT += dt;
                if (tipT > 0.45f && src.text != null) text = src.text();
            }
            if (string.IsNullOrEmpty(text) || p == null)
            {
                if (tipBox != null && tipBox.gameObject.activeSelf) tipBox.gameObject.SetActive(false);
                return;
            }
            if (tipBox == null)
            {
                Transform layer = GameApp.I != null && GameApp.I.tooltipLayer != null ? (Transform)GameApp.I.tooltipLayer : root;
                var img = Ui.Panel(layer, "Tooltip", new Color(14 / 255f, 11 / 255f, 24 / 255f, 0.96f), true, Theme.RoundSmall);
                img.GetComponent<Outline>().effectColor = Theme.LineStrong;
                img.raycastTarget = false;
                var col = img.gameObject.AddComponent<VerticalLayoutGroup>();
                col.padding = new RectOffset(9, 9, 6, 7); col.childControlWidth = true; col.childControlHeight = true; col.childForceExpandWidth = false;
                HK.Fit(col, true, true);
                tipText = HK.T(img.transform, "", 14, Theme.Text);
                tipLe = tipText.gameObject.AddComponent<LayoutElement>();
                tipBox = img.rectTransform;
                tipBox.pivot = new Vector2(0, 1);
                tipBox.anchorMin = tipBox.anchorMax = new Vector2(0.5f, 0.5f);
            }
            if (!tipBox.gameObject.activeSelf) tipBox.gameObject.SetActive(true);
            if (tipText.text != text)
            {
                tipText.text = text;
                tipLe.preferredWidth = Mathf.Min(360, tipText.preferredWidth + 1);
            }
            var parentRt = tipBox.parent as RectTransform;
            var canvas = tipBox.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 lp;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, p.position.ReadValue(), cam, out lp))
            {
                var pr = parentRt.rect;
                float w = tipBox.rect.width, h = tipBox.rect.height;
                float x = Mathf.Min(lp.x + 14, pr.xMax - w - 4);
                float y = lp.y - 20;
                if (y - h < pr.yMin + 4) y = lp.y + h + 10;
                tipBox.anchoredPosition = new Vector2(Mathf.Max(pr.xMin + 4, x), Mathf.Min(pr.yMax - 4, y));
            }
            tipBox.SetAsLastSibling();
        }

        // ------------------------------------------------------------ Aktualisierung (pro Frame, nur bei Änderung)

        public void update()
        {
            var s = sim.s;
            double now = HK.Now();
            double dt = Math.Min(0.1, (now - lastFrame) / 1000);
            lastFrame = now;
            // Gold zählt sichtbar hoch/runter
            if (s.gold > goldLast) goldBump = 0.35f;
            goldLast = s.gold;
            double diff = s.gold - goldShown;
            // Einnahmen zählen sichtbar hoch, Ausgaben werden sofort angezeigt.
            goldShown = diff <= 0 || Math.Abs(diff) < 1 ? s.gold : goldShown + Math.Max(1, diff * Math.Min(1, dt * 9));
            double gr = U.Round(goldShown);
            if (gr != goldTxt) { goldTxt = gr; gold.text = HK.fmt(goldShown); }
            if (s.lives != livesA || s.maxLives != livesB) { livesA = s.lives; livesB = s.maxLives; lives.text = $"{U.Fmt(s.lives)}/{U.Fmt(s.maxLives)}"; }
            if (s.rules.endless && limitStat != null)
            {
                double lim = EconomySystem.currentTowerLimit(sim);
                double n = EconomySystem.countedTowers(sim);
                double fb = Scaling.focusBonus(n, lim);
                sig.Reset().N(lim).C('/').N(n).C('/').N(U.Round(fb * 100));
                if (sig.Changed(ref limitSig))
                {
                    limitV.text = U.IsFinite(lim) ? $"{U.Fmt(n)}/{U.Fmt(lim)}" : U.Fmt(n);
                    limitB.text = fb > 0 ? $"+{U.Fmt(U.Round(fb * 100))} %" : "±0 %";
                    limitB.color = fb > 0 ? Theme.Hex(0x9be36b) : new Color(1, 1, 1, 0.85f);
                    limitV.color = n >= lim ? Theme.Hex(0xffb36b) : Theme.Text;
                    limitTitle = $"Bauplätze {U.Fmt(n)}/{U.Fmt(lim)} · freie Plätze machen alle Türme stärker: jetzt +{U.Fmt(U.Round(fb * 100))} % Schaden. Klicken für Erklärung.";
                }
                fillLimitPop();
            }
            livesLow = s.lives <= Math.Max(3, s.maxLives * 0.25);
            sig.Reset().N(s.wave).C('/').N(s.rules.waves).B(s.rules.endless);
            if (sig.Changed(ref waveTxt)) wave.text = s.rules.endless ? $"{U.Fmt(s.wave)} · Endlos" : $"{U.Fmt(s.wave)}/{U.Fmt(s.rules.waves)}";
            if (energy != null)
            {
                double ev = Math.Floor(s.riftEnergy);
                if (ev != energyV) { energyV = ev; energy.text = U.Fmt(ev); }
            }
            updateStart();
            updateUpAll();
            HK.On(lockBtn, v.rotationLocked);
            bool mini = s.paused && v.miniPaused && !sim.isOver();
            HK.On(miniBtn, mini);
            HK.Show(miniBadge, mini);
            if (s.speed != speedV) { speedV = s.speed; HK.SetBtnText(speedBtn, $"{U.Fmt(s.speed)}×"); }
            if (s.paused != pauseShown) { pauseShown = s.paused; if (pauseBtn != null) HK.SetIcon(pauseBtn, s.paused ? "play" : "pause"); }
            updateTowerBtns();
            updateStrip();
            updateBoss();
            updateWarnings();
            updateMods();
            updateHeroBar();
            updatePanel();
            updateTowerList();
            updateModal();
            updateMinimap(dt);
        }

        void updateStart()
        {
            var s = sim.s;
            bool build = s.phase == "build";
            bool hasAuto = s.he != null && build && s.he.autoTimer > 0;
            string why = s.he != null && s.phase == "combat" ? WaveDirector.earlyCallState(s) : null;
            sig.Reset().S(s.phase).C('|').N(s.wave).C('|').N(hasAuto ? Math.Ceiling(s.he.autoTimer) : -1).C('|').B(s.he != null).C('|').S(why);
            if (!sig.Changed(ref startSig)) return;
            string auto = hasAuto ? $" · {U.Fmt(Math.Ceiling(s.he.autoTimer))} s" : "";
            bool enabled = build;
            if (s.he != null && s.phase == "combat")
            {
                // Helden-Endlos: nächste Welle vorzeitig rufen, sobald die aktuelle vollständig erschienen ist
                enabled = why == null;
                startTitle = why ?? $"Nächste Welle jetzt rufen (Enter) – +{U.Fmt(WaveDirector.EARLY_CALL_GOLD)} Gold je Gegner auf dem Feld";
                HK.SetBtnText(startBtn, why != null ? "Kampf" : $"Welle {U.Fmt(s.wave + 1)} rufen");
            }
            else
            {
                startTitle = s.he != null ? "Nächste Welle sofort starten (Leertaste/Enter)" : "Welle starten (Leertaste)";
                HK.SetBtnText(startBtn, build ? $"Welle {U.Fmt(s.wave + 1)}{auto}" : s.phase == "combat" ? "Kampf" : "…");
            }
            HK.Enable(startBtn, enabled);
            bool cd = auto != "";
            if (cd != startCountdown)
            {
                startCountdown = cd;
                startBtn.GetComponent<Image>().color = cd ? Theme.Hex(0x4a3075) : Theme.BtnPrimary;
            }
        }

        void updateUpAll()
        {
            var s = sim.s;
            sig.Reset().N(Math.Floor(s.gold)).C('|');
            foreach (var t in s.towers) sig.N(t.level).N(t.spec);
            sig.C('|').N(s.towers.Count);
            if (!sig.Changed(ref upAllSig)) return;
            var ups = EconomySystem.upgradableTowers(sim);
            double cheapest = double.PositiveInfinity;
            foreach (var t in ups) cheapest = Math.Min(cheapest, EconomySystem.nextUpgradeCost(t) ?? double.PositiveInfinity);
            // Spezialisierung bereit (Höchststufe, noch keine Spezialisierung, bezahlbar): Zähler auf „Türme“
            int sr = 0;
            foreach (var t in s.towers)
            {
                var td = Towers.TOWERS[t.def];
                if (td.specs.Count == 2 && t.spec == 0 && td.legendary != true && EconomySystem.nextUpgradeCost(t) == null
                    && s.gold >= Math.Min(EconomySystem.specCost(t, 1), EconomySystem.specCost(t, 2))) sr++;
            }
            if (sr != specReady)
            {
                specReady = sr;
                HK.SetBtnText(towerListBtn, sr > 0 ? $"Türme ★{sr}" : "Türme");
                towerListBtn.GetComponentInChildren<UText>().color = sr > 0 ? HK.StarGold : Theme.Text;
            }
            var all = EconomySystem.upgradeAllCost(sim);
            HK.Enable(upAllBtn, !(ups.Count == 0 || cheapest > s.gold));
            upAllTitle = ups.Count > 0
                ? $"Alle Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht (günstigste zuerst, ohne Spezialisierung). {U.Fmt(ups.Count)} Türme offen, komplett {HK.fmt(all.total)} Gold."
                : "Alle Türme sind auf der höchsten Stufe (Spezialisierung im Turmpanel wählen)";
        }

        void updateTowerBtns()
        {
            var s = sim.s;
            updateFusePartners();
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < towerBtnList.Count; i++)
            {
                var tb = towerBtnList[i];
                string id = tb.id;
                if (tb.fuseWith != null)
                {
                    tb.fuse.color = new Color(FUSE_COL.r, FUSE_COL.g, FUSE_COL.b, pulse);
                    tb.outline.effectColor = new Color(FUSE_COL.r, FUSE_COL.g, FUSE_COL.b, 0.55f + 0.45f * pulse);
                }
                bool locked = s.he != null && !PassiveTree.towerUnlocked(id, s.he, s.wave);
                int flags = 0;
                string costTxt = tb.costTxt;
                if (locked)
                {
                    flags = 1;
                    double uw = PassiveTree.towerUnlockWave(id, s.he);
                    sig.Reset().C('W').N(uw);
                    if (sig.Changed(ref tb.costSig)) costTxt = $"W{U.Fmt(uw)}";
                }
                else if (id == "lanebarricade")
                {
                    var ch = EconomySystem.laneCharges(sim);
                    sig.Reset().C('L').N(ch.left).C('|').N(ch.left > 0 ? 0 : Math.Floor(ch.next));
                    if (sig.Changed(ref tb.costSig))
                        costTxt = ch.left > 0 ? $"{U.Fmt(ch.left)}/{U.Fmt(Scaling.LANE_BARRICADE.maxCharges)}" : $"{U.Fmt(Math.Floor(ch.next / 60))}:{U.PadStart(U.Fmt(Math.Floor(ch.next % 60)), 2, "0")}";
                    if (ch.left <= 0) flags |= 2;
                    if (v.buildDef == id) flags |= 4;
                }
                else
                {
                    double cost = EconomySystem.towerCost(sim, id);
                    sig.Reset().C('C').N(cost);
                    if (sig.Changed(ref tb.costSig)) costTxt = U.Fmt(cost);
                    if (s.gold < cost) flags |= 2;
                    if (v.buildDef == id) flags |= 4;
                    if (EconomySystem.prespecAvailable(sim, id)) flags |= 8;
                    if (s.rules.endless && !Scaling.countsTowardLimit(id) && Towers.TOWERS[id].attack != "none") flags |= 16;
                }
                if (costTxt != tb.costTxt) { tb.costTxt = costTxt; tb.cost.text = costTxt; }
                // Befehlsmarke (Affinität kann sich durch Befehlsrelikte ändern)
                if (tb.cmd != null)
                {
                    bool mk = !locked && commandMarked(id);
                    if (tb.cmd.gameObject.activeSelf != mk) tb.cmd.gameObject.SetActive(mk);
                }
                if (flags == tb.flags) continue;
                tb.flags = flags;
                setChip(tb, locked);
                bool poor = (flags & 2) != 0, active = (flags & 4) != 0;
                // Gesperrt/zu teuer: dunkle Abdeckung statt Durchsichtigkeit (sonst blasses Rosa mit weißer Schrift)
                tb.cg.alpha = 1f;
                Common.dim(tb.bg, !tb.btn.interactable ? 0.7f : locked ? 0.3f : poor ? 0.35f : 0f);
                tb.bg.color = locked ? Theme.Hex(0x1e1a26) : Theme.Hex(0x28203c);
                tb.cost.color = poor ? Theme.Bad : locked ? Theme.Hex(0xffd860) : Theme.Gold;
                bool fusePartner = tb.fuseWith != null;
                tb.outline.effectColor = active ? Theme.Gold : fusePartner ? FUSE_COL : Theme.LineStrong;
                tb.outline.effectDistance = active || fusePartner ? new Vector2(3, -3) : new Vector2(1, -1);
                if (fusePartner && tb.btn.interactable) Common.dim(tb.bg, locked ? 0.4f : 0f); // Partner gut sichtbar, auch wenn zu teuer
                tb.prespec.gameObject.SetActive((flags & 8) != 0);
                tb.free.gameObject.SetActive((flags & 16) != 0);
            }
        }

        /// <summary>3.0: Weltstufe und Wellen-Affixe als Chips.</summary>
        void appendWaveTraits(double? tier, List<string> affixes, string immune)
        {
            if (tier.HasValue && U.Truthy(tier.Value))
                Chip(strip.transform, $"Weltstufe {U.Fmt(tier.Value)}", Theme.Hex(0xff8a5c), Theme.Hex(0xffc0a0), null,
                    $"Weltstufe {U.Fmt(tier.Value)}: Gegner +{U.Fmt(U.Round(Scaling.SCALING.tierHp * tier.Value * 100))} % LP, mehr Rüstung und Tempo – dafür +{U.Fmt(U.Round(Scaling.SCALING.tierReward * tier.Value * 100))} % Kopfgeld");
            if (affixes == null) return;
            foreach (var af in affixes)
            {
                var d = Scaling.AFFIXES[af];
                string extra = af == "immune" && immune != null ? $" gegen {HudText.Dmg(immune)}" : "";
                string ic = HK.Glyph(d.icon);
                Chip(strip.transform, $"{(ic != "" ? ic + " " : "")}{d.name}{extra}", Theme.Hex(0xc080ff), Theme.Hex(0xe0c8ff), null, $"{d.description}{extra}");
            }
        }

        /// <summary>Chip (.chip) – Pille mit Rand, optional anklickbar, optional mit Symbol.</summary>
        Image Chip(Transform p, string text, Color border, Color color, Action onClick, string title, Sprite icon = null, bool bold = false)
        {
            var img = Ui.Panel(p, "Chip", new Color(15 / 255f, 11 / 255f, 25 / 255f, 0.8f), true, Theme.Round);
            img.GetComponent<Outline>().effectColor = border;
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(icon != null ? 5 : 11, 11, 3, 3); h.spacing = 5; h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            Ui.Size(img, -1, 30);
            if (icon != null) HK.Img(img.transform, icon, 22);
            var t = HK.T(img.transform, text, 15, color, TextAnchor.MiddleLeft, bold ? FontStyle.Bold : FontStyle.Normal);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (onClick != null)
            {
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                b.onClick.AddListener(() => onClick());
            }
            HK.Tip(img, title);
            return img;
        }

        void updateStrip()
        {
            var s = sim.s;
            if (s.phase == "build" || s.phase == "choice")
            {
                var w = WaveDirector.previewNextWave(s);
                sig.Reset().C('b').N(s.wave).C('-');
                if (w != null) sig.N(w.index); else sig.C('x');
                if (s.he != null) sig.N((s.he.profile ?? "").GetHashCode());
                if (!sig.Changed(ref stripSig)) return;
                HK.Clear(strip.transform);
                if (w == null) return;
                bool ch = w.groups.Any((g) => g.challenge == true);
                // Wellenart (Helden-Endlos) hier statt doppelt in der Kopfzeile
                string profName = null;
                if (s.he != null) foreach (var pf in HeroEndless.WAVE_PROFILES) if (pf.id == s.he.profile) { profName = pf.name; break; }
                Chip(strip.transform, $"Nächste Welle {U.Fmt(w.index)}{(profName != null ? $" · {profName}" : "")}{(ch ? " · ⚔ CHALLENGE-BOSS" : w.isBoss ? " · BOSS" : "")}:", Theme.Line, Theme.Text, null, null);
                foreach (var g in w.groups)
                {
                    var sum = HudText.waveSummary(g);
                    string id = g.enemy;
                    string gboss = g.boss;
                    Sprite img = !string.IsNullOrEmpty(gboss) ? Icons.Model(gboss) : id != "boss" ? Icons.Model(id) : null;
                    Color border = sum.boss ? Theme.Bad : g.mutation != null ? Theme.Hex(0xff7a40) : sum.elite ? Theme.Gold : Theme.LineStrong;
                    Color col = sum.boss ? Theme.Hex(0xffc2c2) : Theme.Text;
                    Chip(strip.transform, $"{sum.label} ×{U.Fmt(sum.count)}", border, col, () =>
                    {
                        v.infoEnemy = v.infoEnemy == id && string.IsNullOrEmpty(gboss) ? null : id;
                        bossInfo = gboss;
                        panelSig = "";
                    }, sum.label, img, sum.boss);
                }
                if (w.@event != null && Biomes.TERRAIN_EVENTS.TryGetValue(w.@event, out var ev))
                    Chip(strip.transform, ev.name, HK.Accent2, Theme.Text, null, ev.description);
                appendWaveTraits(w.tier, w.affixes, w.immune);
            }
            else
            {
                int alive = 0;
                foreach (var e in s.enemies) if (e.alive) alive++;
                double left = s.spawnQueue.Count + alive;
                double over = s.he != null && s.wave >= Scaling.SCALING.overrunFrom && s.he.overrunWave == s.wave ? Math.Max(0, Math.Ceiling(Scaling.SCALING.overrunAfter - (s.he.overrunT ?? 0))) : -1;
                sig.Reset().C('c').N(left).C('-').N(over).C('-').N(s.wave).C('-').S(s.phase);
                if (!sig.Changed(ref stripSig)) return;
                HK.Clear(strip.transform);
                if (s.phase == "combat") Chip(strip.transform, $"Welle {U.Fmt(s.wave)} · verbleibende Gegner: {U.Fmt(left)}{(over >= 0 ? $" · nächste rückt nach in {U.Fmt(over)} s" : "")}", Theme.Line, Theme.Text, null, null);
                if (s.currentWave != null) appendWaveTraits(s.currentWave.tier, s.currentWave.affixes, s.currentWave.immune);
            }
        }

        void updateBoss()
        {
            var s = sim.s;
            EnemyInstance b = null;
            if (s.boss != null) sim.c.enemyByUid.TryGetValue(s.boss.uid, out b);
            HK.Show(bossBox, b != null && s.boss != null);
            if (b == null || s.boss == null) return;
            var def = Enemies.BOSSES[s.boss.id];
            bool chal = b.challenge == true;
            AbilityState casting = null;
            foreach (var x in b.abilities) if (x.windup > 0) { casting = x; break; }
            int platesUp = 0;
            foreach (var p in b.plates) if (p > 0) platesUp++;
            EnemyInstance partner = null;
            foreach (var e in s.enemies) if (e.alive && !string.IsNullOrEmpty(e.boss) && e.uid != b.uid) { partner = e; break; }
            double partnerPct = partner != null ? U.Round((100 * partner.hp) / partner.maxHp) : -1;
            sig.Reset().S(s.boss.id).C('|').B(chal).C('|').S(s.boss.phaseName).C('|').N(platesUp).C('/').N(b.plates.Count).C('|').S(partner?.boss).N(partnerPct).C('|').B(b.enraged).C('|').S(casting?.id);
            if (sig.Changed(ref bossSig))
            {
                bossName.text = $"{(chal ? "⚔ CHALLENGE · " : "")}{def.name} – {def.title}";
                if (chal != bossChallenge) { bossChallenge = chal; bossBox.GetComponent<Outline>().effectColor = chal ? Theme.Gold : new Color(1f, 110 / 255f, 110 / 255f, 0.6f); }
                string plates = b.plates.Count > 0 ? $" · Schwachpunkte {platesUp}/{b.plates.Count}" : "";
                string part = partner != null ? $" · {Enemies.BOSSES[partner.boss].name}: {U.Fmt(partnerPct)} %" : "";
                bossPhase.text = $"{s.boss.phaseName}{plates}{part}{(b.enraged ? " · RASEND" : "")}{(casting != null ? $" · wirkt: {Enemies.ABILITIES[casting.id].name}" : "")}";
            }
            bossHp.Set((float)(b.hp / b.maxHp));
            bossHp.SetExtra(b.maxShield > 0 ? (float)(b.shield / b.maxShield) : 0);
            double poisePct = U.Round((100 * b.poise) / Math.Max(1, b.maxPoise));
            sig.Reset().N(U.Round(b.hp)).C('/').N(U.Round(b.maxHp)).C('/').N(U.Round(b.shield)).C('/').N(poisePct);
            if (sig.Changed(ref bossHpSig))
            {
                bossHp.label.text = $"{HK.fmt(b.hp)} / {HK.fmt(b.maxHp)}{(b.shield > 0 ? $" · Schild {HK.fmt(b.shield)}" : "")}";
                bossPoise.label.text = $"Standfestigkeit {U.Fmt(poisePct)} %";
            }
            bossPoise.Set((float)(b.poise / Math.Max(1, b.maxPoise)));
        }

        void updateWarnings()
        {
            var s = sim.s;
            int n = Math.Min(3, s.telegraphs.Count);
            sig.Reset();
            for (int i = 0; i < n; i++) { var t = s.telegraphs[i]; if (i > 0) sig.C('|'); sig.N(t.uid).C(':').N(Math.Ceiling(t.t * 2)); }
            if (!sig.Changed(ref warnSig)) return;
            HK.Clear(warnings);
            warnPulse.Clear();
            for (int i = 0; i < n; i++)
            {
                var t = s.telegraphs[i];
                bool danger = t.severity == "danger";
                var row = HK.HBox(warnings, danger ? new Color(80 / 255f, 10 / 255f, 10 / 255f, 0.92f) : new Color(60 / 255f, 20 / 255f, 10 / 255f, 0.88f), danger ? Theme.Bad : Theme.Warn, 13, 5, 14);
                if (danger) warnPulse.Add(row.GetComponent<Outline>());
                var a1 = HK.T(row.transform, $"{(danger ? "⚠" : "!")} {t.text}", 16, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                a1.horizontalOverflow = HorizontalWrapMode.Overflow;
                var a2 = HK.T(row.transform, $"{HK.fmtNum(Math.Max(0, t.t), 1)} s", 16, Theme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
                a2.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        void updateMods()
        {
            var s = sim.s;
            sig.Reset();
            for (int i = 0; i < s.modifiers.Count; i++) { if (i > 0) sig.C(','); sig.S(s.modifiers[i]); }
            if (!sig.Changed(ref modsSig)) return;
            HK.Clear(mods);
            foreach (var id in s.modifiers)
            {
                var m = Modifiers.MODIFIERS[id];
                var img = Ui.Panel(mods, "Mod", HK.ChipBg, true, Theme.RoundSmall);
                img.GetComponent<Outline>().effectColor = Theme.LineStrong;
                var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(9, 10, 3, 3); h.spacing = 6; h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false;
                Ui.Size(img, -1, 30);
                string ic = HK.Glyph(m.icon);
                if (ic != "") HK.T(img.transform, ic, 15, Theme.Text).horizontalOverflow = HorizontalWrapMode.Overflow;
                HK.T(img.transform, m.name, 14, Theme.Text).horizontalOverflow = HorizontalWrapMode.Overflow;
                var b = img.gameObject.AddComponent<Button>();
                b.targetGraphic = img;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                string mid = id;
                b.onClick.AddListener(() => onModInfo?.Invoke(mid));
                HK.Tip(img, $"{m.name}: {m.description}");
            }
        }

        // ------------------------------------------------------------ Heldenleiste

        class HeroCardEls
        {
            public Image root;
            public Outline outline;
            public string sig = "";
            public HudBar hp, time, cd;
            public UText cdText;
            public List<Button> abil = new List<Button>();
            public List<Image> abilFill = new List<Image>();
            public List<Outline> abilOutline = new List<Outline>();
            public Button cost;
            public string cdTxt;
        }

        void buildHeroBar()
        {
            var s = sim.s;
            HK.Show(heroBar, s.heroes.Count > 0);
            foreach (var hero in s.heroes)
            {
                var card = new HeroCardEls();
                card.root = Ui.Panel(heroBar, "HeroCard", new Color(15 / 255f, 11 / 255f, 25 / 255f, 0.88f), true, Theme.Round);
                card.outline = card.root.GetComponent<Outline>();
                card.outline.effectColor = Theme.LineStrong;
                var h = card.root.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(5, 6, 5, 5); h.spacing = 6;
                h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                Ui.Size(card.root, 200);
                heroCards[hero.id] = card;
            }
        }

        static string heroKey(int i) { return i == 0 ? "Z" : "X"; }

        static string lastWord(string name)
        {
            var parts = name.Split(' ');
            return parts[parts.Length - 1];
        }

        void updateHeroBar()
        {
            var s = sim.s;
            for (int i = 0; i < s.heroes.Count; i++)
            {
                var hero = s.heroes[i];
                if (!heroCards.TryGetValue(hero.id, out var card)) continue;
                var def = Heroes.HEROES[hero.id];
                var st = HeroSystem.heroStatsOf(sim, hero);
                bool placing = v.placingHero == hero.id;
                bool selected = v.selectedHero == hero.uid;
                sig.Reset().S(hero.state).C('-').N(hero.level).C('-').B(hero.pendingPerk).C('-').S(hero.mode).C('-').B(placing).C('-').B(selected).C('-').S(s.phase);
                if (sig.Changed(ref card.sig)) buildHeroCard(card, hero, i, placing, selected);
                // dynamische Werte
                if (card.hp != null) card.hp.Set((float)Math.Max(0, hero.hp / Math.Max(1, st.maxHp)));
                if (card.time != null) card.time.Set((float)Math.Max(0, Math.Min(1, hero.activeLeft / Math.Max(1, st.activeTime))));
                if (card.cd != null && card.cdText != null)
                {
                    double total = hero.state == "down" ? Heroes.HERO_RULES.downCooldown : Heroes.HERO_RULES.cooldown;
                    card.cd.Set((float)Math.Max(0, 1 - hero.cooldown / total));
                    string ct = $"{U.Fmt(Math.Ceiling(hero.cooldown))} s";
                    if (ct != card.cdTxt) { card.cdTxt = ct; card.cdText.text = ct; }
                }
                if (card.cost != null) card.cost.GetComponent<CanvasGroup>().alpha = s.riftEnergy < def.cost ? 0.6f : 1f;
                for (int slot = 0; slot < card.abil.Count; slot++)
                {
                    var r = HeroSystem.abilityReady(sim, hero, slot);
                    var hc = Theme.Hex(def.accent);
                    card.abilOutline[slot].effectColor = r.ok ? (slot == 2 ? Theme.Gold : hc) : Theme.LineStrong;
                    double p;
                    if (slot == 2) p = hero.ult / 100;
                    else
                    {
                        var ab = def.abilities[slot];
                        p = 1 - hero.abilityCd[slot] / Math.Max(1, ab.cooldown * st.abilityCdMul);
                    }
                    var fr = card.abilFill[slot].rectTransform;
                    float fp = Mathf.Round((float)Math.Max(0, Math.Min(1, p)) * 100) / 100f;
                    if (fr.anchorMax.y != fp) fr.anchorMax = new Vector2(1, fp);
                }
            }
        }

        void buildHeroCard(HeroCardEls card, HeroInstance hero, int i, bool placing, bool selected)
        {
            var def = Heroes.HEROES[hero.id];
            var hc = Theme.Hex(def.accent);
            card.abil.Clear(); card.abilFill.Clear(); card.abilOutline.Clear();
            card.hp = card.time = card.cd = null; card.cdText = null; card.cost = null; card.cdTxt = null;
            var root = card.root.transform;
            HK.Clear(root);
            card.outline.effectColor = placing || selected ? hc : Theme.LineStrong;
            card.outline.effectDistance = placing || selected ? new Vector2(2, -2) : new Vector2(1, -1);
            var cg = Ui.Get<CanvasGroup>(card.root.gameObject);
            cg.alpha = hero.state == "cooldown" || hero.state == "down" ? 0.85f : 1f;
            string hid = hero.id;
            // Porträt
            var portrait = Ui.Panel(root, "Portrait", Theme.Hex(0x120e1e), true, Theme.RoundSmall);
            portrait.GetComponent<Outline>().effectColor = hc;
            Ui.Size(portrait, 52, 52);
            var pb = portrait.gameObject.AddComponent<Button>();
            pb.targetGraphic = portrait;
            pb.onClick.AddListener(() => a.heroCard(hid));
            // PORT: Modell-ID des Helden = HeroId (IconFactory.hero im Webspiel).
            var pi = HK.Img(portrait.transform, Icons.Model(hero.id), 50, hero.state == "down" ? new Color(0.5f, 0.5f, 0.5f, 1) : Color.white);
            Ui.Place(pi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
            var key = HK.T(portrait.transform, heroKey(i), 11, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            Ui.Place(key.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -1), new Vector2(16, 14));
            key.gameObject.AddComponent<Shadow>().effectColor = Color.black;
            var lvlBg = Ui.Panel(portrait.transform, "Lvl", Theme.Gold, false, Theme.Circle);
            Ui.Place(lvlBg.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-2, 2), new Vector2(16, 16));
            var lvl = HK.T(lvlBg.transform, U.Fmt(hero.level), 11, Theme.Hex(0x2a1a00), TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Fill(lvl.rectTransform);
            HK.Tip(portrait, $"{def.name} ({heroKey(i)})");
            // Körper
            var body = HK.VCol(root, 3);
            body.childAlignment = TextAnchor.MiddleLeft;
            HK.Grow(body);
            var nameRow = HK.HRow(body.transform, 4);
            HK.Grow(HK.T(nameRow.transform, lastWord(def.name), 15, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold));
            if (hero.pendingPerk)
            {
                var pk = HK.T(nameRow.transform, "★", 15, Theme.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
                pk.raycastTarget = true;
                HK.Tip(pk, "Perk wählen");
            }
            if (hero.state == "ready")
            {
                card.cost = HK.B(body.transform, placing ? "Feld wählen…" : $"Einsetzen · {U.Fmt(def.cost)}", () => a.heroCard(hid), Ui.BtnKind.Primary, 14, 30, "energy");
                card.cost.gameObject.AddComponent<CanvasGroup>();
            }
            else if (hero.state == "active")
            {
                card.hp = new HudBar(body.transform, Theme.Hex(0x5ae09a), 6, 1);
                card.time = new HudBar(body.transform, Theme.Hex(0x6aa8ff), 4, 1);
                var row = HK.HRow(body.transform, 4, TextAnchor.MiddleLeft, true);
                string[] keys = { "A", "S", "D" };
                for (int slot = 0; slot < 3; slot++)
                {
                    var ab = slot == 2 ? def.ultimate : def.abilities[slot];
                    var bimg = Ui.Panel(row.transform, "Ab", Theme.Hex(0x1a1428), true, Theme.RoundSmall);
                    var ol = bimg.GetComponent<Outline>(); ol.effectColor = Theme.LineStrong;
                    Ui.Size(bimg, -1, 26, 1);
                    var fill = Ui.Panel(bimg.transform, "Fill", slot == 2 ? new Color(1f, 0.7f, 0.24f, 0.6f) : new Color(hc.r, hc.g, hc.b, 0.45f), false, Theme.RoundSmall);
                    var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(1, 0); fr.offsetMin = fr.offsetMax = Vector2.zero;
                    var kt = HK.T(bimg.transform, keys[slot], 12, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                    Ui.Fill(kt.rectTransform);
                    var b = bimg.gameObject.AddComponent<Button>();
                    b.targetGraphic = bimg;
                    var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                    int sl = slot;
                    b.onClick.AddListener(() => a.heroAbility(hid, sl));
                    HK.Tip(bimg, $"{ab.name} ({keys[slot]}): {ab.description}");
                    card.abil.Add(b); card.abilFill.Add(fill); card.abilOutline.Add(ol);
                }
                if (selected)
                {
                    var modes = HK.HRow(body.transform, 3, TextAnchor.MiddleLeft, true);
                    foreach (var m in new[] { "hold", "advance", "retreat" })
                    {
                        string mm = m;
                        var mb = HK.B(modes.transform, "", () => a.heroMode(hid, mm), hero.mode == m ? Ui.BtnKind.On : Ui.BtnKind.Normal, 12, 24, m);
                        Ui.Size(mb, -1, 24, 1);
                        HK.Tip(mb, MODE_LABEL[m]);
                    }
                }
            }
            else
            {
                var r = HK.HRow(body.transform, 4);
                HK.T(r.transform, hero.state == "down" ? "Gefallen" : "Erholt sich", 12, Theme.Muted);
                card.cdText = HK.T(r.transform, "", 12, Theme.Muted);
                card.cd = new HudBar(body.transform, Theme.Hex(0x9080b8), 4, 1);
            }
        }

        // ------------------------------------------------------------ Minikarte

        Texture2D miniTex;
        Color32[] miniBase, miniBuf;
        int miniW, miniH, miniCell;
        double miniRev = double.NaN;
        double miniTimer;

        static Color32 C32(double rgb) { int c = (int)rgb; return new Color32((byte)((c >> 16) & 255), (byte)((c >> 8) & 255), (byte)(c & 255), 255); }

        void updateMinimap(double dt)
        {
            HK.Show(mini, showMinimap);
            if (!showMinimap) return;
            miniTimer -= dt;
            if (miniTimer > 0) return;
            miniTimer = 0.15;
            var s = sim.s;
            var m = s.map;
            int mw = (int)m.width, mh = (int)m.height;
            int cell = Math.Max(3, (int)Math.Floor(168 / Math.Max(m.width, m.height * 1.2)));
            int w = mw * cell;
            int hh = mh * cell;
            double rev = m.rev ?? 0;
            if (miniBase == null || miniW != w || miniH != hh || rev != miniRev)
            {
                miniW = w; miniH = hh; miniCell = cell; miniRev = rev;
                miniBase = new Color32[w * hh];
                miniBuf = new Color32[w * hh];
                var pal = Biomes.BIOMES[m.biome].palette;
                for (int y = 0; y < mh; y++)
                    for (int x = 0; x < mw; x++)
                    {
                        double cc = m.cells[y * mw + x];
                        double col = cc == Cell.Path || cc == Cell.Spawn || cc == Cell.Goal ? pal.path : cc == Cell.Blocked ? pal.obstacle : m.heights[y * mw + x] >= 2 ? pal.high : pal.ground;
                        FillRect(miniBase, x * cell, y * cell, cell, cell, C32(col));
                    }
                var spawn = new Color32(0xc0, 0x60, 0xff, 255);
                foreach (var sp in WorldGenerator.spawnsOf(m)) FillRect(miniBase, (int)sp.x * cell - 1, (int)sp.y * cell - 1, cell + 2, cell + 2, spawn);
                FillRect(miniBase, (int)m.goal.x * cell - 1, (int)m.goal.y * cell - 1, cell + 2, cell + 2, new Color32(0x60, 0xf0, 0xff, 255));
                if (miniTex != null) UnityEngine.Object.Destroy(miniTex);
                miniTex = new Texture2D(w, hh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                mini.texture = miniTex;
                mini.rectTransform.sizeDelta = new Vector2(w, hh);
            }
            Array.Copy(miniBase, miniBuf, miniBase.Length);
            foreach (var t in s.towers) FillRect(miniBuf, (int)t.cx * cell + 1, (int)t.cy * cell + 1, cell - 2, cell - 2, C32(Towers.TOWERS[t.def].color));
            var cBoss = new Color32(0xff, 0x30, 0x40, 255);
            var cElite = new Color32(0xff, 0xd8, 0x4a, 255);
            var cFoe = new Color32(0xff, 0x6a, 0x6a, 255);
            foreach (var e in s.enemies)
            {
                if (!e.alive || e.stealthed) continue;
                bool boss = !string.IsNullOrEmpty(e.boss);
                Circle(miniBuf, (float)(e.x * cell), (float)(e.y * cell), boss ? cell * 0.9f : cell * 0.35f, boss ? cBoss : e.elite ? cElite : cFoe, false);
            }
            foreach (var hero in s.heroes)
            {
                if (hero.state != "active") continue;
                Circle(miniBuf, (float)(hero.x * cell), (float)(hero.y * cell), cell * 0.6f + 1, new Color32(0, 0, 0, 255), false);
                Circle(miniBuf, (float)(hero.x * cell), (float)(hero.y * cell), cell * 0.6f, C32(Heroes.HEROES[hero.id].accent), false);
            }
            if (camQuad != null && camQuad.Count == 4)
            {
                var lc = new Color32(255, 255, 255, 217);
                for (int i = 0; i < 4; i++)
                {
                    var p0 = camQuad[i];
                    var p1 = camQuad[(i + 1) % 4];
                    Line(miniBuf, (float)(p0[0] * cell), (float)(p0[1] * cell), (float)(p1[0] * cell), (float)(p1[1] * cell), lc);
                }
            }
            miniTex.SetPixels32(miniBuf);
            miniTex.Apply(false);
        }

        // Zeichnen mit Ursprung oben links (wie Canvas 2D); die Textur hat ihren Ursprung unten links.
        void FillRect(Color32[] buf, int x0, int y0, int rw, int rh, Color32 c)
        {
            int x1 = Math.Min(miniW, x0 + rw), y1 = Math.Min(miniH, y0 + rh);
            for (int y = Math.Max(0, y0); y < y1; y++)
            {
                int row = (miniH - 1 - y) * miniW;
                for (int x = Math.Max(0, x0); x < x1; x++) buf[row + x] = c;
            }
        }

        void Circle(Color32[] buf, float cx, float cy, float r, Color32 c, bool ring)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r), y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            float r2 = r * r;
            for (int y = Math.Max(0, y0); y < Math.Min(miniH, y1); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(miniW, x1); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d2 = dx * dx + dy * dy;
                    if (d2 <= r2 && (!ring || d2 >= (r - 1.5f) * (r - 1.5f))) buf[(miniH - 1 - y) * miniW + x] = c;
                }
        }

        void Line(Color32[] buf, float x0, float y0, float x1, float y1, Color32 c)
        {
            float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int n = Mathf.Max(1, Mathf.CeilToInt(len));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                if (x < 0 || y < 0 || x >= miniW || y >= miniH) continue;
                buf[(miniH - 1 - y) * miniW + x] = c;
            }
        }

        void miniClick(PointerEventData e)
        {
            var rt = mini.rectTransform;
            Vector2 lp;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out lp)) return;
            var r = rt.rect;
            var m = sim.s.map;
            double x = ((lp.x - r.xMin) / r.width) * m.width;
            double z = ((r.yMax - lp.y) / r.height) * m.height;
            a.lookAt(x, z);
        }

        // ------------------------------------------------------------ Öffentliche Helfer

        public void disarmSell()
        {
            if (sellArmed != 0)
            {
                sellArmed = 0;
                panelSig = "";
            }
        }

        public void forcePanelRefresh()
        {
            panelSig = "";
        }

        public bool hasModal()
        {
            return modalKind != "";
        }

        /// <summary>
        /// Ersatz für <c>hud.root.querySelector('[data-testid="sell"]').click()</c> (Entf/Rück in GameSession):
        /// löst den Verkaufen-Knopf des Turmpanels aus, falls sichtbar und bedienbar.
        /// </summary>
        public void clickSell()
        {
            if (sellBtn != null && sellBtn.gameObject.activeInHierarchy && sellBtn.interactable) sellBtn.onClick.Invoke();
        }
    }
}
