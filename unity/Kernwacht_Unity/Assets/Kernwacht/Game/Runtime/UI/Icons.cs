// Übersetzung von ui/icons.ts (SVG-Symbole) und der Porträt-Idee aus render/IconFactory.ts.
// Get(name): die schlichten Bedienelement-Symbole werden aus denselben SVG-Pfaden wie im Webspiel
//            prozedural in eine 64×64-Textur gerastert (weiß auf transparent, Kantenglättung, Zwischenspeicher).
// Model(id): Porträt eines 3D-Modells aus dem Modellkatalog, einmalig per Render-Textur aufgenommen.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Kernwacht.Models;
using UnityEngine;

namespace Kernwacht.Game.UI
{
    public static class Icons
    {
        // ------------------------------------------------------------ SVG-Daten (wörtlich aus ui/icons.ts, viewBox 0 0 24 24)

        static readonly Dictionary<string, string> P = new Dictionary<string, string>
        {
            ["menu"] = @"<path d=""M4 6h16M4 12h16M4 18h16""/>",
            ["pause"] = @"<path d=""M8 5v14M16 5v14"" stroke-width=""3.2""/>",
            ["play"] = @"<path d=""M7 4.5v15l12-7.5z"" fill=""currentColor"" stroke=""none""/>",
            ["speed"] = @"<path d=""M4 6l7 6-7 6zM12 6l7 6-7 6z"" fill=""currentColor"" stroke=""none""/>",
            ["coin"] = @"<circle cx=""12"" cy=""12"" r=""8.5"" fill=""currentColor"" stroke=""none""/><circle cx=""12"" cy=""12"" r=""5.6"" fill=""none"" stroke=""#7a5a10"" stroke-width=""1.6""/><path d=""M12 8.6v6.8"" stroke=""#7a5a10"" stroke-width=""1.8""/>",
            ["heart"] = @"<path d=""M12 20s-7.5-4.6-7.5-10A4.3 4.3 0 0 1 12 7.4 4.3 4.3 0 0 1 19.5 10c0 5.4-7.5 10-7.5 10z"" fill=""currentColor"" stroke=""none""/>",
            ["wave"] = @"<path d=""M2.5 10c2.4-3 4.8-3 7.2 0s4.8 3 7.2 0 3.4-2 4.6-1M2.5 16c2.4-3 4.8-3 7.2 0s4.8 3 7.2 0 3.4-2 4.6-1""/>",
            ["energy"] = @"<path d=""M13.5 2.5L5 13.5h6l-1.5 8 8.5-11h-6z"" fill=""currentColor"" stroke=""none""/>",
            ["rotL"] = @"<path d=""M4.5 12a7.5 7.5 0 1 0 2.2-5.3""/><path d=""M4 3.5v4.5h4.5""/>",
            ["rotR"] = @"<path d=""M19.5 12a7.5 7.5 0 1 1-2.2-5.3""/><path d=""M20 3.5v4.5h-4.5""/>",
            ["plus"] = @"<path d=""M12 5v14M5 12h14""/>",
            ["minus"] = @"<path d=""M5 12h14""/>",
            ["fit"] = @"<path d=""M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5""/>",
            ["range"] = @"<circle cx=""12"" cy=""12"" r=""8""/><circle cx=""12"" cy=""12"" r=""2.5"" fill=""currentColor""/>",
            ["help"] = @"<circle cx=""12"" cy=""12"" r=""9""/><path d=""M9.3 9.4a2.8 2.8 0 0 1 5.4 1c0 1.9-2.7 2.3-2.7 4""/><circle cx=""12"" cy=""17.6"" r=""0.6"" fill=""currentColor""/>",
            ["close"] = @"<path d=""M6 6l12 12M18 6L6 18""/>",
            ["map"] = @"<path d=""M3.5 6.5l5.5-2 6 2 5.5-2v13l-5.5 2-6-2-5.5 2z""/><path d=""M9 4.5v13M15 6.5v13""/>",
            ["sword"] = @"<path d=""M14.5 3.5h6v6L10 20l-3-3z""/><path d=""M5 15l4 4M3.5 20.5l2.5-2.5""/>",
            ["shield"] = @"<path d=""M12 3l7.5 3v5.5c0 4.5-3.2 8-7.5 9.5-4.3-1.5-7.5-5-7.5-9.5V6z""/>",
            ["snow"] = @"<path d=""M12 3v18M4.2 7.5l15.6 9M4.2 16.5l15.6-9""/><path d=""M9.5 4.5L12 6l2.5-1.5M9.5 19.5L12 18l2.5 1.5""/>",
            ["target"] = @"<circle cx=""12"" cy=""12"" r=""8""/><circle cx=""12"" cy=""12"" r=""3.5""/><path d=""M12 2v4M12 18v4M2 12h4M18 12h4""/>",
            ["flag"] = @"<path d=""M6 21V4""/><path d=""M6 4.5h11l-2.5 4 2.5 4H6""/>",
            ["hold"] = @"<path d=""M12 3l7.5 3v5.5c0 4.5-3.2 8-7.5 9.5-4.3-1.5-7.5-5-7.5-9.5V6z""/><path d=""M8.5 12h7""/>",
            ["advance"] = @"<path d=""M5 12h12M12 6l6 6-6 6""/>",
            ["retreat"] = @"<path d=""M19 12H7M12 6l-6 6 6 6""/>",
            ["star"] = @"<path d=""M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z"" fill=""currentColor"" stroke=""none""/>",
            ["fuse"] = @"<circle cx=""8"" cy=""12"" r=""4.5""/><circle cx=""16"" cy=""12"" r=""4.5""/><path d=""M12 8.6v6.8""/>",
            ["bolt"] = @"<path d=""M13.5 2.5L5 13.5h6l-1.5 8 8.5-11h-6z""/>",
            ["up"] = @"<path d=""M12 19V6M6 11l6-6 6 6""/>",
            ["coins"] = @"<ellipse cx=""12"" cy=""7"" rx=""7"" ry=""3""/><path d=""M5 7v5c0 1.7 3.1 3 7 3s7-1.3 7-3V7M5 12v5c0 1.7 3.1 3 7 3s7-1.3 7-3v-5""/>",
            ["trophy"] = @"<path d=""M8 4h8v5a4 4 0 0 1-8 0zM8 6H4.5a3 3 0 0 0 3.5 4M16 6h3.5a3 3 0 0 1-3.5 4M12 13v4M8.5 20h7M10 17h4""/>",
            ["skull"] = @"<path d=""M12 3a7.5 7.5 0 0 0-7.5 7.5c0 2.6 1.3 4.4 3 5.4V19h9v-3.1c1.7-1 3-2.8 3-5.4A7.5 7.5 0 0 0 12 3z""/><circle cx=""9"" cy=""11"" r=""1.6"" fill=""currentColor""/><circle cx=""15"" cy=""11"" r=""1.6"" fill=""currentColor""/>",
            ["crown"] = @"<path d=""M3.5 8l4.2 4 4.3-7 4.3 7 4.2-4-2 11h-13z""/>",
            ["gear"] = @"<circle cx=""12"" cy=""12"" r=""3.2""/><path d=""M12 2.8v3M12 18.2v3M2.8 12h3M18.2 12h3M5.5 5.5l2.1 2.1M16.4 16.4l2.1 2.1M5.5 18.5l2.1-2.1M16.4 7.6l2.1-2.1""/>",
            ["book"] = @"<path d=""M4 5.5A2.5 2.5 0 0 1 6.5 3H20v15H6.5A2.5 2.5 0 0 0 4 20.5zM4 20.5A2.5 2.5 0 0 0 6.5 21H20""/>",
            ["scroll"] = @"<path d=""M7 3h11a2 2 0 0 1 2 2v2h-4M7 3a2 2 0 0 0-2 2v12M16 7v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-2h11""/>",
            ["infinity"] = @"<path d=""M6.5 8.5C3 8.5 3 15.5 6.5 15.5 10 15.5 14 8.5 17.5 8.5 21 8.5 21 15.5 17.5 15.5 14 15.5 10 8.5 6.5 8.5z""/>",
            ["calendar"] = @"<rect x=""3.5"" y=""5"" width=""17"" height=""15"" rx=""2""/><path d=""M3.5 10h17M8 3v4M16 3v4""/>",
            ["pen"] = @"<path d=""M4 20l4.5-1L19 8.5 15.5 5 5 15.5z""/><path d=""M13.5 7l3.5 3.5""/>",
            ["dice"] = @"<rect x=""4"" y=""4"" width=""16"" height=""16"" rx=""3.5""/><circle cx=""9"" cy=""9"" r=""1.2"" fill=""currentColor""/><circle cx=""15"" cy=""15"" r=""1.2"" fill=""currentColor""/><circle cx=""15"" cy=""9"" r=""1.2"" fill=""currentColor""/><circle cx=""9"" cy=""15"" r=""1.2"" fill=""currentColor""/>",
            ["copy"] = @"<rect x=""8"" y=""8"" width=""12"" height=""12"" rx=""2""/><path d=""M16 8V5.5A1.5 1.5 0 0 0 14.5 4h-9A1.5 1.5 0 0 0 4 5.5v9A1.5 1.5 0 0 0 5.5 16H8""/>",
            ["back"] = @"<path d=""M19 12H6M11 6l-6 6 6 6""/>",
            ["lock"] = @"<rect x=""5"" y=""10.5"" width=""14"" height=""10"" rx=""2""/><path d=""M8 10.5V8a4 4 0 0 1 8 0v2.5""/>",
            ["resume"] = @"<path d=""M6 4.5v15M10 4.5v15l10-7.5z""/>",
            ["minimap"] = @"<rect x=""3.5"" y=""3.5"" width=""17"" height=""17"" rx=""2""/><path d=""M3.5 14l5-4 4 3 3-2.5 5 4""/>",
        };

