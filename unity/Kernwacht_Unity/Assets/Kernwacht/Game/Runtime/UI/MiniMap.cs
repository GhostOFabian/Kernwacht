using System;
using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Port von ui/MiniMap.ts: 2D-Draufsicht einer Karte (Welt-Vorschau, Karteneditor). Formen zusätzlich zu Farben:
// Start = Dreieck, Ziel = Raute, Goldader = Kreis, Leylinie = Ring, Hindernis = dunkles Quadrat mit Kreuz.
// Statt eines Canvas2D-Kontexts zeichnet MiniMapCanvas in ein Color32-Feld, das als Texture2D hochgeladen wird.

namespace Kernwacht.Game.UI
{
    public class MiniMapOptions
    {
        public List<GridPos> route;
        public GridPos highlight;
        public bool grid;
    }

    public static class MiniMap
    {
        private static Color Hex(double c) { return Theme.Hex(c); }

        /// <summary>
        /// TS: drawMiniMap(canvas, map, opts) → Zellgröße in CSS-Pixeln. Die Breite kommt aus dem RectTransform des RawImage (0 → 320);
        /// die Höhe des RawImage wird angepasst (LayoutElement bzw. sizeDelta), die Textur wiederverwendet.
        /// </summary>
        public static double drawMiniMap(RawImage canvas, MapDefinition map, MiniMapOptions opts = null)
        {
            if (canvas == null || map == null) return 0;
            var rt = canvas.rectTransform;
            float cssW = rt.rect.width > 1 ? rt.rect.width : 320;
            int cell = Math.Max(4, (int)Math.Floor(cssW / map.width));
            int h = cell * (int)map.height;
            var le = canvas.GetComponent<LayoutElement>();
            if (le != null) { le.preferredHeight = h; le.minHeight = h; }
            else if (Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y)) rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            var tex = canvas.texture as Texture2D;
            canvas.texture = render(map, cell, opts, tex);
            canvas.uvRect = new Rect(0, 0, 1, 1);
            return cell;
        }

