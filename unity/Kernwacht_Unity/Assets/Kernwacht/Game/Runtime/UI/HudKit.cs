using System;
using System.Collections.Generic;
using System.Text;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UText = UnityEngine.UI.Text;

namespace Kernwacht.Game.UI
{
    /// <summary>
    /// Bausteine des Spiel-HUDs (Ergänzung zu Ui.cs): Knöpfe mit Symbol, passende Breiten, Balken, Fließlayout,
    /// Zeigerereignisse und die Formatierer aus ui/dom.ts (fmt, fmtNum, pct).
    /// </summary>
    internal static class HK
    {
        public const int FS = 18;      // Grundschrift
        public const int SMALL = 15;   // .small (0,86 em)
        public const int TINY = 13;    // .tiny
        public const int H3 = 21;
        public const int H2 = 25;

        public static readonly Color PanelBg = new Color(30 / 255f, 23 / 255f, 48 / 255f, 0.97f);
        public static readonly Color BarBg = new Color(30 / 255f, 24 / 255f, 48 / 255f, 0.86f);
        public static readonly Color ChipBg = new Color(15 / 255f, 11 / 255f, 25 / 255f, 0.82f);
        public static readonly Color Accent2 = Theme.Hex(0x60e8d4);
        public static readonly Color StarGold = Theme.Hex(0xffd36b);

        // ------------------------------------------------------------ Formatierung (ui/dom.ts)

        public static string fmt(double n) { return U.De(U.Round(n), 0); }
        public static string fmtNum(double n, double digits = 1) { return U.De(n, digits); }
        public static string pct(double n) { return $"{U.Fmt(U.Round(n * 100))} %"; }
        public static string Rc(string text, Color c) { return $"<color=#{ColorUtility.ToHtmlStringRGBA(c)}>{text}</color>"; }
        public static string Hex6(double rgb) { return "#" + ((int)rgb & 0xffffff).ToString("x6"); }
        public static Color ColorOf(string css, Color fallback)
        {
            Color c;
            if (!string.IsNullOrEmpty(css) && ColorUtility.TryParseHtmlString(css, out c)) return c;
            return fallback;
        }

        /// <summary>
        /// Symbole aus den Konfigurationen sind oft Emojis, die die UI-Schrift nicht enthält. Nur Zeichen aus
        /// sicheren Blöcken (Latein, Satzzeichen, Pfeile, Formen) bleiben stehen, sonst leer.
        /// </summary>
        public static string Glyph(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            foreach (char ch in s)
            {
                int c = ch;
                bool ok = c < 0x250 || (c >= 0x2000 && c <= 0x22ff) || (c >= 0x25a0 && c <= 0x25ff) || c == 0x2605 || c == 0x2606 || c == 0x2665 || c == 0x2666 || c == 0xfe0f;
                if (!ok) return "";
            }
            return s;
        }

        public static double Now() { return Time.realtimeSinceStartupAsDouble * 1000.0; }

        // ------------------------------------------------------------ Elemente

        public static UText T(Transform p, string s, int size = FS, Color? c = null, TextAnchor a = TextAnchor.MiddleLeft, FontStyle st = FontStyle.Normal)
        {
            var t = Ui.Label(p, s ?? "", size, c ?? Theme.Text, a, st);
            t.lineSpacing = 1.05f;
            return t;
        }

        public static UText B3(Transform p, string s, Color? c = null) { return T(p, s, H3, c, TextAnchor.MiddleLeft, FontStyle.Bold); }

        /// <summary>Einfarbige Fläche ohne Rundung.</summary>
        public static Image Plain(Transform p, string name, Color c, bool raycast = false)
        {
            var img = Ui.Node(name, p).AddComponent<Image>();
            img.color = c;
            img.raycastTarget = raycast;
            return img;
        }

        public static void SetText(UText t, string s)
        {
            if (t != null && t.text != s) t.text = s;
        }