        /// <summary>Alle Symbolnamen aus ui/icons.ts.</summary>
        public static IEnumerable<string> Names => P.Keys;

        public const int SIZE = 64;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Symbol als Sprite (weiß, per Image.color einfärben). Unbekannte Namen → „help“ (wie svgIcon).</summary>
        public static Sprite Get(string name)
        {
            if (name == null || !P.ContainsKey(name)) name = "help";
            if (cache.TryGetValue(name, out var s) && s != null) return s;
            Texture2D tex;
            try { tex = Rasterize(P[name], SIZE); }
            catch (Exception e)
            {
                Debug.LogWarning($"[Icons] Symbol '{name}' konnte nicht gezeichnet werden: {e.Message}");
                tex = Blank(SIZE);
            }
            tex.name = "icon_" + name;
            s = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            s.name = tex.name;
            cache[name] = s;
            return s;
        }

        /// <summary>Entspricht svgIcon(name) im Webspiel: Image-Komponente mit Symbol.</summary>
        public static UnityEngine.UI.Image svgIcon(Transform parent, string name, float size = 22, Color? color = null)
        {
            var go = Ui.Node("Icon_" + name, parent);
            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.sprite = Get(name);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = color ?? Theme.Text;
            Ui.Size(img, size, size);
            Ui.Rt(go).sizeDelta = new Vector2(size, size);
            return img;
        }

