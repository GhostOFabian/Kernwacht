using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    /// <summary>
    /// Farben und Maße der Oberfläche – entsprechen den CSS-Variablen des Webspiels (styles.css :root).
    /// Bezugsgröße 1920×1080; der CanvasScaler skaliert für andere Auflösungen.
    /// </summary>
    public static class Theme
    {
        public static readonly Color Bg = Hex(0x120f1c);
        public static readonly Color Panel = new Color(24 / 255f, 19 / 255f, 38 / 255f, 0.94f);
        public static readonly Color PanelSolid = Hex(0x1b1629);
        public static readonly Color Glass = new Color(22 / 255f, 17 / 255f, 36 / 255f, 0.78f);
        public static readonly Color GlassStrong = new Color(20 / 255f, 15 / 255f, 33 / 255f, 0.9f);
        public static readonly Color Line = new Color(190 / 255f, 160 / 255f, 1f, 0.18f);
        public static readonly Color LineStrong = new Color(190 / 255f, 160 / 255f, 1f, 0.4f);
        public static readonly Color Text = Hex(0xf1ecff);
        public static readonly Color Muted = Hex(0xb3a9cc);
        public static readonly Color Gold = Hex(0xffd84a);
        public static readonly Color Accent = Hex(0xb77bff);
        public static readonly Color Good = Hex(0x6be38a);
        public static readonly Color Bad = Hex(0xff6b6b);
        public static readonly Color Warn = Hex(0xffb347);
        public static readonly Color Btn = Hex(0x2a2140);
        public static readonly Color BtnHover = Hex(0x3a2d58);
        public static readonly Color BtnPrimary = Hex(0x7444cc);
        public static readonly Color BtnDanger = Hex(0x922f2f);
        public static readonly Color BtnOn = Hex(0x43355f);
        public const int Tap = 40;
        public const int FontSize = 18;

        public static Color Hex(int rgb, float a = 1f) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
        public static Color Hex(double rgb, float a = 1f) => Hex((int)rgb, a);
        public static string Css(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.Load<Font>("KernwachtFont");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        static Sprite _round, _roundSmall, _circle;
        /// <summary>Abgerundetes Rechteck (9-Slice), zur Laufzeit erzeugt – keine Bilddateien nötig.</summary>
        public static Sprite Round { get { if (_round == null) _round = MakeRound(32, 10); return _round; } }
        public static Sprite RoundSmall { get { if (_roundSmall == null) _roundSmall = MakeRound(16, 5); return _roundSmall; } }
        public static Sprite Circle { get { if (_circle == null) _circle = MakeRound(64, 32); return _circle; } }

        static Sprite MakeRound(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            int b = Mathf.Min(radius, size / 2 - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }

    /// <summary>Kleine Bausteine, um uGUI per Code ähnlich wie die DOM-Helfer des Webspiels aufzubauen.</summary>
    public static class Ui
    {
        public static RectTransform Rt(GameObject go) => Get<RectTransform>(go);

        /// <summary>Komponente holen oder anlegen (der ??-Operator scheitert an Unitys Fake-Null).</summary>
        public static T Get<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return go;
        }

        /// <summary>Ganze Fläche des Elternobjekts ausfüllen.</summary>
        public static RectTransform Fill(RectTransform r, float inset = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        /// <summary>Feste Position: Anker (0..1), Pivot, Versatz und Größe in Bezugspixeln.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static Image Panel(Transform parent, string name, Color? color = null, bool outline = true, Sprite sprite = null)
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite ?? Theme.Round;
            img.type = Image.Type.Sliced;
            img.color = color ?? Theme.Glass;
            if (outline)
            {
                var o = go.AddComponent<Outline>();
                o.effectColor = Theme.Line;
                o.effectDistance = new Vector2(1, -1);
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size = Theme.FontSize, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = Node("Text", parent);
            var t = go.AddComponent<Text>();
            t.font = Theme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Theme.Text;
            t.alignment = align;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public enum BtnKind { Normal, Primary, Danger, On, Ghost }

        public static Color BtnColor(BtnKind k)
        {
            switch (k)
            {
                case BtnKind.Primary: return Theme.BtnPrimary;
                case BtnKind.Danger: return Theme.BtnDanger;
                case BtnKind.On: return Theme.BtnOn;
                case BtnKind.Ghost: return new Color(1, 1, 1, 0.04f);
            }
            return Theme.Btn;
        }

        public static Button Button(Transform parent, string text, Action onClick, BtnKind kind = BtnKind.Normal, int size = 17)
        {
            var img = Panel(parent, "Btn", BtnColor(kind), true, Theme.RoundSmall);
            if (kind == BtnKind.On) img.GetComponent<Outline>().effectColor = Theme.Gold;
            if (kind == BtnKind.Primary) img.GetComponent<Outline>().effectColor = Theme.Hex(0xc9a0ff);
            var b = img.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.2f, 1.35f, 1f);
            cb.pressedColor = new Color(0.85f, 0.8f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1, 1, 1, 0.45f);
            cb.colorMultiplier = 1.4f;
            b.colors = cb;
            b.targetGraphic = img;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Label(img.transform, text, size, Theme.Text, TextAnchor.MiddleCenter);
            Fill(t.rectTransform, 6);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = Theme.Tap;
            le.preferredHeight = Theme.Tap;
            return b;
        }

        public static void SetText(Button b, string text)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = text;
        }

        public static void SetKind(Button b, BtnKind kind)
        {
            var img = b.GetComponent<Image>();
            img.color = BtnColor(kind);
            var o = b.GetComponent<Outline>();
            if (o != null) o.effectColor = kind == BtnKind.On ? Theme.Gold : kind == BtnKind.Primary ? Theme.Hex(0xc9a0ff) : Theme.Line;
        }

        public static HorizontalLayoutGroup Row(Transform parent, float spacing = 8, TextAnchor align = TextAnchor.MiddleLeft, bool expand = true)
        {
            var go = Node("Row", parent);
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = expand; h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup Column(Transform parent, float spacing = 8, RectOffset padding = null)
        {
            var go = Node("Col", parent);
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = TextAnchor.UpperLeft;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return v;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, float spacing = 8)
        {
            var go = Node("Grid", parent);
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = cell;
            g.spacing = new Vector2(spacing, spacing);
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return g;
        }

        /// <summary>Senkrecht scrollender Bereich; Inhalte in den zurückgegebenen Inhaltscontainer legen.</summary>
        public static RectTransform Scroll(Transform parent, out ScrollRect scroll, float spacing = 8, RectOffset padding = null)
        {
            var root = Node("Scroll", parent);
            scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var vp = Node("Viewport", root.transform);
            Fill(Rt(vp));
            vp.AddComponent<RectMask2D>();
            var vpImg = vp.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.001f);
            var content = Column(vp.transform, spacing, padding);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rt(vp);
            scroll.content = crt;
            return crt;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = Get<LayoutElement>(c.gameObject);
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        /// <summary>Balken (LP, Mana, EP): Hintergrund + Füllung; Rückgabe = Füllungsbild.</summary>
        public static Image Bar(Transform parent, Color fill, float height = 10, Color? bg = null)
        {
            var back = Panel(parent, "Bar", bg ?? new Color(0, 0, 0, 0.55f), false, Theme.RoundSmall);
            Size(back, -1, height);
            var f = Panel(back.transform, "Fill", fill, false, Theme.RoundSmall);
            var r = f.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 0.5f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return f;
        }

        public static void SetBar(Image fill, float v)
        {
            var r = fill.rectTransform;
            r.anchorMax = new Vector2(Mathf.Clamp01(v), 1);
        }

        public static Slider Slider(Transform parent, float value, Action<float> onChange)
        {
            var go = Node("Slider", parent);
            Size(go.transform as RectTransform, -1, 28);
            var bg = Panel(go.transform, "Bg", new Color(0, 0, 0, 0.5f), false, Theme.RoundSmall);
            var br = bg.rectTransform; br.anchorMin = new Vector2(0, 0.35f); br.anchorMax = new Vector2(1, 0.65f); br.offsetMin = br.offsetMax = Vector2.zero;
            var fillArea = Node("FillArea", go.transform);
            var fr = Rt(fillArea); fr.anchorMin = new Vector2(0, 0.35f); fr.anchorMax = new Vector2(1, 0.65f); fr.offsetMin = new Vector2(0, 0); fr.offsetMax = new Vector2(0, 0);
            var fill = Panel(fillArea.transform, "Fill", Theme.Accent, false, Theme.RoundSmall);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Node("HandleArea", go.transform);
            Fill(Rt(handleArea));
            var handle = Panel(handleArea.transform, "Handle", Theme.Gold, false, Theme.Circle);
            handle.rectTransform.sizeDelta = new Vector2(22, 0);
            var s = go.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = 0; s.maxValue = 1;
            s.value = value;
            s.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return s;
        }

        public static Toggle Check(Transform parent, string label, bool value, Action<bool> onChange, string hint = null)
        {
            var row = Row(parent, 10, TextAnchor.MiddleLeft, false);
            Size(row, -1, Theme.Tap);
            var boxImg = Panel(row.transform, "Box", Theme.Btn, true, Theme.RoundSmall);
            Size(boxImg, 26, 26);
            var mark = Panel(boxImg.transform, "Mark", Theme.Accent, false, Theme.RoundSmall);
            Fill(mark.rectTransform, 5);
            var t = row.gameObject.AddComponent<Toggle>();
            t.targetGraphic = boxImg;
            t.graphic = mark;
            t.isOn = value;
            t.onValueChanged.AddListener(v => onChange?.Invoke(v));
            var lbl = Label(row.transform, hint == null ? label : $"{label} <color={Theme.Css(Theme.Muted)}><size=14>– {hint}</size></color>", 17);
            Size(lbl, -1, -1, 1);
            return t;
        }

        /// <summary>Segmentschalter (wie seg() im Webspiel): eine Auswahl aus mehreren Knöpfen.</summary>
        public static List<Button> Seg<T>(Transform parent, (T value, string label)[] opts, T current, Action<T> pick)
        {
            var row = Row(parent, 6, TextAnchor.MiddleLeft, true);
            var list = new List<Button>();
            foreach (var o in opts)
            {
                var val = o.value;
                Button b = null;
                b = Button(row.transform, o.label, () =>
                {
                    foreach (var x in list) SetKind(x, BtnKind.Normal);
                    SetKind(b, BtnKind.On);
                    pick(val);
                }, EqualityComparer<T>.Default.Equals(val, current) ? BtnKind.On : BtnKind.Normal, 16);
                list.Add(b);
            }
            return list;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Tooltip-Text an einem Element: wird vom HUD beim Überfahren angezeigt.</summary>
        public static void Tip(Component c, Func<string> text)
        {
            var tip = Get<TipSource>(c.gameObject);
            tip.text = text;
        }
    }

    public class TipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Func<string> text;
        public static TipSource Current;
        public void OnPointerEnter(PointerEventData e) { Current = this; }
        public void OnPointerExit(PointerEventData e) { if (Current == this) Current = null; }
        void OnDisable() { if (Current == this) Current = null; }
    }
}
