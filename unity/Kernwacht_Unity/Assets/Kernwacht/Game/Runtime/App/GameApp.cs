using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kernwacht.Game.UI;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Kernwacht.Game
{
    // Übersetzung von app/GameApplication.ts (+ main.ts).
    // GameApplication: Start, Szenenwechsel, Lebenszyklus. Besitzt Darstellung, Audio und Speicher.
    // Menüs zeigen im Hintergrund eine generierte Welt (ein Bot spielt eine echte Partie, ohne Speichern).
    //
    // PORT: Einziger Einstiegspunkt der Unity-Szene (GameObject „Kernwacht“). Baut Kamera, Licht, Canvas mit den
    // UI-Ebenen und das EventSystem selbst auf; die Szene muss nichts weiter enthalten.

    /// <summary>TS: ScreenId = 'menu' | 'heroSetup' | 'settings' | 'records' | 'help' | 'editor' | 'results' | 'game'.</summary>
    public class ScreenParams
    {
        public ResultParams results;
        public string helpTab;
        /// <summary>3.10: Karte aus dem Editor, die im Helden-Endlos gespielt werden soll.</summary>
        public MapDefinition heroMap;
        public string back;
    }

    /// <summary>TS: GameApplication.AUTO_TIERS-Eintrag bzw. Argument von renderer.setQuality.</summary>
    public class QualityTier
    {
        public int msaa;
        public int shadowSize;
        public bool bloom;
        public bool dof;
        public QualityTier(int msaa, int shadowSize, bool bloom) { this.msaa = msaa; this.shadowSize = shadowSize; this.bloom = bloom; }
    }

    public class GameApp : MonoBehaviour
    {
        public static GameApp I { get; private set; }

        // ------------------------------------------------------------ Felder (verbindliche Schnittstelle)
        public SaveData save;
        public WorldView view;
        // PORT: 'new' verdeckt die veraltete Component.audio-Eigenschaft von Unity.
        public new AudioSystem audio;
        public GameSession session;
        public SaveSystem saveSystem;
        /// <summary>TS-Name des Speichersystems.</summary>
        public SaveSystem saves => saveSystem;
        public string loadError;
        public string screen = "menu";

        public Canvas canvas;
        public CanvasScaler scaler;
        public RectTransform root;
        public RectTransform worldLayer, hudLayer, screenLayer, overlayLayer, toastLayer, tooltipLayer;
        public Camera mainCamera;
        public Light sun;

        /// <summary>Aktuelle Qualitätsstufe (für die Welt: Glühen/Tiefenunschärfe). PORT: Renderer.setQuality-Ersatz.</summary>
        public QualityTier quality = new QualityTier(4, 2048, true);
        /// <summary>„Bewegung reduzieren“ (TS: CSS-Klasse reduced-motion am Dokument).</summary>
        public bool reducedMotion => save != null && save.settings.reducedMotion;

        GameSimulation bgSim;
        Bot bgBot;
        double bgTimer;
        double bgSpin;
        RectTransform toastBox;
        int lastW, lastH;
        bool hiddenPause;
        bool overlayWasOpen;
        double lastFrameErrorAt = -1e9;
        RenderPipelineAsset pipelineClone;

        ViewUiState bgUi;

        // ------------------------------------------------------------ Start (TS: Konstruktor + boot())

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            try
            {
                Application.targetFrameRate = -1;
                setupCamera();
                setupCanvas();
                setupEventSystem();
                saveSystem = new SaveSystem(PlayerPrefsStorage.safeStorage());
                var res = saveSystem.load();
                save = res.data;
                loadError = res.error;
                view = new WorldView(mainCamera); // PORT: Konstruktor des Welt-Übersetzers (TS: new WorldView(renderer))
                audio = new AudioSystem();        // PORT: TS: new AudioSystem()
                bgUi = new ViewUiState
                {
                    buildDef = null, hoverCell = null, preview = null, selectedTower = 0, showRanges = false, showGrid = false,
                    reducedMotion = false, particleCap = 250, cinematic = true,
                };
                applySettings();
                resize();
            }
            catch (Exception err)
            {
                Debug.LogException(err);
                fatal(err.Message);
            }
        }

        void Start()
        {
            if (I != this) return;
            try
            {
                go("menu");
            }
            catch (Exception err)
            {
                Debug.LogException(err);
                fatal(err.Message);
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void setupCamera()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var go = new GameObject("Hauptkamera");
                go.tag = "MainCamera";
                mainCamera = go.AddComponent<Camera>();
            }
            if (mainCamera.GetComponent<AudioListener>() == null && FindAnyObjectByType<AudioListener>() == null) mainCamera.gameObject.AddComponent<AudioListener>();
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Theme.Bg;
            mainCamera.orthographic = true;
            mainCamera.rect = new Rect(0, 0, 1, 1);
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 200f;

            // Hauptlicht (Sonne) für die Welt
            sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
            if (sun == null)
            {
                var lg = new GameObject("Sonne");
                sun = lg.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.6f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.4f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.24f);

            // Eigene Kopie der URP-Pipeline, damit Qualitätsänderungen das Asset im Editor nicht verändern
            try
            {
                var rp = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (rp != null)
                {
                    pipelineClone = Instantiate(rp);
                    pipelineClone.name = rp.name + " (Laufzeit)";
                    QualitySettings.renderPipeline = pipelineClone;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Kernwacht] Render-Pipeline konnte nicht kopiert werden: " + e.Message);
            }
        }

        void setupCanvas()
        {
            var cgo = new GameObject("Oberflaeche", typeof(RectTransform));
            cgo.layer = 5;
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;

            worldLayer = layer("WorldLayer");
            hudLayer = layer("HudLayer");
            screenLayer = layer("ScreenLayer");
            overlayLayer = layer("OverlayLayer");
            toastLayer = layer("ToastLayer");
            tooltipLayer = layer("TooltipLayer");

            // Toast-Kasten (CSS .toasts: unten mittig, 90 px Abstand, max. 560 px breit, neueste unten)
            toastBox = Ui.Rt(Ui.Node("Toasts", toastLayer));
            toastBox.anchorMin = toastBox.anchorMax = new Vector2(0.5f, 0);
            toastBox.pivot = new Vector2(0.5f, 0);
            toastBox.sizeDelta = new Vector2(560, 600);
            toastBox.anchoredPosition = new Vector2(0, 90);
            var v = toastBox.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6;
            v.childAlignment = TextAnchor.LowerCenter;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
        }

        RectTransform layer(string name)
        {
            var go = Ui.Node(name, root);
            var r = Ui.Fill(Ui.Rt(go));
            return r;
        }

        void setupEventSystem()
        {
            var es = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                es = go.AddComponent<EventSystem>();
            }
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>TS main.ts fatal(): Startfehler verständlich anzeigen statt eines leeren Bildschirms.</summary>
        void fatal(string msg)
        {
            try
            {
                if (root == null) setupCanvas();
                var bg = Ui.Panel(root, "Fatal", Theme.Bg, false);
                Ui.Fill(bg.rectTransform);
                var col = Ui.Column(bg.transform, 16, new RectOffset(80, 80, 80, 80));
                Ui.Fill(Ui.Rt(col.gameObject));
                col.childAlignment = TextAnchor.MiddleCenter;
                Ui.Label(col.transform, "KERNWACHT konnte nicht starten", 40, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                Ui.Label(col.transform, msg ?? "", 20, Theme.Text, TextAnchor.MiddleCenter);
                Ui.Label(col.transform, "Benötigt wird ein aktueller Browser mit WebGL2 (Chrome, Edge, Firefox, Safari ab 15).", 16, Theme.Muted, TextAnchor.MiddleCenter);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        // ------------------------------------------------------------ Speicher & Einstellungen

        public void persist()
        {
            if (!saveSystem.save(save))
            {
                // Kein stilles Scheitern: einmalig sichtbar machen.
                if (loadError == null) toast("Speichern nicht möglich – lokaler Speicher blockiert.", "warn");
            }
        }

        public void applySettings()
        {
            var s = save.settings;
            Keys.setKeyBindings(s.keys);
            audio.setSettings(new AudioSettings { master = s.masterVolume, music = s.musicVolume, sfx = s.sfxVolume, muted = s.muted });
            applyQuality();
            var skin = Achievements.SKINS.FirstOrDefault((k) => k.id == s.skin) ?? Achievements.SKINS[0];
            view.setSkin(new ViewSkin { tint = skin.tint, tintAmount = skin.tintAmount, stone = skin.stone });
            // PORT: TS setzt zusätzlich icons.stoneColor = skin.stone (IconFactory). Icons ist statisch – falls Icons
            // eine Sockelfarbe kennt, hier setzen (Bildschirm-Übersetzer).
            applyUiScale();
            // reducedMotion: TS-CSS-Klasse am Dokument → Eigenschaft GameApp.reducedMotion (liest die Einstellung)
            view.cam.lockRotation = s.lockRotation;
        }

        /// <summary>UI-Größe: Bezugsauflösung 1920×1080 geteilt durch settings.uiScale (= Skalierungsfaktor × uiScale).</summary>
        void applyUiScale()
        {
            if (scaler == null) return;
            double us = save != null ? save.settings.uiScale : 1;
            float k = (float)Math.Max(0.5, Math.Min(2.0, us > 0 ? us : 1));
            scaler.referenceResolution = new Vector2(1920f / k, 1080f / k);
        }

        public double pixelRatio()
        {
            var q = save.settings.quality;
            // PORT: Unity rendert in Canvas-Pixeln; relativ dazu wird die URP-Renderskala gesetzt.
            if (q == "low") return 0.75;
            if (q == "medium") return 1;
            if (q == "high") return 1;
            return 1 * (session?.adaptiveScale ?? 1);
        }

        /// <summary>Automatische Qualitätsstufe (0 = volle Qualität); steigt bei dauerhaft niedriger Bildrate.</summary>
        public int autoTier = 0;
        public static readonly QualityTier[] AUTO_TIERS =
        {
            new QualityTier(4, 2048, true),
            new QualityTier(2, 1024, true),
            // 4.1: Kantenglättung bleibt an (ohne wirkt das Bild treppig/pixelig); stattdessen erst Glühen aus
            new QualityTier(2, 1024, false),
            // Schatten nie zur Laufzeit abschalten: Unter WebGL fehlen dann die passenden Shadervarianten,
            // das Gelände und alle Modelle verschwinden (Fehler „texture format and sampler type“). Kleinste Stufe: 512.
            new QualityTier(2, 512, false),
            new QualityTier(0, 512, false),
        };

        public void applyQuality()
        {
            var q = save.settings.quality;
            bool cine = save.settings.cinematicFx != false;
            QualityTier b = q == "low" ? new QualityTier(0, 512, false)
                : q == "medium" ? new QualityTier(2, 1024, true)
                    : q == "high" ? new QualityTier(4, 2048, true)
                        : AUTO_TIERS[Math.Min(autoTier, AUTO_TIERS.Length - 1)];
            // 3.13: Tiefenunschärfe nur, wo auch Glühen läuft (nicht auf „Niedrig“ bzw. der untersten Automatikstufe)
            quality = new QualityTier(b.msaa, b.shadowSize, b.bloom) { dof = cine && b.bloom };
            setQuality(quality);
        }

        /// <summary>
        /// Ersatz für Renderer.setQuality: Kantenglättung (MSAA), Schatten (Auflösung/aus) der URP-Pipeline und der Sonne.
        /// PORT: Glühen/Tiefenunschärfe liest die Welt aus <see cref="quality"/>. URP-Eigenschaften werden per Reflexion
        /// gesetzt, weil Kernwacht.Game die URP-Assembly nicht referenziert.
        /// </summary>
        void setQuality(QualityTier t)
        {
            if (mainCamera != null) mainCamera.allowMSAA = t.msaa > 0;
            // Nachbearbeitung (Glühen, Tonemapping, Farbkorrektur) ab „Mittel“
            PostFx.SetQuality(mainCamera, t.bloom);
            QualitySettings.antiAliasing = t.msaa;
            if (sun != null) sun.shadows = t.shadowSize > 0 ? LightShadows.Soft : LightShadows.None;
            QualitySettings.shadows = t.shadowSize > 0 ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.shadowResolution = t.shadowSize >= 2048 ? ShadowResolution.High : ShadowResolution.Medium;
            var rp = pipelineClone != null ? pipelineClone : QualitySettings.renderPipeline;
            if (rp != null)
            {
                setProp(rp, "msaaSampleCount", Math.Max(1, t.msaa));
                if (t.shadowSize > 0) setProp(rp, "mainLightShadowmapResolution", t.shadowSize);
                setProp(rp, "supportsMainLightShadows", t.shadowSize > 0);
                setProp(rp, "shadowDistance", t.shadowSize >= 2048 ? 80f : t.shadowSize >= 1024 ? 55f : 40f);
            }
        }

        static void setProp(object o, string name, object value)
        {
            try
            {
                var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p == null || !p.CanWrite) return;
                var target = p.PropertyType;
                object v = value;
                if (target.IsEnum) v = Enum.ToObject(target, Convert.ToInt32(value));
                else if (target != value.GetType()) v = Convert.ChangeType(value, target);
                p.SetValue(o, v);
            }
            catch
            {
                /* Eigenschaft in dieser URP-Version nicht vorhanden */
            }
        }

        /// <summary>Senkt die automatische Qualität um eine Stufe. Liefert false, wenn bereits die niedrigste Stufe aktiv ist.</summary>
        /// <summary>4.1: Qualität wieder anheben, wenn es dauerhaft flüssig läuft.</summary>
        public bool raiseAutoQuality()
        {
            if (autoTier <= 0) return false;
            autoTier--;
            applyQuality();
            return true;
        }

        public bool lowerAutoQuality()
        {
            if (autoTier >= AUTO_TIERS.Length - 1) return false;
            autoTier++;
            applyQuality();
            return true;
        }

        /// <summary>Detailgrad der Geländeausstattung (Gras, Randsteine).</summary>
        public double detailLevel()
        {
            var q = save.settings.quality;
            return q == "low" ? 0 : q == "medium" ? 1 : 2;
        }

        public double particleCap()
        {
            var q = save.settings.quality;
            if (q == "low") return 150;
            if (q == "medium") return 450;
            if (q == "high") return 1100;
            return session != null && session.adaptiveScale < 0.9 ? 300 : 700;
        }

        public void resize()
        {
            float w = Mathf.Max(1, Screen.width);
            float hh = Mathf.Max(1, Screen.height);
            lastW = Screen.width;
            lastH = Screen.height;
            var rp = pipelineClone != null ? pipelineClone : QualitySettings.renderPipeline;
            if (rp != null) setProp(rp, "renderScale", (float)Math.Max(0.1, Math.Min(2.0, pixelRatio())));
            if (view != null) view.cam.Resize(w, hh);
            applyUiScale();
        }

        // ------------------------------------------------------------ Toasts

        public void toast(string text, string severity = "info", double ms = 3200)
        {
            if (toastBox == null) return;
            string prefix = severity == "danger" ? "<b>!!</b> " : severity == "warn" ? "<b>!</b> " : "";
            var img = Ui.Panel(toastBox, "Toast", new Color(20 / 255f, 16 / 255f, 34 / 255f, 0.95f), true, Theme.RoundSmall);
            var o = img.GetComponent<Outline>();
            if (o != null) o.effectColor = severity == "warn" ? Theme.Warn : severity == "danger" ? Theme.Bad : Theme.LineStrong;
            img.raycastTarget = false;
            var hl = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(16, 16, 7, 7);
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            var lbl = Ui.Label(img.transform, prefix + escapeRich(plainText(text)), 16,
                severity == "danger" ? Theme.Hex(0xffd6d6) : Theme.Text, TextAnchor.MiddleCenter, severity == "danger" ? FontStyle.Bold : FontStyle.Normal);
            var le = lbl.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Mathf.Min(lbl.preferredWidth + 2, Mathf.Min(560, Screen.width * 0.92f) - 32);
            var cg = img.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            var fx = img.gameObject.AddComponent<ToastFx>();
            fx.outAt = Time.unscaledTime + (float)ms / 1000f;
            while (toastBox.childCount > 4)
            {
                var first = toastBox.GetChild(0);
                first.SetParent(null, false);
                Destroy(first.gameObject);
            }
        }

        void positionToasts()
        {
            if (toastBox != null) toastBox.anchoredPosition = new Vector2(0, screen == "game" ? 84 : 90);
        }

        static string escapeRich(string s) => s ?? "";

        /// <summary>
        /// Entfernt Emojis, die die Laufzeitschrift nicht darstellen kann (Symbole wie ⚡ 🔥 ⛏ ⚓ in Meldungen).
        /// PORT: Die Schrift enthält keine Emojis (PORTING_UI.md); Text ohne Symbol bleibt verständlich.
        /// </summary>
        public static string plainText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsHighSurrogate(c)) { if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++; continue; }
                if (char.IsLowSurrogate(c)) continue;
                if (c == '️' || c == '︎' || c == '‍') continue;
                if (c >= '⏩' && c <= '⏺') continue;
                if ("⚠⚡⛏⚓⚔⛑⚙⛨⌛⏳⚗⚒⛓⚑⚐☠⚰".IndexOf(c) >= 0) continue;
                sb.Append(c);
            }
            var r = sb.ToString();
            while (r.Contains("  ")) r = r.Replace("  ", " ");
            return r.Trim();
        }

        // ------------------------------------------------------------ Szenen

        public void go(string screen, ScreenParams p = null)
        {
            if (p == null) p = new ScreenParams();
            if (screen != "game" && session != null)
            {
                session.dispose();
                session = null;
            }
            this.screen = screen;
            Ui.Clear(screenLayer);
            Ui.Clear(hudLayer);
            positionToasts();
            if (screen != "game")
            {
                // 4.1: Menüs zeigen das Fantasy-Thema; die Ergebnisse behalten das Thema des Laufs.
                if (Themes.SciFi && screen != "results") { Themes.Apply(Themes.FANTASY); bgSim = null; }
                ensureBackground();
                audio.intensity = 0;
            }
            object el;
            switch (screen)
            {
                // PORT: Bildschirm-Funktionen des Bildschirm-Übersetzers (ui/screens/*.ts → statische Klassen); Rückgabe = Wurzelelement.
                case "menu": el = MenuScreen.renderMenu(this); break;
                case "heroSetup": el = HeroSetupScreen.renderHeroSetup(this, p.heroMap); break;
                case "editor": el = renderEditorPlaceholder(); break;
                case "settings": el = SettingsScreen.renderSettings(this, p.back ?? "menu"); break;
                case "records": el = RecordsScreen.renderRecords(this); break;
                case "help": el = HelpScreen.renderHelp(this, p.helpTab ?? "controls", p.back ?? "menu"); break;
                case "results": el = ResultsScreen.renderResults(this, p.results); break;
                default: el = null; break;
            }
            attach(el, screenLayer);
            // TS: requestAnimationFrame(() => resize(); Fokus auf [data-autofocus] bzw. ersten Knopf)
            pendingResize = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        bool pendingResize;

        static Transform tr(object o)
        {
            if (o is Component c) return c != null ? c.transform : null;
            if (o is GameObject g) return g != null ? g.transform : null;
            return null;
        }

        static void attach(object el, RectTransform parent)
        {
            var t = tr(el);
            if (t == null || parent == null) return;
            if (t.parent != parent) t.SetParent(parent, false);
            t.SetAsLastSibling();
        }

        /// <summary>
        /// PORT: Der Karteneditor (editor/MapEditor.ts) ist keinem Übersetzer zugeteilt. Bis zur Übertragung ein Hinweis
        /// mit Zurück-Knopf statt eines leeren Bildschirms.
        /// </summary>
        RectTransform renderEditorPlaceholder()
        {
            var bg = Ui.Panel(screenLayer, "Editor", new Color(Theme.Bg.r, Theme.Bg.g, Theme.Bg.b, 0.82f), false);
            Ui.Fill(bg.rectTransform);
            var box = Ui.Panel(bg.transform, "Box", Theme.Panel, true);
            Ui.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 260));
            var col = Ui.Column(box.transform, 14, new RectOffset(28, 28, 24, 24));
            Ui.Fill(Ui.Rt(col.gameObject));
            Ui.Label(col.transform, "Karteneditor", 32, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Label(col.transform, "Der Karteneditor ist in dieser Fassung noch nicht verfügbar.", 18, Theme.Muted);
            Ui.Button(col.transform, "Zurück", () => { audio.play("ui"); go("menu"); }, Ui.BtnKind.Primary);
            return bg.rectTransform;
        }

        /// <summary>Hintergrundwelt der Menüs (rein kosmetisch).</summary>
        void ensureBackground()
        {
            if (bgSim != null && !bgSim.isOver())
            {
                view.setMap(bgSim.s.map, detailLevel());
                return;
            }
            // Lebendige Menüwelt: ein Bot spielt im Hintergrund eine echte Partie (ohne Eingaben, ohne Speichern).
            var rnd = new System.Random();
            double entropy = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() ^ rnd.Next(0, 1000000000)) & 0xffffff;
            var seed = RngUtil.randomSeedText(entropy);
            bgSim = GameSimulation.create(new RunSetup { mode = "quick", seedText = seed, variant = "classic", biome = "random", difficulty = "easy", heroes = new List<string> { "blade", "bard" } });
            bgSim.s.gold += 600;
            // PORT: TS (Date.now() >> 4) % n kann durch 32-Bit-Überlauf negativ werden – hier stets gültiger Index.
            int pi = (int)((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >> 4) % Bot.BOT_PROFILES.Count);
            bgBot = new Bot(bgSim, Bot.BOT_PROFILES[pi]);
            bgTimer = 0;
            view.setMap(bgSim.s.map, detailLevel());
            var b = Biomes.BIOMES[bgSim.s.map.biome];
            audio.setMusicTheme(b.musicRoot, b.scale);
        }

        // ------------------------------------------------------------ Hauptschleife (TS: loop())

        void Update()
        {
            if (I != this || save == null) return;
            // Audio nach einer Nutzeraktion entsperren (Autoplay-Richtlinien des Browsers)
            if (anyUserPress())
            {
                try
                {
                    audio.unlock();
                    audio.startMusic();
                }
                catch (Exception e) { Debug.LogException(e); }
            }
            if (Screen.width != lastW || Screen.height != lastH || pendingResize)
            {
                pendingResize = false;
                resize();
            }
            double dt = Math.Min(0.1, Math.Max(0, Time.unscaledDeltaTime));
            if (hiddenPause)
            {
                // Nach Rückkehr aus dem Hintergrund keine Zeit nachholen
                hiddenPause = false;
                dt = 0;
            }
            try
            {
                if (screen == "game" && session != null) session.frame(dt);
                else backgroundFrame(dt);
            }
            catch (Exception err)
            {
                Debug.LogException(err);
                // PORT: im TS je Bild; hier höchstens alle 2 s, damit der Toast-Kasten nicht überläuft
                if (Time.unscaledTime - lastFrameErrorAt > 2)
                {
                    lastFrameErrorAt = Time.unscaledTime;
                    toast($"Darstellungsfehler: {err.Message}", "danger", 6000);
                }
            }
        }

        void LateUpdate()
        {
            overlayWasOpen = overlayOpenNow();
        }

        static bool anyUserPress()
        {
            var m = Mouse.current;
            if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame || m.middleButton.wasPressedThisFrame)) return true;
            var k = Keyboard.current;
            if (k != null && k.anyKey.wasPressedThisFrame) return true;
            var t = Touchscreen.current;
            if (t != null && t.primaryTouch.press.wasPressedThisFrame) return true;
            return false;
        }

        bool overlayOpenNow()
        {
            if (overlayLayer == null) return false;
            for (int i = 0; i < overlayLayer.childCount; i++) if (overlayLayer.GetChild(i).gameObject.activeSelf) return true;
            return false;
        }

        /// <summary>Hilfe/Einstellungen-Overlay offen (oder in diesem Bild geschlossen) – Escape gehört dann dem Overlay.</summary>
        public bool overlayOpen() => overlayOpenNow() || overlayWasOpen;

        void backgroundFrame(double dt)
        {
            var sim = bgSim;
            if (sim == null) return;
            bool reduced = save.settings.reducedMotion;
            // Bot spielt mit; nach Sieg/Niederlage startet eine neue Welt
            if (bgBot != null && !reduced)
            {
                bgTimer -= dt;
                if (bgTimer <= 0)
                {
                    bgTimer = 0.5;
                    try
                    {
                        bgBot.act();
                    }
                    catch
                    {
                        bgBot = null;
                    }
                }
                sim.advance(dt);
                var events = sim.drainEvents();
                if (events.Count > 0) view.handleEvents(events, sim, bgUi);
                if (sim.isOver())
                {
                    bgSpin += dt;
                    if (bgSpin > 4)
                    {
                        bgSpin = 0;
                        bgSim = null;
                        ensureBackground();
                        return;
                    }
                }
            }
            bgUi.reducedMotion = reduced;
            bgUi.cinematic = !reduced;
            view.frame(sim, bgUi, dt);
        }

        // ------------------------------------------------------------ Hintergrund (TS: visibilitychange)

        // PORT: Das Webspiel rechnet bei verborgenem Tab per Zeitgeber weiter (hiddenTick). Ein Unity-WebGL-Build
        // läuft im verborgenen Tab nicht weiter; daher wird die laufende Kampfphase angehalten (session.autoPause)
        // und nach der Rückkehr keine Zeit nachgeholt.
        void OnApplicationPause(bool paused)
        {
            if (paused) enterHidden();
        }

        void OnApplicationFocus(bool focus)
        {
            // Fokusverlust pausiert nur, wenn Unity im Hintergrund ohnehin anhält (2.9.5: kein Auto-Pause bei Fokusverlust).
            if (!focus && !Application.runInBackground) enterHidden();
        }

        void enterHidden()
        {
            hiddenPause = true;
            try
            {
                if (screen == "game" && session != null) session.autoPause();
            }
            catch (Exception err)
            {
                Debug.LogError("[Hintergrund] " + err);
            }
        }

        // ------------------------------------------------------------ Partien

        /// <summary>Baut die Startdaten eines Helden-Endloslaufs aus dem gespeicherten Fortschritt (neue runId).</summary>
        public HeroEndlessSetup heroEndlessSetup(string champion, List<string> startKit, string doctrine = null)
        {
            var m = save.progress.heroMeta;
            Dictionary<string, double> tree = null;
            if (m.champions != null && m.champions.TryGetValue(champion, out var cp) && cp != null && cp.tree != null) tree = cp.tree;
            return new HeroEndlessSetup
            {
                champion = champion,
                doctrine = doctrine,
                startKit = new List<string>(startKit ?? new List<string>()),
                meta = new HeroEndlessSetupMeta
                {
                    bestWave = m.bestWave,
                    unlockedItems = new List<string>(m.unlockedItems),
                    fragments = new Dictionary<string, double>(m.fragments),
                    passive = new Dictionary<string, double>(m.passive),
                    campaignDone = new List<string>(save.progress.campaignCompleted),
                    // Gemeinsame Heldenstufe für alle Helden, Skillbaum je Held
                    champ = new ChampionProgress { level = m.heroLevel.level, xp = m.heroLevel.xp, tree = tree != null ? new Dictionary<string, double>(tree) : new Dictionary<string, double>() },
                    paragon = new ParagonProgress { level = m.paragon.level, xp = m.paragon.xp, points = new Dictionary<string, double>(m.paragon.points) },
                    mastery = new Dictionary<string, double>(m.towerMastery),
                },
                runId = $"he-{toBase36(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())}-{toBase36((long)Math.Floor(UnityEngine.Random.value * 1e9))}",
            };
        }

        static string toBase36(long v)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (v == 0) return "0";
            bool neg = v < 0;
            ulong u = neg ? (ulong)(-v) : (ulong)v;
            var sb = new StringBuilder();
            while (u > 0)
            {
                sb.Insert(0, digits[(int)(u % 36)]);
                u /= 36;
            }
            if (neg) sb.Insert(0, '-');
            return sb.ToString();
        }

        /// <summary>Ein noch offener (fortsetzbarer) Helden-Endloslauf wird verbucht, bevor er überschrieben wird.</summary>
        public void commitPendingHeroRun()
        {
            if (save.resume == null) return;
            var r = SaveSystem.deserializeRun(save.resume);
            if (r.state != null && r.state.mode == "heroEndless") Progress.commitHeroRun(save, r.state);
        }

        public void startRun(RunSetup setup)
        {
            GameSimulation sim;
            try
            {
                sim = GameSimulation.create(setup);
            }
            catch (Exception err)
            {
                Debug.LogException(err);
                toast($"Partie konnte nicht gestartet werden: {err.Message}", "danger", 6000);
                return;
            }
            save.settings.lastVariant = setup.variant;
            save.settings.lastDifficulty = setup.difficulty;
            save.settings.lastBiome = setup.biome;
            if (setup.heroEndless != null)
            {
                save.progress.heroMeta.lastChampion = setup.heroEndless.champion;
                if (setup.heroEndless.doctrine != null) save.progress.heroMeta.lastDoctrine = setup.heroEndless.doctrine;
                save.progress.heroMeta.lastTheme = setup.theme == Themes.SCIFI ? Themes.SCIFI : Themes.FANTASY;
                save.progress.heroMeta.startKit = new List<string>(setup.heroEndless.startKit);
            }
            commitPendingHeroRun();
            save.resume = null;
            persist();
            enterSession(sim);
        }

        public DeserializeRunResult canResume()
        {
            if (save.resume == null) return new DeserializeRunResult { state = null, error = null };
            var r = SaveSystem.deserializeRun(save.resume);
            // 3.9: nur noch Helden-Endlos – alte Spielstände anderer Modi werden nicht mehr angeboten
            if (r.state != null && r.state.mode != "heroEndless") return new DeserializeRunResult { state = null, error = null };
            return r;
        }

        public void resumeRun()
        {
            var r = canResume();
            if (r.state == null)
            {
                toast($"Spielstand kann nicht fortgesetzt werden: {r.error ?? "unbekannt"}. Der Eintrag bleibt erhalten, bis eine neue Partie startet.", "warn", 6000);
                return;
            }
            enterSession(new GameSimulation(r.state));
        }

        void enterSession(GameSimulation sim)
        {
            if (session != null)
            {
                session.dispose();
                session = null;
            }
            Themes.Apply(sim.s.theme);
            go("game");
            session = new GameSession(this, sim);
            var b = Biomes.BIOMES[sim.s.map.biome];
            audio.setMusicTheme(b.musicRoot, b.scale);
        }
    }

    /// <summary>Ein-/Ausblenden eines Toasts (CSS .toast.out: opacity 0 in 0,35 s, entfernt nach +0,4 s).</summary>
    public class ToastFx : MonoBehaviour
    {
        public float outAt;
        CanvasGroup cg;

        void Awake() { cg = GetComponent<CanvasGroup>(); }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now < outAt) return;
            float k = Mathf.Clamp01((now - outAt) / 0.35f);
            if (cg != null) cg.alpha = 1 - k;
            if (now >= outAt + 0.4f) Destroy(gameObject);
        }
    }
}