        static Texture2D Blank(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        // ------------------------------------------------------------ Mini-SVG: Parser + Rasterisierer

        sealed class Shape
        {
            public readonly List<List<Vector2>> subs = new List<List<Vector2>>();
            public readonly List<bool> closed = new List<bool>();
            public Color? fill;
            public Color? stroke;
            public float strokeWidth = 2f;
        }

        static readonly Regex ElemRx = new Regex(@"<(path|circle|ellipse|rect)\b([^>]*?)/?>", RegexOptions.Compiled);
        static readonly Regex AttrRx = new Regex(@"([\w-]+)\s*=\s*""([^""]*)""", RegexOptions.Compiled);
        static readonly Regex TokRx = new Regex(@"[MmLlHhVvCcSsQqTtAaZz]|[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static Color? ParsePaint(string v, Color? inherited)
        {
            if (v == null) return inherited;
            v = v.Trim();
            if (v == "none") return null;
            if (v == "currentColor") return Color.white;
            if (ColorUtility.TryParseHtmlString(v, out var c)) return c;
            return inherited;
        }

        static List<Shape> Parse(string svg)
        {
            var list = new List<Shape>();
            foreach (Match em in ElemRx.Matches(svg))
            {
                var attrs = new Dictionary<string, string>();
                foreach (Match am in AttrRx.Matches(em.Groups[2].Value)) attrs[am.Groups[1].Value] = am.Groups[2].Value;
                string A(string k) => attrs.TryGetValue(k, out var v) ? v : null;
                float N(string k, float d = 0) => attrs.TryGetValue(k, out var v) ? F(v) : d;
                // Wurzel-Vorgaben aus svgIcon(): fill="none", stroke="currentColor", stroke-width="2"
                var sh = new Shape
                {
                    fill = ParsePaint(A("fill"), null),
                    stroke = ParsePaint(A("stroke"), Color.white),
                    strokeWidth = attrs.ContainsKey("stroke-width") ? N("stroke-width") : 2f,
                };
                switch (em.Groups[1].Value)
                {
                    case "path": PathData(A("d") ?? "", sh); break;
                    case "circle": Ellipse(sh, N("cx"), N("cy"), N("r"), N("r")); break;
                    case "ellipse": Ellipse(sh, N("cx"), N("cy"), N("rx"), N("ry")); break;
                    case "rect": RoundRect(sh, N("x"), N("y"), N("width"), N("height"), N("rx", attrs.ContainsKey("ry") ? N("ry") : 0)); break;
                }
                list.Add(sh);
            }
            return list;
        }

        static void Ellipse(Shape sh, float cx, float cy, float rx, float ry)
        {
            var pts = new List<Vector2>();
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) * 8), 16, 96);
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            sh.subs.Add(pts);
            sh.closed.Add(true);
        }