        /// <summary>Zeichnet die Karte in eine Textur (cell = CSS-px je Feld; dpr = Pixel je CSS-px, wie devicePixelRatio).</summary>
        public static Texture2D render(MapDefinition map, int cell, MiniMapOptions opts = null, Texture2D reuse = null, int dpr = 2)
        {
            opts = opts ?? new MiniMapOptions();
            int W = (int)map.width, H = (int)map.height;
            int w = cell * W, h = cell * H;
            var ctx = new MiniMapCanvas(w, h, dpr);
            var pal = Biomes.BIOMES[map.biome].palette;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    double c = map.cells[i];
                    double col = (x + y) % 2 != 0 ? pal.ground : pal.ground2;
                    if (c == Cell.Path || c == Cell.Spawn || c == Cell.Goal) col = pal.path;
                    else if (c == Cell.Blocked) col = pal.obstacle;
                    else if (c == Cell.NoBuild) col = pal.cliff;
                    else if (map.heights[i] >= 2) col = pal.high;
                    ctx.fillRect(x * cell, y * cell, cell, cell, Hex(col));
                    if (c == Cell.Blocked)
                    {
                        var k = new Color(0, 0, 0, 0.45f);
                        ctx.line(x * cell + 2, y * cell + 2, x * cell + cell - 2, y * cell + cell - 2, 1, k);
                        ctx.line(x * cell + cell - 2, y * cell + 2, x * cell + 2, y * cell + cell - 2, 1, k);
                    }
                    if (c == Cell.NoBuild) ctx.fillRect(x * cell + cell * 0.4f, y * cell + cell * 0.4f, cell * 0.2f, cell * 0.2f, new Color(1, 1, 1, 0.18f));
                }
            }
            if (opts.grid)
            {
                var g = new Color(0, 0, 0, 0.18f);
                for (int x = 0; x <= W; x++) ctx.line(x * cell + 0.5f, 0, x * cell + 0.5f, h, 1, g);
                for (int y = 0; y <= H; y++) ctx.line(0, y * cell + 0.5f, w, y * cell + 0.5f, 1, g);
            }
            foreach (double i in map.veins)
                ctx.fillCircle((float)((i % W + 0.5) * cell), (float)((Math.Floor(i / W) + 0.5) * cell), cell * 0.28f, Theme.Hex(0xf0c040));
            if (map.deposits != null)
                foreach (double i in map.deposits)
                    ctx.fillCircle((float)((i % W + 0.5) * cell), (float)((Math.Floor(i / W) + 0.5) * cell), cell * 0.28f, Theme.Hex(0x7ae0ff));
            foreach (double i in map.ley)
                ctx.strokeCircle((float)((i % W + 0.5) * cell), (float)((Math.Floor(i / W) + 0.5) * cell), cell * 0.3f, Math.Max(1.5f, cell * 0.12f), Theme.Hex(0xc070ff));
            if (opts.route != null && opts.route.Count > 1)
            {
                var pts = new List<Vector2>(opts.route.Count);
                foreach (var p in opts.route) pts.Add(new Vector2((float)((p.x + 0.5) * cell), (float)((p.y + 0.5) * cell)));
                ctx.dashedPath(pts, Math.Max(1.5f, cell * 0.14f), cell * 0.3f, cell * 0.25f, new Color(1f, 240 / 255f, 160 / 255f, 0.85f));
            }
            // Start (Dreieck) und Ziel (Raute)
            var s = map.spawn;
            if (s != null)
                ctx.fillPolygon(new[]
                {
                    new Vector2((float)((s.x + 0.15) * cell), (float)((s.y + 0.15) * cell)),
                    new Vector2((float)((s.x + 0.9) * cell), (float)((s.y + 0.5) * cell)),
                    new Vector2((float)((s.x + 0.15) * cell), (float)((s.y + 0.85) * cell)),
                }, Theme.Hex(0xc060ff));
            var gl = map.goal;
            if (gl != null)
                ctx.fillPolygon(new[]
                {
                    new Vector2((float)((gl.x + 0.5) * cell), (float)((gl.y + 0.08) * cell)),
                    new Vector2((float)((gl.x + 0.92) * cell), (float)((gl.y + 0.5) * cell)),
                    new Vector2((float)((gl.x + 0.5) * cell), (float)((gl.y + 0.92) * cell)),
                    new Vector2((float)((gl.x + 0.08) * cell), (float)((gl.y + 0.5) * cell)),
                }, Theme.Hex(0x60f0ff));
            if (opts.highlight != null)
                ctx.strokeRect((float)(opts.highlight.x * cell + 1), (float)(opts.highlight.y * cell + 1), cell - 2, cell - 2, 2, Color.white);
            return ctx.upload(reuse);
        }

        /// <summary>Zelle unter einem Punkt im RawImage (Bildschirmkoordinaten aus einem PointerEvent) – für den Karteneditor.</summary>
        public static bool cellAt(RawImage canvas, MapDefinition map, Vector2 screenPos, Camera eventCamera, out int cx, out int cy)
        {
            cx = cy = -1;
            Vector2 local;
            var rt = canvas.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, eventCamera, out local)) return false;
            var r = rt.rect;
            float u = (local.x - r.xMin) / r.width, v = 1 - (local.y - r.yMin) / r.height;
            cx = Mathf.FloorToInt(u * (float)map.width);
            cy = Mathf.FloorToInt(v * (float)map.height);
            return cx >= 0 && cy >= 0 && cx < map.width && cy < map.height;
        }
    }

    /// <summary>
    /// Minimale Canvas2D-Nachbildung: Koordinaten in CSS-Pixeln, Ursprung oben links; Kanten weich (≈1 px Antialiasing).
    /// </summary>
    public sealed class MiniMapCanvas
    {
        public readonly int width, height, scale;
        private readonly int pw, ph;
        private readonly Color[] px;

        public MiniMapCanvas(int cssW, int cssH, int dpr = 1)
        {
            width = Math.Max(1, cssW); height = Math.Max(1, cssH); scale = Math.Max(1, dpr);
            pw = width * scale; ph = height * scale;
            px = new Color[pw * ph];
        }

        public void clear(Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }

        private void blend(int x, int y, Color c, float cov)
        {
            if (x < 0 || y < 0 || x >= pw || y >= ph) return;
            float a = c.a * cov;
            if (a <= 0) return;
            int i = y * pw + x;
            var d = px[i];
            float oa = a + d.a * (1 - a);
            if (oa <= 0) return;
            px[i] = new Color((c.r * a + d.r * d.a * (1 - a)) / oa, (c.g * a + d.g * d.a * (1 - a)) / oa, (c.b * a + d.b * d.a * (1 - a)) / oa, oa);
        }

        public void fillRect(float x, float y, float w, float h, Color c)
        {
            int x0 = Mathf.RoundToInt(x * scale), y0 = Mathf.RoundToInt(y * scale);
            int x1 = Mathf.RoundToInt((x + w) * scale), y1 = Mathf.RoundToInt((y + h) * scale);
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(pw, x1); y1 = Math.Min(ph, y1);
            bool opaque = c.a >= 0.999f;
            for (int yy = y0; yy < y1; yy++)
                for (int xx = x0; xx < x1; xx++)
                {
                    if (opaque) px[yy * pw + xx] = c;
                    else blend(xx, yy, c, 1);
                }
        }

        public void strokeRect(float x, float y, float w, float h, float lw, Color c)
        {
            line(x, y, x + w, y, lw, c, false);
            line(x + w, y, x + w, y + h, lw, c, false);
            line(x + w, y + h, x, y + h, lw, c, false);
            line(x, y + h, x, y, lw, c, false);
        }

        public void fillCircle(float cx, float cy, float r, Color c)
        {
            float s = scale;
            float pcx = cx * s, pcy = cy * s, pr = r * s;
            int x0 = Mathf.FloorToInt(pcx - pr - 1), x1 = Mathf.CeilToInt(pcx + pr + 1);
            int y0 = Mathf.FloorToInt(pcy - pr - 1), y1 = Mathf.CeilToInt(pcy + pr + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - pcx, dy = y + 0.5f - pcy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float cov = Mathf.Clamp01(pr - d + 0.5f);
                    if (cov > 0) blend(x, y, c, cov);
                }
        }

        public void strokeCircle(float cx, float cy, float r, float lw, Color c)
        {
            float s = scale;
            float pcx = cx * s, pcy = cy * s, pr = r * s, half = lw * s * 0.5f;
            int x0 = Mathf.FloorToInt(pcx - pr - half - 1), x1 = Mathf.CeilToInt(pcx + pr + half + 1);
            int y0 = Mathf.FloorToInt(pcy - pr - half - 1), y1 = Mathf.CeilToInt(pcy + pr + half + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - pcx, dy = y + 0.5f - pcy;
                    float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - pr);
                    float cov = Mathf.Clamp01(half - d + 0.5f);
                    if (cov > 0) blend(x, y, c, cov);
                }
        }

        /// <summary>Linie (Kappe flach wie Canvas „butt“; round=true für runde Enden).</summary>
        public void line(float ax, float ay, float bx, float by, float lw, Color c, bool round = false)
        {
            float s = scale;
            ax *= s; ay *= s; bx *= s; by *= s;
            float half = Mathf.Max(0.5f, lw * s * 0.5f);
            int x0 = Mathf.FloorToInt(Mathf.Min(ax, bx) - half - 1), x1 = Mathf.CeilToInt(Mathf.Max(ax, bx) + half + 1);
            int y0 = Mathf.FloorToInt(Mathf.Min(ay, by) - half - 1), y1 = Mathf.CeilToInt(Mathf.Max(ay, by) + half + 1);
            x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(pw - 1, x1); y1 = Math.Min(ph - 1, y1);
            float vx = bx - ax, vy = by - ay;
            float len2 = vx * vx + vy * vy;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float qx = x + 0.5f - ax, qy = y + 0.5f - ay;
                    float t = len2 > 0 ? (qx * vx + qy * vy) / len2 : 0;
                    float cov;
                    if (!round && (t < 0 || t > 1))
                    {
                        float len = Mathf.Sqrt(len2);
                        float over = (t < 0 ? -t : t - 1) * len;
                        if (over > 0.5f) continue;
                    }
                    float tc = Mathf.Clamp01(t);
                    float dx = qx - vx * tc, dy = qy - vy * tc;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    cov = Mathf.Clamp01(half - d + 0.5f);
                    if (cov > 0) blend(x, y, c, cov);
                }
        }

        /// <summary>Polylinie mit Strichmuster (setLineDash([dash, gap])).</summary>
        public void dashedPath(List<Vector2> pts, float lw, float dash, float gap, Color c)
        {
            float phase = 0;   // Position im Muster
            float period = dash + gap;
            for (int k = 1; k < pts.Count; k++)
            {
                Vector2 a = pts[k - 1], b = pts[k];
                float segLen = Vector2.Distance(a, b);
                float pos = 0;
                while (pos < segLen)
                {
                    float inPat = phase % period;
                    bool on = inPat < dash;
                    float step = Mathf.Min(segLen - pos, on ? dash - inPat : period - inPat);
                    if (on && step > 0)
                    {
                        Vector2 p0 = Vector2.Lerp(a, b, pos / segLen), p1 = Vector2.Lerp(a, b, (pos + step) / segLen);
                        line(p0.x, p0.y, p1.x, p1.y, lw, c);
                    }
                    pos += Mathf.Max(step, 0.0001f);
                    phase += Mathf.Max(step, 0.0001f);
                }
            }
        }

        public void polyline(IList<Vector2> pts, bool closed, float lw, Color c)
        {
            for (int k = 1; k < pts.Count; k++) line(pts[k - 1].x, pts[k - 1].y, pts[k].x, pts[k].y, lw, c, true);
            if (closed && pts.Count > 2) line(pts[pts.Count - 1].x, pts[pts.Count - 1].y, pts[0].x, pts[0].y, lw, c, true);
        }

        public void fillPolygon(IList<Vector2> pts, Color c)
        {
            if (pts.Count < 3) return;
            float s = scale;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x * s); maxX = Mathf.Max(maxX, p.x * s); minY = Mathf.Min(minY, p.y * s); maxY = Mathf.Max(maxY, p.y * s); }
            int x0 = Math.Max(0, Mathf.FloorToInt(minX)), x1 = Math.Min(pw - 1, Mathf.CeilToInt(maxX));
            int y0 = Math.Max(0, Mathf.FloorToInt(minY)), y1 = Math.Min(ph - 1, Mathf.CeilToInt(maxY));
            // 2×2-Unterabtastung für weiche Kanten
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (inside(pts, (x + 0.25f + 0.5f * sx) / s, (y + 0.25f + 0.5f * sy) / s)) hits++;
                    if (hits > 0) blend(x, y, c, hits / 4f);
                }
        }

        private static bool inside(IList<Vector2> pts, float x, float y)
        {
            bool r = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) r = !r;
            }
            return r;
        }

        /// <summary>Kopiert ein anderes Bild gleicher Größe (drawImage der Grundkarte).</summary>
        public void copyFrom(MiniMapCanvas o)
        {
            if (o.px.Length == px.Length) Array.Copy(o.px, px, px.Length);
        }

        /// <summary>Lädt das Bild in eine Textur (wiederverwendet, wenn die Größe passt). Zeilen werden für Unity (unten = 0) gespiegelt.</summary>
        public Texture2D upload(Texture2D reuse = null)
        {
            var tex = reuse;
            if (tex == null || tex.width != pw || tex.height != ph)
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
                tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            }
            if (flipped == null || flipped.Length != px.Length) flipped = new Color32[px.Length];
            for (int y = 0; y < ph; y++)
            {
                int src = y * pw, dst = (ph - 1 - y) * pw;
                for (int x = 0; x < pw; x++) flipped[dst + x] = px[src + x];
            }
            tex.SetPixels32(flipped);
            tex.Apply(false);
            return tex;
        }

        private Color32[] flipped;
    }

    /// <summary>
    /// Laufende Minikarte im Spiel-HUD (Port von Hud.updateMinimap/miniClick aus ui/Hud.ts) zur Wiederverwendung:
    /// Gelände, Weg, Start/Ziel, Türme, Gegner, Helden, Champion und Laden (Helden-Endlos), Kameraausschnitt.
    /// Antippen/Ziehen ruft lookAt(x, z) in Feldkoordinaten auf (TS: HudActions.lookAt).
    /// </summary>
    public class LiveMiniMap : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public GameSimulation sim;
        public Action<double, double> lookAt;
        /// <summary>Kameraausschnitt in Weltkoordinaten (4 Ecken [x, z]), von der Sitzung gesetzt.</summary>
        public List<double[]> camQuad = new List<double[]>();
        public bool showMinimap = true;
        public RawImage image;

        private MiniMapCanvas baseCanvas, frame;
        private int baseW = -1;
        private double baseRev = double.NaN;
        private float miniTimer;
        private int cell = 3;

        public static LiveMiniMap create(Transform parent, GameSimulation sim, Action<double, double> lookAt)
        {
            var go = Ui.Node("MiniMap", parent);
            var bg = go.AddComponent<Image>();
            bg.sprite = Theme.RoundSmall; bg.type = Image.Type.Sliced; bg.color = Theme.Hex(0x0b0814, 0.92f);
            var o = go.AddComponent<Outline>(); o.effectColor = Theme.LineStrong; o.effectDistance = new Vector2(1, -1);
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.5f); sh.effectDistance = new Vector2(0, -4);
            Ui.Place(Ui.Rt(go), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(168, 140));
            var ig = Ui.Node("Img", go.transform);
            Ui.Fill(Ui.Rt(ig), 1);
            var raw = ig.AddComponent<RawImage>();
            raw.raycastTarget = false;
            raw.color = new Color(1, 1, 1, 0.92f);
            var m = go.AddComponent<LiveMiniMap>();
            m.sim = sim; m.lookAt = lookAt; m.image = raw;
            m.showMinimap = sim.s.map.width * sim.s.map.height > 320;
            return m;
        }

        /// <summary>Pro Frame aufrufen (TS: updateMinimap(dt)); zeichnet höchstens alle 0,15 s neu.</summary>
        public void tick(double dt)
        {
            if (gameObject.activeSelf != showMinimap) gameObject.SetActive(showMinimap);
            if (!showMinimap || sim == null) return;
            miniTimer -= (float)dt;
            if (miniTimer > 0) return;
            miniTimer = 0.15f;
            var s = sim.s;
            var m = s.map;
            cell = Math.Max(3, (int)Math.Floor(168 / Math.Max(m.width, m.height * 1.2)));
            int w = (int)m.width * cell, hh = (int)m.height * cell;
            double rev = m.rev ?? 0;
            if (baseCanvas == null || baseW != w || rev != baseRev)
            {
                baseW = w; baseRev = rev;
                var c = new MiniMapCanvas(w, hh, 1);
                var pal = Biomes.BIOMES[m.biome].palette;
                int W = (int)m.width;
                for (int y = 0; y < m.height; y++)
                    for (int x = 0; x < W; x++)
                    {
                        double cc = m.cells[y * W + x];
                        double col = cc == Cell.Path || cc == Cell.Spawn || cc == Cell.Goal ? pal.path : cc == Cell.Blocked ? pal.obstacle : m.heights[y * W + x] >= 2 ? pal.high : pal.ground;
                        c.fillRect(x * cell, y * cell, cell, cell, Theme.Hex(col));
                    }
                foreach (var sp in WorldGenerator.spawnsOf(m)) c.fillRect((float)(sp.x * cell - 1), (float)(sp.y * cell - 1), cell + 2, cell + 2, Theme.Hex(0xc060ff));
                c.fillRect((float)(m.goal.x * cell - 1), (float)(m.goal.y * cell - 1), cell + 2, cell + 2, Theme.Hex(0x60f0ff));
                baseCanvas = c;
                frame = new MiniMapCanvas(w, hh, 1);
                var rt = (RectTransform)transform;
                rt.sizeDelta = new Vector2(w + 2, hh + 2);
            }
            frame.copyFrom(baseCanvas);
            foreach (var t in s.towers)
                frame.fillRect((float)(t.cx * cell + 1), (float)(t.cy * cell + 1), cell - 2, cell - 2, Theme.Hex(Towers.TOWERS[t.def].color));
            foreach (var e in s.enemies)
            {
                if (!e.alive || e.stealthed) continue;
                bool boss = !string.IsNullOrEmpty(e.boss);
                var col = boss ? Theme.Hex(0xff3040) : e.elite ? Theme.Hex(0xffd84a) : Theme.Hex(0xff6a6a);
                float r = boss ? cell * 0.9f : cell * 0.35f;
                frame.fillCircle((float)(e.x * cell), (float)(e.y * cell), r, col);
            }
            foreach (var hero in s.heroes)
            {
                if (hero.state != "active") continue;
                frame.fillCircle((float)(hero.x * cell), (float)(hero.y * cell), cell * 0.6f + 0.5f, Color.black);
                frame.fillCircle((float)(hero.x * cell), (float)(hero.y * cell), cell * 0.6f, Theme.Hex(Heroes.HEROES[hero.id].accent));
            }
            // PORT: Zusatz gegenüber Hud.ts – Laden und Champion im Helden-Endlosmodus
            var he = s.he;
            if (he != null)
            {
                if (he.shop != null) frame.fillRect((float)(he.shop.x * cell - cell * 0.5), (float)(he.shop.y * cell - cell * 0.5), cell, cell, Theme.Gold);
                var ch = he.champion;
                if (ch != null && ch.dead <= 0)
                {
                    frame.fillCircle((float)(ch.x * cell), (float)(ch.y * cell), cell * 0.75f + 0.6f, Color.black);
                    frame.fillCircle((float)(ch.x * cell), (float)(ch.y * cell), cell * 0.75f, Theme.Hex(Champions.CHAMPIONS[ch.id].accent));
                }
            }
            if (camQuad != null && camQuad.Count == 4)
            {
                var pts = new Vector2[4];
                for (int i = 0; i < 4; i++) pts[i] = new Vector2((float)(camQuad[i][0] * cell), (float)(camQuad[i][1] * cell));
                frame.polyline(pts, true, 1.5f, new Color(1, 1, 1, 0.85f));
            }
            image.texture = frame.upload(image.texture as Texture2D);
        }

        public void OnPointerDown(PointerEventData e) { click(e); }
        public void OnDrag(PointerEventData e) { click(e); }

        private void click(PointerEventData e)
        {
            if (sim == null || lookAt == null) return;
            var rt = image != null ? image.rectTransform : (RectTransform)transform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out local)) return;
            var r = rt.rect;
            var m = sim.s.map;
            double x = (local.x - r.xMin) / r.width * m.width;
            double z = (1 - (local.y - r.yMin) / r.height) * m.height;
            lookAt(x, z);
        }

        void OnDestroy()
        {
            if (image != null && image.texture != null) Destroy(image.texture);
        }
    }
}