        public static Image Img(Transform p, Sprite s, float size, Color? c = null)
        {
            var go = Ui.Node("Img", p);
            var img = go.AddComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.color = c ?? Color.white;
            img.raycastTarget = false;
            if (s == null) img.color = new Color(1, 1, 1, 0.08f);
            Ui.Size(img, size, size);
            return img;
        }

        /// <summary>Knopf in Webspiel-Optik; Breite passt sich dem Text an (HudFit), Höhe fest.</summary>
        public static Button B(Transform p, string label, Action onClick, Ui.BtnKind kind = Ui.BtnKind.Normal, int size = 17, float h = 40, string icon = null)
        {
            var b = Ui.Button(p, label ?? "", onClick, kind, size);
            var le = b.GetComponent<LayoutElement>();
            le.minHeight = h; le.preferredHeight = h;
            var t = b.GetComponentInChildren<UText>();
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = Mathf.Max(9, size - 6);
            t.resizeTextMaxSize = size;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            if (kind == Ui.BtnKind.Primary) t.fontStyle = FontStyle.Bold;
            var fit = b.gameObject.AddComponent<HudFit>();
            fit.label = t;
            if (icon != null)
            {
                var ic = Ui.Node("Ic", b.transform).AddComponent<Image>();
                ic.sprite = Icons.Get(icon);
                ic.raycastTarget = false;
                ic.preserveAspect = true;
                float isz = Mathf.Min(h - 12, 22);
                if (string.IsNullOrEmpty(label))
                {
                    Ui.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(isz, isz));
                }
                else
                {
                    Ui.Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(9, 0), new Vector2(isz, isz));
                    t.rectTransform.offsetMin = new Vector2(9 + isz + 4, t.rectTransform.offsetMin.y);
                }
                fit.extra = string.IsNullOrEmpty(label) ? isz : isz + 6;
                fit.icon = ic;
            }
            return b;
        }

        /// <summary>Kleiner Knopf (.btn.sm).</summary>
        public static Button Sm(Transform p, string label, Action onClick, Ui.BtnKind kind = Ui.BtnKind.Normal, string icon = null)
        {
            return B(p, label, onClick, kind, 15, 32, icon);
        }

        /// <summary>Quadratischer Symbolknopf (.btn.icon.sm).</summary>
        public static Button IconBtn(Transform p, string icon, Action onClick, float size = 36)
        {
            var b = B(p, "", onClick, Ui.BtnKind.Normal, 17, size, icon);
            Ui.Size(b, size, size);
            b.GetComponent<HudFit>().fixedWidth = size;
            return b;
        }

        public static UText Label(Button b) { return b.GetComponent<HudFit>()?.label ?? b.GetComponentInChildren<UText>(); }

        public static void SetBtnText(Button b, string s) { SetText(Label(b), s); }

        public static void SetIcon(Button b, string icon)
        {
            var f = b.GetComponent<HudFit>();
            if (f == null || f.icon == null || f.iconName == icon) return;
            f.iconName = icon;
            f.icon.sprite = Icons.Get(icon);
        }

        /// <summary>disabled-Zustand: nicht klickbar und halb durchsichtig (wie .btn:disabled).</summary>
        public static void Enable(Button b, bool on)
        {
            if (b == null) return;
            if (b.interactable != on) b.interactable = on;
            var cg = b.GetComponent<CanvasGroup>();
            if (cg == null) { if (on) return; cg = b.gameObject.AddComponent<CanvasGroup>(); }
            float a = on ? 1f : 0.45f;
            if (cg.alpha != a) cg.alpha = a;
        }

        /// <summary>Hervorhebung „.on“ (Goldrand).</summary>
        public static void On(Button b, bool on, Ui.BtnKind offKind = Ui.BtnKind.Normal)
        {
            var f = b.GetComponent<HudFit>();
            int k = on ? 1 : 0;
            if (f != null && f.onState == k) return;
            if (f != null) f.onState = k;
            Ui.SetKind(b, on ? Ui.BtnKind.On : offKind);
        }

        /// <summary>Goldene Veredeln-Schaltfläche (.star-btn).</summary>
        public static Button Star(Button b)
        {
            b.GetComponent<Image>().color = Theme.Hex(0x7a5c18);
            var o = b.GetComponent<Outline>();
            if (o != null) o.effectColor = new Color(1f, 0.83f, 0.42f, 0.55f);
            Label(b).color = Theme.Hex(0xfff3cf);
            return b;
        }

        public static void Tip(Component c, string text) { if (!string.IsNullOrEmpty(text)) Ui.Tip(c, () => text); }

        public static HorizontalLayoutGroup HRow(Transform p, float spacing = 6, TextAnchor align = TextAnchor.MiddleLeft, bool expand = false)
        {
            var r = Ui.Row(p, spacing, align, expand);
            return r;
        }

        public static VerticalLayoutGroup VCol(Transform p, float spacing = 4, int pad = 0)
        {
            var c = Ui.Column(p, spacing, new RectOffset(pad, pad, pad, pad));
            return c;
        }

        /// <summary>Kasten mit Hintergrund und Rand, senkrecht gestapelt (.upg, .rune-box, .group-box …).</summary>
        public static VerticalLayoutGroup Box(Transform p, Color bg, Color border, int padX = 9, int padY = 7, float spacing = 4)
        {
            var img = Ui.Panel(p, "Box", bg, true, Theme.RoundSmall);
            img.GetComponent<Outline>().effectColor = border;
            var v = img.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(padX, padX, padY, padY);
            v.spacing = spacing;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HBox(Transform p, Color bg, Color border, int padX = 9, int padY = 7, float spacing = 8)
        {
            var img = Ui.Panel(p, "HBox", bg, true, Theme.RoundSmall);
            img.GetComponent<Outline>().effectColor = border;
            var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.padding = new RectOffset(padX, padX, padY, padY);
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static HudFlow Flow(Transform p, float gap = 4, bool center = false)
        {
            var go = Ui.Node("Flow", p);
            var f = go.AddComponent<HudFlow>();
            f.spacingX = gap; f.spacingY = gap; f.center = center;
            return f;
        }

        /// <summary>Elastischer Abstandshalter in einer Zeile.</summary>
        public static void Spacer(Transform p)
        {
            var go = Ui.Node("Spacer", p);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1; le.minWidth = 0;
        }

        public static void Grow(Component c) { Ui.Size(c, -1, -1, 1); }

        public static ContentSizeFitter Fit(Component c, bool w, bool h)
        {
            var f = Ui.Get<ContentSizeFitter>(c.gameObject);
            f.horizontalFit = w ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = h ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        /// <summary>Kinder sofort entfernen (aus dem Layout gelöst, dann zerstört).</summary>
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i);
                c.gameObject.SetActive(false);
                c.SetParent(null, false);
                UnityEngine.Object.Destroy(c.gameObject);
            }
        }

        public static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        /// <summary>Lage eines Elements in Pixeln eines anderen RectTransforms (für Popups an Knöpfen).</summary>
        public static Rect RectIn(RectTransform el, RectTransform space)
        {
            var corners = new Vector3[4];
            el.GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]);
            Vector2 b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        public static bool IsMouse(PointerEventData e)
        {
            var x = e as ExtendedPointerEventData;
            return x == null || x.pointerType == UIPointerType.MouseOrPen;
        }
    }

    /// <summary>Ein Balken wie .bar im Webspiel: Füllung, optionaler Schildanteil, Beschriftung.</summary>
    internal class HudBar
    {
        public Image root, fill, extra;
        public UText label;
        float shown = -1, shownExtra = -1;

        public HudBar(Transform p, Color fillColor, float height, int labelSize = 11, bool withExtra = false)
        {
            root = Ui.Panel(p, "Bar", Theme.Hex(0x0b0814), false, Theme.RoundSmall);
            Ui.Size(root, -1, height);
            fill = Ui.Panel(root.transform, "Fill", fillColor, false, Theme.RoundSmall);
            var r = fill.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 0.5f); r.offsetMin = r.offsetMax = Vector2.zero;
            if (withExtra)
            {
                extra = Ui.Panel(root.transform, "Extra", new Color(112 / 255f, 200 / 255f, 1f, 0.75f), false, Theme.RoundSmall);
                var e = extra.rectTransform; e.anchorMin = Vector2.zero; e.anchorMax = new Vector2(0, 1); e.pivot = new Vector2(0, 0.5f); e.offsetMin = e.offsetMax = Vector2.zero;
            }
            label = HK.T(root.transform, "", labelSize, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Ui.Fill(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sh = label.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.9f);
        }

        public void Set(float v)
        {
            v = Mathf.Clamp01(float.IsNaN(v) ? 0 : v);
            if (Mathf.Abs(v - shown) < 0.0005f) return;
            shown = v;
            fill.rectTransform.anchorMax = new Vector2(v, 1);
        }

        public void SetExtra(float v)
        {
            if (extra == null) return;
            v = Mathf.Clamp01(float.IsNaN(v) ? 0 : v);
            if (Mathf.Abs(v - shownExtra) < 0.0005f) return;
            shownExtra = v;
            extra.rectTransform.anchorMax = new Vector2(v, 1);
        }
    }

    /// <summary>Breite eines Knopfes aus seinem Text (statt der Mindestgröße des Sprites).</summary>
    public class HudFit : MonoBehaviour, ILayoutElement
    {
        public UText label;
        public Image icon;
        public string iconName;
        public float extra;
        public float fixedWidth = -1;
        public int onState = -1;
        float pref;

        public void CalculateLayoutInputHorizontal()
        {
            if (fixedWidth >= 0) { pref = fixedWidth; return; }
            float w = 0;
            if (label != null && !string.IsNullOrEmpty(label.text))
            {
                var set = label.GetGenerationSettings(Vector2.zero);
                set.resizeTextForBestFit = false;
                set.fontSize = label.resizeTextMaxSize > 0 ? label.resizeTextMaxSize : label.fontSize;
                w = label.cachedTextGeneratorForLayout.GetPreferredWidth(label.text, set) / Mathf.Max(0.0001f, label.pixelsPerUnit);
            }
            pref = Mathf.Max(minW, Mathf.Ceil(w + 20 + extra));
        }
        public void CalculateLayoutInputVertical() { }
        /// <summary>Mindestbreite (z. B. .btn.start { min-width: 8.5em }).</summary>
        public float minW;
        public float minWidth => fixedWidth >= 0 ? fixedWidth : Mathf.Max(minW, Mathf.Min(pref, 44));
        public float preferredWidth => pref;
        public float flexibleWidth => -1;
        public float minHeight => -1;
        public float preferredHeight => -1;
        public float flexibleHeight => -1;
        public int layoutPriority => 2;
    }

    /// <summary>Zeiger über einem Element (pointerenter/pointerleave).</summary>
    public class HudHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool over;
        public Action<PointerEventData> onEnter;
        public Action onExit;
        public void OnPointerEnter(PointerEventData e) { over = true; onEnter?.Invoke(e); }
        public void OnPointerExit(PointerEventData e) { over = false; onExit?.Invoke(); }
        void OnDisable() { if (over) { over = false; onExit?.Invoke(); } }
    }

    /// <summary>pointerdown auf einem Element.</summary>
    public class HudPress : MonoBehaviour, IPointerDownHandler
    {
        public Action<PointerEventData> onDown;
        public void OnPointerDown(PointerEventData e) { onDown?.Invoke(e); }
    }

    /// <summary>Taktgeber für Zeitabläufe des HUDs (Banner, Tooltips), unabhängig von der Spielschleife.</summary>
    public class HudTicker : MonoBehaviour
    {
        public Hud hud;
        void Update() { hud?.tick(Time.unscaledDeltaTime); }
    }

    /// <summary>Zeilenumbrechendes Layout (flex-wrap) für Chips, Schalter und Runen.</summary>
    public class HudFlow : LayoutGroup
    {
        public float spacingX = 4, spacingY = 4;
        public bool center;
        readonly List<float> rowW = new List<float>();
        readonly List<float> rowH = new List<float>();
        readonly List<int> rowEnd = new List<int>();

        float ChildW(RectTransform c, float avail)
        {
            return Mathf.Min(Mathf.Max(LayoutUtility.GetMinWidth(c), LayoutUtility.GetPreferredWidth(c)), Mathf.Max(0, avail));
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            float w = padding.horizontal, mn = 0;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                var c = rectChildren[i];
                w += LayoutUtility.GetPreferredWidth(c) + (i > 0 ? spacingX : 0);
                mn = Mathf.Max(mn, LayoutUtility.GetMinWidth(c));
            }
            SetLayoutInputForAxis(mn + padding.horizontal, w, -1, 0);
        }

        void Rows(float width)
        {
            rowW.Clear(); rowH.Clear(); rowEnd.Clear();
            float avail = width - padding.horizontal;
            float x = 0, h = 0;
            for (int i = 0; i < rectChildren.Count; i++)
            {
                var c = rectChildren[i];
                float cw = ChildW(c, avail);
                if (x > 0 && x + spacingX + cw > avail + 0.5f)
                {
                    rowW.Add(x); rowH.Add(h); rowEnd.Add(i);
                    x = 0; h = 0;
                }
                x += (x > 0 ? spacingX : 0) + cw;
                h = Mathf.Max(h, LayoutUtility.GetPreferredHeight(c));
            }
            if (rectChildren.Count > 0) { rowW.Add(x); rowH.Add(h); rowEnd.Add(rectChildren.Count); }
        }

        public override void CalculateLayoutInputVertical()
        {
            Rows(rectTransform.rect.width);
            float h = padding.vertical;
            for (int r = 0; r < rowH.Count; r++) h += rowH[r] + (r > 0 ? spacingY : 0);
            SetLayoutInputForAxis(h, h, -1, 1);
        }

        void Place(int axis)
        {
            float width = rectTransform.rect.width;
            Rows(width);
            float avail = width - padding.horizontal;
            float y = padding.top;
            int start = 0;
            for (int r = 0; r < rowEnd.Count; r++)
            {
                float x = padding.left + (center ? Mathf.Max(0, (avail - rowW[r]) / 2) : 0);
                for (int i = start; i < rowEnd[r]; i++)
                {
                    var c = rectChildren[i];
                    float cw = ChildW(c, avail);
                    float ch = LayoutUtility.GetPreferredHeight(c);
                    if (axis == 0) SetChildAlongAxis(c, 0, x, cw);
                    else SetChildAlongAxis(c, 1, y + (rowH[r] - ch) / 2, ch);
                    x += cw + spacingX;
                }
                y += rowH[r] + spacingY;
                start = rowEnd[r];
            }
        }

        public override void SetLayoutHorizontal() { Place(0); }
        public override void SetLayoutVertical() { Place(1); }
    }

    /// <summary>Signaturen ohne Speicherbelegung vergleichen (nur bei Änderung wird ein string erzeugt).</summary>
    internal class Sig
    {
        readonly StringBuilder sb = new StringBuilder(256);
        public Sig Reset() { sb.Length = 0; return this; }
        public Sig S(string s) { if (s != null) sb.Append(s); return this; }
        public Sig C(char c) { sb.Append(c); return this; }
        public Sig B(bool b) { sb.Append(b ? '1' : '0'); return this; }
        /// <summary>Zahl anhängen: ganzzahlig ohne Speicherbelegung, sonst auf 1/1000 gerundet.</summary>
        public Sig N(double v)
        {
            if (double.IsNaN(v)) { sb.Append('n'); return this; }
            if (double.IsInfinity(v)) { sb.Append(v > 0 ? 'I' : 'J'); return this; }
            double r = Math.Round(v);
            if (Math.Abs(v - r) > 1e-9) { sb.Append('f'); r = Math.Round(v * 1000); }
            long x = (long)r;
            if (x < 0) { sb.Append('-'); x = -x; }
            if (x == 0) { sb.Append('0'); return this; }
            int start = sb.Length;
            while (x > 0) { sb.Append((char)('0' + (int)(x % 10))); x /= 10; }
            int end = sb.Length - 1;
            while (start < end) { char t = sb[start]; sb[start] = sb[end]; sb[end] = t; start++; end--; }
            return this;
        }
        /// <summary>true, wenn sich die Signatur gegenüber <paramref name="last"/> geändert hat; aktualisiert <paramref name="last"/>.</summary>
        public bool Changed(ref string last)
        {
            if (last != null && last.Length == sb.Length)
            {
                bool same = true;
                for (int i = 0; i < sb.Length; i++) if (sb[i] != last[i]) { same = false; break; }
                if (same) return false;
            }
            last = sb.ToString();
            return true;
        }
        public bool Same(string last)
        {
            if (last == null || last.Length != sb.Length) return false;
            for (int i = 0; i < sb.Length; i++) if (sb[i] != last[i]) return false;
            return true;
        }
        public override string ToString() { return sb.ToString(); }
    }

    /// <summary>
    /// Texte aus ui/text.ts und render/palette.ts (DAMAGE_NAMES), die das HUD braucht.
    /// PORT: Gleiche Namen und Inhalte wie TextUtil (Bildschirm-Übersetzer); hier lokal, damit das HUD unabhängig
    /// von dessen Rückgabetypen kompiliert. Bei Bedarf durch TextUtil-Aufrufe ersetzen.
    /// </summary>
    internal static class HudText
    {
        public static readonly Dictionary<string, string> DAMAGE_NAMES = new Dictionary<string, string>
        {
            ["physical"] = "Physisch", ["fire"] = "Feuer", ["frost"] = "Frost", ["lightning"] = "Blitz", ["poison"] = "Gift", ["arcane"] = "Arkan", ["sonic"] = "Schall",
        };

        public static readonly Dictionary<string, string> PRIORITY_NAMES = new Dictionary<string, string>
        {
            ["first"] = "Erster auf dem Weg", ["exit"] = "Nächster zum Ausgang", ["strong"] = "Meiste Lebenspunkte",
            ["weak"] = "Wenigste Lebenspunkte", ["close"] = "Nächster zum Turm", ["random"] = "Zufällig",
        };

        public static readonly List<string> PRIORITY_ORDER = new List<string> { "first", "exit", "strong", "weak", "close", "random" };

        public static readonly Dictionary<string, string> MODE_NAMES = new Dictionary<string, string>
        {
            ["quick"] = "Schnelle Partie", ["campaign"] = "Kampagne", ["endless"] = "Endlosmodus", ["daily"] = "Tagesherausforderung", ["custom"] = "Eigene Karte", ["heroEndless"] = "Helden-Endlos",
        };

        public static string Dmg(string type) { return type != null && DAMAGE_NAMES.TryGetValue(type, out var n) ? n : (type ?? "–"); }

        public static string damageTypeName(TowerDefinition def) { return def.damageType != null ? Dmg(def.damageType) : "–"; }

        public static double V(Dictionary<string, double> st, string k) { return st != null && st.TryGetValue(k, out var v) ? v : 0; }

        public static double dps(Dictionary<string, double> st)
        {
            double dmg = V(st, "damage"), cd = V(st, "cooldown");
            if (dmg <= 0 || cd <= 0) return 0;
            return (dmg * Math.Max(1, V(st, "multishot"))) / cd;
        }

        static readonly string[] SHOW =
        {
            "splash", "chains", "pierce", "multishot", "burnDps", "burnDur", "chillPct", "freezeChance", "poisonDps",
            "poisonMaxStacks", "bleedDps", "bleedMaxStacks", "shredPct", "airDmgMul", "markPct", "interrupt", "knockback", "silenceDur", "stunDur", "armorPierce", "critChance",
            "income", "killBounty", "linkRange", "maxLinks", "networkBonus", "riftBack",
            "meteorCount", "fragments", "arcDps", "arcRange", "pullCells", "laserRamp", "auraDmg", "auraSpeed",
        };

        static readonly HashSet<string> PCT_KEYS = new HashSet<string> { "chillPct", "freezeChance", "markPct", "armorPierce", "critChance", "networkBonus", "auraDmg", "auraSpeed", "shredPct" };

        public static List<string[]> statLines(Dictionary<string, double> st, TowerDefinition def)
        {
            var @out = new List<string[]>();
            double dmg = V(st, "damage");
            if (dmg > 0) @out.Add(new[] { "Schaden", $"{HK.fmtNum(dmg)} ({damageTypeName(def)})" });
            if (V(st, "range") > 0) @out.Add(new[] { "Reichweite", $"{HK.fmtNum(V(st, "range"))} Felder" });
            if (dmg > 0 && def.attack != "pylon") @out.Add(new[] { "Angriff alle", $"{HK.fmtNum(V(st, "cooldown"), 2)} s" });
            double d = dps(st);
            if (d > 0 && def.attack != "pylon") @out.Add(new[] { "Schaden/s", HK.fmtNum(d) });
            foreach (var k in SHOW)
            {
                double v = V(st, k);
                if (!U.Truthy(v) || (k == "multishot" && v <= 1) || (k == "meteorCount" && v <= 1) || (k == "laserRamp" && v <= 1) || (k == "poisonMaxStacks" && V(st, "poisonDps") <= 0) || (k == "bleedMaxStacks" && V(st, "bleedDps") <= 0) || (k == "airDmgMul" && v <= 1)) continue;
                string label = Stats.STAT_LABELS.TryGetValue(k, out var l) ? l : k;
                @out.Add(new[] { label, PCT_KEYS.Contains(k) ? HK.pct(v) : HK.fmtNum(v) });
            }
            if (def.attack != "economy" && def.attack != "none")
            {
                string g = V(st, "hitsGround") > 0 ? "Boden" : "", a = V(st, "hitsAir") > 0 ? "Luft" : "";
                string hit = g != "" && a != "" ? g + " + " + a : (g + a);
                @out.Add(new[] { "Trifft", hit != "" ? hit : "–" });
            }
            return @out;
        }

        public static Dictionary<string, double> baseStatsOf(string id)
        {
            var st = Stats.zeroStats();
            foreach (var kv in Towers.TOWERS[id].@base) st[kv.Key] = kv.Value;
            return st;
        }

        public class WaveSum { public string label; public string icon; public double count; public bool elite; public bool boss; }

        public static WaveSum waveSummary(WaveGroup g)
        {
            if (!string.IsNullOrEmpty(g.boss)) return new WaveSum { label = Enemies.BOSSES[g.boss].name, icon = "", count = 1, elite = false, boss = true };
            var d = Enemies.ENEMIES[g.enemy];
            return new WaveSum { label = g.elite ? $"Elite-{d.name}" : d.name, icon = d.icon, count = g.count, elite = g.elite, boss = false };
        }

        static string pctTxt(double v) { return $"{U.Fmt(U.Round(Math.Abs(v) * 100))} %"; }

        public static void damageMatchups(string type, List<string> good, List<string> bad)
        {
            void add(string name, Dictionary<string, double> resist)
            {
                if (!resist.TryGetValue(type, out var v)) return;
                if (v <= -0.1) good.Add($"{name} +{pctTxt(v)}");
                else if (v >= 0.2) bad.Add($"{name} −{pctTxt(v)}");
            }
            foreach (var e in Enemies.ENEMIES.Values) if (e.id != "boss") add(e.name, e.resist);
            foreach (var b in Enemies.BOSSES.Values) add(b.name, b.resist);
        }

        public static List<string> towersOfType(string type)
        {
            var l = new List<string>();
            foreach (var t in Towers.TOWERS.Values)
                if (t.damageType == type && t.legendary != true && t.attack != "economy" && t.attack != "none" && t.attack != "pylon") l.Add(t.name);
            return l;
        }
    }
}