        static void RoundRect(Shape sh, float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w / 2, h / 2);
            var pts = new List<Vector2>();
            void Corner(float cx, float cy, float a0)
            {
                if (r <= 0) { pts.Add(new Vector2(cx, cy)); return; }
                for (int i = 0; i <= 6; i++)
                {
                    float a = a0 + i * (Mathf.PI / 2) / 6;
                    pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
            }
            if (r <= 0)
            {
                pts.Add(new Vector2(x, y)); pts.Add(new Vector2(x + w, y)); pts.Add(new Vector2(x + w, y + h)); pts.Add(new Vector2(x, y + h));
            }
            else
            {
                Corner(x + w - r, y + r, -Mathf.PI / 2);
                Corner(x + w - r, y + h - r, 0);
                Corner(x + r, y + h - r, Mathf.PI / 2);
                Corner(x + r, y + r, Mathf.PI);
            }
            sh.subs.Add(pts);
            sh.closed.Add(true);
        }

        static void PathData(string d, Shape sh)
        {
            var toks = new List<string>();
            foreach (Match m in TokRx.Matches(d)) toks.Add(m.Value);
            int i = 0;
            char cmd = 'M';
            Vector2 cur = Vector2.zero, start = Vector2.zero, lastCtrl = Vector2.zero;
            char lastCmd = ' ';
            List<Vector2> sub = null;
            bool IsCmd(int k) => k < toks.Count && toks[k].Length == 1 && char.IsLetter(toks[k][0]);
            float Num() => F(toks[i++]);
            void Begin(Vector2 p)
            {
                if (sub != null && sub.Count > 1) { sh.subs.Add(sub); sh.closed.Add(false); }
                sub = new List<Vector2> { p };
            }
            void LineTo(Vector2 p) { if (sub == null) sub = new List<Vector2> { cur }; sub.Add(p); }
            while (i < toks.Count)
            {
                if (IsCmd(i)) cmd = toks[i++][0];
                else if (cmd == 'M') cmd = 'L';
                else if (cmd == 'm') cmd = 'l';
                bool rel = char.IsLower(cmd);
                Vector2 o = rel ? cur : Vector2.zero;
                switch (char.ToUpperInvariant(cmd))
                {
                    case 'M':
                        {
                            var p = new Vector2(Num(), Num()) + o;
                            Begin(p); cur = start = p;
                            break;
                        }
                    case 'L':
                        {
                            var p = new Vector2(Num(), Num()) + o;
                            LineTo(p); cur = p;
                            break;
                        }
                    case 'H':
                        {
                            float x = Num() + (rel ? cur.x : 0);
                            var p = new Vector2(x, cur.y); LineTo(p); cur = p;
                            break;
                        }
                    case 'V':
                        {
                            float y = Num() + (rel ? cur.y : 0);
                            var p = new Vector2(cur.x, y); LineTo(p); cur = p;
                            break;
                        }
                    case 'C':
                        {
                            var c1 = new Vector2(Num(), Num()) + o;
                            var c2 = new Vector2(Num(), Num()) + o;
                            var p = new Vector2(Num(), Num()) + o;
                            Cubic(cur, c1, c2, p, LineTo);
                            lastCtrl = c2; cur = p;
                            break;
                        }
                    case 'S':
                        {
                            char lc = char.ToUpperInvariant(lastCmd);
                            var c1 = (lc == 'C' || lc == 'S') ? cur * 2 - lastCtrl : cur;
                            var c2 = new Vector2(Num(), Num()) + o;
                            var p = new Vector2(Num(), Num()) + o;
                            Cubic(cur, c1, c2, p, LineTo);
                            lastCtrl = c2; cur = p;
                            break;
                        }
                    case 'Q':
                        {
                            var c = new Vector2(Num(), Num()) + o;
                            var p = new Vector2(Num(), Num()) + o;
                            Quad(cur, c, p, LineTo);
                            lastCtrl = c; cur = p;
                            break;
                        }
                    case 'T':
                        {
                            char lc = char.ToUpperInvariant(lastCmd);
                            var c = (lc == 'Q' || lc == 'T') ? cur * 2 - lastCtrl : cur;
                            var p = new Vector2(Num(), Num()) + o;
                            Quad(cur, c, p, LineTo);
                            lastCtrl = c; cur = p;
                            break;
                        }
                    case 'A':
                        {
                            float rx = Num(), ry = Num(), rot = Num();
                            bool large = Num() != 0, sweep = Num() != 0;
                            var p = new Vector2(Num(), Num()) + o;
                            Arc(cur, rx, ry, rot, large, sweep, p, LineTo);
                            cur = p;
                            break;
                        }
                    case 'Z':
                        {
                            if (sub != null && sub.Count > 1) { sh.subs.Add(sub); sh.closed.Add(true); }
                            sub = null;
                            cur = start;
                            break;
                        }
                    default: i++; break;
                }
                lastCmd = cmd;
                // Ein Befehl ohne Zahlen (Z) darf die Schleife nicht blockieren
                if (char.ToUpperInvariant(cmd) == 'Z' && i < toks.Count && !IsCmd(i)) cmd = 'L';
            }
            if (sub != null && sub.Count > 1) { sh.subs.Add(sub); sh.closed.Add(false); }
        }

