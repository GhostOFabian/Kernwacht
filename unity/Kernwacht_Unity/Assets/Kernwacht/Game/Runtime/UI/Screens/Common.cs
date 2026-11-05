// Übersetzung von ui/screens/common.ts (shell, seg, copyText, heroLoadout) plus die uGUI-Bausteine, die die
// Bildschirme statt der DOM-Elemente (h1/h2/p, .card, .notice, <select>, <input>, <details>, Tabellen, Raster) nutzen.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class Common
    {
        // ------------------------------------------------------------ Schriftgrößen (CSS 15 px Grundschrift → 18 px Bezug)

        public const int FS = Theme.FontSize;   // 18
        public const int H1 = 29, H2 = 22, H3 = 19, SMALL = 15, TINY = 13;
        /// <summary>Breite von .screen-inner (CSS 1100 px × 1,2).</summary>
        public const float INNER_W = 1320;

        /// <summary>
        /// Gesperrte/abgeblendete Elemente: dunkle Abdeckung statt Durchsichtigkeit. Eine durchsichtige Kachel ließ
        /// die helle Karte durchscheinen (blasses Lila/Rosa mit weißer Schrift = schlecht lesbar).
        /// </summary>
        public static void dim(Component c, float amount)
        {
            var t = c.transform.Find("Abdunklung");
            Image ov;
            if (t == null)
            {
                if (amount <= 0) return;
                ov = Ui.Node("Abdunklung", c.transform).AddComponent<Image>();
                ov.sprite = Theme.Round; ov.type = Image.Type.Sliced; ov.raycastTarget = false;
                Ui.Fill(ov.rectTransform);
                Ui.Get<LayoutElement>(ov.gameObject).ignoreLayout = true;
            }
            else ov = t.GetComponent<Image>();
            ov.transform.SetAsLastSibling();
            ov.color = new Color(0.05f, 0.035f, 0.09f, Mathf.Clamp01(amount));
            ov.gameObject.SetActive(amount > 0);
        }

        public static readonly Color CardBg = new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.45f);
        public static readonly Color InnerBg = new Color(20 / 255f, 16 / 255f, 32 / 255f, 0.9f);
        public static readonly Color IconBg = new Color(0.13f, 0.1f, 0.21f, 1f);

        // ------------------------------------------------------------ common.ts

        /// <summary>
        /// Bildschirmrahmen (.screen › .screen-inner › .screen-head): Zurück-Knopf (Esc), Titel, Inhalte.
        /// Der Wurzelknoten wird unter app.screenLayer angelegt (bildschirmfüllend) und zurückgegeben.
        /// Jeder Inhalt ist ein Baustein, der in den inneren Bereich zeichnet (TS: ...content).
        /// </summary>
        public static RectTransform shell(GameApp app, string title, string back, params Action<Transform>[] content)
        {
            var root = screenRoot(app, "Screen_" + title);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = new Color(18 / 255f, 15 / 255f, 28 / 255f, 0.72f);
            var body = Ui.Scroll(root, out var sr, 0, new RectOffset(16, 16, 14, 26));
            Ui.Fill(sr.GetComponent<RectTransform>());
            var bodyCol = body.GetComponent<VerticalLayoutGroup>();
            bodyCol.childAlignment = TextAnchor.UpperCenter;
            bodyCol.childForceExpandWidth = false;
            var inner = Ui.Panel(body, "ScreenInner", InnerBg, true, Theme.Round);
            var innerCol = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            innerCol.padding = new RectOffset(26, 26, 16, 26);
            innerCol.spacing = 8;
            innerCol.childControlWidth = true; innerCol.childControlHeight = true;
            innerCol.childForceExpandWidth = true; innerCol.childForceExpandHeight = false;
            var le = Ui.Size(inner, INNER_W);
            le.minWidth = 600;
            le.flexibleWidth = 0;

            var head = Ui.Row(inner.transform, 12, TextAnchor.MiddleLeft, false);
            if (back != null)
            {
                var b = iconOnlyButton(head.transform, "back", () => { sfx(app, "ui"); app.go(back); });
                Ui.Tip(b, () => "Zurück (Esc)");
            }
            var t = Ui.Label(head.transform, TextUtil.sym(title), H1, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Size(t, -1, -1, 1);
            foreach (var c in content) c?.Invoke(inner.transform);
            if (back != null) root.gameObject.AddComponent<EscHandler>().onEsc = () => app.go(back);
            root.gameObject.AddComponent<TipView>();
            var ap = root.gameObject.AddComponent<Appear>();
            ap.target = inner.rectTransform;
            return root;
        }

        /// <summary>Segmentschalter (.seg): eine Auswahl aus mehreren kleinen Knöpfen.</summary>
        public static RectTransform seg(Transform parent, List<(string value, string text)> options, string value, Action<string> onChange, string label)
        {
            var row = Ui.Row(parent, 5, TextAnchor.MiddleLeft, false);
            row.gameObject.name = "Seg_" + label;
            var buttons = new List<Button>();
            string cur = value;
            foreach (var (v, text) in options)
            {
                string val = v;
                Button b = null;
                b = btn(row.transform, text, () =>
                {
                    cur = val;
                    for (int i = 0; i < buttons.Count; i++) Ui.SetKind(buttons[i], options[i].value == cur ? Ui.BtnKind.On : Ui.BtnKind.Normal);
                    onChange(val);
                }, val == value ? Ui.BtnKind.On : Ui.BtnKind.Normal, null, true);
                buttons.Add(b);
            }
            return row.GetComponent<RectTransform>();
        }

        /// <summary>Text in die Zwischenablage (mit Rückmeldung).</summary>
        public static void copyText(GameApp app, string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text;
#if UNITY_WEBGL && !UNITY_EDITOR
                // PORT: Im WebGL-Build erreicht systemCopyBuffer die Zwischenablage des Browsers nicht (ohne .jslib) –
                // wie im Webspiel bei fehlender Zwischenablage: Text gut lesbar anzeigen, damit er abgeschrieben werden kann.
                app.toast($"Kopieren nicht möglich – {text}", "warn", 8000);
#else
                app.toast($"Kopiert: {(text.Length > 40 ? text.Substring(0, 40) + "…" : text)}", "info", 1800);
#endif
            }
            catch
            {
                app.toast($"Kopieren nicht möglich – {text}", "warn", 8000);
            }
        }

        /// <summary>
        /// Heldenauswahl vor der Partie (bis zu zwei Helden). forced: von der Mission vorgegebene Helden.
        /// Ändert app.save.settings.lastHeroes und ruft onChange mit der aktuellen Auswahl auf.
        /// </summary>
        public static RectTransform heroLoadout(Transform parent, GameApp app, List<string> forced, Action<List<string>> onChange)
        {
            var done = app.save.progress.campaignCompleted;
            bool unlocked(string id) => Heroes.isHeroUnlocked(id, done);
            int size = (int)Heroes.HERO_RULES.loadoutSize;
            List<string> picked = forced != null ? new List<string>(forced) : app.save.settings.lastHeroes.Where(unlocked).Take(size).ToList();
            var root = Ui.Column(parent, 6).GetComponent<RectTransform>();
            root.gameObject.name = "Loadout";
            Action render = null;
            render = () =>
            {
                clear(root);
                gridRows(root, Heroes.HERO_ORDER.Count, 6, (row, i) =>
                {
                    string id = Heroes.HERO_ORDER[i];
                    var d = Heroes.HEROES[id];
                    bool ok = unlocked(id);
                    int idx = picked.IndexOf(id);
                    var mission = Campaign.CAMPAIGN.FirstOrDefault((m) => m.id == d.unlock);
                    var tile = Common.tile(row, () =>
                    {
                        if (forced != null || !ok) return;
                        if (idx >= 0) picked.RemoveAt(idx);
                        else
                        {
                            picked.Add(id);
                            if (picked.Count > size) picked = picked.Skip(picked.Count - size).ToList();
                        }
                        app.save.settings.lastHeroes = new List<string>(picked);
                        app.persist();
                        sfx(app, "select");
                        render();
                        onChange(new List<string>(picked));
                    }, idx >= 0 ? Theme.Gold : (Color?)null);
                    setDisabled(tile.btn, !ok || forced != null);
                    Ui.Tip(tile.btn, () => ok ? $"{d.name} – {d.description}" : $"Gesperrt: schließe Mission „{(mission != null ? mission.name : d.unlock)}“ ab");
                    if (idx >= 0) para(tile.col, idx == 0 ? "Z" : "X", "small", TextAnchor.UpperRight).color = Theme.Gold;
                    modelIcon(Ui.Row(tile.col, 0, TextAnchor.MiddleCenter, false).transform, id, 72);
                    para(tile.col, $"<b>{d.name.Split(' ').Last()}</b>", "", TextAnchor.UpperCenter);
                    para(tile.col, ok ? $"{d.role}{(d.support ? " · Unterstützer" : "")}" : $"Gesperrt · {(mission != null ? $"Mission {U.Fmt(mission.index + 1)}" : "gesperrt")}", "tiny muted", TextAnchor.UpperCenter);
                    return tile.rt;
                });
            };
            render();
            onChange(new List<string>(picked));
            return root;
        }

        // ------------------------------------------------------------ Grundbausteine

        /// <summary>Bildschirmwurzel unter app.screenLayer (bildschirmfüllend).</summary>
        public static RectTransform screenRoot(GameApp app, string name)
        {
            Transform layer = app != null && app.screenLayer != null ? (Transform)app.screenLayer : FallbackCanvas();
            var go = Ui.Node(name, layer);
            var rt = Ui.Rt(go);
            Ui.Fill(rt);
            return rt;
        }

        static Transform FallbackCanvas()
        {
            var c = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            return c != null ? c.transform : null;
        }

        public static void sfx(GameApp app, string id)
        {
            try { if (app != null && app.audio != null) app.audio.play(id); }
            catch (Exception e) { Debug.LogWarning($"[UI] Klang '{id}': {e.Message}"); }
        }

        /// <summary>Kinder sofort ausblenden und am Bildende zerstören (TS: replaceChildren).</summary>
        public static void clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                c.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(c);
            }
        }

        /// <summary>Optionales Feld/Property eines fremden Objekts lesen (für Mitglieder außerhalb der festen Schnittstelle).</summary>
        public static T member<T>(object o, string name)
        {
            if (o == null) return default;
            var t = o.GetType();
            const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var f = t.GetField(name, F);
            if (f != null && f.GetValue(o) is T fv) return fv;
            var p = t.GetProperty(name, F);
            if (p != null && p.CanRead && p.GetValue(o) is T pv) return pv;
            return default;
        }

        public static Text label(Transform p, string text, int size = FS, Color? color = null, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.UpperLeft)
        {
            var t = Ui.Label(p, TextUtil.sym(text ?? ""), size, color ?? Theme.Text, align, style);
            t.lineSpacing = 1.08f;
            return t;
        }

        /// <summary>Absatz mit CSS-Klassen: muted, small, tiny, good, bad, warn.</summary>
        public static Text para(Transform p, string text, string cls = "", TextAnchor align = TextAnchor.UpperLeft)
        {
            cls = cls ?? "";
            int size = cls.Contains("tiny") ? TINY : cls.Contains("small") ? SMALL : FS;
            Color col = cls.Contains("muted") ? Theme.Muted : cls.Contains("good") ? Theme.Good : cls.Contains("bad") ? Theme.Bad : cls.Contains("warn") ? Theme.Warn : Theme.Text;
            return label(p, text, size, col, FontStyle.Normal, align);
        }

        public static Text h1(Transform p, string text) => label(p, text, H1, Theme.Text, FontStyle.Bold);

        public static Text h2(Transform p, string text)
        {
            space(p, 8);
            return label(p, text, H2, Theme.Text, FontStyle.Bold);
        }

        public static Text h3(Transform p, string text) => label(p, text, H3, Theme.Text, FontStyle.Bold);

        public static void space(Transform p, float h)
        {
            var go = Ui.Node("Space", p);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
        }

        /// <summary>Aufzählung (ul/li) als Absätze mit Punkt.</summary>
        public static void bullets(Transform p, IEnumerable<string> items, string cls = "")
        {
            var col = Ui.Column(p, 3, new RectOffset(8, 0, 0, 0));
            foreach (var it in items) para(col.transform, "• " + it, cls);
        }

        public static RectTransform card(Transform p, string name = "Card")
        {
            var img = Ui.Panel(p, name, CardBg, true, Theme.Round);
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(16, 16, 10, 14);
            v.spacing = 6;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return img.rectTransform;
        }

        /// <summary>.notice (kind: "", "warn", "good").</summary>
        public static RectTransform notice(Transform p, string richText, string kind = "", string cls = "")
        {
            Color border = kind == "warn" ? Theme.Warn : kind == "good" ? Theme.Good : Theme.LineStrong;
            Color bg = kind == "warn" ? new Color(1f, 170 / 255f, 60 / 255f, 0.12f) : kind == "good" ? new Color(100 / 255f, 220 / 255f, 140 / 255f, 0.12f) : new Color(120 / 255f, 90 / 255f, 200 / 255f, 0.15f);
            var img = Ui.Panel(p, "Notice", bg, true, Theme.Round);
            img.GetComponent<Outline>().effectColor = border;
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(14, 14, 9, 9);
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            para(img.transform, richText, cls);
            return img.rectTransform;
        }

        public static Image image(Transform p, Sprite s, float size, Color? color = null)
        {
            var go = Ui.Node("Img", p);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = color ?? Color.white;
            Ui.Size(img, size, size);
            Ui.Rt(go).sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Porträt eines 3D-Modells auf dunklem, abgerundetem Grund (iconImg + IconFactory).</summary>
        public static Image modelIcon(Transform p, string modelId, float size)
        {
            var bg = Ui.Panel(p, "ModelIc", IconBg, false, Theme.RoundSmall);
            bg.raycastTarget = false;
            Ui.Size(bg, size, size);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var le = bg.GetComponent<LayoutElement>();
            le.flexibleWidth = 0;
            var img = image(bg.transform, Icons.Model(modelId), size);
            Ui.Fill(img.rectTransform);
            UnityEngine.Object.Destroy(img.GetComponent<LayoutElement>());
            return bg;
        }

        /// <summary>Symbol + Text in einer Zeile (z. B. Überschrift mit Porträt).</summary>
        public static HorizontalLayoutGroup iconRow(Transform p, Action<Transform> icon, string text, int size = FS, FontStyle style = FontStyle.Normal, Color? color = null)
        {
            var row = Ui.Row(p, 8, TextAnchor.MiddleLeft, false);
            icon?.Invoke(row.transform);
            var t = label(row.transform, text, size, color, style, TextAnchor.MiddleLeft);
            Ui.Size(t, -1, -1, 1);
            return row;
        }

        // ------------------------------------------------------------ Knöpfe

        /// <summary>Knopf mit natürlicher Breite (.btn / .btn.sm), optional mit SVG-Symbol davor.</summary>
        public static Button btn(Transform p, string text, Action onClick, Ui.BtnKind kind = Ui.BtnKind.Normal, string icon = null, bool sm = false, float width = -1)
        {
            int fs = sm ? SMALL : 17;
            var b = Ui.Button(p, TextUtil.sym(text ?? ""), onClick, kind, fs);
            var t = b.GetComponentInChildren<Text>();
            float h = sm ? 32 : Theme.Tap;
            float textW = t.preferredWidth;
            if (icon != null)
            {
                t.gameObject.SetActive(false);
                var row = Ui.Row(b.transform, 7, TextAnchor.MiddleCenter, false);
                Ui.Fill(row.GetComponent<RectTransform>(), 4);
                row.childControlWidth = true; row.childControlHeight = true; row.childForceExpandHeight = false;
                var ic = Icons.svgIcon(row.transform, icon, sm ? 17 : 21, Theme.Text);
                if (!string.IsNullOrEmpty(text))
                {
                    var t2 = Ui.Label(row.transform, TextUtil.sym(text), fs, Theme.Text, TextAnchor.MiddleLeft, kind == Ui.BtnKind.Primary ? FontStyle.Bold : FontStyle.Normal);
                    t2.horizontalOverflow = HorizontalWrapMode.Overflow;
                    textW = t2.preferredWidth + 7;
                }
                else textW = 0;
                textW += ic.rectTransform.sizeDelta.x;
            }
            else if (kind == Ui.BtnKind.Primary) t.fontStyle = FontStyle.Bold;
            var le = b.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            float w = width >= 0 ? width : textW + (sm ? 22 : 30);
            le.preferredWidth = w;
            le.minWidth = Mathf.Min(w, 60);
            return b;
        }

        public static Button iconOnlyButton(Transform p, string icon, Action onClick, float size = Theme.Tap)
        {
            var b = btn(p, "", onClick, Ui.BtnKind.Normal, icon, false, size);
            var le = b.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = size;
            le.minHeight = le.preferredHeight = size;
            return b;
        }

        /// <summary>.btn.big: Titelzeile (Symbol + Text) und Unterzeile.</summary>
        public static Button bigButton(Transform p, string icon, string title, string sub, Action onClick, Ui.BtnKind kind = Ui.BtnKind.Normal, Color? border = null)
        {
            var b = Ui.Button(p, "", onClick, kind, 19);
            var t = b.GetComponentInChildren<Text>();
            t.gameObject.SetActive(false);
            if (border.HasValue) b.GetComponent<Outline>().effectColor = border.Value;
            var le = b.GetComponent<LayoutElement>();
            le.preferredHeight = -1;
            le.minHeight = 56;
            var v = b.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 10, 10);
            v.spacing = 2;
            v.childAlignment = TextAnchor.MiddleLeft;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var row = Ui.Row(b.transform, 9, TextAnchor.MiddleLeft, false);
            if (icon != null) Icons.svgIcon(row.transform, icon, 22, Theme.Text);
            var tt = label(row.transform, title, 19, Theme.Text, kind == Ui.BtnKind.Primary ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft);
            Ui.Size(tt, -1, -1, 1);
            if (!string.IsNullOrEmpty(sub)) label(b.transform, sub, 14, kind == Ui.BtnKind.Primary ? Theme.Hex(0xeadcff) : Theme.Muted, FontStyle.Normal, TextAnchor.UpperLeft);
            return b;
        }

        public static void setDisabled(Button b, bool disabled)
        {
            b.interactable = !disabled;
            var cg = Ui.Get<CanvasGroup>(b.gameObject);
            cg.alpha = disabled ? 0.5f : 1f;
        }

        public struct Tile { public Button btn; public Transform col; public RectTransform rt; public Image img; }

        /// <summary>Anklickbare Kachel (.champ-pick, .skin, .hero-pick, .item): Panel + Knopf + senkrechte Anordnung.</summary>
        public static Tile tile(Transform p, Action onClick, Color? highlight = null, Color? bg = null, RectOffset pad = null, float spacing = 4)
        {
            // Deckende Kachel: Unitys Outline/Shadow kopieren die ganze Fläche – bei durchscheinendem Grund
            // färbte die Markierungsfarbe (z. B. Gold) die komplette Kachel ein und weiße Schrift war kaum lesbar.
            Color baseBg = bg ?? new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.5f);
            baseBg.a = 1f;
            if (highlight.HasValue) baseBg = Color.Lerp(new Color(30 / 255f, 22 / 255f, 48 / 255f, 1f), highlight.Value, 0.16f);
            else baseBg = Color.Lerp(new Color(18 / 255f, 14 / 255f, 28 / 255f, 1f), baseBg, 0.6f);
            baseBg.a = 1f;
            var img = Ui.Panel(p, "Tile", baseBg, true, Theme.Round);
            var o = img.GetComponent<Outline>();
            o.effectColor = highlight ?? Theme.LineStrong;
            if (highlight.HasValue)
            {
                o.effectDistance = new Vector2(3, -3);
                var glow = img.gameObject.AddComponent<Shadow>();
                glow.effectColor = new Color(highlight.Value.r, highlight.Value.g, highlight.Value.b, 0.3f);
                glow.effectDistance = new Vector2(0, -5);
            }
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.highlightedColor = new Color(1.2f, 1.15f, 1.3f, 1f);
            cb.pressedColor = new Color(0.85f, 0.8f, 0.9f, 1f);
            cb.disabledColor = Color.white;
            cb.colorMultiplier = 1.3f;
            b.colors = cb;
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = pad ?? new RectOffset(10, 10, 9, 10);
            v.spacing = spacing;
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return new Tile { btn = b, col = img.transform, rt = img.rectTransform, img = img };
        }

        // ------------------------------------------------------------ Formularelemente

        /// <summary>.field: Beschriftung (klein, gedämpft) über dem Element. Rückgabe = Behälter für das Element.</summary>
        public static Transform field(Transform p, string caption)
        {
            var col = Ui.Column(p, 4);
            col.gameObject.name = "Field";
            label(col.transform, caption, SMALL, Theme.Muted);
            return col.transform;
        }

        /// <summary>Ersatz für &lt;select&gt;: Knopf mit aktueller Auswahl, öffnet eine Liste über allem.</summary>
        public static Button select(Transform p, List<(string value, string text)> options, string value, Action<string> onChange, string ariaLabel = null)
        {
            string cur = options.Any((o) => o.value == value) ? value : (options.Count > 0 ? options[0].value : null);
            Button b = null;
            string LabelOf(string v) => options.FirstOrDefault((o) => o.value == v).text ?? "";
            b = Ui.Button(p, "", () => SelectPopup.Open(b, options, cur, (v) =>
            {
                cur = v;
                Ui.SetText(b, TextUtil.sym(LabelOf(v)) + "  ▼");
                onChange(v);
            }), Ui.BtnKind.Normal, 16);
            var t = b.GetComponentInChildren<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            t.rectTransform.offsetMin = new Vector2(12, 4);
            t.text = TextUtil.sym(LabelOf(cur)) + "  ▼";
            b.gameObject.name = "Select_" + (ariaLabel ?? "");
            return b;
        }

        /// <summary>Einzeiliges Textfeld (&lt;input type=text&gt;); onChange beim Verlassen/Enter (TS: onchange).</summary>
        public static InputField input(Transform p, string value, int maxLength, Action<InputField, string> onChange)
        {
            var bg = Ui.Panel(p, "Input", new Color(0, 0, 0, 0.35f), true, Theme.RoundSmall);
            Ui.Size(bg, -1, Theme.Tap);
            var tGo = Ui.Node("Text", bg.transform);
            var t = tGo.AddComponent<Text>();
            t.font = Theme.Font; t.fontSize = 17; t.color = Theme.Text; t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Fill(t.rectTransform, 0);
            t.rectTransform.offsetMin = new Vector2(12, 4); t.rectTransform.offsetMax = new Vector2(-12, -4);
            var f = bg.gameObject.AddComponent<InputField>();
            f.textComponent = t;
            f.targetGraphic = bg;
            f.characterLimit = maxLength;
            f.lineType = InputField.LineType.SingleLine;
            f.caretColor = Theme.Gold;
            f.selectionColor = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0.45f);
            f.text = value ?? "";
            f.onEndEdit.AddListener((v) => onChange(f, v));
            return f;
        }

        public static Slider slider(Transform p, double value01, Action<double> onChange)
        {
            bool ready = false;
            var s = Ui.Slider(p, (float)value01, (v) => { if (ready) onChange(v); });
            ready = true;
            return s;
        }

        // ------------------------------------------------------------ <details>, Tabellen, Raster, Balken

        /// <summary>Aufklappbarer Abschnitt (&lt;details class="codex-entry"&gt;). Inhalt wird beim ersten Öffnen gebaut.</summary>
        public static RectTransform details(Transform p, Action<Transform> summary, Action<Transform> body, bool open = false, Action<bool> onToggle = null)
        {
            var img = Ui.Panel(p, "Details", new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.4f), true, Theme.Round);
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(12, 12, 4, 6);
            v.spacing = 4;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var headGo = Ui.Node("Summary", img.transform);
            var headImg = headGo.AddComponent<Image>();
            headImg.color = new Color(1, 1, 1, 0.001f);
            var hb = headGo.AddComponent<Button>();
            var nav = hb.navigation; nav.mode = Navigation.Mode.None; hb.navigation = nav;
            hb.transition = Selectable.Transition.None;
            var head = headGo.AddComponent<HorizontalLayoutGroup>();
            head.spacing = 6; head.childAlignment = TextAnchor.MiddleLeft;
            head.childControlWidth = true; head.childControlHeight = true;
            head.childForceExpandWidth = false; head.childForceExpandHeight = false;
            var hle = headGo.AddComponent<LayoutElement>();
            hle.minHeight = 36;
            var arrow = label(headGo.transform, open ? "▼" : "►", SMALL, Theme.Muted, FontStyle.Normal, TextAnchor.MiddleCenter);
            Ui.Size(arrow, 16);
            summary?.Invoke(headGo.transform);
            var bodyCol = Ui.Column(img.transform, 5, new RectOffset(4, 0, 0, 6));
            bodyCol.gameObject.name = "Body";
            bool built = false;
            void Set(bool o)
            {
                if (o && !built) { built = true; body?.Invoke(bodyCol.transform); }
                bodyCol.gameObject.SetActive(o);
                arrow.text = o ? "▼" : "►";
            }
            Set(open);
            bool state = open;
            hb.onClick.AddListener(() => { state = !state; Set(state); onToggle?.Invoke(state); });
            return img.rectTransform;
        }

        /// <summary>Summary-Text (fett) für details().</summary>
        public static Action<Transform> summaryText(string text, Action<Transform> icon = null) => (t) =>
        {
            icon?.Invoke(t);
            var l = label(t, text, FS, Theme.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
            Ui.Size(l, -1, -1, 1);
        };

        /// <summary>.stats-table: links Bezeichnung (gedämpft), rechts Wert.</summary>
        public static RectTransform statsTable(Transform p, IEnumerable<string[]> rows)
        {
            var col = Ui.Column(p, 2, new RectOffset(0, 0, 3, 3));
            col.gameObject.name = "StatsTable";
            foreach (var r in rows)
            {
                var row = Ui.Row(col.transform, 10, TextAnchor.UpperLeft, false);
                var k = label(row.transform, r[0], SMALL, Theme.Muted);
                Ui.Size(k, -1, -1, 1);
                var v = label(row.transform, r[1], SMALL, Theme.Text, FontStyle.Normal, TextAnchor.UpperRight);
                Ui.Size(v, -1, -1, 1.4f);
            }
            return col.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Raster mit gleich breiten Spalten (CSS grid): build legt die Zelle im übergebenen Zeilen-Behälter an und gibt sie zurück.
        /// Zellen einer Zeile werden gleich hoch.
        /// </summary>
        public static RectTransform gridRows(Transform p, int count, int cols, Func<Transform, int, RectTransform> build, float spacing = 8)
        {
            var col = Ui.Column(p, spacing);
            col.gameObject.name = "Grid";
            for (int start = 0; start < count; start += cols)
            {
                var row = Ui.Row(col.transform, spacing, TextAnchor.UpperLeft, true);
                row.childForceExpandHeight = true;
                for (int c = 0; c < cols; c++)
                {
                    int i = start + c;
                    RectTransform cell;
                    if (i < count) cell = build(row.transform, i);
                    else cell = Ui.Rt(Ui.Node("Empty", row.transform));
                    var le = Ui.Get<LayoutElement>(cell.gameObject);
                    le.preferredWidth = 0; le.minWidth = 0; le.flexibleWidth = 1;
                }
            }
            return col.GetComponent<RectTransform>();
        }

        /// <summary>Waagerechte Spalten mit Anteilen (CSS grid-template-columns: 1.25fr 1fr).</summary>
        public static List<Transform> columns(Transform p, float gap, params float[] fr)
        {
            var row = Ui.Row(p, gap, TextAnchor.UpperLeft, true);
            row.childForceExpandHeight = false;
            var list = new List<Transform>();
            foreach (var f in fr)
            {
                var c = Ui.Column(row.transform, 8);
                var le = Ui.Get<LayoutElement>(c.gameObject);
                le.preferredWidth = 0; le.minWidth = 0; le.flexibleWidth = f;
                list.Add(c.transform);
            }
            return list;
        }
    }

    // ------------------------------------------------------------ Laufzeit-Helfer der Bildschirme

    /// <summary>Esc auf einem Bildschirm mit Zurück-Knopf (TS: keydown-Listener im shell).</summary>
    public class EscHandler : MonoBehaviour
    {
        public Action onEsc;
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || onEsc == null) return;
            if (!kb.escapeKey.wasPressedThisFrame) return;
            if (SettingsScreen.isCapturing() || Overlay.isOpen() || SelectPopup.IsOpen) return;
            onEsc();
        }
    }

    /// <summary>Einblenden wie .screen/.screen-inner-Animation (screenIn / riseIn); entfällt bei „Bewegung reduzieren“.</summary>
    public class Appear : MonoBehaviour
    {
        public RectTransform target;
        public float duration = 0.38f;
        float t;
        CanvasGroup cg;
        bool skip;

        void Start()
        {
            var app = GameApp.I;
            skip = app != null && app.save != null && app.save.settings != null && app.save.settings.reducedMotion;
            cg = Ui.Get<CanvasGroup>(gameObject);
            if (skip) { cg.alpha = 1; enabled = false; return; }
            cg.alpha = 0;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = 1 - Mathf.Pow(1 - k, 3);
            if (cg != null) cg.alpha = e;
            if (target != null) target.localScale = Vector3.one * Mathf.Lerp(0.985f, 1f, e);
            if (k >= 1) { if (target != null) target.localScale = Vector3.one; enabled = false; }
        }
    }

    /// <summary>
    /// Tooltip-Anzeige (title-Attribut) für Bildschirme und Overlays: zeigt TipSource.Current, wenn es zu diesem Baum gehört.
    /// </summary>
    public class TipView : MonoBehaviour
    {
        RectTransform box;
        Text text;
        TipSource last;
        RectTransform rt;

        void Start()
        {
            rt = GetComponent<RectTransform>();
            var img = Ui.Panel(transform, "Tooltip", new Color(20 / 255f, 16 / 255f, 34 / 255f, 0.97f), true, Theme.RoundSmall);
            img.raycastTarget = false;
            img.GetComponent<Outline>().effectColor = Theme.LineStrong;
            box = img.rectTransform;
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.pivot = new Vector2(0, 1);
            text = Ui.Label(box, "", Common.SMALL, Theme.Text, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = new Vector2(0, 1); text.rectTransform.anchorMax = new Vector2(0, 1);
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.anchoredPosition = new Vector2(9, -6);
            box.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (box == null) return;
            var src = TipSource.Current;
            string s = null;
            if (src != null && src.text != null && src.transform.IsChildOf(transform))
            {
                try { s = src.text(); } catch { s = null; }
            }
            if (string.IsNullOrEmpty(s)) { if (box.gameObject.activeSelf) box.gameObject.SetActive(false); last = null; return; }
            box.SetAsLastSibling();
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
            s = TextUtil.sym(s);
            if (src != last || text.text != s)
            {
                last = src;
                text.text = s;
                float w = Mathf.Min(380, text.preferredWidth + 2);
                text.rectTransform.sizeDelta = new Vector2(w, 10);
                float h = text.preferredHeight;
                text.rectTransform.sizeDelta = new Vector2(w, h);
                box.sizeDelta = new Vector2(w + 18, h + 12);
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            var canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mouse.position.ReadValue(), cam, out var local))
            {
                var r = rt.rect;
                var pos = local + new Vector2(14, -18);
                if (pos.x + box.sizeDelta.x > r.xMax - 4) pos.x = local.x - box.sizeDelta.x - 10;
                if (pos.y - box.sizeDelta.y < r.yMin + 4) pos.y = local.y + box.sizeDelta.y + 10;
                box.anchoredPosition = pos - r.center;
            }
        }
    }

    /// <summary>Aufklappliste für Common.select (liegt auf der obersten Ebene der Leinwand, nicht abgeschnitten).</summary>
    public class SelectPopup : MonoBehaviour
    {
        public static bool IsOpen => current != null;
        static SelectPopup current;
        Action close;

        public static void Open(Button anchor, List<(string value, string text)> options, string cur, Action<string> pick)
        {
            if (current != null) current.close();
            var canvas = anchor.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = canvas.rootCanvas.transform as RectTransform;
            // Klickfänger über allem
            var catcher = Ui.Node("SelectPopup", root);
            var crt = Ui.Rt(catcher);
            Ui.Fill(crt);
            var cimg = catcher.AddComponent<Image>();
            cimg.color = new Color(0, 0, 0, 0.001f);
            var cbtn = catcher.AddComponent<Button>();
            cbtn.transition = Selectable.Transition.None;
            var me = catcher.AddComponent<SelectPopup>();
            current = me;
            me.close = () =>
            {
                if (current == me) current = null;
                if (catcher != null) Destroy(catcher);
            };
            cbtn.onClick.AddListener(() => me.close());
            catcher.transform.SetAsLastSibling();

            // Lage des Ankers in Leinwandkoordinaten
            var corners = new Vector3[4];
            (anchor.transform as RectTransform).GetWorldCorners(corners);
            Vector2 bl = root.InverseTransformPoint(corners[0]);
            Vector2 br = root.InverseTransformPoint(corners[3]);
            float width = Mathf.Max(220, br.x - bl.x);
            float itemH = 34;
            float height = Mathf.Min(420, options.Count * itemH + 10);

            var panel = Ui.Panel(catcher.transform, "List", Theme.PanelSolid, true, Theme.RoundSmall);
            panel.GetComponent<Outline>().effectColor = Theme.LineStrong;
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0, 1);
            var rr = root.rect;
            Vector2 pos = bl - rr.center + new Vector2(0, -4);
            if (pos.y - height < rr.yMin - rr.center.y + 8) pos.y = pos.y + height + (corners[1].y - corners[0].y) / root.lossyScale.y + 8;
            prt.anchoredPosition = pos;
            prt.sizeDelta = new Vector2(width, height);
            var content = Ui.Scroll(panel.transform, out var sr, 2, new RectOffset(5, 5, 5, 5));
            Ui.Fill(sr.GetComponent<RectTransform>());
            foreach (var (v, text) in options)
            {
                string val = v;
                var b = Ui.Button(content, TextUtil.sym(text), () => { me.close(); pick(val); }, val == cur ? Ui.BtnKind.On : Ui.BtnKind.Ghost, 16);
                var t = b.GetComponentInChildren<Text>();
                t.alignment = TextAnchor.MiddleLeft;
                t.rectTransform.offsetMin = new Vector2(10, 2);
                var le = b.GetComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = itemH - 2;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) close?.Invoke();
        }

        void OnDestroy()
        {
            if (current == this) current = null;
        }
    }
}
