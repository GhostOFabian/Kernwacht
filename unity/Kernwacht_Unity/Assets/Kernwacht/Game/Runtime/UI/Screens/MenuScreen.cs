// Übersetzung von ui/screens/menu.ts – Hauptmenü mit leuchtendem Logo über der Hintergrundwelt.

using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class MenuScreen
    {
        /// <summary>Baut das Hauptmenü unter app.screenLayer und gibt die Wurzel zurück.</summary>
        public static RectTransform renderMenu(GameApp app)
        {
            var resume = app.canResume();
            GameState st = resume.state;
            System.Action go(System.Action fn) => () =>
            {
                Common.sfx(app, "ui");
                fn();
            };
            var root = Common.screenRoot(app, "Menu");
            var bg = root.gameObject.AddComponent<Image>();
            // .menu: radial-gradient(rgba(18,15,28,.15) → .85) – Mitte durchsichtig, Ränder dunkel
            bg.sprite = Vignette();
            bg.color = Color.white;
            bg.type = Image.Type.Simple;

            var box = Ui.Column(root, 14);
            box.gameObject.name = "MenuBox";
            box.childAlignment = TextAnchor.MiddleCenter;
            var brt = box.GetComponent<RectTransform>();
            Ui.Place(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(552, 0));
            var fit = box.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Logo
            var logo = Ui.Column(box.transform, 2, new RectOffset(0, 0, 8, 6));
            logo.childAlignment = TextAnchor.UpperCenter;
            var main = Ui.Label(logo.transform, "KERNWACHT", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            main.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(main, -1, 86);
            var fx = main.gameObject.AddComponent<LogoFx>();
            fx.spacing = 70 * 0.12f;
            fx.gradient = true;
            var glow1 = main.gameObject.AddComponent<Outline>();
            glow1.effectColor = new Color(183 / 255f, 123 / 255f, 1f, 0.28f);
            glow1.effectDistance = new Vector2(3, -3);
            var glow2 = main.gameObject.AddComponent<Outline>();
            glow2.effectColor = new Color(183 / 255f, 123 / 255f, 1f, 0.12f);
            glow2.effectDistance = new Vector2(7, -7);
            var pulse = main.gameObject.AddComponent<LogoGlow>();
            pulse.a = glow1; pulse.b = glow2;
            var sub = Ui.Label(logo.transform, "WELTEN UNTER BELAGERUNG", 15, Theme.Hex(0xd8c8ff), TextAnchor.MiddleCenter);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            Ui.Size(sub, -1, 22);
            sub.gameObject.AddComponent<LogoFx>().spacing = 15 * 0.4f;

            string loadError = Common.member<string>(app, "loadError");
            if (!string.IsNullOrEmpty(loadError)) Common.notice(box.transform, loadError, "warn");

            // nav.menu-buttons
            var nav = Ui.Panel(box.transform, "MenuButtons", new Color(20 / 255f, 16 / 255f, 32 / 255f, 0.82f), true, Theme.Round);
            var nv = nav.gameObject.AddComponent<VerticalLayoutGroup>();
            nv.padding = new RectOffset(18, 18, 18, 18);
            nv.spacing = 9;
            nv.childControlWidth = true; nv.childControlHeight = true;
            nv.childForceExpandWidth = true; nv.childForceExpandHeight = false;

            if (st != null)
            {
                Common.bigButton(nav.transform, "resume", "Fortsetzen",
                    $"{TextUtil.modeName(st.mode)} · Welle {U.Fmt(st.wave + 1)} · {st.map.name}", go(() => app.resumeRun()), Ui.BtnKind.Primary);
            }
            if (!string.IsNullOrEmpty(resume.error) && app.save.resume != null)
                Common.notice(nav.transform, $"Gespeicherte Partie nicht ladbar: {resume.error}", "warn", "small");
            Common.bigButton(nav.transform, "star", "Helden-Endlos",
                $"Frei steuerbarer Held · Beute, Laden, Skillbaum · Bestwelle {U.Fmt(app.save.progress.heroMeta.bestWave)}",
                go(() => app.go("heroSetup")), st != null ? Ui.BtnKind.Normal : Ui.BtnKind.Primary, st != null ? new Color(1f, 216 / 255f, 96 / 255f, 0.55f) : (Color?)null);
            var r1 = Ui.Row(nav.transform, 9, TextAnchor.MiddleLeft, true);
            Common.btn(r1.transform, "Karteneditor", go(() => app.go("editor")), Ui.BtnKind.Normal, "pen");
            Common.btn(r1.transform, $"Erfolge {app.save.progress.achievements.Count}", go(() => app.go("records")), Ui.BtnKind.Normal, "trophy");
            var r2 = Ui.Row(nav.transform, 9, TextAnchor.MiddleLeft, true);
            Common.btn(r2.transform, "Hilfe & Kodex", go(() => app.go("help")), Ui.BtnKind.Normal, "book");
            Common.btn(r2.transform, "Einstellungen", go(() => app.go("settings")), Ui.BtnKind.Normal, "gear");

            var foot = Common.para(box.transform, $"Version {Application.version} · Einzelspieler, offline spielbar · Alle Bestwerte werden nur lokal gespeichert.", "small muted", TextAnchor.UpperCenter);
            foot.color = Theme.Muted;

            root.gameObject.AddComponent<TipView>();
            var ap = root.gameObject.AddComponent<Appear>();
            ap.target = brt;
            return root;
        }

        /// <summary>Alias (Auftragsbeschreibung): menuScreen(app) = renderMenu(app).</summary>
        public static RectTransform menuScreen(GameApp app) => renderMenu(app);

        static Sprite _vignette;
        /// <summary>Radialer Verlauf: Mitte 15 %, Rand 85 % Deckkraft (Farbe #120f1c).</summary>
        static Sprite Vignette()
        {
            if (_vignette != null) return _vignette;
            const int W = 128, H = 72;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2 - 1, dy = (y + 0.5f) / H * 2 - 1;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.414f * 1.15f);
                    float a = Mathf.Lerp(0.15f, 0.85f, d);
                    px[y * W + x] = new Color(18 / 255f, 15 / 255f, 28 / 255f, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            _vignette = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            return _vignette;
        }
    }

    /// <summary>
    /// Schriftzug-Effekt: Buchstabenabstand (letter-spacing) und senkrechter Verlauf #fff3c4 → #ffd84a (45 %) → #c47aff
    /// (CSS background-clip:text). Muss vor Outline/Shadow-Komponenten liegen.
    /// </summary>
    public class LogoFx : BaseMeshEffect
    {
        public float spacing;
        public bool gradient;
        static readonly Color Top = Theme.Hex(0xfff3c4), Mid = Theme.Hex(0xffd84a), Bot = Theme.Hex(0xc47aff);
        readonly List<UIVertex> verts = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            verts.Clear();
            vh.GetUIVertexStream(verts);
            int quads = verts.Count / 6;
            if (quads == 0) return;
            float shift0 = -(quads - 1) * spacing / 2f;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < verts.Count; i++) { minY = Mathf.Min(minY, verts[i].position.y); maxY = Mathf.Max(maxY, verts[i].position.y); }
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i];
                int q = i / 6;
                v.position.x += shift0 + q * spacing;
                if (gradient && maxY > minY)
                {
                    float k = 1 - (v.position.y - minY) / (maxY - minY); // 0 oben, 1 unten
                    Color c = k < 0.45f ? Color.Lerp(Top, Mid, k / 0.45f) : Color.Lerp(Mid, Bot, (k - 0.45f) / 0.55f);
                    c.a = ((Color)v.color).a;
                    v.color = c;
                }
                verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(verts);
        }
    }

    /// <summary>logoGlow 4 s: Leuchten schwillt zu Gold an und wieder ab (entfällt bei „Bewegung reduzieren“).</summary>
    public class LogoGlow : MonoBehaviour
    {
        public Outline a, b;
        float t;
        static readonly Color Violet = new Color(183 / 255f, 123 / 255f, 1f, 1f), Gold = new Color(1f, 216 / 255f, 74 / 255f, 1f);

        void Update()
        {
            var app = GameApp.I;
            bool reduced = app != null && app.save != null && app.save.settings != null && app.save.settings.reducedMotion;
            if (reduced) t = 0;
            else t += Time.unscaledDeltaTime;
            float k = 0.5f - 0.5f * Mathf.Cos(t / 4f * Mathf.PI * 2); // 0 → 1 (bei 50 %) → 0
            if (a != null) { var c = Color.Lerp(Violet, Gold, k); c.a = Mathf.Lerp(0.28f, 0.42f, k); a.effectColor = c; }
            if (b != null) { var c = Color.Lerp(Violet, Gold, k); c.a = Mathf.Lerp(0.12f, 0.22f, k); b.effectColor = c; b.effectDistance = new Vector2(7 + 3 * k, -(7 + 3 * k)); }
        }
    }
}