        static void Cubic(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p3, Action<Vector2> add)
        {
            const int n = 14;
            for (int k = 1; k <= n; k++)
            {
                float t = k / (float)n, u = 1 - t;
                add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p3);
            }
        }

        static void Quad(Vector2 p0, Vector2 c, Vector2 p2, Action<Vector2> add)
        {
            const int n = 12;
            for (int k = 1; k <= n; k++)
            {
                float t = k / (float)n, u = 1 - t;
                add(u * u * p0 + 2 * u * t * c + t * t * p2);
            }
        }

        /// <summary>SVG-Bogen (Endpunkt-Parametrisierung → Mittelpunkt), als Polylinie.</summary>
        static void Arc(Vector2 p0, float rx, float ry, float rotDeg, bool large, bool sweep, Vector2 p1, Action<Vector2> add)
        {
            if (rx == 0 || ry == 0 || p0 == p1) { add(p1); return; }
            rx = Mathf.Abs(rx); ry = Mathf.Abs(ry);
            float phi = rotDeg * Mathf.Deg2Rad, cphi = Mathf.Cos(phi), sphi = Mathf.Sin(phi);
            float dx = (p0.x - p1.x) / 2, dy = (p0.y - p1.y) / 2;
            float x1 = cphi * dx + sphi * dy, y1 = -sphi * dx + cphi * dy;
            float lam = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lam > 1) { float sq = Mathf.Sqrt(lam); rx *= sq; ry *= sq; }
            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float coef = Mathf.Sqrt(Mathf.Max(0, num / den)) * (large == sweep ? -1 : 1);
            float cxp = coef * rx * y1 / ry, cyp = -coef * ry * x1 / rx;
            float cx = cphi * cxp - sphi * cyp + (p0.x + p1.x) / 2;
            float cy = sphi * cxp + cphi * cyp + (p0.y + p1.y) / 2;
            float Ang(float ux, float uy, float vx, float vy)
            {
                float a = Mathf.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
                return a;
            }
            float th1 = Ang(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry);
            float dth = Ang((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!sweep && dth > 0) dth -= Mathf.PI * 2;
            else if (sweep && dth < 0) dth += Mathf.PI * 2;
            int n = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(dth) / (Mathf.PI / 16) * Mathf.Max(1, Mathf.Max(rx, ry) / 6)), 4, 64);
            for (int k = 1; k <= n; k++)
            {
                float t = th1 + dth * k / n;
                float ex = rx * Mathf.Cos(t), ey = ry * Mathf.Sin(t);
                add(k == n ? p1 : new Vector2(cphi * ex - sphi * ey + cx, sphi * ex + cphi * ey + cy));
            }
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l2 = ab.sqrMagnitude;
            float t = l2 > 1e-9f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0;
            return (a + ab * t - p).magnitude;
        }

        static Texture2D Rasterize(string svg, int size)
        {
            var shapes = Parse(svg);
            float sc = size / 24f;
            var acc = new Color[size * size]; // vormultipliziert
            foreach (var sh in shapes)
            {
                // In Pixelkoordinaten umrechnen (SVG: y nach unten; Textur: y nach oben)
                var subs = new List<Vector2[]>();
                for (int k = 0; k < sh.subs.Count; k++)
                {
                    var src = sh.subs[k];
                    var arr = new Vector2[src.Count];
                    for (int q = 0; q < src.Count; q++) arr[q] = new Vector2(src[q].x * sc, size - src[q].y * sc);
                    subs.Add(arr);
                }
                float hw = sh.strokeWidth * sc / 2f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        if (sh.fill.HasValue)
                        {
                            int wind = 0;
                            float dmin = float.MaxValue;
                            foreach (var poly in subs)
                            {
                                int n = poly.Length;
                                for (int e = 0; e < n; e++)
                                {
                                    Vector2 a = poly[e], b = poly[(e + 1) % n];
                                    dmin = Mathf.Min(dmin, SegDist(p, a, b));
                                    if (a.y <= p.y)
                                    {
                                        if (b.y > p.y && Cross(a, b, p) > 0) wind++;
                                    }
                                    else if (b.y <= p.y && Cross(a, b, p) < 0) wind--;
                                }
                            }
                            float cov = wind != 0 ? Mathf.Clamp01(0.5f + dmin) : Mathf.Clamp01(0.5f - dmin);
                            if (cov > 0) Over(acc, y * size + x, sh.fill.Value, cov);
                        }
                        if (sh.stroke.HasValue && hw > 0)
                        {
                            float dmin = float.MaxValue;
                            for (int k = 0; k < subs.Count; k++)
                            {
                                var poly = subs[k];
                                int n = poly.Length;
                                int segs = sh.closed[k] ? n : n - 1;
                                for (int e = 0; e < segs; e++) dmin = Mathf.Min(dmin, SegDist(p, poly[e], poly[(e + 1) % n]));
                            }
                            float cov = Mathf.Clamp01(hw - dmin + 0.5f);
                            if (cov > 0) Over(acc, y * size + x, sh.stroke.Value, cov);
                        }
                    }
            }
            var px = new Color32[size * size];
            for (int k = 0; k < px.Length; k++)
            {
                var c = acc[k];
                float a = Mathf.Clamp01(c.a);
                px[k] = a > 0.0001f ? (Color32)new Color(c.r / a, c.g / a, c.b / a, a) : new Color32(255, 255, 255, 0);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        static float Cross(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);

        static void Over(Color[] acc, int i, Color c, float cov)
        {
            float a = c.a * cov;
            var d = acc[i];
            acc[i] = new Color(c.r * a + d.r * (1 - a), c.g * a + d.g * (1 - a), c.b * a + d.b * (1 - a), a + d.a * (1 - a));
        }

        // ------------------------------------------------------------ Modell-Porträts (Port von render/IconFactory.ts)

        /// <summary>Isolierte Ebene für die Porträtaufnahmen (wird von keiner Spielkamera gerendert).</summary>
        public const int ICON_LAYER = 31;
        static readonly Dictionary<string, Sprite> modelCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Porträt eines Katalog-Modells (Turm, Gegner, Boss, Held). Wird einmal in eine Render-Textur aufgenommen
        /// und zwischengespeichert. Fehlt das Modell, gibt es einen farbigen Kreis mit dem Anfangsbuchstaben.
        /// </summary>
        public static Sprite Model(string modelId, int size = 128)
        {
            string key = $"{modelId}:{size}";
            if (modelCache.TryGetValue(key, out var hit) && hit != null) return hit;
            Sprite s = null;
            try
            {
                var assets = GameAssets.Instance;
                var cat = assets != null ? assets.catalog : null;
                var entry = cat != null ? cat.Find(modelId) : null;
                if (entry == null && cat != null) entry = cat.Find(WorldModels.champModel(modelId));
                if (entry == null && cat != null) entry = cat.Find(WorldModels.heroModel(modelId));
                if (entry != null && entry.prefab != null) s = RenderPortrait(entry, size);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Icons] Porträt für '{modelId}' fehlgeschlagen: {e.Message}");
                s = null;
            }
            if (s == null) s = Fallback(modelId, size);
            modelCache[key] = s;
            return s;
        }

        /// <summary>Verwirft alle Modellporträts (z. B. nach einem Skin-Wechsel).</summary>
        public static void ClearModelCache()
        {
            modelCache.Clear();
        }

        static Sprite RenderPortrait(ModelCatalog.Entry entry, int size)
        {
            // Blickwinkel wie IconFactory.render(): isometrische Neigung, leicht gedreht; Modell je Art verdreht
            float yaw = entry.category == ModelCategory.Tower ? -0.2f
                : entry.category == ModelCategory.Enemy ? 0.5f
                : entry.category == ModelCategory.Boss ? 0.4f
                : entry.category == ModelCategory.Hero ? 0.55f : 0.3f;
            var rig = new GameObject("KW_IconRig") { hideFlags = HideFlags.HideAndDontSave };
            rig.transform.position = new Vector3(0, -5000, 0);
            GameObject inst = null;
            Camera cam = null;
            Light light = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            try
            {
                inst = UnityEngine.Object.Instantiate(entry.prefab, rig.transform);
                inst.transform.localPosition = Vector3.zero;
                // PORT: Drehsinn der Modelle in Unity (linkshändig) vs. WebGL – Vorzeichen des Gierwinkels ggf. anpassen.
                inst.transform.localRotation = Quaternion.Euler(0, -yaw * Mathf.Rad2Deg, 0);
                foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true)) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                SetLayer(inst.transform, ICON_LAYER);

                Bounds b = default;
                bool any = false;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
                if (!any) return null;

                float height = Mathf.Max(0.05f, b.size.y);
                float horiz = Mathf.Max(b.extents.x, b.extents.z);
                var center = new Vector3(b.center.x, b.min.y + height * 0.48f, b.center.z);
                float ext = Mathf.Max(Mathf.Max(0.62f * Mathf.Max(height, horiz * 1.2f) / 1.15f, height * 0.62f), horiz * 1.15f);

                float pitch = Mathf.Atan(1f / Mathf.Sqrt(2f));
                float cyaw = Mathf.PI / 4 + 0.35f;
                var tc = new Vector3(Mathf.Cos(pitch) * Mathf.Sin(cyaw), Mathf.Sin(pitch), -Mathf.Cos(pitch) * Mathf.Cos(cyaw));
                float dist = 20f + b.extents.magnitude * 2f;

                var camGo = new GameObject("KW_IconCam") { hideFlags = HideFlags.HideAndDontSave };
                camGo.transform.SetParent(rig.transform, false);
                cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = ext;
                cam.cullingMask = 1 << ICON_LAYER;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = dist * 2f + 10f;
                cam.allowHDR = false;
                cam.allowMSAA = true;
                cam.transform.position = center + tc * dist;
                cam.transform.LookAt(center, Vector3.up);

                var lightGo = new GameObject("KW_IconLight") { hideFlags = HideFlags.HideAndDontSave };
                lightGo.transform.SetParent(rig.transform, false);
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.9f);
                light.intensity = 1.25f;
                light.cullingMask = 1 << ICON_LAYER;
                light.shadows = LightShadows.None;
                // LIGHT.light = [-0.5, 0.8, 0.35] (Richtung zur Lichtquelle)
                light.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.5f, 0.8f, -0.35f).normalized);
                // Kantenlicht von hinten oben: hebt dunkle Modelle vom dunklen Knopfgrund ab
                var rimGo = new GameObject("KW_IconRim") { hideFlags = HideFlags.HideAndDontSave };
                rimGo.transform.SetParent(rig.transform, false);
                var rim = rimGo.AddComponent<Light>();
                rim.type = LightType.Directional;
                rim.color = new Color(0.72f, 0.86f, 1f);
                rim.intensity = 0.9f;
                rim.cullingMask = 1 << ICON_LAYER;
                rim.shadows = LightShadows.None;
                rim.transform.rotation = Quaternion.LookRotation(new Vector3(tc.x, -0.55f, tc.z).normalized);

                var desc = new RenderTextureDescriptor(size, size, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear };
                rt = RenderTexture.GetTemporary(desc);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "portrait_" + entry.id };
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                tex.Apply(false, false);
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) cam.targetTexture = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(rig);
                if (inst != null) inst.SetActive(false);
            }
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        // ------------------------------------------------------------ Ersatzbild: farbiger Kreis mit Anfangsbuchstaben

        static readonly Dictionary<char, string[]> GLYPHS = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
        };

        static Sprite Fallback(string id, int size)
        {
            char ch = string.IsNullOrEmpty(id) ? '?' : char.ToUpperInvariant(id[0]);
            if (ch == 'Ä') ch = 'A'; else if (ch == 'Ö') ch = 'O'; else if (ch == 'Ü') ch = 'U';
            if (!GLYPHS.ContainsKey(ch)) ch = '?';
            var g = GLYPHS[ch];
            uint h = 2166136261u;
            foreach (char c in id ?? "") { h ^= c; h *= 16777619u; }
            var col = Color.HSVToRGB((h % 360) / 360f, 0.55f, 0.75f);
            var px = new Color32[size * size];
            float r = size * 0.46f, cx = size / 2f, cy = size / 2f;
            // Buchstabe: 5×7-Raster, mittig, mit weichen Kanten
            float cell = size * 0.085f;
            float gx0 = cx - 2.5f * cell, gy0 = cy + 3.5f * cell;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float d = Mathf.Sqrt((px0 - cx) * (px0 - cx) + (py0 - cy) * (py0 - cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a <= 0) { px[y * size + x] = new Color32(0, 0, 0, 0); continue; }
                    // Rand etwas heller
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(d - (r - 2.5f)) / 2f);
                    Color c = Color.Lerp(col, Color.white, rim * 0.35f);
                    int gx = Mathf.FloorToInt((px0 - gx0) / cell);
                    int gy = Mathf.FloorToInt((gy0 - py0) / cell);
                    if (gx >= 0 && gx < 5 && gy >= 0 && gy < 7 && g[gy][gx] == '#') c = Color.white;
                    c.a = a;
                    px[y * size + x] = c;
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave, name = "fallback_" + id };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
